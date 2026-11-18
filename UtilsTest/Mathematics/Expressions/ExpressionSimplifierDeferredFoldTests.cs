using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// S5A-04 (mission "deferred arithmetic exceptions"): arithmetic constant folding never raises an arithmetic exception itself. A
/// fold that would throw (<see cref="DivideByZeroException"/> for an integral or decimal zero divisor,
/// <see cref="OverflowException"/> for <c>MinValue / -1</c> and a decimal overflow) declines, and the node stays where it
/// stands, raising its exception when, and only if, it is evaluated. These tests cover the numeric types, nested positions,
/// every policy, the comparer and the semantic-security boundaries of the change (user code, effects, budgets, concurrency).
/// The lazy-context map and the root-level matrix live in <c>ExpressionSimplifierDeepAuditTests</c> and
/// <c>ExpressionSimplifierIeeeDivisionPowerTests</c>.
/// </summary>
[TestClass]
public class ExpressionSimplifierDeferredFoldTests
{
    /// <summary>An empty argument list.</summary>
    private static readonly object?[] NoArguments = [];

    /// <summary>The evaluation log of the logging helpers, per thread.</summary>
    [ThreadStatic]
    private static List<string>? _log;

    /// <summary>The number of calls of the custom operator methods.</summary>
    private static int _customCalls;

    /// <summary>A mutable field read by the expressions.</summary>
    private static int _field = 5;

    /// <summary>The evaluation log of the current thread.</summary>
    private static List<string> Log => _log ??= [];

    /// <summary>Logs <paramref name="value"/> and returns it.</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    private static int LogInt(int value)
    {
        Log.Add("int " + value);
        return value;
    }

    /// <summary>Logs <paramref name="value"/> and returns it.</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    private static decimal LogDecimal(decimal value)
    {
        Log.Add("decimal " + value);
        return value;
    }

    /// <summary>Logs a call and throws <see cref="InvalidOperationException"/>.</summary>
    /// <returns>Never returns.</returns>
    private static int ThrowingCall()
    {
        Log.Add("throwing call");
        throw new InvalidOperationException("user code");
    }

    /// <summary>A custom division operator that counts its calls and throws on a zero divisor with its own exception.</summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>The quotient.</returns>
    private static int CustomDivide(int left, int right)
    {
        Interlocked.Increment(ref _customCalls);
        return right == 0 ? throw new ArgumentException("custom divide") : left / right;
    }

    /// <summary>A custom conversion that counts its calls and throws.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Never returns.</returns>
    private static int CustomConvert(decimal value)
    {
        Interlocked.Increment(ref _customCalls);
        throw new OverflowException("custom conversion");
    }

