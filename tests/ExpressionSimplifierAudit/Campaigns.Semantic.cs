using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Utils.Expressions;

namespace ExpressionSimplifierAudit;

/// <summary>The semantic campaigns: differential evaluation, observable effects and the S3 identity matrix.</summary>
internal static partial class Campaigns
{
    /// <summary>The differential classes that are anomalies.</summary>
    private static readonly HashSet<string> DifferentialAnomalies = ["VALUE", "EXC-ADDED", "EXC-REMOVED", "EXC-CHANGED", "SIMPLIFY-THROWS", "COMPILE-FAIL", "TYPE-CHANGED"];

    /// <summary>
    /// Differential campaign (brief §8 and §11): seeded trees over several numeric types are simplified, both trees are compiled
    /// and evaluated on moderate and special values, and every difference is classified (S3 latitude, extreme inputs, or a
    /// candidate defect). A candidate is cross-checked with the expression interpreter and reduced.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="writer">The results.</param>
    /// <returns>The number of anomalies.</returns>
    public static int Differential(CampaignOptions options, ResultWriter writer)
    {
        var table = new SortedDictionary<string, SortedDictionary<string, int>>();
        var signatures = new Dictionary<string, int>();
        int reduced = 0;
        for (int seed = options.Start; seed < options.Seeds; seed++)
        {
            string typeName = options.Types[seed % options.Types.Length];
            NumericDomain domain = NumericDomain.All[typeName];
            var generatorOptions = new GeneratorOptions { MaxDepth = 2 + seed % (options.MaxDepth - 1) };
            Expression body = new TreeGenerator(seed, domain, generatorOptions).Build();
            Worker.BeginCase("diff#" + seed + " " + typeName);
            (string worst, Dictionary<string, int> counts, string? detail, Expression? simplified) = Judge(body, domain, seed, options.Assignments);
            if (!table.TryGetValue(typeName, out SortedDictionary<string, int>? row)) table[typeName] = row = new SortedDictionary<string, int>();
            row[worst] = row.TryGetValue(worst, out int n) ? n + 1 : 1;
            var record = new { seed, type = typeName, worst, counts, detail, source = AuditText.Format(body), simplified = simplified is null ? null : AuditText.Format(simplified) };
            writer.Record(record);
            if (DifferentialAnomalies.Contains(worst))
            {
                string signature = typeName + "|" + worst + "|" + (detail ?? "");
                signatures[signature] = signatures.TryGetValue(signature, out int s) ? s + 1 : 1;
                string? minimal = null, minimalSimplified = null;
                if (signatures[signature] == 1 && reduced++ < options.Reduce)
                {
                    string expectedWorst = worst;
                    Expression small = Reducer.Reduce(body, domain, candidate => Judge(candidate, domain, seed, options.Assignments).Worst == expectedWorst);
                    minimal = AuditText.Format(small);
                    minimalSimplified = Judge(small, domain, seed, options.Assignments).Simplified is { } ms ? AuditText.Format(ms) : null;
                }

                writer.Anomaly(new { seed, type = typeName, worst, signature, detail, source = record.source, simplified = record.simplified, minimal, minimalSimplified, generator = generatorOptions });
            }

            Worker.EndCase();
        }

        var summary = new StringBuilder("# Differential campaign\n\n");
        summary.Append("Seeds ").Append(options.Start).Append("..").Append(options.Seeds - 1).Append(", types ").Append(string.Join(", ", options.Types)).Append(", ")
            .Append(options.Assignments + 2).Append(" assignments per seed. Worst class per seed:\n\n");
        string[] columns = Differential_Classes().Concat(["SIMPLIFY-THROWS-LIKE-SOURCE", "SIMPLIFY-THROWS", "COMPILE-FAIL", "TYPE-CHANGED", "LINQ", "refusal"]).ToArray();
        summary.Append("| Type | ").Append(string.Join(" | ", columns)).Append(" |\n|---|").Append(string.Concat(columns.Select(_ => "---:|"))).Append('\n');
        foreach (KeyValuePair<string, SortedDictionary<string, int>> row in table)
        {
            summary.Append("| ").Append(row.Key).Append(" | ").Append(string.Join(" | ", columns.Select(c => row.Value.Where(p => p.Key == c || (c == "refusal" && p.Key.StartsWith(c, StringComparison.Ordinal))).Sum(p => p.Value)))).Append(" |\n");
        }

        summary.Append("\n| Anomaly signature | Seeds |\n|---|---:|\n");
        foreach (KeyValuePair<string, int> pair in signatures.OrderByDescending(p => p.Value)) summary.Append("| `").Append(pair.Key.Replace("|", "\\|")).Append("` | ").Append(pair.Value).Append(" |\n");
        writer.Summary(summary.ToString());
        return writer.AnomalyCount;
    }

