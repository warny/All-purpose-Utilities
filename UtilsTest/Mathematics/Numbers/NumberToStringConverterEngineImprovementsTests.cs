using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Verifies language-neutral default-interface conversion contracts.</summary>
[TestClass]
public class NumberToStringConverterEngineImprovementsTests
{
    /// <summary>Verifies that the default double overload delegates to decimal conversion.</summary>
    [TestMethod]
    public void Convert_Double_DelegatesToDecimal()
    {
        INumberToStringConverter converter = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(converter.Convert(3.14m), converter.Convert(3.14));
        Assert.AreEqual(converter.Convert(2.5m), converter.Convert(2.5));
    }

    /// <summary>Verifies that the default float overload delegates consistently with double.</summary>
    [TestMethod]
    public void Convert_Float_DelegatesToDouble()
    {
        INumberToStringConverter converter = NumberToStringConverter.GetConverter("EN");
        Assert.AreEqual(converter.Convert(2.5), converter.Convert((float)2.5));
    }

    /// <summary>Verifies that variants survive float-overload delegation.</summary>
    [TestMethod]
    public void Convert_Float_WithVariants_DelegatesToDouble()
    {
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        var options = new NumberToStringConverterOptions(source)
        {
            VariantDimensions = [new NumberToStringConverter.VariantDimension("form", ["base", "alternate"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["form"] = "alternate" },
                    [new NumberToStringConverter.ReplacementRule(source.Convert(5), "ALT", ReplacementScope.Anywhere)]),
            ],
        };
        INumberToStringConverter converter = new NumberToStringConverter(options);

        string doubleResult = converter.Convert(2.5, "form=alternate");
        string floatResult = converter.Convert((float)2.5, "form=alternate");

        StringAssert.Contains(doubleResult, "ALT");
        Assert.AreEqual(doubleResult, floatResult);
    }

    /// <summary>Verifies the language-neutral fraction fallback supplied by the interface.</summary>
    [TestMethod]
    public void ConvertFraction_Interface_DefaultFallback()
    {
        INumberToStringConverter converter = new MinimalConverter();
        Assert.AreEqual("2 / 4", converter.ConvertFraction(2, 4));
    }

    /// <summary>Verifies that multiplicative conversion is unsupported by default.</summary>
    [TestMethod]
    public void SupportsMultiplicative_Interface_DefaultFalse()
    {
        INumberToStringConverter converter = new MinimalConverter();
        Assert.IsFalse(converter.SupportsMultiplicative);
        Assert.ThrowsExactly<NotSupportedException>(() => converter.ConvertMultiplicative(1));
    }

    /// <summary>Verifies that a group connector is used only when the lower group is below its threshold.</summary>
    [TestMethod]
    public void GroupConnector_ObservesConfiguredThreshold()
    {
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        var options = new NumberToStringConverterOptions(source)
        {
            GroupConnector = "LINK",
            GroupConnectorThreshold = 100,
        };
        var converter = new NumberToStringConverter(options);

        StringAssert.Contains(converter.Convert(1001), " LINK ");
        StringAssert.Contains(converter.Convert(1099), " LINK ");
        Assert.IsFalse(converter.Convert(1100).Contains(" LINK ", StringComparison.Ordinal));
        Assert.IsFalse(converter.Convert(1101).Contains(" LINK ", StringComparison.Ordinal));
    }

    /// <summary>Verifies that a null group connector disables connector injection and round-trips through options.</summary>
    [TestMethod]
    public void GroupConnector_NullDisablesInjectionAndRoundTrips()
    {
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        var enabled = new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            GroupConnector = "LINK",
            GroupConnectorThreshold = 100,
        });
        StringAssert.Contains(enabled.Convert(1001), " LINK ");

        var options = new NumberToStringConverterOptions(enabled) { GroupConnector = null };
        var disabled = new NumberToStringConverter(options);

        Assert.IsNull(new NumberToStringConverterOptions(disabled).GroupConnector);
        Assert.IsFalse(disabled.Convert(1001).Contains("LINK", StringComparison.Ordinal));
        Assert.AreEqual(100, options.GroupConnectorThreshold);
    }

    /// <summary>Verifies start-scoped replacements affect only text at the beginning.</summary>
    [TestMethod]
    public void ReplacementScope_StartsWith_AffectsOnlyBeginning()
    {
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        string token = source.Convert(1) + " ";
        var converter = WithReplacement(source, new(token, "PREFIX ", ReplacementScope.StartsWith));

        StringAssert.StartsWith(converter.Convert(100), "PREFIX ");
        Assert.AreEqual(source.Convert(21), converter.Convert(21));
    }

    /// <summary>Verifies end-scoped replacements affect only text at the end.</summary>
    [TestMethod]
    public void ReplacementScope_EndsWith_AffectsOnlyEnding()
    {
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        string token = source.Convert(1);
        var converter = WithReplacement(source, new(token, "SUFFIX", ReplacementScope.EndsWith));

        StringAssert.EndsWith(converter.Convert(21), "SUFFIX");
        Assert.AreEqual(source.Convert(100), converter.Convert(100));
    }

    /// <summary>Verifies start and end scopes are honored when replacements are selected by variants.</summary>
    [TestMethod]
    public void ReplacementScope_VariantRules_HonorStartAndEndScopes()
    {
        NumberToStringConverter source = NumberToStringConverter.GetConverter("EN");
        string one = source.Convert(1);
        var options = new NumberToStringConverterOptions(source)
        {
            VariantDimensions = [new NumberToStringConverter.VariantDimension("form", ["start", "end"])],
            VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["form"] = "start" },
                    [new NumberToStringConverter.ReplacementRule(one + " ", "PREFIX ", ReplacementScope.StartsWith)]),
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string> { ["form"] = "end" },
                    [new NumberToStringConverter.ReplacementRule(one, "SUFFIX", ReplacementScope.EndsWith)]),
            ],
        };
        var converter = new NumberToStringConverter(options);

        StringAssert.StartsWith(converter.Convert(100, "form=start"), "PREFIX ");
        StringAssert.EndsWith(converter.Convert(21, "form=end"), "SUFFIX");
    }

    /// <summary>Creates a converter with one additional global replacement rule.</summary>
    private static NumberToStringConverter WithReplacement(
        NumberToStringConverter source,
        NumberToStringConverter.ReplacementRule replacement)
        => new(new NumberToStringConverterOptions(source)
        {
            Replacements = source.Replacements.Append(replacement).ToList(),
        });

    /// <summary>Minimal language-neutral implementation used to exercise default interface members.</summary>
    private sealed class MinimalConverter : INumberToStringConverter
    {
        /// <summary>Gets the unrestricted maximum value.</summary>
        public BigInteger? MaxNumber => null;

        /// <summary>Formats a big integer as invariant digits.</summary>
        public string Convert(BigInteger number) => number.ToString();

        /// <summary>Formats an integer as invariant digits.</summary>
        public string Convert(int number) => number.ToString();

        /// <summary>Formats a long integer as invariant digits.</summary>
        public string Convert(long number) => number.ToString();

        /// <summary>Formats a decimal as invariant digits.</summary>
        public string Convert(decimal number) => number.ToString();
    }
}
