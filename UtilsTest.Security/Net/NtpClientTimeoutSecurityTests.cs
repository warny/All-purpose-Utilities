using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Security.Net;

/// <summary>
/// Verifies that <see cref="NtpClient"/> and <see cref="UdpNtpTransport"/> fail closed — with a
/// bounded timeout rather than hanging forever — when a server never replies or the caller cancels,
/// using real loopback UDP sockets.
/// </summary>
[TestClass]
public class NtpClientTimeoutSecurityTests
{
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
    public async Task NtpClient_RealLoopbackServer_NoReply_TimesOut()
    {
        // Server receives but never replies.
        var (port, serverTask) = StartLoopbackServer(_ => null);

        var resolver = new LoopbackResolver();

        // Use a short timeout to avoid waiting the full 5 seconds in CI.
        var transport = new UdpNtpTransport(TimeSpan.FromMilliseconds(500));

        var ex = await Assert.ThrowsExactlyAsync<NtpQueryException>(
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

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => NtpClient.GetTimeAsync("loopback.test", port, resolver, transport, cts.Token))
            .ConfigureAwait(false);

        await serverTask.ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task UdpNtpTransport_ExchangeAsync_NoReply_ThrowsSocketExceptionTimedOut()
    {
        // Start a server that receives but never replies.
        var (port, serverTask) = StartLoopbackServer(_ => null);

        var endpoint = new IPEndPoint(IPAddress.Loopback, port);
        byte[] request = new byte[48];
        request[0] = (4 << 3) | 3; // VN=4, Mode=3 (client)

        // Use the production UdpNtpTransport with a short timeout so the test doesn't wait 5 s.
        var transport = new UdpNtpTransport(TimeSpan.FromMilliseconds(300));

        var ex = await Assert.ThrowsExactlyAsync<SocketException>(
            () => transport.ExchangeAsync(endpoint, request, CancellationToken.None))
            .ConfigureAwait(false);

        Assert.AreEqual(SocketError.TimedOut, ex.SocketErrorCode,
            $"Expected TimedOut but got {ex.SocketErrorCode}.");

        await serverTask.ConfigureAwait(false);
    }
}
