using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'fdsc' (Font Descriptors) table provides a set of named scalar values that describe
/// characteristics of the font — such as optical weight, contrast, or size. Each descriptor
/// has a 4-byte tag and a 16.16 fixed-point value.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6fdsc.html"/>
[TTFTable(TableTypes.Tags.FDSC)]
public class FdscTable : TrueTypeTable
{
    // ── Nested type ───────────────────────────────────────────────────────

    /// <summary>
    /// One named descriptor entry: a 4-byte tag and a 16.16 fixed-point scalar value.
    /// </summary>
    public sealed class FontDescriptor
    {
        /// <summary>
        /// Gets or sets the 4-byte ASCII tag identifying the descriptor
        /// (e.g. <c>"wght"</c>, <c>"wdth"</c>, <c>"slnt"</c>).
        /// </summary>
        public string Tag { get; set; } = "    ";

        /// <summary>Gets or sets the descriptor value as a 16.16 fixed-point Int32.</summary>
        public int Value { get; set; }
    }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="FdscTable"/> class.</summary>
    public FdscTable() : base(TableTypes.FDSC) { }

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Gets or sets the table version (0x00010000 = version 1.0).</summary>
    public int Version { get; set; } = 0x00010000;

    /// <summary>Gets or sets the array of font descriptor entries.</summary>
    public FontDescriptor[] Descriptors { get; set; } = [];

    // ── Length ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override int Length => 8 + Descriptors.Length * 8;

    // ── Fixed-point helper ────────────────────────────────────────────────

    /// <summary>Converts a 16.16 fixed-point Int32 to a <see cref="double"/>.</summary>
    public static double FixedToDouble(int value) => value / 65536.0;

    /// <summary>Converts a <see cref="double"/> to a 16.16 fixed-point Int32.</summary>
    public static int DoubleToFixed(double value) => (int)Math.Round(value * 65536.0);

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version = data.Read<Int32>();
        int count = (int)data.Read<UInt32>();
        Descriptors = new FontDescriptor[count];
        for (int i = 0; i < count; i++)
        {
            byte[] tagBytes = [
                (byte)data.ReadByte(),
                (byte)data.ReadByte(),
                (byte)data.ReadByte(),
                (byte)data.ReadByte()
            ];
            Descriptors[i] = new FontDescriptor
            {
                Tag   = Encoding.ASCII.GetString(tagBytes),
                Value = data.Read<Int32>(),
            };
        }
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<Int32>(Version);
        data.Write<UInt32>((uint)Descriptors.Length);
        foreach (var d in Descriptors)
        {
            byte[] src = Encoding.ASCII.GetBytes(d.Tag ?? "    ");
            for (int i = 0; i < 4; i++)
                data.WriteByte(i < src.Length ? src[i] : (byte)' ');
            data.Write<Int32>(d.Value);
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version : {Version:X8}");
        foreach (var d in Descriptors)
            sb.AppendLine($"      [{d.Tag}] = {FixedToDouble(d.Value):F4}");
        return sb.ToString();
    }
}
