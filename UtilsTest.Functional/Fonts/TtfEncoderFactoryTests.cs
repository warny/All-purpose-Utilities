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

    // Regression test (found in PR review): the Microsoft-platform fix above initially always
    // returned UTF-16BE, but per the OpenType spec, encoding IDs 2 (ShiftJIS), 3 (PRC), 4 (Big5), 5
    // (Wansung) and 6 (Johab) are legacy double-byte CJK code pages that predate Microsoft encoding
    // every 'name' record as UTF-16BE. TtfPlatformSpecificID has no named members for these (only
    // 0/1/3 are defined, and 3 is misleadingly named UNICODE_V2 -- a different, Unicode-versioning
    // meaning unrelated to the Microsoft PRC encoding ID that shares the same numeric value), so
    // this casts the raw values directly.
    [TestMethod]
    public void Microsoft_LegacyCjkEncodingIds_ReturnTheirOwnCodePage()
    {
        Assert.AreEqual(Encoding.GetEncoding(932), TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Microsoft, (TtfPlatformSpecificID)2, TtfLanguageID.MS_English_United_States)); // ShiftJIS
        Assert.AreEqual(Encoding.GetEncoding(936), TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Microsoft, (TtfPlatformSpecificID)3, TtfLanguageID.MS_English_United_States)); // PRC
        Assert.AreEqual(Encoding.GetEncoding(950), TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Microsoft, (TtfPlatformSpecificID)4, TtfLanguageID.MS_English_United_States)); // Big5
        Assert.AreEqual(Encoding.GetEncoding(949), TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Microsoft, (TtfPlatformSpecificID)5, TtfLanguageID.MS_English_United_States)); // Wansung
        Assert.AreEqual(Encoding.GetEncoding(1361), TtfEncoderFactory.GetEncoding(
            TtfPlatFormId.Microsoft, (TtfPlatformSpecificID)6, TtfLanguageID.MS_English_United_States)); // Johab
    }
}
