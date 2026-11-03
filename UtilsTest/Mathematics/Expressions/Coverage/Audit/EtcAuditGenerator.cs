using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>The families the audit generator may produce.</summary>
[Flags]
internal enum AuditFamilies
{
    /// <summary>Constants, parameters, arithmetic, comparisons and allow-listed math calls.</summary>
    Arithmetic = 1,

    /// <summary>Traced calls and checks that may throw (observable effects).</summary>
    Effects = 2,

    /// <summary>Conditionals and boolean operators.</summary>
    Conditionals = 4,

    /// <summary>Invoked lambdas (inlining candidates) and closures passed to a call.</summary>
    Lambdas = 8,

    /// <summary>Blocks with variables, assignments and increments.</summary>
    Blocks = 16,

    /// <summary>Try/catch/finally.</summary>
    Try = 32,

    /// <summary>Member and list initializations, index accesses.</summary>
    Initializers = 64,

    /// <summary>Dynamic operations with controlled binders.</summary>
    Dynamic = 128,

    /// <summary>Quoted lambdas applied by a host method.</summary>
    Quote = 256,

    /// <summary>Extension nodes (opaque).</summary>
    Extension = 512,

    /// <summary>Checked conversions that may overflow.</summary>
    Checked = 1024,

    /// <summary>Labels, jumps and switches.</summary>
    ControlFlow = 2048,

    /// <summary>Every family.</summary>
    All = 4095,

    /// <summary>The pure families: no effect, no user code beyond allow-listed math.</summary>
    Pure = Arithmetic | Conditionals,
}

/// <summary>
/// Deterministic generator of valid, compilable expression trees of type <see cref="double"/> over two parameters
/// <see cref="X"/> and <see cref="Y"/> (ETC closure audit, roadmap Â§5 of the audit brief). The same seed always produces the
/// same tree. Shadowing (an inner binder reusing an outer parameter instance), capture-prone declarations (an inner binder
/// declaring <see cref="Z"/>, the replacement used by the substitution properties) and shared subtrees (DAGs) are produced on
/// purpose.
/// </summary>
internal sealed class EtcAuditGenerator
{
    /// <summary>The first free parameter.</summary>
    public static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>The second free parameter.</summary>
    public static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    /// <summary>A parameter used as a substitution replacement; the generator sometimes declares it in an inner binder.</summary>
    public static readonly ParameterExpression Z = Expression.Parameter(typeof(double), "z");

    /// <summary>The constants drawn by the generator.</summary>
    private static readonly double[] Constants = [0.0, 1.0, -1.0, 2.0, 0.5, 3.0, 10.0];

    /// <summary>The allow-listed math methods.</summary>
    private static readonly MethodInfo[] MathMethods =
    [
        typeof(Math).GetMethod(nameof(Math.Abs), [typeof(double)])!,
        typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!,
        typeof(Math).GetMethod(nameof(Math.Max), [typeof(double), typeof(double)])!,
    ];

    /// <summary>The random source.</summary>
    private readonly Random _random;

    /// <summary>The maximum production depth.</summary>
    private readonly int _maxDepth;

    /// <summary>The enabled families.</summary>
    private readonly AuditFamilies _families;

    /// <summary>The double-typed declarations in scope, innermost last.</summary>
    private readonly List<ParameterExpression> _scope = [];

    /// <summary>The scope entries that are block variables (assignable without changing a lambda parameter's meaning).</summary>
    private readonly List<ParameterExpression> _assignable = [];

    /// <summary>Subtrees eligible for sharing, with the scope in which they were built.</summary>
    private readonly List<(Expression Tree, ParameterExpression[] Scope)> _shareable = [];

    /// <summary>A counter for unique trace tags.</summary>
    private int _tags;

