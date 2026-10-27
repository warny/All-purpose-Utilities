using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ExpressionResourceProbe;

/// <summary>The measured shape of an expression graph.</summary>
/// <param name="DistinctNodes">Number of distinct node instances reached (a shared subtree counts once).</param>
/// <param name="LogicalNodes">Number of node occurrences when the graph is unfolded into a tree (a shared subtree counts once per use), saturated at <see cref="long.MaxValue"/>.</param>
/// <param name="Depth">Maximum number of nodes on a root-to-leaf path.</param>
/// <param name="FreeOrDeclaredParameters">Number of distinct <see cref="ParameterExpression"/> instances reached.</param>
/// <param name="OpaqueExtensions">Number of distinct extension nodes reached (counted as leaves, never inspected).</param>
/// <param name="Truncated">Whether the walk stopped because <c>maxDistinctNodes</c> was reached.</param>
internal readonly record struct ExpressionShape(long DistinctNodes, long LogicalNodes, int Depth, int FreeOrDeclaredParameters, int OpaqueExtensions, bool Truncated);

/// <summary>
/// Prototype of the non-recursive structural validator proposed by the ETC Security round: measures distinct nodes, logical
/// (unfolded) nodes and depth of an expression graph with an explicit stack, in time and memory linear in the number of
/// distinct nodes, so it cannot itself exhaust the call stack or explode on a shared DAG.
/// </summary>
/// <remarks>
/// Only framework-defined public properties are read. An extension node (<see cref="ExpressionType.Extension"/>, or any
/// node whose runtime class is not a known framework class) is a leaf: its <c>Reduce</c>, <c>CanReduce</c>,
/// <c>VisitChildren</c> and custom getters are never called. Its virtual <see cref="Expression.NodeType"/> getter is read
/// once, which is the only user code this meter can run (the same getter every dispatcher reads).
/// </remarks>
internal static class ExpressionShapeMeter
{
    /// <summary>Measures <paramref name="root"/>.</summary>
    /// <param name="root">The graph root.</param>
    /// <param name="maxDistinctNodes">Stop after this many distinct nodes (the result is then marked truncated).</param>
    /// <returns>The measured shape.</returns>
    public static ExpressionShape Measure(Expression root, long maxDistinctNodes = long.MaxValue)
    {
        // Post-order over distinct nodes: a node is finalized once all its children are finalized, so depth and
        // logical size are computed by dynamic programming without ever unfolding shared subtrees.
        var depth = new Dictionary<Expression, int>(ReferenceEqualityComparer.Instance);
        var logical = new Dictionary<Expression, BigInteger>(ReferenceEqualityComparer.Instance);
        var parameters = new HashSet<ParameterExpression>(ReferenceEqualityComparer.Instance);
        int extensions = 0;
        bool truncated = false;
        var stack = new Stack<(Expression Node, bool Expanded)>();
        stack.Push((root, false));
        var children = new List<Expression>();

        while (stack.Count > 0)
        {
            (Expression node, bool expanded) = stack.Pop();
            if (depth.ContainsKey(node)) continue;

            children.Clear();
            bool isExtension = !CollectChildren(node, children);
            if (!expanded)
            {
                if (depth.Count >= maxDistinctNodes)
                {
                    truncated = true;
                    break;
                }

                stack.Push((node, true));
                foreach (Expression child in children)
                {
                    if (!depth.ContainsKey(child)) stack.Push((child, false));
                }

                continue;
            }

            int maxChildDepth = 0;
            BigInteger size = BigInteger.One;
            foreach (Expression child in children)
            {
                // A child may be missing only when the walk was truncated; the loop then already stopped.
                maxChildDepth = Math.Max(maxChildDepth, depth[child]);
                size += logical[child];
            }

            depth[node] = maxChildDepth + 1;
            logical[node] = size;
            if (node is ParameterExpression parameter) parameters.Add(parameter);
            if (isExtension) extensions++;
        }

        BigInteger rootSize = logical.TryGetValue(root, out BigInteger value) ? value : BigInteger.Zero;
        long logicalNodes = rootSize > long.MaxValue ? long.MaxValue : (long)rootSize;
        return new ExpressionShape(depth.Count, logicalNodes, depth.TryGetValue(root, out int rootDepth) ? rootDepth : -1, parameters.Count, extensions, truncated);
    }

