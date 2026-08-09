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
    /// Gets or sets the maximum number of lines accepted in a single NNTP multi-line response.
    /// Default is 100 000. Set to 0 to disable.
    /// </summary>
    public int MaxMultilineLines { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum total number of characters accepted across all lines in a single
    /// NNTP multi-line response. Default is 10 MiB worth of characters. Set to 0 to disable.
    /// </summary>
    public int MaxMultilineChars { get; set; } = 10 * 1024 * 1024;

    /// <summary>Gets or sets the maximum UTF-8 byte count for a multiline response.</summary>
    public long MaxMultilineBytes { get; set; } = 40 * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="NntpClient"/> class.
    /// </summary>
    public NntpClient()
    {
    }

    /// <inheritdoc/>
    public override int DefaultPort { get; } = 119;

    /// <summary>
    /// Executes NNTP specific initialization when a connection is established.
    /// </summary>
    /// <param name="stream">Connected stream used to send commands and receive responses.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the server greeting has been processed.</returns>
    protected override async Task OnConnect(Stream stream, bool leaveOpen, CancellationToken cancellationToken)
    {
        await base.OnConnect(stream, leaveOpen, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ServerResponse> greeting = await ReadAsync(cancellationToken).ConfigureAwait(false);
        await EnsureCompletionAsync(greeting).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses NNTP response lines, treating non-numeric data lines as preliminary responses.
    /// </summary>
    /// <param name="line">Response line from the server.</param>
    /// <returns>The parsed response.</returns>
    protected override ServerResponse ParseResponseLine(string line)
    {
        ServerResponse response = base.ParseResponseLine(line);
        if (response.Severity == ResponseSeverity.Unknown && response.Code == line)
        {
            return new ServerResponse(response.Code, ResponseSeverity.Preliminary, response.Message);
        }

        return response;
    }

    /// <summary>
    /// Selects the specified newsgroup.
    /// </summary>
    /// <param name="group">Name of the newsgroup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing article count, first article number and last article number.</returns>
    public async Task<(int articleCount, int firstArticle, int lastArticle)> GroupAsync(string group, CancellationToken cancellationToken = default)
    {
        ValidateCommandArgument(group, nameof(group));
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"GROUP {group}", cancellationToken).ConfigureAwait(false);
        ProtocolResponseValidator.RequireCode("NNTP", "GROUP", responses, "211");
        string[] parts = ExactFields(responses[0].Message, 3, "GROUP", responses);
        if (!TryNonNegative(parts[0], out int count) || !TryNonNegative(parts[1], out int first) || !TryNonNegative(parts[2], out int last) || (count > 0 && first > last))
            throw new ProtocolResponseException("NNTP", "GROUP", responses);
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
        StringBuilder sb = new();
        using StringWriter writer = new(sb, CultureInfo.InvariantCulture);
        await ArticleAsync(id, writer, cancellationToken).ConfigureAwait(false);
        return sb.ToString();
    }

    /// <summary>Streams a complete article while exclusively owning the exchange.</summary>
    public Task ArticleAsync(int id, TextWriter destination, CancellationToken cancellationToken = default) => StreamArticlePartAsync("ARTICLE", "220", id, destination, cancellationToken);

    /// <summary>
    /// Lists available newsgroups.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of tuples containing group name, last and first article numbers.</returns>
    public async Task<IReadOnlyList<(string group, int last, int first)>> ListAsync(CancellationToken cancellationToken = default)
    {
        var (status, body) = await SendMultilineCommandAsync(
            "LIST", r => r.Code == ".", MaxMultilineLines, MaxMultilineChars, MaxMultilineBytes, cancellationToken).ConfigureAwait(false);
        ProtocolResponseValidator.RequireCode("NNTP", "LIST", status, "215");
        List<(string group, int last, int first)> result = new();
        foreach (ServerResponse response in body)
        {
            string line = BodyLineToString(response);
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 3 or > 4 || parts[0].Length == 0 || !TryNonNegative(parts[1], out int high) || !TryNonNegative(parts[2], out int low) || high < low)
                throw new ProtocolResponseException("NNTP", "LIST", status);
            result.Add((parts[0], high, low));
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
        DateTime utc = sinceUtc.Kind == DateTimeKind.Local ? sinceUtc.ToUniversalTime() : DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc);
        string date = utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string time = utc.ToString("HHmmss", CultureInfo.InvariantCulture);
        var (status, body) = await SendMultilineCommandAsync(
            $"NEWGROUPS {date} {time} GMT", r => r.Code == ".", MaxMultilineLines, MaxMultilineChars, MaxMultilineBytes, cancellationToken).ConfigureAwait(false);
        await EnsureCompletionAsync(status).ConfigureAwait(false);
        List<string> result = new(body.Count);
        foreach (ServerResponse response in body)
            result.Add(BodyLineToString(response));
        return result;
    }

    /// <summary>
    /// Retrieves article numbers newer than the given time from the specified group.
    /// </summary>
    /// <param name="group">Name of the newsgroup.</param>
    /// <param name="sinceUtc">Lower bound in UTC.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of article numbers.</returns>
    public async Task<IReadOnlyList<string>> NewNewsAsync(string group, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        ValidateCommandArgument(group, nameof(group));
        DateTime utc = sinceUtc.Kind == DateTimeKind.Local ? sinceUtc.ToUniversalTime() : DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc);
        string date = utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string time = utc.ToString("HHmmss", CultureInfo.InvariantCulture);
        var (status, body) = await SendMultilineCommandAsync(
            $"NEWNEWS {group} {date} {time} GMT", r => r.Code == ".", MaxMultilineLines, MaxMultilineChars, MaxMultilineBytes, cancellationToken).ConfigureAwait(false);
        await EnsureCompletionAsync(status).ConfigureAwait(false);
        List<string> ids = new();
        foreach (ServerResponse response in body)
        {
            string id = BodyLineToString(response);
            if (!IsMessageId(id)) throw new ProtocolResponseException("NNTP", "NEWNEWS", status);
            ids.Add(id);
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
        StringBuilder builder = new();
        using StringWriter writer = new(builder, CultureInfo.InvariantCulture);
        await HeaderAsync(id, writer, cancellationToken).ConfigureAwait(false);
        return builder.ToString();
    }

    /// <summary>Streams article headers while exclusively owning the exchange.</summary>
    public Task HeaderAsync(int id, TextWriter destination, CancellationToken cancellationToken = default) => StreamArticlePartAsync("HEADER", "221", id, destination, cancellationToken);

    /// <summary>
    /// Retrieves only the body of an article.
    /// </summary>
    /// <param name="id">Article number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Body text.</returns>
    public async Task<string> BodyAsync(int id, CancellationToken cancellationToken = default)
    {
        StringBuilder builder = new();
        using StringWriter writer = new(builder, CultureInfo.InvariantCulture);
        await BodyAsync(id, writer, cancellationToken).ConfigureAwait(false);
        return builder.ToString();
    }

    /// <summary>Streams an article body while exclusively owning the exchange.</summary>
    public Task BodyAsync(int id, TextWriter destination, CancellationToken cancellationToken = default) => StreamArticlePartAsync("BODY", "222", id, destination, cancellationToken);

    /// <summary>
    /// Retrieves article status information without returning content.
    /// </summary>
    /// <param name="id">Article number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing article number and message identifier.</returns>
    public async Task<(int id, string messageId)> StatAsync(int id, CancellationToken cancellationToken = default)
    {
        ValidateId(id, nameof(id));
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"STAT {id}", cancellationToken).ConfigureAwait(false);
        ProtocolResponseValidator.RequireCode("NNTP", "STAT", responses, "223");
        return ParseArticleStatus("STAT", responses);
    }

    /// <summary>
    /// Moves to the next article in the selected group.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Article number or <see langword="null"/> if none.</returns>
    public async Task<int?> NextAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync("NEXT", cancellationToken).ConfigureAwait(false);
        if (responses.Count == 0) throw new ProtocolResponseException("NNTP", "NEXT", responses);
        if (responses[^1].Code == "421") return null;
        ProtocolResponseValidator.RequireCode("NNTP", "NEXT", responses, "223");
        return ParseArticleStatus("NEXT", responses).id;
    }

    /// <summary>
    /// Posts a new article to the current group.
    /// </summary>
    /// <param name="article">Full article text including headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PostAsync(string article, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> intermediate = await SendCommandAsync("POST", cancellationToken).ConfigureAwait(false);
        if (intermediate.Count == 0 || intermediate[^1].Severity != ResponseSeverity.Intermediate)
        {
            throw new IOException(intermediate.Count > 0 ? intermediate[^1].Message : "Server closed connection");
        }
        List<string> lines = new();
        using StringReader reader = new(article);
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            if (line.StartsWith(".", StringComparison.Ordinal))
            {
                lines.Add("." + line);
            }
            else
            {
                lines.Add(line);
            }
        }
        lines.Add(".");
        IReadOnlyList<ServerResponse> responses = await SendBodyAndReadAsync(lines, cancellationToken).ConfigureAwait(false);
        await EnsureCompletionAsync(responses).ConfigureAwait(false);
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

    /// <summary>Streams one dot-terminated article component.</summary>
    private async Task StreamArticlePartAsync(string command, string expectedCode, int id, TextWriter destination, CancellationToken cancellationToken)
    {
        ValidateId(id, nameof(id));
        ArgumentNullException.ThrowIfNull(destination);
        await StreamMultilineCommandAsync($"{command} {id}", async (line, token) =>
        {
            await destination.WriteAsync(line, token).ConfigureAwait(false);
            await destination.WriteAsync("\r\n".AsMemory(), token).ConfigureAwait(false);
        },
        new ProtocolPayloadLimits { MaximumLines = MaxMultilineLines, MaximumCharacters = MaxMultilineChars, MaximumBytes = MaxMultilineBytes },
        cancellationToken,
        validateOpeningResponse: status => ProtocolResponseValidator.RequireCode("NNTP", command, status, expectedCode)).ConfigureAwait(false);
    }

    /// <summary>Parses a strict NNTP article-number and message-id response.</summary>
    private static (int id, string messageId) ParseArticleStatus(string command, IReadOnlyList<ServerResponse> responses)
    {
        string[] fields = ExactFields(responses[0].Message, 2, command, responses);
        if (!int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0 || !IsMessageId(fields[1]))
            throw new ProtocolResponseException("NNTP", command, responses);
        return (id, fields[1]);
    }

    /// <summary>Requires an exact number of response fields.</summary>
    private static string[] ExactFields(string? value, int count, string command, IReadOnlyList<ServerResponse> responses)
    {
        string[] fields = value?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return fields.Length == count ? fields : throw new ProtocolResponseException("NNTP", command, responses);
    }

    /// <summary>Parses a non-negative invariant integer.</summary>
    private static bool TryNonNegative(string value, out int result) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0;

    /// <summary>Validates an NNTP message-id token.</summary>
    private static bool IsMessageId(string value) => value.Length is > 2 and <= 998 && value[0] == '<' && value[^1] == '>' && !value.Any(char.IsWhiteSpace) && !value.Any(char.IsControl);

    /// <summary>Validates an article number before any command is written.</summary>
    private static void ValidateId(int id, string name) { if (id <= 0) throw new ArgumentOutOfRangeException(name); }


}
