using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-30C: productive lexical agreement of the scale nouns that <see cref="NumberScale"/> generates
/// (<c>&lt;GeneratedScaleForm suffix="…"&gt;</c>, <see cref="NumberToStringConverterOptions.GeneratedScaleForms"/>).
/// The first half pins the language-independent engine contract on a synthetic two-suffix scale; the
/// second half pins the sourced Russian, Ukrainian, Bulgarian, Czech and Slovak forms (sources in
/// <c>docs/NTS-08-linguistic-sources.md</c>, NTS-30C section) on several generated scales, grouped
/// Conway names above the 999th prefix included.
/// </summary>
[TestClass]
public class NumberToStringNts30CTests
{
    // ===== synthetic engine ==========================================================================

    /// <summary>Unit words of the synthetic language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic language.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>Conway base prefixes 1–9 of the synthetic scale (prefix value 1 is "a", 2 is "b", …).</summary>
    private static readonly string[] Scale0 = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];

    /// <summary>Conway units components of the synthetic scale.</summary>
    private static readonly string[] UnitsPrefixes = ["", "un", "du", "tr", "qa", "qi", "sx", "sp", "oc", "no"];

    /// <summary>Conway tens components of the synthetic scale.</summary>
    private static readonly string[] TensPrefixes = ["", "de", "vi", "tg", "qd", "qq", "sg", "sq", "og", "ng"];

    /// <summary>Conway hundreds components of the synthetic scale.</summary>
    private static readonly string[] HundredsPrefixes = ["", "ce", "dc", "tc", "qc", "qn", "sc", "st", "oc", "nc"];

    /// <summary>Forms of suffix 0 ("X"), selected by the Russian (CLDR one/few/many) selector, without forced variants.</summary>
    private const string GeneratedX = """<GeneratedScaleForm suffix="0" formSelector="RussianPluralLexicalFormSelector"><Forms><Form key="one" value="X1" /><Form key="few" value="XF" /><Form key="many" value="XM" /></Forms></GeneratedScaleForm>""";

    /// <summary>Forms of suffix 1 ("Y"), whose one and few forms force gender b on the multiplier.</summary>
    private const string GeneratedY = """<GeneratedScaleForm suffix="1" formSelector="RussianPluralLexicalFormSelector"><Forms><Form key="one" value="Y1" forceVariants="gender=b" /><Form key="few" value="YF" forceVariants="gender=b" /><Form key="many" value="YM" /></Forms></GeneratedScaleForm>""";

    /// <summary>Test selector counting its calls and returning the CLDR ru category.</summary>
    private sealed class CountingSelector : ILexicalFormSelector
    {
        /// <summary>The number of calls received.</summary>
        public int Calls;

        /// <inheritdoc />
        public string SelectForm(LexicalFormContext context)
        {
            Interlocked.Increment(ref Calls);
            return new RussianPluralLexicalFormSelector().SelectForm(context);
        }
    }

    /// <summary>Test selector always returning a key no configuration declares.</summary>
    private sealed class UnknownKeySelector : ILexicalFormSelector
    {
        /// <inheritdoc />
        public string SelectForm(LexicalFormContext context) => "missing";
    }

    /// <summary>Test selector returning an undeclared key only under gender b, "many" otherwise.</summary>
    private sealed class VariantDependentSelector : ILexicalFormSelector
    {
        /// <inheritdoc />
        public string SelectForm(LexicalFormContext context)
            => context.Variants.TryGetValue("gender", out var gender) && gender == "b" ? "missing" : "many";
    }

    /// <summary>Language specifics counting the finalizations of public results.</summary>
    private sealed class CountingSpecifics : INumberToStringLanguageSpecifics
    {
        /// <summary>The number of finalizations.</summary>
        public int Calls;

        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text)
        {
            Interlocked.Increment(ref Calls);
            return text;
        }
    }

    /// <summary>Creates a unique culture or registration name.</summary>
    private static string NewName() => $"NTS30C-{Guid.NewGuid():N}";

    /// <summary>Renders a ten-entry digit table as XML digits.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The digit elements.</returns>
    private static string Digits(string[] table) => string.Concat(table.Select((s, i) => $"""<Digit digit="{i}" string="{s}" />"""));

    /// <summary>
    /// Creates a synthetic language: groups of two digits, scale 1 named "K", an unbounded Conway scale
    /// whose suffixes are "X" and "Y" (scale 2 = a-X, 3 = a-Y, 4 = b-X, 5 = b-Y, …, the prefix groups
    /// joined by "-", the void group "n"), and a gender dimension a/b turning one/two into oneB/twoB.
    /// </summary>
    /// <param name="culture">The culture identifier.</param>
    /// <param name="scaleContent">Elements appended inside the NumberScale (ScaleForm, GeneratedScaleForm).</param>
    /// <param name="suffixes">The Suffix elements.</param>
    /// <param name="extraLanguages">Additional Language elements.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string scaleContent = "", string suffixes = "<Suffix>X</Suffix><Suffix>Y</Suffix>", string extraLanguages = "")
    {
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="2" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point">
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{Digits(Units)}</Group>
                  <Group level="2">{tens}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false" voidGroup="n" groupSeparator="-">
                  <StaticNames><Scale value="0" string="" /><Scale value="1" string="K" /></StaticNames>
                  <Suffixes>{suffixes}</Suffixes>
                  <Scale0Prefixes>{Digits(Scale0)}</Scale0Prefixes>
                  <UnitsPrefixes>{Digits(UnitsPrefixes)}</UnitsPrefixes>
                  <TensPrefixes>{Digits(TensPrefixes)}</TensPrefixes>
                  <HundredsPrefixes>{Digits(HundredsPrefixes)}</HundredsPrefixes>
                  {scaleContent}
                </NumberScale>
                <Variants>
                  <Dimension name="gender" values="a,b" />
                  <Variant type="gender" variant="b">
                    <Replacement oldValue="one" newValue="oneB" scope="LastWord" />
                    <Replacement oldValue="two" newValue="twoB" scope="LastWord" />
                  </Variant>
                </Variants>
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds the synthetic converter.</summary>
    /// <param name="scaleContent">Elements appended inside the NumberScale.</param>
    /// <param name="suffixes">The Suffix elements.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string scaleContent = "", string suffixes = "<Suffix>X</Suffix><Suffix>Y</Suffix>")
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(Document(culture, scaleContent, suffixes))[culture];
    }

    /// <summary>Registers <paramref name="selector"/> under a fresh type name and returns that name.</summary>
    /// <param name="selector">The selector instance.</param>
    /// <returns>The registered type name.</returns>
    private static string Register(ILexicalFormSelector selector)
    {
        string name = NewName();
        NumberToStringConverter.RegisterLexicalFormSelector(name, selector);
        return name;
    }

    /// <summary>Returns <paramref name="multiplier"/> × 100^<paramref name="scale"/> (the synthetic groups have two digits).</summary>
    /// <param name="multiplier">The group multiplier.</param>
    /// <param name="scale">The scale index.</param>
    /// <returns>The value.</returns>
    private static BigInteger S(long multiplier, int scale) => multiplier * BigInteger.Pow(100, scale);

    /// <summary>Creates a Conway scale equal to the synthetic one but with upper-case base prefixes.</summary>
    /// <param name="suffixes">The suffixes.</param>
    /// <returns>The scale.</returns>
    private static NumberScale UpperCaseScale(params string[] suffixes)
        => new(["", "K"], suffixes, 0, "n", "-", [.. Scale0.Select(p => p.ToUpperInvariant())], UnitsPrefixes, TensPrefixes, HundredsPrefixes);

    /// <summary>Static names are untouched and the generated families follow their own suffix index.</summary>
    [TestMethod]
    public void Synthetic_EachSuffixIndexUsesItsFamily()
    {
        var converter = Build(GeneratedX + GeneratedY);

        Assert.AreEqual("two K", converter.Convert(S(2, 1)));
        Assert.AreEqual("one a-X1", converter.Convert(S(1, 2)));
        Assert.AreEqual("two a-XF", converter.Convert(S(2, 2)));
        Assert.AreEqual("five a-XM", converter.Convert(S(5, 2)));
        Assert.AreEqual("twenty one a-X1", converter.Convert(S(21, 2)));
        Assert.AreEqual("oneB a-Y1", converter.Convert(S(1, 3)));
        Assert.AreEqual("twoB a-YF", converter.Convert(S(2, 3)));
        Assert.AreEqual("five a-YM", converter.Convert(S(5, 3)));
        Assert.AreEqual("two b-XF", converter.Convert(S(2, 4)));
        Assert.AreEqual("twoB b-YF", converter.Convert(S(2, 5)));
    }

    /// <summary>Several Conway prefix values, a grouped one (1000 = a-n) included, share the family of their suffix.</summary>
    [TestMethod]
    public void Synthetic_FamiliesFollowEveryPrefixValue()
    {
        var converter = Build(GeneratedX + GeneratedY);

        // prefix value 12 = du + de; scale = 2 + 2 × (prefix − 1) + suffix index.
        Assert.AreEqual("five dude-XM", converter.Convert(S(5, 24)));
        Assert.AreEqual("twoB dude-YF", converter.Convert(S(2, 25)));
        // prefix value 1000 = "a", void group "n".
        Assert.AreEqual("two a-n-XF", converter.Convert(S(2, 2000)));
        Assert.AreEqual("oneB a-n-Y1", converter.Convert(S(1, 2001)));
        Assert.AreEqual("twenty five a-n-YM", converter.Convert(S(25, 2001)));
        Assert.AreEqual(2, converter.GeneratedScaleForms.Count, "one entry per suffix index, none per scale");
    }

    /// <summary>The forced variants apply to the multiplier of the selected form only, compounds included.</summary>
    [TestMethod]
    public void Synthetic_ForcedVariantsApplyToTheMultiplierOfTheSelectedForm()
    {
        var converter = Build(GeneratedX + GeneratedY);

        Assert.AreEqual("twenty oneB a-Y1", converter.Convert(S(21, 3)));
        Assert.AreEqual("twenty twoB a-YF", converter.Convert(S(22, 3)));
        Assert.AreEqual("ten two a-YM", converter.Convert(S(12, 3)));
        Assert.AreEqual("twenty two a-XF", converter.Convert(S(22, 2)));
        // The caller's own variant never reaches a multiplier whose form forces nothing (historical behaviour),
        // and a forced value wins over the caller's value for the same dimension.
        Assert.AreEqual("ten two a-YM", converter.Convert(S(12, 3), "gender=b"));
        Assert.AreEqual("twenty two a-XF", converter.Convert(S(22, 2), "gender=b"));
        Assert.AreEqual("twoB a-YF", converter.Convert(S(2, 3), "gender=a"));
        // The whole number keeps its own last-word variant pass.
        Assert.AreEqual("twoB a-YF twoB", converter.Convert(S(2, 3) + 2, "gender=b"));
    }

    /// <summary>An exact ScaleForm keeps precedence over the generated family of the same scale.</summary>
    [TestMethod]
    public void Synthetic_ExactScaleFormTakesPrecedence()
    {
        const string exact = """<ScaleForm scale="3" formSelector="RussianPluralLexicalFormSelector"><Forms><Form key="one" value="E1" /><Form key="few" value="EF" /><Form key="many" value="EM" /></Forms></ScaleForm>""";
        var converter = Build(exact + GeneratedX + GeneratedY);

        Assert.AreEqual("two EF", converter.Convert(S(2, 3)));
        Assert.AreEqual("one E1", converter.Convert(S(1, 3)));
        Assert.AreEqual("twoB b-YF", converter.Convert(S(2, 5)));
    }

    /// <summary>Without any generated form the historical name is produced byte for byte.</summary>
    [TestMethod]
    public void Synthetic_NoGeneratedForm_KeepsHistoricalOutput()
    {
        var historical = Build();
        var onlyX = Build(GeneratedX);

        Assert.AreEqual("two a-X", historical.Convert(S(2, 2)));
        Assert.AreEqual("two a-Y", historical.Convert(S(2, 3)));
        Assert.AreEqual("two a-n-Y", historical.Convert(S(2, 2001)));
        Assert.AreEqual(0, historical.GeneratedScaleForms.Count);
        Assert.AreEqual("two a-Y", onlyX.Convert(S(2, 3)), "a suffix without entry keeps the historical name");
        Assert.AreEqual("two a-XF", onlyX.Convert(S(2, 2)));
    }

    /// <summary>The selector runs once per generated scale noun, never again after the forced variants are applied.</summary>
    [TestMethod]
    public void Synthetic_SelectorRunsOncePerScaleNoun()
    {
        var selector = new CountingSelector();
        string name = Register(selector);
        var converter = Build($"""<GeneratedScaleForm suffix="1" formSelector="{name}"><Forms><Form key="one" value="Y1" forceVariants="gender=b" /><Form key="few" value="YF" forceVariants="gender=b" /><Form key="many" value="YM" /></Forms></GeneratedScaleForm>""");

        selector.Calls = 0;
        Assert.AreEqual("twoB a-YF", converter.Convert(S(2, 3)));
        Assert.AreEqual(1, selector.Calls);

        selector.Calls = 0;
        Assert.AreEqual("twoB b-YF five a-YM", converter.Convert(S(2, 5) + S(5, 3)));
        Assert.AreEqual(2, selector.Calls);
    }

    /// <summary>The selector receives the absolute multiplier: a negative value only adds the minus wrapper.</summary>
    [TestMethod]
    public void Synthetic_NegativeValue_OnlyAddsMinus()
    {
        var converter = Build(GeneratedX + GeneratedY);

        Assert.AreEqual("minus twoB a-YF", converter.Convert(-S(2, 3)));
        Assert.AreEqual("minus twenty one a-X1", converter.Convert(-S(21, 2)));
    }

    /// <summary>A public conversion through a generated scale is finalized exactly once.</summary>
    [TestMethod]
    public void Synthetic_GeneratedScale_IsFinalizedOnce()
    {
        var specifics = new CountingSpecifics();
        var options = new NumberToStringConverterOptions(Build(GeneratedX + GeneratedY)) { LanguageSpecifics = specifics };
        var converter = new NumberToStringConverter(options);

        specifics.Calls = 0;
        Assert.AreEqual("twoB b-YF twenty one a-X1 five", converter.Convert(S(2, 5) + S(21, 2) + 5));
        Assert.AreEqual(1, specifics.Calls);
    }

    // ----- validation ------------------------------------------------------------------------------

    /// <summary>A selector returning a key without form is rejected while loading.</summary>
    [TestMethod]
    public void Synthetic_UnknownFormKey_IsRejectedAtLoad()
    {
        string name = Register(new UnknownKeySelector());
        var exception = Assert.ThrowsExactly<NumberToStringConfigurationException>(() =>
            Build($"""<GeneratedScaleForm suffix="0" formSelector="{name}"><Forms><Form key="one" value="X1" /></Forms></GeneratedScaleForm>"""));
        Assert.AreEqual("UNTS007", exception.ErrorCode);
        StringAssert.Contains(exception.Message, "missing");
    }

    /// <summary>A key that only a caller variant can produce still fails deterministically when it is reached.</summary>
    [TestMethod]
    public void Synthetic_UnknownFormKeyUnderVariant_ThrowsUnts007()
    {
        string name = Register(new VariantDependentSelector());
        var converter = Build($"""<GeneratedScaleForm suffix="0" formSelector="{name}"><Forms><Form key="many" value="XM" /></Forms></GeneratedScaleForm>""");

        Assert.AreEqual("two a-XM", converter.Convert(S(2, 2)));
        var exception = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => converter.Convert(S(2, 2), "gender=b"));
        Assert.AreEqual("UNTS007", exception.ErrorCode);
    }

    /// <summary>A suffix index outside the NumberScale suffix list is rejected.</summary>
    [TestMethod]
    public void Synthetic_SuffixIndexOutOfRange_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Build("""<GeneratedScaleForm suffix="2"><Forms><Form key="singular" value="Z" /></Forms></GeneratedScaleForm>"""));
        Assert.Throws<ArgumentException>(() => Build("""<GeneratedScaleForm suffix="1"><Forms><Form key="singular" value="Z" /></Forms></GeneratedScaleForm>""", "<Suffix>X</Suffix>"));
    }

    /// <summary>A negative suffix index is rejected by the schema.</summary>
    [TestMethod]
    public void Synthetic_NegativeSuffixIndex_IsRejectedBySchema()
        => Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build("""<GeneratedScaleForm suffix="-1"><Forms><Form key="singular" value="Z" /></Forms></GeneratedScaleForm>"""));

    /// <summary>Two entries for the same suffix index in one language are rejected.</summary>
    [TestMethod]
    public void Synthetic_DuplicateSuffixIndex_IsRejected()
        => Assert.Throws<ArgumentException>(() => Build(GeneratedX + GeneratedX));

    /// <summary>A forced dimension or value the language does not declare is rejected while loading.</summary>
    [TestMethod]
    public void Synthetic_UnknownForcedVariant_IsRejected()
    {
        var dimension = Assert.ThrowsExactly<NumberToStringConfigurationException>(() =>
            Build("""<GeneratedScaleForm suffix="0" formSelector="RussianPluralLexicalFormSelector"><Forms><Form key="one" value="X1" forceVariants="case=b" /><Form key="few" value="XF" /><Form key="many" value="XM" /></Forms></GeneratedScaleForm>"""));
        Assert.AreEqual("UNTS006", dimension.ErrorCode);
        var value = Assert.ThrowsExactly<NumberToStringConfigurationException>(() =>
            Build("""<GeneratedScaleForm suffix="0" formSelector="RussianPluralLexicalFormSelector"><Forms><Form key="one" value="X1" forceVariants="gender=c" /><Form key="few" value="XF" /><Form key="many" value="XM" /></Forms></GeneratedScaleForm>"""));
        Assert.AreEqual("UNTS006", value.ErrorCode);
    }

    /// <summary>An empty suffix form is rejected while loading.</summary>
    [TestMethod]
    public void Synthetic_EmptySuffixForm_IsRejected()
        => Assert.ThrowsExactly<NumberToStringConfigurationException>(() =>
            Build("""<GeneratedScaleForm suffix="0"><Forms><Form key="singular" value="" /><Form key="plural" value="XP" /></Forms></GeneratedScaleForm>"""));

    /// <summary>A generated form on a scale without dynamic generation is rejected.</summary>
    [TestMethod]
    public void Programmatic_GeneratedFormWithoutDynamicScale_IsRejected()
    {
        var options = new NumberToStringConverterOptions(Build())
        {
            Scale = new NumberScale(["", "K", "M"], []),
            MaxNumber = 999999,
            GeneratedScaleForms = new Dictionary<int, GeneratedScaleForm> { [0] = new(LexicalFormSet.Create(("singular", "on"), ("plural", "ons"))) },
        };
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
    }

    /// <summary>The programmatic entry type validates its own content.</summary>
    [TestMethod]
    public void Programmatic_EntryValidation()
    {
        var forms = LexicalFormSet.Create(("one", "Z1"), ("many", "ZM"));
        Assert.Throws<ArgumentNullException>(() => new GeneratedScaleForm(null!));
        Assert.Throws<ArgumentException>(() => new GeneratedScaleForm(LexicalFormSet.Empty));
        Assert.Throws<ArgumentException>(() => new GeneratedScaleForm(forms, null,
            new Dictionary<string, ForcedVariantSet> { ["few"] = ForcedVariantSet.Parse("gender=b") }));
        Assert.Throws<ArgumentException>(() => new GeneratedScaleForm(forms, null,
            new Dictionary<string, ForcedVariantSet> { ["one"] = null! }));

        var entry = new GeneratedScaleForm(forms);
        Assert.IsInstanceOfType<DefaultLexicalFormSelector>(entry.Selector);
        Assert.AreEqual(0, entry.MultiplierForcedVariants.Count);
    }

    /// <summary>Invalid programmatic keys and values are rejected by the converter.</summary>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    public void Programmatic_InvalidSuffixIndex_IsRejected(int index)
    {
        var options = new NumberToStringConverterOptions(Build())
        {
            GeneratedScaleForms = new Dictionary<int, GeneratedScaleForm> { [index] = new(LexicalFormSet.Create(("singular", "Z"), ("plural", "Zs"))) },
        };
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));

        options.GeneratedScaleForms = new Dictionary<int, GeneratedScaleForm> { [0] = null! };
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
    }

    // ----- programmatic API, clone, inheritance --------------------------------------------------

    /// <summary>Programmatic generated forms, forced variants included, are honoured.</summary>
    [TestMethod]
    public void Programmatic_GeneratedForms_AreHonoured()
    {
        var options = new NumberToStringConverterOptions(Build())
        {
            GeneratedScaleForms = new Dictionary<int, GeneratedScaleForm>
            {
                [1] = new(LexicalFormSet.Create(("one", "Z1"), ("few", "ZF"), ("many", "ZM")), new RussianPluralLexicalFormSelector(),
                    new Dictionary<string, ForcedVariantSet> { ["few"] = ForcedVariantSet.Parse("gender=b") }),
            },
        };
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("twoB a-ZF", converter.Convert(S(2, 3)));
        Assert.AreEqual("one a-Z1", converter.Convert(S(1, 3)));
        Assert.AreEqual("two a-X", converter.Convert(S(2, 2)));
    }

    /// <summary>A clone keeps the generated forms and recomputes the names from a replaced scale.</summary>
    [TestMethod]
    public void Clone_KeepsFormsAndFollowsAReplacedScale()
    {
        var original = Build(GeneratedX + GeneratedY);

        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(original));
        Assert.AreEqual("twoB a-YF", clone.Convert(S(2, 3)));
        Assert.AreSame(original.GeneratedScaleForms[1], clone.GeneratedScaleForms[1]);

        var rescaled = new NumberToStringConverter(new NumberToStringConverterOptions(original) { Scale = UpperCaseScale("X", "Y") });
        Assert.AreEqual("twoB A-YF", rescaled.Convert(S(2, 3)));
        Assert.AreEqual("five B-XM", rescaled.Convert(S(5, 4)));

        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(new NumberToStringConverterOptions(original) { Scale = UpperCaseScale("X") }));
    }

    /// <summary>Under baseOn the entries merge by suffix index: the child replaces one, inherits the other.</summary>
    [TestMethod]
    public void BaseOn_MergesBySuffixIndex()
    {
        string parent = NewName();
        string child = NewName();
        string document = Document(parent, GeneratedX + GeneratedY, extraLanguages: $"""
            <Language baseOn="{parent}"><Culture>{child}</Culture><NumberScale><GeneratedScaleForm suffix="1" formSelector="RussianPluralLexicalFormSelector"><Forms><Form key="one" value="W1" /><Form key="few" value="WF" /><Form key="many" value="WM" /></Forms></GeneratedScaleForm></NumberScale></Language>
            """);

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("two a-XF", converters[child].Convert(S(2, 2)));
        Assert.AreEqual("two a-WF", converters[child].Convert(S(2, 3)));
        Assert.AreEqual("twoB a-YF", converters[parent].Convert(S(2, 3)));
    }

    /// <summary>A child declaring the same suffix index twice is rejected even though the parent declares it too.</summary>
    [TestMethod]
    public void BaseOn_DuplicateInChild_IsRejected()
    {
        string parent = NewName();
        string document = Document(parent, GeneratedX, extraLanguages: $"""
            <Language baseOn="{parent}"><Culture>{NewName()}</Culture><NumberScale>{GeneratedY}{GeneratedY}</NumberScale></Language>
            """);
        Assert.Throws<ArgumentException>(() => NumberToStringConverter.ReadConfiguration(document));
    }

    // ===== languages =================================================================================

    /// <summary>Converts <paramref name="value"/> with the built-in converter of <paramref name="culture"/>.</summary>
    /// <param name="culture">The culture name.</param>
    /// <param name="value">The value.</param>
    /// <param name="variants">The optional variants.</param>
    /// <returns>The cardinal.</returns>
    private static string C(string culture, BigInteger value, params string[] variants)
        => NumberToStringConverter.GetConverter(culture).Convert(value, variants);

    /// <summary>Returns <paramref name="multiplier"/> × 1000^<paramref name="scale"/>.</summary>
    /// <param name="multiplier">The group multiplier.</param>
    /// <param name="scale">The scale index (4 = 10^12).</param>
    /// <returns>The value.</returns>
    private static BigInteger T(long multiplier, int scale) => multiplier * BigInteger.Pow(1000, scale);

    /// <summary>A generated scale of a language: its index and the part of its name before the suffix.</summary>
    /// <param name="Index">The scale index.</param>
    /// <param name="Stem">The Conway prefix and junction, as generated.</param>
    private sealed record GeneratedScale(int Index, string Stem);

    /// <summary>Asserts a matrix: every multiplier, every scale, the expected suffix form of the multiplier's category.</summary>
    /// <param name="culture">The culture.</param>
    /// <param name="scales">The generated scales of one suffix family.</param>
    /// <param name="multipliers">The multiplier, its rendered numeral and its form key.</param>
    /// <param name="suffixForms">The suffix form of each key.</param>
    private static void AssertFamily(string culture, IEnumerable<GeneratedScale> scales,
        IEnumerable<(int Value, string Numeral, string Key)> multipliers, IReadOnlyDictionary<string, string> suffixForms)
    {
        foreach (var scale in scales)
            foreach (var (value, numeral, key) in multipliers)
                Assert.AreEqual($"{numeral} {scale.Stem}{suffixForms[key]}", C(culture, T(value, scale.Index)), $"{culture} {value} × 1000^{scale.Index}");
    }

    /// <summary>The built-in configurations declare one generated entry per suffix family and keep the low exact scales.</summary>
    [TestMethod]
    public void Configurations_DeclareOneEntryPerSuffixFamily()
    {
        foreach (var (culture, suffixes, exact, selectorType) in new (string, int[], int[], Type)[]
        {
            ("RU", [0], [1, 2, 3], typeof(RussianPluralLexicalFormSelector)),
            ("UK", [0], [1, 2, 3], typeof(UkrainianPluralLexicalFormSelector)),
            ("BG", [0], [1, 2, 3], typeof(DefaultLexicalFormSelector)),
            ("CS", [0, 1], [1, 2, 3], typeof(CzechSlovakPluralLexicalFormSelector)),
            ("SK", [0, 1], [2, 3], typeof(CzechSlovakPluralLexicalFormSelector)),
        })
        {
            var converter = NumberToStringConverter.GetConverter(culture);
            CollectionAssert.AreEquivalent(suffixes, converter.GeneratedScaleForms.Keys.ToArray(), culture);
            CollectionAssert.AreEquivalent(exact, converter.ScaleForms.Keys.ToArray(), culture);
            foreach (int suffix in suffixes)
                Assert.IsInstanceOfType(converter.GeneratedScaleForms[suffix].Selector, selectorType, $"{culture} suffix {suffix}");
        }
    }

    /// <summary>Languages outside NTS-30C declare no generated form (PL is bounded at 999 999).</summary>
    [TestMethod]
    public void OtherLanguages_DeclareNoGeneratedForm()
    {
        foreach (string culture in new[] { "PL", "HR", "EN", "FR", "DE", "IT", "ES", "SR", "SL" })
            Assert.AreEqual(0, NumberToStringConverter.GetConverter(culture).GeneratedScaleForms.Count, culture);
        Assert.AreEqual(999999, (int)NumberToStringConverter.GetConverter("PL").MaxNumber!.Value);
    }

    // ----- East Slavic ---------------------------------------------------------------------------

    /// <summary>The CLDR ru/uk multipliers of the matrix with their category.</summary>
    private static readonly int[] EastSlavicMultipliers = [1, 2, 4, 5, 11, 12, 14, 21, 22, 24, 25, 101, 102, 111, 112];

    /// <summary>Returns the CLDR ru/uk category of a multiplier.</summary>
    /// <param name="value">The multiplier.</param>
    /// <returns>The key.</returns>
    private static string EastSlavicKey(int value) => new RussianPluralLexicalFormSelector().SelectForm(new LexicalFormContext(value, new Dictionary<string, string>()));

    /// <summary>Russian masculine numerals of the matrix multipliers.</summary>
    private static readonly Dictionary<int, string> RussianNumerals = new()
    {
        [1] = "один", [2] = "два", [4] = "четыре", [5] = "пять", [11] = "одиннадцать", [12] = "двенадцать", [14] = "четырнадцать",
        [21] = "двадцать один", [22] = "двадцать два", [24] = "двадцать четыре", [25] = "двадцать пять", [101] = "сто один",
        [102] = "сто два", [111] = "сто одиннадцать", [112] = "сто двенадцать",
    };

    /// <summary>Ukrainian masculine numerals of the matrix multipliers.</summary>
    private static readonly Dictionary<int, string> UkrainianNumerals = new()
    {
        [1] = "один", [2] = "два", [4] = "чотири", [5] = "п'ять", [11] = "одинадцять", [12] = "дванадцять", [14] = "чотирнадцять",
        [21] = "двадцять один", [22] = "двадцять два", [24] = "двадцять чотири", [25] = "двадцять п'ять", [101] = "сто один",
        [102] = "сто два", [111] = "сто одинадцять", [112] = "сто дванадцять",
    };

    /// <summary>Russian generated scales: 10^12, 10^15, 10^18, 10^33 (prefix 10), 10^63 (prefix 20) and 10^3003 (prefix 1000).</summary>
    private static readonly GeneratedScale[] RussianScales =
        [new(4, "трилли"), new(5, "квадрилли"), new(6, "квинтилли"), new(11, "децилли"), new(22, "вигинтилли"), new(1001, "миллинилли")];

    /// <summary>Russian: триллион / триллиона / триллионов after the CLDR ru category, the masculine multiplier unchanged.</summary>
    [TestMethod]
    public void Russian_GeneratedScales_AgreeWithTheMultiplier()
        => AssertFamily("RU", RussianScales, EastSlavicMultipliers.Select(m => (m, RussianNumerals[m], EastSlavicKey(m))),
            new Dictionary<string, string> { ["one"] = "он", ["few"] = "она", ["many"] = "онов" });

    /// <summary>Ukrainian generated scales, same indices as Russian.</summary>
    private static readonly GeneratedScale[] UkrainianScales =
        [new(4, "триль"), new(5, "квадриль"), new(6, "квінтиль"), new(11, "дециль"), new(22, "вігінтиль"), new(1001, "мільніль")];

    /// <summary>Ukrainian: трильйон / трильйони / трильйонів after the CLDR uk category, the masculine multiplier unchanged.</summary>
    [TestMethod]
    public void Ukrainian_GeneratedScales_AgreeWithTheMultiplier()
        => AssertFamily("UK", UkrainianScales, EastSlavicMultipliers.Select(m => (m, UkrainianNumerals[m], EastSlavicKey(m))),
            new Dictionary<string, string> { ["one"] = "йон", ["few"] = "йони", ["many"] = "йонів" });

    // ----- Bulgarian -----------------------------------------------------------------------------

    /// <summary>Bulgarian generated scales.</summary>
    private static readonly GeneratedScale[] BulgarianScales =
        [new(4, "трили"), new(5, "квадрили"), new(6, "квинтили"), new(11, "децили"), new(22, "вигинтили"), new(1001, "милинили")];

    /// <summary>
    /// Bulgarian: the singular after exactly one, the count form трилиона otherwise, a masculine multiplier
    /// (един, два), compounds included; the missing conjunction и of the compounds is NTS-30D.
    /// </summary>
    [TestMethod]
    public void Bulgarian_GeneratedScales_AgreeWithTheMultiplier()
        => AssertFamily("BG", BulgarianScales,
            [(1, "един", "singular"), (2, "два", "plural"), (5, "пет", "plural"), (10, "десет", "plural"),
             (21, "двадесет един", "plural"), (22, "двадесет два", "plural"), (25, "двадесет пет", "plural")],
            new Dictionary<string, string> { ["singular"] = "он", ["plural"] = "она" });

    // ----- Czech ---------------------------------------------------------------------------------

    /// <summary>The CS/SK multipliers of the matrix.</summary>
    private static readonly int[] WestSlavicMultipliers = [1, 2, 3, 4, 5, 21, 22, 24, 25];

    /// <summary>Returns the CLDR cs/sk category of a multiplier.</summary>
    /// <param name="value">The multiplier.</param>
    /// <returns>The key.</returns>
    private static string WestSlavicKey(int value) => new CzechSlovakPluralLexicalFormSelector().SelectForm(new LexicalFormContext(value, new Dictionary<string, string>()));

    /// <summary>Czech -on scales (10^12, 10^24, 10^36, prefix 10 at 10^120, prefix 1000 at 10^12000).</summary>
    private static readonly GeneratedScale[] CzechOnScales =
        [new(4, "bili"), new(6, "trili"), new(8, "kvadrili"), new(20, "decili"), new(2000, "milinili")];

    /// <summary>Czech -arda scales (10^15, 10^27, 10^39, 10^123, 10^12003).</summary>
    private static readonly GeneratedScale[] CzechArdScales =
        [new(5, "bili"), new(7, "trili"), new(9, "kvadrili"), new(21, "decili"), new(2001, "milinili")];

    /// <summary>
    /// Czech -on family: masculine bilion (jeden bilion, dva biliony, pět bilionů); after a compound the
    /// genitive with the counting form of the numeral (dvacet jedna bilionů), as for milion (NTS-30B).
    /// </summary>
    [TestMethod]
    public void Czech_OnFamily_AgreesWithTheMultiplier()
        => AssertFamily("CS", CzechOnScales,
            WestSlavicMultipliers.Select(m => (m, m switch
            {
                1 => "jeden", 2 => "dva", 3 => "tři", 4 => "čtyři", 5 => "pět", 21 => "dvacet jedna", 22 => "dvacet dva",
                24 => "dvacet čtyři", _ => "dvacet pět",
            }, WestSlavicKey(m))),
            new Dictionary<string, string> { ["one"] = "on", ["few"] = "ony", ["other"] = "onů" });

    /// <summary>
    /// Czech -arda family: feminine biliarda (jedna biliarda, dvě biliardy, pět biliard); after a compound
    /// the genitive with the counting form (dvacet dva biliard), as for miliarda (NTS-30B).
    /// </summary>
    [TestMethod]
    public void Czech_ArdaFamily_AgreesWithTheMultiplier()
        => AssertFamily("CS", CzechArdScales,
            WestSlavicMultipliers.Select(m => (m, m switch
            {
                1 => "jedna", 2 => "dvě", 3 => "tři", 4 => "čtyři", 5 => "pět", 21 => "dvacet jedna", 22 => "dvacet dva",
                24 => "dvacet čtyři", _ => "dvacet pět",
            }, WestSlavicKey(m))),
            new Dictionary<string, string> { ["one"] = "arda", ["few"] = "ardy", ["other"] = "ard" });

    // ----- Slovak --------------------------------------------------------------------------------

    /// <summary>Slovak -ón scales.</summary>
    private static readonly GeneratedScale[] SlovakOnScales =
        [new(4, "bili"), new(6, "trili"), new(8, "kvadrili"), new(20, "decili"), new(2000, "milinili")];

    /// <summary>Slovak -arda scales.</summary>
    private static readonly GeneratedScale[] SlovakArdScales =
        [new(5, "bili"), new(7, "trili"), new(9, "kvadrili"), new(21, "decili"), new(2001, "milinili")];

    /// <summary>Slovak -ón family: bilión, dva bilióny, päť biliónov; an indeclinable one-word compound governs the genitive.</summary>
    [TestMethod]
    public void Slovak_OnFamily_AgreesWithTheMultiplier()
        => AssertFamily("SK", SlovakOnScales,
            WestSlavicMultipliers.Select(m => (m, m switch
            {
                1 => "jeden", 2 => "dva", 3 => "tri", 4 => "štyri", 5 => "päť", 21 => "dvadsaťjeden", 22 => "dvadsaťdva",
                24 => "dvadsaťštyri", _ => "dvadsaťpäť",
            }, WestSlavicKey(m))),
            new Dictionary<string, string> { ["one"] = "ón", ["few"] = "óny", ["other"] = "ónov" });

    /// <summary>Slovak -arda family: jedna biliarda, dve biliardy, päť biliárd, dvadsaťdva biliárd.</summary>
    [TestMethod]
    public void Slovak_ArdaFamily_AgreesWithTheMultiplier()
        => AssertFamily("SK", SlovakArdScales,
            WestSlavicMultipliers.Select(m => (m, m switch
            {
                1 => "jedna", 2 => "dve", 3 => "tri", 4 => "štyri", 5 => "päť", 21 => "dvadsaťjeden", 22 => "dvadsaťdva",
                24 => "dvadsaťštyri", _ => "dvadsaťpäť",
            }, WestSlavicKey(m))),
            new Dictionary<string, string> { ["one"] = "arda", ["few"] = "ardy", ["other"] = "árd" });

    // ----- anchors, low scales, composites -----------------------------------------------------

    /// <summary>Representative generated values, pinned as whole strings.</summary>
    [TestMethod]
    [DataRow("RU", "2000000000000", "два триллиона")]
    [DataRow("RU", "5000000000000", "пять триллионов")]
    [DataRow("RU", "21000000000000", "двадцать один триллион")]
    [DataRow("RU", "22000000000000", "двадцать два триллиона")]
    [DataRow("RU", "2000000000000000", "два квадриллиона")]
    [DataRow("UK", "2000000000000", "два трильйони")]
    [DataRow("UK", "5000000000000", "п'ять трильйонів")]
    [DataRow("UK", "21000000000000", "двадцять один трильйон")]
    [DataRow("UK", "22000000000000", "двадцять два трильйони")]
    [DataRow("BG", "1000000000000", "един трилион")]
    [DataRow("BG", "2000000000000", "два трилиона")]
    [DataRow("BG", "21000000000000", "двадесет един трилиона")]
    [DataRow("BG", "22000000000000", "двадесет два трилиона")]
    [DataRow("CS", "1000000000000", "jeden bilion")]
    [DataRow("CS", "2000000000000", "dva biliony")]
    [DataRow("CS", "5000000000000", "pět bilionů")]
    [DataRow("CS", "22000000000000", "dvacet dva bilionů")]
    [DataRow("CS", "1000000000000000", "jedna biliarda")]
    [DataRow("CS", "2000000000000000", "dvě biliardy")]
    [DataRow("CS", "5000000000000000", "pět biliard")]
    [DataRow("CS", "22000000000000000", "dvacet dva biliard")]
    [DataRow("SK", "1000000000000", "jeden bilión")]
    [DataRow("SK", "2000000000000", "dva bilióny")]
    [DataRow("SK", "5000000000000", "päť biliónov")]
    [DataRow("SK", "22000000000000", "dvadsaťdva biliónov")]
    [DataRow("SK", "1000000000000000", "jedna biliarda")]
    [DataRow("SK", "2000000000000000", "dve biliardy")]
    [DataRow("SK", "5000000000000000", "päť biliárd")]
    [DataRow("SK", "22000000000000000", "dvadsaťdva biliárd")]
    public void Anchors(string culture, string value, string expected)
        => Assert.AreEqual(expected, C(culture, BigInteger.Parse(value)));

    /// <summary>The exact low scales of NTS-27/NTS-30B are unchanged by the generated families.</summary>
    [TestMethod]
    [DataRow("RU", "2000", "две тысячи")]
    [DataRow("RU", "2000000", "два миллиона")]
    [DataRow("RU", "2000000000", "два миллиарда")]
    [DataRow("UK", "2000", "дві тисячі")]
    [DataRow("UK", "2000000", "два мільйони")]
    [DataRow("UK", "2000000000", "два мільярди")]
    [DataRow("BG", "2000", "две хиляди")]
    [DataRow("BG", "2000000", "два милиона")]
    [DataRow("BG", "2000000000", "два милиарда")]
    [DataRow("CS", "2000", "dva tisíce")]
    [DataRow("CS", "2000000", "dva miliony")]
    [DataRow("CS", "2000000000", "dvě miliardy")]
    [DataRow("CS", "22000000000", "dvacet dva miliard")]
    [DataRow("SK", "2000", "dvetisíc")]
    [DataRow("SK", "2000000", "dva milióny")]
    [DataRow("SK", "2000000000", "dve miliardy")]
    public void LowScales_AreUnchanged(string culture, string value, string expected)
        => Assert.AreEqual(expected, C(culture, BigInteger.Parse(value)));

    /// <summary>A negative value only adds the minus wrapper of the language.</summary>
    [TestMethod]
    [DataRow("RU", "-2000000000000", "минус два триллиона")]
    [DataRow("UK", "-22000000000000", "мінус двадцять два трильйони")]
    [DataRow("BG", "-2000000000000", "минус два трилиона")]
    [DataRow("CS", "-2000000000000000", "minus dvě biliardy")]
    [DataRow("SK", "-5000000000000000", "mínus päť biliárd")]
    public void NegativeValues_OnlyAddMinus(string culture, string value, string expected)
        => Assert.AreEqual(expected, C(culture, BigInteger.Parse(value)));

    /// <summary>A full number mixes generated, exact and unit groups in the same conversion.</summary>
    [TestMethod]
    public void FullNumbers_MixGeneratedAndExactScales()
    {
        Assert.AreEqual("два квадриллиона пять триллионов две тысячи один", C("RU", BigInteger.Parse("2005000000002001")));
        Assert.AreEqual("dvě biliardy tři biliony jeden milion dva", C("CS", BigInteger.Parse("2003000001000002")));
        Assert.AreEqual("dve biliardy dvadsaťjeden biliónov päťtisíc", C("SK", BigInteger.Parse("2021000000005000")));
    }

    /// <summary>Cardinal fragments reused by the composite surfaces carry the corrected generated noun.</summary>
    [TestMethod]
    public void CompositeSurfaces_UseTheCorrectedGeneratedNoun()
    {
        var russian = NumberToStringConverter.GetConverter("RU");
        StringAssert.StartsWith(russian.Convert(2000000000000.5m), "два триллиона");
        var czech = NumberToStringConverter.GetConverter("CS");
        StringAssert.StartsWith(czech.Convert(2000000000000000.25m), "dvě biliardy");
    }
}
