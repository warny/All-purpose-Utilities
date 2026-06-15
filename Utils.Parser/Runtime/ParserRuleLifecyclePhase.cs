namespace Utils.Parser.Runtime;

/// <summary>
/// Identifies the phase of a parser rule lifecycle hook execution.
/// </summary>
public enum ParserRuleLifecyclePhase
{
    /// <summary>
    /// The <c>@init</c> phase: executed at rule entry before any alternative is attempted.
    /// </summary>
    Init,

    /// <summary>
    /// The <c>@after</c> phase: executed after successful rule completion, before the result is memoized.
    /// </summary>
    After
}
