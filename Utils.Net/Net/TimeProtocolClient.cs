using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Client for the Time protocol (RFC 868).
/// </summary>
public static class TimeProtocolClient
{
    private static readonly DateTime Epoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Retrieves the current time from a Time protocol server.
    /// </summary>
    /// <param name="host">Hostname or IP address of the Time server.</param>
    /// <param name="port">TCP port of the Time service, default is 37.</param>
    /// <returns>UTC time reported by the server.</returns>
    public static async Task<DateTime> GetTimeAsync(string host, int port)
    {
        using TcpClient client = new();
        await client.ConnectAsync(host, port);
        byte[] buffer = new byte[4];
        int bytesRead = 0;
        NetworkStream stream = client.GetStream();
        while (bytesRead < 4)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(bytesRead));
            if (read == 0)
            {
                throw new IOException("Connection closed before 4 bytes were received.");
            }
            bytesRead += read;
        }
        uint seconds = (uint)(buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3]);
        return Epoch.AddSeconds(seconds);
    }

    /// <summary>
    /// Retrieves the current time from a Time protocol server on the default port (37).
    /// </summary>
    /// <param name="host">Hostname or IP address of the Time server.</param>
    /// <returns>UTC time reported by the server.</returns>
    public static Task<DateTime> GetTimeAsync(string host)
    {
        return GetTimeAsync(host, 37);
    }
}
