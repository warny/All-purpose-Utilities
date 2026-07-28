using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Utils.Net;

/// <summary>
/// Provides a base server for text based command/response protocols.
/// </summary>
public class CommandResponseServer : IDisposable
{
    private Stream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private CancellationTokenSource? _listenTokenSource;
    private Thread? _listenThread;
    private Task? _processTask;
    private readonly ConcurrentQueue<string> _commandQueue = new();
    private readonly SemaphoreSlim _commandSignal = new(0);
    private bool _leaveOpen;
    private readonly Dictionary<string, CommandRegistration> _handlers = new(StringComparer.OrdinalIgnoreCase);

    // Item 31: lifecycle state machine — 0 NotStarted, 1 Starting, 2 Running, 3 Stopped.
    private const int StateNotStarted = 0;
    private const int StateStarting = 1;
    private const int StateRunning = 2;
    private const int StateStopped = 3;
    private int _state = StateNotStarted;

    private int _maxLineLength = 8192;

    /// <summary>
    /// Gets or sets the maximum number of bytes allowed in a single incoming command line.
    /// Lines longer than this limit cause the session to close with a 500 error.
    /// Default is 8192 bytes (8 KiB).
    /// </summary>
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

    private int _maxCommandQueueDepth = 1000;

    /// <summary>
    /// Gets or sets the maximum number of commands that may wait in the command queue before
    /// the server closes the session to prevent unbounded memory consumption.
    /// Default is 1000. Set to 0 to disable.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int MaxCommandQueueDepth
    {
        get => _maxCommandQueueDepth;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "MaxCommandQueueDepth must be non-negative.");
            _maxCommandQueueDepth = value;
        }
    }

    private readonly HashSet<string> _contexts = new();
    private readonly Func<ServerResponse, string> _formatter;
    private int _errorCount;
    private volatile bool _closeAfterResponse;

    /// <summary>
    /// Gets or sets the logger used to trace server activity.
    /// </summary>
    public ILogger? Logger { get; set; }

    private int _maxConsecutiveErrors;

    /// <summary>
    /// Gets or sets the number of consecutive error responses allowed before the server shuts down.
    /// A value of <c>0</c> disables automatic shutdown.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int MaxConsecutiveErrors
    {
        get => _maxConsecutiveErrors;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "MaxConsecutiveErrors must be non-negative.");
            _maxConsecutiveErrors = value;
        }
    }

    /// <summary>
    /// Validates that <paramref name="value"/> is a non-empty token free of whitespace and
    /// control characters, so it can safely be used as a command verb or context name.
    /// </summary>
    /// <param name="value">Token to validate.</param>
    /// <param name="paramName">Parameter name used in the thrown exception.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is empty or contains whitespace or control characters.
    /// </exception>
    private static void ValidateToken(string value, string paramName)
    {
        if (value is null) throw new ArgumentNullException(paramName);
        if (value.Length == 0) throw new ArgumentException($"{paramName} must not be empty.", paramName);
        foreach (char c in value)
        {
            if (c <= ' ' || c == 0x7F)
                throw new ArgumentException($"{paramName} must not contain whitespace or control characters.", paramName);
        }
    }

    /// <summary>
    /// Item 33: throws if the server has already been started, freezing the static configuration
    /// (registered commands and initial contexts) for the lifetime of the session.
    /// </summary>
    /// <param name="memberName">Name of the member being guarded, used in the exception message.</param>
    /// <exception cref="InvalidOperationException">Thrown when called after <see cref="StartAsync"/>.</exception>
    private void RequireNotStarted(string memberName)
    {
        int state = Volatile.Read(ref _state);
        if (state != StateNotStarted)
            throw new InvalidOperationException($"{memberName} must not be called after StartAsync has been called.");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponseServer"/> class.
    /// </summary>
    /// <param name="formatter">Optional formatter used to convert responses to textual lines.</param>
    public CommandResponseServer(Func<ServerResponse, string>? formatter = null)
    {
        _formatter = formatter ?? DefaultFormatter;
    }

    /// <summary>
    /// Formats a response using the default numeric code representation.
    /// </summary>
    /// <param name="response">Response to format.</param>
    /// <returns>Formatted textual line.</returns>
    private static string DefaultFormatter(ServerResponse response)
    {
        return response.Message is null ? response.Code : $"{response.Code} {response.Message}";
    }

    /// <summary>
    /// Occurs when a command is received from the client. The handler must return the responses to send.
    /// Returning an empty sequence results in no response being written to the client.
    /// </summary>
    public event Func<string, CancellationToken, Task<IEnumerable<ServerResponse>>>? CommandReceived;

    /// <summary>
    /// Registers a command handler. Must be called before <see cref="StartAsync"/>.
    /// </summary>
    /// <param name="command">Command name.</param>
    /// <param name="handler">Handler invoked when the command is received.</param>
    /// <param name="requiredContexts">Contexts required for the command to execute.</param>
    /// <exception cref="InvalidOperationException">Thrown when called after <see cref="StartAsync"/>.</exception>
    public void RegisterCommand(string command, Func<CommandContext, string[], CancellationToken, Task<IEnumerable<ServerResponse>>> handler, params string[] requiredContexts)
    {
        ValidateToken(command, nameof(command));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        RequireNotStarted(nameof(RegisterCommand));
        string[] contextsCopy = requiredContexts is null ? [] : (string[])requiredContexts.Clone();
        foreach (string ctx in contextsCopy) ValidateToken(ctx, nameof(requiredContexts));
        _handlers[command] = new CommandRegistration(handler, contextsCopy);
        Logger?.LogDebug("Command registered: {Command}", command);
    }

    /// <summary>
    /// Registers a command handler (backward-compatible overload without cancellation token).
    /// </summary>
    /// <param name="command">Command name.</param>
    /// <param name="handler">Handler invoked when the command is received.</param>
    /// <param name="requiredContexts">Contexts required for the command to execute.</param>
    public void RegisterCommand(string command, Func<CommandContext, string[], Task<IEnumerable<ServerResponse>>> handler, params string[] requiredContexts)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        RegisterCommand(command, (ctx, args, _) => handler(ctx, args), requiredContexts);
    }

    /// <summary>
    /// Adds an initial context to the server. Must be called before <see cref="StartAsync"/>.
    /// During a live session, contexts are managed through <see cref="CommandContext"/> inside a handler.
    /// </summary>
    /// <param name="context">Context to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when called after <see cref="StartAsync"/>.</exception>
    public void AddContext(string context)
    {
        ValidateToken(context, nameof(context));
        RequireNotStarted(nameof(AddContext));
        _contexts.Add(context);
        Logger?.LogDebug("Context added: {Context}", context);
    }

    /// <summary>
    /// Removes an initial context from the server. Must be called before <see cref="StartAsync"/>.
    /// During a live session, contexts are managed through <see cref="CommandContext"/> inside a handler.
    /// </summary>
    /// <param name="context">Context to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when called after <see cref="StartAsync"/>.</exception>
    public void RemoveContext(string context)
    {
        ValidateToken(context, nameof(context));
        RequireNotStarted(nameof(RemoveContext));
        _contexts.Remove(context);
        Logger?.LogDebug("Context removed: {Context}", context);
    }

    /// <summary>
    /// Determines whether the specified context is active.
    /// </summary>
    /// <param name="context">Context to check.</param>
    /// <returns><see langword="true"/> if the context is active; otherwise, <see langword="false"/>.</returns>
    public bool HasContext(string context)
    {
        ValidateToken(context, nameof(context));
        return _contexts.Contains(context);
    }

    /// <summary>
    /// Starts processing commands using the specified stream.
    /// </summary>
    /// <param name="stream">Bi-directional stream connected to the client.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the server is already running. <see cref="CommandResponseServer"/> instances
    /// are single-use; create a new instance for each incoming connection.
    /// </exception>
    public Task StartAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        // Item 31: atomic single-use guard — only one caller can transition from NotStarted to Starting.
        int prev = Interlocked.CompareExchange(ref _state, StateStarting, StateNotStarted);
        if (prev != StateNotStarted)
        {
            throw new InvalidOperationException(
                "This server instance is already running or has already been used. " +
                "Create a new instance for each incoming connection.");
        }
        try
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            _stream = stream;
            _leaveOpen = leaveOpen;
            // Item 33: initial contexts configured before startup are preserved; they must not be cleared here.
            while (_commandQueue.TryDequeue(out _)) { }
            _errorCount = 0;
            _reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            _writer = new StreamWriter(stream, Encoding.ASCII, 1024, true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };
            _listenTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listenThread = new Thread(() => ListenLoop(_listenTokenSource.Token))
            {
                IsBackground = true
            };
            _listenThread.Start();
            Logger?.LogInformation("Server started");
            _processTask = ProcessQueueAsync(_listenTokenSource.Token);
            Interlocked.Exchange(ref _state, StateRunning);
            return Task.CompletedTask;
        }
        catch
        {
            // Initialization failed: mark as stopped so the instance cannot be reused.
            Interlocked.Exchange(ref _state, StateStopped);
            _reader?.Dispose();
            _writer?.Dispose();
            _listenTokenSource?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Requests that the server close the connection after the current response has been sent.
    /// Safe to call from within a command handler.
    /// </summary>
    /// <remarks>
    /// The flag is checked in <c>ProcessQueueAsync</c> immediately after ALL response lines for
    /// the current command have been flushed to the stream. This guarantees the final response
    /// is received by the peer before the TCP session is torn down.
    /// Handlers must NOT call <see cref="Dispose"/> or cancel the listen token directly: those
    /// would race with <c>ProcessQueueAsync</c>'s write lock and risk dropping the in-flight
    /// response before it reaches the client.
    /// </remarks>
    public void CloseAfterResponse() => _closeAfterResponse = true;

    /// <summary>
    /// Gets a task that completes when the server stops processing commands.
    /// </summary>
    public Task Completion => _processTask ?? Task.CompletedTask;

    /// <summary>
    /// Returns a loggable (redacted) representation of a line received from the client.
    /// The default implementation logs only the verb (first space-separated word) to avoid
    /// accidentally exposing secret-bearing lines such as AUTH continuations, PASS arguments
    /// or Base64-encoded credential payloads.
    /// Override in a protocol-aware subclass to log more detail for lines that are known safe.
    /// </summary>
    /// <param name="line">Raw line received from the client.</param>
    /// <returns>A string safe to write to the log.</returns>
    protected virtual string RedactLineForLog(string line)
    {
        int space = line.IndexOf(' ');
        string verb = space >= 0 ? line[..space] : line;
        string suffix = space >= 0 ? " [...]" : string.Empty;
        return SanitizeForLog(verb) + suffix;
    }

    /// <summary>
    /// Replaces control characters with '?' and truncates the value to
    /// <paramref name="maxLength"/> characters to prevent log injection or flooding.
    /// </summary>
    private static string SanitizeForLog(string value, int maxLength = 100)
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
    /// Sends an unsolicited response to the client.
    /// </summary>
    /// <param name="response">Response to send.</param>
    public async Task SendResponseAsync(ServerResponse response)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Server is not started.");
        }
        string line = _formatter(response);
        Logger?.LogDebug("Sending: {Line}", line);
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(line).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Listens for commands from the client on a dedicated thread and enqueues them for processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <summary>
    /// Reads one line from <paramref name="reader"/>, enforcing <see cref="MaxLineLength"/> during
    /// the read rather than after the full line has been buffered. Returns <see langword="null"/> on EOF.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the line exceeds <see cref="MaxLineLength"/>.</exception>
    private string? ReadLimitedLine(StreamReader reader)
    {
        var sb = new StringBuilder(256);
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
                throw new InvalidDataException($"Incoming line exceeded MaxLineLength ({MaxLineLength}).");
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

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
                string? command;
                try
                {
                    command = ReadLimitedLine(_reader);
                }
                catch (InvalidDataException)
                {
                    Logger?.LogWarning("Incoming line exceeded MaxLineLength ({MaxLineLength}); closing session.", MaxLineLength);
                    _listenTokenSource?.Cancel();
                    break;
                }
                if (command is null)
                {
                    _listenTokenSource?.Cancel();
                    break;
                }
                if (MaxCommandQueueDepth > 0 && _commandQueue.Count >= MaxCommandQueueDepth)
                {
                    Logger?.LogWarning("Command queue depth exceeded MaxCommandQueueDepth ({MaxCommandQueueDepth}); closing session.", MaxCommandQueueDepth);
                    _listenTokenSource?.Cancel();
                    break;
                }
                Logger?.LogDebug("Received: {Command}", RedactLineForLog(command));
                _commandQueue.Enqueue(command);
                _commandSignal.Release();
            }
        }
        catch (IOException)
        {
            // Connection closed.
            _listenTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Stream disposed.
            _listenTokenSource?.Cancel();
        }
        finally
        {
            _commandSignal.Release();
            Logger?.LogWarning("Listener thread terminated");
        }
    }

    /// <summary>
    /// Processes queued commands and sends responses to the client.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        if (_writer is null)
        {
            return;
        }
        try
        {
            while (true)
            {
                await _commandSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                if (!_commandQueue.TryDequeue(out string? command))
                {
                    continue;
                }
                try
                {
                string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string verb = parts.Length > 0 ? parts[0] : string.Empty;
                string[] args = parts.Length > 1 ? parts[1..] : [];
                IEnumerable<ServerResponse>? responses = null;
                List<ServerResponse> responseList;
                if (_handlers.TryGetValue(verb, out CommandRegistration? registration))
                {
                    if (registration.RequiredContexts.All(_contexts.Contains))
                    {
                        CommandContext ctx = new(_contexts);
                        try
                        {
                            responses = await registration.Handler(ctx, args, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "Handler for {Verb} threw an unhandled exception", verb);
                            responses = [new ServerResponse("500", ResponseSeverity.PermanentNegative, "Internal server error")];
                        }
                    }
                    else
                    {
                        responses = [new ServerResponse("503", ResponseSeverity.PermanentNegative, "Bad sequence of commands")];
                    }
                    responses ??= [new ServerResponse("502", ResponseSeverity.PermanentNegative, "Command not implemented")];
                    responseList = responses.ToList();
                }
                else if (CommandReceived is not null)
                {
                    // Item 32: await every CommandReceived subscriber in registration order.
                    // The last non-empty response sequence wins; the first faulting subscriber
                    // aborts the chain and yields a 500 reply.
                    Delegate[] invocations = CommandReceived.GetInvocationList();
                    responseList = [new ServerResponse("502", ResponseSeverity.PermanentNegative, "Command not implemented")];
                    foreach (Delegate d in invocations)
                    {
                        var subscriber = (Func<string, CancellationToken, Task<IEnumerable<ServerResponse>>>)d;
                        try
                        {
                            IEnumerable<ServerResponse> sr = await subscriber(command, cancellationToken).ConfigureAwait(false);
                            List<ServerResponse> candidate = sr?.ToList() ?? [];
                            if (candidate.Count > 0)
                                responseList = candidate;
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "CommandReceived subscriber threw an unhandled exception for command {Verb}", verb);
                            responseList = [new ServerResponse("500", ResponseSeverity.PermanentNegative, "Internal server error")];
                            break; // stop on first faulting subscriber
                        }
                    }
                }
                else
                {
                    responseList = [new ServerResponse("502", ResponseSeverity.PermanentNegative, "Command not implemented")];
                }
                await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    foreach (ServerResponse response in responseList)
                    {
                        string line = _formatter(response);
                        Logger?.LogDebug("Sending: {Line}", line);
                        await _writer.WriteLineAsync(line).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _writeLock.Release();
                }

                if (_closeAfterResponse)
                {
                    await (_listenTokenSource?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
                    break;
                }
                if (responseList.Count > 0 && MaxConsecutiveErrors > 0)
                {
                    ResponseSeverity finalSeverity = responseList[^1].Severity;
                    if (finalSeverity >= ResponseSeverity.TransientNegative)
                    {
                        _errorCount++;
                        if (_errorCount >= MaxConsecutiveErrors)
                        {
                            Logger?.LogWarning("Maximum consecutive errors reached");
                            await (_listenTokenSource?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
                            break;
                        }
                    }
                    else
                    {
                        _errorCount = 0;
                    }
                }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Item 34: any non-cancellation, non-handler fault (formatter, stream write, etc.)
                    // is terminal: cancel the listener and re-throw so Completion faults with the
                    // original exception rather than completing silently.
                    Logger?.LogError(ex, "Unhandled exception in command processing loop; terminating session");
                    await (_listenTokenSource?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
                    throw;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Processing canceled.
        }
    }

    /// <summary>
    /// Releases server resources.
    /// </summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _state, StateStopped);
        _listenTokenSource?.Cancel();
        _reader?.Dispose();
        _writer?.Dispose();
        if (!_leaveOpen)
        {
            _stream?.Dispose();
        }
        _listenThread?.Join(TimeSpan.FromSeconds(1));
        try
        {
            _processTask?.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Ignore exceptions during shutdown.
        }
        _listenTokenSource?.Dispose();
        _commandSignal.Dispose();
        _writeLock.Dispose();
        Logger?.LogInformation("Server stopped");
    }

    /// <summary>
    /// Represents a registered command handler.
    /// </summary>
    private sealed record CommandRegistration(
        Func<CommandContext, string[], CancellationToken, Task<IEnumerable<ServerResponse>>> Handler,
        IReadOnlyCollection<string> RequiredContexts);
}

/// <summary>
/// Provides access to the server contexts during command execution.
/// </summary>
public sealed class CommandContext
{
    private readonly HashSet<string> _contexts;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandContext"/> class.
    /// </summary>
    /// <param name="contexts">Active contexts.</param>
    internal CommandContext(HashSet<string> contexts) => _contexts = contexts;

    /// <summary>
    /// Adds a context to the active set.
    /// </summary>
    /// <param name="context">Context to add.</param>
    public void Add(string context) => _contexts.Add(context);

    /// <summary>
    /// Removes a context from the active set.
    /// </summary>
    /// <param name="context">Context to remove.</param>
    public void Remove(string context) => _contexts.Remove(context);

    /// <summary>
    /// Determines whether the specified context is active.
    /// </summary>
    /// <param name="context">Context to check.</param>
    /// <returns><see langword="true"/> if the context is active; otherwise, <see langword="false"/>.</returns>
    public bool Has(string context) => _contexts.Contains(context);
}

