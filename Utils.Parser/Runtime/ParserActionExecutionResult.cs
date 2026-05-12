namespace Utils.Parser.Runtime;

/// <summary>
/// Outcome returned by <see cref="IParserActionExecutor"/>.
/// </summary>
public enum ParserActionExecutionResult
{
    /// <summary>Action was executed by the configured runtime policy.</summary>
    Executed,

    /// <summary>Action was intentionally not executed and parsing must continue.</summary>
    NotExecuted
}

