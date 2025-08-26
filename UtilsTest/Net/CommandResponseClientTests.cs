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
/// Tests for <see cref="CommandResponseClient"/>.
/// </summary>
[TestClass]
public class CommandResponseClientTests
{
    /// <summary>
    /// Verifies that multiple responses are read until a final status is received.
    /// </summary>
    [TestMethod]
    public async Task SendCommandAsync_ReadsMultipleResponses()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.CommandReceived += cmd =>
                Task.FromResult<IEnumerable<ServerResponse>>(cmd == "MULTI"
                    ? new[] { new ServerResponse(100, "Continue"), new ServerResponse(200, "Done") }
                    : System.Array.Empty<ServerResponse>());
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        IReadOnlyList<ServerResponse> responses;
        using (CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan })
        {
            await client.ConnectAsync("127.0.0.1", port);
            responses = await client.SendCommandAsync("MULTI");
            Assert.AreEqual(2, responses.Count);
            Assert.AreEqual(100, responses[0].Code);
            Assert.AreEqual(200, responses[1].Code);
        }
        await serverTask;
    }

    /// <summary>
    /// Verifies that a no-op command is sent after inactivity.
    /// </summary>
    [TestMethod]
    public async Task Client_SendsNoOp_WhenIdle()
    {
        string? received = null;
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.CommandReceived += cmd =>
            {
                received = cmd;
                return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse(200, "OK") });
            };
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using (CommandResponseClient client = new()
        {
            NoOpCommand = "NOOP",
            NoOpInterval = TimeSpan.FromMilliseconds(100)
        })
        {
            await client.ConnectAsync("127.0.0.1", port);
            await Task.Delay(300);
        }
        await serverTask;
        Assert.AreEqual("NOOP", received);
    }

    /// <summary>
    /// Verifies that the client detects a dropped connection and throws.
    /// </summary>
    [TestMethod]
    public async Task SendCommandAsync_ThrowsOnConnectionLoss()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream ns = serverClient.GetStream();
            using StreamReader reader = new(ns);
            await reader.ReadLineAsync();
            serverClient.Close();
            listener.Stop();
        });

        using CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        await Assert.ThrowsExceptionAsync<IOException>(() => client.SendCommandAsync("PING"));
        await serverTask;
    }

    /// <summary>
    /// Verifies that DisconnectAsync waits for a positive response before closing.
    /// </summary>
    [TestMethod]
    public async Task DisconnectAsync_CompletesOnPositiveResponse()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.CommandReceived += cmd => Task.FromResult<IEnumerable<ServerResponse>>(cmd == "QUIT"
                ? new[] { new ServerResponse(200, "Bye") }
                : System.Array.Empty<ServerResponse>());
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        await client.DisconnectAsync("QUIT", TimeSpan.FromMilliseconds(500));
        await serverTask;
    }

    /// <summary>
    /// Verifies that DisconnectAsync forces the connection to close when no response is received.
    /// </summary>
    [TestMethod]
    public async Task DisconnectAsync_ForcesWhenNoResponse()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream ns = serverClient.GetStream();
            using StreamReader reader = new(ns);
            await reader.ReadLineAsync();
            await reader.ReadLineAsync();
            listener.Stop();
        });

        using CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        await client.DisconnectAsync("QUIT", TimeSpan.FromMilliseconds(100));
        await serverTask;
    }

    /// <summary>
    /// Verifies that the server processes commands sent before previous responses are fully transmitted.
    /// </summary>
    [TestMethod]
    public async Task Server_ProcessesPipelinedCommands()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.CommandReceived += async cmd =>
            {
                if (cmd == "FIRST")
                {
                    await Task.Delay(200);
                }
                return new[] { new ServerResponse(200, cmd) };
            };
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using StreamWriter writer = new(ns) { NewLine = "\r\n", AutoFlush = true };
        using StreamReader reader = new(ns);

        await writer.WriteLineAsync("FIRST");
        await writer.WriteLineAsync("SECOND");

        string? response1 = await reader.ReadLineAsync();
        string? response2 = await reader.ReadLineAsync();

        Assert.AreEqual("200 FIRST", response1);
        Assert.AreEqual("200 SECOND", response2);

        client.Close();
        await serverTask;
    }

    /// <summary>
    /// Verifies that <see cref="CommandResponseClient.ReadAsync"/> retrieves server greetings before any command is sent.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_RetrievesInitialGreeting()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream ns = serverClient.GetStream();
            using StreamWriter writer = new(ns) { NewLine = "\r\n", AutoFlush = true };
            using StreamReader reader = new(ns);
            await writer.WriteLineAsync("200 Ready");
            string? command = await reader.ReadLineAsync();
            if (command == "PING")
            {
                await writer.WriteLineAsync("200 Pong");
            }
            listener.Stop();
        });

        using CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        IReadOnlyList<ServerResponse> greeting = await client.ReadAsync();
        Assert.AreEqual(200, greeting[0].Code);
        IReadOnlyList<ServerResponse> response = await client.SendCommandAsync("PING");
        Assert.AreEqual(200, response[0].Code);
        await serverTask;
    }
}

