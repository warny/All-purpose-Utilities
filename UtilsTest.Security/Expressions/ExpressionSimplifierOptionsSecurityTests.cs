using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;
using static UtilsTest.Security.Expressions.ExpressionResourceTestTrees;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// S5 P18 Architecture B: the simplification policy is immutable and frozen at construction (no published mutable alias, no
/// ambient state), and no policy changes the resource guarantees of the simplifier: the same transformation and structural
/// visits, the same refusal exactly one visit below the measured cost, the same validation refusals, the same controlled stack
/// refusal on a small stack, and the same joining of a running operation.
/// </summary>
[TestClass]
public class ExpressionSimplifierOptionsSecurityTests
{
    /// <summary>Upper bound of the worker threads.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A depth far beyond any stack measured in R0.</summary>
    private const int HostileDepth = 100_000;

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
        /// <summary>Initializes a derived simplifier with explicit limits.</summary>
        /// <param name="limits">The limits.</param>
        public DerivedSimplifier(ExpressionResourceLimits limits)
            : base(limits)
        {
        }
    }

    /// <summary>A sample of policies covering every value of each axis, the default first.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.UserCode },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>Simplifiers of one policy, exact and derived.</summary>
    /// <param name="limits">The limits.</param>
    /// <param name="options">The policy.</param>
    /// <returns>The simplifiers.</returns>
    private static ExpressionSimplifier[] Simplifiers(ExpressionResourceLimits limits, ExpressionSimplifierOptions options)
        => [new ExpressionSimplifier(limits) { Options = options }, new DerivedSimplifier(limits) { Options = options }];

    /// <summary>The trees of the budget tests: a sum to factor, a user call chain, a shared DAG, a nested invoked lambda.</summary>
    /// <returns>Named trees.</returns>
    private static IEnumerable<(string Name, Expression Tree)> BudgetTrees()
    {
        yield return ("sum", LeftSum(12).Sum);
        yield return ("coefficients", EqualCoefficientChain(8));
        yield return ("calls", CallChain(30));
        yield return ("dag", SharedDag(8));
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        yield return ("invoked", Expression.Invoke(Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Multiply(y, Expression.Constant(1.0)), P), y), Expression.Add(P, Expression.Constant(0.0))));
    }

    /// <summary>Measures the visits of one simplification.</summary>
    /// <param name="simplifier">The simplifier (it joins the measuring operation).</param>
    /// <param name="tree">The tree.</param>
    /// <returns>The transformation and structural visits.</returns>
    private static (long Transform, long Structural) Measure(ExpressionSimplifier simplifier, Expression tree)
        => ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, (simplifier, tree), static state =>
        {
            long transform = ExpressionResourceBudget.TransformVisits, structural = ExpressionResourceBudget.StructuralVisits;
            state.simplifier.Simplify(state.tree);
            return (ExpressionResourceBudget.TransformVisits - transform, ExpressionResourceBudget.StructuralVisits - structural);
        });

    /// <summary>Whether a setter is an <c>init</c> accessor (its return carries the <c>IsExternalInit</c> modifier, matched by name).</summary>
    /// <param name="setter">The setter.</param>
    /// <returns><see langword="true"/> for an init-only setter.</returns>
    private static bool IsInitOnly(MethodInfo setter)
        => setter.ReturnParameter.GetRequiredCustomModifiers().Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");

    /// <summary>
    /// The options are an immutable record: every property is init-only, the type is sealed, a <c>with</c> copy never changes the
    /// original, and the simplifier exposes its policy through an init-only property and no field.
    /// </summary>
    [TestMethod]
    public void Options_AreImmutableAndFrozenAtConstruction()
    {
        Type type = typeof(ExpressionSimplifierOptions);
        Assert.IsTrue(type.IsSealed);
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            MethodInfo? setter = property.SetMethod;
            if (setter is null) continue;
            Assert.IsTrue(IsInitOnly(setter), $"{property.Name} is init-only");
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Assert.IsTrue(field.IsInitOnly, $"{field.Name} is read-only");
        }

        Assert.IsFalse(type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Any(field => !field.IsInitOnly), "no mutable static state");

        PropertyInfo options = typeof(ExpressionSimplifier).GetProperty(nameof(ExpressionSimplifier.Options))!;
        Assert.IsTrue(IsInitOnly(options.SetMethod!), "the simplifier's policy is init-only");
        Assert.IsFalse(typeof(ExpressionSimplifier).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Any(field => field.FieldType == type && !field.IsInitOnly), "no mutable field holds a policy");

        ExpressionSimplifierOptions original = ExpressionSimplifierOptions.Default;
        ExpressionSimplifierOptions copy = original with { AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None, TrustPolicy = ExpressionTrustPolicy.TrustDeclarations };
        Assert.AreNotSame(original, copy);
        Assert.AreEqual(ExpressionTrustPolicy.Verified, ExpressionSimplifierOptions.Default.TrustPolicy);
        Assert.IsFalse(ExpressionSimplifierOptions.Default.AggressiveSimplification);
        Assert.AreEqual(ExpressionExceptionPreservation.All, ExpressionSimplifierOptions.Default.ExceptionPreservation);

        var simplifier = new ExpressionSimplifier { Options = copy };
        simplifier.Simplify(Expression.Add(P, Expression.Constant(0.0)));
        Assert.AreSame(copy, simplifier.Options);
    }

    /// <summary>
    /// Every policy consumes exactly the visits of the default, and refuses exactly one transformation or structural visit below
    /// the measured cost, for the exact and a derived simplifier.
    /// </summary>
    [TestMethod]
    public void EveryPolicy_HasTheDefaultBudget()
    {
        foreach ((string name, Expression tree) in BudgetTrees())
        {
            for (int kind = 0; kind < 2; kind++)
            {
                (long transform, long structural) = Measure(Simplifiers(ExpressionResourceLimits.Unlimited, ExpressionSimplifierOptions.Default)[kind], tree);
                Assert.IsTrue(transform > 0, name);
                foreach (ExpressionSimplifierOptions options in Policies)
                {
                    string context = $"{name} #{kind} {options}";
                    Assert.AreEqual((transform, structural), Measure(Simplifiers(ExpressionResourceLimits.Unlimited, options)[kind], tree), context);

                    ExpressionResourceLimits exact = ExpressionResourceLimits.Default with { MaxNodes = long.MaxValue, MaxTransformVisits = transform, MaxStructuralVisits = Math.Max(1, structural) };
                    Simplifiers(exact, options)[kind].Simplify(tree);

                    ExpressionResourceLimitException refused = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                        () => Simplifiers(exact with { MaxTransformVisits = transform - 1 }, options)[kind].Simplify(tree), context);
                    Assert.AreEqual(ExpressionResourceLimitKind.Work, refused.Kind, context);

                    if (structural > 1)
                    {
                        ExpressionResourceLimitException structuralRefusal = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                            () => Simplifiers(exact with { MaxStructuralVisits = structural - 1 }, options)[kind].Simplify(tree), context);
                        Assert.AreEqual(ExpressionResourceLimitKind.StructuralWork, structuralRefusal.Kind, context);
                    }

                    Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, context);
                }
            }
        }
    }

    /// <summary>The default limits refuse a hostile nesting and a shared DAG before any recursion, under every policy.</summary>
    [TestMethod]
    public void EveryPolicy_KeepsTheInputValidation()
    {
        Expression deep = NegateChain(HostileDepth);
        Expression dag = SharedDag(40);
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            foreach (ExpressionSimplifier simplifier in Simplifiers(ExpressionResourceLimits.Default, options))
            {
                RunOnThread(() =>
                {
                    Assert.AreEqual(ExpressionResourceLimitKind.Depth, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(deep)).Kind, options.ToString());
                    Assert.AreEqual(ExpressionResourceLimitKind.Nodes, Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(dag)).Kind, options.ToString());
                }, SmallStack, Timeout);
            }
        }
    }

    /// <summary>
    /// Without limits, only the stack guard stops a hostile nesting: under every policy the simplification ends with a controlled
    /// <see cref="ExpressionResourceLimitKind.Stack"/> refusal on a 256 KiB stack, closes its operation, and the thread can
    /// simplify again with the default result.
    /// </summary>
    [TestMethod]
    public void EveryPolicy_EndsHostileNestingsWithAStackRefusal()
    {
        Expression deep = CallChain(HostileDepth);
        Expression small = Expression.Add(P, Expression.Constant(0.0));
        string expected = new ExpressionSimplifier().Simplify(small).ToString();
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            foreach (ExpressionSimplifier simplifier in Simplifiers(ExpressionResourceLimits.Unlimited, options))
            {
                RunOnThread(() =>
                {
                    ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => simplifier.Simplify(deep), options.ToString());
                    Assert.AreEqual(ExpressionResourceLimitKind.Stack, exception.Kind, options.ToString());
                    Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, options.ToString());
                    Assert.AreEqual(expected, simplifier.Simplify(small).ToString(), options.ToString());
                }, SmallStack, Timeout);
            }
        }
    }

    /// <summary>A simplification with any policy started inside a running operation joins it and cannot loosen its budget.</summary>
    [TestMethod]
    public void EveryPolicy_JoinsTheRunningOperation()
    {
        (Expression sum, _) = LeftSum(12);
        ExpressionResourceLimits outer = ExpressionResourceLimits.Default with { MaxTransformVisits = 10 };
        foreach (ExpressionSimplifierOptions options in Policies)
        {
            var simplifier = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited) { Options = options };
            ExpressionResourceLimitException exception = Assert.ThrowsExactly<ExpressionResourceLimitException>(
                () => ExpressionResourceBudget.Run(outer, null, null, null, (simplifier, sum), static state => state.simplifier.Simplify(state.sum)), options.ToString());
            Assert.AreEqual(ExpressionResourceLimitKind.Work, exception.Kind, options.ToString());
            Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, options.ToString());
        }
    }
}
