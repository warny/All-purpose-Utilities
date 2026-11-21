using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
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
    /// counted in <see cref="FrameworkReads"/> instead, and so is a getter read attributed to an optimized method that cannot
    /// read these getters itself (<see cref="R4ReadAttribution.IsInlinedFrameworkRead"/>). Any other getter read attributed to
    /// an optimized method is undecidable and listed in <see cref="UndecidedReads"/> (see <see cref="R4ReadAttribution"/>).
    /// </summary>
    public int EngineCalls { get; private set; }

    /// <summary>Gets the getter reads made by framework factories.</summary>
    public int FrameworkReads { get; private set; }

    /// <summary>
    /// Gets the getter reads attributed to an optimized method that may read these getters itself, as
    /// <c>member &lt;- Type.Method</c>: the method may also hold an inlined framework factory, so the read is neither counted
    /// as an engine call nor as a framework read. An unoptimized (Debug) build never records one.
    /// </summary>
    public List<string> UndecidedReads { get; } = [];

    /// <summary>Clears the counters.</summary>
    public void ResetCalls()
    {
        Calls.Clear();
        EngineCalls = 0;
        FrameworkReads = 0;
        UndecidedReads.Clear();
    }

    /// <summary>Counts a call to <paramref name="member"/> and throws when armed and the call comes from the engine.</summary>
    /// <param name="member">The member name.</param>
    /// <param name="getter">Whether <paramref name="member"/> is the <c>NodeType</c> or <c>Type</c> getter.</param>
    private void Hit(string member, bool getter = false)
    {
        Calls[member] = Calls.TryGetValue(member, out int count) ? count + 1 : 1;
        if (!Armed) return;

        // The caller is the first frame outside this class (Hit may be inlined into the counted member); a framework factory
        // inlined into an engine method has no frame of its own (see R4ReadAttribution).
        MethodBase? callerMethod = new System.Diagnostics.StackTrace(1, false).GetFrames()
            .Select(frame => frame.GetMethod())
            .FirstOrDefault(method => method?.DeclaringType != typeof(R4HostileExtension));
        Assembly? caller = callerMethod?.DeclaringType?.Assembly;
        if (getter && (caller == typeof(Expression).Assembly || R4ReadAttribution.IsInlinedFrameworkRead(callerMethod)))
        {
            FrameworkReads++;
            return;
        }

        if (getter && R4ReadAttribution.MayHoldInlinedCode(callerMethod))
        {
            UndecidedReads.Add($"{member} <- {callerMethod!.DeclaringType?.Name}.{callerMethod.Name}");
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

/// <summary>
/// Attributes a <c>NodeType</c>/<c>Type</c> getter read whose immediate caller is an optimized method outside the framework,
/// typically an engine method (closure audit, AUD-05).
/// </summary>
/// <remarks>
/// <para>
/// Once an engine method is hot, the tiered JIT recompiles it with optimizations, and, with dynamic PGO, may inline into it
/// the framework factory it calls, even through a delegate (<c>BuildRightAssociative(terms, Expression.Add)</c>). The
/// factory's getter reads then show the engine method as their caller. A test running after many engine calls (the closure
/// audit campaigns) saw <c>Type &lt;- ExpressionSimplifier.BuildPinnedAdditiveChain</c>, a method that never reads a getter:
/// every such read comes from <c>Expression.Add</c> or <c>Expression.Subtract</c>.
/// </para>
/// <para>
/// A read attributed to an optimized method is accepted as an inlined framework read when neither the method nor any method
/// of the engine or of its own assembly it can call or reference (directly, through a lambda or a local function) contains a
/// call to a <c>NodeType</c> or <c>Type</c> getter of an expression: such a method cannot read the node itself, whatever was
/// inlined into it. A virtual or interface call into these assemblies is not followed and makes the method a possible
/// reader, as does a method whose body cannot be decoded. A delegate the method receives and invokes is not followed.
/// </para>
/// <para>
/// Any other read attributed to an optimized method is undecidable: the method may read the getter, and may also hold an
/// inlined factory (reproduced with <c>CopyBlockExpression</c> and <c>Expression.Block</c> once the methods tier up at the
/// first call). It is recorded, not failed. Only an unoptimized (Debug) build attributes every read exactly, and the pull
/// request workflow runs these tests in Debug too (job <c>expression-engine-debug-tests</c>), where such a read fails.
/// </para>
/// </remarks>
internal static class R4ReadAttribution
{
    /// <summary>The engine assembly.</summary>
    private static readonly Assembly EngineAssembly = typeof(Utils.Expressions.ExpressionTransformer).Assembly;

    /// <summary>The framework assembly of the expression factories.</summary>
    private static readonly Assembly FrameworkAssembly = typeof(Expression).Assembly;

    /// <summary>The one- and two-byte IL opcodes by value.</summary>
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opCode => opCode.Value);

    /// <summary>The result of <see cref="MayReadNodeGetters"/> per method.</summary>
    private static readonly ConcurrentDictionary<MethodBase, bool> Readers = new();

    /// <summary>Tests whether <paramref name="assembly"/> is JIT-optimized (a Release build): only then can a factory be inlined into its methods.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns><see langword="true"/> for an optimized assembly.</returns>
    public static bool IsOptimized(Assembly assembly)
        => assembly.GetCustomAttributes<System.Diagnostics.DebuggableAttribute>().All(attribute => !attribute.IsJITOptimizerDisabled);

    /// <summary>Tests whether <paramref name="caller"/>, outside the framework, is optimized, so a framework factory may be inlined into it.</summary>
    /// <param name="caller">The method of the frame that called the getter.</param>
    /// <returns><see langword="true"/> when the frame may hold inlined framework code.</returns>
    public static bool MayHoldInlinedCode(MethodBase? caller)
        => caller?.DeclaringType?.Assembly is { } assembly && assembly != FrameworkAssembly && IsOptimized(assembly);

    /// <summary>Tests whether a getter read attributed to <paramref name="caller"/> is an inlined framework read.</summary>
    /// <param name="caller">The method of the frame that called the getter.</param>
    /// <returns><see langword="true"/> when <paramref name="caller"/> is an optimized method that cannot read the getters.</returns>
    public static bool IsInlinedFrameworkRead(MethodBase? caller)
        => MayHoldInlinedCode(caller) && !MayReadNodeGetters(caller!);

    /// <summary>
    /// Tests whether <paramref name="method"/> or a method of the engine or of its own assembly in its closure calls the
    /// <c>NodeType</c> or <c>Type</c> getter of an expression.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="false"/> only when no such call can be reached.</returns>
    public static bool MayReadNodeGetters(MethodBase method) => Readers.GetOrAdd(method, ComputeMayReadNodeGetters);

    /// <summary>Walks the closure of <paramref name="method"/> for <see cref="MayReadNodeGetters"/>.</summary>
    /// <param name="method">The method.</param>
    /// <returns>Whether a getter call can be reached.</returns>
    private static bool ComputeMayReadNodeGetters(MethodBase method)
    {
        var visited = new HashSet<MethodBase>();
        var pending = new Stack<MethodBase>();
        pending.Push(method);
        while (pending.Count > 0)
        {
            MethodBase current = pending.Pop();
            if (!visited.Add(current)) continue;
            if (!TryGetCallees(current, out List<MethodBase> callees)) return true;

            foreach (MethodBase callee in callees)
            {
                if (IsNodeGetter(callee)) return true;
                if (callee.DeclaringType?.Assembly != EngineAssembly && callee.DeclaringType?.Assembly != method.DeclaringType?.Assembly) continue;
                if (callee.IsAbstract || (callee.IsVirtual && !callee.IsFinal && callee.DeclaringType?.IsSealed != true)) return true;
                pending.Push(callee);
            }
        }

        return false;
    }

    /// <summary>Tests whether <paramref name="method"/> is the <c>NodeType</c> or <c>Type</c> getter of an expression type.</summary>
    /// <param name="method">The called method.</param>
    /// <returns><see langword="true"/> for such a getter.</returns>
    private static bool IsNodeGetter(MethodBase method)
        => method.Name is "get_NodeType" or "get_Type" && typeof(Expression).IsAssignableFrom(method.DeclaringType);

    /// <summary>Lists the methods <paramref name="method"/> calls, constructs or references (<c>ldftn</c>, <c>ldvirtftn</c>).</summary>
    /// <param name="method">The method.</param>
    /// <param name="callees">The methods found.</param>
    /// <returns><see langword="false"/> when the body cannot be decoded.</returns>
    private static bool TryGetCallees(MethodBase method, out List<MethodBase> callees)
    {
        callees = [];
        byte[]? il;
        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            return false;
        }

        if (il is null) return false;
        Type[]? typeArguments = method.DeclaringType is { IsGenericType: true } declaringType ? declaringType.GetGenericArguments() : null;
        Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        int index = 0;
        while (index < il.Length)
        {
            short value = il[index] == 0xFE ? (short)(0xFE00 | il[index + 1]) : il[index];
            if (!OpCodesByValue.TryGetValue(value, out OpCode opCode)) return false;
            index += opCode.Size;
            int operandSize = opCode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, index),
                _ => 4,
            };

            if (opCode.OperandType == OperandType.InlineMethod)
            {
                try
                {
                    if (method.Module.ResolveMethod(BitConverter.ToInt32(il, index), typeArguments, methodArguments) is { } callee) callees.Add(callee);
                }
                catch (Exception exception) when (exception is ArgumentException or BadImageFormatException)
                {
                    return false;
                }
            }

            index += operandSize;
        }

        return true;
    }
}
