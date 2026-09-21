using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UdpServerController : MonoBehaviour
{
    [Header("Configurações da Rede")]
    public int port = 9050;
    private UdpClient udpServer;
    private IPEndPoint remoteEP;

    [Header("Referências do Jogo")]
    public Transform ball;
    public Rigidbody2D ballRb;
    public Transform player1Paddle;
    public Transform player2Paddle;

    [Header("Configurações da Bola")]
    public float ballSpeed = 12f;

    // Clientes Conectados
    private IPEndPoint client1EP = null;
    private IPEndPoint client2EP = null;

    private int score1 = 0;
    private int score2 = 0;
    private bool gameStarted = false;

    void Start()
    {
        // Inicializa o servidor UDP na porta especificada
        udpServer = new UdpClient(port);
        udpServer.Client.ReceiveTimeout = 1; // Leitura não-bloqueante
        remoteEP = new IPEndPoint(IPAddress.Any, 0);

        // Deixa a bola parada no centro aguardando jogadores
        ResetBall();
        Debug.Log($"[SERVIDOR] Servidor rodando na porta {port}. Aguardando conexões...");
    }

    void Update()
    {
        // Proteção contra referências nulas
        if (ball == null || ballRb == null || player1Paddle == null || player2Paddle == null)
        {
            Debug.LogWarning("[SERVIDOR] Por favor, atribua a Bola e as Raquetes no Inspector!");
            return;
        }

        // 1. Receber mensagens dos Clientes
        ReceiveMessages();

        // 2. Se a partida começou, mantém a física e velocidade da bola
        if (gameStarted)
        {
            MaintainBallPhysics();
        }

        // 3. Enviar atualização do estado do jogo para todos os clientes
        SendGameState();
    }

    private void ReceiveMessages()
    {
        try
        {
            while (udpServer.Available > 0)
            {
                byte[] data = udpServer.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data).Trim();

                if (message == "CONNECT")
                {
                    HandleConnection(remoteEP);
                }
                else if (message.StartsWith("MOVE:"))
                {
                    HandleMovement(remoteEP, message);
                }
            }
        }
        catch (Exception)
        {
            // Exceções de timeout de leitura são ignoradas intencionalmente
        }
    }

    private void HandleConnection(IPEndPoint endpoint)
    {
        if (client1EP == null)
        {
            client1EP = endpoint;
            SendToClient("ROLE:1", client1EP);
            Debug.Log($"[SERVIDOR] Jogador 1 conectado: {client1EP}");
        }
        else if (client2EP == null && !endpoint.Equals(client1EP))
        {
            client2EP = endpoint;
            SendToClient("ROLE:2", client2EP);
            Debug.Log($"[SERVIDOR] Jogador 2 conectado: {client2EP}");
        }

        // Se ambos se conectaram, inicia o jogo!
        if (client1EP != null && client2EP != null && !gameStarted)
        {
            StartGame();
        }
    }

    private void HandleMovement(IPEndPoint endpoint, string message)
    {
        string[] parts = message.Split(':');
        if (parts.Length < 2) return;

        if (float.TryParse(parts[1], out float newY))
        {
            // Atualiza a posição Y da raquete correspondente
            if (endpoint.Equals(client1EP))
            {
                player1Paddle.position = new Vector3(player1Paddle.position.x, newY, 0);
            }
            else if (endpoint.Equals(client2EP))
            {
                player2Paddle.position = new Vector3(player2Paddle.position.x, newY, 0);
            }
        }
    }

    private void StartGame()
    {
        gameStarted = true;
        Debug.Log("[SERVIDOR] Dois jogadores conectados! Liberando a bola...");
        LaunchBall();
    }

    public void ResetBall()
    {
        ball.position = Vector3.zero;
        ballRb.linearVelocity = Vector2.zero;
        gameStarted = false;
    }

    public void LaunchBall()
    {
        float x = UnityEngine.Random.value > 0.5f ? 1f : -1f;
        float y = UnityEngine.Random.Range(-0.5f, 0.5f);
        Vector2 dir = new Vector2(x, y).normalized;
        ballRb.linearVelocity = dir * ballSpeed;
        gameStarted = true;
    }

    private void MaintainBallPhysics()
    {
        // Evita loop perfeitamente horizontal
        if (Mathf.Abs(ballRb.linearVelocity.y) < 0.5f)
        {
            float randomY = UnityEngine.Random.Range(0.3f, 0.8f) * (UnityEngine.Random.value > 0.5f ? 1 : -1);
            ballRb.linearVelocity = new Vector2(ballRb.linearVelocity.x, randomY);
        }

        // Mantém a velocidade constante
        ballRb.linearVelocity = ballRb.linearVelocity.normalized * ballSpeed;
    }

    private void SendGameState()
    {
        // Formato da mensagem: STATE:ballX:ballY:p1Y:p2Y:score1:score2
        string state = $"STATE:{ball.position.x:F2}:{ball.position.y:F2}:{player1Paddle.position.y:F2}:{player2Paddle.position.y:F2}:{score1}:{score2}";

        if (client1EP != null) SendToClient(state, client1EP);
        if (client2EP != null) SendToClient(state, client2EP);
    }

    private void SendToClient(string message, IPEndPoint ep)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        udpServer.Send(data, data.Length, ep);
    }

    private void OnDestroy()
    {
        if (udpServer != null) udpServer.Close();
    }

    // Método chamado pelos scripts de Gol no servidor
    public void ScorePoint(int playerNumber)
    {
        if (playerNumber == 1) score1++;
        else if (playerNumber == 2) score2++;

        ResetBall();
        // Relança a bola se ainda tivermos os 2 jogadores conectados
        if (client1EP != null && client2EP != null)
        {
            Invoke(nameof(LaunchBall), 1.0f); // Espera 1 segundo para relançar
        }
    }
}