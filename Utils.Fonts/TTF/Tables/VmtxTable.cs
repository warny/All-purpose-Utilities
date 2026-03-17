using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'vmtx' table contains metric information for the vertical layout of each glyph in the font.
/// It is the vertical analogue of the 'hmtx' table. Its layout consists of a series of
/// <c>longVerMetric</c> records (advance height + top side bearing) followed by additional
/// top-side-bearing-only values for the remaining glyphs.
/// The number of long-metric entries is given by <see cref="VheaTable.NumOfLongVerMetrics"/>,
/// and the total glyph count by <see cref="MaxpTable.NumGlyphs"/>.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6vmtx.html"/>
[TTFTable(TableTypes.Tags.VMTX, TableTypes.Tags.VHEA, TableTypes.Tags.MAXP)]
public class VmtxTable : TrueTypeTable
{
    /// <summary>
    /// Advance heights for the first <see cref="VheaTable.NumOfLongVerMetrics"/> glyphs.
    /// The last value is implicitly repeated for all subsequent glyphs.
    /// </summary>
    internal short[] advanceHeights;

    /// <summary>
    /// Top side bearings for all glyphs (length = <see cref="MaxpTable.NumGlyphs"/>).
    /// For glyphs at index &lt; <see cref="VheaTable.NumOfLongVerMetrics"/> the values are
    /// read together with the advance heights from the long-metric records.
    /// </summary>
    internal short[] topSideBearings;

    /// <summary>
    /// Initializes a new instance of the <see cref="VmtxTable"/> class.
    /// </summary>
    protected internal VmtxTable() : base(TableTypes.VMTX) { }

    /// <inheritdoc/>
    public override TrueTypeFont TrueTypeFont
    {
        get => base.TrueTypeFont;
        protected internal set
        {
            base.TrueTypeFont = value;
            var maxp = TrueTypeFont.GetTable<MaxpTable>(TableTypes.MAXP);
            var vhea = TrueTypeFont.GetTable<VheaTable>(TableTypes.VHEA);
            advanceHeights = new short[vhea.NumOfLongVerMetrics];
            topSideBearings = new short[maxp.NumGlyphs];
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Length = <c>numOfLongVerMetrics × 4 + (numGlyphs − numOfLongVerMetrics) × 2</c>,
    /// which simplifies to <c>2 × (numOfLongVerMetrics + numGlyphs)</c>.
    /// </remarks>
    public override int Length => advanceHeights.Length * 2 + topSideBearings.Length * 2;

    /// <summary>
    /// Returns the advance height for the glyph at the specified index.
    /// If the index exceeds the long-metric array, the last entry is returned (monospaced case).
    /// </summary>
    /// <param name="i">Zero-based glyph index.</param>
    /// <returns>Advance height in font design units.</returns>
    public virtual short GetAdvanceHeight(int i)
    {
        if (i < advanceHeights.Length)
            return advanceHeights[i];
        return advanceHeights[advanceHeights.Length - 1];
    }

    /// <summary>
    /// Returns the top side bearing for the glyph at the specified index.
    /// </summary>
    /// <param name="i">Zero-based glyph index.</param>
    /// <returns>Top side bearing in font design units.</returns>
    public virtual short GetTopSideBearing(int i) => topSideBearings[i];

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Array.Fill(advanceHeights, (short)0);
        Array.Fill(topSideBearings, (short)0);

        // Long-metric records: (advanceHeight, topSideBearing) per glyph
        for (int i = 0; i < advanceHeights.Length; i++)
        {
            advanceHeights[i]  = data.Read<Int16>();
            topSideBearings[i] = data.Read<Int16>();
        }

        // Additional top-side-bearing-only entries
        for (int i = advanceHeights.Length; i < topSideBearings.Length; i++)
            topSideBearings[i] = data.Read<Int16>();
    }

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        for (int i = 0; i < advanceHeights.Length; i++)
        {
            data.Write<Int16>(advanceHeights[i]);
            data.Write<Int16>(topSideBearings[i]);
        }
        for (int i = advanceHeights.Length; i < topSideBearings.Length; i++)
            data.Write<Int16>(topSideBearings[i]);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    NumLongVerMetrics : {advanceHeights.Length}");
        sb.AppendLine($"    NumGlyphs         : {topSideBearings.Length}");
        return sb.ToString();
    }
}
