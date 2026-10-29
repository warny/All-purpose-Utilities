using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Expressions;

/// <summary>What <see cref="InvocationInliningAnalysis.Analyze"/> allows for one lambda invocation.</summary>
internal enum InvocationInliningKind
{
    /// <summary>Inlining is not proven safe: the invocation must be kept as is.</summary>
    Preserve,

    /// <summary>The body never reads a parameter and every argument is inert: the body replaces the invocation unchanged.</summary>
    InlineBody,

    /// <summary>Substituting the arguments into the body through <see cref="ExpressionTransformer.ReplaceArgumentsCore"/> is safe.</summary>
    Substitute,
}

/// <summary>
/// Conservative eligibility check for inlining <c>((p1, …, pn) =&gt; body)(a1, …, an)</c> (ETC-11, ETC R1a). Inlining is
/// allowed only when <see cref="ExpressionTransformer.ReplaceArgumentsCore"/> provably replaces every use of the
/// parameters and the evaluation of the arguments cannot be observed to move, repeat or disappear.
/// </summary>
/// <remarks>
/// <para>
/// The scan follows the families <see cref="ExpressionTransformer.ReplaceArgumentsCore"/> enters (parameter, unary,
/// binary, call, invocation, conditional and, since ETC R1b, member access, type test, object and array creation), in
/// evaluation order. Any other node is a substitution barrier: a parameter
/// referenced inside it (or declared by it, which covers shadowing) blocks inlining, and an extension node, which
/// cannot be inspected without reducing it, always does.
/// </para>
/// <para>
/// Arguments: only constants, defaults and parameter reads are inlined. Their evaluation has no effect and cannot
/// throw, so dropping or repeating it is not observable. A parameter read can still observe a different value if the
/// body may write the variable before the read; a read of such an argument after any node that may run user code or
/// write a variable (call, invocation, custom operator, assignment, untraversed node) therefore blocks inlining.
/// Every other argument keeps the invocation: proving its effects, exceptions and evaluation order is R2's
/// capture-avoiding substitution, not this guard.
/// </para>
/// <para>
/// Since ETC R1b (C-01) substitution rebuilds unary nodes faithfully (operator method and typed throw kept), so a unary
/// node with metadata no longer blocks inlining; R1a refused it because the historical factories dropped them (ETC-04).
/// A property read, like a call, and a constructor call may run user code.
/// </para>
/// </remarks>
internal sealed class InvocationInliningAnalysis
{
    /// <summary>The parameters of the invoked lambda.</summary>
    private readonly ReadOnlyCollection<ParameterExpression> parameters;

    /// <summary>The arguments of the invocation, positionally matching <see cref="parameters"/>.</summary>
    private readonly ReadOnlyCollection<Expression> arguments;

    /// <summary>Whether a node that may run user code or write a variable has been evaluated so far.</summary>
    private bool effectSeen;

    /// <summary>Whether the substitutable part of the body reads one of <see cref="parameters"/>.</summary>
    private bool referenced;

    /// <summary>Lazily created finder for parameter references inside substitution barriers.</summary>
    private ReferenceFinder? finder;

    /// <summary>Initializes an analysis of one invocation.</summary>
    /// <param name="parameters">The parameters of the invoked lambda.</param>
    /// <param name="arguments">The arguments of the invocation.</param>
    private InvocationInliningAnalysis(ReadOnlyCollection<ParameterExpression> parameters, ReadOnlyCollection<Expression> arguments)
    {
        this.parameters = parameters;
        this.arguments = arguments;
    }

