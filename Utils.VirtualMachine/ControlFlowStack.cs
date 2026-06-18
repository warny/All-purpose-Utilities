using System;
using System.Collections.Generic;

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

    /// <summary>Gets the innermost open block, or <see langword="null"/> when the stack is empty.</summary>
    public IControlFlowBlock? CurrentBlock => _blocks.TryPeek(out var b) ? b : null;

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
    /// Handles a BREAK instruction: pops all blocks up to and including the nearest
    /// <see cref="LoopBlock"/>, then sets <see cref="Context.InstructionPointer"/> to
    /// the loop's <see cref="LoopBlock.EndAddress"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no enclosing loop is in scope.</exception>
    public void Break(Context context)
    {
        while (_blocks.TryPop(out var block))
        {
            if (block is LoopBlock loop)
            {
                context.InstructionPointer = loop.EndAddress;
                return;
            }
        }
        throw new InvalidOperationException("BREAK used outside of a loop.");
    }

    /// <summary>
    /// Handles a CONTINUE instruction: pops all blocks nested inside the nearest
    /// <see cref="LoopBlock"/> (the loop itself stays on the stack), then sets
    /// <see cref="Context.InstructionPointer"/> to the loop's <see cref="LoopBlock.StartAddress"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no enclosing loop is in scope.</exception>
    public void Continue(Context context)
    {
        while (_blocks.TryPeek(out var block))
        {
            if (block is LoopBlock loop)
            {
                context.InstructionPointer = loop.StartAddress;
                return;
            }
            _blocks.Pop();
        }
        throw new InvalidOperationException("CONTINUE used outside of a loop.");
    }

    /// <summary>
    /// Handles a THROW instruction: pops all blocks until the nearest <see cref="ExceptionBlock"/>
    /// is found, stores the thrown value in <see cref="ExceptionBlock.ThrownValue"/>, and
    /// redirects <see cref="Context.InstructionPointer"/> to the catch handler (or finally if
    /// no catch is defined). The <see cref="ExceptionBlock"/> remains on the stack so that
    /// the handler body can read <see cref="ExceptionBlock.ThrownValue"/>; ENDTRY pops it.
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
                ex.ThrownValue = value;
                _blocks.Push(ex);
                context.InstructionPointer = ex.CatchAddress ?? ex.FinallyAddress!.Value;
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
    public ControlFlowStack ControlFlow { get; } = new();

    /// <summary>
    /// Initializes a new instance with the given instruction stream.
    /// </summary>
    /// <param name="data">The byte array containing the instruction stream.</param>
    public ControlFlowContext(byte[] data) : base(data) { }
}
