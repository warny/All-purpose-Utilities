using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Expressions;

/// <summary>
/// Classifies what evaluating an expression tree may do besides producing its value: whether it is deterministic, reads
/// mutable state, has side effects, allocates, and which kinds of exceptions it may throw, with the parts of the
/// classification that could not be analyzed or that rely on user declarations (S5 P18 Architecture A).
/// </summary>
/// <remarks>
/// <para>
/// <b>Read-only foundation.</b> The analysis never changes a tree, never compiles, evaluates or invokes anything, and is not
/// consulted by <c>ExpressionSimplifier</c> or <c>ExpressionComparer</c>: their results and the user calls they make are
/// those of the strict internal effect gate of ETC R1b. This classification is finer
/// (exception origins, determinism, declarations, lambda bodies) and is meant to back future, explicitly enabled
/// simplification policies.
/// </para>
/// <para>
/// <b>Calls and expressions.</b> <see cref="AnalyzeMethod"/> classifies the execution of a method alone, its arguments already
/// evaluated. <see cref="Analyze(Expression)"/> classifies an expression: a call is the union of the method's own behavior,
/// of its receiver and of its arguments, plus a possible null receiver; a deterministic method called with a free parameter
/// is a deterministic expression that reads mutable state.
/// </para>
/// <para>
/// <b>Scopes.</b> A parameter or variable that is free in the analyzed tree is mutable state (its value is supplied, and may be
/// changed, by the enclosing code). A variable declared inside the tree (a block variable, the parameter of an invoked lambda,
/// and for <see cref="AnalyzeInvocation(LambdaExpression)"/> the parameters of the analyzed lambda) is local: reading it reads
/// no outside state and writing it is not a side effect, unless a closure or a quoted tree that escapes the evaluation
/// captures it, or it is passed by reference.
/// </para>
/// <para>
/// <b>Lambdas.</b> Evaluating a lambda only allocates a delegate: its body is not part of that evaluation, so its behavior is
/// <see cref="ExpressionBehaviors.Allocates"/> whatever the body does. A lambda invoked in place (<c>Invoke(lambda, args)</c>)
/// and the conversion lambda of a coalescing or compound assignment run their body, which is analyzed recursively with its
/// parameters bound. <see cref="AnalyzeInvocation(LambdaExpression)"/> classifies one invocation of a lambda. Any other
/// delegate invocation is unknown code.
/// </para>
/// <para>
/// <b>Unknown code.</b> Unannotated user methods, property getters and setters outside the trusted lists, constructors,
/// custom operators and conversions without a declaration, delegate invocations, loops, jumps, labels, switches, try blocks,
/// member and list initializers, runtime variables, debug information, dynamic operations and extension nodes are
/// <see cref="ExpressionBehaviors.UnknownCode"/>. An extension node is recognized by its class only
/// (<see cref="ExpressionOpacity.IsOpaque"/>): none of its members is read, nor the <see cref="Expression.Type"/> or
/// <see cref="Expression.NodeType"/> of a node that may be one. A dynamic operation's binder is never invoked. A reflection
/// object that is not a runtime one (a user subclass of <see cref="MethodInfo"/>, <see cref="FieldInfo"/> or
/// <see cref="PropertyInfo"/>, an emit builder) is unknown code without any
/// member read.
/// </para>
/// <para>
/// <b>Not modeled.</b> Asynchronous and resource exceptions (<see cref="OutOfMemoryException"/>, a stack overflow, a thread
/// abort), the evaluation time, and whether the evaluation terminates.
/// </para>
/// <para>
/// <b>Resources.</b> Each call is one expression operation (<see cref="ExpressionResourceLimits"/>): the input is validated
/// against the depth and node limits first, each analyzed node counts one structural visit and checks the execution stack,
/// and a call made while another operation runs on the thread joins it and can only tighten its limits. The analysis keeps
/// its state in a per-call object and in thread-safe caches of method metadata, so concurrent calls are independent.
/// </para>
/// </remarks>
public static class ExpressionBehaviorAnalysis
{
    /// <summary>Classifies the evaluation of <paramref name="expression"/>, with <see cref="ExpressionResourceLimits.Default"/> limits.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>Its behavior.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExpressionResourceLimitException">A resource limit was exceeded, or the stack ran out.</exception>
    public static ExpressionBehavior Analyze(Expression expression) => Analyze(expression, ExpressionResourceLimits.Default);

