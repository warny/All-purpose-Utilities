using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Client for the Network Time Protocol (RFC 5905).
/// </summary>
public static class NtpClient
{
    private static readonly DateTime Epoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Retrieves the current time from an NTP server.
    /// </summary>
    /// <param name="host">Hostname or IP address of the NTP server.</param>
    /// <param name="port">UDP port of the NTP service, default is 123.</param>
    /// <returns>UTC time reported by the server.</returns>
    public static async Task<DateTime> GetTimeAsync(string host, int port)
    {
        byte[] request = new byte[48];
        request[0] = 0x1B; // LI = 0, VN = 3, Mode = 3 (client)
        using UdpClient client = new();
        await client.SendAsync(request, request.Length, host, port);
        UdpReceiveResult result = await client.ReceiveAsync();
        byte[] response = result.Buffer;
        ulong intPart = (ulong)response[40] << 24 | (ulong)response[41] << 16 | (ulong)response[42] << 8 | response[43];
        ulong fracPart = (ulong)response[44] << 24 | (ulong)response[45] << 16 | (ulong)response[46] << 8 | response[47];
        double milliseconds = intPart * 1000 + fracPart * 1000.0 / 0x100000000L;
        return Epoch.AddMilliseconds(milliseconds);
    }

    /// <summary>
    /// Retrieves the current time from an NTP server on the default port (123).
    /// </summary>
    /// <param name="host">Hostname or IP address of the NTP server.</param>
    /// <returns>UTC time reported by the server.</returns>
    public static Task<DateTime> GetTimeAsync(string host)
    {
        return GetTimeAsync(host, 123);
    }
}
