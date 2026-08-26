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

namespace UtilsTest.Security.Net;

/// <summary>
/// Security-relevant client protocol tests covering audit items 40, 62, 64, 65: resource-bound
/// enforcement, session poisoning after unreliable framing/transport, cancellation leaving a
/// reliable failure state, and recipient validation before any network I/O (moved from
/// UtilsTest.Net.NetClientProtocolTests).
/// </summary>
[TestClass]
public class NetClientProtocolSecurityTests
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

    /// <summary>Wraps an outgoing stream and throws after partially accepting one selected write.</summary>
    private sealed class PartialFailingWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _failOnWriteCall;
        private readonly int _bytesBeforeFailure;
        private int _writeCalls;

        public PartialFailingWriteStream(Stream inner, int failOnWriteCall, int bytesBeforeFailure)
        {
            _inner = inner;
            _failOnWriteCall = failOnWriteCall;
            _bytesBeforeFailure = bytesBeforeFailure;
        }

        public override bool CanRead => false;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _writeCalls++;
            if (_writeCalls == _failOnWriteCall)
            {
                int accepted = Math.Min(_bytesBeforeFailure, count);
                if (accepted > 0)
                    _inner.Write(buffer, offset, accepted);
                throw new IOException("Simulated partial write failure.");
            }
            _inner.Write(buffer, offset, count);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _writeCalls++;
            if (_writeCalls == _failOnWriteCall)
            {
                int accepted = Math.Min(_bytesBeforeFailure, buffer.Length);
                if (accepted > 0)
                    await _inner.WriteAsync(buffer[..accepted], cancellationToken).ConfigureAwait(false);
                throw new IOException("Simulated partial write failure.");
            }
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
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
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.EhloAsync("example.test"));
        Assert.IsFalse(client.IsConnected);
        Assert.IsNotNull(client.SessionFailure);
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
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.ListAsync());
        Assert.IsFalse(client.IsConnected);
        await server;
    }

    /// <summary>Verifies a complete AUTH LOGIN rejection does not poison the synchronized SMTP session.</summary>
    [TestMethod]
    public async Task SmtpClient_AuthLoginRejected_DoesNotPoisonSession()
    {
        (DuplexStream stream, StreamWriter writer, StreamReader reader) = CreateTestPair();
        Task server = Task.Run(async () =>
        {
            await writer.WriteLineAsync("220 ready");
            Assert.AreEqual("AUTH LOGIN", await reader.ReadLineAsync());
            await writer.WriteLineAsync("535 authentication failed");
            Assert.AreEqual("HELP", await reader.ReadLineAsync());
            await writer.WriteLineAsync("214 help text");
        });
        SmtpClient client = new();
        await client.ConnectAsync(stream);
        ProtocolResponseException error = await Assert.ThrowsExactlyAsync<ProtocolResponseException>(() =>
            client.AuthenticateAsync("user", "password", SmtpAuthenticationMechanism.Login));
        Assert.AreEqual("535", error.ResponseCode);
        Assert.IsTrue(client.IsConnected);
        CollectionAssert.AreEqual(new[] { "help text" }, (await client.HelpAsync()).ToArray());
        await server;
    }

    /// <summary>Verifies cancellation while an SMTP RCPT response is pending poisons without sending RSET.</summary>
    [TestMethod]
    public async Task SmtpClient_SendMailAsync_CancelDuringRcptResponse_PoisonsWithoutRset()
    {
        (DuplexStream stream, StreamWriter writer, StreamReader reader) = CreateTestPair();
        List<string> commands = [];
        Task server = Task.Run(async () =>
        {
            await writer.WriteLineAsync("220 ready");
            commands.Add((await reader.ReadLineAsync())!);
            await writer.WriteLineAsync("250 sender accepted");
            commands.Add((await reader.ReadLineAsync())!);
            await Task.Delay(300);
            while (reader.Peek() >= 0)
                commands.Add((await reader.ReadLineAsync())!);
        });
        SmtpClient client = new();
        await client.ConnectAsync(stream);
        using CancellationTokenSource cancellation = new(100);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            client.SendMailAsync(SmtpPath.Parse("sender@example.com"), [SmtpPath.Parse("recipient@example.com")], new StringReader("body"), cancellationToken: cancellation.Token));
        Assert.IsFalse(client.IsConnected);
        Assert.IsFalse(commands.Contains("RSET"));
        await server;
    }

    /// <summary>Verifies an AUTH PLAIN transport failure after writing the command poisons the session.</summary>
    [TestMethod]
    public async Task SmtpClient_AuthPlainTransportFailure_PoisonsSession()
    {
        (DuplexStream stream, StreamWriter writer, StreamReader reader) = CreateTestPair();
        Task server = Task.Run(async () =>
        {
            await writer.WriteLineAsync("220 ready");
            _ = await reader.ReadLineAsync();
            writer.Dispose();
        });
        SmtpClient client = new();
        await client.ConnectAsync(stream);
        await Assert.ThrowsExactlyAsync<IOException>(() => client.AuthenticateAsync(new SmtpPlainCredentials("user", "password")));
        Assert.IsFalse(client.IsConnected);
        Assert.IsNotNull(client.SessionFailure);
        await server;
    }

    /// <summary>Verifies a partial SMTP transaction write poisons immediately and never attempts RSET recovery.</summary>
    [TestMethod]
    public async Task SmtpClient_SendMailAsync_PartialRcptWriteFailure_PoisonsWithoutRset()
    {
        Pipe serverToClient = new();
        Pipe clientToServer = new();
        PartialFailingWriteStream failingWrites = new(clientToServer.Writer.AsStream(), failOnWriteCall: 2, bytesBeforeFailure: 8);
        DuplexStream stream = new(serverToClient.Reader.AsStream(), failingWrites);
        StreamWriter writer = new(serverToClient.Writer.AsStream(), Encoding.ASCII)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };
        StreamReader reader = new(clientToServer.Reader.AsStream(), Encoding.ASCII);
        StringBuilder transcript = new();
        Task server = Task.Run(async () =>
        {
            await writer.WriteLineAsync("220 ready");
            string? mail = await reader.ReadLineAsync();
            transcript.AppendLine(mail);
            await writer.WriteLineAsync("250 sender accepted");
            await Task.Delay(200);
            while (clientToServer.Reader.TryRead(out ReadResult result))
            {
                foreach (ReadOnlyMemory<byte> segment in result.Buffer)
                    transcript.Append(Encoding.ASCII.GetString(segment.Span));
                clientToServer.Reader.AdvanceTo(result.Buffer.End);
            }
        });
        SmtpClient client = new();
        await client.ConnectAsync(stream);
        await Assert.ThrowsExactlyAsync<IOException>(() =>
            client.SendMailAsync(SmtpPath.Parse("sender@example.com"), [SmtpPath.Parse("recipient@example.com")], new StringReader("body")));
        Assert.IsFalse(client.IsConnected);
        Assert.IsNotNull(client.SessionFailure);
        await server;
        Assert.IsFalse(transcript.ToString().Contains("RSET", StringComparison.Ordinal));
    }

    /// <summary>Verifies NNTP article commands reject unexpected positive status before reading a payload.</summary>
    [TestMethod]
    public async Task NntpClient_ArticleUnexpectedPositiveCode_DoesNotEnterPayloadOrPoison()
    {
        (DuplexStream stream, StreamWriter writer, StreamReader reader) = CreateTestPair();
        Task server = Task.Run(async () =>
        {
            await writer.WriteLineAsync("200 ready");
            Assert.AreEqual("ARTICLE 1", await reader.ReadLineAsync());
            await writer.WriteLineAsync("200 posting allowed");
            Assert.AreEqual("GROUP comp.test", await reader.ReadLineAsync());
            await writer.WriteLineAsync("211 0 0 0");
        });
        NntpClient client = new();
        await client.ConnectAsync(stream);
        await Assert.ThrowsExactlyAsync<ProtocolResponseException>(() => client.ArticleAsync(1));
        Assert.IsTrue(client.IsConnected);
        Assert.AreEqual((0, 0, 0), await client.GroupAsync("comp.test"));
        await server;
    }

    /// <summary>Verifies that mutating a previously returned DNS servers array does not alter the live parameters.</summary>
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
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            client.SendMailAsync("sender@example.com", new[] { "good@example.com", "bad\rrecipient" }, "body"));

        // Give the server task a moment to detect MAIL FROM if it had been sent
        await Task.WhenAny(serverTask, Task.Delay(200));
        Assert.IsFalse(mailFromSent, "MAIL FROM must not be sent when a recipient fails validation.");
    }
}
