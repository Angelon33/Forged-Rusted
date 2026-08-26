using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Networking
{
    public sealed class GameServer : IDisposable
    {
        private const int MaximumPeers = 128;
        private const int MaximumEventsPerUpdate = 256;
        private const double PendingTimeout = 10.0;
        private const double ConnectedTimeout = 15.0;

        private readonly Dictionary<ITransportHandle, Peer> _peersByHandle = new();
        private readonly Dictionary<uint, Peer> _peersById = new();
        private readonly List<Peer> _timedOutPeers = new();
        private readonly INetworkDeliveryTransport _transport;
        private uint _nextPeerId = 1;
        private bool _started;
        private bool _disposed;

        public bool IsRunning => _started && _transport.IsRunning;
        public int PeerCount => _peersById.Count;

        public event Action<Peer> PeerConnected;
        public event Action<uint> PeerDisconnected;
        public event Action<string> Error;

        public GameServer(
            INetworkDeliveryTransport transport)
        {
            _transport = transport ??
                throw new ArgumentNullException(
                    nameof(transport));
        }

        public void Start(ushort port)
        {
            ThrowIfDisposed();

            if (_started)
                throw new InvalidOperationException("Server is already running.");

            _transport.StartServer(port);
            _started = true;
        }

        public void Update(double now)
        {
            if (!_started || _disposed)
            {
                return;
            }

            _transport.Update(now);

            int processed = 0;
            while (processed < MaximumEventsPerUpdate &&
                   _transport.TryPollEvent(out TransportEvent transportEvent))
            {
                processed++;
                if (transportEvent.Type == TransportEventType.Error)
                {
                    Error?.Invoke(transportEvent.Error);
                    continue;
                }

                HandleDatagram(transportEvent.Remote, transportEvent.Data, now);
            }

            RemoveTimedOutPeers(now);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _started = false;
            _peersByHandle.Clear();
            _peersById.Clear();
            _timedOutPeers.Clear();
            _transport.Dispose();
            _disposed = true;
        }

        public bool Send(
            Peer peer,
            NetworkMessageType type,
            Action<PacketWriter> writePayload,
            NetworkDelivery delivery =
                NetworkDelivery.Unreliable)
        {
            if (peer == null ||
                peer.State != ServerPeerState.Connected ||
                !_peersById.TryGetValue(
                    peer.Id,
                    out Peer registered) ||
                !ReferenceEquals(peer, registered))
            {
                return false;
            }

            return SendMessage(
                peer.Handle,
                type,
                writePayload,
                delivery);
        }

        public void Broadcast(
            NetworkMessageType type,
            Action<PacketWriter> writePayload,
            NetworkDelivery delivery =
                NetworkDelivery.Unreliable)
        {
            byte[] data =
                NetworkProtocol.Encode(
                    type,
                    writePayload);

            foreach (Peer peer in _peersById.Values)
            {
                if (peer.State !=
                    ServerPeerState.Connected)
                {
                    continue;
                }

                if (!_transport.Send(
                        peer.Handle,
                        data,
                        delivery))
                {
                    Error?.Invoke(
                        $"Could not queue {type} " +
                        $"message for peer {peer.Id}.");
                }
            }
        }

        

        private void HandleDatagram(ITransportHandle remote, byte[] data, double now)
        {
            if (remote == null ||
                !NetworkProtocol.TryDecode(
                    data,
                    out NetworkMessageType type,
                    out PacketReader payload))
                return;

            try
            {
                switch (type)
                {
                    case NetworkMessageType.ClientHello:
                        HandleClientHello(remote, payload, now);
                        break;
                    case NetworkMessageType.ClientReady:
                        HandleClientReady(remote, payload, now);
                        break;
                    case NetworkMessageType.Heartbeat:
                        HandleHeartbeat(remote, payload, now);
                        break;
                    case NetworkMessageType.Disconnect:
                        HandleDisconnect(remote, payload);
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                // Malformed payloads are ignored.
            }
        }

        private void HandleClientHello(
            ITransportHandle remote,
            PacketReader payload,
            double now)
        {
            if (payload.Remaining != sizeof(ulong))
                return;

            ulong clientNonce = payload.ReadUInt64();
            if (clientNonce == 0)
                return;

            if (_peersByHandle.TryGetValue(remote, out Peer existing))
            {
                if (existing.ClientNonce == clientNonce)
                {
                    existing.LastReceiveTime = now;
                    SendServerAccept(existing);
                    return;
                }

                if (existing.State == ServerPeerState.Connected)
                    return;

                RemovePeer(existing, false);
            }

            if (_peersById.Count >= MaximumPeers)
                return;

            var peer = new Peer(
                AllocatePeerId(),
                clientNonce,
                CreateRandomUInt64(),
                remote,
                now);

            _peersByHandle.Add(remote, peer);
            _peersById.Add(peer.Id, peer);

            _transport.RegisterRemote(remote);

            SendServerAccept(peer);
        }

        private void HandleClientReady(
            ITransportHandle remote,
            PacketReader payload,
            double now)
        {
            if (payload.Remaining !=
                sizeof(uint) + sizeof(ulong))
            {
                return;
            }

            uint peerId =
                payload.ReadUInt32();

            ulong token =
                payload.ReadUInt64();

            if (!TryAuthenticate(
                    remote,
                    peerId,
                    token,
                    out Peer peer))
            {
                return;
            }

            peer.LastReceiveTime = now;

            bool becameConnected =
                peer.State !=
                ServerPeerState.Connected;

            // This is sequence zero on the reliable stream.
            // Spawn messages will be queued after it.
            bool readyQueued =
                SendMessage(
                    peer.Handle,
                    NetworkMessageType.ServerReady,
                    writer =>
                        writer.Write(peer.Id),
                    NetworkDelivery.ReliableOrdered);

            if (!readyQueued)
                return;

            if (becameConnected)
            {
                peer.State =
                    ServerPeerState.Connected;

                PeerConnected?.Invoke(peer);
            }
        }

        private void HandleHeartbeat(
            ITransportHandle remote,
            PacketReader payload,
            double now)
        {
            if (payload.Remaining != sizeof(uint) + sizeof(ulong))
                return;

            uint peerId = payload.ReadUInt32();
            ulong token = payload.ReadUInt64();
            if (!TryAuthenticate(remote, peerId, token, out Peer peer) ||
                peer.State != ServerPeerState.Connected)
                return;

            peer.LastReceiveTime = now;
            SendMessage(
                peer.Handle,
                NetworkMessageType.HeartbeatAck,
                writer => writer.Write(peer.Id));
        }

        private void HandleDisconnect(ITransportHandle remote, PacketReader payload)
        {
            if (payload.Remaining != sizeof(uint) + sizeof(ulong))
                return;

            uint peerId = payload.ReadUInt32();
            ulong token = payload.ReadUInt64();
            if (TryAuthenticate(remote, peerId, token, out Peer peer))
                RemovePeer(peer, peer.State == ServerPeerState.Connected);
        }

        private bool TryAuthenticate(
            ITransportHandle remote,
            uint peerId,
            ulong token,
            out Peer peer)
        {
            peer = null;
            if (!_peersByHandle.TryGetValue(remote, out Peer byHandle) ||
                !_peersById.TryGetValue(peerId, out Peer byId) ||
                !ReferenceEquals(byHandle, byId) ||
                byHandle.SessionToken != token)
                return false;

            peer = byHandle;
            return true;
        }

        private void SendServerAccept(Peer peer)
        {
            SendMessage(
                peer.Handle,
                NetworkMessageType.ServerAccept,
                writer =>
                {
                    writer.Write(peer.ClientNonce);
                    writer.Write(peer.Id);
                    writer.Write(peer.SessionToken);
                });
        }

        private bool SendMessage(
            ITransportHandle destination,
            NetworkMessageType type,
            Action<PacketWriter> writePayload,
            NetworkDelivery delivery =
                NetworkDelivery.Unreliable)
        {
            byte[] data =
                NetworkProtocol.Encode(
                    type,
                    writePayload);

            bool sent =
                _transport.Send(
                    destination,
                    data,
                    delivery);

            if (!sent)
            {
                Error?.Invoke(
                    $"Could not queue {type} " +
                    "message for sending.");
            }

            return sent;
        }

        private void RemoveTimedOutPeers(double now)
        {
            _timedOutPeers.Clear();
            foreach (Peer peer in _peersById.Values)
            {
                double timeout = peer.State == ServerPeerState.Connected
                    ? ConnectedTimeout
                    : PendingTimeout;

                if (now - peer.LastReceiveTime >= timeout)
                    _timedOutPeers.Add(peer);
            }

            foreach (Peer peer in _timedOutPeers)
                RemovePeer(peer, peer.State == ServerPeerState.Connected);

            _timedOutPeers.Clear();
        }

        private void RemovePeer(
            Peer peer,
            bool notify)
        {
            _peersByHandle.Remove(peer.Handle);
            _peersById.Remove(peer.Id);

            _transport.RemoveRemote(peer.Handle);

            if (notify)
                PeerDisconnected?.Invoke(peer.Id);
        }

        private uint AllocatePeerId()
        {
            for (int attempt = 0; attempt <= MaximumPeers; attempt++)
            {
                uint candidate = _nextPeerId++;
                if (candidate == 0)
                    candidate = _nextPeerId++;

                if (!_peersById.ContainsKey(candidate))
                    return candidate;
            }

            throw new InvalidOperationException("No peer IDs are available.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameServer));
        }

        private static ulong CreateRandomUInt64()
        {
            byte[] bytes = new byte[sizeof(ulong)];
            ulong value;

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                do
                {
                    random.GetBytes(bytes);
                    value = BitConverter.ToUInt64(bytes, 0);
                }
                while (value == 0);
            }

            return value;
        }
    }
}
