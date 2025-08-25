using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Client for the Quote of the Day protocol (RFC 865).
/// </summary>
public static class QuoteOfTheDayClient
{
    /// <summary>
    /// Retrieves a quote from a Quote of the Day server.
    /// </summary>
    /// <param name="host">Hostname or IP address of the Quote server.</param>
    /// <param name="port">TCP port of the Quote service, default is 17.</param>
    /// <returns>Quote returned by the server.</returns>
    public static async Task<string> GetQuoteAsync(string host, int port)
    {
        using TcpClient client = new();
        await client.ConnectAsync(host, port);
        using NetworkStream stream = client.GetStream();
        using StreamReader reader = new(stream, Encoding.ASCII);
        string quote = await reader.ReadToEndAsync();
        return quote.TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Retrieves a quote from a Quote of the Day server on the default port (17).
    /// </summary>
    /// <param name="host">Hostname or IP address of the Quote server.</param>
    /// <returns>Quote returned by the server.</returns>
    public static Task<string> GetQuoteAsync(string host)
    {
        return GetQuoteAsync(host, 17);
    }
}
