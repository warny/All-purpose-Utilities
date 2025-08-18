using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// Represents the 'cmap' table which provides the character-to-glyph mapping for a TrueType font.
/// The table contains one or more subtables (each identified by a platform ID and platform-specific ID)
/// that define different mapping formats.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6cmap.html" />
[TTFTable(TableTypes.Tags.CMAP)]
public class CmapTable : TrueTypeTable, IEnumerable<CMap.CMapFormatBase>
{
	/// <summary>
	/// Represents a subtable identifier defined by a platform ID and platform-specific ID.
	/// Used as a key for the cmap subtables.
	/// </summary>
	public sealed class CmapSubtable : IEquatable<CmapSubtable>, IComparable<CmapSubtable>
	{
		/// <summary>
		/// Gets the platform ID.
		/// </summary>
		public short PlatformID { get; }

		/// <summary>
		/// Gets the platform-specific ID.
		/// </summary>
		public short PlatformSpecificID { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CmapSubtable"/> class.
		/// </summary>
		/// <param name="platformID">The platform ID.</param>
		/// <param name="platformSpecificID">The platform-specific ID.</param>
		internal CmapSubtable(short platformID, short platformSpecificID)
		{
			PlatformID = platformID;
			PlatformSpecificID = platformSpecificID;
		}

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is CmapSubtable other && Equals(other);

		/// <inheritdoc/>
		public bool Equals(CmapSubtable other) =>
			other != null && PlatformID == other.PlatformID && PlatformSpecificID == other.PlatformSpecificID;

		/// <inheritdoc/>
		public override int GetHashCode() => ObjectUtils.ComputeHash(PlatformID, PlatformSpecificID);

		/// <inheritdoc/>
		public int CompareTo([System.Diagnostics.CodeAnalysis.AllowNull] CmapSubtable other)
		{
			if (other is null)
			{
				return 1;
			}
			if (this.Equals(other))
			{
				return 0;
			}

			// Prioritize the Microsoft Unicode subtable (PlatformID = 3, PlatformSpecificID = 1)
			if (this.PlatformID == 3 && this.PlatformSpecificID == 1)
			{
				return -1;
			}
			if (other.PlatformID == 3 && other.PlatformSpecificID == 1)
			{
				return 1;
			}

			// Additional tie-breakers can be added here if needed.
			int comparePlatform = this.PlatformID.CompareTo(other.PlatformID);
			if (comparePlatform != 0)
			{
				return comparePlatform;
			}
			return this.PlatformSpecificID.CompareTo(other.PlatformSpecificID);
		}
	}

	private SortedDictionary<CmapSubtable, CMap.CMapFormatBase> subtables;

	/// <summary>
	/// Gets the array of cmap subtables.
	/// </summary>
	public virtual CMap.CMapFormatBase[] CMaps { get; private set; }

	/// <summary>
	/// Gets a cmap subtable based on the specified platform ID and platform-specific ID.
	/// </summary>
	/// <param name="platformID">The platform ID.</param>
	/// <param name="platformSpecificID">The platform-specific ID.</param>
	/// <returns>The corresponding cmap format subtable if found; otherwise, null.</returns>
	public virtual CMap.CMapFormatBase GetCMap(short platformID, short platformSpecificID)
		=> subtables.GetValueOrDefault(new CmapSubtable(platformID, platformSpecificID));

	/// <summary>
	/// Adds a cmap subtable to the table.
	/// </summary>
	/// <param name="platformID">The platform ID.</param>
	/// <param name="platformSpecificID">The platform-specific ID.</param>
	/// <param name="cm">The cmap format subtable.</param>
	public virtual void AddCMap(short platformID, short platformSpecificID, CMap.CMapFormatBase cm)
	{
		subtables.Add(new CmapSubtable(platformID, platformSpecificID), cm);
		SortMaps();
	}