    /// <summary>Gets a private static method of this class.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The method.</returns>
    private static MethodInfo Method(string name) => typeof(ExpressionSimplifierDeferredFoldTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Formats an expression with the engine's safe formatter.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string Text(Expression expression) => ExpressionTraceFormatter.Format(expression);

    /// <summary>Invokes a compiled lambda with a fresh log and describes its outcome and the log.</summary>
    /// <param name="function">The compiled lambda.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The outcome followed by the evaluation log.</returns>
    private static string Run(Delegate function, params object?[] arguments)
    {
        Log.Clear();
        string outcome = ExpressionSimplifierDeepAuditTests.Outcome(function, arguments);
        return outcome + " | " + string.Join(", ", Log);
    }

    /// <summary>
    /// Simplifies <paramref name="lambda"/> under every policy and checks that simplification completes, that the simplified tree
    /// gives the source's outcome and evaluation log at each argument list, and that a second pass leaves it unchanged.
    /// </summary>
    /// <param name="lambda">The source.</param>
    /// <param name="argumentLists">The argument lists.</param>
    /// <returns>The default policy's simplified lambda.</returns>
    private static LambdaExpression AssertDeferred(LambdaExpression lambda, params object?[][] argumentLists)
    {
        Delegate source = lambda.Compile();
        LambdaExpression? first = null;
        foreach (ExpressionSimplifierOptions options in ExpressionSimplifierDeepAuditTests.AllPolicies)
        {
            var simplifier = new ExpressionSimplifier { Options = options };
            string label = Text(lambda) + " under " + options;
            var simplified = (LambdaExpression)simplifier.Simplify(lambda);
            first ??= simplified;
            Delegate result = simplified.Compile();
            foreach (object?[] arguments in argumentLists)
            {
                Assert.AreEqual(Run(source, arguments), Run(result, arguments), label + " -> " + Text(simplified));
            }

            Assert.AreEqual(Text(simplified), Text(simplifier.Simplify(simplified)), label + ": a second pass changes the result");
        }

        return first!;
    }

    /// <summary>The throwing constant operations of each type, with the exception the compiled node raises.</summary>
    /// <returns>The type, the operation and the exception type name.</returns>
    private static IEnumerable<(Type Type, BinaryExpression Node, string Exception)> ThrowingFolds()
    {
        foreach ((Type type, object one) in new (Type, object)[] { (typeof(short), (short)1), (typeof(ushort), (ushort)1), (typeof(int), 1), (typeof(uint), 1u), (typeof(long), 1L), (typeof(ulong), 1ul), (typeof(decimal), 1m) })
        {
            yield return (type, Expression.Divide(Expression.Constant(one, type), Expression.Constant(Convert.ChangeType(0, type), type)), nameof(DivideByZeroException));
        }

        yield return (typeof(int), Expression.Divide(Expression.Constant(int.MinValue), Expression.Constant(-1)), nameof(OverflowException));
        yield return (typeof(long), Expression.Divide(Expression.Constant(long.MinValue), Expression.Constant(-1L)), nameof(OverflowException));
        yield return (typeof(decimal), Expression.Add(Expression.Constant(decimal.MaxValue), Expression.Constant(1m)), nameof(OverflowException));
        yield return (typeof(decimal), Expression.Subtract(Expression.Constant(decimal.MinValue), Expression.Constant(1m)), nameof(OverflowException));
        yield return (typeof(decimal), Expression.Multiply(Expression.Constant(decimal.MaxValue), Expression.Constant(2m)), nameof(OverflowException));
        yield return (typeof(decimal), Expression.Divide(Expression.Constant(decimal.MaxValue), Expression.Constant(0.5m)), nameof(OverflowException));
    }

    /// <summary>
    /// A throwing fold at the root raises nothing while simplifying, under every policy, and the kept node raises the source's
    /// exception at evaluation; <c>short.MinValue / -1</c> does not throw (the operation wraps in its type) and folds (control).
    /// </summary>
    [TestMethod]
    public void ThrowingFoldAtTheRoot_IsDeferredForEveryTypeAndPolicy()
    {
        foreach ((Type type, BinaryExpression node, string exception) in ThrowingFolds())
        {
            LambdaExpression lambda = Expression.Lambda(node);
            Assert.AreEqual("exception " + exception, ExpressionSimplifierDeepAuditTests.Outcome(lambda.Compile(), []), "Precondition: " + Text(node));
            LambdaExpression simplified = AssertDeferred(lambda, NoArguments);
            var kept = (BinaryExpression)simplified.Body;
            Assert.AreEqual(node.NodeType, kept.NodeType, type.Name);
            Assert.AreEqual(node.Method, kept.Method, type.Name);
            Assert.AreEqual(type, kept.Type, type.Name);
        }

        var wrap = (ConstantExpression)new ExpressionSimplifier().Simplify(Expression.Divide(Expression.Constant(short.MinValue), Expression.Constant((short)-1)));
        Assert.AreEqual(short.MinValue, wrap.Value);
    }

    /// <summary>
    /// A throwing fold nested in a larger expression stays in place: inside a sum, a product, a difference, a quotient and a
    /// factored sum, as an operand of an operator on a parameter, with the parameter on either side. The simplified tree gives
    /// the source's outcome at each point, and a second pass leaves it unchanged.
    /// </summary>
    [TestMethod]
    public void ThrowingFoldNestedInAnExpression_StaysWhereItStands()
    {
        foreach ((Type type, BinaryExpression node, string _) in ThrowingFolds())
        {
            ParameterExpression p = Expression.Parameter(type, "p");
            ParameterExpression b = Expression.Parameter(typeof(bool), "b");
            Expression two = Expression.Constant(Convert.ChangeType(2, type), type);
            Expression[] bodies =
            [
                Expression.Add(p, node),
                Expression.Add(node, p),
                Expression.Multiply(two, Expression.Add(p, node)),
                Expression.Subtract(Expression.Multiply(p, two), node),
                Expression.Divide(node, two),
                Expression.Add(Expression.Multiply(two, p), Expression.Multiply(node, p)),
                Expression.Multiply(Expression.Multiply(two, node), p),
            ];

            foreach (Expression body in bodies)
            {
                // Behind a condition, so the safe input skips the node and the other one evaluates it.
                LambdaExpression lambda = Expression.Lambda(Expression.Condition(b, body, p), b, p);
                object three = Convert.ChangeType(3, type);
                AssertDeferred(lambda, [false, three], [true, three]);
            }
        }
    }

    /// <summary>
    /// Successful folds are unchanged (controls): exact integral and decimal values with their scale, IEEE-754 division by
    /// positive and negative zero, <c>NaN</c> and infinities, signed-zero bit patterns.
    /// </summary>
    [TestMethod]
    public void SuccessfulFolds_KeepTheirExactValues()
    {
        static object? Fold(Expression node) => ((ConstantExpression)new ExpressionSimplifier().Simplify(node)).Value;

        Assert.AreEqual(3, Fold(Expression.Divide(Expression.Constant(7), Expression.Constant(2))));
        Assert.AreEqual(int.MaxValue, Fold(Expression.Divide(Expression.Constant(int.MinValue + 1), Expression.Constant(-1))));
        Assert.AreEqual(int.MinValue, Fold(Expression.Add(Expression.Constant(int.MaxValue), Expression.Constant(1))), "unchecked integral addition wraps");
        Assert.AreEqual(string.Join(",", decimal.GetBits(1.00m * 2m)), string.Join(",", decimal.GetBits((decimal)Fold(Expression.Multiply(Expression.Constant(1.00m), Expression.Constant(2m)))!)));
        Assert.AreEqual(1m / 3m, Fold(Expression.Divide(Expression.Constant(1m), Expression.Constant(3m))));
        Assert.AreEqual(decimal.MaxValue, Fold(Expression.Add(Expression.Constant(decimal.MaxValue - 1m), Expression.Constant(1m))));
        Assert.AreEqual(double.PositiveInfinity, Fold(Expression.Divide(Expression.Constant(1.0), Expression.Constant(0.0))));
        Assert.AreEqual(double.NegativeInfinity, Fold(Expression.Divide(Expression.Constant(1.0), Expression.Constant(-0.0))));
        Assert.IsTrue(double.IsNaN((double)Fold(Expression.Divide(Expression.Constant(0.0), Expression.Constant(0.0)))!));
        Assert.IsTrue(double.IsNaN((double)Fold(Expression.Subtract(Expression.Constant(double.PositiveInfinity), Expression.Constant(double.PositiveInfinity)))!));
        Assert.AreEqual(float.NegativeInfinity, Fold(Expression.Divide(Expression.Constant(-1f), Expression.Constant(0f))));
        Assert.AreEqual(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits((double)Fold(Expression.Multiply(Expression.Constant(-0.0), Expression.Constant(1.0)))!));
        Assert.AreEqual(BitConverter.SingleToInt32Bits(-0f), BitConverter.SingleToInt32Bits((float)Fold(Expression.Divide(Expression.Constant(0f), Expression.Constant(-3f)))!));
    }

    /// <summary>
    /// The comparer simplifies its operands first: it no longer throws on a throwing fold, identifies two identical throwing
    /// trees (reflexive and symmetric, with equal hashes, call after call), tells a throwing tree from a different one, and
    /// stays transitive on safe representatives; in a lambda and in a conditional branch alike.
    /// </summary>
    [TestMethod]
    public void Comparer_OnThrowingFolds_IsConsistent()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        ParameterExpression b = Expression.Parameter(typeof(bool), "b");
        Func<Expression>[] throwing =
        [
            () => Expression.Divide(Expression.Constant(1), Expression.Constant(0)),
            () => Expression.Divide(i, Expression.Constant(0)),
            () => Expression.Divide(Expression.Constant(int.MinValue), Expression.Constant(-1)),
            () => Expression.Convert(Expression.Multiply(Expression.Constant(decimal.MaxValue), Expression.Constant(2m)), typeof(int)),
        ];

        ExpressionComparer comparer = ExpressionComparer.Default;
        foreach (Func<Expression> make in throwing)
        {
            foreach (Func<Expression, Expression> wrap in new Func<Expression, Expression>[] { node => Expression.Lambda(node, i), node => Expression.Lambda(Expression.Condition(b, node, Expression.Constant(2)), b, i) })
            {
                Expression first = wrap(make()), second = wrap(make()), other = wrap(Expression.Add(i, Expression.Constant(1)));
                string label = Text(first);
                for (int call = 0; call < 3; call++)
                {
                    Assert.IsTrue(comparer.Equals(first, second), label);
                    Assert.IsTrue(comparer.Equals(second, first), label);
                    Assert.AreEqual(comparer.GetHashCode(first), comparer.GetHashCode(second), label);
                    Assert.IsFalse(comparer.Equals(first, other), label);
                    Assert.IsFalse(comparer.Equals(other, first), label);
                }
            }
        }

        // Transitivity on safe representatives: a + 0, 0 + a and a are one class.
        Expression a1 = Expression.Add(i, Expression.Constant(0)), a2 = Expression.Add(Expression.Constant(0), i);
        Assert.IsTrue(comparer.Equals(a1, a2) && comparer.Equals(a2, i) && comparer.Equals(a1, i));
    }

