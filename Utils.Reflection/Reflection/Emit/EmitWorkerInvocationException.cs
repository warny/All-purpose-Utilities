using System;
using System.Text;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Thrown on the host side when an isolated Emit worker reports a failure while loading a native
/// DLL or invoking one of its mapped functions.
/// </summary>
public sealed class EmitWorkerInvocationException : Exception
{
    /// <summary>
    /// Full type name of the exception that was thrown inside the worker process, when known.
    /// </summary>
    public string? RemoteExceptionTypeName { get; }

    /// <summary>
    /// Text of the worker-side exception's <see cref="Exception.StackTrace"/>, when known. This is
    /// plain text captured on the worker before it crossed the process boundary — not a real
    /// <see cref="Exception"/> object, which cannot be reconstructed with a meaningful stack trace
    /// across processes. Included so a caller inspecting or logging this exception can see where the
    /// failure actually happened inside the worker, instead of only this exception's own stack trace
    /// (which just shows the host-side call into the worker, not the worker-side call chain).
    /// </summary>
    public string? RemoteStackTrace { get; }

    /// <summary>
    /// Creates a new instance of <see cref="EmitWorkerInvocationException"/>.
    /// </summary>
    /// <param name="message">Description of the failure, including the worker-reported message.</param>
    /// <param name="remoteExceptionTypeName">Full type name of the exception thrown inside the worker, when known.</param>
    /// <param name="remoteStackTrace">Text of the worker-side exception's stack trace, when known.</param>
    public EmitWorkerInvocationException(string message, string? remoteExceptionTypeName, string? remoteStackTrace = null)
        : base(message)
    {
        RemoteExceptionTypeName = remoteExceptionTypeName;
        RemoteStackTrace = remoteStackTrace;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Appends <see cref="RemoteStackTrace"/> (when present) after the base <see cref="Exception.ToString"/>
    /// output, labeled to make clear it describes the worker side of the call, not this exception's own
    /// (host-side) stack trace.
    /// </remarks>
    public override string ToString()
    {
        if (string.IsNullOrEmpty(RemoteStackTrace))
        {
            return base.ToString();
        }

        var builder = new StringBuilder(base.ToString());
        builder.AppendLine();
        builder.Append("--- End of stack trace from the isolated Emit worker process ---");
        builder.AppendLine();
        builder.Append(RemoteStackTrace);
        return builder.ToString();
    }
}
