using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Expressions;

/// <summary>
/// Provides a standard, behavior-preserving optimization pass over expression trees: local identities that are exact for
/// every input of the types they apply to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contract.</b> An optimized tree has exactly the type of its source and, once compiled, produces the same value (the
/// same bits for binary floating point, signed zeros and NaN payloads included, except that the hardware may quiet a
/// signaling NaN) or the same exception, with the same evaluations in the same order. A rule that cannot prove this for the
/// node it inspects leaves the node unchanged. The rules and their guards are listed in
/// <c>docs/expressions/ExpressionOptimiser.md</c>:
/// </para>
/// <list type="bullet">
///   <item><description><c>x + 0</c>, <c>0 + x</c>, <c>x - 0</c>, <c>x * 1</c>, <c>1 * x</c>, <c>x / 1</c> on primitive integral and
///   binary floating-point arithmetic without operator method (lifted forms included); for floating point the zero must
///   have the neutral sign (<c>x + (-0.0)</c>, <c>x - (+0.0)</c>);</description></item>
///   <item><description><c>x * 0</c> and <c>0 * x</c> on non-lifted primitive integral arithmetic, when the dropped operand is
///   proven discardable by <see cref="ExpressionEffectAnalysis"/>;</description></item>
///   <item><description><c>-(-x)</c> on primitive unchecked negation, and identity conversions without method;</description></item>
///   <item><description>constant short circuits of <see cref="bool"/> and lifted <see cref="Nullable{T}"/> of
///   <see cref="bool"/> operators without method, and constant conditions whose selected branch has the conditional's type
///   and whose discarded branch defines no label;</description></item>
///   <item><description>removal of the <c>Default(void)</c> statements of a block other than its last one, and unwrapping of a
///   block holding a single expression of its own type and no variable.</description></item>
/// </list>
/// <para>
/// Custom operator and conversion methods, checked arithmetic, <see cref="decimal"/> arithmetic, quoted expressions and
/// extension nodes are never rewritten. No user code runs: no node is evaluated, no reflection object of the tree is read
/// (the methods are only compared with <see langword="null"/>), and an extension node is passed through without any call
/// into it (ETC R4).
/// </para>
/// <para>
/// <b>Resources.</b> Each call is one operation of the shared expression resource session: the input is validated against
/// <see cref="ResourceLimits"/> (depth and logical nodes) before any work, every node, binding, initializer, switch case and
/// catch block visited counts one structural visit, and every recursion is stack-guarded, so a hostile input ends with an
/// <see cref="ExpressionResourceLimitException"/> instead of a stack overflow. A shared subtree is walked once per use, like
/// every walk of the engine.
/// </para>
/// <para>
/// <b>Tracing.</b> Inside a tracing scope (<see cref="ExpressionTransformTracing"/>) every node visited is a treatment of the
/// trace, reported for <see cref="ExpressionOptimiser"/>: its enter event, the rule that rewrote it if any (a
/// <see cref="ExpressionTransformEventKind.RuleApplied"/> event whose input is the source node), and its exit with the outcome
/// (<see cref="ExpressionTransformOutcome.Unchanged"/>, <see cref="ExpressionTransformOutcome.Rebuilt"/>,
/// <see cref="ExpressionTransformOutcome.RuleApplied"/>, or <see cref="ExpressionTransformOutcome.Boundary"/> for a quote), or
/// its failure. The detailed level adds no event: the optimiser has no candidate rules nor lexical scopes to report. The
/// optimization itself is unchanged, and outside a scope the only cost is one field read per call (no allocation).
/// </para>
/// </remarks>
public sealed class ExpressionOptimiser
{
    /// <summary>Initializes an optimiser with the default resource limits (<see cref="ExpressionResourceLimits.Default"/>).</summary>
    public ExpressionOptimiser() : this(ExpressionResourceLimits.Default)
    {
    }

    /// <summary>Initializes an optimiser with explicit resource limits.</summary>
    /// <param name="resourceLimits">The limits applied to every optimization.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resourceLimits"/> is <see langword="null"/>.</exception>
    public ExpressionOptimiser(ExpressionResourceLimits resourceLimits)
    {
        ArgumentNullException.ThrowIfNull(resourceLimits);
        ResourceLimits = resourceLimits;
    }

    /// <summary>Gets the resource limits applied to every optimization.</summary>
    public ExpressionResourceLimits ResourceLimits { get; }

    /// <summary>
    /// Optimizes the provided expression tree.
    /// </summary>
    /// <param name="expression">Expression to optimize.</param>
    /// <returns>An optimized expression tree of the same type; the source itself when no rule applies.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExpressionResourceLimitException">The input or the work exceeds <see cref="ResourceLimits"/>, or the stack ran out.</exception>
    public Expression Optimize(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return ExpressionResourceBudget.Run(ResourceLimits, this, expression, null, expression, static root =>
        {
            // Tracing (ExpressionTransformTracing): one field read per call; the untraced visitor holds no tracing state.
            StandardOptimiserVisitor visitor = ExpressionResourceBudget.ActiveTrace is { } trace ? new TracedOptimiserVisitor(trace) : new StandardOptimiserVisitor();
            return visitor.Visit(root)!;
        });
    }

