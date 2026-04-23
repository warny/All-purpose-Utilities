using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Verifies standard optimization rewrites for <see cref="ExpressionOptimiser"/>.
/// </summary>
[TestClass]
public class ExpressionOptimiserTests
{
    /// <summary>
    /// Ensures arithmetic identity rewrites are applied.
    /// </summary>
    [TestMethod]
    public void Optimize_AppliesArithmeticIdentities()
    {
        Expression<Func<double, double>> expression = x => (x * 1.0) + 0.0;
        var optimiser = new ExpressionOptimiser();

        var optimized = (Expression<Func<double, double>>)optimiser.Optimize(expression);

        Assert.AreEqual("x => x", optimized.ToString());
    }

    /// <summary>
    /// Ensures boolean short-circuit rewrites simplify constant operands.
    /// </summary>
    [TestMethod]
    public void Optimize_SimplifiesBooleanShortCircuit()
    {
        Expression<Func<bool, bool>> expression = flag => (true && flag) || false;
        var optimiser = new ExpressionOptimiser();

        var optimized = (Expression<Func<bool, bool>>)optimiser.Optimize(expression);

        Assert.AreEqual("flag => flag", optimized.ToString());
    }

    /// <summary>
    /// Ensures constant-conditional rewrites select the correct branch.
    /// </summary>
    [TestMethod]
    public void Optimize_SimplifiesConstantConditionals()
    {
        Expression<Func<int>> expression = () => true ? 2 : 3;
        var optimiser = new ExpressionOptimiser();

        var optimized = (Expression<Func<int>>)optimiser.Optimize(expression);

        Assert.AreEqual(2, optimized.Compile().Invoke());
    }
}
