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

    /// <summary>
    /// Ensures that explicit function declaration syntax is currently rejected by the compiler.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionDeclaration_CurrentlyThrows()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();

        Assert.ThrowsException<InvalidOperationException>(
            () => compiler.Compile("public double add(double a, double b) { a + b }", context));
    }

    /// <summary>
    /// Ensures that invoking another declared function from inside a function body is currently rejected.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionCallingAnotherFunction_ThrowsForUnknownInvokableSymbol()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();

        Assert.ThrowsException<InvalidOperationException>(
            () => compiler.Compile("public double twice(double x) { add(x, x) }", context));
    }

    /// <summary>
    /// Ensures function delegates are resolved when referenced in source.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionDelegateReference_ReturnsDelegateExpression()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        context.Set("add", (Func<double, double, double>)((a, b) => a + b));

        Expression invocation = compiler.Compile("add(2, 3)", context);
        Assert.AreEqual(typeof(Func<double, double, double>), invocation.Type);
    }

    /// <summary>
    /// Ensures that delegate/lambda symbols are resolved when referenced in source.
    /// </summary>
    [TestMethod]
    public void Compile_LambdaSymbolReference_ReturnsDelegateExpression()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        context.Set("increment", (Func<double, double>)(x => x + 1d));

        Expression invocation = compiler.Compile("increment(41)", context);
        Assert.AreEqual(typeof(Func<double, double>), invocation.Type);
    }

    /// <summary>
    /// Ensures core control-flow structures can be parsed into expression nodes.
    /// </summary>
    /// <param name="source">The source snippet containing a control-flow structure.</param>
    [DataTestMethod]
    [DataRow("while (true) 1")]
    [DataRow("if (true) 1")]
    [DataRow("if (true) 1 else 2")]
    [DataRow("switch (1) { case 1: 2 default: 3 }")]
    public void Compile_SupportedControlStructures_ProducesExpressionNode(string source)
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();

        Expression expression = compiler.Compile(source, context);
        Assert.IsNotNull(expression);
    }

    /// <summary>
    /// Ensures unsupported control structures currently fail with a compilation exception.
    /// </summary>
    /// <param name="source">The source snippet containing an unsupported control-flow construct.</param>
    [DataTestMethod]
    [DataRow("for (i = 0; i < 3; i = i + 1) i")]
    [DataRow("foreach (item in values) item")]
    public void Compile_UnsupportedControlStructures_Throws(string source)
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        context.Set("i", Expression.Parameter(typeof(double), "i"));
        context.Set("values", new[] { 1d, 2d, 3d });
        context.Set("item", 0d);

        try
        {
            compiler.Compile(source, context);
            Assert.Fail("Expected compilation to fail for unsupported control structure.");
        }
        catch (Exception)
        {
            // Expected: unsupported construct.
        }
    }

    /// <summary>
    /// Ensures member-access structures that are not yet supported fail with clear exceptions.
    /// </summary>
    /// <param name="source">The source snippet containing member/indexer access.</param>
    [DataTestMethod]
    [DataRow("sample.Field")]
    [DataRow("sample.Property")]
    [DataRow("sample.Method()")]
    public void Compile_UnsupportedMemberAccess_ThrowsInvalidOperation(string source)
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        context.Set("sample", new SampleContainer());

        Assert.ThrowsException<InvalidOperationException>(() => compiler.Compile(source, context));
    }

    /// <summary>
    /// Ensures indexer access currently falls back to the base symbol expression.
    /// </summary>
    [TestMethod]
    public void Compile_IndexerAccess_CurrentlyReturnsBaseSymbolExpression()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        context.Set("sample", new SampleContainer());

        Expression expression = compiler.Compile("sample[0]", context);

        Assert.AreEqual(typeof(SampleContainer), expression.Type);
    }

    /// <summary>
    /// Ensures explicit lambda-expression syntax currently fails compilation.
    /// </summary>
    [TestMethod]
    public void Compile_LambdaExpressionSyntax_Throws()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();

        Assert.ThrowsException<InvalidOperationException>(() => compiler.Compile("x => x + 1", context));
    }

    /// <summary>
    /// Simple test container used for member-access compilation tests.
    /// </summary>
    private sealed class SampleContainer
    {
        /// <summary>
        /// Gets or sets a sample mutable member.
        /// </summary>
        public int Field { get; set; } = 1;

        /// <summary>
        /// Gets a sample property.
        /// </summary>
        public int Property => 2;

        /// <summary>
        /// Returns a sample method value.
        /// </summary>
        /// <returns>A constant numeric value.</returns>
        public int Method() => 3;

        /// <summary>
        /// Gets an indexed value.
        /// </summary>
        /// <param name="index">Index of requested element.</param>
        /// <returns>The indexed value.</returns>
        public int this[int index] => index + 10;
    }

}
