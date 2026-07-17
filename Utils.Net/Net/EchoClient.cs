using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Client for the TCP Echo protocol (RFC 862).
/// </summary>
public static class EchoClient
{
    /// <summary>
    /// Sends a message to an Echo server and returns the echoed response.
    /// </summary>
    /// <param name="host">Hostname or IP address of the Echo server.</param>
    /// <param name="port">TCP port of the Echo service, default is 7.</param>
    /// <param name="message">Message to send.</param>
    /// <param name="cancellationToken">Cancellation token applied to the connect and read operations.</param>
    /// <returns>Echoed message returned by the server.</returns>
    public static async Task<string> EchoAsync(string host, int port, string message, CancellationToken cancellationToken = default)
    {
        using TcpClient client = new();
        await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        NetworkStream stream = client.GetStream();
        byte[] data = Encoding.ASCII.GetBytes(message);
        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        byte[] buffer = new byte[data.Length];
        int bytesRead = 0;
        while (bytesRead < data.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(bytesRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            bytesRead += read;
        }
        return Encoding.ASCII.GetString(buffer, 0, bytesRead);
    }

    /// <summary>
    /// Sends a message to an Echo server on the default port (7) and returns the echoed response.
    /// </summary>
    /// <param name="host">Hostname or IP address of the Echo server.</param>
    /// <param name="message">Message to send.</param>
    /// <param name="cancellationToken">Cancellation token applied to the connect and read operations.</param>
    /// <returns>Echoed message returned by the server.</returns>
    public static Task<string> EchoAsync(string host, string message, CancellationToken cancellationToken = default)
    {
        return EchoAsync(host, 7, message, cancellationToken);
    }
}
