using System;
using System.IO;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'trak' (Tracking) table specifies inter-character spacing adjustments as a function of
/// point size. For each nominal tracking level (e.g. "loose", "normal", "tight") a set of
/// per-size values (in font design units) gives the amount to add to each glyph's advance width.
/// </summary>
/// <remarks>
/// <para>The table may have separate horizontal and vertical tracking data, each consisting of
/// a list of <see cref="TrackEntry"/> records (one per tracking level) and a list of reference
/// point sizes. The per-size spacing values for each entry are stored in a parallel 2-D array.</para>
/// </remarks>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6trak.html"/>
[TTFTable(TableTypes.Tags.TRAK)]
public class TrakTable : TrueTypeTable
{
    // ── Nested types ──────────────────────────────────────────────────────

    /// <summary>
    /// One entry in the tracking table: a nominal tracking value and a name-table entry,
    /// with per-size spacing adjustments.
    /// </summary>
    public sealed class TrackEntry
    {
        /// <summary>
        /// Gets or sets the nominal tracking value as a 16.16 fixed-point number.
        /// Negative = tighter, positive = looser, 0 = normal.
        /// </summary>
        public int Track { get; set; }

        /// <summary>Gets or sets the name-table ID for the human-readable tracking name.</summary>
        public ushort NameIndex { get; set; }

        /// <summary>
        /// Gets or sets the per-size spacing adjustments in font design units,
        /// in the same order as the <see cref="TrackData.Sizes"/> array.
        /// </summary>
        public short[] PerSizeValues { get; set; } = [];
    }

    /// <summary>One direction's worth of tracking data (horizontal or vertical).</summary>
    public sealed class TrackData
    {
        /// <summary>Gets or sets the reference point sizes (16.16 fixed-point) for which tracking values are given.</summary>
        public int[] Sizes { get; set; } = [];

        /// <summary>Gets or sets the tracking entries (one per nominal tracking level).</summary>
        public TrackEntry[] Entries { get; set; } = [];
    }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="TrakTable"/> class.</summary>
    public TrakTable() : base(TableTypes.TRAK) { }

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Gets or sets the table version (0x00010000 = version 1.0).</summary>
    public int Version { get; set; } = 0x00010000;

    /// <summary>Gets or sets the table format (always 0).</summary>
    public ushort Format { get; set; }

    /// <summary>Gets or sets the horizontal tracking data, or <see langword="null"/> if absent.</summary>
    public TrackData HorizData { get; set; }

    /// <summary>Gets or sets the vertical tracking data, or <see langword="null"/> if absent.</summary>
    public TrackData VertData { get; set; }

    // ── Length ────────────────────────────────────────────────────────────

    private static int TrackDataSize(TrackData td)
    {
        if (td == null) return 0;
        int nTracks = td.Entries.Length;
        int nSizes  = td.Sizes.Length;
        // nTracks(2) + nSizes(2) + sizeTableOffset(4)
        // + nTracks × 8 (TrackTableEntry: Fixed track + UInt16 nameIndex + UInt16 offset)
        // + nSizes × 4 (Fixed size entries)
        // + nTracks × nSizes × 2 (Int16 per-size values)
        return 8 + nTracks * 8 + nSizes * 4 + nTracks * nSizes * 2;
    }

    /// <inheritdoc/>
    public override int Length
    {
        get
        {
            // Outer header: Fixed(4) + format(2) + horizOffset(2) + vertOffset(2) + reserved(2) = 12
            int size = 12;
            size += TrackDataSize(HorizData);
            size += TrackDataSize(VertData);
            return size;
        }
    }

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version = data.Read<Int32>();
        Format  = data.Read<UInt16>();
        ushort horizOffset = data.Read<UInt16>();
        ushort vertOffset  = data.Read<UInt16>();
        data.Read<UInt16>(); // reserved

