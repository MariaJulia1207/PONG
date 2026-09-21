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
    public int maxScore = 5; // Limite de pontos para a vitória

    private UdpClient server;
    private IPEndPoint p1EndPoint;
    private IPEndPoint p2EndPoint;

    private int scoreP1 = 0;
    private int scoreP2 = 0;
    private bool isGameOver = false;

    private Vector2 ballVelocity;

    void Start()
    {
        server = new UdpClient(port);
        server.BeginReceive(OnDataReceived, null);
        Debug.Log("[SERVIDOR] Servidor iniciado na porta " + port);

        ResetBall();
    }

    void Update()
    {
        // Se o jogo acabou, não atualiza movimentos nem física
        if (isGameOver) return;

        // Movimentação simples da bola no Servidor
        ballTransform.Translate(ballVelocity * Time.deltaTime);

        // Verificação de limites das paredes para a bola rebate (Eixo Y)
        if (Mathf.Abs(ballTransform.position.y) > 4.5f)
        {
            ballVelocity.y = -ballVelocity.y;
        }

        // Verificação de Ponto (Eixo X)
        if (ballTransform.position.x > 9f)
        {
            AddPointToPlayer(1); // P1 Pontua
        }
        else if (ballTransform.position.x < -9f)
        {
            AddPointToPlayer(2); // P2 Pontua
        }

        // Envia o estado atual do jogo para todos os clientes conectados
        BroadcastState();
    }

    private void AddPointToPlayer(int playerNum)
    {
        if (playerNum == 1) scoreP1++;
        else if (playerNum == 2) scoreP2++;

        // Notifica placar atual
        BroadcastMessage($"SCORE|{scoreP1}|{scoreP2}");

        // Checa condição de vitória
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
            ResetBall();
        }
    }

    private void TriggerGameOver(int winner)
    {
        isGameOver = true;
        ballVelocity = Vector2.zero; // Parar a bola
        
        // Avisa a todos quem ganhou (GAME_OVER|1 ou GAME_OVER|2)
        BroadcastMessage($"GAME_OVER|{winner}");
        Debug.Log($"[SERVIDOR] Fim de jogo! Jogador {winner} venceu.");
    }

    private void ResetBall()
    {
        ballTransform.position = Vector3.zero;
        
        // Direção aleatória no início
        float dirX = Random.value > 0.5f ? 1f : -1f;
        float dirY = Random.Range(-0.5f, 0.5f);
        ballVelocity = new Vector2(dirX, dirY).normalized * ballSpeed;
    }

    private void RestartGame()
    {
        scoreP1 = 0;
        scoreP2 = 0;
        isGameOver = false;

        // Notifica placar zerado e reinício
        BroadcastMessage($"SCORE|{scoreP1}|{scoreP2}");
        BroadcastMessage("GAME_RESET");

        ResetBall();
        Debug.Log("[SERVIDOR] O jogo foi reiniciado!");
    }

    private void OnDataReceived(System.IAsyncResult result)
    {
        try
        {
            IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = server.EndReceive(result, ref clientEP);
            string message = Encoding.UTF8.GetString(data);

            // Registro de novos jogadores
            if (message.StartsWith("CONNECT"))
            {
                if (p1EndPoint == null)
                {
                    p1EndPoint = clientEP;
                    SendDataToClient(p1EndPoint, "ASSIGN:1");
                }
                else if (p2EndPoint == null && !clientEP.Equals(p1EndPoint))
                {
                    p2EndPoint = clientEP;
                    SendDataToClient(p2EndPoint, "ASSIGN:2");
                }
            }
            // Movimentação das raquetes
            else if (message.StartsWith("MOVE:"))
            {
                if (isGameOver) return; // Não move raquetes se o jogo acabou

                float moveAmount = float.Parse(message.Split(':')[1]);
                
                if (clientEP.Equals(p1EndPoint) && p1Paddle != null)
                {
                    p1Paddle.Translate(Vector3.up * moveAmount * paddleSpeed * Time.deltaTime);
                }
                else if (clientEP.Equals(p2EndPoint) && p2Paddle != null)
                {
                    p2Paddle.Translate(Vector3.up * moveAmount * paddleSpeed * Time.deltaTime);
                }
            }
            // Pedido de Reinício do Jogo
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
        BroadcastMessage(state);
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