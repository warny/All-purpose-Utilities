using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

[TestClass]
public class SwitchTests
{
    [TestMethod]
    public void SimpleSwitch()
    {
        var expression = "(int i) => switch(i) { case 1: 10; case 2: 20; default: 0; }";
        var lambda = ExpressionParser.Parse<Func<int, int>>(expression);
        var func = lambda.Compile();

        Assert.AreEqual(10, func(1));
        Assert.AreEqual(20, func(2));
        Assert.AreEqual(0, func(3));
    }

    [TestMethod]
    public void SwitchStatement()
    {
        var expression = "(int i) => { int v = 0; switch(i) { case 1: v = 10; break; case 2: v = 20; break; default: v = 0; break; } return v; }";
        var lambda = ExpressionParser.Parse<Func<int, int>>(expression);
        var func = lambda.Compile();

        Assert.AreEqual(10, func(1));
        Assert.AreEqual(20, func(2));
        Assert.AreEqual(0, func(3));
    }
}

