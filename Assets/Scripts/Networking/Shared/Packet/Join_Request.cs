using Networking;
using UnityEngine;

public class Join_Request : Packet
{
    public Join_Request(PacketReader reader) : base(reader)
    {
    }

    public Join_Request() : base(PacketType.Join_Request, 0)
    {
    }
}
