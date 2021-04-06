#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using TMProtocol;
using TMProtocol.Shared;
using Console = EzConsole.EzConsole;

namespace Terminal_Mafia
{
    class Program
    {
        private static readonly string TitleStripes = @"=======================================================================================================================";
        private static readonly string Title = @"         ___=======___________________________,   _______  _______  ______    __   __  ___   __    _  _______  ___     
     ___/  ////////  \____/                   |  |       ||       ||    _ |  |  |_|  ||   | |  |  | ||   _   ||   |
    /__ |_//[//]//             -------------- |  |_     _||    ___||   | ||  |       ||   | |   |_| ||  |_|  ||   |
    \   |////////_____________________________|    |   |  |   |___ |   |_||_ |       ||   | |       ||       ||   |
     \                 *****        |________|     |   |  |    ___||    __  ||       ||   | |  _    ||       ||   |___
      |       ______________________|              |   |  |   |___ |   |  | || ||_|| ||   | | | |   ||   _   ||       |
      /           /  ||   //                       |___|  |_______||___|  |_||_|   |_||___| |_|  |__||__| |__||_______|
     /           /____\__//     __   __  _______  _______  ___   _______ 
    /           /~~~~~~~~~     |  |_|  ||   _   ||       ||   | |   _   |
   /           /               |       ||  |_|  ||    ___||   | |  |_|  |
  /           /                |       ||       ||   |___ |   | |       |
 /           /                 |       ||       ||    ___||   | |       |
 \----------/                  | ||_|| ||   _   ||   |    |   | |   _   |  _
                               |_|   |_||__| |__||___|    |___| |__| |__| |_|";
        static void Main(string[]? args)
        {
            Console.WriteLine(TitleStripes, ConsoleColor.DarkYellow);
            Console.WriteLine(Title, ConsoleColor.Yellow);
            Console.WriteLine(TitleStripes, ConsoleColor.DarkYellow);
            Console.WriteLine();
            
            LogBlock("Welcome to Terminal Mafia,\n");
            LogBlock("A game about finding out who are lying or to kill all others before they do.\n\n");

            string name = Console.ReadInput("Name");
            Console.WriteLine("Enter a server IP address/hostname to join a game!");
            JoinServer(name);
        }

        private static void JoinServer(string playerName)
        {
            Server server;
            #region Connection loop
            while (true)
            {
                string serverAddress = Console.ReadInput("Server");
                TcpClient socket;
                try
                {
                    socket = new TcpClient(serverAddress, TMProtocol.Codec.StandardPort);
                }
                catch
                {
                    Console.WriteLine("Server does not exist.", ConsoleColor.Red);
                    continue;
                }
                Console.WriteLine("Connecting...", ConsoleColor.Yellow);

                TMProtocol.Server.CConnect connect;
                try
                {
                    connect = Codec.Exchange<TMProtocol.Server.CConnect>(socket,
                        new TMProtocol.Client.Connect(playerName), RequestType.Connect, RequestType.Connect);
                }
                catch (IOException)
                {
                    Console.WriteLine("Server did not respond, maybe it is busy playing a game already?", ConsoleColor.Red);
                    continue;
                }
                catch (InvalidDataException e)
                {
                    Console.WriteLine("Server did not understand request: " + e.Message, ConsoleColor.Red);
                    continue;
                }

                if (connect.Accepted)
                {
                    Console.Clear();
                    Console.Write(connect.ServerName + ": ");
                    Console.WriteLine(connect.Message, ConsoleColor.Green);
                    server = new Server(socket, connect.ServerName);
                    break; // Successfully connected to server
                }
                // Else, connection was not accepted.
                Console.WriteLine(connect.Message, ConsoleColor.Red);
            }
            #endregion

            new GameClient(playerName, server).Start();
        }

        public static void HandleGameStop()
        {
            Console.WriteLine("Goodbye!");
            Thread.Sleep(1000);
        }
        public static void HandleServerClosed(GameClient game)
        {
            Main(null);
        }

        private static void LogBlock(string message) => LogBlock(message, ConsoleColor.Gray);
        private static void LogBlock(string message, ConsoleColor color)
        {
            Console.Write("\t|| ", ConsoleColor.Yellow);
            Console.Write(message, color);
        }
    }
}
