using System;
using System.Collections.Concurrent;
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
    /// Lifetime-managed slot for a single native mapping instance loaded by a
    /// <see cref="WorkerRequestKind.Load"/> request. Tracks the number of <see cref="WorkerRequestKind.Call"/>
    /// requests currently executing against this instance so that <see cref="WorkerRequestKind.Unload"/>
    /// (triggered by <see cref="HandleUnload"/>) waits for all active calls to finish before disposing
    /// the native library handle, preventing use-after-free of emitted delegates.
    /// </summary>
    private sealed class LoadedInterfaceSlot : IDisposable
    {
        /// <summary>Gate protecting <see cref="_activeCallCount"/> and <see cref="_closing"/>.</summary>
        private readonly object _gate = new();
        private int _activeCallCount;
        private bool _closing;

        /// <summary>The emitted mapping instance (an <see cref="Utils.Reflection.LibraryMapper"/> subclass).</summary>
        internal object Instance { get; }

        /// <summary>Interface the instance was mapped from.</summary>
        internal Type InterfaceType { get; }

        /// <summary>
        /// Stable command table: the <see cref="MethodInfo"/> at index <c>i</c> is the method
        /// identified by command ID <c>i</c> in the protocol. Built once at load time via
        /// <see cref="CrossProcessMarshaling.BuildCommandTable"/> to avoid cross-module metadata
        /// token collisions.
        /// </summary>
        internal MethodInfo[] CommandTable { get; }

        internal LoadedInterfaceSlot(object instance, Type interfaceType, MethodInfo[] commandTable)
        {
            Instance = instance;
            InterfaceType = interfaceType;
            CommandTable = commandTable;
        }

        /// <summary>
        /// Tries to start a call on this slot. Increments the active-call count and returns
        /// <see langword="true"/> when the slot is still open; returns <see langword="false"/>
        /// when an Unload (<see cref="Dispose"/>) has already been initiated.
        /// </summary>
        internal bool TryBeginCall()
        {
            lock (_gate)
            {
                if (_closing)
                    return false;
                _activeCallCount++;
                return true;
            }
        }

        /// <summary>Ends a call started with <see cref="TryBeginCall"/> and notifies any waiting Dispose.</summary>
        internal void EndCall()
        {
            lock (_gate)
            {
                _activeCallCount--;
                if (_closing && _activeCallCount == 0)
                    Monitor.PulseAll(_gate);
            }
        }

        /// <summary>
        /// Marks the slot as closing (rejecting new <see cref="TryBeginCall"/> attempts), waits for
        /// all currently executing calls to complete via <see cref="EndCall"/>, and then disposes
        /// <see cref="Instance"/> if it implements <see cref="IDisposable"/>.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                _closing = true;
                while (_activeCallCount > 0)
                    Monitor.Wait(_gate);
            }

            if (Instance is IDisposable disposable)
                disposable.Dispose();
        }
    }

    /// <summary>
    /// Maximum number of requests that may be executing concurrently inside a single worker
    /// process. When this limit is reached, the reader loop blocks (backs up into the OS pipe
    /// buffer) rather than dispatching another <see cref="Task.Run"/>, preventing a fast
    /// producer from exhausting the thread pool or allocating unbounded native resources.
    /// </summary>
    internal const int MaxConcurrency = 64;

    /// <summary>
    /// Runs the request/response loop until a <see cref="WorkerRequestKind.Shutdown"/> request is
    /// received or the input stream ends.
    /// </summary>
    /// <remarks>
    /// A single worker can hold multiple concurrently loaded interfaces (see
    /// <see cref="EmitWorkerPool"/>): each <see cref="WorkerRequestKind.Load"/> allocates a new integer
    /// handle, returned in the response and required on every subsequent
    /// <see cref="WorkerRequestKind.Call"/>/<see cref="WorkerRequestKind.Unload"/> request for that
    /// instance, so several unrelated interfaces can share one worker process without their calls being
    /// misrouted to each other.
    /// <para>
    /// Concurrency is bounded to <see cref="MaxConcurrency"/> by a <see cref="SemaphoreSlim"/>:
    /// when the limit is reached the reader loop blocks on <c>Wait()</c>, which backs pressure
    /// up into the OS named-pipe buffer instead of spawning unbounded thread-pool tasks.
    /// </para>
    /// </remarks>
    /// <param name="input">Reader for incoming JSON-line requests.</param>
    /// <param name="output">Writer for outgoing JSON-line responses.</param>
    internal static void Run(TextReader input, TextWriter output)
    {
        var loaded = new ConcurrentDictionary<int, LoadedInterfaceSlot>();
        var nextHandleBox = new int[1];
        var writeLock = new object();
        // Only active (not-yet-completed) tasks are kept. Each task removes itself via a
        // continuation so the dictionary stays bounded — a long-lived worker that processes
        // many requests never accumulates references to completed tasks.
        var activeTasks = new ConcurrentDictionary<int, Task>();
        int nextTaskId = 0;
        using var concurrencyLimit = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);

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
                {
                    continue;
                }

                if (request.Kind == WorkerRequestKind.Shutdown)
                {
                    bool allDrained = DrainDispatched(activeTasks);
                    WriteResponse(output, writeLock, new WorkerResponse
                    {
                        Id = request.Id,
                        Success = allDrained,
                        ErrorMessage = allDrained ? null :
                            "Forced shutdown: one or more active calls did not complete within the drain deadline.",
                    });
                    return;
                }

                // Handshake is handled inline (not dispatched to the thread pool) so the host can
                // read the version response synchronously before the reader loop even starts.
                if (request.Kind == WorkerRequestKind.Handshake)
                {
                    WriteResponse(output, writeLock, new WorkerResponse
                    {
                        Id = request.Id,
                        Success = true,
                        WorkerVersion = typeof(EmitWorkerHost).Assembly.GetName().Version?.ToString() ?? "0.0.0.0",
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
                    t => activeTasks.TryRemove(taskId, out _),
                    TaskContinuationOptions.ExecuteSynchronously);
            }

            // The input ended without an explicit Shutdown request (for example, the host's side of
            // the pipe closed abruptly). Still give already-dispatched requests a chance to finish
            // and write their response before returning — otherwise a request dispatched just before
            // the last line was read could be silently dropped.
            DrainDispatched(activeTasks);
        }
        finally
        {
            // Dispose every still-loaded native mapping so their libraries are unloaded and any
            // library-specific shutdown code runs deterministically, regardless of how Run exits
            // (normal Shutdown, abrupt end-of-stream, or an unhandled exception).
            foreach (int key in loaded.Keys.ToArray())
            {
                if (loaded.TryRemove(key, out LoadedInterfaceSlot? slot))
                {
                    try { slot.Dispose(); }
                    catch { /* best-effort; move on to the next */ }
                }
            }
        }
    }

    /// <summary>
    /// Waits for every still-active request to finish (and write its response) before
    /// <see cref="Run"/> exits, so requests already in flight are not silently abandoned.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when all active tasks completed before the drain deadline;
    /// <see langword="false"/> when at least one task was still running when the deadline expired.
    /// </returns>
    private static bool DrainDispatched(ConcurrentDictionary<int, Task> activeTasks)
    {
        try
        {
            Task[] snapshot = [.. activeTasks.Values];
            return snapshot.Length == 0 || Task.WaitAll(snapshot, TimeSpan.FromSeconds(5));
        }
        catch
        {
            // A faulted task is still a completed task from the drain perspective.
            return false;
        }
    }

    /// <summary>
    /// Handles a single dispatched request end to end (Load/Call/Unload, or capturing the exception as
    /// a failure response) and writes its response. Runs on a thread-pool thread, concurrently with any
    /// other in-flight request on the same worker — see the class remarks.
    /// </summary>
    private static void ProcessRequest(
        WorkerRequest request, ConcurrentDictionary<int, LoadedInterfaceSlot> loaded, int[] nextHandleBox,
        TextWriter output, object writeLock)
    {
        WorkerResponse response;
        try
        {
            response = request.Kind switch
            {
                WorkerRequestKind.Load => HandleLoad(request, loaded, nextHandleBox),
                WorkerRequestKind.Call => HandleCall(GetSlotOrThrow(loaded, request.Handle), request),
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
                ErrorStackTrace = effective.StackTrace,
            };
        }

        WriteResponse(output, writeLock, response);
    }

    /// <summary>
    /// Loads the interface's declaring assembly, validates it can be marshaled across the process
    /// boundary, emits/wires the native mapping instance, and allocates a new handle for it.
    /// </summary>
    /// <param name="nextHandleBox">
    /// Single-element box holding the last allocated handle, incremented atomically
    /// (<see cref="Interlocked.Increment(ref int)"/>) since concurrently dispatched <see cref="WorkerRequestKind.Load"/>
    /// requests must never be handed the same handle. A plain <see langword="ref"/> local cannot be
    /// captured by the <see cref="Task.Run(Action)"/> closures in <see cref="Run"/>, hence the boxed array.
    /// </param>
    private static WorkerResponse HandleLoad(WorkerRequest request, ConcurrentDictionary<int, LoadedInterfaceSlot> loaded, int[] nextHandleBox)
    {
        Assembly interfaceAssembly = Assembly.LoadFrom(
            request.InterfaceAssemblyPath ?? throw new InvalidOperationException("Load request is missing the interface assembly path."));

        Type interfaceType = interfaceAssembly.GetType(
            request.InterfaceTypeFullName ?? throw new InvalidOperationException("Load request is missing the interface type name."),
            throwOnError: true)!;

        CrossProcessMarshaling.EnsureInterfaceIsSupported(interfaceType);

        // Executed inside an isolated worker process: even if a hostile interface definition were
        // to inject code through crafted member names (see EmitDllMappableClass), the blast radius
        // is contained by the process-container permissions this worker was launched with.
#pragma warning disable UTILSREFL001
        object nativeInstance = LibraryMapper.EmitCore(
            interfaceType,
            request.DllPath ?? throw new InvalidOperationException("Load request is missing the native DLL path."),
            request.CallingConvention);
#pragma warning restore UTILSREFL001

        MethodInfo[] commandTable = CrossProcessMarshaling.BuildCommandTable(interfaceType);
        int handle = Interlocked.Increment(ref nextHandleBox[0]);
        loaded[handle] = new LoadedInterfaceSlot(nativeInstance, interfaceType, commandTable);

        return new WorkerResponse { Id = request.Id, Success = true, Handle = handle };
    }

    /// <summary>
    /// Releases the native mapping instance associated with <paramref name="request"/>'s handle
    /// (disposing it, which unloads its native DLL), freeing the worker to hold other loaded
    /// interfaces without leaking this one. Unlike an unknown <see cref="WorkerRequestKind.Call"/>
    /// handle, unloading an already-unloaded or unknown handle is not an error: it is a best-effort
    /// cleanup request, and the caller (<see cref="EmitWorkerProcess.UnloadInterface"/>) has no
    /// further use for the handle either way once it asks to release it.
    /// </summary>
    /// <remarks>
    /// A <see cref="WorkerRequestKind.Call"/> for the same handle dispatched concurrently with this
    /// request is safe: <see cref="LoadedInterfaceSlot.Dispose"/> marks the slot as closing and waits
    /// for any in-progress call to complete via <see cref="LoadedInterfaceSlot.EndCall"/> before
    /// disposing the native library handle. A call that arrives after the slot is removed from the
    /// dictionary gets an unknown-handle error. A call that is already executing completes normally;
    /// a call that races at <see cref="LoadedInterfaceSlot.TryBeginCall"/> after marking is rejected
    /// with an error instead of running against a freed handle.
    /// </remarks>
    private static WorkerResponse HandleUnload(ConcurrentDictionary<int, LoadedInterfaceSlot> loaded, WorkerRequest request)
    {
        if (loaded.TryRemove(request.Handle, out LoadedInterfaceSlot? slot))
        {
            slot.Dispose();
        }

        return new WorkerResponse { Id = request.Id, Success = true };
    }

    /// <summary>
    /// Resolves the <see cref="WorkerRequestKind.Load"/>-allocated handle referenced by a
    /// <see cref="WorkerRequestKind.Call"/> request.
    /// </summary>
    private static LoadedInterfaceSlot GetSlotOrThrow(ConcurrentDictionary<int, LoadedInterfaceSlot> loaded, int handle)
    {
        if (!loaded.TryGetValue(handle, out LoadedInterfaceSlot? slot))
        {
            throw new InvalidOperationException(
                $"Received a Call request for handle {handle}, which was never loaded (or was already unloaded) on this worker.");
        }

        return slot;
    }

    /// <summary>
    /// Resolves the requested interface method by command ID and invokes it on the native
    /// mapping instance, round-tripping arguments and the return value as JSON.
    /// </summary>
    /// <remarks>
    /// The command ID in the request is an index into <see cref="LoadedInterfaceSlot.CommandTable"/>,
    /// which is the same deterministic ordering produced by
    /// <see cref="CrossProcessMarshaling.BuildCommandTable"/> on both the host and worker sides at load
    /// time. Using a stable index avoids cross-module metadata token collisions for methods inherited
    /// from another assembly/module.
    /// <para>
    /// Call lifetime is coordinated with <see cref="HandleUnload"/> via
    /// <see cref="LoadedInterfaceSlot.TryBeginCall"/> / <see cref="LoadedInterfaceSlot.EndCall"/>:
    /// the slot refuses new calls once an Unload has started, and the Unload waits for any call
    /// already past <see cref="LoadedInterfaceSlot.TryBeginCall"/> to complete before disposing
    /// the native library handle.
    /// </para>
    /// </remarks>
    private static WorkerResponse HandleCall(LoadedInterfaceSlot slot, WorkerRequest request)
    {
        if (!slot.TryBeginCall())
        {
            throw new InvalidOperationException(
                $"Call request for handle {request.Handle} was rejected because the interface is being unloaded.");
        }

        try
        {
            int commandId = request.MethodCommandId;
            if ((uint)commandId >= (uint)slot.CommandTable.Length)
            {
                throw new InvalidOperationException(
                    $"Call request command ID {commandId} is out of range for interface " +
                    $"'{slot.InterfaceType.FullName}' (command table has {slot.CommandTable.Length} entries).");
            }

            MethodInfo method = slot.CommandTable[commandId];
            ParameterInfo[] parameters = method.GetParameters();
            string?[] argumentsJson = request.ArgumentsJson ?? [];

            if (argumentsJson.Length != parameters.Length)
            {
                throw new InvalidOperationException(
                    $"Call request for '{method.Name}' has {argumentsJson.Length} argument slot(s) but the " +
                    $"method expects exactly {parameters.Length}. Every parameter, including 'out' parameters, " +
                    "must have an explicit slot; use 'null' for uninitialized 'out' parameter slots.");
            }

            object?[] arguments = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                Type effectiveType = parameterType.IsByRef ? parameterType.GetElementType()! : parameterType;
                string? argumentJson = argumentsJson[i];
                arguments[i] = argumentJson is null ? null : JsonSerializer.Deserialize(argumentJson, effectiveType, CrossProcessMarshaling.JsonOptions);
            }

            object? result = method.Invoke(slot.Instance, arguments);

            string?[] byRefValuesJson = new string?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType.IsByRef)
                {
                    Type effectiveType = parameters[i].ParameterType.GetElementType()!;
                    byRefValuesJson[i] = JsonSerializer.Serialize(arguments[i], effectiveType, CrossProcessMarshaling.JsonOptions);
                }
            }

            return new WorkerResponse
            {
                Id = request.Id,
                Success = true,
                Handle = request.Handle,
                ReturnValueJson = method.ReturnType == typeof(void) ? null : JsonSerializer.Serialize(result, method.ReturnType, CrossProcessMarshaling.JsonOptions),
                ByRefValuesJson = byRefValuesJson,
            };
        }
        finally
        {
            slot.EndCall();
        }
    }

    /// <summary>
    /// Writes one response line, guarded by <paramref name="writeLock"/> so concurrently completing
    /// dispatched requests (see <see cref="ProcessRequest"/>) never interleave their output.
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
