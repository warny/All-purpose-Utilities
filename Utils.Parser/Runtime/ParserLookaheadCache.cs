using System.Runtime.CompilerServices;

namespace Utils.Parser.Runtime;

/// <summary>
/// Caches lightweight scheduled-alternative look-ahead observations for the current parse execution.
/// Entries are syntax-oriented probe metadata only and are independent from semantic runtime state,
/// parser action side effects, replay frames, and rollback mechanisms.
/// Cached values are advisory: they may inform conservative shortcut rejection, but they never
/// own parse acceptance and never replace authoritative parse execution in <see cref="ParserEngine"/>.
/// </summary>
/// <remarks>
/// The storage is compact but exact. It behaves as one dictionary from <see cref="ParserLookaheadKey"/> (compared field
/// by field, rule names and cursor kinds ordinally) to the first <see cref="ParserLookaheadProbeResult"/> stored for it:
/// membership, duplicate detection, the first value kept and therefore the
/// <see cref="ParserResourceBudget.ChargeRetainedState"/> charges are those of that dictionary. A stored result is
/// returned with the same kind, the same token-name and token-text instances and the same expected-token-name
/// collection instance.
/// <list type="bullet">
/// <item>A key whose fields fit (see <see cref="PackedKey"/>) is packed into 64 bits; its rule name is replaced by an index
/// into a table of the rule names this cache has stored, and its cursor kind by the index of one of the
/// <see cref="ScheduledAlternativeCursorKinds"/> values.</item>
/// <item>A packed entry stores its kind, its expected-token-name collection and the index of its token snapshot (token
/// rule name and text) in a table shared by the entries of the same origin position, which all observe the same
/// token.</item>
/// <item>Any key or result that cannot be represented that way (unknown cursor kind, out-of-range index, <see langword="null"/>
/// name, table full, ...) is stored unchanged in an overflow dictionary of the historical form. The partition of a key
/// depends on the key and on the tables only, and a key is always looked up in both stores, so it is found wherever it
/// was stored.</item>
/// </list>
/// No hash stands for an identity, nothing is evicted, and every table belongs to this cache and is emptied by
/// <see cref="Clear"/>.
/// </remarks>
internal sealed class ParserLookaheadCache
{
    /// <summary>Number of rule names the rule table can index (10 bits of <see cref="PackedKey"/>).</summary>
    private const int MaxRuleNames = 1 << 10;

    /// <summary>Number of token snapshots the snapshot table can index (24 bits of <see cref="PackedValue"/>).</summary>
    private const int MaxTokenSnapshots = 1 << 24;

    /// <summary>The cursor kinds a packed key can encode, indexed by their 2-bit code.</summary>
    private static readonly string[] PackedCursorKinds =
    [
        ScheduledAlternativeCursorKinds.RuleRoot,
        ScheduledAlternativeCursorKinds.LeftRecursiveSeed,
        ScheduledAlternativeCursorKinds.Alternation,
        ScheduledAlternativeCursorKinds.AlternativeRoot,
    ];

    /// <summary>Entries whose key and result fit the packed representation.</summary>
    private readonly Dictionary<PackedKey, PackedValue> _entries = [];

    /// <summary>Entries whose key or result does not fit the packed representation, stored unchanged.</summary>
    private readonly Dictionary<ParserLookaheadKey, ParserLookaheadProbeResult> _overflow = [];

    /// <summary>Index of every rule name used by a packed key, compared ordinally like <see cref="ParserLookaheadKey"/>.</summary>
    private readonly Dictionary<string, int> _ruleIndexes = new(StringComparer.Ordinal);

    /// <summary>The rule names of <see cref="_ruleIndexes"/>, by index (used to enumerate the entries).</summary>
    private readonly List<string> _ruleNames = [];

    /// <summary>The token snapshots (token rule name and text instances) of the packed entries.</summary>
    private readonly List<TokenSnapshot> _tokenSnapshots = [];

    /// <summary>The first token snapshot recorded for each origin position.</summary>
    private readonly Dictionary<int, int> _tokenSnapshotByOrigin = [];

    /// <summary>The rule name of the last rule-table lookup, compared by reference to skip hashing a repeated name.</summary>
    private string? _lastRuleName;

    /// <summary>The rule index of <see cref="_lastRuleName"/>.</summary>
    private int _lastRuleIndex;

