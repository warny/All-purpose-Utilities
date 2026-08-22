using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>Verifies independent literal wire vectors and DateTime kind semantics for every built-in codec.</summary>
[TestClass]
public sealed class DateTimeWireCodecTests
{
    private static readonly DateTime ReferenceUtc = new(1970, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Verifies literal little- and big-endian .NET Binary vectors independently for reading and writing.</summary>
    [TestMethod]
    public void DotNetBinary_GoldenVectors()
    {
        Verify(new DotNetBinaryDateTimeCodec(), ReferenceUtc,
            [0x00, 0x40, 0x1F, 0x22, 0xBF, 0x80, 0x9F, 0x48],
            [0x48, 0x9F, 0x80, 0xBF, 0x22, 0x1F, 0x40, 0x00],
            value => Assert.AreEqual(ReferenceUtc.ToBinary(), value.ToBinary()));
    }

    /// <summary>Verifies literal tick vectors and the codec's Unspecified read contract.</summary>
    [TestMethod]
    public void Ticks_GoldenVectors()
    {
        DateTime expected = DateTime.SpecifyKind(ReferenceUtc, DateTimeKind.Unspecified);
        Verify(new TicksDateTimeCodec(), ReferenceUtc,
            [0x00, 0x40, 0x1F, 0x22, 0xBF, 0x80, 0x9F, 0x08],
            [0x08, 0x9F, 0x80, 0xBF, 0x22, 0x1F, 0x40, 0x00],
            value => { Assert.AreEqual(expected, value); Assert.AreEqual(DateTimeKind.Unspecified, value.Kind); });
    }

    /// <summary>Verifies literal signed Unix-second vectors and Utc semantics.</summary>
    [TestMethod]
    public void UnixSeconds_GoldenVectors()
    {
        Verify(new UnixSecondsDateTimeCodec(), ReferenceUtc,
            [0x80, 0x51, 0x01, 0, 0, 0, 0, 0],
            [0, 0, 0, 0, 0, 0x01, 0x51, 0x80],
            value => { Assert.AreEqual(ReferenceUtc, value); Assert.AreEqual(DateTimeKind.Utc, value.Kind); });
        Assert.AreEqual(DateTime.UnixEpoch.AddSeconds(-1), Read(new UnixSecondsDateTimeCodec(), [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], false));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Read(new UnixSecondsDateTimeCodec(), [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F], false));
    }

    /// <summary>Verifies literal signed Unix-millisecond vectors and Utc semantics.</summary>
    [TestMethod]
    public void UnixMilliseconds_GoldenVectors()
    {
        Verify(new UnixMillisecondsDateTimeCodec(), ReferenceUtc,
            [0x00, 0x5C, 0x26, 0x05, 0, 0, 0, 0],
            [0, 0, 0, 0, 0x05, 0x26, 0x5C, 0x00],
            value => { Assert.AreEqual(ReferenceUtc, value); Assert.AreEqual(DateTimeKind.Utc, value.Kind); });
        Assert.AreEqual(DateTime.UnixEpoch.AddMilliseconds(-1), Read(new UnixMillisecondsDateTimeCodec(), [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], false));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Read(new UnixMillisecondsDateTimeCodec(), [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F], false));
    }

    /// <summary>Verifies the external OLE Automation value 25570.0 and framework Unspecified semantics.</summary>
    [TestMethod]
    public void OleAutomation_GoldenVectors()
    {
        DateTime expected = DateTime.SpecifyKind(ReferenceUtc, DateTimeKind.Unspecified);
        Verify(new OleAutomationDateTimeCodec(), expected,
            [0x00, 0x00, 0x00, 0x00, 0x80, 0xF8, 0xD8, 0x40],
            [0x40, 0xD8, 0xF8, 0x80, 0x00, 0x00, 0x00, 0x00],
            value => { Assert.AreEqual(expected, value); Assert.AreEqual(DateTimeKind.Unspecified, value.Kind); });
    }

    /// <summary>Verifies the Windows FILETIME value for 1970-01-02 and Utc semantics.</summary>
    [TestMethod]
    public void FileTime_GoldenVectors()
    {
        Verify(new FileTimeDateTimeCodec(), ReferenceUtc,
            [0x00, 0x40, 0xA8, 0xFF, 0xA7, 0xB2, 0x9D, 0x01],
            [0x01, 0x9D, 0xB2, 0xA7, 0xFF, 0xA8, 0x40, 0x00],
            value => { Assert.AreEqual(ReferenceUtc, value); Assert.AreEqual(DateTimeKind.Utc, value.Kind); });
    }

    /// <summary>Verifies framework binary round-trips for Local, Utc, and Unspecified values through the public default.</summary>
    [TestMethod]
    public void DotNetBinary_PreservesFrameworkKindSemantics()
    {
        DateTime[] values =
        [
            new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Local),
            new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Unspecified)
        ];
        foreach (DateTime value in values)
        {
            using MemoryStream stream = new();
            new Writer(stream).Write(value);
            stream.Position = 0;
            DateTime copy = new Reader(stream).Read<DateTime>();
            Assert.AreEqual(value.ToBinary(), copy.ToBinary());
            Assert.AreEqual(value.Kind, copy.Kind);
        }
    }

    /// <summary>Verifies one codec's writer and reader independently against literal vectors in both byte orders.</summary>
    private static void Verify<TCodec>(TCodec codec, DateTime writeValue, byte[] littleEndian, byte[] bigEndian, Action<DateTime> assertRead)
        where TCodec : IWireCodec<DateTime>
    {
        CollectionAssert.AreEqual(littleEndian, Write(codec, writeValue, false));
        CollectionAssert.AreEqual(bigEndian, Write(codec, writeValue, true));
        assertRead(Read(codec, littleEndian, false));
        assertRead(Read(codec, bigEndian, true));
    }

    /// <summary>Writes a DateTime codec using independently configured primitive delegates.</summary>
    private static byte[] Write(IWireWriter<DateTime> codec, DateTime value, bool bigEndian)
    {
        using MemoryStream stream = new();
        RawWriter primitives = new() { BigEndian = bigEndian };
        Writer writer = new(stream, primitives.WriterDelegates);
        codec.Write(writer, value);
        return stream.ToArray();
    }

    /// <summary>Reads literal bytes through a DateTime codec using independently configured primitive delegates.</summary>
    private static DateTime Read(IWireReader<DateTime> codec, byte[] bytes, bool bigEndian)
    {
        RawReader primitives = new() { BigEndian = bigEndian };
        Reader reader = new(new MemoryStream(bytes), primitives.ReaderDelegates);
        return codec.Read(reader);
    }
}
