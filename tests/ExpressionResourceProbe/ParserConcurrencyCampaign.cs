using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Expressions.VBSyntax.Runtime;
using Utils.Parser.Bootstrap;
using Utils.Parser.Runtime;

namespace ExpressionResourceProbe;

/// <summary>
/// Issue #709 characterization: concurrent operations on shared parser façades compared, operation by operation, with
/// a sequential oracle computed on independent engines. Prints one <c>CCAMPAIGN</c> line per surface, workload, thread
/// count and seed, and one <c>CBENCH</c> line per benchmark measurement.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// ExpressionResourceProbe parser-concurrency-campaign [--ops &lt;per worker&gt;] [--seeds &lt;count&gt;] [--threads 2,4,8,16] [--surfaces a,b]
/// ExpressionResourceProbe parser-concurrency-bench [--ms &lt;per measurement&gt;] [--threads 1,2,4,8,16] [--mode shared|perthread|percall]
/// </code>
/// The oracle never shares an engine with the campaign: every expected result comes from a fresh
/// <see cref="CompiledGrammar"/> built from the same definition and used sequentially, before the concurrent phase.
/// </remarks>
internal static class ParserConcurrencyCampaign
{
    /// <summary>A grammar with lexer modes, a hidden channel, <c>more</c>, keywords and left-recursive rules.</summary>
    private const string ModeGrammar = """
        grammar ModeProbe;
        prog : stat+ ;
        stat : ID ASSIGN expr SEMI | PRINT expr SEMI | IF expr THEN stat (ELSE stat)? ;
        expr : expr (STAR | SLASH) expr | expr (PLUS | MINUS) expr | expr (LESS | EQUALS) expr | MINUS expr | LPAREN expr RPAREN | ID | INT | str ;
        str : OPEN STRTEXT* CLOSE ;
        PRINT : 'print' ;
        IF : 'if' ;
        THEN : 'then' ;
        ELSE : 'else' ;
        ID : ('a'..'z')+ ;
        INT : ('0'..'9')+ ;
        EQUALS : '==' ;
        ASSIGN : '=' ;
        SEMI : ';' ;
        STAR : '*' ;
        SLASH : '/' ;
        PLUS : '+' ;
        MINUS : '-' ;
        LESS : '<' ;
        LPAREN : '(' ;
        RPAREN : ')' ;
        COMMENT : '#' (~'\n')* -> channel(HIDDEN) ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        OPEN : '"' -> pushMode(STRING) ;
        mode STRING;
        CLOSE : '"' -> popMode ;
        ESCAPE : '\\' -> more ;
        STRTEXT : (~('"' | '\\'))+ ;
        """;

    /// <summary>One shared surface under test.</summary>
    /// <param name="Name">The surface name.</param>
    /// <param name="Parse">The parse entry point of the shared surface (one delegate per worker).</param>
    /// <param name="Tokenize">The tokenize entry point of the shared surface, rendered canonically.</param>
    /// <param name="OracleParse">The sequential oracle parse (fresh engines).</param>
    /// <param name="OracleTokenize">The sequential oracle tokenization (fresh engines).</param>
    /// <param name="Inputs">The corpus.</param>
    private sealed record Surface(
        string Name,
        Func<Func<string, string>> Parse,
        Func<Func<string, string>> Tokenize,
        Func<string, string> OracleParse,
        Func<string, string> OracleTokenize,
        string[] Inputs);

