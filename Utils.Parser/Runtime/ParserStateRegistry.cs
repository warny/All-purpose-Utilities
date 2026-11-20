namespace Utils.Parser.Runtime;

/// <summary>
/// Stores parser-state tracking, shared rule invocation completions, and continuation metadata.
/// Reusable results in this registry are invocation-local transport artifacts; they do not
/// independently decide final parse acceptance, rejection, or final diagnostic selection.
/// Runtime-authoritative state in this registry is limited to visited states, invocation completion tracking,
/// and reusable parse outcomes keyed by invocation identity. Reusable outcomes may carry opaque post-rule
/// execution-state snapshots for memoization-hit restoration only; those snapshots are not replay frames.
/// Continuation descriptors and shared-prefix scheduling metadata are non-authoritative descriptive data.
/// Shared-prefix metadata in this registry is descriptive-only and does not authorize replay,
/// continuation execution, branch merging, semantic equivalence, or rollback safety assumptions.
/// This registry does not contain replay frames, semantic runtime frames, rollback state, or executable continuations.
/// </summary>
internal sealed class ParserStateRegistry
{
    /// <summary>Set of state keys that have been entered during the current parse, used to prevent redundant re-entry.</summary>
    private readonly HashSet<ParserStateKey> _visitedStates = [];
    /// <summary>Continuation keys grouped by rule invocation identity for post-completion replay metadata.</summary>
    private readonly Dictionary<RuleInvocationKey, HashSet<ContinuationKey>> _continuations = [];
    /// <summary>Completed parse results grouped by state-aware rule invocation identity for local reuse.</summary>
    private readonly Dictionary<RuleInvocationKey, List<ParserRuleResult>> _completedResults = [];

    /// <summary>
    /// Resets all registry state for a new parse, releasing the capacity of a collection that grew large
    /// (<see cref="ParserStateRetention"/>).
    /// </summary>
    public void Clear()
    {
        ParserStateRetention.Clear(_visitedStates);
        ParserStateRetention.Clear(_continuations);
        ParserStateRetention.Clear(_completedResults);
    }

    /// <summary>Marks a state as visited and returns <c>true</c> when it was not seen before.</summary>
    /// <remarks>
    /// <see cref="ParserStateKey"/> is visited-state tracking identity only.
    /// It is intentionally independent from local scheduler identity and pruning equivalence identity.
    /// </remarks>
    public bool TryEnterState(ParserStateKey key) => _visitedStates.Add(key);

    /// <summary>
    /// Marks a state as visited for a budgeted parse operation, charging one retained state before a new key is stored.
    /// </summary>
    /// <param name="key">Visited-state identity.</param>
    /// <param name="budget">The budget of the running parse operation.</param>
    /// <returns><c>true</c> when the state was not seen before.</returns>
    /// <exception cref="ParserResourceLimitException">The retained-state budget is exhausted; nothing is stored.</exception>
    public bool TryEnterState(ParserStateKey key, ParserResourceBudget budget)
    {
        if (_visitedStates.Contains(key))
        {
            return false;
        }

        budget.ChargeRetainedState();
        return _visitedStates.Add(key);
    }

    /// <summary>
    /// Adds continuation metadata for a shared rule invocation.
    /// The recorded continuation key is observational transport data only and is not executable resume authority.
    /// </summary>
    public bool AddContinuation(RuleInvocationKey invocation, ContinuationKey continuation)
        => AddContinuation(invocation, continuation, null);

