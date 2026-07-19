using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Thrown when the number of dispatched instructions exceeds the configured instruction budget.
/// </summary>
/// <remarks>
/// This exception is not marked <c>[Serializable]</c> because the <see cref="Budget"/> field
/// has no corresponding serialization constructor or <c>GetObjectData</c> override.
/// </remarks>
public class InstructionBudgetExceededException : Exception
{
    /// <summary>Gets the instruction budget that was exceeded.</summary>
    public long Budget { get; }

    /// <summary>Initializes a new instance of the <see cref="InstructionBudgetExceededException"/> class.</summary>
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
        : base($"Instruction budget of {budget:N0} instructions was exceeded.")
    {
        Budget = budget;
    }
}
