using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.Arrays;
using Utils.Objects;
using Utils.IO.Serialization;

namespace UtilsTest.Streams
{
	[TestClass]
	public class ReaderWriterTests
	{
		[TestMethod]
		public void TestReadAndWriteString1()
		{
			Random r = new Random();
			var testString1 = r.RandomString(10, 20);
			var testString2 = r.RandomString(10, 20);
			using (MemoryStream stream = new MemoryStream())
			{
				Writer writer = new Writer(stream);
				writer.WriteVariableLengthString(testString1, Encoding.UTF8);
				writer.WriteVariableLengthString(testString2, Encoding.UTF8);

				stream.Seek(0, SeekOrigin.Begin);

				Reader reader = new Reader(stream);
				string resultString1 = reader.ReadVariableLengthString(Encoding.UTF8);
				string resultString2 = reader.ReadVariableLengthString(Encoding.UTF8);

				Assert.AreEqual(testString1, resultString1);
				Assert.AreEqual(testString2, resultString2);
			}
		}

		[TestMethod]
		public void TestReadAndWriteString2()
		{
			Random r = new Random();
			var testString1 = r.RandomString(10, 20);
			var testString2 = r.RandomString(10, 20);
			using (MemoryStream stream = new MemoryStream())
			{
				Writer writer = new Writer(stream);
				writer.WriteNullTerminatedString(testString1, Encoding.UTF8, [0]);
				writer.WriteNullTerminatedString(testString2, Encoding.UTF8, [0]);

				stream.Seek(0, SeekOrigin.Begin);

				Reader reader = new Reader(stream);
				string resultString1 = reader.ReadNullTerminatedString(Encoding.UTF8);
				string resultString2 = reader.ReadNullTerminatedString(Encoding.UTF8);

				Assert.AreEqual(testString1, resultString1);
				Assert.AreEqual(testString2, resultString2);
			}
		}

		[TestMethod]
		public void TestReadAndWriteString3()
		{
			Random r = new Random();
			var testString1 = r.RandomString(10, 20);
			var testString2 = r.RandomString(10, 20);
			using (MemoryStream stream = new MemoryStream())
			{
				Writer writer = new Writer(stream);
				writer.WriteFixedLengthString(testString1, 25, Encoding.UTF8);
				writer.WriteFixedLengthString(testString2, 25, Encoding.UTF8);

				stream.Seek(0, SeekOrigin.Begin);

				Reader reader = new Reader(stream);
				string resultString1 = reader.ReadFixedLengthString(25, Encoding.UTF8);
				string resultString2 = reader.ReadFixedLengthString(25, Encoding.UTF8);

				Assert.AreEqual(testString1, resultString1);
				Assert.AreEqual(testString2, resultString2);
			}
		}

		[TestMethod]
		public void TestReadAndWriteStringArray()
		{
			Random r = new Random();
			var testStrings = new string[] {
				 r.RandomString(10, 20),
				 r.RandomString(10, 20),
				 r.RandomString(10, 20),
				 r.RandomString(10, 20),
				 r.RandomString(10, 20)
			};

			using (MemoryStream stream = new MemoryStream())
			{
				Writer writer = new Writer(stream);
				writer.WriteVariableLengthArray(testStrings, typeof(string), stringEncoding: Encoding.Default);

				stream.Seek(0, SeekOrigin.Begin);

				Reader reader = new Reader(stream);
				var result = reader.ReadVariableLengthArray<string>(stringEncoding: Encoding.Default);

				Assert.AreEqual(testStrings.Length, result.Length);
				for (int i = 0; i < testStrings.Length; i++)
				{
					Assert.AreEqual(testStrings[i], result[i]);
				}
			}
		}

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
			DateTime dt2 = DateTime.Today.AddSeconds(r.Next(86400));
			DateTime dt3 = DateTime.Today.AddSeconds(r.Next(86400));

			using (MemoryStream stream = new MemoryStream())
			{
				Writer writer = new Writer(stream);
				writer.WriteByte(b);
				writer.WriteInt16(s);
				writer.WriteInt32(i);
				writer.WriteInt64(l);
				writer.WriteSingle(f);
				writer.WriteDouble(d);
				writer.WriteDateTime(dt1);
				writer.WriteOleDateTime(dt2);
				writer.WriteTimeStamp(dt3);

				stream.Seek(0, SeekOrigin.Begin);

				Reader reader = new Reader(stream);
				byte rb = reader.ReadByte();
				short rs = reader.ReadInt16();
				int ri = reader.ReadInt32();
				long rl = reader.ReadInt64();
				float rf = reader.ReadSingle();
				double rd = reader.ReadDouble();
				DateTime rdt1 = reader.ReadDateTime();
				DateTime rdt2 = reader.ReadOleDateTime();
				DateTime rdt3 = reader.ReadTimeStamp();

				Assert.AreEqual(b, rb);
				Assert.AreEqual(s, rs);
				Assert.AreEqual(i, ri);
				Assert.AreEqual(l, rl);

				Assert.AreEqual(f, rf);
				Assert.AreEqual(d, rd);

				Assert.AreEqual(dt1, rdt1);
				Assert.AreEqual(dt2, rdt2);
				Assert.AreEqual(dt3, rdt3);

			}
		}

