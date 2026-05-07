namespace Utils.Parser.Runtime;

/// <summary>
/// Caches lightweight scheduled-alternative look-ahead observations for the current parse execution.
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
