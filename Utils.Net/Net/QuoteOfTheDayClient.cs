using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Client for the Quote of the Day protocol (RFC 865).
/// </summary>
public static class QuoteOfTheDayClient
{
    /// <summary>
    /// Maximum number of bytes accepted from the server before the connection is closed.
    /// RFC 865 does not mandate a limit; this cap prevents memory exhaustion from a rogue peer.
    /// </summary>
    public const int MaxResponseBytes = 65536;

    /// <summary>
    /// Retrieves a quote from a Quote of the Day server.
    /// </summary>
    /// <param name="host">Hostname or IP address of the Quote server.</param>
    /// <param name="port">TCP port of the Quote service, default is 17.</param>
    /// <param name="cancellationToken">Cancellation token applied to the connect and read operations.</param>
    /// <returns>Quote returned by the server.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the server sends more than <see cref="MaxResponseBytes"/> bytes.
    /// </exception>
    public static async Task<string> GetQuoteAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        using TcpClient client = new();
        await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        using NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[MaxResponseBytes + 1];
        int totalRead = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalRead += read;
            if (totalRead > MaxResponseBytes)
            {
                throw new InvalidDataException($"Quote of the Day response exceeded {MaxResponseBytes} bytes.");
            }
        }
        return Encoding.ASCII.GetString(buffer, 0, totalRead).TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Retrieves a quote from a Quote of the Day server on the default port (17).
    /// </summary>
    /// <param name="host">Hostname or IP address of the Quote server.</param>
    /// <param name="cancellationToken">Cancellation token applied to the connect and read operations.</param>
    /// <returns>Quote returned by the server.</returns>
    public static Task<string> GetQuoteAsync(string host, CancellationToken cancellationToken = default)
    {
        return GetQuoteAsync(host, 17, cancellationToken);
    }
}
