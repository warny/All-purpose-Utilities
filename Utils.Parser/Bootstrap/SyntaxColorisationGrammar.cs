using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace Utils.Parser.Bootstrap;

/// <summary>
/// Represents one classification section in a syntax colorisation descriptor.
/// </summary>
public sealed class SyntaxColorisationSection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxColorisationSection"/> class.
    /// </summary>
    /// <param name="classification">Section classification name.</param>
    public SyntaxColorisationSection(string classification)
    {
        Classification = classification;
    }

    /// <summary>
    /// Gets the classification name.
    /// </summary>
    public string Classification { get; }

    /// <summary>
    /// Gets descriptor rules associated with the classification.
    /// </summary>
    public List<string> Rules { get; } = new();
}

/// <summary>
/// Represents a parsed syntax colorisation descriptor document.
/// </summary>
public sealed class SyntaxColorisationDocument
{
    /// <summary>
    /// Gets declared file extensions.
    /// </summary>
    public List<string> FileExtensions { get; } = new();

    /// <summary>
    /// Gets declared StringSyntax extensions.
    /// </summary>
    public List<string> StringSyntaxExtensions { get; } = new();

    /// <summary>
    /// Gets declared classification sections.
    /// </summary>
    public List<SyntaxColorisationSection> Sections { get; } = new();
}

/// <summary>
/// Parses <c>.syntaxcolor</c> descriptor content using a grammar executed by <see cref="ParserEngine"/>.
/// </summary>
public static class SyntaxColorisationGrammar
{
    private static readonly ParserDefinition Definition = RuleResolver.Resolve(Build());

    /// <summary>
    /// Parses descriptor text from a file.
    /// </summary>
    /// <param name="filePath">Descriptor file path.</param>
    /// <returns>Parsed descriptor document.</returns>
    public static SyntaxColorisationDocument ParseFile(string filePath)
    {
        string content = File.ReadAllText(filePath);
        return Parse(content);
    }

    /// <summary>
    /// Parses descriptor text content.
    /// </summary>
    /// <param name="source">Descriptor source text.</param>
    /// <returns>Parsed descriptor document.</returns>
    public static SyntaxColorisationDocument Parse([StringSyntax("SyntaxColorisation")] string source)
    {
        string normalizedSource = NormalizeRuleContinuationLines(source);
        var lexer = new LexerEngine(Definition);
        int maxTokenCount = ComputeTokenLimit(normalizedSource);
        var tokens = new List<Token>();
        foreach (Token token in lexer.Tokenize(new StringCharStream(normalizedSource)))
        {
            if (token.RuleName is "WS" or "LINE_COMMENT" or "HASH_COMMENT")
            {
                continue;
            }

            tokens.Add(token);
            if (tokens.Count > maxTokenCount)
            {
                throw new InvalidOperationException(
                    $"Descriptor tokenization exceeded the safety limit ({maxTokenCount}) for a source length of {source.Length}.");
            }
        }

        var parser = new ParserEngine(Definition);
        ParseNode root = parser.Parse(tokens);

        return Convert(root);
    }

    /// <summary>
    /// Builds the descriptor grammar definition.
    /// </summary>
    /// <returns>Descriptor parser definition.</returns>
    public static ParserDefinition Build()
    {
        var lexerRules = new List<Rule>
        {
            L("WS", Q(CS(" \t\r\n"), 1, null)),
            L("LINE_COMMENT", Seq(Lit("/"), Lit("/"), Q(Neg(Ref("NEWLINE")), 0, null))),
            L("HASH_COMMENT", Seq(Lit("#"), Q(Neg(Ref("NEWLINE")), 0, null))),
            L("NEWLINE", Alt(Lit("\r\n"), Lit("\n"))),
            L("AT", Lit("@")),
            L("COLON", Lit(":")),
            L("PIPE", Lit("|")),
            L("IDENT", Q(CS("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-"), 1, null)),
            L("QUOTED", Seq(Lit("\""), Q(Neg(Alt(Lit("\""), Ref("NEWLINE"))), 0, null), Lit("\"")))
        };

        var parserRules = new List<Rule>
        {
            P("document", Q(Ref("entry"), 0, null)),
            P("entry", Alt(Ref("directive"), Ref("section"))),
            P("directive", Seq(Ref("AT"), Ref("IDENT"), Ref("COLON"), Ref("value"))),
            P("section", Seq(Ref("value"), Ref("COLON"), Ref("ruleList"))),
            P("ruleList", Seq(Ref("value"), Q(Seq(Ref("PIPE"), Ref("value")), 0, null))),
            P("value", Alt(Ref("QUOTED"), Ref("IDENT")))
        };

        return new ParserDefinition(
            "SyntaxColorisation",
            GrammarType.Combined,
            null,
            Array.Empty<GrammarAction>(),
            Array.Empty<GrammarImport>(),
            new[] { new LexerMode("DEFAULT_MODE", lexerRules) },
            parserRules,
            parserRules[0]);
    }

