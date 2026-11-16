using System.Collections.Generic;
using System.Linq.Expressions;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// What a canonicalization of <see cref="ExpressionSimplifier"/> may assume about an expression it receives (mission C3a, see
/// <c>docs/expressions/SimplifierFlatten.md</c> §14). The states are ordered by strength; a weaker state is always a correct
/// answer, so a state that cannot be proven is <see cref="Unknown"/>.
/// </summary>
internal enum CanonicalizationState : byte
{
    /// <summary>Nothing is known about the transformations that may still apply to the expression.</summary>
    Unknown = 0,

    /// <summary>
    /// The expression is the result of the engine's treatment of a node by this simplifier instance: the rules and the
    /// finalization of that node have run to their end. This is not a fixed point: simplifying the result again, or a parent
    /// rewriting it, may still change it.
    /// </summary>
    LocallySimplified = 1,

    /// <summary>
    /// Simplifying the expression again with this simplifier instance gives a structurally equal expression
    /// (<c>S(e) = e</c>, compared by <see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/>). Proven, in
    /// this version, for the leaves the built-in simplifier never rewrites only: parameters and constants.
    /// </summary>
    Canonical = 2,
}

/// <summary>
/// The enriched description of a term handed to a canonicalization (mission C3a): the term itself, its raw structural hash
/// (<see cref="ExpressionComparer.StructuralHashRaw(Expression?)"/>), its canonical-order key under the current lexical scopes
/// and its canonicalization state. Data only.
/// </summary>
/// <param name="Expression">The term.</param>
/// <param name="StructuralHash">
/// Its raw structural hash: two structurally equal terms have the same hash; two terms with the same hash are not
/// necessarily equal, so a hash match must be confirmed by <see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/>.
/// </param>
/// <param name="Key">Its canonical-order key, built under the lexical scopes enclosing the canonicalization.</param>
/// <param name="State">What this simplifier instance may assume about further simplifications of the term.</param>
internal readonly record struct SimplifiedExpressionInfo(
    Expression Expression,
    int StructuralHash,
    ExpressionCanonicalOrder.KeyNode Key,
    CanonicalizationState State);

/// <summary>
/// Opt-in counters of the metadata reuse of one thread (mission C3a): audits, tests and benchmarks only. Every counter
/// counts calls of the metadata accessors of <see cref="ExpressionSimplifier"/> made while the instance is installed.
/// </summary>
internal sealed class SimplificationMetadataStatistics
{
    /// <summary>Raw structural hashes computed by a walk.</summary>
    public long HashComputations;

    /// <summary>Raw structural hashes read back from the operation's table.</summary>
    public long HashReuses;

    /// <summary>Canonical-order keys built by a walk.</summary>
    public long KeyBuilds;

    /// <summary>Canonical-order keys read back from the operation's table.</summary>
    public long KeyReuses;
}

public partial class ExpressionSimplifier
{
    /// <summary>The opt-in metadata counters of this thread (<see cref="MetadataStatistics"/>).</summary>
    [System.ThreadStatic]
    private static SimplificationMetadataStatistics? t_metadataStatistics;

    /// <summary>
    /// Gets or sets the metadata counters of this thread: audits and benchmarks only. While <see langword="null"/> (the
    /// default), the accessors read this field once per call and record nothing.
    /// </summary>
    internal static SimplificationMetadataStatistics? MetadataStatistics
    {
        get => t_metadataStatistics;
        set => t_metadataStatistics = value;
    }

    /// <summary>
    /// Describes <paramref name="term"/> for a canonicalization running under <paramref name="scopes"/>: its hash, key and
    /// state, each read back from the operation's table when a canonicalization of the running public operation already
    /// computed it, otherwise computed once and stored.
    /// </summary>
    /// <param name="term">A prepared term.</param>
    /// <param name="scopes">The lexical scope snapshot of the canonicalization (<see cref="CaptureLexicalScopeSnapshot"/>).</param>
    /// <returns>The description.</returns>
    internal SimplifiedExpressionInfo DescribeTerm(Expression term, ParameterExpression[][] scopes)
        => new(term, GetStructuralHash(term), GetCanonicalKey(term, scopes), GetCanonicalizationState(term));

