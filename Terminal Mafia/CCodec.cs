using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using TMProtocol;
using TMProtocol.Shared;

namespace Terminal_Mafia
{
    class CCodec
    {
        public static void Send(Server server, IPacket packet, RequestType type, Action onError = null) =>
            Codec.Send(server.Socket, packet, type, client => onError?.Invoke());
        public static RequestType Receive(Server server, out string data, Action onError = null) =>
            Codec.Receive(server.Socket, out data, client => onError?.Invoke());
    }
}