    /// <summary>
    /// Converts a parsed descriptor tree to a strongly typed document.
    /// </summary>
    /// <param name="root">Parse root node.</param>
    /// <returns>Parsed descriptor document.</returns>
    private static SyntaxColorisationDocument Convert(ParseNode root)
    {
        if (root is not ParserNode documentNode || documentNode.Rule.Name != "document")
        {
            throw new InvalidOperationException("Syntax colorisation descriptor root must be 'document'.");
        }

        var document = new SyntaxColorisationDocument();

        foreach (ParserNode entry in Descendants(documentNode, "entry"))
        {
            ParserNode? directive = First(entry, "directive");
            if (directive != null)
            {
                ApplyDirective(document, directive);
                continue;
            }

            ParserNode? sectionNode = First(entry, "section");
            if (sectionNode != null)
            {
                document.Sections.Add(ReadSection(sectionNode));
            }
        }

        return document;
    }

    /// <summary>
    /// Applies one descriptor directive to the target document.
    /// </summary>
    /// <param name="document">Target descriptor document.</param>
    /// <param name="directiveNode">Directive parse node.</param>
    private static void ApplyDirective(SyntaxColorisationDocument document, ParserNode directiveNode)
    {
        string directiveName = ReadLexerValue(directiveNode, "IDENT");
        string value = ReadParserValue(directiveNode, "value");

        if (directiveName.Equals("FileExtension", StringComparison.OrdinalIgnoreCase))
        {
            document.FileExtensions.Add(value);
            return;
        }

        if (directiveName.Equals("StringSyntaxExtension", StringComparison.OrdinalIgnoreCase))
        {
            document.StringSyntaxExtensions.Add(value);
            return;
        }

        throw new InvalidOperationException($"Unsupported directive '{directiveName}'.");
    }

    /// <summary>
    /// Reads one section from the parse tree.
    /// </summary>
    /// <param name="sectionNode">Section parse node.</param>
    /// <returns>Parsed section.</returns>
    private static SyntaxColorisationSection ReadSection(ParserNode sectionNode)
    {
        string classification = ReadParserValue(sectionNode, "value");
        ParserNode ruleList = First(sectionNode, "ruleList")
            ?? throw new InvalidOperationException("Section must include a rule list.");

        var section = new SyntaxColorisationSection(classification);
        foreach (ParserNode valueNode in Descendants(ruleList, "value"))
        {
            section.Rules.Add(ReadValue(valueNode));
        }

        return section;
    }

    /// <summary>
    /// Reads a parser child value by parser rule name.
    /// </summary>
    /// <param name="parent">Parent parser node.</param>
    /// <param name="ruleName">Expected parser rule name.</param>
    /// <returns>Value text.</returns>
    private static string ReadParserValue(ParserNode parent, string ruleName)
    {
        ParserNode node = First(parent, ruleName)
            ?? throw new InvalidOperationException($"Missing parser rule '{ruleName}'.");

        return ReadValue(node);
    }

    /// <summary>
    /// Reads a lexer token value by token rule name.
    /// </summary>
    /// <param name="parent">Parent parser node.</param>
    /// <param name="ruleName">Expected token rule name.</param>
    /// <returns>Token text.</returns>
    private static string ReadLexerValue(ParserNode parent, string ruleName)
    {
        LexerNode node = parent.Children
            .OfType<LexerNode>()
            .FirstOrDefault(lexer => lexer.Rule.Name == ruleName)
            ?? throw new InvalidOperationException($"Missing token '{ruleName}'.");

        return node.Token.Text;
    }

    /// <summary>
    /// Reads one <c>value</c> rule as a string.
    /// </summary>
    /// <param name="valueNode">Value parse node.</param>
    /// <returns>Unescaped value text.</returns>
    private static string ReadValue(ParserNode valueNode)
    {
        LexerNode token = valueNode.Children
            .OfType<LexerNode>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Value node must contain one lexer token.");

        return token.Rule.Name == "QUOTED"
            ? token.Token.Text[1..^1]
            : token.Token.Text;
    }

