using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class GameServer
{

    private readonly ConcurrentQueue<string> _logs = new();

    private readonly Dictionary<int, Peer> _peers = new Dictionary<int, Peer>();

    private INetworkTransport _transport;

    private List<Packet> _packets = new();

    public GameServer(INetworkTransport transport)
    {
        this._transport = transport;
    }

    public bool TryDequeueLog(out string msg)
    {
        return _logs.TryDequeue(out msg);
    }

    public void Start()
    {
        if (this._transport == null)
        {
            return;
        }

        _packets = new();
        _transport.Start();
        _logs.Enqueue("Server started");
    }

    public void Close()
    {
        _transport.Dispose();
        _logs.Enqueue("Server stopped");
    }

    public void Update()
    {
        _transport.Poll(_packets);

        foreach (var packet in _packets)
        {
            _transport.Send(packet.peerId, new Packet(packet.peerId, System.BitConverter.GetBytes(packet.peerId)));
            _logs.Enqueue("Received packet from client: " + packet.peerId);
        }
    }
}

public static class PeerIdProvider
{
    private static int _nextId = 1;

    public static int Next()
    {
        return _nextId++;
    }

    public static void Reset()
    {
        _nextId = 1;
    }
}
