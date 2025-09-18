﻿using System;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Utils.Arrays;
using Utils.Expressions.ExpressionBuilders;
using static Utils.Expressions.ExpressionBuilders.NewBuilder;

namespace Utils.Expressions.Builders;

/// <summary>
/// Provides an <see cref="IBuilder"/> implementation capable of parsing C-style syntax.
/// </summary>
public class CStyleBuilder : IBuilder
{
    /// <summary>
    /// Gets the collection of language symbols that can be recognised by this builder.
    /// </summary>
    public IEnumerable<string> Symbols
        => new string[] { InstructionSeparator.ToString(), ListSeparator.ToString() }
            .Union(SpaceSymbols.Select(s => s.ToString()))
            .Union(AdditionalSymbols)
            .Union(IntegerPrefixes.Select(p => p.Key))
            .Union(StartExpressionBuilders.Select(p => p.Key))
            .Union(StartExpressionBuilders.Values.OfType<IAdditionalTokens>().SelectMany(p => p.AdditionalTokens))
            .Union(FollowUpExpressionBuilder.Select(p => p.Key))
            .Union(FollowUpExpressionBuilder.Values.OfType<IAdditionalTokens>().SelectMany(p => p.AdditionalTokens))
        ;



    /// <summary>
    /// Gets the character separating instructions in the parsed text.
    /// </summary>
    public char InstructionSeparator { get; } = ';';

    /// <summary>
    /// Gets the character separating list items in the parsed text.
    /// </summary>
    public char ListSeparator { get; } = ',';

    /// <summary>
    /// Gets the characters treated as whitespace by the parser.
    /// </summary>
    public char[] SpaceSymbols { get; } = [' ', '\t', '\r', '\n'];

    /// <summary>
    /// Gets additional multi-character symbols recognised by the builder.
    /// </summary>
    public string[] AdditionalSymbols { get; } = ["=>"];

    /// <summary>
    /// Gets the ordered list of token readers used to scan the source text.
    /// </summary>
    public IEnumerable<TryReadToken> TokenReaders { get; } =
    [
        TryReadInterpolatedString1,
        TryReadInterpolatedString2,
        TryReadInterpolatedString3,
        TryReadName,
        TryReadNumber,
        TryReadComment,
        TryReadChar,
        TryReadString1,
        TryReadString2,
        TryReadString3,
    ];

    /// <summary>
    /// Gets the ordered list of transformers applied to normalise raw string tokens.
    /// </summary>
    public IEnumerable<StringTransformer> StringTransformers { get; } =
    [
        StringTransformInterpolated1,
        StringTransformInterpolated2,
        StringTransformInterpolated3,
        StringTransform1,
        StringTransform2,
        StringTransform3,
    ];

    /// <summary>
    /// Gets the builder responsible for constructing numeric constants.
    /// </summary>
    public IStartExpressionBuilder NumberBuilder { get; } = new NumberConstantBuilder();

    /// <summary>
    /// Gets the mapping between numeric prefixes and their respective bases.
    /// </summary>
    public IReadOnlyDictionary<string, int> IntegerPrefixes { get; } = new Dictionary<string, int>()
    {
        { "0x", 16 },
        { "0b", 2 },
        { "0o", 8 },
    };

    /// <summary>
    /// Gets the builders used to interpret tokens that can begin an expression.
    /// </summary>
    public IReadOnlyDictionary<string, IStartExpressionBuilder> StartExpressionBuilders { get; } =
        new Dictionary<string, IStartExpressionBuilder>()
        {
            { "null", new NullBuilder() },
            { "true", new TrueBuilder() },
            { "false", new FalseBuilder() },
            { "sizeof", new SizeOfBuilder() },
            { "typeof", new TypeofBuilder() },
            { "new", new NewBuilder() },

            { "if", new IfBuilder("else") },
            { "while", new WhileBuilder() },
            { "for", new ForBuilder() },
            { "foreach", new ForEachBuilder() },
            { "switch", new SwitchBuilder() },
            { "break", new BreakBuilder() },
            { "continue", new ContinueBuilder() },
            { "return", new ReturnBuilder() },

            { "+", new ReadNextExpressionBuilder() }, // ignore + sign and read the next expression
            { "-", new UnaryMinusBuilder() },
            { "!", new UnaryOperandBuilder(Expression.Not) },
            { "~", new UnaryOperandBuilder(Expression.Not) },

            { "++", new UnaryOperandBuilder(Expression.PreIncrementAssign) },
            { "--", new UnaryOperandBuilder(Expression.PreDecrementAssign) },

            { "{", new BlockBuilder("}", ";") },
            { "(", new ParenthesisBuilder(")", ",") },

            { ".", new ThrowParseException() },
            { ",", new ReadNextExpressionBuilder() },

        };

