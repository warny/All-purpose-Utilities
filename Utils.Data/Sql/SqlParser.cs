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
            { "SELECT", (parser, withClause) => parser.ParseSelect(withClause) },
            { "INSERT", (parser, withClause) => parser.ParseInsert(withClause) },
            { "UPDATE", (parser, withClause) => parser.ParseUpdate(withClause) },
            { "DELETE", (parser, withClause) => parser.ParseDelete(withClause) },
        }.ToImmutableDictionary();

    private static readonly IReadOnlyList<(string Keyword, ClauseStart Clause, bool IncludeKeyword)> Segments =
        new List<(string, ClauseStart, bool)>
        {
            ("FROM", ClauseStart.From, false),
            ("WHERE", ClauseStart.Where, false),
            ("GROUP BY", ClauseStart.GroupBy, false),
            ("HAVING", ClauseStart.Having, false),
            ("ORDER BY", ClauseStart.OrderBy, false),
            ("LIMIT", ClauseStart.Limit, false),
            ("OFFSET", ClauseStart.Offset, false),
            ("RETURNING", ClauseStart.Returning, false),
            ("USING", ClauseStart.Using, false),
            ("UNION", ClauseStart.SetOperator, true),
            ("EXCEPT", ClauseStart.SetOperator, true),
            ("INTERSECT", ClauseStart.SetOperator, true),
        }.ToImmutableList();

    private readonly List<SqlToken> tokens;
    private int position;

    private readonly SqlSyntaxOptions syntaxOptions;

    private SqlParser(IEnumerable<SqlToken> tokens, SqlSyntaxOptions syntaxOptions)
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
    private SqlSelectStatement ParseSelect(WithClause? withClause)
    {
        ExpectKeyword("SELECT");
        bool isDistinct = TryConsumeKeyword("DISTINCT");
        var selectTokens = ReadSectionTokens(ClauseStart.From, ClauseStart.Where, ClauseStart.GroupBy, ClauseStart.Having, ClauseStart.OrderBy, ClauseStart.Limit, ClauseStart.Offset, ClauseStart.Returning, ClauseStart.SetOperator, ClauseStart.StatementEnd);
        var selectSegment = BuildSegment("Select", selectTokens);

        ClauseStart[] clauses =
        [
            ClauseStart.From,
            ClauseStart.Where,
            ClauseStart.GroupBy,
            ClauseStart.Having,
            ClauseStart.OrderBy,
            ClauseStart.Limit,
            ClauseStart.Offset,
            ClauseStart.Returning,
            ClauseStart.Using,
            ClauseStart.SetOperator,
            ClauseStart.StatementEnd,
        ];

        Dictionary<ClauseStart, SqlSegment?> segments = new Dictionary<ClauseStart, SqlSegment?>();

        foreach (var segment in Segments)
        {
            var tokensAfter = clauses.SkipWhile(c => c != segment.Clause).Skip(1).ToArray();
            if (TryConsumeSegmentKeyword(segment.Keyword, out var consumedTokens))
            {
                var segmentTokens = ReadSectionTokens(tokensAfter);
                if (segment.IncludeKeyword)
                {
                    segmentTokens.InsertRange(0, consumedTokens);
                }

                segments[segment.Clause] = BuildSegment(segment.Clause.ToString(), segmentTokens);
            }
            else if (!segments.ContainsKey(segment.Clause))
            {
                segments[segment.Clause] = null;
            }
        }

        return new SqlSelectStatement(
            selectSegment,
            segments[ClauseStart.From],
            segments[ClauseStart.Where],
            segments[ClauseStart.GroupBy],
            segments[ClauseStart.Having],
            segments[ClauseStart.OrderBy],
            segments[ClauseStart.Limit],
            segments[ClauseStart.Offset],
            segments[ClauseStart.SetOperator],
            withClause,
            isDistinct);
    }

    private SqlInsertStatement ParseInsert(WithClause? withClause)
    {
        ExpectKeyword("INSERT");
        if (TryConsumeKeyword("INTO") == false)
        {
            ExpectKeyword("INTO");
        }

        var targetTokens = new List<SqlToken>();
        int returningIndex = FindClauseIndex("RETURNING");
        while (!IsAtEnd)
        {
            if (CheckKeyword("VALUES") || CheckKeyword("SELECT") || CheckKeyword("WITH") || CheckKeyword("RETURNING") || CheckKeyword("OUTPUT"))
            {
                break;
            }

            if (Peek().Text == ";")
            {
                break;
            }

            targetTokens.Add(Read());
        }

        var targetSegment = BuildSegment("Target", targetTokens);
        SqlSegment? valuesSegment = null;
        SqlStatement? sourceQuery = null;
        SqlSegment? outputSegment = null;
        SqlSegment? returningSegment = null;

        if (TryConsumeKeyword("OUTPUT"))
        {
            var outputTokens = ReadSectionTokens(ClauseStart.Values, ClauseStart.Select, ClauseStart.Returning, ClauseStart.StatementEnd);
            outputSegment = BuildSegment("Output", outputTokens);
        }

        if (CheckKeyword("VALUES"))
        {
            ExpectKeyword("VALUES");
            var valuesTokens = new List<SqlToken>();
            while (!IsAtEnd && (returningIndex < 0 || position < returningIndex))
            {
                if (Peek().Text == ";")
                {
                    break;
                }

                valuesTokens.Add(Read());
            }

            valuesSegment = BuildSegment("Values", valuesTokens);
        }
        else if (CheckKeyword("SELECT") || CheckKeyword("WITH"))
        {
            int end = returningIndex >= 0 ? returningIndex : tokens.Count;
            var sourceTokens = tokens.GetRange(position, end - position);
            var subParser = new SqlParser(sourceTokens, syntaxOptions);
            sourceQuery = subParser.ParseStatementWithOptionalCte();
            subParser.ConsumeOptionalTerminator();
            subParser.EnsureEndOfInput();
            position = end;
        }

        if (TryConsumeKeyword("RETURNING"))
        {
            var returningTokens = ReadSectionTokens(ClauseStart.StatementEnd);
            returningSegment = BuildSegment("Returning", returningTokens);
        }

        return new SqlInsertStatement(targetSegment, valuesSegment, sourceQuery, outputSegment, returningSegment, withClause);
    }

    private SqlUpdateStatement ParseUpdate(WithClause? withClause)
    {
        ExpectKeyword("UPDATE");
        var targetTokens = new List<SqlToken>();
        while (!IsAtEnd && !CheckKeyword("SET") && Peek().Text != ";")
        {
            targetTokens.Add(Read());
        }

        var targetSegment = BuildSegment("Target", targetTokens);
        ExpectKeyword("SET");
        var setTokens = ReadSectionTokens(ClauseStart.Output, ClauseStart.From, ClauseStart.Where, ClauseStart.Returning, ClauseStart.StatementEnd);
        var setSegment = BuildSegment("Set", setTokens);

        SqlSegment? fromSegment = null;
        SqlSegment? whereSegment = null;
        SqlSegment? outputSegment = null;
        SqlSegment? returningSegment = null;

        if (TryConsumeKeyword("OUTPUT"))
        {
            var outputTokens = ReadSectionTokens(ClauseStart.From, ClauseStart.Where, ClauseStart.Returning, ClauseStart.StatementEnd);
            outputSegment = BuildSegment("Output", outputTokens);
        }

        if (TryConsumeKeyword("FROM"))
        {
            var fromTokens = ReadSectionTokens(ClauseStart.Where, ClauseStart.Returning, ClauseStart.StatementEnd);
            fromSegment = BuildSegment("From", fromTokens);
        }

        if (TryConsumeKeyword("WHERE"))
        {
            var whereTokens = ReadSectionTokens(ClauseStart.Returning, ClauseStart.StatementEnd);
            whereSegment = BuildSegment("Where", whereTokens);
        }

        if (TryConsumeKeyword("RETURNING"))
        {
            var returningTokens = ReadSectionTokens(ClauseStart.StatementEnd);
            returningSegment = BuildSegment("Returning", returningTokens);
        }

        return new SqlUpdateStatement(targetSegment, setSegment, fromSegment, whereSegment, outputSegment, returningSegment, withClause);
    }

    private SqlDeleteStatement ParseDelete(WithClause? withClause)
    {
        ExpectKeyword("DELETE");
        SqlSegment? targetSegment = null;
        if (!CheckKeyword("FROM"))
        {
            var targetTokens = new List<SqlToken>();
            while (!IsAtEnd && !CheckKeyword("FROM") && Peek().Text != ";")
            {
                targetTokens.Add(Read());
            }

            targetSegment = BuildSegment("Target", targetTokens);
        }

        ExpectKeyword("FROM");
        var fromTokens = ReadSectionTokens(ClauseStart.Output, ClauseStart.Using, ClauseStart.Where, ClauseStart.Returning, ClauseStart.StatementEnd);
        var fromSegment = BuildSegment("From", fromTokens);

        SqlSegment? usingSegment = null;
        SqlSegment? whereSegment = null;
        SqlSegment? outputSegment = null;
        SqlSegment? returningSegment = null;

        if (TryConsumeKeyword("OUTPUT"))
        {
            var outputTokens = ReadSectionTokens(ClauseStart.Using, ClauseStart.Where, ClauseStart.Returning, ClauseStart.StatementEnd);
            outputSegment = BuildSegment("Output", outputTokens);
        }

        if (TryConsumeKeyword("USING"))
        {
            var usingTokens = ReadSectionTokens(ClauseStart.Where, ClauseStart.Returning, ClauseStart.StatementEnd);
            usingSegment = BuildSegment("Using", usingTokens);
        }

        if (TryConsumeKeyword("WHERE"))
        {
            var whereTokens = ReadSectionTokens(ClauseStart.Returning, ClauseStart.StatementEnd);
            whereSegment = BuildSegment("Where", whereTokens);
        }

        if (TryConsumeKeyword("RETURNING"))
        {
            var returningTokens = ReadSectionTokens(ClauseStart.StatementEnd);
            returningSegment = BuildSegment("Returning", returningTokens);
        }

        return new SqlDeleteStatement(targetSegment, fromSegment, usingSegment, whereSegment, outputSegment, returningSegment, withClause);
    }

    private SqlSegment BuildSegment(string name, List<SqlToken> tokens)
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

    private List<SqlToken> ReadSectionTokens(params ClauseStart[] terminators)
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
            }
            else if (terminator == ClauseStart.Where && CheckKeyword("WHERE"))
            {
                return true;
            }
            else if (terminator == ClauseStart.From && CheckKeyword("FROM"))
            {
                return true;
            }
            else if (terminator == ClauseStart.GroupBy && CheckKeywordSequence("GROUP", "BY"))
            {
                return true;
            }
            else if (terminator == ClauseStart.Having && CheckKeyword("HAVING"))
            {
                return true;
            }
            else if (terminator == ClauseStart.OrderBy && CheckKeywordSequence("ORDER", "BY"))
            {
                return true;
            }
            else if (terminator == ClauseStart.Limit && CheckKeyword("LIMIT"))
            {
                return true;
            }
            else if (terminator == ClauseStart.Offset && CheckKeyword("OFFSET"))
            {
                return true;
            }
            else if (terminator == ClauseStart.Output && CheckKeyword("OUTPUT"))
            {
                return true;
            }
            else if (terminator == ClauseStart.Returning && CheckKeyword("RETURNING"))
            {
                return true;
            }
            else if (terminator == ClauseStart.Values && CheckKeyword("VALUES"))
            {
                return true;
            }
            else if (terminator == ClauseStart.Select && (CheckKeyword("SELECT") || CheckKeyword("WITH")))
            {
                return true;
            }
            else if (terminator == ClauseStart.Using && CheckKeyword("USING"))
            {
                return true;
            }
            else if (terminator == ClauseStart.SetOperator && (CheckKeyword("UNION") || CheckKeyword("EXCEPT") || CheckKeyword("INTERSECT")))
            {
                return true;
            }
        }

        return false;
    }

    private int FindClauseIndex(string keyword)
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

    private SqlToken ExpectKeyword(string keyword)
    {
        if (IsAtEnd || !CheckKeyword(keyword))
        {
            throw new SqlParseException($"Expected keyword '{keyword}'.");
        }

        return Read();
    }

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
    private bool TryConsumeSegmentKeyword(string keyword, out List<SqlToken> consumedTokens)
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

    private bool TryConsumeKeyword(string keyword)
    {
        if (CheckKeyword(keyword))
        {
            position++;
            return true;
        }

        return false;
    }

    private bool TryConsumeSymbol(string symbol)
    {
        if (!IsAtEnd && Peek().Text == symbol)
        {
            position++;
            return true;
        }

        return false;
    }

    private bool CheckKeyword(string keyword)
    {
        return !IsAtEnd && Peek().Normalized == keyword;
    }

    private bool CheckKeywordSequence(string first, string second)
    {
        return !IsAtEnd && Peek().Normalized == first && PeekOptional(1)?.Normalized == second;
    }

    private SqlToken Peek(int offset = 0)
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

    private SqlToken Read()
    {
        return tokens[position++];
    }

    private bool IsAtEnd => position >= tokens.Count;
}

internal enum ClauseStart
{
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
        "INSERT",
        "INTO",
        "VALUES",
        "RETURNING",
        "OUTPUT",
        "UPDATE",
        "SET",
        "DELETE",
        "WITH",
        "RECURSIVE",
        "AS",
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
