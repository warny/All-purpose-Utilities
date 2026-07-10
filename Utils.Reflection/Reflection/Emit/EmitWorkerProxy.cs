using System;
using System.Reflection;

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
            if (worker is { } activeWorker)
            {
                if (ownsWorker)
                {
                    activeWorker.Dispose();
                }
                else
                {
                    activeWorker.UnloadInterface(handle);
                }
            }

            worker = null;
            return null;
        }

        if (worker is null)
        {
            throw new ObjectDisposedException(targetMethod.DeclaringType?.FullName ?? nameof(EmitWorkerProxy));
        }

        return worker.InvokeMethod(handle, targetMethod, args);
    }

    private static bool IsDisposeMethod(MethodInfo method) =>
        method.Name == nameof(IDisposable.Dispose) &&
        method.GetParameters().Length == 0 &&
        typeof(IDisposable).IsAssignableFrom(method.DeclaringType);
}
