using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net
{
    /// <summary>
    /// Provides the default socket-based <see cref="IDnsTransport"/> implementation.
    /// </summary>
    internal sealed class SocketDnsTransport : IDnsTransport
    {
        private const int ReceiveTimeoutMs = 5000;
        private const int UdpBufferSize = 4096;
        private static readonly TimeSpan TcpTimeout = TimeSpan.FromSeconds(5);

        /// <inheritdoc />
        public async Task<byte[]> QueryUdpAsync(IPEndPoint server, byte[] query, CancellationToken cancellationToken)
        {
            using var udpSocket = new Socket(server.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
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

            // Copy only the received datagram bytes out of the reusable receive buffer.
            byte[] response = new byte[received.ReceivedBytes];
            Array.Copy(buffer, response, received.ReceivedBytes);
            return response;
        }

        /// <inheritdoc />
        public async Task<byte[]> QueryTcpAsync(IPEndPoint server, byte[] query, CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TcpTimeout);
            CancellationToken effectiveToken = timeoutCts.Token;

            try
            {
                using var tcpSocket = new Socket(server.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await tcpSocket.ConnectAsync(server, effectiveToken).ConfigureAwait(false);

                // DNS over TCP prefixes each message with a 2-byte big-endian length field (RFC 1035 §4.2.2).
                byte[] frame = new byte[2 + query.Length];
                frame[0] = (byte)(query.Length >> 8);
                frame[1] = (byte)(query.Length & 0xFF);
                Array.Copy(query, 0, frame, 2, query.Length);
                int sent = 0;
                while (sent < frame.Length)
                {
                    int count = await tcpSocket.SendAsync(frame.AsMemory(sent), SocketFlags.None, effectiveToken).ConfigureAwait(false);
                    if (count == 0)
                        throw new IOException("DNS TCP connection closed during send.");
                    sent += count;
                }

                byte[] lengthBytes = await ReceiveExactlyAsync(tcpSocket, 2, effectiveToken).ConfigureAwait(false);
                int responseLength = (lengthBytes[0] << 8) | lengthBytes[1];
                if (responseLength <= 0 || responseLength > 65535)
                    throw new InvalidDataException($"DNS TCP response declared invalid length {responseLength}.");

                return await ReceiveExactlyAsync(tcpSocket, responseLength, effectiveToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SocketException((int)SocketError.TimedOut);
            }
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes from <paramref name="socket"/> or fails if the peer closes early.
        /// </summary>
        private static async Task<byte[]> ReceiveExactlyAsync(Socket socket, int count, CancellationToken cancellationToken)
        {
            // Socket reads may be partial, so accumulate bytes until the complete binary frame is available.
            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int received = await socket.ReceiveAsync(buffer.AsMemory(read, count - read), SocketFlags.None, cancellationToken).ConfigureAwait(false);
                if (received == 0)
                    throw new IOException("DNS TCP connection closed unexpectedly.");
                read += received;
            }
            return buffer;
        }
    }
}
