using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>A custom delegate signature for dynamic operations, distinct from every <see cref="Func{TResult}"/> type.</summary>
/// <param name="site">The call site.</param>
/// <param name="left">The first operand.</param>
/// <param name="right">The second operand.</param>
/// <returns>The result.</returns>
internal delegate object R4DynamicDelegate(CallSite site, double left, double right);

/// <summary>
/// An instrumented call-site binder (ETC R4). It counts every call the engine could make into it (<c>Bind</c>,
/// <c>BindDelegate</c>, <c>Equals</c>, <c>GetHashCode</c>, <c>ToString</c>); when it is allowed to bind, the rule it binds adds
/// its <see cref="double"/> arguments and reports every evaluation to <see cref="R4DynamicRuntime"/>, so the number and order
/// of the dynamic operations executed by a compiled tree can be compared.
/// </summary>
internal sealed class R4CountingBinder : CallSiteBinder
{
    /// <summary>Whether <see cref="Bind"/> throws instead of binding.</summary>
    private readonly bool _throwOnBind;

    /// <summary>Initializes a binder.</summary>
    /// <param name="name">The name reported by the bound rule.</param>
    /// <param name="throwOnBind">Whether <see cref="Bind"/> throws (a binder the engine must never call).</param>
    public R4CountingBinder(string name, bool throwOnBind = false)
    {
        Name = name;
        _throwOnBind = throwOnBind;
    }

    /// <summary>Gets the name reported by the bound rule.</summary>
    public string Name { get; }

    /// <summary>Gets the number of <see cref="Bind"/> calls.</summary>
    public int BindCalls { get; private set; }

    /// <summary>Gets the number of <see cref="BindDelegate{T}"/> calls.</summary>
    public int BindDelegateCalls { get; private set; }

    /// <summary>Gets the number of <see cref="Equals(object?)"/>, <see cref="GetHashCode"/> and <see cref="ToString"/> calls.</summary>
    public int IdentityCalls { get; private set; }

    /// <summary>Gets the total number of calls made into this binder.</summary>
    public int TotalCalls => BindCalls + BindDelegateCalls + IdentityCalls;

    /// <inheritdoc/>
    public override Expression Bind(object[] args, ReadOnlyCollection<ParameterExpression> parameters, LabelTarget returnLabel)
    {
        BindCalls++;
        if (_throwOnBind) throw new InvalidOperationException($"The binder '{Name}' must never be bound.");

        Expression sum = Expression.Constant(0.0);
        foreach (ParameterExpression parameter in parameters)
        {
            sum = Expression.Add(sum, Expression.Convert(parameter, typeof(double)));
        }

        Expression reported = Expression.Call(R4DynamicRuntime.ReportMethod, Expression.Constant(Name), sum);
        return returnLabel.Type == typeof(void)
            ? Expression.Block(reported, Expression.Return(returnLabel))
            : Expression.Return(returnLabel, Expression.Convert(reported, returnLabel.Type));
    }

    /// <inheritdoc/>
    public override T? BindDelegate<T>(CallSite<T> site, object[] args) where T : class
    {
        BindDelegateCalls++;
        return base.BindDelegate(site, args);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        IdentityCalls++;
        return obj is R4CountingBinder other && other.Name == Name;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        IdentityCalls++;
        return Name.GetHashCode(StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        IdentityCalls++;
        return Name;
    }
}

/// <summary>A second binder class, so that binders of different classes can be keyed apart.</summary>
internal sealed class R4OtherBinder : CallSiteBinder
{
    /// <inheritdoc/>
    public override Expression Bind(object[] args, ReadOnlyCollection<ParameterExpression> parameters, LabelTarget returnLabel)
        => throw new InvalidOperationException("The other binder must never be bound.");
}

/// <summary>The runtime log of the rules bound by <see cref="R4CountingBinder"/> and of the traced arguments.</summary>
internal static class R4DynamicRuntime
{
    /// <summary>The <see cref="Report"/> method.</summary>
    public static readonly MethodInfo ReportMethod = typeof(R4DynamicRuntime).GetMethod(nameof(Report))!;

    /// <summary>The <see cref="Trace"/> method.</summary>
    public static readonly MethodInfo TraceMethod = typeof(R4DynamicRuntime).GetMethod(nameof(Trace))!;

    /// <summary>The events recorded on the current thread, in order.</summary>
    [ThreadStatic]
    private static List<string>? _events;

    /// <summary>Gets the events recorded on the current thread since the last <see cref="Reset"/>.</summary>
    public static List<string> Events => _events ??= [];

    /// <summary>Clears the events of the current thread.</summary>
    public static void Reset() => Events.Clear();