    /// <summary>Collects the expression children of one node, in <see cref="ExpressionVisitor"/> order, without running user code.</summary>
    /// <param name="node">The node.</param>
    /// <param name="children">Receives the non-null children.</param>
    /// <returns><see langword="false"/> when the node is an extension (opaque leaf); <see langword="true"/> otherwise.</returns>
    private static bool CollectChildren(Expression node, List<Expression> children)
    {
        switch (node)
        {
            case BinaryExpression binary:
                children.Add(binary.Left);
                children.Add(binary.Right);
                Add(children, binary.Conversion);
                return true;
            case UnaryExpression unary:
                Add(children, unary.Operand);
                return true;
            case ConstantExpression or ParameterExpression or DefaultExpression or DebugInfoExpression or RuntimeVariablesExpression:
                return true;
            case MethodCallExpression call:
                Add(children, call.Object);
                children.AddRange(call.Arguments);
                return true;
            case MemberExpression member:
                Add(children, member.Expression);
                return true;
            case InvocationExpression invocation:
                children.Add(invocation.Expression);
                children.AddRange(invocation.Arguments);
                return true;
            case LambdaExpression lambda:
                children.AddRange(lambda.Parameters);
                children.Add(lambda.Body);
                return true;
            case ConditionalExpression conditional:
                children.Add(conditional.Test);
                children.Add(conditional.IfTrue);
                children.Add(conditional.IfFalse);
                return true;
            case TypeBinaryExpression typeBinary:
                children.Add(typeBinary.Expression);
                return true;
            case NewExpression creation:
                children.AddRange(creation.Arguments);
                return true;
            case NewArrayExpression array:
                children.AddRange(array.Expressions);
                return true;
            case MemberInitExpression memberInit:
                children.Add(memberInit.NewExpression);
                foreach (MemberBinding binding in memberInit.Bindings) CollectBinding(binding, children);
                return true;
            case ListInitExpression listInit:
                children.Add(listInit.NewExpression);
                foreach (ElementInit initializer in listInit.Initializers) children.AddRange(initializer.Arguments);
                return true;
            case IndexExpression index:
                Add(children, index.Object);
                children.AddRange(index.Arguments);
                return true;
            case BlockExpression block:
                children.AddRange(block.Variables);
                children.AddRange(block.Expressions);
                return true;
            case TryExpression @try:
                children.Add(@try.Body);
                foreach (CatchBlock handler in @try.Handlers)
                {
                    Add(children, handler.Variable);
                    Add(children, handler.Filter);
                    children.Add(handler.Body);
                }

                Add(children, @try.Finally);
                Add(children, @try.Fault);
                return true;
            case SwitchExpression @switch:
                children.Add(@switch.SwitchValue);
                foreach (SwitchCase @case in @switch.Cases)
                {
                    children.AddRange(@case.TestValues);
                    children.Add(@case.Body);
                }

                Add(children, @switch.DefaultBody);
                return true;
            case LoopExpression loop:
                children.Add(loop.Body);
                return true;
            case GotoExpression @goto:
                Add(children, @goto.Value);
                return true;
            case LabelExpression label:
                Add(children, label.DefaultValue);
                return true;
            case DynamicExpression dynamic:
                children.AddRange(dynamic.Arguments);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Collects the expression children of a member binding, iteratively for nested bindings.</summary>
    /// <param name="root">The binding.</param>
    /// <param name="children">Receives the children.</param>
    private static void CollectBinding(MemberBinding root, List<Expression> children)
    {
        var pending = new Stack<MemberBinding>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case MemberAssignment assignment:
                    children.Add(assignment.Expression);
                    break;
                case MemberListBinding list:
                    foreach (ElementInit initializer in list.Initializers) children.AddRange(initializer.Arguments);
                    break;
                case MemberMemberBinding member:
                    foreach (MemberBinding nested in member.Bindings) pending.Push(nested);
                    break;
            }
        }
    }

    /// <summary>Adds an optional child.</summary>
    /// <param name="children">The child list.</param>
    /// <param name="child">The optional child.</param>
    private static void Add(List<Expression> children, Expression? child)
    {
        if (child is not null) children.Add(child);
    }
}
