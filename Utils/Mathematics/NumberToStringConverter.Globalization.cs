using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Serialization;
using Utils.Expressions;
using Utils.String;

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
                var converter = ReadConverter(language);
                foreach (var culture in language.Cultures)
                {
                    if (!result.ContainsKey(culture))
                    {
                        result.Add(culture, converter);
                    }
                }
            }
            return result;
        }

        private static NumberToStringConverter ReadConverter(LanguageType language)
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

            Func<string, string> adjustFunction;
            if (language.AdjustFunction.IsNullOrWhiteSpace())
            {
                adjustFunction = s => s;
            }
            else
            {
                var e = ExpressionParser.Parse<Func<string, string>>(language.AdjustFunction, ["System.Text", "System.Text.RegularExpressions"]);
                adjustFunction = e.Compile();
            }
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
                language.Replacements?.Replacements.ToDictionary(r => r.OldValue, r => r.NewValue),
                scale,
                adjustFunction,
                fractions,
                maxNumber,
                language.FractionSeparator
            );
        }
    }
}
