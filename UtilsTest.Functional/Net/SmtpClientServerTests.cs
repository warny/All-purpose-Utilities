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
/// Integration tests for <see cref="SmtpClient"/> and <see cref="SmtpServer"/>.
/// </summary>
[TestClass]
public class SmtpClientServerTests
{
    /// <summary>
    /// Verifies that a message can be transferred from client to server.
    /// </summary>
    [TestMethod]
    public async Task ClientServer_TransfersMessage()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        InMemoryStore store = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using SmtpServer server = new(store, d => d == "example.com");
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using SmtpClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        await client.EhloAsync("localhost");
        await client.SendMailAsync("alice@example.com", new[] { "bob@example.com" }, "Subject: Test\r\n\r\nBody");
        await client.QuitAsync();
        await serverTask;

        Assert.IsNotNull(store.Message);
        Assert.AreEqual("alice@example.com", store.Message!.From);
        Assert.AreEqual(1, store.Message!.Recipients.Count);
        Assert.AreEqual("bob@example.com", store.Message!.Recipients[0]);
        StringAssert.Contains(store.Message!.Data, "Subject: Test");
    }

    /// <summary>
    /// Ensures that VRFY, EXPN and HELP commands are handled by the server.
    /// </summary>
    [TestMethod]
    public async Task Server_HandlesVrfyExpnHelp()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        InMemoryStore store = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using SmtpServer server = new(store);
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using SmtpClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        await client.EhloAsync("localhost");
        string? vrfy = await client.VrfyAsync("alice");
        IReadOnlyList<string> expn = await client.ExpnAsync("list");
        IReadOnlyList<string> help = await client.HelpAsync();
        await client.QuitAsync();
        await serverTask;

        StringAssert.Contains(vrfy, "Cannot");
        Assert.AreEqual(0, expn.Count);
        StringAssert.Contains(help[0], "No help available");
    }

    private sealed class InMemoryStore : ISmtpMessageStore
    {
        public SmtpMessage? Message { get; private set; }

        public Task StoreAsync(SmtpMessage message, CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }
}
