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

    [Header("Objetos do Jogo na Cena (4 Jogadores + Bola)")]
    public Transform p1Paddle; // Eq 1 - J1 (Esquerda - Vertical)
    public Transform p2Paddle; // Eq 1 - J2 (Topo - Horizontal)
    public Transform p3Paddle; // Eq 2 - J3 (Direita - Vertical)
    public Transform p4Paddle; // Eq 2 - J4 (Baixo - Horizontal)
    public Transform ballTransform;

    [Header("Interface do Placar e Fim de Jogo")]
    public TextMeshProUGUI scoreTextP1;
    public TextMeshProUGUI scoreTextP2;
    public GameObject gameOverPanel;
    public TextMeshProUGUI winnerText;
    public Button restartButton;

    private UdpClient udpClient;
    private IPEndPoint serverEP;
    private int playerRole = 0; // 1 = J1, 2 = J2, 3 = J3, 4 = J4
    private bool isConnected = false;

    private float moveSendRate = 0.05f; // Max 20 pacotes/seg de input
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
            nextMoveSendTime = 0f; // Reseta o timer de envio
            Debug.Log($"[CLIENTE] Solicitando conexão ao servidor em {ipAddressStr}:{serverPort}...");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CLIENTE] Erro ao conectar: {e.Message}");
        }
    }

    void Update()
    {
        lock (mainThreadQueue)
        {
            while (mainThreadQueue.Count > 0)
            {
                mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        if (!isConnected || playerRole == 0) return;

        float moveInput = 0f;

        // Leitura de entrada baseada no papel do jogador
        if (playerRole == 1 || playerRole == 3)
        {
            // Jogadores Verticais (J1 e J3)
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                moveInput = 1f;
            }
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                moveInput = -1f;
            }
        }
        else if (playerRole == 2 || playerRole == 4)
        {
            // Jogadores Horizontais (J2 e J4)
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                moveInput = 1f;
            }
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                moveInput = -1f;
            }
        }

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

                        string playerInfo = playerRole switch
                        {
                            1 => "Jogador 1 (Verde - Esquerda)",
                            2 => "Jogador 2 (Amarelo - Topo)",
                            3 => "Jogador 3 (Vermelho - Direita)",
                            4 => "Jogador 4 (Azul - Baixo)",
                            _ => $"Jogador {playerRole}"
                        };

                        Debug.Log($"[CLIENTE] Atribuído como {playerInfo}");

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
                if (int.TryParse(message.Split('|')[1], out int winningTeam))
                {
                    EnqueueMainThread(() => ShowGameOver(winningTeam));
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
        if (parts.Length < 7) return; // Espera: STATE | p1Y | p2X | p3Y | p4X | ballX | ballY

        string s1 = parts[1].Replace(',', '.'); // J1 - Y
        string s2 = parts[2].Replace(',', '.'); // J2 - X
        string s3 = parts[3].Replace(',', '.'); // J3 - Y
        string s4 = parts[4].Replace(',', '.'); // J4 - X
        string s5 = parts[5].Replace(',', '.'); // Ball X
        string s6 = parts[6].Replace(',', '.'); // Ball Y

        if (float.TryParse(s1, NumberStyles.Any, CultureInfo.InvariantCulture, out float p1Y) &&
            float.TryParse(s2, NumberStyles.Any, CultureInfo.InvariantCulture, out float p2X) &&
            float.TryParse(s3, NumberStyles.Any, CultureInfo.InvariantCulture, out float p3Y) &&
            float.TryParse(s4, NumberStyles.Any, CultureInfo.InvariantCulture, out float p4X) &&
            float.TryParse(s5, NumberStyles.Any, CultureInfo.InvariantCulture, out float ballX) &&
            float.TryParse(s6, NumberStyles.Any, CultureInfo.InvariantCulture, out float ballY))
        {
            EnqueueMainThread(() =>
            {
                if (p1Paddle != null) p1Paddle.position = new Vector3(p1Paddle.position.x, p1Y, p1Paddle.position.z);
                if (p2Paddle != null) p2Paddle.position = new Vector3(p2X, p2Paddle.position.y, p2Paddle.position.z);
                if (p3Paddle != null) p3Paddle.position = new Vector3(p3Paddle.position.x, p3Y, p3Paddle.position.z);
                if (p4Paddle != null) p4Paddle.position = new Vector3(p4X, p4Paddle.position.y, p4Paddle.position.z);
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

    private void ShowGameOver(int winningTeam)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (winnerText != null)
        {
            int myTeam = (playerRole == 1 || playerRole == 2) ? 1 : 2;
            winnerText.text = (winningTeam == myTeam) ? "SUA EQUIPE VENCEU!" : "SUA EQUIPE PERDEU!";
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