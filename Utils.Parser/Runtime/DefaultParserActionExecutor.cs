namespace Utils.Parser.Runtime;

/// <summary>
/// Conservative default action executor that does not run embedded action code and returns
/// outcomes with <see cref="ParserActionExecutionStatus.NotExecuted"/> for every embedded action.
/// </summary>
internal sealed class DefaultParserActionExecutor : IParserActionExecutor
{
    /// <summary>
    /// Returns outcomes with <see cref="ParserActionExecutionStatus.NotExecuted"/> for every action.
    /// </summary>
    /// <param name="context">Action context.</param>
    /// <returns>Always an outcome with <see cref="ParserActionExecutionStatus.NotExecuted"/>.</returns>
    public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
        => ParserActionExecutionOutcome.NotExecuted();
}
