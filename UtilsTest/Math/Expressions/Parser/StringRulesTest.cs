using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics.Expressions.Parser;
using Utils.Mathematics.Expressions.Parser.RulesImplementations;
using Utils.Objects;

namespace UtilsTest.Math.Expressions.Parser
{
	[TestClass]
	public class StringRulesTest
	{
		[TestMethod]
		public void ValidString()
		{
			string tested = StringUtils.RandomString(10, 20);
			var tester = Rules.String(tested);
			var (result, context) = RuleTester.Test(tester, tested);
			ParserTestsUtils.AssertTrueResult(tested, result);
		}

		[TestMethod]
		public void ValidCharChain()
		{
			string tested = StringUtils.RandomString(10, 20);
			var tester = Rules.Sequence(tested.Select(c=>Rules.Chars(c)));
			var (result, context) = RuleTester.Test(tester, tested);
			ParserTestsUtils.AssertTrueResult(tested, result);
		}


		[TestMethod]
		public void InvalidString()
		{
			string tested = StringUtils.RandomString(10, 20);
			var tester = Rules.String(StringUtils.RandomString(10, 20));
			var (result, context) = RuleTester.Test(tester, tested);
			Assert.IsFalse(result.Success);
		}

		[TestMethod]
		public void ZeroLengthString()
		{
			Assert.ThrowsException<ArgumentNullException>(() => Rules.String(""), "Chaine vide");
		}

		[TestMethod]
		public void StringTestAddition() {
			string tested1 = StringUtils.RandomString(10, 20);
			string tested2 = StringUtils.RandomString(10, 20);

			var tester1 = Rules.String(tested1);
			var tester2 = Rules.String(tested2);

			var tested = tested1 + tested2;
			var tester = tester1 + tester2;

			var (result, context) = RuleTester.Test(tester, tested);
			Assert.IsInstanceOfType(tester, typeof(StringRule));
			ParserTestsUtils.AssertTrueResult(tested, result);
		}
	}
}
