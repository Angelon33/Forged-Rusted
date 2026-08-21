using System;

namespace Networking
{
    public sealed class ClientNetworkModule
        : INetworkModule
    {
        private readonly GameClient _client;
        private readonly ClientReplication _replication;

        private bool _disposed;

        public ClientNetworkModule(
            GameClient client,
            ClientReplication replication)
        {
            _client = client ??
                throw new ArgumentNullException(
                    nameof(client));

            _replication = replication ??
                throw new ArgumentNullException(
                    nameof(replication));
        }

        public void Tick(
            double now,
            double deltaTime)
        {
            if (_disposed)
                return;

            _client.Update(now);

            // Snapshot interpolation
            // will be added here later.
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _replication.Dispose();
            _client.Dispose();
        }
    }
}