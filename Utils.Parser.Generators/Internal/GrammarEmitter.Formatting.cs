using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{

    /// <summary>
    /// Gets the stable generated execution-context class name for a generated grammar facade.
    /// </summary>
    /// <param name="className">Generated grammar facade class name.</param>
    /// <returns>The generated execution-context class name.</returns>
    private static string GetExecutionContextClassName(string className)
    {
        return className + "ExecutionContext";
    }

    /// <summary>
    /// Escapes generated XML documentation text.
    /// </summary>
    /// <param name="value">Value to escape.</param>
    /// <returns>XML-safe text.</returns>
    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string JoinRuleNames(List<G4Rule> rules)
    {
        if (rules.Count == 0) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < rules.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"_{Sanitize(rules[i].Name)}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns the string syntax name exposed for IDE integrations.
    /// </summary>
    /// <param name="grammarName">Declared grammar name.</param>
    /// <returns>The custom syntax name derived from the grammar name.</returns>
    private static string GetStringSyntaxName(string grammarName)
    {
        if (grammarName.EndsWith(GrammarSuffix, StringComparison.Ordinal) && grammarName.Length > GrammarSuffix.Length)
        {
            return grammarName.Substring(0, grammarName.Length - GrammarSuffix.Length);
        }

        return grammarName;
    }

    /// <summary>
    /// Returns keyword lexer rule names whose recognized text matches the rule name case-insensitively.
    /// </summary>
    /// <param name="grammar">Grammar to inspect.</param>
    /// <returns>Sorted keyword names.</returns>
    private static string[] GetStringSyntaxKeywords(G4Grammar grammar)
    {
        var result = new List<string>();

        foreach (var rule in grammar.LexerRules)
        {
            if (TryGetKeywordName(rule, out var keywordName))
            {
                result.Add(keywordName);
            }
        }

        foreach (var mode in grammar.ExtraModes)
        {
            foreach (var rule in mode.Rules)
            {
                if (TryGetKeywordName(rule, out var keywordName))
                {
                    result.Add(keywordName);
                }
            }
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(keyword => keyword, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Returns exact lexer literals composed only of non-alphanumeric characters.
    /// </summary>
    /// <param name="grammar">Grammar to inspect.</param>
    /// <returns>Sorted literal tokens.</returns>
    private static string[] GetStringSyntaxNonAlphanumericTokens(G4Grammar grammar)
    {
        var result = new List<string>();

        foreach (var rule in grammar.LexerRules)
        {
            if (TryGetNonAlphanumericToken(rule, out var token))
            {
                result.Add(token);
            }
        }

        foreach (var mode in grammar.ExtraModes)
        {
            foreach (var rule in mode.Rules)
            {
                if (TryGetNonAlphanumericToken(rule, out var token))
                {
                    result.Add(token);
                }
            }
        }

        return result
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Returns lexer rule names containing the specified marker.
    /// </summary>
    /// <param name="grammar">Grammar to inspect.</param>
    /// <param name="marker">Case-insensitive marker to search in rule names.</param>
    /// <returns>Sorted lexer rule names.</returns>
    private static string[] GetStringSyntaxRulesByName(G4Grammar grammar, string marker)
    {
        var result = new List<string>();

        foreach (var rule in grammar.LexerRules)
        {
            if (RuleNameContains(rule, marker))
            {
                result.Add(rule.Name);
            }
        }

        foreach (var mode in grammar.ExtraModes)
        {
            foreach (var rule in mode.Rules)
            {
                if (RuleNameContains(rule, marker))
                {
                    result.Add(rule.Name);
                }
            }
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(ruleName => ruleName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Determines whether the lexer rule represents a keyword that exactly matches its rule name, ignoring case.
    /// </summary>
    /// <param name="rule">Rule to inspect.</param>
    /// <param name="keywordName">Resolved keyword name when the rule is a keyword.</param>
    /// <returns><see langword="true"/> when the rule is a string-syntax keyword.</returns>
    private static bool TryGetKeywordName(G4Rule rule, out string keywordName)
    {
        keywordName = string.Empty;

        if (!TryGetExactLexerLiteral(rule, out var literal))
        {
            return false;
        }

        if (!string.Equals(rule.Name, literal, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        keywordName = rule.Name;
        return true;
    }

    /// <summary>
    /// Determines whether the lexer rule resolves to a non-alphanumeric exact literal.
    /// </summary>
    /// <param name="rule">Rule to inspect.</param>
    /// <param name="token">Resolved literal token when the rule qualifies.</param>
    /// <returns><see langword="true"/> when the rule maps to a punctuation or operator token.</returns>
    private static bool TryGetNonAlphanumericToken(G4Rule rule, out string token)
    {
        token = string.Empty;

        if (!TryGetExactLexerLiteral(rule, out var literal) || literal.Length == 0)
        {
            return false;
        }

        if (literal.Any(char.IsLetterOrDigit))
        {
            return false;
        }

        token = literal;
        return true;
    }

    /// <summary>
    /// Attempts to extract an exact literal matched by a lexer rule.
    /// </summary>
    /// <param name="rule">Rule to inspect.</param>
    /// <param name="literal">Resolved literal when extraction succeeds.</param>
    /// <returns><see langword="true"/> when the rule resolves to one exact literal.</returns>
    private static bool TryGetExactLexerLiteral(G4Rule rule, out string literal)
    {
        literal = string.Empty;

        if (rule.IsFragment)
        {
            return false;
        }

        return TryGetCaseInsensitiveLiteral(rule.Content, out literal);
    }

    /// <summary>
    /// Determines whether the rule name contains the requested marker.
    /// </summary>
    /// <param name="rule">Rule to inspect.</param>
    /// <param name="marker">Marker searched in the rule name.</param>
    /// <returns><see langword="true"/> when the marker appears in the rule name.</returns>
    private static bool RuleNameContains(G4Rule rule, string marker)
    {
        if (rule.IsFragment)
        {
            return false;
        }

        return rule.Name.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Attempts to extract a single case-insensitive literal from grammar content.
    /// </summary>
    /// <param name="content">Grammar content to inspect.</param>
    /// <param name="literal">Resolved literal when extraction succeeds.</param>
    /// <returns><see langword="true"/> when the content maps to a single exact literal.</returns>
    private static bool TryGetCaseInsensitiveLiteral(G4Content content, out string literal)
    {
        switch (content)
        {
            case G4Alternation alternation:
                if (alternation.Alternatives.Count == 1)
                {
                    return TryGetCaseInsensitiveLiteral(alternation.Alternatives[0], out literal);
                }

                break;

            case G4Alternative alternative:
                if (alternative.Items.Count == 0)
                {
                    literal = string.Empty;
                    return false;
                }

                var alternativeBuilder = new StringBuilder();
                foreach (var item in alternative.Items)
                {
                    if (!TryGetCaseInsensitiveLiteral(item, out var itemLiteral))
                    {
                        literal = string.Empty;
                        return false;
                    }

                    alternativeBuilder.Append(itemLiteral);
                }

                literal = alternativeBuilder.ToString();
                return true;

            case G4Sequence sequence:
                var sequenceBuilder = new StringBuilder();
                foreach (var item in sequence.Items)
                {
                    if (!TryGetCaseInsensitiveLiteral(item, out var itemLiteral))
                    {
                        literal = string.Empty;
                        return false;
                    }

                    sequenceBuilder.Append(itemLiteral);
                }

                literal = sequenceBuilder.ToString();
                return sequence.Items.Count > 0;

            case G4LiteralMatch literalMatch:
                literal = literalMatch.Value;
                return true;

            case G4CharClassMatch charClassMatch:
                return TryGetCaseInsensitiveCharacter(charClassMatch, out literal);
        }

        literal = string.Empty;
        return false;
    }

    /// <summary>
    /// Attempts to extract a single character from a character class while allowing case variants only.
    /// </summary>
    /// <param name="charClassMatch">Character class to inspect.</param>
    /// <param name="literal">Resolved single-character literal when extraction succeeds.</param>
    /// <returns><see langword="true"/> when the character class maps to one logical character.</returns>
    private static bool TryGetCaseInsensitiveCharacter(G4CharClassMatch charClassMatch, out string literal)
    {
        literal = string.Empty;

        if (charClassMatch.Negated)
        {
            return false;
        }

        var values = new List<char>();
        foreach (var (lo, hi) in charClassMatch.Entries)
        {
            if (hi.HasValue)
            {
                return false;
            }

            values.Add(lo);
        }

        if (values.Count == 0)
        {
            return false;
        }

        char canonical = char.ToUpperInvariant(values[0]);
        if (values.Any(value => char.ToUpperInvariant(value) != canonical))
        {
            return false;
        }

        literal = values[0].ToString();
        return true;
    }

    /// <summary>
    /// Joins string literals for generated C# array initializers.
    /// </summary>
    /// <param name="values">Values to join.</param>
    /// <returns>Comma-separated C# string literals.</returns>
    private static string JoinStringLiterals(IEnumerable<string> values)
    {
        return string.Join(", ", values.Select(value => $"\"{Escape(value)}\""));
    }

    private static string Sanitize(string name)
    {
        // Replace characters invalid in C# identifiers
        var sb = new StringBuilder();
        foreach (char c in name)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return sb.ToString();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string CharLiteral(char c) => c switch
    {
        '\\' => @"'\\'",
        '\'' => @"'\''",
        '\n' => @"'\n'",
        '\r' => @"'\r'",
        '\t' => @"'\t'",
        '\0' => @"'\0'",
        _    => char.IsControl(c)
                    ? $"'\\u{(int)c:X4}'"
                    : $"'{c}'"
    };
}
