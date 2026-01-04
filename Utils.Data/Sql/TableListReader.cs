using System;
using System.Collections.Generic;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Reads comma- or join-separated table sources from a shared <see cref="SqlParser"/> context.
/// </summary>
internal sealed class TableListReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableListReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public TableListReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Reads a sequence of table sources until a clause boundary is reached.
    /// </summary>
    /// <param name="segmentNamePrefix">Prefix used to name table segments.</param>
    /// <param name="clauseTerminators">Clause boundaries that stop the list.</param>
    /// <returns>The parsed table segments.</returns>
    public IReadOnlyList<SqlSegment> ReadTables(string segmentNamePrefix, params ClauseStart[] clauseTerminators)
    {
        if (string.IsNullOrWhiteSpace(segmentNamePrefix))
        {
            throw new ArgumentException("Segment name prefix cannot be null or whitespace.", nameof(segmentNamePrefix));
        }

        var results = new List<SqlSegment>();
        int index = 1;
        while (true)
        {
            results.Add(ReadTable($"{segmentNamePrefix}{index}", clauseTerminators));
            index++;

            if (parser.IsAtEnd || parser.IsClauseStart(clauseTerminators))
            {
                break;
            }

            var next = parser.Peek();
            if (next.Text == ",")
            {
                parser.Read();
                continue;
            }

            if (next.Text == ")")
            {
                break;
            }

            throw new SqlParseException($"Unexpected token '{next.Text}' while reading table list.");
        }

        return results;
    }

    /// <summary>
    /// Reads a single table source, including any associated JOIN or APPLY constructs.
    /// </summary>
    /// <param name="segmentName">The segment name assigned to the parsed table.</param>
    /// <param name="clauseTerminators">Clause boundaries that stop the table.</param>
    /// <returns>The parsed table segment.</returns>
    /// <exception cref="SqlParseException">Thrown when no table tokens are found or JOIN clauses are incomplete.</exception>
    private SqlSegment ReadTable(string segmentName, params ClauseStart[] clauseTerminators)
    {
        var tokens = new List<SqlToken>();
        int depth = 0;
        int joinCount = 0;
        int onCount = 0;
        while (!parser.IsAtEnd)
        {
            var current = parser.Peek();
            bool joinSatisfied = onCount >= joinCount;

            if (depth == 0 && joinSatisfied)
            {
                if (current.Text == "," || current.Text == ")")
                {
                    break;
                }

                if (clauseTerminators.Length > 0 && parser.IsClauseStart(clauseTerminators))
                {
                    break;
                }
            }

            tokens.Add(parser.Read());
            UpdateDepth(current, ref depth);

            if (depth == 0)
            {
                if (IsJoinRequiringOn(tokens, current))
                {
                    joinCount++;
                }
                else if (IsOnKeyword(current))
                {
                    onCount++;
                }
            }
        }

        if (tokens.Count == 0)
        {
            throw new SqlParseException("Expected table but none was found.");
        }

        if (onCount < joinCount)
        {
            throw new SqlParseException("Missing ON clause for one or more JOIN operations.");
        }

        return parser.BuildSegment(segmentName, tokens);
    }

    /// <summary>
    /// Determines whether the provided token represents a JOIN that requires an ON clause.
    /// </summary>
    /// <param name="tokens">Tokens collected so far for the current table.</param>
    /// <param name="token">The token to evaluate.</param>
    /// <returns><c>true</c> when the token is a JOIN keyword not preceded by CROSS.</returns>
    private static bool IsJoinRequiringOn(IReadOnlyList<SqlToken> tokens, SqlToken token)
    {
        if (!token.IsKeyword || !string.Equals(token.Normalized, "JOIN", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var previousToken = tokens.Count >= 2 ? tokens[^2] : null;
        return previousToken == null || !string.Equals(previousToken.Normalized, "CROSS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the token represents an ON keyword at the current depth.
    /// </summary>
    /// <param name="token">The token to evaluate.</param>
    /// <returns><c>true</c> when the token is ON.</returns>
    private static bool IsOnKeyword(SqlToken token)
    {
        return token.IsKeyword && string.Equals(token.Normalized, "ON", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Updates the tracked parenthesis depth for the provided token.
    /// </summary>
    /// <param name="token">The token to evaluate.</param>
    /// <param name="depth">The tracked depth to update.</param>
    private static void UpdateDepth(SqlToken token, ref int depth)
    {
        if (token.Text == "(")
        {
            depth++;
        }
        else if (token.Text == ")" && depth > 0)
        {
            depth--;
        }
    }
}
