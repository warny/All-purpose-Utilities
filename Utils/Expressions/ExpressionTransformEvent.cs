using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>The kind of an <see cref="ExpressionTransformEvent"/>.</summary>
public enum ExpressionTransformEventKind
{
    /// <summary>
    /// The engine starts transforming a node (one <c>TransformCore</c> call). <see cref="ExpressionTransformEvent.Input"/> is
    /// the node; <see cref="ExpressionTransformEvent.Rule"/> is the rule of the parent treatment that requested this one, or
    /// <see langword="null"/> when the node is a child prepared by its parent's traversal.
    /// </summary>
    TransformEnter,

    /// <summary>
    /// The transformation of a node returned. <see cref="ExpressionTransformEvent.Output"/> is its result and
    /// <see cref="ExpressionTransformEvent.Outcome"/> says what produced it.
    /// </summary>
    TransformExit,

    /// <summary>
    /// A rule returned a result for the prepared node <see cref="ExpressionTransformEvent.Input"/>: emitted only after the rule
    /// returned. <see cref="ExpressionTransformEvent.Output"/> is that result (<see langword="null"/> when a single-node rule
    /// returned <see langword="null"/>, which the engine returns as is).
    /// </summary>
    RuleApplied,

    /// <summary>
    /// No rule applied and <c>FinalizeExpression</c> returned: <see cref="ExpressionTransformEvent.Input"/> is the prepared
    /// node, <see cref="ExpressionTransformEvent.Output"/> the finalized one.
    /// </summary>
    Finalize,

    /// <summary>
    /// A resource limit refused the operation (<see cref="ExpressionTransformEvent.ResourceLimit"/>). Emitted once, for the
    /// innermost treatment the refusal crossed (or without treatment when the input validation refused it), before the
    /// <see cref="TransformFailed"/> events of the treatments it unwinds.
    /// </summary>
    ResourceLimitReached,

    /// <summary>
    /// An exception left the treatment <see cref="ExpressionTransformEvent.OperationId"/>.
    /// <see cref="ExpressionTransformEvent.Rule"/> is the rule that was running when it crossed the treatment, if any.
    /// </summary>
    TransformFailed,

    /// <summary>(Detailed level) A rule matches the node's signature and is about to be considered.</summary>
    RuleCandidate,

    /// <summary>(Detailed level) A candidate rule did not produce a result (<see cref="ExpressionTransformEvent.Rejection"/>).</summary>
    RuleRejected,

    /// <summary>(Detailed level) The traversal enters a lexical scope (<see cref="ExpressionTransformEvent.ScopeKind"/>).</summary>
    ScopeEnter,

    /// <summary>(Detailed level) The traversal leaves a lexical scope (<see cref="ExpressionTransformEvent.ScopeKind"/>).</summary>
    ScopeExit,

    /// <summary>
    /// (Detailed level) An arithmetic chain was extracted for the <see cref="FlattenAttribute"/> rules of the node
    /// (<see cref="ExpressionTransformEvent.Input"/> is the root of the chain); the rules' candidate, applied and rejected
    /// events follow.
    /// </summary>
    ChainFlattened,
}

/// <summary>What produced the result of a treatment (<see cref="ExpressionTransformEventKind.TransformExit"/>) or of a finalization.</summary>
public enum ExpressionTransformOutcome
{
    /// <summary>Not applicable to the event.</summary>
    None,

    /// <summary>The result is the very input node: nothing changed.</summary>
    Unchanged,

    /// <summary>
    /// The node was only rebuilt around transformed children (a structural reconstruction, or a shallow copy of it): no rule
    /// applied to the node itself and its finalization did not change it.
    /// </summary>
    Rebuilt,

    /// <summary>
    /// <c>FinalizeExpression</c> returned another node than the rebuilt one: for <c>ExpressionSimplifier</c>, the
    /// canonicalization of an additive or multiplicative chain.
    /// </summary>
    Finalized,

    /// <summary>A rule produced the result (see <see cref="ExpressionTransformEvent.Rule"/>).</summary>
    RuleApplied,

    /// <summary>The node is a transformation boundary (a quote the simplifier does not rewrite), returned as is.</summary>
    Boundary,
}

/// <summary>Why a candidate rule produced no result (<see cref="ExpressionTransformEventKind.RuleRejected"/>).</summary>
public enum ExpressionRuleRejection
{
    /// <summary>Not applicable to the event.</summary>
    None,

