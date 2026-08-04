namespace Utils.Fonts.TTF.Parsing;

/// <summary>
/// Identifies the kind of structural anomaly a <see cref="FontDiagnostic"/> reports.
/// </summary>
public enum FontDiagnosticCode
{
    /// <summary>The SFNT offset table (version, numTables, searchRange, entrySelector, rangeShift) is invalid or internally inconsistent.</summary>
    InvalidOffsetTable,

    /// <summary>The table directory (or a single directory entry) declares a range that does not fit within the font.</summary>
    InvalidDirectoryRange,

    /// <summary>Two or more directory entries declare the same table tag.</summary>
    DuplicateTableTag,

    /// <summary>Two directory entries declare the exact same offset and length (an exact alias).</summary>
    AliasedTableRange,

    /// <summary>Two directory entries declare ranges that partially or fully overlap without being an exact alias, or a table overlaps the directory itself.</summary>
    OverlappingTableRange,

    /// <summary>A table's declared checksum does not match its computed checksum.</summary>
    TableChecksumMismatch,

    /// <summary>The whole-font checksum (sum of all words, including 'head'.checksumAdjustment) does not equal the required magic number.</summary>
    FontChecksumMismatch,

    /// <summary>A directory entry's tag has no known table implementation and no way to validate its content beyond range checks.</summary>
    UnsupportedTable,

    /// <summary>A 'cmap' subtable could not be parsed because its content does not match its declared format.</summary>
    MalformedCmapSubtable,

    /// <summary>The 'loca' table's entry count, offsets, or relationship with 'glyf' is invalid.</summary>
    InvalidLoca,

    /// <summary>A compound glyph declares an invalid component (out-of-range glyph index, incompatible flags, a cycle, or a value that cannot be represented on the wire).</summary>
    InvalidCompositeGlyph,

    /// <summary>A configured resource limit (size, count, depth, ...) was exceeded. Always fatal, in both validation modes.</summary>
    ResourceLimitExceeded
}
