using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The CVT variations table (tag: 'cvar') provides delta adjustments to CVT values for different
/// instances of a variable font. It uses the tuple variation store format: each <em>tuple variation</em>
/// covers one region of the variation space and carries packed deltas for the CVT entries it affects.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AxisCount"/> must be set (typically from the 'fvar' table) before calling
/// <see cref="ReadData"/>, because each tuple variation header contains a peak coordinate tuple
/// whose byte length is <c>AxisCount × 2</c>.
/// </para>
/// </remarks>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6cvar.html"/>
/// <see href="https://learn.microsoft.com/en-us/typography/opentype/spec/cvar"/>
[TTFTable(TableTypes.Tags.CVAR)]
public class CvarTable : TrueTypeTable
{
    // ── tupleVariationCount flags ─────────────────────────────────────────────

    /// <summary>Flag in <c>tupleVariationCount</c>: shared packed point numbers precede tuple data.</summary>
    private const ushort SharedPointNumbers = 0x8000;

    // ── tupleIndex flags ──────────────────────────────────────────────────────

    /// <summary>Flag in <c>tupleIndex</c>: an embedded peak tuple record follows the index word (mandatory in 'cvar').</summary>
    private const ushort EmbeddedPeakTuple = 0x8000;

    /// <summary>Flag in <c>tupleIndex</c>: intermediate-region start/end tuples follow the peak tuple.</summary>
    private const ushort IntermediateRegion = 0x4000;

    /// <summary>Flag in <c>tupleIndex</c>: private packed point numbers are embedded in this tuple's serialized data.</summary>
    private const ushort PrivatePointNumbers = 0x2000;

    // ── Packed-point-number control byte ──────────────────────────────────────

    /// <summary>If set in a point-number run's control byte, each element is stored as UInt16 (else UInt8).</summary>
    private const byte PointsAreWords = 0x80;

    /// <summary>Mask for the low 7 bits of a point-number run's control byte (element count – 1).</summary>
    private const byte PointRunCountMask = 0x7F;

    // ── Packed-delta control byte ─────────────────────────────────────────────

    /// <summary>If set in a delta run's control byte, all deltas in the run are zero (no data bytes follow).</summary>
    private const byte DeltasAreZero = 0x80;

    /// <summary>If set (and <see cref="DeltasAreZero"/> is clear) in a delta run's control byte, each delta is Int16 (else Int8).</summary>
    private const byte DeltasAreWords = 0x40;

    /// <summary>Mask for the low 6 bits of a delta run's control byte (delta count – 1).</summary>
    private const byte DeltaRunCountMask = 0x3F;

    // ── Nested data types ─────────────────────────────────────────────────────

    /// <summary>
    /// One tuple variation record: covers one region of the variation space and carries the CVT
    /// deltas to apply when a font instance falls inside that region.
    /// </summary>
    public sealed class TupleVariation
    {
        /// <summary>
        /// Gets the peak coordinates of the variation region, one F2Dot14 value per design axis.
        /// F2Dot14 is a signed 2.14 fixed-point number: divide by 16 384 to get the real value
        /// (e.g. <c>0x4000</c> = 1.0, <c>0x0000</c> = 0.0, <c>0xC000</c> = –1.0).
        /// Length must equal <see cref="AxisCount"/>.
        /// </summary>
        public short[] PeakCoords { get; init; }

        /// <summary>
        /// Gets the start coordinates of an intermediate region, or <see langword="null"/> if this is
        /// a non-intermediate (peak-only) region. Length must equal <see cref="AxisCount"/> when set.
        /// </summary>
        public short[] IntermedStartCoords { get; init; }

        /// <summary>
        /// Gets the end coordinates of an intermediate region, or <see langword="null"/> if this is
        /// a non-intermediate region. Length must equal <see cref="AxisCount"/> when set.
        /// </summary>
        public short[] IntermedEndCoords { get; init; }

