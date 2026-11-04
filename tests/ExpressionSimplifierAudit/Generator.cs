using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ExpressionSimplifierAudit;

/// <summary>The numeric types of the generated campaigns, with their parameters, constants and evaluation values.</summary>
internal sealed class NumericDomain
{
    /// <summary>Creates a domain.</summary>
    /// <param name="type">The expression type.</param>
    /// <param name="constants">The constants the generator draws.</param>
    /// <param name="values">The values the differential evaluation assigns to the parameters.</param>
    private NumericDomain(Type type, object[] constants, object?[] values)
    {
        Type = type;
        Parameters = new[] { "x", "y", "z" }.Select(n => Expression.Parameter(type, n)).ToArray();
        Constants = constants;
        Values = values;
    }

    /// <summary>Gets the expression type.</summary>
    public Type Type { get; }

    /// <summary>Gets the three parameters.</summary>
    public ParameterExpression[] Parameters { get; }

    /// <summary>Gets the constants.</summary>
    public object[] Constants { get; }

    /// <summary>Gets the evaluation values.</summary>
    public object?[] Values { get; }

    /// <summary>Gets whether the type is <see cref="double"/> (powers and functions allowed).</summary>
    public bool IsDouble => Type == typeof(double);

    /// <summary>Gets whether the type is a floating-point type.</summary>
    public bool IsFloating => Type == typeof(double) || Type == typeof(float);

    /// <summary>Gets whether the type has an ordinary negation.</summary>
    public bool HasNegate => Type != typeof(uint) && Type != typeof(ulong);

    /// <summary>The domains, by name.</summary>
    public static readonly Dictionary<string, NumericDomain> All = new()
    {
        ["double"] = new(typeof(double),
            [0.0, 1.0, -1.0, 2.0, -2.0, 0.5, 3.0, 10.0, 1e300],
            [0.0, -0.0, 1.0, -1.0, 2.0, 0.5, -3.0, 7.0, 1e300, -1e300, double.Epsilon, 1e-310, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue]),
        ["float"] = new(typeof(float),
            [0f, 1f, -1f, 2f, -2f, 0.5f, 3f, 10f],
            [0f, -0f, 1f, -1f, 2f, 0.5f, -3f, 7f, 3e38f, float.Epsilon, float.NaN, float.PositiveInfinity, float.NegativeInfinity]),
        ["int"] = new(typeof(int),
            [0, 1, -1, 2, -2, 3, 10],
            [0, 1, -1, 2, -2, 3, 7, int.MaxValue, int.MinValue, 46341]),
        ["long"] = new(typeof(long),
            [0L, 1L, -1L, 2L, -2L, 3L, 10L],
            [0L, 1L, -1L, 2L, -3L, 7L, long.MaxValue, long.MinValue]),
        ["decimal"] = new(typeof(decimal),
            [0m, 1m, -1m, 2m, -2m, 0.5m, 3m, 10m, 1.0m],
            [0m, 1m, -1m, 2m, 0.5m, -3m, 7m, 1.00m, decimal.MaxValue, decimal.MinValue, 0.0000000000000000000000000001m]),
        ["int?"] = new(typeof(int?),
            [0, 1, -1, 2, 3],
            [null, 0, 1, -1, 2, 7, int.MinValue]),
        ["uint"] = new(typeof(uint),
            [0u, 1u, 2u, 3u, 10u],
            [0u, 1u, 2u, 7u, uint.MaxValue]),
    };

    /// <summary>Builds the constant <paramref name="value"/> of this type.</summary>
    /// <param name="value">The value (converted to the underlying type).</param>
    /// <returns>The constant.</returns>
    public Expression Constant(object value)
    {
        Type underlying = Nullable.GetUnderlyingType(Type) ?? Type;
        return Expression.Constant(Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture), Type);
    }
}

/// <summary>Options of the seeded generator.</summary>
internal sealed record GeneratorOptions
{
    /// <summary>Gets the maximum depth.</summary>
    public int MaxDepth { get; init; } = 4;

