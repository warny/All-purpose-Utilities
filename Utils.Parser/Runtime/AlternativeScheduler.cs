using Utils.Parser.Diagnostics;
using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Runs deterministic sequential scheduling for parser alternatives represented as <see cref="ActiveParseState"/>.
/// This component is orchestration-only: it does not own semantic evaluation, diagnostics authority,
/// parser-graph execution, replay, or speculative execution.
/// It can mark states as pruned for local orchestration purposes only; pruning is not a syntax verdict.
/// Parse acceptance remains decided by <see cref="ParserEngine"/>.
/// Look-ahead observations transported by this scheduler remain advisory metadata and do not authorize branch acceptance.
/// Shared-prefix plans produced here remain descriptive lifecycle artifacts:
/// they can be produced, transported, observed, reused for analysis, or discarded without changing parse authority.
/// </summary>
internal sealed class AlternativeScheduler
{
    private readonly IParserRuntimeObserver? _runtimeObserver;
    private readonly ParserLookaheadSharedPrefixDetector _sharedPrefixDetector = new();
    private readonly ParserContinuationFactory _continuationFactory = new();
    private readonly ParserSharedPrefixPlanFactory _sharedPrefixPlanFactory = new();

    /// <summary>
    /// Initializes a scheduler with an optional passive runtime observer.
    /// </summary>
    /// <param name="runtimeObserver">Descriptive observer that cannot influence scheduler decisions.</param>
    public AlternativeScheduler(IParserRuntimeObserver? runtimeObserver = null)
    {
        _runtimeObserver = runtimeObserver;
    }
    /// <summary>
    /// Drives alternative orchestration for a single alternation parse attempt.
    /// Local outcomes here are descriptive scheduling artifacts:
    /// completed/failed/pruned states do not decide global parse success or failure.
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
            NotifyObserver(observation => _runtimeObserver?.OnAlternativeStarted(observation), CreateObservation(ParserRuntimeObservationKind.AlternativeStarted, initial));
            var scheduled = parseAlternative(alternative, index);
            var parsed = scheduled.State;
            lookaheadProbesByAlternative[index] = scheduled.Probe;
            if (parsed is null)
            {
                var failedState = initial.Fail();
                failedStates.Add(failedState);
                NotifyObserver(observation => _runtimeObserver?.OnAlternativeFailed(observation), CreateObservation(ParserRuntimeObservationKind.AlternativeFailed, failedState));
                continue;
            }

