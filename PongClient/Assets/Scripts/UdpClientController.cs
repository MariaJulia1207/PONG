using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UdpClientController : MonoBehaviour
{
    [Header("Configurações de Rede")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;

    [Header("Objetos do Jogo na Cena")]
    public Transform p1Paddle;
    public Transform p2Paddle;
    public Transform ballTransform;

    [Header("Interface de Usuário (Aceita Qualquer Objeto de Texto)")]
    public GameObject scoreTextP1Object;
    public GameObject scoreTextP2Object;
    public GameObject gameOverPanel;
    public GameObject winnerTextObject;

    private UdpClient client;
    private IPEndPoint serverEndPoint;
    private int myPlayerID = 0;

    private Vector3 targetP1Pos;
    private Vector3 targetP2Pos;
    private Vector3 targetBallPos;
    private string pendingScoreP1 = "0";
    private string pendingScoreP2 = "0";
    private string pendingWinnerText = "";
    private bool showGameOver = false;
    private bool hideGameOver = false;

    void Start()
    {
        if (gameOverPanel != null) 
            gameOverPanel.SetActive(false);

        client = new UdpClient();
        serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

        client.BeginReceive(OnDataReceived, null);
        SendDataToServer("CONNECT");
    }

    void Update()
    {
        float moveInput = Input.GetAxisRaw("Vertical");
        if (moveInput != 0 && myPlayerID != 0)
        {
            SendDataToServer($"MOVE:{moveInput}");
        }

        if (p1Paddle != null) 
            p1Paddle.position = Vector3.Lerp(p1Paddle.position, targetP1Pos, Time.deltaTime * 15f);
        
        if (p2Paddle != null) 
            p2Paddle.position = Vector3.Lerp(p2Paddle.position, targetP2Pos, Time.deltaTime * 15f);
        
        if (ballTransform != null) 
            ballTransform.position = Vector3.Lerp(ballTransform.position, targetBallPos, Time.deltaTime * 15f);

        // Atualiza os textos aceitando tanto TextMeshPro quanto Legacy Text
        SetTextValue(scoreTextP1Object, pendingScoreP1);
        SetTextValue(scoreTextP2Object, pendingScoreP2);

        if (showGameOver)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            SetTextValue(winnerTextObject, pendingWinnerText);
            showGameOver = false;
        }

        if (hideGameOver)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            hideGameOver = false;
        }
    }

    // Função auxiliar que define o texto independente do tipo de componente usado
    private void SetTextValue(GameObject textObj, string value)
    {
        if (textObj == null) return;

        // Tenta atualizar como TextMeshPro
        TMP_Text tmp = textObj.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = value;
            return;
        }

        // Tenta atualizar como Text Antigo (Legacy)
        Text legacyText = textObj.GetComponent<Text>();
        if (legacyText != null)
        {
            legacyText.text = value;
        }
    }

    private void OnDataReceived(System.IAsyncResult result)
    {
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = client.EndReceive(result, ref remoteEP);
            string message = Encoding.UTF8.GetString(data);

            if (message.StartsWith("ASSIGN:"))
            {
                myPlayerID = int.Parse(message.Split(':')[1]);
                Debug.Log($"[CLIENTE] Atribuído como Jogador {myPlayerID}");
            }
            else if (message.StartsWith("STATE|"))
            {
                string[] parts = message.Split('|');
                float p1Y = float.Parse(parts[1]);
                float p2Y = float.Parse(parts[2]);
                float ballX = float.Parse(parts[3]);
                float ballY = float.Parse(parts[4]);

                targetP1Pos = new Vector3(p1Paddle != null ? p1Paddle.position.x : -8f, p1Y, 0);
                targetP2Pos = new Vector3(p2Paddle != null ? p2Paddle.position.x : 8f, p2Y, 0);
                targetBallPos = new Vector3(ballX, ballY, 0);
            }
            else if (message.StartsWith("SCORE|"))
            {
                string[] parts = message.Split('|');
                pendingScoreP1 = parts[1];
                pendingScoreP2 = parts[2];
            }
            else if (message.StartsWith("GAME_OVER|"))
            {
                int winner = int.Parse(message.Split('|')[1]);
                pendingWinnerText = $"Jogador {winner} Venceu!";
                showGameOver = true;
            }
            else if (message.Equals("GAME_RESET"))
            {
                hideGameOver = true;
            }

            client.BeginReceive(OnDataReceived, null);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[CLIENTE] Erro no recebimento de dados: " + e.Message);
        }
    }

    public void RequestRestart()
    {
        SendDataToServer("RESTART");
    }

    private void SendDataToServer(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length, serverEndPoint);
    }

    private void OnApplicationQuit()
    {
        client?.Close();
    }
}