using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Utils.Expressions;

/// <summary>
/// The tracing state of one traced operation (see <see cref="ExpressionTransformTracing"/>): the stack of open treatments, the
/// event numbering and the delivery of the events to the scope's tracer. Created only when an operation starts inside a
/// tracing scope, held by the thread's resource session, and dropped when the operation ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>Treatments.</b> Every traced <c>TransformCore</c> call opens a frame (<see cref="EnterTreatment"/>) and closes it
/// (<see cref="ExitTreatment"/>, or <see cref="TreatmentFailed"/> when an exception leaves it). The frame keeps the rule
/// currently running on its node, so a nested treatment knows the rule that requested it and a failure knows the rule it
/// crossed.
/// </para>
/// <para>
/// <b>Delivery.</b> An event is delivered to the tracer immediately when the stack allows it. Otherwise, and for every failure
/// event, it is kept in order in a pending list: <see cref="TreatmentFailed"/> runs in an exception filter, on the stack of the
/// throw site (which may be the exhausted stack of a <see cref="ExpressionResourceLimitKind.Stack"/> refusal), so it records
/// without calling user code. The pending events are delivered, still in order, by the next delivery made on a sufficient
/// stack or by <see cref="Complete"/>, which the outermost operation boundary calls after the stack has unwound.
/// </para>
/// <para>
/// <b>Bounded pending list.</b> The pending list is bounded independently of the tracer (a bounded collector only bounds what it
/// keeps after delivery): it keeps at most <see cref="ExpressionTraceScope.PendingCapacity"/> events, plus
/// <see cref="TerminalReserve"/> slots kept for the events that conclude a failure (a
/// <see cref="ExpressionTransformEventKind.ResourceLimitReached"/> event and the failure of a root treatment), so the final
/// refusal is still delivered when the list is full. The other events are dropped, counted on
/// <see cref="ExpressionTraceScope.UndeliveredEvents"/>, and leave a gap in the sequence numbers.
/// </para>
/// <para>
/// <b>Isolation of the tracer.</b> During a delivery the thread's resource session is replaced by a session that refuses every
/// operation, so the tracer cannot start a transformation, a comparison or a memo entry in the operation it observes. Any
/// exception of the tracer (a refused engine call included) stops the tracing of the scope and is reported on it
/// (<see cref="ExpressionTraceScope.TracerException"/>); it never reaches the operation.
/// </para>
/// </remarks>
internal sealed class ExpressionTraceSession
{
    /// <summary>The default number of ordinary events the pending list keeps (with the list's growth, under 4 MB of events).</summary>
    internal const int DefaultPendingCapacity = 16_384;

    /// <summary>The pending slots kept, beyond the capacity, for the events that conclude a failure.</summary>
    internal const int TerminalReserve = 64;

    /// <summary>The last session identifier handed out in the process.</summary>
    private static long s_lastSessionId;

    /// <summary>The scope the session reports to.</summary>
    private readonly ExpressionTraceScope _scope;

    /// <summary>The tracer of <see cref="_scope"/>.</summary>
    private readonly IExpressionTransformTracer _tracer;

    /// <summary>The identifier of the traced operation.</summary>
    private readonly long _sessionId;

    /// <summary>The sequence number of the last event created.</summary>
    private long _sequence;

    /// <summary>The identifier of the last treatment opened.</summary>
    private long _lastOperationId;

    /// <summary>The open treatments, innermost last.</summary>
    private Frame[] _frames = new Frame[32];

    /// <summary>The number of open treatments.</summary>
    private int _count;

    /// <summary>The events created but not delivered yet, in sequence order.</summary>
    private List<ExpressionTransformEvent>? _pending;

    /// <summary>The number of ordinary events <see cref="_pending"/> keeps (see <see cref="ExpressionTraceScope.PendingCapacity"/>).</summary>
    private readonly int _pendingCapacity;

    /// <summary>The resource failure already reported by a <see cref="ExpressionTransformEventKind.ResourceLimitReached"/> event.</summary>
    private Exception? _reportedResourceFailure;

    /// <summary>The refusing resource session installed during deliveries (created on the first delivery).</summary>
    private ExpressionResourceBudget.Session? _callbackSession;

    /// <summary>Whether tracing stopped because the tracer failed.</summary>
    private bool _stopped;

