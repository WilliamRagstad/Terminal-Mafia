using System;
using System.Collections.Generic;
using System.Text;
using TMProtocol.Shared;

namespace TMProtocol.Server
{
    public class GameStart : IPacket
    {
        public string[] Players;
        public string[] Rules;
        public GameStart(string[] players, string[] rules)
        {
            Players = players;
            Rules = rules;
        }
    }
}
