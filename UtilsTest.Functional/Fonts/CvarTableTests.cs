using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts.TTF.Tables;
using Utils.IO.Serialization;

namespace UtilsTest.Fonts;

[TestClass]
public class CvarTableTests
{
    // Big-endian reader/writer delegates matching TrueType encoding.
    private static readonly RawReader BigEndianReader = new RawReader() { BigEndian = true };
    private static readonly RawWriter BigEndianWriter = new RawWriter() { BigEndian = true };

    private static Reader MakeReader(byte[] data)
        => new Reader(new MemoryStream(data), BigEndianReader.ReaderDelegates);

    /// <summary>
    /// Writes <paramref name="source"/> to a byte array (big-endian), then reads it back
    /// into a fresh <see cref="CvarTable"/> with the same <see cref="CvarTable.AxisCount"/>.
    /// </summary>
    private static (CvarTable table, byte[] bytes) RoundTrip(CvarTable source)
    {
        using var ms     = new MemoryStream();
        var       writer = new Writer(ms, BigEndianWriter.WriterDelegates);
        source.WriteData(writer);
        byte[] bytes = ms.ToArray();

        var rt = new CvarTable { AxisCount = source.AxisCount };
        rt.ReadData(MakeReader(bytes));
        return (rt, bytes);
    }

    // ── Test 1 — Empty table (no tuple variations) ──────────────────────────

