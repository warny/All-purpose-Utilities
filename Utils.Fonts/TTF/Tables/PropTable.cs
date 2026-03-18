using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'prop' (Glyph Properties) table assigns directional and typographic property bits to each
/// glyph. Properties include writing direction (LTR/RTL/neutral), hanging punctuation, kerning
/// suppression, and attachment class.
/// </summary>
/// <remarks>
/// <para>Format 0 (simple): all glyphs share the same default property value — no per-glyph override.</para>
/// <para>Format 1 (lookup): an AAT lookup table maps individual glyphs to their property values.
/// Glyphs not in the lookup use <see cref="DefaultProp"/>.</para>
/// <para>Property bit layout (UInt16):</para>
/// <list type="bullet">
///   <item>Bits 15–14: direction (0=strong L→R, 1=strong R→L, 2=weak, 3=neutral).</item>
///   <item>Bit 13: hangs on right.</item>
///   <item>Bit 12: hangs on left.</item>
///   <item>Bit 11: suppress kerning before.</item>
///   <item>Bit 10: suppress kerning after.</item>
/// </list>
/// </remarks>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6prop.html"/>
[TTFTable(TableTypes.Tags.PROP)]
public class PropTable : TrueTypeTable
{
    // ── Property bit constants ────────────────────────────────────────────

    /// <summary>Mask for the two-bit direction field (bits 15–14).</summary>
    public const ushort DirectionMask = 0xC000;

    /// <summary>Property bit: glyph hangs on its right edge.</summary>
    public const ushort HangsOnRight = 0x2000;

    /// <summary>Property bit: glyph hangs on its left edge.</summary>
    public const ushort HangsOnLeft = 0x1000;

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="PropTable"/> class.</summary>
    public PropTable() : base(TableTypes.PROP) { }

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Gets or sets the table version (0x00010000 or 0x00020000).</summary>
    public int Version { get; set; } = 0x00010000;

    /// <summary>Gets or sets the table format: 0 = no per-glyph lookup, 1 = per-glyph lookup.</summary>
    public ushort Format { get; set; }

    /// <summary>Gets or sets the property value used for glyphs not found in <see cref="GlyphProperties"/>.</summary>
    public ushort DefaultProp { get; set; }

    /// <summary>
    /// Gets or sets per-glyph property overrides (glyph index → property bits).
    /// Populated only when <see cref="Format"/> is 1. Ignored on write when Format is 0.
    /// </summary>
    public Dictionary<ushort, ushort> GlyphProperties { get; set; } = [];

    // ── Length ────────────────────────────────────────────────────────────

    private int LookupSize => 12 + (GlyphProperties.Count + 1) * 4;  // Format 6, like bsln

    /// <inheritdoc/>
    public override int Length => Format == 0
        ? 8                   // version(4) + format(2) + defaultProp(2)
        : 8 + LookupSize;

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version     = data.Read<Int32>();
        Format      = data.Read<UInt16>();
        DefaultProp = data.Read<UInt16>();

        GlyphProperties = [];
        if (Format != 0)
            GlyphProperties = ReadLookupTable(data);
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<Int32>(Version);
        data.Write<UInt16>(Format);
        data.Write<UInt16>(DefaultProp);

