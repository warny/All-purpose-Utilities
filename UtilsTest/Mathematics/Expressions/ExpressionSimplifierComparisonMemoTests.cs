using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Validates the S5 P9 selective strict structural memo of comparer-triggered simplifications: strict key
/// identity, eligibility, reuse, re-entrancy, exception handling, session lifetime and isolation.
/// </summary>
[TestClass]
public class ExpressionSimplifierComparisonMemoTests
{
    /// <summary>Returns the sum of two values for a custom-operator expression.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The sum.</returns>
    public static double CustomAdd(double left, double right) => left + right;

    /// <summary>Returns a constant for an opaque argument, embedding user objects in numeric terms.</summary>
    /// <param name="value">An opaque value, ignored.</param>
    /// <returns>Always one.</returns>
    public static double IdentityFromObject(object value) => 1.0;

    /// <summary>A constant value whose <see cref="Equals(object?)"/> counts its invocations and returns <see langword="false"/>.</summary>
    private sealed class CountingConstant
    {
        /// <summary>Gets the number of <see cref="Equals(object?)"/> invocations.</summary>
        public int EqualsCallCount { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            EqualsCallCount++;
            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>Marker exception thrown by <see cref="HostileConstant"/>.</summary>
    private sealed class HostileConstantException : Exception { }

    /// <summary>A constant value whose <see cref="Equals(object?)"/> always throws.</summary>
    private sealed class HostileConstant
    {
        /// <inheritdoc/>
        public override bool Equals(object? obj) => throw new HostileConstantException();

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>A constant value whose <see cref="Equals(object?)"/> runs a callback, then returns <see langword="false"/>.</summary>
    private sealed class CallbackConstant
    {
        /// <summary>The callback run by <see cref="Equals(object?)"/>.</summary>
        private readonly Action _callback;

        /// <summary>Initializes the constant.</summary>
        /// <param name="callback">The callback run by <see cref="Equals(object?)"/>.</param>
        public CallbackConstant(Action callback) => _callback = callback;

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            _callback();
            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }

    /// <summary>Asserts that the current thread holds no comparison-memo state.</summary>
    private static void AssertNoMemoState()
    {
        Assert.AreEqual(0, ExpressionSimplifier.ComparisonMemoDepth);
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoActive);
        Assert.IsFalse(ExpressionSimplifier.HasComparisonMemo);
    }

    /// <summary>Builds coefficient terms 2, 3, 4, ... whose coefficient/parameter order alternates by parity.</summary>
    /// <param name="parameters">The free parameters.</param>
    /// <returns>The terms.</returns>
    private static Expression[] AlternatingTerms(ParameterExpression[] parameters)
        => parameters.Select((x, i) => i % 2 == 0
            ? (Expression)Expression.Multiply(Expression.Constant((double)(i + 2)), x)
            : Expression.Multiply(x, Expression.Constant((double)(i + 2)))).ToArray();

    /// <summary>Folds <paramref name="items"/> into a left-associated chain.</summary>
    /// <param name="items">The chain items.</param>
    /// <param name="operation">The binary factory.</param>
    /// <returns>The left-associated chain.</returns>
    private static Expression LeftChain(Expression[] items, Func<Expression, Expression, BinaryExpression> operation)
    {
        Expression accumulator = items[0];
        for (int i = 1; i < items.Length; i++) accumulator = operation(accumulator, items[i]);
        return accumulator;
    }

    /// <summary>Creates <paramref name="count"/> distinct free double parameters.</summary>
    /// <param name="count">The number of parameters.</param>
    /// <returns>The parameters <c>x0</c>, <c>x1</c>, ...</returns>
    private static ParameterExpression[] Parameters(int count)
        => Enumerable.Range(0, count).Select(i => Expression.Parameter(typeof(double), "x" + i)).ToArray();

    // ------------------------------------------------------------------------------------------
    // Strict key identity
    // ------------------------------------------------------------------------------------------

    /// <summary>Rebuilt, structurally identical trees share a key, and a session reuses the completed result.</summary>
    [TestMethod]
    public void RebuiltIdenticalTrees_ShareKeyAndReuseResult()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression first = Expression.Add(Expression.Multiply(Expression.Constant(2.0), x), Expression.Constant(1.0));
        Expression rebuilt = Expression.Add(Expression.Multiply(Expression.Constant(2.0), x), Expression.Constant(1.0));
        Assert.IsTrue(ExpressionSimplifier.ComparisonMemoKeysEqual(first, rebuilt));

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Expression firstResult = ExpressionComparer.SimplifyForComparison(first);
            int entries = ExpressionSimplifier.ComparisonMemoEntryCount;
            Expression secondResult = ExpressionComparer.SimplifyForComparison(rebuilt);

            Assert.AreSame(firstResult, secondResult, "A strictly identical rebuilt tree must reuse the completed result.");
            Assert.IsTrue(entries >= 1);
            Assert.AreEqual(entries, ExpressionSimplifier.ComparisonMemoEntryCount, "A memo hit must not add entries.");
        }

