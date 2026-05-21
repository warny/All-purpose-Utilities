namespace Utils.Parser.Runtime;

/// <summary>
/// Defines normalized scheduler observation event kinds.
/// This enum is descriptive-only and does not provide execution control.
/// </summary>
public enum ParserRuntimeObservationKind
{
    /// <summary>
    /// The scheduler started evaluating an alternative.
    /// </summary>
    AlternativeStarted,

    /// <summary>
    /// The scheduler completed evaluation of an alternative.
    /// </summary>
    AlternativeCompleted,

    /// <summary>
    /// The scheduler failed evaluation of an alternative.
    /// </summary>
    AlternativeFailed,

    /// <summary>
    /// The scheduler pruned an alternative for orchestration-only reasons.
    /// </summary>
    AlternativePruned,

    /// <summary>
    /// The scheduler selected a local winner alternative.
    /// </summary>
    AlternativeSelected,

    /// <summary>
    /// The kind could not be inferred from current observation data.
    /// </summary>
    Unknown
}
