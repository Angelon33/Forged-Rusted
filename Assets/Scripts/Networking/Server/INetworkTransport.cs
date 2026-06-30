
using System;
using System.Collections.Generic;

public interface INetworkTransport : IDisposable
{
    void Start();
    void Stop();

    void Send(int peerId, Packet packet);
    void Broadcast(Packet packet);

    void Poll(List<Packet> packets);
}
