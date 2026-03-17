using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'fmtx' (Font Metrics) table provides glyph-based typographic metrics: it identifies a
/// reference glyph whose phantom points carry the horizontal and vertical metric data, and
/// stores the indices of those phantom points within the glyph.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6fmtx.html"/>
[TTFTable(TableTypes.Tags.FMTX)]
public class FmtxTable : TrueTypeTable
{
    /// <summary>Initializes a new instance of the <see cref="FmtxTable"/> class.</summary>
    public FmtxTable() : base(TableTypes.FMTX) { }

    /// <inheritdoc/>
    public override int Length => 16;

    /// <summary>Gets or sets the table version (0x00020000 for version 2.0).</summary>
    public int Version { get; set; } = 0x00020000;

    /// <summary>Gets or sets the glyph index of the glyph that holds the metric phantom points.</summary>
    public uint GlyphIndex { get; set; }

    /// <summary>Gets or sets the phantom-point index for the horizontal before-advance metric.</summary>
    public byte HorizontalBefore { get; set; }

    /// <summary>Gets or sets the phantom-point index for the horizontal after-advance metric.</summary>
    public byte HorizontalAfter { get; set; }

    /// <summary>Gets or sets the phantom-point index for the horizontal caret head.</summary>
    public byte HorizontalCaretHead { get; set; }

    /// <summary>Gets or sets the phantom-point index for the horizontal caret base.</summary>
    public byte HorizontalCaretBase { get; set; }

    /// <summary>Gets or sets the phantom-point index for the vertical before-advance metric.</summary>
    public byte VerticalBefore { get; set; }

    /// <summary>Gets or sets the phantom-point index for the vertical after-advance metric.</summary>
    public byte VerticalAfter { get; set; }

    /// <summary>Gets or sets the phantom-point index for the vertical caret head.</summary>
    public byte VerticalCaretHead { get; set; }

    /// <summary>Gets or sets the phantom-point index for the vertical caret base.</summary>
    public byte VerticalCaretBase { get; set; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when the data is not exactly 16 bytes.</exception>
    public override void ReadData(Reader data)
    {
        if (data.BytesLeft != 16)
            throw new ArgumentException($"Bad fmtx table size: expected 16, got {data.BytesLeft}");

        Version            = data.Read<Int32>();
        GlyphIndex         = data.Read<UInt32>();
        HorizontalBefore   = (byte)data.ReadByte();
        HorizontalAfter    = (byte)data.ReadByte();
        HorizontalCaretHead = (byte)data.ReadByte();
        HorizontalCaretBase = (byte)data.ReadByte();
        VerticalBefore     = (byte)data.ReadByte();
        VerticalAfter      = (byte)data.ReadByte();
        VerticalCaretHead  = (byte)data.ReadByte();
        VerticalCaretBase  = (byte)data.ReadByte();
    }

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<Int32>(Version);
        data.Write<UInt32>(GlyphIndex);
        data.WriteByte(HorizontalBefore);
        data.WriteByte(HorizontalAfter);
        data.WriteByte(HorizontalCaretHead);
        data.WriteByte(HorizontalCaretBase);
        data.WriteByte(VerticalBefore);
        data.WriteByte(VerticalAfter);
        data.WriteByte(VerticalCaretHead);
        data.WriteByte(VerticalCaretBase);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version     : {Version:X8}");
        sb.AppendLine($"    GlyphIndex  : {GlyphIndex}");
        sb.AppendLine($"    HorzBefore  : {HorizontalBefore}");
        sb.AppendLine($"    HorzAfter   : {HorizontalAfter}");
        sb.AppendLine($"    VertBefore  : {VerticalBefore}");
        sb.AppendLine($"    VertAfter   : {VerticalAfter}");
        return sb.ToString();
    }
}
