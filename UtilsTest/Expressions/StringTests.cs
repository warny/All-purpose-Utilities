using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

[TestClass]
public class StringTests
{

    [TestMethod]
    public void SimpleConcatenationTest()
    {
        string[] tests = [
            "Test0",
            "Test1",
            "Test2",
            "Test3",
            "Test4",
            "Test5",
            "Test6",
            "Test7",
            "Test8",
            "Test9",
            ];

        for (int i = 1; i < tests.Length; i++)
        {
            var subTest = tests[0..i];

            var expression = "(item) => " + string.Join(" + ", subTest.Select((s, i) => $"item[{i}]"));
            var e = ExpressionParser.Parse<Func<string[], string>>(expression);
            var f = e.Compile();

            Assert.AreEqual(string.Concat(subTest), f(subTest));
        }
    }

}
