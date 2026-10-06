using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Xml.Schema;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Language-independent engine tests for scale lexical forms (<c>&lt;ScaleForm&gt;</c>,
/// <see cref="NumberToStringConverterOptions.ScaleForms"/> and
/// <see cref="NumberToStringConverterOptions.ScaleFormSelectors"/>): the historical default, form
/// selection by the group multiplier, the selector context, load-time validation, cloning,
/// <c>baseOn</c> merge and the absence of selector resolution on the conversion path.
/// </summary>
[TestClass]
public class NumberToStringScaleFormTests
{
    /// <summary>Unit words of the synthetic language.</summary>
    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>Tens words of the synthetic language.</summary>
    private static readonly string[] Tens = ["", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>
    /// Test selector mapping the multiplier to "one" (1), "dual" (2), "few" (3-10) and "many"
    /// (otherwise), recording every context it receives.
    /// </summary>
    private sealed class FourFormSelector : ILexicalFormSelector
    {
        /// <summary>Gets the contexts received, in call order.</summary>
        public ConcurrentQueue<LexicalFormContext> Contexts { get; } = new();

        /// <inheritdoc />
        public string SelectForm(LexicalFormContext context)
        {
            Contexts.Enqueue(context);
            return context.AbsoluteValue == 1 ? "one"
                : context.AbsoluteValue == 2 ? "dual"
                : context.AbsoluteValue <= 10 ? "few"
                : "many";
        }
    }

    /// <summary>Test selector always returning a key no configuration declares.</summary>
    private sealed class UnknownKeySelector : ILexicalFormSelector
    {
        /// <inheritdoc />
        public string SelectForm(LexicalFormContext context) => "missing";
    }

    /// <summary>Creates a unique culture or registration name.</summary>
    private static string NewName() => $"SCALEFORM-{Guid.NewGuid():N}";

    /// <summary>The four forms used with <see cref="FourFormSelector"/>.</summary>
    private const string FourForms = """<Forms><Form key="one" value="F1" /><Form key="dual" value="F2" /><Form key="few" value="F3" /><Form key="many" value="F4" /></Forms>""";

    /// <summary>Creates a synthetic two-level language whose scale 1 is "foo(s)".</summary>
    /// <param name="culture">The culture identifier.</param>
    /// <param name="scaleForms">The <c>&lt;ScaleForm&gt;</c> elements placed in the NumberScale.</param>
    /// <param name="extra">Additional elements appended to the Language (e.g. Variants).</param>
    /// <param name="extraLanguages">Additional Language elements.</param>
    /// <returns>The configuration document.</returns>
    private static string Document(string culture, string scaleForms = "", string extra = "", string extraLanguages = "")
    {
        string units = string.Concat(Units.Select((u, i) => $"""<Digit digit="{i}" string="{u}" />"""));
        string tens = string.Concat(Tens.Select((t, i) => i == 0
            ? """<Digit digit="0" string="" buildString="*" />"""
            : $"""<Digit digit="{i}" string="{t}" buildString="{t} *" />"""));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="2" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="9999">
                <Culture>{culture}</Culture>
                <Groups>
                  <Group level="1">{units}</Group>
                  <Group level="2">{tens}</Group>
                </Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string="" /><Scale value="1" string="foo(s)" /></StaticNames><Suffixes><Suffix>illion</Suffix></Suffixes>{scaleForms}</NumberScale>
                {extra}
              </Language>
              {extraLanguages}
            </Numbers>
            """;
    }

    /// <summary>Builds the synthetic converter.</summary>
    /// <param name="scaleForms">The <c>&lt;ScaleForm&gt;</c> elements.</param>
    /// <param name="extra">Additional Language elements.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Build(string scaleForms = "", string extra = "")
    {
        string culture = NewName();
        return NumberToStringConverter.ReadConfiguration(Document(culture, scaleForms, extra))[culture];
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

    /// <summary>Converts <paramref name="number"/> as a cardinal.</summary>
    private static string Cardinal(NumberToStringConverter converter, long number, params string[] variants)
        => converter.Convert((BigInteger)number, variants);

    /// <summary>Creates programmatic options equivalent to the synthetic language.</summary>
    /// <returns>The options.</returns>
    private static NumberToStringConverterOptions Options()
    {
        var units = new DigitListType { Digits = [.. Units.Select((u, i) => new DigitType(i, u))] };
        var tens = new DigitListType { Digits = [.. Tens.Select((t, i) => new DigitType(i, t, i == 0 ? "*" : t + " *"))] };
        return new NumberToStringConverterOptions
        {
            Group = 2,
            Zero = "zero",
            Minus = "minus *",
            Groups = new Dictionary<int, DigitListType> { [1] = units, [2] = tens },
            Scale = new NumberScale(["", "foo(s)"], ["illion"]),
            MaxNumber = 9999,
        };
    }

    // ── Historical default ──────────────────────────────────────────────────────────────────────

    /// <summary>Without any scale form the historical singular/plural scale name is used.</summary>
    [TestMethod]
    public void ScaleForms_Absent_KeepHistoricalSingularPlural()
    {
        var converter = Build();

        Assert.AreEqual("one foo", Cardinal(converter, 100));
        Assert.AreEqual("two foos", Cardinal(converter, 200));
        Assert.AreEqual("ten one foos one", Cardinal(converter, 1101));
        Assert.AreEqual(0, converter.ScaleForms.Count);
        Assert.AreEqual(0, converter.ScaleFormSelectors.Count);
    }

    /// <summary>Forms without a selector use the default selector over the overridden singular/plural.</summary>
    [TestMethod]
    public void ScaleForms_WithoutSelector_UseDefaultSelectorOverOverriddenForms()
    {
        var converter = Build("""<ScaleForm scale="1"><Forms><Form key="singular" value="bar" /><Form key="plural" value="bars" /></Forms></ScaleForm>""");

        Assert.AreEqual("one bar", Cardinal(converter, 100));
        Assert.AreEqual("two bars", Cardinal(converter, 200));
        Assert.IsInstanceOfType<DefaultLexicalFormSelector>(converter.ScaleFormSelectors[1]);
    }

    /// <summary>A selector alone keeps the synthesized singular/plural forms of the scale name.</summary>
    [TestMethod]
    public void ScaleForms_SelectorOnly_SynthesizesSingularPluralFromScaleName()
    {
        var converter = Build("""<ScaleForm scale="1" formSelector="default" />""");

        Assert.AreEqual("one foo", Cardinal(converter, 100));
        Assert.AreEqual("two foos", Cardinal(converter, 200));
        Assert.IsTrue(converter.ScaleForms.ContainsKey(1));
    }

    // ── Form selection ──────────────────────────────────────────────────────────────────────────

    /// <summary>The selector chooses the form key from the group multiplier; the word comes from the forms.</summary>
    [TestMethod]
    public void ScaleForms_Selector_ChoosesFormByMultiplier()
    {
        string selector = Register(new FourFormSelector());
        var converter = Build($"""<ScaleForm scale="1" formSelector="{selector}">{FourForms}</ScaleForm>""");

        Assert.AreEqual("one F1", Cardinal(converter, 100));
        Assert.AreEqual("two F2", Cardinal(converter, 200));
        Assert.AreEqual("five F3", Cardinal(converter, 500));
        Assert.AreEqual("ten F3", Cardinal(converter, 1000));
        Assert.AreEqual("ten one F4", Cardinal(converter, 1100));
        Assert.AreEqual("five F3 seven", Cardinal(converter, 507));
        Assert.AreEqual("seven", Cardinal(converter, 7));
    }

    /// <summary>The structured LexicalFormSelector element is honoured like the attribute.</summary>
    [TestMethod]
    public void ScaleForms_StructuredSelectorElement_IsHonoured()
    {
        string selector = Register(new FourFormSelector());
        var converter = Build($"""<ScaleForm scale="1"><LexicalFormSelector type="{selector}" />{FourForms}</ScaleForm>""");

        Assert.AreEqual("two F2", Cardinal(converter, 200));
    }

    /// <summary>The selector receives the group multiplier and the effective variant query.</summary>
    [TestMethod]
    public void ScaleForms_Selector_ReceivesMultiplierAndEffectiveVariants()
    {
        var recorder = new FourFormSelector();
        string selector = Register(recorder);
        var converter = Build(
            $"""<ScaleForm scale="1" formSelector="{selector}">{FourForms}</ScaleForm>""",
            """<Variants><Dimension name="gender" values="m,f" /></Variants>""");

        Cardinal(converter, 503);
        Cardinal(converter, 1203, "gender=f");

        var contexts = recorder.Contexts.ToArray();
        Assert.AreEqual(2, contexts.Length);
        Assert.AreEqual(new BigInteger(5), contexts[0].Value);
        Assert.AreEqual("m", contexts[0].Variants["gender"]);
        Assert.AreEqual(new BigInteger(12), contexts[1].Value);
        Assert.AreEqual("f", contexts[1].Variants["gender"]);
    }

    /// <summary>A form key absent from the configured forms is a deterministic UNTS007 error.</summary>
    [TestMethod]
    public void ScaleForms_UnknownFormKey_ThrowsUnts007()
    {
        string selector = Register(new UnknownKeySelector());
        var converter = Build($"""<ScaleForm scale="1" formSelector="{selector}" />""");

        var exception = Assert.ThrowsExactly<NumberToStringConfigurationException>(() => Cardinal(converter, 200));
        Assert.AreEqual("UNTS007", exception.ErrorCode);
        StringAssert.Contains(exception.Message, "missing");
        Assert.AreEqual("seven", Cardinal(converter, 7));
    }

    // ── Validation ──────────────────────────────────────────────────────────────────────────────

    /// <summary>A scale index the scale cannot name is rejected at construction.</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(3)]
    public void ScaleForms_InvalidScaleIndex_IsRejected(int index)
    {
        var options = Options();
        options.ScaleForms = new Dictionary<int, LexicalFormSet> { [index] = LexicalFormSet.Create(("singular", "x")) };
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));

        options = Options();
        options.ScaleFormSelectors = new Dictionary<int, ILexicalFormSelector> { [index] = new FourFormSelector() };
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
    }

    /// <summary>A scale index the configured scale cannot name is rejected when loading XML.</summary>
    [TestMethod]
    public void ScaleForms_XmlUnnameableScale_IsRejected()
        => Assert.Throws<ArgumentException>(() => Build("""<ScaleForm scale="3" formSelector="default" />"""));

    /// <summary>Scale index 0 (the units chunk, no noun) is rejected by the schema.</summary>
    [TestMethod]
    public void ScaleForms_XmlScaleZero_IsRejectedBySchema()
        => Assert.ThrowsExactly<XmlSchemaValidationException>(() => Build("""<ScaleForm scale="0" formSelector="default" />"""));

    /// <summary>Two entries for the same scale in one language are rejected.</summary>
    [TestMethod]
    public void ScaleForms_DuplicateScale_IsRejected()
        => Assert.Throws<ArgumentException>(() => Build("""<ScaleForm scale="1" formSelector="default" /><ScaleForm scale="1" formSelector="default" />"""));

    /// <summary>Null programmatic entries are rejected.</summary>
    [TestMethod]
    public void ScaleForms_NullProgrammaticEntries_AreRejected()
    {
        var options = Options();
        options.ScaleForms = new Dictionary<int, LexicalFormSet> { [1] = null! };
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));

        options = Options();
        options.ScaleFormSelectors = new Dictionary<int, ILexicalFormSelector> { [1] = null! };
        Assert.Throws<ArgumentException>(() => new NumberToStringConverter(options));
    }

    // ── Programmatic configuration, cloning and lifecycle ───────────────────────────────────────

    /// <summary>Programmatic forms and selectors are honoured.</summary>
    [TestMethod]
    public void ScaleForms_ProgrammaticOptions_AreHonoured()
    {
        var options = Options();
        options.ScaleForms = new Dictionary<int, LexicalFormSet> { [1] = LexicalFormSet.Create(("one", "F1"), ("dual", "F2"), ("few", "F3"), ("many", "F4")) };
        options.ScaleFormSelectors = new Dictionary<int, ILexicalFormSelector> { [1] = new FourFormSelector() };
        var converter = new NumberToStringConverter(options);

        Assert.AreEqual("two F2", Cardinal(converter, 200));
        Assert.AreEqual("ten one F4", Cardinal(converter, 1100));
    }

    /// <summary>Cloning a converter through its options preserves its scale forms and selectors.</summary>
    [TestMethod]
    public void ScaleForms_OptionsClone_PreservesFormsAndSelectors()
    {
        string selector = Register(new FourFormSelector());
        var original = Build($"""<ScaleForm scale="1" formSelector="{selector}">{FourForms}</ScaleForm>""");

        var clone = new NumberToStringConverter(new NumberToStringConverterOptions(original));

        Assert.AreEqual("two F2", Cardinal(clone, 200));
        Assert.AreEqual("five F3 one", Cardinal(clone, 501));
        Assert.AreSame(original.ScaleFormSelectors[1], clone.ScaleFormSelectors[1]);
    }

    /// <summary>Custom selector returning only the synthesized keys "singular" and "plural".</summary>
    private sealed class SingularPluralSelector : ILexicalFormSelector
    {
        /// <inheritdoc />
        public string SelectForm(LexicalFormContext context) => context.AbsoluteValue == 1 ? "singular" : "plural";
    }

    /// <summary>Selector returning "singular" for one, "dual" for two and "plural" otherwise.</summary>
    private sealed class SingularDualPluralSelector : ILexicalFormSelector
    {
        /// <inheritdoc />
        public string SelectForm(LexicalFormContext context)
            => context.AbsoluteValue == 1 ? "singular" : context.AbsoluteValue == 2 ? "dual" : "plural";
    }

    /// <summary>Clones <paramref name="original"/> through its options while replacing the scale by "bar(s)".</summary>
    /// <param name="original">The converter to clone.</param>
    /// <returns>The clone built on the new scale.</returns>
    private static NumberToStringConverter CloneWithBarScale(NumberToStringConverter original)
        => new(new NumberToStringConverterOptions(original) { Scale = new NumberScale(["", "bar(s)"], ["illion"]) });

    /// <summary>A selector-only configuration does not freeze the synthesized names of the original scale in a clone.</summary>
    [TestMethod]
    public void ScaleForms_OptionsClone_SelectorOnlyDoesNotFreezeOldScaleNames()
    {
        var options = Options();
        options.ScaleFormSelectors = new Dictionary<int, ILexicalFormSelector> { [1] = new SingularPluralSelector() };
        var original = new NumberToStringConverter(options);
        Assert.AreEqual("two foos", Cardinal(original, 200));

        var clone = CloneWithBarScale(original);

        Assert.AreEqual("one bar", Cardinal(clone, 100));
        Assert.AreEqual("two bars", Cardinal(clone, 200));
    }

    /// <summary>Synthesized singular/plural forms are recomputed from the clone's scale.</summary>
    [TestMethod]
    public void ScaleForms_OptionsClone_RecomputesSynthesizedFormsAfterScaleChange()
    {
        var options = Options();
        options.ScaleForms = new Dictionary<int, LexicalFormSet> { [1] = LexicalFormSet.Create(("plural", "foozles")) };
        var original = new NumberToStringConverter(options);
        Assert.AreEqual("one foo", Cardinal(original, 100));

        var clone = CloneWithBarScale(original);

        Assert.AreEqual("one bar", Cardinal(clone, 100));
        Assert.AreEqual("three foozles", Cardinal(clone, 300));
    }

    /// <summary>A clone carries only the explicitly configured forms and selectors.</summary>
    [TestMethod]
    public void ScaleForms_OptionsClone_PreservesOnlyExplicitOverrides()
    {
        var selector = new SingularDualPluralSelector();
        var options = Options();
        options.ScaleForms = new Dictionary<int, LexicalFormSet> { [1] = LexicalFormSet.Create(("dual", "pair")) };
        options.ScaleFormSelectors = new Dictionary<int, ILexicalFormSelector> { [1] = selector };
        var original = new NumberToStringConverter(options);

        var cloneOptions = new NumberToStringConverterOptions(original);
        var clone = CloneWithBarScale(original);

        Assert.AreEqual(1, cloneOptions.ScaleForms!.Count);
        Assert.AreSame(selector, cloneOptions.ScaleFormSelectors![1]);
        Assert.AreEqual("one bar", Cardinal(clone, 100));
        Assert.AreEqual("two pair", Cardinal(clone, 200));
        Assert.AreEqual("three bars", Cardinal(clone, 300));
    }

    /// <summary>A selector is resolved once while loading; conversions never resolve it again.</summary>
    [TestMethod]
    public void ScaleForms_Selector_IsResolvedOnceAtLoadNotPerConversion()
    {
        int resolutions = 0;
        string name = NewName();
        NumberToStringConverter.RegisterLexicalFormSelector(name, () =>
        {
            Interlocked.Increment(ref resolutions);
            return new FourFormSelector();
        });
        var converter = Build($"""<ScaleForm scale="1" formSelector="{name}">{FourForms}</ScaleForm>""");
        int afterLoad = resolutions;

        for (int i = 100; i < 9999; i += 97)
            Cardinal(converter, i);

        Assert.AreEqual(1, afterLoad);
        Assert.AreEqual(afterLoad, resolutions);
    }

    // ── baseOn ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>A child without NumberScale inherits the parent's scale forms.</summary>
    [TestMethod]
    public void ScaleForms_BaseOnChildWithoutNumberScale_InheritsForms()
    {
        string selector = Register(new FourFormSelector());
        string parent = NewName();
        string child = NewName();
        string document = Document(parent, $"""<ScaleForm scale="1" formSelector="{selector}">{FourForms}</ScaleForm>""",
            extraLanguages: $"""<Language baseOn="{parent}"><Culture>{child}</Culture></Language>""");

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("two F2", Cardinal(converters[child], 200));
    }

    /// <summary>A child redeclaring the same scale replaces the parent's entry.</summary>
    [TestMethod]
    public void ScaleForms_BaseOnChildSameScale_ReplacesParentEntry()
    {
        string selector = Register(new FourFormSelector());
        string parent = NewName();
        string child = NewName();
        string document = Document(parent, $"""<ScaleForm scale="1" formSelector="{selector}">{FourForms}</ScaleForm>""",
            extraLanguages: $"""
                <Language baseOn="{parent}"><Culture>{child}</Culture>
                  <NumberScale><ScaleForm scale="1"><Forms><Form key="plural" value="bars" /></Forms></ScaleForm></NumberScale>
                </Language>
                """);

        var converters = NumberToStringConverter.ReadConfiguration(document);

        Assert.AreEqual("two F2", Cardinal(converters[parent], 200));
        Assert.AreEqual("two bars", Cardinal(converters[child], 200));
        Assert.AreEqual("one foo", Cardinal(converters[child], 100));
    }
}
