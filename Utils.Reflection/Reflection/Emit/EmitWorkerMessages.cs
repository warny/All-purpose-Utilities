using System.Runtime.InteropServices;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Identifies the purpose of a <see cref="WorkerRequest"/> exchanged with an isolated Emit worker.
/// </summary>
internal enum WorkerRequestKind
{
    /// <summary>Loads the native DLL and emits the mapping class for an interface.</summary>
    Load,

    /// <summary>Invokes a single interface method on the previously loaded mapping instance.</summary>
    Call,

    /// <summary>Requests a graceful shutdown of the worker process.</summary>
    Shutdown,
}

/// <summary>
/// Single-line JSON request sent from the host process to an isolated Emit worker over a named pipe.
/// </summary>
internal sealed class WorkerRequest
{
    /// <summary>Correlation identifier echoed back in the matching <see cref="WorkerResponse"/>.</summary>
    public int Id { get; set; }

    /// <summary>Purpose of this request.</summary>
    public WorkerRequestKind Kind { get; set; }

    /// <summary>(Load) Location on disk of the assembly declaring the interface to map.</summary>
    public string? InterfaceAssemblyPath { get; set; }

    /// <summary>(Load) Full name of the interface type to map.</summary>
    public string? InterfaceTypeFullName { get; set; }

    /// <summary>(Load) Path of the native DLL to load.</summary>
    public string? DllPath { get; set; }

    /// <summary>(Load) Calling convention used for the generated delegates.</summary>
    public CallingConvention CallingConvention { get; set; }

    /// <summary>(Call) Metadata token of the interface method to invoke, resolved against the interface's module.</summary>
    public int MethodMetadataToken { get; set; }

    /// <summary>(Call) JSON payload for each positional argument (including by-ref inputs).</summary>
    public string?[]? ArgumentsJson { get; set; }
}

/// <summary>
/// Single-line JSON response sent from an isolated Emit worker back to the host process.
/// </summary>
internal sealed class WorkerResponse
{
    /// <summary>Correlation identifier matching the originating <see cref="WorkerRequest"/>.</summary>
    public int Id { get; set; }

    /// <summary><see langword="true"/> when the request completed without error.</summary>
    public bool Success { get; set; }

    /// <summary>(Call) JSON payload of the method's return value, or <see langword="null"/> for <see langword="void"/>.</summary>
    public string? ReturnValueJson { get; set; }

    /// <summary>(Call) JSON payload for each by-ref/out parameter, positional, empty entries for non-by-ref parameters.</summary>
    public string?[]? ByRefValuesJson { get; set; }

    /// <summary>Message of the exception that occurred while handling the request, when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Full type name of the exception that occurred while handling the request.</summary>
    public string? ErrorTypeName { get; set; }
}
