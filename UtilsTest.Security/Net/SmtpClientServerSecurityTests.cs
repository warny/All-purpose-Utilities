using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Security.Net;

/// <summary>
/// Verifies the SMTP relay authorization boundary: an unauthenticated client must never be able
/// to relay mail, and relaying must succeed once the client has authenticated (PLAIN or LOGIN).
/// </summary>
[TestClass]
public class SmtpClientServerSecurityTests
{
    /// <summary>
    /// Ensures that relaying is rejected when the client does not authenticate.
    /// </summary>
    [TestMethod]
    public async Task Server_RejectsRelayWithoutAuthentication()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        InMemoryStore store = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using SmtpServer server = new(store, d => d == "example.com", new InMemoryAuthenticator());
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using SmtpClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        await client.EhloAsync("localhost");
        await Assert.ThrowsExactlyAsync<ProtocolResponseException>(() =>
            client.SendMailAsync("alice@example.com", new[] { "bob@other.com" }, "Subject: Test\r\n\r\nBody"));
        await client.QuitAsync();
        await serverTask;

        Assert.IsNull(store.Message);
    }

    /// <summary>
    /// Verifies that relaying succeeds after authentication.
    /// </summary>
    [TestMethod]
    public async Task Server_AllowsRelayAfterAuthentication()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        InMemoryStore store = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using SmtpServer server = new(store, d => d == "example.com", new InMemoryAuthenticator());
            await server.StartAsync(serverClient.GetStream(), isTls: true);
            await server.Completion;
            listener.Stop();
        });

        using SmtpClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        IReadOnlyList<string> extensions = await client.EhloAsync("localhost");
        CollectionAssert.Contains((System.Collections.ICollection)extensions, "AUTH PLAIN LOGIN");
        await client.AuthenticateAsync("user", "pass");
        await client.SendMailAsync("alice@example.com", new[] { "bob@other.com" }, "Subject: Test\r\n\r\nBody");
        await client.QuitAsync();
        await serverTask;

        Assert.IsNotNull(store.Message);
        Assert.AreEqual("bob@other.com", store.Message!.Recipients[0]);
    }

    /// <summary>
    /// Verifies that the LOGIN authentication mechanism is accepted and allows relaying.
    /// </summary>
    [TestMethod]
    public async Task Server_AllowsRelayAfterLoginAuthentication()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        InMemoryStore store = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using SmtpServer server = new(store, d => d == "example.com", new InMemoryAuthenticator());
            await server.StartAsync(serverClient.GetStream(), isTls: true);
            await server.Completion;
            listener.Stop();
        });

        using SmtpClient client = new() { NoOpInterval = Timeout.InfiniteTimeSpan };
        await client.ConnectAsync("127.0.0.1", port);
        await client.EhloAsync("localhost");
        await client.AuthenticateAsync("user", "pass", SmtpAuthenticationMechanism.Login);
        await client.SendMailAsync("alice@example.com", new[] { "bob@other.com" }, "Subject: Test\r\n\r\nBody");
        await client.QuitAsync();
        await serverTask;

        Assert.IsNotNull(store.Message);
        Assert.AreEqual("bob@other.com", store.Message!.Recipients[0]);
    }

    private sealed class InMemoryStore : ISmtpMessageStore
    {
        public SmtpMessage? Message { get; private set; }

        public Task StoreAsync(SmtpMessage message, CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Simple authenticator used by the tests.
    /// </summary>
    private sealed class InMemoryAuthenticator : ISmtpAuthenticator
    {
        /// <summary>
        /// Authenticates the supplied credentials.
        /// </summary>
        /// <param name="user">User name.</param>
        /// <param name="password">Password.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Authentication result.</returns>
        public Task<SmtpAuthenticationResult> AuthenticateAsync(string user, string password, CancellationToken cancellationToken = default)
        {
            bool success = user == "user" && password == "pass";
            return Task.FromResult(new SmtpAuthenticationResult(success, success));
        }
    }
}
