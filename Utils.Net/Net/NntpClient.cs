using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace Utils.Net;

/// <summary>
/// Client for the Network News Transfer Protocol (NNTP).
/// </summary>
public class NntpClient : CommandResponseClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NntpClient"/> class.
    /// </summary>
    public NntpClient()
    {
    }

    /// <inheritdoc/>
	public override int DefaultPort { get; } = 119;

    /// <summary>
    /// Connects to the specified NNTP server using a TCP connection.
    /// </summary>
    /// <param name="host">Server host name or IP address.</param>
    /// <param name="port">Server port, default is 119.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public override async Task ConnectAsync(string host, int port = -1, CancellationToken cancellationToken = default)
    {
        await base.ConnectAsync(host, port, cancellationToken);
        IReadOnlyList<ServerResponse> greeting = await ReadAsync(cancellationToken);
        await EnsureCompletionAsync(greeting);
    }

    /// <summary>
    /// Uses the provided bidirectional <see cref="Stream"/> for communication.
    /// </summary>
    /// <param name="stream">Connected stream used to send commands and receive responses.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public override async Task ConnectAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        await base.ConnectAsync(stream, leaveOpen, cancellationToken);
        IReadOnlyList<ServerResponse> greeting = await ReadAsync(cancellationToken);
        await EnsureCompletionAsync(greeting);
    }

    /// <summary>
    /// Selects the specified newsgroup.
    /// </summary>
    /// <param name="group">Name of the newsgroup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing article count, first article number and last article number.</returns>
    public async Task<(int articleCount, int firstArticle, int lastArticle)> GroupAsync(string group, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"GROUP {group}", cancellationToken);
        await EnsureCompletionAsync(responses);
        string[] parts = responses[0].Message?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        int count = parts.Length > 0 ? int.Parse(parts[0]) : 0;
        int first = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        int last = parts.Length > 2 ? int.Parse(parts[2]) : 0;
        return (count, first, last);
    }

    /// <summary>
    /// Retrieves the full text of an article.
    /// </summary>
    /// <param name="id">Article number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Article text.</returns>
    public async Task<string> ArticleAsync(int id, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"ARTICLE {id}", cancellationToken);
        await EnsureCompletionAsync(responses);
        IReadOnlyList<string> lines = await ReadMultilineAsync(cancellationToken);
        StringBuilder sb = new();
        foreach (string line in lines)
        {
            if (line.Length > 1 && line[0] == '.')
            {
                sb.Append(line.AsSpan(1));
            }
            else
            {
                sb.Append(line);
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Lists available newsgroups.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of tuples containing group name, last and first article numbers.</returns>
    public async Task<IReadOnlyList<(string group, int last, int first)>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync("LIST", cancellationToken);
        await EnsureCompletionAsync(responses);
        IReadOnlyList<string> lines = await ReadMultilineAsync(cancellationToken);
        List<(string group, int last, int first)> result = new();
        foreach (string line in lines)
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                result.Add((parts[0], int.Parse(parts[1]), int.Parse(parts[2])));
            }
        }
        return result;
    }

    /// <summary>
    /// Retrieves groups created after the specified time.
    /// </summary>
    /// <param name="sinceUtc">Lower bound in UTC.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of group names.</returns>
    public async Task<IReadOnlyList<string>> NewGroupsAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        string date = sinceUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string time = sinceUtc.ToString("HHmmss", CultureInfo.InvariantCulture);
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"NEWGROUPS {date} {time}", cancellationToken);
        await EnsureCompletionAsync(responses);
        return await ReadMultilineAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves article numbers newer than the given time from the specified group.
    /// </summary>
    /// <param name="group">Name of the newsgroup.</param>
    /// <param name="sinceUtc">Lower bound in UTC.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of article numbers.</returns>
    public async Task<IReadOnlyList<int>> NewNewsAsync(string group, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        string date = sinceUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string time = sinceUtc.ToString("HHmmss", CultureInfo.InvariantCulture);
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"NEWNEWS {group} {date} {time}", cancellationToken);
        await EnsureCompletionAsync(responses);
        IReadOnlyList<string> lines = await ReadMultilineAsync(cancellationToken);
        List<int> ids = new();
        foreach (string line in lines)
        {
            if (int.TryParse(line, out int id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>
    /// Retrieves only the headers of an article.
    /// </summary>
    /// <param name="id">Article number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Header text.</returns>
    public async Task<string> HeaderAsync(int id, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"HEADER {id}", cancellationToken);
        await EnsureCompletionAsync(responses);
        IReadOnlyList<string> lines = await ReadMultilineAsync(cancellationToken);
        StringBuilder sb = new();
        foreach (string line in lines)
        {
            if (line.Length > 1 && line[0] == '.')
            {
                sb.Append(line.AsSpan(1));
            }
            else
            {
                sb.Append(line);
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Retrieves only the body of an article.
    /// </summary>
    /// <param name="id">Article number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Body text.</returns>
    public async Task<string> BodyAsync(int id, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"BODY {id}", cancellationToken);
        await EnsureCompletionAsync(responses);
        IReadOnlyList<string> lines = await ReadMultilineAsync(cancellationToken);
        StringBuilder sb = new();
        foreach (string line in lines)
        {
            if (line.Length > 1 && line[0] == '.')
            {
                sb.Append(line.AsSpan(1));
            }
            else
            {
                sb.Append(line);
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Retrieves article status information without returning content.
    /// </summary>
    /// <param name="id">Article number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing article number and message identifier.</returns>
    public async Task<(int id, string messageId)> StatAsync(int id, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"STAT {id}", cancellationToken);
        await EnsureCompletionAsync(responses);
        string[] parts = responses[0].Message?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        int articleId = parts.Length > 0 ? int.Parse(parts[0]) : 0;
        string messageId = parts.Length > 1 ? parts[1] : string.Empty;
        return (articleId, messageId);
    }

    /// <summary>
    /// Moves to the next article in the selected group.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Article number or <see langword="null"/> if none.</returns>
    public async Task<int?> NextAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync("NEXT", cancellationToken);
        if (responses.Count == 0 || responses[^1].Severity != ResponseSeverity.Completion)
        {
            return null;
        }
        string[] parts = responses[0].Message?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return parts.Length > 0 ? int.Parse(parts[0]) : null;
    }

    /// <summary>
    /// Posts a new article to the current group.
    /// </summary>
    /// <param name="article">Full article text including headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PostAsync(string article, CancellationToken cancellationToken = default)
    {
        await SendLinesAsync(new[] { "POST" }, cancellationToken);
        IReadOnlyList<ServerResponse> intermediate = await ReadAsync(cancellationToken);
        if (intermediate.Count == 0 || intermediate[^1].Severity != ResponseSeverity.Intermediate)
        {
            throw new IOException(intermediate.Count > 0 ? intermediate[^1].Message : "Server closed connection");
        }
        List<string> lines = new();
        using StringReader reader = new(article);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith(".", StringComparison.Ordinal))
            {
                line = "." + line;
            }
            lines.Add(line);
        }
        lines.Add(".");
        await SendLinesAsync(lines, cancellationToken);
        IReadOnlyList<ServerResponse> responses = await ReadAsync(cancellationToken);
        await EnsureCompletionAsync(responses);
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
    /// Ensures that the last response in the sequence indicates success.
    /// </summary>
    /// <param name="responses">Responses to inspect.</param>
    private static Task EnsureCompletionAsync(IReadOnlyList<ServerResponse> responses)
    {
        if (responses.Count == 0 || responses[^1].Severity != ResponseSeverity.Completion)
        {
            throw new IOException(responses.Count > 0 ? responses[^1].Message : "Server closed connection");
        }
        return Task.CompletedTask;
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
                if (line.TrimEnd() == ".")
                {
                    return lines;
                }
                lines.Add(line);
            }
        }
    }
}

