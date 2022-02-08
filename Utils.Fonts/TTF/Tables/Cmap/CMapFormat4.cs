using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables.CMap;

public class CMapFormat4 : CMapFormatBase
{
	public abstract class Segment : IEquatable<Segment>, IComparable<Segment>
	{
		internal char StartCode { get; }
		internal char EndCode { get; }
		internal abstract bool HasMap { get; }
		internal abstract int Length { get; }

		public Segment(char startCode, char endCode)
		{
			StartCode = startCode;
			EndCode = endCode;
		}

		public bool Equals(Segment other) => this.StartCode == other.StartCode && this.EndCode == other.EndCode;
		public override bool Equals(object obj) => obj is Segment other && this.Equals(other);
		public override int GetHashCode() => ObjectUtils.ComputeHash(StartCode, EndCode);
		public abstract char this[short index] { get; }
		public abstract short this[char c] { get; }
		public int CompareTo(Segment other) => this.StartCode.CompareTo(other?.StartCode);
	}

	public class TableMap : Segment
	{
		internal IReadOnlyList<short> Map { get; }
		private Dictionary<short, char> reverseMap;

		internal override bool HasMap => true;

		internal override int Length => 8 + Map.Count * sizeof(short);

		public TableMap(char startCode, char endCode, short[] map) : base(startCode, endCode)
		{
			this.Map = map;
			reverseMap = new Dictionary<short, char>(
				map
					.Select((s, i) => new KeyValuePair<short, char>(s, (char)i))
					.Where(i => i.Key!=0)
					.GroupBy(i => i.Key)
					.Select(v=>v.FirstOrDefault())
			);
		}

		public override char this[short index] {
			get
			{
				return reverseMap.TryGetValue(index, out var result) ? result : '\0';
			}
		}
		public override short this[char c] => Map[c - StartCode];

		public override string ToString() => "Table";
	}

	public class DeltaMap : Segment
	{

		internal short Delta { get; }
		internal override bool HasMap => false;

		internal override int Length => 8;

		public DeltaMap(char startCode, char endCode, short delta) : base(startCode, endCode)
		{
			this.Delta = delta;
		}

		public override char this[short index]
		{
			get
			{
				var result = (char)(index - Delta);
				if (result.Between(StartCode, EndCode)) return result;
				return '\0';
			}
		}
		public override short this[char c] => (short)(c + Delta);
		public override string ToString() => $"Delta: {Delta}";
	}

	public SortedSet<Segment> segments;

	protected internal CMapFormat4(short s)
		: base(4, s)
	{
		segments = new SortedSet<Segment>();
	}

	public virtual void AddSegment(char startCode, char endCode, short delta)
	{
		Segment segment = new DeltaMap(startCode, endCode, delta);

		segments.Remove(segment);
		segments.Add(segment);
	}

	public virtual void AddSegment(char startCode, char endCode, short[] map)
	{
		map.ArgMustBeOfSize(endCode - startCode + 1);

		Segment segment = new TableMap(startCode, endCode, map);
		segments.Remove(segment);
		segments.Add(segment);
	}

	public virtual void RemoveSegment(char startCode, char endCode)
	{
		Segment segment = new DeltaMap(startCode, endCode, 0);
		segments.Remove(segment);
	}

	public override short Length
	{
		get
		{
			int num = 16;
			num += segments.Count * 8;
			num += segments.Sum(s => s.Length);
			return (short)num;
		}
	}

	public virtual short SegmentCount => (short)segments.Count;

	public virtual short SearchRange
	{
		get
		{

			double pow = Math.Floor(Math.Log(SegmentCount, 2));
			double pow2 = Math.Pow(2, pow);
			return (short)(2 * pow2);
		}
	}

	public virtual short EntrySelector
	{
		get
		{
			int sr2 = SearchRange / 2;
			return (short)Math.Log(sr2, 2);
		}
	}

	public virtual short RangeShift => (short)(2 * SegmentCount - SearchRange);

