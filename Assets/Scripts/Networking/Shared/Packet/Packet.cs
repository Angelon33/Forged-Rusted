
using System.IO;
using Networking;
using Unity.Collections;
using UnityEngine;

public abstract class Packet
{
    public static readonly byte VERSION = 1;
    public byte version;
    public PacketType packetType;

    public byte peerId;

    public Packet(PacketType packetType, byte peerId)
    {
        this.version = VERSION;
        this.packetType = packetType;
        this.peerId = peerId;
    }

    public Packet(PacketReader reader)
    {
        Deserialize(reader);
    }

    public void Serialize(ref PacketWriter writer)
    {
        writer.Write(version);
        writer.Write((byte)packetType);
        writer.Write(peerId);
    }

    public void Deserialize(PacketReader reader)
    {
        peerId = reader.ReadByte();
    }
}

public enum PacketType : byte
{
    Join_Request,
    Join_Response,
    Snapshot,
}