    /// <summary>The origin position of the last token-snapshot lookup, or <see langword="null"/> after <see cref="Clear"/>.</summary>
    private int? _lastSnapshotOrigin;

    /// <summary>The token-snapshot index recorded for <see cref="_lastSnapshotOrigin"/>.</summary>
    private int _lastSnapshotIndex;

    /// <summary>Gets the number of cached look-ahead observations.</summary>
    internal int Count => _entries.Count + _overflow.Count;

    /// <summary>Gets the number of observations stored in the overflow dictionary (diagnostics of the compact representation).</summary>
    internal int OverflowCount => _overflow.Count;

    /// <summary>
    /// Clears all cached look-ahead observations and the tables that index them, releasing the capacity of a collection
    /// that grew large (<see cref="ParserStateRetention"/>). No rule name or token string stays referenced.
    /// </summary>
    public void Clear()
    {
        ParserStateRetention.Clear(_entries);
        ParserStateRetention.Clear(_overflow);
        ParserStateRetention.Clear(_ruleIndexes);
        ParserStateRetention.Clear(_ruleNames);
        ParserStateRetention.Clear(_tokenSnapshots);
        ParserStateRetention.Clear(_tokenSnapshotByOrigin);
        _lastRuleName = null;
        _lastRuleIndex = 0;
        _lastSnapshotOrigin = null;
        _lastSnapshotIndex = 0;
    }

    /// <summary>
    /// Tries to get a cached look-ahead observation for the provided key.
    /// </summary>
    public bool TryGet(ParserLookaheadKey key, out ParserLookaheadProbeResult result)
    {
        if (TryPackKey(key, register: false, out var packedKey) && _entries.TryGetValue(packedKey, out var packedValue))
        {
            result = Unpack(packedValue);
            return true;
        }

        if (_overflow.Count == 0)
        {
            result = default;
            return false;
        }

        return _overflow.TryGetValue(key, out result);
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
        // Duplicate detection reads both stores without changing any table, so a refused charge leaves the cache untouched.
        if (TryPackKey(key, register: false, out var packedKey) && _entries.ContainsKey(packedKey))
        {
            return false;
        }

        if (_overflow.Count != 0 && _overflow.ContainsKey(key))
        {
            return false;
        }

        budget?.ChargeRetainedState();
        if (TryPackKey(key, register: true, out packedKey) && TryPackValue(key.OriginPosition, result, out var packedValue))
        {
            _entries.Add(packedKey, packedValue);
        }
        else
        {
            _overflow.Add(key, result);
        }

        return true;
    }

    /// <summary>
    /// Enumerates the cached observations (diagnostics and tests), in no particular order. Keys are rebuilt from the
    /// tables, so a rule name or cursor kind is the instance the tables hold, which is equal to the stored one.
    /// </summary>
    /// <returns>The key and result of every cached observation.</returns>
    internal IEnumerable<KeyValuePair<ParserLookaheadKey, ParserLookaheadProbeResult>> EnumerateEntries()
    {
        foreach (var (packedKey, packedValue) in _entries)
        {
            yield return new(Unpack(packedKey), Unpack(packedValue));
        }

        foreach (var entry in _overflow)
        {
            yield return entry;
        }
    }

    /// <summary>
    /// Packs a key when its cursor kind is one of <see cref="PackedCursorKinds"/>, its indexes fit their bit fields and its
    /// rule name has (or, when <paramref name="register"/> is <see langword="true"/>, can get) an index in the rule table.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="register">Whether a rule name missing from the rule table is added to it.</param>
    /// <param name="packedKey">Receives the packed key.</param>
    /// <returns><see langword="true"/> when the key is packed.</returns>
    private bool TryPackKey(ParserLookaheadKey key, bool register, out PackedKey packedKey)
    {
        packedKey = default;
        int cursorKind = Array.IndexOf(PackedCursorKinds, key.CursorKind);
        if (cursorKind < 0
            || (uint)key.AlternativeIndex > PackedKey.MaxAlternativeIndex
            || (uint)key.MinimumPrecedence > PackedKey.MaxPrecedence
            || (uint)(key.CursorIndex + 1) > PackedKey.MaxCursorIndex + 1
            || key.RuleName is null)
        {
            return false;
        }

        int ruleIndex;
        if (ReferenceEquals(key.RuleName, _lastRuleName))
        {
            ruleIndex = _lastRuleIndex;
        }
        else if (!_ruleIndexes.TryGetValue(key.RuleName, out ruleIndex))
        {
            if (!register || _ruleNames.Count >= MaxRuleNames)
            {
                return false;
            }

            ruleIndex = _ruleNames.Count;
            _ruleIndexes.Add(key.RuleName, ruleIndex);
            _ruleNames.Add(key.RuleName);
        }

        _lastRuleName = key.RuleName;
        _lastRuleIndex = ruleIndex;
        packedKey = PackedKey.Create(key.OriginPosition, key.AlternativeIndex, ruleIndex, cursorKind, key.MinimumPrecedence, key.CursorIndex);
        return true;
    }

