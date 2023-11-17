using System;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

[TestClass]
public class SimpleComputationTests
{
    [TestMethod]
    public void AdditionTests()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(int x, int y) => x + y");
        var f = (Func<int, int, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            (int x, int y) = (r.Next(), r.Next());

            Assert.AreEqual(x + y, f(x, y));
        }

    }

    [TestMethod]
    public void SubstractionTest()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(int x, int y) => x - y");
        var f = (Func<int, int, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            (int x, int y) = (r.Next(), r.Next());

            Assert.AreEqual(x - y, f(x, y));
        }

    }

    [TestMethod]
    public void MultiplicationTests()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(double x, double y) => x * y");
        var f = (Func<double, double, double>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            (double x, double y) = (r.Next(), r.Next());

            Assert.AreEqual(x * y, f(x, y));
        }

    }

    [TestMethod]
    public void DivisionTest()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(double x, double y) => x / y");
        var f = (Func<double, double, double>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            (double x, double y) = (r.Next(), r.Next());

            Assert.AreEqual(x / y, f(x, y));
        }

    }

    [TestMethod]
    public void PriorityTest1()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(double x, double y, double z) => x * y + z");
        var f = (Func<double, double, double, double>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            (double x, double y, double z) = (r.Next(), r.Next(), r.Next());

            Assert.AreEqual(x * y + z, f(x, y, z));
        }

    }

    [TestMethod]
    public void PriorityTest2()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(double x, double y, double z) => x + y * z");
        var f = (Func<double, double, double, double>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            (double x, double y, double z) = (r.Next(), r.Next(), r.Next());

            Assert.AreEqual(x + y * z, f(x, y, z));
        }

    }

    [TestMethod]
    public void ParenthesisTest1()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(double x, double y, double z) => x * (y + z)");
        var f = (Func<double, double, double, double>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            (double x, double y, double z) = (r.Next(), r.Next(), r.Next());

            Assert.AreEqual(x * (y + z), f(x, y, z));
        }

    }

    [TestMethod]
    public void ParenthesisTest2()
    {
        var r = new Random();

        var e = ExpressionParser.Parse("(double x, double y, double z) => (x + y) * z");
        var f = (Func<double, double, double, double>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            (double x, double y, double z) = (r.Next(), r.Next(), r.Next());

            Assert.AreEqual((x + y) * z, f(x, y, z));
        }

    }

}