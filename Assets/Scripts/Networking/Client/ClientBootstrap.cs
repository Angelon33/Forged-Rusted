using UnityEngine;

public class ClientBootstrap : MonoBehaviour
{
    private SimpleUdpClient client;

    void Start()
    {
        client = new SimpleUdpClient();

        client.Start("127.0.0.1", 25565);

        InvokeRepeating(nameof(Send), 1f, 0.01f);
    }

    void Send()
    {
        client.SendHello();
    }

    void OnDestroy()
    {
        client.Stop();
    }

    void Update()
    {
    }
}
