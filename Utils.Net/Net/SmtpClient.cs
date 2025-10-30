using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Client for the Simple Mail Transfer Protocol (SMTP).
/// </summary>
public class SmtpClient : CommandResponseClient
{
    /// <inheritdoc/>
    public override int DefaultPort { get; } = 25;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpClient"/> class.
    /// </summary>
    public SmtpClient()
    {
    }

    /// <summary>
    /// Authenticates using the specified mechanism.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="password">User password.</param>
    /// <param name="mechanism">Authentication mechanism to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AuthenticateAsync(string user, string password, SmtpAuthenticationMechanism mechanism = SmtpAuthenticationMechanism.Plain, CancellationToken cancellationToken = default)
    {
        switch (mechanism)
        {
            case SmtpAuthenticationMechanism.Plain:
                string payload = "\0" + user + "\0" + password;
                string encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(payload));
                IReadOnlyList<ServerResponse> plainResult = await SendCommandAsync($"AUTH PLAIN {encoded}", cancellationToken);
                await EnsureCompletionAsync(plainResult);
                break;

            case SmtpAuthenticationMechanism.Login:
                IReadOnlyList<ServerResponse> loginResult = await SendCommandAsync("AUTH LOGIN", cancellationToken);
                await EnsureIntermediateAsync(loginResult);
                string userEncoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(user));
                IReadOnlyList<ServerResponse> userResponse = await SendCommandAsync(userEncoded, cancellationToken);
                await EnsureIntermediateAsync(userResponse);
                string passEncoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(password));
                IReadOnlyList<ServerResponse> passResponse = await SendCommandAsync(passEncoded, cancellationToken);
                await EnsureCompletionAsync(passResponse);
                break;

            default:
                throw new NotSupportedException("Unsupported authentication mechanism.");
        }
    }

    /// <summary>
    /// Authenticates using the <c>AUTH PLAIN</c> mechanism.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="password">User password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task AuthenticateAsync(string user, string password, CancellationToken cancellationToken)
    {
        return AuthenticateAsync(user, password, SmtpAuthenticationMechanism.Plain, cancellationToken);
    }

    /// <summary>
    /// Executes SMTP specific initialization when a connection is established.
    /// </summary>
    /// <param name="stream">Connected stream used to send commands and receive responses.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the server greeting has been processed.</returns>
    protected override async Task OnConnect(Stream stream, bool leaveOpen, CancellationToken cancellationToken)
    {
        await base.OnConnect(stream, leaveOpen, cancellationToken);
        IReadOnlyList<ServerResponse> greeting = await ReadAsync(cancellationToken);
        await EnsureCompletionAsync(greeting);
    }

    /// <summary>
    /// Sends the EHLO command and returns the advertised extensions.
    /// </summary>
    /// <param name="domain">Client domain name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of server extensions.</returns>
    public async Task<IReadOnlyList<string>> EhloAsync(string domain, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"EHLO {domain}", cancellationToken);
        List<string> extensions = new();
        for (int i = 1; i < responses.Count - 1; i++)
        {
            if (!string.IsNullOrEmpty(responses[i].Message))
            {
                extensions.Add(responses[i].Message);
            }
        }
        await EnsureCompletionAsync(responses);
        return extensions;
    }

    /// <summary>
    /// Sends the HELO command.
    /// </summary>
    /// <param name="domain">Client domain name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task HeloAsync(string domain, CancellationToken cancellationToken = default)
    {
        await EnsureCompletionAsync(await SendCommandAsync($"HELO {domain}", cancellationToken));
    }

    /// <summary>
    /// Sends the VRFY command to verify an address.
    /// </summary>
    /// <param name="address">Address to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Server response message.</returns>
    public async Task<string?> VrfyAsync(string address, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"VRFY {address}", cancellationToken);
        await EnsureCompletionAsync(responses);
        return responses[^1].Message;
    }

    /// <summary>
    /// Sends the EXPN command to expand a mailing list.
    /// </summary>
    /// <param name="list">List name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Expanded entries returned by the server.</returns>
    public async Task<IReadOnlyList<string>> ExpnAsync(string list, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync($"EXPN {list}", cancellationToken);
        List<string> entries = new();
        for (int i = 0; i < responses.Count - 1; i++)
        {
            if (!string.IsNullOrEmpty(responses[i].Message))
            {
                entries.Add(responses[i].Message);
            }
        }
        await EnsureCompletionAsync(responses);
        return entries;
    }

    /// <summary>
    /// Sends the HELP command optionally for a specific subject.
    /// </summary>
    /// <param name="subject">Command or subject to request help for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Help text returned by the server.</returns>
    public async Task<IReadOnlyList<string>> HelpAsync(string? subject = null, CancellationToken cancellationToken = default)
    {
        string command = subject is null ? "HELP" : $"HELP {subject}";
        IReadOnlyList<ServerResponse> responses = await SendCommandAsync(command, cancellationToken);
        List<string> lines = new();
        foreach (ServerResponse response in responses)
        {
            if (!string.IsNullOrEmpty(response.Message))
            {
                lines.Add(response.Message);
            }
        }
        await EnsureCompletionAsync(responses);
        return lines;
    }

    /// <summary>
    /// Sends a mail message.
    /// </summary>
    /// <param name="from">Sender address.</param>
    /// <param name="recipients">Recipient addresses.</param>
    /// <param name="data">Raw message data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendMailAsync(string from, IEnumerable<string> recipients, string data, CancellationToken cancellationToken = default)
    {
        await EnsureCompletionAsync(await SendCommandAsync($"MAIL FROM:<{from}>", cancellationToken));
        foreach (string rcpt in recipients)
        {
            await EnsureCompletionAsync(await SendCommandAsync($"RCPT TO:<{rcpt}>", cancellationToken));
        }
        await EnsureIntermediateAsync(await SendCommandAsync("DATA", cancellationToken));
        List<string> lines = new();
        using StringReader reader = new(data);
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith('.'))
            {
                lines.Add("." + line);
            }
            else
            {
                lines.Add(line);
            }
        }
        lines.Add(".");
        await SendLinesAsync(lines, cancellationToken);
        IReadOnlyList<ServerResponse> result = await ReadAsync(cancellationToken);
        await EnsureCompletionAsync(result);
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
    /// Parses a single response line using SMTP semantics where a hyphen after the status code
    /// indicates that more lines will follow.
    /// </summary>
    /// <param name="line">Response line from the server.</param>
    /// <returns>Parsed response.</returns>
    protected override ServerResponse ParseResponseLine(string line)
    {
        if (line.Length >= 4 && char.IsDigit(line[0]) && char.IsDigit(line[1]) && char.IsDigit(line[2]))
        {
            string code = line.Substring(0, 3);
            char separator = line[3];
            string message = line.Length > 4 ? line[4..] : string.Empty;
            int digit = code[0] - '0';
            ResponseSeverity severity = digit >= 0 && digit <= 5 ? (ResponseSeverity)digit : ResponseSeverity.Unknown;
            if (separator == '-')
            {
                severity = ResponseSeverity.Preliminary;
            }
            return new ServerResponse(code, severity, message);
        }

        return base.ParseResponseLine(line);
    }

    /// <summary>
    /// Ensures that the final response has completion severity.
    /// </summary>
    /// <param name="responses">Responses to check.</param>
    private static Task EnsureCompletionAsync(IReadOnlyList<ServerResponse> responses)
    {
        if (responses.Count == 0 || responses[^1].Severity != ResponseSeverity.Completion)
        {
            throw new IOException(responses.Count > 0 ? responses[^1].Message : "Server closed connection");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures that the final response has intermediate severity.
    /// </summary>
    /// <param name="responses">Responses to check.</param>
    private static Task EnsureIntermediateAsync(IReadOnlyList<ServerResponse> responses)
    {
        if (responses.Count == 0 || responses[^1].Severity != ResponseSeverity.Intermediate)
        {
            throw new IOException(responses.Count > 0 ? responses[^1].Message : "Server closed connection");
        }
        return Task.CompletedTask;
    }
}
