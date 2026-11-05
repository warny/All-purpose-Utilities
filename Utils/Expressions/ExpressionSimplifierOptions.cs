using System;

namespace Utils.Expressions;

/// <summary>
/// The exception categories of <see cref="ExpressionBehaviors"/> whose observation a simplification must preserve
/// (S5 P18 Architecture B). A set flag means "preserve": a rewrite never drops such an exception, nor changes its order relative
/// to the other observable events of the evaluation. Used by <see cref="ExpressionSimplifierOptions.ExceptionPreservation"/>.
/// </summary>
/// <remarks>
/// <para>
/// Clearing a flag does not make an evaluation that may raise that category eliminable: it only lifts that one obstacle.
/// Side effects, unknown code, allocations, nondeterminism and the categories still preserved keep forbidding the rewrite, and
/// whatever the flags, no rewrite introduces an exception the source cannot raise, nor raises during simplification an exception
/// the source would raise when it runs. See <c>docs/expressions/SimplificationPolicies.md</c>.
/// </para>
/// <para>
/// The zero value, <see cref="None"/>, is the least conservative one; the default of
/// <see cref="ExpressionSimplifierOptions"/> is <see cref="All"/>.
/// </para>
/// </remarks>
[Flags]
public enum ExpressionExceptionPreservation
{
    /// <summary>No category is preserved.</summary>
    None = 0,

    /// <summary>Preserve the exceptions of <see cref="ExpressionBehaviors.MayThrowArithmetic"/> (overflow, division by zero, invalid numeric arguments).</summary>
    Arithmetic = 1 << 0,

    /// <summary>Preserve the exceptions of <see cref="ExpressionBehaviors.MayThrowRuntime"/> (null dereference, invalid cast, absent nullable value, index out of range).</summary>
    Runtime = 1 << 1,

    /// <summary>Preserve the exceptions of <see cref="ExpressionBehaviors.MayThrowUserCode"/> (throw nodes, declared methods, known framework members, unknown code).</summary>
    UserCode = 1 << 2,

    /// <summary>Preserve every category (the default).</summary>
    All = Arithmetic | Runtime | UserCode,
}

/// <summary>
/// The rewrite policy of an <c>ExpressionSimplifier</c> (S5 P18 Architecture B): three independent axes that a later stage
/// (Architecture C) will let the rules consult through the behavior permissions. <b>No rule consults them yet</b>: every value
/// gives the same simplification results, the same user calls and the same comparer answers as the default.
/// </summary>
/// <remarks>
/// <para>
/// <b>Axes.</b> <see cref="TrustPolicy"/> decides whether <see cref="DeterministicAttribute"/> declarations count as facts;
/// <see cref="AggressiveSimplification"/> lifts a closed list of evaluation guarantees on analyzed code;
/// <see cref="ExceptionPreservation"/> lists the exception categories that must stay observable. None of them implies another,
/// and none makes unknown code (<see cref="ExpressionBehaviors.Unknown"/>) or side effects rewritable.
/// </para>
/// <para>
/// <b>Immutability.</b> The type is an immutable record, validated by its <c>init</c> accessors (so a <c>with</c> expression is
/// validated too). A simplifier receives it at construction (<c>new ExpressionSimplifier { Options = … }</c>) and cannot change it
/// afterwards, so nested, re-entrant and concurrent calls on an instance apply the same policy; use two instances for two
/// policies. There is no ambient or static policy.
/// </para>
/// </remarks>
public sealed record ExpressionSimplifierOptions
{
    /// <summary>Backing field of <see cref="TrustPolicy"/>.</summary>
    private readonly ExpressionTrustPolicy _trustPolicy = ExpressionTrustPolicy.Verified;

    /// <summary>Backing field of <see cref="ExceptionPreservation"/>.</summary>
    private readonly ExpressionExceptionPreservation _exceptionPreservation = ExpressionExceptionPreservation.All;

    /// <summary>
    /// Gets the conservative defaults: <see cref="ExpressionTrustPolicy.Verified"/>, no aggressive simplification, every exception
    /// category preserved.
    /// </summary>
    public static ExpressionSimplifierOptions Default { get; } = new();

    /// <summary>
    /// Gets whether <see cref="DeterministicAttribute"/> declarations count as facts. Defaults to
    /// <see cref="ExpressionTrustPolicy.Verified"/>: a classification that relies on a declaration is treated as unknown code.
    /// </summary>
    /// <remarks>
    /// <see cref="ExpressionTrustPolicy.TrustDeclarations"/> trusts the declared promise only (deterministic, no ambient state, no
    /// side effect): a declared method may still throw (<see cref="ExpressionBehaviors.MayThrowUserCode"/>), allocate and read the
    /// state of its arguments, and code that is neither declared nor verified stays unknown. It does not enable
    /// <see cref="AggressiveSimplification"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="ExpressionTrustPolicy"/>.</exception>
    public ExpressionTrustPolicy TrustPolicy
    {
        get => _trustPolicy;
        init => _trustPolicy = value is ExpressionTrustPolicy.Verified or ExpressionTrustPolicy.TrustDeclarations
            ? value
            : throw new ArgumentOutOfRangeException(nameof(TrustPolicy), value, "Undefined trust policy.");
    }

    /// <summary>
    /// Gets whether the rules may relax the guarantees that only matter to identity and timing on analyzed code. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, a future rule may drop, merge, repeat or move an evaluation that allocates (the identity and number of the
    /// objects it creates are not preserved), and merge, repeat or move a nondeterministic evaluation that has no side effect (a
    /// clock read: two reads may become one), provided it raises no exception category that is still preserved.
    /// </para>
    /// <para>
    /// It never relaxes: side effects; unknown code (it does not turn <see cref="ExpressionBehaviors.Unknown"/> into a verified
    /// classification, nor executes user code during simplification); the preserved exception categories
    /// (<see cref="ExceptionPreservation"/> alone decides them); declarations (<see cref="TrustPolicy"/> alone decides them); the
    /// order of a mutable-state read relative to the side effects around it; the absence of new exceptions; the IEEE and domain
    /// contract of the simplifier.
    /// </para>
    /// </remarks>
    public bool AggressiveSimplification { get; init; }

    /// <summary>
    /// Gets the exception categories whose observation must be preserved. Defaults to
    /// <see cref="ExpressionExceptionPreservation.All"/>.
    /// </summary>
    /// <remarks>
    /// A category left out may, under the explicit conditions of a future rule, be dropped or observed in another order relative
    /// to other movable evaluations. It never makes unknown code, side effects, allocations or nondeterminism eliminable, and never
    /// allows a rewrite to introduce an exception or to raise one during simplification.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value has a flag outside <see cref="ExpressionExceptionPreservation.All"/>.</exception>
    public ExpressionExceptionPreservation ExceptionPreservation
    {
        get => _exceptionPreservation;
        init => _exceptionPreservation = (value & ~ExpressionExceptionPreservation.All) == 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(ExceptionPreservation), value, "Undefined exception category.");
    }
}
