using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using TMProtocol;
using TMProtocol.Server;
using TMProtocol.Shared;
using TMServer.Game;
using TMServer.Game.Action;
using TMServer.Game.Story;
using Console = EzConsole.EzConsole;

namespace TMServer
{
    class Server
    {
        private readonly string _name;
        private readonly int _maxPlayers;
        private TcpListener _listener;
        private readonly List<Client> _clients;
        private List<Player> _players;
        private readonly Rules _rules;
        
        private List<Client> GetRoles(Role role) => Map(_players, p => p.Role == role, p => p.Client);
        private List<Client> Mafia => GetRoles(Role.Mafia);
        private List<Client> Police => GetRoles(Role.Police);
        private List<Client> Detectives => GetRoles(Role.Detective);
        private List<Client> Doctors => GetRoles(Role.Doctor);
        private List<Client> Villagers => GetRoles(Role.Villager);

        public Server(string name, int players, Rules rules)
        {
            _name = name;
            _maxPlayers = players;
            _rules = rules;
            _clients = new List<Client>();
        }

        public void Start()
        {
            Thread game = new Thread(Run);
            game.Start();
            game.Join();
        }

        private void Run()
        {
            // Collect client handshakes
            WaitForPlayers();
            GameLoop();
        }

        #region Lobby

