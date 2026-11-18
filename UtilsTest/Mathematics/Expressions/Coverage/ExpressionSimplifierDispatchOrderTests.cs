using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// S5A-11 (mission "S5 residual correctness"): <c>ExpressionTransformer.BuildPlan</c> tries the rules in the order
/// <see cref="Type.GetMethods(BindingFlags)"/> returns them, and the first rule that returns a result wins, so the order is part
/// of the simplifier's behavior. That order is the metadata order of the compiled type: the declaration order within a file
/// and, for the partial files of <see cref="ExpressionSimplifier"/>, the order in which the compiler receives them. Neither .NET
/// nor MSBuild documents those two orders, so these tests pin the plan of the built-in simplifier as the compatibility baseline
/// (they run in the Windows and Ubuntu jobs and in the Debug expression-engine job) together with the priorities known to change
/// results, so that a platform, a build or a refactoring that reorders the rules fails here instead of silently changing a
/// simplification.
/// </summary>
[TestClass]
[TestCategory("S5-Audit")]
public class ExpressionSimplifierDispatchOrderTests
{
    /// <summary>The binding flags of <c>BuildPlan</c>.</summary>
    private const BindingFlags PlanFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod | BindingFlags.Instance;

    /// <summary>The parameter <c>x</c>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>
    /// The signature rules of the built-in simplifier in plan order (<see cref="ExpressionTransformRuleInfo.Id"/> without the
    /// namespace of the declaring type), recorded on <c>master</c> <c>c509bdf6</c>.
    /// </summary>
    private static readonly string[] ExpectedPlan =
    [
        "ExpressionSimplifier.AdditionWithZero(BinaryExpression, Expression, ConstantExpression)",
        "ExpressionSimplifier.AdditionWithZero(BinaryExpression, ConstantExpression, Expression)",
        "ExpressionSimplifier.SubstractionWithZero(BinaryExpression, Expression, ConstantExpression)",
        "ExpressionSimplifier.SubstractionWithZero(BinaryExpression, ConstantExpression, Expression)",
        "ExpressionSimplifier.MultiplicationWithZeroOrOne(BinaryExpression, Expression, ConstantExpression)",
        "ExpressionSimplifier.MultiplicationWithZeroOrOne(BinaryExpression, ConstantExpression, Expression)",
        "ExpressionSimplifier.DivideWithZeroOrOne(BinaryExpression, Expression, ConstantExpression)",
        "ExpressionSimplifier.DivideWithZero(BinaryExpression, ConstantExpression, Expression)",
        "ExpressionSimplifier.PowerOfZeroOrOne(BinaryExpression, ConstantExpression, Expression)",
        "ExpressionSimplifier.PowerByZeroOrOne(BinaryExpression, Expression, ConstantExpression)",
        "ExpressionSimplifier.AdditionOfConstants(BinaryExpression, ConstantExpression, ConstantExpression)",
        "ExpressionSimplifier.AdditionWithNegate(BinaryExpression, Expression, UnaryExpression)",
        "ExpressionSimplifier.AdditionWithNegate(BinaryExpression, UnaryExpression, Expression)",
        "ExpressionSimplifier.SubstractionWithNegate(BinaryExpression, Expression, UnaryExpression)",
        "ExpressionSimplifier.SubstractionWithNegate(BinaryExpression, UnaryExpression, Expression)",
        "ExpressionSimplifier.NegateWithSubstraction(UnaryExpression, BinaryExpression)",
        "ExpressionSimplifier.SubstractionWithAddition(BinaryExpression, Expression, BinaryExpression)",
        "ExpressionSimplifier.SubstractionWithSubstraction(BinaryExpression, Expression, BinaryExpression)",
        "ExpressionSimplifier.SubstractionOfConstants(BinaryExpression, ConstantExpression, ConstantExpression)",
        "ExpressionSimplifier.AdditionOfEqualsElements(BinaryExpression, Expression, Expression)",
        "ExpressionSimplifier.SubstractionOfEqualsElements(BinaryExpression, Expression, Expression)",
        "ExpressionSimplifier.MultiplicationOfConstants(BinaryExpression, ConstantExpression, ConstantExpression)",
        "ExpressionSimplifier.Multiplication(BinaryExpression, Expression, ConstantExpression)",
        "ExpressionSimplifier.Multiplication(BinaryExpression, ConstantExpression, BinaryExpression)",
        "ExpressionSimplifier.Multiplication(BinaryExpression, BinaryExpression, BinaryExpression)",
        "ExpressionSimplifier.DivisionOfConstants(BinaryExpression, ConstantExpression, ConstantExpression)",
        "ExpressionSimplifier.MultiplicationWithNegate(BinaryExpression, Expression, UnaryExpression)",
        "ExpressionSimplifier.MultiplicationWithNegate(BinaryExpression, UnaryExpression, Expression)",
        "ExpressionSimplifier.DivisionWithNegate(BinaryExpression, Expression, UnaryExpression)",
        "ExpressionSimplifier.DivisionWithNegate(BinaryExpression, UnaryExpression, Expression)",
        "ExpressionSimplifier.MultiplicationOfEqualsElements(BinaryExpression, BinaryExpression, Expression)",
        "ExpressionSimplifier.MultiplicationOfEqualsElements(BinaryExpression, Expression, BinaryExpression)",
        "ExpressionSimplifier.MultiplicationOfEqualsElements(BinaryExpression, Expression, Expression)",
        "ExpressionSimplifier.DivisionOfDivision(BinaryExpression, BinaryExpression, BinaryExpression)",
        "ExpressionSimplifier.DivisionOfDivision(BinaryExpression, Expression, BinaryExpression)",
        "ExpressionSimplifier.DivisionOfDivision(BinaryExpression, BinaryExpression, Expression)",
        "ExpressionSimplifier.PowerOfConstants(BinaryExpression, ConstantExpression, ConstantExpression)",
        "ExpressionSimplifier.InvokeExpression(InvocationExpression)",
        "ExpressionSimplifier.PowerConvertionNumber1(Expression, Expression, Expression)",
        "ExpressionSimplifier.PowerConvertionNumber2(Expression, Expression, Expression)",
        "ExpressionSimplifier.LogarithmSimplificationAddNumber(Expression, MethodCallExpression, MethodCallExpression)",
        "ExpressionSimplifier.LogarithmSimplificationSubstractNumber(Expression, MethodCallExpression, MethodCallExpression)",
        "ExpressionSimplifier.Logarithm10SimplificationAddNumber(Expression, MethodCallExpression, MethodCallExpression)",
        "ExpressionSimplifier.Logarithm10SimplificationSubstractNumber(Expression, MethodCallExpression, MethodCallExpression)",
        "ExpressionSimplifier.AdditionOfCos2andSin2Number(Expression, BinaryExpression, BinaryExpression)",
        "ExpressionSimplifier.DivisionOfCosAndSinNumber(Expression, MethodCallExpression, MethodCallExpression)",
        "ExpressionSimplifier.DivisionOfSinAndCosNumber(Expression, MethodCallExpression, MethodCallExpression)",
        "ExpressionSimplifier.MultiplicationOfCosAndTanNumber(Expression, MethodCallExpression, MethodCallExpression)",
        "ExpressionSimplifier.MultiplicationOfTanAndCosNumber(Expression, MethodCallExpression, MethodCallExpression)",
        "ExpressionSimplifier.DivisionOfSinAndTanNumber(Expression, MethodCallExpression, MethodCallExpression)",
        "ExpressionSimplifier.PowerConversionMath(Expression, Expression, Expression)",
        "ExpressionSimplifier.SignConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.MinConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.MaxConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.ClampConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.AbsConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.RoundConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.FloorConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.CeilingConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.SqrtConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.CbrtConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.LogConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.Log2ConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.Log10ConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.CosConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.SinConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.TanConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.ACosConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.ASinConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.ATanConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.CoshConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.SinhConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.TanhConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.ACoshConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.ASinhConversionMath(Expression, Expression[])",
        "ExpressionSimplifier.ATanhConversionMath(Expression, Expression[])",
    ];

