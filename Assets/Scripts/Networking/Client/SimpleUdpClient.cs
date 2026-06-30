using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class SimpleUdpClient
{

    private UdpClient _socket;
    private IPEndPoint _serverEndPoint;

    private readonly ConcurrentQueue<Packet> _outgoing = new();

    private Thread _thread;
    private bool _running;

    private readonly ConcurrentQueue<string> _logs = new();

    public bool TryDequeueLog(out string msg)
    {
        return _logs.TryDequeue(out msg);
    }


    public void Start(string ip, int port)
    {
        _serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

        _socket = new UdpClient();

        _running = true;

        _thread = new Thread(Loop);
        _thread.Start();

        _logs.Enqueue("Client started");
    }

    public void SendHello()
    {
        byte[] data = new byte[] { 1 };

        _outgoing.Enqueue(new Packet(-1, data));

        _logs.Enqueue("Sent HELLO");
    }

    private void Loop()
    {
        while (_running)
        {
            while (_outgoing.TryDequeue(out var packet))
            {
                var data = packet.data;

                _socket.Send(data, data.Length, _serverEndPoint);
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

        _logs.Enqueue("Received RESPONSE from server: " + type);
    }

    public void Stop()
    {
        _running = false;
        _socket.Close();
        _thread.Join();

        _logs.Enqueue("Client stopped");
    }
}
