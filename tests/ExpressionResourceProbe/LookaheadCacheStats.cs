using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Utils.Parser.Runtime;

namespace ExpressionResourceProbe;

/// <summary>
/// Look-ahead cache characterization for the <c>parser-registry</c> probe (<c>--lookahead</c>): reads every entry of the
/// engine's <c>ParserLookaheadCache</c> at the root exit, prints the distributions that decide which compact layout can
/// pay off (kinds, rule and cursor-kind cardinalities, alternative and cursor indexes, group sizes, null fields,
/// expected-token-name collections, string instance reuse) and an order-independent SHA-256 digest of the exact content,
/// so two builds can be compared differentially.
/// </summary>
/// <remarks>
/// Entries are read by reflection, from the historical layout (<c>Dictionary&lt;ParserLookaheadKey,
/// ParserLookaheadProbeResult&gt; _results</c>) or from any layout exposing an internal <c>EnumerateEntries()</c> method
/// that yields <c>KeyValuePair&lt;ParserLookaheadKey, ParserLookaheadProbeResult&gt;</c>.
/// </remarks>
internal static class LookaheadCacheStats
{
    /// <summary>One cache entry, decoded.</summary>
    /// <param name="RuleName">Rule name of the key.</param>
    /// <param name="Origin">Origin position of the key.</param>
    /// <param name="Alternative">Alternative index of the key.</param>
    /// <param name="Precedence">Minimum precedence of the key.</param>
    /// <param name="CursorKind">Cursor kind of the key.</param>
    /// <param name="CursorIndex">Cursor index of the key.</param>
    /// <param name="Kind">Probe kind of the value.</param>
    /// <param name="TokenRuleName">Token rule name of the value.</param>
    /// <param name="TokenText">Token text of the value.</param>
    /// <param name="Expected">Expected token names of the value.</param>
    private sealed record Entry(string RuleName, int Origin, int Alternative, int Precedence, string CursorKind, int CursorIndex, string Kind, string? TokenRuleName, string? TokenText, IReadOnlyList<string>? Expected);

    /// <summary>Describes the look-ahead cache of an engine at the root exit.</summary>
    /// <param name="engine">The engine, during the parse.</param>
    /// <returns>The <c>lookaheadDigest=... lstats=...</c> text.</returns>
    public static string Describe(ParserEngine engine)
    {
        object cache = Field(engine, "_lookaheadCache")!;
        List<Entry> entries = ReadEntries(cache, out string layout);
        var builder = new StringBuilder();
        builder.Append("lookaheadDigest=").Append(Digest(entries)).Append(" lstats=");

        var parts = new List<string>
        {
            "layout:" + layout,
            "entries:" + entries.Count.ToString(CultureInfo.InvariantCulture),
            "kinds:" + Histogram(entries.Select(e => e.Kind)),
            "rules:" + entries.Select(e => e.RuleName).Distinct(StringComparer.Ordinal).Count().ToString(CultureInfo.InvariantCulture),
            "ruleInstances:" + entries.Select(e => (object)e.RuleName).Distinct(ReferenceEqualityComparer.Instance).Count().ToString(CultureInfo.InvariantCulture),
            "cursorKinds:" + Histogram(entries.Select(e => e.CursorKind)),
            "cursorKindInstances:" + entries.Select(e => (object)e.CursorKind).Distinct(ReferenceEqualityComparer.Instance).Count().ToString(CultureInfo.InvariantCulture),
            "altMax:" + (entries.Count == 0 ? -1 : entries.Max(e => e.Alternative)).ToString(CultureInfo.InvariantCulture),
            "altHist:" + Histogram(entries.Select(e => Bucket(e.Alternative))),
            "cursorIndexHist:" + Histogram(entries.Select(e => Bucket(e.CursorIndex))),
            "precHist:" + Histogram(entries.Select(e => Bucket(e.Precedence))),
        };

        // Candidate groupings: their count and the number of entries per group.
        var byGroup5 = entries.GroupBy(e => (e.RuleName, e.Origin, e.Precedence, e.CursorKind, e.CursorIndex)).ToList();
        var byGroup3 = entries.GroupBy(e => (e.RuleName, e.Origin, e.Precedence)).ToList();
        var byOrigin = entries.GroupBy(e => e.Origin).ToList();
        parts.Add("groups5:" + byGroup5.Count.ToString(CultureInfo.InvariantCulture) + "/sizes=" + Histogram(byGroup5.Select(g => Bucket(g.Count()))));
        parts.Add("groups3:" + byGroup3.Count.ToString(CultureInfo.InvariantCulture) + "/sizes=" + Histogram(byGroup3.Select(g => Bucket(g.Count()))));
        parts.Add("origins:" + byOrigin.Count.ToString(CultureInfo.InvariantCulture));

        // Token snapshot: is it constant per origin position (same instances), so it could be stored once per group?
        int originsWithOneTokenSnapshot = byOrigin.Count(g => g.Select(e => (e.TokenRuleName, e.TokenText)).Distinct().Count() == 1);
        int group5WithSameTokenInstances = byGroup5.Count(g => g.All(e => ReferenceEquals(e.TokenRuleName, g.First().TokenRuleName) && ReferenceEquals(e.TokenText, g.First().TokenText)));
        parts.Add("originsOneToken:" + originsWithOneTokenSnapshot.ToString(CultureInfo.InvariantCulture));
        parts.Add("groups5SameTokenRefs:" + group5WithSameTokenInstances.ToString(CultureInfo.InvariantCulture));
        parts.Add("nullTokenRule:" + entries.Count(e => e.TokenRuleName is null).ToString(CultureInfo.InvariantCulture));
        parts.Add("nullTokenText:" + entries.Count(e => e.TokenText is null).ToString(CultureInfo.InvariantCulture));
        parts.Add("tokenTextInstances:" + entries.Where(e => e.TokenText is not null).Select(e => (object)e.TokenText!).Distinct(ReferenceEqualityComparer.Instance).Count().ToString(CultureInfo.InvariantCulture));

        // Expected-token names: absent, empty, sizes, and how many distinct collection instances back them.
        parts.Add("expectedNull:" + entries.Count(e => e.Expected is null).ToString(CultureInfo.InvariantCulture));
        parts.Add("expectedSizes:" + Histogram(entries.Where(e => e.Expected is not null).Select(e => Bucket(e.Expected!.Count))));
        parts.Add("expectedInstances:" + entries.Where(e => e.Expected is not null).Select(e => (object)e.Expected!).Distinct(ReferenceEqualityComparer.Instance).Count().ToString(CultureInfo.InvariantCulture));
        parts.Add("expectedDistinctValues:" + entries.Where(e => e.Expected is not null).Select(e => string.Join("\u0001", e.Expected!)).Distinct(StringComparer.Ordinal).Count().ToString(CultureInfo.InvariantCulture));
        parts.Add("expectedPerRuleAlt:" + entries.Where(e => e.Expected is not null).GroupBy(e => (e.RuleName, e.Alternative)).Count(g => g.Select(e => string.Join("\u0001", e.Expected!)).Distinct(StringComparer.Ordinal).Count() == 1).ToString(CultureInfo.InvariantCulture)
            + "/" + entries.Where(e => e.Expected is not null).Select(e => (e.RuleName, e.Alternative)).Distinct().Count().ToString(CultureInfo.InvariantCulture));
        builder.Append(string.Join(',', parts));
        return builder.ToString();
    }

