using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

[TestClass]
public class OperationsTests
{
    [TestMethod]
    public void MemberTest()
    {
        string[] tests = ["a", "ab", "abc"];
        var expression = "(string s) => s.Length";

        var e = ExpressionParser.Parse(expression);
        var f = (Func<string, int>)e.Compile();

        foreach (var test in tests)
        {
            Assert.AreEqual(test.Length, f(test)); ;
        }

    }

    [TestMethod]
    public void NullOrMemberTest()
    {
        string[] tests = ["a", "ab", "abc", (string)null];
        var expression = "(string s) => s?.Length";

        var e = ExpressionParser.Parse(expression);
        var f = (Func<string, int?>)e.Compile();

        foreach (var test in tests)
        {
            var value = f(test);
            Assert.AreEqual(test?.Length, value); ;
        }

    }
}
