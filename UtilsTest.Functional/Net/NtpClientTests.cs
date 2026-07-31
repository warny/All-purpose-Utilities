using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Functional.Net;

/// <summary>
/// Functional tests for <see cref="NtpClient"/> using real loopback UDP sockets.
/// These tests verify the production <see cref="UdpNtpTransport"/> transport layer without
/// hitting a real NTP server.
/// </summary>
[TestClass]
public class NtpClientFunctionalTests
{
    private static readonly DateTime Epoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Builds a valid NTP server reply for a given request.
    /// Copies the request's Transmit Timestamp (bytes 40-47) into the response's
    /// Originate Timestamp (bytes 24-31), sets stratum=1, version=4, mode=4 (server),
    /// and fills the server Transmit Timestamp with the given server time.
    /// </summary>
    private static byte[] BuildNtpReply(byte[] request, DateTime serverTime)
    {
        byte[] response = new byte[48];
        response[0] = (4 << 3) | 4; // LI=0, VN=4, Mode=4 (server)
        response[1] = 1;             // stratum 1

        // Echo Transmit Timestamp from request into Originate Timestamp of response.
        Array.Copy(request, 40, response, 24, 8);

        // Set server Transmit Timestamp.
        ulong seconds = (ulong)(serverTime - Epoch).TotalSeconds;
        for (int i = 0; i < 4; i++)
            response[40 + i] = (byte)(seconds >> (24 - 8 * i));
        // Non-zero fraction to satisfy non-zero transmit timestamp check.
        response[44] = response[44] == 0 ? (byte)1 : response[44];
        return response;
    }

    /// <summary>
    /// Starts a loopback UDP "server" on a random port. Returns the port and a task that
    /// receives one datagram, applies <paramref name="respondWith"/> to it, and optionally
    /// sends the result back.
    /// </summary>
    private static (int port, Task serverTask) StartLoopbackServer(
        Func<byte[], byte[]?> respondWith,
        CancellationToken ct = default)
    {
        var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;

        var serverTask = Task.Run(async () =>
        {
            try
            {
                UdpReceiveResult result = await server.ReceiveAsync(ct).ConfigureAwait(false);
                byte[]? reply = respondWith(result.Buffer);
                if (reply is not null)
                    await server.SendAsync(reply, reply.Length, result.RemoteEndPoint).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the test cancels.
            }
            finally
            {
                server.Close();
            }
        }, ct);

        return (port, serverTask);
    }

    /// <summary>
    /// A fake resolver that always resolves to the same loopback address.
    /// </summary>
    private sealed class LoopbackResolver : INtpResolver
    {
        private readonly IPAddress _address;
        public LoopbackResolver(IPAddress? address = null) => _address = address ?? IPAddress.Loopback;
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
            => Task.FromResult(new[] { _address });
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task NtpClient_RealLoopbackServer_ReturnsExpectedTime()
    {
        DateTime expectedTime = new(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        var (port, serverTask) = StartLoopbackServer(req => BuildNtpReply(req, expectedTime));

        var resolver = new LoopbackResolver();
        var transport = new UdpNtpTransport();

        DateTime result = await NtpClient.GetTimeAsync("loopback.test", port, resolver, transport, CancellationToken.None)
            .ConfigureAwait(false);

        await serverTask.ConfigureAwait(false);

        // The result should match the server time to within 1 second (NTP fraction rounding).
        TimeSpan diff = (result - expectedTime).Duration();
        Assert.IsTrue(diff < TimeSpan.FromSeconds(1),
            $"Expected time near {expectedTime:O} but got {result:O} (diff = {diff}).");
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task NtpClient_RealLoopbackServer_NoReply_TimesOut()
    {
        // Server receives but never replies.
        var (port, serverTask) = StartLoopbackServer(_ => null);

        var resolver = new LoopbackResolver();

        // Use a short timeout to avoid waiting the full 5 seconds in CI.
        var transport = new UdpNtpTransport(TimeSpan.FromMilliseconds(500));

        var ex = await Assert.ThrowsExceptionAsync<NtpQueryException>(
            () => NtpClient.GetTimeAsync("loopback.test", port, resolver, transport, CancellationToken.None))
            .ConfigureAwait(false);

        Assert.IsTrue(ex.Failures.Count > 0, "Expected at least one failure.");
        Assert.AreEqual(NtpPhase.Exchange, ex.Failures[0].Phase);

        // Cancel the server so it doesn't hang.
        // The server task will unblock when we close the server socket — it already got one packet.
        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task NtpClient_CallerCancellation_StopsReceive()
    {
        using var cts = new CancellationTokenSource();

        // Server that signals cancellation after receiving the request.
        var (port, serverTask) = StartLoopbackServer(req =>
        {
            // Cancel after receiving the request but before sending a reply.
            cts.Cancel();
            return null; // no reply
        });

        var resolver = new LoopbackResolver();
        var transport = new UdpNtpTransport();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => NtpClient.GetTimeAsync("loopback.test", port, resolver, transport, cts.Token))
            .ConfigureAwait(false);

        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task NtpClient_ValidReply_ReturnsParsedUtcTime()
    {
        // A specific, known server time to verify field parsing is correct end-to-end.
        DateTime serverTime = new(2023, 3, 14, 15, 9, 26, DateTimeKind.Utc); // Pi Day

        var (port, serverTask) = StartLoopbackServer(req => BuildNtpReply(req, serverTime));

        var resolver = new LoopbackResolver();
        var transport = new UdpNtpTransport();

        DateTime result = await NtpClient.GetTimeAsync("loopback.test", port, resolver, transport, CancellationToken.None)
            .ConfigureAwait(false);

        await serverTask.ConfigureAwait(false);

        Assert.AreEqual(DateTimeKind.Utc, result.Kind);
        TimeSpan diff = (result - serverTime).Duration();
        Assert.IsTrue(diff < TimeSpan.FromSeconds(1),
            $"Expected ~{serverTime:O} but got {result:O} (diff = {diff}).");
    }
}
