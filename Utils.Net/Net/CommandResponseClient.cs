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
    private readonly Func<string, ServerResponse> _parser;
    private TcpClient? _client;
    private Stream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly ConcurrentQueue<ServerResponse> _responseQueue = new();
    private readonly SemaphoreSlim _responseSignal = new(0);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _listenTokenSource;
    private Thread? _listenThread;
    private Timer? _keepAliveTimer;
    private TimeSpan _noOpInterval = Timeout.InfiniteTimeSpan;
    private string _noOpCommand = "NOOP";
    private bool _leaveOpen;
    private bool _disconnected;

    /// <summary>
    /// Gets or sets the logger used to trace client activity.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponseClient"/> class.
    /// </summary>
    /// <param name="parser">Optional delegate used to parse response lines. When null, a numeric code parser is used.</param>
    public CommandResponseClient(Func<string, ServerResponse>? parser = null)
    {
        _parser = parser ?? DefaultParser;
    }

    /// <summary>
    /// Occurs when a response is received from the server.
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
            _keepAliveTimer?.Change(value, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Connects to the specified host and port using a TCP connection.
    /// </summary>
    /// <param name="host">Server host name or IP address.</param>
    /// <param name="port">Server port.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        Logger?.LogInformation("Connecting to {Host}:{Port}", host, port);
        _client = new TcpClient();
        await _client.ConnectAsync(host, port, cancellationToken);
        await ConnectAsync(_client.GetStream(), false, cancellationToken);
    }

    /// <summary>
    /// Uses the provided bidirectional <see cref="Stream"/> for communication.
    /// </summary>
    /// <param name="stream">Connected stream used to send commands and receive responses.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task ConnectAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
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
        Logger?.LogInformation("Client connected to stream");
        if (_noOpInterval != Timeout.InfiniteTimeSpan)
        {
            _keepAliveTimer = new Timer(async _ => await SendNoOpAsync(), null, _noOpInterval, Timeout.InfiniteTimeSpan);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a command and collects responses until a response with at least completion severity is received.
    /// </summary>
    /// <param name="command">Command to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of responses returned by the server.</returns>
    public async Task<IReadOnlyList<ServerResponse>> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            DrainPendingResponses();
            Logger?.LogInformation("Sending: {Command}", command);
            await _writer.WriteLineAsync(command);
            List<ServerResponse> responses = new();
            while (true)
            {
                await _responseSignal.WaitAsync(cancellationToken);
                if (!_responseQueue.TryDequeue(out ServerResponse response))
                {
                    if (_disconnected)
                    {
                        throw new IOException("Connection closed.");
                    }
                    continue;
                }
                responses.Add(response);
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
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Reads and returns responses that have been received without sending a command.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of responses read from the server.</returns>
    public async Task<IReadOnlyList<ServerResponse>> ReadAsync(CancellationToken cancellationToken = default)
    {
        List<ServerResponse> responses = new();
        await _responseSignal.WaitAsync(cancellationToken);
        do
        {
            if (_responseQueue.TryDequeue(out ServerResponse response))
            {
                responses.Add(response);
            }
        }
        while (_responseSignal.Wait(0));
        ResetKeepAlive();
        return responses;
    }

    /// <summary>
    /// Removes any queued responses that were not consumed by previous commands.
    /// </summary>
    private void DrainPendingResponses()
    {
        while (_responseQueue.TryDequeue(out ServerResponse leftover))
        {
            UnsolicitedResponseReceived?.Invoke(leftover);
        }
        while (_responseSignal.CurrentCount > 0)
        {
            _responseSignal.Wait(0);
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
                string? line = _reader.ReadLineAsync(cancellationToken).GetAwaiter().GetResult();
                if (line is null)
                {
                    break;
                }
                ServerResponse response = _parser(line);
                Logger?.LogInformation("Received: {Code} {Message}", response.Code, response.Message);
                _responseQueue.Enqueue(response);
                _responseSignal.Release();
                UnsolicitedResponseReceived?.Invoke(response);
            }
        }
        catch (OperationCanceledException)
        {
            // Listening was canceled.
        }
        finally
        {
            _disconnected = true;
            _responseSignal.Release();
            Logger?.LogWarning("Listener thread terminated");
        }
    }

    /// <summary>
    /// Default parser that extracts a numeric status code and optional text from a response line.
    /// </summary>
    /// <param name="line">Response line from the server.</param>
    /// <returns>Parsed response.</returns>
    private static ServerResponse DefaultParser(string line)
    {
        if (line.Length >= 3 && char.IsDigit(line[0]))
        {
            string code = line[..3];
            string? text = line.Length > 4 ? line[4..] : string.Empty;
            return new ServerResponse(code, text);
        }
        return new ServerResponse(string.Empty, ResponseSeverity.Unknown, line);
    }

    /// <summary>
    /// Sends the no-op command.
    /// </summary>
    private async Task SendNoOpAsync()
    {
        try
        {
            Logger?.LogDebug("Sending keep-alive: {Command}", _noOpCommand);
            await SendCommandAsync(_noOpCommand);
        }
        catch
        {
            // Ignore keep-alive exceptions.
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
                IReadOnlyList<ServerResponse> responses = await SendCommandAsync(command, cts.Token);
                if (responses.Count == 0 || responses[^1].Severity != ResponseSeverity.Completion)
                {
                    // Force disconnect on missing positive reply.
                    _listenTokenSource?.Cancel();
                }
            }
            catch
            {
                _listenTokenSource?.Cancel();
            }
        }
        else
        {
            _listenTokenSource?.Cancel();
        }

        Dispose();
        Logger?.LogInformation("Disconnected");
    }

    /// <summary>
    /// Resets the keep alive timer.
    /// </summary>
    private void ResetKeepAlive()
    {
        _keepAliveTimer?.Change(_noOpInterval, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Releases the client resources.
    /// </summary>
    public void Dispose()
    {
        _listenTokenSource?.Cancel();
        _listenThread?.Join();
        _keepAliveTimer?.Dispose();
        _reader?.Dispose();
        _writer?.Dispose();
        if (!_leaveOpen)
        {
            _stream?.Dispose();
        }
        _client?.Dispose();
        _responseSignal.Dispose();
        _sendLock.Dispose();
    }
}

