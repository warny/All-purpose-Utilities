using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Transactions;

/// <summary>
/// Executes a sequence of asynchronous transactional actions with cancellation support.
/// </summary>
/// <remarks>
/// <para>
/// The complete action list is validated and materialized before any action executes, so
/// that null entries and enumeration side-effects cannot interleave with execution (#38, #39).
/// </para>
/// <para>
/// Cancellation during the execute phase triggers rollback of all previously executed actions.
/// Once the commit phase begins, individual commit failures follow the same semantics as
/// <see cref="TransactionExecutor"/>: committed actions are never rolled back (#36, #39).
/// </para>
/// <para>
/// When rolling back, every applicable action is attempted even if earlier rollbacks throw.
/// All rollback failures are collected and surfaced in a <see cref="TransactionException"/>,
/// so the original failure is always distinguishable (#37, #39).
/// </para>
/// </remarks>
public class AsyncTransactionExecutor
{
    /// <summary>
    /// Executes <paramref name="actions"/> sequentially, stopping at the first error or cancellation.
    /// Executed-but-not-committed actions are rolled back in reverse order when a failure occurs.
    /// </summary>
    /// <param name="actions">
    /// Actions to execute. The sequence is fully materialized before any action runs.
    /// </param>
    /// <param name="cancellationToken">
    /// Token that signals cancellation. Cancellation during the execute phase triggers rollback.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any element in <paramref name="actions"/> is <see langword="null"/>.</exception>
    /// <exception cref="TransactionException">
    /// Thrown when a failure or cancellation occurs during execution, commit, or rollback.
    /// The <see cref="TransactionException.PrimaryException"/> property holds the original failure;
    /// <see cref="TransactionException.RollbackExceptions"/> holds any rollback failures.
    /// </exception>
    public async Task ExecuteAsync(IEnumerable<IAsyncTransactionalAction> actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actions);

        // Materialize the list before starting any action. This prevents lazy enumeration
        // side-effects from interleaving with execution and validates all entries up front.
        List<IAsyncTransactionalAction> actionList = [];
        int index = 0;
        foreach (var action in actions)
        {
            if (action is null)
                throw new ArgumentException($"Action at index {index} is null.", nameof(actions));
            actionList.Add(action);
            index++;
        }

        // Track how many actions have been executed but not yet committed.
        var executed = new List<IAsyncTransactionalAction>(actionList.Count);
        // Track how many have been committed successfully.
        int committedCount = 0;
        Exception? primaryException = null;

        try
        {
            // Execute phase: stop on first failure or cancellation.
            foreach (var action in actionList)
            {
                // Re-check cancellation between actions (cheap, no token.ThrowIfCancellationRequested
                // inside third-party code we don't control).
                cancellationToken.ThrowIfCancellationRequested();
                await action.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                executed.Add(action);
            }

            // Commit phase: track each commit so we know which ones succeeded.
            for (int i = 0; i < executed.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await executed[i].CommitAsync(cancellationToken).ConfigureAwait(false);
                committedCount++;
            }
        }
        catch (Exception ex)
        {
            primaryException = ex;
        }

        if (primaryException != null)
        {
            // Roll back only the actions that were NOT successfully committed.
            // Use CancellationToken.None for rollbacks: we must attempt cleanup regardless
            // of whether the original operation was cancelled.
            var rollbackExceptions = new List<Exception>();
            for (int i = executed.Count - 1; i >= committedCount; i--)
            {
                try
                {
                    await executed[i].RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception rollbackEx)
                {
                    // Collect every rollback failure rather than stopping at the first.
                    rollbackExceptions.Add(rollbackEx);
                }
            }

            throw new TransactionException(primaryException, rollbackExceptions);
        }
    }
}
