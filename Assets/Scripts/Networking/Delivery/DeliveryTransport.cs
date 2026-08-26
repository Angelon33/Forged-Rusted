using System;
using System.Collections.Generic;

namespace Networking
{
    public sealed class DeliveryTransport :
        INetworkDeliveryTransport
    {
        private const uint Magic = 0x44564C59;
        private const byte Version = 1;

        private const double ResendInterval = 0.2;

        private const int MaximumPendingPackets = 256;
        private const int MaximumBufferedPackets = 256;
        private const int MaximumIncomingEvents = 1024;
        private const int MaximumRawEventsPerUpdate = 2048;

        private const int BasicHeaderSize =
            sizeof(uint) +
            sizeof(byte) +
            sizeof(byte);

        private const int SequencedHeaderSize =
            BasicHeaderSize +
            sizeof(uint);

        private readonly INetworkTransport _transport;

        private readonly Dictionary<
            ITransportHandle,
            RemoteState> _remotes =
                new Dictionary<
                    ITransportHandle,
                    RemoteState>();

        private readonly Queue<TransportEvent> _incoming =
            new Queue<TransportEvent>();

        private readonly List<uint> _sequenceScratch =
            new List<uint>(MaximumPendingPackets);

        private double _now;
        private bool _disposed;

        public bool IsRunning =>
            !_disposed &&
            _transport.IsRunning;

        public DeliveryTransport(
            INetworkTransport transport)
        {
            _transport = transport ??
                throw new ArgumentNullException(
                    nameof(transport));
        }

        public void StartServer(ushort port)
        {
            ThrowIfDisposed();
            _transport.StartServer(port);
        }

        public ITransportHandle StartClient(
            string address,
            ushort port)
        {
            ThrowIfDisposed();

            return _transport.StartClient(
                address,
                port);
        }

        public void RegisterRemote(
            ITransportHandle remote)
        {
            ThrowIfDisposed();

            if (remote == null)
            {
                throw new ArgumentNullException(
                    nameof(remote));
            }

            if (!_remotes.ContainsKey(remote))
                _remotes.Add(remote, new RemoteState());
        }

        public void RemoveRemote(
            ITransportHandle remote)
        {
            if (_disposed || remote == null)
                return;

            _remotes.Remove(remote);
        }

        public bool Send(
            ITransportHandle destination,
            byte[] data,
            NetworkDelivery delivery)
        {
            if (_disposed ||
                !_transport.IsRunning ||
                destination == null ||
                data == null ||
                data.Length == 0 ||
                data.Length >
                    NetworkTransportLimits
                        .MaximumApplicationDatagramSize)
            {
                return false;
            }

            switch (delivery)
            {
                case NetworkDelivery.Unreliable:
                    return SendUnreliable(
                        destination,
                        data);

                case NetworkDelivery.UnreliableSequenced:
                    return SendUnreliableSequenced(
                        destination,
                        data);

                case NetworkDelivery.ReliableOrdered:
                    return SendReliableOrdered(
                        destination,
                        data);

                default:
                    return false;
            }
        }

        public void Update(double now)
        {
            if (_disposed ||
                !_transport.IsRunning)
            {
                return;
            }

            _now = now;

            int processed = 0;

            while (
                processed < MaximumRawEventsPerUpdate &&
                _transport.TryPollEvent(
                    out TransportEvent transportEvent))
            {
                processed++;

                if (transportEvent.Type ==
                    TransportEventType.Error)
                {
                    if (_incoming.Count <
                        MaximumIncomingEvents)
                    {
                        _incoming.Enqueue(
                            transportEvent);
                    }

                    continue;
                }

                ProcessDatagram(
                    transportEvent.Remote,
                    transportEvent.Data);
            }

            DeliverReadyBufferedPackets();
            ResendPendingPackets();
        }

        public bool TryPollEvent(
            out TransportEvent transportEvent)
        {
            if (_incoming.Count == 0)
            {
                transportEvent = default;
                return false;
            }

            transportEvent = _incoming.Dequeue();
            return true;
        }

        public void Stop()
        {
            if (_disposed)
                return;

            _transport.Stop();
            ClearState();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _transport.Dispose();
            ClearState();
        }

        private bool SendUnreliable(
            ITransportHandle destination,
            byte[] data)
        {
            byte[] datagram = Encode(
                DeliveryPacketType.Unreliable,
                0,
                data);

            return _transport.Send(
                destination,
                datagram);
        }

        private bool SendUnreliableSequenced(
            ITransportHandle destination,
            byte[] data)
        {
            if (!_remotes.TryGetValue(
                    destination,
                    out RemoteState state))
            {
                return false;
            }

            uint sequence =
                state.NextUnreliableSendSequence;

            byte[] datagram = Encode(
                DeliveryPacketType.UnreliableSequenced,
                sequence,
                data);

            if (!_transport.Send(
                    destination,
                    datagram))
            {
                return false;
            }

            state.NextUnreliableSendSequence++;

            return true;
        }

        private bool SendReliableOrdered(
            ITransportHandle destination,
            byte[] data)
        {
            if (!_remotes.TryGetValue(
                    destination,
                    out RemoteState state))
            {
                return false;
            }

            if (state.PendingReliable.Count >=
                MaximumPendingPackets)
            {
                return false;
            }

            uint sequence =
                state.NextReliableSendSequence;

            byte[] datagram = Encode(
                DeliveryPacketType.ReliableOrdered,
                sequence,
                data);

            if (!_transport.Send(
                    destination,
                    datagram))
            {
                return false;
            }

            state.NextReliableSendSequence++;

            state.PendingReliable.Add(
                sequence,
                new PendingPacket(
                    datagram,
                    _now));

            return true;
        }

        private void ProcessDatagram(
            ITransportHandle remote,
            byte[] datagram)
        {
            if (remote == null ||
                !TryDecode(
                    datagram,
                    out DeliveryPacketType type,
                    out uint sequence,
                    out byte[] payload))
            {
                return;
            }

            // Handshake messages are unreliable and must be
            // allowed before the server registers the remote.
            if (type == DeliveryPacketType.Unreliable)
            {
                QueueData(remote, payload);
                return;
            }

            if (!_remotes.TryGetValue(
                    remote,
                    out RemoteState state))
            {
                return;
            }

            switch (type)
            {
                case DeliveryPacketType
                    .UnreliableSequenced:

                    ProcessUnreliableSequenced(
                        remote,
                        state,
                        sequence,
                        payload);
                    break;

                case DeliveryPacketType
                    .ReliableOrdered:

                    ProcessReliableOrdered(
                        remote,
                        state,
                        sequence,
                        payload);
                    break;

                case DeliveryPacketType
                    .Acknowledgement:

                    state.PendingReliable.Remove(
                        sequence);
                    break;
            }
        }

        private void ProcessUnreliableSequenced(
            ITransportHandle remote,
            RemoteState state,
            uint sequence,
            byte[] payload)
        {
            if (state.HasReceivedUnreliable &&
                !SequenceUtility.IsNewer(
                    sequence,
                    state.LatestUnreliableReceiveSequence))
            {
                return;
            }

            state.HasReceivedUnreliable = true;

            state.LatestUnreliableReceiveSequence =
                sequence;

            QueueData(remote, payload);
        }

        private void ProcessReliableOrdered(
            ITransportHandle remote,
            RemoteState state,
            uint sequence,
            byte[] payload)
        {
            uint expected =
                state.NextReliableReceiveSequence;

            if (sequence == expected)
            {
                // Do not acknowledge a packet unless it was
                // successfully handed to the application queue.
                if (!QueueData(remote, payload))
                    return;

                SendAcknowledgement(
                    remote,
                    sequence);

                state.NextReliableReceiveSequence++;

                DeliverBufferedPackets(
                    remote,
                    state);

                return;
            }

            if (!SequenceUtility.IsNewer(
                    sequence,
                    expected))
            {
                // It has already been delivered. Acknowledge
                // it again because the previous ACK was lost.
                SendAcknowledgement(
                    remote,
                    sequence);

                return;
            }

            uint distance =
                SequenceUtility.ForwardDistance(
                    expected,
                    sequence);

            if (state.BufferedReliable.ContainsKey(
                sequence))
            {
                SendAcknowledgement(
                    remote,
                    sequence);

                return;
            }

            if (distance > MaximumBufferedPackets ||
                state.BufferedReliable.Count >=
                    MaximumBufferedPackets)
            {
                // It cannot be retained, so do not acknowledge it.
                return;
            }

            state.BufferedReliable.Add(
                sequence,
                payload);

            SendAcknowledgement(
                remote,
                sequence);
        }

        private void DeliverBufferedPackets(
            ITransportHandle remote,
            RemoteState state)
        {
            while (
                state.BufferedReliable.TryGetValue(
                    state.NextReliableReceiveSequence,
                    out byte[] payload))
            {
                if (!QueueData(remote, payload))
                    return;

                state.BufferedReliable.Remove(
                    state.NextReliableReceiveSequence);

                state.NextReliableReceiveSequence++;
            }
        }

        private void DeliverReadyBufferedPackets()
        {
            foreach (
                KeyValuePair<
                    ITransportHandle,
                    RemoteState> remote
                in _remotes)
            {
                if (_incoming.Count >=
                    MaximumIncomingEvents)
                {
                    return;
                }

                DeliverBufferedPackets(
                    remote.Key,
                    remote.Value);
            }
        }

        private void SendAcknowledgement(
            ITransportHandle remote,
            uint sequence)
        {
            byte[] acknowledgement = Encode(
                DeliveryPacketType.Acknowledgement,
                sequence,
                null);

            _transport.Send(
                remote,
                acknowledgement);
        }

        private void ResendPendingPackets()
        {
            foreach (
                KeyValuePair<
                    ITransportHandle,
                    RemoteState> remote
                in _remotes)
            {
                _sequenceScratch.Clear();

                foreach (
                    KeyValuePair<
                        uint,
                        PendingPacket> pending
                    in remote.Value.PendingReliable)
                {
                    if (_now -
                        pending.Value.LastSendTime >=
                        ResendInterval)
                    {
                        _sequenceScratch.Add(
                            pending.Key);
                    }
                }

                for (int index = 0;
                     index < _sequenceScratch.Count;
                     index++)
                {
                    uint sequence =
                        _sequenceScratch[index];

                    if (!remote.Value.PendingReliable
                        .TryGetValue(
                            sequence,
                            out PendingPacket pending))
                    {
                        continue;
                    }

                    if (_transport.Send(
                            remote.Key,
                            pending.Datagram))
                    {
                        pending.LastSendTime = _now;
                    }
                }
            }

            _sequenceScratch.Clear();
        }

        private bool QueueData(
            ITransportHandle remote,
            byte[] payload)
        {
            if (payload == null ||
                payload.Length == 0 ||
                _incoming.Count >= MaximumIncomingEvents)
            {
                return false;
            }

            _incoming.Enqueue(
                TransportEvent.DataReceived(
                    remote,
                    payload));

            return true;
        }

        private static byte[] Encode(
            DeliveryPacketType type,
            uint sequence,
            byte[] payload)
        {
            int headerSize =
                type == DeliveryPacketType.Unreliable
                    ? BasicHeaderSize
                    : SequencedHeaderSize;

            int payloadSize =
                payload?.Length ?? 0;

            var writer = new PacketWriter(
                headerSize + payloadSize);

            writer.Write(Magic);
            writer.Write(Version);
            writer.Write((byte)type);

            if (type != DeliveryPacketType.Unreliable)
                writer.Write(sequence);

            if (payloadSize > 0)
                writer.Write(payload);

            return writer.ToArray();
        }

        private static bool TryDecode(
            byte[] datagram,
            out DeliveryPacketType type,
            out uint sequence,
            out byte[] payload)
        {
            type = default;
            sequence = 0;
            payload = null;

            if (datagram == null ||
                datagram.Length < BasicHeaderSize ||
                datagram.Length >
                    NetworkTransportLimits
                        .MaximumDatagramSize)
            {
                return false;
            }

            try
            {
                var reader =
                    new PacketReader(datagram);

                uint magic =
                    reader.ReadUInt32();

                byte version =
                    reader.ReadByte();

                if (magic != Magic ||
                    version != Version)
                {
                    return false;
                }

                type =
                    (DeliveryPacketType)
                    reader.ReadByte();

                if (!Enum.IsDefined(
                        typeof(DeliveryPacketType),
                        type))
                {
                    return false;
                }

                if (type !=
                    DeliveryPacketType.Unreliable)
                {
                    if (reader.Remaining < sizeof(uint))
                        return false;

                    sequence =
                        reader.ReadUInt32();
                }

                if (type ==
                    DeliveryPacketType.Acknowledgement)
                {
                    return reader.Remaining == 0;
                }

                if (reader.Remaining == 0 ||
                    reader.Remaining >
                        NetworkTransportLimits
                            .MaximumApplicationDatagramSize)
                {
                    return false;
                }

                payload =
                    reader.ReadBytes(
                        reader.Remaining);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void ClearState()
        {
            _remotes.Clear();
            _incoming.Clear();
            _sequenceScratch.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(DeliveryTransport));
            }
        }

        private enum DeliveryPacketType : byte
        {
            Unreliable = 0,
            UnreliableSequenced = 1,
            ReliableOrdered = 2,
            Acknowledgement = 3
        }

        private sealed class RemoteState
        {
            public uint NextUnreliableSendSequence;

            public uint LatestUnreliableReceiveSequence;

            public bool HasReceivedUnreliable;

            public uint NextReliableSendSequence;

            public uint NextReliableReceiveSequence;

            public readonly Dictionary<
                uint,
                PendingPacket> PendingReliable =
                    new Dictionary<
                        uint,
                        PendingPacket>();

            public readonly Dictionary<
                uint,
                byte[]> BufferedReliable =
                    new Dictionary<
                        uint,
                        byte[]>();
        }

        private sealed class PendingPacket
        {
            public byte[] Datagram { get; }

            public double LastSendTime { get; set; }

            public PendingPacket(
                byte[] datagram,
                double lastSendTime)
            {
                Datagram = datagram;
                LastSendTime = lastSendTime;
            }
        }

        private static class SequenceUtility
        {
            public static bool IsNewer(
                uint candidate,
                uint reference)
            {
                return candidate != reference &&
                    unchecked(
                        (int)(candidate - reference)) > 0;
            }

            public static uint ForwardDistance(
                uint from,
                uint to)
            {
                return unchecked(to - from);
            }
        }
    }
}