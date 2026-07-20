using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Base class for all exceptions thrown by the <c>Utils.VirtualMachine</c> library.
/// </summary>
/// <remarks>
/// Deriving from <see cref="InvalidOperationException"/> preserves backward compatibility:
/// existing <c>catch (InvalidOperationException)</c> handlers continue to intercept VM errors
/// without modification.
/// </remarks>
public abstract class VirtualMachineException : InvalidOperationException
{
    /// <summary>Initializes a new instance with no message.</summary>
    protected VirtualMachineException() { }

    /// <summary>Initializes a new instance with the specified message.</summary>
    /// <param name="message">The message that describes the error.</param>
    protected VirtualMachineException(string message) : base(message) { }

    /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    protected VirtualMachineException(string message, Exception innerException) : base(message, innerException) { }
}
