using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
internal sealed class EmitWorkerProcess : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Current wire protocol version, matched against <see cref="EmitWorkerHost.ProtocolVersion"/> during
    /// the initial <see cref="WorkerRequestKind.Hello"/> handshake. Both constants must equal the same value
    /// in any compatible pair of host and worker binaries.
    /// </summary>
    internal const int ProtocolVersion = EmitWorkerHost.ProtocolVersion;

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
    /// Maximum number of timed-out request IDs retained in <see cref="recentlyTimedOutIds"/>, used
    /// to distinguish expected late responses from truly unsolicited or duplicate responses.
    /// </summary>
    private const int MaxTrackedTimedOutIds = 1024;

    /// <summary>
    /// Timeout for the initial <see cref="WorkerRequestKind.Hello"/> handshake performed immediately
    /// after connecting to the worker. Short and non-configurable: the worker must already be running
    /// and waiting before we connect, so the round-trip should be near-instantaneous.
    /// </summary>
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);

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
    /// Maps each loaded handle to the host-side method-ID table built from the Load response's
    /// <see cref="WorkerResponse.MethodDescriptors"/>. Entries are added by <see cref="LoadInterface"/>
    /// and removed by <see cref="UnloadInterface"/> so lookups in <see cref="InvokeMethod"/> always
    /// find the table for any handle that was successfully loaded.
    /// </summary>
    private readonly ConcurrentDictionary<int, IReadOnlyDictionary<MethodInfo, int>> methodIdTableByHandle = new();

    /// <summary>
    /// Bounded set of request IDs that timed out while still pending in the worker. Used by
    /// <see cref="RunReaderLoop"/> to distinguish expected late responses (safe to drop) from
    /// unsolicited or duplicate responses that indicate a protocol violation.
    /// </summary>
    private readonly ConcurrentQueue<long> recentlyTimedOutIds = new();
    private int timedOutIdCount;

    private readonly Task readerLoop;
    private readonly TimeSpan callTimeout;
    private long nextId;
    private int abandonedCallCount;
    private volatile Exception? connectionFault;
    private bool disposed;

    private EmitWorkerProcess(
        IProcessContainer? sandbox,
        Process workerProcess,
        NamedPipeServerStream pipe,
        System.IO.StreamReader reader,
        System.IO.StreamWriter writer,
        TimeSpan callTimeout)
    {
        this.sandbox = sandbox;
        this.workerProcess = workerProcess;
        this.pipe = pipe;
        this.reader = reader;
        this.writer = writer;
        this.callTimeout = callTimeout;
        readerLoop = Task.Run(RunReaderLoop);
    }

    /// <summary>
    /// <see langword="true"/> when this worker is usable for new requests: not disposed and not
    /// retired or faulted (i.e. <see cref="connectionFault"/> is <see langword="null"/>).
    /// <see langword="false"/> means the worker should be replaced by the caller (e.g.
    /// <see cref="EmitWorkerPool"/>) before new interfaces are loaded.
    /// </summary>
    internal bool IsHealthy => connectionFault is null && !disposed;

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
    /// <see cref="DefaultLoadTimeout"/> when <see langword="null"/>. Must be a positive finite duration.
    /// </param>
    /// <param name="callTimeout">
    /// Maximum time to wait for the worker's response to each subsequent
    /// <see cref="WorkerRequestKind.Call"/> request, applied by <see cref="InvokeMethod"/>. Defaults to
    /// <see cref="DefaultCallTimeout"/> when <see langword="null"/>. Must be a positive finite duration.
    /// </param>
    /// <param name="handle">Handle allocated for <paramref name="interfaceType"/> on the new worker; pass to <see cref="InvokeMethod"/>.</param>
    /// <returns>A connected, loaded worker ready to receive <see cref="WorkerRequestKind.Call"/> requests.</returns>
    internal static EmitWorkerProcess Start(
        Type interfaceType, string dllPath, CallingConvention callingConvention,
        TimeSpan? loadTimeout, TimeSpan? callTimeout, out int handle)
    {
        // Validated again inside LoadInterface (needed there for EmitWorkerPool, which calls it
        // directly against an already-running shared worker), but checked here too so an unsupported
        // interface fails immediately instead of paying for a full sandboxed process spawn/teardown.
        CrossProcessMarshaling.EnsureInterfaceIsSupported(interfaceType);

        if (string.IsNullOrEmpty(interfaceType.Assembly.Location))
        {
            throw new NotSupportedException(
                $"The assembly declaring '{interfaceType.FullName}' has no on-disk location (for example, " +
                $"a single-file publish or an in-memory assembly), so it cannot be loaded by an isolated " +
                $"Emit worker. Use LibraryMapper.EmitInProcess<T> instead.");
        }

        EmitWorkerProcess worker = Start(callTimeout);
        try
        {
            handle = worker.LoadInterface(
                interfaceType, dllPath, callingConvention,
                ValidateTimeout(loadTimeout, DefaultLoadTimeout, nameof(loadTimeout)));
        }
        catch
        {
            worker.Dispose();
            throw;
        }

        return worker;
    }

    /// <summary>
    /// Starts the isolated worker process and connects to it, without loading any interface yet.
    /// Used directly by <see cref="EmitWorkerPool"/> to share one worker across multiple
    /// <see cref="LoadInterface"/> calls.
    /// </summary>
    /// <param name="callTimeout">
    /// Maximum time to wait for the worker's response to each <see cref="WorkerRequestKind.Call"/>
    /// request, applied by <see cref="InvokeMethod"/>. Defaults to <see cref="DefaultCallTimeout"/> when
    /// <see langword="null"/>. Must be a positive finite duration.
    /// </param>
    /// <returns>A connected worker, ready to receive <see cref="WorkerRequestKind.Load"/> requests.</returns>
    internal static EmitWorkerProcess Start(TimeSpan? callTimeout = null)
    {
        TimeSpan validatedCallTimeout = ValidateTimeout(callTimeout, DefaultCallTimeout, nameof(callTimeout));

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

        var readerStream = new System.IO.StreamReader(serverPipe, leaveOpen: true);
        var writerStream = new System.IO.StreamWriter(serverPipe, leaveOpen: true) { AutoFlush = true };

        var worker = new EmitWorkerProcess(sandbox, process, serverPipe, readerStream, writerStream, validatedCallTimeout);
        try
        {
            worker.Handshake();
        }
        catch
        {
            worker.Dispose();
            throw;
        }

        return worker;
    }

    /// <summary>
    /// Sends a <see cref="WorkerRequestKind.Hello"/> request and validates that the worker's protocol
    /// version matches <see cref="ProtocolVersion"/>. Called once per connection, immediately after the
    /// pipe is established, so version mismatches are caught before any <see cref="LoadInterface"/> call.
    /// </summary>
    private void Handshake()
    {
        var request = new WorkerRequest
        {
            Id = Interlocked.Increment(ref nextId),
            Kind = WorkerRequestKind.Hello,
            ProtocolVersion = ProtocolVersion,
        };

        WorkerResponse response = SendAndReceive(request, HandshakeTimeout);

        if (!response.Success)
        {
            throw new InvalidOperationException(
                $"Emit worker rejected the protocol handshake: {response.ErrorMessage}");
        }

        if (response.ProtocolVersion != ProtocolVersion)
        {
            throw new InvalidOperationException(
                $"Emit worker returned protocol version {response.ProtocolVersion}; " +
                $"host requires version {ProtocolVersion}. " +
                "Ensure both the host process and the worker executable are from the same build.");
        }
    }

    /// <summary>
    /// Validates that a timeout is a positive finite duration within the range supported by
    /// <see cref="CancellationTokenSource"/>. Returns the effective timeout (the provided value
    /// or <paramref name="defaultValue"/> when <see langword="null"/>).
    /// </summary>
    /// <param name="timeout">Candidate timeout; null means use <paramref name="defaultValue"/>.</param>
    /// <param name="defaultValue">Default applied when <paramref name="timeout"/> is null.</param>
    /// <param name="parameterName">Parameter name for the exception message.</param>
    /// <returns>The validated effective timeout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the resolved timeout is zero, negative, or exceeds the maximum value that
    /// <see cref="CancellationTokenSource"/> can accept (~24.9 days).
    /// </exception>
    internal static TimeSpan ValidateTimeout(TimeSpan? timeout, TimeSpan defaultValue, string parameterName)
    {
        TimeSpan effective = timeout ?? defaultValue;

        if (effective <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                effective,
                "Timeout must be a positive finite duration.");
        }

        // CancellationTokenSource accepts up to int.MaxValue milliseconds.
        if (effective > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                effective,
                $"Timeout must not exceed {TimeSpan.FromMilliseconds(int.MaxValue)}. " +
                "Use a shorter positive timeout.");
        }

        return effective;
    }

    /// <summary>
    /// Requests that the worker load <paramref name="dllPath"/> and emit the mapping class for
    /// <paramref name="interfaceType"/>, then builds the host-side method-ID table from the
    /// worker's Load response.
    /// </summary>
    /// <param name="interfaceType">Interface whose members will be mapped to native exports.</param>
    /// <param name="dllPath">Path to the native DLL to load inside the worker.</param>
    /// <param name="callingConvention">Calling convention used for the generated delegates.</param>
    /// <param name="timeout">Maximum time to wait for the worker's response.</param>
    /// <returns>The handle allocated for this interface on this worker.</returns>
    internal int LoadInterface(Type interfaceType, string dllPath, CallingConvention callingConvention, TimeSpan timeout)
        => LoadInterfaceAsync(interfaceType, dllPath, callingConvention, timeout).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronous variant of <see cref="LoadInterface"/>. Requests that the worker load
    /// <paramref name="dllPath"/> and emit the mapping class for <paramref name="interfaceType"/>,
    /// then builds the host-side method-ID table from the worker's Load response.
    /// </summary>
    internal async Task<int> LoadInterfaceAsync(
        Type interfaceType, string dllPath, CallingConvention callingConvention,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
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

        WorkerResponse response;
        try
        {
            response = await SendAndReceiveAsync(request, timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The Load request was written to the pipe before cancellation fired. The worker may
            // have completed the Load and allocated a handle that we can never observe or unload.
            // Retire this worker immediately to prevent resource leaks from the orphaned handle.
            RetireAfterOrphanedLoad();
            throw;
        }
        catch (TimeoutException)
        {
            // Same as cancellation: the frame was sent, the worker may have created a handle.
            RetireAfterOrphanedLoad();
            throw;
        }

        ThrowIfFailed(response);

        // Build the host-side MethodInfo→ID mapping from the worker-assigned descriptor table.
        if (response.MethodDescriptors is { } descriptors)
        {
            IReadOnlyDictionary<MethodInfo, int> methodIdTable =
                BuildMethodIdTable(interfaceType, descriptors);
            methodIdTableByHandle[response.Handle] = methodIdTable;
        }

        return response.Handle;
    }

    /// <summary>
    /// Releases the native mapping instance identified by <paramref name="handle"/> on the worker
    /// (disposing it, unloading its native DLL), without shutting down the worker process itself.
    /// Best-effort: swallows failures, since there is nothing more the caller can do with a handle
    /// it is done with either way.
    /// </summary>
    /// <param name="handle">Handle previously returned by <see cref="LoadInterface"/>.</param>
    internal void UnloadInterface(int handle)
    {
        if (disposed)
            return;

        methodIdTableByHandle.TryRemove(handle, out _);

        var request = new WorkerRequest
        {
            Id = Interlocked.Increment(ref nextId),
            Kind = WorkerRequestKind.Unload,
            Handle = handle,
        };

        try
        {
            SendAndReceive(request, callTimeout);
        }
        catch
        {
            // Best-effort: a failed/timed-out Unload leaves the worker holding a now-unreferenced
            // instance until the worker itself is eventually disposed.
        }
    }

    /// <summary>
    /// Serializes <paramref name="args"/>, looks up the worker-assigned method ID from the per-handle
    /// table, forwards the call to the worker, applies any by-ref/out results back into
    /// <paramref name="args"/>, and returns the deserialized return value.
    /// </summary>
    /// <remarks>
    /// The method ID sent to the worker is a load-time assigned private integer, not the CLR metadata
    /// token. Metadata tokens are module-scoped and can collide when methods are inherited from another
    /// assembly; private IDs are assigned during <see cref="LoadInterface"/> and are unique within one
    /// loaded handle for its entire lifetime.
    /// </remarks>
    /// <param name="handle">Handle of the target interface instance, as returned by <see cref="LoadInterface"/>.</param>
    /// <param name="method">Interface method being invoked, resolved by the caller's <see cref="System.Reflection.DispatchProxy"/>.</param>
    /// <param name="args">Argument values, in parameter order; by-ref/out slots are updated in place.</param>
    /// <returns>The method's return value, or <see langword="null"/> for <see langword="void"/> methods.</returns>
    internal object? InvokeMethod(int handle, MethodInfo method, object?[] args)
        => InvokeMethodAsync(handle, method, args).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronous variant of <see cref="InvokeMethod"/>. Serializes <paramref name="args"/>, sends
    /// the Call request, and awaits the worker's response without blocking a thread.
    /// By-ref and out parameter values are written back into <paramref name="args"/> before returning.
    /// </summary>
    /// <param name="handle">Handle of the target interface instance, as returned by <see cref="LoadInterfaceAsync"/>.</param>
    /// <param name="method">Interface method being invoked.</param>
    /// <param name="args">Argument values in parameter order; by-ref/out slots are updated in place.</param>
    /// <param name="cancellationToken">Token to cancel the wait before <see cref="callTimeout"/> expires.</param>
    /// <returns>The method's return value, or <see langword="null"/> for <see langword="void"/> methods.</returns>
    internal async Task<object?> InvokeMethodAsync(
        int handle, MethodInfo method, object?[] args,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (!methodIdTableByHandle.TryGetValue(handle, out IReadOnlyDictionary<MethodInfo, int>? methodIdTable))
        {
            throw new InvalidOperationException(
                $"No method-ID table found for handle {handle}. " +
                "The interface may not have been loaded, or may have already been unloaded.");
        }

        if (!methodIdTable.TryGetValue(method, out int methodId))
        {
            throw new InvalidOperationException(
                $"Method '{method.DeclaringType?.FullName}.{method.Name}' is not in the method-ID " +
                $"table for handle {handle}. Only methods declared by the originally loaded interface " +
                "contract may be invoked.");
        }

        ParameterInfo[] parameters = method.GetParameters();
        string?[] argumentsJson = new string?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            Type effectiveType = parameterType.IsByRef ? parameterType.GetElementType()! : parameterType;
            argumentsJson[i] = JsonSerializer.Serialize(args[i], effectiveType, CrossProcessMarshaling.JsonOptions);
        }

        var request = new WorkerRequest
        {
            Id = Interlocked.Increment(ref nextId),
            Kind = WorkerRequestKind.Call,
            Handle = handle,
            MethodId = methodId,
            ArgumentsJson = argumentsJson,
        };

        WorkerResponse response = await SendAndReceiveAsync(request, callTimeout, cancellationToken).ConfigureAwait(false);
        ThrowIfFailed(response, method.Name);

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType.IsByRef)
            {
                Type effectiveType = parameters[i].ParameterType.GetElementType()!;
                string? valueJson = response.ByRefValuesJson is { } values && i < values.Length ? values[i] : null;
                args[i] = valueJson is null
                    ? null
                    : JsonSerializer.Deserialize(valueJson, effectiveType, CrossProcessMarshaling.JsonOptions);
            }
        }

        if (method.ReturnType == typeof(void) || response.ReturnValueJson is null)
            return null;

        return JsonSerializer.Deserialize(response.ReturnValueJson, method.ReturnType, CrossProcessMarshaling.JsonOptions);
    }

    private static void ThrowIfFailed(WorkerResponse response, string? methodName = null)
    {
        if (!response.Success)
        {
            string context = methodName is null ? "the Load request" : $"'{methodName}'";
            throw new EmitWorkerInvocationException(
                $"The isolated Emit worker reported an error while handling {context}: {response.ErrorMessage}",
                response.ErrorTypeName,
                remoteStackTrace: null); // Stack trace omitted by default (worker sanitizes by item 9)
        }
    }

    /// <summary>
    /// Builds the host-side <see cref="MethodInfo"/>→method-ID table by matching each
    /// <see cref="MethodDescriptorDto"/> returned by the worker to a local <see cref="MethodInfo"/>
    /// on <paramref name="interfaceType"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a descriptor cannot be matched to a local method, which indicates that the
    /// interface definition differs between the host and worker assemblies.
    /// </exception>
    private static IReadOnlyDictionary<MethodInfo, int> BuildMethodIdTable(
        Type interfaceType,
        MethodDescriptorDto[] descriptors)
    {
        MethodInfo[] localMethods = interfaceType.GetMethods();
        var table = new Dictionary<MethodInfo, int>(descriptors.Length);
        var seenMethodIds = new HashSet<int>(descriptors.Length);

        foreach (MethodDescriptorDto descriptor in descriptors)
        {
            if (!seenMethodIds.Add(descriptor.MethodId))
            {
                throw new InvalidOperationException(
                    $"The worker returned two descriptors with the same method ID {descriptor.MethodId} " +
                    $"for interface '{interfaceType.FullName}'. Each method must have a unique worker-assigned ID; " +
                    "a duplicate indicates a protocol violation or a corrupted Load response.");
            }

            MethodInfo? matched = FindMatchingMethod(localMethods, descriptor);
            if (matched is null)
            {
                throw new InvalidOperationException(
                    $"Could not match the worker descriptor for method '{descriptor.DeclaringType}.{descriptor.Name}' " +
                    $"to any method on the local interface type '{interfaceType.FullName}'. " +
                    "The interface definition may differ between the host and the worker assembly.");
            }

            if (table.ContainsKey(matched))
            {
                throw new InvalidOperationException(
                    $"The worker returned two descriptors that both match the local method " +
                    $"'{matched.DeclaringType?.FullName}.{matched.Name}' on interface '{interfaceType.FullName}'. " +
                    "Each method must map to exactly one worker-assigned ID; a duplicate indicates " +
                    "a protocol violation or a corrupted Load response.");
            }

            table[matched] = descriptor.MethodId;
        }

        return table;
    }

    /// <summary>
    /// Finds the <see cref="MethodInfo"/> in <paramref name="candidates"/> that matches the given
    /// <paramref name="descriptor"/> by name, declaring type (assembly-qualified), parameter types
    /// (assembly-qualified), and return type (assembly-qualified). The return type is included so
    /// that methods whose signatures differ only by return type — valid in IL-emitted or
    /// dynamically generated assemblies even if C# forbids it — are not confused.
    /// Uses <see cref="MethodDescriptorDto.StableTypeName"/> so the comparison is consistent with
    /// how descriptors are produced by <see cref="MethodDescriptorDto.FromMethodInfo"/>.
    /// </summary>
    private static MethodInfo? FindMatchingMethod(MethodInfo[] candidates, MethodDescriptorDto descriptor)
    {
        foreach (MethodInfo m in candidates)
        {
            if (m.Name != descriptor.Name) continue;
            if ((m.DeclaringType?.AssemblyQualifiedName ?? string.Empty) != descriptor.DeclaringType) continue;
            if (MethodDescriptorDto.StableTypeName(m.ReturnType) != descriptor.ReturnType) continue;

            ParameterInfo[] parameters = m.GetParameters();
            if (parameters.Length != descriptor.ParameterTypes.Length) continue;

            bool match = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (MethodDescriptorDto.StableTypeName(parameters[i].ParameterType) != descriptor.ParameterTypes[i])
                {
                    match = false;
                    break;
                }
            }

            if (match) return m;
        }

        return null;
    }

    /// <summary>
    /// Writes <paramref name="request"/> and waits for its matching response, up to
    /// <paramref name="timeout"/>. Registers the pending completion source before writing so that
    /// a late response can always be matched; cleans up the pending entry immediately if serialization
    /// or the write itself throws, rather than leaving a stale entry until a timeout fires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On timeout, the request's pending entry is removed and the abandoned-call count is incremented
    /// only when a complete frame was successfully written. A serialization or write failure is a
    /// local error, not an abandoned remote call.
    /// </para>
    /// <para>
    /// Responses arriving after timeout are recognized as expected late arrivals via
    /// <see cref="recentlyTimedOutIds"/> and dropped silently. Responses for IDs that were never
    /// registered, or duplicate live-protocol responses, indicate a protocol violation.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Synchronous wrapper for <see cref="SendAndReceiveAsync"/>. Blocks the calling thread until the
    /// worker responds or the timeout expires. Avoid calling this from code that already owns a
    /// synchronization context (e.g. ASP.NET request threads) unless the caller can guarantee that
    /// the completion callbacks do not need to resume on that context.
    /// </summary>
    private WorkerResponse SendAndReceive(WorkerRequest request, TimeSpan timeout)
        => SendAndReceiveAsync(request, timeout).GetAwaiter().GetResult();

    /// <summary>
    /// Sends <paramref name="request"/> and asynchronously waits for the matching response.
    /// Supports cancellation via <paramref name="cancellationToken"/> in addition to the per-request
    /// <paramref name="timeout"/>.
    /// </summary>
    /// <param name="cancellationToken">
    /// Optional token to cancel the wait before the timeout expires. When cancelled,
    /// <see cref="OperationCanceledException"/> is thrown; when the timeout fires,
    /// <see cref="TimeoutException"/> is thrown instead.
    /// </param>
    private async Task<WorkerResponse> SendAndReceiveAsync(
        WorkerRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
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

        // Serialize and write AFTER registering so a late response can always be matched.
        // If serialization or the write fails, clean up the pending entry immediately and rethrow.
        bool frameWritten = false;
        try
        {
            string serialized = JsonSerializer.Serialize(request);
            lock (writeLock)
            {
                writer.WriteLine(serialized);
            }
            frameWritten = true;
        }
        catch (Exception writeEx)
        {
            // Remove the pending entry immediately; this is a local failure, not an abandoned call.
            if (pending.TryRemove(request.Id, out TaskCompletionSource<WorkerResponse>? failed))
            {
                failed.TrySetException(writeEx);
            }
            throw;
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        // Link the caller's token only when it can actually cancel, to avoid an extra allocation.
        using CancellationTokenSource? linkedSource = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellationToken)
            : null;
        CancellationToken effectiveToken = linkedSource?.Token ?? timeoutSource.Token;

        using CancellationTokenRegistration registration = effectiveToken.Register(() =>
        {
            if (pending.TryRemove(request.Id, out TaskCompletionSource<WorkerResponse>? timedOut))
            {
                timedOut.TrySetCanceled(effectiveToken);

                // Track this ID so the reader loop can recognize its eventual late response.
                TrackTimedOutId(request.Id);

                // Only count as abandoned if a complete frame was written.
                if (frameWritten)
                {
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
            }
        });

        try
        {
            return await completion.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException oce)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // Re-throw with the caller's token so the OperationCanceledException carries the
                // right token for the caller's catch blocks.
                throw new OperationCanceledException(
                    $"The '{request.Kind}' request was cancelled.", oce, cancellationToken);
            }

            throw new TimeoutException(
                $"The isolated Emit worker did not respond to a '{request.Kind}' request within {timeout}.");
        }
    }

    /// <summary>
    /// Adds <paramref name="requestId"/> to the bounded <see cref="recentlyTimedOutIds"/> queue
    /// so <see cref="RunReaderLoop"/> can distinguish its eventual late response from a protocol
    /// violation.
    /// </summary>
    private void TrackTimedOutId(long requestId)
    {
        recentlyTimedOutIds.Enqueue(requestId);

        // Trim the oldest entry when the queue exceeds the cap.
        if (Interlocked.Increment(ref timedOutIdCount) > MaxTrackedTimedOutIds)
        {
            recentlyTimedOutIds.TryDequeue(out _);
            Interlocked.Decrement(ref timedOutIdCount);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="responseId"/> appears in
    /// <see cref="recentlyTimedOutIds"/>, meaning it is a late response for a request that already
    /// timed out on the host side, and can therefore be dropped silently.
    /// </summary>
    private bool IsExpectedLateResponse(long responseId)
    {
        foreach (long id in recentlyTimedOutIds)
        {
            if (id == responseId) return true;
        }
        return false;
    }

    /// <summary>
    /// Continuously reads response lines for the lifetime of the connection and completes the matching
    /// pending request. Responses whose ID is not found in <see cref="pending"/> are checked against
    /// <see cref="recentlyTimedOutIds"/>: if found, they are dropped as expected late arrivals;
    /// otherwise they are treated as protocol violations (duplicate or unsolicited IDs) and the
    /// connection is faulted.
    /// </summary>
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
                    throw new InvalidOperationException(
                        "The isolated Emit worker sent a response line that could not be deserialized. " +
                        "The connection is now unusable.", ex);
                }

                if (response is null)
                    continue;

                if (pending.TryRemove(response.Id, out TaskCompletionSource<WorkerResponse>? completion))
                {
                    completion.TrySetResult(response);
                }
                else if (!IsExpectedLateResponse(response.Id))
                {
                    // A response whose ID was never registered and never timed out indicates protocol
                    // corruption (duplicate response, unsolicited ID, or worker bug).
                    throw new InvalidOperationException(
                        $"The isolated Emit worker sent a response with ID {response.Id}, which is not " +
                        "associated with any pending or recently-timed-out request. This indicates " +
                        "a protocol violation and the connection is now unusable.");
                }
                // else: expected late arrival for a previously timed-out request — drop silently.
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
    /// Retires this worker after a <see cref="WorkerRequestKind.Load"/> request was sent but no
    /// response was received (timeout or cancellation). Because the worker may have allocated a
    /// handle that the host can never observe or <see cref="UnloadInterface">unload</see>,
    /// continuing to use this worker would leak the native mapping until worker shutdown.
    /// </summary>
    private void RetireAfterOrphanedLoad()
    {
        if (connectionFault is null)
        {
            connectionFault = new InvalidOperationException(
                "A Load request was cancelled or timed out after being sent to the worker. " +
                "The worker may have allocated a handle that can never be unloaded. " +
                "The worker has been retired to prevent resource leaks from orphaned handles.");
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
    /// Starts the worker process inside the sandbox when one is available, or as a plain child
    /// process when no sandbox could be created for the current platform/configuration.
    /// </summary>
    private static Process StartWorkerProcess(string exePath, string pipeName, ref IProcessContainer? sandbox)
    {
        string[] arguments = BuildWorkerArguments(exePath, pipeName);

        if (sandbox is not null)
        {
            return sandbox.StartProcess(exePath, arguments);
        }

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

    /// <summary>Builds the permission set requested for the isolated Emit worker.</summary>
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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        try
        {
            if (!workerProcess.HasExited)
            {
                var shutdown = new WorkerRequest
                {
                    Id = Interlocked.Increment(ref nextId),
                    Kind = WorkerRequestKind.Shutdown,
                };
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

        // Best-effort: give the background reader loop a moment to observe the disposed reader
        // and exit cleanly.
        try
        {
            readerLoop.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Best-effort.
        }
    }

    /// <summary>
    /// Async-friendly alternative to <see cref="Dispose"/>: sends the Shutdown request without
    /// blocking a thread while waiting for the worker's response, then releases all resources.
    /// Prefer this method from async callers to avoid holding a thread during the IPC round-trip.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        disposed = true;

        try
        {
            if (!workerProcess.HasExited)
            {
                var shutdown = new WorkerRequest
                {
                    Id = Interlocked.Increment(ref nextId),
                    Kind = WorkerRequestKind.Shutdown,
                };
                try
                {
                    await SendAndReceiveAsync(shutdown, ShutdownTimeout).ConfigureAwait(false);
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

        try
        {
            await readerLoop.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort.
        }
    }
}
