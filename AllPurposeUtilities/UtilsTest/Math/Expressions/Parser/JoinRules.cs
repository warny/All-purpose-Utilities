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
	public class JoinRules
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
		public void ComplexRepetition()
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

	}
}
