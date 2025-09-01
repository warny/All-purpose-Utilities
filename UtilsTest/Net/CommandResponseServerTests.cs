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
            server.RegisterCommand("LOGIN", (ctx, args) =>
            {
                ctx.Add("AUTH");
                return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("200", ResponseSeverity.Completion, "OK") });
            });
            server.RegisterCommand("LIST", (ctx, args) =>
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
            server.RegisterCommand("PING", (ctx, args) => Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("200", ResponseSeverity.Completion, "Pong") }));
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
            server.CommandReceived += _ => Task.FromResult<IEnumerable<ServerResponse>>(System.Array.Empty<ServerResponse>());
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

