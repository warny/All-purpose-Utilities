using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
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

    /// <summary>Exposes protected multiline behavior for focused framing tests.</summary>
    private sealed class MultilineTestClient : CommandResponseClient
    {
        /// <summary>Reads a body terminated by a caller-defined marker.</summary>
        public async Task<IReadOnlyList<string>> ReadUntilEndAsync(CancellationToken cancellationToken = default)
        {
            var (_, body) = await SendMultilineCommandAsync(
                "MULTI", response => response.Code == "END", 10, 100, 100, cancellationToken);
            List<string> result = [];
            foreach (ServerResponse response in body)
                result.Add(BodyLineToString(response));
            return result;
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

    /// <summary>Verifies the legacy protected multiline callback remains authoritative.</summary>
    [TestMethod]
    public async Task CommandResponseClient_Multiline_UsesCustomTerminator()
    {
        (DuplexStream stream, StreamWriter writer, StreamReader reader) = CreateTestPair();
        Task server = Task.Run(async () =>
        {
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("200 body follows");
            await writer.WriteLineAsync("first");
            await writer.WriteLineAsync("END");
            await writer.WriteLineAsync("orphan");
        });
        MultilineTestClient client = new();
        await client.ConnectAsync(stream);
        IReadOnlyList<string> body = await client.ReadUntilEndAsync();
        CollectionAssert.AreEqual(new[] { "first" }, body.ToArray());
        await server;
    }

    /// <summary>Verifies SMTP exchange status collection enforces MaxResponseCount and poisons framing.</summary>
    [TestMethod]
    public async Task SmtpClient_ExclusiveStatusReader_EnforcesMaxResponseCount()
    {
        (DuplexStream stream, StreamWriter writer, StreamReader reader) = CreateTestPair();
        Task server = Task.Run(async () =>
        {
            await writer.WriteLineAsync("220 ready");
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("250-first");
            await writer.WriteLineAsync("250 second");
        });
        SmtpClient client = new() { MaxResponseCount = 1 };
        await client.ConnectAsync(stream);
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.EhloAsync("example.test"));
        Assert.IsFalse(client.IsConnected);
        Assert.IsNotNull(client.SessionFailure);
        await server;
    }

    /// <summary>Verifies malformed POP3 mandatory fields produce structured diagnostics.</summary>
    [TestMethod]
    public async Task Pop3Client_MalformedStat_ThrowsProtocolResponseException()
    {
        (DuplexStream stream, StreamWriter writer, StreamReader reader) = CreateTestPair();
        Task server = Task.Run(async () =>
        {
            await writer.WriteLineAsync("+OK ready");
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("+OK 12");
        });
        Pop3Client client = new();
        await client.ConnectAsync(stream);
        ProtocolResponseException error = await Assert.ThrowsExceptionAsync<ProtocolResponseException>(() => client.GetStatAsync());
        Assert.AreEqual("STAT", error.Command);
        await server;
    }

    /// <summary>Verifies POP3 CAPA failures retain the correct sanitized command context.</summary>
    [TestMethod]
    public async Task Pop3Client_CapabilityFailure_UsesCapaCommandContext()
    {
        (DuplexStream stream, StreamWriter writer, StreamReader reader) = CreateTestPair();
        Task server = Task.Run(async () =>
        {
            await writer.WriteLineAsync("+OK ready");
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("-ERR unsupported");
        });
        Pop3Client client = new();
        await client.ConnectAsync(stream);
        ProtocolResponseException error = await Assert.ThrowsExceptionAsync<ProtocolResponseException>(() => client.GetCapabilitiesAsync());
        Assert.AreEqual("CAPA", error.Command);
        await server;
    }

    /// <summary>Verifies materializing multiline commands also apply the byte limit.</summary>
    [TestMethod]
    public async Task Pop3Client_List_ByteLimitExceeded_PoisonsSession()
    {
        (DuplexStream stream, StreamWriter writer, StreamReader reader) = CreateTestPair();
        Task server = Task.Run(async () =>
        {
            await writer.WriteLineAsync("+OK ready");
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("+OK list follows");
            await writer.WriteLineAsync("1 12345");
            await writer.WriteLineAsync(".");
        });
        Pop3Client client = new() { MaxMultilineBytes = 2 };
        await client.ConnectAsync(stream);
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => client.ListAsync());
        Assert.IsFalse(client.IsConnected);
        await server;
    }

    // ──────────────────────────────────────────────────────────────
    // Item 40 — NetworkParameters.PrimaryDns null-safe
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
        if (first.Length > 0) first[0] = IPAddress.Loopback;
        IPAddress[] second = p.DnsServers;
        Assert.AreEqual(p.PrimaryDns, second.Length > 0 ? second[0] : null);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 62 — SMTP: recipients validated before any network I/O
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

    [TestMethod]
    public async Task SmtpClient_SendMailAsync_InvalidRecipient_NoBytesSentToServer()
    {
        // Verifies that recipient validation happens before any MAIL FROM is transmitted.
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        bool mailFromSent = false;
        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("220 smtp.example.com ESMTP");
            // Respond to MAIL FROM if it arrives
            string? line = await sr.ReadLineAsync();
            if (line is not null && line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase))
                mailFromSent = true;
        });

        SmtpClient client = new SmtpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);

        // Second recipient has a CR, which ValidateCommandArgument rejects before MAIL FROM
        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            client.SendMailAsync("sender@example.com", new[] { "good@example.com", "bad\rrecipient" }, "body"));

        // Give the server task a moment to detect MAIL FROM if it had been sent
        await Task.WhenAny(serverTask, Task.Delay(200));
        Assert.IsFalse(mailFromSent, "MAIL FROM must not be sent when a recipient fails validation.");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 59 — SMTP EHLO: last extension line included
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SmtpClient_EhloAsync_IncludesLastExtension()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("220 smtp.example.com ESMTP");
            _ = await sr.ReadLineAsync(); // EHLO command
            await sw.WriteLineAsync("250-smtp.example.com Hello");
            await sw.WriteLineAsync("250-SIZE 10240000");
            await sw.WriteLineAsync("250 STARTTLS");
            sw.BaseStream.Flush();
        });

        SmtpClient client = new SmtpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        IReadOnlyList<string> extensions = await client.EhloAsync("testclient.local");
        await serverTask;

        Assert.IsTrue(extensions.Contains("STARTTLS"),
            $"Last extension 'STARTTLS' must be included. Got: [{string.Join(", ", extensions)}]");
        Assert.IsTrue(extensions.Contains("SIZE 10240000"),
            "Middle extension 'SIZE 10240000' must also be included.");
    }

    // ──────────────────────────────────────────────────────────────
    // Item 65 — POP3 RETR: explicit CRLF line endings
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
        string message = await client.RetrieveAsync(1);
        await serverTask;

        Assert.AreEqual("Subject: hello\r\nbody text\r\n", message);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 64 — Exact terminator: ". " is data, not terminator (POP3 and NNTP)
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
            await sw.WriteLineAsync(". ");
            await sw.WriteLineAsync("next line");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        Pop3Client client = new Pop3Client();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        string message = await client.RetrieveAsync(1);
        await serverTask;

        StringAssert.Contains(message, ". \r\n");
        StringAssert.Contains(message, "next line\r\n");
    }

    [TestMethod]
    public async Task NntpClient_ArticleAsync_DotSpaceLine_IsDataNotTerminator()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("200 NNTP server ready");
            _ = await sr.ReadLineAsync(); // ARTICLE 1
            await sw.WriteLineAsync("220 1 <msg@example.com> article follows");
            await sw.WriteLineAsync(". ");
            await sw.WriteLineAsync("normal line");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        NntpClient client = new NntpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        string article = await client.ArticleAsync(1);
        await serverTask;

        StringAssert.Contains(article, ". \r\n");
        StringAssert.Contains(article, "normal line\r\n");
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

        Assert.AreEqual(".malformed\r\n", article);
    }

    [TestMethod]
    public async Task NntpClient_HeaderAsync_DoubleDotIsUnstuffed()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("200 NNTP server ready");
            _ = await sr.ReadLineAsync(); // HEADER 1
            await sw.WriteLineAsync("221 1 <msg@example.com> head follows");
            await sw.WriteLineAsync("..encoded-header");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        NntpClient client = new NntpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        string header = await client.HeaderAsync(1);
        await serverTask;

        Assert.AreEqual(".encoded-header\r\n", header);
    }

    [TestMethod]
    public async Task NntpClient_BodyAsync_DoubleDotIsUnstuffed()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("200 NNTP server ready");
            _ = await sr.ReadLineAsync(); // BODY 1
            await sw.WriteLineAsync("222 1 <msg@example.com> body follows");
            await sw.WriteLineAsync("..encoded-body-line");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        NntpClient client = new NntpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        string body = await client.BodyAsync(1);
        await serverTask;

        Assert.AreEqual(".encoded-body-line\r\n", body);
    }

    // ──────────────────────────────────────────────────────────────
    // Round-7 — SendMultilineCommandAsync: immediate body delivery
    //
    // These tests verify that body lines arriving in the same burst
    // as the status line are never misrouted as unsolicited responses.
    // The server writes status + body + terminator in one WriteAsync
    // call so the listener sees all content before the client can
    // register a separate ReadAsync waiter.
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Pop3Client_ListAsync_ImmediateBodyDelivery_ReturnsAllEntries()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("+OK POP3 ready");
            _ = await sr.ReadLineAsync(); // LIST
            // Send status + body + terminator atomically.
            await sw.BaseStream.WriteAsync(
                System.Text.Encoding.ASCII.GetBytes("+OK 2 messages\r\n1 1024\r\n2 4096\r\n.\r\n"));
        });

        Pop3Client client = new Pop3Client();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        IReadOnlyDictionary<int, int> result = await client.ListAsync();
        await serverTask;

        Assert.AreEqual(2, result.Count, "Both entries must be returned.");
        Assert.AreEqual(1024, result[1]);
        Assert.AreEqual(4096, result[2]);
    }

    [TestMethod]
    public async Task Pop3Client_RetrieveAsync_ImmediateBodyDelivery_DotUnstuffingApplied()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("+OK POP3 ready");
            _ = await sr.ReadLineAsync(); // RETR 1
            // Dot-stuffed line "..note" must become ".note" in the result.
            await sw.BaseStream.WriteAsync(
                System.Text.Encoding.ASCII.GetBytes("+OK 42 octets\r\nFrom: alice\r\n..note\r\n.\r\n"));
        });

        Pop3Client client = new Pop3Client();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        string message = await client.RetrieveAsync(1);
        await serverTask;

        Assert.AreEqual("From: alice\r\n.note\r\n", message);
    }

    [TestMethod]
    public async Task NntpClient_ArticleAsync_ImmediateBodyDelivery_DotUnstuffingApplied()
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("200 NNTP server ready");
            _ = await sr.ReadLineAsync(); // ARTICLE 1
            // Dot-stuffed line "..sig" must become ".sig" in the result.
            await sw.BaseStream.WriteAsync(
                System.Text.Encoding.ASCII.GetBytes("220 1 <id@host> article\r\nSubject: test\r\n..sig\r\n.\r\n"));
        });

        NntpClient client = new NntpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        string article = await client.ArticleAsync(1);
        await serverTask;

        Assert.AreEqual("Subject: test\r\n.sig\r\n", article);
    }

    // ──────────────────────────────────────────────────────────────
    // Item 67 — NNTP UTC: all three DateTimeKind values send "GMT"
    //
    // Policy:
    //   Utc         → value transmitted as-is with GMT token
    //   Local       → converted to UTC then transmitted with GMT token
    //   Unspecified → interpreted as UTC (no local conversion) with GMT token
    // ──────────────────────────────────────────────────────────────

    private static async Task<string?> CaptureNewGroupsCommandAsync(DateTime sinceUtc)
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();
        string? capturedCommand = null;

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("200 NNTP server ready");
            capturedCommand = await sr.ReadLineAsync();
            await sw.WriteLineAsync("231 new newsgroups follow");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        NntpClient client = new NntpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        await client.NewGroupsAsync(sinceUtc);
        await serverTask;
        return capturedCommand;
    }

    private static async Task<string?> CaptureNewNewsCommandAsync(DateTime sinceUtc)
    {
        (DuplexStream clientStream, StreamWriter sw, StreamReader sr) = CreateTestPair();
        string? capturedCommand = null;

        Task serverTask = Task.Run(async () =>
        {
            await sw.WriteLineAsync("200 NNTP server ready");
            capturedCommand = await sr.ReadLineAsync();
            await sw.WriteLineAsync("230 list of new articles follows");
            await sw.WriteLineAsync(".");
            sw.BaseStream.Flush();
        });

        NntpClient client = new NntpClient();
        await client.ConnectAsync(clientStream, leaveOpen: false);
        await client.NewNewsAsync("misc.test", sinceUtc);
        await serverTask;
        return capturedCommand;
    }

    [TestMethod]
    public async Task NntpClient_NewGroupsAsync_UtcKind_SendsGmt()
    {
        DateTime utcValue = new DateTime(2024, 3, 10, 14, 0, 0, DateTimeKind.Utc);
        string? cmd = await CaptureNewGroupsCommandAsync(utcValue);
        Assert.AreEqual("NEWGROUPS 20240310 140000 GMT", cmd);
    }

    [TestMethod]
    public async Task NntpClient_NewGroupsAsync_UnspecifiedKind_TreatedAsUtcWithGmt()
    {
        DateTime unspecified = new DateTime(2024, 1, 15, 8, 30, 0, DateTimeKind.Unspecified);
        string? cmd = await CaptureNewGroupsCommandAsync(unspecified);
        // Unspecified is treated as UTC: numeric values must be unchanged, GMT appended
        Assert.AreEqual("NEWGROUPS 20240115 083000 GMT", cmd,
            "DateTimeKind.Unspecified must be forwarded as-is (treated as UTC) with the GMT token.");
    }

    [TestMethod]
    public async Task NntpClient_NewGroupsAsync_LocalKind_ConvertedToUtcWithGmt()
    {
        // Build a Local value and derive the expected UTC string independently,
        // so the assertion is correct regardless of the CI machine's timezone.
        DateTime localValue = new DateTime(2024, 6, 21, 12, 0, 0, DateTimeKind.Local);
        DateTime expectedUtc = localValue.ToUniversalTime();
        string expectedCommand =
            $"NEWGROUPS {expectedUtc:yyyyMMdd} {expectedUtc:HHmmss} GMT";

        string? cmd = await CaptureNewGroupsCommandAsync(localValue);
        Assert.AreEqual(expectedCommand, cmd);
    }

    [TestMethod]
    public async Task NntpClient_NewNewsAsync_UtcKind_SendsGmt()
    {
        DateTime utcValue = new DateTime(2024, 3, 10, 14, 0, 0, DateTimeKind.Utc);
        string? cmd = await CaptureNewNewsCommandAsync(utcValue);
        Assert.AreEqual("NEWNEWS misc.test 20240310 140000 GMT", cmd);
    }

    [TestMethod]
    public async Task NntpClient_NewNewsAsync_UnspecifiedKind_TreatedAsUtcWithGmt()
    {
        DateTime unspecified = new DateTime(2024, 1, 15, 8, 30, 0, DateTimeKind.Unspecified);
        string? cmd = await CaptureNewNewsCommandAsync(unspecified);
        Assert.AreEqual("NEWNEWS misc.test 20240115 083000 GMT", cmd,
            "DateTimeKind.Unspecified must be forwarded as-is (treated as UTC) with the GMT token.");
    }

    [TestMethod]
    public async Task NntpClient_NewNewsAsync_LocalKind_ConvertedToUtcWithGmt()
    {
        DateTime localValue = new DateTime(2024, 6, 21, 12, 0, 0, DateTimeKind.Local);
        DateTime expectedUtc = localValue.ToUniversalTime();
        string expectedCommand =
            $"NEWNEWS misc.test {expectedUtc:yyyyMMdd} {expectedUtc:HHmmss} GMT";

        string? cmd = await CaptureNewNewsCommandAsync(localValue);
        Assert.AreEqual(expectedCommand, cmd);
    }
}
