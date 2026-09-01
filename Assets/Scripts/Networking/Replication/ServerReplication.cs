using System;
using System.Collections.Generic;
using UnityEngine;

namespace Networking
{
    public sealed class ServerReplication : IDisposable
    {
        // A player now produces both a Transform entry and a
        // CharacterMotor entry. Fourteen fully populated players
        // still fit below the current maximum packet size.
        private const int MaximumObjectsPerSnapshot = 14;

        private readonly GameServer _server;
        private readonly NetworkWorld _world;

        private readonly Dictionary<uint, PlayerState>
            _playersByPeer =
                new Dictionary<uint, PlayerState>();

        private readonly List<NetObject>
            _snapshotObjects =
                new List<NetObject>(
                    MaximumObjectsPerSnapshot);

        private bool _disposed;

        public ServerReplication(
            GameServer server,
            NetworkWorld world)
        {
            _server = server ??
                throw new ArgumentNullException(
                    nameof(server));

            _world = world ??
                throw new ArgumentNullException(
                    nameof(world));

            _server.PeerConnected +=
                OnPeerConnected;

            _server.PeerDisconnected +=
                OnPeerDisconnected;

            _server.MessageReceived +=
                OnMessageReceived;

            _world.ObjectSpawned +=
                OnObjectSpawned;

            _world.ObjectDespawned +=
                OnObjectDespawned;

            foreach (NetObject netObject in _world.Objects)
                RegisterPlayer(netObject);
        }

        public void Tick(
            uint serverTick,
            float deltaTime)
        {
            if (_disposed)
                return;

            SimulatePlayers(deltaTime);
            SendWorldSnapshot(serverTick);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _server.PeerConnected -=
                OnPeerConnected;

            _server.PeerDisconnected -=
                OnPeerDisconnected;

            _server.MessageReceived -=
                OnMessageReceived;

            _world.ObjectSpawned -=
                OnObjectSpawned;

            _world.ObjectDespawned -=
                OnObjectDespawned;

            _playersByPeer.Clear();
            _snapshotObjects.Clear();
        }

        private void OnPeerConnected(Peer peer)
        {
            foreach (NetObject netObject in _world.Objects)
                SendSpawn(peer, netObject);
        }

        private void OnPeerDisconnected(uint peerId)
        {
            _playersByPeer.Remove(peerId);
        }

        private void OnObjectSpawned(
            NetObject netObject)
        {
            RegisterPlayer(netObject);

            _server.Broadcast(
                NetworkMessageType.ObjectSpawn,
                writer =>
                    WriteSpawn(writer, netObject),
                NetworkDelivery.ReliableOrdered);
        }

        private void OnObjectDespawned(
            uint networkId)
        {
            uint ownerToRemove = 0;

            foreach (
                KeyValuePair<uint, PlayerState> entry
                in _playersByPeer)
            {
                if (entry.Value.NetworkId ==
                    networkId)
                {
                    ownerToRemove = entry.Key;
                    break;
                }
            }

            if (ownerToRemove != 0)
                _playersByPeer.Remove(ownerToRemove);

            _server.Broadcast(
                NetworkMessageType.ObjectDespawn,
                writer =>
                    writer.Write(networkId),
                NetworkDelivery.ReliableOrdered);
        }

        private void RegisterPlayer(
            NetObject netObject)
        {
            if (netObject == null ||
                netObject.OwnerPeerId == 0 ||
                !netObject.TryGetComponent(
                    out CharacterMotor motor) ||
                !netObject.TryGetBehaviour(
                    NetComponentType.CharacterMotor,
                    out NetBehaviour behaviour) ||
                !(behaviour is
                    NetCharacterMotor networkMotor))
            {
                return;
            }

            motor.SetSimulationEnabled(true);

            _playersByPeer[netObject.OwnerPeerId] =
                new PlayerState(
                    netObject.NetworkId,
                    motor,
                    networkMotor,
                    new ServerInputCommandBuffer(
                        netObject.NetworkId,
                        netObject.transform.eulerAngles.y));
        }

        private void OnMessageReceived(
            Peer peer,
            NetworkMessageType type,
            byte[] data)
        {
            if (type !=
                    NetworkMessageType.PlayerInput ||
                !PlayerInputBatchMessage.TryRead(
                    data,
                    out PlayerInputBatchMessage batch) ||
                !_playersByPeer.TryGetValue(
                    peer.Id,
                    out PlayerState state) ||
                batch.NetworkId !=
                    state.NetworkId)
            {
                return;
            }

            for (int index = 0;
                 index < batch.Commands.Length;
                 index++)
            {
                state.InputBuffer.TryInsert(
                    batch.Commands[index]);
            }
        }

