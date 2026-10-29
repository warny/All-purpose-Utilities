using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Utils.Expressions;

/// <summary>The measured shape of an expression graph (see <see cref="ExpressionShapeValidator.Measure"/>).</summary>
/// <param name="DistinctNodes">Distinct items reached (nodes and structural containers; a shared subtree counts once).</param>
/// <param name="LogicalNodes">Item occurrences when the graph is unfolded (a shared subtree counts once per use), saturated at <see cref="long.MaxValue"/>.</param>
/// <param name="Depth">Maximum number of items on a root-to-leaf path (the root alone has depth 1), or 0 for no root.</param>
/// <param name="OpaqueNodes">Distinct nodes of an unknown class (extension or custom nodes), kept as leaves and never inspected.</param>
/// <param name="Truncated">Whether the measurement stopped at its distinct-item cap; the other values are then lower bounds.</param>
internal readonly record struct ExpressionShape(long DistinctNodes, long LogicalNodes, int Depth, long OpaqueNodes, bool Truncated);

/// <summary>
/// Non-recursive structural validation of expression trees (ETC R1a-Security): checks an input against
/// <see cref="ExpressionResourceLimits.MaxDepth"/> and <see cref="ExpressionResourceLimits.MaxNodes"/> before the
/// recursive engine runs, with an explicit stack, so that the validation itself can neither exhaust the call stack
/// nor run longer than the node limit.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is walked.</b> Every child the framework expression model exposes: operands, receivers, arguments, lambda
/// parameters and bodies, conversion lambdas, block variables and statements, catch variables, filters and bodies,
/// finally and fault bodies, switch values, cases (test values and bodies) and default bodies, loop bodies, goto values,
/// label default values, index receivers and arguments, member-init bindings (assignments, nested member bindings,
/// list bindings), list-init element initializers, new and new-array arguments, dynamic arguments and runtime
/// variables. The containers <see cref="SwitchCase"/>, <see cref="CatchBlock"/>, <see cref="MemberBinding"/> and
/// <see cref="ElementInit"/> count as one item and one level each, so a wide or deeply nested binding structure is
/// bounded like an expression.
/// </para>
/// <para>
/// <b>What is never inspected.</b> A node whose class is not a framework expression class (an
/// <see cref="ExpressionType.Extension"/> node, or any custom <see cref="Expression"/> subclass) is an opaque leaf:
/// its <c>Reduce</c>, <c>CanReduce</c>, <c>VisitChildren</c> and custom getters are never called. Dispatch is on the
/// runtime class, so not even the virtual <see cref="Expression.NodeType"/> getter runs: the validator executes no user
/// code.
/// </para>
/// <para>
/// <b>Decision walk.</b> <see cref="Validate"/> first walks the unfolded tree (a shared subtree is walked once per use,
/// exactly as the engine does) and counts every item it pushes, up to <c>min(node limit, 65,536)</c> occurrences: it
/// stops as soon as a path exceeds the depth limit or the count exceeds the node limit. Its time and memory are
/// therefore bounded by that budget, whatever the sharing. The default node limit (50,000) is below the budget, so default
/// validations only ever take this path. The per-thread work stack is reused, so it allocates nothing in the steady state.
/// </para>
/// <para>
/// <b>Shared graphs (ETC R2 follow-up).</b> A node limit above 65,536 is decided in bounded stages: an unfolded walk of
/// 4,096 occurrences (small inputs, historical cost), then the exact measurement below capped at 256 distinct items (a
/// heavily shared DAG, for example <c>e = e + e</c> repeated n times: n + 1 distinct nodes, 2^(n+1) - 1 occurrences, is
/// decided without being unfolded), then the same unfolded walk resumed where it paused, up to 4,194,304 occurrences
/// (large trees without sharing, at the cost of a plain walk), and finally the uncapped exact measurement. The total is O(4,194,304 + distinct items +
/// child edges) whatever the sharing. Before this, a limit above the occurrence count of such a DAG (in particular
/// <see cref="ExpressionResourceLimits.Unlimited"/>) made the validation exponential. With both limits at their maximum
/// nothing can be refused, so nothing is walked. A partial walk is never a success: the validation either completes under
/// both limits or throws. When both limits are exceeded, the stage that decides reports one of them (the depth first for
/// an exact measurement).
/// </para>
/// <para>
/// <b>Exact measurement.</b> <see cref="Measure"/> computes distinct and logical counts and depth by post-order dynamic
/// programming over distinct items (reference identity, never structural equality), in O(distinct items + child edges)
/// without unfolding, with saturating arithmetic, iteratively (explicit stacks), keeping references only in local
/// collections. It decides large validations, reports the observed values of a refusal and serves diagnostics.
/// </para>
/// </remarks>
internal static class ExpressionShapeValidator
{
    /// <summary>Initial capacity of the per-thread work stack.</summary>
    private const int InitialStackCapacity = 64;

