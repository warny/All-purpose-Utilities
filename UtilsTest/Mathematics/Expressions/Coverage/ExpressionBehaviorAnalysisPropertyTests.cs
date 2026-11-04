using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// S5 P18 Architecture A: properties of <see cref="ExpressionBehaviorAnalysis"/> on the seeded trees of the ETC closure audit
/// generator (every family but extension nodes). Soundness: every guarantee the analysis gives (no exception, no side
/// effect, determinism) holds when the compiled tree runs. Differential: where the classification disagrees with the strict
/// gate of <see cref="ExpressionEffectAnalysis"/>, the cause is one of the documented refinements.
/// </summary>
[TestClass]
[TestCategory("S5-P18A")]
public class ExpressionBehaviorAnalysisPropertyTests
{
    /// <summary>The number of generated trees per property.</summary>
    private const int TreeCount = 1500;

    /// <summary>The families of the generated trees.</summary>
    private const AuditFamilies Families = AuditFamilies.All & ~AuditFamilies.Extension;

    /// <summary>
    /// The guarantees hold at run time: a tree classified without exception never throws on the audit inputs, a tree
    /// classified free of side effects records no traced effect, and a deterministic tree free of side effects evaluates to
    /// the same outcomes twice. Each guarantee is given on a substantial part of the corpus, so the property is not vacuous.
    /// </summary>
    [TestMethod]
    public void Guarantees_HoldWhenTheTreeRuns()
    {
        int compiled = 0, noThrow = 0, effectFree = 0, reproducible = 0;
        var failures = new List<string>();
        for (int seed = 0; seed < TreeCount; seed++)
        {
            var generator = new EtcAuditGenerator(seed, 5, Families);
            Expression<Func<double, double, double>> lambda = EtcAuditGenerator.Wrap(generator.Generate());
            Func<double, double, double>? function = EtcAuditSupport.TryCompile(lambda, out _);
            if (function is null) continue;

            compiled++;
            ExpressionBehavior behavior = ExpressionBehaviorAnalysis.AnalyzeInvocation(lambda);
            Assert.IsFalse(behavior.ReliesOnDeclarations, "the generator declares nothing");
            AuditOutcome[] first = EtcAuditSupport.Evaluate(function);

            if (!behavior.MayThrow)
            {
                noThrow++;
                if (first.Any(outcome => outcome.Exception is not null)) failures.Add($"seed {seed}: classified without exception, throws: {Describe(behavior, lambda, first)}");
            }

            if (behavior.IsSideEffectFree)
            {
                effectFree++;
                if (first.Any(outcome => outcome.Events.Length > 0)) failures.Add($"seed {seed}: classified free of side effects, traced effects: {Describe(behavior, lambda, first)}");
            }

            if (behavior.IsDeterministic && behavior.IsSideEffectFree)
            {
                reproducible++;
                AuditOutcome[] second = EtcAuditSupport.Evaluate(function);
                for (int i = 0; i < first.Length; i++)
                {
                    bool same = first[i].Exception == second[i].Exception
                        && BitConverter.DoubleToInt64Bits(first[i].Value) == BitConverter.DoubleToInt64Bits(second[i].Value);
                    if (!same) failures.Add($"seed {seed}: classified deterministic, input {i} gave {first[i]} then {second[i]}");
                }
            }
        }

        Console.WriteLine($"compiled {compiled}, no exception {noThrow}, side-effect free {effectFree}, reproducible {reproducible}");
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(20)));
        Assert.IsTrue(compiled > TreeCount / 2, $"compiled {compiled}");
        Assert.IsTrue(noThrow > compiled / 10 && effectFree > compiled / 10 && reproducible > compiled / 10,
            $"compiled {compiled}, no exception {noThrow}, side-effect free {effectFree}, reproducible {reproducible}");
    }

    /// <summary>Describes a counterexample.</summary>
    /// <param name="behavior">The classification.</param>
    /// <param name="lambda">The tree.</param>
    /// <param name="outcomes">The outcomes.</param>
    /// <returns>A description.</returns>
    private static string Describe(ExpressionBehavior behavior, LambdaExpression lambda, AuditOutcome[] outcomes)
        => $"{behavior.Flags}{Environment.NewLine}{EtcAuditSupport.Print(lambda)}{Environment.NewLine}{string.Join(" | ", outcomes.Select(outcome => outcome.ToString()))}";

    /// <summary>
    /// Compares, node by node, whether the strict gate (<see cref="ExpressionEffectAnalysis.IsInert"/>) and the verified
    /// permissions of the new classification allow every rewrite. Every minimal disagreement (a node that disagrees while no
    /// descendant does) is caused by one of the documented refinements: the new classification is stricter only on created
    /// quoted trees (an allocation); it is more permissive only on checked floating-point arithmetic (which never throws),
    /// trusted numeric functions beyond the gate's allow-list, lambdas invoked in place and blocks whose effects stay local.
    /// </summary>
    [TestMethod]
    public void Disagreements_WithTheStrictGate_AreDocumentedRefinements()
    {
        var stricter = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var permissive = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int seed = 0; seed < TreeCount; seed++)
        {
            var generator = new EtcAuditGenerator(seed, 5, Families);
            Expression body = generator.Generate();
            List<Expression> nodes = EtcAuditSupport.Nodes(body);
            var disagreeing = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
            var newInert = new Dictionary<Expression, bool>(ReferenceEqualityComparer.Instance);
            foreach (Expression node in nodes)
            {
                if (newInert.ContainsKey(node)) continue;
                bool gate = ExpressionEffectAnalysis.IsInert(ExpressionEffectAnalysis.Analyze(node));
                ExpressionBehavior behavior = ExpressionBehaviorAnalysis.Analyze(node);
                bool inert = ExpressionBehaviorPermissions.CanDiscard(behavior, ExpressionTrustPolicy.Verified)
                    && ExpressionBehaviorPermissions.CanRepeat(behavior, ExpressionTrustPolicy.Verified)
                    && ExpressionBehaviorPermissions.CanReorder(behavior, ExpressionTrustPolicy.Verified);
                newInert[node] = inert;
                if (gate != inert) disagreeing.Add(node);
            }

            foreach (Expression node in disagreeing)
            {
                if (EtcAuditSupport.Nodes(node).Skip(1).Any(disagreeing.Contains)) continue;
                SortedDictionary<string, int> bucket = newInert[node] ? permissive : stricter;
                string key = Signature(node);
                bucket[key] = bucket.TryGetValue(key, out int count) ? count + 1 : 1;
            }
        }

        string report = $"stricter: {Format(stricter)}{Environment.NewLine}more permissive: {Format(permissive)}";
        Console.WriteLine(report);
        foreach (string key in stricter.Keys) Assert.IsTrue(IsDocumentedStricter(key), $"undocumented stricter disagreement {key}{Environment.NewLine}{report}");
        foreach (string key in permissive.Keys) Assert.IsTrue(IsDocumentedPermissive(key), $"undocumented permissive disagreement {key}{Environment.NewLine}{report}");
    }

    /// <summary>Formats disagreement counts.</summary>
    /// <param name="counts">The counts.</param>
    /// <returns>A one-line summary.</returns>
    private static string Format(SortedDictionary<string, int> counts) => string.Join(", ", counts.Select(pair => $"{pair.Key} x{pair.Value}"));

    /// <summary>Describes the operator of a node: its node type, and its method when it has one.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The signature.</returns>
    private static string Signature(Expression node)
    {
        if (Utils.Expressions.ExpressionOpacity.IsOpaque(node)) return "Extension";
        string method = node switch
        {
            MethodCallExpression call => call.Method.DeclaringType!.Name + "." + call.Method.Name,
            BinaryExpression { Method: { } binaryMethod } => binaryMethod.DeclaringType!.Name + "." + binaryMethod.Name,
            UnaryExpression { Method: { } unaryMethod } => unaryMethod.DeclaringType!.Name + "." + unaryMethod.Name,
            _ => "",
        };
        string type = node is UnaryExpression or BinaryExpression ? ":" + node.Type.Name : "";
        return method.Length == 0 ? node.NodeType + type : node.NodeType + type + "(" + method + ")";
    }

    /// <summary>Whether a stricter disagreement is documented.</summary>
    /// <param name="key">The node signature.</param>
    /// <returns><see langword="true"/> for a documented refinement.</returns>
    private static bool IsDocumentedStricter(string key) => key.StartsWith("Quote", StringComparison.Ordinal);

    /// <summary>Whether a more permissive disagreement is documented.</summary>
    /// <param name="key">The node signature.</param>
    /// <returns><see langword="true"/> for a documented refinement.</returns>
    private static bool IsDocumentedPermissive(string key)
        => key is "AddChecked:Double" or "SubtractChecked:Double" or "MultiplyChecked:Double" or "NegateChecked:Double"
                or "Invoke" or "Block"
            || key.StartsWith("Call(Math.", StringComparison.Ordinal)
            || key.StartsWith("Call(Double.", StringComparison.Ordinal);
}