        AssertNoMemoState();
    }

    /// <summary>Distinct parameters sharing a name never share a key or a result.</summary>
    [TestMethod]
    public void DistinctParametersWithSameName_DoNotShareKey()
    {
        ParameterExpression first = Expression.Parameter(typeof(double), "x");
        ParameterExpression second = Expression.Parameter(typeof(double), "x");
        Expression left = Expression.Add(first, Expression.Constant(1.0));
        Expression right = Expression.Add(second, Expression.Constant(1.0));
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(left, right));

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Expression leftResult = ExpressionComparer.SimplifyForComparison(left);
            Expression rightResult = ExpressionComparer.SimplifyForComparison(right);

            Assert.AreNotSame(leftResult, rightResult);
            Assert.AreEqual(2, ExpressionSimplifier.ComparisonMemoEntryCount);
            Assert.IsTrue(ExpressionComparer.Default.Equals(right, rightResult));
        }
    }

    /// <summary>Operand orientation is part of the key even for commutative ordinary arithmetic.</summary>
    [TestMethod]
    public void OperandOrientation_IsPartOfKey()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");

        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Add(a, b), Expression.Add(b, a)));
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Multiply(a, b), Expression.Multiply(b, a)));
        Assert.IsTrue(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Add(a, b), Expression.Add(a, b)));
    }

    /// <summary>Binary association is part of the key.</summary>
    [TestMethod]
    public void BinaryAssociation_IsPartOfKey()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression c = Expression.Parameter(typeof(double), "c");

        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(
            Expression.Add(Expression.Add(a, b), c),
            Expression.Add(a, Expression.Add(b, c))));
    }

    /// <summary>Node type, static type and constant type are part of the key.</summary>
    [TestMethod]
    public void NodeTypeAndStaticType_ArePartOfKey()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression i = Expression.Parameter(typeof(int), "i");

        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Add(x, x), Expression.Subtract(x, x)));
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Constant(1.0), Expression.Constant(1.0f)));
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Constant(1), Expression.Constant(1L)));
        Assert.IsTrue(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Add(i, Expression.Constant(1)), Expression.Add(i, Expression.Constant(1))));
    }

    /// <summary>
    /// IEEE-754 signed zeros are distinct keys, and a session returns each sign's own simplification (P6 reused
    /// the signed-zero-normalizing exact numeric value and could return one sign's result for the other).
    /// </summary>
    [TestMethod]
    public void SignedZeros_AreDistinctKeys_AndKeepTheirOwnResult()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression positive = Expression.Multiply(Expression.Constant(0.0), x);
        Expression negative = Expression.Multiply(Expression.Constant(-0.0), x);
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(positive, negative));
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Constant(0.0f), Expression.Constant(-0.0f)));

        double expectedPositive = Evaluate(new ExpressionSimplifier().Simplify(positive), x);
        double expectedNegative = Evaluate(new ExpressionSimplifier().Simplify(negative), x);
        Assert.IsTrue(double.IsPositive(expectedPositive) && expectedPositive == 0.0);
        Assert.IsTrue(double.IsNegative(expectedNegative) && expectedNegative == 0.0);

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            double positiveResult = Evaluate(ExpressionComparer.SimplifyForComparison(positive), x);
            double negativeResult = Evaluate(ExpressionComparer.SimplifyForComparison(negative), x);

            Assert.AreEqual(BitConverter.DoubleToInt64Bits(expectedPositive), BitConverter.DoubleToInt64Bits(positiveResult));
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(expectedNegative), BitConverter.DoubleToInt64Bits(negativeResult));
            Assert.AreEqual(2, ExpressionSimplifier.ComparisonMemoEntryCount);
        }
    }

    /// <summary>NaN keys follow the exact bit representation: identical payloads match, distinct payloads do not.</summary>
    [TestMethod]
    public void NaNRepresentations_FollowExactBits()
    {
        double quietNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000000);
        double payloadNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000001);
        Assert.IsTrue(double.IsNaN(quietNaN) && double.IsNaN(payloadNaN));

        Assert.IsTrue(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Constant(quietNaN), Expression.Constant(quietNaN)));
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Constant(quietNaN), Expression.Constant(payloadNaN)));

        float singleNaN = BitConverter.Int32BitsToSingle(0x7FC00000);
        float singlePayloadNaN = BitConverter.Int32BitsToSingle(0x7FC00001);
        Assert.IsTrue(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Constant(singleNaN), Expression.Constant(singleNaN)));
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Constant(singleNaN), Expression.Constant(singlePayloadNaN)));
    }

    /// <summary>Decimal scale is representation-significant and therefore part of the key.</summary>
    [TestMethod]
    public void DecimalScale_IsPartOfKey()
    {
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Constant(1.0m), Expression.Constant(1.00m)));
        Assert.IsTrue(ExpressionSimplifier.ComparisonMemoKeysEqual(Expression.Constant(1.50m), Expression.Constant(1.50m)));
    }

    // ------------------------------------------------------------------------------------------
    // Eligibility
    // ------------------------------------------------------------------------------------------

    /// <summary>Custom operator metadata makes an operand ineligible, so it is never memoized.</summary>
    [TestMethod]
    public void CustomOperator_IsIneligible()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo method = typeof(ExpressionSimplifierComparisonMemoTests).GetMethod(nameof(CustomAdd))!;
        Expression custom = Expression.Add(x, Expression.Constant(1.0), method);
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(custom));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Multiply(Expression.Constant(2.0), custom)));

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Expression first = ExpressionComparer.SimplifyForComparison(custom);
            Expression second = ExpressionComparer.SimplifyForComparison(custom);

            Assert.AreNotSame(first, second);
            Assert.AreEqual(0, ExpressionSimplifier.ComparisonMemoEntryCount);
        }
    }

    /// <summary>Lifted nullable arithmetic is ineligible.</summary>
    [TestMethod]
    public void LiftedNullableArithmetic_IsIneligible()
    {
        ParameterExpression x = Expression.Parameter(typeof(double?), "x");
        Expression lifted = Expression.Add(x, Expression.Constant(1.0, typeof(double?)));
        Assert.IsTrue(((BinaryExpression)lifted).IsLifted);
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(lifted));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Negate(x)));
    }

    /// <summary>Unsupported node kinds, conversions, checked arithmetic and opaque constants are ineligible.</summary>
    [TestMethod]
    public void UnsupportedNodes_AreIneligible()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin))!;
        MethodInfo identity = typeof(ExpressionSimplifierComparisonMemoTests).GetMethod(nameof(IdentityFromObject))!;

        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Call(sin, x)));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Add(Expression.Call(sin, x), x)));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Condition(Expression.Constant(true), x, x)));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Property(Expression.Constant("text"), nameof(string.Length))));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Convert(x, typeof(float))));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.AddChecked(Expression.Parameter(typeof(int), "i"), Expression.Constant(1))));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Call(identity, Expression.Constant(new CountingConstant(), typeof(object)))));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Constant(1.0, typeof(object))));
        Assert.IsFalse(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Lambda(Expression.Add(x, x), x)));
        Assert.IsTrue(ExpressionSimplifier.IsComparisonMemoEligible(Expression.Negate(Expression.Power(x, Expression.Constant(2.0)))));
    }

    // ------------------------------------------------------------------------------------------
    // Lambda and lexical-scope isolation
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Lambdas are never memoized, alpha-equivalence comparisons keep their result inside an activated session,
    /// and a body operand over bound parameters simplifies exactly as outside the session.
    /// </summary>
    [TestMethod]
    public void LambdasAndBoundParameters_KeepComparerSemantics()
    {
        ParameterExpression p = Expression.Parameter(typeof(double), "p");
        ParameterExpression q = Expression.Parameter(typeof(double), "q");
        LambdaExpression first = Expression.Lambda(Expression.Add(Expression.Multiply(Expression.Constant(2.0), p), q), p, q);
        LambdaExpression swapped = Expression.Lambda(Expression.Add(Expression.Multiply(Expression.Constant(2.0), q), p), q, p);
        LambdaExpression different = Expression.Lambda(Expression.Add(Expression.Multiply(Expression.Constant(2.0), q), p), p, q);
        bool expectedSwapped = ExpressionComparer.Default.Equals(first, swapped);
        bool expectedDifferent = ExpressionComparer.Default.Equals(first, different);
        string expectedBody = new ExpressionSimplifier().Simplify(first.Body).ToString();

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Assert.AreEqual(expectedSwapped, ExpressionComparer.Default.Equals(first, swapped));
            Assert.AreEqual(expectedDifferent, ExpressionComparer.Default.Equals(first, different));
            Assert.AreEqual(expectedBody, ExpressionComparer.SimplifyForComparison(first.Body).ToString());
            Assert.AreEqual(expectedBody, ExpressionComparer.SimplifyForComparison(first.Body).ToString());
        }

        Assert.IsTrue(expectedSwapped);
        Assert.IsFalse(expectedDifferent);
    }

    // ------------------------------------------------------------------------------------------
    // Nested factoring, re-entrancy and exceptions
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Re-entrant population: an outer miss starts with an empty, unallocated memo; nested factoring comparisons
    /// allocate and populate it before the outer simplification returns. The outer insertion must re-read the
    /// shared state (P6 dereferenced a stale null dictionary here) and its result must then be reusable.
    /// </summary>
    [TestMethod]
    public void ReentrantPopulation_DuringOuterMiss_InsertsSafely()
    {
        Expression source = LeftChain(AlternatingTerms(Parameters(6)), Expression.Subtract);
        string expected = new ExpressionSimplifier().Simplify(source).ToString();

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Assert.IsFalse(ExpressionSimplifier.HasComparisonMemo);
            Expression result = ExpressionComparer.SimplifyForComparison(source);

            Assert.IsTrue(ExpressionSimplifier.ComparisonMemoEntryCount > 1, "Nested comparisons must have populated the memo.");
            Assert.AreEqual(expected, result.ToString());
            Assert.AreSame(result, ExpressionComparer.SimplifyForComparison(source));
            Assert.AreEqual(1, ExpressionSimplifier.ComparisonMemoDepth);
        }

        AssertNoMemoState();
    }

    /// <summary>Exceptions thrown while simplifying an eligible operand are never cached and propagate every time.</summary>
    /// <remarks>
    /// The throwing operand is an integral division by zero, which throws at runtime as well; until S5 P12 it was
    /// <c>x / 0.0</c>, which now simplifies without throwing (IEEE-754 division by zero is defined).
    /// </remarks>
    [TestMethod]
    public void Exceptions_AreNeverCached()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        Expression throwing = Expression.Divide(i, Expression.Constant(0));
        Assert.IsTrue(ExpressionSimplifier.IsComparisonMemoEligible(throwing));

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            // Rule exceptions surface wrapped by the reflective rule invocation, exactly as without the memo.
            AssertThrowsRuleException<DivideByZeroException>(() => ExpressionComparer.SimplifyForComparison(throwing));
            AssertThrowsRuleException<DivideByZeroException>(() => ExpressionComparer.SimplifyForComparison(throwing));
            Assert.AreEqual(0, ExpressionSimplifier.ComparisonMemoEntryCount);
            Assert.AreEqual(1, ExpressionSimplifier.ComparisonMemoDepth, "The session depth must be restored after a thrown simplification.");
        }

        AssertNoMemoState();
    }

    /// <summary>The comparer keeps its x-before-y simplification and exception ordering inside an activated session.</summary>
    /// <remarks>
    /// The second operand throws through an integral division by zero, converted to <see cref="double"/> so both
    /// operands keep the same type; until S5 P12 it was <c>x / 0.0</c>, which now simplifies without throwing.
    /// </remarks>
    [TestMethod]
    public void ComparerExceptionOrder_IsPreservedInSession()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        MethodInfo identity = typeof(ExpressionSimplifierComparisonMemoTests).GetMethod(nameof(IdentityFromObject))!;
        Expression hostile = Expression.Add(
            Expression.Multiply(Expression.Constant(2.0), Expression.Call(identity, Expression.Constant(new HostileConstant(), typeof(object)))),
            Expression.Multiply(Expression.Constant(3.0), Expression.Call(identity, Expression.Constant(new HostileConstant(), typeof(object)))));
        Expression dividesByZero = Expression.Convert(Expression.Divide(i, Expression.Constant(0)), typeof(double));

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            AssertThrowsRuleException<HostileConstantException>(() => ExpressionComparer.Default.Equals(hostile, dividesByZero));
            AssertThrowsRuleException<DivideByZeroException>(() => ExpressionComparer.Default.Equals(dividesByZero, hostile));
        }

        AssertNoMemoState();
    }

    /// <summary>
    /// A natural (production-signal) activation on a deep near-miss chain containing user-code constants keeps
    /// the user code's invocation count of the unmemoized baseline: operands that can reach user code are never
    /// eligible, so memo hits only ever skip user-code-free work. The expected count was measured on the
    /// pre-P9 baseline commit <c>133861c5</c> with the same expression.
    /// </summary>
    [TestMethod]
    public void NaturalActivation_UserCodeInvocationCount_MatchesBaseline()
    {
        MethodInfo identity = typeof(ExpressionSimplifierComparisonMemoTests).GetMethod(nameof(IdentityFromObject))!;
        var c0 = new CountingConstant();
        var c1 = new CountingConstant();
        ParameterExpression[] parameters = Parameters(12);
        Expression[] terms = AlternatingTerms(parameters);
        terms[0] = Expression.Multiply(Expression.Constant(2.0), Expression.Call(identity, Expression.Constant(c0, typeof(object))));
        terms[1] = Expression.Multiply(Expression.Constant(3.0), Expression.Call(identity, Expression.Constant(c1, typeof(object))));

        new ExpressionSimplifier().Simplify(LeftChain(terms, Expression.Subtract));

        Assert.AreEqual(BaselineSpyB12C0, c0.EqualsCallCount);
        Assert.AreEqual(BaselineSpyB12C1, c1.EqualsCallCount);
        AssertNoMemoState();
    }

    /// <summary>A user-code exception thrown inside a naturally activated deep session propagates and releases the session.</summary>
    [TestMethod]
    public void NaturalActivation_UserCodeException_ReleasesSession()
    {
        MethodInfo identity = typeof(ExpressionSimplifierComparisonMemoTests).GetMethod(nameof(IdentityFromObject))!;
        ParameterExpression[] parameters = Parameters(12);
        Expression[] terms = AlternatingTerms(parameters);
        terms[0] = Expression.Multiply(Expression.Constant(2.0), Expression.Call(identity, Expression.Constant(new HostileConstant(), typeof(object))));
        terms[1] = Expression.Multiply(Expression.Constant(3.0), Expression.Call(identity, Expression.Constant(new HostileConstant(), typeof(object))));

        AssertThrowsRuleException<HostileConstantException>(() => new ExpressionSimplifier().Simplify(LeftChain(terms, Expression.Subtract)));
        AssertNoMemoState();
    }

    // ------------------------------------------------------------------------------------------
    // End-to-end results, lifetime and inactive path
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Deep additive, subtractive and multiplicative chains (naturally activating the memo through nested
    /// factoring comparisons) keep their exact baseline results, evaluate like their sources, and release all
    /// session state afterwards.
    /// </summary>
    [TestMethod]
    public void NestedFactoringChains_KeepBaselineResults_AndReleaseState()
    {
        ParameterExpression[] parameters = Parameters(12);
        Expression additive = LeftChain(AlternatingTerms(parameters), Expression.Add);
        Expression subtractive = LeftChain(AlternatingTerms(parameters), Expression.Subtract);
        Expression multiplicative = LeftChain(parameters.Cast<Expression>().ToArray(), Expression.Multiply);

        Expression additiveResult = new ExpressionSimplifier().Simplify(additive);
        AssertNoMemoState();
        Expression subtractiveResult = new ExpressionSimplifier().Simplify(subtractive);
        AssertNoMemoState();
        Expression multiplicativeResult = new ExpressionSimplifier().Simplify(multiplicative);
        AssertNoMemoState();

        Assert.AreEqual(BaselineA12, additiveResult.ToString());
        Assert.AreEqual(BaselineG12, multiplicativeResult.ToString());

        double[] values = Enumerable.Range(0, parameters.Length).Select(i => 0.5 + i * 0.75).ToArray();
        Assert.AreEqual(Evaluate(additive, parameters, values), Evaluate(additiveResult, parameters, values), 1e-9);
        Assert.AreEqual(Evaluate(subtractive, parameters, values), Evaluate(subtractiveResult, parameters, values), 1e-9);
        Assert.AreEqual(Evaluate(multiplicative, parameters, values), Evaluate(multiplicativeResult, parameters, values), 1e-6);
    }

    /// <summary>Without an activated session, small comparer simplifications never allocate or consult a memo.</summary>
    [TestMethod]
    public void InactivePath_NeverAllocatesOrReuses()
    {
        AssertNoMemoState();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression operand = Expression.Add(Expression.Multiply(Expression.Constant(2.0), x), Expression.Constant(1.0));

        Expression first = ExpressionComparer.SimplifyForComparison(operand);
        Expression second = ExpressionComparer.SimplifyForComparison(operand);

        Assert.AreNotSame(first, second, "No memo may be consulted outside an activated session.");
        AssertNoMemoState();

        Expression small = LeftChain(AlternatingTerms(Parameters(4)), Expression.Subtract);
        new ExpressionSimplifier().Simplify(small);
        AssertNoMemoState();
    }

    /// <summary>
    /// An independent public simplification started from user code while a session is active neither sees nor
    /// populates the caller's memo, and the caller's session is restored intact afterwards.
    /// </summary>
    [TestMethod]
    public void IndependentPublicCall_FromUserCode_IsDetachedFromCallerSession()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression cached = Expression.Add(Expression.Multiply(Expression.Constant(2.0), x), Expression.Constant(1.0));
        MethodInfo identity = typeof(ExpressionSimplifierComparisonMemoTests).GetMethod(nameof(IdentityFromObject))!;
        int observedDepth = -1;
        bool observedActive = true;
        bool observedMemo = true;
        int callbacks = 0;
        var spy = new CallbackConstant(() =>
        {
            callbacks++;
            observedDepth = ExpressionSimplifier.ComparisonMemoDepth;
            observedActive = ExpressionSimplifier.IsComparisonMemoActive;
            observedMemo = ExpressionSimplifier.HasComparisonMemo;
        });
        Expression independent = Expression.Add(
            Expression.Multiply(Expression.Constant(2.0), Expression.Call(identity, Expression.Constant(spy, typeof(object)))),
            Expression.Multiply(Expression.Constant(3.0), Expression.Call(identity, Expression.Constant(new CountingConstant(), typeof(object)))));

        using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
        {
            Expression cachedResult = ExpressionComparer.SimplifyForComparison(cached);
            int entries = ExpressionSimplifier.ComparisonMemoEntryCount;
            Assert.IsTrue(entries >= 1);

            new ExpressionSimplifier().Simplify(independent);

            Assert.IsTrue(callbacks > 0, "The spy must have observed the independent call.");
            Assert.AreEqual(0, observedDepth);
            Assert.IsFalse(observedActive);
            Assert.IsFalse(observedMemo);
            Assert.AreEqual(1, ExpressionSimplifier.ComparisonMemoDepth);
            Assert.IsTrue(ExpressionSimplifier.IsComparisonMemoActive);
            Assert.AreEqual(entries, ExpressionSimplifier.ComparisonMemoEntryCount);
            Assert.AreSame(cachedResult, ExpressionComparer.SimplifyForComparison(cached));
        }

        AssertNoMemoState();
    }

    /// <summary>
    /// Two genuinely overlapping sessions on distinct threads never share entries: each thread reuses only its
    /// own results while both sessions are open. All waits are bounded.
    /// </summary>
    [TestMethod]
    public void ConcurrentSessions_OnDistinctThreads_AreIsolated()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Func<Expression> build = () => Expression.Add(Expression.Multiply(Expression.Constant(2.0), x), Expression.Constant(1.0));
        TimeSpan timeout = TimeSpan.FromSeconds(30);
        using var barrier = new Barrier(2);
        var results = new Expression?[2, 2];
        var entryCounts = new int[2, 2];
        var barrierReached = new bool[2, 2];
        var failures = new Exception?[2];

        Thread[] threads = Enumerable.Range(0, 2).Select(index => new Thread(() =>
        {
            try
            {
                using (ExpressionSimplifier.BeginActivatedComparisonMemoSessionForTesting())
                {
                    results[index, 0] = ExpressionComparer.SimplifyForComparison(build());
                    entryCounts[index, 0] = ExpressionSimplifier.ComparisonMemoEntryCount;
                    barrierReached[index, 0] = barrier.SignalAndWait(timeout);
                    results[index, 1] = ExpressionComparer.SimplifyForComparison(build());
                    entryCounts[index, 1] = ExpressionSimplifier.ComparisonMemoEntryCount;
                    barrierReached[index, 1] = barrier.SignalAndWait(timeout);
                }
            }
            catch (Exception exception)
            {
                failures[index] = exception;
            }
        })).ToArray();

        foreach (Thread thread in threads) thread.Start();
        foreach (Thread thread in threads) Assert.IsTrue(thread.Join(timeout), "A worker thread did not finish in time.");

        for (int index = 0; index < 2; index++)
        {
            Assert.IsNull(failures[index], failures[index]?.ToString());
            Assert.IsTrue(barrierReached[index, 0] && barrierReached[index, 1], "Both sessions must genuinely overlap.");
            Assert.AreSame(results[index, 0], results[index, 1], "Each thread must reuse its own completed result.");
            Assert.IsTrue(entryCounts[index, 0] >= 1);
            Assert.AreEqual(entryCounts[index, 0], entryCounts[index, 1], "The second lookup must be a hit, adding no entry.");
        }

        Assert.AreNotSame(results[0, 0], results[1, 0], "A thread must never receive another thread's memo entry.");
        AssertNoMemoState();
    }

    // ------------------------------------------------------------------------------------------
    // Helpers and baseline values
    // ------------------------------------------------------------------------------------------

    /// <summary>Baseline (commit <c>133861c5</c>) result of the 12-term additive near-miss chain.</summary>
    private const string BaselineA12 = "((2 * x0) + ((3 * x1) + ((4 * x2) + ((5 * x3) + ((6 * x4) + ((7 * x5) + ((8 * x6) + ((9 * x7) + ((10 * x8) + ((11 * x9) + ((12 * x10) + (13 * x11))))))))))))";

    /// <summary>Baseline (commit <c>133861c5</c>) result of the 12-factor multiplication chain.</summary>
    private const string BaselineG12 = "(x0 * (x1 * (x2 * (x3 * (x4 * (x5 * (x6 * (x7 * (x8 * (x9 * (x10 * x11)))))))))))";

    /// <summary>Baseline (commit <c>133861c5</c>) invocation count of the first counting constant.</summary>
    private const int BaselineSpyB12C0 = 3;

    /// <summary>Baseline (commit <c>133861c5</c>) invocation count of the second counting constant.</summary>
    private const int BaselineSpyB12C1 = 0;

    /// <summary>
    /// Asserts that <paramref name="action"/> throws <typeparamref name="TException"/>, either directly or wrapped in the
    /// <see cref="TargetInvocationException"/> produced by the simplifier's reflective rule invocation.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action expected to throw.</param>
    private static void AssertThrowsRuleException<TException>(Action action) where TException : Exception
    {
        Exception thrown = Assert.Throws<Exception>(action);
        while (thrown is TargetInvocationException { InnerException: not null } wrapper) thrown = wrapper.InnerException;
        Assert.IsInstanceOfType<TException>(thrown);
    }

    /// <summary>Compiles and evaluates a one-parameter expression at <c>x = 3</c>.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="x">Its parameter.</param>
    /// <returns>The value.</returns>
    private static double Evaluate(Expression expression, ParameterExpression x)
        => Expression.Lambda<Func<double, double>>(expression, x).Compile()(3.0);

    /// <summary>Compiles and evaluates an expression over several parameters.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="parameters">Its parameters.</param>
    /// <param name="values">The parameter values.</param>
    /// <returns>The value.</returns>
    private static double Evaluate(Expression expression, ParameterExpression[] parameters, double[] values)
        => (double)Expression.Lambda(expression, parameters).Compile().DynamicInvoke(values.Cast<object>().ToArray())!;
}
