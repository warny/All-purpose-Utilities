using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Scheduling identity only.
/// This key is intentionally richer than pruning identity because scheduler deduplication
/// must preserve local execution-shape differences (for example continuation metadata and precedence context).
/// </summary>
/// <param name="RuleName">Rule name associated with the local scheduled state.</param>
/// <param name="OriginInputPosition">Input position where the rule invocation started.</param>
/// <param name="CurrentInputPosition">Current input position reached by this local state.</param>
/// <param name="AlternativeIndex">Alternative index in scheduler traversal order.</param>
/// <param name="AlternativePriority">Alternative priority value used by deterministic ordering.</param>
/// <param name="CursorIndex">Cursor index in the alternative content.</param>
/// <param name="CursorKind">Cursor kind used to distinguish local traversal shape.</param>
/// <param name="MinimumPrecedence">Minimum precedence context for this scheduled exploration.</param>
/// <param name="Continuation">Optional continuation metadata key (transport metadata only).</param>
internal readonly record struct ActiveParseStateKey(
    string RuleName,
    int OriginInputPosition,
    int CurrentInputPosition,
    int AlternativeIndex,
    int AlternativePriority,
    int CursorIndex,
    string CursorKind,
    int MinimumPrecedence,
    ContinuationKey? Continuation);

/// <summary>
/// Pruning/orchestration grouping identity only.
/// This key intentionally excludes scheduler-local dimensions (for example alternative priority
/// and continuation metadata) and must not be interpreted as semantic equivalence evidence.
/// </summary>
/// <param name="RuleName">Rule name associated with the local branch state.</param>
/// <param name="OriginInputPosition">Input position where the branch invocation started.</param>
/// <param name="CurrentOrEndPosition">Current/end position used by structural branch grouping.</param>
/// <param name="CursorKind">Cursor kind used by branch-equivalence grouping.</param>
/// <param name="CursorIndex">Cursor index used by branch-equivalence grouping.</param>
internal readonly record struct ActiveParseBranchEquivalenceKey(
    string RuleName,
    int OriginInputPosition,
    int CurrentOrEndPosition,
    string CursorKind,
    int CursorIndex);

internal enum ActiveParseStateStatus
{
    /// <summary>
    /// Local exploratory state. This status is runtime-local, non-authoritative,
    /// and may only transition through immutable state cloning.
    /// </summary>
    Active,
    /// <summary>
    /// Local completion for one branch attempt. This does not imply global parse acceptance.
    /// Global acceptance remains owned by <see cref="ParserEngine"/>.
    /// </summary>
    Completed,
    /// <summary>
    /// Local branch failure. This does not imply global parse failure and may still
    /// contribute to diagnostic context chosen by <see cref="ParserEngine"/>.
    /// </summary>
    Failed,
    /// <summary>
    /// Orchestration-only pruning marker used by scheduling infrastructure.
    /// This status does not imply syntax invalidity and must not alter parse outcome,
    /// parse-tree shape, or syntax-error diagnostics. Explicit pruning/ambiguity diagnostics
    /// may still be emitted where supported.
    /// </summary>
    Pruned
}

/// <summary>
/// Represents an active parser state/branch candidate during alternative exploration.
/// The outcome model remains local: this type can describe branch success/failure/pruning,
/// but it cannot determine global parse acceptance or final diagnostics.
/// This data container is intentionally immutable and infrastructure-only.
/// It prepares explicit scheduling of parser work without changing current execution semantics.
/// The model is descriptive scheduling/runtime-local state only: it is non-replayable,
/// non-resumable, and only carries opaque state snapshots for parser attempt-boundary commit.
/// Shared-prefix-related fields in this state are observational metadata boundaries only;
/// they do not grant branch-selection authority, parse acceptance authority, or semantic equivalence guarantees.
/// <para>
/// <see cref="Continuation"/> is metadata that describes a potential continuation anchor.
/// It is not executable runtime replay state and does not imply resume/rollback semantics.
/// </para>
/// <para>
/// <see cref="ParentStateKey"/> models lineage for scheduling metadata and is not an execution stack.
/// <see cref="Depth"/> is structural lineage depth, not semantic invocation-frame depth.
/// </para>
/// </summary>
internal sealed record ActiveParseState
{
    /// <summary>
    /// Parser rule associated with this local scheduling state.
    /// </summary>
    public required Rule Rule { get; init; }
    /// <summary>
    /// Alternative associated with this local scheduling state.
    /// </summary>
    public required Alternative Alternative { get; init; }
    /// <summary>
    /// Input position where this rule invocation started.
    /// </summary>
    public required int OriginInputPosition { get; init; }
    /// <summary>
    /// Current input position reached by this local state.
    /// </summary>
    public required int CurrentInputPosition { get; init; }
    /// <summary>
    /// Stable scheduler-local index of the explored alternative.
    /// </summary>
    public required int AlternativeIndex { get; init; }
    /// <summary>
    /// Local traversal cursor describing where exploration currently is in the alternative content.
    /// </summary>
    public required RuleContentCursor Cursor { get; init; }
    /// <summary>
    /// Local partial parse node for branch-level scheduling.
    /// It is descriptive only and does not grant parse-tree authority.
    /// </summary>
    public required ParseNode PartialNode { get; init; }
    /// <summary>
    /// Optional local end position for completed/failed states.
    /// When null, the state is still represented by its current input position.
    /// </summary>
    public int? EndPosition { get; init; }
    /// <summary>
    /// Local branch status used by scheduling orchestration.
    /// This status is descriptive and not parse-authoritative.
    /// </summary>
    public ActiveParseStateStatus Status { get; init; }
    /// <summary>
    /// Optional lineage key for parent scheduling state metadata.
    /// This is not an executable runtime stack frame reference.
    /// </summary>
    public ActiveParseStateKey? ParentStateKey { get; init; }
    /// <summary>
    /// Structural lineage depth for scheduling metadata.
    /// This value is descriptive and does not model semantic invocation depth.
    /// </summary>
    public int Depth { get; init; }
    /// <summary>
    /// Optional continuation transport metadata.
    /// This value is observable but non-authoritative and remains discardable
    /// without changing parse-authoritative correctness.
    /// </summary>
    public ContinuationKey? Continuation { get; init; }
    /// <summary>
    /// Opaque semantic state snapshot captured after a successful parser attempt-boundary.
    /// The parser engine restores this snapshot only when the scheduler selects this completed state.
    /// This value is not replay authority and is not used for lifecycle hooks or action buffering.
    /// </summary>
    public object? ExecutionStateSnapshot { get; init; }

