using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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

    // ─── C2 — Grammatical gender ───────────────────────────────────────────

    [TestMethod]
    public void Convert_FR_Feminine_OneBecomesUne()
    {
        var converter = NumberToStringConverter.GetConverter("FR");

        Assert.AreEqual("une", converter.Convert(1, NumberGender.Feminine));
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
            Assert.AreEqual(expected, converter.Convert(number, NumberGender.Feminine), $"Féminin FR de {number}");
    }

    [TestMethod]
    public void Convert_FR_Masculine_Unchanged()
    {
        var converter = NumberToStringConverter.GetConverter("FR");

        Assert.AreEqual("un", converter.Convert(1, NumberGender.Masculine));
        Assert.AreEqual("vingt et un", converter.Convert(21, NumberGender.Masculine));
    }

    [TestMethod]
    public void Interface_ConvertWithGender_Exists()
    {
        INumberToStringConverter converter = NumberToStringConverter.GetConverter("FR");

        string result = converter.Convert(new BigInteger(1), NumberGender.Feminine);
        Assert.AreEqual("une", result);
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
