using System;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'OS/2' table contains metrics and other data required for correct rendering on Windows and OS/2
/// platforms. It is present in virtually every TrueType/OpenType font and describes properties such as
/// weight, width, style, typographic metrics, Unicode character ranges and vendor identification.
/// </summary>
/// <remarks>
/// <para>The table exists in six versions (0–5) that grow in size; <see cref="ReadData"/> and
/// <see cref="WriteData"/> adapt automatically to the stored <see cref="Version"/>.</para>
/// <list type="bullet">
///   <item>v0 — 78 bytes (base)</item>
///   <item>v1 — 86 bytes (adds code-page ranges)</item>
///   <item>v2/v3/v4 — 96 bytes (adds typographic height, cap height, break/default chars)</item>
///   <item>v5 — 100 bytes (adds optical size range)</item>
/// </list>
/// </remarks>
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/os2"/>
[TTFTable(TableTypes.Tags.OS_2)]
public class Os2Table : TrueTypeTable
{
    // ── Font embedding / licensing flags (fsType) ─────────────────────────

    /// <summary>Embedding permission flags stored in <see cref="FsType"/>.</summary>
    [Flags]
    public enum EmbeddingFlags : ushort
    {
        /// <summary>No embedding restrictions; freely installable.</summary>
        Installable           = 0x0000,
        /// <summary>Font may not be embedded.</summary>
        RestrictedLicense     = 0x0002,
        /// <summary>Embedding permitted for print and preview only.</summary>
        PrintAndPreview       = 0x0004,
        /// <summary>Embedding permitted for editing.</summary>
        Editable              = 0x0008,
        /// <summary>If set, no subsetting of the font is permitted.</summary>
        NoSubsetting          = 0x0100,
        /// <summary>Only bitmaps may be embedded; no outline data.</summary>
        BitmapEmbeddingOnly   = 0x0200,
    }

    // ── Selection flags (fsSelection) ────────────────────────────────────

    /// <summary>Font style selection flags stored in <see cref="FsSelection"/>.</summary>
    [Flags]
    public enum SelectionFlags : ushort
    {
        /// <summary>Font contains italic glyphs.</summary>
        Italic          = 0x0001,
        /// <summary>Glyphs are underscored.</summary>
        Underscore      = 0x0002,
        /// <summary>Glyphs have their foreground and background colours reversed.</summary>
        Negative        = 0x0004,
        /// <summary>Outline (hollow) glyphs.</summary>
        Outlined        = 0x0008,
        /// <summary>Overstrike (strikethrough) glyphs.</summary>
        Strikeout       = 0x0010,
        /// <summary>Glyphs are emboldened.</summary>
        Bold            = 0x0020,
        /// <summary>Regular-weight font (not bold, not italic).</summary>
        Regular         = 0x0040,
        /// <summary>Use OS/2 typographic metrics for line spacing (v4+).</summary>
        UseTypoMetrics  = 0x0080,
        /// <summary>This font is a "weight/width/slope" variant of a family (v4+).</summary>
        Wws             = 0x0100,
        /// <summary>Font supports oblique rendering.</summary>
        Oblique         = 0x0200,
    }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="Os2Table"/> class.</summary>
    public Os2Table() : base(TableTypes.OS_2) { }

    // ── Version 0 fields (78 bytes) ───────────────────────────────────────

    /// <summary>Gets or sets the OS/2 table version (0–5).</summary>
    public ushort Version { get; set; }

    /// <summary>Gets or sets the arithmetic mean of the width of all non-zero-width glyphs.</summary>
    public short XAvgCharWidth { get; set; }

    /// <summary>
    /// Gets or sets the visual weight of the font in the range 1–1000.
    /// Typical values: 400 = Regular, 700 = Bold.
    /// </summary>
    public ushort UsWeightClass { get; set; } = 400;

    /// <summary>
    /// Gets or sets the relative change in arm-widths (condensed/expanded).
    /// Values 1–9 map from ultra-condensed to ultra-expanded.
    /// </summary>
    public ushort UsWidthClass { get; set; } = 5;

    /// <summary>Gets or sets the font embedding-permission flags (<see cref="EmbeddingFlags"/>).</summary>
    public EmbeddingFlags FsType { get; set; }

