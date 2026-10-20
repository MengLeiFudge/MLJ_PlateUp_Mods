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
using Stopwatch = System.Diagnostics.Stopwatch;
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
        /// <summary>网络库已测得的服务器往返延迟；尚未收到样本时为空。</summary>
        private int? serverPing;
        /// <summary>房间测量每两秒发起一次，五秒无应答作废，十秒未更新不再显示旧值。</summary>
        private const float ProbeInterval = 2f;
        private const float ProbeTimeout = 5f;
        private const float SampleLifetime = 10f;
        private uint probeSequence;
        private uint pendingProbe;
        private long probeStarted;
        private float nextProbeAt;
        private int? roomPing;
        private float roomPingAt;

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

        /// <summary>Steam 房间状态与到房主的往返样本，不用服务器往返代替房间延迟。</summary>
        public static RelayText RoomStatus
        {
            get
            {
                foreach (var transport in instances)
                {
                    switch (transport.LobbyStatus)
                    {
                        case LobbyTransportStatus.Connecting:
                            return RelayText.RoomJoining;
                        case LobbyTransportStatus.Disconnecting:
                            return RelayText.RoomLeaving;
                        case LobbyTransportStatus.Ready:
                            if (transport.IsHosting)
                                return RelayText.RoomHosting;
                            if (!RelaySettings.Enabled)
                                return RelayText.RoomOriginal;
                            if (!transport.ready.Contains(transport.ownerId))
                                return RelayText.RoomWaitingHost;
                            int? ping = Time.realtimeSinceStartup - transport.roomPingAt < SampleLifetime
                                ? transport.roomPing : null;
                            return RelayText.RoomJoined(ping);
                    }
                }
                return RelayText.RoomNone;
            }
        }

        /// <summary>向常驻 HUD 提供两段往返延迟；未连通或样本过期时为空。</summary>
        /// <param name="server">本机到中转服务器的往返毫秒值。</param>
        /// <param name="room">经中转到房主的往返毫秒值；本机房主为零。</param>
        public static void GetLatency(out int? server, out int? room)
        {
            server = null;
            room = null;
            foreach (var transport in instances)
            {
                if (RelaySettings.Enabled && transport.relay != null &&
                    transport.relay.ConnectionState == LiteNetLib.ConnectionState.Connected)
                    server = transport.serverPing;
                if (transport.LobbyStatus != LobbyTransportStatus.Ready)
                    continue;
                if (transport.IsHosting)
                    room = 0;
                else if (RelaySettings.Enabled && transport.ready.Contains(transport.ownerId) &&
                    Time.realtimeSinceStartup - transport.roomPingAt < SampleLifetime)
                    room = transport.roomPing;
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
                RelaySettings.Status = RelayText.Disabled;
                return;
            }
            if (LobbyStatus != LobbyTransportStatus.Ready)
            {
                StopRelay();
                RelaySettings.Status = RelayText.WaitingLobby;
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
                    RelaySettings.Status = RelayText.WaitingCredentials;
                }
                else
                {
                    EnsureConnection();
                    network?.PollEvents(256);
                    if (relay != null && relay.ConnectionState == LiteNetLib.ConnectionState.Connected)
                        RelaySettings.Status = serverPing.HasValue ? RelayText.Connected(serverPing.Value) : RelayText.ServerMeasuring;
                    PollRoomPing();
                }
                LobbyMembers.RemoveAll(member => !ready.Contains((ulong)member.ID));
            }
            catch (Exception ex)
            {
                StopRelay();
                LobbyMembers.Clear();
                failureUntil = Time.realtimeSinceStartup + 3;
                RelaySettings.Status = RelayText.Failure(ex.Message);
                Debug.LogError("[PlateUpRelay] " + ex);
            }
        }

        /// <summary>沿实际中转路径测量到房主的往返；只保留一个未完成请求，超时后丢弃旧样本。</summary>
        private void PollRoomPing()
        {
            if (IsHosting || relay == null || relay.ConnectionState != LiteNetLib.ConnectionState.Connected || !ready.Contains(ownerId))
            {
                pendingProbe = 0;
                roomPing = null;
                return;
            }
            if (pendingProbe != 0)
            {
                if ((Stopwatch.GetTimestamp() - probeStarted) / (double)Stopwatch.Frequency < ProbeTimeout)
                    return;
                pendingProbe = 0;
                roomPing = null;
            }
            if (Time.realtimeSinceStartup < nextProbeAt)
                return;
            nextProbeAt = Time.realtimeSinceStartup + ProbeInterval;
            if (++probeSequence == 0)
                ++probeSequence;
            var payload = new NetDataWriter();
            payload.Put(probeSequence);
            probeStarted = Stopwatch.GetTimestamp();
            if (Protocol.Send(relay, Protocol.Message(ownerId, payload.CopyData(), Protocol.Ping)))
                pendingProbe = probeSequence;
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
                    serverPing = null;
                    roomPing = null;
                    pendingProbe = 0;
                    nextConnect = Time.realtimeSinceStartup + 2;
                    RelaySettings.Status = RelayText.Disconnected(info.Reason.ToString());
                    Debug.LogWarning("[PlateUpRelay] Relay disconnected: " + info.Reason);
                };
                events.NetworkLatencyUpdateEvent += (peer, _) =>
                {
                    // LiteNetLib 的 Ping 和 latency 回调值是半程估算；面板统一显示实际往返。
                    if (peer == relay)
                        serverPing = peer.RoundTripTime;
                };
                events.NetworkReceiveEvent += Receive;
                events.NetworkErrorEvent += (endpoint, error) => RelaySettings.Status = RelayText.NetworkError(error.ToString());
                events.ConnectionRequestEvent += request => request.Reject();
                network = Protocol.CreateManager(events);
                if (!network.Start())
                    throw new InvalidOperationException(RelayText.SocketUnavailable.Value);
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
                RelaySettings.Status = RelayText.Connecting(RelaySettings.Address);
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
                if (source == ownerId)
                {
                    roomPing = null;
                    pendingProbe = 0;
                }
                return;
            }
            if (kind == Protocol.Ping || kind == Protocol.Pong)
            {
                if (reader.AvailableBytes != 4)
                {
                    peer.Disconnect();
                    return;
                }
                if (!ready.Contains(source) || !IsMember(source))
                    return;
                uint sequence = reader.GetUInt();
                if (kind == Protocol.Ping && IsHosting)
                {
                    var payload = new NetDataWriter();
                    payload.Put(sequence);
                    Protocol.Send(peer, Protocol.Message(source, payload.CopyData(), Protocol.Pong));
                }
                else if (kind == Protocol.Pong && !IsHosting && source == ownerId)
                {
                    // 超时或上一轮的应答不能更新当前样本。
                    double elapsed = (Stopwatch.GetTimestamp() - probeStarted) / (double)Stopwatch.Frequency;
                    if (pendingProbe != 0 && sequence == pendingProbe && elapsed < ProbeTimeout)
                    {
                        roomPing = (int)Math.Round(elapsed * 1000);
                        roomPingAt = Time.realtimeSinceStartup;
                        pendingProbe = 0;
                    }
                }
                else
                    peer.Disconnect();
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
            serverPing = null;
            roomPing = null;
            pendingProbe = 0;
            probeStarted = 0;
            nextProbeAt = 0;
            RelaySettings.Status = RelaySettings.Enabled ? RelayText.WaitingLobby : RelayText.Disabled;
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
