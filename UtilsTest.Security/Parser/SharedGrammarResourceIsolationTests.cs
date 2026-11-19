using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Security.Parser;

/// <summary>
/// Issue #709 and S-06: resource budgets stay owned by individual operations when many operations run concurrently on
/// one shared <see cref="CompiledGrammar"/>. A refusal on one thread must not change the counters, the refusal or the
/// result of any other operation, and must leave nothing behind for later operations.
/// </summary>
[TestClass]
public class SharedGrammarResourceIsolationTests
{
    /// <summary>A left-recursive grammar with a parenthesized form.</summary>
    private const string Grammar = """
        grammar L;
        e : e '+' e | '(' e ')' | NAME ;
        NAME : ('a'..'z')+ ;
        WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;

    /// <summary>
    /// Accepted inputs, a token-limit refusal, a depth refusal and a step refusal run concurrently on one shared grammar;
    /// every result and every refusal (kind, phase, limit, observed value and offset) equals the result of the same input
    /// on a fresh grammar used sequentially, and the grammar is still exact afterwards.
    /// </summary>
    [TestMethod]
    public void ConcurrentRefusals_OnSharedGrammar_AreIndependentAndExact()
    {
        var definition = Antlr4GrammarConverter.Parse(Grammar);
        string[] inputs =
        [
            "a + b",
            new string('(', 8) + "a" + new string(')', 8),
            string.Join(" + ", Enumerable.Repeat("a", 12)),
            string.Join(" + ", Enumerable.Repeat("a", 55)),
            string.Join(" + ", Enumerable.Repeat("a", 61)),
            new string('(', 40) + "a" + new string(')', 40),
        ];

        // Limits are derived from the needs of the inputs measured without limits: the depth limit sits just above the
        // deepest accepted input and the step limit just below the need of the 55-term sum, so that sum is refused on its
        // parser steps, the 61-term sum on its tokens and the 40-level nesting on its depth.
        var unlimited = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, ParserResourceLimits.Unlimited);
        ParserResourceUsage Measure(string input)
        {
            _ = unlimited.Parse(input);
            return unlimited.Parser.LastResourceUsage!;
        }

        int acceptedDepth = inputs.Take(4).Max(input => Measure(input).MaxDepth);
        long sumSteps = Measure(inputs[3]).Steps;
        var limits = ParserResourceLimits.Default with { MaxTokens = 120, MaxParseDepth = acceptedDepth, MaxParserSteps = sumSteps - 1 };
        string[] expected = inputs.Select(input => Run(new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, limits), input)).ToArray();
        string corpus = string.Join(" | ", expected.Select(e => e.StartsWith('!') ? e : "accepted"));
        Assert.IsTrue(expected.Any(e => e.StartsWith("!Tokens", StringComparison.Ordinal)), "the corpus contains a token refusal: " + corpus);
        Assert.IsTrue(expected.Any(e => e.StartsWith("!ParseDepth", StringComparison.Ordinal) || e.StartsWith("!Stack", StringComparison.Ordinal)), "the corpus contains a depth refusal: " + corpus);
        Assert.IsTrue(expected.Any(e => e.StartsWith("!ParserSteps", StringComparison.Ordinal)), "the corpus contains a step refusal: " + corpus);
        Assert.IsTrue(expected.Count(e => !e.StartsWith('!')) >= 2, "the corpus contains accepted inputs: " + corpus);

        var shared = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, limits);
        const int Threads = 16;
        const int Operations = 120;
        var mismatches = new List<string>();
        using var barrier = new Barrier(Threads);
        var workers = Enumerable.Range(0, Threads).Select(worker => new Thread(() =>
        {
            var random = new Random(709 + worker);
            for (int i = 0; i < Operations; i++)
            {
                if (i % 6 == 0) barrier.SignalAndWait();
                int index = random.Next(inputs.Length);
                string actual = Run(shared, inputs[index]);
                if (actual != expected[index])
                {
                    lock (mismatches) mismatches.Add($"input {index}: {actual[..Math.Min(160, actual.Length)]}");
                }
            }

            barrier.RemoveParticipant();
        }, 16 * 1024 * 1024)).ToArray();
        foreach (var worker in workers) worker.Start();
        foreach (var worker in workers) worker.Join();

        Assert.AreEqual(0, mismatches.Count, "concurrent operations influenced each other:\n" + string.Join("\n", mismatches.Take(5)));
        CollectionAssert.AreEqual(expected, inputs.Select(input => Run(shared, input)).ToArray(), "no budget or state survives the concurrent phase");
    }

    /// <summary>
    /// After a refusal, the next operation on the same shared grammar starts from a clean budget and clean engine state:
    /// its counters equal those of the same operation on a fresh grammar and no rule frame is left active.
    /// </summary>
    [TestMethod]
    public void OperationAfterRefusal_StartsFromCleanBudgetAndState()
    {
        var definition = Antlr4GrammarConverter.Parse(Grammar);
        var limits = ParserResourceLimits.Default with { MaxParseDepth = 150 };
        const string accepted = "(a + b) + c";
        var fresh = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, limits);
        string expectedTree = Run(fresh, accepted);
        ParserResourceUsage expectedUsage = fresh.Parser.LastResourceUsage!;

        var shared = new CompiledGrammar(definition, ParserRuntimeFeaturePolicy.Default, limits);
        Assert.ThrowsExactly<ParserResourceLimitException>(() => shared.Parse(new string('(', 200) + "a" + new string(')', 200)));
        Assert.AreEqual(expectedTree, Run(shared, accepted));
        ParserResourceUsage usage = shared.Parser.LastResourceUsage!;
        Assert.AreEqual(expectedUsage.Steps, usage.Steps, "the budget is not inherited from the refused operation");
        Assert.AreEqual(expectedUsage.Nodes, usage.Nodes);
        Assert.AreEqual(expectedUsage.MaxDepth, usage.MaxDepth);
        Assert.AreEqual(expectedUsage.RetainedStates, usage.RetainedStates);
    }

    /// <summary>Parses an input and renders the tree, or the refusal with its full classification.</summary>
    /// <param name="grammar">The grammar.</param>
    /// <param name="input">The input.</param>
    /// <returns>The rendering; refusals start with <c>!</c>, any other exception with <c>!!</c>.</returns>
    private static string Run(CompiledGrammar grammar, string input)
    {
        try
        {
            return Dump(grammar.Parse(input));
        }
        catch (ParserResourceLimitException refusal)
        {
            return $"!{refusal.Kind}:{refusal.Phase}:{refusal.Limit}:{refusal.Observed}:{refusal.Offset}";
        }
        catch (Exception exception)
        {
            // Rendered rather than rethrown, so an engine-state corruption fails the assertion instead of the test host.
            return $"!!{exception.GetType().Name}: {exception.Message}";
        }
    }

    /// <summary>Renders the structure, rule names and spans of a tree deterministically.</summary>
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
}
