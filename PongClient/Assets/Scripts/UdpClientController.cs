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
    public string defaultServerIP = "127.0.0.1";
    public int serverPort = 9050;

    [Header("Interface de Conexão (UI)")]
    public TMP_InputField ipInputField;
    public Button connectButton;
    public GameObject connectionPanel;

    [Header("Objetos do Jogo na Cena")]
    public Transform p1Paddle;
    public Transform p2Paddle;
    public Transform ballTransform;

    [Header("Interface do Placar e Fim de Jogo")]
    public TextMeshProUGUI scoreTextP1;
    public TextMeshProUGUI scoreTextP2;
    public GameObject gameOverPanel;
    public TextMeshProUGUI winnerText;
    public Button restartButton;

    private UdpClient udpClient;
    private IPEndPoint serverEP;
    private int playerRole = 0; // 1 = P1, 2 = P2
    private bool isConnected = false;

    // Fila para executar ações na Main Thread da Unity
    private static readonly Queue<Action> mainThreadQueue = new Queue<Action>();

    void Start()
    {
        Application.runInBackground = true;

        if (gameOverPanel != null) 
            gameOverPanel.SetActive(false);

        if (ipInputField != null && string.IsNullOrEmpty(ipInputField.text))
        {
            ipInputField.text = defaultServerIP;
        }

        if (connectButton != null)
        {
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(SendRestartRequest);
        }
    }

    public void OnConnectButtonClicked()
    {
        string targetIP = defaultServerIP;

        if (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
        {
            targetIP = ipInputField.text.Trim();
        }

        ConnectToServer(targetIP);
    }

    private void ConnectToServer(string ipAddressStr)
    {
        CloseSocket();

        if (!IPAddress.TryParse(ipAddressStr, out IPAddress parsedAddress))
        {
            Debug.LogError($"[CLIENTE] Endereço IP inválido: {ipAddressStr}");
            return;
        }

        try
        {
            udpClient = new UdpClient();

            // Previne falha de fechamento de conexão UDP silenciosa no Windows
            const int SIO_UDP_CONNRESET = -1744830452;
            try
            {
                udpClient.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }
            catch { }

            serverEP = new IPEndPoint(parsedAddress, serverPort);

            // Envia pacote de conexão para o Servidor
            byte[] data = Encoding.UTF8.GetBytes("CONNECT");
            udpClient.Send(data, data.Length, serverEP);

            udpClient.BeginReceive(OnDataReceived, null);
            isConnected = true;
            Debug.Log($"[CLIENTE] Solicitando conexão ao servidor em {ipAddressStr}:{serverPort}...");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CLIENTE] Erro ao conectar: {e.Message}");
        }
    }

    void Update()
    {
        // Descarrega as ações pendentes na Thread Principal da Unity
        lock (mainThreadQueue)
        {
            while (mainThreadQueue.Count > 0)
            {
                mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        if (!isConnected || playerRole == 0) return;

        // Captura entrada do jogador e envia para o servidor
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

            if (message.StartsWith("ASSIGN:"))
            {
                if (int.TryParse(message.Split(':')[1], out int assignedId))
                {
                    EnqueueMainThread(() =>
                    {
                        playerRole = assignedId;
                        Debug.Log($"[CLIENTE] Atribuído como Jogador {playerRole}");

                        if (connectionPanel != null)
                        {
                            connectionPanel.SetActive(false);
                        }
                    });
                }
            }
            else if (message.StartsWith("STATE|"))
            {
                ParseState(message);
            }
            else if (message.StartsWith("SCORE|"))
            {
                ParseScore(message);
            }
            else if (message.StartsWith("GAME_OVER|"))
            {
                if (int.TryParse(message.Split('|')[1], out int winner))
                {
                    EnqueueMainThread(() => ShowGameOver(winner));
                }
            }
            else if (message == "GAME_RESET")
            {
                EnqueueMainThread(HideGameOver);
            }

            // Mantém o escutador UDP ativo
            udpClient.BeginReceive(OnDataReceived, null);
        }
        catch (ObjectDisposedException)
        {
            // Socket foi fechado normalmente
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
            if (scoreTextP1 != null) 
            {
                scoreTextP1.text = s1;
            }

            if (scoreTextP2 != null) 
            {
                scoreTextP2.text = s2;
            }
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
        playerRole = 0;
    }
}