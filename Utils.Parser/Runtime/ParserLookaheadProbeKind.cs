namespace Utils.Parser.Runtime;

/// <summary>
/// Describes the outcome of a lightweight look-ahead probe for a scheduled alternative.
/// </summary>
internal enum ParserLookaheadProbeKind
{
    /// <summary>
    /// Probe outcome is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The alternative is known to be impossible to start without consuming input.
    /// </summary>
    ImmediateReject,

    /// <summary>
    /// The alternative may start and requires an actual parse attempt.
    /// </summary>
    RequiresParse,

    /// <summary>
    /// The alternative may succeed without consuming input.
    /// Parsing must still execute normally.
    /// </summary>
    EpsilonPossible
}
