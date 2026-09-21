using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using TMPro;

public class UdpClientController : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;

    [Header("UI & Referências")]
    public TMP_InputField ipInputField;
    public ScoreUIController scoreUIController;

    [Header("Objetos Visuais")]
    public Transform p1Paddle;
    public Transform p2Paddle;
    public Transform ballTransform;

    private UdpClient client;
    private IPEndPoint serverEndPoint;
    private int myPlayerID = 0;

    public void ConnectToServer()
    {
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
        {
            serverIP = ipInputField.text;
        }

        client = new UdpClient();
        serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
        client.BeginReceive(OnDataReceived, null);

        SendData("CONNECT");
    }

    private void Update()
    {
        if (myPlayerID == 0) return;

        float input = Input.GetAxisRaw("Vertical");
        if (input != 0)
        {
            SendData($"MOVE:{input}");
        }
    }

    private void OnDataReceived(System.IAsyncResult result)
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] data = client.EndReceive(result, ref remoteEP);
        string message = Encoding.UTF8.GetString(data);

        if (message.StartsWith("ASSIGN"))
        {
            myPlayerID = int.Parse(message.Split(':')[1]);
        }
        else if (message.StartsWith("STATE"))
        {
            string[] parts = message.Split('|');
            if (parts.Length >= 5)
            {
                float p1Y = float.Parse(parts[1]);
                float p2Y = float.Parse(parts[2]);
                float ballX = float.Parse(parts[3]);
                float ballY = float.Parse(parts[4]);

                p1Paddle.position = new Vector3(p1Paddle.position.x, p1Y, 0);
                p2Paddle.position = new Vector3(p2Paddle.position.x, p2Y, 0);
                ballTransform.position = new Vector3(ballX, ballY, 0);
            }
        }
        else if (message.StartsWith("SCORE"))
        {
            string[] parts = message.Split('|');
            if (parts.Length >= 3 && scoreUIController != null)
            {
                int p1Score = int.Parse(parts[1]);
                int p2Score = int.Parse(parts[2]);
                scoreUIController.SetScore(p1Score, p2Score);
            }
        }

        client.BeginReceive(OnDataReceived, null);
    }

    private void SendData(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        client.Send(data, data.Length, serverEndPoint);
    }

    private void OnApplicationQuit()
    {
        client?.Close();
    }
}