    /// <summary>
    /// Largest node limit decided by the historical unfolded walk alone (ETC R2 follow-up): at least the default node limit,
    /// so default validations take exactly the historical path, whose cost the limit bounds.
    /// </summary>
    private const long UnfoldedItemBudget = 65_536;

    /// <summary>Occurrence budget of the first unfolded stage for a higher node limit: decides small inputs at the historical cost.</summary>
    private const long SmallUnfoldedBudget = 4_096;

    /// <summary>Distinct-item cap of the exact probe for a higher node limit: decides heavily shared DAGs of few distinct items.</summary>
    private const long SharedProbeItems = 256;

    /// <summary>Occurrence budget of the second unfolded stage for a higher node limit: decides large trees at the cost of a plain walk.</summary>
    private const long LargeUnfoldedBudget = 4_194_304;

    /// <summary>Distinct-item cap of the exact measurement made to report a refusal, as a multiple of the node limit.</summary>
    private const long RefusalMeasurementFactor = 4;

    /// <summary>The per-thread work stack of <see cref="Validate"/>; validation runs no user code, so it is never re-entered.</summary>
    [ThreadStatic]
    private static Frame[]? _frames;

    /// <summary>One pending item of the decision walk.</summary>
    /// <param name="Item">An <see cref="Expression"/> or a structural container.</param>
    /// <param name="Depth">The depth of <paramref name="Item"/> (the root has depth 1).</param>
    private readonly record struct Frame(object Item, int Depth);

    /// <summary>The mutable state of one walk, threaded by reference through the child enumeration.</summary>
    private ref struct Walk
    {
        /// <summary>The pending items.</summary>
        public Frame[] Frames;

        /// <summary>The number of pending items.</summary>
        public int Count;

        /// <summary>The remaining item budget, decremented by every child reached; exhausting it throws <see cref="NodeBudgetExhaustedException"/> unless the walk is <see cref="Pausable"/>.</summary>
        public long Remaining;

        /// <summary>
        /// When <see langword="true"/>, the first child reached is kept in <see cref="First"/> instead of being pushed, so the
        /// walk continues into it directly (no array write, hence no GC write barrier, for a single-child node).
        /// </summary>
        public bool TakeFirst;

        /// <summary>The first child of the current item when <see cref="TakeFirst"/> is set, otherwise <see langword="null"/>.</summary>
        public object? First;

        /// <summary>
        /// When <see langword="true"/>, exhausting <see cref="Remaining"/> does not throw: the walk goes negative and the decision
        /// loop pauses after the current item, so it can resume with more budget (ETC R2 follow-up).
        /// </summary>
        public bool Pausable;

        /// <summary>
        /// When <see langword="true"/> (the exact measurement), exhausting <see cref="Remaining"/> sets <see cref="Exhausted"/>
        /// and stops pushing, and the child lists stop being enumerated, instead of throwing (ETC R2 follow-up: no exception cost
        /// on the capped probe of <see cref="Validate"/>).
        /// </summary>
        public bool Capped;

        /// <summary>Whether a <see cref="Capped"/> walk exhausted its budget.</summary>
        public bool Exhausted;

        /// <summary>Initializes a walk.</summary>
        /// <param name="frames">The initial work stack.</param>
        /// <param name="remaining">The item budget.</param>
        /// <param name="takeFirst">Whether the first child is kept aside (see <see cref="TakeFirst"/>).</param>
        public Walk(Frame[] frames, long remaining, bool takeFirst)
        {
            Frames = frames;
            Count = 0;
            Remaining = remaining;
            TakeFirst = takeFirst;
            First = null;
            Pausable = false;
            Capped = false;
            Exhausted = false;
        }
    }

