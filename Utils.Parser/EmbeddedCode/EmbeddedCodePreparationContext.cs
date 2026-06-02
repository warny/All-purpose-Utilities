using System.Collections.Immutable;

namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Describes the explicit preparation environment for ANTLR embedded-code source.
/// </summary>
public sealed record EmbeddedCodePreparationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCodePreparationContext"/> record.
    /// </summary>
    /// <param name="grammarName">Name of the grammar that owns the embedded source.</param>
    /// <param name="target">Preparation path that will consume the embedded source.</param>
    /// <param name="ruleName">Optional rule name associated with the embedded source.</param>
    /// <param name="languageOrCompilerIdentity">Optional explicit language or compiler identity configured by the caller.</param>
    /// <param name="symbolModelVersion">Version of the contextual symbol model understood by the preparer.</param>
    /// <param name="supportedSymbols">Contextual symbols that are available to the preparer.</param>
    public EmbeddedCodePreparationContext(
        string grammarName,
        EmbeddedCodeTarget target,
        string? ruleName = null,
        string? languageOrCompilerIdentity = null,
        int symbolModelVersion = 1,
        IReadOnlySet<EmbeddedCodeContextSymbol>? supportedSymbols = null)
    {
        if (string.IsNullOrWhiteSpace(grammarName))
        {
            throw new ArgumentException("Grammar name cannot be null, empty, or whitespace.", nameof(grammarName));
        }

        if (symbolModelVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(symbolModelVersion), "Symbol model version must be strictly positive.");
        }

        GrammarName = grammarName;
        Target = target;
        RuleName = ruleName;
        LanguageOrCompilerIdentity = languageOrCompilerIdentity;
        SymbolModelVersion = symbolModelVersion;
        SupportedSymbols = supportedSymbols is null
            ? s_defaultSupportedSymbols
            : supportedSymbols.ToImmutableHashSet();
    }

    /// <summary>
    /// Gets the name of the grammar that owns the embedded source.
    /// </summary>
    public string GrammarName { get; }

    /// <summary>
    /// Gets the preparation path that will consume the embedded source.
    /// </summary>
    public EmbeddedCodeTarget Target { get; }

    /// <summary>
    /// Gets the optional rule name associated with the embedded source.
    /// </summary>
    public string? RuleName { get; }

    /// <summary>
    /// Gets the optional explicit language or compiler identity configured by the caller.
    /// </summary>
    public string? LanguageOrCompilerIdentity { get; }

    /// <summary>
    /// Gets the version of the contextual symbol model understood by the preparer.
    /// </summary>
    public int SymbolModelVersion { get; }

    /// <summary>
    /// Gets the contextual symbols that are available to the preparer.
    /// </summary>
    public IReadOnlySet<EmbeddedCodeContextSymbol> SupportedSymbols { get; }

    /// <summary>Default contextual symbol set shared across all contexts created without an explicit symbol set.</summary>
    private static readonly ImmutableHashSet<EmbeddedCodeContextSymbol> s_defaultSupportedSymbols = ImmutableHashSet.Create(
        EmbeddedCodeContextSymbol.RuleName,
        EmbeddedCodeContextSymbol.InputPosition,
        EmbeddedCodeContextSymbol.AlternativeIndex,
        EmbeddedCodeContextSymbol.ElementIndex);
}
