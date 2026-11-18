using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Utils.Expressions;

namespace ExpressionSimplifierAudit;

/// <summary>
/// The pass chain of one case: <c>E, S(E), S(S(E)), …</c> up to a fixed point, a cycle, an error or the pass limit.
/// </summary>
/// <param name="Category"><c>fixed@k</c>, <c>cycle:periodN</c>, <c>error@k</c> or <c>no-fixed-point</c> (as in the convergence campaign).</param>
/// <param name="Forms">The source followed by the output of each pass.</param>
/// <param name="Error">The description of the exception of the failing pass, or <see langword="null"/>.</param>
internal sealed record PassChain(string Category, IReadOnlyList<Expression> Forms, string? Error)
{
    /// <summary>Gets whether one pass reaches a fixed point (or the source already was one).</summary>
    public bool IsStableAfterOnePass => Category is "fixed@0" or "fixed@1";

    /// <summary>Gets whether the chain ends in a cycle between distinct forms.</summary>
    public bool IsCycle => Category.StartsWith("cycle", StringComparison.Ordinal);
}

/// <summary>
/// The diagnosis of one case that one pass does not stabilize: the smallest subtree of <c>S(E)</c> that the second pass
/// rewrites, the rule that rewrites it, and the shapes of both sides with the leaves abstracted.
/// </summary>
/// <param name="Rule">The first rule of the second pass whose output differs structurally from its input.</param>
/// <param name="Rules">Every rule of the second pass whose output differs from its input, in order of first application.</param>
/// <param name="Before">The smallest differing subtree of the first-pass output.</param>
/// <param name="After">The corresponding subtree of the second-pass output.</param>
/// <param name="Family">The family key: the rule and the abstracted shapes of the residue.</param>
internal sealed record StabilityDiagnosis(string Rule, string[] Rules, Expression Before, Expression After, string Family);

/// <summary>
/// The tools of the stability campaign (mission C3d.1): pass chains compared structurally (never by hash), residues located by
/// a parallel walk, responsible rules read from a trace, and a reducer that keeps the types and the operators of the tree.
/// </summary>
internal static class StabilityAnalysis
{
    /// <summary>
    /// Simplifies <paramref name="tree"/> repeatedly, comparing each output with the previous ones by strict structural
    /// signature (no simplification, no hash), until a fixed point, a cycle, an error or <paramref name="passes"/> passes.
    /// </summary>
    /// <param name="tree">The source tree.</param>
    /// <param name="simplify">One public simplification pass.</param>
    /// <param name="passes">The maximum number of passes.</param>
    /// <returns>The pass chain.</returns>
    public static PassChain Iterate(Expression tree, Func<Expression, Expression> simplify, int passes = 8)
    {
        var strict = new StrictForm();
        var signatures = new List<string> { strict.Of(tree) };
        var forms = new List<Expression> { tree };
        for (int pass = 1; pass <= passes; pass++)
        {
            Expression result;
            try
            {
                result = simplify(forms[^1]);
            }
            catch (Exception exception)
            {
                return new PassChain("error@" + pass, forms, AuditText.Describe(exception));
            }

            string signature = strict.Of(result);
            int previous = signatures.LastIndexOf(signature);
            signatures.Add(signature);
            forms.Add(result);
            if (previous == signatures.Count - 2) return new PassChain(pass == 1 ? "fixed@0" : "fixed@" + (pass - 1), forms, null);
            if (previous >= 0) return new PassChain("cycle:period" + (signatures.Count - 1 - previous), forms, null);
        }

        return new PassChain("no-fixed-point", forms, null);
    }

    /// <summary>
    /// Diagnoses a chain that one pass does not stabilize: locates the residue between the first and the second pass output
    /// and traces the second pass to name the rule that rewrites it.
    /// </summary>
    /// <param name="chain">A chain with at least two passes.</param>
    /// <param name="simplify">One public simplification pass (run again, traced).</param>
    /// <returns>The diagnosis.</returns>
    public static StabilityDiagnosis Diagnose(PassChain chain, Func<Expression, Expression> simplify)
    {
        Expression first = chain.Forms[1], second = chain.Forms[2];
        (Expression before, Expression after) = Residue(first, second);
        var collector = new ExpressionTraceCollector(400_000, ExpressionTraceRetention.KeepFirst);
        using (ExpressionTransformTracing.Begin(collector))
        {
            try
            {
                simplify(first);
            }
            catch (Exception)
            {
                // The chain recorded the outcome; the trace up to the failure is enough.
            }
        }

        var strict = new StrictForm();
        var rules = new List<string>();
        foreach (ExpressionTransformEvent e in collector.GetEvents())
        {
            if (e.Kind != ExpressionTransformEventKind.RuleApplied || e.Rule is null || e.Input is null || e.Output is null) continue;
            if (ReferenceEquals(e.Input, e.Output) || strict.Of(e.Input) == strict.Of(e.Output)) continue;
            string name = e.Rule.DisplayName;
            if (!rules.Contains(name)) rules.Add(name);
        }

        string rule = rules.Count > 0 ? rules[0] : "(finalization)";
        return new StabilityDiagnosis(rule, rules.ToArray(), before, after, rule + " :: " + Shape(before) + " => " + Shape(after));
    }