	public override short Map(char ch)
	{
		foreach (var segment in segments)
		{
			if (segment.StartCode < ch)
			{
				continue;
			}
			if (segment.EndCode <= ch)
			{
				return segment[ch];
			}
			return 0;
		}
		return 0;
	}

	public override char ReverseMap(short s)
	{
		foreach (var segment in segments)
		{
			var result = segment[s];
			if (result != 0) return '\0';
		}
		return '\0';
	}

	public override void ReadData(int length, Reader data)
	{
		int segCount = (short)(data.ReadInt16(true) / 2);
		/* Length = */
		data.ReadInt16(true);
		/* Language = */
		data.ReadInt16(true);
		data.ReadInt16(true);
		short[] startCodes = new short[segCount];
		short[] endCodes = new short[segCount];
		short[] idDeltas = new short[segCount];
		short[] idRangeOffsets = new short[segCount];
		/* int glyphArrayPos = 16 + (8 * segCount) 
		 */
		for (int i = 0; i < segCount; i++)
		{
			endCodes[i] = data.ReadInt16(true);
		}
		data.ReadInt16(true);
		for (int i = 0; i < segCount; i++)
		{
			startCodes[i] = data.ReadInt16(true);
		}
		for (int i = 0; i < segCount; i++)
		{
			idDeltas[i] = data.ReadInt16(true);
		}
		for (int i = 0; i < segCount; i++)
		{
			idRangeOffsets[i] = data.ReadInt16(true);
			if (idRangeOffsets[i] <= 0)
			{
				AddSegment((char)startCodes[i], (char)endCodes[i], idDeltas[i]);
				continue;
			}
			int offset = (int)data.Position - 2 + idRangeOffsets[i];
			int size = endCodes[i] - startCodes[i] + 1;
			data.Push();
			var map = data.ReadArray<short>(size, true);
			data.Pop();
			AddSegment((char)startCodes[i], (char)endCodes[i], map);
		}
	}

	public override void WriteData(Writer data)
	{
		data.WriteInt16(Format, true);
		data.WriteInt16(Length, true);
		data.WriteInt16(Language, true);
		data.WriteInt16((short)(SegmentCount * 2), true);
		data.WriteInt16(SearchRange, true);
		data.WriteInt16(EntrySelector, true);
		data.WriteInt16(RangeShift, true);
		foreach (Segment segment in segments)
		{
			data.WriteInt16((short)segment.StartCode, true);
		}
		data.WriteInt16(0, true);
		foreach (Segment segment in segments)
		{
			data.WriteInt16((short)segment.EndCode, true);
		}
		foreach (var segment in segments)
		{
			if (segment is DeltaMap deltaMap)
			{
				data.WriteInt16(deltaMap.Delta, true);
			}
			else if (segment is TableMap tableMap)
			{
				data.WriteInt16(0, true);
			}
		}
		int glyphArrayOffset = 16 + 8 * SegmentCount;
		foreach (var segment in segments)
		{
			if (segment is TableMap tableMap)
			{
				data.WriteInt16((short)(glyphArrayOffset - data.Stream.Position), true);
				data.Push();
				data.Seek(glyphArrayOffset, SeekOrigin.Begin);
				foreach (var index in tableMap.Map)
				{
					data.WriteByte((byte)index);
				}

				data.Pop();
				glyphArrayOffset += tableMap.Map.Count * 2;
			}
			else
			{
				data.WriteInt16(0, true);
			}
		}
	}

	public override string ToString()
	{
		StringBuilder result = new StringBuilder();
		result.Append(base.ToString());
		result.AppendLine($"    SegmentCount : {SegmentCount}");
		result.AppendLine($"    SearchRange  : {SearchRange}");
		result.AppendLine($"    EntrySelector: {EntrySelector}");
		result.AppendLine($"    RangeShift   : {RangeShift}");

		foreach (var segment in segments)
		{
			result.Append($"        Segment: \'{segment.StartCode}\'{(short)segment.StartCode:X2}-\'{segment.EndCode}\'{(short)segment.EndCode:X2} hasMap: {segment.HasMap} => {segment}");
			result.AppendLine();
		}
		return result.ToString();
	}
}

