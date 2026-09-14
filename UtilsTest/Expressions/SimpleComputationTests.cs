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
        var e = (LambdaExpression)compiler.Compile("(int x, int y) => x + y");
        var f = (Func<int, int, int>)e.Compile();

        foreach ((int x, int y) in new (int, int)[]
        {
            (0, 0), (1, -1), (-42, 17), (123_456, 654_321),
            (int.MaxValue, 1), (int.MinValue, -1), (int.MaxValue, int.MaxValue)
        })
            Assert.AreEqual(unchecked(x + y), f(x, y));

    }

    [TestMethod]
    public void SubstractionTest()
    {
        var e = (LambdaExpression)compiler.Compile("(int x, int y) => x - y");
        var f = (Func<int, int, int>)e.Compile();

        foreach ((int x, int y) in new (int, int)[] { (0, 0), (1, -1), (-42, 17), (654_321, 123_456) })
            Assert.AreEqual(x - y, f(x, y));

    }

    [TestMethod]
    public void MultiplicationTests()
    {
        var e = (LambdaExpression)compiler.Compile("(double x, double y) => x * y");
        var f = (Func<double, double, double>)e.Compile();

        foreach ((double x, double y) in new (double, double)[] { (0, 3.5), (1.25, -4), (-2.5, -8), (123.5, 0.25) })
            Assert.AreEqual(x * y, f(x, y));

    }

    [TestMethod]
    public void DivisionTest()
    {
        var e = (LambdaExpression)compiler.Compile("(double x, double y) => x / y");
        var f = (Func<double, double, double>)e.Compile();

        foreach ((double x, double y) in new (double, double)[] { (0, 3.5), (1.25, -4), (-2.5, -8), (123.5, 0.25) })
            Assert.AreEqual(x / y, f(x, y));

    }

    [TestMethod]
    public void PriorityTest1()
    {
        var e = (LambdaExpression)compiler.Compile("(double x, double y, double z) => x * y + z");
        var f = (Func<double, double, double, double>)e.Compile();

        foreach ((double x, double y, double z) in new (double, double, double)[] { (0, 2, 3), (1.5, -4, 2.25), (-3, -2.5, -7), (100, 0.125, -4) })
            Assert.AreEqual(x * y + z, f(x, y, z));

    }

    [TestMethod]
    public void PriorityTest2()
    {
        var e = (LambdaExpression)compiler.Compile("(double x, double y, double z) => x + y * z");
        var f = (Func<double, double, double, double>)e.Compile();

        foreach ((double x, double y, double z) in new (double, double, double)[] { (0, 2, 3), (1.5, -4, 2.25), (-3, -2.5, -7), (100, 0.125, -4) })
            Assert.AreEqual(x + y * z, f(x, y, z));

    }

    [TestMethod]
    public void ParenthesisTest1()
    {
        var e = (LambdaExpression)compiler.Compile("(double x, double y, double z) => x * (y + z)");
        var f = (Func<double, double, double, double>)e.Compile();

        foreach ((double x, double y, double z) in new (double, double, double)[] { (0, 2, 3), (1.5, -4, 2.25), (-3, -2.5, -7), (100, 0.125, -4) })
            Assert.AreEqual(x * (y + z), f(x, y, z));

    }

    [TestMethod]
    public void ParenthesisTest2()
    {
        var e = (LambdaExpression)compiler.Compile("(double x, double y, double z) => (x + y) * z");
        var f = (Func<double, double, double, double>)e.Compile();

        foreach ((double x, double y, double z) in new (double, double, double)[] { (0, 2, 3), (1.5, -4, 2.25), (-3, -2.5, -7), (100, 0.125, -4) })
            Assert.AreEqual((x + y) * z, f(x, y, z));

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
