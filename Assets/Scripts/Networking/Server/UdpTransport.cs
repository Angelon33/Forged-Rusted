
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class UdpTransport : INetworkTransport
{
    private readonly ConcurrentQueue<ReceivedPacket> _incoming = new();
    private readonly ConcurrentQueue<(byte[] data, IPEndPoint target)> _outgoing = new();

    private readonly Dictionary<byte, IPEndPoint> _peers = new();

    private Thread _networkThread;

    private volatile bool _running;

    private UdpClient _socket;

    public UdpTransport(int port = 25565)
    {
        _socket = new UdpClient(port);
    }

    public void Start()
    { 
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

    public void Send(byte[] data, ITransportHandle handle)
    {
        var udp = (UdpTransportHandle)handle;
        _outgoing.Enqueue((data, udp.EndPoint));
    }

    public void Poll(List<ReceivedPacket> packets)
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
            try
            {
                Receive();
                Send();
            }
            catch(SocketException)
            {
            }
            catch(Exception ex)
            {
                Debug.Log(ex);
            }

            Thread.Sleep(1);
        }
    }

    private void Receive()
    {
        while (_socket.Available > 0)
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = _socket.Receive(ref remote);

            Debug.Log("RECEIVED PACKET");
            _incoming.Enqueue(
                new ReceivedPacket(new UdpTransportHandle(remote), data)
            );
        }
    }

        private void Send()
    {
        while (_outgoing.TryDequeue(out var item))
        {
            _socket.Send(item.data, item.data.Length, item.target);
        }
    }
}
