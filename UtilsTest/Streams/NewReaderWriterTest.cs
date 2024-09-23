using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Utils.IO.Serialization;
using Utils.Objects;
using static System.Net.Mime.MediaTypeNames;

namespace UtilsTest.Streams;

[TestClass]
public class NewReaderWriterTest
{

	[TestMethod]
	public void TestReadAndWriteNumbersAndDates()
	{
		void AssertAreEquals<T>(T t1, T t2, IBasicWriter Writer, IBasicReader Reader)
		{
			Assert.AreEqual(t1, t2, $"{typeof(T).Name}, {Writer.GetType().Name}, {Reader.GetType().Name}");

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

		var converters = new (IBasicWriter Writer, IBasicReader Reader)[]
		{
			(new RawWriter(), new RawReader()),
			(new CompressedIntWriter(), new CompressedIntReader()),
			(new UTF8IntWriter(), new UTF8IntReader()),
		};

		foreach (var test in tests)
		{
			foreach (var converter in converters)
			{
				using (MemoryStream stream = new MemoryStream())
				{
					NewWriter writer = new NewWriter(stream, converter.Writer.WriterDelegates);
					writer.WriteByte(test.b);
					writer.Write(test.s);
					writer.Write(test.i);
					writer.Write(test.l);
					writer.Write(test.f);
					writer.Write(test.d);
					writer.Write(test.dt1);

					stream.Seek(0, SeekOrigin.Begin);

					NewReader reader = new NewReader(stream, converter.Reader.ReaderDelegates);
					byte rb = reader.Read<byte>();
					short rs = reader.Read<short>();
					int ri = reader.Read<int>();
					long rl = reader.Read<long>();
					float rf = reader.Read<float>();
					double rd = reader.Read<double>();
					DateTime rdt1 = reader.Read<DateTime>();

					AssertAreEquals(test.b, rb, converter.Writer, converter.Reader);
					AssertAreEquals(test.s, rs, converter.Writer, converter.Reader);
					AssertAreEquals(test.i, ri, converter.Writer, converter.Reader);
					AssertAreEquals(test.l, rl, converter.Writer, converter.Reader);

					AssertAreEquals(test.f, rf, converter.Writer, converter.Reader);
					AssertAreEquals(test.d, rd, converter.Writer, converter.Reader);

					AssertAreEquals(test.dt1, rdt1, converter.Writer, converter.Reader);

				}

			}
		}
	}
}
