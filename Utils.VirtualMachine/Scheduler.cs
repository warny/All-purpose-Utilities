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

    /// <summary>
    /// Gets the total number of instructions dispatched by this scheduler since it was created.
    /// Incremented by <see cref="Step"/> and never reset. Callers may snapshot this value before
    /// a <see cref="Run"/> or <see cref="RunAsync"/> call to compute the per-run instruction count.
    /// </summary>
    public long InstructionsExecuted { get; private set; }

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
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="context"/> is already registered with this scheduler. Sharing a
    /// single context across multiple processes produces contradictory lifecycle states and corrupts
    /// instruction pointer and stack state.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the process identifier counter has reached its maximum value.</exception>
    public ScheduledProcess<T> AddProcess(T context, VirtualProcessor<T> processor, int priority = 0, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(processor);
        if (_nextId == int.MaxValue)
            throw new InvalidOperationException(
                $"Cannot add more processes: the process identifier counter has reached its maximum value ({int.MaxValue}).");
        if (_processes.Any(p => ReferenceEquals(p.Context, context)))
            throw new ArgumentException(
                "The supplied context is already registered with this scheduler. " +
                "Each process must own a distinct context instance.", nameof(context));

        var process = new ScheduledProcess<T>(_nextId++, context, processor, priority, name);
        _processes.Add(process);
        return process;
    }

    /// <summary>
    /// Removes a process from this scheduler by its identifier.
    /// No-op if no process with the given <paramref name="processId"/> is registered.
    /// The process is transitioned to <see cref="ProcessState.Terminated"/> before removal so
    /// that the scheduler's quantum-end cleanup block does not later promote it back to
    /// <see cref="ProcessState.Ready"/> when the process removed itself from inside a handler.
    /// </summary>
    /// <param name="processId">The identifier of the process to remove.</param>
    public void RemoveProcess(int processId)
    {
        int index = _processes.FindIndex(p => p.ProcessId == processId);
        if (index < 0) return;
        var process = _processes[index];
        _processes.RemoveAt(index);
        process.SetState(ProcessState.Terminated);
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
    public bool Step() => StepCore(remainingBudget: 0, originalBudget: 0);

    /// <summary>
    /// Core implementation shared by <see cref="Step"/> and the budget-aware paths in
    /// <see cref="Run"/> and <see cref="RunAsync"/>. Executes one scheduling round with an
    /// optional per-call instruction cap.
    /// </summary>
    /// <param name="remainingBudget">
    /// Maximum instructions allowed across all processes in this call.
    /// Zero means unlimited (the <see cref="Step"/> case).
    /// </param>
    /// <param name="originalBudget">
    /// The <c>maxInstructions</c> value supplied to <see cref="Run"/>/<see cref="RunAsync"/>;
    /// reported in <see cref="InstructionBudgetExceededException.Budget"/> when the limit is hit.
    /// </param>
    private bool StepCore(long remainingBudget, long originalBudget)
    {
        // Snapshot to avoid issues if _processes is modified inside a handler.
        var ready = _processes
            .Where(p => p.State == ProcessState.Ready)
            .OrderByDescending(p => p.Priority)
            .ToList();

        if (ready.Count == 0) return false;

        long stepInstructions = 0;

        try
        {
            foreach (var process in ready)
            {
                // Recheck: a higher-priority process in this pass may have suspended or removed this one.
                if (process.State != ProcessState.Ready || !_processes.Contains(process))
                    continue;

                process.SetState(ProcessState.Running);
                process.ClearYield();

                try
                {
                    for (int i = 0; i < QuantumSteps; i++)
                    {
                        // Item 11: recheck membership before each instruction so that a handler that
                        // calls RemoveProcess on its own process stops further execution immediately.
                        if (!_processes.Contains(process)) break;

                        // Budget enforcement: checked before each instruction so the total number of
                        // instructions dispatched in this StepCore call never exceeds remainingBudget.
                        if (remainingBudget > 0 && stepInstructions >= remainingBudget)
                            throw new InstructionBudgetExceededException(originalBudget);

                        if (!process.Processor.ExecuteStep(process.Context))
                        {
                            // Item 45: validate structural completion before marking as Terminated.
                            // If ValidateCompletion throws, the exception is caught below and the
                            // process transitions to Faulted instead.
                            process.Processor.ValidateCompletion(process.Context);
                            process.SetState(ProcessState.Terminated);
                            break;
                        }
                        stepInstructions++;

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
                        if (contextDone)
                        {
                            // Item 45: validate structural completion before marking as Terminated.
                            process.Processor.ValidateCompletion(process.Context);
                        }
                        process.SetState(contextDone ? ProcessState.Terminated : ProcessState.Ready);
                    }
                }
                catch (InstructionBudgetExceededException)
                {
                    // Restore the process to Ready so it can resume after the budget exception is
                    // caught by the caller and a new Run() call is made.
                    if (process.State == ProcessState.Running)
                        process.SetState(ProcessState.Ready);
                    throw;
                }
                catch (Exception ex)
                {
                    // Item 2: transition to Faulted so Run/RunAsync do not loop forever
                    // waiting for a process that will never leave the Running state.
                    process.SetFaulted(ex);
                }
            }
        }
        finally
        {
            // Always update the global counter, even when a budget exception is thrown mid-step,
            // so InstructionsExecuted reflects the instructions actually dispatched.
            InstructionsExecuted += stepInstructions;
        }

        return true;
    }

    /// <summary>
    /// Runs the scheduler until no process remains in the <see cref="ProcessState.Ready"/>
    /// or <see cref="ProcessState.Running"/> state, until cancellation is requested, or until
    /// <paramref name="maxInstructions"/> instructions have been dispatched in this call.
    /// Returns immediately when all processes are <see cref="ProcessState.Terminated"/> or
    /// <see cref="ProcessState.Suspended"/> (including when there are no processes at all).
    /// </summary>
    /// <param name="cancellationToken">Token that can interrupt the loop between steps.</param>
    /// <param name="maxInstructions">
    /// Maximum number of instructions to dispatch during this call. When greater than zero,
    /// throws <see cref="InstructionBudgetExceededException"/> if the limit is reached before
    /// all processes terminate. When zero (the default), execution is unlimited.
    /// The count is relative to <see cref="InstructionsExecuted"/> at the start of this call.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxInstructions"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InstructionBudgetExceededException">
    /// Thrown when <paramref name="maxInstructions"/> is greater than zero and that many
    /// instructions have been dispatched in this call without all processes terminating.
    /// The total number of instructions dispatched never exceeds <paramref name="maxInstructions"/>.
    /// </exception>
    public void Run(CancellationToken cancellationToken = default, long maxInstructions = 0)
    {
        if (maxInstructions < 0)
            throw new ArgumentOutOfRangeException(nameof(maxInstructions),
                "maxInstructions must be zero (unlimited) or a positive budget.");

        long startCount = InstructionsExecuted;
        // Faulted is treated as terminal: a faulted process will never be rescheduled.
        while (_processes.Any(p => p.State is ProcessState.Ready or ProcessState.Running))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maxInstructions > 0)
            {
                long remaining = maxInstructions - (InstructionsExecuted - startCount);
                if (remaining <= 0)
                    throw new InstructionBudgetExceededException(maxInstructions);
                StepCore(remaining, maxInstructions);
            }
            else
            {
                Step();
            }
        }
    }

    /// <summary>
    /// Asynchronously runs the scheduler until no process remains in the
    /// <see cref="ProcessState.Ready"/> or <see cref="ProcessState.Running"/> state,
    /// until cancellation is requested, or until <paramref name="maxInstructions"/> instructions
    /// have been dispatched in this call. Yields to the caller between each
    /// <see cref="Step"/> call so the calling thread is not blocked for the duration.
    /// </summary>
    /// <remarks>
    /// One <see cref="Step"/> call executes up to <see cref="QuantumSteps"/> instructions for
    /// every ready process in priority order. With many processes, a large quantum, or slow
    /// handlers, each step can still occupy the calling thread for a significant amount of time.
    /// Cancellation is only checked between steps, not within a single step. If tighter
    /// granularity is required, reduce <see cref="QuantumSteps"/> or have handlers check the
    /// token themselves.
    /// </remarks>
    /// <param name="cancellationToken">Token that can interrupt the loop between steps.</param>
    /// <param name="maxInstructions">
    /// Maximum number of instructions to dispatch during this call. When greater than zero,
    /// throws <see cref="InstructionBudgetExceededException"/> if the limit is reached before
    /// all processes terminate. When zero (the default), execution is unlimited.
    /// The count is relative to <see cref="InstructionsExecuted"/> at the start of this call.
    /// The total number of instructions dispatched never exceeds <paramref name="maxInstructions"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxInstructions"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InstructionBudgetExceededException">
    /// Thrown when <paramref name="maxInstructions"/> is greater than zero and that many
    /// instructions have been dispatched in this call without all processes terminating.
    /// </exception>
    public async Task RunAsync(CancellationToken cancellationToken = default, long maxInstructions = 0)
    {
        if (maxInstructions < 0)
            throw new ArgumentOutOfRangeException(nameof(maxInstructions),
                "maxInstructions must be zero (unlimited) or a positive budget.");

        long startCount = InstructionsExecuted;
        while (_processes.Any(p => p.State is ProcessState.Ready or ProcessState.Running))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maxInstructions > 0)
            {
                long remaining = maxInstructions - (InstructionsExecuted - startCount);
                if (remaining <= 0)
                    throw new InstructionBudgetExceededException(maxInstructions);
                StepCore(remaining, maxInstructions);
            }
            else
            {
                Step();
            }
            await Task.Yield();
        }
    }
}
