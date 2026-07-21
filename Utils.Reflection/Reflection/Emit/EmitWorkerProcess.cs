using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Utils.Reflection.ProcessIsolation;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Launches and communicates with an isolated Emit worker: a copy of the current process re-executed
/// with a marker argument that makes it enter <see cref="LibraryMapper.RunWorkerIfRequested"/> instead
/// of its normal <c>Main</c> logic. The worker performs the native DLL loading and the (untrusted)
/// mapping-class generation described in <see cref="EmitDllMappableClass"/>, contained by whatever
/// process-container sandbox is available on the current platform.
/// </summary>
/// <remarks>
/// Requests are correlated to responses by <see cref="WorkerRequest.Id"/> (see
/// <see cref="RunReaderLoop"/>), so several requests can be in flight on the same worker at once — from
/// several threads calling the same proxy concurrently, or several proxies sharing a worker through
/// <see cref="EmitWorkerPool"/>. The worker itself dispatches each request it receives to the thread pool
/// (<see cref="EmitWorkerHost.Run"/>) instead of handling them one at a time, so calls genuinely execute
/// in parallel on both ends, not just queued. The native library backing a mapped interface must be
/// thread-safe for concurrent calls to be safe — that is the caller's responsibility, independent of this
/// class.
/// </remarks>
internal sealed class EmitWorkerProcess : IDisposable
{
    /// <summary>Default timeout for the initial <see cref="WorkerRequestKind.Load"/> request.</summary>
    internal static readonly TimeSpan DefaultLoadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Default timeout for each <see cref="WorkerRequestKind.Call"/> request.</summary>
    internal static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of abandoned calls (requests that timed out while still executing inside the worker)
    /// after which this worker is retired: <see cref="connectionFault"/> is set and all future
    /// callers receive <see cref="InvalidOperationException"/> instead of being sent to a process
    /// whose accumulated side effects are unknown.
    /// </summary>
    internal const int MaxAbandonedCalls = 5;

    /// <summary>
    /// Timeout for the graceful <see cref="WorkerRequestKind.Shutdown"/> handshake in
    /// <see cref="Dispose"/>. Short and non-configurable: a slow shutdown response falls back to
    /// <see cref="KillSilently"/> regardless, so there is nothing to gain from waiting longer.
    /// </summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    private IProcessContainer? sandbox;
    private readonly Process workerProcess;
    private readonly NamedPipeServerStream pipe;
    private readonly System.IO.StreamReader reader;
    private readonly System.IO.StreamWriter writer;
    private readonly object writeLock = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<WorkerResponse>> pending = new();

    /// <summary>
    /// Per-handle reverse command table, built at <see cref="LoadInterface"/> time and used by
    /// <see cref="InvokeMethod"/> to convert a <see cref="MethodInfo"/> to the stable command ID
    /// (<see cref="WorkerRequest.MethodCommandId"/>) that the worker expects. The key is the handle
    /// returned by <see cref="LoadInterface"/>; the value maps each interface method to its zero-based
    /// index in the array produced by <see cref="CrossProcessMarshaling.BuildCommandTable"/>.
    /// </summary>
    private readonly ConcurrentDictionary<int, FrozenDictionary<MethodInfo, int>> commandTables = new();

    private readonly Task readerLoop;
    private readonly TimeSpan callTimeout;

    /// <summary>
    /// When <see langword="true"/>, <see cref="ThrowIfFailed"/> includes
    /// <see cref="WorkerResponse.ErrorTypeName"/> and <see cref="WorkerResponse.ErrorStackTrace"/> in
    /// the thrown <see cref="EmitWorkerInvocationException"/>. When <see langword="false"/> (the
    /// default), those fields are suppressed so local filesystem paths, generated type names, and
    /// worker-internal stack frames are not exposed to callers by default (see TODO finding #9).
    /// </summary>
    private readonly bool includeDiagnostics;

    private long nextId;
    private int abandonedCallCount;
    private volatile Exception? connectionFault;
    private bool disposed;

    /// <summary>Number of requests currently awaiting a response. Exposed for unit testing only.</summary>
    internal int PendingCount => pending.Count;

    /// <summary>Number of calls that timed out while still running inside the worker. Exposed for unit testing only.</summary>
    internal int AbandonedCallCount => abandonedCallCount;

