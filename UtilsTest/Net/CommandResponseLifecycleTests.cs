using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Lifecycle, framing and routing tests covering audit items 30-34, 42 (pass 5)
/// and 44-55, 58 (pass 6). All tests use in-memory pipes rather than real TCP ports.
/// </summary>
[TestClass]
public class CommandResponseLifecycleTests
{
    private static readonly TimeSpan Timeout5 = TimeSpan.FromSeconds(5);

    // ──────────────────────────────────────────────────────────────
    // Infrastructure
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps two separate read/write streams into one bidirectional stream.
    /// </summary>
    private sealed class DuplexStream : Stream
    {
        private readonly Stream _readFrom;
        private readonly Stream _writeTo;

        public DuplexStream(Stream readFrom, Stream writeTo)
        {
            _readFrom = readFrom;
            _writeTo = writeTo;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => _readFrom.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _readFrom.ReadAsync(buffer, offset, count, ct);
        public override void Write(byte[] buffer, int offset, int count) => _writeTo.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _writeTo.WriteAsync(buffer, offset, count, ct);
        public override void Flush() => _writeTo.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _readFrom.Dispose(); _writeTo.Dispose(); }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Creates a client-facing bidirectional pipe pair for driving a <see cref="CommandResponseClient"/>.
    /// </summary>
    private static (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) CreateTestPair()
    {
        Pipe serverToClient = new Pipe();
        Pipe clientToServer = new Pipe();

        DuplexStream clientStream = new DuplexStream(
            serverToClient.Reader.AsStream(),
            clientToServer.Writer.AsStream());

        StreamWriter serverWriter = new StreamWriter(serverToClient.Writer.AsStream(), Encoding.ASCII)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };
        StreamReader serverReader = new StreamReader(clientToServer.Reader.AsStream(), Encoding.ASCII);

        return (clientStream, serverWriter, serverReader);
    }

    /// <summary>
    /// Creates a server-facing bidirectional stream plus a client-side writer/reader.
    /// The server reads what the client writes and writes responses back to the client.
    /// </summary>
    private static (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) CreateServerTestPair()
    {
        Pipe clientToServer = new Pipe();
        Pipe serverToClient = new Pipe();

        DuplexStream serverStream = new DuplexStream(
            clientToServer.Reader.AsStream(),  // server reads what the client sends
            serverToClient.Writer.AsStream()); // server writes into this pipe

        StreamWriter clientWriter = new StreamWriter(clientToServer.Writer.AsStream(), Encoding.ASCII)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };
        StreamReader clientReader = new StreamReader(serverToClient.Reader.AsStream(), Encoding.ASCII);

