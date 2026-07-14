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
        /// <param name="factory">The DNS factory used to create packet writer and reader instances.</param>
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
        public DNSHeader Request(string type, string name, DNSClassId @class = DNSClassId.ALL)
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
                    DNSHeader response = packetReader.Read(responseDatagram);

                    // Validate response correlation: ID, QR bit and opcode must match the query.
                    if (response.ID != request.ID)
                    {
                        continue; // Stale or spoofed reply — try next server.
                    }
                    if (response.QrBit != DNS.DNSQRBit.Response)
                    {
                        continue; // Not a response packet.
                    }
                    if (response.OpCode != request.OpCode)
                    {
                        continue; // Opcode mismatch.
                    }

                    return response;
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
            byte[] responseBytes = new byte[512];
            int receivedBytes;

            IPEndPoint serverEndpoint = new IPEndPoint(server, port);

            using (Socket udpSocket = new Socket(server.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
            {
                udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 5000);

                udpSocket.Connect(serverEndpoint);
                udpSocket.Send(packet);

                EndPoint remoteEndpoint = new IPEndPoint(
                    server.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                        ? IPAddress.IPv6Any
                        : IPAddress.Any,
                    0);
                receivedBytes = udpSocket.ReceiveFrom(responseBytes, responseBytes.Length, SocketFlags.None, ref remoteEndpoint);

                // Reject datagrams that did not originate from the server we queried.
                if (!((IPEndPoint)remoteEndpoint).Address.Equals(server))
                {
                    throw new InvalidOperationException("DNS response received from unexpected endpoint.");
                }
            }

            byte[] response = new byte[receivedBytes];
            Array.Copy(responseBytes, 0, response, 0, receivedBytes);
            return response;
        }
        #endregion
    }
}
