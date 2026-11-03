using System.Threading;

namespace Utils.Expressions;

/// <summary>
/// Opens tracing scopes for the expression transformation engine: every operation of the engine (simplification, comparison,
/// transformation, substitution) started while a scope is active on the current execution context reports its events to the
/// scope's tracer.
/// </summary>
/// <example>
/// <code>
/// var collector = new ExpressionTraceCollector(capacity: 10_000);
/// using (ExpressionTransformTracing.Begin(collector))
/// {
///     simplifier.Simplify(expression);
/// }
///
/// Console.WriteLine(ExpressionTraceExporter.ToText(collector.GetEvents()));
/// </code>
/// </example>
/// <remarks>
/// <para>
/// <b>Isolation.</b> A scope is ambient per execution context (<see cref="AsyncLocal{T}"/>): it flows into the tasks and
/// continuations started inside it, never into concurrent operations started elsewhere, and a scope opened inside another one
/// replaces it until it is disposed. No tracer is ever stored on a transformer instance, so a shared simplifier traces only
/// the calls made inside a scope.
/// </para>
/// <para>
/// <b>Capture.</b> The scope is read once, when an operation starts on a thread where no expression operation runs; the whole
/// operation, including its nested comparisons, nested public calls and re-entrant rules, reports to that tracer. Opening or
/// disposing a scope while an operation runs (from a rule or a tracer) only affects the next operations.
/// </para>
/// <para>
/// <b>Cost.</b> Without any open scope in the process, starting an operation reads one static counter and the engine takes its
/// usual path: no event, session or rendering is created.
/// </para>
/// </remarks>
public static class ExpressionTransformTracing
{
    /// <summary>The innermost scope of the current execution context.</summary>
    private static readonly AsyncLocal<ExpressionTraceScope?> s_current = new();

    /// <summary>The number of scopes opened and not yet disposed in the process: 0 makes the capture a single static read.</summary>
    private static int s_openScopes;

    /// <summary>
    /// Opens a tracing scope on the current execution context: the operations started inside it report to
    /// <paramref name="tracer"/> until the scope is disposed.
    /// </summary>
    /// <param name="tracer">The tracer receiving the events.</param>
    /// <param name="detail">The level of detail.</param>
    /// <returns>The scope; dispose it to stop tracing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tracer"/> is <see langword="null"/>.</exception>
    public static ExpressionTraceScope Begin(IExpressionTransformTracer tracer, ExpressionTraceDetail detail = ExpressionTraceDetail.Standard)
    {
        ArgumentNullException.ThrowIfNull(tracer);
        var scope = new ExpressionTraceScope(tracer, detail, s_current.Value);
        Interlocked.Increment(ref s_openScopes);
        s_current.Value = scope;
        return scope;
    }

    /// <summary>Gets the scope the next operation started on the current execution context would report to, if any.</summary>
    public static ExpressionTraceScope? Current => FindActive(s_current.Value);

    /// <summary>
    /// Gets the scope an operation starting now must report to, or <see langword="null"/>: the fast path of the engine reads only
    /// the process-wide counter of open scopes.
    /// </summary>
    /// <returns>The active, non-faulted scope, or <see langword="null"/>.</returns>
    internal static ExpressionTraceScope? CaptureForOperation()
        => Volatile.Read(ref s_openScopes) == 0 ? null : CaptureSlow();

    /// <summary>Reads the ambient scope (slow path of <see cref="CaptureForOperation"/>).</summary>
    /// <returns>The active, non-faulted scope, or <see langword="null"/>.</returns>
    private static ExpressionTraceScope? CaptureSlow()
    {
        ExpressionTraceScope? scope = FindActive(s_current.Value);
        return scope is null || scope.IsFaulted ? null : scope;
    }

    /// <summary>Skips the disposed scopes (a scope disposed out of order, or from another execution context).</summary>
    /// <param name="scope">The innermost scope of the execution context.</param>
    /// <returns>The innermost scope that is not disposed, or <see langword="null"/>.</returns>
    private static ExpressionTraceScope? FindActive(ExpressionTraceScope? scope)
    {
        while (scope is { IsDisposed: true })
        {
            scope = scope.Parent;
        }

        return scope;
    }

