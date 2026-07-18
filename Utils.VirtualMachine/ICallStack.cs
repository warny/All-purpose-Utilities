using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Represents a call stack that tracks return addresses for nested subroutine calls.
/// </summary>
/// <remarks>
/// Two built-in implementations are provided:
/// <list type="bullet">
///   <item><see cref="SimpleCallStack"/> — stores return addresses only; minimal overhead.</item>
///   <item><see cref="CallStack"/> — stores a <see cref="CallFrame"/> per invocation, providing per-frame local variable storage.</item>
/// </list>
/// </remarks>
public interface ICallStack
{
    /// <summary>Gets the number of frames currently on the stack.</summary>
    int Depth { get; }

    /// <summary>Gets a value indicating whether the stack has no active frames.</summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Gets the maximum number of frames allowed before <see cref="Call"/> throws.
    /// Configured at construction time.
    /// </summary>
    int MaxDepth { get; }

    /// <summary>
    /// Pushes a new frame onto the stack, recording <paramref name="returnAddress"/> as the
    /// instruction pointer to restore when the corresponding <see cref="Return"/> is called.
    /// </summary>
    /// <param name="returnAddress">
    /// Byte offset in the instruction stream to return to. Must be non-negative; negative values
    /// are reserved as termination sentinels and are therefore rejected.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="returnAddress"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Depth"/> has reached <see cref="MaxDepth"/>.</exception>
    void Call(int returnAddress);

    /// <summary>
    /// Pops the current frame and returns the saved return address, or <c>-1</c> when the
    /// stack is empty. Because <see cref="Call"/> rejects negative addresses, a return value
    /// of <c>-1</c> unambiguously signals stack underflow/program termination; callers should
    /// assign it to <see cref="Context.InstructionPointer"/> so the
    /// <see cref="VirtualProcessor{T}"/> execution loop stops.
    /// </summary>
    /// <returns>
    /// The instruction-pointer value saved by the matching <see cref="Call"/> (always ≥ 0),
    /// or <c>-1</c> if the stack is empty.
    /// </returns>
    int Return();

    /// <summary>
    /// Gets the frame at the top of the stack, or <see langword="null"/> when the stack is
    /// empty or the implementation does not support per-frame local variables
    /// (e.g. <see cref="SimpleCallStack"/>).
    /// </summary>
    CallFrame? CurrentFrame { get; }
}
