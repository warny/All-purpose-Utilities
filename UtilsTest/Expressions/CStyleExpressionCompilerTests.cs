using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
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
        Expression expression = compiler.CompileExpression("1 + 2 * 3");
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

        Expression expression = compiler.CompileExpression("x + y", symbols);
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

        Expression expression = compiler.CompileExpression("x - y", symbols);
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

        Expression multiply = compiler.CompileExpression("x * y", symbols);
        Expression divide = compiler.CompileExpression("x / y", symbols);
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
            Expression.Convert(compiler.CompileExpression("x * y + z", symbols), typeof(double)),
            x, y, z).Compile();
        Func<double, double, double, double> priority2 = Expression.Lambda<Func<double, double, double, double>>(
            Expression.Convert(compiler.CompileExpression("x + y * z", symbols), typeof(double)),
            x, y, z).Compile();
        Func<double, double, double, double> parenthesis1 = Expression.Lambda<Func<double, double, double, double>>(
            Expression.Convert(compiler.CompileExpression("x * (y + z)", symbols), typeof(double)),
            x, y, z).Compile();
        Func<double, double, double, double> parenthesis2 = Expression.Lambda<Func<double, double, double, double>>(
            Expression.Convert(compiler.CompileExpression("(x + y) * z", symbols), typeof(double)),
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
        Expression expression = compiler.CompileExpression("x * 2 + 1", new Dictionary<string, Expression>(StringComparer.Ordinal)
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
        Expression assignment = compiler.CompileExpression("value = 10 + 5", new Dictionary<string, Expression>(StringComparer.Ordinal)
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
        Expression expression = compiler.CompileExpression("{ int used = 1; int unused = 2; used }");

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
        compiler.CompileExpression("public double add(double a, double b) { a + b }", context);

        Assert.IsTrue(context.TryGet("add", out object? addSymbol));
        Assert.IsInstanceOfType<Func<double, double, double>>(addSymbol);
        Assert.AreEqual(5d, ((Func<double, double, double>)addSymbol!)(2d, 3d));
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

        Expression invocation = compiler.CompileExpression("add(2, 3)", context);
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

        Expression invocation = compiler.CompileExpression("increment(41)", context);
        Func<double> lambda = Expression.Lambda<Func<double>>(Expression.Convert(invocation, typeof(double))).Compile();
        Assert.AreEqual(42d, lambda());
    }

    [TestMethod]
    public void Compile_WhileInstruction_ProducesTryCatchWrapper()
    {
        var compiler = new CSyntaxExpressionCompiler();
        Expression expression = compiler.CompileExpression("while (true) 1", new ExpressionCompilerContext());
        Assert.AreEqual(ExpressionType.Try, expression.NodeType,
            "while loops are wrapped in a try-catch to support break statements.");
    }

    [TestMethod]
    public void Compile_IfInstruction_WithoutElse_ProducesConditionalExpression()
    {
        var compiler = new CSyntaxExpressionCompiler();
        Expression expression = compiler.CompileExpression("if (true) 1", new ExpressionCompilerContext());
        Assert.IsInstanceOfType<ConditionalExpression>(expression);
    }

    [TestMethod]
    public void Compile_IfInstruction_WithElse_EvaluatesTrueBranch()
    {
        var compiler = new CSyntaxExpressionCompiler();
        Expression expression = compiler.CompileExpression("if (true) 1 else 2", new ExpressionCompilerContext());
        Assert.IsInstanceOfType<ConditionalExpression>(expression);
        Func<int> execute = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();
        Assert.AreEqual(1, execute());
    }

    [TestMethod]
    public void Compile_SwitchInstruction_CompilesToNonDefaultExpression()
    {
        var compiler = new CSyntaxExpressionCompiler();
        Expression expression = compiler.CompileExpression("switch (1) { case 1: 2 default: 3 }", new ExpressionCompilerContext());
        Assert.IsNotNull(expression);
        Assert.AreNotEqual(ExpressionType.Default, expression.NodeType);
    }

    /// <summary>
    /// Ensures a <c>for</c> loop compiles into an expression node through <see cref="ExpressionEx.For"/>.
    /// </summary>
    [TestMethod]
    public void Compile_ForLoop_ExecutesAndAccumulatesCorrectly()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
        ParameterExpression iterator = Expression.Variable(typeof(int), "i");
        ParameterExpression accumulator = Expression.Variable(typeof(int), "sum");
        context.Set("i", iterator);
        context.Set("sum", accumulator);

        Expression loop = compiler.CompileExpression("for (i = 0; i < 4; i = i + 1) sum = sum + i", context);

        var executeBlock = Expression.Block(
            [iterator, accumulator],
            Expression.Assign(accumulator, Expression.Constant(0)),
            loop,
            accumulator);
        Func<int> execute = Expression.Lambda<Func<int>>(Expression.Convert(executeBlock, typeof(int))).Compile();
        Assert.AreEqual(6, execute(), "for (i=0; i<4; i++) sum+=i → 0+1+2+3 = 6");
    }

    /// <summary>
    /// Ensures a <c>foreach</c> loop compiles into an expression node through <see cref="ExpressionEx.ForEach"/>.
    /// </summary>
    [TestMethod]
    public void Compile_ForeachLoop_ExecutesAndAccumulatesCorrectly()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
        ParameterExpression accumulator = Expression.Variable(typeof(int), "sum");
        context.Set("sum", accumulator);
        context.Set("values", new[] { 1, 2, 3, 4 });

        Expression loop = compiler.CompileExpression("foreach (int item in values) sum = sum + item", context);

        var executeBlock = Expression.Block(
            [accumulator],
            Expression.Assign(accumulator, Expression.Constant(0)),
            loop,
            accumulator);
        Func<int> execute = Expression.Lambda<Func<int>>(Expression.Convert(executeBlock, typeof(int))).Compile();
        Assert.AreEqual(10, execute(), "foreach item in [1,2,3,4]: sum += item → 1+2+3+4 = 10");
    }

    /// <summary>
    /// Ensures member and indexer access expressions compile and return the expected values.
    /// </summary>
    /// <param name="source">The source snippet containing member/indexer access.</param>
    /// <param name="expected">The expected integer result when the expression is executed.</param>
    [DataTestMethod]
    [DataRow("sample.Field", 1)]
    [DataRow("sample.Property", 2)]
    [DataRow("sample.Method()", 3)]
    [DataRow("sample[0]", 10)]
    public void Compile_MemberAccess_ReturnsExpectedValue(string source, int expected)
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
        context.Set("sample", new SampleContainer());

        Expression expression = compiler.CompileExpression(source, context);
        Func<int> execute = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();
        Assert.AreEqual(expected, execute());
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
            Expression expression = compiler.CompileExpression("s.Length", symbols);
            Func<int> lambda = Expression.Lambda<Func<int>>(Expression.Convert(expression, typeof(int))).Compile();
            Assert.AreEqual(value.Length, lambda());
        }
    }

    /// <summary>
    /// Ensures explicit lambda-expression syntax compiles to an expression node.
    /// </summary>
    [TestMethod]
    public void Compile_LambdaExpressionSyntax_ProducesInvocableLambda()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();

        Expression expression = compiler.CompileExpression("(double x) => x + 1", context);
        Assert.IsInstanceOfType<LambdaExpression>(expression);
        var lambda = (LambdaExpression)expression;
        Assert.AreEqual(1, lambda.Parameters.Count);
        Func<double, double> compiled = Expression.Lambda<Func<double, double>>(
            Expression.Convert(lambda.Body, typeof(double)), lambda.Parameters).Compile();
        Assert.AreEqual(2d, compiled(1d));
    }

    /// <summary>
    /// Ensures untyped lambda parameters are rewritten with C# aliases from the delegate signature.
    /// </summary>
    [TestMethod]
    public void Compile_GenericLambdaWithUntypedParameters_UsesAliasTypeConversions()
    {
        var compiler = new CSyntaxExpressionCompiler();
        Expression<Func<int, int>> expression = compiler.CompileExpression<Func<int, int>>("(value) => value + 1");
        Func<int, int> function = expression.Compile();

        Assert.AreEqual(42, function(41));
    }

    /// <summary>
    /// Ensures that the initializer of an unused variable is still executed for its side effects.
    /// </summary>
    [TestMethod]
    public void CompileSource_UnusedVariableWithSideEffect_StillExecutesInitializer()
    {
        var compiler = new CSyntaxExpressionCompiler();
        var context = new ExpressionCompilerContext();
        int callCount = 0;
        context.Set("sideEffect", (Func<int>)(() => { callCount++; return 42; }));

        compiler.CompileSource(
            """
            {
                int unused = sideEffect();
                1;
            }
            """,
            context);

        // Force compilation and execution
        var source = """
            {
                int unused = sideEffect();
                1;
            }
            """;
        var expr = compiler.CompileSource(source, context);
        Expression.Lambda<Func<int>>(Expression.Convert(expr, typeof(int))).Compile().Invoke();

        Assert.IsTrue(callCount > 0, "The initializer side effect must still execute even when the variable is unused.");
    }

    /// <summary>
    /// A leading identifier that names a real CLR type must be resolved as a native type so that a
    /// following <c>.Member(...)</c> segment compiles to a static method call (regression coverage
    /// for the <c>TryResolveNativeTypeToken</c> success path).
    /// </summary>
    [TestMethod]
    public void Compile_QualifiedStaticMethodCall_ResolvesNativeTypeForStaticAccess()
    {
        var compiler = new CSyntaxExpressionCompiler();
        Expression expression = compiler.CompileExpression("Math.Abs(-42)");
        double result = Expression.Lambda<Func<double>>(Expression.Convert(expression, typeof(double))).Compile()();

        Assert.AreEqual(42d, result);
    }

    /// <summary>
    /// A leading identifier that is neither a resolvable native type nor a known symbol must fail
    /// with the compiler's own controlled "unable to resolve identifier" error, not with an
    /// unrelated or unhandled exception leaking out of native type resolution (regression coverage
    /// for the <c>TryResolveNativeTypeToken</c> "unknown token" fallback path).
    /// </summary>
    [TestMethod]
    public void Compile_UnknownIdentifier_FallsBackToControlledResolutionError()
    {
        var compiler = new CSyntaxExpressionCompiler();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => compiler.CompileExpression("thisIdentifierIsDefinitelyNotDefinedAnywhere"));

        StringAssert.Contains(exception.Message, "thisIdentifierIsDefinitelyNotDefinedAnywhere");
    }

    /// <summary>
    /// Exercises <c>TryResolveNativeTypeToken</c> directly via reflection (the single production
    /// call site never passes an unresolvable token for a type that also fails to match any known
    /// symbol, so this path is otherwise only reached indirectly). Confirms the CS7095 fix
    /// (removing the always-true <c>when (true)</c> filter from <c>catch (Exception) when (true)</c>)
    /// preserves the pre-existing behavior exactly: any exception raised while resolving an unknown
    /// token as a native type - here <see cref="NotSupportedException"/> - is still swallowed into
    /// <c>null</c>, not just that one exception type.
    /// </summary>
    [TestMethod]
    public void TryResolveNativeTypeToken_UnknownToken_ReturnsNullWithoutThrowing()
    {
        MethodInfo method = typeof(CSyntaxExpressionCompiler).GetMethod(
            "TryResolveNativeTypeToken",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveNativeTypeToken was not found via reflection.");

        object? result = method.Invoke(null, [ "ThisTypeDoesNotExistAnywhere12345", System.Array.Empty<string>() ]);

        Assert.IsNull(result);
    }

    /// <summary>
    /// <see cref="IDelegateCompiler.Compile(string)"/> compiles a bare, non-lambda expression directly
    /// to an executable <see cref="Delegate"/>, skipping the manual <c>Expression.Lambda(...).Compile()</c>
    /// step that <see cref="IExpressionCompiler.CompileExpression(string, IReadOnlyDictionary{string, Expression}?)"/> requires.
    /// </summary>
    [TestMethod]
    public void Compile_SimpleExpression_ReturnsWorkingDelegate()
    {
        var compiler = new CSyntaxExpressionCompiler();

        // Non-generic Compile has no target type to convert to, so the delegate matches the
        // compiled expression's own type (an integer literal expression compiles to int here).
        Delegate compiled = compiler.Compile("1 + 2 * 3");
        var lambda = (Func<int>)compiled;

        Assert.AreEqual(7, lambda());
    }

    /// <summary>
    /// <see cref="IDelegateCompiler.Compile{TDelegate}(string)"/> on an explicit, fully-typed lambda
    /// returns the delegate directly, already compiled.
    /// </summary>
    [TestMethod]
    public void Compile_Generic_ExplicitLambda_ReturnsWorkingDelegate()
    {
        var compiler = new CSyntaxExpressionCompiler();

        Func<int, int> function = compiler.Compile<Func<int, int>>("(int value) => value + 1");

        Assert.AreEqual(42, function(41));
    }

    /// <summary>
    /// The README's recommended one-liner pattern: a bare expression (no lambda syntax) compiled
    /// directly to a parameterless delegate. This requires <see cref="IExpressionCompiler.CompileExpression{TDelegate}(string)"/>
    /// to wrap a non-lambda result in a parameterless lambda when the target delegate has no parameters.
    /// </summary>
    [TestMethod]
    public void Compile_Generic_BareExpressionWithParameterlessDelegate_ReturnsWorkingDelegate()
    {
        var compiler = new CSyntaxExpressionCompiler();

        Func<double> lambda = compiler.Compile<Func<double>>("1 + 2 * 3");

        Assert.AreEqual(7d, lambda());
    }

    /// <summary>
    /// The bare-expression wrapping path must convert the expression's own type to the delegate's
    /// declared return type (here <c>int</c> arithmetic converted to <c>double</c>), not merely wrap it
    /// as-is and let an invalid-cast surface at invocation time.
    /// </summary>
    [TestMethod]
    public void CompileExpression_Generic_BareExpression_ConvertsReturnType()
    {
        var compiler = new CSyntaxExpressionCompiler();

        Expression<Func<double>> expression = compiler.CompileExpression<Func<double>>("2 + 3");

        Assert.AreEqual(typeof(double), expression.Body.Type);
        Assert.AreEqual(5d, expression.Compile()());
    }

    /// <summary>
    /// A bare (non-lambda) expression cannot be compiled against a delegate type that declares
    /// parameters: there is no parameter list in the source to bind names from, so the compiler must
    /// fail explicitly rather than guess or silently ignore the parameters.
    /// </summary>
    [TestMethod]
    public void CompileExpression_Generic_BareExpressionWithParameterizedDelegate_ThrowsExplicitly()
    {
        var compiler = new CSyntaxExpressionCompiler();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => compiler.CompileExpression<Func<int, int>>("1 + 2 * 3"));
    }

    /// <summary>
    /// A <see langword="void"/>-returning delegate (<see cref="Action"/>) must accept a lambda whose body
    /// produces a value: <see cref="Expression.Lambda(Expression, ParameterExpression[])"/> already permits
    /// this (the body's value is simply discarded), so <c>ConvertIfNeeded</c> must not attempt to convert
    /// the body to <see langword="void"/> (which <see cref="Expression.Convert(Expression, Type)"/> does
    /// not support at all).
    /// </summary>
    [TestMethod]
    public void Compile_Generic_ActionDelegateWithValueProducingBody_DoesNotThrow()
    {
        var compiler = new CSyntaxExpressionCompiler();

        Action action = compiler.Compile<Action>("() => 1");

        action();
    }

    /// <summary>
    /// Same as <see cref="Compile_Generic_ActionDelegateWithValueProducingBody_DoesNotThrow"/> but through
    /// <see cref="IExpressionCompiler.CompileExpression{TDelegate}(string)"/> directly.
    /// </summary>
    [TestMethod]
    public void CompileExpression_Generic_ActionDelegateWithValueProducingBody_DoesNotThrow()
    {
        var compiler = new CSyntaxExpressionCompiler();

        Expression<Action> expression = compiler.CompileExpression<Action>("() => 1");

        expression.Compile()();
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
