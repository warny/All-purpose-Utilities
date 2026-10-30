using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Provides a custom equality comparer for <see cref="Expression"/> objects.
/// The comparison involves simplifying each expression via an <see cref="ExpressionSimplifier"/>
/// before checking for structural and semantic equivalence.
/// </summary>
/// <remarks>
/// This comparer is a correctness primitive: <see cref="ExpressionSimplifier"/> uses it to decide whether
/// algebraic rewrites (common-term cancellation, power combination, trigonometric identities, ...) are
/// legal. A false positive here can silently change the mathematical result of a simplified expression, so
/// equality and hashing are implemented structurally, with explicit, scope-aware parameter binding for
/// lambda alpha-equivalence and an exact (not floating-point-approximate) numeric model for cross-type
/// numeric constants. See <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>, stage S1, for the
/// audit this hardening addresses and for remaining known limitations.
///
/// Structural equality covers lambdas, constants, parameters, unary, binary, method-call and member nodes (S1), the
/// scalar families of ETC R1c (conditionals, invocations, type tests, defaults, creations, array creations) and the binding
/// and control-flow families of ETC R2 (blocks, try expressions with their handlers, switches with their cases, loops,
/// jumps, labels, runtime variables and debug information). ETC R2 generalizes parameter binding to binder frames (lambda,
/// block, catch) and adds a label-target bijection per lambda body. Two distinct instances of any other node kind (member
/// and list initializers, index, dynamic and extension nodes) are conservatively treated as unequal.
/// </remarks>
public class ExpressionComparer : IEqualityComparer<Expression>
{
    /// <summary>Tracks nested comparison simplifications while diagnostic capture is active on this thread.</summary>
    [ThreadStatic]
    private static int _comparisonSimplificationDepth;

    /// <summary>
    /// Tracks the approximate magnitude of logical nodes attempted by the current diagnostic comparison
    /// chain. Large values may lose low-order precision, which is acceptable for threshold comparisons.
    /// </summary>
    [ThreadStatic]
    private static float _cumulativeNodeBudget;

    /// <summary>Stores diagnostic events for the active capture on this thread, or <see langword="null"/> when inactive.</summary>
    [ThreadStatic]
    private static List<ComparisonDiagnosticEvent>? _diagnosticEvents;

    /// <summary>Stores the ambient outer comparison context for diagnostic attribution on this thread.</summary>
    [ThreadStatic]
    private static ComparisonOrigin _currentComparisonOrigin;

    /// <summary>
    /// Counts, on this thread, every call this comparer is about to make into a constant value's own
    /// <see cref="object.Equals(object?)"/> or <see cref="object.GetHashCode"/> when that value is not known safe
    /// (see <see cref="IsKnownSafeConstantValue(object)"/>). Incremented before the call, so a call that throws is
    /// counted too. Only ever compared for equality between two reads (roadmap S5 P15).
    /// </summary>
    [ThreadStatic]
    private static long _userCodeInvocationCount;

    /// <summary>
    /// Identifies the active outer simplifier comparison context. Nested direct comparer calls can inherit
    /// this value, so it does not necessarily identify their immediate call site.
    /// </summary>
    internal enum ComparisonOrigin
    {
        /// <summary>The comparison came from an unclassified direct comparer path.</summary>
        Other,
        /// <summary>The comparison came from additive or subtractive factor probing.</summary>
        FactorEqualityProbe,
        /// <summary>The comparison came from multiplication power combination.</summary>
        Multiplication,
    }

    /// <summary>Describes one diagnostic simplification performed for comparison.</summary>
    /// <remarks>
    /// Node counts and cumulative budget are floating-point complexity magnitudes. They are exact for small
    /// exactly representable values but may lose low-order precision for heavily shared DAGs; shared paths
    /// count repeatedly, and the values are intended for threshold comparisons rather than distinct-object
    /// counting.
    /// </remarks>
    internal readonly record struct ComparisonDiagnosticEvent(
        int ComparisonDepth,
        float NodeCount,
        float ArithmeticNodeCount,
        int ArithmeticDepth,
        ExpressionType RootNodeType,
        ComparisonOrigin Origin,
        float CumulativeNodeBudget);

    /// <summary>Gets or sets the comparison origin on the current thread.</summary>
    internal static ComparisonOrigin CurrentComparisonOrigin
    {
        get => _currentComparisonOrigin;
        set => _currentComparisonOrigin = value;
    }

    /// <summary>Gets the current comparison-simplification depth for focused diagnostics.</summary>
    internal static int ComparisonSimplificationDepth => _comparisonSimplificationDepth;

    /// <summary>Gets whether diagnostic event capture is active on the current thread.</summary>
    internal static bool IsDiagnosticCaptureActive => _diagnosticEvents is not null;

    /// <summary>
    /// Gets the number of calls this comparer has made, on the current thread, into the
    /// <see cref="object.Equals(object?)"/> or <see cref="object.GetHashCode"/> of a constant value that is not known
    /// safe. Two equal reads around an operation prove that the comparer ran no such user code during it (roadmap
    /// S5 P15, <c>ExpressionSimplifier</c>'s observed comparison memo).
    /// </summary>
    internal static long UserCodeInvocationCount => _userCodeInvocationCount;

    /// <summary>Starts event capture on the current thread.</summary>
    internal static void BeginDiagnosticCapture()
    {
        _diagnosticEvents = new List<ComparisonDiagnosticEvent>();
    }

    /// <summary>Stops event capture and returns the captured events.</summary>
    /// <returns>The events captured on the current thread.</returns>
    internal static IReadOnlyList<ComparisonDiagnosticEvent> EndDiagnosticCapture()
    {
        List<ComparisonDiagnosticEvent> events = _diagnosticEvents ?? new List<ComparisonDiagnosticEvent>();
        _diagnosticEvents = null;
        return events;
    }

    /// <summary>
    /// A shared <see cref="ExpressionSimplifier"/> instance used to simplify expressions
    /// before they are compared.
    /// </summary>
    /// <remarks>
    /// Engine-internal (ETC R1a-Security): it always joins the comparer's running operation, so the comparer's limits
    /// (never this instance's defaults) bound the simplifications it runs.
    /// </remarks>
    private static readonly ExpressionSimplifier _expressionSimplifier = new ExpressionSimplifier { JoinsRunningOperation = true };

    /// <summary>
    /// Prevents direct instantiation outside of this class. Use <see cref="Default"/> or <see cref="Create"/> instead.
    /// </summary>
    /// <param name="resourceLimits">The resource limits of this instance.</param>
    private ExpressionComparer(ExpressionResourceLimits resourceLimits)
    {
        ResourceLimits = resourceLimits;
    }

    /// <summary>
    /// Gets the default <see cref="ExpressionComparer"/> instance for global usage, with
    /// <see cref="ExpressionResourceLimits.Default"/> resource limits.
    /// </summary>
    public static ExpressionComparer Default { get; } = new ExpressionComparer(ExpressionResourceLimits.Default);

    /// <summary>
    /// Creates a comparer with explicit resource limits (ETC R1a-Security). Equality and hashing behave exactly like
    /// <see cref="Default"/>; only the limits of the operations started by this instance differ.
    /// </summary>
    /// <param name="resourceLimits">The resource limits.</param>
    /// <returns>A new comparer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resourceLimits"/> is <see langword="null"/>.</exception>
    public static ExpressionComparer Create(ExpressionResourceLimits resourceLimits)
    {
        ArgumentNullException.ThrowIfNull(resourceLimits);
        return new ExpressionComparer(resourceLimits);
    }

    /// <summary>
    /// Gets the resource limits of this instance. <see cref="Equals(Expression?, Expression?)"/> and
    /// <see cref="GetHashCode(Expression)"/> validate their inputs against them before simplifying, and the
    /// simplifications and structural walks they run (including the internal simplifier) are bounded by them. A call
    /// made while another expression operation runs on the thread can only tighten the active limits.
    /// </summary>
    public ExpressionResourceLimits ResourceLimits { get; }

    /// <summary>
    /// Determines whether two <see cref="Expression"/> objects are equal by simplifying
    /// and comparing them structurally.
    /// </summary>
    /// <param name="x">The first expression to compare.</param>
    /// <param name="y">The second expression to compare.</param>
    /// <returns>
    /// <see langword="true"/> if both expressions are considered equivalent after simplification;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <see cref="ReferenceEquals(object?, object?)"/> is checked first, before either side is simplified.
    /// This both implements conventional null semantics (<c>null</c>/<c>null</c> is equal, exactly one
    /// <c>null</c> is not) and guarantees reflexivity for expression kinds the structural comparer does not
    /// understand: <see cref="ExpressionSimplifier"/> may rebuild an unsupported node into a new,
    /// no-longer-reference-equal instance, and the comparer itself does not claim structural equality for
    /// such kinds, so without this early check the very same source object could compare unequal to itself.
    /// Neither operand is ever passed to <see cref="ExpressionSimplifier.Simplify"/> while <see langword="null"/>.
    /// This top-level check is intentionally the <em>only</em> place a bare <see cref="ReferenceEquals(object?, object?)"/>
    /// shortcut is used: at this point <paramref name="x"/> and <paramref name="y"/> are being compared with
    /// themselves under a single, unambiguous binding context, so "same object" trivially means "same
    /// meaning". <see cref="EqualsCore"/>'s internal recursion deliberately has no equivalent shortcut,
    /// because a shared sub-expression object reached through two lambdas can carry two different meanings
    /// depending on how each lambda binds its parameters - see <see cref="EqualsCore"/>'s remarks.
    /// <para>
    /// <b>Resource limits (ETC R1a-Security).</b> When the fast path does not decide, both operands are validated
    /// against <see cref="ResourceLimits"/> before either is simplified, and the comparison is bounded by the visit
    /// budgets; an excess throws <see cref="ExpressionResourceLimitException"/>, never <see langword="false"/>. The fast
    /// path itself is an intentional compatibility exception: <c>Equals(x, x)</c> (same instance) and the
    /// <see langword="null"/> cases return without validating anything, so a <see langword="true"/> result there is not
    /// evidence that <paramref name="x"/> satisfies the limits. <see cref="GetHashCode(Expression)"/> always validates.
    /// </para>
    /// </remarks>
    /// <exception cref="ExpressionResourceLimitException">A resource limit was exceeded, or the stack ran out.</exception>
    public bool Equals(Expression? x, Expression? y)
    {
        if (TryFastPathEquals(x, y, out bool fastResult)) return fastResult;

        return ExpressionResourceBudget.Run(ResourceLimits, this, x, y, (X: x!, Y: y!), static state => EqualsAfterFastPath(state.X, state.Y));
    }

    /// <summary>
    /// The part of <see cref="Equals(Expression?, Expression?)"/> that follows the fast path: simplifies both operands
    /// for comparison and compares them structurally.
    /// </summary>
    /// <param name="x">The first operand, not <see langword="null"/>.</param>
    /// <param name="y">The second operand, not <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both operands are equivalent after simplification.</returns>
    private static bool EqualsAfterFastPath(Expression x, Expression y)
    {
        Expression simplifiedX = SimplifyForComparison(x);
        Expression simplifiedY = SimplifyForComparison(y);

        return StructuralEqualsAfterSimplification(simplifiedX, simplifiedY);
    }

    /// <summary>
    /// The comparison the simplifier's own rules use: exactly <see cref="Default"/>'s
    /// <see cref="Equals(Expression?, Expression?)"/>, but joining the running expression operation instead of starting
    /// a public one, so an engine-internal comparison neither validates subtrees again nor applies
    /// <see cref="Default"/>'s limits over the limits the caller configured. Outside any operation it is the public call.
    /// </summary>
    /// <param name="x">The first operand.</param>
    /// <param name="y">The second operand.</param>
    /// <returns><see langword="true"/> when both operands are equivalent after simplification.</returns>
    internal static bool EqualsWithinOperation(Expression? x, Expression? y)
    {
        if (!ExpressionResourceBudget.IsActive) return Default.Equals(x, y);
        if (TryFastPathEquals(x, y, out bool fastResult)) return fastResult;

        return EqualsAfterFastPath(x!, y!);
    }

