using UnityEngine;

namespace Networking
{
    [RequireComponent(typeof(NetObject))]
    public abstract class NetBehaviour
        : MonoBehaviour
    {
        public abstract NetComponentType ComponentType
        {
            get;
        }

        public NetObject NetObject
        {
            get;
            private set;
        }

        internal void Bind(NetObject netObject)
        {
            NetObject = netObject;
        }

        public virtual void OnNetSpawn()
        {
        }

        public virtual void OnNetDespawn()
        {
        }

        public abstract void WriteState(
            PacketWriter writer);

        public abstract void ReadState(
            PacketReader reader,
            uint serverTick);
    }
}