        HorizData = horizOffset != 0 ? ReadTrackData(data, horizOffset) : null;
        VertData  = vertOffset  != 0 ? ReadTrackData(data, vertOffset)  : null;
    }

    private static TrackData ReadTrackData(Reader data, int offset)
    {
        data.Push(offset, SeekOrigin.Begin);

        long tdStart  = data.Position;
        ushort nTracks = data.Read<UInt16>();
        ushort nSizes  = data.Read<UInt16>();
        uint sizeTableOffset = data.Read<UInt32>(); // from start of TrackData

        // Track table entries (12 bytes each)
        var rawEntries = new (int track, ushort nameIndex, ushort valOffset)[nTracks];
        for (int i = 0; i < nTracks; i++)
        {
            rawEntries[i] = (
                data.Read<Int32>(),    // track (Fixed)
                data.Read<UInt16>(),   // nameIndex
                data.Read<UInt16>()    // offset to per-size values (from TrackData start)
            );
        }

        // Size table (Fixed values)
        data.Push((int)(tdStart + sizeTableOffset), SeekOrigin.Begin);
        int[] sizes = new int[nSizes];
        for (int i = 0; i < nSizes; i++)
            sizes[i] = data.Read<Int32>();
        data.Pop();

        // Per-entry values
        var entries = new TrackEntry[nTracks];
        for (int i = 0; i < nTracks; i++)
        {
            data.Push((int)(tdStart + rawEntries[i].valOffset), SeekOrigin.Begin);
            short[] vals = new short[nSizes];
            for (int s = 0; s < nSizes; s++)
                vals[s] = data.Read<Int16>();
            data.Pop();

            entries[i] = new TrackEntry
            {
                Track         = rawEntries[i].track,
                NameIndex     = rawEntries[i].nameIndex,
                PerSizeValues = vals,
            };
        }

        data.Pop(); // pop the outer Push(offset)

        return new TrackData { Sizes = sizes, Entries = entries };
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        // Outer header: version(4) + format(2) + horizOffset(2) + vertOffset(2) + reserved(2)
        data.Write<Int32>(Version);
        data.Write<UInt16>(Format);

        ushort horizOffset = HorizData != null ? (ushort)12 : (ushort)0;
        ushort vertOffset  = VertData  != null ? (ushort)(12 + TrackDataSize(HorizData)) : (ushort)0;

        data.Write<UInt16>(horizOffset);
        data.Write<UInt16>(vertOffset);
        data.Write<UInt16>(0); // reserved

        if (HorizData != null) WriteTrackData(data, HorizData);
        if (VertData  != null) WriteTrackData(data, VertData);
    }

    private static void WriteTrackData(Writer data, TrackData td)
    {
        int nTracks = td.Entries.Length;
        int nSizes  = td.Sizes.Length;

        // Offsets within the TrackData block:
        // header: nTracks(2)+nSizes(2)+sizeTableOffset(4) = 8 bytes
        // track table entries: nTracks × 8 bytes
        // size table: nSizes × 4 bytes
        // per-size value blocks: nTracks × nSizes × 2 bytes
        int sizeTableOffset = 8 + nTracks * 8;                // from TrackData start
        int firstValOffset  = sizeTableOffset + nSizes * 4;   // from TrackData start

        data.Write<UInt16>((ushort)nTracks);
        data.Write<UInt16>((ushort)nSizes);
        data.Write<UInt32>((uint)sizeTableOffset);

        // Track table entries
        for (int i = 0; i < nTracks; i++)
        {
            data.Write<Int32>(td.Entries[i].Track);
            data.Write<UInt16>(td.Entries[i].NameIndex);
            data.Write<UInt16>((ushort)(firstValOffset + i * nSizes * 2));
        }

        // Size table
        foreach (int sz in td.Sizes)
            data.Write<Int32>(sz);

        // Per-size value blocks (row-major: entry × size)
        foreach (var entry in td.Entries)
        {
            short[] vals = entry.PerSizeValues;
            for (int s = 0; s < nSizes; s++)
                data.Write<Int16>(s < vals.Length ? vals[s] : (short)0);
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version : {Version:X8}");
        sb.AppendLine($"    Horiz   : {(HorizData != null ? $"{HorizData.Entries.Length} tracks, {HorizData.Sizes.Length} sizes" : "absent")}");
        sb.AppendLine($"    Vert    : {(VertData  != null ? $"{VertData.Entries.Length} tracks, {VertData.Sizes.Length} sizes"  : "absent")}");
        return sb.ToString();
    }
}