    /// <summary>
    /// The new handler never catches user code: a custom division operator and a custom conversion are never folded or called
    /// while simplifying, under every policy, and still raise their own exceptions at evaluation; a throwing call next to a
    /// throwing fold keeps the source's evaluation order (the call's exception comes first).
    /// </summary>
    [TestMethod]
    public void UserCode_IsNeverEvaluatedNorCaught()
    {
        Expression customDivision = Expression.Divide(Expression.Constant(1), Expression.Constant(0), Method(nameof(CustomDivide)));
        Expression customConversion = Expression.Convert(Expression.Constant(decimal.MaxValue), typeof(int), Method(nameof(CustomConvert)));
        foreach (ExpressionSimplifierOptions options in ExpressionSimplifierDeepAuditTests.AllPolicies)
        {
            _customCalls = 0;
            var division = (Expression<Func<int>>)new ExpressionSimplifier { Options = options }.Simplify(Expression.Lambda<Func<int>>(customDivision));
            var conversion = (Expression<Func<int>>)new ExpressionSimplifier { Options = options }.Simplify(Expression.Lambda<Func<int>>(customConversion));
            Assert.AreEqual(0, _customCalls, "no user code runs while simplifying, " + options);
            Assert.ThrowsExactly<ArgumentException>(() => division.Compile()(), options.ToString());
            Assert.ThrowsExactly<OverflowException>(() => conversion.Compile()(), options.ToString());
        }

        Expression callFirst = Expression.Add(Expression.Call(Method(nameof(ThrowingCall))), Expression.Divide(Expression.Constant(1), Expression.Constant(0)));
        Expression foldFirst = Expression.Add(Expression.Divide(Expression.Constant(1), Expression.Constant(0)), Expression.Call(Method(nameof(ThrowingCall))));
        AssertDeferred(Expression.Lambda<Func<int>>(callFirst), NoArguments);
        AssertDeferred(Expression.Lambda<Func<int>>(foldFirst), NoArguments);
        Assert.AreEqual("exception InvalidOperationException | throwing call", Run(Expression.Lambda<Func<int>>(callFirst).Compile()));
        Assert.AreEqual("exception DivideByZeroException | ", Run(Expression.Lambda<Func<int>>(foldFirst).Compile()));
    }

