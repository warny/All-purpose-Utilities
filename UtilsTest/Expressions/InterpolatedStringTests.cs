using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Expressions;

namespace UtilsTest.Expressions;

[TestClass]
public class InterpolatedStringTests
{
    [TestMethod]
    public void SimpleInterpolation()
    {
        var expr = ExpressionParser.Parse<Func<string, string, string>>("(a, b) => $\"{a} {b}!\"");
        var func = expr.Compile();
        Assert.AreEqual("hello world!", func("hello", "world"));
    }
}
