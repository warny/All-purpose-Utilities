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
    private CancellationTokenSource? _listenTokenSource;
    private Thread? _listenThread;
    private Task? _processTask;
    private readonly ConcurrentQueue<string> _commandQueue = new();
    private readonly SemaphoreSlim _commandSignal = new(0);
    private bool _leaveOpen;
    private readonly Dictionary<string, CommandRegistration> _handlers = new(StringComparer.OrdinalIgnoreCase);
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
    public Task StartAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
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
    /// Sends an unsolicited response to the client.
    /// </summary>
    /// <param name="response">Response to send.</param>
    public Task SendResponseAsync(ServerResponse response)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Server is not started.");
        }
        string line = _formatter(response);
        Logger?.LogInformation("Sending: {Line}", line);
        return _writer.WriteLineAsync(line);
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
                string? command = _reader.ReadLineAsync(cancellationToken).GetAwaiter().GetResult();
                if (command is null)
                {
                    _listenTokenSource?.Cancel();
                    break;
                }
                Logger?.LogInformation("Received: {Command}", command);
                _commandQueue.Enqueue(command);
                _commandSignal.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Listening canceled.
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
                await _commandSignal.WaitAsync(cancellationToken);
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
                string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
                IEnumerable<ServerResponse>? responses = null;
                if (_handlers.TryGetValue(verb, out CommandRegistration? registration))
                {
                    if (registration.RequiredContexts.All(c => _contexts.Contains(c)))
                    {
                        CommandContext ctx = new(_contexts);
                        responses = await registration.Handler(ctx, args);
                    }
                    else
                    {
                        responses = new[] { new ServerResponse("503", ResponseSeverity.PermanentNegative, "Bad sequence of commands") };
                    }
                }
                else if (CommandReceived is not null)
                {
                    responses = await CommandReceived.Invoke(command);
                }
                responses ??= new[] { new ServerResponse("502", ResponseSeverity.PermanentNegative, "Command not implemented") };
                List<ServerResponse> responseList = responses.ToList();
                foreach (ServerResponse response in responseList)
                {
                    string line = _formatter(response);
                    Logger?.LogInformation("Sending: {Line}", line);
                    await _writer.WriteLineAsync(line);
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
                            _listenTokenSource?.Cancel();
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
            _processTask?.Wait();
        }
        catch
        {
            // Ignore exceptions during shutdown.
        }
        _listenTokenSource?.Dispose();
        _commandSignal.Dispose();
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

