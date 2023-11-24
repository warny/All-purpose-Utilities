using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Utils.Expressions.ExpressionBuilders;
using static Utils.Expressions.ExpressionBuilders.NewBuilder;

namespace Utils.Expressions.Builders;

public class CStyleBuilder : IBuilder
{
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



    public char InstructionSeparator { get; } = ';';
    public char ListSeparator { get; } = ',';

    public char[] SpaceSymbols { get; } = [' ', '\t', '\r', '\n'];
    public string[] AdditionalSymbols { get; } = ["=>"];

    public IEnumerable<TryReadToken> TokenReaders { get; } =
    [
        TryReadName,
        TryReadNumber,
        TryReadComment,
        TryReadChar,
        TryReadString1,
        TryReadString2,
        TryReadString3,
    ];

    public IEnumerable<StringTransformer> StringTransformers { get; } =
    [
        StringTransform1,
        StringTransform2,
        StringTransform3,
    ];

    public IStartExpressionBuilder NumberBuilder { get; } = new NumberConstantBuilder();

    public IReadOnlyDictionary<string, int> IntegerPrefixes { get; } = new Dictionary<string, int>()
    {
        { "0x", 16 },
        { "0b", 2 },
        { "0o", 8 },
    };

    public IReadOnlyDictionary<string, IStartExpressionBuilder> StartExpressionBuilders { get; } = new Dictionary<string, IStartExpressionBuilder>()
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
        { "break", new BreakBuilder() },
        { "continue", new ContinueBuilder() },

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
    public IStartExpressionBuilder FallbackUnaryBuilder { get; } = new DefaultUnaryBuilder();

    public IReadOnlyDictionary<string, IFollowUpExpressionBuilder> FollowUpExpressionBuilder { get; } = new Dictionary<string, IFollowUpExpressionBuilder>()
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

    public IFollowUpExpressionBuilder FallbackBinaryOrTernaryBuilder { get; } = new ThrowParseException();


    /// <summary>
    /// Read a name from content at the specified index
    /// </summary>
    /// <param name="content">Code content</param>
    /// <param name="index">Extraction start index</param>
    /// <param name="length">Returned length</param>
    /// <returns><see cref="true"/> if a name has been read otherwise <see cref="false"/></returns>
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
    /// <returns><see cref="true"/> if a number has been read otherwise <see cref="false"/></returns>
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

        var nextChar = content[index + length + 1];
        if ((new char[] { 'M', 'D', 'F', 'L', 'X' }).Contains(char.ToUpperInvariant(nextChar))) length++;

        return true;
    }

    /// <summary>
    /// Read a comment from content at the specified index
    /// </summary>
    /// <param name="content">Code content</param>
    /// <param name="index">Extraction start index</param>
    /// <param name="length">Returned length</param>
    /// <returns><see cref="true"/> if a comment has been read otherwise <see cref="false"/></returns>
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

    private static bool TryReadString1(string content, int index, out int length)
    {
        length = 0;
        char c = content[index];
        if (c != '\"') return false;
        if (content.Substring(index, 3) == "\"\"\"") return false;
        for (length = 1; length + index < content.Length; length++)
        {
            c = content[length + index];
            switch (c)
            {
                case '\\':
                    length++;
                    break;
                case '\"':
                    length++;
                    return true;
            }
        }
        throw new ParseNoEndException("\'", index);
    }

    private static bool TryReadString2(string content, int index, out int length)
    {
        length = 0;
        if (content.Length < index + 2) return false;

        var start = content.Substring(index, 2);
        if (start != "@\"") return false;

        for (length = 2; length + index < content.Length; length++)
        {
            char c = content[length + index];
            switch (c)
            {
                case '\"':
                    length++;
                    if (content[length + index] == '\"') break;
                    return true;
            }
        }
        throw new ParseNoEndException("\'", index);
    }

    private static bool TryReadString3(string content, int index, out int length)
    {
        length = 0;
        if (content.Length < index + 6) return false;

        var start = content.Substring(index, 3);
        if (start != "\"\"\"") return false;

        for (int i = 0; content[i] == '\"'; i++)
        {
            length++;
        }
        string prefix = content.Substring(index, length);

        for (length = prefix.Length; length + index < content.Length; length++)
        {
            char c = content[length + index];
            for (int i = 0; i < prefix.Length; i++)
            {
                if (prefix[i] != content[length + index + 1]) break;
            }
            length += prefix.Length;
            return true;
        }
        throw new ParseNoEndException(prefix, index);
    }


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

    private static bool StringTransform2(string token, out string result)
    {
        result = null;
        if (!token.StartsWith("@\"") || token.StartsWith("\"\"\"")) return false;
        token = token[1..^1];
        result = token.Replace("\"\"", "\"");
        return true;
    }

    private static bool StringTransform3(string token, out string result)
    {
        result = null;
        if (!token.StartsWith("\"\"\"")) return false;
        result = token[token.Length..^token.Length];
        return true;
    }
}
