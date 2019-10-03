using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics.Expressions.Parser;

namespace UtilsTest.Math.Expressions.Parser
{
	[TestClass]
	public class CharRulesTest
	{
		[TestMethod]
		public void ValidChar()
		{
			string testString = "aaaaaa";
			Rule testRule = Rules.Chars("abcdef") * 6;
			var result = RuleTester.Test(testRule, testString);
			Assert.AreEqual(true, result.Success);
			Assert.AreEqual(testString, result.Index.Value);
			Assert.AreEqual(0, result.Index.Start);
			Assert.AreEqual(testString.Length, result.Index.End);
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
