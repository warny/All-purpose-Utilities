using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Language-independent engine tests for <c>&lt;OrdinalComposition&gt;</c>: a value covered by a rule is
/// split numerically into head (value − value mod divisor) and tail (value mod divisor), and its
/// ordinal is ordinal(head) + separator + ordinal(tail), both parts going through the whole ordinal
/// pipeline again. Covers recursion and its termination, precedence, variants, single finalization,
/// validation, programmatic configuration, cloning and <c>baseOn</c>.
/// </summary>
[TestClass]
public class NumberToStringOrdinalCompositionTests
{
    /// <summary>Creates a unique culture name.</summary>
    private static string NewCulture() => $"ORDCOMP-{Guid.NewGuid():N}";

    /// <summary>
    /// The ordinal block: "th" suffix, 1 → first, round thousands through OrdinalScale (1000 →
    /// "thousandth", 2000 → "two thousandth"), a feminine "the" suffix with 1 → "firsta", and the given
    /// composition rules.
    /// </summary>
    /// <param name="rules">The OrdinalComposition elements.</param>
    /// <returns>The ordinal block.</returns>
    private static string Ordinals(string rules) => $"""
        <Ordinals suffix="th">
          <OrdinalException value="1" string="first" />
          <OrdinalScale scales="1" multiplierSeparator=" " />
          {rules}
          <OrdinalVariants><Variant type="gender" variant="fem" suffix="the"><OrdinalException value="1" string="firsta" /></Variant></OrdinalVariants>
        </Ordinals>
        """;

    /// <summary>Builds the synthetic converter.</summary>
    /// <param name="rules">The OrdinalComposition elements.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string rules) => NumberToStringOrdinalScaleTests.Build(Ordinals(rules));

    // ── Composition and recursion ───────────────────────────────────────────────────────────────

    /// <summary>Without rules the historical ordinal is kept.</summary>
    [TestMethod]
    public void OrdinalComposition_Absent_KeepsHistoricalOrdinals()
    {
        var converter = Build("");

        Assert.AreEqual("one thousand tenth", converter.ConvertOrdinal(1010));
        Assert.AreEqual("one thousand oneth", converter.ConvertOrdinal(1001));
        Assert.AreEqual(0, converter.OrdinalCompositionRules.Count);
    }

    /// <summary>The ordinal of head and tail are joined by the configured separator.</summary>
    /// <param name="separator">The configured separator.</param>
    /// <param name="number">The ordinal value.</param>
    /// <param name="expected">The expected ordinal.</param>
    [TestMethod]
    [DataRow(" ", 1010L, "thousandth tenth")]
    [DataRow(" ", 1001L, "thousandth first")]
    [DataRow("", 1001L, "thousandthfirst")]
    [DataRow("-", 1001L, "thousandth-first")]
    [DataRow(" ", 2001L, "two thousandth first")]
    [DataRow(" ", 100001L, "one hundred thousandth first")]
    [DataRow(" ", 1999L, "thousandth nine hundred ninety nineth")]
    public void OrdinalComposition_JoinsHeadAndTailOrdinals(string separator, long number, string expected)
    {
        var converter = Build($"""<OrdinalComposition for="1001..999999" divisor="1000" separator="{separator}" />""");

        Assert.AreEqual(expected, converter.ConvertOrdinal(number));
    }

    /// <summary>The parts go through the pipeline again, so a part covered by another rule is composed in turn.</summary>
    [TestMethod]
    public void OrdinalComposition_Recurses()
    {
        var converter = Build("""
            <OrdinalComposition for="1001..999999" divisor="1000" separator=" " />
            <OrdinalComposition for="101..999" divisor="100" separator="+" />
            """);

        Assert.AreEqual("thousandth one hundredth+first", converter.ConvertOrdinal(1101));
        Assert.AreEqual("thousandth one hundredth", converter.ConvertOrdinal(1100));
        Assert.AreEqual("two hundredth+twenty oneth", converter.ConvertOrdinal(221));
    }

    /// <summary>A value of the range with a zero head or tail is not composed and keeps the rest of the pipeline.</summary>
    [TestMethod]
    public void OrdinalComposition_ZeroHeadOrTail_FallsThrough()
    {
        var converter = Build("""<OrdinalComposition for="1001..999999" divisor="1000" separator=" " />""");
        var wideDivisor = Build("""<OrdinalComposition for="1001..2000" divisor="1000000" separator=" " />""");

        Assert.AreEqual("two thousandth", converter.ConvertOrdinal(2000));
        Assert.AreEqual("one thousand oneth", wideDivisor.ConvertOrdinal(1001));
    }

