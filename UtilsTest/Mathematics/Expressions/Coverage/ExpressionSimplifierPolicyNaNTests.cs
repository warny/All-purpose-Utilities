using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// S5 P18 Architecture C, stage C2b: the <c>NaN</c> fold consumes the simplification policy. Under the default policy the
/// historical whitelist decides alone; under any other policy an ordinary <see cref="double"/>/<see cref="float"/> addition,
/// subtraction, multiplication or division with a <c>NaN</c> operand also drops an operand whose behavior may be discarded under
/// the policy, and keeps every other one, over the 32 policies, the exact and derived simplifiers, every operator and side.
/// </summary>
[TestClass]
[TestCategory("S5-P18C2b")]
public class ExpressionSimplifierPolicyNaNTests
{
    /// <summary>A free parameter of type <see cref="double"/>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>A second free parameter of type <see cref="double"/>.</summary>
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>A free parameter of type <see cref="float"/>.</summary>
    private static readonly ParameterExpression XF = Expression.Parameter(typeof(float), "xf");

    /// <summary>A second free parameter of type <see cref="float"/>.</summary>
    private static readonly ParameterExpression YF = Expression.Parameter(typeof(float), "yf");

    /// <summary>A free parameter of type <see cref="int"/>.</summary>
    private static readonly ParameterExpression I = Expression.Parameter(typeof(int), "i");

    /// <summary>A free parameter of type <see cref="object"/>.</summary>
    private static readonly ParameterExpression O = Expression.Parameter(typeof(object), "o");

    /// <summary>A free parameter of type <see cref="double"/>, assigned by the external assignment family.</summary>
    private static readonly ParameterExpression W = Expression.Parameter(typeof(double), "w");

    /// <summary>A free parameter of type <see cref="Holder"/>.</summary>
    private static readonly ParameterExpression H = Expression.Parameter(typeof(Holder), "h");

    /// <summary>The parameters of every compiled function, in this order.</summary>
    private static readonly ParameterExpression[] Parameters = [X, Y, I, O, W, H];

