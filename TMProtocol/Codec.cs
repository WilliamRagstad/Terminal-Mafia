using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using TMProtocol.Shared;

namespace TMProtocol
{
    public static class Codec
    {
        public static readonly int StandardPort = 8209;
        public static readonly int StandardTimeout = 5000; // 5 seconds

        private const int BufferSize = 256;
        private static readonly Encoding StandardEncoding = Encoding.ASCII;
        private static readonly byte[] Buffer = new byte[BufferSize];
        private static readonly Queue<string> PacketQueue = new Queue<string>();

        public static void Send(TcpClient socket, IPacket packet, RequestType type, Action<TcpClient> onFail = null)
        {
            try
            {
                string data = type + JsonConvert.SerializeObject(packet);
                socket.GetStream().Write(StandardEncoding.GetBytes(data));
            }
            catch (IOException)
            {
                if (onFail != null) onFail.Invoke(socket);
                else throw;
            }
        }

        public static RequestType Receive(TcpClient socket, out string data, Action<TcpClient> onNoData = null)
        {
            if (PacketQueue.Count > 0) data = PacketQueue.Dequeue();
            else
            {
                // Read all new data from network stream.

                // socket.ReceiveTimeout = StandardTimeout;
                // Read the package from a TCP Socket
                data = "";
                int read = BufferSize;
                while (read == BufferSize)
                {
                    try
                    {
                        read = socket.GetStream().Read(Buffer, 0, BufferSize);
                        data += StandardEncoding.GetString(Buffer, 0, read);
                    }
                    catch { break; }
                }

                if (data.Length == 0)
                {
                    if (onNoData != null) onNoData.Invoke(socket);
                    else throw new InvalidDataException("No data received from socket network stream.");
                    return RequestType.CorruptData;
                }

                // Analyze data and enqueue all packets that are occupying network stream.
                string[] packets = ParsePackets(data);
                data = packets[0];
                for (int i = 1; i < packets.Length; i++) PacketQueue.Enqueue(packets[i]);
            }

            int separator = data.IndexOf('{');
            string type = data.Substring(0, separator);
            data = data.Remove(0, separator);

            foreach(RequestType request in Enum.GetValues(typeof(RequestType)))
            {
                string requestName = Enum.GetName(typeof(RequestType), request);
                if (string.Equals(requestName, type, StringComparison.CurrentCultureIgnoreCase)) return request;
            }
            throw new InvalidDataException($"Packet type not found! Protocol does not cover support for '{type}'.");
        }

        public static T Receive<T>(TcpClient socket, RequestType expectedResponse, Action<TcpClient> onNoData = null) where T : IPacket
        {
            RequestType response = Receive(socket, out string data, onNoData);
            if (response == expectedResponse) return Convert<T>(data);
            throw new ArgumentException("Packet type mismatch! Expected receive type must be the same as actual type.", "Expected " + expectedResponse + ", got " + response);
        }

        public static T Exchange<T>(TcpClient socket, IPacket packet, RequestType response, RequestType expectedResponse, Action<TcpClient> onFailSend = null, Action<TcpClient> onNoData = null) where T : IPacket
        {
            Send(socket, packet, response, onFailSend);
            return Receive<T>(socket, expectedResponse, onNoData);
        }

        public static T Convert<T>(string data) where T : IPacket => JsonConvert.DeserializeObject<T>(data);

        #region Helper Functions
        
        private static string[] ParsePackets(string data)
        {
            List<string> packetsList = new List<string>();
            int depth = 0;
            string packet = string.Empty;
            for (int i = 0; i < data.Length; i++)
            {
                switch (data[i])
                {
                    case '{':
                        depth++;
                        break;
                    case  '}':
                        depth--;
                        if (depth == 0)
                        {
                            packetsList.Add(packet + data[i]); // New packet discovered
                            packet = string.Empty;
                            continue;
                        }
                        break;
                }

                packet += data[i];
            }
            return packetsList.ToArray();
        }

        #endregion
    }
}
