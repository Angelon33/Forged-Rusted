using System.Collections.Generic;
using UnityEngine;

namespace Networking
{
    [DefaultExecutionOrder(-900)]
    public sealed class PeerSpawnExample : MonoBehaviour
    {
        [SerializeField]
        private NetObject playerPrefab;

        [SerializeField]
        private Vector3 firstSpawnPosition =
            Vector3.zero;

        [SerializeField]
        private float spacing = 2.5f;

        private readonly Dictionary<uint, uint>
            _objectByPeer =
                new Dictionary<uint, uint>();

        private NetworkRuntime _runtime;
        private int _spawnIndex;

        private void Start()
        {
            _runtime =
                NetworkRuntime.Current;

            if (_runtime == null ||
                !_runtime.RunsServer)
            {
                enabled = false;
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError(
                    "Assign a player prefab " +
                    "to PeerSpawnExample.");

                enabled = false;
                return;
            }

            _runtime.PeerConnected +=
                OnPeerConnected;

            _runtime.PeerDisconnected +=
                OnPeerDisconnected;
        }

        private void OnDestroy()
        {
            if (_runtime == null)
                return;

            _runtime.PeerConnected -=
                OnPeerConnected;

            _runtime.PeerDisconnected -=
                OnPeerDisconnected;
        }

        private void OnPeerConnected(Peer peer)
        {
            if (_objectByPeer.ContainsKey(peer.Id))
                return;

            Vector3 position =
                firstSpawnPosition +
                Vector3.right *
                (_spawnIndex++ * spacing);

            NetObject netObject =
                _runtime.World.SpawnAuthoritative(
                    playerPrefab,
                    position,
                    Quaternion.identity,
                    peer.Id);

            _objectByPeer.Add(
                peer.Id,
                netObject.NetworkId);
        }

        private void OnPeerDisconnected(
            uint peerId)
        {
            if (!_objectByPeer.TryGetValue(
                    peerId,
                    out uint networkId))
            {
                return;
            }

            _objectByPeer.Remove(peerId);

            _runtime.World.Despawn(networkId);
        }
    }
}