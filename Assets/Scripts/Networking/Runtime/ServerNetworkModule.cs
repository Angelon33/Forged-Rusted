using System;

namespace Networking
{
    public sealed class ServerNetworkModule
        : INetworkModule
    {
        private readonly GameServer _server;
        private readonly ServerReplication _replication;

        private bool _disposed;

        public ServerNetworkModule(
            GameServer server,
            ServerReplication replication)
        {
            _server = server ??
                throw new ArgumentNullException(
                    nameof(server));

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

            _server.Update(now);

            // Authoritative 33 Hz simulation
            // will be added here later.
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _replication.Dispose();
            _server.Dispose();
        }
    }
}