    /// <summary>
    /// Effectful operands around a throwing fold keep their evaluations, their order and the point where the exception is
    /// raised; a mutable field read too. The P18 default-policy contract is unchanged for the effectful products by zero:
    /// <c>0 * (4 + F())</c> and <c>0 * (4 * F())</c> still call <c>F</c> once.
    /// </summary>
    [TestMethod]
    public void EffectfulOperands_KeepTheirEvaluations()
    {
        ParameterExpression b = Expression.Parameter(typeof(bool), "b");
        MethodInfo logInt = Method(nameof(LogInt)), logDecimal = Method(nameof(LogDecimal));
        Expression intFold = Expression.Divide(Expression.Call(logInt, Expression.Constant(2)), Expression.Constant(0));
        Expression[] intBodies =
        [
            Expression.Add(Expression.Call(logInt, Expression.Constant(1)), intFold),
            Expression.Add(Expression.Call(logInt, Expression.Constant(1)), Expression.Divide(Expression.Constant(int.MinValue), Expression.Constant(-1))),
            Expression.Multiply(Expression.Add(Expression.Field(null, typeof(ExpressionSimplifierDeferredFoldTests).GetField(nameof(_field), BindingFlags.NonPublic | BindingFlags.Static)!), Expression.Constant(1)), Expression.Divide(Expression.Constant(1), Expression.Constant(0))),
            Expression.Add(Expression.Divide(Expression.Constant(1), Expression.Constant(0)), Expression.Call(logInt, Expression.Constant(3))),
        ];

        foreach (Expression body in intBodies)
        {
            AssertDeferred(Expression.Lambda<Func<bool, int>>(Expression.Condition(b, body, Expression.Call(logInt, Expression.Constant(9))), b), [false], [true]);
        }

        Expression decimalBody = Expression.Add(Expression.Call(logDecimal, Expression.Constant(1m)), Expression.Multiply(Expression.Constant(decimal.MaxValue), Expression.Constant(2m)));
        AssertDeferred(Expression.Lambda<Func<bool, decimal>>(Expression.Condition(b, decimalBody, Expression.Constant(0m)), b), [false], [true]);

        // P18 default policy: the call of an effectful operand of a product by zero is kept.
        foreach (Expression product in new Expression[] { Expression.Multiply(Expression.Constant(0), Expression.Add(Expression.Constant(4), Expression.Call(logInt, Expression.Constant(7)))), Expression.Multiply(Expression.Constant(0), Expression.Multiply(Expression.Constant(4), Expression.Call(logInt, Expression.Constant(7)))) })
        {
            var simplified = (Expression<Func<int>>)new ExpressionSimplifier().Simplify(Expression.Lambda<Func<int>>(product));
            Assert.AreEqual("value 0 | int 7", Run(simplified.Compile()), Text(product));
        }
    }

