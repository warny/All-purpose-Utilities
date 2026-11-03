using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// Observable runtime of the ETC closure audit: every traced operation of a compiled tree is appended to the thread log of
/// <see cref="R4DynamicRuntime"/>, so that the order and count of effects of an original and a transformed tree can be compared.
/// </summary>
public static class AuditRuntime
{
    /// <summary>The <see cref="Check"/> method.</summary>
    public static readonly MethodInfo CheckMethod = typeof(AuditRuntime).GetMethod(nameof(Check))!;

    /// <summary>The <see cref="Apply"/> method.</summary>
    public static readonly MethodInfo ApplyMethod = typeof(AuditRuntime).GetMethod(nameof(Apply))!;

    /// <summary>The <see cref="CallTwice"/> method.</summary>
    public static readonly MethodInfo CallTwiceMethod = typeof(AuditRuntime).GetMethod(nameof(CallTwice))!;

    /// <summary>Records a check and throws <see cref="InvalidOperationException"/> when <paramref name="value"/> is greater than 2.</summary>
    /// <param name="value">The checked value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double Check(double value)
    {
        R4DynamicRuntime.Events.Add("check");
        if (value > 2) throw new InvalidOperationException("check failed");
        return value;
    }

    /// <summary>Compiles and applies a quoted function, so that the meaning of quoted data is observable.</summary>
    /// <param name="function">The quoted function.</param>
    /// <param name="value">The argument.</param>
    /// <returns>The result of the function.</returns>
    public static double Apply(Expression<Func<double, double>> function, double value)
    {
        R4DynamicRuntime.Events.Add("apply");
        return function.Compile()(value);
    }

    /// <summary>Invokes a delegate twice, so that closure captures are observable.</summary>
    /// <param name="function">The delegate.</param>
    /// <param name="value">The argument.</param>
    /// <returns><c>f(v) + f(v) / 2</c>.</returns>
    public static double CallTwice(Func<double, double> function, double value) => function(value) + function(value) / 2;
}

/// <summary>A mutable reference cell whose property accessors are traced.</summary>
public sealed class AuditCell
{
    /// <summary>The backing value.</summary>
    private double _value;

    /// <summary>Gets or sets the traced value.</summary>
    public double V
    {
        get
        {
            R4DynamicRuntime.Events.Add("get");
            return _value;
        }
        set
        {
            R4DynamicRuntime.Events.Add("set");
            _value = value;
        }
    }

    /// <summary>An untraced field.</summary>
    public double W;
}

/// <summary>A list whose <c>Add</c> method and indexer are traced.</summary>
public sealed class AuditList : IEnumerable<double>
{
    /// <summary>The items.</summary>
    private readonly List<double> _items = [];

    /// <summary>Adds an item, traced.</summary>
    /// <param name="value">The item.</param>
    public void Add(double value)
    {
        R4DynamicRuntime.Events.Add("add");
        _items.Add(value);
    }

    /// <summary>Gets the sum of the items.</summary>
    public double Sum
    {
        get
        {
            double sum = 0;
            foreach (double item in _items) sum += item;
            return sum;
        }
    }

    /// <summary>Gets or sets an item, traced; an out-of-range read returns <c>-1</c>.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The item.</returns>
    public double this[int index]
    {
        get
        {
            R4DynamicRuntime.Events.Add("index");
            return index >= 0 && index < _items.Count ? _items[index] : -1;
        }
        set
        {
            R4DynamicRuntime.Events.Add("index-set");
            if (index >= 0 && index < _items.Count) _items[index] = value;
        }
    }

    /// <inheritdoc/>
    public IEnumerator<double> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// An opaque extension node for the audit campaigns: it reduces to its child (so trees holding it compile), counts every
/// member call, and, while <see cref="Armed"/>, records the immediate caller of each call. Unlike
/// <see cref="R4HostileExtension"/>, it never throws, so a campaign observes every read instead of stopping at the first.
/// </summary>
public sealed class AuditExtension : Expression
{
    /// <summary>The child, also the reduction result.</summary>
    private readonly Expression _child;

    /// <summary>Initializes a node.</summary>
    /// <param name="child">The child, of type <see cref="double"/>.</param>
    public AuditExtension(Expression child) => _child = child;

    /// <summary>Gets or sets whether calls are recorded as engine-time calls.</summary>
    public bool Armed { get; set; }

    /// <summary>Gets the calls recorded while armed, as <c>member &lt;- caller</c>.</summary>
    public List<string> ArmedCalls { get; } = [];

    /// <summary>Records a call while armed.</summary>
    /// <param name="member">The member name.</param>
    private void Hit(string member)
    {
        if (!Armed) return;
        var frame = new System.Diagnostics.StackFrame(2, true);
        MethodBase? caller = frame.GetMethod();
        string line = caller?.DeclaringType?.Assembly == typeof(Expression).Assembly ? "" : $" line {frame.GetFileLineNumber()}";
        ArmedCalls.Add($"{member} <- {caller?.DeclaringType?.Assembly.GetName().Name}:{caller?.DeclaringType?.Name}.{caller?.Name}{line}");
    }

    /// <inheritdoc/>
    public override ExpressionType NodeType
    {
        get
        {
            Hit(nameof(NodeType));
            return ExpressionType.Extension;
        }
    }

    /// <inheritdoc/>
    public override Type Type
    {
        get
        {
            Hit(nameof(Type));
            return typeof(double);
        }
    }

    /// <inheritdoc/>
    public override bool CanReduce
    {
        get
        {
            Hit(nameof(CanReduce));
            return true;
        }
    }

    /// <inheritdoc/>
    public override Expression Reduce()
    {
        Hit(nameof(Reduce));
        return _child;
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
        return ReferenceEquals(this, obj);
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
        return "audit-extension";
    }
}
