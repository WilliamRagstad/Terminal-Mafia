using System;
using System.Collections.Generic;
using System.Text;

namespace TMServer.Game.Action
{
    class CommandMafia : ACommand<CommandMafia>, ICommand<CommandMafia>
    {
        #region Commands
        
        public static CommandMafia Kill = new CommandMafia(new [] {"Player"}, "Kill a player during the night.",
            (actor, players , args) =>
            {
                string target = args[0] as string;
                if (string.Equals(target, actor.Name, StringComparison.CurrentCultureIgnoreCase)) return CommandResult.Error("You can't kill yourself.");
                foreach (var player in players)
                {
                    if (string.Equals(player.Name, target, StringComparison.CurrentCultureIgnoreCase)) return CommandResult.Success($"You are trying to kill {player.Name} this night.");
                }
                return CommandResult.Error($"Could not find player {target}!");
            });
        public static CommandMafia List = new CommandMafia(new string[0], "List players", (actor, players, args) =>
        {
            string list = "The current players are ";
            bool first = true;
            bool otherPlayers = false;
            foreach (var player in players)
            {
                if (player == actor) continue;
                otherPlayers = true;
                if (!first) list += ", ";
                list += player.Name;
                first = false;
            }
            if (otherPlayers) list += " and ";
            list += "You.";
            return CommandResult.Message(list);
        });
        public static CommandMafia Help = new CommandMafia(new string[0], "Help page", (actor, players , args)=> CommandResult.Message(GetHelp()));

        #endregion

        #region Constructors
        public CommandMafia() {} // Empty Constructor for reference
        public CommandMafia(string[] args, string description, Func<Player, List<Player>, object[], CommandResult> invoke)
        {
            Arguments = args;
            Description = description;
            Invoke = invoke;
        }

        #endregion

        #region TryParse
        public bool TryParse(string[] parts, int argsLength, out CommandMafia result, out object[] args, out string error)
        {
            switch (parts[0].ToLower())
            {
                case "kill": return CommandRequire(Kill, parts, argsLength, "Kill", 1, out result, out args, out error);
                case "help": return CommandRequire(Help, parts, argsLength, "Help", 0, out result, out args, out error);
                case "list": return CommandRequire(List, parts, argsLength, "List", 0, out result, out args, out error);
                default:
                    result = null;
                    args = new object[0];
                    return CommandUnavailable(parts[0], out error);
            }
        }

        #endregion
    }
}
