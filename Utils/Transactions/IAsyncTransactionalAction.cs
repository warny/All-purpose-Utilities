using System.Threading;
using System.Threading.Tasks;

namespace Utils.Transactions;

/// <summary>
/// Represents an asynchronous action that can be committed or rolled back.
/// </summary>
/// <remarks>
/// Provides the same execute/commit/rollback lifecycle as <see cref="ITransactionalAction"/>
/// but with cancellation-aware asynchronous operations, suitable for I/O-bound work (#39).
/// </remarks>
public interface IAsyncTransactionalAction
{
    /// <summary>
    /// Performs the asynchronous action.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token that signals cancellation. Cancellation during <see cref="ExecuteAsync"/> triggers
    /// rollback of all previously executed actions in the batch.
    /// </param>
    ValueTask ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalizes the action after all actions in the batch succeed.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token that signals cancellation. Note: once the commit phase begins, individual commit
    /// failures follow the same semantics as in <see cref="TransactionExecutor"/>; a committed
    /// action is never rolled back again.
    /// </param>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverts the effects of the action because another action failed or was cancelled.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token for the rollback operation itself. Implementations should treat
    /// this token as a best-effort hint; rollbacks should complete even if cancelled, to
    /// avoid leaving resources in an inconsistent state.
    /// </param>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
