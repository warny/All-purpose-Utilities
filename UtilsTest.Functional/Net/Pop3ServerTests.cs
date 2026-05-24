using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="Pop3Server"/>.
/// </summary>
[TestClass]
public class Pop3ServerTests
{
    /// <summary>
    /// Verifies that the POP3 server interacts with the mailbox provider.
    /// </summary>
    [TestMethod]
    public async Task Pop3Server_BasicFlow()
    {
        InMemoryMailbox mailbox = new();
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream ns = serverClient.GetStream();
            using Pop3Server server = new(mailbox);
            await server.StartAsync(ns);
            await server.Completion;
            listener.Stop();
        });

        using Pop3Client client = new();
        await client.ConnectAsync("127.0.0.1", port);
        await client.AuthenticateAsync("user", "pass");
        (int count, int size) stat = await client.GetStatAsync();
        Assert.AreEqual(2, stat.count);
        IReadOnlyDictionary<int, int> list = await client.ListAsync();
        Assert.AreEqual(2, list.Count);
        string message = await client.RetrieveAsync(1);
        StringAssert.Contains(message, "line1");
        StringAssert.Contains(message, ".leading");
        await client.DeleteAsync(1);
        await client.QuitAsync();
        await serverTask;
    }

    /// <summary>
    /// Verifies extended POP3 commands such as APOP, RSET, CAPA and UIDL.
    /// </summary>
    [TestMethod]
    public async Task Pop3Server_AdditionalCommands()
    {
        InMemoryMailbox mailbox = new();
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using NetworkStream ns = serverClient.GetStream();
            using Pop3Server server = new(mailbox);
            await server.StartAsync(ns);
            await server.Completion;
            listener.Stop();
        });

        using Pop3Client client = new();
        await client.ConnectAsync("127.0.0.1", port);
        IReadOnlyList<string> capa = await client.GetCapabilitiesAsync();
        CollectionAssert.Contains((System.Collections.ICollection)capa, "UIDL");
        CollectionAssert.Contains((System.Collections.ICollection)capa, "APOP");
        await client.AuthenticateApopAsync("user", "pass");
        IReadOnlyDictionary<int, string> uids = await client.ListUniqueIdsAsync();
        Assert.AreEqual(2, uids.Count);
        Assert.AreEqual("uid1", await client.GetUniqueIdAsync(1));
        await client.DeleteAsync(1);
        IReadOnlyDictionary<int, int> list = await client.ListAsync();
        Assert.AreEqual(1, list.Count);
        await client.ResetAsync();
        IReadOnlyDictionary<int, int> resetList = await client.ListAsync();
        Assert.AreEqual(2, resetList.Count);
        await client.QuitAsync();
        await serverTask;
    }

    /// <summary>
    /// Simple in-memory mailbox used for testing.
    /// </summary>
    private sealed class InMemoryMailbox : IPop3Mailbox
    {
        private readonly Dictionary<int, string> _messages = new()
        {
            [1] = "line1\r\n.leading\r\n",
            [2] = "second\r\n"
        };

        private readonly Dictionary<int, string> _uids = new()
        {
            [1] = "uid1",
            [2] = "uid2"
        };

        /// <inheritdoc />
        public Task<bool> AuthenticateAsync(string user, string password, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(user == "user" && password == "pass");
        }

        /// <inheritdoc />
        public Task<bool> AuthenticateApopAsync(string user, string timestamp, string digest, CancellationToken cancellationToken = default)
        {
            if (user != "user")
            {
                return Task.FromResult(false);
            }
            using MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.ASCII.GetBytes(timestamp + "pass"));
            string expected = Convert.ToHexString(hash).ToLowerInvariant();
            return Task.FromResult(string.Equals(expected, digest, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public Task<IReadOnlyDictionary<int, int>> ListAsync(CancellationToken cancellationToken = default)
        {
            Dictionary<int, int> list = new();
            foreach (KeyValuePair<int, string> pair in _messages)
            {
                list[pair.Key] = pair.Value.Length;
            }
            return Task.FromResult<IReadOnlyDictionary<int, int>>(list);
        }

        /// <inheritdoc />
        public Task<string> RetrieveAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_messages[id]);
        }

        /// <inheritdoc />
        public Task<IReadOnlyDictionary<int, string>> ListUidsAsync(CancellationToken cancellationToken = default)
        {
            Dictionary<int, string> list = new();
            foreach (KeyValuePair<int, string> pair in _uids)
            {
                if (_messages.ContainsKey(pair.Key))
                {
                    list[pair.Key] = pair.Value;
                }
            }
            return Task.FromResult<IReadOnlyDictionary<int, string>>(list);
        }

        /// <inheritdoc />
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            _messages.Remove(id);
            _uids.Remove(id);
            return Task.CompletedTask;
        }
    }
}
