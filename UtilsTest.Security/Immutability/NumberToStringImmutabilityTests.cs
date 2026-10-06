using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.Security.Immutability;

/// <summary>
/// Verifies that a <see cref="NumberToStringConverter"/> keeps an immutable snapshot of the options it was built from.
/// </summary>
[TestClass]
public sealed class NumberToStringImmutabilityTests
{
    /// <summary>Units of the synthetic two-level test language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens of the synthetic two-level test language, indexed by digit.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Mutating the source fusion rule list after construction does not affect the converter snapshot.</summary>
    [TestMethod]
    public void Fusion_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var fusions = new List<FusionType> { new() { For = "1" } };
        var converter = new NumberToStringConverter(Options(fusions));

        fusions[0].Right = "changed";
        fusions.Add(new FusionType { For = "2" });

        Assert.AreEqual("twentyone", converter.Convert(new BigInteger(21)));
        Assert.AreEqual("twenty two", converter.Convert(new BigInteger(22)));
    }

    /// <summary>Mutating the source ordinal stem rule list after construction does not affect the converter snapshot.</summary>
    [TestMethod]
    public void OrdinalStem_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var stems = new List<OrdinalStemRule> { new("o", "") };
        var options = Options([]);
        options.OrdinalSuffix = "X";
        options.OrdinalStemRules = stems;
        var converter = new NumberToStringConverter(options);

        stems[0] = new OrdinalStemRule("o", "changed");
        stems.Add(new OrdinalStemRule("e", ""));
        options.OrdinalStemRules = [new OrdinalStemRule("o", "other")];

        Assert.AreEqual("twX", converter.ConvertOrdinal(2));
        Assert.AreEqual("threeX", converter.ConvertOrdinal(3));
        Assert.AreEqual(1, converter.OrdinalStemRules.Count);
        Assert.AreEqual(new OrdinalStemRule("o", ""), converter.OrdinalStemRules[0]);
        Assert.IsFalse(converter.OrdinalStemRules is List<OrdinalStemRule>, "The converter must not expose a mutable list.");
    }

    /// <summary>Selector returning "one" for a multiplier of one and "other" otherwise.</summary>
    private sealed class OneOtherSelector : ILexicalFormSelector
    {
        /// <inheritdoc />
        public string SelectForm(LexicalFormContext context) => context.AbsoluteValue == 1 ? "one" : "other";
    }

    /// <summary>Selector always returning "other".</summary>
    private sealed class AlwaysOtherSelector : ILexicalFormSelector
    {
        /// <inheritdoc />
        public string SelectForm(LexicalFormContext context) => "other";
    }

    /// <summary>Mutating the source scale form and selector dictionaries after construction does not affect the converter snapshot.</summary>
    [TestMethod]
    public void ScaleForms_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var forms = new Dictionary<int, LexicalFormSet> { [1] = LexicalFormSet.Create(("one", "grand"), ("other", "grands")) };
        var selectors = new Dictionary<int, ILexicalFormSelector> { [1] = new OneOtherSelector() };
        var options = Options([]);
        options.ScaleForms = forms;
        options.ScaleFormSelectors = selectors;
        var converter = new NumberToStringConverter(options);

        forms[1] = LexicalFormSet.Create(("one", "changed"), ("other", "changed"));
        selectors[1] = new AlwaysOtherSelector();
        forms.Clear();
        selectors.Clear();

        Assert.AreEqual("one grand", converter.Convert(new BigInteger(100)));
        Assert.AreEqual("two grands", converter.Convert(new BigInteger(200)));
        Assert.IsInstanceOfType<OneOtherSelector>(converter.ScaleFormSelectors[1]);
        Assert.IsFalse(converter.ScaleForms is Dictionary<int, LexicalFormSet>, "The converter must not expose a mutable dictionary.");
        Assert.IsFalse(converter.ScaleFormSelectors is Dictionary<int, ILexicalFormSelector>, "The converter must not expose a mutable dictionary.");
    }

    /// <summary>Mutating the source ordinal replacement lists after construction does not affect the converter snapshot.</summary>
    [TestMethod]
    public void OrdinalReplacements_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var baseReplacements = new List<NumberToStringConverter.ReplacementRule>
        {
            new("twenty one", "twenty-one", ReplacementScope.Standalone),
        };
        var variantReplacements = new List<NumberToStringConverter.ReplacementRule>
        {
            new("twenty-one", "twenty-first", ReplacementScope.Standalone),
        };
        var options = Options([]);
        options.OrdinalSuffix = "th";
        options.VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["plain", "other"])];
        options.OrdinalReplacements = baseReplacements;
        options.OrdinalVariants =
        [
            new NumberToStringConverter.OrdinalVariantRule(
                new Dictionary<string, string> { ["gender"] = "other" },
                new Dictionary<long, string>(), new Dictionary<string, string>(), null, null, variantReplacements),
        ];
        var converter = new NumberToStringConverter(options);

        baseReplacements[0] = new("twenty one", "changed", ReplacementScope.Standalone);
        baseReplacements.Add(new("x", "y", ReplacementScope.Anywhere));
        variantReplacements.Clear();

        Assert.AreEqual("twenty-oneth", converter.ConvertOrdinal(21));
        Assert.AreEqual("twenty-firstth", converter.ConvertOrdinal(21, "gender=other"));
        Assert.AreEqual(1, converter.OrdinalReplacements.Count);
        Assert.AreEqual(1, converter.OrdinalVariants[0].Replacements.Count);
        Assert.IsFalse(converter.OrdinalReplacements is List<NumberToStringConverter.ReplacementRule>, "The converter must not expose a mutable list.");
        Assert.IsFalse(converter.OrdinalVariants[0].Replacements is List<NumberToStringConverter.ReplacementRule>, "The variant must not expose a mutable list.");
    }

    /// <summary>Mutating the source ordinal scale rule list after construction does not affect the converter snapshot.</summary>
    [TestMethod]
    public void OrdinalScaleRules_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var rules = new List<OrdinalScaleRule> { new("1", "-") };
        var options = Options([]);
        options.OrdinalSuffix = "th";
        options.OrdinalScaleRules = rules;
        var converter = new NumberToStringConverter(options);

        rules[0] = new OrdinalScaleRule("1", "+");
        rules.Add(new OrdinalScaleRule("2", "+"));
        options.OrdinalScaleRules = [new OrdinalScaleRule("1", "*")];

        Assert.AreEqual("two-thousandth", converter.ConvertOrdinal(200));
        Assert.AreEqual("thousandth", converter.ConvertOrdinal(100));
        Assert.AreEqual(1, converter.OrdinalScaleRules.Count);
        Assert.AreEqual(new OrdinalScaleRule("1", "-"), converter.OrdinalScaleRules[0]);
        Assert.IsFalse(converter.OrdinalScaleRules is List<OrdinalScaleRule>, "The converter must not expose a mutable list.");
    }

    /// <summary>Mutating the source ordinal composition rule list after construction does not affect the converter snapshot.</summary>
    [TestMethod]
    public void OrdinalCompositionRules_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var rules = new List<OrdinalCompositionRule> { new("101..199", 100, " ") };
        var options = Options([]);
        options.OrdinalSuffix = "th";
        options.OrdinalScaleRules = [new OrdinalScaleRule("1", "")];
        options.OrdinalCompositionRules = rules;
        var converter = new NumberToStringConverter(options);

        rules[0] = new OrdinalCompositionRule("101..199", 100, "+");
        rules.Add(new OrdinalCompositionRule("201..299", 100, "+"));
        options.OrdinalCompositionRules = [new OrdinalCompositionRule("101..199", 100, "*")];

        Assert.AreEqual("thousandth oneth", converter.ConvertOrdinal(101));
        Assert.AreEqual("two thousand oneth", converter.ConvertOrdinal(201));
        Assert.AreEqual(1, converter.OrdinalCompositionRules.Count);
        Assert.AreEqual(new OrdinalCompositionRule("101..199", 100, " "), converter.OrdinalCompositionRules[0]);
        Assert.IsFalse(converter.OrdinalCompositionRules is List<OrdinalCompositionRule>, "The converter must not expose a mutable list.");
    }

    /// <summary>Creates options for a two-level language whose tens digit 2 carries <paramref name="fusions"/>.</summary>
    /// <param name="fusions">The fusion rules attached to the tens digit 2.</param>
    /// <returns>The converter options.</returns>
    private static NumberToStringConverterOptions Options(List<FusionType> fusions)
    {
        var units = new DigitListType { Digits = [.. Units.Select((u, i) => new DigitType(i, u))] };
        var tens = new DigitListType
        {
            Digits = [.. Tens.Select((t, i) => i == 2
                ? new DigitType(i, t, t + " *") { Fusions = fusions }
                : new DigitType(i, t, i == 0 ? "*" : t + " *"))],
        };
        return new NumberToStringConverterOptions
        {
            Group = 2,
            Zero = "zero",
            Minus = "minus *",
            Groups = new Dictionary<int, DigitListType> { [1] = units, [2] = tens },
            Scale = new NumberScale(["", "thousand"], ["illion"]),
        };
    }
}
