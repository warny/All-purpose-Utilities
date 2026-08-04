using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.Fonts.TTF.Parsing;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'loca' table stores the offsets to the locations of the glyphs in the font relative to the beginning of the 'glyf' table.
/// Its purpose is to provide quick access to the data for a particular glyph. For example, in the standard Macintosh glyph ordering,
/// the character A is the 76th glyph in a font. The 'loca' table stores the offset from the start of the 'glyf' table to the position
/// at which the data for each glyph can be found.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6loca.html"/>
[TTFTable(TableTypes.Tags.LOCA, TableTypes.Tags.HEAD, TableTypes.Tags.MAXP)]
public class LocaTable : TrueTypeTable, IEnumerable<LocaRecord>
{
    /// <summary>
    /// The font's 'head' table, providing <see cref="HeadTable.IndexToLocFormat"/> to determine
    /// whether offsets are stored in short or long format.
    /// </summary>
    private HeadTable headTable;

    /// <summary>
    /// The font's 'maxp' table, providing the glyph count used to size <see cref="offsets"/>.
    /// </summary>
    private MaxpTable maxpTable;

    /// <summary>
    /// The glyph offsets into the 'glyf' table, always normalized to byte offsets regardless of
    /// whether they were read in short or long format. Has <see cref="GlyphCount"/> + 1 entries.
    /// Every entry is non-negative: short-format entries are widened from <see cref="ushort"/>
    /// before doubling (so <c>0x8000..0xFFFF</c> becomes <c>65536..131070</c>, not a negative
    /// value), and long-format entries are rejected up front if they exceed <see cref="int.MaxValue"/>.
    /// </summary>
    private int[] offsets;

    /// <summary>
    /// Gets the total number of glyphs in the font.
    /// </summary>
    public int GlyphCount => maxpTable.NumGlyphs;

    /// <summary>
    /// Gets the final loca entry (<c>offsets[GlyphCount]</c>), i.e. the declared total byte length
    /// of the 'glyf' table's glyph data. Used by <see cref="GlyfTable"/> to cross-check its own
    /// length against what 'loca' declares.
    /// </summary>
    internal int TotalGlyphDataLength => offsets[^1];

    /// <summary>
    /// Gets a value indicating whether this is a long format loca table.
    /// </summary>
    public virtual bool IsLongFormat => headTable.IndexToLocFormat == 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocaTable"/> class.
    /// </summary>
    protected internal LocaTable() : base(TableTypes.LOCA) { }

    /// <summary>
    /// Gets or sets the owning <see cref="TrueTypeFont"/>. When set, the required dependent tables are retrieved.
    /// </summary>
    public override TrueTypeFont TrueTypeFont
    {
        get => base.TrueTypeFont;
        protected internal set
        {
            base.TrueTypeFont = value;
            headTable = value.GetTable<HeadTable>(TableTypes.HEAD);
            maxpTable = value.GetTable<MaxpTable>(TableTypes.MAXP);
        }
    }

    /// <summary>
    /// Gets the offset and size of the glyph data for the glyph at the specified index.
    /// </summary>
    /// <param name="index">The zero-based glyph index.</param>
    /// <returns>
    /// A tuple where <c>offset</c> is the starting offset and <c>size</c> is the size (in bytes) of the glyph data.
    /// </returns>
    public (int offset, int size) this[int index]
    {
        get
        {
            index.ArgMustBeLesserThan(GlyphCount);
            return (offsets[index], offsets[index + 1] - offsets[index]);
        }
    }

    /// <summary>
    /// Gets the length (in bytes) of the loca table data. Does not, by itself, recompute
    /// <see cref="offsets"/> from 'glyf' -- see <see cref="PrepareForSerialization"/>, which must be
    /// called explicitly before this property is read for writing.
    /// </summary>
    public override int Length =>
        // In short format each offset is stored as a 16-bit value; in long format as a 32-bit value.
        IsLongFormat ? offsets.Length << 2 : offsets.Length << 1;

