namespace Utils.Parser.Runtime;

/// <summary>
/// Provides the conservative no-op parser rule-call execution policy.
/// </summary>
public sealed class NullParserRuleCallExecutionPolicy : IParserRuleCallExecutionPolicy
{
    /// <summary>
    /// Gets the singleton no-op parser rule-call execution policy.
    /// </summary>
    public static NullParserRuleCallExecutionPolicy Instance { get; } = new();

    /// <summary>
    /// Initializes the singleton no-op policy.
    /// </summary>
    private NullParserRuleCallExecutionPolicy()
    {
    }

    /// <summary>
    /// Performs no work before a parser rule call.
    /// </summary>
    /// <param name="context">Passive metadata for the current parser rule call.</param>
    public void BeforeRuleCall(ParserRuleCallExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// Performs no work after a parser rule call.
    /// </summary>
    /// <param name="context">Passive metadata for the completed parser rule call.</param>
    public void AfterRuleCall(ParserRuleCallExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }
}
