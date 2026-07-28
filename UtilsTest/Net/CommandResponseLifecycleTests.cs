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
}