    /// <summary>
    /// Gets the builder used when no specific unary builder matches the token.
    /// </summary>
    public IStartExpressionBuilder FallbackUnaryBuilder { get; } = new DefaultUnaryBuilder();

    /// <summary>
    /// Gets the builders responsible for parsing tokens that follow an initial expression.
    /// </summary>
    public IReadOnlyDictionary<string, IFollowUpExpressionBuilder> FollowUpExpressionBuilder { get; } =
        new Dictionary<string, IFollowUpExpressionBuilder>()
        {
            { "[", new BracketBuilder() },
            { "(", new RightParenthesisBuilder() },

            { "+", new PlusOperatorBuilder() },
            { "-", new OperatorBuilder(Expression.Subtract, true) },
            { "*", new OperatorBuilder(Expression.Multiply, true) },
            { "/", new OperatorBuilder(Expression.Divide, true) },
            { "%", new OperatorBuilder(Expression.Modulo, true) },
            { "**", new OperatorBuilder(Expression.Power, true) },

            { "++", new PostOperationBuilder(Expression.PostIncrementAssign) },
            { "--", new PostOperationBuilder(Expression.PostDecrementAssign) },

            { "<<", new OperatorBuilder(Expression.LeftShift, false) },
            { ">>", new OperatorBuilder(Expression.RightShift, false) },

            { "<", new OperatorBuilder(Expression.LessThan, false) },
            { ">", new OperatorBuilder(Expression.GreaterThan, false) },
            { "<=", new OperatorBuilder(Expression.LessThanOrEqual, false) },
            { ">=", new OperatorBuilder(Expression.GreaterThanOrEqual, false) },
            { "==", new OperatorBuilder(Expression.Equal, false) },
            { "!=", new OperatorBuilder(Expression.Equal, false) },

            { "^", new OperatorBuilder(Expression.ExclusiveOr, false) },
            { "&", new OperatorBuilder(Expression.And, false) },
            { "|", new OperatorBuilder(Expression.Or, false) },
            { "&&", new OperatorBuilder(Expression.AndAlso, false) },
            { "||", new OperatorBuilder(Expression.OrElse, false) },

            { "??", new OperatorBuilder(Expression.Coalesce, false) },

            { ".", new MemberBuilder() },
            { "?.", new NullOrMemberBuilder() },

            { "is", new TypeMatchBuilder() },
            { "as", new TypeCastBuilder() },

            { "?", new ConditionalBuilder(":") },

            { "=", new AssignationBuilder(Expression.Assign) },

            { "+=", new AddAssignationBuilder() },
            { "-=", new AssignationBuilder(Expression.SubtractAssign) },
            { "*=", new AssignationBuilder(Expression.MultiplyAssign) },
            { "/=", new AssignationBuilder(Expression.DivideAssign) },
            { "%=", new AssignationBuilder(Expression.ModuloAssign) },
            { "**=", new AssignationBuilder(Expression.PowerAssign) },

            { "<<=", new OperatorBuilder(Expression.LeftShiftAssign, false) },
            { ">>=", new OperatorBuilder(Expression.RightShiftAssign, false) },

            { "^=", new AssignationBuilder(Expression.ExclusiveOrAssign) },
            { "|=", new AssignationBuilder(Expression.OrAssign) },
            { "&=", new AssignationBuilder(Expression.AndAssign) },
        };

    /// <summary>
    /// Gets the builder invoked when no binary or ternary builder handles the token.
    /// </summary>
    public IFollowUpExpressionBuilder FallbackBinaryOrTernaryBuilder { get; } = new ThrowParseException();


    /// <summary>
    /// Read a name from content at the specified index
    /// </summary>
    /// <param name="content">Code content</param>
    /// <param name="index">Extraction start index</param>
    /// <param name="length">Returned length</param>
    /// <returns><see langword="true"/> if a name has been read otherwise <see langword="false"/></returns>
    private static bool TryReadName(string content, int index, out int length)
    {
        length = 0;
        char c = content[index];
        // Check if the character is a letter, underscore, or dollar sign (indicating the start of an identifier)
        if (!char.IsLetter(c) && c != '_' && c != '$') return false;

        length++;
        for (int i = index + 1; i < content.Length; i++)
        {
            c = content[i];
            if (!char.IsLetterOrDigit(c) && c != '_') return true;
            length++;
        }
        return true;
    }

