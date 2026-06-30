using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace Utils.NumberToString
{
    public partial class NumberToStringConverter
    {
        static NumberToStringConverter()
        {
            InitializeConfigurations(
                NumberConverterResources.NumberConvertionConfiguration_FR_fr_ca,
                NumberConverterResources.NumberConvertionConfiguration_FR_be_ch,
                NumberConverterResources.NumberConvertionConfiguration_DE,
                NumberConverterResources.NumberConvertionConfiguration_DE_ch,
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

        // Explicitly registered language-specifics instances, consulted before reflection
        private static readonly Dictionary<string, INumberToStringLanguageSpecifics> _registeredSpecifics = new(StringComparer.Ordinal);

        /// <summary>
        /// Registers an <see cref="INumberToStringLanguageSpecifics"/> instance under a given type name
        /// so that XML configurations referencing that name find it without reflection.
        /// </summary>
        /// <param name="typeName">
        /// The type name as it appears in <c>&lt;LanguageSpecifics&gt;</c> elements (full or short name).
        /// </param>
        /// <param name="instance">The instance to register.</param>
        public static void RegisterLanguageSpecifics(string typeName, INumberToStringLanguageSpecifics instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            _registeredSpecifics[typeName] = instance;
        }

        /// <summary>
        /// Loads number-to-string configurations embedded as XML strings.
        /// Duplicate culture keys are silently ignored (first registration wins).
        /// </summary>
        /// <param name="configs">The XML documents describing language configurations.</param>
        public static void InitializeConfigurations(params string[] configs)
            => RegisterConfigurations((IEnumerable<string>)configs);

        /// <summary>
        /// Registers the provided language configurations for later lookup.
        /// Duplicate culture keys are silently ignored (first registration wins).
        /// </summary>
        /// <param name="configs">The XML configuration documents to load.</param>
        public static void RegisterConfigurations(IEnumerable<string> configs)
        {
            foreach (var configuration in configs)
            {
                var languages = ReadConfiguration(configuration);
                foreach (var language in languages)
                {
                    CachedConfigurations.TryAdd(language.Key, language.Value);
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

            static IEnumerable<NumberToStringConverter.ReplacementRule> ParseReplacements(ReplacementsListType list) =>
                list?.Replacements?
                    .Where(r => r.NewValue != null)
                    .Select(r => new NumberToStringConverter.ReplacementRule(r.OldValue, r.NewValue!, r.Scope))
                ?? [];

            static IReadOnlyList<NumberToStringConverter.VariantDimension> ParseVariantDimensions(VariantsType variants) =>
                variants?.Dimensions?
                    .Select(d => new NumberToStringConverter.VariantDimension(
                        d.Name,
                        d.ValuesRaw?.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList()
                            ?? new List<string>(),
                        string.IsNullOrWhiteSpace(d.LocalName) ? null : d.LocalName.Trim()))
                    .ToList()
                ?? new List<NumberToStringConverter.VariantDimension>();

            // Build a normalizer that maps both canonical name and localName to the canonical name.
            // Used when loading variant constraints from XML attributes so that <Variant genus="…">
            // and <Variant gender="…"> are treated identically after German was renamed.
            var parsedDimensions = ParseVariantDimensions(language.Variants);
            var nameNormalizer = parsedDimensions
                .SelectMany(d => string.IsNullOrEmpty(d.LocalName)
                    ? (IEnumerable<(string, string)>)[(d.Name, d.Name)]
                    : [(d.Name, d.Name), (d.LocalName, d.Name)])
                .ToDictionary(t => t.Item1, t => t.Item2, StringComparer.OrdinalIgnoreCase);

            string NormalizeDimName(string raw) =>
                nameNormalizer.TryGetValue(raw, out var canonical) ? canonical : raw;

            IReadOnlyList<NumberToStringConverter.VariantRule> ParseVariantRules(VariantsType variants)
            {
                var result = new List<NumberToStringConverter.VariantRule>();

                if (variants?.Variants?.Count > 0)
                {
                    foreach (var variant in variants.Variants)
                        CollectVariantRules(variant, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), result);
                }

                // Expand form-variant replacements into synthetic VariantRule entries.
                // Multiple <Replacement> elements that share a constraint set are merged so that
                // one VariantRule entry holds all replacement rules for that combination.
                var syntheticByKey =
                    new Dictionary<string, (Dictionary<string, string> Constraints,
                                            List<NumberToStringConverter.ReplacementRule> Replacements)>(StringComparer.Ordinal);

                foreach (var repl in language.Replacements?.Replacements ?? [])
                {
                    if (repl.FormVariants?.Count > 0)
                    {
                        foreach (var (c, form) in ExpandFormVariants(repl.FormVariants, parsedDimensions,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                        {
                            var key = ConstraintKey(c);
                            if (!syntheticByKey.TryGetValue(key, out var entry))
                            {
                                entry = (c, []);
                                syntheticByKey[key] = entry;
                            }
                            entry.Replacements.Add(new NumberToStringConverter.ReplacementRule(repl.OldValue, form, repl.Scope));
                        }
                    }
                }

                foreach (var (constraints, replacements) in syntheticByKey.Values)
                    result.Add(new NumberToStringConverter.VariantRule(constraints, replacements));

                return result;
            }

            void CollectVariantRules(
                VariantType variant,
                Dictionary<string, string> inheritedConstraints,
                List<NumberToStringConverter.VariantRule> result)
            {
                string? dimType = string.IsNullOrEmpty(variant.DimensionType)
                    ? null : NormalizeDimName(variant.DimensionType);

                // values="a,b,c" expands to one rule per value; variant="x" is the single-value form.
                IEnumerable<string> dimValues =
                    !string.IsNullOrEmpty(variant.VariantValues)
                        ? variant.VariantValues.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0)
                        : !string.IsNullOrEmpty(variant.VariantValue)
                            ? [variant.VariantValue]
                            : [""];

                var replacements = (variant.Replacements ?? [])
                    .Where(r => r.NewValue != null)
                    .Select(r => new NumberToStringConverter.ReplacementRule(r.OldValue, r.NewValue!, r.Scope))
                    .ToList();

                foreach (var dimValue in dimValues)
                {
                    var constraints = new Dictionary<string, string>(inheritedConstraints, StringComparer.OrdinalIgnoreCase);
                    if (dimType != null && dimValue.Length > 0)
                        constraints[dimType] = dimValue;

                    result.Add(new NumberToStringConverter.VariantRule(constraints, replacements));

                    foreach (var child in variant.NestedVariants ?? [])
                        CollectVariantRules(child, constraints, result);
                }
            }

            // Returns a stable string key for a constraint dictionary, used to merge form-variant
            // rules that share the same (dimension-type, dimension-value) combination.
            static string ConstraintKey(IReadOnlyDictionary<string, string> c) =>
                string.Join("|", c.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                                   .Select(kvp => $"{kvp.Key}={kvp.Value}"));

            // Walks a FormVariantType tree and yields (constraints, form) pairs.
            // Intermediate nodes (variant attribute present, children present) add one constraint
            // and recurse; leaf nodes (forms attribute present) expand positional entries using the
            // matching Dimension declaration order.
            IEnumerable<(Dictionary<string, string> Constraints, string Form)> ExpandFormVariants(
                IEnumerable<FormVariantType> nodes,
                IReadOnlyList<NumberToStringConverter.VariantDimension> dims,
                IReadOnlyDictionary<string, string> inherited)
            {
                foreach (var node in nodes)
                {
                    var constraints = new Dictionary<string, string>(inherited, StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(node.DimensionType) && !string.IsNullOrEmpty(node.VariantValue))
                        constraints[NormalizeDimName(node.DimensionType)] = node.VariantValue;

                    if (!string.IsNullOrEmpty(node.Forms) && !string.IsNullOrEmpty(node.DimensionType))
                    {
                        var dimName = NormalizeDimName(node.DimensionType);
                        var dimValues = dims
                            .FirstOrDefault(d =>
                                string.Equals(d.Name, dimName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(d.LocalName, dimName, StringComparison.OrdinalIgnoreCase))
                            ?.Values ?? (IReadOnlyList<string>)[];

                        var entries = node.Forms.Split(',');
                        for (int i = 0; i < Math.Min(entries.Length, dimValues.Count); i++)
                        {
                            var form = entries[i].Trim();
                            if (string.IsNullOrEmpty(form)) continue;
                            var leafConstraints = new Dictionary<string, string>(constraints, StringComparer.OrdinalIgnoreCase)
                                { [dimName] = dimValues[i] };
                            yield return (leafConstraints, form);
                        }
                    }
                    else
                    {
                        foreach (var pair in ExpandFormVariants(node.NestedVariants ?? [], dims, constraints))
                            yield return pair;
                    }
                }
            }

            IReadOnlyList<NumberToStringConverter.OrdinalVariantRule> ParseOrdinalVariants(OrdinalsType? ordinals)
            {
                var result = new List<NumberToStringConverter.OrdinalVariantRule>();

                // --- Structural OrdinalVariants container (suffix/removeTrailing/word-rule overrides) ---
                var container = ordinals?.OrdinalVariantsContainer;
                if (container?.Variants?.Count > 0)
                {
                    foreach (var variant in container.Variants)
                        CollectOrdinalVariants(variant, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), result);
                }

                // --- Form-variant expansion from OrdinalException and Ordinal elements ---
                // Multiple elements can contribute to the same constraint set (e.g. an exception for
                // value 1 and a word rule for "один" both needing {gender=feminin, case=acc} = "первую").
                // We merge them into a single OrdinalVariantRule per constraint key so that
                // FindBestOrdinalVariant returns one rule containing both the exception dict and the
                // word-rule dict for that combination — preventing priority collisions at runtime.
                var syntheticByKey =
                    new Dictionary<string, (Dictionary<string, string> Constraints,
                                            Dictionary<long, string> Exceptions,
                                            Dictionary<string, string> WordRules)>(StringComparer.Ordinal);

                (Dictionary<string, string> c, Dictionary<long, string> e, Dictionary<string, string> w)
                    GetOrAddSynthetic(string key, Dictionary<string, string> constraints)
                {
                    if (!syntheticByKey.TryGetValue(key, out var entry))
                    {
                        entry = (constraints, new Dictionary<long, string>(), new Dictionary<string, string>());
                        syntheticByKey[key] = entry;
                    }
                    return entry;
                }

                foreach (var exc in ordinals?.Exceptions ?? [])
                {
                    if (exc.FormVariants?.Count > 0)
                    {
                        foreach (var (c, form) in ExpandFormVariants(exc.FormVariants, parsedDimensions,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                        {
                            var entry = GetOrAddSynthetic(ConstraintKey(c), c);
                            entry.e[exc.Value] = form;
                        }
                    }
                }

                foreach (var rule in ordinals?.Rules ?? [])
                {
                    if (rule.FormVariants?.Count > 0)
                    {
                        foreach (var (c, form) in ExpandFormVariants(rule.FormVariants, parsedDimensions,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                        {
                            var entry = GetOrAddSynthetic(ConstraintKey(c), c);
                            entry.w[rule.From] = form;
                        }
                    }
                }

                foreach (var (constraints, exceptions, wordRules) in syntheticByKey.Values)
                    result.Add(new NumberToStringConverter.OrdinalVariantRule(constraints, exceptions, wordRules, null, null));

                return result;
            }

            void CollectOrdinalVariants(
                OrdinalVariantElementType variant,
                Dictionary<string, string> inheritedConstraints,
                List<NumberToStringConverter.OrdinalVariantRule> result)
            {
                string? dimType = string.IsNullOrEmpty(variant.DimensionType)
                    ? null : NormalizeDimName(variant.DimensionType);

                // values="a,b,c" expands to one rule per value; variant="x" is the single-value form.
                IEnumerable<string> dimValues =
                    !string.IsNullOrEmpty(variant.VariantValues)
                        ? variant.VariantValues.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0)
                        : !string.IsNullOrEmpty(variant.VariantValue)
                            ? [variant.VariantValue]
                            : [""];

                var exceptions = variant.Exceptions?.Where(e => e.StringValue != null)
                    .ToDictionary(e => e.Value, e => e.StringValue!)
                    ?? new Dictionary<long, string>();
                var wordRules = variant.Rules?.Where(r => r.To != null)
                    .ToDictionary(r => r.From, r => r.To!)
                    ?? new Dictionary<string, string>();

                foreach (var dimValue in dimValues)
                {
                    var constraints = new Dictionary<string, string>(inheritedConstraints, StringComparer.OrdinalIgnoreCase);
                    if (dimType != null && dimValue.Length > 0)
                        constraints[dimType] = dimValue;

                    result.Add(new NumberToStringConverter.OrdinalVariantRule(
                        constraints, exceptions, wordRules, variant.Suffix, variant.RemoveTrailing));

                    foreach (var child in variant.NestedVariants ?? [])
                        CollectOrdinalVariants(child, constraints, result);
                }
            }

            var options = new NumberToStringConverterOptions
            {
                Group = language.GroupSize,
                Separator = language.Separator,
                GroupSeparator = language.GroupSeparator,
                Zero = language.Zero,
                Minus = language.Minus,
                DecimalSeparator = language.DecimalSeparator,
                Groups = language.Groups.Groups.ToDictionary(g => g.Level, g => (DigitListType)g),
                Exceptions = language.Exceptions?.Numbers?.ToDictionary(e => (long)e.Value, e => e.StringValue)
                    ?? new Dictionary<long, string>(),
                Replacements = ParseReplacements(language.Replacements),
                Scale = scale,
                LanguageSpecifics = ResolveLanguageSpecifics(language.LanguageSpecificsTypeName),
                LanguageIdentifier = languageIdentifier,
                Fractions = language.Fractions?.Fractions?.ToDictionary(f => f.Digits, f => f.StringValue)
                    ?? new Dictionary<int, string>(),
                MaxNumber = string.IsNullOrWhiteSpace(language.MaxNumber)
                    ? null
                    : BigInteger.Parse(language.MaxNumber, CultureInfo.InvariantCulture),
                FractionSeparator = language.FractionSeparator,
                OrdinalSuffix = language.Ordinals?.Suffix,
                OrdinalRemoveTrailing = language.Ordinals?.RemoveTrailing,
                OrdinalExceptions = language.Ordinals?.Exceptions?
                    .Where(e => e.StringValue != null)
                    .ToDictionary(e => e.Value, e => e.StringValue!)
                    ?? new Dictionary<long, string>(),
                OrdinalWordRules = language.Ordinals?.Rules?
                    .Where(r => r.To != null)
                    .ToDictionary(r => r.From, r => r.To!)
                    ?? new Dictionary<string, string>(),
                OrdinalPrefix = language.Ordinals?.Prefix,
                OrdinalVariants = ParseOrdinalVariants(language.Ordinals),
                VariantDimensions = parsedDimensions,
                VariantRules = ParseVariantRules(language.Variants),
                YearFormat = language.YearFormat == null ? null : new YearFormatOptions(
                    language.YearFormat.HundredWord,
                    language.YearFormat.ZeroConnector,
                    language.YearFormat.SplitRanges.Select(r => (r.From, r.To)).ToList()),
            };

            return new NumberToStringConverter(options);
        }

        /// <summary>
        /// Resolves a language-specific finalizer from a configured type name.
        /// Explicitly registered instances (via <see cref="RegisterLanguageSpecifics"/>) take
        /// priority over the reflection-based lookup.
        /// </summary>
        /// <param name="typeName">The configured type name.</param>
        /// <returns>The resolved instance, or a no-op implementation when the type cannot be resolved.</returns>
        private static INumberToStringLanguageSpecifics ResolveLanguageSpecifics(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return new DefaultNumberToStringLanguageSpecifics();
            }

            if (_registeredSpecifics.TryGetValue(typeName, out var registered))
            {
                return registered;
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
