using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Identifies the purpose of a <see cref="WorkerRequest"/> exchanged with an isolated Emit worker.
/// </summary>
internal enum WorkerRequestKind
{
    /// <summary>
    /// Initial capability exchange: verifies that host and worker share the same protocol version
    /// before any <see cref="Load"/> request is sent. Should be the first request on every new connection.
    /// </summary>
    Hello,

    /// <summary>Loads the native DLL and emits the mapping class for an interface, allocating a new handle for it.</summary>
    Load,

    /// <summary>Invokes a single interface method on a previously loaded mapping instance, identified by <see cref="WorkerRequest.Handle"/>.</summary>
    Call,

    /// <summary>Releases a previously loaded mapping instance (disposing it), identified by <see cref="WorkerRequest.Handle"/>, without shutting down the worker.</summary>
    Unload,

    /// <summary>Requests a graceful shutdown of the worker process.</summary>
    Shutdown,
}

/// <summary>
/// Single-line JSON request sent from the host process to an isolated Emit worker over a named pipe.
/// </summary>
internal sealed class WorkerRequest
{
    /// <summary>Correlation identifier echoed back in the matching <see cref="WorkerResponse"/>.</summary>
    public long Id { get; set; }

    /// <summary>Purpose of this request.</summary>
    public WorkerRequestKind Kind { get; set; }

    /// <summary>
    /// (Hello) Protocol version declared by the host. The worker rejects versions it does not implement.
    /// See <see cref="EmitWorkerHost.ProtocolVersion"/> for the current value.
    /// </summary>
    public int ProtocolVersion { get; set; }

    /// <summary>(Load) Location on disk of the assembly declaring the interface to map.</summary>
    public string? InterfaceAssemblyPath { get; set; }

    /// <summary>(Load) Full name of the interface type to map.</summary>
    public string? InterfaceTypeFullName { get; set; }

    /// <summary>(Load) Path of the native DLL to load.</summary>
    public string? DllPath { get; set; }

    /// <summary>(Load) Calling convention used for the generated delegates.</summary>
    public CallingConvention CallingConvention { get; set; }

    /// <summary>
    /// (Call) Worker-assigned private method ID, as returned in the Load response's
    /// <see cref="WorkerResponse.MethodDescriptors"/> table. Using a load-time assigned ID rather
    /// than a metadata token ensures cross-module method identity is preserved: metadata tokens are
    /// unique only within a single module, so two methods inherited from different assemblies may
    /// share the same numeric token.
    /// </summary>
    public int MethodId { get; set; }

    /// <summary>(Call) JSON payload for each positional argument (including by-ref inputs).</summary>
    public string?[]? ArgumentsJson { get; set; }

    /// <summary>
    /// (Call, Unload) Handle of the loaded interface instance this request targets, as returned by
    /// that instance's <see cref="WorkerRequestKind.Load"/> response. Lets a single worker process
    /// hold several concurrently loaded interfaces (see <see cref="EmitWorkerPool"/>) without their
    /// calls being misrouted to each other. Unused for Load (a new handle is always allocated) and
    /// Shutdown requests.
    /// </summary>
    public int Handle { get; set; }
}

/// <summary>
/// Single-line JSON response sent from an isolated Emit worker back to the host process.
/// </summary>
internal sealed class WorkerResponse
{
    /// <summary>Correlation identifier matching the originating <see cref="WorkerRequest"/>.</summary>
    public long Id { get; set; }

    /// <summary><see langword="true"/> when the request completed without error.</summary>
    public bool Success { get; set; }

    /// <summary>
    /// (Load) Handle allocated for the newly loaded interface instance, to be echoed back on every
    /// subsequent <see cref="WorkerRequestKind.Call"/>/<see cref="WorkerRequestKind.Unload"/> request
    /// for it. (Call) Echoes the request's handle back for the caller's convenience; unused otherwise.
    /// </summary>
    public int Handle { get; set; }

    /// <summary>
    /// (Load) Descriptors for every method in the loaded interface, mapping worker-assigned private
    /// IDs to host-verifiable signatures. The host uses this table to build a
    /// <see cref="System.Reflection.MethodInfo"/>-to-ID mapping so subsequent Call requests send
    /// only the private ID rather than a metadata token, which is unique only within one module.
    /// </summary>
    public MethodDescriptorDto[]? MethodDescriptors { get; set; }

    /// <summary>(Call) JSON payload of the method's return value, or <see langword="null"/> for <see langword="void"/>.</summary>
    public string? ReturnValueJson { get; set; }

    /// <summary>(Call) JSON payload for each by-ref/out parameter, positional, empty entries for non-by-ref parameters.</summary>
    public string?[]? ByRefValuesJson { get; set; }

    /// <summary>
    /// Sanitized message of the exception that occurred while handling the request, when
    /// <see cref="Success"/> is <see langword="false"/>. Does not include local file system paths,
    /// generated type names, or stack traces, which are omitted by default to limit information
    /// disclosure from the worker's internal state.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stable category name of the exception type (e.g. <c>InvalidOperationException</c>), when
    /// known. Worker internals such as generated class names or full assembly-qualified names are
    /// stripped; only the short type name is returned.
    /// </summary>
    public string? ErrorTypeName { get; set; }

    /// <summary>
    /// (Hello) Protocol version implemented by the worker, echoed back so the host can confirm the
    /// value even if it already checked <see cref="Success"/>. Only meaningful when
    /// <see cref="Success"/> is <see langword="true"/>.
    /// </summary>
    public int ProtocolVersion { get; set; }

    /// <summary>
    /// (Shutdown) <see langword="true"/> when all active requests completed and all loaded mappings
    /// were disposed before the graceful-drain deadline. <see langword="false"/> means the deadline
    /// expired and some tasks may still be executing (or were forcibly abandoned). The host may use
    /// this to decide whether to kill the worker process immediately.
    /// </summary>
    public bool ShutdownWasGraceful { get; set; }
}

/// <summary>
/// Describes a single method in a loaded interface, carrying the worker-assigned private method ID
/// and enough signature information for the host to match it against a local
/// <see cref="System.Reflection.MethodInfo"/> without relying on cross-module metadata tokens.
/// </summary>
internal sealed class MethodDescriptorDto
{
    /// <summary>Worker-assigned private method ID, stable for the lifetime of one loaded handle.</summary>
    public int MethodId { get; set; }

    /// <summary>Simple method name (no overload decoration).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full name of the type that declares this method (disambiguates inherited overloads).</summary>
    public string DeclaringType { get; set; } = string.Empty;

    /// <summary>
    /// Full CLR type names of each parameter, in declaration order. By-ref types use the standard
    /// CLR notation (e.g. <c>System.Int32&amp;</c>) so the host can match precisely.
    /// </summary>
    public string[] ParameterTypes { get; set; } = [];

    /// <summary>Full CLR type name of the return type.</summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// Builds a descriptor from <paramref name="methodId"/> and the given <paramref name="method"/>.
    /// </summary>
    internal static MethodDescriptorDto FromMethodInfo(int methodId, MethodInfo method) =>
        new()
        {
            MethodId = methodId,
            Name = method.Name,
            DeclaringType = method.DeclaringType?.FullName ?? string.Empty,
            ParameterTypes = Array.ConvertAll(method.GetParameters(), p => p.ParameterType.FullName ?? string.Empty),
            ReturnType = method.ReturnType.FullName ?? string.Empty,
        };
}
