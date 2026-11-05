namespace Utils.Expressions;

/// <summary>
/// How far a simplification policy may rely on the classification of <see cref="ExpressionBehaviorAnalysis"/> (S5 P18
/// Architecture A, exposed by Architecture B as <see cref="ExpressionSimplifierOptions.TrustPolicy"/>). Not consumed by any rule
/// yet: the simplifier keeps the strict gate of <see cref="ExpressionEffectAnalysis"/>.
/// </summary>
public enum ExpressionTrustPolicy
{
    /// <summary>
    /// Only verified behaviors count (the default): a classification that relies on a <see cref="DeterministicAttribute"/>
    /// declaration is treated as unknown code.
    /// </summary>
    Verified = 0,

    /// <summary>
    /// <see cref="DeterministicAttribute"/> declarations are trusted for what they promise (deterministic, no ambient state, no
    /// side effect); a declared method may still throw, allocate and read the state of its arguments. Unknown code stays
    /// unknown. Independent of <see cref="ExpressionSimplifierOptions.AggressiveSimplification"/> and of
    /// <see cref="ExpressionSimplifierOptions.ExceptionPreservation"/>.
    /// </summary>
    TrustDeclarations = 1,
}

/// <summary>
/// The rewrite permissions an evaluation's <see cref="ExpressionBehavior"/> grants (S5 P18 Architectures A and B): dropping it,
/// performing it a different number of times, or moving it, under an explicit policy. Same meaning as the permissions of
/// <see cref="ExpressionEffectAnalysis"/>, extended to determinism, declarations and the three policy axes of
/// <see cref="ExpressionSimplifierOptions"/>. Not consumed by any rule yet.
/// </summary>
/// <remarks>
/// <para>
/// Under the default policy (<see cref="ExpressionSimplifierOptions.Default"/>):
/// <b>Discard</b> (<c>t * 0 -&gt; 0</c>, <c>t - t -&gt; 0</c>): no side effect, no allocation, no exception. A nondeterministic
/// read without side effect (a clock) may be dropped.
/// <b>Repeat or merge</b> (<c>t + t -&gt; 2 * t</c>, factoring): deterministic, no side effect, no allocation. An exception is
/// allowed: a deterministic evaluation that throws throws on its first evaluation either way.
/// <b>Reorder</b> (canonical ordering): deterministic, no side effect, no allocation, no exception (the order of two exceptions,
/// or of an exception and a read, could change).
/// </para>
/// <para>
/// <b>Policy axes.</b> The trust policy only widens a declaration-based classification to unknown code (<see cref="Effective"/>).
/// <see cref="ExpressionSimplifierOptions.AggressiveSimplification"/> lifts the allocation obstacle of every permission and the
/// nondeterminism obstacle of repeating and reordering; a nondeterministic evaluation repeated under it must raise no preserved
/// exception (two evaluations could disagree on throwing). <see cref="ExpressionSimplifierOptions.ExceptionPreservation"/> lists
/// the exception flags that forbid dropping and reordering. Side effects and <see cref="ExpressionBehaviors.Unknown"/> forbid every
/// permission under every policy. Every condition is "a flag forbids", so the permissions are monotone: fewer flags, a trusting
/// policy, aggressive simplification or fewer preserved categories never remove a permission, and their opposites never add one.
/// </para>
/// <para>
/// <b>These permissions are local, not contextual.</b> Reading mutable state is allowed by every permission, so a permission
/// alone never justifies a rewrite across other evaluations: <see cref="CanRepeat(ExpressionBehavior, ExpressionSimplifierOptions)"/>
/// only means that two evaluations in the same state agree, and
/// <see cref="CanReorder(ExpressionBehavior, ExpressionSimplifierOptions)"/> only that the evaluation is movable among other movable
/// ones. A caller that merges, repeats or moves an evaluation that reads mutable state must also prove that no evaluation with
/// <see cref="ExpressionBehaviors.SideEffects"/> (or unknown code) runs between the merged occurrences or across the move,
/// exactly as <c>SplitPinnedTerms</c> does with <see cref="ExpressionEffectAnalysis"/>. No policy relaxes that condition.
/// </para>
/// </remarks>
internal static class ExpressionBehaviorPermissions
{
    /// <summary>The flags that forbid every permission under every policy.</summary>
    private const ExpressionBehaviors Imperative = ExpressionBehaviors.SideEffects | ExpressionBehaviors.Unknown;