	/// <summary>
	/// Removes a cmap subtable based on the specified platform IDs.
	/// </summary>
	/// <param name="platformID">The platform ID.</param>
	/// <param name="platformSpecificID">The platform-specific ID.</param>
	public virtual void RemoveCMap(short platformID, short platformSpecificID)
	{
		subtables.Remove(new CmapSubtable(platformID, platformSpecificID));
		SortMaps();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CmapTable"/> class.
	/// </summary>
	protected internal CmapTable() : base(TableTypes.CMAP)
	{
		Version = 0;
		subtables = new SortedDictionary<CmapSubtable, CMap.CMapFormatBase>();
	}

	/// <summary>
	/// Gets or sets the version of the cmap table.
	/// </summary>
	public virtual short Version { get; set; }

	/// <summary>
	/// Gets the number of cmap subtables.
	/// </summary>
	public virtual short NumberSubtables => (short)subtables.Count;

	/// <summary>
	/// Gets the total length (in bytes) of the cmap table data.
	/// </summary>
	public override int Length
	{
		get {
			int num = 4; // version (2 bytes) + numberSubtables (2 bytes)
			num += subtables.Count * 8; // Each subtable record is 8 bytes.
			foreach (var cMap in subtables)
			{
				num += cMap.Value.Length;
			}
			return num;
		}
	}

	/// <inheritdoc/>
	public override void ReadData(NewReader data)
	{
		Version = data.ReadInt16(true);
		int numberSubtables = data.ReadInt16(true);

		// Read subtable directory records.
		var subTables = new (short platformID, short platformSpecificID, int offset, int length)[numberSubtables];
		int lastOffset = 0;
		for (int i = 0; i < numberSubtables; i++)
		{
			var platformID = data.ReadInt16(true);
			var platformSpecificID = data.ReadInt16(true);
			var offset = data.ReadInt32(true);
			// Length is calculated as the difference between current and previous offset.
			subTables[i] = (platformID, platformSpecificID, offset, offset - lastOffset);
			lastOffset = offset;
		}

		// Read each subtable.
		for (int i = 0; i < numberSubtables; i++)
		{
			var subTable = subTables[i];
			NewReader mapData = data.Slice(subTable.offset, subTable.length);
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
				Console.WriteLine($"Error reading cmap subtable. PlatformID={subTable.platformID}, PlatformSpecificID={subTable.platformSpecificID}");
				Console.WriteLine($"Reason: {ex.Message}");
			}
		}

		SortMaps();
	}

	/// <summary>
	/// Writes the cmap table data to the specified writer.
	/// </summary>
	/// <param name="data">The writer to which the data is written.</param>
	public override void WriteData(NewWriter data)
	{
		data.WriteInt16(Version, true);
		data.WriteInt16(NumberSubtables, true);
		int length = 4 + NumberSubtables * 8;
		foreach (var subTable in subtables)
		{
			CmapSubtable cmapSubtable = subTable.Key;
			CMap.CMapFormatBase cMap = subTable.Value;
			data.WriteInt16(cmapSubtable.PlatformID, true);
			data.WriteInt16(cmapSubtable.PlatformSpecificID, true);
			data.WriteInt32(length, true);
			length += cMap.Length;
		}
		foreach (var cMapEntry in subtables)
		{
			cMapEntry.Value.WriteData(data);
		}
	}

	/// <summary>
	/// Returns a string representation of the cmap table.
	/// </summary>
	/// <returns>A string describing the cmap table details.</returns>
	public override string ToString()
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine($"    Version: {Version:X2}");
		sb.AppendLine($"    NumMaps: {NumberSubtables}");
		foreach (var subTable in subtables)
		{
			var cmapSubtable = subTable.Key;
			var cMap = subTable.Value;
			sb.Append($"    Map: platformID: {cmapSubtable.PlatformID} - PlatformSpecificID: {cmapSubtable.PlatformSpecificID} - ");
			sb.AppendLine(cMap.ToString());
		}
		return sb.ToString();
	}

	/// <summary>
	/// Sorts the cmap subtables and stores them in the CMaps array.
	/// </summary>
	private void SortMaps() => CMaps = subtables.Values.ToArray();

	/// <inheritdoc/>
	public IEnumerator<CMap.CMapFormatBase> GetEnumerator() => ((IEnumerable<CMap.CMapFormatBase>)CMaps).GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
