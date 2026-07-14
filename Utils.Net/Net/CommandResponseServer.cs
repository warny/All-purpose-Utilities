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

    /// <summary>
    /// Gets or sets the maximum number of bytes allowed in a single incoming command line.
    /// Lines longer than this limit cause the session to close with a 500 error.
    /// Default is 8192 bytes (8 KiB).
    /// </summary>
    public int MaxLineLength { get; set; } = 8192;
    private readonly HashSet<string> _contexts = new();
    private readonly Func<ServerResponse, string> _formatter;
    private int _errorCount;

    /// <summary>
    /// Gets or sets the logger used to trace server activity.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive error responses allowed before the server shuts down.
    /// A value of <c>0</c> disables automatic shutdown.
    /// </summary>
    public int MaxConsecutiveErrors { get; set; }

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
    public event Func<string, Task<IEnumerable<ServerResponse>>>? CommandReceived;

    /// <summary>
    /// Registers a command handler.
    /// </summary>
    /// <param name="command">Command name.</param>
    /// <param name="handler">Handler invoked when the command is received.</param>
    /// <param name="requiredContexts">Contexts required for the command to execute.</param>
    public void RegisterCommand(string command, Func<CommandContext, string[], Task<IEnumerable<ServerResponse>>> handler, params string[] requiredContexts)
    {
        _handlers[command] = new CommandRegistration(handler, requiredContexts);
        Logger?.LogDebug("Command registered: {Command}", command);
    }

    /// <summary>
    /// Adds a context to the server.
    /// </summary>
    /// <param name="context">Context to add.</param>
    public void AddContext(string context)
    {
        _contexts.Add(context);
        Logger?.LogDebug("Context added: {Context}", context);
    }

    /// <summary>
    /// Removes a context from the server.
    /// </summary>
    /// <param name="context">Context to remove.</param>
    public void RemoveContext(string context)
    {
        _contexts.Remove(context);
        Logger?.LogDebug("Context removed: {Context}", context);
    }

    /// <summary>
    /// Determines whether the specified context is active.
    /// </summary>
    /// <param name="context">Context to check.</param>
    /// <returns><see langword="true"/> if the context is active; otherwise, <see langword="false"/>.</returns>
    public bool HasContext(string context) => _contexts.Contains(context);

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
        if (_listenThread is not null)
        {
            throw new InvalidOperationException(
                "This server instance is already running or has already been used. " +
                "Create a new instance for each incoming connection.");
        }
        _stream = stream;
        _leaveOpen = leaveOpen;
        _contexts.Clear();
        while (_commandQueue.TryDequeue(out _)) { }
        _errorCount = 0;
        _reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
        _writer = new StreamWriter(stream, Encoding.ASCII, 1024, true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };
        _listenTokenSource = new CancellationTokenSource();
        _listenThread = new Thread(() => ListenLoop(_listenTokenSource.Token))
        {
            IsBackground = true
        };
        _listenThread.Start();
        Logger?.LogInformation("Server started");
        _processTask = ProcessQueueAsync(_listenTokenSource.Token);
        return Task.CompletedTask;
    }

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
                string? command = _reader.ReadLine();
                if (command is null)
                {
                    _listenTokenSource?.Cancel();
                    break;
                }
                if (MaxLineLength > 0 && command.Length > MaxLineLength)
                {
                    Logger?.LogWarning("Incoming line exceeded MaxLineLength ({MaxLineLength}); closing session.", MaxLineLength);
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
                string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string verb = parts.Length > 0 ? parts[0] : string.Empty;
                string[] args = parts.Length > 1 ? parts[1..] : [];
                IEnumerable<ServerResponse>? responses = null;
                if (_handlers.TryGetValue(verb, out CommandRegistration? registration))
                {
                    if (registration.RequiredContexts.All(_contexts.Contains))
                    {
                        CommandContext ctx = new(_contexts);
                        try
                        {
                            responses = await registration.Handler(ctx, args).ConfigureAwait(false);
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
                }
                else if (CommandReceived is not null)
                {
                    try
                    {
                        responses = await CommandReceived.Invoke(command).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "CommandReceived handler threw an unhandled exception for command {Verb}", verb);
                        responses = [new ServerResponse("500", ResponseSeverity.PermanentNegative, "Internal server error")];
                    }
                }
                responses ??= [new ServerResponse("502", ResponseSeverity.PermanentNegative, "Command not implemented")];
                List<ServerResponse> responseList = responses.ToList();
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
        Func<CommandContext, string[], Task<IEnumerable<ServerResponse>>> Handler,
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

