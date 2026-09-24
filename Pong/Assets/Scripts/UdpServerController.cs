using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UdpServerController : MonoBehaviour
{
    [Header("Configurações do Servidor")]
    public int listenPort = 9050;
    public int maxScore = 5;
    public float paddleSpeed = 0.25f; // Distância por comando de movimento recebido
    public float paddleMinY = -3.8f;
    public float paddleMaxY = 3.8f;

    [Header("Objetos do Jogo na Cena")]
    public Transform p1Paddle;
    public Transform p2Paddle;
    public Transform ballTransform;
    public Ball ballScript;

    private UdpClient udpServer;
    private List<IPEndPoint> connectedClients = new List<IPEndPoint>();

    private int scoreP1 = 0;
    private int scoreP2 = 0;
    private bool gameStarted = false;
    private bool isGameOver = false;

    private static readonly Queue<Action> mainThreadQueue = new Queue<Action>();

    private void Start()
    {
        Application.runInBackground = true;

        if (ballScript == null)
        {
            ballScript = FindAnyObjectByType<Ball>();
        }

        try
        {
            udpServer = new UdpClient(listenPort);

            const int SIO_UDP_CONNRESET = -1744830452;
            try
            {
                udpServer.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }
            catch { }

            udpServer.BeginReceive(OnDataReceived, null);
            Debug.Log($"[SERVIDOR] Servidor rodando na porta {listenPort}. Aguardando 2 jogadores...");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SERVIDOR] Erro ao iniciar servidor UDP: {e.Message}");
        }
    }

    private void Update()
    {
        // Executa eventos pendentes da fila da rede
        lock (mainThreadQueue)
        {
            while (mainThreadQueue.Count > 0)
            {
                mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        if (!gameStarted || isGameOver) return;

        SendStateToClients();
    }

    private void OnDataReceived(IAsyncResult result)
    {
        try
        {
            if (udpServer == null || udpServer.Client == null) return;

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udpServer.EndReceive(result, ref remoteEP);
            string message = Encoding.UTF8.GetString(data).Trim();

            if (message.Equals("CONNECT"))
            {
                HandleConnection(remoteEP);
            }
            else if (message.StartsWith("MOVE:"))
            {
                int playerIndex = connectedClients.IndexOf(remoteEP);
                if (playerIndex != -1 && float.TryParse(message.Split(':')[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float moveDir))
                {
                    EnqueueMainThread(() => MovePlayer(playerIndex + 1, moveDir));
                }
            }
            else if (message.Equals("RESTART"))
            {
                EnqueueMainThread(RestartGame);
            }

            udpServer.BeginReceive(OnDataReceived, null);
        }
        catch (ObjectDisposedException) { }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVIDOR] Erro ao receber dados UDP: " + e.Message);
        }
    }

    private void HandleConnection(IPEndPoint remoteEP)
    {
        if (!connectedClients.Contains(remoteEP) && connectedClients.Count < 2)
        {
            connectedClients.Add(remoteEP);
            int assignedID = connectedClients.Count;

            SendToClient($"ASSIGN:{assignedID}", remoteEP);
            Debug.Log($"[SERVIDOR] Jogador {assignedID} conectado de {remoteEP}");

            if (connectedClients.Count == 2 && !gameStarted)
            {
                gameStarted = true;
                Debug.Log("[SERVIDOR] Ambos os jogadores conectados! Iniciando partida...");

                EnqueueMainThread(() =>
                {
                    if (ballScript != null)
                    {
                        ballScript.LaunchBall();
                    }
                });
            }
        }
        else if (connectedClients.Contains(remoteEP))
        {
            int existingID = connectedClients.IndexOf(remoteEP) + 1;
            SendToClient($"ASSIGN:{existingID}", remoteEP);
        }
    }

    private void MovePlayer(int playerId, float dir)
    {
        Transform paddle = (playerId == 1) ? p1Paddle : p2Paddle;
        if (paddle == null) return;

        // Movimento direto sem Time.deltaTime (evita zerar o movimento na fila de eventos)
        float newY = paddle.position.y + (dir * paddleSpeed);
        newY = Mathf.Clamp(newY, paddleMinY, paddleMaxY);

        paddle.position = new Vector3(paddle.position.x, newY, paddle.position.z);
    }

    public void AddPointToPlayer(int playerNum)
    {
        if (isGameOver) return;

        if (playerNum == 1) scoreP1++;
        else if (playerNum == 2) scoreP2++;

        Debug.Log($"[SERVIDOR] Gol! Placar: P1 {scoreP1} x {scoreP2} P2");

        SendBroadcastMessage($"SCORE|{scoreP1}|{scoreP2}");

        if (scoreP1 >= maxScore)
        {
            EndGame(1);
        }
        else if (scoreP2 >= maxScore)
        {
            EndGame(2);
        }
    }

    private void EndGame(int winnerPlayer)
    {
        isGameOver = true;
        Debug.Log($"[SERVIDOR] Fim de jogo! Jogador {winnerPlayer} venceu.");
        SendBroadcastMessage($"GAME_OVER|{winnerPlayer}");
    }

    public void RestartGame()
    {
        scoreP1 = 0;
        scoreP2 = 0;
        isGameOver = false;

        SendBroadcastMessage($"SCORE|{scoreP1}|{scoreP2}");
        SendBroadcastMessage("GAME_RESET");

        if (ballScript != null)
        {
            ballScript.transform.position = Vector3.zero;
            ballScript.LaunchBall();
        }
    }

    private void SendStateToClients()
    {
        if (p1Paddle == null || p2Paddle == null || ballTransform == null) return;

        // Garante que o ponto decimal seja formatado de forma limpa
        string p1Y = p1Paddle.position.y.ToString("F2", CultureInfo.InvariantCulture);
        string p2Y = p2Paddle.position.y.ToString("F2", CultureInfo.InvariantCulture);
        string bX = ballTransform.position.x.ToString("F2", CultureInfo.InvariantCulture);
        string bY = ballTransform.position.y.ToString("F2", CultureInfo.InvariantCulture);

        string stateMsg = $"STATE|{p1Y}|{p2Y}|{bX}|{bY}";
        SendBroadcastMessage(stateMsg);
    }

    public void SendBroadcastMessage(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        foreach (var clientEP in connectedClients)
        {
            udpServer?.Send(data, data.Length, clientEP);
        }
    }

    private void SendToClient(string msg, IPEndPoint clientEP)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        udpServer?.Send(data, data.Length, clientEP);
    }

    private void EnqueueMainThread(Action action)
    {
        lock (mainThreadQueue)
        {
            mainThreadQueue.Enqueue(action);
        }
    }

    private void OnDestroy()
    {
        udpServer?.Close();
    }

    private void OnApplicationQuit()
    {
        udpServer?.Close();
    }
}