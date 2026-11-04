using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace ExpressionSimplifierAudit;

/// <summary>The parameters of a campaign run.</summary>
/// <param name="Seeds">The number of generated seeds (or corpus cases, depending on the campaign).</param>
/// <param name="Start">The first case or seed to run (to resume after a watchdog abort).</param>
/// <param name="MaxDepth">The maximum depth of generated trees.</param>
/// <param name="Nested">The number of second-level corpus compositions.</param>
/// <param name="Types">The numeric domains of the typed campaigns.</param>
/// <param name="Passes">The maximum number of public passes of the convergence campaign.</param>
/// <param name="Assignments">The number of drawn parameter assignments per differential seed.</param>
/// <param name="Reduce">The maximum number of counterexamples reduced per campaign.</param>
/// <param name="MaxCases">The maximum number of corpus cases (to shorten a run).</param>
internal sealed record CampaignOptions(int Seeds, int Start, int MaxDepth, int Nested, string[] Types, int Passes, int Assignments, int Reduce, int MaxCases);

/// <summary>The audit campaigns (brief §4, §5, §7, §9, §11).</summary>
internal static partial class Campaigns
{
    /// <summary>The classes of the termination campaign that are anomalies.</summary>
    private static readonly HashSet<string> TerminationAnomalies = ["cycle", "expansion", "suspect", "exception", "excessive"];

    /// <summary>
    /// Termination campaign (brief §4): every corpus instantiation and nested composition is simplified under the default
    /// limits with a counting tracer; a refusal, an exception or an excessive visit count is re-run with a trace collector and
    /// classified with <see cref="ExpressionTraceCycleAnalyzer"/>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="writer">The results.</param>
    /// <param name="edges">Receives the dynamic rule-interaction edges.</param>
    /// <returns>The number of anomalies.</returns>
    public static int Terminate(CampaignOptions options, ResultWriter writer, Dictionary<string, long> edges)
    {
        var tracer = new CountingTracer();
        var classes = new SortedDictionary<string, int>();
        var signatures = new Dictionary<string, int>();
        int index = -1, reduced = 0;
        long maxVisits = 0;
        string maxVisitsCase = "";
        foreach ((string label, Expression tree) in Corpus.Instantiate(options.Nested, 2026))
        {
            if (++index < options.Start) continue;
            if (index >= options.MaxCases) break;
            Worker.BeginCase("terminate#" + index + " " + label);
            long visits0 = tracer.Visits, rules0 = tracer.RuleApplications;
            (Expression? result, Exception? error) = Simplification.Run(tree, null, tracer);
            long visits = tracer.Visits - visits0, rules = tracer.RuleApplications - rules0;
            int depth = tracer.MaxDepth;
            tracer.ResetOperationState();
            int nodes = AuditText.Count(tree);
            string cls = error is null
                ? visits > Math.Max(20_000, 500L * nodes) ? "excessive" : "normal"
                : AuditText.Innermost(error) is ExpressionResourceLimitException limit ? "refusal:" + limit.Kind : "exception";
            string? analysis = null;
            if (cls != "normal")
            {
                (string refined, string text) = AnalyzeTrace(tree);
                analysis = text;
                if (refined != "none") cls = refined;
            }

            if (visits > maxVisits)
            {
                maxVisits = visits;
                maxVisitsCase = label;
            }

            classes[cls] = classes.TryGetValue(cls, out int count) ? count + 1 : 1;
            var record = new
            {
                index, label, cls, nodes, visits, rules, depth,
                outputNodes = result is null ? -1 : AuditText.Count(result),
                error = error is null ? null : AuditText.Describe(error),
                result = result is null ? null : AuditText.Format(result),
            };
            writer.Record(record);
            if (TerminationAnomalies.Contains(cls) || cls.StartsWith("refusal", StringComparison.Ordinal))
            {
                string signature = cls + "|" + (error is null ? "" : AuditText.Describe(error) + "|" + Site(error));
                signatures[signature] = signatures.TryGetValue(signature, out int s) ? s + 1 : 1;
                string? minimal = null;
                if (signatures[signature] == 1 && reduced++ < options.Reduce)
                {
                    string expected = cls;
                    Expression small = Reducer.Reduce(tree, NumericDomain.All["double"], candidate => TerminationClass(candidate) == expected);
                    minimal = AuditText.Format(small);
                }

                writer.Anomaly(new { record.index, record.label, cls, signature, record.visits, record.depth, record.error, site = error is null ? null : Site(error), minimal, analysis });
            }

            Worker.EndCase();
        }

        foreach (KeyValuePair<string, long> edge in tracer.Edges) edges[edge.Key] = edges.TryGetValue(edge.Key, out long n) ? n + edge.Value : edge.Value;
        var summary = new StringBuilder("# Termination campaign\n\n");
        summary.Append("Cases: ").Append(index + 1).Append(" (corpus instantiations and ").Append(options.Nested).Append(" nested compositions)\n\n| Class | Cases |\n|---|---:|\n");
        foreach (KeyValuePair<string, int> pair in classes) summary.Append("| ").Append(pair.Key).Append(" | ").Append(pair.Value).Append(" |\n");
        summary.Append("\nLargest visit count: ").Append(maxVisits).Append(" (`").Append(maxVisitsCase).Append("`)\n\n| Anomaly signature | Cases |\n|---|---:|\n");
        foreach (KeyValuePair<string, int> pair in signatures.OrderByDescending(p => p.Value)) summary.Append("| `").Append(pair.Key).Append("` | ").Append(pair.Value).Append(" |\n");
        summary.Append("\nRule applications over the campaign:\n\n| Rule | Applications | Re-entries requested |\n|---|---:|---:|\n");
        foreach (KeyValuePair<string, long> pair in tracer.Applied.OrderByDescending(p => p.Value))
        {
            summary.Append("| ").Append(pair.Key).Append(" | ").Append(pair.Value).Append(" | ").Append(tracer.Reentries.TryGetValue(pair.Key, out long r) ? r : 0).Append(" |\n");
        }

        writer.Summary(summary.ToString());
        return writer.AnomalyCount;
    }

