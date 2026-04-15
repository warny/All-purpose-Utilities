using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates C-like parse-tree compilation to LINQ expression trees.
/// </summary>
[TestClass]
public class CStyleExpressionCompilerTests
{
    /// <summary>
    /// Ensures arithmetic precedence is preserved during parse-tree compilation.
    /// </summary>
    [TestMethod]
    public void Compile_ArithmeticExpression_RespectsPrecedence()
    {
        var compiler = new CStyleExpressionCompiler();
        Expression expression = compiler.Compile("1 + 2 * 3");
        Func<double> lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

        Assert.AreEqual(7d, lambda());
    }

    /// <summary>
    /// Ensures identifiers are resolved from the provided symbol table.
    /// </summary>
    [TestMethod]
    public void Compile_ExpressionWithIdentifier_UsesSymbolBinding()
    {
        var compiler = new CStyleExpressionCompiler();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression expression = compiler.Compile("x * 2 + 1", new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["x"] = x,
        });

        Func<double, double> lambda = Expression.Lambda<Func<double, double>>(Expression.Convert(expression, typeof(double)), x).Compile();
        Assert.AreEqual(9d, lambda(4d));
    }

    /// <summary>
    /// Ensures assignment instructions can be compiled to assignable expressions.
    /// </summary>
    [TestMethod]
    public void Compile_AssignmentInstruction_ProducesAssignmentExpression()
    {
        var compiler = new CStyleExpressionCompiler();
        ParameterExpression local = Expression.Variable(typeof(double), "value");
        Expression assignment = compiler.Compile("value = 10 + 5", new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["value"] = local,
        });

        Expression block = Expression.Block(
            [local],
            assignment,
            local);
        Func<double> lambda = Expression.Lambda<Func<double>>(block).Compile();

        Assert.AreEqual(15d, lambda());
    }

}
