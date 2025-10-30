using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.Fonts.TTF.Tables.CMap;

/// <summary>
/// Represents a CMap Format 4 for mapping characters to glyph indices using segmented arrays.
/// </summary>
public class CMapFormat4 : CMapFormatBase
{
    /// <summary>
    /// Represents a segment in the CMap Format 4 mapping table.
    /// </summary>
    public abstract class Segment : IEquatable<Segment>, IComparable<Segment>
    {
        /// <summary>
        /// Gets the starting character code of the segment.
        /// </summary>
        internal char StartCode { get; }

        /// <summary>
        /// Gets the ending character code of the segment.
        /// </summary>
        internal char EndCode { get; }

        /// <summary>
        /// Gets a value indicating whether this segment provides an explicit mapping table.
        /// </summary>
        internal abstract bool HasMap { get; }

        /// <summary>
        /// Gets the length (in bytes) used by this segment's additional data.
        /// </summary>
        internal abstract int Length { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Segment"/> class.
        /// </summary>
        /// <param name="startCode">The starting character code.</param>
        /// <param name="endCode">The ending character code.</param>
        public Segment(char startCode, char endCode)
        {
            StartCode = startCode;
            EndCode = endCode;
        }

        /// <summary>
        /// Gets the mapped glyph index for the given glyph index value.
        /// </summary>
        /// <param name="index">The glyph index value to look up.</param>
        /// <returns>The corresponding character, or '\0' if not found.</returns>
        public abstract char this[short index] { get; }

        /// <summary>
        /// Gets the glyph index for the given character.
        /// </summary>
        /// <param name="c">The character to map.</param>
        /// <returns>The corresponding glyph index.</returns>
        public abstract short this[char c] { get; }

        /// <inheritdoc/>
        public bool Equals(Segment other) =>
            other != null && this.StartCode == other.StartCode && this.EndCode == other.EndCode;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Segment other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => ObjectUtils.ComputeHash(StartCode, EndCode);

        /// <inheritdoc/>
        public int CompareTo(Segment other) => this.StartCode.CompareTo(other?.StartCode);
    }

    /// <summary>
    /// Represents a segment with an explicit mapping table.
    /// </summary>
    public class TableMap : Segment
    {
        /// <summary>
        /// Gets the explicit mapping table for this segment.
        /// </summary>
        internal IReadOnlyList<short> Map { get; }

        private Dictionary<short, char> reverseMap;

        /// <inheritdoc/>
        internal override bool HasMap => true;

        /// <inheritdoc/>
        internal override int Length => 8 + Map.Count * sizeof(short);

        /// <summary>
        /// Initializes a new instance of the <see cref="TableMap"/> class.
        /// </summary>
        /// <param name="startCode">The starting character code.</param>
        /// <param name="endCode">The ending character code.</param>
        /// <param name="map">An array of glyph indices corresponding to the character codes.</param>
        public TableMap(char startCode, char endCode, short[] map)
            : base(startCode, endCode)
        {
            Map = map;
            // Build a reverse mapping from glyph index to character.
            reverseMap = new Dictionary<short, char>(
                map.Select((s, i) => new KeyValuePair<short, char>(s, (char)(i + startCode)))
                   .Where(kvp => kvp.Key != 0)
                   .GroupBy(kvp => kvp.Key)
                   .Select(g => g.First())
            );
        }

        /// <inheritdoc/>
        public override char this[short index] => reverseMap.TryGetValue(index, out var result) ? result : '\0';

        /// <inheritdoc/>
        public override short this[char c] => Map[c - StartCode];

        /// <inheritdoc/>
        public override string ToString() => "Table";
    }

    /// <summary>
    /// Represents a segment defined by a delta value (no explicit mapping table).
    /// </summary>
    public class DeltaMap : Segment
    {
        /// <summary>
        /// Gets the delta value applied to character codes in this segment.
        /// </summary>
        internal short Delta { get; }

        /// <inheritdoc/>
        internal override bool HasMap => false;

