using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Networking;
using Unity.Collections;
using UnityEngine;

public class SimpleUdpClient
{

    private UdpClient _socket;
    private IPEndPoint _serverEndPoint;

    private readonly ConcurrentQueue<Packet> _outgoing = new();

    private Thread _thread;
    private bool _running;


    public void Start(string ip, int port)
    {
        _serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

        _socket = new UdpClient();

        _running = true;

        _thread = new Thread(Loop);
        _thread.Start();

        Debug.Log("Client started");
    }

    public void SendHello()
    {
        byte[] data = new byte[] { 1 };

        _outgoing.Enqueue(new Join_Request());

        Debug.Log("Sent HELLO");
    }

    private void Loop()
    {
        while (_running)
        {
            PacketWriter writer = new PacketWriter();
            while (_outgoing.TryDequeue(out var packet))
            {
                packet.Serialize(ref writer);

                var data = writer.ToArray();

                _socket.Send(data, data.Length, _serverEndPoint);
                writer.Reset();
            }

            if (_socket.Available > 0)
            {
                IPEndPoint remote = null;

                /*try
                {
                    byte[] data = _socket.Receive(ref remote);
                    Handle(data);
                }
                catch (SocketException e)
                {
                    _logs.Enqueue($"Socket error: {e}");
                }*/

                byte[] data = _socket.Receive(ref remote);
                Handle(data);
            }

            Thread.Sleep(1);
        }
    }

    private void Handle(byte[] data)
    {
        byte type = data[0];

        Debug.Log("Received RESPONSE from server: " + type);
    }

    public void Stop()
    {
        _running = false;
        _socket.Close();
        _thread.Join();

        Debug.Log("Client stopped");
    }
}
