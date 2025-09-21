using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
                    ? new[]
                    {
                        new ServerResponse("100", ResponseSeverity.Preliminary, "Continue"),
                        new ServerResponse("200", ResponseSeverity.Completion, "Done")
                    }
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
    /// Verifies that unsolicited responses are raised when the expectation flag is disabled.
    /// </summary>
    [TestMethod]
    public async Task Client_RaisesUnsolicitedEventWhenExpectationDisabled()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        TaskCompletionSource serverCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream networkStream = serverClient.GetStream();
            using StreamWriter writer = new(networkStream, Encoding.ASCII, 1024, true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };
            await writer.WriteLineAsync("220 Ready");
            await serverCompletion.Task;
            listener.Stop();
        });

        using TestableCommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        TaskCompletionSource<ServerResponse> unsolicited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.UnsolicitedResponseReceived += response => unsolicited.TrySetResult(response);
        client.SetExpectation(0);
        await client.ConnectAsync("127.0.0.1", port);
        ServerResponse greeting = await unsolicited.Task;
        Assert.AreEqual("220", greeting.Code);
        client.SetExpectation(1);
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => client.ReadAsync(cts.Token));
        serverCompletion.SetResult();
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
                return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("200", ResponseSeverity.Completion, "OK") });
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
    /// Verifies that custom expectation modes can be used to process protocol specific payloads before the final response.
    /// </summary>
    [TestMethod]
    public async Task Client_HandlesCustomExpectationThroughOverride()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream networkStream = serverClient.GetStream();
            using StreamReader reader = new(networkStream, Encoding.ASCII, false, 1024, true);
            using StreamWriter writer = new(networkStream, Encoding.ASCII, 1024, true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };
            string? command = await reader.ReadLineAsync();
            if (string.Equals(command, "BLOCK", StringComparison.Ordinal))
            {
                await writer.WriteLineAsync("FIRST");
                await writer.WriteLineAsync("SECOND");
                await writer.WriteLineAsync("END");
                await writer.WriteLineAsync("200 OK");
            }
            await Task.Delay(50);
            serverClient.Close();
            listener.Stop();
        });

        IReadOnlyList<ServerResponse> responses;
        using TextCollectorCommandResponseClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        client.BeginCollecting();
        bool unsolicitedRaised = false;
        client.UnsolicitedResponseReceived += _ => unsolicitedRaised = true;
        responses = await client.SendCommandAsync("BLOCK");
        Assert.IsFalse(unsolicitedRaised, "Custom responses should not trigger the unsolicited handler when handled by the override.");
        Assert.AreEqual(2, client.CollectedLines.Count);
        Assert.AreEqual("FIRST", client.CollectedLines[0]);
        Assert.AreEqual("SECOND", client.CollectedLines[1]);
        Assert.AreEqual(1, responses.Count);
        Assert.AreEqual("200", responses[0].Code);
        Assert.AreEqual(ResponseSeverity.Completion, responses[0].Severity);
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
        Assert.IsTrue(client.IsConnected, "Client should be connected before requesting disconnect.");
        await client.DisconnectAsync("QUIT", TimeSpan.FromMilliseconds(500));
        Assert.IsFalse(client.IsConnected, "Client should disconnect after receiving a completion response.");
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
        Assert.IsTrue(client.IsConnected, "Client should be connected before forcing disconnect.");
        await client.DisconnectAsync("QUIT", TimeSpan.FromMilliseconds(100));
        Assert.IsFalse(client.IsConnected, "Client should forcefully disconnect when no response is returned.");
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
    /// Ensures that response lines are split into code and message segments.
    /// </summary>
    [TestMethod]
    public void SplitCodeAndMessage_SplitsCorrectly()
    {
        TestClient client = new();
        (string code, string? message) = client.Split("123 Example message");
        Assert.AreEqual("123", code);
        Assert.AreEqual("Example message", message);
        (code, message) = client.Split("LINE");
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
        public (string code, string? message) Split(string line) => SplitCodeAndMessage(line);
    }
}

/// <summary>
/// Test specific command/response client exposing the response expectation flag for verification purposes.
/// </summary>
internal sealed class TestableCommandResponseClient : CommandResponseClient
{
    /// <summary>
    /// Sets the expectation flag to the provided value.
    /// </summary>
    /// <param name="expectation">Expectation mode to apply.</param>
    public void SetExpectation(int expectation)
    {
        ResponseExpectation = expectation;
    }
}

/// <summary>
/// Command/response client used to collect custom payloads while a command is in progress.
/// </summary>
internal sealed class TextCollectorCommandResponseClient : CommandResponseClient
{
    private readonly List<string> _collectedLines = new();

    /// <summary>
    /// Gets the lines collected while handling custom responses.
    /// </summary>
    public IReadOnlyList<string> CollectedLines => _collectedLines;

    /// <summary>
    /// Clears previously collected lines and configures the expectation flag to capture custom payloads.
    /// </summary>
    public void BeginCollecting()
    {
        _collectedLines.Clear();
        ResponseExpectation = 2;
    }

    /// <summary>
    /// Handles custom response modes by storing unknown responses until the terminating marker is received.
    /// </summary>
    /// <param name="response">Response delivered by the server.</param>
    /// <param name="expectation">Current expectation flag value.</param>
    /// <returns><see langword="true"/> when the response is processed as part of the custom payload.</returns>
    protected override bool HandleCustomResponse(ServerResponse response, int expectation)
    {
        if (expectation == 2)
        {
            if (string.Equals(response.Code, "END", StringComparison.Ordinal))
            {
                ResponseExpectation = 1;
            }
            else
            {
                _collectedLines.Add(response.Code);
            }
            return true;
        }

        return base.HandleCustomResponse(response, expectation);
    }
}

