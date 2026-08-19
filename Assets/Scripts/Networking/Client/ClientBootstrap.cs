using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;

namespace Networking
{
    public sealed class ClientBootstrap : MonoBehaviour
    {
        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField] private ushort serverPort = 25565;

        private GameClient _client;

        private void Start()
        {
            _client = new GameClient(new UdpTransport());
            _client.StateChanged += OnStateChanged;
            _client.Error += OnClientError;
            _client.Connect(serverAddress, serverPort, GetTime());
        }

        private void Update()
        {
            _client?.Update(GetTime());
        }

        private void OnDestroy()
        {
            if (_client == null)
                return;

            _client.StateChanged -= OnStateChanged;
            _client.Error -= OnClientError;
            _client.Dispose();
            _client = null;
        }

        private static void OnStateChanged(ClientConnectionState state)
        {
            Debug.Log($"Client connection state: {state}");
        }

        private static void OnClientError(string message)
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
