using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

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
/// non-resumable, and not rollback-aware.
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
    public required Rule Rule { get; init; }
    public required Alternative Alternative { get; init; }
    public required int OriginInputPosition { get; init; }
    public required int CurrentInputPosition { get; init; }
    public required int AlternativeIndex { get; init; }
    public required RuleContentCursor Cursor { get; init; }
    /// <summary>
    /// Local partial parse node for branch-level scheduling.
    /// It is descriptive only and does not grant parse-tree authority.
    /// </summary>
    public required ParseNode PartialNode { get; init; }
    public int? EndPosition { get; init; }
    public ActiveParseStateStatus Status { get; init; }
    public ActiveParseStateKey? ParentStateKey { get; init; }
    public int Depth { get; init; }
    public ContinuationKey? Continuation { get; init; }

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

    public ActiveParseState Complete(int endPosition) => this with { Status = ActiveParseStateStatus.Completed, CurrentInputPosition = endPosition, EndPosition = endPosition };
    public ActiveParseState Fail() => this with { Status = ActiveParseStateStatus.Failed, EndPosition = null };
    public ActiveParseState Prune() => this with { Status = ActiveParseStateStatus.Pruned };
    public ActiveParseState WithContinuation(ContinuationKey continuation) => this with { Continuation = continuation };
    public ActiveParseState Advance(int currentInputPosition, RuleContentCursor cursor) => this with { CurrentInputPosition = currentInputPosition, Cursor = cursor };
    public ActiveParseState WithLineage(ActiveParseStateKey? parentStateKey, int depth) => this with { ParentStateKey = parentStateKey, Depth = depth };
    public ParserStateKey ToParserStateKey(int minimumPrecedence) => new(Rule.Name, CurrentInputPosition, AlternativeIndex, Cursor.Index, minimumPrecedence);
    public RuleInvocationKey ToRuleInvocationKey(int minimumPrecedence) => new(Rule.Name, OriginInputPosition, minimumPrecedence);
    public ContinuationKey ToContinuationKey(int resumePosition, int minimumPrecedence) => new(Rule.Name, AlternativeIndex, Cursor.Index, resumePosition, minimumPrecedence);

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
        Continuation = null
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