    /// <summary>
    /// Validates <paramref name="root"/> against the depth and node limits; returns normally only when the whole
    /// unfolded tree fits both.
    /// </summary>
    /// <param name="root">The tree to validate, or <see langword="null"/> (accepted: nothing to walk).</param>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The logical node limit.</param>
    /// <exception cref="ExpressionResourceLimitException">The tree exceeds one of the limits.</exception>
    internal static void Validate(Expression? root, int maxDepth, long maxNodes)
    {
        if (root is null) return;

        // With both limits at their maximum nothing can be refused (logical counts saturate at long.MaxValue), so there is
        // nothing to walk; the stack guards of the operations that follow still apply.
        if (maxDepth == int.MaxValue && maxNodes == long.MaxValue) return;

        // Historical path, unchanged: a node limit up to the unfolded budget (the default limit included) bounds the walk.
        if (maxNodes <= UnfoldedItemBudget)
        {
            ValidateUnfolded(root, maxDepth, maxNodes);
            return;
        }

        ValidateStaged(root, maxDepth, maxNodes);
    }

    /// <summary>
    /// The historical decision walk of <see cref="Validate"/>, for a node limit up to <see cref="UnfoldedItemBudget"/>: walks
    /// the unfolded tree, bounded by <paramref name="maxNodes"/> item occurrences, and stops at the first excess.
    /// </summary>
    /// <param name="root">The tree to validate.</param>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The logical node limit, at most <see cref="UnfoldedItemBudget"/>.</param>
    /// <exception cref="ExpressionResourceLimitException">The tree exceeds one of the limits.</exception>
    private static void ValidateUnfolded(Expression root, int maxDepth, long maxNodes)
    {
        // Every item reached consumes one unit; the root already consumed its own. The walk goes on into the first child
        // of each item directly and pushes only the others.
        var walk = new Walk(_frames ??= new Frame[InitialStackCapacity], maxNodes - 1, takeFirst: true);
        int highWater = 0;
        object current = root;
        int depth = 1;
        try
        {
            // The historical loop, kept inline (locals in registers): this path decides every default validation.
            while (true)
            {
                if (depth > maxDepth)
                {
                    throw Refuse(root, ExpressionResourceLimitKind.Depth, maxDepth, maxNodes, depth);
                }

                walk.First = null;
                PushChildren(current, depth + 1, ref walk);
                if (walk.Count > highWater) highWater = walk.Count;
                if (walk.First is not null)
                {
                    current = walk.First;
                    depth++;
                    continue;
                }

                if (walk.Count == 0) return;
                Frame frame = walk.Frames[--walk.Count];
                current = frame.Item;
                depth = frame.Depth;
            }
        }
        catch (NodeBudgetExhaustedException)
        {
            throw Refuse(root, ExpressionResourceLimitKind.Nodes, maxDepth, maxNodes, maxNodes == long.MaxValue ? maxNodes : maxNodes + 1);
        }
        finally
        {
            ReleaseFrames(walk.Frames, Math.Max(highWater, walk.Count));
        }
    }

