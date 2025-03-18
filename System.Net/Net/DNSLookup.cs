using System;
using System.Net;
using System.Net.Sockets;
using Utils.Net.DNS;

namespace Utils.Net
{
    /// <summary>
    /// A class for performing DNS lookups using specified name servers.
    /// </summary>
    public class DNSLookup
    {
        private readonly DNSPacketWriter packetWriter;
        private readonly DNSPacketReader packetReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="DNSLookup"/> class with default name servers.
        /// </summary>
        public DNSLookup()
        {
            packetWriter = DNSPacketWriter.Default;
            packetReader = DNSPacketReader.Default;
            NameServers = new NetworkParameters().DnsServers;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DNSLookup"/> class with custom name servers.
        /// </summary>
        /// <param name="nameServers">Array of IP addresses representing DNS name servers.</param>
        public DNSLookup(params IPAddress[] nameServers)
        {
            packetWriter = DNSPacketWriter.Default;
            packetReader = DNSPacketReader.Default;
            NameServers = nameServers;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DNSLookup"/> class with custom name servers.
        /// </summary>
        /// <param name="nameServers">Array of IP addresses representing DNS name servers.</param>
        public DNSLookup(DNSFactory factory, params IPAddress[] nameServers)
        {
            packetWriter = new DNSPacketWriter(factory);
            packetReader = new DNSPacketReader(factory);
            NameServers = nameServers;
        }

        /// <summary>
        /// Gets or sets the array of IP addresses representing DNS name servers.
        /// </summary>
        public IPAddress[] NameServers { get; set; }

        /// <summary>
        /// Sends a DNS query request to the specified name servers and returns the response.
        /// </summary>
        /// <param name="type">DNS record type to query (e.g., "A" for IPv4 address).</param>
        /// <param name="name">Domain name to query.</param>
        /// <param name="class">DNS class (default is DNSClass.ALL).</param>
        /// <returns>The DNS response header containing the query result.</returns>
        public DNSHeader Request(string type, string name, DNSClass @class = DNSClass.ALL)
        {
            DNSHeader request = new DNSHeader();
            request.RecursionDesired = true;
            request.Requests.Add(new DNSRequestRecord(type, name, @class));


            byte[] requestDatagram = packetWriter.Write(request);

            foreach (IPAddress nameServer in NameServers)
            {
                try
                {
                    byte[] responseDatagram = UdpTransport(nameServer, 53, requestDatagram);
                    return packetReader.Read(responseDatagram);
                }
                catch
                {
                }
            }

            throw new Exception("Unable to execute the request");
        }

        #region Transport Procedures
        /// <summary>
        /// Sends a UDP request and waits for a response.
        /// </summary>
        /// <param name="server">Remote machine's address.</param>
        /// <param name="port">Port to query.</param>
        /// <param name="packet">Packet to send.</param>
        /// <returns>The response result.</returns>
        private static byte[] UdpTransport(IPAddress server, int port, byte[] packet)
        {
            byte[] responseBytes = new byte[512]; // Initialize a response buffer with a maximum UDP size of 512 bytes
            int receivedBytes = 0;

            using (Socket udpSocket = new Socket(server.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
            {
                udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 5000);

                EndPoint remoteHost = new IPEndPoint(server, port);

                udpSocket.SendTo(packet, packet.Length, SocketFlags.None, remoteHost);

                receivedBytes = udpSocket.ReceiveFrom(responseBytes, responseBytes.Length, SocketFlags.None, ref remoteHost);
            }

            byte[] response = new byte[receivedBytes];
            Array.Copy(responseBytes, 0, response, 0, receivedBytes);
            return response;
        }
        #endregion
    }
}
