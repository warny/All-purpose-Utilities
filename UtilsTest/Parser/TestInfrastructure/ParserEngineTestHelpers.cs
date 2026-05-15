using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser.TestInfrastructure;

/// <summary>
/// Provides lightweight helpers for parser-engine tests without hiding test invariants.
/// </summary>
internal static class ParserEngineTestHelpers
{
    private static readonly CompiledGrammar Grammar = new(ExpGrammar.Build());

    /// <summary>
    /// Parses an input expression using the shared expression grammar.
    /// </summary>
    internal static ParseNode Parse(string input) => Grammar.Parse(input);

    /// <summary>
    /// Creates a token with default mode and channel values.
    /// </summary>
    internal static Token Token(int start, int length, string ruleName, string text)
        => new(new SourceSpan(start, length), ruleName, "DEFAULT_MODE", "DEFAULT_CHANNEL", text);

    /// <summary>
    /// Returns the first lexer node for a given lexer rule name.
    /// </summary>
    internal static LexerNode? FindFirstLexerNode(ParseNode node, string ruleName)
        => new ParseTreeNavigator(node)
            .Descendants()
            .Prepend(new ParseTreeNavigator(node))
            .FirstOrDefault(n => n.IsLexer && n.RuleName == ruleName)
            ?.Node as LexerNode;

    /// <summary>
    /// Returns all lexer nodes for a given lexer rule name.
    /// </summary>
    internal static List<LexerNode> FindAllLexerNodes(ParseNode node, string ruleName)
        => new ParseTreeNavigator(node)
            .Descendants()
            .Prepend(new ParseTreeNavigator(node))
            .Where(n => n.IsLexer && n.RuleName == ruleName)
            .Select(n => (LexerNode)n.Node)
            .ToList();

    /// <summary>
    /// Returns all lexer nodes in a parse tree.
    /// </summary>
    internal static List<LexerNode> FindAllLexerNodesAny(ParseNode node)
        => new ParseTreeNavigator(node)
            .Descendants()
            .Prepend(new ParseTreeNavigator(node))
            .Where(n => n.IsLexer)
            .Select(n => (LexerNode)n.Node)
            .ToList();

    /// <summary>
    /// Returns the first parser node for a given parser rule name.
    /// </summary>
    internal static ParserNode? FindFirstParserNode(ParseNode node, string ruleName)
        => new ParseTreeNavigator(node)
            .Descendants()
            .Prepend(new ParseTreeNavigator(node))
            .FirstOrDefault(n => n.IsParser && n.RuleName == ruleName)
            ?.Node as ParserNode;
}
