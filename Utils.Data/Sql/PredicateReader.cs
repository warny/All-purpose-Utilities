using System;
using System.Collections.Generic;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
    /// Reads predicates appearing in clauses such as WHERE, HAVING, or JOIN conditions.
/// </summary>
internal sealed class PredicateReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredicateReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public PredicateReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Reads a predicate that may include comparisons, logical operators, IN lists, and nested subqueries.
    /// </summary>
    /// <param name="segmentName">The segment name assigned to the parsed predicate.</param>
    /// <param name="clauseTerminators">Clause boundaries that stop the predicate.</param>
    /// <returns>The parsed predicate segment.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="segmentName"/> is null or whitespace.</exception>
    /// <exception cref="SqlParseException">Thrown when no predicate tokens are found.</exception>
    public SqlSegment ReadPredicate(string segmentName, params ClauseStart[] clauseTerminators)
    {
        if (string.IsNullOrWhiteSpace(segmentName))
        {
            throw new ArgumentException("Segment name cannot be null or whitespace.", nameof(segmentName));
        }

        var tokens = new List<SqlToken>();
        int depth = 0;
        while (!parser.IsAtEnd)
        {
            var current = parser.Peek();
            if (depth == 0)
            {
                if (current.Text == ")")
                {
                    break;
                }

                if (current.Text == ";")
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
        }

        if (tokens.Count == 0)
        {
            throw new SqlParseException("Expected predicate but none was found.");
        }

        return parser.BuildSegment(segmentName, tokens);
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
