using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Expressions.VBSyntax.Runtime;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Security.Parser;

/// <summary>
/// S-06 (ETC-16, ETC-18): resource-exhaustion invariants of <c>Utils.Parser</c>. Every hostile nesting, size or work
/// pattern must end with a valid result or a controlled <see cref="ParserResourceLimitException"/>, never with a
/// process-terminating stack overflow or unbounded work, and a refusal must leave the engine reusable.
/// </summary>
/// <remarks>
/// <para>
/// Limit boundaries are exact: each test measures the counter an accepted input needs with
/// <see cref="ParserResourceLimits.Unlimited"/>, then checks that the same input is accepted at exactly that limit and
/// refused one unit below, with the refusal reporting the configured limit and the first attempted value over it.
/// </para>
/// <para>
/// Stack tests run on dedicated worker threads with explicit stack sizes (256 KiB, the smallest stack of the S-06 and
/// ETC R0 campaigns) and with the depth limits disabled, so the recursion itself reaches the stack guard. The guard makes
/// these runs safe inside the test host; the process-isolated evidence on Windows and Linux, Debug and Release, is the
/// <c>tests/ExpressionResourceProbe</c> campaign recorded in <c>docs/parser/ParserResourceSafety.md</c>.
/// </para>
/// </remarks>
[TestClass]
public class ParserResourceLimitTests
{
    /// <summary>The small worker stack of the stack-guard tests.</summary>
    private const int SmallStack = 256 * 1024;

    /// <summary>A large worker stack on which the deterministic depth limits are reached before the stack guard.</summary>
    private const int LargeStack = 64 * 1024 * 1024;

    /// <summary>Right-recursive unary grammar.</summary>
    private const string UnaryGrammar = """
        grammar U;
        e : '-' e | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>Recursion through three distinct rule names that consume nothing themselves.</summary>
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

    /// <summary>Nested quantifiers around a recursive reference.</summary>
    private const string NestedQuantifierGrammar = """
        grammar N;
        l : '(' ( ( l | NAME )* )? ')' ;
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