    /// <summary>
    /// The decision of <see cref="Validate"/> for a node limit above <see cref="UnfoldedItemBudget"/> (ETC R2 follow-up), in
    /// bounded stages, so that neither a small tree, nor a large tree without sharing, nor a heavily shared DAG pays for the
    /// strategy suited to another: (1) the unfolded walk decides small inputs within <see cref="SmallUnfoldedBudget"/>
    /// occurrences; (2) <see cref="Measure"/> capped at <see cref="SharedProbeItems"/> distinct items decides graphs of few
    /// distinct items (a shared DAG) without unfolding them; (3) the same unfolded walk, resumed where it paused, decides large
    /// trees within <see cref="LargeUnfoldedBudget"/> occurrences at the cost of a plain walk; (4) the uncapped exact
    /// measurement decides the rest. Total cost O(<see cref="LargeUnfoldedBudget"/> + distinct items + child edges).
    /// </summary>
    /// <param name="root">The tree to validate.</param>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The logical node limit, above <see cref="UnfoldedItemBudget"/>.</param>
    /// <exception cref="ExpressionResourceLimitException">The tree exceeds one of the limits.</exception>
    private static void ValidateStaged(Expression root, int maxDepth, long maxNodes)
    {
        // A pausable walk never throws for its budget: it stops between two items, with every pushed item already counted,
        // so it can resume after the probe without walking anything twice.
        var walk = new Walk(_frames ??= new Frame[InitialStackCapacity], SmallUnfoldedBudget - 1, takeFirst: true) { Pausable = true };
        int highWater = 0;
        object current = root;
        int depth = 1;
        try
        {
            if (Advance(root, ref walk, ref current, ref depth, ref highWater, maxDepth, maxNodes)) return;

            ExpressionShape probe = Measure(root, SharedProbeItems);
            if (!probe.Truncated)
            {
                Decide(probe, maxDepth, maxNodes);
                return;
            }

            long largeBudget = Math.Min(maxNodes, LargeUnfoldedBudget);
            walk.Remaining += largeBudget - SmallUnfoldedBudget;
            if (Advance(root, ref walk, ref current, ref depth, ref highWater, maxDepth, maxNodes)) return;

            if (largeBudget == maxNodes)
            {
                throw Refuse(root, ExpressionResourceLimitKind.Nodes, maxDepth, maxNodes, maxNodes == long.MaxValue ? maxNodes : maxNodes + 1);
            }
        }
        finally
        {
            ReleaseFrames(walk.Frames, Math.Max(highWater, walk.Count));
        }

        ValidateShared(root, maxDepth, maxNodes);
    }

    /// <summary>
    /// Runs the unfolded decision walk from <paramref name="current"/> until it completes, throws, or (for a pausable walk)
    /// exhausts its budget between two items.
    /// </summary>
    /// <param name="root">The validated tree (for the refusal).</param>
    /// <param name="walk">The walk state.</param>
    /// <param name="current">The next item to expand, already counted; updated when the walk pauses.</param>
    /// <param name="depth">The depth of <paramref name="current"/>; updated when the walk pauses.</param>
    /// <param name="highWater">The highest stack occupancy so far, for <see cref="ReleaseFrames"/>.</param>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The logical node limit (for the refusal).</param>
    /// <returns><see langword="true"/> when the whole tree was walked; <see langword="false"/> when a pausable walk paused.</returns>
    /// <exception cref="ExpressionResourceLimitException">A path exceeds the depth limit.</exception>
    private static bool Advance(Expression root, ref Walk walk, ref object current, ref int depth, ref int highWater, int maxDepth, long maxNodes)
    {
        // The loop works on locals (registers) and writes them back when it leaves, however it leaves.
        object item = current;
        int itemDepth = depth;
        int high = highWater;
        try
        {
            while (true)
            {
                if (itemDepth > maxDepth)
                {
                    throw Refuse(root, ExpressionResourceLimitKind.Depth, maxDepth, maxNodes, itemDepth);
                }

                walk.First = null;
                PushChildren(item, itemDepth + 1, ref walk);
                if (walk.Count > high) high = walk.Count;
                if (walk.First is not null)
                {
                    item = walk.First;
                    itemDepth++;
                }
                else
                {
                    if (walk.Count == 0) return true;
                    Frame frame = walk.Frames[--walk.Count];
                    item = frame.Item;
                    itemDepth = frame.Depth;
                }

                if (walk.Remaining < 0) return false;
            }
        }
        finally
        {
            current = item;
            depth = itemDepth;
            highWater = high;
        }
    }