    /// <summary>Classifies a tree for the reducer of the termination campaign (without tracing).</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The class.</returns>
    private static string TerminationClass(Expression tree)
    {
        var tracer = new CountingTracer();
        (_, Exception? error) = Simplification.Run(tree, null, tracer);
        if (error is null) return tracer.Visits > Math.Max(20_000, 500L * AuditText.Count(tree)) ? "excessive" : "normal";
        if (AuditText.Innermost(error) is ExpressionResourceLimitException) return AnalyzeTrace(tree).Class is var c && c != "none" ? c : "refusal";
        return "exception";
    }

    /// <summary>Returns the method that threw an exception (the innermost frame of the innermost exception).</summary>
    /// <param name="error">The exception.</param>
    /// <returns>The site.</returns>
    private static string Site(Exception error)
    {
        Exception inner = AuditText.Innermost(error);
        var trace = new System.Diagnostics.StackTrace(inner, false);
        System.Diagnostics.StackFrame? frame = trace.GetFrames().FirstOrDefault(f => f.GetMethod()?.DeclaringType?.Namespace?.StartsWith("Utils", StringComparison.Ordinal) == true);
        MethodBase? method = frame?.GetMethod() ?? trace.GetFrame(0)?.GetMethod();
        return method is null ? "?" : method.DeclaringType?.Name + "." + method.Name;
    }

    /// <summary>Traces a simplification with a bounded collector and classifies it with the cycle analyzer.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The refined class (<c>cycle</c>, <c>expansion</c>, <c>suspect</c> or <c>none</c>) and the analyzer report.</returns>
    private static (string Class, string Text) AnalyzeTrace(Expression tree)
    {
        var collector = new ExpressionTraceCollector(300_000, ExpressionTraceRetention.KeepFirst);
        Simplification.Run(tree, null, collector);
        ExpressionTransformEvent[] events = collector.GetEvents();
        ExpressionTraceCycleReport report = ExpressionTraceCycleAnalyzer.Analyze(events);
        string text = ExpressionTraceCycleAnalyzer.ToText(report);
        if (text.Length > 4000) text = text[..4000] + "...";
        string cls = report.Periods.Any(p => p.Kind == ExpressionTraceLoopKind.StrictCycle) ? "cycle"
            : report.Periods.Any(p => p.Kind == ExpressionTraceLoopKind.Expansion) ? "expansion"
            : report.HasRecurrence ? "suspect"
            : "none";
        return (cls, "events=" + collector.TotalEvents + " maxDepth=" + report.MaximumDepth + "\n" + text);
    }

