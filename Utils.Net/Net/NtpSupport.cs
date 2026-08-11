using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net
{
    /// <summary>
    /// Identifies the phase in which an NTP endpoint attempt failed.
    /// </summary>
    public enum NtpPhase
    {
        /// <summary>The UDP send/receive exchange with the endpoint failed.</summary>
        Exchange,

        /// <summary>The response was received but failed protocol validation.</summary>
        Validation,
    }

    /// <summary>
    /// Describes a single failed NTP endpoint attempt.
    /// </summary>
    /// <param name="Endpoint">The endpoint that was contacted.</param>
    /// <param name="Family">The address family of the endpoint.</param>
    /// <param name="Phase">The phase in which the attempt failed.</param>
    /// <param name="Exception">The underlying exception, if any.</param>
    /// <param name="Message">A human-readable description of the failure.</param>
    public sealed record NtpEndpointFailure(
        IPEndPoint Endpoint,
        AddressFamily Family,
        NtpPhase Phase,
        Exception? Exception,
        string Message);

    /// <summary>
    /// Thrown when an NTP query could not be satisfied by any resolved endpoint. The individual
    /// per-endpoint failures are preserved in <see cref="Failures"/>, in attempt order.
    /// </summary>
    public sealed class NtpQueryException : Exception
    {
        /// <summary>
        /// Gets the per-endpoint failures, in attempt order.
        /// </summary>
        public IReadOnlyList<NtpEndpointFailure> Failures { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NtpQueryException"/> class.
        /// </summary>
        /// <param name="message">A description of the overall failure.</param>
        /// <param name="failures">The per-endpoint failures, in attempt order.</param>
        public NtpQueryException(string message, IReadOnlyList<NtpEndpointFailure> failures)
            : base(message)
        {
            Failures = failures?.ToImmutableArray() ?? ImmutableArray<NtpEndpointFailure>.Empty;
        }
    }

    /// <summary>
    /// Resolves an NTP host name to a set of candidate addresses. Abstracted so DNS resolution can
    /// be substituted in tests.
    /// </summary>
    internal interface INtpResolver
    {
        Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Performs a single UDP request/response exchange with an NTP endpoint. Abstracted so the
    /// socket exchange can be substituted in tests.
    /// </summary>
    internal interface INtpTransport
    {
        Task<byte[]> ExchangeAsync(IPEndPoint endpoint, byte[] request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// DNS-based <see cref="INtpResolver"/>.
    /// </summary>
    internal sealed class DnsNtpResolver : INtpResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(host, out IPAddress? literal))
                return Task.FromResult(new[] { literal });

            return Dns.GetHostAddressesAsync(host, cancellationToken);
        }
    }

    /// <summary>
    /// UDP socket-based <see cref="INtpTransport"/>.
    /// </summary>
    internal sealed class UdpNtpTransport : INtpTransport
    {
        private static readonly TimeSpan DefaultReceiveTimeout = TimeSpan.FromSeconds(5);

        private readonly TimeSpan _receiveTimeout;

        /// <summary>
        /// Initializes a new instance of <see cref="UdpNtpTransport"/> with the default 5-second
        /// receive timeout.
        /// </summary>
        public UdpNtpTransport() : this(DefaultReceiveTimeout) { }

        /// <summary>
        /// Initializes a new instance of <see cref="UdpNtpTransport"/> with an injectable receive
        /// timeout. Intended for use in tests that need a shorter timeout to avoid slow test runs.
        /// </summary>
        /// <param name="receiveTimeout">The maximum time to wait for a UDP reply.</param>
        internal UdpNtpTransport(TimeSpan receiveTimeout)
        {
            if (receiveTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    nameof(receiveTimeout),
                    receiveTimeout,
                    "Receive timeout must be greater than zero.");

            if (receiveTimeout == System.Threading.Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(
                    nameof(receiveTimeout),
                    receiveTimeout,
                    "Receive timeout must not be infinite.");

            _receiveTimeout = receiveTimeout;
        }

        public async Task<byte[]> ExchangeAsync(IPEndPoint endpoint, byte[] request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(request);
            if (request.Length == 0)
                throw new ArgumentException("The NTP request must not be empty.", nameof(request));

            using var client = new UdpClient(endpoint.AddressFamily);
            client.Connect(endpoint);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_receiveTimeout);
            CancellationToken effectiveToken = timeoutCts.Token;

            try
            {
                await client.SendAsync(request, request.Length).WaitAsync(effectiveToken).ConfigureAwait(false);

                UdpReceiveResult result = await client.ReceiveAsync(effectiveToken).ConfigureAwait(false);

                if (!result.RemoteEndPoint.Equals(endpoint))
                    throw new System.IO.IOException("NTP response received from unexpected endpoint.");

                return result.Buffer;
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The NTP exchange was canceled by the caller.", ex, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SocketException((int)SocketError.TimedOut);
            }
        }
    }
}
