using Utils.Parser.Diagnostics;
using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Runs deterministic sequential scheduling for parser alternatives represented as <see cref="ActiveParseState"/>.
/// </summary>
internal sealed class AlternativeScheduler
{
    /// <summary>
    /// Drives alternative orchestration for a single alternation parse attempt.
    /// </summary>
    public AlternativeSchedulingResult Run(
        ParseContext context,
        IEnumerable<Alternative> alternatives,
        Rule rule,
        int minimumPrecedence,
        DiagnosticBag? diagnostics,
        Func<Alternative, int, bool> checkPrecedence,
        Func<Alternative, int, ParseNode?> tryParseAlternative,
        Action<ParserStateKey> registerVisitedState,
        Action<Alternative, int, ParserStateKey> onRepeatedState,
        Action onBacktracking)
    {
        var alternativeList = alternatives.OrderBy(static a => a.Priority).ToList();
        var startPosition = context.Position;
        var activeStates = new List<ActiveParseState>();
        var failedStates = new List<ActiveParseState>();
        var visitedStates = new HashSet<ParserStateKey>();

        for (int index = 0; index < alternativeList.Count; index++)
        {
            var alternative = alternativeList[index];
            var stateKey = new ParserStateKey(rule.Name, startPosition, index, index, minimumPrecedence);
            registerVisitedState(stateKey);

            if (!visitedStates.Add(stateKey))
            {
                onRepeatedState(alternative, index, stateKey);
                continue;
            }

            if (!checkPrecedence(alternative, minimumPrecedence))
            {
                continue;
            }

            var savedPosition = context.SavePosition();
            var parsed = tryParseAlternative(alternative, index);
            if (parsed is null)
            {
                failedStates.Add(CreateState(rule, alternative, startPosition, context.Position, index, null, ActiveParseStateStatus.Failed));
                onBacktracking();
                context.RestorePosition(savedPosition);
                continue;
            }

            activeStates.Add(CreateState(rule, alternative, startPosition, context.Position, index, parsed, ActiveParseStateStatus.Completed));
            context.RestorePosition(savedPosition);
        }

        if (activeStates.Count == 0)
        {
            return new AlternativeSchedulingResult(null, [], failedStates, []);
        }

        var deduplicated = DeduplicateStates(activeStates, minimumPrecedence);
        var pruned = PruneEquivalentActiveStates(deduplicated, diagnostics);
        var prunedSet = new HashSet<ActiveParseState>(pruned);
        var prunedStates = deduplicated.Where(s => !prunedSet.Contains(s)).Select(static s => s.Prune()).ToList();

        ActiveParseState? winner = null;
        foreach (var state in pruned)
        {
            if (winner is null || IsBetterState(state, winner))
            {
                winner = state;
            }
        }

        return new AlternativeSchedulingResult(winner, pruned, failedStates, prunedStates);
    }

    /// <summary>
    /// Creates an active parse state for alternative scheduling.
    /// </summary>
    private static ActiveParseState CreateState(
        Rule rule,
        Alternative alternative,
        int originInputPosition,
        int currentInputPosition,
        int alternativeIndex,
        ParseNode? parsedNode,
        ActiveParseStateStatus status)
    {
        return new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = originInputPosition,
            CurrentInputPosition = currentInputPosition,
            AlternativeIndex = alternativeIndex,
            Cursor = new RuleContentCursor { Index = 0, Kind = "alternative-root" },
            PartialNode = parsedNode ?? new ErrorNode(new SourceSpan(currentInputPosition, 0), "DEFAULT_MODE", "Alternative failed", rule),
            EndPosition = status == ActiveParseStateStatus.Completed ? currentInputPosition : null,
            Status = status,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
    }

    private static List<ActiveParseState> DeduplicateStates(IReadOnlyList<ActiveParseState> states, int minimumPrecedence)
    {
        var byIdentity = new Dictionary<ActiveParseStateKey, ActiveParseState>();
        foreach (var state in states)
        {
            var key = state.ToStateKey(minimumPrecedence);
            if (!byIdentity.TryGetValue(key, out var existing) || IsBetterState(state, existing))
            {
                byIdentity[key] = state;
            }
        }

        return byIdentity.Values
            .OrderBy(static s => s.Alternative.Priority)
            .ThenBy(static s => s.AlternativeIndex)
            .ThenBy(static s => s.CurrentInputPosition)
            .ToList();
    }

    private static bool IsBetterState(ActiveParseState candidate, ActiveParseState current)
    {
        if (candidate.CurrentInputPosition != current.CurrentInputPosition)
        {
            return candidate.CurrentInputPosition > current.CurrentInputPosition;
        }

        if (candidate.Alternative.Priority != current.Alternative.Priority)
        {
            return candidate.Alternative.Priority < current.Alternative.Priority;
        }

        return candidate.AlternativeIndex < current.AlternativeIndex;
    }

    private static List<ActiveParseState> PruneEquivalentActiveStates(IReadOnlyList<ActiveParseState> states, DiagnosticBag? diagnostics)
    {
        var groups = new Dictionary<ActiveParseBranchEquivalenceKey, List<ActiveParseState>>();
        foreach (var state in states)
        {
            var key = state.ToBranchEquivalenceKey();
            if (!groups.TryGetValue(key, out var group))
            {
                groups[key] = [state];
                continue;
            }

            bool merged = false;
            for (int i = 0; i < group.Count; i++)
            {
                if (ParserEngine.HasDistinctSemantics(group[i].Alternative, state.Alternative))
                {
                    continue;
                }

                if (IsBetterState(state, group[i]))
                {
                    group[i] = state;
                }

                diagnostics?.AddWithContext(ParserDiagnostics.AmbiguousAlternativesPruned, state.PartialNode.Span.Position, state.PartialNode.Span.Length, state.Rule.Name, null, state.Rule.Name);
                merged = true;
                break;
            }

            if (!merged)
            {
                group.Add(state);
            }
        }

        return groups.Values.SelectMany(static g => g)
            .OrderBy(static s => s.Alternative.Priority)
            .ThenBy(static s => s.AlternativeIndex)
            .ThenByDescending(static s => s.CurrentInputPosition)
            .ToList();
    }
}

internal sealed class AlternativeSchedulingResult
{
    public AlternativeSchedulingResult(ActiveParseState? selectedState, IReadOnlyList<ActiveParseState> completedStates, IReadOnlyList<ActiveParseState> failedStates, IReadOnlyList<ActiveParseState> prunedStates)
    {
        SelectedState = selectedState;
        CompletedStates = completedStates;
        FailedStates = failedStates;
        PrunedStates = prunedStates;
    }

    public ActiveParseState? SelectedState { get; }

    public IReadOnlyList<ActiveParseState> CompletedStates { get; }

    public IReadOnlyList<ActiveParseState> FailedStates { get; }

    public IReadOnlyList<ActiveParseState> PrunedStates { get; }
}
