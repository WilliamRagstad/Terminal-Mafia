using System;
using System.Collections.Generic;
using System.Text;

namespace TMServer.Game.Action
{
    class CommandResult
    {
        public CommandStatus Status;
        public string ErrorMessage;
        public string Result;

        public static CommandResult Message(string message) => new CommandResult
        {
            Status = CommandStatus.Info,
            Result = message
        };
        public static CommandResult Success(string result) => new CommandResult
        {
            Status = CommandStatus.Successful,
            Result = result
        };

        public static CommandResult Error(string message) => new CommandResult
        {
            Status = CommandStatus.Error,
            ErrorMessage = message
        };
    }
}
