using System;
using UnityEngine;

namespace Networking
{
    public sealed class ServerReplication : IDisposable
    {
        private readonly GameServer _server;
        private readonly NetworkWorld _world;

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

            _world.ObjectSpawned +=
                OnObjectSpawned;

            _world.ObjectDespawned +=
                OnObjectDespawned;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _server.PeerConnected -=
                OnPeerConnected;

            _world.ObjectSpawned -=
                OnObjectSpawned;

            _world.ObjectDespawned -=
                OnObjectDespawned;
        }

        private void OnPeerConnected(Peer peer)
        {
            // Send the complete existing roster
            // to the newly connected peer.
            foreach (NetObject netObject
                     in _world.Objects)
            {
                SendSpawn(
                    peer,
                    netObject);
            }
        }

        private void OnObjectSpawned(
            NetObject netObject)
        {
            _server.Broadcast(
                NetworkMessageType.ObjectSpawn,
                writer =>
                    WriteSpawn(
                        writer,
                        netObject));
        }

        private void OnObjectDespawned(
            uint networkId)
        {
            _server.Broadcast(
                NetworkMessageType.ObjectDespawn,
                writer =>
                    writer.Write(networkId));
        }

        private void SendSpawn(
            Peer peer,
            NetObject netObject)
        {
            _server.Send(
                peer,
                NetworkMessageType.ObjectSpawn,
                writer =>
                    WriteSpawn(
                        writer,
                        netObject));
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
    }
}