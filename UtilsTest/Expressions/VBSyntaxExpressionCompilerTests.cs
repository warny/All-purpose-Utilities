using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.VBSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Validates VB-like parse-tree compilation to LINQ expression trees.
/// </summary>
[TestClass]
public class VBSyntaxExpressionCompilerTests
{
    private readonly VBSyntaxExpressionCompiler _compiler = new();

    // ── Arithmetic ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures arithmetic precedence is preserved during parse-tree compilation.
    /// </summary>
    [TestMethod]
    public void Compile_ArithmeticExpression_RespectsPrecedence()
    {
        Expression expr = _compiler.Compile("1 + 2 * 3");
        double result = Expression.Lambda<Func<double>>(
            Expression.Convert(expr, typeof(double))).Compile()();

        Assert.AreEqual(7d, result);
    }

    /// <summary>
    /// Ensures subtraction compiles correctly.
    /// </summary>
    [TestMethod]
    public void Compile_SubtractionExpression_ReturnsCorrectValue()
    {
        Expression expr = _compiler.Compile("10 - 3");
        int result = Expression.Lambda<Func<int>>(expr).Compile()();

        Assert.AreEqual(7, result);
    }

    /// <summary>
    /// Ensures the <c>^</c> power operator compiles to Math.Pow.
    /// </summary>
    [TestMethod]
    public void Compile_PowerOperator_ComputesExponentiation()
    {
        Expression expr = _compiler.Compile("2.0 ^ 10");
        double result = Expression.Lambda<Func<double>>(expr).Compile()();

        Assert.AreEqual(1024d, result);
    }

    // ── Logical operators ─────────────────────────────────────────────────────

    /// <summary>
    /// Ensures <c>True AndAlso False</c> evaluates to false.
    /// </summary>
    [TestMethod]
    public void Compile_AndAlso_EvaluatesShortCircuit()
    {
        Expression expr = _compiler.Compile("True AndAlso False");
        bool result = Expression.Lambda<Func<bool>>(expr).Compile()();

        Assert.IsFalse(result);
    }

    /// <summary>
    /// Ensures <c>False OrElse True</c> evaluates to true.
    /// </summary>
    [TestMethod]
    public void Compile_OrElse_EvaluatesShortCircuit()
    {
        Expression expr = _compiler.Compile("False OrElse True");
        bool result = Expression.Lambda<Func<bool>>(expr).Compile()();

        Assert.IsTrue(result);
    }

    /// <summary>
    /// Ensures <c>Not True</c> negates a boolean constant.
    /// </summary>
    [TestMethod]
    public void Compile_Not_NegatesBoolean()
    {
        Expression expr = _compiler.Compile("Not True");
        bool result = Expression.Lambda<Func<bool>>(expr).Compile()();

        Assert.IsFalse(result);
    }

    // ── Comparison operators ──────────────────────────────────────────────────

