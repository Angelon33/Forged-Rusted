namespace Networking
{
    public static class NetworkTransportLimits
    {
        public const int MaximumDatagramSize = 1200;

        public const int MaximumDeliveryHeaderSize = 10;

        public const int MaximumApplicationDatagramSize =
            MaximumDatagramSize -
            MaximumDeliveryHeaderSize;
    }
}