using System;

namespace Networking
{
    public sealed class ServerNetworkModule
        : INetworkModule
    {
        private readonly GameServer _server;
        private bool _disposed;

        public ServerNetworkModule(GameServer server)
        {
            _server = server ??
                throw new ArgumentNullException(
                    nameof(server));
        }

        public void Tick(
            double now,
            double deltaTime)
        {
            if (_disposed)
                return;

            _server.Update(now);

        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _server.Dispose();
        }
    }
}