    /// <summary>
    /// Termination: both parts are strictly smaller than the value, so even a rule covering every
    /// value with the smallest divisor ends; deep recursion is bounded by the number of digits.
    /// </summary>
    [TestMethod]
    public void OrdinalComposition_EveryValueCovered_Terminates()
    {
        var converter = Build("""<OrdinalComposition for="1.." divisor="10" separator="/" />""");

        Assert.AreEqual("one hundred twentyth/threeth", converter.ConvertOrdinal(123));
        Assert.AreEqual("first", converter.ConvertOrdinal(1));
        string deep = converter.ConvertOrdinal(999_999_999_999);
        Assert.AreEqual("nine hundred ninety nine billion nine hundred ninety nine million nine hundred ninety nine thousand nine hundred ninetyth/nineth", deep);
    }

    // ── Precedence and variants ─────────────────────────────────────────────────────────────────

    /// <summary>A whole-number exception wins over the composition.</summary>
    [TestMethod]
    public void OrdinalComposition_OrdinalException_KeepsPrecedence()
    {
        var converter = Build("""<OrdinalException value="1001" string="special" /><OrdinalComposition for="1001..999999" divisor="1000" separator=" " />""");

        Assert.AreEqual("special", converter.ConvertOrdinal(1001));
        Assert.AreEqual("thousandth twoth", converter.ConvertOrdinal(1002));
    }

    /// <summary>A composed value never reaches the suffix as a whole: only each part is suffixed.</summary>
    [TestMethod]
    public void OrdinalComposition_IsNotSuffixedAgain()
    {
        var converter = Build("""<OrdinalComposition for="1001..999999" divisor="1000" separator=" " />""");

        Assert.IsFalse(converter.ConvertOrdinal(1010).EndsWith("tenthth", StringComparison.Ordinal));
        Assert.AreEqual("thousandth tenth", converter.ConvertOrdinal(1010));
    }

    /// <summary>Both parts receive the caller's variants.</summary>
    [TestMethod]
    public void OrdinalComposition_BothPartsReceiveTheVariants()
    {
        var converter = Build("""<OrdinalComposition for="1001..999999" divisor="1000" separator=" " />""");

        Assert.AreEqual("thousandthe firsta", converter.ConvertOrdinal(1001, "gender=fem"));
        Assert.AreEqual("two thousandthe tenthe", converter.ConvertOrdinal(2010, "gender=fem"));
        Assert.AreEqual("thousandth first", converter.ConvertOrdinal(1001, "gender=plain"));
    }

    /// <summary>The sign policy and the configured prefix apply once to the composed result.</summary>
    [TestMethod]
    public void OrdinalComposition_SignAndPrefix_ApplyOnce()
    {
        var converter = Build("""<OrdinalComposition for="1001..999999" divisor="1000" separator=" " />""");

        Assert.AreEqual("minus thousandth tenth", converter.ConvertOrdinal(-1010));
    }

    /// <summary>The raw adjustment and the language finalization run once on the whole composed ordinal, never per part.</summary>
    [TestMethod]
    public void OrdinalComposition_AdjustsAndFinalizesOnce()
    {
        var source = Build("""<OrdinalComposition for="1001..999999" divisor="1000" separator=" " />""");
        var options = new NumberToStringConverterOptions(source)
        {
            AdjustFunction = text => "[" + text + "]",
            LanguageSpecifics = new WrappingLanguageSpecifics(),
        };

        Assert.AreEqual("<[thousandth tenth]>", new NumberToStringConverter(options).ConvertOrdinal(1010));
    }

    /// <summary>The parts go through the ordinal plugin like any ordinal.</summary>
    [TestMethod]
    public void OrdinalComposition_PartsGoThroughTheOrdinalPlugin()
    {
        var source = Build("""<OrdinalComposition for="1001..999999" divisor="1000" separator=" " />""");
        var options = new NumberToStringConverterOptions(source) { LanguageSpecifics = new TenPlugin() };

        Assert.AreEqual("thousandth TEN", new NumberToStringConverter(options).ConvertOrdinal(1010));
        Assert.AreEqual("TEN", new NumberToStringConverter(options).ConvertOrdinal(10));
    }

    // ── Validation ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Invalid rules are rejected at load.</summary>
    /// <param name="attributes">The rejected rule attributes.</param>
    [TestMethod]
    [DataRow("""for="1001..1999" divisor="1" separator=" " """)]
    [DataRow("""for="1001..1999" divisor="0" separator=" " """)]
    [DataRow("""for="1001..1999" divisor="-10" separator=" " """)]
    [DataRow("""for="0..1999" divisor="1000" separator=" " """)]
    [DataRow("""for="..1999" divisor="1000" separator=" " """)]
    [DataRow("""for="x" divisor="1000" separator=" " """)]
    [DataRow("""for="" divisor="1000" separator=" " """)]
    [DataRow("""divisor="1000" separator=" " """)]
    [DataRow("""for="1001..1999" separator=" " """)]
    [DataRow("""for="1001..1999" divisor="1000" """)]
    public void OrdinalComposition_InvalidRule_IsRejected(string attributes)
        => Assert.Throws<Exception>(() => Build($"""<OrdinalComposition {attributes}/>"""));

