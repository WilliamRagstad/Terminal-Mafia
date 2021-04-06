using System;
using System.Collections.Generic;
using System.Text;

namespace TMProtocol.Server
{
    /// <summary>
    /// Server -> All Clients: A new player has connected - broadcast
    /// </summary>
    public class BConnect : IPacket
    {
        public string[] Players;
        public string NewPlayer;
        public string Message;

        public BConnect(string[] players, string newPlayer, string message)
        {
            Players = players;
            NewPlayer = newPlayer;
            Message = message;
        }
    }
} 
