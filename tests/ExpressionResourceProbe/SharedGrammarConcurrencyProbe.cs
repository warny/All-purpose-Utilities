using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Parser.Runtime;

namespace ExpressionResourceProbe;

/// <summary>
/// S-06 §11 characterization: runs concurrent <see cref="CSyntaxTokenParser.Parse"/> and
/// <see cref="CSyntaxTokenParser.Tokenize"/> calls, which share one lazily-initialized <see cref="CompiledGrammar"/>,
/// and compares every result with a single-threaded reference. Prints one <c>CRESULT</c> line with the number of
/// corrupted trees, corrupted token lists and exceptions by type.
/// </summary>
/// <remarks>Usage: <c>ExpressionResourceProbe parser-concurrency &lt;threads&gt; &lt;iterations&gt;</c>.</remarks>
internal static class SharedGrammarConcurrencyProbe
{
    /// <summary>Runs the characterization.</summary>
    /// <param name="threads">The number of concurrent workers.</param>
    /// <param name="iterations">The number of operations per worker.</param>
    /// <returns>0 when every result matched its reference, 1 otherwise.</returns>
    public static int Run(int threads, int iterations)
    {
        string[] inputs =
        [
            "a + b * c",
            "f(x, y[2]).z - (1 + 2) * 3",
            string.Join(" + ", Enumerable.Range(0, 60).Select(i => "x" + i.ToString(CultureInfo.InvariantCulture))),
            "if (a) b = 1; else b = 2;",
            "((((x0))))",
            "x => x * 2 + \"text\".Length",
        ];

        var parser = new CSyntaxTokenParser();
        string[] treeReferences = inputs.Select(input => Dump(parser.Parse(input))).ToArray();
        string[] tokenReferences = inputs.Select(input => string.Join("|", parser.Tokenize(input).Select(t => t.RuleName + ":" + t.Text + "@" + t.Position))).ToArray();

        long corruptedTrees = 0;
        long corruptedTokens = 0;
        var exceptions = new Dictionary<string, long>(StringComparer.Ordinal);
        var gate = new object();
        using var start = new Barrier(threads);

        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, worker =>
        {
            var local = new CSyntaxTokenParser();
            start.SignalAndWait();
            for (int i = 0; i < iterations; i++)
            {
                int index = (worker + i) % inputs.Length;
                try
                {
                    if ((i & 1) == 0)
                    {
                        if (Dump(local.Parse(inputs[index])) != treeReferences[index]) Interlocked.Increment(ref corruptedTrees);
                    }
                    else if (string.Join("|", local.Tokenize(inputs[index]).Select(t => t.RuleName + ":" + t.Text + "@" + t.Position)) != tokenReferences[index])
                    {
                        Interlocked.Increment(ref corruptedTokens);
                    }
                }
                catch (Exception exception)
                {
                    lock (gate)
                    {
                        string name = exception.GetType().Name;
                        exceptions[name] = exceptions.TryGetValue(name, out long count) ? count + 1 : 1;
                    }
                }
            }
        });

        // The shared grammar must still parse correctly once the concurrent phase is over.
        bool sequentialAfter = inputs.Select((input, index) => Dump(parser.Parse(input)) == treeReferences[index]).All(ok => ok);
        string exceptionText = exceptions.Count == 0 ? "none" : string.Join(",", exceptions.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine($"CRESULT threads={threads} iterations={iterations} operations={(long)threads * iterations} corruptedTrees={corruptedTrees} corruptedTokens={corruptedTokens} exceptions={exceptionText} sequentialAfter={sequentialAfter}");
        return corruptedTrees == 0 && corruptedTokens == 0 && exceptions.Count == 0 && sequentialAfter ? 0 : 1;
    }

    /// <summary>Renders the structure, node types, rule names and spans of a tree.</summary>
    /// <param name="node">The root.</param>
    /// <returns>The rendering.</returns>
    private static string Dump(ParseNode node)
    {
        var builder = new StringBuilder();
        var pending = new Stack<(ParseNode Node, int Depth)>();
        pending.Push((node, 0));
        while (pending.Count > 0)
        {
            (ParseNode current, int depth) = pending.Pop();
            builder.Append(depth).Append(':').Append(current.GetType().Name).Append(':').Append(current.Rule?.Name)
                .Append('@').Append(current.Span.Position).Append('+').Append(current.Span.Length).Append(';');
            if (current is ParserNode parserNode)
            {
                for (int index = parserNode.Children.Count - 1; index >= 0; index--) pending.Push((parserNode.Children[index], depth + 1));
            }
        }

        return builder.ToString();
    }
}
