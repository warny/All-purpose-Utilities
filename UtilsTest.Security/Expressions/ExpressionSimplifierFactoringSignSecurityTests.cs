using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// Security invariants of the K/N/F mission (<c>docs/expressions/SimplifierFlatten.md</c> §26): the finishing of a small factored
/// coefficient (K), the orientation of a negated sum (N) and of an extracted numeric factor or factored product (F) never read an
/// extension node or a custom reflection object beyond what the engine of <c>master</c> reads, never move an observable
/// evaluation, keep the values, the evaluation order and the exceptions under every policy, and stay within the budgets and the
/// execution stack.
/// </summary>
[TestClass]
public class ExpressionSimplifierFactoringSignSecurityTests
{
    private static readonly ParameterExpression A = Expression.Parameter(typeof(double), "a");
    private static readonly ParameterExpression B = Expression.Parameter(typeof(double), "b");
    private static readonly ParameterExpression C = Expression.Parameter(typeof(double), "c");
    private static readonly ParameterExpression D = Expression.Parameter(typeof(double), "d");
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>The policies the invariants are checked under.</summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    private static ConstantExpression K(double value) => Expression.Constant(value);

    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>Runs <paramref name="action"/> with the three corrections of the mission switched off (the engine of master).</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="action">The action.</param>
    /// <returns>Its result.</returns>
    private static T Master<T>(Func<T> action)
    {
        ExpressionSimplifier.FactoringSignConsistencyDisabled = true;
        try
        {
            return action();
        }
        finally
        {
            ExpressionSimplifier.FactoringSignConsistencyDisabled = false;
        }
    }

    /// <summary>An extension node whose every member throws once armed, counting the calls.</summary>
    private sealed class ArmedExtension : Expression
    {
        public bool Armed { get; set; }

        public int Calls { get; private set; }

        private T Hit<T>(T value)
        {
            if (!Armed) return value;
            Calls++;
            throw new InvalidOperationException("A member of an extension node was called.");
        }

        /// <inheritdoc/>
        public override ExpressionType NodeType => Hit(ExpressionType.Extension);

        /// <inheritdoc/>
        public override Type Type => Hit(typeof(double));

        /// <inheritdoc/>
        public override bool CanReduce => Hit(false);

        /// <inheritdoc/>
        public override string ToString() => Hit("extension");
    }

    /// <summary>A plain multiplication, impersonated as a user-defined operator method.</summary>
    /// <param name="x">The left operand.</param>
    /// <param name="y">The right operand.</param>
    /// <returns>The product.</returns>
    public static double Times(double x, double y) => x * y;

    /// <summary>
    /// Hostile operands in every position the mission reads (a negated sum, a subtracted factored product, a negated product, the
    /// pair of a binary factoring, a growing coefficient): the branch reads no member of an armed extension node and no member of
    /// an impersonating method that the engine of master does not read, and gives master's result (or master's exception).
    /// </summary>
    [TestMethod]
    public void HostileOperands_AreNeverReadBeyondMaster()
    {
        var extension = new ArmedExtension();
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        var callMethod = new ImpersonatingMethod(sin);
        var operatorMethod = new ImpersonatingMethod(typeof(ExpressionSimplifierFactoringSignSecurityTests).GetMethod(nameof(Times))!);
        Expression quotient = Expression.Divide(A, B);
        Expression call = Expression.Call(callMethod, X);
        Expression[] sources =
        [
            Expression.Negate(Expression.Subtract(extension, X)),
            Expression.Negate(Expression.Subtract(call, Y)),
            Expression.Negate(Expression.Multiply(K(2), Expression.Subtract(extension, Y))),
            Expression.Negate(Expression.Multiply(K(2), Expression.Subtract(X, Y), operatorMethod)),
            Expression.Subtract(quotient, Expression.Multiply(extension, Expression.Subtract(C, D))),
            Expression.Subtract(quotient, Expression.Multiply(call, Expression.Subtract(C, D))),
            Expression.Subtract(Expression.Multiply(K(-2), X), Expression.Multiply(K(-2), extension)),
            Expression.Subtract(Expression.Multiply(K(-2), X, operatorMethod), Expression.Multiply(K(-2), Y, operatorMethod)),
            Expression.Subtract(Expression.Multiply(K(-2), call), Expression.Multiply(K(-2), Y)),
            Expression.Add(Expression.Multiply(extension, Y), Expression.Multiply(Expression.Add(X, Expression.Add(A, B)), Y)),
            Expression.Add(Expression.Multiply(Y, extension), Expression.Multiply(Expression.Add(X, Expression.Add(A, extension)), extension)),
            Expression.Subtract(Expression.Negate(quotient), Expression.Subtract(extension, D)),
        ];

        foreach (Expression source in sources)
        {
            (Expression? reference, Type? referenceFailure, Expression? branch, Type? branchFailure) = (null, null, null, null);
            int masterExtensionCalls, masterCallReads, masterOperatorReads;
            extension.Armed = true;
            callMethod.Trap.Reset();
            callMethod.Trap.Armed = true;
            operatorMethod.Trap.Reset();
            operatorMethod.Trap.Armed = true;
            try
            {
                int extensionBefore = extension.Calls;
                (reference, referenceFailure) = Master(() => Run(source));
                masterExtensionCalls = extension.Calls - extensionBefore;
                masterCallReads = callMethod.Trap.ArmedCalls + callMethod.Trap.TypeReads;
                masterOperatorReads = operatorMethod.Trap.ArmedCalls + operatorMethod.Trap.TypeReads;
                callMethod.Trap.Reset();
                operatorMethod.Trap.Reset();
                extensionBefore = extension.Calls;
                (branch, branchFailure) = Run(source);
                Assert.AreEqual(masterExtensionCalls, extension.Calls - extensionBefore, "extension reads");
                Assert.AreEqual(masterCallReads, callMethod.Trap.ArmedCalls + callMethod.Trap.TypeReads, "call method reads");
                Assert.AreEqual(masterOperatorReads, operatorMethod.Trap.ArmedCalls + operatorMethod.Trap.TypeReads, "operator method reads");
            }
            finally
            {
                // An armed impersonating method must not stay in the framework's caches (see ExpressionFlattenResourceTests).
                callMethod.Trap.Armed = false;
                operatorMethod.Trap.Armed = false;
                extension.Armed = false;
            }

            Assert.AreEqual(referenceFailure, branchFailure, "exception");
            if (reference is not null) Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(reference, branch), "result");
        }