    /// <summary>
    /// <see langword="true"/> while this worker is connected and has not encountered a connection fault,
    /// a retirement (too many abandoned calls), or been disposed. Used by <see cref="EmitWorkerPool"/> to
    /// decide whether to replace this worker before accepting the next <see cref="EmitWorkerPool.Emit{TInterface}"/>
    /// call.
    /// </summary>
    internal bool IsHealthy => !disposed && connectionFault is null;

    private EmitWorkerProcess(
        IProcessContainer? sandbox,
        Process workerProcess,
        NamedPipeServerStream pipe,
        System.IO.StreamReader reader,
        System.IO.StreamWriter writer,
        TimeSpan callTimeout,
        bool includeDiagnostics = false)
    {
        this.sandbox = sandbox;
        this.workerProcess = workerProcess;
        this.pipe = pipe;
        this.reader = reader;
        this.writer = writer;
        this.callTimeout = callTimeout;
        this.includeDiagnostics = includeDiagnostics;
        readerLoop = Task.Run(RunReaderLoop);
    }

    /// <summary>
    /// Validates that <paramref name="timeout"/> is a positive, finite duration within the range
    /// supported by <see cref="CancellationTokenSource"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="timeout"/> is zero, negative, <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>,
    /// or exceeds <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> milliseconds.
    /// </exception>
    internal static void ValidateTimeout(TimeSpan timeout, string paramName)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName, timeout,
                "Timeout must be a positive duration. Zero and negative values are not supported.");
        }

        // CancellationTokenSource(TimeSpan) accepts up to int.MaxValue milliseconds (~49 days).
        if (timeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(paramName, timeout,
                $"Timeout exceeds the maximum supported duration ({int.MaxValue} ms ≈ 49 days).");
        }
    }

    /// <summary>
    /// Validates that <paramref name="interfaceType"/> can be marshaled across a process boundary,
    /// starts the isolated worker, and requests that it load <paramref name="dllPath"/> and emit the
    /// mapping class for the interface.
    /// </summary>
    /// <param name="interfaceType">Interface whose members will be mapped to native exports.</param>
    /// <param name="dllPath">Path to the native DLL to load inside the worker.</param>
    /// <param name="callingConvention">Calling convention used for the generated delegates.</param>
    /// <param name="loadTimeout">
    /// Maximum time to wait for the worker's response to the initial <see cref="WorkerRequestKind.Load"/>
    /// request (compiling the mapping class and loading the native DLL). Defaults to
    /// <see cref="DefaultLoadTimeout"/> when <see langword="null"/>.
    /// </param>
    /// <param name="callTimeout">
    /// Maximum time to wait for the worker's response to each subsequent
    /// <see cref="WorkerRequestKind.Call"/> request, applied by <see cref="InvokeMethod"/>. Defaults to
    /// <see cref="DefaultCallTimeout"/> when <see langword="null"/>.
    /// </param>
    /// <param name="handle">Handle allocated for <paramref name="interfaceType"/> on the new worker; pass to <see cref="InvokeMethod"/>.</param>
    /// <returns>A connected, loaded worker ready to receive <see cref="WorkerRequestKind.Call"/> requests.</returns>
    internal static EmitWorkerProcess Start(
        Type interfaceType, string dllPath, CallingConvention callingConvention,
        TimeSpan? loadTimeout, TimeSpan? callTimeout, out int handle,
        bool includeDiagnostics = false)
    {
        // Validated again inside LoadInterface (needed there for EmitWorkerPool, which calls it
        // directly against an already-running shared worker), but checked here too so an unsupported
        // interface fails immediately instead of paying for a full sandboxed process spawn/teardown
        // first.
        CrossProcessMarshaling.EnsureInterfaceIsSupported(interfaceType);

        if (string.IsNullOrEmpty(interfaceType.Assembly.Location))
        {
            throw new NotSupportedException(
                $"The assembly declaring '{interfaceType.FullName}' has no on-disk location (for example, " +
                $"a single-file publish or an in-memory assembly), so it cannot be loaded by an isolated " +
                $"Emit worker. Use LibraryMapper.EmitInProcess<T> instead.");
        }

        EmitWorkerProcess worker = Start(callTimeout, includeDiagnostics);
        try
        {
            handle = worker.LoadInterface(interfaceType, dllPath, callingConvention, loadTimeout ?? DefaultLoadTimeout);
        }
        catch
        {
            worker.Dispose();
            throw;
        }

        return worker;
    }

    /// <summary>
    /// Creates a minimal <see cref="EmitWorkerProcess"/> backed by caller-supplied streams, for unit
    /// testing only. The <paramref name="reader"/> is used by the internal reader loop, so passing a
    /// blocking stream keeps the loop alive while the test exercises <see cref="SendAndReceive"/>;
    /// disposing or completing the stream lets the loop exit cleanly.
    /// </summary>
    internal static EmitWorkerProcess CreateForTesting(
        System.IO.StreamReader reader,
        System.IO.StreamWriter writer,
        TimeSpan callTimeout,
        bool includeDiagnostics = false)
    {
        var fakeProcess = new Process();
        var fakePipe = new NamedPipeServerStream(
            $"Utils.Reflection.EmitWorker.UnitTest.{Guid.NewGuid():N}",
            PipeDirection.InOut, maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        return new EmitWorkerProcess(null, fakeProcess, fakePipe, reader, writer, callTimeout, includeDiagnostics);
    }

    /// <summary>
    /// Starts the isolated worker process and connects to it, without loading any interface yet. Used
    /// directly by <see cref="EmitWorkerPool"/> to share one worker across multiple
    /// <see cref="LoadInterface"/> calls; the single-interface <see cref="Start(Type, string, CallingConvention, TimeSpan?, TimeSpan?, out int)"/>
    /// overload calls this and then loads its one interface immediately.
    /// </summary>
    /// <param name="callTimeout">
    /// Maximum time to wait for the worker's response to each <see cref="WorkerRequestKind.Call"/>
    /// request, applied by <see cref="InvokeMethod"/>. Defaults to <see cref="DefaultCallTimeout"/> when
    /// <see langword="null"/>.
    /// </param>
    /// <returns>A connected worker, ready to receive <see cref="WorkerRequestKind.Load"/> requests.</returns>
    internal static EmitWorkerProcess Start(TimeSpan? callTimeout = null, bool includeDiagnostics = false)
    {
        if (callTimeout.HasValue)
            ValidateTimeout(callTimeout.Value, nameof(callTimeout));

        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Unable to determine the current process executable path, required to launch an isolated Emit worker.");

        ProcessContainerPermissions permissions = CreateWorkerPermissions();
        IProcessContainer? sandbox = ProcessContainerFactory.TryCreate(
            windowsContainerName: "Utils.Reflection.EmitWorker.v1",
            windowsDisplayName: "Utils.Reflection Emit Worker",
            windowsDescription: "Isolated process that compiles and executes dynamically emitted native DLL bindings.",
            permissions: permissions);

        string pipeName = $"Utils.Reflection.EmitWorker.{Guid.NewGuid():N}";
        NamedPipeServerStream serverPipe = CreateServerPipe(pipeName, sandbox);

        Process process;
        try
        {
            process = StartWorkerProcess(exePath, pipeName, ref sandbox);
        }
        catch
        {
            serverPipe.Dispose();
            sandbox?.Dispose();
            throw;
        }

        try
        {
            using CancellationTokenSource connectTimeout = new(TimeSpan.FromSeconds(10));
            serverPipe.WaitForConnectionAsync(connectTimeout.Token).GetAwaiter().GetResult();

            if (OperatingSystem.IsWindows() && !ProcessIsolationPlatformSecurity.IsExpectedNamedPipeClient(serverPipe, process.Id))
            {
                throw new InvalidOperationException("Security violation: unexpected process connected to the isolated Emit worker pipe.");
            }
        }
        catch
        {
            serverPipe.Dispose();
            KillSilently(process);
            sandbox?.Dispose();
            throw;
        }

        var reader = new System.IO.StreamReader(serverPipe, leaveOpen: true);
        var writer = new System.IO.StreamWriter(serverPipe, leaveOpen: true) { AutoFlush = true };

        return new EmitWorkerProcess(sandbox, process, serverPipe, reader, writer, callTimeout ?? DefaultCallTimeout, includeDiagnostics);
    }

    /// <summary>
    /// Requests that the worker load <paramref name="dllPath"/> and emit the mapping class for
    /// <paramref name="interfaceType"/>, allocating a new handle for it. A single worker (typically one
    /// obtained through <see cref="EmitWorkerPool"/>) can hold several independently loaded interfaces
    /// at once; each gets its own handle and is invoked/unloaded independently of the others.
    /// </summary>
    /// <param name="interfaceType">Interface whose members will be mapped to native exports.</param>
    /// <param name="dllPath">Path to the native DLL to load inside the worker.</param>
    /// <param name="callingConvention">Calling convention used for the generated delegates.</param>
    /// <param name="timeout">Maximum time to wait for the worker's response.</param>
    /// <returns>The handle allocated for this interface on this worker.</returns>
    internal int LoadInterface(Type interfaceType, string dllPath, CallingConvention callingConvention, TimeSpan timeout)
    {
        ValidateTimeout(timeout, nameof(timeout));
        CrossProcessMarshaling.EnsureInterfaceIsSupported(interfaceType);

        if (string.IsNullOrEmpty(interfaceType.Assembly.Location))
        {
            throw new NotSupportedException(
                $"The assembly declaring '{interfaceType.FullName}' has no on-disk location (for example, " +
                $"a single-file publish or an in-memory assembly), so it cannot be loaded by an isolated " +
                $"Emit worker. Use LibraryMapper.EmitInProcess<T> instead.");
        }

        var request = new WorkerRequest
        {
            Id = Interlocked.Increment(ref nextId),
            Kind = WorkerRequestKind.Load,
            InterfaceAssemblyPath = interfaceType.Assembly.Location,
            InterfaceTypeFullName = interfaceType.FullName,
            DllPath = dllPath,
            CallingConvention = callingConvention,
        };

        WorkerResponse response = SendAndReceive(request, timeout);
        ThrowIfFailed(response);

        // Build the reverse command table on the host side using the same deterministic ordering
        // as the worker (CrossProcessMarshaling.BuildCommandTable). Both sides produce an identical
        // array from the same interface type, so commandId == Array.IndexOf(table, method).
        MethodInfo[] commandTable = CrossProcessMarshaling.BuildCommandTable(interfaceType);
        FrozenDictionary<MethodInfo, int> reverseTable = commandTable
            .Select((m, i) => (m, i))
            .ToFrozenDictionary(t => t.m, t => t.i);
        commandTables[response.Handle] = reverseTable;

        return response.Handle;
    }

    /// <summary>
    /// Releases the native mapping instance identified by <paramref name="handle"/> on the worker
    /// (disposing it, unloading its native DLL), without shutting down the worker process itself — the
    /// worker may still hold other interfaces loaded through <see cref="EmitWorkerPool"/>. Best-effort:
    /// swallows failures, since there is nothing more the caller can do with a handle it is done with
    /// either way.
    /// </summary>
    /// <param name="handle">Handle previously returned by <see cref="LoadInterface"/>.</param>
    internal void UnloadInterface(int handle)
    {
        if (disposed)
        {
            return;
        }

        commandTables.TryRemove(handle, out _);
        var request = new WorkerRequest { Id = Interlocked.Increment(ref nextId), Kind = WorkerRequestKind.Unload, Handle = handle };

        try
        {
            SendAndReceive(request, callTimeout);
        }
        catch
        {
            // Best-effort: a failed/timed-out Unload leaves the worker holding a now-unreferenced
            // instance until the worker itself is eventually disposed; not ideal, but not observable
            // by the caller, who has already discarded the handle either way.
        }
    }

    /// <summary>
    /// Serializes <paramref name="args"/>, forwards the call to the worker for the interface instance
    /// identified by <paramref name="handle"/>, applies any by-ref/out results back into
    /// <paramref name="args"/>, and returns the deserialized return value.
    /// </summary>
    /// <remarks>
    /// Safe to call concurrently from multiple threads, including for different <paramref name="handle"/>
    /// values on a worker shared through <see cref="EmitWorkerPool"/>: each call gets its own
    /// <see cref="WorkerRequest.Id"/> and is independently correlated to its response by
    /// <see cref="RunReaderLoop"/>, and the worker dispatches each request it receives to the thread pool
    /// rather than serializing them (see <see cref="EmitWorkerHost.Run"/>). Concurrent calls that target
    /// the very same <paramref name="handle"/> race exactly as concurrent calls into the same native
    /// library would in-process — safe only if that library itself tolerates concurrent calls.
    /// </remarks>
    /// <param name="handle">Handle of the target interface instance, as returned by <see cref="LoadInterface"/>.</param>
    /// <param name="method">Interface method being invoked, resolved by the caller's <see cref="System.Reflection.DispatchProxy"/>.</param>
    /// <param name="args">Argument values, in parameter order; by-ref/out slots are updated in place.</param>
    /// <returns>The method's return value, or <see langword="null"/> for <see langword="void"/> methods.</returns>
    internal object? InvokeMethod(int handle, MethodInfo method, object?[] args)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        ParameterInfo[] parameters = method.GetParameters();
        string?[] argumentsJson = new string?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            Type effectiveType = parameterType.IsByRef ? parameterType.GetElementType()! : parameterType;
            argumentsJson[i] = JsonSerializer.Serialize(args[i], effectiveType, CrossProcessMarshaling.JsonOptions);
        }

        if (!commandTables.TryGetValue(handle, out FrozenDictionary<MethodInfo, int>? reverseTable))
        {
            throw new InvalidOperationException(
                $"No command table found for handle {handle}. " +
                "The interface may not have been loaded on this worker, or may have already been unloaded.");
        }

        if (!reverseTable.TryGetValue(method, out int commandId))
        {
            throw new InvalidOperationException(
                $"Method '{method.DeclaringType?.FullName}.{method.Name}' is not in the command table for handle {handle}. " +
                "Only methods declared by the loaded interface contract may be invoked.");
        }

        var request = new WorkerRequest
        {
            Id = Interlocked.Increment(ref nextId),
            Kind = WorkerRequestKind.Call,
            Handle = handle,
            MethodCommandId = commandId,
            ArgumentsJson = argumentsJson,
        };

        WorkerResponse response = SendAndReceive(request, callTimeout);
        ThrowIfFailed(response, method.Name);

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType.IsByRef)
            {
                Type effectiveType = parameters[i].ParameterType.GetElementType()!;
                string? valueJson = response.ByRefValuesJson is { } values && i < values.Length ? values[i] : null;
                args[i] = valueJson is null ? null : JsonSerializer.Deserialize(valueJson, effectiveType, CrossProcessMarshaling.JsonOptions);
            }
        }

        if (method.ReturnType == typeof(void) || response.ReturnValueJson is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize(response.ReturnValueJson, method.ReturnType, CrossProcessMarshaling.JsonOptions);
    }

    private void ThrowIfFailed(WorkerResponse response, string? methodName = null)
    {
        if (!response.Success)
        {
            string context = methodName is null ? "the Load request" : $"'{methodName}'";
            throw new EmitWorkerInvocationException(
                $"The isolated Emit worker reported an error while handling {context}: {response.ErrorMessage}",
                includeDiagnostics ? response.ErrorTypeName : null,
                includeDiagnostics ? response.ErrorStackTrace : null);
        }
    }

    /// <summary>
    /// Writes <paramref name="request"/> and waits for its matching response, up to
    /// <paramref name="timeout"/>, without blocking any other concurrently in-flight request on this
    /// worker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers a <see cref="TaskCompletionSource{TResult}"/> under <see cref="WorkerRequest.Id"/>
    /// before writing the request line (guarded by <see cref="writeLock"/> only for the duration of the
    /// write itself, to avoid interleaving concurrent writers on the shared pipe — never held while
    /// waiting for the response). <see cref="RunReaderLoop"/> completes it once the matching response
    /// line arrives.
    /// </para>
    /// <para>
    /// On timeout, only <em>this</em> request's <see cref="TaskCompletionSource{TResult}"/> is
    /// abandoned; the worker process and every other in-flight request are left untouched. This is safe
    /// specifically because responses are correlated by id — a late response for the timed-out request
    /// simply finds no matching pending entry once it eventually arrives and is silently dropped, rather
    /// than being misread as the response to a different, still-pending request. (Before requests were
    /// individually correlated, any single timeout had to assume the worst and kill the whole worker; see
    /// the superseded <c>PoisonAfterTimeout</c> note in <c>Utils.Reflection/TODO.md</c> item 28/34.)
    /// </para>
    /// <para>
    /// The worker-side native call behind a timed-out request keeps running to completion on its own
    /// thread-pool thread inside the worker (see <see cref="EmitWorkerHost.Run"/>) — abandoned, not
    /// cancelled — until it finishes and its answer is dropped for lack of a listener. The outcome is
    /// therefore <em>indeterminate</em>: side effects may or may not have occurred. Each abandoned call
    /// increments <see cref="abandonedCallCount"/>; when that counter reaches <see cref="MaxAbandonedCalls"/>
    /// the worker is retired (<see cref="connectionFault"/> is set and all in-flight requests are failed)
    /// so that callers do not continue sending work to a process whose cumulative state has become
    /// unknowable.
    /// </para>
    /// </remarks>
    /// <param name="request">Request to send.</param>
    /// <param name="timeout">Maximum time to wait for the response.</param>
    /// <returns>The deserialized response.</returns>
    /// <exception cref="TimeoutException">Thrown when the worker does not respond within <paramref name="timeout"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the worker's connection has already failed.</exception>
    private WorkerResponse SendAndReceive(WorkerRequest request, TimeSpan timeout)
    {
        if (connectionFault is { } fault)
        {
            throw new InvalidOperationException("The isolated Emit worker's connection has already failed.", fault);
        }

        var completion = new TaskCompletionSource<WorkerResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(request.Id, completion))
        {
            throw new InvalidOperationException($"An in-flight request with id {request.Id} already exists.");
        }

        // Write is done after the pending entry is registered (so the reader loop can complete it),
        // but inside a try/catch: if serialization or the pipe write fails the entry is removed
        // immediately so the timeout callback cannot later classify this unsent request as an
        // abandoned worker call and unfairly increment the retirement counter.
        try
        {
            lock (writeLock)
            {
                writer.WriteLine(JsonSerializer.Serialize(request));
            }
        }
        catch (Exception writeEx)
        {
            pending.TryRemove(request.Id, out _);
            throw new InvalidOperationException(
                $"Failed to write a '{request.Kind}' request to the isolated Emit worker; " +
                "the connection may have been closed or broken.", writeEx);
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using CancellationTokenRegistration registration = timeoutSource.Token.Register(() =>
        {
            if (pending.TryRemove(request.Id, out TaskCompletionSource<WorkerResponse>? timedOut))
            {
                timedOut.TrySetCanceled(timeoutSource.Token);

                // The native call is still running inside the worker — its result is indeterminate.
                // After MaxAbandonedCalls, retire the worker so callers do not continue sending work
                // to a process whose accumulated side-effect state has become unknowable.
                int count = Interlocked.Increment(ref abandonedCallCount);
                if (count >= MaxAbandonedCalls && connectionFault is null)
                {
                    var retirementFault = new InvalidOperationException(
                        $"The isolated Emit worker has been retired after {count} abandoned calls " +
                        "(requests that timed out while the worker was still processing them). " +
                        "The outcome of each abandoned call is indeterminate.");

                    connectionFault = retirementFault;
                    FailAllPending(retirementFault);
                }
            }
        });

        try
        {
            return completion.Task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"The isolated Emit worker did not respond to a '{request.Kind}' request within {timeout}.");
        }
    }

    /// <summary>
    /// Continuously reads response lines for the lifetime of the connection and completes the matching
    /// <see cref="TaskCompletionSource{TResult}"/> registered by <see cref="SendAndReceive"/>, so several
    /// requests can be in flight at once instead of one at a time.
    /// </summary>
    /// <remarks>
    /// When the loop ends — the worker closed the connection (<c>ReadBoundedLine</c> returns <see langword="null"/>)
    /// or the read faulted (broken pipe, disposed stream, line-length limit exceeded) — every still-pending request fails with that
    /// cause via <see cref="FailAllPending"/>, and, unless this was already a deliberate <see cref="Dispose"/>
    /// (which handles the worker process itself), the worker process is killed so it cannot linger as an
    /// unreachable orphan.
    /// </remarks>
    private void RunReaderLoop()
    {
        Exception fault;
        try
        {
            string? line;
            while ((line = ProtocolFraming.ReadBoundedLine(reader)) is not null)
            {
                WorkerResponse? response;
                try
                {
                    response = JsonSerializer.Deserialize<WorkerResponse>(line);
                }
                catch (JsonException ex)
                {
                    // A malformed response line indicates framing corruption, which breaks the
                    // correlated request/response protocol. Treat it as fatal so the caller learns
                    // immediately rather than waiting for an individual per-request timeout.
                    throw new InvalidOperationException(
                        "The isolated Emit worker sent a response line that could not be deserialized. " +
                        "The connection is now unusable.", ex);
                }

                if (response is not null && pending.TryRemove(response.Id, out TaskCompletionSource<WorkerResponse>? completion))
                {
                    completion.TrySetResult(response);
                }
            }

            fault = new InvalidOperationException("The isolated Emit worker closed the connection unexpectedly.");
        }
        catch (Exception ex)
        {
            fault = ex;
        }

        connectionFault = fault;
        FailAllPending(fault);

        if (!disposed)
        {
            KillSilently(workerProcess);
            sandbox?.Dispose();
        }
    }

    private void FailAllPending(Exception fault)
    {
        foreach (long id in pending.Keys)
        {
            if (pending.TryRemove(id, out TaskCompletionSource<WorkerResponse>? completion))
            {
                completion.TrySetException(fault);
            }
        }
    }

    /// <summary>
    /// Creates the named pipe server. When a sandbox exposing a security identifier is available, a
    /// <see cref="PipeSecurity"/> ACL restricts connections to that identifier plus the current user.
    /// </summary>
    private static NamedPipeServerStream CreateServerPipe(string pipeName, IProcessContainer? sandbox)
    {
        if (!OperatingSystem.IsWindows() ||
            sandbox is null ||
            !sandbox.TryGetSecurityIdentifier(out SecurityIdentifier? securityIdentifier) ||
            securityIdentifier is null)
        {
            return new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        return CreateSecuredServerPipe(pipeName, securityIdentifier);
    }

    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreateSecuredServerPipe(string pipeName, SecurityIdentifier securityIdentifier)
    {
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            securityIdentifier, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
            inBufferSize: 0, outBufferSize: 0, pipeSecurity);
    }

    /// <summary>
    /// Starts the worker process inside the sandbox when one is available (returned by
    /// <see cref="ProcessContainerFactory.TryCreate"/>), or as a plain child process when no sandbox
    /// could be created for the current platform/configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a sandbox <em>was</em> created but its <see cref="IProcessContainer.StartProcess"/> call
    /// fails, the method now throws rather than falling back silently to an unsandboxed process.
    /// Previously the exception was swallowed and the worker was relaunched without isolation, giving
    /// the caller no indication that the security boundary had been crossed.
    /// </para>
    /// <para>
    /// When <see cref="ProcessContainerFactory.TryCreate"/> returns <see langword="null"/> (no sandbox
    /// is available on the current platform or configuration), the worker is started without isolation
    /// from the start. This path does not represent a failure — it is the expected behaviour on
    /// platforms where no process container is implemented.
    /// </para>
    /// </remarks>
    private static Process StartWorkerProcess(string exePath, string pipeName, ref IProcessContainer? sandbox)
    {
        string[] arguments = BuildWorkerArguments(exePath, pipeName);

        if (sandbox is not null)
        {
            // A sandbox was successfully created. Any failure to launch the process inside it is
            // treated as fatal: falling back silently would let an arbitrarily-permissioned process
            // run as if it were sandboxed, defeating the isolation contract.
            return sandbox.StartProcess(exePath, arguments);
        }

        // No sandbox available for this platform/configuration — start as a plain child process.
        // This is expected on platforms without an implemented process container (e.g. Linux when
        // bwrap is not installed); it is not a fallback from a sandbox failure.
        var psi = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        SandboxedProcessEnvironment.ApplyMinimalEnvironment(psi);

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start the isolated Emit worker process.");
    }

    /// <summary>
    /// Builds the argument list passed to <paramref name="exePath"/> to make it enter
    /// <see cref="LibraryMapper.RunWorkerIfRequested"/>.
    /// </summary>
    /// <remarks>
    /// When the host process is running under a generic launcher — most commonly the <c>dotnet</c>
    /// muxer (<c>dotnet MyApp.dll</c>, or an <c>UseAppHost=false</c> deployment) —
    /// <see cref="Environment.ProcessPath"/> resolves to the launcher executable, not the managed
    /// entry assembly. Re-launching that path with just <c>[marker, pipeName]</c> makes the launcher
    /// try to parse the marker as its own first argument (e.g. <c>dotnet
    /// --utils-reflection-emit-worker</c>) instead of forwarding it to the managed <c>Main</c>, so the
    /// worker never starts. Detected by comparing the launcher's file name against
    /// <see cref="Assembly.GetEntryAssembly"/>'s location: when they differ, the managed assembly path
    /// is inserted before the marker (<c>dotnet MyApp.dll --utils-reflection-emit-worker &lt;pipe&gt;</c>),
    /// matching how <c>dotnet</c> itself is invoked to run a framework-dependent deployment.
    /// </remarks>
    /// <param name="exePath">Executable that will be relaunched (<see cref="Environment.ProcessPath"/>).</param>
    /// <param name="pipeName">Name of the named pipe the worker should connect back to.</param>
    /// <returns>The complete, ordered argument list for the relaunched process.</returns>
    internal static string[] BuildWorkerArguments(string exePath, string pipeName)
    {
        string? entryAssemblyLocation = Assembly.GetEntryAssembly()?.Location;

        if (!string.IsNullOrEmpty(entryAssemblyLocation) &&
            !string.Equals(
                System.IO.Path.GetFileNameWithoutExtension(exePath),
                System.IO.Path.GetFileNameWithoutExtension(entryAssemblyLocation),
                StringComparison.OrdinalIgnoreCase))
        {
            return [entryAssemblyLocation, LibraryMapper.WorkerArgumentMarker, pipeName];
        }

        return [LibraryMapper.WorkerArgumentMarker, pipeName];
    }

    /// <summary>
    /// Builds the permission set requested for the isolated Emit worker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ProcessContainerPermissions.AllowDiskWrite"/> is granted on Linux/macOS only, even
    /// though the worker itself has no legitimate need to write files: on those platforms the
    /// host/worker named pipe is backed by a Unix domain socket file under the OS temp directory
    /// (<see cref="System.IO.Pipes.NamedPipeServerStream"/>'s Unix implementation). Without this flag,
    /// <see cref="ProcessIsolation.LinuxBubblewrapContainer"/> mounts a fresh, empty <c>tmpfs</c> over
    /// <c>/tmp</c> and <see cref="ProcessIsolation.MacOsSandboxExecContainer"/> denies
    /// <c>file-write*</c>, so the sandboxed worker can never see or connect to the socket and
    /// <see cref="Start(TimeSpan?)"/> always fails with a connection timeout. Broader than a single-socket bind
    /// would be, but scoping the sandbox to that one path would require extending
    /// <see cref="IProcessContainer"/> beyond what can be validated without a real Linux/macOS
    /// environment — see the equivalent trade-off already documented for
    /// <see cref="ProcessIsolation.LinuxBubblewrapContainer"/>'s and
    /// <see cref="ProcessIsolation.MacOsSandboxExecContainer"/>'s read posture.
    /// </para>
    /// <para>
    /// <b>Must stay <see langword="false"/> on Windows.</b> Windows named pipes are kernel objects
    /// outside the filesystem, so the flag brings no benefit there — and
    /// <see cref="ProcessIsolation.ProcessContainerFactory.TryCreate"/> treats
    /// <see cref="ProcessContainerPermissions.AllowDiskWrite"/> as a request for broader-than-restrictive
    /// permissions and skips AppContainer creation entirely when it is set, which would silently
    /// disable sandboxing for the worker instead of merely being a no-op.
    /// </para>
    /// </remarks>
    /// <returns>The permission set to request for the isolated Emit worker.</returns>
    internal static ProcessContainerPermissions CreateWorkerPermissions() =>
        new() { AllowDiskRead = true, AllowDiskWrite = !OperatingSystem.IsWindows() };

    private static void KillSilently(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // Best-effort; the Job Object's KillOnJobClose (Windows) covers the rest.
        }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            if (!workerProcess.HasExited)
            {
                var shutdown = new WorkerRequest { Id = Interlocked.Increment(ref nextId), Kind = WorkerRequestKind.Shutdown };
                try
                {
                    SendAndReceive(shutdown, ShutdownTimeout);
                }
                catch
                {
                    // Best-effort graceful shutdown; the process is killed below regardless.
                }
            }
        }
        catch
        {
            // Ignore — workerProcess.HasExited itself can throw if the process handle is unusable.
        }

        reader.Dispose();
        writer.Dispose();
        pipe.Dispose();
        KillSilently(workerProcess);

        sandbox?.Dispose();

        // Best-effort: give the background reader loop a moment to observe the disposed reader and
        // exit cleanly. Not awaited indefinitely — Dispose must not block on a stuck reader loop.
        try
        {
            readerLoop.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Best-effort; the loop will still exit on its own once the disposed reader faults its
            // in-flight read, even if this Dispose call doesn't wait around to see it.
        }
    }
}
