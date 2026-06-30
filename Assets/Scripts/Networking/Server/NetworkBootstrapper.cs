using Unity.VisualScripting;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{

    private GameServer server;

    void Start()
    {
        var transport = new UdpTransport();

        server = new GameServer(transport);

        server.Start();
    }

    void Update()
    {
        server?.Update();
        while (server.TryDequeueLog(out var msg))
        {
            Debug.Log(msg);
        }
    }

    void OnApplicationQuit()
    {
        server?.Close();
    }
}