    /// <summary>
    /// Derived simplifiers inherit the decline, and the protected folding rules, called directly as a derived simplifier may,
    /// decline instead of throwing. (That a resource-budget refusal still propagates on a tree holding a throwing fold is a
    /// resource-exhaustion invariant, checked in <c>UtilsTest.Security</c>, <c>ExpressionSimplifierDeferredFoldResourceTests</c>.)
    /// </summary>
    [TestMethod]
    public void DerivedSimplifiersAndDirectRuleCalls_Decline()
    {
        var derived = new DerivedSimplifier();
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        LambdaExpression lambda = Expression.Lambda(Expression.Condition(Expression.GreaterThan(i, Expression.Constant(0)), Expression.Divide(Expression.Constant(1), Expression.Constant(0)), Expression.Constant(2)), i);
        var simplified = (LambdaExpression)derived.Simplify(lambda);
        Assert.AreEqual("value 2", ExpressionSimplifierDeepAuditTests.Outcome(simplified.Compile(), [-1]));
        Assert.AreEqual("exception DivideByZeroException", ExpressionSimplifierDeepAuditTests.Outcome(simplified.Compile(), [1]));

        ConstantExpression one = Expression.Constant(1), zero = Expression.Constant(0), max = Expression.Constant(decimal.MaxValue), two = Expression.Constant(2m);
        Assert.IsNull(derived.Divide(Expression.Divide(one, zero), one, zero));
        Assert.IsNull(derived.Add(Expression.Add(max, max), max, max));
        Assert.IsNull(derived.Multiply(Expression.Multiply(max, two), max, two));
        Assert.IsNull(derived.Subtract(Expression.Subtract(Expression.Constant(decimal.MinValue), two), Expression.Constant(decimal.MinValue), two));
        Assert.IsNotNull(derived.Divide(Expression.Divide(Expression.Constant(6), Expression.Constant(3)), Expression.Constant(6), Expression.Constant(3)), "a successful fold still folds");
    }

