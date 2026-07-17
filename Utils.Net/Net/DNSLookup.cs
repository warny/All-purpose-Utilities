using System;
using System.IO;
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
        // RFC 6891: 4096 bytes is the recommended EDNS(0) payload size for modern resolvers.
        private const int UdpBufferSize = 4096;
        // RFC 1035 §4.2.2: DNS messages over TCP are prefixed with a 2-byte length field.
        private const int TcpReceiveTimeout = 5000;
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

            Exception? lastException = null;
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
                    if (!QuestionMatches(response, request))
                    {
                        continue; // Question section does not echo the query — stale or spoofed.
                    }

                    // If the TC (Truncation) bit is set, the UDP response was cut off.
                    // Retry the same query over TCP to get the full response (RFC 1035 §4.2.1).
                    if (response.MessageTruncated)
                    {
                        responseDatagram = TcpTransport(nameServer, 53, requestDatagram);
                        response = packetReader.Read(responseDatagram);
                        if (response.ID != request.ID || response.QrBit != DNS.DNSQRBit.Response || response.OpCode != request.OpCode)
                        {
                            continue;
                        }
                        if (!QuestionMatches(response, request))
                        {
                            continue;
                        }
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            throw new InvalidOperationException("None of the configured DNS servers returned a valid response.", lastException);
        }

        /// <summary>
        /// Returns <see langword="true"/> when the question section of <paramref name="response"/>
        /// echoes back the same name, type and class as the original <paramref name="request"/>.
        /// A mismatch indicates a stale or spoofed reply.
        /// </summary>
        private static bool QuestionMatches(DNSHeader response, DNSHeader request)
        {
            if (response.Requests.Count != request.Requests.Count)
                return false;
            for (int i = 0; i < request.Requests.Count; i++)
            {
                var sent = request.Requests[i];
                var echoed = response.Requests[i];
                if (!string.Equals(echoed.Name.ToString(), sent.Name.ToString(), StringComparison.OrdinalIgnoreCase))
                    return false;
                // Compare the numeric record type to avoid dependency on textual normalization.
                if (echoed.RequestType != sent.RequestType)
                    return false;
                if (echoed.Class != sent.Class)
                    return false;
            }
            return true;
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
            byte[] responseBytes = new byte[UdpBufferSize];
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

                // Reject datagrams that did not originate from the exact endpoint we queried.
                if (!remoteEndpoint.Equals(serverEndpoint))
                {
                    throw new InvalidOperationException("DNS response received from unexpected endpoint.");
                }
            }

            byte[] response = new byte[receivedBytes];
            Array.Copy(responseBytes, 0, response, 0, receivedBytes);
            return response;
        }

        /// <summary>
        /// Sends a DNS query over TCP and returns the response.
        /// Used as a fallback when the UDP response has the TC (Truncation) bit set.
        /// DNS over TCP prefixes each message with a 2-byte big-endian length field (RFC 1035 §4.2.2).
        /// </summary>
        private static byte[] TcpTransport(IPAddress server, int port, byte[] packet)
        {
            IPEndPoint serverEndpoint = new IPEndPoint(server, port);
            using Socket tcpSocket = new Socket(server.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, TcpReceiveTimeout);
            tcpSocket.Connect(serverEndpoint);

            // Build a single frame: 2-byte length prefix followed by the DNS message.
            // Send as one buffer in a loop to guard against partial writes.
            byte[] frame = new byte[2 + packet.Length];
            frame[0] = (byte)(packet.Length >> 8);
            frame[1] = (byte)(packet.Length & 0xFF);
            Array.Copy(packet, 0, frame, 2, packet.Length);
            int sent = 0;
            while (sent < frame.Length)
            {
                int n = tcpSocket.Send(frame, sent, frame.Length - sent, SocketFlags.None);
                if (n == 0) throw new IOException("DNS TCP connection closed during send.");
                sent += n;
            }

            // Read the 2-byte response length.
            byte[] responseLengthBytes = new byte[2];
            int read = 0;
            while (read < 2)
            {
                int n = tcpSocket.Receive(responseLengthBytes, read, 2 - read, SocketFlags.None);
                if (n == 0) throw new IOException("DNS TCP connection closed unexpectedly.");
                read += n;
            }
            int responseLength = (responseLengthBytes[0] << 8) | responseLengthBytes[1];
            if (responseLength <= 0 || responseLength > 65535)
                throw new InvalidDataException($"DNS TCP response declared invalid length {responseLength}.");

            // Read exactly responseLength bytes.
            byte[] responseBytes = new byte[responseLength];
            read = 0;
            while (read < responseLength)
            {
                int n = tcpSocket.Receive(responseBytes, read, responseLength - read, SocketFlags.None);
                if (n == 0) throw new IOException("DNS TCP connection closed unexpectedly.");
                read += n;
            }
            return responseBytes;
        }
        #endregion
    }
}
