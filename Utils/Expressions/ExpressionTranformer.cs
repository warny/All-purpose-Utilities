using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static System.Reflection.BindingFlags;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.Expressions;

/// <summary>
/// Provides an abstract base class to transform or rewrite LINQ expression trees.
/// Subclasses may override the transformation logic for specific expression signatures.
/// </summary>
public abstract class ExpressionTransformer
{
    /// <summary>
    /// A reference to the <see cref="System.Linq.Expressions.Expression"/> type, used to validate that
    /// a candidate transform method's return type is compatible.
    /// </summary>
    private static readonly Type _typeOfExpression = typeof(Expression);

    /// <summary>
    /// The sentinel value <see cref="ExpressionSignatureAttribute"/> uses for its <c>ExpressionType</c>
    /// to mean "matches any node type" (see <see cref="ExpressionSignatureAttribute.Match(Expression)"/>).
    /// </summary>
    private const ExpressionType WildcardExpressionType = (ExpressionType)(-1);

    /// <summary>
    /// Every value of <see cref="System.Linq.Expressions.ExpressionType"/>, used to eagerly build one
    /// candidate bucket per node type when a transformer's <see cref="TransformPlan"/> is constructed.
    /// </summary>
    private static readonly ExpressionType[] _allExpressionTypes = Enum.GetValues<ExpressionType>();

    /// <summary>
    /// Precomputed, immutable metadata for a single parameter of a transform rule method: its declared
    /// type and (if present) its own <see cref="ExpressionSignatureAttribute"/>-derived constraint.
    /// Replaces repeated <c>ParameterInfo.GetCustomAttributes&lt;T&gt;()</c> calls on every dispatch
    /// with a one-time lookup performed while building the owning <see cref="TransformPlan"/>. Safe to
    /// reuse the same attribute instance across every dispatch because every
    /// <see cref="ExpressionSignatureAttribute"/>-derived attribute defined in this codebase
    /// (<see cref="ExpressionSignatureAttribute"/> itself, <see cref="ExpressionCallSignatureAttribute"/>,
    /// <see cref="ConstantNumericAttribute"/>, <see cref="ReturnTypeAttribute"/>) is immutable: its
    /// fields are set once in its constructor and never reassigned, so <c>Match</c> depends only on
    /// those fields and the expression being tested — never on external or mutable state.
    /// </summary>
    private readonly struct TransformParameter
    {
        /// <summary>The parameter's declared CLR type.</summary>
        public Type ParameterType { get; }

        /// <summary>
        /// The parameter's own <see cref="ExpressionSignatureAttribute"/>-derived constraint, if any;
        /// <see langword="null"/> when the parameter carries no such attribute.
        /// </summary>
        public ExpressionSignatureAttribute? Signature { get; }

        /// <summary>Initializes a new <see cref="TransformParameter"/>.</summary>
        /// <param name="parameterType">The parameter's declared CLR type.</param>
        /// <param name="signature">The parameter's own signature constraint, if any.</param>
        public TransformParameter(Type parameterType, ExpressionSignatureAttribute? signature)
        {
            ParameterType = parameterType;
            Signature = signature;
        }
    }

    /// <summary>
    /// The shape of the argument list a transform rule method expects, precomputed once so
    /// <see cref="TryInvokeTransformMethod"/> can dispatch on a simple enum instead of re-inspecting
    /// parameter count and types on every call.
    /// </summary>
    private enum InvocationKind
    {
        /// <summary>
        /// The method declares no parameters. It can never actually be reached (a rule always needs at
        /// least the node parameter, and <see cref="TryTransform"/> indexes <c>Parameters[0]</c> before
        /// invocation is attempted), preserved only to mirror this pre-existing (unreachable) case.
        /// </summary>
        None,

        /// <summary>The method declares exactly one parameter: the node itself.</summary>
        Single,

        /// <summary>
        /// The method's second parameter is exactly <c>Expression[]</c>: it receives the full prepared
        /// sub-expression array instead of positional typed parameters.
        /// </summary>
        ExpressionArray,

        /// <summary>The method declares more than one parameter, matched and passed positionally.</summary>
        Positional,
    }

    /// <summary>
    /// Precomputed, immutable metadata for a single <see cref="ExpressionSignatureAttribute"/>-annotated
    /// transform rule method: everything <see cref="TryTransform"/> and
    /// <see cref="TryInvokeTransformMethod"/> need without re-reading reflection metadata on the hot path.
    /// </summary>
    private sealed class TransformRule
    {
        /// <summary>The annotated transform rule method.</summary>
        public MethodInfo Method { get; }

        /// <summary>The method-level <see cref="ExpressionSignatureAttribute"/> that makes this a candidate rule.</summary>
        public ExpressionSignatureAttribute Signature { get; }

        /// <summary>Precomputed metadata for every parameter of <see cref="Method"/>, in declaration order.</summary>
        public TransformParameter[] Parameters { get; }

        /// <summary>The precomputed invocation shape of <see cref="Method"/>.</summary>
        public InvocationKind Kind { get; }

        /// <summary>Whether <see cref="Method"/>'s return type is assignable to <see cref="Expression"/>.</summary>
        public bool ReturnsExpression { get; }

        /// <summary>Initializes a new <see cref="TransformRule"/> with its precomputed dispatch metadata.</summary>
        public TransformRule(
            MethodInfo method,
            ExpressionSignatureAttribute signature,
            TransformParameter[] parameters,
            InvocationKind kind,
            bool returnsExpression)
        {
            Method = method;
            Signature = signature;
            Parameters = parameters;
            Kind = kind;
            ReturnsExpression = returnsExpression;
        }
    }

