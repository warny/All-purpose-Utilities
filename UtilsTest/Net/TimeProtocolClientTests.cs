using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="TimeProtocolClient"/>.
/// </summary>
[TestClass]
public class TimeProtocolClientTests
{
    /// <summary>
    /// Verifies that the Time protocol client parses the time sent by the server.
    /// </summary>
    [TestMethod]
    public async Task GetTimeAsync_ReturnsExpectedTime()
    {
        DateTime expected = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        uint seconds = (uint)(expected - new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            NetworkStream stream = serverClient.GetStream();
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(seconds >> 24);
            bytes[1] = (byte)(seconds >> 16);
            bytes[2] = (byte)(seconds >> 8);
            bytes[3] = (byte)seconds;
            await stream.WriteAsync(bytes);
            serverClient.Close();
            listener.Stop();
        });

        DateTime result = await TimeProtocolClient.GetTimeAsync("127.0.0.1", port);
        Assert.AreEqual(expected, result);
        await serverTask;
    }
}
