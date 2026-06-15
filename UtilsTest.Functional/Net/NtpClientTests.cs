using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="NtpClient"/>.
/// </summary>
[TestClass]
public class NtpClientTests
{
    /// <summary>
    /// Verifies that the NTP client parses the time provided by the server.
    /// </summary>
    [TestMethod]
    public async Task GetTimeAsync_ReturnsExpectedTime()
    {
        DateTime expected = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ulong seconds = (ulong)(expected - new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        ulong fraction = 0;
        UdpClient server = new(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        Task serverTask = Task.Run(async () =>
        {
            UdpReceiveResult request = await server.ReceiveAsync();
            byte[] response = new byte[48];
            response[0] = 0x1C; // LI = 0, VN = 3, Mode = 4 (server)
            response[40] = (byte)(seconds >> 24);
            response[41] = (byte)(seconds >> 16);
            response[42] = (byte)(seconds >> 8);
            response[43] = (byte)seconds;
            response[44] = (byte)(fraction >> 24);
            response[45] = (byte)(fraction >> 16);
            response[46] = (byte)(fraction >> 8);
            response[47] = (byte)fraction;
            await server.SendAsync(response, response.Length, request.RemoteEndPoint);
        });

        DateTime result = await NtpClient.GetTimeAsync("127.0.0.1", port);
        Assert.AreEqual(expected, result);
        server.Close();
        await serverTask;
    }
}