    /// <summary>The value classes of <see cref="DiffEval"/>.</summary>
    /// <returns>The classes.</returns>
    private static IEnumerable<string> Differential_Classes() => DiffEval.Classes;

    /// <summary>Simplifies, compiles and evaluates one tree against its simplified form.</summary>
    /// <param name="body">The source body over the domain parameters.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="seed">The seed of the value assignments.</param>
    /// <param name="assignments">The number of drawn assignments.</param>
    /// <returns>The worst class, the class counts, a detail and the simplified tree.</returns>
    private static (string Worst, Dictionary<string, int> Counts, string? Detail, Expression? Simplified) Judge(Expression body, NumericDomain domain, int seed, int assignments)
    {
        var counts = new Dictionary<string, int>();
        (Expression? simplified, Exception? error) = Simplification.Run(body);
        if (error is not null)
        {
            if (AuditText.Innermost(error) is ExpressionResourceLimitException limit) return ("refusal:" + limit.Kind, counts, null, null);
            string description = AuditText.Describe(error) + " at " + Site(error);
            try
            {
                // A simplification that throws what every evaluation of the source throws follows the documented design (a constant
                // zero divisor throws as evaluating the node would); otherwise the exception is moved to simplification time.
                Delegate eager = DiffEval.Compile(body, domain);
                List<object?[]> all = DiffEval.Assignments(domain, seed, assignments);
                int throwing = all.Count(a => DiffEval.Evaluate(eager, a).Exception is not null);
                return (throwing == all.Count ? "SIMPLIFY-THROWS-LIKE-SOURCE" : "SIMPLIFY-THROWS", counts, description + " (source throws on " + throwing + "/" + all.Count + " assignments)", null);
            }
            catch (Exception)
            {
                return ("SIMPLIFY-THROWS", counts, description, null);
            }
        }

        if (simplified!.Type != body.Type) return ("TYPE-CHANGED", counts, body.Type.Name + " -> " + simplified.Type.Name, simplified);
        Delegate source, target;
        try
        {
            source = DiffEval.Compile(body, domain);
        }
        catch (Exception exception)
        {
            return ("same", counts, "source does not compile: " + exception.GetType().Name, simplified);
        }

        try
        {
            target = DiffEval.Compile(simplified, domain);
        }
        catch (Exception exception)
        {
            return ("COMPILE-FAIL", counts, exception.GetType().Name + ": " + exception.Message, simplified);
        }

        string worst = "same";
        string? detail = null;
        bool extreme = DiffEval.HasExtremeConstant(body);
        foreach (object?[] arguments in DiffEval.Assignments(domain, seed, assignments))
        {
            DiffEval.Outcome a = DiffEval.Evaluate(source, arguments);
            DiffEval.Outcome b = DiffEval.Evaluate(target, arguments);
            string cls = DiffEval.Classify(a, b, arguments, extreme);
            if (DifferentialAnomalies.Contains(cls))
            {
                // The LINQ compiler has known defects (ETC closure audit): the interpreter decides whether the trees really differ.
                DiffEval.Outcome ia = DiffEval.Evaluate(DiffEval.Compile(body, domain, interpret: true), arguments);
                DiffEval.Outcome ib = DiffEval.Evaluate(DiffEval.Compile(simplified, domain, interpret: true), arguments);
                if (DiffEval.Classify(ia, ib, arguments, extreme) == "same") cls = "LINQ";
            }

            counts[cls] = counts.TryGetValue(cls, out int n) ? n + 1 : 1;
            if (cls != "LINQ" && DiffEval.Worst(worst, cls) == cls && cls != worst)
            {
                worst = cls;
                detail = "(" + string.Join(", ", arguments.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "null")) + "): "
                    + (a.Exception ?? Convert.ToString(a.Value, CultureInfo.InvariantCulture)) + " vs " + (b.Exception ?? Convert.ToString(b.Value, CultureInfo.InvariantCulture));
            }
            else if (cls == "LINQ" && worst == "same")
            {
                worst = "LINQ";
            }
        }

