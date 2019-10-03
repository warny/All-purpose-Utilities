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
	public class JoinRulesTest
	{
		[TestMethod]
		public void SimpleRepetition()
		{
			Random r = new Random();
			string testString = StringUtils.RandomString(5);
			int repetition = r.Next(3, 5);
			string repetedString = testString.Repeat(repetition);
			Rule rule = Rules.String(testString) * repetition;
			RuleTester.Test(rule, repetedString);

			Assert.AreEqual(repetedString, rule.Result.Index.Value);
			Assert.AreEqual(0, rule.Result.Index.Start);
			Assert.AreEqual(repetedString.Length, rule.Result.Index.End);
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
			RuleTester.Test(rule, repetedString);

			Assert.AreEqual(repetedString, rule.Result.Index.Value);
			Assert.AreEqual(0, rule.Result.Index.Start);
			Assert.AreEqual(repetedString.Length, rule.Result.Index.End);
		}

		[TestMethod]
		public void FalseRepetition()
		{
			string testString = "aaaaaa";
			Rule testRule = Rules.Chars("abcdef") * 5;
			var result = RuleTester.Test(testRule, testString);
			Assert.AreEqual(false, result.Success);
		}

		[TestMethod]
		public void ComplexRepetition2()
		{
			string testString1 = "ababa";
			string testString2 = "abababa";
			Rule testRule = Rules.String("ab") * (2,3) + Rules.String("a");
			var result1 = RuleTester.Test(testRule, testString1);
			Assert.AreEqual(true, result1.Success);
			var result2 = RuleTester.Test(testRule, testString2);
			Assert.AreEqual(true, result2.Success);
		}

		[TestMethod]
		public void FalseComplexRepetition2()
		{
			string testString1 = "ababab";
			string testString2 = "abababab";
			Rule testRule = Rules.String("ab") * (2, 3) + Rules.String("a");
			var result1 = RuleTester.Test(testRule, testString1);
			Assert.AreEqual(false, result1.Success);
			var result2 = RuleTester.Test(testRule, testString2);
			Assert.AreEqual(false, result2.Success);
		}


	}
}
