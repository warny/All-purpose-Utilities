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
}