    /// <summary>
    /// Packs a result whose kind fits 8 bits, sharing the token snapshot of its origin position when the token strings are
    /// the same instances and recording a new snapshot otherwise.
    /// </summary>
    /// <param name="originPosition">The origin position of the key.</param>
    /// <param name="result">The result.</param>
    /// <param name="packedValue">Receives the packed result.</param>
    /// <returns><see langword="true"/> when the result is packed.</returns>
    private bool TryPackValue(int originPosition, ParserLookaheadProbeResult result, out PackedValue packedValue)
    {
        packedValue = default;
        if ((uint)result.Kind > byte.MaxValue)
        {
            return false;
        }

        int snapshotIndex;
        if (_lastSnapshotOrigin == originPosition)
        {
            snapshotIndex = _lastSnapshotIndex;
        }
        else if (!_tokenSnapshotByOrigin.TryGetValue(originPosition, out snapshotIndex))
        {
            snapshotIndex = -1;
        }

        if (snapshotIndex < 0 || !_tokenSnapshots[snapshotIndex].Holds(result.TokenRuleName, result.TokenText))
        {
            if (_tokenSnapshots.Count >= MaxTokenSnapshots)
            {
                return false;
            }

            bool firstOfOrigin = snapshotIndex < 0;
            snapshotIndex = _tokenSnapshots.Count;
            _tokenSnapshots.Add(new TokenSnapshot(result.TokenRuleName, result.TokenText));
            if (!firstOfOrigin)
            {
                // A different token at a known origin keeps its own snapshot; the origin keeps its first one.
                packedValue = new PackedValue(result.Kind, snapshotIndex, result.ExpectedTokenNames);
                return true;
            }

            _tokenSnapshotByOrigin.Add(originPosition, snapshotIndex);
        }

        _lastSnapshotOrigin = originPosition;
        _lastSnapshotIndex = snapshotIndex;
        packedValue = new PackedValue(result.Kind, snapshotIndex, result.ExpectedTokenNames);
        return true;
    }

    /// <summary>Rebuilds the result of a packed entry, with the stored string and collection instances.</summary>
    /// <param name="packedValue">The packed result.</param>
    /// <returns>The result.</returns>
    private ParserLookaheadProbeResult Unpack(PackedValue packedValue)
    {
        var snapshot = _tokenSnapshots[packedValue.TokenSnapshotIndex];
        return ParserLookaheadProbeResult.FromCapturedSnapshot(packedValue.Kind, snapshot.TokenRuleName, snapshot.TokenText, packedValue.ExpectedTokenNames);
    }

    /// <summary>Rebuilds the key of a packed entry from the tables.</summary>
    /// <param name="packedKey">The packed key.</param>
    /// <returns>The key.</returns>
    private ParserLookaheadKey Unpack(PackedKey packedKey)
        => new(
            _ruleNames[packedKey.RuleIndex],
            packedKey.OriginPosition,
            packedKey.AlternativeIndex,
            packedKey.MinimumPrecedence,
            PackedCursorKinds[packedKey.CursorKind],
            packedKey.CursorIndex);

