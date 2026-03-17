using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'fvar' (Font Variations) table defines the design axes and named instances for a variable font.
/// Each axis specifies a minimum, default and maximum design-space coordinate, and each named instance
/// represents a specific position in the design space (e.g. "Regular", "Bold", "Light").
/// </summary>
/// <remarks>
/// Coordinates for axes and instances are stored as 16.16 signed fixed-point values (Int32 / 65 536.0).
/// Use <see cref="FixedToDouble"/> and <see cref="DoubleToFixed"/> for conversion.
/// </remarks>
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/fvar"/>
[TTFTable(TableTypes.Tags.FVAR)]
public class FvarTable : TrueTypeTable
{
    // ── Header constants ──────────────────────────────────────────────────

    /// <summary>Byte offset of the axes array from the start of the table (always 16 for version 1).</summary>
    private const ushort AxesArrayOffset = 16;

    /// <summary>Fixed byte size of one <see cref="VariationAxisRecord"/> on disk.</summary>
    private const ushort AxisRecordSize = 20;

    // ── Nested types ──────────────────────────────────────────────────────

    /// <summary>
    /// Describes one design axis of the variable font.
    /// </summary>
    public sealed class VariationAxisRecord
    {
        /// <summary>
        /// Gets or sets the 4-byte tag identifying this axis (e.g. <c>"wght"</c>, <c>"wdth"</c>, <c>"ital"</c>).
        /// Stored as a 4-character ASCII string.
        /// </summary>
        public string AxisTag { get; set; } = "    ";

        /// <summary>Gets or sets the minimum design-space coordinate (16.16 fixed-point as Int32).</summary>
        public int MinValue { get; set; }

        /// <summary>Gets or sets the default design-space coordinate (16.16 fixed-point as Int32).</summary>
        public int DefaultValue { get; set; }

        /// <summary>Gets or sets the maximum design-space coordinate (16.16 fixed-point as Int32).</summary>
        public int MaxValue { get; set; }

        /// <summary>
        /// Gets or sets the axis flags.
        /// Bit 0 set = axis is hidden (not exposed to the user).
        /// </summary>
        public ushort Flags { get; set; }

        /// <summary>Gets or sets the name-table ID for the axis name string (e.g. "Weight", "Width").</summary>
        public ushort AxisNameID { get; set; }
    }

    /// <summary>
    /// Represents one named instance (a specific point in the design space).
    /// </summary>
    public sealed class InstanceRecord
    {
        /// <summary>Gets or sets the name-table ID for the instance name (e.g. "Regular", "Bold").</summary>
        public ushort SubfamilyNameID { get; set; }

        /// <summary>Gets or sets instance flags (currently always 0).</summary>
        public ushort Flags { get; set; }

        /// <summary>
        /// Gets or sets the design-space coordinates for this instance, one per axis (16.16 fixed-point).
        /// Length must equal <see cref="FvarTable.Axes"/>.<see cref="Array.Length"/>.
        /// </summary>
        public int[] Coordinates { get; set; } = [];

        /// <summary>
        /// Gets or sets the optional PostScript name-table ID for this instance,
        /// or 0xFFFF if not present.
        /// </summary>
        public ushort PostScriptNameID { get; set; } = 0xFFFF;

        /// <summary>When <see langword="true"/> the <see cref="PostScriptNameID"/> field is serialised.</summary>
        public bool HasPostScriptName { get; set; }
    }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="FvarTable"/> class.</summary>
    public FvarTable() : base(TableTypes.FVAR) { }

    // ── Public properties ─────────────────────────────────────────────────

    /// <summary>Gets or sets the major version of the table (must be 1).</summary>
    public ushort MajorVersion { get; set; } = 1;

    /// <summary>Gets or sets the minor version of the table (must be 0).</summary>
    public ushort MinorVersion { get; set; } = 0;

    /// <summary>Gets or sets the array of variation axes.</summary>
    public VariationAxisRecord[] Axes { get; set; } = [];

    /// <summary>Gets or sets the array of named instances.</summary>
    public InstanceRecord[] Instances { get; set; } = [];

    // ── Fixed-point helpers ───────────────────────────────────────────────

    /// <summary>Converts a 16.16 fixed-point Int32 to a <see cref="double"/>.</summary>
    public static double FixedToDouble(int value) => value / 65536.0;

    /// <summary>Converts a <see cref="double"/> to a 16.16 fixed-point Int32.</summary>
    public static int DoubleToFixed(double value) => (int)Math.Round(value * 65536.0);

    // ── Length ────────────────────────────────────────────────────────────

