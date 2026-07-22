using System;
using System.Reflection;
using System.Threading;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// <see cref="DispatchProxy"/> that forwards every interface call to an isolated
/// <see cref="EmitWorkerProcess"/>. Instances are produced by <see cref="LibraryMapper.Emit{I}"/> via
/// <see cref="DispatchProxy.Create{TInterface, TProxy}"/>; the generated proxy type never contains
/// caller-controlled source, so it does not carry the code-generation risk documented on
/// <see cref="EmitDllMappableClass"/>.
/// </summary>
public class EmitWorkerProxy : DispatchProxy
{
    private EmitWorkerProcess? worker;
    private int handle;
    private bool ownsWorker;

    /// <summary>
    /// Number of method invocations currently executing through this proxy. Incremented before
    /// checking the dispose state; decremented in a finally block. The dispose path waits for this
    /// counter to reach zero before releasing worker resources, replacing the previous
    /// <see cref="System.Threading.ReaderWriterLockSlim"/> which was never disposed.
    /// </summary>
    private int activeCallCount;

    /// <summary>
    /// Set to 1 by the first <see cref="IDisposable.Dispose"/> call (via
    /// <see cref="Interlocked.Exchange(ref int, int)"/>). Non-zero means the proxy is disposed or
    /// in the process of being disposed.
    /// </summary>
    private volatile int disposeState;

    /// <summary>
    /// Used by the dispose path to wait (via <see cref="Monitor.Wait(object)"/>) for
    /// <see cref="activeCallCount"/> to reach zero, and by active calls to pulse it when they exit.
    /// </summary>
    private readonly object disposeLock = new();

    /// <summary>
    /// Associates this proxy with the worker it forwards calls to. Called once, immediately after
    /// <see cref="DispatchProxy.Create{TInterface, TProxy}"/>, before the proxy is returned to the caller.
    /// </summary>
    /// <param name="workerProcess">Isolated worker process backing this proxy.</param>
    /// <param name="handle">Handle of this proxy's loaded interface instance on <paramref name="workerProcess"/>.</param>
    /// <param name="ownsWorker">
    /// <see langword="true"/> when this proxy exclusively owns <paramref name="workerProcess"/> (the
    /// classic <see cref="LibraryMapper.Emit{TInterface}"/> path, one worker per interface): disposing
    /// the proxy kills the entire worker process. <see langword="false"/> for a proxy obtained through
    /// <see cref="EmitWorkerPool"/>, which may share <paramref name="workerProcess"/> with other loaded
    /// interfaces: disposing the proxy only releases this interface's handle
    /// (<see cref="EmitWorkerProcess.UnloadInterface"/>), leaving the worker running for the others.
    /// </param>
    internal void AttachWorker(EmitWorkerProcess workerProcess, int handle, bool ownsWorker)
    {
        worker = workerProcess;
        this.handle = handle;
        this.ownsWorker = ownsWorker;
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];

        if (IsDisposeMethod(targetMethod))
        {
            // Only the first Dispose call does real work; subsequent calls are no-ops.
            if (Interlocked.Exchange(ref disposeState, 1) != 0)
                return null;

            // Wait for any currently-executing InvokeMethod calls to complete.
            // Active calls that started before disposeState was set to 1 will find it non-zero
            // on their double-check and throw ObjectDisposedException from their finally block,
            // but the decrement in their finally still runs and may pulse this lock.
            lock (disposeLock)
            {
                while (Volatile.Read(ref activeCallCount) > 0)
                    Monitor.Wait(disposeLock);
            }

            if (worker is { } activeWorker)
            {
                if (ownsWorker)
                    activeWorker.Dispose();
                else
                    activeWorker.UnloadInterface(handle);
            }

            worker = null;
            return null;
        }

        // Fast path: check dispose state before entering the active-call tracking region.
        if (disposeState != 0)
        {
            throw new ObjectDisposedException(targetMethod.DeclaringType?.FullName ?? nameof(EmitWorkerProxy));
        }

        // Increment before the second dispose check so the dispose path always sees our intent.
        Interlocked.Increment(ref activeCallCount);
        try
        {
            // Double-check after increment: the dispose path may have raced past the fast-path check.
            if (disposeState != 0)
            {
                throw new ObjectDisposedException(targetMethod.DeclaringType?.FullName ?? nameof(EmitWorkerProxy));
            }

            if (worker is null)
            {
                throw new ObjectDisposedException(targetMethod.DeclaringType?.FullName ?? nameof(EmitWorkerProxy));
            }

            return worker.InvokeMethod(handle, targetMethod, args);
        }
        finally
        {
            lock (disposeLock)
            {
                if (Interlocked.Decrement(ref activeCallCount) == 0)
                    Monitor.PulseAll(disposeLock);
            }
        }
    }

    private static bool IsDisposeMethod(MethodInfo method) =>
        method.Name == nameof(IDisposable.Dispose) &&
        method.GetParameters().Length == 0 &&
        typeof(IDisposable).IsAssignableFrom(method.DeclaringType);
}