    /// <summary>An operand did not match the type or the signature attribute of the corresponding rule parameter.</summary>
    ParameterMismatch,

    /// <summary>The rule ran and returned <see langword="null"/>: it declined, and the next candidate is tried.</summary>
    Declined,

    /// <summary>
    /// The effect gate of the transformer (ETC R1b) refused every rule of the node, whose operands are not proven inert;
    /// the node goes to its finalization. Under a non-default simplification policy that admits the audited rules outside the
    /// gate (S5 P18 Architecture C), a rule that is not audited is refused with this reason too, without its signature being
    /// matched, and the following candidates are still tried.
    /// </summary>
    EffectGate,

    /// <summary>
    /// The transformer did not admit the chain to its <see cref="FlattenAttribute"/> rules: the chain was not extracted, no
    /// flatten rule ran, and its root is transformed as without flatten rules (the chains below it are offered in turn).
    /// <see cref="Utils.Mathematics.Expressions.ExpressionSimplifier"/> reports it for every additive chain that is not a
    /// linear chain.
    /// </summary>
    ChainNotAdmitted,
}

/// <summary>The lexical scope of a <see cref="ExpressionTransformEventKind.ScopeEnter"/> or <see cref="ExpressionTransformEventKind.ScopeExit"/> event.</summary>
public enum ExpressionTraceScopeKind
{
    /// <summary>Not applicable to the event.</summary>
    None,

    /// <summary>The parameters of a lambda (<see cref="ExpressionTransformEvent.Input"/> is the lambda).</summary>
    Lambda,

    /// <summary>The variables of a block (<see cref="ExpressionTransformEvent.Input"/> is the block).</summary>
    Block,

    /// <summary>The variable of a catch handler (<see cref="ExpressionTransformEvent.Input"/> is the handler body).</summary>
    Catch,
}

/// <summary>The level of detail of a tracing scope.</summary>
public enum ExpressionTraceDetail
{
    /// <summary>
    /// Treatments, applied rules, finalizations, resource refusals and failures
    /// (<see cref="ExpressionTransformEventKind.TransformEnter"/> to <see cref="ExpressionTransformEventKind.TransformFailed"/>).
    /// </summary>
    Standard,

    /// <summary>
    /// Everything in <see cref="Standard"/>, plus every candidate rule, every rejection and every lexical scope: far more
    /// events, for the analysis of rule dispatch.
    /// </summary>
    Detailed,
}

