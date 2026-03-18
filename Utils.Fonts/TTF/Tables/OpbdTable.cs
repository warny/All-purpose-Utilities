using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'opbd' (Optical Bounds) table provides per-glyph adjustments that shift the effective
/// optical edge of a glyph inward or outward, improving the visual alignment of text at
/// the margins. Each glyph can have four adjustments: left, top, right, and bottom.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><term>Format 0</term><description>Values are in font design units.</description></item>
///   <item><term>Format 1</term><description>Values are contour-point indices.</description></item>
/// </list>
/// </remarks>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6opbd.html"/>
[TTFTable(TableTypes.Tags.OPBD)]
public class OpbdTable : TrueTypeTable
{
    // ── Nested type ───────────────────────────────────────────────────────

    /// <summary>
    /// Optical bound adjustments for one glyph (left, top, right, bottom),
    /// either in design units (format 0) or contour-point indices (format 1).
    /// </summary>
    public readonly record struct OpticalBounds(short Left, short Top, short Right, short Bottom);

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="OpbdTable"/> class.</summary>
    public OpbdTable() : base(TableTypes.OPBD) { }

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Gets or sets the table version (0x00010000 = version 1.0).</summary>
    public int Version { get; set; } = 0x00010000;

    /// <summary>
    /// Gets or sets the data format: 0 = values in design units, 1 = contour-point indices.
    /// </summary>
    public ushort Format { get; set; }

    /// <summary>
    /// Gets or sets the per-glyph optical bound adjustments (glyph index → bounds).
    /// </summary>
    public Dictionary<ushort, OpticalBounds> GlyphBounds { get; set; } = [];

    // ── Length ────────────────────────────────────────────────────────────

    // Each entry in the lookup maps glyph→offset into the bounds data; then the bounds are stored separately.
    // For simplicity we write the opbd table using a Format 6 lookup that stores offsets,
    // but we inline the bounds immediately after the lookup table.
    // Header: version(4) + format(2) = 6 bytes
    // Lookup (Format 6 with count+1 entries of 4 bytes): 12 + (n+1)*4
    // Bounds data: n * 8 bytes (4 × Int16)
    private int LookupSize => 12 + (GlyphBounds.Count + 1) * 4;

    /// <inheritdoc/>
    public override int Length => 6 + LookupSize + GlyphBounds.Count * 8;

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version = data.Read<Int32>();
        Format  = data.Read<UInt16>();

        // The spec uses a lookup table to map glyph → absolute offset in the table where bounds live.
        // We read the AAT lookup, then seek to each offset to read the 4-element bound.
        GlyphBounds = ReadLookupBounds(data);
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<Int32>(Version);
        data.Write<UInt16>(Format);

        // Layout: header(6) | lookup(format6) | bounds data
        // The lookup maps glyph → absolute offset from table start to bounds data.
        int boundsDataStart = 6 + LookupSize;
        var sortedGlyphs = new SortedDictionary<ushort, OpticalBounds>(GlyphBounds);

        // Write Format 6 lookup where values are offsets to bounds data
        int n = sortedGlyphs.Count + 1;
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

        int boundsOffset = boundsDataStart;
        foreach (var (glyph, _) in sortedGlyphs)
        {
            data.Write<UInt16>(glyph);
            data.Write<UInt16>((ushort)boundsOffset);
            boundsOffset += 8;
        }
        // Sentinel
        data.Write<UInt16>(0xFFFF);
        data.Write<UInt16>(0);

        // Write bounds data
        foreach (var (_, b) in sortedGlyphs)
        {
            data.Write<Int16>(b.Left);
            data.Write<Int16>(b.Top);
            data.Write<Int16>(b.Right);
            data.Write<Int16>(b.Bottom);
        }
    }

    // ── Lookup reader ─────────────────────────────────────────────────────

    /// <summary>
    /// Reads an AAT lookup table that maps glyph → absolute byte offset, then reads
    /// OpticalBounds from each offset.
    /// </summary>
    private static Dictionary<ushort, OpticalBounds> ReadLookupBounds(Reader data)
    {
        var offsets = new Dictionary<ushort, int>();
        long tableStart = data.Position;
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
            // Other formats: return empty (unsupported in this context)
        }

        var result = new Dictionary<ushort, OpticalBounds>();
        foreach (var (glyph, offset) in offsets)
        {
            if (offset == 0) continue;
            data.Push(offset, SeekOrigin.Begin);
            var bounds = new OpticalBounds(
                data.Read<Int16>(),
                data.Read<Int16>(),
                data.Read<Int16>(),
                data.Read<Int16>());
            data.Pop();
            result[glyph] = bounds;
        }
        return result;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version : {Version:X8}");
        sb.AppendLine($"    Format  : {Format}");
        sb.AppendLine($"    Glyphs  : {GlyphBounds.Count}");
        return sb.ToString();
    }
}
