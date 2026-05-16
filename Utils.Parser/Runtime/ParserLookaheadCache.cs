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
    /// Clears all cached look-ahead observations.
    /// </summary>
    public void Clear()
    {
        _results.Clear();
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
    {
        if (_results.ContainsKey(key))
        {
            return false;
        }

        _results.Add(key, result);
        return true;
    }
}
