using System.Collections.Immutable;

namespace Utils.Parser.Antlr4.Common;

/// <summary>Shared model for ANTLR4 grammar prequel metadata.</summary>
public sealed record Antlr4PrequelModel(
    Antlr4OptionSet? Options,
    IReadOnlyList<Antlr4ImportInfo> Imports,
    IReadOnlyList<Antlr4ActionInfo> Actions,
    IReadOnlyCollection<string> DeclaredTokens,
    IReadOnlyCollection<string> DeclaredChannels,
    bool HasTokensBlock = false,
    bool HasChannelsBlock = false)
{
    private IReadOnlyList<Antlr4ImportInfo> _imports = Imports.ToImmutableArray();
    private IReadOnlyList<Antlr4ActionInfo> _actions = Actions.ToImmutableArray();
    private IReadOnlyCollection<string> _declaredTokens = DeclaredTokens.ToImmutableHashSet(StringComparer.Ordinal);
    private IReadOnlyCollection<string> _declaredChannels = DeclaredChannels.ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>Gets an immutable snapshot of imports in source order.</summary>
    public IReadOnlyList<Antlr4ImportInfo> Imports
    {
        get => _imports;
        init => _imports = value.ToImmutableArray();
    }

    /// <summary>Gets an immutable snapshot of actions in source order.</summary>
    public IReadOnlyList<Antlr4ActionInfo> Actions
    {
        get => _actions;
        init => _actions = value.ToImmutableArray();
    }

    /// <summary>Gets the immutable ordinal set of declared tokens.</summary>
    public IReadOnlyCollection<string> DeclaredTokens
    {
        get => _declaredTokens;
        init => _declaredTokens = value.ToImmutableHashSet(StringComparer.Ordinal);
    }

    /// <summary>Gets the immutable ordinal set of declared channels.</summary>
    public IReadOnlyCollection<string> DeclaredChannels
    {
        get => _declaredChannels;
        init => _declaredChannels = value.ToImmutableHashSet(StringComparer.Ordinal);
    }
}
