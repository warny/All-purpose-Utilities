using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Security.Parser;

/// <summary>
/// Exactness of the compact <see cref="ParserLookaheadCache"/>: every operation sequence gives the same answers, the same
/// stored instances and the same S-06 retained-state charges and refusals as the historical dictionary implementation,
/// kept here verbatim as the reference model (<see cref="HistoricalLookaheadCache"/>).
/// </summary>
[TestClass]
public class ParserLookaheadCacheEquivalenceTests
{
    /// <summary>The input offset reported by the test budgets.</summary>
    private const int Offset = 17;

    /// <summary>Rule names, including a copy of an existing name built as another instance.</summary>
    private static readonly string[] RuleNames = ["expr", "term", "factor", "primary", new string("expr".ToCharArray()), "e", "s"];

    /// <summary>Cursor kinds: the packed constants, copies of them as other instances, and unusual values.</summary>
    private static readonly string[] CursorKinds =
    [
        ScheduledAlternativeCursorKinds.RuleRoot,
        ScheduledAlternativeCursorKinds.LeftRecursiveSeed,
        ScheduledAlternativeCursorKinds.Alternation,
        ScheduledAlternativeCursorKinds.AlternativeRoot,
        new string(ScheduledAlternativeCursorKinds.RuleRoot.ToCharArray()),
        new string(ScheduledAlternativeCursorKinds.Alternation.ToCharArray()),
        "custom-cursor",
        string.Empty,
    ];

    /// <summary>Alternative indexes around the packed range, negative and extreme.</summary>
    private static readonly int[] AlternativeIndexes = [0, 1, 2, 3, 7, 15, 63, 64, 254, 255, 256, 1000, -1, int.MaxValue, int.MinValue];

    /// <summary>Minimum precedences around the packed range, negative and extreme.</summary>
    private static readonly int[] Precedences = [0, 1, 2, 3, 62, 63, 64, -1, int.MaxValue, int.MinValue];

    /// <summary>Cursor indexes around the packed range, negative and extreme.</summary>
    private static readonly int[] CursorIndexes = [-1, 0, 1, 2, 61, 62, 63, 64, -2, int.MaxValue, int.MinValue];

    /// <summary>Probe kinds: every defined kind and two values outside the enumeration.</summary>
    private static readonly ParserLookaheadProbeKind[] Kinds =
    [
        ParserLookaheadProbeKind.Unknown,
        ParserLookaheadProbeKind.ImmediateReject,
        ParserLookaheadProbeKind.RequiresParse,
        ParserLookaheadProbeKind.EpsilonPossible,
        (ParserLookaheadProbeKind)300,
        (ParserLookaheadProbeKind)(-1),
    ];