    /// <summary>
    /// Gets the raw structural hash of <paramref name="expression"/> (<see cref="ExpressionComparer.StructuralHashRaw(Expression?)"/>),
    /// reusing the value a canonicalization of the running public operation already computed for the same node.
    /// </summary>
    /// <param name="expression">The node.</param>
    /// <param name="record">Whether a computed hash is stored for the next canonicalizations (see <see cref="CanonicalizeAdditiveTerms"/>).</param>
    /// <returns>Its raw structural hash.</returns>
    /// <remarks>
    /// The raw hash is a function of the node alone, so the reuse is exact (see <see cref="ExpressionMetadataTable"/>). Outside
    /// an operation (direct calls from tests) nothing is stored.
    /// </remarks>
    internal static int GetStructuralHash(Expression expression, bool record = true)
    {
        SimplificationMetadataStatistics? statistics = t_metadataStatistics;
        ExpressionResourceBudget.Session? session = ExpressionResourceBudget.ActiveSession;
        if (session?.SimplificationMetadata is { } table && table.TryGetValue(expression, out ExpressionMetadataTable.Entry entry) && entry.HasHash)
        {
            if (statistics is not null) statistics.HashReuses++;
            return entry.Hash;
        }

        int hash = ExpressionComparer.StructuralHashRaw(expression);
        if (statistics is not null) statistics.HashComputations++;
        if (record && session is not null) (session.SimplificationMetadata ??= new ExpressionMetadataTable()).SetHash(expression, hash);
        return hash;
    }

    /// <summary>
    /// Gets the canonical-order key of <paramref name="expression"/> under <paramref name="scopes"/>
    /// (<see cref="ExpressionCanonicalOrder.BuildKeyFromSnapshot(Expression?, ParameterExpression[][])"/>), reusing the key a
    /// canonicalization of the running public operation already built for the same node under the same scope frames.
    /// </summary>
    /// <param name="expression">The node.</param>
    /// <param name="scopes">The lexical scope snapshot of the canonicalization.</param>
    /// <param name="record">Whether a built key is stored for the next canonicalizations (see <see cref="CanonicalizeAdditiveTerms"/>).</param>
    /// <returns>Its key.</returns>
    /// <remarks>
    /// A key depends on the enclosing scopes, so a stored key is reused only under the same frames
    /// (<see cref="ExpressionMetadataTable.SameScopes"/>); a node shared by two lexical contexts is keyed once per context,
    /// the latest key replacing the previous one. The reused key is the very instance built before, which is the key a new
    /// build would give: keys are immutable and compare by value (an identical instance only shortcuts the comparison).
    /// </remarks>
    internal static ExpressionCanonicalOrder.KeyNode GetCanonicalKey(Expression expression, ParameterExpression[][] scopes, bool record = true)
    {
        SimplificationMetadataStatistics? statistics = t_metadataStatistics;
        ExpressionResourceBudget.Session? session = ExpressionResourceBudget.ActiveSession;
        if (session?.SimplificationMetadata is { } table
            && table.TryGetValue(expression, out ExpressionMetadataTable.Entry entry)
            && entry.Key is { } stored
            && ExpressionMetadataTable.SameScopes(entry.KeyScopes!, scopes))
        {
            if (statistics is not null) statistics.KeyReuses++;
            return stored;
        }

        ExpressionCanonicalOrder.KeyNode key = ExpressionCanonicalOrder.BuildKeyFromSnapshot(expression, scopes);
        if (statistics is not null) statistics.KeyBuilds++;
        if (record && session is not null) (session.SimplificationMetadata ??= new ExpressionMetadataTable()).SetKey(expression, key, scopes);
        return key;
    }

