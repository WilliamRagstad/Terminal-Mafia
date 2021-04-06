using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace TMServer.Game
{
    class Player
    {
        public Client Client;
        public string Name;
        public Role Role;

        public bool ToBeKilled;
        public bool Dead;

        public Player(Client client, Role role)
        {
            Client = client;
            Name = client.PlayerName;
            Role = role;

            ToBeKilled = false;
            Dead = false;
        }
    }
}
