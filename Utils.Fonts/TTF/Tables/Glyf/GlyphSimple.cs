using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Collections;
using Utils.IO.Serialization;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables.Glyf;

/// <summary>
/// Represents a simple (non‑compound) glyph in a TrueType font.
/// </summary>
public abstract class GlyphSimple : GlyphBase
{
	// Each contour is an array of points (with X, Y coordinates and a flag indicating whether the point is on the curve).
	private (short X, short Y, bool onCurve)[][] contours;

	/// <summary>
	/// Gets or sets the bytecode instructions for the glyph.
	/// </summary>
	protected internal byte[] Instructions { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="GlyphSimple"/> class.
	/// </summary>
	private protected GlyphSimple() { }

	/// <inheritdoc/>
	public override bool IsCompound => false;

	/// <summary>
	/// Gets the total number of points in all contours.
	/// </summary>
	public virtual short PointsCount => (short)contours.Sum(c => c.Length);

	/// <summary>
	/// Gets the contours of the glyph.
	/// Each contour is represented as a sequence of <see cref="TTFPoint"/> instances.
	/// </summary>
	public override IEnumerable<IEnumerable<TTFPoint>> Contours =>
		contours.Select(c => c.Select(p => new TTFPoint(p.X, p.Y, p.onCurve)));

	/// <summary>
	/// Gets the instruction at the specified index.
	/// </summary>
	/// <param name="i">The zero-based index of the instruction.</param>
	/// <returns>The instruction as a byte.</returns>
	public virtual byte GetInstruction(int i) => Instructions[i];

	/// <summary>
	/// Gets the total number of instructions.
	/// </summary>
	public virtual short InstructionsCount => (short)Instructions.Length;

	/// <summary>
	/// Computes the outline flags for all points in the glyph.
	/// </summary>
	/// <returns>An enumerable of <see cref="OutlineFlags"/> corresponding to each point.</returns>
	private IEnumerable<OutlineFlags> GetFlags()
	{
		var points = EnumerableEx.Flatten(contours).ToArray();
		return GetFlags(points);
	}

	/// <summary>
	/// Computes the outline flags for the specified array of points.
	/// </summary>
	/// <param name="points">An array of points represented as tuples (X, Y, onCurve).</param>
	/// <returns>An enumerable of <see cref="OutlineFlags"/> for each point.</returns>
	private static IEnumerable<OutlineFlags> GetFlags((short X, short Y, bool onCurve)[] points)
	{
		// Use nullable shorts for the previous point coordinates.
		(short? X, short? Y, bool onCurve) lastPoint = (null, null, false);

		// Convert each point to a tuple of floats and compute flags.
		foreach (var point in points.Select(p => (X: (float)p.X, Y: (float)p.Y, p.onCurve)))
		{
			OutlineFlags flag = OutlineFlags.None;

			if (point.X == lastPoint.X)
			{
				flag |= OutlineFlags.XIsSame;
			}
			else if (point.X.Between(0, 0xFF))
			{
				flag |= OutlineFlags.XIsByte;
			}
			else if (point.X.Between(-0xFF, 0))
			{
				flag |= OutlineFlags.XIsByte;
				flag |= OutlineFlags.XIsSame;
			}

			if (point.Y == lastPoint.Y)
			{
				flag |= OutlineFlags.YIsSame;
			}
			else if (point.Y.Between(0, 0xFF))
			{
				flag |= OutlineFlags.YIsByte;
			}
			else if (point.Y.Between(-0xFF, 0))
			{
				flag |= OutlineFlags.YIsByte;
				flag |= OutlineFlags.YIsSame;
			}

			flag |= point.onCurve ? OutlineFlags.OnCurve : 0;

			yield return flag;
			// Update lastPoint for next iteration.
			lastPoint = ((short)point.X, (short)point.Y, point.onCurve);
		}
	}

	/// <inheritdoc/>
	public override void ReadData(NewReader data)
	{
		// Read contour end points and adjust them (TTF spec: end point index + 1).
		var contourEndPoints = data.ReadArray<short>(NumContours, true)
								   .Select(nc => nc + 1)
								   .ToArray();
		int numPoints = contourEndPoints[contourEndPoints.Length - 1];

		// Read instructions
		int length = data.ReadInt16(true);
		Instructions = data.ReadArray<byte>(length);

		// Read flags for each point, handling repeats.
		OutlineFlags[] flags = new OutlineFlags[numPoints];
		for (int i = 0; i < flags.Length; i++)
		{
			OutlineFlags flag = (OutlineFlags)data.ReadByte();
			flags[i] = flag;
			if ((flag & OutlineFlags.Repeat) != 0)
			{
				int n = data.ReadByte();
				for (int l = 0; l < n; l++)
				{
					i++;
					flags[i] = flag;
				}
			}
		}

		// Local function to read coordinate differences.
		short[] coordinates(OutlineFlags isByte, OutlineFlags isSame)
		{
			short[] result = new short[numPoints];
			for (int i = 0; i < numPoints; i++)
			{
				OutlineFlags flag = flags[i];
				if (i > 0)
				{
					result[i] = result[i - 1];
				}
				if (flag.HasFlag(isByte))
				{
					int val = data.ReadByte();
					if (!flag.HasFlag(isSame))
					{
						val = -val;
					}
					result[i] = (short)(result[i] + val);
				}
				else if (!flag.HasFlag(isSame))
				{
					result[i] += data.ReadInt16(true);
				}
			}
			return result;
		}

		var xCoords = coordinates(OutlineFlags.XIsByte, OutlineFlags.XIsSame);
		var yCoords = coordinates(OutlineFlags.YIsByte, OutlineFlags.YIsSame);

		// Combine X, Y coordinates with the on-curve flag.
		var points = EnumerableEx.Zip(xCoords, yCoords, flags,
			(x, y, flag) => (x, y, flag.HasFlag(OutlineFlags.OnCurve)));

		// Slice the points array into contours based on the end points.
		contours = EnumerableEx.Slice(points, contourEndPoints)
							   .Select(p => p.ToArray())
							   .ToArray();
	}

	/// <inheritdoc/>
	public override void WriteData(NewWriter data)
	{
		var points = EnumerableEx.Flatten(contours).ToArray();
		var flags = GetFlags(points).ToArray();
		var compactFlags = EnumerableEx.Pack(flags);

		// Write the basic glyph header data.
		base.WriteData(data);

		// Write the number of points per contour.
		for (int i = 0; i < NumContours; i++)
		{
			data.WriteInt16((short)contours[i].Length, true);
		}

		// Write instruction count and instructions.
		data.WriteInt16(InstructionsCount, true);
		for (int i = 0; i < InstructionsCount; i++)
		{
			data.WriteByte(GetInstruction(i));
		}

		// Write the flags in a compact format.
		foreach (var flag in compactFlags)
		{
			var repetition = flag.Repetition;
			while (repetition > 0)
			{
				if (repetition > 1)
				{
					data.WriteByte((byte)(flag.Value | OutlineFlags.Repeat));
					data.WriteByte((byte)MathEx.Min(255, flag.Repetition - 1));
				}
				else
				{
					data.WriteByte((byte)(flag.Value));
				}
				repetition -= 255;
			}
		}

		// Local function to write coordinates based on flags.
		void Write(OutlineFlags isByte, OutlineFlags isSame, Func<(float X, float Y, bool OnCurve), short> getValue)
		{
			for (int i = 0; i < PointsCount; i++)
			{
				OutlineFlags flag = flags[i];
				short value = getValue(points[i]);
				if (flag.HasFlag(isByte))
				{
					if (flag.HasFlag(isSame))
					{
						data.WriteInt16(value, true);
					}
				}
				else if (flag.HasFlag(isSame))
				{
					data.WriteByte((byte)(-value));
				}
				else
				{
					data.WriteByte((byte)value);
				}
			}
		}

		Write(OutlineFlags.XIsByte, OutlineFlags.XIsSame, p => (short)p.X);
		Write(OutlineFlags.YIsByte, OutlineFlags.YIsSame, p => (short)p.Y);
	}

	/// <summary>
	/// Gets the total length (in bytes) of the glyph data.
	/// </summary>
	public override short Length
	{
		get {
			var flags = GetFlags().ToArray();
			var compactFlags = EnumerableEx.Pack(flags);

			int length = base.Length;
			length += NumContours * 2;               // Contour endpoint values
			length += 2 + InstructionsCount;         // Instruction length field and instructions
			length += compactFlags.Count();          // Packed flags
			for (int i = 0; i < PointsCount; i++)
			{
				OutlineFlags flag = flags[i];
				if ((flag & OutlineFlags.XIsByte) != 0) length++;
				else if ((flag & OutlineFlags.XIsSame) == 0) length += 2;

				if ((flag & OutlineFlags.YIsByte) != 0) length++;
				else if ((flag & OutlineFlags.YIsSame) == 0) length += 2;
			}
			return (short)length;
		}
	}
}
