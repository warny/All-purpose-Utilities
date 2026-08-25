using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>
/// NTS-05 — regression tests for the general <see cref="ILexicalFormSelector"/> extensibility
/// mechanism: a selector chooses which named lexical form of a configured constituent applies to
/// a given numeric value/context, independently of <see cref="ForcedVariantSet"/> (which
/// constrains the NUMBER's grammar, not the unit word's form).
/// </summary>
[TestClass]
public class NumberToStringConverterLexicalFormSelectorTests
{
    // ─── DefaultLexicalFormSelector ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void DefaultLexicalFormSelector_SelectsSingularOnlyForAbsoluteValueOne()
    {
        var selector = new DefaultLexicalFormSelector();
        var empty = new Dictionary<string, string>();
        Assert.AreEqual("singular", selector.SelectForm(new LexicalFormContext(1, empty)));
        Assert.AreEqual("singular", selector.SelectForm(new LexicalFormContext(-1, empty)));
        Assert.AreEqual("plural", selector.SelectForm(new LexicalFormContext(0, empty)));
        Assert.AreEqual("plural", selector.SelectForm(new LexicalFormContext(2, empty)));
        Assert.AreEqual("plural", selector.SelectForm(new LexicalFormContext(21, empty)));
    }

    [TestMethod]
    public void LexicalFormContext_AbsoluteValue_IsComputedFromValue()
    {
        var context = new LexicalFormContext(-21, new Dictionary<string, string>());
        Assert.AreEqual(new BigInteger(21), context.AbsoluteValue);
    }

    // ─── LexicalFormSet ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void LexicalFormSet_Create_NullOrEmptySequence_ReturnsEmpty()
    {
        Assert.AreSame(LexicalFormSet.Empty, LexicalFormSet.Create(null!));
        Assert.AreSame(LexicalFormSet.Empty, LexicalFormSet.Create());
    }

    [TestMethod]
    public void LexicalFormSet_Create_DuplicateKey_ThrowsUNTS007()
    {
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(
            () => LexicalFormSet.Create(("singular", "hour"), ("singular", "hours")));
        Assert.AreEqual("UNTS007", ex.ErrorCode);
    }

    [TestMethod]
    public void LexicalFormSet_Create_EmptyKeyOrValue_ThrowsUNTS007()
    {
        Assert.AreEqual("UNTS007", Assert.ThrowsException<NumberToStringConfigurationException>(
            () => LexicalFormSet.Create(("", "hour"))).ErrorCode);
        Assert.AreEqual("UNTS007", Assert.ThrowsException<NumberToStringConfigurationException>(
            () => LexicalFormSet.Create(("singular", ""))).ErrorCode);
    }

    // ─── ResolveLexicalFormSelector — reflection resolution (internal, exercised directly) ────────

    [TestMethod]
    public void ResolveLexicalFormSelector_NullOrDefault_ReturnsDefaultSelector()
    {
        Assert.IsInstanceOfType<DefaultLexicalFormSelector>(NumberToStringConverter.ResolveLexicalFormSelector(null, null));
        Assert.IsInstanceOfType<DefaultLexicalFormSelector>(NumberToStringConverter.ResolveLexicalFormSelector("default", null));
    }

    [TestMethod]
    public void ResolveLexicalFormSelector_RegisteredInstance_IsUsed()
    {
        var instance = new ThreeFormSelector();
        NumberToStringConverter.RegisterLexicalFormSelector("test-registered-instance", instance);
        var resolved = NumberToStringConverter.ResolveLexicalFormSelector("test-registered-instance", null);
        Assert.AreSame(instance, resolved);
    }

    [TestMethod]
    public void ResolveLexicalFormSelector_RegisteredFactory_IsInvoked()
    {
        NumberToStringConverter.RegisterLexicalFormSelector("test-registered-factory", () => new ThreeFormSelector());
        var resolved = NumberToStringConverter.ResolveLexicalFormSelector("test-registered-factory", null);
        Assert.IsInstanceOfType<ThreeFormSelector>(resolved);
    }

