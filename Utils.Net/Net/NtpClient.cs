using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
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

    // NTP mode field mask/values
    private const byte ModeMask = 0x07;
    private const byte ModeServer = 4;
    private const byte LeapNoWarning = 0x00;
    private const byte LeapAlarmMask = 0xC0;

    /// <summary>
    /// Retrieves the current time from an NTP server.
    /// </summary>
    /// <param name="host">Hostname or IP address of the NTP server.</param>
    /// <param name="port">UDP port of the NTP service, default is 123.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTC time reported by the server.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the server sends a malformed or unexpected NTP response.
    /// </exception>
    public static async Task<DateTime> GetTimeAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        byte[] request = new byte[NtpPacketLength];
        request[0] = 0x1B; // LI = 0, VN = 3, Mode = 3 (client)

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0)
        {
            throw new InvalidDataException($"Could not resolve host '{host}'.");
        }
        IPEndPoint serverEndpoint = new(addresses[0], port);

        using UdpClient client = new(serverEndpoint.AddressFamily);
        client.Connect(serverEndpoint);

        await client.SendAsync(request, request.Length).WaitAsync(cancellationToken).ConfigureAwait(false);
        UdpReceiveResult result = await client.ReceiveAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        byte[] response = result.Buffer;

        // Validate that the reply came from the server we contacted.
        if (!result.RemoteEndPoint.Equals(serverEndpoint))
        {
            throw new InvalidDataException("NTP response received from unexpected endpoint.");
        }

        // Minimum NTP packet is 48 bytes.
        if (response.Length < NtpPacketLength)
        {
            throw new InvalidDataException($"NTP response too short ({response.Length} bytes).");
        }

        // Byte 0: LI (bits 7-6), VN (bits 5-3), Mode (bits 2-0)
        byte firstByte = response[0];
        byte mode = (byte)(firstByte & ModeMask);
        byte leap = (byte)(firstByte & LeapAlarmMask);

        // Mode must be 4 (server) or 5 (broadcast).
        if (mode != ModeServer && mode != 5)
        {
            throw new InvalidDataException($"NTP response has unexpected mode {mode}; expected server (4) or broadcast (5).");
        }

        // LI = 3 (11) means the clock is unsynchronised — reject it.
        if (leap == LeapAlarmMask)
        {
            throw new InvalidDataException("NTP server reports unsynchronised clock (LI = 3).");
        }

        // Stratum 0 means "unspecified / invalid" in RFC 5905.
        if (response[1] == 0)
        {
            throw new InvalidDataException("NTP server reports stratum 0 (unspecified/invalid).");
        }

        ulong intPart = (ulong)response[40] << 24 | (ulong)response[41] << 16 | (ulong)response[42] << 8 | response[43];
        ulong fracPart = (ulong)response[44] << 24 | (ulong)response[45] << 16 | (ulong)response[46] << 8 | response[47];
        double milliseconds = intPart * 1000 + fracPart * 1000.0 / 0x100000000L;
        return Epoch.AddMilliseconds(milliseconds);
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
}