    /// <summary>Gets the canonical-order keys of <paramref name="expressions"/>, in order, through <see cref="GetCanonicalKey"/>.</summary>
    /// <param name="expressions">The nodes (the arguments of a function-like term).</param>
    /// <param name="scopes">The lexical scope snapshot of the canonicalization.</param>
    /// <param name="record">Whether built keys are stored.</param>
    /// <returns>Their keys; an empty array for no node.</returns>
    /// <remarks>
    /// Equal to <see cref="ExpressionCanonicalOrder.BuildKeysFromSnapshot(IReadOnlyList{Expression}, ParameterExpression[][])"/>,
    /// which builds every key from the same snapshot with its own label numbering, as one build per node does.
    /// </remarks>
    private static ExpressionCanonicalOrder.KeyNode[] GetCanonicalKeys(IReadOnlyList<Expression> expressions, ParameterExpression[][] scopes, bool record)
    {
        if (expressions.Count == 0) return [];
        var keys = new ExpressionCanonicalOrder.KeyNode[expressions.Count];
        for (int i = 0; i < keys.Length; i++) keys[i] = GetCanonicalKey(expressions[i], scopes, record);
        return keys;
    }

    /// <summary>
    /// Gets what this instance may assume about further simplifications of <paramref name="expression"/>
    /// (<see cref="CanonicalizationState"/>): <see cref="CanonicalizationState.Canonical"/> for a leaf of the built-in type,
    /// otherwise the state this very instance recorded for the node during the running public operation, otherwise
    /// <see cref="CanonicalizationState.Unknown"/>.
    /// </summary>
    /// <param name="expression">The node.</param>
    /// <returns>Its state.</returns>
    /// <remarks>
    /// <para>
    /// <b>Leaves.</b> The built-in simplifier has no rule on a parameter or a constant, its finalization returns a structurally
    /// equal node and the <c>NaN</c> fold only rewrites binary nodes, so <c>S(e) = e</c> holds for them under every policy. A
    /// derived simplifier may declare rules on them: its leaves stay <see cref="CanonicalizationState.Unknown"/>.
    /// </para>
    /// <para>
    /// <b>Recorded states.</b> A state is the claim of the instance that recorded it (<see cref="RecordLocallySimplified"/>),
    /// under its own rules and policy: another instance, even of the same policy, reads <see cref="CanonicalizationState.Unknown"/>.
    /// Equal hashes of two successive results never make a state stronger.
    /// </para>
    /// </remarks>
    internal CanonicalizationState GetCanonicalizationState(Expression expression)
    {
        if (expression is ParameterExpression or ConstantExpression && GetType() == typeof(ExpressionSimplifier)) return CanonicalizationState.Canonical;
        return ExpressionResourceBudget.ActiveSession?.SimplificationMetadata is { } table
            && table.TryGetValue(expression, out ExpressionMetadataTable.Entry entry)
            && ReferenceEquals(entry.StateOwner, this)
                ? entry.State
                : CanonicalizationState.Unknown;
    }

    /// <summary>
    /// Records that <paramref name="expression"/> is the result of this instance's treatment of a node
    /// (<see cref="CanonicalizationState.LocallySimplified"/>), unless a stronger state is already known for it. The state
    /// is recorded only in a table the operation already holds (a canonicalization of the operation created it): creating a
    /// table for a state no canonicalization may read would cost every simple simplification an allocation. A state that is
    /// not recorded reads <see cref="CanonicalizationState.Unknown"/>, which is always a correct answer.
    /// </summary>
    /// <param name="expression">A result of <see cref="PrepareExpression"/> of this instance.</param>
    private void RecordLocallySimplified(Expression expression)
    {
        if (expression is ParameterExpression or ConstantExpression || ExpressionResourceBudget.ActiveSession is not { } session) return;
        if (session.SimplificationMetadata is not { } table) return;
        if (table.TryGetValue(expression, out ExpressionMetadataTable.Entry entry) && ReferenceEquals(entry.StateOwner, this) && entry.State >= CanonicalizationState.LocallySimplified) return;
        table.SetState(expression, CanonicalizationState.LocallySimplified, this);
    }
}
