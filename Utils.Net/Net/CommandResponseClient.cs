using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Utils.Objects;

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
    private CancellationTokenSource? _listenTokenSource;
    private Thread? _listenThread;

    // Item 46: keep-alive runs as a cancellable Task instead of an async-void Timer callback.
    private CancellationTokenSource? _keepAliveCts;
    private Task? _keepAliveTask;

    private TimeSpan _noOpInterval = Timeout.InfiniteTimeSpan;
    private string _noOpCommand = "NOOP";
    private bool _leaveOpen;
    private bool _everConnected;
    private volatile bool _disconnected;
    private TimeSpan _listenTimeout = Timeout.InfiniteTimeSpan;

    // Item 44: track how many command waiters are currently consuming responses so that the
    // listener knows whether an incoming line is solicited or unsolicited.
    private volatile int _activeCommandWaiters;

    // Item 48: lifecycle state for single-use connection guard.
    // 0 = NotConnected, 1 = Connecting, 2 = Connected, 3 = Disposed.
    private const int StateNotConnected = 0;
    private const int StateConnecting = 1;
    private const int StateConnected = 2;
    private const int StateDisposed = 3;
    private int _state = StateNotConnected;

    /// <summary>
    /// Gets or sets the maximum number of bytes allowed in a single incoming response line.
    /// Lines longer than this limit cause the listener loop to disconnect.
    /// Default is 8192 bytes (8 KiB). Set to 0 to disable the check.
    /// </summary>
    public int MaxLineLength { get; set; } = 8192;

    /// <summary>
    /// Gets or sets the maximum number of response lines that <see cref="SendCommandAsync"/> will
    /// accumulate for a single command before throwing <see cref="InvalidDataException"/>.
    /// Default is 10 000. Set to 0 to disable the check.
    /// </summary>
    public int MaxResponseCount { get; set; } = 10_000;

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
    /// Exceptions thrown by subscribers are caught and logged rather than propagated to the
    /// listener thread to prevent a misbehaving callback from killing the transport.
    /// </summary>
    public event Action<ServerResponse>? UnsolicitedResponseReceived;

    /// <summary>
    /// Gets or sets the command sent during inactivity to keep the connection alive.
    /// </summary>
    public string NoOpCommand
    {
        get => _noOpCommand;
        set => _noOpCommand = value;
    }

    /// <summary>
    /// Gets or sets the time to wait before sending a no-op command. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    public TimeSpan NoOpInterval
    {
        get => _noOpInterval;
        set
        {
            _noOpInterval = value;
            RestartKeepAlive();
        }
    }

    /// <summary>
    /// Gets or sets the timeout applied to read operations in the listener loop.
    /// </summary>
    public TimeSpan ListenTimeout
    {
        get => _listenTimeout;
        set
        {
            _listenTimeout = value;
            if (_stream is not null && _stream.CanTimeout)
            {
                _stream.ReadTimeout = value == Timeout.InfiniteTimeSpan ? -1 : (int)value.TotalMilliseconds;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the client is currently connected.
    /// Returns <see langword="false"/> on a newly constructed instance that has not yet called
    /// <see cref="ConnectAsync(string,int,System.Threading.CancellationToken)"/>, and returns
    /// <see langword="false"/> again once the connection has been closed or disposed.
    /// </summary>
    public bool IsConnected => _everConnected && !_disconnected;

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
    public async Task ConnectAsync(string host, int port = -1, CancellationToken cancellationToken = default)
    {
        port = port == -1 ? DefaultPort : port;
        Logger?.LogInformation("Connecting to {Host}:{Port}", host, port);
        _client = new TcpClient();
        await _client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        await ConnectAsync(_client.GetStream(), false, cancellationToken).ConfigureAwait(false);
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
    public Task ConnectAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        // Item 48: atomic single-use connection guard.
        int prev = Interlocked.CompareExchange(ref _state, StateConnecting, StateNotConnected);
        if (prev == StateDisposed)
            throw new ObjectDisposedException(GetType().Name);
        if (prev != StateNotConnected)
            throw new InvalidOperationException(
                "This client is already connected or has already been used. " +
                "Create a new instance for each connection.");

        try
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
            if (stream.CanTimeout)
            {
                stream.ReadTimeout = _listenTimeout == Timeout.InfiniteTimeSpan ? -1 : (int)_listenTimeout.TotalMilliseconds;
            }
            _reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            _writer = new StreamWriter(stream, Encoding.ASCII, 1024, true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };
            // Item 47: link the caller token so cancellation stops the session listener.
            _listenTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listenThread = new Thread(() => ListenLoop(_listenTokenSource.Token))
            {
                IsBackground = true
            };
            _everConnected = true;
            _disconnected = false;
            _listenThread.Start();
            Logger?.LogInformation("Client connected to stream");

            // Item 46: start the keep-alive loop as a cancellable Task.
            RestartKeepAlive();

            Interlocked.Exchange(ref _state, StateConnected);
            return OnConnect(stream, leaveOpen, cancellationToken);
        }
        catch
        {
            Interlocked.Exchange(ref _state, StateDisposed);
            _reader?.Dispose();
            _writer?.Dispose();
            _listenTokenSource?.Dispose();
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
    /// Sends a command and collects responses until a response with at least completion severity is received.
    /// </summary>
    /// <param name="command">Command to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of responses returned by the server.</returns>
    /// <exception cref="IOException">Thrown when the connection has been closed.</exception>
    public async Task<IReadOnlyList<ServerResponse>> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disconnected)
            {
                throw new IOException("Connection closed.");
            }
            DrainPendingResponses();
            Logger?.LogDebug("Sending: {Command}", RedactCommandForLog(command));
            await _writer.WriteLineAsync(command).ConfigureAwait(false);

            // Item 44: signal that a command waiter is now active so ListenLoop does not
            // raise UnsolicitedResponseReceived for the expected reply lines.
            Interlocked.Increment(ref _activeCommandWaiters);
            try
            {
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
                        throw new InvalidDataException($"Server sent more than {MaxResponseCount} response lines for a single command.");
                    }
                    if (response.Severity >= ResponseSeverity.Completion || response.Severity == ResponseSeverity.Unknown)
                    {
                        break;
                    }
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
    /// Reads and returns responses that have been received without sending a command.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of responses read from the server.</returns>
    /// <exception cref="IOException">Thrown when the connection has been closed.</exception>
    public async Task<IReadOnlyList<ServerResponse>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (_disconnected && _responseSignal.CurrentCount == 0)
        {
            throw new IOException("Connection closed.");
        }
        List<ServerResponse> responses = new();
        await _responseSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Sends raw lines to the server without waiting for a response.
    /// </summary>
    /// <param name="lines">Lines to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not connected.</exception>
    protected async Task SendLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (string line in lines)
            {
                await _writer.WriteLineAsync(line).ConfigureAwait(false);
            }
            ResetKeepAlive();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Removes any queued responses that were not consumed by previous commands.
    /// </summary>
    private void DrainPendingResponses()
    {
        while (_responseQueue.TryDequeue(out ServerResponse leftover))
        {
            RaiseUnsolicitedResponseReceived(leftover);
        }
        while (_responseSignal.CurrentCount > 0)
        {
            _responseSignal.Wait(0);
        }
    }

    /// <summary>
    /// Reads one response line from <paramref name="reader"/>, enforcing <see cref="MaxLineLength"/>
    /// incrementally rather than after the full line has been buffered, so a peer cannot exhaust
    /// memory with a single oversized line. Returns <see langword="null"/> on EOF.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the line exceeds <see cref="MaxLineLength"/>.</exception>
    private string? ReadLimitedLine(StreamReader reader)
    {
        var sb = new System.Text.StringBuilder(256);
        int ch;
        while ((ch = reader.Read()) != -1)
        {
            char c = (char)ch;
            if (c == '\n')
            {
                if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
                    sb.Length--;
                return sb.ToString();
            }
            sb.Append(c);
            if (MaxLineLength > 0 && sb.Length > MaxLineLength)
                throw new InvalidDataException($"Incoming response line exceeded MaxLineLength ({MaxLineLength}).");
        }
        return sb.Length == 0 ? null : sb.ToString();
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
                    line = ReadLimitedLine(_reader);
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

                if (MaxLineLength > 0 && line.Length > MaxLineLength)
                {
                    Logger?.LogWarning("Incoming response line exceeded MaxLineLength ({MaxLineLength}); disconnecting.", MaxLineLength);
                    break;
                }

                ServerResponse response = ParseResponseLine(line);
                Logger?.LogDebug("Received: {Code} {Message}", SanitizeForLog(response.Code, 10), SanitizeForLog(response.Message ?? string.Empty, 200));
                _responseQueue.Enqueue(response);
                _responseSignal.Release();

                // Item 44: only raise the unsolicited event when there is no active command
                // waiter. When a command is in flight, its replies are consumed by SendCommandAsync.
                if (_activeCommandWaiters == 0)
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
            }
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
        if (_noOpInterval == Timeout.InfiniteTimeSpan || _listenTokenSource is null) return;
        _keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(_listenTokenSource.Token);
        CancellationToken ct = _keepAliveCts.Token;
        TimeSpan interval = _noOpInterval;
        _keepAliveTask = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(interval, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;
                    await SendNoOpAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Keep-alive loop terminated unexpectedly");
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
            // Ignore cancellation during shutdown.
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Keep-alive command failed");
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
    /// Resets the keep-alive timer by restarting the delay from the current point in time.
    /// </summary>
    protected void ResetKeepAlive()
    {
        // The loop-based keep-alive automatically resets after each send; calling
        // RestartKeepAlive restarts the delay whenever a command is successfully sent.
        RestartKeepAlive();
    }

    /// <summary>
    /// Releases the client resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the client resources.
    /// </summary>
    private void Dispose(bool disposing)
    {
        if (!disposing && _writer is null) return;

        Interlocked.Exchange(ref _state, StateDisposed);
        StopKeepAlive();
        _listenTokenSource?.Cancel();
        _reader?.Dispose();
        _reader = null;
        _writer?.Dispose();
        _writer = null;
        if (!_leaveOpen)
        {
            _stream?.Dispose();
        }
        _listenThread?.Join(TimeSpan.FromSeconds(1));
        _client?.Dispose();
        _responseSignal.Dispose();
        _sendLock.Dispose();
    }

    /// <summary>
    /// Deconstruct the client.
    /// </summary>
    ~CommandResponseClient()
    {
        Dispose(false);
    }

}

