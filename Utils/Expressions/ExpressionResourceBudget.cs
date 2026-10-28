using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Utils.Expressions;

/// <summary>
/// Per-thread resource session shared by the expression-tree engine (ETC R1a-Security): validates the input of every
/// public operation, counts transformation and structural visits against the operation's budgets, guards the
/// execution stack of every recursive walk, and turns an exhausted stack into an
/// <see cref="ExpressionResourceLimitException"/> at the outermost operation boundary.
/// </summary>
/// <remarks>
/// <para>
/// <b>Operations.</b> <see cref="Run{TState, TResult}"/> brackets one public operation. The outermost operation on a
/// thread starts the counters at zero with its own limits. A nested operation (a public call made by a rule or by user
/// code while an operation runs on the same thread) joins it: the counters keep running, its input is validated
/// against the tighter of the active and its own depth/node limits, and each visit budget becomes the tighter of the
/// active deadline and "visits already made + its own budget". When it returns, the previous limits are restored and
/// the visits it made stay counted. A nested call can therefore tighten, never loosen, and never reset the budget.
/// </para>
/// <para>
/// <b>Visits.</b> <see cref="ConsumeTransformVisit"/> is called once per execution of the transformation engine on a
/// node (whatever the entry: <c>TransformCore</c> or a prepared-operand path); <see cref="ConsumeStructuralVisit"/>
/// once per node reached by a structural walk (equality, hashing, canonical keys and their comparisons, substitution,
/// inlining analysis). Both also check the execution stack, so a walk that participates in a budget is
/// stack-guarded too. <see cref="EnsureStack"/> guards recursive helpers that are bounded by the size of their input
/// and do not count work.
/// </para>
/// <para>
/// <b>Isolation.</b> The state is <see cref="ThreadStaticAttribute"/>: the engine is synchronous, so a thread runs at
/// most one operation chain at a time and two threads never share counters or limits. An operation is never resumed on
/// another thread (no <c>await</c> inside the engine), which is why an <c>AsyncLocal</c> would only add cost. Outside
/// any operation the counters are not enforced (a walk entered without a boundary is still stack-guarded); every
/// entry point that can be reached without an enclosing operation opens one.
/// </para>
/// <para>
/// <b>Exceptions.</b> Limits are checked only at visit and validation points, never in <c>finally</c> or <c>catch</c>
/// blocks, so an exception already in flight is never replaced; <see cref="Run{TState, TResult}"/> restores the
/// previous state in a <c>finally</c> block that does not throw. At the outermost boundary only, an
/// <see cref="InsufficientExecutionStackException"/> becomes an <see cref="ExpressionResourceLimitException"/> of kind
/// <see cref="ExpressionResourceLimitKind.Stack"/>, and a resource exception wrapped by reflective rule invocation
/// (<see cref="TargetInvocationException"/> only) is unwrapped. Any other exception, including a user exception that
/// wraps a resource exception, propagates unchanged.
/// </para>
/// </remarks>
internal static class ExpressionResourceBudget
{
    /// <summary>Nesting level of the operations running on this thread (0: none).</summary>
    [ThreadStatic]
    private static int _operationDepth;

    /// <summary>Transformation visits made since the outermost operation started.</summary>
    [ThreadStatic]
    private static long _transformVisits;

    /// <summary>The value of <see cref="_transformVisits"/> above which the active transformation budget is exceeded.</summary>
    [ThreadStatic]
    private static long _transformDeadline;

    /// <summary>The configured transformation budget that defines <see cref="_transformDeadline"/>.</summary>
    [ThreadStatic]
    private static long _transformLimit;

    /// <summary>The value of <see cref="_transformVisits"/> when the budget of <see cref="_transformLimit"/> started.</summary>
    [ThreadStatic]
    private static long _transformBase;

    /// <summary>Structural visits made since the outermost operation started.</summary>
    [ThreadStatic]
    private static long _structuralVisits;

    /// <summary>The value of <see cref="_structuralVisits"/> above which the active structural budget is exceeded.</summary>
    [ThreadStatic]
    private static long _structuralDeadline;

