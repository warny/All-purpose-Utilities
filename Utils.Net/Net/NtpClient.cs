using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Client for the Network Time Protocol (RFC 5905).
/// </summary>
/// <remarks>
/// Unauthenticated NTP is not a trusted security clock. An on-path attacker
/// can manipulate the returned time. Do not use this client for security-sensitive
/// time comparisons (certificate expiry, replay-window checks, etc.).
/// </remarks>
public static class NtpClient
{
    private static readonly DateTime Epoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const int NtpPacketLength = 48;

    // NTP mode field mask/values.
    private const byte ModeMask = 0x07;
    private const byte ModeServer = 4;
    private const byte VersionMask = 0x38; // bits 5-3
    private const byte VersionShift = 3;
    private const byte LeapAlarmMask = 0xC0;

    // Offsets of the 64-bit NTP timestamps within the packet.
    private const int OriginateTimestampOffset = 24;
    private const int TransmitTimestampOffset = 40;

    // Default transport/clock implementations. Overridable for tests via the internal overload.
    private static readonly INtpResolver DefaultResolver = new DnsNtpResolver();
    private static readonly INtpTransport DefaultTransport = new UdpNtpTransport();

    /// <summary>
    /// Retrieves the current time from an NTP server.
    /// </summary>
    /// <param name="host">Hostname or IP address of the NTP server.</param>
    /// <param name="port">UDP port of the NTP service, default is 123.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTC time reported by the server.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="host"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is outside 1–65535.</exception>
    /// <exception cref="NtpQueryException">
    /// Thrown when the host resolves to no address, or when every resolved endpoint failed.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public static Task<DateTime> GetTimeAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        return GetTimeAsync(host, port, DefaultResolver, DefaultTransport, cancellationToken);
    }

    /// <summary>
    /// Retrieves the current time from an NTP server on the default port (123).
    /// </summary>
    /// <param name="host">Hostname or IP address of the NTP server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTC time reported by the server.</returns>
    public static Task<DateTime> GetTimeAsync(string host, CancellationToken cancellationToken = default)
    {
        return GetTimeAsync(host, 123, cancellationToken);
    }

    /// <summary>
    /// Internal core allowing the resolver and transport to be substituted for tests.
    /// </summary>
    internal static async Task<DateTime> GetTimeAsync(
        string host,
        int port,
        INtpResolver resolver,
        INtpTransport transport,
        CancellationToken cancellationToken)
    {
        // Validate arguments before any DNS resolution.
        if (host is null)
            throw new ArgumentNullException(nameof(host));
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host must not be empty or whitespace.", nameof(host));
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535.");

        IPAddress[] addresses = await resolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);
        if (addresses is null || addresses.Length == 0)
        {
            throw new NtpQueryException($"Could not resolve host '{host}'.", Array.Empty<NtpEndpointFailure>());
        }

        var failures = new List<NtpEndpointFailure>();
        foreach (IPAddress address in addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IPEndPoint endpoint = new(address, port);

            // A fresh, non-zero transmit timestamp per attempt lets us correlate the reply.
            byte[] request = BuildRequest(out ulong transmitTimestamp);

            byte[] response;
            try
            {
                response = await transport.ExchangeAsync(endpoint, request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation immediately; never record it as an endpoint failure.
            }
            catch (SocketException ex)
            {
                failures.Add(new NtpEndpointFailure(endpoint, address.AddressFamily, NtpPhase.Exchange, ex, ex.Message));
                continue;
            }
            catch (IOException ex)
            {
                failures.Add(new NtpEndpointFailure(endpoint, address.AddressFamily, NtpPhase.Exchange, ex, ex.Message));
                continue;
            }

            try
            {
                return ParseResponse(response, endpoint, transmitTimestamp);
            }
            catch (InvalidDataException ex)
            {
                failures.Add(new NtpEndpointFailure(endpoint, address.AddressFamily, NtpPhase.Validation, ex, ex.Message));
            }
        }

        throw new NtpQueryException(
            $"All {addresses.Length} resolved endpoint(s) for '{host}' failed.", failures);
    }

    private static byte[] BuildRequest(out ulong transmitTimestamp)
    {
        byte[] request = new byte[NtpPacketLength];
        request[0] = 0x1B; // LI = 0, VN = 3, Mode = 3 (client)

        // A random, non-zero transmit timestamp acts as a nonce for request/response correlation.
        Span<byte> nonce = stackalloc byte[8];
        do
        {
            RandomNumberGenerator.Fill(nonce);
            transmitTimestamp = ((ulong)nonce[0] << 56) | ((ulong)nonce[1] << 48) | ((ulong)nonce[2] << 40)
                | ((ulong)nonce[3] << 32) | ((ulong)nonce[4] << 24) | ((ulong)nonce[5] << 16)
                | ((ulong)nonce[6] << 8) | nonce[7];
        }
        while (transmitTimestamp == 0);

        for (int i = 0; i < 8; i++)
            request[TransmitTimestampOffset + i] = nonce[i];

        return request;
    }

    private static DateTime ParseResponse(byte[] response, IPEndPoint endpoint, ulong expectedOriginate)
    {
        if (response.Length < NtpPacketLength)
            throw new InvalidDataException($"NTP response too short ({response.Length} bytes).");

        byte firstByte = response[0];
        byte mode = (byte)(firstByte & ModeMask);
        byte version = (byte)((firstByte & VersionMask) >> VersionShift);
        byte leap = (byte)(firstByte & LeapAlarmMask);

        // Accept only NTP v3 or v4 server responses.
        if (version != 3 && version != 4)
            throw new InvalidDataException($"NTP response has unsupported version {version}; expected 3 or 4.");

        // Mode must be exactly 4 (server). Broadcast (5) and client (3) are rejected: this is a
        // connected unicast client/server transaction (item 57).
        if (mode != ModeServer)
            throw new InvalidDataException($"NTP response has unexpected mode {mode}; only server mode (4) is accepted.");

        // LI = 3 (11) means the clock is unsynchronised — reject it.
        if (leap == LeapAlarmMask)
            throw new InvalidDataException("NTP server reports unsynchronised clock (LI = 3).");

        // Stratum 0 means "unspecified / invalid" in RFC 5905.
        if (response[1] == 0)
            throw new InvalidDataException("NTP server reports stratum 0 (unspecified/invalid).");

        // Correlate the reply: the server must echo our transmit timestamp in its Originate field.
        ulong originate = ReadTimestamp(response, OriginateTimestampOffset);
        if (originate != expectedOriginate)
            throw new InvalidDataException("NTP originate timestamp does not match the request; possible spoofed reply.");

        // The server transmit timestamp must be non-zero.
        ulong transmit = ReadTimestamp(response, TransmitTimestampOffset);
        if (transmit == 0)
            throw new InvalidDataException("NTP server transmit timestamp is zero.");

        return ConvertTimestamp(transmit, endpoint);
    }

    private static ulong ReadTimestamp(byte[] data, int offset)
    {
        ulong value = 0;
        for (int i = 0; i < 8; i++)
            value = (value << 8) | data[offset + i];
        return value;
    }

    private static DateTime ConvertTimestamp(ulong timestamp, IPEndPoint endpoint)
    {
        uint seconds = (uint)(timestamp >> 32);
        uint fraction = (uint)(timestamp & 0xFFFFFFFF);

        // Convert the 32-bit fraction of a second to ticks using controlled integer arithmetic.
        // ticks = fraction * TicksPerSecond / 2^32, computed in 128-bit-safe steps to avoid overflow
        // and to avoid the precision loss of intermediate double arithmetic.
        long fractionTicks = (long)(((ulong)fraction * (ulong)TimeSpan.TicksPerSecond) >> 32);

        try
        {
            return Epoch
                .AddSeconds(seconds)
                .AddTicks(fractionTicks);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidDataException(
                $"NTP timestamp from {endpoint} is out of the representable DateTime range.", ex);
        }
    }
}