    /// <summary>Reads every entry of a cache.</summary>
    /// <param name="cache">The cache.</param>
    /// <param name="layout">Receives a short description of the layout and its capacity.</param>
    /// <returns>The entries.</returns>
    private static List<Entry> ReadEntries(object cache, out string layout)
    {
        IEnumerable pairs;
        if (Field(cache, "_results") is IDictionary dictionary)
        {
            int capacity = (Field(dictionary, "_buckets") as Array)?.Length ?? -1;
            layout = "dictionary/capacity=" + capacity.ToString(CultureInfo.InvariantCulture);
            pairs = dictionary;
        }
        else
        {
            MethodInfo method = cache.GetType().GetMethod("EnumerateEntries", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMethodException(cache.GetType().Name, "EnumerateEntries");
            layout = "compact";
            pairs = (IEnumerable)method.Invoke(cache, null)!;
        }

        var entries = new List<Entry>();
        foreach (object pair in pairs)
        {
            object key, value;
            if (pair is DictionaryEntry entry)
            {
                key = entry.Key;
                value = entry.Value!;
            }
            else
            {
                key = pair.GetType().GetProperty("Key")!.GetValue(pair)!;
                value = pair.GetType().GetProperty("Value")!.GetValue(pair)!;
            }

            entries.Add(new Entry(
                (string)Property(key, "RuleName")!,
                (int)Property(key, "OriginPosition")!,
                (int)Property(key, "AlternativeIndex")!,
                (int)Property(key, "MinimumPrecedence")!,
                (string)Property(key, "CursorKind")!,
                (int)Property(key, "CursorIndex")!,
                Property(value, "Kind")!.ToString()!,
                (string?)Property(value, "TokenRuleName"),
                (string?)Property(value, "TokenText"),
                (IReadOnlyList<string>?)Property(value, "ExpectedTokenNames")));
        }

        return entries;
    }

    /// <summary>Computes an order-independent digest of the exact entries (null and empty collections are distinct).</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The first 16 hexadecimal characters of the SHA-256 digest.</returns>
    private static string Digest(IEnumerable<Entry> entries)
    {
        static string Text(string? value) => value is null ? "␀" : "\"" + value + "\"";
        var lines = entries
            .Select(e => string.Join('|',
                Text(e.RuleName), e.Origin.ToString(CultureInfo.InvariantCulture), e.Alternative.ToString(CultureInfo.InvariantCulture),
                e.Precedence.ToString(CultureInfo.InvariantCulture), Text(e.CursorKind), e.CursorIndex.ToString(CultureInfo.InvariantCulture),
                e.Kind, Text(e.TokenRuleName), Text(e.TokenText),
                e.Expected is null ? "␀" : "[" + string.Join(",", e.Expected.Select(Text)) + "]"))
            .OrderBy(line => line, StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))))[..16];
    }

    /// <summary>Formats a histogram as <c>value*count</c> items joined by <c>;</c>, most frequent first.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The histogram.</returns>
    private static string Histogram(IEnumerable<string> values)
        => string.Join(';', values.GroupBy(v => v, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(12).Select(g => g.Key + "*" + g.Count().ToString(CultureInfo.InvariantCulture)));

    /// <summary>Buckets an integer: exact below 8, then by powers of two.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The bucket label.</returns>
    private static string Bucket(int value)
    {
        if (value < 8) return value.ToString(CultureInfo.InvariantCulture);
        int low = 8;
        while (low * 2 <= value) low *= 2;
        return low.ToString(CultureInfo.InvariantCulture) + "+";
    }

    /// <summary>Reads a private instance field by name.</summary>
    /// <param name="target">The object.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null"/> when the field does not exist.</returns>
    private static object? Field(object target, string name)
        => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);

    /// <summary>Reads a public instance property by name.</summary>
    /// <param name="target">The object.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The value.</returns>
    private static object? Property(object target, string name)
        => target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!.GetValue(target);
}
