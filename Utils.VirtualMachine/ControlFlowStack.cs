using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.VirtualMachine;

/// <summary>
/// Manages the nesting of structured control flow blocks at runtime:
/// conditionals (<see cref="ConditionalBlock"/>), loops (<see cref="LoopBlock"/>),
/// and exception handlers (<see cref="ExceptionBlock"/>).
/// </summary>
/// <remarks>
/// No bytecodes are defined here. Instruction handlers of the concrete VM call the
/// push/pop/navigate methods and are free to assign any opcode to each operation.
/// </remarks>
public class ControlFlowStack
{
    /// <summary>The default maximum nesting depth when no explicit limit is provided.</summary>
    public const int DefaultMaxDepth = 1024;

    private readonly Stack<IControlFlowBlock> _blocks = new();

    /// <summary>
    /// Gets the maximum number of blocks that may be open simultaneously.
    /// Pushing beyond this limit throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public int MaxDepth { get; }

    /// <summary>Gets the number of currently open blocks.</summary>
    public int Depth => _blocks.Count;

    /// <summary>Gets a value indicating whether no blocks are currently open.</summary>
    public bool IsEmpty => _blocks.Count == 0;

    /// <summary>Gets the innermost open block, or <see langword="null"/> when the stack is empty.</summary>
    public IControlFlowBlock? CurrentBlock => _blocks.TryPeek(out var b) ? b : null;

    /// <summary>
    /// Gets all currently open blocks from innermost (top of stack) to outermost (bottom),
    /// as a live enumerable. Useful for diagnostics and post-execution assertions.
    /// </summary>
    public IEnumerable<IControlFlowBlock> Blocks => _blocks;

