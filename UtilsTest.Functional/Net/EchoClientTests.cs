using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="EchoClient"/>.
/// </summary>
[TestClass]
public class EchoClientTests
{
    /// <summary>
    /// Verifies that the Echo client returns the same message that was sent.
    /// </summary>
    [TestMethod]
    public async Task EchoAsync_ReturnsEchoedMessage()
    {
        const string message = "hello";
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            NetworkStream serverStream = serverClient.GetStream();
            byte[] buffer = new byte[1024];
            int read = await serverStream.ReadAsync(buffer);
            await serverStream.WriteAsync(buffer.AsMemory(0, read));
            serverClient.Close();
            listener.Stop();
        });

        string response = await EchoClient.EchoAsync("127.0.0.1", port, message);
        Assert.AreEqual(message, response);
        await serverTask;
    }
}
