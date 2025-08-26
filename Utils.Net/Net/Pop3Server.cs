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
        _server.RegisterCommand("STAT", HandleStat, "AUTH");
        _server.RegisterCommand("LIST", HandleList, "AUTH");
        _server.RegisterCommand("RETR", HandleRetr, "AUTH");
        _server.RegisterCommand("DELE", HandleDele, "AUTH");
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
        await _server.SendResponseAsync(new ServerResponse(200, "POP3 ready"));
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
        if (response.Code >= 200 && response.Code < 300)
        {
            return response.Message is null ? "+OK" : $"+OK {response.Message}";
        }
        if (response.Code >= 400)
        {
            return response.Message is null ? "-ERR" : $"-ERR {response.Message}";
        }
        return response.Message ?? string.Empty;
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
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse(200, "user accepted") });
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
            return new[] { new ServerResponse(200, "authentication successful") };
        }
        return new[] { new ServerResponse(500, "authentication failed") };
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
        foreach (int size in list.Values)
        {
            total += size;
        }
        return new[] { new ServerResponse(200, $"{list.Count} {total}") };
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
            if (list.TryGetValue(id, out int size))
            {
                return new[] { new ServerResponse(200, $"{id} {size}") };
            }
            return new[] { new ServerResponse(500, "no such message") };
        }
        List<ServerResponse> responses = new() { new ServerResponse(200, $"{list.Count} messages") };
        foreach (KeyValuePair<int, int> pair in list)
        {
            responses.Add(new ServerResponse(100, $"{pair.Key} {pair.Value}"));
        }
        responses.Add(new ServerResponse(100, "."));
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
            return new[] { new ServerResponse(500, "invalid id") };
        }
        string message = await _mailbox.RetrieveAsync(id);
        List<ServerResponse> responses = new() { new ServerResponse(200, "message follows") };
        using StringReader reader = new(message);
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith(".", StringComparison.Ordinal))
            {
                line = "." + line;
            }
            responses.Add(new ServerResponse(100, line));
        }
        responses.Add(new ServerResponse(100, "."));
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
            return new[] { new ServerResponse(500, "invalid id") };
        }
        await _mailbox.DeleteAsync(id);
        return new[] { new ServerResponse(200, "deleted") };
    }

    /// <summary>
    /// Handles the NOOP command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleNoOp(CommandContext ctx, string[] args)
    {
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse(200, string.Empty) });
    }

    /// <summary>
    /// Handles the QUIT command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleQuit(CommandContext ctx, string[] args)
    {
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse(200, "bye") });
    }
}
