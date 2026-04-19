using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Validates simple arithmetic parsing with <see cref="CStyleExpressionCompiler"/>.
/// </summary>
[TestClass]
public class SimpleExpressionParserTest
{
    CStyleExpressionCompiler compiler = new CStyleExpressionCompiler();
    
    [TestMethod]
    public void ParseSimpleExpressions()
    {
        var parameters = new ParameterExpression[] {
                Expression.Parameter(typeof(double), "x"),
                Expression.Parameter(typeof(double), "y")
            };

        var tests = new (string Expression, Expression Expected)[] {
                ("x+y", (double x, double y) => x + y),
                ("x-y", (double x, double y) => x - y),
                ("x*y", (double x, double y) => x * y),
                ("x/y", (double x, double y) => x / y),
                ("x%y", (double x, double y) => x % y),
                ("x**y", (double x, double y) => Math.Pow(x, y)),
            };

        foreach (var test in tests)
        {
            var result = (LambdaExpression)compiler.Compile<Func<double, double, double>>(test.Expression, parameters);
            Assert.AreEqual(test.Expected, result, ExpressionComparer.Default);
        }
    }

    [TestMethod]
    public void ParseGroupingExpressions()
    {
        var parameters = new ParameterExpression[] {
                Expression.Parameter(typeof(double), "x"),
                Expression.Parameter(typeof(double), "y"),
                Expression.Parameter(typeof(double), "z")
            };

        var tests = new (string Expression, Expression Expected)[] {
                ("x+y+z", (double x, double y, double z) => x + y + z),
                ("x-y*z", (double x, double y, double z) => x - y * z),
                ("(x-y)*z", (double x, double y, double z) => (x - y) * z),
                ("x*y+z", (double x, double y, double z) => x * y + z),
                ("x/(y-z)", (double x, double y, double z) => x / ( y - z )),
            };


        foreach (var test in tests)
        {
            var result = (LambdaExpression)compiler.Compile<Func<double, double, double, double>>(test.Expression, parameters);
            var resultFunc = (Func<double, double, double, double>)result.Compile();
            var expectedFunc = ((Expression<Func<double, double, double, double>>)test.Expected).Compile();

            Assert.AreEqual(expectedFunc(8, 3, 2), resultFunc(8, 3, 2), 1e-9);
            Assert.AreEqual(expectedFunc(1.5, -2, 4), resultFunc(1.5, -2, 4), 1e-9);
        }
    }

    [TestMethod]
    public void ParseFunctionExpressions()
    {
        var parameters = new ParameterExpression[] {
                Expression.Parameter(typeof(double), "x"),
            };

        var tests = new (string Expression, Expression Expected)[] {
                ("Cos(x)", (double x) => Math.Cos(x)),
                ("Sin(x)", (double x) => Math.Sin(x)),
                ("Tan(x)", (double x) => Math.Tan(x)),
            };


        foreach (var test in tests)
        {
            var result = (LambdaExpression)compiler.Compile<Func<double, double>>(test.Expression, parameters, typeof(Math), false);
            Assert.AreEqual(test.Expected, result, ExpressionComparer.Default);
        }
    }

    [TestMethod]
    public void ParseImportedOverloadedFunctionExpressions()
    {
        var parameters = new ParameterExpression[] {
                Expression.Parameter(typeof(double), "x"),
            };

        var result = (LambdaExpression)compiler.Compile<Func<double, double>>("Max(x, 1.5)", parameters, typeof(Math), false);
        var resultFunc = (Func<double, double>)result.Compile();

        Assert.AreEqual(2.2d, resultFunc(2.2d), 1e-9);
        Assert.AreEqual(1.5d, resultFunc(0.3d), 1e-9);
    }


    /// <summary>
    /// Ensures two-variable arithmetic expressions compile and execute.
    /// </summary>
    [TestMethod]
    public void Compile_BinaryExpression_ReturnsExpectedValue()
    {
        var x = Expression.Parameter(typeof(double), "x");
        var y = Expression.Parameter(typeof(double), "y");
        var symbols = new Dictionary<string, Expression> { ["x"] = x, ["y"] = y };
        var expression = compiler.Compile("x + y", symbols);
        var lambda = Expression.Lambda<Func<double, double, double>>(Expression.Convert(expression, typeof(double)), x, y).Compile();

        Assert.AreEqual(7d, lambda(3d, 4d), 1e-9);
    }
}
