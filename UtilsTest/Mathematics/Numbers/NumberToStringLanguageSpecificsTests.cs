using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Validates generic language-finalization infrastructure.
/// </summary>
[TestClass]
public class NumberToStringLanguageSpecificsTests
{
    /// <summary>
    /// Ensures that the default implementation leaves text untouched.
    /// </summary>
    [TestMethod]
    public void DefaultSpecifics_FinalizeWriting_DoesNotChangeText()
    {
        INumberToStringLanguageSpecifics specifics = new DefaultNumberToStringLanguageSpecifics();
        const string value = "unchanged text";

        Assert.AreEqual(value, specifics.FinalizeWriting("TEST", value));
    }
}
