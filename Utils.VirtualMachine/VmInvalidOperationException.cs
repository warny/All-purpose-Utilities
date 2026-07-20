using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Thrown when a VM operation is performed in an invalid state (e.g. stack underflow,
/// malformed bytecode structure, or exhausted hard identifier counters).
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> so that existing
/// <c>catch (InvalidOperationException)</c> handlers — historically used to intercept
/// VM state errors such as stack underflow — continue to work without modification.
/// </remarks>
public class VmInvalidOperationException : InvalidOperationException
{
    /// <summary>Initializes a new instance with no message.</summary>
    public VmInvalidOperationException() { }

    /// <summary>Initializes a new instance with the specified message.</summary>
    /// <param name="message">The message that describes the error.</param>
    public VmInvalidOperationException(string message) : base(message) { }

    /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public VmInvalidOperationException(string message, Exception innerException) : base(message, innerException) { }
}