    /// <summary>Returns the flags of <paramref name="behavior"/> under <paramref name="policy"/>.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy; any value but <see cref="ExpressionTrustPolicy.TrustDeclarations"/> is treated as verified.</param>
    /// <returns>The flags, widened to unknown code when a declaration is not trusted.</returns>
    internal static ExpressionBehaviors Effective(ExpressionBehavior behavior, ExpressionTrustPolicy policy)
        => policy != ExpressionTrustPolicy.TrustDeclarations && behavior.ReliesOnDeclarations
            ? behavior.Flags | ExpressionBehaviors.UnknownCode
            : behavior.Flags;

    /// <summary>Maps preserved exception categories to the behavior flags they forbid.</summary>
    /// <param name="preservation">The preserved categories; flags outside <see cref="ExpressionExceptionPreservation.All"/> are ignored.</param>
    /// <returns>The exception flags that must stay observable.</returns>
    internal static ExpressionBehaviors PreservedExceptions(ExpressionExceptionPreservation preservation)
    {
        ExpressionBehaviors flags = ExpressionBehaviors.None;
        if ((preservation & ExpressionExceptionPreservation.Arithmetic) != 0) flags |= ExpressionBehaviors.MayThrowArithmetic;
        if ((preservation & ExpressionExceptionPreservation.Runtime) != 0) flags |= ExpressionBehaviors.MayThrowRuntime;
        if ((preservation & ExpressionExceptionPreservation.UserCode) != 0) flags |= ExpressionBehaviors.MayThrowUserCode;
        return flags;
    }

    /// <summary>Whether the evaluation can be dropped under the default policy with <paramref name="policy"/> as trust policy.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy.</param>
    /// <returns><see langword="true"/> when dropping it is unobservable.</returns>
    internal static bool CanDiscard(ExpressionBehavior behavior, ExpressionTrustPolicy policy)
        => CanDiscard(behavior, policy, aggressive: false, ExpressionExceptionPreservation.All);

    /// <summary>Whether the evaluation can be dropped under <paramref name="options"/>.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="options">The policy.</param>
    /// <returns><see langword="true"/> when dropping it is permitted.</returns>
    internal static bool CanDiscard(ExpressionBehavior behavior, ExpressionSimplifierOptions options)
        => CanDiscard(behavior, options.TrustPolicy, options.AggressiveSimplification, options.ExceptionPreservation);

    /// <summary>
    /// Whether the evaluation can be performed a different number of times (at least once) under the default policy with
    /// <paramref name="policy"/> as trust policy, provided no side effect runs between the occurrences when it reads mutable state
    /// (see the remarks of the class).
    /// </summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy.</param>
    /// <returns><see langword="true"/> when repeating or merging it is unobservable in an unchanged state.</returns>
    internal static bool CanRepeat(ExpressionBehavior behavior, ExpressionTrustPolicy policy)
        => CanRepeat(behavior, policy, aggressive: false, ExpressionExceptionPreservation.All);

    /// <summary>
    /// Whether the evaluation can be performed a different number of times (at least once) under <paramref name="options"/>, with
    /// the same contextual condition as <see cref="CanRepeat(ExpressionBehavior, ExpressionTrustPolicy)"/>.
    /// </summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="options">The policy.</param>
    /// <returns><see langword="true"/> when repeating or merging it is permitted.</returns>
    internal static bool CanRepeat(ExpressionBehavior behavior, ExpressionSimplifierOptions options)
        => CanRepeat(behavior, options.TrustPolicy, options.AggressiveSimplification, options.ExceptionPreservation);

    /// <summary>Whether the evaluation can be moved relative to other movable evaluations under the default policy with <paramref name="policy"/> as trust policy.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy.</param>
    /// <returns><see langword="true"/> when moving it is unobservable.</returns>
    internal static bool CanReorder(ExpressionBehavior behavior, ExpressionTrustPolicy policy)
        => CanReorder(behavior, policy, aggressive: false, ExpressionExceptionPreservation.All);

