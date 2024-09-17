using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.IO.Serialization;
using Utils.Objects;

namespace UtilsTest.Streams;

[TestClass]
public class NewReaderWriterTest
{

	[TestMethod]
	public void TestReadAndWriteNumbersAndDates()
	{
		Random r = new Random();
		byte b = r.RandomByte();
		short s = r.RandomShort();
		int i = r.RandomInt();
		long l = r.RandomLong();
		float f = r.RandomFloat();
		double d = r.RandomDouble();
		DateTime dt1 = DateTime.Now;

		var converters = new (IBasicWriter Writer, IBasicReader Reader)[]
		{
			(new RawWriter(), new RawReader()),
			(new CompressedIntWriter(), new CompressedIntReader()),
			(new UTF8IntWriter(), new UTF8IntReader()),
		};

		foreach (var converter in converters)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				NewWriter writer = new NewWriter(stream, converter.Writer.WriterDelegates);
				writer.WriteByte(b);
				writer.Write(s);
				writer.Write(i);
				writer.Write(l);
				writer.Write(f);
				writer.Write(d);
				writer.Write(dt1);

				stream.Seek(0, SeekOrigin.Begin);

				NewReader reader = new NewReader(stream, converter.Reader.ReaderDelegates);
				byte rb = reader.Read<byte>();
				short rs = reader.Read<short>();
				int ri = reader.Read<int>();
				long rl = reader.Read<long>();
				float rf = reader.Read<float>();
				double rd = reader.Read<double>();
				DateTime rdt1 = reader.Read<DateTime>();

				Assert.AreEqual(b, rb);
				Assert.AreEqual(s, rs);
				Assert.AreEqual(i, ri);
				Assert.AreEqual(l, rl);

				Assert.AreEqual(f, rf);
				Assert.AreEqual(d, rd);

				Assert.AreEqual(dt1, rdt1);

			}

		}
	}
}
