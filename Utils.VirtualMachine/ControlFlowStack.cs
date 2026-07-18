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
    private readonly Stack<IControlFlowBlock> _blocks = new();

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

    /// <summary>Opens a conditional (if/else) block.</summary>
    /// <param name="startAddress">Address of the IF instruction.</param>
    /// <param name="endAddress">Address immediately after the ENDIF.</param>
    /// <param name="elseAddress">Address of the ELSE branch, or <see langword="null"/> if absent.</param>
    public void PushConditional(int startAddress, int endAddress, int? elseAddress = null)
        => _blocks.Push(new ConditionalBlock(startAddress, endAddress, elseAddress));

    /// <summary>Opens a loop block.</summary>
    /// <param name="startAddress">Address of the loop header; target of CONTINUE.</param>
    /// <param name="endAddress">Address after the loop; target of BREAK.</param>
    public void PushLoop(int startAddress, int endAddress)
        => _blocks.Push(new LoopBlock(startAddress, endAddress));

    /// <summary>Opens a try/catch/finally block.</summary>
    /// <param name="startAddress">Address of the TRY instruction.</param>
    /// <param name="catchAddress">Address of the catch handler, or <see langword="null"/>.</param>
    /// <param name="finallyAddress">Address of the finally block, or <see langword="null"/>.</param>
    public void PushException(int startAddress, int? catchAddress, int? finallyAddress)
        => _blocks.Push(new ExceptionBlock(startAddress, catchAddress, finallyAddress));

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
    /// Handles an ENDFINALLY instruction: if a catch handler is pending (stored by a prior
    /// <see cref="Throw"/> call when both <see cref="ExceptionBlock.CatchAddress"/> and
    /// <see cref="ExceptionBlock.FinallyAddress"/> were set), redirects
    /// <see cref="Context.InstructionPointer"/> to that catch handler and clears the pending
    /// address, then transitions the block to <see cref="ExceptionBlockPhase.Catch"/>.
    /// When no pending catch is present but an exception is in flight (finally-only
    /// block, or the innermost finally in a nested try), pops the block and propagates the
    /// exception to the next outer handler by calling <see cref="Throw"/> recursively.
    /// Otherwise, simply pops the block (normal exit from a try/finally without an exception).
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// <see langword="true"/> if execution was redirected to a catch handler (either a pending
    /// one on this block or an outer one found via propagation);
    /// <see langword="false"/> if the block was popped with no handler to redirect to
    /// (normal exit, or unhandled exception that reached the top of the control-flow stack).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the innermost open block is not an <see cref="ExceptionBlock"/>.
    /// </exception>
    public bool EndFinally(Context context)
    {
        if (_blocks.TryPeek(out var top) && top is ExceptionBlock ex)
        {
            if (ex.PendingCatchAddress.HasValue)
            {
                context.InstructionPointer = ex.PendingCatchAddress.Value;
                ex.PendingCatchAddress = null;
                ex.Phase = ExceptionBlockPhase.Catch;
                return true;
            }
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
    /// <see cref="Context.InstructionPointer"/> according to the following rules:
    /// <list type="bullet">
    ///   <item>If the block has a finally clause, jumps to <see cref="ExceptionBlock.FinallyAddress"/>
    ///     first. When a catch clause is also present, its address is stored in
    ///     <see cref="ExceptionBlock.PendingCatchAddress"/> so the ENDFINALLY handler can jump there
    ///     after the finally block completes. The block transitions to
    ///     <see cref="ExceptionBlockPhase.Finally"/>.</item>
    ///   <item>If the block has only a catch clause (no finally), jumps directly to
    ///     <see cref="ExceptionBlock.CatchAddress"/>. The block transitions to
    ///     <see cref="ExceptionBlockPhase.Catch"/>.</item>
    /// </list>
    /// Blocks already in <see cref="ExceptionBlockPhase.Finally"/> or
    /// <see cref="ExceptionBlockPhase.Catch"/> are skipped so that a throw from inside a handler
    /// body propagates to the next outer try scope instead of re-entering the current handler.
    /// The matched <see cref="ExceptionBlock"/> remains on the stack so that the handler body can
    /// read <see cref="ExceptionBlock.ThrownValue"/>; ENDTRY pops it.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <param name="value">The value being thrown.</param>
    /// <returns>
    /// <see langword="true"/> if a handler was found and the instruction pointer was updated;
    /// <see langword="false"/> if no handler is in scope (unhandled throw).
    /// </returns>
    public bool Throw(Context context, object? value)
    {
        while (_blocks.TryPop(out var block))
        {
            if (block is ExceptionBlock ex)
            {
                // Skip exception blocks that are already executing their own handler.
                // A throw from inside catch or finally must propagate to the next outer handler.
                if (ex.Phase != ExceptionBlockPhase.Try)
                    continue;

                ex.ThrownValue = value;
                ex.ExceptionInFlight = true;
                _blocks.Push(ex);
                if (ex.FinallyAddress.HasValue)
                {
                    // Always run finally first. If there is also a catch, store it so ENDFINALLY can jump there.
                    ex.PendingCatchAddress = ex.CatchAddress;
                    ex.Phase = ExceptionBlockPhase.Finally;
                    context.InstructionPointer = ex.FinallyAddress.Value;
                }
                else
                {
                    ex.Phase = ExceptionBlockPhase.Catch;
                    context.InstructionPointer = ex.CatchAddress!.Value;
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
