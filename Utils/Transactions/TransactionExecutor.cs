using System;
using System.Collections.Generic;

namespace Utils.Transactions;

/// <summary>
/// Executes a sequence of transactional actions.
/// </summary>
/// <remarks>
/// <para>
/// The complete action list is validated and materialized before any action executes, so that
/// null entries and enumeration side-effects cannot interleave with execution (#38).
/// </para>
/// <para>
/// If a commit failure occurs, only the actions that have not yet committed are rolled back.
/// Actions that already committed successfully are not rolled back, because rolling back a
/// committed action is not guaranteed to be valid (#36).
/// </para>
/// <para>
/// When rolling back, every action is attempted even if earlier rollbacks throw. All rollback
/// failures are collected and surfaced as inner exceptions of a <see cref="TransactionException"/>,
/// so the original failure is always distinguishable (#37).
/// </para>
/// </remarks>
public class TransactionExecutor
{
    /// <summary>
    /// Runs <paramref name="actions"/> sequentially, stopping at the first error.
    /// Completed and uncommitted actions are rolled back in reverse order when a failure occurs.
    /// </summary>
    /// <param name="actions">Actions to execute. The sequence is fully materialized before any action runs.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any element in <paramref name="actions"/> is <see langword="null"/>.</exception>
    /// <exception cref="TransactionException">
    /// Thrown when a failure occurs during execution, commit, or rollback. The
    /// <see cref="TransactionException.PrimaryException"/> property holds the original failure;
    /// <see cref="TransactionException.RollbackExceptions"/> holds any rollback failures (#37).
    /// </exception>
    public void Execute(IEnumerable<ITransactionalAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        // Materialize the list before starting any action. This prevents lazy enumeration
        // side-effects from interleaving with execution and validates all entries up front (#38).
        List<ITransactionalAction> actionList = [];
        int index = 0;
        foreach (var action in actions)
        {
            if (action is null)
                throw new ArgumentException($"Action at index {index} is null.", nameof(actions));
            actionList.Add(action);
            index++;
        }

        // Track how many actions have been executed but not yet committed.
        List<ITransactionalAction> executed = new(actionList.Count);
        // Track how many have been committed successfully (#36).
        int committedCount = 0;
        Exception? primaryException = null;

        try
        {
            // Execute phase
            foreach (var action in actionList)
            {
                action.Execute();
                executed.Add(action);
            }

            // Commit phase: track each commit so we know which ones succeeded (#36).
            for (int i = 0; i < executed.Count; i++)
            {
                executed[i].Commit();
                committedCount++;
            }
        }
        catch (Exception ex)
        {
            primaryException = ex;
        }

        if (primaryException != null)
        {
            // Roll back only the actions that were NOT successfully committed (#36).
            var rollbackExceptions = new List<Exception>();
            for (int i = executed.Count - 1; i >= committedCount; i--)
            {
                try
                {
                    executed[i].Rollback();
                }
                catch (Exception rollbackEx)
                {
                    // Collect every rollback failure rather than stopping at the first (#37).
                    rollbackExceptions.Add(rollbackEx);
                }
            }

            // Always throw a TransactionException so the original failure is preserved
            // and rollback failures are distinguishable (#37).
            throw new TransactionException(primaryException, rollbackExceptions);
        }
    }
}

/// <summary>
/// Represents a failure during transaction execution.
/// </summary>
public sealed class TransactionException : Exception
{
    /// <summary>
    /// Gets the original exception that triggered the failure.
    /// </summary>
    public Exception PrimaryException { get; }

    /// <summary>
    /// Gets the exceptions thrown by rollback operations, if any.
    /// </summary>
    public IReadOnlyList<Exception> RollbackExceptions { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TransactionException"/>.
    /// </summary>
    /// <param name="primaryException">The original failure.</param>
    /// <param name="rollbackExceptions">Exceptions thrown during rollback (may be empty).</param>
    public TransactionException(Exception primaryException, IReadOnlyList<Exception> rollbackExceptions)
        : base(BuildMessage(primaryException, rollbackExceptions), primaryException)
    {
        PrimaryException = primaryException;
        RollbackExceptions = rollbackExceptions;
    }

    private static string BuildMessage(Exception primary, IReadOnlyList<Exception> rollbacks)
    {
        if (rollbacks.Count == 0)
            return $"Transaction failed: {primary.Message}";

        return $"Transaction failed: {primary.Message} Additionally, {rollbacks.Count} rollback(s) also failed.";
    }
}