    /// <summary>Gets or sets the recommended horizontal size for subscripts, in font design units.</summary>
    public short YSubscriptXSize { get; set; }

    /// <summary>Gets or sets the recommended vertical size for subscripts, in font design units.</summary>
    public short YSubscriptYSize { get; set; }

    /// <summary>Gets or sets the recommended horizontal offset for subscripts, in font design units.</summary>
    public short YSubscriptXOffset { get; set; }

    /// <summary>Gets or sets the recommended vertical offset for subscripts, in font design units.</summary>
    public short YSubscriptYOffset { get; set; }

    /// <summary>Gets or sets the recommended horizontal size for superscripts, in font design units.</summary>
    public short YSuperscriptXSize { get; set; }

    /// <summary>Gets or sets the recommended vertical size for superscripts, in font design units.</summary>
    public short YSuperscriptYSize { get; set; }

    /// <summary>Gets or sets the recommended horizontal offset for superscripts, in font design units.</summary>
    public short YSuperscriptXOffset { get; set; }

    /// <summary>Gets or sets the recommended vertical offset for superscripts, in font design units.</summary>
    public short YSuperscriptYOffset { get; set; }

    /// <summary>Gets or sets the thickness of the strikeout stroke, in font design units.</summary>
    public short YStrikeoutSize { get; set; }

    /// <summary>Gets or sets the position of the strikeout stroke relative to the baseline.</summary>
    public short YStrikeoutPosition { get; set; }

    /// <summary>
    /// Gets or sets the font-family class and subclass codes.
    /// High byte = class (0–14), low byte = subclass.
    /// </summary>
    public short SFamilyClass { get; set; }

    /// <summary>
    /// Gets or sets the 10-byte PANOSE classification numbers.
    /// Index 0 = FamilyType, 1 = SerifStyle, … 9 = LetterForm.
    /// </summary>
    public byte[] Panose { get; set; } = new byte[10];

    /// <summary>Gets or sets the Unicode character range bit flags (bits 0–31).</summary>
    public uint UlUnicodeRange1 { get; set; }

    /// <summary>Gets or sets the Unicode character range bit flags (bits 32–63).</summary>
    public uint UlUnicodeRange2 { get; set; }

    /// <summary>Gets or sets the Unicode character range bit flags (bits 64–95).</summary>
    public uint UlUnicodeRange3 { get; set; }

    /// <summary>Gets or sets the Unicode character range bit flags (bits 96–127).</summary>
    public uint UlUnicodeRange4 { get; set; }

    /// <summary>
    /// Gets or sets the four-character ASCII vendor identifier string (e.g. "ADBE", "MSFT").
    /// Stored as exactly 4 bytes; padded with spaces if shorter.
    /// </summary>
    public string AchVendId { get; set; } = "    ";

    /// <summary>Gets or sets the font style selection flags (<see cref="SelectionFlags"/>).</summary>
    public SelectionFlags FsSelection { get; set; }

    /// <summary>Gets or sets the Unicode code point of the first character in the font.</summary>
    public ushort UsFirstCharIndex { get; set; }

    /// <summary>Gets or sets the Unicode code point of the last character in the font.</summary>
    public ushort UsLastCharIndex { get; set; }

    /// <summary>Gets or sets the typographic ascender above the baseline, in font design units.</summary>
    public short STypoAscender { get; set; }

    /// <summary>Gets or sets the typographic descender below the baseline (negative), in font design units.</summary>
    public short STypoDescender { get; set; }

    /// <summary>Gets or sets the typographic line gap, in font design units.</summary>
    public short STypoLineGap { get; set; }

    /// <summary>Gets or sets the Windows-specific ascender (used as the top of the em square on screen).</summary>
    public ushort UsWinAscent { get; set; }

    /// <summary>Gets or sets the Windows-specific descender (unsigned, below baseline).</summary>
    public ushort UsWinDescent { get; set; }

    // ── Version 1 fields (86 bytes) ───────────────────────────────────────

    /// <summary>Gets or sets the code-page coverage bit flags (bits 0–31). Version ≥ 1.</summary>
    public uint UlCodePageRange1 { get; set; }

