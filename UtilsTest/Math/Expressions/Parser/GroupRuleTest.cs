using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Utils.Mathematics.Expressions.Parser;
using Utils.Objects;

namespace UtilsTest.Math.Expressions.Parser
{
	[TestClass]
	public class GroupRuleTest
	{
		[TestMethod]
		public void SimpleGroup()
		{
			Random r = new Random();
			string testString = StringUtils.RandomString(5);
			int repetition = r.Next(5, 7);
			string repetedString = testString.Repeat(repetition);

			var rule = Rules.Group("test", Rules.String(testString)) + Rules.GroupReference("test") * (repetition - 1);
			var result = RuleTester.Test(rule, repetedString);
			ParserTestsUtils.AssertTrueResult(repetedString, result);

		}

		[TestMethod]
		public void SimpleGroup2()
		{
			Random r = new Random();
			string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			string testString1 = StringUtils.RandomString(5, 10, chars);
			string testString2 = StringUtils.RandomString(5);
			int repetition = r.Next(5, 7);
			string testString = testString1 + testString2 + testString1.Repeat(repetition);

			var rule = Rules.Group("test", Rules.Chars(chars) * (5, 10) ) + Rules.String(testString2)  + Rules.GroupReference("test") * repetition;
			var result = RuleTester.Test(rule, testString);
			ParserTestsUtils.AssertTrueResult(testString, result);

		}

	}
}
