using System;

namespace Utils.Transactions;

/// <summary>
/// Represents an action that can be committed or rolled back.
/// </summary>
/// <remarks>
/// The action is executed once. If all actions in a batch succeed, <see cref="Commit"/> is called.
/// If any action fails, <see cref="Rollback"/> is invoked for the actions that completed before the failure.
/// </remarks>
public interface ITransactionalAction
{
    /// <summary>
    /// Performs the action.
    /// </summary>
    void Execute();

    /// <summary>
    /// Finalizes the action after all actions in the batch succeed.
    /// </summary>
    void Commit();

    /// <summary>
    /// Reverts the effects of the action because another action failed.
    /// </summary>
    void Rollback();
}

