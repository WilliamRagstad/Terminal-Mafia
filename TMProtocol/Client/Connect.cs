using System;
using System.Collections.Generic;
using System.Text;

namespace TMProtocol.Client
{
    public class Connect : IPacket
    {
        public string RequestedName;
        public Connect(string name)
        {
            RequestedName = name;
        }
    }
}
