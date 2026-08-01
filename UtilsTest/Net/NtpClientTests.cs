using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;
using SysArray = System.Array;

namespace UtilsTest.Net;

/// <summary>
/// Unit tests for <see cref="NtpClient"/>. These exercise the internal resolver/transport overload
/// so that no real NTP server is contacted.
/// </summary>
[TestClass]
public class NtpClientTests
{
    private static readonly DateTime Epoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class FakeResolver : INtpResolver
    {
        private readonly IPAddress[] _addresses;
        public FakeResolver(params IPAddress[] addresses) => _addresses = addresses;
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
            => Task.FromResult(_addresses);
    }

    private sealed class FakeTransport : INtpTransport
    {
        private readonly Func<IPEndPoint, byte[], byte[]> _respond;
        public List<IPEndPoint> Calls { get; } = new();
        public FakeTransport(Func<IPEndPoint, byte[], byte[]> respond) => _respond = respond;
        public Task<byte[]> ExchangeAsync(IPEndPoint endpoint, byte[] request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(endpoint);
            return Task.FromResult(_respond(endpoint, request));
        }
    }

    /// <summary>
    /// Builds a valid server response echoing the request's transmit timestamp into the originate
    /// field, and encoding <paramref name="serverTime"/> as the server transmit timestamp.
    /// </summary>
    private static byte[] ServerResponse(byte[] request, DateTime serverTime, byte mode = 4, byte version = 4, byte stratum = 1, byte leap = 0x00, bool echoOriginate = true)
    {
        byte[] response = new byte[48];
        response[0] = (byte)((leap & 0xC0) | ((version & 0x07) << 3) | (mode & 0x07));
        response[1] = stratum;

        // Echo the request transmit timestamp (offset 40..47) into originate (offset 24..31).
        if (echoOriginate)
            SysArray.Copy(request, 40, response, 24, 8);

        ulong seconds = (ulong)(serverTime - Epoch).TotalSeconds;
        for (int i = 0; i < 4; i++)
            response[40 + i] = (byte)(seconds >> (24 - 8 * i));
        // Ensure transmit timestamp is non-zero even when seconds fraction is 0.
        response[43] = (byte)seconds;
        return response;
    }

    private static Task<DateTime> Query(INtpResolver resolver, INtpTransport transport, string host = "pool.ntp", int port = 123, CancellationToken ct = default)
        => NtpClient.GetTimeAsync(host, port, resolver, transport, ct);