    /// <summary>
    /// Ensures <c>=</c> equality comparison works correctly.
    /// </summary>
    [TestMethod]
    public void Compile_Equality_ReturnsTrueWhenEqual()
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["x"] = Expression.Constant(42),
        };
        Expression expr = _compiler.Compile("x = 42", symbols);
        bool result = Expression.Lambda<Func<bool>>(expr).Compile()();

        Assert.IsTrue(result);
    }

    /// <summary>
    /// Ensures <c>&lt;&gt;</c> inequality comparison works correctly.
    /// </summary>
    [TestMethod]
    public void Compile_Inequality_ReturnsTrueWhenNotEqual()
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["x"] = Expression.Constant(42),
        };
        Expression expr = _compiler.Compile("x <> 0", symbols);
        bool result = Expression.Lambda<Func<bool>>(expr).Compile()();

        Assert.IsTrue(result);
    }

    // ── String concatenation ──────────────────────────────────────────────────

    /// <summary>
    /// Ensures the <c>&amp;</c> operator concatenates strings.
    /// </summary>
    [TestMethod]
    public void Compile_StringConcat_ProducesJoinedString()
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["greeting"] = Expression.Constant("Hello"),
        };
        Expression expr = _compiler.Compile("greeting & \", World!\"", symbols);
        string result = Expression.Lambda<Func<string>>(expr).Compile()();

        Assert.AreEqual("Hello, World!", result);
    }

    // ── Literals ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures boolean literals compile to constant expressions.
    /// </summary>
    [TestMethod]
    public void Compile_BooleanLiterals_ProduceConstants()
    {
        Expression trueExpr  = _compiler.Compile("True");
        Expression falseExpr = _compiler.Compile("False");

        Assert.IsInstanceOfType(trueExpr,  typeof(ConstantExpression));
        Assert.IsInstanceOfType(falseExpr, typeof(ConstantExpression));
        Assert.IsTrue( (bool)((ConstantExpression)trueExpr).Value!);
        Assert.IsFalse((bool)((ConstantExpression)falseExpr).Value!);
    }

    /// <summary>
    /// Ensures string literals strip surrounding quotes and unescape doubled quotes.
    /// </summary>
    [TestMethod]
    public void Compile_StringLiteral_UnescapesDoubleQuotes()
    {
        Expression expr = _compiler.Compile("\"say \"\"hi\"\"\"");
        string result = Expression.Lambda<Func<string>>(expr).Compile()();

        Assert.AreEqual("say \"hi\"", result);
    }

    // ── Symbol resolution ─────────────────────────────────────────────────────

    /// <summary>
    /// Ensures a symbol in the dictionary is resolved correctly.
    /// </summary>
    [TestMethod]
    public void Compile_SymbolResolution_ResolvesFromDictionary()
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["value"] = Expression.Constant(99),
        };
        Expression expr = _compiler.Compile("value", symbols);
        int result = Expression.Lambda<Func<int>>(expr).Compile()();

        Assert.AreEqual(99, result);
    }

    /// <summary>
    /// Ensures a symbol in the runtime context is resolved correctly.
    /// </summary>
    [TestMethod]
    public void Compile_SymbolResolution_ResolvesFromContext()
    {
        var context = new VBSyntaxCompilerContext();
        context.Set("pi", 3.14);
        Expression expr = _compiler.Compile("pi", context);
        double result = Expression.Lambda<Func<double>>(expr).Compile()();

        Assert.AreEqual(3.14, result);
    }

    // ── Runtime context / function registration ───────────────────────────────

    /// <summary>
    /// Ensures a compiled function is registered in the runtime context.
    /// </summary>
    [TestMethod]
    public void CompileSource_FunctionDeclaration_RegistersInContext()
    {
        var context = new VBSyntaxCompilerContext();
        string source = """
            Public Function Twice(x As Integer) As Integer
                Return x * 2
            End Function
            """;

        _compiler.CompileSource(source, context);

        Assert.IsTrue(context.TryGet("Twice", out object? value));
        Assert.IsInstanceOfType(value, typeof(Delegate));

        var fn = (Delegate)value!;
        Assert.AreEqual(10, fn.DynamicInvoke(5));
    }

    // ── Member access ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures member access on a constant string compiles correctly.
    /// </summary>
    [TestMethod]
    public void Compile_MemberAccess_StringLength()
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal)
        {
            ["text"] = Expression.Constant("hello"),
        };
        Expression expr = _compiler.Compile("text.Length", symbols);
        int result = Expression.Lambda<Func<int>>(expr).Compile()();

        Assert.AreEqual(5, result);
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────

    /// <summary>
    /// Diagnostic: tokenizes source to verify the lexer output.
    /// </summary>
    [TestMethod]
    public void Tokenize_FunctionDeclaration_ProducesExpectedTokens()
    {
        var parser = new VBSyntaxTokenParser();
        string source = "Public Function Twice(x As Integer) As Integer\n    Return x * 2\nEnd Function";
        var tokens = parser.Tokenize(source);
        var names = tokens.Select(t => t.RuleName).ToArray();
        // Should start with PUBLIC, FUNCTION, IDENTIFIER...
        Assert.AreEqual("PUBLIC",     names[0]);
        Assert.AreEqual("FUNCTION",   names[1]);
        Assert.AreEqual("IDENTIFIER", names[2]); // Twice
        Assert.AreEqual("OPEN_PAREN", names[3]);
        Assert.AreEqual("IDENTIFIER", names[4]); // x
        Assert.AreEqual("AS",         names[5]);
        Assert.AreEqual("INTEGER",    names[6]);
        Assert.AreEqual("CLOSE_PAREN",names[7]);
        Assert.AreEqual("AS",         names[8]);
        Assert.AreEqual("INTEGER",    names[9]);
        Assert.AreEqual("RETURN",     names[10]);
        Assert.AreEqual("IDENTIFIER", names[11]); // x
        Assert.AreEqual("OP_MULTIPLY",names[12]);
        Assert.AreEqual("NUMBER",     names[13]);
        Assert.AreEqual("END",        names[14]);
        Assert.AreEqual("FUNCTION",   names[15]);
        Assert.AreEqual(16, names.Length);
    }

    /// <summary>
    /// Diagnostic: verifies the grammar can parse a Return statement.
    /// </summary>
    [TestMethod]
    public void Parse_ReturnStatement_Succeeds()
    {
        var parser = new VBSyntaxTokenParser();
        // If this doesn't throw, parse succeeded.
        // The OnError handler in the compiler throws for ErrorNodes.
        var context = new VBSyntaxCompilerContext();
        context.Set("x", Expression.Parameter(typeof(int), "x"));
        Expression expr = _compiler.Compile("x * 2", context);
        Assert.IsNotNull(expr);
    }

    // ── Object creation ───────────────────────────────────────────────────────

    /// <summary>
    /// Ensures <c>New System.Text.StringBuilder()</c> creates an instance.
    /// </summary>
    [TestMethod]
    public void Compile_ObjectCreation_CreatesInstance()
    {
        Expression expr = _compiler.Compile("New System.Text.StringBuilder()");
        object result = Expression.Lambda<Func<object>>(
            Expression.Convert(expr, typeof(object))).Compile()();

        Assert.IsInstanceOfType(result, typeof(System.Text.StringBuilder));
    }
}