    /// <summary>Gets the probability weight of the hard factoring/division shapes of the brief.</summary>
    public int HardWeight { get; init; } = 4;

    /// <summary>Gets whether a node may reuse an earlier subtree (a DAG).</summary>
    public bool Shared { get; init; } = true;

    /// <summary>Gets whether effectful leaves (calls, getters, throws, assignments) are drawn.</summary>
    public bool Effects { get; init; }
}

/// <summary>
/// A seeded generator of arithmetic trees weighted towards the shapes the factoring, subtraction and division rules
/// rewrite (brief §11): the same seed and options always build the same tree.
/// </summary>
internal sealed class TreeGenerator
{
    /// <summary>The random source.</summary>
    private readonly Random _random;

    /// <summary>The numeric domain.</summary>
    private readonly NumericDomain _domain;

    /// <summary>The options.</summary>
    private readonly GeneratorOptions _options;

    /// <summary>The subtrees built so far (for shared nodes).</summary>
    private readonly List<Expression> _built = new();

    /// <summary>The <see cref="double"/> functions drawn by the generator.</summary>
    private static readonly MethodInfo[] Functions =
        new[] { "Sin", "Cos", "Log", "Sqrt", "Tan" }.Select(n => typeof(double).GetMethod(n, [typeof(double)])!).ToArray();

    /// <summary>Creates a generator.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="domain">The numeric domain.</param>
    /// <param name="options">The options.</param>
    public TreeGenerator(int seed, NumericDomain domain, GeneratorOptions options)
    {
        _random = new Random(seed);
        _domain = domain;
        _options = options;
    }

    /// <summary>Builds one tree.</summary>
    /// <returns>The tree.</returns>
    public Expression Build() => Node(_options.MaxDepth);

    /// <summary>Builds a node of the domain type.</summary>
    /// <param name="depth">The remaining depth.</param>
    /// <returns>The node.</returns>
    private Expression Node(int depth)
    {
        Expression result = depth <= 0 || _random.Next(5) == 0 ? Leaf() : Inner(depth);
        _built.Add(result);
        return result;
    }

    /// <summary>Builds a leaf: a parameter, a constant or (for the effect campaign) an effectful leaf.</summary>
    /// <returns>The leaf.</returns>
    private Expression Leaf()
    {
        if (_options.Effects && _domain.IsDouble && _random.Next(3) == 0) return EffectLeaves.Draw(_random);
        return _random.Next(5) < 3
            ? _domain.Parameters[_random.Next(_domain.Parameters.Length)]
            : _domain.Constant(_domain.Constants[_random.Next(_domain.Constants.Length)]);
    }

    /// <summary>Builds an inner node.</summary>
    /// <param name="depth">The remaining depth.</param>
    /// <returns>The node.</returns>
    private Expression Inner(int depth)
    {
        int shared = _options.Shared && _built.Count > 0 ? 1 : 0;
        int functions = _domain.IsDouble ? 1 : 0;
        int total = 6 + 2 + functions + (_domain.HasNegate ? 1 : 0) + _options.HardWeight + shared + functions;
        int pick = _random.Next(total);
        if ((pick -= 6) < 0)
        {
            Expression l = Node(depth - 1), r = Node(depth - 1);
            return (pick + 6) switch { 0 or 1 => Expression.Add(l, r), 2 or 3 => Expression.Subtract(l, r), _ => Expression.Multiply(l, r) };
        }

        if ((pick -= 2) < 0) return Expression.Divide(Node(depth - 1), Node(depth - 1));
        if (functions == 1 && (pick -= 1) < 0) return Expression.Power(Node(depth - 1), _random.Next(2) == 0 ? Node(depth - 1) : _domain.Constant(_random.Next(-2, 4)));
        if (_domain.HasNegate && (pick -= 1) < 0) return Expression.Negate(Node(depth - 1));
        if ((pick -= _options.HardWeight) < 0) return Hard(depth);
        if (shared == 1 && (pick -= 1) < 0) return _built[_random.Next(_built.Count)];
        return Expression.Call(Functions[_random.Next(Functions.Length)], Node(depth - 1));
    }

