namespace Utils.Expressions;

/// <summary>
/// Receives the events of the expression transformation engine (<see cref="ExpressionTransformer"/>, the simplifier and every
/// derived transformer) while a tracing scope opened by <see cref="ExpressionTransformTracing.Begin"/> is active.
/// </summary>
/// <remarks>
/// <para>
/// <b>Calls.</b> The engine calls <see cref="OnEvent"/> synchronously, on the thread that runs the traced operation, in the
/// order of <see cref="ExpressionTransformEvent.Sequence"/>. The events of one operation are never delivered concurrently,
/// but a tracer shared by operations running on several threads (a scope that flows into parallel tasks) receives their
/// events concurrently and must synchronize its own state, as <see cref="ExpressionTraceCollector"/> does.
/// </para>
/// <para>
/// <b>Exceptions.</b> An exception thrown by <see cref="OnEvent"/> never reaches the traced operation and never changes its
/// result: the engine catches it, stops tracing for the rest of the scope and exposes it through
/// <see cref="ExpressionTraceScope.TracerException"/>.
/// </para>
/// <para>
/// <b>Re-entry.</b> <see cref="OnEvent"/> must not call the expression engine (simplifier, comparer, transformer,
/// substitution). Such a call is refused with an <see cref="InvalidOperationException"/> before it starts, which counts as an
/// exception of the tracer (see above), so a tracer can never trigger a transformation, a comparison or a memo entry of the
/// operation it observes.
/// </para>
/// <para>
/// <b>Expressions.</b> The events carry references to the trees, never a rendering. Calling <see cref="object.ToString"/> on
/// them runs the framework's printer, which reads extension nodes; <see cref="ExpressionTraceFormatter"/> renders them without
/// inspecting opaque nodes. Format outside the callback when possible: the callback runs on the engine's execution stack.
/// </para>
/// </remarks>
public interface IExpressionTransformTracer
{
    /// <summary>Receives one event of a traced operation.</summary>
    /// <param name="traceEvent">The event. Copy it to keep it: the reference is only valid during the call.</param>
    void OnEvent(in ExpressionTransformEvent traceEvent);
}
