using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.IO.Serialization;

namespace UtilsTest.Streams;

[TestClass]
public class NewReaderWriterTest
{

    [TestMethod]
    public void TestReadAndWriteNumbersAndDates()
    {
        void AssertAreEquals<T>(T expected, T actual)
        {
            Assert.AreEqual(expected, actual, typeof(T).Name);
        }

        (byte b, short s, int i, long l, float f, double d, DateTime dt1)[] tests =
        [
            (0, 0, 0, 0, 0, 0, new DateTime(2000, 2, 29, 12, 34, 56, DateTimeKind.Unspecified)),
            (byte.MinValue, short.MinValue, int.MinValue, long.MinValue, float.MinValue, double.MinValue, DateTime.MinValue),
            (byte.MaxValue, short.MaxValue, int.MaxValue, long.MaxValue, float.MaxValue, double.MaxValue, DateTime.MaxValue),
            (1, -1, -42, -1234567890123, float.Epsilon, double.Epsilon, new DateTime(2024, 2, 29, 23, 59, 58, DateTimeKind.Utc)),
            (254, 1234, 42, 1234567890123, -float.Epsilon, -double.Epsilon, new DateTime(2023, 7, 15, 8, 30, 1, DateTimeKind.Local)),
            (127, -1234, -987654321, -9876543210123, 123.5f, -9876.125, new DateTime(1985, 10, 26, 1, 21, 0, DateTimeKind.Unspecified)),
            (128, 2345, 987654321, 9876543210123, -456.25f, 0.03125, new DateTime(2038, 1, 19, 3, 14, 7, DateTimeKind.Utc))
        ];

        var converters = (Writer: new RawWriter(), Reader: new RawReader());

        foreach (var test in tests)
        {
            using MemoryStream stream = new MemoryStream();

            Writer writer = new Writer(stream, converters.Writer.WriterDelegates);
            writer.WriteByte(test.b);
            writer.Write(test.s);
            writer.Write(test.i);
            writer.Write(test.l);
            writer.Write(test.f);
            writer.Write(test.d);
            writer.Write(test.dt1);

            stream.Seek(0, SeekOrigin.Begin);

            Reader reader = new Reader(stream, converters.Reader.ReaderDelegates);
            byte rb = reader.Read<byte>();
            short rs = reader.Read<short>();
            int ri = reader.Read<int>();
            long rl = reader.Read<long>();
            float rf = reader.Read<float>();
            double rd = reader.Read<double>();
            DateTime rdt1 = reader.Read<DateTime>();

            AssertAreEquals(test.b, rb);
            AssertAreEquals(test.s, rs);
            AssertAreEquals(test.i, ri);
            AssertAreEquals(test.l, rl);

            AssertAreEquals(test.f, rf);
            AssertAreEquals(test.d, rd);

            AssertAreEquals(test.dt1, rdt1);

        }
    }

    [TestMethod]
    public void ReadByTypeReturnsValue()
    {
        using MemoryStream stream = new MemoryStream();
        Writer writer = new Writer(stream, new RawWriter().WriterDelegates);
        writer.Write(123);

        stream.Position = 0;
        Reader reader = new Reader(stream, new RawReader().ReaderDelegates);
        object value = reader.Read(typeof(int));

        Assert.AreEqual(123, value);
    }

    [TestMethod]
    public void SlicePreservesWriters()
    {
        using MemoryStream stream = new MemoryStream();
        Writer writer = new Writer(stream, new RawWriter().WriterDelegates);
        writer.Write(0);

        Writer slice = writer.Slice(0, stream.Length);
        slice.Write(42);

        writer.Position = 0;
        Reader reader = new Reader(stream, new RawReader().ReaderDelegates);
        int value = reader.Read<int>();

        Assert.AreEqual(42, value);
    }

    [TestMethod]
    public void WriteNullObjectThrows()
    {
        using MemoryStream stream = new MemoryStream();
        Writer writer = new Writer(stream, new RawWriter().WriterDelegates);

        Assert.ThrowsExactly<ArgumentNullException>(() => writer.Write((object)null));
    }
}