    /// <summary>Returns the signature rules of <paramref name="transformerType"/> exactly as <c>BuildPlan</c> enumerates them.</summary>
    /// <param name="transformerType">The transformer type.</param>
    /// <returns>The rule methods, in plan order.</returns>
    private static MethodInfo[] PlanRules(Type transformerType)
        => transformerType.GetMethods(PlanFlags)
            .Where(static method => method.GetCustomAttributes<ExpressionSignatureAttribute>().Any() && !method.IsDefined(typeof(FlattenAttribute), inherit: true))
            .ToArray();

    /// <summary>Formats a rule as its <see cref="ExpressionTransformRuleInfo.Id"/>, with the short name of its declaring type.</summary>
    /// <param name="method">The rule method.</param>
    /// <returns>The identifier.</returns>
    private static string Id(MethodInfo method)
        => method.DeclaringType!.Name + "." + method.Name + "(" + string.Join(", ", method.GetParameters().Select(static parameter => parameter.ParameterType.Name)) + ")";

    /// <summary>The plan of the built-in simplifier is the recorded baseline, rule for rule.</summary>
    [TestMethod]
    public void BuiltInPlan_IsTheRecordedBaseline()
    {
        string[] actual = PlanRules(typeof(ExpressionSimplifier)).Select(Id).ToArray();
        Assert.AreEqual(string.Join(Environment.NewLine, ExpectedPlan), string.Join(Environment.NewLine, actual));
    }

