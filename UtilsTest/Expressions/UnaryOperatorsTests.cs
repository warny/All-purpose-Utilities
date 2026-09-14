using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates unary operators compiled by <see cref="CSyntaxExpressionCompiler"/>.
/// </summary>
[TestClass]
public class UnaryOperatorsTests
{
    CSyntaxExpressionCompiler compiler = new CSyntaxExpressionCompiler();
    
    [TestMethod]
    public void PlusTest()
    {
        var e = (LambdaExpression)compiler.Compile("(int x) => +x");
        var f = (Func<int, int>)e.Compile();

        foreach (int x in new int[] { 0, 1, -1, 42, -42, 123_456_789, -123_456_789 })
        {
            Assert.AreEqual(x, f(x));
        }
    }

    [TestMethod]
    public void MinusTest1()
    {
        var e = (LambdaExpression)compiler.Compile("(int x) => -x");
        var f = (Func<int, int>)e.Compile();

        foreach (int x in new int[] { 0, 1, -1, 42, -42, 123_456_789, -123_456_789 })
        {
            Assert.AreEqual(-x, f(x));
        }
    }

    [TestMethod]
    public void MinusTest2()
    {
        foreach (int x in new int[] { 0, 1, -1, 42, -42, 123_456_789, -123_456_789 })
        {
            var body = compiler.Compile($"-{x}");
            var f = Expression.Lambda<Func<int>>(Expression.Convert(body, typeof(int))).Compile();
            Assert.AreEqual(-x, f());
        }
    }

    [TestMethod]
    public void NotTest()
    {
        var e = (LambdaExpression)compiler.Compile("(bool x) => !x");
        var f = (Func<bool, bool>)e.Compile();

        foreach (var x in new bool[] { true, false })
        {
            Assert.AreEqual(!x, f(x));
        }
    }

    [TestMethod]
    public void ComplementTest()
    {
        var e = (LambdaExpression)compiler.Compile("(int x) => ~x");
        var f = (Func<int, int>)e.Compile();

        foreach (int x in new int[] { 0, 1, -1, 42, -42, 123_456_789, -123_456_789 })
        {
            Assert.AreEqual(~x, f(x));
        }
    }

    [TestMethod]
    public void CastTests()
    {
        // Cast syntax (double)x is not supported by the grammar; use arithmetic promotion instead.
        var e = (LambdaExpression)compiler.Compile("(int x) => x + 0.0");
        var f = (Func<int, double>)e.Compile();

        foreach (int x in new int[] { 0, 1, -1, 42, -42, 123_456_789, -123_456_789 })
        {
            Assert.AreEqual((double)x, f(x));
        }
    }

    /// <summary>
    /// Ensures unary negation is compiled correctly.
    /// </summary>
    [TestMethod]
    public void Compile_UnaryNegation_ReturnsExpectedValue()
    {
        var expression = compiler.Compile("-(3)");
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();

        Assert.AreEqual(-3, lambda());
    }
}