        private void SimulatePlayers(
            float deltaTime)
        {
            foreach (
                PlayerState state
                in _playersByPeer.Values)
            {
                if (state.Motor == null)
                    continue;

                PlayerInputMessage command =
                    state.InputBuffer.GetCommandForTick(
                        out bool consumesSequence);

                state.Motor.Simulate(
                    command,
                    deltaTime);

                if (consumesSequence)
                {
                    state.InputBuffer.MarkSimulated(
                        command);

                    state.NetworkMotor
                        .SetLastProcessedInputSequence(
                            command.InputSequence);
                }
            }
        }

        private void SendWorldSnapshot(
            uint serverTick)
        {
            _snapshotObjects.Clear();

            foreach (NetObject netObject in _world.Objects)
            {
                if (netObject == null ||
                    _snapshotObjects.Count >=
                        MaximumObjectsPerSnapshot ||
                    !netObject.TryGetBehaviour(
                        NetComponentType.Transform,
                        out _))
                {
                    continue;
                }

                _snapshotObjects.Add(netObject);
            }

            _server.Broadcast(
                NetworkMessageType.WorldSnapshot,
                writer =>
                {
                    writer.Write(serverTick);

                    int entryCount = 0;

                    for (int index = 0;
                         index < _snapshotObjects.Count;
                         index++)
                    {
                        NetObject netObject =
                            _snapshotObjects[index];

                        // Every selected object has a transform.
                        entryCount++;

                        if (netObject.TryGetBehaviour(
                                NetComponentType.CharacterMotor,
                                out _))
                        {
                            entryCount++;
                        }
                    }

                    writer.Write(
                        (ushort)entryCount);

                    for (int index = 0;
                         index < _snapshotObjects.Count;
                         index++)
                    {
                        NetObject netObject =
                            _snapshotObjects[index];

                        netObject.TryGetBehaviour(
                            NetComponentType.Transform,
                            out NetBehaviour transformBehaviour);

                        WriteStateEntry(
                            writer,
                            netObject.NetworkId,
                            transformBehaviour);

                        if (netObject.TryGetBehaviour(
                                NetComponentType.CharacterMotor,
                                out NetBehaviour characterBehaviour))
                        {
                            WriteStateEntry(
                                writer,
                                netObject.NetworkId,
                                characterBehaviour);
                        }
                    }
                },
                NetworkDelivery.UnreliableSequenced);
        }

        private static void WriteStateEntry(
            PacketWriter writer,
            uint networkId,
            NetBehaviour behaviour)
        {
            var stateWriter =
                new PacketWriter();

            behaviour.WriteState(
                stateWriter);

            byte[] state =
                stateWriter.ToArray();

            writer.Write(networkId);
            writer.Write((byte)behaviour.ComponentType);
            writer.Write((ushort)state.Length);
            writer.Write(state);
        }

        private void SendSpawn(
            Peer peer,
            NetObject netObject)
        {
            _server.Send(
                peer,
                NetworkMessageType.ObjectSpawn,
                writer =>
                    WriteSpawn(writer, netObject),
                NetworkDelivery.ReliableOrdered);
        }

        private static void WriteSpawn(
            PacketWriter writer,
            NetObject netObject)
        {
            Transform objectTransform =
                netObject.transform;

            Vector3 position =
                objectTransform.position;

            Quaternion rotation =
                objectTransform.rotation;

            writer.Write(netObject.PrefabId);
            writer.Write(netObject.NetworkId);
            writer.Write(netObject.OwnerPeerId);

            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);

            writer.Write(rotation.x);
            writer.Write(rotation.y);
            writer.Write(rotation.z);
            writer.Write(rotation.w);
        }

        private sealed class PlayerState
        {
            public uint NetworkId { get; }

            public CharacterMotor Motor { get; }

            public NetCharacterMotor NetworkMotor
            {
                get;
            }

            public ServerInputCommandBuffer InputBuffer
            {
                get;
            }

            public PlayerState(
                uint networkId,
                CharacterMotor motor,
                NetCharacterMotor networkMotor,
                ServerInputCommandBuffer inputBuffer)
            {
                NetworkId = networkId;
                Motor = motor;
                NetworkMotor = networkMotor;
                InputBuffer = inputBuffer ??
                    throw new ArgumentNullException(
                        nameof(inputBuffer));
            }
        }
    }
}
