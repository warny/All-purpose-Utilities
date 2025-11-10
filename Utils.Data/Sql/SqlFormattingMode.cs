namespace Utils.Data.Sql;

/// <summary>
/// Determines how SQL text is formatted when rebuilt from the analyser output.
/// </summary>
public enum SqlFormattingMode
{
    /// <summary>
    /// The SQL text is emitted on a single line.
    /// </summary>
    Inline,

    /// <summary>
    /// Commas are placed at the beginning of lines for list-oriented clauses.
    /// </summary>
    Prefixed,

    /// <summary>
    /// Commas terminate lines for list-oriented clauses.
    /// </summary>
    Suffixed,
}