    /// <summary>
    /// Enumerates parser descendants matching a rule name.
    /// </summary>
    /// <param name="node">Node to inspect.</param>
    /// <param name="ruleName">Rule name to match.</param>
    /// <returns>Matching descendants.</returns>
    private static IEnumerable<ParserNode> Descendants(ParseNode node, string ruleName)
    {
        if (node is not ParserNode rootNode)
        {
            yield break;
        }

        var stack = new Stack<ParseNode>();
        stack.Push(rootNode);

        while (stack.Count > 0)
        {
            ParseNode current = stack.Pop();
            if (current is not ParserNode currentParserNode)
            {
                continue;
            }

            if (currentParserNode.Rule.Name == ruleName)
            {
                yield return currentParserNode;
            }

            for (int i = currentParserNode.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(currentParserNode.Children[i]);
            }
        }
    }

    /// <summary>
    /// Finds the first parser child matching a rule name.
    /// </summary>
    /// <param name="node">Parent parser node.</param>
    /// <param name="ruleName">Rule name to match.</param>
    /// <returns>Matching child parser node, or <see langword="null"/>.</returns>
    private static ParserNode? First(ParserNode node, string ruleName)
    {
        return node.Children
            .OfType<ParserNode>()
            .FirstOrDefault(child => child.Rule.Name == ruleName);
    }

    /// <summary>
    /// Creates a lexer rule.
    /// </summary>
    private static Rule L(string name, RuleContent content)
        => new(name, 0, false, new Alternation(new[] { new Alternative(0, Associativity.Left, content) }))
        { Kind = RuleKind.Lexer };

    /// <summary>
    /// Creates a parser rule.
    /// </summary>
    private static Rule P(string name, RuleContent content)
        => new(name, 0, false, new Alternation(new[] { new Alternative(0, Associativity.Left, content) }))
        { Kind = RuleKind.Parser };

    /// <summary>
    /// Creates one literal tokenizer node.
    /// </summary>
    private static RuleContent Lit(string value) => new LiteralMatch(value);

    /// <summary>
    /// Creates one rule reference node.
    /// </summary>
    private static RuleContent Ref(string ruleName) => new RuleRef(ruleName);

    /// <summary>
    /// Creates one character-set tokenizer node.
    /// </summary>
    private static RuleContent CS(string chars) => new CharSetMatch(new HashSet<char>(chars), false);

    /// <summary>
    /// Creates one negation node.
    /// </summary>
    private static RuleContent Neg(RuleContent inner) => new Negation(inner);

    /// <summary>
    /// Creates a sequence node.
    /// </summary>
    private static RuleContent Seq(params RuleContent[] items) => new Sequence(items);

    /// <summary>
    /// Creates an alternation node.
    /// </summary>
    private static RuleContent Alt(params RuleContent[] items)
        => new Alternation(items.Select((item, index) => new Alternative(index, Associativity.Left, item)).ToArray());

    /// <summary>
    /// Creates a quantifier node.
    /// </summary>
    private static RuleContent Q(RuleContent inner, int min, int? max) => new Quantifier(inner, min, max);

    /// <summary>
    /// Computes the maximum allowed number of significant tokens for one descriptor source.
    /// </summary>
    /// <param name="source">Descriptor source text.</param>
    /// <returns>Maximum token count accepted during lexing.</returns>
    internal static int ComputeTokenLimit(string source)
    {
        int sourceLength = source?.Length ?? 0;
        return Math.Max(256, (sourceLength + 1) * 4);
    }

    /// <summary>
    /// Normalizes section rule continuation lines by prepending a <c>|</c> token when omitted.
    /// </summary>
    /// <param name="source">Raw descriptor source.</param>
    /// <returns>Normalized descriptor source.</returns>
    private static string NormalizeRuleContinuationLines(string source)
    {
        string[] lines = source.Replace("\r\n", "\n").Split('\n');
        var normalized = new List<string>(lines.Length);
        bool insideSection = false;
        bool isFirstRuleLineInSection = false;

        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                normalized.Add(rawLine);
                continue;
            }

            if (trimmed.Contains(":", StringComparison.Ordinal))
            {
                insideSection = !trimmed.StartsWith("@", StringComparison.Ordinal);
                isFirstRuleLineInSection = insideSection;
                normalized.Add(rawLine);
                continue;
            }

            if (insideSection)
            {
                if (isFirstRuleLineInSection)
                {
                    normalized.Add(rawLine);
                    isFirstRuleLineInSection = false;
                    continue;
                }

                if (!trimmed.StartsWith("|", StringComparison.Ordinal))
                {
                    int indentLength = rawLine.Length - rawLine.TrimStart().Length;
                    normalized.Add(rawLine[..indentLength] + "| " + trimmed);
                    continue;
                }
            }

            if (insideSection)
            {
                normalized.Add(rawLine);
                isFirstRuleLineInSection = false;
                continue;
            }

            normalized.Add(rawLine);
        }

        return string.Join("\n", normalized);
    }
}