    /// <summary>The <see cref="double"/> <c>NaN</c> with a payload that differs from the hardware's default.</summary>
    private static readonly double PayloadNaN = BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0123);

    /// <summary>The <see cref="float"/> <c>NaN</c> with a payload that differs from the hardware's default.</summary>
    private static readonly float PayloadNaNF = BitConverter.Int32BitsToSingle(0x7FC0_0123);

    /// <summary>The number of calls of the counting functions and getters on this thread.</summary>
    [ThreadStatic]
    private static int _calls;

    /// <summary>An unannotated function counting its calls.</summary>
    /// <param name="value">The argument.</param>
    /// <returns><c>2 * value</c>.</returns>
    public static double CountDouble(double value)
    {
        _calls++;
        return 2 * value;
    }

    /// <summary>An unannotated user addition, used as a custom operator method.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A finite value even for a <c>NaN</c> operand, so that an erased custom operator is observable.</returns>
    public static double UserAdd(double left, double right)
    {
        _calls++;
        return double.IsNaN(left) || double.IsNaN(right) ? 7.0 : left + right;
    }

    /// <summary>A user type with an unknown getter that counts its reads.</summary>
    public sealed class Holder
    {
        /// <summary>Gets a value, counting the read.</summary>
        public double Value
        {
            get
            {
                _calls++;
                return 4.0;
            }
        }
    }

    /// <summary>An opaque extension node of type <see cref="double"/> that cannot be reduced.</summary>
    private sealed class OpaqueNode : Expression
    {
        /// <inheritdoc/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc/>
        public override Type Type => typeof(double);

        /// <inheritdoc/>
        public override bool CanReduce => false;

        /// <inheritdoc/>
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
    }

    /// <summary>A derived simplifier overriding nothing.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A derived simplifier with its own rules, which run on every negation and every sum, and decline.</summary>
    private sealed class RuleSimplifier : ExpressionSimplifier
    {
        /// <summary>Declines (a positional rule returning <see langword="null"/> declines).</summary>
        /// <param name="e">The negation.</param>
        /// <param name="operand">Its operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Negate)]
        public Expression DeclineNegate(UnaryExpression e, Expression operand) => null!;

        /// <summary>Declines.</summary>
        /// <param name="e">The sum.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression DeclineAdd(BinaryExpression e, Expression left, Expression right) => null!;
    }

    /// <summary>
    /// A derived simplifier whose own rule on negations runs another simplifier (of its own policy) on another tree, and records the
    /// results, the values of <see cref="ExpressionTransformer.RuleRunsOutsideGate"/> and the operation depth around it.
    /// </summary>
    /// <param name="inner">The nested simplifier.</param>
    /// <param name="innerSource">The tree the nested simplifier simplifies.</param>
    private sealed class NestingSimplifier(ExpressionSimplifier inner, Expression innerSource) : ExpressionSimplifier
    {
        /// <summary>Gets the results of the nested operations.</summary>
        public List<Expression> Results { get; } = [];

        /// <summary>Gets the flag and operation depth seen before and after each nested operation.</summary>
        public List<(bool Before, bool After, int DepthBefore, int DepthAfter)> States { get; } = [];

        /// <summary>Runs the nested operation and declines.</summary>
        /// <param name="e">The negation.</param>
        /// <param name="operand">Its operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Negate)]
        public Expression Nest(UnaryExpression e, Expression operand)
        {
            bool before = RuleRunsOutsideGate;
            int depthBefore = ExpressionResourceBudget.OperationDepth;
            Results.Add(inner.Simplify(innerSource));
            States.Add((before, RuleRunsOutsideGate, depthBefore, ExpressionResourceBudget.OperationDepth));
            return null!;
        }
    }

    /// <summary>Formats an expression with the engine's safe formatter.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>The 32 policies, the default first.</summary>
    /// <returns>The policies.</returns>
    private static ExpressionSimplifierOptions[] Policies() => ExpressionSimplifierOptionsTests.AllPolicies().ToArray();

    /// <summary>The exact simplifier, a derived one without rules and a derived one with its own rules, of one policy.</summary>
    /// <param name="options">The policy.</param>
    /// <returns>The simplifiers.</returns>
    private static ExpressionSimplifier[] Simplifiers(ExpressionSimplifierOptions options)
        => [new ExpressionSimplifier { Options = options }, new DerivedSimplifier { Options = options }, new RuleSimplifier { Options = options }];

    // ---------------------------------------------------------------- operand families

    /// <summary>
    /// An operand family: its <see cref="double"/> operand, whether the default policy folds it (the historical whitelist), and
    /// whether a non-default policy folds it, spelled out from the contract of Architecture B rather than computed by the engine.
    /// </summary>
    /// <param name="Name">The family.</param>
    /// <param name="Operand">The operand.</param>
    /// <param name="Historical">Whether the default policy drops it.</param>
    /// <param name="Expected">Whether a non-default policy drops it.</param>
    /// <param name="Analyzed">Whether the decision comes from the behavior analysis (its flags are then cross-checked).</param>
    private sealed record Family(string Name, Expression Operand, bool Historical, Func<ExpressionSimplifierOptions, bool> Expected, bool Analyzed = true);

    /// <summary>Whether <paramref name="options"/> does not preserve arithmetic exceptions.</summary>
    /// <param name="options">The policy.</param>
    /// <returns><see langword="true"/> when they may be removed.</returns>
    private static bool Arithmetic(ExpressionSimplifierOptions options) => (options.ExceptionPreservation & ExpressionExceptionPreservation.Arithmetic) == 0;

    /// <summary>Whether <paramref name="options"/> does not preserve runtime exceptions.</summary>
    /// <param name="options">The policy.</param>
    /// <returns><see langword="true"/> when they may be removed.</returns>
    private static bool Runtime(ExpressionSimplifierOptions options) => (options.ExceptionPreservation & ExpressionExceptionPreservation.Runtime) == 0;

    /// <summary>Whether <paramref name="options"/> does not preserve user-code exceptions.</summary>
    /// <param name="options">The policy.</param>
    /// <returns><see langword="true"/> when they may be removed.</returns>
    private static bool UserCode(ExpressionSimplifierOptions options) => (options.ExceptionPreservation & ExpressionExceptionPreservation.UserCode) == 0;

    /// <summary>Whether <paramref name="options"/> trusts the declarations.</summary>
    /// <param name="options">The policy.</param>
    /// <returns><see langword="true"/> under <see cref="ExpressionTrustPolicy.TrustDeclarations"/>.</returns>
    private static bool Trust(ExpressionSimplifierOptions options) => options.TrustPolicy == ExpressionTrustPolicy.TrustDeclarations;

    /// <summary>The <see cref="double"/> operand families.</summary>
    /// <returns>The families.</returns>
    private static List<Family> Families()
    {
        ParameterExpression local = Expression.Variable(typeof(double), "v");
        ParameterExpression lambdaParameter = Expression.Parameter(typeof(double), "a");
        ParameterExpression lambdaLocal = Expression.Variable(typeof(double), "b");
        Expression clock = Expression.Convert(Expression.Call(typeof(Stopwatch).GetMethod(nameof(Stopwatch.GetTimestamp))!), typeof(double));
        static bool Always(ExpressionSimplifierOptions options) => true;
        static bool Never(ExpressionSimplifierOptions options) => false;
        return
        [
            new("parameter", X, true, Always, Analyzed: false),
            new("pure arithmetic", Expression.Add(Expression.Multiply(X, Y), Expression.Constant(1.0)), true, Always, Analyzed: false),
            new("inert power", Expression.Power(X, Y), false, Never, Analyzed: false),
            new("total function", Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, X), false, Always),
            new("clock", clock, false, Always),
            new("local block", Expression.Block(typeof(double), [local], Expression.Assign(local, X), Expression.Multiply(local, Expression.Constant(2.0))), false, Always),
            new("invoked lambda", Expression.Invoke(
                Expression.Lambda(Expression.Block(typeof(double), [lambdaLocal], Expression.Assign(lambdaLocal, lambdaParameter), Expression.Call(typeof(Math).GetMethod(nameof(Math.Cos), [typeof(double)])!, lambdaLocal)), lambdaParameter),
                X), false, Always),
            new("allocation", Expression.Convert(Expression.ArrayLength(Expression.NewArrayInit(typeof(double), X)), typeof(double)), false, o => o.AggressiveSimplification && Runtime(o)),
            new("integer division", Expression.Convert(Expression.Divide(Expression.Constant(10), I), typeof(double)), false, Arithmetic),
            new("checked conversion", Expression.Convert(Expression.ConvertChecked(Expression.Multiply(X, Expression.Constant(1e12)), typeof(int)), typeof(double)), false, Arithmetic),
            new("unboxing", Expression.Convert(O, typeof(double)), false, Runtime),
            new("throw constant", Expression.Throw(Expression.Constant(new InvalidOperationException()), typeof(double)), false, UserCode),
            new("throw constructed", Expression.Throw(Expression.New(typeof(InvalidOperationException)), typeof(double)), false, Never),
            new("declared call", Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.Scale)), X), false, o => Trust(o) && UserCode(o)),
            new("lying declaration", Expression.Call(BehaviorMethods.Method(nameof(BehaviorMethods.LyingCounter)), X), false, o => Trust(o) && UserCode(o)),
            new("unknown call", Expression.Call(typeof(ExpressionSimplifierPolicyNaNTests).GetMethod(nameof(CountDouble))!, X), false, Never),
            new("external assignment", Expression.Assign(W, X), false, Never),
            new("unknown getter", Expression.Property(H, nameof(Holder.Value)), false, Never),
            new("custom operator", Expression.Add(X, Y, typeof(ExpressionSimplifierPolicyNaNTests).GetMethod(nameof(UserAdd))!), false, Never),
            new("opaque node", new OpaqueNode(), false, Never),
        ];
    }

    /// <summary>
    /// The <see cref="float"/> operand families: native <see cref="float"/> parameters and arithmetic (historical), and every
    /// <see cref="double"/> family converted to <see cref="float"/> (an unchecked narrowing conversion adds no behavior flag, but
    /// the whitelist refuses every conversion, so only a non-default policy drops the converted parameter).
    /// </summary>
    /// <returns>The families.</returns>
    private static List<Family> FloatFamilies()
    {
        static bool Always(ExpressionSimplifierOptions options) => true;
        List<Family> families =
        [
            new("float parameter", XF, true, Always, Analyzed: false),
            new("float arithmetic", Expression.Add(Expression.Multiply(XF, YF), Expression.Constant(1f)), true, Always, Analyzed: false),
            new("converted parameter", Expression.Convert(X, typeof(float)), false, Always),
        ];
        foreach (Family family in Families().Where(f => f.Analyzed))
        {
            families.Add(family with { Name = "converted " + family.Name, Operand = Expression.Convert(family.Operand, typeof(float)) });
        }

        return families;
    }

    /// <summary>The eight binary forms with a <c>NaN</c> constant on either side of each operator.</summary>
    /// <param name="nan">The <c>NaN</c> constant.</param>
    /// <param name="t">The other operand.</param>
    /// <returns>Named sources.</returns>
    private static IEnumerable<(string Form, Expression Source)> BinaryForms(ConstantExpression nan, Expression t)
    {
        foreach ((string symbol, Func<Expression, Expression, BinaryExpression> make) in new (string, Func<Expression, Expression, BinaryExpression>)[]
        {
            ("+", Expression.Add), ("-", Expression.Subtract), ("*", Expression.Multiply), ("/", Expression.Divide),
        })
        {
            yield return ($"NaN {symbol} t", make(nan, t));
            yield return ($"t {symbol} NaN", make(t, nan));
        }
    }

    /// <summary>The wrapped forms: <c>-NaN + t</c>, <c>(c - NaN) + t</c> and <c>t + (c - NaN)</c>.</summary>
    /// <param name="nan">The <c>NaN</c> constant.</param>
    /// <param name="t">The other operand.</param>
    /// <returns>Named sources.</returns>
    private static IEnumerable<(string Form, Expression Source)> WrappedForms(ConstantExpression nan, Expression t)
    {
        Expression c = Expression.Constant(Convert.ChangeType(2, nan.Type, CultureInfo.InvariantCulture), nan.Type);
        yield return ("-NaN + t", Expression.Add(Expression.Negate(nan), t));
        yield return ("(c - NaN) + t", Expression.Add(Expression.Subtract(c, nan), t));
        yield return ("t + (c - NaN)", Expression.Add(t, Expression.Subtract(c, nan)));
    }

    /// <summary>Whether <paramref name="expression"/> is a <c>NaN</c> constant of type <paramref name="type"/>.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="type">The expected type.</param>
    /// <returns><see langword="true"/> for such a constant.</returns>
    private static bool IsNaN(Expression expression, Type type)
        => expression is ConstantExpression constant && constant.Type == type && constant.Value switch
        {
            double value => double.IsNaN(value),
            float value => float.IsNaN(value),
            _ => false,
        };

    // ---------------------------------------------------------------- policy matrix

    /// <summary>
    /// Every family's expectation agrees with the permission it stands for: for the analyzed families, the expectation spelled out
    /// from the contract equals <c>CanDiscard(Analyze(t), options)</c> under every non-default policy; the whitelisted families are
    /// discardable under every policy (the fast path never admits what a policy would refuse); the inert power is discardable too,
    /// but keeps the historical decision on purpose (see <see cref="InertPower_KeepsTheHistoricalDecision"/>).
    /// </summary>
    [TestMethod]
    public void Families_MatchThePermissionMatrix()
    {
        foreach (Family family in Families().Concat(FloatFamilies()))
        {
            ExpressionBehavior behavior = ExpressionBehaviorAnalysis.Analyze(family.Operand);
            foreach (ExpressionSimplifierOptions options in Policies().Skip(1))
            {
                bool permitted = ExpressionBehaviorPermissions.CanDiscard(behavior, options);
                if (family.Analyzed) Assert.AreEqual(family.Expected(options), permitted, $"{family.Name} {options}: {behavior.Flags}");
                else Assert.IsTrue(permitted, $"{family.Name} {options}: {behavior.Flags}");
            }
        }
    }

    /// <summary>
    /// The full matrix: for each <see cref="double"/> family, each of the eight binary forms and the three wrapped forms, each of
    /// the 32 policies and the exact, derived and rule-declaring simplifiers. A family the whitelist accepts gives the default's
    /// tree under every policy (the fast path decides alone; some forms are not folded even by the default, because the
    /// canonicalization merges the constants first, <c>NaN - (x*y + 1)</c> becoming <c>(NaN - 1) - x*y</c>, a shape the historical
    /// recognition does not cover and C2b does not widen). Any other family gives a <c>NaN</c> constant of the node's type exactly
    /// when a non-default policy drops it (the expectation of the family), and a non-constant tree otherwise.
    /// </summary>
    [TestMethod]
    public void Matrix_Double_FoldsExactlyWhatThePolicyAllows()
        => AssertMatrix(Families(), Expression.Constant(PayloadNaN));

    /// <summary>The same matrix for <see cref="float"/>.</summary>
    [TestMethod]
    public void Matrix_Float_FoldsExactlyWhatThePolicyAllows()
        => AssertMatrix(FloatFamilies(), Expression.Constant(PayloadNaNF));

    /// <summary>Runs the matrix for one set of families.</summary>
    /// <param name="families">The families.</param>
    /// <param name="nan">The <c>NaN</c> constant.</param>
    private static void AssertMatrix(List<Family> families, ConstantExpression nan)
    {
        foreach (Family family in families)
        {
            foreach ((string form, Expression source) in BinaryForms(nan, family.Operand).Concat(WrappedForms(nan, family.Operand)))
            {
                string historical = Text(new ExpressionSimplifier().Simplify(source));
                foreach (ExpressionSimplifierOptions options in Policies())
                {
                    bool expected = options != ExpressionSimplifierOptions.Default && family.Expected(options);
                    foreach (ExpressionSimplifier simplifier in Simplifiers(options))
                    {
                        Expression simplified = simplifier.Simplify(source);
                        string context = $"{simplifier.GetType().Name} {form} with {family.Name} {options}: {Text(simplified)}";
                        if (family.Historical) Assert.AreEqual(historical, Text(simplified), context);
                        else if (expected) Assert.IsTrue(IsNaN(simplified, nan.Type), context);
                        else Assert.IsNotInstanceOfType<ConstantExpression>(simplified, context);
                    }
                }
            }
        }
    }

    /// <summary>
    /// The fold returns the source's <c>NaN</c> constant, payload included, for the eight binary forms, under the default (with a
    /// parameter) and under a non-default policy (with a total function, a newly admitted operand), for both types.
    /// </summary>
    [TestMethod]
    public void Fold_KeepsTheNaNPayload()
    {
        Expression sin = Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, X);
        var cases = new (ConstantExpression Nan, Expression Historical, Expression Admitted)[]
        {
            (Expression.Constant(PayloadNaN), X, sin),
            (Expression.Constant(PayloadNaNF), XF, Expression.Convert(sin, typeof(float))),
        };
        var relaxed = new ExpressionSimplifierOptions { AggressiveSimplification = true };
        foreach ((ConstantExpression nan, Expression historical, Expression admitted) in cases)
        {
            foreach ((ExpressionSimplifierOptions options, Expression operand) in new[] { (ExpressionSimplifierOptions.Default, historical), (relaxed, historical), (relaxed, admitted) })
            {
                foreach ((string form, Expression source) in BinaryForms(nan, operand))
                {
                    ConstantExpression? folded = new ExpressionSimplifier { Options = options }.Simplify(source) as ConstantExpression;
                    Assert.IsNotNull(folded, $"{form} {options}");
                    Assert.AreEqual(nan.Type, folded.Type, form);
                    if (nan.Value is double value) Assert.AreEqual(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits((double)folded.Value!), form);
                    else Assert.AreEqual(BitConverter.SingleToInt32Bits((float)nan.Value!), BitConverter.SingleToInt32Bits((float)folded.Value!), form);
                }
            }
        }
    }

    /// <summary>
    /// An operand of inert arithmetic that the whitelist refuses (<c>x ^ y</c>, whose behavior may be discarded) keeps the
    /// historical decision under every policy: the S5 P16 reuse of the comparer's forms requires that preparing an inert operand
    /// again builds the tree the default simplification builds. The same power behind a total function call is dropped.
    /// </summary>
    [TestMethod]
    public void InertPower_KeepsTheHistoricalDecision()
    {
        Expression power = Expression.Power(X, Y);
        Expression source = Expression.Add(Expression.Constant(double.NaN), power);
        string historical = Text(new ExpressionSimplifier().Simplify(source));
        foreach (ExpressionSimplifierOptions options in Policies())
        {
            Assert.AreEqual(historical, Text(new ExpressionSimplifier { Options = options }.Simplify(source)), options.ToString());
        }

        Expression wrapped = Expression.Add(Expression.Constant(double.NaN), Expression.Call(typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!, power));
        Assert.IsTrue(IsNaN(new ExpressionSimplifier { Options = Policies()[1] }.Simplify(wrapped), typeof(double)));
    }

    /// <summary>
    /// The shortcut for nested refusals (an operand that is, or directly evaluates, a <c>NaN</c> node left unfolded is refused
    /// without analysis) decides as the analysis would: for every family and policy, <c>NaN + (y * (NaN + t))</c> and
    /// <c>NaN + (NaN * (NaN + t))</c> fold entirely exactly when <c>t</c> may be dropped, and when they do not, the outer operand
    /// refused without analysis is refused by the analysis too.
    /// </summary>
    [TestMethod]
    public void NestedRefusal_DecidesAsTheAnalysis()
    {
        ConstantExpression nan = Expression.Constant(double.NaN);
        foreach (ExpressionSimplifierOptions options in Policies().Skip(1))
        {
            foreach (Family family in Families().Where(f => f.Analyzed))
            {
                foreach (Func<Expression, Expression> wrap in new Func<Expression, Expression>[] { inner => Expression.Multiply(Y, inner), inner => Expression.Multiply(nan, inner) })
                {
                    Expression source = Expression.Add(nan, wrap(Expression.Add(nan, family.Operand)));
                    Expression simplified = new ExpressionSimplifier { Options = options }.Simplify(source);
                    string context = $"{family.Name} {options}: {Text(simplified)}";
                    Assert.AreEqual(family.Expected(options), IsNaN(simplified, typeof(double)), context);
                    if (simplified is BinaryExpression { NodeType: ExpressionType.Add } outer && outer.Left is ConstantExpression)
                    {
                        Assert.IsFalse(ExpressionBehaviorPermissions.CanDiscard(ExpressionBehaviorAnalysis.Analyze(outer.Right), options), context);
                    }
                }
            }
        }
    }

    /// <summary>A custom operator on the <c>NaN</c> node itself is never folded, under any policy, whatever the other operand.</summary>
    [TestMethod]
    public void CustomOperatorNode_IsNeverFolded()
    {
        MethodInfo userAdd = typeof(ExpressionSimplifierPolicyNaNTests).GetMethod(nameof(UserAdd))!;
        foreach (ExpressionSimplifierOptions options in Policies())
        {
            foreach (Expression source in new Expression[] { Expression.Add(Expression.Constant(double.NaN), X, userAdd), Expression.Add(X, Expression.Constant(double.NaN), userAdd) })
            {
                Expression simplified = new ExpressionSimplifier { Options = options }.Simplify(source);
                Assert.IsNotInstanceOfType<ConstantExpression>(simplified, options.ToString());
                _calls = 0;
                Assert.AreEqual(7.0, Expression.Lambda<Func<double, double>>(simplified, X).Compile()(1.0), options.ToString());
                Assert.AreEqual(1, _calls, options.ToString());
            }
        }
    }

    /// <summary>
    /// The four operators are the only ones folded: <c>NaN ^ t</c>, <c>NaN % t</c> and the comparisons are kept under every policy
    /// with a newly admitted operand (a total function), as the historical fold keeps them with a parameter.
    /// </summary>
    [TestMethod]
    public void OtherOperators_AreNeverFolded()
    {
        Expression sin = Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, X);
        ConstantExpression nan = Expression.Constant(double.NaN);
        foreach (ExpressionSimplifierOptions options in Policies())
        {
            foreach (Expression source in new Expression[] { Expression.Power(nan, sin), Expression.Power(sin, nan), Expression.Modulo(nan, sin), Expression.Modulo(sin, nan) })
            {
                Expression simplified = new ExpressionSimplifier { Options = options }.Simplify(source);
                Assert.IsNotInstanceOfType<ConstantExpression>(simplified, $"{options}: {Text(simplified)}");
            }
        }
    }

    // ---------------------------------------------------------------- observability

    /// <summary>The outcome of one evaluation: the value (every <c>NaN</c> alike) or the exception, and the user calls.</summary>
    /// <param name="function">The compiled function over <see cref="Parameters"/>.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The outcome and the number of counted calls.</returns>
    private static (string Outcome, int Calls) Evaluate(Delegate function, object?[] arguments)
    {
        _calls = 0;
        BehaviorMethods.LyingCalls = 0;
        string outcome;
        try
        {
            object? value = function.DynamicInvoke(arguments);
            outcome = value is double d && double.IsNaN(d) ? "NaN" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        }
        catch (TargetInvocationException exception)
        {
            outcome = "throws " + exception.InnerException!.GetType().Name;
        }

        return (outcome, _calls + BehaviorMethods.LyingCalls);
    }

    /// <summary>The exception category a family's evaluation may raise, by the exception type it raises.</summary>
    /// <param name="exception">The name of the exception type.</param>
    /// <param name="options">The policy.</param>
    /// <returns>Whether the policy allows removing it.</returns>
    private static bool MayRemove(string exception, ExpressionSimplifierOptions options) => exception switch
    {
        "throws DivideByZeroException" or "throws OverflowException" => Arithmetic(options),
        "throws InvalidCastException" or "throws NullReferenceException" => Runtime(options),
        "throws InvalidOperationException" => UserCode(options),
        _ => false,
    };

    /// <summary>
    /// Compiles the source and the simplified tree of <c>NaN + t</c> and <c>t * NaN</c> for every compilable family and policy,
    /// and evaluates both on inputs that make the operands throw or not: a kept operand gives the same value, exception and number
    /// of user calls; a dropped one gives <c>NaN</c> with no user call, and a difference with the source is exactly what the policy
    /// allows (an exception of a category it does not preserve, the calls of a declaration it trusts).
    /// </summary>
    [TestMethod]
    public void Evaluation_DiffersOnlyByWhatThePolicyAllows()
    {
        object?[][] inputs =
        [
            [3.0, 2.0, 2, (object)1.5, 0.0, new Holder()],
            [1e3, -1.0, 0, "text", 0.0, new Holder()],
        ];
        foreach (Family family in Families().Where(f => f.Operand is not OpaqueNode))
        {
            ConstantExpression nan = Expression.Constant(double.NaN);
            foreach (Expression source in new Expression[] { Expression.Add(nan, family.Operand), Expression.Multiply(family.Operand, nan) })
            {
                Delegate sourceFunction = Expression.Lambda(source, Parameters).Compile();
                foreach (ExpressionSimplifierOptions options in Policies())
                {
                    Expression simplified = new ExpressionSimplifier { Options = options }.Simplify(source);
                    bool dropped = IsNaN(simplified, typeof(double));
                    Delegate simplifiedFunction = Expression.Lambda(simplified, Parameters).Compile();
                    foreach (object?[] input in inputs)
                    {
                        (string sourceOutcome, int sourceCalls) = Evaluate(sourceFunction, input);
                        (string outcome, int calls) = Evaluate(simplifiedFunction, input);
                        string context = $"{family.Name} {options} at {input[0]},{input[2]},{input[3]}: {sourceOutcome} -> {outcome}";
                        if (!dropped)
                        {
                            Assert.AreEqual(sourceOutcome, outcome, context);
                            Assert.AreEqual(sourceCalls, calls, context);
                            continue;
                        }

                        Assert.AreEqual("NaN", outcome, context);
                        Assert.AreEqual(0, calls, context);
                        if (sourceOutcome != "NaN") Assert.IsTrue(MayRemove(sourceOutcome, options), context);
                        if (sourceCalls != 0) Assert.AreEqual("lying declaration", family.Name, context);
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------- default compatibility

    /// <summary>
    /// The default keeps the historical structure exactly: every <c>NaN</c> form over every family gives, with the exact
    /// simplifier and every policy equal to the default by value, the same tree as a fresh default simplifier, and that tree is
    /// folded only for the whitelisted families. A non-default policy gives the default's tree whenever it drops nothing more.
    /// </summary>
    [TestMethod]
    public void Default_KeepsTheHistoricalStructure()
    {
        ExpressionSimplifierOptions[] defaults = [ExpressionSimplifierOptions.Default, new ExpressionSimplifierOptions(), ExpressionSimplifierOptions.Default with { }];
        ConstantExpression nan = Expression.Constant(double.NaN);
        foreach (Family family in Families())
        {
            foreach ((string form, Expression source) in BinaryForms(nan, family.Operand).Concat(WrappedForms(nan, family.Operand)))
            {
                Expression historicalTree = new ExpressionSimplifier().Simplify(source);
                string historical = Text(historicalTree);
                if (!family.Historical) Assert.IsNotInstanceOfType<ConstantExpression>(historicalTree, $"{form} {family.Name}: {historical}");
                foreach (ExpressionSimplifierOptions options in defaults)
                {
                    Assert.AreEqual(historical, Text(new ExpressionSimplifier { Options = options }.Simplify(source)), $"{form} {family.Name}");
                }

                foreach (ExpressionSimplifierOptions options in Policies().Skip(1).Where(o => !family.Expected(o)))
                {
                    Assert.AreEqual(historical, Text(new ExpressionSimplifier { Options = options }.Simplify(source)), $"{form} {family.Name} {options}");
                }
            }
        }
    }

    /// <summary>
    /// <see cref="ExpressionComparer.Default"/> keeps the default policy: <c>NaN + Sin(x)</c> does not compare equal to <c>NaN</c>,
    /// although a non-default policy simplifies the first into the second, and the hashes of the comparer are unchanged by the
    /// simplifications of other policies run before.
    /// </summary>
    [TestMethod]
    public void Comparer_KeepsTheDefaultPolicy()
    {
        Expression sin = Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, X);
        ConstantExpression nan = Expression.Constant(double.NaN);
        Expression source = Expression.Add(nan, sin);
        int hash = ExpressionComparer.Default.GetHashCode(source);
        Assert.IsFalse(ExpressionComparer.Default.Equals(source, nan));
        foreach (ExpressionSimplifierOptions options in Policies().Skip(1))
        {
            Assert.IsTrue(IsNaN(new ExpressionSimplifier { Options = options }.Simplify(source), typeof(double)), options.ToString());
        }

        Assert.IsFalse(ExpressionComparer.Default.Equals(source, nan));
        Assert.AreEqual(hash, ExpressionComparer.Default.GetHashCode(source));
        Assert.IsTrue(ExpressionComparer.Default.Equals(Expression.Add(nan, X), nan), "the historical fold is part of the comparer");
    }

    // ---------------------------------------------------------------- roots, lambdas, blocks, quotes

    /// <summary>
    /// The three call sites of the fold apply the policy: at the root (<c>Transform</c>), on a direct operand
    /// (<c>PrepareExpression</c>: <c>Abs(NaN + Sin(x))</c>), in a lambda body (<c>RebuildLambdaExpression</c>, the body is not
    /// prepared), in nested lambdas and in a block. Each is folded under a non-default policy and kept by the default, and the
    /// lambdas keep their delegate type, name, tail-call flag and parameters.
    /// </summary>
    [TestMethod]
    public void CallSites_ApplyThePolicy()
    {
        Expression sin = Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, X);
        Expression folding = Expression.Add(Expression.Constant(double.NaN), sin);
        MethodInfo abs = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!;
        var relaxed = new ExpressionSimplifierOptions { AggressiveSimplification = true };
        var defaultSimplifier = new ExpressionSimplifier();
        var policySimplifier = new ExpressionSimplifier { Options = relaxed };

        // Root.
        Assert.IsFalse(IsNaN(defaultSimplifier.Simplify(folding), typeof(double)));
        Assert.IsTrue(IsNaN(policySimplifier.Simplify(folding), typeof(double)));

        // Direct operand.
        Expression operand = Expression.Call(abs, folding);
        Assert.AreEqual(Text(Expression.Call(abs, Expression.Constant(double.NaN))), Text(policySimplifier.Simplify(operand)));
        Assert.AreNotEqual(Text(Expression.Call(abs, Expression.Constant(double.NaN))), Text(defaultSimplifier.Simplify(operand)));

        // Lambda body, then nested lambdas.
        LambdaExpression lambda = Expression.Lambda<Func<double, double>>(folding, "named", tailCall: true, [X]);
        var result = (LambdaExpression)policySimplifier.Simplify(lambda);
        Assert.IsTrue(IsNaN(result.Body, typeof(double)));
        Assert.AreEqual(typeof(Func<double, double>), result.Type);
        Assert.AreEqual("named", result.Name);
        Assert.IsTrue(result.TailCall);
        Assert.AreSame(X, result.Parameters.Single());
        Assert.IsFalse(IsNaN(((LambdaExpression)defaultSimplifier.Simplify(lambda)).Body, typeof(double)));

        Expression nested = Expression.Lambda<Func<double, Func<double, double>>>(Expression.Lambda<Func<double, double>>(folding, Y), X);
        var outer = (LambdaExpression)policySimplifier.Simplify(nested);
        Assert.IsTrue(IsNaN(((LambdaExpression)outer.Body).Body, typeof(double)));
        Assert.IsFalse(IsNaN(((LambdaExpression)((LambdaExpression)defaultSimplifier.Simplify(nested)).Body).Body, typeof(double)));

        // Block.
        ParameterExpression v = Expression.Variable(typeof(double), "v");
        Expression block = Expression.Block(typeof(double), [v], Expression.Assign(v, X), Expression.Add(Expression.Constant(double.NaN), Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, v)));
        var simplifiedBlock = (BlockExpression)policySimplifier.Simplify(block);
        Assert.IsTrue(IsNaN(simplifiedBlock.Expressions[^1], typeof(double)), Text(simplifiedBlock));
        Assert.IsFalse(IsNaN(((BlockExpression)defaultSimplifier.Simplify(block)).Expressions[^1], typeof(double)));
    }

    /// <summary>
    /// A quote stays a strict boundary when <see cref="ExpressionSimplifier.SimplifyQuotedExpressions"/> is off, under every
    /// policy: the very same node is returned at the root and kept as a call argument. When it is on, the quoted body is folded
    /// under a non-default policy and kept by the default.
    /// </summary>
    [TestMethod]
    public void Quotes_AreBoundariesUnlessEnabled()
    {
        Expression sin = Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, X);
        LambdaExpression lambda = Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Constant(double.NaN), sin), X);
        UnaryExpression quote = Expression.Quote(lambda);
        MethodInfo identity = typeof(BehaviorMethods).GetMethod(nameof(BehaviorMethods.Identity))!.MakeGenericMethod(typeof(Expression<Func<double, double>>));
        Expression argument = Expression.Call(identity, quote);
        foreach (ExpressionSimplifierOptions options in Policies())
        {
            var simplifier = new ExpressionSimplifier { Options = options };
            Assert.AreSame(quote, simplifier.Simplify(quote), options.ToString());
            Assert.AreSame(quote, ((MethodCallExpression)simplifier.Simplify(argument)).Arguments[0], options.ToString());

            var enabled = new ExpressionSimplifier { Options = options, SimplifyQuotedExpressions = true };
            var simplifiedQuote = (UnaryExpression)enabled.Simplify(quote);
            Assert.AreEqual(ExpressionType.Quote, simplifiedQuote.NodeType);
            Assert.AreEqual(options != ExpressionSimplifierOptions.Default, IsNaN(((LambdaExpression)simplifiedQuote.Operand).Body, typeof(double)), options.ToString());
        }
    }

    /// <summary>
    /// A refused fold leaves the node as the historical path leaves it, under every policy: <c>NaN + F(x)</c> with an unknown
    /// <c>F</c> gives the default's tree, which still evaluates <c>F</c> once (the simplifier itself may rebuild the call node, on
    /// the historical path too, so the instance is not an invariant here; the quote tests check instances where no rewrite may
    /// happen at all).
    /// </summary>
    [TestMethod]
    public void RefusedFold_KeepsTheHistoricalTree()
    {
        Expression call = Expression.Call(typeof(ExpressionSimplifierPolicyNaNTests).GetMethod(nameof(CountDouble))!, X);
        Expression source = Expression.Add(Expression.Constant(double.NaN), call);
        string historical = Text(new ExpressionSimplifier().Simplify(source));
        foreach (ExpressionSimplifierOptions options in Policies())
        {
            Expression simplified = new ExpressionSimplifier { Options = options }.Simplify(source);
            Assert.AreEqual(historical, Text(simplified), options.ToString());
            _calls = 0;
            Assert.IsTrue(double.IsNaN(Expression.Lambda<Func<double, double>>(simplified, X).Compile()(1.0)), options.ToString());
            Assert.AreEqual(1, _calls, options.ToString());
        }
    }

    // ---------------------------------------------------------------- reentrance

    /// <summary>
    /// A simplification nested in another one on the same thread, by an instance of another policy, folds with its own policy:
    /// every combination of the default and a relaxed policy, outer and inner, gives the nested operation the result its instance
    /// gives alone and the outer one its own result; the outside-gate flag, the operation depth and the lexical scope are the same
    /// before and after each nested operation, and the flag is cleared at the end.
    /// </summary>
    [TestMethod]
    public void NestedSimplifications_FoldWithTheirOwnPolicy()
    {
        Expression sin = Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, X);
        Expression innerSource = Expression.Lambda<Func<double, double>>(Expression.Add(Expression.Constant(double.NaN), sin), X);
        Expression outerSource = Expression.Lambda<Func<double, double>>(
            Expression.Multiply(Expression.Negate(X), Expression.Add(Expression.Constant(double.NaN), Expression.Call(typeof(Math).GetMethod(nameof(Math.Cos), [typeof(double)])!, Expression.Negate(X)))), X);
        ExpressionSimplifierOptions[] policies = [ExpressionSimplifierOptions.Default, new() { AggressiveSimplification = true }];
        foreach (ExpressionSimplifierOptions outerOptions in policies)
        {
            string outerAlone = Text(new ExpressionSimplifier { Options = outerOptions }.Simplify(outerSource));
            foreach (ExpressionSimplifierOptions innerOptions in policies)
            {
                string context = $"outer {outerOptions}, inner {innerOptions}";
                string innerAlone = Text(new ExpressionSimplifier { Options = innerOptions }.Simplify(innerSource));
                var outer = new NestingSimplifier(new ExpressionSimplifier { Options = innerOptions }, innerSource) { Options = outerOptions };
                Assert.AreEqual(outerAlone, Text(outer.Simplify(outerSource)), context);
                Assert.IsTrue(outer.Results.Count > 0, context);
                Assert.IsTrue(outer.Results.All(result => Text(result) == innerAlone), $"{context}: {innerAlone}");
                Assert.IsTrue(outer.States.All(state => state.Before == state.After && state.DepthBefore == state.DepthAfter), context);
                Assert.IsFalse(ExpressionTransformer.RuleRunsOutsideGate, context);
                Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth, context);
            }
        }

        Assert.AreNotEqual(
            Text(new ExpressionSimplifier().Simplify(innerSource)),
            Text(new ExpressionSimplifier { Options = policies[1] }.Simplify(innerSource)),
            "the inner tree tells the two policies apart");
    }

    /// <summary>
    /// Concurrent simplifications of the same tree by two instances of different policies, on several threads, each give the
    /// result of their own policy.
    /// </summary>
    [TestMethod]
    public void ConcurrentSimplifications_KeepTheirOwnPolicy()
    {
        Expression sin = Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, X);
        Expression source = Expression.Add(Expression.Constant(double.NaN), Expression.Multiply(sin, Expression.Add(Expression.Constant(double.NaN), sin)));
        var defaultSimplifier = new ExpressionSimplifier();
        var policySimplifier = new ExpressionSimplifier { Options = new ExpressionSimplifierOptions { AggressiveSimplification = true } };
        string expectedDefault = Text(defaultSimplifier.Simplify(source));
        string expectedPolicy = Text(policySimplifier.Simplify(source));
        Assert.AreNotEqual(expectedDefault, expectedPolicy);

        var failures = new System.Collections.Concurrent.ConcurrentQueue<string>();
        System.Threading.Tasks.Parallel.For(0, 64, index =>
        {
            ExpressionSimplifier simplifier = index % 2 == 0 ? defaultSimplifier : policySimplifier;
            string expected = index % 2 == 0 ? expectedDefault : expectedPolicy;
            string actual = Text(simplifier.Simplify(source));
            if (actual != expected) failures.Enqueue($"{index}: {actual}");
        });
        Assert.IsTrue(failures.IsEmpty, string.Join("; ", failures));
    }
}