    /// <summary>
    /// Writes the loca table data to the specified writer, using the format selected by the most
    /// recent call to <see cref="PrepareForSerialization"/> (or, for a table that was only ever
    /// read, the format it was read in).
    /// </summary>
    /// <param name="data">The writer to which the table data is written.</param>
    public override void WriteData(Writer data)
    {
        if (IsLongFormat)
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                data.Write<UInt32>((uint)offsets[i]);
            }
        }
        else
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                data.Write<UInt16>((ushort)(offsets[i] >> 1));
            }
        }
    }

    /// <summary>
    /// Recomputes <see cref="offsets"/> from the actual, current lengths of the glyphs in the
    /// 'glyf' table, and selects the short or long 'loca' format accordingly, updating
    /// <see cref="HeadTable.IndexToLocFormat"/> to match. Must be called exactly once, before this
    /// table's <see cref="Length"/> is measured or its data is written, and before 'head' is
    /// serialized -- see <see cref="TrueTypeFont"/>'s <c>PrepareForSerialization</c> phase, which
    /// calls this up front so that no later property read has this side effect.
    /// </summary>
    /// <remarks>
    /// Without recomputing offsets, writing a font whose glyph encoding changed size since it was
    /// read (e.g. a component argument now encoded as a byte instead of a word) would serialize a
    /// 'glyf' table whose actual layout no longer matches the offsets declared here, corrupting
    /// every glyph after the first one whose size changed. Not declared as a dependency in this
    /// class's <see cref="TTFTableAttribute"/> because 'glyf' itself depends on 'loca' to be read --
    /// looking the table up on demand here (only needed for writing, long after both tables exist)
    /// avoids the circular read-time dependency that declaring it would create.
    /// </remarks>
    /// <exception cref="OverflowException">Thrown if the cumulative glyph data size overflows a 32-bit signed integer.</exception>
    internal void PrepareForSerialization()
    {
        if (!TrueTypeFont.TryGetTable<GlyfTable>(TableTypes.GLYF, out var glyfTable))
        {
            return;
        }

        checked
        {
            var refreshed = new int[GlyphCount + 1];
            int offset = 0;
            for (int i = 0; i < GlyphCount; i++)
            {
                refreshed[i] = offset;
                offset += glyfTable.GetGlyph(i)?.Length ?? 0;
            }
            refreshed[GlyphCount] = offset;
            offsets = refreshed;
        }

        // Short format requires every offset to be even (it stores offset/2) and representable in
        // 16 bits once halved; otherwise long format is required. Never throw here for a font that
        // is representable in long format -- silently upgrading is always safe.
        bool canUseShortFormat = true;
        foreach (var value in offsets)
        {
            if ((value & 1) != 0 || (value >> 1) > ushort.MaxValue)
            {
                canUseShortFormat = false;
                break;
            }
        }
        headTable.IndexToLocFormat = (short)(canUseShortFormat ? 0 : 1);
    }

    /// <summary>
    /// Reads the loca table data from the specified reader.
    /// </summary>
    /// <param name="data">The reader from which the table data is read.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the table does not contain exactly <see cref="GlyphCount"/> + 1 entries, when
    /// offsets decrease, or when a long-format offset cannot be represented as a non-negative
    /// 32-bit signed value.
    /// </exception>
    public override void ReadData(Reader data)
    {
        int expectedEntries = GlyphCount + 1;
        int[] read;
        if (IsLongFormat)
        {
            // Offset32: read as UInt32 and reject values that would overflow Int32 before
            // narrowing, rather than silently wrapping to a negative offset.
            var raw = data.ReadArray<uint>(expectedEntries);
            read = new int[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] > int.MaxValue)
                {
                    Reject($"Long-format loca offset {raw[i]} at index {i} exceeds this implementation's supported range.");
                }
                read[i] = (int)raw[i];
            }
        }
        else
        {
            // Offset16: the wire value is unsigned and represents offset/2. Widen to UInt16 first
            // -- not Int16 -- so 0x8000..0xFFFF map to 65536..131070 rather than negative offsets.
            var raw = data.ReadArray<ushort>(expectedEntries);
            read = new int[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                read[i] = raw[i] << 1;
            }
        }

        for (int i = 1; i < read.Length; i++)
        {
            if (read[i] < read[i - 1])
            {
                Reject($"loca offsets must be non-decreasing; entry {i} ({read[i]}) is less than entry {i - 1} ({read[i - 1]}).");
            }
        }

        // Policy-dependent anomalies: neither risks an out-of-bounds read on its own (the entry
        // count, and therefore every offset/size pair the indexer can produce, is already fixed at
        // this point), so strict rejects while permissive records a diagnostic and continues.
        if (data.BytesLeft != 0)
        {
            ReportOrReject(FontDiagnosticCode.InvalidLoca,
                $"loca table declares {data.BytesLeft} extra byte(s) beyond the {expectedEntries} expected entries.");
        }
        if (read.Length > 0 && read[0] != 0)
        {
            ReportOrReject(FontDiagnosticCode.InvalidLoca,
                $"loca table's first offset is {read[0]}, expected 0.");
        }

        offsets = read;
    }

    /// <summary>
    /// Always throws: a non-monotonic or unrepresentable 'loca' offset would let the indexer
    /// return a negative size, which is a memory-safety hazard rather than a policy choice --
    /// fatal in both <see cref="FontValidationMode.Strict"/> and <see cref="FontValidationMode.Permissive"/>.
    /// </summary>
    private void Reject(string message) => FontParsingContext.Reject(FontDiagnosticCode.InvalidLoca, message, TableTypes.LOCA);

    /// <summary>
    /// Reports a policy-level (non-memory-safety) 'loca' anomaly through the active parsing
    /// context when one is available (strict throws, permissive records and continues);
    /// otherwise -- a <see cref="LocaTable"/> read directly, outside <see cref="TrueTypeFont.ParseFont(System.IO.Stream, Parsing.TrueTypeFontParsingOptions)"/>
    /// -- always throws, matching the behavior of the always-fatal anomalies above for callers
    /// that never opted into a parsing context.
    /// </summary>
    private void ReportOrReject(FontDiagnosticCode code, string message)
    {
        var context = TrueTypeFont?.ParsingContext;
        if (context is null)
        {
            throw new InvalidDataException(message);
        }
        context.ReportError(code, message, TableTypes.LOCA);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the loca records.
    /// </summary>
    /// <returns>An enumerator of <see cref="LocaRecord"/>.</returns>
    public IEnumerator<LocaRecord> GetEnumerator()
    {
        IEnumerable<LocaRecord> EnumerateRecords()
        {
            for (int i = 0; i < offsets.Length - 1; i++)
            {
                yield return new LocaRecord(i, offsets[i], offsets[i + 1] - offsets[i]);
            }
        }
        return EnumerateRecords().GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the loca records.
    /// </summary>
    /// <returns>An enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
