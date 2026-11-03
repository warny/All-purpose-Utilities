using System;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC closure audit: characterization of behaviors kept on purpose or still open, so that any change to them is
/// deliberate. Each test states whether it pins a documented compatibility choice, an open defect or a framework limit.
/// </summary>
[TestClass]
[TestCategory("ETC-Audit")]
public class EtcAuditCharacterizationTests
{
    /// <summary>Exposes the protected static <c>CopyExpression</c>.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls <c>CopyExpression</c>.</summary>
        /// <param name="e">The node.</param>
        /// <param name="parameters">The children.</param>
        /// <returns>The copy.</returns>
        public static Expression Copy(Expression e, params Expression[] parameters) => CopyExpression(e, parameters);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>A parameter.</summary>
    private static readonly ParameterExpression P = Expression.Parameter(typeof(double), "p");

    /// <summary>
    /// C-02, historical families (compatibility kept on purpose, documented in the remarks of <c>CopyExpression</c>: "Other
    /// families keep their historical behavior"): binary, unary and conditional copies index their list without an arity
    /// check. A short list throws <see cref="IndexOutOfRangeException"/> (an empty list is not a pass-through, unlike the
    /// families of the arity contract) and extra children are ignored; a constant ignores the list.
    /// </summary>
    [TestMethod]
    public void C02_HistoricalFamilies_KeepTheirUncheckedArity()
    {
        Expression one = Expression.Constant(1.0), two = Expression.Constant(2.0), three = Expression.Constant(3.0);
        Expression add = Expression.Add(P, one);
        Expression negate = Expression.Negate(P);
        Expression condition = Expression.Condition(Expression.Constant(true), P, one);

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => ExposedTransformer.Copy(add));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => ExposedTransformer.Copy(add, two));
        var extraBinary = (BinaryExpression)ExposedTransformer.Copy(add, two, three, one);
        Assert.AreSame(two, extraBinary.Left);
        Assert.AreSame(three, extraBinary.Right);

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => ExposedTransformer.Copy(negate));
        Assert.AreSame(two, ((UnaryExpression)ExposedTransformer.Copy(negate, two, three)).Operand);

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => ExposedTransformer.Copy(condition, Expression.Constant(false), two));
        Assert.AreSame(three, ((ConditionalExpression)ExposedTransformer.Copy(condition, Expression.Constant(false), two, three, one)).IfFalse);

        var constant = (ConstantExpression)ExposedTransformer.Copy(Expression.Constant(5.0), two, three);
        Assert.AreEqual(5.0, constant.Value);
    }

    /// <summary>
    /// C-02, families of the arity contract (R1a–R4), for contrast: an empty list keeps the node and any other wrong length is
    /// refused with <see cref="ArgumentException"/>.
    /// </summary>
    [TestMethod]
    public void C02_ContractFamilies_CheckTheirArity()
    {
        Expression array = Expression.NewArrayInit(typeof(double), P, Expression.Constant(1.0));
        Assert.AreSame(array, ExposedTransformer.Copy(array));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(array, P));
        Assert.ThrowsExactly<ArgumentException>(() => ExposedTransformer.Copy(array, P, P, P));
    }

    /// <summary>
    /// AUD-03 (open, pre-existing since the S5 factoring rules, outside the ETC families): <c>c * (y - 1)</c> and
    /// <c>c * (1 - y)</c> with <c>c</c> other than <c>±1</c> make the multiplicative and subtractive rules rewrite each other
    /// without end. Before ETC R1a-Security the process died of a stack overflow (observed at <c>b5c05dc3</c>); since then the
    /// stack guard turns it into a controlled <see cref="ExpressionResourceLimitKind.Stack"/> refusal, with nothing left
    /// behind. Neighbouring shapes are simplified. When the rule loop is fixed, this test must change.
    /// </summary>
    [TestMethod]
    public void AUD03_RuleLoop_IsAControlledRefusal()
    {
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        var simplifier = new ExpressionSimplifier();
        Expression C(double value) => Expression.Constant(value);

        foreach (Expression body in new Expression[]
        {
            Expression.Multiply(C(10), Expression.Subtract(y, C(1))),
            Expression.Multiply(C(2), Expression.Subtract(y, C(1))),
            Expression.Multiply(C(10), Expression.Subtract(C(1), y)),
        })
        {
            var refusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(Expression.Lambda<Func<double, double>>(body, y)));
            Assert.AreEqual(ExpressionResourceLimitKind.Stack, refusal.Kind);
        }

        foreach (Expression body in new Expression[]
        {
            Expression.Multiply(C(10), Expression.Add(y, C(1))),
            Expression.Multiply(C(10), Expression.Subtract(y, C(2))),
            Expression.Multiply(C(-1), Expression.Subtract(y, C(1))),
        })
        {
            var lambda = Expression.Lambda<Func<double, double>>(body, y);
            var simplified = (Expression<Func<double, double>>)simplifier.Simplify(lambda);
            Assert.AreEqual(lambda.Compile()(3), simplified.Compile()(3));
        }

        // Nothing is left behind by the refusals: the same instance simplifies a valid tree afterwards.
        var small = Expression.Lambda<Func<double, double>>(Expression.Multiply(y, C(1)), y);
        Assert.AreSame(y, ((LambdaExpression)simplifier.Simplify(small)).Body);
    }

    /// <summary>
    /// Framework limit (System.Linq.Expressions, outside the engine): a lambda that redeclares the very parameter instance of
    /// an enclosing lambda and captures it in a closure is mis-compiled (the closure reads another variable); the interpreter
    /// gives the expected result. The audit campaigns skip such trees, whose meaning the framework itself does not fix.
    /// </summary>
    [TestMethod]
    public void Framework_RedeclaredCapturedParameter_IsMisCompiled()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        var inner = Expression.Lambda<Func<double, double>>(Expression.Call(AuditRuntime.CallTwiceMethod, Expression.Lambda<Func<double, double>>(x, z), x), x);
        var outer = Expression.Lambda<Func<double, double>>(Expression.Invoke(inner, Expression.Constant(10.0)), x);

        Assert.AreEqual(15.0, outer.Compile(preferInterpretation: true)(1));
        double compiled = outer.Compile()(1);
        if (compiled == 15.0) Assert.Inconclusive("The framework compiler no longer mis-compiles this shape: the audit skip can be revisited.");
        Assert.AreEqual(0.0, compiled);
    }
}