    /// <summary>
    /// The enumeration of these tests is the plan itself: every rule a traced simplification applies carries, as its
    /// <see cref="ExpressionTransformRuleInfo.DeclarationIndex"/>, its position in <see cref="PlanRules"/>.
    /// </summary>
    [TestMethod]
    public void PlanRules_MirrorsTheEnginePlan()
    {
        MethodInfo[] rules = PlanRules(typeof(ExpressionSimplifier));
        var tracer = new AppliedRules();
        using (ExpressionTransformTracing.Begin(tracer))
        {
            new ExpressionSimplifier().Simplify(Expression.Add(Expression.Call(Log, X), Expression.Call(Log, X)));
            new ExpressionSimplifier().Simplify(Expression.Multiply(X, Expression.Constant(2.0)));
            new ExpressionSimplifier().Simplify(Expression.Call(Pow, X, Expression.Constant(3.0)));
            new ExpressionSimplifier().Simplify(Expression.Subtract(Expression.Constant(0.0), X));
        }

        // The flatten rules are numbered in their own sequence (ExpressionTransformRuleInfo.DeclarationIndex).
        List<ExpressionTransformRuleInfo> signatureRules = tracer.Rules.Where(static rule => rule.FlattenedNodeTypes.Count == 0).ToList();
        Assert.IsTrue(signatureRules.Count >= 4, signatureRules.Count + " rules applied");
        foreach (ExpressionTransformRuleInfo rule in signatureRules)
        {
            Assert.AreSame(typeof(ExpressionSimplifier), rule.TransformerType, rule.Id);
            Assert.AreEqual(rule.Method, rules[rule.DeclarationIndex], rule.Id);
        }
    }

