using Utils.Parser.EmbeddedCode;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Configures the explicit prepared expression runtime policy build.
/// </summary>
public sealed record PreparedExpressionRuntimePolicyBuilderOptions
{
    /// <summary>
    /// Gets an options instance with default prepared expression runtime policy build behavior.
    /// </summary>
    public static PreparedExpressionRuntimePolicyBuilderOptions Default { get; } = new();

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

    /// <summary>
    /// Gets the base runtime feature policy to preserve, or <c>null</c> to start from <see cref="ParserRuntimeFeaturePolicy.Default"/>.
    /// </summary>
    public ParserRuntimeFeaturePolicy? BasePolicy { get; init; }
}
