using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Tracks the lifecycle phase of an <see cref="ExceptionBlock"/> so that
/// <see cref="ControlFlowStack.Throw"/> can skip blocks whose protected try-region is
/// no longer active and avoid re-entering the same handler.
/// </summary>
public enum ExceptionBlockPhase
{
    /// <summary>Execution is inside the protected try body. The block is eligible as a throw target.</summary>
    Try,

    /// <summary>Execution is inside the finally handler. Throws propagate past this block.</summary>
    Finally,

    /// <summary>Execution is inside the catch handler. Throws propagate past this block.</summary>
    Catch,
}

/// <summary>
/// Represents an active try/catch/finally block on a <see cref="ControlFlowStack"/>.
/// </summary>
/// <remarks>
/// Typical VM usage:
/// <list type="bullet">
///   <item>TRY instruction: push an <see cref="ExceptionBlock"/> with the handler addresses.</item>
///   <item>THROW instruction: call <see cref="ControlFlowStack.Throw"/>; unwinds to this block,
///     sets <see cref="ThrownValue"/>, and redirects execution to <see cref="FinallyAddress"/>
///     when present (storing the catch address in <see cref="PendingCatchAddress"/> for later),
///     or directly to <see cref="CatchAddress"/> when there is no finally clause.
///     The block stays on the stack so that the handler body can inspect <see cref="ThrownValue"/>.</item>
///   <item>ENDFINALLY instruction: read <see cref="PendingCatchAddress"/>; if non-null, jump there
///     to enter the catch body; if null, call <see cref="ControlFlowStack.Pop"/> and propagate.</item>
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
    /// When <see cref="ControlFlowStack.Throw"/> routes execution through a finally block because
    /// both <see cref="FinallyAddress"/> and <see cref="CatchAddress"/> are present, this property
    /// stores the catch handler address so that the ENDFINALLY instruction handler can jump there
    /// after the finally block completes. <see langword="null"/> when there is no pending catch
    /// (finally-only or catch-only scenarios, or when the block has not been thrown into yet).
    /// </summary>
    public int? PendingCatchAddress { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when an exception is currently in flight through this block —
    /// set by <see cref="ControlFlowStack.Throw"/> and consumed by
    /// <see cref="ControlFlowStack.EndFinally"/> to propagate the exception to outer handlers
    /// once the finally block completes.
    /// <see langword="false"/> when the finally block was entered during normal (non-exception) execution.
    /// </summary>
    internal bool ExceptionInFlight { get; set; }

    /// <summary>
    /// Current handler phase. Starts at <see cref="ExceptionBlockPhase.Try"/>; transitions to
    /// <see cref="ExceptionBlockPhase.Finally"/> when <see cref="ControlFlowStack.Throw"/> routes
    /// execution into the finally clause, and to <see cref="ExceptionBlockPhase.Catch"/> when
    /// <see cref="ControlFlowStack.EndFinally"/> redirects to the catch clause.
    /// <see cref="ControlFlowStack.Throw"/> skips blocks whose <see cref="Phase"/> is not
    /// <see cref="ExceptionBlockPhase.Try"/> so that a throw from inside a handler propagates
    /// to the next outer handler instead of re-entering the same block.
    /// </summary>
    public ExceptionBlockPhase Phase { get; internal set; } = ExceptionBlockPhase.Try;

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
