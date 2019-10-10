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
	}
}
