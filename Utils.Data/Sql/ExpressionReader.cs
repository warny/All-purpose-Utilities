using System;
using System.Collections.Generic;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Reads SQL expressions that may optionally include aliases from a shared <see cref="SqlParser"/> context.
/// </summary>
internal sealed class ExpressionReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionReader"/> class.
    /// </summary>
    /// <param name="parser">The parser providing token access.</param>
    public ExpressionReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Reads a single expression up to the next comma or specified clause boundary.
    /// </summary>
    /// <param name="segmentName">The segment name assigned to the parsed expression.</param>
    /// <param name="allowAlias">Indicates whether an alias is allowed after the expression.</param>
    /// <param name="clauseTerminators">Clause boundaries that stop the expression.</param>
    /// <returns>The parsed expression along with its optional alias.</returns>
    /// <exception cref="SqlParseException">Thrown when no expression could be read or an alias is malformed.</exception>
    public ExpressionReadResult ReadExpression(string segmentName, bool allowAlias, params ClauseStart[] clauseTerminators)
    {
        if (string.IsNullOrWhiteSpace(segmentName))
        {
            throw new ArgumentException("Segment name cannot be null or whitespace.", nameof(segmentName));
        }

        var tokens = new List<SqlToken>();
        int parenthesisDepth = 0;
        int caseDepth = 0;
        while (!parser.IsAtEnd)
        {
            var current = parser.Peek();
            if (IsTopLevel(parenthesisDepth, caseDepth))
            {
                if (current.Text == ",")
                {
                    break;
                }

                if (clauseTerminators.Length > 0 && parser.IsClauseStart(clauseTerminators))
                {
                    break;
                }
            }

            tokens.Add(parser.Read());
            UpdateDepths(current, ref parenthesisDepth, ref caseDepth);
        }

        if (tokens.Count == 0)
        {
            throw new SqlParseException("Expected expression but none was found.");
        }

        var fullTokens = new List<SqlToken>(tokens);
        string? alias = null;
        if (allowAlias)
        {
            alias = ExtractAlias(tokens);
        }

        if (tokens.Count == 0)
        {
            throw new SqlParseException("Expression cannot be reduced to an alias only.");
        }

        return new ExpressionReadResult(parser.BuildSegment(segmentName, tokens), alias, fullTokens);
    }

    /// <summary>
    /// Extracts an alias from the end of the provided token list when present.
    /// </summary>
    /// <param name="tokens">Tokens composing the expression and potential alias.</param>
    /// <returns>The alias text when found; otherwise, <c>null</c>.</returns>
    /// <exception cref="SqlParseException">Thrown when an alias indicator is not followed by a valid identifier.</exception>
    private static string? ExtractAlias(List<SqlToken> tokens)
    {
        if (tokens.Count >= 2)
        {
            var last = tokens[^1];
            var beforeLast = tokens[^2];
            if (beforeLast.Normalized == "AS")
            {
                if (!last.IsIdentifier)
                {
                    throw new SqlParseException($"Expected identifier after AS but found '{last.Text}'.");
                }

                tokens.RemoveRange(tokens.Count - 2, 2);
                return last.Text;
            }
        }

        if (tokens.Count >= 2)
        {
            var last = tokens[^1];
            var beforeLast = tokens[^2];
            if (last.IsIdentifier && !last.IsKeyword && !IsAliasSeparator(beforeLast))
            {
                tokens.RemoveAt(tokens.Count - 1);
                return last.Text;
            }
        }

        return null;
    }

    /// <summary>
    /// Updates the current parenthesis depth based on the provided token.
    /// </summary>
    /// <param name="token">The token being processed.</param>
    /// <param name="depth">The tracked depth value.</param>
    private static void UpdateDepths(SqlToken token, ref int parenthesisDepth, ref int caseDepth)
    {
        if (token.Text == "(")
        {
            parenthesisDepth++;
        }
        else if (token.Text == ")" && parenthesisDepth > 0)
        {
            parenthesisDepth--;
        }

        if (token.Normalized == "CASE")
        {
            caseDepth++;
        }
        else if (token.Normalized == "END" && caseDepth > 0)
        {
            caseDepth--;
        }
    }

    /// <summary>
    /// Determines whether parsing is currently at the top-level expression scope.
    /// </summary>
    /// <param name="parenthesisDepth">The tracked parenthesis depth.</param>
    /// <param name="caseDepth">The tracked CASE expression depth.</param>
    /// <returns><c>true</c> when both depths indicate the outermost scope.</returns>
    private static bool IsTopLevel(int parenthesisDepth, int caseDepth)
    {
        return parenthesisDepth == 0 && caseDepth == 0;
    }

    /// <summary>
    /// Determines whether the provided token prevents alias extraction because it is attached to an identifier.
    /// </summary>
    /// <param name="token">The token immediately preceding an alias candidate.</param>
    /// <returns><c>true</c> when the token is considered part of the identifier chain.</returns>
    private static bool IsAliasSeparator(SqlToken token)
    {
        return token.Text is "." or "::";
    }
}

/// <summary>
/// Represents an expression read from SQL along with its optional alias.
/// </summary>
internal sealed class ExpressionReadResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionReadResult"/> class.
    /// </summary>
    /// <param name="expression">The parsed expression segment.</param>
    /// <param name="alias">The optional alias associated with the expression.</param>
    public ExpressionReadResult(SqlSegment expression, string? alias, IReadOnlyList<SqlToken> tokens)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Alias = alias;
        Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    /// <summary>
    /// Gets the parsed expression segment.
    /// </summary>
    public SqlSegment Expression { get; }

    /// <summary>
    /// Gets the optional alias associated with the expression.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Gets the tokens that compose the expression including any alias tokens.
    /// </summary>
    public IReadOnlyList<SqlToken> Tokens { get; }

    /// <summary>
    /// Builds the SQL snippet represented by the expression and its alias when present.
    /// </summary>
    /// <returns>The SQL text for the expression.</returns>
    public string ToSql()
    {
        string expressionText = Expression.ToSql();
        return string.IsNullOrWhiteSpace(Alias) ? expressionText : $"{expressionText} {Alias}";
    }
}