    /// <summary>The configured structural budget that defines <see cref="_structuralDeadline"/>.</summary>
    [ThreadStatic]
    private static long _structuralLimit;

    /// <summary>The value of <see cref="_structuralVisits"/> when the budget of <see cref="_structuralLimit"/> started.</summary>
    [ThreadStatic]
    private static long _structuralBase;

    /// <summary>The active depth limit.</summary>
    [ThreadStatic]
    private static int _maxDepth;

    /// <summary>The active logical node limit.</summary>
    [ThreadStatic]
    private static long _maxNodes;

    /// <summary>The instance that owns the innermost running operation (a transformer or a comparer), or <see langword="null"/>.</summary>
    [ThreadStatic]
    private static object? _owner;

    /// <summary>Gets whether an operation is running on this thread.</summary>
    internal static bool IsActive => _operationDepth != 0;

    /// <summary>
    /// Gets the instance that owns the innermost running operation. <see cref="ExpressionTransformer.TransformCore(Expression)"/>
    /// recognizes its own recursion by it: a call on another (non engine-internal) instance is a nested public call.
    /// </summary>
    internal static object? Owner => _owner;

    /// <summary>Gets the operation nesting level on this thread (diagnostics and tests).</summary>
    internal static int OperationDepth => _operationDepth;

    /// <summary>Gets the transformation visits made by the running operation chain (diagnostics and tests).</summary>
    internal static long TransformVisits => _transformVisits;

    /// <summary>Gets the structural visits made by the running operation chain (diagnostics and tests).</summary>
    internal static long StructuralVisits => _structuralVisits;

    /// <summary>
    /// Runs one public operation: opens (or joins) the operation, validates up to two inputs against the active depth
    /// and node limits, runs <paramref name="body"/>, and restores the previous state.
    /// </summary>
    /// <typeparam name="TState">The state passed to <paramref name="body"/> (a value tuple avoids any closure allocation).</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="limits">The limits of the instance that owns the operation.</param>
    /// <param name="owner">The instance that owns the operation, or <see langword="null"/> for an internal walk.</param>
    /// <param name="root">The input to validate, or <see langword="null"/> for none.</param>
    /// <param name="secondRoot">A second input to validate (the other operand of an equality), or <see langword="null"/>.</param>
    /// <param name="state">The state passed to <paramref name="body"/>.</param>
    /// <param name="body">The operation; use a <see langword="static"/> lambda so the delegate is cached.</param>
    /// <returns>The result of <paramref name="body"/>.</returns>
    /// <exception cref="ExpressionResourceLimitException">An input or a budget exceeded a limit, or the stack ran out.</exception>
    internal static TResult Run<TState, TResult>(ExpressionResourceLimits limits, object? owner, Expression? root, Expression? secondRoot, TState state, Func<TState, TResult> body)
    {
        ArgumentNullException.ThrowIfNull(limits);
        Scope scope = Enter(limits, owner);
        try
        {
            ExpressionShapeValidator.Validate(root, _maxDepth, _maxNodes);
            ExpressionShapeValidator.Validate(secondRoot, _maxDepth, _maxNodes);
            return body(state);
        }
        catch (Exception exception) when (scope.IsOutermost && TryTranslate(exception, out Exception? translated, out bool created))
        {
            // A new Stack refusal is thrown as is; an unwrapped resource exception keeps its original stack trace.
            if (created) throw translated!;
            ExceptionDispatchInfo.Capture(translated!).Throw();
            throw;
        }
        finally
        {
            Exit(scope);
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> inside an operation with <see cref="ExpressionResourceLimits.Default"/> limits and no
    /// input validation when none is running, or directly inside the running one: the entry used by internal walks
    /// (canonical keys, raw structural equality) that can be reached without a public boundary.
    /// </summary>
    /// <typeparam name="TState">The state passed to <paramref name="body"/>.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="state">The state.</param>
    /// <param name="body">The walk; use a <see langword="static"/> lambda.</param>
    /// <returns>The result of <paramref name="body"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TResult RunInternal<TState, TResult>(TState state, Func<TState, TResult> body)
        => _operationDepth != 0 ? body(state) : Run(ExpressionResourceLimits.Default, null, null, null, state, body);

    /// <summary>Counts one transformation visit and checks the execution stack.</summary>
    /// <exception cref="ExpressionResourceLimitException">The transformation budget is exhausted.</exception>
    /// <exception cref="InsufficientExecutionStackException">The remaining stack is insufficient (translated at the outermost boundary).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConsumeTransformVisit()
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if (++_transformVisits > _transformDeadline) OnTransformBudgetExceeded();
    }

    /// <summary>Counts one structural visit and checks the execution stack.</summary>
    /// <exception cref="ExpressionResourceLimitException">The structural budget is exhausted.</exception>
    /// <exception cref="InsufficientExecutionStackException">The remaining stack is insufficient (translated at the outermost boundary).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConsumeStructuralVisit()
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if (++_structuralVisits > _structuralDeadline) OnStructuralBudgetExceeded();
    }

