using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Utils.IO.Serialization;
using Utils.Randomization;

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

                var r = new Random();

                (byte b, short s, int i, long l, float f, double d, DateTime dt1)[] tests = [
			(0, 0, 0, 0, 0, 0, DateTime.Now),
			(byte.MinValue, short.MinValue, int.MinValue, long.MinValue, float.MinValue, double.MinValue, DateTime.MinValue),
			(byte.MaxValue, short.MaxValue, int.MaxValue, long.MaxValue, float.MaxValue, double.MaxValue, DateTime.MaxValue),
			(r.RandomByte(),r.RandomShort(),r.RandomInt(),r.RandomLong(),float.Epsilon,double.Epsilon,new DateTime(r.Next(1, 9999), r.Next(1,12), r.Next(1, 28), r.Next(1, 23), r.Next(1,59), r.Next(1,59))),
			(r.RandomByte(),r.RandomShort(),r.RandomInt(),r.RandomLong(),-float.Epsilon,-double.Epsilon,new DateTime(r.Next(1, 9999), r.Next(1,12), r.Next(1, 28), r.Next(1, 23), r.Next(1,59), r.Next(1,59))),
			(r.RandomByte(),r.RandomShort(),r.RandomInt(),r.RandomLong(),r.RandomFloat(),r.RandomDouble(),new DateTime(r.Next(1, 9999), r.Next(1,12), r.Next(1, 28), r.Next(1, 23), r.Next(1,59), r.Next(1,59))),
			(r.RandomByte(),r.RandomShort(),r.RandomInt(),r.RandomLong(),r.RandomFloat(),r.RandomDouble(),new DateTime(r.Next(1, 9999), r.Next(1,12), r.Next(1, 28), r.Next(1, 23), r.Next(1,59), r.Next(1,59))),
			(r.RandomByte(),r.RandomShort(),r.RandomInt(),r.RandomLong(),r.RandomFloat(),r.RandomDouble(),new DateTime(r.Next(1, 9999), r.Next(1,12), r.Next(1, 28), r.Next(1, 23), r.Next(1,59), r.Next(1,59))),
			(r.RandomByte(),r.RandomShort(),r.RandomInt(),r.RandomLong(),r.RandomFloat(),r.RandomDouble(),new DateTime(r.Next(1, 9999), r.Next(1,12), r.Next(1, 28), r.Next(1, 23), r.Next(1,59), r.Next(1,59))),
			(r.RandomByte(),r.RandomShort(),r.RandomInt(),r.RandomLong(),r.RandomFloat(),r.RandomDouble(),new DateTime(r.Next(1, 9999), r.Next(1,12), r.Next(1, 28), r.Next(1, 23), r.Next(1,59), r.Next(1,59))),
			(r.RandomByte(),r.RandomShort(),r.RandomInt(),r.RandomLong(),r.RandomFloat(),r.RandomDouble(),new DateTime(r.Next(1, 9999), r.Next(1,12), r.Next(1, 28), r.Next(1, 23), r.Next(1,59), r.Next(1,59)))
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

		Assert.ThrowsException<ArgumentNullException>(() => writer.Write((object)null));
	}
}
