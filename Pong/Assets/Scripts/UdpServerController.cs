using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;

public class UdpServerController : MonoBehaviour
{
    private UdpClient server;
    private IPEndPoint anyEP;
    private Thread receiveThread;

    private Dictionary<string, int> clientIds = new Dictionary<string, int>();
    private Dictionary<int, IPEndPoint> clientEndPoints = new Dictionary<int, IPEndPoint>();

    [Header("Objetos do Jogo (Física Central)")]
    public Transform p1Paddle;
    public Transform p2Paddle;
    public Ball ball;

    private float p1TargetY;
    private float p2TargetY;

    void Start()
    {
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("[SERVIDOR DEDICADO] Rodando na porta 5001. Aguardando 2 clientes...");
    }

    void Update()
    {
        // Atualiza posição das raquetes no servidor baseado na rede
        if (p1Paddle != null) p1Paddle.position = new Vector3(p1Paddle.position.x, p1TargetY, 0);
        if (p2Paddle != null) p2Paddle.position = new Vector3(p2Paddle.position.x, p2TargetY, 0);

        // Envia estado atualizado do jogo para ambos os clientes conectados
        BroadcastGameState();
    }

    void BroadcastGameState()
    {
        if (clientEndPoints.Count == 0) return;

        string bX = ball.transform.position.x.ToString("F2", CultureInfo.InvariantCulture);
        string bY = ball.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);
        string p1Y = p1Paddle.position.y.ToString("F2", CultureInfo.InvariantCulture);
        string p2Y = p2Paddle.position.y.ToString("F2", CultureInfo.InvariantCulture);

        string stateMsg = $"STATE:{bX};{bY};{p1Y};{p2Y}";
        byte[] bdata = Encoding.UTF8.GetBytes(stateMsg);

        foreach (var kvp in clientEndPoints)
        {
            server.Send(bdata, bdata.Length, kvp.Value);
        }
    }

    void ReceiveData()
    {
        while (true)
        {
            try
            {
                byte[] data = server.Receive(ref anyEP);
                string msg = Encoding.UTF8.GetString(data);
                string key = anyEP.Address + ":" + anyEP.Port;

                // Registra novos clientes (Até no máximo 2)
                if (!clientIds.ContainsKey(key) && clientIds.Count < 2)
                {
                    int assignedId = clientIds.Count + 1;
                    clientIds[key] = assignedId;
                    clientEndPoints[assignedId] = new IPEndPoint(anyEP.Address, anyEP.Port);

                    string assignMsg = "ASSIGN:" + assignedId;
                    byte[] aData = Encoding.UTF8.GetBytes(assignMsg);
                    server.Send(aData, aData.Length, anyEP);

                    Debug.Log($"[SERVIDOR] Cliente registrado como Jogador {assignedId}");
                }

                if (clientIds.ContainsKey(key))
                {
                    int id = clientIds[key];
                    if (msg.StartsWith("POS:"))
                    {
                        float yVal = float.Parse(msg.Substring(4), CultureInfo.InvariantCulture);
                        if (id == 1) p1TargetY = yVal;
                        else if (id == 2) p2TargetY = yVal;
                    }
                }
            }
            catch { break; }
        }
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
        if (server != null) server.Close();
    }
}