    /// <summary>Decides how <paramref name="invocation"/>, whose target is <paramref name="lambda"/>, may be inlined.</summary>
    /// <param name="lambda">The invoked lambda (already prepared).</param>
    /// <param name="invocation">The invocation (already prepared).</param>
    /// <returns>The allowed inlining.</returns>
    public static InvocationInliningKind Analyze(LambdaExpression lambda, InvocationExpression invocation)
    {
        // A body whose type differs from the invocation's (a void delegate, a covariant return) would change the
        // static type seen by the enclosing node.
        if (lambda.Body.Type != invocation.Type)
        {
            return InvocationInliningKind.Preserve;
        }

        ReadOnlyCollection<ParameterExpression> parameters = lambda.Parameters;
        if (parameters.Count == 0)
        {
            return InvocationInliningKind.InlineBody;
        }

        ReadOnlyCollection<Expression> arguments = invocation.Arguments;
        for (int i = 0; i < parameters.Count; i++)
        {
            Expression argument = arguments[i];
            if (parameters[i].IsByRef || argument.Type != parameters[i].Type || argument is not (ConstantExpression or DefaultExpression or ParameterExpression))
            {
                return InvocationInliningKind.Preserve;
            }
        }

        var analysis = new InvocationInliningAnalysis(parameters, arguments);
        if (!analysis.Scan(lambda.Body))
        {
            return InvocationInliningKind.Preserve;
        }

        if (!analysis.referenced)
        {
            return InvocationInliningKind.InlineBody;
        }

        return InvocationInliningKind.Substitute;
    }

    /// <summary>Scans one node of the substitutable part of the body, in evaluation order.</summary>
    /// <param name="e">The node, or <see langword="null"/> for an absent receiver.</param>
    /// <returns><see langword="false"/> if inlining is unsafe.</returns>
    /// <remarks>Single dispatcher of the scan: one structural visit per node, stack check included (ETC R1a-Security).</remarks>
    private bool Scan(Expression? e)
    {
        ExpressionResourceBudget.ConsumeStructuralVisit();
        return e switch
        {
            null => true,
            ParameterExpression parameter => ScanReference(parameter),
            ConstantExpression or DefaultExpression => true,
            UnaryExpression unary => ScanUnary(unary),
            BinaryExpression binary => ScanBinary(binary),
            MethodCallExpression call => ScanCall(call),
            InvocationExpression invocation => ScanInvocation(invocation),
            ConditionalExpression conditional => Scan(conditional.Test) && Scan(conditional.IfTrue) && Scan(conditional.IfFalse),
            MemberExpression member => ScanMember(member),
            TypeBinaryExpression typeBinary => Scan(typeBinary.Expression),
            NewExpression creation => ScanNew(creation),
            NewArrayExpression array => ScanEach(array.Expressions),
            _ => ScanBarrier(e),
        };
    }

    /// <summary>Records a read of a parameter.</summary>
    /// <param name="parameter">The parameter read.</param>
    /// <returns><see langword="false"/> if the read is of a parameter argument that may have been written before.</returns>
    private bool ScanReference(ParameterExpression parameter)
    {
        int index = IndexOf(parameter);
        if (index < 0) return true;

        referenced = true;
        return !(effectSeen && arguments[index] is ParameterExpression);
    }

    /// <summary>Scans a unary node.</summary>
    /// <param name="unary">The node.</param>
    /// <returns><see langword="false"/> if inlining is unsafe.</returns>
    private bool ScanUnary(UnaryExpression unary)
    {
        if (unary.Operand is null)
        {
            // A rethrow: nothing to substitute, and nothing runs after it.
            effectSeen = true;
            return true;
        }

        bool assigns = unary.NodeType is ExpressionType.PreIncrementAssign or ExpressionType.PreDecrementAssign
            or ExpressionType.PostIncrementAssign or ExpressionType.PostDecrementAssign;
        if (assigns && IsOwnParameter(unary.Operand))
        {
            return false;
        }

        if (!Scan(unary.Operand)) return false;

        if (assigns || unary.Method is not null || unary.NodeType == ExpressionType.Throw)
        {
            effectSeen = true;
        }

        return true;
    }

