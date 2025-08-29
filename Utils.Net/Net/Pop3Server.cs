using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Server for the Post Office Protocol version 3 (POP3).
/// </summary>
public sealed class Pop3Server : IDisposable
{
    private readonly CommandResponseServer _server;
    private readonly IPop3Mailbox _mailbox;
    private string? _user;
    private string? _timestamp;
    private readonly HashSet<int> _deleted = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Pop3Server"/> class.
    /// </summary>
    /// <param name="mailbox">Mailbox data provider.</param>
    public Pop3Server(IPop3Mailbox mailbox)
    {
        _mailbox = mailbox;
        _server = new CommandResponseServer(FormatResponse);
        _server.RegisterCommand("USER", HandleUser);
        _server.RegisterCommand("PASS", HandlePass, "USER");
        _server.RegisterCommand("APOP", HandleApop);
        _server.RegisterCommand("STAT", HandleStat, "AUTH");
        _server.RegisterCommand("LIST", HandleList, "AUTH");
        _server.RegisterCommand("UIDL", HandleUidl, "AUTH");
        _server.RegisterCommand("RETR", HandleRetr, "AUTH");
        _server.RegisterCommand("DELE", HandleDele, "AUTH");
        _server.RegisterCommand("RSET", HandleRset, "AUTH");
        _server.RegisterCommand("CAPA", HandleCapa);
        _server.RegisterCommand("NOOP", HandleNoOp, "AUTH");
        _server.RegisterCommand("QUIT", HandleQuit);
    }

    /// <summary>
    /// Starts the POP3 server using the specified stream and sends the greeting line.
    /// </summary>
    /// <param name="stream">Stream connected to the client.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        await _server.StartAsync(stream, leaveOpen, cancellationToken);
        _timestamp = $"<{DateTime.UtcNow:yyyyMMddHHmmss}.{Guid.NewGuid():N}@localhost>";
        await _server.SendResponseAsync(new ServerResponse("+OK", ResponseSeverity.Completion, _timestamp));
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
    /// Formats server responses to POP3 textual lines.
    /// </summary>
    /// <param name="response">Response to format.</param>
    /// <returns>Formatted line.</returns>
    private static string FormatResponse(ServerResponse response)
    {
        if (string.IsNullOrEmpty(response.Code))
        {
            return response.Message ?? string.Empty;
        }
        return string.IsNullOrEmpty(response.Message) ? response.Code : $"{response.Code} {response.Message}";
    }

