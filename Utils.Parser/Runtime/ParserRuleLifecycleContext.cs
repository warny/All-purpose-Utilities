namespace Utils.Parser.Runtime;

/// <summary>
/// Immutable context passed to parser rule lifecycle hook executors.
/// Provides positional metadata for rule entry and exit without granting parse-control authority.
/// </summary>
public sealed class ParserRuleLifecycleContext
{
    /// <summary>
    /// Initializes a parser rule lifecycle context.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule being entered or exited.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    public ParserRuleLifecycleContext(string ruleName, int inputPosition)
    {
        RuleName = ruleName ?? throw new ArgumentNullException(nameof(ruleName));
        InputPosition = inputPosition;
    }

    /// <summary>
    /// Gets the name of the parser rule associated with this lifecycle event.
    /// </summary>
    public string RuleName { get; }

    /// <summary>
    /// Gets the token-stream position at the time of rule entry.
    /// </summary>
    public int InputPosition { get; }
}
