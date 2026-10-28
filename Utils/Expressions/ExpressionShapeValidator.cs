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
/// <b>Decision walk.</b> <see cref="Validate"/> walks the unfolded tree (a shared subtree is walked once per use, exactly
/// as the engine does) and counts every item it pushes: it stops as soon as the count exceeds the node limit or a path
/// exceeds the depth limit. Its time and memory are therefore O(min(logical items, node limit)), whatever the sharing.
/// A partial walk is never a success: it either completes under both limits or throws. The per-thread work stack is
/// reused, so a validation allocates nothing in the steady state.
/// </para>
/// <para>
/// <b>Exact measurement.</b> <see cref="Measure"/> computes distinct and logical counts and depth by post-order dynamic
/// programming over distinct items (reference identity), in O(distinct items) without unfolding, with saturating
/// arithmetic. It reports the observed values of a refusal and serves diagnostics; it is not on the hot path.
/// </para>
/// </remarks>
internal static class ExpressionShapeValidator
{
    /// <summary>Initial capacity of the per-thread work stack.</summary>
    private const int InitialStackCapacity = 64;

    /// <summary>Distinct-item cap of the exact measurement made to report a refusal, as a multiple of the node limit.</summary>
    private const long RefusalMeasurementFactor = 4;

    /// <summary>The per-thread work stack of <see cref="Validate"/>; validation runs no user code, so it is never re-entered.</summary>
    [ThreadStatic]
    private static Frame[]? _frames;

    /// <summary>One pending item of the decision walk.</summary>
    /// <param name="Item">An <see cref="Expression"/> or a structural container.</param>
    /// <param name="Depth">The depth of <paramref name="Item"/> (the root has depth 1).</param>
    private readonly record struct Frame(object Item, int Depth);

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

        Frame[] frames = _frames ??= new Frame[InitialStackCapacity];
        int count = 0;
        int highWater = 1;

