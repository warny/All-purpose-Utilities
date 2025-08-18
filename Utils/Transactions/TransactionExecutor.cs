using System;
using System.Collections.Generic;

namespace Utils.Transactions;

/// <summary>
/// Executes a sequence of transactional actions.
/// </summary>
public class TransactionExecutor
{
        /// <summary>
        /// Runs <paramref name="actions"/> sequentially, stopping at the first error.
        /// Completed actions are committed when all succeed or rolled back in reverse order if an error occurs.
        /// </summary>
        /// <param name="actions">Actions to execute.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actions"/> is <see langword="null"/>.</exception>
        public void Execute(IEnumerable<ITransactionalAction> actions)
        {
                ArgumentNullException.ThrowIfNull(actions);

                List<ITransactionalAction> executed = [];
                try
                {
                        foreach (var action in actions)
                        {
                                ArgumentNullException.ThrowIfNull(action);
                                action.Execute();
                                executed.Add(action);
                        }

                        foreach (var action in executed)
                        {
                                action.Commit();
                        }
                }
                catch
                {
                        for (int i = executed.Count - 1; i >= 0; i--)
                        {
                                executed[i].Rollback();
                        }

                        throw;
                }
        }
}

