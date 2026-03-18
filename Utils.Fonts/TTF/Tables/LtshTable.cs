using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'LTSH' (Linear Threshold) table stores a per-glyph pixel-per-em threshold value.
/// Below this threshold the glyph is non-linear (i.e. hinting is used); at or above it
/// the glyph scales linearly and hinting can be suppressed for better appearance.
/// A value of 1 means the glyph is always linear; 0 means the threshold is unknown.
/// </summary>
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/ltsh"/>
[TTFTable(TableTypes.Tags.LTSH)]
public class LtshTable : TrueTypeTable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LtshTable"/> class.
    /// </summary>
    public LtshTable() : base(TableTypes.LTSH) { }

    /// <summary>Gets or sets the table version (always 0).</summary>
    public ushort Version { get; set; }

    /// <summary>
    /// Gets or sets the per-glyph linear threshold array.
    /// <c>YPels[i]</c> is the minimum PPEM size at which glyph <c>i</c> scales linearly.
    /// </summary>
    public byte[] YPels { get; set; } = [];

    /// <inheritdoc/>
    public override int Length => 4 + YPels.Length;

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version = data.Read<UInt16>();
        int numGlyphs = data.Read<UInt16>();
        YPels = new byte[numGlyphs];
        for (int i = 0; i < numGlyphs; i++)
            YPels[i] = (byte)data.ReadByte();
    }

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<UInt16>(Version);
        data.Write<UInt16>((ushort)YPels.Length);
        foreach (byte y in YPels)
            data.WriteByte(y);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version   : {Version}");
        sb.AppendLine($"    NumGlyphs : {YPels.Length}");
        return sb.ToString();
    }
}
