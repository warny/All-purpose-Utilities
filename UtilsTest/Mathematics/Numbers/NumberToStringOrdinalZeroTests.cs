using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-12 engine tests: the declarative ordinal pipeline must never return the cardinal zero
/// unchanged. Zero succeeds only through an explicit ordinal formation (exception, word rule,
/// suffix, prefix or plugin); otherwise it fails closed. Non-zero values whose ordinal is
/// intentionally identical to the cardinal keep working.
/// </summary>
[TestClass]
public class NumberToStringOrdinalZeroTests
{
    /// <summary>Builds a synthetic language with the given ordinal section and optional variants.</summary>
    /// <param name="ordinals">The complete <c>&lt;Ordinals&gt;</c> element.</param>
    /// <param name="variants">An optional <c>&lt;Variants&gt;</c> element.</param>
    /// <returns>The configured converter.</returns>
    private static NumberToStringConverter Build(string ordinals, string variants = "")
    {
        string culture = $"ORDZERO-{Guid.NewGuid():N}";
        string document = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999">
                <Culture>{culture}</Culture>
                <Groups><Group level="1"><Digit digit="0" string=""/><Digit digit="1" string="one"/><Digit digit="2" string="two"/><Digit digit="3" string="three"/><Digit digit="4" string="four"/><Digit digit="5" string="five"/><Digit digit="6" string="six"/><Digit digit="7" string="seven"/><Digit digit="8" string="eight"/><Digit digit="9" string="nine"/></Group></Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string=""/></StaticNames><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>
                {variants}
                {ordinals}
              </Language>
            </Numbers>
            """;
        return NumberToStringConverter.ReadConfiguration(document)[culture];
    }

    /// <summary>A gender dimension used by the variant tests.</summary>
    private const string GenderDimension = """<Variants><Dimension name="gender" values="m,f" /></Variants>""";

    /// <summary>Ordinal section with exceptions only, none of which covers zero.</summary>
    private const string ExceptionsOnly = """<Ordinals><OrdinalException value="1" string="first" /></Ordinals>""";

    /// <summary>Zero with an ordinal section that has no formation for it fails closed.</summary>
    [TestMethod]
    public void Zero_WithoutAnyFormation_IsNotSupported()
    {
        var converter = Build(ExceptionsOnly);

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0));
    }

    /// <summary>A word rule that matches nothing in "zero" is not a formation for zero.</summary>
    [TestMethod]
    public void Zero_WithUnmatchedWordRules_IsNotSupported()
    {
        var converter = Build("""<Ordinals><Ordinal from="three" to="third" /></Ordinals>""");

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0));
        Assert.AreEqual("third", converter.ConvertOrdinal(3));
    }

    /// <summary>An explicit ordinal exception for zero is used.</summary>
    [TestMethod]
    public void Zero_WithOrdinalException_Succeeds()
    {
        var converter = Build("""<Ordinals><OrdinalException value="0" string="zeroth" /></Ordinals>""");

        Assert.AreEqual("zeroth", converter.ConvertOrdinal(0));
    }

    /// <summary>A word rule matching the zero word is an explicit formation.</summary>
    [TestMethod]
    public void Zero_WithMatchingWordRule_Succeeds()
    {
        var converter = Build("""<Ordinals><Ordinal from="zero" to="zeroth" /></Ordinals>""");

        Assert.AreEqual("zeroth", converter.ConvertOrdinal(0));
    }

    /// <summary>A configured suffix is an explicit formation.</summary>
    [TestMethod]
    public void Zero_WithSuffix_Succeeds()
    {
        var converter = Build("""<Ordinals suffix="th" />""");

        Assert.AreEqual("zeroth", converter.ConvertOrdinal(0));
    }

    /// <summary>A prefix-only ordinal is an explicit formation (e.g. Chinese 第).</summary>
    [TestMethod]
    public void Zero_WithPrefixOnly_Succeeds()
    {
        var converter = Build("""<Ordinals prefix="no. " />""");

        Assert.AreEqual("no. zero", converter.ConvertOrdinal(0));
        Assert.AreEqual("no. five", converter.ConvertOrdinal(5));
    }

    /// <summary>A plugin that handles zero explicitly wins.</summary>
    [TestMethod]
    public void Zero_HandledByPlugin_Succeeds()
    {
        var converter = WithPlugin(Build(ExceptionsOnly), new ZeroPlugin(handlesZero: true));

        Assert.AreEqual("plugin-zero", converter.ConvertOrdinal(0));
    }

    /// <summary>A plugin that declines zero falls back to a non-forming declarative pipeline and fails closed.</summary>
    [TestMethod]
    public void Zero_DeclinedByPluginWithoutDeclarativeFormation_IsNotSupported()
    {
        var converter = WithPlugin(Build(ExceptionsOnly), new ZeroPlugin(handlesZero: false));

        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0));
        Assert.AreEqual("first", converter.ConvertOrdinal(1));
    }

    /// <summary>A non-zero ordinal deliberately identical to its cardinal remains allowed.</summary>
    [TestMethod]
    public void NonZero_UnchangedCardinal_RemainsAllowed()
    {
        var converter = Build(ExceptionsOnly);

        Assert.AreEqual("five", converter.ConvertOrdinal(5));
    }

    /// <summary>A variant-specific zero exception only serves the matching variant.</summary>
    [TestMethod]
    public void Zero_VariantException_ServesOnlyThatVariant()
    {
        var converter = Build("""
            <Ordinals>
              <OrdinalException value="1" string="first" />
              <OrdinalVariants>
                <Variant type="gender" variant="f"><OrdinalException value="0" string="zeroth-f" /></Variant>
              </OrdinalVariants>
            </Ordinals>
            """, GenderDimension);

        Assert.AreEqual("zeroth-f", converter.ConvertOrdinal(0, "gender=f"));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0, "gender=m"));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0));
    }

    /// <summary>A variant-specific suffix forms zero for that variant only.</summary>
    [TestMethod]
    public void Zero_VariantSuffix_FormsOnlyThatVariant()
    {
        var converter = Build("""
            <Ordinals>
              <OrdinalException value="1" string="first" />
              <OrdinalVariants>
                <Variant type="gender" variant="f" suffix="-f" />
              </OrdinalVariants>
            </Ordinals>
            """, GenderDimension);

        Assert.AreEqual("zero-f", converter.ConvertOrdinal(0, "gender=f"));
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertOrdinal(0, "gender=m"));
    }

    /// <summary>The int, long and BigInteger overloads agree for zero.</summary>
    [TestMethod]
    public void Zero_AllOverloads_Agree()
    {
        var failing = Build(ExceptionsOnly);
        Assert.ThrowsExactly<NotSupportedException>(() => failing.ConvertOrdinal(0));
        Assert.ThrowsExactly<NotSupportedException>(() => failing.ConvertOrdinal(0L));
        Assert.ThrowsExactly<NotSupportedException>(() => failing.ConvertOrdinal(BigInteger.Zero));

        var forming = Build("""<Ordinals suffix="th" />""");
        Assert.AreEqual("zeroth", forming.ConvertOrdinal(0));
        Assert.AreEqual("zeroth", forming.ConvertOrdinal(0L));
        Assert.AreEqual("zeroth", forming.ConvertOrdinal(BigInteger.Zero));
    }

    /// <summary>Clones a converter with a replacement language-specifics plugin.</summary>
    private static NumberToStringConverter WithPlugin(NumberToStringConverter source, INumberToStringLanguageSpecifics plugin)
        => new(new NumberToStringConverterOptions(source) { LanguageSpecifics = plugin });

    /// <summary>Test plugin that either handles zero or declines every value.</summary>
    /// <param name="handlesZero">Whether zero is handled.</param>
    private sealed class ZeroPlugin(bool handlesZero) : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
    {
        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text) => text;

        /// <inheritdoc />
        public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
        {
            result = number == 0 && handlesZero ? "plugin-zero" : null;
            return result != null;
        }
    }
}
