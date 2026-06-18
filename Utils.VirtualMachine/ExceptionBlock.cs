using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Represents an active try/catch/finally block on a <see cref="ControlFlowStack"/>.
/// </summary>
/// <remarks>
/// Typical VM usage:
/// <list type="bullet">
///   <item>TRY instruction: push an <see cref="ExceptionBlock"/> with the handler addresses.</item>
///   <item>THROW instruction: call <see cref="ControlFlowStack.Throw"/>; unwinds to this block,
///     sets <see cref="ThrownValue"/>, and redirects execution to <see cref="CatchAddress"/>
///     (or <see cref="FinallyAddress"/> when there is no catch). The block stays on the stack
///     so that the handler body can inspect <see cref="ThrownValue"/>.</item>
///   <item>ENDTRY instruction: call <see cref="ControlFlowStack.Pop"/>.</item>
/// </list>
/// At least one of <see cref="CatchAddress"/> or <see cref="FinallyAddress"/> must be provided.
/// </remarks>
public sealed class ExceptionBlock : IControlFlowBlock
{
    /// <inheritdoc/>
    public int StartAddress { get; }

    /// <summary>Address of the catch handler, or <see langword="null"/> if there is no catch clause.</summary>
    public int? CatchAddress { get; }

    /// <summary>Address of the finally block, or <see langword="null"/> if there is no finally clause.</summary>
    public int? FinallyAddress { get; }

    /// <summary>
    /// The value thrown by the most recent THROW that targeted this block.
    /// <see langword="null"/> when the block was entered normally or has not been thrown into yet.
    /// Set by <see cref="ControlFlowStack.Throw"/> and readable from the catch/finally handler body.
    /// </summary>
    public object? ThrownValue { get; internal set; }

    /// <summary>
    /// Initializes a new exception block.
    /// </summary>
    /// <param name="startAddress">Address of the TRY instruction.</param>
    /// <param name="catchAddress">Address of the catch handler, or <see langword="null"/>.</param>
    /// <param name="finallyAddress">Address of the finally block, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when both <paramref name="catchAddress"/> and <paramref name="finallyAddress"/> are <see langword="null"/>.
    /// </exception>
    public ExceptionBlock(int startAddress, int? catchAddress, int? finallyAddress)
    {
        if (catchAddress is null && finallyAddress is null)
            throw new ArgumentException("An ExceptionBlock must have at least a catch or a finally address.");
        StartAddress = startAddress;
        CatchAddress = catchAddress;
        FinallyAddress = finallyAddress;
    }
}
