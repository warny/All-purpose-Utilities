using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The grouped quotient chains (<c>docs/expressions/SimplifierFlatten.md</c> §16), such as
/// <c>1 - (x1/x0 - (x2/x0 - x3/x0))</c> and <c>1 - ((x1/x0 + x2/x0) + (x3/x0 + x4/x0))</c>: which chains the admission accepts
/// and refuses, and that an accepted one gives, after one pass, exactly the structure of the historical level by level
/// treatment of the built-in simplifier, under every policy.
/// </summary>
/// <remarks>
/// The reference is the built-in <see cref="ExpressionSimplifier"/> itself with the family switched off on the test thread
/// (<see cref="ExpressionSimplifier.GroupedQuotientChainsDisabled"/>): every other path is unchanged, so it simplifies such a
/// chain level by level, as before the family existed. Trees are compared through <see cref="Dump"/> (node types, static types,
/// operator methods, exact constant bits and parameter instances).
/// </remarks>
[TestClass]
public class ExpressionSimplifierGroupedQuotientChainTests
{
    /// <summary>The policies of the brief: default, declaration trust, aggressive, partial exception preservation, permissive.</summary>
    internal static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.Runtime | ExpressionExceptionPreservation.UserCode },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>Renders the complete structure of a tree: node types, static types, operator methods, exact constants, parameter instances.</summary>
    /// <param name="expression">The tree.</param>
    /// <returns>The rendering.</returns>
    internal static string Dump(Expression? expression)
    {
        var builder = new StringBuilder();
        var pending = new Stack<object?>();
        pending.Push(expression);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case string text:
                    builder.Append(text);
                    break;
                case null:
                    builder.Append("null");
                    break;
                case ParameterExpression parameter:
                    builder.Append(parameter.Name).Append('#').Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(parameter));
                    break;
                case ConstantExpression constant:
                    builder.Append('[').Append(constant.Type.Name).Append(':')
                        .Append(constant.Value switch
                        {
                            double value => BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture),
                            float value => BitConverter.SingleToInt32Bits(value).ToString("x8", CultureInfo.InvariantCulture),
                            IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                            null => "null",
                            _ => constant.Value.GetType().Name,
                        })
                        .Append(']');
                    break;
                case BinaryExpression binary:
                    builder.Append('(').Append(binary.NodeType).Append('<').Append(binary.Type.Name).Append('>');
                    if (binary.Method is not null) builder.Append('!').Append(binary.Method.Name);
                    builder.Append(' ');
                    pending.Push(")");
                    pending.Push(binary.Right);
                    pending.Push(" ");
                    pending.Push(binary.Left);
                    break;
                case UnaryExpression unary:
                    builder.Append('(').Append(unary.NodeType).Append('<').Append(unary.Type.Name).Append("> ");
                    pending.Push(")");
                    pending.Push(unary.Operand);
                    break;
                case MethodCallExpression call:
                    builder.Append(call.Method.Name).Append('(');
                    pending.Push(")");
                    for (int i = call.Arguments.Count - 1; i >= 0; i--)
                    {
                        pending.Push(",");
                        pending.Push(call.Arguments[i]);
                    }

                    break;
                case Expression other:
                    builder.Append('<').Append(other.GetType().Name).Append('>');
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Simplifies <paramref name="source"/> with the historical treatment of the built-in simplifier (the family switched off).</summary>
    /// <param name="source">The tree.</param>
    /// <param name="options">The policy.</param>
    /// <returns>The result.</returns>
    internal static Expression Reference(Expression source, ExpressionSimplifierOptions? options = null)
    {
        bool previous = ExpressionSimplifier.GroupedQuotientChainsDisabled;
        ExpressionSimplifier.GroupedQuotientChainsDisabled = true;
        try
        {
            return new ExpressionSimplifier(ExpressionResourceLimits.Unlimited) { Options = options ?? ExpressionSimplifierOptions.Default }.Simplify(source);
        }
        finally
        {
            ExpressionSimplifier.GroupedQuotientChainsDisabled = previous;
        }
    }

    /// <summary>Simplifies <paramref name="source"/> with the admission counters installed.</summary>
    /// <param name="source">The tree.</param>
    /// <param name="options">The policy.</param>
    /// <param name="forceFallback">Whether every admitted grouped chain takes the level by level fallback.</param>
    /// <returns>The result and the counters.</returns>
    internal static (Expression Result, FlattenAdmissionStatistics Statistics) Counted(Expression source, ExpressionSimplifierOptions? options = null, bool forceFallback = false)
    {
        var statistics = new FlattenAdmissionStatistics { ForceGroupedFallback = forceFallback };
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            Expression result = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited) { Options = options ?? ExpressionSimplifierOptions.Default }.Simplify(source);
            return (result, statistics);
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }
    }

    /// <summary>The seeded generator of grouped chains.</summary>
    internal sealed class Generator
    {
        /// <summary>The random source.</summary>
        private readonly Random _random;

        /// <summary>The type of the chain.</summary>
        private readonly Type _type;

        /// <summary>How parameters are named: 0 distinct names, 1 one shared name, 2 no name.</summary>
        private readonly int _naming;

        /// <summary>The shared denominator.</summary>
        private readonly ParameterExpression _denominator;

        /// <summary>The number of parameters created.</summary>
        private int _parameters;

        /// <summary>The coefficients used, kept distinct.</summary>
        private readonly HashSet<double> _coefficients = [];

        /// <summary>Gets the parameters created, the shared denominator first.</summary>
        public List<ParameterExpression> Parameters { get; } = [];

        /// <summary>Creates a generator.</summary>
        /// <param name="seed">The seed.</param>
        public Generator(int seed)
        {
            _random = new Random(seed);
            _type = _random.Next(4) == 0 ? typeof(float) : typeof(double);
            _naming = _random.Next(5) switch { 0 => 1, 1 => 2, _ => 0 };
            _denominator = NewParameter();
        }

        /// <summary>Creates a parameter of the chain's type.</summary>
        /// <returns>The parameter.</returns>
        private ParameterExpression NewParameter()
        {
            string? name = _naming switch { 0 => "p" + _parameters, 1 => "p", _ => null };
            _parameters++;
            ParameterExpression parameter = Expression.Parameter(_type, name);
            Parameters.Add(parameter);
            return parameter;
        }

        /// <summary>Creates a constant of the chain's type.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The constant.</returns>
        private ConstantExpression Constant(double value)
            => _type == typeof(float) ? Expression.Constant((float)value, typeof(float)) : Expression.Constant(value, typeof(double));

        /// <summary>Creates a term: mostly quotients, some parameters and linear products.</summary>
        /// <returns>The term.</returns>
        private Expression Term()
        {
            int kind = _random.Next(10);
            if (kind < 4) return Expression.Divide(NewParameter(), _denominator);
            if (kind < 6) return Expression.Divide(NewParameter(), NewParameter());
            if (kind < 8) return NewParameter();
            if (kind < 9) return Expression.Multiply(NewParameter(), NewParameter());
            double coefficient;
            do coefficient = _random.Next(2, 9) * (_random.Next(2) == 0 ? 1 : -1) + (_random.Next(2) == 0 ? 0 : 0.5);
            while (!_coefficients.Add(coefficient));
            return _random.Next(2) == 0 ? Expression.Multiply(Constant(coefficient), NewParameter()) : Expression.Multiply(NewParameter(), Constant(coefficient));
        }

        /// <summary>Builds a chain of <paramref name="count"/> terms, at most one of them a constant.</summary>
        /// <param name="count">The number of terms.</param>
        /// <returns>The chain.</returns>
        public Expression Chain(int count)
        {
            var terms = new List<Expression>();
            for (int i = 0; i < count; i++) terms.Add(Term());
            if (_random.Next(2) == 0) terms[_random.Next(terms.Count)] = Constant(_random.Next(1, 6) * (_random.Next(3) == 0 ? -1 : 1) + 0.25 * _random.Next(2));
            return _random.Next(4) switch
            {
                0 => RightComb(terms),
                1 => LeftComb(terms),
                _ => Tree(terms, 0, terms.Count),
            };
        }

        /// <summary>A random operator.</summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The sum or the difference.</returns>
        private Expression Combine(Expression left, Expression right)
            => _random.Next(2) == 0 ? Expression.Subtract(left, right) : Expression.Add(left, right);

        /// <summary>A right comb: <c>t1 ± (t2 ± (… ± tn))</c>.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns>The chain.</returns>
        private Expression RightComb(List<Expression> terms)
        {
            Expression chain = terms[^1];
            for (int i = terms.Count - 2; i >= 0; i--) chain = Combine(terms[i], chain);
            return chain;
        }

        /// <summary>A left comb whose right operands are sometimes small groups.</summary>
        /// <param name="terms">The terms.</param>
        /// <returns>The chain.</returns>
        private Expression LeftComb(List<Expression> terms)
        {
            Expression chain = terms[0];
            int i = 1;
            while (i < terms.Count)
            {
                int size = Math.Min(terms.Count - i, _random.Next(1, 4));
                chain = Combine(chain, Tree(terms, i, i + size));
                i += size;
            }

            return chain;
        }

        /// <summary>A random tree over <c>terms[from..to)</c>.</summary>
        /// <param name="terms">The terms.</param>
        /// <param name="from">The first term.</param>
        /// <param name="to">The end of the range.</param>
        /// <returns>The tree.</returns>
        private Expression Tree(List<Expression> terms, int from, int to)
        {
            if (to - from == 1) return terms[from];
            int middle = from + 1 + _random.Next(to - from - 1);
            return Combine(Tree(terms, from, middle), Tree(terms, middle, to));
        }
    }

    /// <summary>
    /// The seeded corpus: every generated chain gives, after one pass and after two passes, exactly the structure of the historical
    /// treatment, under the default policy; a sample of them under every other policy.
    /// </summary>
    [TestMethod]
    public void SeededCorpus_GivesTheHistoricalStructure()
    {
        int admitted = 0;
        // Up to 14 terms: the historical treatment of a grouped chain is super-linear (the seeded corpus of the S5 harness goes
        // further, docs/expressions/SimplifierFlatten.md §16).
        for (int seed = 0; seed < 3000; seed++)
        {
            var generator = new Generator(seed);
            Expression source = generator.Chain(2 + seed % 13);
            string expected = Dump(Reference(source));
            (Expression result, FlattenAdmissionStatistics statistics) = Counted(source);
            admitted += statistics.GroupedChains > 0 ? 1 : 0;
            Assert.AreEqual(expected, Dump(result), $"seed {seed}: {source}");
            Assert.AreEqual(Dump(Reference(Reference(source))), Dump(new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(result)), $"seed {seed} (second pass): {source}");
            if (seed % 10 == 0)
            {
                foreach (ExpressionSimplifierOptions policy in Policies)
                {
                    Assert.AreEqual(Dump(Reference(source, policy)), Dump(Counted(source, policy).Result), $"seed {seed} under {policy}: {source}");
                }
            }
        }

        // 938 of the 3,000 cases are admitted grouped chains (the others hold no subtracted group, or a single term).
        Assert.IsTrue(admitted > 800, admitted.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The fallback of the family, forced on the seeded corpus, gives the historical structure too.</summary>
    [TestMethod]
    public void Fallback_GivesTheHistoricalStructure()
    {
        long fallbacks = 0;
        for (int seed = 0; seed < 600; seed++)
        {
            Expression source = new Generator(seed).Chain(2 + seed % 11);
            (Expression result, FlattenAdmissionStatistics statistics) = Counted(source, forceFallback: true);
            fallbacks += statistics.GroupedFallbacks;
            Assert.AreEqual(0, statistics.GroupedChains, $"seed {seed}");
            Assert.AreEqual(Dump(Reference(source)), Dump(result), $"seed {seed}: {source}");
        }

        Assert.IsTrue(fallbacks > 150, fallbacks.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The parameters of a family chain.</summary>
    /// <param name="type">The type.</param>
    /// <param name="count">The number of parameters.</param>
    /// <param name="prefix">The name prefix.</param>
    /// <returns>The parameters.</returns>
    private static ParameterExpression[] Parameters(Type type, int count, string prefix)
        => [.. Enumerable.Range(0, count).Select(i => Expression.Parameter(type, prefix + i))];

    /// <summary>The quotients <c>x1/x0 … xn/x0</c>, or over distinct denominators.</summary>
    /// <param name="type">The type.</param>
    /// <param name="n">The number of quotients.</param>
    /// <param name="distinct">Whether every quotient has its own denominator.</param>
    /// <returns>The quotients.</returns>
    internal static Expression[] Quotients(Type type, int n, bool distinct)
    {
        ParameterExpression[] x = Parameters(type, n + 1, "x");
        ParameterExpression[] y = Parameters(type, n + 1, "y");
        return [.. Enumerable.Range(1, n).Select(i => (Expression)Expression.Divide(x[i], distinct ? y[i] : x[0]))];
    }

    /// <summary>The constant one of <paramref name="type"/>.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression One(Type type) => Expression.Constant(Convert.ChangeType(1, type, CultureInfo.InvariantCulture), type);

    /// <summary>Builds a family over the given terms.</summary>
    /// <param name="shape"><c>right</c>, <c>balanced</c>, <c>balancedMixed</c>, <c>left</c> (a linear chain) or <c>alternating</c> (a linear chain).</param>
    /// <param name="head">The first term.</param>
    /// <param name="terms">The other terms.</param>
    /// <returns>The chain.</returns>
    internal static Expression Family(string shape, Expression head, IReadOnlyList<Expression> terms) => shape switch
    {
        // head - (t1 - (t2 - (… - tn)))
        "right" => Expression.Subtract(head, terms.Reverse().Aggregate((right, left) => Expression.Subtract(left, right))),
        // head - ((t1 + t2) + (t3 + t4)) …
        "balanced" => Expression.Subtract(head, Balanced(terms, 0, terms.Count, mixed: false, level: 0)),
        // head - ((t1 - t2) + (t3 - t4)) …
        "balancedMixed" => Expression.Subtract(head, Balanced(terms, 0, terms.Count, mixed: true, level: 0)),
        // head - t1 - t2 - … - tn
        "left" => terms.Aggregate(head, (left, right) => Expression.Subtract(left, right)),
        // head - t1 + t2 - t3 + …: the signs of "right"
        _ => terms.Select((t, i) => (t, i)).Aggregate(head, (left, p) => p.i % 2 == 0 ? Expression.Subtract(left, p.t) : Expression.Add(left, p.t)),
    };

    /// <summary>A balanced tree over <c>terms[from..to)</c>, whose odd levels subtract their right operand when <paramref name="mixed"/>.</summary>
    /// <param name="terms">The terms.</param>
    /// <param name="from">The first term.</param>
    /// <param name="to">The end of the range.</param>
    /// <param name="mixed">Whether odd levels subtract.</param>
    /// <param name="level">The level of the node.</param>
    /// <returns>The tree.</returns>
    private static Expression Balanced(IReadOnlyList<Expression> terms, int from, int to, bool mixed, int level)
    {
        if (to - from == 1) return terms[from];
        int middle = from + (to - from) / 2;
        Expression left = Balanced(terms, from, middle, mixed, level + 1);
        Expression right = Balanced(terms, middle, to, mixed, level + 1);
        return mixed && level % 2 == 1 ? Expression.Subtract(left, right) : Expression.Add(left, right);
    }

    /// <summary>Whether the root of <paramref name="source"/> is admitted as a grouped quotient chain, asked inside an operation.</summary>
    /// <param name="source">The root.</param>
    /// <returns><see langword="true"/> when admitted.</returns>
    private static bool AdmittedAsGrouped(Expression source)
    {
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        try
        {
            var simplifier = new ExpressionSimplifier();
            ExpressionRuleRejection rejection = ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, simplifier, null, null, (simplifier, source), static state => state.simplifier.FlattenAdmission(state.source));
            return rejection == ExpressionRuleRejection.None && statistics.GroupedQuotient == 1;
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }
    }

    /// <summary>
    /// Right-associated, balanced and mixed groups, over shared and distinct denominators, in <see cref="double"/> and
    /// <see cref="float"/>, of 2 to 12 quotients: the root is admitted and the result is the historical structure, under every
    /// policy, after one and two passes.
    /// </summary>
    [TestMethod]
    public void Families_AreAdmitted_AndGiveTheHistoricalStructure()
    {
        foreach (Type type in new[] { typeof(double), typeof(float) })
        {
            foreach (string shape in new[] { "right", "balanced", "balancedMixed" })
            {
                foreach (bool distinct in new[] { false, true })
                {
                    for (int n = 2; n <= 12; n++)
                    {
                        Expression source = Family(shape, One(type), Quotients(type, n, distinct));
                        string label = $"{type.Name} {shape} {(distinct ? "distinct" : "shared")} {n}";
                        Assert.IsTrue(AdmittedAsGrouped(source), label);
                        foreach (ExpressionSimplifierOptions policy in Policies)
                        {
                            Assert.AreEqual(Dump(Reference(source, policy)), Dump(Counted(source, policy).Result), label + " under " + policy);
                        }

                        Expression result = new ExpressionSimplifier().Simplify(source);
                        Assert.AreEqual(Dump(Reference(Reference(source))), Dump(new ExpressionSimplifier().Simplify(result)), label + " (second pass)");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Groups of 8 to 128 quotients give exactly the result of the linear chain of the same signed terms (§15, its proof does
    /// not depend on this family): a right-associated group the alternating chain, a balanced group the left-associated
    /// difference; the historical treatment is the reference up to 16 quotients.
    /// </summary>
    [TestMethod]
    public void LargeFamilies_GiveTheLinearChainResult()
    {
        foreach (int n in new[] { 8, 16, 32, 64, 128 })
        {
            foreach (bool distinct in new[] { false, true })
            {
                Expression[] quotients = Quotients(typeof(double), n, distinct);
                Expression one = One(typeof(double));
                foreach (string grouped in new[] { "right", "balanced", "balancedMixed" })
                {
                    Expression source = Family(grouped, one, quotients);
                    (Expression result, FlattenAdmissionStatistics statistics) = Counted(source);
                    Assert.AreEqual(1, statistics.GroupedChains, $"{grouped} {n}");
                    Assert.AreEqual(Dump(new ExpressionSimplifier().Simplify(LinearEquivalent(source))), Dump(result), $"{grouped} {n}");
                    if (n <= 16) Assert.AreEqual(Dump(Reference(source)), Dump(result), $"{grouped} {n} (reference)");
                }
            }
        }
    }

    /// <summary>
    /// The chains of the brief that the family refuses: the root is not admitted, and the result is the historical one.
    /// </summary>
    [TestMethod]
    public void RefusedChains_KeepTheHistoricalTreatment()
    {
        ParameterExpression[] x = Parameters(typeof(double), 6, "x");
        Expression one = Expression.Constant(1.0);
        Expression Q(int i, int j) => Expression.Divide(x[i], x[j]);
        static Expression Right(Expression head, params Expression[] terms) => Family("right", head, terms);
        ParameterExpression[] l = Parameters(typeof(long), 4, "l");
        ParameterExpression[] i32 = Parameters(typeof(int), 4, "i");
        ParameterExpression[] m = Parameters(typeof(decimal), 4, "m");
        ParameterExpression[] n = Parameters(typeof(double?), 4, "n");
        MethodInfo observed = typeof(ExpressionSimplifierGroupedQuotientChainTests).GetMethod(nameof(Observed))!;
        MethodInfo divide = typeof(ExpressionSimplifierGroupedQuotientChainTests).GetMethod(nameof(UserDivide))!;
        MethodInfo add = typeof(ExpressionSimplifierGroupedQuotientChainTests).GetMethod(nameof(UserAdd))!;
        Expression shared = Expression.Subtract(Q(1, 0), Q(2, 0));
        var cases = new (string Label, Expression Source)[]
        {
            ("repeated numerator", Right(one, Q(1, 0), Q(2, 0), Q(1, 0))),
            ("numerator reused as a denominator", Right(one, Q(1, 0), Q(2, 1))),
            ("denominator reused as a numerator", Right(one, Q(1, 0), Q(0, 2))),
            ("denominator reused as a term", Right(one, Q(1, 0), Q(2, 0), x[0])),
            ("denominator reused as a factor", Right(one, Q(1, 0), Expression.Multiply(Expression.Constant(3.0), x[0]))),
            ("two constants", Right(one, Q(1, 0), Expression.Constant(2.0))),
            ("NaN constant", Right(Expression.Constant(double.NaN), Q(1, 0), Q(2, 0))),
            ("no quotient", Right(x[1], x[2], x[3])),
            ("constant numerator", Right(one, Q(1, 0), Expression.Divide(Expression.Constant(2.0), x[0]))),
            ("constant denominator", Right(one, Q(1, 0), Expression.Divide(x[2], Expression.Constant(2.0)))),
            ("sum numerator", Right(one, Q(1, 0), Expression.Divide(Expression.Add(x[2], x[3]), x[0]))),
            ("product denominator", Right(one, Q(1, 0), Expression.Divide(x[2], Expression.Multiply(x[3], x[4])))),
            ("nested division", Right(one, Q(1, 0), Expression.Divide(Q(2, 3), x[0]))),
            ("negated quotient", Right(one, Q(1, 0), Expression.Negate(Q(2, 0)))),
            ("custom division", Right(one, Q(1, 0), Expression.Divide(x[2], x[0], divide))),
            ("custom crossed operator", Expression.Subtract(one, Expression.Add(Q(1, 0), Q(2, 0), add))),
            ("checked crossed operator", Expression.Subtract(one, Expression.SubtractChecked(Q(1, 0), Q(2, 0)))),
            ("effectful call", Right(one, Q(1, 0), Expression.Call(observed, x[2]))),
            ("effectful numerator", Right(one, Q(1, 0), Expression.Divide(Expression.Call(observed, x[2]), x[0]))),
            ("shared group", Expression.Subtract(one, Expression.Add(shared, shared))),
            ("long quotients", Right(One(typeof(long)), Expression.Divide(l[1], l[0]), Expression.Divide(l[2], l[0]))),
            ("int quotients", Right(One(typeof(int)), Expression.Divide(i32[1], i32[0]), Expression.Divide(i32[2], i32[0]))),
            ("decimal quotients", Right(One(typeof(decimal)), Expression.Divide(m[1], m[0]), Expression.Divide(m[2], m[0]))),
            ("lifted quotients", Right(Expression.Constant(1.0, typeof(double?)), Expression.Divide(n[1], n[0]), Expression.Divide(n[2], n[0]))),
        };

        foreach ((string label, Expression source) in cases)
        {
            Assert.IsFalse(AdmittedAsGrouped(source), label);
            Assert.AreEqual(Dump(Reference(source)), Dump(Counted(source).Result), label);
        }
    }

    /// <summary>A user function with an effect: it logs its argument.</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double Observed(double value)
    {
        (s_log ??= []).Add(value);
        return value;
    }

    /// <summary>The log of <see cref="Observed"/>.</summary>
    [ThreadStatic]
    private static List<double>? s_log;

    /// <summary>A user division operator.</summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>The quotient.</returns>
    public static double UserDivide(double left, double right) => left / right;

    /// <summary>A user addition operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum.</returns>
    public static double UserAdd(double left, double right) => left + right;

    /// <summary>
    /// A derived simplifier keeps the level by level treatment and its own rules: a division rule of its own sees every quotient
    /// as often as before, its admission never consults the family, and its result is the historical one of its own type.
    /// </summary>
    [TestMethod]
    public void DerivedSimplifiers_KeepTheirRules()
    {
        Expression source = Family("right", One(typeof(double)), Quotients(typeof(double), 8, false));
        var statistics = new FlattenAdmissionStatistics();
        ExpressionSimplifier.AdmissionStatistics = statistics;
        int divisions;
        string derived;
        try
        {
            var dividing = new DividingSimplifier();
            derived = Dump(dividing.Simplify(source));
            divisions = dividing.Divisions;
        }
        finally
        {
            ExpressionSimplifier.AdmissionStatistics = null;
        }

        Assert.AreEqual(0, statistics.GroupedWalks + statistics.GroupedQuotient);
        ExpressionSimplifier.GroupedQuotientChainsDisabled = true;
        try
        {
            var reference = new DividingSimplifier();
            Assert.AreEqual(Dump(reference.Simplify(source)), derived);
            Assert.AreEqual(reference.Divisions, divisions);
        }
        finally
        {
            ExpressionSimplifier.GroupedQuotientChainsDisabled = false;
        }

        Assert.IsTrue(divisions >= 8, divisions.ToString(CultureInfo.InvariantCulture));

        // A derived simplifier whose finalization differs keeps it on every level.
        var finalizing = new FinalizingSimplifier();
        finalizing.Simplify(source);
        Assert.IsTrue(finalizing.Finalizations > 8, finalizing.Finalizations.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>A derived simplifier with a division rule of its own.</summary>
    private sealed class DividingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the number of divisions the rule saw.</summary>
        public int Divisions { get; private set; }

        /// <summary>Counts the divisions and lets the other rules run.</summary>
        /// <param name="e">The division.</param>
        /// <param name="left">The dividend.</param>
        /// <param name="right">The divisor.</param>
        /// <returns><see langword="null"/>.</returns>
        [ExpressionSignature(ExpressionType.Divide)]
        protected Expression? CountDivision(BinaryExpression e, Expression left, Expression right)
        {
            Divisions++;
            return null;
        }
    }

    /// <summary>A derived simplifier whose finalization is observed.</summary>
    private sealed class FinalizingSimplifier : ExpressionSimplifier
    {
        /// <summary>Gets the number of finalizations.</summary>
        public int Finalizations { get; private set; }

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            Finalizations++;
            return base.FinalizeExpression(e, parameters);
        }
    }

    /// <summary>
    /// The values of an admitted chain, on finite, zero, negative zero, infinite, NaN, tiny and huge operands and zero divisors:
    /// bit for bit those of the historical result (the same structure), and within the reassociation latitude of the source.
    /// </summary>
    [TestMethod]
    public void Values_AreThoseOfTheHistoricalResult()
    {
        double[] samples = [3.0, -2.0, 0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, double.Epsilon, 1e-310, 1e300, -7.5];
        foreach (string shape in new[] { "right", "balanced", "balancedMixed" })
        {
            Expression[] quotients = Quotients(typeof(double), 6, false);
            Expression source = Family(shape, One(typeof(double)), quotients);
            ParameterExpression[] parameters = [.. quotients.Select(q => (ParameterExpression)((BinaryExpression)q).Left).Prepend((ParameterExpression)((BinaryExpression)quotients[0]).Right)];
            var f = (Func<object[], double>)(values => (double)Expression.Lambda(source, parameters).Compile().DynamicInvoke(values)!);
            Delegate g = Expression.Lambda(new ExpressionSimplifier().Simplify(source), parameters).Compile();
            Delegate h = Expression.Lambda(Reference(source), parameters).Compile();
            var random = new Random(shape.Length);
            for (int sample = 0; sample < 400; sample++)
            {
                object[] values = [.. parameters.Select(_ => (object)samples[random.Next(samples.Length)])];
                double actual = (double)g.DynamicInvoke(values)!;
                Assert.AreEqual(BitConverter.DoubleToInt64Bits((double)h.DynamicInvoke(values)!), BitConverter.DoubleToInt64Bits(actual), shape);
                double expected = f(values);
                // The S3 latitude: a reassociated sum may round differently, and with extreme operands (a huge value, or a tiny
                // divisor making a huge quotient) it may cancel, overflow or produce NaN at another point than the source.
                bool extreme = values.Any(static v => Math.Abs((double)v) > 1e290 || ((double)v != 0 && Math.Abs((double)v) < 1e-290));
                bool close = extreme || expected.Equals(actual) || !double.IsFinite(expected) || !double.IsFinite(actual)
                    || Math.Abs(expected) > 1e290 || Math.Abs(expected - actual) <= 1e-9 * Math.Max(1, Math.Abs(expected));
                Assert.IsTrue(close, $"{shape}: {expected} -> {actual}");
            }
        }
    }

    /// <summary>
    /// A grouped chain next to observable evaluations: the evaluations happen as often and in the order of the source, and
    /// the simplification itself runs no user code.
    /// </summary>
    [TestMethod]
    public void ObservableEvaluations_AreKept()
    {
        Expression[] quotients = Quotients(typeof(double), 4, false);
        ParameterExpression x0 = (ParameterExpression)((BinaryExpression)quotients[0]).Right;
        ParameterExpression[] parameters = [x0, .. quotients.Select(q => (ParameterExpression)((BinaryExpression)q).Left)];
        MethodInfo observed = typeof(ExpressionSimplifierGroupedQuotientChainTests).GetMethod(nameof(Observed))!;
        Expression group = Family("right", One(typeof(double)), quotients);
        Expression[] sources =
        [
            Expression.Add(Expression.Call(observed, parameters[1]), group),
            Expression.Subtract(group, Expression.Call(observed, parameters[2])),
            Expression.Multiply(Expression.Call(observed, parameters[3]), Expression.Subtract(group, Expression.Call(observed, parameters[4]))),
        ];

        foreach (Expression source in sources)
        {
            s_log = [];
            Expression result = new ExpressionSimplifier().Simplify(source);
            Assert.AreEqual(0, s_log.Count, "the simplification runs no user code");
            Assert.AreEqual(Dump(Reference(source)), Dump(result));
            Delegate f = Expression.Lambda(source, parameters).Compile();
            Delegate g = Expression.Lambda(result, parameters).Compile();
            object[] values = [2.0, 3.0, 5.0, 7.0, 11.0];
            s_log = [];
            f.DynamicInvoke(values);
            List<double> expected = s_log;
            s_log = [];
            g.DynamicInvoke(values);
            CollectionAssert.AreEqual(expected, s_log, source.ToString());
        }
    }

    /// <summary>
    /// The linear chain of the signed terms of an additive tree: its terms, signs propagated through the subtracted groups, in
    /// source order, combined from left to right (<c>t1 ± t2 ± … ± tn</c>), the same term instances. Iterative.
    /// </summary>
    /// <param name="source">An additive tree whose leftmost term is added.</param>
    /// <returns>The linear chain.</returns>
    internal static Expression LinearEquivalent(Expression source)
    {
        var terms = new List<(Expression Term, bool Negative)>();
        var pending = new Stack<(Expression Node, bool Negative)>();
        pending.Push((source, false));
        while (pending.Count > 0)
        {
            (Expression node, bool negative) = pending.Pop();
            if (node is BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Subtract, Method: null } binary)
            {
                pending.Push((binary.Right, negative ^ (binary.NodeType == ExpressionType.Subtract)));
                pending.Push((binary.Left, negative));
            }
            else
            {
                terms.Add((node, negative));
            }
        }

        Expression chain = terms[0].Term;
        for (int i = 1; i < terms.Count; i++) chain = terms[i].Negative ? Expression.Subtract(chain, terms[i].Term) : Expression.Add(chain, terms[i].Term);
        return chain;
    }

    /// <summary>Simplifies <paramref name="source"/> without limits and returns its transformation and structural visits.</summary>
    /// <param name="source">The tree.</param>
    /// <returns>The visits.</returns>
    private static (long Transform, long Structural) Visits(Expression source)
        => ExpressionResourceBudget.Run(ExpressionResourceLimits.Unlimited, null, null, null, source, static e =>
        {
            new ExpressionSimplifier(ExpressionResourceLimits.Unlimited).Simplify(e);
            return (ExpressionResourceBudget.TransformVisits, ExpressionResourceBudget.StructuralVisits);
        });

    /// <summary>
    /// The work is linear: <c>3n + 2</c> transformation visits for every shape (one for the root, one per term and per operand
    /// of a quotient), and the structural visits of the linear chain of the same signed terms (the extraction, the keys and the
    /// <c>n log n</c> comparisons of the sort of the canonicalization, §15), plus the one walk of the admission.
    /// </summary>
    [TestMethod]
    public void Work_IsLinear()
    {
        foreach (string grouped in new[] { "right", "balanced", "balancedMixed" })
        {
            long previous = 0;
            foreach (int n in new[] { 32, 64, 128, 256, 512 })
            {
                Expression source = Family(grouped, One(typeof(double)), Quotients(typeof(double), n, false));
                (long transform, long structural) = Visits(source);
                (long linearTransform, long linearStructural) = Visits(LinearEquivalent(source));
                Assert.AreEqual(3 * n + 2, transform, $"{grouped} {n}");
                Assert.AreEqual(linearTransform, transform, $"{grouped} {n}");
                Assert.IsTrue(structural <= linearStructural + 2 * n + 2, $"{grouped} {n}: {structural} structural visits, {linearStructural} for the linear chain");
                if (previous > 0) Assert.IsTrue(structural < 2.8 * previous, $"{grouped} {n}: {previous} then {structural} structural visits");
                previous = structural;
            }
        }
    }

    /// <summary>
    /// The comparer, which simplifies both operands, finds a grouped chain equal to the linear chain of the same signed terms,
    /// with the same hash, and different from a chain that differs by one sign.
    /// </summary>
    [TestMethod]
    public void Comparer_IsConsistent()
    {
        foreach (int n in new[] { 4, 9, 33 })
        {
            Expression[] quotients = Quotients(typeof(double), n, false);
            Expression grouped = Family("right", One(typeof(double)), quotients);
            Expression linear = Family("alternating", One(typeof(double)), quotients);
            Expression other = Family("left", One(typeof(double)), quotients);
            Assert.IsTrue(ExpressionComparer.Default.Equals(grouped, linear), n.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual(ExpressionComparer.Default.GetHashCode(grouped), ExpressionComparer.Default.GetHashCode(linear));
            Assert.IsFalse(ExpressionComparer.Default.Equals(grouped, other));
            Assert.IsTrue(ExpressionComparer.Default.Equals(grouped, grouped));
        }
    }

    /// <summary>
    /// Concurrent calls on one instance, a nested public call from user code between the admission and the rule of an outer
    /// chain, and an instance reused after a budget refusal: each call gives the same result, and the switch of the family is
    /// per thread.
    /// </summary>
    [TestMethod]
    public void Concurrency_Reentrancy_AndAmbientState()
    {
        Expression source = Family("balancedMixed", One(typeof(double)), Quotients(typeof(double), 24, false));
        string expected = Dump(new ExpressionSimplifier().Simplify(source));
        var shared = new ExpressionSimplifier();
        System.Threading.Tasks.Parallel.For(0, 32, _ => Assert.AreEqual(expected, Dump(shared.Simplify(source))));

        // A grouped chain whose comparisons run user code that simplifies another grouped chain.
        s_nested = (shared, Family("right", One(typeof(double)), Quotients(typeof(double), 6, true)));
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        Expression carrier = Expression.Call(typeof(ExpressionSimplifierGroupedQuotientChainTests).GetMethod(nameof(Unbox))!, Expression.Constant(new Reentrant(), typeof(object)));
        Expression withReentry = Expression.Subtract(Expression.Add(source, Expression.Multiply(carrier, z)), Expression.Multiply(carrier, z));
        shared.Simplify(withReentry);
        s_nested = null;
        Assert.AreEqual(expected, Dump(shared.Simplify(source)));

        // A refusal between the admission and the rule leaves nothing behind.
        long transform = Visits(source).Transform;
        var limited = new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxTransformVisits = transform - 1 });
        Assert.ThrowsExactly<ExpressionResourceLimitException>(() => limited.Simplify(source));
        Assert.AreEqual(0, ExpressionResourceBudget.OperationDepth);
        Assert.AreEqual(expected, Dump(new ExpressionSimplifier(ExpressionResourceLimits.Unlimited with { MaxTransformVisits = transform }).Simplify(source)));

        // The switch is per thread.
        ExpressionSimplifier.GroupedQuotientChainsDisabled = true;
        try
        {
            // A dedicated thread: a task could run inline on this one.
            bool other = true;
            var thread = new System.Threading.Thread(() => other = ExpressionSimplifier.GroupedQuotientChainsDisabled);
            thread.Start();
            thread.Join();
            Assert.IsFalse(other);
        }
        finally
        {
            ExpressionSimplifier.GroupedQuotientChainsDisabled = false;
        }
    }

    /// <summary>The simplifier and the tree a <see cref="Reentrant"/> value simplifies.</summary>
    [ThreadStatic]
    private static (ExpressionSimplifier Simplifier, Expression Source)? s_nested;

    /// <summary>A value whose equality simplifies a grouped chain, from user code.</summary>
    private sealed class Reentrant
    {
        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (s_nested is { } nested) nested.Simplifier.Simplify(nested.Source);
            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => 1;
    }

    /// <summary>Turns a value into a number.</summary>
    /// <param name="value">The value.</param>
    /// <returns><c>1</c>.</returns>
    public static double Unbox(object value) => 1;
}
