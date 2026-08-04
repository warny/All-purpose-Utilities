using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Utils.Net;

/// <summary>
/// Provides a base client for text based command/response protocols.
/// </summary>
public class CommandResponseClient : IDisposable
{
    private TcpClient? _client;
    private Stream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly ConcurrentQueue<ServerResponse> _responseQueue = new();
    private readonly SemaphoreSlim _responseSignal = new(0);
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Item 47: store the linked cancellation source so caller cancellation stops the listener.
    private CancellationTokenSource? _listenTokenSource;
    private Thread? _listenThread;

    // Item 46: keep-alive runs as a cancellable Task instead of an async-void Timer callback.
    private CancellationTokenSource? _keepAliveCts;
    private Task? _keepAliveTask;
    // P1-3 fix: track last activity as a tick count so ResetKeepAlive only updates a timestamp
    // rather than restarting the loop — which caused the loop to synchronously wait on itself
    // via StopKeepAlive() when called from within SendCommandAsync.
    private long _lastActivityTick;

    private TimeSpan _noOpInterval = Timeout.InfiniteTimeSpan;
    private string _noOpCommand = "NOOP";
    private bool _leaveOpen;
    private volatile bool _disconnected;
    private TimeSpan _listenTimeout = Timeout.InfiniteTimeSpan;

    // Item 44 / P1-A: track active response owners so the listener can route each line exactly
    // once. _activeCommandWaiters covers SendCommandAsync / SendMultilineCommandAsync;
    // _activeReadWaiters covers ReadAsync.
    // When the sum is zero, a line has no owner and is raised as unsolicited only (not queued).
    private volatile int _activeCommandWaiters;
    private volatile int _activeReadWaiters;

    // Item 49: idempotent disposal flag.
    private int _disposed; // 0 = alive, 1 = disposed (use Interlocked)

    // Item 48: lifecycle state for single-use connection guard.
    // 0 = NotConnected, 1 = Connecting, 2 = Connected, 3 = Disposed.
    private const int StateNotConnected = 0;
    private const int StateConnecting = 1;
    private const int StateConnected = 2;
    private const int StateDisposed = 3;
    private const int StatePoisoned = 4;
    private int _state = StateNotConnected;
    private Exception? _poisonCause;

    private int _maxLineLength = 8192;

