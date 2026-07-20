using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Thrown when the number of dispatched instructions exceeds the configured instruction budget.
/// </summary>
/// <remarks>
/// Derives from <see cref="VmLimitExceededException"/> with
/// <see cref="VmLimitExceededException.LimitKind"/> equal to <see cref="VmLimitKind.InstructionCount"/>.
/// The <see cref="Budget"/> property is a convenience alias for <see cref="VmLimitExceededException.Limit"/>.
/// Existing callers catching <see cref="InstructionBudgetExceededException"/> specifically continue to work.
/// </remarks>
public class InstructionBudgetExceededException : VmLimitExceededException
{
    /// <summary>Gets the instruction budget that was exceeded. Alias for <see cref="VmLimitExceededException.Limit"/>.</summary>
    public long Budget => Limit;

    /// <summary>Initializes a new instance with no diagnostic data.</summary>
    public InstructionBudgetExceededException() { }

    /// <summary>Initializes a new instance with a specified error message.</summary>
    /// <param name="message">The message that describes the error.</param>
    public InstructionBudgetExceededException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a specified error message and inner exception.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public InstructionBudgetExceededException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance reporting the budget limit that was exceeded.
    /// </summary>
    /// <param name="budget">The instruction budget that was exceeded.</param>
    public InstructionBudgetExceededException(long budget)
        : base(VmLimitKind.InstructionCount, budget, budget + 1,
               $"Instruction budget of {budget:N0} instructions was exceeded.")
    {
    }
}
