using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using TMPro;

public class UdpClientController : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;

    [Header("Campos de Entrada (UI)")]
    public TMP_InputField ipInputField;
    public GameObject menuPanel;

    [Header("Objetos Visuais da Cena")]
    public Transform p1Paddle;
    public Transform p2Paddle;
    public Transform ballTransform;

    private UdpClient client;
    private IPEndPoint serverEndPoint;

    private int myPlayerID = 0; // 0 = Não conectado, 1 = P1, 2 = P2
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

    // Posições alvo recebidas da rede para movimentação suave
    private float targetP1Y;
    private float targetP2Y;
    private Vector3 targetBallPos;

    public void ConnectToServer()
    {
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
        {
            serverIP = ipInputField.text;
        }

        client = new UdpClient();
        serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

        client.BeginReceive(OnDataReceived, null);

        // Manda mensagem de registro
        SendData("CONNECT");
        Debug.Log("[CLIENTE] Conectando ao servidor: " + serverIP);

        if (menuPanel != null) menuPanel.SetActive(false);
    }

    private void Update()
    {
        // Executa ações de rede na thread principal
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue()?.Invoke();
            }
        }

        if (myPlayerID == 0) return;

        // Capta o input do teclado do jogador local
        float input = Input.GetAxisRaw("Vertical");
        if (input != 0)
        {
            SendData($"MOVE:{input.ToString(CultureInfo.InvariantCulture)}");
        }

        // Aplica as posições sincronizadas pelo servidor na tela do cliente
        p1Paddle.position = new Vector3(p1Paddle.position.x, targetP1Y, 0);
        p2Paddle.position = new Vector3(p2Paddle.position.x, targetP2Y, 0);
        ballTransform.position = targetBallPos;
    }

    private void OnDataReceived(IAsyncResult result)
    {
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = client.EndReceive(result, ref remoteEP);
            string message = Encoding.UTF8.GetString(data);

            if (message.StartsWith("ASSIGN"))
            {
                int assignedId = int.Parse(message.Split(':')[1]);
                lock (mainThreadActions)
                {
                    mainThreadActions.Enqueue(() =>
                    {
                        myPlayerID = assignedId;
                        Debug.Log("[CLIENTE] Conectado e atribuído como Jogador " + myPlayerID);
                    });
                }
            }
            else if (message.StartsWith("STATE"))
            {
                ParseGameState(message);
            }

            client.BeginReceive(OnDataReceived, null);
        }
        catch (ObjectDisposedException) { }
    }

    private void ParseGameState(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 5) return;

        if (float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float p1Y) &&
            float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out float p2Y) &&
            float.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out float bx) &&
            float.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out float by))
        {
            // Atualiza os alvos diretamente para consumo no Update()
            targetP1Y = p1Y;
            targetP2Y = p2Y;
            targetBallPos = new Vector3(bx, by, 0);
        }
    }

    private void SendData(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        client.Send(data, data.Length, serverEndPoint);
    }

    private void OnApplicationQuit()
    {
        client?.Close();
    }
}