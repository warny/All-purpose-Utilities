namespace Utils.Parser.Runtime;

/// <summary>
/// Describes the deterministic result of converting one parsed simple literal to a supported declared type.
/// </summary>
public readonly record struct ParserLiteralConversionResult
{
    /// <summary>
    /// Gets whether conversion succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the converted scalar value, including <c>null</c> for accepted null literals.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Gets the deterministic conversion failure reason, or <c>null</c> after success.
    /// </summary>
    public string? Error { get; init; }
}
