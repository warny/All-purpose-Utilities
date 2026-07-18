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
    /// Coordinates concurrent invocations with disposal: every active call holds a read lock while
    /// the dispose path holds the write lock. <see cref="EnterWriteLock"/> therefore blocks until all
    /// in-progress <see cref="EmitWorkerProcess.InvokeMethod"/> calls complete before the worker is
    /// unloaded or killed, preventing use-after-free of the native DLL handle inside the worker.
    /// </summary>
    private readonly ReaderWriterLockSlim invocationLock = new();

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
            // Write lock: blocks until every in-progress InvokeMethod call releases its read lock,
            // then prevents new invocations from starting. This ensures the worker is not killed
            // while a native call is still executing through it.
            invocationLock.EnterWriteLock();
            try
            {
                if (worker is { } activeWorker)
                {
                    if (ownsWorker)
                        activeWorker.Dispose();
                    else
                        activeWorker.UnloadInterface(handle);
                }

                worker = null;
            }
            finally
            {
                invocationLock.ExitWriteLock();
            }

            return null;
        }

        // Read lock: multiple callers may hold this concurrently; the write lock (Dispose) waits
        // for all of them before it can proceed.
        invocationLock.EnterReadLock();
        try
        {
            if (worker is null)
            {
                throw new ObjectDisposedException(targetMethod.DeclaringType?.FullName ?? nameof(EmitWorkerProxy));
            }

            return worker.InvokeMethod(handle, targetMethod, args);
        }
        finally
        {
            invocationLock.ExitReadLock();
        }
    }

    private static bool IsDisposeMethod(MethodInfo method) =>
        method.Name == nameof(IDisposable.Dispose) &&
        method.GetParameters().Length == 0 &&
        typeof(IDisposable).IsAssignableFrom(method.DeclaringType);
}
