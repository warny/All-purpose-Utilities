namespace Utils.Parser.Diagnostics;

/// <summary>
/// Severity level associated with a parser diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Blocking error that prevents reliable continuation.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Non-blocking warning indicating unsupported or suspicious behavior.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Informational diagnostic describing default behavior.
    /// </summary>
    Info = 2,

    /// <summary>
    /// Debug trace diagnostic.
    /// </summary>
    Debug = 3
}
