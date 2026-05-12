using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Defines policy-driven embedded action handling for <see cref="ParserEngine"/>.
/// Implementations may execute side effects and can therefore influence observable runtime behavior.
/// </summary>
public interface IParserActionExecutor
{
    /// <summary>
    /// Executes or ignores a parser action according to the runtime policy.
    /// </summary>
    /// <param name="context">Immutable action execution context.</param>
    /// <returns>Execution decision for the action.
    /// For memoization safety, implementations should avoid invocation-count-dependent mutable external state.</returns>
    ParserActionExecutionResult Execute(ParserActionExecutionContext context);
}

