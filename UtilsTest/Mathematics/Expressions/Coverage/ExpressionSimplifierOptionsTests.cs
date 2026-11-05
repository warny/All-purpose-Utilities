using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// S5 P18 Architecture B: the public simplification policy (<see cref="ExpressionSimplifierOptions"/>), its defaults and
/// validation, the behavior permissions under the three axes (matrix over every combination of <see cref="ExpressionBehaviors"/>
/// flags, monotonicity, imperative obstacles). Since Architecture C the policies are consumed (see
/// <see cref="ExpressionSimplifierPolicyRulesTests"/> for the rules): a policy equal to the default gives exactly the historical
/// results, user calls, evaluation order and exceptions, and every other policy keeps every guarantee it does not relax (no
/// exception added, traced effects in the same order, user code during simplification unchanged), for the exact and a derived
/// simplifier, sequentially, concurrently and re-entrantly.
/// </summary>
[TestClass]
[TestCategory("S5-P18B")]
public class ExpressionSimplifierOptionsTests
{
    /// <summary>A free parameter of type <see cref="double"/>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>A free parameter of type <see cref="int"/>.</summary>
    private static readonly ParameterExpression I = Expression.Parameter(typeof(int), "i");

    /// <summary>The nine flags a behavior is made of (<see cref="ExpressionBehaviors.MayThrow"/> and <see cref="ExpressionBehaviors.UnknownCode"/> are unions).</summary>
    private static readonly ExpressionBehaviors[] SingleFlags =
    [
        ExpressionBehaviors.ReadsMutableState, ExpressionBehaviors.Nondeterministic, ExpressionBehaviors.SideEffects,
        ExpressionBehaviors.Allocates, ExpressionBehaviors.MayThrowArithmetic, ExpressionBehaviors.MayThrowRuntime,
        ExpressionBehaviors.MayThrowUserCode, ExpressionBehaviors.Unknown, ExpressionBehaviors.ReliesOnDeclaration,
    ];

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
        /// <summary>Initializes a derived simplifier with the default limits.</summary>
        public DerivedSimplifier()
        {
        }

