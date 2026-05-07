using Utils.Parser.Diagnostics;
using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Runs deterministic sequential scheduling for parser alternatives represented as <see cref="ActiveParseState"/>.
/// </summary>
internal sealed class AlternativeScheduler
{
    private readonly ParserLookaheadSharedPrefixDetector _sharedPrefixDetector = new();
    private readonly ParserContinuationFactory _continuationFactory = new();
    private readonly ParserSharedPrefixPlanFactory _sharedPrefixPlanFactory = new();
    /// <summary>
    /// Drives alternative orchestration for a single alternation parse attempt.
    /// </summary>
    public AlternativeSchedulingResult Run(
        Rule rule,
        IEnumerable<Alternative> alternatives,
        int originInputPosition,
        int minimumPrecedence,
        DiagnosticBag? diagnostics,
        Func<Alternative, int, ScheduledAlternativeExecutionResult> parseAlternative)
    {
        var ordered = alternatives.OrderBy(static a => a.Priority).ToList();
        var lookaheadProbesByAlternative = new ParserLookaheadProbeResult[ordered.Count];
        var completedStates = new List<ActiveParseState>();
        var failedStates = new List<ActiveParseState>();

        for (int index = 0; index < ordered.Count; index++)
        {
            var alternative = ordered[index];
            var initial = CreateInitialState(rule, alternative, originInputPosition, index);
            var scheduled = parseAlternative(alternative, index);
            var parsed = scheduled.State;
            lookaheadProbesByAlternative[index] = scheduled.Probe;
            if (parsed is null)
            {
                failedStates.Add(initial.Fail());
                continue;
            }

            completedStates.Add(EnsureInitialized(parsed, initial));
        }

        if (completedStates.Count == 0)
        {
            return new AlternativeSchedulingResult(null, [], failedStates, [], BuildMetadata(rule, ordered, lookaheadProbesByAlternative));
        }

        var deduplicated = DeduplicateStates(completedStates, minimumPrecedence);
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

        return new AlternativeSchedulingResult(winner, pruned, failedStates, prunedStates, BuildMetadata(rule, ordered, lookaheadProbesByAlternative));
    }



    /// <summary>
    /// Builds informational scheduling metadata from shallow look-ahead observations.
    /// </summary>
    private AlternativeSchedulingMetadata BuildMetadata(
        Rule rule,
        IReadOnlyList<Alternative> orderedAlternatives,
        IReadOnlyList<ParserLookaheadProbeResult> lookaheadProbes)
    {
        // Metadata generation intentionally includes shallow observations from attempted
        // alternatives even when parsing fails. Shared-prefix planning metadata is
        // structural and observational only.
        var candidates = _sharedPrefixDetector.Detect(lookaheadProbes);
        var candidateIndexes = candidates
            .SelectMany(static candidate => candidate.AlternativeIndexes)
            .ToHashSet();

        var continuations = new List<ParserContinuationDescriptor>(orderedAlternatives.Count);
        for (var index = 0; index < orderedAlternatives.Count; index++)
        {
            var expectedTokenNames = lookaheadProbes[index].ExpectedTokenNames;
            continuations.Add(_continuationFactory.Create(
                rule,
                orderedAlternatives[index],
                index,
                sequencePosition: 0,
                expectedTokenNames,
                candidateIndexes.Contains(index)));
        }

        var plans = _sharedPrefixPlanFactory.CreatePlans(candidates, continuations);
        return new AlternativeSchedulingMetadata { SharedPrefixPlans = plans };
    }
    private static ActiveParseState CreateInitialState(Rule rule, Alternative alternative, int originInputPosition, int alternativeIndex)
    {
        return new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = originInputPosition,
            CurrentInputPosition = originInputPosition,
            AlternativeIndex = alternativeIndex,
            Cursor = new RuleContentCursor { Index = 0, Kind = "alternative-root" },
            PartialNode = new ErrorNode(new SourceSpan(originInputPosition, 0), "DEFAULT_MODE", "Alternative not evaluated", rule),
            EndPosition = null,
            Status = ActiveParseStateStatus.Active,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
    }

    private static ActiveParseState EnsureInitialized(ActiveParseState state, ActiveParseState fallback)
    {
        return state with
        {
            Rule = state.Rule,
            Alternative = state.Alternative,
            OriginInputPosition = state.OriginInputPosition,
            AlternativeIndex = state.AlternativeIndex,
            Cursor = state.Cursor,
            PartialNode = state.PartialNode,
            Status = state.Status == ActiveParseStateStatus.Active ? ActiveParseStateStatus.Completed : state.Status,
            EndPosition = state.EndPosition ?? state.CurrentInputPosition,
            ParentStateKey = state.ParentStateKey,
            Depth = state.Depth,
            Continuation = state.Continuation
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

        return byIdentity.Values.OrderBy(static s => s.Alternative.Priority).ThenBy(static s => s.AlternativeIndex).ThenBy(static s => s.CurrentInputPosition).ToList();
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

        return groups.Values.SelectMany(static g => g).OrderBy(static s => s.Alternative.Priority).ThenBy(static s => s.AlternativeIndex).ThenByDescending(static s => s.CurrentInputPosition).ToList();
    }
}

internal sealed class AlternativeSchedulingResult
{
    public AlternativeSchedulingResult(ActiveParseState? selectedState, IReadOnlyList<ActiveParseState> completedStates, IReadOnlyList<ActiveParseState> failedStates, IReadOnlyList<ActiveParseState> prunedStates, AlternativeSchedulingMetadata metadata)
    {
        SelectedState = selectedState;
        CompletedStates = completedStates;
        FailedStates = failedStates;
        PrunedStates = prunedStates;
        Metadata = metadata;
    }

    public ActiveParseState? SelectedState { get; }
    public IReadOnlyList<ActiveParseState> CompletedStates { get; }
    public IReadOnlyList<ActiveParseState> FailedStates { get; }
    public IReadOnlyList<ActiveParseState> PrunedStates { get; }
    public AlternativeSchedulingMetadata Metadata { get; }
}
