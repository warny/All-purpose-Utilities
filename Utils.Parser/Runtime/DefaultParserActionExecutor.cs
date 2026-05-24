namespace Utils.Parser.Runtime;

/// <summary>
/// Conservative default action executor that does not run embedded action code and returns
/// <see cref="ParserActionExecutionOutcome.NotExecuted"/> for every embedded action.
/// </summary>
internal sealed class DefaultParserActionExecutor : IParserActionExecutor
{
    /// <summary>
    /// Returns <see cref="ParserActionExecutionOutcome.NotExecuted"/> for every action.
    /// </summary>
    /// <param name="context">Action context.</param>
    /// <returns>Always <see cref="ParserActionExecutionOutcome.NotExecuted"/>.</returns>
    public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
        => ParserActionExecutionOutcome.NotExecuted();
}

