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

    /// <summary>
    /// Associates this proxy with the worker it forwards calls to. Called once, immediately after
    /// <see cref="DispatchProxy.Create{TInterface, TProxy}"/>, before the proxy is returned to the caller.
    /// </summary>
    /// <param name="workerProcess">Isolated worker process backing this proxy.</param>
    internal void AttachWorker(EmitWorkerProcess workerProcess) => worker = workerProcess;

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];

        if (IsDisposeMethod(targetMethod))
        {
            worker?.Dispose();
            worker = null;
            return null;
        }

        if (worker is null)
        {
            throw new ObjectDisposedException(targetMethod.DeclaringType?.FullName ?? nameof(EmitWorkerProxy));
        }

        return worker.InvokeMethod(targetMethod, args);
    }

    private static bool IsDisposeMethod(MethodInfo method) =>
        method.Name == nameof(IDisposable.Dispose) &&
        method.GetParameters().Length == 0 &&
        typeof(IDisposable).IsAssignableFrom(method.DeclaringType);
}