    /// <summary>
    /// A look-ahead key packed into 64 bits: origin position (bits 0-31, any value), alternative index (bits 32-39,
    /// 0-255), rule index (bits 40-49), cursor kind code (bits 50-51), minimum precedence (bits 52-57, 0-63) and cursor
    /// index plus one (bits 58-63, cursor index -1 to 62). The fields are independent, so equality of packed keys is
    /// equality of the unpacked fields.
    /// </summary>
    /// <param name="Bits">The packed fields.</param>
    private readonly record struct PackedKey(ulong Bits)
    {
        /// <summary>Largest alternative index a packed key encodes.</summary>
        public const uint MaxAlternativeIndex = 0xFF;

        /// <summary>Largest minimum precedence a packed key encodes.</summary>
        public const uint MaxPrecedence = 0x3F;

        /// <summary>Largest cursor index a packed key encodes (the smallest is -1).</summary>
        public const uint MaxCursorIndex = 0x3E;

        /// <summary>Gets the origin position.</summary>
        public int OriginPosition => (int)(uint)Bits;

        /// <summary>Gets the alternative index.</summary>
        public int AlternativeIndex => (int)((Bits >> 32) & 0xFF);

        /// <summary>Gets the rule index.</summary>
        public int RuleIndex => (int)((Bits >> 40) & 0x3FF);

        /// <summary>Gets the cursor kind code.</summary>
        public int CursorKind => (int)((Bits >> 50) & 0x3);

        /// <summary>Gets the minimum precedence.</summary>
        public int MinimumPrecedence => (int)((Bits >> 52) & 0x3F);

        /// <summary>Gets the cursor index.</summary>
        public int CursorIndex => (int)(Bits >> 58) - 1;

        /// <summary>Packs fields already checked to fit their ranges.</summary>
        /// <param name="originPosition">The origin position.</param>
        /// <param name="alternativeIndex">The alternative index (0-255).</param>
        /// <param name="ruleIndex">The rule index (0-1023).</param>
        /// <param name="cursorKind">The cursor kind code (0-3).</param>
        /// <param name="minimumPrecedence">The minimum precedence (0-63).</param>
        /// <param name="cursorIndex">The cursor index (-1 to 62).</param>
        /// <returns>The packed key.</returns>
        public static PackedKey Create(int originPosition, int alternativeIndex, int ruleIndex, int cursorKind, int minimumPrecedence, int cursorIndex)
            => new((uint)originPosition
                | (ulong)(uint)alternativeIndex << 32
                | (ulong)(uint)ruleIndex << 40
                | (ulong)(uint)cursorKind << 50
                | (ulong)(uint)minimumPrecedence << 52
                | (ulong)(uint)(cursorIndex + 1) << 58);

        /// <inheritdoc />
        public bool Equals(PackedKey other) => Bits == other.Bits;

        /// <summary>Mixes all 64 bits (Fibonacci hashing), so keys that differ only in the high fields spread over the buckets.</summary>
        /// <returns>The hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => (int)((Bits * 0x9E3779B97F4A7C15UL) >> 32);
    }

    /// <summary>A packed result: the expected-token-name collection instance, the kind (8 bits) and the token-snapshot index (24 bits).</summary>
    private readonly struct PackedValue
    {
        /// <summary>The kind in bits 0-7 and the token-snapshot index in bits 8-31.</summary>
        private readonly uint _bits;

        /// <summary>Initializes a packed result whose kind and snapshot index were checked to fit.</summary>
        /// <param name="kind">The kind.</param>
        /// <param name="tokenSnapshotIndex">The token-snapshot index.</param>
        /// <param name="expectedTokenNames">The expected-token-name collection instance of the result.</param>
        public PackedValue(ParserLookaheadProbeKind kind, int tokenSnapshotIndex, IReadOnlyList<string>? expectedTokenNames)
        {
            _bits = (uint)kind | (uint)tokenSnapshotIndex << 8;
            ExpectedTokenNames = expectedTokenNames;
        }

        /// <summary>Gets the expected-token-name collection instance of the result.</summary>
        public IReadOnlyList<string>? ExpectedTokenNames { get; }

        /// <summary>Gets the kind.</summary>
        public ParserLookaheadProbeKind Kind => (ParserLookaheadProbeKind)(_bits & 0xFF);

        /// <summary>Gets the token-snapshot index.</summary>
        public int TokenSnapshotIndex => (int)(_bits >> 8);
    }

    /// <summary>The token rule name and token text instances shared by packed results.</summary>
    /// <param name="TokenRuleName">The token rule name instance.</param>
    /// <param name="TokenText">The token text instance.</param>
    private readonly record struct TokenSnapshot(string? TokenRuleName, string? TokenText)
    {
        /// <summary>Returns whether this snapshot holds exactly these instances.</summary>
        /// <param name="tokenRuleName">The token rule name.</param>
        /// <param name="tokenText">The token text.</param>
        /// <returns><see langword="true"/> when both instances are the ones of this snapshot.</returns>
        public bool Holds(string? tokenRuleName, string? tokenText)
            => ReferenceEquals(TokenRuleName, tokenRuleName) && ReferenceEquals(TokenText, tokenText);
    }
}
