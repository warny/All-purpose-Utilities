using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>The expression-tree contracts characterized by the ETC coverage matrix.</summary>
internal enum ExpressionContract
{
    /// <summary>A generic <see cref="ExpressionTransformer"/> subclass that recurses through <c>PrepareExpression</c> and finalizes with <c>CopyExpression</c>.</summary>
    GenericTraversal,

    /// <summary>The exact built-in <see cref="ExpressionSimplifier"/>.</summary>
    ExactSimplifier,

    /// <summary>A derived <see cref="ExpressionSimplifier"/> subclass that overrides nothing.</summary>
    DerivedSimplifier,

    /// <summary>Free-parameter substitution through the protected <c>ExpressionTransformer.ReplaceArguments</c>.</summary>
    Substitution,

    /// <summary>The public <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/> on two independently built trees.</summary>
    Equality,

    /// <summary>The public <see cref="ExpressionComparer.GetHashCode(Expression)"/>, checked against <see cref="Equality"/>.</summary>
    Hashing,
}

/// <summary>
/// Runs one <see cref="ExpressionContract"/> on one <see cref="ExpressionNodeSample"/> and classifies the observed
/// behavior into a short, stable outcome string recorded by the ETC baseline matrix.
/// </summary>
/// <remarks>
/// Outcome vocabulary (see the roadmap's "Outcome vocabulary" section):
/// <list type="bullet">
/// <item><description><c>Throws:&lt;Exception&gt;</c>: the contract threw (a <see cref="TargetInvocationException"/> is unwrapped).</description></item>
/// <item><description><c>Corrupts</c>: a result was produced but its <see cref="Expression.Type"/> changed, a child was lost, or (simplifier) the compiled result differs from the original.</description></item>
/// <item><description><c>Unbound</c>: (simplifier) the result can no longer be compiled because it references a parameter that is not in scope.</description></item>
/// <item><description><c>Rewrites</c>: every marker leaf was rewritten.</description></item>
/// <item><description><c>Partial</c>: some, but not all, marker leaves were rewritten.</description></item>
/// <item><description><c>Opaque</c>: no marker leaf was rewritten; the node was passed through.</description></item>
/// <item><description><c>Same</c>: the very same instance was returned, and the sample has no marker leaf.</description></item>
/// <item><description><c>Rebuilt</c>: a new, semantically equivalent instance was returned, and the sample has no marker leaf.</description></item>
/// <item><description><c>Equal</c>/<c>Unequal</c>: the comparer's verdict on two independently built trees.</description></item>
/// <item><description><c>Consistent</c>: hashing is consistent with equality (identical hashes for an equal pair, no exception); <c>Mismatch</c> otherwise.</description></item>
/// </list>
/// </remarks>
internal static class ExpressionContractProbe
{
    /// <summary>The value bound to <see cref="ExpressionNodeCatalog.Free"/> when running a sample.</summary>
    private const double FreeValue = 3.0;

    /// <summary>The constant substituted for <see cref="ExpressionNodeCatalog.Free"/> by the substitution contract.</summary>
    private const double SubstitutedValue = 5.0;

    /// <summary>Runs <paramref name="contract"/> on a fresh tree built from <paramref name="sample"/>.</summary>
    /// <param name="sample">The sample to exercise.</param>
    /// <param name="contract">The contract to run.</param>
    /// <returns>The classified outcome.</returns>
    public static string Observe(ExpressionNodeSample sample, ExpressionContract contract) => contract switch
    {
        ExpressionContract.GenericTraversal => ObserveGenericTraversal(sample.Build()),
        ExpressionContract.ExactSimplifier => ObserveSimplifier(sample.Build(), static e => new ExpressionSimplifier().Simplify(e)),
        ExpressionContract.DerivedSimplifier => ObserveSimplifier(sample.Build(), static e => new DerivedSimplifier().Simplify(e)),
        ExpressionContract.Substitution => ObserveSubstitution(sample.Build()),
        ExpressionContract.Equality => Guard(() => ExpressionComparer.Default.Equals(sample.Build(), sample.Build()) ? "Equal" : "Unequal"),
        ExpressionContract.Hashing => ObserveHashing(sample),
        _ => throw new ArgumentOutOfRangeException(nameof(contract)),
    };

    /// <summary>Formats every contract outcome of <paramref name="sample"/> as one baseline-table row.</summary>
    /// <param name="sample">The sample to exercise.</param>
    /// <returns>The row, in <see cref="ExpressionContract"/> declaration order, separated by <c>" | "</c>.</returns>
    public static string ObserveRow(ExpressionNodeSample sample)
        => string.Join(" | ", Enum.GetValues<ExpressionContract>().Select(contract => Observe(sample, contract)));

