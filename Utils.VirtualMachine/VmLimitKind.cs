namespace Utils.VirtualMachine;

/// <summary>
/// Identifies which configured resource limit was exhausted when a
/// <see cref="VmLimitExceededException"/> is thrown.
/// </summary>
public enum VmLimitKind
{
    /// <summary>The call-stack nesting depth configured by <see cref="VirtualMachineLimits.MaxCallStackDepth"/>.</summary>
    CallStackDepth,

    /// <summary>The control-flow block nesting depth configured by <see cref="VirtualMachineLimits.MaxControlFlowDepth"/>.</summary>
    ControlFlowDepth,

    /// <summary>The operand-stack depth configured by <see cref="VirtualMachineLimits.MaxOperandStackDepth"/>.</summary>
    OperandStackDepth,

    /// <summary>The per-execution instruction budget configured by <see cref="ExecutionLimits.MaxInstructions"/>.</summary>
    InstructionCount,

    /// <summary>The physical-page count configured by <see cref="VirtualMachineLimits.MaxPhysicalPages"/>.</summary>
    PhysicalPageCount,

    /// <summary>The memory-process count configured by <see cref="VirtualMachineLimits.MaxMemoryProcesses"/>.</summary>
    MemoryProcessCount,

    /// <summary>The scheduled-process count configured by <see cref="VirtualMachineLimits.MaxScheduledProcesses"/>.</summary>
    ScheduledProcessCount,
}
