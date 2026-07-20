using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Immutable structural configuration for a virtual machine instance.
/// Encapsulates capacity limits for stacks, scheduler, and virtual memory.
/// </summary>
/// <remarks>
/// All structural capacities must be at least one. Hard implementation ceilings
/// (e.g. <see cref="int.MaxValue"/> identifier exhaustion) remain separate from
/// caller-configured limits stored here.
/// Use <see cref="Default"/> for behavior identical to unconfigured classes.
/// </remarks>
public sealed class VirtualMachineLimits
{
    /// <summary>Gets the maximum number of active call-stack frames. Defaults to <c>512</c>.</summary>
    public int MaxCallStackDepth { get; }

    /// <summary>Gets the maximum number of nested control-flow blocks. Defaults to <c>1024</c>.</summary>
    public int MaxControlFlowDepth { get; }

    /// <summary>Gets the maximum number of operand-stack elements. Defaults to <c>1024</c>.</summary>
    public int MaxOperandStackDepth { get; }

    /// <summary>
    /// Gets the maximum number of simultaneously allocated physical pages.
    /// Defaults to <see cref="int.MaxValue"/> (effectively unlimited, subject to hard identifier exhaustion).
    /// </summary>
    public int MaxPhysicalPages { get; }

    /// <summary>
    /// Gets the maximum number of simultaneously registered memory processes (including the master).
    /// Defaults to <see cref="int.MaxValue"/> (effectively unlimited, subject to hard identifier exhaustion).
    /// </summary>
    public int MaxMemoryProcesses { get; }

    /// <summary>
    /// Gets the maximum number of simultaneously registered scheduled processes.
    /// Defaults to <see cref="int.MaxValue"/> (effectively unlimited, subject to hard identifier exhaustion).
    /// </summary>
    public int MaxScheduledProcesses { get; }

    /// <summary>Gets the maximum instructions per process per scheduler quantum. Defaults to <c>100</c>.</summary>
    public int SchedulerQuantumSteps { get; }

    /// <summary>
    /// Gets the shared validated default instance, matching the behavior of all unconfigured VM classes.
    /// </summary>
    public static VirtualMachineLimits Default { get; } = new VirtualMachineLimits();

    /// <summary>
    /// Initializes a new <see cref="VirtualMachineLimits"/> with the specified capacities.
    /// All parameters default to values matching the current unconfigured behavior.
    /// </summary>
    /// <param name="maxCallStackDepth">Maximum call-stack depth. Must be at least 1.</param>
    /// <param name="maxControlFlowDepth">Maximum control-flow nesting depth. Must be at least 1.</param>
    /// <param name="maxOperandStackDepth">Maximum operand-stack depth. Must be at least 1.</param>
    /// <param name="maxPhysicalPages">Maximum simultaneously allocated pages. Must be at least 1.</param>
    /// <param name="maxMemoryProcesses">Maximum simultaneously registered memory processes (master included). Must be at least 1.</param>
    /// <param name="maxScheduledProcesses">Maximum simultaneously registered scheduled processes. Must be at least 1.</param>
    /// <param name="schedulerQuantumSteps">Maximum instructions per process per scheduler step. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is less than one.</exception>
    public VirtualMachineLimits(
        int maxCallStackDepth = 512,
        int maxControlFlowDepth = 1024,
        int maxOperandStackDepth = 1024,
        int maxPhysicalPages = int.MaxValue,
        int maxMemoryProcesses = int.MaxValue,
        int maxScheduledProcesses = int.MaxValue,
        int schedulerQuantumSteps = 100)
    {
        if (maxCallStackDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCallStackDepth), "MaxCallStackDepth must be at least 1.");
        if (maxControlFlowDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxControlFlowDepth), "MaxControlFlowDepth must be at least 1.");
        if (maxOperandStackDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxOperandStackDepth), "MaxOperandStackDepth must be at least 1.");
        if (maxPhysicalPages < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPhysicalPages), "MaxPhysicalPages must be at least 1.");
        if (maxMemoryProcesses < 1)
            throw new ArgumentOutOfRangeException(nameof(maxMemoryProcesses), "MaxMemoryProcesses must be at least 1.");
        if (maxScheduledProcesses < 1)
            throw new ArgumentOutOfRangeException(nameof(maxScheduledProcesses), "MaxScheduledProcesses must be at least 1.");
        if (schedulerQuantumSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(schedulerQuantumSteps), "SchedulerQuantumSteps must be at least 1.");

        MaxCallStackDepth = maxCallStackDepth;
        MaxControlFlowDepth = maxControlFlowDepth;
        MaxOperandStackDepth = maxOperandStackDepth;
        MaxPhysicalPages = maxPhysicalPages;
        MaxMemoryProcesses = maxMemoryProcesses;
        MaxScheduledProcesses = maxScheduledProcesses;
        SchedulerQuantumSteps = schedulerQuantumSteps;
    }
}
