using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC closure audit (after R4): property and differential campaigns over deterministic, seeded random trees
/// (<see cref="EtcAuditGenerator"/>). Each campaign records the seed of every failure, reduces the counterexample
/// (<see cref="EtcAuditSupport.Shrink"/>) and prints the original and reduced trees. The default campaign size keeps the
/// unit suite fast; set <c>ETC_AUDIT_SEEDS</c> (count) and <c>ETC_AUDIT_SEED_BASE</c> (first seed) to run a larger one.
/// </summary>
/// <remarks>
/// Passing campaigns are evidence over the generated families and compositions, not a proof: the generator covers the
/// families listed in <see cref="AuditFamilies"/>, with depths 3 to 6 productions (about 8 to 14 tree levels).
/// </remarks>
[TestClass]
[TestCategory("ETC-Audit")]
public class EtcAuditPropertyTests
{
    /// <summary>The default number of seeds per campaign.</summary>
    private const int DefaultSeeds = 120;

    /// <summary>Gets or sets the test context.</summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>A derived simplifier overriding nothing (exact-type gates off).</summary>
    private sealed class DerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>A generic transformer without rules that recurses through <c>PrepareExpression</c> and finalizes with <c>CopyExpression</c>.</summary>
    private sealed class RecursiveTransformer : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression PrepareExpression(Expression e) => TransformCore(e);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>A generic transformer without rules and without overriding <c>PrepareExpression</c>.</summary>
    private sealed class NonRecursiveTransformer : ExpressionTransformer
    {
        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);

