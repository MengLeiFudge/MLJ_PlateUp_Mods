using System;
using LiteNetLib;
using LiteNetLib.Utils;

namespace PlateUpRelay
{
    /// <summary>模组与中转服务共用的协议；连接按 Steam 房间与临时凭据隔离。</summary>
    internal static class Protocol
    {
        public const uint Magic = 0x31525550;
        public const int Port = 27841;
        public const int SteamChannel = 27841;
        public const int SecretLength = 32;
        // 与 Steam 可靠消息的 1 MiB 上限对齐；人数不在协议中设限。
        public const int MaxPayload = 1024 * 1024;
        public const int MaxQueuedFragments = 4096;
        public const byte Data = 1;
        public const byte Joined = 2;
        public const byte Left = 3;
        public const byte KeyRequest = 4;
        public const byte KeyReply = 5;

        /// <summary>使用可靠、有序、支持分片的 UDP，保持原游戏收发语义。</summary>
        public static NetManager CreateManager(EventBasedNetListener listener)
        {
            return new NetManager(listener)
            {
                AutoRecycle = true,
                ChannelsCount = 1,
                MtuOverride = 1200,
                UpdateTime = 5,
                PingInterval = 1000,
                DisconnectTimeout = 10000,
                ReconnectDelay = 1000,
                MaxConnectAttempts = 5
            };
        }

        /// <summary>单条完整游戏消息；地址由服务端换成真实连接绑定的发送者。</summary>
        public static NetDataWriter Message(ulong address, byte[] payload)
        {
            var writer = new NetDataWriter(false, payload.Length + 9);
            writer.Put(Data);
            writer.Put(address);
            writer.Put(payload);
            return writer;
        }

        /// <summary>队列积压时显式断开，不让可靠消息无界累积或静默丢弃。</summary>
        public static bool Send(NetPeer peer, NetDataWriter writer)
        {
            if (peer.ConnectionState != ConnectionState.Connected)
                return false;
            if (peer.GetPacketsCountInReliableQueue(0, true) > MaxQueuedFragments)
            {
                peer.Disconnect();
                return false;
            }
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            return true;
        }
    }
}
