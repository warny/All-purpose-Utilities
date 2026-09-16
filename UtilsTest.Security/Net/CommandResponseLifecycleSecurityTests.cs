using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Security.Net;

/// <summary>
/// Security-relevant lifecycle, resource-bound and error-confinement tests covering audit items
/// 31, 34, 42, 45, 48, 52, 53, 54, 58, and rounds 4 / P1-2 / P2-E (moved from
/// UtilsTest.Net.CommandResponseLifecycleTests). All tests use in-memory pipes rather than real TCP ports.
/// </summary>
[TestClass]
public class CommandResponseLifecycleSecurityTests
{
    private static readonly TimeSpan Timeout5 = TimeSpan.FromSeconds(5);

    // ──────────────────────────────────────────────────────────────
    // Infrastructure
    // ──────────────────────────────────────────────────────────────

    /// <summary>Records a thread-safe, timestamped chronology for network callback test failures.</summary>
    private sealed class TestTimeline
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly ConcurrentQueue<string> _events = new();

        /// <summary>Records an event with its elapsed time, thread, and task identifiers.</summary>
        /// <param name="eventName">Safe technical event text.</param>
        public void Record(string eventName)
        {
            _events.Enqueue(
                $"{_stopwatch.Elapsed.TotalMilliseconds,9:F3} ms " +
                $"Thread={Environment.CurrentManagedThreadId} Task={Task.CurrentId?.ToString() ?? "none"} {eventName}");
        }

        /// <summary>Determines whether the chronology contains an event fragment.</summary>
        /// <param name="value">Event fragment to find.</param>
        /// <returns><see langword="true"/> when an event contains the fragment.</returns>
        public bool ContainsEvent(string value) => Contains(_events, value);

        /// <summary>Builds a diagnostic snapshot suitable for GitHub Actions output.</summary>
        /// <param name="expectedEvent">Event for which the test was waiting.</param>
        /// <param name="client">Client whose last known connection state is reported.</param>
        /// <returns>A multiline chronology and state summary.</returns>
        public string BuildDiagnostic(string expectedEvent, CommandResponseClient client)
        {
            string[] events = _events.ToArray();
            return $"Diagnostic snapshot. Expected event: {expectedEvent}.{Environment.NewLine}" +
                $"Elapsed: {_stopwatch.Elapsed.TotalMilliseconds:F3} ms{Environment.NewLine}" +
                $"ConnectionState: {(client.IsConnected ? "Connected" : "NotConnected")}{Environment.NewLine}" +
                $"SessionFailure: {client.SessionFailure?.GetType().FullName ?? "none"}{Environment.NewLine}" +
                $"CallbackReceived: {Contains(events, "Callback frame received")}{Environment.NewLine}" +
                $"CallbackStarted: {Contains(events, "Callback dispatch started")}{Environment.NewLine}" +
                $"CallbackCompleted: {Contains(events, "Callback dispatch completed")}{Environment.NewLine}" +
                $"CallbackErrorPropagated: {Contains(events, "Callback error propagated")}{Environment.NewLine}" +
                $"PingSent: {Contains(events, "Request sent")}{Environment.NewLine}" +
                $"PingReceived: {Contains(events, "PING received by server")}{Environment.NewLine}" +
                $"PingCompleted: {Contains(events, "Request completed")}{Environment.NewLine}" +
                $"Observed events:{Environment.NewLine}{string.Join(Environment.NewLine, events.Select(value => "  " + value))}{Environment.NewLine}" +
                $"Missing event: {expectedEvent}";
        }

