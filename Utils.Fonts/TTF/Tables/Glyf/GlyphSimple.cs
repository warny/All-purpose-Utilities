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
public class GlyphSimple : GlyphBase
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
    internal GlyphSimple() { }

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
        // Coordinates are stored as deltas from the previous point (both here and on the wire),
        // not as absolute values, so the byte/short decision below must be based on the delta.
        short lastX = 0;
        short lastY = 0;

        foreach (var point in points)
        {
            OutlineFlags flag = OutlineFlags.None;
            int dx = point.X - lastX;
            int dy = point.Y - lastY;

            // Per the TrueType spec, when the "IsByte" flag is set, "IsSame" doubles as the sign
            // bit of the byte-encoded delta (set = positive, clear = negative) rather than
            // meaning "unchanged".
            if (dx == 0)
            {
                flag |= OutlineFlags.XIsSame;
            }
            else if (dx is >= -0xFF and <= 0xFF)
            {
                flag |= OutlineFlags.XIsByte;
                if (dx > 0) flag |= OutlineFlags.XIsSame;
            }

            if (dy == 0)
            {
                flag |= OutlineFlags.YIsSame;
            }
            else if (dy is >= -0xFF and <= 0xFF)
            {
                flag |= OutlineFlags.YIsByte;
                if (dy > 0) flag |= OutlineFlags.YIsSame;
            }

            flag |= point.onCurve ? OutlineFlags.OnCurve : 0;

            yield return flag;
            lastX = point.X;
            lastY = point.Y;
        }
    }

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        if (NumContours == 0)
        {
            contours = [];
            return;
        }

        // Read contour end points and adjust them (TTF spec: end point index + 1).
        var contourEndPoints = data.ReadArray<short>(NumContours, true)
                                   .Select(nc => nc + 1)
                                   .ToArray();
        int numPoints = contourEndPoints[contourEndPoints.Length - 1];

        // Read instructions
        int length = data.Read<Int16>();
        Instructions = data.ReadArray<byte>(length);

        // Read flags for each point, handling repeats.
        OutlineFlags[] flags = new OutlineFlags[numPoints];
        for (int i = 0; i < flags.Length; i++)
        {
            OutlineFlags flag = (OutlineFlags)data.Read<Byte>();
            flags[i] = flag;
            if ((flag & OutlineFlags.Repeat) != 0)
            {
                int n = data.Read<Byte>();
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
                    int val = data.Read<Byte>();
                    if (!flag.HasFlag(isSame))
                    {
                        val = -val;
                    }
                    result[i] = (short)(result[i] + val);
                }
                else if (!flag.HasFlag(isSame))
                {
                    result[i] += data.Read<Int16>();
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
    public override void WriteData(Writer data)
    {
        var points = EnumerableEx.Flatten(contours).ToArray();
        var flags = GetFlags(points).ToArray();
        var compactFlags = EnumerableEx.Pack(flags);

        // Write the basic glyph header data.
        base.WriteData(data);

        // Write the end-point index (0-based, i.e. point count - 1) of each contour: ReadData
        // adds 1 back to recover the point count, per the TrueType endPtsOfContours convention.
        for (int i = 0; i < NumContours; i++)
        {
            data.Write<Int16>((short)(contours[i].Length - 1));
        }

        // Write instruction count and instructions.
        data.Write<Int16>(InstructionsCount);
        for (int i = 0; i < InstructionsCount; i++)
        {
            data.Write<Byte>(GetInstruction(i));
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

        // Local function to write coordinate deltas based on flags. Mirrors ReadData's
        // "coordinates" local function exactly, since coordinates are deltas from the previous
        // point, not absolute values.
        void Write(OutlineFlags isByte, OutlineFlags isSame, Func<(float X, float Y, bool OnCurve), short> getValue)
        {
            short previous = 0;
            for (int i = 0; i < PointsCount; i++)
            {
                OutlineFlags flag = flags[i];
                short value = getValue(points[i]);
                int delta = value - previous;
                if (flag.HasFlag(isByte))
                {
                    // Magnitude fits a byte; isSame doubles as the sign bit (set = positive).
                    data.WriteByte((byte)(flag.HasFlag(isSame) ? delta : -delta));
                }
                else if (!flag.HasFlag(isSame))
                {
                    data.Write<Int16>((short)delta);
                }
                // else: isSame set without isByte means the coordinate is unchanged, nothing to write.
                previous = value;
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
        get
        {
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
