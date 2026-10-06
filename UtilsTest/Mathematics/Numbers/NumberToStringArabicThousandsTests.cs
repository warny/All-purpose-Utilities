using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Tests for <see cref="ArabicScaleLexicalFormSelector"/> and a mechanical guard over the Arabic
/// thousands. The guard only checks structural invariants (no multiplier word before ألف/ألفان, the
/// noun form matching the multiplier category); the linguistic forms themselves are pinned by the
/// sourced examples of <c>Arabic.feature</c>.
/// </summary>
[TestClass]
public class NumberToStringArabicThousandsTests
{
    /// <summary>An empty variant query.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoVariants = ImmutableDictionary<string, string>.Empty;

    /// <summary>The selector follows the last number written (the multiplier's last two digits).</summary>
    /// <param name="multiplier">The scale multiplier.</param>
    /// <param name="expected">The expected form key.</param>
    [TestMethod]
    [DataRow(1, "singular")]
    [DataRow(2, "dual")]
    [DataRow(3, "plural")]
    [DataRow(10, "plural")]
    [DataRow(11, "singularAccusative")]
    [DataRow(12, "singularAccusative")]
    [DataRow(20, "singularAccusative")]
    [DataRow(99, "singularAccusative")]
    [DataRow(100, "singular")]
    [DataRow(101, "singular")]
    [DataRow(102, "dual")]
    [DataRow(103, "plural")]
    [DataRow(110, "plural")]
    [DataRow(111, "singularAccusative")]
    [DataRow(121, "singularAccusative")]
    [DataRow(200, "singular")]
    [DataRow(201, "singular")]
    [DataRow(202, "dual")]
    [DataRow(345, "singularAccusative")]
    [DataRow(999, "singularAccusative")]
    public void Selector_FollowsTheLastNumberWritten(int multiplier, string expected)
        => Assert.AreEqual(expected, new ArabicScaleLexicalFormSelector().SelectForm(new LexicalFormContext(multiplier, NoVariants)));

    /// <summary>The scale noun's form does not depend on the gender of the counted noun.</summary>
    [TestMethod]
    public void Selector_IgnoresVariants()
    {
        var selector = new ArabicScaleLexicalFormSelector();
        var feminine = new Dictionary<string, string> { ["gender"] = "muʾannath" };

        Assert.AreEqual(selector.SelectForm(new LexicalFormContext(3, NoVariants)), selector.SelectForm(new LexicalFormContext(3, feminine)));
    }

    /// <summary>Returns the expected noun for a thousands multiplier, by category.</summary>
    /// <param name="multiplier">The multiplier (1-999).</param>
    /// <returns>The noun form.</returns>
    private static string ExpectedNoun(int multiplier) => (multiplier % 100) switch
    {
        0 or 1 => "ألف",
        2 => "ألفان",
        <= 10 => "آلاف",
        _ => "ألفًا",
    };

    /// <summary>
    /// Mechanical guard over every round thousand 1000-999000 in both genders: the thousands noun
    /// matches its multiplier category and no multiplier word "one"/"two" precedes ألف/ألفان.
    /// </summary>
    /// <param name="variant">The variant argument, or an empty string.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("gender=muʾannath")]
    public void RoundThousands_UseTheNounOfTheirMultiplierCategory(string variant)
    {
        var converter = NumberToStringConverter.GetConverter("AR");
        string[] variants = variant.Length == 0 ? [] : [variant];
        var failures = new List<string>();
        for (int multiplier = 1; multiplier <= 999; multiplier++)
        {
            string text = converter.Convert(new BigInteger(multiplier * 1000), variants);
            if (!text.EndsWith(ExpectedNoun(multiplier), StringComparison.Ordinal))
                failures.Add($"{multiplier * 1000}: '{text}'");
            foreach (string forbidden in new[] { "واحد ألف", "اثنان ألف", "واحدة ", "اثنتان " })
                if (text.Contains(forbidden, StringComparison.Ordinal))
                    failures.Add($"{multiplier * 1000}: '{text}' contains '{forbidden}'");
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The cardinal of the multiplier alone is unchanged by the thousands rules (the multiplier is rendered as before).</summary>
    [TestMethod]
    public void Multiplier_IsRenderedLikeTheStandaloneCardinal()
    {
        var converter = NumberToStringConverter.GetConverter("AR");
        var failures = new List<string>();
        for (int multiplier = 3; multiplier <= 999; multiplier++)
        {
            if (multiplier % 100 is 1 or 2) continue;
            string expectedPrefix = multiplier == 200 ? "مائتا" : converter.Convert(new BigInteger(multiplier));
            string text = converter.Convert(new BigInteger(multiplier * 1000));
            if (!text.StartsWith(expectedPrefix + " ", StringComparison.Ordinal))
                failures.Add($"{multiplier * 1000}: '{text}'");
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }
}
