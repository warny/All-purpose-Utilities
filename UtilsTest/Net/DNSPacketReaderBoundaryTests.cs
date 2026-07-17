using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;

namespace UtilsTest.Net;

/// <summary>
/// Tests for edge-case and security-sensitive behaviour in <see cref="DNSPacketReader"/>:
/// compression-pointer cycles, RDLength boundary enforcement, and truncated datagrams.
/// </summary>
[TestClass]
public class DNSPacketReaderBoundaryTests
{
    private static readonly DNSPacketReader Reader = DNSPacketReader.Default;

    // Returns a 12-byte DNS response header with the specified record counts.
    private static byte[] Header(int qdCount, int anCount) =>
    [
        0x00, 0x01,
        0x81, 0x80,
        (byte)(qdCount >> 8), (byte)qdCount,
        (byte)(anCount >> 8), (byte)anCount,
        0x00, 0x00,
        0x00, 0x00,
    ];

    // "example.com" in DNS wire-format labels: 13 bytes total.
    private static readonly byte[] ExampleComLabels =
    [
        0x07, 0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65, // "example"
        0x03, 0x63, 0x6F, 0x6D,                           // "com"
        0x00                                               // root label
    ];

    // Resource record wire header: NAME pointer (→ offset 12) + TYPE + CLASS IN + TTL 3600 + RDLength.
    private static byte[] RrHeader(ushort type, ushort rdLength) =>
    [
        0xC0, 0x0C,                                    // NAME: compression pointer to offset 12
        (byte)(type >> 8), (byte)type,                 // TYPE
        0x00, 0x01,                                    // CLASS IN
        0x00, 0x00, 0x0E, 0x10,                        // TTL 3600
        (byte)(rdLength >> 8), (byte)rdLength,         // RDLength
    ];

    /// <summary>
    /// A compression pointer that points to itself must be detected and rejected.
    /// Without a depth guard the parser would recurse until a StackOverflowException.
    /// </summary>
    [TestMethod]
    public void Read_ThrowsOnSelfReferentialCompressionPointer()
    {
        // QNAME at offset 12 = 0xC0 0x0C → pointer to offset 12 (itself).
        byte[] datagram = [
            .. Header(1, 0),
            0xC0, 0x0C, // QNAME: pointer to offset 12 = itself
            0x00, 0x01, // QTYPE A
            0x00, 0x01, // QCLASS IN
        ];

        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(() => Reader.Read(datagram));
        StringAssert.Contains(ex.Message, "loop",
            "Exception must mention the compression-pointer loop.");
    }

    /// <summary>
    /// A two-node compression cycle (A → B → A) must also be detected before
    /// the recursion depth limit (128) is reached.
    /// </summary>
    [TestMethod]
    public void Read_ThrowsOnTwoNodeCompressionCycle()
    {
        // QNAME at offset 12 = pointer to 16.
        // Bytes 16-17 = pointer back to 12.
        // (QTYPE bytes at 14-15 are never reached because the exception fires first.)
        byte[] datagram = [
            .. Header(1, 0),
            0xC0, 0x10, // offset 12-13: QNAME pointer to 16
            0x00, 0x01, // offset 14-15: QTYPE (never reached)
            0xC0, 0x0C, // offset 16-17: pointer back to 12
        ];

        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(() => Reader.Read(datagram));
        StringAssert.Contains(ex.Message, "loop");
    }

    /// <summary>
    /// When a record's declared RDLength is smaller than what its type reader needs,
    /// <see cref="DNSPacketReader"/> must throw rather than read beyond the boundary.
    /// Here an MX record declares RDLength=1 but needs ≥2 bytes for the preference ushort.
    /// </summary>
    [TestMethod]
    public void Read_ThrowsWhenRDLengthTooShortForRecord()
    {
        byte[] datagram = [
            .. Header(1, 1),
            .. ExampleComLabels,   // question name "example.com" (13 bytes) at offset 12
            0x00, 0x0F,            // QTYPE MX
            0x00, 0x01,            // QCLASS IN
            // Answer: MX with RDLength=1, but the preference field alone needs 2 bytes
            .. RrHeader(0x000F, 1),
            0x0A,                  // first (and only) RDATA byte — second ReadByte() overflows
        ];

        // ReadByte() checks (Position >= RDataEnd) and throws InvalidDataException.
        Assert.ThrowsException<InvalidDataException>(() => Reader.Read(datagram));
    }

