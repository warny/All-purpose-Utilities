using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Server for the Network News Transfer Protocol (NNTP).
/// </summary>
public sealed class NntpServer : IDisposable
{
    private readonly CommandResponseServer _server;
    private readonly INntpArticleStore _store;
    private string? _currentGroup;
    private int? _currentArticle;
    private bool _posting;
    private readonly List<string> _postLines = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NntpServer"/> class.
    /// </summary>
    /// <param name="store">Article data provider.</param>
    public NntpServer(INntpArticleStore store)
    {
        _store = store;
        _server = new CommandResponseServer();
        _server.RegisterCommand("GROUP", HandleGroup);
        _server.RegisterCommand("ARTICLE", HandleArticle, "GROUP");
        _server.RegisterCommand("LIST", HandleList);
        _server.RegisterCommand("NEWGROUPS", HandleNewGroups);
        _server.RegisterCommand("NEWNEWS", HandleNewNews);
        _server.RegisterCommand("HEADER", HandleHeader, "GROUP");
        _server.RegisterCommand("BODY", HandleBody, "GROUP");
        _server.RegisterCommand("STAT", HandleStat, "GROUP");
        _server.RegisterCommand("NEXT", HandleNext, "GROUP");
        _server.RegisterCommand("POST", HandlePost);
        _server.RegisterCommand("QUIT", HandleQuit);
        _server.CommandReceived += HandleUnrecognizedAsync;
    }

    /// <summary>
    /// Starts the NNTP server using the specified stream and sends the greeting line.
    /// </summary>
    /// <param name="stream">Stream connected to the client.</param>
    /// <param name="leaveOpen">True to leave the stream open when disposing the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        await _server.StartAsync(stream, leaveOpen, cancellationToken);
        await _server.SendResponseAsync(new ServerResponse("200", ResponseSeverity.Completion, "server ready"));
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
    /// Handles the GROUP command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleGroup(CommandContext ctx, string[] args)
    {
        string group = args.Length > 0 ? args[0] : string.Empty;
        IReadOnlyDictionary<int, string> articles = await _store.ListAsync(group);
        if (articles.Count == 0)
        {
            ctx.Remove("GROUP");
            _currentGroup = null;
            return new[] { new ServerResponse("411", ResponseSeverity.PermanentNegative, "no such group") };
        }

        int first = int.MaxValue;
        int last = int.MinValue;
        foreach (int id in articles.Keys)
        {
            if (id < first)
            {
                first = id;
            }
            if (id > last)
            {
                last = id;
            }
        }

        ctx.Add("GROUP");
        _currentGroup = group;
        _currentArticle = null;
        return new[] { new ServerResponse("211", ResponseSeverity.Completion, $"{articles.Count} {first} {last} {group}") };
    }

