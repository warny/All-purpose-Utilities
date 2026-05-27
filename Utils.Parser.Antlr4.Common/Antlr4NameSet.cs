namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Creates deterministic ANTLR4 prequel name sets using ordinal comparers.
/// </summary>
public static class Antlr4NameSet
{
    /// <summary>
    /// Builds a read-only set from <paramref name="names"/> preserving ordinal comparison semantics.
    /// </summary>
    /// <param name="names">Names to include in the set.</param>
    /// <returns>An ordinal, read-only set wrapper.</returns>
    public static IReadOnlyCollection<string> Create(IEnumerable<string> names)
    {
        return new ReadOnlyNameSet(names);
    }

    private sealed class ReadOnlyNameSet : IReadOnlyCollection<string>
    {
        private readonly HashSet<string> _inner;

        public ReadOnlyNameSet(IEnumerable<string> names)
        {
            _inner = new HashSet<string>(names, StringComparer.Ordinal);
        }

        public int Count => _inner.Count;

        public bool Contains(string name)
        {
            return _inner.Contains(name);
        }

        public IEnumerator<string> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
