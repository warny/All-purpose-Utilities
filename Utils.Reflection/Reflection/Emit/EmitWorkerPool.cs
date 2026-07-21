using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Maps several native interfaces onto a single shared isolated Emit worker process, instead of the
/// one-process-per-interface cost of <see cref="LibraryMapper.Emit{TInterface}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LibraryMapper.Emit{TInterface}"/> re-launches the current executable as a brand new
/// sandboxed worker process every time it is called — a full CLR startup per mapped interface. An
/// application mapping several distinct native DLLs (or the same DLL through several interfaces) pays
/// that cost once per <c>Emit</c> call. <see cref="EmitWorkerPool"/> starts the worker once, on the
/// first <see cref="Emit{TInterface}"/> call, and reuses it for every subsequent call on the same pool
/// instance: each interface gets its own handle on the shared worker
/// (<see cref="EmitWorkerProcess.LoadInterface"/>), so calls on one interface are never misrouted to
/// another.
/// </para>
/// <para>
/// <b>Trade-off — this is an opt-in, not the default.</b> Sharing a worker process trades away some of
/// the isolation between the interfaces mapped through it: a crash or a hostile/misbehaving interface
/// loaded on the shared worker can take down every other interface loaded on the same pool, where
/// <see cref="LibraryMapper.Emit{TInterface}"/>'s one-worker-per-interface default keeps failures
/// contained to a single interface. Use a pool when the interfaces mapped through it come from a common
/// trust boundary (for example, several DLLs from the same vendor/build) and the reduced process-spawn
/// cost matters; keep using separate <see cref="LibraryMapper.Emit{TInterface}"/> calls when interfaces
/// need to be isolated from each other as well as from the calling process.
/// </para>
/// <para>
/// Disposing a proxy returned by <see cref="Emit{TInterface}"/> releases only that interface's resources
/// on the shared worker; the worker process itself, and any other interface loaded through this pool,
/// keeps running. Dispose the pool itself to shut the worker process down.
/// </para>
/// </remarks>
public sealed class EmitWorkerPool : IDisposable
{
    private readonly object gate = new();
    private readonly TimeSpan? loadTimeout;
    private readonly TimeSpan? callTimeout;
    private readonly bool includeDiagnostics;
    private EmitWorkerProcess? worker;
    private bool disposed;

    /// <summary>
    /// Injectable factory for unit tests. When non-<see langword="null"/>, replaces the default
    /// <see cref="EmitWorkerProcess.Start"/> call in <see cref="GetOrStartWorker"/> so tests can
    /// supply pre-configured workers without spawning a real isolated process.
    /// </summary>
    internal Func<EmitWorkerProcess>? WorkerFactory;

    /// <summary>
    /// Triggers the same worker-health-check-and-start logic as <see cref="Emit{TInterface}"/> but
    /// without loading any interface — for unit testing only.
    /// </summary>
    internal EmitWorkerProcess GetCurrentWorker() => GetOrStartWorker();

    /// <summary>
    /// Creates an empty pool. The shared worker process is not started until the first
    /// <see cref="Emit{TInterface}"/> call.
    /// </summary>
    /// <param name="loadTimeout">
    /// Maximum time to wait for the shared worker's response to each <see cref="Emit{TInterface}"/>
    /// call's load request. Defaults to <see cref="EmitWorkerProcess.DefaultLoadTimeout"/> when
    /// <see langword="null"/>.
    /// </param>
    /// <param name="callTimeout">
    /// Maximum time to wait for the shared worker's response to each native call forwarded through any
    /// proxy returned by this pool. Defaults to <see cref="EmitWorkerProcess.DefaultCallTimeout"/> when
    /// <see langword="null"/>.
    /// </param>
    /// <param name="includeDiagnostics">
    /// When <see langword="true"/>, remote exception type names and stack traces captured inside the
    /// worker are included in <see cref="EmitWorkerInvocationException.RemoteExceptionTypeName"/> and
    /// <see cref="EmitWorkerInvocationException.RemoteStackTrace"/> on failure. When
    /// <see langword="false"/> (the default), these details are suppressed to avoid exposing worker-internal
    /// filesystem paths and generated type names to callers.
    /// </param>
    public EmitWorkerPool(TimeSpan? loadTimeout = null, TimeSpan? callTimeout = null, bool includeDiagnostics = false)
    {
        if (loadTimeout.HasValue)
            EmitWorkerProcess.ValidateTimeout(loadTimeout.Value, nameof(loadTimeout));
        if (callTimeout.HasValue)
            EmitWorkerProcess.ValidateTimeout(callTimeout.Value, nameof(callTimeout));

        this.loadTimeout = loadTimeout;
        this.callTimeout = callTimeout;
        this.includeDiagnostics = includeDiagnostics;
    }

    /// <summary>
    /// Maps <paramref name="dllPath"/> to <typeparamref name="TInterface"/> on this pool's shared
    /// worker, starting the worker first if this is the first call on this pool instance.
    /// </summary>
    /// <typeparam name="TInterface">The interface that defines the functions to map.</typeparam>
    /// <param name="dllPath">The path to the DLL.</param>
    /// <param name="callingConvention">The calling convention of the functions.</param>
    /// <returns>A proxy implementing <typeparamref name="TInterface"/> that forwards every call to the shared worker.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has already been disposed.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="TInterface"/> uses a type that cannot cross a process boundary.
    /// </exception>
    public TInterface Emit<TInterface>(string dllPath, CallingConvention callingConvention)
        where TInterface : class, IDisposable
    {
        EmitWorkerProcess sharedWorker = GetOrStartWorker();

        int handle = sharedWorker.LoadInterface(
            typeof(TInterface), dllPath, callingConvention, loadTimeout ?? EmitWorkerProcess.DefaultLoadTimeout);

        // If proxy construction or attachment fails after the handle has been allocated, unload
        // the handle immediately so it is not orphaned on the shared worker.
        try
        {
            TInterface proxy = DispatchProxy.Create<TInterface, EmitWorkerProxy>();
            ((EmitWorkerProxy)(object)proxy).AttachWorker(sharedWorker, handle, ownsWorker: false);
            return proxy;
        }
        catch
        {
            sharedWorker.UnloadInterface(handle);
            throw;
        }
    }

    private EmitWorkerProcess GetOrStartWorker()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            // Replace a faulted or retired worker before accepting new Emit calls. Existing proxies
            // backed by the old worker continue to fail — we do not retry calls whose side effects
            // may be indeterminate.
            if (worker is { } existing && !existing.IsHealthy)
            {
                existing.Dispose();
                worker = null;
            }

            return worker ??= (WorkerFactory?.Invoke() ?? EmitWorkerProcess.Start(callTimeout, includeDiagnostics));
        }
    }

    /// <summary>
    /// Shuts down the shared worker process, invalidating every proxy previously returned by
    /// <see cref="Emit{TInterface}"/> on this pool.
    /// </summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            worker?.Dispose();
        }
    }
}