    /// <summary>
    /// Handles the ARTICLE command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleArticle(CommandContext ctx, string[] args)
    {
        if (_currentGroup is null)
        {
            return new[] { new ServerResponse("412", ResponseSeverity.PermanentNegative, "no newsgroup selected") };
        }
        if (args.Length == 0 || !int.TryParse(args[0], out int id))
        {
            return new[] { new ServerResponse("420", ResponseSeverity.PermanentNegative, "no article number") };
        }
        string? article = await _store.RetrieveAsync(_currentGroup, id);
        if (article is null)
        {
            return new[] { new ServerResponse("423", ResponseSeverity.PermanentNegative, "no such article") };
        }
        _currentArticle = id;
        List<ServerResponse> responses = new() { new ServerResponse("220", ResponseSeverity.Completion, $"{id} article follows") };
        using StringReader reader = new(article);
        string? line;
        while ((line = reader.ReadLine()) is not null)
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
    /// Handles the LIST command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleList(CommandContext ctx, string[] args)
    {
        IReadOnlyCollection<string> groups = await _store.ListGroupsAsync();
        List<ServerResponse> responses = new()
        {
            new ServerResponse("215", ResponseSeverity.Completion, "list of newsgroups follows")
        };
        foreach (string group in groups)
        {
            IReadOnlyDictionary<int, string> articles = await _store.ListAsync(group);
            int first = int.MaxValue;
            int last = int.MinValue;
            foreach (int id in articles.Keys)
            {
                if (id < first)
                {
                    first = id;
                }
                if (id > last)
                {
                    last = id;
                }
            }
            if (articles.Count == 0)
            {
                first = 0;
                last = 0;
            }
            responses.Add(new ServerResponse(string.Empty, ResponseSeverity.Preliminary, $"{group} {last} {first} y"));
        }
        responses.Add(new ServerResponse(".", ResponseSeverity.Completion, string.Empty));
        return responses;
    }

    /// <summary>
    /// Handles the NEWGROUPS command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleNewGroups(CommandContext ctx, string[] args)
    {
        if (args.Length < 2 || !TryParseDateTime(args[0], args[1], out DateTime since))
        {
            return new[] { new ServerResponse("501", ResponseSeverity.PermanentNegative, "syntax error") };
        }
        IReadOnlyCollection<string> groups = await _store.ListGroupsAsync();
        List<ServerResponse> responses = new()
        {
            new ServerResponse("231", ResponseSeverity.Completion, "list follows")
        };
        foreach (string group in groups)
        {
            DateTime? created = await _store.GetGroupCreationDateAsync(group);
            if (created is not null && created.Value.ToUniversalTime() >= since)
            {
                responses.Add(new ServerResponse(string.Empty, ResponseSeverity.Preliminary, group));
            }
        }
        responses.Add(new ServerResponse(".", ResponseSeverity.Completion, string.Empty));
        return responses;
    }

    /// <summary>
    /// Handles the NEWNEWS command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleNewNews(CommandContext ctx, string[] args)
    {
        if (args.Length < 3 || !TryParseDateTime(args[1], args[2], out DateTime since))
        {
            return new[] { new ServerResponse("501", ResponseSeverity.PermanentNegative, "syntax error") };
        }
        string group = args[0];
        IReadOnlyCollection<int> ids = await _store.ListNewsSinceAsync(group, since);
        List<ServerResponse> responses = new()
        {
            new ServerResponse("230", ResponseSeverity.Completion, "list of new articles follows")
        };
        foreach (int id in ids)
        {
            responses.Add(new ServerResponse(string.Empty, ResponseSeverity.Preliminary, id.ToString()));
        }
        responses.Add(new ServerResponse(".", ResponseSeverity.Completion, string.Empty));
        return responses;
    }

    /// <summary>
    /// Handles the HEADER command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleHeader(CommandContext ctx, string[] args)
    {
        if (_currentGroup is null)
        {
            return new[] { new ServerResponse("412", ResponseSeverity.PermanentNegative, "no newsgroup selected") };
        }
        int id;
        if (args.Length == 0)
        {
            if (_currentArticle is null)
            {
                return new[] { new ServerResponse("420", ResponseSeverity.PermanentNegative, "no article number") };
            }
            id = _currentArticle.Value;
        }
        else if (!int.TryParse(args[0], out id))
        {
            return new[] { new ServerResponse("420", ResponseSeverity.PermanentNegative, "no article number") };
        }
        string? article = await _store.RetrieveAsync(_currentGroup, id);
        if (article is null)
        {
            return new[] { new ServerResponse("423", ResponseSeverity.PermanentNegative, "no such article") };
        }
        _currentArticle = id;
        List<ServerResponse> responses = new() { new ServerResponse("221", ResponseSeverity.Completion, $"{id} headers follow") };
        using StringReader reader = new(article);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                break;
            }
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
    /// Handles the BODY command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleBody(CommandContext ctx, string[] args)
    {
        if (_currentGroup is null)
        {
            return new[] { new ServerResponse("412", ResponseSeverity.PermanentNegative, "no newsgroup selected") };
        }
        int id;
        if (args.Length == 0)
        {
            if (_currentArticle is null)
            {
                return new[] { new ServerResponse("420", ResponseSeverity.PermanentNegative, "no article number") };
            }
            id = _currentArticle.Value;
        }
        else if (!int.TryParse(args[0], out id))
        {
            return new[] { new ServerResponse("420", ResponseSeverity.PermanentNegative, "no article number") };
        }
        string? article = await _store.RetrieveAsync(_currentGroup, id);
        if (article is null)
        {
            return new[] { new ServerResponse("423", ResponseSeverity.PermanentNegative, "no such article") };
        }
        _currentArticle = id;
        List<ServerResponse> responses = new() { new ServerResponse("222", ResponseSeverity.Completion, $"{id} body follows") };
        int separator = article.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        string body = separator >= 0 ? article[(separator + 4)..] : string.Empty;
        using StringReader reader = new(body);
        string? line;
        while ((line = reader.ReadLine()) is not null)
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
    /// Handles the STAT command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleStat(CommandContext ctx, string[] args)
    {
        if (_currentGroup is null)
        {
            return new[] { new ServerResponse("412", ResponseSeverity.PermanentNegative, "no newsgroup selected") };
        }
        int id;
        if (args.Length == 0)
        {
            if (_currentArticle is null)
            {
                return new[] { new ServerResponse("420", ResponseSeverity.PermanentNegative, "no article number") };
            }
            id = _currentArticle.Value;
        }
        else if (!int.TryParse(args[0], out id))
        {
            return new[] { new ServerResponse("420", ResponseSeverity.PermanentNegative, "no article number") };
        }
        string? article = await _store.RetrieveAsync(_currentGroup, id);
        if (article is null)
        {
            return new[] { new ServerResponse("423", ResponseSeverity.PermanentNegative, "no such article") };
        }
        _currentArticle = id;
        return new[] { new ServerResponse("223", ResponseSeverity.Completion, $"{id} <{id}@{_currentGroup}>") };
    }

    /// <summary>
    /// Handles the NEXT command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleNext(CommandContext ctx, string[] args)
    {
        if (_currentGroup is null)
        {
            return new[] { new ServerResponse("412", ResponseSeverity.PermanentNegative, "no newsgroup selected") };
        }
        IReadOnlyDictionary<int, string> articles = await _store.ListAsync(_currentGroup);
        List<int> ids = new(articles.Keys);
        ids.Sort();
        int? next = null;
        foreach (int id in ids)
        {
            if (!_currentArticle.HasValue || id > _currentArticle.Value)
            {
                next = id;
                break;
            }
        }
        if (next is null)
        {
            return new[] { new ServerResponse("421", ResponseSeverity.PermanentNegative, "no next article") };
        }
        _currentArticle = next.Value;
        return new[] { new ServerResponse("223", ResponseSeverity.Completion, $"{next} <{next}@{_currentGroup}>") };
    }

    /// <summary>
    /// Handles the POST command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandlePost(CommandContext ctx, string[] args)
    {
        if (_currentGroup is null)
        {
            return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("440", ResponseSeverity.PermanentNegative, "posting not allowed") });
        }
        _posting = true;
        _postLines.Clear();
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("340", ResponseSeverity.Intermediate, "send article") });
    }

    /// <summary>
    /// Handles unrecognized lines, primarily used to receive article data during POST.
    /// </summary>
    /// <param name="line">Line received from the client.</param>
    /// <returns>Responses to send.</returns>
    private async Task<IEnumerable<ServerResponse>> HandleUnrecognizedAsync(string line)
    {
        if (_posting)
        {
            if (line == ".")
            {
                _posting = false;
                string article = string.Join("\r\n", _postLines) + "\r\n";
                int id = await _store.AddAsync(_currentGroup!, article);
                _currentArticle = id;
                return new[] { new ServerResponse("240", ResponseSeverity.Completion, "article received") };
            }
            if (line.StartsWith(".", StringComparison.Ordinal))
            {
                line = line[1..];
            }
            _postLines.Add(line);
            return Array.Empty<ServerResponse>();
        }
        return new[] { new ServerResponse("502", ResponseSeverity.PermanentNegative, "Command not implemented") };
    }

    /// <summary>
    /// Parses a date and time in YYYYMMDD HHMMSS format.
    /// </summary>
    /// <param name="date">Date portion.</param>
    /// <param name="time">Time portion.</param>
    /// <param name="result">Parsed date/time in UTC.</param>
    /// <returns>True on success.</returns>
    private static bool TryParseDateTime(string date, string time, out DateTime result)
    {
        return DateTime.TryParseExact(date + time, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
    }

    /// <summary>
    /// Handles the QUIT command.
    /// </summary>
    /// <param name="ctx">Command context.</param>
    /// <param name="args">Command arguments.</param>
    /// <returns>Responses to send.</returns>
    private Task<IEnumerable<ServerResponse>> HandleQuit(CommandContext ctx, string[] args)
    {
        return Task.FromResult<IEnumerable<ServerResponse>>(new[] { new ServerResponse("205", ResponseSeverity.Completion, "closing connection") });
    }
}

