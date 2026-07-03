using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Rewrites the deliberately small supported subset of ANTLR-style lexer attributes in embedded C#.
/// </summary>
internal static class EmbeddedLexerAttributeRewriter
{
    private static readonly HashSet<string> SupportedActionReads = new(StringComparer.Ordinal)
    {
        "text",
        "type",
        "channel",
        "mode",
        "line",
        "pos"
    };

    /// <summary>
    /// Rewrites supported lexer attribute reads and returns deterministic validation errors for unsupported forms.
    /// </summary>
    /// <param name="code">Embedded C# source to inspect.</param>
    /// <param name="isPredicate">Whether the source belongs to a lexer predicate.</param>
    /// <returns>The rewritten source and any validation errors.</returns>
    public static EmbeddedParserAttributeRewriteResult Rewrite(string code, bool isPredicate)
    {
        if (code is null) throw new ArgumentNullException(nameof(code));

        var errors = new List<string>();
        var output = new StringBuilder(code.Length);
        int index = 0;
        while (index < code.Length)
        {
            if (TryCopyTriviaOrLiteral(code, ref index, output))
            {
                continue;
            }

            if (code[index] != '$' || index + 1 >= code.Length || !IsIdentifierStart(code[index + 1]))
            {
                output.Append(code[index++]);
                continue;
            }

            int attributeStart = index;
            index++;
            string attribute = ReadIdentifier(code, ref index);
            int attributeEnd = index;
            string attributeText = "$" + attribute;
            int next = SkipWhitespace(code, index);
            if (next < code.Length && code[next] == '.')
            {
                errors.Add($"Chained lexer attribute '{attributeText}' is not supported.");
                output.Append(code, attributeStart, attributeEnd - attributeStart);
                continue;
            }

            if (TryRewriteSupportedWrite(code, attribute, attributeStart, attributeEnd, isPredicate, output, errors, ref index))
            {
                continue;
            }

            if (IsWriteContext(code, attributeStart, attributeEnd))
            {
                string reason = IsRefOrOutContext(code, attributeStart)
                    ? "ref/out lexer attributes are not supported by the ANTLR-style transformer."
                    : "Lexer attribute writes are not supported by the ANTLR-style transformer. Supported lexer attribute writes are $type = ..., $channel = ..., and $mode = ... in lexer inline actions.";
                errors.Add($"{reason} Unsupported attribute: '{attributeText}'.");
                output.Append(code, attributeStart, attributeEnd - attributeStart);
                continue;
            }

            if (isPredicate)
            {
                errors.Add($"Lexer attribute read '{attributeText}' is not supported in lexer predicates.");
                output.Append(code, attributeStart, attributeEnd - attributeStart);
                continue;
            }

            if (!SupportedActionReads.Contains(attribute))
            {
                errors.Add($"Lexer attribute '{attributeText}' is not supported. Supported lexer action attributes are $text, $type, $channel, $mode, $line, and $pos.");
                output.Append(code, attributeStart, attributeEnd - attributeStart);
                continue;
            }

            output.Append(attribute switch
            {
                "text" => "GetRequiredLexerText(context)",
                "type" => "GetRequiredLexerType(context)",
                "channel" => "GetRequiredLexerChannel(context)",
                "mode" => "GetRequiredLexerMode(context)",
                "line" => "GetRequiredLexerLine(context)",
                "pos" => "GetRequiredLexerPos(context)",
                _ => attributeText
            });
        }

        return new EmbeddedParserAttributeRewriteResult(output.ToString(), errors);
    }

