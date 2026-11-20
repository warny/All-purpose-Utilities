using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
/// <remarks>
/// The storage is compact but exact: every collection keeps the full identity of its entries, so membership, duplicate
/// detection and therefore <see cref="ParserResourceBudget.ChargeRetainedState"/> charges are those of one set of
/// <see cref="ParserStateKey"/>, one set of (<see cref="RuleInvocationKey"/>, <see cref="ContinuationKey"/>) pairs and
/// one insertion-ordered result list per <see cref="RuleInvocationKey"/>. Continuations are a set: their enumeration
/// order is not part of the contract. No hash stands for an identity, nothing is
/// evicted, and no storage is shared between registries.
/// </remarks>
internal sealed class ParserStateRegistry
{
    /// <summary>Number of alternative indexes a visited-state group can record in its bit mask.</summary>
    private const int VisitedStateGroupWidth = 64;

    /// <summary>
    /// Visited states whose element index equals their alternative index (every key the parser engine records) and whose
    /// alternative index is below <see cref="VisitedStateGroupWidth"/>, grouped by (rule, position, precedence): bit
    /// <c>i</c> of the mask records the state of alternative <c>i</c>. One entry replaces up to 64 set entries.
    /// </summary>
    private readonly Dictionary<VisitedStateGroupKey, ulong> _visitedStateGroups = [];

    /// <summary>Visited states that cannot be grouped (element index different from the alternative index, or index out of the mask range).</summary>
    private readonly HashSet<ParserStateKey> _visitedStateOverflow = [];

    /// <summary>
    /// Continuation metadata as one flat set of (invocation, continuation) pairs. It records the same pairs as a set of
    /// continuations per invocation without one nested set per invocation.
    /// </summary>
    private readonly HashSet<ContinuationEntry> _continuations = [];

    /// <summary>Completed parse results grouped by state-aware rule invocation identity for local reuse, in insertion order.</summary>
    private readonly Dictionary<RuleInvocationKey, CompletedResults> _completedResults = [];

    /// <summary>Gets the number of distinct visited states recorded.</summary>
    internal long VisitedStateCount
    {
        get
        {
            long count = _visitedStateOverflow.Count;
            foreach (ulong mask in _visitedStateGroups.Values)
            {
                count += BitOperations.PopCount(mask);
            }

            return count;
        }
    }

    /// <summary>Gets the number of visited-state groups (diagnostics of the compact representation).</summary>
    internal int VisitedStateGroupCount => _visitedStateGroups.Count;

    /// <summary>Gets the number of distinct (invocation, continuation) pairs recorded.</summary>
    internal int ContinuationCount => _continuations.Count;

    /// <summary>Gets the number of distinct invocations carrying at least one continuation (computed by a full scan).</summary>
    internal int ContinuationInvocationCount => _continuations.Select(static entry => entry.Invocation).Distinct().Count();

    /// <summary>Gets the number of distinct invocations carrying at least one completed result.</summary>
    internal int CompletedInvocationCount => _completedResults.Count;

    /// <summary>Gets the number of completed results recorded.</summary>
    internal long CompletedResultCount
    {
        get
        {
            long count = 0;
            foreach (var results in _completedResults.Values)
            {
                count += results.Count;
            }

            return count;
        }
    }

    /// <summary>
    /// Resets all registry state for a new parse, releasing the capacity of a collection that grew large
    /// (<see cref="ParserStateRetention"/>).
    /// </summary>
    public void Clear()
    {
        ParserStateRetention.Clear(_visitedStateGroups);
        ParserStateRetention.Clear(_visitedStateOverflow);
        ParserStateRetention.Clear(_continuations);
        ParserStateRetention.Clear(_completedResults);
    }

    /// <summary>Marks a state as visited and returns <c>true</c> when it was not seen before.</summary>
    /// <remarks>
    /// <see cref="ParserStateKey"/> is visited-state tracking identity only.
    /// It is intentionally independent from local scheduler identity and pruning equivalence identity.
    /// </remarks>
    public bool TryEnterState(ParserStateKey key) => TryEnterStateCore(key, null);

    /// <summary>
    /// Marks a state as visited for a budgeted parse operation, charging one retained state before a new key is stored.
    /// </summary>
    /// <param name="key">Visited-state identity.</param>
    /// <param name="budget">The budget of the running parse operation.</param>
    /// <returns><c>true</c> when the state was not seen before.</returns>
    /// <exception cref="ParserResourceLimitException">The retained-state budget is exhausted; nothing is stored.</exception>
    public bool TryEnterState(ParserStateKey key, ParserResourceBudget budget) => TryEnterStateCore(key, budget);

