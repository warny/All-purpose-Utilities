using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions.Parser;

namespace UtilsTest.Math.Expressions.Parser
{
	[TestClass]
	public class CharRulesTests
	{
		[TestMethod]
		public void ValidChar()
		{
			string testString = "aaaaaa";
			Rule testRule = Rules.Chars("abcdef") * 6;
			var result = RuleTester.Test(testRule, testString);
			ParserTestsUtils.AssertTrueResult(testString, result);
		}


		[TestMethod]
		public void InvalidChar()
		{
			string testString = "aaaaaa";
			Rule testRule = Rules.Chars("bcdef") * 6;
			var result = RuleTester.Test(testRule, testString);
			Assert.AreEqual(false, result.Success);
		}
	}
}