    /// <summary>Rewrites a supported simple lexer attribute assignment statement when present.</summary>
    private static bool TryRewriteSupportedWrite(string code, string attribute, int attributeStart, int attributeEnd, bool isPredicate, StringBuilder output, List<string> errors, ref int index)
    {
        int assignment = SkipWhitespace(code, attributeEnd);
        if (assignment >= code.Length || code[assignment] != '=' || StartsWith(code, assignment, "==") || StartsWith(code, assignment, "=>"))
        {
            return false;
        }

        string attributeText = "$" + attribute;
        if (isPredicate)
        {
            errors.Add($"Lexer attributes are not supported in lexer predicates. Unsupported attribute: '{attributeText}'.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        if (!string.Equals(attribute, "type", StringComparison.Ordinal) && !string.Equals(attribute, "channel", StringComparison.Ordinal) && !string.Equals(attribute, "mode", StringComparison.Ordinal))
        {
            errors.Add($"Lexer attribute write '{attributeText}' is not supported. Supported lexer attribute writes are $type = ..., $channel = ..., and $mode = ... in lexer inline actions.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        int previous = PreviousNonWhitespace(code, attributeStart - 1);
        if (previous >= 0 && code[previous] != ';' && code[previous] != '{' && code[previous] != '}')
        {
            errors.Add($"Complex lexer attribute write '{attributeText}' is not supported. Supported lexer attribute writes are simple statements only.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        int valueStart = SkipWhitespace(code, assignment + 1);
        if (!TryReadSimpleAssignmentValue(code, valueStart, out string? value, out int valueEnd))
        {
            errors.Add($"Complex lexer attribute write '{attributeText}' is not supported. Supported lexer attribute writes are simple identifier or string assignments only.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        int terminator = SkipWhitespace(code, valueEnd);
        if (terminator >= code.Length || code[terminator] != ';')
        {
            errors.Add($"Complex lexer attribute write '{attributeText}' is not supported. Supported lexer attribute writes are simple statements only.");
            output.Append(code, attributeStart, attributeEnd - attributeStart);
            return true;
        }

        output.Append(attribute switch
        {
            "type" => "SetLexerType(result, ",
            "channel" => "SetLexerChannel(result, ",
            "mode" => "SetLexerMode(result, ",
            _ => throw new InvalidOperationException($"Unsupported lexer attribute write: {attribute}.")
        });
        output.Append('"');
        output.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        output.Append("\");");
        index = terminator + 1;
        return true;
    }

    /// <summary>Reads a supported assignment value as either an identifier or a simple quoted string.</summary>
    private static bool TryReadSimpleAssignmentValue(string code, int start, out string value, out int end)
    {
        value = string.Empty;
        end = start;
        if (start >= code.Length)
        {
            return false;
        }

        if (IsIdentifierStart(code[start]))
        {
            end = start;
            value = ReadIdentifier(code, ref end);
            return true;
        }

        if (code[start] != '"')
        {
            return false;
        }

        var builder = new StringBuilder();
        end = start + 1;
        while (end < code.Length)
        {
            char current = code[end++];
            if (current == '"')
            {
                value = builder.ToString();
                return true;
            }

            if (current == '\\' && end < code.Length)
            {
                char escaped = code[end++];
                builder.Append(escaped);
                continue;
            }

            builder.Append(current);
        }

        return false;
    }

    /// <summary>Copies comments and C# string or character literals without rewriting their contents.</summary>
    private static bool TryCopyTriviaOrLiteral(string code, ref int index, StringBuilder output)
    {
        int start = index;
        if (code[index] == '/' && index + 1 < code.Length && code[index + 1] == '/')
        {
            index += 2;
            while (index < code.Length && code[index] != '\n') index++;
        }
        else if (code[index] == '/' && index + 1 < code.Length && code[index + 1] == '*')
        {
            index += 2;
            while (index + 1 < code.Length && !(code[index] == '*' && code[index + 1] == '/')) index++;
            index = Math.Min(code.Length, index + 2);
        }
        else if (TryGetStringPrefix(code, index, out int quoteIndex, out bool verbatim))
        {
            index = quoteIndex + 1;
            while (index < code.Length)
            {
                if (code[index] == '"')
                {
                    if (verbatim && index + 1 < code.Length && code[index + 1] == '"')
                    {
                        index += 2;
                        continue;
                    }

                    index++;
                    break;
                }

                if (!verbatim && code[index] == '\\' && index + 1 < code.Length) index += 2;
                else index++;
            }
        }
        else if (code[index] == '\'')
        {
            index++;
            while (index < code.Length)
            {
                if (code[index] == '\\' && index + 1 < code.Length) index += 2;
                else if (code[index++] == '\'') break;
            }
        }
        else
        {
            return false;
        }

        output.Append(code, start, index - start);
        return true;
    }

    /// <summary>Recognizes normal, verbatim, interpolated, and verbatim interpolated C# string prefixes.</summary>
    private static bool TryGetStringPrefix(string code, int index, out int quoteIndex, out bool verbatim)
    {
        quoteIndex = index;
        verbatim = false;
        if (code[index] == '"') return true;
        if (code[index] == '@' && index + 1 < code.Length && code[index + 1] == '"')
        {
            quoteIndex = index + 1;
            verbatim = true;
            return true;
        }
        if (code[index] == '$' && index + 1 < code.Length && code[index + 1] == '"')
        {
            quoteIndex = index + 1;
            return true;
        }
        if (index + 2 < code.Length && ((code[index] == '$' && code[index + 1] == '@') || (code[index] == '@' && code[index + 1] == '$')) && code[index + 2] == '"')
        {
            quoteIndex = index + 2;
            verbatim = true;
            return true;
        }
        return false;
    }

    /// <summary>Detects assignment, increment/decrement, and ref/out write contexts around an attribute expression.</summary>
    private static bool IsWriteContext(string code, int start, int end)
    {
        int next = SkipWhitespace(code, end);
        string[] followingOperators = ["++", "--", "??=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", "="];
        if (followingOperators.Any(op => StartsWith(code, next, op)) && !StartsWith(code, next, "==") && !StartsWith(code, next, "=>")) return true;
        int previous = PreviousNonWhitespace(code, start - 1);
        if (previous >= 1 && ((code[previous - 1] == '+' && code[previous] == '+') || (code[previous - 1] == '-' && code[previous] == '-'))) return true;
        return IsRefOrOutContext(code, start);
    }

    /// <summary>Determines whether a lexer attribute is preceded by ref or out.</summary>
    private static bool IsRefOrOutContext(string code, int start)
    {
        int previous = PreviousNonWhitespace(code, start - 1);
        int wordEnd = previous + 1;
        while (previous >= 0 && IsIdentifierPart(code[previous])) previous--;
        string previousWord = code.Substring(previous + 1, wordEnd - previous - 1);
        return string.Equals(previousWord, "ref", StringComparison.Ordinal) || string.Equals(previousWord, "out", StringComparison.Ordinal);
    }

    /// <summary>Finds the previous non-whitespace character index.</summary>
    private static int PreviousNonWhitespace(string code, int index)
    {
        while (index >= 0 && char.IsWhiteSpace(code[index])) index--;
        return index;
    }

    /// <summary>Reads one identifier and advances the supplied index.</summary>
    private static string ReadIdentifier(string text, ref int index)
    {
        int start = index++;
        while (index < text.Length && IsIdentifierPart(text[index])) index++;
        return text.Substring(start, index - start);
    }

    /// <summary>Skips whitespace from a source index.</summary>
    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        return index;
    }

    /// <summary>Tests whether source text starts with a value using ordinal matching.</summary>
    private static bool StartsWith(string text, int index, string value)
    {
        return index >= 0 && index + value.Length <= text.Length && string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    /// <summary>Determines whether a character can start the supported identifier form.</summary>
    private static bool IsIdentifierStart(char value) => value == '_' || char.IsLetter(value);

    /// <summary>Determines whether a character can continue the supported identifier form.</summary>
    private static bool IsIdentifierPart(char value) => value == '_' || char.IsLetterOrDigit(value);
}
