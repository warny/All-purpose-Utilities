using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Fonts;
using Utils.Fonts.TTF;

namespace UtilsTest.Fonts;

[TestClass]
public class TtfEncoderFactoryTests
{
    [ClassInitialize]
    public static void RegisterEncodings(TestContext _)
    {
        // Required on non-Windows platforms to make extended code pages (e.g. 1252) available
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // ── Unicode platform ────────────────────────────────────────────────────

    [TestMethod]
    public void Unicode_AnyLanguage_ReturnsBigEndianUnicode()
    {
        var encoding = TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Unicode,
            TtfPlatformSpecificID.UNICODE_DEFAULT,
            TtfLanguageID.MAC_English);

        Assert.AreEqual(Encoding.BigEndianUnicode, encoding);
    }

    // ── Macintosh platform — valid code page ────────────────────────────────

    [TestMethod]
    public void Macintosh_ValidCodePage_ReturnsEncoding()
    {
        // 1252 is Windows-1252, a widely available code page on .NET
        var encoding = TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Macintosh,
            TtfPlatformSpecificID.MAC_ROMAN,
            (TtfLanguageID)1252);

        Assert.IsNotNull(encoding);
        Assert.AreNotEqual(Encoding.ASCII, encoding);
    }

    // ── Macintosh platform — invalid code page → ASCII fallback ────────────

    [TestMethod]
    public void Macintosh_InvalidCodePage_FallsBackToAscii()
    {
        // 9999 is not a valid .NET code page; the bare catch should return ASCII
        var encoding = TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Macintosh,
            TtfPlatformSpecificID.MAC_ROMAN,
            (TtfLanguageID)9999);

        Assert.AreEqual(Encoding.ASCII, encoding);
    }

    // ── Microsoft platform — invalid code page → ASCII fallback ────────────

    [TestMethod]
    public void Microsoft_InvalidCodePage_FallsBackToAscii()
    {
        // MS_English_United_States = 1033 is a Windows LCID, not a valid code page
        var encoding = TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Microsoft,
            TtfPlatformSpecificID.UNICODE_DEFAULT,
            TtfLanguageID.MS_English_United_States);

        Assert.AreEqual(Encoding.ASCII, encoding);
    }

    // ── Encoding.GetEncoding(0) special case ───────────────────────────────

    [TestMethod]
    public void Macintosh_CodePage0_ReturnsNonNullEncoding()
    {
        // MAC_English = 0 → Encoding.GetEncoding(0) returns the system default ANSI code page
        var encoding = TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Macintosh,
            TtfPlatformSpecificID.MAC_ROMAN,
            TtfLanguageID.MAC_English);

        Assert.IsNotNull(encoding);
    }
}