    /// <summary>
    /// The priorities between rules that match the same node and give different results, including across the partial files
    /// (<c>ExpressionSimplifier.cs</c> before <c>ExpressionSimplifier.INumber.cs</c> before <c>ExpressionSimplifier.Math.cs</c>),
    /// with the result each one decides. Reordering any pair changes the result on the right.
    /// </summary>
    [TestMethod]
    public void Priorities_ThatDecideAResult()
    {
        string[] plan = PlanRules(typeof(ExpressionSimplifier)).Select(Id).ToArray();
        static int IndexOf(string[] plan, string id)
        {
            int index = System.Array.IndexOf(plan, id);
            Assert.IsTrue(index >= 0, id);
            return index;
        }

        // log(x) + log(x): the factoring of equal terms (ExpressionSimplifier.cs) runs before the logarithm combination
        // (ExpressionSimplifier.INumber.cs), so the result is 2 * Log(x), not Log(x * x).
        Assert.IsTrue(IndexOf(plan, "ExpressionSimplifier.AdditionOfEqualsElements(BinaryExpression, Expression, Expression)")
            < IndexOf(plan, "ExpressionSimplifier.LogarithmSimplificationAddNumber(Expression, MethodCallExpression, MethodCallExpression)"));
        Assert.AreEqual("2 * Double.Log(x)", ExpressionTraceFormatter.Format(new ExpressionSimplifier().Simplify(Expression.Add(Expression.Call(Log, X), Expression.Call(Log, X)))));

        // double.Pow: PowerConvertionNumber1 (ExpressionSimplifier.INumber.cs) always applies, so PowerConversionMath
        // (ExpressionSimplifier.Math.cs) is shadowed (the S2 ledger's DUPLICATE_DISPATCH); both build the same power node.
        Assert.IsTrue(IndexOf(plan, "ExpressionSimplifier.PowerConvertionNumber1(Expression, Expression, Expression)")
            < IndexOf(plan, "ExpressionSimplifier.PowerConversionMath(Expression, Expression, Expression)"));
        Assert.AreEqual(ExpressionType.Power, new ExpressionSimplifier().Simplify(Expression.Call(Pow, X, Expression.Constant(3.0))).NodeType);

        // The reassociations run before the cancellation of equal operands, and the commutation of a constant factor before the
        // negation rules (S5A-05): the first-pass results of these shapes depend on it (finished since C3c/C3d).
        Assert.IsTrue(IndexOf(plan, "ExpressionSimplifier.SubstractionWithAddition(BinaryExpression, Expression, BinaryExpression)")
            < IndexOf(plan, "ExpressionSimplifier.SubstractionOfEqualsElements(BinaryExpression, Expression, Expression)"));
        Assert.IsTrue(IndexOf(plan, "ExpressionSimplifier.Multiplication(BinaryExpression, Expression, ConstantExpression)")
            < IndexOf(plan, "ExpressionSimplifier.MultiplicationWithNegate(BinaryExpression, UnaryExpression, Expression)"));

        // The zero and one identities come first in their buckets, before any folding or factoring rule.
        Assert.AreEqual("ExpressionSimplifier.AdditionWithZero(BinaryExpression, Expression, ConstantExpression)", plan[0]);
    }

    /// <summary>
    /// A derived simplifier's plan: the rules it declares come first, in its own declaration order, then the built-in rules in
    /// the recorded order (the order <see cref="Type.GetMethods(BindingFlags)"/> returns for a derived type), so a derived rule
    /// matching the same node as a built-in one wins.
    /// </summary>
    [TestMethod]
    public void DerivedPlan_PutsItsOwnRulesFirst()
    {
        string[] plan = PlanRules(typeof(DerivedSimplifier)).Select(Id).ToArray();
        CollectionAssert.AreEqual(
            new[] { "DerivedSimplifier.First(BinaryExpression, Expression, Expression)", "DerivedSimplifier.Second(BinaryExpression, Expression, Expression)" }.Concat(ExpectedPlan).ToArray(),
            plan);
        Assert.AreEqual("x", ExpressionTraceFormatter.Format(new DerivedSimplifier().Simplify(Expression.Add(X, Expression.Constant(1.0)))));
    }

    /// <summary>The <see cref="double"/> logarithm.</summary>
    private static readonly MethodInfo Log = typeof(double).GetMethod(nameof(double.Log), [typeof(double)])!;

    /// <summary>The <see cref="double"/> power.</summary>
    private static readonly MethodInfo Pow = typeof(double).GetMethod(nameof(double.Pow), [typeof(double), typeof(double)])!;

    /// <summary>A derived simplifier declaring two rules on additions.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
        /// <summary>Replaces any ordinary addition by its left operand (a deliberately wrong rule, to observe the priority).</summary>
        /// <param name="e">The addition.</param>
        /// <param name="left">Its prepared left operand.</param>
        /// <param name="right">Its prepared right operand.</param>
        /// <returns><paramref name="left"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression First(BinaryExpression e, Expression left, Expression right) => left;

        /// <summary>Never reached: <see cref="First"/> always applies.</summary>
        /// <param name="e">The addition.</param>
        /// <param name="left">Its prepared left operand.</param>
        /// <param name="right">Its prepared right operand.</param>
        /// <returns><paramref name="right"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression Second(BinaryExpression e, Expression left, Expression right) => right;
    }

    /// <summary>Collects the rules applied during a traced operation.</summary>
    private sealed class AppliedRules : IExpressionTransformTracer
    {
        /// <summary>Gets the applied rules, in order.</summary>
        public List<ExpressionTransformRuleInfo> Rules { get; } = [];

        /// <inheritdoc/>
        public void OnEvent(in ExpressionTransformEvent traceEvent)
        {
            if (traceEvent.Kind == ExpressionTransformEventKind.RuleApplied && traceEvent.Rule is { } rule) Rules.Add(rule);
        }
    }
}
