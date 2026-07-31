using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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
        private const int DnsPort = 53;

        private readonly DNSPacketWriter packetWriter;
        private readonly DNSPacketReader packetReader;
        private readonly IDnsTransport transport;
        private IPAddress[] _nameServers = Array.Empty<IPAddress>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DNSLookup"/> class with default name servers
        /// discovered from the local network configuration.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no usable DNS server could be discovered from the host configuration.
        /// </exception>
        public DNSLookup()
        {
            packetWriter = DNSPacketWriter.Default;
            packetReader = DNSPacketReader.Default;
            transport = new SocketDnsTransport();
            IPAddress[] discovered = new NetworkParameters().DnsServers;
            if (discovered.Length == 0)
            {
                throw new InvalidOperationException(
                    "No DNS server was discovered from the host network configuration. " +
                    "Provide name servers explicitly via the DNSLookup(params IPAddress[]) constructor.");
            }
            NameServers = discovered;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DNSLookup"/> class with custom name servers.
        /// </summary>
        /// <param name="nameServers">Array of IP addresses representing DNS name servers.</param>
        public DNSLookup(params IPAddress[] nameServers)
        {
            packetWriter = DNSPacketWriter.Default;
            packetReader = DNSPacketReader.Default;
            transport = new SocketDnsTransport();
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
            transport = new SocketDnsTransport();
            NameServers = nameServers;
        }

        /// <summary>
        /// Internal constructor allowing a test transport to be injected.
        /// </summary>
        internal DNSLookup(IDnsTransport transport, params IPAddress[] nameServers)
        {
            packetWriter = DNSPacketWriter.Default;
            packetReader = DNSPacketReader.Default;
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            NameServers = nameServers;
        }

        /// <summary>
        /// Gets or sets the array of IP addresses representing DNS name servers.
        /// </summary>
        /// <remarks>
        /// The getter returns a defensive copy; mutating the returned array does not affect the
        /// configuration. The setter validates and copies its input: <see langword="null"/>, empty
        /// arrays, <see langword="null"/> entries, wildcard/unspecified addresses
        /// (<see cref="IPAddress.Any"/>, <see cref="IPAddress.IPv6Any"/>, <see cref="IPAddress.None"/>,
        /// <see cref="IPAddress.IPv6None"/>) and multicast addresses are rejected. Duplicates are
        /// removed while preserving first-seen order. Loopback addresses are accepted (a local DNS
        /// resolver is valid).
        /// </remarks>
        /// <exception cref="ArgumentNullException">The value or one of its entries is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The value is empty or contains an invalid address.</exception>
        public IPAddress[] NameServers
        {
            get => (IPAddress[])_nameServers.Clone();
            set => _nameServers = ValidateNameServers(value);
        }

        /// <summary>
        /// Validates and normalises a set of name servers: rejects null/empty/invalid entries and
        /// deduplicates while preserving order.
        /// </summary>
        private static IPAddress[] ValidateNameServers(IPAddress[] nameServers)
        {
            if (nameServers is null)
                throw new ArgumentNullException(nameof(nameServers));

            // Copy immediately so a caller mutating the input array afterwards cannot affect us.
            var snapshot = (IPAddress[])nameServers.Clone();
            if (snapshot.Length == 0)
                throw new ArgumentException("At least one DNS server must be specified.", nameof(nameServers));

            var result = new List<IPAddress>(snapshot.Length);
            var seen = new HashSet<IPAddress>();
            foreach (IPAddress address in snapshot)
            {
                if (address is null)
                    throw new ArgumentNullException(nameof(nameServers), "DNS server entries must not be null.");

                if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
                    || address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None))
                {
                    throw new ArgumentException(
                        $"'{address}' is a wildcard/unspecified address and cannot be used as a DNS server.",
                        nameof(nameServers));
                }

                if (IsMulticast(address))
                {
                    throw new ArgumentException(
                        $"'{address}' is a multicast address and cannot be used as a DNS server.",
                        nameof(nameServers));
                }

                if (seen.Add(address))
                    result.Add(address);
            }

            return result.ToArray();
        }

        private static bool IsMulticast(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return address.IsIPv6Multicast;

            // IPv4 multicast is 224.0.0.0/4 (first octet 224–239).
            byte[] bytes = address.GetAddressBytes();
            return bytes.Length == 4 && bytes[0] >= 224 && bytes[0] <= 239;
        }

        /// <summary>
        /// Sends a DNS query request to the configured name servers and returns the response.
        /// </summary>
        /// <param name="type">DNS record type to query (e.g., "A" for IPv4 address).</param>
        /// <param name="name">Domain name to query.</param>
        /// <param name="class">DNS class (default is <see cref="DNSClassId.ALL"/>).</param>
        /// <returns>The DNS response header containing the query result.</returns>
        /// <exception cref="DnsLookupException">
        /// Thrown when none of the configured servers returned a valid response. The individual
        /// failures are preserved in <see cref="DnsLookupException.Failures"/>.
        /// </exception>
        public DNSHeader Request(string type, string name, DNSClassId @class = DNSClassId.ALL)
        {
            // Bridge to the async implementation; there is no synchronization context in library code.
            return RequestAsync(type, name, @class, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously sends a DNS query request to the configured name servers and returns the response.
        /// </summary>
        /// <param name="type">DNS record type to query (e.g., "A" for IPv4 address).</param>
        /// <param name="name">Domain name to query.</param>
        /// <param name="class">DNS class (default is <see cref="DNSClassId.ALL"/>).</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The DNS response header containing the query result.</returns>
        /// <exception cref="DnsLookupException">
        /// Thrown when none of the configured servers returned a valid response. The individual
        /// failures are preserved in <see cref="DnsLookupException.Failures"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
        public async Task<DNSHeader> RequestAsync(string type, string name, DNSClassId @class = DNSClassId.ALL, CancellationToken cancellationToken = default)
        {
            DNSHeader request = new DNSHeader { RecursionDesired = true };
            request.Requests.Add(new DNSRequestRecord(type, name, @class));

            byte[] requestDatagram = packetWriter.Write(request);

            // Snapshot the configured servers so a concurrent NameServers mutation cannot change the
            // set of endpoints mid-request.
            IPAddress[] servers = _nameServers;
            var failures = new List<DnsServerFailure>();

            foreach (IPAddress nameServer in servers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IPEndPoint endpoint = new IPEndPoint(nameServer, DnsPort);

                DNSHeader? response = await TryQueryServerAsync(endpoint, request, requestDatagram, failures, cancellationToken)
                    .ConfigureAwait(false);
                if (response is not null)
                    return response;
            }

            throw new DnsLookupException(failures);
        }

        /// <summary>
        /// Attempts a single server. Returns the validated response, or <see langword="null"/> when
        /// the attempt failed (a <see cref="DnsServerFailure"/> is appended to <paramref name="failures"/>).
        /// </summary>
        private async Task<DNSHeader?> TryQueryServerAsync(
            IPEndPoint endpoint,
            DNSHeader request,
            byte[] requestDatagram,
            List<DnsServerFailure> failures,
            CancellationToken cancellationToken)
        {
            byte[] responseDatagram;
            try
            {
                responseDatagram = await transport.QueryUdpAsync(endpoint, requestDatagram, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw; // Never convert cancellation into a per-server failure.
            }
            catch (SocketException ex)
            {
                failures.Add(new DnsServerFailure(endpoint, DnsTransport.Udp, ClassifySocketError(ex), ex, ex.Message));
                return null;
            }
            catch (IOException ex)
            {
                // The socket transport raises IOException for an unexpected source endpoint.
                DnsFailureKind kind = ex.Message.Contains("unexpected endpoint", StringComparison.OrdinalIgnoreCase)
                    ? DnsFailureKind.UnexpectedEndpoint
                    : DnsFailureKind.SocketError;
                failures.Add(new DnsServerFailure(endpoint, DnsTransport.Udp, kind, ex, ex.Message));
                return null;
            }

            DNSHeader response;
            try
            {
                response = packetReader.Read(responseDatagram);
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IndexOutOfRangeException || ex is ArgumentException)
            {
                failures.Add(new DnsServerFailure(endpoint, DnsTransport.Udp, DnsFailureKind.MalformedResponse, ex, ex.Message));
                return null;
            }

            DnsFailureKind? correlation = ValidateCorrelation(response, request);
            if (correlation is not null)
            {
                failures.Add(new DnsServerFailure(endpoint, DnsTransport.Udp, correlation.Value, null,
                    $"Response failed correlation check ({correlation.Value})."));
                return null;
            }

            // If the TC (Truncation) bit is set, retry the same query over TCP (RFC 1035 §4.2.1).
            if (response.MessageTruncated)
            {
                byte[] tcpResponse;
                try
                {
                    tcpResponse = await transport.QueryTcpAsync(endpoint, requestDatagram, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is SocketException || ex is IOException || ex is InvalidDataException)
                {
                    failures.Add(new DnsServerFailure(endpoint, DnsTransport.Tcp, DnsFailureKind.TcpFallbackFailure, ex, ex.Message));
                    return null;
                }

                try
                {
                    response = packetReader.Read(tcpResponse);
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is IndexOutOfRangeException || ex is ArgumentException)
                {
                    failures.Add(new DnsServerFailure(endpoint, DnsTransport.Tcp, DnsFailureKind.MalformedResponse, ex, ex.Message));
                    return null;
                }

                DnsFailureKind? tcpCorrelation = ValidateCorrelation(response, request);
                if (tcpCorrelation is not null)
                {
                    failures.Add(new DnsServerFailure(endpoint, DnsTransport.Tcp, tcpCorrelation.Value, null,
                        $"TCP response failed correlation check ({tcpCorrelation.Value})."));
                    return null;
                }
            }

            return response;
        }

        private static DnsFailureKind ClassifySocketError(SocketException ex)
        {
            return ex.SocketErrorCode switch
            {
                SocketError.TimedOut => DnsFailureKind.Timeout,
                _ => DnsFailureKind.SocketError,
            };
        }

        /// <summary>
        /// Validates that a response correlates with the request. Returns the first failing
        /// correlation kind, or <see langword="null"/> when the response correlates correctly.
        /// </summary>
        private static DnsFailureKind? ValidateCorrelation(DNSHeader response, DNSHeader request)
        {
            if (response.ID != request.ID)
                return DnsFailureKind.TransactionIdMismatch;
            if (response.QrBit != DNSQRBit.Response)
                return DnsFailureKind.NotAResponse;
            if (response.OpCode != request.OpCode)
                return DnsFailureKind.OpcodeMismatch;
            if (!QuestionMatches(response, request))
                return DnsFailureKind.QuestionMismatch;
            return null;
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
                if (echoed.RequestType != sent.RequestType)
                    return false;
                if (echoed.Class != sent.Class)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Default socket-based <see cref="IDnsTransport"/> implementation.
        /// </summary>
        private sealed class SocketDnsTransport : IDnsTransport
        {
            private const int ReceiveTimeoutMs = 5000;

            public async Task<byte[]> QueryUdpAsync(IPEndPoint server, byte[] query, CancellationToken cancellationToken)
            {
                using var udpSocket = new Socket(server.AddressFamily, SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
                udpSocket.Connect(server);
                await udpSocket.SendAsync(query, SocketFlags.None, cancellationToken).ConfigureAwait(false);

                byte[] buffer = new byte[UdpBufferSize];
                EndPoint remoteEndpoint = new IPEndPoint(
                    server.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(ReceiveTimeoutMs);
                SocketReceiveFromResult received;
                try
                {
                    received = await udpSocket.ReceiveFromAsync(buffer, SocketFlags.None, remoteEndpoint, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new SocketException((int)SocketError.TimedOut);
                }

                if (!received.RemoteEndPoint.Equals(server))
                    throw new IOException("DNS response received from unexpected endpoint.");

                byte[] response = new byte[received.ReceivedBytes];
                Array.Copy(buffer, response, received.ReceivedBytes);
                return response;
            }

            public async Task<byte[]> QueryTcpAsync(IPEndPoint server, byte[] query, CancellationToken cancellationToken)
            {
                using var tcpSocket = new Socket(server.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                await tcpSocket.ConnectAsync(server, cancellationToken).ConfigureAwait(false);

                // DNS over TCP prefixes each message with a 2-byte big-endian length field (RFC 1035 §4.2.2).
                byte[] frame = new byte[2 + query.Length];
                frame[0] = (byte)(query.Length >> 8);
                frame[1] = (byte)(query.Length & 0xFF);
                Array.Copy(query, 0, frame, 2, query.Length);
                int sent = 0;
                while (sent < frame.Length)
                {
                    int n = await tcpSocket.SendAsync(frame.AsMemory(sent), SocketFlags.None, cancellationToken).ConfigureAwait(false);
                    if (n == 0) throw new IOException("DNS TCP connection closed during send.");
                    sent += n;
                }

                byte[] lengthBytes = await ReceiveExactlyAsync(tcpSocket, 2, cancellationToken).ConfigureAwait(false);
                int responseLength = (lengthBytes[0] << 8) | lengthBytes[1];
                if (responseLength <= 0 || responseLength > 65535)
                    throw new InvalidDataException($"DNS TCP response declared invalid length {responseLength}.");

                return await ReceiveExactlyAsync(tcpSocket, responseLength, cancellationToken).ConfigureAwait(false);
            }

            private static async Task<byte[]> ReceiveExactlyAsync(Socket socket, int count, CancellationToken cancellationToken)
            {
                byte[] buffer = new byte[count];
                int read = 0;
                while (read < count)
                {
                    int n = await socket.ReceiveAsync(buffer.AsMemory(read, count - read), SocketFlags.None, cancellationToken).ConfigureAwait(false);
                    if (n == 0) throw new IOException("DNS TCP connection closed unexpectedly.");
                    read += n;
                }
                return buffer;
            }
        }
    }
}