        /// <summary>Initializes a derived simplifier with explicit limits.</summary>
        /// <param name="limits">The limits.</param>
        public DerivedSimplifier(ExpressionResourceLimits limits)
            : base(limits)
        {
        }
    }

    /// <summary>Every combination of the three axes: 2 trust policies, 2 aggressiveness values, 8 exception sets.</summary>
    /// <returns>The 32 policies, the default first.</returns>
    internal static IEnumerable<ExpressionSimplifierOptions> AllPolicies()
    {
        yield return ExpressionSimplifierOptions.Default;
        foreach (ExpressionTrustPolicy trust in new[] { ExpressionTrustPolicy.Verified, ExpressionTrustPolicy.TrustDeclarations })
        {
            foreach (bool aggressive in new[] { false, true })
            {
                for (int preservation = 0; preservation <= (int)ExpressionExceptionPreservation.All; preservation++)
                {
                    var options = new ExpressionSimplifierOptions
                    {
                        TrustPolicy = trust,
                        AggressiveSimplification = aggressive,
                        ExceptionPreservation = (ExpressionExceptionPreservation)preservation,
                    };
                    if (options != ExpressionSimplifierOptions.Default) yield return options;
                }
            }
        }
    }

    /// <summary>Every raw combination of the nine behavior flags, consistent with the analysis or not.</summary>
    /// <returns>The 512 behaviors.</returns>
    private static IEnumerable<ExpressionBehavior> AllBehaviors()
    {
        for (int mask = 0; mask < 1 << SingleFlags.Length; mask++)
        {
            ExpressionBehaviors flags = ExpressionBehaviors.None;
            for (int bit = 0; bit < SingleFlags.Length; bit++)
            {
                if ((mask & (1 << bit)) != 0) flags |= SingleFlags[bit];
            }

            yield return new ExpressionBehavior(flags);
        }
    }

    /// <summary>The three permissions of a behavior under a policy.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="options">The policy.</param>
    /// <returns>Discard, repeat and reorder.</returns>
    private static (bool Discard, bool Repeat, bool Reorder) Permissions(ExpressionBehavior behavior, ExpressionSimplifierOptions options)
        => (ExpressionBehaviorPermissions.CanDiscard(behavior, options), ExpressionBehaviorPermissions.CanRepeat(behavior, options),
            ExpressionBehaviorPermissions.CanReorder(behavior, options));

    /// <summary>Whether every permission of <paramref name="weaker"/> is also granted by <paramref name="stronger"/>.</summary>
    /// <param name="weaker">The permissions expected to be fewer.</param>
    /// <param name="stronger">The permissions expected to be more.</param>
    /// <returns><see langword="true"/> when <paramref name="weaker"/> implies <paramref name="stronger"/>.</returns>
    private static bool Implies((bool Discard, bool Repeat, bool Reorder) weaker, (bool Discard, bool Repeat, bool Reorder) stronger)
        => (!weaker.Discard || stronger.Discard) && (!weaker.Repeat || stronger.Repeat) && (!weaker.Reorder || stronger.Reorder);

    // ---------------------------------------------------------------- defaults and validation

    /// <summary>The defaults are conservative, and every existing constructor, exact or derived, starts from them.</summary>
    [TestMethod]
    public void Defaults_AreConservative()
    {
        ExpressionSimplifierOptions defaults = ExpressionSimplifierOptions.Default;
        Assert.AreEqual(ExpressionTrustPolicy.Verified, defaults.TrustPolicy);
        Assert.IsFalse(defaults.AggressiveSimplification);
        Assert.AreEqual(ExpressionExceptionPreservation.All, defaults.ExceptionPreservation);
        Assert.AreEqual(defaults, new ExpressionSimplifierOptions());


        ExpressionSimplifier[] simplifiers =
        [
            new ExpressionSimplifier(),
            new ExpressionSimplifier(ExpressionResourceLimits.Default),
            new ExpressionSimplifier(ExpressionResourceLimits.Unlimited),
            new ExpressionSimplifier { SimplifyQuotedExpressions = true },
            new DerivedSimplifier(),
            new DerivedSimplifier(ExpressionResourceLimits.Unlimited),
        ];
        foreach (ExpressionSimplifier simplifier in simplifiers)
        {
            Assert.AreSame(ExpressionSimplifierOptions.Default, simplifier.Options, simplifier.GetType().Name);
        }
    }

    /// <summary>The options are set at construction, alongside the limits and the quote option, on exact and derived simplifiers.</summary>
    [TestMethod]
    public void Options_AreSetAtConstruction()
    {
        var options = new ExpressionSimplifierOptions { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.UserCode };
        var exact = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited) { Options = options, SimplifyQuotedExpressions = true };
        var derived = new DerivedSimplifier { Options = options };
        Assert.AreSame(options, exact.Options);
        Assert.IsTrue(exact.SimplifyQuotedExpressions);
        Assert.AreSame(options, derived.Options);
        Assert.IsFalse(derived.SimplifyQuotedExpressions);
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExpressionSimplifier { Options = null! });
        Assert.ThrowsExactly<ArgumentNullException>(() => new DerivedSimplifier { Options = null! });
    }

    /// <summary>Undefined trust policies and exception categories are refused, directly and through <c>with</c>.</summary>
    [TestMethod]
    public void InvalidValues_AreRefused()
    {
        foreach (int value in new[] { -1, 2, 3, int.MaxValue, int.MinValue })
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ExpressionSimplifierOptions { TrustPolicy = (ExpressionTrustPolicy)value }, value.ToString());
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ExpressionSimplifierOptions.Default with { TrustPolicy = (ExpressionTrustPolicy)value }, value.ToString());
        }

        foreach (int value in new[] { 8, 9, 15, 16, -1, int.MinValue, int.MaxValue })
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ExpressionSimplifierOptions { ExceptionPreservation = (ExpressionExceptionPreservation)value }, value.ToString());
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ExpressionSimplifierOptions.Default with { ExceptionPreservation = (ExpressionExceptionPreservation)value }, value.ToString());
        }

        for (int value = 0; value <= 7; value++)
        {
            Assert.AreEqual((ExpressionExceptionPreservation)value, new ExpressionSimplifierOptions { ExceptionPreservation = (ExpressionExceptionPreservation)value }.ExceptionPreservation);
        }
    }

    /// <summary>The three axes are independent: setting one never changes another, and every combination is distinct.</summary>
    [TestMethod]
    public void Axes_AreIndependent()
    {
        ExpressionSimplifierOptions[] policies = AllPolicies().ToArray();
        Assert.AreEqual(32, policies.Length);
        Assert.AreEqual(32, policies.Distinct().Count());

        ExpressionSimplifierOptions trusting = ExpressionSimplifierOptions.Default with { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations };
        Assert.IsFalse(trusting.AggressiveSimplification, "trusting declarations does not enable aggressive simplification");
        Assert.AreEqual(ExpressionExceptionPreservation.All, trusting.ExceptionPreservation);

        ExpressionSimplifierOptions aggressive = ExpressionSimplifierOptions.Default with { AggressiveSimplification = true };
        Assert.AreEqual(ExpressionTrustPolicy.Verified, aggressive.TrustPolicy, "aggressive simplification does not trust declarations");
        Assert.AreEqual(ExpressionExceptionPreservation.All, aggressive.ExceptionPreservation, "aggressive simplification does not drop exceptions");

        ExpressionSimplifierOptions relaxed = ExpressionSimplifierOptions.Default with { ExceptionPreservation = ExpressionExceptionPreservation.None };
        Assert.AreEqual(ExpressionTrustPolicy.Verified, relaxed.TrustPolicy);
        Assert.IsFalse(relaxed.AggressiveSimplification);
    }

    // ---------------------------------------------------------------- permissions

    /// <summary>
    /// Under the default aggressiveness and exception set, the policy overloads give exactly the Architecture A permissions of
    /// the same trust policy, for every behavior.
    /// </summary>
    [TestMethod]
    public void Permissions_DefaultPolicyMatchesArchitectureA()
    {
        foreach (ExpressionTrustPolicy trust in new[] { ExpressionTrustPolicy.Verified, ExpressionTrustPolicy.TrustDeclarations })
        {
            ExpressionSimplifierOptions options = ExpressionSimplifierOptions.Default with { TrustPolicy = trust };
            foreach (ExpressionBehavior behavior in AllBehaviors())
            {
                Assert.AreEqual(ExpressionBehaviorPermissions.CanDiscard(behavior, trust), ExpressionBehaviorPermissions.CanDiscard(behavior, options), $"{trust} {behavior.Flags}");
                Assert.AreEqual(ExpressionBehaviorPermissions.CanRepeat(behavior, trust), ExpressionBehaviorPermissions.CanRepeat(behavior, options), $"{trust} {behavior.Flags}");
                Assert.AreEqual(ExpressionBehaviorPermissions.CanReorder(behavior, trust), ExpressionBehaviorPermissions.CanReorder(behavior, options), $"{trust} {behavior.Flags}");
            }
        }

        // The Architecture A contract itself, restated on the raw flags.
        foreach (ExpressionBehavior behavior in AllBehaviors())
        {
            ExpressionBehaviors flags = ExpressionBehaviorPermissions.Effective(behavior, ExpressionTrustPolicy.Verified);
            Assert.AreEqual((flags & (ExpressionBehaviors.SideEffects | ExpressionBehaviors.Allocates | ExpressionBehaviors.MayThrow | ExpressionBehaviors.Unknown)) == 0,
                ExpressionBehaviorPermissions.CanDiscard(behavior, ExpressionSimplifierOptions.Default), behavior.Flags.ToString());
            Assert.AreEqual((flags & (ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.SideEffects | ExpressionBehaviors.Allocates | ExpressionBehaviors.Unknown)) == 0,
                ExpressionBehaviorPermissions.CanRepeat(behavior, ExpressionSimplifierOptions.Default), behavior.Flags.ToString());
            Assert.AreEqual((flags & (ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.SideEffects | ExpressionBehaviors.Allocates | ExpressionBehaviors.MayThrow | ExpressionBehaviors.Unknown)) == 0,
                ExpressionBehaviorPermissions.CanReorder(behavior, ExpressionSimplifierOptions.Default), behavior.Flags.ToString());
        }
    }

    /// <summary>Side effects and unknown code forbid every permission under every policy; so does an untrusted declaration.</summary>
    [TestMethod]
    public void Permissions_ImperativeObstacles()
    {
        foreach (ExpressionSimplifierOptions options in AllPolicies())
        {
            foreach (ExpressionBehavior behavior in AllBehaviors())
            {
                bool imperative = (behavior.Flags & (ExpressionBehaviors.SideEffects | ExpressionBehaviors.Unknown)) != 0
                    || options.TrustPolicy == ExpressionTrustPolicy.Verified && behavior.ReliesOnDeclarations;
                if (!imperative) continue;
                Assert.AreEqual((false, false, false), Permissions(behavior, options), $"{options} {behavior.Flags}");
            }

            Assert.AreEqual((false, false, false), Permissions(new ExpressionBehavior(ExpressionBehaviors.UnknownCode), options), options.ToString());
            Assert.AreEqual((false, false, false), Permissions(new ExpressionBehavior(ExpressionBehaviors.Unknown), options), "a malformed Unknown alone is still unknown: " + options);
            Assert.AreEqual((true, true, true), Permissions(ExpressionBehavior.None, options), options.ToString());
            Assert.AreEqual((true, true, true), Permissions(new ExpressionBehavior(ExpressionBehaviors.ReadsMutableState), options), "permissions are local: " + options);
        }
    }

    /// <summary>
    /// Monotonicity: an extra flag never adds a permission; trusting declarations, aggressive simplification and fewer preserved
    /// categories never remove one.
    /// </summary>
    [TestMethod]
    public void Permissions_AreMonotone()
    {
        ExpressionSimplifierOptions[] policies = AllPolicies().ToArray();
        ExpressionBehavior[] behaviors = AllBehaviors().ToArray();
        foreach (ExpressionSimplifierOptions options in policies)
        {
            foreach (ExpressionBehavior behavior in behaviors)
            {
                var permissions = Permissions(behavior, options);

                foreach (ExpressionBehaviors flag in SingleFlags)
                {
                    Assert.IsTrue(Implies(Permissions(new ExpressionBehavior(behavior.Flags | flag), options), permissions), $"{options} {behavior.Flags} + {flag}");
                }

                if (options.TrustPolicy == ExpressionTrustPolicy.Verified)
                {
                    Assert.IsTrue(Implies(permissions, Permissions(behavior, options with { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations })), $"verified is not more permissive: {options} {behavior.Flags}");
                }

                if (!options.AggressiveSimplification)
                {
                    Assert.IsTrue(Implies(permissions, Permissions(behavior, options with { AggressiveSimplification = true })), $"disabling aggressiveness adds nothing: {options} {behavior.Flags}");
                }

                foreach (ExpressionExceptionPreservation category in new[] { ExpressionExceptionPreservation.Arithmetic, ExpressionExceptionPreservation.Runtime, ExpressionExceptionPreservation.UserCode })
                {
                    if ((options.ExceptionPreservation & category) != 0) continue;
                    Assert.IsTrue(Implies(Permissions(behavior, options with { ExceptionPreservation = options.ExceptionPreservation | category }), permissions),
                        $"preserving {category} adds nothing: {options} {behavior.Flags}");
                }
            }
        }
    }

    /// <summary>
    /// The precise contract of each relaxation: what a cleared exception category, aggressive simplification and trusted
    /// declarations lift, and what they leave in place.
    /// </summary>
    [TestMethod]
    public void Permissions_RelaxationContract()
    {
        foreach (ExpressionSimplifierOptions options in AllPolicies())
        {
            ExpressionBehaviors preserved = ExpressionBehaviorPermissions.PreservedExceptions(options.ExceptionPreservation);
            foreach (ExpressionBehavior behavior in AllBehaviors())
            {
                ExpressionBehaviors flags = ExpressionBehaviorPermissions.Effective(behavior, options.TrustPolicy);
                var (discard, repeat, reorder) = Permissions(behavior, options);
                string context = $"{options} {behavior.Flags}";

                // A preserved exception forbids dropping and moving, never repeating a deterministic evaluation.
                if ((flags & preserved) != 0) Assert.IsFalse(discard || reorder, context);

                // Allocation forbids every permission unless aggressive.
                if ((flags & ExpressionBehaviors.Allocates) != 0 && !options.AggressiveSimplification) Assert.IsFalse(discard || repeat || reorder, context);

                // Nondeterminism forbids repeating and moving unless aggressive, and then only without a preserved exception.
                if ((flags & ExpressionBehaviors.Nondeterministic) != 0 && (!options.AggressiveSimplification || (flags & preserved) != 0)) Assert.IsFalse(repeat || reorder, context);

                // Conversely, nothing else forbids them.
                bool imperative = (flags & (ExpressionBehaviors.SideEffects | ExpressionBehaviors.Unknown)) != 0;
                bool allocation = (flags & ExpressionBehaviors.Allocates) != 0 && !options.AggressiveSimplification;
                bool nondeterministic = (flags & ExpressionBehaviors.Nondeterministic) != 0;
                Assert.AreEqual(!imperative && !allocation && (flags & preserved) == 0, discard, context);
                Assert.AreEqual(!imperative && !allocation && (!nondeterministic || options.AggressiveSimplification && (flags & preserved) == 0), repeat, context);
                Assert.AreEqual(!imperative && !allocation && (flags & preserved) == 0 && (!nondeterministic || options.AggressiveSimplification), reorder, context);
            }
        }
    }

    /// <summary>The exception categories map one to one to the exception flags, and unknown bits relax nothing.</summary>
    [TestMethod]
    public void PreservedExceptions_MapEachCategory()
    {
        Assert.AreEqual(ExpressionBehaviors.None, ExpressionBehaviorPermissions.PreservedExceptions(ExpressionExceptionPreservation.None));
        Assert.AreEqual(ExpressionBehaviors.MayThrowArithmetic, ExpressionBehaviorPermissions.PreservedExceptions(ExpressionExceptionPreservation.Arithmetic));
        Assert.AreEqual(ExpressionBehaviors.MayThrowRuntime, ExpressionBehaviorPermissions.PreservedExceptions(ExpressionExceptionPreservation.Runtime));
        Assert.AreEqual(ExpressionBehaviors.MayThrowUserCode, ExpressionBehaviorPermissions.PreservedExceptions(ExpressionExceptionPreservation.UserCode));
        Assert.AreEqual(ExpressionBehaviors.MayThrow, ExpressionBehaviorPermissions.PreservedExceptions(ExpressionExceptionPreservation.All));
        Assert.AreEqual(ExpressionBehaviors.MayThrow, ExpressionBehaviorPermissions.PreservedExceptions((ExpressionExceptionPreservation)~0), "an unknown bit never removes a category");
    }

    /// <summary>An undefined trust policy reaching the internal permissions is treated as verified, never as trusting.</summary>
    [TestMethod]
    public void Permissions_UndefinedTrustPolicyIsVerified()
    {
        var declared = new ExpressionBehavior(ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode);
        foreach (int value in new[] { -1, 2, 7, int.MaxValue })
        {
            var policy = (ExpressionTrustPolicy)value;
            Assert.AreEqual(ExpressionBehaviors.UnknownCode | ExpressionBehaviors.ReliesOnDeclaration, ExpressionBehaviorPermissions.Effective(declared, policy), value.ToString());
            Assert.IsFalse(ExpressionBehaviorPermissions.CanRepeat(declared, policy), value.ToString());
        }

        Assert.IsTrue(ExpressionBehaviorPermissions.CanRepeat(declared, ExpressionTrustPolicy.TrustDeclarations));
    }

    /// <summary>The permissions of behaviors produced by the analysis itself, not only of annotated methods, under each axis.</summary>
    [TestMethod]
    public void Permissions_OfAnalyzedExpressions()
    {
        ParameterExpression array = Expression.Parameter(typeof(double[]), "a");
        ParameterExpression j = Expression.Parameter(typeof(int), "j");
        var cases = new (string Name, Expression Tree, ExpressionBehaviors Flags)[]
        {
            ("sin", Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, X), ExpressionBehaviors.ReadsMutableState),
            ("sign", Expression.Call(typeof(Math).GetMethod(nameof(Math.Sign), [typeof(double)])!, X), ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowArithmetic),
            ("division", Expression.Divide(I, j), ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowArithmetic),
            ("length", Expression.ArrayLength(array), ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowRuntime),
            ("clock", Expression.Property(null, typeof(DateTime), nameof(DateTime.Now)), ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.Nondeterministic),
            ("box", Expression.Convert(X, typeof(object)), ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.Allocates),
            ("declared", Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Scale)), X), ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowUserCode | ExpressionBehaviors.ReliesOnDeclaration),
            ("unknown", Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Unannotated)), X), ExpressionBehaviors.UnknownCode),
            ("assignment", Expression.Assign(X, Expression.Constant(1.0)), ExpressionBehaviors.SideEffects),
        };

        foreach ((string name, Expression tree, ExpressionBehaviors flags) in cases)
        {
            ExpressionBehavior behavior = ExpressionBehaviorAnalysis.Analyze(tree);
            Assert.AreEqual(flags, behavior.Flags & flags, name);
            if (name != "assignment") Assert.AreEqual(flags, behavior.Flags, name);
        }

        ExpressionBehavior Of(string name) => ExpressionBehaviorAnalysis.Analyze(cases.Single(c => c.Name == name).Tree);
        ExpressionSimplifierOptions defaults = ExpressionSimplifierOptions.Default;
        ExpressionSimplifierOptions noArithmetic = defaults with { ExceptionPreservation = ExpressionExceptionPreservation.Runtime | ExpressionExceptionPreservation.UserCode };
        ExpressionSimplifierOptions noRuntime = defaults with { ExceptionPreservation = ExpressionExceptionPreservation.Arithmetic | ExpressionExceptionPreservation.UserCode };
        ExpressionSimplifierOptions aggressive = defaults with { AggressiveSimplification = true };
        ExpressionSimplifierOptions trusting = defaults with { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations };
        ExpressionSimplifierOptions trustingNoUser = trusting with { ExceptionPreservation = ExpressionExceptionPreservation.Arithmetic | ExpressionExceptionPreservation.Runtime };
        ExpressionSimplifierOptions everything = new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None };

        Assert.AreEqual((true, true, true), Permissions(Of("sin"), defaults));
        Assert.AreEqual((false, true, false), Permissions(Of("sign"), defaults));
        Assert.AreEqual((true, true, true), Permissions(Of("sign"), noArithmetic));
        Assert.AreEqual((false, true, false), Permissions(Of("sign"), noRuntime), "clearing another category lifts nothing");
        Assert.AreEqual((true, true, true), Permissions(Of("division"), noArithmetic));
        Assert.AreEqual((false, true, false), Permissions(Of("length"), noArithmetic));
        Assert.AreEqual((true, true, true), Permissions(Of("length"), noRuntime));
        Assert.AreEqual((true, false, false), Permissions(Of("clock"), defaults));
        Assert.AreEqual((true, true, true), Permissions(Of("clock"), aggressive));
        Assert.AreEqual((false, false, false), Permissions(Of("box"), defaults with { ExceptionPreservation = ExpressionExceptionPreservation.None }), "relaxing exceptions does not relax allocations");
        Assert.AreEqual((true, true, true), Permissions(Of("box"), aggressive));
        Assert.AreEqual((false, false, false), Permissions(Of("declared"), aggressive with { ExceptionPreservation = ExpressionExceptionPreservation.None }), "aggressive simplification does not trust declarations");
        Assert.AreEqual((false, true, false), Permissions(Of("declared"), trusting), "a trusted declaration may still throw");
        Assert.AreEqual((true, true, true), Permissions(Of("declared"), trustingNoUser));
        Assert.AreEqual((false, false, false), Permissions(Of("unknown"), everything), "no policy makes unknown code rewritable");
        Assert.AreEqual((false, false, false), Permissions(Of("assignment"), everything), "no policy makes a side effect rewritable");
    }

    // ---------------------------------------------------------------- default unchanged, guarantees kept

    /// <summary>A traced argument: records its evaluation in <see cref="R4DynamicRuntime.Events"/>.</summary>
    /// <param name="name">The trace name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The call.</returns>
    private static Expression Trace(string name, Expression value) => Expression.Call(R4DynamicRuntime.TraceMethod, Expression.Constant(name), value);

    /// <summary>The hand-built corpus: user calls, exceptions, custom operators and conversions, lambdas, quotes, DAGs.</summary>
    /// <returns>Named bodies of type <see cref="double"/> over <see cref="EtcAuditGenerator.X"/> and <see cref="EtcAuditGenerator.Y"/>.</returns>
    private static List<(string Name, Expression Body)> HandCorpus()
    {
        ParameterExpression x = EtcAuditGenerator.X, y = EtcAuditGenerator.Y;
        Expression lying = Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.LyingCounter)), x);
        Expression declared = Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Scale)), x);
        Expression unknown = Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Unannotated)), y);
        Expression traced = Trace("t", x);
        Expression shared = Expression.Add(Expression.Multiply(x, y), Trace("s", y));
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        LambdaExpression identity = Expression.Lambda<Func<double, double>>(Expression.Add(a, Expression.Constant(0.0)), a);
        Expression intDivision = Expression.Convert(Expression.Divide(Expression.Convert(x, typeof(int)), Expression.Convert(y, typeof(int))), typeof(double));
        Expression checkedInt = Expression.Convert(Expression.ConvertChecked(Expression.Multiply(x, Expression.Constant(1e12)), typeof(int)), typeof(double));
        ParameterExpression m = Expression.Parameter(typeof(Money), "m");
        Expression money = Expression.Convert(Expression.Property(
            Expression.Add(Expression.Convert(Expression.Convert(x, typeof(decimal)), typeof(Money)), Expression.Convert(Expression.Constant(0m), typeof(Money))),
            nameof(Money.Amount)), typeof(double));
        Expression moneyDifference = Expression.Convert(Expression.Convert(Expression.Subtract(
            Expression.Convert(Expression.Convert(x, typeof(decimal)), typeof(Money)), Expression.Convert(Expression.Convert(x, typeof(decimal)), typeof(Money))),
            typeof(decimal)), typeof(double));
        Expression quote = Expression.Call(QuoteHost.ApplyMethod, Expression.Quote(Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Multiply(a, Expression.Constant(1.0)), Expression.Constant(0.0)), a)), x);
        Expression clock = Expression.Convert(Expression.Property(Expression.Property(null, typeof(DateTime), nameof(DateTime.Now)), nameof(DateTime.Ticks)), typeof(double));

        return
        [
            ("lying - lying", Expression.Subtract(lying, lying)),
            ("lying * 0", Expression.Multiply(lying, Expression.Constant(0.0))),
            ("declared + declared", Expression.Add(declared, declared)),
            ("2 unknown - 2 unknown", Expression.Add(Expression.Multiply(Expression.Constant(2.0), unknown), Expression.Multiply(Expression.Constant(-2.0), unknown))),
            ("traced order", Expression.Add(Expression.Add(Trace("a", x), Trace("b", y)), Expression.Multiply(Trace("c", x), Expression.Constant(0.0)))),
            ("traced twice", Expression.Subtract(traced, traced)),
            ("shared dag", Expression.Add(Expression.Multiply(shared, shared), shared)),
            ("int division * 0", Expression.Multiply(intDivision, Expression.Constant(0.0))),
            ("checked overflow * 0", Expression.Multiply(checkedInt, Expression.Constant(0.0))),
            ("throw", Expression.Condition(Expression.GreaterThan(x, Expression.Constant(2.0)), Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(double)), Expression.Add(x, Expression.Constant(0.0)))),
            ("custom operator", money),
            ("custom difference", moneyDifference),
            ("invoked lambda", Expression.Invoke(identity, Expression.Add(x, y))),
            ("closure", Expression.Invoke(Expression.Lambda<Func<double>>(Expression.Multiply(x, Expression.Constant(1.0))))),
            ("quote", quote),
            // Abs keeps the evaluated value +0 whatever the two reads, so the outcomes are reproducible.
            ("clock - clock", Expression.Multiply(Expression.Call(typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!, Expression.Subtract(clock, clock)), Expression.Constant(0.0))),
            ("algebra", Expression.Subtract(Expression.Multiply(Expression.Add(x, Expression.Constant(1.0)), Expression.Add(x, Expression.Constant(1.0))), Expression.Multiply(x, x))),
        ];
    }

    /// <summary>A host method receiving a quoted lambda.</summary>
    public static class QuoteHost
    {
        /// <summary>The <see cref="Apply"/> method.</summary>
        public static readonly MethodInfo ApplyMethod = typeof(QuoteHost).GetMethod(nameof(Apply))!;

        /// <summary>Compiles and applies a quoted function.</summary>
        /// <param name="function">The quoted function.</param>
        /// <param name="x">The argument.</param>
        /// <returns><c>function(x)</c>.</returns>
        public static double Apply(Expression<Func<double, double>> function, double x) => function.Compile()(x);
    }

    /// <summary>The simplifiers of one policy: exact and derived, with the quote option off and on.</summary>
    /// <param name="options">The policy.</param>
    /// <returns>The simplifiers, labeled.</returns>
    private static IEnumerable<(string Label, ExpressionSimplifier Simplifier)> Simplifiers(ExpressionSimplifierOptions options)
    {
        yield return ("exact", new ExpressionSimplifier { Options = options });
        yield return ("exact+quotes", new ExpressionSimplifier(ExpressionResourceLimits.Default) { Options = options, SimplifyQuotedExpressions = true });
        yield return ("derived", new DerivedSimplifier { Options = options });
    }

    /// <summary>The reference simplifier of a label, with the default options.</summary>
    /// <param name="label">The label of <see cref="Simplifiers"/>.</param>
    /// <returns>The simplifier.</returns>
    private static ExpressionSimplifier Reference(string label) => label switch
    {
        "exact" => new ExpressionSimplifier(),
        "exact+quotes" => new ExpressionSimplifier { SimplifyQuotedExpressions = true },
        _ => new DerivedSimplifier(),
    };

    /// <summary>A strict rendering of a result: its debug view and its number of distinct nodes (which pins DAG sharing).</summary>
    /// <param name="e">The result.</param>
    /// <returns>The rendering.</returns>
    private static string Render(Expression e) => EtcAuditSupport.Print(e) + Environment.NewLine + "distinct nodes: " + EtcAuditSupport.Nodes(e).Count;

    /// <summary>The result of <see cref="Run"/>.</summary>
    /// <param name="Render">The rendering of the simplified tree.</param>
    /// <param name="SimplificationCalls">The user calls made while simplifying.</param>
    /// <param name="Outcomes">The outcomes of the simplified tree, as text.</param>
    /// <param name="Evaluated">The outcomes of the simplified tree, or <see langword="null"/> when it was not evaluated.</param>
    /// <param name="LyingCalls">The calls of the lying declaration while evaluating.</param>
    private sealed record RunResult(string Render, string SimplificationCalls, string Outcomes, AuditOutcome[]? Evaluated, int LyingCalls);

    /// <summary>Simplifies and evaluates a body, counting the user calls made during simplification.</summary>
    /// <param name="simplifier">The simplifier.</param>
    /// <param name="body">The body.</param>
    /// <param name="evaluate">Whether to compile and evaluate the result.</param>
    /// <returns>The rendering, the user calls during simplification, and the outcomes.</returns>
    private static RunResult Run(ExpressionSimplifier simplifier, Expression body, bool evaluate)
    {
        BehaviorMethods.LyingCalls = 0;
        int counter = BehaviorMethods.Counter;
        R4DynamicRuntime.Reset();
        Expression result;
        string thrown = "";
        try
        {
            result = simplifier.Simplify(body);
        }
        catch (Exception ex) when (ex is not AssertFailedException)
        {
            result = body;
            thrown = ex.GetType().Name;
        }

        string calls = $"lying {BehaviorMethods.LyingCalls}, counter {BehaviorMethods.Counter - counter}, events {string.Join(",", R4DynamicRuntime.Events)}, thrown {thrown}";
        string outcomes = "";
        AuditOutcome[]? evaluated = null;
        int lyingCalls = 0;
        if (evaluate && thrown.Length == 0)
        {
            Func<double, double, double>? function = EtcAuditSupport.TryCompile(EtcAuditGenerator.Wrap(result), out string? error);
            if (function is null)
            {
                outcomes = "not compilable: " + error;
            }
            else
            {
                BehaviorMethods.LyingCalls = 0;
                evaluated = EtcAuditSupport.Evaluate(function);
                lyingCalls = BehaviorMethods.LyingCalls;
                outcomes = string.Join(" | ", evaluated.Select(o => o.ToString())) + $" lying {lyingCalls}";
            }
        }

        return new RunResult(Render(result), calls, outcomes, evaluated, lyingCalls);
    }

    /// <summary>The policies equal to the default by value, built three ways: each keeps the historical admission.</summary>
    /// <returns>The default-equal policies.</returns>
    private static ExpressionSimplifierOptions[] DefaultEqualPolicies()
        => [ExpressionSimplifierOptions.Default, new ExpressionSimplifierOptions(), ExpressionSimplifierOptions.Default with { }];

    /// <summary>
    /// Asserts that the outcomes of a relaxed policy keep every guarantee the policy does not relax, compared with the default
    /// on the same input: no exception added or changed; an exception of the default may disappear only when the policy leaves a
    /// category unpreserved, the effects traced before it then still running first; otherwise the same traced effects in the same
    /// order; the same value, except where the default's value is not finite (the symbolic identities such as <c>t * 0</c> do not
    /// keep <c>NaN</c> or infinities); a lying declaration evaluated less often only when the policy trusts declarations and does not
    /// preserve user-code exceptions, never more often.
    /// </summary>
    /// <param name="expected">The default's result.</param>
    /// <param name="actual">The policy's result.</param>
    /// <param name="options">The policy.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertKeepsTheGuarantees(RunResult expected, RunResult actual, ExpressionSimplifierOptions options, string context)
    {
        Assert.AreEqual(expected.SimplificationCalls, actual.SimplificationCalls, context);
        Assert.AreEqual(expected.Evaluated is null, actual.Evaluated is null, context);
        if (expected.Evaluated is null || actual.Evaluated is null) return;

        bool relaxesAnException = options.ExceptionPreservation != ExpressionExceptionPreservation.All;
        for (int k = 0; k < expected.Evaluated.Length; k++)
        {
            AuditOutcome source = expected.Evaluated[k], relaxed = actual.Evaluated[k];
            string at = $"{context} input {k}: {source} vs {relaxed}";
            if (relaxed.Exception is not null)
            {
                Assert.AreEqual(source.Exception, relaxed.Exception, at);
                CollectionAssert.AreEqual(source.Events, relaxed.Events, at);
            }
            else if (source.Exception is not null)
            {
                Assert.IsTrue(relaxesAnException, at);
                CollectionAssert.AreEqual(source.Events, relaxed.Events.Take(source.Events.Length).ToArray(), at);
            }
            else
            {
                CollectionAssert.AreEqual(source.Events, relaxed.Events, at);
                Assert.IsTrue(source.Value.Equals(relaxed.Value) || !double.IsFinite(source.Value), at);
            }
        }

        Assert.IsTrue(actual.LyingCalls <= expected.LyingCalls, context);
        if (actual.LyingCalls < expected.LyingCalls)
        {
            Assert.IsTrue(options.TrustPolicy == ExpressionTrustPolicy.TrustDeclarations && (options.ExceptionPreservation & ExpressionExceptionPreservation.UserCode) == 0, context);
        }
    }

    /// <summary>
    /// On the hand-built corpus, for the exact and a derived simplifier, with the quote option off and on: a policy equal to the
    /// default gives exactly the historical tree (structure and sharing), user calls during simplification, and, when the result
    /// runs, the same values, exceptions, traced effects in the same order and number of calls of a lying declaration; every other
    /// policy keeps the guarantees it does not relax (<see cref="AssertKeepsTheGuarantees"/>).
    /// </summary>
    [TestMethod]
    public void EveryPolicy_KeepsItsGuarantees_HandCorpus()
    {
        List<(string Name, Expression Body)> corpus = HandCorpus();
        foreach ((string label, _) in Simplifiers(ExpressionSimplifierOptions.Default))
        {
            foreach ((string name, Expression body) in corpus)
            {
                RunResult expected = Run(Reference(label), body, evaluate: true);
                foreach (ExpressionSimplifierOptions options in DefaultEqualPolicies())
                {
                    RunResult same = Run(Simplifiers(options).Single(s => s.Label == label).Simplifier, body, evaluate: true);
                    string context = $"{label} {name} default-equal";
                    Assert.AreEqual(expected.Render, same.Render, context);
                    Assert.AreEqual(expected.SimplificationCalls, same.SimplificationCalls, context);
                    Assert.AreEqual(expected.Outcomes, same.Outcomes, context);
                }

                foreach (ExpressionSimplifierOptions options in AllPolicies().Skip(1))
                {
                    RunResult actual = Run(Simplifiers(options).Single(s => s.Label == label).Simplifier, body, evaluate: true);
                    AssertKeepsTheGuarantees(expected, actual, options, $"{label} {name} {options}");
                }
            }
        }
    }

    /// <summary>
    /// The same contract on 150 generated trees of every family but extension nodes (conditionals, lambdas, blocks, try,
    /// initializers, dynamic operations, quotes, checked conversions, control flow, shared subtrees). Evaluation runs for the first
    /// 40 trees under every policy.
    /// </summary>
    [TestMethod]
    public void EveryPolicy_KeepsItsGuarantees_GeneratedCorpus()
    {
        var generator = new EtcAuditGenerator(20261105, 5, AuditFamilies.All & ~AuditFamilies.Extension);
        Expression[] trees = Enumerable.Range(0, 150).Select(_ => generator.Generate()).ToArray();
        ExpressionSimplifierOptions[] policies = AllPolicies().Skip(1).ToArray();
        foreach ((string label, _) in Simplifiers(ExpressionSimplifierOptions.Default))
        {
            for (int i = 0; i < trees.Length; i++)
            {
                bool evaluate = i < 40;
                RunResult expected = Run(Reference(label), trees[i], evaluate);
                foreach (ExpressionSimplifierOptions options in DefaultEqualPolicies())
                {
                    RunResult same = Run(Simplifiers(options).Single(s => s.Label == label).Simplifier, trees[i], evaluate);
                    string context = $"{label} tree {i} default-equal";
                    Assert.AreEqual(expected.Render, same.Render, context);
                    Assert.AreEqual(expected.SimplificationCalls, same.SimplificationCalls, context);
                    Assert.AreEqual(expected.Outcomes, same.Outcomes, context);
                }

                foreach (ExpressionSimplifierOptions options in policies)
                {
                    RunResult actual = Run(Simplifiers(options).Single(s => s.Label == label).Simplifier, trees[i], evaluate);
                    AssertKeepsTheGuarantees(expected, actual, options, $"{label} tree {i} {options}");
                }
            }
        }
    }

    /// <summary>
    /// Extension nodes: under every policy the simplifier makes exactly the reads of the default (none of them by the engine
    /// beyond the documented ones) and keeps the same extension instances (an extension node is unknown code: no policy drops
    /// it); a default-equal policy also gives the same tree size, while another policy may rewrite the nodes around them.
    /// </summary>
    [TestMethod]
    public void EveryPolicy_KeepsExtensionNodesAndTheirReads()
    {
        var generator = new EtcAuditGenerator(20261106, 4, AuditFamilies.All, wrapExtensions: true);
        Expression[] trees = Enumerable.Range(0, 60).Select(_ => generator.Generate()).ToArray();
        Assert.IsTrue(generator.Extensions.Count > 0, "the corpus holds extension nodes");

        string Observe(ExpressionSimplifier simplifier, Expression tree)
        {
            foreach (AuditExtension extension in generator.Extensions)
            {
                extension.ArmedCalls.Clear();
                extension.Armed = true;
            }

            Expression result;
            try
            {
                result = simplifier.Simplify(tree);
            }
            finally
            {
                foreach (AuditExtension extension in generator.Extensions) extension.Armed = false;
            }

            HashSet<Expression> nodes = new(EtcAuditSupport.Nodes(result), ReferenceEqualityComparer.Instance);
            IEnumerable<string> kept = generator.Extensions.Select((extension, index) => nodes.Contains(extension) ? index.ToString() : "");
            IEnumerable<string> reads = generator.Extensions.Select(extension => extension.ArmedCalls.Count.ToString());
            return $"kept [{string.Join(",", kept)}] reads [{string.Join(",", reads)}] nodes {nodes.Count}";
        }

        // The kept instances and the reads, without the tree size.
        static string KeptAndReads(string observed) => observed[..observed.LastIndexOf(" nodes ", StringComparison.Ordinal)];

        ExpressionSimplifierOptions[] policies = AllPolicies().ToArray();
        for (int i = 0; i < trees.Length; i++)
        {
            string expected = Observe(new ExpressionSimplifier(), trees[i]);
            string expectedDerived = Observe(new DerivedSimplifier(), trees[i]);
            foreach (ExpressionSimplifierOptions options in DefaultEqualPolicies())
            {
                Assert.AreEqual(expected, Observe(new ExpressionSimplifier { Options = options }, trees[i]), $"tree {i} default-equal");
                Assert.AreEqual(expectedDerived, Observe(new DerivedSimplifier { Options = options }, trees[i]), $"derived tree {i} default-equal");
            }

            foreach (ExpressionSimplifierOptions options in policies)
            {
                Assert.AreEqual(KeptAndReads(expected), KeptAndReads(Observe(new ExpressionSimplifier { Options = options }, trees[i])), $"tree {i} {options}");
                Assert.AreEqual(KeptAndReads(expectedDerived), KeptAndReads(Observe(new DerivedSimplifier { Options = options }, trees[i])), $"derived tree {i} {options}");
            }
        }
    }

    /// <summary>
    /// A custom constant compared by the factoring rules runs its <see cref="object.Equals(object?)"/> and
    /// <see cref="object.GetHashCode"/> during simplification: every policy makes exactly the calls of the default, in the same
    /// order.
    /// </summary>
    [TestMethod]
    public void EveryPolicy_MakesTheSameUserCallsDuringSimplification()
    {
        var log = new List<string>();
        Expression tree = Expression.Add(Expression.Multiply(Inert(new LoggingConstant("a", log)), X), Expression.Multiply(Inert(new LoggingConstant("a", log)), X));
        Expression twin = Expression.Subtract(Expression.Add(Inert(new LoggingConstant("c", log)), Inert(new LoggingConstant("c", log))), Inert(new LoggingConstant("d", log)));

        foreach (Expression source in new[] { tree, twin })
        {
            log.Clear();
            Expression reference = new ExpressionSimplifier().Simplify(source);
            string[] expectedCalls = [.. log];
            string expected = EtcAuditSupport.Print(reference);
            Assert.IsTrue(expectedCalls.Length > 0, "the corpus runs user code during simplification");
            foreach (ExpressionSimplifierOptions options in AllPolicies())
            {
                log.Clear();
                Expression result = new ExpressionSimplifier { Options = options }.Simplify(source);
                CollectionAssert.AreEqual(expectedCalls, log, options.ToString());
                Assert.AreEqual(expected, EtcAuditSupport.Print(result), options.ToString());
            }
        }
    }

    /// <summary>
    /// An inert term holding a custom constant, which the simplifier compares structurally (so it calls the constant's equality
    /// members): <c>ReferenceEqual(constant, null) ? 1 : 2</c>.
    /// </summary>
    /// <param name="constant">The constant.</param>
    /// <returns>The term, of type <see cref="double"/>.</returns>
    private static Expression Inert(LoggingConstant constant)
        => Expression.Condition(Expression.ReferenceEqual(Expression.Constant(constant, typeof(object)), Expression.Constant(null, typeof(object))),
            Expression.Constant(1.0), Expression.Constant(2.0));

    /// <summary>A constant whose equality members log their calls and may run a nested simplification.</summary>
    /// <param name="tag">The equality key.</param>
    /// <param name="log">The call log.</param>
    private sealed class LoggingConstant(string tag, List<string> log)
    {
        /// <summary>Gets or sets an action run by <see cref="Equals(object?)"/> (a re-entrant simplification).</summary>
        public Action? OnEquals { get; set; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            log.Add("Equals " + tag);
            OnEquals?.Invoke();
            return obj is LoggingConstant other && other.Tag == tag;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            log.Add("GetHashCode " + tag);
            return tag.GetHashCode(StringComparison.Ordinal);
        }

        /// <summary>Gets the equality key.</summary>
        public string Tag => tag;
    }

    // ---------------------------------------------------------------- concurrency and re-entrance

    /// <summary>
    /// Instances with different policies run in parallel on shared trees, and one instance is shared by every worker: every
    /// result matches the sequential result of the same policy, and no instance changes the policy of another.
    /// </summary>
    [TestMethod]
    public void ConcurrentInstances_DoNotContaminateEachOther()
    {
        var generator = new EtcAuditGenerator(20261107, 4, AuditFamilies.All & ~AuditFamilies.Extension);
        Expression[] trees = Enumerable.Range(0, 60).Select(_ => generator.Generate()).ToArray();
        ExpressionSimplifierOptions[] policies = AllPolicies().ToArray();
        Dictionary<ExpressionSimplifierOptions, string[]> sequential = policies.ToDictionary(
            options => options,
            options => trees.Select(t => Render(new ExpressionSimplifier { Options = options }.Simplify(t))).ToArray());
        var shared = new ExpressionSimplifier { Options = policies[^1] };

        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, policies.Length * 2, new ParallelOptions { MaxDegreeOfParallelism = 8 }, worker =>
        {
            ExpressionSimplifierOptions options = policies[worker % policies.Length];
            ExpressionSimplifier own = worker % 2 == 0 ? new ExpressionSimplifier { Options = options } : new DerivedSimplifier { Options = options };
            for (int k = 0; k < trees.Length; k++)
            {
                int index = (k + worker * 7) % trees.Length;
                ExpressionSimplifier simplifier = k % 3 == 0 ? shared : own;
                string actual = Render(simplifier.Simplify(trees[index]));
                if (actual != sequential[simplifier.Options][index]) failures.Add($"worker {worker} tree {index} {simplifier.Options}");
                if (!ReferenceEquals(own.Options, options) || !ReferenceEquals(shared.Options, policies[^1])) failures.Add($"worker {worker}: policy changed");
            }
        });

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(10)));
        Assert.AreEqual(ExpressionSimplifierOptions.Default, new ExpressionSimplifierOptions(), "the default is unchanged");
    }

    /// <summary>
    /// A simplification started by user code during another one (a constant's <see cref="object.Equals(object?)"/>) with another
    /// policy joins the running operation and gives the default result; the outer result is unchanged.
    /// </summary>
    [TestMethod]
    public void ReentrantSimplification_WithAnotherPolicy_GivesDefaultResults()
    {
        Expression inner = Expression.Subtract(Expression.Multiply(Expression.Add(X, Expression.Constant(1.0)), Expression.Add(X, Expression.Constant(1.0))), Expression.Multiply(X, X));
        string innerExpected = Render(new ExpressionSimplifier().Simplify(inner));

        var log = new List<string>();
        var innerResults = new List<string>();
        var first = new LoggingConstant("c", log);
        var second = new LoggingConstant("c", log);
        Expression outer = Expression.Add(Expression.Multiply(Inert(first), X), Expression.Multiply(Inert(second), X));
        string outerExpected = Render(new ExpressionSimplifier().Simplify(outer));

        foreach (ExpressionSimplifierOptions options in AllPolicies())
        {
            var nested = new ExpressionSimplifier { Options = options with { AggressiveSimplification = !options.AggressiveSimplification } };
            first.OnEquals = second.OnEquals = () => innerResults.Add(Render(nested.Simplify(inner)));
            innerResults.Clear();
            Assert.AreEqual(outerExpected, Render(new ExpressionSimplifier { Options = options }.Simplify(outer)), options.ToString());
            Assert.IsTrue(innerResults.Count > 0, "the outer simplification compares the constants: " + options);
            Assert.IsTrue(innerResults.All(r => r == innerExpected), options.ToString());
        }

        first.OnEquals = second.OnEquals = null;
    }
}
