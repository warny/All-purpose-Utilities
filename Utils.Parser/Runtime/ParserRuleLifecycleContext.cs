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
        : this(ruleName, inputPosition, null)
    {
    }

    /// <summary>
    /// Initializes a parser rule lifecycle context with an optional invocation frame.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule being entered or exited.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    /// <param name="invocationFrame">Passive parser rule invocation frame, or <c>null</c> when no frame is available.</param>
    public ParserRuleLifecycleContext(string ruleName, int inputPosition, ParserRuleInvocationFrame? invocationFrame)
    {
        RuleName = ruleName ?? throw new ArgumentNullException(nameof(ruleName));
        InputPosition = inputPosition;
        InvocationFrame = invocationFrame;
    }

    /// <summary>
    /// Gets the name of the parser rule associated with this lifecycle event.
    /// </summary>
    public string RuleName { get; }

    /// <summary>
    /// Gets the token-stream position at the time of rule entry.
    /// </summary>
    public int InputPosition { get; }

    /// <summary>
    /// Gets the passive invocation frame associated with this lifecycle event when available.
    /// </summary>
    public ParserRuleInvocationFrame? InvocationFrame { get; }
}
