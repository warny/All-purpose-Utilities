namespace Utils.Parser.Runtime;

/// <summary>
/// Stores parser-state tracking, shared rule invocation completions, and continuation metadata.
/// Reusable results in this registry are invocation-local transport artifacts; they do not
/// independently decide final parse acceptance, rejection, or final diagnostic selection.
/// Runtime-authoritative state in this registry is limited to visited states, invocation completion tracking,
/// and reusable parse outcomes keyed by invocation identity.
/// Continuation descriptors and shared-prefix scheduling metadata are non-authoritative descriptive data.
/// This registry does not contain replay frames, semantic runtime frames, rollback state, or executable continuations.
/// </summary>
internal sealed class ParserStateRegistry
{
    private readonly HashSet<ParserStateKey> _visitedStates = [];
    private readonly Dictionary<RuleInvocationKey, HashSet<ContinuationKey>> _continuations = [];
    private readonly Dictionary<RuleInvocationKey, List<ParserRuleResult>> _completedResults = [];

    /// <summary>Resets all registry state for a new parse.</summary>
    public void Clear()
    {
        _visitedStates.Clear();
        _continuations.Clear();
        _completedResults.Clear();
    }

    /// <summary>Marks a state as visited and returns <c>true</c> when it was not seen before.</summary>
    public bool TryEnterState(ParserStateKey key) => _visitedStates.Add(key);

    /// <summary>Adds a continuation for a shared rule invocation.</summary>
    public bool AddContinuation(RuleInvocationKey invocation, ContinuationKey continuation)
    {
        if (!_continuations.TryGetValue(invocation, out var set))
        {
            set = [];
            _continuations[invocation] = set;
        }

        return set.Add(continuation);
    }

    /// <summary>Adds a completed result for a shared rule invocation.</summary>
    public bool AddCompletedResult(RuleInvocationKey invocation, ParserRuleResult result)
    {
        if (!_completedResults.TryGetValue(invocation, out var list))
        {
            list = [];
            _completedResults[invocation] = list;
        }

        if (list.Any(existing => existing.EndPosition == result.EndPosition
            && existing.IsFailure == result.IsFailure
            && ReferenceEquals(existing.Node, result.Node)))
        {
            return false;
        }

        list.Add(result);
        return true;
    }

    /// <summary>Gets continuations previously registered for an invocation.</summary>
    public IReadOnlyList<ContinuationKey> GetContinuations(RuleInvocationKey invocation)
    {
        return _continuations.TryGetValue(invocation, out var set) ? [.. set] : [];
    }

    /// <summary>Gets completed results previously registered for an invocation.</summary>
    public IReadOnlyList<ParserRuleResult> GetCompletedResults(RuleInvocationKey invocation)
    {
        return _completedResults.TryGetValue(invocation, out var list) ? list : [];
    }

    /// <summary>
    /// Determines whether an invocation has any reusable completion result (success or failure).
    /// Current deterministic preference is: first reusable success, otherwise first reusable failure.
    /// This is invocation-local completion reuse, not final branch-selection authority.
    /// Reuse currently assumes deterministic evaluator/executor behavior for the same invocation key
    /// and does not model external semantic/action state.
    /// </summary>
    public bool TryGetReusableResult(RuleInvocationKey invocation, out ParserRuleResult result)
    {
        result = default;
        if (!_completedResults.TryGetValue(invocation, out var list) || list.Count == 0)
        {
            return false;
        }

        var success = list.FirstOrDefault(static item => !item.IsFailure && item.Node is not null);
        if (success.Node is not null)
        {
            result = success;
            return true;
        }

        var failure = list.FirstOrDefault(static item => item.IsFailure);
        if (!failure.IsFailure)
        {
            return false;
        }

        result = failure;
        return true;
    }
}
