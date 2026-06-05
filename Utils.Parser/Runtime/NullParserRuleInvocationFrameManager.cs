namespace Utils.Parser.Runtime;

/// <summary>
/// Default passive parser rule invocation-frame manager.
/// It creates inert frames for lifecycle context propagation and does not retain current-frame state.
/// </summary>
public sealed class NullParserRuleInvocationFrameManager : IParserRuleInvocationFrameManager
{
    /// <summary>
    /// Gets the singleton no-op invocation-frame manager instance.
    /// </summary>
    public static NullParserRuleInvocationFrameManager Instance { get; } = new();

    /// <summary>
    /// Initializes the singleton no-op invocation-frame manager.
    /// </summary>
    private NullParserRuleInvocationFrameManager()
    {
    }

    /// <summary>
    /// Gets no current frame because this implementation intentionally does not retain per-parse state.
    /// </summary>
    public ParserRuleInvocationFrame? Current => null;

    /// <summary>
    /// Creates an inert parser rule invocation frame without enabling metadata execution.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule being entered.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    /// <returns>An empty passive invocation frame.</returns>
    public ParserRuleInvocationFrame Enter(string ruleName, int inputPosition)
    {
        return new ParserRuleInvocationFrame(ruleName, inputPosition);
    }

    /// <summary>
    /// Performs no action when leaving a parser rule invocation.
    /// </summary>
    /// <param name="frame">Invocation frame returned by <see cref="Enter"/>.</param>
    /// <param name="succeeded">Whether the parser rule produced a parse node before leaving (ignored).</param>
    public void Exit(ParserRuleInvocationFrame frame, bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(frame);
    }
}