        /// <inheritdoc/>
        internal override int Length => 8;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeltaMap"/> class.
        /// </summary>
        /// <param name="startCode">The starting character code.</param>
        /// <param name="endCode">The ending character code.</param>
        /// <param name="delta">The delta value to apply.</param>
        public DeltaMap(char startCode, char endCode, short delta)
            : base(startCode, endCode) => Delta = delta;

        /// <inheritdoc/>
        public override char this[short index]
        {
            get
            {
                var result = (char)(index - Delta);
                return result.Between(StartCode, EndCode) ? result : '\0';
            }
        }

        /// <inheritdoc/>
        public override short this[char c] => (short)(c + Delta);

        /// <inheritdoc/>
        public override string ToString() => $"Delta: {Delta}";
    }

    private readonly SortedSet<Segment> segments;

    /// <summary>
    /// Initializes a new instance of the <see cref="CMapFormat4"/> class.
    /// </summary>
    /// <param name="language">The language identifier.</param>
    protected internal CMapFormat4(short language)
        : base(4, language)
    {
        segments = [];
    }

    /// <summary>
    /// Adds a segment defined by a delta value to this mapping.
    /// </summary>
    /// <param name="startCode">The starting character code of the segment.</param>
    /// <param name="endCode">The ending character code of the segment.</param>
    /// <param name="delta">The delta value for the segment.</param>
    public virtual void AddSegment(char startCode, char endCode, short delta)
    {
        Segment segment = new DeltaMap(startCode, endCode, delta);
        segments.Remove(segment);
        segments.Add(segment);
    }

    /// <summary>
    /// Adds a segment with an explicit mapping table to this mapping.
    /// </summary>
    /// <param name="startCode">The starting character code of the segment.</param>
    /// <param name="endCode">The ending character code of the segment.</param>
    /// <param name="map">An array of glyph indices corresponding to the character codes.</param>
    public virtual void AddSegment(char startCode, char endCode, short[] map)
    {
        map.ArgMustBeOfSize(endCode - startCode + 1);
        Segment segment = new TableMap(startCode, endCode, map);
        segments.Remove(segment);
        segments.Add(segment);
    }

    /// <summary>
    /// Removes a segment from this mapping.
    /// </summary>
    /// <param name="startCode">The starting character code of the segment.</param>
    /// <param name="endCode">The ending character code of the segment.</param>
    public virtual void RemoveSegment(char startCode, char endCode)
    {
        Segment segment = new DeltaMap(startCode, endCode, 0);
        segments.Remove(segment);
    }

    /// <inheritdoc/>
    public override short Length
    {
        get
        {
            int num = 16;
            num += segments.Count * 8;
            num += segments.Sum(s => s.Length);
            return (short)num;
        }
    }

    /// <summary>
    /// Gets the number of segments in this mapping.
    /// </summary>
    public virtual short SegmentCount => (short)segments.Count;

    /// <summary>
    /// Gets the search range used in the CMap header.
    /// </summary>
    public virtual short SearchRange
    {
        get
        {
            double pow = Math.Floor(Math.Log(SegmentCount, 2));
            double pow2 = Math.Pow(2, pow);
            return (short)(2 * pow2);
        }
    }

    /// <summary>
    /// Gets the entry selector used in the CMap header.
    /// </summary>
    public virtual short EntrySelector
    {
        get
        {
            int sr2 = SearchRange / 2;
            return (short)Math.Log(sr2, 2);
        }
    }

    /// <summary>
    /// Gets the range shift used in the CMap header.
    /// </summary>
    public virtual short RangeShift => (short)(2 * SegmentCount - SearchRange);

    /// <inheritdoc/>
    public override short Map(char ch)
    {
        // Iterate over segments to find one that covers the given character.
        foreach (var segment in segments)
        {
            if (ch >= segment.StartCode && ch <= segment.EndCode)
            {
                return segment[ch];
            }
        }
        return 0;
    }