        private void WaitForPlayers()
        {
            _listener = new TcpListener(IPAddress.Any, Codec.StandardPort);
            _listener.Start();
            Log("Waiting for players...");
            while (_clients.Count < _maxPlayers)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient(); // Blocks until a client has connected to the server
                    new Thread(HandleClient).Start(client); // Handle communication with connected client
                }
                catch (Exception)
                {
                    /* Ignore Network Exceptions, some thrown because we stop the listener in another thread (HandleClient) while the current thread is blocked by AcceptTcpClient */
                }
            }
            _listener.Stop(); // Do not accept any more clients, all collected player sockets are stored in _clients
        }
        private void HandleClient(object clientObject)
        {
            TcpClient client = (TcpClient)clientObject;
            RequestType request = Codec.Receive(client, out string data);
            switch (request)
            {
                case RequestType.Connect:
                    TMProtocol.Client.Connect connect = Codec.Convert<TMProtocol.Client.Connect>(data);
                    // Check if player can join
                    if (_clients.Count == _maxPlayers)
                    {
                        SCodec.Send(client, TMProtocol.Server.CConnect.Reject($"Server is full ({_clients.Count}/{_maxPlayers}), try again later."), RequestType.Connect, HandleClientError);
                        break;
                    }

                    // Validate player
                    if (IsValidPlayer(connect, out string rejectReason))
                    {
                        string message =
                            $"({_clients.Count + 1}/{_maxPlayers}) {connect.RequestedName} joined the lobby!";
                        SCodec.Send(client, TMProtocol.Server.CConnect.Accept(_name, $"Welcome {connect.RequestedName} to the lobby!"), RequestType.Connect, HandleClientError);
                        Log(message, ConsoleColor.Green);
                        _clients.Add(new Client(client, connect.RequestedName));
                        SCodec.Broadcast(_clients, new TMProtocol.Server.BConnect(GetPlayerNames(), connect.RequestedName, message), RequestType.LobbyUpdate, HandleClientError);

                        if (_clients.Count == _maxPlayers) _listener.Stop(); // Don't accept more clients
                    }
                    else SCodec.Send(client, TMProtocol.Server.CConnect.Reject(rejectReason), RequestType.Connect, HandleClientError);
                    break;
                default:
                    Log(request + " was unexpected at this time?", ConsoleColor.Yellow);
                    break;
            }
        }
        private void HandleClientError(TcpClient client)
        {
            // We would not write to the network stream, this could be because the client has crashed or shut down.
            // Therefore we call the client disconnected.
            Client c = TcpToClient(client);
            Log($"{c.PlayerName} left the lobby!", ConsoleColor.DarkYellow);
            _clients.Remove(c);
            _players.RemoveAll(p => p.Client == c);
        }

        #region Connection Helper Functions

        private string[] GetPlayerNames()
        {
            string[] names = new string[_clients.Count];
            for (int i = 0; i < _clients.Count; i++) names[i] = _clients[i].PlayerName;
            return names;
        }

        private string[] GetRulesArray() =>
            new[]
            {
                $"Villagers: {_rules.Villagers}",
                $"Police: {_rules.Police}",
                $"Doctors: {_rules.Doctors}",
                $"Detectives: {_rules.Detectives}",
                $"Mafia: {_rules.Mafia}"
            };
        #endregion

        #region Connect Validation

        private bool IsValidPlayer(TMProtocol.Client.Connect request, out string rejectReason)
        {
            rejectReason = null;

            // Check if player name collision
            foreach (Client client in _clients)
            {
                if (string.Equals(client.PlayerName, request.RequestedName, StringComparison.CurrentCultureIgnoreCase))
                {
                    rejectReason = "Requested name is already in use.";
                    return false;
                }
            }

            // Validate name formatting
            if (!request.RequestedName.All(char.IsLetter))
            {
                rejectReason = "Requested name contains illegal character (name must be all letters).";
                return false;
            }

            return true;
        }

        #endregion

        #endregion

        #region Game Loop

        private void GameLoop()
        {
            SCodec.Broadcast(_clients, new TMProtocol.Server.GameStart(GetPlayerNames(), GetRulesArray()), RequestType.GameStart, HandleClientError);
            AssignRoles();
            SCodec.Broadcast(_clients, new Message(Prologue.Get(Mafia.Count)), RequestType.Story, HandleClientError);

            GameState state;
            while (true)
            {
                SCodec.Broadcast(_clients.FindAll(c => !c.Equals(Mafia.First())), new Message("You will wake up soon..."), RequestType.Story, HandleClientError);
                // MafiaWakesUp();
                WakeUpRole(Role.Mafia, Mafia, Game.Story.Night.Mafia.Get(Mafia.Count), (player, action) => {
                    bool commandExecuted = false;
                    if (CommandMafia.TryParse(action.Command, out CommandMafia cmd, out object[] args, out string error))
                    { 
                        CommandResult result = cmd.Invoke(ClientToPlayer(player), _players, args);
                        switch (result.Status)
                        {
                            case CommandStatus.Info:
                                SCodec.Send(player, new Message(result.Result), RequestType.ActionInfo, HandleClientError);
                                break;
                            case CommandStatus.Successful:
                                SCodec.Send(player, new Message(result.Result), RequestType.ActionSuccess, HandleClientError);
                                commandExecuted = true; // Successfully executed all player command actions.
                                Log(player.PlayerName + ", " + result.Result);
                                break;
                            case CommandStatus.Error:
                                SCodec.Send(player, new Message(result.ErrorMessage), RequestType.ActionError, HandleClientError);
                                break;
                        }
                    }
                    else SCodec.Send(player, new Message(error), RequestType.ActionError, HandleClientError);
                    return commandExecuted;
                });
            }
            SCodec.Broadcast(_clients, new Message(Epilogue.Get(state, Mafia.Count)), RequestType.GameEnd, HandleClientError);
        }
         
        private void AssignRoles()
        {
            Log("Assigning players new roles...");
            _players = new List<Player>();
            // Generate roles-list
            List<Role> roles = new List<Role>();
            for (int i = 0; i < _rules.Police; i++) roles.Add(Role.Police);
            for (int i = 0; i < _rules.Doctors; i++) roles.Add(Role.Doctor);
            for (int i = 0; i < _rules.Detectives; i++) roles.Add(Role.Detective);
            for (int i = 0; i < _rules.Mafia; i++) roles.Add(Role.Mafia);
            for (int i = 0; i < _rules.Villagers; i++) roles.Add(Role.Villager);
            // Shuffle
            Random rng = new Random();
            for (int i = roles.Count - 1; i > 1; i--)
            {
                int k = rng.Next(roles.Count + 1);
                Role r = roles[k];
                roles[k] = roles[i];
                roles[i] = r;
            }

            // Assign roles
            for (int i = 0; i < _clients.Count; i++)
            {
                _players.Add(new Player(_clients[i], roles[i]));
                string message = "You are a " + roles[i];
                Log(_clients[i].PlayerName + ": " + message);
                SCodec.Send(_clients[i], new TMProtocol.Shared.Message(message), RequestType.AssignedRole, HandleClientError);
            }
        }

        private void WakeUpRole(Role role, List<Client> roleClients, string[] nightStory, Func<Client, TMProtocol.Client.Action, bool> actionHandler)
        {
            if (roleClients.Count > 0)
            {
                Log(role + "wakes up.");
                SCodec.Broadcast(_clients, new Message(nightStory), RequestType.Story, HandleClientError);
                foreach (Client player in roleClients)
                {
                    Log("Waiting for " + player.PlayerName + " to take action.");
                    bool commandExecuted = false;
                    while (!commandExecuted)
                    {
                        SCodec.Send(player, new Message("You have woken up!"), RequestType.Action, HandleClientError);
                        RequestType request;
                        string data;
                        try
                        {
                            request = Codec.Receive(player.Socket, out data);
                        }
                        catch (Exception e)
                        {
                            HandleClientError(player.Socket);
                            break;
                        }
                        switch (request)
                        {
                            case RequestType.Action:
                                commandExecuted = actionHandler(player, Codec.Convert<TMProtocol.Client.Action>(data));
                                break;
                            default:
                                Log(request + " was unexpected at this time?", ConsoleColor.Yellow);
                                break;
                        }
                    }
                }
            }
        }
        private void MafiaWakesUp()
        {
            if (Mafia.Count > 0)
            {
                Log("Mafia wakes up.");
                SCodec.Broadcast(_clients, new Message(Game.Story.Night.Mafia.Get(Mafia.Count)), RequestType.Story, HandleClientError);
                SCodec.Broadcast(_clients.FindAll(c => !c.Equals(Mafia.First())), new Message("You will wake up soon..."), RequestType.Story, HandleClientError);
                foreach (Client player in Mafia)
                {
                    Log("Waiting for " + player.PlayerName + " to take action.");
                    bool commandExecuted = false;
                    while (!commandExecuted)
                    {
                        SCodec.Send(player, new Message("You have woken up!"), RequestType.Action, HandleClientError);
                        RequestType request;
                        string data;
                        try
                        {
                            request = Codec.Receive(player.Socket, out data);
                        }
                        catch (Exception e)
                        {
                            HandleClientError(player.Socket);
                            break;
                        }
                        switch (request)
                        {
                            case RequestType.Action:
                                TMProtocol.Client.Action action = Codec.Convert<TMProtocol.Client.Action>(data);
                                if (CommandMafia.TryParse(action.Command, out CommandMafia cmd, out object[] args, out string error))
                                {
                                    CommandResult result = cmd.Invoke(ClientToPlayer(player), _players, args);
                                    switch (result.Status)
                                    {
                                        case CommandStatus.Info:
                                            SCodec.Send(player, new Message(result.Result), RequestType.ActionInfo, HandleClientError);
                                            break;
                                        case CommandStatus.Successful:
                                            SCodec.Send(player, new Message(result.Result), RequestType.ActionSuccess, HandleClientError);
                                            commandExecuted = true; // Successfully executed all player command actions.
                                            Log(player.PlayerName + ", " + result.Result);
                                            break;
                                        case CommandStatus.Error:
                                            SCodec.Send(player, new Message(result.ErrorMessage), RequestType.ActionError, HandleClientError);
                                            break;
                                    }
                                }
                                else SCodec.Send(player, new Message(error), RequestType.ActionError, HandleClientError);
                                break;
                            default:
                                Log(request + " was unexpected at this time?", ConsoleColor.Yellow);
                                break;
                        }
                    }
                }
            }
        }

        #region GameLoop Helper Functions

        private Player TcpToPlayer(TcpClient tcp) => ClientToPlayer(TcpToClient(tcp));
        private Client TcpToClient(TcpClient tcp) => _clients.Find(c => c.Socket.Equals(tcp));
        private Player ClientToPlayer(Client client) => _players.Find(p => p.Client.Equals(client));

        #endregion

        #endregion

        #region Logging

        private static readonly string logPrefix = "Server: ";
        private static void Log(string message) => Log(message, ConsoleColor.DarkGray);
        private static void Log(string message, ConsoleColor color)
        {
            Console.Write(logPrefix);
            Console.WriteLine(message, color);
        }

        #endregion

        #region Linq Helpers

        static List<T1> Map<T2, T1>(IList<T2> list, Predicate<T2> p, Func<T2, T1> converter)
        {
            List<T1> r = new List<T1>();
            foreach (T2 e in list)
            {
                if (p.Invoke(e)) r.Add(converter(e));
            }
            return r;
        }

        #endregion
    }
}