        if (Format != 0)
            WriteLookupTableFormat6(data, GlyphProperties);
    }

    // ── AAT Lookup helpers ────────────────────────────────────────────────

    /// <summary>Reads an AAT lookup table (formats 0, 2, 4, 6, 8) at the current stream position.</summary>
    private static Dictionary<ushort, ushort> ReadLookupTable(Reader data)
    {
        var result = new Dictionary<ushort, ushort>();
        long tableStart = data.Position;
        ushort format = data.Read<UInt16>();

        switch (format)
        {
            case 0:
            {
                // Format 0: simple array indexed by glyph
                int nGlyphs = (int)(data.BytesLeft / 2);
                for (ushort g = 0; g < nGlyphs; g++)
                {
                    ushort v = data.Read<UInt16>();
                    if (v != 0) result[g] = v;
                }
                break;
            }
            case 2:
            {
                // Format 2: segment-to-single (all glyphs in segment share a value)
                data.Read<UInt16>(); // unitSize
                ushort nSegs = data.Read<UInt16>();
                data.Read<UInt16>(); data.Read<UInt16>(); data.Read<UInt16>(); // binary search params
                for (int i = 0; i < nSegs; i++)
                {
                    ushort lastGlyph  = data.Read<UInt16>();
                    ushort firstGlyph = data.Read<UInt16>();
                    ushort value      = data.Read<UInt16>();
                    if (firstGlyph == 0xFFFF) break;
                    for (ushort g = firstGlyph; g <= lastGlyph; g++)
                        result[g] = value;
                }
                break;
            }
            case 4:
            {
                // Format 4: segment to offset (each segment maps to an array of values)
                data.Read<UInt16>(); data.Read<UInt16>(); data.Read<UInt16>(); // unitSize+nSegs+bsearch
                data.Read<UInt16>(); data.Read<UInt16>();
                // simplified: read until we see the sentinel
                while (data.BytesLeft >= 6)
                {
                    ushort lastGlyph  = data.Read<UInt16>();
                    ushort firstGlyph = data.Read<UInt16>();
                    ushort offset     = data.Read<UInt16>();
                    if (firstGlyph == 0xFFFF) break;
                    data.Push((int)(tableStart + offset), SeekOrigin.Begin);
                    for (ushort g = firstGlyph; g <= lastGlyph; g++)
                        result[g] = data.Read<UInt16>();
                    data.Pop();
                }
                break;
            }
            case 6:
            {
                // Format 6: single table (sorted list of glyph, value pairs + 0xFFFF sentinel)
                data.Read<UInt16>(); data.Read<UInt16>(); data.Read<UInt16>();
                data.Read<UInt16>(); data.Read<UInt16>(); // binary search params
                while (data.BytesLeft >= 4)
                {
                    ushort glyph = data.Read<UInt16>();
                    ushort value = data.Read<UInt16>();
                    if (glyph == 0xFFFF) break;
                    result[glyph] = value;
                }
                break;
            }
            case 8:
            {
                // Format 8: trimmed array starting at firstGlyph
                ushort firstGlyph = data.Read<UInt16>();
                ushort glyphCount = data.Read<UInt16>();
                for (ushort i = 0; i < glyphCount; i++)
                {
                    ushort v = data.Read<UInt16>();
                    if (v != 0) result[(ushort)(firstGlyph + i)] = v;
                }
                break;
            }
        }

        return result;
    }

    /// <summary>Writes a Format 6 (single table) AAT lookup with a 0xFFFF sentinel entry.</summary>
    private static void WriteLookupTableFormat6(Writer data, Dictionary<ushort, ushort> map)
    {
        int n = map.Count + 1;  // +1 for sentinel
        // Binary search parameters
        int unitSize    = 4;
        int searchRange = 1;
        int entrySelector = 0;
        while (searchRange * 2 <= n) { searchRange *= 2; entrySelector++; }
        int rangeShift = (n - searchRange) * unitSize;
        searchRange *= unitSize;

        data.Write<UInt16>(6);   // format
        data.Write<UInt16>((ushort)unitSize);
        data.Write<UInt16>((ushort)n);
        data.Write<UInt16>((ushort)searchRange);
        data.Write<UInt16>((ushort)entrySelector);
        data.Write<UInt16>((ushort)rangeShift);

        var sorted = new SortedDictionary<ushort, ushort>(map);
        foreach (var (k, v) in sorted)
        {
            data.Write<UInt16>(k);
            data.Write<UInt16>(v);
        }
        // Sentinel
        data.Write<UInt16>(0xFFFF);
        data.Write<UInt16>(0);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version     : {Version:X8}");
        sb.AppendLine($"    Format      : {Format}");
        sb.AppendLine($"    DefaultProp : {DefaultProp:X4}");
        sb.AppendLine($"    GlyphOverrides: {GlyphProperties.Count}");
        return sb.ToString();
    }
}