    /// <summary>
    /// Walks two trees in parallel while they have the same node and exactly one differing child, and returns the smallest
    /// pair of differing subtrees (the whole trees when they differ at the root or in several children).
    /// </summary>
    /// <param name="a">The first tree.</param>
    /// <param name="b">The second tree.</param>
    /// <returns>The differing subtrees.</returns>
    public static (Expression Before, Expression After) Residue(Expression a, Expression b)
    {
        var strict = new StrictForm();
        while (true)
        {
            Expression[] left = Children(a), right = Children(b);
            if (left.Length == 0 || left.Length != right.Length || !SameNode(a, b)) return (a, b);
            int differing = -1;
            for (int i = 0; i < left.Length; i++)
            {
                if (strict.Of(left[i]) == strict.Of(right[i])) continue;
                if (differing >= 0) return (a, b);
                differing = i;
            }

            if (differing < 0) return (a, b);
            a = left[differing];
            b = right[differing];
        }
    }

    /// <summary>Whether two nodes have the same class, node type, static type and operator method (children excluded).</summary>
    /// <param name="a">The first node.</param>
    /// <param name="b">The second node.</param>
    /// <returns><see langword="true"/> when the nodes only differ by their children.</returns>
    private static bool SameNode(Expression a, Expression b)
        => a.NodeType == b.NodeType && a.Type == b.Type && a.GetType() == b.GetType() && (a, b) switch
        {
            (BinaryExpression x, BinaryExpression y) => x.Method == y.Method && x.IsLiftedToNull == y.IsLiftedToNull,
            (UnaryExpression x, UnaryExpression y) => x.Method == y.Method,
            (MethodCallExpression x, MethodCallExpression y) => x.Method == y.Method,
            _ => true,
        };

    /// <summary>The children of the node classes the residue walk and the reducer descend into.</summary>
    /// <param name="e">The node.</param>
    /// <returns>Its children (empty for a leaf or a class they do not descend into).</returns>
    private static Expression[] Children(Expression e) => e switch
    {
        BinaryExpression b => [b.Left, b.Right],
        UnaryExpression u => [u.Operand],
        MethodCallExpression { Object: null } c => [.. c.Arguments],
        ConditionalExpression c => [c.Test, c.IfTrue, c.IfFalse],
        _ => [],
    };

    /// <summary>
    /// The shape of a subtree with its leaves abstracted: parameters named <c>a</c>, <c>b</c>, … by first occurrence, the
    /// constants 0, 1, -1 and 2 kept, the other constants printed <c>c</c>, subtrees deeper than four levels printed <c>…</c>.
    /// </summary>
    /// <param name="e">The subtree.</param>
    /// <returns>The shape.</returns>
    public static string Shape(Expression e)
    {
        var names = new Dictionary<ParameterExpression, string>(ReferenceEqualityComparer.Instance);
        var builder = new StringBuilder();
        Write(e, 0);
        return builder.ToString();

        void Write(Expression node, int depth)
        {
            if (depth > 4)
            {
                builder.Append('…');
                return;
            }

            switch (node)
            {
                case ParameterExpression p:
                    if (!names.TryGetValue(p, out string? name)) names[p] = name = ((char)('a' + Math.Min(names.Count, 25))).ToString();
                    builder.Append(name);
                    break;
                case ConstantExpression c:
                    builder.Append(ConstantShape(c));
                    break;
                case BinaryExpression b:
                    builder.Append('(');
                    Write(b.Left, depth + 1);
                    builder.Append(' ').Append(Operator(b.NodeType)).Append(b.Method is null ? "" : "'").Append(' ');
                    Write(b.Right, depth + 1);
                    builder.Append(')');
                    break;
                case UnaryExpression { NodeType: ExpressionType.Negate } u:
                    builder.Append('-');
                    Write(u.Operand, depth + 1);
                    break;
                case UnaryExpression u:
                    builder.Append(u.NodeType).Append('(');
                    Write(u.Operand, depth + 1);
                    builder.Append(')');
                    break;
                case MethodCallExpression call:
                    builder.Append(call.Method.Name).Append('(');
                    for (int i = 0; i < call.Arguments.Count; i++)
                    {
                        if (i > 0) builder.Append(", ");
                        Write(call.Arguments[i], depth + 1);
                    }

                    builder.Append(')');
                    break;
                default:
                    builder.Append(node.NodeType).Append('?');
                    break;
            }
        }
    }

