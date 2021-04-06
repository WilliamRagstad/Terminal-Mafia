using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using TMProtocol.Client;

namespace TMServer
{
    public class Client
    {
        public TcpClient Socket;
        public string PlayerName;

        public Client(TcpClient socket, string playerName)
        {
            Socket = socket;
            PlayerName = playerName;
        }
    }
}