    /// <summary>
    /// Drops every reference written to the reused work stack (popped slots included), so it keeps no tree alive, and keeps
    /// it for the next validation unless a large input grew it.
    /// </summary>
    /// <param name="frames">The work stack, possibly grown by the walk.</param>
    /// <param name="used">The highest number of slots written.</param>
    private static void ReleaseFrames(Frame[] frames, int used)
    {
        Array.Clear(frames, 0, Math.Min(used, frames.Length));
        _frames = frames.Length > 4096 ? null : frames;
    }

    /// <summary>
    /// The exact decision of <see cref="Validate"/> for an input with more occurrences than <see cref="UnfoldedItemBudget"/>:
    /// <see cref="Measure"/> computes depth and saturated logical size over distinct items (reference identity), in
    /// O(distinct items + child edges), so a shared DAG is never unfolded. The measurement is capped at
    /// <paramref name="maxNodes"/> distinct items or child edges: exceeding either implies more than
    /// <paramref name="maxNodes"/> logical items.
    /// </summary>
    /// <param name="root">The tree to validate.</param>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The logical node limit.</param>
    /// <exception cref="ExpressionResourceLimitException">The tree exceeds one of the limits (the depth is checked first).</exception>
    private static void ValidateShared(Expression root, int maxDepth, long maxNodes)
    {
        ExpressionShape shape = Measure(root, maxNodes);
        if (shape.Truncated)
        {
            // Distinct items and child edges are lower bounds of the logical size: the limit is exceeded, by at least one.
            throw new ExpressionResourceLimitException(ExpressionResourceLimitKind.Nodes, maxNodes, SaturatingAdd(maxNodes, 1));
        }

        Decide(shape, maxDepth, maxNodes);
    }

    /// <summary>Decides a complete (not truncated) exact measurement against the limits, the depth first.</summary>
    /// <param name="shape">The measured shape.</param>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The logical node limit.</param>
    /// <exception cref="ExpressionResourceLimitException">The shape exceeds one of the limits; the observed value is exact (saturated at <see cref="long.MaxValue"/>).</exception>
    private static void Decide(ExpressionShape shape, int maxDepth, long maxNodes)
    {
        if (shape.Depth > maxDepth)
        {
            throw new ExpressionResourceLimitException(ExpressionResourceLimitKind.Depth, maxDepth, shape.Depth);
        }

        if (shape.LogicalNodes > maxNodes)
        {
            throw new ExpressionResourceLimitException(ExpressionResourceLimitKind.Nodes, maxNodes, shape.LogicalNodes);
        }
    }

