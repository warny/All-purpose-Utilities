using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Builds default <c>.syntaxcolor</c> descriptor content from an ANTLR4 <c>.g4</c> grammar.
/// </summary>
public sealed class G4ColorizationDescriptorScaffolder
{
    private static readonly Regex LexerRuleRegex = new Regex(
        "(?ms)^\\s*(fragment\\s+)?(?<name>[A-Z][A-Z0-9_]*)\\s*:\\s*(?<body>.*?);",
        RegexOptions.Compiled);

    private static readonly Regex GrammarNameRegex = new Regex(
        "(?m)^\\s*(?:lexer\\s+|parser\\s+)?grammar\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*;",
        RegexOptions.Compiled);

    private static readonly Regex SingleWordLiteralRegex = new Regex(
        "^'(?<word>[A-Za-z_][A-Za-z0-9_]*)'$",
        RegexOptions.Compiled);

    private static readonly Regex OperatorLiteralRegex = new Regex(
        "^'(?<operator>[^A-Za-z0-9_\\s]+)'$",
        RegexOptions.Compiled);

    private static readonly string[] StandardKeywordRules = { "FOR", "WHILE", "DO", "IF", "SWITCH" };
    private static readonly string[] StandardNumberRules = { "NUMBER", "DECIMAL", "INT", "DIGIT", "DIGITS" };
    private static readonly string[] StandardStringRules = { "STRING", "QUOTED_STRING", "MULTILINE_STRING" };
    private static readonly string[] StandardOperatorRules = { "PLUS", "MINUS", "STAR", "SLASH", "LPAREN", "RPAREN", "LBRACK", "RBRACK", "LBRACE", "RBRACE", "OPERATOR" };
    private static readonly string[] StandardRawTextRules = { "TEXT" };

    /// <summary>
    /// Creates descriptor content from a grammar file.
    /// </summary>
    /// <param name="grammarFilePath">Path to the ANTLR4 grammar file.</param>
    /// <param name="fileExtensions">Optional file extensions to include in the descriptor.</param>
    /// <returns>Descriptor content text.</returns>
    public string CreateFromGrammarFile(string grammarFilePath, IEnumerable<string>? fileExtensions = null)
    {
        string grammarText = File.ReadAllText(grammarFilePath);
        return CreateFromGrammarText(grammarText, grammarFilePath, fileExtensions);
    }

