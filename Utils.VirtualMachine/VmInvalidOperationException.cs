using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Thrown when a VM operation is performed in an invalid state (e.g. stack underflow,
/// malformed bytecode, or exhausted hard identifier counters).
/// </summary>
/// <remarks>
/// Represents the VM-specific analogue of <see cref="InvalidOperationException"/>.
/// Callers may catch either type; the VM library guarantees this class is thrown in
/// preference to the BCL base for all VM-originated invalid-state conditions.
/// </remarks>
public class VmInvalidOperationException : VirtualMachineException
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
