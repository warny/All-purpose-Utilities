using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="CommandResponseServer"/> command mapping and contexts.
/// </summary>
[TestClass]
public class CommandResponseServerTests
{
    /// <summary>
    /// Ensures that commands are gated by required contexts.
    /// </summary>
    [TestMethod]
    public async Task Command_RequiresContext()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.RegisterCommand("LOGIN", (ctx, args, ct) =>
            {
                ctx.Add("AUTH");
                return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("200", ResponseSeverity.Completion, "OK") });
            });
            server.RegisterCommand("LIST", (ctx, args, ct) =>
                Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("200", ResponseSeverity.Completion, "Listed") }),
                "AUTH");
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        IReadOnlyList<ServerResponse> responses = await client.SendCommandAsync("LIST");
        Assert.AreEqual("503", responses[0].Code);
        responses = await client.SendCommandAsync("LOGIN");
        Assert.AreEqual("200", responses[0].Code);
        responses = await client.SendCommandAsync("LIST");
        Assert.AreEqual("200", responses[0].Code);
        client.Dispose();
        await serverTask;
    }

    /// <summary>
    /// Ensures that server logging captures commands and responses.
    /// </summary>
    [TestMethod]
    public async Task Server_LogsExchanges()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        ListLogger logger = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new() { Logger = logger };
            server.RegisterCommand("PING", (ctx, args, ct) => Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("200", ResponseSeverity.Completion, "Pong") }));
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        await client.SendCommandAsync("PING");
        client.Dispose();
        await serverTask;

        Assert.IsTrue(logger.Entries.Exists(e => e.Contains("Received: PING")), "Command not logged");
        Assert.IsTrue(logger.Entries.Exists(e => e.Contains("Sending: 200 Pong")), "Response not logged");
    }

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
        await Assert.ThrowsExceptionAsync<IOException>(() => client.SendCommandAsync("BOGUS"));
    }

    /// <summary>
    /// Ensures that <see cref="CommandResponseServer.CloseAfterResponse"/> causes the server to
    /// send the response produced by the handler and then immediately close the session.
    /// The client must receive the response and then observe EOF on the next read.
    /// </summary>
    [TestMethod]
    public async Task CloseAfterResponse_SendsResponseThenClosesSession()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            // The handler calls CloseAfterResponse() before returning its response.
            // The close must happen AFTER the response is flushed (not during the handler),
            // so the client receives "200 Closing" before the TCP FIN.
            server.RegisterCommand("QUIT", (ctx, args, ct) =>
            {
                server.CloseAfterResponse();
                return Task.FromResult<IEnumerable<ServerResponse>>(
                    new[] { new ServerResponse("200", ResponseSeverity.Completion, "Closing") });
            });
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using System.IO.StreamReader reader = new(client.GetStream(), System.Text.Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(client.GetStream(), System.Text.Encoding.ASCII, 1024, true)
            { NewLine = "\r\n", AutoFlush = true };

        await writer.WriteLineAsync("QUIT");

        string? response = await reader.ReadLineAsync();
        Assert.IsNotNull(response, "The handler's response must be delivered before close");
        Assert.IsTrue(response!.StartsWith("200"), $"Expected 200 Closing, got: {response}");

        // Session must be closed: next read returns EOF.
        string? eof = await reader.ReadLineAsync();
        Assert.IsNull(eof, "Expected EOF after CloseAfterResponse, but got: " + eof);

        await serverTask;
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

    /// <summary>
    /// Ensures that handlers can return no responses.
    /// </summary>
    [TestMethod]
    public async Task CommandReceived_AllowsEmptyResponses()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.CommandReceived += (_, ct) => Task.FromResult<IEnumerable<ServerResponse>>(System.Array.Empty<ServerResponse>());
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync("127.0.0.1", port);
        using (StreamWriter writer = new(client.GetStream()))
        {
            writer.NewLine = "\r\n";
            await writer.WriteLineAsync("TEST");
            await writer.FlushAsync();
        }
        client.Close();
        await serverTask;
    }
}