    /// <summary>Builds one of the hard shapes of the brief, with shared factors.</summary>
    /// <param name="depth">The remaining depth.</param>
    /// <returns>The node.</returns>
    private Expression Hard(int depth)
    {
        Expression a = Node(depth - 2), b = Node(depth - 2), c = Node(depth - 2), x = Node(depth - 2);
        Expression one = _domain.Constant(1);
        int shapes = _domain.IsDouble ? 15 : 11;
        switch (_random.Next(shapes))
        {
            case 0: return Expression.Add(Expression.Multiply(a, x), Expression.Multiply(b, x));
            case 1: return Expression.Subtract(Expression.Multiply(a, x), Expression.Multiply(b, x));
            case 2: return Expression.Multiply(c, Expression.Subtract(a, b));
            case 3: return Expression.Divide(Expression.Divide(a, b), Expression.Divide(c, x));
            case 4: return Expression.Subtract(Expression.Subtract(Expression.Subtract(a, b), c), x);
            case 5: return Expression.Subtract(a, Expression.Subtract(b, Expression.Subtract(c, x)));
            case 6: return Expression.Divide(x, Expression.Divide(one, x));
            case 7: return Expression.Divide(Expression.Subtract(one, b), Expression.Divide(one, b));
            case 8: return Expression.Add(Expression.Multiply(a, x), Expression.Multiply(Expression.Subtract(one, a), x));
            case 9: return Expression.Divide(x, x);
            case 10: return Expression.Subtract(x, x);
            case 11: return Expression.Multiply(Expression.Power(x, _domain.Constant(2)), Expression.Power(x, _domain.Constant(3)));
            case 12: return Expression.Power(Expression.Power(x, a), b);
            case 13:
                MethodInfo sin = typeof(double).GetMethod("Sin", [typeof(double)])!, cos = typeof(double).GetMethod("Cos", [typeof(double)])!;
                return Expression.Add(Expression.Power(Expression.Call(sin, x), _domain.Constant(2)), Expression.Power(Expression.Call(cos, x), _domain.Constant(2)));
            default:
                MethodInfo log = typeof(double).GetMethod("Log", [typeof(double)])!;
                return Expression.Add(Expression.Call(log, a), Expression.Call(log, b));
        }
    }
}

/// <summary>Effectful leaves for the observable-effect campaign: every evaluation is logged.</summary>
public static class EffectLeaves
{
    /// <summary>The evaluation log of the current run.</summary>
    public static readonly List<string> Log = new();

    /// <summary>The counter returned by <see cref="Next"/>.</summary>
    private static int s_counter;

    /// <summary>The methods and property drawn by <see cref="Draw"/>.</summary>
    private static readonly MemberInfo[] Members =
    [
        typeof(EffectLeaves).GetMethod(nameof(Next))!,
        typeof(EffectLeaves).GetMethod(nameof(Two))!,
        typeof(EffectLeaves).GetProperty(nameof(Spy))!,
        typeof(EffectLeaves).GetMethod(nameof(Fail))!,
    ];

    /// <summary>Resets the log and the counter.</summary>
    public static void Reset()
    {
        Log.Clear();
        s_counter = 0;
    }

    /// <summary>Returns 1, 2, 3, ... (a different value at each call) and logs the call.</summary>
    /// <returns>The next value.</returns>
    public static double Next()
    {
        Log.Add("next");
        return ++s_counter;
    }

    /// <summary>Returns 2 and logs the call.</summary>
    /// <returns>2.</returns>
    public static double Two()
    {
        Log.Add("two");
        return 2.0;
    }

    /// <summary>Gets 3 and logs the read (a getter with an effect).</summary>
    public static double Spy
    {
        get
        {
            Log.Add("spy");
            return 3.0;
        }
    }

