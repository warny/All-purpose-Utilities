using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net
{
    /// <summary>
    /// Abstracts the network transport used by <see cref="DNSLookup"/> so that the lookup logic
    /// (server iteration, correlation, error aggregation) can be tested without touching real
    /// sockets.
    /// </summary>
    internal interface IDnsTransport
    {
        /// <summary>
        /// Sends a DNS query over UDP to <paramref name="server"/> and returns the raw response.
        /// </summary>
        /// <exception cref="SocketException">A socket-level error occurred.</exception>
        /// <exception cref="IOException">The endpoint that answered was unexpected, or the datagram was invalid.</exception>
        /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
        Task<byte[]> QueryUdpAsync(IPEndPoint server, byte[] query, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a DNS query over TCP to <paramref name="server"/> and returns the raw response.
        /// </summary>
        /// <exception cref="SocketException">A socket-level error occurred.</exception>
        /// <exception cref="IOException">The connection was closed before the full response was received.</exception>
        /// <exception cref="InvalidDataException">The response framing was invalid.</exception>
        /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
        Task<byte[]> QueryTcpAsync(IPEndPoint server, byte[] query, CancellationToken cancellationToken);
    }
}
