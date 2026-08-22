using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Numerics;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>
/// Verifies the portable wire formats used by extended raw serialization converters.
/// </summary>
[TestClass]
public class RawSerializationWireFormatTests
{
    private static readonly Int128 Int128Vector =
        ((Int128)0x0011223344556677UL << 64) | 0x8899AABBCCDDEEFFUL;

    private static readonly UInt128 UInt128Vector =
        ((UInt128)0xFFEEDDCCBBAA9988UL << 64) | 0x7766554433221100UL;

    private static readonly byte[] GuidBytes =
    [
        0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77,
        0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF
    ];

    /// <summary>Verifies the exact signed 128-bit byte sequence in both numeric byte orders.</summary>
    [TestMethod]
    public void Int128WriterUsesSelectedEndianness()
    {
        CollectionAssert.AreEqual(
            new byte[] { 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 },
            Serialize(false, (raw, writer) => raw.WriteInt128(writer, Int128Vector)));
        CollectionAssert.AreEqual(
            new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF },
            Serialize(true, (raw, writer) => raw.WriteInt128(writer, Int128Vector)));
    }

    /// <summary>Verifies signed 128-bit boundaries and the fixed position of the sign bit.</summary>
    [TestMethod]
    public void Int128BoundariesRoundTripInBothEndiannesses()
    {
        Int128[] values = [Int128.Zero, Int128.One, -Int128.One, Int128.MinValue, Int128.MaxValue];
        AssertRoundTrips(values, (raw, writer, value) => raw.WriteInt128(writer, value), (raw, reader) => raw.ReadInt128(reader));

        byte[] littleMinimum = new byte[16];
        littleMinimum[15] = 0x80;
        byte[] bigMinimum = new byte[16];
        bigMinimum[0] = 0x80;
        CollectionAssert.AreEqual(littleMinimum, Serialize(false, (raw, writer) => raw.WriteInt128(writer, Int128.MinValue)));
        CollectionAssert.AreEqual(bigMinimum, Serialize(true, (raw, writer) => raw.WriteInt128(writer, Int128.MinValue)));
    }

    /// <summary>Verifies the exact unsigned 128-bit byte sequence in both numeric byte orders.</summary>
    [TestMethod]
    public void UInt128WriterUsesSelectedEndianness()
    {
        CollectionAssert.AreEqual(
            new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF },
            Serialize(false, (raw, writer) => raw.WriteUInt128(writer, UInt128Vector)));
        CollectionAssert.AreEqual(
            new byte[] { 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 },
            Serialize(true, (raw, writer) => raw.WriteUInt128(writer, UInt128Vector)));
    }

    /// <summary>Verifies unsigned 128-bit boundaries in both numeric byte orders.</summary>
    [TestMethod]
    public void UInt128BoundariesRoundTripInBothEndiannesses()
    {
        UInt128[] values = [UInt128.MinValue, UInt128.MaxValue];
        AssertRoundTrips(values, (raw, writer, value) => raw.WriteUInt128(writer, value), (raw, reader) => raw.ReadUInt128(reader));
    }

    /// <summary>Verifies exact BigInteger framing and sign-correct payload transitions.</summary>
    [TestMethod]
    public void BigIntegerWriterUsesEndianAwareFramingAndCanonicalPayload()
    {
        CollectionAssert.AreEqual(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x80, 0x00 }, SerializeBigInteger(false, 128));
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0x02, 0x00, 0x80 }, SerializeBigInteger(true, 128));
        CollectionAssert.AreEqual(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x7F, 0xFF }, SerializeBigInteger(false, -129));
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0x02, 0xFF, 0x7F }, SerializeBigInteger(true, -129));
    }

    /// <summary>Verifies that independently supplied BigInteger wire vectors are decoded correctly.</summary>
    [TestMethod]
    public void BigIntegerReaderUsesEndianAwareFramingAndPayload()
    {
        Assert.AreEqual(new BigInteger(128), Deserialize(false, new byte[] { 0x02, 0x00, 0x00, 0x00, 0x80, 0x00 }, (raw, reader) => raw.ReadBigInteger(reader)));
        Assert.AreEqual(new BigInteger(128), Deserialize(true, new byte[] { 0x00, 0x00, 0x00, 0x02, 0x00, 0x80 }, (raw, reader) => raw.ReadBigInteger(reader)));
        Assert.AreEqual(new BigInteger(-129), Deserialize(false, new byte[] { 0x02, 0x00, 0x00, 0x00, 0x7F, 0xFF }, (raw, reader) => raw.ReadBigInteger(reader)));
        Assert.AreEqual(new BigInteger(-129), Deserialize(true, new byte[] { 0x00, 0x00, 0x00, 0x02, 0xFF, 0x7F }, (raw, reader) => raw.ReadBigInteger(reader)));
    }

    /// <summary>Verifies BigInteger byte-count and sign transitions, including values beyond 128 bits.</summary>
    [TestMethod]
    public void BigIntegerBoundaryMatrixRoundTripsInBothEndiannesses()
    {
        BigInteger huge = (BigInteger.One << 200) + 0x12345;
        BigInteger[] values = [-256, -255, -129, -128, -127, -1, 0, 1, 127, 128, 129, 255, 256, huge, -huge];
        AssertRoundTrips(values, (raw, writer, value) => raw.WriteBigInteger(writer, value), (raw, reader) => raw.ReadBigInteger(reader));
    }

    /// <summary>Verifies that GUID writing always uses the canonical RFC/network byte layout.</summary>
    [TestMethod]
    public void GuidWriterUsesCanonicalLayoutIndependentlyOfNumericEndianness()
    {
        Guid value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        CollectionAssert.AreEqual(GuidBytes, Serialize(false, (raw, writer) => raw.WriteGuid(writer, value)));
        CollectionAssert.AreEqual(GuidBytes, Serialize(true, (raw, writer) => raw.WriteGuid(writer, value)));
    }

    /// <summary>Verifies that an external canonical GUID vector is read in either numeric byte order.</summary>
    [TestMethod]
    public void GuidReaderUsesCanonicalLayoutIndependentlyOfNumericEndianness()
    {
        Guid expected = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        Assert.AreEqual(expected, Deserialize(false, GuidBytes, (raw, reader) => raw.ReadGuid(reader)));
        Assert.AreEqual(expected, Deserialize(true, GuidBytes, (raw, reader) => raw.ReadGuid(reader)));
    }

    /// <summary>Verifies that generic reader and writer converter resolution still supports all IO-03 types.</summary>
    [TestMethod]
    public void GenericReaderWriterResolvesExtendedWireTypes()
    {
        object[] values = [Int128Vector, UInt128Vector, new BigInteger(128), Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")];
        foreach (object value in values)
        {
            using var stream = new MemoryStream();
            var writer = new Writer(stream);
            writer.Write(value);
            stream.Position = 0;
            var reader = new Reader(stream);
            Assert.AreEqual(value, reader.Read(value.GetType()));
        }
    }

    /// <summary>Serializes one raw value to an in-memory binary buffer.</summary>
    private static byte[] Serialize(bool bigEndian, Action<RawWriter, IWriter> write)
    {
        using var stream = new MemoryStream();
        write(new RawWriter { BigEndian = bigEndian }, new Writer(stream));
        return stream.ToArray();
    }

    /// <summary>Serializes one BigInteger to an in-memory binary buffer.</summary>
    private static byte[] SerializeBigInteger(bool bigEndian, BigInteger value) =>
        Serialize(bigEndian, (raw, writer) => raw.WriteBigInteger(writer, value));

    /// <summary>Deserializes one raw value from an independently supplied binary buffer.</summary>
    private static T Deserialize<T>(bool bigEndian, byte[] bytes, Func<RawReader, IReader, T> read)
    {
        using var stream = new MemoryStream(bytes);
        return read(new RawReader { BigEndian = bigEndian }, new Reader(stream));
    }

    /// <summary>Checks raw writer-to-reader round trips for every value in both numeric byte orders.</summary>
    private static void AssertRoundTrips<T>(T[] values, Action<RawWriter, IWriter, T> write, Func<RawReader, IReader, T> read)
    {
        foreach (bool bigEndian in new[] { false, true })
        {
            foreach (T value in values)
            {
                byte[] bytes = Serialize(bigEndian, (raw, writer) => write(raw, writer, value));
                Assert.AreEqual(value, Deserialize(bigEndian, bytes, read));
            }
        }
    }
}