        return (worst, counts, detail, simplified);
    }

    /// <summary>
    /// Observable-effect campaign (brief §9): trees with effectful leaves (a counter, a constant call, a getter with an effect,
    /// a throwing call) are evaluated before and after simplification; the value, the exception and the ordered evaluation log
    /// must be identical. Then fixed scenarios exercise the user code the comparer may run while simplifying (throwing,
    /// unstable and logging <see cref="object.Equals(object?)"/>).
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="writer">The results.</param>
    /// <returns>The number of anomalies.</returns>
    public static int Effects(CampaignOptions options, ResultWriter writer)
    {
        NumericDomain domain = NumericDomain.All["double"];
        var counts = new SortedDictionary<string, int>();
        int rewritten = 0;
        for (int seed = options.Start; seed < options.Seeds; seed++)
        {
            Worker.BeginCase("effects#" + seed);
            Expression body = new TreeGenerator(seed, domain, new GeneratorOptions { MaxDepth = 2 + seed % (options.MaxDepth - 1), Effects = true }).Build();
            (Expression? simplified, Exception? error) = Simplification.Run(body);
            string cls;
            string? detail = null;
            if (error is not null)
            {
                cls = AuditText.Innermost(error) is ExpressionResourceLimitException ? "refusal" : "SIMPLIFY-THROWS";
                detail = AuditText.Describe(error);
            }
            else
            {
                if (new StrictForm().Of(body) != new StrictForm().Of(simplified!)) rewritten++;
                cls = "same";
                Delegate source = DiffEval.Compile(body, domain), target = DiffEval.Compile(simplified!, domain);
                foreach (object?[] arguments in DiffEval.Assignments(domain, seed, 2).Take(3))
                {
                    EffectLeaves.Reset();
                    DiffEval.Outcome a = DiffEval.Evaluate(source, arguments);
                    string logA = string.Join(",", EffectLeaves.Log);
                    EffectLeaves.Reset();
                    DiffEval.Outcome b = DiffEval.Evaluate(target, arguments);
                    string logB = string.Join(",", EffectLeaves.Log);
                    string valueClass = DiffEval.Classify(a, b, arguments, DiffEval.HasExtremeConstant(body));
                    if (logA != logB)
                    {
                        cls = "EFFECTS-DIFFER";
                        detail = logA + " | " + logB;
                        break;
                    }

                    if (DifferentialAnomalies.Contains(valueClass))
                    {
                        cls = valueClass;
                        detail = (a.Exception ?? Convert.ToString(a.Value, CultureInfo.InvariantCulture)) + " vs " + (b.Exception ?? Convert.ToString(b.Value, CultureInfo.InvariantCulture));
                        break;
                    }
                }
            }

            counts[cls] = counts.TryGetValue(cls, out int n) ? n + 1 : 1;
            var record = new { seed, cls, detail, source = AuditText.Format(body), simplified = simplified is null ? null : AuditText.Format(simplified) };
            writer.Record(record);
            if (cls != "same" && cls != "refusal") writer.Anomaly(record);
            Worker.EndCase();
        }

        var scenarios = UserCodeScenarios().ToList();
        foreach (var scenario in scenarios)
        {
            writer.Record(scenario);
            if (!scenario.Expected) writer.Anomaly(scenario);
        }

        var summary = new StringBuilder("# Observable-effect campaign\n\n");
        summary.Append("Seeds ").Append(options.Start).Append("..").Append(options.Seeds - 1).Append(" (").Append(rewritten).Append(" trees rewritten by the simplifier).\n\n| Class | Seeds |\n|---|---:|\n");
        foreach (KeyValuePair<string, int> pair in counts) summary.Append("| ").Append(pair.Key).Append(" | ").Append(pair.Value).Append(" |\n");
        summary.Append("\nUser code run by the simplification itself:\n\n| Scenario | Observed | As expected |\n|---|---|---|\n");
        foreach (var scenario in scenarios) summary.Append("| ").Append(scenario.Name).Append(" | ").Append(scenario.Observed).Append(" | ").Append(scenario.Expected ? "yes" : "**no**").Append(" |\n");
        writer.Summary(summary.ToString());
        return writer.AnomalyCount;
    }

    /// <summary>A user-code scenario result.</summary>
    /// <param name="Name">The scenario.</param>
    /// <param name="Observed">What was observed.</param>
    /// <param name="Expected">Whether it matches the documented contract.</param>
    private sealed record ScenarioResult(string Name, string Observed, bool Expected);

    /// <summary>The fixed scenarios of user code run while simplifying (comparer calls into opaque constant values).</summary>
    /// <returns>The results.</returns>
    private static IEnumerable<ScenarioResult> UserCodeScenarios()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression Chain(object a, object b) => Expression.Subtract(
            Expression.Multiply(Expression.Constant(2.0), Families.Carrier(a)),
            Expression.Multiply(Expression.Constant(2.0), Families.Carrier(b)));

        (Expression? r1, Exception? e1) = Simplification.Run(Chain(new ThrowingEquals(), new ThrowingEquals()));
        yield return new ScenarioResult("throwing Equals propagates", e1 is null ? "no exception: " + AuditText.Format(r1) : AuditText.Describe(e1), e1 is not null && AuditText.Innermost(e1) is ThrowingEqualsException);

        var unstable = new UnstableEquals();
        (Expression? r2, Exception? e2) = Simplification.Run(Chain(unstable, new UnstableEquals()));
        yield return new ScenarioResult("unstable Equals terminates", e2 is null ? AuditText.Format(r2) : AuditText.Describe(e2), e2 is null);

        var log = new List<string>();
        Expression logged = Chain(new LoggingEquals("a", log), new LoggingEquals("b", log));
        Simplification.Run(logged);
        string first = string.Join(",", log);
        log.Clear();
        Simplification.Run(logged);
        string second = string.Join(",", log);
        yield return new ScenarioResult("user calls deterministic across calls", first + " / " + second, first == second);

        log.Clear();
        var same = new LoggingEquals("s", log);
        (Expression? r4, _) = Simplification.Run(Chain(same, same));
        yield return new ScenarioResult("same opaque value cancels (2c - 2c)", AuditText.Format(r4) + " calls=" + string.Join(",", log), r4 is ConstantExpression);
    }

    /// <summary>An opaque value whose <see cref="Equals(object?)"/> throws.</summary>
    private sealed class ThrowingEquals
    {
        /// <inheritdoc/>
        public override bool Equals(object? obj) => throw new ThrowingEqualsException();

        /// <inheritdoc/>
        public override int GetHashCode() => 1;
    }

    /// <summary>The exception of <see cref="ThrowingEquals"/>.</summary>
    private sealed class ThrowingEqualsException : Exception;

    /// <summary>An opaque value whose <see cref="Equals(object?)"/> alternates its answer.</summary>
    private sealed class UnstableEquals
    {
        /// <summary>The number of calls.</summary>
        private static int s_calls;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => (++s_calls & 1) == 0;

        /// <inheritdoc/>
        public override int GetHashCode() => s_calls;
    }

    /// <summary>An opaque value whose equality is by reference and whose calls are logged.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="log">The log.</param>
    private sealed class LoggingEquals(string id, List<string> log)
    {
        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            log.Add(id + "=" + ((obj as LoggingEquals)?.Id ?? "?"));
            return ReferenceEquals(this, obj);
        }

        /// <summary>Gets the identifier.</summary>
        private string Id => id;

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            log.Add(id + "#");
            return 3;
        }
    }

    /// <summary>
    /// S3 identity matrix (brief §8): each identity is built for each applicable type, simplified, and evaluated on every
    /// assignment of the domain values; the matrix records the simplified form and the difference classes.
    /// </summary>
    /// <param name="writer">The results.</param>
    /// <returns>The number of identities with a candidate defect (a class outside the S3 latitude on moderate inputs).</returns>
    public static int S3Matrix(ResultWriter writer)
    {
        var summary = new StringBuilder("# S3 identity matrix\n\nClasses over every assignment of the domain values (`ieee`: a NaN or an infinity on one side with moderate inputs; `extreme`: a difference with an extreme input; `rounding`: relative difference at most 1e-6; `signedZero`: +0 against -0).\n\n");
        summary.Append("| Identity | Type | Simplified | Assignments | Differences |\n|---|---|---|---:|---|\n");
        foreach ((string name, string[] types, Func<NumericDomain, Expression> build) in Identities())
        {
            foreach (string typeName in types)
            {
                NumericDomain domain = NumericDomain.All[typeName];
                Worker.BeginCase("s3 " + name + " " + typeName);
                Expression body = build(domain);
                (Expression? simplified, Exception? error) = Simplification.Run(body);
                var classes = new SortedDictionary<string, int>();
                var examples = new Dictionary<string, string>();
                int assignments = 0;
                if (simplified is not null)
                {
                    Delegate source = DiffEval.Compile(body, domain), target = DiffEval.Compile(simplified, domain);
                    foreach (object? a in domain.Values)
                    {
                        foreach (object? b in domain.Values)
                        {
                            object?[] arguments = [a, b, domain.Values[2]];
                            DiffEval.Outcome sa = DiffEval.Evaluate(source, arguments), sb = DiffEval.Evaluate(target, arguments);
                            string cls = DiffEval.Classify(sa, sb, arguments);
                            assignments++;
                            if (cls == "same") continue;
                            classes[cls] = classes.TryGetValue(cls, out int n) ? n + 1 : 1;
                            if (!examples.ContainsKey(cls))
                            {
                                examples[cls] = "x=" + Convert.ToString(a, CultureInfo.InvariantCulture) + ",y=" + Convert.ToString(b, CultureInfo.InvariantCulture) + ": "
                                    + (sa.Exception ?? Convert.ToString(sa.Value, CultureInfo.InvariantCulture)) + " vs " + (sb.Exception ?? Convert.ToString(sb.Value, CultureInfo.InvariantCulture));
                            }
                        }
                    }
                }

                string text = error is not null ? "throws " + AuditText.Describe(error) : AuditText.Format(simplified);
                string differences = classes.Count == 0 ? "none" : string.Join("; ", classes.Select(p => p.Key + " " + p.Value + " (" + examples[p.Key] + ")"));
                var record = new { identity = name, type = typeName, simplified = text, assignments, classes, examples };
                writer.Record(record);
                if (classes.Keys.Any(DifferentialAnomalies.Contains) || error is not null) writer.Anomaly(record);
                summary.Append("| `").Append(name).Append("` | ").Append(typeName).Append(" | `").Append(text.Replace("|", "\\|")).Append("` | ").Append(assignments).Append(" | ").Append(differences.Replace("|", "\\|")).Append(" |\n");
                Worker.EndCase();
            }
        }

        writer.Summary(summary.ToString());
        return writer.AnomalyCount;
    }

    /// <summary>The identities of the brief (§8) and their applicable types.</summary>
    /// <returns>The named builders.</returns>
    private static IEnumerable<(string Name, string[] Types, Func<NumericDomain, Expression> Build)> Identities()
    {
        string[] all = ["double", "float", "int", "long", "decimal", "int?"];
        string[] signed = ["double", "float", "int", "long", "decimal", "int?"];
        string[] dbl = ["double"];
        MethodInfo Method(string name) => typeof(double).GetMethod(name, [typeof(double)])!;
        Expression X(NumericDomain d) => d.Parameters[0];
        Expression Y(NumericDomain d) => d.Parameters[1];
        Expression Z(NumericDomain d) => d.Parameters[2];
        Expression K(NumericDomain d, double v) => d.Constant(v);
        yield return ("x * 0", all, d => Expression.Multiply(X(d), K(d, 0)));
        yield return ("0 / x", all, d => Expression.Divide(K(d, 0), X(d)));
        yield return ("x / x", all, d => Expression.Divide(X(d), X(d)));
        yield return ("x - x", all, d => Expression.Subtract(X(d), X(d)));
        yield return ("x / -1", signed, d => Expression.Divide(X(d), K(d, -1)));
        yield return ("x * -1", signed, d => Expression.Multiply(X(d), K(d, -1)));
        yield return ("x + 0", all, d => Expression.Add(X(d), K(d, 0)));
        yield return ("x / 1", all, d => Expression.Divide(X(d), K(d, 1)));
        yield return ("-(-x)", signed, d => Expression.Negate(Expression.Negate(X(d))));
        yield return ("x - (-y)", signed, d => Expression.Subtract(X(d), Expression.Negate(Y(d))));
        yield return ("2*x - 2*x", all, d => Expression.Subtract(Expression.Multiply(K(d, 2), X(d)), Expression.Multiply(K(d, 2), X(d))));
        yield return ("x*y + x*z", all, d => Expression.Add(Expression.Multiply(X(d), Y(d)), Expression.Multiply(X(d), Z(d))));
        yield return ("(x / y) / z", all, d => Expression.Divide(Expression.Divide(X(d), Y(d)), Z(d)));
        yield return ("x / (y / z)", all, d => Expression.Divide(X(d), Expression.Divide(Y(d), Z(d))));
        yield return ("(x / y) / (z / x)", all, d => Expression.Divide(Expression.Divide(X(d), Y(d)), Expression.Divide(Z(d), X(d))));
        yield return ("x / (1 / x)", all, d => Expression.Divide(X(d), Expression.Divide(K(d, 1), X(d))));
        yield return ("x ^ 0", dbl, d => Expression.Power(X(d), K(d, 0)));
        yield return ("0 ^ x", dbl, d => Expression.Power(K(d, 0), X(d)));
        yield return ("1 ^ x", dbl, d => Expression.Power(K(d, 1), X(d)));
        yield return ("x ^ -1", dbl, d => Expression.Power(X(d), K(d, -1)));
        yield return ("x ^ -2", dbl, d => Expression.Power(X(d), K(d, -2)));
        yield return ("x^2 * x^3", dbl, d => Expression.Multiply(Expression.Power(X(d), K(d, 2)), Expression.Power(X(d), K(d, 3))));
        yield return ("x * x", dbl, d => Expression.Multiply(X(d), X(d)));
        yield return ("(x^y)^2", dbl, d => Expression.Power(Expression.Power(X(d), Y(d)), K(d, 2)));
        yield return ("x^y + z^2", dbl, d => Expression.Add(Expression.Power(X(d), Y(d)), Expression.Power(Z(d), K(d, 2))));
        yield return ("log(x) + log(y)", dbl, d => Expression.Add(Expression.Call(Method("Log"), X(d)), Expression.Call(Method("Log"), Y(d))));
        yield return ("log(x) - log(y)", dbl, d => Expression.Subtract(Expression.Call(Method("Log"), X(d)), Expression.Call(Method("Log"), Y(d))));
        yield return ("log10(x) + log10(y)", dbl, d => Expression.Add(Expression.Call(Method("Log10"), X(d)), Expression.Call(Method("Log10"), Y(d))));
        yield return ("sin(x)^2 + cos(x)^2", dbl, d => Expression.Add(Expression.Power(Expression.Call(Method("Sin"), X(d)), K(d, 2)), Expression.Power(Expression.Call(Method("Cos"), X(d)), K(d, 2))));
        yield return ("sin(x) / cos(x)", dbl, d => Expression.Divide(Expression.Call(Method("Sin"), X(d)), Expression.Call(Method("Cos"), X(d))));
        yield return ("cos(x) / sin(x)", dbl, d => Expression.Divide(Expression.Call(Method("Cos"), X(d)), Expression.Call(Method("Sin"), X(d))));
        yield return ("cos(x) * tan(x)", dbl, d => Expression.Multiply(Expression.Call(Method("Cos"), X(d)), Expression.Call(Method("Tan"), X(d))));
        yield return ("sin(x) / tan(x)", dbl, d => Expression.Divide(Expression.Call(Method("Sin"), X(d)), Expression.Call(Method("Tan"), X(d))));
        yield return ("Round(x, 2)", dbl, d => Expression.Call(typeof(double).GetMethod("Round", [typeof(double), typeof(int)])!, X(d), Expression.Constant(2)));
    }
}
