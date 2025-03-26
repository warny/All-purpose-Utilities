using System;
using System.Collections.Generic;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables
{
	/// <summary>
	/// Represents the 'kern' table which contains kerning pairs for adjusting the spacing between glyphs in a TrueType font.
	/// This implementation supports the most common format (version 0) with a single horizontal kerning subtable.
	/// </summary>
	[TTFTable(TableTypes.Tags.KERN)]
	public class KernTable : TrueTypeTable
	{
		// Dictionary mapping (leftGlyph, rightGlyph) pairs to a kerning adjustment value.
		private Dictionary<(ushort left, ushort right), short> kerningPairs;

		/// <summary>
		/// Initializes a new instance of the <see cref="KernTable"/> class.
		/// </summary>
		public KernTable() : base(TableTypes.KERN)
		{
			kerningPairs = new Dictionary<(ushort, ushort), short>();
		}

		/// <summary>
		/// Retrieves the spacing correction (kerning value) between two glyphs.
		/// In this simplified implementation, the input characters are treated as glyph indices.
		/// </summary>
		/// <param name="before">The glyph index of the preceding character.</param>
		/// <param name="after">The glyph index of the following character.</param>
		/// <returns>The kerning adjustment in font units, or 0 if no kerning pair is found.</returns>
		public float GetSpacingCorrection(char before, char after)
		{
			ushort left = (ushort)before;
			ushort right = (ushort)after;
			if (kerningPairs.TryGetValue((left, right), out short value))
			{
				return value;
			}
			return 0f;
		}

		/// <inheritdoc/>
		public override void ReadData(Reader data)
		{
			// Read the kern table header.
			// Header: version (uint16) and number of subtables (uint16)
			ushort version = data.ReadUInt16(true);
			ushort nTables = data.ReadUInt16(true);

			// For simplicity, we only support one subtable.
			// Read subtable header: subVersion (uint16), length (uint16), coverage (uint16)
			ushort subVersion = data.ReadUInt16(true);
			ushort subtableLength = data.ReadUInt16(true);
			ushort coverage = data.ReadUInt16(true);

			// Next field: number of kerning pairs (uint16)
			ushort nPairs = data.ReadUInt16(true);
			// Skip searchRange, entrySelector, and rangeShift (3*uint16)
			data.ReadUInt16(true);
			data.ReadUInt16(true);
			data.ReadUInt16(true);

			kerningPairs = new Dictionary<(ushort, ushort), short>(nPairs);
			for (int i = 0; i < nPairs; i++)
			{
				ushort leftGlyph = data.ReadUInt16(true);
				ushort rightGlyph = data.ReadUInt16(true);
				short adjustment = data.ReadInt16(true);
				kerningPairs[(leftGlyph, rightGlyph)] = adjustment;
			}
		}

		/// <inheritdoc/>
		public override void WriteData(Writer data)
		{
			// Write kern table header: version = 0, nTables = 1.
			data.WriteUInt16(0, true);
			data.WriteUInt16(1, true);

			// Calculate subtable length: header (14 bytes) + 6 bytes per pair.
			ushort nPairs = (ushort)kerningPairs.Count;
			ushort subtableLength = (ushort)(14 + nPairs * 6);

			// Write subtable header.
			data.WriteUInt16(0, true);              // subVersion = 0
			data.WriteUInt16(subtableLength, true); // subtable length
			data.WriteUInt16(0, true);              // coverage (assumed horizontal, no cross-stream kerning)

			data.WriteUInt16(nPairs, true);         // number of pairs
													// For simplicity, compute searchRange, entrySelector, and rangeShift as follows:
			ushort searchRange = (ushort)(Math.Pow(2, Math.Floor(Math.Log(nPairs, 2))) * 6);
			ushort entrySelector = (ushort)Math.Log(Math.Pow(2, Math.Floor(Math.Log(nPairs, 2))), 2);
			ushort rangeShift = (ushort)(nPairs * 6 - searchRange);
			data.WriteUInt16(searchRange, true);
			data.WriteUInt16(entrySelector, true);
			data.WriteUInt16(rangeShift, true);

			// Write each kerning pair.
			foreach (var pair in kerningPairs)
			{
				data.WriteUInt16(pair.Key.left, true);
				data.WriteUInt16(pair.Key.right, true);
				data.WriteInt16(pair.Value, true);
			}
		}

		/// <inheritdoc/>
		public override int Length
		{
			get {
				// Total length: 4 bytes (table header) + 14 bytes (subtable header) + 6 bytes per kerning pair.
				return 4 + 14 + kerningPairs.Count * 6;
			}
		}
	}
}
