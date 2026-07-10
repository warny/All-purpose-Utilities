using Utils.Parser.Diagnostics.EmbeddedCode;
using Utils.Parser.Source;

namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Describes a raw ANTLR embedded-code source block without assigning execution behavior to it.
/// </summary>
public sealed record EmbeddedCodeSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCodeSource"/> record.
    /// </summary>
    /// <param name="sourceText">Raw embedded-code source text without surrounding ANTLR delimiters.</param>
    /// <param name="kind">Kind of ANTLR construct that owns the source text.</param>
    /// <param name="ruleName">Optional rule name that owns the source text.</param>
    /// <param name="alternativeIndex">Optional zero-based alternative index that owns the source text.</param>
    /// <param name="elementIndex">Optional zero-based element index that owns the source text.</param>
    /// <param name="location">Optional human-readable source location for diagnostics or tooling.</param>
    public EmbeddedCodeSource(
        string sourceText,
        EmbeddedCodeKind kind,
        string? ruleName = null,
        int? alternativeIndex = null,
        int? elementIndex = null,
        SourceCodeLocation? location = null)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ValidateIndex(alternativeIndex, nameof(alternativeIndex));
        ValidateIndex(elementIndex, nameof(elementIndex));

        RawCode = new RawEmbeddedCode(sourceText);
        SourceText = sourceText;
        Kind = kind;
        RuleName = ruleName;
        AlternativeIndex = alternativeIndex;
        ElementIndex = elementIndex;
        Location = location;
    }

    /// <summary>
    /// Gets the raw embedded-code source text without surrounding ANTLR delimiters.
    /// </summary>
    public string SourceText { get; }

    /// <summary>
    /// Gets the typed raw embedded-code source text without surrounding ANTLR delimiters.
    /// </summary>
    public RawEmbeddedCode RawCode { get; }

    /// <summary>
    /// Gets the kind of ANTLR construct that owns the source text.
    /// </summary>
    public EmbeddedCodeKind Kind { get; }

    /// <summary>
    /// Gets the optional rule name that owns the source text.
    /// </summary>
    public string? RuleName { get; }

    /// <summary>
    /// Gets the optional zero-based alternative index that owns the source text.
    /// </summary>
    public int? AlternativeIndex { get; }

    /// <summary>
    /// Gets the optional zero-based element index that owns the source text.
    /// </summary>
    public int? ElementIndex { get; }

    /// <summary>
    /// Gets the optional human-readable source location for diagnostics or tooling.
    /// </summary>
    public SourceCodeLocation? Location { get; }

    /// <summary>
    /// Validates that an optional source index is zero or greater when supplied.
    /// </summary>
    /// <param name="value">Optional index value to validate.</param>
    /// <param name="parameterName">Parameter name used when throwing validation errors.</param>
    private static void ValidateIndex(int? value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Embedded-code indexes must be greater than or equal to zero.");
        }
    }
}