        /// <summary>Determines whether the captured chronology contains the requested event fragment.</summary>
        /// <param name="events">Captured event lines.</param>
        /// <param name="value">Event fragment to find.</param>
        /// <returns><see langword="true"/> when an event contains the fragment.</returns>
        private static bool Contains(IEnumerable<string> events, string value) =>
            events.Any(item => item.Contains(value, StringComparison.Ordinal));
    }

    /// <summary>Forwards structured client logs into a test chronology without external logging dependencies.</summary>
    private sealed class TimelineLogger : ILogger
    {
        private readonly TestTimeline _timeline;

        /// <summary>Initializes a logger that forwards safe structured diagnostics to a test chronology.</summary>
        /// <param name="timeline">Chronology that receives each formatted log entry.</param>
        public TimelineLogger(TestTimeline timeline) => _timeline = timeline;

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string exceptionDetail = exception is null
                ? string.Empty
                : $" ExceptionType={exception.GetType().FullName} StackTrace={exception.StackTrace ?? "unavailable"}";
            _timeline.Record($"Level={logLevel} {formatter(state, exception)}{exceptionDetail}");
        }
    }

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

    /// <summary>Awaits a task and reports the complete network chronology if it does not complete.</summary>
    /// <typeparam name="T">Task result type.</typeparam>
    /// <param name="task">Operation being observed.</param>
    /// <param name="expectedEvent">Event whose absence identifies the timeout stage.</param>
    /// <param name="timeline">Chronology populated by the test and client logger.</param>
    /// <param name="client">Client whose last known state is included in diagnostics.</param>
    /// <param name="timeout">Maximum wait used only as a deadlock guard.</param>
    /// <returns>The completed task result.</returns>
    private static async Task<T> WithDiagnosticTimeoutAsync<T>(
        Task<T> task,
        string expectedEvent,
        TestTimeline timeline,
        CommandResponseClient client,
        TimeSpan? timeout = null)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeout ?? Timeout5)).ConfigureAwait(false);
        if (completed != task)
        {
            Assert.Fail(
                $"Timed out waiting for {expectedEvent}.{Environment.NewLine}" +
                timeline.BuildDiagnostic(expectedEvent, client));
        }

        return await task.ConfigureAwait(false);
    }

    /// <summary>Awaits a task and reports the complete network chronology if it does not complete.</summary>
    /// <param name="task">Operation being observed.</param>
    /// <param name="expectedEvent">Event whose absence identifies the timeout stage.</param>
    /// <param name="timeline">Chronology populated by the test and client logger.</param>
    /// <param name="client">Client whose last known state is included in diagnostics.</param>
    /// <param name="timeout">Maximum wait used only as a deadlock guard.</param>
    private static async Task WithDiagnosticTimeoutAsync(
        Task task,
        string expectedEvent,
        TestTimeline timeline,
        CommandResponseClient client,
        TimeSpan? timeout = null)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeout ?? Timeout5)).ConfigureAwait(false);
        if (completed != task)
        {
            Assert.Fail(
                $"Timed out waiting for {expectedEvent}.{Environment.NewLine}" +
                timeline.BuildDiagnostic(expectedEvent, client));
        }

        await task.ConfigureAwait(false);
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

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            server.StartAsync(serverStream, leaveOpen: true));
    }

    [TestMethod]
    public async Task StartAsync_AfterStopped_Throws()
    {
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        CommandResponseServer server = new();
        await server.StartAsync(serverStream, leaveOpen: true);
        server.Dispose();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            server.StartAsync(serverStream, leaveOpen: true));
    }

    [TestMethod]
    public async Task StartAsync_AfterInitializationFailure_Throws()
    {
        using CommandResponseServer server = new();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            server.StartAsync(null!, leaveOpen: true));

        // A failed startup must leave the instance unusable (single-use contract).
        (DuplexStream serverStream, StreamWriter clientWriter, StreamReader clientReader) = CreateServerTestPair();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            server.StartAsync(serverStream, leaveOpen: true));
    }

    // ──────────────────────────────────────────────────────────────
    // Item 42 — Validate server resource-limit configuration arguments
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void MaxConsecutiveErrors_NegativeValue_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => server.MaxConsecutiveErrors = -1);
    }

    [TestMethod]
    public void MaxLineLength_NegativeValue_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => server.MaxLineLength = -1);
    }

    [TestMethod]
    public void MaxCommandQueueDepth_NegativeValue_Throws()
    {
        CommandResponseServer server = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => server.MaxCommandQueueDepth = -1);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 34 — Terminal fault cancels listener and faults Completion (fail-closed)
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
    // Item 45 — Unsolicited subscriber exceptions do not kill listener
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UnsolicitedResponseReceived_SubscriberThrows_ListenerSurvives()
    {
        TestTimeline timeline = new();
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new() { Logger = new TimelineLogger(timeline) };
        await client.ConnectAsync(clientStream, leaveOpen: true);

        TaskCompletionSource<Exception> callbackObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.CallbackError += ex =>
        {
            timeline.Record("CallbackError observed by test subscriber");
            callbackObserved.TrySetResult(ex);
        };
        client.UnsolicitedResponseReceived += _ =>
        {
            timeline.Record("Unsolicited subscriber invoked");
            throw new InvalidOperationException("Subscriber fault");
        };

        timeline.Record("Writing unsolicited response");
        await serverWriter.WriteLineAsync("220 Welcome");
        Exception callbackError = await WithDiagnosticTimeoutAsync(
            callbackObserved.Task,
            "CallbackError propagation",
            timeline,
            client);

        Assert.IsInstanceOfType<InvalidOperationException>(
            callbackError,
            timeline.BuildDiagnostic("InvalidOperationException propagated", client));
        Assert.IsTrue(
            client.IsConnected,
            "Client must remain connected after subscriber exception." + Environment.NewLine +
            timeline.BuildDiagnostic("connected client", client));

        timeline.Record("Starting PING request");
        Task<IReadOnlyList<ServerResponse>> sendTask = client.SendCommandAsync("PING");
        string? received = await WithDiagnosticTimeoutAsync(
            Task.Run(() => serverReader.ReadLine()),
            "PING reception by server",
            timeline,
            client);
        timeline.Record("PING received by server");
        await serverWriter.WriteLineAsync("250 OK");
        IReadOnlyList<ServerResponse> responses = await WithDiagnosticTimeoutAsync(
            sendTask,
            "PING request completion",
            timeline,
            client);
        timeline.Record("PING completed by client");
        Assert.AreEqual(
            "250",
            responses[0].Code,
            timeline.BuildDiagnostic("PING response code 250", client));
    }

    [TestMethod]
    public async Task UnsolicitedSubscriberThrows_ErrorIsObservable()
    {
        // P2-5: exceptions from unsolicited-response subscribers must be forwarded to
        // CallbackError so they are observable even when no logger is configured.
        TestTimeline timeline = new();
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using CommandResponseClient client = new() { Logger = new TimelineLogger(timeline) };
        await client.ConnectAsync(clientStream, leaveOpen: true);

        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        TaskCompletionSource callbackObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.CallbackError += ex =>
        {
            timeline.Record($"CallbackError observed by test subscriber; ExceptionType={ex.GetType().FullName}");
            errors.Add(ex);
            callbackObserved.TrySetResult();
        };
        client.UnsolicitedResponseReceived += _ =>
        {
            timeline.Record("Unsolicited subscriber invoked");
            throw new InvalidOperationException("Subscriber fault");
        };

        timeline.Record("Writing unsolicited response");
        await serverWriter.WriteLineAsync("220 Welcome");
        await WithDiagnosticTimeoutAsync(
            callbackObserved.Task,
            "CallbackError propagation",
            timeline,
            client);

        timeline.Record("Starting PING request");
        Task<IReadOnlyList<ServerResponse>> sendTask = client.SendCommandAsync("PING");
        string? receivedCommand = await WithDiagnosticTimeoutAsync(
            Task.Run(() => serverReader.ReadLine()),
            "PING reception by server",
            timeline,
            client);
        timeline.Record("PING received by server");
        Assert.AreEqual(
            "PING",
            receivedCommand,
            timeline.BuildDiagnostic("PING received by server", client));
        await serverWriter.WriteLineAsync("250 OK");
        IReadOnlyList<ServerResponse> responses = await WithDiagnosticTimeoutAsync(
            sendTask,
            "PING request completion",
            timeline,
            client);
        timeline.Record("PING completed by client");

        Assert.AreEqual(
            1,
            errors.Count,
            "CallbackError must fire once for the subscriber exception." + Environment.NewLine +
            timeline.BuildDiagnostic("exactly one CallbackError", client));
        Assert.IsInstanceOfType<InvalidOperationException>(
            errors.Single(),
            timeline.BuildDiagnostic("InvalidOperationException propagated", client));
        Assert.IsTrue(
            client.IsConnected,
            "Client must remain connected after the subscriber exception." + Environment.NewLine +
            timeline.BuildDiagnostic("connected client", client));
        Assert.AreEqual(
            "250",
            responses[0].Code,
            timeline.BuildDiagnostic("PING response code 250", client));
        Assert.IsTrue(
            timeline.ContainsEvent("Callback dispatch completed"),
            timeline.BuildDiagnostic("Callback dispatch completed", client));
        Assert.IsTrue(
            timeline.ContainsEvent("ConnectionId=") && timeline.ContainsEvent("CallbackId=1"),
            timeline.BuildDiagnostic("correlated connection and callback identifiers", client));
        Assert.IsTrue(
            timeline.ContainsEvent("Request completed") && timeline.ContainsEvent("RequestId=1"),
            timeline.BuildDiagnostic("correlated PING request completion", client));
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

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
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

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            client.SendLinesPublicAsync(["SAFE", "BAD\rINJECT"]));
    }

    [TestMethod]
    public async Task SendLinesAsync_LineWithLF_Throws()
    {
        (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) = CreateTestPair();
        using TestableClient client = new();
        await client.ConnectAsync(clientStream, leaveOpen: true);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
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
    // Item 58 — Validate resource-limit and injection-relevant configuration values
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void MaxLineLength_NegativeValue_ThrowsOnClient()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => client.MaxLineLength = -1);
    }

    [TestMethod]
    public void MaxResponseCount_NegativeValue_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => client.MaxResponseCount = -1);
    }

    [TestMethod]
    public void NoOpCommand_ContainsCr_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentException>(() => client.NoOpCommand = "NOOP\r");
    }

    [TestMethod]
    public void NoOpCommand_ContainsLf_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentException>(() => client.NoOpCommand = "NOOP\n");
    }

    [TestMethod]
    public void NoOpCommand_ContainsNul_Throws()
    {
        CommandResponseClient client = new();
        Assert.ThrowsExactly<ArgumentException>(() => client.NoOpCommand = "NOOP\0");
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
    // P1-2 (round 4) — ReadAsync must serialize with SendCommandAsync (response integrity)
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
}
