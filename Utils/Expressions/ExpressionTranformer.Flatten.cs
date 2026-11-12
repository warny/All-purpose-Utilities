using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Reflection.BindingFlags;

namespace Utils.Expressions;

/// <summary>
/// The <see cref="FlattenAttribute"/> part of <see cref="ExpressionTransformer"/>: discovery and validation of the flatten rules,
/// iterative extraction of an arithmetic chain from the raw tree, invocation of the rules, and the historical fallback when
/// every rule declines (see <c>docs/expressions/FlattenArchitecture.md</c>).
/// </summary>
public abstract partial class ExpressionTransformer
{
    /// <summary>The number of operators a chain can cross (<see cref="FlattenOperatorIndex"/>).</summary>
    private const int FlattenOperatorCount = 5;

    /// <summary>
    /// Maps a flattenable operator to its slot in <see cref="TransformPlan"/>'s flatten index: <see cref="ExpressionType.Add"/>,
    /// <see cref="ExpressionType.Subtract"/>, <see cref="ExpressionType.Negate"/>, <see cref="ExpressionType.Multiply"/>,
    /// <see cref="ExpressionType.Divide"/>; <c>-1</c> for any other node type.
    /// </summary>
    /// <param name="nodeType">The node type.</param>
    /// <returns>The slot, or <c>-1</c>.</returns>
    private static int FlattenOperatorIndex(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Add => 0,
        ExpressionType.Subtract => 1,
        ExpressionType.Negate => 2,
        ExpressionType.Multiply => 3,
        ExpressionType.Divide => 4,
        _ => -1,
    };

    /// <summary>Gets the family of a flattenable operator.</summary>
    /// <param name="nodeType">An operator for which <see cref="FlattenOperatorIndex"/> is not negative.</param>
    /// <returns>The family of the operator.</returns>
    private static FlattenFamily FamilyOf(ExpressionType nodeType)
        => nodeType is ExpressionType.Multiply or ExpressionType.Divide ? FlattenFamily.Multiplicative : FlattenFamily.Additive;

    /// <summary>The two families of operators a chain can cross.</summary>
    private enum FlattenFamily
    {
        /// <summary><see cref="ExpressionType.Add"/>, <see cref="ExpressionType.Subtract"/>, <see cref="ExpressionType.Negate"/>.</summary>
        Additive,

        /// <summary><see cref="ExpressionType.Multiply"/>, <see cref="ExpressionType.Divide"/>.</summary>
        Multiplicative,
    }

    /// <summary>
    /// The flatten rules that declare one same set of operators, in declaration order. Every operator belongs to at most one
    /// group, so a chain is extracted once whatever the number of rules that receive it. Immutable once the plan is built.
    /// </summary>
    private sealed class FlattenGroup
    {
        /// <summary>Initializes a group.</summary>
        /// <param name="family">The family of the operators.</param>
        /// <param name="operations">The distinct operators of the group, in the order of the first rule's declaration.</param>
        /// <param name="mask">The bit set of the operators (bit <see cref="FlattenOperatorIndex"/>).</param>
        /// <param name="rules">The rules, in declaration order.</param>
        public FlattenGroup(FlattenFamily family, ExpressionType[] operations, int mask, TransformRule[] rules)
        {
            Family = family;
            Operations = operations;
            Mask = mask;
            Rules = rules;
        }

        /// <summary>Gets the family of the operators.</summary>
        public FlattenFamily Family { get; }

        /// <summary>Gets the distinct operators of the group.</summary>
        public ExpressionType[] Operations { get; }

        /// <summary>Gets the bit set of the operators.</summary>
        public int Mask { get; }

        /// <summary>Gets the rules of the group, in declaration order.</summary>
        public TransformRule[] Rules { get; }

        /// <summary>Gets whether the operators are the additive ones.</summary>
        public bool IsAdditive => Family == FlattenFamily.Additive;
    }

