using System;
using System.Collections.Generic;
using System.Globalization;
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

    [Header("Objetos do Jogo")]
    public Transform player1Paddle;
    public Transform player2Paddle;
    public Ball ball; // Referência ao script Ball do Servidor

    public float paddleSpeed = 10f;
    private bool gameStarted = false;

    // Fila para executar ações da Thread de Rede dentro da Main Thread do Unity
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

    void Start()
    {
        // Garante que a bola comece parada no centro
        if (ball != null && ball.rb != null)
        {
            ball.rb.linearVelocity = Vector2.zero;
        }

        udpServer = new UdpClient(port);
        udpServer.BeginReceive(OnDataReceived, null);
        Debug.Log("[SERVIDOR] Rodando na porta " + port + ". Aguardando jogadores...");
    }

    void Update()
    {
        // Executa comandos recebidos da rede com segurança na Main Thread
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue()?.Invoke();
            }
        }

        // Transmite o estado do jogo para os clientes apenas após ambos conectarem
        if (gameStarted)
        {
            string p1Y = player1Paddle.position.y.ToString("F2", CultureInfo.InvariantCulture);
            string p2Y = player2Paddle.position.y.ToString("F2", CultureInfo.InvariantCulture);
            string bX = ball.transform.position.x.ToString("F2", CultureInfo.InvariantCulture);
            string bY = ball.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);

            string gameState = $"STATE|{p1Y}|{p2Y}|{bX}|{bY}";

            if (client1EndPoint != null) SendToClient(client1EndPoint, gameState);
            if (client2EndPoint != null) SendToClient(client2EndPoint, gameState);
        }
    }

    private void OnDataReceived(IAsyncResult result)
    {
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] receivedBytes = udpServer.EndReceive(result, ref remoteEP);
            string message = Encoding.UTF8.GetString(receivedBytes);

            if (message == "CONNECT")
            {
                lock (mainThreadActions)
                {
                    mainThreadActions.Enqueue(() => RegisterClient(remoteEP));
                }
            }
            else if (message.StartsWith("MOVE"))
            {
                lock (mainThreadActions)
                {
                    mainThreadActions.Enqueue(() => ProcessMovement(remoteEP, message));
                }
            }

            udpServer.BeginReceive(OnDataReceived, null);
        }
        catch (ObjectDisposedException) { }
    }

    private void RegisterClient(IPEndPoint endPoint)
    {
        if (client1EndPoint == null)
        {
            client1EndPoint = endPoint;
            SendToClient(client1EndPoint, "ASSIGN:1");
            Debug.Log("[SERVIDOR] Jogador 1 conectado: " + endPoint);
        }
        else if (client2EndPoint == null && !endPoint.Equals(client1EndPoint))
        {
            client2EndPoint = endPoint;
            SendToClient(client2EndPoint, "ASSIGN:2");
            Debug.Log("[SERVIDOR] Jogador 2 conectado: " + endPoint);

            // Inicia o jogo automaticamente ao conectar o 2º cliente
            StartGame();
        }
    }

    private void StartGame()
    {
        gameStarted = true;
        Debug.Log("[SERVIDOR] Ambos os jogadores conectados! Lançando a bola...");
        
        if (ball != null)
        {
            ball.LaunchBall(); // Executa o método de lançamento da bola no Servidor
        }
    }

    private void ProcessMovement(IPEndPoint sender, string message)
    {
        if (!gameStarted) return; // Não permite mover antes do jogo começar

        string[] parts = message.Split(':');
        if (parts.Length < 2) return;

        if (float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float direction))
        {
            if (sender.Equals(client1EndPoint))
            {
                player1Paddle.Translate(Vector3.up * direction * paddleSpeed * Time.deltaTime);
            }
            else if (sender.Equals(client2EndPoint))
            {
                player2Paddle.Translate(Vector3.up * direction * paddleSpeed * Time.deltaTime);
            }
        }
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