    /// <summary>Classifies the generic recursive transformer, whose only rule rewrites the marker constant <c>1.0</c> into <c>2.0</c>.</summary>
    /// <param name="original">The tree to transform.</param>
    /// <returns>The classified outcome.</returns>
    private static string ObserveGenericTraversal(Expression original) => Guard(() =>
    {
        Expression result = new MarkerRewritingTransformer().Transform(original);
        MarkerCounts before = MarkerCounts.Of(original);
        MarkerCounts after = MarkerCounts.Of(result);

        if (result.Type != original.Type || after.UnitConstants + after.RewrittenConstants < before.UnitConstants)
        {
            return "Corrupts";
        }

        if (RootMetadata(result) != RootMetadata(original))
        {
            return "Drops";
        }

        return ClassifyMarkers(original, result, before.UnitConstants, after.RewrittenConstants);
    });

    /// <summary>Classifies a simplifier, using the <c>p * 1.0</c> marker and a compiled run of both trees.</summary>
    /// <param name="original">The tree to simplify.</param>
    /// <param name="simplify">The simplification entry point.</param>
    /// <returns>The classified outcome.</returns>
    private static string ObserveSimplifier(Expression original, Func<Expression, Expression> simplify) => Guard(() =>
    {
        Expression result = simplify(original);
        if (result.Type != original.Type)
        {
            return "Corrupts";
        }

        string originalRun = Run(original);
        string resultRun = Run(result);
        if (!originalRun.StartsWith("Compile:", StringComparison.Ordinal) && resultRun.StartsWith("Compile:", StringComparison.Ordinal))
        {
            return "Unbound";
        }

        if (originalRun != resultRun)
        {
            return "Corrupts";
        }

        int markers = MarkerCounts.Of(original).UnitMultiplications;
        int rewritten = markers - MarkerCounts.Of(result).UnitMultiplications;
        return ClassifyMarkers(original, result, markers, rewritten);
    });

    /// <summary>Classifies substitution of <see cref="ExpressionNodeCatalog.Free"/> by a constant.</summary>
    /// <param name="original">The tree in which to substitute.</param>
    /// <returns>The classified outcome.</returns>
    private static string ObserveSubstitution(Expression original) => Guard(() =>
    {
        Expression result = new SubstitutingTransformer().Substitute(original, ExpressionNodeCatalog.Free, Expression.Constant(SubstitutedValue));
        if (result.Type != original.Type)
        {
            return "Corrupts";
        }

        // A bare free parameter is itself the substitution target, so its root legitimately changes.
        if (!ReferenceEquals(original, ExpressionNodeCatalog.Free) && RootMetadata(result) != RootMetadata(original))
        {
            return "Drops";
        }

        int occurrences = MarkerCounts.Of(original).FreeReferences;
        int replaced = occurrences - MarkerCounts.Of(result).FreeReferences;
        return ClassifyMarkers(original, result, occurrences, replaced);
    });

    /// <summary>
    /// Builds a textual fingerprint of the semantic metadata carried by the root node of <paramref name="expression"/>
    /// (not its children), used to detect metadata dropped by a structure-preserving contract.
    /// </summary>
    /// <param name="expression">The node to fingerprint.</param>
    /// <returns>The fingerprint; two nodes with the same fingerprint carry the same root metadata.</returns>
    /// <remarks>Label targets, constructors, members and methods are compared by reference identity, as the framework does.</remarks>
    internal static string RootMetadata(Expression expression)
    {
        string common = expression.NodeType + ":" + expression.Type.Name;
        string specific = expression switch
        {
            UnaryExpression unary => $"{Id(unary.Method)}/{unary.IsLifted}/{unary.IsLiftedToNull}",
            BinaryExpression binary => $"{Id(binary.Method)}/{binary.IsLifted}/{binary.IsLiftedToNull}/{binary.Conversion is not null}",
            LambdaExpression lambda => $"{lambda.Name}/{lambda.TailCall}/{lambda.Parameters.Count}",
            BlockExpression block => $"{string.Join(",", block.Variables.Select(Id))}/{block.Expressions.Count}",
            NewExpression @new => $"{Id(@new.Constructor)}/{@new.Members?.Count}",
            SwitchExpression @switch => $"{Id(@switch.Comparison)}/{@switch.Cases.Count}",
            TryExpression @try => $"{@try.Handlers.Count}/{string.Join(",", @try.Handlers.Select(handler => Id(handler.Variable) + handler.Test.Name + (handler.Filter is not null)))}/{@try.Finally is not null}/{@try.Fault is not null}",
            LoopExpression loop => $"{Id(loop.BreakLabel)}/{Id(loop.ContinueLabel)}",
            GotoExpression @goto => $"{Id(@goto.Target)}/{@goto.Kind}",
            LabelExpression label => Id(label.Target),
            IndexExpression index => Id(index.Indexer),
            MemberInitExpression memberInit => string.Join(",", memberInit.Bindings.Select(binding => binding.BindingType + Id(binding.Member))),
            ListInitExpression listInit => string.Join(",", listInit.Initializers.Select(initializer => Id(initializer.AddMethod))),
            NewArrayExpression newArray => newArray.Expressions.Count.ToString(CultureInfo.InvariantCulture),
            TypeBinaryExpression typeBinary => typeBinary.TypeOperand.Name,
            DynamicExpression dynamic => $"{Id(dynamic.Binder)}/{dynamic.DelegateType.Name}",
            MemberExpression member => Id(member.Member),
            MethodCallExpression call => Id(call.Method),
            _ => string.Empty,
        };

        return common + "|" + specific;
    }

