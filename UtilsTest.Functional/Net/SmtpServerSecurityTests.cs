using System;
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
/// Security and limit-enforcement tests for <see cref="SmtpServer"/>:
/// authentication lockout and DATA body limits.
/// </summary>
[TestClass]
public class SmtpServerSecurityTests
{
    private sealed class InMemoryStore : ISmtpMessageStore
    {
        public SmtpMessage? Message { get; private set; }
        public Task StoreAsync(SmtpMessage message, CancellationToken ct = default)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAuthenticator : ISmtpAuthenticator
    {
        public Task<SmtpAuthenticationResult> AuthenticateAsync(string user, string password, CancellationToken ct = default)
        {
            bool ok = user == "user" && password == "pass";
            return Task.FromResult(new SmtpAuthenticationResult(ok, ok));
        }
    }

    // Reads multi-line SMTP responses (e.g. "250-...\r\n250 OK") until the final line.
    private static async Task<string?> ReadSmtpResponseAsync(System.IO.StreamReader reader)
    {
        string? line;
        do { line = await reader.ReadLineAsync(); }
        while (line != null && line.Length >= 4 && line[3] == '-');
        return line;
    }

    /// <summary>
    /// After <see cref="SmtpServer.MaxAuthAttempts"/> consecutive AUTH PLAIN failures the server
    /// must send 535, set the lockout flag, and immediately close the TCP session via
    /// <see cref="CommandResponseServer.CloseAfterResponse"/>. A subsequent read from the client
    /// must return EOF rather than another response.
    /// </summary>
    [TestMethod]
    public async Task Server_LocksOutAndClosesAfterMaxPlainAuthFailures()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        InMemoryStore store = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using SmtpServer server = new(store, null, new InMemoryAuthenticator());
            server.MaxAuthAttempts = 3;
            await server.StartAsync(serverClient.GetStream(), isTls: true);
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using System.IO.StreamReader reader = new(ns, Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

        await reader.ReadLineAsync(); // 220 greeting

        await writer.WriteLineAsync("EHLO test");
        await ReadSmtpResponseAsync(reader); // 250 OK (last line)

        string wrongCreds = Convert.ToBase64String(Encoding.ASCII.GetBytes("\0wronguser\0wrongpass"));
        for (int i = 0; i < 3; i++)
        {
            await writer.WriteLineAsync($"AUTH PLAIN {wrongCreds}");
            string? line = await reader.ReadLineAsync();
            Assert.IsNotNull(line, $"Expected 535 on attempt {i + 1}");
            Assert.IsTrue(line!.StartsWith("535"), $"Expected 535 on attempt {i + 1}, got: {line}");
        }

        // After MaxAuthAttempts failures CloseAfterResponse() closes the session.
        string? eof = await reader.ReadLineAsync();
        Assert.IsNull(eof, "Expected EOF after lockout");

        await serverTask;
    }

    /// <summary>
    /// The same lockout applies to AUTH LOGIN (multi-step exchange).
    /// Each failed attempt consists of: AUTH LOGIN → 334 Username → user → 334 Password → pass → 535.
    /// After MaxAuthAttempts full exchanges the server must close.
    /// </summary>
    [TestMethod]
    public async Task Server_LocksOutAndClosesAfterMaxLoginAuthFailures()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        InMemoryStore store = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using SmtpServer server = new(store, null, new InMemoryAuthenticator());
            server.MaxAuthAttempts = 3;
            await server.StartAsync(serverClient.GetStream(), isTls: true);
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using System.IO.StreamReader reader = new(ns, Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

        await reader.ReadLineAsync(); // 220

        await writer.WriteLineAsync("EHLO test");
        await ReadSmtpResponseAsync(reader); // 250 OK

        for (int i = 0; i < 3; i++)
        {
            await writer.WriteLineAsync("AUTH LOGIN");
            string? prompt1 = await reader.ReadLineAsync(); // 334 Username:
            Assert.IsTrue(prompt1!.StartsWith("334"), $"Attempt {i + 1}: expected 334 Username");

            await writer.WriteLineAsync(Convert.ToBase64String(Encoding.ASCII.GetBytes("wronguser")));
            string? prompt2 = await reader.ReadLineAsync(); // 334 Password:
            Assert.IsTrue(prompt2!.StartsWith("334"), $"Attempt {i + 1}: expected 334 Password");

            await writer.WriteLineAsync(Convert.ToBase64String(Encoding.ASCII.GetBytes("wrongpass")));
            string? result = await reader.ReadLineAsync(); // 535
            Assert.IsTrue(result!.StartsWith("535"), $"Attempt {i + 1}: expected 535, got: {result}");
        }

        string? eof = await reader.ReadLineAsync();
        Assert.IsNull(eof, "Expected EOF after lockout");

        await serverTask;
    }

