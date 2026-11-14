using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Utils.Expressions;

/// <summary>
/// Open-addressing set of expression nodes, by reference, modeled on <see cref="ExpressionEffectTable"/>: no comparer
/// dispatch (so no <see cref="object.Equals(object?)"/> or <see cref="object.GetHashCode"/> of a node ever runs), linear
/// probing over a power-of-two array, no removal. Owned by one resource session, hence by one thread.
/// </summary>
internal sealed class ExpressionNodeSet
{
    /// <summary>The stored nodes; <see langword="null"/> marks an empty slot.</summary>
    private Expression?[] _keys = new Expression?[32];

    /// <summary>The number of stored nodes.</summary>
    private int _count;

    /// <summary>Gets the number of stored nodes.</summary>
    public int Count => _count;

    /// <summary>Determines whether <paramref name="key"/> is stored.</summary>
    /// <param name="key">The node.</param>
    /// <returns><see langword="true"/> when the node is stored.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Expression key)
    {
        Expression?[] keys = _keys;
        int mask = keys.Length - 1;
        for (int index = Slot(key, mask); ; index = (index + 1) & mask)
        {
            Expression? stored = keys[index];
            if (ReferenceEquals(stored, key)) return true;
            if (stored is null) return false;
        }
    }

    /// <summary>Stores <paramref name="key"/>.</summary>
    /// <param name="key">The node.</param>
    /// <returns><see langword="true"/> when the node was not stored yet.</returns>
    public bool Add(Expression key)
    {
        // Keeps the load factor at most one half, so probe sequences stay short and always reach an empty slot.
        if ((_count + 1) * 2 > _keys.Length) Grow();
        if (!Insert(_keys, key)) return false;
        _count++;
        return true;
    }

    /// <summary>Doubles the capacity and reinserts every node.</summary>
    private void Grow()
    {
        Expression?[] oldKeys = _keys;
        var keys = new Expression?[oldKeys.Length * 2];
        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldKeys[i] is { } key) Insert(keys, key);
        }

        _keys = keys;
    }

    /// <summary>Inserts a node into the given array.</summary>
    /// <param name="keys">The node array.</param>
    /// <param name="key">The node.</param>
    /// <returns><see langword="true"/> when the node was not stored yet.</returns>
    private static bool Insert(Expression?[] keys, Expression key)
    {
        int mask = keys.Length - 1;
        for (int index = Slot(key, mask); ; index = (index + 1) & mask)
        {
            Expression? stored = keys[index];
            if (stored is null)
            {
                keys[index] = key;
                return true;
            }

            if (ReferenceEquals(stored, key)) return false;
        }
    }

    /// <summary>Computes the first probed slot of <paramref name="key"/> from its identity hash (Fibonacci hashing).</summary>
    /// <param name="key">The node.</param>
    /// <param name="mask">The capacity minus one.</param>
    /// <returns>The slot index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Slot(Expression key, int mask)
        => (int)((uint)(RuntimeHelpers.GetHashCode(key) * -1640531527) >> 7) & mask;
}
