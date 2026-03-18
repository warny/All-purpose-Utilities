using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The baseline table (tag: 'bsln') defines the positions of baselines for each script in the font,
/// allowing mixed-script text to align glyphs from different writing systems on a common line.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6bsln.html"/>
[TTFTable(TableTypes.Tags.BSLN)]
public class BslnTable : TrueTypeTable
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Total number of baseline slots defined by the spec (indices 0–31).</summary>
    public const int BaselineCount = 32;

    /// <summary>Roman (Latin) baseline — most glyphs sit above, descenders extend below.</summary>
    public const ushort RomanBaseline = 0;

    /// <summary>Ideographic centered baseline — CJK glyphs centered on the line.</summary>
    public const ushort IdeographicCenteredBaseline = 1;

    /// <summary>Ideographic low baseline — CJK glyphs that hang from the Roman baseline.</summary>
    public const ushort IdeographicLowBaseline = 2;

    /// <summary>Hanging baseline — Devanagari and derived scripts, top-aligned.</summary>
    public const ushort HangingBaseline = 3;

    /// <summary>Math baseline — mathematical operators centered at x-height.</summary>
    public const ushort MathBaseline = 4;

    /// <summary>Sentinel value in the control-point array indicating no control point for that slot.</summary>
    public const ushort NoControlPoint = 0xFFFF;

    // ── Format enum ───────────────────────────────────────────────────────────

    /// <summary>Discriminates the four bsln sub-table layouts.</summary>
    public enum BslnFormat : ushort
    {
        /// <summary>
        /// Distance-based: 32 FUnit deltas from each glyph's natural baseline.
        /// No per-glyph lookup. All glyphs use <see cref="DefaultBaseline"/>.
        /// </summary>
        DistanceNoMap = 0,

        /// <summary>
        /// Distance-based: 32 FUnit deltas, plus an AAT lookup table that maps
        /// individual glyph indices to baseline classes.
        /// </summary>
        DistanceWithMap = 1,

        /// <summary>
        /// Control-point-based: positions derived from control points on a designated
        /// standard glyph after hinting. No per-glyph lookup.
        /// </summary>
        ControlPointNoMap = 2,

        /// <summary>
        /// Control-point-based, plus an AAT lookup table that maps individual glyph
        /// indices to baseline classes.
        /// </summary>
        ControlPointWithMap = 3
    }

    // ── Header ────────────────────────────────────────────────────────────────

    /// <summary>Gets or sets the table version (fixed32). Default <c>0x00010000</c>.</summary>
    public int Version { get; set; } = 0x00010000;

    /// <summary>Gets or sets the sub-table format.</summary>
    public BslnFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the default baseline class (0–31) applied to all glyphs not covered
    /// by the per-glyph lookup (used by formats 1 and 3) or to all glyphs (formats 0 and 2).
    /// </summary>
    public ushort DefaultBaseline { get; set; }

    // ── Formats 0 and 1 ── distance-based ────────────────────────────────────

    /// <summary>
    /// Gets or sets the 32-element delta array (one Int16 FUnit value per baseline slot).
    /// Each element is the distance from the natural baseline of glyphs assigned to that
    /// baseline class to the baseline position itself.
    /// Applies to <see cref="BslnFormat.DistanceNoMap"/> and <see cref="BslnFormat.DistanceWithMap"/>.
    /// </summary>
    public short[] Deltas { get; set; } = new short[BaselineCount];

    // ── Formats 2 and 3 ── control-point-based ───────────────────────────────

    /// <summary>
    /// Gets or sets the glyph index of the standard glyph whose post-hinting control
    /// points define all baseline positions.
    /// Applies to <see cref="BslnFormat.ControlPointNoMap"/> and <see cref="BslnFormat.ControlPointWithMap"/>.
    /// </summary>
    public ushort StandardGlyph { get; set; }

    /// <summary>
    /// Gets or sets the 32-element control-point index array (one UInt16 per baseline slot).
    /// Each element is a control point index within <see cref="StandardGlyph"/>, or
    /// <see cref="NoControlPoint"/> (0xFFFF) if that baseline has no control point.
    /// Applies to <see cref="BslnFormat.ControlPointNoMap"/> and <see cref="BslnFormat.ControlPointWithMap"/>.
    /// </summary>
    public ushort[] ControlPoints { get; set; } = new ushort[BaselineCount];

    // ── Formats 1 and 3 ── per-glyph lookup ──────────────────────────────────

    /// <summary>
    /// Gets or sets the mapping from glyph index to baseline class (0–31).
    /// Glyphs absent from this map use <see cref="DefaultBaseline"/>.
    /// Applies to <see cref="BslnFormat.DistanceWithMap"/> and <see cref="BslnFormat.ControlPointWithMap"/>.
    /// Written as AAT lookup Format 6 (single table); all five common read formats are supported.
    /// </summary>
    public Dictionary<ushort, ushort> GlyphBaselineMap { get; set; } = [];

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="BslnTable"/> class.</summary>
    public BslnTable() : base(TableTypes.BSLN) { }

    // ── Length ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override int Length => Format switch
    {
        BslnFormat.DistanceNoMap       => 8 + 64,
        BslnFormat.DistanceWithMap     => 8 + 64 + LookupTableSize,
        BslnFormat.ControlPointNoMap   => 8 + 66,
        BslnFormat.ControlPointWithMap => 8 + 66 + LookupTableSize,
        _ => throw new InvalidOperationException($"Unknown bsln format {(ushort)Format}")
    };

    /// <summary>
    /// Byte size of the AAT lookup Format-6 table produced by <see cref="WriteData"/>.
    /// Includes a 12-byte binSrchHeader, N glyph-value pairs, and one 0xFFFF sentinel (each 4 bytes).
    /// </summary>
    private int LookupTableSize => 12 + (GlyphBaselineMap.Count + 1) * 4;

    // ── ReadData ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        // Header: 8 bytes
        Version = data.Read<Int32>();
        Format = (BslnFormat)data.Read<UInt16>();
        DefaultBaseline = data.Read<UInt16>();

        switch (Format)
        {
            case BslnFormat.DistanceNoMap:
            case BslnFormat.DistanceWithMap:
                ReadDeltas(data);
                break;
            case BslnFormat.ControlPointNoMap:
            case BslnFormat.ControlPointWithMap:
                ReadControlPoints(data);
                break;
            default:
                throw new InvalidDataException($"Unknown bsln format {(ushort)Format}");
        }

        if (Format == BslnFormat.DistanceWithMap || Format == BslnFormat.ControlPointWithMap)
            GlyphBaselineMap = ReadLookupTable(data);
    }

    /// <summary>Reads 32 consecutive Int16 delta values into <see cref="Deltas"/>.</summary>
    private void ReadDeltas(Reader data)
    {
        Deltas = new short[BaselineCount];
        for (int i = 0; i < BaselineCount; i++)
            Deltas[i] = data.Read<Int16>();
    }

    /// <summary>Reads the standard glyph index followed by 32 UInt16 control-point indices.</summary>
    private void ReadControlPoints(Reader data)
    {
        StandardGlyph = data.Read<UInt16>();
        ControlPoints = new ushort[BaselineCount];
        for (int i = 0; i < BaselineCount; i++)
            ControlPoints[i] = data.Read<UInt16>();
    }

    /// <summary>
    /// Reads an AAT lookup table at the current stream position and returns the resulting
    /// glyph-to-baseline-class map. Supports AAT lookup formats 0, 2, 4, 6, and 8.
    /// </summary>
    /// <param name="data">Reader positioned at the first byte of the lookup table (format field).</param>
    /// <returns>Dictionary mapping glyph indices to baseline class values.</returns>
    /// <exception cref="InvalidDataException">Thrown for unrecognised lookup format bytes.</exception>
    private static Dictionary<ushort, ushort> ReadLookupTable(Reader data)
    {
        var map = new Dictionary<ushort, ushort>();

        // Record the absolute start of the lookup table for Format-4 offset arithmetic
        long lookupStart = data.Position;
        ushort lookupFormat = data.Read<UInt16>();

        switch (lookupFormat)
        {
            case 0: // Simple array — one UInt16 per glyph from 0 to nUnits-1
            {
                data.Read<UInt16>(); // unitSize
                ushort nUnits = data.Read<UInt16>();
                data.Read<UInt16>(); data.Read<UInt16>(); data.Read<UInt16>(); // search params
                for (ushort g = 0; g < nUnits; g++)
                    map[g] = data.Read<UInt16>();
                break;
            }

            case 2: // Segment single — each segment [first..last] maps to one baseline class
            {
                data.Read<UInt16>(); // unitSize
                ushort nUnits = data.Read<UInt16>();
                data.Read<UInt16>(); data.Read<UInt16>(); data.Read<UInt16>(); // search params
                for (int i = 0; i < nUnits; i++)
                {
                    ushort lastGlyph  = data.Read<UInt16>();
                    ushort firstGlyph = data.Read<UInt16>();
                    ushort value      = data.Read<UInt16>();
                    if (lastGlyph == 0xFFFF) break;  // sentinel segment
                    for (ushort g = firstGlyph; g <= lastGlyph; g++)
                        map[g] = value;
                }
                break;
            }

            case 4: // Segment array — each segment has a byte offset (from lookupStart) to its value array
            {
                data.Read<UInt16>(); // unitSize
                ushort nUnits = data.Read<UInt16>();
                data.Read<UInt16>(); data.Read<UInt16>(); data.Read<UInt16>(); // search params
                var segments = new List<(ushort first, ushort last, ushort offset)>(nUnits);
                for (int i = 0; i < nUnits; i++)
                {
                    ushort lastGlyph   = data.Read<UInt16>();
                    ushort firstGlyph  = data.Read<UInt16>();
                    ushort valueOffset = data.Read<UInt16>();
                    if (lastGlyph == 0xFFFF) break;  // sentinel
                    segments.Add((firstGlyph, lastGlyph, valueOffset));
                }
                foreach (var (first, last, offset) in segments)
                {
                    // offset is a byte offset from the beginning of this lookup table
                    data.Push((int)(lookupStart + offset), SeekOrigin.Begin);
                    for (ushort g = first; g <= last; g++)
                        map[g] = data.Read<UInt16>();
                    data.Pop();
                }
                break;
            }

            case 6: // Single table — sorted (glyph, value) pairs terminated by 0xFFFF sentinel
            {
                data.Read<UInt16>(); // unitSize
                ushort nUnits = data.Read<UInt16>();
                data.Read<UInt16>(); data.Read<UInt16>(); data.Read<UInt16>(); // search params
                for (int i = 0; i < nUnits; i++)
                {
                    ushort glyph = data.Read<UInt16>();
                    ushort value = data.Read<UInt16>();
                    if (glyph == 0xFFFF) break;  // sentinel
                    map[glyph] = value;
                }
                break;
            }

            case 8: // Trimmed array — dense array for a contiguous range [firstGlyph..firstGlyph+count-1]
            {
                ushort firstGlyph  = data.Read<UInt16>();
                ushort glyphCount  = data.Read<UInt16>();
                for (ushort i = 0; i < glyphCount; i++)
                    map[(ushort)(firstGlyph + i)] = data.Read<UInt16>();
                break;
            }

            default:
                throw new InvalidDataException($"Unsupported AAT lookup format {lookupFormat}");
        }

        return map;
    }

    // ── WriteData ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        // Header: 8 bytes
        data.Write<Int32>(Version);
        data.Write<UInt16>((ushort)Format);
        data.Write<UInt16>(DefaultBaseline);

        switch (Format)
        {
            case BslnFormat.DistanceNoMap:
            case BslnFormat.DistanceWithMap:
                WriteDeltas(data);
                break;
            case BslnFormat.ControlPointNoMap:
            case BslnFormat.ControlPointWithMap:
                WriteControlPoints(data);
                break;
            default:
                throw new InvalidOperationException($"Unknown bsln format {(ushort)Format}");
        }

        if (Format == BslnFormat.DistanceWithMap || Format == BslnFormat.ControlPointWithMap)
            WriteLookupTableFormat6(data);
    }

    /// <summary>Writes exactly <see cref="BaselineCount"/> Int16 values from <see cref="Deltas"/>, padding with 0.</summary>
    private void WriteDeltas(Writer data)
    {
        for (int i = 0; i < BaselineCount; i++)
            data.Write<Int16>(i < Deltas.Length ? Deltas[i] : (short)0);
    }

    /// <summary>
    /// Writes <see cref="StandardGlyph"/> followed by exactly <see cref="BaselineCount"/> UInt16 values
    /// from <see cref="ControlPoints"/>, padding absent slots with <see cref="NoControlPoint"/>.
    /// </summary>
    private void WriteControlPoints(Writer data)
    {
        data.Write<UInt16>(StandardGlyph);
        for (int i = 0; i < BaselineCount; i++)
            data.Write<UInt16>(i < ControlPoints.Length ? ControlPoints[i] : NoControlPoint);
    }

    /// <summary>
    /// Writes <see cref="GlyphBaselineMap"/> as an AAT lookup Format 6 (single table):
    /// a 12-byte binSrchHeader, entries sorted by glyph index, and a 0xFFFF/0xFFFF sentinel.
    /// </summary>
    private void WriteLookupTableFormat6(Writer data)
    {
        var sorted   = GlyphBaselineMap.OrderBy(kv => kv.Key).ToArray();
        ushort nUnits   = (ushort)(sorted.Length + 1);  // sorted entries + sentinel
        const ushort unitSize = 4;

        // Binary-search header parameters (same formula as KernTable)
        ushort searchRange   = (ushort)(Math.Pow(2, Math.Floor(Math.Log2(nUnits))) * unitSize);
        ushort entrySelector = (ushort)Math.Floor(Math.Log2(nUnits));
        ushort rangeShift    = (ushort)(nUnits * unitSize - searchRange);

        // binSrchHeader (12 bytes)
        data.Write<UInt16>(6);              // lookup format
        data.Write<UInt16>(unitSize);
        data.Write<UInt16>(nUnits);
        data.Write<UInt16>(searchRange);
        data.Write<UInt16>(entrySelector);
        data.Write<UInt16>(rangeShift);

        // Sorted (glyph, baselineClass) pairs
        foreach (var kv in sorted)
        {
            data.Write<UInt16>(kv.Key);
            data.Write<UInt16>(kv.Value);
        }

        // Sentinel entry
        data.Write<UInt16>(0xFFFF);
        data.Write<UInt16>(0xFFFF);
    }
}