    /// <summary>Formats a metadata object's identity, or <c>null</c>, for <see cref="RootMetadata(Expression)"/>.</summary>
    /// <param name="value">The metadata object.</param>
    /// <returns>
    /// A token that is equal for the same reflection member (declaring type, name and metadata token) or, for any
    /// other object such as a <see cref="LabelTarget"/>, a declared <see cref="ParameterExpression"/> or a binder, for
    /// the same instance.
    /// </returns>
    private static string Id(object? value) => value switch
    {
        null => "null",
        MemberInfo member => $"{member.DeclaringType?.FullName}.{member.Name}#{member.MetadataToken.ToString(CultureInfo.InvariantCulture)}",
        _ => value.GetType().Name + "#" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Classifies hashing against equality on two independently built trees.</summary>
    /// <param name="sample">The sample to hash.</param>
    /// <returns>The classified outcome.</returns>
    private static string ObserveHashing(ExpressionNodeSample sample) => Guard(() =>
    {
        Expression x = sample.Build();
        Expression y = sample.Build();
        int xHash = ExpressionComparer.Default.GetHashCode(x);
        int yHash = ExpressionComparer.Default.GetHashCode(y);
        bool equal;
        try
        {
            equal = ExpressionComparer.Default.Equals(x, y);
        }
        catch (Exception)
        {
            return "Consistent";
        }

        return !equal || xHash == yHash ? "Consistent" : "Mismatch";
    });

    /// <summary>Maps marker counts to <c>Rewrites</c>, <c>Partial</c>, <c>Opaque</c>, <c>Same</c> or <c>Rebuilt</c>.</summary>
    /// <param name="original">The original tree.</param>
    /// <param name="result">The resulting tree.</param>
    /// <param name="markers">The number of markers in <paramref name="original"/>.</param>
    /// <param name="rewritten">The number of markers rewritten in <paramref name="result"/>.</param>
    /// <returns>The classified outcome.</returns>
    private static string ClassifyMarkers(Expression original, Expression result, int markers, int rewritten)
    {
        if (markers == 0)
        {
            return ReferenceEquals(original, result) ? "Same" : "Rebuilt";
        }

        if (rewritten == markers) return "Rewrites";
        return rewritten == 0 ? "Opaque" : "Partial";
    }

    /// <summary>Runs <paramref name="observe"/> and converts an exception into a <c>Throws:&lt;type&gt;</c> outcome.</summary>
    /// <param name="observe">The observation to run.</param>
    /// <returns>The observation result, or the exception outcome.</returns>
    private static string Guard(Func<string> observe)
    {
        try
        {
            return observe();
        }
        catch (Exception exception)
        {
            return "Throws:" + Unwrap(exception).GetType().Name;
        }
    }

    /// <summary>Unwraps nested <see cref="TargetInvocationException"/> instances.</summary>
    /// <param name="exception">The caught exception.</param>
    /// <returns>The innermost non-reflection exception.</returns>
    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } inner })
        {
            exception = inner;
        }

        return exception;
    }

    /// <summary>
    /// Compiles <paramref name="expression"/> as a lambda over <see cref="ExpressionNodeCatalog.Free"/>, declaring the
    /// catalog's other free variables as block locals, runs it and formats the result.
    /// </summary>
    /// <param name="expression">The expression to run.</param>
    /// <returns>
    /// A normalized textual result: the value (sequences joined, delegates and expressions reduced to their kind),
    /// <c>Throw:&lt;type&gt;</c> for a run-time exception, or <c>Compile:&lt;type&gt;</c> when compilation fails.
    /// </returns>
    private static string Run(Expression expression)
    {
        Delegate compiled;
        try
        {
            ParameterExpression[] locals = [ExpressionNodeCatalog.Variable, ExpressionNodeCatalog.IntVariable];
            Expression body = expression.Type == typeof(void)
                ? Expression.Block(locals, expression, Expression.Constant("void", typeof(object)))
                : Expression.Block(locals, Expression.Convert(expression, typeof(object)));
            compiled = Expression.Lambda<Func<double, object>>(body, ExpressionNodeCatalog.Free).Compile();
        }
        catch (Exception exception)
        {
            return "Compile:" + Unwrap(exception).GetType().Name;
        }

        try
        {
            return Format(((Func<double, object>)compiled)(FreeValue));
        }
        catch (Exception exception)
        {
            return "Throw:" + Unwrap(exception).GetType().Name;
        }
    }

    /// <summary>Formats a run result so that equal values produce equal text.</summary>
    /// <param name="value">The value returned by a compiled sample.</param>
    /// <returns>The normalized text.</returns>
    private static string Format(object? value) => value switch
    {
        null => "null",
        Delegate => "delegate",
        Expression => "expression",
        string text => text,
        IEnumerable sequence => "[" + string.Join(",", sequence.Cast<object?>().Select(Format)) + "]",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Counts the three markers of the catalog in a tree.</summary>
    /// <param name="UnitConstants">The number of <c>1.0</c> <see cref="double"/> constants.</param>
    /// <param name="RewrittenConstants">The number of <c>2.0</c> <see cref="double"/> constants.</param>
    /// <param name="UnitMultiplications">The number of <c>_ * 1.0</c> multiplications.</param>
    /// <param name="FreeReferences">The number of references to <see cref="ExpressionNodeCatalog.Free"/>.</param>
    private readonly record struct MarkerCounts(int UnitConstants, int RewrittenConstants, int UnitMultiplications, int FreeReferences)
    {
        /// <summary>Counts the markers of <paramref name="expression"/>.</summary>
        /// <param name="expression">The tree to inspect.</param>
        /// <returns>The marker counts.</returns>
        public static MarkerCounts Of(Expression expression)
        {
            var counter = new MarkerCounter();
            counter.Visit(expression);
            return new MarkerCounts(counter.UnitConstants, counter.RewrittenConstants, counter.UnitMultiplications, counter.FreeReferences);
        }
    }

    /// <summary>An <see cref="ExpressionVisitor"/> that counts markers without modifying or reducing anything.</summary>
    private sealed class MarkerCounter : ExpressionVisitor
    {
        /// <summary>Gets the number of <c>1.0</c> constants seen.</summary>
        public int UnitConstants { get; private set; }

        /// <summary>Gets the number of <c>2.0</c> constants seen.</summary>
        public int RewrittenConstants { get; private set; }

        /// <summary>Gets the number of <c>_ * 1.0</c> multiplications seen.</summary>
        public int UnitMultiplications { get; private set; }

        /// <summary>Gets the number of references to <see cref="ExpressionNodeCatalog.Free"/> seen.</summary>
        public int FreeReferences { get; private set; }

        /// <inheritdoc/>
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is double value)
            {
                if (value == 1.0) UnitConstants++;
                if (value == 2.0) RewrittenConstants++;
            }

            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Multiply && node.Right is ConstantExpression { Value: 1.0 })
            {
                UnitMultiplications++;
            }

            return base.VisitBinary(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (ReferenceEquals(node, ExpressionNodeCatalog.Free)) FreeReferences++;
            return node;
        }
    }

    /// <summary>A generic transformer that recurses into children and rewrites the marker constant <c>1.0</c> into <c>2.0</c>.</summary>
    private sealed class MarkerRewritingTransformer : ExpressionTransformer
    {
        /// <summary>Rewrites the marker constant.</summary>
        /// <param name="constant">The constant node.</param>
        /// <returns><c>2.0</c> for the marker constant, the node itself otherwise.</returns>
        [ExpressionSignature(ExpressionType.Constant)]
        private Expression RewriteMarker(ConstantExpression constant)
            => constant.Value is double value && value == 1.0 ? Expression.Constant(2.0) : constant;

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>A derived simplifier that overrides nothing, characterizing the subclass compatibility path.</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Exposes the protected substitution entry point of <see cref="ExpressionTransformer"/>.</summary>
    private sealed class SubstitutingTransformer : ExpressionTransformer
    {
        /// <summary>Replaces <paramref name="parameter"/> by <paramref name="replacement"/> in <paramref name="expression"/>.</summary>
        /// <param name="expression">The tree.</param>
        /// <param name="parameter">The parameter to replace.</param>
        /// <param name="replacement">The replacement expression.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Substitute(Expression expression, ParameterExpression parameter, Expression replacement)
            => ReplaceArguments(expression, [parameter], [replacement]);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }
}
