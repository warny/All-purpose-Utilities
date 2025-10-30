using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions
{
    [TestClass]
    public class ExpressionIntegrationTests
    {
        readonly ExpressionIntegration integration = new ExpressionIntegration("x");
        readonly ExpressionSimplifier simplifier = new ExpressionSimplifier();

        [TestMethod]
        public void ExpressionsIntegration()
        {
            var parameters = new ParameterExpression[]
            {
                Expression.Parameter(typeof(double), "x"),
            };

            var tests = new (string function, string integral)[]
            {
                ("1/x", "Log(x)"),
                ("1/(x**2)", "-(1/x)"),
                ("1/Sqrt(x)", "2*Sqrt(x)"),
                ("Sinh(x)", "Cosh(x)"),
                ("Cosh(x)", "Sinh(x)"),
                ("Tanh(x)", "Log(Cosh(x))"),
            };

            foreach (var test in tests)
            {
                var func = ExpressionParser.Parse(test.function, parameters, typeof(double), false);
                var expected = ExpressionParser.Parse(test.integral, parameters, typeof(double), false);
                var result = simplifier.Simplify(integration.Integrate(func));
                Assert.AreEqual(expected, result, ExpressionComparer.Default);
            }
        }
    }
}
