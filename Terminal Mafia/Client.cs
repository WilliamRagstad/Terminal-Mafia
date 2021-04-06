using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMProtocol;
using TMProtocol.Shared;
using Console = EzConsole.EzConsole;

namespace Terminal_Mafia
{
    class GameClient
    {
        private bool GameOver;
        private Thread GameThread;
        private readonly string _playerName;
        private readonly Server _server;
        private string[] _allPlayers;
        public GameClient(string playerName, Server server)
        {
            _playerName = playerName;
            _server = server;
            _allPlayers = new string[0];
        }

        public void Start()
        {
            GameOver = false;
            GameThread = new Thread(Run);
            GameThread.Start();
            GameThread.Join();
            Program.HandleGameStop();
        }

        private void Run()
        {
            // Wait for game to start
            WaitToStart();
            GameLoop();
        }

        #region Game
        private void WaitToStart()
        {
            RequestType request = CCodec.Receive(_server, out string data, HandleServerError);
            switch (request)
            {
                case RequestType.LobbyUpdate:
                    TMProtocol.Server.BConnect playerConnected = Codec.Convert<TMProtocol.Server.BConnect>(data);
                    _allPlayers = playerConnected.Players;
                    if (!string.Equals(playerConnected.NewPlayer, _playerName, StringComparison.CurrentCultureIgnoreCase)) LogServer(playerConnected.Message, ConsoleColor.Green);
                    WaitToStart();
                    break;
                case RequestType.GameStart:
                    TMProtocol.Server.GameStart start = Codec.Convert<TMProtocol.Server.GameStart>(data);
                    _allPlayers = start.Players;
                    LogServer("Starting game...", ConsoleColor.Green);
                    // Print rules
                    Console.WriteLine();
                    LogBlock("Game Rules\n", ConsoleColor.Cyan);
                    foreach (string startRule in start.Rules) LogBlock(startRule + '\n', ConsoleColor.DarkCyan);
                    Console.WriteLine();
                    break;
                default:
                    Log(request + " was unexpected at this time?", ConsoleColor.Yellow);
                    WaitToStart();
                    break;
            }
        }

        private void GameLoop()
        {
            WaitForNewRole();
            WaitForStory(); // Prologue + Other players wake up + You will wake up soon
            WaitForAction();
            WaitForStory(); // Other players wake up

            // GameLoop(); // Restart game loop
        }

        private void WaitForNewRole()
        {
            if (GameOver) return;
            RequestType request = CCodec.Receive(_server, out string data, HandleServerError);
            switch (request)
            {
                case RequestType.AssignedRole:
                    TMProtocol.Shared.Message message = Codec.Convert<TMProtocol.Shared.Message>(data);
                    LogStory(message.Contents, ConsoleColor.Yellow);
                    break;
                default:
                    Unexpected(request, WaitForNewRole);
                    break;
            }
        }
        private void WaitForStory()
        {
            if (GameOver) return;
            RequestType request = CCodec.Receive(_server, out string data, HandleServerError);
            switch (request)
            {
                case RequestType.Story:
                    TMProtocol.Shared.Message story = Codec.Convert<TMProtocol.Shared.Message>(data);
                    LogStory(story.Contents);
                    break;
                default:
                    Unexpected(request, WaitForStory);
                    break;
            }
        }
        private void WaitForAction() => WaitForAction(true);
        private void WaitForAction(bool first)
        {
            if (GameOver) return;
            RequestType request = CCodec.Receive(_server, out string data, HandleServerError);
            switch (request)
            {
                case RequestType.Story:
                    TMProtocol.Shared.Message story = Codec.Convert<TMProtocol.Shared.Message>(data);
                    LogStory(story.Contents);
                    WaitForAction();
                    break;
                case RequestType.Action:
                    TMProtocol.Shared.Message action = Codec.Convert<TMProtocol.Shared.Message>(data);
                    if (first) LogStory(action.Contents);
                    string command = Console.ReadInput("You").Trim();
                    CCodec.Send(_server, new TMProtocol.Client.Action(command), RequestType.Action, HandleServerError);
                    RequestType commandAnswer = CCodec.Receive(_server, out data, HandleServerError);
                    switch (commandAnswer)
                    {
                        case RequestType.ActionSuccess:
                            TMProtocol.Shared.Message successMessage = Codec.Convert<TMProtocol.Shared.Message>(data);
                            LogStory(successMessage.Contents);
                            break;
                        case RequestType.ActionInfo:
                            TMProtocol.Shared.Message infoMessage = Codec.Convert<TMProtocol.Shared.Message>(data);
                            LogStory(infoMessage.Contents);
                            WaitForAction(false); // Wait for another try
                            break;
                        case RequestType.ActionError:
                            TMProtocol.Shared.Message errorMessage = Codec.Convert<TMProtocol.Shared.Message>(data);
                            LogError(errorMessage.Contents);
                            WaitForAction(false); // Wait for another try
                            break;
                    }
                    break;

                default:
                    Unexpected(request, WaitForAction);
                    break;
            }
        }
        #endregion

        #region Network Error Handling

        private void HandleServerError()
        {
            GameOver = true;
            _server.Socket.Close();
            LogError("Server was closed unexpectedly!");
            Thread.Sleep(1000);
            Console.Clear();
            LogError("Server was closed unexpectedly!");
            Program.HandleServerClosed(this);
        }

        #endregion

        #region Logging

        private void Unexpected(RequestType request, Action loop)
        {
            Log(request + " was unexpected at this time?", ConsoleColor.Red);
            if (!GameOver) loop();
        }

        private static readonly string logPrefix = "Internal: ";
        private static void Log(string message) => Log(message, ConsoleColor.DarkGray);
        private static void Log(string message, ConsoleColor color)
        {
            Console.Write(logPrefix);
            Console.WriteLine(message, color);
        }
        
        private static void LogBlock(string message) => LogBlock(message, ConsoleColor.Gray);
        private static void LogBlock(string message, ConsoleColor color)
        {
            Console.Write("\t|| ", ConsoleColor.Yellow);
            Console.Write(message, color);
        }

        private void LogServer(string message) => LogServer(message, ConsoleColor.Gray);
        private void LogServer(string message, ConsoleColor color)
        {
            Console.Write(_server.ServerName + ": ");
            Console.WriteLine(message, color);
        }
        private void LogServer(string[] messages) => LogServer(messages, ConsoleColor.Gray);
        private void LogServer(string[] messages, ConsoleColor color)
        {
            foreach (string message in messages) LogServer(message, color);
        }

        private void LogStory(string[] texts) => LogStory(texts, ConsoleColor.Gray);
        private void LogStory(string[] texts, ConsoleColor color)
        {
            foreach (string text in texts)
            {
                foreach (char c in text)
                {
                    Console.Write(c.ToString(), color);
                    Thread.Sleep(50);
                }
                Console.WriteLine(); // Newline
                Thread.Sleep(500);
            }
            Console.WriteLine(); // Newline
        }
        private static void LogError(params string[] message)
        {
            foreach (string text in message)
            {
                Console.WriteLine(text, ConsoleColor.Red);
            }
            Console.WriteLine(); // Newline
        }
        #endregion
    }
}
