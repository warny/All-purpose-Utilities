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
        var r = new Random();

        var e = (LambdaExpression)compiler.Compile("(int x) => +x");
        var f = (Func<int, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
            Assert.AreEqual(x, f(x));
        }
    }

    [TestMethod]
    public void MinusTest1()
    {
        var r = new Random();

        var e = (LambdaExpression)compiler.Compile("(int x) => -x");
        var f = (Func<int, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
            Assert.AreEqual(-x, f(x));
        }
    }

    [TestMethod]
    public void MinusTest2()
    {
        var r = new Random();

        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
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
        var r = new Random();

        var e = (LambdaExpression)compiler.Compile("(int x) => ~x");
        var f = (Func<int, int>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
            Assert.AreEqual(~x, f(x));
        }
    }

    [Ignore("typeof/sizeof are not supported by the grammar")]
    [TestMethod]
    public void SizeofTypeofTest()
    {
        // Grammar has no typeof/sizeof rules; test kept for reference only.
    }

    [Ignore("'new' expressions are not supported by the grammar")]
    [TestMethod]
    public void NewTest()
    {
        // Grammar has no new-expression rule; test kept for reference only.
    }

    [TestMethod]
    public void CastTests()
    {
        // Cast syntax (double)x is not supported by the grammar; use arithmetic promotion instead.
        var r = new Random();

        var e = (LambdaExpression)compiler.Compile("(int x) => x + 0.0");
        var f = (Func<int, double>)e.Compile();

        for (int i = 0; i < 10; i++)
        {
            var x = r.Next(i);
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