/// <summary>
/// One event of a traced expression transformation (see <see cref="IExpressionTransformTracer"/>). This type only holds data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hierarchy.</b> A traced operation is a session (<see cref="SessionId"/>). Every <c>TransformCore</c> call in it is a
/// treatment with its own <see cref="OperationId"/>; <see cref="ParentOperationId"/> is the treatment it runs in (the parent
/// node's treatment, whether the node is a child being prepared or the result of a rule that transforms again), and
/// <see cref="Depth"/> the number of enclosing treatments. Nested comparisons and nested public calls made during the
/// operation belong to the same session, so the whole recursion can be rebuilt from the events.
/// </para>
/// <para>
/// <b>Expressions.</b> <see cref="Input"/> and <see cref="Output"/> are references to the engine's trees, never copies or
/// renderings; an event keeps them alive as long as it is stored. <see cref="NodeType"/> is only read from framework nodes:
/// it is <see langword="null"/> for an extension node, whose members the engine never reads (ETC R4).
/// </para>
/// </remarks>
public readonly struct ExpressionTransformEvent
{
    /// <summary>Initializes an event.</summary>
    /// <param name="kind">The kind of event.</param>
    /// <param name="sessionId">The traced operation.</param>
    /// <param name="sequence">The position of the event in its session.</param>
    /// <param name="operationId">The current treatment, or 0.</param>
    /// <param name="parentOperationId">The enclosing treatment, or 0.</param>
    /// <param name="depth">The number of treatments enclosing the current one.</param>
    /// <param name="nodeType">The node type of <paramref name="input"/>, when it is a framework node.</param>
    /// <param name="transformerType">The runtime type of the transformer running the treatment.</param>
    /// <param name="rule">The rule concerned by the event, if any.</param>
    /// <param name="input">The node the event is about.</param>
    /// <param name="output">The node produced, if any.</param>
    /// <param name="outcome">What produced <paramref name="output"/>.</param>
    /// <param name="rejection">Why a rule produced no result.</param>
    /// <param name="scopeKind">The lexical scope entered or left.</param>
    /// <param name="exception">The exception that left the treatment.</param>
    /// <param name="resourceLimit">The resource limit that refused the operation.</param>
    internal ExpressionTransformEvent(
        ExpressionTransformEventKind kind,
        long sessionId,
        long sequence,
        long operationId,
        long parentOperationId,
        int depth,
        ExpressionType? nodeType,
        Type? transformerType,
        ExpressionTransformRuleInfo? rule,
        Expression? input,
        Expression? output,
        ExpressionTransformOutcome outcome,
        ExpressionRuleRejection rejection,
        ExpressionTraceScopeKind scopeKind,
        Exception? exception,
        ExpressionResourceLimitKind? resourceLimit)
    {
        Kind = kind;
        SessionId = sessionId;
        Sequence = sequence;
        OperationId = operationId;
        ParentOperationId = parentOperationId;
        Depth = depth;
        NodeType = nodeType;
        TransformerType = transformerType;
        Rule = rule;
        Input = input;
        Output = output;
        Outcome = outcome;
        Rejection = rejection;
        ScopeKind = scopeKind;
        Exception = exception;
        ResourceLimit = resourceLimit;
    }

    /// <summary>Gets the kind of event.</summary>
    public ExpressionTransformEventKind Kind { get; }

    /// <summary>Gets the identifier of the traced operation (unique in the process).</summary>
    public long SessionId { get; }

    /// <summary>Gets the position of the event in its session, starting at 1, without gaps.</summary>
    public long Sequence { get; }

    /// <summary>Gets the treatment the event belongs to, or 0 when it belongs to none (a refused input).</summary>
    public long OperationId { get; }

    /// <summary>Gets the treatment enclosing <see cref="OperationId"/>, or 0 for a root treatment.</summary>
    public long ParentOperationId { get; }

    /// <summary>Gets the number of treatments enclosing <see cref="OperationId"/> (0 for a root treatment).</summary>
    public int Depth { get; }

    /// <summary>Gets the node type of <see cref="Input"/>, or <see langword="null"/> when it is an extension node or there is no input.</summary>
    public ExpressionType? NodeType { get; }

    /// <summary>Gets the runtime type of the transformer running the treatment.</summary>
    public Type? TransformerType { get; }

    /// <summary>Gets the rule concerned by the event (see <see cref="ExpressionTransformEventKind"/> for its meaning per kind).</summary>
    public ExpressionTransformRuleInfo? Rule { get; }

    /// <summary>Gets the node the event is about (see <see cref="ExpressionTransformEventKind"/>).</summary>
    public Expression? Input { get; }

    /// <summary>Gets the node produced by a rule, a finalization or a treatment.</summary>
    public Expression? Output { get; }

    /// <summary>Gets what produced <see cref="Output"/>, for <see cref="ExpressionTransformEventKind.TransformExit"/> and <see cref="ExpressionTransformEventKind.Finalize"/>.</summary>
    public ExpressionTransformOutcome Outcome { get; }

    /// <summary>Gets why a rule produced no result, for <see cref="ExpressionTransformEventKind.RuleRejected"/>.</summary>
    public ExpressionRuleRejection Rejection { get; }

    /// <summary>Gets the lexical scope entered or left, for <see cref="ExpressionTransformEventKind.ScopeEnter"/> and <see cref="ExpressionTransformEventKind.ScopeExit"/>.</summary>
    public ExpressionTraceScopeKind ScopeKind { get; }

    /// <summary>Gets the exception that left the treatment, for <see cref="ExpressionTransformEventKind.TransformFailed"/> and <see cref="ExpressionTransformEventKind.ResourceLimitReached"/>.</summary>
    public Exception? Exception { get; }

    /// <summary>Gets the resource limit that refused the operation, for <see cref="ExpressionTransformEventKind.ResourceLimitReached"/>.</summary>
    public ExpressionResourceLimitKind? ResourceLimit { get; }

    /// <summary>Returns a short description of the event that never renders its expressions.</summary>
    /// <returns>The kind, sequence, treatment and rule of the event.</returns>
    public override string ToString()
        => $"#{Sequence} {Kind} op {OperationId} (parent {ParentOperationId}, depth {Depth}){(Rule is null ? string.Empty : " " + Rule.DisplayName)}";
}
