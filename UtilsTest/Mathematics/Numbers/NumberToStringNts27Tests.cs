using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Language-independent engine tests for the NTS-27 primitives: the group pattern (groups of different
/// widths, e.g. the Indian 3,2,2 … grouping), the inner digit forms (<c>innerString</c>/<c>innerBuildString</c>,
/// used when a higher digit of the number is non-zero) and the CLDR-based plural selectors of the scale nouns.
/// </summary>
[TestClass]
public class NumberToStringNts27Tests
{
    /// <summary>Unit words of the synthetic cardinal.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic cardinal.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Creates a unique culture name.</summary>
    private static string NewCulture() => $"NTS27-{Guid.NewGuid():N}";

    /// <summary>Builds the three digit levels of the synthetic cardinal.</summary>
    /// <param name="customize">Optional changes applied to the digits (level, digit).</param>
    /// <returns>The level 1..3 digit tables.</returns>
    private static Dictionary<int, DigitListType> Table(Action<int, DigitType>? customize = null)
    {
        var table = new Dictionary<int, DigitListType>
        {
            [1] = new DigitListType { Digits = [.. Units.Select((u, i) => new DigitType(i, u))] },
            [2] = new DigitListType
            {
                Digits = [.. Tens.Select((t, i) => i == 0 ? new DigitType(0, "", "*") : new DigitType(i, t, t + " *"))],
            },
            [3] = new DigitListType
            {
                Digits = [.. Units.Select((u, i) => i == 0 ? new DigitType(0, "", "*") : new DigitType(i, u + " hundred", u + " hundred *"))],
            },
        };
        if (customize != null)
            foreach (var (level, list) in table)
                foreach (var digit in list.Digits)
                    customize(level, digit);
        return table;
    }

    /// <summary>Creates options for the synthetic language with the given scale names.</summary>
    /// <param name="scaleNames">The static scale names (index 0 = units).</param>
    /// <param name="pattern">The group pattern, or <see langword="null"/> for uniform groups.</param>
    /// <param name="groups">The digit tables, or <see langword="null"/> for the plain synthetic tables.</param>
    /// <returns>The options.</returns>
    private static NumberToStringConverterOptions Options(string[] scaleNames, IReadOnlyList<int>? pattern = null, Dictionary<int, DigitListType>? groups = null)
        => new()
        {
            Group = 3,
            GroupPattern = pattern ?? [],
            Zero = "zero",
            Minus = "minus *",
            Groups = groups ?? Table(),
            Scale = new NumberScale(scaleNames, []),
            MaxNumber = null,
        };

    /// <summary>Indian scale names of the synthetic language.</summary>
    private static readonly string[] IndianScales = ["", "thousand", "lakh", "crore", "arab", "kharab"];

    /// <summary>Western scale names of the synthetic language.</summary>
    private static readonly string[] WesternScales = ["", "thousand", "million", "billion", "trillion"];

    /// <summary>Converts <paramref name="value"/> with <paramref name="converter"/>.</summary>
    private static string C(NumberToStringConverter converter, BigInteger value) => converter.Convert(value);

    /// <summary>
    /// Proof that uniform groups cannot express the Indian grouping: with groups of three digits the
    /// second group spans 10^3 to 10^5 and the third starts at 10^6, so a scale named at 10^5 (lakh) can never
    /// be reached and 100 000 is "one hundred thousand" whatever the scale names, which is why a group pattern
    /// is needed (NTS-27, Hindi एक लाख).
    /// </summary>
    [TestMethod]
    public void UniformGroups_CannotNameTheLakh()
    {
        var uniform = new NumberToStringConverter(Options(IndianScales));

        Assert.AreEqual("one hundred thousand", C(uniform, 100_000));
        // The value one lakh is never rendered with the word "lakh": the third group starts at a million.
        Assert.AreEqual("one lakh", C(uniform, 1_000_000));
        Assert.AreEqual("one lakh two hundred thirty four thousand five hundred sixty seven", C(uniform, 1_234_567));
    }

    /// <summary>The pattern 3,2 groups the units by three, then every group by two, the last width repeating.</summary>
    [TestMethod]
    public void GroupPattern_IndianGrouping()
    {
        var indian = new NumberToStringConverter(Options(IndianScales, [3, 2]));

        Assert.AreEqual("zero", C(indian, 0));
        Assert.AreEqual("nine hundred ninety nine", C(indian, 999));
        Assert.AreEqual("one thousand", C(indian, 1_000));
        Assert.AreEqual("ninety nine thousand nine hundred ninety nine", C(indian, 99_999));
        Assert.AreEqual("one lakh", C(indian, 100_000));
        Assert.AreEqual("one lakh one", C(indian, 100_001));
        Assert.AreEqual("ten lakh", C(indian, 1_000_000));
        Assert.AreEqual("ten two lakh thirty four thousand five hundred sixty seven", C(indian, 1_234_567));
        Assert.AreEqual("one crore", C(indian, 10_000_000));
        Assert.AreEqual("ninety nine kharab ninety nine arab ninety nine crore ninety nine lakh ninety nine thousand nine hundred ninety nine",
            C(indian, 9_999_999_999_999));
        Assert.AreEqual("minus one lakh", C(indian, -100_000));
        CollectionAssert.AreEqual(new[] { 3, 2 }, indian.GroupPattern.ToArray());
    }

    /// <summary>The last width repeats for every higher group, also beyond <see cref="long"/>.</summary>
    [TestMethod]
    public void GroupPattern_LastWidthRepeatsForBigIntegers()
    {
        string[] names = ["", .. Enumerable.Range(1, 40).Select(i => $"s{i}")];
        var converter = new NumberToStringConverter(Options(names, [3, 2]));

        // Group k (k >= 1) starts at 10^(3 + 2(k-1)).
        Assert.AreEqual("one s30", C(converter, BigInteger.Pow(10, 3 + 2 * 29)));
        Assert.AreEqual("ten s30 one", C(converter, BigInteger.Pow(10, 3 + 2 * 29 + 1) + 1));
        Assert.AreEqual("minus one s30", C(converter, -BigInteger.Pow(10, 3 + 2 * 29)));
    }

    /// <summary>A pattern of the table width alone, or an empty pattern, is the historical decomposition.</summary>
    [TestMethod]
    public void GroupPattern_UniformPatternIsHistoricalBehaviour()
    {
        var historical = new NumberToStringConverter(Options(WesternScales));
        var explicitUniform = new NumberToStringConverter(Options(WesternScales, [3]));

        Assert.AreEqual(0, historical.GroupPattern.Count);
        foreach (long value in new long[] { 0, 1, 21, 999, 1000, 1001, 21021, 999999, 1_000_000, 2_002_002, 999_999_999_999_999 })
            Assert.AreEqual(C(historical, value), C(explicitUniform, value), $"value {value}");
    }

    /// <summary>Widths that the digit tables cannot render are rejected at construction.</summary>
    [TestMethod]
    public void GroupPattern_InvalidWidthsAreRejected()
    {
        foreach (int[] pattern in new[] { new[] { 0 }, new[] { 3, 4 }, new[] { -1 }, new[] { 3, 0, 2 } })
        {
            var exception = Assert.Throws<ArgumentException>(() => new NumberToStringConverter(Options(IndianScales, pattern)), string.Join(",", pattern));
            StringAssert.Contains(exception.Message, "between 1 and the highest digit level (3)");
        }
    }

    /// <summary>The converter snapshots the pattern; the options clone copies it.</summary>
    [TestMethod]
    public void GroupPattern_IsSnapshotted()
    {
        var pattern = new List<int> { 3, 2 };
        var options = Options(IndianScales, pattern);
        var converter = new NumberToStringConverter(options);
        pattern[1] = 3;
        options.GroupPattern = [3];

        Assert.AreEqual("one lakh", C(converter, 100_000));
        CollectionAssert.AreEqual(new[] { 3, 2 }, converter.GroupPattern.ToArray());

        var clone = new NumberToStringConverterOptions(converter);
        CollectionAssert.AreEqual(new[] { 3, 2 }, clone.GroupPattern.ToArray());
        Assert.AreEqual("one lakh", C(new NumberToStringConverter(clone), 100_000));
    }

    /// <summary>An ordinal scale rule locates its scale at 10^(index × width): it is rejected with mixed widths.</summary>
    [TestMethod]
    public void GroupPattern_OrdinalScaleRulesRequireUniformGroups()
    {
        var options = Options(IndianScales, [3, 2]);
        options.OrdinalScaleRules = [new OrdinalScaleRule("1..", " ")];

        var exception = Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
        StringAssert.Contains(exception.Message, "single width");
    }

    /// <summary>Builds a configuration document of the synthetic language.</summary>
    /// <param name="culture">The culture.</param>
    /// <param name="languageAttributes">Extra Language attributes.</param>
    /// <param name="extraLanguages">Additional Language elements.</param>
    /// <param name="digitAttributes">Extra attributes of the zero hundreds digit.</param>
    /// <returns>The document.</returns>
    private static string Document(string culture, string languageAttributes = "", string extraLanguages = "", string digitAttributes = "") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
          <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="9999999999999" {languageAttributes}>
            <Culture>{culture}</Culture>
            <Groups>
              <Group level="1">{string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""))}</Group>
              <Group level="2">{string.Concat(Tens.Select((t, i) => i == 0 ? """<Digit digit="0" string="" buildString="*" />""" : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""))}</Group>
              <Group level="3">{string.Concat(Units.Select((u, i) => i == 0 ? $"""<Digit digit="0" string="" buildString="*" {digitAttributes}/>""" : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""))}</Group>
            </Groups>
            <NumberScale firstLetterUpperCase="false"><StaticNames>{string.Concat(IndianScales.Select((n, i) => $"""<Scale value="{i}" string="{n}" />"""))}</StaticNames></NumberScale>
          </Language>
          {extraLanguages}
        </Numbers>
        """;

    /// <summary>XML: groupPattern is read, inherited through baseOn and overridable by a child.</summary>
    [TestMethod]
    public void Xml_GroupPatternIsReadAndInherited()
    {
        string parent = NewCulture();
        string child = NewCulture();
        string uniformChild = NewCulture();
        var converters = NumberToStringConverter.ReadConfiguration(Document(parent, """groupPattern="3,2" """,
            $"""
            <Language baseOn="{parent}"><Culture>{child}</Culture></Language>
            <Language baseOn="{parent}" groupPattern="3" maxNumber="999999999999"><Culture>{uniformChild}</Culture></Language>
            """));

        Assert.AreEqual("one lakh", C(converters[parent], 100_000));
        Assert.AreEqual("one crore", C(converters[child], 10_000_000));
        CollectionAssert.AreEqual(new[] { 3, 2 }, converters[child].GroupPattern.ToArray());
        Assert.AreEqual("one hundred thousand", C(converters[uniformChild], 100_000));
    }

    /// <summary>XML: a malformed pattern is rejected (schema), a width above the digit levels by the converter.</summary>
    [TestMethod]
    public void Xml_InvalidGroupPatternIsRejected()
    {
        Assert.Throws<Exception>(() => NumberToStringConverter.ReadConfiguration(Document(NewCulture(), """groupPattern="3,x" """)));
        Assert.Throws<Exception>(() => NumberToStringConverter.ReadConfiguration(Document(NewCulture(), """groupPattern="0" """)));
        Assert.Throws<Exception>(() => NumberToStringConverter.ReadConfiguration(Document(NewCulture(), """groupPattern="" """)));
        var exception = Assert.Throws<ArgumentException>(() => NumberToStringConverter.ReadConfiguration(Document(NewCulture(), """groupPattern="3,4" """)));
        StringAssert.Contains(exception.Message, "highest digit level");
    }

    /// <summary>The internal parser reports a blank or non-numeric width.</summary>
    [TestMethod]
    public void ParseGroupPattern_ValidatesEveryWidth()
    {
        CollectionAssert.AreEqual(System.Array.Empty<int>(), NumberToStringConverter.ParseGroupPattern(null, "X"));
        CollectionAssert.AreEqual(new[] { 3, 2 }, NumberToStringConverter.ParseGroupPattern("3, 2", "X"));
        foreach (string invalid in new[] { "", "3,", "a", "-1", "3,0" })
            Assert.Throws<InvalidOperationException>(() => NumberToStringConverter.ParseGroupPattern(invalid, "X"), invalid);
        Assert.AreEqual(0, NumberToStringConverter.GroupIndexOfDigit([3, 2], 3, 2));
        Assert.AreEqual(1, NumberToStringConverter.GroupIndexOfDigit([3, 2], 3, 4));
        Assert.AreEqual(2, NumberToStringConverter.GroupIndexOfDigit([3, 2], 3, 5));
        Assert.AreEqual(5, NumberToStringConverter.GroupIndexOfDigit([3, 2], 3, 12));
        Assert.AreEqual(4, NumberToStringConverter.GroupIndexOfDigit([], 3, 12));
    }

    /// <summary>
    /// innerBuildString on the zero hundreds: used for a group that follows a higher non-zero group, never in
    /// the most significant group (the Vietnamese "không trăm" pattern).
    /// </summary>
    [TestMethod]
    public void InnerBuildString_AppliesOnlyAfterAHigherNonZeroDigit()
    {
        var converter = new NumberToStringConverter(Options(WesternScales, groups: Table((level, digit) =>
        {
            if (level == 3 && digit.Digit == 0) digit.InnerBuildString = "zero hundred *";
        })));

        Assert.AreEqual("five", C(converter, 5));
        Assert.AreEqual("twenty five", C(converter, 25));
        Assert.AreEqual("two thousand zero hundred five", C(converter, 2005));
        Assert.AreEqual("two thousand two hundred five", C(converter, 2205));
        Assert.AreEqual("two thousand", C(converter, 2000));
        Assert.AreEqual("one million zero hundred five", C(converter, 1_000_005));
        Assert.AreEqual("five thousand zero hundred five", C(converter, 5005));
        Assert.AreEqual("minus two thousand zero hundred five", C(converter, -2005));
        // The public single-group API renders a group as the most significant one.
        Assert.AreEqual("five", converter.ConvertGroup(3, 5));
    }

    /// <summary>
    /// innerBuildString also applies inside one group, after a non-zero higher digit of the same group
    /// (the Vietnamese "linh", the Chinese 零), and innerString replaces the bare text of a digit.
    /// </summary>
    [TestMethod]
    public void InnerForms_ApplyInsideAGroup()
    {
        var converter = new NumberToStringConverter(Options(WesternScales, groups: Table((level, digit) =>
        {
            if (level == 2 && digit.Digit == 0) digit.InnerBuildString = "and *";
            if (level == 3 && digit.Digit == 1) digit.InnerString = "a hundred";
        })));

        Assert.AreEqual("one hundred and five", C(converter, 105));
        Assert.AreEqual("five", C(converter, 5));
        Assert.AreEqual("one hundred", C(converter, 100));
        Assert.AreEqual("one thousand a hundred", C(converter, 1100));
        Assert.AreEqual("one thousand and five", C(converter, 1005));
        Assert.AreEqual("one hundred ten", C(converter, 110));
    }

    /// <summary>Without inner forms every conversion is the historical one.</summary>
    [TestMethod]
    public void InnerForms_AbsentIsHistoricalBehaviour()
    {
        var converter = new NumberToStringConverter(Options(WesternScales));

        Assert.AreEqual("two thousand five", C(converter, 2005));
        Assert.AreEqual("one hundred five", C(converter, 105));
    }

    /// <summary>The inner forms are copied into the exposed digits and the options clone.</summary>
    [TestMethod]
    public void InnerForms_AreCopied()
    {
        var groups = Table((level, digit) =>
        {
            if (level == 3 && digit.Digit == 0) { digit.InnerBuildString = "zero hundred *"; digit.InnerString = "unused"; }
        });
        var converter = new NumberToStringConverter(Options(WesternScales, groups: groups));
        groups[3].Digits[0].InnerBuildString = "changed *";

        Assert.AreEqual("two thousand zero hundred five", C(converter, 2005));
        var clone = new NumberToStringConverterOptions(converter);
        Assert.AreEqual("two thousand zero hundred five", C(new NumberToStringConverter(clone), 2005));

        DigitType exposed = converter.Groups[3][0];
        Assert.AreEqual("zero hundred *", exposed.InnerBuildString);
        Assert.AreEqual("unused", exposed.InnerString);
        exposed.InnerBuildString = "mutated *";
        Assert.AreEqual("two thousand zero hundred five", C(converter, 2005));
    }

    /// <summary>XML: innerString and innerBuildString are schema-valid and honoured.</summary>
    [TestMethod]
    public void Xml_InnerFormsAreRead()
    {
        string culture = NewCulture();
        var converter = NumberToStringConverter.ReadConfiguration(
            Document(culture, digitAttributes: """innerBuildString="zero hundred *" innerString="" """))[culture];

        Assert.AreEqual("two thousand zero hundred five", C(converter, 2005));
        Assert.AreEqual("five", C(converter, 5));
    }

    /// <summary>CLDR ru categories: one (…1 but not …11), few (…2-4 but not …12-14), many otherwise.</summary>
    [TestMethod]
    public void RussianPluralSelector_FollowsCldr()
    {
        var selector = new RussianPluralLexicalFormSelector();
        var expected = new Dictionary<long, string>
        {
            [0] = "many", [1] = "one", [2] = "few", [4] = "few", [5] = "many", [11] = "many", [12] = "many",
            [14] = "many", [20] = "many", [21] = "one", [22] = "few", [25] = "many", [101] = "one", [111] = "many",
            [112] = "many", [121] = "one", [122] = "few", [999] = "many", [-21] = "one",
        };
        foreach (var (value, category) in expected)
            Assert.AreEqual(category, selector.SelectForm(new LexicalFormContext(value, new Dictionary<string, string>())), $"value {value}");
    }

    /// <summary>CLDR pl categories: one (exactly 1), few (…2-4 but not …12-14), many otherwise (21 is many).</summary>
    [TestMethod]
    public void PolishPluralSelector_FollowsCldr()
    {
        var selector = new PolishPluralLexicalFormSelector();
        var expected = new Dictionary<long, string>
        {
            [0] = "many", [1] = "one", [2] = "few", [4] = "few", [5] = "many", [11] = "many", [12] = "many",
            [14] = "many", [21] = "many", [22] = "few", [25] = "many", [101] = "many", [102] = "few", [112] = "many",
            [122] = "few", [-1] = "one", [-22] = "few",
        };
        foreach (var (value, category) in expected)
            Assert.AreEqual(category, selector.SelectForm(new LexicalFormContext(value, new Dictionary<string, string>())), $"value {value}");
        Assert.AreEqual("many", selector.SelectForm(new LexicalFormContext(BigInteger.Pow(10, 30) + 1, new Dictionary<string, string>())));
    }
}