    /// <summary>Whether the evaluation can be moved relative to other movable evaluations under <paramref name="options"/>.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="options">The policy.</param>
    /// <returns><see langword="true"/> when moving it is permitted.</returns>
    internal static bool CanReorder(ExpressionBehavior behavior, ExpressionSimplifierOptions options)
        => CanReorder(behavior, options.TrustPolicy, options.AggressiveSimplification, options.ExceptionPreservation);

    /// <summary>Discard: no side effect, no unknown code, no allocation unless aggressive, no preserved exception.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="trust">The trust policy.</param>
    /// <param name="aggressive">Whether aggressive simplification is enabled.</param>
    /// <param name="preservation">The preserved exception categories.</param>
    /// <returns><see langword="true"/> when no flag forbids it.</returns>
    private static bool CanDiscard(ExpressionBehavior behavior, ExpressionTrustPolicy trust, bool aggressive, ExpressionExceptionPreservation preservation)
        => (Effective(behavior, trust) & (Imperative | AllocationObstacle(aggressive) | PreservedExceptions(preservation))) == 0;

    /// <summary>
    /// Repeat or merge: no side effect, no unknown code, no allocation unless aggressive; nondeterminism forbids it unless
    /// aggressive, and then only together with a preserved exception (two evaluations could disagree on throwing). Exceptions of a
    /// deterministic evaluation never forbid it.
    /// </summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="trust">The trust policy.</param>
    /// <param name="aggressive">Whether aggressive simplification is enabled.</param>
    /// <param name="preservation">The preserved exception categories.</param>
    /// <returns><see langword="true"/> when no flag forbids it.</returns>
    private static bool CanRepeat(ExpressionBehavior behavior, ExpressionTrustPolicy trust, bool aggressive, ExpressionExceptionPreservation preservation)
    {
        ExpressionBehaviors flags = Effective(behavior, trust);
        ExpressionBehaviors obstacles = Imperative | AllocationObstacle(aggressive);
        if (!aggressive) obstacles |= ExpressionBehaviors.Nondeterministic;
        else if ((flags & ExpressionBehaviors.Nondeterministic) != 0) obstacles |= PreservedExceptions(preservation);
        return (flags & obstacles) == 0;
    }

    /// <summary>Reorder: no side effect, no unknown code, no allocation or nondeterminism unless aggressive, no preserved exception.</summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="trust">The trust policy.</param>
    /// <param name="aggressive">Whether aggressive simplification is enabled.</param>
    /// <param name="preservation">The preserved exception categories.</param>
    /// <returns><see langword="true"/> when no flag forbids it.</returns>
    private static bool CanReorder(ExpressionBehavior behavior, ExpressionTrustPolicy trust, bool aggressive, ExpressionExceptionPreservation preservation)
    {
        ExpressionBehaviors obstacles = Imperative | AllocationObstacle(aggressive) | PreservedExceptions(preservation);
        if (!aggressive) obstacles |= ExpressionBehaviors.Nondeterministic;
        return (Effective(behavior, trust) & obstacles) == 0;
    }

    /// <summary>The allocation obstacle, lifted by aggressive simplification.</summary>
    /// <param name="aggressive">Whether aggressive simplification is enabled.</param>
    /// <returns><see cref="ExpressionBehaviors.Allocates"/>, or nothing when aggressive.</returns>
    private static ExpressionBehaviors AllocationObstacle(bool aggressive) => aggressive ? ExpressionBehaviors.None : ExpressionBehaviors.Allocates;

    /// <summary>
    /// Maps a behavior to the effects of <see cref="ExpressionEffectAnalysis"/> under <paramref name="policy"/>, the bridge a
    /// future policy could use to feed the existing gate: exceptions become <see cref="ExpressionEffects.MayThrow"/>,
    /// side effects, nondeterminism and unknown code become <see cref="ExpressionEffects.Observable"/>.
    /// </summary>
    /// <param name="behavior">The behavior.</param>
    /// <param name="policy">The trust policy.</param>
    /// <returns>The equivalent effects.</returns>
    /// <remarks>
    /// The bridge only knows the trust axis: the gate has no notion of nondeterminism or of exception categories, so it stays as
    /// strict as the default policy on the aggressive and exception axes. A rule that honors those axes must ask the permissions
    /// of this class with the simplifier's <see cref="ExpressionSimplifierOptions"/>, not the bridged effects.
    /// </remarks>
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
