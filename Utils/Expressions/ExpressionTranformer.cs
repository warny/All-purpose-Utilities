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
    /// The <see cref="ExpressionSignatureAttribute"/>-derived types shipped in this file, whose
    /// <see cref="ExpressionSignatureAttribute.Match(Expression)"/> override is known — by manual
    /// inspection, see the class-level remarks on <see cref="ExpressionSignatureAttribute"/> — to never
    /// accept a node whose <see cref="Expression.NodeType"/> differs from the <see cref="ExpressionType"/>
    /// declared to the attribute's constructor (or to accept every node type, for the wildcard sentinel).
    /// <see cref="BuildPlan"/> and <see cref="BuildRule"/> use this to decide, respectively, whether a
    /// method-level rule can be safely bucketed by its declared <see cref="ExpressionType"/> and whether a
    /// parameter-level constraint's attribute instance can be safely cached and reused. A third-party
    /// <see cref="ExpressionSignatureAttribute"/> subclass not in this set is not assumed to honor either
    /// invariant, and is instead handled the way the pre-indexing implementation always handled every
    /// rule: evaluated as a candidate for every node type, with a fresh attribute instance re-fetched on
    /// every parameter check.
    /// </summary>
    private static readonly HashSet<Type> _knownSignatureAttributeTypes =
    [
        typeof(ExpressionSignatureAttribute),
        typeof(ExpressionCallSignatureAttribute),
        typeof(ConstantNumericAttribute),
        typeof(ReturnTypeAttribute),
    ];

    /// <summary>
    /// Whether <paramref name="attributeType"/> is one of the <see cref="ExpressionSignatureAttribute"/>
    /// implementations shipped in this file (see <see cref="_knownSignatureAttributeTypes"/>), and
    /// therefore safe to bucket by declared <see cref="ExpressionType"/> and to cache/reuse as a single
    /// instance.
    /// </summary>
    /// <param name="attributeType">The runtime type of an <see cref="ExpressionSignatureAttribute"/> instance.</param>
    /// <returns><see langword="true"/> if known-safe; otherwise <see langword="false"/>.</returns>
    private static bool IsKnownSignatureAttributeType(Type attributeType) => _knownSignatureAttributeTypes.Contains(attributeType);

    /// <summary>
    /// Every value of <see cref="System.Linq.Expressions.ExpressionType"/>, used to eagerly build one
    /// candidate bucket per node type when a transformer's <see cref="TransformPlan"/> is constructed.
    /// </summary>
    private static readonly ExpressionType[] _allExpressionTypes = Enum.GetValues<ExpressionType>();

    /// <summary>
    /// Precomputed, immutable metadata for a single parameter of a transform rule method: its declared
    /// type and (if present) its own <see cref="ExpressionSignatureAttribute"/>-derived constraint.
    /// Replaces repeated <c>ParameterInfo.GetCustomAttributes&lt;T&gt;()</c> calls on every dispatch with
    /// a one-time lookup performed while building the owning <see cref="TransformPlan"/> — but only for
    /// the four <see cref="ExpressionSignatureAttribute"/>-derived attribute types shipped in this file
    /// (<see cref="ExpressionSignatureAttribute"/> itself, <see cref="ExpressionCallSignatureAttribute"/>,
    /// <see cref="ConstantNumericAttribute"/>, <see cref="ReturnTypeAttribute"/>), which are known to be
    /// safe to instantiate once and reuse across every dispatch (their fields are set once in their
    /// constructor and never reassigned, so <c>Match</c> depends only on those fields and the expression
    /// being tested). A parameter carrying a custom, third-party <see cref="ExpressionSignatureAttribute"/>
    /// subclass cannot be assumed stateless this way — a consumer's <c>Match</c> override could legally
    /// depend on mutable instance state — so its instance is deliberately <em>not</em> cached here;
    /// <see cref="UncachedSignatureParameter"/> lets <see cref="CheckParameter"/> keep re-fetching (and
    /// therefore re-constructing) a fresh attribute instance on every check, exactly as the pre-indexing
    /// implementation always did for every parameter attribute.
    /// </summary>
    private readonly struct TransformParameter
    {
        /// <summary>The parameter's declared CLR type.</summary>
        public Type ParameterType { get; }

        /// <summary>
        /// The parameter's own <see cref="ExpressionSignatureAttribute"/>-derived constraint, cached once
        /// because its runtime type is one of the four shipped in this file; <see langword="null"/> when
        /// the parameter carries no such attribute, or when it carries one whose type is not known to be
        /// safe to cache (see <see cref="UncachedSignatureParameter"/>).
        /// </summary>
        public ExpressionSignatureAttribute? Signature { get; }

        /// <summary>
        /// Set instead of <see cref="Signature"/> when the parameter carries an
        /// <see cref="ExpressionSignatureAttribute"/>-derived attribute whose runtime type is not one of
        /// the four shipped in this file: <see cref="CheckParameter"/> re-reads this
        /// <see cref="System.Reflection.ParameterInfo"/>'s attribute on every check instead of reusing a
        /// cached instance, since a custom subclass could be stateful. <see langword="null"/> whenever
        /// <see cref="Signature"/> is set, or when the parameter carries no attribute at all.
        /// </summary>
        public ParameterInfo? UncachedSignatureParameter { get; }

        /// <summary>Initializes a new <see cref="TransformParameter"/>.</summary>
        /// <param name="parameterType">The parameter's declared CLR type.</param>
        /// <param name="signature">The parameter's own signature constraint, if its type is known to be safe to cache.</param>
        /// <param name="uncachedSignatureParameter">
        /// The parameter's <see cref="System.Reflection.ParameterInfo"/>, set only when it carries a
        /// signature constraint whose type is not known to be safe to cache.
        /// </param>
        public TransformParameter(Type parameterType, ExpressionSignatureAttribute? signature, ParameterInfo? uncachedSignatureParameter)
        {
            ParameterType = parameterType;
            Signature = signature;
            UncachedSignatureParameter = uncachedSignatureParameter;
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
        /// least the node parameter, and <see cref="TryTransform"/> reads the first logical argument before
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

        /// <summary>
        /// Whether <see cref="Method"/>'s shape is one <see cref="DetermineFastInvokerEligibility"/>
        /// considers safe to fast-path — computed once, cheaply (no reflection beyond inspecting metadata
        /// already fetched for <see cref="Method"/> and <see cref="Parameters"/>), when this rule is built.
        /// Does not by itself mean a <see cref="System.Reflection.MethodInvoker"/> has been built yet — see
        /// <see cref="FastInvoker"/>, which defers that (comparatively expensive) step to first use.
        /// </summary>
        private readonly bool _fastInvokerEligible;

        private MethodInvoker? _fastInvoker;
        private volatile bool _fastInvokerInitialized;

        /// <summary>
        /// A cached <see cref="System.Reflection.MethodInvoker"/> for <see cref="Method"/>, used by
        /// <see cref="TryInvokeTransformMethod"/> as a faster, allocation-reduced alternative to
        /// <see cref="MethodBase.Invoke(object, object[])"/> — <see langword="null"/> when
        /// <see cref="Method"/>'s shape isn't one <see cref="DetermineFastInvokerEligibility"/>
        /// considers safe to fast-path, in which case every dispatch falls back to <see cref="Method"/>.Invoke
        /// exactly as before this optimization.
        /// </summary>
        /// <remarks>
        /// <see cref="System.Reflection.MethodInvoker.Create(MethodBase)"/> is deferred to the first actual
        /// read of this property rather than performed eagerly while building the owning
        /// <see cref="TransformPlan"/> (i.e. for every eligible rule, whether or not it is ever actually
        /// invoked): a transformer type can declare far more rules than a given call ever dispatches
        /// through, so eagerly constructing an invoker for every one of them measurably regressed
        /// construction-time cost and allocations (see the PR description that introduced this laziness)
        /// for no corresponding end-to-end benefit.
        /// <para>
        /// This uses a hand-rolled double-checked pattern — a <see langword="volatile"/>
        /// <c>_fastInvokerInitialized</c> flag guarding a plain <c>_fastInvoker</c> field, the flag written
        /// only after the field — rather than <see cref="System.Threading.LazyInitializer.EnsureInitialized{T}(ref T, ref bool, ref object?, Func{T})"/>:
        /// that overload requires a <see cref="Func{TResult}"/> factory, and a property getter would have to
        /// allocate a fresh closure over <c>this</c> on every single read to supply one — even on the
        /// overwhelmingly common already-initialized read that never actually invokes it — defeating the
        /// point of caching. <see cref="ExpressionTransformer.CreateFastInvokerCore"/> is a pure,
        /// side-effect-free function of <see cref="Method"/>, so a benign race that runs it more than once
        /// under contention is acceptable (both threads compute an equivalent result; the CLR guarantees
        /// the reference-field write itself is atomic, so no reader ever observes a torn value) — the
        /// volatile flag exists only to distinguish "not yet computed" from "computed as null" (an eligible
        /// method whose <c>Create</c> call itself failed), which a plain nullable field alone could not.
        /// </para>
        /// </remarks>
        public MethodInvoker? FastInvoker
        {
            get
            {
                if (!_fastInvokerEligible)
                {
                    return null;
                }

                if (_fastInvokerInitialized)
                {
                    return _fastInvoker;
                }

                MethodInvoker? invoker = CreateFastInvokerCore(Method);
                _fastInvoker = invoker;
                _fastInvokerInitialized = true;
                return invoker;
            }
        }

        /// <summary>Initializes a new <see cref="TransformRule"/> with its precomputed dispatch metadata.</summary>
        public TransformRule(
            MethodInfo method,
            ExpressionSignatureAttribute signature,
            TransformParameter[] parameters,
            InvocationKind kind,
            bool returnsExpression,
            bool fastInvokerEligible)
        {
            Method = method;
            Signature = signature;
            Parameters = parameters;
            Kind = kind;
            ReturnsExpression = returnsExpression;
            _fastInvokerEligible = fastInvokerEligible;
        }
    }

    /// <summary>
    /// Precomputed dispatch plan for a concrete transformer type: for every possible
    /// <see cref="ExpressionType"/>, the ordered list of candidate rules that could apply to a node of
    /// that type. A rule appears in exactly one bucket — the one matching its declared
    /// <see cref="ExpressionType"/> — only when its attribute's runtime type is known-safe (see
    /// <see cref="IsKnownSignatureAttributeType"/>); it appears in every bucket when it is a wildcard rule
    /// (<see cref="WildcardExpressionType"/>) or when its attribute's runtime type is not known-safe (a
    /// custom/third-party <see cref="ExpressionSignatureAttribute"/> subclass could legally override
    /// <c>Match</c> to accept other node types than the one declared, so it is conservatively kept a
    /// candidate everywhere, exactly as the pre-indexing linear scan evaluated every rule for every node).
    /// Within a bucket, rules keep the exact relative order they were declared in (the order
    /// <see cref="Type.GetMethods(BindingFlags)"/> returned), because that order is an implicit part of
    /// existing transformer behavior: a rule returning <see langword="null"/> defers to the next one, so
    /// reordering candidates would change which rule "wins". A bucket exists for every real
    /// <see cref="ExpressionType"/> value, even an empty one; a node whose <c>NodeType</c> is not one of
    /// those real values at all (nothing stops a third-party <see cref="Expression"/> subclass from
    /// overriding the <see langword="virtual"/> <c>NodeType</c> property with an arbitrary value) instead
    /// gets every rule, unfiltered — see <see cref="TransformPlan.GetCandidates"/>. Built once per
    /// concrete transformer type and shared by every instance; immutable once constructed, so it is safe
    /// to read concurrently without locking.
    /// </summary>
    private sealed class TransformPlan
    {
        private readonly Dictionary<ExpressionType, TransformRule[]> _rulesByNodeType;
        private readonly TransformRule[] _allRules;

        /// <summary>Initializes a new <see cref="TransformPlan"/> from its precomputed buckets.</summary>
        /// <param name="rulesByNodeType">
        /// One entry for every real <see cref="ExpressionType"/> value (see <see cref="_allExpressionTypes"/>),
        /// even when its candidate array is empty — <see cref="GetCandidates"/> relies on a successful
        /// dictionary lookup, not just a non-empty result, to distinguish "a real node type with no
        /// candidate rules" from "not a real node type at all" (see <paramref name="allRules"/>).
        /// </param>
        /// <param name="allRules">
        /// Every rule, in original declaration order, unfiltered by node type — the fallback
        /// <see cref="GetCandidates"/> returns for an <see cref="ExpressionType"/> outside
        /// <paramref name="rulesByNodeType"/>'s keys.
        /// </param>
        public TransformPlan(Dictionary<ExpressionType, TransformRule[]> rulesByNodeType, TransformRule[] allRules)
        {
            _rulesByNodeType = rulesByNodeType;
            _allRules = allRules;
        }

        /// <summary>
        /// Returns the ordered candidate rules for <paramref name="nodeType"/>. This is a coarse filter
        /// only: callers must still evaluate each candidate's <see cref="TransformRule.Signature"/>
        /// <c>Match</c> before invoking it, since specialized attributes (e.g. one restricting a call to
        /// a specific method name) apply constraints this index does not encode.
        /// </summary>
        /// <param name="nodeType">The <see cref="ExpressionType"/> of the node being transformed.</param>
        /// <returns>
        /// The candidate rules for that node type when it is one of the real <see cref="ExpressionType"/>
        /// values (an empty array if none apply); otherwise every rule, unfiltered. <see cref="Expression"/>
        /// is publicly derivable and its <c>NodeType</c> property is <see langword="virtual"/>, so nothing
        /// stops a third-party <see cref="Expression"/> subclass from returning a value outside
        /// <see cref="Enum.GetValues{TEnum}"/>'s real <see cref="ExpressionType"/> values (this includes
        /// values a future .NET version might add and this library doesn't know about yet). The
        /// pre-indexing linear scan would still evaluate every rule's <c>Match</c> against such a node —
        /// including wildcard rules, which this index would otherwise wrongly starve of a bucket entirely
        /// since one was never pre-populated for a value outside the real enum — so this falls back to
        /// the complete, unfiltered rule list to preserve that behavior exactly.
        /// </returns>
        public TransformRule[] GetCandidates(ExpressionType nodeType)
            => _rulesByNodeType.TryGetValue(nodeType, out TransformRule[]? candidates)
                ? candidates
                : _allRules;
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
    /// <see cref="ExpressionType"/> while preserving their original relative order. A rule is bucketed
    /// solely by its declared <see cref="ExpressionSignatureAttribute.ExpressionType"/> only when that
    /// attribute's runtime type is known-safe (see <see cref="IsKnownSignatureAttributeType"/>) — i.e.
    /// its <c>Match</c> override is known to never accept a node type other than the declared one.
    /// A rule using the wildcard sentinel, <em>or</em> a custom/third-party attribute type we cannot make
    /// that guarantee about, is conservatively added to every bucket, exactly as the pre-indexing linear
    /// scan evaluated every such rule against every node.
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

        var rulesByNodeType = new Dictionary<ExpressionType, List<TransformRule>>();
        foreach (ExpressionType nodeType in _allExpressionTypes)
        {
            rulesByNodeType[nodeType] = new List<TransformRule>();
        }

        foreach (TransformRule rule in rules)
        {
            bool isUnrestrictedCandidate = rule.Signature.ExpressionType == WildcardExpressionType
                || !IsKnownSignatureAttributeType(rule.Signature.GetType());

            if (isUnrestrictedCandidate)
            {
                foreach (List<TransformRule> bucket in rulesByNodeType.Values)
                {
                    bucket.Add(rule);
                }
            }
            else if (rulesByNodeType.TryGetValue(rule.Signature.ExpressionType, out List<TransformRule>? bucket))
            {
                bucket.Add(rule);
            }
            // else: ExpressionType is a value outside the real ExpressionType enum (nothing stops a
            // caller from writing e.g. [ExpressionSignature((ExpressionType)123456)]) and isn't the
            // wildcard sentinel either. No bucket exists for it, so the rule matches no node type at
            // all — exactly what the pre-indexing Match() comparison against a real e.NodeType would
            // have produced, just without ever needing to evaluate it.
        }

        // Every real ExpressionType gets an entry here, even an empty one: GetCandidates relies on the
        // dictionary lookup itself (not merely a non-empty result) to tell "a real node type with no
        // candidate rules" (fast, correct empty result) apart from "not a real node type at all" (falls
        // back to the unfiltered allRules array below). Skipping empty buckets here would make every
        // real-but-ruleless ExpressionType wrongly take that fallback too.
        var result = new Dictionary<ExpressionType, TransformRule[]>();
        foreach (KeyValuePair<ExpressionType, List<TransformRule>> bucket in rulesByNodeType)
        {
            result[bucket.Key] = bucket.Value.ToArray();
        }

        return new TransformPlan(result, rules.ToArray());
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

        // ExpressionSignatureAttribute declares [AttributeUsage(..., Inherited = true)], so a parameter
        // of an override can inherit its constraint from the corresponding parameter of the base virtual
        // method it overrides even when the override itself carries no attribute at all.
        // ParameterInfo.GetCustomAttributesData() below never walks that inheritance chain — unlike
        // GetCustomAttributes<T>() (used by CheckParameter's dynamic fallback), which does. For an
        // override, therefore, every parameter unconditionally falls back to that dynamic path instead
        // of being (mis)classified from data that can't see an inherited attribute; only a method that
        // doesn't override anything has no inheritance chain for GetCustomAttributesData() to miss.
        bool isOverride = method.GetBaseDefinition() != method;

        for (int i = 0; i < parameterInfos.Length; i++)
        {
            ParameterInfo parameterInfo = parameterInfos[i];

            if (isOverride)
            {
                parameters[i] = new TransformParameter(parameterInfo.ParameterType, null, parameterInfo);
                continue;
            }

            // Inspect CustomAttributeData first: it exposes the attribute's runtime type (AttributeType)
            // without invoking its constructor. Only known-safe attribute types (see
            // IsKnownSignatureAttributeType) are then actually instantiated here, since a custom/
            // third-party attribute's constructor could have observable side effects or throw — it must
            // only ever be constructed where the pre-indexing implementation constructed it: inside
            // CheckParameter, on demand, once per check.
            CustomAttributeData? signatureAttributeData = parameterInfo.GetCustomAttributesData()
                .FirstOrDefault(data => typeof(ExpressionSignatureAttribute).IsAssignableFrom(data.AttributeType));

            if (signatureAttributeData is null)
            {
                parameters[i] = new TransformParameter(parameterInfo.ParameterType, null, null);
            }
            else if (IsKnownSignatureAttributeType(signatureAttributeData.AttributeType))
            {
                ExpressionSignatureAttribute? paramSignature = parameterInfo
                    .GetCustomAttributes<ExpressionSignatureAttribute>()
                    .FirstOrDefault();
                parameters[i] = new TransformParameter(parameterInfo.ParameterType, paramSignature, null);
            }
            else
            {
                parameters[i] = new TransformParameter(parameterInfo.ParameterType, null, parameterInfo);
            }
        }

        InvocationKind kind = parameters.Length switch
        {
            > 1 when parameters[1].ParameterType == typeof(Expression[]) => InvocationKind.ExpressionArray,
            > 1 => InvocationKind.Positional,
            1 => InvocationKind.Single,
            _ => InvocationKind.None,
        };

        bool returnsExpression = _typeOfExpression.IsAssignableFrom(method.ReturnType);
        bool fastInvokerEligible = DetermineFastInvokerEligibility(method, parameters, kind);

        return new TransformRule(method, signature, parameters, kind, returnsExpression, fastInvokerEligible);
    }

    /// <summary>
    /// The maximum number of arguments <see cref="DetermineFastInvokerEligibility"/> considers for the
    /// <see cref="System.Reflection.MethodInvoker"/> fast path. A standalone benchmark comparing every
    /// invocation shape this class actually uses (see the PR description that introduced this constant)
    /// measured the fixed-argument <c>MethodInvoker.Invoke</c> overloads (1 through 4 arguments) 1.35x to
    /// 3.2x faster than <see cref="MethodBase.Invoke(object, object[])"/>, and allocation-free for the two
    /// shapes (<see cref="InvocationKind.Single"/>, <see cref="InvocationKind.ExpressionArray"/>) that
    /// previously allocated a fresh invocation array on every call. The same benchmark measured the
    /// <c>Span&lt;object?&gt;</c> overload — the only <see cref="System.Reflection.MethodInvoker"/> overload
    /// available for 5+ arguments — SLOWER than <see cref="MethodBase.Invoke(object, object[])"/> for a
    /// 5-argument call, so rules with more parameters than this deliberately stay on the historical
    /// <see cref="MethodBase.Invoke(object, object[])"/> path instead of routing through that slower
    /// overload. In practice, no shipped rule (across <c>Utils</c>, <c>Utils.Mathematics</c>, and their
    /// test doubles) declares more than 3 parameters (the widest shape is a <see cref="BinaryExpression"/>
    /// rule: node, left, right); this constant is set to 4 — matching the widest context this class ever
    /// builds, the 4-slot <see cref="ExpressionType.Conditional"/> context (node, test, ifTrue, ifFalse) —
    /// as forward-looking headroom rather than the narrowest value that happens to cover today's rules.
    /// </summary>
    private const int MaxFastInvokerParameterCount = 4;

    /// <summary>
    /// Determines whether <paramref name="method"/>'s shape, for its precomputed <paramref name="kind"/>,
    /// is safe to fast-path via <see cref="System.Reflection.MethodInvoker"/> — the actual (comparatively
    /// expensive) <see cref="MethodInvoker.Create(MethodBase)"/> call is deferred to
    /// <see cref="TransformRule.FastInvoker"/>'s first read (see its remarks for why), so this method only
    /// performs cheap metadata inspection of data already fetched for <paramref name="method"/> and
    /// <paramref name="parameters"/>, no reflection calls of its own.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> — meaning the historical <see cref="MethodBase.Invoke(object, object[])"/>
    /// path must be used instead — for any method shape this fast path cannot safely reproduce:
    /// <list type="bullet">
    /// <item><description>
    /// an open generic method or one still containing generic parameters: <see cref="MethodInvoker.Create"/>
    /// succeeds for these, but the resulting invoker's <c>Invoke</c> then throws
    /// <see cref="InvalidOperationException"/> UNWRAPPED — exactly like <see cref="MethodBase.Invoke(object, object[])"/>
    /// does for the same method, but our wrapping helper cannot tell that apart from a rule genuinely
    /// throwing <see cref="InvalidOperationException"/> itself, which must be wrapped;
    /// </description></item>
    /// <item><description>a VarArgs calling convention;</description></item>
    /// <item><description>a by-ref return type;</description></item>
    /// <item><description>any ref/out/pointer/by-ref-like parameter (copy-back and marshaling semantics differ from <see cref="MethodBase.Invoke(object, object[])"/>, and are not needed by any shipped rule);</description></item>
    /// <item><description>more than <see cref="MaxFastInvokerParameterCount"/> parameters (see that constant's remarks);</description></item>
    /// <item><description>an abstract method (never actually reachable here since <see cref="BuildPlan"/> only scans the concrete, instantiated transformer type, which cannot have any abstract members left — kept for defense in depth);</description></item>
    /// <item><description>
    /// for <see cref="InvocationKind.ExpressionArray"/> specifically, anything other than exactly 2 declared
    /// parameters: <see cref="TryInvokeTransformMethod"/> always invokes such a rule with exactly the node
    /// and its <c>Expression[]</c> sub-expressions, regardless of how many parameters the method actually
    /// declares, so a fast invoker built for a rule declaring 3 or more parameters (2nd one
    /// <c>Expression[]</c>, still bucketed as <see cref="InvocationKind.ExpressionArray"/> by the switch in
    /// <see cref="BuildRule"/>) would be invoked with fewer arguments than it expects.
    /// </description></item>
    /// </list>
    /// Even when this returns <see langword="true"/>, <see cref="TransformRule.FastInvoker"/> can still end
    /// up <see langword="null"/> at first use if <see cref="MethodInvoker.Create(MethodBase)"/> itself then
    /// throws for some other shape not enumerated above, rather than letting that surface as a transformer
    /// construction (or, now, first-dispatch) failure; <see cref="OutOfMemoryException"/> is deliberately
    /// left unhandled there.
    /// <para>
    /// Even for a method this returns <see langword="true"/> for, <see cref="System.Reflection.MethodInvoker"/>'s
    /// own documented remarks note that the target method "may be inlined for performance and not appear
    /// in stack traces" — unlike <see cref="MethodBase.Invoke(object, object[])"/>. The exception TYPE and
    /// wrapping structure the fast path reproduces (see <see cref="InvokeSingleRule"/> and its siblings)
    /// are therefore guaranteed identical to the historical behavior, but the exact
    /// stack-trace shape of an exception thrown through the fast path is not.
    /// </para>
    /// </remarks>
    /// <param name="method">The annotated transform rule method.</param>
    /// <param name="parameters">The method's precomputed per-parameter metadata.</param>
    /// <param name="kind">The method's precomputed invocation shape.</param>
    /// <returns><see langword="true"/> if safe to fast-path; otherwise <see langword="false"/>.</returns>
    private static bool DetermineFastInvokerEligibility(MethodInfo method, TransformParameter[] parameters, InvocationKind kind)
    {
        if (kind is not (InvocationKind.Single or InvocationKind.ExpressionArray or InvocationKind.Positional))
        {
            return false;
        }

        if (kind == InvocationKind.ExpressionArray && parameters.Length != 2)
        {
            return false;
        }

        if (method.IsAbstract
            || method.IsGenericMethodDefinition
            || method.ContainsGenericParameters
            || (method.CallingConvention & CallingConventions.VarArgs) != 0
            || method.ReturnType.IsByRef
            || parameters.Length == 0
            || parameters.Length > MaxFastInvokerParameterCount)
        {
            return false;
        }

        foreach (TransformParameter parameter in parameters)
        {
            Type parameterType = parameter.ParameterType;
            if (parameterType.IsByRef || parameterType.IsPointer || parameterType.IsByRefLike)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Actually constructs a <see cref="System.Reflection.MethodInvoker"/> for <paramref name="method"/>,
    /// called only once per rule (by <see cref="TransformRule.FastInvoker"/>, lazily, on its first read —
    /// see that property's remarks for why this is deferred rather than performed eagerly for every
    /// <see cref="DetermineFastInvokerEligibility"/>-approved rule while building the owning
    /// <see cref="TransformPlan"/>). Never called for a method <see cref="DetermineFastInvokerEligibility"/>
    /// rejected.
    /// </summary>
    /// <param name="method">The annotated transform rule method, already known eligible.</param>
    /// <returns>The constructed invoker, or <see langword="null"/> if <see cref="MethodInvoker.Create(MethodBase)"/> itself throws.</returns>
    private static MethodInvoker? CreateFastInvokerCore(MethodInfo method)
    {
        try
        {
            return MethodInvoker.Create(method);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
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
    /// reassigned, but the array returned by <see cref="MaterializeExpressionParameters"/> is not
    /// protected from mutation. Pure implementation detail of
    /// <see cref="Transform(Expression)"/>: never exposed outside this class.
    /// </summary>
    /// <remarks>
    /// For every node type except <see cref="UnaryExpression"/> and <see cref="BinaryExpression"/>, the
    /// backing <c>expressionParameters</c> field always holds a real (possibly empty) <see cref="Expression"/>
    /// array, exactly as before this type gained a lazy representation. For a <see cref="UnaryExpression"/>
    /// prepared by <see cref="PrepareUnary"/> or a <see cref="BinaryExpression"/> prepared by
    /// <see cref="PrepareBinary"/>, that field is instead <see langword="null"/>: a private sentinel meaning
    /// "the logical parameter(s) live directly on the rebuilt node — <c>((UnaryExpression)Expression).Operand</c>
    /// for Unary, <c>((BinaryExpression)Expression).Left</c>/<c>.Right</c> for Binary — and have not been
    /// array-materialized" — this lets <see cref="PrepareUnary"/>/<see cref="PrepareBinary"/> skip the
    /// <see cref="Expression"/>[1]/[2] allocation entirely on paths that only ever need positional or
    /// <see cref="InvocationKind.Single"/> access (see <see cref="GetExpressionParameter"/> and
    /// <see cref="GetInvocationArgument"/>). <see langword="null"/> is not used as a general lazy-list
    /// abstraction for any other node type: every other <c>Prepare*</c> method keeps constructing this
    /// struct through the array-accepting constructor below.
    /// </remarks>
    private readonly struct TransformContext
    {
        /// <summary>
        /// The prepared sub-expressions of <see cref="Expression"/>, or <see langword="null"/> exactly
        /// when <see cref="Expression"/> is a <see cref="UnaryExpression"/> prepared by
        /// <see cref="PrepareUnary"/> or a <see cref="BinaryExpression"/> prepared by
        /// <see cref="PrepareBinary"/> without materializing an array — see the type-level remarks.
        /// </summary>
        private readonly Expression[]? expressionParameters;

        /// <summary>
        /// The expression to match/finalize: for node types that are rebuilt (Unary, Binary, MethodCall,
        /// Conditional, Invocation, Lambda) this is the rebuilt node; for Constant/Parameter/default it
        /// is the original node unchanged.
        /// </summary>
        public Expression Expression { get; }

        /// <summary>
        /// The logical parameter count of <see cref="Expression"/>: the real array's length when one was
        /// materialized, or — for a lazy context, see the type-level remarks — 2 for a
        /// <see cref="BinaryExpression"/> (<c>Left</c>/<c>Right</c>) or 1 for a <see cref="UnaryExpression"/>
        /// (<c>Operand</c>). Binary is checked first: it is the more allocation-sensitive hot path this
        /// struct was originally introduced for (see the PR that added binary laziness), so this ordering
        /// keeps its dispatch a single type test, same as before <see cref="UnaryExpression"/> gained the
        /// same lazy representation.
        /// </summary>
        private int ExpressionParameterCount => expressionParameters?.Length ?? (Expression is BinaryExpression ? 2 : 1);

        /// <summary>Gets the number of arguments in the historical positional invocation layout.</summary>
        public int InvocationArgumentCount => Expression is ConstantExpression ? 2 : ExpressionParameterCount + 1;

        /// <summary>
        /// Initializes a new <see cref="TransformContext"/> with an already-prepared expression and its
        /// materialized sub-expressions.
        /// </summary>
        /// <param name="expression">The (possibly rebuilt) expression to match/finalize.</param>
        /// <param name="expressionParameters">The prepared sub-expressions of <paramref name="expression"/>.</param>
        public TransformContext(Expression expression, Expression[] expressionParameters)
        {
            Expression = expression;
            this.expressionParameters = expressionParameters;
        }

        /// <summary>
        /// Initializes a new lazy <see cref="TransformContext"/> for a prepared <see cref="BinaryExpression"/>
        /// whose two logical parameters (<c>Left</c>/<c>Right</c>) have not been array-materialized — see
        /// the type-level remarks.
        /// </summary>
        /// <param name="expression">The rebuilt binary expression to match/finalize.</param>
        public TransformContext(BinaryExpression expression)
        {
            Expression = expression;
            expressionParameters = null;
        }

        /// <summary>
        /// Initializes a new lazy <see cref="TransformContext"/> for a prepared <see cref="UnaryExpression"/>
        /// whose single logical parameter (<c>Operand</c>) has not been array-materialized — see the
        /// type-level remarks.
        /// </summary>
        /// <param name="expression">The rebuilt unary expression to match/finalize.</param>
        public TransformContext(UnaryExpression expression)
        {
            Expression = expression;
            expressionParameters = null;
        }

        /// <summary>
        /// Gets one logical sub-expression of <see cref="Expression"/> without requiring an array to have
        /// been materialized: reads the real array when one exists, or — for a lazy context — <c>Left</c>/
        /// <c>Right</c> directly off a <see cref="BinaryExpression"/>, or <c>Operand</c> directly off a
        /// <see cref="UnaryExpression"/>. Binary is checked first, matching <see cref="ExpressionParameterCount"/>'s
        /// ordering.
        /// </summary>
        /// <param name="index">The zero-based logical parameter index.</param>
        /// <returns>The sub-expression at <paramref name="index"/>.</returns>
        /// <exception cref="IndexOutOfRangeException">
        /// <paramref name="index"/> is outside the valid range, matching native array-indexing behavior.
        /// </exception>
        public Expression GetExpressionParameter(int index)
        {
            if (expressionParameters is not null)
            {
                return expressionParameters[index];
            }

            if (Expression is BinaryExpression binary)
            {
                return index switch
                {
                    0 => binary.Left,
                    1 => binary.Right,
                    _ => throw new IndexOutOfRangeException(),
                };
            }

            UnaryExpression unary = (UnaryExpression)Expression;
            return index switch
            {
                0 => unary.Operand,
                _ => throw new IndexOutOfRangeException(),
            };
        }

        /// <summary>
        /// Returns the real <see cref="Expression"/> array backing this context, materializing it now for
        /// a lazy context (allocating a fresh <c>[Left, Right]</c> array for a <see cref="BinaryExpression"/>,
        /// or <c>[Operand]</c> for a <see cref="UnaryExpression"/>), or returning the existing array by
        /// reference for every other context — the same array identity callers observed before this type
        /// gained a lazy representation.
        /// </summary>
        /// <returns>The materialized sub-expression array.</returns>
        public Expression[] MaterializeExpressionParameters()
        {
            if (expressionParameters is not null)
            {
                return expressionParameters;
            }

            if (Expression is BinaryExpression binary)
            {
                return [binary.Left, binary.Right];
            }

            UnaryExpression unary = (UnaryExpression)Expression;
            return [unary.Operand];
        }

        /// <summary>Gets one argument from the historical positional invocation layout without allocating it.</summary>
        /// <param name="index">The zero-based invocation argument index.</param>
        /// <returns>The expression, constant value, or prepared sub-expression at <paramref name="index"/>.</returns>
        public object? GetInvocationArgument(int index)
        {
            if (index == 0)
                return Expression;

            if (Expression is ConstantExpression constant)
            {
                if (index == 1)
                    return constant.Value;

                throw new IndexOutOfRangeException();
            }

            return GetExpressionParameter(index - 1);
        }

        /// <summary>Materializes the historical positional argument array for reflection invocation.</summary>
        /// <returns>A new positional array containing the node followed by its logical arguments.</returns>
        public object[] MaterializeInvocationArguments()
        {
            object[] arguments = new object[InvocationArgumentCount];
            for (int i = 0; i < arguments.Length; i++)
                arguments[i] = GetInvocationArgument(i)!;

            return arguments;
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
            : FinalizeExpression(context.Expression, context.MaterializeExpressionParameters());
    }

    /// <summary>
    /// Dispatches to the node-type-specific <c>Prepare*</c> method that prepares/recurses into
    /// sub-expressions via <see cref="PrepareExpression"/>, rebuilds the node where applicable, and
    /// assembles the expression parameter array later used by <see cref="TryTransform"/>.
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
        => new(cc, Array.Empty<Expression>());

    /// <summary>
    /// Prepares a <see cref="UnaryExpression"/> by preparing its <c>Operand</c> and rebuilding the
    /// node via <see cref="CopyUnaryExpression"/> so that candidate transform methods observe the
    /// prepared operand rather than the original one.
    /// </summary>
    /// <param name="ue">The unary expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    /// <remarks>
    /// Unlike <see cref="PrepareMethodCall"/>, <see cref="PrepareConditional"/>, etc., this deliberately
    /// does not build an <see cref="Expression"/>[1] array: <see cref="CopyUnaryExpression"/> rebuilds the
    /// node directly from the local <c>operand</c> variable, and the unary-specific
    /// <see cref="TransformContext"/> constructor stores no array — see <see cref="TransformContext"/>'s
    /// remarks. The rebuilt node's own <c>Operand</c> becomes the logical storage for its one prepared
    /// child, materialized into a real array only if a rule or <see cref="FinalizeExpression"/> actually
    /// needs one (<see cref="TransformContext.MaterializeExpressionParameters"/>).
    /// </remarks>
    private TransformContext PrepareUnary(UnaryExpression ue)
    {
        Expression operand = PrepareExpression(ue.Operand);
        UnaryExpression copied = CopyUnaryExpression(ue, operand);
        return new TransformContext(copied);
    }

    /// <summary>
    /// Prepares a <see cref="BinaryExpression"/> by preparing its <c>Left</c> and <c>Right</c>
    /// operands and rebuilding the node via <see cref="CopyExpression"/> (which preserves
    /// <see cref="BinaryExpression.Method"/>, <see cref="BinaryExpression.IsLiftedToNull"/>, and
    /// <see cref="BinaryExpression.Conversion"/>).
    /// </summary>
    /// <param name="be">The binary expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    /// <remarks>
    /// Unlike most other <c>Prepare*</c> methods (only <see cref="PrepareUnary"/> shares this), this
    /// deliberately does not build an <see cref="Expression"/>[2] array: <see cref="CopyBinaryExpression"/>
    /// rebuilds the node directly from the local <c>left</c>/<c>right</c> variables, and the
    /// binary-specific <see cref="TransformContext"/> constructor stores no array — see
    /// <see cref="TransformContext"/>'s remarks. The rebuilt node's own <c>Left</c>/<c>Right</c> become the
    /// logical storage for its two prepared children, materialized into a real array only if a rule or
    /// <see cref="FinalizeExpression"/> actually needs one (<see cref="TransformContext.MaterializeExpressionParameters"/>).
    /// </remarks>
    private TransformContext PrepareBinary(BinaryExpression be)
    {
        Expression left = PrepareExpression(be.Left);
        Expression right = PrepareExpression(be.Right);
        BinaryExpression copied = CopyBinaryExpression(be, left, right);
        return new TransformContext(copied);
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

        // Indexed loop instead of Select(...).ToArray(): mce.Arguments is a ReadOnlyCollection
        // (already IList-backed, so ToArray() would allocate the same exact-size array), but this
        // avoids the iterator/delegate overhead of Select for what is a hot path. The receiver above
        // is still prepared before any argument, and arguments are prepared strictly in order.
        int argumentCount = mce.Arguments.Count;
        Expression[] expressionParameters = argumentCount == 0
            ? Array.Empty<Expression>()
            : new Expression[argumentCount];
        for (int i = 0; i < argumentCount; i++)
        {
            expressionParameters[i] = PrepareExpression(mce.Arguments[i]);
        }

        MethodCallExpression copied = transformedObject is null
            ? Expression.Call(mce.Method, expressionParameters)
            : Expression.Call(transformedObject, mce.Method, expressionParameters);

        return new TransformContext(copied, expressionParameters);
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
        return new TransformContext(copied, expressionParameters);
    }

    /// <summary>
    /// Prepares a <see cref="ParameterExpression"/>: it is a leaf node with no sub-expressions, so it
    /// is returned unchanged.
    /// </summary>
    /// <param name="pe">The parameter expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareParameter(ParameterExpression pe)
        => new(pe, Array.Empty<Expression>());

    /// <summary>
    /// Prepares an <see cref="InvocationExpression"/> by preparing the invoked target expression and
    /// every argument, then rebuilding the node via <see cref="Expression.Invoke(Expression, Expression[])"/>.
    /// </summary>
    /// <param name="ie">The invocation expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareInvocation(InvocationExpression ie)
    {
        Expression invokedExpression = PrepareExpression(ie.Expression);

        // Indexed loop instead of Select(...).ToArray(): the target is prepared before any
        // argument, and arguments are prepared strictly in order (see PrepareMethodCall for why
        // this avoids Select's iterator/delegate overhead without changing allocation counts).
        int argumentCount = ie.Arguments.Count;
        Expression[] expressionParameters = argumentCount == 0
            ? Array.Empty<Expression>()
            : new Expression[argumentCount];
        for (int i = 0; i < argumentCount; i++)
        {
            expressionParameters[i] = PrepareExpression(ie.Arguments[i]);
        }

        InvocationExpression copied = Expression.Invoke(invokedExpression, expressionParameters);

        return new TransformContext(copied, expressionParameters);
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
        // Indexed loop instead of Select(...).ToArray(): parameters must all be prepared, in
        // order, before Transform(le.Body) runs below (a subclass may rely on that ordering).
        // The array is declared and allocated as ParameterExpression[], not Expression[], so its
        // runtime type stays ParameterExpression[] even though it is stored through the
        // Expression[]-typed TransformContext.expressionParameters field — code elsewhere (and the
        // Expression.Lambda call just below) still depends on that runtime type. The explicit cast
        // is preserved so a PrepareExpression override returning the wrong type still throws
        // InvalidCastException immediately, before the body is ever transformed.
        int parameterCount = le.Parameters.Count;
        ParameterExpression[] expressionParameters = parameterCount == 0
            ? Array.Empty<ParameterExpression>()
            : new ParameterExpression[parameterCount];
        for (int i = 0; i < parameterCount; i++)
        {
            expressionParameters[i] = (ParameterExpression)PrepareExpression(le.Parameters[i]);
        }

        LambdaExpression copied = Expression.Lambda(Transform(le.Body), expressionParameters);

        return new TransformContext(copied, expressionParameters);
    }

    /// <summary>
    /// Prepares any expression node not handled by a more specific <c>Prepare*</c> method (e.g.
    /// <see cref="MemberExpression"/>, <see cref="NewExpression"/>): no sub-expression preparation is
    /// attempted, and the node is passed through unchanged.
    /// </summary>
    /// <param name="e">The expression to prepare.</param>
    /// <returns>The resulting <see cref="TransformContext"/>.</returns>
    private TransformContext PrepareDefault(Expression e)
        => new(e, Array.Empty<Expression>());

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
        object[]? materializedInvocationArguments = null;

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
            object? firstArgument = materializedInvocationArguments is null
                ? context.Expression
                : materializedInvocationArguments[0];
            if (!rule.Parameters[0].ParameterType.IsInstanceOfType(firstArgument))
                continue;

            if (!TryInvokeTransformMethod(rule, context, ref materializedInvocationArguments, out object? invokeResult))
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
    private bool TryInvokeTransformMethod(
        TransformRule rule,
        TransformContext context,
        ref object[]? materializedInvocationArguments,
        out object? result)
    {
        TransformParameter[] ruleParameters = rule.Parameters;

        switch (rule.Kind)
        {
            case InvocationKind.ExpressionArray:
                // The second parameter is the array of sub-expressions. Materializes it on demand: for a
                // lazy BinaryExpression context this is the first point an Expression[2] is actually
                // required (an ExpressionArray rule always terminates dispatch once invoked — see
                // TransformRule.Kind's remarks — so there is no later fallback that could reuse a
                // pre-materialized array anyway).
                result = InvokeExpressionArrayRule(rule, context.Expression, context.MaterializeExpressionParameters());
                return true;

            case InvocationKind.Positional:
                // Validate each expression parameter against the method parameter types
                for (int i = 1; i < ruleParameters.Length; i++)
                {
                    object? argument = GetInvocationArgument(context, materializedInvocationArguments, i);
                    if (argument is Expression paramExpr)
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
                        if (!ruleParameters[i].ParameterType.IsAssignableFrom(argument!.GetType()))
                        {
                            result = null;
                            return false;
                        }
                    }
                }

                result = InvokePositionalRule(rule, context, ref materializedInvocationArguments);
                return result is not null;

            case InvocationKind.Single:
                result = InvokeSingleRule(
                    rule,
                    materializedInvocationArguments is null ? context.Expression : materializedInvocationArguments[0]);
                return true;

            default:
                // No valid parameters => skip
                result = null;
                return false;
        }
    }

    /// <summary>
    /// Gets a positional argument from a previously materialized reflection array when present, or
    /// directly from the logical context otherwise. Reusing the array preserves any reflection-driven
    /// slot mutations for later candidate rules.
    /// </summary>
    /// <param name="context">The logical arguments for the current node.</param>
    /// <param name="materializedInvocationArguments">The cached reflection array, if one has been created.</param>
    /// <param name="index">The zero-based invocation argument index.</param>
    /// <returns>The argument at <paramref name="index"/>.</returns>
    private static object? GetInvocationArgument(
        TransformContext context,
        object[]? materializedInvocationArguments,
        int index)
        => materializedInvocationArguments is null
            ? context.GetInvocationArgument(index)
            : materializedInvocationArguments[index];

    /// <summary>
    /// Invokes a <see cref="InvocationKind.Single"/>-shaped rule: <paramref name="node"/> is the sole
    /// argument. Uses <see cref="TransformRule.FastInvoker"/> when available (no invocation array is
    /// allocated, unlike the <c>[node]</c> array literal the fallback below still builds); otherwise falls back to
    /// <see cref="MethodBase.Invoke(object, object[])"/> exactly as before this optimization.
    /// </summary>
    /// <param name="rule">The rule to invoke.</param>
    /// <param name="node">The node being transformed, i.e. the rule's sole argument.</param>
    /// <returns>The rule's return value.</returns>
    private object? InvokeSingleRule(TransformRule rule, object node)
    {
        if (rule.FastInvoker is MethodInvoker invoker)
        {
            try
            {
                return invoker.Invoke(this, node);
            }
            catch (Exception ex)
            {
                // Reproduces MethodBase.Invoke's contract: any exception surfacing from the rule body
                // (guaranteed here, since DetermineFastInvokerEligibility already validated the argument shape) is
                // wrapped in a NEW TargetInvocationException, even when it is itself already one (see the
                // Transform_RuleThrowsTargetInvocationException_IsDoubleWrapped regression test) or an
                // OutOfMemoryException (see Transform_RuleThrowsOutOfMemoryException_WrappedInTargetInvocationException
                // — MethodBase.Invoke does not treat it specially either).
                throw new TargetInvocationException(ex);
            }
        }

        return rule.Method.Invoke(this, [node]);
    }

    /// <summary>
    /// Invokes an <see cref="InvocationKind.ExpressionArray"/>-shaped rule: <paramref name="expression"/>
    /// and <paramref name="expressionParameters"/> are its two arguments. Uses
    /// <see cref="TransformRule.FastInvoker"/> when available (no invocation array is allocated, unlike
    /// the <c>[expression, expressionParameters]</c> array literal the fallback below still builds); otherwise falls back to
    /// <see cref="MethodBase.Invoke(object, object[])"/> exactly as before this optimization.
    /// </summary>
    /// <param name="rule">The rule to invoke.</param>
    /// <param name="expression">The (possibly rebuilt) node being transformed.</param>
    /// <param name="expressionParameters">Its prepared sub-expressions.</param>
    /// <returns>The rule's return value.</returns>
    private object? InvokeExpressionArrayRule(TransformRule rule, Expression expression, Expression[] expressionParameters)
    {
        if (rule.FastInvoker is MethodInvoker invoker)
        {
            try
            {
                return invoker.Invoke(this, expression, expressionParameters);
            }
            catch (Exception ex)
            {
                // See InvokeSingleRule's remarks: wraps exactly like MethodBase.Invoke, including
                // double-wrapping a rule-thrown TargetInvocationException and wrapping a rule-thrown
                // OutOfMemoryException.
                throw new TargetInvocationException(ex);
            }
        }

        return rule.Method.Invoke(this, [expression, expressionParameters]);
    }

    /// <summary>
    /// Invokes an <see cref="InvocationKind.Positional"/>-shaped rule with the arguments in
    /// <paramref name="context"/>. Uses <see cref="TransformRule.FastInvoker"/> only when the logical argument count
    /// exactly matches <see cref="TransformRule.Parameters"/>' length — the same node type can supply a
    /// different number of arguments than a given rule declares (e.g. a <see cref="MethodCallExpression"/>
    /// with a varying argument count), and a mismatch must keep reaching
    /// <see cref="MethodBase.Invoke(object, object[])"/> to reproduce its historical
    /// <see cref="TargetParameterCountException"/> (too many) — the "too few" case never reaches this
    /// method at all: it fails earlier, in <see cref="TryInvokeTransformMethod"/>'s per-parameter
    /// validation loop, with an <see cref="IndexOutOfRangeException"/>. Otherwise falls back to
    /// <see cref="MethodBase.Invoke(object, object[])"/> exactly as before this optimization.
    /// </summary>
    /// <param name="rule">The rule to invoke.</param>
    /// <param name="context">The logical positional arguments for the current node.</param>
    /// <param name="materializedInvocationArguments">The cached reflection argument array, if already required.</param>
    /// <returns>The rule's return value.</returns>
    private object? InvokePositionalRule(
        TransformRule rule,
        TransformContext context,
        ref object[]? materializedInvocationArguments)
    {
        if (rule.FastInvoker is MethodInvoker invoker && context.InvocationArgumentCount == rule.Parameters.Length)
        {
            try
            {
                switch (context.InvocationArgumentCount)
                {
                    case 2:
                        return invoker.Invoke(
                            this,
                            GetInvocationArgument(context, materializedInvocationArguments, 0),
                            GetInvocationArgument(context, materializedInvocationArguments, 1));
                    case 3:
                        return invoker.Invoke(
                            this,
                            GetInvocationArgument(context, materializedInvocationArguments, 0),
                            GetInvocationArgument(context, materializedInvocationArguments, 1),
                            GetInvocationArgument(context, materializedInvocationArguments, 2));
                    case 4:
                        return invoker.Invoke(
                            this,
                            GetInvocationArgument(context, materializedInvocationArguments, 0),
                            GetInvocationArgument(context, materializedInvocationArguments, 1),
                            GetInvocationArgument(context, materializedInvocationArguments, 2),
                            GetInvocationArgument(context, materializedInvocationArguments, 3));
                }
            }
            catch (Exception ex)
            {
                // See InvokeSingleRule's remarks: wraps exactly like MethodBase.Invoke, including
                // double-wrapping a rule-thrown TargetInvocationException and wrapping a rule-thrown
                // OutOfMemoryException.
                throw new TargetInvocationException(ex);
            }
        }

        materializedInvocationArguments ??= context.MaterializeInvocationArguments();
        return rule.Method.Invoke(this, materializedInvocationArguments);
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
        => ReplaceArgumentsCore(e, oldParameters, newParameters);

    /// <summary>
    /// Collection-based core of <see cref="ReplaceArguments"/>. Accepts <see cref="IReadOnlyList{T}"/>
    /// rather than arrays so a caller that already holds a
    /// <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}"/> (e.g.
    /// <see cref="LambdaExpression.Parameters"/>, <see cref="InvocationExpression.Arguments"/>) can
    /// pass it directly instead of copying it into an array first. <paramref name="oldParameters"/>
    /// and <paramref name="newParameters"/> are deliberately not validated up front: a node type this
    /// switch does not recognize (see the default fallthrough) returns <paramref name="e"/> unchanged
    /// without ever consulting either list, and <see cref="ReplaceArguments"/>'s historical
    /// null/short-array exception behavior for the protected array-based overload depends on that lazy
    /// access timing.
    /// </summary>
    /// <param name="e">The expression in which parameter references are replaced.</param>
    /// <param name="oldParameters">
    /// The parameters to remove; may be <see langword="null"/> if only node types that never read it are
    /// encountered.
    /// </param>
    /// <param name="newParameters">
    /// The replacement expressions; may be <see langword="null"/> if only node types that never read it
    /// are encountered.
    /// </param>
    /// <returns>A copy of <paramref name="e"/> where specified parameters are replaced.</returns>
    internal Expression ReplaceArgumentsCore(Expression e, IReadOnlyList<ParameterExpression>? oldParameters, IReadOnlyList<Expression>? newParameters)
    {
        switch (e)
        {
            case ParameterExpression pe:
                {
                    // Array.IndexOf is used directly whenever oldParameters actually is (or is a null
                    // reference of static type) ParameterExpression[] -- i.e. whenever this call
                    // originates from the protected array-based overload -- so that overload keeps its
                    // exact original first-match/equality/null-exception behavior. A non-null,
                    // non-array IReadOnlyList<ParameterExpression> (the new collection-based callers,
                    // e.g. LambdaExpression.Parameters) falls back to an equivalent manual scan.
                    int i = oldParameters is ParameterExpression[] array
                        ? Array.IndexOf(array, pe)
                        : oldParameters is null
                            ? Array.IndexOf<ParameterExpression>(null!, pe)
                            : IndexOfParameter(oldParameters, pe);

                    if (i < 0) return e;

                    // Same reasoning as above, mirrored for newParameters: a real Expression[] is
                    // indexed natively so a too-short array still throws IndexOutOfRangeException (not
                    // an interface-dispatch-flavored exception), and a null reference throws on the
                    // element access exactly like the historical array-typed overload did.
                    return newParameters is Expression[] newArray ? newArray[i] : newParameters![i];
                }
            case UnaryExpression ue:
                return CopyExpression(ue, ReplaceArgumentsCore(ue.Operand, oldParameters, newParameters));

            case BinaryExpression be:
                {
                    var left = ReplaceArgumentsCore(be.Left, oldParameters, newParameters);
                    var right = ReplaceArgumentsCore(be.Right, oldParameters, newParameters);
                    return CopyExpression(be, left, right);
                }
            case InvocationExpression ie:
                {
                    Expression invokedExpression = ReplaceArgumentsCore(ie.Expression, oldParameters, newParameters);

                    int argumentCount = ie.Arguments.Count;
                    Expression[] arguments = argumentCount == 0
                        ? Array.Empty<Expression>()
                        : new Expression[argumentCount];
                    for (int i = 0; i < argumentCount; i++)
                    {
                        arguments[i] = ReplaceArgumentsCore(ie.Arguments[i], oldParameters, newParameters);
                    }

                    return Expression.Invoke(invokedExpression, arguments);
                }
            case MethodCallExpression mce:
                {
                    Expression? replacedObject = mce.Object is null
                        ? null
                        : ReplaceArgumentsCore(mce.Object, oldParameters, newParameters);

                    int argumentCount = mce.Arguments.Count;
                    Expression[] arguments = argumentCount == 0
                        ? Array.Empty<Expression>()
                        : new Expression[argumentCount];
                    for (int i = 0; i < argumentCount; i++)
                    {
                        arguments[i] = ReplaceArgumentsCore(mce.Arguments[i], oldParameters, newParameters);
                    }

                    return replacedObject is null
                        ? Expression.Call(mce.Method, arguments)
                        : Expression.Call(replacedObject, mce.Method, arguments);
                }
            case ConditionalExpression ce:
                return Expression.Condition(
                    ReplaceArgumentsCore(ce.Test, oldParameters, newParameters),
                    ReplaceArgumentsCore(ce.IfTrue, oldParameters, newParameters),
                    ReplaceArgumentsCore(ce.IfFalse, oldParameters, newParameters),
                    ce.Type);
        }
        return e;
    }

    /// <summary>
    /// Linear scan matching <see cref="Array.IndexOf{T}(T[], T)"/>'s first-match-wins semantics and
    /// default equality comparer, for an <see cref="IReadOnlyList{T}"/> that is not itself an array
    /// (the collection-based callers of <see cref="ReplaceArgumentsCore"/> pass a
    /// <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}"/> here, e.g.
    /// <see cref="LambdaExpression.Parameters"/>).
    /// </summary>
    /// <param name="list">The list to search.</param>
    /// <param name="value">The value to find.</param>
    /// <returns>The index of the first matching element, or -1 if none is found.</returns>
    private static int IndexOfParameter(IReadOnlyList<ParameterExpression> list, ParameterExpression value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (EqualityComparer<ParameterExpression>.Default.Equals(list[i], value))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Rebuilds a <see cref="BinaryExpression"/> from its (already prepared) <paramref name="left"/> and
    /// <paramref name="right"/> operands, preserving <see cref="BinaryExpression.Method"/>,
    /// <see cref="BinaryExpression.IsLiftedToNull"/>, and <see cref="BinaryExpression.Conversion"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Expression.MakeBinary(ExpressionType, Expression, Expression, bool, MethodInfo, LambdaExpression)"/>
    /// rather than a type-specific factory (<see cref="Expression.Add(Expression, Expression)"/>, etc.),
    /// since those silently drop <see cref="BinaryExpression.Method"/>,
    /// <see cref="BinaryExpression.IsLiftedToNull"/>, and <see cref="BinaryExpression.Conversion"/>. This
    /// matters for user-defined operators (<c>Method</c>), <c>Coalesce</c> with a conversion lambda
    /// (<c>Conversion</c>), and lifted nullable operators (<c>IsLiftedToNull</c>). Shared by
    /// <see cref="PrepareBinary"/> (which calls this directly, without going through the params-array
    /// <see cref="CopyExpression"/> overload) and by <see cref="CopyExpression"/>'s own binary branch.
    /// </remarks>
    /// <param name="expression">The original binary expression being copied.</param>
    /// <param name="left">The (already prepared) left operand.</param>
    /// <param name="right">The (already prepared) right operand.</param>
    /// <returns>A binary expression with the same node type and metadata but the supplied operands.</returns>
    private static BinaryExpression CopyBinaryExpression(BinaryExpression expression, Expression left, Expression right)
    {
        return Expression.MakeBinary(
            expression.NodeType,
            left,
            right,
            expression.IsLiftedToNull,
            expression.Method,
            expression.Conversion);
    }

    /// <summary>
    /// Rebuilds a <see cref="UnaryExpression"/> from its (already prepared) <paramref name="operand"/>,
    /// dispatching on <paramref name="expression"/>'s <see cref="Expression.NodeType"/>.
    /// </summary>
    /// <remarks>
    /// Intentionally mirrors <see cref="CopyExpression"/>'s own <see cref="UnaryExpression"/> branches
    /// case-for-case, rather than using a single generic
    /// <see cref="Expression.MakeUnary(ExpressionType, Expression, Type, MethodInfo)"/> call: the
    /// historical per-node-type factories used here (<see cref="Expression.Negate(Expression)"/>,
    /// <see cref="Expression.Convert(Expression, Type)"/>, etc.) do not forward
    /// <see cref="UnaryExpression.Method"/> the way <see cref="CopyBinaryExpression"/> forwards
    /// <see cref="BinaryExpression.Method"/> via <see cref="Expression.MakeBinary(ExpressionType, Expression, Expression, bool, MethodInfo, LambdaExpression)"/>
    /// — an explicit method on the original node is silently dropped, and a typed <c>Throw</c>'s declared
    /// <see cref="Expression.Type"/> is likewise not reproduced (<see cref="Expression.Throw(Expression)"/>,
    /// not the typed overload). Both are pre-existing, historical quirks of <see cref="CopyExpression"/>;
    /// this helper exists purely to let <see cref="PrepareUnary"/> skip the params-array
    /// <see cref="CopyExpression"/> overload's <see cref="Expression"/>[] allocation, not to change what
    /// gets reconstructed — see the characterization tests in
    /// <c>ExpressionTransformerUnaryLazyParametersTests</c> that pin this down against
    /// <see cref="CopyExpression"/> as the behavioral oracle.
    /// </remarks>
    /// <param name="expression">The original unary expression being copied.</param>
    /// <param name="operand">The (already prepared) operand.</param>
    /// <returns>A unary expression with the same node type and (where reproduced) metadata but the supplied operand.</returns>
    private static UnaryExpression CopyUnaryExpression(UnaryExpression expression, Expression operand)
    {
        return expression.NodeType switch
        {
            ExpressionType.ArrayLength => Expression.ArrayLength(operand),
            ExpressionType.Convert => Expression.Convert(operand, expression.Type),
            ExpressionType.ConvertChecked => Expression.ConvertChecked(operand, expression.Type),
            ExpressionType.Negate => Expression.Negate(operand),
            ExpressionType.UnaryPlus => Expression.UnaryPlus(operand),
            ExpressionType.NegateChecked => Expression.NegateChecked(operand),
            ExpressionType.Not => Expression.Not(operand),
            ExpressionType.Quote => Expression.Quote(operand),
            ExpressionType.TypeAs => Expression.TypeAs(operand, expression.Type),
            ExpressionType.Decrement => Expression.Decrement(operand),
            ExpressionType.Increment => Expression.Increment(operand),
            ExpressionType.Throw => Expression.Throw(operand),
            ExpressionType.Unbox => Expression.Unbox(operand, expression.Type),
            ExpressionType.PreIncrementAssign => Expression.PreIncrementAssign(operand),
            ExpressionType.PreDecrementAssign => Expression.PreDecrementAssign(operand),
            ExpressionType.PostIncrementAssign => Expression.PostIncrementAssign(operand),
            ExpressionType.PostDecrementAssign => Expression.PostDecrementAssign(operand),
            ExpressionType.OnesComplement => Expression.OnesComplement(operand),
            ExpressionType.IsTrue => Expression.IsTrue(operand),
            ExpressionType.IsFalse => Expression.IsFalse(operand),
            _ => throw new NotSupportedException($"Expression type '{expression.NodeType}' is not supported."),
        };
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
        // Delegates to CopyBinaryExpression (see its remarks for why MakeBinary specifically is used)
        // so PrepareBinary can share the exact same reconstruction logic without going through this
        // params-array overload.
        if (e is BinaryExpression binaryExpr && parameters.Length >= 2)
        {
            return CopyBinaryExpression(binaryExpr, parameters[0], parameters[1]);
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

        // If the parameter has its own known-safe ExpressionSignatureAttribute, ensure it matches
        // using the cached instance.
        if (parameter.Signature is not null)
            return parameter.Signature.Match(e);

        // A custom/third-party attribute type: re-fetch (and therefore re-construct) a fresh instance
        // on every check, exactly like the pre-indexing implementation always did, since such an
        // attribute could legally be stateful (see TransformParameter.UncachedSignatureParameter).
        if (parameter.UncachedSignatureParameter is not null)
        {
            ExpressionSignatureAttribute? signature = parameter.UncachedSignatureParameter
                .GetCustomAttributes<ExpressionSignatureAttribute>()
                .FirstOrDefault();
            return signature is null || signature.Match(e);
        }

        // No signature attribute at all.
        return true;
    }
}

/// <summary>
/// Marks a method or parameter as having a signature requirement for a certain <see cref="ExpressionType"/>.
/// When used on a method, the method is considered for transformation if its attribute matches the current node type.
/// When used on a parameter, it further restricts which sub-expressions are permissible.
/// </summary>
/// <remarks>
/// The three <see cref="Match"/> overrides shipped in this file (<see cref="ExpressionCallSignatureAttribute"/>,
/// <see cref="ConstantNumericAttribute"/>, <see cref="ReturnTypeAttribute"/>) only ever return
/// <see langword="true"/> for expressions whose <see cref="Expression.NodeType"/> equals
/// <see cref="ExpressionType"/> (or for any node when <see cref="ExpressionType"/> is the wildcard sentinel
/// <c>-1</c>). A custom subclass is free to override <see cref="Match"/> with a broader or otherwise
/// different node-type semantics than <see cref="ExpressionType"/> declares — nothing here prevents that.
/// <see cref="ExpressionTransformer"/> buckets a method-level rule by its declared <see cref="ExpressionType"/>
/// only when the attribute's runtime type is one of the four listed above; a rule whose attribute is any
/// other (custom/third-party) type is conservatively kept a dispatch candidate for every node type, since
/// its <see cref="Match"/> override might accept node types other than the declared one. This preserves the
/// pre-indexing behavior (evaluate every annotated rule's <see cref="Match"/> for every node) for custom
/// attribute types, at the cost of the per-node-type filtering the four shipped types benefit from.
/// </remarks>
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
    /// <remarks>
    /// This base implementation never matches a node type other than <see cref="ExpressionType"/> (or
    /// every type, for the wildcard sentinel <c>-1</c>) — see the class-level
    /// <see cref="ExpressionSignatureAttribute"/> remarks for how a subclass overriding this to widen
    /// that set is handled by <see cref="ExpressionTransformer"/>.
    /// </remarks>
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
    /// <remarks>
    /// This array reference is never reassigned after construction, but — like any get-only array
    /// property — its elements are not protected from external mutation. Nothing in this codebase
    /// mutates it after construction.
    /// </remarks>
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
    /// <remarks>
    /// Evaluates the declaring-type constraint(s) in <see cref="Types"/> BEFORE the method-name
    /// comparison, and stops at the first matching type — exactly mirroring the historical
    /// <c>Types.Any(ec.Method.DeclaringType.IsDefinedBy)</c> traversal this indexed loop replaces.
    /// This ordering is observable: a null element in <see cref="Types"/> still throws (via
    /// <see cref="TypeEx.IsDefinedBy"/>'s own null-guard) even when <see cref="FunctionName"/> would
    /// not have matched the call. <see cref="Types"/> itself is read on every call rather than
    /// snapshotted, so external mutation of its elements remains observable, matching the historical
    /// behavior of reading the live array through <c>Enumerable.Any</c>. A null <see cref="Types"/>
    /// array raises the same <see cref="ArgumentNullException"/> (<c>ParamName</c> <c>"source"</c>)
    /// that <c>Enumerable.Any</c>'s own null-source guard used to raise, rather than letting the loop
    /// fail with an unrelated <see cref="NullReferenceException"/> on <c>types.Length</c>.
    /// </remarks>
    public override bool Match(Expression e)
    {
        if (e is not MethodCallExpression ec) return false;

        Type[] types = Types;
        if (types is null) throw new ArgumentNullException("source");

        Type? declaringType = ec.Method.DeclaringType;
        for (int i = 0; i < types.Length; i++)
        {
            if (declaringType!.IsDefinedBy(types[i]))
            {
                return ec.Method.Name == FunctionName;
            }
        }

        return false;
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
    /// <remarks>
    /// <c>Convert.ToDouble(cc.Value)</c> is deliberately re-evaluated for each candidate in
    /// <see cref="Values"/> rather than hoisted out of the loop: <see cref="NumberUtils.IsNumeric(object)"/>
    /// accepts any runtime type implementing <c>INumber&lt;TSelf&gt;</c>, not just CLR primitives, so a
    /// third-party numeric type's conversion could in principle have observable per-call side effects.
    /// This mirrors the historical per-element <c>Convert.ToDouble</c> call inside
    /// <c>Values.Any(v => v == Convert.ToDouble(cc.Value))</c>.
    /// </remarks>
    public override bool Match(Expression e)
    {
        if (e is not ConstantExpression cc) return false;
        if (!NumberUtils.IsNumeric(cc.Value)) return false;

        // If no specific allowed values, any numeric constant is fine
        IReadOnlyList<double>? values = Values;
        if (values is null) return true;

        // Otherwise, ensure the constant's value is among the specified set
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == Convert.ToDouble(cc.Value))
            {
                return true;
            }
        }

        return false;
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
