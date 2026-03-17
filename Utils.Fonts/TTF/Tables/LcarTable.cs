using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'lcar' (Ligature Caret) table specifies caret positions within ligature glyphs so that
/// a text caret can be placed at intermediate positions inside a ligature (e.g. between
/// the two characters of a fi ligature). Each ligature glyph has one or more caret values.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><term>Format 0</term><description>Caret positions are in font design units.</description></item>
///   <item><term>Format 1</term><description>Caret positions are contour-point indices.</description></item>
/// </list>
/// </remarks>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6lcar.html"/>
[TTFTable(TableTypes.Tags.LCAR)]
public class LcarTable : TrueTypeTable
{
    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="LcarTable"/> class.</summary>
    public LcarTable() : base(TableTypes.LCAR) { }

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Gets or sets the table version (0x00010000 = version 1.0).</summary>
    public int Version { get; set; } = 0x00010000;

    /// <summary>
    /// Gets or sets the data format: 0 = caret values in design units; 1 = contour-point indices.
    /// </summary>
    public ushort Format { get; set; }

    /// <summary>
    /// Gets or sets the per-ligature-glyph caret positions.
    /// Each entry maps a glyph index to an array of caret positions (or point indices).
    /// For a two-component ligature the array has one element (the single intermediate caret).
    /// </summary>
    public Dictionary<ushort, short[]> GlyphCarets { get; set; } = [];

    // ── Length ────────────────────────────────────────────────────────────

    // Header: version(4)+format(2) = 6 bytes
    // Lookup (Format 6): 12 + (n+1)*4 — maps glyph → offset into caret-data region
    // Caret data: per glyph: count(2) + count*2 bytes
    private int LookupSize => 12 + (GlyphCarets.Count + 1) * 4;

    /// <inheritdoc/>
    public override int Length
    {
        get
        {
            int caretDataSize = 0;
            foreach (var c in GlyphCarets.Values)
                caretDataSize += 2 + c.Length * 2;  // count(2) + carets
            return 6 + LookupSize + caretDataSize;
        }
    }

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version = data.Read<Int32>();
        Format  = data.Read<UInt16>();
        GlyphCarets = ReadLookupCarets(data);
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<Int32>(Version);
        data.Write<UInt16>(Format);

        // Layout after header(6): lookup then caret data
        int caretDataStart = 6 + LookupSize;
        var sorted = new SortedDictionary<ushort, short[]>(GlyphCarets);

        // Write Format 6 lookup (glyph → absolute offset to caret data)
        int n = sorted.Count + 1;
        int unitSize     = 4;
        int searchRange  = 1;
        int entrySelector = 0;
        while (searchRange * 2 <= n) { searchRange *= 2; entrySelector++; }
        int rangeShift = (n - searchRange) * unitSize;
        searchRange   *= unitSize;

        data.Write<UInt16>(6);
        data.Write<UInt16>((ushort)unitSize);
        data.Write<UInt16>((ushort)n);
        data.Write<UInt16>((ushort)searchRange);
        data.Write<UInt16>((ushort)entrySelector);
        data.Write<UInt16>((ushort)rangeShift);

        int cursor = caretDataStart;
        foreach (var (glyph, carets) in sorted)
        {
            data.Write<UInt16>(glyph);
            data.Write<UInt16>((ushort)cursor);
            cursor += 2 + carets.Length * 2;
        }
        data.Write<UInt16>(0xFFFF);
        data.Write<UInt16>(0);

        // Write caret data
        foreach (var (_, carets) in sorted)
        {
            data.Write<UInt16>((ushort)carets.Length);
            foreach (short c in carets)
                data.Write<Int16>(c);
        }
    }

    // ── Lookup reader ─────────────────────────────────────────────────────

    private static Dictionary<ushort, short[]> ReadLookupCarets(Reader data)
    {
        var offsets = new Dictionary<ushort, int>();
        ushort format = data.Read<UInt16>();

        switch (format)
        {
            case 6:
            {
                data.Read<UInt16>(); data.Read<UInt16>(); data.Read<UInt16>();
                data.Read<UInt16>(); data.Read<UInt16>();
                while (data.BytesLeft >= 4)
                {
                    ushort glyph  = data.Read<UInt16>();
                    ushort offset = data.Read<UInt16>();
                    if (glyph == 0xFFFF) break;
                    offsets[glyph] = offset;
                }
                break;
            }
            case 8:
            {
                ushort first = data.Read<UInt16>();
                ushort count = data.Read<UInt16>();
                for (ushort i = 0; i < count; i++)
                {
                    ushort offset = data.Read<UInt16>();
                    if (offset != 0) offsets[(ushort)(first + i)] = offset;
                }
                break;
            }
        }

        var result = new Dictionary<ushort, short[]>();
        foreach (var (glyph, offset) in offsets)
        {
            if (offset == 0) continue;
            data.Push(offset, SeekOrigin.Begin);
            ushort caretCount = data.Read<UInt16>();
            var carets = new short[caretCount];
            for (int i = 0; i < caretCount; i++)
                carets[i] = data.Read<Int16>();
            data.Pop();
            result[glyph] = carets;
        }
        return result;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version : {Version:X8}");
        sb.AppendLine($"    Format  : {Format}");
        sb.AppendLine($"    Glyphs  : {GlyphCarets.Count}");
        return sb.ToString();
    }
}
