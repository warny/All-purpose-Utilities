namespace Utils.Parser.Antlr4.Common.Diagnostics;

/// <summary>
/// Neutral diagnostic fact codes derived from <see cref="Antlr4PrequelModel"/>.
/// </summary>
public enum Antlr4PrequelDiagnosticCode
{
    /// <summary>
    /// Indicates an import declaration was parsed but cannot be resolved by this model.
    /// </summary>
    ImportParsedButNotResolved,

    /// <summary>
    /// Indicates a <c>tokens { ... }</c> block is present.
    /// </summary>
    TokensBlockIgnored,

    /// <summary>
    /// Indicates a non-default channel declaration is present.
    /// </summary>
    ChannelsBlockIgnored,

    /// <summary>
    /// Indicates a grammar-level action is present.
    /// </summary>
    GrammarActionIgnored,
}
