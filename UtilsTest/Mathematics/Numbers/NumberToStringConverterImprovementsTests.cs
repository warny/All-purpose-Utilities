using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.Mathematics;
using Utils.NumberToString;
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
            Assert.AreEqual(expected, converter.ConvertOrdinal(number), $"FR ordinal of {number}");
    }

    // ─── C2 — Grammatical variants (gender) ───────────────────────────────

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
            Assert.AreEqual(expected, converter.Convert(number, "gender=feminin"), $"FR feminine of {number}");
    }

    [TestMethod]
    public void Convert_FR_Masculine_IsDefault()
    {
        // "masculin" is the first value of the dimension → same result with or without the parameter
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
        // An unknown dimension is silently ignored → same result as Convert(1)
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

        // Hundred + unit → both must switch to feminine
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
        // Hyphens are word boundaries → LastWord works on compound numbers
        Assert.AreEqual("vint-i-una",  converter.Convert(21, "gender=femení"));
        Assert.AreEqual("vint-i-dues", converter.Convert(22, "gender=femení"));
        Assert.AreEqual("trenta-una",  converter.Convert(31, "gender=femení"));
    }

    [TestMethod]
    public void Convert_CA_Femeni_TwoHundred()
    {
        var converter = NumberToStringConverter.GetConverter("CA");

        // Only dos-cents has a feminine form in Catalan
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

        // FR-be uses septante/huitante/nonante — the LastWord rule applies in the same way
        (int number, string expected)[] cases = [
            (21, "vingt et une"),
            (71, "septante et une"),
            (81, "huitante et une"),
            (91, "nonante et une"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.Convert(number, "gender=feminin"), $"FR-be feminine of {number}");
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

        // Without variant: counting form (GermanSpecifics EndsWith "ein" → "eins")
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

        // German compound numbers (einundzwanzig…) are invariable:
        // "ein" is fused into the compound and does not match the last word.
        Assert.AreEqual("einundzwanzig", converter.Convert(21, "genus=feminin"));
        Assert.AreEqual("einundzwanzig", converter.Convert(21, "kasus=akkusativ", "genus=maskulin"));
    }

    [TestMethod]
    public void Convert_DE_VariantDimensions_ListsGenderAndCase()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        // Canonical English names are exposed on Name; local-language aliases are on LocalName
        var names = converter.VariantDimensions.Select(d => d.Name).ToList();
        CollectionAssert.Contains(names, "gender");
        CollectionAssert.Contains(names, "case");

        var gender = converter.VariantDimensions.First(d => d.Name == "gender");
        Assert.AreEqual("genus", gender.LocalName);
        CollectionAssert.AreEqual(
            new[] { "maskulin", "feminin", "neutrum" },
            gender.Values.ToArray());

        var cas = converter.VariantDimensions.First(d => d.Name == "case");
        Assert.AreEqual("kasus", cas.LocalName);
        CollectionAssert.AreEqual(
            new[] { "nominativ", "akkusativ", "dativ", "genitiv" },
            cas.Values.ToArray());
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

    [TestMethod]
    public void ConvertCurrency_EN_SubunitRoundingCarry()
    {
        // Regression: when the fractional part rounds up to the subunit factor
        // (e.g. 1.999m → subunits = Math.Round(99.9) = 100), the carry must
        // propagate into the unit count. Before the fix the result was
        // "one dollar and one hundred cents".
        var converter = NumberToStringConverter.GetConverter("EN");
        var currency = new CurrencyDefinition
        {
            UnitSingular = "dollar",
            UnitPlural = "dollars",
            SubunitSingular = "cent",
            SubunitPlural = "cents",
            Connector = "and",
        };

        Assert.AreEqual("two dollars", converter.ConvertCurrency(1.999m, currency));
        Assert.AreEqual("one dollar",  converter.ConvertCurrency(0.995m, currency));
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

    // ─── C2i — Variants FI (sijamuoto: grammatical cases) ────────────────────

    [TestMethod]
    public void Convert_FI_Partitiivi_Units()
    {
        var converter = NumberToStringConverter.GetConverter("FI");

        // Nominative (default)
        Assert.AreEqual("yksi",  converter.Convert(1));
        Assert.AreEqual("kaksi", converter.Convert(2));
        // Partitive
        Assert.AreEqual("yhtä",      converter.Convert(1, "sijamuoto=partitiivi"));
        Assert.AreEqual("kahta",     converter.Convert(2, "sijamuoto=partitiivi"));
        Assert.AreEqual("kolmea",    converter.Convert(3, "sijamuoto=partitiivi"));
        Assert.AreEqual("neljää",    converter.Convert(4, "sijamuoto=partitiivi"));
        Assert.AreEqual("viittä",    converter.Convert(5, "sijamuoto=partitiivi"));
        Assert.AreEqual("kuutta",    converter.Convert(6, "sijamuoto=partitiivi"));
        Assert.AreEqual("seitsemää", converter.Convert(7, "sijamuoto=partitiivi"));
        Assert.AreEqual("kahdeksaa", converter.Convert(8, "sijamuoto=partitiivi"));
        Assert.AreEqual("yhdeksää",  converter.Convert(9, "sijamuoto=partitiivi"));
    }

    [TestMethod]
    public void Convert_FI_Partitiivi_ScaleAndCompound()
    {
        var converter = NumberToStringConverter.GetConverter("FI");

        // Scale words alone
        Assert.AreEqual("kymmentä",      converter.Convert(10,  "sijamuoto=partitiivi"));
        Assert.AreEqual("sataa",         converter.Convert(100, "sijamuoto=partitiivi"));
        // 1000: FI has no replacement "yksi tuhat"→"tuhat", so "yksi" stays
        Assert.AreEqual("yksi tuhatta",  converter.Convert(1000, "sijamuoto=partitiivi"));
        // Compounds: tens + unit
        Assert.AreEqual("kaksikymmentä yhtä", converter.Convert(21, "sijamuoto=partitiivi"));
        Assert.AreEqual("kaksikymmentä kahta", converter.Convert(22, "sijamuoto=partitiivi"));
        // Compound hundred (already partitive-compatible) + unit
        Assert.AreEqual("kaksisataa yhtä", converter.Convert(201, "sijamuoto=partitiivi"));
    }

    [TestMethod]
    public void Convert_FI_Partitiivi_Exceptions11To19()
    {
        var converter = NumberToStringConverter.GetConverter("FI");

        Assert.AreEqual("yhtätoista",      converter.Convert(11, "sijamuoto=partitiivi"));
        Assert.AreEqual("kahtatoista",     converter.Convert(12, "sijamuoto=partitiivi"));
        Assert.AreEqual("seitsemäätoista", converter.Convert(17, "sijamuoto=partitiivi"));
        Assert.AreEqual("kahdeksaatoista", converter.Convert(18, "sijamuoto=partitiivi"));
        Assert.AreEqual("yhdeksäätoista",  converter.Convert(19, "sijamuoto=partitiivi"));
        // In compound context: sata + exception
        Assert.AreEqual("kaksisataa yhtätoista", converter.Convert(211, "sijamuoto=partitiivi"));
    }

    [TestMethod]
    public void Convert_FI_Genetiivi_Units()
    {
        var converter = NumberToStringConverter.GetConverter("FI");

        Assert.AreEqual("yhden",    converter.Convert(1, "sijamuoto=genetiivi"));
        Assert.AreEqual("kahden",   converter.Convert(2, "sijamuoto=genetiivi"));
        Assert.AreEqual("kolmen",   converter.Convert(3, "sijamuoto=genetiivi"));
        Assert.AreEqual("neljän",   converter.Convert(4, "sijamuoto=genetiivi"));
        Assert.AreEqual("viiden",   converter.Convert(5, "sijamuoto=genetiivi"));
        Assert.AreEqual("kuuden",   converter.Convert(6, "sijamuoto=genetiivi"));
        // seitsemän/kahdeksan/yhdeksän: invariable in genitive
        Assert.AreEqual("seitsemän", converter.Convert(7, "sijamuoto=genetiivi"));
        Assert.AreEqual("kahdeksan", converter.Convert(8, "sijamuoto=genetiivi"));
        Assert.AreEqual("yhdeksän",  converter.Convert(9, "sijamuoto=genetiivi"));
    }

    [TestMethod]
    public void Convert_FI_Genetiivi_TensAndHundreds()
    {
        var converter = NumberToStringConverter.GetConverter("FI");

        // Compound tens
        Assert.AreEqual("kahdenkymmenen",    converter.Convert(20, "sijamuoto=genetiivi"));
        Assert.AreEqual("kolmenkymmenen",    converter.Convert(30, "sijamuoto=genetiivi"));
        Assert.AreEqual("seitsemänkymmenen", converter.Convert(70, "sijamuoto=genetiivi"));
        // Tens + unit
        Assert.AreEqual("kahdenkymmenen yhden",  converter.Convert(21, "sijamuoto=genetiivi"));
        Assert.AreEqual("kahdenkymmenen kahden", converter.Convert(22, "sijamuoto=genetiivi"));
        // Compound hundreds
        Assert.AreEqual("kahdensadan",   converter.Convert(200, "sijamuoto=genetiivi"));
        Assert.AreEqual("kolmensadan",   converter.Convert(300, "sijamuoto=genetiivi"));
        Assert.AreEqual("yhdeksänsadan", converter.Convert(900, "sijamuoto=genetiivi"));
        // Full compound: hundred + tens + unit
        Assert.AreEqual("kahdensadan kahdenkymmenen yhden",  converter.Convert(221, "sijamuoto=genetiivi"));
        Assert.AreEqual("kahdensadan kahdenkymmenen kahden", converter.Convert(222, "sijamuoto=genetiivi"));
    }

    [TestMethod]
    public void Convert_FI_Genetiivi_Exceptions11To16()
    {
        var converter = NumberToStringConverter.GetConverter("FI");

        Assert.AreEqual("yhdentoista",  converter.Convert(11, "sijamuoto=genetiivi"));
        Assert.AreEqual("kahdentoista", converter.Convert(12, "sijamuoto=genetiivi"));
        Assert.AreEqual("kuudentoista", converter.Convert(16, "sijamuoto=genetiivi"));
        // 17-19 invariable in genitive
        Assert.AreEqual("seitsemäntoista", converter.Convert(17, "sijamuoto=genetiivi"));
        Assert.AreEqual("kahdeksantoista", converter.Convert(18, "sijamuoto=genetiivi"));
        Assert.AreEqual("yhdeksäntoista",  converter.Convert(19, "sijamuoto=genetiivi"));
    }

    [TestMethod]
    public void Convert_FI_Nominatiivi_IsDefaultAndExplicit()
    {
        var converter = NumberToStringConverter.GetConverter("FI");

        Assert.AreEqual("yksi",          converter.Convert(1));
        Assert.AreEqual("yksi",          converter.Convert(1, "sijamuoto=nominatiivi"));
        Assert.AreEqual("kaksikymmentä", converter.Convert(20));
        Assert.AreEqual("kaksikymmentä", converter.Convert(20, "sijamuoto=nominatiivi"));
    }

    [TestMethod]
    public void Convert_FI_ListsVariantDimensions()
    {
        var converter = NumberToStringConverter.GetConverter("FI");
        var dims = converter.VariantDimensions.ToList();

        Assert.AreEqual(1, dims.Count);
        Assert.AreEqual("case", dims[0].Name);          // canonical English name
        Assert.AreEqual("sijamuoto", dims[0].LocalName); // Finnish local alias
        CollectionAssert.AreEqual(
            new[] { "nominatiivi", "partitiivi", "genetiivi" },
            dims[0].Values.ToArray()
        );
    }

    // ─── C3 — Ordinal pipeline: rules applied before AdjustFunction ────────

    [TestMethod]
    public void ConvertOrdinal_WordRulesMatchBeforeAdjustFunction()
    {
        // Regression: before the fix, AdjustFunction ran before ordinal rules,
        // so an uppercase AdjustFunction turned "twenty-one" into "TWENTY-ONE"
        // and the word rule "one"→"first" never matched, producing "TWENTY-ONEth".
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            AdjustFunction = s => s.ToUpperInvariant()
        };
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("TWENTY-FIRST", converter.ConvertOrdinal(21));
        Assert.AreEqual("THIRTIETH",    converter.ConvertOrdinal(30));
        Assert.AreEqual("FORTY-SECOND", converter.ConvertOrdinal(42));
    }

    // ─── C4 — Ordinal conversion (Belgian/Swiss French) ────────────────────

    [TestMethod]
    public void ConvertOrdinal_FRbe_FirstIsException()
    {
        var converter = NumberToStringConverter.GetConverter("FR-be");
        Assert.AreEqual("premier", converter.ConvertOrdinal(1));
    }

    [TestMethod]
    public void ConvertOrdinal_FRbe_BelgianNumbers()
    {
        var converter = NumberToStringConverter.GetConverter("FR-be");

        (int number, string expected)[] cases = [
            (2,  "deuxième"),
            (5,  "cinquième"),           // word rule
            (9,  "neuvième"),            // word rule
            (21, "vingt et unième"),     // "un" via word rule
            (70, "septantième"),         // Belgian 70
            (71, "septante et unième"),
            (80, "huitantième"),         // Belgian 80
            (81, "huitante et unième"),
            (90, "nonantième"),          // Belgian 90
            (91, "nonante et unième"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(number), $"FR-be ordinal of {number}");
    }

    // ─── C5 — Ordinal conversion (Dutch) ───────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_NL_FirstIsException()
    {
        var converter = NumberToStringConverter.GetConverter("NL");
        Assert.AreEqual("eerste", converter.ConvertOrdinal(1));
    }

    [TestMethod]
    public void ConvertOrdinal_NL_WordRulesForUnitsAndTeens()
    {
        var converter = NumberToStringConverter.GetConverter("NL");

        (int number, string expected)[] cases = [
            (2,  "tweede"),
            (3,  "derde"),
            (4,  "vierde"),
            (5,  "vijfde"),
            (6,  "zesde"),
            (7,  "zevende"),
            (8,  "achtste"),      // suffix "ste" on "acht" (no explicit rule needed)
            (9,  "negende"),
            (10, "tiende"),
            (11, "elfde"),
            (12, "twaalfde"),
            (13, "dertiende"),
            (19, "negentiende"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(number), $"NL ordinal of {number}");
    }

    [TestMethod]
    public void ConvertOrdinal_NL_SuffixSteForTensAndCompounds()
    {
        var converter = NumberToStringConverter.GetConverter("NL");

        (int number, string expected)[] cases = [
            (20,  "twintigste"),
            (21,  "eenentwintigste"),  // fused compound → suffix "ste" applies to whole
            (100, "honderdste"),
            (101, "honderd eerste"),   // "een" word rule in compound context
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(number), $"NL ordinal of {number}");
    }

    // ─── C6 — Ordinal conversion (Basque) ──────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_EU_FirstIsException()
    {
        var converter = NumberToStringConverter.GetConverter("EU");
        Assert.AreEqual("lehenengo", converter.ConvertOrdinal(1));
    }

    [TestMethod]
    public void ConvertOrdinal_EU_SuffixGarren()
    {
        var converter = NumberToStringConverter.GetConverter("EU");

        (int number, string expected)[] cases = [
            (2,    "bigarren"),
            (3,    "hirugarren"),
            (10,   "hamargarren"),
            (11,   "hamaikagarren"),       // exception 11=hamaika
            (20,   "hogeigarren"),
            (21,   "hogeita batgarren"),   // "bat" in compound → suffix on last word
            (1000, "milagarren"),
        ];

        foreach (var (number, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(number), $"EU ordinal of {number}");
    }

    // ─── D2 — INumberToStringConverter default implementations ─────────────

    [TestMethod]
    public void Interface_NewMembersHaveDefaultImplementations()
    {
        // A minimal implementation that only provides the original members must
        // still compile and work against the extended interface — new members
        // fall back to their default implementations.
        INumberToStringConverter converter = new MinimalConverter();

        // VariantDimensions → empty list
        Assert.AreEqual(0, converter.VariantDimensions.Count);

        // SupportsOrdinals → false by default
        Assert.IsFalse(converter.SupportsOrdinals);

        // Variant overloads → delegate to non-variant Convert, ignore parameters
        Assert.AreEqual("42", converter.Convert((BigInteger)42, "gender=feminin"));
        Assert.AreEqual("7",  converter.Convert(7,  "gender=feminin"));
        Assert.AreEqual("99", converter.Convert(99L, "gender=feminin"));

        // Convert(Number) → delegates to Convert(Numerator)
        Assert.AreEqual("3", converter.Convert(new Number(3, 2)));  // 3/2 → Numerator=3

        // ConvertOrdinal(int) → throws NotSupportedException
        Assert.ThrowsException<NotSupportedException>(() => converter.ConvertOrdinal(1));
        // ConvertOrdinal(long) in int range → delegates → same NotSupportedException
        Assert.ThrowsException<NotSupportedException>(() => converter.ConvertOrdinal(1L));
        // ConvertOrdinal(long) outside int range → OverflowException from checked cast
        Assert.ThrowsException<OverflowException>(() => converter.ConvertOrdinal((long)int.MaxValue + 1));
    }

    private sealed class MinimalConverter : INumberToStringConverter
    {
        public BigInteger? MaxNumber => null;
        public string Convert(BigInteger number) => number.ToString();
        public string Convert(int     number)    => number.ToString();
        public string Convert(long    number)    => number.ToString();
        public string Convert(decimal number)    => number.ToString();
    }

    private sealed class StubLanguageSpecifics(Action onCall) : INumberToStringLanguageSpecifics
    {
        public string FinalizeWriting(string languageIdentifier, string text)
        {
            onCall();
            return text;
        }
    }

    // ── C7 ─ Prefix ordinals ────────────────────────────────────────────
    [TestMethod]
    public void ConvertOrdinal_ZH_Prefix()
    {
        var zh = NumberToStringConverter.GetConverter("ZH");
        Assert.AreEqual("第一", zh.ConvertOrdinal(1));
        Assert.AreEqual("第二", zh.ConvertOrdinal(2));
        Assert.AreEqual("第十", zh.ConvertOrdinal(10));
        Assert.AreEqual("第一百", zh.ConvertOrdinal(100));
    }

    [TestMethod]
    public void ConvertOrdinal_JA_Prefix()
    {
        var ja = NumberToStringConverter.GetConverter("JA");
        Assert.AreEqual("第一", ja.ConvertOrdinal(1));
        Assert.AreEqual("第三", ja.ConvertOrdinal(3));
    }

    [TestMethod]
    public void ConvertOrdinal_KO_Prefix()
    {
        var ko = NumberToStringConverter.GetConverter("KO");
        Assert.AreEqual("제일", ko.ConvertOrdinal(1));
        Assert.AreEqual("제이", ko.ConvertOrdinal(2));
    }

    // ── C8 ─ Variant ordinals ────────────────────────────────────────────
    [TestMethod]
    public void ConvertOrdinal_ES_MasculinoDefault()
    {
        var es = NumberToStringConverter.GetConverter("ES");
        Assert.AreEqual("primero", es.ConvertOrdinal(1));
        Assert.AreEqual("segundo", es.ConvertOrdinal(2));
        Assert.AreEqual("décimo", es.ConvertOrdinal(10));
        Assert.AreEqual("vigésimo", es.ConvertOrdinal(20));
    }

    [TestMethod]
    public void ConvertOrdinal_ES_Femenino()
    {
        var es = NumberToStringConverter.GetConverter("ES");
        Assert.AreEqual("primera",  es.ConvertOrdinal(1,  "gender=femenino"));
        Assert.AreEqual("segunda",  es.ConvertOrdinal(2,  "gender=femenino"));
        Assert.AreEqual("décima",   es.ConvertOrdinal(10, "gender=femenino"));
        Assert.AreEqual("vigésima", es.ConvertOrdinal(20, "gender=femenino"));
    }

    [TestMethod]
    public void ConvertOrdinal_IT_Default()
    {
        var it = NumberToStringConverter.GetConverter("IT");
        Assert.AreEqual("primo",       it.ConvertOrdinal(1));
        Assert.AreEqual("secondo",     it.ConvertOrdinal(2));
        Assert.AreEqual("undicesimo",  it.ConvertOrdinal(11));
        Assert.AreEqual("ventesimo",   it.ConvertOrdinal(20));
        Assert.AreEqual("centesimo",   it.ConvertOrdinal(100));
        Assert.AreEqual("millesimo",   it.ConvertOrdinal(1000));
    }

    [TestMethod]
    public void ConvertOrdinal_IT_Femminile()
    {
        var it = NumberToStringConverter.GetConverter("IT");
        Assert.AreEqual("prima",       it.ConvertOrdinal(1,    "gender=femminile"));
        Assert.AreEqual("seconda",     it.ConvertOrdinal(2,    "gender=femminile"));
        Assert.AreEqual("undicesima",  it.ConvertOrdinal(11,   "gender=femminile"));
        Assert.AreEqual("ventesima",   it.ConvertOrdinal(20,   "gender=femminile"));
        Assert.AreEqual("millesima",   it.ConvertOrdinal(1000, "gender=femminile"));
    }

    // ── C8b — SupportsOrdinals property ──────────────────────────────────

    [TestMethod]
    public void SupportsOrdinals_TrueForLanguagesWithOrdinals()
    {
        foreach (var culture in new[] { "EN", "FR", "ES", "IT", "NL", "EU", "ZH", "JA", "KO", "DE", "HE", "EE", "CA", "GL", "PT", "RU", "FI", "PL", "AR", "HI", "EL", "WO" })
            Assert.IsTrue(NumberToStringConverter.GetConverter(culture).SupportsOrdinals, $"{culture}.SupportsOrdinals");
    }

    [TestMethod]
    public void SupportsOrdinals_FalseForLanguagesWithoutOrdinals()
    {
        // ZU (Zulu) is the only language without ordinal configuration
        Assert.IsFalse(NumberToStringConverter.GetConverter("ZU").SupportsOrdinals, "ZU.SupportsOrdinals");
    }

    [TestMethod]
    public void SupportsOrdinals_DefaultInterfaceReturnsFalse()
    {
        INumberToStringConverter converter = new MinimalConverter();
        Assert.IsFalse(converter.SupportsOrdinals);
    }

    // ── C8c — Ordinals DE ────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_DE_Irregulars()
    {
        var de = NumberToStringConverter.GetConverter("DE");

        (int n, string expected)[] cases = [
            (1, "erste"),
            (3, "dritte"),
            (7, "siebte"),
            (8, "achte"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, de.ConvertOrdinal(n), $"DE ordinal of {n}");
    }

    [TestMethod]
    public void ConvertOrdinal_DE_WordRulesAndSuffix()
    {
        var de = NumberToStringConverter.GetConverter("DE");

        (int n, string expected)[] cases = [
            (2,  "zweite"),
            (4,  "vierte"),
            (5,  "fünfte"),
            (6,  "sechste"),
            (9,  "neunte"),
            (10, "zehnte"),
            (11, "elfte"),
            (12, "zwölfte"),
            (13, "dreizehnte"),
            (19, "neunzehnte"),
            (20, "zwanzigste"),
            (21, "einundzwanzigste"),
            (30, "dreißigste"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, de.ConvertOrdinal(n), $"DE ordinal of {n}");
    }

    [TestMethod]
    public void ConvertOrdinal_DE_Compounds()
    {
        var de = NumberToStringConverter.GetConverter("DE");

        // "ein tausend" replacement is active → 1000 = "tausend"
        Assert.AreEqual("tausendste",  de.ConvertOrdinal(1000));
        // 1001 = "tausend ein" → last word "ein" → "erste"
        Assert.AreEqual("tausend erste", de.ConvertOrdinal(1001));
        // 1003 = "tausend drei" → last word "drei" → "dritte"
        Assert.AreEqual("tausend dritte", de.ConvertOrdinal(1003));
    }

    [TestMethod]
    public void ConvertOrdinal_DE_WithVariants_IrregularForm()
    {
        var de = NumberToStringConverter.GetConverter("DE");

        // schwach (weak) — used after a definite article; case order: nom/akk/dat/gen

        // maskulin schwach: only non-nominativ → "sten"-type endings
        Assert.AreEqual("erste",  de.ConvertOrdinal(1, "deklination=schwach", "genus=maskulin", "kasus=nominativ"));
        Assert.AreEqual("ersten", de.ConvertOrdinal(1, "deklination=schwach", "genus=maskulin", "kasus=akkusativ"));
        Assert.AreEqual("ersten", de.ConvertOrdinal(1, "deklination=schwach", "genus=maskulin", "kasus=dativ"));
        Assert.AreEqual("ersten", de.ConvertOrdinal(1, "deklination=schwach", "genus=maskulin", "kasus=genitiv"));

        // feminin schwach: nom=akk="erste", dat=gen="ersten"
        Assert.AreEqual("erste",  de.ConvertOrdinal(1, "deklination=schwach", "genus=feminin", "kasus=nominativ"));
        Assert.AreEqual("erste",  de.ConvertOrdinal(1, "deklination=schwach", "genus=feminin", "kasus=akkusativ"));
        Assert.AreEqual("ersten", de.ConvertOrdinal(1, "deklination=schwach", "genus=feminin", "kasus=dativ"));
        Assert.AreEqual("ersten", de.ConvertOrdinal(1, "deklination=schwach", "genus=feminin", "kasus=genitiv"));

        // neutrum schwach: nom=akk="erste" (same as feminin schwach)
        Assert.AreEqual("erste",  de.ConvertOrdinal(1, "deklination=schwach", "genus=neutrum", "kasus=nominativ"));
        Assert.AreEqual("erste",  de.ConvertOrdinal(1, "deklination=schwach", "genus=neutrum", "kasus=akkusativ"));

        // stark (strong) — used without an article

        // maskulin stark: nom="erster", akk=gen="ersten", dat="erstem"
        Assert.AreEqual("erster", de.ConvertOrdinal(1, "deklination=stark", "genus=maskulin", "kasus=nominativ"));
        Assert.AreEqual("ersten", de.ConvertOrdinal(1, "deklination=stark", "genus=maskulin", "kasus=akkusativ"));
        Assert.AreEqual("erstem", de.ConvertOrdinal(1, "deklination=stark", "genus=maskulin", "kasus=dativ"));
        Assert.AreEqual("ersten", de.ConvertOrdinal(1, "deklination=stark", "genus=maskulin", "kasus=genitiv"));

        // feminin stark: nom=akk="erste", dat=gen="erster"
        Assert.AreEqual("erste",  de.ConvertOrdinal(1, "deklination=stark", "genus=feminin", "kasus=nominativ"));
        Assert.AreEqual("erste",  de.ConvertOrdinal(1, "deklination=stark", "genus=feminin", "kasus=akkusativ"));
        Assert.AreEqual("erster", de.ConvertOrdinal(1, "deklination=stark", "genus=feminin", "kasus=dativ"));
        Assert.AreEqual("erster", de.ConvertOrdinal(1, "deklination=stark", "genus=feminin", "kasus=genitiv"));

        // neutrum stark: nom=akk="erstes", dat="erstem", gen="ersten"
        Assert.AreEqual("erstes", de.ConvertOrdinal(1, "deklination=stark", "genus=neutrum", "kasus=nominativ"));
        Assert.AreEqual("erstes", de.ConvertOrdinal(1, "deklination=stark", "genus=neutrum", "kasus=akkusativ"));
        Assert.AreEqual("erstem", de.ConvertOrdinal(1, "deklination=stark", "genus=neutrum", "kasus=dativ"));
        Assert.AreEqual("ersten", de.ConvertOrdinal(1, "deklination=stark", "genus=neutrum", "kasus=genitiv"));
    }

    [TestMethod]
    public void ConvertOrdinal_DE_WithVariants_RegularSuffix()
    {
        var de = NumberToStringConverter.GetConverter("DE");

        // 20 has no OrdinalException and no word rule → suffix="ste" from <Ordinals>
        // OrdinalVariants overrides the suffix per genus × deklination × kasus

        // schwach maskulin: only akk+dat+gen → "sten"; nom stays "ste"
        Assert.AreEqual("zwanzigste",  de.ConvertOrdinal(20, "deklination=schwach", "genus=maskulin", "kasus=nominativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=schwach", "genus=maskulin", "kasus=akkusativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=schwach", "genus=maskulin", "kasus=dativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=schwach", "genus=maskulin", "kasus=genitiv"));

        // schwach feminin: only dat+gen → "sten"; nom+akk stay "ste"
        Assert.AreEqual("zwanzigste",  de.ConvertOrdinal(20, "deklination=schwach", "genus=feminin", "kasus=nominativ"));
        Assert.AreEqual("zwanzigste",  de.ConvertOrdinal(20, "deklination=schwach", "genus=feminin", "kasus=akkusativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=schwach", "genus=feminin", "kasus=dativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=schwach", "genus=feminin", "kasus=genitiv"));

        // schwach neutrum: same as feminin
        Assert.AreEqual("zwanzigste",  de.ConvertOrdinal(20, "deklination=schwach", "genus=neutrum", "kasus=nominativ"));
        Assert.AreEqual("zwanzigste",  de.ConvertOrdinal(20, "deklination=schwach", "genus=neutrum", "kasus=akkusativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=schwach", "genus=neutrum", "kasus=dativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=schwach", "genus=neutrum", "kasus=genitiv"));

        // stark maskulin: nom → "ster"; akk+gen → "sten"; dat → "stem"
        Assert.AreEqual("zwanzigster", de.ConvertOrdinal(20, "deklination=stark", "genus=maskulin", "kasus=nominativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=stark", "genus=maskulin", "kasus=akkusativ"));
        Assert.AreEqual("zwanzigstem", de.ConvertOrdinal(20, "deklination=stark", "genus=maskulin", "kasus=dativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=stark", "genus=maskulin", "kasus=genitiv"));

        // stark feminin: nom+akk → "ste"; dat+gen → "ster"
        Assert.AreEqual("zwanzigste",  de.ConvertOrdinal(20, "deklination=stark", "genus=feminin", "kasus=nominativ"));
        Assert.AreEqual("zwanzigste",  de.ConvertOrdinal(20, "deklination=stark", "genus=feminin", "kasus=akkusativ"));
        Assert.AreEqual("zwanzigster", de.ConvertOrdinal(20, "deklination=stark", "genus=feminin", "kasus=dativ"));
        Assert.AreEqual("zwanzigster", de.ConvertOrdinal(20, "deklination=stark", "genus=feminin", "kasus=genitiv"));

        // stark neutrum: nom+akk → "stes"; dat → "stem"; gen → "sten"
        Assert.AreEqual("zwanzigstes", de.ConvertOrdinal(20, "deklination=stark", "genus=neutrum", "kasus=nominativ"));
        Assert.AreEqual("zwanzigstes", de.ConvertOrdinal(20, "deklination=stark", "genus=neutrum", "kasus=akkusativ"));
        Assert.AreEqual("zwanzigstem", de.ConvertOrdinal(20, "deklination=stark", "genus=neutrum", "kasus=dativ"));
        Assert.AreEqual("zwanzigsten", de.ConvertOrdinal(20, "deklination=stark", "genus=neutrum", "kasus=genitiv"));

        // Compound (21+): suffix variant also applies to word-appended forms
        Assert.AreEqual("einundzwanzigste",  de.ConvertOrdinal(21));
        Assert.AreEqual("einundzwanzigster", de.ConvertOrdinal(21, "deklination=stark", "genus=maskulin", "kasus=nominativ"));
        Assert.AreEqual("einundzwanzigsten", de.ConvertOrdinal(21, "deklination=stark", "genus=maskulin", "kasus=akkusativ"));
    }

    // ── C8d — Ordinals HE ────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_HE_MasculineDefault()
    {
        var he = NumberToStringConverter.GetConverter("HE");

        Assert.AreEqual("ראשון",  he.ConvertOrdinal(1));
        Assert.AreEqual("שני",    he.ConvertOrdinal(2));
        Assert.AreEqual("שלישי",  he.ConvertOrdinal(3));
        Assert.AreEqual("עשירי",  he.ConvertOrdinal(10));
    }

    [TestMethod]
    public void ConvertOrdinal_HE_Nekeva()
    {
        var he = NumberToStringConverter.GetConverter("HE");

        Assert.AreEqual("ראשונה",  he.ConvertOrdinal(1,  "gender=nekeva"));
        Assert.AreEqual("שנייה",   he.ConvertOrdinal(2,  "gender=nekeva"));
        Assert.AreEqual("שלישית",  he.ConvertOrdinal(3,  "gender=nekeva"));
        Assert.AreEqual("עשירית",  he.ConvertOrdinal(10, "gender=nekeva"));
    }

    [TestMethod]
    public void ConvertOrdinal_HE_AboveTenFallsBackToCardinal()
    {
        var he = NumberToStringConverter.GetConverter("HE");
        // No ordinal config above 10 → cardinal returned
        Assert.AreEqual("עשרים", he.ConvertOrdinal(20));
    }

    // ── C8e — Ordinals EE ────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_EE_FirstIsIrregular()
    {
        var ee = NumberToStringConverter.GetConverter("EE");
        Assert.AreEqual("gbãtõ", ee.ConvertOrdinal(1));
    }

    [TestMethod]
    public void ConvertOrdinal_EE_OthersGetPrefix()
    {
        var ee = NumberToStringConverter.GetConverter("EE");
        Assert.AreEqual("etsõ eve",  ee.ConvertOrdinal(2));
        Assert.AreEqual("etsõ eto",  ee.ConvertOrdinal(3));
        Assert.AreEqual("etsõ asea", ee.ConvertOrdinal(9));
    }

    // ── C8f — Ordinals CA ────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_CA_MasculiDefault()
    {
        var ca = NumberToStringConverter.GetConverter("CA");

        (int n, string expected)[] cases = [
            (1,  "primer"),
            (2,  "segon"),
            (3,  "tercer"),
            (4,  "quart"),
            (5,  "cinquè"),
            (9,  "novè"),
            (10, "desè"),
            (11, "onzè"),
            (19, "dinovè"),
            (20, "vintè"),
            (30, "trentè"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, ca.ConvertOrdinal(n), $"CA ordinal of {n}");
    }

    [TestMethod]
    public void ConvertOrdinal_CA_Femeni()
    {
        var ca = NumberToStringConverter.GetConverter("CA");

        Assert.AreEqual("primera",  ca.ConvertOrdinal(1,  "gender=femení"));
        Assert.AreEqual("quarta",   ca.ConvertOrdinal(4,  "gender=femení"));
        Assert.AreEqual("cinquena", ca.ConvertOrdinal(5,  "gender=femení"));
        Assert.AreEqual("dinovena", ca.ConvertOrdinal(19, "gender=femení"));
        Assert.AreEqual("vintena",  ca.ConvertOrdinal(20, "gender=femení"));
        Assert.AreEqual("trentena", ca.ConvertOrdinal(30, "gender=femení"));
    }

    [TestMethod]
    public void ConvertOrdinal_CA_Femeni_Compound()
    {
        var ca = NumberToStringConverter.GetConverter("CA");
        // 21 = "vint-i-un" → femení → "vint-i-una" → suffix "ena" - trailing "a" = "unena"
        Assert.AreEqual("vint-i-unena", ca.ConvertOrdinal(21, "gender=femení"));
        // 22 = "vint-i-dos" → femení → "vint-i-dues" → word rule "dues"→"dosena"
        Assert.AreEqual("vint-i-dosena", ca.ConvertOrdinal(22, "gender=femení"));
    }

    // ── C8g — Ordinals GL ────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_GL_MasculinoDefault()
    {
        var gl = NumberToStringConverter.GetConverter("GL");

        (int n, string expected)[] cases = [
            (1,  "primeiro"),
            (6,  "sexto"),
            (10, "décimo"),
            (12, "duodécimo"),
            (20, "vixésimo"),
            (30, "trixésimo"),
            (100, "centésimo"),
            (1000, "milésimo"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, gl.ConvertOrdinal(n), $"GL ordinal of {n}");
    }

    [TestMethod]
    public void ConvertOrdinal_GL_Feminino()
    {
        var gl = NumberToStringConverter.GetConverter("GL");

        Assert.AreEqual("primeira",  gl.ConvertOrdinal(1,  "gender=feminino"));
        Assert.AreEqual("décima",    gl.ConvertOrdinal(10, "gender=feminino"));
        Assert.AreEqual("vixésima",  gl.ConvertOrdinal(20, "gender=feminino"));
        Assert.AreEqual("centésima", gl.ConvertOrdinal(100, "gender=feminino"));
        Assert.AreEqual("milésima",  gl.ConvertOrdinal(1000, "gender=feminino"));
    }

    [TestMethod]
    public void ConvertOrdinal_GL_Feminino_Compound()
    {
        var gl = NumberToStringConverter.GetConverter("GL");
        // 21 = "vinte e un" → femení cardinal → "vinte e unha" → ordinal "unha"→"primeira"
        Assert.AreEqual("vinte e primeira", gl.ConvertOrdinal(21, "gender=feminino"));
        // 22 = "vinte e dous" → femení cardinal → "vinte e dúas" → ordinal "dúas"→"segunda"
        Assert.AreEqual("vinte e segunda",  gl.ConvertOrdinal(22, "gender=feminino"));
        // 23 = "vinte e tres" → not transformed by cardinal → ordinal "tres"→"terceira"
        Assert.AreEqual("vinte e terceira", gl.ConvertOrdinal(23, "gender=feminino"));
    }

    // ── C8h — Ordinals PT ────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_PT_MasculinoDefault()
    {
        var pt = NumberToStringConverter.GetConverter("PT");

        (int n, string expected)[] cases = [
            (1,  "primeiro"),
            (9,  "nono"),
            (10, "décimo"),
            (11, "décimo primeiro"),
            (19, "décimo nono"),
            (20, "vigésimo"),
            (30, "trigésimo"),
            (100, "centésimo"),
            (1000, "milésimo"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, pt.ConvertOrdinal(n), $"PT ordinal of {n}");
    }

    [TestMethod]
    public void ConvertOrdinal_PT_Feminino()
    {
        var pt = NumberToStringConverter.GetConverter("PT");

        Assert.AreEqual("primeira",       pt.ConvertOrdinal(1,  "gender=feminino"));
        Assert.AreEqual("nona",           pt.ConvertOrdinal(9,  "gender=feminino"));
        Assert.AreEqual("décima",         pt.ConvertOrdinal(10, "gender=feminino"));
        Assert.AreEqual("décima primeira", pt.ConvertOrdinal(11, "gender=feminino"));
        Assert.AreEqual("décima nona",    pt.ConvertOrdinal(19, "gender=feminino"));
        Assert.AreEqual("vigésima",       pt.ConvertOrdinal(20, "gender=feminino"));
        Assert.AreEqual("centésima",      pt.ConvertOrdinal(100, "gender=feminino"));
        Assert.AreEqual("milésima",       pt.ConvertOrdinal(1000, "gender=feminino"));
    }

    [TestMethod]
    public void ConvertOrdinal_PT_Feminino_Compound()
    {
        var pt = NumberToStringConverter.GetConverter("PT");
        // 21 = "vinte e um" → femení cardinal → "vinte e uma" → ordinal "uma"→"primeira"
        Assert.AreEqual("vinte e primeira", pt.ConvertOrdinal(21, "gender=feminino"));
        // 22 = "vinte e dois" → femení → "vinte e duas" → ordinal "duas"→"segunda"
        Assert.AreEqual("vinte e segunda",  pt.ConvertOrdinal(22, "gender=feminino"));
        // 23 = "vinte e três" → not transformed → ordinal "três"→"terceira"
        Assert.AreEqual("vinte e terceira", pt.ConvertOrdinal(23, "gender=feminino"));
    }

    // ── C9 ─ IOrdinalLanguageSpecifics plugin ─────────────────────────
    [TestMethod]
    public void ConvertOrdinal_Plugin_OverridesXmlPipeline()
    {
        // Build a converter with a plugin that returns "ORDINAL_<n>" for any number > 0
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = new OrdinalPluginSpecifics()
        };
        var conv = new NumberToStringConverter(options);

        Assert.AreEqual("ORDINAL_1",  conv.ConvertOrdinal(1));
        Assert.AreEqual("ORDINAL_42", conv.ConvertOrdinal(42));
        // The plugin returns false for 0, so the XML pipeline handles it → "zeroth"
        Assert.AreEqual("zeroth", conv.ConvertOrdinal(0));
    }

    // ─── FR — Ordinal variants (gender=feminin) ───────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_FR_Feminine_PremiereBecomesPremiere()
    {
        var converter = NumberToStringConverter.GetConverter("FR-fr");
        Assert.AreEqual("première", converter.ConvertOrdinal(1, "gender=feminin"));
        Assert.AreEqual("premier",  converter.ConvertOrdinal(1));
    }

    // ─── RU — Ordinals ───────────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_RU_AppliesRulesAndSuffix()
    {
        var converter = NumberToStringConverter.GetConverter("RU");
        (int n, string expected)[] cases =
        [
            (1,    "первый"),          // exception
            (2,    "второй"),          // word rule: два → второй
            (3,    "третий"),          // word rule: три → третий
            (4,    "четвёртый"),       // word rule: четыре → четвёртый
            (5,    "пятый"),           // suffix: пять - ь + ый
            (6,    "шестой"),          // word rule: шесть → шестой
            (7,    "седьмой"),         // word rule: семь → седьмой
            (8,    "восьмой"),         // word rule: восемь → восьмой
            (9,    "девятый"),         // suffix: девять - ь + ый
            (10,   "десятый"),         // suffix: десять - ь + ый
            (11,   "одиннадцатый"),    // suffix: одиннадцать - ь + ый
            (20,   "двадцатый"),       // suffix: двадцать - ь + ый
            (21,   "двадцать первый"), // compound: last word один → первый
            (40,   "сороковой"),       // word rule: сорок → сороковой
            (100,  "сотый"),           // word rule: сто → сотый
            (1000, "тысячный"),        // word rule: тысяча → тысячный
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(n), $"RU ordinal of {n}");
    }

    // ─── EN — ConvertYear ────────────────────────────────────────────────────

    [TestMethod]
    public void ConvertYear_EN_SplitRanges()
    {
        var converter = NumberToStringConverter.GetConverter("EN");
        (int year, string expected)[] cases =
        [
            (1984, "nineteen eighty-four"),  // range 1100-1999, remainder ≥ 10
            (1900, "nineteen hundred"),       // range 1100-1999, remainder = 0
            (1905, "nineteen oh five"),       // range 1100-1999, remainder 1-9
            (1100, "eleven hundred"),         // début de la plage 1100-1999
            (2024, "twenty twenty-four"),     // range 2010-2099
            (2010, "twenty ten"),             // début de la plage 2010-2099
            (2000, "two thousand"),             // hors plage → Convert(2000)
            (2005, "two thousand, five"),      // entre les deux plages → Convert(2005)
            (1066, "one thousand, sixty-six"), // sous la plage → Convert(1066)
        ];
        foreach (var (year, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertYear(year), $"EN year {year}");
    }

    // ─── EL — Ordinals ───────────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_EL_WordRulesAndVariants()
    {
        var converter = NumberToStringConverter.GetConverter("EL");
        Assert.AreEqual("πρώτος",    converter.ConvertOrdinal(1));
        Assert.AreEqual("δεύτερος",  converter.ConvertOrdinal(2));
        Assert.AreEqual("τρίτος",    converter.ConvertOrdinal(3));
        Assert.AreEqual("δέκατος",   converter.ConvertOrdinal(10));
        Assert.AreEqual("ενδέκατος", converter.ConvertOrdinal(11));
        Assert.AreEqual("εικοστός",  converter.ConvertOrdinal(20));
        Assert.AreEqual("εκατοστός", converter.ConvertOrdinal(100));
        Assert.AreEqual("πρώτη",     converter.ConvertOrdinal(1,  "gender=θηλυκό"));
        Assert.AreEqual("δεύτερη",   converter.ConvertOrdinal(2,  "gender=θηλυκό"));
        Assert.AreEqual("πρώτο",     converter.ConvertOrdinal(1,  "gender=ουδέτερο"));
        Assert.AreEqual("εικοστή",   converter.ConvertOrdinal(20, "gender=θηλυκό"));
    }

    // ─── FI — Ordinals ───────────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_FI_WordRules()
    {
        var converter = NumberToStringConverter.GetConverter("FI");
        (int n, string expected)[] cases =
        [
            (1,   "ensimmäinen"),
            (2,   "toinen"),
            (3,   "kolmas"),
            (4,   "neljäs"),
            (5,   "viides"),
            (10,  "kymmenes"),
            (11,  "yhdestoista"),
            (20,  "kahdeskymmenes"),
            (100, "sadas"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(n), $"FI ordinal of {n}");
    }

    // ─── HI — Ordinals ───────────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_HI_SuffixAndExceptions()
    {
        var converter = NumberToStringConverter.GetConverter("HI");
        Assert.AreEqual("पहला",       converter.ConvertOrdinal(1));
        Assert.AreEqual("दूसरा",      converter.ConvertOrdinal(2));
        Assert.AreEqual("तीसरा",      converter.ConvertOrdinal(3));
        Assert.AreEqual("चौथा",       converter.ConvertOrdinal(4));
        Assert.AreEqual("पांचवाँ",    converter.ConvertOrdinal(5));
        Assert.AreEqual("छठा",        converter.ConvertOrdinal(6));
        Assert.AreEqual("सातवाँ",     converter.ConvertOrdinal(7));
        Assert.AreEqual("ग्यारहवाँ",  converter.ConvertOrdinal(11));
        Assert.AreEqual("बीसवाँ",     converter.ConvertOrdinal(20));
    }

    // ─── PL — Ordinals ───────────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_PL_WordRules()
    {
        var converter = NumberToStringConverter.GetConverter("PL");
        (int n, string expected)[] cases =
        [
            (1,   "pierwszy"),
            (2,   "drugi"),
            (3,   "trzeci"),
            (5,   "piąty"),
            (10,  "dziesiąty"),
            (11,  "jedenasty"),
            (20,  "dwudziesty"),
            (21,  "dwadzieścia pierwszy"), // limitation XML : seul le dernier mot est transformé
            (100, "setny"),
            (1000, "tysięczny"),
        ];
        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(n), $"PL ordinal of {n}");
    }

    // ─── AR — Ordinals ───────────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_AR_Exceptions()
    {
        var converter = NumberToStringConverter.GetConverter("AR");
        Assert.AreEqual("أول",  converter.ConvertOrdinal(1));
        Assert.AreEqual("ثانٍ", converter.ConvertOrdinal(2));
        Assert.AreEqual("ثالث", converter.ConvertOrdinal(3));
        Assert.AreEqual("عاشر", converter.ConvertOrdinal(10));
    }

    // ─── WO — Ordinals ───────────────────────────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_WO_SuffixAndException()
    {
        var converter = NumberToStringConverter.GetConverter("WO");
        Assert.AreEqual("bu njëkk", converter.ConvertOrdinal(1));
        Assert.AreEqual("ñaarël",   converter.ConvertOrdinal(2));
        Assert.AreEqual("fukkël",   converter.ConvertOrdinal(10));
    }

    // ── C10 — ConvertOrdinal(long) overload ─────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_Long_SmallNumber_SameAsInt()
    {
        var converter = NumberToStringConverter.GetConverter("EN");

        Assert.AreEqual(converter.ConvertOrdinal(1),   converter.ConvertOrdinal(1L));
        Assert.AreEqual(converter.ConvertOrdinal(21),  converter.ConvertOrdinal(21L));
        Assert.AreEqual(converter.ConvertOrdinal(100), converter.ConvertOrdinal(100L));
    }

    [TestMethod]
    public void ConvertOrdinal_Long_AboveIntMax_UsesXmlPipeline()
    {
        // Values above int.MaxValue bypass the plugin and go through the XML pipeline
        var converter = NumberToStringConverter.GetConverter("EN");
        long n = (long)int.MaxValue + 2;   // 2147483649 = "two billion, one hundred forty-seven million, four hundred eighty-three thousand, six hundred forty-nine"

        string result = converter.ConvertOrdinal(n);

        // The ordinal suffix "th" applies to last word; must not throw
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
    }

    [TestMethod]
    public void ConvertOrdinal_Long_Negative()
    {
        var converter = NumberToStringConverter.GetConverter("EN");

        Assert.AreEqual("minus first", converter.ConvertOrdinal(-1L));
        Assert.AreEqual(converter.ConvertOrdinal(-21), converter.ConvertOrdinal(-21L));
    }

    [TestMethod]
    public void ConvertOrdinal_Long_WithVariants()
    {
        var converter = NumberToStringConverter.GetConverter("ES");

        Assert.AreEqual("primera", converter.ConvertOrdinal(1L, "gender=femenino"));
        Assert.AreEqual("primera", converter.ConvertOrdinal(1,  "gender=femenino"));
    }

    // ── C11 — YearFormat DE ─────────────────────────────────────────────────

    [TestMethod]
    public void ConvertYear_DE_SplitRange()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        (int year, string expected)[] cases =
        [
            (1984, "neunzehn vierundachtzig"),   // remainder ≥ 10
            (1900, "neunzehn hundert"),           // remainder = 0 → hundredWord
            (1100, "elf hundert"),                // 11 = "elf" (exception)
            (1999, "neunzehn neunundneunzig"),    // top of the range
        ];

        foreach (var (year, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertYear(year), $"DE year {year}");
    }

    [TestMethod]
    public void ConvertYear_DE_OutsideRangeFallsBackToConvert()
    {
        var converter = NumberToStringConverter.GetConverter("DE");

        // 1099 and 2000 are outside [1100, 1999] → regular Convert
        Assert.AreEqual(converter.Convert(1099), converter.ConvertYear(1099));
        Assert.AreEqual(converter.Convert(2000), converter.ConvertYear(2000));
    }

    // ── C12 — YearFormat NL ─────────────────────────────────────────────────

    [TestMethod]
    public void ConvertYear_NL_SplitRange()
    {
        var converter = NumberToStringConverter.GetConverter("NL");

        (int year, string expected)[] cases =
        [
            (1984, "negentien vierentachtig"),    // remainder ≥ 10
            (1900, "negentien honderd"),           // remainder = 0 → hundredWord
            (1100, "elf honderd"),                 // 11 = "elf" (exception)
        ];

        foreach (var (year, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertYear(year), $"NL year {year}");
    }

    [TestMethod]
    public void ConvertYear_NL_OutsideRangeFallsBackToConvert()
    {
        var converter = NumberToStringConverter.GetConverter("NL");

        Assert.AreEqual(converter.Convert(1099), converter.ConvertYear(1099));
        Assert.AreEqual(converter.Convert(2000), converter.ConvertYear(2000));
    }

    // ── C13 — Ordinal HI feminine variant ───────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_HI_StriiVariant_Exceptions()
    {
        var converter = NumberToStringConverter.GetConverter("HI");

        Assert.AreEqual("पहली",   converter.ConvertOrdinal(1, "gender=strī"));
        Assert.AreEqual("दूसरी",  converter.ConvertOrdinal(2, "gender=strī"));
        Assert.AreEqual("तीसरी",  converter.ConvertOrdinal(3, "gender=strī"));
        Assert.AreEqual("चौथी",   converter.ConvertOrdinal(4, "gender=strī"));
    }

    [TestMethod]
    public void ConvertOrdinal_HI_StriiVariant_WordRuleAndSuffix()
    {
        var converter = NumberToStringConverter.GetConverter("HI");

        // 6: word rule छह→छठी (overrides the base छह→छठा)
        Assert.AreEqual("छठी",      converter.ConvertOrdinal(6, "gender=strī"));
        // 5, 7+ : suffix वीं instead of वाँ
        Assert.AreEqual("पांचवीं",  converter.ConvertOrdinal(5, "gender=strī"));
        Assert.AreEqual("सातवीं",   converter.ConvertOrdinal(7, "gender=strī"));
    }

    [TestMethod]
    public void ConvertOrdinal_HI_DefaultMasculineUnchanged()
    {
        var converter = NumberToStringConverter.GetConverter("HI");

        Assert.AreEqual("पहला",    converter.ConvertOrdinal(1));
        Assert.AreEqual("छठा",     converter.ConvertOrdinal(6));
        Assert.AreEqual("सातवाँ",  converter.ConvertOrdinal(7));
    }

    // ── C14 — Ordinal AR feminine variant ───────────────────────────────────

    [TestMethod]
    public void ConvertOrdinal_AR_MuannathaVariant()
    {
        var converter = NumberToStringConverter.GetConverter("AR");

        (int n, string expected)[] cases =
        [
            (1,  "أولى"),
            (2,  "ثانية"),
            (3,  "ثالثة"),
            (4,  "رابعة"),
            (5,  "خامسة"),
            (6,  "سادسة"),
            (7,  "سابعة"),
            (8,  "ثامنة"),
            (9,  "تاسعة"),
            (10, "عاشرة"),
        ];

        foreach (var (n, expected) in cases)
            Assert.AreEqual(expected, converter.ConvertOrdinal(n, "gender=muʾannath"), $"AR muʾannath ordinal of {n}");
    }

    [TestMethod]
    public void ConvertOrdinal_AR_DefaultMasculineUnchanged()
    {
        var converter = NumberToStringConverter.GetConverter("AR");

        Assert.AreEqual("أول",  converter.ConvertOrdinal(1));
        Assert.AreEqual("ثانٍ", converter.ConvertOrdinal(2));
        Assert.AreEqual("عاشر", converter.ConvertOrdinal(10));
    }

    // ── C15a — Variant without selector throws ───────────────────────────────

    [TestMethod]
    public void ReadConfiguration_VariantWithoutSelector_Throws()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8" ?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *">
                <Culture>TEST-BAD-VARIANT</Culture>
                <Groups><Group level="1"><Digit digit="1" string="one" /></Group></Groups>
                <NumberScale firstLetterUpperCase="false">
                  <StaticNames><Scale value="0" string="" /></StaticNames>
                </NumberScale>
                <Variants>
                  <Dimension name="case" values="nom,acc" />
                  <Variant type="case">
                    <Replacement oldValue="one" newValue="ONE" scope="LastWord" />
                  </Variant>
                </Variants>
              </Language>
            </Numbers>
            """;

        Assert.ThrowsException<InvalidOperationException>(() =>
            NumberToStringConverter.ReadConfiguration(xml));
    }

    [TestMethod]
    public void ReadConfiguration_OrdinalVariantWithoutSelector_Throws()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8" ?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *">
                <Culture>TEST-BAD-ORDVARIANT</Culture>
                <Groups><Group level="1"><Digit digit="1" string="one" /></Group></Groups>
                <NumberScale firstLetterUpperCase="false">
                  <StaticNames><Scale value="0" string="" /></StaticNames>
                </NumberScale>
                <Ordinals suffix="th">
                  <OrdinalVariants>
                    <Variant type="case">
                      <OrdinalException value="1" string="first" />
                    </Variant>
                  </OrdinalVariants>
                </Ordinals>
              </Language>
            </Numbers>
            """;

        Assert.ThrowsException<InvalidOperationException>(() =>
            NumberToStringConverter.ReadConfiguration(xml));
    }

    // ── C15 — Multi-value variant syntax (values="a,b,c") ───────────────────

    private const string MultiValueTestConfig = """
        <?xml version="1.0" encoding="utf-8" ?>
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
          <Language groupSize="3" separator=" " groupSeparator="" zero="nul" minus="minus *">
            <Culture>TEST-MV</Culture>
            <Groups>
              <Group level="1">
                <Digit digit="0" string="" />
                <Digit digit="1" string="één" />
                <Digit digit="2" string="twee" />
                <Digit digit="3" string="drie" />
              </Group>
              <Group level="2">
                <Digit digit="0" string="" buildString="*" />
                <Digit digit="1" string="tien" buildString="*tien" />
                <Digit digit="2" string="twintig" buildString="*entwintig" />
              </Group>
              <Group level="3">
                <Digit digit="0" string="" buildString="*" />
                <Digit digit="1" string="honderd" buildString="honderd *" />
              </Group>
            </Groups>
            <NumberScale firstLetterUpperCase="false">
              <StaticNames>
                <Scale value="0" string="" />
                <Scale value="1" string="duizend" />
              </StaticNames>
            </NumberScale>
            <Ordinals suffix="de">
              <OrdinalException value="1" string="eerste" />
              <OrdinalVariants>
                <Variant type="case" values="genitief,datief,accusatief" suffix="den">
                  <OrdinalException value="1" string="eersten" />
                </Variant>
              </OrdinalVariants>
            </Ordinals>
            <Variants>
              <Dimension name="case" values="nominatief,genitief,datief,accusatief" />
              <Variant type="case" values="genitief,datief">
                <Replacement oldValue="één" newValue="ener" scope="LastWord" />
              </Variant>
              <Variant type="case" variant="accusatief">
                <Replacement oldValue="één" newValue="enen" scope="LastWord" />
              </Variant>
            </Variants>
          </Language>
        </Numbers>
        """;

    [TestMethod]
    public void OrdinalVariant_MultiValue_SameExceptionForAllListedCases()
    {
        var converters = NumberToStringConverter.ReadConfiguration(MultiValueTestConfig);
        var c = converters["TEST-MV"];

        // base (no case variant) → exception "eerste"
        Assert.AreEqual("eerste", c.ConvertOrdinal(1));
        // the three values listed in values="genitief,datief,accusatief" all apply "eersten"
        Assert.AreEqual("eersten", c.ConvertOrdinal(1, "case=genitief"),   "genitief");
        Assert.AreEqual("eersten", c.ConvertOrdinal(1, "case=datief"),     "datief");
        Assert.AreEqual("eersten", c.ConvertOrdinal(1, "case=accusatief"), "accusatief");
    }

    [TestMethod]
    public void OrdinalVariant_MultiValue_SuffixOverrideForAllListedCases()
    {
        var converters = NumberToStringConverter.ReadConfiguration(MultiValueTestConfig);
        var c = converters["TEST-MV"];

        // 2 → no exception → base suffix "de" → "tweede"
        Assert.AreEqual("tweede", c.ConvertOrdinal(2));
        // genitief/datief/accusatief → suffix "den" → "tweeden"
        Assert.AreEqual("tweeden", c.ConvertOrdinal(2, "case=genitief"),   "genitief suffix");
        Assert.AreEqual("tweeden", c.ConvertOrdinal(2, "case=datief"),     "datief suffix");
        Assert.AreEqual("tweeden", c.ConvertOrdinal(2, "case=accusatief"), "accusatief suffix");
    }

    [TestMethod]
    public void CardinalVariant_MultiValue_SameReplacementForAllListedCases()
    {
        var converters = NumberToStringConverter.ReadConfiguration(MultiValueTestConfig);
        var c = converters["TEST-MV"];

        // base / nominatief: één unchanged
        Assert.AreEqual("één",  c.Convert(1));
        Assert.AreEqual("één",  c.Convert(1, "case=nominatief"));
        // genitief and datief share the same replacement rule via values="genitief,datief"
        Assert.AreEqual("ener", c.Convert(1, "case=genitief"), "genitief");
        Assert.AreEqual("ener", c.Convert(1, "case=datief"),   "datief");
        // accusatief uses the separate single-value rule
        Assert.AreEqual("enen", c.Convert(1, "case=accusatief"), "accusatief");
    }

    private sealed class OrdinalPluginSpecifics
        : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
    {
        public string FinalizeWriting(string lang, string text) => text;

        public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> variants, out string? result)
        {
            if (number == 0) { result = null; return false; }
            result = $"ORDINAL_{number}";
            return true;
        }
    }

    // ─── Triggers ─────────────────────────────────────────────────────────────

    private static NumberToStringConverter MakeTriggerConverter(
        IEnumerable<NumberToStringConverter.TriggerRule> triggers)
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            Triggers = triggers.ToList()
        };
        return new NumberToStringConverter(options);
    }

    private static NumberToStringConverter.TriggerReplace SimpleReplace(string from, string to, bool regex = false) =>
        new(from, regex, [], to);

    private static NumberToStringConverter.TriggerRule EndTrigger(string from, string to, bool regex = false) =>
        new(NumberToStringConverter.TriggerAt.End, null, [SimpleReplace(from, to, regex)]);

    private static NumberToStringConverter.TriggerRule GroupTrigger(int? groupIndex, string from, string to, bool regex = false) =>
        new(NumberToStringConverter.TriggerAt.Group,
            groupIndex.HasValue ? [groupIndex.Value] : null,
            [SimpleReplace(from, to, regex)]);

    private static NumberToStringConverter.TriggerRule GroupWithScaleTrigger(string from, string to) =>
        new(NumberToStringConverter.TriggerAt.GroupWithScale, null, [SimpleReplace(from, to)]);

    [TestMethod]
    public void Trigger_End_Unconditional_LiteralReplace()
    {
        var c = MakeTriggerConverter([EndTrigger("one", "ONE")]);
        Assert.AreEqual("ONE", c.Convert(1));
        Assert.AreEqual("twenty-ONE", c.Convert(21));
        Assert.AreEqual("two", c.Convert(2));
    }

    [TestMethod]
    public void Trigger_End_Regex()
    {
        var c = MakeTriggerConverter([EndTrigger(@"\bone\b", "1", regex: true)]);
        Assert.AreEqual("1", c.Convert(1));
        Assert.AreEqual("twenty-1", c.Convert(21));
        Assert.AreEqual("1 thousand", c.Convert(1_000));
        Assert.AreEqual("two", c.Convert(2));
    }

    [TestMethod]
    public void Trigger_End_VariantConditioned()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        var options = new NumberToStringConverterOptions(en);
        options.VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["masc", "fem"])];
        // Replace with variant forms: masc="one" (default, first form), fem="una"
        var forms = new List<(IReadOnlyDictionary<string, string>, string)>
        {
            (new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gender"] = "masc" }, "one"),
            (new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gender"] = "fem"  }, "una"),
        };
        var replace = new NumberToStringConverter.TriggerReplace("one", false, forms, defaultTo: "one");
        options.Triggers = [new NumberToStringConverter.TriggerRule(NumberToStringConverter.TriggerAt.End, null, [replace])];
        var c = new NumberToStringConverter(options);

        Assert.AreEqual("una", c.Convert(1, "gender=fem"));
        Assert.AreEqual("one", c.Convert(1, "gender=masc"));
        Assert.AreEqual("one", c.Convert(1));  // default
    }

    [TestMethod]
    public void Trigger_End_NoDefaultTo_SkipsWhenNoVariantMatches()
    {
        // When no DefaultTo and no variant matches, the replacement is skipped entirely
        // (the regex is never even evaluated — ApplyTriggerReplace short-circuits)
        var en = NumberToStringConverter.GetConverter("EN");
        var options = new NumberToStringConverterOptions(en);
        options.VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["masc", "fem"])];
        var forms = new List<(IReadOnlyDictionary<string, string>, string)>
        {
            (new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gender"] = "fem" }, "una"),
        };
        // No DefaultTo → only fires for fem; masc and no-variant calls are unchanged
        var replace = new NumberToStringConverter.TriggerReplace("one", false, forms, defaultTo: null);
        options.Triggers = [new NumberToStringConverter.TriggerRule(NumberToStringConverter.TriggerAt.End, null, [replace])];
        var c = new NumberToStringConverter(options);

        Assert.AreEqual("una", c.Convert(1, "gender=fem"));
        Assert.AreEqual("one", c.Convert(1, "gender=masc"));  // no default → skipped
        Assert.AreEqual("one", c.Convert(1));                  // no default → skipped
    }

    [TestMethod]
    public void Trigger_Group0_FiresOnlyForUnitsChunk()
    {
        // group(0) transforms digits of the units chunk only — thousands chunk (group 1) is untouched
        var c = MakeTriggerConverter([GroupTrigger(0, "one", "UNO")]);
        Assert.AreEqual("UNO", c.Convert(1));
        Assert.AreEqual("one thousand", c.Convert(1_000));       // group 1 not affected
        Assert.AreEqual("one thousand, UNO", c.Convert(1_001));  // only units group changes
    }

    [TestMethod]
    public void Trigger_Group_AllGroups_FiresForEveryGroup()
    {
        // group (no index) fires for all groups, digits-only, before scale
        var c = MakeTriggerConverter([GroupTrigger(null, "one", "UNO")]);
        Assert.AreEqual("UNO", c.Convert(1));
        Assert.AreEqual("UNO thousand", c.Convert(1_000));       // thousands group: "one" → "UNO", then scale appended
        Assert.AreEqual("UNO thousand, UNO", c.Convert(1_001));
    }

    [TestMethod]
    public void Trigger_GroupWithScale_FiresAfterScale()
    {
        // groupWithScale fires on "digits + scale" text after ApplyReplacements
        var c = MakeTriggerConverter([GroupWithScaleTrigger("one thousand", "mille")]);
        Assert.AreEqual("mille", c.Convert(1_000));
        Assert.AreEqual("two thousand", c.Convert(2_000));         // not matching
        Assert.AreEqual("mille, one", c.Convert(1_001));           // units group stays separate
    }

    [TestMethod]
    public void Trigger_End_FiresForOrdinals()
    {
        // end triggers fire in ConvertOrdinal pipeline (after ApplyOrdinalTransform, before FinalizeWriting)
        var c = MakeTriggerConverter([EndTrigger("second", "SECOND")]);
        Assert.AreEqual("SECOND", c.ConvertOrdinal(2));
        Assert.AreEqual("third", c.ConvertOrdinal(3));  // unrelated ordinal unaffected
    }

    [TestMethod]
    public void Trigger_Group0_BreaksOrdinalWordRule_ByDesign()
    {
        // group(0) fires inside ConvertRaw, which ordinals call too.
        // "two" → "TWO" before ordinal rules run → word rule "two"→"second" can't match → suffix "th" used
        var c = MakeTriggerConverter([GroupTrigger(0, "two", "TWO")]);
        Assert.AreEqual("TWO", c.Convert(2));
        Assert.AreEqual("TWOth", c.ConvertOrdinal(2));  // intended: documented side-effect
    }

    [TestMethod]
    public void Trigger_MultipleTriggersAppliedInOrder()
    {
        var c = MakeTriggerConverter([
            EndTrigger("one", "eins"),
            EndTrigger("eins", "EIN"),
        ]);
        Assert.AreEqual("EIN", c.Convert(1));
    }

    // ─── Significant-digits precision ─────────────────────────────────────────

    [TestMethod]
    public void RoundToSignificantDigits_BasicCases()
    {
        Assert.AreEqual(123000000, (long)MathEx.RoundToSignificantDigits(123456789, 3));
        Assert.AreEqual(120000000, (long)MathEx.RoundToSignificantDigits(123456789, 2));
        Assert.AreEqual(100000000, (long)MathEx.RoundToSignificantDigits(123456789, 1));
        Assert.AreEqual(123456789, (long)MathEx.RoundToSignificantDigits(123456789, 9));
        Assert.AreEqual(123456789, (long)MathEx.RoundToSignificantDigits(123456789, 12));
    }

    [TestMethod]
    public void RoundToSignificantDigits_RoundsUp_When5()
    {
        // 125 → precision 2: 125 → scale=10, (125+5)/10*10 = 130
        Assert.AreEqual(130, (long)MathEx.RoundToSignificantDigits(125, 2));
        // 155000 → precision 2: scale=10000, (155000+5000)/10000*10000 = 160000
        Assert.AreEqual(160000, (long)MathEx.RoundToSignificantDigits(155000, 2));
    }

    [TestMethod]
    public void RoundToSignificantDigits_RoundsDown_WhenBelow5()
    {
        // 124 → precision 2: scale=10, (124+5)/10*10 = 120
        Assert.AreEqual(120, (long)MathEx.RoundToSignificantDigits(124, 2));
    }

    [TestMethod]
    public void RoundToSignificantDigits_Zero_ReturnsZero()
    {
        Assert.AreEqual(BigInteger.Zero, MathEx.RoundToSignificantDigits(0, 3));
    }

    [TestMethod]
    public void RoundToSignificantDigits_Negative_PreservesSign()
    {
        Assert.AreEqual(-123000000, (long)MathEx.RoundToSignificantDigits(-123456789, 3));
        Assert.AreEqual(-130, (long)MathEx.RoundToSignificantDigits(-125, 2));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void RoundToSignificantDigits_ZeroDigits_Throws()
    {
        MathEx.RoundToSignificantDigits(123, 0);
    }

    [TestMethod]
    public void Convert_WithPrecision_FR()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // 123456789 → precision 3 → 123000000
        Assert.AreEqual("cent vingt trois millions", fr.Convert((BigInteger)123456789, 3));
        // 123456789 → precision 2 → 120000000
        Assert.AreEqual("cent vingt millions", fr.Convert((BigInteger)123456789, 2));
        // 123456789 → precision 1 → 100000000
        Assert.AreEqual("cent millions", fr.Convert((BigInteger)123456789, 1));
    }

    [TestMethod]
    public void Convert_WithPrecision_EN()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        // 123456789 → precision 3 → 123000000
        Assert.AreEqual("one hundred and twenty-three million", en.Convert((BigInteger)123456789, 3));
        // 123456789 → precision 2 → 120000000
        Assert.AreEqual("one hundred and twenty million", en.Convert((BigInteger)123456789, 2));
    }

    [TestMethod]
    public void Convert_WithPrecision_NoPrecisionLoss_WhenPrecisionLargeEnough()
    {
        var en = NumberToStringConverter.GetConverter("EN");
        string full = en.Convert(123456789);
        string withPrecision = en.Convert((BigInteger)123456789, 20);
        Assert.AreEqual(full, withPrecision);
    }

    // ─── F1 — Convert(decimal, int mandatoryDecimalDigits) ───────────────────

    [TestMethod]
    public void ConvertDecimal_MandatoryDigits_Negative_PreservesNaturalBehavior()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // -1 is the internal sentinel for "show as-is"; both paths must produce the same result.
        Assert.AreEqual(fr.Convert(21.5m), fr.Convert(21.5m, -1, null, []));
        Assert.AreEqual(fr.Convert(21.5m), fr.Convert(21.5m, []));
    }

    [TestMethod]
    public void ConvertDecimal_MandatoryDigits_Zero_SuppressesDecimalPartAfterRounding()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // 21.4m → rounds to 21 (AwayFromZero) → decimal part suppressed
        Assert.AreEqual("vingt et un", fr.Convert(21.4m, 0));
        // 21.5m → rounds to 22 (AwayFromZero, midpoint rounds up) → decimal part suppressed
        Assert.AreEqual(fr.Convert(22), fr.Convert(21.5m, 0));
    }

    [TestMethod]
    public void ConvertDecimal_MandatoryDigits_PadsDecimalToRequiredLength_FR()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // "5" padded to "50" → Fractions[2]="centième(s)" → Convert(50)="cinquante" → "centièmes"
        Assert.AreEqual("vingt et un virgule cinquante centièmes", fr.Convert(21.5m, 2));
    }

    [TestMethod]
    public void ConvertDecimal_MandatoryDigits_ShowsZeroWhenDecimalPartIsZero()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // No decimal part → padded to "00" → Convert(0)="zéro" → singular "centième" (0 ∈ [-1,1])
        Assert.AreEqual("vingt et un virgule zéro centième", fr.Convert(21m, 2));
    }

    [TestMethod]
    public void ConvertDecimal_MandatoryDigits_RoundsExtraDecimalDigits()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // 21.567m → decimal.Round(..., 2, AwayFromZero) = 21.57
        // The decimal sub-value goes through .Replace("-", " "), so hyphens become spaces.
        Assert.AreEqual("vingt et un virgule cinquante sept centièmes", fr.Convert(21.567m, 2));
        // 21.564m → 21.56
        Assert.AreEqual("vingt et un virgule cinquante six centièmes", fr.Convert(21.564m, 2));
    }

    // ─── F2 — Convert(decimal, params string[] variants) ─────────────────────

    [TestMethod]
    public void ConvertDecimal_WithVariants_AppliedToIntegerPart_FR()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // gender=feminin: "un" → "une" on the integer part; decimal part unchanged
        Assert.AreEqual("une virgule cinq dixièmes", fr.Convert(1.5m, "gender=feminin"));
        // masculine (default) — identical to Convert(1.5m)
        Assert.AreEqual(fr.Convert(1.5m), fr.Convert(1.5m, "gender=masculin"));
    }

    [TestMethod]
    public void ConvertDecimal_WithVariantsAndPrecision_FR()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // Variants and mandatory precision compose: integer "une" + decimal "cinquante centièmes"
        Assert.AreEqual("une virgule cinquante centièmes", fr.Convert(1.5m, 2, "gender=feminin"));
    }

    // ─── F3 — DecimalFormatOptions.DecimalSeparator ──────────────────────────

    [TestMethod]
    public void DecimalFormatOptions_DecimalSeparator_PluralizedAgainstIntegerPart()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var opts = new DecimalFormatOptions { DecimalSeparator = "euro(s)" };
        // integer = 1 → between(-1,1) → singular "euro"
        Assert.AreEqual("un euro cinquante centièmes",          fr.Convert(1.50m,  2, opts));
        // integer = 2 → plural "euros"
        Assert.AreEqual("deux euros cinquante centièmes",       fr.Convert(2.50m,  2, opts));
        // integer = 21 → plural "euros"
        Assert.AreEqual("vingt et un euros cinquante centièmes", fr.Convert(21.50m, 2, opts));
    }

    [TestMethod]
    public void DecimalFormatOptions_DecimalSeparator_NoMarker_PassedThrough()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // No "(s)" marker → word is used unchanged regardless of the integer value
        var opts = new DecimalFormatOptions { DecimalSeparator = "virgule" };
        Assert.AreEqual("un virgule cinquante centièmes",        fr.Convert(1.50m,  2, opts));
        Assert.AreEqual("vingt et un virgule cinquante centièmes", fr.Convert(21.50m, 2, opts));
    }

    // ─── F4 — DecimalFormatOptions.DecimalSuffix ─────────────────────────────

    [TestMethod]
    public void DecimalFormatOptions_DecimalSuffix_OverridesFractionConfig()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var opts = new DecimalFormatOptions { DecimalSuffix = "centime(s)" };
        // "centime(s)" replaces the configured "centième(s)" from FR's <Fractions>
        Assert.AreEqual("vingt et un virgule cinquante centimes", fr.Convert(21.50m, 2, opts));
    }

    [TestMethod]
    public void DecimalFormatOptions_DecimalSuffix_PluralizedAgainstDecimalValue()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var opts = new DecimalFormatOptions { DecimalSuffix = "centime(s)" };
        // decimal value 50 → plural "centimes"
        Assert.AreEqual("vingt et un virgule cinquante centimes", fr.Convert(21.50m, 2, opts));
        // decimal value 1 → singular "centime"
        Assert.AreEqual("un virgule un centime",                  fr.Convert(1.01m,  2, opts));
        // decimal value 0 → singular "centime" (0 ∈ [-1,1])
        Assert.AreEqual("vingt et un virgule zéro centime",       fr.Convert(21m,    2, opts));
    }

    [TestMethod]
    public void DecimalFormatOptions_DecimalSuffix_ForcesWholeNumberConversionWithoutFractionConfig()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // FR has no <Fraction> entry for 4 digits → without override: digit-by-digit.
        // With DecimalSuffix: whole-number conversion is forced regardless.
        var opts = new DecimalFormatOptions { DecimalSuffix = "dix-millième(s)" };
        // 21.5m with 4 digits → pad "5" to "5000" → Convert(5000)="cinq mille" → plural suffix
        Assert.AreEqual("vingt et un virgule cinq mille dix-millièmes", fr.Convert(21.5m, 4, opts));
    }

    // ─── F5 — DecimalFormatOptions.OmitZeroDecimals ──────────────────────────

    [TestMethod]
    public void DecimalFormatOptions_OmitZeroDecimals_SuppressesZeroDecimalPart()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var opts = new DecimalFormatOptions { OmitZeroDecimals = true };
        // 21m → mandatory 2 digits → "00" → zero → decimal part (and separator) suppressed
        Assert.AreEqual("vingt et un", fr.Convert(21m, 2, opts));
    }

    [TestMethod]
    public void DecimalFormatOptions_OmitZeroDecimals_DoesNotSuppressNonZeroDecimal()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var opts = new DecimalFormatOptions { OmitZeroDecimals = true };
        // 21.5m → "50" after padding → not zero → decimal part shown normally
        Assert.AreEqual("vingt et un virgule cinquante centièmes", fr.Convert(21.5m, 2, opts));
    }

    [TestMethod]
    public void DecimalFormatOptions_OmitZeroDecimals_WorksAfterRounding()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var optsOmit = new DecimalFormatOptions { OmitZeroDecimals = true, DecimalSuffix = "centime(s)" };
        // 21.004m → rounds to 21.00 (4 < 5, rounds down) → zero → suppressed
        Assert.AreEqual("vingt et un", fr.Convert(21.004m, 2, new DecimalFormatOptions { OmitZeroDecimals = true }));
        // 21.005m → rounds to 21.01 (5 rounds up, AwayFromZero) → not zero → shown
        Assert.AreEqual("vingt et un virgule un centime", fr.Convert(21.005m, 2, optsOmit));
    }

    [TestMethod]
    public void DecimalFormatOptions_OmitZeroDecimals_FalseShowsZeroDecimalPart()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        // Explicitly false → zero decimal part is shown (same as null options)
        Assert.AreEqual("vingt et un virgule zéro centième", fr.Convert(21m, 2, new DecimalFormatOptions { OmitZeroDecimals = false }));
        Assert.AreEqual("vingt et un virgule zéro centième", fr.Convert(21m, 2, (DecimalFormatOptions?)null));
    }

    // ─── F6 — Combined DecimalFormatOptions ──────────────────────────────────

    [TestMethod]
    public void DecimalFormatOptions_Combined_CurrencyStyle()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var opts = new DecimalFormatOptions
        {
            DecimalSeparator = "euro(s)",
            DecimalSuffix    = "centime(s)",
            OmitZeroDecimals = true,
        };
        Assert.AreEqual("un euro cinquante centimes",           fr.Convert(1.50m,  2, opts));
        Assert.AreEqual("vingt et un euros cinquante centimes", fr.Convert(21.50m, 2, opts));
        Assert.AreEqual("un euro un centime",                   fr.Convert(1.01m,  2, opts));
        // OmitZeroDecimals: separator (unit name) is also omitted when decimal part is zero
        Assert.AreEqual("vingt et un",                         fr.Convert(21m,    2, opts));
    }

    [TestMethod]
    public void DecimalFormatOptions_Combined_NegativeNumber()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var opts = new DecimalFormatOptions { DecimalSeparator = "euro(s)", DecimalSuffix = "centime(s)" };
        Assert.AreEqual("moins cinq euros cinquante centimes", fr.Convert(-5.50m, 2, opts));
    }

    [TestMethod]
    public void DecimalFormatOptions_Combined_WithVariants()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var opts = new DecimalFormatOptions { DecimalSeparator = "euro(s)", DecimalSuffix = "centime(s)" };
        // gender=feminin: integer "1" → "une", decimal "1" → "une"; separator and suffix unchanged
        Assert.AreEqual("une euro une centime", fr.Convert(1.01m, 2, opts, "gender=feminin"));
    }

    // ─── F7 — Interface default implementations for new decimal overloads ─────

    [TestMethod]
    public void Interface_ConvertDecimal_NewOverloads_DefaultsToConvertDecimal()
    {
        // A minimal implementation that only implements the original Convert(decimal)
        // must compile and produce results from all new overloads via default implementations.
        INumberToStringConverter converter = new MinimalConverter();
        // Use Convert(decimal) as the reference value to stay locale-independent.
        string expected = converter.Convert(21.5m);

        Assert.AreEqual(expected, converter.Convert(21.5m, "gender=feminin"));
        Assert.AreEqual(expected, converter.Convert(21.5m, 2, []));
        Assert.AreEqual(expected, converter.Convert(21.5m, 2, (DecimalFormatOptions?)null));
        Assert.AreEqual(expected, converter.Convert(21.5m, 2, (DecimalFormatOptions?)null, []));
        Assert.AreEqual(expected, converter.Convert(21.5m, 2, new DecimalFormatOptions { OmitZeroDecimals = true }));
    }
}
