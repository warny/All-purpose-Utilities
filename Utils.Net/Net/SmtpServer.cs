using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Simple server implementation for the Simple Mail Transfer Protocol (SMTP).
/// </summary>
public sealed class SmtpServer : IDisposable
{
    private readonly CommandResponseServer _server;
    private readonly ISmtpMessageStore _store;
    private readonly ISmtpAuthenticator? _authenticator;
    private readonly Func<string, bool> _isLocalDomain;
    private string? _from;
    private readonly List<string> _recipients = new();
    private readonly List<string> _dataLines = new();
    private int _dataChars;
    private string? _loginUser;
    private bool _isAuthenticated;
    private bool _canRelay;
    private bool _isTls;
    private int _failedAuthCount;

    /// <summary>
    /// Gets or sets the maximum total number of characters accepted in a single SMTP DATA body.
    /// Default is 10 MiB worth of characters. Set to 0 to disable.
    /// </summary>
    public int MaxDataChars { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of lines accepted in a single SMTP DATA body.
    /// Default is 100 000. Set to 0 to disable.
    /// </summary>
    public int MaxDataLines { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum number of failed authentication attempts allowed per connection
    /// before the session is terminated. Default is 5. Set to 0 to disable.
    /// </summary>
    public int MaxAuthAttempts { get; set; } = 5;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpServer"/> class.
    /// </summary>
    /// <param name="store">Store used to persist received messages.</param>
    /// <param name="isLocalDomain">
    /// Function used to determine if a domain is local and does not require relaying.
    /// When <see langword="null"/>, all non-authenticated recipients are rejected (fail-closed).
    /// Pass an explicit predicate to allow delivery to specific local domains.
    /// </param>
    /// <param name="authenticator">Optional authenticator used to validate credentials.</param>
    public SmtpServer(ISmtpMessageStore store, Func<string, bool>? isLocalDomain = null, ISmtpAuthenticator? authenticator = null)
    {
        _store = store;
        _authenticator = authenticator;
        _isLocalDomain = isLocalDomain ?? (_ => false);
        _server = new CommandResponseServer(SmtpFormatter);
        _server.RegisterCommand("EHLO", HandleEhlo);
        _server.RegisterCommand("HELO", HandleHelo);
        _server.RegisterCommand("AUTH", HandleAuth, "GREETED");
        _server.RegisterCommand("VRFY", HandleVrfy, "GREETED");
        _server.RegisterCommand("EXPN", HandleExpn, "GREETED");
        _server.RegisterCommand("HELP", HandleHelp);
        _server.RegisterCommand("MAIL", HandleMail, "GREETED");
        _server.RegisterCommand("RCPT", HandleRcpt, "MAIL");
        _server.RegisterCommand("DATA", HandleData, "RCPT");
        _server.RegisterCommand("RSET", HandleRset);
        _server.RegisterCommand("NOOP", HandleNoOp);
        _server.RegisterCommand("QUIT", HandleQuit);
        _server.CommandReceived += HandleSpecialLinesAsync;
    }

    /// <summary>
    /// Starts the SMTP server and sends the greeting line.
    /// </summary>
    /// <param name="stream">Stream connected to the client.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the server.</param>
    /// <param name="isTls">
    /// Set to <see langword="true"/> when <paramref name="stream"/> is already protected by TLS.
    /// When <see langword="false"/> (the default), the server will not advertise or accept AUTH
    /// to prevent credentials from being transmitted in cleartext.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(Stream stream, bool leaveOpen = false, bool isTls = false, CancellationToken cancellationToken = default)
    {
        _isTls = isTls;
        await _server.StartAsync(stream, leaveOpen, cancellationToken).ConfigureAwait(false);
        await _server.SendResponseAsync(new ServerResponse("220", ResponseSeverity.Completion, "ready")).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a task that completes when the server stops processing commands.
    /// </summary>
    public Task Completion => _server.Completion;

    /// <summary>
    /// Releases the resources used by the server.
    /// </summary>
    public void Dispose()
    {
        _server.Dispose();
    }

    /// <summary>
    /// Handles the EHLO command for Extended SMTP and advertises supported features.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleEhlo(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        ctx.Add("GREETED");
        List<ServerResponse> responses = new()
        {
            new ServerResponse("250", ResponseSeverity.Preliminary, "Hello")
        };
        if (_authenticator is not null && _isTls)
        {
            responses.Add(new ServerResponse("250", ResponseSeverity.Preliminary, "AUTH PLAIN LOGIN"));
        }
        responses.Add(new ServerResponse("250", ResponseSeverity.Completion, "OK"));
        return Task.FromResult<IEnumerable<ServerResponse>>(responses);
    }

    /// <summary>
    /// Handles the HELO command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleHelo(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        ctx.Add("GREETED");
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("250", ResponseSeverity.Completion, "Hello") });
    }

    /// <summary>
    /// Handles the MAIL command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleMail(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        // Null reverse-path (<>) is explicitly allowed for bounce messages.
        if (!TryParseSmtpPath(args, allowNullPath: true, out string? from))
        {
            return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("501", ResponseSeverity.PermanentNegative, "Malformed MAIL FROM path") });
        }
        _from = from ?? string.Empty;
        _recipients.Clear();
        ctx.Add("MAIL");
        ctx.Remove("RCPT");
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("250", ResponseSeverity.Completion, "OK") });
    }

    /// <summary>
    /// Handles the AUTH command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleAuth(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        if (_authenticator is null || !_isTls)
        {
            return new[] { new ServerResponse("502", ResponseSeverity.PermanentNegative, "Auth not supported") };
        }
        if (args.Length == 0)
        {
            return new[] { new ServerResponse("501", ResponseSeverity.PermanentNegative, "Invalid auth") };
        }
        string mechanism = args[0];
        if (mechanism.Equals("PLAIN", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                return new[] { new ServerResponse("501", ResponseSeverity.PermanentNegative, "Invalid auth") };
            }
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(args[1]);
            }
            catch (FormatException)
            {
                return new[] { new ServerResponse("501", ResponseSeverity.PermanentNegative, "Invalid auth") };
            }
            string credentials = Encoding.ASCII.GetString(decoded);
            int secondNull = credentials.IndexOf('\0', 1);
            string user = secondNull > 0 ? credentials[1..secondNull] : string.Empty;
            string password = secondNull > 0 && secondNull < credentials.Length - 1 ? credentials[(secondNull + 1)..] : string.Empty;
            SmtpAuthenticationResult result = await _authenticator.AuthenticateAsync(user, password, cancellationToken).ConfigureAwait(false);
            if (result.IsAuthenticated)
            {
                _failedAuthCount = 0;
                _isAuthenticated = true;
                _canRelay = result.CanRelay;
                ctx.Add("AUTH");
                return new[] { new ServerResponse("235", ResponseSeverity.Completion, "Authenticated") };
            }
            _failedAuthCount++;
            if (MaxAuthAttempts > 0 && _failedAuthCount >= MaxAuthAttempts)
            {
                return new[] { new ServerResponse("535", ResponseSeverity.PermanentNegative, "Too many authentication failures — bye") };
            }
            return new[] { new ServerResponse("535", ResponseSeverity.PermanentNegative, "Authentication failed") };
        }
        if (mechanism.Equals("LOGIN", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Add("AUTH-LOGIN-USER");
            _loginUser = null;
            string prompt = Convert.ToBase64String(Encoding.ASCII.GetBytes("Username:"));
            return new[] { new ServerResponse("334", ResponseSeverity.Intermediate, prompt) };
        }
        return new[] { new ServerResponse("504", ResponseSeverity.PermanentNegative, "Unsupported auth") };
    }

    /// <summary>
    /// Handles the VRFY command used to verify an address.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private static Task<IEnumerable<ServerResponse>> HandleVrfy(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("252", ResponseSeverity.Completion, "Cannot VRFY user") });
    }

    /// <summary>
    /// Handles the EXPN command used to expand a mailing list.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private static Task<IEnumerable<ServerResponse>> HandleExpn(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("252", ResponseSeverity.Completion, "Cannot EXPN list") });
    }

    /// <summary>
    /// Handles the HELP command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private static Task<IEnumerable<ServerResponse>> HandleHelp(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("214", ResponseSeverity.Completion, "No help available") });
    }

    /// <summary>
    /// Handles the RCPT command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleRcpt(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        if (!TryParseSmtpPath(args, allowNullPath: false, out string? address) || address is null)
        {
            return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("501", ResponseSeverity.PermanentNegative, "Malformed RCPT TO path") });
        }
        string domain = string.Empty;
        int at = address.IndexOf('@');
        if (at >= 0 && at < address.Length - 1)
        {
            domain = address[(at + 1)..];
        }
        if (!_isLocalDomain(domain) && (!_isAuthenticated || !_canRelay))
        {
            return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("550", ResponseSeverity.PermanentNegative, "Relaying denied") });
        }
        _recipients.Add(address);
        ctx.Add("RCPT");
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("250", ResponseSeverity.Completion, "OK") });
    }

    /// <summary>
    /// Handles the DATA command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleData(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        _dataLines.Clear();
        _dataChars = 0;
        ctx.Add("DATA");
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("354", ResponseSeverity.Intermediate, "Start mail input") });
    }

    /// <summary>
    /// Handles the RSET command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleRset(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        _from = null;
        _recipients.Clear();
        _dataLines.Clear();
        ctx.Remove("MAIL");
        ctx.Remove("RCPT");
        ctx.Remove("DATA");
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("250", ResponseSeverity.Completion, "OK") });
    }

    /// <summary>
    /// Handles the NOOP command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private static Task<IEnumerable<ServerResponse>> HandleNoOp(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("250", ResponseSeverity.Completion, "OK") });
    }

    /// <summary>
    /// Handles the QUIT command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private static Task<IEnumerable<ServerResponse>> HandleQuit(CommandContext ctx, string[] args, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("221", ResponseSeverity.Completion, "Bye") });
    }

    /// <summary>
    /// Routes lines that are part of multi-step commands such as DATA or AUTH LOGIN.
    /// </summary>
    /// <param name="line">Line received from the client.</param>
    /// <returns>Responses to send, or <see langword="null"/> if the line was not handled.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleSpecialLinesAsync(string line, CancellationToken cancellationToken)
    {
        IEnumerable<ServerResponse>? responses = await HandleAuthLoginAsync(line, cancellationToken).ConfigureAwait(false);
        if (responses is not null)
        {
            return responses;
        }
        responses = await HandleDataLinesAsync(line, cancellationToken).ConfigureAwait(false);
        return responses;
    }

    /// <summary>
    /// Handles lines that belong to the AUTH LOGIN handshake.
    /// </summary>
    /// <param name="line">Line received from the client.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleAuthLoginAsync(string line, CancellationToken cancellationToken)
    {
        if (_server.HasContext("AUTH-LOGIN-USER"))
        {
            string user;
            try
            {
                user = Encoding.ASCII.GetString(Convert.FromBase64String(line));
            }
            catch (FormatException)
            {
                _server.RemoveContext("AUTH-LOGIN-USER");
                return new[] { new ServerResponse("501", ResponseSeverity.PermanentNegative, "Invalid auth") };
            }
            _loginUser = user;
            _server.RemoveContext("AUTH-LOGIN-USER");
            _server.AddContext("AUTH-LOGIN-PASS");
            string prompt = Convert.ToBase64String(Encoding.ASCII.GetBytes("Password:"));
            return new[] { new ServerResponse("334", ResponseSeverity.Intermediate, prompt) };
        }
        if (_server.HasContext("AUTH-LOGIN-PASS"))
        {
            string password;
            try
            {
                password = Encoding.ASCII.GetString(Convert.FromBase64String(line));
            }
            catch (FormatException)
            {
                _server.RemoveContext("AUTH-LOGIN-PASS");
                return new[] { new ServerResponse("501", ResponseSeverity.PermanentNegative, "Invalid auth") };
            }
            _server.RemoveContext("AUTH-LOGIN-PASS");
            if (_authenticator is not null && _loginUser is not null)
            {
                SmtpAuthenticationResult result = await _authenticator.AuthenticateAsync(_loginUser, password, cancellationToken).ConfigureAwait(false);
                if (result.IsAuthenticated)
                {
                    _failedAuthCount = 0;
                    _isAuthenticated = true;
                    _canRelay = result.CanRelay;
                    _server.AddContext("AUTH");
                    return new[] { new ServerResponse("235", ResponseSeverity.Completion, "Authenticated") };
                }
            }
            _failedAuthCount++;
            if (MaxAuthAttempts > 0 && _failedAuthCount >= MaxAuthAttempts)
            {
                return new[] { new ServerResponse("535", ResponseSeverity.PermanentNegative, "Too many authentication failures — bye") };
            }
            return new[] { new ServerResponse("535", ResponseSeverity.PermanentNegative, "Authentication failed") };
        }
        return null;
    }

    /// <summary>
    /// Handles lines sent after the DATA command until a single dot line terminates the message.
    /// </summary>
    /// <param name="line">Line received from the client.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleDataLinesAsync(string line, CancellationToken cancellationToken)
    {
        if (_server.HasContext("DATA"))
        {
            if (line == ".")
            {
                _server.RemoveContext("DATA");
                string data = string.Join("\r\n", _dataLines);
                SmtpMessage message = new(_from ?? string.Empty, new List<string>(_recipients), data);
                await _store.StoreAsync(message, cancellationToken).ConfigureAwait(false);
                _from = null;
                _recipients.Clear();
                _dataLines.Clear();
                return new[] { new ServerResponse("250", ResponseSeverity.Completion, "OK") };
            }

            string processed = line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line;
            _dataLines.Add(processed);
            _dataChars += processed.Length;
            if (MaxDataLines > 0 && _dataLines.Count > MaxDataLines)
            {
                _server.RemoveContext("DATA");
                _dataLines.Clear();
                _dataChars = 0;
                return new[] { new ServerResponse("552", ResponseSeverity.PermanentNegative, "Message body too long (line limit exceeded)") };
            }
            if (MaxDataChars > 0 && _dataChars > MaxDataChars)
            {
                _server.RemoveContext("DATA");
                _dataLines.Clear();
                _dataChars = 0;
                return new[] { new ServerResponse("552", ResponseSeverity.PermanentNegative, "Message body too long (size limit exceeded)") };
            }
            return Array.Empty<ServerResponse>();
        }

        return null;
    }

    /// <summary>
    /// Parses a strict SMTP path from command arguments (RFC 5321).
    /// Paths must be enclosed in angle brackets. The null reverse-path <c>&lt;&gt;</c> is
    /// accepted only when <paramref name="allowNullPath"/> is <see langword="true"/>.
    /// Source routes are stripped. Local-part is limited to 64 characters, domain to 255,
    /// and the full address to 254 characters.
    /// </summary>
    /// <param name="args">Space-split command arguments (everything after the verb).</param>
    /// <param name="allowNullPath">Whether <c>&lt;&gt;</c> (null reverse-path) is valid.</param>
    /// <param name="address">
    /// When the method returns <see langword="true"/>, contains the extracted address,
    /// or <see langword="null"/> for the null reverse-path.
    /// </param>
    /// <returns><see langword="true"/> if the path is syntactically valid.</returns>
    private static bool TryParseSmtpPath(string[] args, bool allowNullPath, out string? address)
    {
        address = null;
        if (args.Length == 0) return false;

        // Rejoin tokens in case a space appears between "FROM:" and "<addr>".
        string argString = string.Join(" ", args);

        int lt = argString.IndexOf('<');
        int gt = lt >= 0 ? argString.IndexOf('>', lt + 1) : -1;
        if (lt < 0 || gt <= lt) return false;

        string path = argString[(lt + 1)..gt];

        // Null reverse-path.
        if (path.Length == 0)
        {
            if (!allowNullPath) return false;
            address = null;
            return true;
        }

        // Strip source routes ("@route1,@route2:localpart@domain" → "localpart@domain").
        int colon = path.IndexOf(':');
        int firstAt = path.IndexOf('@');
        if (colon >= 0 && (firstAt < 0 || colon < firstAt))
        {
            path = path[(colon + 1)..];
        }

        // Reject control characters and whitespace.
        foreach (char c in path)
        {
            if (c <= 0x20 || c == 0x7F) return false;
        }

        // Must contain exactly one '@' and both parts must be non-empty.
        int at = path.IndexOf('@');
        if (at <= 0 || at == path.Length - 1) return false;
        if (path.IndexOf('@', at + 1) >= 0) return false;

        string localPart = path[..at];
        string domain = path[(at + 1)..];

        // RFC 5321 limits.
        if (localPart.Length > 64 || domain.Length > 255 || path.Length > 254) return false;

        address = path;
        return true;
    }

    /// <summary>
    /// Formats SMTP responses adding a hyphen for preliminary replies to build multi-line responses.
    /// </summary>
    /// <param name="response">Response to format.</param>
    /// <returns>Formatted response line.</returns>
    private static string SmtpFormatter(ServerResponse response)
    {
        string separator = response.Severity == ResponseSeverity.Preliminary ? "-" : " ";
        return response.Message is null ? response.Code : $"{response.Code}{separator}{response.Message}";
    }
}