    /// <summary>
    /// Convergence campaign (brief §5): <c>E(k+1) = Simplify(E(k))</c> for at most <see cref="CampaignOptions.Passes"/> passes,
    /// compared with the strict structural signature (no simplification during the comparison). Reports the pass at which a
    /// fixed point is reached, cycles between forms, growth, the residue shapes of the first pass, and the rules the second
    /// pass applies.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="writer">The results.</param>
    /// <returns>The number of anomalies (cycles between forms and absence of a fixed point).</returns>
    public static int Converge(CampaignOptions options, ResultWriter writer)
    {
        var categories = new SortedDictionary<string, int>();
        var tagsWhenNotIdempotent = new SortedDictionary<string, int>();
        var secondPassRules = new SortedDictionary<string, long>();
        var byTemplate = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
        int index = -1;
        foreach ((string label, Expression tree) in ConvergenceCases(options))
        {
            if (++index < options.Start) continue;
            if (index >= options.MaxCases) break;
            Worker.BeginCase("converge#" + index + " " + label);
            var strict = new StrictForm();
            var forms = new List<string> { strict.Of(tree) };
            var sizes = new List<int> { AuditText.Count(tree) };
            Expression current = tree;
            string category = "no-fixed-point";
            string? error = null;
            Expression? first = null;
            CountingTracer? secondPass = null;
            for (int pass = 1; pass <= options.Passes; pass++)
            {
                CountingTracer? tracer = pass == 2 ? secondPass = new CountingTracer() : null;
                (Expression? result, Exception? failure) = Simplification.Run(current, null, tracer);
                if (failure is not null)
                {
                    error = AuditText.Describe(failure);
                    category = "error@" + pass;
                    break;
                }

                first ??= result;
                string form = strict.Of(result!);
                sizes.Add(AuditText.Count(result!));
                int previous = forms.LastIndexOf(form);
                forms.Add(form);
                current = result!;
                if (previous == forms.Count - 2)
                {
                    category = pass == 1 ? "fixed@0" : "fixed@" + (pass - 1);
                    break;
                }

                if (previous >= 0)
                {
                    category = "cycle:period" + (forms.Count - 1 - previous);
                    break;
                }
            }

            if (category == "no-fixed-point" && sizes.Count >= 3 && sizes[^1] > sizes[^2] && sizes[^2] > sizes[^3]) category = "no-fixed-point:growing";
            categories[category] = categories.TryGetValue(category, out int c) ? c + 1 : 1;
            string template = label.Split(" | ")[0];
            if (!byTemplate.TryGetValue(template, out int[]? row)) byTemplate[template] = row = new int[4];
            row[category == "fixed@0" || category == "fixed@1" ? 0 : category == "fixed@2" ? 1 : category.StartsWith("fixed", StringComparison.Ordinal) ? 2 : 3]++;
            string[] tags = [];
            if (first is not null && category != "fixed@0" && category != "fixed@1")
            {
                tags = ResidueTags(first).ToArray();
                foreach (string tag in tags) tagsWhenNotIdempotent[tag] = tagsWhenNotIdempotent.TryGetValue(tag, out int t) ? t + 1 : 1;
                if (secondPass is not null)
                {
                    foreach (KeyValuePair<string, long> pair in secondPass.Applied) secondPassRules[pair.Key] = secondPassRules.TryGetValue(pair.Key, out long r) ? r + pair.Value : pair.Value;
                }
            }

            var record = new
            {
                index, label, category, sizes, tags, error,
                first = first is null ? null : AuditText.Format(first),
                last = AuditText.Format(current),
                secondPassRules = secondPass?.Applied.Keys.ToArray(),
            };
            writer.Record(record);
            if (category.StartsWith("cycle", StringComparison.Ordinal) || category.StartsWith("no-fixed-point", StringComparison.Ordinal) || category.StartsWith("error", StringComparison.Ordinal))
            {
                writer.Anomaly(record);
            }

            Worker.EndCase();
        }

        var summary = new StringBuilder("# Convergence campaign\n\n");
        summary.Append("Cases: ").Append(index + 1).Append(", at most ").Append(options.Passes).Append(" public passes, strict structural comparison.\n\n");
        summary.Append("`fixed@k`: the k-th pass output is a fixed point (`fixed@0`: the source already was; `fixed@1`: one pass is idempotent on these inputs).\n\n| Category | Cases |\n|---|---:|\n");
        foreach (KeyValuePair<string, int> pair in categories) summary.Append("| ").Append(pair.Key).Append(" | ").Append(pair.Value).Append(" |\n");
        summary.Append("\nResidue shapes in the first-pass output of the cases that are not a fixed point after one pass:\n\n| Residue | Cases |\n|---|---:|\n");
        foreach (KeyValuePair<string, int> pair in tagsWhenNotIdempotent.OrderByDescending(p => p.Value)) summary.Append("| ").Append(pair.Key).Append(" | ").Append(pair.Value).Append(" |\n");
        summary.Append("\nRules applied by the second pass of those cases (what the first pass left to do):\n\n| Rule | Applications |\n|---|---:|\n");
        foreach (KeyValuePair<string, long> pair in secondPassRules.OrderByDescending(p => p.Value)) summary.Append("| ").Append(pair.Key).Append(" | ").Append(pair.Value).Append(" |\n");
        summary.Append("\nPer template (corpus level 1 and generated): one pass suffices / two passes / more / no fixed point or cycle or error:\n\n| Template | 1 | 2 | >2 | none |\n|---|---:|---:|---:|---:|\n");
        foreach (KeyValuePair<string, int[]> pair in byTemplate)
        {
            if (pair.Key.StartsWith("nested#", StringComparison.Ordinal) || pair.Key.StartsWith("gen#", StringComparison.Ordinal)) continue;
            summary.Append("| `").Append(pair.Key).Append("` | ").Append(string.Join(" | ", pair.Value)).Append(" |\n");
        }

        writer.Summary(summary.ToString());
        return writer.AnomalyCount;
    }

