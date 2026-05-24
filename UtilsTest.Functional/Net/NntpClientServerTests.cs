using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="NntpClient"/> and <see cref="NntpServer"/> interaction.
/// </summary>
[TestClass]
public class NntpClientServerTests
{
    /// <summary>
    /// Verifies that the NNTP client can retrieve an article from the server.
    /// </summary>
    [TestMethod]
    public async Task NntpClientServer_BasicFlow()
    {
        InMemoryArticleStore store = new();
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream ns = serverClient.GetStream();
            using NntpServer server = new(store);
            await server.StartAsync(ns);
            await server.Completion;
            listener.Stop();
        });

        using NntpClient client = new();
        await client.ConnectAsync("127.0.0.1", port);
        IReadOnlyList<(string group, int last, int first)> groups = await client.ListAsync();
        Assert.AreEqual("comp.test", groups[0].group);
        IReadOnlyList<string> newGroups = await client.NewGroupsAsync(DateTime.UtcNow.AddDays(-4));
        CollectionAssert.Contains((System.Collections.ICollection)newGroups, "comp.test");
        // Uses a margin larger than one day to avoid boundary timing flakiness with strict "> since" filtering.
        IReadOnlyList<int> newNews = await client.NewNewsAsync("comp.test", DateTime.UtcNow.AddDays(-2));
        CollectionAssert.Contains((System.Collections.ICollection)newNews, 1);
        (int count, int first, int last) info = await client.GroupAsync("comp.test");
        Assert.AreEqual(1, info.count);
        int? nextId = await client.NextAsync();
        Assert.AreEqual(1, nextId);
        string header = await client.HeaderAsync(1);
        Assert.AreEqual("header\r\n", header);
        string body = await client.BodyAsync(1);
        Assert.AreEqual("hello\r\n", body);
        (int id, string messageId) stat = await client.StatAsync(1);
        Assert.AreEqual(1, stat.id);
        await client.PostAsync("header2\r\n\r\nposted\r\n");
        IReadOnlyList<int> newNews2 = await client.NewNewsAsync("comp.test", DateTime.UtcNow.AddMinutes(-1));
        CollectionAssert.Contains((System.Collections.ICollection)newNews2, 2);
        await client.QuitAsync();
        await serverTask;
    }

    /// <summary>
    /// Simple in-memory article store used for testing.
    /// </summary>
    private sealed class InMemoryArticleStore : INntpArticleStore
    {
        private sealed class Article
        {
            public Article(string text, DateTime date)
            {
                Text = text;
                Date = date;
            }

            public string Text { get; }

            public DateTime Date { get; }
        }

        private sealed class Group
        {
            public Group(DateTime created)
            {
                Created = created;
            }

            public DateTime Created { get; }

            public Dictionary<int, Article> Articles { get; } = new();
        }

        private readonly Dictionary<string, Group> _groups = new()
        {
            ["comp.test"] = new Group(DateTime.UtcNow.AddDays(-3))
            {
                Articles = { [1] = new Article("header\r\n\r\nhello\r\n", DateTime.UtcNow.AddDays(-1)) }
            }
        };

        public Task<IReadOnlyCollection<string>> ListGroupsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<string>>(_groups.Keys.ToList());
        }

        public Task<DateTime?> GetGroupCreationDateAsync(string group, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DateTime?>(_groups.TryGetValue(group, out Group? g) ? g.Created : null);
        }

        public Task<IReadOnlyDictionary<int, string>> ListAsync(string group, CancellationToken cancellationToken = default)
        {
            if (_groups.TryGetValue(group, out Group? g))
            {
                Dictionary<int, string> result = new();
                foreach (KeyValuePair<int, Article> kvp in g.Articles)
                {
                    result[kvp.Key] = kvp.Value.Text;
                }
                return Task.FromResult<IReadOnlyDictionary<int, string>>(result);
            }
            return Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
        }

        public Task<IReadOnlyCollection<int>> ListNewsSinceAsync(string group, DateTime sinceUtc, CancellationToken cancellationToken = default)
        {
            if (_groups.TryGetValue(group, out Group? g))
            {
                List<int> ids = new();
                foreach (KeyValuePair<int, Article> kvp in g.Articles)
                {
                    if (kvp.Value.Date > sinceUtc)
                    {
                        ids.Add(kvp.Key);
                    }
                }
                return Task.FromResult<IReadOnlyCollection<int>>(ids);
            }
            return Task.FromResult<IReadOnlyCollection<int>>(System.Array.Empty<int>());
        }

        public Task<string?> RetrieveAsync(string group, int id, CancellationToken cancellationToken = default)
        {
            if (_groups.TryGetValue(group, out Group? g) && g.Articles.TryGetValue(id, out Article? article))
            {
                return Task.FromResult<string?>(article.Text);
            }
            return Task.FromResult<string?>(null);
        }

        public Task<int> AddAsync(string group, string article, CancellationToken cancellationToken = default)
        {
            if (!_groups.TryGetValue(group, out Group? g))
            {
                g = new Group(DateTime.UtcNow);
                _groups[group] = g;
            }
            int id = g.Articles.Count == 0 ? 1 : g.Articles.Keys.Max() + 1;
            g.Articles[id] = new Article(article, DateTime.UtcNow);
            return Task.FromResult(id);
        }
    }
}

