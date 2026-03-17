using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'avar' (Axis Variations) table maps normalized design-axis coordinates through per-axis
/// piecewise-linear segment maps, allowing non-linear axis progressions and ensuring that certain
/// canonical normalized coordinates (−1, 0, +1) map to themselves.
/// </summary>
/// <remarks>
/// <para>Coordinates in this table are stored as F2Dot14 (signed 2.14 fixed-point, i.e. Int16 / 16 384.0).
/// For example 0x4000 = 1.0, 0xC000 = –1.0, 0x0000 = 0.0.</para>
/// <para>Version 1.0 contains one <see cref="SegmentMap"/> per axis in the same order as 'fvar'.</para>
/// </remarks>
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/avar"/>
[TTFTable(TableTypes.Tags.AVAR)]
public class AvarTable : TrueTypeTable
{
    // ── Nested types ──────────────────────────────────────────────────────

    /// <summary>
    /// A single input→output coordinate mapping entry within a <see cref="SegmentMap"/>.
    /// Both values are F2Dot14 (divide by 16 384 to obtain the real normalized value).
    /// </summary>
    public readonly record struct AxisValueMap(short FromCoord, short ToCoord);

    /// <summary>
    /// The piecewise-linear segment map for one design axis.
    /// Must include mappings for –1→–1, 0→0 and +1→+1 as required by the spec.
    /// </summary>
    public sealed class SegmentMap
    {
        /// <summary>Gets or sets the ordered list of (from, to) coordinate pairs.</summary>
        public AxisValueMap[] Mappings { get; set; } = [];
    }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="AvarTable"/> class.</summary>
    public AvarTable() : base(TableTypes.AVAR) { }

    // ── Public properties ─────────────────────────────────────────────────

    /// <summary>Gets or sets the major version of the table (must be 1).</summary>
    public ushort MajorVersion { get; set; } = 1;

    /// <summary>Gets or sets the minor version of the table (must be 0).</summary>
    public ushort MinorVersion { get; set; } = 0;

    /// <summary>
    /// Gets or sets the segment maps, one per axis in the same order as the 'fvar' table.
    /// An axis with no remapping should still have its identity mapping entries (−1,−1), (0,0), (+1,+1).
    /// </summary>
    public SegmentMap[] SegmentMaps { get; set; } = [];

    // ── F2Dot14 helpers ───────────────────────────────────────────────────

    /// <summary>Converts an F2Dot14 raw short to a <see cref="double"/>.</summary>
    public static double F2Dot14ToDouble(short value) => value / 16384.0;

    /// <summary>Converts a <see cref="double"/> to an F2Dot14 raw short.</summary>
    public static short DoubleToF2Dot14(double value)
        => (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)Math.Round(value * 16384.0)));

    // ── Length ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override int Length
    {
        get
        {
            // 8-byte header: majorVersion(2) + minorVersion(2) + reserved(2) + axisCount(2)
            int size = 8;
            foreach (var sm in SegmentMaps)
                size += 2 + sm.Mappings.Length * 4;  // positionMapCount(2) + entries(4 each)
            return size;
        }
    }

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        MajorVersion = data.Read<UInt16>();
        MinorVersion = data.Read<UInt16>();
        data.Read<UInt16>(); // reserved
        int axisCount = data.Read<UInt16>();

        SegmentMaps = new SegmentMap[axisCount];
        for (int i = 0; i < axisCount; i++)
        {
            int posMapCount = data.Read<UInt16>();
            var mappings = new AxisValueMap[posMapCount];
            for (int m = 0; m < posMapCount; m++)
            {
                short from = data.Read<Int16>();
                short to   = data.Read<Int16>();
                mappings[m] = new AxisValueMap(from, to);
            }
            SegmentMaps[i] = new SegmentMap { Mappings = mappings };
        }
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<UInt16>(MajorVersion);
        data.Write<UInt16>(MinorVersion);
        data.Write<UInt16>(0);  // reserved
        data.Write<UInt16>((ushort)SegmentMaps.Length);

        foreach (var sm in SegmentMaps)
        {
            data.Write<UInt16>((ushort)sm.Mappings.Length);
            foreach (var (from, to) in sm.Mappings)
            {
                data.Write<Int16>(from);
                data.Write<Int16>(to);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version : {MajorVersion}.{MinorVersion}");
        sb.AppendLine($"    Axes    : {SegmentMaps.Length}");
        for (int i = 0; i < SegmentMaps.Length; i++)
            sb.AppendLine($"      Axis[{i}]: {SegmentMaps[i].Mappings.Length} mappings");
        return sb.ToString();
    }
}
