namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Describes the runtime dispatch identity shared by parser embedded-code execution paths.
/// </summary>
public sealed record EmbeddedCodeRuntimeKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCodeRuntimeKey"/> record.
    /// </summary>
    /// <param name="kind">Embedded-code kind used by runtime dispatch.</param>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="sourceText">Raw embedded-code source text.</param>
    /// <param name="alternativeIndex">Optional runtime alternative index.</param>
    /// <param name="elementIndex">Optional runtime element index.</param>
    public EmbeddedCodeRuntimeKey(
        EmbeddedCodeKind kind,
        string ruleName,
        string sourceText,
        int? alternativeIndex,
        int? elementIndex)
    {
        ArgumentNullException.ThrowIfNull(ruleName);
        ArgumentNullException.ThrowIfNull(sourceText);

        Kind = kind;
        RuleName = ruleName;
        SourceText = sourceText;
        AlternativeIndex = alternativeIndex;
        ElementIndex = elementIndex;
    }

    /// <summary>Gets the embedded-code kind used by runtime dispatch.</summary>
    public EmbeddedCodeKind Kind { get; }

    /// <summary>Gets the owning parser rule name.</summary>
    public string RuleName { get; }

    /// <summary>Gets the raw embedded-code source text.</summary>
    public string SourceText { get; }

    /// <summary>Gets the optional runtime alternative index.</summary>
    public int? AlternativeIndex { get; }

    /// <summary>Gets the optional runtime element index.</summary>
    public int? ElementIndex { get; }

    /// <summary>
    /// Creates a runtime key from source metadata when it has an owning parser rule.
    /// </summary>
    /// <param name="source">Embedded-code source metadata.</param>
    /// <returns>A runtime key for executable parser embedded code.</returns>
    public static EmbeddedCodeRuntimeKey FromSource(EmbeddedCodeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.RuleName is null)
        {
            throw new ArgumentException("Runtime embedded-code keys require an owning parser rule.", nameof(source));
        }

        return new EmbeddedCodeRuntimeKey(source.Kind, source.RuleName, source.SourceText, source.AlternativeIndex, source.ElementIndex);
    }
}
