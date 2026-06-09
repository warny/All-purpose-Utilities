namespace Utils.Parser.Runtime;

/// <summary>
/// Manages passive parser rule invocation frames for optional runtime policies.
/// Implementations may observe rule entry and exit, but they must not own parser control flow.
/// </summary>
public interface IParserRuleInvocationFrameManager
{
    /// <summary>
    /// Enters a parser rule invocation and creates the passive invocation frame for that attempt.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule being entered.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    /// <param name="descriptor">Passive rule metadata descriptor for the invocation, when available.</param>
    /// <returns>The invocation frame associated with this parser rule attempt.</returns>
    ParserRuleInvocationFrame Enter(string ruleName, int inputPosition, ParserRuleInvocationDescriptor? descriptor = null);

    /// <summary>
    /// Leaves a parser rule invocation that was previously entered.
    /// </summary>
    /// <param name="frame">Invocation frame returned by <see cref="Enter"/>.</param>
    /// <param name="succeeded">Whether the parser rule produced a parse node before leaving.</param>
    void Exit(ParserRuleInvocationFrame frame, bool succeeded);

    /// <summary>
    /// Gets the current invocation frame when the implementation tracks one.
    /// </summary>
    ParserRuleInvocationFrame? Current { get; }

    /// <summary>
    /// Called by <c>ParserEngine</c> just before the post-rule execution-state snapshot is captured,
    /// while the rule's invocation frame is still current.
    /// Implementations that support call results must finalize them here so that the snapshot
    /// reflects the post-rule call-result state and memoization hits can restore it correctly.
    /// The default implementation performs no action.
    /// </summary>
    /// <param name="frame">The current invocation frame about to be snapshotted.</param>
    /// <param name="succeeded">Whether the parser rule produced a parse node.</param>
    void PrepareCallResultForSnapshot(ParserRuleInvocationFrame frame, bool succeeded)
    {
    }

    /// <summary>
    /// Called by <c>ParserEngine</c> after a direct child parser-rule call completes successfully
    /// (whether from a fresh parse or a memoized hit) to annotate the current frame's last completed
    /// child call result with the raw argument text from the call site.
    /// This ensures that the parent-visible call result carries the current call site's
    /// <c>callee[...]</c> metadata rather than stale metadata from a memoized execution-state snapshot.
    /// The default implementation performs no action.
    /// </summary>
    /// <param name="rawArguments">
    /// Raw call-site argument text from the current <c>RuleRef.RawArguments</c>, or <c>null</c> when
    /// the call site carries no argument clause. Not evaluated, not bound to child parameters.
    /// </param>
    void AnnotateLastChildCallRawArguments(string? rawArguments)
    {
    }
}
