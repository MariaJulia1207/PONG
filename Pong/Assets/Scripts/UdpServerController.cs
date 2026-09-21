using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UdpServerController : MonoBehaviour
{
    public int port = 9050;

    [Header("Objetos do Jogo (Servidor)")]
    public Transform p1Paddle;
    public Transform p2Paddle;
    public Transform ballTransform;

    [Header("Configurações do Jogo")]
    public float paddleSpeed = 8f;
    public float ballSpeed = 10f;
    public int maxScore = 5;

    private UdpClient server;
    private IPEndPoint p1EndPoint;
    private IPEndPoint p2EndPoint;

    private int scoreP1 = 0;
    private int scoreP2 = 0;
    private bool isGameOver = false;
    private bool gameStarted = false;

    private Vector2 ballVelocity;

    void Start()
    {
        server = new UdpClient(port);
        server.BeginReceive(OnDataReceived, null);
        Debug.Log("[SERVIDOR] Servidor iniciado na porta " + port);

        // Zera pontuações e trava a bola no centro
        ResetGameToWaitingState();
    }

    void Update()
    {
        // Se ambos não conectaram ou se o jogo acabou, força a bola parada
        if (!gameStarted || isGameOver)
        {
            if (ballTransform != null) ballTransform.position = Vector3.zero;
            BroadcastState();
            return;
        }

        // Movimentação da bola
        ballTransform.Translate(ballVelocity * Time.deltaTime);

        // 1. Rebatida nas paredes (Eixo Y)
        if (Mathf.Abs(ballTransform.position.y) > 4.5f)
        {
            ballVelocity.y = -ballVelocity.y;
            float clampedY = Mathf.Clamp(ballTransform.position.y, -4.5f, 4.5f);
            ballTransform.position = new Vector3(ballTransform.position.x, clampedY, 0);
        }

        // 2. Colisão com Raquetes
        CheckPaddleCollision();

        // 3. Verificação de Ponto (Eixo X)
        if (ballTransform.position.x > 9f)
        {
            AddPointToPlayer(1);
        }
        else if (ballTransform.position.x < -9f)
        {
            AddPointToPlayer(2);
        }

        // Envia o estado das posições para os clientes
        BroadcastState();
    }

    private void CheckPaddleCollision()
    {
        if (p1Paddle != null && ballTransform.position.x <= p1Paddle.position.x + 0.5f && ballTransform.position.x >= p1Paddle.position.x - 0.5f)
        {
            if (Mathf.Abs(ballTransform.position.y - p1Paddle.position.y) <= 1.5f && ballVelocity.x < 0)
            {
                BounceFromPaddle(p1Paddle.position.y);
            }
        }

        if (p2Paddle != null && ballTransform.position.x >= p2Paddle.position.x - 0.5f && ballTransform.position.x <= p2Paddle.position.x + 0.5f)
        {
            if (Mathf.Abs(ballTransform.position.y - p2Paddle.position.y) <= 1.5f && ballVelocity.x > 0)
            {
                BounceFromPaddle(p2Paddle.position.y);
            }
        }
    }

    private void BounceFromPaddle(float paddleY)
    {
        float dirX = -ballVelocity.x;
        float dirY = (ballTransform.position.y - paddleY) * 1.5f;

        if (Mathf.Abs(dirY) < 0.2f)
        {
            dirY = Random.value > 0.5f ? 0.5f : -0.5f;
        }

        ballVelocity = new Vector2(dirX, dirY).normalized * ballSpeed;
    }

    private void AddPointToPlayer(int playerNum)
    {
        if (playerNum == 1) scoreP1++;
        else if (playerNum == 2) scoreP2++;

        SendBroadcastMessage($"SCORE|{scoreP1}|{scoreP2}");

        if (scoreP1 >= maxScore)
        {
            TriggerGameOver(1);
        }
        else if (scoreP2 >= maxScore)
        {
            TriggerGameOver(2);
        }
        else
        {
            LaunchBall();
        }
    }

    private void TriggerGameOver(int winner)
    {
        isGameOver = true;
        ballVelocity = Vector2.zero;
        if (ballTransform != null) ballTransform.position = Vector3.zero;

        SendBroadcastMessage($"GAME_OVER|{winner}");
        Debug.Log($"[SERVIDOR] Fim de jogo! Jogador {winner} venceu.");
    }

    private void ResetGameToWaitingState()
    {
        gameStarted = false;
        isGameOver = false;
        ballVelocity = Vector2.zero;
        if (ballTransform != null) ballTransform.position = Vector3.zero;
    }

    private void LaunchBall()
    {
        if (ballTransform != null) ballTransform.position = Vector3.zero;

        float dirX = Random.value > 0.5f ? 1f : -1f;
        float dirY = Random.Range(-0.5f, 0.5f);

        if (Mathf.Abs(dirY) < 0.2f) dirY = 0.4f;

        ballVelocity = new Vector2(dirX, dirY).normalized * ballSpeed;
    }

    private void RestartGame()
    {
        scoreP1 = 0;
        scoreP2 = 0;
        isGameOver = false;

        SendBroadcastMessage($"SCORE|{scoreP1}|{scoreP2}");
        SendBroadcastMessage("GAME_RESET");

        if (p1EndPoint != null && p2EndPoint != null)
        {
            gameStarted = true;
            LaunchBall();
        }
        else
        {
            ResetGameToWaitingState();
        }

        Debug.Log("[SERVIDOR] O jogo foi reiniciado!");
    }

    private void OnDataReceived(System.IAsyncResult result)
    {
        try
        {
            IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = server.EndReceive(result, ref clientEP);
            string message = Encoding.UTF8.GetString(data);

            if (message.StartsWith("CONNECT"))
            {
                if (p1EndPoint == null)
                {
                    p1EndPoint = clientEP;
                    SendDataToClient(p1EndPoint, "ASSIGN:1");
                    Debug.Log("[SERVIDOR] Jogador 1 conectado: " + clientEP);
                }
                else if (p2EndPoint == null && !clientEP.Equals(p1EndPoint))
                {
                    p2EndPoint = clientEP;
                    SendDataToClient(p2EndPoint, "ASSIGN:2");
                    Debug.Log("[SERVIDOR] Jogador 2 conectado: " + clientEP);
                }

                // SÓ INICIA SE OS DOIS ESTIVEREM DEFINIDOS
                if (p1EndPoint != null && p2EndPoint != null && !gameStarted)
                {
                    gameStarted = true;
                    LaunchBall();
                    Debug.Log("[SERVIDOR] Ambos conectados! Partida iniciada.");
                }
            }
            else if (message.StartsWith("MOVE:"))
            {
                if (isGameOver || !gameStarted) return;

                float moveAmount = float.Parse(message.Split(':')[1]);

                if (clientEP.Equals(p1EndPoint) && p1Paddle != null)
                {
                    float newY = Mathf.Clamp(p1Paddle.position.y + moveAmount * paddleSpeed * Time.deltaTime, -3.8f, 3.8f);
                    p1Paddle.position = new Vector3(p1Paddle.position.x, newY, 0);
                }
                else if (clientEP.Equals(p2EndPoint) && p2Paddle != null)
                {
                    float newY = Mathf.Clamp(p2Paddle.position.y + moveAmount * paddleSpeed * Time.deltaTime, -3.8f, 3.8f);
                    p2Paddle.position = new Vector3(p2Paddle.position.x, newY, 0);
                }
            }
            else if (message.StartsWith("RESTART"))
            {
                RestartGame();
            }

            server.BeginReceive(OnDataReceived, null);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SERVIDOR] Erro na recepção: " + e.Message);
        }
    }

    private void BroadcastState()
    {
        if (p1Paddle == null || p2Paddle == null || ballTransform == null) return;

        string state = $"STATE|{p1Paddle.position.y}|{p2Paddle.position.y}|{ballTransform.position.x}|{ballTransform.position.y}";
        SendBroadcastMessage(state);
    }

    private void SendBroadcastMessage(string msg)
    {
        if (p1EndPoint != null) SendDataToClient(p1EndPoint, msg);
        if (p2EndPoint != null) SendDataToClient(p2EndPoint, msg);
    }

    private void SendDataToClient(IPEndPoint target, string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        server.Send(data, data.Length, target);
    }

    private void OnApplicationQuit()
    {
        server?.Close();
    }
}