    /// <summary>Initializes the tracing state of an operation starting in <paramref name="scope"/>.</summary>
    /// <param name="scope">The active scope.</param>
    private ExpressionTraceSession(ExpressionTraceScope scope)
    {
        _scope = scope;
        _tracer = scope.Tracer;
        Detailed = scope.Detail == ExpressionTraceDetail.Detailed;
        _pendingCapacity = scope.PendingCapacity;
        _sessionId = Interlocked.Increment(ref s_lastSessionId);
    }

    /// <summary>Gets whether the scope asked for the detailed events (candidates, rejections, scopes).</summary>
    internal bool Detailed { get; }

    /// <summary>Gets whether the session still delivers events (the tracer has not failed).</summary>
    internal bool IsActive => !_stopped;

    /// <summary>One open treatment.</summary>
    private struct Frame
    {
        /// <summary>The treatment identifier.</summary>
        public long Id;

        /// <summary>The node being transformed.</summary>
        public Expression Input;

        /// <summary>The runtime type of the transformer.</summary>
        public Type TransformerType;

        /// <summary>The rule running on the node, if any.</summary>
        public ExpressionTransformRuleInfo? CurrentRule;

        /// <summary>The rule that produced the node's result, if any.</summary>
        public ExpressionTransformRuleInfo? AppliedRule;
    }

    /// <summary>
    /// Starts the tracing state of an operation when a tracing scope is active, otherwise returns <see langword="null"/> after a
    /// single static read.
    /// </summary>
    /// <returns>The tracing state, or <see langword="null"/>.</returns>
    internal static ExpressionTraceSession? TryStart()
    {
        ExpressionTraceScope? scope = ExpressionTransformTracing.CaptureForOperation();
        return scope is null ? null : new ExpressionTraceSession(scope);
    }

    /// <summary>Opens a treatment and reports <see cref="ExpressionTransformEventKind.TransformEnter"/>.</summary>
    /// <param name="transformerType">The runtime type of the transformer.</param>
    /// <param name="input">The node to transform.</param>
    /// <returns>The frame index of the treatment, to pass to <see cref="ExitTreatment"/> and <see cref="TreatmentFailed"/>.</returns>
    internal int EnterTreatment(Type transformerType, Expression input)
    {
        if (_stopped) return -1;

        if (_count == _frames.Length) Array.Resize(ref _frames, _count * 2);
        int index = _count;
        long parent = index == 0 ? 0 : _frames[index - 1].Id;
        ExpressionTransformRuleInfo? requestingRule = index == 0 ? null : _frames[index - 1].CurrentRule;
        long id = ++_lastOperationId;
        _frames[index] = new Frame { Id = id, Input = input, TransformerType = transformerType };
        _count = index + 1;
        Emit(Create(ExpressionTransformEventKind.TransformEnter, id, parent, index, transformerType, requestingRule, input, null, ExpressionTransformOutcome.None));
        return index;
    }

    /// <summary>Closes a treatment that returned and reports <see cref="ExpressionTransformEventKind.TransformExit"/>.</summary>
    /// <param name="index">The frame index returned by <see cref="EnterTreatment"/>.</param>
    /// <param name="input">The node that was transformed.</param>
    /// <param name="prepared">The node rebuilt by the traversal, before rules and finalization.</param>
    /// <param name="result">The result of the treatment.</param>
    /// <param name="boundary">Whether the node was a transformation boundary.</param>
    internal void ExitTreatment(int index, Expression input, Expression prepared, Expression? result, bool boundary)
    {
        if (_stopped || index < 0 || index >= _count) return;

        Frame frame = _frames[index];
        _frames[index] = default;
        _count = index;
        ExpressionTransformOutcome outcome = boundary
            ? ExpressionTransformOutcome.Boundary
            : frame.AppliedRule is not null ? ExpressionTransformOutcome.RuleApplied : Classify(input, prepared, result);
        Emit(Create(ExpressionTransformEventKind.TransformExit, frame.Id, ParentOf(index), index, frame.TransformerType, frame.AppliedRule, input, result, outcome));
    }

