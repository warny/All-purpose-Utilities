using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Validates language-specific finalization implementations used by number-to-string conversion.
/// </summary>
[TestClass]
public class NumberToStringLanguageSpecificsTests
{
    /// <summary>
    /// Ensures that German finalization rewrites feminine scale names and terminal "ein".
    /// </summary>
    [TestMethod]
    public void GermanSpecifics_FinalizeWriting_AppliesExpectedRules()
    {
        INumberToStringLanguageSpecifics specifics = new GermanNumberToStringLanguageSpecifics();

        Assert.AreEqual("eine Million", specifics.FinalizeWriting("de-DE", "ein Million"));
        Assert.AreEqual("eins", specifics.FinalizeWriting("de-DE", "ein"));
    }

    /// <summary>
    /// Ensures that the default implementation leaves text untouched.
    /// </summary>
    [TestMethod]
    public void DefaultSpecifics_FinalizeWriting_DoesNotChangeText()
    {
        INumberToStringLanguageSpecifics specifics = new DefaultNumberToStringLanguageSpecifics();
        const string value = "unchanged text";

        Assert.AreEqual(value, specifics.FinalizeWriting("EN", value));
    }

    /// <summary>
    /// Ensures that PolishOrdinalLanguageSpecifics is found by reflection when referenced in XML.
    /// </summary>
    [TestMethod]
    public void PolishOrdinalSpecifics_ResolvedViaReflection()
    {
        // PL config contains <LanguageSpecifics>PolishOrdinalLanguageSpecifics</LanguageSpecifics>
        // The engine resolves this via reflection on the Utils.NumberToString assembly.
        var c = NumberToStringConverter.GetConverter("PL");

        // 21 requires the plugin (not XML-only); if reflection fails the plugin is silent and returns wrong form
        Assert.AreEqual("dwudziesty pierwszy", c.ConvertOrdinal(21), "plugin resolved via reflection");
        Assert.AreEqual("dwudziesta pierwsza", c.ConvertOrdinal(21, "rodzaj=feminin"), "plugin + feminine");
    }
}
