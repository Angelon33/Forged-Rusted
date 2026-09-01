using System;

namespace Networking
{
    public sealed class ServerNetworkModule
        : INetworkModule
    {
        private const double TickInterval =
            1.0 / 33.0;

        private const int MaximumTicksPerFrame = 5;
        private const double MaximumFrameTime = 0.25;

        private readonly GameServer _server;

        private readonly ServerReplication
            _replication;

        private readonly NetworkDiagnostics
            _diagnostics;

        private double _accumulator;
        private uint _serverTick;
        private bool _disposed;

        public ServerNetworkModule(
            GameServer server,
            ServerReplication replication,
            NetworkDiagnostics diagnostics)
        {
            _server = server ??
                throw new ArgumentNullException(
                    nameof(server));

            _replication = replication ??
                throw new ArgumentNullException(
                    nameof(replication));

            _diagnostics = diagnostics ??
                throw new ArgumentNullException(
                    nameof(diagnostics));
        }

        public void Tick(
            double now,
            double deltaTime)
        {
            if (_disposed)
                return;

            // Receive new input before running simulation.
            _server.Update(now);

            _accumulator +=
                Math.Min(
                    Math.Max(deltaTime, 0.0),
                    MaximumFrameTime);

            int ticks = 0;

            while (_accumulator >= TickInterval &&
                   ticks < MaximumTicksPerFrame)
            {
                _serverTick++;

                _diagnostics.ServerTick =
                    _serverTick;

                _replication.Tick(
                    _serverTick,
                    (float)TickInterval);

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
            _server.Dispose();
        }
    }
}
