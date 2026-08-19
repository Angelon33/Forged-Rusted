
using System;
using System.Collections.Generic;
using System.Net;
using Unity.Collections;

public interface INetworkTransport : IDisposable
{
    void Start();
    void Stop();

    void Send(byte[] data, ITransportHandle handle);

    void Poll(List<ReceivedPacket> packets);
}

public readonly struct ReceivedPacket
{
    public readonly ITransportHandle Handle;

    public readonly byte[] Data;

    public ReceivedPacket(
        ITransportHandle handle,
        byte[] data)
    {
        Handle = handle;
        Data = data;
    }
}
