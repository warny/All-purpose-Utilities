using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Utils.Data.Sql;

#nullable enable

internal sealed class SqlParser
{
    private static readonly IReadOnlyDictionary<string, Func<SqlParser, WithClause?, SqlStatement>> StatementParsers =
        new Dictionary<string, Func<SqlParser, WithClause?, SqlStatement>>(StringComparer.OrdinalIgnoreCase)
        {
            { "SELECT", (parser, withClause) => new SelectStatementParser(parser).Parse(withClause) },
            { "INSERT", (parser, withClause) => new InsertStatementParser(parser).Parse(withClause) },
            { "UPDATE", (parser, withClause) => new UpdateStatementParser(parser).Parse(withClause) },
            { "DELETE", (parser, withClause) => new DeleteStatementParser(parser).Parse(withClause) },
        }.ToImmutableDictionary();

    private readonly List<SqlToken> tokens;
    private int position;

    private readonly SqlSyntaxOptions syntaxOptions;

    /// <summary>
    /// Gets the tokens being parsed.
    /// </summary>
    internal List<SqlToken> Tokens => tokens;

    /// <summary>
    /// Gets or sets the current parsing position within <see cref="Tokens"/>.
    /// </summary>
    internal int Position
    {
        get => position;
        set => position = value;
    }

    /// <summary>
    /// Gets the syntax options currently applied to parsing.
    /// </summary>
    internal SqlSyntaxOptions SyntaxOptions => syntaxOptions;

    /// <summary>
    /// Gets a value indicating whether the parser has consumed all tokens.
    /// </summary>
    internal bool IsAtEnd => position >= tokens.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlParser"/> class with pre-tokenized input.
    /// </summary>
    /// <param name="tokens">The tokens representing the SQL statement.</param>
    /// <param name="syntaxOptions">The syntax options guiding token interpretation.</param>
    internal SqlParser(IEnumerable<SqlToken> tokens, SqlSyntaxOptions syntaxOptions)
    {
        this.syntaxOptions = syntaxOptions ?? throw new ArgumentNullException(nameof(syntaxOptions));
        this.tokens = tokens.ToList();
    }

    public static SqlParser Create(string sql, SqlSyntaxOptions? syntaxOptions = null)
    {
        syntaxOptions ??= SqlSyntaxOptions.Default;
        var tokenizer = new SqlTokenizer(sql, syntaxOptions);
        return new SqlParser(tokenizer.Tokenize(), syntaxOptions);
    }

    public SqlStatement ParseStatementWithOptionalCte()
    {
        WithClause? withClause = null;
        if (TryConsumeKeyword("WITH"))
        {
            withClause = ParseWithClause();
        }

        var statement = ParseStatementCore(withClause);
        return statement;
    }

    public void ConsumeOptionalTerminator()
    {
        while (!IsAtEnd && Peek().Text == ";")
        {
            position++;
        }
    }

    public void EnsureEndOfInput()
    {
        if (!IsAtEnd)
        {
            throw new SqlParseException($"Unexpected token '{Peek().Text}' after end of statement.");
        }
    }

    /// <summary>
    /// Parses the next SQL statement using the available statement parsers.
    /// </summary>
    /// <param name="withClause">The optional WITH clause already parsed for the statement.</param>
    /// <returns>The parsed <see cref="SqlStatement"/> instance.</returns>
    /// <exception cref="SqlParseException">Thrown when the input is incomplete or unsupported.</exception>
    private SqlStatement ParseStatementCore(WithClause? withClause)
    {
        if (IsAtEnd)
        {
            throw new SqlParseException("Unexpected end of input while expecting a statement.");
        }

        var next = Peek();
        if (StatementParsers.TryGetValue(next.Normalized, out var parserFunc))
        {
            return parserFunc(this, withClause);
        }

        throw new SqlParseException($"Unsupported statement starting with '{next.Text}'.");
    }

    private WithClause ParseWithClause()
    {
        bool isRecursive = TryConsumeKeyword("RECURSIVE");
        var definitions = new List<CteDefinition>();
        do
        {
            string name = ExpectIdentifier();
            IReadOnlyList<string>? columns = null;
            if (TryConsumeSymbol("("))
            {
                columns = ParseColumnList();
                ExpectSymbol(")");
            }

            ExpectKeyword("AS");
            ExpectSymbol("(");
            var subTokens = ReadTokensUntilMatchingParenthesis();
            var subParser = new SqlParser(subTokens, syntaxOptions);
            var statement = subParser.ParseStatementWithOptionalCte();
            subParser.ConsumeOptionalTerminator();
            subParser.EnsureEndOfInput();
            definitions.Add(new CteDefinition(name, columns, statement));
        }
        while (TryConsumeSymbol(","));

        return new WithClause(isRecursive, definitions);
    }

