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
    /// Gets or sets the maximum number of frames allowed before <see cref="Call"/> throws.
    /// </summary>
    /// <value>Defaults to <c>512</c>.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value less than one.</exception>
    int MaxDepth { get; set; }

    /// <summary>
    /// Pushes a new frame onto the stack, recording <paramref name="returnAddress"/> as the
    /// instruction pointer to restore when the corresponding <see cref="Return"/> is called.
    /// </summary>
    /// <param name="returnAddress">Byte offset in the instruction stream to return to.</param>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Depth"/> has reached <see cref="MaxDepth"/>.</exception>
    void Call(int returnAddress);

    /// <summary>
    /// Pops the current frame and returns the saved return address.
    /// </summary>
    /// <returns>The instruction-pointer value saved by the matching <see cref="Call"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the stack is empty.</exception>
    int Return();

    /// <summary>
    /// Attempts to pop the current frame without throwing when the stack is empty.
    /// </summary>
    /// <param name="returnAddress">
    /// When this method returns <see langword="true"/>, receives the saved return address; otherwise, zero.
    /// </param>
    /// <returns><see langword="true"/> if a frame was popped; <see langword="false"/> if the stack was empty.</returns>
    bool TryReturn(out int returnAddress);
}