    /// <summary>Gets or sets the code-page coverage bit flags (bits 32–63). Version ≥ 1.</summary>
    public uint UlCodePageRange2 { get; set; }

    // ── Version 2/3/4 fields (96 bytes) ──────────────────────────────────

    /// <summary>Gets or sets the height of a lowercase 'x' relative to the baseline. Version ≥ 2.</summary>
    public short SxHeight { get; set; }

    /// <summary>Gets or sets the height of an uppercase letter relative to the baseline. Version ≥ 2.</summary>
    public short SCapHeight { get; set; }

    /// <summary>
    /// Gets or sets the Unicode code point to use for missing characters.
    /// 0 means no default character is defined. Version ≥ 2.
    /// </summary>
    public ushort UsDefaultChar { get; set; }

    /// <summary>
    /// Gets or sets the Unicode code point of the word-break character (typically 0x0020 SPACE). Version ≥ 2.
    /// </summary>
    public ushort UsBreakChar { get; set; } = 0x0020;

    /// <summary>
    /// Gets or sets the maximum length of a target glyph context for any feature in the font.
    /// Version ≥ 2.
    /// </summary>
    public ushort UsMaxContext { get; set; }

    // ── Version 5 fields (100 bytes) ─────────────────────────────────────

    /// <summary>
    /// Gets or sets the lower end of the optical size range (in twentieths of a point). Version ≥ 5.
    /// </summary>
    public ushort UsLowerOpticalPointSize { get; set; }

    /// <summary>
    /// Gets or sets the upper end of the optical size range (in twentieths of a point). Version ≥ 5.
    /// </summary>
    public ushort UsUpperOpticalPointSize { get; set; }

    // ── Length ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override int Length => Version switch
    {
        0         => 78,
        1         => 86,
        2 or 3 or 4 => 96,
        _         => 100,   // version 5+
    };

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version          = data.Read<UInt16>();
        XAvgCharWidth    = data.Read<Int16>();
        UsWeightClass    = data.Read<UInt16>();
        UsWidthClass     = data.Read<UInt16>();
        FsType           = (EmbeddingFlags)data.Read<UInt16>();
        YSubscriptXSize  = data.Read<Int16>();
        YSubscriptYSize  = data.Read<Int16>();
        YSubscriptXOffset = data.Read<Int16>();
        YSubscriptYOffset = data.Read<Int16>();
        YSuperscriptXSize = data.Read<Int16>();
        YSuperscriptYSize = data.Read<Int16>();
        YSuperscriptXOffset = data.Read<Int16>();
        YSuperscriptYOffset = data.Read<Int16>();
        YStrikeoutSize   = data.Read<Int16>();
        YStrikeoutPosition = data.Read<Int16>();
        SFamilyClass     = data.Read<Int16>();

        // 10-byte PANOSE array
        Panose = new byte[10];
        for (int i = 0; i < 10; i++)
            Panose[i] = (byte)data.ReadByte();

        UlUnicodeRange1  = data.Read<UInt32>();
        UlUnicodeRange2  = data.Read<UInt32>();
        UlUnicodeRange3  = data.Read<UInt32>();
        UlUnicodeRange4  = data.Read<UInt32>();

        // 4-byte vendor ID (ASCII)
        byte[] vendId = new byte[4];
        for (int i = 0; i < 4; i++)
            vendId[i] = (byte)data.ReadByte();
        AchVendId = Encoding.ASCII.GetString(vendId);

        FsSelection        = (SelectionFlags)data.Read<UInt16>();
        UsFirstCharIndex   = data.Read<UInt16>();
        UsLastCharIndex    = data.Read<UInt16>();
        STypoAscender      = data.Read<Int16>();
        STypoDescender     = data.Read<Int16>();
        STypoLineGap       = data.Read<Int16>();
        UsWinAscent        = data.Read<UInt16>();
        UsWinDescent       = data.Read<UInt16>();

        if (Version >= 1)
        {
            UlCodePageRange1 = data.Read<UInt32>();
            UlCodePageRange2 = data.Read<UInt32>();
        }

