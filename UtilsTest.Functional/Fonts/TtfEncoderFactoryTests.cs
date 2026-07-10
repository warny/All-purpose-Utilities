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
        // Required on non-Windows platforms to make extended code pages (e.g. MacRoman) available
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

    // ── Macintosh platform: encoding depends on the script (PlatformSpecificID), not language ──

    // Regression test: the encoding used to be selected via Encoding.GetEncoding((int)languageID),
    // treating a Macintosh *language* code (e.g. 0 = English) as if it were a .NET code page number.
    // That number is never a valid code page, so this call used to fall back to ASCII for every
    // Macintosh record regardless of language. It must now consistently resolve to MacRoman for the
    // Roman script, independently of which language the string is written in.
    [TestMethod]
    public void Macintosh_Roman_AnyLanguage_ReturnsMacRoman()
    {
        var englishEncoding = TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Macintosh, TtfPlatformSpecificID.MAC_ROMAN, TtfLanguageID.MAC_English);
        var frenchEncoding = TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Macintosh, TtfPlatformSpecificID.MAC_ROMAN, TtfLanguageID.MAC_French);

        var macRoman = Encoding.GetEncoding(10000);
        Assert.AreEqual(macRoman, englishEncoding);
        Assert.AreEqual(macRoman, frenchEncoding);
    }

    // ── Microsoft platform: always UTF-16BE, regardless of language ────────

    // Regression test: MS_English_United_States = 1033 is a Windows LCID, not a valid code page.
    // The encoding used to be selected via Encoding.GetEncoding((int)languageID), which threw for
    // this (and virtually every realistic) LCID and fell back to ASCII -- silently corrupting any
    // non-ASCII 'name'/'post' string. Microsoft 'name' records are always UTF-16BE, independently
    // of language.
    [TestMethod]
    public void Microsoft_AnyLanguage_ReturnsBigEndianUnicode()
    {
        var englishEncoding = TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Microsoft, TtfPlatformSpecificID.UNICODE_DEFAULT, TtfLanguageID.MS_English_United_States);
        var otherLanguageEncoding = TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Microsoft, TtfPlatformSpecificID.UNICODE_V11, (TtfLanguageID)9999);

        Assert.AreEqual(Encoding.BigEndianUnicode, englishEncoding);
        Assert.AreEqual(Encoding.BigEndianUnicode, otherLanguageEncoding);
    }
}