    /// <summary>Whether the operator bit set <paramref name="mask"/> declares <paramref name="nodeType"/>.</summary>
    /// <param name="mask">A bit set of operators (bit <see cref="FlattenOperatorIndex"/>).</param>
    /// <param name="nodeType">The node type of a framework binary or unary node.</param>
    /// <returns><see langword="true"/> when the operator is declared.</returns>
    private static bool MaskDeclares(int mask, ExpressionType nodeType)
    {
        int index = FlattenOperatorIndex(nodeType);
        return index >= 0 && (mask & (1 << index)) != 0;
    }

    /// <summary>Creates the exception reported for an invalid <see cref="FlattenAttribute"/> rule, when the plan is built.</summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="method">The rule method.</param>
    /// <param name="reason">Why the rule is invalid.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidOperationException InvalidFlattenRule(Type transformerType, MethodInfo method, string reason)
        => new($"The [Flatten] rule '{method.DeclaringType?.FullName}.{method.Name}' of transformer '{transformerType.FullName}' is invalid: {reason}.");

    /// <summary>
    /// Rejects a static method marked with <see cref="FlattenAttribute"/>: the plan only holds instance rules, so such a method
    /// would otherwise be silently ignored.
    /// </summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <exception cref="InvalidOperationException">A static method is marked with <see cref="FlattenAttribute"/>.</exception>
    private static void ThrowOnStaticFlattenRule(Type transformerType)
    {
        foreach (MethodInfo method in transformerType.GetMethods(Public | NonPublic | Static | FlattenHierarchy))
        {
            if (method.IsDefined(typeof(FlattenAttribute), inherit: true))
            {
                throw InvalidFlattenRule(transformerType, method, "a flatten rule must be an instance method");
            }
        }
    }

