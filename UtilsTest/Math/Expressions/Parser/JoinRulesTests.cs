using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics.Expressions.Parser;
using Utils.Objects;

namespace UtilsTest.Math.Expressions.Parser
{
	[TestClass]
	public class JoinRulesTests
	{
		[TestMethod]
		public void SimpleRepetition()
		{
			Random r = new Random();
			string testString = StringUtils.RandomString(5);
			int repetition = r.Next(3, 5);
			string repetedString = testString.Repeat(repetition);

			Rule rule = Rules.String(testString) * repetition;
			var (result, context) = RuleTester.Test(rule, repetedString);
			ParserTestsUtils.AssertTrueResult(repetedString, result);
		}

		[TestMethod]
		public void ComplexRepetition1()
		{
			Random r = new Random();
			string testString = StringUtils.RandomString(5);
			int maxRepetition = r.Next(5, 8);
			int minRepetition = r.Next(3, maxRepetition);
			int repetition = r.Next(minRepetition, maxRepetition);
			string repetedString = testString.Repeat(repetition);

			Rule rule = Rules.String(testString) * (minRepetition, maxRepetition);
			var (result, context) = RuleTester.Test(rule, repetedString);
			ParserTestsUtils.AssertTrueResult(repetedString, result);
		}

		[TestMethod]
		public void FalseRepetition()
		{
			string testString = "aaaaaa";
			Rule testRule = Rules.Chars("abcdef") * 5;
			var (result, context) = RuleTester.Test(testRule, testString);
			Assert.IsFalse(result.Success);
		}

		[TestMethod]
		public void ComplexRepetition2()
		{
			Rule testRule = Rules.String("ab") * (2,3) + Rules.String("a");

			string testString1 = "ababa";
			var (result1, context1) = RuleTester.Test(testRule, testString1);
			ParserTestsUtils.AssertTrueResult(testString1, result1);

			string testString2 = "abababa";
			var (result2, context2) = RuleTester.Test(testRule, testString2);
			ParserTestsUtils.AssertTrueResult(testString2, result2);
		}

		[TestMethod]
		public void FalseComplexRepetition2()
		{
			Rule testRule = Rules.String("ab") * (2, 3) + Rules.String("a");
			
			string testString1 = "ababab";
			var (result1, context1) = RuleTester.Test(testRule, testString1);
			Assert.IsFalse(result1.Success);
			
			string testString2 = "abababab";
			var (result2, context2) = RuleTester.Test(testRule, testString2);
			Assert.IsFalse(result2.Success);
		}

		[TestMethod]
		public void SequenceWithOr()
		{
			Random r = new Random();
			string[] testStrings = new string[3];
			testStrings[0] = r.RandomString(r.Next(2, 4));
			testStrings[1] = testStrings[0] + r.RandomString(r.Next(2, 4));
			testStrings[2] = testStrings[1] + r.RandomString(r.Next(2, 4));
			string tail = r.RandomString(r.Next(2, 4));

			var testString = testStrings[1] + tail;

			var testRule = Rules.Or(testStrings.Select(s => Rules.String(s))) + Rules.String(tail);

			var (result, context) = RuleTester.Test(testRule, testString);
			ParserTestsUtils.AssertTrueResult(testString, result); 

		}

	}
}
