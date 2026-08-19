using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;

namespace Networking
{
    public sealed class ListenServerBootstrap : MonoBehaviour
    {
        [SerializeField]
        private ushort port = 25565;

        private GameServer _server;
        private GameClient _localClient;

        private void Start()
        {
            LoopbackTransport.CreatePair(
                out LoopbackTransport serverLoopback,
                out LoopbackTransport clientLoopback);

            var serverTransport =
                new CompositeServerTransport(
                    new UdpTransport(),
                    serverLoopback);

            _server = new GameServer(serverTransport);

            _server.PeerConnected += OnPeerConnected;
            _server.PeerDisconnected += OnPeerDisconnected;
            _server.Error += OnServerError;

            _server.Start(port);

            _localClient =
                new GameClient(clientLoopback);

            _localClient.StateChanged +=
                OnLocalClientStateChanged;

            _localClient.Error +=
                OnLocalClientError;

            _localClient.Connect(
                "loopback",
                0,
                GetTime());

            Debug.Log(
                $"Listen server started on UDP port {port}.");
        }

        private void Update()
        {
            double now = GetTime();

            _server?.Update(now);
            _localClient?.Update(now);
        }

        private void OnDestroy()
        {
            if (_localClient != null)
            {
                _localClient.StateChanged -=
                    OnLocalClientStateChanged;

                _localClient.Error -=
                    OnLocalClientError;

                _localClient.Dispose();
                _localClient = null;
            }

            if (_server != null)
            {
                _server.PeerConnected -=
                    OnPeerConnected;

                _server.PeerDisconnected -=
                    OnPeerDisconnected;

                _server.Error -=
                    OnServerError;

                _server.Dispose();
                _server = null;
            }
        }

        private static void OnLocalClientStateChanged(
            ClientConnectionState state)
        {
            Debug.Log(
                $"Local client connection state: {state}");
        }

        private static void OnLocalClientError(
            string message)
        {
            Debug.LogWarning(
                $"Local client: {message}");
        }

        private static void OnPeerConnected(
            Peer peer)
        {
            Debug.Log(
                $"Peer {peer.Id} connected.");
        }

        private static void OnPeerDisconnected(
            uint peerId)
        {
            Debug.Log(
                $"Peer {peerId} disconnected.");
        }

        private static void OnServerError(
            string message)
        {
            Debug.LogWarning(message);
        }

        private static double GetTime()
        {
            return
                (double)Stopwatch.GetTimestamp() /
                Stopwatch.Frequency;
        }
    }
}