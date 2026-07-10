using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates <see cref="EmitWorkerPool"/>'s disposal semantics without starting a real isolated worker
/// process (spawning one is out of scope for automated tests here, same accepted limitation as
/// <c>EmitWorkerProxyTests</c>/<c>EmitWorkerProtocolTests</c>).
/// </summary>
[TestClass]
public class EmitWorkerPoolTests
{
    public interface ITinyInterface : IDisposable
    {
        int Foo(int value);
    }

    [TestMethod]
    public void Emit_AfterDispose_ThrowsObjectDisposedException()
    {
        var pool = new EmitWorkerPool();
        pool.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(
            () => pool.Emit<ITinyInterface>("does-not-matter.dll", CallingConvention.Winapi));
    }

    [TestMethod]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var pool = new EmitWorkerPool();

        pool.Dispose();
        pool.Dispose();
    }

    [TestMethod]
    public void Dispose_WithoutEverCallingEmit_DoesNotThrow()
    {
        // No worker was ever started (lazy on first Emit<TInterface> call): disposing must be a no-op,
        // not attempt to shut down a worker that was never created.
        using var pool = new EmitWorkerPool();
    }
}
