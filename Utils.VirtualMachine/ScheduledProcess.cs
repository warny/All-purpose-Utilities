using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Wraps a context and dedicated processor pair with scheduling metadata, allowing
/// <see cref="Scheduler{T}"/> to track lifecycle state, priority, and cooperative yield signals.
/// </summary>
/// <typeparam name="T">The context type, constrained to <see cref="Context"/>.</typeparam>
/// <remarks>
/// Instances are created exclusively by <see cref="Scheduler{T}.AddProcess"/>.
/// </remarks>
public sealed class ScheduledProcess<T> where T : Context
{
    private volatile ProcessState _state = ProcessState.Ready;
    private volatile bool _yieldRequested;

    /// <summary>Gets the unique identifier for this process within its scheduler.</summary>
    public int ProcessId { get; }

    /// <summary>
    /// Gets or sets the scheduling priority. Higher values run first in each
    /// <see cref="Scheduler{T}.Step"/> pass. May be changed at runtime between quanta.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>Gets the current lifecycle state of this process.</summary>
    public ProcessState State => _state;

    /// <summary>Gets the execution context (instruction pointer, data, stack, etc.).</summary>
    public T Context { get; }

    /// <summary>Gets the virtual processor dedicated to this process.</summary>
    public VirtualProcessor<T> Processor { get; }

    /// <summary>
    /// Gets a value indicating whether a cooperative yield has been requested.
    /// The scheduler reads this flag after each <see cref="VirtualProcessor{T}.ExecuteStep"/>
    /// call and interrupts the quantum when it is <see langword="true"/>.
    /// </summary>
    public bool YieldRequested => _yieldRequested;

    internal ScheduledProcess(int processId, T context, VirtualProcessor<T> processor, int priority)
    {
        ProcessId = processId;
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Processor = processor ?? throw new ArgumentNullException(nameof(processor));
        Priority = priority;
    }

    /// <summary>
    /// Suspends the process. Transitions <see cref="ProcessState.Ready"/> or
    /// <see cref="ProcessState.Running"/> to <see cref="ProcessState.Suspended"/>.
    /// No-op when already <see cref="ProcessState.Suspended"/> or
    /// <see cref="ProcessState.Terminated"/>.
    /// </summary>
    public void Suspend()
    {
        if (_state == ProcessState.Terminated || _state == ProcessState.Suspended) return;
        _state = ProcessState.Suspended;
    }

    /// <summary>
    /// Resumes a suspended process, transitioning it to <see cref="ProcessState.Ready"/>.
    /// No-op when the process is not in the <see cref="ProcessState.Suspended"/> state.
    /// </summary>
    public void Resume()
    {
        if (_state == ProcessState.Suspended)
            _state = ProcessState.Ready;
    }

    /// <summary>
    /// Requests a cooperative yield. After the currently executing instruction completes,
    /// the scheduler ends the quantum early and returns this process to
    /// <see cref="ProcessState.Ready"/> so other processes can run.
    /// </summary>
    public void RequestYield() => _yieldRequested = true;

    /// <summary>Sets the process state. Called exclusively by <see cref="Scheduler{T}"/>.</summary>
    internal void SetState(ProcessState state) => _state = state;

    /// <summary>
    /// Clears the yield flag. Called by <see cref="Scheduler{T}"/> before starting each
    /// quantum to prevent a stale yield request from the previous quantum from taking effect.
    /// </summary>
    internal void ClearYield() => _yieldRequested = false;
}