    /// <summary>
    /// Several hundred deterministic pseudo-random sequences (insertions, lookups, duplicates, near keys, every field
    /// varied, unusual values, S-06 limits near the number of distinct entries, refusals and operations after them,
    /// clears and reuse) are replayed on both caches and compared after every operation.
    /// </summary>
    [TestMethod]
    public void RandomOperationSequences_MatchHistoricalCache()
    {
        int refusals = 0, clears = 0, overflowed = 0, maxCount = 0;
        for (int seed = 0; seed < 400; seed++)
        {
            var random = new Random(seed);
            var compact = new ParserLookaheadCache();
            var historical = new HistoricalLookaheadCache();
            var keys = new List<ParserLookaheadKey>();

            // Mostly unlimited budgets, otherwise a limit near the distinct entry count of the sequence, or tiny.
            long? limit = (seed % 4) switch { 0 => null, 1 => 900 + random.Next(400), 2 => 1 + random.Next(30), _ => 1_500 + random.Next(1_000) };
            (ParserResourceBudget? compactBudget, ParserResourceBudget? historicalBudget) = (Budget(limit), Budget(limit));
            var tokens = new Dictionary<int, (string? Rule, string? Text)>();
            int operations = 2_500 + random.Next(1_000);
            for (int step = 0; step < operations; step++)
            {
                string context = $"seed {seed} step {step}";
                int choice = random.Next(800) == 0 ? -1 : random.Next(100);
                if (choice < 0)
                {
                    compact.Clear();
                    historical.Clear();
                    keys.Clear();
                    tokens.Clear();
                    (compactBudget, historicalBudget) = (Budget(limit), Budget(limit));
                    clears++;
                    Assert.AreEqual(0, compact.Count, context);
                }
                else if (choice < 35 && keys.Count > 0)
                {
                    // Lookup of a known key, of a near key, or of a fresh key.
                    ParserLookaheadKey key = random.Next(3) switch
                    {
                        0 => keys[random.Next(keys.Count)],
                        1 => Near(random, keys[random.Next(keys.Count)]),
                        _ => RandomKey(random),
                    };
                    AssertSameLookup(compact, historical, key, context);
                }
                else
                {
                    // Insertion of a fresh key, a duplicate (possibly with another result) or a near key.
                    ParserLookaheadKey key = keys.Count == 0 || random.Next(3) == 0
                        ? RandomKey(random)
                        : random.Next(2) == 0 ? keys[random.Next(keys.Count)] : Near(random, keys[random.Next(keys.Count)]);
                    ParserLookaheadProbeResult result = RandomResult(random, key.OriginPosition, tokens);
                    string compactOutcome = Outcome(() => compact.TryAdd(key, result, compactBudget));
                    string historicalOutcome = Outcome(() => historical.TryAdd(key, result, historicalBudget));
                    Assert.AreEqual(historicalOutcome, compactOutcome, context);
                    Assert.AreEqual(historicalBudget?.RetainedStates, compactBudget?.RetainedStates, context);
                    if (compactOutcome.StartsWith("refused", StringComparison.Ordinal)) refusals++;
                    keys.Add(key);
                    AssertSameLookup(compact, historical, key, context);
                }

                Assert.AreEqual(historical.Count, compact.Count, context);
                maxCount = Math.Max(maxCount, compact.Count);
                if (step % 500 == 0) AssertSameContent(compact, historical, context);
            }

            overflowed += compact.OverflowCount;
            AssertSameContent(compact, historical, $"seed {seed} end");
            compact.Clear();
            historical.Clear();
            Assert.AreEqual(0, compact.Count);
            foreach (ParserLookaheadKey key in keys.Take(50)) AssertSameLookup(compact, historical, key, $"seed {seed} after clear");
        }

        Assert.IsTrue(refusals > 100, $"the campaign exercises refusals ({refusals})");
        Assert.IsTrue(clears > 100, $"the campaign exercises clears ({clears})");
        Assert.IsTrue(overflowed > 1_000, $"the campaign exercises the overflow store ({overflowed})");
        Assert.IsTrue(maxCount > 1_000, $"the campaign grows the caches through several resizes ({maxCount})");
    }

    /// <summary>A limit equal to the number of distinct entries accepts them all; one less refuses the last one, before it is stored.</summary>
    [TestMethod]
    public void RetainedStateLimit_AtAndBelowTheDistinctEntryCount()
    {
        ParserLookaheadKey[] keys = Enumerable.Range(0, 200)
            .Select(i => new ParserLookaheadKey(RuleNames[i % 4], i / 10, i % 10, 0, CursorKinds[i % 3], -1))
            .Append(new ParserLookaheadKey("expr", 3, 1, 0, "custom-cursor", 5))
            .Append(new ParserLookaheadKey("expr", 3, 300, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1))
            .ToArray();
        var result = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "x", ["ID"]);

