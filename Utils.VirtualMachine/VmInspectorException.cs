using System;

namespace Utils.VirtualMachine;

/// <summary>Identifies which <see cref="IVmInspector{T}"/> callback threw.</summary>
public enum VmInspectorPhase
{
    /// <summary>The exception was thrown by <see cref="IVmInspector{T}.OnBreakpoint"/>.</summary>
    OnBreakpoint,

    /// <summary>The exception was thrown by <see cref="IVmInspector{T}.BeforeInstruction"/>.</summary>
    BeforeInstruction
}

/// <summary>
/// Thrown when an <see cref="IVmInspector{T}"/> callback raises an exception, allowing callers
/// to distinguish inspector failures from instruction-execution faults.
/// </summary>
/// <remarks>
/// The original exception is available as <see cref="Exception.InnerException"/>.
/// Context state at the time of the throw reflects whatever the callback had already mutated;
/// full snapshot-and-restore of context state is outside the scope of this wrapping layer.
/// </remarks>
public class VmInspectorException : Exception
{
    /// <summary>Gets the callback phase that threw.</summary>
    public VmInspectorPhase Phase { get; }

    /// <summary>Gets the instruction-pointer address at the time of the callback.</summary>
    public int Address { get; }

    /// <summary>Gets the human-readable name of the instruction at <see cref="Address"/>.</summary>
    public string InstructionName { get; }

    /// <summary>Initializes a new instance of the <see cref="VmInspectorException"/> class.</summary>
    public VmInspectorException() : base() { InstructionName = string.Empty; }

    /// <summary>Initializes a new instance with a specified error message.</summary>
    /// <param name="message">The message that describes the error.</param>
    public VmInspectorException(string message) : base(message) { InstructionName = string.Empty; }

    /// <summary>Initializes a new instance with a specified error message and inner exception.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public VmInspectorException(string message, Exception innerException)
        : base(message, innerException) { InstructionName = string.Empty; }

    /// <summary>
    /// Initializes a new instance that wraps an inspector callback failure.
    /// </summary>
    /// <param name="phase">The callback that threw.</param>
    /// <param name="address">Instruction-pointer address at the time of the callback.</param>
    /// <param name="instructionName">Human-readable instruction name.</param>
    /// <param name="innerException">The original exception thrown by the callback.</param>
    public VmInspectorException(
        VmInspectorPhase phase,
        int address,
        string instructionName,
        Exception innerException)
        : base(
            $"Inspector callback '{phase}' threw at address {address} (instruction '{instructionName}'): {innerException.Message}",
            innerException)
    {
        Phase = phase;
        Address = address;
        InstructionName = instructionName;
    }
}