    /// <summary>Records the evaluation of a dynamic operation bound by a counting binder.</summary>
    /// <param name="name">The binder name.</param>
    /// <param name="value">The sum of the operands.</param>
    /// <returns>The boxed sum.</returns>
    public static object Report(string name, double value)
    {
        Events.Add($"{name}:{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        return value;
    }

    /// <summary>Records the evaluation of a traced argument.</summary>
    /// <param name="name">The argument name.</param>
    /// <param name="value">The value to return.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double Trace(string name, double value)
    {
        Events.Add(name);
        return value;
    }
}

/// <summary>
/// A hostile extension node (ETC R4): every member the engine could call is counted, and, once <see cref="Armed"/>, throws.
/// It reduces to its child, so a tree holding it can still be compiled when the members are not armed.
/// </summary>
internal sealed class R4HostileExtension : Expression
{
    /// <summary>The child, also the reduction result.</summary>
    private readonly Expression _child;

    /// <summary>Whether this node can reduce.</summary>
    private readonly bool _reducible;

    /// <summary>Initializes a hostile node.</summary>
    /// <param name="child">The child of type <see cref="double"/>.</param>
    /// <param name="reducible">Whether <see cref="CanReduce"/> reports <see langword="true"/>.</param>
    public R4HostileExtension(Expression child, bool reducible = true)
    {
        _child = child;
        _reducible = reducible;
    }

    /// <summary>Gets or sets whether every counted member throws.</summary>
    public bool Armed { get; set; }

    /// <summary>Gets the calls per member name.</summary>
    public Dictionary<string, int> Calls { get; } = [];

    /// <summary>Gets the total number of counted calls.</summary>
    public int TotalCalls => Calls.Values.Sum();

    /// <summary>
    /// Gets the number of calls the engine itself made: any call to a member other than the <c>NodeType</c> and <c>Type</c>
    /// getters, and a getter read whose immediate caller is engine code (the <c>Utils</c> assembly). A getter read whose
    /// immediate caller is a framework factory (<c>System.Linq.Expressions</c>) validating a supported parent it rebuilds is
    /// counted in <see cref="FrameworkReads"/> instead.
    /// </summary>
    public int EngineCalls { get; private set; }

    /// <summary>Gets the getter reads made by framework factories.</summary>
    public int FrameworkReads { get; private set; }

    /// <summary>Clears the counters.</summary>
    public void ResetCalls()
    {
        Calls.Clear();
        EngineCalls = 0;
        FrameworkReads = 0;
    }

    /// <summary>Counts a call to <paramref name="member"/> and throws when armed and the call comes from the engine.</summary>
    /// <param name="member">The member name.</param>
    /// <param name="getter">Whether <paramref name="member"/> is the <c>NodeType</c> or <c>Type</c> getter.</param>
    private void Hit(string member, bool getter = false)
    {
        Calls[member] = Calls.TryGetValue(member, out int count) ? count + 1 : 1;
        if (!Armed) return;

        // Frame 0 is Hit, frame 1 the counted member, frame 2 its caller (tests run without JIT optimization).
        Assembly? caller = new System.Diagnostics.StackFrame(2, false).GetMethod()?.DeclaringType?.Assembly;
        if (getter && caller == typeof(Expression).Assembly)
        {
            FrameworkReads++;
            return;
        }

        EngineCalls++;
        throw new InvalidOperationException($"{member} was called on an opaque extension node by {caller?.GetName().Name ?? "unknown code"}.");
    }

    /// <inheritdoc/>
    public override ExpressionType NodeType
    {
        get
        {
            Hit(nameof(NodeType), getter: true);
            return ExpressionType.Extension;
        }
    }

    /// <inheritdoc/>
    public override Type Type
    {
        get
        {
            Hit(nameof(Type), getter: true);
            return typeof(double);
        }
    }

    /// <inheritdoc/>
    public override bool CanReduce
    {
        get
        {
            Hit(nameof(CanReduce));
            return _reducible;
        }
    }

    /// <inheritdoc/>
    public override Expression Reduce()
    {
        Hit(nameof(Reduce));
        return _reducible ? _child : this;
    }

    /// <inheritdoc/>
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        Hit(nameof(VisitChildren));
        return this;
    }

    /// <inheritdoc/>
    protected override Expression Accept(ExpressionVisitor visitor)
    {
        Hit(nameof(Accept));
        return base.Accept(visitor);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        Hit(nameof(Equals));
        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        Hit(nameof(GetHashCode));
        return 0;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        Hit(nameof(ToString));
        return "hostile";
    }
}
