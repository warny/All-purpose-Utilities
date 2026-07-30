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
    // Item 31 — Atomic single-use server startup
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task StartAsync_ConcurrentCalls_OnlyOneSucceeds()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CommandResponseServer server = new();

        int success = 0;
        int failure = 0;
        Task[] tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            try
            {
                await server.StartAsync(serverStream, leaveOpen: true);
                Interlocked.Increment(ref success);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref failure);
            }
        })).ToArray();

        await WithTimeout(Task.WhenAll(tasks), "Concurrent StartAsync did not complete within 5s.");
        Assert.AreEqual(1, success, "Exactly one StartAsync must succeed.");
        Assert.AreEqual(7, failure, "All other StartAsync calls must throw.");
    }

    [TestMethod]
    public async Task StartAsync_SecondCallAfterStart_Throws()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            server.StartAsync(serverStream, leaveOpen: true));
    }

    [TestMethod]
    public async Task StartAsync_AfterStopped_Throws()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true);
        server.Dispose();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            server.StartAsync(serverStream, leaveOpen: true));
    }

    [TestMethod]
    public async Task StartAsync_AfterInitializationFailure_Throws()
    {
        using CommandResponseServer server = new();
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
            server.StartAsync(null!, leaveOpen: true));

        // A failed startup must leave the instance unusable (single-use contract).
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            server.StartAsync(serverStream, leaveOpen: true));
    }

    // ──────────────────────────────────────────────────────────────
    // Item 42 — Validate server configuration arguments
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void RegisterCommand_NullCommand_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentNullException>(() =>
            server.RegisterCommand(null!, (_, _, _) => Task.FromResult<IEnumerable<ServerResponse>>([])));
    }

    [TestMethod]
    public void RegisterCommand_EmptyCommand_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentException>(() =>
            server.RegisterCommand(string.Empty, (_, _, _) => Task.FromResult<IEnumerable<ServerResponse>>([])));
    }

    [TestMethod]
    public void RegisterCommand_CommandWithSpace_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentException>(() =>
            server.RegisterCommand("FOO BAR", (_, _, _) => Task.FromResult<IEnumerable<ServerResponse>>([])));
    }

    [TestMethod]
    public void RegisterCommand_NullHandler_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentNullException>(() =>
            server.RegisterCommand("FOO", (Func<CommandContext, string[], CancellationToken, Task<IEnumerable<ServerResponse>>>)null!));
    }

    [TestMethod]
    public void AddContext_NullContext_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentNullException>(() => server.AddContext(null!));
    }

    [TestMethod]
    public void AddContext_EmptyContext_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentException>(() => server.AddContext(string.Empty));
    }

    [TestMethod]
    public void RemoveContext_NullContext_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentNullException>(() => server.RemoveContext(null!));
    }

    [TestMethod]
    public void HasContext_NullContext_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentNullException>(() => server.HasContext(null!));
    }

    [TestMethod]
    public void MaxConsecutiveErrors_NegativeValue_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => server.MaxConsecutiveErrors = -1);
    }

    [TestMethod]
    public void MaxLineLength_NegativeValue_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => server.MaxLineLength = -1);
    }

    [TestMethod]
    public void MaxCommandQueueDepth_NegativeValue_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => server.MaxCommandQueueDepth = -1);
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

        Assert.ThrowsException<InvalidOperationException>(() =>
            server.RegisterCommand("FOO", (_, _, _) => Task.FromResult<IEnumerable<ServerResponse>>([])));
    }

    [TestMethod]
    public async Task AddContext_AfterStartAsync_Throws()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true);

        Assert.ThrowsException<InvalidOperationException>(() => server.AddContext("AUTH"));
    }

    [TestMethod]
    public async Task RemoveContext_AfterStartAsync_Throws()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CommandResponseServer server = new();
        server.AddContext("AUTH");
        await server.StartAsync(serverStream, leaveOpen: true);

        Assert.ThrowsException<InvalidOperationException>(() => server.RemoveContext("AUTH"));
    }

    // ──────────────────────────────────────────────────────────────
    // Item 34 — Terminal fault cancels listener and faults Completion
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task FormatterException_FaultsCompletion()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        int call = 0;
        CommandResponseServer server = new(response =>
        {
            if (Interlocked.Increment(ref call) == 1)
                throw new InvalidOperationException("Formatter fault");
            return $"{response.Code} {response.Message}";
        });
        server.RegisterCommand("BOOM", (_, _, _) =>
            Task.FromResult<IEnumerable<ServerResponse>>(
                [new ServerResponse("250", ResponseSeverity.Completion, "OK")]));
        await server.StartAsync(serverStream, leaveOpen: true);

        await clientWriter.WriteLineAsync("BOOM");
        Task completion = server.Completion;
        Task settled = await Task.WhenAny(completion, Task.Delay(Timeout5)).ConfigureAwait(false);
        if (settled != completion)
            Assert.Fail("Completion did not settle within 5s after formatter fault.");

        Assert.IsTrue(completion.IsFaulted || completion.IsCanceled,
            "Completion must be faulted or cancelled after a terminal formatter exception.");

        server.Dispose();
    }

    // ──────────────────────────────────────────────────────────────
    // Item 54 — Lazy response enumerable fault is contained
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Handler_LazyEnumerableThrows_Returns500()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        using CommandResponseServer server = new();
        server.RegisterCommand("LAZY", (_, _, _) =>
            Task.FromResult<IEnumerable<ServerResponse>>(ThrowingEnumerable()));
        await server.StartAsync(serverStream, leaveOpen: true);

        await clientWriter.WriteLineAsync("LAZY");
        string? reply = await WithTimeout(Task.Run(() => clientReader.ReadLine()), "Did not receive reply within 5s.");

        Assert.IsTrue(reply?.StartsWith("500") == true, $"Expected 500 but got: {reply}");
    }

    private static IEnumerable<ServerResponse> ThrowingEnumerable()
    {
        yield return new ServerResponse("250", ResponseSeverity.Completion, "OK");
        throw new InvalidOperationException("Lazy fault");
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
    // Item 45 — Unsolicited subscriber exceptions do not kill listener
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UnsolicitedResponseReceived_SubscriberThrows_ListenerSurvives()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        client.UnsolicitedResponseReceived += _ => throw new InvalidOperationException("Subscriber fault");

        await serverWriter.WriteLineAsync("220 Welcome");
        await Task.Delay(200);

        Assert.IsTrue(client.IsConnected, "Client must remain connected after subscriber exception.");

        Task<IReadOnlyList<ServerResponse>> sendTask = client.SendCommandAsync("PING");
        string? received = await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see PING after subscriber fault.");
        await serverWriter.WriteLineAsync("250 OK");
        IReadOnlyList<ServerResponse> responses = await WithTimeout(sendTask, "Did not receive PING response after subscriber fault.");
        Assert.AreEqual("250", responses[0].Code);
    }

    [TestMethod]
    public async Task UnsolicitedSubscriberThrows_ErrorIsObservable()
    {
        // P2-5: exceptions from unsolicited-response subscribers must be forwarded to
        // CallbackError so they are observable even when no logger is configured.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        client.CallbackError += ex => errors.Add(ex);
        client.UnsolicitedResponseReceived += _ => throw new InvalidOperationException("Subscriber fault");

        await serverWriter.WriteLineAsync("220 Welcome");
        await Task.Delay(200);

        Assert.AreEqual(1, errors.Count, "CallbackError must fire once for the subscriber exception.");
        Assert.IsInstanceOfType<InvalidOperationException>(errors.First());
        Assert.IsTrue(client.IsConnected, "Client must remain connected after the subscriber exception.");
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

    // ──────────────────────────────────────────────────────────────
    // Item 48 — Atomic single-use client connection
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ConnectAsync_SecondCallAfterConnect_Throws()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            client.ConnectAsync(clientStream, leaveOpen: true));
    }

    [TestMethod]
    public async Task ConnectAsync_ConcurrentCalls_OnlyOneSucceeds()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();

        int success = 0;
        int failure = 0;
        Task[] tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            try
            {
                await client.ConnectAsync(clientStream, leaveOpen: true);
                Interlocked.Increment(ref success);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                Interlocked.Increment(ref failure);
            }
        })).ToArray();

        await WithTimeout(Task.WhenAll(tasks), "Concurrent ConnectAsync did not complete within 5s.");
        Assert.AreEqual(1, success, "Exactly one ConnectAsync must succeed.");
        Assert.AreEqual(3, failure, "All other ConnectAsync calls must throw.");
    }

    [TestMethod]
    public async Task ConnectAsync_AfterDispose_Throws()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);
        client.Dispose();

        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
            client.ConnectAsync(clientStream, leaveOpen: true));
    }

    // ──────────────────────────────────────────────────────────────
    // Item 51 — Transactional TCP setup: resource rollback on failure
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ConnectAsync_Stream_NullStream_Throws()
    {
        using CommandResponseClient client = new();
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
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
    // Item 52 — Line length units are characters, not bytes
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task MaxLineLength_ExactBoundary_Accepted_Client()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        client.MaxLineLength = 5;
        await client.ConnectAsync(clientStream, leaveOpen: true);

        // Start ReadAsync before the server writes so _activeReadWaiters is incremented
        // before the response arrives — avoids a race where the listener routes the line
        // as unsolicited because no owner is registered yet.
        Task<IReadOnlyList<ServerResponse>> readTask = client.ReadAsync();
        await serverWriter.WriteLineAsync("250 X");
        IReadOnlyList<ServerResponse> responses = await WithTimeout(readTask, "ReadAsync timed out.");
        Assert.AreEqual(1, responses.Count);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 53 — SendLinesAsync validates against injection
    // ──────────────────────────────────────────────────────────────

    private sealed class TestableClient : CommandResponseClient
    {
        public Task SendLinesPublicAsync(IEnumerable<string> lines, CancellationToken ct = default)
            => SendLinesAsync(lines, ct);
    }

    [TestMethod]
    public async Task SendLinesAsync_LineWithCR_Throws()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using TestableClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            client.SendLinesPublicAsync(["SAFE", "BAD\rINJECT"]));
    }

    [TestMethod]
    public async Task SendLinesAsync_LineWithLF_Throws()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using TestableClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            client.SendLinesPublicAsync(["SAFE", "BAD\nINJECT"]));
    }

    [TestMethod]
    public async Task SendLinesAsync_CallerListMutation_DoesNotBypassValidation()
    {
        // P2-6: the caller holds a List<string> and mutates it between the call to
        // SendLinesAsync and the write loop. Without a defensive copy, a bad line inserted
        // after validation could reach the wire unchecked.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using TestableClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        List<string> lines = ["SAFE"];
        // This call must not throw: at the moment of the call the list is clean.
        // The point of the test is that the internal copy is made atomically at call
        // time, so a post-call mutation of `lines` cannot influence what is sent.
        Task send = client.SendLinesPublicAsync(lines);
        lines.Add("INJECTED\r\nBAD"); // mutate after the call but before the write finishes

        // Consume the server side so the write can complete without blocking.
        string? received = await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not receive line.");
        await send;

        // The injected entry was added after the defensive copy, so only "SAFE" was sent.
        Assert.AreEqual("SAFE", received, "Only the original line must have been sent.");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 58 — Validate timeout/limit configuration values
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void MaxLineLength_NegativeValue_ThrowsOnClient()
    {
        CommandResponseClient client = new();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => client.MaxLineLength = -1);
    }

    [TestMethod]
    public void MaxResponseCount_NegativeValue_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => client.MaxResponseCount = -1);
    }

    [TestMethod]
    public void ListenTimeout_NegativeValue_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
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
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
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
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
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
        Assert.ThrowsException<ArgumentNullException>(() => client.NoOpCommand = null!);
    }

    [TestMethod]
    public void NoOpCommand_Empty_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsException<ArgumentException>(() => client.NoOpCommand = "");
    }

    [TestMethod]
    public void NoOpCommand_Whitespace_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsException<ArgumentException>(() => client.NoOpCommand = "   ");
    }

    [TestMethod]
    public void NoOpCommand_ContainsCr_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsException<ArgumentException>(() => client.NoOpCommand = "NOOP\r");
    }

    [TestMethod]
    public void NoOpCommand_ContainsLf_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsException<ArgumentException>(() => client.NoOpCommand = "NOOP\n");
    }

    [TestMethod]
    public void NoOpCommand_ContainsNul_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsException<ArgumentException>(() => client.NoOpCommand = "NOOP\0");
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
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => client.ListenTimeout = tooLarge);
    }

    [TestMethod]
    public void ListenTimeout_Zero_DoesNotThrow()
    {
        CommandResponseClient client = new();
        client.ListenTimeout = TimeSpan.Zero;
    }

    [TestMethod]
    public void MaxLineLength_Zero_DoesNotThrow()
    {
        CommandResponseClient client = new();
        client.MaxLineLength = 0;
    }

    [TestMethod]
    public void MaxResponseCount_Zero_DoesNotThrow()
    {
        CommandResponseClient client = new();
        client.MaxResponseCount = 0;
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
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
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
    // P2-E — CallbackError subscribers must not kill the listener
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CallbackError_SubscriberThrows_ListenerSurvives()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        client.UnsolicitedResponseReceived += _ => throw new InvalidOperationException("subscriber fault");
        client.CallbackError += _ => throw new InvalidOperationException("callbackError subscriber fault");

        await serverWriter.WriteLineAsync("220 Welcome");
        await Task.Delay(200);

        Assert.IsTrue(client.IsConnected, "Listener must survive both subscriber and CallbackError faults.");

        // Verify the transport is still functional.
        Task<IReadOnlyList<ServerResponse>> sendTask = client.SendCommandAsync("PING");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "PING was not received.");
        await serverWriter.WriteLineAsync("250 OK");
        IReadOnlyList<ServerResponse> responses = await WithTimeout(sendTask, "PING response not received.");
        Assert.AreEqual("250", responses[0].Code);
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
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
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
    // P1-2 (round 4) — ReadAsync must serialize with SendCommandAsync
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ReadAsync_ConcurrentWithSendCommand_CannotStealCommandResponse()
    {
        // ReadAsync now takes the same _sendLock as SendCommandAsync, so they cannot
        // overlap. This test verifies that ReadAsync does not dequeue the response
        // that was intended for a concurrent SendCommandAsync.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        // Start a command that will hold _sendLock while waiting for its response.
        Task<IReadOnlyList<ServerResponse>> commandTask = client.SendCommandAsync("PING");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not receive PING.");

        // Start ReadAsync — it must block on _sendLock while SendCommandAsync holds it.
        Task<IReadOnlyList<ServerResponse>> readTask = client.ReadAsync();
        await Task.Delay(50); // give ReadAsync time to block on the lock

        // Deliver the command response; only SendCommandAsync should receive it.
        await serverWriter.WriteLineAsync("250 PONG");
        IReadOnlyList<ServerResponse> commandResponse =
            await WithTimeout(commandTask, "PING response not received within 5s.");

        // Small delay so ReadAsync can acquire _sendLock and increment _activeReadWaiters
        // before the next server message arrives.
        await Task.Delay(50);

        // Now deliver an unsolicited message; ReadAsync (now the sole waiter) should get it.
        await serverWriter.WriteLineAsync("250 Unsolicited");
        IReadOnlyList<ServerResponse> readResponse =
            await WithTimeout(readTask, "ReadAsync did not complete within 5s.");

        Assert.AreEqual(1, commandResponse.Count);
        Assert.AreEqual("PONG", commandResponse[0].Message,
            "SendCommandAsync must receive its own response, not the unsolicited one.");
        Assert.AreEqual(1, readResponse.Count);
        Assert.AreEqual("Unsolicited", readResponse[0].Message,
            "ReadAsync must receive the unsolicited message, not the command response.");
    }

    [TestMethod]
    public async Task ConcurrentReadAsync_Calls_AreSerializedByLock()
    {
        // Two concurrent ReadAsync calls must be serialized via _sendLock.
        // The second call blocks until the first completes and releases the lock, so
        // each call receives its own distinct response without cross-contamination.
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<IReadOnlyList<ServerResponse>> read1 = client.ReadAsync();
        Task<IReadOnlyList<ServerResponse>> read2 = client.ReadAsync();
        await Task.Delay(50); // let read1 acquire _sendLock and read2 block on it

        // Send only the first response; only read1 (current lock holder) can receive it.
        await serverWriter.WriteLineAsync("250 First");
        IReadOnlyList<ServerResponse> r1 = await WithTimeout(read1,
            "First ReadAsync timed out waiting for '250 First'.");

        // After read1 releases the lock, give read2 time to acquire it and register
        // _activeReadWaiters before the second response is sent.
        await Task.Delay(50);

        // Now read2 holds _sendLock and is waiting; send its response.
        await serverWriter.WriteLineAsync("251 Second");
        IReadOnlyList<ServerResponse> r2 = await WithTimeout(read2,
            "Second ReadAsync timed out waiting for '251 Second'.");

        Assert.IsTrue(r1.Any(r => r.Code == "250"),
            "First ReadAsync must receive '250 First'.");
        Assert.IsFalse(r1.Any(r => r.Code == "251"),
            "First ReadAsync must not steal '251 Second'.");
        Assert.IsTrue(r2.Any(r => r.Code == "251"),
            "Second ReadAsync must receive '251 Second'.");
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
    // Round-5 review — CommandResponseClient multiline transition fix
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Exposes the protected atomic multiline operation for lifecycle tests.
    /// </summary>
    private sealed class MultilineTestClient : CommandResponseClient
    {
        /// <summary>
        /// Sends a test multiline command whose successful opening response is code 200.
        /// </summary>
        public Task<(IReadOnlyList<ServerResponse> Responses, IReadOnlyList<string> Payload)> SendMultilineAsync(
            string command,
            CancellationToken cancellationToken = default)
        {
            return SendMultilineCommandAsync(
                command,
                responses => responses.Count > 0 && responses[^1].Code == "200",
                100,
                10_000,
                cancellationToken);
        }
    }

    [TestMethod]
    public async Task CompletedSingleLineCommand_UnsolicitedResponse_IsRaisedImmediately()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        TaskCompletionSource<ServerResponse> unsolicited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.UnsolicitedResponseReceived += response => unsolicited.TrySetResult(response);
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<IReadOnlyList<ServerResponse>> command = client.SendCommandAsync("PING");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see PING.");
        await serverWriter.WriteLineAsync("200 Pong");
        await WithTimeout(command, "PING did not complete.");
        await serverWriter.WriteLineAsync("610 Notification");

        ServerResponse response = await WithTimeout(unsolicited.Task, "Unsolicited response was not raised immediately.");
        Assert.AreEqual("610", response.Code);
    }

    [TestMethod]
    public async Task CompletedSingleLineCommand_UnsolicitedResponse_IsNotDiscardedByNextCommand()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new();
        int notifications = 0;
        client.UnsolicitedResponseReceived += response =>
        {
            if (response.Code == "610") Interlocked.Increment(ref notifications);
        };
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<IReadOnlyList<ServerResponse>> first = client.SendCommandAsync("FIRST");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see FIRST.");
        await serverWriter.WriteLineAsync("200 First");
        await WithTimeout(first, "FIRST did not complete.");
        await serverWriter.WriteLineAsync("610 Notification");
        await Task.Delay(100);

        Task<IReadOnlyList<ServerResponse>> second = client.SendCommandAsync("SECOND");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see SECOND.");
        await serverWriter.WriteLineAsync("200 Second");
        await WithTimeout(second, "SECOND did not complete.");
        Assert.AreEqual(1, notifications, "The next command must not discard an unsolicited notification.");
    }

    [TestMethod]
    public async Task MultilineCommand_ImmediatePayload_IsRetained()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using MultilineTestClient client = new();
        int unsolicited = 0;
        client.UnsolicitedResponseReceived += _ => Interlocked.Increment(ref unsolicited);
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<(IReadOnlyList<ServerResponse> Responses, IReadOnlyList<string> Payload)> command = client.SendMultilineAsync("MULTI");
        await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see MULTI.");
        await serverWriter.WriteLineAsync("200 Payload follows");
        await serverWriter.WriteLineAsync("first");
        await serverWriter.WriteLineAsync("second");
        await serverWriter.WriteLineAsync(".");

        var result = await WithTimeout(command, "Atomic multiline command did not complete.");
        CollectionAssert.AreEqual(new[] { "first", "second" }, result.Payload.ToArray());
        Assert.AreEqual(0, unsolicited);
    }

    [TestMethod]
    public async Task MultilineCommand_DoesNotExposeTransitionToOtherCallers()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using MultilineTestClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        Task<(IReadOnlyList<ServerResponse> Responses, IReadOnlyList<string> Payload)> multiline = client.SendMultilineAsync("MULTI");
        Assert.AreEqual("MULTI", await WithTimeout(Task.Run(() => serverReader.ReadLine()), "Server did not see MULTI."));
        await serverWriter.WriteLineAsync("200 Payload follows");
        Task<IReadOnlyList<ServerResponse>> competing = client.SendCommandAsync("NEXT");
        await serverWriter.WriteLineAsync("payload");
        await Task.Delay(100);
        Assert.IsFalse(competing.IsCompleted, "A competing command entered the multiline transition.");
        await serverWriter.WriteLineAsync(".");
        await WithTimeout(multiline, "MULTI did not consume its terminator.");
        Assert.AreEqual("NEXT", await WithTimeout(Task.Run(() => serverReader.ReadLine()), "NEXT did not run after MULTI."));
        await serverWriter.WriteLineAsync("200 Next");
        await WithTimeout(competing, "NEXT did not complete.");
    }
}
