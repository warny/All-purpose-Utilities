using System;
using System.Collections.Generic;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Reads simple comma-separated field names from a shared <see cref="SqlParser"/> context.
/// </summary>
internal sealed class FieldListReader
{
    private readonly SqlParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldListReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public FieldListReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Reads a sequence of fields separated by commas until a clause boundary is reached.
    /// </summary>
    /// <param name="segmentNamePrefix">Prefix used to name field segments.</param>
    /// <param name="clauseTerminators">Clause boundaries that stop the list.</param>
    /// <returns>The parsed field segments.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="segmentNamePrefix"/> is null or whitespace.</exception>
    public IReadOnlyList<SqlSegment> ReadFields(string segmentNamePrefix, params ClauseStart[] clauseTerminators)
    {
        if (string.IsNullOrWhiteSpace(segmentNamePrefix))
        {
            throw new ArgumentException("Segment name prefix cannot be null or whitespace.", nameof(segmentNamePrefix));
        }

        var results = new List<SqlSegment>();
        int index = 1;
        while (true)
        {
            results.Add(ReadField($"{segmentNamePrefix}{index}", clauseTerminators));
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

            throw new SqlParseException($"Unexpected token '{next.Text}' while reading field list.");
        }

        return results;
    }

    /// <summary>
    /// Reads a single field, stopping before the next comma, closing parenthesis, or clause boundary.
    /// </summary>
    /// <param name="segmentName">The segment name assigned to the parsed field.</param>
    /// <param name="clauseTerminators">Clause boundaries that stop the field.</param>
    /// <returns>The parsed field segment.</returns>
    /// <exception cref="SqlParseException">Thrown when no field tokens are found.</exception>
    private SqlSegment ReadField(string segmentName, params ClauseStart[] clauseTerminators)
    {
        var tokens = new List<SqlToken>();
        int depth = 0;
        while (!parser.IsAtEnd)
        {
            var current = parser.Peek();
            if (depth == 0)
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
            if (current.Text == "(")
            {
                depth++;
            }
            else if (current.Text == ")" && depth > 0)
            {
                depth--;
            }
        }

        if (tokens.Count == 0)
        {
            throw new SqlParseException("Expected field but none was found.");
        }

        return parser.BuildSegment(segmentName, tokens);
    }
}