        static (Expression? Result, Type? Failure) Run(Expression source)
        {
            try
            {
                return (new ExpressionSimplifier().Simplify(source), null);
            }
            catch (Exception exception)
            {
                return (null, exception.GetType());
            }
        }
    }

    /// <summary>
    /// Checked, lifted and unsigned arithmetic is never oriented or distributed: <c>MultiplyChecked</c> pairs, <c>int?</c> pairs
    /// and negated sums, and <see cref="uint"/> sums keep master's result.
    /// </summary>
    [TestMethod]
    public void CheckedLiftedAndUnsignedArithmetic_KeepsMaster()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i"), j = Expression.Parameter(typeof(int), "j");
        ParameterExpression p = Expression.Parameter(typeof(int?), "p"), q = Expression.Parameter(typeof(int?), "q");
        ParameterExpression u = Expression.Parameter(typeof(uint), "u"), v = Expression.Parameter(typeof(uint), "v");
        Expression[] sources =
        [
            Expression.Subtract(Expression.MultiplyChecked(Expression.Constant(-2), i), Expression.MultiplyChecked(Expression.Constant(-2), j)),
            Expression.SubtractChecked(Expression.Multiply(Expression.Constant(-2), i), Expression.Multiply(Expression.Constant(-2), j)),
            Expression.NegateChecked(Expression.Subtract(i, j)),
            Expression.Subtract(Expression.Multiply(Expression.Constant(-2, typeof(int?)), p), Expression.Multiply(Expression.Constant(-2, typeof(int?)), q)),
            Expression.Negate(Expression.Subtract(p, q)),
            Expression.Subtract(Expression.Multiply(Expression.Constant(2u), u), Expression.Multiply(Expression.Constant(2u), v)),
            Expression.Negate(Expression.Subtract(Expression.Parameter(typeof(decimal), "m"), Expression.Parameter(typeof(decimal), "n"))),
        ];
        foreach (Expression source in sources)
        {
            Expression reference = Master(() => new ExpressionSimplifier().Simplify(source));
            Expression branch = new ExpressionSimplifier().Simplify(source);
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(reference, branch), $"{Text(source)}: {Text(reference)} against {Text(branch)}");
        }
    }

    /// <summary>Records the evaluations of the observable members.</summary>
    public static class Probe
    {
        [ThreadStatic]
        private static List<string>? t_log;

        /// <summary>Gets the evaluation log of the thread.</summary>
        public static List<string> Log => t_log ??= [];

        /// <summary>Gets an observable value (a getter with an effect).</summary>
        public static double Getter
        {
            get
            {
                Log.Add("get");
                return 3;
            }
        }

        /// <summary>An observable call.</summary>
        /// <param name="value">The value returned.</param>
        /// <returns><paramref name="value"/>.</returns>
        public static double Call(double value)
        {
            Log.Add("call " + value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return value;
        }

        /// <summary>A call that throws.</summary>
        /// <param name="value">Ignored.</param>
        /// <returns>Never.</returns>
        public static double Throw(double value)
        {
            Log.Add("throw");
            throw new InvalidOperationException("thrown");
        }
    }

    /// <summary>
    /// Observable evaluations (a getter with an effect, calls, a throwing call) inside a negated sum, a subtracted factored product,
    /// a binary factoring pair and a growing coefficient: under every policy, the simplified tree computes the same value, logs the
    /// same evaluations in the same order and throws the same exception as its source.
    /// </summary>
    [TestMethod]
    public void ObservableEvaluations_AreKeptUnderEveryPolicy()
    {
        Expression getter = Expression.Property(null, typeof(Probe).GetProperty(nameof(Probe.Getter))!);
        Expression Call(double value) => Expression.Call(typeof(Probe).GetMethod(nameof(Probe.Call))!, K(value));
        Expression thrower = Expression.Call(typeof(Probe).GetMethod(nameof(Probe.Throw))!, X);
        Expression[] bodies =
        [
            Expression.Negate(Expression.Subtract(getter, X)),
            Expression.Negate(Expression.Subtract(Call(1), Call(2))),
            Expression.Subtract(Expression.Negate(Expression.Divide(A, B)), Expression.Subtract(Expression.Multiply(K(-2), getter), Call(4))),
            Expression.Subtract(Expression.Divide(A, B), Expression.Multiply(getter, Expression.Subtract(C, D))),
            Expression.Subtract(Expression.Multiply(K(-2), Call(5)), Expression.Multiply(K(-2), Call(6))),
            Expression.Add(Expression.Multiply(Call(7), Y), Expression.Multiply(Expression.Add(X, Expression.Add(Call(8), B)), Y)),
            Expression.Negate(Expression.Multiply(K(2), Expression.Subtract(getter, thrower))),
            Expression.Subtract(Expression.Multiply(K(-2), thrower), Expression.Multiply(K(-2), getter)),
        ];
        ParameterExpression[] parameters = [A, B, C, D, X, Y];
        object[] arguments = [1.5, 2.0, 3.0, -4.0, 0.5, 7.0];
        foreach (ExpressionSimplifierOptions policy in Policies)
        {
            foreach (Expression body in bodies)
            {
                LambdaExpression source = Expression.Lambda(body, parameters);
                LambdaExpression simplified = (LambdaExpression)new ExpressionSimplifier { Options = policy }.Simplify(source);
                (object? sourceValue, string? sourceFailure, string sourceLog) = Evaluate(source, arguments);
                (object? value, string? failure, string log) = Evaluate(simplified, arguments);
                Assert.AreEqual(sourceFailure, failure, $"{policy}: {Text(body)} -> {Text(simplified)}");
                Assert.AreEqual(sourceLog, log, $"{policy}: {Text(body)} -> {Text(simplified)}");
                Assert.AreEqual(sourceValue, value, $"{policy}: {Text(body)} -> {Text(simplified)}");
            }
        }

        static (object? Value, string? Failure, string Log) Evaluate(LambdaExpression lambda, object[] arguments)
        {
            Probe.Log.Clear();
            try
            {
                object? value = lambda.Compile().DynamicInvoke(arguments);
                return (value, null, string.Join(",", Probe.Log));
            }
            catch (TargetInvocationException exception)
            {
                return (null, exception.InnerException!.GetType().Name, string.Join(",", Probe.Log));
            }
        }
    }

    /// <summary>
    /// The budgets: a long negated sum and a long subtracted factored product are refused cleanly just below their minimal
    /// structural budget, and the same instance then succeeds with the same result (no state left behind).
    /// </summary>
    [TestMethod]
    public void Budgets_RefuseCleanly()
    {
        Expression sum = X;
        for (int index = 0; index < 400; index++) sum = Expression.Subtract(sum, Expression.Parameter(typeof(double), "p" + index));
        Expression[] sources =
        [
            Expression.Negate(sum),
            Expression.Subtract(Expression.Divide(A, B), Expression.Multiply(K(2), sum)),
        ];
        foreach (Expression source in sources)
        {
            Expression expected = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(source);
            long low = 1, high = 1L << 24;
            while (low + 1 < high)
            {
                long middle = (low + high) / 2;
                try
                {
                    new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxStructuralVisits = middle }).Simplify(source);
                    high = middle;
                }
                catch (ExpressionResourceLimitException)
                {
                    low = middle;
                }
            }

            var refused = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxStructuralVisits = high - 1 });
            Assert.ThrowsExactly<ExpressionResourceLimitException>(() => refused.Simplify(source));
            var exact = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxStructuralVisits = high });
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, exact.Simplify(source)));
            Assert.IsTrue(ExpressionComparer.StructuralEqualsRaw(expected, exact.Simplify(source)));
        }
    }

    /// <summary>
    /// Deeply nested negations of differences, on a small stack: the orientation of each negated sum is a canonicalization of a
    /// level, guarded like every recursion of the engine, so the simplification either completes or refuses with the engine's
    /// exception, never a stack overflow.
    /// </summary>
    [TestMethod]
    public void NestedNegatedSums_AreGuardedOnASmallStack()
    {
        Expression source = X;
        for (int index = 0; index < 3000; index++) source = Expression.Negate(Expression.Subtract(source, Expression.Parameter(typeof(double), "p" + (index % 7))));
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(source);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, 256 * 1024);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(120)), "timeout");
        Assert.IsTrue(failure is null or ExpressionResourceLimitException, failure?.ToString());
    }
}