    /// <summary>Runs the campaign.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>0 when every operation matched the oracle, 1 otherwise.</returns>
    public static int RunCampaign(string[] args)
    {
        int ops = int.Parse(Option(args, "--ops") ?? "300", CultureInfo.InvariantCulture);
        int seeds = int.Parse(Option(args, "--seeds") ?? "3", CultureInfo.InvariantCulture);
        int[] threadCounts = (Option(args, "--threads") ?? "2,4,8,16").Split(',').Select(t => int.Parse(t, CultureInfo.InvariantCulture)).ToArray();
        string[]? selected = Option(args, "--surfaces")?.Split(',');
        string[] workloads = ["parse", "tokenize", "mixed"];

        bool allClean = true;
        foreach (Surface surface in CreateSurfaces().Where(s => selected is null || selected.Contains(s.Name)))
        {
            // Oracle first, sequentially, on fresh engines: never on the shared instance under test.
            string[] treeOracle = surface.Inputs.Select(surface.OracleParse).ToArray();
            string[] tokenOracle = surface.Inputs.Select(surface.OracleTokenize).ToArray();
            if (Array.IndexOf(args, "--show-oracle") >= 0)
            {
                for (int i = 0; i < surface.Inputs.Length; i++)
                {
                    string Shorten(string s) => s.Length <= 160 ? s : s[..160] + "...";
                    Console.WriteLine($"ORACLE surface={surface.Name} input#{i} tree={Shorten(treeOracle[i])} tokens={Shorten(tokenOracle[i])}");
                }
            }

            foreach (int threads in threadCounts)
            {
                foreach (string workload in workloads)
                {
                    for (int seed = 1; seed <= seeds; seed++)
                    {
                        allClean &= RunOne(surface, treeOracle, tokenOracle, threads, workload, seed, ops);
                    }
                }
            }
        }

        Console.WriteLine($"CCAMPAIGN-SUMMARY clean={allClean}");
        return allClean ? 0 : 1;
    }

