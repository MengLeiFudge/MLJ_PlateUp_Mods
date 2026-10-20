using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LiteNetLib;
using LiteNetLib.Utils;
using PlateUpRelay;

/// <summary>独立游戏数据中转；仅在已认证的同一房间内转发，不代理任意目标地址。</summary>
internal sealed class RelayServer : IDisposable
{
    private sealed class Room
    {
        public required string Key { get; init; }
        public required ulong Owner { get; init; }
        public Dictionary<ulong, Session> Members { get; } = new();
    }

    private sealed class Session
    {
        public required Room Room { get; init; }
        public required ulong Player { get; init; }
        public required NetPeer Peer { get; init; }
    }

    private readonly Dictionary<string, Room> rooms = new();
    private readonly NetManager network;
    private bool stopping;

    public RelayServer()
    {
        var events = new EventBasedNetListener();
        events.ConnectionRequestEvent += Accept;
        events.PeerConnectedEvent += Connected;
        events.PeerDisconnectedEvent += (peer, _) => Disconnected(peer);
        events.NetworkReceiveEvent += Receive;
        events.NetworkErrorEvent += (endpoint, error) => Console.Error.WriteLine($"UDP error {endpoint}: {error}");
        network = Protocol.CreateManager(events);
    }

    /// <summary>凭据由房主通过 Steam 已认证通道分发，服务端将身份绑定到连接。</summary>
    private void Accept(ConnectionRequest request)
    {
        var data = request.Data;
        if (data.AvailableBytes != 4 + 8 * 3 + Protocol.SecretLength || data.GetUInt() != Protocol.Magic)
        {
            request.Reject();
            return;
        }
        ulong lobby = data.GetULong();
        ulong owner = data.GetULong();
        ulong player = data.GetULong();
        byte[] secret = data.GetRemainingBytes();
        if (lobby == 0 || owner == 0 || player == 0)
        {
            request.Reject();
            return;
        }
        // 凭据参与房间索引，外部连接无法仅靠猜测 Lobby ID 抢占或加入房间。
        string key = lobby.ToString() + ":" + Convert.ToHexString(SHA256.HashData(secret));
        if (!rooms.TryGetValue(key, out var room))
        {
            room = new Room { Key = key, Owner = owner };
            rooms.Add(key, room);
        }
        if (room.Owner != owner || room.Members.ContainsKey(player))
        {
            request.Reject();
            return;
        }
        NetPeer? peer = request.Accept();
        if (peer == null)
        {
            if (room.Members.Count == 0)
                rooms.Remove(key);
            return;
        }
        var session = new Session { Room = room, Player = player, Peer = peer };
        peer.Tag = session;
        room.Members.Add(player, session);
    }

    private static void Notify(NetPeer peer, byte kind, ulong player)
    {
        var writer = new NetDataWriter();
        writer.Put(kind);
        writer.Put(player);
        Protocol.Send(peer, writer);
    }

    /// <summary>先发送就绪成员通知，再允许游戏发送初始状态，避免入房时丢失首包。</summary>
    private void Connected(NetPeer peer)
    {
        if (peer.Tag is not Session session)
        {
            peer.Disconnect();
            return;
        }
        foreach (var member in session.Room.Members.Values)
        {
            if (member.Peer.ConnectionState != ConnectionState.Connected)
                continue;
            Notify(peer, Protocol.Joined, member.Player);
            if (member.Player != session.Player)
                Notify(member.Peer, Protocol.Joined, session.Player);
        }
        Console.WriteLine($"Player connected; active sessions={network.ConnectedPeersCount}, rooms={rooms.Count}");
    }

    private void Disconnected(NetPeer peer)
    {
        if (peer.Tag is not Session session)
            return;
        session.Room.Members.Remove(session.Player);
        if (session.Room.Members.Count == 0)
            rooms.Remove(session.Room.Key);
        else
            foreach (var member in session.Room.Members.Values)
                Notify(member.Peer, Protocol.Left, session.Player);
        peer.Tag = null;
        Console.WriteLine($"Player disconnected; rooms={rooms.Count}");
    }

    private static void Receive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
    {
        if (peer.Tag is not Session sender || channel != 0 || delivery != DeliveryMethod.ReliableOrdered ||
            reader.AvailableBytes < 10 || reader.AvailableBytes > Protocol.MaxPayload + 9 || reader.GetByte() != Protocol.Data)
        {
            peer.Disconnect();
            return;
        }
        ulong destination = reader.GetULong();
        if (destination == sender.Player || (sender.Player != sender.Room.Owner && destination != sender.Room.Owner))
        {
            peer.Disconnect();
            return;
        }
        if (!sender.Room.Members.TryGetValue(destination, out var target) || target.Peer.ConnectionState != ConnectionState.Connected)
        {
            // 目标刚退出时告知发送端撤下成员；可靠消息不伪装为已送达。
            Notify(peer, Protocol.Left, destination);
            return;
        }
        if (!Protocol.Send(target.Peer, Protocol.Message(sender.Player, reader.GetRemainingBytes())))
            Notify(peer, Protocol.Left, destination);
    }

    public void Run(int port)
    {
        if (!network.Start(IPAddress.Any, IPAddress.IPv6Any, port))
            throw new InvalidOperationException($"Cannot bind UDP {port}");
        Console.WriteLine($"PlateUp relay listening on UDP {port}; protocol v1");
        Console.CancelKeyPress += Cancel;
        using var signal = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            Volatile.Write(ref stopping, true);
        });
        try
        {
            while (!Volatile.Read(ref stopping))
            {
                network.PollEvents(1024);
                Thread.Sleep(5);
            }
        }
        finally
        {
            Console.CancelKeyPress -= Cancel;
        }
    }

    private void Cancel(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        Volatile.Write(ref stopping, true);
    }

    public void Dispose() => network.Stop();

    public static int Main(string[] args)
    {
        if (args.Length > 1 || (args.Length == 1 && (!int.TryParse(args[0], out int candidate) || candidate < 1 || candidate > 65535)))
        {
            Console.Error.WriteLine("Usage: PlateUpRelay.Server [udp-port]");
            return 2;
        }
        int port = args.Length == 0 ? Protocol.Port : int.Parse(args[0]);
        using var server = new RelayServer();
        server.Run(port);
        return 0;
    }
}
