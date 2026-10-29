using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Utils.Expressions;

/// <summary>
/// Open-addressing table mapping expression nodes, by reference, to their <see cref="ExpressionEffects"/> (ETC R1b). It
/// replaces a <see cref="Dictionary{TKey, TValue}"/> with a reference comparer on the hot path of the simplifier's rule
/// gate: no comparer dispatch, linear probing over a power-of-two array, no removal. Owned by one resource session, hence
/// by one thread.
/// </summary>
internal sealed class ExpressionEffectTable
{
    /// <summary>The stored keys; <see langword="null"/> marks an empty slot.</summary>
    private Expression?[] _keys = new Expression?[64];

    /// <summary>The effects of the key at the same index.</summary>
    private ExpressionEffects[] _values = new ExpressionEffects[64];

    /// <summary>The number of stored keys.</summary>
    private int _count;

    /// <summary>Looks up the effects stored for <paramref name="key"/>.</summary>
    /// <param name="key">The node.</param>
    /// <param name="effects">The stored effects, when found.</param>
    /// <returns><see langword="true"/> when the node is stored.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(Expression key, out ExpressionEffects effects)
    {
        Expression?[] keys = _keys;
        int mask = keys.Length - 1;
        for (int index = Slot(key, mask); ; index = (index + 1) & mask)
        {
            Expression? stored = keys[index];
            if (ReferenceEquals(stored, key))
            {
                effects = _values[index];
                return true;
            }

            if (stored is null)
            {
                effects = ExpressionEffects.None;
                return false;
            }
        }
    }

    /// <summary>Stores (or replaces) the effects of <paramref name="key"/>.</summary>
    /// <param name="key">The node.</param>
    /// <param name="effects">Its effects.</param>
    public void Set(Expression key, ExpressionEffects effects)
    {
        // Keeps the load factor at most one half, so probe sequences stay short and always reach an empty slot.
        if ((_count + 1) * 2 > _keys.Length) Grow();
        if (Insert(_keys, _values, key, effects)) _count++;
    }

    /// <summary>Doubles the capacity and reinserts every key.</summary>
    private void Grow()
    {
        Expression?[] oldKeys = _keys;
        ExpressionEffects[] oldValues = _values;
        var keys = new Expression?[oldKeys.Length * 2];
        var values = new ExpressionEffects[keys.Length];
        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldKeys[i] is { } key) Insert(keys, values, key, oldValues[i]);
        }

        _keys = keys;
        _values = values;
    }

    /// <summary>Inserts or replaces a key in the given arrays.</summary>
    /// <param name="keys">The key array.</param>
    /// <param name="values">The value array.</param>
    /// <param name="key">The node.</param>
    /// <param name="effects">Its effects.</param>
    /// <returns><see langword="true"/> when the key was not stored yet.</returns>
    private static bool Insert(Expression?[] keys, ExpressionEffects[] values, Expression key, ExpressionEffects effects)
    {
        int mask = keys.Length - 1;
        for (int index = Slot(key, mask); ; index = (index + 1) & mask)
        {
            Expression? stored = keys[index];
            if (stored is null)
            {
                keys[index] = key;
                values[index] = effects;
                return true;
            }

            if (ReferenceEquals(stored, key))
            {
                values[index] = effects;
                return false;
            }
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
