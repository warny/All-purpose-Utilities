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
    /// <summary>
    /// The internal switch of the latest boundary finishing of refused chains, read by reflection: the binary pair finishing
    /// (mission B, <c>RefusedBinaryFinishingDisabled</c>) when the checkout has it, otherwise the suffix finishing (mission M4,
    /// <c>RefusedSuffixFinishingDisabled</c>); <see langword="null"/> on a checkout that has neither.
    /// </summary>
    private static readonly PropertyInfo? SuffixFinishingSwitch =
        typeof(ExpressionSimplifier).GetProperty("RefusedBinaryFinishingDisabled", BindingFlags.NonPublic | BindingFlags.Static)
        ?? typeof(ExpressionSimplifier).GetProperty("RefusedSuffixFinishingDisabled", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>
    /// Reference campaign of mission M4 (<c>docs/expressions/SimplifierFlatten.md</c> §22): every case of the convergence,
    /// termination, differential and effect corpora is simplified with the suffix finishing switched off (reference H, the result
    /// of <c>master</c> <c>48ae9be3</c>) and on. Every case whose first result changes is classified: whether it is the second call
    /// of the reference (<c>F</c>), and whether it is a fixed point of the new engine.
    /// </summary>
    /// <param name="options">The options (the corpora of the four campaigns).</param>
    /// <param name="writer">The results.</param>
    /// <returns>The number of changed cases that are not the second call of the reference, or are not fixed points.</returns>
    public static int Reference(CampaignOptions options, ResultWriter writer)
    {
        if (SuffixFinishingSwitch is null)
        {
            writer.Summary("# Reference campaign\n\nThe checkout has no suffix finishing switch: nothing to compare.\n");
            return 0;
        }

        var counts = new SortedDictionary<string, int>();
        foreach ((string corpus, string label, Expression tree) in ReferenceCases(options))
        {
            Worker.BeginCase("reference " + label);
            (Expression? h1, Exception? e1) = RunSuffix(tree, disabled: true);
            (Expression? b1, Exception? e2) = RunSuffix(tree, disabled: false);
            string key = corpus;
            if (e1 is not null || e2 is not null)
            {
                if ((e1 is null) != (e2 is null) || e1!.GetType() != e2!.GetType())
                {
                    Count(counts, key + "|EXCEPTION-CHANGED");
                    writer.Anomaly(new { corpus, label, kind = "EXCEPTION-CHANGED", h = e1?.GetType().Name, branch = e2?.GetType().Name });
                }
                else Count(counts, key + "|both-throw");
                Worker.EndCase();
                continue;
            }

            var form = new StrictForm();
            string h = form.Of(h1), b = form.Of(b1);
            if (h == b)
            {
                Count(counts, key + "|unchanged");
                Worker.EndCase();
                continue;
            }

            (Expression? h2, _) = RunSuffix(h1!, disabled: true);
            (Expression? b2, _) = RunSuffix(b1!, disabled: false);
            bool isF = h2 is not null && form.Of(h2) == b;
            bool fixedPoint = b2 is not null && form.Of(b2) == b;

            // A changed result that is F but not a fixed point is classified by the reference's own third call: when F is not a
            // fixed point of the reference either, the residue is the reference's, one call earlier.
            bool referenceFixed = !isF || fixedPoint || (RunSuffix(h2!, disabled: true) is ({ } h3, null) && form.Of(h3) == form.Of(h2));
            string kind = (isF ? "F" : "not-F") + (fixedPoint ? "|fixed" : referenceFixed ? "|NOT-FIXED" : "|not-fixed-as-F-on-reference");
            Count(counts, key + "|" + kind);
            var record = new { corpus, label, kind, h = AuditText.Format(h1!), f = h2 is null ? null : AuditText.Format(h2), branch = AuditText.Format(b1!), branch2 = b2 is null ? null : AuditText.Format(b2) };
            writer.Record(record);
            if (!isF || !fixedPoint && referenceFixed) writer.Anomaly(record);
            Worker.EndCase();
        }

        var summary = new StringBuilder("# Reference campaign\n\n")
            .Append("Switch: ").Append(SuffixFinishingSwitch.Name)
            .Append(". H: the finishing switched off; F: H simplified again with it switched off; branch: switched on.\n\n")
            .Append("| Corpus and class | Cases |\n|---|---:|\n");
        foreach (KeyValuePair<string, int> pair in counts) summary.Append("| ").Append(pair.Key.Replace("|", " · ")).Append(" | ").Append(pair.Value).Append(" |\n");
        writer.Summary(summary.ToString());
        return writer.AnomalyCount;
    }

    /// <summary>Increments a counter.</summary>
    /// <param name="counts">The counters.</param>
    /// <param name="key">The key.</param>
    private static void Count(SortedDictionary<string, int> counts, string key) => counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;

    /// <summary>Simplifies <paramref name="tree"/> with the suffix finishing switched off or on, on this thread.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="disabled">Whether the suffix finishing is switched off.</param>
    /// <returns>The result or the exception.</returns>
    private static (Expression? Result, Exception? Error) RunSuffix(Expression tree, bool disabled)
    {
        SuffixFinishingSwitch!.SetValue(null, disabled);
        try
        {
            return Simplification.Run(tree);
        }
        finally
        {
            SuffixFinishingSwitch.SetValue(null, false);
        }
    }

    /// <summary>The cases of the convergence, termination, differential and effect corpora, with the options of their campaigns.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The corpus name, the label and the tree of each case.</returns>
    private static IEnumerable<(string Corpus, string Label, Expression Tree)> ReferenceCases(CampaignOptions options)
    {
        foreach ((string label, Expression tree) in ConvergenceCases(options)) yield return ("converge", label, tree);
        foreach ((string label, Expression tree) in Corpus.Instantiate(options.Nested, 2026)) yield return ("terminate", label, tree);
        for (int seed = 0; seed < options.Seeds; seed++)
        {
            string typeName = options.Types[seed % options.Types.Length];
            var generatorOptions = new GeneratorOptions { MaxDepth = 2 + seed % (options.MaxDepth - 1) };
            yield return ("diff", "diff#" + seed + " " + typeName, new TreeGenerator(seed, NumericDomain.All[typeName], generatorOptions).Build());
        }

        for (int seed = 0; seed < options.Seeds; seed++)
        {
            var generatorOptions = new GeneratorOptions { MaxDepth = 2 + seed % (options.MaxDepth - 1), Effects = true };
            yield return ("effects", "effects#" + seed, new TreeGenerator(seed, NumericDomain.All["double"], generatorOptions).Build());
        }
    }
}
