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
    private Timer? _keepAliveTimer;
    private TimeSpan _noOpInterval = Timeout.InfiniteTimeSpan;
    private string _noOpCommand = "NOOP";
    private bool _leaveOpen;
    private bool _disconnected;
    private TimeSpan _listenTimeout = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Gets or sets the logger used to trace client activity.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponseClient"/> class.
    /// </summary>
    public CommandResponseClient() { }

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
        set {
            _noOpInterval = value;
            _keepAliveTimer?.Change(value, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Gets or sets the timeout applied to read operations in the listener loop.
    /// </summary>
    public TimeSpan ListenTimeout
    {
        get => _listenTimeout;
        set {
            _listenTimeout = value;
            if (_stream is not null && _stream.CanTimeout)
            {
                _stream.ReadTimeout = value == Timeout.InfiniteTimeSpan ? -1 : (int)value.TotalMilliseconds;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the client is still connected.
    /// </summary>
    public bool IsConnected => !_disconnected;

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
    public virtual async Task ConnectAsync(string host, int port = -1, CancellationToken cancellationToken = default)
    {
        port = port == -1 ? DefaultPort : port;
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
    public virtual Task ConnectAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
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
    /// <exception cref="IOException">Thrown when the connection has been closed.</exception>
    public async Task<IReadOnlyList<ServerResponse>> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_disconnected)
            {
                throw new IOException("Connection closed.");
            }
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
    /// <exception cref="IOException">Thrown when the connection has been closed.</exception>
    public async Task<IReadOnlyList<ServerResponse>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (_disconnected && _responseSignal.CurrentCount == 0)
        {
            throw new IOException("Connection closed.");
        }
        List<ServerResponse> responses = new();
        await _responseSignal.WaitAsync(cancellationToken);
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
        while (await _responseSignal.WaitAsync(0));
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

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            foreach (string line in lines)
            {
                await _writer.WriteLineAsync(line);
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
                string? line;
                try
                {
                    line = _reader.ReadLine();
                }
                catch (IOException ex) when (ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.TimedOut)
                {
                    // Exit the loop when no data is received within the read timeout.
                    break;
                }

                if (line is null)
                {
                    break;
                }

                ServerResponse response = ParseResponseLine(line);
                Logger?.LogInformation("Received: {Code} {Message}", response.Code, response.Message);
                _responseQueue.Enqueue(response);
                _responseSignal.Release();
                UnsolicitedResponseReceived?.Invoke(response);
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
                    await _listenTokenSource?.CancelAsync();
                }
            }
            catch
            {
                await _listenTokenSource?.CancelAsync();
            }
        }
        else
        {
            await _listenTokenSource?.CancelAsync();
        }

        Dispose();
        Logger?.LogInformation("Disconnected");
    }

    /// <summary>
    /// Resets the keep alive timer.
    /// </summary>
    protected void ResetKeepAlive()
    {
        _keepAliveTimer?.Change(_noOpInterval, Timeout.InfiniteTimeSpan);
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
        _keepAliveTimer?.Dispose();
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

