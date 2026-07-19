namespace Utils.VirtualMachine;

/// <summary>
/// Represents an active loop on a <see cref="ControlFlowStack"/>.
/// </summary>
/// <remarks>
/// Typical VM usage:
/// <list type="bullet">
///   <item>LOOP instruction: push a <see cref="LoopBlock"/> with <see cref="StartAddress"/> set to
///     the first instruction of the loop <em>body</em> (i.e. the byte immediately after the LOOP
///     instruction itself) and <see cref="EndAddress"/> set to the first byte after the loop.</item>
///   <item>ENDLOOP instruction: jump to <see cref="StartAddress"/> to re-enter the body (the block
///     stays on the stack for the next iteration).</item>
///   <item>BREAK instruction: call <see cref="ControlFlowStack.Break"/>; pops up to and
///     including this block, then jumps to <see cref="EndAddress"/>.</item>
///   <item>CONTINUE instruction: call <see cref="ControlFlowStack.Continue"/>; pops any blocks
///     nested inside this loop, then jumps to <see cref="StartAddress"/>.</item>
/// </list>
/// <para>
/// <b>Important:</b> <see cref="StartAddress"/> must not point at the LOOP instruction itself.
/// If ENDLOOP jumped back to LOOP, the LOOP handler would push a second <see cref="LoopBlock"/>
/// on every iteration, growing the <see cref="ControlFlowStack"/> without bound. Set
/// <see cref="StartAddress"/> to the IP value already advanced past the LOOP operands, as returned
/// by <see cref="Context.InstructionPointer"/> when the LOOP handler is executing.
/// </para>
/// </remarks>
public sealed class LoopBlock : IControlFlowBlock
{
    /// <inheritdoc/>
    /// <remarks>This is the target of a CONTINUE instruction.</remarks>
    public int StartAddress { get; }

    /// <summary>First instruction after the loop body; target of a BREAK instruction.</summary>
    public int EndAddress { get; }

    /// <summary>
    /// Initializes a new loop block.
    /// </summary>
    /// <param name="startAddress">Address of the loop header (CONTINUE jumps here).</param>
    /// <param name="endAddress">Address immediately after the loop (BREAK jumps here).</param>
    public LoopBlock(int startAddress, int endAddress)
    {
        StartAddress = startAddress;
        EndAddress = endAddress;
    }
}
