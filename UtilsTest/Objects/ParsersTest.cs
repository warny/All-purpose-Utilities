using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class ParsersTest
	{
		[TestMethod]
		public void ParserTest1()
		{
			var tests = new (CultureInfo Culture, string String, DateTime Value)[] {
				(CultureInfo.InvariantCulture, "2008-12-15 18:32:06", new DateTime(2008, 12, 15, 18, 32, 6)),
				(CultureInfo.GetCultureInfo("en-US"), "12/15/2008 6:32:06 PM", new DateTime(2008, 12, 15, 18, 32, 6)),
				(CultureInfo.GetCultureInfo("fr-FR"), "15/12/2008 18:32:06", new DateTime(2008, 12, 15, 18, 32, 6)),
				(CultureInfo.GetCultureInfo("jp-JP"), "2008/12/15 18:32:06", new DateTime(2008, 12, 15, 18, 32, 6))
			};

			Assert.IsTrue(Parsers.CanParse(typeof(DateTime)));
			foreach (var test in tests)
			{
				Assert.AreEqual(test.Value, Parsers.Parse<DateTime>(test.String, test.Culture));
			}

		}

		[TestMethod]
		public void ParserTest2()
		{
			var tests = new (CultureInfo Culture, string String, double Value)[] {
				(CultureInfo.InvariantCulture, "123.456", 123.456),
				(CultureInfo.GetCultureInfo("en-US"), "123.456", 123.456),
				(CultureInfo.GetCultureInfo("fr-FR"), "123,456", 123.456),
				(CultureInfo.GetCultureInfo("jp-JP"), "123.456", 123.456)
			};

			Assert.IsTrue(Parsers.CanParse<double>());
			foreach (var test in tests)
			{
				Assert.AreEqual(test.Value, Parsers.Parse<double>(test.String, test.Culture));
			}

		}

		[TestMethod]
		public void ParserTest3()
		{
			var tests = new (CultureInfo Culture, string String, double? Value)[] {
				(CultureInfo.InvariantCulture, "123.456", 123.456),
				(CultureInfo.GetCultureInfo("en-US"), "", null),
				(CultureInfo.GetCultureInfo("fr-FR"), "", null),
				(CultureInfo.GetCultureInfo("jp-JP"), "", null)
			};

			Assert.IsTrue(Parsers.CanParse<double?>());
			foreach (var test in tests)
			{
				Assert.AreEqual(test.Value, Parsers.Parse<double?>(test.String, test.Culture));
			}

		}

		[TestMethod]
		public void ParserTest4()
		{
			var tests = new (CultureInfo Culture, string String, double? Value)[] {
				(CultureInfo.InvariantCulture, "123.456", 123.456),
				(CultureInfo.GetCultureInfo("en-US"), "", 1.0),
				(CultureInfo.GetCultureInfo("fr-FR"), "", 1.0),
				(CultureInfo.GetCultureInfo("jp-JP"), "", 1.0)
			};

			Assert.IsTrue(Parsers.CanParse<double?>());
			foreach (var test in tests)
			{
				Assert.AreEqual(test.Value, Parsers.ParseOrDefault(test.String, test.Culture, 1.0));
			}

		}
	}
}
