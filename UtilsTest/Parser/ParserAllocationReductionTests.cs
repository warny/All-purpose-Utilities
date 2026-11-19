using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;
using Utils.Parser.Source;

namespace UtilsTest.Parser;

/// <summary>
/// S-06 allocation reductions are behavior-neutral: precomputed grammar-invariant scheduling shapes, the scheduler's
/// observer-free and single-candidate fast paths, and the lexer's precomputed candidate rules and reused scratch buffers
/// produce exactly the results of the general paths.
/// </summary>
[TestClass]
public class ParserAllocationReductionTests
{
    /// <summary>Grammar whose rule has several alternatives with distinct priorities and an inline alternation.</summary>
    private const string Grammar = """
        grammar S;
        s : NAME '=' v | NAME '(' ')' | NAME ( '+' | '-' ) NAME | NAME ;
        v : NAME | NUMBER ;
        NAME : ('a'..'z')+ ;
        NUMBER : ('0'..'9')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>
    /// The scheduler returns the same selected, completed, failed and pruned states with and without an observer, for
    /// sorted and unsorted inputs, with zero, one and several completed candidates; the observer sees every event.
    /// </summary>
    [TestMethod]
    public void Scheduler_FastPaths_MatchTheGeneralPath()
    {
        var definition = Antlr4GrammarConverter.Parse(Grammar);
        var rule = definition.AllRules["s"];
        var ordered = rule.Content.Alternatives.OrderBy(a => a.Priority).ToList();
        var reversed = Enumerable.Reverse(ordered).ToList();
        var context = new ParseContext([]);

        foreach (var successes in new int[][] { [], [1], [0, 2], [0, 1, 2, 3] })
        {
            foreach (var input in new[] { ordered, reversed })
            {
                var observer = new RecordingObserver();
                var withObserver = Run(new AlternativeScheduler(observer), rule, input, context, definition, successes);
                var withoutObserver = Run(new AlternativeScheduler(), rule, input, context, definition, successes);
                var label = $"successes [{string.Join(",", successes)}], {(ReferenceEquals(input, ordered) ? "sorted" : "unsorted")}";

                Assert.AreEqual(withObserver.SelectedState, withoutObserver.SelectedState, label);
                CollectionAssert.AreEqual(withObserver.CompletedStates.ToList(), withoutObserver.CompletedStates.ToList(), label);
                CollectionAssert.AreEqual(withObserver.FailedStates.ToList(), withoutObserver.FailedStates.ToList(), label);
                CollectionAssert.AreEqual(withObserver.PrunedStates.ToList(), withoutObserver.PrunedStates.ToList(), label);
                Assert.AreEqual(ordered.Count - successes.Length, withoutObserver.FailedStates.Count, label);
                Assert.AreEqual(ordered.Count, observer.Started, label);
                Assert.AreEqual(ordered.Count - successes.Length, observer.Failed, label);
                Assert.AreEqual(successes.Length, observer.Completed, label);
                Assert.AreEqual(successes.Length == 0 ? 0 : 1, observer.Selected, label);
                if (successes.Length == 1)
                {
                    Assert.AreEqual(1, withoutObserver.CompletedStates.Count, label);
                    Assert.AreEqual(0, withoutObserver.PrunedStates.Count, label);
                }
            }
        }
    }

    /// <summary>
    /// The precomputed scheduling shapes and the on-the-fly fallback (a start rule that is not part of the definition)
    /// produce the same tree.
    /// </summary>
    [TestMethod]
    public void SchedulingShapes_PrecomputedAndFallback_ProduceTheSameTree()
    {
        var definition = Antlr4GrammarConverter.Parse(Grammar);
        var tokens = new CompiledGrammar(definition).Tokenize("a + b");
        var rule = definition.AllRules["s"];
        var detached = rule with { Content = new Alternation(rule.Content.Alternatives.ToList()) };
        Assert.IsFalse(ReferenceEquals(rule.Content.Alternatives, detached.Content.Alternatives));

        var precomputed = new ParserEngine(definition).Parse(tokens, rule);
        var fallback = new ParserEngine(definition).Parse(tokens, detached);
        Assert.IsNotInstanceOfType<ErrorNode>(precomputed);
        Assert.AreEqual(Shape(precomputed), Shape(fallback));
    }

    /// <summary>
    /// Commands collected by a candidate rule that matches no character (a zero-length success) or loses to a longer
    /// candidate never leak into the accepted token: the reused scratch buffers are cleared per candidate.
    /// </summary>
    [TestMethod]
    public void LexerScratchBuffers_DoNotLeakCommandsBetweenCandidates()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar L;
            s : (A | AB | NAME)* ;
            EMPTY : ( 'z' )? -> skip ;
            A : 'a' -> channel(HIDDEN) ;
            AB : 'ab' ;
            NAME : 'c' ;
            """);
        var tokens = new CompiledGrammar(definition).Tokenize("abcac");
        CollectionAssert.AreEqual(
            new[] { "AB:DEFAULT_CHANNEL:ab", "NAME:DEFAULT_CHANNEL:c", "A:HIDDEN:a", "NAME:DEFAULT_CHANNEL:c" },
            tokens.Select(t => $"{t.RuleName}:{t.Channel}:{t.Text}").ToArray());
    }

    /// <summary>Runs the scheduler with real preparation inputs and a scripted alternative outcome.</summary>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="alternatives">The alternatives, in the order passed to the scheduler.</param>
    /// <param name="context">The cursor.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="successes">The indexes, in priority order, of the alternatives that complete.</param>
    /// <returns>The scheduling result.</returns>
    private static AlternativeSchedulingResult Run(AlternativeScheduler scheduler, Rule rule, IReadOnlyList<Alternative> alternatives, ParseContext context, ParserDefinition definition, int[] successes)
    {
        var ordered = alternatives.OrderBy(a => a.Priority).ToList();
        var prepared = new SchedulingPreparation(new ParserLookaheadProbe(), new ParserLookaheadCache(), name => definition.AllRules.TryGetValue(name, out var found) ? found : null)
            .Prepare(rule, ordered, new SchedulingPreparationContext(context, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1, false, _ => true));
        return scheduler.Run(
            rule,
            alternatives,
            originInputPosition: 0,
            minimumPrecedence: 0,
            diagnostics: null,
            parseAlternative: (alternative, index) => successes.Contains(index)
                ? new ScheduledAlternativeExecutionResult(Completed(rule, alternative, index, end: 1 + index % 2), new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, null, null))
                : new ScheduledAlternativeExecutionResult(null, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null)),
            precomputedDescriptors: prepared.StructuralDescriptors,
            precomputedContinuationMetadata: prepared.ContinuationDescriptors,
            precomputedLookaheadProbes: prepared.LookaheadProbes,
            precomputedSharedPrefixCandidates: prepared.SharedPrefixCandidates);
    }

    /// <summary>Creates a completed scheduled state.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="alternative">The alternative.</param>
    /// <param name="index">The alternative index.</param>
    /// <param name="end">The end position.</param>
    /// <returns>The state.</returns>
    private static ActiveParseState Completed(Rule rule, Alternative alternative, int index, int end) => new()
    {
        Rule = rule,
        Alternative = alternative,
        OriginInputPosition = 0,
        CurrentInputPosition = end,
        AlternativeIndex = index,
        Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
        PartialNode = new ParserNode(new SourceSpan(0, end), "DEFAULT_MODE", rule, []),
        EndPosition = end,
        Status = ActiveParseStateStatus.Completed,
        ParentStateKey = null,
        Depth = 0,
        Continuation = null,
    };

    /// <summary>Renders a tree's node types, rules and spans in pre-order.</summary>
    /// <param name="node">The root.</param>
    /// <returns>The rendering.</returns>
    private static string Shape(ParseNode node)
        => node is ParserNode parserNode
            ? $"{node.GetType().Name}:{node.Rule?.Name}@{node.Span.Position}+{node.Span.Length}[{string.Join(",", parserNode.Children.Select(Shape))}]"
            : $"{node.GetType().Name}:{node.Rule?.Name}@{node.Span.Position}+{node.Span.Length}";

    /// <summary>Counts scheduler observations by kind.</summary>
    private sealed class RecordingObserver : IParserRuntimeObserver
    {
        /// <summary>Gets the started count.</summary>
        public int Started { get; private set; }

        /// <summary>Gets the completed count.</summary>
        public int Completed { get; private set; }

        /// <summary>Gets the failed count.</summary>
        public int Failed { get; private set; }

        /// <summary>Gets the pruned count.</summary>
        public int Pruned { get; private set; }

        /// <summary>Gets the selected count.</summary>
        public int Selected { get; private set; }

        /// <inheritdoc />
        public void OnAlternativeStarted(AlternativeRuntimeObservation observation) => Started++;

        /// <inheritdoc />
        public void OnAlternativeCompleted(AlternativeRuntimeObservation observation) => Completed++;

        /// <inheritdoc />
        public void OnAlternativeFailed(AlternativeRuntimeObservation observation) => Failed++;

        /// <inheritdoc />
        public void OnAlternativePruned(AlternativeRuntimeObservation observation) => Pruned++;

        /// <inheritdoc />
        public void OnAlternativeSelected(AlternativeRuntimeObservation observation) => Selected++;
    }
}