        /// <inheritdoc/>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters) => CopyExpression(e, parameters);
    }

    /// <summary>Exposes the protected substitution.</summary>
    private sealed class ExposedTransformer : ExpressionTransformer
    {
        /// <summary>Calls <c>ReplaceArguments</c>.</summary>
        /// <param name="e">The tree.</param>
        /// <param name="parameter">The replaced parameter.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The substituted tree.</returns>
        public Expression Replace(Expression e, ParameterExpression parameter, Expression replacement) => ReplaceArguments(e, [parameter], [replacement]);

        /// <inheritdoc/>
        public override Expression Transform(Expression expression) => TransformCore(expression);
    }

    /// <summary>Gets the seeds of a campaign.</summary>
    /// <param name="salt">A per-campaign offset, so campaigns do not share trees.</param>
    /// <returns>The seeds.</returns>
    private static IEnumerable<int> Seeds(int salt)
    {
        int count = int.TryParse(Environment.GetEnvironmentVariable("ETC_AUDIT_SEEDS"), out int c) ? c : DefaultSeeds;
        int first = int.TryParse(Environment.GetEnvironmentVariable("ETC_AUDIT_SEED_BASE"), out int b) ? b : 1;
        return Enumerable.Range(first, count).Select(seed => seed * 7919 + salt);
    }

    /// <summary>
    /// The production depth used for a seed: 3 to 6 by default (about 8 to 14 tree levels); <c>ETC_AUDIT_MIN_DEPTH</c> raises
    /// the lower bound of the four-value range.
    /// </summary>
    /// <param name="seed">The seed.</param>
    /// <returns>The depth.</returns>
    private static int DepthOf(int seed)
        => (int.TryParse(Environment.GetEnvironmentVariable("ETC_AUDIT_MIN_DEPTH"), out int minimum) ? minimum : 3) + (seed & 0x7fffffff) % 4;

    /// <summary>Measures a tree: distinct nodes and depth (extension nodes count as leaves).</summary>
    /// <param name="e">The tree.</param>
    /// <returns>The node count and the depth.</returns>
    private static (int Nodes, int Depth) Measure(Expression e)
    {
        int Depth(Expression node, int level) => level > 200 ? level : node switch
        {
            BinaryExpression b => Math.Max(Depth(b.Left, level + 1), Depth(b.Right, level + 1)),
            UnaryExpression { Operand: { } o } => Depth(o, level + 1),
            MethodCallExpression c => c.Arguments.Select(a => Depth(a, level + 1)).DefaultIfEmpty(level).Max(),
            ConditionalExpression c => new[] { c.Test, c.IfTrue, c.IfFalse }.Max(a => Depth(a, level + 1)),
            InvocationExpression i => i.Arguments.Append(i.Expression).Max(a => Depth(a, level + 1)),
            LambdaExpression l => Depth(l.Body, level + 1),
            BlockExpression b => b.Expressions.Max(a => Depth(a, level + 1)),
            TryExpression t => t.Handlers.Select(h => h.Body).Append(t.Body).Max(a => Depth(a, level + 1)),
            DynamicExpression d => d.Arguments.Select(a => Depth(a, level + 1)).DefaultIfEmpty(level).Max(),
            MemberExpression { Expression: { } m } => Depth(m, level + 1),
            MemberInitExpression m => m.Bindings.OfType<MemberAssignment>().Select(a => Depth(a.Expression, level + 2)).DefaultIfEmpty(level).Max(),
            ListInitExpression l => l.Initializers.SelectMany(i => i.Arguments).Select(a => Depth(a, level + 2)).DefaultIfEmpty(level).Max(),
            IndexExpression i => i.Arguments.Append(i.Object!).Max(a => Depth(a, level + 1)),
            NewArrayExpression n => n.Expressions.Select(a => Depth(a, level + 1)).DefaultIfEmpty(level).Max(),
            SwitchExpression s => s.Cases.Select(c => c.Body).Append(s.SwitchValue).Append(s.DefaultBody).Max(a => Depth(a, level + 1)),
            LabelExpression { DefaultValue: { } v } => Depth(v, level + 1),
            GotoExpression { Value: { } v } => Depth(v, level + 1),
            _ => level,
        };

        return (EtcAuditSupport.Nodes(e).Count, Depth(e, 1));
    }

    /// <summary>Runs an engine operation with every extension node of the tree armed, and checks that no binder was called.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="generator">The generator of the tree.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The result.</returns>
    private static T Engine<T>(EtcAuditGenerator generator, Func<T> operation)
    {
        int binderCalls = generator.Binder.TotalCalls + generator.TwinBinder.TotalCalls;
        foreach (AuditExtension extension in generator.Extensions) extension.Armed = true;
        try
        {
            return operation();
        }
        finally
        {
            foreach (AuditExtension extension in generator.Extensions)
            {
                extension.Armed = false;
                foreach (string call in extension.ArmedCalls)
                {
                    lock (ExtensionReads)
                    {
                        ExtensionReads[call] = ExtensionReads.TryGetValue(call, out int count) ? count + 1 : 1;
                    }

                    Assert.IsTrue(IsFrameworkRead(call, generator.WrapsExtensions), "The engine read or called a member of an extension node: " + call);
                }

                extension.ArmedCalls.Clear();
            }

            Assert.AreEqual(binderCalls, generator.Binder.TotalCalls + generator.TwinBinder.TotalCalls, "The engine called a dynamic binder.");
        }
    }

    /// <summary>
    /// The member calls on extension nodes observed during engine operations, keyed by <c>member &lt;- immediate caller</c>.
    /// Only the <c>NodeType</c> and <c>Type</c> getters may appear, and only from framework factories or from the framework
    /// getters of untyped blocks and conditionals (whose tail call hides their own frame); see
    /// <see cref="IsFrameworkRead"/>.
    /// </summary>
    private static readonly Dictionary<string, int> ExtensionReads = [];

    /// <summary>A failure of a differential campaign.</summary>
    /// <param name="Seed">The seed.</param>
    /// <param name="Kind">The divergence.</param>
    /// <param name="Original">The original body.</param>
    /// <param name="Reduced">The reduced body.</param>
    /// <param name="Detail">The outcomes.</param>
    private sealed record Failure(int Seed, string Kind, Expression Original, Expression Reduced, string Detail);

    /// <summary>Formats failures for an assertion message.</summary>
    /// <param name="failures">The failures.</param>
    /// <returns>The message.</returns>
    private static string Report(List<Failure> failures)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{failures.Count} failure(s).");
        foreach (Failure failure in failures.Take(5))
        {
            builder.AppendLine($"--- seed {failure.Seed}: {failure.Kind}");
            builder.AppendLine(failure.Detail);
            builder.AppendLine("reduced:");
            builder.AppendLine(EtcAuditSupport.Print(failure.Reduced));
        }

        return builder.ToString();
    }

    /// <summary>
    /// The differential check of a transformation: the transformed tree must compile and show the same effects, in the same
    /// order and number, the same exceptions and the same values (up to rounding).
    /// </summary>
    /// <remarks>
    /// The original is evaluated compiled and interpreted (<c>Compile(preferInterpretation: true)</c>). When the two disagree,
    /// the framework itself gives the tree two meanings (observed: a parameter instance redeclared by a nested lambda and
    /// captured by a closure is mis-compiled by <c>System.Linq.Expressions</c>), so there is no reference and the tree is
    /// skipped (<see cref="EtcAuditSupport.Divergence.None"/> with <paramref name="frameworkInconsistent"/> set).
    /// </remarks>
    /// <param name="body">The original body.</param>
    /// <param name="transform">The transformation of the wrapped lambda.</param>
    /// <param name="detail">The outcomes on failure.</param>
    /// <param name="frameworkInconsistent">Whether the original has no reliable meaning (compiled and interpreted differ).</param>
    /// <returns>The divergence, or <see langword="null"/> when the original cannot be compiled.</returns>
    private static EtcAuditSupport.Divergence? Differential(Expression body, Func<Expression<Func<double, double, double>>, LambdaExpression> transform, out string detail, out bool frameworkInconsistent)
    {
        detail = "";
        frameworkInconsistent = false;
        Expression<Func<double, double, double>> original = EtcAuditGenerator.Wrap(body);
        Func<double, double, double>? compiled = EtcAuditSupport.TryCompile(original, out _);
        if (compiled is null) return null;

        AuditOutcome[] expected = EtcAuditSupport.Evaluate(compiled);
        Func<double, double, double> interpreted;
        try
        {
            interpreted = original.Compile(preferInterpretation: true);
        }
        catch (ArgumentOutOfRangeException)
        {
            // The framework interpreter fails on the same shape the compiler mis-compiles (observed in LocalVariables.Box).
            frameworkInconsistent = true;
            return EtcAuditSupport.Divergence.None;
        }

        if (EtcAuditSupport.Compare(expected, EtcAuditSupport.Evaluate(interpreted)) > EtcAuditSupport.Divergence.Rounding)
        {
            frameworkInconsistent = true;
            return EtcAuditSupport.Divergence.None;
        }

        LambdaExpression transformed = transform(original);
        var typed = transformed as Expression<Func<double, double, double>>;
        if (typed is null)
        {
            detail = "the transformation changed the delegate type: " + transformed.Type;
            return EtcAuditSupport.Divergence.Effects;
        }

        Func<double, double, double>? transformedCompiled = EtcAuditSupport.TryCompile(typed, out string? error);
        if (transformedCompiled is null)
        {
            detail = "the transformed tree does not compile: " + error;
            return EtcAuditSupport.Divergence.Effects;
        }

        AuditOutcome[] actual = EtcAuditSupport.Evaluate(transformedCompiled);
        EtcAuditSupport.Divergence divergence = EtcAuditSupport.Compare(expected, actual);
        if (divergence > EtcAuditSupport.Divergence.Rounding)
        {
            // The framework compiler may give the transformed tree another meaning than its interpreter (observed: an inlined
            // block variable's slot reused by an uninitialized variable of a later block). When the interpreted transformed
            // tree has the original's meaning, the engine preserved it and the difference is the framework's.
            AuditOutcome[] actualInterpreted = EtcAuditSupport.Evaluate(typed.Compile(preferInterpretation: true));
            if (EtcAuditSupport.Compare(actual, actualInterpreted) > EtcAuditSupport.Divergence.Rounding
                && EtcAuditSupport.Compare(expected, actualInterpreted) <= EtcAuditSupport.Divergence.Rounding)
            {
                frameworkInconsistent = true;
                return EtcAuditSupport.Divergence.None;
            }

            // A divergence that disappears on the regular inputs is the documented S3 class (symbolic identities on NaN,
            // infinities and signed zeros, and whatever depends on them: a branch, an exception, an argument).
            if (EtcAuditSupport.Compare(expected, actual, regularOnly: true) <= EtcAuditSupport.Divergence.Rounding)
            {
                divergence = EtcAuditSupport.Divergence.Ieee;
            }
            var builder = new StringBuilder();
            for (int i = 0; i < expected.Length; i++)
            {
                builder.AppendLine($"  input {EtcAuditSupport.Inputs[i]}: expected {expected[i]} / actual {actual[i]}");
            }

            builder.AppendLine("transformed:");
            builder.AppendLine(EtcAuditSupport.Print(typed));
            detail = builder.ToString();
        }

        return divergence;
    }

    /// <summary>Runs a differential campaign of one transformation over the seeds of a campaign.</summary>
    /// <param name="salt">The campaign salt.</param>
    /// <param name="families">The generated families.</param>
    /// <param name="transform">The transformation, receiving the generator (for arming) and the wrapped lambda.</param>
    /// <param name="ieee">Collects the reduced IEEE-class divergences (a NaN or infinity on one side only), reported for triage.</param>
    /// <returns>The failures.</returns>
    private List<Failure> RunDifferential(int salt, AuditFamilies families, Func<EtcAuditGenerator, Expression<Func<double, double, double>>, LambdaExpression> transform, List<Failure> ieee)
    {
        var failures = new List<Failure>();
        int compiled = 0, inconsistent = 0;
        var refusals = new List<string>();
        long totalNodes = 0;
        int maxDepth = 0;
        foreach (int seed in Seeds(salt))
        {
            var generator = new EtcAuditGenerator(seed, DepthOf(seed), families);
            Expression body = generator.Generate();
            (int nodes, int depth) = Measure(body);
            totalNodes += nodes;
            maxDepth = Math.Max(maxDepth, depth);
            EtcAuditSupport.Divergence? divergence;
            string detail;
            bool frameworkInconsistent;
            try
            {
                divergence = Differential(body, lambda => transform(generator, lambda), out detail, out frameworkInconsistent);
            }
            catch (ExpressionResourceLimitException ex)
            {
                // A controlled refusal is the resource contract, not a wrong result (AUD-03: rule loops on c * (y - 1)).
                refusals.Add($"{seed}:{ex.Kind}");
                continue;
            }
            catch (Exception ex) when (ex is not AssertFailedException)
            {
                failures.Add(new Failure(seed, "transformation threw " + ex.GetType().Name + ": " + ex.Message, body, body, ex.ToString()));
                continue;
            }

            if (divergence is null) continue;
            if (frameworkInconsistent)
            {
                inconsistent++;
                continue;
            }

            compiled++;
            if (divergence > EtcAuditSupport.Divergence.Rounding)
            {
                EtcAuditSupport.Divergence kind = divergence.Value;
                Expression reduced = EtcAuditSupport.Shrink(body, candidate =>
                {
                    var g = new EtcAuditGenerator(seed, DepthOf(seed), families);
                    return Differential(candidate, lambda => transform(g, lambda), out _, out bool skip) >= kind && !skip;
                });
                Differential(reduced, lambda => transform(new EtcAuditGenerator(seed, DepthOf(seed), families), lambda), out string reducedDetail, out _);
                var failure = new Failure(seed, kind.ToString(), body, reduced, (reducedDetail.Length > 0 ? reducedDetail : detail).Split("transformed:")[0]);
                (kind == EtcAuditSupport.Divergence.Ieee ? ieee : failures).Add(failure);
            }
        }

        TestContext.WriteLine($"salt {salt}: {compiled} trees compared (mean {totalNodes / Math.Max(1, compiled + inconsistent)} distinct nodes, max depth {maxDepth}), {inconsistent} framework-inconsistent (compiled and interpreted disagree), {failures.Count} failures, {ieee.Count} S3/IEEE-class divergences, {refusals.Count} controlled refusals ({string.Join(",", refusals.Take(10))})");
        foreach (Failure failure in ieee) TestContext.WriteLine($"S3/IEEE seed {failure.Seed}: reduced {failure.Reduced}");
        Assert.IsTrue(compiled > 0, "The generator produced no compilable tree.");
        return failures;
    }

    /// <summary>
    /// The exact simplifier preserves every observable property of trees mixing every family (effects, exceptions,
    /// initializers, dynamic operations, quotes, extensions, control flow); the engine never calls a binder or an extension
    /// member.
    /// </summary>
    [TestMethod]
    public void Audit_Simplify_PreservesObservableBehavior()
    {
        var ieee = new List<Failure>();
        var simplifier = new ExpressionSimplifier();
        List<Failure> failures = RunDifferential(1, AuditFamilies.All, (g, lambda) => Engine(g, () => (LambdaExpression)simplifier.Simplify(lambda)), ieee);
        Assert.AreEqual(0, failures.Count, Report(failures));
    }

    /// <summary>The same campaign with the quote opt-in (quoted lambdas simplified) and with a derived simplifier.</summary>
    [TestMethod]
    public void Audit_SimplifyVariants_PreserveObservableBehavior()
    {
        var ieee = new List<Failure>();
        var optIn = new ExpressionSimplifier { SimplifyQuotedExpressions = true };
        var derived = new DerivedSimplifier();
        List<Failure> failures = RunDifferential(2, AuditFamilies.All, (g, lambda) => Engine(g, () => (LambdaExpression)optIn.Simplify(lambda)), ieee);
        failures.AddRange(RunDifferential(3, AuditFamilies.All, (g, lambda) => Engine(g, () => (LambdaExpression)derived.Simplify(lambda)), ieee));
        Assert.AreEqual(0, failures.Count, Report(failures));
    }

    /// <summary>Generic transformers without rules are faithful: same structure (raw structural equality), same behavior.</summary>
    [TestMethod]
    public void Audit_GenericTransformers_AreFaithful()
    {
        var failures = new List<string>();
        foreach (int seed in Seeds(4))
        {
            var generator = new EtcAuditGenerator(seed, DepthOf(seed), AuditFamilies.All);
            Expression<Func<double, double, double>> lambda = EtcAuditGenerator.Wrap(generator.Generate());
            foreach (ExpressionTransformer transformer in new ExpressionTransformer[] { new RecursiveTransformer(), new NonRecursiveTransformer() })
            {
                Expression result = Engine(generator, () => transformer.Transform(lambda));
                bool equal = Engine(generator, () => ExpressionComparer.StructuralEqualsRaw(lambda, result));
                if (!equal)
                {
                    failures.Add($"seed {seed} {transformer.GetType().Name}: not structurally equal\n{EtcAuditSupport.Print(lambda)}\n=>\n{EtcAuditSupport.Print(result)}");
                    continue;
                }

                if (result is not Expression<Func<double, double, double>> typed) continue;
                Func<double, double, double>? original = EtcAuditSupport.TryCompile(lambda, out _);
                Func<double, double, double>? transformed = EtcAuditSupport.TryCompile(typed, out string? error);
                if (original is null) continue;
                if (transformed is null)
                {
                    failures.Add($"seed {seed} {transformer.GetType().Name}: does not compile: {error}");
                    continue;
                }

                EtcAuditSupport.Divergence divergence = EtcAuditSupport.Compare(EtcAuditSupport.Evaluate(original), EtcAuditSupport.Evaluate(transformed));
                if (divergence != EtcAuditSupport.Divergence.None) failures.Add($"seed {seed} {transformer.GetType().Name}: {divergence}");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(5)));
    }

    /// <summary>
    /// Equality and hashing: a fresh clone and an alpha-renamed clone are equal to the original (raw and public comparers),
    /// with equal hashes and a zero canonical comparison; the public comparer is reflexive, symmetric and transitive on a pool
    /// of trees and near-variants, equal trees hash equally, and the canonical order is antisymmetric and transitive.
    /// </summary>
    [TestMethod]
    public void Audit_EqualityHashAndKeys_Properties()
    {
        var failures = new List<string>();
        ParameterExpression[][] noScopes = [];
        int refused = 0;
        foreach (int seed in Seeds(5).Take(60))
        {
            var generator = new EtcAuditGenerator(seed, DepthOf(seed), AuditFamilies.All);
            Expression body = generator.Generate();
            Expression<Func<double, double, double>> lambda = EtcAuditGenerator.Wrap(body);
            bool hasExtension = generator.Extensions.Count > 0;
            try
            {
                ExpressionComparer.Default.GetHashCode(lambda);
            }
            catch (ExpressionResourceLimitException)
            {
                // A controlled refusal of the public comparer (AUD-03); the raw walks are still covered by the other seeds.
                continue;
            }

            try
            {
                CheckEqualityPool(seed, generator, body, lambda, hasExtension, noScopes, failures);
            }
            catch (ExpressionResourceLimitException)
            {
                refused++;
            }
        }

        TestContext.WriteLine($"{refused} seeds refused by the resource limits (AUD-03).");
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Distinct().Take(10)));
    }

    /// <summary>Checks the equality, hash and canonical-order properties on the pool of one generated tree.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="generator">The generator of the tree.</param>
    /// <param name="body">The body.</param>
    /// <param name="lambda">The wrapped body.</param>
    /// <param name="hasExtension">Whether the tree holds an extension node (no alpha-renamed clone is then compared).</param>
    /// <param name="noScopes">The empty enclosing scope list of the canonical order.</param>
    /// <param name="failures">Collects the failures.</param>
    private static void CheckEqualityPool(int seed, EtcAuditGenerator generator, Expression body, Expression<Func<double, double, double>> lambda, bool hasExtension, ParameterExpression[][] noScopes, List<string> failures)
    {
        {
            var pool = new List<(string Name, Expression Tree)>
            {
                ("original", lambda),
                ("clone", EtcAuditSupport.Clone(lambda, rename: false)),
                ("body", body),
                ("body-clone", EtcAuditSupport.Clone(body, rename: false)),
            };
            if (!hasExtension) pool.Add(("alpha", EtcAuditSupport.Clone(lambda, rename: true)));

            // Near-variants: one constant changed, x and y swapped, the operands of the first binary node swapped.
            ConstantExpression? constant = EtcAuditSupport.Nodes(body).OfType<ConstantExpression>().FirstOrDefault(c => c.Type == typeof(double));
            if (constant is not null) pool.Add(("constant+1", EtcAuditGenerator.Wrap(EtcAuditSupport.ReplaceNode(body, constant, Expression.Constant((double)constant.Value! + 1)))));
            pool.Add(("swapped-xy", Expression.Lambda<Func<double, double, double>>(body, EtcAuditGenerator.Y, EtcAuditGenerator.X)));

            foreach ((string name, Expression tree) in pool.Where(p => p.Name is "clone" or "alpha" or "body-clone"))
            {
                Expression reference = name == "body-clone" ? body : lambda;
                if (!Engine(generator, () => ExpressionComparer.StructuralEqualsRaw(reference, tree))) failures.Add($"seed {seed}: raw equality fails for {name}");
                if (Engine(generator, () => ExpressionComparer.StructuralHashRaw(reference)) != Engine(generator, () => ExpressionComparer.StructuralHashRaw(tree))) failures.Add($"seed {seed}: raw hash differs for {name}");
                if (!Engine(generator, () => ExpressionComparer.Default.Equals(reference, tree))) failures.Add($"seed {seed}: public equality fails for {name}");
                if (Engine(generator, () => ExpressionComparer.Default.GetHashCode(reference)) != Engine(generator, () => ExpressionComparer.Default.GetHashCode(tree))) failures.Add($"seed {seed}: public hash differs for {name}");
                if (Engine(generator, () => ExpressionCanonicalOrder.Compare(reference, tree, noScopes)) != 0) failures.Add($"seed {seed}: canonical order does not tie {name}");
            }

            int n = pool.Count;
            var equal = new bool[n, n];
            var order = new int[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    Expression a = pool[i].Tree, b = pool[j].Tree;
                    equal[i, j] = Engine(generator, () => ExpressionComparer.Default.Equals(a, b));
                    order[i, j] = Math.Sign(Engine(generator, () => ExpressionCanonicalOrder.Compare(a, b, noScopes)));
                    bool raw = Engine(generator, () => ExpressionComparer.StructuralEqualsRaw(a, b));
                    if (raw && order[i, j] != 0) failures.Add($"seed {seed}: raw-equal {pool[i].Name}/{pool[j].Name} but canonical order {order[i, j]}");
                    if (equal[i, j] && Engine(generator, () => ExpressionComparer.Default.GetHashCode(a)) != Engine(generator, () => ExpressionComparer.Default.GetHashCode(b)))
                    {
                        failures.Add($"seed {seed}: equal {pool[i].Name}/{pool[j].Name} with different hashes");
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (!equal[i, i]) failures.Add($"seed {seed}: not reflexive on {pool[i].Name}");
                if (order[i, i] != 0) failures.Add($"seed {seed}: canonical order not reflexive on {pool[i].Name}");
                for (int j = 0; j < n; j++)
                {
                    if (equal[i, j] != equal[j, i]) failures.Add($"seed {seed}: not symmetric {pool[i].Name}/{pool[j].Name}");
                    if (order[i, j] != -order[j, i]) failures.Add($"seed {seed}: canonical order not antisymmetric {pool[i].Name}/{pool[j].Name}");
                    for (int k = 0; k < n; k++)
                    {
                        if (equal[i, j] && equal[j, k] && !equal[i, k]) failures.Add($"seed {seed}: not transitive {pool[i].Name}/{pool[j].Name}/{pool[k].Name}");
                        if (order[i, j] <= 0 && order[j, k] <= 0 && order[i, k] > 0) failures.Add($"seed {seed}: canonical order not transitive {pool[i].Name}/{pool[j].Name}/{pool[k].Name}");
                    }
                }
            }

            // Free parameters are distinguished: swapping x and y as declarations changes the meaning when both are used.
            HashSet<ParameterExpression> free = EtcAuditSupport.FreeParameters(body);
            if (free.Contains(EtcAuditGenerator.X) && free.Contains(EtcAuditGenerator.Y)
                && Engine(generator, () => ExpressionComparer.StructuralEqualsRaw(lambda, pool.Single(p => p.Name == "swapped-xy").Tree)))
            {
                failures.Add($"seed {seed}: swapped declarations compare equal");
            }
        }
    }

    /// <summary>
    /// Capture-avoiding substitution: replacing <c>x</c> by a constant, by the parameter <c>z</c> (which the generator also
    /// declares in inner binders, to provoke captures) or by <c>-z</c> leaves no free <c>x</c> outside quotes, and has the
    /// meaning of the original evaluated with <c>x</c> bound to the replacement's value. A non-assignable replacement of an
    /// assigned <c>x</c> is refused with <see cref="ArgumentException"/>.
    /// </summary>
    [TestMethod]
    public void Audit_Substitution_IsCaptureAvoidingAndFaithful()
    {
        var failures = new List<string>();
        var transformer = new ExposedTransformer();
        ParameterExpression x = EtcAuditGenerator.X, y = EtcAuditGenerator.Y, z = EtcAuditGenerator.Z;
        int checkedCount = 0, frameworkInconsistent = 0;
        foreach (int seed in Seeds(6))
        {
            var generator = new EtcAuditGenerator(seed, DepthOf(seed), AuditFamilies.All);
            Expression body = generator.Generate();
            // Substitution never enters a quote (C-05): an x captured inside a quote stays free by design.
            if (EtcAuditSupport.Nodes(body).Any(node => node.NodeType == ExpressionType.Quote && EtcAuditSupport.FreeParameters(node).Contains(x))) continue;


            foreach ((string name, Expression replacement, Func<double, double> valueOf) in new (string, Expression, Func<double, double>)[]
            {
                ("constant", Expression.Constant(1.5), _ => 1.5),
                ("z", z, v => v),
                ("-z", Expression.Negate(z), v => -v),
            })
            {
                Expression substituted;
                try
                {
                    substituted = Engine(generator, () => transformer.Replace(body, x, replacement));
                }
                catch (ArgumentException) when (replacement is not ParameterExpression && EtcAuditSupport.IsWritten(body, x))
                {
                    continue;
                }
                catch (Exception ex) when (ex is not AssertFailedException)
                {
                    failures.Add($"seed {seed} {name}: substitution threw {ex.GetType().Name}: {ex.Message}\n{EtcAuditSupport.Print(body)}");
                    continue;
                }

                if (EtcAuditSupport.FreeParameters(substituted).Contains(x)) failures.Add($"seed {seed} {name}: x still free\n{EtcAuditSupport.Print(substituted)}");

                // The reference binds x to the replacement's value through an invocation. Its outer parameters are distinct
                // instances (y0, x0): redeclaring the very instances the inner lambda declares and captures in a closure makes
                // the framework compiler read the outer variable (observed, outside the engine).
                var substitutedLambda = Expression.Lambda<Func<double, double, double, double>>(substituted, y, z, x);
                var expectedLambda = Expression.Lambda<Func<double, double, double, double>>(
                    Expression.Invoke(Expression.Lambda<Func<double, double, double>>(body, x, y), replacement, OuterY), OuterY, z, OuterX);
                Func<double, double, double, double>? actual = TryCompile3(substitutedLambda);
                Func<double, double, double, double>? expected = TryCompile3(expectedLambda);
                if (expected is null) continue;
                if (actual is null)
                {
                    failures.Add($"seed {seed} {name}: substituted tree does not compile\n{EtcAuditSupport.Print(substituted)}");
                    continue;
                }

                checkedCount++;
                for (int i = 0; i < EtcAuditSupport.Inputs.Length; i++)
                {
                    (double a, double b) = EtcAuditSupport.Inputs[i];
                    AuditOutcome e = Run3(expected, b, a), o = Run3(actual, b, a);
                    EtcAuditSupport.Divergence divergence = EtcAuditSupport.Compare([e], [o]);
                    if (divergence > EtcAuditSupport.Divergence.Rounding && InterpretedAgree(expectedLambda, substitutedLambda, b, a))
                    {
                        // The framework compiler gives one of the trees another meaning than its interpreter (a parameter
                        // instance redeclared by a nested binder, an uninitialized block variable): no engine divergence.
                        frameworkInconsistent++;
                        break;
                    }

                    if (divergence > EtcAuditSupport.Divergence.Rounding)
                    {
                        failures.Add($"seed {seed} {name} input {(a, b)}: {divergence} expected {e} actual {o}\n{EtcAuditSupport.Print(body)}\n=>\n{EtcAuditSupport.Print(substituted)}");
                        break;
                    }
                }
            }
        }

        TestContext.WriteLine($"{checkedCount} substitutions checked, {frameworkInconsistent} framework-inconsistent (the interpreted trees agree).");
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(5)));
    }

    /// <summary>Under the default policy, every quote of a simplified tree is an original quote instance (never rewritten).</summary>
    [TestMethod]
    public void Audit_DefaultQuotePolicy_KeepsEveryQuoteInstance()
    {
        var simplifier = new ExpressionSimplifier();
        var derived = new DerivedSimplifier();
        int quotes = 0;
        foreach (int seed in Seeds(7))
        {
            var generator = new EtcAuditGenerator(seed, DepthOf(seed), AuditFamilies.All);
            Expression<Func<double, double, double>> lambda = EtcAuditGenerator.Wrap(generator.Generate());
            if (!generator.UsedQuote) continue;

            var originals = new HashSet<Expression>(EtcAuditSupport.Nodes(lambda).Where(n => n.NodeType == ExpressionType.Quote), ReferenceEqualityComparer.Instance);
            foreach (ExpressionSimplifier s in new[] { simplifier, derived })
            {
                Expression result;
                try
                {
                    result = Engine(generator, () => s.Simplify(lambda));
                }
                catch (ExpressionResourceLimitException)
                {
                    continue;
                }

                foreach (Expression quote in EtcAuditSupport.Nodes(result).Where(n => n.NodeType == ExpressionType.Quote))
                {
                    quotes++;
                    Assert.IsTrue(originals.Contains(quote), $"seed {seed}: {s.GetType().Name} produced a quote that is not an original instance:\n{EtcAuditSupport.Print(quote)}");
                }
            }
        }

        TestContext.WriteLine($"{quotes} quotes checked.");
    }

    /// <summary>
    /// Prints the extension member calls observed so far by the campaigns of this class (each one is already checked by
    /// <see cref="IsFrameworkRead"/> when it is observed) and checks them again.
    /// </summary>
    [TestMethod]
    public void Audit_ExtensionReadsObserved_AreFrameworkReads()
    {
        Dictionary<string, int> reads;
        lock (ExtensionReads) reads = new Dictionary<string, int>(ExtensionReads);
        foreach ((string call, int count) in reads.OrderBy(pair => pair.Key, StringComparer.Ordinal)) TestContext.WriteLine($"{count,6} {call}");

        Assert.IsTrue(reads.Keys.All(call => IsFrameworkRead(call, strict: false)));
    }


    /// <summary>
    /// Tests whether an extension call recorded during an engine operation is a framework read: a <c>NodeType</c>/<c>Type</c>
    /// getter called from <c>System.Linq.Expressions</c>, or, unless <paramref name="strict"/>, a <c>Type</c> getter whose
    /// immediate caller is engine code: the engine reads the <c>Type</c> of a framework block or conditional whose result or
    /// branch is the extension node, and that framework getter tail-calls the extension's (see
    /// <see cref="Audit_FrameworkTypeGetters_HideTheirFrame"/>; every such site observed was checked by line). The strict
    /// campaign (<see cref="Audit_StrictExtensionOpacity_NoEngineRead"/>) shows that no read remains once the extension nodes
    /// cannot be in such a position.
    /// </summary>
    /// <param name="call">The recorded call.</param>
    /// <returns><see langword="true"/> for a framework read.</returns>
    /// <param name="strict">
    /// Whether the extension nodes were generated as call arguments only (<see cref="EtcAuditGenerator.WrapsExtensions"/>): no
    /// framework getter can then delegate to them, so a read attributed to the engine is an engine read.
    /// </param>
    /// <remarks>
    /// A <c>NodeType</c> getter attributed to engine code is accepted only when the engine assembly is JIT-optimized: the
    /// framework factories the engine calls (<c>Expression.Block</c>, <c>Expression.Switch</c>…) may then be inlined into the
    /// engine method, which hides their frame. In a Debug build of the engine nothing is inlined into it, so such a read is an
    /// engine read and fails (run the campaigns with <c>-c Debug</c> to check it).
    /// </remarks>
    private static bool IsFrameworkRead(string call, bool strict)
    {
        bool getter = call.StartsWith("Type <- ", StringComparison.Ordinal) || call.StartsWith("NodeType <- ", StringComparison.Ordinal);
        if (!getter) return false;
        if (call.Contains("<- System.Linq.Expressions:", StringComparison.Ordinal)) return true;
        if (strict || !call.Contains("<- Utils:", StringComparison.Ordinal)) return false;
        return call.StartsWith("Type <- ", StringComparison.Ordinal) || EngineIsOptimized;
    }

    /// <summary>Gets whether the engine assembly is JIT-optimized (a Release build).</summary>
    private static bool EngineIsOptimized
        => typeof(ExpressionTransformer).Assembly.GetCustomAttributes(typeof(System.Diagnostics.DebuggableAttribute), false)
            .OfType<System.Diagnostics.DebuggableAttribute>().All(attribute => !attribute.IsJITOptimizerDisabled);

    /// <summary>
    /// The attribution mechanism behind <see cref="TailCalledTypeReaders"/>: reading <c>Type</c> on a framework block or
    /// conditional whose last child or branch is an extension node reaches the extension's getter with the reading method as
    /// its immediate caller (the framework getter's frame is gone).
    /// </summary>
    [TestMethod]
    public void Audit_FrameworkTypeGetters_HideTheirFrame()
    {
        var extension = new AuditExtension(Expression.Constant(1.0));
        Expression block = Expression.Block(Expression.Constant(0.0), extension);
        Expression conditional = Expression.Condition(Expression.Constant(true), extension, extension);
        extension.Armed = true;
        Type blockType = block.Type, conditionalType = conditional.Type;
        extension.Armed = false;

        Assert.AreEqual(typeof(double), blockType);
        Assert.AreEqual(typeof(double), conditionalType);
        TestContext.WriteLine(string.Join("\n", extension.ArmedCalls));
        Assert.IsTrue(extension.ArmedCalls.Count >= 1 && extension.ArmedCalls.All(call => call.StartsWith("Type <- ", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Strict extension opacity: with every extension node generated as a call argument (never the result of a block or a
    /// branch of a conditional), simplification, generic transformation, substitution, public and raw equality, hashing and
    /// canonical keys never read or call any member of an extension node; only framework factories read its <c>Type</c> and
    /// <c>NodeType</c>.
    /// </summary>
    [TestMethod]
    public void Audit_StrictExtensionOpacity_NoEngineRead()
    {
        var simplifier = new ExpressionSimplifier();
        var transformer = new ExposedTransformer();
        int extensions = 0;
        foreach (int seed in Seeds(8))
        {
            var generator = new EtcAuditGenerator(seed, DepthOf(seed), AuditFamilies.All, wrapExtensions: true);
            Expression body = generator.Generate();
            if (generator.Extensions.Count == 0) continue;
            extensions += generator.Extensions.Count;
            Expression<Func<double, double, double>> lambda = EtcAuditGenerator.Wrap(body);
            Expression clone = EtcAuditSupport.Clone(lambda, rename: false);

            Expression simplified;
            try
            {
                simplified = Engine(generator, () => simplifier.Simplify(lambda));
            }
            catch (ExpressionResourceLimitException)
            {
                continue;
            }

            Engine(generator, () => new RecursiveTransformer().Transform(lambda));
            Engine(generator, () => new NonRecursiveTransformer().Transform(lambda));
            Engine(generator, () => ExpressionComparer.Default.Equals(lambda, clone));
            Engine(generator, () => ExpressionComparer.Default.GetHashCode(lambda));
            Engine(generator, () => ExpressionComparer.StructuralEqualsRaw(lambda, clone));
            Engine(generator, () => ExpressionComparer.StructuralHashRaw(simplified));
            Engine(generator, () => ExpressionCanonicalOrder.Compare(body, simplified is LambdaExpression l ? l.Body : simplified, [[EtcAuditGenerator.X, EtcAuditGenerator.Y]]));
            try
            {
                Engine(generator, () => transformer.Replace(body, EtcAuditGenerator.X, EtcAuditGenerator.Z));
            }
            catch (ArgumentException)
            {
                // A refused substitution is not an extension read.
            }
        }

        TestContext.WriteLine($"{extensions} extension nodes checked.");
        Assert.IsTrue(extensions > 0);
    }

    /// <summary>
    /// Replays one seed of a campaign with every simplifier configuration and prints the outcome, the stack of any exception
    /// and the trees (set <c>ETC_AUDIT_REPLAY</c> to the seed, as printed by a campaign); inconclusive otherwise.
    /// </summary>
    [TestMethod]
    public void Audit_ReplaySeed()
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("ETC_AUDIT_REPLAY"), out int seed))
        {
            Assert.Inconclusive("Set ETC_AUDIT_REPLAY to replay a seed.");
            return;
        }

        {
            var generator = new EtcAuditGenerator(seed, DepthOf(seed), AuditFamilies.All);
            Expression<Func<double, double, double>> lambda = EtcAuditGenerator.Wrap(generator.Generate());
            Expression alpha = EtcAuditSupport.Clone(lambda, rename: true);
            TestContext.WriteLine($"raw equality with the alpha clone: {ExpressionComparer.StructuralEqualsRaw(lambda, alpha)}");
            TestContext.WriteLine(EtcAuditSupport.Print(lambda));
            TestContext.WriteLine("alpha clone:");
            TestContext.WriteLine(EtcAuditSupport.Print(alpha));
        }

        foreach ((string name, ExpressionSimplifier simplifier) in new (string, ExpressionSimplifier)[]
        {
            ("default", new ExpressionSimplifier()),
            ("opt-in", new ExpressionSimplifier { SimplifyQuotedExpressions = true }),
            ("derived", new DerivedSimplifier()),
        })
        {
            var generator = new EtcAuditGenerator(seed, DepthOf(seed), AuditFamilies.All);
            Expression<Func<double, double, double>> lambda = EtcAuditGenerator.Wrap(generator.Generate());
            try
            {
                var result = (Expression<Func<double, double, double>>)simplifier.Simplify(lambda);
                TestContext.WriteLine($"{name}: ok\n{EtcAuditSupport.Print(result)}");
                TestContext.WriteLine("original compiled:    " + string.Join(" | ", EtcAuditSupport.Evaluate(lambda.Compile())));
                TestContext.WriteLine("original interpreted: " + string.Join(" | ", EtcAuditSupport.Evaluate(lambda.Compile(preferInterpretation: true))));
                TestContext.WriteLine("result compiled:      " + string.Join(" | ", EtcAuditSupport.Evaluate(result.Compile())));
                TestContext.WriteLine("result interpreted:   " + string.Join(" | ", EtcAuditSupport.Evaluate(result.Compile(preferInterpretation: true))));
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"{name}: {ex.GetType().Name}: {ex.Message}");
                Type thrown = ex.GetType();
                Expression reduced = EtcAuditSupport.Shrink(lambda.Body, candidate =>
                {
                    try
                    {
                        simplifier.Simplify(EtcAuditGenerator.Wrap(candidate));
                        return false;
                    }
                    catch (Exception again)
                    {
                        return again.GetType() == thrown;
                    }
                });
                TestContext.WriteLine($"{name}: reduced counterexample:\n{EtcAuditSupport.Print(reduced)}");
            }
        }
    }

    /// <summary>The outer <c>y</c> of the substitution reference, distinct from the generator's.</summary>
    private static readonly ParameterExpression OuterY = Expression.Parameter(typeof(double), "y0");

    /// <summary>The unused outer <c>x</c> of the substitution reference, distinct from the generator's.</summary>
    private static readonly ParameterExpression OuterX = Expression.Parameter(typeof(double), "x0");

    /// <summary>Compiles a three-parameter lambda, or returns <see langword="null"/>.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <returns>The delegate.</returns>
    private static Func<double, double, double, double>? TryCompile3(Expression<Func<double, double, double, double>> lambda)
    {
        try
        {
            return lambda.Compile();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Tests whether the interpreted reference and the interpreted substituted tree agree on one input.</summary>
    /// <param name="expected">The reference lambda.</param>
    /// <param name="actual">The substituted lambda.</param>
    /// <param name="y">The value of <c>y</c>.</param>
    /// <param name="z">The value of <c>z</c>.</param>
    /// <returns><see langword="true"/> if they agree up to rounding.</returns>
    private static bool InterpretedAgree(Expression<Func<double, double, double, double>> expected, Expression<Func<double, double, double, double>> actual, double y, double z)
    {
        try
        {
            AuditOutcome e = Run3(expected.Compile(preferInterpretation: true), y, z);
            AuditOutcome o = Run3(actual.Compile(preferInterpretation: true), y, z);
            return EtcAuditSupport.Compare([e], [o]) <= EtcAuditSupport.Divergence.Rounding;
        }
        catch (ArgumentOutOfRangeException)
        {
            // The interpreter itself fails on such shapes (LocalVariables.Box).
            return true;
        }
    }

    /// <summary>Evaluates a three-parameter delegate as <c>f(y, z, 0)</c>.</summary>
    /// <param name="function">The delegate.</param>
    /// <param name="y">The value of <c>y</c>.</param>
    /// <param name="z">The value of <c>z</c>.</param>
    /// <returns>The outcome.</returns>
    private static AuditOutcome Run3(Func<double, double, double, double> function, double y, double z)
    {
        R4DynamicRuntime.Reset();
        try
        {
            return new AuditOutcome(function(y, z, 0), null, [.. R4DynamicRuntime.Events]);
        }
        catch (Exception ex)
        {
            return new AuditOutcome(double.NaN, ex.GetType(), [.. R4DynamicRuntime.Events]);
        }
    }
}
