using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

    /// <summary>
    /// Occurs when a command is received from the client. The handler must return the responses to send.
    /// </summary>
    public event Func<string, Task<IEnumerable<ServerResponse>>>? CommandReceived;

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
        string line = response.Message is null ? $"{response.Code:D3}" : $"{response.Code:D3} {response.Message}";
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
                    break;
                }
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
        while (!cancellationToken.IsCancellationRequested)
        {
            await _commandSignal.WaitAsync(cancellationToken);
            if (!_commandQueue.TryDequeue(out string? command))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                continue;
            }
            IEnumerable<ServerResponse>? responses = null;
            if (CommandReceived is not null)
            {
                responses = await CommandReceived.Invoke(command);
            }
            responses ??= new[] { new ServerResponse(500, "Command handler not set") };
            foreach (ServerResponse response in responses)
            {
                string line = response.Message is null ? $"{response.Code:D3}" : $"{response.Code:D3} {response.Message}";
                await _writer.WriteLineAsync(line);
            }
        }
    }

    /// <summary>
    /// Releases server resources.
    /// </summary>
    public void Dispose()
    {
        _listenTokenSource?.Cancel();
        _listenThread?.Join();
        try
        {
            _processTask?.Wait();
        }
        catch
        {
            // Ignore exceptions during shutdown.
        }
        _reader?.Dispose();
        _writer?.Dispose();
        if (!_leaveOpen)
        {
            _stream?.Dispose();
        }
        _listenTokenSource?.Dispose();
        _commandSignal.Dispose();
    }
}