    /// <summary>Initializes a generator.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="maxDepth">The maximum production depth.</param>
    /// <param name="families">The enabled families.</param>
    /// <param name="wrapExtensions">
    /// Whether every extension node is the argument of a traced call, so that it is never the result of a block or a branch
    /// of a conditional (positions whose framework <c>Type</c> getter tail-calls the extension's).
    /// </param>
    public EtcAuditGenerator(int seed, int maxDepth, AuditFamilies families, bool wrapExtensions = false)
    {
        WrapsExtensions = wrapExtensions;
        _random = new Random(seed);
        _maxDepth = maxDepth;
        _families = families;
        Binder = new R4CountingBinder("b");
        TwinBinder = new R4CountingBinder("b");
    }

    /// <summary>Gets whether every extension node is the argument of a traced call.</summary>
    public bool WrapsExtensions { get; }

    /// <summary>Gets the binder of the generated dynamic operations.</summary>
    public R4CountingBinder Binder { get; }

    /// <summary>Gets a second binder, equal to <see cref="Binder"/> by its own <c>Equals</c> but a different instance.</summary>
    public R4CountingBinder TwinBinder { get; }

    /// <summary>Gets the extension nodes produced.</summary>
    public List<AuditExtension> Extensions { get; } = [];

    /// <summary>Gets whether a quote was produced.</summary>
    public bool UsedQuote { get; private set; }

    /// <summary>Gets whether a subtree is shared by two parents.</summary>
    public bool UsedSharing { get; private set; }

    /// <summary>Generates a tree.</summary>
    /// <returns>The body, of type <see cref="double"/>, free in <see cref="X"/> and <see cref="Y"/>.</returns>
    public Expression Generate() => Double(0);

    /// <summary>Wraps a body generated by this class in a compilable lambda.</summary>
    /// <param name="body">The body.</param>
    /// <returns><c>(x, y) =&gt; body</c>.</returns>
    public static Expression<Func<double, double, double>> Wrap(Expression body) => Expression.Lambda<Func<double, double, double>>(body, X, Y);

    /// <summary>Tests whether <paramref name="family"/> is enabled.</summary>
    /// <param name="family">The family.</param>
    /// <returns><see langword="true"/> if enabled.</returns>
    private bool Has(AuditFamilies family) => (_families & family) != 0;

    /// <summary>Draws <see langword="true"/> with probability <paramref name="p"/>.</summary>
    /// <param name="p">The probability.</param>
    /// <returns>The draw.</returns>
    private bool Chance(double p) => _random.NextDouble() < p;

    /// <summary>Picks an element.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The items.</param>
    /// <returns>An element.</returns>
    private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    /// <summary>Generates a leaf.</summary>
    /// <returns>A constant or a parameter in scope.</returns>
    private Expression Leaf()
    {
        int choice = _random.Next(10);
        if (choice < 4) return Expression.Constant(Pick(Constants));
        if (choice < 6) return X;
        if (choice < 8) return Y;
        return _scope.Count > 0 ? Pick(_scope) : X;
    }

    /// <summary>Generates a <see cref="double"/> expression.</summary>
    /// <param name="depth">The current production depth.</param>
    /// <returns>The expression.</returns>
    private Expression Double(int depth)
    {
        if (depth >= _maxDepth || Chance(0.15 + 0.05 * depth)) return Leaf();

        if (_shareable.Count > 0 && Chance(0.04))
        {
            (Expression tree, ParameterExpression[] scope) = Pick(_shareable);
            if (IsScopePrefix(scope))
            {
                UsedSharing = true;
                return tree;
            }
        }

        Expression result;
        while (true)
        {
            Expression? candidate = _random.Next(22) switch
            {
                0 or 1 or 2 => Has(AuditFamilies.Arithmetic) ? Arithmetic(depth) : null,
                3 => Has(AuditFamilies.Arithmetic) ? Expression.Negate(Double(depth + 1)) : null,
                4 => Has(AuditFamilies.Arithmetic) ? MathCall(depth) : null,
                5 or 6 => Has(AuditFamilies.Effects) ? Traced(depth) : null,
                7 => Has(AuditFamilies.Effects) ? Expression.Call(AuditRuntime.CheckMethod, Double(depth + 1)) : null,
                8 => Has(AuditFamilies.Conditionals) ? Expression.Condition(Bool(depth + 1), Double(depth + 1), Double(depth + 1)) : null,
                9 or 10 => Has(AuditFamilies.Lambdas) ? Invoked(depth) : null,
                11 => Has(AuditFamilies.Lambdas) ? Closure(depth) : null,
                12 or 13 => Has(AuditFamilies.Blocks) ? Block(depth) : null,
                14 => Has(AuditFamilies.Blocks) && _assignable.Count > 0 ? Assignment(depth) : null,
                15 => Has(AuditFamilies.Try) ? Try(depth) : null,
                16 => Has(AuditFamilies.Initializers) ? Initializer(depth) : null,
                17 => Has(AuditFamilies.Dynamic) ? Dynamic(depth) : null,
                18 => Has(AuditFamilies.Quote) ? Quote(depth) : null,
                19 => Has(AuditFamilies.Extension) ? Extension() : null,
                20 => Has(AuditFamilies.Checked) ? Expression.Convert(Expression.ConvertChecked(Double(depth + 1), typeof(int)), typeof(double)) : null,
                _ => Has(AuditFamilies.ControlFlow) ? ControlFlow(depth) : null,
            };

            if (candidate is not null)
            {
                result = candidate;
                break;
            }
        }

        if (result is not (ParameterExpression or ConstantExpression) && Chance(0.2) && !ContainsLabel(result))
        {
            _shareable.Add((result, [.. _scope]));
        }

        return result;
    }

