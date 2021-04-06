using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TMServer.Game.Action
{
    abstract class ACommand<T> where T : ICommand<T>, new()
    {
        private static T _refT = new T();
        public string Description;
        public string[] Arguments;
        
        public Func<Player, List<Player>, object[], CommandResult> Invoke;

        #region High-Level TryParse API
        
        public static bool TryParse(string command, out T result, out object[] arguments, out string error)
        {
            result = default;
            string[] parts = command.Split(' ');
            if (parts.Length == 0)
            {
                arguments = new object[0];
                error = "Command must have an action keyword!";
                return false;
            }
            return _refT.TryParse(parts, parts.Length - 1, out result, out arguments, out error);
        }

        #endregion

        #region Implementator Helper Functions
        
        internal static bool CommandUnavailable(string command, out string error)
        {
            error = $"The command '{command}' is unknown. Use Help to see all available commands.";
            return false;
        }

        internal static bool CommandRequire(T target, string[] parts, int receivedArguments, string command, int requiredArguments, out T result, out object[] args, out string error)
        {
            result = default;
            error = null;
            args = new object[requiredArguments];

            if (receivedArguments == requiredArguments)
            {
                result = target;
                for (int i = 0; i < requiredArguments; i++) args[i] = parts[i + 1];
                return true;
            }

            error = $"The {command} command requires {requiredArguments} argument(s).";
            return false;
        }
        public static string GetHelp()
        {
            string help = string.Empty;
            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Static | BindingFlags.Public);

            foreach (FieldInfo f in fields)
            {
                string command = $"{f.Name} ";
                if (f.GetValue(typeof(T)) is ACommand<T> info)
                {
                    command = info.Arguments.Aggregate(command, (current, argument) => current + $"[{argument}] ");
                    command += $"- {info.Description}";
                }

                help += command + '\n';
            }

            return help;
        }

        #endregion
    }
}