        foreach (long limit in new long[] { keys.Length, keys.Length - 1 })
        {
            var compact = new ParserLookaheadCache();
            var historical = new HistoricalLookaheadCache();
            ParserResourceBudget compactBudget = Budget(limit)!, historicalBudget = Budget(limit)!;
            var compactOutcomes = new List<string>();
            var historicalOutcomes = new List<string>();
            foreach (ParserLookaheadKey key in keys)
            {
                compactOutcomes.Add(Outcome(() => compact.TryAdd(key, result, compactBudget)));
                historicalOutcomes.Add(Outcome(() => historical.TryAdd(key, result, historicalBudget)));
            }

            CollectionAssert.AreEqual(historicalOutcomes, compactOutcomes);
            Assert.AreEqual(historicalBudget.RetainedStates, compactBudget.RetainedStates);
            if (limit == keys.Length)
            {
                Assert.IsTrue(compactOutcomes.All(outcome => outcome == "True"));
                Assert.AreEqual(keys.Length, compact.Count);
            }
            else
            {
                Assert.AreEqual($"refused RetainedStates Parser {limit} {limit + 1} {Offset}", compactOutcomes[^1]);
                Assert.AreEqual(keys.Length - 1, compact.Count);
                Assert.IsFalse(compact.TryGet(keys[^1], out _), "a refused entry is not stored");
            }

            AssertSameContent(compact, historical, $"limit {limit}");
        }
    }

    /// <summary>With an exhausted budget a duplicate is still answered without a charge, and a new entry is refused before it is stored.</summary>
    [TestMethod]
    public void ExhaustedBudget_DuplicateIsFreeAndNewEntryIsRefused()
    {
        var compact = new ParserLookaheadCache();
        var historical = new HistoricalLookaheadCache();
        ParserResourceBudget compactBudget = Budget(2)!, historicalBudget = Budget(2)!;
        var first = new ParserLookaheadKey("expr", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var second = new ParserLookaheadKey("expr", 0, 1, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var third = new ParserLookaheadKey("expr", 0, 2, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var overflowThird = new ParserLookaheadKey("expr", 0, 2, 0, "custom-cursor", -1);
        var result = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "x");

        foreach (ParserLookaheadKey key in new[] { first, second, first, second, third, overflowThird, first, third })
        {
            Assert.AreEqual(Outcome(() => historical.TryAdd(key, result, historicalBudget)), Outcome(() => compact.TryAdd(key, result, compactBudget)));
            Assert.AreEqual(historicalBudget.RetainedStates, compactBudget.RetainedStates);
        }

        Assert.AreEqual(2L, compactBudget.RetainedStates);
        Assert.AreEqual(2, compact.Count);
        Assert.IsFalse(compact.TryGet(third, out _));
        Assert.IsFalse(compact.TryGet(overflowThird, out _));
        Assert.AreEqual("False", Outcome(() => compact.TryAdd(first, result, compactBudget)), "a duplicate costs nothing");
        StringAssert.StartsWith(Outcome(() => compact.TryAdd(third, result, compactBudget)), "refused RetainedStates Parser 2 3 ");
        AssertSameContent(compact, historical, "exhausted");
    }

    /// <summary>After <see cref="ParserLookaheadCache.Clear"/> the cache references no rule name, token string or expected-name collection it stored.</summary>
    [TestMethod]
    public void Clear_ReleasesEveryStoredReference()
    {
        var cache = new ParserLookaheadCache();
        WeakReference[] references = Fill(cache);
        cache.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.AreEqual(0, cache.Count);
        foreach (WeakReference reference in references)
        {
            Assert.IsFalse(reference.IsAlive, "a stored instance is still referenced after Clear");
        }

        GC.KeepAlive(cache);
    }

    /// <summary>Stores entries built from fresh instances and returns weak references to those instances.</summary>
    /// <param name="cache">The cache.</param>
    /// <returns>Weak references to the rule name, the token strings and the expected-name collections.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] Fill(ParserLookaheadCache cache)
    {
        string rule = new("fresh-rule".ToCharArray());
        string tokenRule = new("FRESH".ToCharArray());
        string tokenText = new("fresh-text".ToCharArray());
        var packed = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, tokenRule, tokenText, [tokenRule]);
        var overflow = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, tokenRule, tokenText, [tokenText]);
        Assert.IsTrue(cache.TryAdd(new ParserLookaheadKey(rule, 4, 1, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1), packed));
        Assert.IsTrue(cache.TryAdd(new ParserLookaheadKey(rule, 4, 1, 0, "custom-cursor", -1), overflow));
        Assert.AreEqual(1, cache.OverflowCount);
        return [new(rule), new(tokenRule), new(tokenText), new(packed.ExpectedTokenNames), new(overflow.ExpectedTokenNames)];
    }

    /// <summary>Asserts that both caches answer a lookup identically, with the same instances.</summary>
    /// <param name="compact">The compact cache.</param>
    /// <param name="historical">The reference model.</param>
    /// <param name="key">The key.</param>
    /// <param name="context">The failure context.</param>
    private static void AssertSameLookup(ParserLookaheadCache compact, HistoricalLookaheadCache historical, ParserLookaheadKey key, string context)
    {
        bool expectedFound = historical.TryGet(key, out ParserLookaheadProbeResult expected);
        bool actualFound = compact.TryGet(key, out ParserLookaheadProbeResult actual);
        Assert.AreEqual(expectedFound, actualFound, $"{context}: presence of {key}");
        AssertSameResult(expected, actual, $"{context}: {key}");
    }

    /// <summary>Asserts that two results are equal and expose the same string and collection instances.</summary>
    /// <param name="expected">The reference result.</param>
    /// <param name="actual">The compact result.</param>
    /// <param name="context">The failure context.</param>
    private static void AssertSameResult(ParserLookaheadProbeResult expected, ParserLookaheadProbeResult actual, string context)
    {
        Assert.AreEqual(expected, actual, context);
        Assert.AreEqual(expected.Kind, actual.Kind, context);
        Assert.AreSame(expected.TokenRuleName, actual.TokenRuleName, context);
        Assert.AreSame(expected.TokenText, actual.TokenText, context);
        Assert.AreSame(expected.ExpectedTokenNames, actual.ExpectedTokenNames, context);
    }

    /// <summary>Asserts that both caches hold exactly the same entries, through lookups and through enumeration.</summary>
    /// <param name="compact">The compact cache.</param>
    /// <param name="historical">The reference model.</param>
    /// <param name="context">The failure context.</param>
    private static void AssertSameContent(ParserLookaheadCache compact, HistoricalLookaheadCache historical, string context)
    {
        Assert.AreEqual(historical.Count, compact.Count, context);
        foreach (var (key, expected) in historical.Entries)
        {
            Assert.IsTrue(compact.TryGet(key, out ParserLookaheadProbeResult actual), $"{context}: {key} missing");
            AssertSameResult(expected, actual, $"{context}: {key}");
        }

        var enumerated = compact.EnumerateEntries().ToList();
        Assert.AreEqual(historical.Count, enumerated.Count, context);
        Assert.AreEqual(enumerated.Count, enumerated.Select(entry => entry.Key).Distinct().Count(), $"{context}: enumerated keys are distinct");
        foreach (var (key, actual) in enumerated)
        {
            Assert.IsTrue(historical.TryGet(key, out ParserLookaheadProbeResult expected), $"{context}: enumerated {key} unknown");
            AssertSameResult(expected, actual, $"{context}: enumerated {key}");
        }
    }

    /// <summary>Runs an insertion and describes its outcome, or its refusal with every observable field.</summary>
    /// <param name="operation">The insertion.</param>
    /// <returns><c>True</c>, <c>False</c> or <c>refused kind phase limit observed offset</c>.</returns>
    private static string Outcome(Func<bool> operation)
    {
        try
        {
            return operation().ToString();
        }
        catch (ParserResourceLimitException exception)
        {
            return $"refused {exception.Kind} {exception.Phase} {exception.Limit} {exception.Observed} {exception.Offset}";
        }
    }

    /// <summary>Creates a parser budget with a retained-state limit, or none.</summary>
    /// <param name="limit">The retained-state limit, or <see langword="null"/> for an unbudgeted cache.</param>
    /// <returns>The budget.</returns>
    private static ParserResourceBudget? Budget(long? limit)
        => limit is { } max ? new ParserResourceBudget(ParserResourceLimits.Default with { MaxRetainedStates = max }, ParserResourcePhase.Parser, static () => Offset) : null;

    /// <summary>Draws a key, mostly in the packed ranges with repeated origins, sometimes with unusual fields.</summary>
    /// <param name="random">The generator.</param>
    /// <returns>The key.</returns>
    private static ParserLookaheadKey RandomKey(Random random)
    {
        bool unusual = random.Next(10) == 0;
        return new ParserLookaheadKey(
            unusual && random.Next(4) == 0 ? null! : RuleNames[random.Next(RuleNames.Length)],
            unusual && random.Next(3) == 0 ? (random.Next(2) == 0 ? int.MinValue + random.Next(3) : int.MaxValue - random.Next(3)) : random.Next(60),
            unusual ? AlternativeIndexes[random.Next(AlternativeIndexes.Length)] : random.Next(20),
            unusual ? Precedences[random.Next(Precedences.Length)] : random.Next(4),
            unusual ? (random.Next(4) == 0 ? null! : CursorKinds[random.Next(CursorKinds.Length)]) : CursorKinds[random.Next(4)],
            unusual ? CursorIndexes[random.Next(CursorIndexes.Length)] : random.Next(3) - 1);
    }

    /// <summary>Returns a key that differs from <paramref name="key"/> by exactly one field (or by the instance of its rule name).</summary>
    /// <param name="random">The generator.</param>
    /// <param name="key">The key.</param>
    /// <returns>The near key.</returns>
    private static ParserLookaheadKey Near(Random random, ParserLookaheadKey key)
        => random.Next(7) switch
        {
            0 => key with { RuleName = RuleNames[random.Next(RuleNames.Length)] },
            1 => key with { OriginPosition = unchecked(key.OriginPosition + (random.Next(2) == 0 ? 1 : -1)) },
            2 => key with { AlternativeIndex = unchecked(key.AlternativeIndex + (random.Next(2) == 0 ? 1 : -1)) },
            3 => key with { MinimumPrecedence = unchecked(key.MinimumPrecedence + (random.Next(2) == 0 ? 1 : -1)) },
            4 => key with { CursorKind = CursorKinds[random.Next(CursorKinds.Length)] },
            5 => key with { CursorIndex = unchecked(key.CursorIndex + (random.Next(2) == 0 ? 1 : -1)) },
            _ => key with { RuleName = key.RuleName is null ? null! : new string(key.RuleName.ToCharArray()) },
        };

    /// <summary>
    /// Draws a result: usually the token snapshot of its origin (as the engine produces it), sometimes other instances
    /// of the same text, other texts or <see langword="null"/>; every kind; expected names absent, empty, single or with
    /// repeated values in a significant order.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="origin">The origin position of the key.</param>
    /// <param name="tokens">The token snapshot drawn for each origin so far.</param>
    /// <returns>The result.</returns>
    private static ParserLookaheadProbeResult RandomResult(Random random, int origin, Dictionary<int, (string? Rule, string? Text)> tokens)
    {
        if (!tokens.TryGetValue(origin, out var token))
        {
            token = random.Next(20) == 0 ? (null, null) : ("ID", "t" + origin.ToString(System.Globalization.CultureInfo.InvariantCulture));
            tokens[origin] = token;
        }

        if (random.Next(8) == 0)
        {
            token = random.Next(4) switch
            {
                0 => (token.Rule is null ? null : new string(token.Rule.ToCharArray()), token.Text),
                1 => (token.Rule, token.Text is null ? null : new string(token.Text.ToCharArray())),
                2 => (null, null),
                _ => ("OTHER", "other"),
            };
        }

        IReadOnlyList<string>? expected = random.Next(6) switch
        {
            0 => null,
            1 => [],
            2 => ["ID", "ID", "NUM"],
            3 => ["NUM", "ID"],
            _ => [token.Rule ?? "ID"],
        };
        return new ParserLookaheadProbeResult(Kinds[random.Next(Kinds.Length)], token.Rule, token.Text, expected);
    }

    /// <summary>The historical look-ahead cache (before the compact layout), kept verbatim as the reference model.</summary>
    private sealed class HistoricalLookaheadCache
    {
        /// <summary>The cached observations.</summary>
        private readonly Dictionary<ParserLookaheadKey, ParserLookaheadProbeResult> _results = [];

        /// <summary>Gets the number of cached observations.</summary>
        public int Count => _results.Count;

        /// <summary>Gets the cached observations.</summary>
        public IEnumerable<KeyValuePair<ParserLookaheadKey, ParserLookaheadProbeResult>> Entries => _results;

        /// <summary>Clears all cached observations.</summary>
        public void Clear() => ParserStateRetention.Clear(_results);

        /// <summary>Tries to get a cached observation.</summary>
        /// <param name="key">The key.</param>
        /// <param name="result">Receives the result.</param>
        /// <returns><see langword="true"/> when found.</returns>
        public bool TryGet(ParserLookaheadKey key, out ParserLookaheadProbeResult result) => _results.TryGetValue(key, out result);

        /// <summary>Adds an observation when no value exists for the same key, charging one retained state first.</summary>
        /// <param name="key">The key.</param>
        /// <param name="result">The result.</param>
        /// <param name="budget">The budget, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when stored.</returns>
        public bool TryAdd(ParserLookaheadKey key, ParserLookaheadProbeResult result, ParserResourceBudget? budget)
        {
            if (_results.ContainsKey(key))
            {
                return false;
            }

            budget?.ChargeRetainedState();
            _results.Add(key, result);
            return true;
        }
    }
}
