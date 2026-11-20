using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Expressions.VBSyntax.Runtime;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Runtime;

namespace ExpressionResourceProbe;

/// <summary>
/// Registry-metadata probe (<c>parser-registry</c>): parses one scenario with a directly constructed
/// <see cref="ParserEngine"/> and prints one <c>RRESULT</c> line with the work counters, the sizes of the parse-scoped
/// registry collections at their peak, the live memory at that peak and after the parse, the elapsed time, the
/// allocations, and SHA-256 digests of the parse tree, of the diagnostics and of the action/predicate trace, so two builds
/// can be compared differentially.
/// </summary>
/// <remarks>
/// Usage: <c>parser-registry &lt;scenario&gt; &lt;size&gt; [--peak] [--breakdown] [--lookahead] [--diagnostics] [--repeat N] [--max-retained N]</c>.
/// <list type="bullet">
/// <item><c>--peak</c> forces a full collection when the root rule invocation exits, which is the point where every
/// registry collection holds its final content (the engine clears them right after). It distorts the elapsed time, so
/// timing runs omit it.</item>
/// <item><c>--lookahead</c> implies <c>--peak</c> and appends the look-ahead cache characterization and content digest of
/// <see cref="LookaheadCacheStats"/>, read after the live peak is measured.</item>
/// <item><c>--diagnostics</c> parses with a <see cref="DiagnosticBag"/> (which disables the negative look-ahead shortcut)
/// and digests the diagnostics.</item>
/// <item><c>--max-retained N</c> sets <see cref="ParserResourceLimits.MaxRetainedStates"/>; the refusal is classified.</item>
/// </list>
/// Registry sizes are read by reflection and accept both the historical layout (nested continuation sets, one result
/// list per invocation, one visited-state set) and the compact layout introduced by <c>perf/parser-registry-metadata</c>.
/// </remarks>
internal static class RegistryProbe
{
    /// <summary>The probe scenarios: name and factory of the grammar text, input text and policy kind.</summary>
    private static readonly IReadOnlyDictionary<string, Func<int, RegistryScenario>> Catalog = new Dictionary<string, Func<int, RegistryScenario>>(StringComparer.Ordinal)
    {
        ["csyntax-sum"] = n => new(Dialect: "CSyntax", Grammar: null, Text: Sum(n), Stateful: false),
        ["csyntax-mixed"] = n => new(Dialect: "CSyntax", Grammar: null, Text: Mixed(n), Stateful: false),
        ["csyntax-parens"] = n => new(Dialect: "CSyntax", Grammar: null, Text: new string('(', n) + "x0" + new string(')', n), Stateful: false),
        ["vbsyntax-sum"] = n => new(Dialect: "VBSyntax", Grammar: null, Text: Sum(n), Stateful: false),
        ["vbsyntax-mixed"] = n => new(Dialect: "VBSyntax", Grammar: null, Text: Mixed(n, visualBasic: true), Stateful: false),
        ["g-alternatives"] = n => new(null, ManyAlternativesGrammar, string.Join(" ", Enumerable.Range(0, n).Select(i => "k" + (i % 20).ToString(CultureInfo.InvariantCulture) + " a"))),
        ["g-prefix"] = n => new(null, CommonPrefixGrammar, string.Join(" ", Enumerable.Range(0, n).Select(i => (i % 4) switch { 0 => "a b c d", 1 => "a b c e", 2 => "a b f", _ => "a g" }))),
        ["g-ambiguous"] = n => new(null, AmbiguousGrammar, Repeat("a ", n)),
        ["g-leftrec-sum"] = n => new(null, LeftRecursiveGrammar, string.Join(" + ", Enumerable.Repeat("a", Math.Max(1, n)))),
        ["g-leftrec-mixed"] = n => new(null, LeftRecursivePrecedenceGrammar, string.Join(" ", Enumerable.Range(0, Math.Max(1, n)).Select(i => (i == 0 ? string.Empty : (i % 3 == 0 ? "* " : "+ ")) + (i % 5 == 0 ? "( a + a )" : "a")))),
        ["g-deep"] = n => new(null, RuleChainGrammar, new string('[', n) + "a" + new string(']', n)),
        ["vbsyntax-text"] = _ => new(Dialect: "VBSyntax", Grammar: null, Text: Environment.GetEnvironmentVariable("PROBE_TEXT") ?? "x0", Stateful: false),
        ["csyntax-text"] = _ => new(Dialect: "CSyntax", Grammar: null, Text: Environment.GetEnvironmentVariable("PROBE_TEXT") ?? "x0", Stateful: false),
        ["g-epsilon"] = n => new(null, EpsilonGrammar, string.Join(" ", Enumerable.Range(0, n).Select(i => (i % 6) switch { 0 => "a", 1 => "x a", 2 => "b", 3 => "b x", 4 => "c d", _ => "d" }))),
        ["g-stateful"] = n => new(null, StatefulGrammar, string.Join(" ", Enumerable.Range(0, n).Select(i => (i % 3) switch { 0 => "a", 1 => "a = a", _ => "a ;" })), Stateful: true),
    };