    /// <summary>Scans a binary node.</summary>
    /// <param name="binary">The node.</param>
    /// <returns><see langword="false"/> if inlining is unsafe.</returns>
    private bool ScanBinary(BinaryExpression binary)
    {
        bool assigns = IsAssignment(binary.NodeType);
        if (assigns && IsOwnParameter(binary.Left))
        {
            return false;
        }

        // Substitution keeps the conversion lambda as is, so it must not use a parameter.
        if (binary.Conversion is not null && ContainsOwnReference(binary.Conversion))
        {
            return false;
        }

        if (!Scan(binary.Left) || !Scan(binary.Right)) return false;

        if (assigns || binary.Method is not null || binary.Conversion is not null)
        {
            effectSeen = true;
        }

        return true;
    }

    /// <summary>Scans a method call: receiver, then arguments, then the call itself, which may run user code.</summary>
    /// <param name="call">The node.</param>
    /// <returns><see langword="false"/> if inlining is unsafe.</returns>
    private bool ScanCall(MethodCallExpression call)
    {
        if (!Scan(call.Object)) return false;

        ParameterInfo[] methodParameters = call.Method.GetParameters();
        for (int i = 0; i < call.Arguments.Count; i++)
        {
            Expression argument = call.Arguments[i];
            if (methodParameters[i].ParameterType.IsByRef && IsOwnParameter(argument)) return false;
            if (!Scan(argument)) return false;
        }

        effectSeen = true;
        return true;
    }

    /// <summary>Scans an invocation: target, then arguments, then the invocation itself, which may run user code.</summary>
    /// <param name="invocation">The node.</param>
    /// <returns><see langword="false"/> if inlining is unsafe.</returns>
    private bool ScanInvocation(InvocationExpression invocation)
    {
        if (!Scan(invocation.Expression)) return false;

        ParameterInfo[]? delegateParameters = null;
        for (int i = 0; i < invocation.Arguments.Count; i++)
        {
            Expression argument = invocation.Arguments[i];
            if (IsOwnParameter(argument))
            {
                // A parameter passed by reference could be written by the callee: substitution would lose the write.
                delegateParameters ??= invocation.Expression.Type.GetMethod("Invoke")?.GetParameters();
                if (delegateParameters is null || delegateParameters[i].ParameterType.IsByRef) return false;
            }

            if (!Scan(argument)) return false;
        }

        effectSeen = true;
        return true;
    }

    /// <summary>Scans a member access: the receiver, then the read itself; a property getter may run user code.</summary>
    /// <param name="member">The node.</param>
    /// <returns><see langword="false"/> if inlining is unsafe.</returns>
    private bool ScanMember(MemberExpression member)
    {
        if (!Scan(member.Expression)) return false;

        if (member.Member is not FieldInfo)
        {
            effectSeen = true;
        }

        return true;
    }

    /// <summary>Scans an object creation: arguments in order, then the constructor, which may run user code.</summary>
    /// <param name="creation">The node.</param>
    /// <returns><see langword="false"/> if inlining is unsafe.</returns>
    private bool ScanNew(NewExpression creation)
    {
        // Only a constructor-less value-type creation has no constructor, and it has no argument.
        ParameterInfo[] constructorParameters = creation.Constructor?.GetParameters() ?? [];
        for (int i = 0; i < creation.Arguments.Count; i++)
        {
            Expression argument = creation.Arguments[i];
            if (constructorParameters[i].ParameterType.IsByRef && IsOwnParameter(argument)) return false;
            if (!Scan(argument)) return false;
        }

        if (creation.Constructor is not null)
        {
            effectSeen = true;
        }

        return true;
    }

    /// <summary>Scans the elements or bounds of an array creation, in order; the allocation itself writes no variable.</summary>
    /// <param name="children">The elements or bounds.</param>
    /// <returns><see langword="false"/> if inlining is unsafe.</returns>
    private bool ScanEach(ReadOnlyCollection<Expression> children)
    {
        for (int i = 0; i < children.Count; i++)
        {
            if (!Scan(children[i])) return false;
        }

        return true;
    }