        /// <summary>
        /// Gets or sets the CVT indices to which <see cref="Deltas"/> apply, or <see langword="null"/>
        /// to indicate that deltas apply to <em>all</em> CVT entries (in order).
        /// </summary>
        public int[] PointNumbers { get; set; }

        /// <summary>
        /// Gets or sets the delta values (signed 16-bit) to add to each targeted CVT entry.
        /// When <see cref="PointNumbers"/> is <see langword="null"/>, the array is indexed directly
        /// by CVT index; otherwise <c>Deltas[i]</c> applies to <c>PointNumbers[i]</c>.
        /// </summary>
        public short[] Deltas { get; set; }

        /// <summary>Initializes a new <see cref="TupleVariation"/>.</summary>
        /// <param name="peakCoords">Peak coordinates (F2Dot14, one per axis). Must not be null.</param>
        /// <param name="intermedStartCoords">Intermediate start coordinates, or null.</param>
        /// <param name="intermedEndCoords">Intermediate end coordinates, or null.</param>
        /// <param name="pointNumbers">CVT indices, or null for all CVTs.</param>
        /// <param name="deltas">Delta values to apply.</param>
        public TupleVariation(
            short[] peakCoords,
            short[] intermedStartCoords,
            short[] intermedEndCoords,
            int[]   pointNumbers,
            short[] deltas)
        {
            PeakCoords         = peakCoords         ?? throw new ArgumentNullException(nameof(peakCoords));
            IntermedStartCoords = intermedStartCoords;
            IntermedEndCoords  = intermedEndCoords;
            PointNumbers       = pointNumbers;
            Deltas             = deltas             ?? [];
        }
    }

