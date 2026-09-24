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

    // Controle de taxa de envio de movimento (Evita FLOOD no servidor)
    private float moveSendRate = 0.05f; // 20 envios por segundo
    private float nextMoveSendTime = 0f;

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

            const int SIO_UDP_CONNRESET = -1744830452;
            try
            {
                udpClient.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }
            catch { }

            serverEP = new IPEndPoint(parsedAddress, serverPort);

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
        // Desempilha e executa ações na Main Thread
        lock (mainThreadQueue)
        {
            while (mainThreadQueue.Count > 0)
            {
                mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        if (!isConnected || playerRole == 0) return;

        // Captura de input contínuo
        float moveInput = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            moveInput = 1f;
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            moveInput = -1f;
        }

        // Envia comando de movimento com controle de frequência (evita sobrecarregar o socket)
        if (moveInput != 0f && Time.time >= nextMoveSendTime)
        {
            nextMoveSendTime = Time.time + moveSendRate;
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

            udpClient.BeginReceive(OnDataReceived, null);
        }
        catch (ObjectDisposedException) { }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENTE] Erro na recepção UDP: " + e.Message);
        }
    }

    private void ParseState(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 5) return;

        // Troca vírgula por ponto por garantia contra variações do Windows
        string s1 = parts[1].Replace(',', '.');
        string s2 = parts[2].Replace(',', '.');
        string s3 = parts[3].Replace(',', '.');
        string s4 = parts[4].Replace(',', '.');

        if (float.TryParse(s1, NumberStyles.Any, CultureInfo.InvariantCulture, out float p1Y) &&
            float.TryParse(s2, NumberStyles.Any, CultureInfo.InvariantCulture, out float p2Y) &&
            float.TryParse(s3, NumberStyles.Any, CultureInfo.InvariantCulture, out float ballX) &&
            float.TryParse(s4, NumberStyles.Any, CultureInfo.InvariantCulture, out float ballY))
        {
            EnqueueMainThread(() =>
            {
                if (p1Paddle != null) p1Paddle.position = new Vector3(p1Paddle.position.x, p1Y, p1Paddle.position.z);
                if (p2Paddle != null) p2Paddle.position = new Vector3(p2Paddle.position.x, p2Y, p2Paddle.position.z);
                if (ballTransform != null) ballTransform.position = new Vector3(ballX, ballY, ballTransform.position.z);
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
        playerRole = 0;
    }
}