    /// <summary>
    /// Records that an exception left a treatment, from an exception filter: never delivers, never throws, always returns
    /// <see langword="false"/> so that the exception keeps propagating untouched.
    /// </summary>
    /// <param name="index">The frame index of the treatment.</param>
    /// <param name="exception">The exception leaving it.</param>
    /// <returns><see langword="false"/>.</returns>
    internal bool TreatmentFailed(int index, Exception exception)
    {
        if (_stopped || index < 0 || index >= _count) return false;

        Frame frame = _frames[index];
        Exception innermost = Unwrap(exception);
        if (ResourceLimitOf(innermost) is { } limit && !ReferenceEquals(innermost, _reportedResourceFailure))
        {
            _reportedResourceFailure = innermost;
            Record(CreateFailure(ExpressionTransformEventKind.ResourceLimitReached, frame, index, innermost, limit));
        }

        Record(CreateFailure(ExpressionTransformEventKind.TransformFailed, frame, index, exception, null));
        for (int i = index; i < _count; i++) _frames[i] = default;
        _count = index;
        return false;
    }

    /// <summary>Marks <paramref name="rule"/> as running on the innermost treatment.</summary>
    /// <param name="rule">The rule about to be invoked.</param>
    internal void BeginRule(ExpressionTransformRuleInfo rule)
    {
        if (_count > 0) _frames[_count - 1].CurrentRule = rule;
    }

    /// <summary>Reports that the running rule produced a result (<see cref="ExpressionTransformEventKind.RuleApplied"/>).</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="input">The prepared node the rule received.</param>
    /// <param name="output">The rule's result.</param>
    internal void RuleApplied(ExpressionTransformRuleInfo rule, Expression input, Expression? output)
    {
        if (_stopped || _count == 0) return;

        int index = _count - 1;
        _frames[index].CurrentRule = null;
        _frames[index].AppliedRule = rule;
        Emit(Create(ExpressionTransformEventKind.RuleApplied, _frames[index].Id, ParentOf(index), index, _frames[index].TransformerType, rule, input, output, ExpressionTransformOutcome.RuleApplied));
    }

    /// <summary>Reports that the running rule produced no result; the rejection event itself belongs to the detailed level.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="input">The prepared node.</param>
    /// <param name="rejection">Why it produced no result.</param>
    internal void RuleDeclined(ExpressionTransformRuleInfo rule, Expression input, ExpressionRuleRejection rejection)
    {
        if (_stopped || _count == 0) return;

        _frames[_count - 1].CurrentRule = null;
        if (Detailed) RuleRejected(rule, input, rejection);
    }

    /// <summary>(Detailed level) Reports a candidate rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="input">The prepared node.</param>
    internal void RuleCandidate(ExpressionTransformRuleInfo rule, Expression input)
    {
        if (_stopped || _count == 0) return;

        int index = _count - 1;
        Emit(Create(ExpressionTransformEventKind.RuleCandidate, _frames[index].Id, ParentOf(index), index, _frames[index].TransformerType, rule, input, null, ExpressionTransformOutcome.None));
    }

    /// <summary>(Detailed level) Reports a rejected rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="input">The prepared node.</param>
    /// <param name="rejection">Why it produced no result.</param>
    internal void RuleRejected(ExpressionTransformRuleInfo rule, Expression input, ExpressionRuleRejection rejection)
    {
        if (_stopped || _count == 0) return;

        int index = _count - 1;
        Emit(new ExpressionTransformEvent(
            ExpressionTransformEventKind.RuleRejected, _sessionId, ++_sequence, _frames[index].Id, ParentOf(index), index, NodeTypeOf(input),
            _frames[index].TransformerType, rule, input, null, ExpressionTransformOutcome.None, rejection, ExpressionTraceScopeKind.None, null, null));
    }

    /// <summary>(Detailed level) Reports that the chain rooted at <paramref name="root"/> was extracted for its flatten rules.</summary>
    /// <param name="root">The root of the chain.</param>
    internal void ChainFlattened(Expression root)
    {
        if (_stopped || _count == 0) return;

        int index = _count - 1;
        Emit(Create(ExpressionTransformEventKind.ChainFlattened, _frames[index].Id, ParentOf(index), index, _frames[index].TransformerType, null, root, null, ExpressionTransformOutcome.None));
    }

