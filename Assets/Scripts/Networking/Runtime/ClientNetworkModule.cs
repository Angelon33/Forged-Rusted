using System;

namespace Networking
{
    public sealed class ClientNetworkModule
        : INetworkModule
    {
        private const double TickInterval =
            1.0 / 33.0;

        private const int MaximumTicksPerFrame = 5;
        private const double MaximumFrameTime = 0.25;

        private readonly GameClient _client;

        private readonly ClientReplication
            _replication;

        private double _accumulator;
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

            float frameDelta =
                (float)Math.Min(
                    Math.Max(deltaTime, 0.0),
                    MaximumFrameTime);

            // Remote visuals interpolate every rendered frame.
            _replication.Interpolate(frameDelta);

            _accumulator += frameDelta;

            int ticks = 0;

            while (_accumulator >= TickInterval &&
                   ticks < MaximumTicksPerFrame)
            {
                _replication.SendInput();

                _accumulator -= TickInterval;
                ticks++;
            }

            if (ticks == MaximumTicksPerFrame &&
                _accumulator >= TickInterval)
            {
                _accumulator = 0.0;
            }
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