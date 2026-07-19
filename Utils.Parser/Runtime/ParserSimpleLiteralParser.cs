namespace Utils.Parser.Runtime;

/// <summary>
/// Parses the narrow simple-literal subset supported by parser rule-call binding.
/// </summary>
public static class ParserSimpleLiteralParser
{
    /// <summary>Attempts to parse a supported literal without evaluating expressions.</summary>
    public static bool TryParse(string rawText, out object? value)
        => Utils.Parser.Antlr4.Common.ParserSimpleLiteralParser.TryParse(rawText, out value);
}
