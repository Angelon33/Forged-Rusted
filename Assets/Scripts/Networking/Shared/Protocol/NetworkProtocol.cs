using System;

namespace Networking
{
    public static class NetworkProtocol
    {
        public const uint Magic = 0x46525354;
        public const ushort Version = 1;

        public const int HeaderSize =
            sizeof(uint) +
            sizeof(ushort) +
            sizeof(byte) +
            sizeof(ushort);

        public const int MaximumDatagramSize =
            NetworkTransportLimits.MaximumApplicationDatagramSize;

        public static byte[] Encode(
            NetworkMessageType type,
            Action<PacketWriter> writePayload)
        {
            var payloadWriter = new PacketWriter();
            writePayload?.Invoke(payloadWriter);

            if (payloadWriter.Length > ushort.MaxValue)
                throw new InvalidOperationException("Payload is too large.");

            var writer = new PacketWriter(HeaderSize + payloadWriter.Length);

            writer.Write(Magic);
            writer.Write(Version);
            writer.Write((byte)type);
            writer.Write((ushort)payloadWriter.Length);
            writer.Write(payloadWriter.WrittenSpan);

            byte[] result = writer.ToArray();

            if (result.Length > MaximumDatagramSize)
                throw new InvalidOperationException(
                    $"Datagram exceeds {MaximumDatagramSize} bytes.");

            return result;
        }

        public static bool TryDecode(
            byte[] datagram,
            out NetworkMessageType type,
            out PacketReader payload)
        {
            type = default;
            payload = null;

            if (datagram == null ||
                datagram.Length < HeaderSize ||
                datagram.Length > MaximumDatagramSize)
            {
                return false;
            }

            try
            {
                var reader = new PacketReader(datagram);

                uint magic = reader.ReadUInt32();
                ushort version = reader.ReadUInt16();
                type = (NetworkMessageType)reader.ReadByte();
                ushort payloadLength = reader.ReadUInt16();

                if (magic != Magic || version != Version)
                    return false;

                if (!Enum.IsDefined(typeof(NetworkMessageType), type))
                    return false;

                if (reader.Remaining != payloadLength)
                    return false;

                payload = new PacketReader(reader.ReadBytes(payloadLength));
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