    /// <summary>
    /// Handles a node <see cref="ExpressionTransformer.ReplaceArgumentsCore"/> does not enter: it must not use a
    /// parameter, and it is assumed to have effects.
    /// </summary>
    /// <param name="e">The node.</param>
    /// <returns><see langword="false"/> if the node references or declares one of the parameters.</returns>
    private bool ScanBarrier(Expression e)
    {
        if (ContainsOwnReference(e)) return false;

        effectSeen = true;
        return true;
    }

    /// <summary>Tests whether <paramref name="e"/> mentions one of the parameters anywhere, or cannot be inspected.</summary>
    /// <param name="e">The subtree.</param>
    /// <returns><see langword="true"/> for a reference, a declaration reusing a parameter instance, or an extension node.</returns>
    private bool ContainsOwnReference(Expression e)
    {
        finder ??= new ReferenceFinder(this);
        return finder.Find(e);
    }

    /// <summary>Tests whether <paramref name="e"/> is one of the invoked lambda's parameters.</summary>
    /// <param name="e">The expression.</param>
    /// <returns><see langword="true"/> if it is.</returns>
    private bool IsOwnParameter(Expression e) => e is ParameterExpression parameter && IndexOf(parameter) >= 0;

    /// <summary>Finds the position of <paramref name="parameter"/> among the lambda's parameters, by reference.</summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>Its index, or -1.</returns>
    private int IndexOf(ParameterExpression parameter)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (ReferenceEquals(parameters[i], parameter)) return i;
        }

        return -1;
    }

    /// <summary>Tests whether a binary node type assigns its left operand.</summary>
    /// <param name="nodeType">The node type.</param>
    /// <returns><see langword="true"/> for <c>Assign</c> and every compound assignment.</returns>
    private static bool IsAssignment(ExpressionType nodeType) => nodeType is ExpressionType.Assign
        or ExpressionType.AddAssign or ExpressionType.AddAssignChecked or ExpressionType.SubtractAssign
        or ExpressionType.SubtractAssignChecked or ExpressionType.MultiplyAssign or ExpressionType.MultiplyAssignChecked
        or ExpressionType.DivideAssign or ExpressionType.ModuloAssign or ExpressionType.PowerAssign
        or ExpressionType.AndAssign or ExpressionType.OrAssign or ExpressionType.ExclusiveOrAssign
        or ExpressionType.LeftShiftAssign or ExpressionType.RightShiftAssign;

    /// <summary>
    /// Read-only visitor looking for any mention of the analysed parameters. It never rebuilds a node and never reduces
    /// an extension node: an extension is reported as a mention, since its children are unknown.
    /// </summary>
    private sealed class ReferenceFinder : ExpressionVisitor
    {
        /// <summary>The analysis whose parameters are looked for.</summary>
        private readonly InvocationInliningAnalysis owner;

        /// <summary>Whether a mention was found during the current search.</summary>
        private bool found;

        /// <summary>Initializes a finder for the parameters of <paramref name="owner"/>.</summary>
        /// <param name="owner">The analysis.</param>
        public ReferenceFinder(InvocationInliningAnalysis owner) => this.owner = owner;

        /// <summary>Searches <paramref name="e"/>.</summary>
        /// <param name="e">The subtree.</param>
        /// <returns><see langword="true"/> if a parameter is mentioned or an extension node is present.</returns>
        public bool Find(Expression e)
        {
            found = false;
            Visit(e);
            return found;
        }

        /// <inheritdoc/>
        /// <remarks>Counts one structural visit per node and checks the stack before recursing (ETC R1a-Security).</remarks>
        public override Expression? Visit(Expression? node)
        {
            if (found) return node;
            ExpressionResourceBudget.ConsumeStructuralVisit();
            return base.Visit(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (owner.IndexOf(node) >= 0) found = true;
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitExtension(Expression node)
        {
            found = true;
            return node;
        }
    }
}
