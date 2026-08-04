using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>Client for the Simple Mail Transfer Protocol (SMTP).</summary>
public class SmtpClient : CommandResponseClient
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private TimeSpan _transactionRecoveryTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc/>
    public override int DefaultPort { get; } = 25;

    /// <summary>Gets or sets the bounded timeout used to recover an envelope with RSET.</summary>
    public TimeSpan TransactionRecoveryTimeout
    {
        get => _transactionRecoveryTimeout;
        set => _transactionRecoveryTimeout = value > TimeSpan.Zero && value != Timeout.InfiniteTimeSpan ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Initializes an SMTP client.</summary>
    public SmtpClient() { }

    /// <summary>Authenticates with SASL PLAIN credentials using strict UTF-8.</summary>
    [Obsolete("SMTP AUTH may expose credentials on unencrypted connections. Use a TLS-protected stream.", false)]
    public Task AuthenticateAsync(SmtpPlainCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        string value = $"{credentials.AuthorizationIdentity}\0{credentials.AuthenticationIdentity}\0{credentials.Password}";
        string payload = Convert.ToBase64String(StrictUtf8.GetBytes(value));
        return ExecuteExclusiveExchangeAsync("AUTH", async (context, token) =>
        {
            bool started = false;
            try
            {
                started = true;
                await context.WriteLineAsync($"AUTH PLAIN {payload}", token).ConfigureAwait(false);
                IReadOnlyList<ServerResponse> responses = await ReadResponsesAsync(context, token).ConfigureAwait(false);
                ProtocolResponseValidator.RequireCompletion("SMTP", "AUTH", responses);
                return true;
            }
            catch (Exception ex) when (started && ex is OperationCanceledException)
            {
                context.Poison(ex);
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>Authenticates with PLAIN or LOGIN using strict UTF-8.</summary>
    [Obsolete("SMTP AUTH may expose credentials on unencrypted connections. Use a TLS-protected stream.", false)]
    public Task AuthenticateAsync(string user, string password, SmtpAuthenticationMechanism mechanism = SmtpAuthenticationMechanism.Plain, CancellationToken cancellationToken = default)
    {
        SmtpPlainCredentials credentials = new(user, password);
        if (mechanism == SmtpAuthenticationMechanism.Plain) return AuthenticateAsync(credentials, cancellationToken);
        if (mechanism != SmtpAuthenticationMechanism.Login) throw new NotSupportedException("Unsupported authentication mechanism.");
        string encodedUser = Convert.ToBase64String(StrictUtf8.GetBytes(credentials.AuthenticationIdentity));
        string encodedPassword = Convert.ToBase64String(StrictUtf8.GetBytes(credentials.Password));
        return ExecuteExclusiveExchangeAsync("AUTH", async (context, token) =>
        {
            bool started = false;
            try
            {
                started = true;
                await context.WriteLineAsync("AUTH LOGIN", token).ConfigureAwait(false);
                ProtocolResponseValidator.RequireIntermediate("SMTP", "AUTH", await ReadResponsesAsync(context, token).ConfigureAwait(false));
                await context.WriteLineAsync(encodedUser, token).ConfigureAwait(false);
                ProtocolResponseValidator.RequireIntermediate("SMTP", "AUTH", await ReadResponsesAsync(context, token).ConfigureAwait(false));
                await context.WriteLineAsync(encodedPassword, token).ConfigureAwait(false);
                ProtocolResponseValidator.RequireCompletion("SMTP", "AUTH", await ReadResponsesAsync(context, token).ConfigureAwait(false));
                return true;
            }
            catch (Exception ex) when (started && ex is OperationCanceledException or IOException)
            {
                context.Poison(ex);
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>Authenticates with SASL PLAIN.</summary>
    [Obsolete("SMTP AUTH may expose credentials on unencrypted connections. Use a TLS-protected stream.", false)]
    public Task AuthenticateAsync(string user, string password, CancellationToken cancellationToken) => AuthenticateAsync(user, password, SmtpAuthenticationMechanism.Plain, cancellationToken);

    /// <inheritdoc/>
    protected override async Task OnConnect(Stream stream, bool leaveOpen, CancellationToken cancellationToken)
    {
        await base.OnConnect(stream, leaveOpen, cancellationToken).ConfigureAwait(false);
        ProtocolResponseValidator.RequireCompletion("SMTP", "CONNECT", await ReadAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Sends EHLO and returns all advertised extension lines after the identity line.</summary>
    public async Task<IReadOnlyList<string>> EhloAsync(string domain, CancellationToken cancellationToken = default)
    {
        ValidateCommandArgument(domain, nameof(domain));
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"EHLO {domain}", cancellationToken).ConfigureAwait(false);
        ProtocolResponseValidator.RequireCompletion("SMTP", "EHLO", responses);
        return responses.Skip(1).Select(r => r.Message).Where(m => !string.IsNullOrEmpty(m)).Cast<string>().ToArray();
    }

    /// <summary>Sends HELO.</summary>
    public async Task HeloAsync(string domain, CancellationToken cancellationToken = default)
    {
        ValidateCommandArgument(domain, nameof(domain));
        ProtocolResponseValidator.RequireCompletion("SMTP", "HELO", await SendCommandAsync($"HELO {domain}", cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Verifies an address.</summary>
    public async Task<string?> VrfyAsync(string address, CancellationToken cancellationToken = default)
    {
        ValidateCommandArgument(address, nameof(address));
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"VRFY {address}", cancellationToken).ConfigureAwait(false);
        ProtocolResponseValidator.RequireCompletion("SMTP", "VRFY", responses);
        return responses[^1].Message;
    }

    /// <summary>Expands a mailing list.</summary>
    public async Task<IReadOnlyList<string>> ExpnAsync(string list, CancellationToken cancellationToken = default)
    {
        ValidateCommandArgument(list, nameof(list));
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"EXPN {list}", cancellationToken).ConfigureAwait(false);
        ProtocolResponseValidator.RequireCompletion("SMTP", "EXPN", responses);
        return responses.Select(r => r.Message).Where(m => !string.IsNullOrEmpty(m)).Cast<string>().ToArray();
    }

    /// <summary>Requests SMTP help.</summary>
    public async Task<IReadOnlyList<string>> HelpAsync(string? subject = null, CancellationToken cancellationToken = default)
    {
        if (subject is not null) ValidateCommandArgument(subject, nameof(subject));
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync(subject is null ? "HELP" : $"HELP {subject}", cancellationToken).ConfigureAwait(false);
        ProtocolResponseValidator.RequireCompletion("SMTP", "HELP", responses);
        return responses.Select(r => r.Message).Where(m => !string.IsNullOrEmpty(m)).Cast<string>().ToArray();
    }

    /// <summary>Sends a materialized message after strict path validation.</summary>
    public Task SendMailAsync(string from, IEnumerable<string> recipients, string data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(data);
        SmtpPath sender;
        SmtpPath[] snapshot;
        try
        {
            sender = SmtpPath.Parse(from);
            snapshot = recipients.Select(value => SmtpPath.Parse(value ?? throw new ArgumentException("Recipients cannot contain null.", nameof(recipients)))).ToArray();
        }
        catch (FormatException error)
        {
            throw new ArgumentException("The sender or a recipient is not a valid SMTP path.", nameof(recipients), error);
        }
        return SendMailAsync(sender, snapshot, new StringReader(data), null, cancellationToken);
    }

    /// <summary>Streams a message through one exclusive SMTP transaction.</summary>
    public Task SendMailAsync(SmtpPath from, IReadOnlyList<SmtpPath> recipients, TextReader data, SmtpMailOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(data);
        SmtpPath[] snapshot = recipients.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("At least one recipient is required.", nameof(recipients));
        if (snapshot.Any(p => p is null || p.Value.Length == 0)) throw new ArgumentException("Recipients must be non-empty paths.", nameof(recipients));
        options ??= new SmtpMailOptions();
        if (!options.SmtpUtf8 && (from.AllowsUtf8 || snapshot.Any(p => p.AllowsUtf8))) throw new ArgumentException("SMTPUTF8 paths require SmtpUtf8=true.", nameof(options));
        string mailOptions = FormatOptions(options);
        return ExecuteExclusiveExchangeAsync("MAIL", async (context, token) =>
        {
            bool mailAccepted = false;
            bool dataAccepted = false;
            bool dataFramed = false;
            try
            {
                await context.WriteLineAsync($"MAIL FROM:<{from.Value}>{mailOptions}", token).ConfigureAwait(false);
                ProtocolResponseValidator.RequireCompletion("SMTP", "MAIL", await ReadResponsesAsync(context, token).ConfigureAwait(false));
                mailAccepted = true;
                foreach (SmtpPath recipient in snapshot)
                {
                    await context.WriteLineAsync($"RCPT TO:<{recipient.Value}>", token).ConfigureAwait(false);
                    ProtocolResponseValidator.RequireCompletion("SMTP", "RCPT", await ReadResponsesAsync(context, token).ConfigureAwait(false));
                }
                await context.WriteLineAsync("DATA", token).ConfigureAwait(false);
                ProtocolResponseValidator.RequireIntermediate("SMTP", "DATA", await ReadResponsesAsync(context, token).ConfigureAwait(false));
                dataAccepted = true;
                string? line;
                while ((line = await data.ReadLineAsync(token).ConfigureAwait(false)) is not null)
                    await context.WriteLineAsync(line.StartsWith(".", StringComparison.Ordinal) ? "." + line : line, token).ConfigureAwait(false);
                await context.WriteLineAsync(".", token).ConfigureAwait(false);
                dataFramed = true;
                ProtocolResponseValidator.RequireCompletion("SMTP", "DATA", await ReadResponsesAsync(context, token).ConfigureAwait(false));
                return true;
            }
            catch (Exception primary)
            {
                if (dataAccepted && !dataFramed)
                {
                    context.Poison(primary);
                    throw;
                }
                if (!mailAccepted) throw;
                try
                {
                    using CancellationTokenSource recovery = new(TransactionRecoveryTimeout);
                    await context.WriteLineAsync("RSET", recovery.Token).ConfigureAwait(false);
                    ProtocolResponseValidator.RequireCompletion("SMTP", "RSET", await ReadResponsesAsync(context, recovery.Token).ConfigureAwait(false));
                }
                catch (Exception recoveryError)
                {
                    SmtpTransactionException combined = new(primary, recoveryError);
                    context.Poison(combined);
                    throw combined;
                }
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>Streams message bytes by decoding them strictly with the supplied encoding.</summary>
    public Task SendMailAsync(SmtpPath from, IReadOnlyList<SmtpPath> recipients, Stream data, Encoding encoding, SmtpMailOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(encoding);
        StreamReader reader = new(data, encoding, true, 1024, true);
        return SendMailAsync(from, recipients, reader, options, cancellationToken);
    }

    /// <summary>Sends QUIT and closes the connection.</summary>
    public Task QuitAsync(CancellationToken cancellationToken = default) => DisconnectAsync("QUIT", TimeSpan.FromSeconds(5), cancellationToken);

    /// <inheritdoc/>
    protected override ServerResponse ParseResponseLine(string line)
    {
        if (line.Length >= 3 && int.TryParse(line.AsSpan(0, 3), out int code))
        {
            char separator = line.Length > 3 ? line[3] : ' ';
            ResponseSeverity severity = separator == '-' ? ResponseSeverity.Preliminary : (ResponseSeverity)(code / 100);
            return new ServerResponse(line[..3], severity, line.Length > 4 ? line[4..] : string.Empty);
        }
        return base.ParseResponseLine(line);
    }

    /// <summary>Reads one complete SMTP status response.</summary>
    private static async ValueTask<IReadOnlyList<ServerResponse>> ReadResponsesAsync(ProtocolExchangeContext context, CancellationToken token)
    {
        List<ServerResponse> responses = [];
        do { responses.Add(await context.ReadLineAsync(token).ConfigureAwait(false)); }
        while (responses[^1].Severity == ResponseSeverity.Preliminary);
        return responses;
    }

    /// <summary>Formats typed ESMTP parameters.</summary>
    private static string FormatOptions(SmtpMailOptions options)
    {
        StringBuilder value = new();
        if (options.Size is long size) value.Append(" SIZE=").Append(size);
        if (options.Body is SmtpBodyKind body) value.Append(" BODY=").Append(body == SmtpBodyKind.SevenBit ? "7BIT" : "8BITMIME");
        if (options.SmtpUtf8) value.Append(" SMTPUTF8");
        return value.ToString();
    }
}

/// <summary>Preserves both an SMTP transaction failure and its failed recovery.</summary>
public sealed class SmtpTransactionException : IOException
{
    /// <summary>Initializes a combined SMTP transaction exception.</summary>
    public SmtpTransactionException(Exception operationException, Exception recoveryException) : base("SMTP transaction failed and RSET recovery also failed.", operationException) => RecoveryException = recoveryException;
    /// <summary>Gets the failed RSET exception.</summary>
    public Exception RecoveryException { get; }
}
