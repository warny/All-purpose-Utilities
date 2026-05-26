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
    public static ISet<string> Create(IEnumerable<string> names)
    {
        return new ReadOnlySet<string>(new HashSet<string>(names, StringComparer.Ordinal));
    }

    private sealed class ReadOnlySet<T>(HashSet<T> inner) : ISet<T>
    {
        public int Count => inner.Count;
        public bool IsReadOnly => true;

        public bool Contains(T item) => inner.Contains(item);
        public IEnumerator<T> GetEnumerator() => inner.GetEnumerator();
        public bool IsProperSubsetOf(IEnumerable<T> other) => inner.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => inner.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => inner.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => inner.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => inner.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => inner.SetEquals(other);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public void CopyTo(T[] array, int arrayIndex) => inner.CopyTo(array, arrayIndex);

        public bool Add(T item) => throw new NotSupportedException("Set is read-only.");
        void ICollection<T>.Add(T item) => throw new NotSupportedException("Set is read-only.");
        public bool Remove(T item) => throw new NotSupportedException("Set is read-only.");
        public void Clear() => throw new NotSupportedException("Set is read-only.");
        public void ExceptWith(IEnumerable<T> other) => throw new NotSupportedException("Set is read-only.");
        public void IntersectWith(IEnumerable<T> other) => throw new NotSupportedException("Set is read-only.");
        public void SymmetricExceptWith(IEnumerable<T> other) => throw new NotSupportedException("Set is read-only.");
        public void UnionWith(IEnumerable<T> other) => throw new NotSupportedException("Set is read-only.");
    }
}
