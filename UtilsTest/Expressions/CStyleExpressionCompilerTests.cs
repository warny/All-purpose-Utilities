using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
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
        Func<double> lambda = Expression.Lambda<Func<double>>(Expression.Convert(invocation, typeof(double))).Compile();
        Assert.AreEqual(5d, lambda());
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
        Func<double> lambda = Expression.Lambda<Func<double>>(Expression.Convert(invocation, typeof(double))).Compile();
        Assert.AreEqual(42d, lambda());
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
    /// Ensures a <c>for</c> loop compiles into an expression node through <see cref="ExpressionEx.For"/>.
    /// </summary>
    [TestMethod]
    public void Compile_ForLoop_ProducesExpressionNode()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        ParameterExpression iterator = Expression.Variable(typeof(int), "i");
        ParameterExpression accumulator = Expression.Variable(typeof(int), "sum");
        context.Set("i", iterator);
        context.Set("sum", accumulator);

        Expression loop = compiler.Compile("for (i = 0; i < 4; i = i + 1) sum = sum + i", context);
        Assert.IsNotNull(loop);
    }

    /// <summary>
    /// Ensures a <c>foreach</c> loop compiles into an expression node through <see cref="ExpressionEx.ForEach"/>.
    /// </summary>
    [TestMethod]
    public void Compile_ForeachLoop_ProducesExpressionNode()
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        ParameterExpression accumulator = Expression.Variable(typeof(int), "sum");
        ParameterExpression iterator = Expression.Variable(typeof(int), "item");
        context.Set("sum", accumulator);
        context.Set("item", iterator);
        context.Set("values", new[] { 1, 2, 3, 4 });

        Expression loop = compiler.Compile("foreach (int item in values) sum = sum + item", context);
        Assert.IsNotNull(loop);
    }

    /// <summary>
    /// Ensures member and indexer access structures compile successfully.
    /// </summary>
    /// <param name="source">The source snippet containing member/indexer access.</param>
    [DataTestMethod]
    [DataRow("sample.Field")]
    [DataRow("sample.Property")]
    [DataRow("sample.Method()")]
    [DataRow("sample[0]")]
    public void Compile_MemberAccess_CompilesSuccessfully(string source)
    {
        var compiler = new CStyleExpressionCompiler();
        var context = new CStyleCompilerContext();
        context.Set("sample", new SampleContainer());

        Expression expression = compiler.Compile(source, context);
        Assert.IsNotNull(expression);
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
