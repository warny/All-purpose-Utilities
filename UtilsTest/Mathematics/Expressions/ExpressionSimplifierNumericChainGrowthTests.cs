using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Pins the simplification of purely numeric additive chains whose canonical form is <c>(0 - a) + R</c> (roadmap S5
/// P16) and bounds its comparer work. Re-simplifying such a form rewrites <c>0 - a</c> to <c>-a</c>, then
/// <c>-a + R</c> to <c>R - a</c> through <c>AdditionWithNegate</c>, which compared <c>a</c> with <c>R</c> (simplifying
/// both) and then prepared both again; that doubled the work at every term. The characterization tests (simplified
/// trees, subclass hooks, user calls and exceptions) were measured on the pre-P16 baseline (<c>9b4bc462</c>) and pass
/// unchanged on it; the work-bound tests fail on it.
/// </summary>
[TestClass]
public class ExpressionSimplifierNumericChainGrowthTests
{
    /// <summary>Returns the product of two values for a custom-operator expression.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The product.</returns>
    public static double CustomMultiply(double left, double right) => left * right;

    /// <summary>The custom multiplication operator.</summary>
    private static readonly MethodInfo CustomMultiplyMethod = typeof(ExpressionSimplifierNumericChainGrowthTests).GetMethod(nameof(CustomMultiply))!;

    /// <summary>Marker exception thrown by a <see cref="LoggingConstant"/>.</summary>
    private sealed class LoggingConstantException : Exception
    {
        /// <summary>Initializes the exception.</summary>
        /// <param name="message">The message identifying the throwing call.</param>
        public LoggingConstantException(string message) : base(message) { }
    }

    /// <summary>
    /// An opaque constant whose <see cref="Equals(object?)"/> appends <c>self~other</c> to a shared log, runs an
    /// optional callback, throws when the shared log reaches a chosen length, and otherwise answers with reference
    /// equality.
    /// </summary>
    private sealed class LoggingConstant
    {
        /// <summary>The shared call log.</summary>
        private readonly List<string> _log;

        /// <summary>The 1-based shared call number that throws, or 0.</summary>
        private readonly int _throwAt;

        /// <summary>A callback run by every <see cref="Equals(object?)"/> call, or <see langword="null"/>.</summary>
        private readonly Action? _callback;

        /// <summary>Initializes the constant.</summary>
        /// <param name="id">The identifier written to the log.</param>
        /// <param name="log">The shared call log.</param>
        /// <param name="throwAt">The 1-based shared call number that throws, or 0.</param>
        /// <param name="callback">A callback run by every call, or <see langword="null"/>.</param>
        public LoggingConstant(string id, List<string> log, int throwAt = 0, Action? callback = null)
        {
            Id = id;
            _log = log;
            _throwAt = throwAt;
            _callback = callback;
        }

        /// <summary>Gets the identifier written to the log.</summary>
        public string Id { get; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            _log.Add(Id + "~" + ((obj as LoggingConstant)?.Id ?? "?"));
            _callback?.Invoke();
            if (_log.Count == _throwAt) throw new LoggingConstantException(Id + "#" + _log.Count);
            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 17;
    }

    /// <summary>A subclass counting its preparation and finalization hooks, without changing their results.</summary>
    private sealed class CountingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the number of <c>PrepareExpression</c> calls.</summary>
        public int PrepareCount { get; private set; }

        /// <summary>Gets the number of <c>FinalizeExpression</c> calls.</summary>
        public int FinalizeCount { get; private set; }

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e)
        {
            PrepareCount++;
            return base.PrepareExpression(e);
        }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            FinalizeCount++;
            return base.FinalizeExpression(e, parameters);
        }
    }

    /// <summary>Creates <paramref name="n"/> parameters <c>x0..x(n-1)</c> of type <paramref name="type"/>.</summary>
    /// <param name="n">The number of parameters.</param>
    /// <param name="type">The parameter type.</param>
    /// <returns>The parameters.</returns>
    private static ParameterExpression[] Parameters(int n, Type type)
        => Enumerable.Range(0, n).Select(i => Expression.Parameter(type, "x" + i)).ToArray();

    /// <summary>Builds the constant <paramref name="value"/> of type <paramref name="type"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The constant type.</param>
    /// <returns>The constant.</returns>
    private static Expression Constant(double value, Type type)
        => Expression.Constant(Convert.ChangeType(value, type, CultureInfo.InvariantCulture), type);

    /// <summary>Builds the terms <c>100*x0, x1*3, 4*x2, x3*5, ...</c> (the order of the factors alternates).</summary>
    /// <param name="xs">The parameters.</param>
    /// <returns>The terms.</returns>
    private static Expression[] Terms(ParameterExpression[] xs)
    {
        Type type = xs[0].Type;
        var terms = new Expression[xs.Length];
        terms[0] = Expression.Multiply(Constant(100, type), xs[0]);
        for (int i = 1; i < xs.Length; i++)
        {
            terms[i] = i % 2 == 0
                ? Expression.Multiply(Constant(i + 2, type), xs[i])
                : Expression.Multiply(xs[i], Constant(i + 2, type));
        }

        return terms;
    }

    /// <summary>Combines <paramref name="items"/> left-associatively with <paramref name="combine"/>.</summary>
    /// <param name="items">The operands.</param>
    /// <param name="combine">The binary factory.</param>
    /// <returns>The chain.</returns>
    private static Expression Chain(IEnumerable<Expression> items, Func<Expression, Expression, BinaryExpression> combine)
        => items.Aggregate((accumulator, item) => combine(accumulator, item));

    /// <summary>
    /// The P16 chain <c>100*x0 - x1*3 - 4*x2 - ...</c>: its first term sorts last, so the canonical form is
    /// <c>(0 - 3*x1) + ((0 - 4*x2) + (... + 100*x0))</c>.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="type">The numeric type.</param>
    /// <returns>The chain.</returns>
    private static Expression TailSortedChain(int n, Type? type = null)
        => Chain(Terms(Parameters(n, type ?? typeof(double))), Expression.Subtract);

    /// <summary>Simplifies <paramref name="expression"/> with a new built-in simplifier and returns its text.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The simplified expression's text.</returns>
    private static string Simplified(Expression expression)
    {
        Expression result = new ExpressionSimplifier().Simplify(expression);
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            return result.ToString();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>Counts the comparer simplifications of one public simplification, through the diagnostic capture.</summary>
    /// <param name="source">The expression to simplify.</param>
    /// <returns>The number of comparer-triggered simplifications.</returns>
    private static int CountComparerSimplifications(Expression source)
    {
        ExpressionComparer.BeginDiagnosticCapture();
        try
        {
            new ExpressionSimplifier().Simplify(source);
        }
        finally
        {
            ExpressionComparer.EndDiagnosticCapture();
        }

        ExpressionComparer.BeginDiagnosticCapture();
        new ExpressionSimplifier().Simplify(source);
        return ExpressionComparer.EndDiagnosticCapture().Count;
    }

    /// <summary>Returns the innermost exception thrown by <paramref name="action"/>, or <see langword="null"/>.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The innermost exception, or <see langword="null"/> when nothing is thrown.</returns>
    private static Exception? Innermost(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            while (exception.InnerException is not null) exception = exception.InnerException;
            return exception;
        }
    }

    /// <summary>Returns a short stable digest of <paramref name="text"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The first 16 hexadecimal digits of its SHA-256 digest.</returns>
    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    // ------------------------------------------------------------------------------------------
    // Characterization: simplified trees (measured on 9b4bc462)
    // ------------------------------------------------------------------------------------------

    /// <summary>The first term sorted last: the canonical form is the chain of negative groups ending with it.</summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="expected">The simplified tree.</param>
    [TestMethod]
    [DataRow(2, "((0 - (3 * x1)) + (100 * x0))")]
    [DataRow(4, "((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + (100 * x0))))")]
    [DataRow(6, "((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + ((0 - (7 * x5)) + (100 * x0))))))")]
    [DataRow(9, "((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + ((0 - (7 * x5)) + ((0 - (8 * x6)) + ((0 - (9 * x7)) + ((0 - (10 * x8)) + (100 * x0)))))))))")]
    public void TailSortedChain_SimplifiedForm(int n, string expected)
        => Assert.AreEqual(expected, Simplified(TailSortedChain(n)));

    /// <summary>
    /// The shapes around the P16 chain: first term sorted first (negated head), source terms in reverse order,
    /// right-associated source, pure addition, equal and permuted coefficients, a lambda binding every parameter.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <param name="expected">The simplified tree.</param>
    [TestMethod]
    [DataRow("negatedHead", "-((3 * x1) + ((4 * x2) + ((5 * x3) + ((6 * x4) + ((7 * x5) + ((8 * x6) + (100 * x0)))))))")]
    [DataRow("reversed", "(((100 * x0) - (3 * x1)) - ((4 * x2) + ((5 * x3) + ((6 * x4) + ((7 * x5) + (8 * x6))))))")]
    [DataRow("rightAssociated", "(((100 * x0) - (3 * x1)) - ((4 * x2) + ((5 * x3) + ((6 * x4) + ((7 * x5) + (8 * x6))))))")]
    [DataRow("addition", "((3 * x1) + ((4 * x2) + ((5 * x3) + ((6 * x4) + ((7 * x5) + ((8 * x6) + (100 * x0)))))))")]
    [DataRow("equalCoefficients", "(((((((x0 - x1) - x2) - x3) - x4) - x5) - x6) * 3)")]
    [DataRow("permutedCoefficients", "((2 * x0) + ((0 - (16 * x3)) + ((0 - (30 * x6)) + ((0 - (39 * x1)) + ((0 - (53 * x4)) + ((0 - (76 * x2)) + (0 - (90 * x5))))))))")]
    [DataRow("lambda", "(x0, x1, x2, x3, x4, x5, x6) => ((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + ((0 - (7 * x5)) + ((0 - (8 * x6)) + (100 * x0)))))))")]
    [DataRow("negatedInLambda", "(x0, x1, x2, x3, x4, x5, x6) => ((0 - (2 * x3)) + ((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + ((0 - (7 * x5)) + ((0 - (8 * x6)) + (100 * x0))))))))")]
    public void ChainShapes_SimplifiedForm(string shape, string expected)
    {
        ParameterExpression[] xs = Parameters(7, typeof(double));
        Expression[] terms = Terms(xs);
        Expression source = shape switch
        {
            "negatedHead" => Chain(terms.Skip(1).Prepend(Expression.Negate(terms[0])), Expression.Subtract),
            "reversed" => Expression.Add(
                Chain(terms.Skip(1).Reverse().Skip(1).Prepend(Expression.Subtract(Expression.Constant(0.0), terms[^1])), Expression.Subtract),
                terms[0]),
            "rightAssociated" => Expression.Subtract(terms[0], terms.Skip(1).Reverse().Aggregate((accumulator, term) => Expression.Add(term, accumulator))),
            "addition" => Chain(terms, Expression.Add),
            "equalCoefficients" => Chain(xs.Select(x => (Expression)Expression.Multiply(Expression.Constant(3.0), x)), Expression.Subtract),
            "permutedCoefficients" => Chain(xs.Select((x, i) => (Expression)Expression.Multiply(Expression.Constant(2.0 + (i * 37 % 97)), x)), Expression.Subtract),
            "lambda" => Expression.Lambda(Chain(terms, Expression.Subtract), xs),
            "negatedInLambda" => Expression.Lambda(
                Expression.Add(Expression.Negate(Expression.Multiply(Expression.Constant(2.0), xs[3])), Chain(terms, Expression.Subtract)),
                xs),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        Assert.AreEqual(expected, Simplified(source));
    }

    /// <summary>Every order of the same four signed terms, in both associations: the digest of the 48 simplified trees.</summary>
    [TestMethod]
    public void TermPermutations_SimplifiedForms()
    {
        ParameterExpression[] xs = Parameters(4, typeof(double));
        Expression[] terms = Terms(xs);
        var forms = new List<string>();
        foreach (int[] order in Permutations([0, 1, 2, 3]))
        {
            Expression[] ordered = order.Select(i => terms[i]).ToArray();
            forms.Add(Simplified(Chain(ordered, Expression.Subtract)));
            forms.Add(Simplified(Expression.Subtract(ordered[0], ordered.Skip(1).Reverse().Aggregate((accumulator, term) => Expression.Subtract(term, accumulator)))));
        }

        Assert.AreEqual(48, forms.Count);
        Assert.AreEqual("A2922F82B2886E39", Digest(string.Join("|", forms)));
    }

    /// <summary>Enumerates the permutations of <paramref name="items"/> in lexicographic order of positions.</summary>
    /// <param name="items">The items.</param>
    /// <returns>Every permutation.</returns>
    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (int i = 0; i < items.Length; i++)
        {
            int head = items[i];
            foreach (int[] rest in Permutations(items.Where((_, j) => j != i).ToArray()))
            {
                yield return rest.Prepend(head).ToArray();
            }
        }
    }

    /// <summary>
    /// Negated additions that cancel or factor, and ones that do not: the comparison of <c>AdditionWithNegate</c> and
    /// the factoring of the rewritten difference.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <param name="expected">The simplified tree.</param>
    [TestMethod]
    [DataRow("negatedLeftCancels", "0")]
    [DataRow("negatedRightCancels", "0")]
    [DataRow("negatedChainCancels", "((((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + (100 * x0))))) - (0 - (3 * x1))) - ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + (100 * x0)))))")]
    [DataRow("sumMinusPart", "a")]
    [DataRow("differencePlusPart", "a")]
    [DataRow("plusZero", "0")]
    [DataRow("timesOne", "c")]
    [DataRow("partiallyFactorable", "((0 - (3 * a)) + ((0 - (4 * a)) + ((0 - (5 * b)) + (100 * c))))")]
    [DataRow("repeatedTerm", "((0 - (3 * a)) + ((0 - (6 * a)) + (100 * c)))")]
    [DataRow("notEqual", "((0 - c) + ((0 - a) + b))")]
    public void NegatedAdditions_SimplifiedForm(string shape, string expected)
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a");
        ParameterExpression b = Expression.Parameter(typeof(double), "b");
        ParameterExpression c = Expression.Parameter(typeof(double), "c");
        Expression three = Expression.Constant(3.0);
        Expression source = shape switch
        {
            "negatedLeftCancels" => Expression.Add(Expression.Negate(Expression.Multiply(three, a)), Expression.Multiply(a, three)),
            "negatedRightCancels" => Expression.Add(Expression.Subtract(a, b), Expression.Negate(Expression.Subtract(a, b))),
            "negatedChainCancels" => Expression.Add(Expression.Negate(TailSortedChain(5)), TailSortedChain(5)),
            "sumMinusPart" => Expression.Add(Expression.Negate(b), Expression.Add(a, b)),
            "differencePlusPart" => Expression.Add(Expression.Negate(Expression.Negate(b)), Expression.Subtract(a, b)),
            "plusZero" => Expression.Add(Expression.Negate(Expression.Add(a, Expression.Constant(0.0))), a),
            "timesOne" => Expression.Add(Expression.Negate(Expression.Multiply(a, Expression.Constant(1.0))), Expression.Add(a, c)),
            "partiallyFactorable" => Expression.Add(Expression.Negate(Expression.Multiply(Expression.Constant(4.0), a)),
                Expression.Subtract(Expression.Subtract(Expression.Multiply(Expression.Constant(100.0), c), Expression.Multiply(a, three)), Expression.Multiply(Expression.Constant(5.0), b))),
            "repeatedTerm" => Expression.Add(Expression.Negate(Expression.Multiply(three, a)),
                Expression.Subtract(Expression.Subtract(Expression.Multiply(Expression.Constant(100.0), c), Expression.Multiply(three, a)), Expression.Multiply(three, a))),
            "notEqual" => Expression.Add(Expression.Negate(a), Expression.Subtract(b, c)),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        Assert.AreEqual(expected, Simplified(source));
    }

    /// <summary>
    /// Repeated terms held by one shared reference and by structurally equal copies simplify to the same tree.
    /// </summary>
    [TestMethod]
    public void RepeatedTerms_SharedReferencesAndCopies_SameForm()
    {
        ParameterExpression[] xs = Parameters(8, typeof(double));
        Expression[] shared = Terms(xs);
        Expression[] copies = Terms(xs);
        for (int i = 3; i < shared.Length; i++)
        {
            shared[i] = shared[i - 2];
            copies[i] = Expression.Multiply(((BinaryExpression)copies[i - 2]).Left, ((BinaryExpression)copies[i - 2]).Right);
        }

        string sharedForm = Simplified(Chain(shared, Expression.Subtract));
        Assert.AreEqual("(((((0 - (3 * x1)) - (3 * x1)) - (3 * x1)) - (3 * x1)) + ((((0 - (4 * x2)) - (4 * x2)) - (4 * x2)) + (100 * x0)))", sharedForm);
        Assert.AreEqual(sharedForm, Simplified(Chain(copies, Expression.Subtract)));
    }

    /// <summary>
    /// The P16 chain over every ordinary numeric type (the unsigned ones keep <c>0 - t</c>). Since ETC R1b (C-06) a decimal
    /// chain is not reassociated (decimal arithmetic may throw on overflow), so it keeps its source shape.
    /// </summary>
    /// <param name="typeName">The numeric type.</param>
    /// <param name="expected">The simplified tree.</param>
    [TestMethod]
    [DataRow("Int32", "((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + (100 * x0)))))")]
    [DataRow("Int64", "((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + (100 * x0)))))")]
    [DataRow("UInt32", "((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + (100 * x0)))))")]
    [DataRow("Single", "((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + (100 * x0)))))")]
    [DataRow("Decimal", "(((((100 * x0) - (3 * x1)) - (4 * x2)) - (5 * x3)) - (6 * x4))")]
    public void TypedChains_SimplifiedForm(string typeName, string expected)
        => Assert.AreEqual(expected, Simplified(TailSortedChain(5, Type.GetType("System." + typeName)!)));

    /// <summary>
    /// Negated additions over non-finite, signed-zero and extreme constants: the comparison and the rewritten difference
    /// keep their IEEE-754 behavior. The decimal shape used to be rewritten (the rules reassociated additions that may
    /// overflow); since ETC R1b (C-06) its decimal operands are not inert, so it is returned with its source structure.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <param name="expected">The simplified tree, or the innermost exception type.</param>
    [TestMethod]
    [DataRow("nan", "0")]
    [DataRow("infinity", "((0 - (3 * z)) + ((100 * x) + ((0 - (Infinity * y)) - (Infinity * y))))")]
    [DataRow("negativeZero", "((0 - (3 * z)) + (100 * x))")]
    [DataRow("decimalMax", "(-(79228162514264337593543950335 + x) + (((79228162514264337593543950335 + (79228162514264337593543950335 * x)) - (-7.25 * y)) - 79228162514264337593543950335))")]
    public void SpecialConstants_SimplifiedForm(string shape, string expected)
    {
        Expression source;
        if (shape == "decimalMax")
        {
            ParameterExpression x = Expression.Parameter(typeof(decimal), "x");
            ParameterExpression y = Expression.Parameter(typeof(decimal), "y");
            Expression max = Expression.Constant(decimal.MaxValue);
            source = Expression.Add(Expression.Negate(Expression.Add(max, x)),
                Expression.Subtract(Expression.Subtract(Expression.Add(max, Expression.Multiply(max, x)), Expression.Multiply(Expression.Constant(-7.25m), y)), max));
        }
        else
        {
            double value = shape switch { "nan" => double.NaN, "infinity" => double.PositiveInfinity, _ => -0.0 };
            ParameterExpression x = Expression.Parameter(typeof(double), "x");
            ParameterExpression y = Expression.Parameter(typeof(double), "y");
            ParameterExpression z = Expression.Parameter(typeof(double), "z");
            Expression term = Expression.Multiply(Expression.Constant(value), y);
            source = Expression.Add(Expression.Negate(term),
                Expression.Subtract(Expression.Subtract(Expression.Multiply(Expression.Constant(100.0), x), Expression.Multiply(Expression.Constant(value), y)), Expression.Multiply(z, Expression.Constant(3.0))));
        }

        Exception? exception = null;
        string actual = "";
        exception = Innermost(() => actual = Simplified(source));
        Assert.AreEqual(expected, exception is null ? actual : exception.GetType().Name);
    }

    /// <summary>
    /// The P16 chain with a custom multiplication operator on every term: each term keeps its method. Since ETC R1b (C-06)
    /// a custom operator is user code, so the terms are no longer sorted: they keep their source order.
    /// </summary>
    [TestMethod]
    public void CustomOperatorChain_SimplifiedForm()
    {
        ParameterExpression[] xs = Parameters(6, typeof(double));
        Expression source = Chain(
            xs.Select((x, i) => (Expression)Expression.Multiply(Expression.Constant(i == 0 ? 100.0 : i + 2.0), x, CustomMultiplyMethod)),
            Expression.Subtract);

        Expression result = new ExpressionSimplifier().Simplify(source);

        Assert.AreEqual("((100 * x0) + ((0 - (3 * x1)) + ((0 - (4 * x2)) + ((0 - (5 * x3)) + ((0 - (6 * x4)) + (0 - (7 * x5)))))))", result.ToString());
        Assert.AreEqual(6, CountCustomMultiplications(result));
    }

    /// <summary>Counts the multiplications of <paramref name="expression"/> that use <see cref="CustomMultiply"/>.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The count.</returns>
    private static int CountCustomMultiplications(Expression expression) => expression switch
    {
        BinaryExpression binary => (binary.Method == CustomMultiplyMethod ? 1 : 0) + CountCustomMultiplications(binary.Left) + CountCustomMultiplications(binary.Right),
        UnaryExpression unary => CountCustomMultiplications(unary.Operand),
        _ => 0,
    };

    // ------------------------------------------------------------------------------------------
    // Characterization: subclasses, user code, exceptions, re-entrancy (measured on 9b4bc462)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A subclass observes every preparation and finalization of the historical path: its hooks run exactly as many times
    /// as before, and it builds the same trees.
    /// </summary>
    /// <param name="n">The number of terms.</param>
    /// <param name="negatedTerm">Whether the chain is added to <c>-(2 * y)</c>, so that the subclass's own pass runs <c>AdditionWithNegate</c>.</param>
    /// <param name="expectedPrepare">The number of <c>PrepareExpression</c> calls.</param>
    /// <param name="expectedFinalize">The number of <c>FinalizeExpression</c> calls.</param>
    [TestMethod]
    [DataRow(4, false, 14, 13)]
    [DataRow(7, false, 26, 24)]
    [DataRow(4, true, 130, 129)]
    [DataRow(7, true, 982, 980)]
    public void Subclass_ObservesEveryPreparation(int n, bool negatedTerm, int expectedPrepare, int expectedFinalize)
    {
        var simplifier = new CountingSimplifier();
        Expression source = negatedTerm
            ? Expression.Add(Expression.Negate(Expression.Multiply(Expression.Constant(2.0), Expression.Parameter(typeof(double), "y"))), TailSortedChain(n))
            : TailSortedChain(n);

        string result = simplifier.Simplify(source).ToString();

        Assert.AreEqual(Simplified(source), result);
        Assert.AreEqual(expectedPrepare, simplifier.PrepareCount);
        Assert.AreEqual(expectedFinalize, simplifier.FinalizeCount);
    }

    /// <summary>
    /// Builds <c>-(3 * Id(negated)) + (100 * Id(values[0]) - 3 * Id(values[1]) - 4 * Id(values[2]) - ...)</c>: every term
    /// holds an opaque constant, so the factor comparisons reach the constants' own <see cref="object.Equals(object?)"/>.
    /// </summary>
    /// <param name="negated">The opaque value of the negated term.</param>
    /// <param name="values">The opaque values of the chain's terms.</param>
    /// <returns>The negated addition.</returns>
    private static Expression OpaqueNegatedAddition(object negated, object[] values)
    {
        static Expression Opaque(double coefficient, object value)
            => Expression.Multiply(Expression.Constant(coefficient), SafeOpaqueTerm.Carrier(value));
        Expression chain = Chain(values.Select((value, i) => Opaque(i == 0 ? 100 : i + 2, value)), Expression.Subtract);
        return Expression.Add(Expression.Negate(Opaque(3, negated)), chain);
    }

    /// <summary>Creates <paramref name="n"/> logging constants <c>c0..c(n-1)</c> sharing <paramref name="log"/>.</summary>
    /// <param name="n">The number of constants.</param>
    /// <param name="log">The shared call log.</param>
    /// <param name="throwAt">The 1-based shared call number that throws, or 0.</param>
    /// <returns>The constants.</returns>
    private static object[] LoggingConstants(int n, List<string> log, int throwAt = 0)
        => Enumerable.Range(0, n).Select(i => (object)new LoggingConstant("c" + i, log, throwAt)).ToArray();

    /// <summary>
    /// Opaque constants reached by the comparison of <c>AdditionWithNegate</c>: the sequence of their own
    /// <see cref="object.Equals(object?)"/> calls and the simplified tree.
    /// </summary>
    /// <param name="n">The number of chain terms.</param>
    /// <param name="sameValue">Whether the negated term holds the value of the chain's second term.</param>
    /// <param name="expectedDigest">The digest of the call log and the simplified tree.</param>
    /// <param name="expectedCalls">The number of calls.</param>
    [TestMethod]
    [DataRow(3, false, "7CA167CBDAAE6DE0", 23)]
    [DataRow(5, false, "7F5D265CD4120525", 563)]
    [DataRow(5, true, "EE3FFAF773BA5DEA", 563)]
    public void OpaqueNegatedAddition_UserCallsAndForm(int n, bool sameValue, string expectedDigest, int expectedCalls)
    {
        var log = new List<string>();
        object[] values = LoggingConstants(n, log);
        object negated = sameValue ? values[1] : new LoggingConstant("m", log);

        string result = Simplified(OpaqueNegatedAddition(negated, values));

        Assert.AreEqual(expectedCalls, log.Count);
        Assert.AreEqual(expectedDigest, Digest(string.Join(";", log) + "|" + result));
    }

    /// <summary>A constant throwing at its k-th <see cref="object.Equals(object?)"/> call: same call, same exception.</summary>
    /// <param name="throwAt">The throwing call.</param>
    /// <param name="expectedMessage">The message of the propagated exception, or <c>none</c>.</param>
    /// <param name="expectedCalls">The number of calls made.</param>
    [TestMethod]
    [DataRow(1, "c0#1", 1)]
    [DataRow(3, "c0#3", 3)]
    [DataRow(40, "c0#40", 40)]
    [DataRow(400, "c0#400", 400)]
    public void OpaqueNegatedAddition_ThrowingEquals(int throwAt, string expectedMessage, int expectedCalls)
    {
        var log = new List<string>();
        var negated = new LoggingConstant("m", log, throwAt);

        Exception? exception = Innermost(() => Simplified(OpaqueNegatedAddition(negated, LoggingConstants(5, log, throwAt))));

        Assert.AreEqual(expectedMessage, exception is LoggingConstantException ? exception.Message : exception?.GetType().Name ?? "none");
        Assert.AreEqual(expectedCalls, log.Count);
    }

    /// <summary>
    /// A constant whose <see cref="object.Equals(object?)"/> re-enters the public simplifier on a P16 chain and the public
    /// comparer: the outer calls, the re-entrant results and the simplified tree are unchanged.
    /// </summary>
    [TestMethod]
    public void OpaqueNegatedAddition_ReentrantEquals()
    {
        var log = new List<string>();
        var inner = new List<string>();
        Action reenter = () =>
        {
            inner.Add(Simplified(TailSortedChain(5)));
            inner.Add(ExpressionComparer.Default.Equals(TailSortedChain(4), TailSortedChain(4)).ToString());
        };
        var negated = new LoggingConstant("m", log, callback: reenter);

        string result = Simplified(OpaqueNegatedAddition(negated, LoggingConstants(5, log)));

        Assert.AreEqual(563, log.Count);
        Assert.AreEqual("42C1BCB906E80466", Digest(string.Join(";", log) + "|" + string.Join(";", inner) + "|" + result));
    }

    /// <summary>The P9 strict keys of rebuilt P16 chains: equal for the same structure, distinct otherwise.</summary>
    [TestMethod]
    public void ComparisonMemoKeys_StructurallyIdenticalAndDistinctChains()
    {
        Expression first = TailSortedChain(6);
        ParameterExpression[] xs = CollectParameters(first);
        Expression rebuilt = Chain(Terms(xs), Expression.Subtract);
        Expression other = TailSortedChain(6);

        Assert.IsTrue(ExpressionSimplifier.IsComparisonMemoEligible(first));
        Assert.IsTrue(ExpressionSimplifier.ComparisonMemoKeysEqual(first, rebuilt));
        Assert.IsFalse(ExpressionSimplifier.ComparisonMemoKeysEqual(first, other));
        Assert.AreEqual(Simplified(first), Simplified(rebuilt));
    }

    /// <summary>Collects the parameters of <paramref name="expression"/> in the order <c>x0, x1, ...</c>.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The parameters sorted by name.</returns>
    private static ParameterExpression[] CollectParameters(Expression expression)
    {
        var parameters = new HashSet<ParameterExpression>();
        void Visit(Expression e)
        {
            switch (e)
            {
                case ParameterExpression p: parameters.Add(p); break;
                case BinaryExpression b: Visit(b.Left); Visit(b.Right); break;
                case UnaryExpression u: Visit(u.Operand); break;
            }
        }

        Visit(expression);
        return parameters.OrderBy(p => int.Parse(p.Name![1..], CultureInfo.InvariantCulture)).ToArray();
    }


    /// <summary>
    /// Chains whose head is not inert arithmetic keep the second preparation, hence their comparer work, exactly: an
    /// opaque constant (never compared), its known-safe control, a call, and the decimal chain (its arithmetic can throw on
    /// overflow). The opaque chain still costs exactly its known-safe control (S5 P15). Measured on the baseline. Since ETC
    /// R1b (C-06) the decimal chain is no longer rewritten at all (its operands may throw), so it makes only the comparer
    /// work of preparing its leaves, far below the former 17,718.
    /// </summary>
    /// <param name="head">The head term.</param>
    /// <param name="expected">The number of comparer simplifications.</param>
    [TestMethod]
    [DataRow("opaque", 17_718)]
    [DataRow("knownSafe", 17_718)]
    [DataRow("call", 17_718)]
    [DataRow("decimal", 8)]
    public void NonInertChains_KeepTheirComparerWork(string head, int expected)
    {
        var log = new List<string>();
        Expression source;
        if (head == "decimal")
        {
            source = TailSortedChain(8, typeof(decimal));
        }
        else
        {
            ParameterExpression[] xs = Parameters(8, typeof(double));
            Expression[] terms = Terms(xs);
            Expression value = head switch
            {
                "opaque" => SafeOpaqueTerm.Carrier(new LoggingConstant("h", log)),
                "knownSafe" => SafeOpaqueTerm.Carrier("safe"),
                _ => Expression.Call(typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!, xs[0]),
            };
            terms[0] = Expression.Multiply(Expression.Constant(100.0), value);
            source = Chain(terms, Expression.Subtract);
        }

        Assert.AreEqual(expected, CountComparerSimplifications(source));
        Assert.AreEqual(0, log.Count);
    }

    // ------------------------------------------------------------------------------------------
    // Work bounds (fail on 9b4bc462)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The comparer work of the P16 chain no longer doubles with each term: 12 terms cost under 20,000 comparer
    /// simplifications (101,414 before P16) and 16 terms under twice as many as 12 (991,350 before P16, about 9.8 times).
    /// </summary>
    [TestMethod]
    public void TailSortedChain_ComparerWorkIsPolynomial()
    {
        int twelve = CountComparerSimplifications(TailSortedChain(12));
        int sixteen = CountComparerSimplifications(TailSortedChain(16));

        Assert.IsTrue(twelve < 20_000, $"{twelve} comparer simplifications for 12 terms.");
        Assert.IsTrue(sixteen < 2 * twelve, $"{sixteen} comparer simplifications for 16 terms, {twelve} for 12.");
    }

    /// <summary>
    /// The integral chain benefits like the floating-point one: its operands are inert arithmetic (no integral division).
    /// </summary>
    [TestMethod]
    public void TailSortedIntegralChain_ComparerWorkIsBounded()
    {
        int events = CountComparerSimplifications(TailSortedChain(12, typeof(long)));

        Assert.IsTrue(events < 20_000, $"{events} comparer simplifications for 12 terms.");
    }
}
