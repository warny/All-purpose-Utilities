using System;
using System.Collections.Generic;

namespace Utils.Data.Sql;

#nullable enable

/// <summary>
/// Reads comma-separated SQL expressions using an underlying <see cref="ExpressionReader"/>.
/// </summary>
internal sealed class ExpressionListReader
{
    private readonly SqlParser parser;
    private readonly ExpressionReader expressionReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionListReader"/> class.
    /// </summary>
    /// <param name="parser">The parser supplying token access.</param>
    public ExpressionListReader(SqlParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        expressionReader = new ExpressionReader(this.parser);
    }

    /// <summary>
    /// Reads a sequence of expressions separated by commas until a clause boundary is reached.
    /// </summary>
    /// <param name="segmentNamePrefix">Prefix used to name expression segments.</param>
    /// <param name="allowAliases">Indicates whether expressions may declare aliases.</param>
    /// <param name="clauseTerminators">Clause boundaries that stop the list.</param>
    /// <returns>The parsed expressions with their aliases.</returns>
    public IReadOnlyList<ExpressionReadResult> ReadExpressions(string segmentNamePrefix, bool allowAliases, params ClauseStart[] clauseTerminators)
    {
        if (string.IsNullOrWhiteSpace(segmentNamePrefix))
        {
            throw new ArgumentException("Segment name prefix cannot be null or whitespace.", nameof(segmentNamePrefix));
        }

        var results = new List<ExpressionReadResult>();
        int index = 1;
        while (true)
        {
            results.Add(expressionReader.ReadExpression($"{segmentNamePrefix}{index}", allowAliases, clauseTerminators));
            index++;

            if (parser.IsAtEnd || parser.Peek().Text != ",")
            {
                break;
            }

            parser.Read();
        }

        return results;
    }
}
