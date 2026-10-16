using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using Utils.NumberToString;
using Utils.Numerics;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-30D: the generic <c>finalComponentConnector</c> primitive
/// (<see cref="NumberToStringConverterOptions.FinalComponentConnector"/>), which joins the last additive component of
/// every numeral phrase to the rest, and its Bulgarian configuration <c>и</c>. The first half pins the
/// language-independent contract on a synthetic language whose English-looking output is deliberately not English
/// grammar; the second half pins the sourced Bulgarian forms (sources in <c>docs/NTS-08-linguistic-sources.md</c>,
/// NTS-30D section).
/// </summary>
[TestClass]
public class NumberToStringNts30DTests
{
    // ===== synthetic engine ==========================================================================

    /// <summary>Unit words of the synthetic language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic language.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Atomic 11–19 of the synthetic language.</summary>
    private static readonly string[] Teens = ["eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];

    /// <summary>Language specifics counting the finalizations of public results.</summary>
    private sealed class CountingSpecifics : INumberToStringLanguageSpecifics
    {
        /// <summary>The number of finalizations.</summary>
        public int Calls;

        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text)
        {
            Interlocked.Increment(ref Calls);
            return text;
        }
    }

    /// <summary>Creates a unique culture or registration name.</summary>
    /// <returns>The name.</returns>
    private static string NewName() => $"NTS30D-{Guid.NewGuid():N}";