    [TestMethod]
    public void ResolveLexicalFormSelector_AssemblyQualifiedName_ResolvesByReflection()
    {
        // No prior registration: exercises the real Type.GetType/assembly-scan reflection path.
        string typeName = typeof(ReflectionLoadedSelector).AssemblyQualifiedName!;
        var resolved = NumberToStringConverter.ResolveLexicalFormSelector(typeName, "TEST");
        Assert.IsInstanceOfType<ReflectionLoadedSelector>(resolved);
        Assert.AreEqual("custom", resolved.SelectForm(new LexicalFormContext(1, new Dictionary<string, string>())));
    }

    [TestMethod]
    public void ResolveLexicalFormSelector_ConfigAwareConstructor_ReceivesConfiguration()
    {
        string typeName = typeof(ConfigAwareSelector).AssemblyQualifiedName!;
        var resolved = NumberToStringConverter.ResolveLexicalFormSelector(typeName, "TEST-LANG");
        var configAware = (ConfigAwareSelector)resolved;
        Assert.AreEqual(typeName, configAware.Configuration.TypeName);
        Assert.AreEqual("TEST-LANG", configAware.Configuration.LanguageIdentifier);
    }

    [TestMethod]
    public void ResolveLexicalFormSelector_TypeNotFound_ThrowsUNTS008()
    {
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(
            () => NumberToStringConverter.ResolveLexicalFormSelector("Nonexistent.Type.Name, NonexistentAssembly", null));
        Assert.AreEqual("UNTS008", ex.ErrorCode);
    }

    [TestMethod]
    public void ResolveLexicalFormSelector_TypeDoesNotImplementInterface_ThrowsUNTS008()
    {
        string typeName = typeof(NotASelector).AssemblyQualifiedName!;
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(
            () => NumberToStringConverter.ResolveLexicalFormSelector(typeName, null));
        Assert.AreEqual("UNTS008", ex.ErrorCode);
    }

    [TestMethod]
    public void ResolveLexicalFormSelector_InstantiationFails_ThrowsUNTS008()
    {
        string typeName = typeof(ThrowingConstructorSelector).AssemblyQualifiedName!;
        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(
            () => NumberToStringConverter.ResolveLexicalFormSelector(typeName, null));
        Assert.AreEqual("UNTS008", ex.ErrorCode);
    }

    // ─── Synthetic 3-form selector — proves the abstraction is not hard-wired to singular/plural ──

