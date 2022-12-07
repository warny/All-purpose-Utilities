using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class StringConverterTests
	{
		[TestMethod]
		public void ToDateTimeConversionTest()
		{
			var tests = new (CultureInfo Culture, string String, DateTime Value)[] {
				(CultureInfo.InvariantCulture, "2008-12-15 18:32:06", new DateTime(2008, 12, 15, 18, 32, 6)),
				(CultureInfo.GetCultureInfo("en-US"), "12/15/2008 6:32:06 PM", new DateTime(2008, 12, 15, 18, 32, 6)),
				(CultureInfo.GetCultureInfo("fr-FR"), "15/12/2008 18:32:06", new DateTime(2008, 12, 15, 18, 32, 6)),
				(CultureInfo.GetCultureInfo("jp-JP"), "2008/12/15 18:32:06", new DateTime(2008, 12, 15, 18, 32, 6))
			};

			foreach (var test in tests)
			{
				var converter = new StringConverter(test.Culture);
				Assert.IsTrue(converter.CanConvert<string, DateTime>());
				Assert.AreEqual(test.Value, converter.Convert<string, DateTime>(test.String));
			}

		}

		[TestMethod]
		public void ToDoubleConverstionTest()
		{
			var tests = new (CultureInfo Culture, string String, double Value)[] {
				(CultureInfo.InvariantCulture, "123.456", 123.456),
				(CultureInfo.GetCultureInfo("en-US"), "123.456", 123.456),
				(CultureInfo.GetCultureInfo("fr-FR"), "123,456", 123.456),
			};

			foreach (var test in tests)
			{
				var converter = new StringConverter(test.Culture);
				Assert.IsTrue(converter.CanConvert<string, double>());
				Assert.AreEqual(test.Value, converter.Convert<string, double>(test.String));
			}
		}

		[TestMethod]
		public void ToNullableDoubleConverstionTest()
		{
			var tests = new (CultureInfo Culture, string String, double? Value)[] {
				(CultureInfo.InvariantCulture, "123.456", 123.456),
				(CultureInfo.GetCultureInfo("en-US"), "", null),
				(CultureInfo.GetCultureInfo("fr-FR"), "", null),
				(CultureInfo.GetCultureInfo("jp-JP"), "", null)
			};

			foreach (var test in tests)
			{
				var converter = new StringConverter(test.Culture);
				Assert.IsTrue(converter.CanConvert<string, double?>());
				Assert.AreEqual(test.Value, converter.Convert<string, double?>(test.String));
			}
		}

		[TestMethod]
		public void ToGuidConvertionTest()
		{
			var tests = new (string String, Guid Value)[] {
				("6e1b3671-0038-4055-b8ee-1316bf478b5e", new Guid("6e1b3671-0038-4055-b8ee-1316bf478b5e")),
				("17914f8d-61e3-4aeb-8f34-ff74f33fcaf4", new Guid("17914f8d-61e3-4aeb-8f34-ff74f33fcaf4")),
				("fb24ae1d-df9d-447b-9890-573e1fa8c512", new Guid("fb24ae1d-df9d-447b-9890-573e1fa8c512")),
				("5f39b160-d006-474a-ab9c-cd6deaf3961b", new Guid("5f39b160-d006-474a-ab9c-cd6deaf3961b")),
				("13742841-cca8-40b5-955e-5f2c57e9ea35", new Guid("13742841-cca8-40b5-955e-5f2c57e9ea35"))
			};

			var converter = new StringConverter();
			foreach (var test in tests)
			{
				Assert.IsTrue(converter.CanConvert<string, Guid>());
				Assert.AreEqual(test.Value, converter.Convert<string, Guid>(test.String));
			}
		}
	}
}
