using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Utils.Arrays;
using Utils.Streams.BaseEncoding;

namespace UtilsTest.BaseEncoding
{
	[TestClass]
	public class BaseDecoderStreamTest
	{
		[TestMethod]
		public void Base16Test1()
		{
			string source = "01020304";
			byte[] target = { 1, 2, 3, 4 };

			var stream = new MemoryStream();
			var decoder = new BaseDecoderStream(stream, Bases.Base16);
			decoder.Write(source);
			decoder.Close();

			byte[] result = stream.ToArray();

			var comparer = new ArrayEqualityComparer<byte>();
			Assert.IsTrue(comparer.Equals(target, result));
		}

		[TestMethod]
		public void Base16Test2()
		{
			string source = "4142434445";
			byte[] target = { 0x41, 0x42, 0x43, 0x44, 0x45 };

			var stream = new MemoryStream();
			var decoder = new BaseDecoderStream(stream, Bases.Base16);
			decoder.Write(source);
			decoder.Close();

			byte[] result = stream.ToArray();

			var comparer = new ArrayEqualityComparer<byte>();
			Assert.IsTrue(comparer.Equals(target, result));
		}

		[TestMethod]
		public void Base32Test1()
		{
			string source = "IFBEGRCF";
			byte[] target = { 0x41, 0x42, 0x43, 0x44, 0x45 };

			var stream = new MemoryStream();
			var decoder = new BaseDecoderStream(stream, Bases.Base32);
			decoder.Write(source);
			decoder.Close();

			byte[] result = stream.ToArray();

			var comparer = new ArrayEqualityComparer<byte>();
			Assert.IsTrue(comparer.Equals(target, result));
		}

		[TestMethod]
		public void Base32Test2()
		{
			string source = "IFBA====";
			byte[] target = { 0x41, 0x42 };

			var stream = new MemoryStream();
			var decoder = new BaseDecoderStream(stream, Bases.Base32);
			decoder.Write(source);
			decoder.Close();

			byte[] result = stream.ToArray();

			var comparer = new ArrayEqualityComparer<byte>();
			Assert.IsTrue(comparer.Equals(target, result));
		}

		[TestMethod]
		public void Base64Test1()
		{
			string source = "QUJDREU=";
			byte[] target = { 0x41, 0x42, 0x43, 0x44, 0x45 };

			var stream = new MemoryStream();
			var decoder = new BaseDecoderStream(stream, Bases.Base64);
			decoder.Write(source);
			decoder.Close();

			byte[] result = stream.ToArray();

			var comparer = new ArrayEqualityComparer<byte>();
			Assert.IsTrue(comparer.Equals(target, result));
		}

		[TestMethod]
		public void Base64Test2()
		{
			string source = "QUI=";
			byte[] target = { 0x41, 0x42 };

			var stream = new MemoryStream();
			var decoder = new BaseDecoderStream(stream, Bases.Base64);
			decoder.Write(source);
			decoder.Close();

			byte[] result = stream.ToArray();

			var comparer = new ArrayEqualityComparer<byte>();
			Assert.IsTrue(comparer.Equals(target, result));
		}

	}
}
