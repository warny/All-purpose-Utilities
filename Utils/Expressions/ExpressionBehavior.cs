namespace Utils.Expressions;

/// <summary>
/// What evaluating an expression may do besides producing its value, as classified by <see cref="ExpressionBehaviorAnalysis"/>
/// (S5 P18 Architecture A). Every flag but <see cref="ReliesOnDeclaration"/> is a "may" fact: an absent flag is a guarantee
/// of the analysis, a present flag only says that the behavior could not be excluded.
/// </summary>
/// <remarks>
/// <see cref="Unknown"/> marks code the analysis cannot see (an unannotated user method, a property getter, a constructor, a
/// delegate, a dynamic operation, an extension node, a loop or jump). It always comes with every other "may" flag
/// (<see cref="UnknownCode"/>), so a consumer that tests a single flag never mistakes unanalyzed code for safe code.
/// </remarks>
[Flags]
public enum ExpressionBehaviors
{
    /// <summary>The evaluation produces a value and nothing else.</summary>
    None = 0,

    /// <summary>
    /// The evaluation reads a location whose content can change between two evaluations: a parameter or variable that is
    /// free in the analyzed expression (a captured variable included), a field that is neither read-only nor constant, an
    /// array element, or the state of an object passed to a declared method.
    /// </summary>
    ReadsMutableState = 1 << 0,

    /// <summary>
    /// Two evaluations in the same state may produce different results: a clock, a random generator, a fresh identifier, or
    /// unknown code.
    /// </summary>
    Nondeterministic = 1 << 1,

    /// <summary>
    /// The evaluation may change state that outlives it or that another part of the program can observe: a write to a free or
    /// captured variable, a field, an array element or a property, a known state-advancing method, or unknown code. A write to
    /// a variable local to the analyzed code (a lambda parameter, a block variable) that no escaping closure captures is not a
    /// side effect.
    /// </summary>
    SideEffects = 1 << 2,

    /// <summary>The evaluation may allocate an object whose identity is observable (array, boxed value, delegate, quoted tree, object).</summary>
    Allocates = 1 << 3,

    /// <summary>
    /// The evaluation may throw an exception raised by built-in arithmetic or by a trusted numeric function: overflow of
    /// checked or decimal arithmetic, integer or decimal division by zero, the <c>NaN</c> sign, invalid numeric arguments
    /// (<c>Clamp</c> bounds, rounding digits or mode).
    /// </summary>
    MayThrowArithmetic = 1 << 4,

    /// <summary>
    /// The evaluation may throw another exception raised by the runtime itself: a null dereference, an invalid cast or
    /// unboxing, an absent nullable value, an array index or size out of range.
    /// </summary>
    MayThrowRuntime = 1 << 5,

    /// <summary>
    /// The evaluation may throw an exception raised by user code: an explicit <c>throw</c> node of the tree, a declared
    /// (<see cref="DeterministicAttribute"/>) method, a known framework member, or unknown code.
    /// </summary>
    MayThrowUserCode = 1 << 6,

    /// <summary>
    /// Part of the evaluation could not be analyzed. Always set together with <see cref="UnknownCode"/>: every other "may" flag
    /// is set as well.
    /// </summary>
    Unknown = 1 << 7,

    /// <summary>
    /// Provenance, not a behavior: part of the classification relies on a <see cref="DeterministicAttribute"/> declaration made
    /// by the author of a method, which the analysis cannot verify.
    /// </summary>
    ReliesOnDeclaration = 1 << 8,

    /// <summary>Every exception flag.</summary>
    MayThrow = MayThrowArithmetic | MayThrowRuntime | MayThrowUserCode,

    /// <summary>The classification of code the analysis cannot see: every "may" flag, with <see cref="Unknown"/>.</summary>
    UnknownCode = ReadsMutableState | Nondeterministic | SideEffects | Allocates | MayThrow | Unknown,
}

/// <summary>
/// The classified behavior of an evaluation (S5 P18 Architecture A): its <see cref="ExpressionBehaviors"/> flags and the
/// guarantees they give.
/// </summary>
/// <param name="Flags">The behavior flags.</param>
/// <remarks>
/// The guarantees hold within one process. A trusted numeric function is reproducible there, but its last bits may differ
/// between platforms or runtime versions. A guarantee whose <see cref="ReliesOnDeclarations"/> is <see langword="true"/>
/// is as good as the declarations it relies on.
/// </remarks>
public readonly record struct ExpressionBehavior(ExpressionBehaviors Flags)
{
    /// <summary>Gets the behavior of an evaluation that only produces a value.</summary>
    public static ExpressionBehavior None => default;

    /// <summary>Gets whether every part of the evaluation was analyzed (no <see cref="ExpressionBehaviors.Unknown"/> code).</summary>
    public bool IsFullyAnalyzed => (Flags & ExpressionBehaviors.Unknown) == 0;

    /// <summary>
    /// Gets whether two evaluations in the same state produce the same outcome: equal results, or exceptions of the same type.
    /// Reading mutable state is allowed (the result follows that state); allocation is allowed (the results are equal, not
    /// identical).
    /// </summary>
    public bool IsDeterministic => (Flags & (ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.Unknown)) == 0;

    /// <summary>Gets whether the evaluation changes no state that outlives it.</summary>
    public bool IsSideEffectFree => (Flags & (ExpressionBehaviors.SideEffects | ExpressionBehaviors.Unknown)) == 0;

    /// <summary>
    /// Gets whether every evaluation produces the same outcome whatever happens between them: deterministic, free of side
    /// effects, reading no mutable state and allocating nothing.
    /// </summary>
    public bool IsReproducible
        => (Flags & (ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.SideEffects | ExpressionBehaviors.ReadsMutableState
            | ExpressionBehaviors.Allocates | ExpressionBehaviors.Unknown)) == 0;

    /// <summary>Gets whether the evaluation may throw an exception of any origin.</summary>
    public bool MayThrow => (Flags & ExpressionBehaviors.MayThrow) != 0;

    /// <summary>Gets whether the evaluation may throw an exception raised by user or unknown code.</summary>
    public bool MayThrowUserCode => (Flags & ExpressionBehaviors.MayThrowUserCode) != 0;

    /// <summary>Gets whether part of the classification relies on a <see cref="DeterministicAttribute"/> declaration.</summary>
    public bool ReliesOnDeclarations => (Flags & ExpressionBehaviors.ReliesOnDeclaration) != 0;

    /// <summary>Combines the behaviors of two evaluations that both run (or may both run).</summary>
    /// <param name="other">The other behavior.</param>
    /// <returns>The union of both behaviors.</returns>
    public ExpressionBehavior Union(ExpressionBehavior other) => new(Flags | other.Flags);
}
