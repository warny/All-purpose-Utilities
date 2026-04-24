using System.Linq.Expressions;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Expressions.CSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates C-like parse-tree compilation to LINQ expression trees.
/// </summary>
[TestClass]
public class CSyntaxExpressionCompilerTests
{
    /// <summary>
    /// Ensures arithmetic precedence is preserved during parse-tree compilation.
    /// </summary>
    [TestMethod]
    public void Compile_ArithmeticExpression_RespectsPrecedence()
    {
        var compiler = new CSyntaxExpressionCompiler();
        Expression expression = compiler.Compile("1 + 2 * 3");
        Func<double> lambda = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile();

        Assert.AreEqual(7d, lambda());
    }

    /// <summary>
    /// Duplicates legacy addition coverage from the historical expression compiler tests.
    /// </summary>
    [TestMethod]
    public void Compile_AdditionExpression_MatchesLegacyCompilerBehavior()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var random = new Random(42);
        ParameterExpression x = Expression.Parameter(typeof(int), "x");
        ParameterExpression y = Expression.Parameter(typeof(int), "y");
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["x"] = x,
            ["y"] = y,
        };

        Expression expression = compiler.Compile("x + y", symbols);
        Func<int, int, int> lambda = Expression.Lambda<Func<int, int, int>>(Expression.Convert(expression, typeof(int)), x, y).Compile();

        for (int i = 0; i < 10; i++)
        {
            int left = random.Next(-10_000, 10_001);
            int right = random.Next(-10_000, 10_001);
            Assert.AreEqual(left + right, lambda(left, right));
        }
    }

    /// <summary>
    /// Duplicates legacy subtraction coverage from the historical expression compiler tests.
    /// </summary>
    [TestMethod]
    public void Compile_SubtractionExpression_MatchesLegacyCompilerBehavior()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var random = new Random(84);
        ParameterExpression x = Expression.Parameter(typeof(int), "x");
        ParameterExpression y = Expression.Parameter(typeof(int), "y");
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["x"] = x,
            ["y"] = y,
        };

        Expression expression = compiler.Compile("x - y", symbols);
        Func<int, int, int> lambda = Expression.Lambda<Func<int, int, int>>(Expression.Convert(expression, typeof(int)), x, y).Compile();

        for (int i = 0; i < 10; i++)
        {
            int left = random.Next(-10_000, 10_001);
            int right = random.Next(-10_000, 10_001);
            Assert.AreEqual(left - right, lambda(left, right));
        }
    }

    /// <summary>
    /// Duplicates legacy multiplication/division precedence coverage from the historical expression compiler tests.
    /// </summary>
    [TestMethod]
    public void Compile_MultiplicationAndDivisionExpressions_MatchLegacyCompilerBehavior()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var random = new Random(126);
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["x"] = x,
            ["y"] = y,
        };

        Expression multiply = compiler.Compile("x * y", symbols);
        Expression divide = compiler.Compile("x / y", symbols);
        Func<double, double, double> multiplyLambda = Expression.Lambda<Func<double, double, double>>(Expression.Convert(multiply, typeof(double)), x, y).Compile();
        Func<double, double, double> divideLambda = Expression.Lambda<Func<double, double, double>>(Expression.Convert(divide, typeof(double)), x, y).Compile();

        for (int i = 0; i < 10; i++)
        {
            double left = random.Next(1, 10_000);
            double right = random.Next(1, 10_000);
            Assert.AreEqual(left * right, multiplyLambda(left, right), 1e-9);
            Assert.AreEqual(left / right, divideLambda(left, right), 1e-9);
        }
    }

    /// <summary>
    /// Duplicates legacy precedence and parenthesis coverage from the historical expression compiler tests.
    /// </summary>
    [TestMethod]
    public void Compile_PrecedenceAndParenthesisExpressions_MatchLegacyCompilerBehavior()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var random = new Random(168);
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["x"] = x,
            ["y"] = y,
            ["z"] = z,
        };

        Func<double, double, double, double> priority1 = Expression.Lambda<Func<double, double, double, double>>(
            Expression.Convert(compiler.Compile("x * y + z", symbols), typeof(double)),
            x, y, z).Compile();
        Func<double, double, double, double> priority2 = Expression.Lambda<Func<double, double, double, double>>(
            Expression.Convert(compiler.Compile("x + y * z", symbols), typeof(double)),
            x, y, z).Compile();
        Func<double, double, double, double> parenthesis1 = Expression.Lambda<Func<double, double, double, double>>(
            Expression.Convert(compiler.Compile("x * (y + z)", symbols), typeof(double)),
            x, y, z).Compile();
        Func<double, double, double, double> parenthesis2 = Expression.Lambda<Func<double, double, double, double>>(
            Expression.Convert(compiler.Compile("(x + y) * z", symbols), typeof(double)),
            x, y, z).Compile();

        for (int i = 0; i < 10; i++)
        {
            double left = random.Next(1, 1_000);
            double mid = random.Next(1, 1_000);
            double right = random.Next(1, 1_000);

            Assert.AreEqual(left * mid + right, priority1(left, mid, right), 1e-9);
            Assert.AreEqual(left + mid * right, priority2(left, mid, right), 1e-9);
            Assert.AreEqual(left * (mid + right), parenthesis1(left, mid, right), 1e-9);
            Assert.AreEqual((left + mid) * right, parenthesis2(left, mid, right), 1e-9);
        }
    }

    /// <summary>
    /// Ensures identifiers are resolved from the provided symbol table.
    /// </summary>
    [TestMethod]
    public void Compile_ExpressionWithIdentifier_UsesSymbolBinding()
    {
        var compiler = new CSyntaxExpressionCompiler();
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
        var compiler = new CSyntaxExpressionCompiler();
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
    /// Ensures unused local declarations are ignored when building block variables.
    /// </summary>
    [TestMethod]
    public void Compile_BlockWithUnusedDeclaration_IgnoresUnusedVariable()
    {
        var compiler = new CSyntaxExpressionCompiler();
        Expression expression = compiler.Compile("{ int used = 1; int unused = 2; used }");

        Assert.IsInstanceOfType<BlockExpression>(expression);
        var block = (BlockExpression)expression;
        Assert.IsFalse(block.Variables.Any(variable => variable.Name == "unused"));

        Func<int> lambda = Expression.Lambda<Func<int>>(Expression.Convert(block, typeof(int))).Compile();
        Assert.AreEqual(1, lambda());
    }

    /// <summary>
    /// Ensures that explicit function declaration syntax can be compiled.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionDeclaration_Compiles()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
        Expression declaration = compiler.Compile("public double add(double a, double b) { a + b }", context);
        Assert.IsNotNull(declaration);
    }

    /// <summary>
    /// Ensures that declared methods can reference methods declared later in source order.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionCallingAnotherFunction_ResolvesForwardReference()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
        compiler.CompileSource(
            """
            public double twice(double x) { add(x, x) }
            public double add(double x, double y) { x + y; }
            """,
            context);

        Assert.IsTrue(context.TryGet("twice", out object? twiceSymbol));
        Assert.IsInstanceOfType<Func<double, double>>(twiceSymbol);
        double value = ((Func<double, double>)twiceSymbol)(3d);
        Assert.AreEqual(6d, value);
    }

    /// <summary>
    /// Ensures function delegates are resolved when referenced in source.
    /// </summary>
    [TestMethod]
    public void Compile_FunctionDelegateReference_ReturnsDelegateExpression()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
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
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
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
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();

        Expression expression = compiler.Compile(source, context);
        Assert.IsNotNull(expression);
    }

    /// <summary>
    /// Ensures a <c>for</c> loop compiles into an expression node through <see cref="ExpressionEx.For"/>.
    /// </summary>
    [TestMethod]
    public void Compile_ForLoop_ProducesExpressionNode()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
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
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
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
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
        context.Set("sample", new SampleContainer());

        Expression expression = compiler.Compile(source, context);
        Assert.IsNotNull(expression);
    }

    /// <summary>
    /// Duplicates legacy member-access coverage from the historical expression compiler tests.
    /// </summary>
    [TestMethod]
    public void Compile_MemberLengthExpression_MatchesLegacyCompilerBehavior()
    {
        var compiler = new CSyntaxExpressionCompiler();
        string[] values = ["a", "ab", "abc"];

        foreach (string value in values)
        {
            var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
            {
                ["s"] = Expression.Constant(value),
            };
            Expression expression = compiler.Compile("s.Length", symbols);
            Func<int> lambda = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();
            Assert.AreEqual(value.Length, lambda());
        }
    }

    /// <summary>
    /// Ensures explicit lambda-expression syntax compiles to an expression node.
    /// </summary>
    [TestMethod]
    public void Compile_LambdaExpressionSyntax_Compiles()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();

        Expression expression = compiler.Compile("x => x + 1", context);
        Assert.IsNotNull(expression);
    }

    /// <summary>
    /// Ensures untyped lambda parameters are rewritten with C# aliases from the delegate signature.
    /// </summary>
    [TestMethod]
    public void Compile_GenericLambdaWithUntypedParameters_UsesAliasTypeConversions()
    {
        var compiler = new CSyntaxExpressionCompiler();
        Expression<Func<int, int>> expression = compiler.Compile<Func<int, int>>("(value) => value + 1");
        Func<int, int> function = expression.Compile();

        Assert.AreEqual(42, function(41));
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
