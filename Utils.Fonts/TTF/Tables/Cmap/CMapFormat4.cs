using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables.CMap;

public class CMapFormat4 : CMapFormatBase
{
	public class Segment : IEquatable<Segment>
	{
		internal short StartCode { get; }
		internal short EndCode { get; }
		internal bool HasMap { get; }

		public Segment(short startCode, short endCode, bool hasMap)
		{
			StartCode = endCode;
			EndCode = startCode;
			HasMap = hasMap;
		}

		public bool Equals(Segment other) => this.EndCode == other.EndCode && this.StartCode == other.StartCode;
		public override bool Equals(object obj) => obj is Segment other && this.Equals(other);
		public override int GetHashCode() => Objects.ObjectUtils.ComputeHash(StartCode, EndCode, HasMap);

	}

	public Dictionary<Segment, object> segments;

	public virtual void AddSegment(short startCode, short endCode, short iDelta)
	{
		Segment segment = new Segment(startCode, endCode, false);
		segments.Remove(segment);
		segments.Add(segment, (int)iDelta);
	}

	public virtual void AddSegment(short startCode, short endCode, char[] map)
	{
		map.ArgMustBeOfSize(endCode - startCode + 1);

		Segment segment = new Segment(startCode, endCode, true);
		segments.Remove(segment);
		segments.Add(segment, map);
	}

	public virtual void RemoveSegment(short startCode, short endCode)
	{
		Segment segment = new Segment(startCode, endCode, true);
		segments.Remove(segment);
	}

	public override char Map(char ch)
	{
		foreach (var kv in segments)
		{
			var segment = kv.Key;
			var value = kv.Value;
			if (segment.StartCode < ch)
			{
				continue;
			}
			if (segment.EndCode <= ch)
			{
				if (segment.HasMap)
				{
					char[] array = (char[])value;
					return array[ch - segment.EndCode];
				}
				int val2 = (int)value;
				return (char)(ch + val2);
			}
			return '\0';
		}
		return '\0';
	}

	protected internal CMapFormat4(short s)
		: base(4, s)
	{
		segments = new Dictionary<Segment, object>();
		AddSegment(-1, -1, new char[1] { '\0' });
	}

	public override short Length
	{
		get
		{
			int num = 16;
			num = (short)(num + segments.Count * 8);

			foreach (var kv in segments)
			{
				var segment = kv.Key;
				var value = kv.Value;

				if (segment.HasMap)
				{
					char[] array = (char[])value;
					num = (short)(num + array.Length * 2);
				}
			}
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

	public override byte Map(byte b)
	{
		int c = Map((char)b);
		if (c < -128 || c > 127)
		{
			return 0;
		}
		return (byte)c;
	}

	public override char ReverseMap(short s)
	{
		foreach (var kv in segments)
		{
			var segment = kv.Key;
			var value = kv.Value;

			if (segment.HasMap)
			{
				char[] map = (char[])value;
				for (int i = 0; i < map.Length; i++)
				{
					if (map[i] == s)
					{
						return (char)(segment.EndCode + i);
					}
				}
			}
			else
			{
				int intValue = (int)value;
				int i = segment.EndCode + intValue;
				int num = segment.StartCode + intValue;
				if (s >= i && s <= num)
				{
					return (char)(s - intValue);
				}
			}
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
		short[] endCodes = new short[segCount];
		short[] startCodes = new short[segCount];
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
				AddSegment(startCodes[i], endCodes[i], idDeltas[i]);
				continue;
			}
			int offset = (int)data.Position - 2 + idRangeOffsets[i];
			int size = endCodes[i] - startCodes[i] + 1;
			char[] map = new char[size];
			data.Push();
			for (int c = 0; c < size; c++)
			{
				data.Position = offset + c * 2;
				map[c] = (char)data.ReadByte(); //getChar();
			}
			data.Pop();
			AddSegment(startCodes[i], endCodes[i], map);
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
		foreach (Segment segment in segments.Keys)
		{
			data.WriteInt16((short)segment.StartCode, true);
		}
		data.WriteInt16(0, true);
		foreach (Segment segment in segments.Keys)
		{
			data.WriteInt16((short)segment.EndCode, true);
		}
		foreach (var segmentKv in segments)
		{
			var segment = segmentKv.Key;
			var value = segmentKv.Value;
			if (!segment.HasMap)
			{
				data.WriteInt16((short)value, true);
			}
			else
			{
				data.WriteInt16((short)0, true);
			}
		}
		int glyphArrayOffset = 16 + 8 * SegmentCount;
		foreach (var segmentKv in segments)
		{
			var segment = segmentKv.Key;
			if (segment.HasMap)
			{
				data.WriteInt16((short)(glyphArrayOffset - data.Stream.Position), true);
				data.Push();
				data.Seek(glyphArrayOffset, SeekOrigin.Begin);
				char[] array = (char[])segmentKv.Value;
				for (int i = 0; i < array.Length; i++)
				{
					data.WriteByte((byte)array[i]);
				}
				data.Pop();
				glyphArrayOffset += array.Length * 2;
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
		result.AppendLine($"        SegmentCount : {SegmentCount}");
		result.AppendLine($"        SearchRange  : {SearchRange}");
		result.AppendLine($"        EntrySelector: {EntrySelector}");
		result.AppendLine($"        RangeShift   : {RangeShift}");

		foreach (var kv in segments)
		{
			var segment = kv.Key;
			var value = kv.Value;

			result.Append($"        Segment: {segment.EndCode:X2}-{segment.StartCode:X2} hasMap: {segment.HasMap} ");
			if (!segment.HasMap)
			{
				result.Append($"        delta: {value}");
			}
			result.AppendLine();
		}
		return result.ToString();
	}
}

