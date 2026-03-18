using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Centralizes SQL tokenization and segment construction helpers used by the parser runtime.
/// </summary>
internal static class SqlParsingInfrastructure
{
    /// <summary>
    /// Builds a segment from a flat token list.
    /// </summary>
    /// <param name="segmentName">Logical name of the segment.</param>
    /// <param name="tokens">Tokens to wrap in the segment.</param>
    /// <param name="syntaxOptions">Syntax options associated with the segment.</param>
    /// <returns>The built <see cref="SqlSegment"/>.</returns>
    public static SqlSegment BuildSegment(string segmentName, List<SqlToken> tokens, SqlSyntaxOptions syntaxOptions)
    {
        return new SqlSegment(segmentName, BuildSegmentParts(tokens, syntaxOptions), syntaxOptions);
    }

    /// <summary>
    /// Builds the segment parts from a token list, recursively parsing nested statements as subqueries.
    /// </summary>
    /// <param name="tokens">Tokens that belong to the segment.</param>
    /// <param name="syntaxOptions">Syntax options used while parsing subqueries.</param>
    /// <returns>The parsed segment parts.</returns>
    public static IReadOnlyList<ISqlSegmentPart> BuildSegmentParts(List<SqlToken> tokens, SqlSyntaxOptions syntaxOptions)
    {
        var parts = new List<ISqlSegmentPart>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Text == "(")
            {
                int closing = FindMatchingParenthesis(tokens, i);
                if (closing > i + 1)
                {
                    var innerTokens = tokens.GetRange(i + 1, closing - i - 1);
                    if (LooksLikeStatement(innerTokens))
                    {
                        var statement = new UtilsParserSqlQueryParser(syntaxOptions).ParseTokens(innerTokens);
                        parts.Add(new SqlSubqueryPart(statement));
                        i = closing;
                        continue;
                    }
                }
            }

            parts.Add(new SqlTokenPart(token.Text));
        }

        return parts;
    }

    /// <summary>
    /// Determines whether tokens likely represent a SQL statement.
    /// </summary>
    /// <param name="tokens">Tokens to inspect.</param>
    /// <returns><c>true</c> when the token stream looks like a statement; otherwise, <c>false</c>.</returns>
    public static bool LooksLikeStatement(List<SqlToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return false;
        }

        if (string.Equals(tokens[0].Normalized, "WITH", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return tokens[0].Normalized is "SELECT" or "INSERT" or "UPDATE" or "DELETE";
    }

    /// <summary>
    /// Finds the matching closing parenthesis for the opening parenthesis at the specified index.
    /// </summary>
    /// <param name="tokens">Token stream to inspect.</param>
    /// <param name="openingIndex">Index of the opening parenthesis.</param>
    /// <returns>The matching closing parenthesis index.</returns>
    public static int FindMatchingParenthesis(List<SqlToken> tokens, int openingIndex)
    {
        int depth = 0;
        for (int index = openingIndex; index < tokens.Count; index++)
        {
            if (tokens[index].Text == "(")
            {
                depth++;
            }
            else if (tokens[index].Text == ")")
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        throw new SqlParseException("Unbalanced parenthesis in SQL segment.");
    }
}

/// <summary>
/// Represents a token emitted by the SQL tokenizer.
/// </summary>
internal sealed class SqlToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlToken"/> class.
    /// </summary>
    /// <param name="text">Original token text.</param>
    /// <param name="normalized">Normalized token text used for comparisons.</param>
    /// <param name="isIdentifier">Indicates whether the token represents an identifier.</param>
    /// <param name="isKeyword">Indicates whether the token represents a keyword.</param>
    public SqlToken(string text, string normalized, bool isIdentifier, bool isKeyword)
    {
        Text = text;
        Normalized = normalized;
        IsIdentifier = isIdentifier;
        IsKeyword = isKeyword;
    }

    /// <summary>
    /// Gets the original token text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the normalized token text.
    /// </summary>
    public string Normalized { get; }

    /// <summary>
    /// Gets a value indicating whether the token is an identifier.
    /// </summary>
    public bool IsIdentifier { get; }

    /// <summary>
    /// Gets a value indicating whether the token is a keyword.
    /// </summary>
    public bool IsKeyword { get; }
}

