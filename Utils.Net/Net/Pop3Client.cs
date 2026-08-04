using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;

namespace Utils.Net;

/// <summary>
/// Client for the Post Office Protocol version 3 (POP3).
/// </summary>
public class Pop3Client : CommandResponseClient
{
    private string? _timestamp;

    /// <summary>
    /// Gets or sets the maximum number of lines accepted in a single POP3 multi-line response.
    /// Default is 100 000. Set to 0 to disable.
    /// </summary>
    public int MaxMultilineLines { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum total number of characters accepted across all lines in a single
    /// POP3 multi-line response. Default is 10 MiB worth of characters. Set to 0 to disable.
    /// </summary>
    public int MaxMultilineChars { get; set; } = 10 * 1024 * 1024;

    /// <summary>Gets or sets the byte limit for POP3 payloads.</summary>
    public long MaxMultilineBytes { get; set; } = 40 * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pop3Client"/> class.
    /// </summary>
    public Pop3Client()
    {
    }

    /// <inheritdoc/>
    public override int DefaultPort { get; } = 110;

    /// <summary>
    /// Executes POP3 specific initialization when a connection is established.
    /// </summary>
    /// <param name="stream">Connected stream used to send commands and receive responses.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the server greeting has been processed.</returns>
    protected override async Task OnConnect(Stream stream, bool leaveOpen, CancellationToken cancellationToken)
    {
        await base.OnConnect(stream, leaveOpen, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ServerResponse> greeting = await ReadAsync(cancellationToken).ConfigureAwait(false);
        await EnsureOkAsync(greeting).ConfigureAwait(false);
        _timestamp = ExtractTimestamp(greeting);
    }

    /// <summary>
    /// Authenticates the user with the POP3 server.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="password">Password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// This authentication flow sends credentials with the <c>USER</c>/<c>PASS</c> commands.
    /// Use it only over a transport already protected by TLS (for example, an <c>SslStream</c>).
    /// The method is marked as obsolete to trigger a compile-time warning and help prevent
    /// accidental usage on unencrypted channels.
    /// </remarks>
    [Obsolete("POP3 USER/PASS authentication can expose credentials on unencrypted connections. Use a TLS-protected stream or a stronger mechanism.", false)]
    public async Task AuthenticateAsync(string user, string password, CancellationToken cancellationToken = default)
    {
        ValidateCommandArgument(user, nameof(user));
        ValidateCommandArgument(password, nameof(password));
        await EnsureOkAsync(await SendCommandAsync($"USER {user}", cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        await EnsureOkAsync(await SendCommandAsync($"PASS {password}", cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// Authenticates the user using the APOP challenge-response mechanism.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="password">Password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// APOP uses MD5, which is cryptographically broken. It provides no meaningful protection
    /// against an active attacker who can observe or modify the challenge. Use a TLS-protected
    /// stream with USER/PASS or a modern SASL mechanism regardless of authentication method.
    /// </remarks>
    [Obsolete("APOP relies on MD5, which is cryptographically broken. Use a TLS-protected transport instead.", false)]
    public async Task AuthenticateApopAsync(string user, string password, CancellationToken cancellationToken = default)
    {
        ValidateCommandArgument(user, nameof(user));
        if (_timestamp is null)
        {
            throw new InvalidOperationException("Server greeting did not contain APOP timestamp");
        }
        using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.ASCII.GetBytes(_timestamp + password));
        string digest = Convert.ToHexString(hash).ToLowerInvariant();
        await EnsureOkAsync(await SendCommandAsync($"APOP {user} {digest}", cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves mailbox statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple containing number of messages and total mailbox size.</returns>
    public async Task<(int messageCount, long mailboxSize)> GetStatAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync("STAT", cancellationToken).ConfigureAwait(false);
        EnsureOk(responses, "STAT");
        string[] parts = SplitExact(responses[0].Message, 2, "STAT");
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 0 ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long size) || size < 0)
            throw Malformed("STAT", responses);
        return (count, size);
    }

    /// <summary>
    /// Retrieves a list of messages with their sizes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping message number to its size.</returns>
    public async Task<IReadOnlyDictionary<int, int>> ListAsync(CancellationToken cancellationToken = default)
    {
        var (status, body) = await SendMultilineCommandAsync(
            "LIST", r => r.Code == ".", MaxMultilineLines, MaxMultilineChars, cancellationToken).ConfigureAwait(false);
        EnsureOk(status, "LIST");
        Dictionary<int, int> result = new();
        foreach (ServerResponse response in body)
        {
            string line = BodyLineToString(response);
            string[] parts = SplitExact(line, 2, "LIST");
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0 ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int size) || size < 0 || !result.TryAdd(id, size))
                throw Malformed("LIST", status);
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
        StringBuilder builder = new();
        using StringWriter writer = new(builder, CultureInfo.InvariantCulture);
        await RetrieveAsync(id, writer, cancellationToken).ConfigureAwait(false);
        return builder.ToString();
    }

    /// <summary>Streams a retrieved message to a writer without materializing its payload.</summary>
    public async Task RetrieveAsync(int id, TextWriter destination, CancellationToken cancellationToken = default)
    {
        ValidateId(id, nameof(id));
        ArgumentNullException.ThrowIfNull(destination);
        IReadOnlyList<ServerResponse> status = await StreamMultilineCommandAsync($"RETR {id}", async (line, token) =>
        {
            await destination.WriteAsync(line, token).ConfigureAwait(false);
            await destination.WriteAsync("\r\n".AsMemory(), token).ConfigureAwait(false);
        }, CreateLimits(), cancellationToken).ConfigureAwait(false);
        EnsureOk(status, "RETR");
    }

    /// <summary>
    /// Marks the specified message for deletion.
    /// </summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        ValidateId(id, nameof(id));
        await EnsureOkAsync(await SendCommandAsync($"DELE {id}", cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the deletion marks for all messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureOkAsync(await SendCommandAsync("RSET", cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
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
        var (status, body) = await SendMultilineCommandAsync(
            "CAPA", r => r.Code == ".", MaxMultilineLines, MaxMultilineChars, cancellationToken).ConfigureAwait(false);
        EnsureOk(status, "UIDL");
        List<string> result = new(body.Count);
        foreach (ServerResponse response in body)
            result.Add(BodyLineToString(response));
        return result;
    }

    /// <summary>
    /// Retrieves unique identifiers for all messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping message numbers to unique identifiers.</returns>
    public async Task<IReadOnlyDictionary<int, string>> ListUniqueIdsAsync(CancellationToken cancellationToken = default)
    {
        var (status, body) = await SendMultilineCommandAsync(
            "UIDL", r => r.Code == ".", MaxMultilineLines, MaxMultilineChars, cancellationToken).ConfigureAwait(false);
        await EnsureOkAsync(status).ConfigureAwait(false);
        Dictionary<int, string> result = new();
        foreach (ServerResponse response in body)
        {
            string line = BodyLineToString(response);
            string[] parts = SplitExact(line, 2, "UIDL");
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0 ||
                !IsValidUniqueId(parts[1]) || !result.TryAdd(id, parts[1])) throw Malformed("UIDL", status);
        }
        return result;
    }

    /// <summary>
    /// Retrieves the unique identifier for a single message.
    /// </summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unique identifier or <see langword="null"/> if not found.</returns>
    public async Task<string> GetUniqueIdAsync(int id, CancellationToken cancellationToken = default)
    {
        ValidateId(id, nameof(id));
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"UIDL {id}", cancellationToken).ConfigureAwait(false);
        EnsureOk(responses, "UIDL");
        string[] parts = SplitExact(responses[0].Message, 2, "UIDL");
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int responseId) || responseId != id || !IsValidUniqueId(parts[1]))
            throw Malformed("UIDL", responses);
        return parts[1];
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

    /// <summary>Validates a POP3 completion response.</summary>
    private static void EnsureOk(IReadOnlyList<ServerResponse> responses, string command) => ProtocolResponseValidator.RequireCode("POP3", command, responses, "+OK");

    /// <summary>Creates configured multiline limits.</summary>
    private ProtocolPayloadLimits CreateLimits() => new() { MaximumLines = MaxMultilineLines, MaximumCharacters = MaxMultilineChars, MaximumBytes = MaxMultilineBytes };

    /// <summary>Requires an exact field count.</summary>
    private static string[] SplitExact(string? value, int count, string command)
    {
        string[] parts = value?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return parts.Length == count ? parts : throw new InvalidDataException($"Malformed POP3 {command} response.");
    }

    /// <summary>Creates a structured malformed-response exception.</summary>
    private static ProtocolResponseException Malformed(string command, IReadOnlyList<ServerResponse> responses) => new("POP3", command, responses);

    /// <summary>Validates a public POP3 message number before any network write.</summary>
    private static void ValidateId(int id, string name) { if (id <= 0) throw new ArgumentOutOfRangeException(name); }

    /// <summary>Validates the bounded POP3 unique-id token.</summary>
    private static bool IsValidUniqueId(string value) => value.Length is > 0 and <= 1024 && value.AsSpan().IndexOfAnyInRange('\0', ' ') < 0 && !value.Any(char.IsControl);

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
        return new ServerResponse(line, ResponseSeverity.Unknown, null);
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