    [TestMethod]
    public void Convert_Synthetic_ThreeFormSelector_SelectsBeyondSingularPlural()
    {
        // Purely synthetic (bucket = value % 3): not Russian, not any real language — only proves
        // the mechanism supports more than two configured, selector-chosen forms.
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = ("hour", "hours", null),
            },
            TimeUnitForms = new Dictionary<string, LexicalFormSet>
            {
                ["hour"] = LexicalFormSet.Create(("one", "syn-one"), ("few", "syn-few"), ("many", "syn-many")),
            },
            TimeUnitFormSelectors = new Dictionary<string, ILexicalFormSelector>
            {
                ["hour"] = new ThreeFormSelector(),
            },
        };
        var synthetic = new NumberToStringConverter(options);

        Assert.AreEqual("one syn-one", synthetic.Convert(new TimeSpan(1, 0, 0)));
        Assert.AreEqual("two syn-few", synthetic.Convert(new TimeSpan(2, 0, 0)));
        Assert.AreEqual("three syn-many", synthetic.Convert(new TimeSpan(3, 0, 0)));
    }

    [TestMethod]
    public void FormatTimeUnit_SelectorReturnsUnconfiguredKey_ThrowsUNTS007AtRuntime()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = ("hour", "hours", null),
            },
            TimeUnitFormSelectors = new Dictionary<string, ILexicalFormSelector>
            {
                ["hour"] = new ThreeFormSelector(), // requests "one"/"few"/"many"; Forms only has singular/plural
            },
        };
        var converter = new NumberToStringConverter(options);

        var ex = Assert.ThrowsException<NumberToStringConfigurationException>(
            () => converter.Convert(new TimeSpan(1, 0, 0)));
        Assert.AreEqual("UNTS007", ex.ErrorCode);
    }

    // ─── Custom selector loaded through the real XML configuration pipeline ───────────────────────

    [TestMethod]
    public void ReadConfiguration_UnitWithFormSelectorAttribute_UsesCustomSelectorAndForms()
    {
        string typeName = typeof(ReflectionLoadedSelector).AssemblyQualifiedName!;
        string xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999">
                <Culture>NTS05-XML-TEST</Culture>
                <Groups><Group level="1"><Digit digit="0" string=""/><Digit digit="1" string="one"/><Digit digit="2" string="two"/><Digit digit="3" string="three"/><Digit digit="4" string="four"/><Digit digit="5" string="five"/><Digit digit="6" string="six"/><Digit digit="7" string="seven"/><Digit digit="8" string="eight"/><Digit digit="9" string="nine"/></Group></Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string=""/></StaticNames><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>
                <TimeUnits>
                  <Unit name="hour" singular="hour" plural="hours" formSelector="{typeName}">
                    <Forms>
                      <Form key="custom" value="custom-hour" />
                    </Forms>
                  </Unit>
                  <Unit name="minute" singular="minute" plural="minutes" />
                  <Unit name="second" singular="second" plural="seconds" />
                </TimeUnits>
              </Language>
            </Numbers>
            """;

        var converters = NumberToStringConverter.ReadConfiguration(xml);
        var converter = converters["NTS05-XML-TEST"];

        // The custom selector always returns "custom" regardless of count/context; the numeral
        // itself still renders normally ("one" for 1), proving lexical form selection and numeral
        // rendering compose rather than the selector replacing the whole fragment.
        Assert.AreEqual("one custom-hour", converter.Convert(new TimeSpan(1, 0, 0)));
    }

    // ─── Immutability / snapshot ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void TimeUnitForms_MutatingSourceDictionaryAfterConstruction_DoesNotAffectConverter()
    {
        var source = new Dictionary<string, LexicalFormSet>
        {
            ["hour"] = LexicalFormSet.Create(("singular", "hour"), ("plural", "hours")),
        };
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            TimeUnitForms = source,
        };
        var converter = new NumberToStringConverter(options);

        source["hour"] = LexicalFormSet.Empty;
        source["minute"] = LexicalFormSet.Create(("singular", "minute"), ("plural", "minutes"));

        Assert.AreEqual("one hour", converter.Convert(new TimeSpan(1, 0, 0)));
    }

    // ─── Test-only selector implementations ────────────────────────────────────────────────────

    private sealed class ThreeFormSelector : ILexicalFormSelector
    {
        public string SelectForm(LexicalFormContext context)
        {
            long bucket = (long)(context.AbsoluteValue % 3);
            return bucket switch { 1 => "one", 2 => "few", _ => "many" };
        }
    }
}

/// <summary>Test-only selector resolved purely by reflection (never pre-registered).</summary>
internal sealed class ReflectionLoadedSelector : ILexicalFormSelector
{
    public string SelectForm(LexicalFormContext context) => "custom";
}

/// <summary>Test-only selector proving the <see cref="LexicalFormSelectorConfiguration"/> constructor-injection path.</summary>
internal sealed class ConfigAwareSelector(LexicalFormSelectorConfiguration configuration) : ILexicalFormSelector
{
    public LexicalFormSelectorConfiguration Configuration { get; } = configuration;
    public string SelectForm(LexicalFormContext context) => "custom";
}

/// <summary>Test-only type that deliberately does not implement <see cref="ILexicalFormSelector"/>.</summary>
internal sealed class NotASelector;

/// <summary>Test-only selector whose constructor always throws.</summary>
internal sealed class ThrowingConstructorSelector : ILexicalFormSelector
{
    public ThrowingConstructorSelector() => throw new InvalidOperationException("Deliberate test failure.");
    public string SelectForm(LexicalFormContext context) => "unreachable";
}
