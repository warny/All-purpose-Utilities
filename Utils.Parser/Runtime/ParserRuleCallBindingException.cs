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
    /// <param name="parameterName">Declared target parameter name, when applicable.</param>
    /// <param name="declaredType">Raw declared target type, when applicable.</param>
    public ParserRuleCallBindingException(
        string ruleName,
        string? rawArguments,
        string reason,
        int? argumentIndex = null,
        string? parameterName = null,
        string? declaredType = null)
        : base(CreateMessage(ruleName, rawArguments, reason, argumentIndex, parameterName, declaredType))
    {
        RuleName = ruleName;
        RawArguments = rawArguments;
        ArgumentIndex = argumentIndex;
        ParameterName = parameterName;
        DeclaredType = declaredType;
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
    /// Gets the declared target parameter name, when applicable.
    /// </summary>
    public string? ParameterName { get; }

    /// <summary>
    /// Gets the raw declared target type, when applicable.
    /// </summary>
    public string? DeclaredType { get; }

    /// <summary>
    /// Creates the exception message from stable call-site metadata.
    /// </summary>
    /// <param name="ruleName">Target parser rule name.</param>
    /// <param name="rawArguments">Raw call-site arguments.</param>
    /// <param name="reason">Validation failure reason.</param>
    /// <param name="argumentIndex">Optional offending argument index.</param>
    /// <param name="parameterName">Optional target parameter name.</param>
    /// <param name="declaredType">Optional raw declared target type.</param>
    /// <returns>The formatted exception message.</returns>
    private static string CreateMessage(
        string ruleName,
        string? rawArguments,
        string reason,
        int? argumentIndex,
        string? parameterName,
        string? declaredType)
    {
        string indexText = argumentIndex is null ? string.Empty : $" Argument index: {argumentIndex.Value}.";
        if (parameterName is null && declaredType is null)
        {
            return $"Cannot bind positional literal arguments for parser rule '{ruleName}'. {reason}{indexText} Raw arguments: '{rawArguments ?? "<none>"}'.";
        }

        string parameterText = parameterName is null ? string.Empty : $" Parameter: '{parameterName}'.";
        string typeText = declaredType is null ? string.Empty : $" Declared type: '{declaredType}'.";
        return $"Cannot bind literal arguments for parser rule '{ruleName}'. {reason}{indexText}{parameterText}{typeText} Raw arguments: '{rawArguments ?? "<none>"}'.";
    }
}