    public ActiveParseStateKey ToStateKey(int minimumPrecedence) => new(
        Rule.Name,
        OriginInputPosition,
        CurrentInputPosition,
        AlternativeIndex,
        Alternative.Priority,
        Cursor.Index,
        Cursor.Kind,
        minimumPrecedence,
        Continuation);

    public ActiveParseBranchEquivalenceKey ToBranchEquivalenceKey() => new(
        Rule.Name,
        OriginInputPosition,
        EndPosition ?? CurrentInputPosition,
        Cursor.Kind,
        Cursor.Index);

    /// <summary>
    /// Produces a new local completion state for scheduling transport.
    /// This transformation is non-authoritative and does not finalize global parse outcomes.
    /// </summary>
    public ActiveParseState Complete(int endPosition) => this with { Status = ActiveParseStateStatus.Completed, CurrentInputPosition = endPosition, EndPosition = endPosition };
    /// <summary>
    /// Produces a new local failure state for branch-level orchestration.
    /// This does not assert global parse failure.
    /// </summary>
    public ActiveParseState Fail() => this with { Status = ActiveParseStateStatus.Failed, EndPosition = null };
    /// <summary>
    /// Produces an orchestration-only pruned marker.
    /// Pruning metadata is not a syntax-invalidity verdict.
    /// </summary>
    public ActiveParseState Prune() => this with { Status = ActiveParseStateStatus.Pruned };
    /// <summary>
    /// Attaches continuation metadata to this state.
    /// The attached key is descriptive and never executable replay authority.
    /// </summary>
    public ActiveParseState WithContinuation(ContinuationKey continuation) => this with { Continuation = continuation };
    /// <summary>
    /// Advances local descriptive traversal state.
    /// This update is orchestration metadata only and does not imply execution replay capability.
    /// </summary>
    public ActiveParseState Advance(int currentInputPosition, RuleContentCursor cursor) => this with { CurrentInputPosition = currentInputPosition, Cursor = cursor };
    /// <summary>
    /// Attaches lineage metadata for deterministic runtime observability.
    /// Lineage metadata is non-executable and does not represent resumable runtime frames.
    /// </summary>
    public ActiveParseState WithLineage(ActiveParseStateKey? parentStateKey, int depth) => this with { ParentStateKey = parentStateKey, Depth = depth };
    /// <summary>
    /// Projects this descriptive state into parser visited-state identity.
    /// </summary>
    public ParserStateKey ToParserStateKey(int minimumPrecedence) => new(Rule.Name, CurrentInputPosition, AlternativeIndex, Cursor.Index, minimumPrecedence);
    /// <summary>
    /// Projects this state into invocation-reuse identity.
    /// Invocation reuse identity is not execution-replay identity.
    /// </summary>
    public RuleInvocationKey ToRuleInvocationKey(int minimumPrecedence) => new(Rule.Name, OriginInputPosition, minimumPrecedence, ParserExecutionStateKey.Stateless);
    /// <summary>
    /// Builds continuation transport metadata from the current descriptive state.
    /// Returned key is metadata-only and not executable continuation state.
    /// </summary>
    public ContinuationKey ToContinuationKey(int resumePosition, int minimumPrecedence) => new(Rule.Name, AlternativeIndex, Cursor.Index, resumePosition, minimumPrecedence);

    /// <summary>
    /// Rehydrates descriptive scheduling state from a branch snapshot.
    /// This conversion does not reconstruct execution history.
    /// </summary>
    public static ActiveParseState FromBranch(ParseBranch branch) => new()
    {
        Rule = branch.Rule,
        Alternative = branch.Alternative,
        OriginInputPosition = branch.InputPosition,
        CurrentInputPosition = branch.EndPosition,
        AlternativeIndex = -1,
        Cursor = branch.Cursor,
        PartialNode = branch.PartialNode,
        EndPosition = branch.EndPosition,
        Status = branch.IsComplete ? ActiveParseStateStatus.Completed : ActiveParseStateStatus.Failed,
        ParentStateKey = null,
        Depth = 0,
        Continuation = null,
        ExecutionStateSnapshot = null
    };

    public ParseBranch ToBranch() => new()
    {
        Rule = Rule,
        Alternative = Alternative,
        InputPosition = OriginInputPosition,
        Cursor = Cursor,
        PartialNode = PartialNode,
        EndPosition = EndPosition ?? CurrentInputPosition,
        IsComplete = Status == ActiveParseStateStatus.Completed
    };
}