    /// <summary>
    /// Implements the local rewrites. Every guard reads only framework nodes: a child is tested with
    /// <see cref="ExpressionOpacity.IsOpaque"/> before its <see cref="Expression.Type"/> is read, and operator methods are only
    /// compared with <see langword="null"/>.
    /// </summary>
    private class StandardOptimiserVisitor : ExpressionVisitor
    {
        /// <summary>The label search of the discarded conditional branches, created on first use and reused.</summary>
        private LabelDefinitionFinder? _labelFinder;

        /// <inheritdoc />
        /// <remarks>
        /// Counts one structural visit (stack check included) per node, and returns an extension node unchanged before the
        /// framework visitor calls its overridable <c>Accept</c> (ETC R4).
        /// </remarks>
        public override Expression? Visit(Expression? node)
        {
            if (node is null) return null;
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (ExpressionOpacity.IsOpaque(node)) return node;
            return base.Visit(node);
        }

        /// <summary>Dispatches a counted, non-opaque node to the framework visitor (for <see cref="TracedOptimiserVisitor"/>).</summary>
        /// <param name="node">The node.</param>
        /// <returns>The visited node.</returns>
        protected Expression Dispatch(Expression node) => base.Visit(node)!;

        /// <summary>Called when <paramref name="rule"/> produced <paramref name="result"/>; only a traced visitor records it.</summary>
        /// <param name="rule">The rule.</param>
        /// <param name="result">The rewritten node.</param>
        /// <returns><paramref name="result"/>.</returns>
        protected virtual Expression Applied(OptimiserRule rule, Expression result) => result;

        /// <inheritdoc />
        /// <remarks>
        /// Only a dynamic operation reaches this method (extension nodes are stopped by <see cref="Visit(Expression?)"/>): the
        /// framework visitor would reduce it to a call-site invocation, so its arguments are visited and the operation is
        /// rebuilt with its own binder and delegate type, which are never called.
        /// </remarks>
        protected override Expression VisitExtension(Expression node)
        {
            if (node is not DynamicExpression dynamic) return node;
            ReadOnlyCollection<Expression> arguments = Visit(dynamic.Arguments);
            return ReferenceEquals(arguments, dynamic.Arguments) ? dynamic : dynamic.Update(arguments);
        }

        /// <inheritdoc />
        protected override Expression VisitUnary(UnaryExpression node)
        {
            // A quoted expression is data: its value is the quoted tree itself, which must stay unchanged.
            if (node.NodeType == ExpressionType.Quote) return node;

            Expression? operand = Visit(node.Operand);
            if (operand is not null && !ExpressionOpacity.IsOpaque(operand) && node.Method is null)
            {
                if (node.NodeType == ExpressionType.Convert && RewriteConvert(node, operand) is { } converted) return Applied(OptimiserRule.IdentityConversion, converted);
                if (node.NodeType == ExpressionType.Negate && RewriteNegate(node, operand) is { } negated) return Applied(OptimiserRule.DoubleNegation, negated);
            }

            return node.Update(operand);
        }

        /// <summary>Rewrites <c>Convert(x)</c> to its own type without method, a no-op, to <c>x</c>.</summary>
        /// <param name="node">The conversion, without method.</param>
        /// <param name="operand">The visited, non-opaque operand.</param>
        /// <returns>The operand, or <see langword="null"/> when the conversion changes the type.</returns>
        private static Expression? RewriteConvert(UnaryExpression node, Expression operand)
            => ReferenceEquals(operand.Type, node.Type) ? operand : null;

        /// <summary>
        /// Rewrites <c>-(-x)</c> to <c>x</c>: primitive unchecked negation flips the sign bit (or wraps) twice, null included.
        /// </summary>
        /// <param name="node">The outer negation, without method.</param>
        /// <param name="operand">The visited, non-opaque operand.</param>
        /// <returns>The inner operand, or <see langword="null"/> when the operand is not a negation of the same type.</returns>
        private static Expression? RewriteNegate(UnaryExpression node, Expression operand)
            => operand is UnaryExpression { NodeType: ExpressionType.Negate, Method: null, Operand: var inner }
                && !ExpressionOpacity.IsOpaque(inner)
                && ReferenceEquals(inner.Type, node.Type)
                ? inner
                : null;

        /// <inheritdoc />
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.Method is null && (node.NodeType == ExpressionType.AndAlso || node.NodeType == ExpressionType.OrElse))
            {
                return VisitShortCircuit(node);
            }

            Expression left = Visit(node.Left)!;
            Expression right = Visit(node.Right)!;
            if (node.Method is null
                && !ExpressionOpacity.IsOpaque(left)
                && !ExpressionOpacity.IsOpaque(right)
                && TryGetArithmeticKind(node.Type, out ArithmeticKind kind))
            {
                (Expression? rewritten, OptimiserRule rule) = node.NodeType switch
                {
                    ExpressionType.Add => (RewriteAdd(node, left, right, kind), OptimiserRule.AddZero),
                    ExpressionType.Subtract => (RewriteSubtract(node, left, right, kind), OptimiserRule.SubtractZero),
                    ExpressionType.Multiply => (RewriteMultiply(node, left, right, kind), OptimiserRule.Multiply),
                    ExpressionType.Divide => (RewriteDivide(node, left, right, kind), OptimiserRule.DivideOne),
                    _ => ((Expression?)null, OptimiserRule.None),
                };
                if (rewritten is not null) return Applied(rule, rewritten);
            }