    /// <summary>
    /// Initializes a new <see cref="ControlFlowStack"/> with the specified maximum nesting depth.
    /// </summary>
    /// <param name="maxDepth">
    /// Maximum number of blocks that may be open simultaneously.
    /// Defaults to <see cref="DefaultMaxDepth"/> (<c>1024</c>).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDepth"/> is less than one.</exception>
    public ControlFlowStack(int maxDepth = DefaultMaxDepth)
    {
        if (maxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth must be at least 1.");
        MaxDepth = maxDepth;
    }

    /// <summary>Opens a conditional (if/else) block.</summary>
    /// <param name="startAddress">Address of the IF instruction.</param>
    /// <param name="endAddress">Address immediately after the ENDIF.</param>
    /// <param name="elseAddress">Address of the ELSE branch, or <see langword="null"/> if absent.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any non-null address is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="MaxDepth"/> would be exceeded.</exception>
    public void PushConditional(int startAddress, int endAddress, int? elseAddress = null)
    {
        ThrowIfDepthExceeded();
        _blocks.Push(new ConditionalBlock(startAddress, endAddress, elseAddress));
    }

    /// <summary>Opens a loop block.</summary>
    /// <param name="startAddress">Address of the loop header; target of CONTINUE.</param>
    /// <param name="endAddress">Address after the loop; target of BREAK.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any address is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="MaxDepth"/> would be exceeded.</exception>
    public void PushLoop(int startAddress, int endAddress)
    {
        ThrowIfDepthExceeded();
        _blocks.Push(new LoopBlock(startAddress, endAddress));
    }

    /// <summary>Opens a try/catch/finally block.</summary>
    /// <param name="startAddress">Address of the TRY instruction.</param>
    /// <param name="catchAddress">Address of the catch handler, or <see langword="null"/>.</param>
    /// <param name="finallyAddress">Address of the finally block, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when both <paramref name="catchAddress"/> and <paramref name="finallyAddress"/> are <see langword="null"/>.
    /// An exception block with no handler is unreachable and indicates malformed bytecode.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any non-null address is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="MaxDepth"/> would be exceeded.</exception>
    public void PushException(int startAddress, int? catchAddress, int? finallyAddress)
    {
        if (catchAddress is null && finallyAddress is null)
            throw new ArgumentException(
                "An exception block must have at least one handler: catchAddress or finallyAddress must be non-null.");
        ThrowIfDepthExceeded();
        _blocks.Push(new ExceptionBlock(startAddress, catchAddress, finallyAddress));
    }

    private void ThrowIfDepthExceeded()
    {
        if (_blocks.Count >= MaxDepth)
            throw new InvalidOperationException(
                $"Control-flow stack overflow: maximum nesting depth of {MaxDepth} exceeded.");
    }

    /// <summary>
    /// Closes the innermost open block. Called at ENDIF, ENDLOOP, or ENDTRY.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no block is open.</exception>
    public IControlFlowBlock Pop()
    {
        if (_blocks.Count == 0)
            throw new InvalidOperationException("Control flow stack underflow: no open block to close.");
        return _blocks.Pop();
    }

    /// <summary>
    /// Closes the innermost open block, asserting that it is of type <typeparamref name="T"/>.
    /// Use this overload when the bytecode guarantees the block type (e.g. ENDIF always closes a
    /// <see cref="ConditionalBlock"/>); mismatched types indicate malformed bytecode.
    /// </summary>
    /// <typeparam name="T">The expected concrete block type.</typeparam>
    /// <returns>The closed block cast to <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no block is open.</exception>
    /// <exception cref="VirtualProcessorException">
    /// Thrown when the innermost block is not of type <typeparamref name="T"/>.
    /// </exception>
    public T Pop<T>() where T : class, IControlFlowBlock
    {
        if (_blocks.Count == 0)
            throw new InvalidOperationException("Control flow stack underflow: no open block to close.");
        if (_blocks.Peek() is not T)
            throw new VirtualProcessorException(
                $"Control flow type mismatch: expected {typeof(T).Name} but found {_blocks.Peek().GetType().Name}.");
        return (T)_blocks.Pop();
    }

    /// <summary>
    /// Handles a BREAK instruction: pops all blocks up to and including the nearest
    /// <see cref="LoopBlock"/>, then sets <see cref="Context.InstructionPointer"/> to
    /// the loop's <see cref="LoopBlock.EndAddress"/>.
    /// </summary>
    /// <remarks>
    /// The operation is transactional: the loop is located first without mutating the stack.
    /// The exception is thrown before any block is popped, so the stack remains intact when
    /// malformed bytecode issues BREAK outside a loop.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when no enclosing loop is in scope.</exception>
    public void Break(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Validate first — locate the nearest loop without mutating the stack.
        LoopBlock? target = FindEnclosing<LoopBlock>();
        if (target is null)
            throw new InvalidOperationException("BREAK used outside of a loop.");

        // Commit: pop all inner blocks, including the loop itself.
        while (_blocks.TryPop(out var block))
        {
            if (block is LoopBlock)
            {
                context.InstructionPointer = target.EndAddress;
                return;
            }
        }
    }

    /// <summary>
    /// Handles a CONTINUE instruction: pops all blocks nested inside the nearest
    /// <see cref="LoopBlock"/> (the loop itself stays on the stack), then sets
    /// <see cref="Context.InstructionPointer"/> to the loop's <see cref="LoopBlock.StartAddress"/>.
    /// </summary>
    /// <remarks>
    /// The operation is transactional: the loop is located first without mutating the stack.
    /// The exception is thrown before any block is popped, so the stack remains intact when
    /// malformed bytecode issues CONTINUE outside a loop.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when no enclosing loop is in scope.</exception>
    public void Continue(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Validate first — locate the nearest loop without mutating the stack.
        LoopBlock? target = FindEnclosing<LoopBlock>();
        if (target is null)
            throw new InvalidOperationException("CONTINUE used outside of a loop.");

        // Commit: pop all inner blocks that are nested inside the loop, but keep the loop itself.
        while (_blocks.TryPeek(out var block))
        {
            if (block is LoopBlock)
            {
                context.InstructionPointer = target.StartAddress;
                return;
            }
            _blocks.Pop();
        }
    }

    /// <summary>
    /// Handles an ENDCATCH instruction: closes the catch body and, when a finally clause is
    /// pending (stored by a prior <see cref="Throw"/> call when both
    /// <see cref="ExceptionBlock.CatchAddress"/> and <see cref="ExceptionBlock.FinallyAddress"/>
    /// were set), redirects <see cref="Context.InstructionPointer"/> to that finally block and
    /// clears <see cref="ExceptionBlock.ExceptionInFlight"/> so that <see cref="EndFinally"/>
    /// will not attempt to re-propagate the now-handled exception. When no pending finally is
    /// present (catch-only block), the block is popped.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// <see langword="true"/> if execution was redirected to a finally block;
    /// <see langword="false"/> if the block was popped with no finally to redirect to.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the innermost open block is not an <see cref="ExceptionBlock"/> in the
    /// <see cref="ExceptionBlockPhase.Catch"/> phase.
    /// </exception>
    public bool EndCatch(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_blocks.TryPeek(out var top) && top is ExceptionBlock ex && ex.Phase == ExceptionBlockPhase.Catch)
        {
            if (ex.PendingFinallyAddress.HasValue)
            {
                // Exception was handled by catch; run finally for cleanup.
                // ExceptionInFlight cleared so EndFinally just pops normally.
                ex.ExceptionInFlight = false;
                ex.Phase = ExceptionBlockPhase.Finally;
                context.InstructionPointer = ex.PendingFinallyAddress.Value;
                ex.PendingFinallyAddress = null;
                return true;
            }
            _blocks.Pop();
            return false;
        }
        throw new InvalidOperationException("ENDCATCH used outside of a catch block.");
    }

