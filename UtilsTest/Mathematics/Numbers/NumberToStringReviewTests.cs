using System.Collections.Concurrent;
using System.Threading;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Regression tests added during review of the resolved-configuration work.</summary>
[TestClass]
public class NumberToStringReviewTests
{
    /// <summary>Verifies named and generated zero multiplicatives and signed extreme inputs.</summary>
    [TestMethod]
    public void ConvertMultiplicative_HandlesZeroAndSignedInputs()
    {
        var generated = new NumberToStringConverter(new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            Multiplicatives = new Dictionary<int, string>(),
            MultiplicativeSuffix = " times",
        });
        var named = new NumberToStringConverter(new NumberToStringConverterOptions(generated)
        {
            Multiplicatives = new Dictionary<int, string> { [0] = "never" },
        });

        Assert.AreEqual("zero times", generated.ConvertMultiplicative(0));
        Assert.AreEqual("never", named.ConvertMultiplicative(0));
        StringAssert.StartsWith(generated.ConvertMultiplicative(-2), "minus ");
        StringAssert.StartsWith(generated.ConvertMultiplicative(int.MinValue), "minus ");
    }

    /// <summary>Verifies that date values resembling tokens are emitted literally.</summary>
    [TestMethod]
    public void DatePattern_DoesNotRescanInsertedValues()
    {
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN"))
        {
            DatePattern = "{ordinal-day}/{year}",
            DateFirstDay = "{year}",
        });

        Assert.AreEqual("{year}/twenty twenty-six", converter.Convert(new DateOnly(2026, 1, 1)));
    }

    /// <summary>Verifies that malformed and unknown date tokens fail during construction.</summary>
    [DataTestMethod]
    [DataRow("{unknown}")]
    [DataRow("{year")]
    [DataRow("year}")]
    public void DatePattern_InvalidSyntaxThrows(string pattern)
    {
        var options = new NumberToStringConverterOptions(NumberToStringConverter.GetConverter("EN")) { DatePattern = pattern };
        Assert.ThrowsExactly<InvalidOperationException>(() => new NumberToStringConverter(options));
    }

    /// <summary>Verifies the explicit bound contract when no large-number scale is configured.</summary>
    [TestMethod]
    public void MissingScale_RequiresCompatibleMaxNumber()
    {
        string culture = "NO-SCALE-" + Guid.NewGuid().ToString("N");
        string valid = CreateConfiguration(culture, "zero");
        Assert.IsTrue(NumberToStringConverter.ReadConfiguration(valid).ContainsKey(culture));

        string absent = valid.Replace(" maxNumber=\"999\"", string.Empty, StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidOperationException>(() => NumberToStringConverter.ReadConfiguration(absent));
        string excessive = valid.Replace("maxNumber=\"999\"", "maxNumber=\"1000\"", StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidOperationException>(() => NumberToStringConverter.ReadConfiguration(excessive));
    }

    /// <summary>Verifies factory validation and that a fresh specifics instance is created per converter.</summary>
    [TestMethod]
    public void RegisterLanguageSpecifics_FactoryCreatesPerConverter()
    {
        string typeName = "factory-" + Guid.NewGuid().ToString("N");
        int creations = 0;
        NumberToStringConverter.RegisterLanguageSpecifics(typeName, () =>
        {
            Interlocked.Increment(ref creations);
            return new MarkerSpecifics();
        });
        string xml = CreateConfiguration("FACTORY-A-" + Guid.NewGuid().ToString("N"), "zero", typeName)
            .Replace("</Numbers>", CreateLanguage("FACTORY-B-" + Guid.NewGuid().ToString("N"), "nil", typeName) + "</Numbers>");

        var converters = NumberToStringConverter.ReadConfiguration(xml);

        Assert.AreEqual(2, creations);
        Assert.IsTrue(converters.Values.All(c => c.Convert(1).EndsWith("!", StringComparison.Ordinal)));
        Assert.ThrowsExactly<ArgumentNullException>(() => NumberToStringConverter.RegisterLanguageSpecifics("x", (Func<INumberToStringLanguageSpecifics>)null!));
    }

    /// <summary>Verifies all duplicate policies against the existing registry.</summary>
    [TestMethod]
    public void RegisterConfigurations_AppliesDuplicatePolicies()
    {
        string culture = "POLICY-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(culture, "first")], DuplicateCulturePolicy.Replace);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            NumberToStringConverter.RegisterConfigurations([CreateConfiguration(culture, "rejected")], DuplicateCulturePolicy.Reject));

        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(culture, "kept")], DuplicateCulturePolicy.KeepExisting);
        Assert.AreEqual("first", NumberToStringConverter.GetConverter(culture).Zero);
        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(culture, "replacement")], DuplicateCulturePolicy.Replace);
        Assert.AreEqual("replacement", NumberToStringConverter.GetConverter(culture).Zero);
    }

    /// <summary>Verifies that reading a configuration returns but does not register its converter.</summary>
    [TestMethod]
    public void ReadConfiguration_DoesNotPublishConverter()
    {
        string culture = "READ-CONVERTER-" + Guid.NewGuid().ToString("N");

        var converters = NumberToStringConverter.ReadConfiguration(CreateConfiguration(culture, "read"));

        Assert.IsTrue(converters.ContainsKey(culture));
        Assert.IsFalse(NumberToStringConverter.TryGetConverter(culture, out _));
    }

    /// <summary>Verifies that a previously read definition remains available to later reads.</summary>
    [TestMethod]
    public void ReadConfiguration_PreviouslyReadDefinitionCanBeUsedAsBase()
    {
        string baseCulture = "READ-BASE-" + Guid.NewGuid().ToString("N");
        string childCulture = "READ-CHILD-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.ReadConfiguration(CreateConfiguration(baseCulture, "read"));

        var converters = NumberToStringConverter.ReadConfiguration(CreateChildConfiguration(childCulture, baseCulture));

        Assert.AreEqual("read", converters[childCulture].Zero);
    }

    /// <summary>Verifies that a read definition is isolated from the registration registry.</summary>
    [TestMethod]
    public void ReadConfiguration_DefinitionIsNotVisibleToRegistration()
    {
        string baseCulture = "READ-ISOLATED-BASE-" + Guid.NewGuid().ToString("N");
        string childCulture = "READ-ISOLATED-CHILD-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.ReadConfiguration(CreateConfiguration(baseCulture, "read"));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            NumberToStringConverter.RegisterConfigurations([CreateChildConfiguration(childCulture, baseCulture)]));

        StringAssert.Contains(exception.Message, "was not found");
        Assert.IsFalse(NumberToStringConverter.TryGetConverter(baseCulture, out _));
        Assert.IsFalse(NumberToStringConverter.TryGetConverter(childCulture, out _));
    }

    /// <summary>Verifies that a failed read publishes none of its otherwise valid definitions.</summary>
    [TestMethod]
    public void ReadConfiguration_InvalidDocumentDoesNotPublishDefinitions()
    {
        string baseCulture = "READ-INVALID-BASE-" + Guid.NewGuid().ToString("N");
        string invalidCulture = "READ-INVALID-LANGUAGE-" + Guid.NewGuid().ToString("N");
        string childCulture = "READ-INVALID-CHILD-" + Guid.NewGuid().ToString("N");
        string invalidDocument = CreateConfiguration(baseCulture, "valid").Replace(
            "</Numbers>",
            $"<Language><Culture>{invalidCulture}</Culture></Language></Numbers>",
            StringComparison.Ordinal);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            NumberToStringConverter.ReadConfiguration(invalidDocument));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            NumberToStringConverter.ReadConfiguration(CreateChildConfiguration(childCulture, baseCulture)));
    }

    /// <summary>Verifies that KeepExisting resolves a child against the registered definition.</summary>
    [TestMethod]
    public void RegisterConfigurations_KeepExisting_ChildUsesExistingGlobalBaseDefinition()
    {
        string baseCulture = "KEEP-BASE-" + Guid.NewGuid().ToString("N");
        string childCulture = "KEEP-CHILD-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(baseCulture, "existing")]);

        NumberToStringConverter.RegisterConfigurations(
            [CreateConfiguration(baseCulture, "candidate"), CreateChildConfiguration(childCulture, baseCulture)],
            DuplicateCulturePolicy.KeepExisting);

        Assert.AreEqual("existing", NumberToStringConverter.GetConverter(baseCulture).Zero);
        Assert.AreEqual("existing", NumberToStringConverter.GetConverter(childCulture).Zero);
    }

    /// <summary>Verifies that KeepExisting makes the first definition in a batch authoritative.</summary>
    [TestMethod]
    public void RegisterConfigurations_KeepExisting_EarlierBatchDefinitionRemainsAuthoritative()
    {
        string baseCulture = "KEEP-BATCH-BASE-" + Guid.NewGuid().ToString("N");
        string childCulture = "KEEP-BATCH-CHILD-" + Guid.NewGuid().ToString("N");
        string secondDocument = CreateConfiguration(baseCulture, "second").Replace(
            "</Numbers>",
            $"<Language baseOn=\"{baseCulture}\"><Culture>{childCulture}</Culture></Language></Numbers>",
            StringComparison.Ordinal);

        NumberToStringConverter.RegisterConfigurations(
            [CreateConfiguration(baseCulture, "first"), secondDocument],
            DuplicateCulturePolicy.KeepExisting);

        Assert.AreEqual("first", NumberToStringConverter.GetConverter(baseCulture).Zero);
        Assert.AreEqual("first", NumberToStringConverter.GetConverter(childCulture).Zero);
    }

    /// <summary>Verifies multi-base inheritance uses the effective global and batch definitions.</summary>
    [TestMethod]
    public void RegisterConfigurations_KeepExisting_MultipleBasesUseEffectiveDefinitions()
    {
        string cultureA = "KEEP-MULTI-A-" + Guid.NewGuid().ToString("N");
        string cultureB = "KEEP-MULTI-B-" + Guid.NewGuid().ToString("N");
        string childCulture = "KEEP-MULTI-CHILD-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(cultureA, "existing-a")]);
        string baseB = $"<Numbers xmlns=\"Utils/NumberConvertionConfiguration.xsd\"><Language baseOn=\"{cultureA}\" minus=\"minus-b *\"><Culture>{cultureB}</Culture></Language></Numbers>";
        string child = $"<Numbers xmlns=\"Utils/NumberConvertionConfiguration.xsd\"><Language baseOn=\"{cultureA}, {cultureB}\"><Culture>{childCulture}</Culture></Language></Numbers>";

        NumberToStringConverter.RegisterConfigurations(
            [CreateConfiguration(cultureA, "candidate-a"), baseB, child],
            DuplicateCulturePolicy.KeepExisting);

        NumberToStringConverter converter = NumberToStringConverter.GetConverter(childCulture);
        Assert.AreEqual("existing-a", converter.Zero);
        Assert.AreEqual("minus-b *", converter.Minus);
    }

    /// <summary>Verifies that Replace resolves a child against the replacement definition.</summary>
    [TestMethod]
    public void RegisterConfigurations_Replace_ChildUsesReplacementBaseDefinition()
    {
        string baseCulture = "REPLACE-BASE-" + Guid.NewGuid().ToString("N");
        string childCulture = "REPLACE-CHILD-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(baseCulture, "existing")]);

        NumberToStringConverter.RegisterConfigurations(
            [CreateConfiguration(baseCulture, "replacement"), CreateChildConfiguration(childCulture, baseCulture)],
            DuplicateCulturePolicy.Replace);

        Assert.AreEqual("replacement", NumberToStringConverter.GetConverter(baseCulture).Zero);
        Assert.AreEqual("replacement", NumberToStringConverter.GetConverter(childCulture).Zero);
    }

    /// <summary>Verifies that Reject publishes neither converters nor hidden definitions after a collision.</summary>
    [TestMethod]
    public void RegisterConfigurations_Reject_CollisionPublishesNothing()
    {
        string collisionCulture = "REJECT-BASE-" + Guid.NewGuid().ToString("N");
        string stagedCulture = "REJECT-STAGED-" + Guid.NewGuid().ToString("N");
        string probeCulture = "REJECT-PROBE-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(collisionCulture, "existing")]);

        Assert.ThrowsExactly<InvalidOperationException>(() => NumberToStringConverter.RegisterConfigurations(
            [CreateConfiguration(stagedCulture, "staged"), CreateConfiguration(collisionCulture, "candidate")]));

        Assert.IsFalse(NumberToStringConverter.TryGetConverter(stagedCulture, out _));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            NumberToStringConverter.RegisterConfigurations([CreateChildConfiguration(probeCulture, stagedCulture)]));
        Assert.IsFalse(NumberToStringConverter.TryGetConverter(probeCulture, out _));
    }

    /// <summary>Verifies that an invalid multi-document batch leaves no converter or base definition.</summary>
    [TestMethod]
    public void RegisterConfigurations_InvalidBatchPublishesNoDefinitions()
    {
        string validCulture = "INVALID-STAGED-" + Guid.NewGuid().ToString("N");
        string probeCulture = "INVALID-PROBE-" + Guid.NewGuid().ToString("N");

        Assert.ThrowsExactly<XmlException>(() => NumberToStringConverter.RegisterConfigurations(
            [CreateConfiguration(validCulture, "valid"), "<Numbers>"]));

        Assert.IsFalse(NumberToStringConverter.TryGetConverter(validCulture, out _));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            NumberToStringConverter.RegisterConfigurations([CreateChildConfiguration(probeCulture, validCulture)]));
    }

    /// <summary>Verifies that a child in a replacing document inherits that document's replacement base.</summary>
    [TestMethod]
    public void RegisterConfigurations_ReplaceUsesCurrentDocumentBaseForChild()
    {
        string baseCulture = "BATCH-BASE-" + Guid.NewGuid().ToString("N");
        string childCulture = "BATCH-CHILD-" + Guid.NewGuid().ToString("N");
        string oldDocument = CreateConfiguration(baseCulture, "old");
        string replacementDocument = $"<Numbers xmlns=\"Utils/NumberConvertionConfiguration.xsd\">{CreateLanguage(baseCulture, "new", null)}<Language baseOn=\"{baseCulture}\"><Culture>{childCulture}</Culture></Language></Numbers>";

        NumberToStringConverter.RegisterConfigurations(
            [oldDocument, replacementDocument],
            DuplicateCulturePolicy.Replace);

        Assert.AreEqual("new", NumberToStringConverter.GetConverter(baseCulture).Zero);
        Assert.AreEqual("new", NumberToStringConverter.GetConverter(childCulture).Zero);
    }

    /// <summary>Verifies that undefined duplicate policies are rejected before any configuration is built.</summary>
    [TestMethod]
    public void RegisterConfigurations_InvalidPolicyThrows()
    {
        var invalidPolicy = (DuplicateCulturePolicy)99;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            NumberToStringConverter.RegisterConfigurations([], invalidPolicy));
    }

    /// <summary>Verifies that concurrent replacement batches commit complete converter/definition pairs.</summary>
    [TestMethod]
    public void RegisterConfigurations_ConcurrentReplaceIsConsistent()
    {
        string culture = "CONCURRENT-" + Guid.NewGuid().ToString("N");
        var errors = new ConcurrentQueue<Exception>();
        Parallel.ForEach(new[] { "alpha", "beta" }, zero =>
        {
            try
            {
                NumberToStringConverter.RegisterConfigurations([CreateConfiguration(culture, zero)], DuplicateCulturePolicy.Replace);
            }
            catch (Exception exception)
            {
                errors.Enqueue(exception);
            }
        });

        Assert.AreEqual(0, errors.Count);
        Assert.IsTrue(new[] { "alpha", "beta" }.Contains(NumberToStringConverter.GetConverter(culture).Zero));
    }

    /// <summary>Verifies that concurrent base replacement and inheritance resolution observe one coherent registry state.</summary>
    [TestMethod]
    public void RegisterConfigurations_ConcurrentInheritanceUsesCoherentBase()
    {
        string baseCulture = "CONCURRENT-BASE-" + Guid.NewGuid().ToString("N");
        string childCulture = "CONCURRENT-CHILD-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(baseCulture, "initial")], DuplicateCulturePolicy.Replace);
        string child = $"<Numbers xmlns=\"Utils/NumberConvertionConfiguration.xsd\"><Language baseOn=\"{baseCulture}\"><Culture>{childCulture}</Culture></Language></Numbers>";
        var errors = new ConcurrentQueue<Exception>();

        Parallel.Invoke(
            () => Capture(() => NumberToStringConverter.RegisterConfigurations(
                [CreateConfiguration(baseCulture, "replacement")], DuplicateCulturePolicy.Replace), errors),
            () => Capture(() => NumberToStringConverter.RegisterConfigurations([child], DuplicateCulturePolicy.Replace), errors));

        Assert.AreEqual(0, errors.Count);
        Assert.IsTrue(new[] { "initial", "replacement" }.Contains(NumberToStringConverter.GetConverter(childCulture).Zero));
    }

    /// <summary>Captures an exception produced by a concurrent registration action.</summary>
    private static void Capture(Action action, ConcurrentQueue<Exception> errors)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            errors.Enqueue(exception);
        }
    }

    /// <summary>Creates a complete bounded configuration document.</summary>
    private static string CreateConfiguration(string culture, string zero, string? specifics = null)
        => $"<Numbers xmlns=\"Utils/NumberConvertionConfiguration.xsd\">{CreateLanguage(culture, zero, specifics)}</Numbers>";

    /// <summary>Creates a configuration containing one language that inherits a registered base.</summary>
    private static string CreateChildConfiguration(string culture, string baseCulture)
        => $"<Numbers xmlns=\"Utils/NumberConvertionConfiguration.xsd\"><Language baseOn=\"{baseCulture}\"><Culture>{culture}</Culture></Language></Numbers>";

    /// <summary>Creates one complete bounded language element.</summary>
    private static string CreateLanguage(string culture, string zero, string? specifics)
    {
        string digits = string.Concat(Enumerable.Range(0, 10).Select(i => $"<Digit digit=\"{i}\" string=\"{i}\"/>"));
        string specificsElement = specifics == null ? string.Empty : $"<LanguageSpecifics>{specifics}</LanguageSpecifics>";
        return $"<Language groupSize=\"3\" separator=\" \" groupSeparator=\"\" zero=\"{zero}\" minus=\"minus *\" maxNumber=\"999\"><Culture>{culture}</Culture><Groups><Group level=\"1\">{digits}</Group></Groups>{specificsElement}</Language>";
    }

    /// <summary>Test finalizer that marks each completed output.</summary>
    private sealed class MarkerSpecifics : INumberToStringLanguageSpecifics
    {
        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text) => text + "!";
    }
}