    [TestMethod]
    public async Task GetTimeAsync_NullHost_Throws()
    {
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => Query(new FakeResolver(IPAddress.Loopback), new FakeTransport((e, r) => new byte[48]), host: null!));
    }

    [TestMethod]
    public async Task GetTimeAsync_EmptyHost_Throws()
    {
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => Query(new FakeResolver(IPAddress.Loopback), new FakeTransport((e, r) => new byte[48]), host: "   "));
    }

    [TestMethod]
    public async Task GetTimeAsync_InvalidPort_Throws()
    {
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
            () => Query(new FakeResolver(IPAddress.Loopback), new FakeTransport((e, r) => new byte[48]), port: 0));
    }

    [TestMethod]
    public async Task GetTimeAsync_NoResolvedAddress_ThrowsClearException()
    {
        await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(), new FakeTransport((e, r) => new byte[48])));
    }

    [TestMethod]
    public async Task GetTimeAsync_FirstEndpointFails_SecondSucceeds()
    {
        DateTime expected = new(2021, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var first = IPAddress.Parse("192.0.2.1");
        var second = IPAddress.Parse("192.0.2.2");
        var transport = new FakeTransport((ep, req) =>
        {
            if (ep.Address.Equals(first))
                throw new SocketException((int)SocketError.TimedOut);
            return ServerResponse(req, expected);
        });

        DateTime result = await Query(new FakeResolver(first, second), transport);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(2, transport.Calls.Count);
    }

    [TestMethod]
    public async Task GetTimeAsync_AllEndpointsFail_AggregatesFailures()
    {
        var transport = new FakeTransport((ep, req) => throw new SocketException((int)SocketError.ConnectionRefused));
        var ex = await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.2")), transport));
        Assert.AreEqual(2, ex.Failures.Count);
    }

    [TestMethod]
    public async Task GetTimeAsync_CancellationStopsFallbackImmediately()
    {
        using var cts = new CancellationTokenSource();
        var transport = new FakeTransport((ep, req) =>
        {
            cts.Cancel();
            throw new SocketException((int)SocketError.TimedOut);
        });
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => Query(new FakeResolver(IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.2")), transport, ct: cts.Token));
    }

    [TestMethod]
    public async Task GetTimeAsync_BroadcastMode5_ThrowsInvalidDataException()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, mode: 5));
        var ex = await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
        Assert.IsInstanceOfType(ex.Failures[0].Exception, typeof(InvalidDataException));
    }

    [TestMethod]
    public async Task GetTimeAsync_ClientMode3Response_Throws()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, mode: 3));
        await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_ServerMode4_IsAccepted()
    {
        DateTime expected = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var transport = new FakeTransport((ep, req) => ServerResponse(req, expected, mode: 4));
        DateTime result = await Query(new FakeResolver(IPAddress.Loopback), transport);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public async Task GetTimeAsync_OriginateTimestampMismatch_Throws()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, echoOriginate: false));
        var ex = await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
        Assert.IsInstanceOfType(ex.Failures[0].Exception, typeof(InvalidDataException));
    }

    [TestMethod]
    public async Task GetTimeAsync_ZeroTransmitTimestamp_Throws()
    {
        var transport = new FakeTransport((ep, req) =>
        {
            byte[] response = ServerResponse(req, DateTime.UtcNow);
            SysArray.Clear(response, 40, 8); // zero the transmit timestamp
            return response;
        });
        await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_LeapAlarm_Throws()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, leap: 0xC0));
        await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_StratumZero_Throws()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, stratum: 0));
        await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_TooShortPacket_Throws()
    {
        var transport = new FakeTransport((ep, req) => new byte[10]);
        await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_UnexpectedEndpoint_Throws()
    {
        // The real UDP transport rejects unexpected endpoints; here we simulate the resulting
        // IOException surfacing as an aggregated failure.
        var transport = new FakeTransport((ep, req) => throw new IOException("NTP response received from unexpected endpoint."));
        var ex = await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
        Assert.AreEqual(NtpPhase.Exchange, ex.Failures[0].Phase);
    }

    [TestMethod]
    public async Task GetTimeAsync_ValidResponse_ReturnsUtcDateTime()
    {
        DateTime expected = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var transport = new FakeTransport((ep, req) => ServerResponse(req, expected));
        DateTime result = await Query(new FakeResolver(IPAddress.Loopback), transport);
        Assert.AreEqual(DateTimeKind.Utc, result.Kind);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void UdpNtpTransport_ZeroTimeout_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new UdpNtpTransport(TimeSpan.Zero));
    }

    [TestMethod]
    public void UdpNtpTransport_NegativeTimeout_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new UdpNtpTransport(TimeSpan.FromSeconds(-1)));
    }

    [TestMethod]
    public void UdpNtpTransport_InfiniteTimeout_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new UdpNtpTransport(System.Threading.Timeout.InfiniteTimeSpan));
    }

    /// <summary>
    /// Verifies that <see cref="UdpNtpTransport.ExchangeAsync"/> throws
    /// <see cref="ArgumentNullException"/> when <paramref name="endpoint"/> is <see langword="null"/>.
    /// </summary>
    [TestMethod]
    public async Task UdpNtpTransport_NullEndpoint_ThrowsArgumentNullException()
    {
        var transport = new UdpNtpTransport(TimeSpan.FromSeconds(1));

        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => transport.ExchangeAsync(null!, new byte[48], CancellationToken.None))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that <see cref="UdpNtpTransport.ExchangeAsync"/> throws
    /// <see cref="ArgumentNullException"/> when the request buffer is <see langword="null"/>.
    /// </summary>
    [TestMethod]
    public async Task UdpNtpTransport_NullRequest_ThrowsArgumentNullException()
    {
        var transport = new UdpNtpTransport(TimeSpan.FromSeconds(1));
        var endpoint = new IPEndPoint(IPAddress.Loopback, 123);

        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            () => transport.ExchangeAsync(endpoint, null!, CancellationToken.None))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that <see cref="UdpNtpTransport.ExchangeAsync"/> throws
    /// <see cref="ArgumentException"/> when the request buffer is empty.
    /// </summary>
    [TestMethod]
    public async Task UdpNtpTransport_EmptyRequest_ThrowsArgumentException()
    {
        var transport = new UdpNtpTransport(TimeSpan.FromSeconds(1));
        var endpoint = new IPEndPoint(IPAddress.Loopback, 123);

        await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => transport.ExchangeAsync(endpoint, new byte[0], CancellationToken.None))
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task GetTimeAsync_FractionConversion_IsAccurateWithinOneTickOrDocumentedTolerance()
    {
        DateTime baseTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Encode a half-second fraction (0x80000000) in the transmit timestamp.
        var transport = new FakeTransport((ep, req) =>
        {
            byte[] response = ServerResponse(req, baseTime);
            response[44] = 0x80; // fraction high byte = 0.5s
            response[45] = 0x00;
            response[46] = 0x00;
            response[47] = 0x00;
            return response;
        });

        DateTime result = await Query(new FakeResolver(IPAddress.Loopback), transport);
        TimeSpan diff = result - baseTime.AddMilliseconds(500);
        Assert.IsTrue(Math.Abs(diff.Ticks) <= 1, $"Fraction conversion off by {diff.Ticks} ticks.");
    }
}
