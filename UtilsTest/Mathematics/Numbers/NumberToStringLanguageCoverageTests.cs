using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Utils.NumberToString;

namespace UtilsTest.NumberToString;

/// <summary>
/// Meta-coverage of the language <c>.feature</c> files: every language whose ordinals or clock
/// were activated or repaired by NTS-08 must declare its capability explicitly and cover the
/// structurally important values. The test inspects the ReqnRoll-generated test methods (their
/// tags, descriptions, and example rows) by reflection; it never duplicates linguistic strings.
/// </summary>
[TestClass]
public class NumberToStringLanguageCoverageTests
{
    /// <summary>Languages whose ordinals were activated or repaired by NTS-08.</summary>
    private static readonly string[] OrdinalLanguages =
        ["DA", "NO", "SV", "BG", "HR", "HU", "CS", "SK", "UK", "RO", "AR", "HE", "FA", "TR"];

    /// <summary>Languages whose idiomatic clock was added by NTS-08.</summary>
    private static readonly string[] ClockLanguages =
    [
        "NL", "DA", "NO", "SV", "BG", "HR", "HU", "CS", "SK", "UK", "PL", "RU", "ES", "IT", "PT", "GL", "RO",
        "EL", "FI", "AR", "HE", "FA", "TR", "HI", "JA", "KO", "ZH", "VN", "SW", "ZU", "EU",
    ];

    /// <summary>
    /// Languages whose clock configuration contains a distinct literal whole-hour form for every
    /// display hour. Their language feature must exercise all twelve entries so a typo in an
    /// otherwise unreachable XML branch cannot be hidden by structural lookup coverage alone.
    /// </summary>
    private static readonly string[] LiteralClockHourLanguages = ["EU", "KO", "ZU"];

    /// <summary>Languages that deliberately keep a capability unsupported, with the capability concerned.</summary>
    private static readonly (string Language, string Capability, bool Supported)[] DeclaredCapabilities =
    [
        ("SW", "ordinal conversion", false),
        ("ZU", "ordinal conversion", false),
        ("EE", "clock-time conversion", false),
        ("WO", "clock-time conversion", false),
    ];

    /// <summary>
    /// Ensures each newly ordinal language declares its ordinal capability and that its ordinal
    /// examples include a compound (21-99), a value with hundreds, and a value with thousands.
    /// </summary>
    [TestMethod]
    public void OrdinalLanguages_DeclareCapabilityAndCoverStructuralValues()
    {
        foreach (string language in OrdinalLanguages)
        {
            List<MethodInfo> methods = ScenarioMethods(language);
            Assert.IsTrue(HasCapabilityScenario(methods, "ordinal conversion is supported"), $"{language}: no ordinal capability scenario.");
            Assert.IsTrue(NumberToStringConverter.GetConverter(language).SupportsOrdinals, language);

            long[] numbers = ExampleValues(methods, "ordinal")
                .Select(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ? n : -1)
                .Where(n => n > 0)
                .ToArray();
            Assert.IsTrue(numbers.Any(n => n is > 20 and < 100 && n % 10 != 0), $"{language}: no compound ordinal example.");
            Assert.IsTrue(numbers.Any(n => n is >= 100 and < 1000), $"{language}: no hundreds ordinal example.");
            Assert.IsTrue(numbers.Any(n => n >= 1000), $"{language}: no thousands ordinal example.");
        }
    }

    /// <summary>
    /// NTS-12 guard over every built-in language: an ordinal-capable converter either forms an
    /// ordinal of zero that differs from its cardinal zero, or rejects zero with
    /// <see cref="NotSupportedException"/>; it never returns the cardinal zero unchanged.
    /// </summary>
    [TestMethod]
    public void BuiltInLanguages_NeverReturnTheCardinalZeroAsOrdinal()
    {
        var converters = NumberToStringConverter.BuiltInInitialization.Converters;
        Assert.IsTrue(converters.Count > 0, "No built-in converter was initialized.");
        foreach (var (culture, converter) in converters)
        {
            if (!converter.SupportsOrdinals) continue;
            string cardinal = converter.Convert(0);
            string ordinal;
            try
            {
                ordinal = converter.ConvertOrdinal(0);
            }
            catch (NotSupportedException)
            {
                continue;
            }
            Assert.AreNotEqual(cardinal, ordinal, $"{culture}: the ordinal of zero is the unchanged cardinal.");
        }
    }