    /// <summary>
    /// Gets or sets the maximum number of characters allowed in a single incoming response line.
    /// Lines longer than this limit cause the listener loop to disconnect.
    /// Default is 8192. Set to 0 to disable the check.
    /// </summary>
    /// <remarks>
    /// The limit is measured in UTF-16 characters (as counted by <see cref="StringBuilder.Length"/>)
    /// because the underlying <see cref="StreamReader"/> decodes bytes before this check is applied.
    /// When using ASCII encoding (the default) the character count equals the byte count for
    /// all code points below 128. For other encodings or non-ASCII content the character count
    /// may differ from the raw byte count.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int MaxLineLength
    {
        get => _maxLineLength;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "MaxLineLength must be non-negative.");
            _maxLineLength = value;
        }
    }

    private int _maxResponseCount = 10_000;

    /// <summary>
    /// Gets or sets the maximum number of response lines that <see cref="SendCommandAsync"/> will
    /// accumulate for a single command before throwing <see cref="InvalidDataException"/>.
    /// Default is 10 000. Set to 0 to disable the check.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int MaxResponseCount
    {
        get => _maxResponseCount;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "MaxResponseCount must be non-negative.");
            _maxResponseCount = value;
        }
    }

    /// <summary>
    /// Gets or sets the logger used to trace client activity.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponseClient"/> class.
    /// </summary>
    public CommandResponseClient() { }

    /// <summary>
    /// Occurs when a response is received from the server while no command waiter is active.
    /// Exceptions thrown by subscribers are caught; the exception is logged and then forwarded
    /// to <see cref="CallbackError"/> so callers can observe it without a logger.
    /// </summary>
    public event Action<ServerResponse>? UnsolicitedResponseReceived;

    /// <summary>
    /// Occurs when a subscriber of <see cref="UnsolicitedResponseReceived"/> throws an
    /// unhandled exception or when the keep-alive loop terminates unexpectedly.
    /// Provides an observable channel for callback faults when no logger is configured.
    /// </summary>
    public event Action<Exception>? CallbackError;

    /// <summary>
    /// Gets or sets the command sent during inactivity to keep the connection alive.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the value is empty, whitespace, or contains CR, LF or NUL.</exception>
    public string NoOpCommand
    {
        get => _noOpCommand;
        set
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("NoOpCommand must not be empty or whitespace.", nameof(value));
            if (value.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0)
                throw new ArgumentException("NoOpCommand must not contain CR, LF or NUL.", nameof(value));
            _noOpCommand = value;
        }
    }

    /// <summary>
    /// Gets or sets the time to wait before sending a no-op command.
    /// Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable keep-alive.
    /// Zero is rejected to prevent a busy-loop.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is zero or negative (unless it is <see cref="Timeout.InfiniteTimeSpan"/>).
    /// </exception>
    public TimeSpan NoOpInterval
    {
        get => _noOpInterval;
        set
        {
            if (value != Timeout.InfiniteTimeSpan && value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "NoOpInterval must be strictly positive or Timeout.InfiniteTimeSpan.");
            _noOpInterval = value;
            RestartKeepAlive();
        }
    }

    /// <summary>
    /// Gets or sets the timeout applied to read operations in the listener loop.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is negative and not <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    public TimeSpan ListenTimeout
    {
        get => _listenTimeout;
        set
        {
            if (value != Timeout.InfiniteTimeSpan && value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "ListenTimeout must be non-negative or Timeout.InfiniteTimeSpan.");
            // Clamp to int range to avoid overflow when converting to milliseconds (item 58).
            if (value != Timeout.InfiniteTimeSpan && value.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "ListenTimeout must not exceed approximately 24.8 days (int.MaxValue milliseconds).");
            _listenTimeout = value;
            if (_stream is not null && _stream.CanTimeout)
            {
                _stream.ReadTimeout = value == Timeout.InfiniteTimeSpan
                    ? -1
                    : (int)value.TotalMilliseconds;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the client is currently connected.
    /// Returns <see langword="false"/> on a newly constructed instance that has not yet called
    /// <see cref="ConnectAsync(string,int,System.Threading.CancellationToken)"/>, while
    /// <see cref="OnConnect"/> is still pending, and once the connection has been closed or disposed.
    /// </summary>
    /// <remarks>
    /// The value becomes <see langword="true"/> only after <see cref="OnConnect"/> completes
    /// successfully, so subclasses that use <see cref="OnConnect"/> for protocol handshaking can
    /// rely on the property to reflect the fully-initialised connection state.
    /// </remarks>
    public bool IsConnected => _state == StateConnected;

    /// <summary>
    /// Gets the first error that made the protocol session unusable, or <see langword="null"/>.
    /// </summary>
    public Exception? SessionFailure => Volatile.Read(ref _poisonCause);

    /// <summary>
    /// Default port used by the protocol.
    /// </summary>
    public virtual int DefaultPort { get; } = 0;

    /// <summary>
    /// Connects to the specified host and port using a TCP connection.
    /// </summary>
    /// <param name="host">Server host name or IP address.</param>
    /// <param name="port">Server port.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when a connection has already been established.</exception>
    public async Task ConnectAsync(string host, int port = -1, CancellationToken cancellationToken = default)
    {
        port = port == -1 ? DefaultPort : port;
        Logger?.LogInformation("Connecting to {Host}:{Port}", host, port);

        // Item 51 / P1-B: build in local variables; transfer to fields only after full success.
        // Do NOT commit _client before ConnectAsync(Stream) succeeds: if that call or OnConnect
        // fails and rolls back, the TcpClient must still be disposed by the outer catch.
        TcpClient? tcpClient = null;
        try
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            await ConnectAsync(tcpClient.GetStream(), false, cancellationToken).ConfigureAwait(false);
            // Full success: commit TcpClient ownership to the field.
            _client = tcpClient;
            tcpClient = null;
        }
        catch
        {
            tcpClient?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Uses the provided bidirectional <see cref="Stream"/> for communication.
    /// </summary>
    /// <param name="stream">Connected stream used to send commands and receive responses.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the client.</param>
    /// <param name="cancellationToken">
    /// Cancellation token whose lifetime is linked to the session: cancelling this token
    /// after connection stops the listener, the keep-alive loop, and all pending waiters.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a connection has already been established.</exception>
    public async Task ConnectAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        // Item 48: atomic single-use connection guard.
        int prev = Interlocked.CompareExchange(ref _state, StateConnecting, StateNotConnected);
        if (prev == StateDisposed)
            throw new ObjectDisposedException(GetType().Name);
        if (prev != StateNotConnected)
            throw new InvalidOperationException(
                "This client is already connected or has already been used. " +
                "Create a new instance for each connection.");

        // Item 51: build all resources in local variables so we can roll back on failure.
        StreamReader? reader = null;
        StreamWriter? writer = null;
        CancellationTokenSource? listenCts = null;
        // P1-1 fix: track whether resources have been committed to fields so the catch
        // block can choose the correct cleanup path.
        bool committed = false;
        try
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
            if (stream.CanTimeout)
            {
                int readTimeoutMs = _listenTimeout == Timeout.InfiniteTimeSpan
                    ? -1
                    : (int)_listenTimeout.TotalMilliseconds;
                stream.ReadTimeout = readTimeoutMs;
            }
            reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            writer = new StreamWriter(stream, Encoding.ASCII, 1024, true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };
            // Item 47: link the caller token so cancellation stops the session listener.
            listenCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _reader = reader;
            _writer = writer;
            _listenTokenSource = listenCts;
            reader = null;
            writer = null;
            listenCts = null;

            CancellationTokenSource capturedCts = _listenTokenSource;
            _listenThread = new Thread(() => ListenLoop(capturedCts.Token))
            {
                IsBackground = true
            };

            _disconnected = false;
            _listenThread.Start();
            Logger?.LogInformation("Client connected to stream");

            // P1-1 / P1-C: mark resources as committed before awaiting OnConnect so that the
            // catch block can roll back correctly. State intentionally stays at Connecting until
            // OnConnect succeeds: public callers that check IsConnected or call SendCommandAsync
            // will see "not yet connected", and the keep-alive loop will not start.
            committed = true;
            await OnConnect(stream, leaveOpen, cancellationToken).ConfigureAwait(false);

            // OnConnect succeeded. Promote the instance to Connected and start keep-alive.
            Interlocked.Exchange(ref _state, StateConnected);
            RestartKeepAlive();
        }
        catch
        {
            if (committed)
            {
                // OnConnect failed after resources were committed to fields.
                // Null each field after disposing so the subsequent Dispose() call (e.g. from a
                // using statement) finds only nulls and does not attempt double-cleanup.
                // RestartKeepAlive was not called yet (P1-C), so StopKeepAlive is a no-op here.
                StopKeepAlive();
                _listenTokenSource?.Cancel();
                _reader?.Dispose();  _reader = null;
                _listenThread?.Join(TimeSpan.FromSeconds(1));  _listenThread = null;
                _writer?.Dispose();  _writer = null;
                if (!_leaveOpen) _stream?.Dispose();
                _stream = null;  // P1-B: null so Dispose() skips the already-disposed stream
                _listenTokenSource?.Dispose();  _listenTokenSource = null;
            }
            else
            {
                // Synchronous setup failed; only local variables need cleanup.
                reader?.Dispose();
                writer?.Dispose();
                listenCts?.Dispose();
            }
            Interlocked.Exchange(ref _state, StateDisposed);
            throw;
        }
    }

    /// <summary>
    /// Called after the client has attached to the provided <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Connected stream used to send commands and receive responses.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when post-connection logic has executed.</returns>
    protected virtual Task OnConnect(Stream stream, bool leaveOpen, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes one complete protocol exchange while exclusively owning the connection.
    /// </summary>
    /// <typeparam name="TResult">Result produced by the exchange.</typeparam>
    /// <param name="command">Sanitized command name used for diagnostics.</param>
    /// <param name="exchange">Exchange implementation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exchange result.</returns>
    protected async Task<TResult> ExecuteExclusiveExchangeAsync<TResult>(
        string command,
        Func<ProtocolExchangeContext, CancellationToken, ValueTask<TResult>> exchange,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        ThrowIfUnavailable();
        CancellationToken sessionToken = _listenTokenSource?.Token ?? CancellationToken.None;
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionToken);
        await _sendLock.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();
            DrainPendingResponses();
            Interlocked.Increment(ref _activeCommandWaiters);
            try
            {
                return await exchange(new ProtocolExchangeContext(this, command), linked.Token).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCommandWaiters);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Marks this session unusable after protocol framing is lost.
    /// </summary>
    /// <param name="cause">The primary failure.</param>
    protected void PoisonSession(Exception cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        if (Interlocked.CompareExchange(ref _poisonCause, cause, null) is not null)
            return;
        Interlocked.Exchange(ref _state, StatePoisoned);
        _disconnected = true;
        try { _listenTokenSource?.Cancel(); } catch (ObjectDisposedException) { }
        try { _reader?.Dispose(); } catch (Exception ex) { Logger?.LogDebug(ex, "Error closing poisoned session reader"); }
        try { _writer?.Dispose(); } catch (Exception ex) { Logger?.LogDebug(ex, "Error closing poisoned session writer"); }
        try { if (!_leaveOpen) _stream?.Dispose(); } catch (Exception ex) { Logger?.LogDebug(ex, "Error closing poisoned session transport"); }
        _responseSignal.Release();
    }

    /// <summary>
    /// Throws when this client cannot start another protocol operation.
    /// </summary>
    private void ThrowIfUnavailable()
    {
        if (_disposed != 0 || _state == StateDisposed)
            throw new ObjectDisposedException(GetType().Name);
        if (_state == StatePoisoned)
            throw new ProtocolSessionPoisonedException("The protocol session is unusable.", _poisonCause);
        if (_disconnected)
            throw new IOException("Connection closed.");
        if (_state != StateConnected)
            throw new InvalidOperationException("Client is not connected.");
    }

    /// <summary>
    /// Provides controlled access to a connection during an exclusive protocol exchange.
    /// </summary>
    protected sealed class ProtocolExchangeContext
    {
        private readonly CommandResponseClient _owner;

        /// <summary>
        /// Initializes a context owned by one client exchange.
        /// </summary>
        internal ProtocolExchangeContext(CommandResponseClient owner, string command)
        {
            _owner = owner;
            Command = command;
        }

        /// <summary>Gets the sanitized command verb.</summary>
        public string Command { get; }

        /// <summary>Writes one validated protocol line.</summary>
        public async ValueTask WriteLineAsync(string line, CancellationToken cancellationToken)
        {
            ValidateCommandArgument(line, nameof(line));
            _owner.Logger?.LogDebug("Sending: {Command}", _owner.RedactCommandForLog(line));
            await _owner._writer!.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Reads the next framed response line.</summary>
        public async ValueTask<ServerResponse> ReadLineAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                await _owner._responseSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (_owner._responseQueue.TryDequeue(out ServerResponse response))
                    return response;
                if (_owner._disconnected)
                    throw new IOException("Connection closed before the response completed.");
            }
        }

        /// <summary>Marks the session unusable.</summary>
        public void Poison(Exception cause) => _owner.PoisonSession(cause);
    }

    /// <summary>
    /// Sends a command and collects responses until a response with at least completion severity is received.
    /// </summary>
    /// <param name="command">Command to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of responses returned by the server.</returns>
    /// <exception cref="IOException">Thrown when the connection has been closed.</exception>
    public async Task<IReadOnlyList<ServerResponse>> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        // P1-C: require the fully Connected state so external callers cannot send commands
        // while OnConnect is still negotiating the session (state = Connecting).
        if (_disposed != 0)
            throw new ObjectDisposedException(GetType().Name);
        if (_state == StateDisposed || _disconnected)
            throw new IOException("Connection closed.");
        if (_state != StateConnected)
            throw new InvalidOperationException("Client is not connected.");

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disconnected)
            {
                throw new IOException("Connection closed.");
            }
            DrainPendingResponses();
            Logger?.LogDebug("Sending: {Command}", RedactCommandForLog(command));

            // Item 44 / P1-2 race fix: register as the active response owner BEFORE writing
            // the command so that a response arriving immediately after (or during) the write
            // is routed to the queue rather than raised as unsolicited.
            Interlocked.Increment(ref _activeCommandWaiters);
            bool commandWritten = false;
            bool responseComplete = false;
            try
            {
                await _writer.WriteLineAsync(command.AsMemory(), cancellationToken).ConfigureAwait(false);
                commandWritten = true;
                List<ServerResponse> responses = new();
                while (true)
                {
                    await _responseSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                    if (!_responseQueue.TryDequeue(out ServerResponse response))
                    {
                        if (_disconnected)
                        {
                            throw new IOException("Connection closed.");
                        }
                        continue;
                    }
                    responses.Add(response);
                    if (MaxResponseCount > 0 && responses.Count > MaxResponseCount)
                    {
                        InvalidDataException error = new($"Server sent more than {MaxResponseCount} response lines for a single command.");
                        PoisonSession(error);
                        throw error;
                    }
                    if (response.Severity >= ResponseSeverity.Completion || response.Severity == ResponseSeverity.Unknown)
                    {
                        break;
                    }
                }
                responseComplete = true;
                ResetKeepAlive();
                return responses;
            }
            catch (Exception ex) when (commandWritten && !responseComplete && ex is OperationCanceledException or IOException)
            {
                PoisonSession(ex);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _activeCommandWaiters);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Reconstructs the original text line from a body <see cref="ServerResponse"/> as it
    /// would have appeared on the wire. Use this to convert body lines returned by
    /// <see cref="SendMultilineCommandAsync"/> back into plain strings for protocol processing
    /// (e.g. dot-unstuffing, field parsing).
    /// </summary>
    /// <param name="response">A body response line.</param>
    /// <returns>
    /// <c>response.Code</c> when <c>response.Message</c> is <see langword="null"/>;
    /// otherwise <c>"Code Message"</c>.
    /// </returns>
    protected static string BodyLineToString(ServerResponse response) =>
        response.Message is null ? response.Code : $"{response.Code} {response.Message}";

    /// <summary>Removes one dot from a correctly dot-stuffed payload line.</summary>
    internal static ReadOnlyMemory<char> UnstuffDotLine(ReadOnlyMemory<char> line) =>
        line.Span.StartsWith("..".AsSpan(), StringComparison.Ordinal) ? line[1..] : line;

    /// <summary>
    /// Streams a complete dot-terminated exchange while retaining exclusive connection ownership.
    /// </summary>
    protected Task<IReadOnlyList<ServerResponse>> StreamMultilineCommandAsync(
        string command,
        Func<ReadOnlyMemory<char>, CancellationToken, ValueTask> consumeLine,
        ProtocolPayloadLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumeLine);
        ArgumentNullException.ThrowIfNull(limits);
        ValidateCommandArgument(command, nameof(command));
        return ExecuteExclusiveExchangeAsync<IReadOnlyList<ServerResponse>>(command, async (context, token) =>
        {
            bool payloadStarted = false;
            bool terminated = false;
            try
            {
                await context.WriteLineAsync(command, token).ConfigureAwait(false);
                List<ServerResponse> status = [];
                while (true)
                {
                    ServerResponse response = await context.ReadLineAsync(token).ConfigureAwait(false);
                    status.Add(response);
                    if (MaxResponseCount > 0 && status.Count > MaxResponseCount)
                        throw new InvalidDataException($"Server sent more than {MaxResponseCount} status lines.");
                    if (response.Severity >= ResponseSeverity.Completion || response.Severity == ResponseSeverity.Unknown)
                        break;
                }
                if (status[^1].Severity is ResponseSeverity.TransientNegative or ResponseSeverity.PermanentNegative)
                    return status;

                payloadStarted = true;
                int lines = 0;
                long characters = 0;
                long bytes = 0;
                while (true)
                {
                    ServerResponse response = await context.ReadLineAsync(token).ConfigureAwait(false);
                    string raw = BodyLineToString(response);
                    if (raw == ".")
                    {
                        terminated = true;
                        break;
                    }
                    lines = checked(lines + 1);
                    characters = checked(characters + raw.Length);
                    bytes = checked(bytes + Encoding.UTF8.GetByteCount(raw));
                    if ((limits.MaximumLines > 0 && lines > limits.MaximumLines) ||
                        (limits.MaximumCharacters > 0 && characters > limits.MaximumCharacters) ||
                        (limits.MaximumBytes > 0 && bytes > limits.MaximumBytes))
                        throw new InvalidDataException("The dot-terminated payload exceeded its configured limit.");
                    await consumeLine(UnstuffDotLine(raw.AsMemory()), token).ConfigureAwait(false);
                }
                ResetKeepAlive();
                return status;
            }
            catch (Exception ex) when (payloadStarted && !terminated)
            {
                context.Poison(ex);
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Sends a command, receives the opening status response(s), then reads all body lines
    /// until <paramref name="isBodyTerminator"/> returns <see langword="true"/>, all within
    /// a single locked operation. Use for multi-line protocol commands such as POP3 RETR or
    /// NNTP ARTICLE where body lines immediately follow the status response; holding
    /// <c>_activeCommandWaiters</c> throughout both phases eliminates the window in which
    /// body lines would otherwise be misrouted as unsolicited responses.
    /// </summary>
    /// <param name="command">Command line to send (must not contain CR, LF or NUL).</param>
    /// <param name="isBodyTerminator">
    /// Returns <see langword="true"/> for the body line that signals the end of the payload
    /// (e.g. a bare <c>.</c> in POP3 or NNTP). The terminator line itself is not included
    /// in the returned body lines.
    /// </param>
    /// <param name="maxBodyLines">
    /// Maximum number of body lines to accept. <see cref="InvalidDataException"/> is thrown
    /// when the limit is exceeded. Set to 0 to disable.
    /// </param>
    /// <param name="maxBodyChars">
    /// Maximum total characters across all body lines. <see cref="InvalidDataException"/> is
    /// thrown when the limit is exceeded. Set to 0 to disable.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple of <c>StatusLines</c> (the opening response line(s), including the final
    /// completion or unknown-severity line) and <c>BodyLines</c> (all lines before the
    /// terminator). <c>BodyLines</c> is empty when the last status line indicates a negative
    /// response (severity ≥ <see cref="ResponseSeverity.TransientNegative"/>).
    /// </returns>
    /// <exception cref="IOException">Thrown when the connection closes before the exchange completes.</exception>
    /// <exception cref="InvalidDataException">Thrown when <see cref="MaxResponseCount"/>, <paramref name="maxBodyLines"/> or <paramref name="maxBodyChars"/> is exceeded.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the client is not connected.</exception>
    protected async Task<(IReadOnlyList<ServerResponse> StatusLines, IReadOnlyList<ServerResponse> BodyLines)> SendMultilineCommandAsync(
        string command,
        Func<ServerResponse, bool> isBodyTerminator,
        int maxBodyLines = 0,
        int maxBodyChars = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isBodyTerminator);
        List<ServerResponse> body = [];
        ProtocolPayloadLimits limits = new()
        {
            MaximumLines = maxBodyLines,
            MaximumCharacters = maxBodyChars,
            MaximumBytes = 0
        };
        IReadOnlyList<ServerResponse> status = await StreamMultilineCommandAsync(
            command,
            (line, _) =>
            {
                body.Add(ParseResponseLine(line.ToString()));
                return ValueTask.CompletedTask;
            },
            limits,
            cancellationToken).ConfigureAwait(false);
        return (status, body);
    }

    /// <summary>
    /// Reads and returns responses that have been received without sending a command.
    /// May be called from <see cref="OnConnect"/> (while the state is still
    /// <c>Connecting</c>) to read a server greeting banner before the connection is
    /// promoted to <c>Connected</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of responses read from the server.</returns>
    /// <exception cref="IOException">Thrown when the connection has been closed.</exception>
    public async Task<IReadOnlyList<ServerResponse>> ReadAsync(CancellationToken cancellationToken = default)
    {
        // P1-A: register as a response owner BEFORE all state checks and lock acquisition so
        // the listener immediately routes any incoming line to the queue. Without this early
        // increment, a response arriving between ConnectAsync returning and ReadAsync acquiring
        // the lock would be treated as unsolicited (no owner registered yet) and lost.
        Interlocked.Increment(ref _activeReadWaiters);
        try
        {
            // Allow during StateConnecting so OnConnect implementations can read the server
            // greeting (P1-1). External callers see StateNotConnected or StateDisposed until
            // OnConnect has completed and the state is promoted to StateConnected.
            if (_disposed != 0)
                throw new ObjectDisposedException(GetType().Name);
            if (_state == StateDisposed || _disconnected)
                throw new IOException("Connection closed.");
            if (_state != StateConnected && _state != StateConnecting)
                throw new InvalidOperationException("Client is not connected.");

            // Link the session lifetime token so a concurrent Dispose() or session end unblocks
            // both the lock wait and the signal wait (same pattern as SendLinesAsync, P1-3).
            CancellationToken sessionToken = _listenTokenSource?.Token ?? CancellationToken.None;
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionToken);
            CancellationToken effective = linkedCts.Token;

            // P1-2: serialize with SendCommandAsync by taking the same _sendLock, so only one
            // response consumer is active at a time. Without this guard, a concurrent ReadAsync
            // could dequeue a response that was intended for SendCommandAsync (or vice versa),
            // because both operations share the same _responseQueue.
            await _sendLock.WaitAsync(effective).ConfigureAwait(false);
            try
            {
                if (_disposed != 0)
                    throw new ObjectDisposedException(GetType().Name);
                if (_disconnected && _responseSignal.CurrentCount == 0)
                    throw new IOException("Connection closed.");

                List<ServerResponse> responses = new();
                await _responseSignal.WaitAsync(effective).ConfigureAwait(false);
                do
                {
                    if (_responseQueue.TryDequeue(out ServerResponse response))
                    {
                        responses.Add(response);
                    }
                    else if (_disconnected)
                    {
                        throw new IOException("Connection closed.");
                    }
                }
                while (await _responseSignal.WaitAsync(0).ConfigureAwait(false));
                ResetKeepAlive();
                return responses;
            }
            finally
            {
                _sendLock.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeReadWaiters);
        }
    }

    /// <summary>
    /// Sends raw lines to the server without waiting for a response.
    /// Each line is validated to ensure it contains no CR, LF or NUL characters that
    /// could inject additional protocol commands.
    /// </summary>
    /// <param name="lines">Lines to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when any line in <paramref name="lines"/> contains CR, LF or NUL.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the client is not connected.</exception>
    protected async Task SendLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
    {
        // P1-3: apply the same state guards as SendCommandAsync before waiting on the lock.
        if (_disposed != 0)
            throw new ObjectDisposedException(GetType().Name);
        if (_state == StateDisposed || _disconnected)
            throw new IOException("Connection closed.");
        if (_state != StateConnected)
            throw new InvalidOperationException("Client is not connected.");

        // Item 53 / P2-6: always materialise into a new list — even when the caller passes a
        // List<string> — so a concurrent mutation between validation and the write loop cannot
        // bypass the per-line checks or inject additional commands.
        List<string> lineList = [..lines];
        foreach (string line in lineList)
        {
            if (line is null) throw new ArgumentException("Lines must not contain null entries.", nameof(lines));
            ValidateCommandArgument(line, nameof(lines));
        }

        // Link the session lifetime token so a concurrent Dispose() or caller cancellation
        // both unblock the lock wait and each individual write.
        CancellationToken sessionToken = _listenTokenSource?.Token ?? CancellationToken.None;
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionToken);
        CancellationToken effective = linkedCts.Token;

        await _sendLock.WaitAsync(effective).ConfigureAwait(false);
        try
        {
            // P1-3: re-verify state after acquiring the lock; the session may have ended
            // between the initial check and lock acquisition.
            if (_disposed != 0)
                throw new ObjectDisposedException(GetType().Name);
            if (_disconnected)
                throw new IOException("Connection closed.");

            foreach (string line in lineList)
            {
                await _writer!.WriteLineAsync(line.AsMemory(), effective).ConfigureAwait(false);
            }
            ResetKeepAlive();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Sends a sequence of raw lines and reads the server's response in a single locked
    /// operation. Use for body-then-response exchanges (SMTP DATA body, NNTP POST body)
    /// where the server replies immediately after the terminator. Holding
    /// <c>_activeCommandWaiters > 0</c> throughout both phases ensures the response is
    /// always routed to the queue and never raised as unsolicited.
    /// </summary>
    /// <param name="lines">Lines to send (must not contain CR, LF or NUL).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The server's response to the body.</returns>
    /// <exception cref="ArgumentException">Thrown when any line contains CR, LF or NUL.</exception>
    /// <exception cref="IOException">Thrown when the connection has been closed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the client is not connected.</exception>
    protected async Task<IReadOnlyList<ServerResponse>> SendBodyAndReadAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
    {
        if (_disposed != 0)
            throw new ObjectDisposedException(GetType().Name);
        if (_state == StateDisposed || _disconnected)
            throw new IOException("Connection closed.");
        if (_state != StateConnected)
            throw new InvalidOperationException("Client is not connected.");

        List<string> lineList = [..lines];
        foreach (string line in lineList)
        {
            if (line is null) throw new ArgumentException("Lines must not contain null entries.", nameof(lines));
            ValidateCommandArgument(line, nameof(lines));
        }

        CancellationToken sessionToken = _listenTokenSource?.Token ?? CancellationToken.None;
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionToken);
        CancellationToken effective = linkedCts.Token;

        await _sendLock.WaitAsync(effective).ConfigureAwait(false);
        try
        {
            if (_disposed != 0)
                throw new ObjectDisposedException(GetType().Name);
            if (_disconnected)
                throw new IOException("Connection closed.");

            DrainPendingResponses();

            Interlocked.Increment(ref _activeCommandWaiters);
            try
            {
                foreach (string line in lineList)
                {
                    await _writer!.WriteLineAsync(line.AsMemory(), effective).ConfigureAwait(false);
                }
                List<ServerResponse> responses = new();
                while (true)
                {
                    await _responseSignal.WaitAsync(effective).ConfigureAwait(false);
                    if (!_responseQueue.TryDequeue(out ServerResponse response))
                    {
                        if (_disconnected)
                            throw new IOException("Connection closed.");
                        continue;
                    }
                    responses.Add(response);
                    if (MaxResponseCount > 0 && responses.Count > MaxResponseCount)
                        throw new InvalidDataException($"Server sent more than {MaxResponseCount} response lines for a single command.");
                    if (response.Severity >= ResponseSeverity.Completion || response.Severity == ResponseSeverity.Unknown)
                        break;
                }
                ResetKeepAlive();
                return responses;
            }
            finally
            {
                Interlocked.Decrement(ref _activeCommandWaiters);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Discards any queued responses left by a previous cancelled or interrupted waiter.
    /// </summary>
    private void DrainPendingResponses()
    {
        // With exclusive routing (P1-A), a line is only enqueued when a command or read waiter
        // was active at the moment of arrival. Items found here are therefore residual responses
        // from a cancelled SendCommandAsync or ReadAsync that dequeued its waiter count but
        // left signals in the queue (e.g. cancelled during _responseSignal.WaitAsync).
        // Discard them so the next command starts with a clean queue.
        while (_responseQueue.TryDequeue(out _)) { }
        while (_responseSignal.CurrentCount > 0) { _responseSignal.Wait(0); }
    }

    /// <summary>
    /// Reads one response line from <paramref name="reader"/>, enforcing <see cref="MaxLineLength"/>
    /// incrementally. Uses async reads so that <paramref name="cancellationToken"/> can interrupt a
    /// blocking read (e.g. on a Pipe that has no data). Returns <see langword="null"/> on EOF or
    /// cancellation.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the line exceeds <see cref="MaxLineLength"/>.</exception>
    private string? ReadLimitedLine(StreamReader reader, CancellationToken cancellationToken)
    {
        var sb = new System.Text.StringBuilder(256);
        char[] buf = new char[1];

        // Build a per-line CTS that fires after ListenTimeout.  When _listenTimeout is
        // infinite the session token is used directly to avoid allocating a linked CTS.
        // NetworkStream.ReadAsync does not honour stream.ReadTimeout on .NET Core, so we
        // implement the timeout ourselves via CancellationTokenSource.CancelAfter().
        CancellationTokenSource? timeoutCts = _listenTimeout != Timeout.InfiniteTimeSpan
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        timeoutCts?.CancelAfter(_listenTimeout);
        CancellationToken effectiveToken = timeoutCts?.Token ?? cancellationToken;

        try
        {
            while (true)
            {
                int read;
                try
                {
                    read = reader.ReadAsync(buf.AsMemory(0, 1), effectiveToken).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (timeoutCts is not null && timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Timeout fired before the session token — surface as a socket-style read timeout
                    // so the ListenLoop catch clause exits the loop without marking it as an error.
                    throw new IOException("Read timed out.", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.TimedOut));
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                if (read == 0) return sb.Length == 0 ? null : sb.ToString();
                char c = buf[0];
                if (c == '\n')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
                        sb.Length--;
                    return sb.ToString();
                }
                sb.Append(c);
                // A trailing \r will be stripped when \n arrives, so exclude it from the count.
                int effectiveLength = (sb.Length > 0 && sb[sb.Length - 1] == '\r') ? sb.Length - 1 : sb.Length;
                if (MaxLineLength > 0 && effectiveLength > MaxLineLength)
                    throw new InvalidDataException($"Incoming response line exceeded MaxLineLength ({MaxLineLength}).");
            }
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }

    /// <summary>
    /// Listens for responses from the server on a dedicated thread and enqueues them for processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private void ListenLoop(CancellationToken cancellationToken)
    {
        if (_reader is null)
        {
            return;
        }
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = ReadLimitedLine(_reader, cancellationToken);
                }
                catch (IOException ex) when (ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.TimedOut)
                {
                    // Exit the loop when no data is received within the read timeout.
                    break;
                }
                catch (InvalidDataException)
                {
                    Logger?.LogWarning("Incoming response line exceeded MaxLineLength ({MaxLineLength}); disconnecting.", MaxLineLength);
                    break;
                }

                if (line is null)
                {
                    break;
                }

                ServerResponse response = ParseResponseLine(line);
                Logger?.LogDebug("Received: {Code} {Message}", SanitizeForLog(response.Code, 10), SanitizeForLog(response.Message ?? string.Empty, 200));

                // P1-A: route each line to exactly one destination.
                // Enqueue (and signal) only when a command or read waiter is active; otherwise
                // the line is unsolicited and is delivered directly to the event without being
                // stored in the queue (preventing ReadAsync / SendCommandAsync from seeing it later).
                //
                // P1-1 (banner): also queue during StateConnecting so the server greeting is
                // buffered even before OnConnect calls ReadAsync. Without this, a banner that
                // arrives the moment the listener starts would be routed as unsolicited (no owner
                // is registered yet) and would never reach the ReadAsync call in OnConnect.
                //
                // Callers that want to read a banner after ConnectAsync (i.e. outside OnConnect)
                // must call ReadAsync immediately — ReadAsync increments _activeReadWaiters before
                // acquiring the lock, which closes the race window sufficiently for in-process
                // streams. For real TCP connections, reading from OnConnect is the reliable pattern.
                if (_state == StateConnecting || _activeCommandWaiters + _activeReadWaiters > 0)
                {
                    _responseQueue.Enqueue(response);
                    _responseSignal.Release();
                }
                else
                {
                    RaiseUnsolicitedResponseReceived(response);
                }
            }
        }
        catch (IOException)
        {
            // Connection closed.
        }
        catch (ObjectDisposedException)
        {
            // Stream disposed.
        }
        finally
        {
            _disconnected = true;
            // P1-C: update state so IsConnected returns false as soon as the listener exits,
            // regardless of whether Dispose() has been called yet.
            Interlocked.CompareExchange(ref _state, StateDisposed, StateConnected);
            _responseSignal.Release();
            Logger?.LogWarning("Listener thread terminated");
        }
    }

    /// <summary>
    /// Raises <see cref="UnsolicitedResponseReceived"/>, catching and logging any subscriber
    /// exceptions so they cannot terminate the listener thread (item 45).
    /// </summary>
    private void RaiseUnsolicitedResponseReceived(ServerResponse response)
    {
        Action<ServerResponse>? handler = UnsolicitedResponseReceived;
        if (handler is null) return;
        foreach (Delegate d in handler.GetInvocationList())
        {
            try
            {
                ((Action<ServerResponse>)d)(response);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "UnsolicitedResponseReceived subscriber threw an unhandled exception");
                RaiseCallbackError(ex);
            }
        }
    }

    /// <summary>
    /// Forwards <paramref name="ex"/> to each <see cref="CallbackError"/> subscriber in turn,
    /// catching individual subscriber exceptions so a misbehaving subscriber cannot suppress
    /// the delivery to the remaining subscribers or propagate back into the transport (P2-E).
    /// </summary>
    private void RaiseCallbackError(Exception ex)
    {
        Action<Exception>? handler = CallbackError;
        if (handler is null) return;
        foreach (Delegate d in handler.GetInvocationList())
        {
            try { ((Action<Exception>)d)(ex); }
            catch (Exception inner) { Logger?.LogError(inner, "CallbackError subscriber threw an unhandled exception"); }
        }
    }

    /// <summary>
    /// Validates that <paramref name="value"/> does not contain CR, LF or NUL characters,
    /// which would allow an attacker to inject additional protocol commands.
    /// </summary>
    /// <param name="value">String to validate.</param>
    /// <param name="paramName">Parameter name used in the exception message.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> contains <c>\r</c>, <c>\n</c> or <c>\0</c>.
    /// </exception>
    protected static void ValidateCommandArgument(string value, string paramName)
    {
        if (value.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0)
        {
            throw new ArgumentException(
                "Command argument must not contain CR, LF or NUL characters.",
                paramName);
        }
    }

    /// <summary>
    /// Returns a loggable (redacted) representation of a command before it is sent.
    /// The default implementation logs only the verb (first space-separated word) to avoid
    /// accidentally exposing secret-bearing arguments such as AUTH credentials or PASS values.
    /// Override in a protocol subclass to log more detail for commands that are known to be safe.
    /// </summary>
    /// <param name="command">Command about to be sent.</param>
    /// <returns>A string safe to write to the log.</returns>
    protected virtual string RedactCommandForLog(string command)
    {
        int space = command.IndexOf(' ');
        string verb = space >= 0 ? command[..space] : command;
        string suffix = space >= 0 ? " [...]" : string.Empty;
        return SanitizeForLog(verb) + suffix;
    }

    /// <summary>
    /// Replaces control characters with '?' and truncates the value to
    /// <paramref name="maxLength"/> characters to prevent log injection or flooding.
    /// </summary>
    protected static string SanitizeForLog(string value, int maxLength = 100)
    {
        bool truncated = value.Length > maxLength;
        ReadOnlySpan<char> source = truncated ? value.AsSpan(0, maxLength) : value.AsSpan();
        char[] chars = new char[truncated ? maxLength + 3 : source.Length];
        for (int i = 0; i < source.Length; i++)
            chars[i] = source[i] < 0x20 || source[i] == 0x7F ? '?' : source[i];
        if (truncated) { chars[maxLength] = '.'; chars[maxLength + 1] = '.'; chars[maxLength + 2] = '.'; }
        return new string(chars);
    }

    /// <summary>
    /// Splits a response line into the status code and the remaining text.
    /// </summary>
    /// <param name="line">Line to split.</param>
    /// <returns>Tuple containing the code and optional message.</returns>
    protected static (string code, string? message) SplitCodeAndMessage(string line)
    {
        int index = line.IndexOf(' ');
        return index >= 0
            ? (line[..index], line[(index + 1)..])
            : (line, null);
    }

    /// <summary>
    /// Parses a single response line. The default implementation expects a three-digit
    /// numeric status code followed by an optional text message. Lines that do not start
    /// with a numeric code are treated as raw text payloads.
    /// </summary>
    /// <param name="line">Response line from the server.</param>
    /// <returns>Parsed response.</returns>
    protected virtual ServerResponse ParseResponseLine(string line)
    {
        if (line.Length >= 3 &&
            char.IsDigit(line[0]) &&
            char.IsDigit(line[1]) &&
            char.IsDigit(line[2]) &&
            (line.Length == 3 || line[3] == ' '))
        {
            string code = line[..3];
            string? text = line.Length >= 4 ? line[4..] : null;
            ResponseSeverity severity = ResponseSeverity.Unknown;
            int digit = code[0] - '0';
            if (digit >= 0 && digit <= 5)
            {
                severity = (ResponseSeverity)digit;
            }
            return new ServerResponse(code, severity, text);
        }
        return new ServerResponse(line, ResponseSeverity.Unknown, null);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 46: cancellable keep-alive loop replacing async-void Timer
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Stops any running keep-alive task and starts a new one if the interval is finite.
    /// </summary>
    private void RestartKeepAlive()
    {
        StopKeepAlive();
        // P1-C: do not start keep-alive until the state is fully Connected (i.e. OnConnect has
        // returned successfully). Setting NoOpInterval before ConnectAsync returns is permitted
        // but the loop is not created until ConnectAsync explicitly calls RestartKeepAlive().
        if (_noOpInterval == Timeout.InfiniteTimeSpan || _listenTokenSource is null || _state != StateConnected) return;
        Interlocked.Exchange(ref _lastActivityTick, Environment.TickCount64);
        _keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(_listenTokenSource.Token);
        CancellationToken ct = _keepAliveCts.Token;
        _keepAliveTask = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Calculate how long until the next send is due, based on last activity.
                    // Reading _noOpInterval each iteration picks up runtime changes to the property.
                    long intervalMs = (long)_noOpInterval.TotalMilliseconds;
                    long elapsedMs = Environment.TickCount64 - Interlocked.Read(ref _lastActivityTick);
                    long remainingMs = intervalMs - elapsedMs;
                    if (remainingMs > 0)
                        await Task.Delay(TimeSpan.FromMilliseconds(remainingMs), ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;
                    // Re-check after delay: a command may have arrived during the wait.
                    elapsedMs = Environment.TickCount64 - Interlocked.Read(ref _lastActivityTick);
                    if (elapsedMs < intervalMs)
                        continue; // activity restarted the idle window; recalculate without self-wait
                    await SendNoOpAsync(ct).ConfigureAwait(false);
                    // SendNoOpAsync → SendCommandAsync → ResetKeepAlive updates _lastActivityTick.
                    // The loop then recalculates the next delay without restarting itself.
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Keep-alive loop terminated unexpectedly");
                RaiseCallbackError(ex);  // P2-E: isolated from keep-alive loop; subscriber faults go to log only
            }
        }, ct);
    }

    /// <summary>
    /// Cancels and awaits the current keep-alive task (up to 1 s).
    /// </summary>
    private void StopKeepAlive()
    {
        _keepAliveCts?.Cancel();
        try
        {
            _keepAliveTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore faults during shutdown.
        }
        _keepAliveCts?.Dispose();
        _keepAliveCts = null;
        _keepAliveTask = null;
    }

    /// <summary>
    /// Sends the no-op command.
    /// </summary>
    private async Task SendNoOpAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger?.LogDebug("Sending keep-alive: {Command}", _noOpCommand);
            await SendCommandAsync(_noOpCommand, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path; do not report via CallbackError.
        }
        catch (ObjectDisposedException)
        {
            // Resources were disposed during shutdown; treat as normal cancellation.
        }
        catch (Exception ex)
        {
            // P2-F: forward genuine NOOP failures to the observable error channel so callers
            // can detect recurring connection issues even when no logger is configured.
            Logger?.LogWarning(ex, "Keep-alive command failed");
            RaiseCallbackError(ex);
        }
    }

    /// <summary>
    /// Disconnects from the server, optionally sending a termination command.
    /// </summary>
    /// <param name="command">Termination command to send. Null to close immediately.</param>
    /// <param name="timeout">Time to wait for a positive (2xx) response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectAsync(string? command = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Logger?.LogInformation("Disconnecting");
        if (_writer is not null && command is not null)
        {
            try
            {
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (timeout.HasValue && timeout.Value != Timeout.InfiniteTimeSpan)
                {
                    cts.CancelAfter(timeout.Value);
                }
                IReadOnlyList<ServerResponse> responses = await SendCommandAsync(command, cts.Token).ConfigureAwait(false);
                if (responses.Count == 0 || responses[^1].Severity != ResponseSeverity.Completion)
                {
                    // Force disconnect on missing positive reply.
                    await (_listenTokenSource?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
                }
            }
            catch
            {
                await (_listenTokenSource?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
            }
        }
        else
        {
            await (_listenTokenSource?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        }

        Dispose();
        Logger?.LogInformation("Disconnected");
    }

    /// <summary>
    /// Records the current time as the last activity instant, which postpones the next keep-alive send.
    /// Does not restart the keep-alive loop, avoiding a deadlock where the loop would wait on itself.
    /// </summary>
    protected void ResetKeepAlive()
    {
        Interlocked.Exchange(ref _lastActivityTick, Environment.TickCount64);
    }

    /// <summary>
    /// Releases the client resources. Safe to call multiple times (idempotent).
    /// </summary>
    public void Dispose()
    {
        // Item 49: idempotent — only one caller proceeds through cleanup.
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        Interlocked.Exchange(ref _state, StateDisposed);

        StopKeepAlive();
        _listenTokenSource?.Cancel();
        _reader?.Dispose();          // interrupt any blocking read so the listener thread can exit
        _reader = null;
        _listenThread?.Join(TimeSpan.FromSeconds(1));  // wait for listener to stop before disposing writer
        _writer?.Dispose();          // P1-D: safe to dispose only after the listener has exited
        _writer = null;
        if (!_leaveOpen)
        {
            _stream?.Dispose();
        }
        _listenTokenSource?.Dispose();
        _client?.Dispose();
        // _responseSignal and _sendLock are intentionally NOT disposed: SemaphoreSlim holds no
        // native resources unless AvailableWaitHandle is accessed (which we never do), so there
        // is nothing to release. Disposing them would race with any SendCommandAsync / ReadAsync
        // finally block that calls Release() after the 1-second StopKeepAlive timeout expires,
        // causing a secondary ObjectDisposedException with no benefit (item 49 / P1-1).
        GC.SuppressFinalize(this);
    }

    // Item 50: no finalizer — all resources are managed. A finalizer that joins threads or
    // disposes managed semaphores from the finalizer thread can deadlock the GC.
}
