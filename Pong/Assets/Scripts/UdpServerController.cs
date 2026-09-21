using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class PongServerController : MonoBehaviour
{
    public int port = 9050;
    private UdpClient udpServer;
    private IPEndPoint client1EndPoint = null;
    private IPEndPoint client2EndPoint = null;

    [Header("Componentes do Servidor")]
    public PaddleServer player1Paddle;
    public PaddleServer player2Paddle;
    public Transform ballTransform;

    private float p1Input = 0f;
    private float p2Input = 0f;

    void Start()
    {
        udpServer = new UdpClient(port);
        udpServer.BeginReceive(OnDataReceived, null);
        Debug.Log("Servidor rodando na porta " + port);
    }

    private void OnDataReceived(System.IAsyncResult result)
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] receivedBytes = udpServer.EndReceive(result, ref remoteEP);
        string message = Encoding.UTF8.GetString(receivedBytes);

        if (message == "CONNECT")
        {
            RegisterClient(remoteEP);
        }
        else if (message.StartsWith("MOVE"))
        {
            string[] parts = message.Split(':');
            if (parts.Length >= 2)
            {
                float dir = float.Parse(parts[1]);
                if (remoteEP.Equals(client1EndPoint)) p1Input = dir;
                else if (remoteEP.Equals(client2EndPoint)) p2Input = dir;
            }
        }

        udpServer.BeginReceive(OnDataReceived, null);
    }

    private void RegisterClient(IPEndPoint endPoint)
    {
        if (client1EndPoint == null)
        {
            client1EndPoint = endPoint;
            SendToClient(client1EndPoint, "ASSIGN:1");
        }
        else if (client2EndPoint == null && !endPoint.Equals(client1EndPoint))
        {
            client2EndPoint = endPoint;
            SendToClient(client2EndPoint, "ASSIGN:2");
        }
    }

    void Update()
    {
        if (p1Input != 0 && player1Paddle != null)
        {
            player1Paddle.MovePaddle(p1Input);
            p1Input = 0f;
        }
        if (p2Input != 0 && player2Paddle != null)
        {
            player2Paddle.MovePaddle(p2Input);
            p2Input = 0f;
        }

        string gameState = $"STATE|{player1Paddle.transform.position.y}|{player2Paddle.transform.position.y}|{ballTransform.position.x}|{ballTransform.position.y}";
        BroadcastData(gameState);
    }

    public void BroadcastData(string message)
    {
        if (client1EndPoint != null) SendToClient(client1EndPoint, message);
        if (client2EndPoint != null) SendToClient(client2EndPoint, message);
    }

    private void SendToClient(IPEndPoint endPoint, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        udpServer.Send(data, data.Length, endPoint);
    }

    private void OnApplicationQuit()
    {
        udpServer?.Close();
    }
}