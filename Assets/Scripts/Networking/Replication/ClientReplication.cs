using System;
using UnityEngine;

namespace Networking
{
    public sealed class ClientReplication : IDisposable
    {
        private const int SpawnPayloadSize =
            sizeof(ushort) +
            sizeof(uint) +
            sizeof(uint) +
            (sizeof(float) * 3) +
            (sizeof(float) * 4);

        private readonly GameClient _client;
        private readonly NetworkWorld _world;

        private bool _disposed;

        public ClientReplication(
            GameClient client,
            NetworkWorld world)
        {
            _client = client ??
                throw new ArgumentNullException(
                    nameof(client));

            _world = world ??
                throw new ArgumentNullException(
                    nameof(world));

            _client.MessageReceived +=
                OnMessageReceived;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _client.MessageReceived -=
                OnMessageReceived;
        }

        private void OnMessageReceived(
            NetworkMessageType type,
            byte[] data)
        {
            try
            {
                switch (type)
                {
                    case NetworkMessageType.ObjectSpawn:
                        HandleSpawn(data);
                        break;

                    case NetworkMessageType.ObjectDespawn:
                        HandleDespawn(data);
                        break;
                }
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    $"Discarded malformed {type}: " +
                    exception.Message);
            }
        }

        private void HandleSpawn(byte[] data)
        {
            if (data == null ||
                data.Length != SpawnPayloadSize)
            {
                return;
            }

            var reader =
                new PacketReader(data);

            ushort prefabId =
                reader.ReadUInt16();

            uint networkId =
                reader.ReadUInt32();

            uint ownerPeerId =
                reader.ReadUInt32();

            var position =
                new Vector3(
                    reader.ReadFloat(),
                    reader.ReadFloat(),
                    reader.ReadFloat());

            var rotation =
                new Quaternion(
                    reader.ReadFloat(),
                    reader.ReadFloat(),
                    reader.ReadFloat(),
                    reader.ReadFloat());

            if (prefabId == 0 ||
                networkId == 0 ||
                !IsFinite(position) ||
                !IsFinite(rotation))
            {
                return;
            }

            float magnitudeSquared =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;

            if (magnitudeSquared < 0.0001f ||
                float.IsInfinity(magnitudeSquared))
            {
                return;
            }

            rotation = rotation.normalized;

            _world.SpawnOrResolveReplica(
                prefabId,
                networkId,
                ownerPeerId,
                position,
                rotation);
        }

        private void HandleDespawn(byte[] data)
        {
            if (data == null ||
                data.Length != sizeof(uint))
            {
                return;
            }

            var reader =
                new PacketReader(data);

            uint networkId =
                reader.ReadUInt32();

            if (networkId != 0)
                _world.DespawnReplica(networkId);
        }

        private static bool IsFinite(Vector3 value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(
            Quaternion value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z) &&
                IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}