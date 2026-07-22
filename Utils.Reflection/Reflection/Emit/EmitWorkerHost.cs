using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Runs inside the isolated Emit worker process. Reads <see cref="WorkerRequest"/> lines from
/// <see cref="TextReader"/>, performs the requested load/call/unload/shutdown, and writes back
/// <see cref="WorkerResponse"/> lines.
/// </summary>
/// <remarks>
/// Each request is dispatched to the thread pool (<see cref="Task.Run(Action)"/>) as soon as it is read,
/// instead of being handled inline before the next line is read: several <see cref="WorkerRequestKind.Call"/>
/// requests can therefore execute concurrently inside the worker, matching <see cref="EmitWorkerProcess"/>'s
/// id-correlated protocol on the host side, which no longer waits for one call to finish before sending
/// the next. Response order on the wire is not guaranteed to match request order as a result — the host
/// does not rely on it, matching responses to requests by <see cref="WorkerResponse.Id"/> instead. The
/// native library backing a mapped interface must itself be safe to call concurrently for this to be
/// safe; this worker has no way to know or enforce that, so it is entirely the caller's responsibility.
/// </remarks>
internal static class EmitWorkerHost
{
    /// <summary>
    /// Current wire protocol version. Both the host (in <see cref="EmitWorkerProcess.ProtocolVersion"/>)
    /// and the worker declare this constant with the same value; mismatches are detected during the
    /// initial <see cref="WorkerRequestKind.Hello"/> handshake so stale executables are rejected before
    /// any <see cref="WorkerRequestKind.Load"/> request is sent.
    /// </summary>
    internal const int ProtocolVersion = 1;

    /// <summary>
    /// Maximum number of requests that may be executing concurrently inside a single worker
    /// process. When this limit is reached, the reader loop blocks (backs up into the OS pipe
    /// buffer) rather than dispatching another <see cref="Task.Run"/>, preventing a fast
    /// producer from exhausting the thread pool or allocating unbounded native resources.
    /// </summary>
    internal const int MaxConcurrency = 64;

    /// <summary>
    /// Maximum time allotted to drain all in-flight request tasks when a graceful shutdown is
    /// requested. After this deadline, the worker sends a response with
    /// <see cref="WorkerResponse.ShutdownWasGraceful"/> set to <see langword="false"/> to notify
    /// the host that some tasks may still be executing.
    /// </summary>
    private static readonly TimeSpan GracefulShutdownDrainTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Reference-type owner for a single loaded interface handle. Controls call admission via
    /// lease tokens and ensures the underlying native mapping is disposed exactly once, only after
    /// all active calls have completed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lifecycle transitions are: <c>Open → Closing → Disposed</c>.
    /// <see cref="TryAcquireCallLease"/> rejects any state other than <c>Open</c>.
    /// <see cref="CloseAndDispose"/> switches to <c>Closing</c>, waits (without force) or skips
    /// (with force) the active-call drain, then disposes.
    /// </para>
    /// <para>
    /// A per-handle lock guards all transitions; it is never held across a native call, so one
    /// blocked DLL cannot stop unrelated handles from being closed or called.
    /// </para>
    /// </remarks>
    internal sealed class LoadedInterfaceState : IDisposable
    {
        private enum Lifecycle { Open, Closing, Disposed }

        private readonly object _lifetimeLock = new();
        private Lifecycle _lifecycle = Lifecycle.Open;
        private int _activeCallCount;

        /// <summary>The emitted native mapping instance.</summary>
        public object Instance { get; }

        /// <summary>Interface type the instance was mapped from.</summary>
        public Type InterfaceType { get; }

        /// <summary>Frozen table mapping worker-assigned method IDs to validated <see cref="MethodInfo"/> instances.</summary>
        public FrozenDictionary<int, MethodInfo> MethodsById { get; }

        /// <summary>
        /// Creates a state object wrapping <paramref name="instance"/> and associates it with
        /// the given <paramref name="interfaceType"/> and <paramref name="methodsById"/> table.
        /// </summary>
        public LoadedInterfaceState(
            object instance,
            Type interfaceType,
            FrozenDictionary<int, MethodInfo> methodsById)
        {
            Instance = instance;
            InterfaceType = interfaceType;
            MethodsById = methodsById;
        }

