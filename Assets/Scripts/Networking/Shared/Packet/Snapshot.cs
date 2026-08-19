using System.Collections.Generic;
using Networking;
using Unity.Collections;

public class Snapshot : Packet
{
    public List<NetObject> ToAdd;
    public List<NetObject> ToUpdate;
    public List<NetObject> ToRemove;

    public Snapshot(byte peerId) : base(PacketType.Snapshot, peerId)
    {
        ToAdd = new();
        ToUpdate = new();
        ToRemove = new();
    }

    public Snapshot(PacketReader reader) : base(reader)
    {
        ToAdd = new();
        ToUpdate = new();
        ToRemove = new();
    }

    public new void Serialize(ref PacketWriter writer)
    {
        base.Serialize(ref writer);
    }

    public new void Deserialize(PacketReader reader)
    {
        
    }
}
