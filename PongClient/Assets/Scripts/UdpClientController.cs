using UnityEngine;
using TMPro;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;

public class UdpClientController : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField ipInputField;
    public GameObject menuPanel;

    [Header("Objetos em Cena")]
    public Transform p1Paddle;
    public Transform p2Paddle;
    public Transform ballTransform;
    public float paddleSpeed = 10f;

    private UdpClient client;
    private IPEndPoint serverEP;
    private Thread receiveThread;

    private int myId = -1;
    private bool isConnected = false;

    private Vector3 targetBallPos;
    private Vector3 targetP1Pos;
    private Vector3 targetP2Pos;

    public void ConnectToServer()
    {
        string ip = ipInputField != null && !string.IsNullOrEmpty(ipInputField.text) ? ipInputField.text : "127.0.0.1";

        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse(ip), 5001);
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        // Envia mensagem inicial ao servidor dedicado
        byte[] hello = Encoding.UTF8.GetBytes("HELLO");
        client.Send(hello, hello.Length);

        if (menuPanel != null) menuPanel.SetActive(false);
        isConnected = true;
    }

    void Update()
    {
        if (!isConnected) return;

        float inputV = Input.GetAxis("Vertical");

        // Transmite o movimento da raquete atribuída
        if (myId == 1 && p1Paddle != null)
        {
            p1Paddle.Translate(Vector3.up * inputV * paddleSpeed * Time.deltaTime);
            SendPosition(p1Paddle.position.y);
        }
        else if (myId == 2 && p2Paddle != null)
        {
            p2Paddle.Translate(Vector3.up * inputV * paddleSpeed * Time.deltaTime);
            SendPosition(p2Paddle.position.y);
        }

        // Interpolação do estado vindo do Servidor
        if (ballTransform != null)
            ballTransform.position = Vector3.Lerp(ballTransform.position, targetBallPos, Time.deltaTime * 25f);

        if (myId == 1 && p2Paddle != null)
            p2Paddle.position = Vector3.Lerp(p2Paddle.position, targetP2Pos, Time.deltaTime * 20f);
        else if (myId == 2 && p1Paddle != null)
            p1Paddle.position = Vector3.Lerp(p1Paddle.position, targetP1Pos, Time.deltaTime * 20f);
    }

    void SendPosition(float yPos)
    {
        string msg = "POS:" + yPos.ToString("F2", CultureInfo.InvariantCulture);
        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length);
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (isConnected)
        {
            try
            {
                byte[] data = client.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);

                if (msg.StartsWith("ASSIGN:"))
                {
                    myId = int.Parse(msg.Substring(7));
                    Debug.Log("[CLIENTE] Conectado e atribuído como Jogador " + myId);
                }
                else if (msg.StartsWith("STATE:"))
                {
                    string[] parts = msg.Substring(6).Split(';');
                    if (parts.Length == 4)
                    {
                        float bx = float.Parse(parts[0], CultureInfo.InvariantCulture);
                        float by = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float p1y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float p2y = float.Parse(parts[3], CultureInfo.InvariantCulture);

                        targetBallPos = new Vector3(bx, by, 0);
                        targetP1Pos = new Vector3(p1Paddle.position.x, p1y, 0);
                        targetP2Pos = new Vector3(p2Paddle.position.x, p2y, 0);
                    }
                }
            }
            catch { break; }
        }
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
        if (client != null) client.Close();
    }
}