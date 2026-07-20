using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Thrown when a caller-configured VM resource limit is exhausted.
/// </summary>
/// <remarks>
/// This exception covers structural capacity limits (stack depth, page count, process count)
/// and per-execution instruction budgets. Hard identifier-space exhaustion
/// (<see cref="int.MaxValue"/> process/page IDs) is reported separately as
/// <see cref="InvalidOperationException"/>.
/// <para>
/// Every public instance carries valid <see cref="LimitKind"/>, <see cref="Limit"/>, and
/// <see cref="AttemptedValue"/> metadata. Constructors that cannot supply real metadata are
/// restricted to subclasses via <c>protected</c> access so that catch handlers can always rely
/// on these properties to identify and route limit violations.
/// </para>
/// </remarks>
public class VmLimitExceededException : Exception
{
    /// <summary>Gets the kind of limit that was exhausted.</summary>
    public VmLimitKind LimitKind { get; }

    /// <summary>Gets the configured maximum value that was exceeded.</summary>
    public long Limit { get; }

    /// <summary>Gets the value that the operation attempted to reach, which exceeded <see cref="Limit"/>.</summary>
    public long AttemptedValue { get; }

    /// <summary>
    /// Initializes a new instance with full limit metadata.
    /// </summary>
    /// <param name="limitKind">The kind of limit that was exhausted.</param>
    /// <param name="limit">The configured maximum.</param>
    /// <param name="attemptedValue">The value the operation attempted to reach.</param>
    public VmLimitExceededException(VmLimitKind limitKind, long limit, long attemptedValue)
        : base(BuildMessage(limitKind, limit, attemptedValue))
    {
        LimitKind = limitKind;
        Limit = limit;
        AttemptedValue = attemptedValue;
    }

    /// <summary>
    /// Initializes a new instance with full limit metadata and a custom message.
    /// For use by subclasses that need to supply a specialized message without auto-generation.
    /// </summary>
    /// <param name="limitKind">The kind of limit that was exhausted.</param>
    /// <param name="limit">The configured maximum. Zero when the budget is unknown (legacy constructors).</param>
    /// <param name="attemptedValue">The value the operation attempted to reach. Zero when unknown.</param>
    /// <param name="message">Custom exception message.</param>
    protected VmLimitExceededException(VmLimitKind limitKind, long limit, long attemptedValue, string message)
        : base(message)
    {
        LimitKind = limitKind;
        Limit = limit;
        AttemptedValue = attemptedValue;
    }

    /// <summary>
    /// Initializes a new instance with full limit metadata, a custom message, and an inner exception.
    /// For use by subclasses that wrap another exception.
    /// </summary>
    /// <param name="limitKind">The kind of limit that was exhausted.</param>
    /// <param name="limit">The configured maximum. Zero when the budget is unknown (legacy constructors).</param>
    /// <param name="attemptedValue">The value the operation attempted to reach. Zero when unknown.</param>
    /// <param name="message">Custom exception message.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    protected VmLimitExceededException(VmLimitKind limitKind, long limit, long attemptedValue, string message, Exception innerException)
        : base(message, innerException)
    {
        LimitKind = limitKind;
        Limit = limit;
        AttemptedValue = attemptedValue;
    }

    private static string BuildMessage(VmLimitKind kind, long limit, long attempted)
        => $"VM limit exceeded ({kind}): configured limit is {limit:N0}, attempted value is {attempted:N0}.";
}
