using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Data.Sql;

abstract internal class StatementParserBase
{
	protected readonly SqlParser parser;

	public static IPartReader SelectReader = SelectPartReader.Singleton;
	public static IPartReader SpdateTargetReader => UpdatePartReader.Singleton;
	public static IPartReader DeleteReader => DeletePartReader.Singleton;
	public static IPartReader FromReader => FromPartReader.Singleton;
	public static IPartReader UsingReader => UsingPartReader.Singleton;
	public static IPartReader WhereReader => WherePartReader.Singleton;
	public static IPartReader OutputReader => OutputPartReader.Singleton;
	public static IPartReader ReturningReader => ReturningPartReader.Singleton;
	public static IPartReader LimitReader => LimitPartReader.Singleton;
	public static IPartReader OffsetReader => OffsetPartReader.Singleton;
	public static IPartReader IntoReader => IntoPartReader.Singleton;
	public static IPartReader ValuesReader => ValuesPartReader.Singleton;
	public static IPartReader SetReader => SetPartReader.Singleton;
	public static IPartReader GroupByReader => GroupByPartReader.Singleton;
	public static IPartReader HavingReader => HavingPartReader.Singleton;
	public static IPartReader OrderByReader => OrderByPartReader.Singleton;
	public static IPartReader SetOperatorReader => SetOperatorPartReader.Singleton;


	protected StatementParserBase(SqlParser parser)
	{
		this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
	}

	protected Dictionary<ClauseStart, SqlSegment> ReadSegments(params IEnumerable<IPartReader> readers)
    {
        Dictionary<ClauseStart, SqlSegment> segments = new();
        return this.ReadSegments(segments, readers);
    }

    protected Dictionary<ClauseStart, SqlSegment> ReadSegments(Dictionary<ClauseStart, SqlSegment> segments, params IEnumerable<IPartReader> readers)
    {
        Queue<IPartReader> readersQueue = new Queue<IPartReader>(readers);

        while (readersQueue.Count > 0)
        {
            var reader = readersQueue.Dequeue();
            SqlSegment? segment = null;
            if (!parser.IsAtEnd)
            {
                foreach (var keyWordSequence in reader.KeywordSequences)
                {
                    if (!parser.CheckKeywordSequence(keyWordSequence)) continue;
                    segment = reader.TryRead(parser, readersQueue.Select(r => r.Clause));
                }
            }
            segments[reader.Clause] = segment;
        }

        return segments;
    }
}
