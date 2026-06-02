using Utils.Parser.EmbeddedCode;

namespace Utils.Parser.Expressions;

/// <summary>
/// Configures explicit registry building for prepared expression embedded-code artifacts.
/// </summary>
public sealed record PreparedExpressionEmbeddedCodeRegistryBuilderOptions
{
    /// <summary>
    /// Gets an options instance with default registry builder behavior.
    /// </summary>
    public static PreparedExpressionEmbeddedCodeRegistryBuilderOptions Default { get; } = new();

    /// <summary>
    /// Gets the grammar name supplied to preparation contexts, or <c>null</c> to use the parser definition name.
    /// </summary>
    public string? GrammarName { get; init; }

    /// <summary>
    /// Gets the explicit language or compiler identity supplied to preparation contexts.
    /// </summary>
    public string? LanguageOrCompilerIdentity { get; init; }

    /// <summary>
    /// Gets the contextual symbols available to the preparer, or <c>null</c> to use preparation defaults.
    /// </summary>
    public IReadOnlySet<EmbeddedCodeContextSymbol>? SupportedSymbols { get; init; }
}