    /// <summary>
    /// Adds continuation metadata for a shared rule invocation of a budgeted parse operation, charging one retained state
    /// before a new entry is stored.
    /// </summary>
    /// <param name="invocation">Invocation identity.</param>
    /// <param name="continuation">Observational continuation key.</param>
    /// <param name="budget">The budget of the running parse operation, or <see langword="null"/> when unbudgeted.</param>
    /// <returns><c>true</c> when the continuation was not recorded before.</returns>
    /// <exception cref="ParserResourceLimitException">The retained-state budget is exhausted; nothing is stored.</exception>
    public bool AddContinuation(RuleInvocationKey invocation, ContinuationKey continuation, ParserResourceBudget? budget)
    {
        if (!_continuations.TryGetValue(invocation, out var set))
        {
            budget?.ChargeRetainedState();
            set = [];
            _continuations[invocation] = set;
            return set.Add(continuation);
        }

        if (set.Contains(continuation))
        {
            return false;
        }

        budget?.ChargeRetainedState();
        return set.Add(continuation);
    }

    /// <summary>
    /// Adds a completed result for a shared rule invocation.
    /// Stored outcomes are invocation-local reuse artifacts and do not transfer global parse-authority ownership.
    /// Reuse identity (<see cref="RuleInvocationKey"/>) is intentionally independent from pruning/scheduling identities.
    /// Result execution-state snapshots are opaque post-rule restoration data for memoization hits, not action replay authority.
    /// </summary>
    public bool AddCompletedResult(RuleInvocationKey invocation, ParserRuleResult result)
        => AddCompletedResult(invocation, result, null);

    /// <summary>
    /// Adds a completed result for a shared rule invocation of a budgeted parse operation, charging one retained state
    /// before a new result is stored. A refused insertion stores nothing and ends the whole parse: completed results are
    /// never evicted or silently dropped.
    /// </summary>
    /// <param name="invocation">State-aware invocation identity.</param>
    /// <param name="result">Completed result.</param>
    /// <param name="budget">The budget of the running parse operation, or <see langword="null"/> when unbudgeted.</param>
    /// <returns><c>true</c> when the result was stored.</returns>
    /// <exception cref="ParserResourceLimitException">The retained-state budget is exhausted; nothing is stored.</exception>
    public bool AddCompletedResult(RuleInvocationKey invocation, ParserRuleResult result, ParserResourceBudget? budget)
    {
        if (_completedResults.TryGetValue(invocation, out var list))
        {
            foreach (var existing in list)
            {
                if (existing.EndPosition == result.EndPosition
                    && existing.IsFailure == result.IsFailure
                    && ReferenceEquals(existing.Node, result.Node))
                {
                    return false;
                }
            }
        }

        budget?.ChargeRetainedState();
        if (list is null)
        {
            list = [];
            _completedResults[invocation] = list;
        }

        list.Add(result);
        return true;
    }

    /// <summary>
    /// Gets continuations previously registered for an invocation.
    /// Returned values are metadata snapshots and remain discardable from an execution-authority perspective.
    /// Retrieval does not provide replay/resume capability and does not imply executable parser frames.
    /// </summary>
    public IReadOnlyList<ContinuationKey> GetContinuations(RuleInvocationKey invocation)
    {
        return _continuations.TryGetValue(invocation, out var set) ? [.. set] : [];
    }

    /// <summary>
    /// Gets completed results previously registered for an invocation.
    /// Retrieved values are reusable local outcomes and not final parse acceptance decisions.
    /// </summary>
    public IReadOnlyList<ParserRuleResult> GetCompletedResults(RuleInvocationKey invocation)
    {
        return _completedResults.TryGetValue(invocation, out var list) ? list : [];
    }

    /// <summary>
    /// Determines whether an invocation has any reusable completion result (success or failure).
    /// Current deterministic preference is: first reusable success, otherwise first reusable failure.
    /// This is invocation-local completion reuse, not final branch-selection authority.
    /// Reuse is isolated by the semantic execution-state key carried by the invocation key, and the returned result
    /// may carry a post-rule execution-state snapshot that the parser engine restores without replaying actions.
    /// </summary>
    public bool TryGetReusableResult(RuleInvocationKey invocation, out ParserRuleResult result)
    {
        result = default;
        if (!_completedResults.TryGetValue(invocation, out var list) || list.Count == 0)
        {
            return false;
        }

        // Reuse chooses an invocation-completion artifact only; it does not recreate branch execution history.
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