    /// <summary>Tests whether the scope of a shareable subtree is still open (a prefix of the current scope).</summary>
    /// <param name="scope">The scope in which the subtree was built.</param>
    /// <returns><see langword="true"/> if every declaration it may reference is still in scope.</returns>
    private bool IsScopePrefix(ParameterExpression[] scope)
    {
        if (scope.Length > _scope.Count) return false;
        for (int i = 0; i < scope.Length; i++)
        {
            if (!ReferenceEquals(scope[i], _scope[i])) return false;
        }

        return true;
    }

    /// <summary>Generates a binary arithmetic node.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The node.</returns>
    private Expression Arithmetic(int depth)
    {
        Expression left = Double(depth + 1), right = Double(depth + 1);
        return _random.Next(5) switch
        {
            0 => Expression.Add(left, right),
            1 => Expression.Subtract(left, right),
            2 => Expression.Multiply(left, right),
            3 => Expression.Divide(left, right),
            _ => Expression.Add(left, Expression.Multiply(Expression.Constant(Pick(Constants)), right)),
        };
    }

    /// <summary>Generates an allow-listed math call.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The call.</returns>
    private Expression MathCall(int depth)
    {
        MethodInfo method = Pick(MathMethods);
        return method.GetParameters().Length == 1
            ? Expression.Call(method, Double(depth + 1))
            : Expression.Call(method, Double(depth + 1), Double(depth + 1));
    }

    /// <summary>Generates a traced value.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The call.</returns>
    private Expression Traced(int depth)
        => Expression.Call(R4DynamicRuntime.TraceMethod, Expression.Constant("t" + _tags++), Double(depth + 1));

    /// <summary>Generates a boolean expression.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The expression.</returns>
    private Expression Bool(int depth)
    {
        if (depth >= _maxDepth) return Expression.LessThan(Leaf(), Leaf());

        return _random.Next(6) switch
        {
            0 or 1 => Expression.LessThan(Double(depth + 1), Double(depth + 1)),
            2 => Expression.Equal(Double(depth + 1), Double(depth + 1)),
            3 => Expression.Not(Bool(depth + 1)),
            4 => Expression.AndAlso(Bool(depth + 1), Bool(depth + 1)),
            _ => Expression.OrElse(Bool(depth + 1), Bool(depth + 1)),
        };
    }

    /// <summary>Picks a declaration for a new binder: usually fresh, sometimes an outer instance (shadowing) or <see cref="Z"/>.</summary>
    /// <param name="name">The name of a fresh declaration.</param>
    /// <returns>The declaration.</returns>
    private ParameterExpression Declaration(string name)
    {
        int choice = _random.Next(10);
        if (choice == 0) return X;
        if (choice == 1) return Z;
        if (choice == 2 && _scope.Count > 0) return Pick(_scope);
        return Expression.Parameter(typeof(double), name);
    }

