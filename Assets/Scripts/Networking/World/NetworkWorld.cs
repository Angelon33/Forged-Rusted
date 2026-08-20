using System;
using System.Collections.Generic;
using UnityEngine;

namespace Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkWorld : MonoBehaviour
    {
        [SerializeField]
        private Transform spawnedObjectRoot;

        private readonly Dictionary<uint, NetObject>
            _objects =
                new Dictionary<uint, NetObject>();

        private NetworkRuntime _runtime;

        private uint _nextNetworkId = 1;
        private bool _initialized;

        public int ObjectCount =>
            _objects.Count;

        public IEnumerable<NetObject> Objects =>
            _objects.Values;

        public event Action<NetObject>
            ObjectSpawned;

        public event Action<uint>
            ObjectDespawned;

        internal void Initialize(
            NetworkRuntime runtime)
        {
            if (_initialized)
            {
                throw new InvalidOperationException(
                    "NetworkWorld is already initialized.");
            }

            _runtime = runtime ??
                throw new ArgumentNullException(
                    nameof(runtime));

            _initialized = true;
        }

        public NetObject SpawnAuthoritative(
            NetObject prefab,
            Vector3 position,
            Quaternion rotation,
            uint ownerPeerId = 0)
        {
            EnsureInitialized();

            if (!_runtime.RunsServer)
            {
                throw new InvalidOperationException(
                    "Only a server runtime can spawn " +
                    "authoritative objects.");
            }

            if (prefab == null)
            {
                throw new ArgumentNullException(
                    nameof(prefab));
            }

            NetObject instance =
                Instantiate(
                    prefab,
                    position,
                    rotation,
                    spawnedObjectRoot);

            try
            {
                Register(
                    instance,
                    AllocateNetworkId(),
                    ownerPeerId);

                return instance;
            }
            catch
            {
                Destroy(instance.gameObject);
                throw;
            }
        }

        public void RegisterReplica(
            NetObject instance,
            uint networkId,
            uint ownerPeerId)
        {
            EnsureInitialized();

            if (!_runtime.RunsClient)
            {
                throw new InvalidOperationException(
                    "This runtime does not " +
                    "contain a client.");
            }

            Register(
                instance,
                networkId,
                ownerPeerId);
        }

        public bool TryGet(
            uint networkId,
            out NetObject netObject)
        {
            return _objects.TryGetValue(
                networkId,
                out netObject);
        }

        public bool Despawn(uint networkId)
        {
            if (!_objects.TryGetValue(
                    networkId,
                    out NetObject netObject))
            {
                return false;
            }

            _objects.Remove(networkId);

            if (netObject != null)
                netObject.ClearNetworkState();

            ObjectDespawned?.Invoke(networkId);

            if (netObject != null)
                Destroy(netObject.gameObject);

            return true;
        }

        internal void Shutdown()
        {
            if (!_initialized)
                return;

            foreach (NetObject netObject
                     in _objects.Values)
            {
                if (netObject == null)
                    continue;

                netObject.ClearNetworkState();
                Destroy(netObject.gameObject);
            }

            _objects.Clear();

            _nextNetworkId = 1;
            _runtime = null;
            _initialized = false;
        }

        private void Register(
            NetObject netObject,
            uint networkId,
            uint ownerPeerId)
        {
            if (netObject == null)
            {
                throw new ArgumentNullException(
                    nameof(netObject));
            }

            if (networkId == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(networkId));
            }

            if (_objects.ContainsKey(networkId))
            {
                throw new InvalidOperationException(
                    $"Network ID {networkId} " +
                    "is already registered.");
            }

            // Register first so OnNetSpawn can resolve itself.
            _objects.Add(
                networkId,
                netObject);

            try
            {
                netObject.Initialize(
                    networkId,
                    ownerPeerId);
            }
            catch
            {
                _objects.Remove(networkId);
                throw;
            }

            ObjectSpawned?.Invoke(netObject);
        }

        private uint AllocateNetworkId()
        {
            while (true)
            {
                uint candidate =
                    _nextNetworkId++;

                if (candidate != 0 &&
                    !_objects.ContainsKey(candidate))
                {
                    return candidate;
                }
            }
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "NetworkWorld is not initialized.");
            }
        }
    }
}