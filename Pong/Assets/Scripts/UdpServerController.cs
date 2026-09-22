using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UdpServerController : MonoBehaviour
{
    [Header("Configurações do Servidor")]
    public int listenPort = 9050;

    private UdpClient udpServer;
    private List<IPEndPoint> connectedClients = new List<IPEndPoint>();

    private int scoreP1 = 0;
    private int scoreP2 = 0;

    private void Start()
    {
        udpServer = new UdpClient(listenPort);
        udpServer.BeginReceive(OnDataReceived, null);
        Debug.Log("[SERVIDOR] Servidor UDP iniciado na porta " + listenPort);
    }

    private void OnDataReceived(System.IAsyncResult result)
    {
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udpServer.EndReceive(result, ref remoteEP);
            string message = Encoding.UTF8.GetString(data);

            if (!connectedClients.Contains(remoteEP))
            {
                connectedClients.Add(remoteEP);
                int assignedID = connectedClients.Count;
                SendToClient($"ASSIGN:{assignedID}", remoteEP);
            }

            udpServer.BeginReceive(OnDataReceived, null);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SERVIDOR] Erro ao receber dados: " + e.Message);
        }
    }

    // MÉTODO PÚBLICO: Permite que o GoalArea.cs chame esta função[cite: 3]
    public void AddPointToPlayer(int playerNum)
    {
        if (playerNum == 1)
        {
            scoreP1++;
        }
        else if (playerNum == 2)
        {
            scoreP2++;
        }

        Debug.Log($"[SERVIDOR] Golo! Placar atual: P1 {scoreP1} x {scoreP2} P2");

        // Transmite o placar atualizado para todos os clientes
        SendBroadcastMessage($"SCORE|{scoreP1}|{scoreP2}");
    }

    public void SendBroadcastMessage(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        foreach (var clientEP in connectedClients)
        {
            udpServer.Send(data, data.Length, clientEP);
        }
    }

    private void SendToClient(string msg, IPEndPoint clientEP)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        udpServer.Send(data, data.Length, clientEP);
    }

    private void OnApplicationQuit()
    {
        udpServer?.Close();
    }
}