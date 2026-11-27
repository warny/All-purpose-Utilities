using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Utils.Mathematics.Expressions;

namespace ExpressionSimplifierAudit;

internal static partial class Campaigns
{
    /// <summary>The internal switch of the grouped quotient chains (§16), read by reflection; <see langword="null"/> when absent.</summary>
    private static readonly PropertyInfo? GroupedChainsSwitch =
        typeof(ExpressionSimplifier).GetProperty("GroupedQuotientChainsDisabled", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>
    /// Sign equivariance campaign (<c>docs/expressions/SimplifierFlatten.md</c> §25). Every term set is written with the same
    /// logical signed terms in many ways, along two separate axes:
    /// <list type="bullet">
    /// <item><b>Placement</b>: the physical place of the sign of each term (in its coefficient, in its additive sign, under one
    /// negation). On <c>master</c> a sum keeps the placement of its terms (the competing forms of §20.1), so placements may give
    /// distinct forms.</item>
    /// <item><b>Association</b>, for one placement: the order of the terms and the shape of the tree (left, right, balanced and
    /// random), with additions and subtractions chosen so that every leaf keeps its net additive sign (a leaf under a
    /// subtracted group is subtracted again by an inner subtraction). The leaves are the same expressions in every tree of the
    /// placement, so a canonicalization that only depends on the collected terms gives one form per placement.</item>
    /// </list>
    /// Each tree is simplified once, then again (idempotence), its values are compared with the source (numeric equivalence),
    /// and it is simplified with the grouped quotient chains switched off (grouped against level by level equivalence).
    /// </summary>
    /// <param name="options">The options (<see cref="CampaignOptions.Seeds"/> sets the number of trees per placement).</param>
    /// <param name="writer">The results.</param>
    /// <returns>
    /// The number of anomalies: unstable results, value mismatches, grouped/levels differences and, with bound parameters
    /// (no free-parameter tie, L-3), placements whose associations give more than one form.
    /// </returns>
    public static int Signs(CampaignOptions options, ResultWriter writer)
    {
        int treesPerPlacement = Math.Clamp(options.Seeds / 200, 6, 40);
        var summary = new StringBuilder("# Sign equivariance campaign\n\n")
            .Append("Variant: ").Append(SignVariant.Name ?? "(none)").Append(". Placements per set: up to ").Append(SignPlacements)
            .Append("; trees per placement: ").Append(treesPerPlacement + 3).Append(" (left, right, balanced, random).\n\n")
            .Append("| Term set | Binding | Trees | Placements | Association-dependent placements | Max forms per placement | Distinct forms | Unstable | Value mismatches | Grouped ≠ levels |\n")
            .Append("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|\n");
        var totals = new int[7];
        foreach (SignTermSet set in SignTermSets())
        {
            foreach (bool bound in new[] { false, true })
            {
                Worker.BeginCase("signs " + set.Name + " " + bound);
                var strict = new StrictForm();
                var allForms = new HashSet<string>();
                int trees = 0, placements = 0, dependent = 0, maxForms = 0, unstable = 0, mismatches = 0, groupedDifferences = 0;
                foreach ((int placement, SignLeaf[] leaves) in SignPlacementsOf(set))
                {
                    placements++;
                    var forms = new Dictionary<string, (int Count, string Text, string Source)>();
                    foreach (Expression body in SignAssociations(set.Type, leaves, placement, treesPerPlacement))
                    {
                        trees++;
                        Expression tree = bound ? Expression.Lambda(body, set.Parameters) : body;
                        (Expression? result, Exception? error) = Simplification.Run(tree);
                        if (error is not null)
                        {
                            writer.Anomaly(new { set = set.Name, bound, placement, kind = "EXCEPTION", source = AuditText.Format(tree), error = AuditText.Describe(error) });
                            continue;
                        }

                        string form = strict.Of(result);
                        allForms.Add(form);
                        forms[form] = forms.TryGetValue(form, out var known) ? (known.Count + 1, known.Text, known.Source) : (1, AuditText.Format(result), AuditText.Format(tree));
                        (Expression? again, _) = Simplification.Run(result!);
                        if (again is null || strict.Of(again) != form)
                        {
                            unstable++;
                            writer.Anomaly(new { set = set.Name, bound, placement, kind = "UNSTABLE", source = AuditText.Format(tree), first = AuditText.Format(result), second = again is null ? null : AuditText.Format(again) });
                        }

                        if (!SameSignValues(set, body, bound ? ((LambdaExpression)result!).Body : result!))
                        {
                            mismatches++;
                            writer.Anomaly(new { set = set.Name, bound, placement, kind = "VALUE", source = AuditText.Format(tree), first = AuditText.Format(result) });
                        }

                        if (GroupedChainsSwitch is not null)
                        {
                            GroupedChainsSwitch.SetValue(null, true);
                            (Expression? levels, _) = Simplification.Run(tree);
                            GroupedChainsSwitch.SetValue(null, false);
                            if (levels is null || strict.Of(levels) != form)
                            {
                                groupedDifferences++;
                                writer.Anomaly(new { set = set.Name, bound, placement, kind = "GROUPED-LEVELS", source = AuditText.Format(tree), grouped = AuditText.Format(result), levels = levels is null ? null : AuditText.Format(levels) });
                            }
                        }
                    }

                    maxForms = Math.Max(maxForms, forms.Count);
                    if (forms.Count > 1)
                    {
                        dependent++;
                        var record = new { set = set.Name, bound, placement, kind = "ASSOCIATION", forms = forms.Values.OrderByDescending(f => f.Count).Select(f => f.Count + "× " + f.Text + "  <=  " + f.Source).ToArray() };
                        if (bound) writer.Anomaly(record);
                        else writer.Record(record);
                    }
                }

                writer.Record(new { set = set.Name, bound, trees, placements, dependent, maxForms, forms = allForms.Count, unstable, mismatches, groupedDifferences });
                summary.Append("| ").Append(set.Name).Append(" | ").Append(bound ? "lambda" : "free").Append(" | ").Append(trees).Append(" | ").Append(placements)
                    .Append(" | ").Append(dependent).Append(" | ").Append(maxForms).Append(" | ").Append(allForms.Count).Append(" | ").Append(unstable)
                    .Append(" | ").Append(mismatches).Append(" | ").Append(groupedDifferences).Append(" |\n");
                int[] values = [trees, placements, dependent, allForms.Count, unstable, mismatches, groupedDifferences];
                for (int i = 0; i < totals.Length; i++) totals[i] += values[i];
                Worker.EndCase();
            }
        }

        summary.Append("| **total** | | ").Append(totals[0]).Append(" | ").Append(totals[1]).Append(" | ").Append(totals[2]).Append(" | | ").Append(totals[3])
            .Append(" | ").Append(totals[4]).Append(" | ").Append(totals[5]).Append(" | ").Append(totals[6]).Append(" |\n");
        writer.Summary(summary.ToString());
        return writer.AnomalyCount;
    }

    /// <summary>The maximal number of placements per set (the first one puts every sign in the additive sign of a positive-coefficient term).</summary>
    private const int SignPlacements = 24;

    /// <summary>A logical signed term: <c>sign * coefficient * factor</c>, the coefficient being omitted when it is <see langword="null"/>.</summary>
    /// <param name="Negative">Whether the term is subtracted.</param>
    /// <param name="Coefficient">The direct constant coefficient (of the type of the set), or <see langword="null"/> for a plain term.</param>
    /// <param name="Factor">The factor (shared by every tree of the set).</param>
    private sealed record SignTerm(bool Negative, object? Coefficient, Expression Factor);

    /// <summary>A term set of the campaign.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="Type">The numeric type.</param>
    /// <param name="Parameters">The parameters.</param>
    /// <param name="Terms">The logical terms.</param>
    private sealed record SignTermSet(string Name, Type Type, ParameterExpression[] Parameters, SignTerm[] Terms);

    /// <summary>A leaf of one placement: an expression and the net additive sign it must have in the tree.</summary>
    /// <param name="Expression">The leaf expression.</param>
    /// <param name="Subtracted">Whether the leaf is subtracted (net sign of its position).</param>
    private sealed record SignLeaf(Expression Expression, bool Subtracted);

    /// <summary>The term sets: linear chains (admitted), quotient chains (grouped or refused), calls, shared products, extreme and integral coefficients.</summary>
    /// <returns>The sets.</returns>
    private static IEnumerable<SignTermSet> SignTermSets()
    {
        ParameterExpression[] p = new[] { "a", "b", "c", "d", "e" }.Select(n => Expression.Parameter(typeof(double), n)).ToArray();
        (ParameterExpression a, ParameterExpression b, ParameterExpression c, ParameterExpression d, ParameterExpression e) = (p[0], p[1], p[2], p[3], p[4]);
        Expression quotient = Expression.Divide(b, e);
        Expression shared = Expression.Multiply(c, d);
        MethodInfo sin = typeof(Math).GetMethod(nameof(Math.Sin), [typeof(double)])!;
        yield return new("{a, -6.5c, d}", typeof(double), p, [new(false, null, a), new(false, -6.5, c), new(false, null, d)]);
        yield return new("{a, -b/e, -6.5c, d}", typeof(double), p, [new(false, null, a), new(true, null, quotient), new(false, -6.5, c), new(false, null, d)]);
        yield return new("{a, -b/e, +6.5c, d}", typeof(double), p, [new(false, null, a), new(true, null, quotient), new(false, 6.5, c), new(false, null, d)]);
        yield return new("{a, b/e, -2c, -3d}", typeof(double), p, [new(false, null, a), new(false, null, quotient), new(false, -2.0, c), new(true, 3.0, d)]);
        yield return new("{a, -2(c*d), b/e} shared", typeof(double), p, [new(false, null, a), new(false, -2.0, shared), new(false, null, quotient)]);
        yield return new("{a, -2c, 2d, b/e}", typeof(double), p, [new(false, null, a), new(false, -2.0, c), new(true, -2.0, d), new(false, null, quotient)]);
        yield return new("{sin(a), -1e300c, d/e}", typeof(double), p, [new(false, null, Expression.Call(sin, a)), new(false, -1e300, c), new(false, null, Expression.Divide(d, e))]);
        ParameterExpression[] q = new[] { "a", "b", "c", "d", "e" }.Select(n => Expression.Parameter(typeof(int), n)).ToArray();
        yield return new("int {a, -3c, b/e}", typeof(int), q, [new(false, null, q[0]), new(false, -3, q[2]), new(false, null, Expression.Divide(q[1], q[4]))]);
        yield return new("int {a, (MinValue+1)c, d}", typeof(int), q, [new(false, null, q[0]), new(false, int.MinValue + 1, q[2]), new(false, null, q[3])]);
        yield return new("int {a, MinValue c, d}", typeof(int), q, [new(false, null, q[0]), new(false, int.MinValue, q[2]), new(false, null, q[3])]);
    }

    /// <summary>
    /// The placements of a set: for each term, its leaf expression and net sign. A coefficient term <c>s * k * t</c> is
    /// <c>k * t</c> with sign <c>s</c>, <c>(-k) * t</c> with sign <c>-s</c>, <c>-((-k) * t)</c> with sign <c>s</c> or
    /// <c>-(k * t)</c> with sign <c>-s</c>, the coefficient on the left or the right; a plain term is <c>t</c> with sign <c>s</c>
    /// or <c>-t</c> with sign <c>-s</c>. The first placement is the plain one; the others are drawn from a fixed seed, without
    /// repetition.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <returns>The placement index and its leaves.</returns>
    private static IEnumerable<(int Placement, SignLeaf[] Leaves)> SignPlacementsOf(SignTermSet set)
    {
        var random = new Random(7);
        var seen = new HashSet<string>();
        for (int attempt = 0, produced = 0; attempt < 400 && produced < SignPlacements; attempt++)
        {
            int[] choices = set.Terms.Select(term => attempt == 0 ? 0 : random.Next(term.Coefficient is null ? 2 : 8)).ToArray();
            if (!seen.Add(string.Join(",", choices))) continue;
            produced++;
            yield return (produced - 1, set.Terms.Select((term, i) => SignLeafOf(set.Type, term, choices[i])).ToArray());
        }
    }

    /// <summary>One leaf encoding of a term (see <see cref="SignPlacementsOf"/>).</summary>
    /// <param name="type">The numeric type.</param>
    /// <param name="term">The term.</param>
    /// <param name="choice">The encoding.</param>
    /// <returns>The leaf.</returns>
    private static SignLeaf SignLeafOf(Type type, SignTerm term, int choice)
    {
        if (term.Coefficient is null) return choice == 0 ? new(term.Factor, term.Negative) : new(Expression.Negate(term.Factor), !term.Negative);
        bool right = choice >= 4;
        Expression Product(bool negated)
        {
            ConstantExpression k = term.Coefficient switch
            {
                double value => Expression.Constant(negated ? -value : value, type),
                int value => Expression.Constant(negated ? unchecked(-value) : value, type),
                _ => throw new ArgumentException("Unsupported coefficient " + term.Coefficient),
            };
            return right ? Expression.Multiply(term.Factor, k) : Expression.Multiply(k, term.Factor);
        }

        return (choice % 4) switch
        {
            0 => new(Product(false), term.Negative),
            1 => new(Product(true), !term.Negative),
            2 => new(Expression.Negate(Product(true)), term.Negative),
            _ => new(Expression.Negate(Product(false)), !term.Negative),
        };
    }

    /// <summary>
    /// The associations of one placement: a left comb, a right comb and a balanced tree in the order of the set, then random
    /// orders and shapes; the first leaf of every order is an added one, and every node adds or subtracts its right subtree so
    /// that each leaf keeps its net sign.
    /// </summary>
    /// <param name="type">The numeric type.</param>
    /// <param name="leaves">The leaves of the placement.</param>
    /// <param name="placement">The placement index (seeds the random trees).</param>
    /// <param name="count">The number of random trees.</param>
    /// <returns>The trees.</returns>
    private static IEnumerable<Expression> SignAssociations(Type type, SignLeaf[] leaves, int placement, int count)
    {
        if (!leaves.Any(leaf => !leaf.Subtracted)) yield break;
        SignLeaf[] ordered = Rotated([.. leaves]);
        yield return SignAssociation(ordered, 0, ordered.Length, SignShape.Left, null);
        yield return SignAssociation(ordered, 0, ordered.Length, SignShape.Right, null);
        yield return SignAssociation(ordered, 0, ordered.Length, SignShape.Balanced, null);
        var random = new Random(100 * placement + 1);
        for (int i = 0; i < count; i++)
        {
            SignLeaf[] order = Rotated([.. leaves.OrderBy(_ => random.Next())]);
            yield return SignAssociation(order, 0, order.Length, (SignShape)random.Next(4), random);
        }
    }

    /// <summary>Rotates an order so that it starts with an added leaf.</summary>
    /// <param name="order">The order.</param>
    /// <returns>The rotated order.</returns>
    private static SignLeaf[] Rotated(SignLeaf[] order)
    {
        int first = Array.FindIndex(order, leaf => !leaf.Subtracted);
        return [.. order.Skip(first), .. order.Take(first)];
    }

    /// <summary>The shape of a generated tree.</summary>
    private enum SignShape
    {
        /// <summary>A left comb.</summary>
        Left,

        /// <summary>A right comb.</summary>
        Right,

        /// <summary>A balanced tree.</summary>
        Balanced,

        /// <summary>A random split at every node.</summary>
        Random,
    }

    /// <summary>
    /// Builds the subtree of <c>leaves[from..to)</c> whose value is the signed sum of its leaves relative to the sign of its first
    /// leaf: a node adds its right subtree when the first leaf of that subtree has the sign of the node's first leaf, and
    /// subtracts it otherwise.
    /// </summary>
    /// <param name="leaves">The leaves.</param>
    /// <param name="from">The first leaf.</param>
    /// <param name="to">The end of the range.</param>
    /// <param name="shape">The shape.</param>
    /// <param name="random">The random source of <see cref="SignShape.Random"/> splits.</param>
    /// <returns>The subtree.</returns>
    private static Expression SignAssociation(SignLeaf[] leaves, int from, int to, SignShape shape, Random? random)
    {
        if (to - from == 1) return leaves[from].Expression;
        int middle = shape switch
        {
            SignShape.Left => to - 1,
            SignShape.Right => from + 1,
            SignShape.Balanced => from + (to - from) / 2,
            _ => from + 1 + random!.Next(to - from - 1),
        };

        Expression left = SignAssociation(leaves, from, middle, shape, random);
        Expression right = SignAssociation(leaves, middle, to, shape, random);
        return leaves[middle].Subtracted == leaves[from].Subtracted ? Expression.Add(left, right) : Expression.Subtract(left, right);
    }

    /// <summary>
    /// Whether the simplified body computes the values of the source on fixed samples: exactly for integers (unchecked
    /// arithmetic), within the relative reassociation latitude <c>1e-9</c> for <see cref="double"/>; an exception is expected on
    /// both sides alike.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <param name="source">The source body.</param>
    /// <param name="result">The simplified body.</param>
    /// <returns><see langword="true"/> when the values agree.</returns>
    private static bool SameSignValues(SignTermSet set, Expression source, Expression result)
    {
        Delegate before = Expression.Lambda(source, set.Parameters).Compile();
        Delegate after = Expression.Lambda(result, set.Parameters).Compile();
        double[][] samples = [[0.5, 1.5, 2.5, -1.25, 4], [3, -2, 0.75, 2, -0.5], [-7, 9, -3, 11, 2]];
        foreach (double[] sample in samples)
        {
            object[] arguments = set.Type == typeof(int) ? sample.Select(v => (object)(int)Math.Round(v * 3)).ToArray() : sample.Cast<object>().ToArray();
            (object? x, Exception? ex) = InvokeSample(before, arguments);
            (object? y, Exception? ey) = InvokeSample(after, arguments);
            if ((ex is null) != (ey is null)) return false;
            if (ex is not null)
            {
                if (ex.GetType() != ey!.GetType()) return false;
                continue;
            }

            if (x is double p && y is double q)
            {
                if (!(p.Equals(q) || Math.Abs(p - q) <= 1e-9 * Math.Max(Math.Abs(p), Math.Abs(q)))) return false;
            }
            else if (!Equals(x, y))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Invokes a compiled delegate and unwraps the exception of its body.</summary>
    /// <param name="function">The delegate.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The value or the exception.</returns>
    private static (object? Value, Exception? Error) InvokeSample(Delegate function, object[] arguments)
    {
        try
        {
            return (function.DynamicInvoke(arguments), null);
        }
        catch (TargetInvocationException exception)
        {
            return (null, exception.InnerException ?? exception);
        }
    }
}