    /// <summary>Generates an invoked lambda <c>((a) =&gt; body)(argument)</c>, an inlining candidate.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The invocation.</returns>
    private Expression Invoked(int depth)
    {
        Expression argument = _random.Next(4) switch
        {
            0 => Expression.Constant(Pick(Constants)),
            1 => _scope.Count > 0 && Chance(0.5) ? Pick(_scope) : (Chance(0.5) ? X : Y),
            _ => Double(depth + 1),
        };

        ParameterExpression parameter = Declaration("a");
        _scope.Add(parameter);
        Expression body;
        try
        {
            body = Double(depth + 1);
        }
        finally
        {
            _scope.RemoveAt(_scope.Count - 1);
        }

        return Expression.Invoke(Expression.Lambda<Func<double, double>>(body, parameter), argument);
    }

    /// <summary>Generates a closure passed to a host call that invokes it twice.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The call.</returns>
    private Expression Closure(int depth)
    {
        ParameterExpression parameter = Declaration("c");
        _scope.Add(parameter);
        Expression body;
        try
        {
            body = Double(depth + 1);
        }
        finally
        {
            _scope.RemoveAt(_scope.Count - 1);
        }

        return Expression.Call(AuditRuntime.CallTwiceMethod, Expression.Lambda<Func<double, double>>(body, parameter), Double(depth + 1));
    }

    /// <summary>Generates a block declaring a variable, assigning it, then computing a value.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The block.</returns>
    private Expression Block(int depth)
    {
        Expression initial = Double(depth + 1);
        ParameterExpression variable = Declaration("v");
        _scope.Add(variable);
        _assignable.Add(variable);
        try
        {
            Expression body = Double(depth + 1);
            return Chance(0.3)
                ? Expression.Block([variable], Expression.Assign(variable, initial), Expression.PreIncrementAssign(variable), body)
                : Expression.Block([variable], Expression.Assign(variable, initial), body);
        }
        finally
        {
            _scope.RemoveAt(_scope.Count - 1);
            _assignable.RemoveAt(_assignable.Count - 1);
        }
    }

    /// <summary>Generates an assignment to a block variable in scope, as a value.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The assignment.</returns>
    private Expression Assignment(int depth)
    {
        ParameterExpression target = Pick(_assignable);
        return _random.Next(3) switch
        {
            0 => Expression.AddAssign(target, Double(depth + 1)),
            1 => Expression.PostIncrementAssign(target),
            _ => Expression.Assign(target, Double(depth + 1)),
        };
    }

    /// <summary>Generates a try/catch/finally of type <see cref="double"/>.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The node.</returns>
    private Expression Try(int depth)
    {
        Expression body = Expression.Call(AuditRuntime.CheckMethod, Double(depth + 1));
        ParameterExpression exception = Expression.Variable(typeof(InvalidOperationException), "ex");
        Expression handler = Double(depth + 1);
        Expression @finally = Expression.Call(R4DynamicRuntime.TraceMethod, Expression.Constant("fin" + _tags++), Expression.Constant(0.0));
        CatchBlock @catch = Chance(0.5)
            ? Expression.Catch(exception, handler)
            : Expression.Catch(exception, handler, Expression.LessThan(Leaf(), Expression.Constant(100.0)));
        return Chance(0.5) ? Expression.TryCatchFinally(body, @finally, @catch) : Expression.TryCatch(body, @catch);
    }

