namespace Utils.Parser.Runtime;

/// <summary>
/// Executes parser rule lifecycle hooks (<c>@init</c> and <c>@after</c>) under a runtime feature policy.
/// Implementations may execute side effects that change observable parser execution-context state.
/// </summary>
public interface IParserRuleLifecycleExecutor
{
    /// <summary>
    /// Executes a rule lifecycle hook for the specified phase and rule name.
    /// Implementations should be no-ops when the phase and rule combination is unhandled.
    /// </summary>
    /// <param name="phase">Lifecycle phase being executed (<c>Init</c> or <c>After</c>).</param>
    /// <param name="ruleName">Name of the parser rule triggering the lifecycle event.</param>
    /// <param name="context">Immutable lifecycle context providing positional metadata.</param>
    void Execute(ParserRuleLifecyclePhase phase, string ruleName, ParserRuleLifecycleContext context);
}
