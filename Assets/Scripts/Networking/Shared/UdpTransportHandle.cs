using System.Net;

public sealed class UdpTransportHandle : ITransportHandle
{
    public IPEndPoint EndPoint { get; }

    public UdpTransportHandle(IPEndPoint endpoint)
    {
        EndPoint = endpoint;
    }

    public bool Equals(UdpTransportHandle other)
    {
        if (other is null) return false;
        return EndPoint.Equals(other.EndPoint);
    }

    public override bool Equals(object obj)
        => obj is UdpTransportHandle other && Equals(other);

    public override int GetHashCode()
        => EndPoint.GetHashCode();
}