    // ── Public properties ─────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the major version of the table (must be 1).
    /// </summary>
    public ushort MajorVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minor version of the table (must be 0).
    /// </summary>
    public ushort MinorVersion { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of design axes. Must be set to match the 'fvar' axis count
    /// before calling <see cref="ReadData"/>; used to determine the size of each peak/intermediate tuple record.
    /// </summary>
    public int AxisCount { get; set; }

    /// <summary>
    /// Gets or sets the array of tuple variation records, each describing CVT deltas for one
    /// region of the variation space.
    /// </summary>
    public TupleVariation[] TupleVariations { get; set; } = [];

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="CvarTable"/> class.</summary>
    public CvarTable() : base(TableTypes.CVAR) { }

    // ── Length ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override int Length
    {
        get
        {
            // 8-byte header
            int size = 8;

            // Tuple variation headers
            foreach (var tv in TupleVariations)
                size += TupleHeaderSize(tv);

            // Serialized data block
            foreach (var tv in TupleVariations)
                size += TupleSerializedSize(tv);

            return size;
        }
    }

    /// <summary>Computes the byte size of a single <see cref="TupleVariation"/>'s header.</summary>
    private int TupleHeaderSize(TupleVariation tv)
    {
        // variationDataSize(2) + tupleIndex(2) + peak(axisCount*2)
        int size = 4 + AxisCount * 2;
        if (tv.IntermedStartCoords != null)
            size += AxisCount * 4;  // start + end tuples
        return size;
    }

    /// <summary>Computes the byte size of a single <see cref="TupleVariation"/>'s serialized data.</summary>
    private int TupleSerializedSize(TupleVariation tv)
        => PackedPointNumbersSize(tv.PointNumbers) + PackedDeltasSize(tv.Deltas);

    // ── ReadData ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="AxisCount"/> has not been set (is zero).
    /// </exception>
    /// <exception cref="InvalidDataException">Thrown for malformed table data.</exception>
    public override void ReadData(Reader data)
    {
        if (AxisCount <= 0)
            throw new InvalidOperationException(
                "AxisCount must be set to the font's axis count (from the 'fvar' table) before calling ReadData.");

        // Header (8 bytes)
        MajorVersion = data.Read<UInt16>();
        MinorVersion = data.Read<UInt16>();
        ushort rawCount  = data.Read<UInt16>();
        ushort dataOffset = data.Read<UInt16>();

        bool hasSharedPoints = (rawCount & SharedPointNumbers) != 0;
        int  count = rawCount & 0x0FFF;

        if (count == 0)
        {
            TupleVariations = [];
            return;
        }

        // Read all tuple variation headers
        var hdrs = new (int dataSize, short[] peak, short[] start, short[] end, bool privatePoints)[count];
        for (int i = 0; i < count; i++)
        {
            ushort variationDataSize = data.Read<UInt16>();
            ushort tupleIndex        = data.Read<UInt16>();

            bool hasEmbedded  = (tupleIndex & EmbeddedPeakTuple)    != 0; // always true in cvar
            bool hasIntermed  = (tupleIndex & IntermediateRegion)   != 0;
            bool hasPrivate   = (tupleIndex & PrivatePointNumbers)  != 0;

            short[] peak  = null;
            short[] start = null;
            short[] end   = null;

            if (hasEmbedded)
            {
                peak = new short[AxisCount];
                for (int a = 0; a < AxisCount; a++)
                    peak[a] = data.Read<Int16>();
            }

            if (hasIntermed)
            {
                start = new short[AxisCount];
                end   = new short[AxisCount];
                for (int a = 0; a < AxisCount; a++)
                    start[a] = data.Read<Int16>();
                for (int a = 0; a < AxisCount; a++)
                    end[a] = data.Read<Int16>();
            }

            hdrs[i] = (variationDataSize, peak, start, end, hasPrivate);
        }

        // Move to the serialized data block
        data.Push((int)dataOffset, SeekOrigin.Begin);

        // Optional shared point numbers at the start of the serialized data block
        int[] sharedPoints = null;
        if (hasSharedPoints)
            sharedPoints = ReadPackedPointNumbers(data);

        // Read per-tuple serialized data
        var variations = new TupleVariation[count];
        for (int i = 0; i < count; i++)
        {
            var (dataSize, peak, start, end, hasPrivate) = hdrs[i];
            long tupleDataStart = data.Position;

            int[] points;
            if (hasPrivate)
                points = ReadPackedPointNumbers(data);
            else
                points = sharedPoints;

            // Remaining bytes in this tuple's data block are packed deltas
            int deltaBytes = dataSize - (int)(data.Position - tupleDataStart);
            short[] deltas = ReadPackedDeltas(data, deltaBytes);

            // Advance past any unread padding (defensive)
            data.Position = tupleDataStart + dataSize;

            variations[i] = new TupleVariation(peak, start, end, points, deltas);
        }

        data.Pop();
        TupleVariations = variations;
    }

    /// <summary>
    /// Reads packed point numbers at the current stream position.
    /// Returns <see langword="null"/> when the count byte is 0 (meaning "all CVTs").
    /// </summary>
    private static int[] ReadPackedPointNumbers(Reader data)
    {
        byte b0 = (byte)data.ReadByte();
        if (b0 == 0)
            return null;  // "all CVTs"

        int count;
        if ((b0 & 0x80) != 0)
        {
            // Two-byte count: high bits from b0 & 0x7F, low byte follows
            byte b1 = (byte)data.ReadByte();
            count = ((b0 & 0x7F) << 8) | b1;
        }
        else
            count = b0;

        var points = new int[count];
        int last   = 0;
        int filled = 0;

        while (filled < count)
        {
            byte cb     = (byte)data.ReadByte();
            bool words  = (cb & PointsAreWords) != 0;
            int  runLen = (cb & PointRunCountMask) + 1;

            for (int r = 0; r < runLen && filled < count; r++)
            {
                int delta = words ? data.Read<UInt16>() : data.ReadByte();
                last += delta;
                points[filled++] = last;
            }
        }

        return points;
    }

    /// <summary>
    /// Reads <paramref name="byteCount"/> bytes of packed delta data and returns the decoded values.
    /// Reads until the byte budget is exhausted; the count of decoded deltas is determined by the data.
    /// </summary>
    private static short[] ReadPackedDeltas(Reader data, int byteCount)
    {
        if (byteCount <= 0)
            return [];

        var    result   = new List<short>();
        long   startPos = data.Position;

        while (data.Position < startPos + byteCount)
        {
            byte cb       = (byte)data.ReadByte();
            int  runCount = (cb & DeltaRunCountMask) + 1;

            if ((cb & DeltasAreZero) != 0)
            {
                // Run of zero deltas — no data bytes follow
                for (int r = 0; r < runCount; r++)
                    result.Add(0);
            }
            else if ((cb & DeltasAreWords) != 0)
            {
                // Run of Int16 deltas
                for (int r = 0; r < runCount; r++)
                    result.Add(data.Read<Int16>());
            }
            else
            {
                // Run of Int8 deltas (sign-extended to Int16)
                for (int r = 0; r < runCount; r++)
                    result.Add((short)(sbyte)(byte)data.ReadByte());
            }
        }

        return [.. result];
    }

    // ── WriteData ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        int count = TupleVariations.Length;

        // Compute dataOffset = 8 (header) + sum of all tuple header sizes
        int headerBlockSize = 8 + TupleVariations.Sum(tv => TupleHeaderSize(tv));
        ushort dataOffset   = (ushort)headerBlockSize;

        // Header (8 bytes)
        // We always use PRIVATE_POINT_NUMBERS per tuple — no shared point numbers.
        data.Write<UInt16>(MajorVersion);
        data.Write<UInt16>(MinorVersion);
        data.Write<UInt16>((ushort)count);   // tupleVariationCount, no SHARED_POINT_NUMBERS flag
        data.Write<UInt16>(dataOffset);

        // Tuple variation headers
        foreach (var tv in TupleVariations)
        {
            bool hasIntermed = tv.IntermedStartCoords != null;
            ushort tupleIndex = (ushort)(EmbeddedPeakTuple | PrivatePointNumbers
                                        | (hasIntermed ? IntermediateRegion : 0));

            data.Write<UInt16>((ushort)TupleSerializedSize(tv));  // variationDataSize
            data.Write<UInt16>(tupleIndex);

            // Peak tuple (mandatory in cvar)
            for (int a = 0; a < AxisCount; a++)
                data.Write<Int16>(a < tv.PeakCoords.Length ? tv.PeakCoords[a] : (short)0);

            // Intermediate start/end tuples (optional)
            if (hasIntermed)
            {
                for (int a = 0; a < AxisCount; a++)
                    data.Write<Int16>(a < tv.IntermedStartCoords.Length ? tv.IntermedStartCoords[a] : (short)0);
                for (int a = 0; a < AxisCount; a++)
                    data.Write<Int16>(a < tv.IntermedEndCoords.Length  ? tv.IntermedEndCoords[a]   : (short)0);
            }
        }

        // Serialized data block (one block per tuple, PRIVATE_POINT_NUMBERS always set)
        foreach (var tv in TupleVariations)
        {
            WritePackedPointNumbers(data, tv.PointNumbers);
            WritePackedDeltas(data, tv.Deltas);
        }
    }

