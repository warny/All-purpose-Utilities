namespace Utils.Parser.Runtime;

/// <summary>
/// Provides syntactic top-level splitting of raw rule-call argument text preserved from <c>callee[...]</c> grammar clauses.
/// </summary>
public static class ParserRawArgumentSplitter
{
    /// <summary>Splits raw rule-call argument text into top-level argument slices at commas.</summary>
    public static IReadOnlyList<string> SplitTopLevel(string rawArguments)
        => Utils.Parser.Antlr4.Common.ParserRawArgumentSplitter.SplitTopLevel(rawArguments);
}
