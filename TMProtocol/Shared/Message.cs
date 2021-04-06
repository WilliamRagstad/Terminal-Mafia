using System;
using System.Collections.Generic;
using System.Text;

namespace TMProtocol.Shared
{
    public class Message : IPacket
    {
        public string[] Contents;

        public Message(params string[] contents)
        {
            Contents = contents;
        }
    }
}
