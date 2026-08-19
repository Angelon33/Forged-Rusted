using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;

namespace Networking
{
    public sealed class ServerBootstrap : MonoBehaviour
    {
        [SerializeField] private ushort port = 25565;

        private GameServer _server;

        private void Start()
        {
            _server = new GameServer(new UdpTransport());
            _server.PeerConnected += OnPeerConnected;
            _server.PeerDisconnected += OnPeerDisconnected;
            _server.Error += OnServerError;
            _server.Start(port);
            Debug.Log($"Server started on UDP port {port}.");
        }

        private void Update()
        {
            _server?.Update(GetTime());
        }

        private void OnDestroy()
        {
            if (_server == null)
                return;

            _server.PeerConnected -= OnPeerConnected;
            _server.PeerDisconnected -= OnPeerDisconnected;
            _server.Error -= OnServerError;
            _server.Dispose();
            _server = null;
        }

        private static void OnPeerConnected(Peer peer)
        {
            Debug.Log($"Peer {peer.Id} connected.");
        }

        private static void OnPeerDisconnected(uint peerId)
        {
            Debug.Log($"Peer {peerId} disconnected.");
        }

        private static void OnServerError(string message)
        {
            Debug.LogWarning(message);
        }

        private static double GetTime()
        {
            return (double)Stopwatch.GetTimestamp() /
                   Stopwatch.Frequency;
        }
    }
}
