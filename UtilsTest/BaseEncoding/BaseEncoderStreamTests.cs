using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.IO.BaseEncoding;

namespace UtilsTest.BaseEncoding
{
	[TestClass]
	public class BaseEncoderStreamTests
	{

		[TestMethod]
		public void Base16Test1()
		{
			byte[] source = { 1, 2 , 3, 4 };
			string target = "01020304";

			var stringWriter = new StringWriter();
			var stream = new BaseEncoderStream(stringWriter, Bases.Base16);
			stream.Write(source, 0, source.Length);
			stream.Close();
			Assert.AreEqual(target, stringWriter.ToString());
		}

		[TestMethod]
		public void Base16Test2()
		{
			byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
			string target = "4142434445";

			var stringWriter = new StringWriter();
			var stream = new BaseEncoderStream(stringWriter, Bases.Base16);
			stream.Write(source, 0, source.Length);
			stream.Close();
			Assert.AreEqual(target, stringWriter.ToString());
		}

		[TestMethod]
		public void Base32Test1()
		{
			byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
			string target = "IFBEGRCF";

			var stringWriter = new StringWriter();
			var stream = new BaseEncoderStream(stringWriter, Bases.Base32);
			stream.Write(source, 0, source.Length);
			stream.Close();
			Assert.AreEqual(target, stringWriter.ToString());
		}

		[TestMethod]
		public void Base32Test2()
		{
			byte[] source = { 0x41, 0x42 };
			string target = "IFBA====";

			var stringWriter = new StringWriter();
			var stream = new BaseEncoderStream(stringWriter, Bases.Base32);
			stream.Write(source, 0, source.Length);
			stream.Close();
			Assert.AreEqual(target, stringWriter.ToString());
		}

		[TestMethod]
		public void Base32Test3()
		{
			byte[] source = { 0x41, 0x42, 0x43 };
			string target = "IFBEG===";

			var stringWriter = new StringWriter();
			var stream = new BaseEncoderStream(stringWriter, Bases.Base32);
			stream.Write(source, 0, source.Length);
			stream.Close();
			Assert.AreEqual(target, stringWriter.ToString());
		}

		[TestMethod]
		public void Base64Test1()
		{
			byte[] source = { 0x41, 0x42, 0x43, 0x44, 0x45 };
			string target = "QUJDREU=";

			var stringWriter = new StringWriter();
			var stream = new BaseEncoderStream(stringWriter, Bases.Base64);
			stream.Write(source, 0, source.Length);
			stream.Close();
			Assert.AreEqual(target, stringWriter.ToString());
		}

		[TestMethod]
		public void Base64Test2()
		{
			byte[] source = { 0x41, 0x42 };
			string target = "QUI=";

			var stringWriter = new StringWriter();
			var stream = new BaseEncoderStream(stringWriter, Bases.Base64);
			stream.Write(source, 0, source.Length);
			stream.Close();
			Assert.AreEqual(target, stringWriter.ToString());
		}

		[TestMethod]
		public void Base64Test3()
		{
			byte[] source = { 0x41, 0x42, 0x43 };
			string target = "QUJD";

			var stringWriter = new StringWriter();
			var stream = new BaseEncoderStream(stringWriter, Bases.Base64);
			stream.Write(source, 0, source.Length);
			stream.Close();
			Assert.AreEqual(target, stringWriter.ToString());
		}
	}
}
