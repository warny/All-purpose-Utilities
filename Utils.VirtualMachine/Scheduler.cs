using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.VirtualMachine;

/// <summary>
/// A cooperative, priority-based process scheduler that time-slices
/// <see cref="ScheduledProcess{T}"/> instances, giving each process its own
/// <see cref="VirtualProcessor{T}"/>.
/// </summary>
/// <typeparam name="T">The context type shared by all managed processes.</typeparam>
/// <remarks>
/// Not thread-safe. <see cref="Step"/> and <see cref="Run"/> must not be called
/// concurrently from multiple threads.
/// </remarks>
public class Scheduler<T> where T : Context
{
    private readonly List<ScheduledProcess<T>> _processes = [];
    private int _nextId;

    /// <summary>Gets the maximum number of instructions each process may execute per <see cref="Step"/> call.</summary>
    public int QuantumSteps { get; }

    /// <summary>Gets the list of all processes registered with this scheduler.</summary>
    public IReadOnlyList<ScheduledProcess<T>> Processes => _processes;

    /// <summary>
    /// Initializes a new <see cref="Scheduler{T}"/> with the specified quantum size.
    /// </summary>
    /// <param name="quantumSteps">
    /// Maximum instructions to execute per process per <see cref="Step"/> call. Defaults to <c>100</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="quantumSteps"/> is less than one.
    /// </exception>
    public Scheduler(int quantumSteps = 100)
    {
        if (quantumSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(quantumSteps), "Quantum must be at least 1.");
        QuantumSteps = quantumSteps;
    }

    /// <summary>
    /// Registers a new process with this scheduler.
    /// </summary>
    /// <param name="context">The execution context for the process.</param>
    /// <param name="processor">The virtual processor dedicated to this process.</param>
    /// <param name="priority">
    /// Initial scheduling priority. Higher values run before lower values in the same
    /// <see cref="Step"/> pass. Defaults to <c>0</c>.
    /// </param>
    /// <param name="name">
    /// Optional human-readable name for the process, used in diagnostics and logging.
    /// Accessible via <see cref="ScheduledProcess{T}.Name"/>. Defaults to <see langword="null"/>.
    /// </param>
    /// <returns>The newly created <see cref="ScheduledProcess{T}"/> in the <see cref="ProcessState.Ready"/> state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="processor"/> is <see langword="null"/>.</exception>
    public ScheduledProcess<T> AddProcess(T context, VirtualProcessor<T> processor, int priority = 0, string? name = null)
    {
        var process = new ScheduledProcess<T>(_nextId++, context, processor, priority, name);
        _processes.Add(process);
        return process;
    }

    /// <summary>
    /// Removes a process from this scheduler by its identifier.
    /// No-op if no process with the given <paramref name="processId"/> is registered.
    /// </summary>
    /// <param name="processId">The identifier of the process to remove.</param>
    public void RemoveProcess(int processId)
    {
        int index = _processes.FindIndex(p => p.ProcessId == processId);
        if (index >= 0) _processes.RemoveAt(index);
    }

    /// <summary>
    /// Executes one scheduling round: runs every <see cref="ProcessState.Ready"/> process
    /// in descending priority order, each for up to <see cref="QuantumSteps"/> instructions.
    /// A process may be interrupted early if it terminates, yields cooperatively, or suspends.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if at least one process ran during this pass;
    /// <see langword="false"/> if no processes were in the <see cref="ProcessState.Ready"/> state.
    /// </returns>
    public bool Step()
    {
        // Snapshot to avoid issues if _processes is modified inside a handler.
        var ready = _processes
            .Where(p => p.State == ProcessState.Ready)
            .OrderByDescending(p => p.Priority)
            .ToList();

        if (ready.Count == 0) return false;

        foreach (var process in ready)
        {
            // Recheck: a higher-priority process in this pass may have suspended or removed this one.
            if (process.State != ProcessState.Ready || !_processes.Contains(process))
                continue;

            process.SetState(ProcessState.Running);
            process.ClearYield();

            for (int i = 0; i < QuantumSteps; i++)
            {
                if (!process.Processor.ExecuteStep(process.Context))
                {
                    process.SetState(ProcessState.Terminated);
                    break;
                }

                // Check after ExecuteStep: yield or suspension may have been requested
                // from inside the instruction handler that just ran.
                if (process.YieldRequested || process.State == ProcessState.Suspended)
                {
                    process.ClearYield();
                    if (process.State == ProcessState.Running)
                        process.SetState(ProcessState.Ready);
                    break;
                }
            }

            // Quantum exhausted without an explicit break; check whether the last instruction
            // called Terminate() (IP < 0) or ran past the bytecode end (IP >= Data.Length).
            if (process.State == ProcessState.Running)
            {
                bool contextDone = process.Context.InstructionPointer < 0
                    || process.Context.InstructionPointer >= process.Context.Data.Length;
                process.SetState(contextDone ? ProcessState.Terminated : ProcessState.Ready);
            }
        }

        return true;
    }

    /// <summary>
    /// Runs the scheduler until no process remains in the <see cref="ProcessState.Ready"/>
    /// or <see cref="ProcessState.Running"/> state, or until cancellation is requested.
    /// Returns immediately when all processes are <see cref="ProcessState.Terminated"/> or
    /// <see cref="ProcessState.Suspended"/> (including when there are no processes at all).
    /// </summary>
    /// <param name="cancellationToken">Token that can interrupt the loop between steps.</param>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public void Run(CancellationToken cancellationToken = default)
    {
        while (_processes.Any(p => p.State is ProcessState.Ready or ProcessState.Running))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Step();
        }
    }

    /// <summary>
    /// Asynchronously runs the scheduler until no process remains in the
    /// <see cref="ProcessState.Ready"/> or <see cref="ProcessState.Running"/> state,
    /// or until cancellation is requested. Yields to the caller between each
    /// <see cref="Step"/> call so the calling thread is not blocked for the duration.
    /// </summary>
    /// <param name="cancellationToken">Token that can interrupt the loop between steps.</param>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (_processes.Any(p => p.State is ProcessState.Ready or ProcessState.Running))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Step();
            await Task.Yield();
        }
    }
}