    /// <summary>
    /// Creates descriptor content from grammar text.
    /// </summary>
    /// <param name="grammarText">ANTLR4 grammar content.</param>
    /// <param name="grammarFileName">Grammar source file name.</param>
    /// <param name="fileExtensions">Optional file extensions to include in the descriptor.</param>
    /// <returns>Descriptor content text.</returns>
    public string CreateFromGrammarText(string grammarText, string grammarFileName, IEnumerable<string>? fileExtensions = null)
    {
        string[] resolvedFileExtensions = NormalizeFileExtensions(fileExtensions, grammarFileName);
        string grammarName = ResolveGrammarName(grammarText, grammarFileName);
        string stringSyntaxExtension = ResolveStringSyntaxExtension(grammarName);

        List<LexerRuleModel> lexerRules = ParseLexerRules(grammarText);

        string[] keywordRules = lexerRules
            .Where(rule => !rule.IsFragment && IsKeywordRule(rule.Body))
            .Select(rule => rule.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(rule => rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] numberRules = lexerRules
            .Where(rule => !rule.IsFragment && IsNumberRule(rule.Name))
            .Select(rule => rule.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(rule => rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] stringRules = lexerRules
            .Where(rule => !rule.IsFragment && rule.Name.IndexOf("STRING", StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(rule => rule.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(rule => rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] operatorRules = lexerRules
            .Where(rule => !rule.IsFragment && (IsOperatorRule(rule.Body) || IsDelimiterRule(rule.Body)))
            .Select(rule => rule.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(rule => rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] rawTextRules = lexerRules
            .Where(rule => !rule.IsFragment && rule.Name.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(rule => rule.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(rule => rule, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return BuildDescriptorContent(
            resolvedFileExtensions,
            stringSyntaxExtension,
            keywordRules,
            numberRules,
            stringRules,
            operatorRules,
            rawTextRules);
    }

    /// <summary>
    /// Parses lexer rule declarations from grammar text.
    /// </summary>
    /// <param name="grammarText">ANTLR4 grammar content.</param>
    /// <returns>Parsed lexer rules.</returns>
    private static List<LexerRuleModel> ParseLexerRules(string grammarText)
    {
        var rules = new List<LexerRuleModel>();

        MatchCollection matches = LexerRuleRegex.Matches(grammarText);
        foreach (Match match in matches)
        {
            bool isFragment = !string.IsNullOrWhiteSpace(match.Groups[1].Value);
            string name = match.Groups["name"].Value.Trim();
            string body = match.Groups["body"].Value.Trim();
            rules.Add(new LexerRuleModel(name, body, isFragment));
        }

        return rules;
    }

    /// <summary>
    /// Resolves grammar name from source text.
    /// </summary>
    /// <param name="grammarText">ANTLR4 grammar content.</param>
    /// <param name="grammarFileName">Grammar file name used as fallback.</param>
    /// <returns>Grammar name.</returns>
    private static string ResolveGrammarName(string grammarText, string grammarFileName)
    {
        Match grammarNameMatch = GrammarNameRegex.Match(grammarText);
        if (grammarNameMatch.Success)
        {
            return grammarNameMatch.Groups["name"].Value;
        }

        return Path.GetFileNameWithoutExtension(grammarFileName);
    }

    /// <summary>
    /// Builds descriptor text from categorized rules.
    /// </summary>
    private static string BuildDescriptorContent(
        IEnumerable<string> fileExtensions,
        string stringSyntaxExtension,
        IEnumerable<string> keywordRules,
        IEnumerable<string> numberRules,
        IEnumerable<string> stringRules,
        IEnumerable<string> operatorRules,
        IEnumerable<string> rawTextRules)
    {
        var builder = new StringBuilder();

        foreach (string extension in fileExtensions)
        {
            builder.AppendLine($"@FileExtension : \"{extension}\"");
        }

        builder.AppendLine($"@StringSyntaxExtension : \"{stringSyntaxExtension}\"");
        builder.AppendLine("# Tag rule example:");
        builder.AppendLine("# Tag : TAG_OPEN | TAG_CLOSE");

        AppendSection(builder, "Number", numberRules, StandardNumberRules);
        AppendSection(builder, "String", stringRules, StandardStringRules);
        AppendSection(builder, "Keyword", keywordRules, StandardKeywordRules);
        AppendSection(builder, "Operator", operatorRules, StandardOperatorRules);
        AppendSection(builder, "\"Raw text\"", rawTextRules, StandardRawTextRules);

        return builder.ToString();
    }

    /// <summary>
    /// Appends one descriptor section and comments unused standard rules.
    /// </summary>
    private static void AppendSection(StringBuilder builder, string sectionName, IEnumerable<string> rules, IEnumerable<string> standardRules)
    {
        string[] resolvedRules = rules.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] unusedStandardRules = standardRules
            .Where(standardRule => !resolvedRules.Contains(standardRule, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        builder.AppendLine($"{sectionName} :");

        if (unusedStandardRules.Length > 0)
        {
            builder.AppendLine($"    # Unused standard rules: {string.Join(" | ", unusedStandardRules)}");
        }

        builder.AppendLine($"    {JoinRules(resolvedRules)}");
    }

    /// <summary>
    /// Normalizes file extensions and uses grammar file extension fallback when absent.
    /// </summary>
    private static string[] NormalizeFileExtensions(IEnumerable<string>? fileExtensions, string grammarFileName)
    {
        string[] normalized = (fileExtensions ?? Array.Empty<string>())
            .Select(extension => extension?.Trim())
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension!.StartsWith(".", StringComparison.Ordinal) ? extension! : $".{extension}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length > 0)
        {
            return normalized;
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(grammarFileName);
        return new[] { $".{fileNameWithoutExtension.ToLowerInvariant()}" };
    }

    /// <summary>
    /// Resolves the StringSyntax extension name from grammar name.
    /// </summary>
    private static string ResolveStringSyntaxExtension(string grammarName)
    {
        return grammarName.EndsWith("Grammar", StringComparison.Ordinal)
            ? grammarName.Substring(0, grammarName.Length - "Grammar".Length)
            : grammarName;
    }

    /// <summary>
    /// Returns a pipe-separated rule list or a placeholder when no rules were found.
    /// </summary>
    private static string JoinRules(IEnumerable<string> rules)
    {
        string[] resolved = rules.ToArray();
        return resolved.Length == 0 ? "TODO" : string.Join(" | ", resolved);
    }

    /// <summary>
    /// Checks whether a lexer rule body represents a single literal keyword.
    /// </summary>
    private static bool IsKeywordRule(string ruleBody)
    {
        Match match = SingleWordLiteralRegex.Match(ruleBody);
        return match.Success;
    }

    /// <summary>
    /// Checks whether a lexer rule body represents a single non-alphanumeric operator literal.
    /// </summary>
    private static bool IsOperatorRule(string ruleBody)
    {
        Match match = OperatorLiteralRegex.Match(ruleBody);
        return match.Success;
    }

    /// <summary>
    /// Checks whether a lexer rule body represents a delimiter literal.
    /// </summary>
    /// <param name="ruleBody">Rule body text.</param>
    /// <returns><see langword="true"/> when body is one delimiter literal.</returns>
    private static bool IsDelimiterRule(string ruleBody)
    {
        return ruleBody.Equals("'('", StringComparison.Ordinal) ||
               ruleBody.Equals("')'", StringComparison.Ordinal) ||
               ruleBody.Equals("'['", StringComparison.Ordinal) ||
               ruleBody.Equals("']'", StringComparison.Ordinal) ||
               ruleBody.Equals("'{'", StringComparison.Ordinal) ||
               ruleBody.Equals("'}'", StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks whether a rule name conventionally represents numbers.
    /// </summary>
    private static bool IsNumberRule(string ruleName)
    {
        return ruleName.IndexOf("NUMBER", StringComparison.OrdinalIgnoreCase) >= 0 ||
               ruleName.IndexOf("DECIMAL", StringComparison.OrdinalIgnoreCase) >= 0 ||
               ruleName.Equals("INT", StringComparison.OrdinalIgnoreCase) ||
               ruleName.Equals("DIGIT", StringComparison.OrdinalIgnoreCase) ||
               ruleName.Equals("DIGITS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stores one parsed lexer rule.
    /// </summary>
    private sealed record LexerRuleModel(string Name, string Body, bool IsFragment);
}
