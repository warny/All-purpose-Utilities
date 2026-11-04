using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Utils.Expressions;

namespace ExpressionSimplifierAudit;

/// <summary>
/// Command-line entry point of the S5 deep-audit harness. Exit codes: 0 no anomaly, 1 anomalies found, 2 usage error,
/// 3 harness failure, 5 a case exceeded the per-case timeout, 6 the memory limit was exceeded (the last two write the case
/// to <c>&lt;campaign&gt;-aborted.txt</c>; rerun with <c>--start</c> after it and <c>--resume</c>).
/// </summary>
public static class Program
{
    /// <summary>The usage text.</summary>
    private const string Usage = """
        Usage: ExpressionSimplifierAudit <command> [options]
          inventory                 rule inventory, static graph (add --edges <file> for the dynamic graph)
          terminate                 termination campaign over the structural corpus (writes terminate-edges.tsv)
          converge                  convergence campaign (repeated public passes)
          canon                     canonicalization campaign
          comparer                  comparer and canonical-order invariants
          diff                      differential campaign over several numeric types
          effects                   observable-effect campaign
          s3                        S3 identity matrix
          measure <family> <n>      complexity of one family at one size (one process per measurement)
          families                  list the families
          trace "<template>"        simplify a template (x, y, z are parameters) and print passes, trace and cycle report
          replay diff|effects <seed> re-run one seed of a campaign verbosely
        Options: --out <dir> --seeds <n> --start <n> --depth <n> --nested <n> --types a,b --passes <n> --assign <n>
                 --reduce <n> --max-cases <n> --timeout <s> --memory <MB> --resume --unlimited --edges <file> --max-seconds <s>
        """;

