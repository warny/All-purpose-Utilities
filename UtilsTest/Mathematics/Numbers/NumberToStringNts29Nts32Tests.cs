using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Technical tests of NTS-29 (attested ordinals of zero: Danish <c>nulte</c>, Hungarian <c>nulladik</c>) and
/// NTS-32 (Polish ordinal of 400: <c>czterechsetny</c>). The linguistic forms are pinned by the language
/// <c>.feature</c> files; these tests check the plugin extension point and the extent of each change.
/// </summary>
[TestClass]
public class NumberToStringNts29Nts32Tests
{
    /// <summary>The Polish gender (rodzaj) values, in the plugin's index order.</summary>
    private static readonly string[] s_rodzaje = ["maskulin", "feminin", "nijaki", "plural_mos", "plural"];

    /// <summary>The Polish case (przypadek) values, in the plugin's index order.</summary>
    private static readonly string[] s_przypadki = ["mianownik", "dopełniacz", "celownik", "biernik", "narzędnik", "miejscownik"];

    /// <summary>Only the Danish plugin forms the ordinal of zero; Norwegian and Swedish defer it to the pipeline.</summary>
    [TestMethod]
    public void ScandinavianZeroHook_IsDanishOnly()
    {
        var variants = new Dictionary<string, string>();

        Assert.IsTrue(new DanishOrdinalLanguageSpecifics().TryConvertOrdinal(0, variants, out string? danish));
        Assert.AreEqual("nulte", danish);
        Assert.IsFalse(new NorwegianOrdinalLanguageSpecifics().TryConvertOrdinal(0, variants, out string? norwegian));
        Assert.IsNull(norwegian);
        Assert.IsFalse(new SwedishOrdinalLanguageSpecifics().TryConvertOrdinal(0, variants, out string? swedish));
        Assert.IsNull(swedish);
        Assert.IsFalse(new DanishOrdinalLanguageSpecifics().TryConvertOrdinal(-1, variants, out string? negative));
        Assert.IsNull(negative);
    }

    /// <summary>The Hungarian plugin forms the ordinal of zero and still declines negative values.</summary>
    [TestMethod]
    public void HungarianPlugin_FormsZeroOnly()
    {
        var variants = new Dictionary<string, string>();
        var plugin = new HungarianOrdinalLanguageSpecifics();

        Assert.IsTrue(plugin.TryConvertOrdinal(0, variants, out string? zero));
        Assert.AreEqual("nulladik", zero);
        Assert.IsFalse(plugin.TryConvertOrdinal(-1, variants, out string? negative));
        Assert.IsNull(negative);
    }

    /// <summary>The ordinals of zero never change the cardinal of zero.</summary>
    [TestMethod]
    [DataRow("DA", "nul", "nulte")]
    [DataRow("HU", "nulla", "nulladik")]
    public void ZeroOrdinals_LeaveTheCardinalUnchanged(string culture, string cardinal, string ordinal)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.AreEqual(cardinal, converter.Convert(0));
        Assert.AreEqual(ordinal, converter.ConvertOrdinal(0));
    }

    /// <summary>Norwegian, Swedish and Polish keep failing closed for zero.</summary>
    [TestMethod]
    [DataRow("NO")]
    [DataRow("SV")]
    [DataRow("PL")]
    public void OtherZeroOrdinals_StillFailClosed(string culture)
        => Assert.ThrowsExactly<NotSupportedException>(() => NumberToStringConverter.GetConverter(culture).ConvertOrdinal(0));

    /// <summary>
    /// Every form of the Polish 400th is built on czterechsetn-, the forms of 1400, 2400 and 5400 end with it,
    /// and no ordinal from 1 to 9 999 still contains czterosetn-.
    /// </summary>
    [TestMethod]
    public void Polish400_EveryFormUsesCzterechsetn()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("PL");
        foreach (string rodzaj in s_rodzaje)
            foreach (string przypadek in s_przypadki)
            {
                string[] variants = [$"rodzaj={rodzaj}", $"przypadek={przypadek}"];
                string form = converter.ConvertOrdinal(400, variants);
                StringAssert.StartsWith(form, "czterechsetn", $"{rodzaj} {przypadek}");
                Assert.AreEqual("tysiąc " + form, converter.ConvertOrdinal(1400, variants), $"1400 {rodzaj} {przypadek}");
                Assert.AreEqual("dwa tysiące " + form, converter.ConvertOrdinal(2400, variants), $"2400 {rodzaj} {przypadek}");
                Assert.AreEqual("pięć tysięcy " + form, converter.ConvertOrdinal(5400, variants), $"5400 {rodzaj} {przypadek}");
            }

        for (int value = 1; value < 10_000; value++)
            Assert.IsFalse(converter.ConvertOrdinal(value).Contains("czterosetn"), $"{value}");
    }

    /// <summary>
    /// The 400th declines exactly like the 300th (same adjectival endings after the stem), so only the stem
    /// changed.
    /// </summary>
    [TestMethod]
    public void Polish400_DeclinesLikeTheThreeHundredth()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("PL");
        foreach (string rodzaj in s_rodzaje)
            foreach (string przypadek in s_przypadki)
            {
                string[] variants = [$"rodzaj={rodzaj}", $"przypadek={przypadek}"];
                string threeHundredth = converter.ConvertOrdinal(300, variants);
                Assert.AreEqual("czterech" + threeHundredth["trzech".Length..], converter.ConvertOrdinal(400, variants), $"{rodzaj} {przypadek}");
            }
    }

    /// <summary>Only the round 400 changed: the cardinal czterysta and the round thousand czterotysięczny are kept.</summary>
    [TestMethod]
    [DataRow(401L, "czterysta pierwszy")]
    [DataRow(415L, "czterysta piętnasty")]
    [DataRow(499L, "czterysta dziewięćdziesiąty dziewiąty")]
    [DataRow(4000L, "czterotysięczny")]
    [DataRow(4400L, "cztery tysiące czterechsetny")]
    public void Polish400_OnlyTheRoundValueChanged(long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter("PL").ConvertOrdinal(number));

    /// <summary>The Polish cardinal of 400 is unchanged.</summary>
    [TestMethod]
    public void Polish400_CardinalIsUnchanged()
        => Assert.AreEqual("czterysta", NumberToStringConverter.GetConverter("PL").Convert(400));
}
