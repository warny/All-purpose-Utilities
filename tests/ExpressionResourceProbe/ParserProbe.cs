using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Expressions.VBSyntax.Runtime;
using Utils.Parser.Bootstrap;
using Utils.Parser.Runtime;

namespace ExpressionResourceProbe;

/// <summary>
/// S-06 parser resource probe: runs one <c>Utils.Parser</c> scenario per process and prints one <c>PRESULT</c> line with
/// input, work, tree and memory measurements. Deep scenarios may terminate the process with a stack overflow, which the
/// caller observes as an exit code; never call these scenarios from a test project.
/// </summary>
/// <remarks>
/// Usage: <c>ExpressionResourceProbe parser &lt;scenario&gt; &lt;size&gt; tokenize|parse|compile [--thread ...] [--plimits default|unlimited]</c>.
/// Rule depth, rule invocations and alternative attempts are observed through public runtime policies (a counting
/// invocation-frame manager and a passive runtime observer); registry, memo and look-ahead sizes are read by reflection
/// from the engine after the parse, so no production code is instrumented.
/// </remarks>
internal static class ParserProbe
{
    /// <summary>Exit code of a successful run.</summary>
    private const int ExitSuccess = 0;

    /// <summary>Exit code when <see cref="InsufficientExecutionStackException"/> was caught.</summary>
    private const int ExitInsufficientStack = 10;

    /// <summary>Exit code when <see cref="OutOfMemoryException"/> was caught.</summary>
    private const int ExitOutOfMemory = 11;

    /// <summary>Exit code for any other exception.</summary>
    private const int ExitOtherException = 12;

    /// <summary>Exit code of a controlled parser resource refusal.</summary>
    private const int ExitResourceLimit = 13;

    /// <summary>The scenario catalog: name, description and source/grammar factory.</summary>
    private static readonly IReadOnlyList<ParserScenario> Catalog =
    [
        new("P-csyntax-parens", "CSyntax '((...(x0)...))', n pairs", n => Dialect("CSyntax", new string('(', n) + "x0" + new string(')', n))),
        new("P-csyntax-unary", "CSyntax '- - ... - x0', n unary operators", n => Dialect("CSyntax", Repeat("- ", n) + "x0")),
        new("P-csyntax-sum", "CSyntax 'x0 + x1 + ... + x(n-1)'", n => Dialect("CSyntax", Sum(n))),
        new("P-csyntax-ident", "CSyntax identifier of n characters", n => Dialect("CSyntax", "x" + new string('a', Math.Max(0, n - 1)))),
        new("P-csyntax-string", "CSyntax string literal of n characters", n => Dialect("CSyntax", "\"" + new string('a', n) + "\"")),
        new("P-csyntax-invalid", "CSyntax 'x0 x1 ... x(n-1)' (invalid, n tokens)", n => Dialect("CSyntax", string.Join(" ", Enumerable.Range(0, n).Select(i => "x" + i)))),
        new("P-vbsyntax-parens", "VBSyntax '((...(x0)...))', n pairs", n => Dialect("VBSyntax", new string('(', n) + "x0" + new string(')', n))),
        new("P-vbsyntax-unary", "VBSyntax '- - ... - x0', n unary operators", n => Dialect("VBSyntax", Repeat("- ", n) + "x0")),
        new("P-vbsyntax-sum", "VBSyntax 'x0 + x1 + ... + x(n-1)'", n => Dialect("VBSyntax", Sum(n))),
        new("P-g-unary", "grammar e : '-' e | NAME ; input '- ... - a'", n => Grammar(UnaryGrammar, Repeat("- ", n) + "a")),
        new("P-g-rulechain", "grammar e : f ; f : g ; g : '[' e ']' | NAME ; input '[[...a...]]'", n => Grammar(RuleChainGrammar, new string('[', n) + "a" + new string(']', n))),
        new("P-g-quantifier", "grammar l : '(' i* ')' ; i : l | NAME ; input '((...(a)...))'", n => Grammar(QuantifierGrammar, new string('(', n) + "a" + new string(')', n))),
        new("P-g-alternation", "grammar e : ( '(' ( e | NAME ) ')' | NAME ) ; inline alternations, input '((...a...))'", n => Grammar(AlternationGrammar, new string('(', n) + "a" + new string(')', n))),
        new("P-g-leftrec", "grammar e : e '+' e | '(' e ')' | NAME ; input '((...(a)...))'", n => Grammar(LeftRecursiveGrammar, new string('(', n) + "a" + new string(')', n))),
        new("P-g-leftrec-sum", "grammar e : e '+' e | '(' e ')' | NAME ; input 'a + a + ... + a'", n => Grammar(LeftRecursiveGrammar, string.Join(" + ", Enumerable.Repeat("a", Math.Max(1, n))))),
        new("P-g-epsilon", "grammar e : z '(' e ')' z | NAME ; z : ( 'q' )? ; (zero-width levels), input '((...a...))'", n => Grammar(EpsilonGrammar, new string('(', n) + "a" + new string(')', n))),
        new("P-g-ambiguous", "grammar s : t* EOF-free ; t : 'a' | 'a' 'a' | u ; u : 'a' t? ; input 'a a ... a'", n => Grammar(AmbiguousGrammar, Repeat("a ", n))),
        new("P-g-lexrec", "lexer NEST : '(' NEST? ')' ; input '((...))'", n => Grammar(LexerRecursiveGrammar, new string('(', n) + new string(')', n))),
        new("P-g-hidden", "grammar with a HIDDEN channel; input 'a /*c*/ a ...', n tokens", n => Grammar(HiddenChannelGrammar, Repeat("a /*c*/ ", n))),
    ];

