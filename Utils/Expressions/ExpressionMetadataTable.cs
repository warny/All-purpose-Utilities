using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Utils.Mathematics.Expressions;

namespace Utils.Expressions;

/// <summary>
/// Open-addressing table mapping expression nodes, by reference, to the metadata the simplifier's canonicalizations already
/// computed for them during the running public operation (mission C3a): the raw structural hash
/// (<see cref="ExpressionComparer.StructuralHashRaw(Expression?)"/>), the canonical-order key
/// (<see cref="ExpressionCanonicalOrder.BuildKeyFromSnapshot(Expression?, ParameterExpression[][])"/>) with the lexical scope
/// snapshot it was built under, and the canonicalization state recorded by one simplifier instance. Modeled on
/// <see cref="ExpressionEffectTable"/>: no comparer dispatch, linear probing over a power-of-two array, no removal. Owned by
/// one resource session, hence by one thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>Validity.</b> Expression nodes are immutable. The raw hash is a function of the node alone (it starts from an empty
/// scope stack and identifies free parameters and unsupported nodes by identity), so a stored hash stays valid for the whole
/// chain. A key also depends on the enclosing lexical scopes (a parameter bound by an enclosing lambda is keyed by its
/// position): it is reused only under a snapshot made of the very same scope frames (<see cref="SameScopes"/>). A state is
/// read only by the instance that recorded it.
/// </para>
/// <para>
/// <b>Budgets.</b> Computing a value consumes the structural visits of its walk, as before; reusing it consumes nothing,
/// because no walk takes place. The table never stores a value that was not fully computed: a budget refusal raised in the
/// middle of a hash or a key leaves no entry behind.
/// </para>
/// </remarks>
internal sealed class ExpressionMetadataTable
{
    /// <summary>The stored keys; <see langword="null"/> marks an empty slot.</summary>
    private Expression?[] _keys = new Expression?[8];

    /// <summary>The metadata of the key at the same index.</summary>
    private Entry[] _values = new Entry[8];

    /// <summary>The number of stored keys.</summary>
    private int _count;

    /// <summary>Gets the number of stored nodes (focused tests).</summary>
    internal int Count => _count;

    /// <summary>Gets the number of slots (focused tests).</summary>
    internal int Capacity => _keys.Length;

    /// <summary>The metadata stored for one node; every part is optional.</summary>
    internal struct Entry
    {
        /// <summary>Whether <see cref="Hash"/> holds the raw structural hash of the node.</summary>
        public bool HasHash;

        /// <summary>The raw structural hash, when <see cref="HasHash"/> is set.</summary>
        public int Hash;

        /// <summary>The canonical-order key, or <see langword="null"/> when none was built.</summary>
        public ExpressionCanonicalOrder.KeyNode? Key;

        /// <summary>The lexical scope snapshot <see cref="Key"/> was built under.</summary>
        public ParameterExpression[][]? KeyScopes;

        /// <summary>The canonicalization state recorded by <see cref="StateOwner"/>.</summary>
        public CanonicalizationState State;

        /// <summary>The simplifier instance that recorded <see cref="State"/>, or <see langword="null"/>.</summary>
        public object? StateOwner;
    }

    /// <summary>Looks up the metadata stored for <paramref name="key"/>.</summary>
    /// <param name="key">The node.</param>
    /// <param name="entry">The stored metadata, when found.</param>
    /// <returns><see langword="true"/> when the node is stored.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(Expression key, out Entry entry)
    {
        int index = Find(key);
        if (index < 0)
        {
            entry = default;
            return false;
        }

        entry = _values[index];
        return true;
    }

    /// <summary>Stores the raw structural hash of <paramref name="key"/>.</summary>
    /// <param name="key">The node.</param>
    /// <param name="hash">Its hash.</param>
    public void SetHash(Expression key, int hash)
    {
        ref Entry entry = ref GetOrAdd(key);
        entry.Hash = hash;
        entry.HasHash = true;
    }

