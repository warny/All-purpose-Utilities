namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Represents a single ANTLR4 <c>import</c> entry, with an optional alias.
/// </summary>
/// <param name="GrammarName">Imported grammar name.</param>
/// <param name="Alias">Optional alias token used on the left side of <c>=</c> in the import declaration.</param>
internal sealed record Antlr4ImportInfo(string GrammarName, string? Alias);