    /// <summary>
    /// Precomputed dispatch plan for a concrete transformer type: for every possible
    /// <see cref="ExpressionType"/>, the ordered list of candidate rules that could apply to a node of
    /// that type. A rule appears in a bucket either because it declares that exact
    /// <see cref="ExpressionType"/> or because it is a wildcard rule
    /// (<see cref="WildcardExpressionType"/>) — wildcard rules therefore appear in every bucket, since a
    /// rule matching "any node type" must remain a candidate no matter which node is being transformed.
    /// Within a bucket, rules keep the exact relative order they were declared in (the order
    /// <see cref="Type.GetMethods(BindingFlags)"/> returned), because that order is an implicit part of
    /// existing transformer behavior: a rule returning <see langword="null"/> defers to the next one, so
    /// reordering candidates would change which rule "wins". Built once per concrete transformer type
    /// and shared by every instance; immutable once constructed, so it is safe to read concurrently
    /// without locking.
    /// </summary>
    private sealed class TransformPlan
    {
        private readonly Dictionary<ExpressionType, TransformRule[]> _rulesByNodeType;

        /// <summary>Initializes a new <see cref="TransformPlan"/> from its precomputed buckets.</summary>
        public TransformPlan(Dictionary<ExpressionType, TransformRule[]> rulesByNodeType)
        {
            _rulesByNodeType = rulesByNodeType;
        }

        /// <summary>
        /// Returns the ordered candidate rules for <paramref name="nodeType"/>. This is a coarse filter
        /// only: callers must still evaluate each candidate's <see cref="TransformRule.Signature"/>
        /// <c>Match</c> before invoking it, since specialized attributes (e.g. one restricting a call to
        /// a specific method name) apply constraints this index does not encode.
        /// </summary>
        /// <param name="nodeType">The <see cref="ExpressionType"/> of the node being transformed.</param>
        /// <returns>The candidate rules for that node type, or an empty array if none exist.</returns>
        public TransformRule[] GetCandidates(ExpressionType nodeType)
            => _rulesByNodeType.TryGetValue(nodeType, out TransformRule[]? candidates)
                ? candidates
                : Array.Empty<TransformRule>();
    }

    /// <summary>
    /// This transformer type's precomputed dispatch plan, shared with every other instance of the same
    /// concrete type via <see cref="_transformPlanCache"/>.
    /// </summary>
    private readonly TransformPlan _transformPlan;

    /// <summary>
    /// Caches the built <see cref="TransformPlan"/> per concrete transformer type, since it only
    /// depends on the type and never on instance state. This lets subclasses cheaply construct a fresh
    /// instance per operation (e.g. to isolate per-call state instead of mutating a shared field)
    /// without repeating the reflection scan and plan construction on every construction.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, TransformPlan> _transformPlanCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionTransformer"/> class.
    /// During construction, it retrieves (or, on first use of the concrete type, builds and caches) the
    /// <see cref="TransformPlan"/> gathering every method marked with
    /// <see cref="ExpressionSignatureAttribute"/> from the derived type.
    /// </summary>
    protected ExpressionTransformer()
    {
        _transformPlan = _transformPlanCache.GetOrAdd(GetType(), static t => BuildPlan(t));
    }

    /// <summary>
    /// Scans <paramref name="transformerType"/> for <see cref="ExpressionSignatureAttribute"/>-annotated
    /// methods, precomputes a <see cref="TransformRule"/> for each, and buckets them by
    /// <see cref="ExpressionType"/> (with wildcard rules included in every bucket) while preserving
    /// their original relative order.
    /// </summary>
    /// <param name="transformerType">The concrete transformer type to scan.</param>
    /// <returns>The resulting <see cref="TransformPlan"/>.</returns>
    private static TransformPlan BuildPlan(Type transformerType)
    {
        List<TransformRule> rules = transformerType
            .GetMethods(Public | NonPublic | InvokeMethod | Instance)
            .Select(m => (Method: m, Attr: m.GetCustomAttributes<ExpressionSignatureAttribute>().FirstOrDefault()))
            .Where(ma => ma.Attr != null)
            .Select(ma => BuildRule(ma.Method, ma.Attr!))
            .ToList();

        var rulesByNodeType = new Dictionary<ExpressionType, TransformRule[]>();
        foreach (ExpressionType nodeType in _allExpressionTypes)
        {
            TransformRule[] candidates = rules
                .Where(r => r.Signature.ExpressionType == WildcardExpressionType || r.Signature.ExpressionType == nodeType)
                .ToArray();
            if (candidates.Length > 0)
            {
                rulesByNodeType[nodeType] = candidates;
            }
        }

        return new TransformPlan(rulesByNodeType);
    }

    /// <summary>
    /// Precomputes a <see cref="TransformRule"/> for a single annotated transform method: its
    /// per-parameter metadata (type and any <see cref="ExpressionSignatureAttribute"/>-derived
    /// constraint), its invocation shape, and whether its return type is a valid <see cref="Expression"/>.
    /// </summary>
    /// <param name="method">The annotated transform rule method.</param>
    /// <param name="signature">The method-level <see cref="ExpressionSignatureAttribute"/>.</param>
    /// <returns>The resulting <see cref="TransformRule"/>.</returns>
    private static TransformRule BuildRule(MethodInfo method, ExpressionSignatureAttribute signature)
    {
        ParameterInfo[] parameterInfos = method.GetParameters();
        var parameters = new TransformParameter[parameterInfos.Length];
        for (int i = 0; i < parameterInfos.Length; i++)
        {
            ExpressionSignatureAttribute? paramSignature = parameterInfos[i]
                .GetCustomAttributes<ExpressionSignatureAttribute>()
                .FirstOrDefault();
            parameters[i] = new TransformParameter(parameterInfos[i].ParameterType, paramSignature);
        }

        InvocationKind kind = parameters.Length switch
        {
            > 1 when parameters[1].ParameterType == typeof(Expression[]) => InvocationKind.ExpressionArray,
            > 1 => InvocationKind.Positional,
            1 => InvocationKind.Single,
            _ => InvocationKind.None,
        };

        bool returnsExpression = _typeOfExpression.IsAssignableFrom(method.ReturnType);

        return new TransformRule(method, signature, parameters, kind, returnsExpression);
    }

