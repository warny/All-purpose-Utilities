namespace Utils.VirtualMachine;

/// <summary>
/// Represents an active loop on a <see cref="ControlFlowStack"/>.
/// </summary>
/// <remarks>
/// Typical VM usage:
/// <list type="bullet">
///   <item>LOOP instruction: push a <see cref="LoopBlock"/> with the current IP as
///     <see cref="StartAddress"/> and the first byte after the loop as <see cref="EndAddress"/>.</item>
///   <item>ENDLOOP instruction: jump to <see cref="StartAddress"/> to re-evaluate the condition
///     (the block stays on the stack for the next iteration).</item>
///   <item>BREAK instruction: call <see cref="ControlFlowStack.Break"/>; pops up to and
///     including this block, then jumps to <see cref="EndAddress"/>.</item>
///   <item>CONTINUE instruction: call <see cref="ControlFlowStack.Continue"/>; pops any blocks
///     nested inside this loop, then jumps to <see cref="StartAddress"/>.</item>
/// </list>
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
