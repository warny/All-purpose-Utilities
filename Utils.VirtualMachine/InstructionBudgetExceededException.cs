using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Thrown when the number of dispatched instructions exceeds the configured instruction budget.
/// </summary>
/// <remarks>
/// Derives from <see cref="VmLimitExceededException"/> with
/// <see cref="VmLimitExceededException.LimitKind"/> always equal to
/// <see cref="VmLimitKind.InstructionCount"/>. The <see cref="Budget"/> property is a convenience
/// alias for <see cref="VmLimitExceededException.Limit"/>.
/// <para>
/// When a budget is unknown (legacy message-only constructors), <see cref="Budget"/> and
/// <see cref="VmLimitExceededException.AttemptedValue"/> are both zero; callers should check
/// the message text instead of relying on these fields.
/// </para>
/// Existing callers catching <see cref="InstructionBudgetExceededException"/> specifically
/// continue to work regardless of which constructor was used.
/// </remarks>
public class InstructionBudgetExceededException : VmLimitExceededException
{
    /// <summary>
    /// Gets the instruction budget that was exceeded, or zero when the budget is not known
    /// (message-only constructors). Alias for <see cref="VmLimitExceededException.Limit"/>.
    /// </summary>
    public long Budget => Limit;

    /// <summary>
    /// Initializes a new instance with no budget information.
    /// <see cref="Budget"/> is zero; <see cref="VmLimitExceededException.LimitKind"/> is
    /// <see cref="VmLimitKind.InstructionCount"/>.
    /// </summary>
    public InstructionBudgetExceededException()
        : base(VmLimitKind.InstructionCount, limit: 0, attemptedValue: 0,
               "Instruction budget exceeded.")
    {
    }

    /// <summary>
    /// Initializes a new instance with a specified error message and no budget information.
    /// <see cref="Budget"/> is zero; <see cref="VmLimitExceededException.LimitKind"/> is
    /// <see cref="VmLimitKind.InstructionCount"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InstructionBudgetExceededException(string message)
        : base(VmLimitKind.InstructionCount, limit: 0, attemptedValue: 0, message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a specified error message and inner exception.
    /// <see cref="Budget"/> is zero; <see cref="VmLimitExceededException.LimitKind"/> is
    /// <see cref="VmLimitKind.InstructionCount"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public InstructionBudgetExceededException(string message, Exception innerException)
        : base(VmLimitKind.InstructionCount, limit: 0, attemptedValue: 0, message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance reporting the budget limit that was exceeded.
    /// </summary>
    /// <param name="budget">The instruction budget that was exceeded.</param>
    public InstructionBudgetExceededException(long budget)
        : base(VmLimitKind.InstructionCount, budget,
               budget == long.MaxValue ? long.MaxValue : budget + 1,
               $"Instruction budget of {budget:N0} instructions was exceeded.")
    {
    }
}
