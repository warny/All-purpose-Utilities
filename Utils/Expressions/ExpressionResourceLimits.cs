using System;

namespace Utils.Expressions;

/// <summary>
/// Immutable resource limits applied by the expression-tree engine (<see cref="ExpressionTransformer"/>,
/// <c>ExpressionSimplifier</c>, <c>ExpressionComparer</c>) to one public operation.
/// </summary>
/// <remarks>
/// <para>
/// A public operation is the outermost call into the engine on a thread (for example <c>Simplify</c>, <c>Equals</c>,
/// <c>GetHashCode</c>, or the first <see cref="ExpressionTransformer"/> engine call of a custom transformer). Before any
/// work, its input is validated against <see cref="MaxDepth"/> and <see cref="MaxNodes"/>; during the work, the
/// transformation and structural visits it performs are counted against <see cref="MaxTransformVisits"/> and
/// <see cref="MaxStructuralVisits"/>. Every excess ends with an <see cref="ExpressionResourceLimitException"/>.
/// </para>
/// <para>
/// A public call made while another one is running on the same thread (from a rule, or from user code such as a
/// constant's <see cref="object.Equals(object?)"/>) joins the running operation: it validates its own input and may
/// tighten the limits for its own duration, but it never loosens them and never resets the counters.
/// </para>
/// <para>
/// <see cref="Unlimited"/> (or any limit set to <see cref="int.MaxValue"/>/<see cref="long.MaxValue"/>) disables the
/// corresponding check for trusted inputs. The execution-stack guard is never disabled: a recursion that would
/// exhaust the stack still ends with an <see cref="ExpressionResourceLimitException"/> of kind
/// <see cref="ExpressionResourceLimitKind.Stack"/>.
/// </para>
/// <para>
/// The visit budgets bound the measured engine work; they do not bound time or memory directly. One transformation
/// visit costs roughly 1–4 µs and 1–6 KB of allocations on the measured workloads, so the default budget may still
/// allocate several gigabytes cumulatively before it is exhausted.
/// </para>
/// </remarks>
public sealed record ExpressionResourceLimits
{
    /// <summary>The default maximum depth: 512 nodes on a root-to-leaf path.</summary>
    public const int DefaultMaxDepth = 512;

    /// <summary>The default maximum number of logical nodes: 50,000.</summary>
    public const long DefaultMaxNodes = 50_000;

    /// <summary>The default maximum number of transformation visits per operation: 1,000,000.</summary>
    public const long DefaultMaxTransformVisits = 1_000_000;

    /// <summary>The default maximum number of structural visits per operation: 10,000,000.</summary>
    public const long DefaultMaxStructuralVisits = 10_000_000;

    /// <summary>Backing field of <see cref="MaxDepth"/>.</summary>
    private readonly int _maxDepth = DefaultMaxDepth;

    /// <summary>Backing field of <see cref="MaxNodes"/>.</summary>
    private readonly long _maxNodes = DefaultMaxNodes;

    /// <summary>Backing field of <see cref="MaxTransformVisits"/>.</summary>
    private readonly long _maxTransformVisits = DefaultMaxTransformVisits;

    /// <summary>Backing field of <see cref="MaxStructuralVisits"/>.</summary>
    private readonly long _maxStructuralVisits = DefaultMaxStructuralVisits;

    /// <summary>Gets the default limits: depth 512, 50,000 logical nodes, 1,000,000 transformation visits, 10,000,000 structural visits.</summary>
    public static ExpressionResourceLimits Default { get; } = new();

    /// <summary>
    /// Gets limits that disable the depth, node and work checks, for trusted inputs. The execution-stack guard stays
    /// active.
    /// </summary>
    public static ExpressionResourceLimits Unlimited { get; } = new()
    {
        MaxDepth = int.MaxValue,
        MaxNodes = long.MaxValue,
        MaxTransformVisits = long.MaxValue,
        MaxStructuralVisits = long.MaxValue,
    };

    /// <summary>
    /// Gets the maximum number of nodes on any root-to-leaf path of an input tree (the root alone has depth 1).
    /// Structural containers (switch cases, catch blocks, member bindings, element initializers) count as one level.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxDepth
    {
        get => _maxDepth;
        init => _maxDepth = RequirePositive(value, nameof(MaxDepth));
    }

    /// <summary>
    /// Gets the maximum number of logical nodes of an input tree: node occurrences when the tree is unfolded, so a
    /// subtree shared by two parents counts twice (the engine walks it twice). Bounds the distinct nodes too.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long MaxNodes
    {
        get => _maxNodes;
        init => _maxNodes = RequirePositive(value, nameof(MaxNodes));
    }

    /// <summary>
    /// Gets the maximum number of transformation visits of one operation: one visit per node processed by the
    /// <see cref="ExpressionTransformer"/> engine, including nested comparer simplifications and rule re-entries.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long MaxTransformVisits
    {
        get => _maxTransformVisits;
        init => _maxTransformVisits = RequirePositive(value, nameof(MaxTransformVisits));
    }

    /// <summary>
    /// Gets the maximum number of structural visits of one operation: one visit per node reached by structural
    /// equality, structural hashing, canonical-order keys and their comparisons, and parameter substitution.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public long MaxStructuralVisits
    {
        get => _maxStructuralVisits;
        init => _maxStructuralVisits = RequirePositive(value, nameof(MaxStructuralVisits));
    }

    /// <summary>Validates a limit.</summary>
    /// <param name="value">The value.</param>
    /// <param name="name">The property name.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than 1.</exception>
    private static int RequirePositive(int value, string name)
        => value >= 1 ? value : throw new ArgumentOutOfRangeException(name, value, "A resource limit must be at least 1.");

    /// <summary>Validates a limit.</summary>
    /// <param name="value">The value.</param>
    /// <param name="name">The property name.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than 1.</exception>
    private static long RequirePositive(long value, string name)
        => value >= 1 ? value : throw new ArgumentOutOfRangeException(name, value, "A resource limit must be at least 1.");
}
