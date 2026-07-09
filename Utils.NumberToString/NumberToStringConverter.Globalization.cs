using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using Utils.Range;

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
                NumberConverterResources.NumberConvertionConfiguration_DA,
                NumberConverterResources.NumberConvertionConfiguration_EN,
                NumberConverterResources.NumberConvertionConfiguration_ES,
                NumberConverterResources.NumberConvertionConfiguration_BG,
                NumberConverterResources.NumberConvertionConfiguration_CA,
                NumberConverterResources.NumberConvertionConfiguration_EU,
                NumberConverterResources.NumberConvertionConfiguration_FA,
                NumberConverterResources.NumberConvertionConfiguration_GL,
                NumberConverterResources.NumberConvertionConfiguration_IT,
                NumberConverterResources.NumberConvertionConfiguration_CS,
                NumberConverterResources.NumberConvertionConfiguration_SK,
                NumberConverterResources.NumberConvertionConfiguration_FI,
                NumberConverterResources.NumberConvertionConfiguration_AR,
                NumberConverterResources.NumberConvertionConfiguration_HE,
                NumberConverterResources.NumberConvertionConfiguration_HR,
                NumberConverterResources.NumberConvertionConfiguration_HU,
                NumberConverterResources.NumberConvertionConfiguration_ZH,
                NumberConverterResources.NumberConvertionConfiguration_KO,
                NumberConverterResources.NumberConvertionConfiguration_JA,
                NumberConverterResources.NumberConvertionConfiguration_PT,
                NumberConverterResources.NumberConvertionConfiguration_PL,
                NumberConverterResources.NumberConvertionConfiguration_HI,
                NumberConverterResources.NumberConvertionConfiguration_ID,
                NumberConverterResources.NumberConvertionConfiguration_EL,
                NumberConverterResources.NumberConvertionConfiguration_NL,
                NumberConverterResources.NumberConvertionConfiguration_NO,
                NumberConverterResources.NumberConvertionConfiguration_RO,
                NumberConverterResources.NumberConvertionConfiguration_RU,
                NumberConverterResources.NumberConvertionConfiguration_SV,
                NumberConverterResources.NumberConvertionConfiguration_SW,
                NumberConverterResources.NumberConvertionConfiguration_TR,
                NumberConverterResources.NumberConvertionConfiguration_UK,
                NumberConverterResources.NumberConvertionConfiguration_VN,
                NumberConverterResources.NumberConvertionConfiguration_ZU,
                NumberConverterResources.NumberConvertionConfiguration_EE,
                NumberConverterResources.NumberConvertionConfiguration_WO
            );
        }

        // Caches configurations for different cultures — ConcurrentDictionary for thread-safety
        private static readonly ConcurrentDictionary<string, NumberToStringConverter> CachedConfigurations = new(StringComparer.InvariantCultureIgnoreCase);

        // Explicitly registered language-specifics instances, consulted before reflection
        private static readonly ConcurrentDictionary<string, INumberToStringLanguageSpecifics> _registeredSpecifics = new(StringComparer.Ordinal);

        // Stores resolved LanguageType objects for cross-document baseOn resolution
        private static readonly ConcurrentDictionary<string, LanguageType> _cachedLanguageTypes = new(StringComparer.OrdinalIgnoreCase);

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

            // Build a within-document lookup so baseOn can reference a language declared earlier
            // in the same XML document (case-insensitive culture keys).
            var docLanguageTypes = new Dictionary<string, LanguageType>(StringComparer.OrdinalIgnoreCase);
            foreach (var lang in obj.Languages)
                foreach (var culture in lang.Cultures ?? [])
                    docLanguageTypes.TryAdd(culture, lang);

            var result = new Dictionary<string, NumberToStringConverter>();
            foreach (var language in obj.Languages)
            {
                var resolved = string.IsNullOrEmpty(language.BaseOn)
                    ? language
                    : ResolveBaseOn(language, docLanguageTypes);

                // Cache the resolved LanguageType so other documents can reference it via baseOn.
                foreach (var culture in resolved.Cultures ?? [])
                    _cachedLanguageTypes.TryAdd(culture, resolved);

                foreach (var culture in resolved.Cultures ?? [])
                {
                    if (!result.ContainsKey(culture))
                        result.Add(culture, ReadConverter(resolved, culture));
                }
            }
            return result;
        }

        /// <summary>
        /// Looks up the base language and merges it with the child, producing a fully resolved
        /// <see cref="LanguageType"/>. The resolved cache is checked first so that a chain such as
        /// <c>BASE → MID → CHILD</c> always merges against the fully resolved ancestor even when
        /// all three are declared in the same XML document. If the base is found only in
        /// <paramref name="docLanguages"/> (not yet cached) and also carries a <c>baseOn</c>
        /// attribute, it is resolved recursively before the merge.
        /// Throws <see cref="InvalidOperationException"/> when the referenced base cannot be found.
        /// </summary>
        private static LanguageType ResolveBaseOn(
            LanguageType child,
            IReadOnlyDictionary<string, LanguageType> docLanguages)
        {
            string baseKey = child.BaseOn;

            // Prefer the already-resolved version from the cache (handles multi-document chains
            // and in-order same-document chains where the base was already processed).
            if (!_cachedLanguageTypes.TryGetValue(baseKey, out var baseType))
            {
                if (!docLanguages.TryGetValue(baseKey, out baseType))
                    throw new InvalidOperationException(
                        $"Language configuration error: baseOn=\"{baseKey}\" cannot be resolved. " +
                        $"Ensure the base language is registered before the derived language.");

                // The base was found in the raw same-document dict but not yet cached.
                // If it itself carries a baseOn, resolve it recursively so the merge always
                // operates on a fully inherited configuration.
                if (!string.IsNullOrEmpty(baseType.BaseOn))
                    baseType = ResolveBaseOn(baseType, docLanguages);
            }

            return MergeLanguageType(baseType!, child);
        }

        /// <summary>
        /// Returns a new <see cref="LanguageType"/> where <paramref name="child"/> values override
        /// corresponding fields of <paramref name="baseType"/>. A null/default/empty value in the
        /// child means "inherit from base"; a non-null value means "override". For
        /// <see cref="OrdinalsType"/>, exceptions and word-rules are merged element-by-element
        /// so a child can extend rather than replace the base's ordinal configuration.
        /// </summary>
        private static LanguageType MergeLanguageType(LanguageType baseType, LanguageType child) =>
            new()
            {
                Cultures = child.Cultures,
                BaseOn = null,
                GroupSize = child.GroupSize != 0 ? child.GroupSize : baseType.GroupSize,
                Separator = child.Separator ?? baseType.Separator,
                GroupSeparator = child.GroupSeparator ?? baseType.GroupSeparator,
                Zero = child.Zero ?? baseType.Zero,
                Minus = child.Minus ?? baseType.Minus,
                DecimalSeparator = child.DecimalSeparator ?? baseType.DecimalSeparator,
                FractionSeparator = child.FractionSeparator ?? baseType.FractionSeparator,
                MaxNumber = child.MaxNumber ?? baseType.MaxNumber,
                Groups = child.Groups ?? baseType.Groups,
                Exceptions = child.Exceptions ?? baseType.Exceptions,
                NumberScale = child.NumberScale ?? baseType.NumberScale,
                Replacements = child.Replacements ?? baseType.Replacements,
                LanguageSpecificsTypeName = !string.IsNullOrEmpty(child.LanguageSpecificsTypeName)
                    ? child.LanguageSpecificsTypeName : baseType.LanguageSpecificsTypeName,
                Fractions = child.Fractions ?? baseType.Fractions,
                Ordinals = MergeOrdinalsType(baseType.Ordinals, child.Ordinals),
                Variants = child.Variants ?? baseType.Variants,
                YearFormat = child.YearFormat ?? baseType.YearFormat,
                Triggers = (child.Triggers?.Count > 0) ? child.Triggers : baseType.Triggers,
                Multiplicatives = child.Multiplicatives ?? baseType.Multiplicatives,
                GroupConnector = child.GroupConnector ?? baseType.GroupConnector,
                GroupConnectorThresholdString = child.GroupConnectorThresholdString ?? baseType.GroupConnectorThresholdString,
                IntraGroupConnector = child.IntraGroupConnector ?? baseType.IntraGroupConnector,
                IntraGroupConnectorThresholdString = child.IntraGroupConnectorThresholdString ?? baseType.IntraGroupConnectorThresholdString,
                ScaleConnector = child.ScaleConnector ?? baseType.ScaleConnector,
                ScaleConnectorThresholdString = child.ScaleConnectorThresholdString ?? baseType.ScaleConnectorThresholdString,
                TimeUnits = child.TimeUnits ?? baseType.TimeUnits,
                DateFormat = child.DateFormat ?? baseType.DateFormat,
            };

        /// <summary>
        /// Merges two <see cref="OrdinalsType"/> instances. When <paramref name="childOrdinals"/> is
        /// <see langword="null"/>, the base is returned unchanged. Otherwise ordinal exceptions and
        /// word-rules from the base are merged with those from the child (child values win on conflict,
        /// new values are added), while suffix, prefix, removeTrailing, and OrdinalVariants are
        /// overridden only when the child provides them explicitly.
        /// </summary>
        private static OrdinalsType? MergeOrdinalsType(OrdinalsType? baseOrdinals, OrdinalsType? childOrdinals)
        {
            if (childOrdinals == null) return baseOrdinals;
            if (baseOrdinals == null) return childOrdinals;

            // Merge OrdinalExceptions: base list, child overrides matching values, new values appended.
            var mergedExceptions = new List<OrdinalExceptionType>(baseOrdinals.Exceptions ?? []);
            foreach (var childExc in childOrdinals.Exceptions ?? [])
            {
                var existing = mergedExceptions.FirstOrDefault(e => e.Value == childExc.Value);
                if (existing != null) mergedExceptions.Remove(existing);
                mergedExceptions.Add(childExc);
            }

            // Merge OrdinalRules: base list, child overrides matching "from" keys, new rules appended.
            var mergedRules = new List<OrdinalRuleType>(baseOrdinals.Rules ?? []);
            foreach (var childRule in childOrdinals.Rules ?? [])
            {
                var existing = mergedRules.FirstOrDefault(r =>
                    string.Equals(r.From, childRule.From, StringComparison.Ordinal));
                if (existing != null) mergedRules.Remove(existing);
                mergedRules.Add(childRule);
            }

            return new OrdinalsType
            {
                Suffix = childOrdinals.Suffix ?? baseOrdinals.Suffix,
                RemoveTrailing = childOrdinals.RemoveTrailing ?? baseOrdinals.RemoveTrailing,
                Prefix = childOrdinals.Prefix ?? baseOrdinals.Prefix,
                Exceptions = mergedExceptions,
                Rules = mergedRules,
                OrdinalVariantsContainer = childOrdinals.OrdinalVariantsContainer ?? baseOrdinals.OrdinalVariantsContainer,
            };
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
                    .Select(r => new NumberToStringConverter.ReplacementRule(r.OldValue, r.NewValue!, r.Scope, r.OnScale,
                        r.OnValue))
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
                            entry.Replacements.Add(new NumberToStringConverter.ReplacementRule(repl.OldValue, form, repl.Scope, repl.OnScale,
                                repl.OnValue));
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

                if (dimType != null && string.IsNullOrEmpty(variant.VariantValue) && string.IsNullOrEmpty(variant.VariantValues))
                    throw new InvalidOperationException(
                        $"Variant with type=\"{variant.DimensionType}\" must declare either a \"variant\" or a \"values\" attribute.");

                // values="a,b,c" expands to one rule per value; variant="x" is the single-value form.
                IEnumerable<string> dimValues =
                    !string.IsNullOrEmpty(variant.VariantValues)
                        ? variant.VariantValues.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0)
                        : !string.IsNullOrEmpty(variant.VariantValue)
                            ? [variant.VariantValue]
                            : [""];

                var replacements = (variant.Replacements ?? [])
                    .Where(r => r.NewValue != null)
                    .Select(r => new NumberToStringConverter.ReplacementRule(r.OldValue, r.NewValue!, r.Scope, r.OnScale,
                        r.OnValue))
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

                    if (!string.IsNullOrEmpty(node.Value))
                    {
                        // Single-value shorthand: variant="X" value="form" — yields exactly one (constraints, form) pair.
                        yield return (constraints, node.Value);
                    }
                    else if (!string.IsNullOrEmpty(node.Forms) && !string.IsNullOrEmpty(node.DimensionType))
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

                // First forms from elements that omit string=/to= become defaults
                // (empty-constraint fallback so no-variant calls still resolve correctly).
                var fallbackExceptions = new Dictionary<long, string>();
                var fallbackWordRules  = new Dictionary<string, string>();

                foreach (var exc in ordinals?.Exceptions ?? [])
                {
                    if (exc.FormVariants?.Count > 0)
                    {
                        bool captureFirst = exc.StringValue == null;
                        foreach (var (c, form) in ExpandFormVariants(exc.FormVariants, parsedDimensions,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                        {
                            if (captureFirst) { fallbackExceptions.TryAdd(exc.Value, form); captureFirst = false; }
                            var entry = GetOrAddSynthetic(ConstraintKey(c), c);
                            entry.e[exc.Value] = form;
                        }
                    }
                }

                foreach (var rule in ordinals?.Rules ?? [])
                {
                    if (rule.FormVariants?.Count > 0)
                    {
                        bool captureFirst = rule.To == null;
                        foreach (var (c, form) in ExpandFormVariants(rule.FormVariants, parsedDimensions,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                        {
                            if (captureFirst) { fallbackWordRules.TryAdd(rule.From, form); captureFirst = false; }
                            var entry = GetOrAddSynthetic(ConstraintKey(c), c);
                            entry.w[rule.From] = form;
                        }
                    }
                }

                // Merge synthetic exceptions/wordRules into any container rule sharing the same
                // constraint key. Without this, a container rule (suffix=sten, exceptions={}) and
                // a synthetic rule (exceptions={1:"ersten",...}, suffix=null) both have specificity
                // 3 and FindBestOrdinalVariant picks whichever appears first, losing the other's data.
                var containerKeyToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < result.Count; i++)
                    containerKeyToIndex[ConstraintKey(result[i].Constraints)] = i;

                foreach (var (constraints, exceptions, wordRules) in syntheticByKey.Values)
                {
                    var key = ConstraintKey(constraints);
                    if (containerKeyToIndex.TryGetValue(key, out var idx))
                    {
                        var existing = result[idx];
                        var mergedExc = new Dictionary<long, string>(existing.Exceptions);
                        foreach (var kv in exceptions) mergedExc[kv.Key] = kv.Value;
                        var mergedWr = new Dictionary<string, string>(existing.WordRules);
                        foreach (var kv in wordRules) mergedWr[kv.Key] = kv.Value;
                        result[idx] = new NumberToStringConverter.OrdinalVariantRule(
                            existing.Constraints, mergedExc, mergedWr,
                            existing.Suffix, existing.RemoveTrailing);
                    }
                    else
                    {
                        result.Add(new NumberToStringConverter.OrdinalVariantRule(constraints, exceptions, wordRules, null, null));
                    }
                }

                if (fallbackExceptions.Count > 0 || fallbackWordRules.Count > 0)
                    result.Add(new NumberToStringConverter.OrdinalVariantRule(
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                        fallbackExceptions, fallbackWordRules, null, null));

                return result;
            }

            void CollectOrdinalVariants(
                OrdinalVariantElementType variant,
                Dictionary<string, string> inheritedConstraints,
                List<NumberToStringConverter.OrdinalVariantRule> result)
            {
                string? dimType = string.IsNullOrEmpty(variant.DimensionType)
                    ? null : NormalizeDimName(variant.DimensionType);

                if (dimType != null && string.IsNullOrEmpty(variant.VariantValue) && string.IsNullOrEmpty(variant.VariantValues))
                    throw new InvalidOperationException(
                        $"OrdinalVariant with type=\"{variant.DimensionType}\" must declare either a \"variant\" or a \"values\" attribute.");

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

            // Parses "group(0,1,-1)" → (Group, [0,1,-1]), "end" → (End, null), etc.
            static (NumberToStringConverter.TriggerAt At, int[]? Indices) ParseExecuteAt(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return (NumberToStringConverter.TriggerAt.End, null);
                raw = raw.Trim();
                int parenIdx = raw.IndexOf('(');
                string core = parenIdx >= 0 ? raw[..parenIdx].Trim() : raw;
                string? indexPart = parenIdx >= 0 ? raw[(parenIdx + 1)..].TrimEnd(')').Trim() : null;

                var at = core.ToLowerInvariant() switch
                {
                    "group"          => NumberToStringConverter.TriggerAt.Group,
                    "groupwithscale" => NumberToStringConverter.TriggerAt.GroupWithScale,
                    _                => NumberToStringConverter.TriggerAt.End,
                };

                int[]? indices = null;
                if (indexPart != null)
                {
                    indices = indexPart.Split(',')
                        .Select(s => s.Trim()).Where(s => s.Length > 0)
                        .Select(s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture))
                        .ToArray();
                    if (indices.Length == 0) indices = null;
                }
                return (at, indices);
            }

            IReadOnlyList<NumberToStringConverter.TriggerRule> ParseTriggers(List<TriggerType>? triggers)
            {
                if (triggers == null || triggers.Count == 0) return [];

                var result = new List<NumberToStringConverter.TriggerRule>();
                foreach (var trigger in triggers)
                {
                    var (at, indices) = ParseExecuteAt(trigger.ExecuteAt);
                    var replaces = new List<NumberToStringConverter.TriggerReplace>();

                    foreach (var replace in trigger.Replaces ?? [])
                    {
                        string? defaultTo = replace.To;
                        var forms = new List<(IReadOnlyDictionary<string, string>, string)>();

                        if (replace.FormVariants?.Count > 0)
                        {
                            bool captureFirst = defaultTo == null;
                            foreach (var (constraints, form) in ExpandFormVariants(
                                replace.FormVariants, parsedDimensions,
                                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                            {
                                if (captureFirst) { defaultTo = form; captureFirst = false; }
                                forms.Add((constraints, form));
                            }
                        }

                        replaces.Add(new NumberToStringConverter.TriggerReplace(
                            replace.From, replace.IsRegex, forms, defaultTo));
                    }

                    result.Add(new NumberToStringConverter.TriggerRule(at, indices, replaces));
                }
                return result;
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
                Triggers = ParseTriggers(language.Triggers),
                YearFormat = language.YearFormat == null ? null : new YearFormatOptions(
                    language.YearFormat.HundredWord,
                    language.YearFormat.ZeroConnector,
                    language.YearFormat.SplitRanges.Count == 0 ? null
                        : new IntRange<int>(string.Join(",", language.YearFormat.SplitRanges.Select(r => $"{r.From}-{r.To}"))),
                    language.YearFormat.BeforeChristSuffix),
                Multiplicatives = language.Multiplicatives?.Entries
                    ?.ToDictionary(e => e.Value, e => e.String),
                MultiplicativeSuffix = language.Multiplicatives?.Suffix,
                GroupConnector = language.GroupConnector,
                GroupConnectorThreshold = language.GroupConnectorThreshold,
                IntraGroupConnector = language.IntraGroupConnector,
                IntraGroupConnectorThreshold = language.IntraGroupConnectorThreshold,
                ScaleConnector = language.ScaleConnector,
                ScaleConnectorThreshold = language.ScaleConnectorThreshold,
                TimeUnits = language.TimeUnits?.Units?
                    .ToDictionary(u => u.Name, u => (u.Singular, u.Plural, u.Count1Form)),
                DatePattern = language.DateFormat?.Pattern,
                DateFirstDay = language.DateFormat?.FirstDay,
                DateTimeConnector = language.DateFormat?.DateTimeConnector,
            };

            var converter = new NumberToStringConverter(options);
            ValidateVariantReferences(converter, languageIdentifier);
            return converter;
        }

        /// <summary>
        /// Validates that all variant dimension references in VariantRules, OrdinalVariants, and
        /// TriggerReplace.Forms constraints are declared dimensions for the converter, and that
        /// the constraint values used are among the values declared for that dimension.
        /// Throws <see cref="InvalidOperationException"/> when an unknown dimension key or an
        /// undeclared value is found.
        /// </summary>
        private static void ValidateVariantReferences(NumberToStringConverter converter, string configSource)
        {
            var dimensionsByKey = converter.VariantDimensions
                .SelectMany(d => new[] { d.Name }.Concat(d.LocalName != null ? [d.LocalName] : [])
                    .Select(key => (key, dimension: d)))
                .ToDictionary(t => t.key, t => t.dimension, StringComparer.OrdinalIgnoreCase);

            string Declared() =>
                string.Join(", ", converter.VariantDimensions.Select(d => d.Name));

            void ValidateKeyValue(string kind, string key, string value)
            {
                if (!dimensionsByKey.TryGetValue(key, out var dimension))
                    throw new InvalidOperationException(
                        $"[{configSource}] {kind} references unknown dimension '{key}'. " +
                        $"Declared: [{Declared()}].");

                if (!dimension.Values.Contains(value, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"[{configSource}] {kind} references unknown value '{value}' for dimension '{key}'. " +
                        $"Declared values: [{string.Join(", ", dimension.Values)}].");
            }

            foreach (var rule in converter.VariantRules)
                foreach (var (key, value) in rule.Constraints)
                    ValidateKeyValue("Variant rule", key, value);

            foreach (var rule in converter.OrdinalVariants)
                foreach (var (key, value) in rule.Constraints)
                    ValidateKeyValue("OrdinalVariant rule", key, value);

            foreach (var trigger in converter.Triggers)
            {
                foreach (var replace in trigger.Replaces)
                {
                    foreach (var (constraints, _) in replace.Forms)
                    {
                        foreach (var (key, value) in constraints)
                            ValidateKeyValue("TriggerReplace", key, value);
                    }
                }
            }
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

            if (specificsType == null)
                throw new InvalidOperationException(
                    $"LanguageSpecifics type '{typeName}' could not be found in any loaded assembly. " +
                    $"Call RegisterLanguageSpecifics(\"{typeName}\", instance) before loading the configuration.");

            if (!typeof(INumberToStringLanguageSpecifics).IsAssignableFrom(specificsType)
                || specificsType.IsAbstract
                || specificsType.IsInterface)
                throw new InvalidOperationException(
                    $"LanguageSpecifics type '{typeName}' does not implement INumberToStringLanguageSpecifics " +
                    $"or is abstract/interface.");

            return Activator.CreateInstance(specificsType) as INumberToStringLanguageSpecifics
                   ?? throw new InvalidOperationException(
                       $"LanguageSpecifics type '{typeName}' could not be instantiated.");
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