        if (Version >= 2)
        {
            SxHeight        = data.Read<Int16>();
            SCapHeight      = data.Read<Int16>();
            UsDefaultChar   = data.Read<UInt16>();
            UsBreakChar     = data.Read<UInt16>();
            UsMaxContext    = data.Read<UInt16>();
        }

        if (Version >= 5)
        {
            UsLowerOpticalPointSize = data.Read<UInt16>();
            UsUpperOpticalPointSize = data.Read<UInt16>();
        }
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<UInt16>(Version);
        data.Write<Int16>(XAvgCharWidth);
        data.Write<UInt16>(UsWeightClass);
        data.Write<UInt16>(UsWidthClass);
        data.Write<UInt16>((ushort)FsType);
        data.Write<Int16>(YSubscriptXSize);
        data.Write<Int16>(YSubscriptYSize);
        data.Write<Int16>(YSubscriptXOffset);
        data.Write<Int16>(YSubscriptYOffset);
        data.Write<Int16>(YSuperscriptXSize);
        data.Write<Int16>(YSuperscriptYSize);
        data.Write<Int16>(YSuperscriptXOffset);
        data.Write<Int16>(YSuperscriptYOffset);
        data.Write<Int16>(YStrikeoutSize);
        data.Write<Int16>(YStrikeoutPosition);
        data.Write<Int16>(SFamilyClass);

        // PANOSE (always 10 bytes)
        byte[] panose = Panose ?? new byte[10];
        for (int i = 0; i < 10; i++)
            data.WriteByte(i < panose.Length ? panose[i] : (byte)0);

        data.Write<UInt32>(UlUnicodeRange1);
        data.Write<UInt32>(UlUnicodeRange2);
        data.Write<UInt32>(UlUnicodeRange3);
        data.Write<UInt32>(UlUnicodeRange4);

        // Vendor ID (always exactly 4 bytes, space-padded)
        byte[] vendId = new byte[4];
        byte[] src = Encoding.ASCII.GetBytes(AchVendId ?? "    ");
        for (int i = 0; i < 4; i++)
            vendId[i] = i < src.Length ? src[i] : (byte)' ';
        foreach (byte b in vendId)
            data.WriteByte(b);

        data.Write<UInt16>((ushort)FsSelection);
        data.Write<UInt16>(UsFirstCharIndex);
        data.Write<UInt16>(UsLastCharIndex);
        data.Write<Int16>(STypoAscender);
        data.Write<Int16>(STypoDescender);
        data.Write<Int16>(STypoLineGap);
        data.Write<UInt16>(UsWinAscent);
        data.Write<UInt16>(UsWinDescent);

        if (Version >= 1)
        {
            data.Write<UInt32>(UlCodePageRange1);
            data.Write<UInt32>(UlCodePageRange2);
        }

        if (Version >= 2)
        {
            data.Write<Int16>(SxHeight);
            data.Write<Int16>(SCapHeight);
            data.Write<UInt16>(UsDefaultChar);
            data.Write<UInt16>(UsBreakChar);
            data.Write<UInt16>(UsMaxContext);
        }

        if (Version >= 5)
        {
            data.Write<UInt16>(UsLowerOpticalPointSize);
            data.Write<UInt16>(UsUpperOpticalPointSize);
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version         : {Version}");
        sb.AppendLine($"    WeightClass     : {UsWeightClass}");
        sb.AppendLine($"    WidthClass      : {UsWidthClass}");
        sb.AppendLine($"    FsType          : {FsType}");
        sb.AppendLine($"    FsSelection     : {FsSelection}");
        sb.AppendLine($"    VendorID        : {AchVendId}");
        sb.AppendLine($"    TypoAscender    : {STypoAscender}");
        sb.AppendLine($"    TypoDescender   : {STypoDescender}");
        sb.AppendLine($"    WinAscent       : {UsWinAscent}");
        sb.AppendLine($"    WinDescent      : {UsWinDescent}");
        if (Version >= 2)
        {
            sb.AppendLine($"    xHeight         : {SxHeight}");
            sb.AppendLine($"    CapHeight       : {SCapHeight}");
        }
        return sb.ToString();
    }
}
