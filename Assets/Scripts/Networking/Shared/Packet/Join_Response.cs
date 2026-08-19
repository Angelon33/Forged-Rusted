using Networking;
using UnityEngine;

public class Join_Response : Packet
{
    public Join_Response(PacketReader reader) : base(reader)
    {
    }

    public Join_Response(byte peerId) : base(PacketType.Join_Response, peerId)
    {
    }
}