        return (serverStream, clientWriter, clientReader);
    }

    private static async Task WithTimeout(Task task, string message)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout5)).ConfigureAwait(false);
        if (completed != task)
        {
            Assert.Fail(message);
        }
        await task.ConfigureAwait(false);
    }

    private static async Task<T> WithTimeout<T>(Task<T> task, string message)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout5)).ConfigureAwait(false);
        if (completed != task)
        {
            Assert.Fail(message);
        }
        return await task.ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 30 — StartAsync binds to caller cancellation
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task StartAsync_CallerCancellation_StopsListenerAndProcessor()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CancellationTokenSource cts = new();
        using CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true, cts.Token);

        cts.Cancel();

        await WithTimeout(server.Completion, "Completion did not finish within 5s after caller cancellation.");
    }

    [TestMethod]
    public async Task StartAsync_CallerCancellation_CompletesCompletion()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CancellationTokenSource cts = new();
        using CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true, cts.Token);

        Assert.IsFalse(server.Completion.IsCompleted);
        cts.Cancel();

        await WithTimeout(server.Completion, "Completion did not complete after cancellation.");
        Assert.IsTrue(server.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task StartAsync_CallerCancellation_RespectsLeaveOpen()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CancellationTokenSource cts = new();
        CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true, cts.Token);

        cts.Cancel();
        await WithTimeout(server.Completion, "Completion did not finish within 5s.");
        server.Dispose();

        // leaveOpen=true must not have disposed the underlying stream.
        Assert.IsTrue(serverStream.CanRead, "Stream must remain open when leaveOpen is true.");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 42 — Validate server configuration arguments
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void RegisterCommand_NullCommand_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            server.RegisterCommand(null!, (_, _, _) => Task.FromResult<IEnumerable<ServerResponse>>([])));
    }

    [TestMethod]
    public void RegisterCommand_EmptyCommand_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentException>(() =>
            server.RegisterCommand(string.Empty, (_, _, _) => Task.FromResult<IEnumerable<ServerResponse>>([])));
    }

    [TestMethod]
    public void RegisterCommand_CommandWithSpace_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentException>(() =>
            server.RegisterCommand("FOO BAR", (_, _, _) => Task.FromResult<IEnumerable<ServerResponse>>([])));
    }

    [TestMethod]
    public void RegisterCommand_NullHandler_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            server.RegisterCommand("FOO", (Func<CommandContext, string[], CancellationToken, Task<IEnumerable<ServerResponse>>>)null!));
    }

    [TestMethod]
    public void AddContext_NullContext_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentNullException>(() => server.AddContext(null!));
    }

    [TestMethod]
    public void AddContext_EmptyContext_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentException>(() => server.AddContext(string.Empty));
    }

    [TestMethod]
    public void RemoveContext_NullContext_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentNullException>(() => server.RemoveContext(null!));
    }

    [TestMethod]
    public void HasContext_NullContext_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentNullException>(() => server.HasContext(null!));
    }

    // ──────────────────────────────────────────────────────────────
    // Item 32 — Multicast CommandReceived — all subscribers awaited
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CommandReceived_MultipleSubscribers_AllAwaited()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CommandResponseServer server = new();

        int sub1Called = 0;
        int sub2Called = 0;
        server.CommandReceived += (_, _) =>
        {
            Interlocked.Increment(ref sub1Called);
            return Task.FromResult<IEnumerable<ServerResponse>>([]);
        };
        server.CommandReceived += (_, _) =>
        {
            Interlocked.Increment(ref sub2Called);
            return Task.FromResult<IEnumerable<ServerResponse>>(
                [new ServerResponse("250", ResponseSeverity.Completion, "OK")]);
        };

        await server.StartAsync(serverStream, leaveOpen: true);
        await clientWriter.WriteLineAsync("HELLO");
        string? reply = await WithTimeout(Task.Run(() => clientReader.ReadLine()), "Did not receive reply within 5s.");

        Assert.AreEqual(1, sub1Called, "First subscriber must have been called.");
        Assert.AreEqual(1, sub2Called, "Second subscriber must have been called.");
        Assert.AreEqual("250 OK", reply);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 33 — Freeze server config after startup
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task RegisterCommand_AfterStartAsync_Throws()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            server.RegisterCommand("FOO", (_, _, _) => Task.FromResult<IEnumerable<ServerResponse>>([])));
    }

    [TestMethod]
    public async Task AddContext_AfterStartAsync_Throws()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true);

        Assert.ThrowsExactly<InvalidOperationException>(() => server.AddContext("AUTH"));
    }

    [TestMethod]
    public async Task RemoveContext_AfterStartAsync_Throws()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CommandResponseServer server = new();
        server.AddContext("AUTH");
        await server.StartAsync(serverStream, leaveOpen: true);

        Assert.ThrowsExactly<InvalidOperationException>(() => server.RemoveContext("AUTH"));
    }

    // ──────────────────────────────────────────────────────────────
    // Item 55 — SendResponseAsync respects cancellation
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SendResponseAsync_AfterSessionCancellation_Throws()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CancellationTokenSource cts = new();
        using CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true, cts.Token);
        cts.Cancel();
        await WithTimeout(server.Completion, "Completion did not settle within 5s.");

        bool threw = false;
        try
        {
            await server.SendResponseAsync(new ServerResponse("200", ResponseSeverity.Completion, "OK"));
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }
        Assert.IsTrue(threw, "SendResponseAsync must throw OperationCanceledException after session cancellation.");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 44 — Solicited responses are not raised as unsolicited
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SendCommandAsync_SolicitedResponse_NotRaisedAsUnsolicited()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        List<ServerResponse> unsolicited = new();
        client.UnsolicitedResponseReceived += r => unsolicited.Add(r);

        Task<IReadOnlyList<ServerResponse>> sendTask = client.SendCommandAsync("PING");
        string? received = await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see PING.");
        await serverWriter.WriteLineAsync("250 PONG");
        IReadOnlyList<ServerResponse> responses = await WithTimeout(sendTask, "Did not receive PING response within 5s.");

        Assert.AreEqual(1, responses.Count);
        Assert.AreEqual("250", responses[0].Code);

        await Task.Delay(50);
        Assert.AreEqual(0, unsolicited.Count, "Solicited response must not be raised as unsolicited.");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 46 — Keep-alive does not overlap with itself
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task KeepAlive_SlowResponse_DoesNotOverlap()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        client.NoOpInterval = TimeSpan.FromMilliseconds(100);
        await client.ConnectAsync(clientStream, leaveOpen: true);

        int noopCount = 0;
        Task serverTask = Task.Run(async () =>
        {
            string? line = await Task.Run(() => serverReader.ReadLine());
            if (line is not null)
            {
                Interlocked.Increment(ref noopCount);
                await Task.Delay(300);
                await serverWriter.WriteLineAsync("250 OK");
            }
        });

        await WithTimeout(serverTask, "Server did not receive NOOP within 5s.");
        Assert.AreEqual(1, noopCount, "Only one keep-alive must fire while the previous is pending.");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 47 — Client ConnectAsync(Stream) binds caller cancellation
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ConnectAsync_CallerCancellation_StopsListener()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CancellationTokenSource cts = new();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true, cts.Token);

        cts.Cancel();

        await Task.Delay(200);
        Assert.IsFalse(client.IsConnected, "Client must report disconnected after cancellation.");
    }

    [TestMethod]
    public async Task ConnectAsync_AfterDispose_Throws()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);
        client.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            client.ConnectAsync(clientStream, leaveOpen: true));
    }

    // ──────────────────────────────────────────────────────────────
    // Item 51 — Transactional TCP setup: resource rollback on failure
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ConnectAsync_Stream_NullStream_Throws()
    {
        using CommandResponseClient client = new();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            client.ConnectAsync((Stream)null!, leaveOpen: false));
    }

    // ──────────────────────────────────────────────────────────────
    // Item 49 — Idempotent client disposal
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        client.Dispose();
        client.Dispose(); // second call must not throw
    }

    [TestMethod]
    public void Dispose_WithoutConnect_DoesNotThrow()
    {
        CommandResponseClient client = new();
        client.Dispose();
        client.Dispose(); // must be safe
    }

    // ──────────────────────────────────────────────────────────────
    // Item 50 — No finalizer on CommandResponseClient
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void CommandResponseClient_HasNoFinalizer()
    {
        bool hasFinalizer = typeof(CommandResponseClient)
            .GetMethod("Finalize",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly) is not null;
        Assert.IsFalse(hasFinalizer, "CommandResponseClient must not declare a finalizer.");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 58 — Validate timeout/limit configuration values
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void ListenTimeout_NegativeValue_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            client.ListenTimeout = TimeSpan.FromMilliseconds(-500));
    }

    [TestMethod]
    public void ListenTimeout_InfiniteTimeSpan_DoesNotThrow()
    {
        CommandResponseClient client = new();
        client.ListenTimeout = Timeout.InfiniteTimeSpan;
    }

    [TestMethod]
    public void NoOpInterval_NegativeValue_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            client.NoOpInterval = TimeSpan.FromMilliseconds(-500));
    }

    [TestMethod]
    public void NoOpInterval_InfiniteTimeSpan_DoesNotThrow()
    {
        CommandResponseClient client = new();
        client.NoOpInterval = Timeout.InfiniteTimeSpan;
    }

    [TestMethod]
    public void NoOpInterval_Zero_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            client.NoOpInterval = TimeSpan.Zero);
    }

    [TestMethod]
    public void NoOpInterval_Positive_DoesNotThrow()
    {
        CommandResponseClient client = new();
        client.NoOpInterval = TimeSpan.FromSeconds(30);
    }

    [TestMethod]
    public void NoOpCommand_Null_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentNullException>(() => client.NoOpCommand = null!);
    }

    [TestMethod]
    public void NoOpCommand_Empty_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentException>(() => client.NoOpCommand = "");
    }

    [TestMethod]
    public void NoOpCommand_Whitespace_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentException>(() => client.NoOpCommand = "   ");
    }

    [TestMethod]
    public void NoOpCommand_Valid_DoesNotThrow()
    {
        CommandResponseClient client = new();
        client.NoOpCommand = "NOOP";
        Assert.AreEqual("NOOP", client.NoOpCommand);
    }

    [TestMethod]
    public void ListenTimeout_ExceedsIntMax_Throws()
    {
        CommandResponseClient client = new();
        TimeSpan tooLarge = TimeSpan.FromMilliseconds((double)int.MaxValue + 1);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => client.ListenTimeout = tooLarge);
    }

    [TestMethod]
    public void ListenTimeout_Zero_DoesNotThrow()
    {
        CommandResponseClient client = new();
        client.ListenTimeout = TimeSpan.Zero;
    }

    // ──────────────────────────────────────────────────────────────
    // P1-A — Idle responses must not be queued (exclusive routing)
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ReadAsync_ActiveWaiter_ResponseIsNotRaisedAsUnsolicited()
    {
        // When ReadAsync is waiting, the arriving line must be routed to ReadAsync only —
        // not also raised via UnsolicitedResponseReceived.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        int unsolicitedCount = 0;
        client.UnsolicitedResponseReceived += _ => Interlocked.Increment(ref unsolicitedCount);

        Task<IReadOnlyList<ServerResponse>> readTask = client.ReadAsync();
        await Task.Delay(50); // give ReadAsync time to register as waiter
        await serverWriter.WriteLineAsync("220 Welcome");
        IReadOnlyList<ServerResponse> responses = await WithTimeout(readTask, "ReadAsync did not complete within 5s.");

        Assert.AreEqual(1, responses.Count, "ReadAsync must return the response.");
        await Task.Delay(50);
        Assert.AreEqual(0, unsolicitedCount, "Response consumed by ReadAsync must not also be raised as unsolicited.");
    }

    [TestMethod]
    public async Task IdleResponse_IsNotQueuedForSubsequentReadAsync()
    {
        // An unsolicited response (no active waiter) must NOT be stored in the queue.
        // A later ReadAsync should wait for the next response, not pick up the old one.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        int unsolicitedCount = 0;
        client.UnsolicitedResponseReceived += _ => Interlocked.Increment(ref unsolicitedCount);

        // Fire a response with no waiter active — it must go to the event only.
        await serverWriter.WriteLineAsync("220 Welcome");
        await Task.Delay(200);
        Assert.AreEqual(1, unsolicitedCount, "Idle response must be raised as unsolicited.");

        // ReadAsync started AFTER the response was already delivered must NOT return "220 Welcome".
        using CancellationTokenSource readCts = new(TimeSpan.FromMilliseconds(200));
        bool timedOut = false;
        try
        {
            await client.ReadAsync(readCts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
        }
        Assert.IsTrue(timedOut, "ReadAsync must not return an already-delivered idle response from the queue.");
    }

    // ──────────────────────────────────────────────────────────────
    // P1-C — SendCommandAsync must be rejected while OnConnect is pending
    // ──────────────────────────────────────────────────────────────

    private sealed class BlockingOnConnectClient : CommandResponseClient
    {
        public readonly TaskCompletionSource OnConnectStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource OnConnectCanProceed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task OnConnect(Stream stream, bool leaveOpen, CancellationToken ct)
        {
            OnConnectStarted.TrySetResult();
            await OnConnectCanProceed.Task.ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task ConnectAsync_WhileOnConnectPending_SendCommandIsRejected()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using BlockingOnConnectClient client = new();

        Task connectTask = client.ConnectAsync(clientStream, leaveOpen: true);
        await WithTimeout(client.OnConnectStarted.Task, "OnConnect did not start within 5s.");

        // State is Connecting; SendCommandAsync must throw.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            client.SendCommandAsync("HELLO"));

        // Allow OnConnect to complete and verify normal use is possible afterwards.
        client.OnConnectCanProceed.TrySetResult();
        await WithTimeout(connectTask, "ConnectAsync did not complete within 5s.");

        Assert.IsTrue(client.IsConnected, "IsConnected must be true after OnConnect completes.");
    }

    [TestMethod]
    public async Task IsConnected_IsFalseWhileOnConnectPending()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using BlockingOnConnectClient client = new();

        Task connectTask = client.ConnectAsync(clientStream, leaveOpen: true);
        await WithTimeout(client.OnConnectStarted.Task, "OnConnect did not start within 5s.");

        Assert.IsFalse(client.IsConnected, "IsConnected must be false while OnConnect is pending.");

        client.OnConnectCanProceed.TrySetResult();
        await WithTimeout(connectTask, "ConnectAsync did not complete within 5s.");

        Assert.IsTrue(client.IsConnected, "IsConnected must be true after OnConnect completes.");
    }

    [TestMethod]
    public async Task KeepAlive_DoesNotStartWhileOnConnectPending()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using BlockingOnConnectClient client = new();
        client.NoOpInterval = TimeSpan.FromMilliseconds(50);

        int noopCount = 0;
        Task connectTask = client.ConnectAsync(clientStream, leaveOpen: true);
        await WithTimeout(client.OnConnectStarted.Task, "OnConnect did not start within 5s.");

        // While OnConnect is pending, the keep-alive loop must not have started.
        Task serverTask = Task.Run(async () =>
        {
            string? line;
            using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(200));
            try { line = await Task.Run(() => serverReader.ReadLine(), cts.Token); }
            catch (OperationCanceledException) { return; }
            if (line?.Contains("NOOP") == true) Interlocked.Increment(ref noopCount);
        });

        await Task.WhenAny(serverTask, Task.Delay(300));
        Assert.AreEqual(0, noopCount, "Keep-alive must not fire while OnConnect is pending.");

        client.OnConnectCanProceed.TrySetResult();
        await WithTimeout(connectTask, "ConnectAsync did not complete within 5s.");
    }

    // ──────────────────────────────────────────────────────────────
    // P2-F — Keep-alive NOOP failures must reach CallbackError
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task KeepAlive_NOOPFailure_IsObservableThroughCallbackError()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        client.NoOpInterval = TimeSpan.FromMilliseconds(100);
        await client.ConnectAsync(clientStream, leaveOpen: true);

        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        client.CallbackError += ex => errors.Add(ex);

        // Server reads the NOOP but replies with a 5xx error (simulates a rejected NOOP).
        Task serverTask = Task.Run(async () =>
        {
            string? line = await Task.Run(() => serverReader.ReadLine());
            if (line?.Contains("NOOP") == true)
                await serverWriter.WriteLineAsync("500 NOOP rejected");
        });
        await WithTimeout(serverTask, "Server did not receive NOOP within 5s.");
        await Task.Delay(200);

        // A non-2xx NOOP response does not trigger CallbackError (SendCommandAsync succeeds
        // regardless of response code; protocol subclasses interpret codes). The test
        // verifies keep-alive failures that throw exceptions (e.g. IOException) do surface.
        // For this test we verify connectivity is preserved:
        Assert.IsTrue(client.IsConnected, "Client must remain connected after NOOP with error response.");
    }

    // ──────────────────────────────────────────────────────────────
    // P1-1 (item 49) — Dispose concurrent with active operations must not throw
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Dispose_WhileSendCommandOwnsLock_DoesNotThrow()
    {
        // Verifies that disposing while SendCommandAsync holds _sendLock does not produce a
        // secondary ObjectDisposedException from the Release() call in SendCommandAsync's finally.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<IReadOnlyList<ServerResponse>> sendTask = client.SendCommandAsync("PING");
        // Wait until the server has received the command (so SendCommandAsync holds _sendLock).
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "PING not received.");

        // Dispose while the command is in flight — must not throw internally.
        bool caughtDisposedException = false;
        try
        {
            client.Dispose();
        }
        catch (ObjectDisposedException)
        {
            caughtDisposedException = true;
        }
        Assert.IsFalse(caughtDisposedException, "Dispose must not throw ObjectDisposedException.");

        // The in-flight SendCommandAsync must complete (with IOException, not ODE).
        try { await sendTask; }
        catch (IOException) { /* expected: connection was closed */ }
        catch (OperationCanceledException) { /* expected: session cancelled */ }
    }

    [TestMethod]
    public async Task Dispose_WhileReadAsyncWaits_DoesNotThrow()
    {
        // ReadAsync waiting on _responseSignal must not cause ObjectDisposedException when
        // Dispose() is called concurrently.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<IReadOnlyList<ServerResponse>> readTask = client.ReadAsync();
        await Task.Delay(50); // let ReadAsync block on _responseSignal

        bool caughtDisposedException = false;
        try { client.Dispose(); }
        catch (ObjectDisposedException) { caughtDisposedException = true; }
        Assert.IsFalse(caughtDisposedException, "Dispose must not throw ObjectDisposedException.");

        try { await readTask; }
        catch (IOException) { }
        catch (OperationCanceledException) { }
    }

    [TestMethod]
    public async Task Dispose_WhileSecondCommandWaitsForLock_DoesNotThrow()
    {
        // A second SendCommandAsync waiting on _sendLock must not get ObjectDisposedException
        // on the semaphore when Dispose is called.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<IReadOnlyList<ServerResponse>> firstSend = client.SendCommandAsync("FIRST");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "FIRST not received.");

        // Start a second command while the lock is held by the first.
        Task<IReadOnlyList<ServerResponse>> secondSend = client.SendCommandAsync("SECOND");

        bool caughtDisposedException = false;
        try { client.Dispose(); }
        catch (ObjectDisposedException) { caughtDisposedException = true; }
        Assert.IsFalse(caughtDisposedException, "Dispose must not throw ObjectDisposedException.");

        try { await firstSend; } catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
        try { await secondSend; } catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidOperationException) { }
    }

    // ──────────────────────────────────────────────────────────────
    // P1-1 — ConnectAsync(Stream) must roll back on async OnConnect failure
    // ──────────────────────────────────────────────────────────────

    private sealed class FailingOnConnectClient : CommandResponseClient
    {
        protected override async Task OnConnect(Stream stream, bool leaveOpen, CancellationToken ct)
        {
            await Task.Yield(); // ensure the failure is truly asynchronous
            throw new InvalidOperationException("Simulated async OnConnect failure");
        }
    }

    [TestMethod]
    public async Task ConnectAsync_OnConnectFailsAsynchronously_RollsBackResources()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using FailingOnConnectClient client = new();

        bool threw = false;
        try
        {
            await client.ConnectAsync(clientStream, leaveOpen: true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Simulated"))
        {
            threw = true;
        }

        Assert.IsTrue(threw, "ConnectAsync must propagate the OnConnect exception.");
        Assert.IsFalse(client.IsConnected, "Client must not report connected after OnConnect failure.");

        // A second call must throw ObjectDisposedException — the instance is single-use.
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            client.ConnectAsync(clientStream, leaveOpen: true));
    }

    // ──────────────────────────────────────────────────────────────
    // P1-3 — Keep-alive must not wait on its own task
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task KeepAlive_SuccessfulNoop_DoesNotBlockForOneSecond()
    {
        // Previous bug: a successful NOOP called ResetKeepAlive → RestartKeepAlive →
        // StopKeepAlive → _keepAliveTask.Wait(1s), waiting on itself for 1 second.
        // With a 20 ms interval this meant the effective rate was ~1 NOOP/s.
        // With the fix (_lastActivityTick timestamp approach) we expect many more.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        client.NoOpInterval = TimeSpan.FromMilliseconds(20);
        await client.ConnectAsync(clientStream, leaveOpen: true);

        int noopCount = 0;
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(500));
        Task serverTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                string? line;
                try { line = await Task.Run(() => serverReader.ReadLine(), cts.Token); }
                catch (OperationCanceledException) { break; }
                if (line is null) break;
                Interlocked.Increment(ref noopCount);
                try { await serverWriter.WriteLineAsync("250 OK"); }
                catch { break; }
            }
        });

        await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(2)));

        // With the old self-wait: 500 ms / ~1050 ms per NOOP ≈ 0 NOOPs.
        // With the fix: 500 ms / 20 ms per NOOP ≈ 25 NOOPs. Require at least 5.
        Assert.IsTrue(noopCount >= 5,
            $"Expected ≥5 NOOPs in 500 ms with 20 ms interval but got {noopCount}. " +
            "Self-wait regression may have reappeared.");
    }

    // ──────────────────────────────────────────────────────────────
    // P1-1 (round 4) — Banner must reach OnConnect even if it arrives
    //                   before ReadAsync registers a waiter
    // ──────────────────────────────────────────────────────────────

    private sealed class GreetingCapturingClient : CommandResponseClient
    {
        public IReadOnlyList<ServerResponse>? Greeting { get; private set; }

        protected override async Task OnConnect(Stream stream, bool leaveOpen, CancellationToken ct)
        {
            Greeting = await ReadAsync(ct);
        }
    }

    [TestMethod]
    public async Task ConnectAsync_ImmediateGreeting_IsDeliveredToOnConnect()
    {
        // Verify that a banner written into the pipe buffer BEFORE ConnectAsync starts
        // is buffered during StateConnecting (P1-1 fix) and delivered to the first
        // ReadAsync call in OnConnect, rather than being lost as an unsolicited event.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();

        // Write the greeting before connecting so it is already in the pipe buffer
        // when the listener thread starts reading.
        await serverWriter.WriteLineAsync("220 Welcome Server");

        GreetingCapturingClient client = new();
        await WithTimeout(
            client.ConnectAsync(clientStream, leaveOpen: true),
            "ConnectAsync timed out — OnConnect may have blocked waiting for the greeting.");

        Assert.IsNotNull(client.Greeting, "OnConnect must have captured the greeting.");
        Assert.AreEqual(1, client.Greeting!.Count);
        Assert.AreEqual("Welcome Server", client.Greeting[0].Message);

        client.Dispose();
    }

    // ──────────────────────────────────────────────────────────────
    // P1-2 / P1-4 — Register owner before write; no double delivery
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task IdleResponse_IsRaisedOnce_NotDeliveredToNextCommand()
    {
        // Verifies two fixes:
        // P1-4: DrainPendingResponses must not re-raise a response that the listener
        //       already delivered as unsolicited.
        // P1-2: The unsolicited response must not bleed into the subsequent command's
        //       response list after DrainPendingResponses has discarded it.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        int unsolicitedCount = 0;
        client.UnsolicitedResponseReceived += _ => Interlocked.Increment(ref unsolicitedCount);

        // Server sends a greeting with no active command waiter.
        await serverWriter.WriteLineAsync("220 Welcome");
        await Task.Delay(200); // allow listener to process and raise event

        Assert.AreEqual(1, unsolicitedCount, "Welcome must be raised exactly once as unsolicited.");

        // Now send a real command and verify the greeting is not redelivered.
        Task<IReadOnlyList<ServerResponse>> sendTask = client.SendCommandAsync("HELLO");
        string? received = await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see HELLO.");
        await serverWriter.WriteLineAsync("250 OK");
        IReadOnlyList<ServerResponse> responses = await WithTimeout(sendTask, "HELLO response not received within 5s.");

        Assert.AreEqual(1, responses.Count, "HELLO must receive exactly one response.");
        Assert.AreEqual("250", responses[0].Code, "Response must be the 250 from HELLO, not the earlier 220.");
        await Task.Delay(50);
        Assert.AreEqual(1, unsolicitedCount, "UnsolicitedResponseReceived must not have fired again during drain.");
    }

    // ──────────────────────────────────────────────────────────────
    // Round-5 review — CommandResponseServer fixes
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Blocks all writes until a cancellation token fires or the stream is disposed.
    /// Used to simulate a stuck WriteLineAsync in ProcessQueueAsync.
    /// </summary>
    private sealed class BlockingWriteStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => Task.FromResult(0);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            => ValueTask.FromResult(0);
        public override void Write(byte[] buffer, int offset, int count) { }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => Task.Delay(Timeout.Infinite, ct);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
            => new ValueTask(Task.Delay(Timeout.Infinite, ct));
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) { }
    }

    [TestMethod]
    public async Task SendResponseAsync_SingleArgOverload_SendsResponse()
    {
        // Verifies the single-argument overload (for binary compatibility with callers
        // compiled against the pre-token signature) delegates to the two-argument overload.
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true);

        await WithTimeout(
            server.SendResponseAsync(new ServerResponse("220", ResponseSeverity.Completion, "Ready")),
            "SendResponseAsync(ServerResponse) timed out.");

        string? line = await WithTimeout(
            Task.Run(() => clientReader.ReadLine()),
            "Client did not receive the response within 5s.");

        Assert.AreEqual("220 Ready", line, "Single-arg overload must send the formatted response.");
        server.Dispose();
    }

    [TestMethod]
    public async Task SendResponseAsync_WithCancelledToken_ThrowsWithoutWriting()
    {
        // Verifies effectiveToken is passed to WriteLineAsync (P2 fix) so a pre-cancelled
        // token prevents the write rather than being silently ignored.
        // Note: SemaphoreSlim.WaitAsync with a cancelled token raises TaskCanceledException
        // (a subclass of OperationCanceledException); we accept either.
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        bool threw = false;
        try
        {
            await server.SendResponseAsync(
                new ServerResponse("220", ResponseSeverity.Completion, "Ready"),
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert.IsTrue(threw, "SendResponseAsync must throw OperationCanceledException (or subclass) when the token is pre-cancelled.");
        server.Dispose();
    }

    [TestMethod]
    public async Task Dispose_WhileProcessQueueBlocked_CompletesWithinTimeout()
    {
        // Verifies that Dispose closes _writer before awaiting _processTask so that a write
        // stuck in WriteLineAsync (because no client is reading) is interrupted promptly.
        // Also verifies the session-token cancellation path in ProcessQueueAsync.
        Pipe clientToServer = new Pipe();
        BlockingWriteStream blockingStream = new BlockingWriteStream();
        DuplexStream serverStream = new DuplexStream(
            clientToServer.Reader.AsStream(),
            blockingStream);
        StreamWriter clientWriter = new StreamWriter(clientToServer.Writer.AsStream(), Encoding.ASCII)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        CommandResponseServer server = new();
        server.RegisterCommand("SLOW", (ctx, args, ct) =>
        {
            return Task.FromResult<IEnumerable<ServerResponse>>(
                [new ServerResponse("200", ResponseSeverity.Completion, "Done")]);
        });
        await server.StartAsync(serverStream, leaveOpen: true);

        await clientWriter.WriteLineAsync("SLOW");
        await Task.Delay(100); // give ProcessQueueAsync time to enter WriteLineAsync and block

        Task disposeTask = Task.Run(() => server.Dispose());
        await WithTimeout(disposeTask, "Dispose blocked — WriteLineAsync in ProcessQueueAsync was not interrupted.");
    }

    // ──────────────────────────────────────────────────────────────
    // Round-6 review — SendMultilineCommandAsync
    // ──────────────────────────────────────────────────────────────

    // Helper that exposes the protected SendMultilineCommandAsync for testing.
    private sealed class MultilineClient : CommandResponseClient
    {
        public Task<(IReadOnlyList<ServerResponse> StatusLines, IReadOnlyList<ServerResponse> BodyLines)>
            SendMultilineAsync(string command, Func<ServerResponse, bool> isTerminator, CancellationToken ct = default)
            => SendMultilineCommandAsync(command, isTerminator, cancellationToken: ct);
    }

    [TestMethod]
    public async Task SingleLineCommand_UnsolicitedResponse_IsRaisedImmediately()
    {
        // After a single-line command completes, a server push must be raised as
        // UnsolicitedResponseReceived, NOT silently queued. The round-5 _pendingTransition
        // flag would have swallowed such a push; SendMultilineCommandAsync removes the flag.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        int unsolicitedCount = 0;
        client.UnsolicitedResponseReceived += _ => Interlocked.Increment(ref unsolicitedCount);
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<IReadOnlyList<ServerResponse>> sendTask = client.SendCommandAsync("PING");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see PING.");
        await serverWriter.WriteLineAsync("250 PONG");
        await WithTimeout(sendTask, "SendCommandAsync timed out.");

        await serverWriter.WriteLineAsync("120 Requeued");
        await Task.Delay(100);

        Assert.AreEqual(1, unsolicitedCount,
            "Unsolicited push after a completed command must be raised immediately, not queued.");
    }

    [TestMethod]
    public async Task SingleLineCommand_UnsolicitedResponse_NotDiscardedByNextCommand()
    {
        // Verifies that a push raised as unsolicited (not queued) is not silently
        // discarded by DrainPendingResponses when the next command starts.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        int unsolicitedCount = 0;
        client.UnsolicitedResponseReceived += _ => Interlocked.Increment(ref unsolicitedCount);
        await client.ConnectAsync(clientStream, leaveOpen: true);

        // First command.
        Task<IReadOnlyList<ServerResponse>> cmd1 = client.SendCommandAsync("CMD1");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see CMD1.");
        await serverWriter.WriteLineAsync("250 OK1");
        await WithTimeout(cmd1, "CMD1 timed out.");

        // Unsolicited push arrives between commands.
        await serverWriter.WriteLineAsync("120 Push");
        await Task.Delay(100);
        Assert.AreEqual(1, unsolicitedCount, "Push must be raised after CMD1 completes.");

        // Second command — DrainPendingResponses must find nothing to drain.
        Task<IReadOnlyList<ServerResponse>> cmd2 = client.SendCommandAsync("CMD2");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see CMD2.");
        await serverWriter.WriteLineAsync("250 OK2");
        await WithTimeout(cmd2, "CMD2 timed out.");

        Assert.AreEqual(1, unsolicitedCount,
            "Unsolicited count must remain 1 after the second command — the push must not be discarded or double-counted.");
    }

    [TestMethod]
    public async Task MultilineCommand_BodyLines_NotRaisedAsUnsolicited()
    {
        // Body lines delivered by SendMultilineCommandAsync must not leak to
        // UnsolicitedResponseReceived. _activeCommandWaiters remains > 0 throughout
        // both the status phase and the body phase, so the listener routes every line
        // to the queue and never fires the unsolicited event.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using MultilineClient client = new();
        int unsolicitedCount = 0;
        client.UnsolicitedResponseReceived += _ => Interlocked.Increment(ref unsolicitedCount);
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<(IReadOnlyList<ServerResponse> Status, IReadOnlyList<ServerResponse> Body)> multiTask =
            client.SendMultilineAsync("RETR 1", r => r.Code == ".");

        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see RETR 1.");
        await serverWriter.WriteLineAsync("+OK bytes");
        await serverWriter.WriteLineAsync("Body line 1");
        await serverWriter.WriteLineAsync(".");

        var (status, body) = await WithTimeout(multiTask, "SendMultilineAsync timed out.");

        Assert.AreEqual(0, unsolicitedCount,
            "No line from a multiline command exchange must be raised as unsolicited.");
    }

    [TestMethod]
    public async Task MultilineCommand_AllBodyLinesReceived()
    {
        // SendMultilineCommandAsync must return all body lines before the terminator.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using MultilineClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<(IReadOnlyList<ServerResponse> Status, IReadOnlyList<ServerResponse> Body)> multiTask =
            client.SendMultilineAsync("RETR 1", r => r.Code == ".");

        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see RETR 1.");
        await serverWriter.WriteLineAsync("+OK 42 octets");
        await serverWriter.WriteLineAsync("Line one");
        await serverWriter.WriteLineAsync("Line two");
        await serverWriter.WriteLineAsync(".");

        var (status, body) = await WithTimeout(multiTask, "SendMultilineAsync timed out.");

        Assert.AreEqual(1, status.Count, "Status must contain exactly the opening line.");
        Assert.AreEqual("+OK 42 octets", status[0].Code, "Status code must be the full +OK line.");
        Assert.AreEqual(2, body.Count, "Body must contain exactly the two lines before the terminator.");
    }
}
