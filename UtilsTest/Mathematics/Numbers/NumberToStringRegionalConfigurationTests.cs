using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Verifies the general → regional <c>baseOn</c> hierarchy of the built-in configurations: every public
/// culture is declared by exactly one document, resolves to that document's converter, and regional
/// children inherit or replace exactly the sections they are meant to.
/// </summary>
[TestClass]
public class NumberToStringRegionalConfigurationTests
{
    /// <summary>XML namespace of the built-in configuration documents.</summary>
    private static readonly XNamespace ConfigurationNamespace = "Utils/NumberConvertionConfiguration.xsd";

    /// <summary>Public culture → name of the only built-in document allowed to declare it.</summary>
    private static readonly IReadOnlyDictionary<string, string> ExpectedDocumentByCulture =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FR"] = "FR-fr-ca",
            ["FR-fr"] = "FR-fr-ca",
            ["FR-ca"] = "FR-fr-ca",
            ["FR-be"] = "FR-be",
            ["FR-ch"] = "FR-ch",
            ["CA"] = "CA",
            ["ca-ES"] = "CA",
            ["ca-ES-valencia"] = "CA-valencia",
            ["ID"] = "ID",
            ["ID-ID"] = "ID",
            ["MS"] = "MS",
            ["MS-MY"] = "MS",
            ["EN"] = "EN",
            ["EN-us"] = "EN",
            ["EN-GB"] = "EN-GB",
            ["EN-uk"] = "EN-GB",
            ["DE"] = "DE",
            ["de-CH"] = "DE-ch",
        };

    /// <summary>Ensures no culture is declared twice, whether in two documents or twice in one.</summary>
    [TestMethod]
    public void BuiltInCultures_AreDeclaredExactlyOnce()
    {
        var duplicates = GetDeclaredCultures()
            .GroupBy(declaration => declaration.Culture, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(declaration => declaration.Document))}")
            .ToArray();

        Assert.AreEqual(0, duplicates.Length, string.Join(" | ", duplicates));
    }

    /// <summary>Ensures each regional culture is declared by its own document, never by its general parent.</summary>
    [TestMethod]
    public void RegionalCultures_AreDeclaredByTheirOwnDocument()
    {
        var declarations = GetDeclaredCultures()
            .ToDictionary(declaration => declaration.Culture, declaration => declaration.Document, StringComparer.OrdinalIgnoreCase);

        foreach (var (culture, document) in ExpectedDocumentByCulture)
        {
            Assert.IsTrue(declarations.TryGetValue(culture, out string? actual), $"{culture} is not declared.");
            Assert.AreEqual(document, actual, culture);
        }
    }

    /// <summary>Ensures each regional child derives from its general language and inherits or replaces ClockTime as designed.</summary>
    [TestMethod]
    [DataRow("FR-be", "FR", false)]
    [DataRow("FR-ch", "FR", false)]
    [DataRow("CA-valencia", "CA", true)]
    [DataRow("MS", "ID", true)]
    [DataRow("EN-GB", "EN", false)]
    [DataRow("DE-ch", "DE", false)]
    public void RegionalChildren_DeriveFromTheirGeneralLanguage(string document, string expectedBase, bool replacesClockTime)
    {
        XElement language = LoadDocument(document).Root!.Elements(ConfigurationNamespace + "Language").Single();

        Assert.AreEqual(expectedBase, (string?)language.Attribute("baseOn"));
        Assert.AreEqual(replacesClockTime, language.Element(ConfigurationNamespace + "ClockTime") != null);
        Assert.IsTrue(NumberToStringConverter.GetConverter(expectedBase).SupportsClockTimeConversion);
    }

    /// <summary>
    /// Ensures every public culture resolves through <see cref="NumberToStringConverter.GetConverter(string)"/>
    /// to a converter bound to that culture and exhibiting the wording of its own document, so a
    /// regional culture can never be served accidentally by its general parent (or vice versa).
    /// </summary>
    [TestMethod]
    [DataRow("FR", "cardinal:70", "soixante-dix")]
    [DataRow("FR-fr", "cardinal:70", "soixante-dix")]
    [DataRow("FR-ca", "cardinal:70", "soixante-dix")]
    [DataRow("FR-be", "cardinal:80", "quatre-vingts")]
    [DataRow("FR-be", "cardinal:70", "septante")]
    [DataRow("FR-ch", "cardinal:80", "huitante")]
    [DataRow("FR-ch", "cardinal:70", "septante")]
    [DataRow("CA", "clock:01:15", "un quart de dues")]
    [DataRow("ca-ES", "clock:01:15", "un quart de dues")]
    [DataRow("ca-ES-valencia", "clock:01:15", "la una i quart")]
    [DataRow("ID", "cardinal:8", "delapan")]
    [DataRow("ID-ID", "cardinal:8", "delapan")]
    [DataRow("MS", "cardinal:8", "lapan")]
    [DataRow("MS-MY", "cardinal:8", "lapan")]
    [DataRow("EN", "date:2026-07-02", "July second, twenty twenty-six")]
    [DataRow("EN-us", "date:2026-07-02", "July second, twenty twenty-six")]
    [DataRow("EN-GB", "date:2026-07-02", "second July twenty twenty-six")]
    [DataRow("EN-uk", "date:2026-07-02", "second July twenty twenty-six")]
    [DataRow("DE", "cardinal:1000", "tausend")]
    [DataRow("de-CH", "cardinal:1000", "ein tausend")]
    public void PublicCultures_ResolveToTheirOwnConfiguration(string culture, string probe, string expected)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.AreEqual(culture, converter.LanguageIdentifier, ignoreCase: true);
        string[] parts = probe.Split(':', 2);
        string actual = parts[0] switch
        {
            "cardinal" => converter.Convert(long.Parse(parts[1])),
            "clock" => converter.ConvertClockTime(TimeOnly.Parse(parts[1])),
            "date" => converter.Convert(DateOnly.Parse(parts[1])),
            _ => throw new ArgumentOutOfRangeException(nameof(probe), probe, null),
        };
        Assert.AreEqual(expected, actual, culture);
    }

    /// <summary>Ensures French regional children inherit the parent idiomatic ClockTime section.</summary>
    [TestMethod]
    [DataRow("FR-be")]
    [DataRow("FR-ch")]
    public void FrenchRegionalChildren_InheritFrenchClockTime(string culture)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.IsTrue(converter.SupportsClockTimeConversion);
        Assert.AreEqual("une heure et quart", converter.ConvertClockTime(new TimeOnly(1, 15)));
        Assert.AreEqual("deux heures moins le quart", converter.ConvertClockTime(new TimeOnly(1, 45)));
        Assert.AreEqual("midi moins cinq", converter.ConvertClockTime(new TimeOnly(11, 55)));
    }

    /// <summary>
    /// Ensures the Valencian invariable "dos" stays scoped to <c>ConvertClockTime</c>: the exact-time
    /// APIs keep the inherited Catalan gender agreement ("dues hores").
    /// </summary>
    [TestMethod]
    public void Valencian_InvariableDos_IsLimitedToClockTime()
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter("ca-ES-valencia");

        Assert.AreEqual("les dos en punt", converter.ConvertClockTime(new TimeOnly(2, 0)));
        Assert.AreEqual("les dos menys quart", converter.ConvertClockTime(new TimeOnly(1, 45)));
        Assert.AreEqual("la una en punt", converter.ConvertClockTime(new TimeOnly(1, 0)));
        Assert.AreEqual("dues hores", converter.Convert(new TimeOnly(2, 0)));
        Assert.AreEqual("dues hores", converter.Convert(TimeSpan.FromHours(2)));
        Assert.AreEqual("les dues en punt", NumberToStringConverter.GetConverter("CA").ConvertClockTime(new TimeOnly(2, 0)));
    }

    /// <summary>
    /// Cross-checks the ordinal plugins against the XML cardinals: for every <c>n &gt; 1</c> the
    /// ordinal must be <c>"ke" + cardinal(n)</c>. The plugins carry their own copy of the lexical
    /// stems (see <c>IndonesianOrdinalLanguageSpecifics</c>), so this detects any future divergence
    /// between the plugin and <c>ID.xml</c>/<c>MS.xml</c>.
    /// </summary>
    [TestMethod]
    [DataRow("ID")]
    [DataRow("MS")]
    public void IndonesianAndMalayOrdinals_StayInSyncWithXmlCardinals(string culture)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);

        Assert.AreEqual("pertama", converter.ConvertOrdinal(1L));
        foreach (long value in OrdinalSamples())
            Assert.AreEqual("ke" + converter.Convert(value), converter.ConvertOrdinal(value), $"{culture} {value}");
    }

    /// <summary>Ensures Malay ordinals never reintroduce Indonesian stems, and Indonesian ordinals never pick up Malay ones.</summary>
    [TestMethod]
    [DataRow("MS", "delapan,kedelapan,miliar,triliun,kuadriliun,kuintiliun")]
    [DataRow("ID", "lapan,kelapan,bilion,trilion,kuadrilion,kuintilion")]
    public void IndonesianAndMalayOrdinals_DoNotLeakForeignStems(string culture, string forbiddenWords)
    {
        NumberToStringConverter converter = NumberToStringConverter.GetConverter(culture);
        var forbidden = new HashSet<string>(forbiddenWords.Split(','), StringComparer.Ordinal);

        foreach (long value in OrdinalSamples())
        {
            string ordinal = converter.ConvertOrdinal(value);
            string? leaked = ordinal.Split(' ').FirstOrDefault(forbidden.Contains);
            Assert.IsNull(leaked, $"{culture} ordinal {value} = \"{ordinal}\"");
        }
    }

    /// <summary>Gets ordinal sample values covering every lexical stem and every configured scale.</summary>
    private static IEnumerable<long> OrdinalSamples()
    {
        for (long value = 2; value <= 120; value++)
            yield return value;
        foreach (long value in new long[]
        {
            180, 800, 888, 1_000, 8_008, 18_000, 80_000, 800_000, 1_000_000, 8_000_000,
            1_000_000_000, 8_000_000_000, 18_000_000_000, 1_000_000_000_000, 8_000_000_000_000,
            1_000_000_000_000_000, 8_000_000_000_000_000, 1_000_000_000_000_000_000,
            8_888_888_888_888_888_888, long.MaxValue,
        })
            yield return value;
    }

    /// <summary>Gets every <c>&lt;Culture&gt;</c> declaration of every built-in document.</summary>
    private static IEnumerable<(string Culture, string Document)> GetDeclaredCultures()
        => NumberToStringConverter.BuiltInConfigurations
            .SelectMany(source => XDocument.Parse(source.ConfigurationFactory())
                .Descendants(ConfigurationNamespace + "Culture")
                .Select(culture => (culture.Value.Trim(), source.Name)));

    /// <summary>Loads one built-in document by its registry name.</summary>
    private static XDocument LoadDocument(string name)
        => XDocument.Parse(NumberToStringConverter.BuiltInConfigurations.Single(source => source.Name == name).ConfigurationFactory());
}
