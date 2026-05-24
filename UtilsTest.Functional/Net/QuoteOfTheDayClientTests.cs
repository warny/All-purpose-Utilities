using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="QuoteOfTheDayClient"/>.
/// </summary>
[TestClass]
public class QuoteOfTheDayClientTests
{
    /// <summary>
    /// Verifies that the Quote of the Day client retrieves the quote sent by the server.
    /// </summary>
    [TestMethod]
    public async Task GetQuoteAsync_ReturnsQuote()
    {
        const string quote = "Be yourself";
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            NetworkStream serverStream = serverClient.GetStream();
            byte[] bytes = Encoding.ASCII.GetBytes(quote + "\r\n");
            await serverStream.WriteAsync(bytes);
            serverClient.Close();
            listener.Stop();
        });

        string result = await QuoteOfTheDayClient.GetQuoteAsync("127.0.0.1", port);
        Assert.AreEqual(quote, result);
        await serverTask;
    }
}