    /// <summary>
    /// Validates the <see cref="FlattenAttribute"/> rules of a transformer type and groups them by operator set (see
    /// <see cref="FlattenGroup"/>). Every check runs once, here, so an invalid rule fails the construction of the transformer
    /// instead of an invocation.
    /// </summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="methods">The instance methods marked with <see cref="FlattenAttribute"/>, in <c>GetMethods</c> order.</param>
    /// <param name="rules">The rules built for <paramref name="methods"/>, in the same order.</param>
    /// <returns>The groups, in the order of their first rule.</returns>
    /// <exception cref="InvalidOperationException">A rule is invalid, or two rules declare overlapping but different operator sets.</exception>
    private static FlattenGroup[] BuildFlattenGroups(Type transformerType, List<MethodInfo> methods, out TransformRule[] rules)
    {
        rules = new TransformRule[methods.Count];
        var groupRules = new List<List<TransformRule>>();
        var groupOperations = new List<ExpressionType[]>();
        var groupMasks = new List<int>();

        for (int i = 0; i < methods.Count; i++)
        {
            MethodInfo method = methods[i];
            FlattenAttribute attribute = method.GetCustomAttributes<FlattenAttribute>(inherit: true).First();
            (ExpressionType[] operations, int mask) = ValidateFlattenOperations(transformerType, method, attribute);
            TransformRule rule = BuildFlattenRule(transformerType, method);
            rules[i] = rule;

            int group = groupMasks.IndexOf(mask);
            if (group < 0)
            {
                foreach (int existing in groupMasks)
                {
                    if ((existing & mask) != 0)
                    {
                        throw InvalidFlattenRule(transformerType, method, "its operators overlap those of another flatten rule without being the same set; rules sharing an operator must declare exactly the same operators");
                    }
                }

                groupMasks.Add(mask);
                groupOperations.Add(operations);
                groupRules.Add([]);
                group = groupMasks.Count - 1;
            }

            groupRules[group].Add(rule);
        }

        var groups = new FlattenGroup[groupMasks.Count];
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i] = new FlattenGroup(FamilyOf(groupOperations[i][0]), groupOperations[i], groupMasks[i], groupRules[i].ToArray());
        }

        return groups;
    }

    /// <summary>Validates the operators declared by a flatten rule.</summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="method">The rule method.</param>
    /// <param name="attribute">Its attribute.</param>
    /// <returns>The distinct operators, in declaration order, and their bit set.</returns>
    /// <exception cref="InvalidOperationException">No operator, an operator that cannot be flattened, or operators of both families.</exception>
    private static (ExpressionType[] Operations, int Mask) ValidateFlattenOperations(Type transformerType, MethodInfo method, FlattenAttribute attribute)
    {
        if (attribute.Operations.Count == 0)
        {
            throw InvalidFlattenRule(transformerType, method, "it declares no operator");
        }

        var operations = new List<ExpressionType>();
        int mask = 0;
        foreach (ExpressionType operation in attribute.Operations)
        {
            int index = FlattenOperatorIndex(operation);
            if (index < 0)
            {
                throw InvalidFlattenRule(transformerType, method, $"'{operation}' cannot be flattened (only Add, Subtract, Negate, Multiply and Divide can)");
            }

            if (FamilyOf(operation) != FamilyOf(attribute.Operations[0]))
            {
                throw InvalidFlattenRule(transformerType, method, "it mixes additive (Add, Subtract, Negate) and multiplicative (Multiply, Divide) operators");
            }

            if ((mask & (1 << index)) == 0)
            {
                mask |= 1 << index;
                operations.Add(operation);
            }
        }

        return (operations.ToArray(), mask);
    }

    /// <summary>
    /// Builds the <see cref="TransformRule"/> of a flatten rule after checking its shape:
    /// <c>Expression? Rule(FlattenedTerm[])</c> or <c>Expression? Rule(Expression, FlattenedTerm[])</c>.
    /// </summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="method">The rule method.</param>
    /// <returns>The rule, of kind <see cref="InvocationKind.Flattened"/>.</returns>
    /// <exception cref="InvalidOperationException">The method's shape is not one of the two supported signatures.</exception>
    private static TransformRule BuildFlattenRule(Type transformerType, MethodInfo method)
    {
        if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
        {
            throw InvalidFlattenRule(transformerType, method, "a flatten rule cannot be generic");
        }

        if (!_typeOfExpression.IsAssignableFrom(method.ReturnType))
        {
            throw InvalidFlattenRule(transformerType, method, "a flatten rule must return an Expression");
        }

        ParameterInfo[] parameterInfos = method.GetParameters();
        bool validShape = parameterInfos.Length switch
        {
            1 => parameterInfos[0].ParameterType == typeof(FlattenedTerm[]),
            2 => parameterInfos[0].ParameterType == typeof(Expression) && parameterInfos[1].ParameterType == typeof(FlattenedTerm[]),
            _ => false,
        };
        if (!validShape)
        {
            throw InvalidFlattenRule(transformerType, method, "its parameters must be (FlattenedTerm[] terms) or (Expression source, FlattenedTerm[] terms)");
        }

        var parameters = new TransformParameter[parameterInfos.Length];
        for (int i = 0; i < parameterInfos.Length; i++)
        {
            // A constraint on a flatten parameter would never be evaluated: reject it rather than ignore it. IsDefined reads the
            // attribute data (inherited ones included) without constructing a custom attribute.
            if (Attribute.IsDefined(parameterInfos[i], typeof(ExpressionSignatureAttribute), inherit: true))
            {
                throw InvalidFlattenRule(transformerType, method, "the parameters of a flatten rule cannot carry an ExpressionSignatureAttribute");
            }

            parameters[i] = new TransformParameter(parameterInfos[i].ParameterType, null, null);
        }

        bool fastInvokerEligible = DetermineFastInvokerEligibility(method, parameters, InvocationKind.Flattened);
        return new TransformRule(method, null, parameters, InvocationKind.Flattened, returnsExpression: true, fastInvokerEligible);
    }

    /// <summary>Builds the trace identity of every flatten rule, numbered in their own declaration order.</summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="rules">The flatten rules, in declaration order.</param>
    /// <param name="nameCounts">The number of rules (of both kinds) per method name.</param>
    private static void AssignFlattenRuleInfos(Type transformerType, TransformRule[] rules, Dictionary<string, int> nameCounts)
    {
        for (int i = 0; i < rules.Length; i++)
        {
            TransformRule rule = rules[i];
            IReadOnlyList<ExpressionType> operations = rule.Method.GetCustomAttributes<FlattenAttribute>(inherit: true).First().Operations;
            rule.Info = new ExpressionTransformRuleInfo(rule.Method, transformerType, operations[0], i, nameCounts[rule.Method.Name] > 1, operations);
        }
    }

    /// <summary>
    /// Decides whether an operator node of a declared flatten operator may be crossed by a chain (structural crossing only: it
    /// grants no permission to reassociate, drop or move anything). The default accepts only an ordinary intrinsic operator:
    /// no operator method (so no user-defined or <see langword="decimal"/> operator), and a result of one of the runtime's
    /// primitive arithmetic types (<see langword="int"/>, <see langword="uint"/>, <see langword="long"/>,
    /// <see langword="ulong"/>, <see langword="short"/>, <see langword="ushort"/>, <see langword="float"/>,
    /// <see langword="double"/>), compared by reference, which excludes the lifted (nullable) operators and any user
    /// <see cref="Type"/> object. Checked operators have their own node types and are never declared.
    /// </summary>
    /// <param name="node">A framework <see cref="BinaryExpression"/> or <see cref="UnaryExpression"/> whose node type the group declares.</param>
    /// <returns><see langword="true"/> when the chain may cross <paramref name="node"/>; otherwise it is an operand of the chain.</returns>
    /// <remarks>
    /// <c>internal virtual</c>, not <c>protected</c>: only a transformer of this assembly (the simplifier) may widen it. It must
    /// read no member of the node's operands: an operand may be an extension node (ETC R4).
    /// </remarks>
    internal virtual bool CanFlattenThrough(Expression node) => node switch
    {
        BinaryExpression binary => binary.Method is null && IsIntrinsicArithmeticType(binary.Type),
        UnaryExpression unary => unary.Method is null && IsIntrinsicArithmeticType(unary.Type),
        _ => false,
    };

    /// <summary>
    /// Whether <paramref name="type"/> is, by reference, one of the runtime's primitive types with intrinsic arithmetic
    /// operators (see <see cref="CanFlattenThrough"/>). No member of <paramref name="type"/> is read.
    /// </summary>
    /// <param name="type">The result type of an operator node.</param>
    /// <returns><see langword="true"/> for an intrinsic arithmetic type.</returns>
    private static bool IsIntrinsicArithmeticType(Type type)
        => ReferenceEquals(type, typeof(double))
            || ReferenceEquals(type, typeof(int))
            || ReferenceEquals(type, typeof(long))
            || ReferenceEquals(type, typeof(float))
            || ReferenceEquals(type, typeof(uint))
            || ReferenceEquals(type, typeof(ulong))
            || ReferenceEquals(type, typeof(short))
            || ReferenceEquals(type, typeof(ushort));

    /// <summary>
    /// Whether a chain of the operators <paramref name="mask"/> crosses <paramref name="node"/>: a framework binary or unary
    /// node (type tests first, so an extension node is never read) of a declared operator that <see cref="CanFlattenThrough"/>
    /// accepts.
    /// </summary>
    /// <param name="node">A node of the raw tree.</param>
    /// <param name="mask">The operator bit set of the chain's group.</param>
    /// <returns><see langword="true"/> when the chain crosses the node.</returns>
    internal bool IsCrossedByChain(Expression node, int mask)
        => node is BinaryExpression or UnaryExpression && MaskDeclares(mask, node.NodeType) && CanFlattenThrough(node);

    /// <summary>
    /// One node of an extracted chain, in pre-order: a crossed operator node, or an operand. Data only.
    /// </summary>
    /// <param name="Node">The node of the raw tree.</param>
    /// <param name="Crossed">Whether the chain crosses it (otherwise it is an operand).</param>
    private readonly record struct ChainEntry(Expression Node, bool Crossed);

    /// <summary>
    /// An arithmetic chain extracted from the raw tree: the terms given to the rules and the provenance needed to rebuild the
    /// chain faithfully (the crossed nodes, their operators, methods, types and grouping, in pre-order). Built once per
    /// extraction; the terms array is never handed to a rule (each rule receives a copy).
    /// </summary>
    private sealed class FlattenedChain
    {
        /// <summary>The crossed nodes and operands, in pre-order.</summary>
        private readonly ChainEntry[] _entries;

        /// <summary>Initializes a chain.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="entries">The crossed nodes and operands, in pre-order.</param>
        /// <param name="terms">The terms, in operand order.</param>
        private FlattenedChain(Expression source, ChainEntry[] entries, FlattenedTerm[] terms)
        {
            Source = source;
            _entries = entries;
            Terms = terms;
        }

        /// <summary>Gets the root of the chain, as it appears in the raw tree.</summary>
        public Expression Source { get; }

        /// <summary>Gets the terms, in operand order (the pristine copy kept by the engine).</summary>
        public FlattenedTerm[] Terms { get; }

        /// <summary>Gets the operator nodes the chain crosses, root first, in pre-order (with repetitions for shared nodes).</summary>
        public IEnumerable<Expression> CrossedNodes => _entries.Where(static entry => entry.Crossed).Select(static entry => entry.Node);

        /// <summary>
        /// Extracts the chain rooted at <paramref name="root"/> with an explicit work stack (no recursion, whatever the depth of
        /// the chain), counting one structural visit per node examined. Every occurrence of a shared node is kept.
        /// </summary>
        /// <param name="transformer">The transformer, which decides which operator nodes are crossed.</param>
        /// <param name="root">The root of the chain; the caller checked that it is crossed.</param>
        /// <param name="mask">The operator bit set of the chain's group.</param>
        /// <param name="additive">Whether the operators are the additive ones (otherwise the multiplicative ones).</param>
        /// <returns>The extracted chain.</returns>
        /// <exception cref="ExpressionResourceLimitException">The structural budget is exhausted.</exception>
        internal static FlattenedChain Extract(ExpressionTransformer transformer, Expression root, int mask, bool additive)
        {
            var entries = new List<ChainEntry>();
            var terms = new List<FlattenedTerm>();
            var work = new Stack<(Expression Node, bool Positive)>();
            work.Push((root, true));

            while (work.Count > 0)
            {
                (Expression node, bool positive) = work.Pop();
                ExpressionResourceBudget.ConsumeStructuralVisit();

                if (!ReferenceEquals(node, root) && !transformer.IsCrossedByChain(node, mask))
                {
                    entries.Add(new ChainEntry(node, Crossed: false));
                    ExpressionType operation = additive
                        ? positive ? ExpressionType.Add : ExpressionType.Subtract
                        : positive ? ExpressionType.Multiply : ExpressionType.Divide;
                    terms.Add(new FlattenedTerm(operation, node, terms.Count));
                    continue;
                }

                entries.Add(new ChainEntry(node, Crossed: true));
                if (node is UnaryExpression unary)
                {
                    // Negate: the operand's sign is inverted.
                    work.Push((unary.Operand, !positive));
                    continue;
                }

                // The right operand is pushed first so that the left one is examined first (pre-order, left to right).
                // Subtract and Divide invert their right operand.
                BinaryExpression binary = (BinaryExpression)node;
                bool rightInverted = binary.NodeType is ExpressionType.Subtract or ExpressionType.Divide;
                work.Push((binary.Right, rightInverted ? !positive : positive));
                work.Push((binary.Left, positive));
            }

            return new FlattenedChain(root, entries.ToArray(), terms.ToArray());
        }

        /// <summary>
        /// Rebuilds the chain with the given operands, in operand order: the same operator nodes, with their node types,
        /// methods, lifting and result types, and the same grouping (the very parentheses of the source). Iterative.
        /// </summary>
        /// <param name="operands">One operand per term, in <see cref="FlattenedTerm.SourceIndex"/> order.</param>
        /// <returns><see cref="Source"/> itself when every operand is the original one; otherwise the rebuilt chain.</returns>
        /// <exception cref="ArgumentException">The operand count differs from the term count.</exception>
        /// <exception cref="InvalidOperationException">An operand's type does not fit its operator (thrown by the expression factories).</exception>
        internal Expression Rebuild(IReadOnlyList<Expression> operands)
        {
            ArgumentNullException.ThrowIfNull(operands);
            if (operands.Count != Terms.Length) throw new ArgumentException($"Expected {Terms.Length} operands, got {operands.Count}.", nameof(operands));

            bool unchanged = true;
            for (int i = 0; i < operands.Count && unchanged; i++) unchanged = ReferenceEquals(operands[i], Terms[i].Operand);
            if (unchanged) return Source;

            // Reverse pre-order: a node is met after its whole right subtree, then its whole left subtree, so the left result
            // is on top of the stack, the right one below it.
            var values = new Stack<Expression>();
            int operand = operands.Count;
            for (int i = _entries.Length - 1; i >= 0; i--)
            {
                ChainEntry entry = _entries[i];
                if (!entry.Crossed)
                {
                    values.Push(operands[--operand]);
                }
                else if (entry.Node is UnaryExpression unary)
                {
                    values.Push(CopyUnaryExpression(unary, values.Pop()));
                }
                else
                {
                    Expression left = values.Pop();
                    Expression right = values.Pop();
                    values.Push(CopyBinaryExpression((BinaryExpression)entry.Node, left, right));
                }
            }

            return values.Pop();
        }
    }

    /// <summary>
    /// Rebuilds the chain rooted at <paramref name="source"/> with new operands: the very operator nodes the flatten rules of
    /// this transformer cross, with their node types, methods and result types, and the same grouping (parentheses) as the
    /// source, so that a rule which transforms every operand can keep the original association instead of rebuilding it from
    /// the signs alone (which <c>(a / b) * c</c> and <c>a / (b / c)</c> share).
    /// </summary>
    /// <param name="source">The root of a chain, as received by a flatten rule.</param>
    /// <param name="operands">One operand per term, in <see cref="FlattenedTerm.SourceIndex"/> order.</param>
    /// <returns><paramref name="source"/> itself when every operand is the original one; otherwise the rebuilt chain.</returns>
    /// <remarks>
    /// The chain is extracted again from <paramref name="source"/> (linear, counted as structural work). When no expression
    /// operation runs on the thread, this call starts one with <see cref="ResourceLimits"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="operands"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> is not the root of a chain of this transformer's flatten rules, or the operand count differs
    /// from the term count.
    /// </exception>
    /// <exception cref="InvalidOperationException">An operand's type does not fit its operator.</exception>
    protected Expression RebuildFlattenedChain(Expression source, IReadOnlyList<Expression> operands)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operands);
        if (!ExpressionResourceBudget.IsActive || (!JoinsRunningOperation && !ReferenceEquals(ExpressionResourceBudget.Owner, this)))
        {
            return ExpressionResourceBudget.Run(
                ResourceLimits,
                this,
                source,
                null,
                (Transformer: this, Source: source, Operands: operands),
                static state => state.Transformer.RebuildFlattenedChainCore(state.Source, state.Operands));
        }

        return RebuildFlattenedChainCore(source, operands);
    }

    /// <summary>The body of <see cref="RebuildFlattenedChain"/>, inside an operation.</summary>
    /// <param name="source">The root of a chain.</param>
    /// <param name="operands">The new operands.</param>
    /// <returns>The rebuilt chain.</returns>
    private Expression RebuildFlattenedChainCore(Expression source, IReadOnlyList<Expression> operands)
    {
        FlattenGroup? group = source is BinaryExpression or UnaryExpression ? _transformPlan.GetFlattenGroup(source.NodeType) : null;
        if (group is null || !CanFlattenThrough(source))
        {
            throw new ArgumentException("The expression is not the root of a chain of this transformer's flatten rules.", nameof(source));
        }

        return FlattenedChain.Extract(this, source, group.Mask, group.IsAdditive).Rebuild(operands);
    }

    /// <summary>A crossed node of a chain whose flatten rules all declined, for one transformer (see <see cref="_refusedChainNodes"/>).</summary>
    /// <param name="Transformer">The transformer whose rules declined.</param>
    /// <param name="Node">The crossed node.</param>
    private readonly record struct RefusedChainKey(ExpressionTransformer Transformer, Expression Node);

    /// <summary>Compares <see cref="RefusedChainKey"/> values by the identity of both references, never calling a node's own members.</summary>
    private sealed class RefusedChainKeyComparer : IEqualityComparer<RefusedChainKey>
    {
        /// <summary>The single instance.</summary>
        public static readonly RefusedChainKeyComparer Instance = new();

        /// <inheritdoc/>
        public bool Equals(RefusedChainKey x, RefusedChainKey y)
            => ReferenceEquals(x.Transformer, y.Transformer) && ReferenceEquals(x.Node, y.Node);

        /// <inheritdoc/>
        public int GetHashCode(RefusedChainKey key)
            => HashCode.Combine(RuntimeHelpers.GetHashCode(key.Transformer), RuntimeHelpers.GetHashCode(key.Node));
    }

    /// <summary>
    /// The crossed nodes of the chains being processed historically after all their flatten rules declined, per transformer,
    /// with an occurrence count (a shared node can be crossed several times). While a node is here, reaching it again through
    /// <see cref="TransformCore(Expression)"/> does not extract a chain from it again: the fallback stays historical and the
    /// extraction is never repeated at every level. Per thread, like the other engine state, since one transformer instance
    /// may serve several threads; entries are removed in <c>finally</c>.
    /// </summary>
    [ThreadStatic]
    private static Dictionary<RefusedChainKey, int>? _refusedChainNodes;

    /// <summary>Whether <paramref name="node"/> is a crossed node of a chain this transformer is processing historically.</summary>
    /// <param name="node">The node about to be transformed.</param>
    /// <returns><see langword="true"/> when its flatten rules already declined it.</returns>
    private bool IsRefusedChainNode(Expression node)
        => _refusedChainNodes is { Count: > 0 } refused && refused.ContainsKey(new RefusedChainKey(this, node));

    /// <summary>
    /// <see cref="TransformCore(Expression)"/> for a transformer that declares flatten rules. When <paramref name="e"/> is the
    /// root of a chain (a crossed node of a declared operator, not already declined), the chain is extracted from the raw tree
    /// and given to the rules of its group, in declaration order; the first non-<see langword="null"/> result is the result of
    /// the node. Otherwise, and when every rule declines, the node is transformed exactly as without flatten rules.
    /// </summary>
    /// <param name="e">The node to transform.</param>
    /// <param name="trace">The tracing state of the operation, or <see langword="null"/>.</param>
    /// <param name="prepared">The node rebuilt by the traversal (for the trace), or <paramref name="e"/> when a flatten rule applied.</param>
    /// <param name="boundary">Whether the node was a transformation boundary.</param>
    /// <returns>The result of the node.</returns>
    private Expression TransformWithFlatten(Expression e, ExpressionTraceSession? trace, out Expression prepared, out bool boundary)
    {
        // A node reached from a rule admitted outside the effect gate gets its own admission, as in TransformPrepared.
        if (_ruleRunsOutsideGate)
        {
            _ruleRunsOutsideGate = false;
            try
            {
                return TransformWithFlatten(e, trace, out prepared, out boundary);
            }
            finally
            {
                _ruleRunsOutsideGate = true;
            }
        }

        // Type tests first: the node type of an extension node is never read (ETC R4).
        FlattenGroup? group = e is BinaryExpression or UnaryExpression ? _transformPlan.GetFlattenGroup(e.NodeType) : null;
        if (group is null || !CanFlattenThrough(e) || IsRefusedChainNode(e))
        {
            return TransformHistorically(e, trace, out prepared, out boundary);
        }

        // The effect gate, on the raw root: inert operands make the whole chain inert. Flatten rules are never audited, so a
        // refusal leaves the node to the historical path (where audited signature rules may still run outside the gate).
        if (!AllowsRules(e))
        {
            if (trace is { Detailed: true })
            {
                foreach (TransformRule rule in group.Rules) trace.RuleRejected(rule.Info, e, ExpressionRuleRejection.EffectGate);
            }

            return TransformHistorically(e, trace, out prepared, out boundary);
        }

        FlattenedChain chain = FlattenedChain.Extract(this, e, group.Mask, group.IsAdditive);
        if (trace is { Detailed: true }) trace.ChainFlattened(e);

        // The node is processed here: one transformation visit, before any rule runs (the historical fallback counts its own).
        ExpressionResourceBudget.ConsumeTransformVisit();
        foreach (TransformRule rule in group.Rules)
        {
            if (trace is { Detailed: true }) trace.RuleCandidate(rule.Info, e);
            trace?.BeginRule(rule.Info);

            // Every rule receives its own copy: a declining rule that reorders or overwrites its array never affects the next one.
            Expression? result = InvokeFlattenRule(rule, e, (FlattenedTerm[])chain.Terms.Clone());
            if (result is not null)
            {
                trace?.RuleApplied(rule.Info, e, result);
                prepared = e;
                boundary = false;
                return result;
            }

            trace?.RuleDeclined(rule.Info, e, ExpressionRuleRejection.Declined);
        }

        return TransformDeclinedChain(e, chain, trace, out prepared, out boundary);
    }

    /// <summary>The historical part of <see cref="TransformCore(Expression)"/>: preparation, signature rules and finalization.</summary>
    /// <param name="e">The node to transform.</param>
    /// <param name="trace">The tracing state of the operation, or <see langword="null"/>.</param>
    /// <param name="prepared">The node rebuilt by the traversal.</param>
    /// <param name="boundary">Whether the node was a transformation boundary.</param>
    /// <returns>The result of the node.</returns>
    private Expression TransformHistorically(Expression e, ExpressionTraceSession? trace, out Expression prepared, out bool boundary)
    {
        TransformContext context = PrepareTransform(e);
        prepared = context.Expression;
        boundary = context.IsBoundary;
        return TransformPrepared(context, trace);
    }

    /// <summary>
    /// Processes a chain whose flatten rules all declined exactly as without flatten rules (<see cref="TransformHistorically"/>),
    /// with its crossed nodes marked as declined for the duration, so that the descent never extracts a sub-chain of it again.
    /// The marks are removed in <c>finally</c>.
    /// </summary>
    /// <param name="e">The root of the chain.</param>
    /// <param name="chain">The extracted chain.</param>
    /// <param name="trace">The tracing state of the operation, or <see langword="null"/>.</param>
    /// <param name="prepared">The node rebuilt by the traversal.</param>
    /// <param name="boundary">Whether the node was a transformation boundary.</param>
    /// <returns>The result of the node.</returns>
    private Expression TransformDeclinedChain(Expression e, FlattenedChain chain, ExpressionTraceSession? trace, out Expression prepared, out bool boundary)
    {
        Dictionary<RefusedChainKey, int> refused = _refusedChainNodes ??= new Dictionary<RefusedChainKey, int>(RefusedChainKeyComparer.Instance);
        foreach (Expression node in chain.CrossedNodes)
        {
            var key = new RefusedChainKey(this, node);
            refused[key] = refused.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        try
        {
            return TransformHistorically(e, trace, out prepared, out boundary);
        }
        finally
        {
            foreach (Expression node in chain.CrossedNodes)
            {
                var key = new RefusedChainKey(this, node);
                int count = refused[key];
                if (count == 1) refused.Remove(key);
                else refused[key] = count - 1;
            }
        }
    }

    /// <summary>
    /// Invokes a flatten rule with <see cref="MethodBase.Invoke(object, object[])"/>'s exception contract, like the signature
    /// rules (an exception of the rule body is wrapped in a new <see cref="TargetInvocationException"/>; a resource failure
    /// propagates unwrapped).
    /// </summary>
    /// <param name="rule">The flatten rule.</param>
    /// <param name="source">The root of the chain, for a rule that declares it.</param>
    /// <param name="terms">The rule's own copy of the terms.</param>
    /// <returns>The rule's result; <see langword="null"/> when it declines.</returns>
    private Expression? InvokeFlattenRule(TransformRule rule, Expression source, FlattenedTerm[] terms)
    {
        bool withSource = rule.Parameters.Length == 2;
        if (rule.FastInvoker is MethodInvoker invoker)
        {
            try
            {
                return (Expression?)(withSource ? invoker.Invoke(this, source, terms) : invoker.Invoke(this, terms));
            }
            catch (Exception ex) when (!IsResourceFailure(ex))
            {
                // See InvokeSingleRule: wraps exactly like MethodBase.Invoke.
                throw new TargetInvocationException(ex);
            }
        }

        return (Expression?)InvokeRuleReflectively(rule, withSource ? [source, terms] : [terms]);
    }
}