    /// <summary>Right-recursive unary grammar.</summary>
    private const string UnaryGrammar = """
        grammar U;
        e : '-' e | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Recursion through a chain of distinct rule names that consume nothing themselves.</summary>
    private const string RuleChainGrammar = """
        grammar R;
        e : f ;
        f : g ;
        g : '[' e ']' | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Recursion through a quantifier body.</summary>
    private const string QuantifierGrammar = """
        grammar Q;
        l : '(' i* ')' | NAME ;
        i : l | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Recursion through nested inline alternations of one rule.</summary>
    private const string AlternationGrammar = """
        grammar A;
        e : ( '(' ( e | NAME ) ')' | NAME ) ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Direct left recursion with a parenthesized primary.</summary>
    private const string LeftRecursiveGrammar = """
        grammar L;
        e : e '+' e | '(' e ')' | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Nesting through rules that match empty input at every level.</summary>
    private const string EpsilonGrammar = """
        grammar Z;
        e : z '(' e ')' z | NAME ;
        z : ( 'q' )? ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Highly ambiguous grammar over a flat input.</summary>
    private const string AmbiguousGrammar = """
        grammar M;
        s : t* ;
        t : 'a' | 'a' 'a' | u ;
        u : 'a' t? ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Recursive lexer rule.</summary>
    private const string LexerRecursiveGrammar = """
        grammar X;
        s : NEST ;
        NEST : '(' NEST? ')' ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Grammar routing comments to the hidden channel.</summary>
    private const string HiddenChannelGrammar = """
        grammar H;
        s : NAME* ;
        NAME : ('a'..'z')+ ;
        COMMENT : '/*' .*? '*/' -> channel(HIDDEN) ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Prints the scenario catalog.</summary>
    /// <returns><see cref="ExitSuccess"/>.</returns>
    public static int List()
    {
        foreach (ParserScenario scenario in Catalog) Console.WriteLine($"{scenario.Name}\tP\t{scenario.Description}");
        return ExitSuccess;
    }

    /// <summary>Runs one parser scenario operation and prints the <c>PRESULT</c> line.</summary>
    /// <param name="scenarioName">The scenario name.</param>
    /// <param name="size">The size.</param>
    /// <param name="operation"><c>tokenize</c>, <c>parse</c> or <c>compile</c> (dialect scenarios only).</param>
    /// <param name="thread">The thread kind, echoed in the result.</param>
    /// <param name="limits">The parser limits selector, echoed in the result.</param>
    /// <param name="allocationProfile">Whether to sample allocations by type (<c>--alloc-profile</c>; inflates the allocation count).</param>
    /// <returns>The exit code.</returns>
    public static int Run(string scenarioName, int size, string operation, string thread, string limits, bool allocationProfile = false)
    {
        ParserScenario scenario = Catalog.FirstOrDefault(s => s.Name == scenarioName)
            ?? throw new ArgumentException($"Unknown parser scenario '{scenarioName}'.");

        // Warm-up on a tiny instance so JIT time is not measured; it runs the same code paths.
        ParserScenarioInstance warmUp = scenario.Create(Math.Min(size, 2));
        Execute(warmUp, operation, operation == "compile" ? null : CreateGrammar(warmUp, new Counters(), limits));

        ParserScenarioInstance instance = scenario.Create(size);
        var counters = new Counters();

        // The grammar is built (and its definition converted) before the measurement starts.
        CompiledGrammar? measuredGrammar = operation == "compile" ? null : CreateGrammar(instance, counters, limits);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long liveBefore = GC.GetTotalMemory(forceFullCollection: true);
        using var sampler = allocationProfile ? new AllocationSampler() : null;
        sampler?.Start();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        string outcome;
        int exitCode;
        ExecutionResult result = default;
        try
        {
            result = Execute(instance, operation, measuredGrammar);
            outcome = "ok" + (result.Detail.Length > 0 ? ":" + result.Detail : string.Empty);
            exitCode = ExitSuccess;
        }
        catch (InsufficientExecutionStackException)
        {
            outcome = "insufficient-stack";
            exitCode = ExitInsufficientStack;
        }
        catch (OutOfMemoryException)
        {
            outcome = "out-of-memory";
            exitCode = ExitOutOfMemory;
        }
        catch (Exception exception) when (ResourceLimitOutcome(exception) is { } resourceOutcome)
        {
            outcome = resourceOutcome;
            exitCode = ExitResourceLimit;
        }
        catch (Exception exception)
        {
            outcome = "exception:" + exception.GetType().Name;
            exitCode = ExitOtherException;
        }

        stopwatch.Stop();
        sampler?.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // Live memory retained by the engine (registry, memo, caches) and by the result tree, measured while both are reachable.
        long liveAfter = GC.GetTotalMemory(forceFullCollection: true);
        RegistrySizes registry = result.Grammar is null ? RegistrySizes.Unknown : ReadRegistry(result.Grammar);
        string budget = measuredGrammar is null ? "budget=-" : ReadBudget(measuredGrammar);
        GC.KeepAlive(result.Tree);
        long peakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;

        Console.WriteLine(string.Join(' ',
            $"PRESULT scenario={scenario.Name} size={size} op={operation} thread={thread} limits={limits}",
            $"os={(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux")} runtime={RuntimeInformation.FrameworkDescription.Replace(' ', '_')} arch={RuntimeInformation.ProcessArchitecture}",
            $"chars={instance.Text.Length} tokens={Format(result.Tokens)} defaultTokens={Format(result.DefaultTokens)}",
            $"ruleInvocations={counters.RuleInvocations} maxRuleDepth={counters.MaxRuleDepth} alternativeAttempts={counters.AlternativeAttempts}",
            $"treeNodes={Format(result.TreeNodes)} treeDepth={Format(result.TreeDepth)}",
            $"memoKeys={Format(registry.MemoKeys)} memoResults={Format(registry.MemoResults)} visitedStates={Format(registry.VisitedStates)} continuationKeys={Format(registry.ContinuationKeys)} continuations={Format(registry.Continuations)} lookaheadEntries={Format(registry.LookaheadEntries)}",
            budget,
            "ms=" + stopwatch.Elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
            "allocated=" + allocated.ToString(CultureInfo.InvariantCulture),
            "liveDelta=" + (liveAfter - liveBefore).ToString(CultureInfo.InvariantCulture),
            "peakWorkingSet=" + peakWorkingSet.ToString(CultureInfo.InvariantCulture),
            "outcome=" + outcome));
        if (sampler is not null) Console.WriteLine(sampler.Format(40));
        return exitCode;
    }

    /// <summary>
    /// Describes a controlled parser resource refusal, located by type name so the probe still builds against a baseline
    /// that has no such exception type.
    /// </summary>
    /// <param name="exception">The caught exception.</param>
    /// <returns>The outcome text, or <see langword="null"/> when the exception is not a parser resource refusal.</returns>
    private static string? ResourceLimitOutcome(Exception exception)
    {
        if (exception.GetType().Name != "ParserResourceLimitException") return null;
        object? kind = exception.GetType().GetProperty("Kind")?.GetValue(exception);
        object? phase = exception.GetType().GetProperty("Phase")?.GetValue(exception);
        object? observed = exception.GetType().GetProperty("Observed")?.GetValue(exception);
        return $"resource-limit:{phase}/{kind}@{observed}";
    }

    /// <summary>Executes one operation on a scenario instance.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="grammar">The instrumented grammar, or <see langword="null"/> for <c>compile</c>.</param>
    /// <returns>The measured result.</returns>
    private static ExecutionResult Execute(ParserScenarioInstance instance, string operation, CompiledGrammar? grammar)
    {
        if (operation == "compile")
        {
            if (instance.Dialect is null) throw new ArgumentException("Operation 'compile' needs a CSyntax or VBSyntax scenario.");
            var symbols = new Dictionary<string, System.Linq.Expressions.Expression>(StringComparer.Ordinal);
            foreach (string name in Identifiers(instance.Text)) symbols[name] = System.Linq.Expressions.Expression.Parameter(typeof(double), name);
            System.Linq.Expressions.Expression compiled = instance.Dialect == "CSyntax"
                ? new CSyntaxExpressionCompiler().CompileExpression(instance.Text, symbols)
                : new VBSyntaxExpressionCompiler().CompileExpression(instance.Text, symbols);
            return new ExecutionResult(compiled.NodeType.ToString(), null, null, null, null, null, compiled);
        }

        if (grammar is null) throw new ArgumentNullException(nameof(grammar));
        if (operation == "tokenize")
        {
            IReadOnlyList<Token> tokens = grammar.Tokenize(instance.Text);
            int defaultTokens = tokens.Count(token => token.Channel == "DEFAULT_CHANNEL");
            return new ExecutionResult(string.Empty, tokens.Count, defaultTokens, null, null, grammar, tokens);
        }

        if (operation == "parse")
        {
            ParseNode tree = grammar.Parse(instance.Text);
            (long nodes, long depth) = MeasureTree(tree);
            return new ExecutionResult(tree is ErrorNode ? "error-node" : string.Empty, null, null, nodes, depth, grammar, tree);
        }

        throw new ArgumentException($"Unknown parser operation '{operation}'.", nameof(operation));
    }

    /// <summary>Creates a fresh, instrumented compiled grammar for the instance (never the shared dialect instance).</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="counters">The counters.</param>
    /// <param name="limits">The parser limits selector (ignored by the baseline).</param>
    /// <returns>The grammar.</returns>
    private static CompiledGrammar CreateGrammar(ParserScenarioInstance instance, Counters counters, string limits)
    {
        var definition = instance.Dialect switch
        {
            "CSyntax" => SharedDefinition(typeof(CSyntaxTokenParser)),
            "VBSyntax" => SharedDefinition(typeof(VBSyntaxTokenParser)),
            _ => Antlr4GrammarConverter.Parse(instance.Grammar!),
        };

        ParserRuntimeFeaturePolicy policy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleInvocationFrameManager = new CountingFrameManager(counters),
            RuntimeObserver = new CountingObserver(counters),
        };
        return ParserLimitsAdapter.Create(definition, policy, limits);
    }

    /// <summary>Reads the parser definition of a dialect's shared lazily-initialized grammar.</summary>
    /// <param name="parserType">The public token parser type.</param>
    /// <returns>The definition.</returns>
    private static Utils.Parser.Model.ParserDefinition SharedDefinition(Type parserType)
    {
        FieldInfo field = parserType.GetField("SharedGrammar", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(parserType.Name, "SharedGrammar");
        var lazy = (Lazy<CompiledGrammar>)field.GetValue(null)!;
        return lazy.Value.Definition;
    }

    /// <summary>Counts the nodes and the depth of a parse tree iteratively.</summary>
    /// <param name="root">The root.</param>
    /// <returns>The node count and the depth (the root alone has depth 1).</returns>
    private static (long Nodes, long Depth) MeasureTree(ParseNode root)
    {
        long nodes = 0;
        long depth = 0;
        var stack = new Stack<(ParseNode Node, long Depth)>();
        stack.Push((root, 1));
        while (stack.Count > 0)
        {
            (ParseNode node, long level) = stack.Pop();
            nodes++;
            depth = Math.Max(depth, level);
            if (node is ParserNode parserNode)
            {
                foreach (ParseNode child in parserNode.Children) stack.Push((child, level + 1));
            }
        }

        return (nodes, depth);
    }

    /// <summary>Reads the sizes of the parse-scoped registry, memo and look-ahead cache of a grammar's parser engine.</summary>
    /// <param name="grammar">The grammar, after a parse.</param>
    /// <returns>The sizes.</returns>
    private static RegistrySizes ReadRegistry(CompiledGrammar grammar)
    {
        object? parser = Field(grammar, "_parser");
        object? registry = parser is null ? null : Field(parser, "_stateRegistry");
        object? cache = parser is null ? null : Field(parser, "_lookaheadCache");
        if (registry is null || cache is null) return RegistrySizes.Unknown;

        var completed = Field(registry, "_completedResults") as IDictionary;
        var continuations = Field(registry, "_continuations") as IDictionary;
        var visited = Field(registry, "_visitedStates") as IEnumerable;
        var lookahead = Field(cache, "_results") as ICollection;

        long memoResults = 0;
        if (completed is not null) foreach (object? list in completed.Values) memoResults += ((ICollection)list!).Count;
        long continuationCount = 0;
        if (continuations is not null) foreach (object? set in continuations.Values) continuationCount += ((IEnumerable)set!).Cast<object>().LongCount();
        long visitedCount = visited?.Cast<object>().LongCount() ?? -1;

        return new RegistrySizes(completed?.Count ?? -1, memoResults, visitedCount, continuations?.Count ?? -1, continuationCount, lookahead?.Count ?? -1);
    }

    /// <summary>
    /// Reads the internal resource counters of the last lexer and parser operations of a grammar (S-06 builds only; a
    /// baseline build has none and reports <c>-</c>).
    /// </summary>
    /// <param name="grammar">The grammar, after the operation.</param>
    /// <returns>The formatted counters.</returns>
    private static string ReadBudget(CompiledGrammar grammar)
    {
        object? lexer = Property(grammar, "Lexer");
        object? parser = Property(grammar, "Parser");
        object? lexerBudget = lexer is null ? null : Property(lexer, "LastResourceBudget");
        object? parserUsage = parser is null ? null : Property(parser, "LastResourceUsage");
        string Read(object? target, string name) => target is null ? "-" : Convert.ToString(Property(target, name), CultureInfo.InvariantCulture) ?? "-";
        return $"lexChars={Read(lexerBudget, "InputCharacters")} lexTokens={Read(lexerBudget, "Tokens")} lexSteps={Read(lexerBudget, "Steps")} lexDepth={Read(lexerBudget, "MaxDepthReached")} "
            + $"parseDepth={Read(parserUsage, "MaxDepth")} parseSteps={Read(parserUsage, "Steps")} parseNodes={Read(parserUsage, "Nodes")} retainedStates={Read(parserUsage, "RetainedStates")}";
    }

    /// <summary>Reads a public or internal instance property by name.</summary>
    /// <param name="target">The object.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The value, or <see langword="null"/> when the property does not exist.</returns>
    private static object? Property(object target, string name)
        => target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);

    /// <summary>Reads a private instance field by name.</summary>
    /// <param name="target">The object.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null"/> when the field does not exist.</returns>
    private static object? Field(object target, string name)
        => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);

    /// <summary>Formats an optional measurement.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value, or <c>-</c>.</returns>
    private static string Format(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";

    /// <summary>Repeats a fragment.</summary>
    /// <param name="fragment">The fragment.</param>
    /// <param name="count">The repetition count.</param>
    /// <returns>The text.</returns>
    private static string Repeat(string fragment, int count) => new StringBuilder(fragment.Length * Math.Max(0, count)).Insert(0, fragment, Math.Max(0, count)).ToString();

    /// <summary>Builds <c>x0 + x1 + ... + x(n-1)</c>.</summary>
    /// <param name="n">The term count.</param>
    /// <returns>The text.</returns>
    private static string Sum(int n) => string.Join(" + ", Enumerable.Range(0, Math.Max(1, n)).Select(i => "x" + i.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Lists the distinct identifiers of a dialect source (used as compile symbols).</summary>
    /// <param name="text">The source.</param>
    /// <returns>The identifiers.</returns>
    private static IEnumerable<string> Identifiers(string text)
    {
        var current = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (char c in text + " ")
        {
            if (char.IsLetterOrDigit(c) || c == '_') { current.Append(c); continue; }
            if (current.Length > 0 && char.IsLetter(current[0]) && seen.Add(current.ToString())) yield return current.ToString();
            current.Clear();
        }
    }

    /// <summary>Creates a dialect instance.</summary>
    /// <param name="dialect"><c>CSyntax</c> or <c>VBSyntax</c>.</param>
    /// <param name="text">The source.</param>
    /// <returns>The instance.</returns>
    private static ParserScenarioInstance Dialect(string dialect, string text) => new(text, dialect, null);

    /// <summary>Creates a custom-grammar instance.</summary>
    /// <param name="grammar">The ANTLR4 grammar text.</param>
    /// <param name="text">The source.</param>
    /// <returns>The instance.</returns>
    private static ParserScenarioInstance Grammar(string grammar, string text) => new(text, null, grammar);

    /// <summary>A named parser scenario.</summary>
    /// <param name="Name">The stable name.</param>
    /// <param name="Description">What the size controls.</param>
    /// <param name="Create">Creates the instance of a given size.</param>
    private sealed record ParserScenario(string Name, string Description, Func<int, ParserScenarioInstance> Create);

    /// <summary>One sized parser scenario instance.</summary>
    /// <param name="Text">The source text.</param>
    /// <param name="Dialect"><c>CSyntax</c>, <c>VBSyntax</c>, or <see langword="null"/> for a custom grammar.</param>
    /// <param name="Grammar">The custom grammar text, or <see langword="null"/>.</param>
    private sealed record ParserScenarioInstance(string Text, string? Dialect, string? Grammar);

    /// <summary>The measurements of one operation.</summary>
    /// <param name="Detail">A short outcome detail.</param>
    /// <param name="Tokens">All emitted tokens (tokenize).</param>
    /// <param name="DefaultTokens">Default-channel tokens (tokenize).</param>
    /// <param name="TreeNodes">Parse-tree node count (parse).</param>
    /// <param name="TreeDepth">Parse-tree depth (parse).</param>
    /// <param name="Grammar">The grammar used, kept reachable for the live-memory measurement.</param>
    /// <param name="Tree">The result object, kept reachable for the live-memory measurement.</param>
    private readonly record struct ExecutionResult(string Detail, long? Tokens, long? DefaultTokens, long? TreeNodes, long? TreeDepth, CompiledGrammar? Grammar, object? Tree)
    {
        /// <summary>Gets the detail, never <see langword="null"/>.</summary>
        public string Detail { get; init; } = Detail ?? string.Empty;
    }

    /// <summary>Registry, memo and cache sizes after a parse.</summary>
    /// <param name="MemoKeys">Distinct completed-result invocation keys.</param>
    /// <param name="MemoResults">Completed results.</param>
    /// <param name="VisitedStates">Visited-state keys.</param>
    /// <param name="ContinuationKeys">Distinct continuation invocation keys.</param>
    /// <param name="Continuations">Continuation entries.</param>
    /// <param name="LookaheadEntries">Look-ahead cache entries.</param>
    private sealed record RegistrySizes(long? MemoKeys, long? MemoResults, long? VisitedStates, long? ContinuationKeys, long? Continuations, long? LookaheadEntries)
    {
        /// <summary>Unknown sizes (no parse, or fields not found).</summary>
        public static RegistrySizes Unknown { get; } = new(null, null, null, null, null, null);
    }

    /// <summary>Counters fed by the instrumented runtime policies.</summary>
    private sealed class Counters
    {
        /// <summary>Rule invocations that entered a frame (memo misses).</summary>
        public long RuleInvocations;

        /// <summary>Current rule-frame depth.</summary>
        public int CurrentRuleDepth;

        /// <summary>Maximum rule-frame depth.</summary>
        public int MaxRuleDepth;

        /// <summary>Scheduled alternative attempts.</summary>
        public long AlternativeAttempts;
    }

    /// <summary>Counts rule frames and their nesting through the public invocation-frame manager contract.</summary>
    /// <param name="counters">The counters.</param>
    private sealed class CountingFrameManager(Counters counters) : IParserRuleInvocationFrameManager
    {
        /// <inheritdoc />
        public ParserRuleInvocationFrame? Current => null;

        /// <inheritdoc />
        public ParserRuleInvocationFrame Enter(string ruleName, int inputPosition, ParserRuleInvocationDescriptor? descriptor = null)
        {
            counters.RuleInvocations++;
            counters.CurrentRuleDepth++;
            counters.MaxRuleDepth = Math.Max(counters.MaxRuleDepth, counters.CurrentRuleDepth);
            return NullParserRuleInvocationFrameManager.Instance.Enter(ruleName, inputPosition, descriptor);
        }

        /// <inheritdoc />
        public void Exit(ParserRuleInvocationFrame frame, bool succeeded) => counters.CurrentRuleDepth--;
    }

    /// <summary>Counts scheduled alternative attempts through the passive runtime observer contract.</summary>
    /// <param name="counters">The counters.</param>
    private sealed class CountingObserver(Counters counters) : IParserRuntimeObserver
    {
        /// <inheritdoc />
        public void OnAlternativeStarted(AlternativeRuntimeObservation observation) => counters.AlternativeAttempts++;

        /// <inheritdoc />
        public void OnAlternativeCompleted(AlternativeRuntimeObservation observation)
        {
        }

        /// <inheritdoc />
        public void OnAlternativeFailed(AlternativeRuntimeObservation observation)
        {
        }

        /// <inheritdoc />
        public void OnAlternativePruned(AlternativeRuntimeObservation observation)
        {
        }

        /// <inheritdoc />
        public void OnAlternativeSelected(AlternativeRuntimeObservation observation)
        {
        }
    }
}