		private class TestRWClass : IReadable, IWritable
		{
			[Field(0)]
			public byte b { get; set; }

			[Field(1)]
			public short s { get; set; }

			[Field(2)]
			public int i { get; set; }

			[Field(3)]
			public long l { get; set; }

			[Field(4)]
			public float f { get; set; }

			[Field(5)]
			public double d { get; set; }

			[Field(6)]
			public DateTime dt1 { get; set; }

			[Field(7)]
			public DateTime dt2 { get; set; }

			[Field(8)]
			public DateTime dt3 { get; set; }

			[Field(9)]
			public TimeSpan ts { get; set; }

			[Field(9, FieldEncoding: FieldEncodingEnum.VariableLength, StringEncoding: "UTF-8")]
			public string s1 { get; set; }

			[Field(10, FieldEncoding: FieldEncodingEnum.NullTerminated, StringEncoding: "ISO-8859-1")]
			public string s2 { get; set; }

			[Field(11, length: 20, FieldEncoding: FieldEncodingEnum.FixedLength, StringEncoding: "ISO-8859-1")]
			public string s3 { get; set; }

			[Field(12, StringEncoding: "ISO-8859-1")]
			public string[] stringsArray { get; set; }

			[Field(13, StringEncoding: "ISO-8859-1")]
			public List<string> stringsList { get; set; }

			[Field(14)]
			public List<double> doubleList { get; set; }
		}

		[TestMethod]
		public void TestClass()
		{
			Random r = new Random();
			var obj = new TestRWClass()
			{
				b = r.RandomByte(),
				s = r.RandomShort(),
				i = r.RandomInt(),
				l = r.RandomLong(),
				f = r.RandomFloat(),
				d = r.RandomDouble(),
				dt1 = DateTime.Now,
				dt2 = DateTime.Today.AddSeconds(r.Next(86400)),
				dt3 = DateTime.Today.AddSeconds(r.Next(86400)),
				s1 = r.RandomString(10, 20),
				s2 = r.RandomString(10, 20),
				s3 = r.RandomString(10, 20),
				stringsArray = r.RandomArray(5, 10, i => r.RandomString(5, 20)),
				stringsList = new List<string>(r.RandomArray(5, 10, i => r.RandomString(5, 20))),
				doubleList = new List<double>(r.RandomArray(5, 10, i => r.RandomDouble()))
			};

			using (MemoryStream stream = new MemoryStream())
			{
				Writer writer = new Writer(stream);
				writer.Write(obj);

				stream.Position = 0;

				Reader reader = new Reader(stream);
				var result = reader.Read<TestRWClass>();

				Assert.AreEqual(obj.b, result.b);
				Assert.AreEqual(obj.s, result.s);
				Assert.AreEqual(obj.i, result.i);
				Assert.AreEqual(obj.l, result.l);
				Assert.AreEqual(obj.f, result.f);
				Assert.AreEqual(obj.d, result.d);
				Assert.AreEqual(obj.dt1, result.dt1);
				Assert.AreEqual(obj.dt2, result.dt2);
				Assert.AreEqual(obj.dt3, result.dt3);
				Assert.AreEqual(obj.ts, result.ts);
				Assert.AreEqual(obj.s1, result.s1);
				Assert.AreEqual(obj.s2, result.s2);
				Assert.AreEqual(obj.s3, result.s3);
				var stringsArrayComparer = new ArrayEqualityComparer<string>();
				Assert.IsTrue(stringsArrayComparer.Equals(obj.stringsArray, result.stringsArray));
				Assert.IsTrue(stringsArrayComparer.Equals(obj.stringsList.ToArray(), result.stringsList.ToArray()));
				var doubleArrayComparer = new ArrayEqualityComparer<double>();
				Assert.IsTrue(doubleArrayComparer.Equals(obj.doubleList.ToArray(), result.doubleList.ToArray()));
			}
		}
	}
}