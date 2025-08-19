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
			ushort version = data.Read<UInt16>();
			ushort nTables = data.Read<UInt16>();

			// For simplicity, we only support one subtable.
			// Read subtable header: subVersion (uint16), length (uint16), coverage (uint16)
			ushort subVersion = data.Read<UInt16>();
			ushort subtableLength = data.Read<UInt16>();
			ushort coverage = data.Read<UInt16>();

			// Next field: number of kerning pairs (uint16)
			ushort nPairs = data.Read<UInt16>();
			// Skip searchRange, entrySelector, and rangeShift (3*uint16)
			data.Read<UInt16>();
			data.Read<UInt16>();
			data.Read<UInt16>();

			kerningPairs = new Dictionary<(ushort, ushort), short>(nPairs);
			for (int i = 0; i < nPairs; i++)
			{
				ushort leftGlyph = data.Read<UInt16>();
				ushort rightGlyph = data.Read<UInt16>();
				short adjustment = data.Read<Int16>();
				kerningPairs[(leftGlyph, rightGlyph)] = adjustment;
			}
		}

		/// <inheritdoc/>
		public override void WriteData(Writer data)
		{
			// Write kern table header: version = 0, nTables = 1.
			data.Write<UInt16>(0);
			data.Write<UInt16>(1);

			// Calculate subtable length: header (14 bytes) + 6 bytes per pair.
			ushort nPairs = (ushort)kerningPairs.Count;
			ushort subtableLength = (ushort)(14 + nPairs * 6);

			// Write subtable header.
			data.Write<UInt16>(0);              // subVersion = 0
			data.Write<UInt16>(subtableLength); // subtable length
			data.Write<UInt16>(0);              // coverage (assumed horizontal, no cross-stream kerning)

			data.Write<UInt16>(nPairs);         // number of pairs
												// For simplicity, compute searchRange, entrySelector, and rangeShift as follows:
			ushort searchRange = (ushort)(Math.Pow(2, Math.Floor(Math.Log(nPairs, 2))) * 6);
			ushort entrySelector = (ushort)Math.Log(Math.Pow(2, Math.Floor(Math.Log(nPairs, 2))), 2);
			ushort rangeShift = (ushort)(nPairs * 6 - searchRange);
			data.Write<UInt16>(searchRange);
			data.Write<UInt16>(entrySelector);
			data.Write<UInt16>(rangeShift);

			// Write each kerning pair.
			foreach (var pair in kerningPairs)
			{
				data.Write<UInt16>(pair.Key.left);
				data.Write<UInt16>(pair.Key.right);
				data.Write<Int16>(pair.Value);
			}
		}

		/// <inheritdoc/>
		public override int Length =>
				// Total length: 4 bytes (table header) + 14 bytes (subtable header) + 6 bytes per kerning pair.
				4 + 14 + kerningPairs.Count * 6;
	}
}
