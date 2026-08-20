using System;

namespace Networking
{
    public interface INetworkModule : IDisposable
    {
        void Tick(double now, double deltaTime);
    }
}