        // Every pushed item consumes one unit; the root already consumed its own.
        long remaining = maxNodes - 1;
        frames[count++] = new Frame(root, 1);
        try
        {
            while (count > 0)
            {
                Frame frame = frames[--count];
                if (frame.Depth > maxDepth)
                {
                    throw Refuse(root, ExpressionResourceLimitKind.Depth, maxDepth, maxNodes, frame.Depth);
                }

                PushChildren(frame.Item, frame.Depth + 1, ref frames, ref count, ref remaining);
                if (count > highWater) highWater = count;
            }
        }
        catch (NodeBudgetExhaustedException)
        {
            throw Refuse(root, ExpressionResourceLimitKind.Nodes, maxDepth, maxNodes, maxNodes == long.MaxValue ? maxNodes : maxNodes + 1);
        }
        finally
        {
            // Drop every reference written so far (popped slots included), so the reused stack keeps no tree alive;
            // a stack grown by a large input is not kept.
            Array.Clear(frames, 0, Math.Min(Math.Max(highWater, count), frames.Length));
            _frames = frames.Length > 4096 ? null : frames;
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
        var children = new Frame[InitialStackCapacity];
        long opaque = 0;
        long edgeBudget = maxDistinctItems;
        bool truncated = false;
        pending.Push((root, false));

        while (pending.Count > 0)
        {
            (object item, bool expanded) = pending.Pop();
            if (finalized.ContainsKey(item)) continue;

            int childCount = 0;
            bool known;
            if (!expanded)
            {
                try
                {
                    known = PushChildren(item, 0, ref children, ref childCount, ref edgeBudget);
                }
                catch (NodeBudgetExhaustedException)
                {
                    truncated = true;
                    break;
                }
            }
            else
            {
                long unbounded = long.MaxValue;
                known = PushChildren(item, 0, ref children, ref childCount, ref unbounded);
            }

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
    /// Pushes the children of one item onto <paramref name="frames"/>, dispatching on its runtime class (never on a
    /// virtual getter).
    /// </summary>
    /// <param name="item">An <see cref="Expression"/> or a structural container.</param>
    /// <param name="childDepth">The depth given to every pushed child.</param>
    /// <param name="frames">The work stack, grown when needed.</param>
    /// <param name="count">The number of frames in use.</param>
    /// <param name="remaining">The remaining item budget, decremented by every push.</param>
    /// <returns><see langword="false"/> when <paramref name="item"/> is of an unknown class (an opaque leaf).</returns>
    /// <remarks>
    /// One dispatch case per expression class or container, each a short list of pushes (node-type dispatch
    /// exception of <c>Utils/AGENTS.md</c>). The order of the pushes does not matter for the measured shape.
    /// </remarks>
    private static bool PushChildren(object item, int childDepth, ref Frame[] frames, ref int count, ref long remaining)
    {
        switch (item)
        {
            case BinaryExpression binary:
                Push(binary.Left, childDepth, ref frames, ref count, ref remaining);
                Push(binary.Right, childDepth, ref frames, ref count, ref remaining);
                Push(binary.Conversion, childDepth, ref frames, ref count, ref remaining);
                return true;
            case UnaryExpression unary:
                Push(unary.Operand, childDepth, ref frames, ref count, ref remaining);
                return true;
            case ConstantExpression or ParameterExpression or DefaultExpression or DebugInfoExpression:
                return true;
            case MethodCallExpression call:
                Push(call.Object, childDepth, ref frames, ref count, ref remaining);
                PushAll(call.Arguments, childDepth, ref frames, ref count, ref remaining);
                return true;
            case MemberExpression member:
                Push(member.Expression, childDepth, ref frames, ref count, ref remaining);
                return true;
            case InvocationExpression invocation:
                Push(invocation.Expression, childDepth, ref frames, ref count, ref remaining);
                PushAll(invocation.Arguments, childDepth, ref frames, ref count, ref remaining);
                return true;
            case LambdaExpression lambda:
                PushAll(lambda.Parameters, childDepth, ref frames, ref count, ref remaining);
                Push(lambda.Body, childDepth, ref frames, ref count, ref remaining);
                return true;
            case ConditionalExpression conditional:
                Push(conditional.Test, childDepth, ref frames, ref count, ref remaining);
                Push(conditional.IfTrue, childDepth, ref frames, ref count, ref remaining);
                Push(conditional.IfFalse, childDepth, ref frames, ref count, ref remaining);
                return true;
            case TypeBinaryExpression typeBinary:
                Push(typeBinary.Expression, childDepth, ref frames, ref count, ref remaining);
                return true;
            case NewExpression creation:
                PushAll(creation.Arguments, childDepth, ref frames, ref count, ref remaining);
                return true;
            case NewArrayExpression array:
                PushAll(array.Expressions, childDepth, ref frames, ref count, ref remaining);
                return true;
            case MemberInitExpression memberInit:
                Push(memberInit.NewExpression, childDepth, ref frames, ref count, ref remaining);
                PushAll(memberInit.Bindings, childDepth, ref frames, ref count, ref remaining);
                return true;
            case ListInitExpression listInit:
                Push(listInit.NewExpression, childDepth, ref frames, ref count, ref remaining);
                PushAll(listInit.Initializers, childDepth, ref frames, ref count, ref remaining);
                return true;
            case IndexExpression index:
                Push(index.Object, childDepth, ref frames, ref count, ref remaining);
                PushAll(index.Arguments, childDepth, ref frames, ref count, ref remaining);
                return true;
            case BlockExpression block:
                PushAll(block.Variables, childDepth, ref frames, ref count, ref remaining);
                PushAll(block.Expressions, childDepth, ref frames, ref count, ref remaining);
                return true;
            case TryExpression @try:
                Push(@try.Body, childDepth, ref frames, ref count, ref remaining);
                PushAll(@try.Handlers, childDepth, ref frames, ref count, ref remaining);
                Push(@try.Finally, childDepth, ref frames, ref count, ref remaining);
                Push(@try.Fault, childDepth, ref frames, ref count, ref remaining);
                return true;
            case SwitchExpression @switch:
                Push(@switch.SwitchValue, childDepth, ref frames, ref count, ref remaining);
                PushAll(@switch.Cases, childDepth, ref frames, ref count, ref remaining);
                Push(@switch.DefaultBody, childDepth, ref frames, ref count, ref remaining);
                return true;
            case LoopExpression loop:
                Push(loop.Body, childDepth, ref frames, ref count, ref remaining);
                return true;
            case GotoExpression @goto:
                Push(@goto.Value, childDepth, ref frames, ref count, ref remaining);
                return true;
            case LabelExpression label:
                Push(label.DefaultValue, childDepth, ref frames, ref count, ref remaining);
                return true;
            case RuntimeVariablesExpression runtimeVariables:
                PushAll(runtimeVariables.Variables, childDepth, ref frames, ref count, ref remaining);
                return true;
            case DynamicExpression dynamic:
                PushAll(dynamic.Arguments, childDepth, ref frames, ref count, ref remaining);
                return true;
            case CatchBlock handler:
                Push(handler.Variable, childDepth, ref frames, ref count, ref remaining);
                Push(handler.Filter, childDepth, ref frames, ref count, ref remaining);
                Push(handler.Body, childDepth, ref frames, ref count, ref remaining);
                return true;
            case SwitchCase @case:
                PushAll(@case.TestValues, childDepth, ref frames, ref count, ref remaining);
                Push(@case.Body, childDepth, ref frames, ref count, ref remaining);
                return true;
            case MemberAssignment assignment:
                Push(assignment.Expression, childDepth, ref frames, ref count, ref remaining);
                return true;
            case MemberMemberBinding memberBinding:
                PushAll(memberBinding.Bindings, childDepth, ref frames, ref count, ref remaining);
                return true;
            case MemberListBinding listBinding:
                PushAll(listBinding.Initializers, childDepth, ref frames, ref count, ref remaining);
                return true;
            case ElementInit initializer:
                PushAll(initializer.Arguments, childDepth, ref frames, ref count, ref remaining);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Pushes every element of a child list.</summary>
    /// <typeparam name="T">The child type.</typeparam>
    /// <param name="children">The children.</param>
    /// <param name="childDepth">Their depth.</param>
    /// <param name="frames">The work stack.</param>
    /// <param name="count">The number of frames in use.</param>
    /// <param name="remaining">The remaining item budget.</param>
    private static void PushAll<T>(ReadOnlyCollection<T> children, int childDepth, ref Frame[] frames, ref int count, ref long remaining) where T : class
    {
        for (int i = 0; i < children.Count; i++)
        {
            Push(children[i], childDepth, ref frames, ref count, ref remaining);
        }
    }

    /// <summary>Pushes an optional child.</summary>
    /// <param name="child">The child, or <see langword="null"/> (ignored).</param>
    /// <param name="childDepth">Its depth.</param>
    /// <param name="frames">The work stack, doubled when full.</param>
    /// <param name="count">The number of frames in use.</param>
    /// <param name="remaining">The remaining item budget; <see cref="NodeBudgetExhaustedException"/> is thrown when it goes negative.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Push(object? child, int childDepth, ref Frame[] frames, ref int count, ref long remaining)
    {
        if (child is null) return;
        if (--remaining < 0) throw new NodeBudgetExhaustedException();
        if (count == frames.Length) Array.Resize(ref frames, frames.Length * 2);
        frames[count++] = new Frame(child, childDepth);
    }

    /// <summary>Private signal of <see cref="Push"/>: the item budget of <see cref="Validate"/> is exhausted.</summary>
    /// <remarks>Thrown only on the refusal path and always caught by <see cref="Validate"/>: it never escapes.</remarks>
    private sealed class NodeBudgetExhaustedException : Exception
    {
    }
}