    /// <summary>
    /// Read a number from content at the specified index
    /// </summary>
    /// <param name="content">Code content</param>
    /// <param name="index">Extraction start index</param>
    /// <param name="length">Returned length</param>
    /// <returns><see langword="true"/> if a number has been read otherwise <see langword="false"/></returns>
    private static bool TryReadNumber(string content, int index, out int length)
    {
        length = 0;
        char c = content[index];
        if (!char.IsDigit(c)) return false;

        length = 1;
        //cas des nombres aux formats non décimaux de la forme 0x
        if (c == '0' && content.Length > index + length + 1 && char.IsLetter(content[index + 1]))
        {
            length++;
            for (int i = index + 3; i < content.Length; i++)
            {
                if (!char.IsLetterOrDigit(content[i])) break;
                length++;
            }
            if (length > 2) return true;
        }

        // Continue searching for the end of the numeric value
        bool hasDot = false;
        length = 1;
        for (int i = index + 1; i < content.Length; i++)
        {
            if (!char.IsDigit(content[i]) && content[i] != '.') break;
            if (content[i] == '.')
            {
                if (hasDot) return true;
                if (!char.IsDigit(content[i + 1])) return true;
                hasDot = true;
            }
            length++;
        }

        if (content.Length <= index + length + 1) return true;

        var nextChar = content[index + length];
        if ((new char[] { 'M', 'D', 'F', 'L', 'X' }).Contains(char.ToUpperInvariant(nextChar))) length++;

        return true;
    }

    /// <summary>
    /// Read a comment from content at the specified index
    /// </summary>
    /// <param name="content">Code content</param>
    /// <param name="index">Extraction start index</param>
    /// <param name="length">Returned length</param>
    /// <returns><see langword="true"/> if a comment has been read otherwise <see langword="false"/></returns>
    private static bool TryReadComment(string content, int index, out int length)
    {
        length = 0;
        char c = content[index];
        if (c != '/') return false;

        string[] endComment;
        switch (content[index + 1])
        {
            case '*':
                endComment = ["*/"];
                break;
            case '/':
                endComment = ["\r", "\n"];
                break;
            default:
                return false;
        }

        for (int i = index + 2; i < content.Length; i++)
        {
            c = content[i];
            foreach (var marker in endComment)
            {
                int j;
                for (j = 0; j < marker.Length; j++)
                {
                    if (marker[j] != content[i + j]) break;
                }
                if (j == marker.Length)
                {
                    length = i + j - index;
                    return true;
                }
            }
        }

        return false;

    }

    /// <summary>
    /// Attempts to read a character literal starting from <paramref name="index"/>.
    /// </summary>
    /// <param name="content">The input code.</param>
    /// <param name="index">The index of the first character to analyse.</param>
    /// <param name="length">When successful, the length of the matched token.</param>
    /// <returns><see langword="true"/> when a character literal is recognised.</returns>
    private static bool TryReadChar(string content, int index, out int length)
    {
        length = 0;
        char c = content[index];
        if (c != '\'') return false;

        // Found a backslash, so skip the next character
        if (content[index + 1] == '\\')
        {
            if (content[index + 3] != '\'') throw new ParseNoEndException("\'", index);
            length = 4;
            return true;
        }

        if (content[index + 2] != '\'') throw new ParseNoEndException("\'", index);
        length = 3;
        return true;
    }

    /// <summary>
    /// Attempts to read a standard quoted string starting from <paramref name="index"/>.
    /// </summary>
    /// <param name="content">The input code.</param>
    /// <param name="index">The index of the first character to analyse.</param>
    /// <param name="length">When successful, the length of the matched token.</param>
    /// <returns><see langword="true"/> when a quoted string is recognised.</returns>
    private static bool TryReadString1(string content, int index, out int length)
    {
        length = 0;
        ReadOnlySpan<char> span = content.AsSpan();
        if ((uint)index >= (uint)span.Length)
        {
            return false;
        }

        if (span[index] != '\"')
        {
            return false;
        }

        ReadOnlySpan<char> remaining = span[index..];
        if (remaining.StartWith("\"\"\"".AsSpan()))
        {
            return false;
        }

        int position = index + 1;
        while (position < span.Length)
        {
            char current = span[position];
            if (current == '\\')
            {
                position++;
                if (position >= span.Length)
                {
                    throw new ParseNoEndException("\"", index);
                }

                position++;
                continue;
            }

            if (current == '\"')
            {
                length = position - index + 1;
                return true;
            }

            position++;
        }

        throw new ParseNoEndException("\"", index);
    }