    /// <summary>
    /// Prepares an expression for transformation. Subclasses can override this to apply
    /// initial logic before the main <see cref="Transform(Expression)"/> switch (e.g., caching).
    /// The default implementation returns the expression unchanged.
    /// </summary>
    /// <param name="e">The expression to prepare.</param>
    /// <returns>The prepared expression.</returns>
    protected virtual Expression PrepareExpression(Expression e) => e;

    /// <summary>
    /// Readonly context produced by <see cref="PrepareTransform"/> and consumed by
    /// <see cref="TryTransform"/>/<see cref="TryInvokeTransformMethod"/>. Its own fields cannot be
    /// reassigned, but <see cref="ExpressionParameters"/> and <see cref="Parameters"/> are arrays whose
    /// elements are not protected from mutation. Pure implementation detail of
    /// <see cref="Transform(Expression)"/>: never exposed outside this class.
    /// </summary>
    private readonly struct TransformContext
    {
        /// <summary>
        /// The expression to match/finalize: for node types that are rebuilt (Unary, Binary, MethodCall,
        /// Conditional, Invocation, Lambda) this is the rebuilt node; for Constant/Parameter/default it
        /// is the original node unchanged.
        /// </summary>
        public Expression Expression { get; }

        /// <summary>
        /// The prepared sub-expressions of <see cref="Expression"/> (empty array for Constant/Parameter/
        /// default). Passed to <see cref="FinalizeExpression"/> and to the special
        /// <c>Expression[]</c>-shaped transform-method overload.
        /// </summary>
        public Expression[] ExpressionParameters { get; }

        /// <summary>
        /// The full positional argument list used to match a candidate transform method's parameters and
        /// to invoke it (index 0 is always <see cref="Expression"/> itself; for
        /// <see cref="ConstantExpression"/>, its boxed <c>Value</c> follows at index 1).
        /// </summary>
        public object[] Parameters { get; }

        /// <summary>
        /// Initializes a new <see cref="TransformContext"/> with the already-prepared expression,
        /// sub-expressions, and invocation argument list.
        /// </summary>
        /// <param name="expression">The (possibly rebuilt) expression to match/finalize.</param>
        /// <param name="expressionParameters">The prepared sub-expressions of <paramref name="expression"/>.</param>
        /// <param name="parameters">The positional argument list used to match and invoke a transform method.</param>
        public TransformContext(Expression expression, Expression[] expressionParameters, object[] parameters)
        {
            Expression = expression;
            ExpressionParameters = expressionParameters;
            Parameters = parameters;
        }
    }

    /// <summary>
    /// Applies transformation rules to a given expression, returning a (potentially) modified expression.
    /// This method checks for known signatures (via <see cref="ExpressionSignatureAttribute"/>-annotated methods)
    /// and if a match is found, invokes the corresponding transformation function.
    /// If no signature method matches, it calls <see cref="FinalizeExpression"/> by default.
    /// </summary>
    /// <param name="e">The expression to transform.</param>
    /// <returns>A possibly rewritten expression.</returns>
    protected Expression Transform(Expression e)
    {
        TransformContext context = PrepareTransform(e);

        if (TryTransform(context, out Expression? result))
        {
            // Preserves the pre-refactor behavior where a matching rule returning null (allowed for the
            // single-parameter and Expression[]-shaped overloads, see TryInvokeTransformMethod) made
            // Transform itself return null rather than falling back to FinalizeExpression.
            return result!;
        }

        return context.Expression is ConstantExpression
            ? FinalizeExpression(context.Expression, Array.Empty<Expression>())
            : FinalizeExpression(context.Expression, context.ExpressionParameters);
    }

    /// <summary>
    /// Dispatches to the node-type-specific <c>Prepare*</c> method that prepares/recurses into
    /// sub-expressions via <see cref="PrepareExpression"/>, rebuilds the node where applicable, and
    /// assembles the parameter arrays later used by <see cref="TryTransform"/>.
    /// </summary>
    private TransformContext PrepareTransform(Expression e) => e switch
    {
        ConstantExpression cc => PrepareConstant(cc),
        UnaryExpression ue => PrepareUnary(ue),
        BinaryExpression be => PrepareBinary(be),
        MethodCallExpression mce => PrepareMethodCall(mce),
        ConditionalExpression ce => PrepareConditional(ce),
        ParameterExpression pe => PrepareParameter(pe),
        InvocationExpression ie => PrepareInvocation(ie),
        LambdaExpression le => PrepareLambda(le),
        _ => PrepareDefault(e),
    };

    /// <summary>
    /// Prepares a <see cref="ConstantExpression"/>: it has no sub-expressions, and the argument list
    /// passed to candidate transform methods is the node itself followed by its boxed <c>Value</c>.
    /// </summary>
    /// <param name="cc">The constant expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareConstant(ConstantExpression cc)
        => new(cc, Array.Empty<Expression>(), [cc, cc.Value]);

