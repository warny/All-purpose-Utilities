namespace Utils.Parser.Runtime;

/// <summary>
/// Clears the per-operation collections of an engine without letting a large operation pin its capacity. Clearing a
/// <see cref="Dictionary{TKey, TValue}"/> or a <see cref="HashSet{T}"/> keeps its bucket and entry arrays, so an engine
/// that served one large input would otherwise keep memory proportional to that input for as long as it lives. A
/// collection that held more than <see cref="MaxRetainedEntries"/> entries is shrunk after clearing; a smaller one keeps
/// its capacity, so sequential reuse on ordinary inputs does not regrow it on every operation.
/// </summary>
internal static class ParserStateRetention
{
    /// <summary>
    /// The number of entries above which a cleared collection releases its capacity. A collection kept below it retains at
    /// most a few hundred kilobytes, whatever the size of the inputs the engine has processed.
    /// </summary>
    internal const int MaxRetainedEntries = 1024;

    /// <summary>Removes every element of a set and releases its capacity when it exceeded <see cref="MaxRetainedEntries"/>.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="set">The set to clear.</param>
    public static void Clear<T>(HashSet<T> set)
    {
        bool large = set.Count > MaxRetainedEntries;
        set.Clear();
        if (large) set.TrimExcess();
    }

    /// <summary>Removes every element of a list and releases its capacity when it exceeded <see cref="MaxRetainedEntries"/>.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list to clear.</param>
    public static void Clear<T>(List<T> list)
    {
        bool large = list.Count > MaxRetainedEntries;
        list.Clear();
        if (large) list.TrimExcess();
    }

    /// <summary>Removes every entry of a dictionary and releases its capacity when it exceeded <see cref="MaxRetainedEntries"/>.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary to clear.</param>
    public static void Clear<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        bool large = dictionary.Count > MaxRetainedEntries;
        dictionary.Clear();
        if (large) dictionary.TrimExcess();
    }
}
