using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Expressions.VBSyntax.Runtime;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Issue #709: concurrent <see cref="CompiledGrammar.Parse"/> and <see cref="CompiledGrammar.Tokenize"/> calls on one
/// shared instance (and on the façades that share one: generated static grammars, CSyntax/VBSyntax token parsers) must
/// return exactly what a sequential call on independent engines returns. Every expected value comes from a fresh
/// <see cref="CompiledGrammar"/> used sequentially before the concurrent phase; workers re-synchronize on a barrier so
/// the operations really overlap. Before the fix these tests returned silently corrupted trees and token lists.
/// </summary>
[TestClass]
public class CompiledGrammarConcurrencyTests
{
    /// <summary>A grammar with lexer modes, <c>more</c>, a hidden channel, keywords and left recursion.</summary>
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

    /// <summary>Valid, invalid and lexically varied inputs of <see cref="ModeGrammar"/>.</summary>
    private static readonly string[] ModeInputs =
    [
        "x = 1;",
        "print \"hello\";",
        "print \"es\\\"c\" + x; # trailing comment",
        "if a < b then x = a * (b + 1); else print -c;",
        "a = b + c * d - e / f == g;",
        string.Join(" ", Enumerable.Range(0, 30).Select(i => "v = v + " + i + ";")),
        "x = " + new string('(', 15) + "1" + new string(')', 15) + ";",
        "x = ;",
        "print \"unterminated",
        "if then",
        "x = 1 $ 2;",
    ];

    /// <summary>C-like inputs: valid, nested, unary, comparisons, strings and syntax errors.</summary>
    private static readonly string[] CInputs =
    [
        "a + b * c",
        "f(x, y[2]).z - (1 + 2) * 3",
        string.Join(" + ", Enumerable.Range(0, 40).Select(i => "x" + i)),
        "((((x0))))",
        "-(-(-(!x)))",
        "a < b == c >= d && e != f || !g",
        "\"esc\\\"aped\" + 'c' + @\"verbatim\"",
        "a + * b",
        "(((a)",
        "§",
        "",
    ];

    /// <summary>VB-like inputs: valid, nested, unary, comparisons, strings and syntax errors.</summary>
    private static readonly string[] VbInputs =
    [
        "a + b * c",
        "f(x, y(2)).z - (1 + 2) * 3",
        string.Join(" + ", Enumerable.Range(0, 40).Select(i => "x" + i)),
        "((((x0))))",
        "Not Not -x",
        "a < b AndAlso c <> d OrElse Not e",
        "\"text\" & \"other\"",
        "a + * b",
        "(((a)",
        "",
    ];

