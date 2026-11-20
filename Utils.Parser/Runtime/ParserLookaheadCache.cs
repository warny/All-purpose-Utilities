namespace Utils.Parser.Runtime;

/// <summary>
/// Caches lightweight scheduled-alternative look-ahead observations for the current parse execution.
/// Entries are syntax-oriented probe metadata only and are independent from semantic runtime state,
/// parser action side effects, replay frames, and rollback mechanisms.
/// Cached values are advisory: they may inform conservative shortcut rejection, but they never
/// own parse acceptance and never replace authoritative parse execution in <see cref="ParserEngine"/>.
/// </summary>
internal sealed class ParserLookaheadCache
{
    private readonly Dictionary<ParserLookaheadKey, ParserLookaheadProbeResult> _results = [];

    /// <summary>
    /// Clears all cached look-ahead observations, releasing the capacity of a cache that grew large
    /// (<see cref="ParserStateRetention"/>).
    /// </summary>
    public void Clear()
    {
        ParserStateRetention.Clear(_results);
    }

    /// <summary>
    /// Tries to get a cached look-ahead observation for the provided key.
    /// </summary>
    public bool TryGet(ParserLookaheadKey key, out ParserLookaheadProbeResult result)
    {
        return _results.TryGetValue(key, out result);
    }

    /// <summary>
    /// Adds a look-ahead observation when no value exists for the same key.
    /// </summary>
    public bool TryAdd(ParserLookaheadKey key, ParserLookaheadProbeResult result)
        => TryAdd(key, result, null);

    /// <summary>
    /// Adds a look-ahead observation for a budgeted parse operation when no value exists for the same key, charging one
    /// retained state before the entry is stored.
    /// </summary>
    /// <param name="key">Look-ahead identity.</param>
    /// <param name="result">Advisory probe result.</param>
    /// <param name="budget">The budget of the running parse operation, or <see langword="null"/> when unbudgeted.</param>
    /// <returns><c>true</c> when the entry was stored.</returns>
    /// <exception cref="ParserResourceLimitException">The retained-state budget is exhausted; nothing is stored.</exception>
    public bool TryAdd(ParserLookaheadKey key, ParserLookaheadProbeResult result, ParserResourceBudget? budget)
    {
        if (_results.ContainsKey(key))
        {
            return false;
        }

        budget?.ChargeRetainedState();
        _results.Add(key, result);
        return true;
    }
}