    /// <summary>The cases of the convergence campaign: the corpus, the families at small sizes and generated trees.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The labelled cases.</returns>
    private static IEnumerable<(string Label, Expression Tree)> ConvergenceCases(CampaignOptions options)
    {
        foreach ((string label, Expression tree) in Corpus.Instantiate(options.Nested, 2027)) yield return (label, tree);
        foreach ((string name, _) in Families.Catalog)
        {
            foreach (int n in new[] { 2, 3, 4, 6, 8 }) yield return ("family:" + name + " | n=" + n, Families.Build(name, n));
        }

        NumericDomain domain = NumericDomain.All["double"];
        for (int seed = 0; seed < options.Seeds; seed++)
        {
            yield return ("gen#" + seed, new TreeGenerator(seed, domain, new GeneratorOptions { MaxDepth = 2 + seed % (options.MaxDepth - 1) }).Build());
        }
    }

    /// <summary>Detects the shapes a later pass rewrites (incomplete finalization) in a simplified tree.</summary>
    /// <param name="tree">The first-pass output.</param>
    /// <returns>The distinct residue tags.</returns>
    private static IEnumerable<string> ResidueTags(Expression tree)
    {
        var tags = new HashSet<string>();
        Visit(tree);
        return tags;

        void Visit(Expression? e)
        {
            switch (e)
            {
                case BinaryExpression b:
                    bool lc = b.Left is ConstantExpression, rc = b.Right is ConstantExpression;
                    if (lc && rc && b.NodeType is ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply or ExpressionType.Divide or ExpressionType.Power)
                        tags.Add(b.NodeType == ExpressionType.Subtract && IsValue(b.Left, 0) ? "0 - c (negative constant group)" : "unfolded constant pair " + b.NodeType);
                    if (b.NodeType == ExpressionType.Multiply && (IsValue(b.Left, 1) || IsValue(b.Right, 1) || IsValue(b.Left, -1) || IsValue(b.Right, -1)) && !(lc && rc)) tags.Add("* ±1");
                    if (b.NodeType == ExpressionType.Divide && (IsValue(b.Right, 1) || IsValue(b.Right, -1)) && !lc) tags.Add("/ ±1");
                    if (b.NodeType is ExpressionType.Add && (IsValue(b.Left, 0) || IsValue(b.Right, 0)) && !(lc && rc)) tags.Add("+ 0");
                    if (b.NodeType is ExpressionType.Divide && (b.Left.NodeType == ExpressionType.Divide || b.Right.NodeType == ExpressionType.Divide)) tags.Add("nested division");
                    if (b.NodeType is ExpressionType.Multiply && rc && !lc) tags.Add("constant on the right of *");
                    if (b.NodeType is ExpressionType.Multiply && lc && b.Right is BinaryExpression { NodeType: ExpressionType.Multiply, Left: ConstantExpression }) tags.Add("c1 * (c2 * t)");
                    if (b.NodeType is ExpressionType.Subtract && b.Right is UnaryExpression { NodeType: ExpressionType.Negate }) tags.Add("t - (-u)");
                    if (b.NodeType is ExpressionType.Subtract && b.Right is BinaryExpression { NodeType: ExpressionType.Subtract or ExpressionType.Add } && !lc) tags.Add("t - (sum)");
                    if (b.NodeType is ExpressionType.Add or ExpressionType.Subtract && b.Left is UnaryExpression { NodeType: ExpressionType.Negate }) tags.Add("(-u) ± t");
                    Visit(b.Left);
                    Visit(b.Right);
                    break;
                case UnaryExpression u:
                    if (u.NodeType == ExpressionType.Negate && u.Operand is ConstantExpression) tags.Add("-(c)");
                    if (u.NodeType == ExpressionType.Negate && u.Operand is UnaryExpression { NodeType: ExpressionType.Negate }) tags.Add("-(-t)");
                    if (u.NodeType == ExpressionType.Negate && u.Operand is BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Subtract }) tags.Add("-(sum)");
                    if (u.NodeType == ExpressionType.Negate && u.Operand is BinaryExpression { NodeType: ExpressionType.Multiply or ExpressionType.Divide }) tags.Add("-(product)");
                    Visit(u.Operand);
                    break;
                case MethodCallExpression call:
                    foreach (Expression argument in call.Arguments) Visit(argument);
                    break;
                case LambdaExpression lambda:
                    Visit(lambda.Body);
                    break;
            }
        }
    }

    /// <summary>Whether a node is a numeric constant equal to <paramref name="value"/>.</summary>
    /// <param name="e">The node.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    private static bool IsValue(Expression e, double value)
        => e is ConstantExpression { Value: { } v } && v is double or float or int or long or decimal or short or uint or ulong
            && Convert.ToDouble(v, CultureInfo.InvariantCulture) == value;

    /// <summary>
    /// Canonicalization campaign (brief §7): every permutation and association of a set of terms, combined additively,
    /// multiplicatively and with signs, free or bound by a lambda, is simplified; the campaign reports how many distinct strict
    /// forms each set produces, whether the public comparer and its hash agree on them, and whether each form is stable.
    /// </summary>
    /// <param name="writer">The results.</param>
    /// <returns>The number of sets whose forms the comparer does not identify (anomalies).</returns>
    public static int Canonicalize(ResultWriter writer)
    {
        var summary = new StringBuilder("# Canonicalization campaign\n\n| Term set | Combination | Binding | Trees | Distinct forms | Comparer-unequal pairs | Hash mismatches | Unstable forms | Example forms |\n|---|---|---|---:|---:|---:|---:|---:|---|\n");
        foreach ((string name, Func<ParameterExpression[], Expression[]> make) in CanonicalSets())
        {
            foreach (string combination in new[] { "sum", "product", "signed" })
            {
                foreach (bool bound in new[] { false, true })
                {
                    Worker.BeginCase("canon " + name + " " + combination + " " + bound);
                    ParameterExpression[] ps = new[] { "x", "y", "z" }.Select(n => Expression.Parameter(typeof(double), n)).ToArray();
                    Expression[] terms = make(ps);
                    var trees = new List<Expression>();
                    foreach (int[] permutation in Permutations(terms.Length))
                    {
                        Expression[] ordered = permutation.Select(i => terms[i]).ToArray();
                        foreach (bool left in new[] { true, false })
                        {
                            Expression body = Combine(ordered, permutation, combination, left);
                            trees.Add(bound ? Expression.Lambda<Func<double, double, double, double>>(body, ps) : body);
                        }
                    }

                    var strict = new StrictForm();
                    var simplified = new List<Expression>();
                    var forms = new List<string>();
                    int unstable = 0;
                    string? error = null;
                    foreach (Expression tree in trees)
                    {
                        (Expression? result, Exception? failure) = Simplification.Run(tree);
                        if (failure is not null)
                        {
                            error = AuditText.Describe(failure);
                            continue;
                        }

                        simplified.Add(result!);
                        string form = strict.Of(result!);
                        forms.Add(form);
                        (Expression? again, _) = Simplification.Run(result!);
                        if (again is null || strict.Of(again) != form) unstable++;
                    }

                    int unequal = 0, hashMismatch = 0;
                    for (int i = 0; i < simplified.Count; i++)
                    {
                        for (int j = i + 1; j < simplified.Count; j++)
                        {
                            bool equal = ExpressionComparer.Default.Equals(simplified[i], simplified[j]);
                            if (!equal) unequal++;
                            else if (ExpressionComparer.Default.GetHashCode(simplified[i]) != ExpressionComparer.Default.GetHashCode(simplified[j])) hashMismatch++;
                        }
                    }

                    List<string> distinct = forms.Distinct().ToList();
                    string examples = string.Join(" ; ", simplified.GroupBy(s => strict.Of(s)).Take(3).Select(g => "`" + AuditText.Format(g.First()).Replace("|", "\\|") + "`"));
                    var record = new { set = name, combination, bound, trees = trees.Count, distinct = distinct.Count, unequal, hashMismatch, unstable, error, examples };
                    writer.Record(record);
                    if (unequal > 0 || hashMismatch > 0 || error is not null) writer.Anomaly(record);
                    summary.Append("| ").Append(name).Append(" | ").Append(combination).Append(" | ").Append(bound ? "lambda" : "free").Append(" | ").Append(trees.Count)
                        .Append(" | ").Append(distinct.Count).Append(" | ").Append(unequal).Append(" | ").Append(hashMismatch).Append(" | ").Append(unstable).Append(" | ").Append(error ?? examples).Append(" |\n");
                    Worker.EndCase();
                }
            }
        }

        writer.Summary(summary.ToString());
        return writer.AnomalyCount;
    }

    /// <summary>The term sets of the canonicalization campaign.</summary>
    /// <returns>The named term factories.</returns>
    private static IEnumerable<(string Name, Func<ParameterExpression[], Expression[]> Make)> CanonicalSets()
    {
        MethodInfo sin = typeof(double).GetMethod("Sin", [typeof(double)])!;
        MethodInfo custom = typeof(Families).GetMethod(nameof(Families.CustomMultiply))!;
        MethodInfo next = typeof(EffectLeaves).GetMethod(nameof(EffectLeaves.Next))!;
        object opaque = new CountingOpaque(99);
        yield return ("{x, y, z}", p => [p[0], p[1], p[2]]);
        yield return ("{2x, 3y, z}", p => [Expression.Multiply(Expression.Constant(2.0), p[0]), Expression.Multiply(Expression.Constant(3.0), p[1]), p[2]]);
        yield return ("{x, sin(y), 2}", p => [p[0], Expression.Call(sin, p[1]), Expression.Constant(2.0)]);
        yield return ("{x, carrier(opaque), 3y}", p => [p[0], Families.Carrier(opaque), Expression.Multiply(Expression.Constant(3.0), p[1])]);
        yield return ("{x*y, y*z, 2}", p => [Expression.Multiply(p[0], p[1]), Expression.Multiply(p[1], p[2]), Expression.Constant(2.0)]);
        yield return ("{x, custom(2,y), z}", p => [p[0], Expression.Multiply(Expression.Constant(2.0), p[1], custom), p[2]]);
        yield return ("{x, Next(), y}", p => [p[0], Expression.Call(next), p[1]]);
        yield return ("{x, x, y}", p => [p[0], p[0], p[1]]);
    }

    /// <summary>Combines ordered terms into a left- or right-associated sum, product or signed sum.</summary>
    /// <param name="terms">The terms, in order.</param>
    /// <param name="permutation">The original index of each term (the signed sum negates the term of original index 1).</param>
    /// <param name="combination"><c>sum</c>, <c>product</c> or <c>signed</c>.</param>
    /// <param name="left">Whether the chain is left-associated.</param>
    /// <returns>The tree.</returns>
    private static Expression Combine(Expression[] terms, int[] permutation, string combination, bool left)
    {
        bool Negative(int position) => combination == "signed" && permutation[position] == 1;
        Expression Signed(int position) => Negative(position) ? Expression.Negate(terms[position]) : terms[position];
        Func<Expression, Expression, Expression> op = combination == "product" ? Expression.Multiply : Expression.Add;
        if (left)
        {
            Expression acc = Signed(0);
            for (int i = 1; i < terms.Length; i++) acc = combination == "signed" && Negative(i) ? Expression.Subtract(acc, terms[i]) : op(acc, terms[i]);
            return acc;
        }

        Expression right = Signed(terms.Length - 1);
        for (int i = terms.Length - 2; i >= 0; i--) right = op(Signed(i), right);
        return right;
    }

    /// <summary>Enumerates the permutations of <c>0..n-1</c>.</summary>
    /// <param name="n">The size.</param>
    /// <returns>The permutations.</returns>
    private static IEnumerable<int[]> Permutations(int n)
    {
        if (n == 1)
        {
            yield return [0];
            yield break;
        }

        foreach (int[] smaller in Permutations(n - 1))
        {
            for (int position = 0; position <= smaller.Length; position++)
            {
                var result = new List<int>(smaller);
                result.Insert(position, n - 1);
                yield return result.ToArray();
            }
        }
    }

    /// <summary>
    /// Comparer and canonical-order invariants (brief §7): reflexivity on rebuilt copies, symmetry, transitivity, hash
    /// consistency, alpha-equivalence, agreement of the comparison of sources and of simplified forms, and the consistency of
    /// the raw structural equality with the S4 canonical order (antisymmetry, ties, transitivity).
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="writer">The results.</param>
    /// <returns>The number of invariant violations.</returns>
    public static int ComparerInvariants(CampaignOptions options, ResultWriter writer)
    {
        var violations = new SortedDictionary<string, int>();
        var checks = new SortedDictionary<string, int>();
        void Check(string invariant, bool holds, Func<object> details)
        {
            checks[invariant] = checks.TryGetValue(invariant, out int c) ? c + 1 : 1;
            if (holds) return;
            violations[invariant] = violations.TryGetValue(invariant, out int v) ? v + 1 : 1;
            if (violations[invariant] <= 20) writer.Anomaly(new { invariant, details = details() });
        }

        ExpressionComparer comparer = ExpressionComparer.Default;
        foreach (string typeName in options.Types)
        {
            NumericDomain domain = NumericDomain.All[typeName];
            for (int seed = options.Start; seed < options.Seeds; seed++)
            {
                Worker.BeginCase("comparer " + typeName + " #" + seed);
                Expression e = new TreeGenerator(seed, domain, new GeneratorOptions { MaxDepth = 3, HardWeight = 2 }).Build();
                Expression copy = new DeepCopy().Visit(e)!;
                (Expression? s, Exception? failure) = Simplification.Run(e);
                if (failure is not null)
                {
                    Worker.EndCase();
                    continue;
                }

                Expression other = new TreeGenerator(seed + 1_000_003, domain, new GeneratorOptions { MaxDepth = 3, HardWeight = 2 }).Build();
                if (Simplification.Run(other).Error is not null)
                {
                    // The auxiliary tree itself does not simplify (an eager constant-folding exception, see the differential campaign).
                    other = copy;
                }

                Expression commuted = e is BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Multiply } b ? Expression.MakeBinary(b.NodeType, b.Right, b.Left) : copy;
                Expression[] pool = [e, copy, s!, commuted, other];
                try
                {
                    Check("reflexive (rebuilt copy)", comparer.Equals(e, copy), () => new { typeName, seed, e = AuditText.Format(e) });
                    for (int i = 0; i < pool.Length; i++)
                    {
                        for (int j = 0; j < pool.Length; j++)
                        {
                            if (i == j) continue;
                            bool ij = comparer.Equals(pool[i], pool[j]);
                            Check("symmetric", ij == comparer.Equals(pool[j], pool[i]), () => new { typeName, seed, a = AuditText.Format(pool[i]), b = AuditText.Format(pool[j]) });
                            if (ij) Check("hash consistent", comparer.GetHashCode(pool[i]) == comparer.GetHashCode(pool[j]), () => new { typeName, seed, a = AuditText.Format(pool[i]), b = AuditText.Format(pool[j]) });
                            for (int k = 0; k < pool.Length; k++)
                            {
                                if (k == i || k == j || !ij || !comparer.Equals(pool[j], pool[k])) continue;
                                Check("transitive", comparer.Equals(pool[i], pool[k]), () => new { typeName, seed, a = AuditText.Format(pool[i]), b = AuditText.Format(pool[j]), c = AuditText.Format(pool[k]) });
                            }
                        }
                    }

                    (Expression? so, _) = Simplification.Run(other);
                    if (so is not null)
                    {
                        Check("Equals(a,b) == Equals(S(a),S(b))", comparer.Equals(e, other) == comparer.Equals(s!, so), () => new { typeName, seed, a = AuditText.Format(e), b = AuditText.Format(other) });
                    }

                    ParameterExpression p1 = Expression.Parameter(domain.Type, "p"), p2 = Expression.Parameter(domain.Type, "q");
                    Expression l1 = Expression.Lambda(new Substitute(domain.Parameters[0], p1).Visit(e)!, p1);
                    Expression l2 = Expression.Lambda(new Substitute(domain.Parameters[0], p2).Visit(e)!, p2);
                    Check("alpha-equivalent lambdas", comparer.Equals(l1, l2), () => new { typeName, seed, e = AuditText.Format(e) });

                    if (EngineInternals.HasStructural)
                    {
                        Expression[] keys = [s!, so ?? s!, copy, e, other];
                        for (int i = 0; i < keys.Length; i++)
                        {
                            for (int j = 0; j < keys.Length; j++)
                            {
                                int cij = EngineInternals.CanonicalOrder(keys[i], keys[j]);
                                Check("canonical order antisymmetric", cij == -EngineInternals.CanonicalOrder(keys[j], keys[i]), () => new { typeName, seed, a = AuditText.Format(keys[i]), b = AuditText.Format(keys[j]) });
                                if (EngineInternals.StructuralEqualsRaw(keys[i], keys[j]))
                                {
                                    Check("raw equal => canonical tie", cij == 0, () => new { typeName, seed, a = AuditText.Format(keys[i]), b = AuditText.Format(keys[j]) });
                                    Check("raw equal => raw hash equal", EngineInternals.StructuralHashRaw(keys[i]) == EngineInternals.StructuralHashRaw(keys[j]), () => new { typeName, seed, a = AuditText.Format(keys[i]) });
                                }

                                for (int k = 0; k < keys.Length; k++)
                                {
                                    int cjk = EngineInternals.CanonicalOrder(keys[j], keys[k]);
                                    if (cij < 0 && cjk < 0) Check("canonical order transitive (<)", EngineInternals.CanonicalOrder(keys[i], keys[k]) < 0, () => new { typeName, seed, a = AuditText.Format(keys[i]), b = AuditText.Format(keys[j]), c = AuditText.Format(keys[k]) });
                                    if (cij == 0 && cjk == 0) Check("canonical ties transitive", EngineInternals.CanonicalOrder(keys[i], keys[k]) == 0, () => new { typeName, seed, a = AuditText.Format(keys[i]), b = AuditText.Format(keys[j]), c = AuditText.Format(keys[k]) });
                                }
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    Check("no exception", false, () => new { typeName, seed, error = AuditText.Describe(exception), e = AuditText.Format(e) });
                }

                Worker.EndCase();
            }
        }

        foreach ((string name, Expression a, Expression b, bool expected) in ScopeCases())
        {
            Check("scope case: " + name, comparer.Equals(a, b) == expected, () => new { name, a = AuditText.Format(a), b = AuditText.Format(b), expected });
        }

        var summary = new StringBuilder("# Comparer and canonical-order invariants\n\n| Invariant | Checks | Violations |\n|---|---:|---:|\n");
        foreach (KeyValuePair<string, int> pair in checks) summary.Append("| ").Append(pair.Key).Append(" | ").Append(pair.Value).Append(" | ").Append(violations.TryGetValue(pair.Key, out int v) ? v : 0).Append(" |\n");
        writer.Summary(summary.ToString());
        writer.Record(new { checks, violations });
        return writer.AnomalyCount;
    }

    /// <summary>Curated scope cases: alpha-equivalence, shadowing, captured variables and block variables.</summary>
    /// <returns>The cases with their expected comparer answer.</returns>
    private static IEnumerable<(string Name, Expression A, Expression B, bool Expected)> ScopeCases()
    {
        ParameterExpression a = Expression.Parameter(typeof(double), "a"), b = Expression.Parameter(typeof(double), "b");
        ParameterExpression c = Expression.Parameter(typeof(double), "c"), d = Expression.Parameter(typeof(double), "d");
        ParameterExpression free = Expression.Parameter(typeof(double), "f"), free2 = Expression.Parameter(typeof(double), "f");
        yield return ("nested alpha", Expression.Lambda(Expression.Lambda(Expression.Add(a, b), b), a), Expression.Lambda(Expression.Lambda(Expression.Add(c, d), d), c), true);
        yield return ("nested commuted binders (a + b)", Expression.Lambda(Expression.Lambda(Expression.Add(a, b), b), a), Expression.Lambda(Expression.Lambda(Expression.Add(d, c), d), c), true);
        yield return ("nested swapped binders (a - b)", Expression.Lambda(Expression.Lambda(Expression.Subtract(a, b), b), a), Expression.Lambda(Expression.Lambda(Expression.Subtract(d, c), d), c), false);
        yield return ("shadowing", Expression.Lambda(Expression.Lambda(a, a), a), Expression.Lambda(Expression.Lambda(c, d), c), false);
        yield return ("captured same free", Expression.Lambda(Expression.Add(a, free), a), Expression.Lambda(Expression.Add(c, free), c), true);
        yield return ("captured distinct free, same name", Expression.Lambda(Expression.Add(a, free), a), Expression.Lambda(Expression.Add(c, free2), c), false);
        yield return ("commuted bound sum", Expression.Lambda(Expression.Add(a, b), a, b), Expression.Lambda(Expression.Add(d, c), c, d), true);
        ParameterExpression v1 = Expression.Variable(typeof(double), "v"), v2 = Expression.Variable(typeof(double), "w");
        yield return ("block alpha", Expression.Block([v1], Expression.Assign(v1, a), v1), Expression.Block([v2], Expression.Assign(v2, a), v2), true);
        yield return ("int 1 vs double 1.0 constant (documented exact cross-type numeric model)", Expression.Constant(1), Expression.Constant(1.0), true);
    }

    /// <summary>Rebuilds every node of a tree (distinct instances, same structure, same parameters and constants).</summary>
    private sealed class DeepCopy : ExpressionVisitor
    {
        /// <inheritdoc/>
        protected override Expression VisitBinary(BinaryExpression node)
            => Expression.MakeBinary(node.NodeType, Visit(node.Left)!, Visit(node.Right)!, node.IsLiftedToNull, node.Method, node.Conversion);

        /// <inheritdoc/>
        protected override Expression VisitUnary(UnaryExpression node)
            => Expression.MakeUnary(node.NodeType, Visit(node.Operand)!, node.Type, node.Method);

        /// <inheritdoc/>
        protected override Expression VisitConstant(ConstantExpression node) => Expression.Constant(node.Value, node.Type);

        /// <inheritdoc/>
        protected override Expression VisitMethodCall(MethodCallExpression node)
            => Expression.Call(node.Object is null ? null : Visit(node.Object), node.Method, node.Arguments.Select(a => Visit(a)!));
    }

    /// <summary>Substitutes one parameter by another.</summary>
    /// <param name="from">The parameter to replace.</param>
    /// <param name="to">Its replacement.</param>
    private sealed class Substitute(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
