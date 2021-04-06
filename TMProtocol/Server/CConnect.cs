using System;
using System.Collections.Generic;
using System.Text;

namespace TMProtocol.Server
{
    /// <summary>
    /// Server -> One Client: connect response
    /// </summary>
    public class CConnect : IPacket
    {
        public string ServerName;
        public bool Accepted;
        public string Message;
        
        public static CConnect Reject(string reason) => new CConnect(null, false, reason);
        public static CConnect Accept(string name, string message) => new CConnect(name, true, message);
        public CConnect(string name, bool accepted, string message)
        {
            ServerName = name;
            Accepted = accepted;
            Message = message;
        }
    }
}
