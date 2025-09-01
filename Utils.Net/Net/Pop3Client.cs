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
public class Pop3Client : CommandResponseClient
{
    private string? _timestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pop3Client"/> class.
    /// </summary>
    public Pop3Client()
    {
    }

    /// <summary>
    /// Connects to the specified POP3 server using a TCP connection.
    /// </summary>
    /// <param name="host">Server host name or IP address.</param>
    /// <param name="port">Server port, default is 110.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(string host, int port = 110, CancellationToken cancellationToken = default)
    {
        await base.ConnectAsync(host, port, cancellationToken);
        IReadOnlyList<ServerResponse> greeting = await ReadAsync(cancellationToken);
        await EnsureOkAsync(greeting);
        _timestamp = ExtractTimestamp(greeting);
    }

    /// <summary>
    /// Uses the provided bidirectional <see cref="Stream"/> for communication.
    /// </summary>
    /// <param name="stream">Connected stream used to send commands and receive responses.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        await base.ConnectAsync(stream, leaveOpen, cancellationToken);
        IReadOnlyList<ServerResponse> greeting = await ReadAsync(cancellationToken);
        await EnsureOkAsync(greeting);
        _timestamp = ExtractTimestamp(greeting);
    }

    /// <summary>
    /// Authenticates the user with the POP3 server.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="password">Password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AuthenticateAsync(string user, string password, CancellationToken cancellationToken = default)
    {
        await EnsureOkAsync(await SendCommandAsync($"USER {user}", cancellationToken));
        await EnsureOkAsync(await SendCommandAsync($"PASS {password}", cancellationToken));
    }

    /// <summary>
    /// Authenticates the user using the APOP challenge-response mechanism.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="password">Password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AuthenticateApopAsync(string user, string password, CancellationToken cancellationToken = default)
    {
        if (_timestamp is null)
        {
            throw new InvalidOperationException("Server greeting did not contain APOP timestamp");
        }
        using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.ASCII.GetBytes(_timestamp + password));
        string digest = Convert.ToHexString(hash).ToLowerInvariant();
        await EnsureOkAsync(await SendCommandAsync($"APOP {user} {digest}", cancellationToken));
    }

    /// <summary>
    /// Retrieves mailbox statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing number of messages and total mailbox size.</returns>
    public async Task<(int messageCount, int mailboxSize)> GetStatAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync("STAT", cancellationToken);
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
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync("LIST", cancellationToken);
        await EnsureOkAsync(responses);
        IReadOnlyList<string> lines = await ReadMultilineAsync(cancellationToken);
        Dictionary<int, int> result = new();
        foreach (string line in lines)
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"RETR {id}", cancellationToken);
        await EnsureOkAsync(responses);
        StringBuilder builder = new();
        IReadOnlyList<string> lines = await ReadMultilineAsync(cancellationToken);
        foreach (string line in lines)
        {
            string content = line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line;
            builder.AppendLine(content);
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
        await EnsureOkAsync(await SendCommandAsync($"DELE {id}", cancellationToken));
    }

    /// <summary>
    /// Resets the deletion marks for all messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureOkAsync(await SendCommandAsync("RSET", cancellationToken));
    }

    /// <summary>
    /// Sends the NOOP command.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task NoOpAsync(CancellationToken cancellationToken = default)
    {
        return SendCommandAsync("NOOP", cancellationToken);
    }

    /// <summary>
    /// Sends the QUIT command and closes the connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task QuitAsync(CancellationToken cancellationToken = default)
    {
        return DisconnectAsync("QUIT", TimeSpan.FromSeconds(5), cancellationToken);
    }

    /// <summary>
    /// Retrieves the server capabilities.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of capability names.</returns>
    public async Task<IReadOnlyList<string>> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync("CAPA", cancellationToken);
        await EnsureOkAsync(responses);
        return await ReadMultilineAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves unique identifiers for all messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping message numbers to unique identifiers.</returns>
    public async Task<IReadOnlyDictionary<int, string>> ListUniqueIdsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync("UIDL", cancellationToken);
        await EnsureOkAsync(responses);
        IReadOnlyList<string> lines = await ReadMultilineAsync(cancellationToken);
        Dictionary<int, string> result = new();
        foreach (string line in lines)
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[0], out int id))
            {
                result[id] = parts[1];
            }
        }
        return result;
    }

    /// <summary>
    /// Retrieves the unique identifier for a single message.
    /// </summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unique identifier or <see langword="null"/> if not found.</returns>
    public async Task<string?> GetUniqueIdAsync(int id, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"UIDL {id}", cancellationToken);
        await EnsureOkAsync(responses);
        string[] parts = responses[0].Message?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return parts.Length > 1 ? parts[1] : null;
    }

    /// <summary>
    /// Ensures that the last response in the sequence indicates success.
    /// </summary>
    /// <param name="responses">Responses to inspect.</param>
    private static Task EnsureOkAsync(IReadOnlyList<ServerResponse> responses)
    {
        if (responses.Count == 0 || responses[^1].Severity != ResponseSeverity.Completion)
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
    protected override ServerResponse ParseResponseLine(string line)
    {
        (string code, string? message) = SplitCodeAndMessage(line);
        if (string.Equals(code, "+OK", StringComparison.OrdinalIgnoreCase))
        {
            return new ServerResponse(code, ResponseSeverity.Completion, message ?? string.Empty);
        }
        if (string.Equals(code, "-ERR", StringComparison.OrdinalIgnoreCase))
        {
            return new ServerResponse(code, ResponseSeverity.PermanentNegative, message ?? string.Empty);
        }
        return new ServerResponse(code, ResponseSeverity.Unknown, message);
    }

    /// <summary>
    /// Reads lines from the server until a single dot line is encountered.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of lines excluding the terminating dot.</returns>
    private async Task<IReadOnlyList<string>> ReadMultilineAsync(CancellationToken cancellationToken)
    {
        List<string> lines = new();
        while (true)
        {
            IReadOnlyList<ServerResponse> batch = await ReadAsync(cancellationToken);
            foreach (ServerResponse response in batch)
            {
                string line = response.Message is null ? response.Code : $"{response.Code} {response.Message}";
                if (line == ".")
                {
                    return lines;
                }
                lines.Add(line);
            }
        }
    }

    /// <summary>
    /// Extracts the APOP timestamp from the greeting responses.
    /// </summary>
    /// <param name="responses">Greeting responses.</param>
    /// <returns>Timestamp string or <see langword="null"/> if not found.</returns>
    private static string? ExtractTimestamp(IReadOnlyList<ServerResponse> responses)
    {
        if (responses.Count == 0)
        {
            return null;
        }
        string? msg = responses[0].Message;
        if (msg is null)
        {
            return null;
        }
        int start = msg.IndexOf('<');
        int end = msg.IndexOf('>', start + 1);
        return start >= 0 && end > start ? msg[start..(end + 1)] : null;
    }
}