    /// <summary>Measures <paramref name="root"/> exactly, without unfolding shared subtrees.</summary>
    /// <param name="root">The graph root, or <see langword="null"/>.</param>
    /// <param name="maxDistinctItems">
    /// Stop after this many distinct items, or after this many child edges examined; the result is then marked
    /// truncated. Bounding the edges too keeps a node with a huge child list from costing more than the cap.
    /// </param>
    /// <returns>The measured shape.</returns>
    internal static ExpressionShape Measure(Expression? root, long maxDistinctItems = long.MaxValue)
    {
        if (root is null) return new ExpressionShape(0, 0, 0, 0, false);

        // Post-order over distinct items: an item is finalized once all its children are, so depth and logical size
        // are computed by dynamic programming. An item met again while in progress (only possible through a custom,
        // cyclic object graph) is treated as a leaf, so the walk always terminates. A child already scheduled by another
        // parent may be scheduled again (so that it is finalized before every parent), which the edge budget bounds.
        var finalized = new Dictionary<object, (int Depth, long Logical)>(ReferenceEqualityComparer.Instance);
        var inProgress = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<(object Item, bool Expanded)>();
        var walk = new Walk(new Frame[InitialStackCapacity], maxDistinctItems, takeFirst: false) { Capped = true };
        long opaque = 0;
        bool truncated = false;
        pending.Push((root, false));

        while (pending.Count > 0)
        {
            (object item, bool expanded) = pending.Pop();
            if (finalized.ContainsKey(item)) continue;

            // A second schedule of an item already being expanded (a child shared by several parents, or twice by one) is
            // dropped before its children are enumerated again, so every item's child edges are examined once.
            if (!expanded && inProgress.Contains(item)) continue;

            walk.Count = 0;
            bool known;
            if (!expanded)
            {
                // A capped walk stops pushing (and enumerating a wide child list) at its edge budget instead of throwing.
                known = PushChildren(item, 0, ref walk);
                if (walk.Exhausted)
                {
                    truncated = true;
                    break;
                }
            }
            else
            {
                long edgeBudgetLeft = walk.Remaining;
                walk.Remaining = long.MaxValue;
                known = PushChildren(item, 0, ref walk);
                walk.Remaining = edgeBudgetLeft;
            }

            Frame[] children = walk.Frames;
            int childCount = walk.Count;

            if (!expanded)
            {
                if (finalized.Count + inProgress.Count >= maxDistinctItems)
                {
                    truncated = true;
                    break;
                }

                if (!inProgress.Add(item)) continue;
                pending.Push((item, true));
                for (int i = 0; i < childCount; i++)
                {
                    object child = children[i].Item;
                    if (!finalized.ContainsKey(child) && !inProgress.Contains(child)) pending.Push((child, false));
                }

                continue;
            }

            int depth = 0;
            long logical = 1;
            for (int i = 0; i < childCount; i++)
            {
                if (finalized.TryGetValue(children[i].Item, out (int Depth, long Logical) child))
                {
                    depth = Math.Max(depth, child.Depth);
                    logical = SaturatingAdd(logical, child.Logical);
                }
                else
                {
                    // A child still in progress closes a cycle: count it once, as a leaf.
                    depth = Math.Max(depth, 1);
                    logical = SaturatingAdd(logical, 1);
                }
            }

            inProgress.Remove(item);
            finalized[item] = (depth == int.MaxValue ? depth : depth + 1, logical);
            if (!known) opaque++;
        }

        (int rootDepth, long rootLogical) = finalized.TryGetValue(root, out (int Depth, long Logical) rootInfo) ? rootInfo : (0, 0);
        return new ExpressionShape(finalized.Count, rootLogical, rootDepth, opaque, truncated);
    }

    /// <summary>Builds the refusal of <paramref name="root"/>, measuring the exact observed value when it is affordable.</summary>
    /// <param name="root">The refused tree.</param>
    /// <param name="kind">The exceeded dimension.</param>
    /// <param name="maxDepth">The depth limit.</param>
    /// <param name="maxNodes">The node limit.</param>
    /// <param name="lowerBound">The value observed when the decision walk stopped.</param>
    /// <returns>The exception to throw.</returns>
    private static ExpressionResourceLimitException Refuse(Expression root, ExpressionResourceLimitKind kind, int maxDepth, long maxNodes, long lowerBound)
    {
        long cap = maxNodes > long.MaxValue / RefusalMeasurementFactor ? long.MaxValue : maxNodes * RefusalMeasurementFactor;
        ExpressionShape shape = Measure(root, Math.Min(cap, 1_000_000));

        // A truncated measurement only gives lower bounds: the decision walk's own bound is then the reported value.
        long observed = kind == ExpressionResourceLimitKind.Depth ? shape.Depth : shape.LogicalNodes;
        return kind == ExpressionResourceLimitKind.Depth
            ? new ExpressionResourceLimitException(kind, maxDepth, shape.Truncated ? lowerBound : Math.Max(observed, lowerBound))
            : new ExpressionResourceLimitException(kind, maxNodes, shape.Truncated ? lowerBound : Math.Max(observed, lowerBound));
    }

