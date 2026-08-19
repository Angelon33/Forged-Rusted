using System;
using System.Security.Cryptography;

namespace Networking
{
    public sealed class GameClient : IDisposable
    {
        private const double RetryInterval = 0.75;
        private const double ConnectTimeout = 10.0;
        private const double HeartbeatInterval = 2.0;
        private const double ServerTimeout = 15.0;
        private const int MaximumEventsPerUpdate = 256;

        private readonly INetworkTransport _transport;
        private ITransportHandle _server;
        private ClientConnectionState _state = ClientConnectionState.Stopped;
        private ulong _clientNonce;
        private ulong _sessionToken;
        private uint _peerId;
        private double _startedAt;
        private double _lastSendTime;
        private double _lastServerReceiveTime;
        private bool _disposed;

        public ClientConnectionState State => _state;
        public uint PeerId => _peerId;
        public bool IsConnected => _state == ClientConnectionState.Connected;

        public event Action<ClientConnectionState> StateChanged;
        public event Action<string> Error;

        public GameClient(INetworkTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public void Connect(string address, ushort port, double now)
        {
            ThrowIfDisposed();

            if (_state != ClientConnectionState.Stopped)
                throw new InvalidOperationException("Client has already been started.");

            _server = _transport.StartClient(address, port);
            _clientNonce = CreateRandomUInt64();
            _startedAt = now;
            _lastServerReceiveTime = now;
            SetState(ClientConnectionState.SendingHello);
            SendHello(now);
        }

        public void Update(double now)
        {
            if (_disposed || _state == ClientConnectionState.Stopped)
                return;

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

                if (transportEvent.Remote == null || !_server.Equals(transportEvent.Remote))
                    continue;

                HandleDatagram(transportEvent.Data, now);
            }

            if (_state == ClientConnectionState.SendingHello ||
                _state == ClientConnectionState.SendingReady)
            {
                if (now - _startedAt >= ConnectTimeout)
                {
                    TimeOut("Connection handshake timed out.");
                    return;
                }

                if (now - _lastSendTime >= RetryInterval)
                {
                    if (_state == ClientConnectionState.SendingHello)
                        SendHello(now);
                    else
                        SendReady(now);
                }

                return;
            }

            if (_state != ClientConnectionState.Connected)
                return;

            if (now - _lastServerReceiveTime >= ServerTimeout)
            {
                TimeOut("Server connection timed out.");
                return;
            }

            if (now - _lastSendTime >= HeartbeatInterval)
                SendHeartbeat(now);
        }

        public void Disconnect()
        {
            if (_disposed)
                return;

            if (_state == ClientConnectionState.Connected)
            {
                SendMessage(
                    NetworkMessageType.Disconnect,
                    writer =>
                    {
                        writer.Write(_peerId);
                        writer.Write(_sessionToken);
                    });
            }

            _transport.Stop();
            SetState(ClientConnectionState.Stopped);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Disconnect();
            _transport.Dispose();
            _disposed = true;
        }

        private void HandleDatagram(byte[] data, double now)
        {
            if (!NetworkProtocol.TryDecode(
                    data,
                    out NetworkMessageType type,
                    out PacketReader payload))
                return;

            try
            {
                switch (type)
                {
                    case NetworkMessageType.ServerAccept:
                        HandleServerAccept(payload, now);
                        break;
                    case NetworkMessageType.ServerReady:
                        HandleServerReady(payload, now);
                        break;
                    case NetworkMessageType.HeartbeatAck:
                        HandleHeartbeatAck(payload, now);
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                // Malformed payloads are ignored.
            }
        }

        private void HandleServerAccept(PacketReader payload, double now)
        {
            const int expectedSize = sizeof(ulong) + sizeof(uint) + sizeof(ulong);
            if (payload.Remaining != expectedSize ||
                (_state != ClientConnectionState.SendingHello &&
                 _state != ClientConnectionState.SendingReady))
                return;

            ulong nonce = payload.ReadUInt64();
            uint peerId = payload.ReadUInt32();
            ulong token = payload.ReadUInt64();

            if (nonce != _clientNonce || peerId == 0 || token == 0)
                return;

            if (_state == ClientConnectionState.SendingReady &&
                (_peerId != peerId || _sessionToken != token))
                return;

            _peerId = peerId;
            _sessionToken = token;
            _lastServerReceiveTime = now;
            SetState(ClientConnectionState.SendingReady);
            SendReady(now);
        }

        private void HandleServerReady(PacketReader payload, double now)
        {
            if (payload.Remaining != sizeof(uint) ||
                (_state != ClientConnectionState.SendingReady &&
                 _state != ClientConnectionState.Connected))
                return;

            uint peerId = payload.ReadUInt32();
            if (peerId != _peerId)
                return;

            _lastServerReceiveTime = now;
            SetState(ClientConnectionState.Connected);
        }

        private void HandleHeartbeatAck(PacketReader payload, double now)
        {
            if (payload.Remaining != sizeof(uint) ||
                _state != ClientConnectionState.Connected)
                return;

            if (payload.ReadUInt32() == _peerId)
                _lastServerReceiveTime = now;
        }

        private void SendHello(double now)
        {
            SendMessage(
                NetworkMessageType.ClientHello,
                writer => writer.Write(_clientNonce));
            _lastSendTime = now;
        }

        private void SendReady(double now)
        {
            SendMessage(
                NetworkMessageType.ClientReady,
                writer =>
                {
                    writer.Write(_peerId);
                    writer.Write(_sessionToken);
                });
            _lastSendTime = now;
        }

        private void SendHeartbeat(double now)
        {
            SendMessage(
                NetworkMessageType.Heartbeat,
                writer =>
                {
                    writer.Write(_peerId);
                    writer.Write(_sessionToken);
                });
            _lastSendTime = now;
        }

        private void SendMessage(
            NetworkMessageType type,
            Action<PacketWriter> writePayload)
        {
            byte[] data = NetworkProtocol.Encode(type, writePayload);
            if (!_transport.Send(_server, data))
                Error?.Invoke($"Could not queue {type} message for sending.");
        }

        private void TimeOut(string reason)
        {
            Error?.Invoke(reason);
            _transport.Stop();
            SetState(ClientConnectionState.TimedOut);
        }

        private void SetState(ClientConnectionState state)
        {
            if (_state == state)
                return;

            _state = state;
            StateChanged?.Invoke(state);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameClient));
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