    /// <summary>Checks the execution stack before a recursive step that does not count work.</summary>
    /// <exception cref="InsufficientExecutionStackException">The remaining stack is insufficient (translated at the outermost boundary).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EnsureStack() => RuntimeHelpers.EnsureSufficientExecutionStack();

    /// <summary>Slow path of <see cref="ConsumeTransformVisit"/>: throws inside an operation, otherwise disables the check until the next one.</summary>
    /// <exception cref="ExpressionResourceLimitException">An operation is running and its transformation budget is exhausted.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OnTransformBudgetExceeded()
    {
        if (_operationDepth == 0)
        {
            _transformDeadline = long.MaxValue;
            return;
        }

        throw new ExpressionResourceLimitException(ExpressionResourceLimitKind.Work, _transformLimit, _transformVisits - _transformBase);
    }

    /// <summary>Slow path of <see cref="ConsumeStructuralVisit"/>: throws inside an operation, otherwise disables the check until the next one.</summary>
    /// <exception cref="ExpressionResourceLimitException">An operation is running and its structural budget is exhausted.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OnStructuralBudgetExceeded()
    {
        if (_operationDepth == 0)
        {
            _structuralDeadline = long.MaxValue;
            return;
        }

        throw new ExpressionResourceLimitException(ExpressionResourceLimitKind.StructuralWork, _structuralLimit, _structuralVisits - _structuralBase);
    }

    /// <summary>Opens or joins an operation and returns the state to restore.</summary>
    /// <param name="limits">The limits of the entering instance.</param>
    /// <param name="owner">The entering instance.</param>
    /// <returns>The saved state.</returns>
    private static Scope Enter(ExpressionResourceLimits limits, object? owner)
    {
        var scope = new Scope(
            _owner,
            _operationDepth,
            _transformDeadline,
            _transformLimit,
            _transformBase,
            _structuralDeadline,
            _structuralLimit,
            _structuralBase,
            _maxDepth,
            _maxNodes);

        if (_operationDepth == 0)
        {
            _transformVisits = 0;
            _structuralVisits = 0;
            _transformBase = 0;
            _structuralBase = 0;
            _transformLimit = limits.MaxTransformVisits;
            _structuralLimit = limits.MaxStructuralVisits;
            _transformDeadline = limits.MaxTransformVisits;
            _structuralDeadline = limits.MaxStructuralVisits;
            _maxDepth = limits.MaxDepth;
            _maxNodes = limits.MaxNodes;
        }
        else
        {
            long transformDeadline = SaturatingAdd(_transformVisits, limits.MaxTransformVisits);
            if (transformDeadline < _transformDeadline)
            {
                _transformDeadline = transformDeadline;
                _transformLimit = limits.MaxTransformVisits;
                _transformBase = _transformVisits;
            }

            long structuralDeadline = SaturatingAdd(_structuralVisits, limits.MaxStructuralVisits);
            if (structuralDeadline < _structuralDeadline)
            {
                _structuralDeadline = structuralDeadline;
                _structuralLimit = limits.MaxStructuralVisits;
                _structuralBase = _structuralVisits;
            }

            _maxDepth = Math.Min(_maxDepth, limits.MaxDepth);
            _maxNodes = Math.Min(_maxNodes, limits.MaxNodes);
        }

        _operationDepth++;
        _owner = owner;
        return scope;
    }

