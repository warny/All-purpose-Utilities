using System;
using System.Collections.Generic;
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
                return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse(200, "OK") });
            });
            server.RegisterCommand("LIST", (ctx, args) =>
                Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse(200, "Listed") }),
                "AUTH");
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        IReadOnlyList<ServerResponse> responses = await client.SendCommandAsync("LIST");
        Assert.AreEqual(503, responses[0].Code);
        responses = await client.SendCommandAsync("LOGIN");
        Assert.AreEqual(200, responses[0].Code);
        responses = await client.SendCommandAsync("LIST");
        Assert.AreEqual(200, responses[0].Code);
        client.Dispose();
        await serverTask;
    }
}

