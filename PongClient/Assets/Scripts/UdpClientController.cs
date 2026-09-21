using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UdpClientController : MonoBehaviour
{
    [Header("Configurações de Rede")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;

    [Header("Objetos do Jogo na Cena")]
    public Transform p1Paddle;
    public Transform p2Paddle;
    public Transform ballTransform;

    [Header("Interface de Usuário (UI)")]
    public TMP_Text scoreTextP1;
    public TMP_Text scoreTextP2;
    public GameObject gameOverPanel;
    public TMP_Text winnerText;
    public Button restartButton;

    private UdpClient udpClient;
    private IPEndPoint serverEP;
    private int playerRole = 0; // 1 = P1, 2 = P2
    private bool isConnected = false;

    // Fila para executar ações com segurança na Main Thread da Unity
    private static readonly Queue<Action> mainThreadQueue = new Queue<Action>();

    void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(SendRestartRequest);
        }

        ConnectToServer();
    }

    public void ConnectToServer()
    {
        CloseSocket(); // Limpa conexões anteriores se existirem

        if (!IPAddress.TryParse(serverIP, out IPAddress parsedAddress))
        {
            Debug.LogError($"[CLIENTE] Endereço IP inválido: {serverIP}");
            return;
        }

        try
        {
            udpClient = new UdpClient();
            serverEP = new IPEndPoint(parsedAddress, serverPort);

            // Envia o pedido de conexão para o Servidor
            byte[] data = Encoding.UTF8.GetBytes("CONNECT");
            udpClient.Send(data, data.Length, serverEP);

            udpClient.BeginReceive(OnDataReceived, null);
            isConnected = true;
            Debug.Log($"[CLIENTE] Conectado ao servidor em {serverIP}:{serverPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CLIENTE] Erro ao conectar: {e.Message}");
        }
    }

    void Update()
    {
        // Processa ações acumuladas que precisam de rodar na Main Thread
        lock (mainThreadQueue)
        {
            while (mainThreadQueue.Count > 0)
            {
                mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        if (!isConnected || playerRole == 0) return;

        // Captura movimento do jogador (Setas Cima/Baixo ou W/S)
        float moveInput = Input.GetAxisRaw("Vertical");

        if (moveInput != 0)
        {
            string msg = $"MOVE:{moveInput.ToString(CultureInfo.InvariantCulture)}";
            byte[] data = Encoding.UTF8.GetBytes(msg);
            try
            {
                udpClient?.Send(data, data.Length, serverEP);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CLIENTE] Erro ao enviar pacote de movimento: " + ex.Message);
            }
        }
    }

    private void OnDataReceived(IAsyncResult result)
    {
        try
        {
            if (udpClient == null || udpClient.Client == null) return;

            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udpClient.EndReceive(result, ref ep);
            string message = Encoding.UTF8.GetString(data).Trim();

            // Atribuição de número do Jogador (ASSIGN:1 ou ASSIGN:2)
            if (message.StartsWith("ASSIGN:"))
            {
                if (int.TryParse(message.Split(':')[1], out int assignedId))
                {
                    EnqueueMainThread(() =>
                    {
                        playerRole = assignedId;
                        Debug.Log($"[CLIENTE] Atribuído como Jogador {playerRole}");
                    });
                }
            }
            // Estado de posições (STATE|p1Y|p2Y|ballX|ballY)
            else if (message.StartsWith("STATE|"))
            {
                ParseState(message);
            }
            // Placar do jogo (SCORE|p1Score|p2Score)
            else if (message.StartsWith("SCORE|"))
            {
                ParseScore(message);
            }
            // Fim de jogo (GAME_OVER|winner)
            else if (message.StartsWith("GAME_OVER|"))
            {
                if (int.TryParse(message.Split('|')[1], out int winner))
                {
                    EnqueueMainThread(() => ShowGameOver(winner));
                }
            }
            // Reinício de partida
            else if (message == "GAME_RESET")
            {
                EnqueueMainThread(HideGameOver);
            }

            // Continua a escutar a rede
            udpClient.BeginReceive(OnDataReceived, null);
        }
        catch (ObjectDisposedException)
        {
            // Exceção normal disparada ao fechar o jogo ou socket
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENTE] Erro na recepção UDP: " + e.Message);
        }
    }

    private void ParseState(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 5) return;

        if (float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float p1Y) &&
            float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out float p2Y) &&
            float.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out float ballX) &&
            float.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out float ballY))
        {
            EnqueueMainThread(() =>
            {
                if (p1Paddle != null) p1Paddle.position = new Vector3(p1Paddle.position.x, p1Y, 0);
                if (p2Paddle != null) p2Paddle.position = new Vector3(p2Paddle.position.x, p2Y, 0);
                if (ballTransform != null) ballTransform.position = new Vector3(ballX, ballY, 0);
            });
        }
    }

    private void ParseScore(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 3) return;

        string s1 = parts[1];
        string s2 = parts[2];

        EnqueueMainThread(() =>
        {
            if (scoreTextP1 != null) scoreTextP1.text = s1;
            if (scoreTextP2 != null) scoreTextP2.text = s2;
        });
    }

    private void ShowGameOver(int winner)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (winnerText != null)
        {
            winnerText.text = (winner == playerRole) ? "VOCÊ VENCEU!" : "VOCÊ PERDEU!";
        }
    }

    private void HideGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    public void SendRestartRequest()
    {
        if (udpClient == null || serverEP == null) return;
        try
        {
            byte[] data = Encoding.UTF8.GetBytes("RESTART");
            udpClient.Send(data, data.Length, serverEP);
        }
        catch (Exception e)
        {
            Debug.LogError("[CLIENTE] Erro ao enviar solicitação de reinício: " + e.Message);
        }
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
        CloseSocket();
    }

    private void OnApplicationQuit()
    {
        CloseSocket();
    }

    private void CloseSocket()
    {
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch { }
            udpClient = null;
        }
        isConnected = false;
    }
}