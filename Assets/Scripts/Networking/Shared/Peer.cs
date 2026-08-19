using System;

public class Peer
{
    public byte Id { get; }

    public ITransportHandle Handle { get; }

    public DateTime LastReceiveTime { get; set; }

    public uint LastSequenceReceived { get; set; }

    public uint LastSequenceSent { get; set; }

    public Peer(byte id, ITransportHandle handle)
    {
        Id = id;
        Handle = handle;
    }
}