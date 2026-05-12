namespace Utils.Parser.Runtime;

/// <summary>
/// Conservative default action executor that does not run embedded action code.
/// </summary>
internal sealed class DefaultParserActionExecutor : IParserActionExecutor
{
    /// <summary>
    /// Returns <see cref="ParserActionExecutionResult.NotExecuted"/> for every action.
    /// </summary>
    /// <param name="context">Action context.</param>
    /// <returns>Always <see cref="ParserActionExecutionResult.NotExecuted"/>.</returns>
    public ParserActionExecutionResult Execute(ParserActionExecutionContext context)
        => ParserActionExecutionResult.NotExecuted;
}

