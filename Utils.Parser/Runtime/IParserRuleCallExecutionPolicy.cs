namespace Utils.Parser.Runtime;

/// <summary>
/// Defines explicit opt-in callbacks around parser rule calls.
/// Implementations may observe call-site metadata but do not receive automatic argument evaluation,
/// parameter binding, or parameter seeding.
/// </summary>
public interface IParserRuleCallExecutionPolicy
{
    /// <summary>
    /// Handles a parser rule call immediately before the target rule is parsed.
    /// </summary>
    /// <param name="context">Passive metadata for the current parser rule call.</param>
    void BeforeRuleCall(ParserRuleCallExecutionContext context);

    /// <summary>
    /// Handles a parser rule call after the target rule has succeeded or failed.
    /// </summary>
    /// <param name="context">
    /// Passive metadata for the completed parser rule call, including its success status and call result when available.
    /// </param>
    void AfterRuleCall(ParserRuleCallExecutionContext context);
}