    /// <summary>
    /// Handles the USER command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleUser(CommandContext ctx, string[] args)
    {
        _user = args.Length > 0 ? args[0] : string.Empty;
        ctx.Add("USER");
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("+OK", ResponseSeverity.Completion, "user accepted") });
    }

    /// <summary>
    /// Handles the PASS command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandlePass(CommandContext ctx, string[] args)
    {
        string password = args.Length > 0 ? args[0] : string.Empty;
        bool ok = await _mailbox.AuthenticateAsync(_user ?? string.Empty, password);
        if (ok)
        {
            ctx.Remove("USER");
            ctx.Add("AUTH");
            return new[] { new ServerResponse("+OK", ResponseSeverity.Completion, "authentication successful") };
        }
        return new[] { new ServerResponse("-ERR", ResponseSeverity.PermanentNegative, "authentication failed") };
    }

    /// <summary>
    /// Handles the STAT command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleStat(CommandContext ctx, string[] args)
    {
        IReadOnlyDictionary<int, int> list = await _mailbox.ListAsync();
        int total = 0;
        int count = 0;
        foreach (KeyValuePair<int, int> pair in list)
        {
            if (_deleted.Contains(pair.Key))
            {
                continue;
            }
            count++;
            total += pair.Value;
        }
        return new[] { new ServerResponse("+OK", ResponseSeverity.Completion, $"{count} {total}") };
    }

    /// <summary>
    /// Handles the LIST command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleList(CommandContext ctx, string[] args)
    {
        IReadOnlyDictionary<int, int> list = await _mailbox.ListAsync();
        if (args.Length > 0 && int.TryParse(args[0], out int id))
        {
            if (!
                _deleted.Contains(id) &&
                list.TryGetValue(id, out int size))
            {
                return new[] { new ServerResponse("+OK", ResponseSeverity.Completion, $"{id} {size}") };
            }
            return new[] { new ServerResponse("-ERR", ResponseSeverity.PermanentNegative, "no such message") };
        }
        List<ServerResponse> responses = new() { new ServerResponse("+OK", ResponseSeverity.Preliminary, string.Empty) };
        foreach (KeyValuePair<int, int> pair in list)
        {
            if (_deleted.Contains(pair.Key))
            {
                continue;
            }
            responses.Add(new ServerResponse(string.Empty, ResponseSeverity.Preliminary, $"{pair.Key} {pair.Value}"));
        }
        responses.Add(new ServerResponse(".", ResponseSeverity.Completion, string.Empty));
        return responses;
    }

    /// <summary>
    /// Handles the RETR command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleRetr(CommandContext ctx, string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int id))
        {
            return new[] { new ServerResponse("-ERR", ResponseSeverity.PermanentNegative, "invalid id") };
        }
        IReadOnlyDictionary<int, int> list = await _mailbox.ListAsync();
        if (_deleted.Contains(id) || !list.ContainsKey(id))
        {
            return new[] { new ServerResponse("-ERR", ResponseSeverity.PermanentNegative, "no such message") };
        }
        string message = await _mailbox.RetrieveAsync(id);
        List<ServerResponse> responses = new() { new ServerResponse("+OK", ResponseSeverity.Preliminary, "message follows") };
        using StringReader reader = new(message);
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith(".", StringComparison.Ordinal))
            {
                line = "." + line;
            }
            responses.Add(new ServerResponse(string.Empty, ResponseSeverity.Preliminary, line));
        }
        responses.Add(new ServerResponse(".", ResponseSeverity.Completion, string.Empty));
        return responses;
    }

    /// <summary>
    /// Handles the DELE command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleDele(CommandContext ctx, string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int id))
        {
            return new[] { new ServerResponse("-ERR", ResponseSeverity.PermanentNegative, "invalid id") };
        }
        IReadOnlyDictionary<int, int> list = await _mailbox.ListAsync();
        if (_deleted.Contains(id) || !list.ContainsKey(id))
        {
            return new[] { new ServerResponse("-ERR", ResponseSeverity.PermanentNegative, "no such message") };
        }
        _deleted.Add(id);
        return new[] { new ServerResponse("+OK", ResponseSeverity.Completion, "deleted") };
    }

    /// <summary>
    /// Handles the NOOP command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleNoOp(CommandContext ctx, string[] args)
    {
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("+OK", ResponseSeverity.Completion, string.Empty) });
    }

    /// <summary>
    /// Handles the RSET command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleRset(CommandContext ctx, string[] args)
    {
        _deleted.Clear();
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("+OK", ResponseSeverity.Completion, string.Empty) });
    }

    /// <summary>
    /// Handles the CAPA command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleCapa(CommandContext ctx, string[] args)
    {
        List<ServerResponse> responses = new()
        {
            new ServerResponse("+OK", ResponseSeverity.Preliminary, "Capability list follows"),
            new ServerResponse(string.Empty, ResponseSeverity.Preliminary, "USER"),
            new ServerResponse(string.Empty, ResponseSeverity.Preliminary, "APOP"),
            new ServerResponse(string.Empty, ResponseSeverity.Preliminary, "UIDL"),
            new ServerResponse(".", ResponseSeverity.Completion, string.Empty)
        };
        return Task.FromResult<IEnumerable<ServerResponse>>(responses);
    }

    /// <summary>
    /// Handles the UIDL command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleUidl(CommandContext ctx, string[] args)
    {
        IReadOnlyDictionary<int, string> list = await _mailbox.ListUidsAsync();
        if (args.Length > 0 && int.TryParse(args[0], out int id))
        {
            if (!_deleted.Contains(id) && list.TryGetValue(id, out string uid))
            {
                return new[] { new ServerResponse("+OK", ResponseSeverity.Completion, $"{id} {uid}") };
            }
            return new[] { new ServerResponse("-ERR", ResponseSeverity.PermanentNegative, "no such message") };
        }
        List<ServerResponse> responses = new() { new ServerResponse("+OK", ResponseSeverity.Preliminary, string.Empty) };
        foreach (KeyValuePair<int, string> pair in list)
        {
            if (_deleted.Contains(pair.Key))
            {
                continue;
            }
            responses.Add(new ServerResponse(string.Empty, ResponseSeverity.Preliminary, $"{pair.Key} {pair.Value}"));
        }
        responses.Add(new ServerResponse(".", ResponseSeverity.Completion, string.Empty));
        return responses;
    }

    /// <summary>
    /// Handles the APOP command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleApop(CommandContext ctx, string[] args)
    {
        if (_timestamp is null || args.Length < 2)
        {
            return new[] { new ServerResponse("-ERR", ResponseSeverity.PermanentNegative, "invalid arguments") };
        }
        string user = args[0];
        string digest = args[1];
        bool ok = await _mailbox.AuthenticateApopAsync(user, _timestamp, digest);
        if (ok)
        {
            ctx.Add("AUTH");
            return new[] { new ServerResponse("+OK", ResponseSeverity.Completion, "authentication successful") };
        }
        return new[] { new ServerResponse("-ERR", ResponseSeverity.PermanentNegative, "authentication failed") };
    }

    /// <summary>
    /// Handles the QUIT command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleQuit(CommandContext ctx, string[] args)
    {
        foreach (int id in _deleted)
        {
            await _mailbox.DeleteAsync(id);
        }
        _deleted.Clear();
        return new[] { new ServerResponse("+OK", ResponseSeverity.Completion, "bye") };
    }
}