    /// <summary>
    /// Performs the top-level, pre-simplification checks <see cref="Equals(Expression?, Expression?)"/>
    /// itself applies before ever calling <see cref="ExpressionSimplifier.Simplify(Expression)"/>: the
    /// <see cref="ReferenceEquals(object?, object?)"/>/null shortcut and the root-<see cref="LambdaExpression"/>
    /// metadata check.
    /// </summary>
    /// <param name="x">The first expression to compare, as originally supplied (not yet simplified).</param>
    /// <param name="y">The second expression to compare, as originally supplied (not yet simplified).</param>
    /// <param name="result">
    /// The final equality result, meaningful only when this method returns <see langword="true"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when one of the fast-path checks already decides the comparison (so
    /// <paramref name="result"/> is the final answer and no simplification is needed); <see langword="false"/>
    /// when a full simplify-then-compare is required.
    /// </returns>
    /// <remarks>
    /// Factored out of <see cref="Equals(Expression?, Expression?)"/>, with no behavior change, so the S5
    /// per-rule <c>ExpressionSimplifier.FactorEqualityProbe</c> batching helper (see
    /// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>, stage S5) can apply the exact same
    /// top-level semantics to a candidate pair before consulting its own simplified-form cache, instead of
    /// duplicating this logic. The three checks, and their order, are unchanged from before this refactor:
    /// see <see cref="Equals(Expression?, Expression?)"/>'s own remarks (still accurate, just distributed
    /// across this method and the two others introduced alongside it) for why each one exists.
    /// </remarks>
    internal static bool TryFastPathEquals(Expression? x, Expression? y, out bool result)
    {
        if (ReferenceEquals(x, y)) { result = true; return true; }
        if (x is null || y is null) { result = false; return true; }

        if (x is LambdaExpression xRoot && y is LambdaExpression yRoot
            && (xRoot.TailCall != yRoot.TailCall || xRoot.Type != yRoot.Type))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    /// <summary>
    /// Simplifies <paramref name="expression"/> using the exact same shared, exact-built-in
    /// <see cref="ExpressionSimplifier"/> instance <see cref="Equals(Expression?, Expression?)"/> and
    /// <see cref="GetHashCode(Expression)"/> use, so a caller that pre-computes a simplified form (such as
    /// the S5 <c>FactorEqualityProbe</c> batching helper) observes exactly the same result - including the
    /// same independent, self-contained top-level lexical-scope boundary <see cref="ExpressionSimplifier.Simplify(Expression)"/>
    /// establishes for every call - as calling the public <see cref="Equals(Expression?, Expression?)"/> would.
    /// </summary>
    /// <param name="expression">The expression to simplify; never <see langword="null"/>.</param>
    /// <returns>The simplified expression.</returns>
    /// <remarks>
    /// Nested calls join the current comparison session of
    /// <see cref="ExpressionSimplifier.SimplifyForComparisonInSession"/>, which may return a previously completed
    /// result for a strictly identical, user-code-free operand (roadmap S5 P9). The returned shape is the same as
    /// a fresh simplification; only its object identity may be shared.
    /// </remarks>
    internal static Expression SimplifyForComparison(Expression expression)
    {
        if (!IsDiagnosticCaptureActive)
        {
            return ExpressionSimplifier.SimplifyForComparisonInSession(_expressionSimplifier, expression);
        }

        using ExpressionSimplifier.ExpressionMetricsScope metricsScope = ExpressionSimplifier.BeginExpressionMetricsScope();
        _comparisonSimplificationDepth++;
        if (_comparisonSimplificationDepth == 1) _cumulativeNodeBudget = 0f;
        try
        {
            ExpressionSimplifier.ExpressionMetrics metrics = ExpressionSimplifier.GetExpressionMetrics(expression);
            _cumulativeNodeBudget += metrics.NodeCount;
            _diagnosticEvents?.Add(new ComparisonDiagnosticEvent(
                _comparisonSimplificationDepth,
                metrics.NodeCount,
                metrics.ArithmeticNodeCount,
                metrics.ArithmeticDepth,
                expression.NodeType,
                _currentComparisonOrigin,
                _cumulativeNodeBudget));
            return ExpressionSimplifier.SimplifyForComparisonInSession(_expressionSimplifier, expression);
        }
        finally
        {
            _comparisonSimplificationDepth--;
            if (_comparisonSimplificationDepth == 0) _cumulativeNodeBudget = 0f;
        }
    }

    /// <summary>
    /// Structurally compares two ALREADY-simplified expressions using the exact same policy the public
    /// <see cref="Equals(Expression?, Expression?)"/> uses for its own post-simplification comparison: the
    /// non-safe constant policy (a non-numeric, non-known-safe constant's own <see cref="object.Equals(object?)"/>
    /// may run), not <see cref="StructuralEqualsRaw(Expression?, Expression?)"/>'s narrower safe-constant
    /// policy. This is the method <see cref="Equals(Expression?, Expression?)"/> itself calls after
    /// simplifying both operands; it is exposed here so a caller that has already produced (and, e.g.,
    /// cached) each operand's simplified form - see the S5 <c>FactorEqualityProbe</c> helper - does not need
    /// to duplicate this exact policy or re-derive it from <see cref="StructuralEqualsRaw(Expression?, Expression?)"/>,
    /// which would be a silent, unsafe policy change (see that method's remarks and the S5 roadmap entry's
    /// "Critical warning").
    /// </summary>
    /// <param name="simplifiedX">The first expression, already simplified via <see cref="SimplifyForComparison(Expression)"/> (or equivalent).</param>
    /// <param name="simplifiedY">The second expression, already simplified via <see cref="SimplifyForComparison(Expression)"/> (or equivalent).</param>
    /// <returns><see langword="true"/> if the two already-simplified expressions are structurally equivalent under the public, non-safe-constant policy.</returns>
    internal static bool StructuralEqualsAfterSimplification(Expression? simplifiedX, Expression? simplifiedY)
        => ExpressionResourceBudget.RunInternal(
            (X: simplifiedX, Y: simplifiedY),
            static state => EqualsCore(state.X, state.Y, new ParameterBindingContext()));

    /// <summary>
    /// Recursively compares two already-simplified expressions for structural equivalence, resolving
    /// <see cref="ParameterExpression"/> references through <paramref name="context"/> rather than by name.
    /// </summary>
    /// <param name="x">The first (already simplified) expression, or a null sub-expression such as a static call's receiver.</param>
    /// <param name="y">The second (already simplified) expression, or a null sub-expression.</param>
    /// <param name="context">The per-comparison-call parameter binding stack. Never shared across calls to the public <see cref="Equals(Expression?, Expression?)"/>.</param>
    /// <returns><see langword="true"/> if the two sub-expressions are structurally equivalent.</returns>
    /// <remarks>
    /// <see cref="ParameterExpression"/> is resolved through <paramref name="context"/>, never by a generic
    /// <see cref="ReferenceEquals(object?, object?)"/> shortcut: the very same <see cref="ParameterExpression"/>
    /// instance can be declared at different positions in two lambdas being compared (for example
    /// <c>(p, q) => p</c> versus <c>(q, p) => p</c>), and only its binding position, not object identity,
    /// determines which argument it means on each side. This is also why <em>no other</em> node kind here
    /// gets a bare <c>ReferenceEquals(x, y)</c> shortcut either: a larger shared sub-expression object (for
    /// example a <see cref="MemberExpression"/> that both lambdas use as their body) can still contain a
    /// <see cref="ParameterExpression"/> whose meaning differs between the two binding contexts, so treating
    /// the shared object as trivially equal to itself would skip exactly the check that matters. Recursing
    /// structurally even into identical shared objects is the only safe option; the one place object
    /// identity is a valid shortcut is the outermost, single-binding-context comparison in the public
    /// <see cref="Equals(Expression?, Expression?)"/>.
    /// </remarks>
    private static bool EqualsCore(Expression? x, Expression? y, ParameterBindingContext context)
    {
        // Single dispatcher of every recursive comparison: one structural visit per compared pair, stack check
        // included (ETC R1a-Security). A family added to this switch is protected without further change.
        ExpressionResourceBudget.ConsumeStructuralVisit();
        if (x is null || y is null) return ReferenceEquals(x, y);
        if (x.NodeType != y.NodeType) return false;

        if (x is ParameterExpression xp && y is ParameterExpression yp)
        {
            return context.AreEquivalent(xp, yp);
        }

        if (x is ConstantExpression xc && y is ConstantExpression yc)
        {
            return ConstantsEqual(xc, yc, context.SafeConstantsOnly);
        }

        if (x.Type != y.Type) return false;

        return (x, y) switch
        {
            (LambdaExpression xl, LambdaExpression yl) => LambdasEqual(xl, yl, context),
            (UnaryExpression xu, UnaryExpression yu) => UnaryEqual(xu, yu, context),
            (BinaryExpression xb, BinaryExpression yb) => BinaryEqual(xb, yb, context),
            (MethodCallExpression xm, MethodCallExpression ym) => MethodCallsEqual(xm, ym, context),
            (MemberExpression xme, MemberExpression yme) => MemberEqual(xme, yme, context),

            // ETC R1c (C-04): scalar families without binder. Node type and result type are already equal here.
            (ConditionalExpression xco, ConditionalExpression yco) => ConditionalsEqual(xco, yco, context),
            (InvocationExpression xi, InvocationExpression yi) => InvocationsEqual(xi, yi, context),
            (TypeBinaryExpression xt, TypeBinaryExpression yt) => xt.TypeOperand == yt.TypeOperand && EqualsCore(xt.Expression, yt.Expression, context),
            (DefaultExpression, DefaultExpression) => true,
            (NewExpression xn, NewExpression yn) => CreationsEqual(xn, yn, context),
            (NewArrayExpression xa, NewArrayExpression ya) => SequencesEqual(xa.Expressions, ya.Expressions, context),

            // ETC R2: binding and control-flow families, under the lexical scope model (binder frames for block and catch
            // variables, a label bijection per lambda body).
            (BlockExpression xbl, BlockExpression ybl) => BlocksEqual(xbl, ybl, context),
            (TryExpression xtr, TryExpression ytr) => TriesEqual(xtr, ytr, context),
            (SwitchExpression xsw, SwitchExpression ysw) => SwitchesEqual(xsw, ysw, context),
            (LoopExpression xlo, LoopExpression ylo) => context.LabelsCorrespond(xlo.BreakLabel, ylo.BreakLabel)
                && context.LabelsCorrespond(xlo.ContinueLabel, ylo.ContinueLabel)
                && EqualsCore(xlo.Body, ylo.Body, context),
            (GotoExpression xg, GotoExpression yg) => xg.Kind == yg.Kind && context.LabelsCorrespond(xg.Target, yg.Target) && EqualsCore(xg.Value, yg.Value, context),
            (LabelExpression xla, LabelExpression yla) => context.LabelsCorrespond(xla.Target, yla.Target) && EqualsCore(xla.DefaultValue, yla.DefaultValue, context),
            (RuntimeVariablesExpression xr, RuntimeVariablesExpression yr) => RuntimeVariablesEqual(xr, yr, context),

            // Debug information is non-semantic metadata, like a parameter or lambda name: any two DebugInfo nodes are equal.
            (DebugInfoExpression, DebugInfoExpression) => true,

            // ETC R3: initializations and index accesses. Bindings and initializers are compared in declaration order (never
            // sorted), with their members, binding kinds and Add methods; nothing is executed.
            (MemberInitExpression xmi, MemberInitExpression ymi) => EqualsCore(xmi.NewExpression, ymi.NewExpression, context) && BindingsEqual(xmi.Bindings, ymi.Bindings, context),
            (ListInitExpression xli, ListInitExpression yli) => EqualsCore(xli.NewExpression, yli.NewExpression, context) && ElementInitsEqual(xli.Initializers, yli.Initializers, context),
            (IndexExpression xix, IndexExpression yix) => object.Equals(xix.Indexer, yix.Indexer) && EqualsCore(xix.Object, yix.Object, context) && SequencesEqual(xix.Arguments, yix.Arguments, context),

            // Unsupported node kind: conservatively unequal, even for a shared/reused instance - see this
            // method's remarks on why no ReferenceEquals shortcut is used here.
            _ => false,
        };
    }

    /// <summary>
    /// Compares two <see cref="BlockExpression"/> nodes (ETC R2): compatible variable declarations (count, types,
    /// <see cref="ParameterExpression.IsByRef"/>), then the statements in order under a pushed block frame. The result types
    /// are already equal.
    /// </summary>
    /// <param name="x">The first block.</param>
    /// <param name="y">The second block.</param>
    /// <param name="context">The active binding context.</param>
    /// <returns><see langword="true"/> if the blocks are alpha-equivalent.</returns>
    private static bool BlocksEqual(BlockExpression x, BlockExpression y, ParameterBindingContext context)
    {
        if (x.Expressions.Count != y.Expressions.Count || !DeclarationsCompatible(x.Variables, y.Variables)) return false;

        context.PushScope(BinderKind.Block, x.Variables, y.Variables);
        try
        {
            return SequencesEqual(x.Expressions, y.Expressions, context);
        }
        finally
        {
            context.PopScope();
        }
    }

    /// <summary>
    /// Compares two <see cref="TryExpression"/> nodes (ETC R2): the protected body, then the handlers in order (test type,
    /// variable, then filter and body under the catch frame, which binds neither siblings nor the body), then the finally and
    /// the fault. A finally never matches a fault.
    /// </summary>
    /// <param name="x">The first try expression.</param>
    /// <param name="y">The second try expression.</param>
    /// <param name="context">The active binding context.</param>
    /// <returns><see langword="true"/> if the try expressions are alpha-equivalent.</returns>
    private static bool TriesEqual(TryExpression x, TryExpression y, ParameterBindingContext context)
    {
        if (x.Handlers.Count != y.Handlers.Count || !EqualsCore(x.Body, y.Body, context)) return false;

        for (int i = 0; i < x.Handlers.Count; i++)
        {
            if (!HandlersEqual(x.Handlers[i], y.Handlers[i], context)) return false;
        }

        return EqualsCore(x.Finally, y.Finally, context) && EqualsCore(x.Fault, y.Fault, context);
    }

    /// <summary>
    /// Compares two <see cref="CatchBlock"/>s of <see cref="TriesEqual"/>: same test type, both without variable or with
    /// compatible variables, then the filter and the body under a catch frame binding the two variables. A handler is not an
    /// expression node, so its comparison is counted as one structural visit explicitly.
    /// </summary>
    /// <param name="x">The first handler.</param>
    /// <param name="y">The second handler.</param>
    /// <param name="context">The active binding context.</param>
    /// <returns><see langword="true"/> if the handlers are alpha-equivalent.</returns>
    private static bool HandlersEqual(CatchBlock x, CatchBlock y, ParameterBindingContext context)
    {
        ExpressionResourceBudget.ConsumeStructuralVisit();
        if (x.Test != y.Test) return false;
        if (x.Variable is null || y.Variable is null)
        {
            return x.Variable is null && y.Variable is null && EqualsCore(x.Filter, y.Filter, context) && EqualsCore(x.Body, y.Body, context);
        }

        if (x.Variable.Type != y.Variable.Type || x.Variable.IsByRef != y.Variable.IsByRef) return false;

        context.PushScope(BinderKind.Catch, x.Variable, y.Variable);
        try
        {
            return EqualsCore(x.Filter, y.Filter, context) && EqualsCore(x.Body, y.Body, context);
        }
        finally
        {
            context.PopScope();
        }
    }

    /// <summary>
    /// Compares two <see cref="SwitchExpression"/> nodes (ETC R2): the comparison method (possibly <see langword="null"/>),
    /// the switch value, the cases in order (test values in order, then body; a case is counted as one structural visit),
    /// then the default body. Cases are never reordered and the comparison method is never run.
    /// </summary>
    /// <param name="x">The first switch.</param>
    /// <param name="y">The second switch.</param>
    /// <param name="context">The active binding context.</param>
    /// <returns><see langword="true"/> if the switches are structurally equivalent.</returns>
    private static bool SwitchesEqual(SwitchExpression x, SwitchExpression y, ParameterBindingContext context)
    {
        if (!object.Equals(x.Comparison, y.Comparison) || x.Cases.Count != y.Cases.Count || !EqualsCore(x.SwitchValue, y.SwitchValue, context))
        {
            return false;
        }

        for (int i = 0; i < x.Cases.Count; i++)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            SwitchCase xCase = x.Cases[i];
            SwitchCase yCase = y.Cases[i];
            if (!SequencesEqual(xCase.TestValues, yCase.TestValues, context) || !EqualsCore(xCase.Body, yCase.Body, context)) return false;
        }

        return EqualsCore(x.DefaultBody, y.DefaultBody, context);
    }

