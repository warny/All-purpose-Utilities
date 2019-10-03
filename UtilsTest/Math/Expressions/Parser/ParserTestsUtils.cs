using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions.Parser;

namespace UtilsTest.Math.Expressions.Parser
{
	public static class ParserTestsUtils
	{
		public static void AssertTrueResult(string testString, Result result)
		{
			Assert.IsTrue(result.Success);
			Assert.AreEqual(testString, result.Index.Value);
			Assert.AreEqual(0, result.Index.Start);
			Assert.AreEqual(testString.Length, result.Index.End);
			Assert.AreEqual(testString, result.Index.Value);
		}

	}
}
