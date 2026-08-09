using System.Collections.Immutable;

namespace Utils.Parser.Model;

/// <summary>
/// Binds a grammar declared <c>superClass</c> to runtime lexer extensions.
/// </summary>
public sealed record GrammarExtensionBinding
{
    private IReadOnlySet<string> _lexerRuleNames = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
    private IReadOnlySet<string> _declaredTokens = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
    private IReadOnlySet<string> _declaredChannels = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);

    /// <summary>Grammar name declaring the binding.</summary>
    public string GrammarName { get; init; } = string.Empty;

    /// <summary>Grammar type to which the binding applies.</summary>
    public GrammarType AppliesTo { get; init; }

    /// <summary>Name of the declared ANTLR <c>superClass</c>.</summary>
    public string SuperClassName { get; init; } = string.Empty;

    /// <summary>Gets an immutable ordinal set of lexer rules declared by the grammar.</summary>
    public IReadOnlySet<string> LexerRuleNames
    {
        get => _lexerRuleNames;
        init => _lexerRuleNames = value.ToImmutableHashSet(StringComparer.Ordinal);
    }

    /// <summary>Gets an immutable ordinal set of declared tokens.</summary>
    public IReadOnlySet<string> DeclaredTokens
    {
        get => _declaredTokens;
        init => _declaredTokens = value.ToImmutableHashSet(StringComparer.Ordinal);
    }

    /// <summary>Gets an immutable ordinal set of declared channels.</summary>
    public IReadOnlySet<string> DeclaredChannels
    {
        get => _declaredChannels;
        init => _declaredChannels = value.ToImmutableHashSet(StringComparer.Ordinal);
    }
}