    [TestMethod]
    public void EmptyTable_RoundTrip()
    {
        // Binary: header only (8 bytes):
        //   majorVersion=1, minorVersion=0, tupleVariationCount=0, dataOffset=8
        var source = new CvarTable { AxisCount = 1, TupleVariations = [] };

        Assert.AreEqual(8, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(8, bytes.Length);
        Assert.AreEqual((ushort)1, table.MajorVersion);
        Assert.AreEqual((ushort)0, table.MinorVersion);
        Assert.AreEqual(0, table.TupleVariations.Length);
    }

    // ── Test 2 — Single tuple, all CVTs, 1 axis ─────────────────────────────

    [TestMethod]
    public void SingleTuple_AllCvts_1Axis_RoundTrip()
    {
        // One tuple variation:
        //   peak = 1.0 (F2Dot14 = 0x4000), no intermediate region
        //   PointNumbers = null  →  "all CVTs"
        //   Deltas = [10, 20, 30]
        //
        // Expected binary layout (19 bytes, big-endian):
        //   Header: 00 01 00 00 00 01 00 0E          (8 bytes, dataOffset = 14)
        //   Tuple header:
        //     variationDataSize = 00 05  (1 for all-points + 4 for deltas)
        //     tupleIndex        = A0 00  (0x8000 EMBEDDED_PEAK | 0x2000 PRIVATE_POINTS)
        //     peakTuple[0]      = 40 00  (1.0 in F2Dot14)            (6 bytes total)
        //   Serialized data (at offset 14):
        //     packed points:  00                     (count=0 → all CVTs)
        //     packed deltas:  02 0A 14 1E            (3 int8: 10, 20, 30)  (5 bytes total)

        var source = new CvarTable
        {
            AxisCount = 1,
            TupleVariations =
            [
                new CvarTable.TupleVariation(
                    peakCoords:          [0x4000],   // 1.0
                    intermedStartCoords: null,
                    intermedEndCoords:   null,
                    pointNumbers:        null,        // all CVTs
                    deltas:              [10, 20, 30])
            ]
        };

        // Header(8) + tupleHdr(4+2=6) + serialized(1+4=5) = 19
        Assert.AreEqual(19, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(19, bytes.Length);
        Assert.AreEqual(1, table.TupleVariations.Length);

        var tv = table.TupleVariations[0];
        Assert.AreEqual(1, tv.PeakCoords.Length);
        Assert.AreEqual(unchecked((short)0x4000), tv.PeakCoords[0]);  // 1.0 in F2Dot14
        Assert.IsNull(tv.IntermedStartCoords, "No intermediate region expected.");
        Assert.IsNull(tv.PointNumbers, "null means all CVTs.");
        Assert.AreEqual(3, tv.Deltas.Length);
        Assert.AreEqual(10,  tv.Deltas[0]);
        Assert.AreEqual(20,  tv.Deltas[1]);
        Assert.AreEqual(30,  tv.Deltas[2]);
    }

    // ── Test 3 — Single tuple, private point numbers, mixed deltas ──────────

    [TestMethod]
    public void SingleTuple_PrivatePoints_MixedDeltas_RoundTrip()
    {
        // One tuple variation with explicit CVT indices and mixed delta magnitudes:
        //   peak = –1.0  (F2Dot14 = 0x8000 as short = –32768)
        //   PointNumbers = [0, 2, 5]    (specific CVT entries)
        //   Deltas = [100, -50, 200]    (200 needs int16 because 200 > 127)
        //
        // Packed point numbers for [0, 2, 5]:
        //   count=3 → 0x03 (1 byte)
        //   run: cb=0x02 (3 uint8 elements), deltas=[0, 2, 3]  → 4 bytes
        //   total: 5 bytes
        //
        // Packed deltas for [100, -50, 200]:
        //   200 > 127 → need int16 for the run
        //   cb = 0x42 (DELTAS_ARE_WORDS | count=3), then 0x0064, 0xFFCE, 0x00C8  → 7 bytes
        //   total: 1+6 = 7 bytes
        //
        // variationDataSize = 5 + 7 = 12 bytes
        // Total = 8 (hdr) + 6 (tupleHdr) + 12 (data) = 26 bytes

        short peakF2Dot14 = unchecked((short)0x8000);  // –2.0 in F2Dot14

        var source = new CvarTable
        {
            AxisCount = 1,
            TupleVariations =
            [
                new CvarTable.TupleVariation(
                    peakCoords:          [peakF2Dot14],
                    intermedStartCoords: null,
                    intermedEndCoords:   null,
                    pointNumbers:        [0, 2, 5],
                    deltas:              [100, -50, 200])
            ]
        };

        Assert.AreEqual(26, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(26, bytes.Length);
        var tv = table.TupleVariations[0];
        Assert.AreEqual(peakF2Dot14, tv.PeakCoords[0]);
        CollectionAssert.AreEqual(new[] { 0, 2, 5 },       tv.PointNumbers);
        CollectionAssert.AreEqual(new short[] { 100, -50, 200 }, tv.Deltas);
    }

    // ── Test 4 — Intermediate region, 2 axes ────────────────────────────────

    [TestMethod]
    public void IntermediateRegion_2Axes_RoundTrip()
    {
        // One tuple variation with intermediate region boundaries across 2 design axes:
        //   peak  = (0.5, 1.0)   F2Dot14: (0x2000, 0x4000)
        //   start = (0.0, 0.5)   F2Dot14: (0x0000, 0x2000)
        //   end   = (1.0, 1.0)   F2Dot14: (0x4000, 0x4000)
        //   PointNumbers = null  →  all CVTs
        //   Deltas = [5, -10]    (int8)
        //
        // Tuple header (16 bytes):
        //   variationDataSize(2) + tupleIndex(2)
        //   + peak(2×2) + start(2×2) + end(2×2)  = 4+4+8 = 16
        // Serialized data:
        //   points: 0x00 (1 byte)
        //   deltas: cb=0x01 (count=2, int8), 5, -10 → 3 bytes
        //   variationDataSize = 4
        // Total = 8 + 16 + 4 = 28

        var source = new CvarTable
        {
            AxisCount = 2,
            TupleVariations =
            [
                new CvarTable.TupleVariation(
                    peakCoords:          [0x2000, 0x4000],
                    intermedStartCoords: [0x0000, 0x2000],
                    intermedEndCoords:   [0x4000, 0x4000],
                    pointNumbers:        null,
                    deltas:              [5, -10])
            ]
        };

        Assert.AreEqual(28, source.Length);

        var (table, bytes) = RoundTrip(source);

        Assert.AreEqual(28, bytes.Length);
        var tv = table.TupleVariations[0];

        Assert.AreEqual(2, tv.PeakCoords.Length);
        Assert.AreEqual(unchecked((short)0x2000), tv.PeakCoords[0]);
        Assert.AreEqual(unchecked((short)0x4000), tv.PeakCoords[1]);

        Assert.IsNotNull(tv.IntermedStartCoords);
        Assert.AreEqual(unchecked((short)0x0000), tv.IntermedStartCoords[0]);
        Assert.AreEqual(unchecked((short)0x2000), tv.IntermedStartCoords[1]);

        Assert.IsNotNull(tv.IntermedEndCoords);
        Assert.AreEqual(unchecked((short)0x4000), tv.IntermedEndCoords[0]);
        Assert.AreEqual(unchecked((short)0x4000), tv.IntermedEndCoords[1]);

        Assert.IsNull(tv.PointNumbers);
        CollectionAssert.AreEqual(new short[] { 5, -10 }, tv.Deltas);
    }

    // ── Test 5 — ReadData from raw binary (Format 0 shared point numbers) ───

    [TestMethod]
    public void ReadData_SharedPointNumbers_ParsesCorrectly()
    {
        // Table with SHARED_POINT_NUMBERS + 2 tuples that share CVT indices [1, 3].
        // 1 axis.
        //
        // Shared point data (count=2, run uint8: [1, 3]):
        //   02           count = 2
        //   01 01 02     run (2 uint8 values): delta[0]=1→pt=1, delta[1]=2→pt=3
        //   = 4 bytes
        //
        // Tuple 0: peak=0x4000, no private points, deltas=[7, -7]
        //   data: deltas only: cb=0x01 (2 int8), 07, F9  = 3 bytes
        //
        // Tuple 1: peak=0x8000, no private points, deltas=[0, 0]
        //   data: deltas only: cb=0x81 (2 zeros) = 1 byte
        //
        // Header:  00 01 00 00  (version)
        //          80 02        (tupleVariationCount: SHARED_POINT_NUMBERS | 2)
        //          00 12        (dataOffset = 8 + 2*(4+2) = 8+12 = 20 = 0x14)
        //                       Wait: 2 headers × (4 base + 1 axis × 2) = 2 × 6 = 12 → dataOffset = 20
        //
        // Tuple hdr 0: 00 03  A0 00  40 00   (dataSize=3, tupleIndex=EMBEDDED_PEAK, peak=0x4000)
        // Tuple hdr 1: 00 01  A0 00  80 00   (dataSize=1, tupleIndex=EMBEDDED_PEAK, peak=0x8000)
        //
        // Serialized data at offset 20:
        //   Shared points: 02 01 01 02      (4 bytes)
        //   Tuple 0 data:  01 07 F9         (3 bytes)
        //   Tuple 1 data:  81               (1 byte)
        //
        // Total: 20 + 4 + 3 + 1 = 28 bytes

        byte[] data =
        [
            // Header
            0x00, 0x01,              // majorVersion = 1
            0x00, 0x00,              // minorVersion = 0
            0x80, 0x02,              // tupleVariationCount: SHARED_POINT_NUMBERS | count=2
            0x00, 0x14,              // dataOffset = 20

            // Tuple variation header 0 (6 bytes)
            0x00, 0x03,              // variationDataSize = 3
            0xA0, 0x00,              // tupleIndex: EMBEDDED_PEAK(0x8000) | PRIVATE_POINTS(0x2000)...
                                     // Wait — no PRIVATE_POINTS here since we use shared!
                                     // tupleIndex = EMBEDDED_PEAK only: 0x8000
            // Let me recalculate — these tuples do NOT have PRIVATE_POINT_NUMBERS:
            // tupleIndex = 0x8000 (EMBEDDED_PEAK_TUPLE only)
            0x40, 0x00,              // peakTuple[0] = 0x4000 (1.0)

            // Tuple variation header 1 (6 bytes)
            0x00, 0x01,              // variationDataSize = 1
            0x80, 0x00,              // tupleIndex = 0x8000 (EMBEDDED_PEAK_TUPLE only)
            0x80, 0x00,              // peakTuple[0] = 0x8000 (-2.0 as F2Dot14)

            // Serialized data at offset 20:
            // Shared packed point numbers [1, 3]:
            0x02,                    // count = 2
            0x01,                    // run: 2 uint8 elements (runLen-1=1 → 0x01)
            0x01, 0x02,              // deltas: 1 (pt=1), 2 (pt=1+2=3)
            // Tuple 0 deltas [7, -7]:
            0x01,                    // 2 int8 values
            0x07, 0xF9,              // 7, -7
            // Tuple 1 deltas [0, 0]:
            0x81,                    // DELTAS_ARE_ZERO | (2-1=1) → 2 zeros
        ];

        // Fix the header: tupleIndex values above were incorrectly set to 0xA000.
        // Shared tuples should have tupleIndex = 0x8000 (only EMBEDDED_PEAK_TUPLE).
        // The byte array already has 0xA0 0x00 for header 0 — let me fix this.
        // tupleIndex = 0x8000 → bytes: 0x80, 0x00.
        // Let me rebuild the byte array correctly.

        data = [
            // Header
            0x00, 0x01,   // majorVersion = 1
            0x00, 0x00,   // minorVersion = 0
            0x80, 0x02,   // tupleVariationCount: 0x8002 = SHARED_POINT_NUMBERS | count=2
            0x00, 0x14,   // dataOffset = 20

            // Tuple hdr 0: variationDataSize=3, tupleIndex=0x8000 (EMBEDDED_PEAK), peak=0x4000
            0x00, 0x03,   // variationDataSize = 3
            0x80, 0x00,   // tupleIndex = EMBEDDED_PEAK_TUPLE, no PRIVATE_POINTS
            0x40, 0x00,   // peakTuple[0] = 0x4000 (1.0 in F2Dot14)

            // Tuple hdr 1: variationDataSize=1, tupleIndex=0x8000, peak=0x8000
            0x00, 0x01,   // variationDataSize = 1
            0x80, 0x00,   // tupleIndex = EMBEDDED_PEAK_TUPLE, no PRIVATE_POINTS
            0x80, 0x00,   // peakTuple[0] = 0x8000 (-2.0 in F2Dot14)

            // Serialized data at offset 20:
            // Shared point numbers [1, 3]: count=2, run(2 uint8): 1, 2
            0x02,         // count = 2
            0x01,         // run control: POINTS_ARE_WORDS=0, runLen-1=1 → 2 uint8 elements
            0x01, 0x02,   // pt[0]=0+1=1, pt[1]=1+2=3

            // Tuple 0 deltas (3 bytes): [7, -7] as int8
            0x01,         // 2 int8 deltas (count-1=1)
            0x07,         // 7
            0xF9,         // -7 (0xF9 = 249, reinterpret as signed int8 = -7)

            // Tuple 1 deltas (1 byte): [0, 0] as zero-run
            0x81,         // DELTAS_ARE_ZERO | (2-1=1) → 2 zeros
        ];

        var table = new CvarTable { AxisCount = 1 };
        table.ReadData(MakeReader(data));

        Assert.AreEqual(2, table.TupleVariations.Length);

        var tv0 = table.TupleVariations[0];
        Assert.AreEqual(unchecked((short)0x4000), tv0.PeakCoords[0]);
        // Shared points [1, 3]
        CollectionAssert.AreEqual(new[] { 1, 3 }, tv0.PointNumbers);
        Assert.AreEqual(2, tv0.Deltas.Length);
        Assert.AreEqual(7,  tv0.Deltas[0]);
        Assert.AreEqual(-7, tv0.Deltas[1]);

        var tv1 = table.TupleVariations[1];
        Assert.AreEqual(unchecked((short)0x8000), tv1.PeakCoords[0]);
        CollectionAssert.AreEqual(new[] { 1, 3 }, tv1.PointNumbers);
        Assert.AreEqual(2, tv1.Deltas.Length);
        Assert.AreEqual(0, tv1.Deltas[0]);
        Assert.AreEqual(0, tv1.Deltas[1]);
    }

    // ── Test 6 — ReadData throws when AxisCount is not set ──────────────────

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void ReadData_AxisCountNotSet_ThrowsInvalidOperation()
    {
        var table = new CvarTable(); // AxisCount = 0 by default
        table.ReadData(MakeReader([0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x08]));
    }
}
