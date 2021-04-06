using System;
using System.Collections.Generic;
using System.Text;

namespace TMServer.Game.Action
{
    public interface ICommand<T>
    {
        bool TryParse(string[] commandParts, int partLength, out T result, out object[] arguments,
            out string error);
    }
}
