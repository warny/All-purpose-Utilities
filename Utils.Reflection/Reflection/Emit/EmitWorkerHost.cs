using System;
using System.Collections.Concurrent;
using System.IO;
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
    /// A single native mapping instance loaded by a <see cref="WorkerRequestKind.Load"/> request, kept
    /// alive until a matching <see cref="WorkerRequestKind.Unload"/> request or worker shutdown.
    /// </summary>
    /// <param name="Instance">The emitted mapping instance (an <see cref="Utils.Reflection.LibraryMapper"/> subclass).</param>
    /// <param name="InterfaceType">Interface the instance was mapped from, used to resolve method tokens on <see cref="WorkerRequestKind.Call"/>.</param>
    private readonly record struct LoadedInterface(object Instance, Type InterfaceType);

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
    /// </remarks>
    /// <param name="input">Reader for incoming JSON-line requests.</param>
    /// <param name="output">Writer for outgoing JSON-line responses.</param>
    internal static void Run(TextReader input, TextWriter output)
    {
        var loaded = new ConcurrentDictionary<int, LoadedInterface>();
        var nextHandleBox = new int[1];
        var writeLock = new object();
        var dispatched = new ConcurrentBag<Task>();

        string? line;
        while ((line = input.ReadLine()) is not null)
        {
            WorkerRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<WorkerRequest>(line);
            }
            catch (JsonException)
            {
                // A malformed line should never happen with a well-behaved host; ignore it rather
                // than tearing down the worker over a single corrupted message.
                continue;
            }

            if (request is null)
            {
                continue;
            }

            if (request.Kind == WorkerRequestKind.Shutdown)
            {
                DrainDispatched(dispatched);
                WriteResponse(output, writeLock, new WorkerResponse { Id = request.Id, Success = true });
                return;
            }

            WorkerRequest dispatchedRequest = request;
            dispatched.Add(Task.Run(() => ProcessRequest(dispatchedRequest, loaded, nextHandleBox, output, writeLock)));
        }

        // The input ended without an explicit Shutdown request (for example, the host's side of the
        // pipe closed abruptly). Still give already-dispatched requests a chance to finish and write
        // their response before returning, for the same reason the Shutdown path below does — otherwise
        // a request dispatched just before the last line was read could be silently dropped.
        DrainDispatched(dispatched);
    }

    /// <summary>
    /// Best-effort: waits for every request dispatched so far to finish (and write its response) before
    /// <see cref="Run"/> returns, so requests already in flight are not silently abandoned. Not a hard
    /// guarantee — a request that is itself stuck is bounded by <see cref="EmitWorkerProcess.SendAndReceive"/>'s
    /// per-request timeout on the host side, not by this drain, and can still be abandoned here.
    /// </summary>
    private static void DrainDispatched(ConcurrentBag<Task> dispatched)
    {
        try
        {
            Task.WaitAll([.. dispatched], TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Best-effort; the caller proceeds (shuts down or returns) regardless.
        }
    }

    /// <summary>
    /// Handles a single dispatched request end to end (Load/Call/Unload, or capturing the exception as
    /// a failure response) and writes its response. Runs on a thread-pool thread, concurrently with any
    /// other in-flight request on the same worker — see the class remarks.
    /// </summary>
    private static void ProcessRequest(
        WorkerRequest request, ConcurrentDictionary<int, LoadedInterface> loaded, int[] nextHandleBox,
        TextWriter output, object writeLock)
    {
        WorkerResponse response;
        try
        {
            response = request.Kind switch
            {
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
    private static WorkerResponse HandleLoad(WorkerRequest request, ConcurrentDictionary<int, LoadedInterface> loaded, int[] nextHandleBox)
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

        int handle = Interlocked.Increment(ref nextHandleBox[0]);
        loaded[handle] = new LoadedInterface(nativeInstance, interfaceType);

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
    /// request races exactly as a concurrent Dispose/call pair would in-process: whichever runs first
    /// wins, and a Call that loses the race fails with an unknown-handle error rather than corrupting
    /// state. Callers that need a strict ordering between the two must serialize them themselves (for
    /// example, only calling <see cref="EmitWorkerProcess.UnloadInterface"/> once no other thread holds
    /// a reference to the corresponding proxy).
    /// </remarks>
    private static WorkerResponse HandleUnload(ConcurrentDictionary<int, LoadedInterface> loaded, WorkerRequest request)
    {
        if (loaded.TryRemove(request.Handle, out LoadedInterface entry) && entry.Instance is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return new WorkerResponse { Id = request.Id, Success = true };
    }

    /// <summary>
    /// Resolves the <see cref="WorkerRequestKind.Load"/>-allocated handle referenced by a
    /// <see cref="WorkerRequestKind.Call"/> request.
    /// </summary>
    private static LoadedInterface GetLoadedOrThrow(ConcurrentDictionary<int, LoadedInterface> loaded, int handle)
    {
        if (!loaded.TryGetValue(handle, out LoadedInterface entry))
        {
            throw new InvalidOperationException(
                $"Received a Call request for handle {handle}, which was never loaded (or was already unloaded) on this worker.");
        }

        return entry;
    }

    /// <summary>
    /// Resolves the requested interface method by metadata token and invokes it on the native
    /// mapping instance, round-tripping arguments and the return value as JSON.
    /// </summary>
    private static WorkerResponse HandleCall(LoadedInterface loadedInterface, WorkerRequest request)
    {
        var method = (MethodInfo)loadedInterface.InterfaceType.Module.ResolveMethod(request.MethodMetadataToken);
        ParameterInfo[] parameters = method.GetParameters();
        string?[] argumentsJson = request.ArgumentsJson ?? [];
        object?[] arguments = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            Type effectiveType = parameterType.IsByRef ? parameterType.GetElementType()! : parameterType;
            string? argumentJson = i < argumentsJson.Length ? argumentsJson[i] : null;
            arguments[i] = argumentJson is null ? null : JsonSerializer.Deserialize(argumentJson, effectiveType, CrossProcessMarshaling.JsonOptions);
        }

        object? result = method.Invoke(loadedInterface.Instance, arguments);

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
