using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions
{
    [TestClass]
    public class ExpressionSimplifierTests
    {
        [TestMethod]
        public void SimpleExpressionTest1()
        {
            (Expression<Func<double, double>> Expression, Expression<Func<double, double>> Expected)[] Test = [
                (x => double.Pow(double.Cos(x), 2) + double.Pow(double.Sin(x), 2), x => 1),
                (x => double.Sin(x) / double.Cos(x), x => double.Tan(x)),
                (x => double.Cos(x) / double.Sin(x), x => 1 / double.Tan(x)),
                (x => double.Cos(x) * double.Tan(x), x => double.Sin(x)),
                (x => double.Tan(x) * double.Cos(x), x => double.Sin(x)),
                (x => double.Sin(x) / double.Tan(x), x => double.Cos(x)),
                (x => 3 * x + 2 * x, x => 5 * x),
                (x => 3 * x + x * 2, x => 5 * x),
                (x => x + 2 * x, x => 3 * x),
                (x => x - 2 * x, x => -x),
                (x => 2 * x - 2 * x, x => 0),
                (x => -2 * x + 2 * x, x => 0)
            ];

            var simplifier = new ExpressionSimplifier();
            foreach (var test in Test)
            {
                var simplified = simplifier.Simplify(test.Expression);
                Assert.AreEqual(test.Expected, simplified, ExpressionComparer.Default);
            }
        }

    }
}
