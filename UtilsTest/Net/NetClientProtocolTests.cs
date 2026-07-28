using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Unit tests covering audit items 40, 43, 59, 62, 64, 65, 67, 68.
/// </summary>
[TestClass]
public class NetClientProtocolTests
{
    // ──────────────────────────────────────────────────────────────
    // Infrastructure
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps two separate read/write streams into one bidirectional stream,
    /// used to inject fake server data into <see cref="CommandResponseClient"/>.
    /// </summary>
    private sealed class DuplexStream : Stream
    {
        private readonly Stream _readFrom;
        private readonly Stream _writeTo;

        public DuplexStream(Stream readFrom, Stream writeTo)
        {
            _readFrom = readFrom;
            _writeTo = writeTo;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => _readFrom.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _readFrom.ReadAsync(buffer, offset, count, ct);
        public override void Write(byte[] buffer, int offset, int count) => _writeTo.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _writeTo.WriteAsync(buffer, offset, count, ct);
        public override void Flush() => _writeTo.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _readFrom.Dispose(); _writeTo.Dispose(); }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Creates an in-process bidirectional pipe pair so that a fake server task and a
    /// real protocol client can exchange lines without a real TCP connection.
    /// </summary>
    private static (DuplexStream clientStream, StreamWriter serverWriter, StreamReader serverReader) CreateTestPair()
    {
        Pipe serverToClient = new Pipe();
        Pipe clientToServer = new Pipe();

        DuplexStream clientStream = new DuplexStream(
            serverToClient.Reader.AsStream(),
            clientToServer.Writer.AsStream());

        StreamWriter serverWriter = new StreamWriter(serverToClient.Writer.AsStream(), Encoding.ASCII)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };
        StreamReader serverReader = new StreamReader(clientToServer.Reader.AsStream(), Encoding.ASCII);

        return (clientStream, serverWriter, serverReader);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 40 — NetworkParameters.PrimaryDns null-safe (no IndexOutOfRangeException)
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void NetworkParameters_PrimaryDns_EqualsFirstDnsServerOrNull()
    {
        NetworkParameters p = new NetworkParameters();
        IPAddress[] dns = p.DnsServers;
        IPAddress? expected = dns.Length > 0 ? dns[0] : null;
        Assert.AreEqual(expected, p.PrimaryDns);
    }

    [TestMethod]
    public void NetworkParameters_DnsServers_ReturnsDefensiveCopy()
    {
        NetworkParameters p = new NetworkParameters();
        IPAddress[] first = p.DnsServers;
        // Mutate the returned array
        if (first.Length > 0) first[0] = IPAddress.Loopback;
        IPAddress[] second = p.DnsServers;
        // The mutation must not affect the stored state
        Assert.AreEqual(p.PrimaryDns, second.Length > 0 ? second[0] : null);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 62 — SMTP: null / empty recipients validated before MAIL FROM
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SmtpClient_SendMailAsync_NullRecipients_ThrowsArgumentNullException()
    {
        SmtpClient client = new SmtpClient();
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
            client.SendMailAsync("sender@example.com", null!, "body"));
    }

    [TestMethod]
    public async Task SmtpClient_SendMailAsync_EmptyRecipients_ThrowsArgumentException()
    {
        SmtpClient client = new SmtpClient();
        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            client.SendMailAsync("sender@example.com", new string[0], "body"));
    }