    /// <summary>Logs the call and throws.</summary>
    /// <returns>Never returns.</returns>
    public static double Fail()
    {
        Log.Add("fail");
        throw new InvalidOperationException("Fail");
    }

    /// <summary>Draws one effectful leaf (the throwing one rarely).</summary>
    /// <param name="random">The random source.</param>
    /// <returns>The leaf.</returns>
    public static Expression Draw(Random random)
    {
        int pick = random.Next(10);
        MemberInfo member = pick < 4 ? Members[0] : pick < 7 ? Members[1] : pick < 9 ? Members[2] : Members[3];
        return member is PropertyInfo property ? Expression.Property(null, property) : Expression.Call((MethodInfo)member);
    }
}

/// <summary>Reduces a counterexample tree while a property keeps holding (greedy subtree replacement).</summary>
internal static class Reducer
{
    /// <summary>
    /// Repeatedly replaces a node by one of its children of the same type, by a parameter or by the constants 0 and 1, keeping
    /// a replacement when <paramref name="holds"/> is still true, until no replacement succeeds.
    /// </summary>
    /// <param name="tree">The counterexample.</param>
    /// <param name="domain">Its numeric domain (for the replacement leaves).</param>
    /// <param name="holds">The property to preserve.</param>
    /// <param name="maxAttempts">The maximum number of property evaluations.</param>
    /// <returns>The reduced tree.</returns>
    public static Expression Reduce(Expression tree, NumericDomain domain, Func<Expression, bool> holds, int maxAttempts = 400)
    {
        int attempts = 0;
        bool progress = true;
        while (progress && attempts < maxAttempts)
        {
            progress = false;
            foreach (Expression node in Nodes(tree).OrderByDescending(AuditText.Count).ToList())
            {
                foreach (Expression replacement in Replacements(node, domain))
                {
                    if (++attempts > maxAttempts) return tree;
                    Expression candidate;
                    try
                    {
                        candidate = new Replacer(node, replacement).Visit(tree)!;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (AuditText.Count(candidate) < AuditText.Count(tree) && SafeHolds(holds, candidate))
                    {
                        tree = candidate;
                        progress = true;
                        break;
                    }
                }

                if (progress) break;
            }
        }

        return tree;
    }

    /// <summary>Evaluates the property, treating an exception of the property itself as "does not hold".</summary>
    /// <param name="holds">The property.</param>
    /// <param name="candidate">The candidate.</param>
    /// <returns>Whether the property holds.</returns>
    private static bool SafeHolds(Func<Expression, bool> holds, Expression candidate)
    {
        try
        {
            return holds(candidate);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Lists the distinct inner nodes of a tree.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The nodes.</returns>
    private static IEnumerable<Expression> Nodes(Expression tree)
    {
        var seen = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<Expression>();
        stack.Push(tree);
        while (stack.Count > 0)
        {
            Expression e = stack.Pop();
            if (!seen.Add(e)) continue;
            switch (e)
            {
                case BinaryExpression b:
                    yield return e;
                    stack.Push(b.Left);
                    stack.Push(b.Right);
                    break;
                case UnaryExpression u:
                    yield return e;
                    stack.Push(u.Operand);
                    break;
                case MethodCallExpression c:
                    yield return e;
                    foreach (Expression a in c.Arguments) stack.Push(a);
                    break;
                case ConstantExpression or ParameterExpression:
                    yield return e;
                    break;
            }
        }
    }

    /// <summary>The candidate replacements of a node.</summary>
    /// <param name="node">The node.</param>
    /// <param name="domain">The numeric domain.</param>
    /// <returns>The replacements.</returns>
    private static IEnumerable<Expression> Replacements(Expression node, NumericDomain domain)
    {
        IEnumerable<Expression> children = node switch
        {
            BinaryExpression b => [b.Left, b.Right],
            UnaryExpression u => [u.Operand],
            MethodCallExpression c => c.Arguments,
            _ => [],
        };
        foreach (Expression child in children.Where(c => c.Type == node.Type)) yield return child;
        if (node.Type != domain.Type) yield break;
        if (node is not ParameterExpression) yield return domain.Parameters[0];
        if (node is not ConstantExpression) yield return domain.Constant(1);
        if (node is not ConstantExpression) yield return domain.Constant(2);
        if (node is not ConstantExpression) yield return domain.Constant(0);
    }

    /// <summary>Replaces every occurrence of one node by another.</summary>
    /// <param name="target">The node to replace.</param>
    /// <param name="replacement">Its replacement.</param>
    private sealed class Replacer(Expression target, Expression replacement) : ExpressionVisitor
    {
        /// <inheritdoc/>
        public override Expression? Visit(Expression? node) => ReferenceEquals(node, target) ? replacement : base.Visit(node);
    }
}

/// <summary>Compiles and evaluates a source tree and its simplified form, and classifies their differences.</summary>
internal static class DiffEval
{
    /// <summary>The outcome of one evaluation: a value or an exception type.</summary>
    /// <param name="Value">The value (when no exception).</param>
    /// <param name="Exception">The exception type name, or <see langword="null"/>.</param>
    public readonly record struct Outcome(object? Value, string? Exception);

    /// <summary>The difference classes, from the least to the most severe.</summary>
    public static readonly string[] Classes = ["same", "rounding", "signedZero", "ieee", "extreme", "VALUE", "EXC-ADDED", "EXC-REMOVED", "EXC-CHANGED"];

    /// <summary>Compiles a lambda over the domain parameters.</summary>
    /// <param name="body">The body.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="interpret">Whether to use the interpreter instead of the compiler.</param>
    /// <returns>The delegate.</returns>
    public static Delegate Compile(Expression body, NumericDomain domain, bool interpret = false)
        => Expression.Lambda(body, domain.Parameters).Compile(interpret);

    /// <summary>Evaluates a delegate on one assignment of the parameters.</summary>
    /// <param name="function">The delegate.</param>
    /// <param name="arguments">The parameter values.</param>
    /// <returns>The outcome.</returns>
    public static Outcome Evaluate(Delegate function, object?[] arguments)
    {
        try
        {
            return new Outcome(function.DynamicInvoke(arguments), null);
        }
        catch (TargetInvocationException exception)
        {
            return new Outcome(null, AuditText.Innermost(exception).GetType().Name);
        }
    }

    /// <summary>Builds the parameter assignments of a seed: a fixed moderate set, then values drawn from the domain pool.</summary>
    /// <param name="domain">The domain.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="count">The number of drawn assignments.</param>
    /// <returns>The assignments.</returns>
    public static List<object?[]> Assignments(NumericDomain domain, int seed, int count)
    {
        var random = new Random(seed ^ 0x5A5A);
        var result = new List<object?[]>
        {
            new[] { domain.Values[2 % domain.Values.Length], domain.Values[4 % domain.Values.Length], domain.Values[5 % domain.Values.Length] },
            new[] { domain.Values[4 % domain.Values.Length], domain.Values[3 % domain.Values.Length], domain.Values[2 % domain.Values.Length] },
        };
        for (int i = 0; i < count; i++)
        {
            result.Add([domain.Values[random.Next(domain.Values.Length)], domain.Values[random.Next(domain.Values.Length)], domain.Values[random.Next(domain.Values.Length)]]);
        }

        return result;
    }

    /// <summary>Classifies the difference between a source outcome and a simplified outcome.</summary>
    /// <param name="source">The source outcome.</param>
    /// <param name="simplified">The simplified outcome.</param>
    /// <param name="arguments">The parameter values (to tell moderate inputs from extreme ones).</param>
    /// <param name="extremeConstants">Whether the source holds a constant of extreme magnitude (its value differences are reported as <c>extreme</c>).</param>
    /// <returns>The class (see <see cref="Classes"/>).</returns>
    public static string Classify(Outcome source, Outcome simplified, object?[] arguments, bool extremeConstants = false)
    {
        if (source.Exception is not null || simplified.Exception is not null)
        {
            if (source.Exception == simplified.Exception) return "same";
            if (source.Exception is null) return "EXC-ADDED";
            if (simplified.Exception is null) return "EXC-REMOVED";
            return "EXC-CHANGED";
        }

        object? a = source.Value, b = simplified.Value;
        if (a is null || b is null) return a is null && b is null ? "same" : "VALUE";
        if (a.GetType() != b.GetType()) return "VALUE";
        bool moderate = !extremeConstants && arguments.All(IsModerate);
        switch (a)
        {
            case double x:
                return ClassifyFloating(x, (double)b, moderate, 1e-9);
            case float x:
                return ClassifyFloating(x, (float)b, moderate, 1e-4);
            case decimal x:
                if (x == (decimal)b) return "same";
                return moderate ? "VALUE" : "extreme";
            default:
                return a.Equals(b) ? "same" : moderate ? "VALUE" : "extreme";
        }
    }

    /// <summary>Classifies two floating-point results.</summary>
    /// <param name="a">The source value.</param>
    /// <param name="b">The simplified value.</param>
    /// <param name="moderate">Whether every input is moderate.</param>
    /// <param name="tolerance">The relative difference reported as rounding (wider for <see cref="float"/>).</param>
    /// <returns>The class.</returns>
    private static string ClassifyFloating(double a, double b, bool moderate, double tolerance)
    {
        if (BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b) || (double.IsNaN(a) && double.IsNaN(b))) return "same";
        if (a == b) return "signedZero";
        if (!double.IsFinite(a) || !double.IsFinite(b)) return moderate ? "ieee" : "extreme";
        if (Math.Abs(a - b) <= tolerance * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)))) return "rounding";
        return moderate ? "VALUE" : "extreme";
    }

    /// <summary>Whether a parameter value is moderate: finite, and zero or of magnitude in [1e-3, 1e3] (or null for a nullable).</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a moderate value.</returns>
    private static bool IsModerate(object? value) => value switch
    {
        null => true,
        double d => d == 0 || (double.IsFinite(d) && Math.Abs(d) >= 1e-3 && Math.Abs(d) <= 1e3),
        float f => f == 0 || (float.IsFinite(f) && Math.Abs(f) >= 1e-3f && Math.Abs(f) <= 1e3f),
        decimal m => m == 0 || (Math.Abs(m) >= 0.001m && Math.Abs(m) <= 1000m),
        int i => Math.Abs((long)i) <= 1000,
        long l => l > -1000 && l < 1000,
        uint u => u <= 1000,
        _ => true,
    };

    /// <summary>Whether a tree holds a numeric constant of magnitude above 1e6 (reassociation around it is S3 latitude).</summary>
    /// <param name="e">The tree.</param>
    /// <returns><see langword="true"/> when such a constant occurs.</returns>
    public static bool HasExtremeConstant(Expression? e) => e switch
    {
        ConstantExpression { Value: double d } => Math.Abs(d) > 1e6,
        ConstantExpression { Value: float f } => Math.Abs(f) > 1e6f,
        ConstantExpression { Value: decimal m } => Math.Abs(m) > 1e6m,
        BinaryExpression b => HasExtremeConstant(b.Left) || HasExtremeConstant(b.Right),
        UnaryExpression u => HasExtremeConstant(u.Operand),
        MethodCallExpression c => c.Arguments.Any(HasExtremeConstant),
        _ => false,
    };

    /// <summary>Returns the more severe of two classes.</summary>
    /// <param name="a">The first class.</param>
    /// <param name="b">The second class.</param>
    /// <returns>The more severe class.</returns>
    public static string Worst(string a, string b) => Array.IndexOf(Classes, a) >= Array.IndexOf(Classes, b) ? a : b;
}
