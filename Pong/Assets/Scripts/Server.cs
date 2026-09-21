using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;

public class UdpServerTwoClients : MonoBehaviour
{
    UdpClient server;
    IPEndPoint anyEP;
    Thread receiveThread;
    Dictionary<string, int> clientIds = new Dictionary<string, int>();
    int nextId = 1;

    void Start()
    {
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);
        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();
        Debug.Log("Servidor iniciado na porta 5001");
    }

    void ReceiveData()
    {
        while (true)
        {
            byte[] data = server.Receive(ref anyEP);
            string msg = Encoding.UTF8.GetString(data);
            string key = anyEP.Address + ":" + anyEP.Port;

            if (!clientIds.ContainsKey(key))
            {
                clientIds[key] = nextId++;
                string assignMsg = "ASSIGN:" + clientIds[key];
                server.Send(Encoding.UTF8.GetBytes(assignMsg), assignMsg.Length, anyEP);
            }

            int id = clientIds[key];
            if (msg.StartsWith("POS:"))
            {
                string coords = msg.Substring(4);
                string broadcast = $"POS:{id};{coords}";
                byte[] bdata = Encoding.UTF8.GetBytes(broadcast);

                foreach (var kvp in clientIds)
                {
                    var parts = kvp.Key.Split(':');
                    IPEndPoint ep = new IPEndPoint(
                        IPAddress.Parse(parts[0]),
                        int.Parse(parts[1])
                    );
                    server.Send(bdata, bdata.Length, ep);
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        receiveThread.Abort();
        server.Close();
    }

}