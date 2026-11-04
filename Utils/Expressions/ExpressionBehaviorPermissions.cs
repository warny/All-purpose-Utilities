namespace Utils.Expressions;

/// <summary>
/// How far a simplification policy may rely on the classification of <see cref="ExpressionBehaviorAnalysis"/> (S5 P18
/// Architecture A, preparation of the future <c>ExpressionSimplifierOptions</c>). Not consumed yet: the simplifier keeps the
/// strict gate of <see cref="ExpressionEffectAnalysis"/>.
/// </summary>
internal enum ExpressionTrustPolicy
{
    /// <summary>Only verified behaviors count: a classification that relies on a declaration is treated as unknown code.</summary>
    Verified,

    /// <summary>
    /// <see cref="DeterministicAttribute"/> declarations are trusted; exceptions of declared code still forbid dropping or
    /// moving it. Unknown code stays unknown: this policy is one axis of a future option, independent of an
    /// <c>AggressiveSimplification</c> that would rewrite unknown code, and of the exception-preservation policy.
    /// </summary>
    TrustDeclarations,
}

/// <summary>
/// The rewrite permissions an evaluation's <see cref="ExpressionBehavior"/> grants (S5 P18 Architecture A): dropping it,
/// performing it a different number of times, or moving it. Same meaning as the permissions of
/// <see cref="ExpressionEffectAnalysis"/>, extended to determinism and declarations. Not consumed by any rule yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>Discard</b> (<c>t * 0 -&gt; 0</c>, <c>t - t -&gt; 0</c>): no side effect, no allocation, no exception. A nondeterministic
/// read without side effect (a clock) may be dropped.
/// </para>
/// <para>
/// <b>Repeat or merge</b> (<c>t + t -&gt; 2 * t</c>, factoring): deterministic, no side effect, no allocation. An exception is
/// allowed: a deterministic evaluation that throws throws on its first evaluation either way.
/// </para>
/// <para>
/// <b>Reorder</b> (canonical ordering): deterministic, no side effect, no allocation, no exception (the order of two exceptions,
/// or of an exception and a read, could change).
/// </para>
/// <para>
/// <b>These permissions are local, not contextual.</b> Reading mutable state is allowed by every permission, so a permission
/// alone never justifies a rewrite across other evaluations: <see cref="CanRepeat"/> only means that two evaluations in the
/// same state agree, and <see cref="CanReorder"/> only that the evaluation is movable among other movable ones. A caller that
/// merges, repeats or moves an evaluation that reads mutable state must also prove that no evaluation with
/// <see cref="ExpressionBehaviors.SideEffects"/> (or unknown code) runs between the merged occurrences or across the move,
/// exactly as <c>SplitPinnedTerms</c> does with <see cref="ExpressionEffectAnalysis"/>.
/// </para>
/// </remarks>
internal static class ExpressionBehaviorPermissions
{
    /// <summary>Returns the flags of <paramref name="behavior"/> under <paramref name="policy"/>.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy.</param>
    /// <returns>The flags, widened to unknown code when a declaration is not trusted.</returns>
    internal static ExpressionBehaviors Effective(ExpressionBehavior behavior, ExpressionTrustPolicy policy)
        => policy == ExpressionTrustPolicy.Verified && behavior.ReliesOnDeclarations
            ? behavior.Flags | ExpressionBehaviors.UnknownCode
            : behavior.Flags;

    /// <summary>Whether the evaluation can be dropped.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy.</param>
    /// <returns><see langword="true"/> when dropping it is unobservable.</returns>
    internal static bool CanDiscard(ExpressionBehavior behavior, ExpressionTrustPolicy policy)
        => (Effective(behavior, policy) & (ExpressionBehaviors.SideEffects | ExpressionBehaviors.Allocates | ExpressionBehaviors.MayThrow | ExpressionBehaviors.Unknown)) == 0;

    /// <summary>
    /// Whether the evaluation can be performed a different number of times (at least once), provided no side effect runs
    /// between the occurrences when it reads mutable state (see the remarks of the class).
    /// </summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy.</param>
    /// <returns><see langword="true"/> when repeating or merging it is unobservable in an unchanged state.</returns>
    internal static bool CanRepeat(ExpressionBehavior behavior, ExpressionTrustPolicy policy)
        => (Effective(behavior, policy) & (ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.SideEffects | ExpressionBehaviors.Allocates | ExpressionBehaviors.Unknown)) == 0;

    /// <summary>Whether the evaluation can be moved relative to other movable evaluations.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy.</param>
    /// <returns><see langword="true"/> when moving it is unobservable.</returns>
    internal static bool CanReorder(ExpressionBehavior behavior, ExpressionTrustPolicy policy)
        => (Effective(behavior, policy) & (ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.SideEffects | ExpressionBehaviors.Allocates
            | ExpressionBehaviors.MayThrow | ExpressionBehaviors.Unknown)) == 0;

    /// <summary>
    /// Maps a behavior to the effects of <see cref="ExpressionEffectAnalysis"/> under <paramref name="policy"/>, the bridge a
    /// future policy would use to feed the existing gate: exceptions become <see cref="ExpressionEffects.MayThrow"/>,
    /// side effects, nondeterminism and unknown code become <see cref="ExpressionEffects.Observable"/>.
    /// </summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy.</param>
    /// <returns>The equivalent effects.</returns>
    internal static ExpressionEffects ToEffects(ExpressionBehavior behavior, ExpressionTrustPolicy policy)
    {
        ExpressionBehaviors flags = Effective(behavior, policy);
        ExpressionEffects effects = ExpressionEffects.None;
        if ((flags & ExpressionBehaviors.ReadsMutableState) != 0) effects |= ExpressionEffects.ReadsMutableState;
        if ((flags & ExpressionBehaviors.MayThrow) != 0) effects |= ExpressionEffects.MayThrow;
        if ((flags & ExpressionBehaviors.Allocates) != 0) effects |= ExpressionEffects.Allocates;
        if ((flags & (ExpressionBehaviors.SideEffects | ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.Unknown)) != 0) effects |= ExpressionEffects.Observable;
        return effects;
    }
}
