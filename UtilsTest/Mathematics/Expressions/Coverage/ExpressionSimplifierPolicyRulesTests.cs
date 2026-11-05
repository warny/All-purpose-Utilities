using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// S5 P18 Architecture C (stages C1 and C2): the simplification policy is consumed by the audited zero and one identities.
/// Under the default policy the historical admission is kept alone; under any other policy a node whose operands are not inert
/// still gets its audited rules, which drop an evaluation only when its behavior may be discarded under the policy, keep every
/// other evaluation, never throw for a zero divisor, and never let a derived simplifier's own rules run outside the gate.
/// </summary>
[TestClass]
[TestCategory("S5-P18C")]
public class ExpressionSimplifierPolicyRulesTests
{
    /// <summary>A free parameter of type <see cref="double"/>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>A free parameter of type <see cref="int"/>.</summary>
    private static readonly ParameterExpression I = Expression.Parameter(typeof(int), "i");

    /// <summary>A free parameter of type <see cref="decimal"/>.</summary>
    private static readonly ParameterExpression D = Expression.Parameter(typeof(decimal), "d");

    /// <summary>The number of calls of the counting functions on this thread.</summary>
    [ThreadStatic]
    private static int _calls;

    /// <summary>An unannotated <see cref="int"/> function counting its calls.</summary>
    /// <param name="value">The argument.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static int CountInt(int value)
    {
        _calls++;
        return value;
    }

    /// <summary>An unannotated <see cref="decimal"/> function counting its calls.</summary>
    /// <param name="value">The argument.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static decimal CountDecimal(decimal value)
    {
        _calls++;
        return value;
    }

    /// <summary>An unannotated <see cref="double"/> function counting its calls.</summary>
    /// <param name="value">The argument.</param>
    /// <returns><c>2 * value</c>.</returns>
    public static double CountDouble(double value)
    {
        _calls++;
        return 2 * value;
    }

    /// <summary>Builds a call to a counting function of this class.</summary>
    /// <param name="name">The function name.</param>
    /// <param name="argument">The argument.</param>
    /// <returns>The call.</returns>
    private static Expression Count(string name, Expression argument) => Expression.Call(typeof(ExpressionSimplifierPolicyRulesTests).GetMethod(name)!, argument);

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A derived simplifier with its own rule, which drops the left operand of any product by zero.</summary>
    private sealed class DroppingSimplifier : ExpressionSimplifier
    {
        /// <summary>Drops <paramref name="left"/> unconditionally.</summary>
        /// <param name="e">The product.</param>
        /// <param name="left">The dropped operand.</param>
        /// <param name="right">The zero.</param>
        /// <returns><paramref name="right"/>.</returns>
        [ExpressionSignature(ExpressionType.Multiply)]
        public Expression DropEverything(BinaryExpression e, Expression left, [ConstantNumeric(0)] ConstantExpression right) => right;
    }

    /// <summary>A derived simplifier hiding an audited rule with <see langword="new"/>.</summary>
    private sealed class HidingSimplifier : ExpressionSimplifier
    {
        /// <summary>Rewrites <c>t + 0</c> to <c>42</c>, dropping <paramref name="left"/>.</summary>
        /// <param name="e">The sum.</param>
        /// <param name="left">The dropped operand.</param>
        /// <param name="right">The zero.</param>
        /// <returns><c>42</c>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        public new Expression AdditionWithZero(BinaryExpression e, Expression left, [ConstantNumeric(0)] ConstantExpression right) => Expression.Constant(42.0);
    }

    /// <summary>A derived simplifier recording, from its own rule on negations, whether a rule ran outside the gate at that time.</summary>
    private sealed class FlagRecordingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the values of <see cref="ExpressionTransformer.RuleRunsOutsideGate"/> seen by <see cref="Record"/>.</summary>
        public List<bool> Seen { get; } = [];

