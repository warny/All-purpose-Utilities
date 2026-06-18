namespace Utils.VirtualMachine;

/// <summary>
/// Base interface for all structured control flow blocks that can be pushed onto
/// a <see cref="ControlFlowStack"/>: conditionals, loops, and exception handlers.
/// </summary>
/// <remarks>
/// The three concrete implementations are <see cref="ConditionalBlock"/>,
/// <see cref="LoopBlock"/>, and <see cref="ExceptionBlock"/>.
/// No bytecode is associated at this level; opcodes are assigned by each concrete VM.
/// </remarks>
public interface IControlFlowBlock
{
    /// <summary>Gets the instruction-stream offset at which this block was opened.</summary>
    int StartAddress { get; }
}
