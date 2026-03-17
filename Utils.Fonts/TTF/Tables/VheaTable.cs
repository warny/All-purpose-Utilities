using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'vhea' table contains information needed to lay out fonts whose characters are written
/// vertically, that is, either top-to-bottom or bottom-to-top. It is the vertical analogue of
/// the 'hhea' (horizontal header) table and has the same 36-byte fixed structure.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6vhea.html"/>
[TTFTable(TableTypes.Tags.VHEA)]
public class VheaTable : TrueTypeTable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VheaTable"/> class.
    /// </summary>
    public VheaTable() : base(TableTypes.VHEA) { }

    /// <inheritdoc/>
    public override int Length => 36;

    /// <summary>Gets or sets the version of the vhea table. Version 1.1 = 0x00011000.</summary>
    public virtual int Version { get; set; } = 0x00011000;

    /// <summary>Gets or sets the typographic ascender for vertical layout (distance from the centre line to the top of the em square).</summary>
    public virtual short VertTypoAscender { get; set; }

    /// <summary>Gets or sets the typographic descender for vertical layout (distance from the centre line to the bottom of the em square).</summary>
    public virtual short VertTypoDescender { get; set; }

    /// <summary>Gets or sets the additional line spacing for vertical layout.</summary>
    public virtual short VertTypoLineGap { get; set; }

    /// <summary>Gets or sets the maximum advance height across all glyphs.</summary>
    public virtual short AdvanceHeightMax { get; set; }

    /// <summary>Gets or sets the minimum top side bearing across all glyphs.</summary>
    public virtual short MinTopSideBearing { get; set; }

    /// <summary>Gets or sets the minimum bottom side bearing across all glyphs.</summary>
    public virtual short MinBottomSideBearing { get; set; }

    /// <summary>Gets or sets the maximum y-extent (top side bearing + bounding-box height).</summary>
    public virtual short YMaxExtent { get; set; }

    /// <summary>Gets or sets the rise component of the caret slope for vertical text (typically 0 for horizontal caret).</summary>
    public virtual short CaretSlopeRise { get; set; }

    /// <summary>Gets or sets the run component of the caret slope for vertical text.</summary>
    public virtual short CaretSlopeRun { get; set; }

    /// <summary>Gets or sets the caret offset for vertical text (0 for non-slanted fonts).</summary>
    public virtual short CaretOffset { get; set; }

    /// <summary>Gets or sets the metric data format (currently always 0).</summary>
    public virtual short MetricDataFormat { get; set; }

    /// <summary>Gets or sets the number of advance-height/top-side-bearing long metric entries in the 'vmtx' table.</summary>
    public virtual ushort NumOfLongVerMetrics { get; set; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when the remaining data is not exactly 36 bytes.</exception>
    public override void ReadData(Reader data)
    {
        if (data.BytesLeft != 36)
            throw new ArgumentException("Bad vhea table size");

        Version              = data.Read<Int32>();
        VertTypoAscender     = data.Read<Int16>();
        VertTypoDescender    = data.Read<Int16>();
        VertTypoLineGap      = data.Read<Int16>();
        AdvanceHeightMax     = data.Read<Int16>();
        MinTopSideBearing    = data.Read<Int16>();
        MinBottomSideBearing = data.Read<Int16>();
        YMaxExtent           = data.Read<Int16>();
        CaretSlopeRise       = data.Read<Int16>();
        CaretSlopeRun        = data.Read<Int16>();
        CaretOffset          = data.Read<Int16>();
        data.Read<Int16>(); // reserved
        data.Read<Int16>(); // reserved
        data.Read<Int16>(); // reserved
        data.Read<Int16>(); // reserved
        MetricDataFormat     = data.Read<Int16>();
        NumOfLongVerMetrics  = data.Read<UInt16>();
    }

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<Int32>(Version);
        data.Write<Int16>(VertTypoAscender);
        data.Write<Int16>(VertTypoDescender);
        data.Write<Int16>(VertTypoLineGap);
        data.Write<Int16>(AdvanceHeightMax);
        data.Write<Int16>(MinTopSideBearing);
        data.Write<Int16>(MinBottomSideBearing);
        data.Write<Int16>(YMaxExtent);
        data.Write<Int16>(CaretSlopeRise);
        data.Write<Int16>(CaretSlopeRun);
        data.Write<Int16>(CaretOffset);
        data.Write<Int16>(0); // reserved
        data.Write<Int16>(0); // reserved
        data.Write<Int16>(0); // reserved
        data.Write<Int16>(0); // reserved
        data.Write<Int16>(MetricDataFormat);
        data.Write<UInt16>(NumOfLongVerMetrics);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version                : {Version:X8}");
        sb.AppendLine($"    VertTypoAscender       : {VertTypoAscender}");
        sb.AppendLine($"    VertTypoDescender      : {VertTypoDescender}");
        sb.AppendLine($"    VertTypoLineGap        : {VertTypoLineGap}");
        sb.AppendLine($"    AdvanceHeightMax       : {AdvanceHeightMax}");
        sb.AppendLine($"    MinTopSideBearing      : {MinTopSideBearing}");
        sb.AppendLine($"    MinBottomSideBearing   : {MinBottomSideBearing}");
        sb.AppendLine($"    YMaxExtent             : {YMaxExtent}");
        sb.AppendLine($"    CaretSlopeRise         : {CaretSlopeRise}");
        sb.AppendLine($"    CaretSlopeRun          : {CaretSlopeRun}");
        sb.AppendLine($"    CaretOffset            : {CaretOffset}");
        sb.AppendLine($"    MetricDataFormat       : {MetricDataFormat}");
        sb.AppendLine($"    NumOfLongVerMetrics    : {NumOfLongVerMetrics}");
        return sb.ToString();
    }
}