    /// <summary>
    /// Records a visited state in its group mask or in the overflow set, charging <paramref name="budget"/> only when the
    /// state is new and before anything is stored.
    /// </summary>
    /// <param name="key">Visited-state identity.</param>
    /// <param name="budget">The budget of the running parse operation, or <see langword="null"/> when unbudgeted.</param>
    /// <returns><c>true</c> when the state was not seen before.</returns>
    /// <exception cref="ParserResourceLimitException">The retained-state budget is exhausted; nothing is stored.</exception>
    private bool TryEnterStateCore(ParserStateKey key, ParserResourceBudget? budget)
    {
        // The partition depends on the key alone, so a key is always looked up where it would have been stored.
        if (key.ElementIndex != key.AlternativeIndex || (uint)key.AlternativeIndex >= VisitedStateGroupWidth)
        {
            if (_visitedStateOverflow.Contains(key))
            {
                return false;
            }

            budget?.ChargeRetainedState();
            return _visitedStateOverflow.Add(key);
        }

        var groupKey = new VisitedStateGroupKey(key.RuleName, key.InputPosition, key.MinimumPrecedence);
        ulong bit = 1UL << key.AlternativeIndex;
        ref ulong mask = ref CollectionsMarshal.GetValueRefOrNullRef(_visitedStateGroups, groupKey);
        if (Unsafe.IsNullRef(ref mask))
        {
            budget?.ChargeRetainedState();
            _visitedStateGroups.Add(groupKey, bit);
            return true;
        }

        if ((mask & bit) != 0)
        {
            return false;
        }

        budget?.ChargeRetainedState();
        mask |= bit;
        return true;
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
        var entry = new ContinuationEntry(invocation, continuation);
        if (_continuations.Contains(entry))
        {
            return false;
        }

        budget?.ChargeRetainedState();
        return _continuations.Add(entry);
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
        ref CompletedResults results = ref CollectionsMarshal.GetValueRefOrNullRef(_completedResults, invocation);
        if (Unsafe.IsNullRef(ref results))
        {
            budget?.ChargeRetainedState();
            _completedResults.Add(invocation, new CompletedResults(result));
            return true;
        }

        for (int index = 0; index < results.Count; index++)
        {
            var existing = results[index];
            if (existing.EndPosition == result.EndPosition
                && existing.IsFailure == result.IsFailure
                && ReferenceEquals(existing.Node, result.Node))
            {
                return false;
            }
        }

        budget?.ChargeRetainedState();
        results.Add(result);
        return true;
    }

    /// <summary>
    /// Gets the distinct continuations previously registered for an invocation, in an unspecified order.
    /// Returned values are metadata snapshots and remain discardable from an execution-authority perspective.
    /// Retrieval does not provide replay/resume capability and does not imply executable parser frames.
    /// </summary>
    /// <remarks>
    /// The contract is the set of continuations: each distinct continuation appears exactly once, and callers must not
    /// rely on the enumeration order (neither the historical per-invocation set nor the current flat set guarantees one).
    /// The parser engine never reads continuations; this inspection scans every recorded pair and is meant for tests and
    /// diagnostics only.
    /// </remarks>
    public IReadOnlyList<ContinuationKey> GetContinuations(RuleInvocationKey invocation)
    {
        List<ContinuationKey> continuations = [];
        foreach (var entry in _continuations)
        {
            if (entry.Invocation == invocation)
            {
                continuations.Add(entry.Continuation);
            }
        }

        return continuations;
    }

    /// <summary>
    /// Gets completed results previously registered for an invocation, in insertion order.
    /// Retrieved values are reusable local outcomes and not final parse acceptance decisions.
    /// </summary>
    public IReadOnlyList<ParserRuleResult> GetCompletedResults(RuleInvocationKey invocation)
    {
        if (!_completedResults.TryGetValue(invocation, out var results))
        {
            return [];
        }

        var copy = new ParserRuleResult[results.Count];
        for (int index = 0; index < copy.Length; index++)
        {
            copy[index] = results[index];
        }

        return copy;
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
        ref CompletedResults results = ref CollectionsMarshal.GetValueRefOrNullRef(_completedResults, invocation);
        if (Unsafe.IsNullRef(ref results))
        {
            return false;
        }

        // Reuse chooses an invocation-completion artifact only; it does not recreate branch execution history.
        for (int index = 0; index < results.Count; index++)
        {
            var item = results[index];
            if (!item.IsFailure && item.Node is not null)
            {
                result = item;
                return true;
            }
        }

        for (int index = 0; index < results.Count; index++)
        {
            var item = results[index];
            if (item.IsFailure)
            {
                result = item;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Groups the visited states of one rule at one input position and precedence; the alternative index selects a bit of
    /// the group mask.
    /// </summary>
    /// <param name="RuleName">The name of the grammar rule.</param>
    /// <param name="InputPosition">The input position.</param>
    /// <param name="MinimumPrecedence">The minimum precedence level.</param>
    private readonly record struct VisitedStateGroupKey(string RuleName, int InputPosition, int MinimumPrecedence);

    /// <summary>One recorded continuation: the invocation it belongs to and the continuation itself.</summary>
    /// <param name="Invocation">Invocation identity.</param>
    /// <param name="Continuation">Observational continuation key.</param>
    private readonly record struct ContinuationEntry(RuleInvocationKey Invocation, ContinuationKey Continuation);

    /// <summary>
    /// The completed results of one invocation in insertion order. The first result is stored inline: the parser engine
    /// stores at most one result per invocation, so the overflow list is only allocated by direct registry callers.
    /// </summary>
    private struct CompletedResults
    {
        /// <summary>The first stored result.</summary>
        private readonly ParserRuleResult _first;

        /// <summary>The results stored after the first one, or <see langword="null"/> when there are none.</summary>
        private List<ParserRuleResult>? _more;

        /// <summary>Initializes the results of an invocation with its first result.</summary>
        /// <param name="first">The first result.</param>
        public CompletedResults(ParserRuleResult first)
        {
            _first = first;
            _more = null;
        }

        /// <summary>Gets the number of stored results.</summary>
        public readonly int Count => 1 + (_more?.Count ?? 0);

        /// <summary>Gets the result at an insertion index.</summary>
        /// <param name="index">The insertion index, below <see cref="Count"/>.</param>
        /// <returns>The result.</returns>
        public readonly ParserRuleResult this[int index] => index == 0 ? _first : _more![index - 1];

        /// <summary>Appends a result.</summary>
        /// <param name="result">The result.</param>
        public void Add(ParserRuleResult result) => (_more ??= []).Add(result);
    }
}
