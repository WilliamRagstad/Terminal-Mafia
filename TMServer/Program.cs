using System;
using System.Net;
using TMProtocol;
using TMProtocol.Shared;
using Console = EzConsole.EzConsole;

namespace TMServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Terminal Mafia ", ConsoleColor.Yellow);
            Console.WriteLine("| Server\n", ConsoleColor.DarkGray);
            
            LogBlock("Settings\n", ConsoleColor.Cyan);
            LogBlock("Setup and customize the game experience to your liking.\n\n");

            string name = Console.ReadInput("Name");
            int players = GetPlayers();
            
            Console.WriteLine();
            LogBlock("Rules\n", ConsoleColor.Cyan);
            LogBlock($"Important: The total number of players assigned to each role must not exceed {players}.\n\n");

            Rules rules = GetRules(players);

            string ip = GetIpAddress();
            Console.WriteLine();
            LogBlock("Hosting started!\n", ConsoleColor.Green);
            LogBlock("Players can connect using "); Console.Write(ip, ConsoleColor.Cyan); Console.WriteLine("!");
            LogBlock("Make sure that the port "); Console.Write(Codec.StandardPort.ToString(), ConsoleColor.Cyan); Console.Write(" is forwarded through you local router to the network.\n\n");

            Server server = new Server(name, players, rules);
            server.Start();
        }

        private static int GetPlayers()
        {
            // TODO: Change back 0 to 3.
            int players;
            while ((players = Console.ReadInput<int>("Players", 4)) <= 0) Console.WriteLine("Must be four or more players", ConsoleColor.Red);
            return players;
        }

        private static Rules GetRules(int players)
        {
            int mafia = Console.ReadInput<int>("Mafia", players * 1/4);
            int police = Console.ReadInput<int>("Policemen", 0);
            int doctor = Console.ReadInput<int>("Doctors", 0);
            int detectives = Console.ReadInput<int>("Detectives", 0);
            int villagers = Console.ReadInput<int>("Villagers", players * 3/4);

            int total = mafia + police + doctor + detectives + villagers;
            if (total < players) Console.WriteLine("Too few roles assigned, remember each player must have one role!");
            else if (total > players) Console.WriteLine("Too many roles assigned, not enough players!");
            else return new Rules {
                Mafia = mafia,
                Police = police,
                Doctors = doctor,
                Detectives = detectives,
                Villagers = villagers
            };

            return GetRules(players); // Try to get the rules one more time...
        }

        private static string GetIpAddress()  
        {
            return new WebClient().DownloadString("http://icanhazip.com").Trim();
        }

        private static void LogBlock(string message) => LogBlock(message, ConsoleColor.Gray);
        private static void LogBlock(string message, ConsoleColor color)
        {
            Console.Write("\t|| ", ConsoleColor.Yellow);
            Console.Write(message, color);
        }
    }
}