    /// <summary>Adds two non-negative counts, saturating at <see cref="long.MaxValue"/>.</summary>
    /// <param name="a">The first count.</param>
    /// <param name="b">The second count.</param>
    /// <returns>The saturated sum.</returns>
    private static long SaturatingAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;

    /// <summary>
    /// Pushes the children of one item onto the work stack, dispatching on its runtime class (never on a
    /// virtual getter).
    /// </summary>
    /// <param name="item">An <see cref="Expression"/> or a structural container.</param>
    /// <param name="childDepth">The depth given to every pushed child.</param>
    /// <param name="walk">The walk state (work stack, item budget, first-child slot).</param>
    /// <returns><see langword="false"/> when <paramref name="item"/> is of an unknown class (an opaque leaf).</returns>
    /// <remarks>
    /// One dispatch case per expression class or container, each a short list of pushes (node-type dispatch
    /// exception of <c>Utils/AGENTS.md</c>). The classes are disjoint, so the case order only affects speed: the most
    /// frequent arithmetic classes come first (each failed type test costs a class-hierarchy walk). The order of the
    /// pushes does not matter for the measured shape.
    /// </remarks>
    private static bool PushChildren(object item, int childDepth, ref Walk walk)
    {
        switch (item)
        {
            case BinaryExpression binary:
                Push(binary.Left, childDepth, ref walk);
                Push(binary.Right, childDepth, ref walk);
                Push(binary.Conversion, childDepth, ref walk);
                return true;
            case MethodCallExpression call:
                Push(call.Object, childDepth, ref walk);
                PushArguments(call, childDepth, ref walk);
                return true;
            case UnaryExpression unary:
                Push(unary.Operand, childDepth, ref walk);
                return true;
            case ParameterExpression or ConstantExpression:
                return true;
            case MemberExpression member:
                Push(member.Expression, childDepth, ref walk);
                return true;
            case InvocationExpression invocation:
                Push(invocation.Expression, childDepth, ref walk);
                PushArguments(invocation, childDepth, ref walk);
                return true;
            case LambdaExpression lambda:
                PushAll(lambda.Parameters, childDepth, ref walk);
                Push(lambda.Body, childDepth, ref walk);
                return true;
            case ConditionalExpression conditional:
                Push(conditional.Test, childDepth, ref walk);
                Push(conditional.IfTrue, childDepth, ref walk);
                Push(conditional.IfFalse, childDepth, ref walk);
                return true;
            case TypeBinaryExpression typeBinary:
                Push(typeBinary.Expression, childDepth, ref walk);
                return true;
            case NewExpression creation:
                PushAll(creation.Arguments, childDepth, ref walk);
                return true;
            case NewArrayExpression array:
                PushAll(array.Expressions, childDepth, ref walk);
                return true;
            case MemberInitExpression memberInit:
                Push(memberInit.NewExpression, childDepth, ref walk);
                PushAll(memberInit.Bindings, childDepth, ref walk);
                return true;
            case ListInitExpression listInit:
                Push(listInit.NewExpression, childDepth, ref walk);
                PushAll(listInit.Initializers, childDepth, ref walk);
                return true;
            case IndexExpression index:
                Push(index.Object, childDepth, ref walk);
                PushAll(index.Arguments, childDepth, ref walk);
                return true;
            case BlockExpression block:
                PushAll(block.Variables, childDepth, ref walk);
                PushAll(block.Expressions, childDepth, ref walk);
                return true;
            case TryExpression @try:
                Push(@try.Body, childDepth, ref walk);
                PushAll(@try.Handlers, childDepth, ref walk);
                Push(@try.Finally, childDepth, ref walk);
                Push(@try.Fault, childDepth, ref walk);
                return true;
            case SwitchExpression @switch:
                Push(@switch.SwitchValue, childDepth, ref walk);
                PushAll(@switch.Cases, childDepth, ref walk);
                Push(@switch.DefaultBody, childDepth, ref walk);
                return true;
            case LoopExpression loop:
                Push(loop.Body, childDepth, ref walk);
                return true;
            case GotoExpression @goto:
                Push(@goto.Value, childDepth, ref walk);
                return true;
            case LabelExpression label:
                Push(label.DefaultValue, childDepth, ref walk);
                return true;
            case DefaultExpression or DebugInfoExpression:
                return true;
            case RuntimeVariablesExpression runtimeVariables:
                PushAll(runtimeVariables.Variables, childDepth, ref walk);
                return true;
            case DynamicExpression dynamic:
                PushAll(dynamic.Arguments, childDepth, ref walk);
                return true;
            case CatchBlock handler:
                Push(handler.Variable, childDepth, ref walk);
                Push(handler.Filter, childDepth, ref walk);
                Push(handler.Body, childDepth, ref walk);
                return true;
            case SwitchCase @case:
                PushAll(@case.TestValues, childDepth, ref walk);
                Push(@case.Body, childDepth, ref walk);
                return true;
            case MemberAssignment assignment:
                Push(assignment.Expression, childDepth, ref walk);
                return true;
            case MemberMemberBinding memberBinding:
                PushAll(memberBinding.Bindings, childDepth, ref walk);
                return true;
            case MemberListBinding listBinding:
                PushAll(listBinding.Initializers, childDepth, ref walk);
                return true;
            case ElementInit initializer:
                PushAll(initializer.Arguments, childDepth, ref walk);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Pushes the arguments of a call or an invocation through <see cref="IArgumentProvider"/>, which reads them without
    /// materializing (or going through) their <see cref="ReadOnlyCollection{T}"/>.
    /// </summary>
    /// <param name="provider">The node.</param>
    /// <param name="childDepth">The depth of the arguments.</param>
    /// <param name="walk">The walk state (work stack, item budget, first-child slot).</param>
    private static void PushArguments(IArgumentProvider provider, int childDepth, ref Walk walk)
    {
        int count = provider.ArgumentCount;
        for (int i = 0; i < count && !walk.Exhausted; i++)
        {
            Push(provider.GetArgument(i), childDepth, ref walk);
        }
    }

    /// <summary>Pushes every element of a child list.</summary>
    /// <typeparam name="T">The child type.</typeparam>
    /// <param name="children">The children.</param>
    /// <param name="childDepth">Their depth.</param>
    /// <param name="walk">The walk state (work stack, item budget, first-child slot).</param>
    private static void PushAll<T>(ReadOnlyCollection<T> children, int childDepth, ref Walk walk) where T : class
    {
        for (int i = 0; i < children.Count && !walk.Exhausted; i++)
        {
            Push(children[i], childDepth, ref walk);
        }
    }

    /// <summary>Pushes an optional child.</summary>
    /// <param name="child">The child, or <see langword="null"/> (ignored).</param>
    /// <param name="childDepth">Its depth.</param>
    /// <param name="walk">The walk state (work stack, item budget, first-child slot).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Push(object? child, int childDepth, ref Walk walk)
    {
        if (child is null) return;
        if (--walk.Remaining < 0)
        {
            if (walk.Capped)
            {
                walk.Exhausted = true;
                return;
            }

            if (!walk.Pausable) throw new NodeBudgetExhaustedException();
        }
        if (walk.TakeFirst && walk.First is null)
        {
            walk.First = child;
            return;
        }

        if (walk.Count == walk.Frames.Length) Array.Resize(ref walk.Frames, walk.Frames.Length * 2);
        walk.Frames[walk.Count++] = new Frame(child, childDepth);
    }

    /// <summary>Private signal of <see cref="Push"/>: the item budget of <see cref="Validate"/> is exhausted.</summary>
    /// <remarks>Thrown only on the refusal path of the historical walk and always caught there: it never escapes.</remarks>
    private sealed class NodeBudgetExhaustedException : Exception
    {
    }
}
