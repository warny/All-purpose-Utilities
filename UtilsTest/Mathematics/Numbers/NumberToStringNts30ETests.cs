using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-30E: the separator between a scale multiplier and its scale noun
/// (<see cref="NumberScale.MultiplierSeparator"/>, overridden per exact scale by
/// <c>&lt;ScaleForm multiplierSeparator="…"&gt;</c> /
/// <see cref="NumberToStringConverterOptions.ScaleFormMultiplierSeparators"/>), and the Finnish cardinals it
/// enables: one word inside a group (<c>kaksikymmentäyksi</c>), multiplier and <c>tuhat/tuhatta</c> written
/// together (<c>kaksituhatta</c>), a space between groups. The first half pins the language-independent
/// engine contract on a synthetic language; the second half pins the sourced Finnish forms (sources in
/// <c>docs/NTS-08-linguistic-sources.md</c>, NTS-30E section).
/// </summary>
[TestClass]
public class NumberToStringNts30ETests
{
    // ===== synthetic engine ==========================================================================

    /// <summary>Unit words of the synthetic language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic language.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

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

    /// <summary>Creates a unique culture name.</summary>
    private static string NewName() => $"NTS30E-{Guid.NewGuid():N}";

    /// <summary>Renders a ten-entry digit table as XML digits.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The digit elements.</returns>
    private static string Digits(string[] table) => string.Concat(table.Select((s, i) => $"""<Digit digit="{i}" string="{s}" />"""));