    /// <summary>Restores the state saved by <see cref="Enter"/>; the outermost exit also clears the counters.</summary>
    /// <param name="scope">The saved state.</param>
    private static void Exit(in Scope scope)
    {
        _owner = scope.Owner;
        _operationDepth = scope.OperationDepth;
        _transformDeadline = scope.TransformDeadline;
        _transformLimit = scope.TransformLimit;
        _transformBase = scope.TransformBase;
        _structuralDeadline = scope.StructuralDeadline;
        _structuralLimit = scope.StructuralLimit;
        _structuralBase = scope.StructuralBase;
        _maxDepth = scope.MaxDepth;
        _maxNodes = scope.MaxNodes;
        if (scope.IsOutermost)
        {
            _transformVisits = 0;
            _structuralVisits = 0;
        }
    }

    /// <summary>
    /// Decides whether the outermost boundary must translate <paramref name="exception"/>: an
    /// <see cref="InsufficientExecutionStackException"/> becomes a <see cref="ExpressionResourceLimitKind.Stack"/>
    /// refusal, and a resource exception reached only through <see cref="TargetInvocationException"/> layers is
    /// unwrapped. A bare resource exception and every other exception are left alone.
    /// </summary>
    /// <param name="exception">The exception leaving the operation.</param>
    /// <param name="translated">The exception to throw instead.</param>
    /// <param name="created">Whether <paramref name="translated"/> is a new exception (a Stack refusal) rather than an unwrapped one.</param>
    /// <returns><see langword="true"/> when <paramref name="exception"/> must be replaced.</returns>
    private static bool TryTranslate(Exception exception, out Exception? translated, out bool created)
    {
        Exception innermost = exception;
        while (innermost is TargetInvocationException { InnerException: { } inner })
        {
            innermost = inner;
        }

        created = innermost is InsufficientExecutionStackException;
        translated = innermost switch
        {
            InsufficientExecutionStackException stack => new ExpressionResourceLimitException(ExpressionResourceLimitKind.Stack, 0, 0, stack),
            ExpressionResourceLimitException resource when !ReferenceEquals(resource, exception) => resource,
            _ => null,
        };
        return translated is not null;
    }

    /// <summary>Adds two non-negative counts, saturating at <see cref="long.MaxValue"/>.</summary>
    /// <param name="a">The first count.</param>
    /// <param name="b">The second count.</param>
    /// <returns>The saturated sum.</returns>
    private static long SaturatingAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;

    /// <summary>The state saved when an operation is opened or joined, restored when it ends.</summary>
    /// <param name="Owner">The previous owner.</param>
    /// <param name="OperationDepth">The nesting level before the operation.</param>
    /// <param name="TransformDeadline">The previous transformation deadline.</param>
    /// <param name="TransformLimit">The previous transformation limit.</param>
    /// <param name="TransformBase">The previous transformation base.</param>
    /// <param name="StructuralDeadline">The previous structural deadline.</param>
    /// <param name="StructuralLimit">The previous structural limit.</param>
    /// <param name="StructuralBase">The previous structural base.</param>
    /// <param name="MaxDepth">The previous depth limit.</param>
    /// <param name="MaxNodes">The previous node limit.</param>
    private readonly record struct Scope(
        object? Owner,
        int OperationDepth,
        long TransformDeadline,
        long TransformLimit,
        long TransformBase,
        long StructuralDeadline,
        long StructuralLimit,
        long StructuralBase,
        int MaxDepth,
        long MaxNodes)
    {
        /// <summary>Gets whether this scope is the outermost operation of the thread.</summary>
        public bool IsOutermost => OperationDepth == 0;
    }
}
