using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// S5 A-10 positive controls (S-07): the provenance check refuses only reflection objects that are not the runtime's; the real
/// numeric functions, operators, conversions and fields keep their classification and their simplifications, for the exact and a
/// derived simplifier.
/// </summary>
[TestClass]
[TestCategory("S5-A10")]
public class ExpressionReflectionProvenanceControlTests
{
    /// <summary>A free parameter of type <see cref="double"/>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>A free parameter of type <see cref="float"/>.</summary>
    private static readonly ParameterExpression F = Expression.Parameter(typeof(float), "f");

    /// <summary>A free parameter of type <see cref="decimal"/>.</summary>
    private static readonly ParameterExpression D = Expression.Parameter(typeof(decimal), "d");

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Gets a static method by declaring type, name and parameter types.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="name">The name.</param>
    /// <param name="parameters">The parameter types.</param>
    /// <returns>The method.</returns>
    private static MethodInfo Static(Type type, string name, params Type[] parameters)
        => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, parameters) ?? throw new MissingMethodException(type.Name, name);

    /// <summary>The real numeric functions keep their gate classification and are accepted by the provenance check.</summary>
    [TestMethod]
    public void RealFunctions_KeepTheirClassification()
    {
        MethodInfo[] functions =
        [
            Static(typeof(Math), nameof(Math.Sin), typeof(double)),
            Static(typeof(MathF), nameof(MathF.Sin), typeof(float)),
            Static(typeof(double), nameof(double.Sin), typeof(double)),
            Static(typeof(float), nameof(float.Sin), typeof(float)),
            Static(typeof(Math), nameof(Math.Pow), typeof(double), typeof(double)),
        ];

        foreach (MethodInfo function in functions)
        {
            Assert.IsTrue(ExpressionReflectionProvenance.IsRuntime(function), function.ToString());
            Assert.AreEqual(ExpressionEffects.None, ExpressionEffectAnalysis.MethodEffects(function), function.ToString());

            // Pow is absent from the P18-A trusted list (a conservative omission recorded in the A-10 note, out of its scope).
            if (function.Name != nameof(Math.Pow)) Assert.AreEqual(ExpressionBehaviors.None, ExpressionBehaviorAnalysis.AnalyzeMethod(function).Flags, function.ToString());
        }

        Assert.AreEqual(ExpressionEffects.MayThrow, ExpressionEffectAnalysis.MethodEffects(Static(typeof(Math), nameof(Math.Sign), typeof(double))));
    }

    /// <summary>The real functions, operators and fields are simplified exactly as before, by the exact and a derived simplifier.</summary>
    [TestMethod]
    public void RealMembers_AreStillSimplified()
    {
        MethodInfo mathSin = Static(typeof(Math), nameof(Math.Sin), typeof(double));
        MethodInfo doubleSin = Static(typeof(double), nameof(double.Sin), typeof(double));
        MethodInfo doubleCos = Static(typeof(double), nameof(double.Cos), typeof(double));
        MethodInfo mathFSin = Static(typeof(MathF), nameof(MathF.Sin), typeof(float));

        foreach (ExpressionSimplifier simplifier in new ExpressionSimplifier[] { new(), new DerivedSimplifier() })
        {
            string name = simplifier.GetType().Name;
            Assert.AreEqual(0.0, (simplifier.Simplify(Expression.Multiply(Expression.Call(mathSin, X), Expression.Constant(0.0))) as ConstantExpression)?.Value, name);
            Assert.AreEqual(0.0, (simplifier.Simplify(Expression.Subtract(Expression.Call(doubleSin, X), Expression.Call(doubleSin, X))) as ConstantExpression)?.Value, name);
            Assert.AreEqual(0f, (simplifier.Simplify(Expression.Multiply(Expression.Call(mathFSin, F), Expression.Constant(0f))) as ConstantExpression)?.Value, name);
            Assert.AreEqual(ExpressionType.Power, simplifier.Simplify(Expression.Call(Static(typeof(Math), nameof(Math.Pow), typeof(double), typeof(double)), X, Expression.Constant(3.0))).NodeType, name);
            Assert.AreEqual(1.0, (simplifier.Simplify(Expression.Add(
                Expression.Power(Expression.Call(doubleSin, X), Expression.Constant(2.0)),
                Expression.Power(Expression.Call(doubleCos, X), Expression.Constant(2.0)))) as ConstantExpression)?.Value, name);
            Assert.AreSame(D, simplifier.Simplify(Expression.Add(D, Expression.Constant(0m))), name);
        }

        Assert.IsTrue(new ExpressionCallSignatureAttribute(typeof(Math), nameof(Math.Sin)).Match(Expression.Call(mathSin, X)));
        Assert.IsTrue(ExpressionSimplifier.IsOrdinaryBinaryArithmetic(Expression.Add(D, Expression.Constant(0m))));
        Assert.IsTrue(ExpressionSimplifier.IsOrdinaryUnaryNegate(Expression.Negate(D)));
        Assert.IsTrue(ExpressionSimplifier.IsOrdinaryBinaryArithmetic(Expression.Multiply(X, X)));
    }

    /// <summary>The real conversions and fields keep their gate classification.</summary>
    [TestMethod]
    public void RealConversionsAndFields_KeepTheirClassification()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        Assert.AreEqual(ExpressionEffects.ReadsMutableState, ExpressionEffectAnalysis.Analyze(Expression.Convert(i, typeof(decimal))));
        Assert.AreEqual(ExpressionEffects.ReadsMutableState | ExpressionEffects.MayThrow, ExpressionEffectAnalysis.Analyze(Expression.Convert(D, typeof(int))));
        Assert.AreEqual(ExpressionEffects.None, ExpressionEffectAnalysis.Analyze(Expression.Field(null, typeof(Math), nameof(Math.PI))));
        Assert.AreEqual(ExpressionEffects.None, ExpressionEffectAnalysis.Analyze(Expression.Field(null, typeof(string), nameof(string.Empty))));
        Assert.AreEqual(ExpressionEffects.ReadsMutableState, ExpressionEffectAnalysis.Analyze(Expression.Field(null, typeof(BehaviorCell), nameof(BehaviorCell.StaticMutable))));
    }
}
