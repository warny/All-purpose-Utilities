using Utils.Parser.Diagnostics;

namespace Utils.Parser.Runtime;

/// <summary>
/// Runs deterministic sequential scheduling for parser alternatives represented as <see cref="ActiveParseState"/>.
/// </summary>
internal sealed class AlternativeScheduler
{
    private readonly ParserStateRegistry _registry;

    /// <summary>
    /// Initializes a scheduler bound to the parser state registry.
    /// </summary>
    /// <param name="registry">Shared registry used by the parser engine.</param>
    public AlternativeScheduler(ParserStateRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Schedules already evaluated alternative states, applies pruning, and selects the winning state.
    /// </summary>
    /// <param name="initialStates">Completed active states produced for alternatives.</param>
    /// <param name="diagnostics">Optional diagnostic collector.</param>
    /// <returns>Scheduling result with selected and categorized states.</returns>
    public AlternativeSchedulingResult Run(
        IReadOnlyList<ActiveParseState> initialStates,
        DiagnosticBag? diagnostics)
    {
        var statesByKey = new Dictionary<ActiveParseStateKey, ActiveParseState>();
        foreach (var state in initialStates)
        {
            // Scheduler identity deduplicates exact same state identity only.
            var key = state.ToStateKey(minimumPrecedence: 0);
            if (!statesByKey.TryGetValue(key, out var existing) || IsBetterState(state, existing))
            {
                statesByKey[key] = state;
            }
        }

        var deduplicated = statesByKey.Values
            .OrderBy(static s => s.Alternative.Priority)
            .ThenBy(static s => s.AlternativeIndex)
            .ThenBy(static s => s.CurrentInputPosition)
            .ToList();

        var pruned = PruneEquivalentActiveStates(deduplicated, diagnostics);
        var prunedSet = new HashSet<ActiveParseState>(pruned);
        var prunedStates = deduplicated
            .Where(s => !prunedSet.Contains(s))
            .Select(static s => s.Prune())
            .ToList();

        ActiveParseState? selected = null;
        foreach (var state in pruned)
        {
            if (selected is null || IsBetterState(state, selected))
            {
                selected = state;
            }
        }

        return new AlternativeSchedulingResult(selected, pruned, [], prunedStates);
    }

    /// <summary>
    /// Compares two active states using parser-compatible deterministic tie-breaking.
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
    /// Prunes states that are equivalent in parse shape while preserving semantically distinct alternatives.
    /// </summary>
    private static List<ActiveParseState> PruneEquivalentActiveStates(
        IReadOnlyList<ActiveParseState> states,
        DiagnosticBag? diagnostics)
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

                diagnostics?.AddWithContext(
                    ParserDiagnostics.AmbiguousAlternativesPruned,
                    state.PartialNode.Span.Position,
                    state.PartialNode.Span.Length,
                    state.Rule.Name,
                    null,
                    state.Rule.Name);

                merged = true;
                break;
            }

            if (!merged)
            {
                group.Add(state);
            }
        }

        return groups.Values
            .SelectMany(static g => g)
            .OrderBy(static s => s.Alternative.Priority)
            .ThenBy(static s => s.AlternativeIndex)
            .ThenByDescending(static s => s.CurrentInputPosition)
            .ToList();
    }
}

/// <summary>
/// Represents the output of a deterministic alternative scheduling pass.
/// </summary>
internal sealed class AlternativeSchedulingResult
{
    /// <summary>
    /// Initializes a new scheduling result.
    /// </summary>
    public AlternativeSchedulingResult(
        ActiveParseState? selectedState,
        IReadOnlyList<ActiveParseState> completedStates,
        IReadOnlyList<ActiveParseState> failedStates,
        IReadOnlyList<ActiveParseState> prunedStates)
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