    /// <summary>Overlapping rules are rejected, whatever their order or divisors.</summary>
    [TestMethod]
    public void OrdinalComposition_OverlappingRules_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => Build("""
            <OrdinalComposition for="1001..1999" divisor="1000" separator=" " />
            <OrdinalComposition for="1500..2500" divisor="100" separator=" " />
            """));
        Assert.Throws<ArgumentException>(() => Build("""
            <OrdinalComposition for="1010" divisor="1000" separator=" " />
            <OrdinalComposition for="1001..1999" divisor="1000" separator="" />
            """));
    }

    /// <summary>Programmatic rules are honoured and validated (null rule, null range, null separator, divisor).</summary>
    [TestMethod]
    public void OrdinalComposition_Programmatic_IsHonouredAndValidated()
    {
        var source = Build("");
        var options = new NumberToStringConverterOptions(source) { OrdinalCompositionRules = [new OrdinalCompositionRule("1010", 1000, " ")] };
        Assert.AreEqual("thousandth tenth", new NumberToStringConverter(options).ConvertOrdinal(1010));
        Assert.AreEqual("one thousand ten oneth", new NumberToStringConverter(options).ConvertOrdinal(1011));

        foreach (var invalid in new List<OrdinalCompositionRule?> { null, new(null!, 1000, " "), new("1010", 1000, null!), new("1010", 1, " ") })
        {
            options.OrdinalCompositionRules = [invalid!];
            Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
        }
    }

    /// <summary>The converter snapshots the rules: later changes to the source list are not observed.</summary>
    [TestMethod]
    public void OrdinalComposition_Programmatic_IsSnapshotted()
    {
        var source = Build("");
        var rules = new List<OrdinalCompositionRule> { new("1010", 1000, " ") };
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(source) { OrdinalCompositionRules = rules });
        rules[0] = new OrdinalCompositionRule("1010", 1000, "+");
        rules.Add(new OrdinalCompositionRule("1011", 1000, "+"));

        Assert.AreEqual("thousandth tenth", converter.ConvertOrdinal(1010));
        Assert.AreEqual(1, converter.OrdinalCompositionRules.Count);
    }

    // ── Cloning and baseOn ──────────────────────────────────────────────────────────────────────

    /// <summary>Cloning a converter through its options preserves its composition rules.</summary>
    [TestMethod]
    public void OrdinalComposition_OptionsClone_PreservesTheRules()
    {
        var original = Build("""<OrdinalComposition for="1001..999999" divisor="1000" separator=" " />""");

        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(original));

        Assert.AreEqual("thousandth tenth", clone.ConvertOrdinal(1010));
        CollectionAssert.AreEqual(original.OrdinalCompositionRules.ToArray(), clone.OrdinalCompositionRules.ToArray());
    }

    /// <summary>A child without composition rules inherits the parent's; a child declaring its own replaces them.</summary>
    [TestMethod]
    public void OrdinalComposition_BaseOn_InheritsOrReplacesTheList()
    {
        string parent = NewCulture();
        string inheriting = NewCulture();
        string replacing = NewCulture();
        string document = NumberToStringOrdinalScaleTests.Document(parent,
            Ordinals("""<OrdinalComposition for="1001..999999" divisor="1000" separator=" " />"""),
            $"""
            <Language baseOn="{parent}"><Culture>{inheriting}</Culture><Ordinals suffix="TH" /></Language>
            <Language baseOn="{parent}"><Culture>{replacing}</Culture><Ordinals><OrdinalComposition for="1010" divisor="1000" separator="-" /></Ordinals></Language>
            """);

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("thousandTH tenTH", converters[inheriting].ConvertOrdinal(1010));
        Assert.AreEqual("thousandth-tenth", converters[replacing].ConvertOrdinal(1010));
        Assert.AreEqual("one thousand ten oneth", converters[replacing].ConvertOrdinal(1011));
    }

    /// <summary>Wraps finalized texts in angle brackets, to observe the number of finalizations.</summary>
    private sealed class WrappingLanguageSpecifics : INumberToStringLanguageSpecifics
    {
        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text) => "<" + text + ">";
    }

    /// <summary>An ordinal plugin forming only ten, to observe that composed parts consult the plugin.</summary>
    private sealed class TenPlugin : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
    {
        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text) => text;

        /// <inheritdoc />
        public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
            => TryConvertOrdinal((long)number, activeVariants, out result);

        /// <inheritdoc />
        public bool TryConvertOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
        {
            result = number == 10 ? "TEN" : null;
            return result is not null;
        }
    }
}
