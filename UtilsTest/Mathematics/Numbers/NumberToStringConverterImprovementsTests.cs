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

        // Without variant: counting form (GermanSpecifics: "ein" → "eins")
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
        Assert.AreEqual("sijamuoto", dims[0].Name);
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

        // Variant overloads → delegate to non-variant Convert, ignore parameters
        Assert.AreEqual("42", converter.Convert((BigInteger)42, "gender=feminin"));
        Assert.AreEqual("7",  converter.Convert(7,  "gender=feminin"));
        Assert.AreEqual("99", converter.Convert(99L, "gender=feminin"));

        // Convert(Number) → delegates to Convert(Numerator)
        Assert.AreEqual("3", converter.Convert(new Number(3, 2)));  // 3/2 → Numerator=3

        // ConvertOrdinal → throws NotSupportedException
        Assert.ThrowsException<NotSupportedException>(() => converter.ConvertOrdinal(1));
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
}