    /// <summary>
    /// Handles an ENDFINALLY instruction: when an exception is still in flight (finally-only
    /// path, or a nested finally inside a catch that rethrew), pops the block and propagates
    /// the exception to the next outer handler by calling <see cref="Throw"/> recursively.
    /// Otherwise, simply pops the block (normal try-exit or cleanup after a catch body).
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// <see langword="true"/> if the exception was propagated to an outer handler;
    /// <see langword="false"/> if the block was popped with no propagation
    /// (normal exit, or unhandled exception that reached the top of the control-flow stack).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the innermost open block is not an <see cref="ExceptionBlock"/>.
    /// </exception>
    public bool EndFinally(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_blocks.TryPeek(out var top) && top is ExceptionBlock ex)
        {
            _blocks.Pop();
            if (ex.ExceptionInFlight)
                return Throw(context, ex.ThrownValue);
            return false;
        }
        throw new InvalidOperationException("ENDFINALLY used outside of an exception block.");
    }

    /// <summary>
    /// Returns the nearest enclosing block of type <typeparamref name="T"/> without removing it
    /// from the stack, or <see langword="null"/> when no such block is open.
    /// Useful for introspection (e.g. "are we inside a loop?") and diagnostic assertions.
    /// </summary>
    /// <typeparam name="T">The concrete block type to search for.</typeparam>
    /// <returns>The innermost open block of type <typeparamref name="T"/>, or <see langword="null"/>.</returns>
    public T? FindEnclosing<T>() where T : class, IControlFlowBlock
        => _blocks.OfType<T>().FirstOrDefault();

    /// <summary>
    /// Handles a THROW instruction: pops all blocks until the nearest <see cref="ExceptionBlock"/>
    /// whose <see cref="ExceptionBlock.Phase"/> is <see cref="ExceptionBlockPhase.Try"/> is found,
    /// stores the thrown value in <see cref="ExceptionBlock.ThrownValue"/>, and redirects
    /// <see cref="Context.InstructionPointer"/> according to conventional structured-exception
    /// semantics (catch executes before finally):
    /// <list type="bullet">
    ///   <item>If the block has a catch clause, jumps to <see cref="ExceptionBlock.CatchAddress"/>
    ///     first. When a finally clause is also present, its address is stored in
    ///     <see cref="ExceptionBlock.PendingFinallyAddress"/> so the ENDCATCH handler can jump
    ///     there after the catch body completes normally. The block transitions to
    ///     <see cref="ExceptionBlockPhase.Catch"/>.</item>
    ///   <item>If the block has only a finally clause (no catch), jumps directly to
    ///     <see cref="ExceptionBlock.FinallyAddress"/> and sets
    ///     <see cref="ExceptionBlock.ExceptionInFlight"/> so that <see cref="EndFinally"/> can
    ///     propagate to the next outer handler. The block transitions to
    ///     <see cref="ExceptionBlockPhase.Finally"/>.</item>
    /// </list>
    /// A throw from inside a <see cref="ExceptionBlockPhase.Catch"/> body whose block also has a
    /// <see cref="ExceptionBlock.FinallyAddress"/> redirects through that finally before propagating
    /// outward, so the finally always runs regardless of how the catch exits. A throw from inside
    /// a block already in <see cref="ExceptionBlockPhase.Finally"/>, or in
    /// <see cref="ExceptionBlockPhase.Catch"/> without a finally, skips that block entirely and
    /// propagates to the next outer try scope. The matched <see cref="ExceptionBlock"/> remains on
    /// the stack so that the handler body can read <see cref="ExceptionBlock.ThrownValue"/>;
    /// ENDCATCH or ENDTRY closes it.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <param name="value">The value being thrown.</param>
    /// <returns>
    /// <see langword="true"/> if a handler was found and the instruction pointer was updated;
    /// <see langword="false"/> if no handler is in scope (unhandled throw).
    /// </returns>
    public bool Throw(Context context, object? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        while (_blocks.TryPop(out var block))
        {
            if (block is ExceptionBlock ex)
            {
                // A throw from inside a catch body whose block also has a finally clause must
                // still run that finally before propagating outward (conventional semantics).
                if (ex.Phase == ExceptionBlockPhase.Catch && ex.FinallyAddress.HasValue)
                {
                    ex.ThrownValue = value;
                    ex.PendingFinallyAddress = null;
                    ex.ExceptionInFlight = true;
                    ex.Phase = ExceptionBlockPhase.Finally;
                    _blocks.Push(ex);
                    context.InstructionPointer = ex.FinallyAddress.Value;
                    return true;
                }

                // Skip blocks already in Finally phase, or in Catch phase without a finally.
                // A throw from these must propagate to the next outer handler.
                if (ex.Phase != ExceptionBlockPhase.Try)
                    continue;

                ex.ThrownValue = value;
                _blocks.Push(ex);
                if (ex.CatchAddress.HasValue)
                {
                    // Catch-first (conventional): jump to catch. If there is also a finally,
                    // store it so ENDCATCH can jump there after the catch body completes.
                    ex.PendingFinallyAddress = ex.FinallyAddress;
                    ex.Phase = ExceptionBlockPhase.Catch;
                    context.InstructionPointer = ex.CatchAddress.Value;
                }
                else
                {
                    // Finally-only: FinallyAddress is guaranteed non-null here.
                    // Exception remains in flight so EndFinally can propagate after cleanup.
                    ex.ExceptionInFlight = true;
                    ex.Phase = ExceptionBlockPhase.Finally;
                    context.InstructionPointer = ex.FinallyAddress.GetValueOrDefault();
                }
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// A <see cref="DefaultContext"/> that carries a <see cref="ControlFlowStack"/> for processors
/// implementing structured control flow (conditionals, loops, try/catch/finally).
/// </summary>
public class ControlFlowContext : DefaultContext
{
    /// <summary>Gets the control flow stack managing open blocks for the current execution.</summary>
    public ControlFlowStack ControlFlow { get; }

    /// <summary>
    /// Initializes a new instance with the given instruction stream and a fresh <see cref="ControlFlowStack"/>.
    /// </summary>
    /// <param name="data">The byte data containing the instruction stream.</param>
    public ControlFlowContext(ReadOnlyMemory<byte> data) : base(data)
    {
        ControlFlow = new ControlFlowStack();
    }

    /// <summary>
    /// Initializes a new instance with the given instruction stream and a caller-supplied
    /// <see cref="ControlFlowStack"/>, allowing pre-configuration or substitution.
    /// </summary>
    /// <param name="data">The byte data containing the instruction stream.</param>
    /// <param name="controlFlow">The control flow stack to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="controlFlow"/> is <see langword="null"/>.</exception>
    public ControlFlowContext(ReadOnlyMemory<byte> data, ControlFlowStack controlFlow) : base(data)
    {
        ControlFlow = controlFlow ?? throw new ArgumentNullException(nameof(controlFlow));
    }
}

/// <summary>
/// A <see cref="DefaultContext"/> combining an <see cref="ICallStack"/> for subroutine calls
/// with a <see cref="ControlFlowStack"/> for structured control flow.
/// Use this when the VM needs both call/return and if/loop/try semantics.
/// </summary>
public class FullContext : DefaultContext
{
    /// <summary>Gets the call stack used to track subroutine return addresses.</summary>
    public ICallStack CallStack { get; }

    /// <summary>Gets the control flow stack managing open structured blocks.</summary>
    public ControlFlowStack ControlFlow { get; }

    /// <summary>
    /// Initializes a new instance with a default <see cref="CallStack"/> and a fresh <see cref="ControlFlowStack"/>.
    /// </summary>
    /// <param name="data">The byte data containing the instruction stream.</param>
    public FullContext(ReadOnlyMemory<byte> data) : base(data)
    {
        CallStack = new CallStack();
        ControlFlow = new ControlFlowStack();
    }

    /// <summary>
    /// Initializes a new instance with a caller-supplied <see cref="ICallStack"/> implementation
    /// and a fresh <see cref="ControlFlowStack"/>.
    /// </summary>
    /// <param name="data">The byte data containing the instruction stream.</param>
    /// <param name="callStack">The call stack to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="callStack"/> is <see langword="null"/>.</exception>
    public FullContext(ReadOnlyMemory<byte> data, ICallStack callStack) : base(data)
    {
        CallStack = callStack ?? throw new ArgumentNullException(nameof(callStack));
        ControlFlow = new ControlFlowStack();
    }

    /// <summary>
    /// Initializes a new instance with caller-supplied <see cref="ICallStack"/> and
    /// <see cref="ControlFlowStack"/> implementations, allowing full substitution for testing
    /// or pre-configuration.
    /// </summary>
    /// <param name="data">The byte data containing the instruction stream.</param>
    /// <param name="callStack">The call stack to use.</param>
    /// <param name="controlFlow">The control flow stack to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <see langword="null"/>.</exception>
    public FullContext(ReadOnlyMemory<byte> data, ICallStack callStack, ControlFlowStack controlFlow) : base(data)
    {
        CallStack = callStack ?? throw new ArgumentNullException(nameof(callStack));
        ControlFlow = controlFlow ?? throw new ArgumentNullException(nameof(controlFlow));
    }
}
