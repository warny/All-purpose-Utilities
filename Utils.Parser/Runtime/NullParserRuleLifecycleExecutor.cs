namespace Utils.Parser.Runtime;

/// <summary>
/// No-op parser rule lifecycle executor used by conservative runtime policies.
/// Neither <c>@init</c> nor <c>@after</c> hooks are executed.
/// </summary>
public sealed class NullParserRuleLifecycleExecutor : IParserRuleLifecycleExecutor
{
    /// <summary>
    /// Gets the singleton no-op lifecycle executor instance.
    /// </summary>
    public static NullParserRuleLifecycleExecutor Instance { get; } = new();

    /// <summary>
    /// Initializes the singleton no-op lifecycle executor.
    /// </summary>
    private NullParserRuleLifecycleExecutor()
    {
    }

    /// <summary>
    /// Performs no action. Rule lifecycle hooks are not executed in the conservative policy.
    /// </summary>
    /// <param name="phase">Lifecycle phase (ignored).</param>
    /// <param name="ruleName">Rule name (ignored).</param>
    /// <param name="context">Lifecycle context (validated non-null).</param>
    public void Execute(ParserRuleLifecyclePhase phase, string ruleName, ParserRuleLifecycleContext context)
    {
        ArgumentNullException.ThrowIfNull(ruleName);
        ArgumentNullException.ThrowIfNull(context);
    }
}
