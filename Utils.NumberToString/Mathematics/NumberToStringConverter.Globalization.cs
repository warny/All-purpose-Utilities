using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace Utils.Mathematics
{
    public partial class NumberToStringConverter
    {
        static NumberToStringConverter()
        {
            InitializeConfigurations(
                NumberConverterResources.NumberConvertionConfiguration_FR_fr_ca,
                NumberConverterResources.NumberConvertionConfiguration_FR_be_ch,
                NumberConverterResources.NumberConvertionConfiguration_DE,
                NumberConverterResources.NumberConvertionConfiguration_EN,
                NumberConverterResources.NumberConvertionConfiguration_ES,
                NumberConverterResources.NumberConvertionConfiguration_CA,
                NumberConverterResources.NumberConvertionConfiguration_EU,
                NumberConverterResources.NumberConvertionConfiguration_GL,
                NumberConverterResources.NumberConvertionConfiguration_IT,
                NumberConverterResources.NumberConvertionConfiguration_FI,
                NumberConverterResources.NumberConvertionConfiguration_AR,
                NumberConverterResources.NumberConvertionConfiguration_HE,
                NumberConverterResources.NumberConvertionConfiguration_ZH,
                NumberConverterResources.NumberConvertionConfiguration_KO,
                NumberConverterResources.NumberConvertionConfiguration_JA,
                NumberConverterResources.NumberConvertionConfiguration_PT,
                NumberConverterResources.NumberConvertionConfiguration_PL,
                NumberConverterResources.NumberConvertionConfiguration_HI,
                NumberConverterResources.NumberConvertionConfiguration_EL,
                NumberConverterResources.NumberConvertionConfiguration_NL,
                NumberConverterResources.NumberConvertionConfiguration_RU,
                NumberConverterResources.NumberConvertionConfiguration_ZU,
                NumberConverterResources.NumberConvertionConfiguration_EE,
                NumberConverterResources.NumberConvertionConfiguration_WO
            );
        }

        // Caches configurations for different cultures
        private static readonly Dictionary<string, NumberToStringConverter> CachedConfigurations = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Loads number-to-string configurations embedded as XML strings.
        /// </summary>
        /// <param name="configs">The XML documents describing language configurations.</param>
        public static void InitializeConfigurations(params string[] configs)
            => RegisterConfigurations((IEnumerable<string>)configs);

        /// <summary>
        /// Registers the provided language configurations for later lookup.
        /// </summary>
        /// <param name="configs">The XML configuration documents to load.</param>
        public static void RegisterConfigurations(IEnumerable<string> configs)
        {
            foreach (var configuration in configs)
            {
                var languages = ReadConfiguration(configuration);
                foreach (var language in languages)
                {
                    CachedConfigurations.Add(language.Key, language.Value);
                }
            }
        }

        /// <summary>
        /// Parses a configuration document into converter instances keyed by culture name.
        /// </summary>
        /// <param name="configuration">The XML configuration document.</param>
        /// <returns>A dictionary mapping culture names to converters.</returns>
        public static Dictionary<string, NumberToStringConverter> ReadConfiguration(string configuration)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Numbers), "Utils/NumberConvertionConfiguration.xsd");

            Numbers obj;
            using (StringReader reader = new StringReader(configuration))
            {
                obj = (Numbers)serializer.Deserialize(reader);
            }

            var result = new Dictionary<string, NumberToStringConverter>();
            foreach (var language in obj.Languages)
            {
                foreach (var culture in language.Cultures)
                {
                    if (!result.ContainsKey(culture))
                    {
                        result.Add(culture, ReadConverter(language, culture));
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Builds a converter for a specific language definition and culture identifier.
        /// </summary>
        /// <param name="language">The language definition deserialized from XML.</param>
        /// <param name="languageIdentifier">The culture or language identifier currently bound to the converter.</param>
        /// <returns>A configured <see cref="NumberToStringConverter"/> instance.</returns>
        private static NumberToStringConverter ReadConverter(LanguageType language, string languageIdentifier)
        {
            var confScale = language.NumberScale;

            var scale = new NumberScale(
                confScale.StaticNames.Scales.OrderBy(n => n.Value).Select(n => n.StringValue).ToArray(),
                confScale.Suffixes?.Values?.ToArray() ?? Array.Empty<string>(),
                confScale.StartIndex,
                confScale.VoidGroup,
                confScale.GroupSeparator,
                confScale.Scale0Prefixes?.Digits.OrderBy(n => n.Digit).Select(n => n.StringValue).ToArray(),
                confScale.UnitsPrefixes?.Digits.OrderBy(n => n.Digit).Select(n => n.StringValue).ToArray(),
                confScale.TensPrefixes?.Digits.OrderBy(n => n.Digit).Select(n => n.StringValue).ToArray(),
                confScale.HundredsPrefixes?.Digits.OrderBy(n => n.Digit).Select(n => n.StringValue).ToArray(),
                confScale.FirstLetterUpperCase
            );

            INumberToStringLanguageSpecifics languageSpecifics = ResolveLanguageSpecifics(language.LanguageSpecificsTypeName);
            var fractions = language.Fractions?.Fractions?.ToDictionary(f => f.Digits, f => f.StringValue) ?? new Dictionary<int, string>();

            BigInteger? maxNumber = null;
            if (!string.IsNullOrWhiteSpace(language.MaxNumber))
            {
                maxNumber = BigInteger.Parse(language.MaxNumber, CultureInfo.InvariantCulture);
            }

            return new NumberToStringConverter(
                language.GroupSize,
                language.Separator,
                language.GroupSeparator,
                language.Zero,
                language.Minus,
                language.DecimalSeparator,
                language.Groups.Groups.ToDictionary(g => g.Level, g => (DigitListType)g),
                language.Exceptions?.Numbers?.ToDictionary(e => (long)e.Value, e => e.StringValue) ?? new Dictionary<long, string>(),
                language.Replacements?.Replacements
                    .Select(r => new NumberToStringConverter.ReplacementRule(r.OldValue, r.NewValue, r.Scope)),
                scale,
                adjustFunction: null,
                languageSpecifics,
                languageIdentifier,
                fractions,
                maxNumber,
                language.FractionSeparator
            );
        }

        /// <summary>
        /// Resolves a language-specific finalizer from a configured type name.
        /// </summary>
        /// <param name="typeName">The configured type name.</param>
        /// <returns>The resolved instance, or a no-op implementation when the type cannot be resolved.</returns>
        private static INumberToStringLanguageSpecifics ResolveLanguageSpecifics(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return new DefaultNumberToStringLanguageSpecifics();
            }

            Type specificsType = Type.GetType(typeName, throwOnError: false);
            if (specificsType == null)
            {
                specificsType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .FirstOrDefault(t => string.Equals(t.FullName, typeName, StringComparison.Ordinal)
                                         || string.Equals(t.Name, typeName, StringComparison.Ordinal));
            }

            if (specificsType == null
                || !typeof(INumberToStringLanguageSpecifics).IsAssignableFrom(specificsType)
                || specificsType.IsAbstract
                || specificsType.IsInterface)
            {
                return new DefaultNumberToStringLanguageSpecifics();
            }

            return Activator.CreateInstance(specificsType) as INumberToStringLanguageSpecifics
                   ?? new DefaultNumberToStringLanguageSpecifics();
        }

        /// <summary>
        /// Safely enumerates types from an assembly.
        /// </summary>
        /// <param name="assembly">The assembly to inspect.</param>
        /// <returns>The loadable types from <paramref name="assembly"/>.</returns>
        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }

        /// <summary>
        /// Retrieves a configuration resource by suffix from the embedded resource manager.
        /// </summary>
        /// <param name="suffix">The culture suffix that identifies the resource.</param>
        /// <returns>The XML configuration content for the requested culture.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the configuration resource cannot be found.</exception>
        private static string GetConfigurationResource(string suffix)
        {
            string resourceName = $"NumberConvertionConfiguration.{suffix}";
            string? configuration = NumberConverterResources.ResourceManager.GetString(resourceName, NumberConverterResources.Culture);

            if (configuration == null)
            {
                throw new InvalidOperationException($"Number conversion configuration resource '{resourceName}' was not found.");
            }

            return configuration;
        }
    }
}