    /// <summary>Reports the finalization of the innermost treatment (<see cref="ExpressionTransformEventKind.Finalize"/>).</summary>
    /// <param name="prepared">The node given to <c>FinalizeExpression</c>.</param>
    /// <param name="finalized">Its result.</param>
    internal void Finalized(Expression prepared, Expression? finalized)
    {
        if (_stopped || _count == 0) return;

        int index = _count - 1;
        Emit(Create(ExpressionTransformEventKind.Finalize, _frames[index].Id, ParentOf(index), index, _frames[index].TransformerType, null, prepared, finalized, Classify(prepared, prepared, finalized)));
    }

    /// <summary>(Detailed level) Reports that the traversal enters or leaves a lexical scope.</summary>
    /// <param name="kind">The event kind (<see cref="ExpressionTransformEventKind.ScopeEnter"/> or <see cref="ExpressionTransformEventKind.ScopeExit"/>).</param>
    /// <param name="scopeKind">The scope.</param>
    /// <param name="node">The lambda, the block, or the body of the catch handler.</param>
    internal void Scope(ExpressionTransformEventKind kind, ExpressionTraceScopeKind scopeKind, Expression? node)
    {
        if (_stopped) return;

        int index = _count - 1;
        long id = index < 0 ? 0 : _frames[index].Id;
        Emit(new ExpressionTransformEvent(
            kind, _sessionId, ++_sequence, id, index < 0 ? 0 : ParentOf(index), int.Max(index, 0), NodeTypeOf(node),
            index < 0 ? null : _frames[index].TransformerType, null, node, null, ExpressionTransformOutcome.None, ExpressionRuleRejection.None,
            scopeKind, null, null));
    }

    /// <summary>
    /// Ends the session at the outermost operation boundary, once the stack has unwound: reports a resource refusal that no
    /// treatment crossed (the input validation), then delivers the pending events.
    /// </summary>
    /// <param name="failure">The exception that ends the operation, or <see langword="null"/>.</param>
    /// <remarks>
    /// Pending events that still cannot be delivered (the caller itself runs out of stack) are dropped and counted on
    /// <see cref="ExpressionTraceScope.UndeliveredEvents"/>.
    /// </remarks>
    internal void Complete(Exception? failure)
    {
        if (_stopped) return;

        if (failure is not null)
        {
            Exception innermost = Unwrap(failure);
            if (ResourceLimitOf(innermost) is { } limit && !ReferenceEquals(innermost, _reportedResourceFailure))
            {
                _reportedResourceFailure = innermost;
                Record(new ExpressionTransformEvent(
                    ExpressionTransformEventKind.ResourceLimitReached, _sessionId, ++_sequence, 0, 0, 0, null, null, null, null, null,
                    ExpressionTransformOutcome.None, ExpressionRuleRejection.None, ExpressionTraceScopeKind.None, innermost, limit));
            }
        }

        if (!TryFlush() && !_stopped) _scope.ReportUndelivered(_pending!.Count);
        _pending = null;
        _frames = [];
        _count = 0;
    }

    /// <summary>Creates a regular event of the session.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="operationId">The treatment.</param>
    /// <param name="parentId">The enclosing treatment.</param>
    /// <param name="depth">The depth of the treatment.</param>
    /// <param name="transformerType">The transformer type.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="input">The input node.</param>
    /// <param name="output">The output node.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The event, with the next sequence number.</returns>
    private ExpressionTransformEvent Create(
        ExpressionTransformEventKind kind, long operationId, long parentId, int depth, Type transformerType,
        ExpressionTransformRuleInfo? rule, Expression? input, Expression? output, ExpressionTransformOutcome outcome)
        => new(kind, _sessionId, ++_sequence, operationId, parentId, depth, NodeTypeOf(input), transformerType, rule, input, output, outcome,
            ExpressionRuleRejection.None, ExpressionTraceScopeKind.None, null, null);