        /// <summary>
        /// Tries to acquire a call lease for the duration of a single native invocation.
        /// Returns <see langword="null"/> when the handle is already closing or disposed.
        /// The caller must dispose the returned token after the native call returns to decrement
        /// the active-call count, which may unblock a concurrent <see cref="CloseAndDispose"/>.
        /// </summary>
        public CallLease? TryAcquireCallLease()
        {
            lock (_lifetimeLock)
            {
                if (_lifecycle != Lifecycle.Open)
                    return null;
                _activeCallCount++;
                return new CallLease(this);
            }
        }

        private void ReleaseCallLease()
        {
            lock (_lifetimeLock)
            {
                _activeCallCount--;
                if (_lifecycle == Lifecycle.Closing && _activeCallCount == 0)
                    Monitor.PulseAll(_lifetimeLock);
            }
        }

        /// <summary>
        /// Marks the handle as closing, optionally waits for active calls to drain, then disposes
        /// the underlying mapping exactly once.
        /// </summary>
        /// <param name="force">
        /// When <see langword="true"/>, skips waiting for active leases to be released — suitable
        /// when the worker process is already exiting and leaving pending native calls stuck would
        /// itself be a safety problem. The process will be killed by the host shortly after.
        /// When <see langword="false"/>, blocks until all active calls have released their leases.
        /// </param>
        public void CloseAndDispose(bool force = false)
        {
            bool shouldDispose;
            lock (_lifetimeLock)
            {
                if (_lifecycle == Lifecycle.Disposed)
                    return;
                _lifecycle = Lifecycle.Closing;
                if (!force)
                {
                    while (_activeCallCount > 0)
                        Monitor.Wait(_lifetimeLock);
                }
                _lifecycle = Lifecycle.Disposed;
                shouldDispose = true;
            }

            if (shouldDispose && Instance is IDisposable disposable)
            {
                try { disposable.Dispose(); }
                catch { /* best-effort: process is about to exit */ }
            }
        }

        /// <inheritdoc/>
        public void Dispose() => CloseAndDispose(force: false);

        /// <summary>
        /// Call-lease token returned by <see cref="TryAcquireCallLease"/>. Disposing this token
        /// decrements the owning state's active-call count and may unblock a pending
        /// <see cref="CloseAndDispose"/>.
        /// </summary>
        public sealed class CallLease : IDisposable
        {
            private readonly LoadedInterfaceState _owner;

            internal CallLease(LoadedInterfaceState owner) => _owner = owner;

            /// <summary>Releases the call lease, decrementing the owning state's active-call count.</summary>
            public void Dispose() => _owner.ReleaseCallLease();
        }
    }