    /// <summary>Runs a command.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The exit code.</returns>
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length == 0)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var options = new Dictionary<string, string>();
        var positional = new List<string>();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                string key = args[i][2..];
                bool flag = key is "resume" or "unlimited";
                options[key] = flag ? "true" : i + 1 < args.Length ? args[++i] : "";
            }
            else
            {
                positional.Add(args[i]);
            }
        }

        string Get(string key, string fallback) => options.TryGetValue(key, out string? value) ? value : fallback;
        int GetInt(string key, int fallback) => int.Parse(Get(key, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
        string command = args[0];
        string output = Get("out", Path.Combine("artifacts", "s5-audit", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)));
        bool resume = options.ContainsKey("resume");
        var campaign = new CampaignOptions(
            Seeds: GetInt("seeds", 2000),
            Start: GetInt("start", 0),
            MaxDepth: Math.Max(2, GetInt("depth", 5)),
            Nested: GetInt("nested", 4000),
            Types: Get("types", "double,float,int,long,decimal,int?,uint").Split(',', StringSplitOptions.RemoveEmptyEntries),
            Passes: GetInt("passes", 8),
            Assignments: GetInt("assign", 24),
            Reduce: GetInt("reduce", 40),
            MaxCases: GetInt("max-cases", int.MaxValue));
        var timeout = TimeSpan.FromSeconds(GetInt("timeout", 60));
        long memory = GetInt("memory", 6144) * 1024L * 1024L;

        switch (command)
        {
            case "families":
                foreach ((string name, string shape) in Families.Catalog) Console.WriteLine(name + "\t" + shape);
                return 0;
            case "measure":
                if (positional.Count < 2) return Fail("measure needs <family> <n>");
                return Worker.Run(() =>
                {
                    object record = Measure.Run(positional[0], int.Parse(positional[1], CultureInfo.InvariantCulture), options.ContainsKey("unlimited"), double.Parse(Get("max-seconds", "8"), CultureInfo.InvariantCulture));
                    string line = JsonSerializer.Serialize(record, record.GetType());
                    Console.WriteLine(line);
                    Directory.CreateDirectory(output);
                    File.AppendAllText(Path.Combine(output, "measure.jsonl"), line + Environment.NewLine, new UTF8Encoding(false));
                    return 0;
                }, timeout, memory, (reason, current) => Abort(output, "measure", reason, positional[0] + " " + positional[1]));
            case "trace":
                if (positional.Count < 1) return Fail("trace needs a template");
                return Worker.Run(() => Trace(positional[0]), timeout, memory, (reason, current) => Console.Error.WriteLine(reason + ": " + current));
            case "replay":
                if (positional.Count < 2) return Fail("replay needs a campaign and a seed");
                return Worker.Run(() => Replay(positional[0], int.Parse(positional[1], CultureInfo.InvariantCulture), campaign), timeout, memory, (reason, current) => Console.Error.WriteLine(reason + ": " + current));
        }

        Func<ResultWriter, int>? body = command switch
        {
            "inventory" => w => Inventory.Run(w, options.TryGetValue("edges", out string? edges) ? edges : Path.Combine(output, "terminate-edges.tsv")),
            "terminate" => w =>
            {
                var edges = new Dictionary<string, long>();
                int anomalies = Campaigns.Terminate(campaign, w, edges);
                File.WriteAllLines(Path.Combine(output, "terminate-edges.tsv"), edges.OrderByDescending(p => p.Value).Select(p => p.Key + "\t" + p.Value));
                return anomalies;
            },
            "converge" => w => Campaigns.Converge(campaign, w),
            "canon" => w => Campaigns.Canonicalize(w),
            "comparer" => w => Campaigns.ComparerInvariants(campaign, w),
            "diff" => w => Campaigns.Differential(campaign, w),
            "effects" => w => Campaigns.Effects(campaign, w),
            "s3" => w => Campaigns.S3Matrix(w),
            _ => null,
        };
        if (body is null) return Fail("Unknown command " + command);

        return Worker.Run(() =>
        {
            using var writer = new ResultWriter(output, command, resume);
            int anomalies = body(writer);
            Console.WriteLine(command + ": " + anomalies + " anomalies; results in " + Path.GetFullPath(output));
            return anomalies == 0 ? 0 : 1;
        }, timeout, memory, (reason, current) => Abort(output, command, reason, current));
    }

    /// <summary>Writes the case that made the watchdog abort the process.</summary>
    /// <param name="output">The output directory.</param>
    /// <param name="command">The campaign.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="current">The case.</param>
    private static void Abort(string output, string command, string reason, string current)
    {
        Directory.CreateDirectory(output);
        File.AppendAllText(Path.Combine(output, command + "-aborted.txt"), reason + "\t" + current + Environment.NewLine, new UTF8Encoding(false));
        Console.Error.WriteLine(command + " aborted (" + reason + "): " + current);
    }

    /// <summary>Prints a usage error.</summary>
    /// <param name="message">The message.</param>
    /// <returns>2.</returns>
    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(Usage);
        return 2;
    }

    /// <summary>Simplifies a template repeatedly and prints the passes, the trace of the first pass and its cycle report.</summary>
    /// <param name="text">The template (x, y and z are parameters; other letters are parameters a..w).</param>
    /// <returns>0.</returns>
    private static int Trace(string text)
    {
        var template = new Template(text);
        var bindings = template.Placeholders.ToDictionary(c => c, c => (Expression)(c switch { 'x' => Corpus.X, 'y' => Corpus.Y, 'z' => Corpus.Z, _ => Expression.Parameter(typeof(double), c.ToString()) }));
        Expression current = template.Build(bindings);
        Console.WriteLine("source:  " + AuditText.Format(current));
        var collector = new ExpressionTraceCollector(200_000, ExpressionTraceRetention.KeepFirst);
        for (int pass = 1; pass <= 6; pass++)
        {
            (Expression? result, Exception? error) = Simplification.Run(current, null, pass == 1 ? collector : null);
            if (error is not null)
            {
                Console.WriteLine("pass " + pass + ": throws " + AuditText.Describe(error));
                break;
            }

            Console.WriteLine("pass " + pass + ": " + AuditText.Format(result));
            if (new StrictForm().Of(result) == new StrictForm().Of(current) && pass > 1) break;
            current = result!;
        }

        ExpressionTransformEvent[] events = collector.GetEvents();
        Console.WriteLine();
        Console.WriteLine(ExpressionTraceExporter.ToText(events.Take(400)));
        Console.WriteLine(ExpressionTraceCycleAnalyzer.ToText(ExpressionTraceCycleAnalyzer.Analyze(events)));
        return 0;
    }

    /// <summary>Re-runs one seed of the differential or effect campaign and prints the trees and every evaluation.</summary>
    /// <param name="campaignName"><c>diff</c> or <c>effects</c>.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="options">The campaign options (types, depth, assignments).</param>
    /// <returns>0.</returns>
    private static int Replay(string campaignName, int seed, CampaignOptions options)
    {
        bool effects = campaignName == "effects";
        NumericDomain domain = NumericDomain.All[effects ? "double" : options.Types[seed % options.Types.Length]];
        Expression body = new TreeGenerator(seed, domain, new GeneratorOptions { MaxDepth = 2 + seed % (options.MaxDepth - 1), Effects = effects }).Build();
        Console.WriteLine("type:       " + domain.Type.Name);
        Console.WriteLine("source:     " + AuditText.Format(body));
        (Expression? simplified, Exception? error) = Simplification.Run(body);
        if (error is not null)
        {
            Console.WriteLine("simplify throws: " + AuditText.Describe(error) + Environment.NewLine + AuditText.Innermost(error));
            return 0;
        }

        Console.WriteLine("simplified: " + AuditText.Format(simplified));
        Delegate source = DiffEval.Compile(body, domain), target = DiffEval.Compile(simplified!, domain);
        foreach (object?[] arguments in DiffEval.Assignments(domain, seed, options.Assignments))
        {
            EffectLeaves.Reset();
            DiffEval.Outcome a = DiffEval.Evaluate(source, arguments);
            string logA = string.Join(",", EffectLeaves.Log);
            EffectLeaves.Reset();
            DiffEval.Outcome b = DiffEval.Evaluate(target, arguments);
            string logB = string.Join(",", EffectLeaves.Log);
            Console.WriteLine("(" + string.Join(", ", arguments.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "null")) + "): "
                + (a.Exception ?? Convert.ToString(a.Value, CultureInfo.InvariantCulture)) + " vs " + (b.Exception ?? Convert.ToString(b.Value, CultureInfo.InvariantCulture))
                + " -> " + DiffEval.Classify(a, b, arguments) + (effects ? " | " + logA + " | " + logB : ""));
        }

        return 0;
    }
}