    /// <summary>
    /// Attempts to read a verbatim string starting from <paramref name="index"/>.
    /// </summary>
    /// <param name="content">The input code.</param>
    /// <param name="index">The index of the first character to analyse.</param>
    /// <param name="length">When successful, the length of the matched token.</param>
    /// <returns><see langword="true"/> when a verbatim string is recognised.</returns>
    private static bool TryReadString2(string content, int index, out int length)
    {
        length = 0;
        ReadOnlySpan<char> span = content.AsSpan();
        if (index < 0 || index + 1 >= span.Length)
        {
            return false;
        }

        if (span[index] != '@' || span[index + 1] != '\"')
        {
            return false;
        }

        int position = index + 2;
        while (position < span.Length)
        {
            if (span[position] == '\"')
            {
                if (position + 1 < span.Length && span[position + 1] == '\"')
                {
                    position += 2;
                    continue;
                }

                length = position - index + 1;
                return true;
            }

            position++;
        }

        throw new ParseNoEndException("\"", index);
    }

    /// <summary>
    /// Attempts to read a triple-quoted string starting from <paramref name="index"/>.
    /// </summary>
    /// <param name="content">The input code.</param>
    /// <param name="index">The index of the first character to analyse.</param>
    /// <param name="length">When successful, the length of the matched token.</param>
    /// <returns><see langword="true"/> when a triple-quoted string is recognised.</returns>
    private static bool TryReadString3(string content, int index, out int length)
    {
        length = 0;
        ReadOnlySpan<char> span = content.AsSpan();
        if ((uint)index >= (uint)span.Length)
        {
            return false;
        }

        if (span[index] != '\"')
        {
            return false;
        }

        ReadOnlySpan<char> remaining = span[index..];
        if (!remaining.StartWith("\"\"\"".AsSpan()))
        {
            return false;
        }

        int delimiterLength = 0;
        while (delimiterLength < remaining.Length && remaining[delimiterLength] == '\"')
        {
            delimiterLength++;
        }

        int position = index + delimiterLength;
        while (position < span.Length)
        {
            if (span[position] != '\"')
            {
                position++;
                continue;
            }

            int runLength = 0;
            while (position + runLength < span.Length && span[position + runLength] == '\"')
            {
                runLength++;
                if (runLength == delimiterLength)
                {
                    length = position + runLength - index;
                    return true;
                }
            }

            position += Math.Max(runLength, 1);
        }

        string prefix = new string('\"', delimiterLength);
        throw new ParseNoEndException(prefix, index);
    }

    /// <summary>
    /// Attempts to read an interpolated string with escaped content starting from <paramref name="index"/>.
    /// </summary>
    /// <param name="content">The input code.</param>
    /// <param name="index">The index of the first character to analyse.</param>
    /// <param name="length">When successful, the length of the matched token.</param>
    /// <returns><see langword="true"/> when an interpolated string is recognised.</returns>
    private static bool TryReadInterpolatedString1(string content, int index, out int length)
    {
        length = 0;
        if (content.Length < index + 2 || content[index] != '$') return false;
        return TryReadString1(content, index + 1, out length) && (length += 1) > 0;
    }

    /// <summary>
    /// Attempts to read a verbatim interpolated string starting from <paramref name="index"/>.
    /// </summary>
    /// <param name="content">The input code.</param>
    /// <param name="index">The index of the first character to analyse.</param>
    /// <param name="length">When successful, the length of the matched token.</param>
    /// <returns><see langword="true"/> when an interpolated verbatim string is recognised.</returns>
    private static bool TryReadInterpolatedString2(string content, int index, out int length)
    {
        length = 0;
        if (content.Length < index + 3) return false;
        if (content[index] == '$' && content[index + 1] == '@')
        {
            if (TryReadString2(content, index + 1, out length)) { length += 1; return true; }
        }
        else if (content[index] == '@' && content[index + 1] == '$')
        {
            if (TryReadString2(content, index + 1, out length)) return true;
        }
        return false;
    }

