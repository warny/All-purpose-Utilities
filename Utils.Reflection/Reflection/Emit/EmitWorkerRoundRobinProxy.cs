using System;
using System.Reflection;
using System.Threading;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// <see cref="DispatchProxy"/> that round-robins each call across a fixed set of independent isolated
/// Emit worker processes, produced by <see cref="LibraryMapper.EmitRoundRobin{TInterface}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Complements the in-process concurrency <see cref="EmitWorkerProcess"/> already supports (several
/// calls in flight on the <em>same</em> worker — see <see cref="EmitWorkerProcess.InvokeMethod"/>):
/// that requires the native library backing the interface to itself be safe to call concurrently, since
/// every call ends up in the same worker process. Round-robining across <em>separate</em> worker
/// processes instead sidesteps that requirement entirely — each process has its own independent load of
/// the native DLL — at the cost of one full sandboxed process per member of the set (see
/// <see cref="LibraryMapper.Emit{TInterface}"/>'s per-process startup cost) rather than one process total.
/// </para>
/// <para>
/// Each worker is protected by a <see cref="SemaphoreSlim"/>(1, 1) so at most one invocation runs on
/// any single worker process at a time. If more concurrent callers arrive than there are workers, the
/// excess callers wait (blocking) until a worker becomes available. This guarantees that each
/// underlying native DLL instance never sees a concurrent call, fulfilling the non-concurrent-access
/// contract that motivated choosing round-robin over the single-worker <see cref="LibraryMapper.Emit{TInterface}"/>.
/// </para>
/// </remarks>
public class EmitWorkerRoundRobinProxy : DispatchProxy
{
    private readonly record struct Member(EmitWorkerProcess Worker, int Handle, SemaphoreSlim Semaphore);

    private Member[] members = [];
    private int nextIndex = -1;

    /// <summary>
    /// Associates this proxy with the fixed set of workers it round-robins calls across. Called once,
    /// immediately after <see cref="DispatchProxy.Create{TInterface, TProxy}"/>, before the proxy is
    /// returned to the caller.
    /// </summary>
    /// <param name="workers">
    /// The workers to round-robin across, each already loaded with the same interface (its own handle
    /// on its own process). This proxy exclusively owns every one of them: disposing the proxy disposes
    /// each worker in turn, shutting down its process.
    /// </param>
    internal void AttachWorkers((EmitWorkerProcess Worker, int Handle)[] workers)
    {
        members = Array.ConvertAll(workers, w => new Member(w.Worker, w.Handle, new SemaphoreSlim(1, 1)));
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];

        if (IsDisposeMethod(targetMethod))
        {
            Member[] owned = members;
            members = [];

            foreach (Member m in owned)
            {
                m.Worker.Dispose();
                m.Semaphore.Dispose();
            }

            return null;
        }

        Member[] current = members;
        if (current.Length == 0)
        {
            throw new ObjectDisposedException(targetMethod.DeclaringType?.FullName ?? nameof(EmitWorkerRoundRobinProxy));
        }

        // Interlocked.Increment can overflow int back to negative after ~2^31 calls; casting to uint
        // before the modulo keeps the index in range regardless, without needing to reset the counter.
        int index = (int)((uint)Interlocked.Increment(ref nextIndex) % current.Length);
        Member member = current[index];

        // Acquire the per-worker semaphore before calling so that no two concurrent callers can
        // execute on the same worker process simultaneously, even when more callers are active than
        // there are workers. The semaphore is released in the finally block regardless of outcome.
        member.Semaphore.Wait();
        try
        {
            return member.Worker.InvokeMethod(member.Handle, targetMethod, args);
        }
        finally
        {
            member.Semaphore.Release();
        }
    }

    private static bool IsDisposeMethod(MethodInfo method) =>
        method.Name == nameof(IDisposable.Dispose) &&
        method.GetParameters().Length == 0 &&
        typeof(IDisposable).IsAssignableFrom(method.DeclaringType);
}