            var completedState = EnsureInitialized(parsed);
            completedStates.Add(completedState);
            NotifyObserver(observation => _runtimeObserver?.OnAlternativeCompleted(observation), CreateObservation(ParserRuntimeObservationKind.AlternativeCompleted, completedState));
        }

        if (completedStates.Count == 0)
        {
            return new AlternativeSchedulingResult(null, [], failedStates, [], BuildMetadata(rule, ordered, lookaheadProbesByAlternative));
        }

        // Deduplication uses scheduling identity (ActiveParseStateKey) and is intentionally
        // separate from pruning equivalence (ActiveParseBranchEquivalenceKey).
        // This is not the final parse selection contract; it only reduces equivalent local candidates.
        var deduplicated = DeduplicateStates(completedStates, minimumPrecedence);
        // Pruning is an orchestration optimization only; it is not a syntax-validity verdict.
        var pruned = PruneEquivalentActiveStates(deduplicated, diagnostics);
        var prunedSet = new HashSet<ActiveParseState>(pruned);
        var prunedStates = deduplicated.Where(s => !prunedSet.Contains(s)).Select(static s => s.Prune()).ToList();
        foreach (var prunedState in prunedStates)
        {
            NotifyObserver(observation => _runtimeObserver?.OnAlternativePruned(observation), CreateObservation(ParserRuntimeObservationKind.AlternativePruned, prunedState));
        }

        // The selected state is the scheduler's best local candidate after orchestration filters.
        // ParserEngine still owns final parse acceptance (including trailing-token validation).
        ActiveParseState? winner = null;
        foreach (var state in pruned)
        {
            if (winner is null || IsBetterState(state, winner))
            {
                winner = state;
            }
        }

        if (winner is not null)
        {
            NotifyObserver(observation => _runtimeObserver?.OnAlternativeSelected(observation), CreateObservation(ParserRuntimeObservationKind.AlternativeSelected, winner));
        }

        return new AlternativeSchedulingResult(winner, pruned, failedStates, prunedStates, BuildMetadata(rule, ordered, lookaheadProbesByAlternative));
    }



    /// <summary>
    /// Builds informational scheduling metadata from shallow look-ahead observations.
    /// Produced metadata remains non-authoritative and does not change branch execution requirements.
    /// </summary>
    private AlternativeSchedulingMetadata BuildMetadata(
        Rule rule,
        IReadOnlyList<Alternative> orderedAlternatives,
        IReadOnlyList<ParserLookaheadProbeResult> lookaheadProbes)
    {
        // Metadata lifecycle boundary:
        // observations are produced from attempted alternatives, transported through scheduling,
        // and exposed for diagnostics/audit tooling. This metadata remains structural-only,
        // independent from parse acceptance, and discardable without semantic changes.
        var candidates = _sharedPrefixDetector.Detect(lookaheadProbes);
        var candidateIndexes = candidates
            .SelectMany(static candidate => candidate.AlternativeIndexes)
            .ToHashSet();

        var continuations = new List<ParserContinuationDescriptor>(orderedAlternatives.Count);
        for (var index = 0; index < orderedAlternatives.Count; index++)
        {
            var expectedTokenNames = lookaheadProbes[index].ExpectedTokenNames;
            var sharedTokenName = ResolveSharedTokenName(candidates, index);
            var sharedPrefixSequencePosition = sharedTokenName is null
                ? 0
                : _continuationFactory.ComputeSharedPrefixSequencePosition(orderedAlternatives[index], sharedTokenName);
            continuations.Add(_continuationFactory.Create(
                rule,
                orderedAlternatives[index],
                index,
                sequencePosition: sharedPrefixSequencePosition,
                expectedTokenNames,
                candidateIndexes.Contains(index)));
        }

        // Shared-prefix plans remain observational scheduler metadata:
        // they expose deterministic grouping information, but never grant
        // replay/resume/merge authority and never replace real parser execution.
        var plans = _sharedPrefixPlanFactory.CreatePlans(candidates, continuations);
        return new AlternativeSchedulingMetadata { SharedPrefixPlans = plans };
    }

    /// <summary>
    /// Resolves the first shared token candidate for an alternative while preserving detector ordering.
    /// </summary>
    private static string? ResolveSharedTokenName(
        IReadOnlyList<ParserLookaheadSharedPrefixCandidate> candidates,
        int alternativeIndex)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index].AlternativeIndexes.Contains(alternativeIndex))
            {
                return candidates[index].TokenName;
            }
        }

        return null;
    }
    /// <summary>
    /// Creates a passive immutable observation payload from a parse state.
    /// </summary>
    /// <param name="state">State to project to observation data.</param>
    /// <returns>Immutable observation payload.</returns>
    private static AlternativeRuntimeObservation CreateObservation(ParserRuntimeObservationKind kind, ActiveParseState state)
    {
        return new AlternativeRuntimeObservation(
            kind,
            state.Rule.Name,
            state.AlternativeIndex,
            state.Alternative.Priority,
            state.OriginInputPosition,
            state.CurrentInputPosition,
            ParseObservationStatus(state.Status));
    }

    private static ParserRuntimeObservationStatus ParseObservationStatus(ActiveParseStateStatus status)
    {
        return Enum.TryParse<ParserRuntimeObservationStatus>(status.ToString(), ignoreCase: true, out var normalized)
            ? normalized
            : ParserRuntimeObservationStatus.Unknown;
    }


    /// <summary>
    /// Notifies a runtime observer callback while isolating observer exceptions from parser scheduling flow.
    /// </summary>
    /// <param name="callback">Observer callback to invoke.</param>
    /// <param name="observation">Immutable observation payload passed to the callback.</param>
    private static void NotifyObserver(Action<AlternativeRuntimeObservation>? callback, AlternativeRuntimeObservation observation)
    {
        if (callback is null)
        {
            return;
        }

        try
        {
            callback(observation);
        }
        catch
        {
            // Observer callbacks are intentionally isolated from runtime control flow.
        }
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
            Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
            PartialNode = new ErrorNode(new SourceSpan(originInputPosition, 0), "DEFAULT_MODE", "Alternative not evaluated", rule),
            EndPosition = null,
            Status = ActiveParseStateStatus.Active,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
    }

    private static ActiveParseState EnsureInitialized(ActiveParseState state)
    {
        return state with
        {
            Status = state.Status == ActiveParseStateStatus.Active ? ActiveParseStateStatus.Completed : state.Status,
            EndPosition = state.EndPosition ?? state.CurrentInputPosition
        };
    }

    /// <summary>
    /// Deduplicates locally completed states by structural scheduling identity.
    /// Deduplication keeps the strongest state per identity using <see cref="IsBetterState"/>,
    /// then returns states in deterministic traversal order.
    /// </summary>
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

    /// <summary>
    /// Compares two local candidates for best-candidate selection using the current observable contract:
    /// longer match wins; if tied, lower precedence priority value wins; if still tied,
    /// lower alternative index wins.
    /// </summary>
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

    /// <summary>
    /// Prunes equivalent states for orchestration efficiency only.
    /// This method must not create new syntax diagnostics beyond delegated ambiguity reporting,
    /// and must not alter parse authority owned by <see cref="ParserEngine"/>.
    /// </summary>
    private static List<ActiveParseState> PruneEquivalentActiveStates(IReadOnlyList<ActiveParseState> states, DiagnosticBag? diagnostics)
    {
        // Pruning safety invariant:
        // two branches are mergeable only when HasDistinctSemantics reports no potential semantic divergence.
        // This equivalence is structural/orchestration-oriented and intentionally conservative;
        // it does not claim semantic-runtime equivalence beyond current heuristics.
        // Branch equivalence and invocation-result reuse are separate identity systems.
        // If distinct semantics are possible, both branches must be preserved.
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