    /// <summary>
    /// Attempts to read a triple-quoted interpolated string starting from <paramref name="index"/>.
    /// </summary>
    /// <param name="content">The input code.</param>
    /// <param name="index">The index of the first character to analyse.</param>
    /// <param name="length">When successful, the length of the matched token.</param>
    /// <returns><see langword="true"/> when a triple-quoted interpolated string is recognised.</returns>
    private static bool TryReadInterpolatedString3(string content, int index, out int length)
    {
        length = 0;
        if (content.Length < index + 4 || content[index] != '$') return false;
        return TryReadString3(content, index + 1, out length) && (length += 1) > 0;
    }


    /// <summary>
    /// Normalises escaped string literals by decoding escape sequences.
    /// </summary>
    /// <param name="token">The token to transform.</param>
    /// <param name="result">The decoded string.</param>
    /// <returns><see langword="true"/> when the token represents an escaped string.</returns>
    private static bool StringTransform1(string token, out string result)
    {
        result = null;
        if (!new char[] { '\"', '\'' }.Contains(token[0])) return false;

        //enlève les " du début et de la fin de la chaîne
        token = token[1..^1];
        result = Regex.Replace(token, @"\\(?<char>.)", m =>
            m.Groups["char"].Value switch
            {
                "\\" => "\\",
                "\'" => "\'",
                "\"" => "\"",
                "n" => "\n'",
                "r" => "\r'",
                "t" => "\t'",
                "v" => "\v'",
                "b" => "\b'",
                "f" => "\f'",
                "a" => "\a'",
                _ => throw new ParseUnknownException("\\" + m.Groups["char"].Value, 0),
            }
        );
        return true;
    }

    /// <summary>
    /// Normalises verbatim string literals by collapsing doubled quotes.
    /// </summary>
    /// <param name="token">The token to transform.</param>
    /// <param name="result">The decoded string.</param>
    /// <returns><see langword="true"/> when the token represents a verbatim string.</returns>
    private static bool StringTransform2(string token, out string result)
    {
        result = null;
        if (!token.StartsWith("@\"")) return false;
        token = token[2..^1];
        result = token.Replace("\"\"", "\"");
        return true;
    }

    /// <summary>
    /// Normalises triple-quoted strings by trimming the surrounding quotes.
    /// </summary>
    /// <param name="token">The token to transform.</param>
    /// <param name="result">The decoded string.</param>
    /// <returns><see langword="true"/> when the token represents a triple-quoted string.</returns>
    private static bool StringTransform3(string token, out string result)
    {
        result = null;
        if (!token.StartsWith("\"\"\"")) return false;
        result = token[token.Length..^token.Length];
        return true;
    }

    /// <summary>
    /// Normalises interpolated strings by delegating to the escaped string transformer.
    /// </summary>
    /// <param name="token">The token to transform.</param>
    /// <param name="result">The decoded string.</param>
    /// <returns><see langword="true"/> when the token represents an interpolated escaped string.</returns>
    private static bool StringTransformInterpolated1(string token, out string result)
    {
        result = null;
        if (!token.StartsWith("$\"") || token.StartsWith("$@")) return false;
        return StringTransform1(token[1..], out result);
    }

    /// <summary>
    /// Normalises verbatim interpolated strings while preserving their content ordering.
    /// </summary>
    /// <param name="token">The token to transform.</param>
    /// <param name="result">The decoded string.</param>
    /// <returns><see langword="true"/> when the token represents a verbatim interpolated string.</returns>
    private static bool StringTransformInterpolated2(string token, out string result)
    {
        result = null;
        if (token.StartsWith("$@\"") || token.StartsWith("@$\""))
        {
            var t = token.Remove(token.IndexOf('$'), 1);
            return StringTransform2(t, out result);
        }
        return false;
    }

    /// <summary>
    /// Normalises triple-quoted interpolated strings by removing the interpolation prefix.
    /// </summary>
    /// <param name="token">The token to transform.</param>
    /// <param name="result">The decoded string.</param>
    /// <returns><see langword="true"/> when the token represents a triple-quoted interpolated string.</returns>
    private static bool StringTransformInterpolated3(string token, out string result)
    {
        result = null;
        if (!token.StartsWith("$\"\"\"")) return false;
        return StringTransform3(token[1..], out result);
    }
}