    /// <summary>
    /// Ensures each new clock language declares its clock capability and that its clock examples
    /// include a whole hour, a non-zero minute position, and a source hour above twelve (or, for
    /// the Swahili offset clock, a source hour in the second half of the day).
    /// </summary>
    [TestMethod]
    public void ClockLanguages_DeclareCapabilityAndCoverStructuralValues()
    {
        foreach (string language in ClockLanguages)
        {
            List<MethodInfo> methods = ScenarioMethods(language);
            Assert.IsTrue(HasCapabilityScenario(methods, "clock-time conversion is supported"), $"{language}: no clock capability scenario.");
            Assert.IsTrue(NumberToStringConverter.GetConverter(language).SupportsClockTimeConversion, language);

            TimeOnly[] times = ExampleValues(methods, "clock")
                .Select(value => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly t) ? (TimeOnly?)t : null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToArray();
            Assert.IsTrue(times.Any(t => t.Minute == 0), $"{language}: no whole-hour clock example.");
            Assert.IsTrue(times.Any(t => t.Minute != 0), $"{language}: no minute clock example.");
            Assert.IsTrue(times.Any(t => t.Hour > 12), $"{language}: no clock example after 12:00.");
        }
    }

    /// <summary>
    /// Ensures languages implemented as twelve literal clock-hour branches exercise every branch
    /// in their ReqnRoll feature. The expected linguistic strings remain in the feature files;
    /// this meta-test only enforces branch coverage.
    /// </summary>
    [TestMethod]
    public void LiteralClockHourLanguages_CoverEveryWholeHour()
    {
        int[] expectedHours = [.. Enumerable.Range(1, 12)];
        foreach (string language in LiteralClockHourLanguages)
        {
            int[] actualHours = [.. ExampleValues(ScenarioMethods(language), "clock")
                .Select(value => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time)
                    ? (TimeOnly?)time
                    : null)
                .Where(time => time.HasValue && time.Value.Minute == 0 && time.Value.Hour is >= 1 and <= 12)
                .Select(time => time!.Value.Hour)
                .Distinct()
                .OrderBy(hour => hour)];

            CollectionAssert.AreEqual(expectedHours, actualHours, $"{language}: literal whole-hour clock table is not fully covered.");
        }
    }

    /// <summary>Ensures deliberately deferred capabilities are stated explicitly and match the converters.</summary>
    [TestMethod]
    public void DeferredCapabilities_AreDeclaredExplicitly()
    {
        foreach (var (language, capability, supported) in DeclaredCapabilities)
        {
            string expected = supported ? $"{capability} is supported" : $"{capability} is unsupported";
            Assert.IsTrue(HasCapabilityScenario(ScenarioMethods(language), expected), $"{language}: missing '{expected}'.");
            NumberToStringConverter converter = NumberToStringConverter.GetConverter(language);
            bool actual = capability.StartsWith("ordinal", StringComparison.Ordinal)
                ? converter.SupportsOrdinals
                : converter.SupportsClockTimeConversion;
            Assert.AreEqual(supported, actual, $"{language} {capability}");
        }
    }

    /// <summary>Gets the generated scenario methods tagged with a language code.</summary>
    /// <param name="language">The feature tag (language code).</param>
    /// <returns>The tagged test methods.</returns>
    private static List<MethodInfo> ScenarioMethods(string language)
    {
        var methods = typeof(NumberToStringLanguageCoverageTests).Assembly.GetTypes()
            .Where(type => type.Namespace?.EndsWith("Features.Languages", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(method => method.GetCustomAttributes<TestCategoryAttribute>()
                .Any(category => category.TestCategories.Contains(language, StringComparer.Ordinal)))
            .ToList();
        Assert.IsTrue(methods.Count > 0, $"No generated scenario is tagged {language}.");
        return methods;
    }

    /// <summary>Determines whether a capability scenario with the given statement exists.</summary>
    /// <param name="methods">The language's scenario methods.</param>
    /// <param name="statement">The capability statement, e.g. "ordinal conversion is supported".</param>
    /// <returns><see langword="true"/> when a scenario description contains the statement.</returns>
    private static bool HasCapabilityScenario(IEnumerable<MethodInfo> methods, string statement)
        => methods.Any(method => method.GetCustomAttribute<DescriptionAttribute>()?.Description
            .Contains(statement, StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>
    /// Gets the first example column of every scenario outline whose description contains a keyword,
    /// excluding outlines that pin rejections (their values are not covered conversions).
    /// </summary>
    /// <param name="methods">The language's scenario methods.</param>
    /// <param name="keyword">The keyword selecting the outlines (e.g. "ordinal", "clock").</param>
    /// <returns>The first value of each example row.</returns>
    private static IEnumerable<string> ExampleValues(IEnumerable<MethodInfo> methods, string keyword)
        => methods
            .Where(method => method.GetCustomAttribute<DescriptionAttribute>()?.Description is { } description
                && description.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                && !description.Contains("rejected", StringComparison.OrdinalIgnoreCase))
            .SelectMany(method => method.GetCustomAttributes<DataRowAttribute>())
            .Select(row => row.Data.Length > 0 ? row.Data[0] as string : null)
            .Where(value => value != null)
            .Select(value => value!);
}
