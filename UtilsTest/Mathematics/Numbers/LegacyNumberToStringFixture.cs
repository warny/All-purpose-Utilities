using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Completes legacy XML fixtures whose original tests target configuration concerns other than digit completeness.</summary>
internal static class LegacyNumberToStringFixture
{
    /// <summary>Completes final digit tables and derives a scale-compatible maximum before loading a legacy fixture.</summary>
    /// <param name="configuration">The legacy XML configuration.</param>
    /// <returns>The converters built from the completed fixture.</returns>
    internal static Dictionary<string, NumberToStringConverter> ReadConfiguration(string configuration)
        => NumberToStringConverter.ReadConfiguration(Complete(configuration));

    /// <summary>Completes a legacy fixture without loading it.</summary>
    /// <param name="configuration">The legacy XML configuration.</param>
    /// <returns>The completed XML.</returns>
    internal static string Complete(string configuration)
    {
        XDocument document = XDocument.Parse(configuration);
        XNamespace ns = "Utils/NumberConvertionConfiguration.xsd";
        foreach (XElement language in document.Root?.Elements(ns + "Language") ?? [])
        {
            foreach (XElement group in language.Descendants(ns + "Group"))
            {
                var present = group.Elements(ns + "Digit")
                    .Select(d => (int?)d.Attribute("digit"))
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .ToHashSet();
                for (int digit = 0; digit <= 9; digit++)
                    if (!present.Contains(digit))
                        group.Add(new XElement(ns + "Digit",
                            new XAttribute("digit", digit),
                            new XAttribute("string", digit.ToString(CultureInfo.InvariantCulture))));
            }

            bool inherits = language.Attribute("baseOn") != null;
            string[] dynamicParts = ["Suffixes", "Scale0Prefixes", "UnitsPrefixes", "TensPrefixes", "HundredsPrefixes"];
            bool unbounded = dynamicParts.All(name => language.Descendants(ns + name).Any());
            if (!inherits && !unbounded && language.Attribute("maxNumber") == null)
            {
                int groupSize = (int?)language.Attribute("groupSize") ?? 3;
                var indices = language.Descendants(ns + "Scale")
                    .Select(s => (int?)s.Attribute("value"))
                    .Where(i => i.HasValue)
                    .Select(i => i!.Value)
                    .ToArray();
                int maximumScaleIndex = indices.Length == 0 ? 0 : Math.Max(0, indices.Max());
                int exponent = Math.Max(1, groupSize) * (maximumScaleIndex + 1);
                language.SetAttributeValue("maxNumber", BigInteger.Pow(10, exponent) - 1);
            }
        }
        return document.ToString(SaveOptions.DisableFormatting);
    }
}
