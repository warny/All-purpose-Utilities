using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
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

		[TestMethod]
		public void ParserTest5()
		{
			var tests = new (string String, Guid Value)[] {
				("6e1b3671-0038-4055-b8ee-1316bf478b5e", new Guid("6e1b3671-0038-4055-b8ee-1316bf478b5e")),
				("17914f8d-61e3-4aeb-8f34-ff74f33fcaf4", new Guid("17914f8d-61e3-4aeb-8f34-ff74f33fcaf4")),
				("fb24ae1d-df9d-447b-9890-573e1fa8c512", new Guid("fb24ae1d-df9d-447b-9890-573e1fa8c512")),
				("5f39b160-d006-474a-ab9c-cd6deaf3961b", new Guid("5f39b160-d006-474a-ab9c-cd6deaf3961b")),
				("13742841-cca8-40b5-955e-5f2c57e9ea35", new Guid("13742841-cca8-40b5-955e-5f2c57e9ea35"))
			};

			Assert.IsTrue(Parsers.CanParse<Guid>());
			foreach (var test in tests)
			{
				Assert.AreEqual(test.Value, Parsers.Parse<Guid>(test.String));
			}
		}
	}
}