    /// <summary>
    /// Compares two binding lists of a member initialization or of a <see cref="MemberMemberBinding"/> (ETC R3), position by
    /// position: same count, then for each pair the same binding kind and member, and equivalent contents (the assigned value,
    /// the nested bindings, or the initializers). A binding is not an expression node, so each pair is counted as one
    /// structural visit explicitly. A binding of an unknown class is never equal.
    /// </summary>
    /// <param name="x">The first bindings.</param>
    /// <param name="y">The second bindings.</param>
    /// <param name="context">The active binding context.</param>
    /// <returns><see langword="true"/> if the binding lists are structurally equivalent.</returns>
    private static bool BindingsEqual(IReadOnlyList<MemberBinding> x, IReadOnlyList<MemberBinding> y, ParameterBindingContext context)
    {
        if (x.Count != y.Count) return false;
        for (int i = 0; i < x.Count; i++)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            MemberBinding xb = x[i];
            MemberBinding yb = y[i];
            if (xb.BindingType != yb.BindingType || !object.Equals(xb.Member, yb.Member)) return false;

            bool equal = (xb, yb) switch
            {
                (MemberAssignment xa, MemberAssignment ya) => EqualsCore(xa.Expression, ya.Expression, context),
                (MemberMemberBinding xm, MemberMemberBinding ym) => BindingsEqual(xm.Bindings, ym.Bindings, context),
                (MemberListBinding xl, MemberListBinding yl) => ElementInitsEqual(xl.Initializers, yl.Initializers, context),
                _ => false,
            };
            if (!equal) return false;
        }