    private int InstanceSize => Instances.Length == 0
        ? 4 + Axes.Length * 4
        : Instances[0].HasPostScriptName
            ? 4 + Axes.Length * 4 + 2
            : 4 + Axes.Length * 4;

    /// <inheritdoc/>
    public override int Length
        => AxesArrayOffset                          // 16-byte header
         + Axes.Length * AxisRecordSize             // axis records (20 bytes each)
         + Instances.Length * InstanceSize;         // instance records (variable)

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        MajorVersion = data.Read<UInt16>();
        MinorVersion = data.Read<UInt16>();

        ushort axesArrayOffset = data.Read<UInt16>();
        data.Read<UInt16>();  // reserved (always 2)
        ushort axisCount    = data.Read<UInt16>();
        ushort axisSize     = data.Read<UInt16>();    // must be 20
        ushort instanceCount = data.Read<UInt16>();
        ushort instanceSize  = data.Read<UInt16>();

        // Move to axes (usually already at position 16)
        data.Push((int)axesArrayOffset, SeekOrigin.Begin);

        Axes = new VariationAxisRecord[axisCount];
        for (int i = 0; i < axisCount; i++)
        {
            // Tag: 4-byte ASCII
            byte[] tagBytes = [
                (byte)data.ReadByte(),
                (byte)data.ReadByte(),
                (byte)data.ReadByte(),
                (byte)data.ReadByte()
            ];
            Axes[i] = new VariationAxisRecord
            {
                AxisTag      = Encoding.ASCII.GetString(tagBytes),
                MinValue     = data.Read<Int32>(),
                DefaultValue = data.Read<Int32>(),
                MaxValue     = data.Read<Int32>(),
                Flags        = data.Read<UInt16>(),
                AxisNameID   = data.Read<UInt16>(),
            };
        }

        // Instance records follow the axes
        bool hasPsName = instanceSize == 4 + axisCount * 4 + 2;

        Instances = new InstanceRecord[instanceCount];
        for (int i = 0; i < instanceCount; i++)
        {
            var inst = new InstanceRecord
            {
                SubfamilyNameID = data.Read<UInt16>(),
                Flags           = data.Read<UInt16>(),
                Coordinates     = new int[axisCount],
                HasPostScriptName = hasPsName,
            };
            for (int a = 0; a < axisCount; a++)
                inst.Coordinates[a] = data.Read<Int32>();
            if (hasPsName)
                inst.PostScriptNameID = data.Read<UInt16>();
            Instances[i] = inst;
        }

        data.Pop();
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        bool hasPsName = Instances.Length > 0 && Instances[0].HasPostScriptName;
        ushort instanceSize = (ushort)(4 + Axes.Length * 4 + (hasPsName ? 2 : 0));

        // 16-byte header
        data.Write<UInt16>(MajorVersion);
        data.Write<UInt16>(MinorVersion);
        data.Write<UInt16>(AxesArrayOffset);
        data.Write<UInt16>(2);                           // reserved
        data.Write<UInt16>((ushort)Axes.Length);
        data.Write<UInt16>(AxisRecordSize);
        data.Write<UInt16>((ushort)Instances.Length);
        data.Write<UInt16>(instanceSize);

        // Axis records (20 bytes each)
        foreach (var axis in Axes)
        {
            byte[] tagBytes = new byte[4];
            byte[] src = Encoding.ASCII.GetBytes(axis.AxisTag ?? "    ");
            for (int i = 0; i < 4; i++)
                tagBytes[i] = i < src.Length ? src[i] : (byte)' ';
            foreach (byte b in tagBytes)
                data.WriteByte(b);

            data.Write<Int32>(axis.MinValue);
            data.Write<Int32>(axis.DefaultValue);
            data.Write<Int32>(axis.MaxValue);
            data.Write<UInt16>(axis.Flags);
            data.Write<UInt16>(axis.AxisNameID);
        }

        // Instance records
        foreach (var inst in Instances)
        {
            data.Write<UInt16>(inst.SubfamilyNameID);
            data.Write<UInt16>(inst.Flags);
            foreach (int coord in inst.Coordinates)
                data.Write<Int32>(coord);
            if (hasPsName)
                data.Write<UInt16>(inst.PostScriptNameID);
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version  : {MajorVersion}.{MinorVersion}");
        sb.AppendLine($"    AxesCount: {Axes.Length}");
        foreach (var a in Axes)
            sb.AppendLine($"      [{a.AxisTag}] min={FixedToDouble(a.MinValue):F2} def={FixedToDouble(a.DefaultValue):F2} max={FixedToDouble(a.MaxValue):F2}");
        sb.AppendLine($"    InstanceCount: {Instances.Length}");
        return sb.ToString();
    }
}