    /// <summary>Closes <paramref name="scope"/>: called once by <see cref="ExpressionTraceScope.Dispose"/>.</summary>
    /// <param name="scope">The scope being disposed.</param>
    internal static void Close(ExpressionTraceScope scope)
    {
        Interlocked.Decrement(ref s_openScopes);
        if (ReferenceEquals(s_current.Value, scope))
        {
            s_current.Value = FindActive(scope.Parent);
        }
    }
}

/// <summary>
/// A tracing scope opened by <see cref="ExpressionTransformTracing.Begin"/>: dispose it to stop tracing.
/// </summary>
public sealed class ExpressionTraceScope : IDisposable
{
    /// <summary>The first exception thrown by the tracer, or <see langword="null"/>.</summary>
    private Exception? _tracerException;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>The number of events dropped before delivery.</summary>
    private long _undeliveredEvents;

    /// <summary>Initializes a scope.</summary>
    /// <param name="tracer">The tracer.</param>
    /// <param name="detail">The level of detail.</param>
    /// <param name="parent">The scope it replaces until it is disposed.</param>
    internal ExpressionTraceScope(IExpressionTransformTracer tracer, ExpressionTraceDetail detail, ExpressionTraceScope? parent)
    {
        Tracer = tracer;
        Detail = detail;
        Parent = parent;
    }

    /// <summary>Gets the tracer receiving the events of the scope.</summary>
    public IExpressionTransformTracer Tracer { get; }

    /// <summary>Gets the level of detail of the scope.</summary>
    public ExpressionTraceDetail Detail { get; }

    /// <summary>
    /// Gets the first exception thrown by the tracer, or by an engine call the tracer attempted (refused), or
    /// <see langword="null"/>. Once it is set, the operations of the scope are no longer traced; the traced operations are never
    /// affected by it.
    /// </summary>
    public Exception? TracerException => Volatile.Read(ref _tracerException);

    /// <summary>Gets whether the tracer failed (see <see cref="TracerException"/>).</summary>
    public bool IsFaulted => TracerException is not null;

    /// <summary>Gets whether the scope was disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Gets the number of events of the scope's operations that were created but never delivered to the tracer, because the
    /// bounded list of events awaiting delivery was full (an operation creating many events while the stack was too short to
    /// call the tracer) or because the stack was still too short when the operation ended. A dropped event leaves a gap in the
    /// <see cref="ExpressionTransformEvent.Sequence"/> numbers; the resource refusal and the failure of the root treatment that
    /// conclude a failed operation are kept. The events a tracer itself discards (a bounded collector, for instance) are not
    /// counted here.
    /// </summary>
    public long UndeliveredEvents => Interlocked.Read(ref _undeliveredEvents);

    /// <summary>
    /// Gets or sets the number of ordinary events an operation of the scope keeps while their delivery is deferred (internal
    /// tuning point, read when an operation starts).
    /// </summary>
    internal int PendingCapacity { get; set; } = ExpressionTraceSession.DefaultPendingCapacity;

    /// <summary>Gets the scope this one replaced.</summary>
    internal ExpressionTraceScope? Parent { get; }

    /// <summary>Records the first exception of the tracer: the scope stops tracing.</summary>
    /// <param name="exception">The exception thrown by the tracer.</param>
    internal void ReportFault(Exception exception) => Interlocked.CompareExchange(ref _tracerException, exception, null);

    /// <summary>Counts events dropped before delivery (see <see cref="UndeliveredEvents"/>).</summary>
    /// <param name="count">The number of dropped events.</param>
    internal void ReportUndelivered(long count) => Interlocked.Add(ref _undeliveredEvents, count);

    /// <summary>Stops tracing: the next operations of the execution context report to the enclosing scope, if any.</summary>
    /// <remarks>An operation already running keeps reporting to this scope's tracer until it returns.</remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            ExpressionTransformTracing.Close(this);
        }
    }
}