    /// <summary>
    /// Prepares a <see cref="UnaryExpression"/> by preparing its <c>Operand</c> and rebuilding the
    /// node via <see cref="CopyExpression"/> so that candidate transform methods observe the prepared
    /// operand rather than the original one.
    /// </summary>
    /// <param name="ue">The unary expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareUnary(UnaryExpression ue)
    {
        Expression[] expressionParameters = [PrepareExpression(ue.Operand)];
        var copied = (UnaryExpression)CopyExpression(ue, expressionParameters);
        return new TransformContext(copied, expressionParameters, [copied, copied.Operand]);
    }

    /// <summary>
    /// Prepares a <see cref="BinaryExpression"/> by preparing its <c>Left</c> and <c>Right</c>
    /// operands and rebuilding the node via <see cref="CopyExpression"/> (which preserves
    /// <see cref="BinaryExpression.Method"/>, <see cref="BinaryExpression.IsLiftedToNull"/>, and
    /// <see cref="BinaryExpression.Conversion"/>).
    /// </summary>
    /// <param name="be">The binary expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareBinary(BinaryExpression be)
    {
        Expression[] expressionParameters =
        [
            PrepareExpression(be.Left),
            PrepareExpression(be.Right)
        ];
        var copied = (BinaryExpression)CopyExpression(be, expressionParameters);
        return new TransformContext(copied, expressionParameters, [copied, copied.Left, copied.Right]);
    }

    /// <summary>
    /// Prepares a <see cref="MethodCallExpression"/> by preparing the instance receiver (if any) and
    /// every argument, then rebuilding the call inline (rather than through <see cref="CopyExpression"/>)
    /// so the prepared receiver is preserved.
    /// </summary>
    /// <param name="mce">The method-call expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareMethodCall(MethodCallExpression mce)
    {
        // Transform the instance receiver alongside the arguments. The previous code
        // only prepared arguments, leaving mce.Object as the original (un-transformed)
        // expression. Rebuilding inline (rather than through CopyExpression) lets us
        // pass the transformed receiver without adding an extra parameter slot.
        Expression? transformedObject = mce.Object is null ? null : PrepareExpression(mce.Object);
        Expression[] expressionParameters = mce.Arguments.Select(PrepareExpression).ToArray();
        MethodCallExpression copied = transformedObject is null
            ? Expression.Call(mce.Method, expressionParameters)
            : Expression.Call(transformedObject, mce.Method, expressionParameters);

        object[] parameters = new object[mce.Arguments.Count + 1];
        parameters[0] = copied;
        Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
        return new TransformContext(copied, expressionParameters, parameters);
    }

    /// <summary>
    /// Prepares a <see cref="ConditionalExpression"/> by preparing its <c>Test</c>, <c>IfTrue</c>, and
    /// <c>IfFalse</c> branches and rebuilding the node via <see cref="CopyExpression"/>.
    /// </summary>
    /// <param name="ce">The conditional expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareConditional(ConditionalExpression ce)
    {
        // A ternary has exactly three sub-expressions (Test, IfTrue, IfFalse). Without an
        // explicit case here it fell through to the default branch, which produced an empty
        // sub-expression array; CopyExpression's Conditional branch then indexed parameters[0..2]
        // and threw IndexOutOfRangeException (see TODO-2026-07-11-pass3.md item #43 note).
        Expression[] expressionParameters =
        [
            PrepareExpression(ce.Test),
            PrepareExpression(ce.IfTrue),
            PrepareExpression(ce.IfFalse)
        ];
        var copied = (ConditionalExpression)CopyExpression(ce, expressionParameters);
        return new TransformContext(copied, expressionParameters, [copied, copied.Test, copied.IfTrue, copied.IfFalse]);
    }

    /// <summary>
    /// Prepares a <see cref="ParameterExpression"/>: it is a leaf node with no sub-expressions, so it
    /// is returned unchanged.
    /// </summary>
    /// <param name="pe">The parameter expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareParameter(ParameterExpression pe)
        => new(pe, Array.Empty<Expression>(), [pe]);

    /// <summary>
    /// Prepares an <see cref="InvocationExpression"/> by preparing the invoked target expression and
    /// every argument, then rebuilding the node via <see cref="Expression.Invoke(Expression, Expression[])"/>.
    /// </summary>
    /// <param name="ie">The invocation expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareInvocation(InvocationExpression ie)
    {
        Expression invokedExpression = PrepareExpression(ie.Expression);
        Expression[] expressionParameters = ie.Arguments.Select(PrepareExpression).ToArray();
        InvocationExpression copied = Expression.Invoke(invokedExpression, expressionParameters);

        object[] parameters = new object[ie.Arguments.Count + 1];
        parameters[0] = copied;
        Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
        return new TransformContext(copied, expressionParameters, parameters);
    }

    /// <summary>
    /// Prepares a <see cref="LambdaExpression"/> by preparing its parameters and recursively calling
    /// <see cref="Transform(Expression)"/> directly on its body (rather than <see cref="PrepareExpression"/>),
    /// then rebuilding the lambda.
    /// </summary>
    /// <param name="le">The lambda expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareLambda(LambdaExpression le)
    {
        // Recursively transform the body, and prepare parameter expressions
        Expression[] expressionParameters = le.Parameters
                                               .Select(a => (ParameterExpression)PrepareExpression(a))
                                               .ToArray();
        LambdaExpression copied = Expression.Lambda(Transform(le.Body), (ParameterExpression[])expressionParameters);

        object[] parameters = new object[le.Parameters.Count + 1];
        parameters[0] = copied;
        Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
        return new TransformContext(copied, expressionParameters, parameters);
    }

    /// <summary>
    /// Prepares any expression node not handled by a more specific <c>Prepare*</c> method (e.g.
    /// <see cref="MemberExpression"/>, <see cref="NewExpression"/>): no sub-expression preparation is
    /// attempted, and the node is passed through unchanged.
    /// </summary>
    /// <param name="e">The expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareDefault(Expression e)
        => new(e, Array.Empty<Expression>(), [e]);

