
public struct Packet
{
    public int peerId;
    public byte[] data;

    public Packet(int id, byte[] data)
    {
        this.peerId = id;
        this.data = data;
    }
}