    /// <summary>
    /// Creates a synthetic language: groups of three digits, atomic 11–19, the static scales "thousand",
    /// "million" and "billion" (domain below 10^12), and a gender dimension a/b turning one/two into oneB/twoB.
    /// </summary>
    /// <param name="culture">The culture identifier.</param>
    /// <param name="attributes">Extra attributes of the Language element (e.g. <c>finalComponentConnector="and"</c>).</param>
    /// <param name="extraLanguages">Additional Language elements.</param>
    /// <param name="extra">Additional elements appended to the Language after its Variants.</param>
    /// <param name="tensFusion">An optional Fusion element placed on the tens digit 2.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string attributes = "", string extraLanguages = "", string extra = "", string tensFusion = "")
    {
        string units = string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : i == 2 && tensFusion.Length > 0
                ? $"""<Digit digit="{i}" string="{t}" buildString="{t} *">{tensFusion}</Digit>"""
                : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        string hundreds = string.Concat(Units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""));
        string teens = string.Concat(Teens.Select((t, i) => $"""<Number value="{i + 11}" string="{t}" />"""));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point"
                        fractionSeparator="over" maxNumber="999999999999" {attributes}>
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{units}</Group>
                  <Group level="2">{tens}</Group>
                  <Group level="3">{hundreds}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false">
                  <StaticNames>
                    <Scale value="0" string="" /><Scale value="1" string="thousand" />
                    <Scale value="2" string="million" /><Scale value="3" string="billion" />
                  </StaticNames>
                  <Suffixes><Suffix>illion</Suffix></Suffixes>
                </NumberScale>
                <Exceptions>{teens}</Exceptions>
                <Variants>
                  <Dimension name="gender" values="a,b" />
                  <Variant type="gender" variant="b">
                    <Replacement oldValue="one" newValue="oneB" scope="LastWord" />
                    <Replacement oldValue="two" newValue="twoB" scope="LastWord" />
                  </Variant>
                </Variants>
                {extra}
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds a synthetic converter.</summary>
    /// <param name="attributes">Extra attributes of the Language element.</param>
    /// <param name="extra">Additional elements appended to the Language after its Variants.</param>
    /// <param name="tensFusion">An optional Fusion element placed on the tens digit 2.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string attributes = "", string extra = "", string tensFusion = "")
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(Document(culture, attributes, "", extra, tensFusion))[culture];
    }

    /// <summary>Builds the synthetic converter with the connector "and".</summary>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter BuildAnd() => Build("""finalComponentConnector="and" """);

    /// <summary>Counts the occurrences of the word <paramref name="word"/> in <paramref name="text"/>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The word.</param>
    /// <returns>The number of occurrences.</returns>
    private static int CountWord(string text, string word) => text.Split(' ').Count(w => w == word);

    /// <summary>Returns the number of additive components of a value below 1000 (11–19 are atomic).</summary>
    /// <param name="value">A value between 0 and 999.</param>
    /// <returns>The number of components.</returns>
    private static int Components(int value)
    {
        int hundreds = value / 100, rest = value % 100;
        int count = hundreds > 0 ? 1 : 0;
        if (rest is >= 11 and <= 19) return count + 1;
        return count + (rest / 10 > 0 ? 1 : 0) + (rest % 10 > 0 ? 1 : 0);
    }

    /// <summary>Without the attribute the converter has no connector and composes exactly as before.</summary>
    [TestMethod]
    public void Absent_NoConnector_HistoricalComposition()
    {
        var converter = Build();
        Assert.IsNull(converter.FinalComponentConnector);
        Assert.AreEqual("twenty one", converter.Convert(21));
        Assert.AreEqual("one hundred one", converter.Convert(101));
        Assert.AreEqual("one hundred twenty one", converter.Convert(121));
        Assert.AreEqual("one thousand one", converter.Convert(1001));
        Assert.AreEqual("one thousand one hundred twenty one", converter.Convert(1121));
    }

    /// <summary>Inside one group the connector precedes the last non-zero component, once.</summary>
    [TestMethod]
    [DataRow(1, "one")]
    [DataRow(10, "ten")]
    [DataRow(20, "twenty")]
    [DataRow(21, "twenty and one")]
    [DataRow(25, "twenty and five")]
    [DataRow(100, "one hundred")]
    [DataRow(101, "one hundred and one")]
    [DataRow(110, "one hundred and ten")]
    [DataRow(120, "one hundred and twenty")]
    [DataRow(121, "one hundred twenty and one")]
    [DataRow(125, "one hundred twenty and five")]
    [DataRow(200, "two hundred")]
    [DataRow(205, "two hundred and five")]
    [DataRow(220, "two hundred and twenty")]
    [DataRow(225, "two hundred twenty and five")]
    [DataRow(999, "nine hundred ninety and nine")]
    public void WithinAGroup_ConnectorBeforeTheLastComponent(int value, string expected)
        => Assert.AreEqual(expected, BuildAnd().Convert(value));

    /// <summary>An exception (11–19) is one atomic component: never split, and itself the last component.</summary>
    [TestMethod]
    [DataRow(11, "eleven")]
    [DataRow(15, "fifteen")]
    [DataRow(19, "nineteen")]
    [DataRow(111, "one hundred and eleven")]
    [DataRow(115, "one hundred and fifteen")]
    [DataRow(1015, "one thousand and fifteen")]
    [DataRow(15000, "fifteen thousand")]
    [DataRow(115000, "one hundred and fifteen thousand")]
    public void Exceptions_AreAtomicComponents(int value, string expected)
        => Assert.AreEqual(expected, BuildAnd().Convert(value));

    /// <summary>
    /// Across groups the connector precedes the last group only when that group is a single component; otherwise
    /// the connector already sits inside the last group.
    /// </summary>
    [TestMethod]
    [DataRow(1000, "one thousand")]
    [DataRow(1001, "one thousand and one")]
    [DataRow(1010, "one thousand and ten")]
    [DataRow(1100, "one thousand and one hundred")]
    [DataRow(1101, "one thousand one hundred and one")]
    [DataRow(1120, "one thousand one hundred and twenty")]
    [DataRow(1121, "one thousand one hundred twenty and one")]
    [DataRow(1000000, "one million")]
    [DataRow(1000001, "one million and one")]
    [DataRow(1001000, "one million and one thousand")]
    [DataRow(1001001, "one million one thousand and one")]
    [DataRow(1100000, "one million and one hundred thousand")]
    [DataRow(1000100, "one million and one hundred")]
    [DataRow(1000000001, "one billion and one")]
    public void AcrossGroups_ConnectorBeforeTheLastGroupWhenItIsOneComponent(int value, string expected)
        => Assert.AreEqual(expected, BuildAnd().Convert(value));

    /// <summary>
    /// A multiplier is a numeral phrase of its own: its connector stays inside it, before the scale noun, and the
    /// whole number still gets its own connector before a single-component last group.
    /// </summary>
    [TestMethod]
    [DataRow(21000, "twenty and one thousand")]
    [DataRow(21001, "twenty and one thousand and one")]
    [DataRow(21121, "twenty and one thousand one hundred twenty and one")]
    [DataRow(121000, "one hundred twenty and one thousand")]
    [DataRow(1121000, "one million one hundred twenty and one thousand")]
    [DataRow(21000000, "twenty and one million")]
    [DataRow(21000121, "twenty and one million one hundred twenty and one")]
    [DataRow(21001001, "twenty and one million one thousand and one")]
    public void Multipliers_KeepTheirOwnConnector(int value, string expected)
        => Assert.AreEqual(expected, BuildAnd().Convert(value));

    /// <summary>
    /// The composition metadata: below 1000 the connector occurs exactly once in every value of two or more
    /// components and never otherwise, and 121 never becomes "one hundred and twenty and one".
    /// </summary>
    [TestMethod]
    public void EveryValueBelowAThousand_HasOneConnectorPerCompoundPhrase()
    {
        var converter = BuildAnd();
        for (int value = 1; value < 1000; value++)
        {
            string text = converter.Convert(value);
            Assert.AreEqual(Components(value) >= 2 ? 1 : 0, CountWord(text, "and"), $"{value}: '{text}'");
            Assert.IsFalse(text.StartsWith("and") || text.EndsWith("and"), $"{value}: '{text}'");
        }
        Assert.AreNotEqual("one hundred and twenty and one", converter.Convert(121));
    }

    /// <summary>
    /// Across groups: one connector per multiplier phrase of two or more components, plus one for the whole number
    /// when its last non-zero group is a single component; 1121 has exactly one.
    /// </summary>
    [TestMethod]
    public void AcrossGroups_ConnectorCountFollowsThePhrases()
    {
        var converter = BuildAnd();
        Assert.AreEqual(1, CountWord(converter.Convert(1121), "and"));
        foreach (int high in new[] { 1, 2, 11, 20, 21, 100, 101, 115, 120, 121, 999 })
            foreach (int low in new[] { 0, 1, 11, 20, 21, 100, 101, 120, 121, 999 })
            {
                int value = high * 1000 + low;
                // The multiplier's own connector, then exactly one for the non-zero last group: inside it when it
                // has two components or more, before it otherwise.
                int expected = (Components(high) >= 2 ? 1 : 0) + (low == 0 ? 0 : 1);
                string text = converter.Convert(value);
                Assert.AreEqual(expected, CountWord(text, "and"), $"{value}: '{text}'");
            }
    }

    /// <summary>The connector belongs to the raw composition: caller variants still change only the last numeral.</summary>
    [TestMethod]
    public void Variants_ApplyAfterTheConnector()
    {
        var converter = BuildAnd();
        Assert.AreEqual("twenty and oneB", converter.Convert(21, "gender=b"));
        Assert.AreEqual("twenty and twoB", converter.Convert(22, "gender=b"));
        Assert.AreEqual("one thousand and oneB", converter.Convert(1001, "gender=b"));
        Assert.AreEqual("twenty and one", converter.Convert(21, "gender=a"));
    }

    /// <summary>The connector is a plain cardinal word: negatives, decimals and fractions reuse the corrected cardinals.</summary>
    [TestMethod]
    public void CompositeSurfaces_ReuseTheCardinal()
    {
        var converter = BuildAnd();
        Assert.AreEqual("minus twenty and one", converter.Convert(-21));
        Assert.AreEqual("twenty and one point five", converter.Convert(21.5m));
        Assert.AreEqual("twenty and one over one hundred and one", converter.ConvertFraction(21, 101));
        Assert.AreEqual("twenty and one over one hundred and one", converter.Convert(new Number(21, 101)));
    }

    /// <summary>The scale connector stays orthogonal: it joins multiplier and noun after the multiplier's own connector.</summary>
    [TestMethod]
    public void ScaleConnector_IsOrthogonal()
    {
        var converter = Build("""finalComponentConnector="and" scaleConnector="of" scaleConnectorThreshold="20" """);
        Assert.AreEqual("twenty and one of thousand", converter.Convert(21000));
        Assert.AreEqual("five thousand and one", converter.Convert(5001));
    }

    /// <summary>A public result is finalized exactly once; the connector is never injected by the finalizer.</summary>
    [TestMethod]
    public void FinalizeWriting_RunsOncePerPublicResult()
    {
        string name = NewName();
        var specifics = new CountingSpecifics();
        NumberToStringConverter.RegisterLanguageSpecifics(name, specifics);
        string culture = NewName();
        var converter = NumberToStringConverter.ReadConfiguration(
            Document(culture, """finalComponentConnector="and" """, extra: $"<LanguageSpecifics>{name}</LanguageSpecifics>"))[culture];
        int before = specifics.Calls;
        Assert.AreEqual("one thousand one hundred twenty and one", converter.Convert(1121));
        Assert.AreEqual(before + 1, specifics.Calls);
    }

    /// <summary><c>baseOn</c>: absent inherits, an explicit value replaces, an explicit empty value removes the connector.</summary>
    [TestMethod]
    public void Connector_IsInheritedThroughBaseOn()
    {
        string parent = NewName();
        string inheriting = NewName();
        string replacing = NewName();
        string removing = NewName();
        string document = Document(parent, """finalComponentConnector="and" """, $"""
            <Language baseOn="{parent}"><Culture>{inheriting}</Culture></Language>
            <Language baseOn="{parent}" finalComponentConnector="plus"><Culture>{replacing}</Culture></Language>
            <Language baseOn="{parent}" finalComponentConnector=""><Culture>{removing}</Culture></Language>
            """);
        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("and", converters[inheriting].FinalComponentConnector);
        Assert.AreEqual("twenty and one", converters[inheriting].Convert(21));
        Assert.AreEqual("plus", converters[replacing].FinalComponentConnector);
        Assert.AreEqual("one thousand plus one", converters[replacing].Convert(1001));
        Assert.IsNull(converters[removing].FinalComponentConnector);
        Assert.AreEqual("twenty one", converters[removing].Convert(21));
    }

    /// <summary>The programmatic options carry the connector, and cloning a converter through its options keeps it.</summary>
    [TestMethod]
    public void Connector_ProgrammaticOptionsAndClone()
    {
        var original = BuildAnd();
        var options = new NumberToStringConverterOptions(original);
        Assert.AreEqual("and", options.FinalComponentConnector);
        var clone = new NumberToStringConverter(options);
        Assert.AreEqual("and", clone.FinalComponentConnector);
        Assert.AreEqual("one hundred twenty and one", clone.Convert(121));

        var programmatic = new NumberToStringConverter(new NumberToStringConverterOptions(Build()) { FinalComponentConnector = "et" });
        Assert.AreEqual("twenty et one", programmatic.Convert(21));
        var blank = new NumberToStringConverter(new NumberToStringConverterOptions(original) { FinalComponentConnector = "  " });
        Assert.IsNull(blank.FinalComponentConnector);
        Assert.AreEqual("twenty one", blank.Convert(21));
    }

    /// <summary>
    /// The connectors that claim the same junctions cannot be combined: the group connector (between groups), the
    /// intra-group connector (after the hundreds) and a Fusion (which rewrites a junction) are rejected at load.
    /// </summary>
    [TestMethod]
    public void Connector_CombinedWithAJunctionRewritingPrimitive_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Build("""finalComponentConnector="and" groupConnector="and" """));
        Assert.ThrowsExactly<ArgumentException>(() => Build("""finalComponentConnector="and" intraGroupConnector="o" """));
        Assert.ThrowsExactly<ArgumentException>(() => Build("""finalComponentConnector="and" """,
            tensFusion: """<Fusion for="1..9" />"""));
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(
            new NumberToStringConverterOptions(Build("""groupConnector="and" """)) { FinalComponentConnector = "and" }));
    }

    // ===== Bulgarian =================================================================================

    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    /// <summary>The Bulgarian configuration declares the connector и, inherited by BG-BG.</summary>
    [TestMethod]
    public void Bulgarian_DeclaresTheConnector()
    {
        Assert.AreEqual("и", Get("BG").FinalComponentConnector);
        Assert.AreEqual("и", Get("BG-BG").FinalComponentConnector);
    }

    /// <summary>
    /// Bulgarian values below a thousand: и before the last component (RBE, и, sense 3: двайсет и пет, сто и пет,
    /// сто двайсет и пет; Официален правописен речник 2012 / БЕРОН: двайсет и пет, сто двайсет и пет).
    /// </summary>
    [TestMethod]
    [DataRow(1, "едно")]
    [DataRow(10, "десет")]
    [DataRow(11, "единадесет")]
    [DataRow(19, "деветнадесет")]
    [DataRow(20, "двадесет")]
    [DataRow(21, "двадесет и едно")]
    [DataRow(25, "двадесет и пет")]
    [DataRow(100, "сто")]
    [DataRow(101, "сто и едно")]
    [DataRow(105, "сто и пет")]
    [DataRow(110, "сто и десет")]
    [DataRow(111, "сто и единадесет")]
    [DataRow(115, "сто и петнадесет")]
    [DataRow(120, "сто и двадесет")]
    [DataRow(121, "сто двадесет и едно")]
    [DataRow(125, "сто двадесет и пет")]
    [DataRow(199, "сто деветдесет и девет")]
    [DataRow(200, "двеста")]
    [DataRow(201, "двеста и едно")]
    [DataRow(205, "двеста и пет")]
    [DataRow(210, "двеста и десет")]
    [DataRow(220, "двеста и двадесет")]
    [DataRow(221, "двеста двадесет и едно")]
    [DataRow(225, "двеста двадесет и пет")]
    [DataRow(324, "триста двадесет и четири")]
    [DataRow(999, "деветстотин деветдесет и девет")]
    public void Bulgarian_BelowAThousand(int value, string expected)
    {
        Assert.AreEqual(expected, Get("BG").Convert(value));
        Assert.AreEqual(expected, Get("BG-BG").Convert(value));
    }

    /// <summary>
    /// Bulgarian values across the thousands (RBE: хиляда и петстотин; БЕРОН: хиляда и триста; bg.wikipedia
    /// "Имена на числата": хиляда двадесет и четири).
    /// </summary>
    [TestMethod]
    [DataRow(1000, "хиляда")]
    [DataRow(1001, "хиляда и едно")]
    [DataRow(1005, "хиляда и пет")]
    [DataRow(1010, "хиляда и десет")]
    [DataRow(1011, "хиляда и единадесет")]
    [DataRow(1020, "хиляда и двадесет")]
    [DataRow(1021, "хиляда двадесет и едно")]
    [DataRow(1024, "хиляда двадесет и четири")]
    [DataRow(1100, "хиляда и сто")]
    [DataRow(1101, "хиляда сто и едно")]
    [DataRow(1110, "хиляда сто и десет")]
    [DataRow(1120, "хиляда сто и двадесет")]
    [DataRow(1121, "хиляда сто двадесет и едно")]
    [DataRow(1200, "хиляда и двеста")]
    [DataRow(1201, "хиляда двеста и едно")]
    [DataRow(1220, "хиляда двеста и двадесет")]
    [DataRow(1221, "хиляда двеста двадесет и едно")]
    [DataRow(1300, "хиляда и триста")]
    [DataRow(1500, "хиляда и петстотин")]
    [DataRow(2000, "две хиляди")]
    [DataRow(2001, "две хиляди и едно")]
    [DataRow(10000, "десет хиляди")]
    [DataRow(10001, "десет хиляди и едно")]
    [DataRow(10100, "десет хиляди и сто")]
    [DataRow(10101, "десет хиляди сто и едно")]
    public void Bulgarian_Thousands(int value, string expected)
        => Assert.AreEqual(expected, Get("BG").Convert(value));

    /// <summary>
    /// A compound multiplier keeps its own и before the scale noun, and the agreement of NTS-30B stays on its last
    /// numeral (една хиляди, един милиона, два милиона); the whole number adds и before a one-component last group.
    /// bulgaro.io: сто хиляди сто седемдесет и шест, сто и една хиляди сто седемдесет и шест, двеста и деветнадесет
    /// хиляди сто седемдесет и шест.
    /// </summary>
    [TestMethod]
    [DataRow(21000, "двадесет и една хиляди")]
    [DataRow(22000, "двадесет и две хиляди")]
    [DataRow(25000, "двадесет и пет хиляди")]
    [DataRow(21001, "двадесет и една хиляди и едно")]
    [DataRow(21100, "двадесет и една хиляди и сто")]
    [DataRow(21121, "двадесет и една хиляди сто двадесет и едно")]
    [DataRow(100000, "сто хиляди")]
    [DataRow(101000, "сто и една хиляди")]
    [DataRow(121000, "сто двадесет и една хиляди")]
    [DataRow(100176, "сто хиляди сто седемдесет и шест")]
    [DataRow(101176, "сто и една хиляди сто седемдесет и шест")]
    [DataRow(219176, "двеста и деветнадесет хиляди сто седемдесет и шест")]
    public void Bulgarian_ThousandsMultipliers(int value, string expected)
        => Assert.AreEqual(expected, Get("BG").Convert(value));

    /// <summary>Millions and milliards: the multiplier's own и, the masculine един/два of NTS-30B, the count form.</summary>
    [TestMethod]
    [DataRow(1000000L, "един милион")]
    [DataRow(1000001L, "един милион и едно")]
    [DataRow(1001000L, "един милион и хиляда")]
    [DataRow(1001001L, "един милион хиляда и едно")]
    [DataRow(1100000L, "един милион и сто хиляди")]
    [DataRow(1100001L, "един милион сто хиляди и едно")]
    [DataRow(21000000L, "двадесет и един милиона")]
    [DataRow(22000000L, "двадесет и два милиона")]
    [DataRow(25000000L, "двадесет и пет милиона")]
    [DataRow(21000001L, "двадесет и един милиона и едно")]
    [DataRow(21001000L, "двадесет и един милиона и хиляда")]
    [DataRow(21001001L, "двадесет и един милиона хиляда и едно")]
    [DataRow(21000121L, "двадесет и един милиона сто двадесет и едно")]
    [DataRow(21000000000L, "двадесет и един милиарда")]
    [DataRow(22000000000L, "двадесет и два милиарда")]
    [DataRow(25000000000L, "двадесет и пет милиарда")]
    public void Bulgarian_MillionsAndMilliards(long value, string expected)
        => Assert.AreEqual(expected, Get("BG").Convert(value));

    /// <summary>
    /// The Sofia University model number (ezik.bg, "Числително име": 1 200 065 785 = един милиард двеста милиона
    /// шейсет и пет хиляди седемстотин осемдесет и пет): one и inside the multiplier 65, one before the last component
    /// of the last group, none elsewhere. The converter writes the full form шестдесет for the colloquial шейсет.
    /// </summary>
    [TestMethod]
    public void Bulgarian_SourcedModelNumber()
        => Assert.AreEqual("един милиард двеста милиона шестдесет и пет хиляди седемстотин осемдесет и пет",
            Get("BG").Convert(1200065785));

    /// <summary>Generated scales (NTS-30C): the multiplier keeps its и and its forced masculine form, with the count form.</summary>
    [TestMethod]
    [DataRow(21, "двадесет и един трилиона")]
    [DataRow(22, "двадесет и два трилиона")]
    [DataRow(25, "двадесет и пет трилиона")]
    [DataRow(101, "сто и един трилиона")]
    [DataRow(121, "сто двадесет и един трилиона")]
    public void Bulgarian_GeneratedScales(int multiplier, string expected)
    {
        var converter = Get("BG");
        BigInteger trillion = BigInteger.Pow(10, 12);
        Assert.AreEqual(expected, converter.Convert(multiplier * trillion));
        Assert.AreEqual(expected + " и едно", converter.Convert(multiplier * trillion + 1));
        Assert.AreEqual(expected + " сто двадесет и едно", converter.Convert(multiplier * trillion + 121));
        Assert.AreEqual(expected.Replace("трилиона", "квадрилиона"), converter.Convert(multiplier * trillion * 1000));
        Assert.AreEqual(expected, converter.Convert(multiplier * trillion, "gender=feminine"),
            "the forced masculine multiplier of a generated scale is shielded from the caller's gender");
    }

    /// <summary>The caller's gender changes the last numeral only; и stays.</summary>
    [TestMethod]
    [DataRow(21, "standalone", "двадесет и едно")]
    [DataRow(21, "masculine", "двадесет и един")]
    [DataRow(21, "feminine", "двадесет и една")]
    [DataRow(21, "neuter", "двадесет и едно")]
    [DataRow(22, "masculine", "двадесет и два")]
    [DataRow(22, "feminine", "двадесет и две")]
    [DataRow(101, "masculine", "сто и един")]
    [DataRow(121, "feminine", "сто двадесет и една")]
    [DataRow(1001, "masculine", "хиляда и един")]
    [DataRow(21001, "feminine", "двадесет и една хиляди и една")]
    [DataRow(21000000, "feminine", "двадесет и един милиона")]
    public void Bulgarian_CallerGender(int value, string gender, string expected)
        => Assert.AreEqual(expected, Get("BG").Convert(value, $"gender={gender}"));

    /// <summary>Negative values put the minus word before the corrected cardinal.</summary>
    [TestMethod]
    public void Bulgarian_Negatives()
    {
        var converter = Get("BG");
        Assert.AreEqual("минус двадесет и едно", converter.Convert(-21));
        Assert.AreEqual("минус сто двадесет и едно", converter.Convert(-121));
        Assert.AreEqual("минус двадесет и една хиляди", converter.Convert(-21000));
    }

    /// <summary>
    /// Decimals: the integer part is the corrected cardinal; the decimal separator цяло and the digit reading are
    /// unchanged, and no second и is added between the parts.
    /// </summary>
    [TestMethod]
    public void Bulgarian_Decimals()
    {
        var converter = Get("BG");
        Assert.AreEqual("двадесет и едно цяло пет", converter.Convert(21.5m));
        Assert.AreEqual("сто и едно цяло пет", converter.Convert(101.5m));
        Assert.AreEqual("сто и едно цяло пет",
            converter.Convert(101.5m, -1, new DecimalFormatOptions { ReadingMode = DecimalReadingMode.DigitByDigit }));
        Assert.AreEqual("сто двадесет и едно цяло две едно", converter.Convert(121.21m));
    }

    /// <summary>
    /// A caller-defined currency (no Bulgarian vocabulary added): the unit and subunit cardinals get their own и,
    /// their forced gender stays on the last numeral, and the currency connector is the caller's.
    /// </summary>
    [TestMethod]
    public void Bulgarian_CallerCurrency()
    {
        var lev = new CurrencyDefinition
        {
            UnitSingular = "лев", UnitPlural = "лева", SubunitSingular = "стотинка", SubunitPlural = "стотинки", Connector = "и",
            UnitForcedVariants = ForcedVariantSet.Create(("gender", "masculine")),
            SubunitForcedVariants = ForcedVariantSet.Create(("gender", "feminine")),
        };
        var converter = Get("BG");
        Assert.AreEqual("двадесет и един лева и двадесет и една стотинки", converter.ConvertCurrency(21.21m, lev));
        Assert.AreEqual("сто и един лева и една стотинка", converter.ConvertCurrency(101.01m, lev));
        Assert.AreEqual("сто двадесет и един лева и двадесет и една стотинки", converter.ConvertCurrency(121.21m, lev));
    }

    /// <summary>
    /// Fractions keep the и of the numerator cardinal and of the ordinal denominator. NTS-30H replaced the connector
    /// reading (двадесет и едно на сто и едно) with the ordinal one; NTS-39 opened the compound numerator ending in една
    /// (plural denominator), which keeps the и of both terms.
    /// </summary>
    [TestMethod]
    public void Bulgarian_Fractions()
    {
        var converter = Get("BG");
        Assert.AreEqual("двадесет и две сто и първи", converter.ConvertFraction(22, 101));
        Assert.AreEqual("сто двадесет и пет хиляда и първи", converter.ConvertFraction(125, 1001));
        Assert.AreEqual("двадесет и две сто и първи", converter.Convert(new Number(22, 101)));
        Assert.AreEqual("двадесет и една сто и първи", converter.ConvertFraction(21, 101));
    }

    /// <summary>
    /// Ordinals come from BulgarianOrdinalLanguageSpecifics, which already wrote и and is unchanged: no doubled
    /// conjunction.
    /// </summary>
    [TestMethod]
    [DataRow(21, "двадесет и първи")]
    [DataRow(101, "сто и първи")]
    [DataRow(121, "сто двадесет и първи")]
    [DataRow(1001, "хиляда и първи")]
    [DataRow(1102, "хиляда сто и втори")]
    [DataRow(21001, "двадесет и една хиляди и първи")]
    public void Bulgarian_Ordinals_Unchanged(int value, string expected)
    {
        string ordinal = Get("BG").ConvertOrdinal(value);
        Assert.AreEqual(expected, ordinal);
        Assert.IsFalse(Regex.IsMatch(ordinal, @"\bи и\b"));
    }

    /// <summary>The clock hours (1–12) are single components: the literal и of the clock patterns is not doubled.</summary>
    [TestMethod]
    public void Bulgarian_Clock_Unchanged()
    {
        var converter = Get("BG");
        Assert.AreEqual("един и петнайсет", converter.ConvertClockTime(new TimeOnly(13, 15)));
        Assert.AreEqual("дванадесет и половина", converter.ConvertClockTime(new TimeOnly(12, 30)));
        Assert.AreEqual("единадесет без петнайсет", converter.ConvertClockTime(new TimeOnly(10, 45)));
    }
}
