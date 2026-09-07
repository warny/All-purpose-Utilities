using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Net;

namespace UtilsTest.Net;

/// <summary>
/// Validates the real DNS socket transport over deterministic loopback connections.
/// </summary>
[TestClass]
public class SocketDnsTransportTests
{
    /// <summary>
    /// Verifies that UDP queries and responses are transferred without changing their bytes.
    /// </summary>
    [TestMethod]
    public async Task QueryUdpAsync_TransfersExactDatagramsOverLoopback()
    {
        byte[] query = [0x01, 0x23, 0x45, 0x67];
        byte[] response = [0x89, 0xAB, 0xCD];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEndpoint = (IPEndPoint)server.Client.LocalEndPoint!;
        byte[]? receivedQuery = null;

        Task serverTask = Task.Run(async () =>
        {
            UdpReceiveResult received = await server.ReceiveAsync(timeout.Token);
            receivedQuery = received.Buffer;
            await server.SendAsync(response, received.RemoteEndPoint, timeout.Token);
        }, timeout.Token);

        var transport = new SocketDnsTransport();
        Task<byte[]> clientTask = transport.QueryUdpAsync(serverEndpoint, query, timeout.Token);
        await Task.WhenAll(serverTask, clientTask);

        CollectionAssert.AreEqual(query, receivedQuery);
        CollectionAssert.AreEqual(response, await clientTask);
    }

    /// <summary>
    /// Verifies the two-byte big-endian framing and exact payload transfer used by DNS over TCP.
    /// </summary>
    [TestMethod]
    public async Task QueryTcpAsync_TransfersLengthPrefixedMessagesOverLoopback()
    {
        byte[] query = [0x10, 0x20, 0x30, 0x40, 0x50];
        byte[] response = [0x60, 0x70, 0x80];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;
        byte[]? receivedQuery = null;

        try
        {
            Task serverTask = Task.Run(async () =>
            {
                using TcpClient client = await listener.AcceptTcpClientAsync(timeout.Token);
                await using NetworkStream stream = client.GetStream();
                byte[] requestLength = new byte[2];
                await stream.ReadExactlyAsync(requestLength, timeout.Token);
                int queryLength = BinaryPrimitives.ReadUInt16BigEndian(requestLength);
                receivedQuery = new byte[queryLength];
                await stream.ReadExactlyAsync(receivedQuery, timeout.Token);

                byte[] responseLength = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(responseLength, checked((ushort)response.Length));
                await stream.WriteAsync(responseLength, timeout.Token);
                await stream.WriteAsync(response, timeout.Token);
            }, timeout.Token);

            var transport = new SocketDnsTransport();
            Task<byte[]> clientTask = transport.QueryTcpAsync(serverEndpoint, query, timeout.Token);
            await Task.WhenAll(serverTask, clientTask);

            CollectionAssert.AreEqual(query, receivedQuery);
            CollectionAssert.AreEqual(response, await clientTask);
        }
        finally
        {
            listener.Stop();
        }
    }
}