/// <summary>
/// Tokenizes SQL text into the lightweight <see cref="SqlToken"/> model.
/// </summary>
internal sealed class SqlTokenizer
{
    private static readonly FrozenSet<string> Keywords = new HashSet<string>
    {
        "SELECT",
        "FROM",
        "WHERE",
        "GROUP",
        "BY",
        "HAVING",
        "ORDER",
        "LIMIT",
        "OFFSET",
        "UNION",
        "ALL",
        "DISTINCT",
        "CASE",
        "INSERT",
        "INTO",
        "VALUES",
        "RETURNING",
        "WHEN",
        "THEN",
        "ELSE",
        "END",
        "OUTPUT",
        "UPDATE",
        "SET",
        "DELETE",
        "WITH",
        "RECURSIVE",
        "AS",
        "IF",
        "ON",
        "JOIN",
        "INNER",
        "LEFT",
        "RIGHT",
        "FULL",
        "OUTER",
        "USING",
        "INTERSECT",
        "EXCEPT",
        "AND",
        "OR",
        "NOT",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly string sql;
    private readonly SqlSyntaxOptions syntaxOptions;
    private int index;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTokenizer"/> class.
    /// </summary>
    /// <param name="sql">SQL text to tokenize.</param>
    /// <param name="syntaxOptions">Syntax options controlling identifiers and parameters.</param>
    public SqlTokenizer(string sql, SqlSyntaxOptions syntaxOptions)
    {
        this.sql = sql ?? throw new ArgumentNullException(nameof(sql));
        this.syntaxOptions = syntaxOptions ?? throw new ArgumentNullException(nameof(syntaxOptions));
    }

    /// <summary>
    /// Tokenizes the configured SQL text.
    /// </summary>
    /// <returns>The produced SQL tokens.</returns>
    public List<SqlToken> Tokenize()
    {
        var tokens = new List<SqlToken>();
        while (!IsAtEnd)
        {
            char current = Peek();
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '-' && PeekAhead(1) == '-')
            {
                SkipLineComment();
                continue;
            }

            if (current == '/' && PeekAhead(1) == '*')
            {
                SkipBlockComment();
                continue;
            }

            if (current == '\'' || current == '"')
            {
                tokens.Add(ReadString());
                continue;
            }

            if (current == '[')
            {
                tokens.Add(ReadBracketIdentifier());
                continue;
            }

            if (IsIdentifierStart(current))
            {
                tokens.Add(ReadIdentifierOrKeyword());
                continue;
            }

            if (char.IsDigit(current))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            if (IsOperatorStart(current))
            {
                tokens.Add(ReadOperator());
                continue;
            }

            tokens.Add(ReadSymbol());
        }

        return tokens;
    }

    private SqlToken ReadIdentifierOrKeyword()
    {
        int start = index;
        index++;
        while (!IsAtEnd && IsIdentifierPart(Peek()))
        {
            index++;
        }

        string text = sql.Substring(start, index - start);
        bool isKeyword = Keywords.Contains(text);
        string normalized = isKeyword ? text.ToUpperInvariant() : text;
        return new SqlToken(text, normalized, !isKeyword, isKeyword);
    }

    private SqlToken ReadNumber()
    {
        int start = index;
        index++;
        while (!IsAtEnd && (char.IsDigit(Peek()) || Peek() == '.'))
        {
            index++;
        }

        string text = sql.Substring(start, index - start);
        return new SqlToken(text, text, false, false);
    }

    private SqlToken ReadString()
    {
        char delimiter = Read();
        var buffer = new List<char> { delimiter };
        while (!IsAtEnd)
        {
            char current = Read();
            buffer.Add(current);
            if (current == delimiter)
            {
                if (!IsAtEnd && Peek() == delimiter)
                {
                    buffer.Add(Read());
                    continue;
                }

                break;
            }
        }

        string text = new string(buffer.ToArray());
        return new SqlToken(text, text, false, false);
    }

    private SqlToken ReadBracketIdentifier()
    {
        var buffer = new List<char> { Read() };
        while (!IsAtEnd)
        {
            char current = Read();
            buffer.Add(current);
            if (current == ']')
            {
                break;
            }
        }

        string text = new string(buffer.ToArray());
        return new SqlToken(text, text, true, false);
    }

    private SqlToken ReadOperator()
    {
        int start = index;
        index++;
        if (!IsAtEnd)
        {
            string twoChars = sql.Substring(start, Math.Min(2, sql.Length - start));
            if (twoChars is ">=" or "<=" or "<>" or "!=" or "||")
            {
                index = start + 2;
            }
        }

        string text = sql.Substring(start, index - start);
        return new SqlToken(text, text, false, false);
    }

    private SqlToken ReadSymbol()
    {
        char symbol = Read();
        string text = symbol.ToString();
        return new SqlToken(text, text, false, false);
    }

    private bool IsIdentifierStart(char current)
    {
        return char.IsLetter(current)
            || current == '_'
            || current == '#'
            || syntaxOptions.IsIdentifierPrefix(current);
    }

    private static bool IsIdentifierPart(char current)
    {
        return char.IsLetterOrDigit(current) || current is '_' or '$' or '#';
    }

    private static bool IsOperatorStart(char current)
    {
        return current is '>' or '<' or '!' or '|';
    }

    private void SkipLineComment()
    {
        while (!IsAtEnd && Read() != '\n')
        {
        }
    }

    private void SkipBlockComment()
    {
        index += 2;
        while (!IsAtEnd)
        {
            if (Peek() == '*' && PeekAhead(1) == '/')
            {
                index += 2;
                return;
            }

            index++;
        }
    }

    private char Read()
    {
        return sql[index++];
    }

    private char Peek()
    {
        return sql[index];
    }

    private char PeekAhead(int offset)
    {
        int target = index + offset;
        return target < sql.Length ? sql[target] : '\0';
    }

    private bool IsAtEnd => index >= sql.Length;
}
