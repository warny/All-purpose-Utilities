using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Client for the Post Office Protocol version 3 (POP3).
/// </summary>
public class Pop3Client : IDisposable
{
    private readonly CommandResponseClient _client;
    private bool _expectMultiLine;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pop3Client"/> class.
    /// </summary>
    public Pop3Client()
    {
        _client = new CommandResponseClient(ParseResponse);
    }

    /// <summary>
    /// Gets or sets the interval at which a NOOP command is automatically sent.
    /// </summary>
    public TimeSpan NoOpInterval
    {
        get => _client.NoOpInterval;
        set => _client.NoOpInterval = value;
    }

    /// <summary>
    /// Connects to the specified POP3 server using a TCP connection.
    /// </summary>
    /// <param name="host">Server host name or IP address.</param>
    /// <param name="port">Server port, default is 110.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(string host, int port = 110, CancellationToken cancellationToken = default)
    {
        await _client.ConnectAsync(host, port, cancellationToken);
        IReadOnlyList<ServerResponse> greeting = await _client.ReadAsync(cancellationToken);
        await EnsureOkAsync(greeting);
    }

    /// <summary>
    /// Uses the provided bidirectional <see cref="Stream"/> for communication.
    /// </summary>
    /// <param name="stream">Connected stream used to send commands and receive responses.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        await _client.ConnectAsync(stream, leaveOpen, cancellationToken);
        IReadOnlyList<ServerResponse> greeting = await _client.ReadAsync(cancellationToken);
        await EnsureOkAsync(greeting);
    }

    /// <summary>
    /// Authenticates the user with the POP3 server.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="password">Password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AuthenticateAsync(string user, string password, CancellationToken cancellationToken = default)
    {
        await EnsureOkAsync(await _client.SendCommandAsync($"USER {user}", cancellationToken));
        await EnsureOkAsync(await _client.SendCommandAsync($"PASS {password}", cancellationToken));
    }

    /// <summary>
    /// Retrieves mailbox statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing number of messages and total mailbox size.</returns>
    public async Task<(int messageCount, int mailboxSize)> GetStatAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await _client.SendCommandAsync("STAT", cancellationToken);
        await EnsureOkAsync(responses);
        string[] parts = responses[0].Message?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        int count = parts.Length > 0 ? int.Parse(parts[0]) : 0;
        int size = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        return (count, size);
    }

    /// <summary>
    /// Retrieves a list of messages with their sizes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping message number to its size.</returns>
    public async Task<IReadOnlyDictionary<int, int>> ListAsync(CancellationToken cancellationToken = default)
    {
        _expectMultiLine = true;
        IReadOnlyList<ServerResponse> responses = await _client.SendCommandAsync("LIST", cancellationToken);
        await EnsureOkAsync(responses);
        Dictionary<int, int> result = new();
        for (int i = 1; i < responses.Count - 1; i++)
        {
            string[] parts = responses[i].Message?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if (parts.Length >= 2 && int.TryParse(parts[0], out int id) && int.TryParse(parts[1], out int size))
            {
                result[id] = size;
            }
        }
        return result;
    }

    /// <summary>
    /// Retrieves the full text of a message.
    /// </summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Text of the message with dot-stuffing removed.</returns>
    public async Task<string> RetrieveAsync(int id, CancellationToken cancellationToken = default)
    {
        _expectMultiLine = true;
        IReadOnlyList<ServerResponse> responses = await _client.SendCommandAsync($"RETR {id}", cancellationToken);
        await EnsureOkAsync(responses);
        StringBuilder builder = new();
        for (int i = 1; i < responses.Count - 1; i++)
        {
            string line = responses[i].Message ?? string.Empty;
            if (line.StartsWith("..", StringComparison.Ordinal))
            {
                line = line[1..];
            }
            builder.AppendLine(line);
        }
        return builder.ToString();
    }

    /// <summary>
    /// Marks the specified message for deletion.
    /// </summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureOkAsync(await _client.SendCommandAsync($"DELE {id}", cancellationToken));
    }

    /// <summary>
    /// Sends the NOOP command.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task NoOpAsync(CancellationToken cancellationToken = default)
    {
        return _client.SendCommandAsync("NOOP", cancellationToken);
    }

    /// <summary>
    /// Sends the QUIT command and closes the connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task QuitAsync(CancellationToken cancellationToken = default)
    {
        return _client.DisconnectAsync("QUIT", TimeSpan.FromSeconds(5), cancellationToken);
    }

    /// <summary>
    /// Releases the resources used by the client.
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
    }

    /// <summary>
    /// Ensures that the last response in the sequence indicates success.
    /// </summary>
    /// <param name="responses">Responses to inspect.</param>
    private static Task EnsureOkAsync(IReadOnlyList<ServerResponse> responses)
    {
        if (responses.Count == 0 || responses[^1].Code < 200 || responses[^1].Code >= 300)
        {
            throw new IOException(responses.Count > 0 ? responses[^1].Message : "Server closed connection");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Parses POP3 response lines.
    /// </summary>
    /// <param name="line">Line received from the server.</param>
    /// <returns>Parsed response.</returns>
    private ServerResponse ParseResponse(string line)
    {
        if (_expectMultiLine)
        {
            if (line == ".")
            {
                _expectMultiLine = false;
                return new ServerResponse(200, string.Empty);
            }
            if (line.StartsWith("+OK", StringComparison.OrdinalIgnoreCase))
            {
                return new ServerResponse(100, line.Length > 3 ? line[3..].TrimStart() : string.Empty);
            }
            if (line.StartsWith("-ERR", StringComparison.OrdinalIgnoreCase))
            {
                _expectMultiLine = false;
                return new ServerResponse(500, line.Length > 4 ? line[4..].TrimStart() : string.Empty);
            }
            return new ServerResponse(100, line);
        }
        else
        {
            if (line.StartsWith("+OK", StringComparison.OrdinalIgnoreCase))
            {
                return new ServerResponse(200, line.Length > 3 ? line[3..].TrimStart() : string.Empty);
            }
            if (line.StartsWith("-ERR", StringComparison.OrdinalIgnoreCase))
            {
                return new ServerResponse(500, line.Length > 4 ? line[4..].TrimStart() : string.Empty);
            }
            return new ServerResponse(0, line);
        }
    }
}