    /// <summary>
    /// Renders a synthetic language: groups of two digits, scale 1 named "x" and scale 2 named "Y", tens
    /// composed with a space ("twenty one").
    /// </summary>
    /// <param name="culture">The culture identifier.</param>
    /// <param name="scaleAttributes">Attributes appended to the NumberScale element.</param>
    /// <param name="scaleContent">Elements appended inside the NumberScale (ScaleForm).</param>
    /// <param name="languageAttributes">Attributes appended to the Language element.</param>
    /// <param name="separator">The language separator.</param>
    /// <returns>The Language element.</returns>
    private static string Language(string culture, string scaleAttributes = "", string scaleContent = "", string languageAttributes = "", string separator = " ")
    {
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t}{separator}*" />"""));
        return $"""
              <Language groupSize="2" separator="{separator}" groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999999" {languageAttributes}>
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{Digits(Units)}</Group>
                  <Group level="2">{tens}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false" {scaleAttributes}>
                  <StaticNames><Scale value="0" string="" /><Scale value="1" string="x" /><Scale value="2" string="Y" /></StaticNames>
                  <Suffixes><Suffix>Z</Suffix></Suffixes>
                  {scaleContent}
                </NumberScale>
              </Language>
            """;
    }

    /// <summary>Wraps Language elements in a configuration document.</summary>
    /// <param name="languages">The Language elements.</param>
    /// <returns>The document.</returns>
    private static string Document(params string[] languages) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
        {string.Concat(languages)}
        </Numbers>
        """;

    /// <summary>Builds the synthetic converter.</summary>
    /// <param name="scaleAttributes">Attributes appended to the NumberScale element.</param>
    /// <param name="scaleContent">Elements appended inside the NumberScale.</param>
    /// <param name="languageAttributes">Attributes appended to the Language element.</param>
    /// <param name="separator">The language separator.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string scaleAttributes = "", string scaleContent = "", string languageAttributes = "", string separator = " ")
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(Document(Language(culture, scaleAttributes, scaleContent, languageAttributes, separator)))[culture];
    }

    /// <summary>Returns <paramref name="multiplier"/> × 100^<paramref name="scale"/> (the synthetic groups have two digits).</summary>
    /// <param name="multiplier">The group multiplier.</param>
    /// <param name="scale">The scale index.</param>
    /// <returns>The value.</returns>
    private static BigInteger S(long multiplier, int scale) => multiplier * BigInteger.Pow(100, scale);

    /// <summary>Returns the form <paramref name="key"/> of <paramref name="forms"/>.</summary>
    /// <param name="forms">The forms.</param>
    /// <param name="key">The form key.</param>
    /// <returns>The form.</returns>
    private static string Form(LexicalFormSet forms, string key)
    {
        Assert.IsTrue(forms.TryGetForm(key, out string value), key);
        return value;
    }

    /// <summary>Without any multiplier separator, the language separator joins multiplier and scale noun (historical behaviour).</summary>
    [TestMethod]
    public void Synthetic_Absent_UsesTheLanguageSeparator()
    {
        var spaced = Build();
        Assert.IsNull(spaced.Scale.MultiplierSeparator);
        Assert.AreEqual(0, spaced.ScaleFormMultiplierSeparators.Count);
        Assert.AreEqual("two x", spaced.Convert(S(2, 1)));
        Assert.AreEqual("two Y", spaced.Convert(S(2, 2)));
        Assert.AreEqual("two Y twenty one x five", spaced.Convert(S(2, 2) + S(21, 1) + 5));

        // A language whose separator is empty (Hungarian kétezer) keeps gluing its scales.
        var glued = Build(separator: "");
        Assert.AreEqual("twox", glued.Convert(S(2, 1)));
        Assert.AreEqual("twoY", glued.Convert(S(2, 2)));
    }

    /// <summary>The NumberScale default replaces the language separator at the multiplier boundary only.</summary>
    [TestMethod]
    public void Synthetic_NumberScaleDefault_AppliesToEveryScale()
    {
        var converter = Build(scaleAttributes: """multiplierSeparator="_" """);

        Assert.AreEqual("_", converter.Scale.MultiplierSeparator);
        Assert.AreEqual("two_x", converter.Convert(S(2, 1)));
        Assert.AreEqual("two_Y", converter.Convert(S(2, 2)));
        // Inside a group and between groups the language separators are untouched.
        Assert.AreEqual("two_Y twenty one_x five", converter.Convert(S(2, 2) + S(21, 1) + 5));

        // An explicit space on a glued language spaces the scales only.
        var glued = Build(scaleAttributes: """multiplierSeparator=" " """, separator: "");
        Assert.AreEqual(" ", glued.Scale.MultiplierSeparator);
        Assert.AreEqual("two x", glued.Convert(S(2, 1)));
        Assert.AreEqual("twentyone Y", glued.Convert(S(21, 2)));
    }

    /// <summary>An exact ScaleForm override, even empty, wins for its scale; the other scales inherit the NumberScale default.</summary>
    [TestMethod]
    public void Synthetic_ExactOverride_WinsForItsScaleOnly()
    {
        var converter = Build(scaleAttributes: """multiplierSeparator=" " """, scaleContent: """<ScaleForm scale="1" multiplierSeparator="" />""");

        Assert.AreEqual("", converter.ScaleFormMultiplierSeparators[1]);
        Assert.IsFalse(converter.ScaleFormMultiplierSeparators.ContainsKey(2));
        Assert.AreEqual("twox", converter.Convert(S(2, 1)));
        Assert.AreEqual("twenty onex", converter.Convert(S(21, 1)));
        Assert.AreEqual("two Y", converter.Convert(S(2, 2)));
        Assert.AreEqual("two Y twenty onex five", converter.Convert(S(2, 2) + S(21, 1) + 5));
        Assert.AreEqual("minus twox", converter.Convert(-S(2, 1)));

        // Without a NumberScale default, the override still applies and the other scales keep the language separator.
        var noDefault = Build(scaleContent: """<ScaleForm scale="2" multiplierSeparator="=" />""");
        Assert.AreEqual("two x", noDefault.Convert(S(2, 1)));
        Assert.AreEqual("two=Y", noDefault.Convert(S(2, 2)));
    }

    /// <summary>The override is orthogonal to the form selection: it applies to whichever form the selector chooses.</summary>
    [TestMethod]
    public void Synthetic_ExactOverride_AppliesToEverySelectedForm()
    {
        var converter = Build(scaleAttributes: """multiplierSeparator=" " """, scaleContent: """
            <ScaleForm scale="1" multiplierSeparator="">
              <Forms><Form key="singular" value="x" /><Form key="plural" value="xs" /></Forms>
            </ScaleForm>
            """);

        Assert.AreEqual("onex", converter.Convert(S(1, 1)));
        Assert.AreEqual("twoxs", converter.Convert(S(2, 1)));
        Assert.AreEqual("twenty onexs", converter.Convert(S(21, 1)));
        Assert.AreEqual("xs", Form(converter.ScaleForms[1], "plural"));
    }

    /// <summary>Whitespace is a legitimate separator and is never trimmed.</summary>
    [TestMethod]
    public void Synthetic_SeparatorsAreNotTrimmed()
    {
        var converter = Build(scaleAttributes: """multiplierSeparator="  " """, scaleContent: """<ScaleForm scale="1" multiplierSeparator=" - " />""");

        Assert.AreEqual("  ", converter.Scale.MultiplierSeparator);
        Assert.AreEqual(" - ", converter.ScaleFormMultiplierSeparators[1]);
        Assert.AreEqual("two - x", converter.Convert(S(2, 1)));
        Assert.AreEqual("two  Y", converter.Convert(S(2, 2)));
    }

    /// <summary>A multiplier written after its scale noun uses the same separator.</summary>
    [TestMethod]
    public void Synthetic_AfterScalePosition_UsesTheSameSeparator()
    {
        var converter = Build(scaleAttributes: """multiplierSeparator=" " """, scaleContent: """<ScaleForm scale="1" multiplierSeparator="" />""",
            languageAttributes: """multiplierPosition="afterScale" """);

        Assert.AreEqual("xtwo", converter.Convert(S(2, 1)));
        Assert.AreEqual("Y two", converter.Convert(S(2, 2)));
    }

    /// <summary>
    /// The scale connector keeps its historical formatting (language separator, connector, language separator)
    /// when its threshold is reached; below the threshold the effective multiplier separator applies.
    /// </summary>
    [TestMethod]
    public void Synthetic_ScaleConnector_KeepsItsHistoricalFormatting()
    {
        const string connector = """scaleConnector="de" scaleConnectorThreshold="20" """;
        var converter = Build(scaleAttributes: """multiplierSeparator=" " """, scaleContent: """<ScaleForm scale="1" multiplierSeparator="" />""",
            languageAttributes: connector);

        Assert.AreEqual("twox", converter.Convert(S(2, 1)), "threshold missed: the empty override");
        Assert.AreEqual("twenty five de x", converter.Convert(S(25, 1)), "threshold hit: separator + connector + separator");
        Assert.AreEqual("two Y", converter.Convert(S(2, 2)), "threshold missed: the NumberScale default");
        Assert.AreEqual("twenty five de Y", converter.Convert(S(25, 2)));
        Assert.AreEqual("twenty five de Y twox one", converter.Convert(S(25, 2) + S(2, 1) + 1));

        // Byte-compatible with the historical configuration when no multiplier separator is configured.
        var historical = Build(languageAttributes: connector);
        Assert.AreEqual("two x", historical.Convert(S(2, 1)));
        Assert.AreEqual("twenty five de x", historical.Convert(S(25, 1)));
    }

    /// <summary>
    /// baseOn: an absent attribute inherits, an explicit value (empty included) replaces; a ScaleForm of the child
    /// replaces the inherited entry of its scale as a whole, separator included.
    /// </summary>
    [TestMethod]
    public void Synthetic_BaseOn_InheritsAndReplaces()
    {
        string parent = NewName(), inheriting = NewName(), replacing = NewName(), emptied = NewName(), reset = NewName();
        var converters = NumberToStringConverter.ReadConfiguration(Document(
            Language(parent, """multiplierSeparator=" " """, """<ScaleForm scale="1" multiplierSeparator="" />"""),
            $"""<Language baseOn="{parent}"><Culture>{inheriting}</Culture></Language>""",
            $"""<Language baseOn="{parent}"><Culture>{replacing}</Culture><NumberScale multiplierSeparator="-" /></Language>""",
            $"""<Language baseOn="{parent}"><Culture>{emptied}</Culture><NumberScale multiplierSeparator="" /></Language>""",
            $"""<Language baseOn="{parent}"><Culture>{reset}</Culture><NumberScale><ScaleForm scale="1" /></NumberScale></Language>"""));

        Assert.AreEqual("twox two Y", Both(converters[parent]));
        Assert.AreEqual("twox two Y", Both(converters[inheriting]));
        Assert.AreEqual("twox two-Y", Both(converters[replacing]));
        Assert.AreEqual("twox twoY", Both(converters[emptied]));
        Assert.AreEqual("", converters[emptied].Scale.MultiplierSeparator);
        Assert.AreEqual("two x two Y", Both(converters[reset]));
        Assert.IsFalse(converters[reset].ScaleFormMultiplierSeparators.ContainsKey(1));

        static string Both(NumberToStringConverter c) => c.Convert(S(2, 1)) + " " + c.Convert(S(2, 2));
    }

    /// <summary>A generated scale uses the NumberScale default; an exact override keyed by its index still wins.</summary>
    [TestMethod]
    public void Generated_UsesTheNumberScaleDefault()
    {
        _ = NumberToStringConverter.GetConverter("EN");
        string plain = NewName(), exact = NewName();
        var converters = NumberToStringConverter.ReadConfiguration(Document(
            $"""<Language baseOn="EN"><Culture>{plain}</Culture><NumberScale multiplierSeparator="_"><ScaleForm scale="1" multiplierSeparator="" /></NumberScale></Language>""",
            $"""<Language baseOn="EN"><Culture>{exact}</Culture><NumberScale><ScaleForm scale="2" multiplierSeparator="=" /></NumberScale></Language>"""));

        Assert.AreEqual("twothousand", converters[plain].Convert(2000));
        Assert.AreEqual("two_million", converters[plain].Convert(2_000_000));
        Assert.AreEqual("two_billion", converters[plain].Convert(2_000_000_000));
        Assert.AreEqual("two thousand", converters[exact].Convert(2000));
        Assert.AreEqual("two=million", converters[exact].Convert(2_000_000));
        Assert.AreEqual("two billion", converters[exact].Convert(2_000_000_000));
    }

    /// <summary>Programmatic construction, cloning and the immutable snapshot of the configured separators.</summary>
    [TestMethod]
    public void Programmatic_ConstructionCloneAndSnapshot()
    {
        var source = Build();
        var overrides = new Dictionary<int, string> { [1] = "" };
        var options = new NumberToStringConverterOptions(source)
        {
            Scale = new NumberScale(["", "x", "Y"], ["Z"]) { MultiplierSeparator = "+" },
            ScaleFormMultiplierSeparators = overrides,
        };
        var converter = new NumberToStringConverter(options);
        overrides[1] = "!";
        overrides[2] = "!";

        Assert.AreEqual("twox", converter.Convert(S(2, 1)));
        Assert.AreEqual("two+Y", converter.Convert(S(2, 2)));
        Assert.AreEqual(1, converter.ScaleFormMultiplierSeparators.Count, "the dictionary is snapshotted at construction");

        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(converter));
        Assert.AreEqual("+", clone.Scale.MultiplierSeparator);
        Assert.AreEqual("", clone.ScaleFormMultiplierSeparators[1]);
        Assert.AreEqual("twox", clone.Convert(S(2, 1)));
        Assert.AreEqual("two+Y", clone.Convert(S(2, 2)));

        // A clone given a scale without a default falls back to the language separator.
        var rescaled = new NumberToStringConverter(new NumberToStringConverterOptions(converter) { Scale = new NumberScale(["", "x", "Y"], ["Z"]) });
        Assert.AreEqual("twox", rescaled.Convert(S(2, 1)));
        Assert.AreEqual("two Y", rescaled.Convert(S(2, 2)));
    }

    /// <summary>Overrides must target a scale the NumberScale can name and must not be null.</summary>
    [TestMethod]
    public void Programmatic_InvalidOverridesAreRejected()
    {
        var source = Build();

        foreach (int index in new[] { 0, -1, 99 })
            Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(source)
            {
                ScaleFormMultiplierSeparators = new Dictionary<int, string> { [index] = "" },
            }), $"scale {index}");
        Assert.ThrowsExactly<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            ScaleFormMultiplierSeparators = new Dictionary<int, string> { [1] = null! },
        }));
        Assert.ThrowsExactly<ArgumentException>(() => Build(scaleContent: """<ScaleForm scale="99" multiplierSeparator="" />"""));
    }

    /// <summary>The new boundary introduces no intermediate finalization: one public result, one call.</summary>
    [TestMethod]
    public void Finalization_RunsOncePerPublicResult()
    {
        var specifics = new CountingSpecifics();
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(Build(
            scaleAttributes: """multiplierSeparator=" " """, scaleContent: """<ScaleForm scale="1" multiplierSeparator="" />"""))
        {
            LanguageSpecifics = specifics,
        });

        Assert.AreEqual("two Y twenty onex five", converter.Convert(S(2, 2) + S(21, 1) + 5));
        Assert.AreEqual(1, specifics.Calls);
    }

    /// <summary>
    /// The built-in configurations state their junction explicitly: the SCALE bases a space, Hungarian (which inherits
    /// SCALE-LONG but glues its scale nouns) an empty string, Finnish a space overridden by "" for the thousands.
    /// </summary>
    [TestMethod]
    public void BuiltIn_ScaleBasesAndExceptionsAreExplicit()
    {
        Assert.AreEqual(" ", NumberToStringConverter.GetConverter("SCALE-LONG").Scale.MultiplierSeparator);
        Assert.AreEqual(" ", NumberToStringConverter.GetConverter("SCALE-SHORT").Scale.MultiplierSeparator);
        Assert.AreEqual(" ", NumberToStringConverter.GetConverter("SCALE-SHORT-CYRILLIC").Scale.MultiplierSeparator);
        Assert.AreEqual("", NumberToStringConverter.GetConverter("HU").Scale.MultiplierSeparator);
        Assert.AreEqual("", NumberToStringConverter.GetConverter("HU-HU").Scale.MultiplierSeparator);
        Assert.AreEqual(" ", NumberToStringConverter.GetConverter("FI").Scale.MultiplierSeparator);
        Assert.AreEqual("", NumberToStringConverter.GetConverter("FI").ScaleFormMultiplierSeparators[1]);

        // Hungarian keeps its joined scale nouns.
        var hu = NumberToStringConverter.GetConverter("HU");
        Assert.AreEqual("ezer", hu.Convert(1000));
        Assert.IsFalse(hu.Convert(2000).Contains(' '), hu.Convert(2000));
        Assert.IsFalse(hu.Convert(2_000_000).Contains(' '), hu.Convert(2_000_000));
    }

    /// <summary>
    /// Every built-in language but Finnish resolves the junction exactly as before NTS-30E (its language separator), and
    /// configures no per-scale override: their outputs cannot change.
    /// </summary>
    [TestMethod]
    public void BuiltIn_EffectiveJunctionIsTheHistoricalOneOutsideFinnish()
    {
        string[] cultures = ["AR", "BG", "CA", "ca-ES-valencia", "CS", "DA", "DE", "de-CH", "EE", "EL", "EN", "EN-GB", "ES", "EU", "FA",
            "FI", "FR", "FR-be", "FR-ca", "FR-ch", "GL", "HE", "HI", "HR", "HU", "ID", "IT", "JA", "KO", "MS", "NL", "NO", "PL", "PT",
            "RO", "RU", "SCALE-LONG", "SCALE-SHORT", "SCALE-SHORT-CYRILLIC", "SK", "SV", "SW", "TR", "UK", "VN", "WO", "ZH", "ZU"];
        foreach (string culture in cultures)
        {
            var converter = NumberToStringConverter.GetConverter(culture);
            if (culture.StartsWith("FI", StringComparison.OrdinalIgnoreCase)) continue;
            // NTS-49: the French configurations deliberately hyphenate the junction (1990 spelling: deux-millions).
            string expectedJunction = culture.StartsWith("FR", StringComparison.OrdinalIgnoreCase) ? "-" : converter.Separator;
            Assert.AreEqual(expectedJunction, converter.Scale.MultiplierSeparator ?? converter.Separator, culture);
            Assert.AreEqual(0, converter.ScaleFormMultiplierSeparators.Count, culture);
        }
    }

    // ===== Finnish ===================================================================================

    /// <summary>The Finnish converter.</summary>
    private static NumberToStringConverter FI => NumberToStringConverter.GetConverter("FI");

    /// <summary>
    /// Kielitoimiston ohjepankki, "Luvut ja numerot: numeroiden ryhmittely": the part below a thousand is one
    /// word, so is the part counting thousands; "tuhat viisisataa", "neljätoistatuhatta kuusisataa",
    /// "viisimiljoonaa neljäsataakaksikymmentäkuusituhatta kuusisataaseitsemänkymmentäneljä".
    /// </summary>
    [DataTestMethod]
    [DataRow(0, "nolla")]
    [DataRow(1, "yksi")]
    [DataRow(9, "yhdeksän")]
    [DataRow(10, "kymmenen")]
    [DataRow(11, "yksitoista")]
    [DataRow(19, "yhdeksäntoista")]
    [DataRow(20, "kaksikymmentä")]
    [DataRow(21, "kaksikymmentäyksi")]
    [DataRow(22, "kaksikymmentäkaksi")]
    [DataRow(25, "kaksikymmentäviisi")]
    [DataRow(30, "kolmekymmentä")]
    [DataRow(99, "yhdeksänkymmentäyhdeksän")]
    [DataRow(100, "sata")]
    [DataRow(101, "satayksi")]
    [DataRow(110, "satakymmenen")]
    [DataRow(111, "satayksitoista")]
    [DataRow(120, "satakaksikymmentä")]
    [DataRow(121, "satakaksikymmentäyksi")]
    [DataRow(199, "satayhdeksänkymmentäyhdeksän")]
    [DataRow(200, "kaksisataa")]
    [DataRow(201, "kaksisataayksi")]
    [DataRow(220, "kaksisataakaksikymmentä")]
    [DataRow(221, "kaksisataakaksikymmentäyksi")]
    [DataRow(999, "yhdeksänsataayhdeksänkymmentäyhdeksän")]
    [DataRow(1000, "tuhat")]
    [DataRow(1001, "tuhat yksi")]
    [DataRow(1010, "tuhat kymmenen")]
    [DataRow(1021, "tuhat kaksikymmentäyksi")]
    [DataRow(1100, "tuhat sata")]
    [DataRow(1121, "tuhat satakaksikymmentäyksi")]
    [DataRow(1500, "tuhat viisisataa")]
    [DataRow(1999, "tuhat yhdeksänsataayhdeksänkymmentäyhdeksän")]
    [DataRow(2000, "kaksituhatta")]
    [DataRow(2001, "kaksituhatta yksi")]
    [DataRow(2021, "kaksituhatta kaksikymmentäyksi")]
    [DataRow(2100, "kaksituhatta sata")]
    [DataRow(2121, "kaksituhatta satakaksikymmentäyksi")]
    [DataRow(3000, "kolmetuhatta")]
    [DataRow(5000, "viisituhatta")]
    [DataRow(10000, "kymmenentuhatta")]
    [DataRow(11000, "yksitoistatuhatta")]
    [DataRow(14600, "neljätoistatuhatta kuusisataa")]
    [DataRow(20000, "kaksikymmentätuhatta")]
    [DataRow(21000, "kaksikymmentäyksituhatta")]
    [DataRow(99000, "yhdeksänkymmentäyhdeksäntuhatta")]
    [DataRow(100000, "satatuhatta")]
    [DataRow(101000, "satayksituhatta")]
    [DataRow(121000, "satakaksikymmentäyksituhatta")]
    [DataRow(426674, "neljäsataakaksikymmentäkuusituhatta kuusisataaseitsemänkymmentäneljä")]
    [DataRow(999000, "yhdeksänsataayhdeksänkymmentäyhdeksäntuhatta")]
    [DataRow(21121, "kaksikymmentäyksituhatta satakaksikymmentäyksi")]
    [DataRow(121999, "satakaksikymmentäyksituhatta yhdeksänsataayhdeksänkymmentäyhdeksän")]
    [DataRow(999999, "yhdeksänsataayhdeksänkymmentäyhdeksäntuhatta yhdeksänsataayhdeksänkymmentäyhdeksän")]
    public void Finnish_Nominative(int value, string expected)
    {
        Assert.AreEqual(expected, FI.Convert(value));
        Assert.AreEqual(expected, FI.Convert(value, "sijamuoto=nominatiivi"));
    }

    /// <summary>The three boundaries: none inside a group, none before tuhat/tuhatta, exactly one space between groups.</summary>
    [TestMethod]
    public void Finnish_Boundaries()
    {
        Assert.IsFalse(FI.Convert(21).Contains(' '));
        Assert.IsFalse(FI.Convert(121).Contains(' '));
        Assert.IsFalse(FI.Convert(21000).Contains(' '));
        Assert.IsFalse(FI.Convert(121000).Contains(' '));
        Assert.AreEqual(1, FI.Convert(21121).Count(c => c == ' '));
        Assert.AreEqual(1, FI.Convert(999999).Count(c => c == ' '));
        Assert.IsFalse(FI.Convert(21121).Contains("  "));
    }

    /// <summary>
    /// Thousand agreement: tuhat after the elided multiplier one, tuhatta (partitive) after every other multiplier
    /// (Kielitoimiston ohjepankki: "tuhat viisisataa", "viisituhatta", "neljätoistatuhatta", "satatuhatta").
    /// </summary>
    [TestMethod]
    public void Finnish_ThousandForms()
    {
        Assert.AreEqual(" ", FI.Scale.MultiplierSeparator);
        Assert.AreEqual("", FI.ScaleFormMultiplierSeparators[1]);
        Assert.AreEqual("tuhat", Form(FI.ScaleForms[1], "singular"));
        Assert.AreEqual("tuhatta", Form(FI.ScaleForms[1], "plural"));
    }

    /// <summary>
    /// Kielitoimiston ohjepankki, "Luvut ja numerot: peruslukujen taivuttaminen": every component of an inflected
    /// compound inflects, in one word (20:tä kahtakymmentä, 21:tä kahtakymmentäyhtä, 22:ta kahtakymmentäkahta,
    /// 115:tä sataaviittätoista, 125:tä sataakahtakymmentäviittä, 523:a viittäsataakahtakymmentäkolmea,
    /// 3 000:ta kolmeatuhatta). The other rows apply the same rule to the sourced component forms.
    /// </summary>
    [DataTestMethod]
    [DataRow(1, "yhtä")]
    [DataRow(9, "yhdeksää")]
    [DataRow(10, "kymmentä")]
    [DataRow(11, "yhtätoista")]
    [DataRow(17, "seitsemäätoista")]
    [DataRow(20, "kahtakymmentä")]
    [DataRow(21, "kahtakymmentäyhtä")]
    [DataRow(22, "kahtakymmentäkahta")]
    [DataRow(100, "sataa")]
    [DataRow(101, "sataayhtä")]
    [DataRow(115, "sataaviittätoista")]
    [DataRow(121, "sataakahtakymmentäyhtä")]
    [DataRow(125, "sataakahtakymmentäviittä")]
    [DataRow(200, "kahtasataa")]
    [DataRow(201, "kahtasataayhtä")]
    [DataRow(211, "kahtasataayhtätoista")]
    [DataRow(523, "viittäsataakahtakymmentäkolmea")]
    [DataRow(1000, "tuhatta")]
    [DataRow(1001, "tuhatta yhtä")]
    [DataRow(2000, "kahtatuhatta")]
    [DataRow(3000, "kolmeatuhatta")]
    [DataRow(10000, "kymmentätuhatta")]
    [DataRow(21000, "kahtakymmentäyhtätuhatta")]
    [DataRow(100000, "sataatuhatta")]
    [DataRow(21121, "kahtakymmentäyhtätuhatta sataakahtakymmentäyhtä")]
    public void Finnish_Partitive(int value, string expected)
        => Assert.AreEqual(expected, FI.Convert(value, "sijamuoto=partitiivi"));

    /// <summary>
    /// Same source: 20:n kahdenkymmenen, 21:n kahdenkymmenenyhden, 22:n kahdenkymmenenkahden,
    /// 125:n sadankahdenkymmenenviiden, 3 000:n kolmentuhannen; seitsemän, kahdeksan and yhdeksän are
    /// invariable (7:n seitsemän, 17:n seitsemäntoista).
    /// </summary>
    [DataTestMethod]
    [DataRow(1, "yhden")]
    [DataRow(7, "seitsemän")]
    [DataRow(10, "kymmenen")]
    [DataRow(11, "yhdentoista")]
    [DataRow(17, "seitsemäntoista")]
    [DataRow(20, "kahdenkymmenen")]
    [DataRow(21, "kahdenkymmenenyhden")]
    [DataRow(22, "kahdenkymmenenkahden")]
    [DataRow(70, "seitsemänkymmenen")]
    [DataRow(100, "sadan")]
    [DataRow(101, "sadanyhden")]
    [DataRow(121, "sadankahdenkymmenenyhden")]
    [DataRow(125, "sadankahdenkymmenenviiden")]
    [DataRow(200, "kahdensadan")]
    [DataRow(221, "kahdensadankahdenkymmenenyhden")]
    [DataRow(1000, "tuhannen")]
    [DataRow(2000, "kahdentuhannen")]
    [DataRow(3000, "kolmentuhannen")]
    [DataRow(10000, "kymmenentuhannen")]
    [DataRow(21000, "kahdenkymmenenyhdentuhannen")]
    [DataRow(21121, "kahdenkymmenenyhdentuhannen sadankahdenkymmenenyhden")]
    public void Finnish_Genitive(int value, string expected)
        => Assert.AreEqual(expected, FI.Convert(value, "sijamuoto=genetiivi"));

    /// <summary>The composite surfaces reuse the corrected cardinal; decimal digits keep their own pronunciation.</summary>
    [TestMethod]
    public void Finnish_CompositeSurfaces()
    {
        Assert.AreEqual("miinus kaksikymmentäyksi", FI.Convert(-21));
        Assert.AreEqual("miinus kaksikymmentäyksituhatta", FI.Convert(-21000));
        Assert.AreEqual("kaksikymmentäyksi pilkku viisi", FI.Convert(21.5m));
        Assert.AreEqual("kaksikymmentäyksi pilkku viisi seitsemän",
            FI.Convert(21.57m, -1, new DecimalFormatOptions { ReadingMode = DecimalReadingMode.DigitByDigit }));
        Assert.AreEqual("kaksikymmentäyksi yli satayksi", FI.ConvertFraction(21, 101));
        Assert.AreEqual("kaksikymmentäyksi yli satayksi", FI.Convert(new Utils.Numerics.Number(21, 101)));
        Assert.AreEqual("kaksituhatta kaksikymmentäyksi", FI.ConvertYear(2021));
        var euro = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euroa", SubunitSingular = "sentti", SubunitPlural = "senttiä", Connector = "ja" };
        Assert.AreEqual("kaksikymmentäyksi euroa ja viisi senttiä", FI.ConvertCurrency(21.05m, euro));
        Assert.AreEqual("puoli kaksitoista", FI.ConvertClockTime(new TimeOnly(11, 30)));
    }

    /// <summary>
    /// The ordinals below a thousand keep their NTS-28 forms. NTS-37 opened the ordinals from 1001 on, with one
    /// space between groups (Kielitoimiston ohjepankki: 3 760. kolmastuhannes seitsemässadaskuudeskymmenes); see
    /// <see cref="NumberToStringNts37Tests"/>.
    /// </summary>
    [TestMethod]
    public void Finnish_OrdinalsBelowAThousandAreUnchanged()
    {
        Assert.AreEqual("kahdeskymmenesensimmäinen", FI.ConvertOrdinal(21));
        Assert.AreEqual("sadaskahdeskymmenesensimmäinen", FI.ConvertOrdinal(121));
        Assert.AreEqual("tuhannes", FI.ConvertOrdinal(1000));
        Assert.AreEqual("tuhannes yhdeksässadasyhdeksäskymmenesyhdeksäs", FI.ConvertOrdinal(1999));
    }

    /// <summary>NTS-37 removed the 0-999 999 limit: the millions and the larger scales are named (see <see cref="NumberToStringNts37Tests"/>).</summary>
    [TestMethod]
    public void Finnish_DomainIsUnbounded()
    {
        Assert.IsNull(FI.MaxNumber);
        Assert.AreEqual("miljoona", FI.Convert(1_000_000));
    }
}