    /// <inheritdoc/>
    public override char ReverseMap(short s)
    {
        // Iterate over segments to find the first segment that reverse-maps the glyph index.
        foreach (var segment in segments)
        {
            var result = segment[s];
            if (result != '\0')
            {
                return result;
            }
        }
        return '\0';
    }

    /// <inheritdoc/>
    public override void ReadData(int length, Reader data)
    {
        int segCount = (short)(data.Read<Int16>() << 1);
        // Read header fields (length, language, etc.); here we ignore some fields.
        data.Read<Int16>(); // overall length (unused)
        data.Read<Int16>(); // language
        data.Read<Int16>(); // segCountX2 (already used)

        short[] endCodes = new short[segCount];
        for (int i = 0; i < segCount; i++)
        {
            endCodes[i] = data.Read<Int16>();
        }
        data.Read<Int16>(); // reservedPad

        short[] startCodes = new short[segCount];
        for (int i = 0; i < segCount; i++)
        {
            startCodes[i] = data.Read<Int16>();
        }

        short[] idDeltas = new short[segCount];
        for (int i = 0; i < segCount; i++)
        {
            idDeltas[i] = data.Read<Int16>();
        }

        short[] idRangeOffsets = new short[segCount];
        for (int i = 0; i < segCount; i++)
        {
            idRangeOffsets[i] = data.Read<Int16>();
            if (idRangeOffsets[i] <= 0)
            {
                AddSegment((char)startCodes[i], (char)endCodes[i], idDeltas[i]);
                continue;
            }
            int offset = (int)data.Position - 2 + idRangeOffsets[i];
            int size = endCodes[i] - startCodes[i] + 1;
            data.Push();
            data.Seek(offset, SeekOrigin.Begin);
            var map = data.ReadArray<short>(size, true);
            data.Pop();
            AddSegment((char)startCodes[i], (char)endCodes[i], map);
        }
    }

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<Int16>(Format);
        data.Write<Int16>(Length);
        data.Write<Int16>(Language);
        data.Write<Int16>((short)(SegmentCount >> 1));
        data.Write<Int16>(SearchRange);
        data.Write<Int16>(EntrySelector);
        data.Write<Int16>(RangeShift);

        // Write start codes for each segment.
        foreach (Segment segment in segments)
        {
            data.Write<Int16>((short)segment.StartCode);
        }
        data.Write<Int16>(0);

        // Write end codes for each segment.
        foreach (Segment segment in segments)
        {
            data.Write<Int16>((short)segment.EndCode);
        }

        // Write idDeltas or 0 if a mapping table is present.
        foreach (var segment in segments)
        {
            if (segment is DeltaMap deltaMap)
            {
                data.Write<Int16>(deltaMap.Delta);
            }
            else if (segment is TableMap)
            {
                data.Write<Int16>(0);
            }
        }

        int glyphArrayOffset = 16 + 8 * SegmentCount;
        // Write glyph array offsets or mapping table data.
        foreach (var segment in segments)
        {
            if (segment is TableMap tableMap)
            {
                data.Write<Int16>((short)(glyphArrayOffset - data.Stream.Position));
                data.Push();
                data.Seek(glyphArrayOffset, SeekOrigin.Begin);
                foreach (var index in tableMap.Map)
                {
                    data.Write<Byte>((byte)index);
                }
                data.Pop();
                glyphArrayOffset += tableMap.Map.Count * 2;
            }
            else
            {
                data.Write<Int16>(0);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder result = new StringBuilder();
        result.Append(base.ToString());
        result.AppendLine($"    SegmentCount : {SegmentCount}");
        result.AppendLine($"    SearchRange  : {SearchRange}");
        result.AppendLine($"    EntrySelector: {EntrySelector}");
        result.AppendLine($"    RangeShift   : {RangeShift}");

        foreach (var segment in segments)
        {
            result.Append($"        Segment: '{segment.StartCode}'{(short)segment.StartCode:X2}-'{segment.EndCode}'{(short)segment.EndCode:X2} hasMap: {segment.HasMap} => {segment}");
            result.AppendLine();
        }
        return result.ToString();
    }
}