    /// <summary>The abstracted text of a constant.</summary>
    /// <param name="c">The constant.</param>
    /// <returns>0, 1, -1 or 2 for those values, NaN or ∞ for the non-finite values, <c>c</c> otherwise.</returns>
    private static string ConstantShape(ConstantExpression c)
    {
        if (c.Value is null || c.Value is not IConvertible || c.Value is bool or string or char) return "c";
        double value;
        try
        {
            value = Convert.ToDouble(c.Value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return "c";
        }

        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "∞" : "-∞";
        return value is 0 or 1 or -1 or 2 ? value.ToString(CultureInfo.InvariantCulture) : "c";
    }

    /// <summary>The symbol of an arithmetic node type.</summary>
    /// <param name="type">The node type.</param>
    /// <returns>The symbol.</returns>
    private static string Operator(ExpressionType type) => type switch
    {
        ExpressionType.Add or ExpressionType.AddChecked => "+",
        ExpressionType.Subtract or ExpressionType.SubtractChecked => "-",
        ExpressionType.Multiply or ExpressionType.MultiplyChecked => "*",
        ExpressionType.Divide => "/",
        ExpressionType.Power => "^",
        ExpressionType.Modulo => "%",
        _ => type.ToString(),
    };

    /// <summary>
    /// Reduces a tree while <paramref name="holds"/> keeps holding: repeatedly replaces a node by one of its children of the
    /// same type, by a parameter of the same type taken from the tree, or by the constants 0, 1 and 2 of its type, largest
    /// nodes first. Every candidate is rebuilt by <see cref="ExpressionVisitor"/>, so the factory methods validate the types and
    /// the operators; a candidate they reject is skipped. The tree keeps its nature: no node is replaced by a node of another
    /// type, and no operator is changed.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="holds">The property to preserve.</param>
    /// <param name="maxAttempts">The maximum number of property evaluations.</param>
    /// <returns>The reduced tree.</returns>
    public static Expression Reduce(Expression tree, Func<Expression, bool> holds, int maxAttempts = 300)
    {
        int attempts = 0;
        bool progress = true;
        while (progress && attempts < maxAttempts)
        {
            progress = false;
            List<ParameterExpression> parameters = FreeParameters(tree);
            foreach (Expression node in Nodes(tree).OrderByDescending(AuditText.Count).ToList())
            {
                foreach (Expression replacement in Replacements(node, parameters))
                {
                    if (++attempts > maxAttempts) return tree;
                    Expression candidate;
                    try
                    {
                        candidate = new NodeReplacer(node, replacement).Visit(tree)!;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    bool holdsForCandidate;
                    try
                    {
                        holdsForCandidate = AuditText.Count(candidate) < AuditText.Count(tree) && holds(candidate);
                    }
                    catch (Exception)
                    {
                        holdsForCandidate = false;
                    }

                    if (holdsForCandidate)
                    {
                        tree = candidate;
                        progress = true;
                        break;
                    }
                }

                if (progress) break;
            }
        }

        return tree;
    }

    /// <summary>The distinct nodes of a tree, by reference.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The nodes.</returns>
    private static IEnumerable<Expression> Nodes(Expression tree)
    {
        var seen = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<Expression>();
        stack.Push(tree);
        while (stack.Count > 0)
        {
            Expression e = stack.Pop();
            if (!seen.Add(e)) continue;
            yield return e;
            foreach (Expression child in Children(e)) stack.Push(child);
        }
    }

    /// <summary>The parameters of a tree, in order of first occurrence.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The parameters.</returns>
    private static List<ParameterExpression> FreeParameters(Expression tree)
    {
        var result = new List<ParameterExpression>();
        foreach (Expression node in Nodes(tree))
        {
            if (node is ParameterExpression p && !result.Contains(p)) result.Add(p);
        }

        return result;
    }

    /// <summary>The candidate replacements of a node, all of its type.</summary>
    /// <param name="node">The node.</param>
    /// <param name="parameters">The parameters of the tree.</param>
    /// <returns>The replacements.</returns>
    private static IEnumerable<Expression> Replacements(Expression node, List<ParameterExpression> parameters)
    {
        foreach (Expression child in Children(node))
        {
            if (child.Type == node.Type) yield return child;
        }

        foreach (ParameterExpression p in parameters.Where(p => p.Type == node.Type && !ReferenceEquals(p, node)).Take(2))
        {
            yield return p;
        }

        if (node is ConstantExpression) yield break;
        Type type = Nullable.GetUnderlyingType(node.Type) ?? node.Type;
        if (!type.IsPrimitive && type != typeof(decimal)) yield break;
        foreach (int value in new[] { 1, 2, 0 })
        {
            yield return Expression.Constant(Convert.ChangeType(value, type, CultureInfo.InvariantCulture), node.Type);
        }
    }

    /// <summary>Replaces every occurrence of one node by another.</summary>
    /// <param name="target">The node to replace.</param>
    /// <param name="replacement">Its replacement.</param>
    private sealed class NodeReplacer(Expression target, Expression replacement) : ExpressionVisitor
    {
        /// <inheritdoc/>
        public override Expression? Visit(Expression? node) => ReferenceEquals(node, target) ? replacement : base.Visit(node);
    }
}

/// <summary>The stability campaign (mission C3d.1).</summary>
internal static partial class Campaigns
{
    /// <summary>
    /// Stability campaign (mission C3d.1): the cases of the convergence campaign; every case that one pass does not stabilize is
    /// diagnosed (residue, responsible rule, family) and the first <see cref="CampaignOptions.Reduce"/> cases of each family are
    /// reduced while they keep the same category kind (cycle, or not stable after one pass) and the same responsible rule.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="writer">The results.</param>
    /// <returns>The number of cycles.</returns>
    public static int Stability(CampaignOptions options, ResultWriter writer)
    {
        static Expression Pass(Expression e)
        {
            (Expression? result, Exception? error) = Simplification.Run(e);
            if (error is not null) throw error;
            return result!;
        }

        // S5_AUDIT_BIND=1: every case is wrapped in a lambda binding its free parameters, in order of first occurrence, so that
        // the free-parameter ties of the S4 order (documented limitation L-3) are told apart from the cycles of bound trees.
        IEnumerable<(string Label, Expression Tree)> cases = ConvergenceCases(options);
        if (Environment.GetEnvironmentVariable("S5_AUDIT_BIND") == "1")
        {
            cases = cases.Select(static c => (c.Label, (Expression)Expression.Lambda(c.Tree, FreeParameterCollector.Collect(c.Tree))));
        }

        return RunStability(cases, Pass, options, writer);
    }

    /// <summary>Collects the free parameters of a tree in order of first occurrence (parameters declared by a lambda, a block or a catch are bound).</summary>
    private sealed class FreeParameterCollector : ExpressionVisitor
    {
        /// <summary>The free parameters found, in order.</summary>
        private readonly List<ParameterExpression> _free = [];

        /// <summary>The parameters declared by the enclosing binders.</summary>
        private readonly HashSet<ParameterExpression> _bound = [];

        /// <summary>Returns the free parameters of <paramref name="tree"/>.</summary>
        /// <param name="tree">The tree.</param>
        /// <returns>The free parameters, in order of first occurrence.</returns>
        public static List<ParameterExpression> Collect(Expression tree)
        {
            var collector = new FreeParameterCollector();
            collector.Visit(tree);
            return collector._free;
        }

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_bound.Contains(node) && !_free.Contains(node)) _free.Add(node);
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            foreach (ParameterExpression parameter in node.Parameters) _bound.Add(parameter);
            return base.VisitLambda(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitBlock(BlockExpression node)
        {
            foreach (ParameterExpression variable in node.Variables) _bound.Add(variable);
            return base.VisitBlock(node);
        }

        /// <inheritdoc/>
        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            if (node.Variable is not null) _bound.Add(node.Variable);
            return base.VisitCatchBlock(node);
        }
    }

    /// <summary>
    /// The body of the stability campaign over any case source and any simplification (reused by external harnesses for other
    /// corpora, policies and derived simplifiers).
    /// </summary>
    /// <param name="cases">The labelled cases.</param>
    /// <param name="pass">One public simplification pass.</param>
    /// <param name="options">The options (start, maximum number of cases, passes, reductions per family).</param>
    /// <param name="writer">The results.</param>
    /// <returns>The number of cycles.</returns>
    public static int RunStability(IEnumerable<(string Label, Expression Tree)> cases, Func<Expression, Expression> pass, CampaignOptions options, ResultWriter writer)
    {
        var categories = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var families = new Dictionary<string, (int Count, int Cycles, string Example)>(StringComparer.Ordinal);
        var rules = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var reducedPerFamily = new Dictionary<string, int>(StringComparer.Ordinal);
        int index = -1, cycles = 0;
        foreach ((string label, Expression tree) in cases)
        {
            if (++index < options.Start) continue;
            if (index >= options.MaxCases) break;
            Worker.BeginCase("stability#" + index + " " + label);
            PassChain chain = StabilityAnalysis.Iterate(tree, pass, options.Passes);
            categories[chain.Category] = categories.TryGetValue(chain.Category, out int n) ? n + 1 : 1;
            if (chain.IsStableAfterOnePass || chain.Error is not null && chain.Forms.Count < 3)
            {
                Worker.EndCase();
                continue;
            }

            if (chain.IsCycle) cycles++;
            StabilityDiagnosis diagnosis = StabilityAnalysis.Diagnose(chain, pass);
            rules[diagnosis.Rule] = rules.TryGetValue(diagnosis.Rule, out int r) ? r + 1 : 1;
            string? reduced = null, reducedFirst = null, reducedSecond = null, reducedFamily = null;
            int done = reducedPerFamily.TryGetValue(diagnosis.Family, out int d) ? d : 0;
            if (done < options.Reduce)
            {
                reducedPerFamily[diagnosis.Family] = done + 1;
                bool cycle = chain.IsCycle;
                Expression small = StabilityAnalysis.Reduce(tree, candidate =>
                {
                    PassChain c = StabilityAnalysis.Iterate(candidate, pass, options.Passes);
                    if (c.Error is not null || c.IsStableAfterOnePass || c.IsCycle != cycle) return false;
                    return StabilityAnalysis.Diagnose(c, pass).Rule == diagnosis.Rule;
                });
                PassChain smallChain = StabilityAnalysis.Iterate(small, pass, options.Passes);
                reduced = AuditText.Format(small);
                if (smallChain.Forms.Count > 2)
                {
                    reducedFirst = AuditText.Format(smallChain.Forms[1]);
                    reducedSecond = AuditText.Format(smallChain.Forms[2]);
                    reducedFamily = StabilityAnalysis.Diagnose(smallChain, pass).Family;
                }
            }

            string key = reducedFamily ?? diagnosis.Family;
            families.TryGetValue(key, out var entry);
            families[key] = (entry.Count + 1, entry.Cycles + (chain.IsCycle ? 1 : 0), entry.Example ?? reduced ?? AuditText.Format(tree));
            writer.Anomaly(new
            {
                index, label, category = chain.Category,
                sizes = chain.Forms.Select(AuditText.Count).ToArray(),
                rule = diagnosis.Rule, rules = diagnosis.Rules, family = key,
                residueBefore = AuditText.Format(diagnosis.Before), residueAfter = AuditText.Format(diagnosis.After),
                first = AuditText.Format(chain.Forms[1]), second = AuditText.Format(chain.Forms[2]),
                reduced, reducedFirst, reducedSecond,
            });
            Worker.EndCase();
        }

        var summary = new StringBuilder("# Stability campaign (mission C3d.1)\n\n");
        summary.Append("Cases: ").Append(index + 1).Append(", at most ").Append(options.Passes).Append(" passes, strict structural comparison (no hash).\n\n| Category | Cases |\n|---|---:|\n");
        foreach (KeyValuePair<string, int> pair in categories) summary.Append("| ").Append(pair.Key).Append(" | ").Append(pair.Value).Append(" |\n");
        summary.Append("\nFirst rule of the second pass that changes its input (cases not stable after one pass):\n\n| Rule | Cases |\n|---|---:|\n");
        foreach (KeyValuePair<string, int> pair in rules.OrderByDescending(p => p.Value)) summary.Append("| ").Append(pair.Key).Append(" | ").Append(pair.Value).Append(" |\n");
        summary.Append("\nFamilies (rule :: residue of the first pass => second pass, leaves abstracted), with a reduced example:\n\n| Family | Cases | Cycles | Example |\n|---|---:|---:|---|\n");
        foreach (KeyValuePair<string, (int Count, int Cycles, string Example)> pair in families.OrderByDescending(p => p.Value.Count))
        {
            summary.Append("| `").Append(pair.Key.Replace("|", "\\|")).Append("` | ").Append(pair.Value.Count).Append(" | ").Append(pair.Value.Cycles)
                .Append(" | `").Append(pair.Value.Example.Replace("|", "\\|")).Append("` |\n");
        }

        writer.Summary(summary.ToString());
        return cycles;
    }
}
