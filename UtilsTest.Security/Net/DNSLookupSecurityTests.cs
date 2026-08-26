using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Utils.Net;
using Utils.Net.DNS;

namespace UtilsTest.Security.Net;

/// <summary>
/// Verifies that <see cref="DNSLookup"/> binds responses to the originating query (rejecting
/// mismatched transaction IDs and malformed replies — DNS anti-spoofing/correlation invariants),
/// and that its configured name-server list cannot be mutated through a published alias.
/// </summary>
[TestClass]
public class DNSLookupSecurityTests
{
    private static readonly IPAddress ServerA = IPAddress.Parse("192.0.2.1");
    private static readonly IPAddress ServerB = IPAddress.Parse("192.0.2.2");

    // ---- Defensive copy of the configured name-server list ----

    [TestMethod]
    public void Constructor_CallerMutatesInputArray_DoesNotChangeConfiguration()
    {
        var input = new[] { ServerA, ServerB };
        var lookup = new DNSLookup(input);
        input[0] = IPAddress.Parse("203.0.113.9");

        CollectionAssert.AreEqual(new[] { ServerA, ServerB }, lookup.NameServers);
    }

    [TestMethod]
    public void NameServers_GetterReturnsDefensiveCopy()
    {
        var lookup = new DNSLookup(ServerA, ServerB);
        IPAddress[] first = lookup.NameServers;
        first[0] = IPAddress.Parse("203.0.113.9");

        CollectionAssert.AreEqual(new[] { ServerA, ServerB }, lookup.NameServers);
    }

    [TestMethod]
    public void NameServers_SetterCopiesInput()
    {
        var lookup = new DNSLookup(ServerA);
        var input = new[] { ServerB };
        lookup.NameServers = input;
        input[0] = IPAddress.Parse("203.0.113.9");

        CollectionAssert.AreEqual(new[] { ServerB }, lookup.NameServers);
    }

    // ---- Response binding and malformed-response rejection ----

    private static byte[] BuildResponse(byte[] queryBytes, Action<DNSHeader>? mutate = null)
    {
        DNSHeader query = DNSPacketReader.Default.Read(queryBytes);
        var response = new DNSHeader { ID = query.ID };
        response.QrBit = DNSQRBit.Response;
        response.OpCode = query.OpCode;
        foreach (var q in query.Requests)
            response.Requests.Add((DNSRequestRecord)q.Clone());
        mutate?.Invoke(response);
        return DNSPacketWriter.Default.Write(response);
    }

    private sealed class FakeTransport : IDnsTransport
    {
        private readonly Func<IPEndPoint, byte[], (byte[]? udp, Exception? error)> _udp;
        private readonly Func<IPEndPoint, byte[], byte[]>? _tcp;
        public List<IPEndPoint> UdpCalls { get; } = new();

        public FakeTransport(
            Func<IPEndPoint, byte[], (byte[]? udp, Exception? error)> udp,
            Func<IPEndPoint, byte[], byte[]>? tcp = null)
        {
            _udp = udp;
            _tcp = tcp;
        }

        public Task<byte[]> QueryUdpAsync(IPEndPoint server, byte[] query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UdpCalls.Add(server);
            var (udp, error) = _udp(server, query);
            if (error is not null)
                return Task.FromException<byte[]>(error);
            return Task.FromResult(udp!);
        }

        public Task<byte[]> QueryTcpAsync(IPEndPoint server, byte[] query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_tcp is null)
                return Task.FromException<byte[]>(new IOException("no tcp"));
            return Task.FromResult(_tcp(server, query));
        }
    }

    [TestMethod]
    public async Task RequestAsync_MalformedResponse_IsAggregated()
    {
        var transport = new FakeTransport((ep, q) => (new byte[] { 1, 2, 3 }, null));
        var lookup = new DNSLookup(transport, ServerA);

        var ex = await Assert.ThrowsExactlyAsync<DnsLookupException>(() => lookup.RequestAsync("A", "example.com"));
        Assert.AreEqual(1, ex.Failures.Count);
        Assert.AreEqual(DnsFailureKind.MalformedResponse, ex.Failures[0].Kind);
    }

    [TestMethod]
    public async Task RequestAsync_WrongTransactionId_IsAggregated()
    {
        var transport = new FakeTransport((ep, q) => (BuildResponse(q, h => h.ID = (ushort)(h.ID ^ 0x1)), null));
        var lookup = new DNSLookup(transport, ServerA);

        var ex = await Assert.ThrowsExactlyAsync<DnsLookupException>(() => lookup.RequestAsync("A", "example.com"));
        Assert.AreEqual(DnsFailureKind.TransactionIdMismatch, ex.Failures[0].Kind);
    }
}
