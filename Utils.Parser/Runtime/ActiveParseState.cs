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
    Active,
    Completed,
    Failed,
    Pruned
}

/// <summary>
/// Represents an active parser state/branch candidate during alternative exploration.
/// This data container is intentionally immutable and infrastructure-only.
/// It prepares explicit scheduling of parser work without changing current execution semantics.
/// </summary>
internal sealed record ActiveParseState
{
    public required Rule Rule { get; init; }
    public required Alternative Alternative { get; init; }
    public required int OriginInputPosition { get; init; }
    public required int CurrentInputPosition { get; init; }
    public required int AlternativeIndex { get; init; }
    public required RuleContentCursor Cursor { get; init; }
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
