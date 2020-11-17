using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Schema;
using Utils.Arrays;
using Utils.Mathematics;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class StringTest
	{
		[TestMethod]
		public void IsNumberTest1()
		{
			string[] testStrings = {
				"1234567890", 
				"-1234567890", 
				"12345.67890",
				"-12345.67890"
			};
			foreach (var testString in testStrings)
			{
				Assert.IsTrue(testString.IsNumber(CultureInfo.InvariantCulture));
			}
		}


		[TestMethod]
		public void IsNumberTest2()
		{
			string[] testStrings = {
				"ABCDEF",
				"-12345ABCD",
				"-12345.678.90"
			};
			foreach (var testString in testStrings)
			{
				Assert.IsFalse(testString.IsNumber(CultureInfo.InvariantCulture));
			}
		}

		[TestMethod]
		public void MidTest()
		{
			string testString = "ABCDE";

			var tests = new[] {
				( Start: 1, End: -4, Result: "AB"),
				( Start: 1, End: -1, Result: "B"),
				( Start: 0, End: 5, Result: "ABCDE"),
				( Start: 0, End: 3, Result: "ABC"),
				( Start: 2, End: 3, Result: "CDE"),
				( Start: 1, End: 5, Result: "BCDE"),
				( Start: 2, End: -2, Result: "BC"),
				( Start: -2, End: -2, Result: "CD"),
				( Start: -5, End: -2, Result: "A"),
				( Start: -1, End: 2, Result: "E"),
				};

			foreach (var test in tests)
			{
				Assert.AreEqual(test.Result, testString.Mid(test.Start, test.End));
			}
		}

		[TestMethod]
		public void LeftTest()
		{
			string testString = "ABCDE";

			var tests = new[] {
				( Length: 5, Result: "ABCDE"),
				( Length: 3, Result: "ABC"),
				( Length: 6, Result: "ABCDE")
			};

			foreach (var test in tests)
			{
				Assert.AreEqual(test.Result, testString.Left(test.Length));
			}
		}

		[TestMethod]
		public void RightTest()
		{
			string testString = "ABCDE";

			var tests = new[] {
				( Length: 5, Result: "ABCDE"),
				( Length: 3, Result: "CDE"),
				( Length: 6, Result: "ABCDE")
				};

			foreach (var test in tests)
			{
				Assert.AreEqual(test.Result, testString.Right(test.Length));
			}
		}

		[TestMethod]
		public void TrimTest()
		{
			string testString = "-+/ABCDE-+/";

			var tests = new (Func<char, bool> Function, string Result, string ResultLeft, string ResultRight)[] {
				( Function: (char c) => c.In('-', '+', '/'), Result: "ABCDE", ResultLeft: "ABCDE-+/", ResultRight: "-+/ABCDE"),
				( Function: (char c) => c.In('-'), Result: "+/ABCDE-+/", ResultLeft: "+/ABCDE-+/", ResultRight: "-+/ABCDE-+/"),
				( Function: (char c) => c.In('/'), Result: "-+/ABCDE-+", ResultLeft: "-+/ABCDE-+/", ResultRight: "-+/ABCDE-+"),
				};

			foreach (var test in tests)
			{
				Assert.AreEqual(test.Result, testString.Trim(test.Function));
				Assert.AreEqual(test.ResultLeft, testString.TrimStart(test.Function));
				Assert.AreEqual(test.ResultRight, testString.TrimEnd(test.Function));
			}
		}

		[TestMethod]
		public void LikeTest1()
		{
			string testString = "ABCDE";

			var tests = new[] {
				( WildCard: "ABCDE", IgnoreCase: false),
				( WildCard: "abcde", IgnoreCase: true),
				( WildCard: "ABC*", IgnoreCase: false),
				( WildCard: "ABC??", IgnoreCase: false),
				( WildCard: "*CDE", IgnoreCase: false),
				( WildCard: "??CDE", IgnoreCase: false),
				( WildCard: "?BCD?", IgnoreCase: false),
				( WildCard: "*BCD*", IgnoreCase: false),
				( WildCard: "*C*", IgnoreCase: false)
				};

			foreach (var test in tests)
			{
				Assert.IsTrue(testString.Like(test.WildCard, test.IgnoreCase));
			}
		}

		[TestMethod]
		public void LikeTest2()
		{
			string testString = "ABCDE";

			var tests = new[] {
				( WildCard: "ABC", IgnoreCase: false),
				( WildCard: "abcde", IgnoreCase: false),
				( WildCard: "BACDE", IgnoreCase: false),
				( WildCard: "ABC?", IgnoreCase: false),
				( WildCard: "?CDE", IgnoreCase: false),
				( WildCard: "BCD?", IgnoreCase: false),
				( WildCard: "*BCD", IgnoreCase: false),
				( WildCard: "BCD*", IgnoreCase: false),
				( WildCard: "C*", IgnoreCase: false),
				( WildCard: "C*", IgnoreCase: false)
				};

			foreach (var test in tests)
			{
				Assert.IsFalse(testString.Like(test.WildCard, test.IgnoreCase));
			}
		}

		[TestMethod]
		public void CommandLineParser()
		{
			var comparer = new ArrayEqualityComparer<string>();

			var tests = new (string line, string[] expected)[] {
				("", new string [] { }),
				("a", new string[] { "a" }),
				(" abc ", new string[] { "abc" }),
				("a b ", new string[] { "a", "b" }),
				("a b \"c d\"", new string[] { "a", "b", "c d" }),
				(@"/src:""C:\tmp\Some Folder\Sub Folder"" /users:""abcdefg@hijkl.com"" tasks:""SomeTask,Some Other Task"" -someParam", new string[] { @"/src:""C:\tmp\Some Folder\Sub Folder""", @"/users:""abcdefg@hijkl.com""", @"tasks:""SomeTask,Some Other Task""", @"-someParam" })
			};

			foreach (var test in tests)
			{
				var result = StringUtils.ParseCommandLine(test.line);
				Assert.IsTrue(comparer.Equals(test.expected, result));
			}
		}

	}
}
