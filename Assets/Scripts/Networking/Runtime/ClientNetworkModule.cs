using System;

namespace Networking
{
    public sealed class ClientNetworkModule
        : INetworkModule
    {
        private readonly GameClient _client;
        private bool _disposed;

        public ClientNetworkModule(GameClient client)
        {
            _client = client ??
                throw new ArgumentNullException(
                    nameof(client));
        }

        public void Tick(
            double now,
            double deltaTime)
        {
            if (_disposed)
                return;

            _client.Update(now);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _client.Dispose();
        }
    }
}