    /// <summary>Runs one surface, thread count, workload and seed.</summary>
    /// <param name="surface">The surface.</param>
    /// <param name="treeOracle">The expected tree rendering of every input.</param>
    /// <param name="tokenOracle">The expected token rendering of every input.</param>
    /// <param name="threads">The number of workers.</param>
    /// <param name="workload">parse, tokenize or mixed.</param>
    /// <param name="seed">The deterministic seed of the operation schedule.</param>
    /// <param name="ops">The number of operations per worker.</param>
    /// <returns>Whether every operation matched the oracle.</returns>
    private static bool RunOne(Surface surface, string[] treeOracle, string[] tokenOracle, int threads, string workload, int seed, int ops)
    {
        const int PhaseLength = 25;
        long wrongTrees = 0, wrongTokens = 0, wrongFailures = 0, expectedFailures = 0, unexpectedExceptions = 0;
        var exceptionNames = new Dictionary<string, long>(StringComparer.Ordinal);
        var gate = new object();

        // The barrier re-synchronizes the workers every PhaseLength operations, so the operations really overlap
        // instead of merely being scheduled concurrently.
        using var barrier = new Barrier(threads);
        var workers = new Thread[threads];
        long allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var watch = Stopwatch.StartNew();
        for (int w = 0; w < threads; w++)
        {
            int worker = w;
            workers[w] = new Thread(() =>
            {
                Func<string, string> parse = surface.Parse();
                Func<string, string> tokenize = surface.Tokenize();
                var random = new Random(seed * 7919 + worker);
                for (int i = 0; i < ops; i++)
                {
                    if (i % PhaseLength == 0) barrier.SignalAndWait();
                    int index = random.Next(surface.Inputs.Length);
                    bool doParse = workload switch { "parse" => true, "tokenize" => false, _ => random.Next(2) == 0 };
                    string expected = doParse ? treeOracle[index] : tokenOracle[index];
                    string actual;
                    try
                    {
                        actual = doParse ? parse(surface.Inputs[index]) : tokenize(surface.Inputs[index]);
                    }
                    catch (Exception exception)
                    {
                        // Oracle renderings of refusals and other exceptions start with "!" (see RenderException), so an
                        // exception thrown by an operation whose oracle did not throw is unexpected.
                        actual = RenderException(exception);
                        if (!expected.StartsWith('!'))
                        {
                            Interlocked.Increment(ref unexpectedExceptions);
                            lock (gate)
                            {
                                exceptionNames[exception.GetType().Name] = exceptionNames.GetValueOrDefault(exception.GetType().Name) + 1;
                            }

                            continue;
                        }
                    }

                    if (expected.StartsWith('!'))
                    {
                        if (actual == expected) Interlocked.Increment(ref expectedFailures);
                        else Interlocked.Increment(ref wrongFailures);
                    }
                    else if (actual != expected)
                    {
                        if (doParse) Interlocked.Increment(ref wrongTrees);
                        else Interlocked.Increment(ref wrongTokens);
                    }
                }

                // Leave the barrier so that workers with fewer remaining phases do not block the others.
                barrier.RemoveParticipant();
            }, 4 * 1024 * 1024);
            workers[w].Start();
        }

        foreach (Thread thread in workers) thread.Join();
        watch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        long total = (long)threads * ops;

        // The shared surface must still match the oracle sequentially once the concurrent phase is over.
        Func<string, string> afterParse = surface.Parse();
        bool sequentialAfter = surface.Inputs.Select((input, index) => SafeRender(afterParse, input) == treeOracle[index]).All(ok => ok);
        bool clean = wrongTrees == 0 && wrongTokens == 0 && wrongFailures == 0 && unexpectedExceptions == 0 && sequentialAfter;
        string exceptionText = exceptionNames.Count == 0 ? "none" : string.Join(",", exceptionNames.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"CCAMPAIGN surface={surface.Name} workload={workload} threads={threads} seed={seed} operations={total} wrongTrees={wrongTrees} wrongTokens={wrongTokens} wrongFailures={wrongFailures} expectedFailures={expectedFailures} unexpectedExceptions={unexpectedExceptions} exceptions={exceptionText} sequentialAfter={sequentialAfter} elapsedMs={watch.Elapsed.TotalMilliseconds:F0} opsPerSec={total / watch.Elapsed.TotalSeconds:F0} bytesPerOp={allocated / Math.Max(1, total)} clean={clean}"));
        return clean;
    }

    /// <summary>Builds the surfaces under test.</summary>
    /// <returns>The surfaces.</returns>
    private static IEnumerable<Surface> CreateSurfaces()
    {
        string[] cInputs =
        [
            "a + b * c",
            "f(x, y[2]).z - (1 + 2) * 3",
            Sum(60),
            "if (a) b = 1; else b = 2;",
            "((((x0))))",
            "-(-(-(!x)))",
            "a < b == c >= d && e != f || !g",
            "a ? b ? 1 : 2 : c ? 3 : 4",
            "x => x * 2 + \"text\".Length",
            "\"esc\\\"aped\" + 'c' + @\"verbatim\"",
            Nest(12),
            "a + * b",
            "(((a)",
            "1 +",
            "§",
            "",
        ];
        string[] vbInputs =
        [
            "a + b * c",
            "f(x, y(2)).z - (1 + 2) * 3",
            Sum(60),
            "If(a, 1, 2)",
            "((((x0))))",
            "Not Not -x",
            "a < b AndAlso c <> d OrElse Not e",
            "\"text\" & \"other\"",
            "a Mod b \\ c ^ 2",
            Nest(12),
            "a + * b",
            "(((a)",
            "1 +",
            "",
        ];
        string[] modeInputs =
        [
            "x = 1;",
            "print \"hello\";",
            "print \"es\\\"c\" + x; # trailing comment",
            "if a < b then x = a * (b + 1); else print -c;",
            "a = b + c * d - e / f == g;",
            string.Join(" ", Enumerable.Range(0, 40).Select(i => "v = v + " + i.ToString(CultureInfo.InvariantCulture) + ";")),
            "x = " + new string('(', 20) + "1" + new string(')', 20) + ";",
            "x = ;",
            "print \"unterminated",
            "if then",
            "x = 1 $ 2;",
        ];

        Type cGrammar = typeof(CSyntaxTokenParser).Assembly.GetType("Utils.Expressions.CSyntax.Runtime.CSyntaxTokenizerGrammar", true)!;
        Type vbGrammar = typeof(VBSyntaxTokenParser).Assembly.GetType("Utils.Expressions.VBSyntax.Runtime.VBSyntaxTokenizerGrammar", true)!;
        var cDefinition = (Utils.Parser.Model.ParserDefinition)cGrammar.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
        var vbDefinition = (Utils.Parser.Model.ParserDefinition)vbGrammar.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
        var cStatic = (CompiledGrammar)cGrammar.GetProperty("Grammar", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var modeDefinition = Antlr4GrammarConverter.Parse(ModeGrammar);
        var tightLimits = ParserResourceLimits.Default with { MaxTokens = 60, MaxParseDepth = 120, MaxParserSteps = 4_000 };

        Func<string, string> Fresh(Utils.Parser.Model.ParserDefinition definition, ParserResourceLimits limits, bool parse)
            => input => SafeRender(text =>
            {
                var oracle = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, limits);
                return parse ? RenderTree(oracle.Parse(text)) : RenderTokens(oracle.Tokenize(text));
            }, input);

        // Front ends: a distinct CSyntaxTokenParser per worker, all sharing the static CompiledGrammar (issue #709).
        yield return new Surface("csyntax",
            () => { var p = new CSyntaxTokenParser(); return input => RenderTree(p.Parse(input)); },
            () => { var p = new CSyntaxTokenParser(); return input => string.Join("|", p.Tokenize(input).Select(t => t.RuleName + ":" + t.Text + "@" + t.Position + "+" + t.Length)); },
            Fresh(cDefinition, ParserResourceLimits.Default, true),
            input => SafeRender(text => string.Join("|", new CompiledGrammar(cDefinition).Tokenize(text).Select(t => t.RuleName + ":" + t.Text + "@" + t.Span.Position + "+" + t.Span.Length)), input),
            cInputs);
        yield return new Surface("vbsyntax",
            () => { var p = new VBSyntaxTokenParser(); return input => RenderTree(p.Parse(input)); },
            () => { var p = new VBSyntaxTokenParser(); return input => string.Join("|", p.Tokenize(input).Select(t => t.RuleName + ":" + t.Text + "@" + t.Position + "+" + t.Length)); },
            Fresh(vbDefinition, ParserResourceLimits.Default, true),
            input => SafeRender(text => string.Join("|", new CompiledGrammar(vbDefinition).Tokenize(text).Select(t => t.RuleName + ":" + t.Text + "@" + t.Span.Position + "+" + t.Span.Length)), input),
            vbInputs);

        // The generated static façade itself (CSyntaxTokenizerGrammar.Grammar), with full token metadata.
        yield return new Surface("generated-static",
            () => input => RenderTree(cStatic.Parse(input)),
            () => input => RenderTokens(cStatic.Tokenize(input)),
            Fresh(cDefinition, ParserResourceLimits.Default, true),
            Fresh(cDefinition, ParserResourceLimits.Default, false),
            cInputs);

        // A directly shared CompiledGrammar with lexer modes, more, hidden channel and left recursion.
        var sharedModes = new CompiledGrammar(modeDefinition);
        yield return new Surface("modes",
            () => input => RenderTree(sharedModes.Parse(input)),
            () => input => RenderTokens(sharedModes.Tokenize(input)),
            Fresh(modeDefinition, ParserResourceLimits.Default, true),
            Fresh(modeDefinition, ParserResourceLimits.Default, false),
            modeInputs);

        // The same grammar under tight limits: some operations are refused, and refusals must match the oracle exactly.
        var sharedTight = new CompiledGrammar(modeDefinition, ParserRuntimeFeaturePolicy.Default, tightLimits);
        yield return new Surface("limits",
            () => input => RenderTree(sharedTight.Parse(input)),
            () => input => RenderTokens(sharedTight.Tokenize(input)),
            Fresh(modeDefinition, tightLimits, true),
            Fresh(modeDefinition, tightLimits, false),
            modeInputs);

        // Expression compilation through one compiler instance shared by every worker, then evaluation.
        var sharedCompiler = new CSyntaxExpressionCompiler();
        var sharedVbCompiler = new VBSyntaxExpressionCompiler();
        string[] compileInputs = ["x * 2 + 1", "(x + 1) * (x - 1) / 2", "Max(x, 4) + 0.5", "-x * -(x - 3) / 4", Sum(30).Replace("x1", "x", StringComparison.Ordinal), "x +"];
        string[] vbCompileInputs = ["x * 2 + 1", "x / 2", "(x + 1) / 2", "(x + 1) * (x - 1) / 2", "-x * -(x - 3) / 4", "x ^ 2 - x", "x +"];
        string Evaluate(Func<double, double> function) => string.Join(",", new[] { -2.5, 0, 1, 3, 7 }.Select(v => function(v).ToString("R", CultureInfo.InvariantCulture)));

        // Every compilation gets its own parameter: only the compiler instance and the grammar are shared.
        Func<double, double> CompileC(CSyntaxExpressionCompiler compiler, string text)
            => (Func<double, double>)((LambdaExpression)compiler.CompileExpression<Func<double, double>>(text, [Expression.Parameter(typeof(double), "x")], typeof(Math), false)).Compile();
        Func<double, double> CompileVb(VBSyntaxExpressionCompiler compiler, string text)
            => (Func<double, double>)compiler.CompileExpression<Func<double, double>>(text, [Expression.Parameter(typeof(double), "x")]).Compile();
        yield return new Surface("compile-csyntax",
            () => input => Evaluate(CompileC(sharedCompiler, input)),
            () => { var p = new CSyntaxTokenParser(); return input => RenderTree(p.Parse(input)); },
            input => SafeRender(text => Evaluate(CompileC(new CSyntaxExpressionCompiler(), text)), input),
            input => SafeRender(text => RenderTree(new CompiledGrammar(cDefinition).Parse(text)), input),
            compileInputs);
        yield return new Surface("compile-vbsyntax",
            () => input => Evaluate(CompileVb(sharedVbCompiler, input)),
            () => input => RenderTree(new VBSyntaxTokenParser().Parse(input)),
            input => SafeRender(text => Evaluate(CompileVb(new VBSyntaxExpressionCompiler(), text)), input),
            input => SafeRender(text => RenderTree(new CompiledGrammar(vbDefinition).Parse(text)), input),
            vbCompileInputs);
    }

    /// <summary>Runs the throughput and allocation benchmark.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>0.</returns>
    public static int RunBench(string[] args)
    {
        int ms = int.Parse(Option(args, "--ms") ?? "1500", CultureInfo.InvariantCulture);
        int[] threadCounts = (Option(args, "--threads") ?? "1,2,4,8,16").Split(',').Select(t => int.Parse(t, CultureInfo.InvariantCulture)).ToArray();
        string mode = Option(args, "--mode") ?? "shared";
        string label = Option(args, "--label") ?? "run";

        Type cGrammar = typeof(CSyntaxTokenParser).Assembly.GetType("Utils.Expressions.CSyntax.Runtime.CSyntaxTokenizerGrammar", true)!;
        var definition = (Utils.Parser.Model.ParserDefinition)cGrammar.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
        var shared = new CompiledGrammar(definition);

        // Construction cost of one CompiledGrammar (engines included) and of one fresh pair of engines.
        MeasureConstruction(label, "compiled-grammar", () => new CompiledGrammar(definition));
        MeasureConstruction(label, "lexer-engine", () => new LexerEngine(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default));
        MeasureConstruction(label, "parser-engine", () => new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default));

        (string Name, string Input, bool Parse)[] workloads =
        [
            ("short", "a + b * c", true),
            ("medium", "f(x, y[2]).z - (1 + 2) * 3 + (a < b ? c : d) * g(h, i)", true),
            ("deep", Nest(15), true),
            ("flat", Sum(200), true),
            ("tokenize", Sum(200), false),
            ("mixed", "", true),
        ];

        foreach (var workload in workloads)
        {
            foreach (int threads in threadCounts)
            {
                // shared: one CompiledGrammar for every worker (only meaningful when it is thread-safe);
                // perthread: one CompiledGrammar per worker (the sequentially correct parallel baseline);
                // percall: one CompiledGrammar per operation.
                Func<int, CompiledGrammar> grammarFor = mode switch
                {
                    "shared" => _ => shared,
                    "perthread" => _ => new CompiledGrammar(definition),
                    "percall" => _ => null!,
                    _ => throw new ArgumentException($"Unknown mode '{mode}'."),
                };

                long operations = 0;
                using var barrier = new Barrier(threads + 1);
                bool stop = false;
                var workers = new Thread[threads];
                for (int w = 0; w < threads; w++)
                {
                    int worker = w;
                    workers[w] = new Thread(() =>
                    {
                        CompiledGrammar? grammar = grammarFor(worker);
                        long local = 0;
                        barrier.SignalAndWait();
                        while (!Volatile.Read(ref stop))
                        {
                            CompiledGrammar g = grammar ?? new CompiledGrammar(definition);
                            if (workload.Name == "mixed")
                            {
                                _ = (local & 1) == 0 ? g.Parse("f(x, y[2]).z - (1 + 2) * 3") : (object)g.Tokenize(Sum(40));
                            }
                            else if (workload.Parse)
                            {
                                _ = g.Parse(workload.Input);
                            }
                            else
                            {
                                _ = g.Tokenize(workload.Input);
                            }

                            local++;
                        }

                        Interlocked.Add(ref operations, local);
                        barrier.SignalAndWait();
                    }, 4 * 1024 * 1024);
                    workers[w].Start();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long allocatedBefore = GC.GetTotalAllocatedBytes(true);
                barrier.SignalAndWait();
                var watch = Stopwatch.StartNew();
                Thread.Sleep(ms);
                Volatile.Write(ref stop, true);
                barrier.SignalAndWait();
                watch.Stop();
                long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
                foreach (Thread thread in workers) thread.Join();
                long peak = Process.GetCurrentProcess().PeakWorkingSet64;
                double perSecond = operations / watch.Elapsed.TotalSeconds;
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"CBENCH label={label} mode={mode} workload={workload.Name} threads={threads} ops={operations} opsPerSec={perSecond:F0} usPerOpPerThread={threads * 1e6 / Math.Max(1, perSecond):F2} bytesPerOp={allocated / Math.Max(1, operations)} peakWorkingSetMiB={peak / (1024 * 1024)}"));
            }
        }

        return 0;
    }

    /// <summary>Converts an ANTLR4 grammar file and prints the conversion diagnostics (campaign grammar development aid).</summary>
    /// <param name="path">The grammar file, or <c>-</c> for the built-in mode grammar.</param>
    /// <returns>0 when the grammar converts, 1 otherwise.</returns>
    public static int CheckGrammar(string path)
    {
        var diagnostics = new Utils.Parser.Diagnostics.DiagnosticBag();
        try
        {
            _ = Antlr4GrammarConverter.Parse(path == "-" ? ModeGrammar : System.IO.File.ReadAllText(path), diagnostics);
            Console.WriteLine("grammar ok");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.GetType().Name + ": " + exception.Message + " (diagnostics: " + diagnostics.Count() + ")");
            return 1;
        }
    }

    /// <summary>
    /// Separates the cost of a fresh <see cref="ParserEngine"/> per parse from the parse itself: the same pre-tokenized
    /// input is parsed by one reused engine and by a new engine per parse (single thread), printing one
    /// <c>CENGINE</c> line per input and variant.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <returns>0.</returns>
    public static int RunEngineReuse(string[] args)
    {
        int ms = int.Parse(Option(args, "--ms") ?? "1500", CultureInfo.InvariantCulture);
        Type cGrammar = typeof(CSyntaxTokenParser).Assembly.GetType("Utils.Expressions.CSyntax.Runtime.CSyntaxTokenizerGrammar", true)!;
        var definition = (Utils.Parser.Model.ParserDefinition)cGrammar.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
        var grammar = new CompiledGrammar(definition);
        (string Name, string Input)[] inputs = [("short", "a + b * c"), ("medium", "f(x, y[2]).z - (1 + 2) * 3 + (a < b ? c : d) * g(h, i)"), ("deep", Nest(15)), ("flat", Sum(200))];
        foreach (var (name, input) in inputs)
        {
            IReadOnlyList<Token> tokens = grammar.Tokenize(input);
            var reused = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default);
            foreach (string variant in new[] { "reused", "fresh", "reused", "fresh" })
            {
                Func<ParserEngine> engine = variant == "reused" ? () => reused : () => new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default);
                for (int i = 0; i < 20; i++) _ = engine().Parse(tokens);
                GC.Collect();
                long before = GC.GetTotalAllocatedBytes(true);
                long count = 0;
                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < ms)
                {
                    _ = engine().Parse(tokens);
                    count++;
                }

                watch.Stop();
                long allocated = GC.GetTotalAllocatedBytes(true) - before;
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"CENGINE input={name} variant={variant} ops={count} usPerOp={watch.Elapsed.TotalMilliseconds * 1000 / count:F1} bytesPerOp={allocated / count}"));
            }
        }

        return 0;
    }

    /// <summary>Measures the time and allocation of a construction.</summary>
    /// <param name="label">The run label.</param>
    /// <param name="name">The measured construction.</param>
    /// <param name="create">The construction.</param>
    private static void MeasureConstruction(string label, string name, Func<object> create)
    {
        for (int i = 0; i < 20; i++) _ = create();
        const int Count = 200;
        long before = GC.GetTotalAllocatedBytes(true);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < Count; i++) _ = create();
        watch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(true) - before;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"CBENCH-CTOR label={label} construct={name} usPerConstruct={watch.Elapsed.TotalMilliseconds * 1000 / Count:F2} bytesPerConstruct={allocated / Count}"));
    }

    /// <summary>Renders the structure, node types, rule names, modes, spans, token metadata and error messages of a tree.</summary>
    /// <param name="node">The root.</param>
    /// <returns>The canonical rendering.</returns>
    private static string RenderTree(ParseNode node)
    {
        var builder = new StringBuilder();
        var pending = new Stack<(ParseNode Node, int Depth)>();
        pending.Push((node, 0));
        while (pending.Count > 0)
        {
            (ParseNode current, int depth) = pending.Pop();
            builder.Append(depth).Append(':').Append(current.GetType().Name).Append(':').Append(current.Rule?.Name)
                .Append(':').Append(current.ModeName).Append('@').Append(current.Span.Position).Append('+').Append(current.Span.Length)
                .Append('L').Append(current.Span.Line).Append('C').Append(current.Span.Column);
            switch (current)
            {
                case LexerNode lexer:
                    builder.Append('[').Append(lexer.Token.RuleName).Append('|').Append(lexer.Token.Channel).Append('|').Append(lexer.Token.Text).Append(']');
                    break;
                case ErrorNode error:
                    builder.Append("{").Append(error.Message).Append('}');
                    break;
            }

            builder.Append(';');
            if (current is ParserNode parserNode)
            {
                for (int index = parserNode.Children.Count - 1; index >= 0; index--) pending.Push((parserNode.Children[index], depth + 1));
            }
        }

        return builder.ToString();
    }

    /// <summary>Renders a token list with every token field.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The canonical rendering.</returns>
    private static string RenderTokens(IReadOnlyList<Token> tokens)
        => string.Join("|", tokens.Select(t => string.Create(CultureInfo.InvariantCulture,
            $"{t.RuleName}:{t.ModeName}:{t.Channel}:{t.Text}@{t.Span.Position}+{t.Span.Length}L{t.Span.Line}C{t.Span.Column}")));

    /// <summary>Renders an exception: refusals with their full classification, other exceptions with type and message.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The rendering, starting with <c>!</c>.</returns>
    private static string RenderException(Exception exception) => exception is ParserResourceLimitException refusal
        ? string.Create(CultureInfo.InvariantCulture, $"!refusal:{refusal.Kind}:{refusal.Phase}:{refusal.Limit}:{refusal.Observed}:{refusal.Offset}")
        : "!" + exception.GetType().Name + ":" + exception.Message;

    /// <summary>Runs a rendering and renders its exception, if any.</summary>
    /// <param name="render">The rendering.</param>
    /// <param name="input">The input.</param>
    /// <returns>The rendering.</returns>
    private static string SafeRender(Func<string, string> render, string input)
    {
        try
        {
            return render(input);
        }
        catch (Exception exception)
        {
            return RenderException(exception);
        }
    }

    /// <summary>Builds <c>x0 + x1 + ...</c>.</summary>
    /// <param name="count">The number of terms.</param>
    /// <returns>The text.</returns>
    private static string Sum(int count) => string.Join(" + ", Enumerable.Range(0, count).Select(i => "x" + i.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Builds <paramref name="depth"/> nested parentheses around a binary expression.</summary>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The text.</returns>
    private static string Nest(int depth) => new string('(', depth) + "a + b" + new string(')', depth);

    /// <summary>Reads an option value.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="name">The option name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private static string? Option(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
