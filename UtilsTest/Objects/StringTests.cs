using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Schema;
using Utils.Arrays;
using Utils.Collections;
using Utils.Mathematics;
using Utils.Objects;

namespace UtilsTest.Objects
{
	[TestClass]
	public class StringTests
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
				( Start: -2, End: 2, Result: "DE"),
				( Start: -1, End: 2, Result: "E"),
				};

			foreach (var (Start, End, Result) in tests)
			{
				Assert.AreEqual(Result, testString.Mid(Start, End));
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

			foreach (var (Length, Result) in tests)
			{
				Assert.AreEqual(Result, testString.Left(Length));
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

			foreach (var (Length, Result) in tests)
			{
				Assert.AreEqual(Result, testString.Right(Length));
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

			foreach (var (Function, Result, ResultLeft, ResultRight) in tests)
			{
				Assert.AreEqual(Result, testString.Trim(Function));
				Assert.AreEqual(ResultLeft, testString.TrimStart(Function));
				Assert.AreEqual(ResultRight, testString.TrimEnd(Function));
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

			foreach (var (WildCard, IgnoreCase) in tests)
			{
				Assert.IsTrue(testString.Like(WildCard, IgnoreCase));
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

			foreach (var (WildCard, IgnoreCase) in tests)
			{
				Assert.IsFalse(testString.Like(WildCard, IgnoreCase));
			}
		}

		[TestMethod]
		public void ToPluralTest()
		{
			var tests = new (string input, int number, string expected)[]{
				("child(ren)", 1, "child"),
				("child(ren)", 2, "children"),
				("vert(ex|ices)", 1, "vertex"),
				("vert(ex|ices)", 2, "vertices"),
				("chev(al|aux)", 1, "cheval"),
				("chev(al|aux)", 2, "chevaux"),
			};

			foreach (var (input, number, expected) in tests)
			{
				Assert.AreEqual(expected, input.ToPlural(number));
			}
		}

		[TestMethod]
		public void CommandLineParser()
		{
			var comparer = EnumerableEqualityComparer<string>.Default;

			var tests = new (string line, string[] expected)[] {
				("", new string [] { }),
				("a", new string[] { "a" }),
				(" abc ", new string[] { "abc" }),
				("a b ", new string[] { "a", "b" }),
				("a b \"c d\"", new string[] { "a", "b", "c d" }),
				("a b \"c\"\"d\"", new string[] { "a", "b", "c\"d" }),
				(@"/src:""C:\tmp\Some Folder\Sub Folder"" /users:""abcdefg@hijkl.com"" tasks:""SomeTask,Some Other Task"" -someParam", new string[] { @"/src:""C:\tmp\Some Folder\Sub Folder""", @"/users:""abcdefg@hijkl.com""", @"tasks:""SomeTask,Some Other Task""", @"-someParam" })
			};

			foreach (var (line, expected) in tests)
			{
				var result = StringUtils.ParseCommandLine(line);
				Assert.IsTrue(comparer.Equals(expected, result));
			}
		}

	}
}
