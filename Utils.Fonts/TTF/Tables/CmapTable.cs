using System;
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;
using Utils.Fonts.TTF;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// Cmap Table
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6cmap.html" />
[TTFTable(TrueTypeTableTypes.Tags.cmap)]
public class CmapTable : TrueTypeTable
{
	public sealed class CmapSubtable : IEquatable<CmapSubtable>, IComparable<CmapSubtable>
	{
		public short PlatformID { get; }
		public short PlatformSpecificID { get; }

		internal CmapSubtable(short platformID, short platformSpecificID)
		{
			this.PlatformID = platformID;
			this.PlatformSpecificID = platformSpecificID;
		}

		public override bool Equals(object obj) => obj is CmapSubtable other && Equals(other);
		public bool Equals(CmapSubtable other) => PlatformID == other.PlatformID && PlatformSpecificID == other.PlatformSpecificID;
		public override int GetHashCode() => Objects.ObjectUtils.ComputeHash(PlatformID, PlatformSpecificID);

		public int CompareTo([AllowNull] CmapSubtable other)
		{
			if (other == null) return 1;
			if (this.Equals(other)) return 0;

			if (this.PlatformID == 3 && this.PlatformSpecificID == 1) return -1;
			if (other.PlatformID == 3 && other.PlatformSpecificID == 1) return 1;

			if (this.PlatformSpecificID == 1 && this.PlatformSpecificID == 0) return -1;
			if (other.PlatformSpecificID == 1 && other.PlatformSpecificID == 0) return -1;

			return new int[] {
				this.PlatformID.CompareTo(other.PlatformID),
				this.PlatformSpecificID.CompareTo(other.PlatformSpecificID)
			}.FirstOrDefault(t => t != 0);
		}
	}

	private SortedDictionary<CmapSubtable, CMap.CMapFormatBase> subtables;

	public virtual CMap.CMapFormatBase[] CMaps { get; private set; }

	public virtual CMap.CMapFormatBase GetCMap(short platformID, short platformSpecificID)
		=> subtables.GetValueOrDefault(new CmapSubtable(platformID, platformSpecificID));

	public virtual void AddCMap(short platformID, short platformSpecificID, CMap.CMapFormatBase cm) {
		subtables.Add(new CmapSubtable(platformID, platformSpecificID), cm);
		SortMaps();
	}

	public virtual void RemoveCMap(short platformID, short platformSpecificID) {
		subtables.Remove(new CmapSubtable(platformID, platformSpecificID));
		SortMaps();
	}


	protected internal CmapTable() : base(TrueTypeTableTypes.cmap)
	{
		Version = 0;
		subtables = new SortedDictionary<CmapSubtable, CMap.CMapFormatBase>();
	}

	public override int Length
	{
		get {
			int num = 4;
			num += subtables.Count * 8;
			foreach (var cMap in subtables)
			{
				num += cMap.Value.Length;
			}
			return num;
		}
	}

	public virtual short Version { get; set; }

	public virtual short NumberSubtables => (short)subtables.Count;

	public override void ReadData(Reader data)
	{
		Version = data.ReadInt16(true);
		int numberSubtables = data.ReadInt16(true);

		var subTables = new (short platformID, short platformSpecificID, int offset, int length)[numberSubtables];

		int lastOffset = 0;
		for (int i = 0; i < numberSubtables; i++)
		{
			var platformID = data.ReadInt16(true);
			var platformSpecificID = data.ReadInt16(true);
			var offset = data.ReadInt32(true);

			subTables[i] = (platformID, platformSpecificID, offset, offset - lastOffset);
			lastOffset = offset;
		}

		for (int i = 0; i < numberSubtables; i++)
		{
			var subTable = subTables[i];
			Reader mapData = data.Slice(subTable.offset, subTable.length);
			try
			{
				CMap.CMapFormatBase cMap = CMap.CMapFormatBase.GetMap(mapData);
				if (cMap is not null)
				{
					AddCMap(subTable.platformID, subTable.platformSpecificID, cMap);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reading map.  PlatformID={subTable.platformID}, PlatformSpecificID={subTable.platformSpecificID}");
				Console.WriteLine($"Reason: {ex.Message}");
			}
		}

		SortMaps();
	}

	private void SortMaps()
	{
		CMaps = subtables.Values.ToArray();
	}

	public override void WriteData(Writer data)
	{
		data.WriteInt16(Version, true);
		data.WriteInt16(NumberSubtables, true);
		int length = 4 + NumberSubtables * 8;
		foreach (var subTable in subtables) {
			CmapSubtable cmapSubtable = subTable.Key;
			CMap.CMapFormatBase cMap = subTable.Value;
			data.WriteInt16(cmapSubtable.PlatformID, true);
			data.WriteInt16(cmapSubtable.PlatformSpecificID, true);
			data.WriteInt32(length, true);
			length += cMap.Length;
		}
		foreach (var cMap2 in subtables) {
			cMap2.Value.WriteData(data);
		}
	}

	public override string ToString()
	{
		StringBuilder val = new StringBuilder();
		val.AppendLine($"    Version: {Version:X2}");
		val.AppendLine($"    NumMaps: {NumberSubtables}");
		foreach (var subTable in subtables) {
			var cmapSubtable = subTable.Key;
			var cMap = subTable.Value;
			val.Append($"    Map: platformID: {cmapSubtable.PlatformID} - PlatformSpecificID: {cmapSubtable.PlatformSpecificID} - ");
			val.AppendLine(cMap.ToString());
		}
		return val.ToString();
	}
}

