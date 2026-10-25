using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-56, the Danish and Norwegian compound tens 21–99 are written as one word:
/// <list type="bullet">
/// <item><description>
/// Danish: Dansk Sprognævn, "Et eller flere ord": "Talord under 100 skrives i ét ord: halvtreds, treoghalvtreds, …,
/// femogtyve"; "Talord over 100 skrives i flere ord: to hundred(e), fire hundred(e) (og) niogtyve". Den Danske Ordbog,
/// <c>enogtyve</c>. The neuter <c>et</c> only replaces a final word <c>en</c> (<c>hundrede og et</c>), never the compound.
/// </description></item>
/// <item><description>
/// Norwegian Bokmål: Bokmålsordboka (Språkrådet and the University of Bergen), <c>tjueen</c> and <c>tjueto</c>, determiner,
/// "grunntallet 21", one invariable form (no <c>tjueei</c>; <c>tjueett</c> is only the card game).
/// </description></item>
/// </list>
/// The ordinal plugins already fused the compounds (<c>enogtyvende</c>, <c>tjueførste</c>); only the declarative
/// cardinals are corrected. Sources: <c>docs/NTS-08-linguistic-sources.md</c>, NTS-56 section.
/// </summary>
[TestClass]
public class NumberToStringNts56Tests
{
    /// <summary>The Danish compound tens are one word, alone, after <c>hundrede og</c> and before a scale.</summary>
    [TestMethod]
    [DataRow("DA", 21L, "enogtyve")]
    [DataRow("DA", 22L, "toogtyve")]
    [DataRow("DA", 29L, "niogtyve")]
    [DataRow("DA", 35L, "femogtredive")]
    [DataRow("DA", 47L, "syvogfyrre")]
    [DataRow("DA", 53L, "treoghalvtreds")]
    [DataRow("DA", 66L, "seksogtres")]
    [DataRow("DA", 78L, "otteoghalvfjerds")]
    [DataRow("DA", 84L, "fireogfirs")]
    [DataRow("DA", 99L, "nioghalvfems")]
    [DataRow("DA", 20L, "tyve")]
    [DataRow("DA", 50L, "halvtreds")]
    [DataRow("DA", 129L, "hundrede og niogtyve")]
    [DataRow("DA", 429L, "fire hundrede og niogtyve")]
    [DataRow("DA", 21_000L, "enogtyve tusind")]
    [DataRow("DA", 21_000_000L, "enogtyve millioner")]
    [DataRow("DA-DK", 25L, "femogtyve")]
    public void Danish_CompoundTens_OneWord(string language, long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(language).Convert(number));

    /// <summary>The Norwegian compound tens are one word, alone, after hundreds and before a scale.</summary>
    [TestMethod]
    [DataRow("NO", 21L, "tjueen")]
    [DataRow("NO", 22L, "tjueto")]
    [DataRow("NO", 29L, "tjueni")]
    [DataRow("NO", 35L, "trettifem")]
    [DataRow("NO", 47L, "førtisju")]
    [DataRow("NO", 53L, "femtitre")]
    [DataRow("NO", 66L, "sekstiseks")]
    [DataRow("NO", 78L, "syttiåtte")]
    [DataRow("NO", 84L, "åttifire")]
    [DataRow("NO", 99L, "nittini")]
    [DataRow("NO", 20L, "tjue")]
    [DataRow("NO", 121L, "ett hundre tjueen")]
    [DataRow("NO", 21_000L, "tjueen tusen")]
    [DataRow("NO", 21_000_000L, "tjueen millioner")]
    [DataRow("NB", 21L, "tjueen")]
    [DataRow("NB-NO", 22L, "tjueto")]
    public void Norwegian_CompoundTens_OneWord(string language, long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(language).Convert(number));

    /// <summary>The compound is invariable; the gender variants still change a final standalone one.</summary>
    [TestMethod]
    [DataRow("DA", "gender=intetkøn", 21L, "enogtyve")]
    [DataRow("DA", "gender=intetkøn", 101L, "hundrede og et")]
    [DataRow("NO", "gender=hunkjønn", 21L, "tjueen")]
    [DataRow("NO", "gender=intetkjønn", 21L, "tjueen")]
    [DataRow("NO", "gender=hunkjønn", 1L, "ei")]
    [DataRow("NO", "gender=intetkjønn", 1L, "ett")]
    public void GenderVariants_LeaveTheCompound(string language, string variant, long number, string expected)
        => Assert.AreEqual(expected, NumberToStringConverter.GetConverter(language).Convert(number, variant));

    /// <summary>The cardinals now match the ordinals, which were already fused.</summary>
    [TestMethod]
    public void Ordinals_Unchanged()
    {
        Assert.AreEqual("enogtyvende", NumberToStringConverter.GetConverter("DA").ConvertOrdinal(21));
        Assert.AreEqual("tjueførste", NumberToStringConverter.GetConverter("NO").ConvertOrdinal(21));
    }

    /// <summary>The digit-by-digit decimal reading (category A of NTS-34) reads the corrected integer part.</summary>
    [TestMethod]
    public void Decimals_ReadTheCorrectedIntegerPart()
    {
        Assert.AreEqual("enogtyve komma nul fem", NumberToStringConverter.GetConverter("DA").Convert(21.05m));
        Assert.AreEqual("tjueen komma null fem", NumberToStringConverter.GetConverter("NO").Convert(21.05m));
    }
}
