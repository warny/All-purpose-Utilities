using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Immutable execution parameters for a single call to
/// <see cref="VirtualProcessor{T}.Execute(T, ExecutionLimits, System.Threading.CancellationToken)"/>,
/// <see cref="Scheduler{T}.Run(ExecutionLimits, System.Threading.CancellationToken)"/>, or
/// <see cref="Scheduler{T}.RunAsync(ExecutionLimits, System.Threading.CancellationToken)"/>.
/// </summary>
/// <remarks>
/// Does not store a <see cref="System.Threading.CancellationToken"/>; cancellation remains
/// a direct runtime argument of execution methods.
/// Use <see cref="Unlimited"/> to express no instruction budget.
/// </remarks>
public sealed class ExecutionLimits
{
    /// <summary>
    /// Gets the maximum number of instructions to dispatch in one execution call,
    /// or <see langword="null"/> when execution is unlimited.
    /// </summary>
    /// <remarks>When non-null, the value is always at least 1.</remarks>
    public long? MaxInstructions { get; }

    /// <summary>
    /// Gets the shared unlimited instance: no instruction budget is applied.
    /// </summary>
    public static ExecutionLimits Unlimited { get; } = new ExecutionLimits();

    /// <summary>
    /// Initializes an unlimited <see cref="ExecutionLimits"/> instance.
    /// Prefer <see cref="Unlimited"/> over constructing a new instance.
    /// </summary>
    public ExecutionLimits()
    {
        MaxInstructions = null;
    }

    /// <summary>
    /// Initializes a new <see cref="ExecutionLimits"/> with the specified instruction budget.
    /// </summary>
    /// <param name="maxInstructions">
    /// Maximum number of instructions to dispatch. Must be at least 1.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxInstructions"/> is less than one.</exception>
    public ExecutionLimits(long maxInstructions)
    {
        if (maxInstructions < 1)
            throw new ArgumentOutOfRangeException(nameof(maxInstructions),
                "MaxInstructions must be at least 1 when specified. Use ExecutionLimits.Unlimited for no budget.");
        MaxInstructions = maxInstructions;
    }
}
