using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-53, the generic scale connector on synthetic languages that follow no real grammar:
/// <c>&lt;ScaleConnector default&gt;&lt;Substitution value numberRange numberModulo scales&gt;</c>
/// (<see cref="NumberToStringConverterOptions.ScaleConnectorRules"/>). A default, then contextual substitutions on the
/// whole multiplier of a scale group (optionally modulo, a paired multiplier included) and its scale index; the most
/// specific substitution wins and, at equal specificity, the first declared. The legacy
/// <c>scaleConnector</c>/<c>scaleConnectorThreshold</c> shorthand keeps its meaning and acts as the last declared
/// substitution.
/// </summary>
[TestClass]
public class NumberToStringNts53PrimitiveTests
{
    /// <summary>Unit words of the synthetic languages.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic languages.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Creates a unique culture name.</summary>
    /// <returns>The name.</returns>
    private static string NewName() => $"NTS53-{Guid.NewGuid():N}";

    /// <summary>
    /// Builds a synthetic configuration: groups of three digits, static scales thousand, million(s) and billion (or,
    /// when <paramref name="paired"/>, thousand and million(s) paired from the million), and a form dimension
    /// plain/bound whose "bound" form writes "one" as "an".
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="extra">Elements appended to the Language.</param>
    /// <param name="attributes">Attributes appended to the Language.</param>
    /// <param name="paired">Whether the scales are paired from the million.</param>
    /// <param name="extraLanguages">Further Language elements.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string extra = "", string attributes = "", bool paired = false, string extraLanguages = "")
    {
        string units = string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        string hundreds = string.Concat(Units.Select((u, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{u} hundred" buildString="{u} hundred *" />"""));
        string scale = paired
            ? """<NumberScale firstLetterUpperCase="false" pairedScalesFrom="2"><StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /><Scale value="2" string="milli(on|ons)" /></StaticNames></NumberScale>"""
            : """<NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /><Scale value="1" string="thousand" /><Scale value="2" string="milli(on|ons)" /><Scale value="3" string="billion" /></StaticNames></NumberScale>""";
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point"
                        fractionSeparator="over" maxNumber="999999999999" {attributes}>
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{units}</Group>
                  <Group level="2">{tens}</Group>
                  <Group level="3">{hundreds}</Group>
                </Groups>
                {scale}
                <Variants>
                  <Dimension name="form" values="plain,bound" />
                  <Variant type="form" variant="bound"><Replacement oldValue="one" newValue="an" scope="LastWord" /></Variant>
                </Variants>
                {extra}
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds a synthetic converter.</summary>
    /// <param name="extra">Elements appended to the Language.</param>
    /// <param name="attributes">Attributes appended to the Language.</param>
    /// <param name="paired">Whether the scales are paired from the million.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string extra = "", string attributes = "", bool paired = false)
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(Document(culture, extra, attributes, paired))[culture];
    }

    /// <summary>Builds a converter whose ScaleConnector element holds <paramref name="substitutions"/>.</summary>
    /// <param name="substitutions">The Substitution elements.</param>
    /// <param name="defaultValue">The default attribute, or <see langword="null"/> to leave it out.</param>
    /// <param name="paired">Whether the scales are paired from the million.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter With(string substitutions, string? defaultValue = "", bool paired = false)
        => Build(defaultValue is null
            ? $"<ScaleConnector>{substitutions}</ScaleConnector>"
            : $"<ScaleConnector default=\"{defaultValue}\">{substitutions}</ScaleConnector>", paired: paired);

    /// <summary>Parses a range expression.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The range.</returns>
    private static Utils.Range.IntRange<long> R(string expression) => NumberToStringConverter.ParseRangeExpression(expression);

    /// <summary>Values exercising every junction of the synthetic languages.</summary>
    private static readonly long[] Probes =
    [
        0, 1, 19, 20, 21, 999, 1_000, 2_000, 19_000, 20_000, 21_000, 100_000, 101_000, 119_000, 120_000, 121_000, 999_000,
        1_000_000, 2_000_000, 20_000_000, 119_000_000, 120_000_000, 2_000_000_000, 20_000_000_000, 120_119_020_001,
        999_999_999_999,
    ];

    // ===== Default ===================================================================================

    /// <summary>Without a ScaleConnector the junction is historical.</summary>
    [TestMethod]
    public void Absent_Historical()
    {
        var converter = Build();
        Assert.IsNull(converter.ScaleConnectorRules);
        Assert.IsNull(converter.ScaleConnector);
        Assert.AreEqual("twenty thousand", converter.Convert(20_000));
        Assert.AreEqual("two millions", converter.Convert(2_000_000));
    }

    /// <summary>An empty default without substitution gives the historical text.</summary>
    [TestMethod]
    public void EmptyDefault_Historical()
    {
        var historical = Build();
        foreach (var converter in new[] { With(""), With("", defaultValue: null) })
        {
            Assert.IsNotNull(converter.ScaleConnectorRules);
            Assert.AreEqual("", converter.ScaleConnectorRules!.Default);
            Assert.AreEqual(0, converter.ScaleConnectorRules.Substitutions.Count);
            foreach (long n in Probes)
                Assert.AreEqual(historical.Convert(n), converter.Convert(n), n.ToString());
        }
    }

    /// <summary>A non-empty default joins every multiplier to its scale noun, never the units.</summary>
    [TestMethod]
    public void NonEmptyDefault()
    {
        var converter = With("", defaultValue: "OF");
        Assert.AreEqual("one OF thousand", converter.Convert(1_000));
        Assert.AreEqual("two OF millions three", converter.Convert(2_000_003));
        Assert.AreEqual("twenty", converter.Convert(20));
    }

    // ===== Constraints ===============================================================================

    /// <summary>A range on the whole multiplier.</summary>
    [TestMethod]
    public void Range()
    {
        var converter = With("""<Substitution numberRange="20.." value="of" />""");
        Assert.AreEqual("ten nine thousand", converter.Convert(19_000));
        Assert.AreEqual("twenty of thousand", converter.Convert(20_000));
        Assert.AreEqual("one hundred ten nine of thousand", converter.Convert(119_000));
        Assert.AreEqual("twenty of millions twenty", converter.Convert(20_000_020));
    }

    /// <summary>The modulo tests the last digits of the multiplier and declares no constraint of its own.</summary>
    [TestMethod]
    public void Modulo()
    {
        var converter = With("""
            <Substitution numberRange="1..19" numberModulo="100" value="" />
            <Substitution numberRange="20.." value="of" />
            """);
        Assert.AreEqual("ten nine thousand", converter.Convert(19_000));
        Assert.AreEqual("twenty of thousand", converter.Convert(20_000));
        Assert.AreEqual("one hundred of thousand", converter.Convert(100_000));
        Assert.AreEqual("one hundred one thousand", converter.Convert(101_000));
        Assert.AreEqual("one hundred ten nine thousand", converter.Convert(119_000));
        Assert.AreEqual("one hundred twenty of thousand", converter.Convert(120_000));
        Assert.AreEqual("two hundred ten nine millions", converter.Convert(219_000_000));
        Assert.AreEqual(1, converter.ScaleConnectorRules!.Substitutions[0].Specificity);
        Assert.AreEqual(1, converter.ScaleConnectorRules.Substitutions[1].Specificity);
    }

    /// <summary>A scale constraint alone.</summary>
    [TestMethod]
    public void Scales()
    {
        var converter = With("""<Substitution scales="2" value="of" />""");
        Assert.AreEqual("two thousand", converter.Convert(2_000));
        Assert.AreEqual("two of millions", converter.Convert(2_000_000));
        Assert.AreEqual("two billion", converter.Convert(2_000_000_000));
    }

    /// <summary>Two constraints together must both hold.</summary>
    [TestMethod]
    public void TwoConstraints()
    {
        var converter = With("""<Substitution numberRange="20.." scales="2.." value="of" />""");
        Assert.AreEqual("twenty thousand", converter.Convert(20_000));
        Assert.AreEqual("two millions", converter.Convert(2_000_000));
        Assert.AreEqual("twenty of millions", converter.Convert(20_000_000));
        Assert.AreEqual("twenty of billion", converter.Convert(20_000_000_000));
        Assert.AreEqual(2, converter.ScaleConnectorRules!.Substitutions[0].Specificity);
    }

    /// <summary>No match and an empty default: the historical junction.</summary>
    [TestMethod]
    public void NoMatch_Default()
    {
        var converter = With("""<Substitution numberRange="500" value="of" />""", defaultValue: "BY");
        Assert.AreEqual("five hundred of thousand", converter.Convert(500_000));
        Assert.AreEqual("four hundred BY thousand", converter.Convert(400_000));
    }

    // ===== Resolution ================================================================================

    /// <summary>At equal specificity the first declared wins, whatever the values.</summary>
    [TestMethod]
    public void EqualSpecificity_FirstDeclaredWins()
    {
        var ab = With("""
            <Substitution numberRange="20.." value="A" />
            <Substitution numberRange="10..30" value="B" />
            """);
        var ba = With("""
            <Substitution numberRange="10..30" value="B" />
            <Substitution numberRange="20.." value="A" />
            """);
        Assert.AreEqual("twenty A thousand", ab.Convert(20_000));
        Assert.AreEqual("twenty B thousand", ba.Convert(20_000));
        Assert.AreEqual("ten B thousand", ab.Convert(10_000));
        Assert.AreEqual("forty A thousand", ba.Convert(40_000));
    }

    /// <summary>The most specific substitution wins, whatever the declaration order.</summary>
    [TestMethod]
    public void MostSpecificWins()
    {
        foreach (var converter in new[]
        {
            With("""
                <Substitution numberRange="20.." value="A" />
                <Substitution numberRange="20.." scales="2" value="B" />
                """),
            With("""
                <Substitution numberRange="20.." scales="2" value="B" />
                <Substitution numberRange="20.." value="A" />
                """),
        })
        {
            Assert.AreEqual("twenty A thousand", converter.Convert(20_000));
            Assert.AreEqual("twenty B millions", converter.Convert(20_000_000));
            Assert.AreEqual("twenty A billion", converter.Convert(20_000_000_000));
        }
    }

    /// <summary>The caller's variants do not take part in the decision.</summary>
    [TestMethod]
    public void IndependentOfVariants()
    {
        var converter = With("""<Substitution numberRange="20.." value="of" />""");
        Assert.AreEqual("twenty of thousand an", converter.Convert(20_001, "form=bound"));
        Assert.AreEqual("one thousand", converter.Convert(1_000, "form=bound"));
    }

    // ===== Paired multiplier =========================================================================

    /// <summary>
    /// The range of the named scale sees its whole paired multiplier (2500), neither its lower group (500) nor its upper
    /// one (2).
    /// </summary>
    [TestMethod]
    public void Paired_WholeMultiplier()
    {
        var whole = With("""<Substitution numberRange="2500" scales="2" value="of" />""", paired: true);
        var lower = With("""<Substitution numberRange="500" scales="2" value="of" />""", paired: true);
        var upper = With("""<Substitution numberRange="2" scales="2" value="of" />""", paired: true);
        Assert.AreEqual("two thousand five hundred of millions", whole.Convert(2_500_000_000));
        Assert.AreEqual("two thousand five hundred millions", lower.Convert(2_500_000_000));
        Assert.AreEqual("two thousand five hundred millions", upper.Convert(2_500_000_000));
        Assert.AreEqual("five hundred of millions", lower.Convert(500_000_000));
        Assert.AreEqual("two of millions", upper.Convert(2_000_000));
    }

    /// <summary>
    /// A paired multiplier is a complete cardinal: its own thousands group is decided on its own multiplier (2 of
    /// 2500), like any thousands group, and the named scale on the whole multiplier.
    /// </summary>
    [TestMethod]
    public void Paired_InnerThousandsDecidedOnTheirOwnMultiplier()
    {
        var converter = With("""<Substitution numberRange="2" value="of" />""", paired: true);
        Assert.AreEqual("two of thousand five hundred millions", converter.Convert(2_500_000_000));
        Assert.AreEqual("two of thousand", converter.Convert(2_000));
    }

    /// <summary>
    /// The modulo and the scale constraint on a paired multiplier: both rules have two constraints, so the exception
    /// declared first wins on 2119 (19 modulo 100); a one-constraint exception would lose against the two-constraint rule.
    /// </summary>
    [TestMethod]
    public void Paired_ModuloAndScales()
    {
        var weaker = With("""
            <Substitution numberRange="1..19" numberModulo="100" value="" />
            <Substitution numberRange="20.." scales="2" value="of" />
            """, paired: true);
        Assert.AreEqual("two thousand one hundred ten nine of millions", weaker.Convert(2_119_000_000));
        var converter = With("""
            <Substitution numberRange="1..19" numberModulo="100" scales="2" value="" />
            <Substitution numberRange="20.." scales="2" value="of" />
            """, paired: true);
        Assert.AreEqual("two thousand one hundred ten nine millions", converter.Convert(2_119_000_000));
        Assert.AreEqual("two thousand one hundred twenty of millions", converter.Convert(2_120_000_000));
        Assert.AreEqual("one thousand of millions", converter.Convert(1_000_000_000));
        Assert.AreEqual("twenty thousand", converter.Convert(20_000));
        var thousands = With("""<Substitution numberRange="20.." scales="1" value="of" />""", paired: true);
        Assert.AreEqual("one thousand millions", thousands.Convert(1_000_000_000));
        Assert.AreEqual("twenty of thousand", thousands.Convert(20_000));
    }

    // ===== Legacy shorthand ==========================================================================

    /// <summary>The legacy attributes are byte-identical to master and to the equivalent substitution.</summary>
    [TestMethod]
    public void Legacy_EquivalentToARangeSubstitution()
    {
        var legacy = Build(attributes: """scaleConnector="of" scaleConnectorThreshold="20" """);
        var rules = With("""<Substitution numberRange="20.." value="of" />""");
        Assert.AreEqual("of", legacy.ScaleConnector);
        Assert.AreEqual(20, legacy.ScaleConnectorThreshold);
        Assert.IsNull(legacy.ScaleConnectorRules);
        foreach (long n in Probes)
            Assert.AreEqual(rules.Convert(n), legacy.Convert(n), n.ToString());
        Assert.AreEqual("one hundred ten nine of thousand", legacy.Convert(119_000));
    }

    /// <summary>A shorthand set through the options behaves as before NTS-53, its empty connector included.</summary>
    [TestMethod]
    public void Legacy_Options_Historical()
    {
        var options = new NumberToStringConverterOptions(Build()) { ScaleConnector = "of", ScaleConnectorThreshold = 20 };
        var converter = new NumberToStringConverter(options);
        Assert.AreEqual("ten nine thousand", converter.Convert(19_000));
        Assert.AreEqual("twenty of thousand", converter.Convert(20_000));
        var empty = new NumberToStringConverter(new NumberToStringConverterOptions(Build()) { ScaleConnector = "", ScaleConnectorThreshold = 20 });
        Assert.AreEqual("twenty  thousand", empty.Convert(20_000));
        Assert.AreEqual("ten nine thousand", empty.Convert(19_000));
    }

    /// <summary>
    /// The declared substitutions refine the shorthand, which keeps its public values: they take precedence over it,
    /// and it takes precedence over the default.
    /// </summary>
    [TestMethod]
    public void Legacy_RefinedBySubstitutions()
    {
        var converter = Build("""
            <ScaleConnector default="BY"><Substitution numberRange="1..19" numberModulo="100" value="" /></ScaleConnector>
            """, attributes: """scaleConnector="of" scaleConnectorThreshold="20" """);
        Assert.AreEqual("of", converter.ScaleConnector);
        Assert.AreEqual(20, converter.ScaleConnectorThreshold);
        Assert.AreEqual(1, converter.ScaleConnectorRules!.Substitutions.Count);
        Assert.AreEqual("one hundred ten nine thousand", converter.Convert(119_000));
        Assert.AreEqual("one hundred twenty of thousand", converter.Convert(120_000));
        Assert.AreEqual("one hundred of thousand", converter.Convert(100_000));
        Assert.AreEqual("five thousand", converter.Convert(5_000));
        var withDefaultOnly = Build("""<ScaleConnector default="BY" />""", attributes: """scaleConnector="of" """);
        Assert.AreEqual("twenty of thousand", withDefaultOnly.Convert(20_000));
        Assert.AreEqual("two BY thousand", withDefaultOnly.Convert(2_000));
    }

    // ===== Validation ================================================================================

    /// <summary>The schema rejects what it can express.</summary>
    [TestMethod]
    [DataRow("""<Substitution numberRange="20.." />""")]
    [DataRow("""<Substitution numberRange="x" value="of" />""")]
    [DataRow("""<Substitution numberRange="-1" value="of" />""")]
    [DataRow("""<Substitution numberRange="" value="of" />""")]
    [DataRow("""<Substitution numberRange="1..19" numberModulo="1" value="of" />""")]
    [DataRow("""<Substitution numberRange="1..19" numberModulo="x" value="of" />""")]
    [DataRow("""<Substitution scales="0.." value="of" />""")]
    [DataRow("""<Substitution scales="..2" value="of" />""")]
    [DataRow("""<Substitution scales="" value="of" />""")]
    [DataRow("""<Substitution numberRange="20.." value="of" beforeNoun="a" />""")]
    [DataRow("""<Substitution numberRange="20.." value="of" unknown="1" />""")]
    [DataRow("""<Other />""")]
    public void Schema_RejectsMalformed(string substitution)
        => Assert.ThrowsExactly<XmlSchemaValidationException>(() => With(substitution));

    /// <summary>The schema rejects an unknown attribute of the element and a child of another kind.</summary>
    [TestMethod]
    public void Schema_RejectsMalformedElement()
    {
        Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build("""<ScaleConnector value="of" />"""));
        Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build("""<ScaleConnector><Replacement oldValue="a" newValue="b" /></ScaleConnector>"""));
    }

    /// <summary>The loader rejects what the schema cannot express.</summary>
    [TestMethod]
    [DataRow("""<Substitution value="of" />""")]
    [DataRow("""<Substitution numberRange="20.." value=" " />""")]
    [DataRow("""<Substitution numberRange="200.." numberModulo="100" value="of" />""")]
    [DataRow("""<Substitution numberModulo="100" value="of" />""")]
    public void Loader_RejectsInvalid(string substitution)
        => Assert.ThrowsExactly<ArgumentException>(() => With(substitution));

    /// <summary>A white-space default is rejected.</summary>
    [TestMethod]
    public void Loader_RejectsWhiteSpaceDefault()
        => Assert.ThrowsExactly<ArgumentException>(() => With("", defaultValue: " "));

    /// <summary>The public constructors validate their arguments.</summary>
    [TestMethod]
    public void Api_Validation()
    {
        var range = R("20..");
        Assert.ThrowsExactly<ArgumentNullException>(() => new ScaleConnectorRule(null!, range));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRule(" ", range));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRule("of"));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRule("of", R("..5")));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRule("of", R("..5") & R("10..")));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRule("of", numberModulo: 100));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRule("of", R("1"), numberModulo: 1));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRule("of", R("200.."), numberModulo: 100));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRule("of", scales: R("0..")));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRule("of", scales: R("1..2") & R("5..")));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ScaleConnectorRules(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRules(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new ScaleConnectorRules(substitutions: [null!]));
        var rule = new ScaleConnectorRule("", R("1..19"), 100, R("1.."));
        Assert.AreEqual("", rule.Value);
        Assert.AreEqual(100L, rule.NumberModulo);
        Assert.AreEqual(2, rule.Specificity);
    }

    // ===== baseOn, clone, immutability ===============================================================

    /// <summary>baseOn: an absent element inherits it; a declared one replaces it as a whole.</summary>
    [TestMethod]
    public void BaseOn_ReplacedAsAWhole()
    {
        string parent = NewName(), child = NewName(), replaced = NewName();
        var converters = NumberToStringConverter.ReadConfiguration(Document(parent,
            extra: """<ScaleConnector><Substitution numberRange="1..19" numberModulo="100" value="" /></ScaleConnector>""",
            attributes: """scaleConnector="of" """,
            extraLanguages: $"""
                <Language baseOn="{parent}"><Culture>{child}</Culture></Language>
                <Language baseOn="{parent}"><Culture>{replaced}</Culture><ScaleConnector default="" /></Language>
                """));
        Assert.AreEqual("one hundred ten nine thousand", converters[child].Convert(119_000));
        Assert.AreEqual("one hundred twenty of thousand", converters[child].Convert(120_000));
        Assert.AreEqual(1, converters[child].ScaleConnectorRules!.Substitutions.Count);
        Assert.AreEqual("of", converters[child].ScaleConnector);
        Assert.AreEqual("one hundred ten nine of thousand", converters[replaced].Convert(119_000));
        Assert.AreEqual(0, converters[replaced].ScaleConnectorRules!.Substitutions.Count);
    }

    /// <summary>Options → converter → options → converter keeps the rules, their order and the shorthand.</summary>
    [TestMethod]
    public void OptionsAndClone_KeepOrder()
    {
        var rules = new ScaleConnectorRules("", [
            new ScaleConnectorRule("FIRST", R("20..")),
            new ScaleConnectorRule("SECOND", R("20..")),
            new ScaleConnectorRule("", R("1..9"), 100),
        ]);
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(Build())
        {
            ScaleConnectorRules = rules,
            ScaleConnector = "of",
            ScaleConnectorThreshold = 10,
        });
        Assert.AreSame(rules, converter.ScaleConnectorRules);
        var options = new NumberToStringConverterOptions(converter);
        Assert.AreSame(rules, options.ScaleConnectorRules);
        Assert.AreEqual("of", options.ScaleConnector);
        Assert.AreEqual(10, options.ScaleConnectorThreshold);
        var clone = new NumberToStringConverter(options);
        foreach (long n in Probes)
            Assert.AreEqual(converter.Convert(n), clone.Convert(n), n.ToString());
        Assert.AreEqual("twenty FIRST thousand", clone.Convert(20_000));
        Assert.AreEqual("ten of thousand", clone.Convert(10_000));
        CollectionAssert.AreEqual(new[] { "FIRST", "SECOND", "" }, clone.ScaleConnectorRules!.Substitutions.Select(s => s.Value).ToArray());
    }

    /// <summary>Mutating the source sequence after construction changes neither the rules nor the converter.</summary>
    [TestMethod]
    public void Snapshot()
    {
        var list = new List<ScaleConnectorRule> { new("of", R("20..")) };
        var rules = new ScaleConnectorRules("", list);
        var options = new NumberToStringConverterOptions(Build()) { ScaleConnectorRules = rules };
        var converter = new NumberToStringConverter(options);
        list.Add(new ScaleConnectorRule("BY", R("0..")));
        options.ScaleConnectorRules = null;
        Assert.AreEqual(1, rules.Substitutions.Count);
        Assert.AreEqual("two thousand", converter.Convert(2_000));
        Assert.AreEqual("twenty of thousand", converter.Convert(20_000));
    }

    /// <summary>The internal zero group (no multiplier) of a language that renders one is decided on 0.</summary>
    [TestMethod]
    public void InternalZeroGroup_DecidedOnZero()
    {
        var converter = Build("""<ScaleConnector default="OF"><Substitution numberRange="0" value="" /></ScaleConnector>""",
            attributes: """internalZeroGroupString="nil" """);
        Assert.AreEqual("one OF million nil thousand one", converter.Convert(1_000_001));
    }

    /// <summary>A large paired multiplier is a <see cref="long"/>: no overflow up to the domain.</summary>
    [TestMethod]
    public void Paired_LargestMultiplier()
    {
        var converter = With("""<Substitution numberRange="999999" value="of" />""", paired: true);
        StringAssert.EndsWith(converter.Convert((BigInteger)999_999_000_000), " of millions");
    }
}
