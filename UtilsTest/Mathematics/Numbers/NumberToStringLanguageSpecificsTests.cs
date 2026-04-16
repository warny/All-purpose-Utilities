using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

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
}
