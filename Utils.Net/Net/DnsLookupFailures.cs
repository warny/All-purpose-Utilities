using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Utils.Net
{
    /// <summary>
    /// Identifies the transport used for a DNS query attempt.
    /// </summary>
    public enum DnsTransport
    {
        /// <summary>The query was attempted over UDP.</summary>
        Udp,

        /// <summary>The query was attempted over TCP (typically a truncation fallback).</summary>
        Tcp,
    }

    /// <summary>
    /// Categorises why a single DNS server attempt failed.
    /// </summary>
    public enum DnsFailureKind
    {
        /// <summary>The server did not respond within the timeout budget.</summary>
        Timeout,

        /// <summary>A socket-level error occurred (connection refused, network unreachable, ...).</summary>
        SocketError,

        /// <summary>The TCP connection was closed before the full response was received.</summary>
        ConnectionClosed,

        /// <summary>The response could not be parsed as a valid DNS message.</summary>
        MalformedResponse,

        /// <summary>The response transaction identifier did not match the request.</summary>
        TransactionIdMismatch,

        /// <summary>The message was not marked as a response (QR bit not set).</summary>
        NotAResponse,

        /// <summary>The response opcode did not match the request opcode.</summary>
        OpcodeMismatch,

        /// <summary>The echoed question section did not match the request.</summary>
        QuestionMismatch,

        /// <summary>The datagram originated from an endpoint other than the one queried.</summary>
        UnexpectedEndpoint,

        /// <summary>The UDP response was truncated and the TCP fallback also failed.</summary>
        TcpFallbackFailure,

        /// <summary>A generic protocol-level violation not covered by the more specific kinds.</summary>
        ProtocolError,
    }

    /// <summary>
    /// Describes a single failed DNS server attempt, preserving enough context to diagnose why
    /// the attempt did not yield a usable response.
    /// </summary>
    /// <param name="Endpoint">The server endpoint that was queried.</param>
    /// <param name="Transport">The transport used for the attempt.</param>
    /// <param name="Kind">The category of failure.</param>
    /// <param name="Exception">The underlying exception, if any.</param>
    /// <param name="Message">A human-readable description of the failure.</param>
    public sealed record DnsServerFailure(
        IPEndPoint Endpoint,
        DnsTransport Transport,
        DnsFailureKind Kind,
        Exception? Exception,
        string Message);

    /// <summary>
    /// Thrown when every configured DNS server failed to return a usable response. The individual
    /// per-server failures are preserved in <see cref="Failures"/>, in the order in which the
    /// servers were attempted.
    /// </summary>
    public sealed class DnsLookupException : Exception
    {
        /// <summary>
        /// Gets the per-server failures, in attempt order.
        /// </summary>
        public IReadOnlyList<DnsServerFailure> Failures { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsLookupException"/> class.
        /// </summary>
        /// <param name="failures">The per-server failures, in attempt order.</param>
        public DnsLookupException(IReadOnlyList<DnsServerFailure> failures)
            : base(BuildMessage(failures))
        {
            Failures = failures is null
                ? (IReadOnlyList<DnsServerFailure>)Array.Empty<DnsServerFailure>()
                : failures.ToArray();
        }

        private static string BuildMessage(IReadOnlyList<DnsServerFailure>? failures)
        {
            if (failures is null || failures.Count == 0)
                return "No DNS server returned a valid response.";

            var lines = new List<string>(failures.Count + 1)
            {
                $"None of the {failures.Count} configured DNS server attempt(s) returned a valid response:"
            };
            foreach (DnsServerFailure failure in failures)
            {
                lines.Add($"  - {failure.Endpoint} [{failure.Transport}] {failure.Kind}: {failure.Message}");
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