    /// <summary>
    /// When <see cref="SmtpServer.MaxDataLines"/> is exceeded the server must respond with
    /// 552, clear the DATA buffer, and leave the session open for further commands.
    /// The stored message count must remain zero.
    /// </summary>
    [TestMethod]
    public async Task Server_RejectsDataBodyExceedingLineLimit()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        InMemoryStore store = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using SmtpServer server = new(store, d => d == "example.com");
            server.MaxDataLines = 5;
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using System.IO.StreamReader reader = new(ns, Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

        await reader.ReadLineAsync(); // 220

        await writer.WriteLineAsync("EHLO test");
        await ReadSmtpResponseAsync(reader); // 250 OK

        await writer.WriteLineAsync("MAIL FROM:<from@example.com>");
        Assert.IsTrue((await reader.ReadLineAsync())!.StartsWith("250"));

        await writer.WriteLineAsync("RCPT TO:<to@example.com>");
        Assert.IsTrue((await reader.ReadLineAsync())!.StartsWith("250"));

        await writer.WriteLineAsync("DATA");
        Assert.IsTrue((await reader.ReadLineAsync())!.StartsWith("354"));

        // Send MaxDataLines+1 lines; only the 6th triggers the 552 response.
        // Lines during DATA have no per-line response from the server.
        for (int i = 1; i <= 6; i++)
            await writer.WriteLineAsync($"Line {i}");

        string? response = await reader.ReadLineAsync();
        Assert.IsNotNull(response);
        Assert.IsTrue(response!.StartsWith("552"), $"Expected 552 after line limit, got: {response}");
        Assert.IsNull(store.Message, "Message must not be stored when the line limit is exceeded");

        client.Close();
        await serverTask;
    }

    /// <summary>
    /// When <see cref="SmtpServer.MaxDataChars"/> is exceeded the server must respond with 552.
    /// </summary>
    [TestMethod]
    public async Task Server_RejectsDataBodyExceedingCharLimit()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        InMemoryStore store = new();
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync();
            using SmtpServer server = new(store, d => d == "example.com");
            server.MaxDataChars = 50;
            await server.StartAsync(serverClient.GetStream());
            await server.Completion;
            listener.Stop();
        });

        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream ns = client.GetStream();
        using System.IO.StreamReader reader = new(ns, Encoding.ASCII, false, 1024, true);
        using StreamWriter writer = new(ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

        await reader.ReadLineAsync(); // 220

        await writer.WriteLineAsync("EHLO test");
        await ReadSmtpResponseAsync(reader);

        await writer.WriteLineAsync("MAIL FROM:<from@example.com>");
        await reader.ReadLineAsync(); // 250

        await writer.WriteLineAsync("RCPT TO:<to@example.com>");
        await reader.ReadLineAsync(); // 250

        await writer.WriteLineAsync("DATA");
        await reader.ReadLineAsync(); // 354

        // One line of 51 chars exceeds MaxDataChars=50.
        await writer.WriteLineAsync(new string('x', 51));

        string? response = await reader.ReadLineAsync();
        Assert.IsNotNull(response);
        Assert.IsTrue(response!.StartsWith("552"), $"Expected 552 after char limit, got: {response}");
        Assert.IsNull(store.Message);

        client.Close();
        await serverTask;
    }
}
