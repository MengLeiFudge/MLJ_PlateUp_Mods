using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Kitchen;
using Kitchen.NetworkSupport;
using Kitchen.Transports;
using LiteNetLib;
using LiteNetLib.Utils;
using Steamworks;
using UnityEngine;
using SteamNet = Steamworks.SteamNetworking;

namespace PlateUpRelay
{
    /// <summary>
    /// 继承原 Steam 房间生命周期，只将对局消息改走独立可靠 UDP 中转。
    /// IsTargetConnected 沿用基类对待加入目标的保留语义；SendStatus 控制握手完成前暂停发送，
    /// 未完成的连接由游戏原有活性超时处理，避免异步入房刚开始就被判定为断开。
    /// </summary>
    internal sealed class RelaySteamTransport : SteamLobbyTransport
    {
        private static readonly HashSet<RelaySteamTransport> instances = new HashSet<RelaySteamTransport>();
        private readonly HashSet<ulong> ready = new HashSet<ulong>();
        private NetManager network;
        private NetPeer relay;
        private byte[] secret;
        private ulong lobbyId;
        private ulong ownerId;
        private int revision = -1;
        private float nextKeyRequest;
        private float nextConnect;
        private float failureUntil;

        public RelaySteamTransport(JoinCode code) : base(code) { }

        public static bool HasRemotePlayers
        {
            get
            {
                foreach (var transport in instances)
                    if (transport.CurrentLobby.Id.IsValid && transport.CurrentLobby.MemberCount > 1)
                        return true;
                return false;
            }
        }

        public override void Initialise()
        {
            base.Initialise();
            instances.Add(this);
        }

        public override void Dispose()
        {
            instances.Remove(this);
            StopRelay();
            base.Dispose();
        }

        public override void RequestDisconnect()
        {
            StopRelay();
            base.RequestDisconnect();
        }

        public override void RequestSuspend()
        {
            StopRelay();
            base.RequestSuspend();
        }

        public override TransportSendStatus SendStatus
        {
            get
            {
                if (RelaySettings.Enabled && (relay == null || relay.ConnectionState != LiteNetLib.ConnectionState.Connected ||
                    (!IsHosting && !ready.Contains(ownerId))))
                    return TransportSendStatus.NoSendNotConnected;
                return base.SendStatus;
            }
        }

        /// <summary>主线程轮询事件；未准备好的中转成员不提前暴露给游戏状态同步。</summary>
        public override void Update()
        {
            base.Update();
            if (!RelaySettings.Enabled)
            {
                StopRelay();
                RelaySettings.Status = "未启用中转";
                return;
            }
            if (LobbyStatus != LobbyTransportStatus.Ready)
            {
                StopRelay();
                RelaySettings.Status = "等待 Steam 房间";
                return;
            }
            if (Time.realtimeSinceStartup < failureUntil)
            {
                LobbyMembers.Clear();
                return;
            }
            try
            {
                if (lobbyId != (ulong)CurrentLobby.Id || ownerId != (ulong)CurrentLobby.Owner.Id || revision != RelaySettings.Revision)
                {
                    StopRelay();
                    lobbyId = CurrentLobby.Id;
                    ownerId = CurrentLobby.Owner.Id;
                    revision = RelaySettings.Revision;
                    if (ownerId == (ulong)SteamClient.SteamId)
                    {
                        secret = new byte[Protocol.SecretLength];
                        using (var random = RandomNumberGenerator.Create())
                            random.GetBytes(secret);
                    }
                    Debug.Log("[PlateUpRelay] Steam lobby ready; starting relay handshake as " + (IsHosting ? "host." : "client."));
                }
                PollBootstrap();
                if (secret == null)
                {
                    RequestKey();
                    RelaySettings.Status = "等待房主模组确认身份（房主也需开启中转）";
                }
                else
                {
                    EnsureConnection();
                    network?.PollEvents(256);
                    if (relay != null && relay.ConnectionState == LiteNetLib.ConnectionState.Connected)
                        RelaySettings.Status = !IsHosting && !ready.Contains(ownerId)
                            ? "服务器已连接，等待房主连接同一服务器"
                            : "服务器已连接 · 往返 " + relay.Ping + " ms";
                }
                LobbyMembers.RemoveAll(member => !ready.Contains((ulong)member.ID));
            }
            catch (Exception ex)
            {
                StopRelay();
                LobbyMembers.Clear();
                failureUntil = Time.realtimeSinceStartup + 3;
                RelaySettings.Status = "中转异常：" + ex.Message;
                Debug.LogError("[PlateUpRelay] " + ex);
            }
        }

        protected override TransportSendResult SendToClient(SteamNetworkTarget client, byte[] data)
        {
            if (!RelaySettings.Enabled)
                return base.SendToClient(client, data);
            if (client == null || data == null || data.Length == 0 || data.Length > Protocol.MaxPayload)
                return TransportSendResult.FailedMissingArgument;
            if (relay == null || !ready.Contains((ulong)client.ID))
                return TransportSendResult.FailedNotConnected;
            return Protocol.Send(relay, Protocol.Message(client.ID, data))
                ? TransportSendResult.Success : TransportSendResult.FailedNotConnected;
        }

