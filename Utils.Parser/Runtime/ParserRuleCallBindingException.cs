namespace Utils.Parser.Runtime;

/// <summary>
/// Reports deterministic validation failures from positional literal parser rule-call binding.
/// </summary>
public sealed class ParserRuleCallBindingException : InvalidOperationException
{
    /// <summary>
    /// Initializes a parser rule-call binding exception.
    /// </summary>
    /// <param name="ruleName">Target parser rule name.</param>
    /// <param name="rawArguments">Raw call-site arguments without outer brackets.</param>
    /// <param name="reason">Human-readable validation failure reason.</param>
    /// <param name="argumentIndex">Zero-based offending argument index, when applicable.</param>
    public ParserRuleCallBindingException(
        string ruleName,
        string? rawArguments,
        string reason,
        int? argumentIndex = null)
        : base(CreateMessage(ruleName, rawArguments, reason, argumentIndex))
    {
        RuleName = ruleName;
        RawArguments = rawArguments;
        ArgumentIndex = argumentIndex;
    }

    /// <summary>
    /// Gets the target parser rule name.
    /// </summary>
    public string RuleName { get; }

    /// <summary>
    /// Gets the raw call-site arguments without outer brackets.
    /// </summary>
    public string? RawArguments { get; }

    /// <summary>
    /// Gets the zero-based offending argument index, when applicable.
    /// </summary>
    public int? ArgumentIndex { get; }

    /// <summary>
    /// Creates the exception message from stable call-site metadata.
    /// </summary>
    /// <param name="ruleName">Target parser rule name.</param>
    /// <param name="rawArguments">Raw call-site arguments.</param>
    /// <param name="reason">Validation failure reason.</param>
    /// <param name="argumentIndex">Optional offending argument index.</param>
    /// <returns>The formatted exception message.</returns>
    private static string CreateMessage(string ruleName, string? rawArguments, string reason, int? argumentIndex)
    {
        string indexText = argumentIndex is null ? string.Empty : $" Argument index: {argumentIndex.Value}.";
        return $"Cannot bind positional literal arguments for parser rule '{ruleName}'. {reason}{indexText} Raw arguments: '{rawArguments ?? "<none>"}'.";
    }
}
