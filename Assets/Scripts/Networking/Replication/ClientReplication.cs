using System;
using System.Collections.Generic;
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

        private const int MaximumSnapshotEntries = 32;

        private const float TickDelta =
            1f / 33f;

        private readonly GameClient _client;
        private readonly NetworkWorld _world;

        private readonly HashSet<NetTransform>
            _interpolated =
                new HashSet<NetTransform>();

        private NetObject _localPlayer;
        private PlayerInputReader _inputReader;
        private NetCharacterMotor _predictedMotor;

        // Zero means no input has been processed, so real
        // input sequences begin at one.
        private uint _nextInputSequence = 1;

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

        public void SendInput()
        {
            if (_disposed ||
                !_client.IsConnected)
            {
                return;
            }

            EnsureLocalPlayer();

            if (_localPlayer == null ||
                _inputReader == null ||
                _predictedMotor == null)
            {
                return;
            }

            PlayerInputMessage message =
                _inputReader.BuildMessage(
                    _localPlayer.NetworkId,
                    _nextInputSequence++);

            _client.Send(
                NetworkMessageType.PlayerInput,
                writer =>
                    message.Write(writer),
                NetworkDelivery.UnreliableSequenced);

            // Predict immediately instead of waiting for the
            // server's snapshot to return.
            _predictedMotor.Predict(
                message,
                TickDelta);
        }

        public void Interpolate(float deltaTime)
        {
            if (_disposed ||
                deltaTime <= 0f)
            {
                return;
            }

            NetworkRuntime runtime =
                NetworkRuntime.Current;

            if (runtime != null &&
                runtime.RunsServer)
            {
                // Listen servers share authoritative objects.
                return;
            }

            _interpolated.RemoveWhere(
                netTransform =>
                    netTransform == null ||
                    !netTransform.NetObject.IsSpawned);

            foreach (
                NetTransform netTransform
                in _interpolated)
            {
                netTransform.Interpolate(deltaTime);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _client.MessageReceived -=
                OnMessageReceived;

            _inputReader?.Deactivate();
            _predictedMotor?.StopPrediction();

            _inputReader = null;
            _predictedMotor = null;
            _localPlayer = null;

            _interpolated.Clear();
        }

        private void EnsureLocalPlayer()
        {
            if (_localPlayer != null &&
                _localPlayer.IsSpawned &&
                _localPlayer.IsLocallyOwned &&
                _inputReader != null &&
                _predictedMotor != null)
            {
                return;
            }

            _inputReader?.Deactivate();
            _predictedMotor?.StopPrediction();

            _inputReader = null;
            _predictedMotor = null;
            _localPlayer = null;

            foreach (NetObject netObject in _world.Objects)
            {
                if (netObject == null ||
                    !netObject.IsLocallyOwned ||
                    !netObject.TryGetComponent(
                        out PlayerInputReader reader) ||
                    !netObject.TryGetBehaviour(
                        NetComponentType.CharacterMotor,
                        out NetBehaviour behaviour) ||
                    !(behaviour is
                        NetCharacterMotor predictedMotor))
                {
                    continue;
                }

                _localPlayer = netObject;
                _inputReader = reader;
                _predictedMotor = predictedMotor;

                _inputReader.Activate();
                _predictedMotor.StartPrediction();

                return;
            }
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

                    case NetworkMessageType.WorldSnapshot:
                        HandleWorldSnapshot(data);
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

            _world.SpawnOrResolveReplica(
                prefabId,
                networkId,
                ownerPeerId,
                position,
                rotation.normalized);
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

            if (_localPlayer != null &&
                _localPlayer.NetworkId == networkId)
            {
                _inputReader?.Deactivate();
                _predictedMotor?.StopPrediction();

                _inputReader = null;
                _predictedMotor = null;
                _localPlayer = null;
            }

            if (networkId != 0)
                _world.DespawnReplica(networkId);
        }

        private void HandleWorldSnapshot(
            byte[] data)
        {
            NetworkRuntime runtime =
                NetworkRuntime.Current;

            if (runtime != null &&
                runtime.RunsServer)
            {
                // Never apply loopback snapshots to shared,
                // authoritative listen-server objects.
                return;
            }

            if (data == null ||
                data.Length <
                    sizeof(uint) +
                    sizeof(ushort))
            {
                return;
            }

            var reader =
                new PacketReader(data);

            uint serverTick =
                reader.ReadUInt32();

            ushort entryCount =
                reader.ReadUInt16();

            if (entryCount >
                MaximumSnapshotEntries)
            {
                return;
            }

            for (int index = 0;
                 index < entryCount;
                 index++)
            {
                const int entryHeaderSize =
                    sizeof(uint) +
                    sizeof(byte) +
                    sizeof(ushort);

                if (reader.Remaining <
                    entryHeaderSize)
                {
                    return;
                }

                uint networkId =
                    reader.ReadUInt32();

                var componentType =
                    (NetComponentType)
                    reader.ReadByte();

                ushort stateLength =
                    reader.ReadUInt16();

                if (networkId == 0 ||
                    stateLength == 0 ||
                    reader.Remaining < stateLength)
                {
                    return;
                }

                byte[] state =
                    reader.ReadBytes(stateLength);

                if (!_world.TryGet(
                        networkId,
                        out NetObject netObject) ||
                    netObject == null)
                {
                    continue;
                }

                if (componentType ==
                        NetComponentType.Transform &&
                    netObject.IsLocallyOwned &&
                    netObject.TryGetBehaviour(
                        NetComponentType.CharacterMotor,
                        out _))
                {
                    // NetCharacterMotor reconciles the local
                    // predicted object. Applying NetTransform too
                    // would cause the two systems to fight.
                    continue;
                }

                if (!netObject.TryApplyState(
                        componentType,
                        state,
                        serverTick))
                {
                    continue;
                }

                if (componentType ==
                        NetComponentType.Transform &&
                    netObject.TryGetBehaviour(
                        NetComponentType.Transform,
                        out NetBehaviour behaviour) &&
                    behaviour is
                        NetTransform netTransform)
                {
                    _interpolated.Add(netTransform);
                }
            }

            if (reader.Remaining != 0)
            {
                throw new InvalidOperationException(
                    "WorldSnapshot contains trailing bytes.");
            }
        }

        private static bool IsFinite(
            Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(
            Quaternion value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z) &&
                   IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}