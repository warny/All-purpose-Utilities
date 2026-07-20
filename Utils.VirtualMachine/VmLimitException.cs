using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Abstract base for all exceptions thrown when a caller-configured VM resource limit is exhausted.
/// </summary>
/// <remarks>
/// Derives from <see cref="VmInvalidOperationException"/> so that limits previously reported as
/// <see cref="System.InvalidOperationException"/> (e.g. operand-stack overflow) remain catchable
/// by the same handlers after the migration to the structured limit hierarchy.
/// <para>
/// Carries the three diagnostic fields common to every limit violation:
/// <see cref="LimitKind"/>, <see cref="Limit"/>, and <see cref="AttemptedValue"/>.
/// Catch handlers may target this type to intercept any limit violation regardless of
/// which specific resource was exhausted.
/// </para>
/// </remarks>
public abstract class VmLimitException : VmInvalidOperationException
{
    /// <summary>Gets the kind of limit that was exhausted.</summary>
    public VmLimitKind LimitKind { get; }

    /// <summary>Gets the configured maximum value that was exceeded.</summary>
    public long Limit { get; }

    /// <summary>Gets the value that the operation attempted to reach, which exceeded <see cref="Limit"/>.</summary>
    public long AttemptedValue { get; }

    /// <summary>
    /// Initializes a new instance with full limit metadata and a message.
    /// </summary>
    /// <param name="limitKind">The kind of limit that was exhausted.</param>
    /// <param name="limit">The configured maximum.</param>
    /// <param name="attemptedValue">The value the operation attempted to reach.</param>
    /// <param name="message">The exception message.</param>
    protected VmLimitException(VmLimitKind limitKind, long limit, long attemptedValue, string message)
        : base(message)
    {
        LimitKind = limitKind;
        Limit = limit;
        AttemptedValue = attemptedValue;
    }

    /// <summary>
    /// Initializes a new instance with full limit metadata, a message, and an inner exception.
    /// </summary>
    /// <param name="limitKind">The kind of limit that was exhausted.</param>
    /// <param name="limit">The configured maximum.</param>
    /// <param name="attemptedValue">The value the operation attempted to reach.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    protected VmLimitException(VmLimitKind limitKind, long limit, long attemptedValue, string message, Exception innerException)
        : base(message, innerException)
    {
        LimitKind = limitKind;
        Limit = limit;
        AttemptedValue = attemptedValue;
    }
}
