namespace Utils.Parser.Model;

/// <summary>
/// Binds a grammar declared <c>superClass</c> to runtime lexer extensions.
/// </summary>
public sealed record GrammarExtensionBinding
{
    /// <summary>Grammar name declaring the binding.</summary>
    public string GrammarName { get; init; } = string.Empty;

    /// <summary>Grammar type to which the binding applies.</summary>
    public GrammarType AppliesTo { get; init; }

    /// <summary>Name of the declared ANTLR <c>superClass</c>.</summary>
    public string SuperClassName { get; init; } = string.Empty;

    /// <summary>Lexer rules declared by the grammar owning the binding.</summary>
    public IReadOnlySet<string> LexerRuleNames { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Tokens declared in <c>tokens { ... }</c>.</summary>
    public IReadOnlySet<string> DeclaredTokens { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Channels declared in <c>channels { ... }</c>.</summary>
    public IReadOnlySet<string> DeclaredChannels { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}