    /// <summary>
    /// Concurrent and reentrant simplifications of throwing folds all complete with the same result. (The limited-stack case
    /// is a resource-exhaustion invariant, checked in <c>UtilsTest.Security</c>.)
    /// </summary>
    [TestMethod]
    public void ConcurrentAndReentrantSimplifications_Complete()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        Expression Build() => Expression.Lambda(Expression.Condition(Expression.GreaterThan(i, Expression.Constant(0)), Expression.Add(i, Expression.Divide(Expression.Constant(int.MinValue), Expression.Constant(-1))), Expression.Constant(2)), i);
        string expected = Text(new ExpressionSimplifier().Simplify(Build()));

        string[] results = new string[64];
        Parallel.For(0, results.Length, index => results[index] = Text(new ExpressionSimplifier().Simplify(Build())));
        CollectionAssert.AreEqual(Enumerable.Repeat(expected, results.Length).ToArray(), results);

        // Reentrancy: a simplification started from inside a rule of another one.
        var reentrant = new ReentrantSimplifier(Build);
        Assert.AreEqual(ExpressionType.Add, reentrant.Simplify(Expression.Add(i, Expression.Constant(5))).NodeType);
        Assert.AreEqual(expected, reentrant.Inner);
    }

    /// <summary>A derived simplifier exposing the protected constant-folding rules.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
        /// <summary>Calls <c>DivisionOfConstants</c>.</summary>
        /// <param name="e">The node.</param>
        /// <param name="left">The left constant.</param>
        /// <param name="right">The right constant.</param>
        /// <returns>The rule's result.</returns>
        public Expression Divide(BinaryExpression e, ConstantExpression left, ConstantExpression right) => DivisionOfConstants(e, left, right);

        /// <summary>Calls <c>AdditionOfConstants</c>.</summary>
        /// <param name="e">The node.</param>
        /// <param name="left">The left constant.</param>
        /// <param name="right">The right constant.</param>
        /// <returns>The rule's result.</returns>
        public Expression Add(BinaryExpression e, ConstantExpression left, ConstantExpression right) => AdditionOfConstants(e, left, right);

        /// <summary>Calls <c>MultiplicationOfConstants</c>.</summary>
        /// <param name="e">The node.</param>
        /// <param name="left">The left constant.</param>
        /// <param name="right">The right constant.</param>
        /// <returns>The rule's result.</returns>
        public Expression Multiply(BinaryExpression e, ConstantExpression left, ConstantExpression right) => MultiplicationOfConstants(e, left, right);

        /// <summary>Calls <c>SubstractionOfConstants</c>.</summary>
        /// <param name="e">The node.</param>
        /// <param name="left">The left constant.</param>
        /// <param name="right">The right constant.</param>
        /// <returns>The rule's result.</returns>
        public Expression Subtract(BinaryExpression e, ConstantExpression left, ConstantExpression right) => SubstractionOfConstants(e, left, right);
    }

    /// <summary>A derived simplifier whose rule on additions simplifies another expression with a new simplifier.</summary>
    /// <param name="build">Builds the expression simplified from inside the rule.</param>
    private sealed class ReentrantSimplifier(Func<Expression> build) : ExpressionSimplifier
    {
        /// <summary>Gets the text of the inner simplification's result.</summary>
        public string? Inner { get; private set; }

        /// <summary>Simplifies the inner expression once, then declines.</summary>
        /// <param name="e">The addition.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression? Reenter(BinaryExpression e, ParameterExpression left, ConstantExpression right)
        {
            Inner ??= Text(new ExpressionSimplifier().Simplify(build()));
            return null;
        }
    }
}
