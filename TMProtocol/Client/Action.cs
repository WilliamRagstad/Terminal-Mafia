using System;
using System.Collections.Generic;
using System.Text;

namespace TMProtocol.Client
{
    public class Action : IPacket
    {
        public string Command;

        public Action(string command)
        {
            Command = command;
        }
    }
}