    /// <summary>
    /// Iterates the candidate rules for <c>context.Expression.NodeType</c> — from
    /// <see cref="_transformPlan"/>, in original declaration order (see <see cref="TransformPlan"/>) —
    /// looking for one whose <see cref="ExpressionSignatureAttribute"/> matches and whose first
    /// parameter accepts the node; delegates the invocation itself to
    /// <see cref="TryInvokeTransformMethod"/>. The plan only narrows the search to plausible candidates:
    /// <c>Signature.Match</c> is still evaluated for every one of them below, so specialized attributes
    /// (e.g. constraining a call to a specific method name) keep filtering exactly as before. Mirrors
    /// the original foreach loop's semantics, including which conditions continue to the next rule vs.
    /// return.
    /// </summary>
    private bool TryTransform(TransformContext context, out Expression? result)
    {
        Expression e = context.Expression;
        object[] parameters = context.Parameters;

        foreach (TransformRule rule in _transformPlan.GetCandidates(e.NodeType))
        {
            // If the attribute doesn't match the expression, skip
            if (!rule.Signature.Match(e))
                continue;

            // The method must return an Expression (or derived) type
            if (!rule.ReturnsExpression)
            {
                throw new InvalidProgramException("Transform method must return an Expression type.");
            }

            // The first parameter must match the main expression
            if (!rule.Parameters[0].ParameterType.IsInstanceOfType(parameters[0]))
                continue;

            if (!TryInvokeTransformMethod(rule, context, out object? invokeResult))
                continue;

            result = (Expression?)invokeResult;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Reproduces the three original invocation branches (<c>Expression[]</c>-shaped overload,
    /// multi-parameter overload with per-parameter compatibility checks, single-parameter overload) and
    /// the "zero extra parameters" no-op case, now dispatching on the precomputed
    /// <see cref="TransformRule.Kind"/> instead of re-inspecting <see cref="ParameterInfo"/>. Returns
    /// <see langword="false"/> exactly where the original code executed <c>continue</c> against the
    /// outer foreach (incompatible parameter, invalid parameter, null result from the multi-parameter
    /// branch, or no usable parameter list). Does NOT wrap
    /// <see cref="MethodBase.Invoke(object, object[])"/> in a try/catch: any
    /// <see cref="System.Reflection.TargetInvocationException"/> thrown by the invoked rule propagates
    /// unchanged.
    /// </summary>
    private bool TryInvokeTransformMethod(TransformRule rule, TransformContext context, out object? result)
    {
        TransformParameter[] ruleParameters = rule.Parameters;
        object[] parameters = context.Parameters;

        switch (rule.Kind)
        {
            case InvocationKind.ExpressionArray:
                // The second parameter is the array of sub-expressions
                result = rule.Method.Invoke(this, new object[] { context.Expression, context.ExpressionParameters });
                return true;

            case InvocationKind.Positional:
                // Validate each expression parameter against the method parameter types
                for (int i = 1; i < ruleParameters.Length; i++)
                {
                    if (parameters[i] is Expression paramExpr)
                    {
                        if (!CheckParameter(paramExpr, ruleParameters[i]))
                        {
                            result = null;
                            return false;
                        }
                    }
                    else
                    {
                        // If it's not an Expression, check if we can assign directly
                        if (!ruleParameters[i].ParameterType.IsAssignableFrom(parameters[i].GetType()))
                        {
                            result = null;
                            return false;
                        }
                    }
                }

                result = rule.Method.Invoke(this, parameters);
                return result is not null;

            case InvocationKind.Single:
                result = rule.Method.Invoke(this, new[] { parameters[0] });
                return true;

            default:
                // No valid parameters => skip
                result = null;
                return false;
        }
    }

    /// <summary>
    /// Called if no custom transformation method (annotated with <see cref="ExpressionSignatureAttribute"/>)
    /// is found. Allows final post-processing. The default implementation throws an exception.
    /// </summary>
    /// <param name="e">The expression being finalized.</param>
    /// <param name="parameters">The sub-expressions or operands for <paramref name="e"/>.</param>
    /// <returns>A finalized expression.</returns>
    /// <exception cref="Exception">Thrown by default to indicate that transformation cannot be completed.</exception>
    protected virtual Expression FinalizeExpression(Expression e, Expression[] parameters)
    {
        throw new Exception("The expression transformation cannot be finalized.");
    }

    /// <summary>
    /// Replaces all occurrences of <paramref name="oldParameters"/> within <paramref name="e"/>
    /// with the corresponding items in <paramref name="newParameters"/>.
    /// </summary>
    /// <param name="e">The expression in which parameter references are replaced.</param>
    /// <param name="oldParameters">The parameters to remove.</param>
    /// <param name="newParameters">The new expressions that replace <paramref name="oldParameters"/>.</param>
    /// <returns>A copy of <paramref name="e"/> where specified parameters are replaced.</returns>
    protected Expression ReplaceArguments(Expression e, ParameterExpression[] oldParameters, Expression[] newParameters)
    {
        switch (e)
        {
            case ParameterExpression pe:
                {
                    int i = Array.IndexOf(oldParameters, pe);
                    return i >= 0 ? newParameters[i] : e;
                }
            case UnaryExpression ue:
                return CopyExpression(ue, ReplaceArguments(ue.Operand, oldParameters, newParameters));

            case BinaryExpression be:
                {
                    var left = ReplaceArguments(be.Left, oldParameters, newParameters);
                    var right = ReplaceArguments(be.Right, oldParameters, newParameters);
                    return CopyExpression(be, left, right);
                }
            case InvocationExpression ie:
                {
                    Expression invokedExpression = ReplaceArguments(ie.Expression, oldParameters, newParameters);
                    var arguments = ie.Arguments
                                      .Select(a => ReplaceArguments(a, oldParameters, newParameters))
                                      .ToArray();
                    return Expression.Invoke(invokedExpression, arguments);
                }
            case MethodCallExpression mce:
                {
                    Expression? replacedObject = mce.Object is null
                        ? null
                        : ReplaceArguments(mce.Object, oldParameters, newParameters);
                    var arguments = mce.Arguments
                                       .Select(a => ReplaceArguments(a, oldParameters, newParameters))
                                       .ToArray();
                    return replacedObject is null
                        ? Expression.Call(mce.Method, arguments)
                        : Expression.Call(replacedObject, mce.Method, arguments);
                }
            case ConditionalExpression ce:
                return Expression.Condition(
                    ReplaceArguments(ce.Test, oldParameters, newParameters),
                    ReplaceArguments(ce.IfTrue, oldParameters, newParameters),
                    ReplaceArguments(ce.IfFalse, oldParameters, newParameters),
                    ce.Type);
        }
        return e;
    }

    /// <summary>
    /// Creates a new expression of the same <see cref="ExpressionType"/> as <paramref name="e"/>,
    /// using the supplied <paramref name="parameters"/> as sub-expressions or arguments.
    /// If certain <see cref="ExpressionType"/> values are not supported by this switch,
    /// they are simply returned as-is or an exception is thrown.
    /// </summary>
    /// <param name="e">The original expression to copy.</param>
    /// <param name="parameters">The sub-expressions to insert into the copied expression.</param>
    /// <returns>
    /// A new expression replicating the structure of <paramref name="e"/> with
    /// possibly different sub-expressions.
    /// </returns>
    protected static Expression CopyExpression(Expression e, params Expression[] parameters)
    {
        // Use MakeBinary for every BinaryExpression so that Method, IsLiftedToNull, and
        // Conversion are all preserved. The type-specific factory methods (Expression.Add, etc.)
        // silently drop these fields; MakeBinary is the only factory that carries them all.
        // This matters for: user-defined operators (Method), Coalesce with a conversion lambda
        // (Conversion), and lifted nullable operators (IsLiftedToNull).
        if (e is BinaryExpression binaryExpr && parameters.Length >= 2)
        {
            return Expression.MakeBinary(
                binaryExpr.NodeType,
                parameters[0],
                parameters[1],
                binaryExpr.IsLiftedToNull,
                binaryExpr.Method,
                binaryExpr.Conversion);
        }

        return e.NodeType switch
        {
            ExpressionType.Add => Expression.Add(parameters[0], parameters[1]),
            ExpressionType.AddChecked => Expression.AddChecked(parameters[0], parameters[1]),
            ExpressionType.And => Expression.And(parameters[0], parameters[1]),
            ExpressionType.AndAlso => Expression.AndAlso(parameters[0], parameters[1]),
            ExpressionType.ArrayLength => Expression.ArrayLength(parameters[0]),
            ExpressionType.ArrayIndex => Expression.ArrayIndex(parameters[0], parameters[1]),
            ExpressionType.Call => CopyMethodCall((MethodCallExpression)e, parameters),
            ExpressionType.Coalesce => Expression.Coalesce(parameters[0], parameters[1]),
            ExpressionType.Conditional => Expression.Condition(parameters[0], parameters[1], parameters[2], ((ConditionalExpression)e).Type),
            ExpressionType.Constant => Expression.Constant(((ConstantExpression)e).Value, e.Type),
            ExpressionType.Convert => Expression.Convert(parameters[0], ((UnaryExpression)e).Type),
            ExpressionType.ConvertChecked => Expression.ConvertChecked(parameters[0], ((UnaryExpression)e).Type),
            ExpressionType.Divide => Expression.Divide(parameters[0], parameters[1]),
            ExpressionType.Equal => Expression.Equal(parameters[0], parameters[1]),
            ExpressionType.ExclusiveOr => Expression.ExclusiveOr(parameters[0], parameters[1]),
            ExpressionType.GreaterThan => Expression.GreaterThan(parameters[0], parameters[1]),
            ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(parameters[0], parameters[1]),
            ExpressionType.Invoke => Expression.Invoke(((InvocationExpression)e).Expression, parameters),
            ExpressionType.Lambda => e,
            ExpressionType.LeftShift => Expression.LeftShift(parameters[0], parameters[1]),
            ExpressionType.LessThan => Expression.LessThan(parameters[0], parameters[1]),
            ExpressionType.LessThanOrEqual => Expression.LessThanOrEqual(parameters[0], parameters[1]),
            ExpressionType.ListInit => e,
            ExpressionType.MemberAccess => e,
            ExpressionType.MemberInit => e,
            ExpressionType.Modulo => Expression.Modulo(parameters[0], parameters[1]),
            ExpressionType.Multiply => Expression.Multiply(parameters[0], parameters[1]),
            ExpressionType.MultiplyChecked => Expression.MultiplyChecked(parameters[0], parameters[1]),
            ExpressionType.Negate => Expression.Negate(parameters[0]),
            ExpressionType.UnaryPlus => Expression.UnaryPlus(parameters[0]),
            ExpressionType.NegateChecked => Expression.NegateChecked(parameters[0]),
            ExpressionType.New => Expression.New(((NewExpression)e).Constructor, parameters),
            ExpressionType.NewArrayInit => Expression.NewArrayInit(((NewArrayExpression)e).Type.GetElementType()!, parameters),
            ExpressionType.NewArrayBounds => Expression.NewArrayBounds(((NewArrayExpression)e).Type.GetElementType()!, parameters),
            ExpressionType.Not => Expression.Not(parameters[0]),
            ExpressionType.NotEqual => Expression.NotEqual(parameters[0], parameters[1]),
            ExpressionType.Or => Expression.Or(parameters[0], parameters[1]),
            ExpressionType.OrElse => Expression.OrElse(parameters[0], parameters[1]),
            ExpressionType.Parameter => e,
            ExpressionType.Power => Expression.Power(parameters[0], parameters[1]),
            ExpressionType.Quote => Expression.Quote(parameters[0]),
            ExpressionType.RightShift => Expression.RightShift(parameters[0], parameters[1]),
            ExpressionType.Subtract => Expression.Subtract(parameters[0], parameters[1]),
            ExpressionType.SubtractChecked => Expression.SubtractChecked(parameters[0], parameters[1]),
            ExpressionType.TypeAs => Expression.TypeAs(parameters[0], ((UnaryExpression)e).Type),
            ExpressionType.TypeIs => Expression.TypeIs(parameters[0], ((TypeBinaryExpression)e).TypeOperand),
            ExpressionType.TypeEqual => Expression.TypeEqual(parameters[0], ((TypeBinaryExpression)e).TypeOperand),
            ExpressionType.Assign => Expression.Assign(parameters[0], parameters[1]),
            ExpressionType.Block => Expression.Block(parameters),
            ExpressionType.DebugInfo => e,
            ExpressionType.Decrement => Expression.Decrement(parameters[0]),
            ExpressionType.Dynamic => e,
            ExpressionType.Default => e,
            ExpressionType.Extension => e,
            ExpressionType.Goto => e,
            ExpressionType.Increment => Expression.Increment(parameters[0]),
            ExpressionType.Index => e,
            ExpressionType.Label => e,
            ExpressionType.RuntimeVariables => e,
            ExpressionType.Loop => Expression.Loop(parameters[0]),
            ExpressionType.Switch => e,
            ExpressionType.Throw => Expression.Throw(parameters[0]),
            ExpressionType.Try => e,
            ExpressionType.Unbox => Expression.Unbox(parameters[0], ((UnaryExpression)e).Type),
            ExpressionType.AddAssign => Expression.AddAssign(parameters[0], parameters[1]),
            ExpressionType.AndAssign => Expression.AndAssign(parameters[0], parameters[1]),
            ExpressionType.DivideAssign => Expression.DivideAssign(parameters[0], parameters[1]),
            ExpressionType.ExclusiveOrAssign => Expression.ExclusiveOrAssign(parameters[0], parameters[1]),
            ExpressionType.LeftShiftAssign => Expression.LeftShiftAssign(parameters[0], parameters[1]),
            ExpressionType.ModuloAssign => Expression.ModuloAssign(parameters[0], parameters[1]),
            ExpressionType.MultiplyAssign => Expression.MultiplyAssign(parameters[0], parameters[1]),
            ExpressionType.OrAssign => Expression.OrAssign(parameters[0], parameters[1]),
            ExpressionType.PowerAssign => Expression.PowerAssign(parameters[0], parameters[1]),
            ExpressionType.RightShiftAssign => Expression.RightShiftAssign(parameters[0], parameters[1]),
            ExpressionType.SubtractAssign => Expression.SubtractAssign(parameters[0], parameters[1]),
            ExpressionType.AddAssignChecked => Expression.AddAssignChecked(parameters[0], parameters[1]),
            ExpressionType.MultiplyAssignChecked => Expression.MultiplyAssignChecked(parameters[0], parameters[1]),
            ExpressionType.SubtractAssignChecked => Expression.SubtractAssignChecked(parameters[0], parameters[1]),
            ExpressionType.PreIncrementAssign => Expression.PreIncrementAssign(parameters[0]),
            ExpressionType.PreDecrementAssign => Expression.PreDecrementAssign(parameters[0]),
            ExpressionType.PostIncrementAssign => Expression.PostIncrementAssign(parameters[0]),
            ExpressionType.PostDecrementAssign => Expression.PostDecrementAssign(parameters[0]),
            ExpressionType.OnesComplement => Expression.OnesComplement(parameters[0]),
            ExpressionType.IsTrue => Expression.IsTrue(parameters[0]),
            ExpressionType.IsFalse => Expression.IsFalse(parameters[0]),
            _ => throw new NotSupportedException($"Expression type '{e.NodeType}' is not supported.")
        };
    }

    /// <summary>
    /// Rebuilds a <see cref="MethodCallExpression"/> preserving its instance receiver. The previous code
    /// always used the static <see cref="Expression.Call(MethodInfo, Expression[])"/> overload, which
    /// dropped <see cref="MethodCallExpression.Object"/> and therefore threw an
    /// <see cref="ArgumentException"/> ("Static method requires null instance, non-static method requires
    /// non-null instance") whenever an instance method call flowed through the transformer.
    /// </summary>
    /// <param name="original">The original method-call expression being copied.</param>
    /// <param name="arguments">The (already prepared) argument sub-expressions.</param>
    /// <returns>A method-call expression with the same method and instance and the supplied arguments.</returns>
    private static Expression CopyMethodCall(MethodCallExpression original, Expression[] arguments)
    {
        return original.Object is null
            ? Expression.Call(original.Method, arguments)
            : Expression.Call(original.Object, original.Method, arguments);
    }

    /// <summary>
    /// Checks whether the given expression matches the type specified by <paramref name="parameter"/>,
    /// and if it carries its own <see cref="ExpressionSignatureAttribute"/>-derived constraint, verifies
    /// that as well.
    /// </summary>
    /// <param name="e">The expression to validate.</param>
    /// <param name="parameter">The precomputed parameter metadata to validate against.</param>
    /// <returns>True if <paramref name="e"/> is valid for the parameter; otherwise false.</returns>
    private static bool CheckParameter(Expression e, TransformParameter parameter)
    {
        // Check if the expression type is compatible with the parameter
        if (!parameter.ParameterType.IsAssignableFrom(e.GetType()))
            return false;

        // If the parameter has its own ExpressionSignatureAttribute, ensure it matches
        return parameter.Signature is null || parameter.Signature.Match(e);
    }
}

/// <summary>
/// Marks a method or parameter as having a signature requirement for a certain <see cref="ExpressionType"/>.
/// When used on a method, the method is considered for transformation if its attribute matches the current node type.
/// When used on a parameter, it further restricts which sub-expressions are permissible.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class ExpressionSignatureAttribute : Attribute
{
    /// <summary>
    /// Gets the <see cref="ExpressionType"/> that this signature attribute matches. If set to -1,
    /// any <see cref="ExpressionType"/> is permitted.
    /// </summary>
    public ExpressionType ExpressionType { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ExpressionSignatureAttribute"/> for a specific
    /// <see cref="ExpressionType"/>.
    /// </summary>
    /// <param name="expressionType">The node type to match, or -1 for any.</param>
    public ExpressionSignatureAttribute(ExpressionType expressionType)
    {
        ExpressionType = expressionType;
    }

    /// <summary>
    /// Indicates whether the given expression matches the requirements of this attribute.
    /// The default implementation checks <see cref="ExpressionType"/> or allows any if set to -1.
    /// </summary>
    /// <param name="e">The expression to match.</param>
    /// <returns>True if it matches; otherwise false.</returns>
    public virtual bool Match(Expression e)
    {
        return ExpressionType == (ExpressionType)(-1) || e.NodeType == ExpressionType;
    }
}

/// <summary>
/// Marks a method as matching only method-call expressions that invoke a specific function name
/// on a specific type. This is a specialized <see cref="ExpressionSignatureAttribute"/> for calls.
/// </summary>
public class ExpressionCallSignatureAttribute : ExpressionSignatureAttribute
{
    /// <summary>
    /// Gets the declaring type(s) that should match the method call.
    /// </summary>
    public Type[] Types { get; }

    /// <summary>
    /// Gets the method name that should match the call.
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ExpressionCallSignatureAttribute"/> for calls
    /// to <paramref name="type"/>.<paramref name="functionName"/>.
    /// </summary>
    /// <param name="type">The declaring type of the target method.</param>
    /// <param name="functionName">The name of the method to match.</param>
    public ExpressionCallSignatureAttribute(Type type, string functionName)
        : base(ExpressionType.Call)
    {
        Types = [type];
        FunctionName = functionName;
    }

    /// <summary>
    /// Creates a new instance of <see cref="ExpressionCallSignatureAttribute"/> for calls
    /// to <paramref name="types"/>.<paramref name="functionName"/>.
    /// </summary>
    /// <param name="types">One or more declaring types of the target method.</param>
    /// <param name="functionName">The name of the method to match.</param>
    public ExpressionCallSignatureAttribute(Type[] types, string functionName)
        : base(ExpressionType.Call)
    {
        Types = types;
        FunctionName = functionName;
    }

    /// <inheritdoc />
    public override bool Match(Expression e)
    {
        if (e is not MethodCallExpression ec) return false;
        return Types.Any(ec.Method.DeclaringType.IsDefinedBy) && ec.Method.Name == FunctionName;
    }
}

/// <summary>
/// A specialized attribute that indicates the expected parameter is a <see cref="ConstantExpression"/>
/// holding a numeric value, optionally restricted to a specific set of allowed numeric values.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class ConstantNumericAttribute : ExpressionSignatureAttribute
{
    /// <summary>
    /// The allowed numeric values, if any. If null, any numeric constant is allowed.
    /// </summary>
    public IReadOnlyList<double>? Values { get; }

    /// <summary>
    /// Creates a new instance allowing any numeric constant.
    /// </summary>
    public ConstantNumericAttribute()
        : base(ExpressionType.Constant)
    {
        Values = null;
    }

    /// <summary>
    /// Creates a new instance allowing only the specified numeric values.
    /// </summary>
    /// <param name="values">The allowed numeric values.</param>
    public ConstantNumericAttribute(params double[] values)
        : base(ExpressionType.Constant)
    {
        Values = values.ToImmutableArray();
    }

    /// <inheritdoc />
    public override bool Match(Expression e)
    {
        if (e is not ConstantExpression cc) return false;
        if (!NumberUtils.IsNumeric(cc.Value)) return false;

        // If no specific allowed values, any numeric constant is fine
        if (Values == null) return true;

        // Otherwise, ensure the constant's value is among the specified set
        return Values.Any(v => v == Convert.ToDouble(cc.Value));
    }
}

/// <summary>
/// A specialized attribute that indicates the matched expression's return type
/// must be assignable to a specified type. Useful for restricting the
/// type of an operand beyond its <see cref="ExpressionType"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class ReturnTypeAttribute : ExpressionSignatureAttribute
{
    /// <summary>
    /// Gets the required return type (or interface) for the matched expression.
    /// </summary>
    public Type ReturnType { get; }

    /// <summary>
    /// Creates an attribute requiring the expression type to be assignable to <paramref name="returnType"/>.
    /// </summary>
    /// <param name="returnType">The required return type or base class.</param>
    public ReturnTypeAttribute(Type returnType)
        : base((ExpressionType)(-1))
    {
        ReturnType = returnType;
    }

    /// <inheritdoc />
    public override bool Match(Expression e)
    {
        return ReturnType.IsAssignableFrom(e.Type);
    }
}
