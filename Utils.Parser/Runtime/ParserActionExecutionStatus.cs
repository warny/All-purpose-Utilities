namespace Utils.Parser.Runtime;

/// <summary>
/// Represents the parser action execution status returned by runtime policy executors.
/// </summary>
public enum ParserActionExecutionStatus
{
    /// <summary>
    /// Action was executed by the configured runtime policy.
    /// </summary>
    Executed,

    /// <summary>
    /// Action was intentionally not executed and parsing must continue.
    /// </summary>
    NotExecuted
}
