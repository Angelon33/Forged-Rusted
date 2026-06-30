
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class UdpTransport : INetworkTransport
{
    private readonly ConcurrentQueue<Packet> _incoming = new();

    private readonly ConcurrentQueue<OutgoingPacket> _outgoing = new();

    private readonly Dictionary<int, IPEndPoint> _peers = new();

    private Thread _networkThread;

    private volatile bool _running;

    private UdpClient _socket;

    private int _port = 25565;

    public UdpTransport(int port = 25565)
    {
        this._port = port;
    }

    public void Start()
    {
        if(_socket != null)
        {
            return;
        }
        _socket = new UdpClient(_port);

        _running = true;

        _networkThread = new Thread(NetworkLoop);
        _networkThread.Start();
    }
    public void Stop()
    {
        _running = false;
        _socket.Close();
        _networkThread.Join();
    }

    public void Dispose()
    {
        Stop();
    }

    public void Broadcast(Packet packet)
    {
        _outgoing.Enqueue(new OutgoingPacket(-1, packet, true));
    }

    public void Send(int peerId, Packet packet)
    {
        if(!_peers.ContainsKey(peerId))
        {
            return;
        }
        _outgoing.Enqueue(new OutgoingPacket(peerId, packet));
    }

    public void Poll(List<Packet> packets)
    {
        if(packets == null)
        {
            return;
        }

        packets.Clear();

        while (_incoming.TryDequeue(out var packet))
        {
            packets.Add(packet);
        }
    }

    private void NetworkLoop()
    {
        while (_running)
        {
            Receive();
            Send();

            Thread.Sleep(1);
        }
    }

    private void Receive()
    {
        while (_socket.Available > 0)
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            byte[] data = _socket.Receive(ref remote);

            var peerId = ResolvePeer(remote);

            _incoming.Enqueue(
                new Packet(peerId, BitConverter.GetBytes(peerId))
            );
        }
    }

    private int ResolvePeer(IPEndPoint endpoint)
    {
        foreach (var kv in _peers)
        {
            if (kv.Value.Equals(endpoint))
                return kv.Key;
        }

        var id = PeerIdProvider.Next();
        _peers[id] = endpoint;

        return id;
    }

        private void Send()
    {
        while (_outgoing.TryDequeue(out var packet))
        {
            var data = packet.packet.data;
            if(packet.shouldBroadcast)
            {
                foreach(var peer in _peers)
                {
                    _socket.Send(data, data.Length, peer.Value);
                }
                continue;
            }
            if (!_peers.TryGetValue(packet.peerId, out var endpoint))
                continue;

            _socket.Send(data, data.Length, endpoint);
        }
    }

    private record OutgoingPacket
    {
        public readonly bool shouldBroadcast;
        public readonly int peerId;
        public readonly Packet packet;

        public OutgoingPacket(int id, Packet packet, bool shouldBroadcast = false)
        {
            this.peerId = id;
            this.packet = packet;
            this.shouldBroadcast = shouldBroadcast;
        }
    }
}