    /// <summary>Classifies the evaluation of <paramref name="expression"/>.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="resourceLimits">The resource limits of the operation.</param>
    /// <returns>Its behavior.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="resourceLimits"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExpressionResourceLimitException">A resource limit was exceeded, or the stack ran out.</exception>
    public static ExpressionBehavior Analyze(Expression expression, ExpressionResourceLimits resourceLimits)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(resourceLimits);
        return ExpressionResourceBudget.Run(resourceLimits, null, expression, null, expression, static root => new ExpressionBehavior(new Walker().AnalyzeRoot(root)));
    }

    /// <summary>
    /// Classifies the evaluation of <paramref name="expression"/> from inside the expression engine (S5 P18 Architecture C):
    /// when an operation runs on the thread, the analysis joins it directly, without opening a nested operation nor validating
    /// <paramref name="expression"/> again, which is a part of a tree the engine is already working on; otherwise it is the public
    /// entry <see cref="Analyze(Expression, ExpressionResourceLimits)"/>.
    /// </summary>
    /// <remarks>
    /// Joining the running operation keeps every guard on the analyzed nodes, rebuilt ones included: each one consumes one
    /// structural visit of the operation's budget and checks the execution stack, so a large or deeply nested operand, or a
    /// shared DAG walked once per path, is refused by the structural budget or the stack guard. A nested public call would apply
    /// the same budgets (the simplifier's limits are already those of the running operation), so only the shape validation, a
    /// second full walk, is saved.
    /// </remarks>
    /// <param name="expression">The expression.</param>
    /// <param name="resourceLimits">The limits of the calling instance, used only when no operation runs.</param>
    /// <returns>Its behavior.</returns>
    /// <exception cref="ExpressionResourceLimitException">A resource limit was exceeded, or the stack ran out.</exception>
    internal static ExpressionBehavior AnalyzeWithinOperation(Expression expression, ExpressionResourceLimits resourceLimits)
        => ExpressionResourceBudget.IsActive
            ? new ExpressionBehavior(new Walker().AnalyzeRoot(expression))
            : Analyze(expression, resourceLimits);

    /// <summary>
    /// Classifies one invocation of <paramref name="lambda"/> with arbitrary arguments, with
    /// <see cref="ExpressionResourceLimits.Default"/> limits: the behavior of its body, its parameters being local.
    /// </summary>
    /// <param name="lambda">The lambda.</param>
    /// <returns>The behavior of an invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lambda"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExpressionResourceLimitException">A resource limit was exceeded, or the stack ran out.</exception>
    public static ExpressionBehavior AnalyzeInvocation(LambdaExpression lambda) => AnalyzeInvocation(lambda, ExpressionResourceLimits.Default);

    /// <summary>Classifies one invocation of <paramref name="lambda"/> with arbitrary arguments.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <param name="resourceLimits">The resource limits of the operation.</param>
    /// <returns>The behavior of an invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lambda"/> or <paramref name="resourceLimits"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExpressionResourceLimitException">A resource limit was exceeded, or the stack ran out.</exception>
    public static ExpressionBehavior AnalyzeInvocation(LambdaExpression lambda, ExpressionResourceLimits resourceLimits)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        ArgumentNullException.ThrowIfNull(resourceLimits);
        return ExpressionResourceBudget.Run(resourceLimits, null, lambda, null, lambda, static root => new ExpressionBehavior(new Walker().AnalyzeInvocationRoot(root)));
    }

    /// <summary>
    /// Classifies the execution of <paramref name="method"/> alone, its receiver and arguments already evaluated: a trusted
    /// built-in numeric function, a known nondeterministic framework member, a <see cref="DeterministicAttribute"/>
    /// declaration, or unknown code.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>Its behavior.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
    public static ExpressionBehavior AnalyzeMethod(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return new ExpressionBehavior(ExpressionMethodBehaviors.Classify(method));
    }

    /// <summary>
    /// One analysis: the lexical scope of the code being analyzed and what escaping closures capture. Created per call and
    /// owned by one thread.
    /// </summary>
    private sealed class Walker
    {
        /// <summary>The innermost declaration index of each local in scope; created by the first declaration.</summary>
        private Dictionary<ParameterExpression, int>? _locals;

        /// <summary>
        /// The declarations in scope, in order, with the index of the declaration each one shadows (-1 for none); created by
        /// the first declaration, so a tree without binders allocates nothing but the walker.
        /// </summary>
        private List<(ParameterExpression Local, int Shadowed)>? _declarations;

        /// <summary>
        /// While the body of an escaping closure (or a quoted tree) is walked, the scope height at its creation: the locals
        /// declared below it are captured by the closure. -1 otherwise.
        /// </summary>
        private int _escapeFloor = -1;

        /// <summary>The locals captured by an escaping closure.</summary>
        private HashSet<ParameterExpression>? _captured;

        /// <summary>Whether an escaping closure holds code the walk cannot see, which may capture any local.</summary>
        private bool _capturesUnknown;

        /// <summary>The locals written by the evaluation.</summary>
        private HashSet<ParameterExpression>? _written;

        /// <summary>Gets the number of declarations in scope.</summary>
        private int Height => _declarations?.Count ?? 0;

        /// <summary>Classifies the evaluation of a root expression.</summary>
        /// <param name="root">The root.</param>
        /// <returns>Its behavior.</returns>
        public ExpressionBehaviors AnalyzeRoot(Expression root) => Complete(Visit(root));

        /// <summary>Classifies one invocation of a root lambda.</summary>
        /// <param name="lambda">The lambda.</param>
        /// <returns>The behavior of its body, its parameters being local.</returns>
        public ExpressionBehaviors AnalyzeInvocationRoot(LambdaExpression lambda)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            return Complete(VisitBody(lambda));
        }

        /// <summary>
        /// Adds the side effect of the writes to locals that an escaping closure captures: the closure observes them after
        /// the evaluation.
        /// </summary>
        /// <param name="behaviors">The behaviors of the evaluation.</param>
        /// <returns>The completed behaviors.</returns>
        private ExpressionBehaviors Complete(ExpressionBehaviors behaviors)
        {
            if (_written is null) return behaviors;
            if (_capturesUnknown || (_captured is not null && _written.Overlaps(_captured))) behaviors |= ExpressionBehaviors.SideEffects;
            return behaviors;
        }

        /// <summary>
        /// Classifies one node and its evaluated descendants. Dispatches on the runtime class first, so an extension node is
        /// recognized before any of its members could be read; the order of the cases does not matter otherwise (the classes
        /// are disjoint).
        /// </summary>
        /// <param name="expression">The node, or <see langword="null"/> for an absent optional child.</param>
        /// <returns>Its behavior.</returns>
        private ExpressionBehaviors Visit(Expression? expression)
        {
            if (expression is null) return ExpressionBehaviors.None;
            ExpressionResourceBudget.ConsumeStructuralVisit();
            return expression switch
            {
                ConstantExpression or DefaultExpression => ExpressionBehaviors.None,
                ParameterExpression parameter => VisitRead(parameter),
                BinaryExpression binary => VisitBinary(binary),
                UnaryExpression unary => VisitUnary(unary),
                MethodCallExpression call => VisitCall(call),
                MemberExpression member => VisitMember(member),
                ConditionalExpression conditional => Visit(conditional.Test) | Visit(conditional.IfTrue) | Visit(conditional.IfFalse),
                InvocationExpression invocation => VisitInvocation(invocation),
                LambdaExpression lambda => VisitEscaping(lambda),
                BlockExpression block => VisitBlock(block),
                TypeBinaryExpression typeBinary => Visit(typeBinary.Expression),
                NewExpression creation => VisitNew(creation),
                NewArrayExpression array => VisitNewArray(array),
                IndexExpression index => VisitIndex(index),
                _ => Unsupported(),
            };
        }

        /// <summary>
        /// Classifies a node family the analysis does not model, an extension node included, without reading it. Inside an
        /// escaping closure, such code may capture any local.
        /// </summary>
        /// <returns><see cref="ExpressionBehaviors.UnknownCode"/>.</returns>
        private ExpressionBehaviors Unsupported()
        {
            if (_escapeFloor >= 0) _capturesUnknown = true;
            return ExpressionBehaviors.UnknownCode;
        }

        /// <summary>Classifies the read of a parameter or variable.</summary>
        /// <param name="parameter">The parameter.</param>
        /// <returns>None for a local, <see cref="ExpressionBehaviors.ReadsMutableState"/> for a free variable.</returns>
        private ExpressionBehaviors VisitRead(ParameterExpression parameter)
        {
            if (_locals is null || !_locals.TryGetValue(parameter, out int index)) return ExpressionBehaviors.ReadsMutableState;
            if (index < _escapeFloor) (_captured ??= new(ReferenceEqualityComparer.Instance)).Add(parameter);
            return ExpressionBehaviors.None;
        }

        /// <summary>Classifies the write of a parameter or variable.</summary>
        /// <param name="parameter">The written parameter.</param>
        /// <returns>None for a local written by value, <see cref="ExpressionBehaviors.SideEffects"/> otherwise.</returns>
        private ExpressionBehaviors VisitWrite(ParameterExpression parameter)
        {
            if (_locals is null || !_locals.TryGetValue(parameter, out int index) || parameter.IsByRef) return ExpressionBehaviors.SideEffects;
            if (index < _escapeFloor)
            {
                // A closure writing a local of the enclosing code: the write happens when the closure runs, after this evaluation.
                (_captured ??= new(ReferenceEqualityComparer.Instance)).Add(parameter);
                return ExpressionBehaviors.SideEffects;
            }

            (_written ??= new(ReferenceEqualityComparer.Instance)).Add(parameter);
            return ExpressionBehaviors.None;
        }

        /// <summary>Declares locals, shadowing any outer declaration of the same variable.</summary>
        /// <param name="locals">The declared variables.</param>
        private void Declare(IReadOnlyList<ParameterExpression> locals)
        {
            if (locals.Count == 0) return;
            Dictionary<ParameterExpression, int> scope = _locals ??= new(ReferenceEqualityComparer.Instance);
            List<(ParameterExpression Local, int Shadowed)> declarations = _declarations ??= [];
            for (int i = 0; i < locals.Count; i++)
            {
                ParameterExpression local = locals[i];
                declarations.Add((local, scope.TryGetValue(local, out int shadowed) ? shadowed : -1));
                scope[local] = declarations.Count - 1;
            }
        }

        /// <summary>Ends the scope of every declaration above <paramref name="height"/>, restoring the declarations they shadowed.</summary>
        /// <param name="height">The scope height to restore.</param>
        private void Release(int height)
        {
            if (_declarations is null || _locals is null) return;
            for (int i = _declarations.Count - 1; i >= height; i--)
            {
                (ParameterExpression local, int shadowed) = _declarations[i];
                if (shadowed < 0) _locals.Remove(local);
                else _locals[local] = shadowed;
                _declarations.RemoveAt(i);
            }
        }

        /// <summary>Classifies the body of a lambda that runs, its parameters declared as locals.</summary>
        /// <param name="lambda">The lambda.</param>
        /// <returns>The behavior of its body.</returns>
        private ExpressionBehaviors VisitBody(LambdaExpression lambda)
        {
            int height = Height;
            Declare(lambda.Parameters);
            try
            {
                return Visit(lambda.Body);
            }
            finally
            {
                Release(height);
            }
        }

        /// <summary>
        /// Classifies a lambda (or a quoted tree) created but not run by the evaluation: it allocates. When locals are in
        /// scope, its body is walked to find those it captures; the behavior of the body itself is not part of the evaluation.
        /// </summary>
        /// <param name="lambda">The lambda.</param>
        /// <returns><see cref="ExpressionBehaviors.Allocates"/>.</returns>
        private ExpressionBehaviors VisitEscaping(LambdaExpression lambda)
        {
            if (Height == 0) return ExpressionBehaviors.Allocates;

            int floor = _escapeFloor;
            if (floor < 0) _escapeFloor = Height;
            try
            {
                VisitBody(lambda);
            }
            finally
            {
                _escapeFloor = floor;
            }

            return ExpressionBehaviors.Allocates;
        }

        /// <summary>
        /// Classifies an invocation: a lambda invoked in place runs its body with its parameters bound to the arguments; any
        /// other delegate is unknown code.
        /// </summary>
        /// <param name="invocation">The invocation.</param>
        /// <returns>Its behavior.</returns>
        private ExpressionBehaviors VisitInvocation(InvocationExpression invocation)
        {
            ExpressionBehaviors arguments = VisitEach(invocation.Arguments);
            if (invocation.Expression is LambdaExpression lambda)
            {
                ExpressionResourceBudget.ConsumeStructuralVisit();
                return arguments | VisitBody(lambda);
            }

            return arguments | Visit(invocation.Expression) | Unsupported();
        }

        /// <summary>Classifies a block: its variables are locals of its expressions.</summary>
        /// <param name="block">The block.</param>
        /// <returns>The union of the behaviors of its expressions.</returns>
        private ExpressionBehaviors VisitBlock(BlockExpression block)
        {
            int height = Height;
            Declare(block.Variables);
            try
            {
                return VisitEach(block.Expressions);
            }
            finally
            {
                Release(height);
            }
        }

        /// <summary>Returns the union of the behaviors of <paramref name="expressions"/>.</summary>
        /// <param name="expressions">The expressions.</param>
        /// <returns>The union of their behaviors.</returns>
        private ExpressionBehaviors VisitEach(IReadOnlyList<Expression> expressions)
        {
            ExpressionBehaviors behaviors = ExpressionBehaviors.None;
            for (int i = 0; i < expressions.Count; i++)
            {
                behaviors |= Visit(expressions[i]);
            }

            return behaviors;
        }

        /// <summary>Classifies a binary node: assignments, coalescing, then the operator with its operands.</summary>
        /// <param name="binary">The node.</param>
        /// <returns>Its behavior.</returns>
        private ExpressionBehaviors VisitBinary(BinaryExpression binary)
        {
            ExpressionType nodeType = binary.NodeType;
            if (ExpressionBehaviorRules.CompoundOperator(nodeType) is { } compound) return VisitAssignment(binary, compound);

            ExpressionBehaviors operands = Visit(binary.Left) | Visit(binary.Right);
            if (nodeType == ExpressionType.Coalesce)
            {
                // The conversion lambda runs on the left value when it is not null.
                return binary.Conversion is null ? operands : operands | VisitConversion(binary.Conversion);
            }

            ExpressionBehaviors own = binary.Method is { } method
                ? ExpressionMethodBehaviors.Classify(method)
                : ExpressionBehaviorRules.IntrinsicBinary(nodeType, binary.Type, binary.Right);
            return own | operands;
        }

        /// <summary>Classifies the conversion lambda of a coalescing or compound assignment, which runs its body.</summary>
        /// <param name="conversion">The conversion lambda.</param>
        /// <returns>The behavior of one run.</returns>
        private ExpressionBehaviors VisitConversion(LambdaExpression conversion)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            return VisitBody(conversion);
        }

        /// <summary>
        /// Classifies an assignment (<paramref name="compound"/> is <see cref="ExpressionType.Assign"/>) or a compound
        /// assignment (<paramref name="compound"/> is its operator): the value, the operator, and the write of the target.
        /// </summary>
        /// <param name="binary">The assignment.</param>
        /// <param name="compound">The operator applied before the write, or <see cref="ExpressionType.Assign"/>.</param>
        /// <returns>Its behavior.</returns>
        private ExpressionBehaviors VisitAssignment(BinaryExpression binary, ExpressionType compound)
        {
            bool reads = compound != ExpressionType.Assign;
            ExpressionBehaviors behaviors = VisitTarget(binary.Left, reads) | Visit(binary.Right);
            if (!reads) return behaviors;

            behaviors |= binary.Method is { } method
                ? ExpressionMethodBehaviors.Classify(method)
                : ExpressionBehaviorRules.IntrinsicBinary(compound, binary.Type, binary.Right);
            return binary.Conversion is null ? behaviors : behaviors | VisitConversion(binary.Conversion);
        }

        /// <summary>
        /// Classifies the target of a write: a parameter or variable, a field or property, an indexer or array element; any
        /// other target is unknown code.
        /// </summary>
        /// <param name="target">The written node.</param>
        /// <param name="reads">Whether the target is read first (compound assignment, increment).</param>
        /// <returns>The behavior of evaluating, reading and writing the target.</returns>
        private ExpressionBehaviors VisitTarget(Expression target, bool reads)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            switch (target)
            {
                case ParameterExpression parameter:
                    return VisitWrite(parameter) | (reads ? VisitRead(parameter) : ExpressionBehaviors.None);
                case MemberExpression { Member: FieldInfo field } member when ExpressionReflectionProvenance.IsRuntime(field):
                    return ExpressionBehaviors.SideEffects | ExpressionBehaviors.ReadsMutableState | VisitReceiver(member.Expression);
                case IndexExpression { Indexer: null } index:
                    return ExpressionBehaviors.SideEffects | ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowRuntime
                        | Visit(index.Object) | VisitEach(index.Arguments);
                default:
                    // A property or indexer setter is user code, and so is any other target.
                    return Unsupported();
            }
        }

        /// <summary>Classifies a unary node: quotes, throws, increments, then the operator with its operand.</summary>
        /// <param name="unary">The node.</param>
        /// <returns>Its behavior.</returns>
        private ExpressionBehaviors VisitUnary(UnaryExpression unary)
        {
            ExpressionType nodeType = unary.NodeType;
            switch (nodeType)
            {
                case ExpressionType.Quote:
                    // A quoted tree is data, created by the evaluation; like a closure, it captures the locals it references.
                    if (unary.Operand is not LambdaExpression quoted) return Unsupported();
                    ExpressionResourceBudget.ConsumeStructuralVisit();
                    return VisitEscaping(quoted);
                case ExpressionType.Throw:
                    return ExpressionBehaviors.MayThrowUserCode | Visit(unary.Operand);
                case ExpressionType.PreIncrementAssign or ExpressionType.PreDecrementAssign
                    or ExpressionType.PostIncrementAssign or ExpressionType.PostDecrementAssign:
                    ExpressionBehaviors own = unary.Method is { } increment ? ExpressionMethodBehaviors.Classify(increment) : ExpressionBehaviors.None;
                    return own | VisitTarget(unary.Operand, reads: true);
            }

            ExpressionBehaviors operand = Visit(unary.Operand);
            if (unary.Method is { } method) return ExpressionMethodBehaviors.Classify(method) | operand;

            // The intrinsic rules read the operand's type, which an extension node must not be asked for.
            return ExpressionOpacity.IsOpaque(unary.Operand)
                ? operand
                : ExpressionBehaviorRules.IntrinsicUnary(nodeType, unary.Operand.Type, unary.Type) | operand;
        }

        /// <summary>Classifies a method call: the method, the receiver (and its null check) and the arguments.</summary>
        /// <param name="call">The call.</param>
        /// <returns>Its behavior.</returns>
        private ExpressionBehaviors VisitCall(MethodCallExpression call)
            => ExpressionMethodBehaviors.Classify(call.Method) | VisitReceiver(call.Object) | VisitEach(call.Arguments);

        /// <summary>
        /// Classifies the receiver of an instance member: its evaluation, and the null reference it may be when it is of a
        /// reference type and not a non-null constant.
        /// </summary>
        /// <param name="receiver">The receiver, or <see langword="null"/> for a static member.</param>
        /// <returns>Its behavior.</returns>
        private ExpressionBehaviors VisitReceiver(Expression? receiver)
        {
            ExpressionBehaviors behaviors = Visit(receiver);
            if (receiver is null or ConstantExpression { Value: not null } || ExpressionOpacity.IsOpaque(receiver)) return behaviors;
            return receiver.Type.IsValueType ? behaviors : behaviors | ExpressionBehaviors.MayThrowRuntime;
        }

        /// <summary>
        /// Classifies a member read: a field reads mutable state unless it is read-only or constant; a property runs its
        /// getter, classified as a method.
        /// </summary>
        /// <param name="member">The node.</param>
        /// <returns>Its behavior, receiver included.</returns>
        private ExpressionBehaviors VisitMember(MemberExpression member)
        {
            MemberInfo info = member.Member;
            if (!ExpressionReflectionProvenance.IsRuntime(info)) return Unsupported() | Visit(member.Expression);

            ExpressionBehaviors own = info switch
            {
                FieldInfo field => field.IsInitOnly || field.IsLiteral ? ExpressionBehaviors.None : ExpressionBehaviors.ReadsMutableState,
                PropertyInfo property => property.GetGetMethod(nonPublic: true) is { } getter
                    ? ExpressionMethodBehaviors.Classify(getter)
                    : Unsupported(),
                _ => Unsupported(),
            };
            return own | VisitReceiver(member.Expression);
        }

        /// <summary>Classifies an object creation: a constructor is unknown code; a value type without constructor is its default.</summary>
        /// <param name="creation">The node.</param>
        /// <returns>Its behavior, arguments included.</returns>
        private ExpressionBehaviors VisitNew(NewExpression creation)
        {
            ExpressionBehaviors arguments = VisitEach(creation.Arguments);
            return creation.Constructor is null ? arguments : arguments | Unsupported();
        }

        /// <summary>Classifies an array creation: it allocates; explicit bounds may be negative.</summary>
        /// <param name="array">The node.</param>
        /// <returns>Its behavior, elements or bounds included.</returns>
        private ExpressionBehaviors VisitNewArray(NewArrayExpression array)
        {
            ExpressionBehaviors own = array.NodeType == ExpressionType.NewArrayBounds
                ? ExpressionBehaviors.Allocates | ExpressionBehaviors.MayThrowRuntime
                : ExpressionBehaviors.Allocates;
            return own | VisitEach(array.Expressions);
        }

        /// <summary>Classifies an index read: an indexer runs its getter; an array element is mutable state and may be out of range.</summary>
        /// <param name="index">The node.</param>
        /// <returns>Its behavior, receiver and arguments included.</returns>
        private ExpressionBehaviors VisitIndex(IndexExpression index)
        {
            ExpressionBehaviors own;
            if (index.Indexer is null)
            {
                own = ExpressionBehaviors.ReadsMutableState | ExpressionBehaviors.MayThrowRuntime;
            }
            else
            {
                own = ExpressionReflectionProvenance.IsRuntime(index.Indexer) && index.Indexer.GetGetMethod(nonPublic: true) is { } getter
                    ? ExpressionMethodBehaviors.Classify(getter)
                    : Unsupported();
            }

            return own | VisitReceiver(index.Object) | VisitEach(index.Arguments);
        }
    }
}