    // ── Packed point number helpers ───────────────────────────────────────────

    /// <summary>Writes packed point numbers for a single tuple variation.</summary>
    /// <param name="data">Target writer.</param>
    /// <param name="points">CVT indices, or <see langword="null"/> to encode "all CVTs".</param>
    private static void WritePackedPointNumbers(Writer data, int[] points)
    {
        if (points == null)
        {
            data.WriteByte(0x00);   // count 0 = all CVTs
            return;
        }

        // Write count (1 or 2 bytes)
        int count = points.Length;
        if (count <= 0x7F)
            data.WriteByte((byte)count);
        else
        {
            data.WriteByte((byte)(0x80 | (count >> 8)));
            data.WriteByte((byte)(count & 0xFF));
        }

        if (count == 0)
            return;

        // Compute delta-from-previous values and check if any exceed 255
        int prev = 0;
        bool useWords = false;
        var deltas = new int[count];
        for (int i = 0; i < count; i++)
        {
            deltas[i] = points[i] - prev;
            if (deltas[i] > 255) useWords = true;
            prev = points[i];
        }

        // Write runs of up to 128 elements
        int idx = 0;
        while (idx < count)
        {
            int runLen = Math.Min(128, count - idx);
            data.WriteByte((byte)((useWords ? PointsAreWords : 0) | (runLen - 1)));
            for (int k = idx; k < idx + runLen; k++)
            {
                if (useWords)
                    data.Write<UInt16>((ushort)deltas[k]);
                else
                    data.WriteByte((byte)deltas[k]);
            }
            idx += runLen;
        }
    }

