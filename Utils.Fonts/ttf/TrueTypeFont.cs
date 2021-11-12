using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Utils.IO.Serialization;
using Utils.Mathematics;

namespace Utils.Fonts.TTF
{
	public class TrueTypeFont
	{
		public int Type { get; }

		private readonly Dictionary<Tag, TrueTypeTable> tables;

		public TrueTypeFont(int type)
		{
			Type = type;
			tables = new Dictionary<Tag, TrueTypeTable>();
		}

		public static TrueTypeFont ParseFont(byte[] bytes)
		{
			MemoryStream ms = new MemoryStream(bytes);
			var data = new Reader(ms);
			return ParseFont(data);
		}

		public static TrueTypeFont ParseFont(Stream s)
		{
			if (!s.CanRead) throw new InvalidOperationException("The stream must be readable");
			if (!s.CanSeek)
			{
				var ms = new MemoryStream();
				s.CopyTo(ms);
				s = ms;
			}
			var reader = new Reader(s);
			return ParseFont(reader);
		}

		public static TrueTypeFont ParseFont(Reader data)
		{
			int type = data.ReadInt32(true);
			int numTables = data.ReadInt16(true);
			/* short searchRange = */ data.ReadInt16(true);
			/* short entrySelector = */ data.ReadInt16(true);
			/* short rangeShift = */ data.ReadInt16(true);
			TrueTypeFont trueTypeFont = new TrueTypeFont(type);
			ParseDirectories(data, numTables, trueTypeFont);
			return trueTypeFont;
		}

		public virtual byte[] WriteFont()
		{
			MemoryStream ms = new MemoryStream(Length);
			var data = new Writer(ms);

			data.WriteInt32(Type, true);
			data.WriteInt16(TablesCount, true);
			data.WriteInt16(SearchRange, true);
			data.WriteInt16(EntrySelector, true);
			data.WriteInt16(RangeShift, true);
			int currentoffset = 12 + TablesCount * 16;
			foreach (var tagTable in tables) {
				var tag = tagTable.Key;
				TrueTypeTable obj = tagTable.Value;

				MemoryStream datasStream = new MemoryStream();
				Writer w = new Writer(datasStream);
				obj.WriteData(w);
				var datas = datasStream.ToArray();
				int dataLength = datas.Length;
				data.WriteFixedLengthString(tag, 4, Encoding.ASCII);
				data.WriteInt32(ComputeChecksum(tag, new ReaderWriter(datasStream)), true);
				data.WriteInt32(currentoffset, true);
				data.WriteInt32(dataLength, true);
				data.Push();
				data.Seek(currentoffset, SeekOrigin.Begin);
				data.WriteBytes(datas);
				data.Pop();
				currentoffset += dataLength;
				//Adjust File Length
				while (currentoffset % 4 > 0)
				{
					currentoffset++;
					data.WriteByte(0);
				}
			}
			data.Position = 0;
			UpdateChecksumAdj(new ReaderWriter(ms));
			return ms.ToArray();
		}

		private static void ParseDirectories(Reader data, int numTables, TrueTypeFont ttf)
		{
			var tables = new SortedSet<TableDeclaration>();

			for (int i = 0; i < numTables; i++)
			{
				tables.Add(new TableDeclaration()
				{
					Tag = data.ReadFixedLengthString(4, Encoding.ASCII),
					CheckSum = data.ReadInt32(true),
					Offset = data.ReadInt32(true),
					DataLength = data.ReadInt32(true),
				});
			}

			foreach (var td in tables)
			{
				data.Position = td.Offset;
				td.Data = new MemoryStream(data.ReadBytes(td.DataLength));
				var checkSum = ComputeChecksum(td.Tag, new ReaderWriter(td.Data));
				if (checkSum != td.CheckSum)
				{
					Console.WriteLine($"Declared Checksum {td.CheckSum:X4} is different from {checkSum:X4}");
				}
			}

			void ReadTable(Tag tag)
			{
				var table = tables.FirstOrDefault(td => td.Tag == tag);
				ttf.AddTable(table.Tag, new Reader(table.Data));
				tables.Remove(table);
			}
			ReadTable(TrueTypeTableTypes.head);
			ReadTable(TrueTypeTableTypes.maxp);
			ReadTable(TrueTypeTableTypes.loca);
			ReadTable(TrueTypeTableTypes.hhea);

			foreach (var table in tables)
			{
 				ttf.AddTable(table.Tag, new Reader(table.Data));
			}

		}

