using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Utils.Fonts.TTF.Parsing;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// Represents the 'cmap' table which provides the character-to-glyph mapping for a TrueType font.
/// The table contains one or more subtables (each identified by a platform ID and platform-specific ID)
/// that define different mapping formats.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6cmap.html" />
[TTFTable(TableTypes.Tags.CMAP)]
public class CmapTable : TrueTypeTable, IEnumerable<CMap.CMapFormatBase>
{
    /// <summary>
    /// Represents a subtable identifier defined by a platform ID and platform-specific ID.
    /// Used as a key for the cmap subtables.
    /// </summary>
    public sealed class CmapSubtable : IEquatable<CmapSubtable>, IComparable<CmapSubtable>
    {
        /// <summary>
        /// Gets the platform ID.
        /// </summary>
        public ushort PlatformID { get; }

        /// <summary>
        /// Gets the platform-specific ID.
        /// </summary>
        public ushort PlatformSpecificID { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CmapSubtable"/> class.
        /// </summary>
        /// <param name="platformID">The platform ID.</param>
        /// <param name="platformSpecificID">The platform-specific ID.</param>
        internal CmapSubtable(ushort platformID, ushort platformSpecificID)
        {
            PlatformID = platformID;
            PlatformSpecificID = platformSpecificID;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is CmapSubtable other && Equals(other);

        /// <inheritdoc/>
        public bool Equals(CmapSubtable? other) =>
            other != null && PlatformID == other.PlatformID && PlatformSpecificID == other.PlatformSpecificID;

        /// <inheritdoc/>
        public override int GetHashCode() => ObjectUtils.ComputeHash(PlatformID, PlatformSpecificID);

        /// <inheritdoc/>
        public int CompareTo(CmapSubtable? other)
        {
            if (other is null)
            {
                return 1;
            }
            if (this.Equals(other))
            {
                return 0;
            }

            // Prioritize the Microsoft Unicode subtable (PlatformID = 3, PlatformSpecificID = 1)
            if (this.PlatformID == 3 && this.PlatformSpecificID == 1)
            {
                return -1;
            }
            if (other.PlatformID == 3 && other.PlatformSpecificID == 1)
            {
                return 1;
            }

            // Additional tie-breakers can be added here if needed.
            int comparePlatform = this.PlatformID.CompareTo(other.PlatformID);
            if (comparePlatform != 0)
            {
                return comparePlatform;
            }
            return this.PlatformSpecificID.CompareTo(other.PlatformSpecificID);
        }
    }

    private SortedDictionary<CmapSubtable, CMap.CMapFormatBase> subtables;
    private IReadOnlyList<CMap.CMapFormatBase> cachedCMaps;

    /// <summary>
    /// Gets the cmap subtables, in priority order (Microsoft Unicode first, see
    /// <see cref="CmapSubtable.CompareTo"/>). Backed by an immutable snapshot: mutating the
    /// returned list is not possible, and it never reflects a later <see cref="AddCMap"/>/
    /// <see cref="RemoveCMap"/> call made after it was read -- call this property again to observe
    /// changes.
    /// </summary>
    public virtual IReadOnlyList<CMap.CMapFormatBase> CMaps => cachedCMaps ??= new ReadOnlyCollection<CMap.CMapFormatBase>(subtables.Values.ToArray());

    /// <summary>
    /// Gets a cmap subtable based on the specified platform ID and platform-specific ID.
    /// </summary>
    /// <param name="platformID">The platform ID.</param>
    /// <param name="platformSpecificID">The platform-specific ID.</param>
    /// <returns>The corresponding cmap format subtable if found; otherwise, null.</returns>
    public virtual CMap.CMapFormatBase GetCMap(ushort platformID, ushort platformSpecificID)
        => subtables.GetValueOrDefault(new CmapSubtable(platformID, platformSpecificID));

    /// <summary>
    /// Adds a cmap subtable to the table.
    /// </summary>
    /// <param name="platformID">The platform ID.</param>
    /// <param name="platformSpecificID">The platform-specific ID.</param>
    /// <param name="cm">The cmap format subtable.</param>
    public virtual void AddCMap(ushort platformID, ushort platformSpecificID, CMap.CMapFormatBase cm)
    {
        subtables[new CmapSubtable(platformID, platformSpecificID)] = cm;
        cachedCMaps = null;
    }

    /// <summary>
    /// Removes a cmap subtable based on the specified platform IDs.
    /// </summary>
    /// <param name="platformID">The platform ID.</param>
    /// <param name="platformSpecificID">The platform-specific ID.</param>
    public virtual void RemoveCMap(ushort platformID, ushort platformSpecificID)
    {
        subtables.Remove(new CmapSubtable(platformID, platformSpecificID));
        cachedCMaps = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CmapTable"/> class.
    /// </summary>
    protected internal CmapTable() : base(TableTypes.CMAP)
    {
        Version = 0;
        subtables = new SortedDictionary<CmapSubtable, CMap.CMapFormatBase>();
    }

    /// <summary>
    /// Gets or sets the version of the cmap table.
    /// </summary>
    public virtual ushort Version { get; set; }

    /// <summary>
    /// Gets the number of cmap subtables.
    /// </summary>
    public virtual ushort NumberSubtables => (ushort)subtables.Count;

    /// <summary>
    /// Gets the total length (in bytes) of the cmap table data.
    /// </summary>
    public override int Length
    {
        get
        {
            int num = 4; // version (2 bytes) + numberSubtables (2 bytes)
            num += subtables.Count * 8; // Each subtable record is 8 bytes.
            // Subtables that were parsed as a shared instance (multiple platform/encoding records
            // pointing at the same offset) must only be counted once towards the payload length.
            foreach (var cMap in subtables.Values.Distinct())
            {
                num += cMap.Length;
            }
            return num;
        }
    }

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        var context = TrueTypeFont?.ParsingContext;
        long cmapLength = data.BytesLeft;

        Version = data.Read<UInt16>();
        int numberSubtables = data.Read<UInt16>();

        var options = context?.Options ?? Parsing.TrueTypeFontParsingOptions.Default;
        if (numberSubtables > options.MaximumCmapSubtables)
        {
            FontParsingContext.Reject(FontDiagnosticCode.ResourceLimitExceeded,
                $"cmap declares {numberSubtables} subtables, exceeding MaximumCmapSubtables ({options.MaximumCmapSubtables}).",
                TableTypes.CMAP);
        }

        long directoryEnd = checked(4L + (long)numberSubtables * 8L);
        if (directoryEnd > cmapLength)
        {
            FontParsingContext.Reject(FontDiagnosticCode.InvalidDirectoryRange,
                $"cmap subtable directory ({directoryEnd} bytes for {numberSubtables} subtables) does not fit within the cmap table ({cmapLength} bytes).",
                TableTypes.CMAP);
        }

        // Read subtable directory records.
        var subTables = new (ushort platformID, ushort platformSpecificID, uint offset)[numberSubtables];
        for (int i = 0; i < numberSubtables; i++)
        {
            var platformID = data.Read<UInt16>();
            var platformSpecificID = data.Read<UInt16>();
            var offset = data.Read<UInt32>();
            if (offset < directoryEnd || offset >= cmapLength)
            {
                ReportOrReject(context, FontDiagnosticCode.InvalidDirectoryRange,
                    $"cmap subtable record #{i} (platformID={platformID}, platformSpecificID={platformSpecificID}) declares offset {offset}, outside the cmap table (valid range [{directoryEnd}, {cmapLength})).");
                continue;
            }
            subTables[i] = (platformID, platformSpecificID, offset);
        }

        // Subtables sharing the same offset (deliberately, per spec) are parsed exactly once and
        // the resulting instance is reused across every platform/encoding record that points at it.
        var parsedByOffset = new Dictionary<uint, CMap.CMapFormatBase>();

        // Read each subtable.
        for (int i = 0; i < numberSubtables; i++)
        {
            var subTable = subTables[i];
            if (subTable.offset == 0)
            {
                continue; // Dropped above (out-of-range offset): never a valid offset (always >= directoryEnd > 0).
            }

            if (!parsedByOffset.TryGetValue(subTable.offset, out var cMap))
            {
                // Bounded only by "the rest of cmap from this offset" -- never by the next
                // directory record's offset. Subtables are not guaranteed to be laid out
                // contiguously in offset order (padding, or a genuinely non-contiguous layout, can
                // sit between them), so a next-offset-derived bound could let a malformed or
                // hostile subtable's own reads run past its真 declared length into padding or an
                // unrelated subtable's bytes without ever failing. CMapFormatBase.GetMap reads the
                // format's own declared length from the wire and re-bounds to exactly that length
                // before dispatching to the format-specific reader.
                long available = cmapLength - subTable.offset;
                Reader mapData = data.Slice(subTable.offset, available);
                try
                {
                    cMap = CMap.CMapFormatBase.GetMap(mapData);
                }
                catch (Exception ex) when (ex is InvalidDataException or FormatException
                                               or NotSupportedException or ArgumentException
                                               or OverflowException or IndexOutOfRangeException
                                               or EndOfStreamException)
                {
                    ReportOrReject(context, FontDiagnosticCode.MalformedCmapSubtable,
                        $"cmap subtable at offset {subTable.offset} (platformID={subTable.platformID}, platformSpecificID={subTable.platformSpecificID}) is malformed: {ex.Message}");
                    continue;
                }
                if (cMap is not null)
                {
                    parsedByOffset[subTable.offset] = cMap;
                }
            }

            if (cMap is not null)
            {
                AddCMap(subTable.platformID, subTable.platformSpecificID, cMap);
            }
        }
    }

    /// <summary>
    /// Reports a diagnosable anomaly through <paramref name="context"/> when parsing runs under the
    /// normal font-parsing pipeline (throwing in strict mode, recording in permissive mode);
    /// otherwise -- a <see cref="CmapTable"/> read directly, outside <see cref="TrueTypeFont.ParseFont(System.IO.Stream, Parsing.TrueTypeFontParsingOptions)"/>
    /// -- always throws, matching this table's previous unconditional behavior for callers that
    /// never opted into a parsing context.
    /// </summary>
    private static void ReportOrReject(FontParsingContext context, FontDiagnosticCode code, string message)
    {
        if (context is null)
        {
            throw new InvalidDataException(message);
        }
        context.ReportError(code, message, TableTypes.CMAP);
    }

    /// <summary>
    /// Writes the cmap table data to the specified writer.
    /// </summary>
    /// <param name="data">The writer to which the data is written.</param>
    public override void WriteData(Writer data)
    {
        data.Write<UInt16>(Version);
        data.Write<UInt16>(NumberSubtables);
        int length = 4 + NumberSubtables * 8;
        var offsetsByMap = new Dictionary<CMap.CMapFormatBase, int>();
        foreach (var subTable in subtables)
        {
            CmapSubtable cmapSubtable = subTable.Key;
            CMap.CMapFormatBase cMap = subTable.Value;
            data.Write<UInt16>(cmapSubtable.PlatformID);
            data.Write<UInt16>(cmapSubtable.PlatformSpecificID);
            if (!offsetsByMap.TryGetValue(cMap, out int mapOffset))
            {
                mapOffset = length;
                offsetsByMap[cMap] = mapOffset;
                length += cMap.Length;
            }
            data.Write<UInt32>((uint)mapOffset);
        }
        foreach (var cMap in offsetsByMap.Keys)
        {
            cMap.WriteData(data);
        }
    }

    /// <summary>
    /// Returns a string representation of the cmap table.
    /// </summary>
    /// <returns>A string describing the cmap table details.</returns>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"    Version: {Version:X2}");
        sb.AppendLine($"    NumMaps: {NumberSubtables}");
        foreach (var subTable in subtables)
        {
            var cmapSubtable = subTable.Key;
            var cMap = subTable.Value;
            sb.Append($"    Map: platformID: {cmapSubtable.PlatformID} - PlatformSpecificID: {cmapSubtable.PlatformSpecificID} - ");
            sb.AppendLine(cMap.ToString());
        }
        return sb.ToString();
    }

    /// <inheritdoc/>
    public IEnumerator<CMap.CMapFormatBase> GetEnumerator() => CMaps.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