    // ──────────────────────────────────────────────────────────────
    // Item 59 — SMTP EHLO: last extension line is included
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SmtpClient_EhloAsync_IncludesLastExtension()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            // Greeting
            await sw.WriteLineAsync("220 smtp.example.com ESMTP");
            // Client reads greeting in OnConnect
            _ = await sr.ReadLineAsync(); // discard EHLO command
            await sw.WriteLineAsync("250-smtp.example.com Hello");
            await sw.WriteLineAsync("250-SIZE 10240000");
            await sw.WriteLineAsync("250 STARTTLS");
            sw.BaseStream.Flush();
        });

        SmtpClient client = new SmtpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        IReadOnlyList<string> extensions = await client.EhloAsync("testclient.local");
        await serverTask;

        // The last "STARTTLS" line must be included
        Assert.IsTrue(extensions.Contains("STARTTLS"),
            $"Expected 'STARTTLS' in extensions but got: [{string.Join(", ", extensions)}]");
        Assert.IsTrue(extensions.Contains("SIZE 10240000"),
            "Expected 'SIZE 10240000' in extensions.");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 65 — POP3 RETR: CRLF line endings preserved
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Pop3Client_RetrieveAsync_UsesCrlfLineEndings()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("+OK POP3 ready");
            _ = await sr.ReadLineAsync(); // RETR 1
            await sw.WriteLineAsync("+OK message follows");
            await sw.WriteLineAsync("Subject: hello");
            await sw.WriteLineAsync("body text");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        Pop3Client client = new Pop3Client();
        await client.ConnectAsync(clientStream, leaveOpen: false);
#pragma warning disable CS0618
        string message = await client.RetrieveAsync(1);
#pragma warning restore CS0618
        await serverTask;

        Assert.AreEqual("Subject: hello\r\nbody text\r\n", message);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 64 — POP3 exact terminator: ". " (dot-space) is data, not terminator
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Pop3Client_DotSpaceLine_IsDataNotTerminator()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("+OK POP3 ready");
            _ = await sr.ReadLineAsync(); // RETR 1
            await sw.WriteLineAsync("+OK message follows");
            // ". " must be treated as data (not the terminator ".")
            await sw.WriteLineAsync(". ");
            await sw.WriteLineAsync("next line");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        Pop3Client client = new Pop3Client();
        await client.ConnectAsync(clientStream, leaveOpen: false);
#pragma warning disable CS0618
        string message = await client.RetrieveAsync(1);
#pragma warning restore CS0618
        await serverTask;

        // Both lines must be present; ". " is data, "next line" follows it
        StringAssert.Contains(message, ". \r\n");
        StringAssert.Contains(message, "next line\r\n");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 68 — NNTP dot-unstuffing: only ".." lines are unstuffed
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task NntpClient_ArticleAsync_DoubleDotIsUnstuffed()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("200 NNTP server ready");
            _ = await sr.ReadLineAsync(); // ARTICLE 1
            await sw.WriteLineAsync("220 1 <msg@example.com> article follows");
            await sw.WriteLineAsync("..dot-prefixed content");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        NntpClient client = new NntpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        string article = await client.ArticleAsync(1);
        await serverTask;

        // "..dot-prefixed content" → ".dot-prefixed content" after unstuffing
        Assert.AreEqual(".dot-prefixed content\r\n", article);
    }

    [TestMethod]
    public async Task NntpClient_ArticleAsync_SingleDotPrefixIsNotUnstuffed()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("200 NNTP server ready");
            _ = await sr.ReadLineAsync(); // ARTICLE 1
            await sw.WriteLineAsync("220 1 <msg@example.com> article follows");
            await sw.WriteLineAsync(".malformed");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        NntpClient client = new NntpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        string article = await client.ArticleAsync(1);
        await serverTask;

        // ".malformed" must NOT be unstuffed — the leading dot is preserved
        Assert.AreEqual(".malformed\r\n", article);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 67 — NNTP UTC: Unspecified Kind treated as UTC, not Local
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task NntpClient_NewGroupsAsync_UnspecifiedDateTimeKindSentAsUtc()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();
        string? capturedCommand = null;

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("200 NNTP server ready");
            capturedCommand = await sr.ReadLineAsync(); // NEWGROUPS ...
            await sw.WriteLineAsync("231 new newsgroups follow");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        NntpClient client = new NntpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        // Unspecified kind: numeric value is 2024-01-15 08:30:00
        DateTime unspecified = new DateTime(2024, 1, 15, 8, 30, 0, DateTimeKind.Unspecified);
        await client.NewGroupsAsync(unspecified);
        await serverTask;

        // Must format the value as-is (treating Unspecified as UTC), not convert through local timezone
        Assert.AreEqual("NEWGROUPS 20240115 083000", capturedCommand,
            "DateTimeKind.Unspecified must be treated as UTC, not converted through local timezone.");
    }
}
