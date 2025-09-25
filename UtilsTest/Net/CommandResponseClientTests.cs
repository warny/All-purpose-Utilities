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
                    ?
					[
						new ServerResponse("100", ResponseSeverity.Preliminary, "Continue"),
                        new ServerResponse("200", ResponseSeverity.Completion, "Done")
                    ]
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
            Assert.AreEqual("100", responses[0].Code);
            Assert.AreEqual(ResponseSeverity.Preliminary, responses[0].Severity);
            Assert.AreEqual("200", responses[1].Code);
            Assert.AreEqual(ResponseSeverity.Completion, responses[1].Severity);
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
                return Task.FromResult<IEnumerable<ServerResponse>>([new ServerResponse("200", ResponseSeverity.Completion, "OK")]);
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
    /// Verifies that the client stops waiting when the server stays silent beyond the listen timeout.
    /// </summary>
    [TestMethod]
    public async Task SendCommandAsync_ThrowsWhenServerSilent()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        TaskCompletionSource tcs = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            await tcs.Task;
            listener.Stop();
        });

        using CommandResponseClient client = new()
        {
            NoOpInterval = Timeout.InfiniteTimeSpan,
            ListenTimeout = TimeSpan.FromMilliseconds(100)
        };
        await client.ConnectAsync("127.0.0.1", port);
        await Assert.ThrowsExceptionAsync<IOException>(() => client.SendCommandAsync("PING"));
        tcs.SetResult();
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
                ? new[] { new ServerResponse("200", ResponseSeverity.Completion, "Bye") }
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
                return new[] { new ServerResponse("200", ResponseSeverity.Completion, cmd) };
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
        Assert.AreEqual("200", greeting[0].Code);
        IReadOnlyList<ServerResponse> response = await client.SendCommandAsync("PING");
        Assert.AreEqual("200", response[0].Code);
        await serverTask;
    }

    /// <summary>
    /// Verifies that client logging captures sent commands and received responses.
    /// </summary>
    [TestMethod]
    public async Task Client_LogsExchanges()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using CommandResponseServer server = new();
            server.CommandReceived += cmd => Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("200", ResponseSeverity.Completion, "Pong") });
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        ListLogger logger = new();
        using CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan, Logger = logger };
        await client.ConnectAsync("127.0.0.1", port);
        await client.SendCommandAsync("PING");
        await client.DisconnectAsync();
        await serverTask;

        Assert.IsTrue(logger.Entries.Exists(e => e.Contains("Sending: PING")), "Command send not logged");
        Assert.IsTrue(logger.Entries.Exists(e => e.Contains("Received: 200 Pong")), "Response receive not logged");
    }

    /// <summary>
    /// Ensures that disposing the client completes even when the server remains silent.
    /// </summary>
    [TestMethod]
    public async Task Dispose_CompletesWhenServerSilent()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        TaskCompletionSource tcs = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            await tcs.Task;
            listener.Stop();
        });

        CommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        Task disposeTask = Task.Run(client.Dispose);
        Assert.IsTrue(disposeTask.Wait(TimeSpan.FromSeconds(1)), "Dispose timed out");
        tcs.SetResult();
        await serverTask;
    }

    /// <summary>
    /// Ensures that overriding <see cref="CommandResponseClient.OnConnect(Stream, bool, CancellationToken)"/> is honored when connecting.
    /// </summary>
    [TestMethod]
    public async Task ConnectAsync_InvokesOnConnectOverride()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream serverStream = serverClient.GetStream();
            await Task.Delay(50);
            listener.Stop();
        });

        using OnConnectClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        Assert.IsTrue(client.OnConnectInvoked, "OnConnect override was not invoked.");
        await client.DisconnectAsync();
        await serverTask;
    }

    /// <summary>
    /// Ensures that response lines are split into code and message segments.
    /// </summary>
    [TestMethod]
    public void SplitCodeAndMessage_SplitsCorrectly()
    {
        TestClient client = new();
        (string code, string? message) = TestClient.Split("123 Example message");
        Assert.AreEqual("123", code);
        Assert.AreEqual("Example message", message);
        (code, message) = TestClient.Split("LINE");
        Assert.AreEqual("LINE", code);
        Assert.IsNull(message);
    }

    /// <summary>
    /// Test client exposing the split helper.
    /// </summary>
    private class TestClient : CommandResponseClient
    {
        public override int DefaultPort { get; } = 50;

        /// <summary>
        /// Exposes <see cref="CommandResponseClient.SplitCodeAndMessage(string)"/> for testing.
        /// </summary>
        /// <param name="line">Line to split.</param>
        /// <returns>Tuple containing code and optional message.</returns>
        public static (string code, string? message) Split(string line) => SplitCodeAndMessage(line);
    }

    /// <summary>
    /// Test client used to verify that <see cref="CommandResponseClient.OnConnect(Stream, bool, CancellationToken)"/> overrides are invoked.
    /// </summary>
    private sealed class OnConnectClient : CommandResponseClient
    {
        /// <summary>
        /// Gets a value indicating whether <see cref="OnConnect(Stream, bool, CancellationToken)"/> has been invoked.
        /// </summary>
        public bool OnConnectInvoked { get; private set; }

        /// <inheritdoc/>
        protected override Task OnConnect(Stream stream, bool leaveOpen, CancellationToken cancellationToken)
        {
            OnConnectInvoked = true;
            return Task.CompletedTask;
        }
    }
}

