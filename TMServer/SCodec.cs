using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using TMProtocol;
using TMProtocol.Shared;

namespace TMServer
{
    /// <summary>
    /// Codec wrappers used by Server
    /// </summary>
    public class SCodec
    {
        public static void Send(Client client, IPacket packet, RequestType type, Action<TcpClient> onError = null) =>
            Codec.Send(client.Socket, packet, type, onError);
        public static void Send(TcpClient client, IPacket packet, RequestType type, Action<TcpClient> onError = null) =>
            Codec.Send(client, packet, type, onError);
        public static void Broadcast(List<Client> clients, IPacket packet, RequestType type,
            Action<TcpClient> onError = null)
        {
            List<TcpClient> errorClients = new List<TcpClient>();
            clients.ForEach(c => Codec.Send(c.Socket, packet, type, ec => errorClients.Add(ec)));
            foreach (TcpClient errorClient in errorClients) onError?.Invoke(errorClient);
        }
    }
}
