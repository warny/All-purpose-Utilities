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

namespace UtilsTest.Security.Net;

/// <summary>
/// Verifies that <see cref="NtpClient"/> rejects spoofed, malformed, or unbound NTP responses
/// (mode, leap indicator, stratum, originate-timestamp binding, transmit timestamp, endpoint,
/// packet length), matching the anti-spoofing invariants documented for NTP response validation.
/// These exercise the internal resolver/transport overload so that no real NTP server is contacted.
/// </summary>
[TestClass]
public class NtpClientSpoofingTests
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
    public async Task GetTimeAsync_BroadcastMode5_ThrowsInvalidDataException()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, mode: 5));
        var ex = await Assert.ThrowsExactlyAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
        Assert.IsInstanceOfType(ex.Failures[0].Exception, typeof(InvalidDataException));
    }

    [TestMethod]
    public async Task GetTimeAsync_ClientMode3Response_Throws()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, mode: 3));
        await Assert.ThrowsExactlyAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_OriginateTimestampMismatch_Throws()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, echoOriginate: false));
        var ex = await Assert.ThrowsExactlyAsync<NtpQueryException>(
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
        await Assert.ThrowsExactlyAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_LeapAlarm_Throws()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, leap: 0xC0));
        await Assert.ThrowsExactlyAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_StratumZero_Throws()
    {
        var transport = new FakeTransport((ep, req) => ServerResponse(req, DateTime.UtcNow, stratum: 0));
        await Assert.ThrowsExactlyAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_TooShortPacket_Throws()
    {
        var transport = new FakeTransport((ep, req) => new byte[10]);
        await Assert.ThrowsExactlyAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
    }

    [TestMethod]
    public async Task GetTimeAsync_UnexpectedEndpoint_Throws()
    {
        // The real UDP transport rejects unexpected endpoints; here we simulate the resulting
        // IOException surfacing as an aggregated failure.
        var transport = new FakeTransport((ep, req) => throw new IOException("NTP response received from unexpected endpoint."));
        var ex = await Assert.ThrowsExactlyAsync<NtpQueryException>(
            () => Query(new FakeResolver(IPAddress.Loopback), transport));
        Assert.AreEqual(NtpPhase.Exchange, ex.Failures[0].Phase);
    }
}
