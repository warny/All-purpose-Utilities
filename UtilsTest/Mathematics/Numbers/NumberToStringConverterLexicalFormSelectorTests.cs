using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
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
        // "test-registered-instance" is not a real type name: if this bypassed the registration and
        // fell through to reflection, resolution would fail with UNTS008 instead of succeeding —
        // this proves a registered instance takes precedence over reflection.
        var instance = new ThreeFormSelector();
        NumberToStringConverter.RegisterLexicalFormSelector("test-registered-instance", instance);
        var resolved = NumberToStringConverter.ResolveLexicalFormSelector("test-registered-instance", null);
        Assert.AreSame(instance, resolved);
    }

    [TestMethod]
    public void ResolveLexicalFormSelector_RegisteredFactory_IsInvoked()
    {
        // Same precedence argument as above: "test-registered-factory" resolves only because the
        // registered factory is consulted before reflection.
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

    // ─── TimeUnitForms/TimeUnitFormSelectors — effective (not override-only) contract ─────────────

    [TestMethod]
    public void TimeUnitForms_BuiltInEN_ReturnsEffectiveFormsForEveryUnit()
    {
        var en = NumberToStringConverter.GetConverter("EN");

        CollectionAssert.AreEquivalent(new[] { "hour", "minute", "second" }, en.TimeUnitForms.Keys.ToArray());

        AssertHasSingularPlural(en.TimeUnitForms["hour"], "hour", "hours");
        AssertHasSingularPlural(en.TimeUnitForms["minute"], "minute", "minutes");
        AssertHasSingularPlural(en.TimeUnitForms["second"], "second", "seconds");

        static void AssertHasSingularPlural(LexicalFormSet forms, string singular, string plural)
        {
            Assert.IsTrue(forms.TryGetForm("singular", out var s));
            Assert.AreEqual(singular, s);
            Assert.IsTrue(forms.TryGetForm("plural", out var p));
            Assert.AreEqual(plural, p);
        }
    }

    [TestMethod]
    public void TimeUnitFormSelectors_BuiltInEN_ReturnsDefaultSelectorForEveryUnit()
    {
        var en = NumberToStringConverter.GetConverter("EN");

        CollectionAssert.AreEquivalent(new[] { "hour", "minute", "second" }, en.TimeUnitFormSelectors.Keys.ToArray());
        Assert.IsInstanceOfType<DefaultLexicalFormSelector>(en.TimeUnitFormSelectors["hour"]);
        Assert.IsInstanceOfType<DefaultLexicalFormSelector>(en.TimeUnitFormSelectors["minute"]);
        Assert.IsInstanceOfType<DefaultLexicalFormSelector>(en.TimeUnitFormSelectors["second"]);
    }

    [TestMethod]
    public void TimeUnitForms_ExplicitOverrideOnOneUnit_MergesWithSynthesizedFormsAndSiblingsStaySynthesizedOnly()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            TimeUnitForms = new Dictionary<string, LexicalFormSet>
            {
                ["hour"] = LexicalFormSet.Create(("custom", "custom-hour")),
            },
        };
        var converter = new NumberToStringConverter(options);

        // "hour" keeps its synthesized singular/plural AND gains the explicit override.
        var hourForms = converter.TimeUnitForms["hour"];
        Assert.IsTrue(hourForms.TryGetForm("singular", out var hs));
        Assert.AreEqual("hour", hs);
        Assert.IsTrue(hourForms.TryGetForm("plural", out var hp));
        Assert.AreEqual("hours", hp);
        Assert.IsTrue(hourForms.TryGetForm("custom", out var hc));
        Assert.AreEqual("custom-hour", hc);

        // "minute" has no configured override, yet the effective view still reports it, with only
        // its synthesized singular/plural.
        var minuteForms = converter.TimeUnitForms["minute"];
        Assert.IsTrue(minuteForms.TryGetForm("singular", out var ms));
        Assert.AreEqual("minute", ms);
        Assert.IsTrue(minuteForms.TryGetForm("plural", out var mp));
        Assert.AreEqual("minutes", mp);
        Assert.IsFalse(minuteForms.TryGetForm("custom", out _));
    }

    [TestMethod]
    public void TimeUnitFormSelectors_OverrideOnOneUnit_SiblingsStillReportDefaultSelector()
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            TimeUnitFormSelectors = new Dictionary<string, ILexicalFormSelector>
            {
                ["hour"] = new ThreeFormSelector(),
            },
        };
        var converter = new NumberToStringConverter(options);

        Assert.IsInstanceOfType<ThreeFormSelector>(converter.TimeUnitFormSelectors["hour"]);
        Assert.IsInstanceOfType<DefaultLexicalFormSelector>(converter.TimeUnitFormSelectors["minute"]);
        Assert.IsInstanceOfType<DefaultLexicalFormSelector>(converter.TimeUnitFormSelectors["second"]);
    }

    [TestMethod]
    public void Clone_NarrowingTimeUnits_DoesNotResurrectRemovedUnitFormsOrSelectors()
    {
        var source = NumberToStringConverter.GetConverter("EN");
        var options = new NumberToStringConverterOptions(source)
        {
            TimeUnits = new Dictionary<string, (string Singular, string Plural, string? Count1Form)>
            {
                ["hour"] = source.TimeUnits["hour"],
            },
        };
        var narrowed = new NumberToStringConverter(options);

        CollectionAssert.AreEquivalent(new[] { "hour" }, narrowed.TimeUnitForms.Keys.ToArray());
        CollectionAssert.AreEquivalent(new[] { "hour" }, narrowed.TimeUnitFormSelectors.Keys.ToArray());
    }

    // ─── Selector-specific XML configuration (<LexicalFormSelector><Configuration>) ────────────────

    [TestMethod]
    public void ReadConfiguration_UnitWithLexicalFormSelectorElement_PassesConfigurationToSelector()
    {
        string typeName = typeof(ConfigDrivenFormSelector).AssemblyQualifiedName!;
        string xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999">
                <Culture>NTS05-XML-CONFIG-TEST</Culture>
                <Groups><Group level="1"><Digit digit="0" string=""/><Digit digit="1" string="one"/><Digit digit="2" string="two"/><Digit digit="3" string="three"/><Digit digit="4" string="four"/><Digit digit="5" string="five"/><Digit digit="6" string="six"/><Digit digit="7" string="seven"/><Digit digit="8" string="eight"/><Digit digit="9" string="nine"/></Group></Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string=""/></StaticNames><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>
                <TimeUnits>
                  <Unit name="hour" singular="hour" plural="hours">
                    <LexicalFormSelector type="{typeName}">
                      <Configuration form="configured-hour-form" />
                    </LexicalFormSelector>
                    <Forms>
                      <Form key="configured-hour-form" value="configured-hour" />
                    </Forms>
                  </Unit>
                  <Unit name="minute" singular="minute" plural="minutes" />
                  <Unit name="second" singular="second" plural="seconds" />
                </TimeUnits>
              </Language>
            </Numbers>
            """;

        var converters = NumberToStringConverter.ReadConfiguration(xml);
        var converter = converters["NTS05-XML-CONFIG-TEST"];

        Assert.AreEqual("one configured-hour", converter.Convert(new TimeSpan(1, 0, 0)));
    }

    [TestMethod]
    public void ReadConfiguration_SameSelectorTypeWithDifferentConfigurationsOnTwoUnits_EachUsesItsOwnConfiguration()
    {
        string typeName = typeof(ConfigDrivenFormSelector).AssemblyQualifiedName!;
        string xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999">
                <Culture>NTS05-XML-DUAL-CONFIG-TEST</Culture>
                <Groups><Group level="1"><Digit digit="0" string=""/><Digit digit="1" string="one"/><Digit digit="2" string="two"/><Digit digit="3" string="three"/><Digit digit="4" string="four"/><Digit digit="5" string="five"/><Digit digit="6" string="six"/><Digit digit="7" string="seven"/><Digit digit="8" string="eight"/><Digit digit="9" string="nine"/></Group></Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string=""/></StaticNames><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>
                <TimeUnits>
                  <Unit name="hour" singular="hour" plural="hours">
                    <LexicalFormSelector type="{typeName}">
                      <Configuration form="hour-form" />
                    </LexicalFormSelector>
                    <Forms><Form key="hour-form" value="loud-hour" /></Forms>
                  </Unit>
                  <Unit name="minute" singular="minute" plural="minutes">
                    <LexicalFormSelector type="{typeName}">
                      <Configuration form="minute-form" />
                    </LexicalFormSelector>
                    <Forms><Form key="minute-form" value="quiet-minute" /></Forms>
                  </Unit>
                  <Unit name="second" singular="second" plural="seconds" />
                </TimeUnits>
              </Language>
            </Numbers>
            """;

        var converters = NumberToStringConverter.ReadConfiguration(xml);
        var converter = converters["NTS05-XML-DUAL-CONFIG-TEST"];

        // Same selector TYPE, two different <Configuration> subtrees: each unit must use its own,
        // proving the reflection activator cache is keyed by type name (and re-invoked per unit),
        // not a single shared configured instance.
        Assert.AreEqual("one loud-hour", converter.Convert(new TimeSpan(1, 0, 0)));
        Assert.AreEqual("one quiet-minute", converter.Convert(new TimeSpan(0, 1, 0)));
    }

    // ─── Reflection activation caching — cached per type name, not per configured instance ─────────
    //
    // These tests are intentionally self-contained: each uses a dedicated, test-local selector type
    // and resets its own static counter immediately before asserting, so results never depend on
    // MSTest's (unspecified) execution order or on prior tests having already touched the shared,
    // process-wide activator registry. Reflection/type-constructor discovery being cached per type
    // name (as opposed to per resolution) is CachedLoader's own responsibility — not re-verified
    // here via cache-internals introspection — but its functional consequence (the same type name
    // used by two units with two different configurations never cross-contaminates) is exercised by
    // ReadConfiguration_SameSelectorTypeWithDifferentConfigurationsOnTwoUnits_EachUsesItsOwnConfiguration
    // above.

    [TestMethod]
    public void ResolveLexicalFormSelector_SameTypeResolvedTwice_ConstructsANewInstanceEachTime()
    {
        string typeName = typeof(ConstructionCountingSelector).AssemblyQualifiedName!;
        ConstructionCountingSelector.ConstructionCount = 0;

        var first = NumberToStringConverter.ResolveLexicalFormSelector(typeName, null);
        var second = NumberToStringConverter.ResolveLexicalFormSelector(typeName, null);

        // The activation STRATEGY for this type name is cached and reused (CachedLoader), but each
        // resolution still invokes it fresh: the cache never stores one shared configured instance.
        Assert.AreEqual(2, ConstructionCountingSelector.ConstructionCount);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void RegisterLexicalFormSelector_ConfigAwareFactory_ReceivesPerUnitConfigurationAndTakesPrecedenceOverReflection()
    {
        // Not a real type name: if this were ever routed to the reflection loader instead of the
        // registered factory below, resolution would fail with UNTS008 (type not found).
        const string registeredName = "test-registered-configured-factory";
        NumberToStringConverter.RegisterLexicalFormSelector(
            registeredName,
            config => new ConfigAwareSelector(config));

        var configurationA = XElement.Parse("<Configuration form=\"a\" />");
        var configurationB = XElement.Parse("<Configuration form=\"b\" />");

        var resolvedA = (ConfigAwareSelector)NumberToStringConverter.ResolveLexicalFormSelector(registeredName, "LANG-A", configurationA);
        var resolvedB = (ConfigAwareSelector)NumberToStringConverter.ResolveLexicalFormSelector(registeredName, "LANG-B", configurationB);

        Assert.AreEqual("LANG-A", resolvedA.Configuration.LanguageIdentifier);
        Assert.AreEqual("a", resolvedA.Configuration.Configuration!.Attribute("form")!.Value);
        Assert.AreEqual("LANG-B", resolvedB.Configuration.LanguageIdentifier);
        Assert.AreEqual("b", resolvedB.Configuration.Configuration!.Attribute("form")!.Value);
    }

    [TestMethod]
    public void Convert_ReflectionResolvedSelector_ConstructsSelectorOnceAtConfigurationLoadNotOnEachConvertCall()
    {
        string typeName = typeof(HotPathReflectionGuardSelector).AssemblyQualifiedName!;
        HotPathReflectionGuardSelector.ConstructionCount = 0;
        string xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Numbers xmlns="Utils/NumberConvertionConfiguration.xsd">
              <Language groupSize="3" separator=" " groupSeparator="" zero="zero" minus="minus *" decimalSeparator="point" maxNumber="999">
                <Culture>NTS05-XML-HOTPATH-TEST</Culture>
                <Groups><Group level="1"><Digit digit="0" string=""/><Digit digit="1" string="one"/><Digit digit="2" string="two"/><Digit digit="3" string="three"/><Digit digit="4" string="four"/><Digit digit="5" string="five"/><Digit digit="6" string="six"/><Digit digit="7" string="seven"/><Digit digit="8" string="eight"/><Digit digit="9" string="nine"/></Group></Groups>
                <NumberScale firstLetterUpperCase="false"><StaticNames><Scale value="0" string=""/></StaticNames><Suffixes><Suffix>on</Suffix></Suffixes></NumberScale>
                <TimeUnits>
                  <Unit name="hour" singular="hour" plural="hours" formSelector="{typeName}" />
                  <Unit name="minute" singular="minute" plural="minutes" />
                  <Unit name="second" singular="second" plural="seconds" />
                </TimeUnits>
              </Language>
            </Numbers>
            """;

        var converters = NumberToStringConverter.ReadConfiguration(xml);
        Assert.AreEqual(1, HotPathReflectionGuardSelector.ConstructionCount);

        var converter = converters["NTS05-XML-HOTPATH-TEST"];
        for (int i = 1; i <= 5; i++)
            converter.Convert(new TimeSpan(i, 0, 0));

        // Repeated Convert calls must never re-resolve/re-construct the selector: resolution
        // happens exactly once, while loading configuration.
        Assert.AreEqual(1, HotPathReflectionGuardSelector.ConstructionCount);
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

/// <summary>
/// Test-only selector that always returns the form key configured on its own
/// <c>&lt;Configuration form="..."/&gt;</c> subtree, regardless of numeric value/variants — proves a
/// selector actually receives and can use its own selector-specific configuration.
/// </summary>
internal sealed class ConfigDrivenFormSelector(LexicalFormSelectorConfiguration configuration) : ILexicalFormSelector
{
    private readonly string _formKey = configuration.Configuration?.Attribute("form")?.Value
        ?? throw new InvalidOperationException("ConfigDrivenFormSelector requires a 'form' attribute on <Configuration>.");

    public string SelectForm(LexicalFormContext context) => _formKey;
}

/// <summary>Test-only selector whose constructor counts invocations, to prove the activation cache stores an activator (invoked anew per resolution), not a shared instance.</summary>
internal sealed class ConstructionCountingSelector : ILexicalFormSelector
{
    public static int ConstructionCount;
    public ConstructionCountingSelector() => ConstructionCount++;
    public string SelectForm(LexicalFormContext context) => "instrumented";
}

/// <summary>Test-only selector whose constructor counts invocations, to prove Convert(...) never re-resolves/re-constructs a selector already resolved while loading configuration.</summary>
internal sealed class HotPathReflectionGuardSelector : ILexicalFormSelector
{
    public static int ConstructionCount;
    public HotPathReflectionGuardSelector() => ConstructionCount++;
    public string SelectForm(LexicalFormContext context) =>
        context.AbsoluteValue == System.Numerics.BigInteger.One ? "singular" : "plural";
}