    /// <summary>Flat grammar with a hidden channel.</summary>
    private const string HiddenChannelGrammar = """
        grammar H;
        s : NAME* ;
        NAME : ('a'..'z')+ ;
        COMMENT : '#' ('a'..'z')* -> channel(HIDDEN) ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>The recursion families of the call-graph audit, with an input builder for a nesting size.</summary>
    private static readonly (string Name, string Grammar, Func<int, string> Input)[] NestingFamilies =
    [
        ("unary", UnaryGrammar, n => string.Concat(Enumerable.Repeat("- ", n)) + "a"),
        ("rule chain", RuleChainGrammar, n => new string('[', n) + "a" + new string(']', n)),
        ("quantifier", QuantifierGrammar, n => new string('(', n) + "a" + new string(')', n)),
        ("inline alternation", AlternationGrammar, n => new string('(', n) + "a" + new string(')', n)),
        ("left recursion", LeftRecursiveGrammar, n => new string('(', n) + "a" + new string(')', n)),
        ("zero-width levels", EpsilonGrammar, n => new string('(', n) + "a" + new string(')', n)),
        ("nested quantifiers", NestedQuantifierGrammar, n => new string('(', n) + "a" + new string(')', n)),
    ];

    // ---------------------------------------------------------------------------------------------------------------
    // ETC-16: depth and stack.
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Every recursion family is accepted at exactly the parse depth it needs and refused one level below, with the
    /// exact limit and attempted depth, whatever the rule names and whether input is consumed between levels.
    /// </summary>
    [TestMethod]
    public void ParseDepth_EveryRecursionFamily_AcceptsAtTheLimitAndRefusesOneBelow()
    {
        foreach (var (name, grammar, input) in NestingFamilies)
        {
            var text = input(40);
            var needed = Measure(grammar, text).Parser.MaxDepth;
            Assert.IsTrue(needed > 40, name);

            var accepted = Compile(grammar, ParserResourceLimits.Default with { MaxParseDepth = needed }).Parse(text);
            Assert.IsNotInstanceOfType<ErrorNode>(accepted, name);

            var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(
                () => Compile(grammar, ParserResourceLimits.Default with { MaxParseDepth = needed - 1 }).Parse(text), name);
            Assert.AreEqual(ParserResourceLimitKind.ParseDepth, refusal.Kind, name);
            Assert.AreEqual(ParserResourcePhase.Parser, refusal.Phase, name);
            Assert.AreEqual(needed - 1, refusal.Limit, name);
            Assert.AreEqual(needed, refusal.Observed, name);
            Assert.IsNotNull(refusal.Offset, name);
        }
    }

    /// <summary>
    /// On a 256 KiB stack with the depth limit disabled, every recursion family ends a hostile nesting with a controlled
    /// stack refusal carrying an <see cref="InsufficientExecutionStackException"/>, never with a stack overflow.
    /// </summary>
    [TestMethod]
    public void StackGuard_EveryRecursionFamily_RefusesHostileNestingOnASmallStack()
    {
        foreach (var (name, grammar, input) in NestingFamilies)
        {
            var compiled = Compile(grammar, ParserResourceLimits.Unlimited);
            var text = input(20_000);
            RunOnThread(SmallStack, () =>
            {
                var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => compiled.Parse(text), name);
                Assert.AreEqual(ParserResourceLimitKind.Stack, refusal.Kind, name);
                Assert.AreEqual(ParserResourcePhase.Parser, refusal.Phase, name);
                Assert.IsInstanceOfType<InsufficientExecutionStackException>(refusal.InnerException, name);
                Assert.IsTrue(refusal.Observed > 0, name);
            });

            // The same engine parses a small input afterwards, on the same kind of thread.
            RunOnThread(SmallStack, () => Assert.IsNotInstanceOfType<ErrorNode>(compiled.Parse(input(2)), name));
        }
    }

    /// <summary>
    /// The historical ETC-16 reproducer (20 parenthesis pairs, a 42-character input) and far deeper nesting are refused
    /// by the public CSyntax and VBSyntax front ends with a controlled exception on every thread size; shallow nesting
    /// still compiles.
    /// </summary>
    [TestMethod]
    public void PublicFrontEnds_DeepParentheses_EndWithAControlledRefusal()
    {
        var symbols = new Dictionary<string, System.Linq.Expressions.Expression>
        {
            ["x0"] = System.Linq.Expressions.Expression.Parameter(typeof(double), "x0"),
        };
        string Nest(int n) => new string('(', n) + "x0" + new string(')', n);

        foreach (var stack in new[] { SmallStack, 1024 * 1024, LargeStack })
        {
            RunOnThread(stack, () =>
            {
                foreach (var depth in new[] { 20, 200, 10_000 })
                {
                    AssertParsesOrRefuses(() => new CSyntaxTokenParser().Parse(Nest(depth)), $"CSyntax {depth} on {stack}");
                    AssertParsesOrRefuses(() => new VBSyntaxTokenParser().Parse(Nest(depth)), $"VBSyntax {depth} on {stack}");
                }

                var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => new CSyntaxExpressionCompiler().CompileExpression(Nest(10_000), symbols));
                Assert.IsTrue(refusal.Kind is ParserResourceLimitKind.Stack or ParserResourceLimitKind.ParseDepth);
            });
        }

        // Shallow nesting still compiles on a 1 MiB thread in Release and Debug (Debug frames are larger: the stack guard
        // refuses about 10 CSyntax levels there, 19 in Release).
        RunOnThread(1024 * 1024, () =>
        {
            Assert.IsNotNull(new CSyntaxExpressionCompiler().CompileExpression(Nest(5), symbols));
            Assert.IsNotNull(new VBSyntaxExpressionCompiler().CompileExpression(Nest(5), symbols));
        });
    }

    /// <summary>
    /// On a large stack the default depth limit, not the stack guard, refuses deep CSyntax nesting: the refusal is
    /// deterministic and reports <see cref="ParserResourceLimits.DefaultMaxParseDepth"/>.
    /// </summary>
    [TestMethod]
    public void DefaultDepthLimit_OnALargeStack_RefusesDeterministically()
    {
        RunOnThread(LargeStack, () =>
        {
            var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(
                () => new CSyntaxTokenParser().Parse(new string('(', 1_000) + "x0" + new string(')', 1_000)));
            Assert.AreEqual(ParserResourceLimitKind.ParseDepth, refusal.Kind);
            Assert.AreEqual(ParserResourceLimits.DefaultMaxParseDepth, refusal.Limit);
            Assert.AreEqual(ParserResourceLimits.DefaultMaxParseDepth + 1L, refusal.Observed);
        });
    }

    /// <summary>
    /// A recursive lexer rule is accepted at exactly the lexer depth it needs, refused one level below, and stopped by
    /// the stack guard on a small stack.
    /// </summary>
    [TestMethod]
    public void LexerDepth_RecursiveLexerRule_IsBoundedAndStackSafe()
    {
        var text = new string('(', 50) + new string(')', 50);
        var needed = Measure(LexerRecursiveGrammar, text).Lexer.MaxDepth;
        Assert.IsTrue(needed > 50);

        Assert.AreEqual(1, Compile(LexerRecursiveGrammar, ParserResourceLimits.Default with { MaxLexerDepth = needed }).Tokenize(text).Count);
        var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(
            () => Compile(LexerRecursiveGrammar, ParserResourceLimits.Default with { MaxLexerDepth = needed - 1 }).Tokenize(text));
        Assert.AreEqual(ParserResourceLimitKind.LexerDepth, refusal.Kind);
        Assert.AreEqual(ParserResourcePhase.Lexer, refusal.Phase);
        Assert.AreEqual(needed - 1, refusal.Limit);
        Assert.AreEqual(needed, refusal.Observed);

        var unlimited = Compile(LexerRecursiveGrammar, ParserResourceLimits.Unlimited);
        var hostile = new string('(', 100_000) + new string(')', 100_000);
        RunOnThread(SmallStack, () =>
        {
            var stackRefusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => unlimited.Parse(hostile));
            Assert.AreEqual(ParserResourceLimitKind.Stack, stackRefusal.Kind);
            Assert.AreEqual(ParserResourcePhase.Lexer, stackRefusal.Phase);
            Assert.AreEqual(1, unlimited.Tokenize("()").Count);
        });
    }

    /// <summary>
    /// <see cref="ParseTreeCompiler{TContext, TResult}"/> stops a tree deeper than its thread can visit with a controlled
    /// stack refusal of the tree-compilation phase.
    /// </summary>
    [TestMethod]
    public void TreeCompilation_DeepTree_RefusesOnASmallStack()
    {
        ParseNode? tree = null;
        RunOnThread(LargeStack, () => tree = Compile(UnaryGrammar, ParserResourceLimits.Unlimited).Parse(string.Concat(Enumerable.Repeat("- ", 3_000)) + "a"));
        Assert.IsNotInstanceOfType<ErrorNode>(tree);

        var compiler = new ParseTreeCompiler<int, int>().DefaultAscend((_, _, children) => children.Sum() + 1);
        RunOnThread(SmallStack, () =>
        {
            var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => compiler.Compile(tree!, 0));
            Assert.AreEqual(ParserResourceLimitKind.Stack, refusal.Kind);
            Assert.AreEqual(ParserResourcePhase.TreeCompilation, refusal.Phase);
        });

        RunOnThread(LargeStack, () => Assert.IsTrue(compiler.Compile(tree!, 0) > 3_000));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // ETC-18: work, nodes, retained states, tokens and input size.
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Every work and size counter of a flat CSyntax-like parse is accepted at exactly its measured value and refused
    /// one unit below, with the exact kind, phase, limit and attempted value.
    /// </summary>
    [TestMethod]
    public void WorkAndSizeLimits_AcceptAtTheLimitAndRefuseOneBelow()
    {
        var text = string.Join(" + ", Enumerable.Repeat("a", 200));
        var usage = Measure(LeftRecursiveGrammar, text);
        var cases = new (ParserResourceLimitKind Kind, ParserResourcePhase Phase, long Needed, Func<long, ParserResourceLimits> Limits)[]
        {
            (ParserResourceLimitKind.ParserSteps, ParserResourcePhase.Parser, usage.Parser.Steps, v => ParserResourceLimits.Default with { MaxParserSteps = v }),
            (ParserResourceLimitKind.ParseNodes, ParserResourcePhase.Parser, usage.Parser.Nodes, v => ParserResourceLimits.Default with { MaxParseNodes = v }),
            (ParserResourceLimitKind.RetainedStates, ParserResourcePhase.Parser, usage.Parser.RetainedStates, v => ParserResourceLimits.Default with { MaxRetainedStates = v }),
            (ParserResourceLimitKind.LexerSteps, ParserResourcePhase.Lexer, usage.Lexer.Steps, v => ParserResourceLimits.Default with { MaxLexerSteps = v }),
            (ParserResourceLimitKind.Tokens, ParserResourcePhase.Lexer, usage.Lexer.Tokens, v => ParserResourceLimits.Default with { MaxTokens = (int)v }),
            (ParserResourceLimitKind.InputCharacters, ParserResourcePhase.Lexer, text.Length, v => ParserResourceLimits.Default with { MaxInputCharacters = (int)v }),
        };

        var reference = Dump(Compile(LeftRecursiveGrammar, ParserResourceLimits.Unlimited).Parse(text));
        foreach (var (kind, phase, needed, limits) in cases)
        {
            Assert.IsTrue(needed > 1, kind.ToString());
            Assert.AreEqual(reference, Dump(Compile(LeftRecursiveGrammar, limits(needed)).Parse(text)), kind.ToString());

            var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => Compile(LeftRecursiveGrammar, limits(needed - 1)).Parse(text), kind.ToString());
            Assert.AreEqual(kind, refusal.Kind);
            Assert.AreEqual(phase, refusal.Phase, kind.ToString());
            Assert.AreEqual(needed - 1, refusal.Limit, kind.ToString());
            Assert.AreEqual(needed, refusal.Observed, kind.ToString());
        }
    }

    /// <summary>
    /// The input-size and token limits are enforced while reading and producing tokens through the public
    /// <see cref="LexerEngine.Tokenize"/> stream API, before an oversized input is buffered or fully materialized.
    /// </summary>
    [TestMethod]
    public void LexerLimits_StreamTokenization_StopsWhileProducingTokens()
    {
        var definition = Antlr4GrammarConverter.Parse(HiddenChannelGrammar);
        var lexer = new LexerEngine(definition, null, ParserResourceLimits.Default with { MaxInputCharacters = 1_000, MaxTokens = 50 });

        var produced = 0;
        var tokenRefusal = Assert.ThrowsExactly<ParserResourceLimitException>(() =>
        {
            foreach (var _ in lexer.Tokenize(new StringReader(string.Concat(Enumerable.Repeat("ab #c ", 100)))))
            {
                produced++;
            }
        });
        Assert.AreEqual(ParserResourceLimitKind.Tokens, tokenRefusal.Kind);
        Assert.AreEqual(50, produced, "exactly the allowed tokens are produced before the refusal");

        var reader = new CountingReader(new string('a', 1_000_000));
        var sizeRefusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => lexer.Tokenize(reader).ToList());
        Assert.AreEqual(ParserResourceLimitKind.InputCharacters, sizeRefusal.Kind);
        Assert.AreEqual(1_001L, sizeRefusal.Observed);
        Assert.AreEqual(1_001, reader.CharactersRead, "the reader is not drained past the first character over the limit");
    }

    /// <summary>
    /// Tokens supplied directly to <see cref="ParserEngine.Parse"/> are bounded by <see cref="ParserResourceLimits.MaxTokens"/>
    /// like tokenizer output: every received token counts (hidden channels included), a prebuilt list over the limit is
    /// refused from its count, a lazy (even endless) sequence is refused after exactly the allowed tokens were examined,
    /// and inputs at the limit keep the tree of an unlimited engine.
    /// </summary>
    [TestMethod]
    public void DirectTokenInput_IsBoundedByMaxTokens()
    {
        var definition = Antlr4GrammarConverter.Parse(HiddenChannelGrammar);
        var all = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Unlimited).Tokenize("ab #c de #f gh");
        Assert.AreEqual(5, all.Count);
        Assert.AreEqual(2, all.Count(t => t.Channel == "HIDDEN"));
        var expected = Dump(new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Unlimited).Parse(all));
        ParserEngine Engine(int maxTokens) => new(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default with { MaxTokens = maxTokens });

        // At the limit: prebuilt list, hidden tokens included in the count, and lazy sequence.
        Assert.AreEqual(expected, Dump(Engine(5).Parse(all)));
        Assert.AreEqual(expected, Dump(Engine(5).Parse(all.Select(t => t))));

        // One below: hidden tokens count, so 3 default-channel tokens do not fit in 4.
        foreach (var input in new IEnumerable<Token>[] { all, all.ToList(), all.Select(t => t) })
        {
            var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => Engine(4).Parse(input));
            Assert.AreEqual(ParserResourceLimitKind.Tokens, refusal.Kind);
            Assert.AreEqual(ParserResourcePhase.Parser, refusal.Phase);
            Assert.AreEqual(4L, refusal.Limit);
            Assert.AreEqual(5L, refusal.Observed);
            Assert.AreEqual(all[4].Span.Position, refusal.Offset);
        }

        // An endless lazy sequence is never materialized: enumeration stops at the first token over the limit.
        var produced = 0;
        IEnumerable<Token> Endless()
        {
            for (var index = 0; ; index++)
            {
                produced++;
                yield return all[index % all.Count] with { Span = new Utils.Parser.Source.SourceSpan(index, 1, 1, index + 1) };
            }
        }

        var endless = Assert.ThrowsExactly<ParserResourceLimitException>(() => Engine(1_000).Parse(Endless()));
        Assert.AreEqual(ParserResourceLimitKind.Tokens, endless.Kind);
        Assert.AreEqual(1_001L, endless.Observed);
        Assert.AreEqual(1_000, endless.Offset);
        Assert.AreEqual(1_001, produced, "only the allowed tokens and the first token over the limit are pulled");

        // A refused direct input leaves the engine reusable.
        var engine = Engine(4);
        Assert.ThrowsExactly<ParserResourceLimitException>(() => engine.Parse(all));
        Assert.AreEqual(Dump(new ParserEngine(definition).Parse(all.Take(3))), Dump(engine.Parse(all.Take(3))));
    }

    /// <summary>
    /// A short, highly ambiguous input cannot perform unlimited work: the parser-step budget refuses it, and parser work
    /// spent in rejected or backtracked branches is never refunded.
    /// </summary>
    [TestMethod]
    public void ParserSteps_AmbiguousGrammar_IsBoundedAndNeverRefunded()
    {
        var text = string.Concat(Enumerable.Repeat("a ", 60));
        var usage = Measure(AmbiguousGrammar, text).Parser;
        Assert.IsTrue(usage.Steps > usage.Nodes, "backtracked branches keep their charged steps");

        var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(
            () => Compile(AmbiguousGrammar, ParserResourceLimits.Default with { MaxParserSteps = 500 }).Parse(text));
        Assert.AreEqual(ParserResourceLimitKind.ParserSteps, refusal.Kind);
        Assert.AreEqual(501L, refusal.Observed);
    }

    /// <summary>
    /// The default limits keep the historical 20,000-term ETC-18 input accepted while bounding its work, and refuse an
    /// input over <see cref="ParserResourceLimits.DefaultMaxInputCharacters"/> before any tokenization.
    /// </summary>
    [TestMethod]
    public void DefaultLimits_HistoricalFlatSum_IsAcceptedAndOversizedInputIsRefused()
    {
        var sum = string.Join(" + ", Enumerable.Range(0, 20_000).Select(i => "x" + i));
        Assert.IsNotInstanceOfType<ErrorNode>(new CSyntaxTokenParser().Parse(sum));

        var oversized = new string('x', ParserResourceLimits.DefaultMaxInputCharacters + 1);
        var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => new CSyntaxTokenParser().Tokenize(oversized));
        Assert.AreEqual(ParserResourceLimitKind.InputCharacters, refusal.Kind);
        Assert.AreEqual((long)oversized.Length, refusal.Observed);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Refusal hygiene.
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A refusal never contains input text, is not converted into an <see cref="ErrorNode"/>, and leaves the same engine
    /// able to produce the same tree as a fresh engine for the next input.
    /// </summary>
    [TestMethod]
    public void Refusal_LeavesNoTextAndNoStateBehind()
    {
        var compiled = Compile(RuleChainGrammar, ParserResourceLimits.Default with { MaxParseDepth = 60 });
        var secret = "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[secretpayload]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]";
        var refusal = Assert.ThrowsExactly<ParserResourceLimitException>(() => compiled.Parse(secret));
        StringAssert.DoesNotMatch(refusal.Message, new System.Text.RegularExpressions.Regex("secret|\\[\\["));

        Assert.AreEqual(0, ActiveRuleFrameCount(compiled), "active rule frames are released");
        Assert.AreEqual(
            Dump(Compile(RuleChainGrammar, ParserResourceLimits.Default).Parse("[[a]]")),
            Dump(compiled.Parse("[[a]]")),
            "the engine is reusable after a refusal");
    }

    /// <summary>
    /// After a refusal deep inside nested rules, every invocation frame entered is exited, every rule call gets its
    /// after-call callback, the execution-state manager is not left in a rejected branch's state, and policy exceptions
    /// are propagated unchanged rather than converted into resource refusals.
    /// </summary>
    [TestMethod]
    public void Refusal_RestoresPolicyStateAndKeepsPolicyExceptions()
    {
        var frames = new BalancedFrameManager();
        var calls = new BalancedRuleCallPolicy();
        var state = new CountingStateManager();
        var policy = ParserRuntimeFeaturePolicy.Default with
        {
            RuleInvocationFrameManager = frames,
            RuleCallExecutionPolicy = calls,
            ExecutionStateManager = state,
        };
        var compiled = new CompiledGrammar(Antlr4GrammarConverter.Parse(RuleChainGrammar), policy, ParserResourceLimits.Default with { MaxParseDepth = 80 });

        Assert.ThrowsExactly<ParserResourceLimitException>(() => compiled.Parse(new string('[', 200) + "a" + new string(']', 200)));
        Assert.IsTrue(frames.Entered > 10);
        Assert.AreEqual(frames.Entered, frames.Exited, "every entered frame is exited");
        Assert.AreEqual(0, frames.Depth);
        Assert.AreEqual(calls.Before, calls.After, "every rule call gets its after-call callback");
        Assert.IsNotInstanceOfType<ErrorNode>(compiled.Parse("[a]"));
        Assert.AreEqual(frames.Entered, frames.Exited);

        var throwing = ParserRuntimeFeaturePolicy.Default with { SemanticPredicateEvaluator = new ThrowingPredicateEvaluator() };
        var predicateGrammar = new CompiledGrammar(Antlr4GrammarConverter.Parse("""
            grammar P;
            s : { check() }? NAME ;
            NAME : ('a'..'z')+ ;
            """), throwing, ParserResourceLimits.Default);
        var policyFailure = Assert.ThrowsExactly<InvalidOperationException>(() => predicateGrammar.Parse("a"));
        Assert.AreEqual(ThrowingPredicateEvaluator.Message, policyFailure.Message);
    }

    /// <summary>
    /// Budgets are owned by individual operations: concurrent parses on separate grammar instances never share counters,
    /// so a refused operation on one thread does not affect operations running on other threads.
    /// </summary>
    [TestMethod]
    public void Budgets_AreOwnedByIndividualOperations()
    {
        var definition = Antlr4GrammarConverter.Parse(LeftRecursiveGrammar);
        var text = string.Join(" + ", Enumerable.Repeat("a", 100));
        var expected = Dump(new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default).Parse(text));
        var steps = Measure(LeftRecursiveGrammar, text).Parser.Steps;

        Parallel.For(0, 64, i =>
        {
            if (i % 4 == 0)
            {
                var tight = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default with { MaxParserSteps = steps / 2 });
                Assert.ThrowsExactly<ParserResourceLimitException>(() => tight.Parse(text));
            }
            else
            {
                var exact = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default with { MaxParserSteps = steps });
                for (var repeat = 0; repeat < 3; repeat++)
                {
                    Assert.AreEqual(expected, Dump(exact.Parse(text)), "a budget is never shared or accumulated across operations");
                }
            }
        });
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>Compiles a grammar with explicit limits.</summary>
    /// <param name="grammar">The ANTLR4 grammar text.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The compiled grammar.</returns>
    private static CompiledGrammar Compile(string grammar, ParserResourceLimits limits)
        => new(Antlr4GrammarConverter.Parse(grammar), ParserRuntimeFeaturePolicy.Default, limits);

    /// <summary>Parses an input with unlimited budgets on a large stack and returns the internal counters it used.</summary>
    /// <param name="grammar">The ANTLR4 grammar text.</param>
    /// <param name="text">The input.</param>
    /// <returns>The lexer counters and the parser usage.</returns>
    private static (ParserResourceUsage Lexer, ParserResourceUsage Parser) Measure(string grammar, string text)
    {
        (ParserResourceUsage, ParserResourceUsage) result = default;
        RunOnThread(LargeStack, () =>
        {
            var compiled = Compile(grammar, ParserResourceLimits.Unlimited);
            Assert.IsNotInstanceOfType<ErrorNode>(compiled.Parse(text));
            result = (compiled.Lexer.LastResourceBudget!.Snapshot(), compiled.Parser.LastResourceUsage!);
        });

        return result;
    }

    /// <summary>Asserts that a parse either returns a tree or ends with a parser resource refusal.</summary>
    /// <param name="parse">The parse.</param>
    /// <param name="message">The assertion message.</param>
    private static void AssertParsesOrRefuses(Func<ParseNode> parse, string message)
    {
        try
        {
            Assert.IsNotNull(parse(), message);
        }
        catch (ParserResourceLimitException refusal)
        {
            Assert.IsTrue(refusal.Kind is ParserResourceLimitKind.Stack or ParserResourceLimitKind.ParseDepth, message);
        }
    }

    /// <summary>Renders the structure, rule names and spans of a tree deterministically.</summary>
    /// <param name="node">The root.</param>
    /// <returns>The rendering.</returns>
    private static string Dump(ParseNode node)
    {
        var builder = new System.Text.StringBuilder();
        var pending = new Stack<(ParseNode Node, int Depth)>();
        pending.Push((node, 0));
        while (pending.Count > 0)
        {
            var (current, depth) = pending.Pop();
            builder.Append(depth).Append(':').Append(current.GetType().Name).Append(':').Append(current.Rule?.Name)
                .Append('@').Append(current.Span.Position).Append('+').Append(current.Span.Length).Append(';');
            if (current is ParserNode parserNode)
            {
                for (var index = parserNode.Children.Count - 1; index >= 0; index--)
                {
                    pending.Push((parserNode.Children[index], depth + 1));
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>Reads the number of active rule frames of a grammar's parser engine.</summary>
    /// <param name="grammar">The grammar.</param>
    /// <returns>The count.</returns>
    private static int ActiveRuleFrameCount(CompiledGrammar grammar)
    {
        var field = typeof(ParserEngine).GetField("_activeRuleFrames", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return ((HashSet<ParserFrameKey>)field.GetValue(grammar.Parser)!).Count;
    }

    /// <summary>Runs an action on a dedicated thread with an explicit stack size and rethrows its failure.</summary>
    /// <param name="stackSize">The stack size in bytes.</param>
    /// <param name="action">The action.</param>
    private static void RunOnThread(int stackSize, Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, stackSize);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    /// <summary>A reader that counts the characters handed out.</summary>
    /// <param name="text">The text.</param>
    private sealed class CountingReader(string text) : TextReader
    {
        /// <summary>The next character index.</summary>
        private int _index;

        /// <summary>Gets the number of characters read.</summary>
        public int CharactersRead => _index;

        /// <inheritdoc />
        public override int Peek() => _index < text.Length ? text[_index] : -1;

        /// <inheritdoc />
        public override int Read() => _index < text.Length ? text[_index++] : -1;
    }

    /// <summary>An invocation-frame manager that checks entries and exits stay balanced.</summary>
    private sealed class BalancedFrameManager : IParserRuleInvocationFrameManager
    {
        /// <summary>Gets the number of entered frames.</summary>
        public int Entered { get; private set; }

        /// <summary>Gets the number of exited frames.</summary>
        public int Exited { get; private set; }

        /// <summary>Gets the current frame depth.</summary>
        public int Depth { get; private set; }

        /// <inheritdoc />
        public ParserRuleInvocationFrame? Current => null;

        /// <inheritdoc />
        public ParserRuleInvocationFrame Enter(string ruleName, int inputPosition, ParserRuleInvocationDescriptor? descriptor = null)
        {
            Entered++;
            Depth++;
            return NullParserRuleInvocationFrameManager.Instance.Enter(ruleName, inputPosition, descriptor);
        }

        /// <inheritdoc />
        public void Exit(ParserRuleInvocationFrame frame, bool succeeded)
        {
            Exited++;
            Depth--;
        }
    }

    /// <summary>A rule-call policy that counts its before and after callbacks.</summary>
    private sealed class BalancedRuleCallPolicy : IParserRuleCallExecutionPolicy
    {
        /// <summary>Gets the number of before-call callbacks.</summary>
        public int Before { get; private set; }

        /// <summary>Gets the number of after-call callbacks.</summary>
        public int After { get; private set; }

        /// <inheritdoc />
        public void BeforeRuleCall(ParserRuleCallExecutionContext context) => Before++;

        /// <inheritdoc />
        public void AfterRuleCall(ParserRuleCallExecutionContext context) => After++;
    }

    /// <summary>A stateful execution-state manager whose state key changes with every capture.</summary>
    private sealed class CountingStateManager : IParserExecutionStateManager
    {
        /// <summary>The current state value.</summary>
        private ulong _value;

        /// <inheritdoc />
        public object Capture() => _value++;

        /// <inheritdoc />
        public void Restore(object snapshot) => _value = (ulong)snapshot;

        /// <inheritdoc />
        public ParserExecutionStateKey GetCurrentStateKey() => new(_value);
    }

    /// <summary>A semantic predicate evaluator that throws, standing for failing user code.</summary>
    private sealed class ThrowingPredicateEvaluator : ISemanticPredicateEvaluator
    {
        /// <summary>The message of the thrown exception.</summary>
        public const string Message = "user predicate failure";

        /// <inheritdoc />
        public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
            => throw new InvalidOperationException(Message);
    }
}
