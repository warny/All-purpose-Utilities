using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R3: an ordered trace of the user code run by an instrumented initialization or index access (constructors, setters,
/// getters, <c>Add</c> methods, indexer accessors, tagged argument evaluations). Each test owns its own instance, embedded
/// in the tree as a constant, so concurrent tests never share a trace.
/// </summary>
public sealed class R3TraceLog
{
    /// <summary>Gets the recorded entries, in call order.</summary>
    public List<string> Entries { get; } = [];

    /// <summary>Records one entry.</summary>
    /// <param name="entry">The entry.</param>
    public void Write(string entry) => Entries.Add(entry);

    /// <summary>Records <paramref name="tag"/> and returns <paramref name="value"/>: marks the evaluation of an operand.</summary>
    /// <typeparam name="T">The operand type.</typeparam>
    /// <param name="tag">The tag recorded.</param>
    /// <param name="value">The operand value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public T Tag<T>(string tag, T value)
    {
        Write(tag);
        return value;
    }

    /// <summary>Gets the <see cref="Tag{T}"/> method closed over <paramref name="type"/>.</summary>
    /// <param name="type">The operand type.</param>
    /// <returns>The closed method.</returns>
    public static MethodInfo TagMethod(Type type) => typeof(R3TraceLog).GetMethod(nameof(Tag))!.MakeGenericMethod(type);

    /// <summary>Builds <c>log.Tag(tag, operand)</c>.</summary>
    /// <param name="log">The trace.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="operand">The operand.</param>
    /// <returns>The tagged operand.</returns>
    public static Expression Tagged(R3TraceLog log, string tag, Expression operand)
        => Expression.Call(Expression.Constant(log), TagMethod(operand.Type), Expression.Constant(tag), operand);

    /// <summary>Formats a number for a trace entry, independently of the current culture.</summary>
    /// <param name="value">The number.</param>
    /// <returns>Its invariant text.</returns>
    internal static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>An instrumented class whose constructor, setters and getters write to an <see cref="R3TraceLog"/> (ETC R3).</summary>
public sealed class R3TracedHolder
{
    /// <summary>The trace written to.</summary>
    private readonly R3TraceLog _log;

    /// <summary>The name used in the trace entries.</summary>
    private readonly string _name;

    /// <summary>The backing value of <see cref="Value"/>.</summary>
    private double _value;

    /// <summary>The backing value of <see cref="Other"/>.</summary>
    private double _other;

    /// <summary>The lazily created nested holder.</summary>
    private R3TracedHolder? _child;

    /// <summary>The collection exposed by <see cref="Items"/>.</summary>
    private readonly R3TracedList _items;

    /// <summary>The backing value of <see cref="Callback"/>.</summary>
    private Func<double, double>? _callback;

    /// <summary>Initializes a holder named <c>h</c>, recording <c>h.ctor</c>.</summary>
    /// <param name="log">The trace.</param>
    public R3TracedHolder(R3TraceLog log) : this(log, "h", "ctor")
    {
    }

    /// <summary>Initializes a holder named <c>h</c> with a seed, recording <c>h.ctor(seed)</c>.</summary>
    /// <param name="log">The trace.</param>
    /// <param name="seed">The initial <see cref="Value"/>.</param>
    public R3TracedHolder(R3TraceLog log, double seed) : this(log, "h", "ctor(" + R3TraceLog.Text(seed) + ")")
    {
        _value = seed;
    }

    /// <summary>Initializes a named holder.</summary>
    /// <param name="log">The trace.</param>
    /// <param name="name">The name used in the trace entries.</param>
    /// <param name="constructor">The constructor entry recorded.</param>
    private R3TracedHolder(R3TraceLog log, string name, string constructor)
    {
        _log = log;
        _name = name;
        _items = new R3TracedList(log, name + ".items");
        log.Write(name + "." + constructor);
    }

    /// <summary>A plain field, assigned without user code.</summary>
    public double Field;

    /// <summary>Gets or sets a traced value.</summary>
    public double Value
    {
        get
        {
            _log.Write(_name + ".get Value");
            return _value;
        }
        set
        {
            _log.Write(_name + ".set Value=" + R3TraceLog.Text(value));
            _value = value;
        }
    }

    /// <summary>Gets or sets a second traced value.</summary>
    public double Other
    {
        get
        {
            _log.Write(_name + ".get Other");
            return _other;
        }
        set
        {
            _log.Write(_name + ".set Other=" + R3TraceLog.Text(value));
            _other = value;
        }
    }

    /// <summary>Gets the nested holder, created on first access (traced).</summary>
    public R3TracedHolder Child
    {
        get
        {
            _log.Write(_name + ".get Child");
            return _child ??= new R3TracedHolder(_log, _name + ".child", "ctor");
        }
    }

    /// <summary>Gets the traced collection (the getter is traced).</summary>
    public R3TracedList Items
    {
        get
        {
            _log.Write(_name + ".get Items");
            return _items;
        }
    }

    /// <summary>Gets or sets a callback (the setter is traced).</summary>
    public Func<double, double>? Callback
    {
        get => _callback;
        set
        {
            _log.Write(_name + ".set Callback");
            _callback = value;
        }
    }

    /// <summary>Gets a text summary of the state, for result comparisons.</summary>
    /// <returns>The summary.</returns>
    public string Describe()
        => $"{_name}:{R3TraceLog.Text(_value)}/{R3TraceLog.Text(_other)}/{R3TraceLog.Text(Field)}/[{_items.Describe()}]"
            + (_child is null ? string.Empty : "{" + _child.Describe() + "}")
            + (_callback is null ? string.Empty : "#" + R3TraceLog.Text(_callback(2.0)));

    /// <summary>Gets a public instance member of this class by name.</summary>
    /// <param name="name">The member name.</param>
    /// <returns>The member.</returns>
    public static MemberInfo Member(string name) => typeof(R3TracedHolder).GetMember(name)[0];
}

/// <summary>An instrumented collection with overloaded and multi-parameter <c>Add</c> methods writing to an <see cref="R3TraceLog"/> (ETC R3).</summary>
public class R3TracedList : IEnumerable<double>
{
    /// <summary>The trace written to.</summary>
    private readonly R3TraceLog _log;

    /// <summary>The name used in the trace entries.</summary>
    private readonly string _name;

    /// <summary>The added values, in order.</summary>
    private readonly List<string> _values = [];

    /// <summary>Initializes a collection named <c>list</c>, recording <c>list.ctor</c>.</summary>
    /// <param name="log">The trace.</param>
    public R3TracedList(R3TraceLog log) : this(log, "list")
    {
        log.Write("list.ctor");
    }

    /// <summary>Initializes a named collection without recording its creation (owned by a holder).</summary>
    /// <param name="log">The trace.</param>
    /// <param name="name">The name used in the trace entries.</param>
    internal R3TracedList(R3TraceLog log, string name)
    {
        _log = log;
        _name = name;
    }

    /// <summary>Adds a number.</summary>
    /// <param name="value">The number.</param>
    public void Add(double value)
    {
        _log.Write(_name + ".Add(" + R3TraceLog.Text(value) + ")");
        _values.Add(R3TraceLog.Text(value));
    }

    /// <summary>Adds a pair of numbers (a two-parameter <c>Add</c>).</summary>
    /// <param name="first">The first number.</param>
    /// <param name="second">The second number.</param>
    public void Add(double first, double second)
    {
        _log.Write(_name + ".Add(" + R3TraceLog.Text(first) + "," + R3TraceLog.Text(second) + ")");
        _values.Add(R3TraceLog.Text(first) + ":" + R3TraceLog.Text(second));
    }

    /// <summary>Adds a text (an overload of <c>Add</c>).</summary>
    /// <param name="value">The text.</param>
    public void Add(string value)
    {
        _log.Write(_name + ".Add(\"" + value + "\")");
        _values.Add('"' + value + '"');
    }

    /// <summary>Adds a code; a negative code throws after being traced.</summary>
    /// <param name="code">The code.</param>
    /// <exception cref="InvalidOperationException"><paramref name="code"/> is negative.</exception>
    public void Add(int code)
    {
        _log.Write(_name + ".Add#" + code.ToString(CultureInfo.InvariantCulture));
        if (code < 0) throw new InvalidOperationException("Negative code " + code.ToString(CultureInfo.InvariantCulture) + ".");
        _values.Add("#" + code.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Gets a text summary of the added values.</summary>
    /// <returns>The summary.</returns>
    public string Describe() => string.Join(",", _values);

    /// <summary>Gets the <c>Add</c> overload taking <paramref name="parameterTypes"/>.</summary>
    /// <param name="parameterTypes">The parameter types.</param>
    /// <returns>The method.</returns>
    public static MethodInfo AddMethod(params Type[] parameterTypes) => typeof(R3TracedList).GetMethod(nameof(Add), parameterTypes)!;

    /// <inheritdoc/>
    public IEnumerator<double> GetEnumerator() => ((IEnumerable<double>)System.Array.Empty<double>()).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// A collection whose <c>Add(double)</c> hides the base one (ETC R3): on this type both <c>Add</c> methods are valid for the
/// same arguments, so two initializers can differ by their <c>Add</c> method only.
/// </summary>
public sealed class R3ShadowingList : R3TracedList
{
    /// <summary>Initializes the collection.</summary>
    /// <param name="log">The trace.</param>
    public R3ShadowingList(R3TraceLog log) : base(log)
    {
    }

    /// <summary>Adds a number twice (hides the base method).</summary>
    /// <param name="value">The number.</param>
    public new void Add(double value)
    {
        base.Add(value);
        base.Add(value);
    }

    /// <summary>Gets the hiding <c>Add(double)</c>.</summary>
    public static MethodInfo HidingAdd { get; } = typeof(R3ShadowingList).GetMethod(nameof(Add), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, [typeof(double)])!;
}

/// <summary>An instrumented class with two indexers writing to an <see cref="R3TraceLog"/> (ETC R3).</summary>
public class R3TracedGrid
{
    /// <summary>The trace written to.</summary>
    private readonly R3TraceLog _log;

    /// <summary>The cells of the two-argument indexer.</summary>
    private readonly double[,] _cells = new double[4, 4];

    /// <summary>The cells of the text indexer.</summary>
    private readonly Dictionary<string, double> _named = [];

    /// <summary>Initializes a grid.</summary>
    /// <param name="log">The trace.</param>
    public R3TracedGrid(R3TraceLog log) => _log = log;

    /// <summary>Gets or sets a cell (both accessors are traced).</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <returns>The cell value.</returns>
    public double this[int row, int column]
    {
        get
        {
            _log.Write($"get[{row},{column}]");
            return _cells[row, column];
        }
        set
        {
            _log.Write($"set[{row},{column}]=" + R3TraceLog.Text(value));
            _cells[row, column] = value;
        }
    }

    /// <summary>Gets or sets a named cell (both accessors are traced; a missing name reads as zero).</summary>
    /// <param name="name">The name.</param>
    /// <returns>The cell value.</returns>
    public double this[string name]
    {
        get
        {
            _log.Write($"get[\"{name}\"]");
            return _named.TryGetValue(name, out double value) ? value : 0.0;
        }
        set
        {
            _log.Write($"set[\"{name}\"]=" + R3TraceLog.Text(value));
            _named[name] = value;
        }
    }

    /// <summary>Gets a text summary of the state, for result comparisons.</summary>
    /// <returns>The summary.</returns>
    public string Describe()
    {
        var parts = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                if (_cells[i, j] != 0.0) parts.Add($"{i}{j}=" + R3TraceLog.Text(_cells[i, j]));
            }
        }

        foreach (KeyValuePair<string, double> pair in _named) parts.Add(pair.Key + "=" + R3TraceLog.Text(pair.Value));
        return string.Join(";", parts);
    }

    /// <summary>Gets the two-argument indexer.</summary>
    public static PropertyInfo CellIndexer { get; } = typeof(R3TracedGrid).GetProperty("Item", [typeof(int), typeof(int)])!;

    /// <summary>Gets the text indexer.</summary>
    public static PropertyInfo NameIndexer { get; } = typeof(R3TracedGrid).GetProperty("Item", [typeof(string)])!;
}

/// <summary>
/// A grid whose two-argument indexer hides the base one (ETC R3): on this type both indexers accept the same receiver and
/// arguments, so two index accesses can differ by their indexer only.
/// </summary>
public sealed class R3ShadowingGrid : R3TracedGrid
{
    /// <summary>Initializes the grid.</summary>
    /// <param name="log">The trace.</param>
    public R3ShadowingGrid(R3TraceLog log) : base(log)
    {
    }

    /// <summary>Gets a constant cell (hides the base indexer); setting is ignored.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <returns>Always <c>-1</c>.</returns>
    public new double this[int row, int column]
    {
        get => -1.0;
        set { }
    }

    /// <summary>Gets the hiding indexer.</summary>
    public static PropertyInfo HidingIndexer { get; } = typeof(R3ShadowingGrid).GetProperty("Item", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, typeof(double), [typeof(int), typeof(int)], null)!;
}