    /// <summary>
    /// Computes the encoded byte size of packed point numbers without writing.
    /// </summary>
    private static int PackedPointNumbersSize(int[] points)
    {
        if (points == null)
            return 1;   // single 0x00 byte

        int count = points.Length;
        int size  = count <= 0x7F ? 1 : 2;   // count field

        if (count == 0)
            return size;

        int prev = 0;
        bool useWords = false;
        for (int i = 0; i < count; i++)
        {
            if (points[i] - prev > 255) { useWords = true; break; }
            prev = points[i];
        }

        // runs of up to 128; each run: 1 control byte + runLen * (1 or 2)
        int runs = (count + 127) / 128;
        size += runs + count * (useWords ? 2 : 1);
        return size;
    }

    // ── Packed delta helpers ──────────────────────────────────────────────────

    /// <summary>Writes packed deltas for a single tuple variation.</summary>
    /// <param name="data">Target writer.</param>
    /// <param name="deltas">Delta values to encode (may be empty or null).</param>
    private static void WritePackedDeltas(Writer data, short[] deltas)
    {
        if (deltas == null || deltas.Length == 0)
            return;

        int i = 0;
        while (i < deltas.Length)
        {
            // Prefer a zero run when the current value is 0
            if (deltas[i] == 0)
            {
                int run = 0;
                while (i + run < deltas.Length && deltas[i + run] == 0 && run < 64)
                    run++;
                data.WriteByte((byte)(DeltasAreZero | (run - 1)));
                i += run;
                continue;
            }

            // Collect non-zero values (up to 64) and decide int8 vs int16
            bool needsWord = false;
            int  count     = 0;
            int  maxRun    = Math.Min(64, deltas.Length - i);

            for (int k = 0; k < maxRun && deltas[i + k] != 0; k++)
            {
                if (deltas[i + k] < -128 || deltas[i + k] > 127)
                    needsWord = true;
                count++;
            }

            if (needsWord)
            {
                data.WriteByte((byte)(DeltasAreWords | (count - 1)));
                for (int k = i; k < i + count; k++)
                    data.Write<Int16>(deltas[k]);
            }
            else
            {
                data.WriteByte((byte)(count - 1));
                for (int k = i; k < i + count; k++)
                    data.WriteByte((byte)(sbyte)deltas[k]);
            }

            i += count;
        }
    }

    /// <summary>
    /// Computes the encoded byte size of packed deltas without writing.
    /// </summary>
    private static int PackedDeltasSize(short[] deltas)
    {
        if (deltas == null || deltas.Length == 0)
            return 0;

        int size = 0;
        int i    = 0;

        while (i < deltas.Length)
        {
            if (deltas[i] == 0)
            {
                int run = 0;
                while (i + run < deltas.Length && deltas[i + run] == 0 && run < 64)
                    run++;
                size += 1;
                i    += run;
                continue;
            }

            bool needsWord = false;
            int  count     = 0;
            int  maxRun    = Math.Min(64, deltas.Length - i);

            for (int k = 0; k < maxRun && deltas[i + k] != 0; k++)
            {
                if (deltas[i + k] < -128 || deltas[i + k] > 127)
                    needsWord = true;
                count++;
            }

            size += 1 + count * (needsWord ? 2 : 1);
            i    += count;
        }

        return size;
    }
}
