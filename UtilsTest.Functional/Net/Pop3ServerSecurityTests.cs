using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Tests for <see cref="Pop3Server"/> authentication lockout behaviour.
/// After <see cref="Pop3Server.MaxAuthAttempts"/> consecutive failures the server must
/// send the final -ERR and immediately close the TCP session via
/// <see cref="CommandResponseServer.CloseAfterResponse"/>.
/// </summary>
[TestClass]
public class Pop3ServerSecurityTests
{
    /// <summary>Minimal mailbox stub; authentication always returns false for wrong credentials.</summary>
    private sealed class InMemoryMailbox : IPop3Mailbox
    {
        public Task<bool> AuthenticateAsync(string user, string password, CancellationToken ct = default)
            => Task.FromResult(user == "user" && password == "pass");

        public Task<bool> AuthenticateApopAsync(string user, string timestamp, string digest, CancellationToken ct = default)
        {
            if (user != "user") return Task.FromResult(false);
            using MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.ASCII.GetBytes(timestamp + "pass"));
            string expected = Convert.ToHexString(hash).ToLowerInvariant();
            return Task.FromResult(string.Equals(expected, digest, StringComparison.OrdinalIgnoreCase));
        }

        public Task<IReadOnlyDictionary<int, int>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());

        public Task<IReadOnlyDictionary<int, string>> ListUidsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());

        public Task<string> RetrieveAsync(int id, CancellationToken ct = default)
            => Task.FromResult(string.Empty);

        public Task DeleteAsync(int id, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// After MaxAuthAttempts consecutive USER/PASS failures the server must send
    /// "-ERR Too many authentication failures — bye" and close the session.
    /// </summary>
    [TestMethod]
    public async Task Server_LocksOutAndClosesAfterMaxPassAuthFailures()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using Pop3Server server = new(new InMemoryMailbox());
            server.MaxAuthAttempts = 3;
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using System.IO.StreamReader reader = new(ns, Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

        await reader.ReadLineAsync(); // +OK greeting

        for (int i = 0; i < 3; i++)
        {
            await writer.WriteLineAsync("USER user");
            string? userReply = await reader.ReadLineAsync();
            Assert.IsTrue(userReply!.StartsWith("+OK"), $"Attempt {i + 1}: expected +OK for USER");

            await writer.WriteLineAsync("PASS wrongpassword");
            string? passReply = await reader.ReadLineAsync();
            Assert.IsNotNull(passReply, $"Expected -ERR on attempt {i + 1}");
            Assert.IsTrue(passReply!.StartsWith("-ERR"), $"Attempt {i + 1}: expected -ERR, got: {passReply}");
        }

        // After MaxAuthAttempts the session is closed by CloseAfterResponse().
        string? eof = await reader.ReadLineAsync();
        Assert.IsNull(eof, "Expected EOF after lockout");

        await serverTask;
    }

    /// <summary>
    /// After MaxAuthAttempts consecutive APOP failures the server must close the session.
    /// APOP is an alternative authentication mechanism (MD5 challenge-response) and uses
    /// the same <c>_failedAuthCount</c> / <c>_authLocked</c> logic as USER/PASS.
    /// </summary>
    [TestMethod]
    public async Task Server_LocksOutAndClosesAfterMaxApopAuthFailures()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using Pop3Server server = new(new InMemoryMailbox());
            server.MaxAuthAttempts = 3;
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using System.IO.StreamReader reader = new(ns, Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

        await reader.ReadLineAsync(); // +OK <timestamp@localhost>

        // Send 3 APOP attempts with an incorrect digest.
        for (int i = 0; i < 3; i++)
        {
            await writer.WriteLineAsync("APOP user wrongdigest");
            string? reply = await reader.ReadLineAsync();
            Assert.IsNotNull(reply, $"Expected -ERR on attempt {i + 1}");
            Assert.IsTrue(reply!.StartsWith("-ERR"), $"Attempt {i + 1}: expected -ERR, got: {reply}");
        }

        string? eof = await reader.ReadLineAsync();
        Assert.IsNull(eof, "Expected EOF after lockout");

        await serverTask;
    }
}