        /// <summary>Records the flag and declines (a positional rule returning <see langword="null"/> declines).</summary>
        /// <param name="e">The negation.</param>
        /// <param name="operand">Its operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Negate)]
        public Expression Record(UnaryExpression e, Expression operand)
        {
            Seen.Add(RuleRunsOutsideGate);
            return null!;
        }
    }

    /// <summary>
    /// Invokes a one-argument function and returns its value (invariant text) or the name of its exception. The sign of a zero is
    /// not kept: it is the documented latitude of the symbolic contract (<c>0 - t</c> is <c>+0</c> where <c>-t</c> is <c>-0</c>).
    /// </summary>
    /// <param name="function">The compiled function.</param>
    /// <param name="argument">The argument.</param>
    /// <returns>The outcome.</returns>
    private static string Outcome(Delegate function, object argument)
    {
        try
        {
            object? value = function.DynamicInvoke(argument);
            if (value is 0.0 or -0.0) return "0";
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        }
        catch (TargetInvocationException exception)
        {
            return "throws " + exception.InnerException!.GetType().Name;
        }
    }

    /// <summary>Formats an expression with the engine's safe formatter.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>The 32 policies, the default first.</summary>
    /// <returns>The policies.</returns>
    private static ExpressionSimplifierOptions[] Policies() => ExpressionSimplifierOptionsTests.AllPolicies().ToArray();

    // ---------------------------------------------------------------- admission

    /// <summary>
    /// The historical admission is kept exactly for the policies equal to the default (by value), and the audited set is the ten
    /// zero and one identities of <see cref="ExpressionSimplifier"/>, identified by method handle: inherited by a derived type,
    /// never a derived rule, not even one hiding an audited rule with <see langword="new"/>.
    /// </summary>
    [TestMethod]
    public void Admission_HistoricalOnlyForTheDefault_AuditedSetIsExact()
    {
        Assert.IsTrue(new ExpressionSimplifier().UsesHistoricalAdmission);
        Assert.IsTrue(new ExpressionSimplifier { Options = new ExpressionSimplifierOptions() }.UsesHistoricalAdmission);
        Assert.IsTrue(new ExpressionSimplifier { Options = ExpressionSimplifierOptions.Default with { } }.UsesHistoricalAdmission);
        Assert.IsTrue(new DerivedSimplifier().UsesHistoricalAdmission);
        foreach (ExpressionSimplifierOptions options in Policies().Skip(1))
        {
            Assert.IsFalse(new ExpressionSimplifier { Options = options }.UsesHistoricalAdmission, options.ToString());
            Assert.IsFalse(new DerivedSimplifier { Options = options }.UsesHistoricalAdmission, options.ToString());
        }

        string[] audited = ["AdditionWithZero", "SubstractionWithZero", "MultiplicationWithZeroOrOne", "DivideWithZeroOrOne", "DivideWithZero", "PowerOfZeroOrOne", "PowerByZeroOrOne"];
        Assert.AreEqual(10, ExpressionSimplifier.AuditedRuleCount);

        var simplifier = new ExpressionSimplifier();
        foreach (Type type in new[] { typeof(ExpressionSimplifier), typeof(DerivedSimplifier), typeof(DroppingSimplifier) })
        {
            MethodInfo[] rules = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => method.IsDefined(typeof(ExpressionSignatureAttribute), inherit: false)).ToArray();
            Assert.AreEqual(10, rules.Count(simplifier.IsAuditedRule), type.Name);
            foreach (MethodInfo rule in rules)
            {
                bool expected = audited.Contains(rule.Name) && rule.DeclaringType == typeof(ExpressionSimplifier);
                Assert.AreEqual(expected, simplifier.IsAuditedRule(rule), $"{type.Name}.{rule.Name}");
            }
        }

        MethodInfo hiding = typeof(HidingSimplifier).GetMethod(nameof(HidingSimplifier.AdditionWithZero), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        Assert.IsFalse(simplifier.IsAuditedRule(hiding), "a new method hiding an audited rule is not audited");
    }

    // ---------------------------------------------------------------- drops

    /// <summary>The operands whose evaluation the dropping identities may remove, by behavior.</summary>
    /// <returns>Named operands of type <see cref="double"/>.</returns>
    private static List<(string Name, Expression Operand)> DroppableOperands()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "v");
        ParameterExpression free = Expression.Parameter(typeof(double), "w");
        ParameterExpression boxed = Expression.Parameter(typeof(object), "o");
        return
        [
            ("unknown call", Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Unannotated)), X)),
            ("declared call", Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Scale)), X)),
            ("lying declaration", Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.LyingCounter)), X)),
            ("integer division", Expression.Convert(Expression.Divide(Expression.Convert(X, typeof(int)), I), typeof(double))),
            ("checked conversion", Expression.Convert(Expression.ConvertChecked(Expression.Multiply(X, Expression.Constant(1e12)), typeof(int)), typeof(double))),
            ("unboxing", Expression.Convert(boxed, typeof(double))),
            ("throw constructed", Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(double))),
            ("throw constant", Expression.Throw(Expression.Constant(new InvalidOperationException()), typeof(double))),
            ("clock", Expression.Convert(Expression.Call(typeof(Stopwatch).GetMethod(nameof(Stopwatch.GetTimestamp))!), typeof(double))),
            ("allocation", Expression.Convert(Expression.ArrayLength(Expression.NewArrayInit(typeof(double), X)), typeof(double))),
            ("assignment", Expression.Assign(free, X)),
            ("local block", Expression.Block(typeof(double), [local], Expression.Assign(local, X), Expression.Multiply(local, Expression.Constant(2.0)))),
        ];
    }

    /// <summary>The dropping identities over an operand: the rewrite and its result when the operand is dropped.</summary>
    /// <param name="t">The operand.</param>
    /// <returns>Named sources with the constant they become.</returns>
    private static IEnumerable<(string Shape, Expression Source, double Result)> DroppingShapes(Expression t)
    {
        yield return ("t * 0", Expression.Multiply(t, Expression.Constant(0.0)), 0.0);
        yield return ("0 * t", Expression.Multiply(Expression.Constant(0.0), t), 0.0);
        yield return ("0 / t", Expression.Divide(Expression.Constant(0.0), t), 0.0);
        yield return ("t ^ 0", Expression.Power(t, Expression.Constant(0.0)), 1.0);
        yield return ("0 ^ t", Expression.Power(Expression.Constant(0.0), t), 0.0);
        yield return ("1 ^ t", Expression.Power(Expression.Constant(1.0), t), 1.0);
    }

    /// <summary>
    /// The dropping identities (<c>t * 0</c>, <c>0 * t</c>, <c>0 / t</c>, <c>t ^ 0</c>, <c>0 ^ t</c>, <c>1 ^ t</c>) drop a
    /// non-inert operand exactly when the policy is not the default and the operand's behavior may be discarded under it, for the
    /// exact and a derived simplifier, over every operand family and the 32 policies. The expectations are spelled out for each
    /// family as well: unknown code (a call, a throw of a constructed exception, whose constructor is user code), a side effect
    /// and an allocation without aggressive simplification are never dropped; a declaration only under trust with user-code
    /// exceptions not preserved, a throw of a constant exception only with user-code exceptions not preserved; a partial integer division or checked conversion only
    /// with arithmetic exceptions not preserved; an unboxing only with runtime exceptions not preserved; an allocation only under
    /// aggressive simplification (one that may also throw a runtime exception, with runtime exceptions not preserved too); a
    /// clock read and a block with local effects under every non-default policy (nondeterminism forbids repeating or
    /// moving, not dropping).
    /// </summary>
    [TestMethod]
    public void DroppingRules_DropOnlyWhatThePolicyAllows()
    {
        foreach ((string name, Expression operand) in DroppableOperands())
        {
            ExpressionBehavior behavior = ExpressionBehaviorAnalysis.Analyze(operand);
            foreach (ExpressionSimplifierOptions options in Policies())
            {
                bool expected = options != ExpressionSimplifierOptions.Default && ExpressionBehaviorPermissions.CanDiscard(behavior, options);
                Assert.AreEqual(ExpectedDrop(name, options), expected, $"{name} {options}: the permission matrix");
                foreach (ExpressionSimplifier simplifier in new ExpressionSimplifier[] { new() { Options = options }, new DerivedSimplifier { Options = options } })
                {
                    foreach ((string shape, Expression source, double result) in DroppingShapes(operand))
                    {
                        Expression simplified = simplifier.Simplify(source);
                        string context = $"{simplifier.GetType().Name} {shape} with {name} {options}: {Text(simplified)}";
                        if (expected)
                        {
                            Assert.AreEqual(result, (simplified as ConstantExpression)?.Value, context);
                        }
                        else
                        {
                            Assert.IsNotInstanceOfType<ConstantExpression>(simplified, context);
                        }
                    }
                }
            }
        }
    }

    /// <summary>The documented expectation of <see cref="DroppingRules_DropOnlyWhatThePolicyAllows"/>, family by family.</summary>
    /// <param name="name">The operand family.</param>
    /// <param name="options">The policy.</param>
    /// <returns>Whether the operand is dropped.</returns>
    private static bool ExpectedDrop(string name, ExpressionSimplifierOptions options)
    {
        if (options == ExpressionSimplifierOptions.Default) return false;
        bool arithmetic = (options.ExceptionPreservation & ExpressionExceptionPreservation.Arithmetic) == 0;
        bool runtime = (options.ExceptionPreservation & ExpressionExceptionPreservation.Runtime) == 0;
        bool userCode = (options.ExceptionPreservation & ExpressionExceptionPreservation.UserCode) == 0;
        bool trust = options.TrustPolicy == ExpressionTrustPolicy.TrustDeclarations;
        return name switch
        {
            "unknown call" or "assignment" or "throw constructed" => false,
            "declared call" or "lying declaration" => trust && userCode,
            "integer division" or "checked conversion" => arithmetic,
            "unboxing" => runtime,
            "throw constant" => userCode,
            "allocation" => options.AggressiveSimplification && runtime,
            "clock" or "local block" => true,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }

    /// <summary>
    /// A dropped evaluation is really gone and a kept one still runs once: the lying declaration (it counts its calls) is
    /// evaluated by <c>lying(x) * 0</c> once under the default and the policies that preserve user-code exceptions, and never
    /// once dropped; the unknown counting call is evaluated once under every policy.
    /// </summary>
    [TestMethod]
    public void DroppingRules_EvaluationCounts()
    {
        Expression lying = Expression.Multiply(Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.LyingCounter)), X), Expression.Constant(0.0));
        Expression unknown = Expression.Multiply(Expression.Constant(0.0), Count(nameof(CountDouble), X));
        foreach (ExpressionSimplifierOptions options in Policies())
        {
            var simplifier = new ExpressionSimplifier { Options = options };
            Func<double, double> lyingFunction = Expression.Lambda<Func<double, double>>(simplifier.Simplify(lying), X).Compile();
            BehaviorMethods.LyingCalls = 0;
            Assert.AreEqual(0.0, lyingFunction(3.0), options.ToString());
            Assert.AreEqual(ExpectedDrop("lying declaration", options) ? 0 : 1, BehaviorMethods.LyingCalls, options.ToString());

            Func<double, double> unknownFunction = Expression.Lambda<Func<double, double>>(simplifier.Simplify(unknown), X).Compile();
            _calls = 0;
            Assert.AreEqual(0.0, unknownFunction(3.0), options.ToString());
            Assert.AreEqual(1, _calls, options.ToString());
        }
    }

    /// <summary>
    /// Removing an exception is what a relaxed category allows, nothing more: <c>(x / i) * 0</c> (an integer division by a
    /// variable) throws <see cref="DivideByZeroException"/> at <c>i = 0</c> under the policies that preserve arithmetic
    /// exceptions, and returns 0 under the others; it never throws while simplifying.
    /// </summary>
    [TestMethod]
    public void DroppingRules_RemoveOnlyNonPreservedExceptions()
    {
        Expression source = Expression.Multiply(Expression.Convert(Expression.Divide(Expression.Convert(X, typeof(int)), I), typeof(double)), Expression.Constant(0.0));
        foreach (ExpressionSimplifierOptions options in Policies())
        {
            Func<double, int, double> function = Expression.Lambda<Func<double, int, double>>(new ExpressionSimplifier { Options = options }.Simplify(source), X, I).Compile();
            Assert.AreEqual(0.0, function(5.0, 2), options.ToString());
            if (ExpectedDrop("integer division", options))
            {
                Assert.AreEqual(0.0, function(5.0, 0), options.ToString());
            }
            else
            {
                Assert.ThrowsExactly<DivideByZeroException>(() => function(5.0, 0), options.ToString());
            }
        }
    }

    /// <summary>The example of the <c>Utils</c> README: <c>(x / i) * 0</c> is kept by the default and becomes 0 without arithmetic preservation.</summary>
    [TestMethod]
    public void ReadmeExample_IntegerDivisionTimesZero()
    {
        ParameterExpression x = Expression.Parameter(typeof(int), "x"), i = Expression.Parameter(typeof(int), "i");
        Expression product = Expression.Multiply(Expression.Divide(x, i), Expression.Constant(0));

        Expression kept = new ExpressionSimplifier().Simplify(product);
        Assert.IsInstanceOfType<BinaryExpression>(kept);
        Assert.ThrowsExactly<DivideByZeroException>(() => Expression.Lambda<Func<int, int, int>>(kept, x, i).Compile()(1, 0));

        Expression dropped = new ExpressionSimplifier
        {
            Options = new ExpressionSimplifierOptions
            {
                ExceptionPreservation = ExpressionExceptionPreservation.Runtime | ExpressionExceptionPreservation.UserCode,
            },
        }.Simplify(product);
        Assert.AreEqual(0, (dropped as ConstantExpression)?.Value);
    }

    /// <summary>
    /// The example table of <c>docs/expressions/SimplificationPolicies.md</c> §8.3, row by row and policy by policy: "kept" means
    /// the result is not a constant, otherwise the expected constant or rendering.
    /// </summary>
    [TestMethod]
    public void DocumentedExamples_MatchTheNote()
    {
        Expression f = Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Unannotated)), X);
        Expression s = Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Scale)), X);
        ParameterExpression j = Expression.Parameter(typeof(int), "j");
        Expression clock = Expression.Call(typeof(Stopwatch).GetMethod(nameof(Stopwatch.GetTimestamp))!);
        ExpressionSimplifierOptions[] columns =
        [
            ExpressionSimplifierOptions.Default,
            new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
            new() { AggressiveSimplification = true },
            new() { ExceptionPreservation = ExpressionExceptionPreservation.Runtime | ExpressionExceptionPreservation.UserCode },
            new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, ExceptionPreservation = ExpressionExceptionPreservation.Arithmetic | ExpressionExceptionPreservation.Runtime },
        ];

        (string Name, Expression Source, string?[] Expected)[] rows =
        [
            ("F(x) * 1", Expression.Multiply(f, Expression.Constant(1.0)), ["kept", Text(f), Text(f), Text(f), Text(f)]),
            ("F(x) * 0", Expression.Multiply(f, Expression.Constant(0.0)), ["kept", "kept", "kept", "kept", "kept"]),
            ("S(x) * 0", Expression.Multiply(s, Expression.Constant(0.0)), ["kept", "kept", "kept", "kept", "0"]),
            ("(i / j) * 0", Expression.Multiply(Expression.Divide(I, j), Expression.Constant(0)), ["kept", "kept", "kept", "0", "kept"]),
            ("GetTimestamp() * 0", Expression.Multiply(clock, Expression.Constant(0L)), ["kept", "0", "0", "0", "0"]),
            ("F(i) / 0", Expression.Divide(Count(nameof(CountInt), I), Expression.Constant(0)), ["kept", "kept", "kept", "kept", "kept"]),
        ];

        foreach ((string name, Expression source, string?[] expected) in rows)
        {
            for (int column = 0; column < columns.Length; column++)
            {
                Expression result = new ExpressionSimplifier { Options = columns[column] }.Simplify(source);
                string actual = result is ConstantExpression constant
                    ? Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture)!
                    : expected[column] == "kept" ? "kept" : Text(result);
                if (expected[column] == "kept" && result is not ConstantExpression) Assert.AreNotEqual(Text(f), Text(result), $"{name} column {column}: {Text(result)}");
                Assert.AreEqual(expected[column], actual, $"{name} column {column}: {Text(result)}");
            }
        }
    }

    // ---------------------------------------------------------------- identities that keep the evaluation

    /// <summary>The identities that keep the single evaluation of their operand, with the expected result.</summary>
    /// <param name="t">The operand.</param>
    /// <param name="one">The constant one of the operand's type.</param>
    /// <param name="zero">The constant zero of the operand's type.</param>
    /// <returns>Named sources with their expected rewrite (<see langword="null"/> when no non-default policy rewrites them).</returns>
    private static IEnumerable<(string Shape, Expression Source, Expression? Expected)> PreservingShapes(Expression t, ConstantExpression one, ConstantExpression zero)
    {
        Expression minusOne = Expression.Constant(Convert.ChangeType(-1, t.Type), t.Type);
        bool signedIntegral = t.Type == typeof(int);
        yield return ("t + 0", Expression.Add(t, zero), t);
        yield return ("0 + t", Expression.Add(zero, t), t);
        yield return ("t - 0", Expression.Subtract(t, zero), t);
        yield return ("0 - t", Expression.Subtract(zero, t), Expression.Negate(t));
        yield return ("t * 1", Expression.Multiply(t, one), t);
        yield return ("1 * t", Expression.Multiply(one, t), t);
        yield return ("t * -1", Expression.Multiply(t, minusOne), Expression.Negate(t));
        yield return ("-1 * t", Expression.Multiply(minusOne, t), Expression.Negate(t));
        yield return ("t / 1", Expression.Divide(t, one), t);

        // x / -1 -> -x is declined for a signed integral type under every policy (MinValue / -1 throws, -MinValue wraps).
        yield return ("t / -1", Expression.Divide(t, minusOne), signedIntegral ? null : Expression.Negate(t));
        if (t.Type == typeof(double))
        {
            yield return ("t ^ 1", Expression.Power(t, Expression.Constant(1.0)), t);
            yield return ("t ^ -1", Expression.Power(t, Expression.Constant(-1.0)), Expression.Divide(Expression.Constant(1.0), t));
            yield return ("t ^ -2", Expression.Power(t, Expression.Constant(-2.0)), Expression.Divide(Expression.Constant(1.0), Expression.Power(t, Expression.Constant(2.0))));
        }
    }

    /// <summary>
    /// The identities that change no evaluation (<c>F() + 0</c>, <c>0 - F()</c>, <c>F() * ±1</c>, <c>F() / ±1</c>, <c>F() ^ 1</c>,
    /// <c>F() ^ -n</c>) apply to an unknown counting call under every non-default policy and under none with the default, for
    /// <see cref="double"/>, <see cref="int"/> and <see cref="decimal"/>; the result evaluates the call exactly once and gives the
    /// source's value, <c>MinValue</c> wrapping included.
    /// </summary>
    [TestMethod]
    public void PreservingRules_KeepTheSingleEvaluation()
    {
        var cases = new (Expression Operand, ConstantExpression One, ConstantExpression Zero, ParameterExpression Parameter, object[] Arguments)[]
        {
            (Count(nameof(CountDouble), X), Expression.Constant(1.0), Expression.Constant(0.0), X, [0.0, -0.75, 3.5, double.NaN, double.PositiveInfinity]),
            (Count(nameof(CountInt), I), Expression.Constant(1), Expression.Constant(0), I, [0, 7, -7, int.MinValue, int.MaxValue]),
            (Count(nameof(CountDecimal), D), Expression.Constant(1m), Expression.Constant(0m), D, [0m, 2.5m, -1.25m, decimal.MinValue, decimal.MaxValue]),
        };

        foreach ((Expression operand, ConstantExpression one, ConstantExpression zero, ParameterExpression parameter, object[] arguments) in cases)
        {
            foreach ((string shape, Expression source, Expression? expected) in PreservingShapes(operand, one, zero))
            {
                Delegate sourceFunction = Expression.Lambda(source, parameter).Compile();
                foreach (ExpressionSimplifierOptions options in Policies())
                {
                    Expression simplified = new ExpressionSimplifier { Options = options }.Simplify(source);
                    string context = $"{operand.Type.Name} {shape} {options}: {Text(simplified)}";
                    // The default keeps the historical result (the additive canonicalization already folds F() + 0 there).
                    if (expected is not null && options != ExpressionSimplifierOptions.Default)
                    {
                        Assert.AreEqual(Text(expected), Text(simplified), context);
                    }
                    else if (options != ExpressionSimplifierOptions.Default)
                    {
                        Assert.AreEqual(Text(new ExpressionSimplifier().Simplify(source)), Text(simplified), context);
                    }

                    Delegate simplifiedFunction = Expression.Lambda(simplified, parameter).Compile();
                    foreach (object argument in arguments)
                    {
                        _calls = 0;
                        string expectedOutcome = Outcome(sourceFunction, argument);
                        Assert.AreEqual(1, _calls, context);
                        _calls = 0;
                        string actualOutcome = Outcome(simplifiedFunction, argument);
                        Assert.AreEqual(1, _calls, context);
                        Assert.AreEqual(expectedOutcome, actualOutcome, $"{context} at {argument}");
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------- zero divisor

    /// <summary>
    /// A constant zero divisor of an integral or decimal division whose dividend is not inert never throws while simplifying:
    /// outside the gate the rule declines, so the dividend still runs first and the evaluation throws
    /// <see cref="DivideByZeroException"/> as the source does. An inert dividend keeps the historical behavior under every policy
    /// (the fold throws while simplifying, as evaluating the node would).
    /// </summary>
    [TestMethod]
    public void ZeroDivisor_OfANonInertDividend_IsLeftToEvaluation()
    {
        Expression intSource = Expression.Divide(Count(nameof(CountInt), I), Expression.Constant(0));
        Expression decimalSource = Expression.Divide(Count(nameof(CountDecimal), D), Expression.Constant(0m));
        Expression inert = Expression.Divide(Expression.Add(I, Expression.Constant(1)), Expression.Constant(0));
        foreach (ExpressionSimplifierOptions options in Policies())
        {
            var simplifier = new ExpressionSimplifier { Options = options };
            Func<int, int> intFunction = Expression.Lambda<Func<int, int>>(simplifier.Simplify(intSource), I).Compile();
            _calls = 0;
            Assert.ThrowsExactly<DivideByZeroException>(() => intFunction(3), options.ToString());
            Assert.AreEqual(1, _calls, options.ToString());

            Func<decimal, decimal> decimalFunction = Expression.Lambda<Func<decimal, decimal>>(simplifier.Simplify(decimalSource), D).Compile();
            _calls = 0;
            Assert.ThrowsExactly<DivideByZeroException>(() => decimalFunction(3m), options.ToString());
            Assert.AreEqual(1, _calls, options.ToString());

            Exception? thrown = null;
            try
            {
                simplifier.Simplify(inert);
            }
            catch (Exception exception)
            {
                thrown = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
            }

            Assert.IsInstanceOfType<DivideByZeroException>(thrown, options.ToString());
        }
    }

    // ---------------------------------------------------------------- derived simplifiers and admission state

    /// <summary>
    /// A derived simplifier's own rules keep the historical admission under every policy: a rule dropping any operand of a
    /// product by zero never runs on <c>F() * 0</c> (it does on inert operands), and a <see langword="new"/> rule hiding
    /// <c>AdditionWithZero</c> never turns <c>F() + 0</c> into its constant, while the inherited audited rules do apply.
    /// </summary>
    [TestMethod]
    public void DerivedRules_KeepTheHistoricalAdmission()
    {
        Expression call = Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Unannotated)), X);
        foreach (ExpressionSimplifierOptions options in Policies())
        {
            Expression dropped = new DroppingSimplifier { Options = options }.Simplify(Expression.Multiply(call, Expression.Constant(0.0)));
            Assert.IsNotInstanceOfType<ConstantExpression>(dropped, $"{options}: {Text(dropped)}");
            Assert.IsInstanceOfType<ConstantExpression>(new DroppingSimplifier { Options = options }.Simplify(Expression.Multiply(X, Expression.Constant(0.0))), options.ToString());

            Expression hidden = new HidingSimplifier { Options = options }.Simplify(Expression.Add(call, Expression.Constant(0.0)));
            Assert.AreNotEqual(42.0, (hidden as ConstantExpression)?.Value, options.ToString());
            if (options != ExpressionSimplifierOptions.Default) Assert.AreEqual(Text(call), Text(hidden), options.ToString());

            Expression inherited = new DerivedSimplifier { Options = options }.Simplify(Expression.Multiply(call, Expression.Constant(-1.0)));
            if (options != ExpressionSimplifierOptions.Default) Assert.AreEqual(Text(Expression.Negate(call)), Text(inherited), options.ToString());
        }
    }

    /// <summary>
    /// The admission of a rule outside the gate is not inherited by the nodes it transforms again (<c>AdditionWithZero</c>
    /// re-transforms its operand): every rule called by the dispatch while a node is transformed sees the flag cleared, and the
    /// flag is cleared after the operation, also when a resource refusal leaves an audited rule with an exception.
    /// </summary>
    [TestMethod]
    public void OutsideGateFlag_IsScopedToTheAuditedRule()
    {
        Expression call = Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Unannotated)), Expression.Negate(X));
        Expression source = Expression.Add(call, Expression.Constant(0.0));
        var options = new ExpressionSimplifierOptions { AggressiveSimplification = true };
        var simplifier = new FlagRecordingSimplifier { Options = options };
        Expression result = simplifier.Simplify(source);
        Assert.AreEqual(Text(call), Text(result));
        Assert.IsTrue(simplifier.Seen.Count >= 2, "the negation is transformed again by AdditionWithZero");
        Assert.IsTrue(simplifier.Seen.All(seen => !seen), string.Join(",", simplifier.Seen));
        Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate);

        // A structural budget that the gate's analysis fits in but the audited rule's behavior analysis does not.
        Expression block = DroppableOperands().Single(o => o.Name == "local block").Operand;
        Expression dropping = Expression.Multiply(block, Expression.Constant(0.0));
        bool refused = false;
        for (long budget = 1; budget < 200 && !refused; budget++)
        {
            try
            {
                new ExpressionSimplifier(ExpressionResourceLimits.Default with { MaxStructuralVisits = budget }) { Options = options }.Simplify(dropping);
            }
            catch (ExpressionResourceLimitException)
            {
                refused = true;
            }

            Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate, $"budget {budget}");
        }

        Assert.IsTrue(refused, "a small structural budget refuses the simplification");
    }

    /// <summary>
    /// The analysis an audited rule runs joins the simplification under the instance's own limits: an unlimited simplifier drops
    /// an operand larger than the default node limit (an array of 60,000 constants, allocated, whose length is read) without being
    /// refused, while a simplifier with the default limits refuses the input before any rule runs.
    /// </summary>
    [TestMethod]
    public void AuditedRuleAnalysis_UsesTheInstanceLimits()
    {
        Expression[] elements = Enumerable.Range(0, 60_000).Select(k => (Expression)Expression.Constant((double)k)).ToArray();
        Expression operand = Expression.Convert(Expression.ArrayLength(Expression.NewArrayInit(typeof(double), elements)), typeof(double));
        Expression source = Expression.Multiply(operand, Expression.Constant(0.0));
        var options = new ExpressionSimplifierOptions { AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None };

        Expression result = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited) { Options = options }.Simplify(source);
        Assert.AreEqual(0.0, (result as ConstantExpression)?.Value);

        ExpressionResourceLimitException refused = Assert.ThrowsExactly<ExpressionResourceLimitException>(() => new ExpressionSimplifier { Options = options }.Simplify(source));
        Assert.AreEqual(ExpressionResourceLimitKind.Nodes, refused.Kind);
    }
}
