using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// S-06 public contract of <see cref="ParserResourceLimits"/> and <see cref="ParserResourceLimitException"/>, and
/// compatibility of accepted inputs: below the limits, budgeting changes neither trees, spans, token sequences nor
/// diagnostics. The resource-exhaustion invariants themselves are covered by <c>UtilsTest.Security</c>.
/// </summary>
[TestClass]
public class ParserResourceLimitsTests
{
    /// <summary>Grammar with a hidden channel, skipped whitespace and direct left recursion.</summary>
    private const string MixedGrammar = """
        grammar M;
        s : e (';' e)* ;
        e : e '*' e | e '+' e | '(' e ')' | NAME ;
        NAME : ('a'..'z')+ ;
        COMMENT : '#' ('a'..'z')* -> channel(HIDDEN) ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>The defaults are the documented constants, and every default is positive.</summary>
    [TestMethod]
    public void Default_HasTheDocumentedValues()
    {
        var limits = ParserResourceLimits.Default;
        Assert.AreEqual(ParserResourceLimits.DefaultMaxInputCharacters, limits.MaxInputCharacters);
        Assert.AreEqual(ParserResourceLimits.DefaultMaxTokens, limits.MaxTokens);
        Assert.AreEqual(ParserResourceLimits.DefaultMaxLexerSteps, limits.MaxLexerSteps);
        Assert.AreEqual(ParserResourceLimits.DefaultMaxLexerDepth, limits.MaxLexerDepth);
        Assert.AreEqual(ParserResourceLimits.DefaultMaxParseDepth, limits.MaxParseDepth);
        Assert.AreEqual(ParserResourceLimits.DefaultMaxParserSteps, limits.MaxParserSteps);
        Assert.AreEqual(ParserResourceLimits.DefaultMaxParseNodes, limits.MaxParseNodes);
        Assert.AreEqual(ParserResourceLimits.DefaultMaxRetainedStates, limits.MaxRetainedStates);
        Assert.AreEqual(1_000_000, limits.MaxInputCharacters);
        Assert.AreEqual(500_000, limits.MaxTokens);
        Assert.AreEqual(50_000_000L, limits.MaxLexerSteps);
        Assert.AreEqual(4_000, limits.MaxLexerDepth);
        Assert.AreEqual(4_000, limits.MaxParseDepth);
        Assert.AreEqual(10_000_000L, limits.MaxParserSteps);
        Assert.AreEqual(5_000_000L, limits.MaxParseNodes);
        Assert.AreEqual(5_000_000L, limits.MaxRetainedStates);
        Assert.AreEqual(new ParserResourceLimits(), limits);
    }

    /// <summary><see cref="ParserResourceLimits.Unlimited"/> sets every dimension to its type's maximum value.</summary>
    [TestMethod]
    public void Unlimited_DisablesEveryDimension()
    {
        var limits = ParserResourceLimits.Unlimited;
        Assert.AreEqual(int.MaxValue, limits.MaxInputCharacters);
        Assert.AreEqual(int.MaxValue, limits.MaxTokens);
        Assert.AreEqual(long.MaxValue, limits.MaxLexerSteps);
        Assert.AreEqual(int.MaxValue, limits.MaxLexerDepth);
        Assert.AreEqual(int.MaxValue, limits.MaxParseDepth);
        Assert.AreEqual(long.MaxValue, limits.MaxParserSteps);
        Assert.AreEqual(long.MaxValue, limits.MaxParseNodes);
        Assert.AreEqual(long.MaxValue, limits.MaxRetainedStates);
    }

    /// <summary>Every limit rejects zero and negative values with the property name, and accepts 1.</summary>
    [TestMethod]
    public void EveryLimit_RejectsNonPositiveValues()
    {
        var setters = new (string Name, Func<long, ParserResourceLimits> Set)[]
        {
            (nameof(ParserResourceLimits.MaxInputCharacters), v => ParserResourceLimits.Default with { MaxInputCharacters = (int)v }),
            (nameof(ParserResourceLimits.MaxTokens), v => ParserResourceLimits.Default with { MaxTokens = (int)v }),
            (nameof(ParserResourceLimits.MaxLexerSteps), v => ParserResourceLimits.Default with { MaxLexerSteps = v }),
            (nameof(ParserResourceLimits.MaxLexerDepth), v => ParserResourceLimits.Default with { MaxLexerDepth = (int)v }),
            (nameof(ParserResourceLimits.MaxParseDepth), v => ParserResourceLimits.Default with { MaxParseDepth = (int)v }),
            (nameof(ParserResourceLimits.MaxParserSteps), v => ParserResourceLimits.Default with { MaxParserSteps = v }),
            (nameof(ParserResourceLimits.MaxParseNodes), v => ParserResourceLimits.Default with { MaxParseNodes = v }),
            (nameof(ParserResourceLimits.MaxRetainedStates), v => ParserResourceLimits.Default with { MaxRetainedStates = v }),
        };

        foreach (var (name, set) in setters)
        {
            foreach (var invalid in new long[] { 0, -1 })
            {
                var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => set(invalid), name);
                Assert.AreEqual(name, exception.ParamName);
            }

            Assert.IsNotNull(set(1), name);
        }
    }

    /// <summary>The exception exposes its fields and builds a message without input text.</summary>
    [TestMethod]
    public void Exception_ExposesItsFieldsAndAFixedMessage()
    {
        var exception = new ParserResourceLimitException(ParserResourceLimitKind.ParserSteps, ParserResourcePhase.Parser, 10, 11, 42);
        Assert.AreEqual(ParserResourceLimitKind.ParserSteps, exception.Kind);
        Assert.AreEqual(ParserResourcePhase.Parser, exception.Phase);
        Assert.AreEqual(10L, exception.Limit);
        Assert.AreEqual(11L, exception.Observed);
        Assert.AreEqual(42, exception.Offset);
        Assert.AreEqual("The Parser operation exceeded its ParserSteps limit at input offset 42 (11 > 10).", exception.Message);
        Assert.IsInstanceOfType<InvalidOperationException>(exception);

        var inner = new InsufficientExecutionStackException();
        var stack = new ParserResourceLimitException(ParserResourceLimitKind.Stack, ParserResourcePhase.Lexer, 0, 7, null, inner);
        Assert.AreSame(inner, stack.InnerException);
        Assert.IsNull(stack.Offset);
        Assert.AreEqual("The Lexer operation stopped because the remaining execution stack was insufficient (recursion depth 7).", stack.Message);
    }

    /// <summary>Engines and grammars expose their limits, use the defaults when none are given, and reject null limits.</summary>
    [TestMethod]
    public void Engines_ExposeTheirLimits()
    {
        var definition = Antlr4GrammarConverter.Parse(MixedGrammar);
        var custom = ParserResourceLimits.Default with { MaxParseDepth = 123 };

        Assert.AreSame(ParserResourceLimits.Default, new CompiledGrammar(definition).ResourceLimits);
        Assert.AreSame(ParserResourceLimits.Default, new ParserEngine(definition).ResourceLimits);
        Assert.AreSame(ParserResourceLimits.Default, new LexerEngine(definition).ResourceLimits);
        Assert.AreSame(custom, new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, custom).ResourceLimits);
        Assert.AreSame(custom, new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default, custom).ResourceLimits);
        Assert.AreSame(custom, new LexerEngine(definition, null, custom).ResourceLimits);
        Assert.ThrowsExactly<ArgumentNullException>(() => new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LexerEngine(definition, null, null!));
    }

    /// <summary>
    /// Below the limits, default and unlimited budgets produce the same tokens (all channels, texts and spans), the same
    /// tree shapes and spans, the same diagnostics and the same syntax-error outcomes.
    /// </summary>
    [TestMethod]
    public void AcceptedInputs_AreIndependentOfTheBudget()
    {
        var definition = Antlr4GrammarConverter.Parse(MixedGrammar);
        var inputs = new[]
        {
            "a", "a + b * c", "(a + b) * c ; d", "a # note\n+ b", "((((a))))", "a + ", "a b", string.Empty,
            string.Join(" + ", Enumerable.Repeat("x", 300)),
        };

        foreach (var input in inputs)
        {
            var bounded = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default);
            var unbounded = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Unlimited);
            Assert.AreEqual(DumpTokens(unbounded.Tokenize(input)), DumpTokens(bounded.Tokenize(input)), input);
            Assert.AreEqual(Dump(unbounded.Parse(input)), Dump(bounded.Parse(input)), input);

            var boundedDiagnostics = new DiagnosticBag();
            var unboundedDiagnostics = new DiagnosticBag();
            var boundedTree = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Default)
                .Parse(bounded.Tokenize(input), diagnostics: boundedDiagnostics);
            var unboundedTree = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Unlimited)
                .Parse(unbounded.Tokenize(input), diagnostics: unboundedDiagnostics);
            Assert.AreEqual(Dump(unboundedTree), Dump(boundedTree), input);
            CollectionAssert.AreEqual(
                unboundedDiagnostics.Select(d => d.Code + ":" + d.Message).ToArray(),
                boundedDiagnostics.Select(d => d.Code + ":" + d.Message).ToArray(),
                input);
        }
    }

    /// <summary>
    /// The CSyntax front end parses the same trees under the default limits as an unlimited engine over the same
    /// definition.
    /// </summary>
    [TestMethod]
    public void CSyntax_AcceptedExpressions_AreIndependentOfTheBudget()
    {
        var field = typeof(CSyntaxTokenParser).GetField("SharedGrammar", BindingFlags.NonPublic | BindingFlags.Static)!;
        var definition = ((Lazy<CompiledGrammar>)field.GetValue(null)!).Value.Definition;
        var unbounded = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Unlimited);
        foreach (var input in new[] { "x", "-(x + 2) * y", "f(a, b[1]).c", "x => x * 2", "((((x0))))", "\"text\" + 'c'", "if (a) b = 1; else b = 2;" })
        {
            Assert.AreEqual(Dump(unbounded.Parse(input)), Dump(new CSyntaxTokenParser().Parse(input)), input);
        }
    }

    /// <summary>
    /// Parse-scoped registry, memo and look-ahead state is released when the parse ends, so a long-lived engine does not
    /// keep the parse states of its last input alive.
    /// </summary>
    [TestMethod]
    public void Parse_ReleasesParseScopedStateWhenItEnds()
    {
        var definition = Antlr4GrammarConverter.Parse(MixedGrammar);
        var engine = new ParserEngine(definition);
        var tokens = new CompiledGrammar(definition).Tokenize("a + b * (c + d) ; e");
        Assert.IsNotInstanceOfType<ErrorNode>(engine.Parse(tokens));

        var registry = (ParserStateRegistry)Field(engine, "_stateRegistry");
        Assert.AreEqual(0L, registry.CompletedResultCount);
        Assert.AreEqual(0, registry.CompletedInvocationCount);
        Assert.AreEqual(0, registry.ContinuationCount);
        Assert.AreEqual(0L, registry.VisitedStateCount);
        Assert.AreEqual(0, registry.VisitedStateGroupCount);
        Assert.AreEqual(0, ((ParserLookaheadCache)Field(engine, "_lookaheadCache")).Count);
        Assert.IsTrue(engine.LastResourceUsage!.RetainedStates > 0, "the parse did retain states while it ran");
    }

    /// <summary>
    /// <see cref="ParserEngine.Parse"/> keeps filtering non-default channels, whether the caller passes a list of
    /// default-channel tokens (reused without copying), a list containing hidden tokens, or a lazy sequence.
    /// </summary>
    [TestMethod]
    public void Parse_FiltersHiddenChannelsForEveryTokenSource()
    {
        var definition = Antlr4GrammarConverter.Parse(MixedGrammar);
        var all = new CompiledGrammar(definition).Tokenize("a #x\n+ #y\nb");
        Assert.IsTrue(all.Any(t => t.Channel == "HIDDEN"));
        var visible = all.Where(t => t.Channel == "DEFAULT_CHANNEL").ToList();

        var expected = Dump(new ParserEngine(definition).Parse(visible));
        Assert.AreEqual(expected, Dump(new ParserEngine(definition).Parse(all)));
        Assert.AreEqual(expected, Dump(new ParserEngine(definition).Parse(all.Where(_ => true))));
        Assert.AreEqual(3, visible.Count);
    }

    /// <summary>The iterative <see cref="ParseTreeNavigator.Descendants()"/> keeps the depth-first pre-order of the recursive definition.</summary>
    [TestMethod]
    public void Descendants_KeepsDepthFirstPreOrder()
    {
        var tree = new CompiledGrammar(Antlr4GrammarConverter.Parse(MixedGrammar)).Parse("(a + b) * c + d ; e * (f + g)");
        var navigator = new ParseTreeNavigator(tree);

        static IEnumerable<ParseNode> Reference(ParseNode node)
        {
            if (node is not ParserNode parserNode) yield break;
            foreach (var child in parserNode.Children)
            {
                yield return child;
                foreach (var grandchild in Reference(child)) yield return grandchild;
            }
        }

        var expected = Reference(tree).ToList();
        var actual = navigator.Descendants().Select(n => n.Node).ToList();
        Assert.IsTrue(expected.Count > 20);
        CollectionAssert.AreEqual(expected, actual);
        CollectionAssert.AreEqual(expected.Where(n => n.Rule?.Name == "e").ToList(), navigator.Descendants("e").Select(n => n.Node).ToList());
        Assert.AreEqual(0, new ParseTreeNavigator(navigator.Descendants().First(n => n.IsLexer).Node).Descendants().Count());
    }

    /// <summary>Reads a private instance field.</summary>
    /// <param name="target">The object.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value.</returns>
    private static object Field(object target, string name)
        => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    /// <summary>Renders tokens with their channel, text and span.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The rendering.</returns>
    private static string DumpTokens(IReadOnlyList<Token> tokens)
        => string.Join("|", tokens.Select(t => $"{t.RuleName}:{t.Channel}:{t.Text}@{t.Span.Position}+{t.Span.Length}/{t.Span.Line}.{t.Span.Column}"));

    /// <summary>Renders the structure, node types, rule names and spans of a tree deterministically.</summary>
    /// <param name="node">The root.</param>
    /// <returns>The rendering.</returns>
    private static string Dump(ParseNode node)
    {
        var builder = new StringBuilder();
        var pending = new Stack<(ParseNode Node, int Depth)>();
        pending.Push((node, 0));
        while (pending.Count > 0)
        {
            var (current, depth) = pending.Pop();
            builder.Append(depth).Append(':').Append(current.GetType().Name).Append(':').Append(current.Rule?.Name)
                .Append('@').Append(current.Span.Position).Append('+').Append(current.Span.Length);
            if (current is ErrorNode error) builder.Append('!').Append(error.Message);
            builder.Append(';');
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
}
