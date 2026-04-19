using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates arithmetic computations compiled by <see cref="CSyntaxExpressionCompiler"/>.
/// </summary>
[TestClass]
public class SimpleComputationTests
{
    CSyntaxExpressionCompiler compiler = new CSyntaxExpressionCompiler();

    [TestMethod]
    public void AdditionTests()
    {
        var r = new Random();

        var e = (LambdaExpression)compiler.Compile("(int x, int y) => x + y");
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

        var e = (LambdaExpression)compiler.Compile("(int x, int y) => x - y");
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

        var e = (LambdaExpression)compiler.Compile("(double x, double y) => x * y");
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

        var e = (LambdaExpression)compiler.Compile("(double x, double y) => x / y");
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

        var e = (LambdaExpression)compiler.Compile("(double x, double y, double z) => x * y + z");
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

        var e = (LambdaExpression)compiler.Compile("(double x, double y, double z) => x + y * z");
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

        var e = (LambdaExpression)compiler.Compile("(double x, double y, double z) => x * (y + z)");
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

        var e = (LambdaExpression)compiler.Compile("(double x, double y, double z) => (x + y) * z");
        var f = (Func<double, double, double, double>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            (double x, double y, double z) = (r.Next(), r.Next(), r.Next());

            Assert.AreEqual((x + y) * z, f(x, y, z));
        }

    }



    /// <summary>
    /// Ensures arithmetic precedence remains consistent.
    /// </summary>
    [TestMethod]
    public void Compile_ArithmeticExpression_RespectsPrecedence()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var expression = compiler.Compile("(10 + 2) * 3 - 6 / 2");
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();

        Assert.AreEqual(33, lambda());
    }
}
