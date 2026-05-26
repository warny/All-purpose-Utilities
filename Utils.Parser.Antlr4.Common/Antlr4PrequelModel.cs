namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Shared model for ANTLR4 grammar prequel metadata.
/// </summary>
/// <param name="Options">Optional grammar options block metadata.</param>
/// <param name="Imports">Imported grammar metadata entries in source order.</param>
/// <param name="Actions">Grammar actions in source order.</param>
/// <param name="DeclaredTokens">Declared tokens from <c>tokens { ... }</c> blocks.</param>
/// <param name="DeclaredChannels">Declared channels from <c>channels { ... }</c> blocks.</param>
public sealed record Antlr4PrequelModel(
    Antlr4OptionSet? Options,
    IReadOnlyList<Antlr4ImportInfo> Imports,
    IReadOnlyList<Antlr4ActionInfo> Actions,
    ISet<string> DeclaredTokens,
    ISet<string> DeclaredChannels);
