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

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.Convert(1000));
    }

    [TestMethod]
    public void Convert_ThrowsWhenNegativeExceedsMaxNumber()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            MaxNumber = new BigInteger(999)
        };
        var converter = new NumberToStringConverter(options);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => converter.Convert(-1000));
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
            <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999999">
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
    public void RegisterConfigurations_DuplicateCultureThrowsByDefault()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => NumberToStringConverter.RegisterConfigurations([MinimalXmlConfig, MinimalXmlConfig]));
    }

    // ─── C2 — Grammatical variants (gender) ───────────────────────────────

    [TestMethod]
    public void Interface_ConvertWithVariants_Exists()
    {
        INumberToStringConverter converter = NumberToStringConverter.GetConverter("FR");

        string result = converter.Convert(new BigInteger(1), "gender=feminin");
        Assert.AreEqual("une", result);
    }

    [TestMethod]
    public void Convert_UnknownVariantDimension_Throws()
    {
        var converter = NumberToStringConverter.GetConverter("FR");
        Assert.ThrowsExactly<ArgumentException>(() => converter.Convert(1, "cas=inconnu"));
    }

    // ─── C2c — Variants DE (genus / kasus) ────────────────────────────────

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

    // ─── C5 — Ordinal conversion (Dutch) ───────────────────────────────────

    // ─── C6 — Ordinal conversion (Basque) ──────────────────────────────────

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
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(1));
        // ConvertOrdinal(long) in int range → delegates → same NotSupportedException
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(1L));
        // ConvertOrdinal(long) outside int range → OverflowException from checked cast
        Assert.ThrowsExactly<OverflowException>(() => converter.ConvertOrdinal((long)int.MaxValue + 1));
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

    // ── C8 ─ Variant ordinals ────────────────────────────────────────────

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

    // ── C8d — Ordinals HE ────────────────────────────────────────────────

    // ── C8e — Ordinals EE ────────────────────────────────────────────────

    // ── C8f — Ordinals CA ────────────────────────────────────────────────

    // ── C8g — Ordinals GL ────────────────────────────────────────────────

    // ── C8h — Ordinals PT ────────────────────────────────────────────────

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

    [TestMethod]
    public void ConvertOrdinal_Plugin_LongDefault_FallsBackAboveIntMax()
    {
        // OrdinalPluginSpecifics only implements TryConvertOrdinal(int); the default long
        // implementation delegates for values ≤ int.MaxValue and returns false above.
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = new OrdinalPluginSpecifics()
        };
        var conv = new NumberToStringConverter(options);

        // int.MaxValue is still within range → plugin handles it
        Assert.AreEqual($"ORDINAL_{int.MaxValue}", conv.ConvertOrdinal((long)int.MaxValue));
        // Above int.MaxValue → plugin returns false → XML pipeline produces a non-null result
        long big = (long)int.MaxValue + 1;
        string result = conv.ConvertOrdinal(big);
        Assert.IsNotNull(result);
        Assert.IsFalse(result.StartsWith("ORDINAL_"), "XML pipeline should have handled the large value");
    }

    [TestMethod]
    public void ConvertOrdinal_Plugin_LongOverride_HandlesLargeValues()
    {
        // LargeOrdinalPluginSpecifics overrides TryConvertOrdinal(long) directly.
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            LanguageSpecifics = new LargeOrdinalPluginSpecifics()
        };
        var conv = new NumberToStringConverter(options);

        Assert.AreEqual("ORDINAL_1",   conv.ConvertOrdinal(1L));
        Assert.AreEqual($"ORDINAL_{(long)int.MaxValue + 1}", conv.ConvertOrdinal((long)int.MaxValue + 1));
    }

    // ─── FR — Ordinal variants (gender=feminin) ───────────────────────────────

    // ─── RU — Ordinals ───────────────────────────────────────────────────────

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

    // ─── FI — Ordinals ───────────────────────────────────────────────────────

    // ─── HI — Ordinals ───────────────────────────────────────────────────────

    // ─── PL — Ordinals ───────────────────────────────────────────────────────

    // ─── AR — Ordinals ───────────────────────────────────────────────────────

    // ─── WO — Ordinals ───────────────────────────────────────────────────────

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

    // ── C14 — Ordinal AR feminine variant ───────────────────────────────────

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

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            LegacyNumberToStringFixture.ReadConfiguration(xml));
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

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            LegacyNumberToStringFixture.ReadConfiguration(xml));
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
        var converters = LegacyNumberToStringFixture.ReadConfiguration(MultiValueTestConfig);
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
        var converters = LegacyNumberToStringFixture.ReadConfiguration(MultiValueTestConfig);
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
        var converters = LegacyNumberToStringFixture.ReadConfiguration(MultiValueTestConfig);
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

    // Implements TryConvertOrdinal(long) directly to handle values above int.MaxValue.
    private sealed class LargeOrdinalPluginSpecifics
        : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
    {
        public string FinalizeWriting(string lang, string text) => text;

        public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> variants, out string? result)
            => TryConvertOrdinal((long)number, variants, out result);

        public bool TryConvertOrdinal(long number, IReadOnlyDictionary<string, string> variants, out string? result)
        {
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
        var forms = new List<NumberToStringConverter.TriggerReplacementForm>
        {
            new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gender"] = "masc" }, "one"),
            new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gender"] = "fem"  }, "una"),
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
        var forms = new List<NumberToStringConverter.TriggerReplacementForm>
        {
            new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gender"] = "fem" }, "una"),
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
    public void RoundToSignificantDigits_ZeroDigits_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MathEx.RoundToSignificantDigits(123, 0));
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

    // ─── G1 — Convert(int/long, int significantDigits) ────────────────────────

    [TestMethod]
    public void Convert_Int_SignificantDigits_MatchesBigInteger()
    {
        var en = NumberToStringConverter.GetConverter("EN");

        Assert.AreEqual(en.Convert((System.Numerics.BigInteger)12345, 3), en.Convert(12345,  3));
        Assert.AreEqual(en.Convert((System.Numerics.BigInteger)12345, 3), en.Convert(12345L, 3));
        Assert.AreEqual(en.Convert((System.Numerics.BigInteger)1,     3), en.Convert(1,      3));
        Assert.AreEqual(en.Convert((System.Numerics.BigInteger)999,   2), en.Convert(999,    2));
        Assert.AreEqual(en.Convert((System.Numerics.BigInteger)999,   2), en.Convert(999L,   2));
    }

    [TestMethod]
    public void Convert_Long_SignificantDigits_WithVariants_MatchesBigInteger()
    {
        var fr = NumberToStringConverter.GetConverter("FR");

        // 210 rounded to 1 significant digit → 200 ; 21 rounded to 2 → 21
        Assert.AreEqual(fr.Convert((System.Numerics.BigInteger)210, 1, "gender=feminin"),
                        fr.Convert(210L, 1, "gender=feminin"));
        Assert.AreEqual(fr.Convert((System.Numerics.BigInteger)21, 2, "gender=feminin"),
                        fr.Convert(21, 2, "gender=feminin"));
    }

    [TestMethod]
    public void Convert_Interface_IntLong_SignificantDigits_DelegatesToBigInteger()
    {
        INumberToStringConverter iface = NumberToStringConverter.GetConverter("EN");

        Assert.AreEqual(iface.Convert((System.Numerics.BigInteger)12345, 3),
                        iface.Convert(12345, 3));
        Assert.AreEqual(iface.Convert((System.Numerics.BigInteger)12345, 3),
                        iface.Convert(12345L, 3));
    }

    // ─── G2 — ConvertCurrency avec variants morphologiques ────────────────────

    private static CurrencyDefinition LiveCurrency() => new CurrencyDefinition
    {
        UnitSingular    = "livre",
        UnitPlural      = "livres",
        SubunitSingular = "sou",
        SubunitPlural   = "sous",
        SubunitDigits   = 2,
        Connector       = "et",
    };

    [TestMethod]
    public void ConvertCurrency_FR_DefaultVariant_IsMasculine()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var livre = LiveCurrency();

        // Without variant: masculine numeral (default FR dimension value)
        Assert.AreEqual("vingt et un livres", fr.ConvertCurrency(21m, livre));
        Assert.AreEqual("un livre",           fr.ConvertCurrency(1m,  livre));
    }

    [TestMethod]
    public void ConvertCurrency_FR_FeminineVariant_InflectsNumeral()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var livre = LiveCurrency();

        // Feminine variant: "un" → "une", "vingt et un" → "vingt et une"
        Assert.AreEqual("une livre",           fr.ConvertCurrency(1m,  livre, "gender=feminin"));
        Assert.AreEqual("vingt et une livres", fr.ConvertCurrency(21m, livre, "gender=feminin"));
        Assert.AreEqual("trente et une livres", fr.ConvertCurrency(31m, livre, "gender=feminin"));
    }

    [TestMethod]
    public void ConvertCurrency_FR_FeminineVariant_AppliesToSubunitsAsWell()
    {
        var fr = NumberToStringConverter.GetConverter("FR");
        var livre = LiveCurrency();

        // 21.01 → "vingt et une livres et un sou" (masculine, sou doesn't inflect un)
        // With gender=feminin: "vingt et une livres et une sous"… but "une sous" is grammatically
        // wrong in real French; we test the mechanical inflection, not linguistic correctness.
        // Compare against the BigInteger sub-conversions to stay independent of locale rendering.
        string unitsPart    = fr.Convert(21L, "gender=feminin");   // "vingt et une"
        string subunitsPart = fr.Convert(1L,  "gender=feminin");   // "une"
        string expected     = $"{unitsPart} livres et {subunitsPart} sou";

        Assert.AreEqual(expected, fr.ConvertCurrency(21.01m, livre, "gender=feminin"));
    }

    [TestMethod]
    public void ConvertCurrency_Interface_Variants_DelegatesToConcrete()
    {
        INumberToStringConverter iface = NumberToStringConverter.GetConverter("FR");
        var livre = LiveCurrency();

        // Calling via the interface must reach the concrete implementation (not the default throw).
        Assert.AreEqual(iface.ConvertCurrency(21m, livre),
                        iface.ConvertCurrency(21m, livre, []));
        Assert.AreEqual(iface.ConvertCurrency(21m, livre, "gender=feminin"),
                        ((NumberToStringConverter)iface).ConvertCurrency(21m, livre, "gender=feminin"));
    }

    // ─── onScale — replacement restreint à un groupe d'échelle ───────────

    private const string OnScaleTestConfig = """
        <?xml version="1.0" encoding="utf-8" ?>
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">

          <!-- TEST-OS-BASE : base <Replacement onScale="1"> -->
          <Language groupSize="3" separator=" " groupSeparator="" zero="zéro" minus="moins *" decimalSeparator="virgule">
            <Culture>TEST-OS-BASE</Culture>
            <Groups>
              <Group level="1">
                <Digit digit="0" string="" />
                <Digit digit="1" string="un" />
                <Digit digit="2" string="deux" />
              </Group>
              <Group level="2">
                <Digit digit="0" string="" buildString="*" />
              </Group>
              <Group level="3">
                <Digit digit="0" string="" buildString="*" />
              </Group>
            </Groups>
            <NumberScale firstLetterUpperCase="false">
              <StaticNames>
                <Scale value="0" string="" />
                <Scale value="1" string="mille" />
              </StaticNames>
            </NumberScale>
            <Replacements>
              <!-- fires only when processing the thousands group (onScale=1) -->
              <Replacement oldValue="un" newValue="MILLE_UN" scope="Anywhere" onScale="1" />
            </Replacements>
          </Language>

          <!-- TEST-OS-VARIANT : <Variant>/<Replacement onScale="1"> -->
          <Language groupSize="3" separator=" " groupSeparator="" zero="zéro" minus="moins *" decimalSeparator="virgule">
            <Culture>TEST-OS-VARIANT</Culture>
            <Groups>
              <Group level="1">
                <Digit digit="0" string="" />
                <Digit digit="1" string="un" />
                <Digit digit="2" string="deux" />
              </Group>
              <Group level="2">
                <Digit digit="0" string="" buildString="*" />
              </Group>
              <Group level="3">
                <Digit digit="0" string="" buildString="*" />
              </Group>
            </Groups>
            <NumberScale firstLetterUpperCase="false">
              <StaticNames>
                <Scale value="0" string="" />
                <Scale value="1" string="mille" />
              </StaticNames>
            </NumberScale>
            <Variants>
              <Dimension name="gender" values="masc,fem" />
              <!-- global variant rule: applies on the full combined string -->
              <Variant type="gender" variant="fem">
                <Replacement oldValue="deux" newValue="deux_f" scope="Anywhere" />
              </Variant>
              <!-- scale-specific: "un"→"une" only inside the thousands group -->
              <Variant type="gender" variant="fem">
                <Replacement oldValue="un" newValue="une" scope="Anywhere" onScale="1" />
              </Variant>
            </Variants>
          </Language>

        </Numbers>
        """;

    [TestMethod]
    public void BaseReplacement_OnScale_FiresOnlyInTargetGroup()
    {
        var c = LegacyNumberToStringFixture.ReadConfiguration(OnScaleTestConfig)["TEST-OS-BASE"];

        Assert.AreEqual("MILLE_UN mille",    c.Convert(1000), "1000");
        Assert.AreEqual("MILLE_UN mille un", c.Convert(1001), "1001: thousands yes, units no");
        Assert.AreEqual("un",                c.Convert(1),    "1: no thousands group processed");
        Assert.AreEqual("deux mille",        c.Convert(2000), "2000: 'deux' ≠ 'un'");
    }

    [TestMethod]
    public void VariantReplacement_OnScale_FiresPerGroupBeforeCombination()
    {
        var c = LegacyNumberToStringFixture.ReadConfiguration(OnScaleTestConfig)["TEST-OS-VARIANT"];

        // onScale=1 rule fires for thousands group only
        Assert.AreEqual("une mille",     c.Convert(1000, "gender=fem"), "1000 fem: thousands inflected");
        Assert.AreEqual("une mille un",  c.Convert(1001, "gender=fem"), "1001 fem: thousands yes, units no");
        Assert.AreEqual("un",            c.Convert(1,    "gender=fem"), "1 fem: no thousands group");

        // global variant rule still fires on the full combined string
        Assert.AreEqual("deux_f mille un", c.Convert(2001, "gender=fem"), "2001 fem: global rule on combined");

        // default variant (masc): no onScale=1 fem rule, no change
        Assert.AreEqual("un mille un", c.Convert(1001), "1001 masc: no change");
    }

    // ─── baseOn XML inheritance (C1) ──────────────────────────────────────────

    private const string BaseOnTestConfig = """
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
          <Language groupSize="3" separator=" " groupSeparator="" zero="nul" minus="min *" decimalSeparator="komma">
            <Culture>TEST-BO-BASE</Culture>
            <Groups>
              <Group level="1">
                <Digit digit="0" string="" />
                <Digit digit="1" string="een" />
                <Digit digit="2" string="twee" />
              </Group>
              <Group level="2">
                <Digit digit="0" string="" buildString="*" />
                <Digit digit="1" string="tien" buildString="*tien" />
                <Digit digit="2" string="twintig" buildString="*entwintig" />
              </Group>
              <Group level="3">
                <Digit digit="0" string="" buildString="*" />
                <Digit digit="1" string="honderd" buildString="honderd*" />
              </Group>
            </Groups>
            <NumberScale firstLetterUpperCase="false">
              <StaticNames>
                <Scale value="0" string=""/>
                <Scale value="1" string="duizend"/>
              </StaticNames>
            </NumberScale>
            <Replacements>
              <Replacement oldValue="een duizend" newValue="duizend" />
            </Replacements>
            <Ordinals suffix="de">
              <OrdinalException value="1" string="eerste" />
            </Ordinals>
          </Language>
          <Language baseOn="TEST-BO-BASE">
            <Culture>TEST-BO-DERIVED</Culture>
            <Replacements />
            <Ordinals>
              <OrdinalException value="2" string="tweede" />
            </Ordinals>
          </Language>
        </Numbers>
        """;

    [TestMethod]
    public void BaseOn_InheritsGroupsAndScaleFromBase()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(BaseOnTestConfig);
        var derived = cs["TEST-BO-DERIVED"];
        Assert.AreEqual("twee",    derived.Convert(2),   "single digit");
        Assert.AreEqual("tien",    derived.Convert(10),  "tens");
        Assert.AreEqual("honderd", derived.Convert(100), "hundreds");
    }

    [TestMethod]
    public void BaseOn_EmptyReplacementsOverridesBase_ThousandNotCollapsed()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(BaseOnTestConfig);
        var baseConv    = cs["TEST-BO-BASE"];
        var derivedConv = cs["TEST-BO-DERIVED"];

        Assert.AreEqual("duizend",     baseConv.Convert(1000),    "base collapses 1000");
        Assert.AreEqual("een duizend", derivedConv.Convert(1000), "derived keeps 'een duizend'");
    }

    [TestMethod]
    public void BaseOn_OrdinalsAreMerged_ChildExceptionAdded_BaseExceptionInherited()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(BaseOnTestConfig);
        var derived = cs["TEST-BO-DERIVED"];

        Assert.AreEqual("eerste", derived.ConvertOrdinal(1),  "exception from base");
        Assert.AreEqual("tweede", derived.ConvertOrdinal(2),  "exception from child");
        Assert.AreEqual("tiende", derived.ConvertOrdinal(10), "suffix 'de' inherited from base");
    }

    [TestMethod]
    public void BaseOn_DeChInheritsDeConfiguration()
    {
        var de   = NumberToStringConverter.GetConverter("DE");
        var deCh = NumberToStringConverter.GetConverter("de-CH");

        // DE collapses "ein tausend" → "tausend"; DE-CH keeps "ein tausend"
        Assert.AreEqual("tausend",     de.Convert(1000),   "DE: replacement applied");
        Assert.AreEqual("ein tausend", deCh.Convert(1000), "DE-CH: no replacement");

        // Both share the inherited ordinal word rules
        Assert.AreEqual("erste",  de.ConvertOrdinal(1),   "DE: irregular ordinal 1");
        Assert.AreEqual("erste",  deCh.ConvertOrdinal(1), "DE-CH: inherited ordinal 1");

        // DE-CH has an explicit ordinal exception for 1000
        Assert.AreEqual("tausendste", deCh.ConvertOrdinal(1000), "DE-CH: ordinal 1000 exception");
    }

    [TestMethod]
    public void BaseOn_ChainedInheritance_GrandparentConfigurationPropagates()
    {
        // BASE → MID (overrides nothing) → CHILD (adds an ordinal exception).
        // All in the same document. The P2 fix ensures CHILD merges against the
        // fully resolved MID (which carries all grandparent fields), not raw MID.
        const string chain = """
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="nul" minus="min *" decimalSeparator="komma">
                <Culture>TEST-CHAIN-BASE</Culture>
                <Groups>
                  <Group level="1">
                    <Digit digit="0" string="" />
                    <Digit digit="1" string="een" />
                    <Digit digit="2" string="twee" />
                  </Group>
                  <Group level="2">
                    <Digit digit="0" string="" buildString="*" />
                    <Digit digit="1" string="tien" buildString="*tien" />
                  </Group>
                  <Group level="3">
                    <Digit digit="0" string="" buildString="*" />
                    <Digit digit="1" string="honderd" buildString="honderd*" />
                  </Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false">
                  <StaticNames>
                    <Scale value="0" string=""/>
                    <Scale value="1" string="duizend"/>
                  </StaticNames>
                </NumberScale>
                <Ordinals suffix="de" />
              </Language>
              <Language baseOn="TEST-CHAIN-BASE">
                <Culture>TEST-CHAIN-MID</Culture>
              </Language>
              <Language baseOn="TEST-CHAIN-MID">
                <Culture>TEST-CHAIN-CHILD</Culture>
                <Ordinals>
                  <OrdinalException value="1" string="eerste" />
                </Ordinals>
              </Language>
            </Numbers>
            """;

        var cs = LegacyNumberToStringFixture.ReadConfiguration(chain);
        var child = cs["TEST-CHAIN-CHILD"];

        // grandparent groups and scale propagated through two levels
        Assert.AreEqual("twee",    child.Convert(2),   "digit inherited from grandparent");
        Assert.AreEqual("honderd", child.Convert(100), "hundreds inherited from grandparent");
        // child's ordinal exception
        Assert.AreEqual("eerste",  child.ConvertOrdinal(1),  "child exception");
        // grandparent suffix "de" inherited through mid and child merge
        Assert.AreEqual("tiende",  child.ConvertOrdinal(10), "suffix from grandparent");
    }

    [TestMethod]
    public void BaseOn_UnresolvedBase_ThrowsDescriptiveException()
    {
        const string bad = """
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language baseOn="DOES-NOT-EXIST">
                <Culture>TEST-BAD</Culture>
              </Language>
            </Numbers>
            """;
        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => LegacyNumberToStringFixture.ReadConfiguration(bad));
        StringAssert.Contains(ex.Message, "DOES-NOT-EXIST");
    }

    // ─── ParseRangeExpression — syntaxe XML → IntRange<long> ─────────────────

    [TestMethod]
    public void ParseRangeExpression_ExactValue()
    {
        var r = NumberToStringConverter.ParseRangeExpression("5");
        Assert.IsTrue(r.Contains(5));
        Assert.IsFalse(r.Contains(4));
        Assert.IsFalse(r.Contains(6));
    }

    [TestMethod]
    public void ParseRangeExpression_InclusiveRange()
    {
        var r = NumberToStringConverter.ParseRangeExpression("2..5");
        Assert.IsFalse(r.Contains(1));
        Assert.IsTrue(r.Contains(2));
        Assert.IsTrue(r.Contains(3));
        Assert.IsTrue(r.Contains(5));
        Assert.IsFalse(r.Contains(6));
    }

    [TestMethod]
    public void ParseRangeExpression_OpenStart()
    {
        var r = NumberToStringConverter.ParseRangeExpression("..4");
        Assert.IsTrue(r.Contains(0));
        Assert.IsTrue(r.Contains(4));
        Assert.IsFalse(r.Contains(5));
    }

    [TestMethod]
    public void ParseRangeExpression_OpenEnd()
    {
        var r = NumberToStringConverter.ParseRangeExpression("10..");
        Assert.IsFalse(r.Contains(9));
        Assert.IsTrue(r.Contains(10));
        Assert.IsTrue(r.Contains(999));
    }

    [TestMethod]
    public void ParseRangeExpression_CommaSeparated()
    {
        var r = NumberToStringConverter.ParseRangeExpression("1,3,5..7,20..");
        Assert.IsTrue(r.Contains(1));
        Assert.IsFalse(r.Contains(2));
        Assert.IsTrue(r.Contains(3));
        Assert.IsTrue(r.Contains(5));
        Assert.IsTrue(r.Contains(6));
        Assert.IsTrue(r.Contains(7));
        Assert.IsFalse(r.Contains(8));
        Assert.IsFalse(r.Contains(19));
        Assert.IsTrue(r.Contains(20));
        Assert.IsTrue(r.Contains(999));
    }

    [TestMethod]
    public void ParseRangeExpression_EmptyString_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => NumberToStringConverter.ParseRangeExpression(""));
        Assert.ThrowsExactly<ArgumentException>(() => NumberToStringConverter.ParseRangeExpression("   "));
    }

    // ─── onValue — onScale + onValue via XML ─────────────────────────────────

    private const string OnValueConfig = """
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
          <Language groupSize="3" separator=" " groupSeparator="" zero="nul" minus="min *" decimalSeparator="komma">
            <Culture>TEST-OV</Culture>
            <Groups>
              <Group level="1">
                <Digit digit="0" string="" />
                <Digit digit="1" string="en" />
                <Digit digit="2" string="to" />
                <Digit digit="3" string="tre" />
              </Group>
              <Group level="2">
                <Digit digit="0" string="" buildString="*" />
                <Digit digit="1" string="ti" buildString="*ti" />
                <Digit digit="2" string="tjue" buildString="tjue*" />
              </Group>
              <Group level="3">
                <Digit digit="0" string="" buildString="*" />
                <Digit digit="1" string="hundre" buildString="hundre*" />
              </Group>
            </Groups>
            <NumberScale firstLetterUpperCase="false">
              <StaticNames>
                <Scale value="0" string=""/>
                <Scale value="1" string="tusen"/>
              </StaticNames>
            </NumberScale>
            <!-- onScale=1 onValue=1 → fires only for the thousands group when its value is 1 -->
            <Replacements>
              <Replacement oldValue="en tusen" newValue="tusen" onScale="1" onValue="1" />
            </Replacements>
          </Language>
        </Numbers>
        """;

    [TestMethod]
    public void OnValue_OnScale1_Value1_ReplacesExact1000()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(OnValueConfig);
        var c = cs["TEST-OV"];
        // 1 × 1000: group text "en tusen" → replaced → "tusen"
        Assert.AreEqual("tusen", c.Convert(1000));
    }

    [TestMethod]
    public void OnValue_OnScale1_Value2_DoesNotReplace()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(OnValueConfig);
        var c = cs["TEST-OV"];
        // 2 × 1000: group text "to tusen" → onValue="1" does NOT match → unchanged
        Assert.AreEqual("to tusen", c.Convert(2000));
    }

    [TestMethod]
    public void OnValue_OnScale1_Value1_DoesNotAffectOtherGroups()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(OnValueConfig);
        var c = cs["TEST-OV"];
        // 1001: units group = 1 (scale=0, not scale=1) → no replacement for "en"
        Assert.AreEqual("tusen en", c.Convert(1001));
    }

    [TestMethod]
    public void OnValue_OnScale1_Value21_DoesNotReplace()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(OnValueConfig);
        var c = cs["TEST-OV"];
        // 21000: thousands group value is 21, not 1 → no replacement
        Assert.AreEqual("tjueen tusen", c.Convert(21000));
    }

    // ─── onValue — global (no onScale) ───────────────────────────────────────

    private const string OnValueGlobalConfig = """
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
          <Language groupSize="3" separator=" " groupSeparator="" zero="nul" minus="min *" decimalSeparator="komma">
            <Culture>TEST-OVG</Culture>
            <Groups>
              <Group level="1">
                <Digit digit="0" string="" />
                <Digit digit="1" string="en" />
                <Digit digit="2" string="to" />
              </Group>
              <Group level="2">
                <Digit digit="0" string="" buildString="*" />
                <Digit digit="1" string="ti" buildString="*ti" />
              </Group>
              <Group level="3">
                <Digit digit="0" string="" buildString="*" />
                <Digit digit="1" string="hundre" buildString="hundre*" />
              </Group>
            </Groups>
            <NumberScale firstLetterUpperCase="false">
              <StaticNames>
                <Scale value="0" string=""/>
                <Scale value="1" string="tusen"/>
              </StaticNames>
            </NumberScale>
            <!-- Global onValue: fires in the final-string pass only when abs==1 -->
            <Replacements>
              <Replacement oldValue="en" newValue="ett" onValue="1" />
            </Replacements>
          </Language>
        </Numbers>
        """;

    [TestMethod]
    public void OnValue_Global_FiresOnlyForMatchingFullNumber()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(OnValueGlobalConfig);
        var c = cs["TEST-OVG"];
        // abs=1 → final text "en" matches onValue="1" → "ett"
        Assert.AreEqual("ett", c.Convert(1));
        // abs=2 → final text "to" → onValue="1" doesn't fire
        Assert.AreEqual("to", c.Convert(2));
        // abs=11 → final text "enti" (ten+one combined) → onValue="1" doesn't fire
        Assert.AreEqual("enti", c.Convert(11));
    }

    // ─── onValue — real-language scenario (DE variant) ───────────────────────

    [TestMethod]
    public void OnValue_DE_Scoped_ThousandReplacementViaOnValue()
    {
        // Use the built-in DE converter. DE already has a global Replacement
        // "ein tausend" → "tausend". We verify the same result is achievable by
        // reading a custom config that uses onScale+onValue instead.
        const string deVariant = """
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="null" minus="minus *" decimalSeparator="Komma">
                <Culture>TEST-DE-OV</Culture>
                <Groups>
                  <Group level="1">
                    <Digit digit="0" string="" />
                    <Digit digit="1" string="ein" />
                    <Digit digit="2" string="zwei" />
                    <Digit digit="3" string="drei" />
                  </Group>
                  <Group level="2">
                    <Digit digit="0" string="" buildString="*" />
                    <Digit digit="1" string="zehn" buildString="*zehn" />
                    <Digit digit="2" string="zwanzig" buildString="*undzwanzig" />
                  </Group>
                  <Group level="3">
                    <Digit digit="0" string="" buildString="*" />
                    <Digit digit="1" string="hundert" buildString="einhundert*" />
                  </Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false">
                  <StaticNames>
                    <Scale value="0" string=""/>
                    <Scale value="1" string="tausend"/>
                  </StaticNames>
                </NumberScale>
                <Replacements>
                  <Replacement oldValue="ein tausend" newValue="tausend" onScale="1" onValue="1" />
                </Replacements>
              </Language>
            </Numbers>
            """;

        var cs = LegacyNumberToStringFixture.ReadConfiguration(deVariant);
        var c = cs["TEST-DE-OV"];

        Assert.AreEqual("tausend",            c.Convert(1000),  "1 000");
        Assert.AreEqual("zwei tausend",       c.Convert(2000),  "2 000");
        Assert.AreEqual("tausend ein",        c.Convert(1001),  "1 001 — units group unaffected");
        Assert.AreEqual("einundzwanzig tausend", c.Convert(21000), "21 000 — value≠1, no replacement");
    }

    // ─── onScale range syntax ─────────────────────────────────────────────────

    private const string OnScaleRangeConfig = """
        <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
          <Language groupSize="3" separator=" " groupSeparator="" zero="nul" minus="min *" decimalSeparator="komma">
            <Culture>TEST-SCR</Culture>
            <Groups>
              <Group level="1">
                <Digit digit="0" string="" />
                <Digit digit="1" string="un" />
                <Digit digit="2" string="deux" />
                <Digit digit="3" string="trois" />
              </Group>
              <Group level="2">
                <Digit digit="0" string="" buildString="*" />
                <Digit digit="1" string="dix" buildString="*dix" />
              </Group>
              <Group level="3">
                <Digit digit="0" string="" buildString="*" />
              </Group>
            </Groups>
            <NumberScale firstLetterUpperCase="false">
              <StaticNames>
                <Scale value="0" string=""/>
                <Scale value="1" string="mille"/>
                <Scale value="2" string="million"/>
                <Scale value="3" string="milliard"/>
              </StaticNames>
            </NumberScale>
            <Replacements>
              <!-- Fires for thousands AND millions (groups 1..2): collapse "un X" → "un-X" -->
              <Replacement oldValue="un mille" newValue="MILLE" onScale="1..2" />
              <Replacement oldValue="un million" newValue="MILLION" onScale="1..2" />
            </Replacements>
          </Language>
        </Numbers>
        """;

    [TestMethod]
    public void OnScale_RangeSyntax_FiresForBothMatchingGroups()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(OnScaleRangeConfig);
        var c = cs["TEST-SCR"];

        // onScale="1..2" should fire for group 1 (thousands) and group 2 (millions)
        Assert.AreEqual("MILLE",         c.Convert(1_000),         "1 000 — thousands group matches");
        Assert.AreEqual("MILLION",       c.Convert(1_000_000),     "1 000 000 — millions group matches");
        // Group 3 (milliards) is outside range 1..2
        Assert.AreEqual("un milliard",   c.Convert(1_000_000_000), "1 000 000 000 — milliard not in 1..2");
    }

    [TestMethod]
    public void OnScale_RangeSyntax_DoesNotFireOutsideRange()
    {
        var cs = LegacyNumberToStringFixture.ReadConfiguration(OnScaleRangeConfig);
        var c = cs["TEST-SCR"];

        // group 0 (units) is outside range 1..2 → no replacement
        Assert.AreEqual("un",   c.Convert(1),   "1 — units group not in 1..2");
        Assert.AreEqual("deux", c.Convert(2),   "2 — units group not in 1..2");
    }

    // ── AR — Ordinals 11-19 ──────────────────────────────────────────────────

    // ── PL — Ordinals avec variantes (plugin 20+, XML 11-19) ─────────────────

}