		public virtual short TablesCount => (short)tables.Count;

		public virtual short SearchRange
		{
			get {
				double num = Math.Floor(Math.Log(TablesCount, 2));
				double num2 = Math.Pow(2.0, num);
				return (short)(16.0 * num2);
			}
		}

		private int Length => 12 + TablesCount * 16 + tables.Values.Sum(t => MathEx.Ceiling(t.Length, 4));

		public virtual short EntrySelector
		{
			get {
				double num = Math.Floor(Math.Log(TablesCount, 2));
				double num2 = Math.Pow(2.0, num);
				return (short)Math.Log(num2, 2);
			}
		}

		public virtual short RangeShift
		{
			get {
				double num = Math.Floor(Math.Log(TablesCount, 2));
				short num2 = (short)Math.Pow(2.0, num);
				return (short)(num2 * 16 - SearchRange);
			}
		}

		private static int ComputeChecksum(string tagString, ReaderWriter data)
		{
			unchecked
			{
				int result = 0;
				data.Push();
				if (tagString == "head")
				{
					data.Position = 8;
					data.Writer.WriteInt32(0);
				}
				int nLongs = ((int)data.BytesLeft + 3) / 4;
				while (nLongs-- > 0)
				{
					if (data.BytesLeft > 3)
					{
						result += data.Reader.ReadInt32(true);
						continue;
					}
					int b0 = (data.BytesLeft > 0) ? data.Reader.ReadByte() : 0;
					int b1 = (data.BytesLeft > 0) ? data.Reader.ReadByte() : 0;
					int b2 = (data.BytesLeft > 0) ? data.Reader.ReadByte() : 0;
					result += ((0xFF & b0) << 24) | ((0xFF & b1) << 16) | ((0xFF & b2) << 8);
				}
				data.Pop();
				return result;
			}
		}

		private void UpdateChecksumAdj(ReaderWriter data)
		{
			unchecked
			{
				long checksum = ComputeChecksum("", data);
				long checksumAdj = 0xb1b0afbaL - checksum;
				int offset = 12 + TablesCount * 16;
				foreach (var table in tables) {
					var tag = table.Key;
					if (tag == TrueTypeTableTypes.head)
					{
						data.Seek(offset + 8);
						data.Writer.WriteUInt32((uint)checksumAdj);
						break;
					}
					offset += table.Value.Length;
					if ((offset % 4) != 0)
					{
						offset += (4 - (offset % 4));
					}
				}
			}
		}

		public virtual T GetTable<T>(Tag tag) where T : TrueTypeTable => (T)tables[tag];
		public virtual void AddTable(Tag tag, Reader data) => tables[tag] = TrueTypeTable.CreateTable(this, tag, data);
		public virtual void AddTable(Tag tag, TrueTypeTable ttt) => tables[tag] = ttt;
		public virtual void RemoveTable(Tag tag) => tables.Remove(tag);

		public override string ToString()
		{
			StringBuilder result = new StringBuilder();
			result.AppendLine($"Type         : {Type}");
			result.AppendLine($"NumTables    : {TablesCount}");
			result.AppendLine($"SearchRange  : {SearchRange}");
			result.AppendLine($"EntrySelector: {EntrySelector}");
			result.AppendLine($"RangeShift   : {RangeShift}");
			foreach (var table in tables)
			{
				result.AppendLine(table.Value.ToString());
			}
			return result.ToString();
		}

		public sealed class TableDeclaration : IComparable<TableDeclaration>, IComparable
		{
			public Tag Tag { get; set; }
			public int CheckSum { get; set; }
			public int Offset { get; set; }
			public int DataLength { get; set; }
			public MemoryStream Data { get; set; }

			public override string ToString() => $"{Tag} - CheckSum={CheckSum:X4} - Offset={Offset} - DataLength={DataLength}";
			public int CompareTo(TableDeclaration other) => Offset.CompareTo(other.Offset);
			public int CompareTo(object obj) => obj is TableDeclaration td ? CompareTo(td) : -1;
		}
	}
}