    /// <summary>Creates a failure event for an open frame.</summary>
    /// <param name="kind"><see cref="ExpressionTransformEventKind.TransformFailed"/> or <see cref="ExpressionTransformEventKind.ResourceLimitReached"/>.</param>
    /// <param name="frame">The frame the exception crossed.</param>
    /// <param name="index">Its index.</param>
    /// <param name="exception">The exception.</param>
    /// <param name="limit">The refused resource, for a resource event.</param>
    /// <returns>The event, with the next sequence number.</returns>
    private ExpressionTransformEvent CreateFailure(ExpressionTransformEventKind kind, Frame frame, int index, Exception exception, ExpressionResourceLimitKind? limit)
        => new(kind, _sessionId, ++_sequence, frame.Id, ParentOf(index), index, NodeTypeOf(frame.Input), frame.TransformerType, frame.CurrentRule,
            frame.Input, null, ExpressionTransformOutcome.None, ExpressionRuleRejection.None, ExpressionTraceScopeKind.None, exception, limit);

    /// <summary>Gets the identifier of the treatment enclosing the frame at <paramref name="index"/>.</summary>
    /// <param name="index">The frame index.</param>
    /// <returns>The parent identifier, or 0 for a root treatment.</returns>
    private long ParentOf(int index) => index == 0 ? 0 : _frames[index - 1].Id;

    /// <summary>Delivers an event now if the stack allows it and nothing is pending, otherwise keeps it in order.</summary>
    /// <param name="traceEvent">The event.</param>
    private void Emit(in ExpressionTransformEvent traceEvent)
    {
        if ((_pending is { Count: > 0 } && !TryFlush()) || !RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            Record(traceEvent);
            return;
        }

        Deliver(traceEvent);
    }

    /// <summary>
    /// Keeps an event for a later delivery (never calls the tracer), or drops and counts it when the pending list is full; the
    /// events concluding a failure use the reserve beyond the capacity.
    /// </summary>
    /// <param name="traceEvent">The event.</param>
    private void Record(in ExpressionTransformEvent traceEvent)
    {
        List<ExpressionTransformEvent> pending = _pending ??= [];
        int limit = IsTerminal(traceEvent) ? _pendingCapacity + TerminalReserve : _pendingCapacity;
        if (pending.Count >= limit)
        {
            _scope.ReportUndelivered(1);
            return;
        }

        pending.Add(traceEvent);
    }

    /// <summary>Gets whether an event concludes a failure: a resource refusal, or the failure of a root treatment.</summary>
    /// <param name="traceEvent">The event.</param>
    /// <returns><see langword="true"/> for a terminal event.</returns>
    private static bool IsTerminal(in ExpressionTransformEvent traceEvent)
        => traceEvent.Kind == ExpressionTransformEventKind.ResourceLimitReached
            || (traceEvent.Kind == ExpressionTransformEventKind.TransformFailed && traceEvent.Depth == 0);

    /// <summary>Delivers the pending events in order when the stack allows it.</summary>
    /// <returns><see langword="true"/> when nothing is left pending.</returns>
    private bool TryFlush()
    {
        if (_pending is not { Count: > 0 } pending) return true;
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack()) return false;

        // A delivery cannot add events (the tracer cannot reach the engine), so the list is stable while it is read.
        for (int i = 0; i < pending.Count && !_stopped; i++)
        {
            Deliver(pending[i]);
        }

