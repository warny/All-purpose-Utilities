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
/// Typical VM usage follows conventional structured-exception semantics
/// (catch executes before finally):
/// <list type="bullet">
///   <item>TRY instruction: push an <see cref="ExceptionBlock"/> with the handler addresses.</item>
///   <item>THROW instruction: call <see cref="ControlFlowStack.Throw"/>; unwinds to this block,
///     sets <see cref="ThrownValue"/>, and redirects execution to <see cref="CatchAddress"/>
///     when present (storing <see cref="FinallyAddress"/> in <see cref="PendingFinallyAddress"/>
///     for later), or directly to <see cref="FinallyAddress"/> when there is no catch clause.
///     The block stays on the stack so that the handler body can inspect <see cref="ThrownValue"/>.</item>
///   <item>ENDCATCH instruction (catch body completed normally): call
///     <see cref="ControlFlowStack.EndCatch"/>; if <see cref="PendingFinallyAddress"/> is set,
///     jumps there for cleanup; otherwise pops the block.</item>
///   <item>ENDFINALLY instruction: call <see cref="ControlFlowStack.EndFinally"/>; if an exception
///     is still in flight (finally-only path), propagates to the next outer handler; otherwise
///     pops the block (normal cleanup after catch, or normal try-exit).</item>
///   <item>ENDTRY instruction: call <see cref="ControlFlowStack.Pop"/> to close the block on the
///     normal (non-exception) path through a try body.</item>
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
    /// When <see cref="ControlFlowStack.Throw"/> routes execution into a catch handler and both
    /// <see cref="CatchAddress"/> and <see cref="FinallyAddress"/> are present, this property
    /// stores the finally address so that the ENDCATCH instruction handler can jump there after
    /// the catch body completes normally. <see langword="null"/> when there is no pending finally
    /// (finally-only or catch-only scenarios, or when the block has not been thrown into yet).
    /// </summary>
    public int? PendingFinallyAddress { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when an exception is currently in flight through this block —
    /// set by <see cref="ControlFlowStack.Throw"/> on the finally-only path and consumed by
    /// <see cref="ControlFlowStack.EndFinally"/> to propagate the exception to outer handlers
    /// once the finally block completes.
    /// <see langword="false"/> when the finally block was entered during normal (non-exception)
    /// execution, or after <see cref="ControlFlowStack.EndCatch"/> consumed the exception.
    /// </summary>
    internal bool ExceptionInFlight { get; set; }

    /// <summary>
    /// Current handler phase. Starts at <see cref="ExceptionBlockPhase.Try"/>; transitions to
    /// <see cref="ExceptionBlockPhase.Catch"/> when <see cref="ControlFlowStack.Throw"/> routes
    /// execution into the catch clause, and to <see cref="ExceptionBlockPhase.Finally"/> when
    /// execution enters the finally clause (either via <see cref="ControlFlowStack.EndCatch"/>
    /// or directly from <see cref="ControlFlowStack.Throw"/> in the finally-only case).
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any non-null address is negative. Negative values are the termination sentinel
    /// and must not be used as branch targets.
    /// </exception>
    public ExceptionBlock(int startAddress, int? catchAddress, int? finallyAddress)
    {
        if (catchAddress is null && finallyAddress is null)
            throw new ArgumentException("An ExceptionBlock must have at least a catch or a finally address.");
        if (startAddress < 0)
            throw new ArgumentOutOfRangeException(nameof(startAddress), "Branch target addresses must be non-negative.");
        if (catchAddress.HasValue && catchAddress.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(catchAddress), "Branch target addresses must be non-negative.");
        if (finallyAddress.HasValue && finallyAddress.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(finallyAddress), "Branch target addresses must be non-negative.");
        StartAddress = startAddress;
        CatchAddress = catchAddress;
        FinallyAddress = finallyAddress;
    }
}
