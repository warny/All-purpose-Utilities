using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'hdmx' (Horizontal Device Metrics) table stores pre-computed advance widths for specific
/// pixels-per-em sizes, allowing rasterizers to avoid recomputing them at render time. Each
/// <see cref="DeviceRecord"/> covers one PPEM size and contains one advance-width byte per glyph.
/// </summary>
/// <remarks>
/// Each device record is padded to a 4-byte boundary:
/// <c>sizeDeviceRecord = (numGlyphs + 2 + 3) &amp; ~3</c> (ppem + maxWidth + widths + padding).
/// </remarks>
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/hdmx"/>
[TTFTable(TableTypes.Tags.HDMX, TableTypes.Tags.MAXP)]
public class HdmxTable : TrueTypeTable
{
    // ── Nested type ───────────────────────────────────────────────────────

    /// <summary>
    /// Contains per-glyph advance widths (in pixels) for one specific PPEM size.
    /// </summary>
    public sealed class DeviceRecord
    {
        /// <summary>Gets or sets the pixels-per-em size this record applies to.</summary>
        public byte Ppem { get; set; }

        /// <summary>Gets or sets the maximum advance width across all glyphs at this PPEM.</summary>
        public byte MaxWidth { get; set; }

        /// <summary>
        /// Gets or sets the per-glyph advance widths (one byte per glyph, in display pixels).
        /// Length must equal the font's glyph count.
        /// </summary>
        public byte[] Widths { get; set; } = [];
    }

    // ── Internal state (for dependency injection) ─────────────────────────

    /// <summary>Glyph count taken from the 'maxp' table when the font is loaded.</summary>
    private int numGlyphs;

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="HdmxTable"/> class.</summary>
    protected internal HdmxTable() : base(TableTypes.HDMX) { }

    // ── TrueTypeFont dependency ───────────────────────────────────────────

    /// <inheritdoc/>
    public override TrueTypeFont TrueTypeFont
    {
        get => base.TrueTypeFont;
        protected internal set
        {
            base.TrueTypeFont = value;
            var maxp = TrueTypeFont.GetTable<MaxpTable>(TableTypes.MAXP);
            numGlyphs = maxp.NumGlyphs;
        }
    }

    // ── Public properties ─────────────────────────────────────────────────

    /// <summary>Gets or sets the table version (always 0).</summary>
    public ushort Version { get; set; }

    /// <summary>Gets or sets the array of device records, one per PPEM size.</summary>
    public DeviceRecord[] Records { get; set; } = [];

    // ── Length ────────────────────────────────────────────────────────────

    private int SizeDeviceRecord => Records.Length > 0
        ? (Records[0].Widths.Length + 2 + 3) & ~3
        : 4;

    /// <inheritdoc/>
    public override int Length
    {
        get
        {
            if (Records.Length == 0) return 8;
            int sdr = (Records[0].Widths.Length + 2 + 3) & ~3;
            return 8 + Records.Length * sdr;
        }
    }

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version = data.Read<UInt16>();
        int numRecords      = data.Read<Int16>();
        int sizeDeviceRecord = data.Read<Int32>();

        // Per-glyph width count = sizeDeviceRecord − 2 (ppem + maxWidth), then strip padding
        int glyphCount = numGlyphs > 0 ? numGlyphs : Math.Max(0, sizeDeviceRecord - 2);

        Records = new DeviceRecord[numRecords];
        for (int i = 0; i < numRecords; i++)
        {
            var rec = new DeviceRecord
            {
                Ppem     = (byte)data.ReadByte(),
                MaxWidth = (byte)data.ReadByte(),
                Widths   = new byte[glyphCount],
            };
            for (int g = 0; g < glyphCount; g++)
                rec.Widths[g] = (byte)data.ReadByte();

            // Skip padding bytes to reach next record boundary
            int padding = sizeDeviceRecord - 2 - glyphCount;
            for (int p = 0; p < padding; p++)
                data.ReadByte();

            Records[i] = rec;
        }
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        int glyphCount = Records.Length > 0 ? Records[0].Widths.Length : 0;
        int sdr = (glyphCount + 2 + 3) & ~3;

        data.Write<UInt16>(Version);
        data.Write<Int16>((short)Records.Length);
        data.Write<Int32>(sdr);

        foreach (var rec in Records)
        {
            data.WriteByte(rec.Ppem);
            data.WriteByte(rec.MaxWidth);
            for (int g = 0; g < glyphCount; g++)
                data.WriteByte(g < rec.Widths.Length ? rec.Widths[g] : (byte)0);

            // Padding
            int padding = sdr - 2 - glyphCount;
            for (int p = 0; p < padding; p++)
                data.WriteByte(0);
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version    : {Version}");
        sb.AppendLine($"    NumRecords : {Records.Length}");
        foreach (var r in Records)
            sb.AppendLine($"      PPEM={r.Ppem,3}  MaxWidth={r.MaxWidth}");
        return sb.ToString();
    }
}