    /// <summary>
    /// Runs the request/response loop until a <see cref="WorkerRequestKind.Shutdown"/> request is
    /// received or the input stream ends. Always disposes every remaining loaded interface in a
    /// <c>finally</c> block, so no native resources are leaked regardless of how the loop exits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single worker can hold multiple concurrently loaded interfaces (see
    /// <see cref="EmitWorkerPool"/>): each <see cref="WorkerRequestKind.Load"/> allocates a new integer
    /// handle, returned in the response and required on every subsequent
    /// <see cref="WorkerRequestKind.Call"/>/<see cref="WorkerRequestKind.Unload"/> request for that
    /// instance.
    /// </para>
    /// <para>
    /// Concurrency is bounded to <see cref="MaxConcurrency"/> by a <see cref="SemaphoreSlim"/>.
    /// </para>
    /// <para>
    /// On a <see cref="WorkerRequestKind.Shutdown"/> request, the loop transitions to a Draining state:
    /// no new requests are accepted, all in-flight tasks are awaited up to
    /// <see cref="GracefulShutdownDrainTimeout"/>, and then every loaded handle is closed via its
    /// lease-aware <see cref="LoadedInterfaceState.CloseAndDispose"/>. A successful response with
    /// <see cref="WorkerResponse.ShutdownWasGraceful"/> indicating whether the deadline was met is sent
    /// only after all of this completes.
    /// </para>
    /// </remarks>
    /// <param name="input">Reader for incoming JSON-line requests.</param>
    /// <param name="output">Writer for outgoing JSON-line responses.</param>
    internal static void Run(TextReader input, TextWriter output)
    {
        var loaded = new ConcurrentDictionary<int, LoadedInterfaceState>();
        var nextHandleBox = new int[1];
        var writeLock = new object();
        // Only active (not-yet-completed) tasks are kept. Each task removes itself via a
        // continuation so the dictionary stays bounded.
        var activeTasks = new ConcurrentDictionary<int, Task>();
        int nextTaskId = 0;
        using var concurrencyLimit = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        bool draining = false;

        try
        {
            string? line;
            while ((line = ProtocolFraming.ReadBoundedLine(input)) is not null)
            {
                WorkerRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<WorkerRequest>(line);
                }
                catch (JsonException ex)
                {
                    // A malformed request line indicates framing corruption — the protocol is no longer
                    // trustworthy. Throw so the worker exits rather than continuing with an unknown state.
                    throw new InvalidOperationException(
                        "The Emit worker host received a request line that could not be deserialized. " +
                        "The connection is now unusable.", ex);
                }

                if (request is null)
                    continue;

                if (request.Kind == WorkerRequestKind.Shutdown)
                {
                    draining = true;
                    bool graceful = DrainAndCloseAll(activeTasks, loaded, force: false);
                    WriteResponse(output, writeLock, new WorkerResponse
                    {
                        Id = request.Id,
                        Success = true,
                        ShutdownWasGraceful = graceful,
                    });
                    return;
                }

                if (draining)
                {
                    // Already draining — reject any request that arrives after a Shutdown was issued.
                    WriteResponse(output, writeLock, new WorkerResponse
                    {
                        Id = request.Id,
                        Success = false,
                        ErrorMessage = "The worker is shutting down and cannot accept new requests.",
                    });
                    continue;
                }

                // Acquire the semaphore BEFORE Task.Run so the reader loop blocks (backs up into the
                // OS pipe buffer) rather than enqueuing an unbounded number of thread-pool tasks.
                concurrencyLimit.Wait();

                WorkerRequest dispatchedRequest = request;
                int taskId = Interlocked.Increment(ref nextTaskId);

                Task task = Task.Run(() =>
                {
                    try
                    {
                        ProcessRequest(dispatchedRequest, loaded, nextHandleBox, output, writeLock);
                    }
                    finally
                    {
                        concurrencyLimit.Release();
                    }
                });

                // Register before attaching the removal continuation so the entry is always present
                // when the continuation fires, even if the task completes extremely quickly.
                activeTasks[taskId] = task;
                _ = task.ContinueWith(
                    _ => activeTasks.TryRemove(taskId, out _),
                    TaskContinuationOptions.ExecuteSynchronously);
            }

            // The input stream ended without an explicit Shutdown request. Drain dispatched tasks
            // and close handles, then return without writing any response.
            DrainAndCloseAll(activeTasks, loaded, force: false);
        }
        finally
        {
            // Best-effort cleanup for all remaining handles not yet closed by the normal paths.
            // Uses force=true because the process is about to exit: waiting indefinitely for
            // stuck native calls to drain would itself be worse than the race.
            foreach (int handle in loaded.Keys)
            {
                if (loaded.TryRemove(handle, out LoadedInterfaceState? state))
                {
                    try { state.CloseAndDispose(force: true); }
                    catch { /* best-effort */ }
                }
            }
        }
    }

    /// <summary>
    /// Awaits all still-active dispatched tasks up to <see cref="GracefulShutdownDrainTimeout"/>,
    /// then closes every remaining loaded handle. Returns <see langword="true"/> when all tasks
    /// completed before the deadline, <see langword="false"/> when the deadline expired.
    /// </summary>
    private static bool DrainAndCloseAll(
        ConcurrentDictionary<int, Task> activeTasks,
        ConcurrentDictionary<int, LoadedInterfaceState> loaded,
        bool force)
    {
        bool graceful = true;
        try
        {
            Task[] snapshot = [.. activeTasks.Values];
            if (snapshot.Length > 0)
                graceful = Task.WaitAll(snapshot, GracefulShutdownDrainTimeout);
        }
        catch
        {
            graceful = false;
        }

        // Close every remaining loaded handle. If a graceful drain timed out, use force=true so
        // CloseAndDispose does not block on calls that never finished.
        bool forceClose = force || !graceful;
        foreach (int handle in loaded.Keys)
        {
            if (loaded.TryRemove(handle, out LoadedInterfaceState? state))
            {
                try { state.CloseAndDispose(forceClose); }
                catch { /* best-effort */ }
            }
        }

        return graceful;
    }

    /// <summary>
    /// Handles a single dispatched request end to end (Load/Call/Unload, or capturing the exception as
    /// a failure response) and writes its response. Runs on a thread-pool thread, concurrently with any
    /// other in-flight request on the same worker.
    /// </summary>
    private static void ProcessRequest(
        WorkerRequest request, ConcurrentDictionary<int, LoadedInterfaceState> loaded, int[] nextHandleBox,
        TextWriter output, object writeLock)
    {
        WorkerResponse response;
        try
        {
            response = request.Kind switch
            {
                WorkerRequestKind.Hello => HandleHello(request),
                WorkerRequestKind.Load => HandleLoad(request, loaded, nextHandleBox),
                WorkerRequestKind.Call => HandleCall(GetLoadedOrThrow(loaded, request.Handle), request),
                WorkerRequestKind.Unload => HandleUnload(loaded, request),
                _ => throw new InvalidOperationException($"Unknown worker request kind '{request.Kind}'."),
            };
        }
        catch (Exception ex)
        {
            Exception effective = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            response = new WorkerResponse
            {
                Id = request.Id,
                Success = false,
                ErrorMessage = effective.Message,
                ErrorTypeName = effective.GetType().FullName,
                // Stack trace is omitted by default; it may contain internal paths and type names.
                // Expose it only when explicitly opted in via diagnostics configuration.
            };
        }

        WriteResponse(output, writeLock, response);
    }

    /// <summary>
    /// Validates the host's protocol version and echoes the worker's version back. A mismatched
    /// version causes an immediate failure response so the host can decide whether to kill the worker.
    /// </summary>
    private static WorkerResponse HandleHello(WorkerRequest request)
    {
        if (request.ProtocolVersion != ProtocolVersion)
        {
            return new WorkerResponse
            {
                Id = request.Id,
                Success = false,
                ErrorMessage =
                    $"Protocol version mismatch: host uses version {request.ProtocolVersion}, " +
                    $"worker implements version {ProtocolVersion}. " +
                    "Ensure both the host process and the worker executable are from the same build.",
            };
        }

        return new WorkerResponse
        {
            Id = request.Id,
            Success = true,
            ProtocolVersion = ProtocolVersion,
        };
    }

    /// <summary>
    /// Loads the interface's declaring assembly, emits/wires the native mapping instance, assigns
    /// worker-private method IDs, and allocates a new handle for the loaded state.
    /// </summary>
    private static WorkerResponse HandleLoad(
        WorkerRequest request,
        ConcurrentDictionary<int, LoadedInterfaceState> loaded,
        int[] nextHandleBox)
    {
        Assembly interfaceAssembly = Assembly.LoadFrom(
            request.InterfaceAssemblyPath
                ?? throw new InvalidOperationException("Load request is missing the interface assembly path."));

        Type interfaceType = interfaceAssembly.GetType(
            request.InterfaceTypeFullName
                ?? throw new InvalidOperationException("Load request is missing the interface type name."),
            throwOnError: true)!;

        CrossProcessMarshaling.EnsureInterfaceIsSupported(interfaceType);

        // Executed inside an isolated worker process: the blast radius is contained by the
        // process-container permissions this worker was launched with.
#pragma warning disable UTILSREFL001
        object nativeInstance = LibraryMapper.EmitCore(
            interfaceType,
            request.DllPath ?? throw new InvalidOperationException("Load request is missing the native DLL path."),
            request.CallingConvention);
#pragma warning restore UTILSREFL001

        // Assign contiguous worker-private method IDs at load time, independent of metadata tokens.
        // These IDs are stable for the lifetime of this loaded handle.
        MethodInfo[] methods = interfaceType.GetMethods();
        var methodsById = new Dictionary<int, MethodInfo>(methods.Length);
        var descriptors = new MethodDescriptorDto[methods.Length];
        for (int i = 0; i < methods.Length; i++)
        {
            methodsById[i] = methods[i];
            descriptors[i] = MethodDescriptorDto.FromMethodInfo(i, methods[i]);
        }

        int handle = Interlocked.Increment(ref nextHandleBox[0]);
        loaded[handle] = new LoadedInterfaceState(nativeInstance, interfaceType, methodsById.ToFrozenDictionary());

        return new WorkerResponse
        {
            Id = request.Id,
            Success = true,
            Handle = handle,
            MethodDescriptors = descriptors,
        };
    }

    /// <summary>
    /// Acquires the closing state for the handle and waits for all active calls to drain before
    /// disposing the underlying native mapping. Reports success regardless of whether the handle
    /// was already unloaded, since unload is a best-effort cleanup.
    /// </summary>
    private static WorkerResponse HandleUnload(
        ConcurrentDictionary<int, LoadedInterfaceState> loaded,
        WorkerRequest request)
    {
        if (loaded.TryRemove(request.Handle, out LoadedInterfaceState? state))
        {
            // Wait for any in-flight calls to finish before disposing; blocks only until all leases
            // acquired before TryRemove are released. New leases cannot be acquired after TryRemove
            // because GetLoadedOrThrow will not find the handle.
            state.CloseAndDispose(force: false);
        }

        return new WorkerResponse { Id = request.Id, Success = true };
    }

    /// <summary>
    /// Resolves the <see cref="WorkerRequestKind.Load"/>-allocated handle referenced by a
    /// <see cref="WorkerRequestKind.Call"/> request.
    /// </summary>
    private static LoadedInterfaceState GetLoadedOrThrow(
        ConcurrentDictionary<int, LoadedInterfaceState> loaded,
        int handle)
    {
        if (!loaded.TryGetValue(handle, out LoadedInterfaceState? entry))
        {
            throw new InvalidOperationException(
                $"Received a Call request for handle {handle}, which was never loaded (or was already unloaded) on this worker.");
        }

        return entry;
    }

    /// <summary>
    /// Looks up the method by its worker-assigned private ID, acquires a call lease to prevent
    /// concurrent disposal, invokes the method, and returns the serialized result.
    /// </summary>
    /// <remarks>
    /// The lease spans from just before argument deserialization through the end of return-value
    /// serialization, ensuring the native mapping instance cannot be disposed while the call
    /// (including any native code it reaches) is still executing.
    /// </remarks>
    private static WorkerResponse HandleCall(LoadedInterfaceState loadedInterface, WorkerRequest request)
    {
        if (!loadedInterface.MethodsById.TryGetValue(request.MethodId, out MethodInfo? method))
        {
            throw new InvalidOperationException(
                $"Call request method ID {request.MethodId} is not in the method table for handle " +
                $"{request.Handle} (interface '{loadedInterface.InterfaceType.FullName}'). " +
                "Only method IDs assigned at load time may be used.");
        }

        // Acquire a lease to prevent concurrent Unload/Dispose from releasing the mapping while
        // the native call executes. TryAcquireCallLease is atomic: if it returns non-null, the
        // mapping stays alive until the lease is disposed.
        using LoadedInterfaceState.CallLease? lease = loadedInterface.TryAcquireCallLease();
        if (lease is null)
        {
            throw new InvalidOperationException(
                $"Handle {request.Handle} is closing and cannot accept new calls.");
        }

        ParameterInfo[] parameters = method.GetParameters();
        string?[] argumentsJson = request.ArgumentsJson ?? [];

        if (argumentsJson.Length != parameters.Length)
        {
            throw new InvalidOperationException(
                $"Call request for '{method.Name}' supplied {argumentsJson.Length} argument(s) " +
                $"but the method declares {parameters.Length} parameter(s). " +
                "Argument count must match exactly.");
        }

        object?[] arguments = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            Type effectiveType = parameterType.IsByRef ? parameterType.GetElementType()! : parameterType;
            string? argumentJson = argumentsJson[i];
            arguments[i] = argumentJson is null
                ? null
                : JsonSerializer.Deserialize(argumentJson, effectiveType, CrossProcessMarshaling.JsonOptions);
        }

        object? result = method.Invoke(loadedInterface.Instance, arguments);

        string?[] byRefValuesJson = new string?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType.IsByRef)
            {
                Type effectiveType = parameters[i].ParameterType.GetElementType()!;
                byRefValuesJson[i] = JsonSerializer.Serialize(
                    arguments[i], effectiveType, CrossProcessMarshaling.JsonOptions);
            }
        }

        return new WorkerResponse
        {
            Id = request.Id,
            Success = true,
            Handle = request.Handle,
            ReturnValueJson = method.ReturnType == typeof(void)
                ? null
                : JsonSerializer.Serialize(result, method.ReturnType, CrossProcessMarshaling.JsonOptions),
            ByRefValuesJson = byRefValuesJson,
        };
    }

    /// <summary>
    /// Writes one response line, guarded by <paramref name="writeLock"/> so concurrently completing
    /// dispatched requests never interleave their output.
    /// </summary>
    private static void WriteResponse(TextWriter output, object writeLock, WorkerResponse response)
    {
        lock (writeLock)
        {
            output.WriteLine(JsonSerializer.Serialize(response));
            output.Flush();
        }
    }
}