    /// <summary>Generates a member initialization, a list initialization or an index access.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The node.</returns>
    private Expression Initializer(int depth)
    {
        switch (_random.Next(4))
        {
            case 0:
                return Expression.Property(
                    Expression.MemberInit(Expression.New(typeof(AuditCell)), Expression.Bind(typeof(AuditCell).GetProperty(nameof(AuditCell.V))!, Double(depth + 1))),
                    nameof(AuditCell.V));
            case 1:
                return Expression.Field(
                    Expression.MemberInit(Expression.New(typeof(AuditCell)), Expression.Bind(typeof(AuditCell).GetField(nameof(AuditCell.W))!, Double(depth + 1))),
                    nameof(AuditCell.W));
            case 2:
                {
                    MethodInfo add = typeof(AuditList).GetMethod(nameof(AuditList.Add))!;
                    Expression list = Expression.ListInit(Expression.New(typeof(AuditList)), Expression.ElementInit(add, Double(depth + 1)), Expression.ElementInit(add, Double(depth + 1)));
                    return Chance(0.5)
                        ? Expression.Property(list, nameof(AuditList.Sum))
                        : Expression.MakeIndex(list, typeof(AuditList).GetProperty("Item")!, [Expression.Constant(_random.Next(3))]);
                }
            default:
                return Expression.ArrayAccess(Expression.NewArrayInit(typeof(double), Double(depth + 1), Double(depth + 1)), Expression.Constant(_random.Next(2)));
        }
    }

    /// <summary>Generates a dynamic operation over two arguments, converted back to <see cref="double"/>.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The node.</returns>
    private Expression Dynamic(int depth)
        => Expression.Convert(Expression.Dynamic(Chance(0.8) ? Binder : TwinBinder, typeof(object), Double(depth + 1), Double(depth + 1)), typeof(double));

    /// <summary>Generates a quoted lambda applied by a host call; the quoted body may capture parameters in scope.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The call.</returns>
    private Expression Quote(int depth)
    {
        UsedQuote = true;
        ParameterExpression parameter = Expression.Parameter(typeof(double), "q");
        _scope.Add(parameter);
        Expression body;
        try
        {
            body = Double(depth + 1);
        }
        finally
        {
            _scope.RemoveAt(_scope.Count - 1);
        }

        return Expression.Call(AuditRuntime.ApplyMethod, Expression.Quote(Expression.Lambda<Func<double, double>>(body, parameter)), Double(depth + 1));
    }

    /// <summary>Generates an opaque extension node reducing to a constant.</summary>
    /// <returns>The node.</returns>
    private Expression Extension()
    {
        var extension = new AuditExtension(Expression.Constant(Pick(Constants)));
        Extensions.Add(extension);
        return WrapsExtensions ? Expression.Call(R4DynamicRuntime.TraceMethod, Expression.Constant("e" + _tags++), extension) : extension;
    }

    /// <summary>Generates a jump to a label carrying a value, or a switch.</summary>
    /// <param name="depth">The current depth.</param>
    /// <returns>The node.</returns>
    private Expression ControlFlow(int depth)
    {
        if (Chance(0.5))
        {
            LabelTarget target = Expression.Label(typeof(double), "l" + _tags++);
            return Expression.Block(
                Expression.IfThen(Bool(depth + 1), Expression.Return(target, Double(depth + 1))),
                Expression.Label(target, Double(depth + 1)));
        }

        return Expression.Switch(
            Expression.Convert(Double(depth + 1), typeof(int)),
            Double(depth + 1),
            Expression.SwitchCase(Double(depth + 1), Expression.Constant(0)),
            Expression.SwitchCase(Double(depth + 1), Expression.Constant(1), Expression.Constant(2)));
    }

    /// <summary>Tests whether a subtree defines a label (such a subtree cannot be shared: a label is defined once).</summary>
    /// <param name="e">The subtree.</param>
    /// <returns><see langword="true"/> if it holds a label, a switch or a block (a block may declare a variable reused elsewhere).</returns>
    private static bool ContainsLabel(Expression e)
    {
        var finder = new LabelFinder();
        finder.Visit(e);
        return finder.Found;
    }

    /// <summary>Finds labels without touching extension nodes.</summary>
    private sealed class LabelFinder : ExpressionVisitor
    {
        /// <summary>Gets whether a label was found.</summary>
        public bool Found { get; private set; }

        /// <inheritdoc/>
        protected override Expression VisitLabel(LabelExpression node)
        {
            Found = true;
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitExtension(Expression node)
        {
            // A plain visitor reaches a dynamic operation here: its arguments may hold labels.
            if (node is DynamicExpression dynamic)
            {
                foreach (Expression argument in dynamic.Arguments) Visit(argument);
            }

            return node;
        }
    }
}
