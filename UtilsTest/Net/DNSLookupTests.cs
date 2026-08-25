using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Utils.Net;
using Utils.Net.DNS;

namespace UtilsTest.Net;

[TestClass]
public class DNSLookupTests
{
    private static readonly IPAddress ServerA = IPAddress.Parse("192.0.2.1");
    private static readonly IPAddress ServerB = IPAddress.Parse("192.0.2.2");

    // ---- NameServers validation (item 38) ----

    [TestMethod]
    public void Constructor_NullNameServers_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DNSLookup((IPAddress[])null!));
    }

    [TestMethod]
    public void Constructor_EmptyNameServers_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DNSLookup(System.Array.Empty<IPAddress>()));
    }

    [TestMethod]
    public void Constructor_NullEntry_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DNSLookup(new IPAddress[] { null! }));
    }

    [TestMethod]
    public void Constructor_AnyAddress_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DNSLookup(IPAddress.Any));
    }

    [TestMethod]
    public void Constructor_IPv6Any_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DNSLookup(IPAddress.IPv6Any));
    }

    [TestMethod]
    public void Constructor_MulticastAddress_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DNSLookup(IPAddress.Parse("224.0.0.251")));
    }

    [TestMethod]
    public void Constructor_DuplicateAddresses_DeduplicatesPreservingOrder()
    {
        var lookup = new DNSLookup(ServerA, ServerB, ServerA);
        CollectionAssert.AreEqual(new[] { ServerA, ServerB }, lookup.NameServers);
    }

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

    [TestMethod]
    public void LoopbackAddress_IsAccepted()
    {
        var lookup = new DNSLookup(IPAddress.Loopback);
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, lookup.NameServers);
    }

    // ---- Error aggregation and fallback (item 37) ----

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
    public async Task RequestAsync_FirstServerTimeout_SecondSucceeds()
    {
        var transport = new FakeTransport((ep, q) =>
            ep.Address.Equals(ServerA)
                ? (null, new SocketException((int)SocketError.TimedOut))
                : (BuildResponse(q), null));

        var lookup = new DNSLookup(transport, ServerA, ServerB);
        DNSHeader response = await lookup.RequestAsync("A", "example.com");

        Assert.AreEqual(DNSQRBit.Response, response.QrBit);
        Assert.AreEqual(2, transport.UdpCalls.Count);
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

    [TestMethod]
    public async Task RequestAsync_TruncatedThenTcpValid_Succeeds()
    {
        var transport = new FakeTransport(
            udp: (ep, q) => (BuildResponse(q, h => h.MessageTruncated = true), null),
            tcp: (ep, q) => BuildResponse(q));

        var lookup = new DNSLookup(transport, ServerA);
        DNSHeader response = await lookup.RequestAsync("A", "example.com");
        Assert.AreEqual(DNSQRBit.Response, response.QrBit);
        Assert.IsFalse(response.MessageTruncated);
    }

    [TestMethod]
    public async Task RequestAsync_AllFail_AggregatesDiagnosticsInOrder()
    {
        var transport = new FakeTransport((ep, q) =>
            ep.Address.Equals(ServerA)
                ? (null, new SocketException((int)SocketError.TimedOut))
                : (null, new SocketException((int)SocketError.ConnectionRefused)));

        var lookup = new DNSLookup(transport, ServerA, ServerB);
        var ex = await Assert.ThrowsExactlyAsync<DnsLookupException>(() => lookup.RequestAsync("A", "example.com"));

        Assert.AreEqual(2, ex.Failures.Count);
        Assert.AreEqual(ServerA, ex.Failures[0].Endpoint.Address);
        Assert.AreEqual(DnsFailureKind.Timeout, ex.Failures[0].Kind);
        Assert.AreEqual(ServerB, ex.Failures[1].Endpoint.Address);
        Assert.AreEqual(DnsFailureKind.SocketError, ex.Failures[1].Kind);
    }

    [TestMethod]
    public async Task RequestAsync_Cancellation_IsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var transport = new FakeTransport((ep, q) => (BuildResponse(q), null));
        var lookup = new DNSLookup(transport, ServerA);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => lookup.RequestAsync("A", "example.com", DNSClassId.ALL, cts.Token));
    }

    // ---- Argument validation on RequestAsync (item 56) ----

    [TestMethod]
    public async Task RequestAsync_NullType_Throws()
    {
        var lookup = new DNSLookup(new FakeTransport((ep, q) => (BuildResponse(q), null)), ServerA);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => lookup.RequestAsync(null!, "example.com"));
    }

    [TestMethod]
    public async Task RequestAsync_EmptyType_Throws()
    {
        var lookup = new DNSLookup(new FakeTransport((ep, q) => (BuildResponse(q), null)), ServerA);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => lookup.RequestAsync("", "example.com"));
    }

    [TestMethod]
    public async Task RequestAsync_WhitespaceType_Throws()
    {
        var lookup = new DNSLookup(new FakeTransport((ep, q) => (BuildResponse(q), null)), ServerA);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => lookup.RequestAsync("   ", "example.com"));
    }

    [TestMethod]
    public async Task RequestAsync_NullName_Throws()
    {
        var lookup = new DNSLookup(new FakeTransport((ep, q) => (BuildResponse(q), null)), ServerA);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => lookup.RequestAsync("A", null!));
    }

    [TestMethod]
    public async Task RequestAsync_EmptyName_Throws()
    {
        var lookup = new DNSLookup(new FakeTransport((ep, q) => (BuildResponse(q), null)), ServerA);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => lookup.RequestAsync("A", ""));
    }

    [TestMethod]
    public async Task RequestAsync_WhitespaceName_Throws()
    {
        var lookup = new DNSLookup(new FakeTransport((ep, q) => (BuildResponse(q), null)), ServerA);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => lookup.RequestAsync("A", "   "));
    }
}
