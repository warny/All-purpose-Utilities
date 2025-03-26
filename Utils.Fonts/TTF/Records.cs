using System;

namespace Utils.Fonts.TTF;

/// <summary>
/// Represents a point in the TrueType glyph space.
/// </summary>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="OnCurve">Indicates whether the point is on the curve.</param>
public record TTFPoint(float X, float Y, bool OnCurve)
{
	/// <summary>
	/// Calculates the midpoint between two TrueType points.
	/// The resulting point is marked as OnCurve.
	/// </summary>
	/// <param name="p1">The first point.</param>
	/// <param name="p2">The second point.</param>
	/// <returns>A new <see cref="TTFPoint"/> representing the midpoint.</returns>
	public static TTFPoint MidPoint(TTFPoint p1, TTFPoint p2)
		=> new TTFPoint((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, true);
}

/// <summary>
/// Represents a record from the 'loca' table of a TrueType font.
/// </summary>
/// <param name="Index">The record index.</param>
/// <param name="Offset">The offset of the record in the file.</param>
/// <param name="Size">The size of the record.</param>
public record LocaRecord(int Index, int Offset, int Size);
