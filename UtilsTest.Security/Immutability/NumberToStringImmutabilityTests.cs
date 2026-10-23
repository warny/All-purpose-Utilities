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

    /// <summary>
    /// NTS-30: changing the internal-zero-group text and the currency forced variants of the options after construction
    /// does not affect the converter, whose snapshot is exposed read-only.
    /// </summary>
    [TestMethod]
    public void InternalZeroGroupAndCurrencyContext_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var vnOptions = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("VN"));
        var vn = new NumberToStringConverter(vnOptions);
        vnOptions.InternalZeroGroupString = null;
        Assert.AreEqual("không", vn.InternalZeroGroupString);
        Assert.AreEqual("một triệu không nghìn không trăm linh một", vn.Convert(new BigInteger(1_000_001)));

        var esOptions = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("ES"));
        var es = new NumberToStringConverter(esOptions);
        esOptions.CurrencyForcedVariants = ForcedVariantSet.Empty;
        var euro = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euros", SubunitSingular = "céntimo", SubunitPlural = "céntimos" };
        Assert.IsFalse(es.CurrencyForcedVariants.IsEmpty);
        Assert.AreEqual("un euro", es.ConvertCurrency(1m, euro));
    }

    /// <summary>
    /// NTS-51: changing the currency terminal-scale connector, its scales or the currency replacements of the options
    /// after construction, or the list the rules came from, does not affect the converter, whose snapshot is exposed
    /// read-only.
    /// </summary>
    [TestMethod]
    public void CurrencyPhrase_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var euro = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euros", SubunitSingular = "céntimo", SubunitPlural = "céntimos" };
        var esOptions = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("ES"));
        var es = new NumberToStringConverter(esOptions);
        esOptions.CurrencyTerminalScaleConnector = "del";
        esOptions.CurrencyTerminalScales = "1..";
        Assert.AreEqual("de", es.CurrencyTerminalScaleConnector);
        Assert.AreEqual("2..", es.CurrencyTerminalScales);
        Assert.AreEqual("un millón de euros", es.ConvertCurrency(1_000_000m, euro));
        Assert.AreEqual("mil euros", es.ConvertCurrency(1_000m, euro));

        var italianEuro = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euro", SubunitSingular = "centesimo", SubunitPlural = "centesimi" };
        var rules = new List<CurrencyReplacementRule>(NumberToStringConverter.GetConverter("IT").CurrencyReplacements);
        var itOptions = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("IT")) { CurrencyReplacements = rules };
        var it = new NumberToStringConverter(itOptions);
        rules.Clear();
        itOptions.CurrencyReplacements = [];
        Assert.AreEqual(1, it.CurrencyReplacements.Count);
        Assert.IsFalse(it.CurrencyReplacements is List<CurrencyReplacementRule>, "The converter must not expose a mutable list.");
        Assert.IsFalse(it.CurrencyReplacements[0].ExceptBeforeNoun is List<string>, "The rule must not expose a mutable list.");
        Assert.AreEqual("un euro", it.ConvertCurrency(1m, italianEuro));
    }

    /// <summary>
    /// NTS-52: the currency noun connector snapshots its substitutions and their noun beginnings in the order supplied;
    /// mutating the source sequences or the options afterwards changes neither the connector nor the converter, and no
    /// mutable list is exposed.
    /// </summary>
    [TestMethod]
    public void CurrencyNounConnector_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var euro = new CurrencyDefinition { UnitSingular = "euro", UnitPlural = "euros", SubunitSingular = "centime", SubunitPlural = "centimes" };
        var franc = new CurrencyDefinition { UnitSingular = "franc", UnitPlural = "francs", SubunitSingular = "centime", SubunitPlural = "centimes" };
        var scales = NumberToStringConverter.ParseRangeExpression("2..");
        var prefixes = new List<string> { "e" };
        var rules = new List<CurrencyNounConnectorRule>
        {
            new("de", terminalScales: scales),
            new("d'", "", scales, beforeNoun: prefixes),
        };
        var connector = new CurrencyNounConnector("", null, rules);
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("FR")) { CurrencyNounConnector = connector };
        var fr = new NumberToStringConverter(options);
        rules.Clear();
        prefixes[0] = "f";
        options.CurrencyNounConnector = new CurrencyNounConnector("XX");
        Assert.AreSame(connector, fr.CurrencyNounConnector);
        Assert.AreEqual(2, connector.Substitutions.Count);
        Assert.AreEqual("e", connector.Substitutions[1].BeforeNoun[0]);
        Assert.IsFalse(connector.Substitutions is List<CurrencyNounConnectorRule>, "The connector must not expose a mutable list.");
        Assert.IsFalse(connector.Substitutions[1].BeforeNoun is List<string>, "The rule must not expose a mutable list.");
        Assert.AreEqual("un-million d'euros", fr.ConvertCurrency(1_000_000m, euro));
        Assert.AreEqual("un-million de francs", fr.ConvertCurrency(1_000_000m, franc));
        Assert.AreEqual("mille euros", fr.ConvertCurrency(1_000m, euro));
    }

    /// <summary>
    /// NTS-53: the scale connector snapshots its substitutions in the order supplied; mutating the source sequence or the
    /// options afterwards changes neither the rules nor the converter, and no mutable list is exposed.
    /// </summary>
    [TestMethod]
    public void ScaleConnectorRules_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var list = new List<ScaleConnectorRule>
        {
            new("", NumberToStringConverter.ParseRangeExpression("1..19"), 100),
        };
        var rules = new ScaleConnectorRules("", list);
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("RO")) { ScaleConnectorRules = rules };
        var ro = new NumberToStringConverter(options);
        list.Clear();
        list.Add(new ScaleConnectorRule("XX", NumberToStringConverter.ParseRangeExpression("0..")));
        options.ScaleConnectorRules = new ScaleConnectorRules("YY");
        options.ScaleConnector = "ZZ";
        Assert.AreSame(rules, ro.ScaleConnectorRules);
        Assert.AreEqual(1, rules.Substitutions.Count);
        Assert.IsFalse(rules.Substitutions is List<ScaleConnectorRule>, "The rules must not expose a mutable list.");
        Assert.AreEqual("de", ro.ScaleConnector);
        Assert.AreEqual("o sută nouăsprezece mii", ro.Convert(119_000));
        Assert.AreEqual("o sută douăzeci de mii", ro.Convert(120_000));
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

    /// <summary>
    /// Mutating the source generated scale forms (the options dictionary, the forced-variant dictionary handed
    /// to an entry) after construction affects neither the entry nor the converter, and the exposed views are
    /// not mutable collections.
    /// </summary>
    [TestMethod]
    public void GeneratedScaleForms_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        string[] digits = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];
        var forced = new Dictionary<string, ForcedVariantSet> { ["other"] = ForcedVariantSet.Parse("gender=other") };
        var entry = new GeneratedScaleForm(LexicalFormSet.Create(("one", "X1"), ("other", "XO")), new OneOtherSelector(), forced);
        var generated = new Dictionary<int, GeneratedScaleForm> { [0] = entry };
        var options = Options([]);
        options.Scale = new NumberScale(["", "thousand"], ["X"], 0, "n", "-", digits, digits, digits, digits);
        options.VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["plain", "other"])];
        options.VariantRules =
        [
            new NumberToStringConverter.VariantRule(
                new Dictionary<string, string> { ["gender"] = "other" },
                [new NumberToStringConverter.ReplacementRule("two", "TWO", ReplacementScope.LastWord)]),
        ];
        options.GeneratedScaleForms = generated;
        var converter = new NumberToStringConverter(options);

        forced["other"] = ForcedVariantSet.Empty;
        forced["one"] = ForcedVariantSet.Parse("gender=other");
        generated[0] = new GeneratedScaleForm(LexicalFormSet.Create(("one", "changed"), ("other", "changed")), new AlwaysOtherSelector());
        generated.Clear();
        options.GeneratedScaleForms = null;

        Assert.AreEqual("one a-X1", converter.Convert(BigInteger.Pow(100, 2)));
        Assert.AreEqual("TWO a-XO", converter.Convert(2 * BigInteger.Pow(100, 2)));
        Assert.AreEqual(1, entry.MultiplierForcedVariants.Count);
        Assert.IsFalse(converter.GeneratedScaleForms is Dictionary<int, GeneratedScaleForm>, "The converter must not expose a mutable dictionary.");
        Assert.IsFalse(entry.MultiplierForcedVariants is Dictionary<string, ForcedVariantSet>, "The entry must not expose a mutable dictionary.");
    }

    /// <summary>
    /// Mutating the source variants forced by exact scale forms (the outer dictionary and the per-form dictionary)
    /// after construction does not affect the converter, and the exposed views are not mutable collections.
    /// </summary>
    [TestMethod]
    public void ScaleFormMultiplierForcedVariants_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var perForm = new Dictionary<string, ForcedVariantSet> { ["other"] = ForcedVariantSet.Parse("gender=other") };
        var forced = new Dictionary<int, IReadOnlyDictionary<string, ForcedVariantSet>> { [1] = perForm };
        var options = Options([]);
        options.ScaleForms = new Dictionary<int, LexicalFormSet> { [1] = LexicalFormSet.Create(("one", "grand"), ("other", "grands")) };
        options.ScaleFormSelectors = new Dictionary<int, ILexicalFormSelector> { [1] = new OneOtherSelector() };
        options.VariantDimensions = [new NumberToStringConverter.VariantDimension("gender", ["plain", "other"])];
        options.VariantRules =
        [
            new NumberToStringConverter.VariantRule(
                new Dictionary<string, string> { ["gender"] = "other" },
                [new NumberToStringConverter.ReplacementRule("two", "TWO", ReplacementScope.LastWord)]),
        ];
        options.ScaleFormMultiplierForcedVariants = forced;
        var converter = new NumberToStringConverter(options);

        perForm["other"] = ForcedVariantSet.Empty;
        perForm["one"] = ForcedVariantSet.Parse("gender=other");
        forced[1] = new Dictionary<string, ForcedVariantSet>();
        forced.Clear();
        options.ScaleFormMultiplierForcedVariants = null;

        Assert.AreEqual("TWO grands", converter.Convert(new BigInteger(200)));
        Assert.AreEqual("one grand", converter.Convert(new BigInteger(100)));
        Assert.AreEqual(1, converter.ScaleFormMultiplierForcedVariants[1].Count);
        Assert.IsFalse(converter.ScaleFormMultiplierForcedVariants is Dictionary<int, IReadOnlyDictionary<string, ForcedVariantSet>>,
            "The converter must not expose a mutable dictionary.");
        Assert.IsFalse(converter.ScaleFormMultiplierForcedVariants[1] is Dictionary<string, ForcedVariantSet>,
            "The converter must not expose a mutable per-form dictionary.");
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

    /// <summary>
    /// Mutating the source scale-scoped groups after construction (the list, its group dictionary, a
    /// digit list, a <see cref="DigitType"/> in place, the options themselves) does not affect the
    /// converter, and the exposed snapshot cannot alter its conversions.
    /// </summary>
    [TestMethod]
    public void ScaleScopedGroups_SourceMutationAfterConstruction_DoesNotChangeConverter()
    {
        var scopedUnits = new DigitListType { Digits = [.. Units.Select((u, i) => new DigitType(i, i == 2 ? "beta" : u))] };
        var scopedTens = new DigitListType { Digits = [.. Tens.Select((t, i) => new DigitType(i, t, i == 0 ? "*" : t + " *"))] };
        var scopedTables = new Dictionary<int, DigitListType> { [1] = scopedUnits, [2] = scopedTens };
        var scoped = new List<ScaleScopedGroups> { new("1", scopedTables) };
        var options = Options([]);
        options.ScaleScopedGroups = scoped;
        options.ScaleMultiplierPosition = ScaleMultiplierPosition.AfterScale;
        var converter = new NumberToStringConverter(options);

        scopedUnits.Digits[2].StringValue = "changed";
        scopedUnits.Digits.Add(new DigitType(2, "duplicate"));
        scopedTables[1] = new DigitListType { Digits = [.. Units.Select((u, i) => new DigitType(i, "replaced"))] };
        scoped.Add(new ScaleScopedGroups("2", scopedTables));
        options.ScaleScopedGroups = [];
        options.ScaleMultiplierPosition = ScaleMultiplierPosition.BeforeScale;

        Assert.AreEqual("thousand beta", converter.Convert(new BigInteger(200)));
        Assert.AreEqual(1, converter.ScaleScopedGroups.Count);

        converter.ScaleScopedGroups[0].Groups[1].Digits.Single(d => d.Digit == 2).StringValue = "exposed";
        converter.Groups[1][2].StringValue = "exposed";
        Assert.AreEqual("thousand beta", converter.Convert(new BigInteger(200)));
        Assert.AreEqual("two", converter.Convert(new BigInteger(2)));
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
