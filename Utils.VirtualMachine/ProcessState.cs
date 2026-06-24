namespace Utils.VirtualMachine;

/// <summary>
/// Represents the lifecycle state of a process managed by <see cref="Scheduler{T}"/>.
/// </summary>
public enum ProcessState
{
    /// <summary>The process is eligible to run and will be scheduled in the next <see cref="Scheduler{T}.Step"/> call.</summary>
    Ready,

    /// <summary>The process is currently executing its quantum.</summary>
    Running,

    /// <summary>The process has been suspended and will not run until <see cref="ScheduledProcess{T}.Resume"/> is called.</summary>
    Suspended,

    /// <summary>The process has terminated and will never run again.</summary>
    Terminated,
}
