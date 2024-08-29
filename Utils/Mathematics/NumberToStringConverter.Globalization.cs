using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics
{
    public partial class NumberToStringConverter
    {
        static NumberToStringConverter() {
            InitializeConfigurations(
                NumberConverterResources.NumberConvertionConfiguration_FR_fr_ca,
                NumberConverterResources.NumberConvertionConfiguration_FR_be_ch,
                NumberConverterResources.NumberConvertionConfiguration_DE,
                NumberConverterResources.NumberConvertionConfiguration_EN
            );
        }

		// Caches configurations for different cultures
		private static readonly Dictionary<string, NumberToStringConverter> configurations = new Dictionary<string, NumberToStringConverter>(StringComparer.InvariantCultureIgnoreCase);

		public static void InitializeConfigurations(params string[] configurations)
            => RegisterConfigurations((IEnumerable<string>)configurations);
        public static void RegisterConfigurations(IEnumerable<string> configurations) {
            foreach (var configuration in configurations)
            {
                var languages = ReadConfiguration(configuration);
                foreach (var language in languages)
                {
                    NumberToStringConverter.configurations.Add(language.Key, language.Value);
                }
            }
        }

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
                    result.Add(culture, converter);
                }
            }
            return result;
        }

        private static NumberToStringConverter ReadConverter(LanguageType language)
        {
            var confScale = language.NumberScale;

            var scale = new NumberScale(
                confScale.StaticNames.Scales.OrderBy(n => n.Value).Select(n => n.StringValue).ToArray(),
                confScale.Suffixes.Values,
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
            if (language.AdjustFunction.IsNullOrWhiteSpace()) {
                adjustFunction = s => s;
            } else {
                var e = ExpressionParser.Parse<Func<string, string>>(language.AdjustFunction, ["System.Text", "System.Text.RegularExpressions"]);
                adjustFunction = e.Compile();
            }

            return new NumberToStringConverter(
                language.GroupSize,
                language.Separator,
                language.GroupSeparator,
                language.Zero,
                language.Minus,
                language.Groups.Groups.ToDictionary(g=>g.Level, g=>(DigitListType)g),
                language.Exceptions.Numbers.ToDictionary(e=>(long)e.Value, e=>e.StringValue),
                language.Replacements?.Replacements.ToDictionary(r=>r.OldValue, r=>r.NewValue),
                scale,
                adjustFunction
            );
        }
    }
}
