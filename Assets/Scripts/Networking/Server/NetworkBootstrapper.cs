using Unity.VisualScripting;
using UnityEngine;

namespace Networking {
    public class NetworkBootstrap : MonoBehaviour
    {

        private GameServer server;

        void Start()
        {
            var transport = new UdpTransport();

            server = new GameServer(transport);

            server.Start();
        }

        void FixedUpdate()
        {
            server?.Update();
        }

        void OnApplicationQuit()
        {
            server?.Close();
        }
    }
}