            return node.Update(left, node.Conversion, right);
        }

        /// <summary>
        /// Rewrites <c>AndAlso</c>/<c>OrElse</c> without method on <see cref="bool"/> or lifted <see cref="Nullable{T}"/> of
        /// <see cref="bool"/>: a deciding constant on the left (<c>false &amp;&amp; r</c>, <c>true || r</c>) is the result and
        /// the right operand, never evaluated at run time, is not visited; a neutral constant on either side
        /// (<c>true &amp;&amp; r</c>, <c>l &amp;&amp; true</c>, <c>false || r</c>, <c>l || false</c>) leaves the other operand,
        /// evaluated exactly as before. A deciding constant on the right keeps the node: the left operand must still run.
        /// </summary>
        /// <param name="node">The short circuit.</param>
        /// <returns>The rewritten or rebuilt node.</returns>
        private Expression VisitShortCircuit(BinaryExpression node)
        {
            Type type = node.Type;
            bool isAnd = node.NodeType == ExpressionType.AndAlso;
            bool boolean = type == typeof(bool) || type == typeof(bool?);
            Expression left = Visit(node.Left)!;
            if (boolean && TryGetBoolean(left, type, out bool leftValue))
            {
                // false && r -> false, true || r -> true: the constant already has the node's type.
                if (leftValue != isAnd) return Applied(OptimiserRule.ShortCircuit, left);

                Expression kept = Visit(node.Right)!;
                if (!ExpressionOpacity.IsOpaque(kept) && ReferenceEquals(kept.Type, type)) return Applied(OptimiserRule.ShortCircuit, kept);
                return node.Update(left, node.Conversion, kept);
            }

            Expression right = Visit(node.Right)!;
            if (boolean
                && TryGetBoolean(right, type, out bool rightValue)
                && rightValue == isAnd
                && !ExpressionOpacity.IsOpaque(left)
                && ReferenceEquals(left.Type, type))
            {
                return Applied(OptimiserRule.ShortCircuit, left);
            }

            return node.Update(left, node.Conversion, right);
        }

        /// <inheritdoc />
        /// <remarks>
        /// A constant condition selects its branch without visiting the other one, provided the selected branch has exactly
        /// the type of the conditional (an explicitly typed <see cref="void"/> or reference conditional keeps its node) and the
        /// discarded branch defines no label a jump could target.
        /// </remarks>
        protected override Expression VisitConditional(ConditionalExpression node)
        {
            Expression test = Visit(node.Test)!;
            if (test is ConstantExpression { Value: bool selected } constant && ReferenceEquals(constant.Type, typeof(bool)))
            {
                // The type of a conditional without explicit type is read from its first branch: both branches are tested
                // for opacity before any type is read.
                Expression chosen = selected ? node.IfTrue : node.IfFalse;
                Expression discarded = selected ? node.IfFalse : node.IfTrue;
                if (!ExpressionOpacity.IsOpaque(chosen)
                    && !ExpressionOpacity.IsOpaque(discarded)
                    && ReferenceEquals(chosen.Type, node.Type)
                    && !(_labelFinder ??= new LabelDefinitionFinder()).MayDefineLabel(discarded))
                {
                    return Applied(OptimiserRule.ConstantCondition, Visit(chosen)!);
                }
            }

            Expression ifTrue = Visit(node.IfTrue)!;
            Expression ifFalse = Visit(node.IfFalse)!;
            return node.Update(test, ifTrue, ifFalse);
        }

        /// <inheritdoc />
        /// <remarks>
        /// A <c>Default(void)</c> statement other than the last one is a no-op and is removed; the last expression always stays,
        /// because it gives the block its value and, without an explicit type, its type. The block is rebuilt with its own type
        /// and variables (<see cref="BlockExpression.Update"/>). A block without variables holding one expression of its own
        /// type is that expression, unless it is a label, whose scope would move to the enclosing block.
        /// </remarks>
        protected override Expression VisitBlock(BlockExpression node)
        {
            ReadOnlyCollection<Expression> expressions = node.Expressions;
            int last = expressions.Count - 1;

            // The list is created only once a statement is removed or rewritten; an unchanged block allocates nothing.
            List<Expression>? kept = null;
            bool anyRemoved = false;
            for (int i = 0; i <= last; i++)
            {
                Expression visited = Visit(expressions[i])!;
                bool removed = i < last && visited is DefaultExpression && visited.Type == typeof(void);
                anyRemoved |= removed;
                if (kept is null && !removed && ReferenceEquals(visited, expressions[i])) continue;
                if (kept is null)
                {
                    kept = new List<Expression>(expressions.Count);
                    for (int j = 0; j < i; j++) kept.Add(expressions[j]);
                }

                if (!removed) kept.Add(visited);
            }

            if (UnwrapBlock(node, (IReadOnlyList<Expression>?)kept ?? expressions) is { } single) return Applied(OptimiserRule.UnwrapBlock, single);
            if (kept is null) return node;

            BlockExpression rebuilt = node.Update(node.Variables, kept);
            return anyRemoved ? Applied(OptimiserRule.EmptyStatement, rebuilt) : rebuilt;
        }

        /// <summary>
        /// Unwraps a block without variables holding one expression of its own type, unless it is a label, whose scope would move
        /// to the enclosing block.
        /// </summary>
        /// <param name="node">The block.</param>
        /// <param name="expressions">Its visited statements, the removed ones left out.</param>
        /// <returns>The single expression, or <see langword="null"/> when the block must stay.</returns>
        private static Expression? UnwrapBlock(BlockExpression node, IReadOnlyList<Expression> expressions)
            => node.Variables.Count == 0
                && expressions.Count == 1
                && expressions[0] is var single
                && !ExpressionOpacity.IsOpaque(single)
                && single is not LabelExpression
                && ReferenceEquals(single.Type, node.Type)
                ? single
                : null;

        /// <inheritdoc />
        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            // A catch block is not an expression node: it is counted as one structural visit (ETC R2).
            ExpressionResourceBudget.ConsumeStructuralVisit();
            return base.VisitCatchBlock(node);
        }

        /// <inheritdoc />
        protected override SwitchCase VisitSwitchCase(SwitchCase node)
        {
            // A switch case is not an expression node: it is counted as one structural visit (ETC R2).
            ExpressionResourceBudget.ConsumeStructuralVisit();
            return base.VisitSwitchCase(node);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The bindings are walked by <see cref="VisitBindingList"/>, never by the framework visitor, which recurses through
        /// nested member bindings without any node in between and builds a delegate at every level (ETC R3).
        /// </remarks>
        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            NewExpression creation = VisitAndConvert(node.NewExpression, nameof(VisitMemberInit));
            ReadOnlyCollection<MemberBinding> bindings = VisitBindingList(node.Bindings);
            return ReferenceEquals(creation, node.NewExpression) && ReferenceEquals(bindings, node.Bindings) ? node : node.Update(creation, bindings);
        }

        /// <inheritdoc />
        protected override Expression VisitListInit(ListInitExpression node)
        {
            NewExpression creation = VisitAndConvert(node.NewExpression, nameof(VisitListInit));
            ReadOnlyCollection<ElementInit> initializers = VisitInitializerList(node.Initializers);
            return ReferenceEquals(creation, node.NewExpression) && ReferenceEquals(initializers, node.Initializers) ? node : node.Update(creation, initializers);
        }

        /// <inheritdoc />
        protected override MemberBinding VisitMemberBinding(MemberBinding node) => VisitBindingNode(node);

        /// <inheritdoc />
        protected override ElementInit VisitElementInit(ElementInit node) => VisitInitializer(node);

        /// <summary>Visits bindings in declaration order; returns <paramref name="bindings"/> itself when none changed.</summary>
        /// <param name="bindings">The bindings.</param>
        /// <returns>The visited bindings.</returns>
        private ReadOnlyCollection<MemberBinding> VisitBindingList(ReadOnlyCollection<MemberBinding> bindings)
        {
            MemberBinding[]? changed = null;
            for (int i = 0; i < bindings.Count; i++)
            {
                MemberBinding visited = VisitBindingNode(bindings[i]);
                if (changed is null && ReferenceEquals(visited, bindings[i])) continue;
                if (changed is null)
                {
                    changed = new MemberBinding[bindings.Count];
                    for (int j = 0; j < i; j++) changed[j] = bindings[j];
                }

                changed[i] = visited;
            }

            return changed is null ? bindings : new ReadOnlyCollection<MemberBinding>(changed);
        }

        /// <summary>
        /// Visits one binding (one structural visit, stack check included): its value, its nested bindings or its initializers.
        /// A binding of an unknown class is returned unchanged.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <returns>The visited binding, <paramref name="binding"/> itself when nothing changed.</returns>
        private MemberBinding VisitBindingNode(MemberBinding binding)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            switch (binding)
            {
                case MemberAssignment assignment:
                    Expression value = Visit(assignment.Expression)!;
                    return ReferenceEquals(value, assignment.Expression) ? assignment : assignment.Update(value);
                case MemberMemberBinding memberBinding:
                    ReadOnlyCollection<MemberBinding> nested = VisitBindingList(memberBinding.Bindings);
                    return ReferenceEquals(nested, memberBinding.Bindings) ? memberBinding : memberBinding.Update(nested);
                case MemberListBinding listBinding:
                    ReadOnlyCollection<ElementInit> initializers = VisitInitializerList(listBinding.Initializers);
                    return ReferenceEquals(initializers, listBinding.Initializers) ? listBinding : listBinding.Update(initializers);
                default:
                    return binding;
            }
        }

        /// <summary>Visits initializers in order; returns <paramref name="initializers"/> itself when none changed.</summary>
        /// <param name="initializers">The initializers.</param>
        /// <returns>The visited initializers.</returns>
        private ReadOnlyCollection<ElementInit> VisitInitializerList(ReadOnlyCollection<ElementInit> initializers)
        {
            ElementInit[]? changed = null;
            for (int i = 0; i < initializers.Count; i++)
            {
                ElementInit visited = VisitInitializer(initializers[i]);
                if (changed is null && ReferenceEquals(visited, initializers[i])) continue;
                if (changed is null)
                {
                    changed = new ElementInit[initializers.Count];
                    for (int j = 0; j < i; j++) changed[j] = initializers[j];
                }

                changed[i] = visited;
            }

            return changed is null ? initializers : new ReadOnlyCollection<ElementInit>(changed);
        }

        /// <summary>
        /// Visits the arguments of one initializer (one structural visit); a changed initializer is rebuilt with its original
        /// <see cref="ElementInit.AddMethod"/>, never rediscovered from the argument types.
        /// </summary>
        /// <param name="initializer">The initializer.</param>
        /// <returns>The visited initializer, <paramref name="initializer"/> itself when nothing changed.</returns>
        private ElementInit VisitInitializer(ElementInit initializer)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            ReadOnlyCollection<Expression> arguments = Visit(initializer.Arguments);
            return ReferenceEquals(arguments, initializer.Arguments) ? initializer : initializer.Update(arguments);
        }

        /// <summary>Rewrites <c>x + 0</c> and <c>0 + x</c>, the zero having the neutral sign for floating point.</summary>
        /// <param name="node">The addition.</param>
        /// <param name="left">The visited left operand.</param>
        /// <param name="right">The visited right operand.</param>
        /// <param name="kind">The arithmetic kind of the node's type.</param>
        /// <returns>The kept operand, or <see langword="null"/> when no identity applies.</returns>
        private static Expression? RewriteAdd(BinaryExpression node, Expression left, Expression right, ArithmeticKind kind)
            => KeepIfNeutral(node, left, right, NeutralRole.AddedZero, kind) ?? KeepIfNeutral(node, right, left, NeutralRole.AddedZero, kind);

        /// <summary>Rewrites <c>x - 0</c>, the zero being positive for floating point.</summary>
        /// <param name="node">The subtraction.</param>
        /// <param name="left">The visited left operand.</param>
        /// <param name="right">The visited right operand.</param>
        /// <param name="kind">The arithmetic kind of the node's type.</param>
        /// <returns>The left operand, or <see langword="null"/> when no identity applies.</returns>
        private static Expression? RewriteSubtract(BinaryExpression node, Expression left, Expression right, ArithmeticKind kind)
            => KeepIfNeutral(node, left, right, NeutralRole.SubtractedZero, kind);

        /// <summary>Rewrites <c>x / 1</c>.</summary>
        /// <param name="node">The division.</param>
        /// <param name="left">The visited left operand.</param>
        /// <param name="right">The visited right operand.</param>
        /// <param name="kind">The arithmetic kind of the node's type.</param>
        /// <returns>The left operand, or <see langword="null"/> when no identity applies.</returns>
        private static Expression? RewriteDivide(BinaryExpression node, Expression left, Expression right, ArithmeticKind kind)
            => KeepIfNeutral(node, left, right, NeutralRole.One, kind);

        /// <summary>
        /// Rewrites <c>x * 1</c> and <c>1 * x</c>, then, for non-lifted integral arithmetic only, <c>x * 0</c> and <c>0 * x</c>
        /// when the dropped operand is discardable (no write, exception, allocation or unknown code): an integral product with
        /// zero is zero and never overflows, whereas a lifted product with zero is <see langword="null"/> for a
        /// <see langword="null"/> operand and a floating-point one is NaN or a signed zero.
        /// </summary>
        /// <param name="node">The product.</param>
        /// <param name="left">The visited left operand.</param>
        /// <param name="right">The visited right operand.</param>
        /// <param name="kind">The arithmetic kind of the node's type.</param>
        /// <returns>The rewritten node, or <see langword="null"/> when no identity applies.</returns>
        private static Expression? RewriteMultiply(BinaryExpression node, Expression left, Expression right, ArithmeticKind kind)
        {
            Expression? kept = KeepIfNeutral(node, left, right, NeutralRole.One, kind) ?? KeepIfNeutral(node, right, left, NeutralRole.One, kind);
            if (kept is not null || kind != ArithmeticKind.Integral) return kept;

            // The zero constant has the node's (non-nullable) type: it is the result.
            if (Classify(right) == ConstantClass.PositiveZero && IsDiscardable(left)) return right;
            if (Classify(left) == ConstantClass.PositiveZero && IsDiscardable(right)) return left;
            return null;
        }

        /// <summary>
        /// Returns <paramref name="kept"/> when <paramref name="neutral"/> is the neutral constant of
        /// <paramref name="role"/> for the node's type and <paramref name="kept"/> has exactly that type.
        /// </summary>
        /// <param name="node">The arithmetic node.</param>
        /// <param name="kept">The operand that would replace the node.</param>
        /// <param name="neutral">The operand that must be the neutral constant.</param>
        /// <param name="role">Which neutral constant is required.</param>
        /// <param name="kind">The arithmetic kind of the node's type.</param>
        /// <returns><paramref name="kept"/>, or <see langword="null"/>.</returns>
        private static Expression? KeepIfNeutral(BinaryExpression node, Expression kept, Expression neutral, NeutralRole role, ArithmeticKind kind)
        {
            ConstantClass constant = Classify(neutral);
            bool isNeutral = role switch
            {
                // x + 0: for floating point only -0.0 is neutral (-0.0 + +0.0 = +0.0).
                NeutralRole.AddedZero => constant == (kind == ArithmeticKind.Floating ? ConstantClass.NegativeZero : ConstantClass.PositiveZero),

                // x - 0: for floating point only +0.0 is neutral (-0.0 - -0.0 = +0.0).
                NeutralRole.SubtractedZero => constant == ConstantClass.PositiveZero,
                _ => constant == ConstantClass.One,
            };

            return isNeutral && ReferenceEquals(neutral.Type, node.Type) && ReferenceEquals(kept.Type, node.Type) ? kept : null;
        }

        /// <summary>Whether the evaluation of <paramref name="operand"/> can be dropped (<see cref="ExpressionEffectAnalysis.CanDiscard"/>).</summary>
        /// <param name="operand">A visited, non-opaque operand.</param>
        /// <returns><see langword="true"/> when dropping it is unobservable.</returns>
        private static bool IsDiscardable(Expression operand) => ExpressionEffectAnalysis.CanDiscard(ExpressionEffectAnalysis.Analyze(operand));

        /// <summary>
        /// Classifies the arithmetic of a node type: the primitive integral types with expression-tree arithmetic
        /// (<see cref="short"/>, <see cref="ushort"/>, <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>,
        /// <see cref="ulong"/>) and the binary floating-point types (<see cref="float"/>, <see cref="double"/>), or their
        /// nullable forms. <see cref="decimal"/> is excluded: its identities change the scale of the result.
        /// </summary>
        /// <param name="type">The node type.</param>
        /// <param name="kind">The arithmetic kind.</param>
        /// <returns><see langword="true"/> for a supported type.</returns>
        private static bool TryGetArithmeticKind(Type type, out ArithmeticKind kind)
        {
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            bool lifted = !ReferenceEquals(underlying, type);
            if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(uint) || underlying == typeof(ulong)
                || underlying == typeof(short) || underlying == typeof(ushort))
            {
                kind = lifted ? ArithmeticKind.LiftedIntegral : ArithmeticKind.Integral;
                return true;
            }

            if (underlying == typeof(double) || underlying == typeof(float))
            {
                kind = ArithmeticKind.Floating;
                return true;
            }

            kind = default;
            return false;
        }

        /// <summary>
        /// Classifies a constant operand exactly in its own type: positive zero, negative zero (floating point only), one, or
        /// anything else. No conversion, no user code: the boxed value is matched by type.
        /// </summary>
        /// <param name="expression">The operand.</param>
        /// <returns>Its class.</returns>
        private static ConstantClass Classify(Expression expression)
        {
            if (expression is not ConstantExpression { Value: { } value }) return ConstantClass.Other;
            return value switch
            {
                int v => v == 0 ? ConstantClass.PositiveZero : v == 1 ? ConstantClass.One : ConstantClass.Other,
                long v => v == 0 ? ConstantClass.PositiveZero : v == 1 ? ConstantClass.One : ConstantClass.Other,
                uint v => v == 0 ? ConstantClass.PositiveZero : v == 1 ? ConstantClass.One : ConstantClass.Other,
                ulong v => v == 0 ? ConstantClass.PositiveZero : v == 1 ? ConstantClass.One : ConstantClass.Other,
                short v => v == 0 ? ConstantClass.PositiveZero : v == 1 ? ConstantClass.One : ConstantClass.Other,
                ushort v => v == 0 ? ConstantClass.PositiveZero : v == 1 ? ConstantClass.One : ConstantClass.Other,
                double v => v == 1.0 ? ConstantClass.One : v == 0.0 ? (double.IsNegative(v) ? ConstantClass.NegativeZero : ConstantClass.PositiveZero) : ConstantClass.Other,
                float v => v == 1.0f ? ConstantClass.One : v == 0.0f ? (float.IsNegative(v) ? ConstantClass.NegativeZero : ConstantClass.PositiveZero) : ConstantClass.Other,
                _ => ConstantClass.Other,
            };
        }

        /// <summary>
        /// Reads a Boolean constant of exactly <paramref name="type"/> (<see cref="bool"/> or <see cref="Nullable{T}"/> of
        /// <see cref="bool"/>); a <see langword="null"/> constant is not one.
        /// </summary>
        /// <param name="expression">The operand.</param>
        /// <param name="type">The type of the short circuit.</param>
        /// <param name="value">The constant value.</param>
        /// <returns><see langword="true"/> for such a constant.</returns>
        private static bool TryGetBoolean(Expression expression, Type type, out bool value)
        {
            if (expression is ConstantExpression { Value: bool constant } node && ReferenceEquals(node.Type, type))
            {
                value = constant;
                return true;
            }

            value = false;
            return false;
        }
    }

    /// <summary>
    /// <see cref="StandardOptimiserVisitor"/> inside a traced operation: the same visit, each node inside a treatment of the trace
    /// (enter, rule applied, exit or failure).
    /// </summary>
    /// <param name="trace">The tracing state of the operation.</param>
    private sealed class TracedOptimiserVisitor(ExpressionTraceSession trace) : StandardOptimiserVisitor
    {
        /// <summary>The tracing state of the operation.</summary>
        private readonly ExpressionTraceSession _trace = trace;

        /// <summary>
        /// The rule that rewrote the node being returned: a rewrite sets it after every child visit of its node, and the visit of
        /// that node reads and clears it.
        /// </summary>
        private OptimiserRule _appliedRule;

        /// <inheritdoc />
        /// <remarks>
        /// A failure is recorded by an exception filter that always declines: the exception propagates untouched, without any
        /// handler running on the (possibly exhausted) stack of the throw site. An opaque node is never read: its events carry no
        /// node type, and it is returned unchanged.
        /// </remarks>
        public override Expression? Visit(Expression? node)
        {
            if (node is null) return null;
            ExpressionResourceBudget.ConsumeStructuralVisit();
            int frame = _trace.EnterTreatment(typeof(ExpressionOptimiser), node);
            try
            {
                bool opaque = ExpressionOpacity.IsOpaque(node);
                Expression result = opaque ? node : Dispatch(node);
                OptimiserRule rule = _appliedRule;
                _appliedRule = OptimiserRule.None;
                if (rule != OptimiserRule.None)
                {
                    ExpressionTransformRuleInfo info = OptimiserRuleInfos.Get(rule);
                    _trace.BeginRule(info);
                    _trace.RuleApplied(info, node, result);
                }

                // Without a rule, a result other than the node is the node rebuilt around its optimised children.
                _trace.ExitTreatment(frame, node, rule == OptimiserRule.None ? result : node, result, !opaque && node.NodeType == ExpressionType.Quote);
                return result;
            }
            catch (Exception exception) when (_trace.TreatmentFailed(frame, exception))
            {
                // Never reached: the filter only records the failure.
                throw;
            }
        }

        /// <inheritdoc />
        protected override Expression Applied(OptimiserRule rule, Expression result)
        {
            _appliedRule = rule;
            return result;
        }
    }

    /// <summary>The rules of the optimiser, as reported by tracing.</summary>
    private enum OptimiserRule
    {
        /// <summary>No rule rewrote the node.</summary>
        None,

        /// <summary><c>x + 0</c>, <c>0 + x</c>.</summary>
        AddZero,

        /// <summary><c>x - 0</c>.</summary>
        SubtractZero,

        /// <summary><c>x * 1</c>, <c>1 * x</c>, <c>x * 0</c>, <c>0 * x</c>.</summary>
        Multiply,

        /// <summary><c>x / 1</c>.</summary>
        DivideOne,

        /// <summary>An identity conversion without method.</summary>
        IdentityConversion,

        /// <summary><c>-(-x)</c>.</summary>
        DoubleNegation,

        /// <summary>A constant short circuit.</summary>
        ShortCircuit,

        /// <summary>A constant condition.</summary>
        ConstantCondition,

        /// <summary>The removal of <c>Default(void)</c> statements of a block.</summary>
        EmptyStatement,

        /// <summary>The unwrapping of a block holding a single expression.</summary>
        UnwrapBlock,
    }

    /// <summary>
    /// The trace identities of the <see cref="OptimiserRule"/> values: each identifies the method implementing the rule. Built
    /// on the first traced rewrite only, so an untraced optimization never reads reflection metadata.
    /// </summary>
    private static class OptimiserRuleInfos
    {
        /// <summary>The identities, indexed by <see cref="OptimiserRule"/>.</summary>
        private static readonly ExpressionTransformRuleInfo[] Infos = Build();

        /// <summary>Gets the identity of <paramref name="rule"/>.</summary>
        /// <param name="rule">A rule other than <see cref="OptimiserRule.None"/>.</param>
        /// <returns>Its identity.</returns>
        public static ExpressionTransformRuleInfo Get(OptimiserRule rule) => Infos[(int)rule];

        /// <summary>Builds the identities, in the order of <see cref="OptimiserRule"/>.</summary>
        /// <returns>The identities; the slot of <see cref="OptimiserRule.None"/> is unused.</returns>
        private static ExpressionTransformRuleInfo[] Build()
        {
            const ExpressionType AnyNodeType = (ExpressionType)(-1);
            (OptimiserRule Rule, string Method, ExpressionType NodeType)[] rules =
            [
                (OptimiserRule.AddZero, "RewriteAdd", ExpressionType.Add),
                (OptimiserRule.SubtractZero, "RewriteSubtract", ExpressionType.Subtract),
                (OptimiserRule.Multiply, "RewriteMultiply", ExpressionType.Multiply),
                (OptimiserRule.DivideOne, "RewriteDivide", ExpressionType.Divide),
                (OptimiserRule.IdentityConversion, "RewriteConvert", ExpressionType.Convert),
                (OptimiserRule.DoubleNegation, "RewriteNegate", ExpressionType.Negate),
                (OptimiserRule.ShortCircuit, "VisitShortCircuit", AnyNodeType),
                (OptimiserRule.ConstantCondition, "VisitConditional", ExpressionType.Conditional),
                (OptimiserRule.EmptyStatement, "VisitBlock", ExpressionType.Block),
                (OptimiserRule.UnwrapBlock, "UnwrapBlock", ExpressionType.Block),
            ];

            const BindingFlags Flags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var infos = new ExpressionTransformRuleInfo[rules.Length + 1];
            for (int i = 0; i < rules.Length; i++)
            {
                MethodInfo method = typeof(StandardOptimiserVisitor).GetMethod(rules[i].Method, Flags)!;
                infos[(int)rules[i].Rule] = new ExpressionTransformRuleInfo(method, typeof(ExpressionOptimiser), rules[i].NodeType, i, isOverloaded: false);
            }

            return infos;
        }
    }

    /// <summary>The arithmetic families the identities apply to.</summary>
    private enum ArithmeticKind
    {
        /// <summary>Non-lifted primitive integral arithmetic.</summary>
        Integral,

        /// <summary>Lifted primitive integral arithmetic (a <see langword="null"/> operand gives <see langword="null"/>).</summary>
        LiftedIntegral,

        /// <summary>Binary floating-point arithmetic, lifted or not.</summary>
        Floating,
    }

    /// <summary>The neutral constant an identity requires.</summary>
    private enum NeutralRole
    {
        /// <summary>The zero of <c>x + 0</c> (negative zero for floating point).</summary>
        AddedZero,

        /// <summary>The zero of <c>x - 0</c> (positive zero for floating point).</summary>
        SubtractedZero,

        /// <summary>The one of <c>x * 1</c> and <c>x / 1</c>.</summary>
        One,
    }

    /// <summary>The exact class of a numeric constant.</summary>
    private enum ConstantClass
    {
        /// <summary>Not a zero or a one of a supported type.</summary>
        Other,

        /// <summary>An integral zero or a floating-point positive zero.</summary>
        PositiveZero,

        /// <summary>A floating-point negative zero.</summary>
        NegativeZero,

        /// <summary>One.</summary>
        One,
    }

    /// <summary>
    /// Read-only search for the label definitions of a subtree the optimiser discards (the unselected branch of a constant
    /// conditional): a <see cref="LabelExpression"/> or a <see cref="LoopExpression"/> may be the target of a jump from outside
    /// the subtree, so discarding it could break the jump. An extension node may hold one and is assumed to. A lambda is not
    /// entered: a jump never crosses a lambda boundary.
    /// </summary>
    private sealed class LabelDefinitionFinder : ExpressionVisitor
    {
        /// <summary>Whether a possible label definition was found.</summary>
        private bool _found;

        /// <summary>Whether <paramref name="expression"/> may define a label a jump outside it could target.</summary>
        /// <param name="expression">The discarded subtree.</param>
        /// <returns><see langword="false"/> only when it provably defines none.</returns>
        public bool MayDefineLabel(Expression expression)
        {
            if (expression is ConstantExpression or DefaultExpression or ParameterExpression) return false;
            _found = false;
            Visit(expression);
            return _found;
        }

        /// <inheritdoc />
        public override Expression? Visit(Expression? node)
        {
            if (_found || node is null) return node;
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (ExpressionOpacity.IsOpaque(node) || node is LabelExpression or LoopExpression)
            {
                _found = true;
                return node;
            }

            return node is LambdaExpression ? node : base.Visit(node);
        }

        /// <inheritdoc />
        protected override Expression VisitExtension(Expression node)
        {
            // Only a dynamic operation reaches this method: its arguments are its only children.
            if (node is DynamicExpression dynamic) Visit(dynamic.Arguments);
            return node;
        }

        /// <inheritdoc />
        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            Visit(node.NewExpression);
            ReadOnlyBindingWalk.VisitBindings(this, node.Bindings);
            return node;
        }

        /// <inheritdoc />
        protected override Expression VisitListInit(ListInitExpression node)
        {
            Visit(node.NewExpression);
            ReadOnlyBindingWalk.VisitInitializers(this, node.Initializers);
            return node;
        }

        /// <inheritdoc />
        protected override MemberBinding VisitMemberBinding(MemberBinding node)
        {
            ReadOnlyBindingWalk.VisitBinding(this, node);
            return node;
        }

        /// <inheritdoc />
        protected override ElementInit VisitElementInit(ElementInit node)
        {
            ReadOnlyBindingWalk.VisitInitializer(this, node);
            return node;
        }

        /// <inheritdoc />
        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            return base.VisitCatchBlock(node);
        }

        /// <inheritdoc />
        protected override SwitchCase VisitSwitchCase(SwitchCase node)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            return base.VisitSwitchCase(node);
        }
    }
}