        /// <summary>身份确认沿用 Steam 的已认证 P2P 通道，仅传递一次房间临时凭据。</summary>
        private void PollBootstrap()
        {
            for (int count = 0; count < 64 && SteamNet.IsP2PPacketAvailable(Protocol.SteamChannel); count++)
            {
                var packet = SteamNet.ReadP2PPacket(Protocol.SteamChannel);
                if (!packet.HasValue)
                    break;
                byte[] bytes = packet.Value.Data;
                if (bytes == null || (bytes.Length != 13 && bytes.Length != 13 + Protocol.SecretLength))
                    continue;
                ulong sender = packet.Value.SteamId;
                var reader = new NetDataReader(bytes);
                if (reader.GetUInt() != Protocol.Magic)
                    continue;
                byte kind = reader.GetByte();
                if (reader.GetULong() != lobbyId || !IsMember(sender))
                    continue;
                if (kind == Protocol.KeyRequest && reader.AvailableBytes == 0 && ownerId == (ulong)SteamClient.SteamId && secret != null)
                {
                    var reply = Bootstrap(Protocol.KeyReply);
                    reply.Put(secret);
                    if (!SteamNet.SendP2PPacket(packet.Value.SteamId, reply.CopyData(), nChannel: Protocol.SteamChannel))
                        Debug.LogWarning("[PlateUpRelay] Steam could not send room credentials; client will retry.");
                }
                else if (kind == Protocol.KeyReply && reader.AvailableBytes == Protocol.SecretLength && sender == ownerId && secret == null)
                {
                    secret = reader.GetRemainingBytes();
                    Debug.Log("[PlateUpRelay] Received room credentials from the Steam host.");
                }
            }
        }

        private NetDataWriter Bootstrap(byte kind)
        {
            var writer = new NetDataWriter();
            writer.Put(Protocol.Magic);
            writer.Put(kind);
            writer.Put(lobbyId);
            return writer;
        }

        private void RequestKey()
        {
            if (Time.realtimeSinceStartup < nextKeyRequest)
                return;
            nextKeyRequest = Time.realtimeSinceStartup + 2;
            SteamNet.SendP2PPacket((SteamId)ownerId, Bootstrap(Protocol.KeyRequest).CopyData(), nChannel: Protocol.SteamChannel);
        }

        private bool IsMember(ulong player)
        {
            foreach (var member in CurrentLobby.Members)
                if ((ulong)member.Id == player)
                    return true;
            return false;
        }

        private void EnsureConnection()
        {
            if (network == null)
            {
                var events = new EventBasedNetListener();
                events.PeerConnectedEvent += peer =>
                {
                    relay = peer;
                    Debug.Log("[PlateUpRelay] Connected to relay " + RelaySettings.Address);
                };
                events.PeerDisconnectedEvent += (peer, info) =>
                {
                    relay = null;
                    ready.Clear();
                    nextConnect = Time.realtimeSinceStartup + 2;
                    RelaySettings.Status = "中转未连接：" + info.Reason + "，稍后重试";
                    Debug.LogWarning("[PlateUpRelay] Relay disconnected: " + info.Reason);
                };
                events.NetworkReceiveEvent += Receive;
                events.NetworkErrorEvent += (endpoint, error) => RelaySettings.Status = "UDP 网络错误：" + error;
                events.ConnectionRequestEvent += request => request.Reject();
                network = Protocol.CreateManager(events);
                if (!network.Start())
                    throw new InvalidOperationException("无法打开本机 UDP socket");
            }
            if (relay == null && Time.realtimeSinceStartup >= nextConnect)
            {
                var connect = new NetDataWriter();
                connect.Put(Protocol.Magic);
                connect.Put(lobbyId);
                connect.Put(ownerId);
                connect.Put((ulong)SteamClient.SteamId);
                connect.Put(secret);
                relay = network.Connect(RelaySettings.Endpoint, connect);
                nextConnect = Time.realtimeSinceStartup + 5;
                RelaySettings.Status = "正在连接 " + RelaySettings.Address;
            }
        }

        private void Receive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
        {
            if (reader.AvailableBytes < 9 || reader.AvailableBytes > Protocol.MaxPayload + 9 ||
                channel != 0 || delivery != DeliveryMethod.ReliableOrdered)
            {
                peer.Disconnect();
                return;
            }
            byte kind = reader.GetByte();
            ulong source = reader.GetULong();
            if (kind == Protocol.Joined && reader.AvailableBytes == 0)
            {
                if (IsMember(source) && ready.Add(source) && !IsHosting && source == ownerId)
                    Debug.Log("[PlateUpRelay] Host is ready on the relay; game traffic can start.");
                return;
            }
            if (kind == Protocol.Left && reader.AvailableBytes == 0)
            {
                ready.Remove(source);
                return;
            }
            if (kind != Protocol.Data || reader.AvailableBytes == 0)
            {
                peer.Disconnect();
                return;
            }
            if (ready.Contains(source) && IsMember(source) && (IsHosting || source == ownerId))
                ReceiveData(new SteamNetworkTarget((SteamId)source), reader.GetRemainingBytes());
        }

        private void StopRelay()
        {
            if (network != null)
                network.Stop();
            network = null;
            relay = null;
            ready.Clear();
            if (secret != null)
                Array.Clear(secret, 0, secret.Length);
            secret = null;
            lobbyId = 0;
            ownerId = 0;
            nextConnect = 0;
            nextKeyRequest = 0;
        }
    }
}