    private IReadOnlyList<string> ParseColumnList()
    {
        var columns = new List<string>();
        do
        {
            columns.Add(ExpectIdentifier());
        }
        while (TryConsumeSymbol(","));

        return columns;
    }

    /// <summary>
    /// Parses a SELECT statement, reading each clause segment in order.
    /// </summary>
    /// <param name="withClause">The WITH clause associated with the SELECT, if any.</param>
    /// <returns>The parsed <see cref="SqlSelectStatement"/> instance.</returns>
    /// <summary>
    /// Builds a <see cref="SqlSegment"/> from the provided tokens.
    /// </summary>
    /// <param name="name">The logical name of the segment.</param>
    /// <param name="tokens">The tokens that compose the segment.</param>
    /// <returns>A <see cref="SqlSegment"/> representing the parsed section.</returns>
    internal SqlSegment BuildSegment(string name, List<SqlToken> tokens)
    {
        return new SqlSegment(name, BuildSegmentParts(tokens, syntaxOptions), syntaxOptions);
    }

    /// <summary>
    /// Builds the segment parts from the provided token list.
    /// </summary>
    /// <param name="tokens">Tokens that compose the segment.</param>
    /// <returns>The collection of parts representing the segment.</returns>
    internal static IReadOnlyList<ISqlSegmentPart> BuildSegmentParts(List<SqlToken> tokens, SqlSyntaxOptions syntaxOptions)
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
                        var subParser = new SqlParser(innerTokens, syntaxOptions);
                        var subStatement = subParser.ParseStatementWithOptionalCte();
                        subParser.ConsumeOptionalTerminator();
                        subParser.EnsureEndOfInput();
                        parts.Add(new SqlSubqueryPart(subStatement));
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
    /// Determines whether the provided tokens represent a SQL statement.
    /// </summary>
    /// <param name="innerTokens">Tokens located within a parenthesis.</param>
    /// <returns><c>true</c> when the tokens represent a statement, otherwise <c>false</c>.</returns>
    private static bool LooksLikeStatement(List<SqlToken> innerTokens)
    {
        if (innerTokens.Count == 0)
        {
            return false;
        }

        var first = innerTokens[0];
        return first.Normalized is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "WITH";
    }

