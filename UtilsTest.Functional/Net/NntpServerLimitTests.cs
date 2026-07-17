using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="NntpServer"/> article-size limits and required-header validation.
/// All tests use raw TCP to observe exact NNTP responses during article upload.
/// </summary>
[TestClass]
public class NntpServerLimitTests
{
    /// <summary>
    /// Minimal article store with one pre-seeded article so that GROUP returns 211.
    /// </summary>
    private sealed class InMemoryArticleStore : INntpArticleStore
    {
        private readonly Dictionary<int, string> _articles = new()
        {
            [1] = "From: seed@example.com\r\nNewsgroups: comp.test\r\nSubject: Seed\r\n\r\nSeed article.\r\n"
        };
        private int _nextId = 2;

        public Task<IReadOnlyCollection<string>> ListGroupsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<string>>(new[] { "comp.test" });

        public Task<DateTime?> GetGroupCreationDateAsync(string group, CancellationToken ct = default)
            => Task.FromResult<DateTime?>(group == "comp.test" ? (DateTime?)DateTime.UtcNow.AddDays(-1) : null);

        public Task<IReadOnlyDictionary<int, string>> ListAsync(string group, CancellationToken ct = default)
        {
            if (group != "comp.test")
                return Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
            return Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>(_articles));
        }

        public Task<IReadOnlyCollection<int>> ListNewsSinceAsync(string group, DateTime since, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<int>>(Array.Empty<int>());

        public Task<string?> RetrieveAsync(string group, int id, CancellationToken ct = default)
            => Task.FromResult(_articles.TryGetValue(id, out string? a) ? a : null);

        public Task<int> AddAsync(string group, string article, CancellationToken ct = default)
        {
            int id = _nextId++;
            _articles[id] = article;
            return Task.FromResult(id);
        }
    }

    /// <summary>
    /// When an article being posted exceeds <see cref="NntpServer.MaxPostLines"/> the server
    /// must respond with 441 and discard the partial article without storing it.
    /// </summary>
    [TestMethod]
    public async Task Server_RejectsPostExceedingLineLimit()
    {
        InMemoryArticleStore store = new();
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NntpServer server = new(store, () => true);
            server.MaxPostLines = 5;
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using System.IO.StreamReader reader = new(ns, Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

        string? greeting = await reader.ReadLineAsync();
        Assert.IsTrue(greeting!.StartsWith("200"), $"Expected 200 greeting, got: {greeting}");

        await writer.WriteLineAsync("GROUP comp.test");
        string? groupReply = await reader.ReadLineAsync();
        Assert.IsTrue(groupReply!.StartsWith("211"), $"Expected 211, got: {groupReply}");

        await writer.WriteLineAsync("POST");
        string? postReady = await reader.ReadLineAsync();
        Assert.IsTrue(postReady!.StartsWith("340"), $"Expected 340, got: {postReady}");

        // Lines 1-5 fit within MaxPostLines=5; line 6 triggers the 441 response.
        // No "." terminator is sent; the server rejects mid-stream.
        // Note: article lines must not start with registered NNTP command names (GROUP, BODY,
        // ARTICLE, etc.) because ProcessQueueAsync checks registered handlers before CommandReceived.
        string[] lines = [
            "From: test@example.com",
            "Newsgroups: comp.test",
            "Subject: Test",
            "",
            "Content line one",
            "Content line two pushing the count past MaxPostLines=5",
        ];
        foreach (string line in lines)
            await writer.WriteLineAsync(line);

        string? response = await reader.ReadLineAsync();
        Assert.IsNotNull(response);
        Assert.IsTrue(response!.StartsWith("441"),
            $"Expected 441 (line limit exceeded), got: {response}");
        StringAssert.Contains(response, "line limit",
            "Error message should identify the line-limit cause.");

        client.Close();
        await serverTask;
    }

    /// <summary>
    /// When the cumulative character count of a posted article exceeds
    /// <see cref="NntpServer.MaxPostChars"/> the server must respond with 441.
    /// </summary>
    [TestMethod]
    public async Task Server_RejectsPostExceedingCharLimit()
    {
        InMemoryArticleStore store = new();
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NntpServer server = new(store, () => true);
            server.MaxPostChars = 100;
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using System.IO.StreamReader reader = new(ns, Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

        await reader.ReadLineAsync(); // 200

        await writer.WriteLineAsync("GROUP comp.test");
        await reader.ReadLineAsync(); // 211

        await writer.WriteLineAsync("POST");
        await reader.ReadLineAsync(); // 340

        // Required headers (~54 chars) + blank + 71-char body line → total > MaxPostChars=100.
        await writer.WriteLineAsync("From: test@example.com");  // 22 chars
        await writer.WriteLineAsync("Newsgroups: comp.test");   // 21 chars
        await writer.WriteLineAsync("Subject: hi");             // 11 chars
        await writer.WriteLineAsync("");                        //  0 chars
        await writer.WriteLineAsync(new string('x', 71));      // 71 chars — cumulative > 100

        string? response = await reader.ReadLineAsync();
        Assert.IsNotNull(response);
        Assert.IsTrue(response!.StartsWith("441"),
            $"Expected 441 (size limit exceeded), got: {response}");
        StringAssert.Contains(response, "size limit",
            "Error message should identify the size-limit cause.");

        client.Close();
        await serverTask;
    }

    /// <summary>
    /// If a posted article is missing a required header (From, Newsgroups, or Subject)
    /// the server must respond 441 after the terminating "." line rather than storing the article.
    /// </summary>
    [TestMethod]
    public async Task Server_RejectsPostMissingRequiredHeader()
    {
        InMemoryArticleStore store = new();
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NntpServer server = new(store, () => true);
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using System.IO.StreamReader reader = new(ns, Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

        await reader.ReadLineAsync(); // 200

        await writer.WriteLineAsync("GROUP comp.test");
        await reader.ReadLineAsync(); // 211

        await writer.WriteLineAsync("POST");
        await reader.ReadLineAsync(); // 340

        // Article has From and Newsgroups but is missing the required Subject header.
        // Note: body lines must not start with NNTP command names (BODY, GROUP, etc.)
        // because ProcessQueueAsync checks registered handlers before CommandReceived.
        await writer.WriteLineAsync("From: test@example.com");
        await writer.WriteLineAsync("Newsgroups: comp.test");
        await writer.WriteLineAsync("");
        await writer.WriteLineAsync("Text without a Subject header.");
        await writer.WriteLineAsync("."); // end-of-article marker

        string? response = await reader.ReadLineAsync();
        Assert.IsNotNull(response);
        Assert.IsTrue(response!.StartsWith("441"),
            $"Expected 441 (missing header), got: {response}");
        StringAssert.Contains(response, "Subject",
            "Error should identify the missing header by name.");

        client.Close();
        await serverTask;
    }
}
