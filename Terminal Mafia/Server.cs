using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Terminal_Mafia
{
    class Server
    {
        public TcpClient Socket;
        public string ServerName;

        public Server(TcpClient socket, string serverName)
        {
            Socket = socket;
            ServerName = serverName;
        }
    }
}
