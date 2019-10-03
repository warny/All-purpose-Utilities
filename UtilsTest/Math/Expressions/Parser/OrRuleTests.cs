using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Mathematics.Expressions.Parser;
using Utils.Objects;

namespace UtilsTest.Math.Expressions.Parser
{
	[TestClass]
	public class OrRuleTests
	{
		[TestMethod]
		public void SimpleOr1()
		{
			Random r = new Random();
			string[] testStrings = 
			{
				r.RandomString(r.Next(5, 10)),
				r.RandomString(r.Next(5, 10)),
				r.RandomString(r.Next(5, 10)),
			};

			var testRule = Rules.Or(testStrings.Select(s => Rules.String(s)));
			var testString = testStrings[r.Next(0, testStrings.Length - 1)];
			var result = RuleTester.Test(testRule, testString);
			ParserTestsUtils.AssertTrueResult(testString, result);
		}

		[TestMethod]
		public void SimpleOr2()
		{
			Random r = new Random();
			string[] testStrings = new string[3];
			testStrings[0] = r.RandomString(r.Next(2, 4));
			testStrings[1] = testStrings[0] + r.RandomString(r.Next(2, 4));
			testStrings[2] = testStrings[1] + r.RandomString(r.Next(2, 4));

			var testRule = Rules.Or(testStrings.Select(s => Rules.String(s)));
			var testString = testStrings[testStrings.Length - 1];
			var result = RuleTester.Test(testRule, testString);
			ParserTestsUtils.AssertTrueResult(testString, result);
		}
	}
}
