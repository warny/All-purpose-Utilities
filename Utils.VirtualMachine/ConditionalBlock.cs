namespace Utils.VirtualMachine;

/// <summary>
/// Represents an active if/else conditional block on a <see cref="ControlFlowStack"/>.
/// </summary>
/// <remarks>
/// Typical VM usage:
/// <list type="bullet">
///   <item>IF instruction: evaluate condition; push a <see cref="ConditionalBlock"/>;
///     if false, jump to <see cref="ElseAddress"/> (or <see cref="EndAddress"/> when no else).</item>
///   <item>ELSE instruction: jump to <see cref="EndAddress"/>; execution continues in the else branch.</item>
///   <item>ENDIF instruction: call <see cref="ControlFlowStack.Pop"/>.</item>
/// </list>
/// </remarks>
public sealed class ConditionalBlock : IControlFlowBlock
{
    /// <inheritdoc/>
    public int StartAddress { get; }

    /// <summary>
    /// Address of the else branch, or <see langword="null"/> when there is no else clause.
    /// </summary>
    public int? ElseAddress { get; }

    /// <summary>First instruction after the entire if/else structure (ENDIF address).</summary>
    public int EndAddress { get; }

    /// <summary>
    /// Initializes a new conditional block.
    /// </summary>
    /// <param name="startAddress">Address of the IF instruction.</param>
    /// <param name="endAddress">Address immediately after the ENDIF.</param>
    /// <param name="elseAddress">Address of the ELSE branch, or <see langword="null"/> if absent.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when any non-null address is negative. Negative values are the termination sentinel
    /// and must not be used as branch targets.
    /// </exception>
    public ConditionalBlock(int startAddress, int endAddress, int? elseAddress = null)
    {
        if (startAddress < 0)
            throw new System.ArgumentOutOfRangeException(nameof(startAddress), "Branch target addresses must be non-negative.");
        if (endAddress < 0)
            throw new System.ArgumentOutOfRangeException(nameof(endAddress), "Branch target addresses must be non-negative.");
        if (elseAddress.HasValue && elseAddress.Value < 0)
            throw new System.ArgumentOutOfRangeException(nameof(elseAddress), "Branch target addresses must be non-negative.");
        StartAddress = startAddress;
        EndAddress = endAddress;
        ElseAddress = elseAddress;
    }
}