        return true;
    }

    /// <summary>
    /// Compares two initializer lists (ETC R3), position by position: same count, then for each pair the same
    /// <see cref="ElementInit.AddMethod"/> and equivalent arguments in order. Each pair is counted as one structural visit.
    /// </summary>
    /// <param name="x">The first initializers.</param>
    /// <param name="y">The second initializers.</param>
    /// <param name="context">The active binding context.</param>
    /// <returns><see langword="true"/> if the initializer lists are structurally equivalent.</returns>
    private static bool ElementInitsEqual(IReadOnlyList<ElementInit> x, IReadOnlyList<ElementInit> y, ParameterBindingContext context)
    {
        if (x.Count != y.Count) return false;
        for (int i = 0; i < x.Count; i++)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (!object.Equals(x[i].AddMethod, y[i].AddMethod) || !SequencesEqual(x[i].Arguments, y[i].Arguments, context)) return false;
        }

        return true;
    }

    /// <summary>
    /// Compares two <see cref="RuntimeVariablesExpression"/> nodes (ETC R2): the variables are references, resolved in order
    /// through the active binding context (bound by position, free by identity); they declare nothing.
    /// </summary>
    /// <param name="x">The first node.</param>
    /// <param name="y">The second node.</param>
    /// <param name="context">The active binding context.</param>
    /// <returns><see langword="true"/> if both nodes reference corresponding variables in the same order.</returns>
    private static bool RuntimeVariablesEqual(RuntimeVariablesExpression x, RuntimeVariablesExpression y, ParameterBindingContext context)
        => SequencesEqual(x.Variables, y.Variables, context);

    /// <summary>Compares two <see cref="ConditionalExpression"/> nodes: test, then the two branches, in order (never swapped).</summary>
    /// <param name="x">The first conditional.</param>
    /// <param name="y">The second conditional.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the conditionals are structurally equivalent.</returns>
    private static bool ConditionalsEqual(ConditionalExpression x, ConditionalExpression y, ParameterBindingContext context)
        => EqualsCore(x.Test, y.Test, context) && EqualsCore(x.IfTrue, y.IfTrue, context) && EqualsCore(x.IfFalse, y.IfFalse, context);

    /// <summary>Compares two <see cref="InvocationExpression"/> nodes: the invoked expression, then the arguments in order. Nothing is invoked.</summary>
    /// <param name="x">The first invocation.</param>
    /// <param name="y">The second invocation.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the invocations are structurally equivalent.</returns>
    private static bool InvocationsEqual(InvocationExpression x, InvocationExpression y, ParameterBindingContext context)
        => x.Arguments.Count == y.Arguments.Count && EqualsCore(x.Expression, y.Expression, context) && SequencesEqual(x.Arguments, y.Arguments, context);

    /// <summary>
    /// Compares two <see cref="NewExpression"/> nodes: the constructor (both <see langword="null"/> for a value-type default
    /// creation), the arguments in order, then the <see cref="NewExpression.Members"/> (both absent, or the same members in the
    /// same order). The constructor is never run.
    /// </summary>
    /// <param name="x">The first creation.</param>
    /// <param name="y">The second creation.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the creations are structurally equivalent.</returns>
    private static bool CreationsEqual(NewExpression x, NewExpression y, ParameterBindingContext context)
    {
        if (!object.Equals(x.Constructor, y.Constructor) || !MembersEqual(x.Members, y.Members)) return false;
        return SequencesEqual(x.Arguments, y.Arguments, context);
    }

    /// <summary>Compares two optional member lists of <see cref="NewExpression.Members"/>: both absent, or the same members in the same order.</summary>
    /// <param name="x">The first list, or <see langword="null"/>.</param>
    /// <param name="y">The second list, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the lists are equal.</returns>
    private static bool MembersEqual(IReadOnlyList<MemberInfo>? x, IReadOnlyList<MemberInfo>? y)
    {
        if (x is null || y is null) return x is null && y is null;
        if (x.Count != y.Count) return false;
        for (int i = 0; i < x.Count; i++)
        {
            if (!object.Equals(x[i], y[i])) return false;
        }

        return true;
    }

    /// <summary>Compares two child lists element by element, in order, through <see cref="EqualsCore"/>.</summary>
    /// <param name="x">The first list.</param>
    /// <param name="y">The second list.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if both lists have the same length and pairwise equivalent elements.</returns>
    private static bool SequencesEqual(IReadOnlyList<Expression> x, IReadOnlyList<Expression> y, ParameterBindingContext context)
    {
        if (x.Count != y.Count) return false;
        for (int i = 0; i < x.Count; i++)
        {
            if (!EqualsCore(x[i], y[i], context)) return false;
        }

        return true;
    }

    /// <summary>Compares two <see cref="UnaryExpression"/> nodes: operator method, lifting flags, then the operand.</summary>
    /// <param name="x">The first unary expression.</param>
    /// <param name="y">The second unary expression.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the unary expressions are structurally equivalent.</returns>
    private static bool UnaryEqual(UnaryExpression x, UnaryExpression y, ParameterBindingContext context)
        => object.Equals(x.Method, y.Method)
            && x.IsLifted == y.IsLifted
            && x.IsLiftedToNull == y.IsLiftedToNull
            && EqualsCore(x.Operand, y.Operand, context);

    /// <summary>
    /// Compares two <see cref="BinaryExpression"/> nodes: operator method, lifting flags, then operands and
    /// the coalesce conversion lambda.
    /// </summary>
    /// <param name="x">The first binary expression.</param>
    /// <param name="y">The second binary expression.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the binary expressions are structurally equivalent.</returns>
    /// <remarks>
    /// For an ordinary (non-lifted, predefined-operator) <see cref="ExpressionType.Add"/> or
    /// <see cref="ExpressionType.Multiply"/> node — see <see cref="ExpressionSimplifier.IsOrdinaryBinaryArithmetic(BinaryExpression)"/> —
    /// operands are also compared SWAPPED as a fallback when the positional comparison fails, since those
    /// operators are mathematically commutative. This matters specifically for two distinct FREE
    /// <see cref="ParameterExpression"/> operands (an expression not wrapped in an enclosing
    /// <see cref="LambdaExpression"/>): <see cref="ExpressionSimplifier"/>'s stage-S4 canonicalization
    /// deliberately does not invent a cross-tree order between two such parameters (see the roadmap's "Free
    /// parameters" policy), so <c>Simplify(Add(a, b))</c> and <c>Simplify(Add(b, a))</c> can legitimately
    /// keep their different source operand order — without this commutative fallback, this comparer would
    /// then report <c>Equals(Add(a, b), Add(b, a))</c> as <see langword="false"/> for such free parameters,
    /// a regression from this comparer's pre-S4 observable behavior (a real bug this narrowly-scoped
    /// correction fixes, per the S4 roadmap entry's own carve-out for one). Every already-supported case
    /// (bound parameters, which S4 keeps canonicalizing deterministically by position; non-commutative
    /// operators; custom/lifted operators, excluded by <c>IsOrdinaryBinaryArithmetic</c>) is unaffected: the
    /// positional comparison already succeeds for those, so the swapped fallback is never reached.
    /// <para>
    /// <b>Public compatibility decision (S4 review, round 4).</b> This IS a deliberate, accepted widening of
    /// this type's public <see cref="Equals(Expression?, Expression?)"/>/<see cref="GetHashCode(Expression)"/>
    /// contract, not only an internal S4 correctness fix: two distinct FREE parameters sharing the same
    /// <see cref="ParameterExpression.Name"/> (e.g. both literally named <c>"v"</c>) previously compared
    /// unequal under <c>Add</c>/swapped-<c>Add</c> (the pre-S4 textual key tied on identical name text, the
    /// then-positional-only comparison found the operands reference-unequal); this fallback now matches
    /// them, and any external caller using <see cref="Default"/> as a <see cref="Dictionary{TKey, TValue}"/>/
    /// <see cref="HashSet{T}"/> key comparer for <see cref="Expression"/> trees containing such same-named
    /// distinct free parameters observes that change. The project's batched-2.0.0 versioning plan (see
    /// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>) treats this kind of accumulating,
    /// individually-reasoned public-behavior change as expected between major releases rather than a
    /// per-change compatibility blocker; reverting to the old, narrower behavior is not adopted here because
    /// it was itself an accidental side effect of the pre-S4 textual-key implementation (ordinary commutative
    /// addition failing to compare two of its own re-orderings equal, purely because of how two UNRELATED
    /// operands happened to be spelled) rather than a deliberately designed contract, and reintroducing it
    /// would reintroduce that same non-commutativity inconsistency rather than removing one.
    /// </para>
    /// <para>
    /// <b>Explicit scope boundary (S4 review, round 5): NOT a general n-ary restoration.</b> This fallback
    /// swaps only the two operands of a SINGLE <see cref="BinaryExpression"/> node; it is not a general
    /// associative-commutative MULTISET match over an entire chain of ordinary <c>Add</c>/<c>Multiply</c>
    /// nodes. Before S4, three (or more) free parameters chained through ordinary addition/multiplication
    /// converged to one canonical order via the old <see cref="ParameterExpression.Name"/>-based textual
    /// sort regardless of source association - e.g. <c>Simplify((a+b)+c)</c> and <c>Simplify((c+b)+a)</c>
    /// both produced the same tree, so this comparer trivially agreed on them too. Post-S4, each side keeps
    /// its own source association/order instead (see the "Free parameters" policy referenced above), and
    /// this two-operand fallback does not bridge a 3+-term permutation: at the root, comparing
    /// <c>(a+b)+c</c> against <c>(c+b)+a</c> either positionally or swapped requires an <c>Add</c> subtree
    /// to match a bare parameter, which fails structurally either way, so
    /// <c>Equals((a+b)+c, (c+b)+a)</c> is <see langword="false"/> for three distinct free parameters even
    /// though they are algebraically the same sum. Recognizing this would require comparing/hashing such
    /// chains as associative-commutative collections of terms - a materially larger capability than this
    /// narrow regression fix, deliberately NOT attempted here; see the roadmap's round 5 decision.
    /// Characterized (not merely left unspecified) by
    /// <c>ExpressionComparerTests.FreeParameters_ThreeTermAdditionPermutation_CharacterizesPostS4ScopeBoundary</c>
    /// and its multiplicative counterpart.
    /// </para>
    /// </remarks>
    private static bool BinaryEqual(BinaryExpression x, BinaryExpression y, ParameterBindingContext context)
    {
        if (!object.Equals(x.Method, y.Method) || x.IsLifted != y.IsLifted || x.IsLiftedToNull != y.IsLiftedToNull)
        {
            return false;
        }

        // A failed positional attempt must not leave label correspondences behind for the swapped attempt (ETC R2).
        int labelMark = context.LabelMark;
        if (EqualsCore(x.Left, y.Left, context) && EqualsCore(x.Right, y.Right, context) && EqualsCore(x.Conversion, y.Conversion, context))
        {
            return true;
        }

        context.RollbackLabels(labelMark);
        return IsCommutative(x)
            && IsCommutative(y)
            && EqualsCore(x.Left, y.Right, context)
            && EqualsCore(x.Right, y.Left, context)
            && EqualsCore(x.Conversion, y.Conversion, context);
    }

    /// <summary>Whether <paramref name="binary"/> is an ordinary <see cref="ExpressionType.Add"/> or <see cref="ExpressionType.Multiply"/> node, and therefore mathematically commutative.</summary>
    /// <param name="binary">The binary expression to classify.</param>
    /// <returns><see langword="true"/> if <paramref name="binary"/>'s operands may be compared in either order.</returns>
    private static bool IsCommutative(BinaryExpression binary)
        => (binary.NodeType == ExpressionType.Add || binary.NodeType == ExpressionType.Multiply)
            && ExpressionSimplifier.IsOrdinaryBinaryArithmetic(binary);

    /// <summary>Compares two <see cref="MemberExpression"/> nodes: member identity, then the receiver.</summary>
    /// <param name="x">The first member access.</param>
    /// <param name="y">The second member access.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the member accesses are structurally equivalent.</returns>
    private static bool MemberEqual(MemberExpression x, MemberExpression y, ParameterBindingContext context)
        => object.Equals(x.Member, y.Member) && EqualsCore(x.Expression, y.Expression, context);

    /// <summary>Compares two <see cref="LambdaExpression"/> nodes: metadata, parameter types (positionally), then the body under a pushed binding scope.</summary>
    /// <param name="x">The first lambda.</param>
    /// <param name="y">The second lambda.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the lambdas are alpha-equivalent.</returns>
    /// <remarks>
    /// <see cref="LambdaExpression.Type"/> (the delegate type) is already required equal by the caller's
    /// blanket <c>x.Type != y.Type</c> check. <see cref="LambdaExpression.Name"/> is debug metadata and
    /// deliberately excluded from equality, as documented in the S1 roadmap entry for this PR. Since the S1
    /// reconstruction-fidelity fix, the exact built-in <see cref="ExpressionSimplifier"/> - the only runtime
    /// type <see cref="_expressionSimplifier"/> ever is - preserves <see cref="LambdaExpression.TailCall"/>
    /// and <see cref="LambdaExpression.Type"/> for every lambda it rebuilds, at every nesting depth, not
    /// only at the root: both checks here are therefore live, useful comparisons for a NESTED lambda too, not
    /// merely defense in depth. Since ETC R1b (C-01) every transformer, a derived simplifier included, keeps both
    /// at every depth; before, a derived simplifier erased them below the root.
    /// </remarks>
    private static bool LambdasEqual(LambdaExpression x, LambdaExpression y, ParameterBindingContext context)
    {
        if (x.TailCall != y.TailCall) return false;

        var xParameters = x.Parameters;
        var yParameters = y.Parameters;
        if (!DeclarationsCompatible(xParameters, yParameters)) return false;

        // ETC R2: a lambda body has its own label scope, so label correspondences never leak between lambda bodies.
        context.PushScope(BinderKind.Lambda, xParameters, yParameters);
        (int Scope, int Mark) labels = context.EnterLabelScope();
        try
        {
            return EqualsCore(x.Body, y.Body, context);
        }
        finally
        {
            context.ExitLabelScope(labels);
            context.PopScope();
        }
    }

    /// <summary>Compares two <see cref="MethodCallExpression"/> nodes: method identity, argument count, receiver, then arguments in order.</summary>
    /// <param name="x">The first method call.</param>
    /// <param name="y">The second method call.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the calls are structurally equivalent.</returns>
    private static bool MethodCallsEqual(MethodCallExpression x, MethodCallExpression y, ParameterBindingContext context)
    {
        if (!object.Equals(x.Method, y.Method)) return false;
        if (x.Arguments.Count != y.Arguments.Count) return false;
        if (!EqualsCore(x.Object, y.Object, context)) return false;

        for (int i = 0; i < x.Arguments.Count; i++)
        {
            if (!EqualsCore(x.Arguments[i], y.Arguments[i], context)) return false;
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="value"/>'s runtime type is one whose <see cref="object.Equals(object?)"/> and
    /// <see cref="object.GetHashCode"/> — and, for <see cref="bool"/>/<see cref="char"/>/<see cref="Enum"/>
    /// specifically, whose <see cref="IComparable"/> — implementations are fixed, non-user-overridable BCL
    /// behavior, never culture-dependent. Shared between this comparer's <c>safeConstantsOnly</c> policy
    /// (<see cref="ConstantsEqual"/>/<see cref="HashConstant"/>) and
    /// <c>ExpressionCanonicalOrder.ConstantKey</c>'s structural ORDER comparison, so both agree on exactly
    /// the same "known safe to compare directly" set instead of drifting independently. <see cref="string"/>
    /// is included here (its default <see cref="object.Equals(object?)"/>/<see cref="object.GetHashCode"/>
    /// are ordinal, not culture-aware — only its default <see cref="IComparable{T}"/> is culture-aware, which
    /// is why the order side uses <see cref="string.CompareOrdinal(string?, string?)"/> explicitly rather
    /// than calling this method for ordering). Every other reference/value type (including a struct that
    /// implements <see cref="IComparable"/>) is treated as opaque: this method deliberately does not attempt
    /// to enumerate every "probably safe" framework type, only the small set the S4 roadmap entry names.
    /// </summary>
    /// <param name="value">The boxed constant value to classify; never <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/>'s type is in the known-safe set.</returns>
    internal static bool IsKnownSafeConstantValue(object value) => value is string or bool or char or Enum;

    /// <summary>
    /// Compares two <see cref="ConstantExpression"/> nodes, using an exact cross-type numeric model when
    /// both are native numeric constants, and null-safe/type-safe value comparison otherwise.
    /// </summary>
    /// <param name="x">The first constant.</param>
    /// <param name="y">The second constant.</param>
    /// <param name="safeConstantsOnly">
    /// When <see langword="true"/>, a non-numeric constant whose value is NOT a known-safe type (see
    /// <see cref="IsKnownSafeConstantValue(object)"/>) is compared by reference identity of its boxed
    /// <see cref="ConstantExpression.Value"/> instead of by calling the value's own possibly user-defined
    /// <see cref="object.Equals(object?)"/> override — see <see cref="ParameterBindingContext.SafeConstantsOnly"/>.
    /// A known-safe value (<see cref="string"/>, <see cref="bool"/>, <see cref="char"/>, <see cref="Enum"/>)
    /// still compares by value even when <paramref name="safeConstantsOnly"/> is <see langword="true"/>,
    /// since its <see cref="object.Equals(object?)"/> is known-safe to call.
    /// </param>
    /// <returns><see langword="true"/> if the constants represent the same value.</returns>
    private static bool ConstantsEqual(ConstantExpression x, ConstantExpression y, bool safeConstantsOnly)
    {
        if (TryGetExactNumericValue(x, out ExactNumericValue xn) && TryGetExactNumericValue(y, out ExactNumericValue yn))
        {
            return xn.Equals(yn);
        }

        if (x.Type != y.Type) return false;
        if (x.Value is null || y.Value is null) return x.Value is null && y.Value is null;

        bool knownSafe = IsKnownSafeConstantValue(x.Value);
        if (safeConstantsOnly && !knownSafe)
        {
            return ReferenceEquals(x.Value, y.Value);
        }

        if (!knownSafe) _userCodeInvocationCount++;
        return x.Value.Equals(y.Value);
    }

    /// <summary>Attempts to build an exact rational/NaN/infinity key for a constant whose declared <see cref="Expression.Type"/> is one of the native numeric types in <see cref="Types.Number"/>.</summary>
    /// <param name="constant">The constant to inspect.</param>
    /// <param name="value">The resulting exact numeric value, when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="constant"/>'s declared type is a native numeric type.</returns>
    private static bool TryGetExactNumericValue(ConstantExpression constant, out ExactNumericValue value)
    {
        if (!Types.Number.Contains(constant.Type))
        {
            value = default;
            return false;
        }

        value = ExactNumericValue.FromBoxed(constant.Type, constant.Value!);
        return true;
    }

    /// <summary>
    /// Structurally compares two expressions exactly like <see cref="Equals(Expression?, Expression?)"/>,
    /// EXCEPT that neither operand is ever passed through <see cref="ExpressionSimplifier.Simplify(Expression)"/>
    /// first, and no top-level <see cref="ReferenceEquals(object?, object?)"/> shortcut is applied.
    /// </summary>
    /// <remarks>
    /// This is the "raw, already-simplified expression" helper the S4 structural canonicalization key
    /// (<c>ExpressionSimplifier</c>'s additive grouping) shares with this comparer, per the roadmap's
    /// explicit instruction not to call the public, simplifying <see cref="Equals(Expression?, Expression?)"/>
    /// from inside canonical-key generation: <see cref="ExpressionSimplifier"/> already calls this method
    /// from deep inside its own <c>Simplify</c> recursion (building an additive-grouping key while
    /// finalizing an <c>Add</c>/<c>Subtract</c> node), and re-entering <see cref="ExpressionSimplifier.Simplify(Expression)"/>
    /// there would recursively re-simplify an expression that is already mid-simplification. Both operands
    /// passed here are therefore expected to already be simplified (or otherwise already in the exact
    /// structural shape the caller wants compared) — this method performs no simplification of its own.
    /// A free <see cref="ParameterExpression"/> (not bound by a <see cref="LambdaExpression"/> encountered
    /// during this call's own recursive descent) is compared by reference, exactly like
    /// <see cref="Equals(Expression?, Expression?)"/>'s own free-parameter policy: <paramref name="x"/> and
    /// <paramref name="y"/> are expected to be sibling sub-expressions of the same original tree, so a
    /// parameter free relative to both sides that is actually bound by a still-enclosing lambda resolves
    /// correctly via that shared reference, without this method needing to know about that enclosing scope.
    /// <para>
    /// A non-numeric <see cref="ConstantExpression"/> whose value is NOT a known-safe type (see
    /// <see cref="IsKnownSafeConstantValue(object)"/>: <see cref="string"/>, <see cref="bool"/>,
    /// <see cref="char"/>, <see cref="Enum"/>) is compared by REFERENCE identity of its boxed
    /// <see cref="ConstantExpression.Value"/>, never by calling the value's own possibly user-defined
    /// <see cref="object.Equals(object?)"/> — see <see cref="ParameterBindingContext.SafeConstantsOnly"/>. A
    /// known-safe value still compares by value, since calling its <see cref="object.Equals(object?)"/> is
    /// known not to execute user code. This is a deliberate, narrower policy than the public
    /// <see cref="Equals(Expression?, Expression?)"/> uses only for the opaque case: this raw entry point's
    /// only caller, <see cref="ExpressionSimplifier"/>'s additive-grouping equality, runs automatically as
    /// part of ordinary <c>Simplify()</c> calls, so it must never execute arbitrary user code (which an
    /// opaque constant's own <see cref="object.Equals(object?)"/>/<see cref="object.GetHashCode"/> override
    /// could contain, with side effects or a thrown exception) merely to decide whether two constant terms
    /// belong in the same additive group.
    /// </para>
    /// </remarks>
    /// <param name="x">The first (already simplified, or otherwise final) expression to compare.</param>
    /// <param name="y">The second (already simplified, or otherwise final) expression to compare.</param>
    /// <returns><see langword="true"/> if the two sub-expressions are structurally equivalent.</returns>
    internal static bool StructuralEqualsRaw(Expression? x, Expression? y)
        => ExpressionResourceBudget.RunInternal(
            (X: x, Y: y),
            static state => EqualsCore(state.X, state.Y, new ParameterBindingContext(safeConstantsOnly: true)));

    /// <summary>
    /// Structurally hashes an expression exactly like <see cref="GetHashCode(Expression)"/>, EXCEPT that
    /// <paramref name="e"/> is never passed through <see cref="ExpressionSimplifier.Simplify(Expression)"/>
    /// first. See <see cref="StructuralEqualsRaw(Expression?, Expression?)"/>'s remarks for why this raw
    /// entry point exists and is consistent with it (a hash mirroring an equality contract must agree with
    /// that exact equality, not with the simplifying public <see cref="Equals(Expression?, Expression?)"/>).
    /// </summary>
    /// <param name="e">The (already simplified, or otherwise final) expression to hash, or <see langword="null"/>.</param>
    /// <returns>A hash code consistent with <see cref="StructuralEqualsRaw(Expression?, Expression?)"/>.</returns>
    internal static int StructuralHashRaw(Expression? e)
        => ExpressionResourceBudget.RunInternal(e, static expression => Hash(expression, new ParameterScopeStack(safeConstantsOnly: true)));

    /// <summary>
    /// Returns a hash code for the specified <see cref="Expression"/> by simplifying it and computing a
    /// structural hash that mirrors <see cref="Equals(Expression?, Expression?)"/>: alpha-equivalent
    /// lambdas and exact cross-type numeric constants hash identically, satisfying the
    /// <see cref="IEqualityComparer{T}"/> contract that equal objects have equal hash codes.
    /// </summary>
    /// <param name="obj">The <see cref="Expression"/> for which to get a hash code.</param>
    /// <returns>An integer hash code consistent with <see cref="Equals(Expression?, Expression?)"/>.</returns>
    /// <remarks>
    /// ETC R1a-Security: <paramref name="obj"/> is always validated against <see cref="ResourceLimits"/> (there is no
    /// fast path), and the simplification and hashing are bounded by the visit budgets; an excess throws
    /// <see cref="ExpressionResourceLimitException"/>, never a fallback hash.
    /// </remarks>
    /// <exception cref="ExpressionResourceLimitException">A resource limit was exceeded, or the stack ran out.</exception>
    public int GetHashCode(Expression obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        return ExpressionResourceBudget.Run(ResourceLimits, this, obj, null, obj, static expression =>
        {
            Expression simplified = _expressionSimplifier.TransformDetached(expression);
            return Hash(simplified, new ParameterScopeStack());
        });
    }

    /// <summary>Computes a structural hash for <paramref name="e"/>, mirroring the metadata inspected by <see cref="EqualsCore"/>.</summary>
    /// <param name="e">The (already simplified) expression to hash, or <see langword="null"/> for an absent optional sub-expression.</param>
    /// <param name="scopes">The active lambda parameter scope stack for the single tree being hashed.</param>
    /// <returns>A hash code such that structurally/alpha-equivalent expressions produce the same value.</returns>
    private static int Hash(Expression? e, ParameterScopeStack scopes)
    {
        // Single dispatcher of every recursive hash: one structural visit per node, stack check included
        // (ETC R1a-Security). A family added to this switch is protected without further change.
        ExpressionResourceBudget.ConsumeStructuralVisit();
        return e switch
        {
            null => 0,
            LambdaExpression le => HashLambda(le, scopes),
            ParameterExpression pe => HashParameter(pe, scopes),
            ConstantExpression ce => HashConstant(ce, scopes.SafeConstantsOnly),
            UnaryExpression ue => HashUnary(ue, scopes),
            BinaryExpression be => HashBinary(be, scopes),
            MethodCallExpression mce => HashMethodCall(mce, scopes),
            MemberExpression me => HashMember(me, scopes),
            ConditionalExpression ce => HashConditional(ce, scopes),
            InvocationExpression ie => HashInvocation(ie, scopes),
            TypeBinaryExpression tbe => HashCode.Combine(tbe.NodeType, tbe.Type, tbe.TypeOperand, Hash(tbe.Expression, scopes)),
            DefaultExpression de => HashCode.Combine(ExpressionType.Default, de.Type),
            NewExpression ne => HashNew(ne, scopes),
            NewArrayExpression nae => HashSequence(HashCode.Combine(nae.NodeType, nae.Type), nae.Expressions, scopes),

            // ETC R2: binding and control-flow families, hashed in the same structural order as EqualsCore compares them.
            BlockExpression block => HashBlock(block, scopes),
            TryExpression @try => HashTry(@try, scopes),
            SwitchExpression @switch => HashSwitch(@switch, scopes),
            LoopExpression loop => HashCode.Combine(ExpressionType.Loop, loop.Type, HashLabel(loop.BreakLabel, scopes), HashLabel(loop.ContinueLabel, scopes), Hash(loop.Body, scopes)),
            GotoExpression @goto => HashCode.Combine(ExpressionType.Goto, @goto.Type, @goto.Kind, HashLabel(@goto.Target, scopes), Hash(@goto.Value, scopes)),
            LabelExpression label => HashCode.Combine(ExpressionType.Label, label.Type, HashLabel(label.Target, scopes), Hash(label.DefaultValue, scopes)),
            RuntimeVariablesExpression runtimeVariables => HashSequence(HashCode.Combine(ExpressionType.RuntimeVariables, runtimeVariables.Type), runtimeVariables.Variables, scopes),
            DebugInfoExpression => HashCode.Combine(ExpressionType.DebugInfo, typeof(void)),

            // ETC R3: initializations and index accesses, hashed in the same structural order as EqualsCore compares them.
            MemberInitExpression memberInit => HashCode.Combine(ExpressionType.MemberInit, memberInit.Type, Hash(memberInit.NewExpression, scopes), HashBindings(memberInit.Bindings, scopes)),
            ListInitExpression listInit => HashCode.Combine(ExpressionType.ListInit, listInit.Type, Hash(listInit.NewExpression, scopes), HashElementInits(listInit.Initializers, scopes)),
            IndexExpression index => HashSequence(HashCode.Combine(ExpressionType.Index, index.Type, index.Indexer, Hash(index.Object, scopes)), index.Arguments, scopes),
            _ => HashUnsupported(e),
        };
    }

    /// <summary>
    /// Hashes the bindings of a member initialization or of a <see cref="MemberMemberBinding"/> (ETC R3): the count, then for
    /// each binding in order its kind, its member and the hash of its contents, each binding counted as one structural visit.
    /// </summary>
    /// <param name="bindings">The bindings.</param>
    /// <param name="scopes">The active scope stack.</param>
    /// <returns>A hash code mirroring <see cref="BindingsEqual"/>.</returns>
    private static int HashBindings(IReadOnlyList<MemberBinding> bindings, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(bindings.Count);
        foreach (MemberBinding binding in bindings)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            hc.Add(binding.BindingType);
            hc.Add(binding.Member);
            hc.Add(binding switch
            {
                MemberAssignment assignment => Hash(assignment.Expression, scopes),
                MemberMemberBinding memberBinding => HashBindings(memberBinding.Bindings, scopes),
                MemberListBinding listBinding => HashElementInits(listBinding.Initializers, scopes),
                _ => 0,
            });
        }

        return hc.ToHashCode();
    }

    /// <summary>
    /// Hashes initializers (ETC R3): the count, then for each initializer in order its <see cref="ElementInit.AddMethod"/> and
    /// its arguments, each initializer counted as one structural visit.
    /// </summary>
    /// <param name="initializers">The initializers.</param>
    /// <param name="scopes">The active scope stack.</param>
    /// <returns>A hash code mirroring <see cref="ElementInitsEqual"/>.</returns>
    private static int HashElementInits(IReadOnlyList<ElementInit> initializers, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(initializers.Count);
        foreach (ElementInit initializer in initializers)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            hc.Add(initializer.AddMethod);
            hc.Add(HashSequence(0, initializer.Arguments, scopes));
        }

        return hc.ToHashCode();
    }

    /// <summary>Hashes a label target (ETC R2) by its first-occurrence index in the current label scope and its type; never by name.</summary>
    /// <param name="target">The target, or <see langword="null"/> for an absent loop label.</param>
    /// <param name="scopes">The active scope stack, which owns the label indices.</param>
    /// <returns>A hash code mirroring <see cref="ParameterBindingContext.LabelsCorrespond"/>.</returns>
    private static int HashLabel(LabelTarget? target, ParameterScopeStack scopes)
        => target is null ? -1 : HashCode.Combine(scopes.LabelIndex(target), target.Type);

    /// <summary>Hashes a <see cref="BlockExpression"/> (ETC R2): result type, declaration types and flags, then the statements under a pushed block frame.</summary>
    /// <param name="block">The block to hash.</param>
    /// <param name="scopes">The active scope stack.</param>
    /// <returns>A hash code mirroring <see cref="BlocksEqual"/>.</returns>
    private static int HashBlock(BlockExpression block, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Block);
        hc.Add(block.Type);
        AddDeclarations(ref hc, block.Variables);
        scopes.Push(BinderKind.Block, block.Variables);
        try
        {
            hc.Add(HashSequence(block.Expressions.Count, block.Expressions, scopes));
        }
        finally
        {
            scopes.Pop();
        }

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="TryExpression"/> (ETC R2): result type, body, each handler (test type, variable, filter and body under a catch frame), finally, fault.</summary>
    /// <param name="node">The try expression to hash.</param>
    /// <param name="scopes">The active scope stack.</param>
    /// <returns>A hash code mirroring <see cref="TriesEqual"/>.</returns>
    private static int HashTry(TryExpression node, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Try);
        hc.Add(node.Type);
        hc.Add(Hash(node.Body, scopes));
        hc.Add(node.Handlers.Count);
        foreach (CatchBlock handler in node.Handlers)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            hc.Add(handler.Test);
            if (handler.Variable is null)
            {
                hc.Add(false);
                hc.Add(Hash(handler.Filter, scopes));
                hc.Add(Hash(handler.Body, scopes));
                continue;
            }

            hc.Add(true);
            hc.Add(handler.Variable.Type);
            hc.Add(handler.Variable.IsByRef);
            scopes.Push(BinderKind.Catch, [handler.Variable]);
            try
            {
                hc.Add(Hash(handler.Filter, scopes));
                hc.Add(Hash(handler.Body, scopes));
            }
            finally
            {
                scopes.Pop();
            }
        }

        hc.Add(Hash(node.Finally, scopes));
        hc.Add(Hash(node.Fault, scopes));
        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="SwitchExpression"/> (ETC R2): result type, comparison method, switch value, cases in order (test values, body), default body.</summary>
    /// <param name="node">The switch to hash.</param>
    /// <param name="scopes">The active scope stack.</param>
    /// <returns>A hash code mirroring <see cref="SwitchesEqual"/>.</returns>
    private static int HashSwitch(SwitchExpression node, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Switch);
        hc.Add(node.Type);
        hc.Add(node.Comparison);
        hc.Add(Hash(node.SwitchValue, scopes));
        hc.Add(node.Cases.Count);
        foreach (SwitchCase @case in node.Cases)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            hc.Add(HashSequence(0, @case.TestValues, scopes));
            hc.Add(Hash(@case.Body, scopes));
        }

        hc.Add(Hash(node.DefaultBody, scopes));
        return hc.ToHashCode();
    }

    /// <summary>Adds the declaration metadata a binder comparison checks (count, then each type and <see cref="ParameterExpression.IsByRef"/>); names never count.</summary>
    /// <param name="hc">The hash being built.</param>
    /// <param name="declarations">The declarations.</param>
    private static void AddDeclarations(ref HashCode hc, IReadOnlyList<ParameterExpression> declarations)
    {
        hc.Add(declarations.Count);
        foreach (ParameterExpression declaration in declarations)
        {
            hc.Add(declaration.Type);
            hc.Add(declaration.IsByRef);
        }
    }

    /// <summary>Hashes a <see cref="ConditionalExpression"/>: result type, then test and branches in order.</summary>
    /// <param name="ce">The conditional to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring <see cref="ConditionalsEqual"/>.</returns>
    private static int HashConditional(ConditionalExpression ce, ParameterScopeStack scopes)
        => HashCode.Combine(ExpressionType.Conditional, ce.Type, Hash(ce.Test, scopes), Hash(ce.IfTrue, scopes), Hash(ce.IfFalse, scopes));

    /// <summary>Hashes an <see cref="InvocationExpression"/>: result type, the invoked expression, then the arguments in order.</summary>
    /// <param name="ie">The invocation to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring <see cref="InvocationsEqual"/>.</returns>
    private static int HashInvocation(InvocationExpression ie, ParameterScopeStack scopes)
        => HashSequence(HashCode.Combine(ExpressionType.Invoke, ie.Type, Hash(ie.Expression, scopes)), ie.Arguments, scopes);

    /// <summary>Hashes a <see cref="NewExpression"/>: result type, constructor, members in order, then the arguments in order.</summary>
    /// <param name="ne">The creation to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring <see cref="CreationsEqual"/>.</returns>
    private static int HashNew(NewExpression ne, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.New);
        hc.Add(ne.Type);
        hc.Add(ne.Constructor);
        hc.Add(ne.Members?.Count ?? -1);
        if (ne.Members is not null)
        {
            foreach (MemberInfo member in ne.Members) hc.Add(member);
        }

        return HashSequence(hc.ToHashCode(), ne.Arguments, scopes);
    }

    /// <summary>Combines <paramref name="seed"/> with the count and the hashes of <paramref name="children"/>, in order.</summary>
    /// <param name="seed">The hash of the node's own metadata.</param>
    /// <param name="children">The children, hashed through <see cref="Hash"/>.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>The combined hash code.</returns>
    private static int HashSequence(int seed, IReadOnlyList<Expression> children, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(seed);
        hc.Add(children.Count);
        foreach (Expression child in children) hc.Add(Hash(child, scopes));
        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="LambdaExpression"/>: metadata, parameter types, then the body under a pushed scope.</summary>
    /// <param name="le">The lambda to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="LambdasEqual"/> compares.</returns>
    private static int HashLambda(LambdaExpression le, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Lambda);
        hc.Add(le.Type);
        hc.Add(le.TailCall);
        AddDeclarations(ref hc, le.Parameters);

        // ETC R2: the body has its own label scope, as in LambdasEqual.
        scopes.Push(BinderKind.Lambda, le.Parameters);
        var labels = scopes.EnterLabelScope();
        try
        {
            hc.Add(Hash(le.Body, scopes));
        }
        finally
        {
            scopes.ExitLabelScope(labels);
            scopes.Pop();
        }

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="ParameterExpression"/> by its relative binding depth and declaration position when bound, or by reference identity when free.</summary>
    /// <param name="pe">The parameter to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code such that alpha-equivalent bound parameters, or the same free parameter, produce the same value.</returns>
    private static int HashParameter(ParameterExpression pe, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Parameter);
        if (scopes.TryLocate(pe, out int depth, out int position, out BinderKind kind))
        {
            hc.Add(depth);
            hc.Add(position);
            hc.Add(kind);
        }
        else
        {
            hc.Add(RuntimeHelpers.GetHashCode(pe));
        }
        hc.Add(pe.Type);

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="ConstantExpression"/>, using the exact numeric key (without the original CLR type) for native numeric constants, and <see cref="Expression.Type"/> plus a value-derived hash otherwise.</summary>
    /// <param name="ce">The constant to hash.</param>
    /// <param name="safeConstantsOnly">
    /// When <see langword="true"/>, a non-numeric constant whose value is NOT a known-safe type (see
    /// <see cref="IsKnownSafeConstantValue(object)"/>) is hashed by
    /// <see cref="RuntimeHelpers.GetHashCode(object?)"/> (reference identity) instead of the value's own
    /// possibly user-defined <see cref="object.GetHashCode"/> override, mirroring
    /// <see cref="ConstantsEqual"/>'s <c>safeConstantsOnly</c> policy so equal keys still hash equally. A
    /// known-safe value (<see cref="string"/>, <see cref="bool"/>, <see cref="char"/>, <see cref="Enum"/>)
    /// still hashes by value even when <paramref name="safeConstantsOnly"/> is <see langword="true"/>. This
    /// use of <see cref="RuntimeHelpers.GetHashCode(object?)"/> is only ever a <c>Dictionary</c>/<c>GroupBy</c>
    /// lookup accelerator (see <see cref="StructuralHashRaw(Expression?)"/>'s caller,
    /// <see cref="ExpressionSimplifier"/>'s additive grouping) — never the final canonical ORDER, which the
    /// roadmap requires to never be hash-based.
    /// </param>
    /// <returns>A hash code mirroring the value comparison <see cref="ConstantsEqual"/> performs.</returns>
    private static int HashConstant(ConstantExpression ce, bool safeConstantsOnly)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Constant);
        if (TryGetExactNumericValue(ce, out ExactNumericValue numeric))
        {
            hc.Add(true);
            hc.Add(numeric);
        }
        else
        {
            hc.Add(false);
            hc.Add(ce.Type);
            hc.Add(ce.Value switch
            {
                null => 0,
                var value when safeConstantsOnly && !IsKnownSafeConstantValue(value) => RuntimeHelpers.GetHashCode(value),
                var value => HashConstantValue(value),
            });
        }

        return hc.ToHashCode();
    }

    /// <summary>
    /// Calls a constant value's own <see cref="object.GetHashCode"/>, counting the call in
    /// <see cref="UserCodeInvocationCount"/> first when the value is not known safe.
    /// </summary>
    /// <param name="value">The non-null boxed constant value.</param>
    /// <returns>The value's hash code.</returns>
    private static int HashConstantValue(object value)
    {
        if (!IsKnownSafeConstantValue(value)) _userCodeInvocationCount++;
        return value.GetHashCode();
    }

    /// <summary>Hashes a <see cref="UnaryExpression"/>: node type, result type, operator method, lifting flags, then the operand.</summary>
    /// <param name="ue">The unary expression to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="EqualsCore"/>'s unary case compares.</returns>
    private static int HashUnary(UnaryExpression ue, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ue.NodeType);
        hc.Add(ue.Type);
        hc.Add(ue.Method);
        hc.Add(ue.IsLifted);
        hc.Add(ue.IsLiftedToNull);
        hc.Add(Hash(ue.Operand, scopes));

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="BinaryExpression"/>: node type, result type, operator method, lifting flags, operands, then the coalesce conversion lambda.</summary>
    /// <param name="be">The binary expression to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="EqualsCore"/>'s binary case (including its commutative fallback, see <see cref="IsCommutative(BinaryExpression)"/>) compares.</returns>
    private static int HashBinary(BinaryExpression be, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(be.NodeType);
        hc.Add(be.Type);
        hc.Add(be.Method);
        hc.Add(be.IsLifted);
        hc.Add(be.IsLiftedToNull);

        bool commutative = IsCommutative(be);
        var labelMark = scopes.LabelMark;
        int leftHash = Hash(be.Left, scopes);

        // Both operands of a commutative node number their labels from the same point (ETC R2), so the order-independent
        // combination below stays consistent with the swapped match of BinaryEqual; the indices are forgotten afterwards.
        if (commutative) scopes.RollbackLabels(labelMark);
        int rightHash = Hash(be.Right, scopes);
        if (commutative) scopes.RollbackLabels(labelMark);
        if (commutative)
        {
            // Order-independent combination: BinaryEqual accepts a swapped-operand match for an ordinary
            // Add/Multiply node, so two such nodes it considers equal (e.g. Add(a, b) and Add(b, a) for two
            // distinct free parameters) must also hash identically regardless of which operand is Left vs
            // Right, per the IEqualityComparer<T> "equal objects have equal hash codes" contract.
            hc.Add(unchecked(leftHash + rightHash));
        }
        else
        {
            hc.Add(leftHash);
            hc.Add(rightHash);
        }

        hc.Add(Hash(be.Conversion, scopes));

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="MethodCallExpression"/>: method identity, receiver, then arguments in order.</summary>
    /// <param name="mce">The method call to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="MethodCallsEqual"/> compares.</returns>
    private static int HashMethodCall(MethodCallExpression mce, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Call);
        hc.Add(mce.Type);
        hc.Add(mce.Method);
        hc.Add(Hash(mce.Object, scopes));
        hc.Add(mce.Arguments.Count);
        foreach (var a in mce.Arguments) hc.Add(Hash(a, scopes));

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="MemberExpression"/>: member identity, then the receiver.</summary>
    /// <param name="me">The member access to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="EqualsCore"/>'s member case compares.</returns>
    private static int HashMember(MemberExpression me, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.MemberAccess);
        hc.Add(me.Type);
        hc.Add(me.Member);
        hc.Add(Hash(me.Expression, scopes));

        return hc.ToHashCode();
    }

    /// <summary>Computes a coarse, conservative hash for an unsupported node kind: collisions between distinct unequal nodes are acceptable, but this must never disagree with <see cref="EqualsCore"/>'s "false" for that node kind.</summary>
    /// <param name="e">The unsupported expression to hash.</param>
    /// <returns>A hash code based only on <see cref="Expression.NodeType"/> and <see cref="Expression.Type"/>.</returns>
    private static int HashUnsupported(Expression e) => HashCode.Combine(e.NodeType, e.Type);

    /// <summary>
    /// A per-<see cref="Equals(Expression?, Expression?)"/>-call, mutable stack of paired
    /// (<c>left</c>, <c>right</c>) <see cref="ParameterExpression"/> bindings, used to resolve lambda
    /// alpha-equivalence by declaration position/instance rather than by <see cref="ParameterExpression.Name"/>.
    /// Never stored on <see cref="ExpressionComparer.Default"/>, so concurrent comparisons never interfere.
    /// </summary>
    private sealed class ParameterBindingContext
    {
        /// <summary>The binding pairs of the open binder frames, innermost last; created by the first binder (ETC R2: lazily, so a comparison without binder allocates no list).</summary>
        private List<(ParameterExpression Left, ParameterExpression Right)>? _bindings;

        /// <summary>The open binder frames (ETC R2), innermost last: their kind and the index of their first binding in <see cref="_bindings"/>; created by the first binder, so a comparison without binder allocates nothing more than before R2.</summary>
        private List<(BinderKind Kind, int Start)>? _frames;

        /// <summary>The label correspondences (ETC R2), created on the first label met, so a comparison without label allocates nothing for them.</summary>
        private LabelMaps? _labels;

        /// <summary>The identifier of the current label scope (0 outside any lambda; a fresh one per lambda body).</summary>
        private int _labelScope;

        /// <summary>The last label scope identifier handed out.</summary>
        private int _lastLabelScope;

        /// <summary>
        /// When <see langword="true"/>, a non-numeric <see cref="ConstantExpression"/> compared through
        /// this context is compared by reference identity of its boxed value instead of the value's own
        /// possibly user-defined <see cref="object.Equals(object?)"/> override. Used only by
        /// <see cref="StructuralEqualsRaw(Expression?, Expression?)"/>: the public, simplifying
        /// <see cref="Equals(Expression?, Expression?)"/> always uses <see langword="false"/>, preserving
        /// its historical behavior unchanged.
        /// </summary>
        public bool SafeConstantsOnly { get; }

        /// <summary>Initializes a new, empty <see cref="ParameterBindingContext"/>.</summary>
        /// <param name="safeConstantsOnly">The value of <see cref="SafeConstantsOnly"/>.</param>
        public ParameterBindingContext(bool safeConstantsOnly = false)
        {
            SafeConstantsOnly = safeConstantsOnly;
        }

        /// <summary>
        /// Opens a binder frame of <paramref name="kind"/>, pushing one binding pair per positionally-corresponding declaration
        /// (lambda parameters, block variables, a catch variable). Callers check first that both declaration lists have the
        /// same count, types and <see cref="ParameterExpression.IsByRef"/> flags (<see cref="DeclarationsCompatible"/>), and close
        /// the frame with <see cref="PopScope"/> in a <see langword="finally"/> block.
        /// </summary>
        /// <param name="kind">The binder kind.</param>
        /// <param name="leftParameters">The left binder's declarations, in declaration order.</param>
        /// <param name="rightParameters">The right binder's declarations, in declaration order; must have the same count as <paramref name="leftParameters"/>.</param>
        public void PushScope(BinderKind kind, IReadOnlyList<ParameterExpression> leftParameters, IReadOnlyList<ParameterExpression> rightParameters)
        {
            List<(ParameterExpression Left, ParameterExpression Right)> bindings = _bindings ??= [];
            (_frames ??= []).Add((kind, bindings.Count));
            for (int i = 0; i < leftParameters.Count; i++)
            {
                bindings.Add((leftParameters[i], rightParameters[i]));
            }
        }

        /// <summary>Opens a binder frame declaring a single pair (a catch variable).</summary>
        /// <param name="kind">The binder kind.</param>
        /// <param name="left">The left declaration.</param>
        /// <param name="right">The right declaration.</param>
        public void PushScope(BinderKind kind, ParameterExpression left, ParameterExpression right)
        {
            List<(ParameterExpression Left, ParameterExpression Right)> bindings = _bindings ??= [];
            (_frames ??= []).Add((kind, bindings.Count));
            bindings.Add((left, right));
        }

        /// <summary>Closes the innermost binder frame, removing exactly the bindings it pushed.</summary>
        public void PopScope()
        {
            List<(BinderKind Kind, int Start)> frames = _frames!;
            int start = frames[^1].Start;
            frames.RemoveAt(frames.Count - 1);
            _bindings!.RemoveRange(start, _bindings.Count - start);
        }

        /// <summary>
        /// Opens a fresh label scope for a lambda body (ETC R2): label correspondences established outside are not visible
        /// inside, and those established inside are dropped by <see cref="ExitLabelScope"/>, so they never leak between
        /// independent lambda bodies.
        /// </summary>
        /// <returns>The state to pass to <see cref="ExitLabelScope"/>.</returns>
        public (int Scope, int Mark) EnterLabelScope()
        {
            (int Scope, int Mark) saved = (_labelScope, LabelMark);
            _labelScope = ++_lastLabelScope;
            return saved;
        }

        /// <summary>Closes the label scope opened by <see cref="EnterLabelScope"/>, undoing every correspondence it added.</summary>
        /// <param name="saved">The value <see cref="EnterLabelScope"/> returned.</param>
        public void ExitLabelScope((int Scope, int Mark) saved)
        {
            RollbackLabels(saved.Mark);
            _labelScope = saved.Scope;
        }

        /// <summary>Gets a mark of the label correspondences recorded so far, for <see cref="RollbackLabels"/>.</summary>
        public int LabelMark => _labels?.Undo.Count ?? 0;

        /// <summary>
        /// Undoes every label correspondence recorded after <paramref name="mark"/>: used when a comparison attempt fails and
        /// another one is tried (the commutative fallback of <see cref="BinaryEqual"/>), so the failed attempt leaves nothing.
        /// </summary>
        /// <param name="mark">A value of <see cref="LabelMark"/>.</param>
        public void RollbackLabels(int mark)
        {
            if (_labels is not { } labels) return;

            List<(bool Left, LabelTarget Key, (LabelTarget Other, int Scope)? Previous)> undo = labels.Undo;
            for (int i = undo.Count - 1; i >= mark; i--)
            {
                (bool left, LabelTarget key, (LabelTarget Other, int Scope)? previous) = undo[i];
                Dictionary<LabelTarget, (LabelTarget Other, int Scope)> map = left ? labels.Left : labels.Right;
                if (previous is { } entry) map[key] = entry;
                else map.Remove(key);
            }

            undo.RemoveRange(mark, undo.Count - mark);
        }

        /// <summary>
        /// Decides whether label target <paramref name="x"/> (left tree) corresponds to <paramref name="y"/> (right tree) in the
        /// current label scope (ETC R2): both absent; or same <see cref="LabelTarget.Type"/> and either both already mapped to
        /// each other, or neither mapped yet (the pair is then recorded, at its first occurrence, whether a definition or a
        /// jump). Names never count; the mapping stays a bijection.
        /// </summary>
        /// <param name="x">The left target, or <see langword="null"/>.</param>
        /// <param name="y">The right target, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the targets correspond.</returns>
        public bool LabelsCorrespond(LabelTarget? x, LabelTarget? y)
        {
            if (x is null || y is null) return x is null && y is null;
            if (x.Type != y.Type) return false;

            LabelMaps labels = _labels ??= new LabelMaps();
            bool leftMapped = TryGetLabel(labels.Left, x, out LabelTarget? leftOther);
            bool rightMapped = TryGetLabel(labels.Right, y, out LabelTarget? rightOther);
            if (leftMapped || rightMapped)
            {
                return leftMapped && rightMapped && ReferenceEquals(leftOther, y) && ReferenceEquals(rightOther, x);
            }

            RecordLabel(true, labels, labels.Left, x, y);
            RecordLabel(false, labels, labels.Right, y, x);
            return true;
        }

        /// <summary>Looks up a correspondence established in the current label scope.</summary>
        /// <param name="map">The side's map.</param>
        /// <param name="key">The target.</param>
        /// <param name="other">The corresponding target, when found.</param>
        /// <returns><see langword="true"/> if <paramref name="key"/> is mapped in the current label scope.</returns>
        private bool TryGetLabel(Dictionary<LabelTarget, (LabelTarget Other, int Scope)> map, LabelTarget key, out LabelTarget? other)
        {
            if (map.TryGetValue(key, out (LabelTarget Other, int Scope) entry) && entry.Scope == _labelScope)
            {
                other = entry.Other;
                return true;
            }

            other = null;
            return false;
        }

        /// <summary>Records one side of a correspondence, logging the entry it replaces so it can be undone.</summary>
        /// <param name="left">Whether <paramref name="map"/> is the left side's map.</param>
        /// <param name="labels">The label maps, whose undo log records the write.</param>
        /// <param name="map">The side's map.</param>
        /// <param name="key">The target.</param>
        /// <param name="other">The corresponding target.</param>
        private void RecordLabel(bool left, LabelMaps labels, Dictionary<LabelTarget, (LabelTarget Other, int Scope)> map, LabelTarget key, LabelTarget other)
        {
            (LabelTarget Other, int Scope)? previous = map.TryGetValue(key, out (LabelTarget Other, int Scope) existing) ? existing : null;
            labels.Undo.Add((left, key, previous));
            map[key] = (other, _labelScope);
        }

        /// <summary>
        /// The label correspondences of one comparison (ETC R2): each left target maps to the right target it corresponds to
        /// and the label scope that established the correspondence, mirrored on the right so the mapping stays a bijection, with
        /// an undo log (side, key, replaced entry) for scope exits and failed commutative attempts.
        /// </summary>
        private sealed class LabelMaps
        {
            /// <summary>The correspondences of the left tree's targets.</summary>
            public readonly Dictionary<LabelTarget, (LabelTarget Other, int Scope)> Left = new(ReferenceEqualityComparer.Instance);

            /// <summary>The correspondences of the right tree's targets.</summary>
            public readonly Dictionary<LabelTarget, (LabelTarget Other, int Scope)> Right = new(ReferenceEqualityComparer.Instance);

            /// <summary>The undo log of the writes to <see cref="Left"/> and <see cref="Right"/>.</summary>
            public readonly List<(bool Left, LabelTarget Key, (LabelTarget Other, int Scope)? Previous)> Undo = [];
        }

        /// <summary>
        /// Determines whether <paramref name="x"/> (from the left tree) and <paramref name="y"/> (from the
        /// right tree) refer to the same bound position, searching the most recently pushed bindings first
        /// so inner (shadowing) declarations win over outer ones.
        /// </summary>
        /// <param name="x">The left-hand parameter reference.</param>
        /// <param name="y">The right-hand parameter reference.</param>
        /// <returns>
        /// <see langword="true"/> if both are bound to exactly the same pair, or if neither is currently
        /// bound and they are the same free-parameter instance.
        /// </returns>
        public bool AreEquivalent(ParameterExpression x, ParameterExpression y)
        {
            List<(ParameterExpression Left, ParameterExpression Right)>? bindings = _bindings;
            if (bindings is null) return ReferenceEquals(x, y);

            int leftIndex = -1;
            int rightIndex = -1;
            for (int i = bindings.Count - 1; i >= 0; i--)
            {
                if (leftIndex < 0 && ReferenceEquals(bindings[i].Left, x)) leftIndex = i;
                if (rightIndex < 0 && ReferenceEquals(bindings[i].Right, y)) rightIndex = i;
                if (leftIndex >= 0 && rightIndex >= 0) break;
            }

            if (leftIndex < 0 && rightIndex < 0) return ReferenceEquals(x, y);
            return leftIndex == rightIndex;
        }
    }

    /// <summary>
    /// A per-<see cref="GetHashCode(Expression)"/>-call stack of lambda parameter lists, used to hash a
    /// <see cref="ParameterExpression"/> by its relative binding depth and declaration position (a
    /// De Bruijn-style index) rather than by name or object identity, so alpha-equivalent expressions hash
    /// identically regardless of which <see cref="ParameterExpression"/> instances or names they use.
    /// </summary>
    private sealed class ParameterScopeStack
    {
        /// <summary>The open binder scopes, innermost last; created by the first binder (ETC R2: lazily, so hashing a tree without binder allocates no list).</summary>
        private List<(BinderKind Kind, IReadOnlyList<ParameterExpression> Parameters)>? _scopes;

        /// <summary>The first-occurrence index of each label target seen in the current label scope (ETC R2), lazily created.</summary>
        private Dictionary<LabelTarget, int>? _labels;

        /// <summary>Undo log of <see cref="_labels"/> insertions, so a label scope or a commutative operand can be rolled back.</summary>
        private List<LabelTarget>? _labelUndo;

        /// <summary>The index the next new label target receives in the current label scope.</summary>
        private int _nextLabel;

        /// <summary>Mirrors <see cref="ParameterBindingContext.SafeConstantsOnly"/> for the hashing side; see its remarks.</summary>
        public bool SafeConstantsOnly { get; }

        /// <summary>Initializes a new, empty <see cref="ParameterScopeStack"/>.</summary>
        /// <param name="safeConstantsOnly">The value of <see cref="SafeConstantsOnly"/>.</param>
        public ParameterScopeStack(bool safeConstantsOnly = false)
        {
            SafeConstantsOnly = safeConstantsOnly;
        }

        /// <summary>Pushes a binder's declarations when descending into its scope.</summary>
        /// <param name="kind">The binder kind.</param>
        /// <param name="parameters">The binder's declarations, in declaration order.</param>
        public void Push(BinderKind kind, IReadOnlyList<ParameterExpression> parameters) => (_scopes ??= []).Add((kind, parameters));

        /// <summary>Pops the innermost binder's declarations when leaving its scope.</summary>
        public void Pop() => _scopes!.RemoveAt(_scopes.Count - 1);

        /// <summary>Locates <paramref name="parameter"/> among the currently active scopes, innermost first.</summary>
        /// <param name="parameter">The parameter reference to locate.</param>
        /// <param name="relativeDepth">The number of enclosing scopes between the innermost scope and the one declaring <paramref name="parameter"/> (0 = innermost), when found.</param>
        /// <param name="position">The parameter's declaration position within its scope, when found.</param>
        /// <param name="kind">The kind of the declaring binder, when found.</param>
        /// <returns><see langword="true"/> if <paramref name="parameter"/> is bound in an active scope; <see langword="false"/> if it is a free parameter.</returns>
        public bool TryLocate(ParameterExpression parameter, out int relativeDepth, out int position, out BinderKind kind)
        {
            List<(BinderKind Kind, IReadOnlyList<ParameterExpression> Parameters)>? scopes = _scopes;
            for (int i = (scopes?.Count ?? 0) - 1; i >= 0; i--)
            {
                var scope = scopes![i].Parameters;
                for (int j = 0; j < scope.Count; j++)
                {
                    if (ReferenceEquals(scope[j], parameter))
                    {
                        relativeDepth = scopes.Count - 1 - i;
                        position = j;
                        kind = scopes[i].Kind;
                        return true;
                    }
                }
            }

            relativeDepth = -1;
            position = -1;
            kind = default;
            return false;
        }

        /// <summary>
        /// Gets the first-occurrence index of <paramref name="target"/> in the current label scope, assigning the next one at
        /// its first occurrence (ETC R2). Names never count.
        /// </summary>
        /// <param name="target">The label target.</param>
        /// <returns>Its index.</returns>
        public int LabelIndex(LabelTarget target)
        {
            _labels ??= new Dictionary<LabelTarget, int>(ReferenceEqualityComparer.Instance);
            if (_labels.TryGetValue(target, out int index)) return index;

            (_labelUndo ??= []).Add(target);
            _labels.Add(target, _nextLabel);
            return _nextLabel++;
        }

        /// <summary>Gets a mark of the label indices assigned so far, for <see cref="RollbackLabels"/>.</summary>
        public (int Undo, int Next) LabelMark => (_labelUndo?.Count ?? 0, _nextLabel);

        /// <summary>Forgets every label index assigned after <paramref name="mark"/>.</summary>
        /// <param name="mark">A value of <see cref="LabelMark"/>.</param>
        public void RollbackLabels((int Undo, int Next) mark)
        {
            if (_labelUndo is { } undo)
            {
                for (int i = undo.Count - 1; i >= mark.Undo; i--) _labels!.Remove(undo[i]);
                undo.RemoveRange(mark.Undo, undo.Count - mark.Undo);
            }

            _nextLabel = mark.Next;
        }

        /// <summary>Opens a fresh label scope for a lambda body: outer indices are hidden and inner ones dropped on exit.</summary>
        /// <returns>The state to pass to <see cref="ExitLabelScope"/>.</returns>
        public (Dictionary<LabelTarget, int>? Labels, List<LabelTarget>? Undo, int Next) EnterLabelScope()
        {
            (Dictionary<LabelTarget, int>? Labels, List<LabelTarget>? Undo, int Next) saved = (_labels, _labelUndo, _nextLabel);
            _labels = null;
            _labelUndo = null;
            _nextLabel = 0;
            return saved;
        }

        /// <summary>Restores the label scope saved by <see cref="EnterLabelScope"/>.</summary>
        /// <param name="saved">The value <see cref="EnterLabelScope"/> returned.</param>
        public void ExitLabelScope((Dictionary<LabelTarget, int>? Labels, List<LabelTarget>? Undo, int Next) saved)
            => (_labels, _labelUndo, _nextLabel) = saved;
    }

    /// <summary>
    /// The kind of a binder frame of the lexical scope model (ETC R2): a lambda (including the conversion lambda of a binary
    /// node, which is an ordinary lambda in its own child slot), a block or a catch handler. Frames are opened in pairs on both
    /// sides of a comparison, so a binding position never matches across kinds; the kind also enters the hash of a bound
    /// parameter.
    /// </summary>
    private enum BinderKind
    {
        /// <summary>A lambda's parameters, in scope in its body.</summary>
        Lambda,

        /// <summary>A block's variables, in scope in all its statements.</summary>
        Block,

        /// <summary>A catch handler's variable, in scope in its filter and body only.</summary>
        Catch,
    }

    /// <summary>
    /// Whether two declaration lists can be bound positionally (ETC R2): same count, and pairwise the same
    /// <see cref="Expression.Type"/> and <see cref="ParameterExpression.IsByRef"/>. Names never count.
    /// </summary>
    /// <param name="x">The left declarations.</param>
    /// <param name="y">The right declarations.</param>
    /// <returns><see langword="true"/> if the declarations are compatible.</returns>
    private static bool DeclarationsCompatible(IReadOnlyList<ParameterExpression> x, IReadOnlyList<ParameterExpression> y)
    {
        if (x.Count != y.Count) return false;
        for (int i = 0; i < x.Count; i++)
        {
            if (x[i].Type != y[i].Type || x[i].IsByRef != y[i].IsByRef) return false;
        }

        return true;
    }

    /// <summary>
    /// An exact representation of a native numeric constant's mathematical value, used to compare and hash
    /// numeric constants across different CLR numeric types without floating-point or decimal rounding.
    /// Finite values are stored as a normalized (reduced, positive-denominator) exact rational; special
    /// IEEE values collapse to one of three categories. Constructed directly from each type's exact bit
    /// representation (integer value, <see cref="decimal.GetBits(decimal)"/>, or IEEE 754 sign/exponent/
    /// mantissa decomposition) - never via <see cref="object.ToString"/> or a lossy round trip through
    /// another numeric type.
    /// </summary>
    internal readonly struct ExactNumericValue : IEquatable<ExactNumericValue>, IComparable<ExactNumericValue>
    {
        /// <summary>The category of an <see cref="ExactNumericValue"/>: an exact finite rational, or one of the three non-finite IEEE categories.</summary>
        private enum NumericKind
        {
            /// <summary>A finite value, stored as an exact <c>numerator / denominator</c> rational.</summary>
            Finite,

            /// <summary>Any not-a-number value; all NaN bit patterns collapse to this single category.</summary>
            NaN,

            /// <summary>Positive infinity.</summary>
            PositiveInfinity,

            /// <summary>Negative infinity.</summary>
            NegativeInfinity,
        }

        private readonly NumericKind _kind;
        private readonly BigInteger _numerator;
        private readonly BigInteger _denominator;

        /// <summary>Initializes an already-normalized exact numeric value. Use <see cref="Normalize"/> rather than calling this directly for a <see cref="NumericKind.Finite"/> value.</summary>
        /// <param name="kind">The value's category.</param>
        /// <param name="numerator">The exact numerator; meaningful only when <paramref name="kind"/> is <see cref="NumericKind.Finite"/>.</param>
        /// <param name="denominator">The exact, positive denominator; meaningful only when <paramref name="kind"/> is <see cref="NumericKind.Finite"/>.</param>
        private ExactNumericValue(NumericKind kind, BigInteger numerator, BigInteger denominator)
        {
            _kind = kind;
            _numerator = numerator;
            _denominator = denominator;
        }

        /// <summary>Builds the exact value of a boxed native numeric constant.</summary>
        /// <param name="type">The constant's declared CLR type; must be one of the types in <see cref="Types.Number"/>.</param>
        /// <param name="value">The boxed value.</param>
        /// <returns>The exact numeric representation of <paramref name="value"/>.</returns>
        public static ExactNumericValue FromBoxed(Type type, object value)
        {
            if (type == typeof(float)) return FromSingle((float)value);
            if (type == typeof(double)) return FromDouble((double)value);
            if (type == typeof(decimal)) return FromDecimal((decimal)value);
            if (type == typeof(byte)) return FromInteger((byte)value);
            if (type == typeof(ushort)) return FromInteger((ushort)value);
            if (type == typeof(uint)) return FromInteger((uint)value);
            if (type == typeof(ulong)) return FromInteger((ulong)value);
            if (type == typeof(sbyte)) return FromInteger((sbyte)value);
            if (type == typeof(short)) return FromInteger((short)value);
            if (type == typeof(int)) return FromInteger((int)value);
            if (type == typeof(long)) return FromInteger((long)value);

            throw new NotSupportedException($"Unsupported numeric constant type '{type}'.");
        }

        /// <summary>Builds the exact value of a native signed or unsigned integer, widened to <see cref="BigInteger"/> without loss.</summary>
        /// <param name="value">The integer value.</param>
        /// <returns>The exact value, as <c>value / 1</c>.</returns>
        private static ExactNumericValue FromInteger(BigInteger value) => Normalize(NumericKind.Finite, value, BigInteger.One);

        /// <summary>Builds the exact value of a <see cref="decimal"/> from its 96-bit significand, scale and sign, per <see cref="decimal.GetBits(decimal)"/>.</summary>
        /// <param name="value">The decimal value.</param>
        /// <returns>The exact value, as <c>±significand / 10^scale</c>.</returns>
        private static ExactNumericValue FromDecimal(decimal value)
        {
            int[] bits = decimal.GetBits(value);
            BigInteger significand = (BigInteger)(uint)bits[0]
                | ((BigInteger)(uint)bits[1] << 32)
                | ((BigInteger)(uint)bits[2] << 64);
            bool negative = (bits[3] & int.MinValue) != 0;
            int scale = (bits[3] >> 16) & 0xFF;

            BigInteger numerator = negative ? -significand : significand;
            BigInteger denominator = BigInteger.Pow(10, scale);
            return Normalize(NumericKind.Finite, numerator, denominator);
        }

        /// <summary>Builds the exact value of a <see cref="float"/> from its IEEE 754 sign/exponent/mantissa bits, handling subnormals; never via <see cref="object.ToString"/> or a round trip through another numeric type.</summary>
        /// <param name="value">The single-precision value.</param>
        /// <returns>The exact value, or a <see cref="NumericKind.NaN"/>/infinity category for non-finite input.</returns>
        private static ExactNumericValue FromSingle(float value)
        {
            if (float.IsNaN(value)) return new ExactNumericValue(NumericKind.NaN, default, default);
            if (float.IsPositiveInfinity(value)) return new ExactNumericValue(NumericKind.PositiveInfinity, default, default);
            if (float.IsNegativeInfinity(value)) return new ExactNumericValue(NumericKind.NegativeInfinity, default, default);

            int bits = BitConverter.SingleToInt32Bits(value);
            bool negative = bits < 0;
            int exponent = (bits >> 23) & 0xFF;
            int mantissa = bits & 0x7FFFFF;

            BigInteger significand;
            int binaryExponent;
            if (exponent == 0)
            {
                significand = mantissa;
                binaryExponent = -149; // subnormal: value = mantissa * 2^(1 - 127 - 23)
            }
            else
            {
                significand = mantissa | (1 << 23);
                binaryExponent = exponent - 127 - 23; // normal: value = (2^23 + mantissa) * 2^(exponent - 127 - 23)
            }

            if (negative) significand = -significand;
            return FromBinary(significand, binaryExponent);
        }

        /// <summary>Builds the exact value of a <see cref="double"/> from its IEEE 754 sign/exponent/mantissa bits, handling subnormals; never via <see cref="object.ToString"/> or a round trip through another numeric type.</summary>
        /// <param name="value">The double-precision value.</param>
        /// <returns>The exact value, or a <see cref="NumericKind.NaN"/>/infinity category for non-finite input.</returns>
        private static ExactNumericValue FromDouble(double value)
        {
            if (double.IsNaN(value)) return new ExactNumericValue(NumericKind.NaN, default, default);
            if (double.IsPositiveInfinity(value)) return new ExactNumericValue(NumericKind.PositiveInfinity, default, default);
            if (double.IsNegativeInfinity(value)) return new ExactNumericValue(NumericKind.NegativeInfinity, default, default);

            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;

            BigInteger significand;
            int binaryExponent;
            if (exponent == 0)
            {
                significand = mantissa;
                binaryExponent = -1074; // subnormal: value = mantissa * 2^(1 - 1023 - 52)
            }
            else
            {
                significand = mantissa | (1L << 52);
                binaryExponent = exponent - 1023 - 52; // normal: value = (2^52 + mantissa) * 2^(exponent - 1023 - 52)
            }

            if (negative) significand = -significand;
            return FromBinary(significand, binaryExponent);
        }

        /// <summary>Converts a <c>significand * 2^binaryExponent</c> pair (as produced by <see cref="FromSingle"/>/<see cref="FromDouble"/>) into a normalized exact rational.</summary>
        /// <param name="significand">The signed integer significand.</param>
        /// <param name="binaryExponent">The base-2 exponent applied to <paramref name="significand"/>; may be negative.</param>
        /// <returns>The normalized exact finite value.</returns>
        private static ExactNumericValue FromBinary(BigInteger significand, int binaryExponent)
        {
            if (binaryExponent >= 0)
            {
                return Normalize(NumericKind.Finite, significand * BigInteger.Pow(2, binaryExponent), BigInteger.One);
            }

            return Normalize(NumericKind.Finite, significand, BigInteger.Pow(2, -binaryExponent));
        }

        /// <summary>Reduces a <c>numerator / denominator</c> pair to its canonical form (zero collapses to <c>0/1</c>; otherwise divided by their greatest common divisor), so equal values always compare and hash identically.</summary>
        /// <param name="kind">The value's category; passed through unchanged.</param>
        /// <param name="numerator">The signed numerator before reduction.</param>
        /// <param name="denominator">The positive denominator before reduction.</param>
        /// <returns>The normalized exact numeric value.</returns>
        private static ExactNumericValue Normalize(NumericKind kind, BigInteger numerator, BigInteger denominator)
        {
            if (numerator.IsZero) return new ExactNumericValue(kind, BigInteger.Zero, BigInteger.One);

            BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            return new ExactNumericValue(kind, numerator / gcd, denominator / gcd);
        }

        /// <inheritdoc/>
        public bool Equals(ExactNumericValue other)
        {
            if (_kind != other._kind) return false;
            return _kind != NumericKind.Finite || (_numerator == other._numerator && _denominator == other._denominator);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ExactNumericValue other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
            => _kind == NumericKind.Finite ? HashCode.Combine(_kind, _numerator, _denominator) : HashCode.Combine(_kind);

        /// <summary>
        /// Deterministically orders exact numeric values: <see cref="NumericKind.NegativeInfinity"/> first,
        /// then <see cref="NumericKind.Finite"/> values by their exact rational value, then
        /// <see cref="NumericKind.PositiveInfinity"/>, then <see cref="NumericKind.NaN"/> last. Used by the
        /// S4 structural canonical-order key (<see cref="Utils.Mathematics.Expressions.ExpressionCanonicalOrder"/>)
        /// to order numeric <see cref="ConstantExpression"/> terms without ever formatting the value as text.
        /// </summary>
        /// <param name="other">The value to compare against.</param>
        /// <returns>A negative value if this value sorts before <paramref name="other"/>, zero if equal, positive otherwise.</returns>
        public int CompareTo(ExactNumericValue other)
        {
            int kindRank = KindRank(_kind);
            int otherKindRank = KindRank(other._kind);
            if (kindRank != otherKindRank) return kindRank.CompareTo(otherKindRank);
            if (_kind != NumericKind.Finite) return 0;

            // Denominators are always positive after Normalize, so cross-multiplication preserves order.
            BigInteger left = _numerator * other._denominator;
            BigInteger right = other._numerator * _denominator;
            return left.CompareTo(right);
        }

        /// <summary>Fixed ordering rank for each <see cref="NumericKind"/>, used by <see cref="CompareTo(ExactNumericValue)"/>: negative infinity first, then finite values (by exact rational value), then positive infinity, then NaN last.</summary>
        /// <param name="kind">The category to rank.</param>
        /// <returns>A fixed, deterministic integer rank.</returns>
        private static int KindRank(NumericKind kind) => kind switch
        {
            NumericKind.NegativeInfinity => 0,
            NumericKind.Finite => 1,
            NumericKind.PositiveInfinity => 2,
            NumericKind.NaN => 3,
            _ => 4,
        };
    }
}
