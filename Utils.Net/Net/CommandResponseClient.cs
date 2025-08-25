using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    private Timer? _keepAliveTimer;
    private TimeSpan _noOpInterval = Timeout.InfiniteTimeSpan;
    private string _noOpCommand = "NOOP";
    private bool _leaveOpen;

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
        _ = Task.Run(() => ListenAsync(_listenTokenSource.Token), cancellationToken);
        if (_noOpInterval != Timeout.InfiniteTimeSpan)
        {
            _keepAliveTimer = new Timer(async _ => await SendNoOpAsync(), null, _noOpInterval, Timeout.InfiniteTimeSpan);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a command and collects responses until a non 1xx status code is received.
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
            await _writer.WriteLineAsync(command);
            List<ServerResponse> responses = new();
            while (true)
            {
                await _responseSignal.WaitAsync(cancellationToken);
                if (!_responseQueue.TryDequeue(out ServerResponse response))
                {
                    continue;
                }
                responses.Add(response);
                if (response.Code < 100 || response.Code >= 200)
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
    /// Listens for responses from the server and enqueues them for processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
        {
            return;
        }
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await _reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }
            ServerResponse response = ParseResponse(line);
            _responseQueue.Enqueue(response);
            _responseSignal.Release();
            UnsolicitedResponseReceived?.Invoke(response);
        }
    }

    /// <summary>
    /// Parses a response line into a <see cref="ServerResponse"/> structure.
    /// </summary>
    /// <param name="line">Response line from the server.</param>
    /// <returns>Parsed response.</returns>
    private static ServerResponse ParseResponse(string line)
    {
        int code = 0;
        string? text = null;
        if (line.Length >= 3 && int.TryParse(line[..3], out int parsed))
        {
            code = parsed;
            text = line.Length > 4 ? line[4..] : string.Empty;
        }
        return new ServerResponse(code, text);
    }

    /// <summary>
    /// Sends the no-op command.
    /// </summary>
    private async Task SendNoOpAsync()
    {
        try
        {
            await SendCommandAsync(_noOpCommand);
        }
        catch
        {
            // Ignore keep-alive exceptions.
        }
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

