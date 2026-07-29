using System.Collections.Concurrent;
using System.Threading;
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
        Assert.ThrowsException<InvalidOperationException>(() => new NumberToStringConverter(options));
    }

    /// <summary>Verifies the explicit bound contract when no large-number scale is configured.</summary>
    [TestMethod]
    public void MissingScale_RequiresCompatibleMaxNumber()
    {
        string culture = "NO-SCALE-" + Guid.NewGuid().ToString("N");
        string valid = CreateConfiguration(culture, "zero");
        Assert.IsTrue(NumberToStringConverter.ReadConfiguration(valid).ContainsKey(culture));

        string absent = valid.Replace(" maxNumber=\"999\"", string.Empty, StringComparison.Ordinal);
        Assert.ThrowsException<InvalidOperationException>(() => NumberToStringConverter.ReadConfiguration(absent));
        string excessive = valid.Replace("maxNumber=\"999\"", "maxNumber=\"1000\"", StringComparison.Ordinal);
        Assert.ThrowsException<InvalidOperationException>(() => NumberToStringConverter.ReadConfiguration(excessive));
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
        Assert.ThrowsException<ArgumentNullException>(() => NumberToStringConverter.RegisterLanguageSpecifics("x", (Func<INumberToStringLanguageSpecifics>)null!));
    }

    /// <summary>Verifies all duplicate policies against the existing registry.</summary>
    [TestMethod]
    public void RegisterConfigurations_AppliesDuplicatePolicies()
    {
        string culture = "POLICY-" + Guid.NewGuid().ToString("N");
        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(culture, "first")], DuplicateCulturePolicy.Replace);
        Assert.ThrowsException<InvalidOperationException>(() =>
            NumberToStringConverter.RegisterConfigurations([CreateConfiguration(culture, "rejected")], DuplicateCulturePolicy.Reject));

        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(culture, "kept")], DuplicateCulturePolicy.KeepExisting);
        Assert.AreEqual("first", NumberToStringConverter.GetConverter(culture).Zero);
        NumberToStringConverter.RegisterConfigurations([CreateConfiguration(culture, "replacement")], DuplicateCulturePolicy.Replace);
        Assert.AreEqual("replacement", NumberToStringConverter.GetConverter(culture).Zero);
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
