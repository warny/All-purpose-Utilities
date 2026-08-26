using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Security.Net;

/// <summary>
/// Tests for <see cref="CommandResponseServer"/> guard-fous against connection abuse.
/// </summary>
[TestClass]
public class CommandResponseServerAbuseResistanceTests
{
    /// <summary>
    /// Ensures that the server shuts down after a configurable number of consecutive errors.
    /// </summary>
    [TestMethod]
    public async Task Server_ShutsDown_AfterConsecutiveErrors()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new() { MaxConsecutiveErrors = 3 };
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        for (int i = 0; i < 3; i++)
        {
            IReadOnlyList<ServerResponse> replies = await client.SendCommandAsync("BOGUS");
            Assert.AreEqual("502", replies[0].Code);
        }
        await serverTask; // should complete after third error
        await Assert.ThrowsExactlyAsync<IOException>(() => client.SendCommandAsync("BOGUS"));
    }

    /// <summary>
    /// Ensures that receiving a line longer than <see cref="CommandResponseServer.MaxLineLength"/>
    /// causes the server to close the session without sending a response.
    /// The client must observe EOF immediately after the oversized line.
    /// </summary>
    [TestMethod]
    public async Task MaxLineLength_ClosesSessionWithoutResponse()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            // MaxLineLength = 10 so any command longer than 10 chars terminates the session.
            using CommandResponseServer server = new() { MaxLineLength = 10 };
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using System.IO.StreamReader reader = new(client.GetStream(), System.Text.Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(client.GetStream(), System.Text.Encoding.ASCII, 1024, true)
            { NewLine = "\r\n", AutoFlush = true };

        // This 25-character line exceeds MaxLineLength=10.
        await writer.WriteLineAsync("TOOLONG_COMMAND_IS_REJECTED");

        // The server closes without writing any response; the client observes EOF.
        string? eof = await reader.ReadLineAsync();
        Assert.IsNull(eof, "Expected EOF after MaxLineLength violation, but got: " + eof);

        await serverTask;
    }
}
