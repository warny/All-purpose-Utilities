using System;

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
    /// Creates a new instance of <see cref="EmitWorkerInvocationException"/>.
    /// </summary>
    /// <param name="message">Description of the failure, including the worker-reported message.</param>
    /// <param name="remoteExceptionTypeName">Full type name of the exception thrown inside the worker, when known.</param>
    public EmitWorkerInvocationException(string message, string? remoteExceptionTypeName)
        : base(message)
    {
        RemoteExceptionTypeName = remoteExceptionTypeName;
    }
}