        pending.Clear();
        return true;
    }

    /// <summary>
    /// Calls the tracer with the thread's resource session replaced by a refusing one; an exception of the tracer stops the
    /// tracing of the scope and is reported on it.
    /// </summary>
    /// <param name="traceEvent">The event.</param>
    private void Deliver(in ExpressionTransformEvent traceEvent)
    {
        if (_stopped) return;

        ExpressionResourceBudget.Session? engineSession = ExpressionResourceBudget.EnterTracerCallback(_callbackSession ??= ExpressionResourceBudget.CreateRefusingSession());
        try
        {
            _tracer.OnEvent(in traceEvent);
        }
        catch (Exception exception)
        {
            _stopped = true;
            _scope.ReportFault(exception);
        }
        finally
        {
            ExpressionResourceBudget.ExitTracerCallback(engineSession);
        }

        if (_stopped)
        {
            _pending = null;
            ExpressionResourceBudget.DetachTrace(this);
        }
    }

    /// <summary>
    /// Classifies the result of a treatment or finalization: the very input, a reconstruction of the prepared node, or a node
    /// produced by the finalization.
    /// </summary>
    /// <param name="input">The node the treatment received.</param>
    /// <param name="prepared">The node rebuilt by the traversal.</param>
    /// <param name="result">The result.</param>
    /// <returns>The outcome.</returns>
    private static ExpressionTransformOutcome Classify(Expression input, Expression prepared, Expression? result)
    {
        if (ReferenceEquals(result, input)) return ExpressionTransformOutcome.Unchanged;
        if (ReferenceEquals(result, prepared) || IsShallowCopy(prepared, result)) return ExpressionTransformOutcome.Rebuilt;
        return ExpressionTransformOutcome.Finalized;
    }

    /// <summary>
    /// Whether <paramref name="copy"/> is a shallow copy of <paramref name="original"/>: same node type, metadata and child
    /// references, for the families whose finalization copies the node (<c>CopyExpression</c>). Opaque nodes are compared by
    /// reference only; any other family is not recognized.
    /// </summary>
    /// <param name="original">The prepared node.</param>
    /// <param name="copy">The finalized node.</param>
    /// <returns><see langword="true"/> for a recognized shallow copy.</returns>
    private static bool IsShallowCopy(Expression original, Expression? copy)
    {
        if (copy is null || ExpressionOpacity.IsOpaque(original) || ExpressionOpacity.IsOpaque(copy)) return false;

        return (original, copy) switch
        {
            (ConstantExpression a, ConstantExpression b) => a.Type == b.Type && ReferenceEquals(a.Value, b.Value),
            (BinaryExpression a, BinaryExpression b) => a.NodeType == b.NodeType && a.Type == b.Type && ReferenceEquals(a.Left, b.Left)
                && ReferenceEquals(a.Right, b.Right) && a.Method == b.Method && ReferenceEquals(a.Conversion, b.Conversion),
            (UnaryExpression a, UnaryExpression b) => a.NodeType == b.NodeType && a.Type == b.Type && ReferenceEquals(a.Operand, b.Operand) && a.Method == b.Method,
            (MethodCallExpression a, MethodCallExpression b) => a.Method == b.Method && ReferenceEquals(a.Object, b.Object) && SameReferences(a.Arguments, b.Arguments),
            (ConditionalExpression a, ConditionalExpression b) => a.Type == b.Type && ReferenceEquals(a.Test, b.Test) && ReferenceEquals(a.IfTrue, b.IfTrue) && ReferenceEquals(a.IfFalse, b.IfFalse),
            (InvocationExpression a, InvocationExpression b) => ReferenceEquals(a.Expression, b.Expression) && SameReferences(a.Arguments, b.Arguments),
            (MemberExpression a, MemberExpression b) => a.Member == b.Member && ReferenceEquals(a.Expression, b.Expression),
            _ => false,
        };
    }

    /// <summary>Whether two child lists hold the same references in the same order.</summary>
    /// <param name="x">The first list.</param>
    /// <param name="y">The second list.</param>
    /// <returns><see langword="true"/> when both lists hold the same references.</returns>
    private static bool SameReferences(IReadOnlyList<Expression> x, IReadOnlyList<Expression> y)
    {
        if (x.Count != y.Count) return false;
        for (int i = 0; i < x.Count; i++)
        {
            if (!ReferenceEquals(x[i], y[i])) return false;
        }

        return true;
    }

    /// <summary>Gets the node type of a framework node, or <see langword="null"/> for an extension node or no node (ETC R4).</summary>
    /// <param name="node">The node.</param>
    /// <returns>The node type, or <see langword="null"/>.</returns>
    private static ExpressionType? NodeTypeOf(Expression? node)
        => node is null || ExpressionOpacity.IsOpaque(node) ? null : node.NodeType;

    /// <summary>Removes the <see cref="TargetInvocationException"/> layers added by reflective rule invocation.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The innermost exception under those layers.</returns>
    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } inner })
        {
            exception = inner;
        }

        return exception;
    }

    /// <summary>Gets the resource refused by a resource failure of the engine, or <see langword="null"/> for any other exception.</summary>
    /// <param name="exception">The unwrapped exception.</param>
    /// <returns>The refused resource, or <see langword="null"/>.</returns>
    private static ExpressionResourceLimitKind? ResourceLimitOf(Exception exception) => exception switch
    {
        ExpressionResourceLimitException resource => resource.Kind,
        InsufficientExecutionStackException => ExpressionResourceLimitKind.Stack,
        _ => null,
    };
}
