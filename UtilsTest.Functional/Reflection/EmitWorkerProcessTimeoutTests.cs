using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates the low-level primitive <see cref="Utils.Reflection.Reflection.Emit.EmitWorkerProcess"/>'s
/// request/response timeout relies on: <see cref="TextReader.ReadLineAsync(CancellationToken)"/> over a
/// <see cref="NamedPipeServerStream"/> actually observes cancellation instead of blocking forever.
/// Spawning a real hung worker process end-to-end is out of scope for automated tests here (same
/// accepted limitation as <see cref="EmitWorkerHostLoopTests"/>/<c>EmitWorkerProxyTests</c>'s Load/Call
/// round trip), so this exercises the exact primitive <c>EmitWorkerProcess.SendAndReceive</c> is built
/// on, using two ends of a real named pipe within this test process (no second OS process needed).
/// </summary>
[TestClass]
public class EmitWorkerProcessTimeoutTests
{
    [TestMethod]
    public async Task ReadLineAsync_WithCancellation_ThrowsWhenNoResponseArrives()
    {
        string pipeName = $"UtilsTest.EmitWorkerProcessTimeoutTests.{Guid.NewGuid():N}";

        using var server = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        Task serverConnect = server.WaitForConnectionAsync();
        await client.ConnectAsync(5_000);
        await serverConnect;

        using var reader = new StreamReader(server, leaveOpen: true);
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            async () => await reader.ReadLineAsync(timeoutSource.Token));
        stopwatch.Stop();

        // The client never wrote anything, so the only way this returns is via cancellation. Bound the
        // elapsed time generously (well under EmitWorkerProcess's real 30s default) to catch a
        // regression where the token stops being observed and the read silently falls back to blocking.
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"ReadLineAsync took {stopwatch.Elapsed} to observe cancellation; expected well under 5s.");
    }
}