    /// <summary>Concurrent <c>Parse</c> calls on one shared grammar return the sequential oracle's trees.</summary>
    /// <param name="threads">The number of overlapping workers.</param>
    [TestMethod]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(16)]
    public void SharedGrammar_ConcurrentParse_MatchesSequentialOracle(int threads)
    {
        var definition = Antlr4GrammarConverter.Parse(ModeGrammar);
        var shared = new CompiledGrammar(definition);
        string[] expected = ModeInputs.Select(input => RenderTree(new CompiledGrammar(definition).Parse(input))).ToArray();

        AssertMatchesOracle(threads, ModeInputs.Length, index => RenderTree(shared.Parse(ModeInputs[index])), index => expected[index]);
        CollectionAssert.AreEqual(expected, ModeInputs.Select(input => RenderTree(shared.Parse(input))).ToArray(), "no state survives the concurrent phase");
    }

    /// <summary>Concurrent <c>Tokenize</c> calls on one shared grammar return the sequential oracle's token lists.</summary>
    /// <param name="threads">The number of overlapping workers.</param>
    [TestMethod]
    [DataRow(2)]
    [DataRow(8)]
    [DataRow(16)]
    public void SharedGrammar_ConcurrentTokenize_MatchesSequentialOracle(int threads)
    {
        var definition = Antlr4GrammarConverter.Parse(ModeGrammar);
        var shared = new CompiledGrammar(definition);
        string[] expected = ModeInputs.Select(input => RenderTokens(new CompiledGrammar(definition).Tokenize(input))).ToArray();

        AssertMatchesOracle(threads, ModeInputs.Length, index => RenderTokens(shared.Tokenize(ModeInputs[index])), index => expected[index]);
    }

    /// <summary>Interleaved concurrent <c>Parse</c> and <c>Tokenize</c> calls on one shared grammar match the oracle.</summary>
    /// <param name="threads">The number of overlapping workers.</param>
    [TestMethod]
    [DataRow(4)]
    [DataRow(16)]
    public void SharedGrammar_MixedParseAndTokenize_MatchesSequentialOracle(int threads)
    {
        var definition = Antlr4GrammarConverter.Parse(ModeGrammar);
        var shared = new CompiledGrammar(definition);
        var oracle = new CompiledGrammar(definition);
        string[] expected =
        [
            .. ModeInputs.Select(input => RenderTree(oracle.Parse(input))),
            .. ModeInputs.Select(input => RenderTokens(oracle.Tokenize(input))),
        ];

        AssertMatchesOracle(threads, expected.Length,
            index => index < ModeInputs.Length
                ? RenderTree(shared.Parse(ModeInputs[index]))
                : RenderTokens(shared.Tokenize(ModeInputs[index - ModeInputs.Length])),
            index => expected[index]);
    }

    /// <summary>
    /// Distinct <see cref="CSyntaxTokenParser"/> instances share one static grammar; concurrent parses and tokenizations
    /// through them match independent sequential engines.
    /// </summary>
    [TestMethod]
    public void CSyntaxTokenParser_ConcurrentParseAndTokenize_MatchesSequentialOracle()
    {
        var definition = BuildGeneratedDefinition(typeof(CSyntaxTokenParser), "Utils.Expressions.CSyntax.Runtime.CSyntaxTokenizerGrammar");
        string[] expected =
        [
            .. CInputs.Select(input => RenderTree(new CompiledGrammar(definition).Parse(input))),
            .. CInputs.Select(input => string.Join("|", new CompiledGrammar(definition).Tokenize(input).Select(t => t.RuleName + ":" + t.Text + "@" + t.Span.Position + "+" + t.Span.Length))),
        ];

        AssertMatchesOracle(8, expected.Length,
            index => index < CInputs.Length
                ? RenderTree(new CSyntaxTokenParser().Parse(CInputs[index]))
                : string.Join("|", new CSyntaxTokenParser().Tokenize(CInputs[index - CInputs.Length]).Select(t => t.RuleName + ":" + t.Text + "@" + t.Position + "+" + t.Length)),
            index => expected[index]);
    }

    /// <summary>
    /// Distinct <see cref="VBSyntaxTokenParser"/> instances share one static grammar; concurrent parses and tokenizations
    /// through them match independent sequential engines.
    /// </summary>
    [TestMethod]
    public void VBSyntaxTokenParser_ConcurrentParseAndTokenize_MatchesSequentialOracle()
    {
        var definition = BuildGeneratedDefinition(typeof(VBSyntaxTokenParser), "Utils.Expressions.VBSyntax.Runtime.VBSyntaxTokenizerGrammar");
        string[] expected =
        [
            .. VbInputs.Select(input => RenderTree(new CompiledGrammar(definition).Parse(input))),
            .. VbInputs.Select(input => string.Join("|", new CompiledGrammar(definition).Tokenize(input).Select(t => t.RuleName + ":" + t.Text + "@" + t.Span.Position + "+" + t.Span.Length))),
        ];

        AssertMatchesOracle(8, expected.Length,
            index => index < VbInputs.Length
                ? RenderTree(new VBSyntaxTokenParser().Parse(VbInputs[index]))
                : string.Join("|", new VBSyntaxTokenParser().Tokenize(VbInputs[index - VbInputs.Length]).Select(t => t.RuleName + ":" + t.Text + "@" + t.Position + "+" + t.Length)),
            index => expected[index]);
    }

    /// <summary>
    /// The generated static façade (<c>ExpGrammar.Grammar</c>, <c>ExpGrammar.Parse</c>, <c>ExpGrammar.Tokenize</c>) and
    /// the fresh-context <c>ExpGrammar.ParseWithEmbeddedCode(string)</c> path can be used concurrently.
    /// </summary>
    [TestMethod]
    public void GeneratedStaticFacade_ConcurrentCalls_MatchSequentialOracle()
    {
        string[] inputs = ["1+2*3", "(1+2)*(3-4)/5", string.Join("+", Enumerable.Range(0, 50)), "((((((7))))))", "1+", "2*(3", "4 4"];
        var definition = ExpGrammar.Build();
        string[] trees = inputs.Select(input => RenderTree(new CompiledGrammar(definition).Parse(input))).ToArray();
        string[] tokens = inputs.Select(input => RenderTokens(new CompiledGrammar(definition).Tokenize(input))).ToArray();

        AssertMatchesOracle(8, inputs.Length * 4,
            index => (index / inputs.Length) switch
            {
                0 => RenderTree(ExpGrammar.Grammar.Parse(inputs[index % inputs.Length])),
                1 => RenderTree(ExpGrammar.Parse(inputs[index % inputs.Length])),
                2 => RenderTokens(ExpGrammar.Tokenize(inputs[index % inputs.Length])),
                _ => RenderTree(ExpGrammar.ParseWithEmbeddedCode(inputs[index % inputs.Length])),
            },
            index => index / inputs.Length == 2 ? tokens[index % inputs.Length] : trees[index % inputs.Length]);
    }

    /// <summary>
    /// A call made on a grammar while another call of the same grammar is still running on the same thread (here from a
    /// lexer action, in a non-default lexer mode) runs on its own engines: it neither resets the outer tokenization's
    /// mode stack nor sees its state.
    /// </summary>
    [TestMethod]
    public void NestedCallOnSameGrammar_DoesNotDisturbTheOuterCall()
    {
        const string grammar = """
            grammar Nested;
            s : (NAME | str)+ ;
            str : OPEN WORD* CLOSE ;
            NAME : ('a'..'z')+ ;
            WS : ' '+ -> skip ;
            OPEN : '"' -> pushMode(TEXT) ;
            mode TEXT;
            CLOSE : '"' -> popMode ;
            WORD : ('a'..'z')+ { nest } ;
            SPACE : ' '+ -> skip ;
            """;
        const string outerInput = "ab \"cd ef\" gh \"ij\" kl";
        const string nestedInput = "xy \"zz\" w";
        var definition = Antlr4GrammarConverter.Parse(grammar);
        var plain = new CompiledGrammar(definition);
        string expectedOuterTree = RenderTree(plain.Parse(outerInput));
        string expectedOuterTokens = RenderTokens(plain.Tokenize(outerInput));
        string expectedNested = RenderTree(plain.Parse(nestedInput));

        var nesting = new NestingLexerActionExecutor(nestedInput);
        var reentrant = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default with { LexerActionExecutor = nesting });
        nesting.Grammar = reentrant;

        Assert.AreEqual(expectedOuterTree, RenderTree(reentrant.Parse(outerInput)));
        Assert.AreEqual(expectedOuterTokens, RenderTokens(reentrant.Tokenize(outerInput)));
        Assert.IsTrue(nesting.NestedResults.Count >= 6, "every WORD action of both outer calls ran a nested parse");
        Assert.IsTrue(nesting.NestedResults.All(result => result == expectedNested), "nested parses are unaffected by the outer call");
    }

    /// <summary>
    /// The components of <see cref="ParserRuntimeFeaturePolicy.Default"/> keep no instance state, which is the
    /// assumption behind the concurrent-use guarantee of <see cref="CompiledGrammar"/> under the default policy.
    /// </summary>
    [TestMethod]
    public void DefaultPolicyComponents_AreStateless()
    {
        var policy = ParserRuntimeFeaturePolicy.Default;
        object?[] components =
        [
            policy.SemanticPredicateEvaluator,
            policy.ParserActionExecutor,
            policy.LexerActionExecutor,
            policy.LexerPredicateEvaluator,
            policy.ExecutionStateManager,
            policy.RuleLifecycleExecutor,
            policy.RuleInvocationFrameManager,
            policy.RuleCallExecutionPolicy,
            policy.EmbeddedCodeTransformer,
        ];

        Assert.IsNull(policy.RuntimeObserver, "the default policy has no observer");
        foreach (object? component in components)
        {
            Assert.IsNotNull(component);
            FieldInfo[] fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.AreEqual(0, fields.Length, $"{component.GetType().Name} keeps instance state: {string.Join(", ", fields.Select(f => f.Name))}");
        }
    }

    /// <summary>
    /// A caller-owned custom policy whose components are thread-safe may be shared by concurrent calls: results match
    /// the oracle and the thread-safe component observes every evaluation exactly once per call.
    /// </summary>
    [TestMethod]
    public void ThreadSafeCustomPolicy_SharedByConcurrentCalls_MatchesSequentialOracle()
    {
        const string grammar = """
            grammar P;
            s : item+ ;
            item : { accept }? NUM | NAME ;
            NUM : ('0'..'9')+ ;
            NAME : ('a'..'z')+ ;
            WS : ' '+ -> skip ;
            """;
        string[] inputs = ["1 a 3", "a b c", "7", "x 9 y 11 z", "2 4"];
        var definition = Antlr4GrammarConverter.Parse(grammar);

        // The oracle uses its own evaluator instance, sequentially, and counts the evaluations of every input.
        var oracleEvaluator = new CountingPredicateEvaluator();
        var oracle = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default with { SemanticPredicateEvaluator = oracleEvaluator });
        var expected = new string[inputs.Length];
        var evaluationsPerInput = new long[inputs.Length];
        for (int i = 0; i < inputs.Length; i++)
        {
            long before = oracleEvaluator.Evaluations;
            expected[i] = RenderTree(oracle.Parse(inputs[i]));
            evaluationsPerInput[i] = oracleEvaluator.Evaluations - before;
        }

        var sharedEvaluator = new CountingPredicateEvaluator();
        var shared = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default with { SemanticPredicateEvaluator = sharedEvaluator });
        long expectedEvaluations = AssertMatchesOracle(8, inputs.Length, index => RenderTree(shared.Parse(inputs[index])), index => expected[index], index => evaluationsPerInput[index]);
        Assert.AreEqual(expectedEvaluations, sharedEvaluator.Evaluations, "each call evaluated its predicates exactly as a sequential call does");
    }

    /// <summary>
    /// Every call runs on its own engines while the grammar-invariant preparations are built once and shared: two calls
    /// expose distinct engines whose tables are the same instances.
    /// </summary>
    [TestMethod]
    public void Calls_UsePrivateEnginesOverSharedPreparations()
    {
        var grammar = new CompiledGrammar(Antlr4GrammarConverter.Parse(ModeGrammar));
        _ = grammar.Parse("x = 1;");
        LexerEngine firstLexer = grammar.Lexer;
        ParserEngine firstParser = grammar.Parser;
        _ = grammar.Parse("x = 2;");

        Assert.AreNotSame(firstLexer, grammar.Lexer);
        Assert.AreNotSame(firstParser, grammar.Parser);
        Assert.AreSame(ReadField(firstLexer, "_keywordTrieByMode"), ReadField(grammar.Lexer, "_keywordTrieByMode"));
        Assert.AreSame(ReadField(firstLexer, "_candidateRulesByMode"), ReadField(grammar.Lexer, "_candidateRulesByMode"));
        Assert.AreSame(ReadField(firstParser, "_schedulingShapes"), ReadField(grammar.Parser, "_schedulingShapes"));
        Assert.AreNotSame(ReadField(firstParser, "_stateRegistry"), ReadField(grammar.Parser, "_stateRegistry"));
    }

    /// <summary>
    /// Runs <paramref name="operationCount"/> distinct operations on every worker, in a per-worker deterministic order,
    /// with a barrier every few operations so that the workers overlap, and asserts that every result equals its oracle.
    /// </summary>
    /// <param name="threads">The number of workers.</param>
    /// <param name="operationCount">The number of distinct operations.</param>
    /// <param name="run">Runs operation <c>i</c> and renders its result.</param>
    /// <param name="expected">The oracle rendering of operation <c>i</c>.</param>
    /// <param name="weight">An optional per-operation weight summed over every executed operation.</param>
    /// <returns>The sum of <paramref name="weight"/> over every executed operation (0 without weight).</returns>
    private static long AssertMatchesOracle(int threads, int operationCount, Func<int, string> run, Func<int, string> expected, Func<int, long>? weight = null)
    {
        const int Rounds = 12;
        const int PhaseLength = 8;
        var failures = new List<string>();
        long weightSum = 0;
        using var barrier = new Barrier(threads);
        var workers = new Thread[threads];
        for (int w = 0; w < threads; w++)
        {
            int worker = w;
            workers[w] = new Thread(() =>
            {
                var random = new Random(709 * 31 + worker);
                int total = Rounds * operationCount;
                for (int i = 0; i < total; i++)
                {
                    if (i % PhaseLength == 0) barrier.SignalAndWait();
                    int index = random.Next(operationCount);
                    string actual;
                    try
                    {
                        actual = run(index);
                    }
                    catch (Exception exception)
                    {
                        actual = "!" + exception.GetType().Name + ": " + exception.Message;
                    }

                    if (weight is not null) Interlocked.Add(ref weightSum, weight(index));
                    if (actual != expected(index))
                    {
                        lock (failures)
                        {
                            if (failures.Count < 5) failures.Add($"worker {worker}, operation {index}: {Shorten(actual)}");
                            else if (failures.Count == 5) failures.Add("...");
                        }
                    }
                }

                barrier.RemoveParticipant();
            }, 4 * 1024 * 1024);
            workers[w].Start();
        }

        foreach (Thread thread in workers) thread.Join();
        Assert.AreEqual(0, failures.Count, "concurrent results differ from the sequential oracle:\n" + string.Join("\n", failures));
        return weightSum;
    }

    /// <summary>Builds the definition of a generated grammar class that is internal to a front-end assembly.</summary>
    /// <param name="anchor">A public type of the front-end assembly.</param>
    /// <param name="typeName">The generated grammar's full type name.</param>
    /// <returns>A fresh resolved definition, independent of the shared static grammar.</returns>
    private static ParserDefinition BuildGeneratedDefinition(Type anchor, string typeName)
        => (ParserDefinition)anchor.Assembly.GetType(typeName, true)!.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;

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
            var (current, depth) = pending.Pop();
            builder.Append(depth).Append(':').Append(current.GetType().Name).Append(':').Append(current.Rule?.Name)
                .Append(':').Append(current.ModeName).Append('@').Append(current.Span.Position).Append('+').Append(current.Span.Length)
                .Append('L').Append(current.Span.Line).Append('C').Append(current.Span.Column);
            if (current is LexerNode lexer) builder.Append('[').Append(lexer.Token.RuleName).Append('|').Append(lexer.Token.Channel).Append('|').Append(lexer.Token.Text).Append(']');
            if (current is ErrorNode error) builder.Append('{').Append(error.Message).Append('}');
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
        => string.Join("|", tokens.Select(t => $"{t.RuleName}:{t.ModeName}:{t.Channel}:{t.Text}@{t.Span.Position}+{t.Span.Length}L{t.Span.Line}C{t.Span.Column}"));

    /// <summary>Shortens a rendering for an assertion message.</summary>
    /// <param name="text">The rendering.</param>
    /// <returns>At most 200 characters.</returns>
    private static string Shorten(string text) => text.Length <= 200 ? text : text[..200] + "...";

    /// <summary>Reads a private instance field.</summary>
    /// <param name="target">The object.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value.</returns>
    private static object ReadField(object target, string name)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    /// <summary>A lexer action executor that parses another input on the same grammar from inside every action.</summary>
    /// <param name="nestedInput">The input of the nested parse.</param>
    private sealed class NestingLexerActionExecutor(string nestedInput) : ILexerActionExecutor
    {
        /// <summary>The nesting depth of the current thread, so that nested calls do not nest again.</summary>
        [ThreadStatic]
        private static int _depth;

        /// <summary>Gets or sets the grammar the nested calls run on.</summary>
        public CompiledGrammar? Grammar { get; set; }

        /// <summary>Gets the rendering of every nested parse.</summary>
        public List<string> NestedResults { get; } = [];

        /// <inheritdoc />
        public LexerActionExecutionOutcome Execute(LexerActionExecutionContext context, LexerActionExecutionResult result)
        {
            if (_depth == 0)
            {
                _depth++;
                try
                {
                    NestedResults.Add(RenderTree(Grammar!.Parse(nestedInput)));
                }
                finally
                {
                    _depth--;
                }
            }

            return LexerActionExecutionOutcome.NotExecuted;
        }
    }

    /// <summary>A thread-safe predicate evaluator that accepts every predicate and counts its evaluations.</summary>
    private sealed class CountingPredicateEvaluator : ISemanticPredicateEvaluator
    {
        /// <summary>The number of evaluations (updated atomically).</summary>
        private long _evaluations;

        /// <summary>Gets the number of evaluations so far.</summary>
        public long Evaluations => Interlocked.Read(ref _evaluations);

        /// <inheritdoc />
        public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
        {
            Interlocked.Increment(ref _evaluations);
            return SemanticPredicateEvaluationOutcome.Satisfied;
        }
    }
}