    /// <summary>
    /// Finds the index of the closing parenthesis that matches the opening parenthesis at the specified index.
    /// </summary>
    /// <param name="list">The token list containing the parenthesis.</param>
    /// <param name="start">Index of the opening parenthesis.</param>
    /// <returns>The index of the closing parenthesis, or -1 when not found.</returns>
    private static int FindMatchingParenthesis(List<SqlToken> list, int start)
    {
        int depth = 0;
        for (int i = start; i < list.Count; i++)
        {
            if (list[i].Text == "(")
            {
                depth++;
            }
            else if (list[i].Text == ")")
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private List<SqlToken> ReadTokensUntilMatchingParenthesis()
    {
        var result = new List<SqlToken>();
        int depth = 1;
        while (!IsAtEnd)
        {
            var token = Read();
            if (token.Text == "(")
            {
                depth++;
            }
            else if (token.Text == ")")
            {
                depth--;
                if (depth == 0)
                {
                    return result;
                }
            }

            result.Add(token);
        }

        throw new SqlParseException("Unterminated parenthesis in WITH clause definition.");
    }

    /// <summary>
    /// Reads tokens until one of the specified clause starts is encountered at depth zero.
    /// </summary>
    /// <param name="terminators">Clause boundaries that end the current section.</param>
    /// <returns>The tokens collected for the current section.</returns>
    internal List<SqlToken> ReadSectionTokens(params ClauseStart[] terminators)
    {
        var tokens = new List<SqlToken>();
        int depth = 0;
        while (!IsAtEnd)
        {
            if (Peek().Text == ";" && depth == 0)
            {
                break;
            }

            if (Peek().Text == "(")
            {
                depth++;
            }
            else if (Peek().Text == ")")
            {
                if (depth == 0)
                {
                    break;
                }

                depth--;
            }

            if (depth == 0 && terminators.Length > 0 && CheckClauseStart(terminators))
            {
                break;
            }

            tokens.Add(Read());
        }

        return tokens;
    }

    /// <summary>
    /// Determines whether the current token marks the start of one of the specified clauses.
    /// </summary>
    /// <param name="terminators">Clause starts that should stop parsing.</param>
    /// <returns><c>true</c> when the current position matches a clause start.</returns>
    internal bool IsClauseStart(params ClauseStart[] terminators)
    {
        return CheckClauseStart(terminators);
    }

    private bool CheckClauseStart(params ClauseStart[] terminators)
    {
        foreach (var terminator in terminators)
        {
            if (terminator == ClauseStart.StatementEnd)
            {
                if (IsAtEnd || Peek().Text == ";")
                {
                    return true;
                }

                continue;
            }

            if (!ClauseStartKeywordRegistry.TryGetClauseKeywords(terminator, out var keywordSequences))
            {
                continue;
            }

            foreach (var keywordSequence in keywordSequences)
            {
                if (CheckKeywordSequence(keywordSequence))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the index of the next keyword, ignoring nested parentheses.
    /// </summary>
    /// <param name="keyword">The keyword to look for.</param>
    /// <returns>The token index if found; otherwise, -1.</returns>
    internal int FindClauseIndex(string keyword)
    {
        int depth = 0;
        for (int i = position; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Text == "(")
            {
                depth++;
            }
            else if (token.Text == ")")
            {
                if (depth > 0)
                {
                    depth--;
                }
            }

            if (depth == 0 && token.Normalized == keyword)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Consumes the specified keyword or throws an exception when the stream does not match.
    /// </summary>
    /// <param name="keyword">The keyword to consume.</param>
    /// <returns>The consumed <see cref="SqlToken"/>.</returns>
    /// <exception cref="SqlParseException">Thrown when the expected keyword is missing.</exception>
    internal SqlToken ExpectKeyword(string keyword)
    {
        if (IsAtEnd || !CheckKeyword(keyword))
        {
            throw new SqlParseException($"Expected keyword '{keyword}'.");
        }

        return Read();
    }

    /// <summary>
    /// Consumes an identifier at the current position or throws when the token is not an identifier.
    /// </summary>
    /// <returns>The identifier text.</returns>
    /// <exception cref="SqlParseException">Thrown when the token is not an identifier.</exception>
    private string ExpectIdentifier()
    {
        if (IsAtEnd)
        {
            throw new SqlParseException("Expected identifier but reached end of statement.");
        }

        var token = Peek();
        if (!token.IsIdentifier)
        {
            throw new SqlParseException($"Expected identifier but found '{token.Text}'.");
        }

        position++;
        return token.Text;
    }

    /// <summary>
    /// Consumes the expected symbol or throws when a different token is found.
    /// </summary>
    /// <param name="symbol">The symbol to consume.</param>
    /// <returns>The consumed token.</returns>
    /// <exception cref="SqlParseException">Thrown when the symbol does not match.</exception>
    private SqlToken ExpectSymbol(string symbol)
    {
        if (IsAtEnd || Peek().Text != symbol)
        {
            throw new SqlParseException($"Expected symbol '{symbol}'.");
        }

        return Read();
    }

    /// <summary>
    /// Attempts to consume a clause keyword, including composite keywords such as "GROUP BY".
    /// </summary>
    /// <param name="keyword">The keyword text to match.</param>
    /// <param name="consumedTokens">The tokens consumed when the keyword is matched.</param>
    /// <returns><c>true</c> when the keyword is consumed; otherwise, <c>false</c>.</returns>
    internal bool TryConsumeSegmentKeyword(string keyword, out List<SqlToken> consumedTokens)
    {
        consumedTokens = new List<SqlToken>();
        if (keyword == ";")
        {
            if (!IsAtEnd && Peek().Text == ";")
            {
                consumedTokens.Add(Read());
                return true;
            }

            return false;
        }

        var parts = keyword.Split(' ');
        if (parts.Length == 1)
        {
            if (CheckKeyword(parts[0]))
            {
                consumedTokens.Add(Read());
                return true;
            }

            return false;
        }

        if (parts.Length == 2 && CheckKeywordSequence(parts[0], parts[1]))
        {
            consumedTokens.Add(Read());
            consumedTokens.Add(Read());
            return true;
        }

        return false;
    }

    internal bool TryConsumeKeyword(string keyword)
    {
        if (CheckKeyword(keyword))
        {
            position++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to consume the specified symbol at the current position.
    /// </summary>
    /// <param name="symbol">The symbol expected.</param>
    /// <returns><c>true</c> when the symbol was consumed; otherwise, <c>false</c>.</returns>
    private bool TryConsumeSymbol(string symbol)
    {
        if (!IsAtEnd && Peek().Text == symbol)
        {
            position++;
            return true;
        }

        return false;
    }

    internal bool CheckKeyword(string keyword)
    {
        return !IsAtEnd && Peek().Normalized == keyword;
    }

    internal bool CheckKeywordSequence(string first, string second)
    {
        return CheckKeywordSequence(new[] { first, second });
    }

    /// <summary>
    /// Checks whether the upcoming tokens match the provided keyword sequence.
    /// </summary>
    /// <param name="keywordSequence">The ordered keywords to verify.</param>
    /// <returns><c>true</c> when the upcoming tokens match the full sequence; otherwise, <c>false</c>.</returns>
    internal bool CheckKeywordSequence(IReadOnlyList<string> keywordSequence)
    {
        if (keywordSequence.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < keywordSequence.Count; i++)
        {
            var token = PeekOptional(i);
            if (token is null || token.Normalized != keywordSequence[i])
            {
                return false;
            }
        }

        return true;
    }

    internal SqlToken Peek(int offset = 0)
    {
        return tokens[position + offset];
    }

    private SqlToken? PeekOptional(int offset)
    {
        int index = position + offset;
        if (index >= tokens.Count)
        {
            return null;
        }

        return tokens[index];
    }

    internal SqlToken Read()
    {
        return tokens[position++];
    }
}

internal enum ClauseStart
{
    Into,
    From,
    Where,
    GroupBy,
    Having,
    OrderBy,
    Limit,
    Offset,
    Output,
    Values,
    Select,
    Returning,
    Using,
    SetOperator,
    StatementEnd,
}

internal sealed class SqlToken
{
    public SqlToken(string text, string normalized, bool isIdentifier, bool isKeyword)
    {
        Text = text;
        Normalized = normalized;
        IsIdentifier = isIdentifier;
        IsKeyword = isKeyword;
    }

    public string Text { get; }

    public string Normalized { get; }

    public bool IsIdentifier { get; }

    public bool IsKeyword { get; }
}

internal sealed class SqlTokenizer
{
    private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
    };

    private readonly string sql;
    private readonly SqlSyntaxOptions syntaxOptions;
    private int index;

    public SqlTokenizer(string sql, SqlSyntaxOptions syntaxOptions)
    {
        this.sql = sql ?? throw new ArgumentNullException(nameof(sql));
        this.syntaxOptions = syntaxOptions ?? throw new ArgumentNullException(nameof(syntaxOptions));
    }

    public List<SqlToken> Tokenize()
    {
        var tokens = new List<SqlToken>();
        while (!IsAtEnd)
        {
            char c = Peek();
            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }

            if (c == '-' && PeekAhead(1) == '-')
            {
                SkipLineComment();
                continue;
            }

            if (c == '/' && PeekAhead(1) == '*')
            {
                SkipBlockComment();
                continue;
            }

            if (c == '\'' || c == '"')
            {
                tokens.Add(ReadString());
                continue;
            }

            if (c == '[')
            {
                tokens.Add(ReadBracketIdentifier());
                continue;
            }

            if (IsIdentifierStart(c))
            {
                tokens.Add(ReadIdentifierOrKeyword());
                continue;
            }

            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            if (IsOperatorStart(c))
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
        bool isIdentifier = !isKeyword;
        return new SqlToken(text, normalized, isIdentifier, isKeyword);
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
        var builder = new StringBuilder();
        builder.Append(delimiter);
        while (!IsAtEnd)
        {
            char c = Read();
            builder.Append(c);
            if (c == delimiter)
            {
                if (!IsAtEnd && Peek() == delimiter)
                {
                    builder.Append(Read());
                    continue;
                }

                break;
            }
        }

        return new SqlToken(builder.ToString(), builder.ToString(), false, false);
    }

    private SqlToken ReadBracketIdentifier()
    {
        var builder = new StringBuilder();
        builder.Append(Read());
        while (!IsAtEnd)
        {
            char c = Read();
            builder.Append(c);
            if (c == ']')
            {
                break;
            }
        }

        string text = builder.ToString();
        return new SqlToken(text, text, true, false);
    }

    private SqlToken ReadOperator()
    {
        int start = index;
        index++;
        if (!IsAtEnd)
        {
            string two = sql.Substring(start, Math.Min(2, sql.Length - start));
            if (two is ">=" or "<=" or "<>" or "!=")
            {
                index = start + 2;
                return new SqlToken(two, two, false, false);
            }
        }

        string text = sql.Substring(start, 1);
        return new SqlToken(text, text, false, false);
    }

    private SqlToken ReadSymbol()
    {
        char c = Read();
        return new SqlToken(c.ToString(CultureInfo.InvariantCulture), c.ToString(CultureInfo.InvariantCulture), false, false);
    }

    private void SkipLineComment()
    {
        index += 2;
        while (!IsAtEnd && Peek() != '\n')
        {
            index++;
        }

        if (!IsAtEnd)
        {
            index++;
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
                break;
            }

            index++;
        }
    }

    private bool IsOperatorStart(char c) => c switch
    {
        '>' or '<' or '=' or '!' => true,
        _ => false,
    };

    private bool IsIdentifierStart(char c)
    {
        return char.IsLetter(c) || c == '_' || c == '$' || syntaxOptions.IsIdentifierPrefix(c);
    }

    private bool IsIdentifierPart(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '$' || syntaxOptions.IsIdentifierPrefix(c);
    }

    private char Peek() => sql[index];

    private char PeekAhead(int offset) => index + offset < sql.Length ? sql[index + offset] : '\0';

    private char Read() => sql[index++];

    private bool IsAtEnd => index >= sql.Length;
}
