using System;
using System.Collections.Generic;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// Cmap Table
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6cmap.html" />
[TTFTable(TrueTypeTableTypes.Tags.cmap)]
public class CmapTable : TrueTypeTable
{
	public sealed class CmapSubtable : IEquatable<CmapSubtable>
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
	}

	private Dictionary<CmapSubtable, CMap.CMapFormat> subtables;

	public virtual CMap.CMapFormat[] CMaps
	{
		get
		{
			List<CMap.CMapFormat> c = new List<CMap.CMapFormat>();

			CMap.CMapFormat cmap_3_1 = GetCMap(3, 1);
			if (cmap_3_1 is not null) { c.Add(cmap_3_1); }
			CMap.CMapFormat cmap_1_0 = GetCMap(1, 0);
			if (cmap_1_0 is not null) { c.Add(cmap_1_0); }

			foreach (var cmap in subtables.Values)
			{
				if (!c.Contains(cmap)) { c.Add(cmap); }
			}

			return c.ToArray();
		}
	}

	public virtual CMap.CMapFormat GetCMap(short platformID, short platformSpecificID)
		=> (CMap.CMapFormat)subtables[new CmapSubtable(platformID, platformSpecificID)];

	public virtual void AddCMap(short platformID, short platformSpecificID, CMap.CMapFormat cm)
		=> subtables.Add(new CmapSubtable(platformID, platformSpecificID), cm);

	public virtual void RemoveCMap(short platformID, short platformSpecificID)
		=> subtables.Remove(new CmapSubtable(platformID, platformSpecificID));


	protected internal CmapTable() : base(TrueTypeTableTypes.cmap)
	{
		Version = 0;
		subtables = new Dictionary<CmapSubtable, CMap.CMapFormat>();
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
		for (int i = 0; i < numberSubtables; i++)
		{
			short platformID = data.ReadInt16(true);
			short platformSpecificID = data.ReadInt16(true);
			int offset = data.ReadInt32(true);
			Reader mapData = data.Slice(offset, data.BytesLeft);
			try
			{
				CMap.CMapFormat cMap = CMap.CMapFormat.GetMap(mapData);
				if (cMap is not null)
				{
					AddCMap(platformID, platformSpecificID, cMap);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reading map.  PlatformID={platformID}, PlatformSpecificID={platformSpecificID}");
				Console.WriteLine($"Reason: {ex.Message}");
			}
		}
	}

	public override void WriteData(Writer data)
	{
		data.WriteInt16(Version, true);
		data.WriteInt16(NumberSubtables, true);
		int length = 4 + NumberSubtables * 8;
		foreach (var subTable in subtables) {
			CmapSubtable cmapSubtable = subTable.Key;
			CMap.CMapFormat cMap = subTable.Value;
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
		val.AppendLine($"    Version: {(int)Version}");
		val.AppendLine($"    NumMaps: {(int)NumberSubtables}");
		foreach (var subTable in subtables) {
			var cmapSubtable = subTable.Key;
			var cMap = subTable.Value;
			val.Append($"    Map: platformID: {cmapSubtable.PlatformID} - PlatformSpecificID: {cmapSubtable.PlatformSpecificID} - ");
			val.AppendLine(cMap.ToString());
		}
		return val.ToString();
	}
}