    /// <summary>
    /// When a record reader consumes fewer bytes than RDLength declares, the cursor
    /// must advance to the declared boundary. This test verifies that by placing two
    /// consecutive CNAME records where the first has 2 extra padding bytes in its RDATA.
    /// If the cursor is NOT advanced the second record would be parsed at the wrong offset
    /// and produce a garbled result.
    /// </summary>
    [TestMethod]
    public void Read_AdvancesCursorToRDLengthBoundaryWhenRecordUnderreads()
    {
        // "a.com" wire labels: 0x01 0x61 0x03 0x63 0x6F 0x6D 0x00 = 7 bytes
        // "b.com" wire labels: 0x01 0x62 0x03 0x63 0x6F 0x6D 0x00 = 7 bytes

        byte[] datagram = [
            .. Header(1, 2),
            // Question: "test.com" CNAME IN at offset 12
            0x04, 0x74, 0x65, 0x73, 0x74, // "test"
            0x03, 0x63, 0x6F, 0x6D, 0x00, // "com" + root
            0x00, 0x05, 0x00, 0x01,        // QTYPE CNAME, QCLASS IN
            // Answer 1: CNAME → "a.com"; RDLength=9 but only 7 bytes consumed by the reader.
            // The 2 trailing 0x00 bytes are padding inside the declared RDATA window.
            .. RrHeader(0x0005, 9),
            0x01, 0x61,                     // "a"
            0x03, 0x63, 0x6F, 0x6D, 0x00,  // "com" + root  (7 bytes consumed by reader)
            0x00, 0x00,                     // 2 unread padding bytes within the RDATA window
            // Answer 2: CNAME → "b.com"; RDLength=7 (exact). Correct parsing depends on
            // the cursor being at the right offset after answer 1.
            .. RrHeader(0x0005, 7),
            0x01, 0x62,                     // "b"
            0x03, 0x63, 0x6F, 0x6D, 0x00,  // "com" + root
        ];

        DNSHeader header = Reader.Read(datagram);

        Assert.AreEqual(2, header.Responses.Count,
            "Both records must be parsed; cursor advance past padding is required.");
        CNAME first = (CNAME)header.Responses[0].RData;
        Assert.AreEqual("a.com", first.CName.ToString());
        CNAME second = (CNAME)header.Responses[1].RData;
        Assert.AreEqual("b.com", second.CName.ToString(),
            "If the cursor did not advance past the padding, 'b.com' would be corrupted.");
    }

    /// <summary>
    /// A datagram shorter than 12 bytes (the DNS header) must be rejected immediately.
    /// </summary>
    [TestMethod]
    public void Read_ThrowsWhenDatagramShorterThanHeader()
    {
        Assert.ThrowsException<InvalidDataException>(() => Reader.Read(new byte[8]));
    }

    /// <summary>
    /// When a record's RDATA window extends beyond the end of the datagram, reads inside
    /// that window must throw rather than reading from uninitialised memory.
    /// </summary>
    [TestMethod]
    public void Read_ThrowsWhenDatagramTruncatedInsideRecord()
    {
        // MX answer declares RDLength=4, but the datagram is cut off after 2 RDATA bytes.
        byte[] datagram = [
            .. Header(1, 1),
            .. ExampleComLabels,
            0x00, 0x0F, 0x00, 0x01,  // QTYPE MX, QCLASS IN
            .. RrHeader(0x000F, 4),   // RDLength = 4
            0x00, 0x0A,               // only 2 of the 4 declared bytes present
        ];

        Assert.ThrowsException<InvalidDataException>(() => Reader.Read(datagram));
    }
}