    /// <summary>Stores the canonical-order key of <paramref name="key"/> built under <paramref name="scopes"/>.</summary>
    /// <param name="key">The node.</param>
    /// <param name="orderKey">Its key.</param>
    /// <param name="scopes">The snapshot the key was built under.</param>
    public void SetKey(Expression key, ExpressionCanonicalOrder.KeyNode orderKey, ParameterExpression[][] scopes)
    {
        ref Entry entry = ref GetOrAdd(key);
        entry.Key = orderKey;
        entry.KeyScopes = scopes;
    }

    /// <summary>Stores the canonicalization state of <paramref name="key"/> recorded by <paramref name="owner"/>.</summary>
    /// <param name="key">The node.</param>
    /// <param name="state">The state.</param>
    /// <param name="owner">The recording simplifier.</param>
    public void SetState(Expression key, CanonicalizationState state, object owner)
    {
        ref Entry entry = ref GetOrAdd(key);
        entry.State = state;
        entry.StateOwner = owner;
    }

    /// <summary>
    /// Whether two lexical scope snapshots hold the very same scope frames, in the same order: a key built under one is the
    /// key the other would build, since a key reads the frames only (by position and parameter identity).
    /// </summary>
    /// <param name="x">The first snapshot.</param>
    /// <param name="y">The second snapshot.</param>
    /// <returns><see langword="true"/> for the same frames.</returns>
    /// <remarks>Conservative: two distinct frame arrays with the same parameters compare different, which only costs a rebuild.</remarks>
    public static bool SameScopes(ParameterExpression[][] x, ParameterExpression[][] y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x.Length != y.Length) return false;
        for (int i = 0; i < x.Length; i++)
        {
            if (!ReferenceEquals(x[i], y[i])) return false;
        }

        return true;
    }

    /// <summary>Finds the slot of <paramref name="key"/>.</summary>
    /// <param name="key">The node.</param>
    /// <returns>Its slot, or <c>-1</c> when it is not stored.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Find(Expression key)
    {
        Expression?[] keys = _keys;
        int mask = keys.Length - 1;
        for (int index = Slot(key, mask); ; index = (index + 1) & mask)
        {
            Expression? stored = keys[index];
            if (ReferenceEquals(stored, key)) return index;
            if (stored is null) return -1;
        }
    }

    /// <summary>Returns the entry of <paramref name="key"/>, adding an empty one when it is not stored yet.</summary>
    /// <param name="key">The node.</param>
    /// <returns>A reference to its entry, valid until the next addition.</returns>
    private ref Entry GetOrAdd(Expression key)
    {
        // An update of a stored node never grows the table: only a new key counts against the load factor.
        int found = Find(key);
        if (found >= 0) return ref _values[found];

        // Keeps the load factor at most one half, so probe sequences stay short and always reach an empty slot.
        if ((_count + 1) * 2 > _keys.Length) Grow();
        Expression?[] keys = _keys;
        int mask = keys.Length - 1;
        for (int index = Slot(key, mask); ; index = (index + 1) & mask)
        {
            Expression? stored = keys[index];
            if (stored is null)
            {
                keys[index] = key;
                _count++;
                return ref _values[index];
            }

            if (ReferenceEquals(stored, key)) return ref _values[index];
        }
    }

    /// <summary>Doubles the capacity and reinserts every key.</summary>
    private void Grow()
    {
        Expression?[] oldKeys = _keys;
        Entry[] oldValues = _values;
        var keys = new Expression?[oldKeys.Length * 2];
        var values = new Entry[keys.Length];
        int mask = keys.Length - 1;
        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldKeys[i] is not { } key) continue;
            int index = Slot(key, mask);
            while (keys[index] is not null) index = (index + 1) & mask;
            keys[index] = key;
            values[index] = oldValues[i];
        }

        _keys = keys;
        _values = values;
    }

    /// <summary>Computes the first probed slot of <paramref name="key"/> from its identity hash (Fibonacci hashing).</summary>
    /// <param name="key">The node.</param>
    /// <param name="mask">The capacity minus one.</param>
    /// <returns>The slot index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Slot(Expression key, int mask)
        => (int)((uint)(RuntimeHelpers.GetHashCode(key) * -1640531527) >> 7) & mask;
}
