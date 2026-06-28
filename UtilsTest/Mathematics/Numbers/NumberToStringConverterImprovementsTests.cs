using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;
using Utils.Mathematics;
using Utils.Numerics;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// Tests for bug fixes and new features added to NumberToStringConverter.
/// </summary>
[TestClass]
public class NumberToStringConverterImprovementsTests
{
    // ─── A1 — Bug fix: double AdjustFunction ───────────────────────────────

    [TestMethod]
    public void AdjustFunction_CalledOnceForPositive()
    {
        int callCount = 0;
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            AdjustFunction = s => { callCount++; return s.ToUpperInvariant(); }
        };
        var converter = new NumberToStringConverter(options);

        converter.Convert(42);

        Assert.AreEqual(1, callCount, "AdjustFunction must be called exactly once for positive numbers.");
    }

    [TestMethod]
    public void AdjustFunction_NotCalledForNegative()
    {
        // For negative numbers the AdjustFunction is applied to the absolute value
        // before the minus template, not applied again afterwards.
        int callCount = 0;
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            AdjustFunction = s => { callCount++; return s; }
        };
        var converter = new NumberToStringConverter(options);

        string result = converter.Convert(-1);

        Assert.AreEqual(1, callCount, "AdjustFunction must be called exactly once for negative numbers.");
        Assert.IsTrue(result.StartsWith("minus ", StringComparison.Ordinal));
    }

    // ─── B1 — Convert(Number) exposed on interface ─────────────────────────

    [TestMethod]
    public void Interface_ConvertNumber_ReturnsExpectedText()
    {
        INumberToStringConverter converter = NumberToStringConverter.GetConverter("EN");
        var half = new Number(1, 2);

        string result = converter.Convert(half);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
    }

    // ─── B2 — MaxNumber validation ─────────────────────────────────────────

    [TestMethod]
    public void Convert_ThrowsWhenExceedingMaxNumber()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            MaxNumber = new BigInteger(999)
        };
        var converter = new NumberToStringConverter(options);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => converter.Convert(1000));
    }

    [TestMethod]
    public void Convert_ThrowsWhenNegativeExceedsMaxNumber()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            MaxNumber = new BigInteger(999)
        };
        var converter = new NumberToStringConverter(options);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => converter.Convert(-1000));
    }

    [TestMethod]
    public void Convert_DoesNotThrowAtExactMaxNumber()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            MaxNumber = new BigInteger(999)
        };
        var converter = new NumberToStringConverter(options);

        string result = converter.Convert(999);
        Assert.AreEqual("nine hundred and ninety-nine", result);
    }

    // ─── B4 — RegisterConfigurations ignores duplicates ────────────────────

    private const string MinimalXmlConfig = """
        <?xml version="1.0" encoding="utf-8" ?>
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
            <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point">
                <Culture>TEST-DUPLICATE-B4</Culture>
                <Groups>
                    <Group level="1">
                        <Digit digit="0" string="" /><Digit digit="1" string="one" />
                        <Digit digit="2" string="two" /><Digit digit="3" string="three" />
                        <Digit digit="4" string="four" /><Digit digit="5" string="five" />
                        <Digit digit="6" string="six" /><Digit digit="7" string="seven" />
                        <Digit digit="8" string="eight" /><Digit digit="9" string="nine" />
                    </Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false">
                    <StaticNames><Scale value="0" string=""/><Scale value="1" string="thousand"/></StaticNames>
                    <Suffixes><Suffix>on</Suffix></Suffixes>
                </NumberScale>
            </Language>
        </Numbers>
        """;

    [TestMethod]
    public void RegisterConfigurations_DuplicateCultureDoesNotThrow()
    {
        // Registering the same culture twice must not throw (TryAdd instead of Add).
        NumberToStringConverter.RegisterConfigurations([MinimalXmlConfig, MinimalXmlConfig]);
    }

    // ─── C1 — Ordinal conversion (English) ─────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_EN_Irregulars()
    {
        var converter = NumberToStringConverter.GetConverter("EN");

        (int number, string expected)[] cases = [
            (1, "first"),
            (2, "second"),
            (3, "third"),
            (5, "fifth"),
            (8, "eighth"),
            (9, "ninth"),
            (12, "twelfth"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(number), $"Ordinal of {number}");
    }

    [TestMethod]
    public void ConvertOrdinal_EN_RegularSuffix()
    {
        var converter = NumberToStringConverter.GetConverter("EN");

        (int number, string expected)[] cases = [
            (4, "fourth"),
            (6, "sixth"),
            (7, "seventh"),
            (10, "tenth"),
            (11, "eleventh"),
            (13, "thirteenth"),
            (100, "one hundredth"),
            (1000, "one thousandth"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(number), $"Ordinal of {number}");
    }

    [TestMethod]
    public void ConvertOrdinal_EN_CompoundNumbers()
    {
        var converter = NumberToStringConverter.GetConverter("EN");

        (int number, string expected)[] cases = [
            (21, "twenty-first"),
            (22, "twenty-second"),
            (23, "twenty-third"),
            (24, "twenty-fourth"),
            (30, "thirtieth"),
            (31, "thirty-first"),
            (101, "one hundred and first"),
            (1001, "one thousand, first"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(number), $"Ordinal of {number}");
    }

    [TestMethod]
    public void ConvertOrdinal_EN_Negative()
    {
        var converter = NumberToStringConverter.GetConverter("EN");

        Assert.AreEqual("minus first", converter.ConvertOrdinal(-1));
        Assert.AreEqual("minus twenty-first", converter.ConvertOrdinal(-21));
    }

    // ─── C1 — Ordinal conversion (French) ──────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_FR_FirstIsException()
    {
        var converter = NumberToStringConverter.GetConverter("FR");

        Assert.AreEqual("premier", converter.ConvertOrdinal(1));
    }

    [TestMethod]
    public void ConvertOrdinal_FR_RegularAndRules()
    {
        var converter = NumberToStringConverter.GetConverter("FR");

        (int number, string expected)[] cases = [
            (2, "deuxième"),
            (3, "troisième"),
            (4, "quatrième"),     // "quatre" strips trailing 'e'
            (5, "cinquième"),     // word rule
            (6, "sixième"),
            (8, "huitième"),
            (9, "neuvième"),      // word rule
            (10, "dixième"),
            (11, "onzième"),      // "onze" strips trailing 'e'
            (20, "vingtième"),
            (21, "vingt et unième"),  // "un" → "unième" via word rule
            (100, "centième"),
            (1000, "millième"),    // "mille" strips trailing 'e'
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(number), $"Ordinal FR de {number}");
    }

    // ─── C2 — Variants grammaticaux (genre) ───────────────────────────────

    [TestMethod]
    public void Convert_FR_Feminine_OneBecomesUne()
    {
        var converter = NumberToStringConverter.GetConverter("FR");

        Assert.AreEqual("une", converter.Convert(1, "gender=feminin"));
    }

    [TestMethod]
    public void Convert_FR_Feminine_CompoundWithEtUn()
    {
        var converter = NumberToStringConverter.GetConverter("FR");

        (int number, string expected)[] cases = [
            (21, "vingt et une"),
            (31, "trente et une"),
            (61, "soixante et une"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.Convert(number, "gender=feminin"), $"Féminin FR de {number}");
    }

    [TestMethod]
    public void Convert_FR_Masculine_IsDefault()
    {
        // "masculin" est la première valeur de la dimension → même résultat qu'avec ou sans paramètre
        var converter = NumberToStringConverter.GetConverter("FR");

        Assert.AreEqual("un", converter.Convert(1));
        Assert.AreEqual("un", converter.Convert(1, "gender=masculin"));
        Assert.AreEqual("vingt et un", converter.Convert(21));
        Assert.AreEqual("vingt et un", converter.Convert(21, "gender=masculin"));
    }

    [TestMethod]
    public void Convert_FR_Feminine_MillionNotAffected()
    {
        // "un million" ends with "million", not "un" → variant gender=feminin must NOT apply
        var converter = NumberToStringConverter.GetConverter("FR");

        Assert.AreEqual("un million", converter.Convert(1_000_000, "gender=feminin"));
    }

    [TestMethod]
    public void Convert_FR_Feminine_LargeCompound()
    {
        // "un million vingt et un" ends with "un" → only last word is replaced
        var converter = NumberToStringConverter.GetConverter("FR");

        Assert.AreEqual("un million vingt et une", converter.Convert(1_000_021, "gender=feminin"));
    }

    [TestMethod]
    public void Interface_ConvertWithVariants_Exists()
    {
        INumberToStringConverter converter = NumberToStringConverter.GetConverter("FR");

        string result = converter.Convert(new BigInteger(1), "gender=feminin");
        Assert.AreEqual("une", result);
    }

    [TestMethod]
    public void Convert_UnknownVariantDimension_FallsBackSilently()
    {
        // Une dimension inconnue est ignorée silencieusement → résultat identique à Convert(1)
        var converter = NumberToStringConverter.GetConverter("FR");

        string result = converter.Convert(1, "cas=inconnu");
        Assert.AreEqual("un", result);
    }

    // ─── C2d — Variants ES (género) ───────────────────────────────────────

    [TestMethod]
    public void Convert_ES_Femenino_OneBecomesUna()
    {
        var converter = NumberToStringConverter.GetConverter("ES");

        Assert.AreEqual("uno", converter.Convert(1));
        Assert.AreEqual("una", converter.Convert(1, "gender=femenino"));
    }

    [TestMethod]
    public void Convert_ES_Femenino_Hundreds()
    {
        var converter = NumberToStringConverter.GetConverter("ES");

        (int number, string expected)[] cases = [
            (200, "doscientas"),
            (300, "trescientas"),
            (400, "cuatrocientas"),
            (500, "quinientas"),
            (600, "seiscientas"),
            (700, "setecientas"),
            (800, "ochocientas"),
            (900, "novecientas"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.Convert(number, "gender=femenino"), $"ES femenino {number}");
    }

    // ─── C2e — Variants PT (género) ───────────────────────────────────────

    [TestMethod]
    public void Convert_PT_Feminino_UnitsAndComposites()
    {
        var converter = NumberToStringConverter.GetConverter("PT");

        Assert.AreEqual("um",         converter.Convert(1));
        Assert.AreEqual("uma",        converter.Convert(1,  "gender=feminino"));
        Assert.AreEqual("duas",       converter.Convert(2,  "gender=feminino"));
        Assert.AreEqual("vinte e uma", converter.Convert(21, "gender=feminino"));
        Assert.AreEqual("vinte e duas", converter.Convert(22, "gender=feminino"));
    }

    [TestMethod]
    public void Convert_PT_Feminino_Hundreds()
    {
        var converter = NumberToStringConverter.GetConverter("PT");

        Assert.AreEqual("duzentas",     converter.Convert(200, "gender=feminino"));
        Assert.AreEqual("trezentas",    converter.Convert(300, "gender=feminino"));
        Assert.AreEqual("quatrocentas", converter.Convert(400, "gender=feminino"));
        Assert.AreEqual("quinhentas",   converter.Convert(500, "gender=feminino"));
    }

    [TestMethod]
    public void Convert_PT_Feminino_CompoundWithHundreds()
    {
        var converter = NumberToStringConverter.GetConverter("PT");

        // Centaine + unité → les deux doivent passer au féminin
        Assert.AreEqual("duzentas e uma",  converter.Convert(201, "gender=feminino"));
        Assert.AreEqual("duzentas e duas", converter.Convert(202, "gender=feminino"));
    }

    // ─── C2f — Variants IT (genere) ───────────────────────────────────────

    [TestMethod]
    public void Convert_IT_Femminile_OneBecomesUna()
    {
        var converter = NumberToStringConverter.GetConverter("IT");

        Assert.AreEqual("uno", converter.Convert(1));
        Assert.AreEqual("una", converter.Convert(1, "gender=femminile"));
    }

    // ─── C2g — Variants CA (gènere) ───────────────────────────────────────

    [TestMethod]
    public void Convert_CA_Femeni_UnitsAndHyphenComposites()
    {
        var converter = NumberToStringConverter.GetConverter("CA");

        Assert.AreEqual("un",        converter.Convert(1));
        Assert.AreEqual("una",       converter.Convert(1,  "gender=femení"));
        Assert.AreEqual("dues",      converter.Convert(2,  "gender=femení"));
        // Les tirets sont des frontières de mot → LastWord fonctionne sur les composés
        Assert.AreEqual("vint-i-una",  converter.Convert(21, "gender=femení"));
        Assert.AreEqual("vint-i-dues", converter.Convert(22, "gender=femení"));
        Assert.AreEqual("trenta-una",  converter.Convert(31, "gender=femení"));
    }

    [TestMethod]
    public void Convert_CA_Femeni_TwoHundred()
    {
        var converter = NumberToStringConverter.GetConverter("CA");

        // Seule dos-cents a une forme féminine en catalan
        Assert.AreEqual("dues-centes",     converter.Convert(200, "gender=femení"));
        Assert.AreEqual("dues-centes una", converter.Convert(201, "gender=femení"));
    }

    // ─── C2h — Variants GL (xénero) ───────────────────────────────────────

    [TestMethod]
    public void Convert_GL_Feminino_UnitsAndComposites()
    {
        var converter = NumberToStringConverter.GetConverter("GL");

        Assert.AreEqual("un",           converter.Convert(1));
        Assert.AreEqual("unha",         converter.Convert(1,  "gender=feminino"));
        Assert.AreEqual("dúas",         converter.Convert(2,  "gender=feminino"));
        Assert.AreEqual("vinte e unha", converter.Convert(21, "gender=feminino"));
        Assert.AreEqual("vinte e dúas", converter.Convert(22, "gender=feminino"));
    }

    [TestMethod]
    public void Convert_GL_Feminino_TwoHundred()
    {
        var converter = NumberToStringConverter.GetConverter("GL");

        Assert.AreEqual("douscentas",      converter.Convert(200, "gender=feminino"));
        Assert.AreEqual("douscentas unha", converter.Convert(201, "gender=feminino"));
    }

    // ─── C2b — Variants FR-be/ch (genre) ─────────────────────────────────

    [TestMethod]
    public void Convert_FRbe_Feminine_OneBecomesUne()
    {
        var converter = NumberToStringConverter.GetConverter("FR-be");

        Assert.AreEqual("une", converter.Convert(1, "gender=feminin"));
    }

    [TestMethod]
    public void Convert_FRbe_Feminine_SeptanteEtUne()
    {
        var converter = NumberToStringConverter.GetConverter("FR-be");

        // FR-be utilise septante/huitante/nonante — la règle LastWord s'applique de la même façon
        (int number, string expected)[] cases = [
            (21, "vingt et une"),
            (71, "septante et une"),
            (81, "huitante et une"),
            (91, "nonante et une"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.Convert(number, "gender=feminin"), $"Féminin FR-be de {number}");
    }

    [TestMethod]
    public void Convert_FRbe_Masculine_IsDefault()
    {
        var converter = NumberToStringConverter.GetConverter("FR-be");

        Assert.AreEqual("un", converter.Convert(1));
        Assert.AreEqual("un", converter.Convert(1, "gender=masculin"));
        Assert.AreEqual("septante et un", converter.Convert(71));
        Assert.AreEqual("septante et un", converter.Convert(71, "gender=masculin"));
    }

    [TestMethod]
    public void Convert_FRbe_Feminine_MillionNotAffected()
    {
        var converter = NumberToStringConverter.GetConverter("FR-be");

        Assert.AreEqual("un million", converter.Convert(1_000_000, "gender=feminin"));
    }

    // ─── C2c — Variants DE (genus / kasus) ────────────────────────────────

    [TestMethod]
    public void Convert_DE_Default_IsEins()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        // Sans variante : forme de comptage (GermanSpecifics: "ein" → "eins")
        Assert.AreEqual("eins", converter.Convert(1));
        Assert.AreEqual("eins", converter.Convert(1, "genus=maskulin"));
        Assert.AreEqual("eins", converter.Convert(1, "kasus=nominativ"));
    }

    [TestMethod]
    public void Convert_DE_Feminin_Nominativ_IsEine()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        Assert.AreEqual("eine", converter.Convert(1, "genus=feminin"));
        Assert.AreEqual("eine", converter.Convert(1, "kasus=nominativ", "genus=feminin"));
        Assert.AreEqual("eine", converter.Convert(1, "kasus=akkusativ", "genus=feminin"));
    }

    [TestMethod]
    public void Convert_DE_Akkusativ_Maskulin_IsEinen()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        Assert.AreEqual("einen", converter.Convert(1, "kasus=akkusativ", "genus=maskulin"));
    }

    [TestMethod]
    public void Convert_DE_Dativ_IsEinem_OrEiner()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        Assert.AreEqual("einem", converter.Convert(1, "kasus=dativ", "genus=maskulin"));
        Assert.AreEqual("einem", converter.Convert(1, "kasus=dativ", "genus=neutrum"));
        Assert.AreEqual("einer", converter.Convert(1, "kasus=dativ", "genus=feminin"));
    }

    [TestMethod]
    public void Convert_DE_Genitiv_IsEines_OrEiner()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        Assert.AreEqual("eines", converter.Convert(1, "kasus=genitiv", "genus=maskulin"));
        Assert.AreEqual("eines", converter.Convert(1, "kasus=genitiv", "genus=neutrum"));
        Assert.AreEqual("einer", converter.Convert(1, "kasus=genitiv", "genus=feminin"));
    }

    [TestMethod]
    public void Convert_DE_CompoundNumbers_NotInflected()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        // Les composés allemands (einundzwanzig…) sont invariables :
        // "ein" y est soudé, il ne correspond pas au dernier mot.
        Assert.AreEqual("einundzwanzig", converter.Convert(21, "genus=feminin"));
        Assert.AreEqual("einundzwanzig", converter.Convert(21, "kasus=akkusativ", "genus=maskulin"));
    }

    [TestMethod]
    public void Convert_DE_VariantDimensions_ListsGenusAndKasus()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        var names = converter.VariantDimensions.Select(d => d.Name).ToList();
        CollectionAssert.Contains(names, "genus");
        CollectionAssert.Contains(names, "kasus");

        var genus = converter.VariantDimensions.First(d => d.Name == "genus");
        CollectionAssert.AreEqual(
            new[] { "maskulin", "feminin", "neutrum" },
            genus.Values.ToArray());

        var kasus = converter.VariantDimensions.First(d => d.Name == "kasus");
        CollectionAssert.AreEqual(
            new[] { "nominativ", "akkusativ", "dativ", "genitiv" },
            kasus.Values.ToArray());
    }

    // ─── C3 — Currency conversion ──────────────────────────────────────────

    [TestMethod]
    public void ConvertCurrency_EN_WholeAmount()
    {
        var converter = NumberToStringConverter.GetConverter("EN");
        var currency = new CurrencyDefinition
        {
            UnitSingular = "dollar",
            UnitPlural = "dollars",
            SubunitSingular = "cent",
            SubunitPlural = "cents",
            Connector = "and",
        };

        Assert.AreEqual("one dollar", converter.ConvertCurrency(1m, currency));
        Assert.AreEqual("two dollars", converter.ConvertCurrency(2m, currency));
        Assert.AreEqual("zero dollars", converter.ConvertCurrency(0m, currency));
    }

    [TestMethod]
    public void ConvertCurrency_EN_WithSubunits()
    {
        var converter = NumberToStringConverter.GetConverter("EN");
        var currency = new CurrencyDefinition
        {
            UnitSingular = "dollar",
            UnitPlural = "dollars",
            SubunitSingular = "cent",
            SubunitPlural = "cents",
            Connector = "and",
        };

        Assert.AreEqual("one dollar and fifty cents", converter.ConvertCurrency(1.50m, currency));
        Assert.AreEqual("twelve dollars and one cent", converter.ConvertCurrency(12.01m, currency));
    }

    [TestMethod]
    public void ConvertCurrency_EN_Negative()
    {
        var converter = NumberToStringConverter.GetConverter("EN");
        var currency = new CurrencyDefinition
        {
            UnitSingular = "dollar",
            UnitPlural = "dollars",
            SubunitSingular = "cent",
            SubunitPlural = "cents",
            Connector = "and",
        };

        Assert.AreEqual("minus five dollars and fifty cents", converter.ConvertCurrency(-5.50m, currency));
    }

    [TestMethod]
    public void ConvertCurrency_FR_Example()
    {
        var converter = NumberToStringConverter.GetConverter("FR");
        var currency = new CurrencyDefinition
        {
            UnitSingular = "euro",
            UnitPlural = "euros",
            SubunitSingular = "centime",
            SubunitPlural = "centimes",
            Connector = "et",
        };

        Assert.AreEqual("un euro", converter.ConvertCurrency(1m, currency));
        Assert.AreEqual("vingt et un euros et cinquante centimes", converter.ConvertCurrency(21.50m, currency));
    }

    // ─── D1 — RegisterLanguageSpecifics factory ────────────────────────────

    [TestMethod]
    public void RegisterLanguageSpecifics_OverridesReflectionLookup()
    {
        int callCount = 0;
        var stub = new StubLanguageSpecifics(() => callCount++);
        NumberToStringConverter.RegisterLanguageSpecifics(nameof(StubLanguageSpecifics), stub);

        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = stub,
        };
        var converter = new NumberToStringConverter(options);
        converter.Convert(1);

        Assert.AreEqual(1, callCount);
    }

    private sealed class StubLanguageSpecifics(Action onCall) : INumberToStringLanguageSpecifics
    {
        public string FinalizeWriting(string languageIdentifier, string text)
        {
            onCall();
            return text;
        }
    }
}