    /// <summary>Twenty keyword-led alternatives and a fallback, so every item attempts many alternatives.</summary>
    private static readonly string ManyAlternativesGrammar =
        "grammar K;\ns : x* ;\nx : "
        + string.Join(" | ", Enumerable.Range(0, 20).Select(i => "K" + i.ToString(CultureInfo.InvariantCulture) + " NAME"))
        + " | NAME ;\n"
        + string.Concat(Enumerable.Range(0, 20).Select(i => "K" + i.ToString(CultureInfo.InvariantCulture) + " : 'k" + i.ToString(CultureInfo.InvariantCulture) + "' ;\n"))
        + "NAME : ('a'..'z')+ ;\nWS : (' ' | '\\t' | '\\r' | '\\n')+ -> skip ;\n";

    /// <summary>Alternatives sharing common prefixes, so failed alternatives consume tokens before backtracking.</summary>
    private const string CommonPrefixGrammar = """
        grammar P;
        s : x* ;
        x : 'a' 'b' 'c' 'd' | 'a' 'b' 'c' 'e' | 'a' 'b' 'f' | 'a' 'g' ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Highly ambiguous grammar over a flat input (heavy backtracking).</summary>
    private const string AmbiguousGrammar = """
        grammar M;
        s : t* ;
        t : 'a' | 'a' 'a' | u ;
        u : 'a' t? ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

/// <summary>
    /// Empty alternatives, optional items and a nested alternation with an empty branch, so probes report
    /// <c>EpsilonPossible</c> and nested-alternation cursor contexts are cached.
    /// </summary>
    private const string EpsilonGrammar = """
        grammar Eps;
        s : item* ;
        item : opt 'a' | 'b' opt | ( 'c' | 'e' )? 'd' ;
        opt : 'x'? ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Direct left recursion.</summary>
    private const string LeftRecursiveGrammar = """
        grammar L;
        e : e '+' e | '(' e ')' | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Direct left recursion with two precedence levels.</summary>
    private const string LeftRecursivePrecedenceGrammar = """
        grammar LP;
        e : e '*' e | e '+' e | '(' e ')' | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Deep recursion through a chain of rules.</summary>
    private const string RuleChainGrammar = """
        grammar R;
        e : f ;
        f : g ;
        g : '[' e ']' | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>
    /// Actions and semantic predicates driven by a counter execution state: every action changes the state key, so
    /// memoization must restore post-rule snapshots, and the predicates depend on the restored state.
    /// </summary>
    private const string StatefulGrammar = """
        grammar S;
        s : item* ;
        item : assign | stmt | expr ;
        assign : NAME '=' {bump} expr ;
        stmt : expr ';' {bump} ;
        expr : {even}? NAME {bump} | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Runs one scenario and prints the <c>RRESULT</c> line.</summary>
    /// <param name="args">The command line (<c>parser-registry scenario size ...</c>).</param>
    /// <returns>0 on success, 13 on a resource refusal, 12 on any other exception.</returns>
    public static int Run(string[] args)
    {
        string name = args[1];
        int size = int.Parse(args[2], CultureInfo.InvariantCulture);
        bool breakdown = Array.IndexOf(args, "--breakdown") >= 0;
        bool lookahead = Array.IndexOf(args, "--lookahead") >= 0;
        bool peak = breakdown || lookahead || Array.IndexOf(args, "--peak") >= 0;
        bool withDiagnostics = Array.IndexOf(args, "--diagnostics") >= 0;
        int repeat = Option(args, "--repeat") is { } r ? int.Parse(r, CultureInfo.InvariantCulture) : 1;
        long? maxRetained = Option(args, "--max-retained") is { } m ? long.Parse(m, CultureInfo.InvariantCulture) : null;
        if (!Catalog.TryGetValue(name, out var factory)) throw new ArgumentException($"Unknown registry scenario '{name}'.");

        RegistryScenario scenario = factory(size);
        Utils.Parser.Model.ParserDefinition definition = scenario.Dialect switch
        {
            "CSyntax" => SharedDefinition(typeof(CSyntaxTokenParser)),
            "VBSyntax" => SharedDefinition(typeof(VBSyntaxTokenParser)),
            _ => Antlr4GrammarConverter.Parse(scenario.Grammar!),
        };
        ParserResourceLimits limits = maxRetained is { } max ? ParserResourceLimits.Default with { MaxRetainedStates = max } : ParserResourceLimits.Default;
        IReadOnlyList<Token> tokens = new CompiledGrammar(definition).Tokenize(scenario.Text);

        // Warm-up on the same input so JIT time is not measured; it also checks the run is deterministic.
        string? warmUpTree = null;
        for (int i = 0; i < 1; i++)
        {
            var warm = RunOnce(definition, limits, tokens, scenario.Stateful, withDiagnostics, peak: false);
            warmUpTree = warm.TreeDigest;
        }

        var times = new List<double>();
        var allocations = new List<long>();
        RunResult last = default;
        for (int i = 0; i < repeat; i++)
        {
            last = RunOnce(definition, limits, tokens, scenario.Stateful, withDiagnostics, peak, breakdown, lookahead);
            times.Add(last.Milliseconds);
            allocations.Add(last.Allocated);
            if (last.TreeDigest != warmUpTree) throw new InvalidOperationException("Non-deterministic parse tree between runs.");
        }

        times.Sort();
        Console.WriteLine(string.Join(' ',
            $"RRESULT scenario={name} size={size} diagnostics={withDiagnostics} peak={peak} repeat={repeat} maxRetained={(maxRetained?.ToString(CultureInfo.InvariantCulture) ?? "default")}",
            $"os={(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux")} runtime={RuntimeInformation.FrameworkDescription.Replace(' ', '_')}",
            $"tokens={tokens.Count} outcome={last.Outcome}",
            $"steps={last.Usage?.Steps.ToString(CultureInfo.InvariantCulture) ?? "-"} nodes={last.Usage?.Nodes.ToString(CultureInfo.InvariantCulture) ?? "-"} retained={last.Usage?.Retained.ToString(CultureInfo.InvariantCulture) ?? "-"} depth={last.Usage?.Depth.ToString(CultureInfo.InvariantCulture) ?? "-"}",
            last.Sizes.Format(),
            $"liveBefore={last.LiveBefore} livePeak={(peak ? last.LivePeak.ToString(CultureInfo.InvariantCulture) : "-")} liveAfter={last.LiveAfter} peakRegistry={(peak ? (last.LivePeak - last.LiveAfter).ToString(CultureInfo.InvariantCulture) : "-")}",
            $"allocated={allocations.Min()} allocatedMax={allocations.Max()}",
            $"msMin={times[0].ToString("0.###", CultureInfo.InvariantCulture)} msMedian={times[times.Count / 2].ToString("0.###", CultureInfo.InvariantCulture)}",
            $"actions={last.ActionCount} tree={last.TreeDigest} diag={last.DiagnosticDigest} trace={last.TraceDigest}",
            last.Breakdown,
            last.Lookahead));
        return last.Outcome.StartsWith("resource-limit", StringComparison.Ordinal) ? 13 : last.Outcome.StartsWith("exception", StringComparison.Ordinal) ? 12 : 0;
    }

    /// <summary>Parses the tokens once on a fresh engine and measures it.</summary>
    /// <param name="definition">The grammar definition.</param>
    /// <param name="limits">The resource limits.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="stateful">Whether to install the counter state manager, action executor and predicate evaluator.</param>
    /// <param name="withDiagnostics">Whether to collect diagnostics.</param>
    /// <param name="peak">Whether to measure the live memory and the registry sizes when the root rule exits.</param>
    /// <returns>The measurements.</returns>
    /// <param name="breakdown">Whether to release the collections one by one at the root exit and report the live memory each one held (destructive: the measured parse is finished at that point).</param>
    /// <param name="lookahead">Whether to characterize the look-ahead cache at the root exit (<see cref="LookaheadCacheStats"/>).</param>
    private static RunResult RunOnce(Utils.Parser.Model.ParserDefinition definition, ParserResourceLimits limits, IReadOnlyList<Token> tokens, bool stateful, bool withDiagnostics, bool peak, bool breakdown = false, bool lookahead = false)
    {
        var trace = new StringBuilder();
        var state = new CounterState(trace);
        var frames = new PeakFrameManager();
        ParserRuntimeFeaturePolicy policy = ParserRuntimeFeaturePolicy.Default with { RuleInvocationFrameManager = frames };
        if (stateful)
        {
            policy = policy with { ExecutionStateManager = state, ParserActionExecutor = state, SemanticPredicateEvaluator = state };
        }

        var engine = new ParserEngine(definition, policy, limits);
        long livePeak = 0;
        RegistrySizes sizes = RegistrySizes.Unknown;
        string breakdownText = "breakdown=-";
        string lookaheadText = "lookaheadDigest=-";
        if (peak)
        {
            frames.OnRootExit = () =>
            {
                // The last root exit wins: it is the one that follows the final completed-result insertion.
                sizes = ReadRegistry(engine);
                livePeak = GC.GetTotalMemory(forceFullCollection: true);
                if (lookahead) lookaheadText = LookaheadCacheStats.Describe(engine);
                if (breakdown) breakdownText = Breakdown(engine, livePeak);
            };
        }

        var diagnostics = withDiagnostics ? new DiagnosticBag() : null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long liveBefore = GC.GetTotalMemory(forceFullCollection: true);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        ParseNode? tree = null;
        string outcome;
        try
        {
            tree = engine.Parse(tokens, diagnostics: diagnostics);
            outcome = tree is ErrorNode ? "error-node" : "ok";
        }
        catch (ParserResourceLimitException exception)
        {
            outcome = $"resource-limit:{exception.Phase}/{exception.Kind}/limit={exception.Limit}/observed={exception.Observed}/offset={exception.Offset?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
        }
        catch (Exception exception)
        {
            outcome = "exception:" + exception.GetType().Name;
        }

        stopwatch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        long liveAfter = GC.GetTotalMemory(forceFullCollection: true);
        UsageCounters? usage = ReadUsage(engine);
        string treeDigest = Digest(tree is null ? "<none>" : Render(tree));
        string diagnosticDigest = diagnostics is null ? "-" : Digest(string.Join("\n", diagnostics.Select(d => $"{d.Code}|{d.RuleName}|{d.Message}|{d.Span}")));
        GC.KeepAlive(tree);
        return new RunResult(outcome, usage, sizes, liveBefore, livePeak, liveAfter, allocated, stopwatch.Elapsed.TotalMilliseconds, treeDigest, diagnosticDigest, Digest(trace.ToString()), state.Actions, breakdownText, lookaheadText);
    }

    /// <summary>
    /// Releases the registry and look-ahead collections one at a time (every collection field of the registry, then the
    /// look-ahead cache) and reports the live bytes each release freed.
    /// </summary>
    /// <param name="engine">The engine, at the root exit.</param>
    /// <param name="live">The live bytes before the first release.</param>
    /// <returns>The <c>breakdown=field:bytes,...</c> text.</returns>
    private static string Breakdown(ParserEngine engine, long live)
    {
        var parts = new List<string>();
        object registry = Field(engine, "_stateRegistry")!;
        var targets = registry.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(field => (Name: field.Name, Value: field.GetValue(registry)))
            .Concat(LookaheadCollections(Field(engine, "_lookaheadCache")!));
        foreach ((string name, object? value) in targets)
        {
            if (value is null) continue;
            value.GetType().GetMethod("Clear", Type.EmptyTypes)?.Invoke(value, null);
            value.GetType().GetMethod("TrimExcess", Type.EmptyTypes)?.Invoke(value, null);
            long now = GC.GetTotalMemory(forceFullCollection: true);
            parts.Add(name.TrimStart('_') + ":" + (live - now).ToString(CultureInfo.InvariantCulture));
            live = now;
        }

        return "breakdown=" + string.Join(',', parts);
    }

    /// <summary>Renders a parse tree canonically (node kind, rule, span, mode, token rule, channel and text).</summary>
    /// <param name="root">The root.</param>
    /// <returns>The rendering.</returns>
    private static string Render(ParseNode root)
    {
        var builder = new StringBuilder();
        var stack = new Stack<(ParseNode Node, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            (ParseNode node, int depth) = stack.Pop();
            builder.Append(depth).Append(' ').Append(node.GetType().Name).Append(' ').Append(node.Rule?.Name).Append(' ')
                .Append(node.Span.Position).Append('+').Append(node.Span.Length).Append(' ').Append(node.ModeName);
            if (node is LexerNode lexer) builder.Append(' ').Append(lexer.Token.RuleName).Append(' ').Append(lexer.Token.Channel).Append(' ').Append(lexer.Token.Text);
            if (node is ErrorNode error) builder.Append(' ').Append(error.Message);
            builder.Append('\n');
            if (node is ParserNode parser)
            {
                for (int i = parser.Children.Count - 1; i >= 0; i--) stack.Push((parser.Children[i], depth + 1));
            }
        }

        return builder.ToString();
    }

    /// <summary>Computes a short SHA-256 digest.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The first 16 hexadecimal characters.</returns>
    private static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    /// <summary>Reads the parse counters of the engine's last operation.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The counters, or <see langword="null"/> when unavailable.</returns>
    private static UsageCounters? ReadUsage(ParserEngine engine)
    {
        object? usage = engine.GetType().GetProperty("LastResourceUsage", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(engine);
        if (usage is null) return null;
        long Read(string property) => Convert.ToInt64(usage.GetType().GetProperty(property)!.GetValue(usage), CultureInfo.InvariantCulture);
        return new UsageCounters(Read("Steps"), Read("Nodes"), Read("RetainedStates"), Read("MaxDepth"));
    }

    /// <summary>Reads the registry and look-ahead sizes of an engine, historical or compact layout.</summary>
    /// <param name="engine">The engine, during the parse.</param>
    /// <returns>The sizes.</returns>
    private static RegistrySizes ReadRegistry(ParserEngine engine)
    {
        object registry = Field(engine, "_stateRegistry")!;
        object cache = Field(engine, "_lookaheadCache")!;
        long lookahead = LookaheadCount(cache);

        // Historical layout: HashSet<ParserStateKey>, Dictionary<RuleInvocationKey, HashSet<ContinuationKey>>,
        // Dictionary<RuleInvocationKey, List<ParserRuleResult>>.
        if (Field(registry, "_visitedStates") is IEnumerable visitedSet && Field(registry, "_continuations") is IDictionary nested)
        {
            long continuationPairs = 0;
            foreach (object? set in nested.Values) continuationPairs += ((IEnumerable)set!).Cast<object>().LongCount();
            var completed = (IDictionary)Field(registry, "_completedResults")!;
            long results = 0;
            foreach (object? list in completed.Values) results += ((ICollection)list!).Count;
            return new RegistrySizes(visitedSet.Cast<object>().LongCount(), -1, nested.Count, continuationPairs, completed.Count, results, lookahead);
        }

        // Compact layout: internal count properties.
        long Count(string property) => Convert.ToInt64(registry.GetType().GetProperty(property, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.GetValue(registry), CultureInfo.InvariantCulture);
        return new RegistrySizes(Count("VisitedStateCount"), Count("VisitedStateGroupCount"), Count("ContinuationInvocationCount"), Count("ContinuationCount"), Count("CompletedInvocationCount"), Count("CompletedResultCount"), lookahead);
    }

    /// <summary>Reads the number of look-ahead entries of the historical (<c>_results</c>) or compact (<c>Count</c>) cache layout.</summary>
    /// <param name="cache">The look-ahead cache.</param>
    /// <returns>The number of entries.</returns>
    internal static long LookaheadCount(object cache)
        => Field(cache, "_results") is ICollection results
            ? results.Count
            : Convert.ToInt64(cache.GetType().GetProperty("Count", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.GetValue(cache), CultureInfo.InvariantCulture);

    /// <summary>
    /// Lists the collection fields of the look-ahead cache, named <c>lookahead</c> for the historical single dictionary and
    /// <c>lookahead.&lt;field&gt;</c> for every collection of another layout.
    /// </summary>
    /// <param name="cache">The look-ahead cache.</param>
    /// <returns>The named collections.</returns>
    private static IEnumerable<(string Name, object? Value)> LookaheadCollections(object cache)
    {
        if (Field(cache, "_results") is { } results) return [("_lookahead", results)];
        return cache.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => typeof(ICollection).IsAssignableFrom(field.FieldType))
            .Select(field => ("_lookahead." + field.Name.TrimStart('_'), field.GetValue(cache)));
    }

    /// <summary>Reads a private instance field by name.</summary>
    /// <param name="target">The object.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null"/> when the field does not exist.</returns>
    private static object? Field(object target, string name)
        => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);

    /// <summary>Reads the parser definition of a dialect's shared lazily-initialized grammar.</summary>
    /// <param name="parserType">The public token parser type.</param>
    /// <returns>The definition.</returns>
    private static Utils.Parser.Model.ParserDefinition SharedDefinition(Type parserType)
    {
        FieldInfo field = parserType.GetField("SharedGrammar", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(parserType.Name, "SharedGrammar");
        return ((Lazy<CompiledGrammar>)field.GetValue(null)!).Value.Definition;
    }

    /// <summary>Reads the value following <paramref name="name"/>.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="name">The option name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private static string? Option(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>Builds <c>x0 + x1 + ... + x(n-1)</c>.</summary>
    /// <param name="n">The term count.</param>
    /// <returns>The text.</returns>
    private static string Sum(int n) => string.Join(" + ", Enumerable.Range(0, Math.Max(1, n)).Select(i => "x" + i.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Builds a mixed expression of calls, member accesses, indexers, products and logical comparisons.</summary>
    /// <param name="n">The term count.</param>
    /// <param name="visualBasic">Whether to use the VBSyntax spelling of indexers and logical operators.</param>
    /// <returns>The text.</returns>
    private static string Mixed(int n, bool visualBasic = false)
    {
        var builder = new StringBuilder("x0");
        for (int i = 1; i < Math.Max(1, n); i++)
        {
            string term = (i % 5) switch
            {
                0 => $"f{i}(x{i}, {i})",
                1 => $"x{i}.Length",
                2 => $"(x{i} * {i})",
                3 => visualBasic ? $"x{i}({i})" : $"x{i}[{i}]",
                _ => visualBasic ? $"(x{i} > {i} And x{i} < {i + 9})" : $"(x{i} > {i} && x{i} < {i + 9})",
            };
            builder.Append(i % 2 == 0 ? " + " : " - ").Append(term);
        }

        return builder.ToString();
    }

    /// <summary>Repeats a fragment.</summary>
    /// <param name="fragment">The fragment.</param>
    /// <param name="count">The repetition count.</param>
    /// <returns>The text.</returns>
    private static string Repeat(string fragment, int count) => string.Concat(Enumerable.Repeat(fragment, Math.Max(0, count)));

    /// <summary>A probe scenario.</summary>
    /// <param name="Dialect"><c>CSyntax</c>, <c>VBSyntax</c>, or <see langword="null"/> for a custom grammar.</param>
    /// <param name="Grammar">The custom grammar text, or <see langword="null"/>.</param>
    /// <param name="Text">The input text.</param>
    /// <param name="Stateful">Whether the counter state policies are installed.</param>
    private sealed record RegistryScenario(string? Dialect, string? Grammar, string Text, bool Stateful = false);

    /// <summary>The parse counters of one operation.</summary>
    /// <param name="Steps">Parser steps.</param>
    /// <param name="Nodes">Parse nodes.</param>
    /// <param name="Retained">Retained states.</param>
    /// <param name="Depth">Maximum depth.</param>
    private sealed record UsageCounters(long Steps, long Nodes, long Retained, long Depth);

    /// <summary>The measurements of one parse.</summary>
    private readonly record struct RunResult(
        string Outcome,
        UsageCounters? Usage,
        RegistrySizes Sizes,
        long LiveBefore,
        long LivePeak,
        long LiveAfter,
        long Allocated,
        double Milliseconds,
        string TreeDigest,
        string DiagnosticDigest,
        string TraceDigest,
        long ActionCount,
        string Breakdown,
        string Lookahead);

    /// <summary>Registry and look-ahead sizes at the root exit.</summary>
    /// <param name="Visited">Distinct visited-state keys.</param>
    /// <param name="VisitedGroups">Visited-state groups of the compact layout (-1 for the historical layout).</param>
    /// <param name="ContinuationInvocations">Distinct invocations carrying continuations.</param>
    /// <param name="Continuations">Distinct (invocation, continuation) entries.</param>
    /// <param name="CompletedInvocations">Distinct memoized invocations.</param>
    /// <param name="CompletedResults">Memoized results.</param>
    /// <param name="Lookahead">Look-ahead cache entries.</param>
    private sealed record RegistrySizes(long Visited, long VisitedGroups, long ContinuationInvocations, long Continuations, long CompletedInvocations, long CompletedResults, long Lookahead)
    {
        /// <summary>Unknown sizes.</summary>
        public static RegistrySizes Unknown { get; } = new(-1, -1, -1, -1, -1, -1, -1);

        /// <summary>Formats the sizes.</summary>
        /// <returns>The <c>name=value</c> pairs.</returns>
        public string Format()
            => $"visited={Visited} visitedGroups={VisitedGroups} contInvocations={ContinuationInvocations} continuations={Continuations} memoKeys={CompletedInvocations} memoResults={CompletedResults} lookahead={Lookahead}";
    }

    /// <summary>Calls <see cref="OnRootExit"/> whenever the outermost rule invocation exits.</summary>
    private sealed class PeakFrameManager : IParserRuleInvocationFrameManager
    {
        /// <summary>The current rule depth.</summary>
        private int _depth;

        /// <summary>Gets or sets the callback run when the depth returns to zero.</summary>
        public Action? OnRootExit { get; set; }

        /// <inheritdoc />
        public ParserRuleInvocationFrame? Current => null;

        /// <inheritdoc />
        public ParserRuleInvocationFrame Enter(string ruleName, int inputPosition, ParserRuleInvocationDescriptor? descriptor = null)
        {
            _depth++;
            return NullParserRuleInvocationFrameManager.Instance.Enter(ruleName, inputPosition, descriptor);
        }

        /// <inheritdoc />
        public void Exit(ParserRuleInvocationFrame frame, bool succeeded)
        {
            _depth--;
            if (_depth == 0) OnRootExit?.Invoke();
        }
    }

    /// <summary>
    /// Counter execution state: <c>bump</c> actions increment it, <c>even</c> predicates accept only even values, the
    /// state key is the counter, and every action and predicate evaluation is appended to a trace.
    /// </summary>
    /// <param name="trace">The trace.</param>
    private sealed class CounterState(StringBuilder trace) : IParserExecutionStateManager, IParserActionExecutor, ISemanticPredicateEvaluator
    {
        /// <summary>The counter.</summary>
        private int _value;

        /// <summary>Gets the number of executed actions.</summary>
        public long Actions { get; private set; }

        /// <inheritdoc />
        public object Capture() => _value;

        /// <inheritdoc />
        public void Restore(object snapshot)
        {
            _value = (int)snapshot;
            trace.Append('R').Append(_value).Append(';');
        }

        /// <inheritdoc />
        public ParserExecutionStateKey GetCurrentStateKey() => new((ulong)_value);

        /// <inheritdoc />
        public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
        {
            _value++;
            Actions++;
            trace.Append('A').Append(context.Rule.Name).Append('@').Append(context.InputPosition).Append('/').Append(context.AlternativeIndex).Append('/').Append(context.ElementIndex).Append('=').Append(_value).Append(';');
            return ParserActionExecutionOutcome.Executed;
        }

        /// <inheritdoc />
        public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
        {
            bool satisfied = _value % 2 == 0;
            trace.Append('P').Append(context.Rule.Name).Append('@').Append(context.InputPosition).Append('=').Append(satisfied ? '1' : '0').Append(';');
            return satisfied ? SemanticPredicateEvaluationOutcome.Satisfied : SemanticPredicateEvaluationOutcome.Rejected;
        }
    }
}
