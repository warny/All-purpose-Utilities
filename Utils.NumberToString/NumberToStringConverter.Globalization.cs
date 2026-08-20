using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Utils.Range;

namespace Utils.NumberToString
{
    public partial class NumberToStringConverter
    {
        /// <summary>Controls whether an XML configuration is validated against the embedded schema.</summary>
        internal enum ConfigurationSchemaValidation
        {
            /// <summary>Skips schema validation while retaining secure XML parsing and semantic validation.</summary>
            Skip,

            /// <summary>Validates the document against the embedded configuration schema.</summary>
            Validate
        }

        /// <summary>
        /// Gets the ordered configuration documents distributed with the package. Scale bases must
        /// precede languages that inherit from them.
        /// </summary>
        internal static IReadOnlyList<string> BuiltInConfigurations { get; } =
        [
            NumberConverterResources.NumberConvertionConfiguration_SCALE,
            NumberConverterResources.NumberConvertionConfiguration_FR_fr_ca,
            NumberConverterResources.NumberConvertionConfiguration_FR_be_ch,
            NumberConverterResources.NumberConvertionConfiguration_DE,
            NumberConverterResources.NumberConvertionConfiguration_DE_ch,
            NumberConverterResources.NumberConvertionConfiguration_DA,
            NumberConverterResources.NumberConvertionConfiguration_EN,
            NumberConverterResources.NumberConvertionConfiguration_EN_GB,
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
        ];

        private static readonly XmlSerializer ConfigurationSerializer =
            new(typeof(NumbersXmlModel), "Utils/NumberConvertionConfiguration.xsd");

        private static readonly Lazy<XmlSchemaSet> ConfigurationSchemas = new(CreateConfigurationSchemas);

        static NumberToStringConverter()
        {
            RegisterConfigurations(BuiltInConfigurations, DuplicateCulturePolicy.Reject, ConfigurationSchemaValidation.Skip);
        }

        // Caches configurations for different cultures — ConcurrentDictionary for thread-safety
        private static readonly ConcurrentDictionary<string, NumberToStringConverter> CachedConfigurations = new(StringComparer.InvariantCultureIgnoreCase);

        // Explicitly registered language-specifics instances, consulted before reflection
        private static readonly ConcurrentDictionary<string, Func<INumberToStringLanguageSpecifics>> _registeredSpecifics = new(StringComparer.Ordinal);

        private static readonly object ConfigurationLock = new();

        // Stores resolved language definitions for cross-document baseOn resolution.
        // A LanguageDefinition (not the public LanguageType) is cached so that a child in a later
        // document still sees which fields the base declared explicitly versus inherited.
        private static readonly ConcurrentDictionary<string, LanguageDefinition> _cachedLanguageTypes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers an <see cref="INumberToStringLanguageSpecifics"/> instance under a given type name
        /// so that XML configurations referencing that name find it without reflection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If an instance is already registered under <paramref name="typeName"/>, it is
        /// replaced silently. The registered instance is shared across all converters that
        /// reference the type name and is invoked concurrently during conversion.
        /// Implementations must therefore be stateless or internally thread-safe.
        /// </para>
        /// </remarks>
        /// <param name="typeName">
        /// The type name as it appears in <c>&lt;LanguageSpecifics&gt;</c> elements (full or short name).
        /// Must be non-null and non-whitespace.
        /// </param>
        /// <param name="instance">The instance to register. Must not be <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="typeName"/> is null, empty, or whitespace.</exception>
        public static void RegisterLanguageSpecifics(string typeName, INumberToStringLanguageSpecifics instance)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(typeName, nameof(typeName));
            ArgumentNullException.ThrowIfNull(instance);
            _registeredSpecifics[typeName] = () => instance;
        }

        /// <summary>Registers a factory that creates a language-specific implementation for each converter.</summary>
        /// <param name="typeName">The configured type name.</param>
        /// <param name="factory">The non-null factory.</param>
        public static void RegisterLanguageSpecifics(
            string typeName,
            Func<INumberToStringLanguageSpecifics> factory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(typeName, nameof(typeName));
            ArgumentNullException.ThrowIfNull(factory);
            _registeredSpecifics[typeName] = factory;
        }

        /// <summary>
        /// Loads number-to-string configurations embedded as XML strings.
        /// Duplicate normalized culture keys are rejected.
        /// </summary>
        /// <param name="configs">The XML documents describing language configurations.</param>
        public static void InitializeConfigurations(params string[] configs)
            => RegisterConfigurations((IEnumerable<string>)configs);

        /// <summary>
        /// Registers the provided language configurations for later lookup.
        /// Duplicate normalized culture keys are rejected by default.
        /// </summary>
        /// <param name="configs">The XML configuration documents to load.</param>
        public static void RegisterConfigurations(IEnumerable<string> configs)
            => RegisterConfigurations(configs, DuplicateCulturePolicy.Reject);

        /// <summary>Atomically registers configuration documents using an explicit collision policy.</summary>
        /// <param name="configs">The XML documents to register.</param>
        /// <param name="duplicateCulturePolicy">The collision policy.</param>
        public static void RegisterConfigurations(IEnumerable<string> configs, DuplicateCulturePolicy duplicateCulturePolicy)
            => RegisterConfigurations(configs, duplicateCulturePolicy, ConfigurationSchemaValidation.Validate);

        /// <summary>Atomically registers configuration documents using the specified schema policy.</summary>
        private static void RegisterConfigurations(IEnumerable<string> configs, DuplicateCulturePolicy duplicateCulturePolicy, ConfigurationSchemaValidation schemaValidation)
        {
            ArgumentNullException.ThrowIfNull(configs);
            if (!Enum.IsDefined(duplicateCulturePolicy))
                throw new ArgumentOutOfRangeException(
                    nameof(duplicateCulturePolicy),
                    duplicateCulturePolicy,
                    "Unsupported duplicate culture policy.");
            lock (ConfigurationLock)
            {
                var converters = new Dictionary<string, NumberToStringConverter>(StringComparer.OrdinalIgnoreCase);
                var definitions = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (var configuration in configs)
                {
                    var batch = BuildConfiguration(configuration, commitDefinitions: false, definitions, schemaValidation);
                    foreach (var language in batch.Converters)
                    {
                        if (converters.TryAdd(language.Key, language.Value)) continue;
                        if (duplicateCulturePolicy == DuplicateCulturePolicy.Reject)
                            throw new InvalidOperationException($"Duplicate normalized culture '{language.Key}' in configuration batch.");
                        if (duplicateCulturePolicy == DuplicateCulturePolicy.Replace)
                            converters[language.Key] = language.Value;
                    }
                    foreach (var definition in batch.Definitions)
                    {
                        if (definitions.TryAdd(definition.Key, definition.Value)) continue;
                        if (duplicateCulturePolicy == DuplicateCulturePolicy.Replace)
                            definitions[definition.Key] = definition.Value;
                    }
                }

                var collisions = converters.Keys.Where(CachedConfigurations.ContainsKey).ToArray();
                if (duplicateCulturePolicy == DuplicateCulturePolicy.Reject && collisions.Length > 0)
                    throw new InvalidOperationException($"Cultures already registered: {string.Join(", ", collisions)}.");
                foreach (var (culture, converter) in converters)
                {
                    if (duplicateCulturePolicy == DuplicateCulturePolicy.KeepExisting && CachedConfigurations.ContainsKey(culture))
                        continue;
                    CachedConfigurations[culture] = converter;
                    _cachedLanguageTypes[culture] = definitions[culture];
                }
            }
        }

        /// <summary>Trims whitespace from a culture identifier at registration/lookup boundaries.</summary>
        private static string NormalizeCulture(string culture) => culture.Trim();

        /// <summary>
        /// Parses a configuration document into converter instances keyed by culture name.
        /// Proceeds in three phases to guarantee atomicity:
        /// <list type="number">
        ///   <item><description>Resolve all <c>baseOn</c> inheritance chains (throws on cycle or missing base — nothing is committed).</description></item>
        ///   <item><description>Build converters from the resolved definitions (throws on invalid config — nothing is committed).</description></item>
        ///   <item><description>Commit all resolved types to <see cref="_cachedLanguageTypes"/> for cross-document inheritance.</description></item>
        /// </list>
        /// If any phase throws, no partial state is published to the shared caches.
        /// </summary>
        /// <param name="configuration">The XML configuration document.</param>
        /// <returns>A dictionary mapping culture names to converters.</returns>
        public static Dictionary<string, NumberToStringConverter> ReadConfiguration(string configuration)
        {
            var batch = BuildConfiguration(configuration, commitDefinitions: true, schemaValidation: ConfigurationSchemaValidation.Validate);
            return batch.Converters;
        }

        /// <summary>Builds a configuration with an explicit schema policy for unit verification.</summary>
        /// <param name="configuration">The XML configuration document.</param>
        /// <param name="schemaValidation">The schema-validation policy.</param>
        internal static void BuildConfigurationForTesting(string configuration, ConfigurationSchemaValidation schemaValidation)
            => BuildConfiguration(configuration, commitDefinitions: false, schemaValidation: schemaValidation);

        /// <summary>Validates and deserializes one document without running cross-document semantic resolution.</summary>
        /// <param name="configuration">The XML configuration document.</param>
        internal static void ValidateConfigurationSchemaForTesting(string configuration)
            => DeserializeConfiguration(configuration, ConfigurationSchemaValidation.Validate);

        /// <summary>Deserializes a configuration through a secure, optionally schema-validating XML reader.</summary>
        private static NumbersXmlModel DeserializeConfiguration(string configuration, ConfigurationSchemaValidation schemaValidation)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            if (schemaValidation == ConfigurationSchemaValidation.Validate)
                EnsureUniquePostProcessingSections(configuration);
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            if (schemaValidation == ConfigurationSchemaValidation.Validate)
            {
                settings.ValidationType = ValidationType.Schema;
                settings.Schemas = ConfigurationSchemas.Value;
            }

            using StringReader textReader = new(configuration);
            using XmlReader xmlReader = XmlReader.Create(textReader, settings);
            try
            {
                return (NumbersXmlModel?)ConfigurationSerializer.Deserialize(xmlReader)
                    ?? throw new InvalidOperationException("The configuration document did not produce a model.");
            }
            catch (InvalidOperationException exception) when (exception.InnerException is XmlSchemaValidationException schemaException)
            {
                ExceptionDispatchInfo.Capture(schemaException).Throw();
                throw;
            }
        }

        /// <summary>
        /// Rejects repeated order-independent language sections that XSD 1.0 cannot constrain
        /// individually inside a repeating <c>xs:choice</c>.
        /// </summary>
        /// <param name="configuration">The XML configuration document.</param>
        private static void EnsureUniquePostProcessingSections(string configuration)
        {
            HashSet<string> orderIndependentSections =
            [
                "Replacements", "Exceptions", "LanguageSpecifics", "Fractions", "Ordinals",
                "Variants", "YearFormat", "Multiplicatives", "TimeUnits", "DateFormat"
            ];
            HashSet<string>? seenSections = null;
            using StringReader textReader = new(configuration);
            using XmlReader reader = XmlReader.Create(textReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;
                if (reader.Depth == 1 && reader.LocalName == "Language")
                {
                    seenSections = new HashSet<string>(StringComparer.Ordinal);
                    continue;
                }
                if (reader.Depth != 2 || seenSections is null || !orderIndependentSections.Contains(reader.LocalName))
                    continue;
                if (seenSections.Add(reader.LocalName))
                    continue;

                IXmlLineInfo lineInfo = (IXmlLineInfo)reader;
                throw new XmlSchemaValidationException(
                    $"The language section '{reader.LocalName}' may occur only once.",
                    null,
                    lineInfo.LineNumber,
                    lineInfo.LinePosition);
            }
        }

        /// <summary>Loads and compiles the embedded number-conversion schema.</summary>
        private static XmlSchemaSet CreateConfigurationSchemas()
        {
            const string resourceName = "Utils.NumberToString.NumberConvertionConfiguration.xsd";
            using Stream schemaStream = typeof(NumberToStringConverter).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded configuration schema '{resourceName}' was not found.");
            using XmlReader schemaReader = XmlReader.Create(schemaStream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            XmlSchemaSet schemas = new() { XmlResolver = null };
            schemas.Add("Utils/NumberConvertionConfiguration.xsd", schemaReader);
            schemas.Compile();
            return schemas;
        }

        /// <summary>Builds one configuration document without publishing converters.</summary>
        private static ConfigurationBatch BuildConfiguration(
            string configuration,
            bool commitDefinitions,
            IReadOnlyDictionary<string, LanguageDefinition>? inheritedBatchDefinitions = null,
            ConfigurationSchemaValidation schemaValidation = ConfigurationSchemaValidation.Validate)
        {
            NumbersXmlModel obj = DeserializeConfiguration(configuration, schemaValidation);

            var languageModels = obj.Languages ?? new List<LanguageXmlModel>();

            // Project each XML model onto an internal definition that carries explicit-presence
            // information (Optional<T>) for the value-type attributes.
            var definitions = new List<LanguageDefinition>(languageModels.Count);
            foreach (var model in languageModels)
                definitions.Add(ToDefinition(model));

            // Build a within-document lookup so baseOn can reference any language in this
            // document regardless of declaration order (case-insensitive culture keys).
            var docLanguages = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in definitions)
                foreach (var culture in def.Cultures)
                    docLanguages.TryAdd(NormalizeCulture(culture), def);

            // Phase 1 — resolve all languages. Keep resolved definitions in local dictionaries so
            // nothing is committed to the shared cache until every language in this document
            // has been resolved successfully (atomicity).
            var resolvedDefinitions = new List<LanguageDefinition>(definitions.Count);
            var localCacheAdditions = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);
            if (inheritedBatchDefinitions != null)
                foreach (var entry in inheritedBatchDefinitions)
                    localCacheAdditions.Add(entry.Key, entry.Value);
            var currentDefinitions = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in definitions)
            {
                LanguageDefinition resolved;
                if (string.IsNullOrEmpty(definition.BaseOn))
                {
                    resolved = definition;
                }
                else
                {
                    // Pass both the document-local map and the accumulating local cache so that
                    // languages resolved earlier in this document are available as bases.
                    resolved = ResolveLanguage(
                        definition,
                        docLanguages,
                        localCacheAdditions,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        new List<string>());
                }

                resolvedDefinitions.Add(resolved);

                // Stage resolved definition into the local cache so later languages in this document
                // can inherit from it via baseOn without hitting the global cache yet.
                foreach (var culture in resolved.Cultures)
                {
                    string normalizedCulture = NormalizeCulture(culture);
                    localCacheAdditions.TryAdd(normalizedCulture, resolved);
                    if (!currentDefinitions.TryAdd(normalizedCulture, resolved))
                        throw new InvalidOperationException($"Duplicate normalized culture '{normalizedCulture}' in configuration document.");
                }
            }

            // Phase 2 — all resolutions succeeded; build the public language types and converters
            // (may throw if the language configuration is invalid, e.g. missing scale, bad variant
            // reference). Converters are constructed before committing to _cachedLanguageTypes: if
            // ReadConverter throws, no partial state is published to the global cache.
            var result = new Dictionary<string, NumberToStringConverter>();
            foreach (var resolved in resolvedDefinitions)
            {
                var language = BuildResolvedLanguage(resolved);
                foreach (var culture in resolved.Cultures)
                {
                    var key = NormalizeCulture(culture);
                    ValidateResolvedLanguage(language, key);
                    if (!result.TryAdd(key, ReadConverter(language, key)))
                        throw new InvalidOperationException($"Duplicate normalized culture '{key}' in configuration document.");
                }
            }

            // Phase 3 — all converters built successfully; commit resolved definitions to the global cache.
            if (commitDefinitions)
                lock (ConfigurationLock)
                    foreach (var resolved in resolvedDefinitions)
                        foreach (var culture in resolved.Cultures)
                            _cachedLanguageTypes.TryAdd(NormalizeCulture(culture), resolved);

            return new ConfigurationBatch(result, currentDefinitions);
        }

        private sealed record ConfigurationBatch(
            Dictionary<string, NumberToStringConverter> Converters,
            Dictionary<string, LanguageDefinition> Definitions);

        /// <summary>
        /// Splits a <c>baseOn</c> attribute value into individual culture keys.
        /// Commas are the separator; each token is trimmed.
        /// </summary>
        private static IReadOnlyList<string> ParseBaseOnKeys(string baseOn)
        {
            var keys = new List<string>();
            foreach (var part in baseOn.Split(','))
            {
                var key = NormalizeCulture(part);
                if (key.Length > 0)
                    keys.Add(key);
            }
            return keys;
        }

        /// <summary>
        /// Fully resolves a language by recursively resolving all its <c>baseOn</c> bases,
        /// then merging them in order (earlier bases have lower priority) and finally
        /// overlaying the child's own settings on top.
        /// </summary>
        /// <param name="child">The language to resolve.</param>
        /// <param name="docLanguages">All raw (unresolved) languages in the current document.</param>
        /// <param name="localCache">Resolved languages accumulated so far within this document.</param>
        /// <param name="visiting">Culture keys currently on the resolution stack (cycle detection).</param>
        /// <param name="resolutionPath">Ordered list of keys on the stack (for error messages).</param>
        /// <returns>A fully resolved <see cref="LanguageType"/> with all inherited settings merged in.</returns>
        private static LanguageDefinition ResolveLanguage(
            LanguageDefinition child,
            IReadOnlyDictionary<string, LanguageDefinition> docLanguages,
            IReadOnlyDictionary<string, LanguageDefinition> localCache,
            HashSet<string> visiting,
            List<string> resolutionPath)
        {
            // Use the first culture name as the canonical key for this node.
            string childKey = child.Cultures.Count > 0
                ? NormalizeCulture(child.Cultures[0])
                : string.Empty;

            if (!visiting.Add(childKey))
            {
                string path = string.Join(" → ", resolutionPath) + " → " + childKey;
                throw new InvalidOperationException(
                    $"Language configuration error: baseOn cycle detected: {path}.");
            }
            resolutionPath.Add(childKey);

            try
            {
                // Build the accumulated inherited definition by merging bases left-to-right
                // (later base has higher priority than earlier base; child is highest).
                LanguageDefinition accumulated = CreateEmptyLanguageDefinition();

                if (!string.IsNullOrEmpty(child.BaseOn))
                {
                    var baseKeys = ParseBaseOnKeys(child.BaseOn);
                    foreach (var baseKey in baseKeys)
                    {
                        LanguageDefinition resolvedBase = FindAndResolveBase(
                            baseKey, docLanguages, localCache, visiting, resolutionPath);
                        accumulated = MergeLanguageDefinition(inherited: accumulated, overriding: resolvedBase);
                    }
                }

                return MergeLanguageDefinition(inherited: accumulated, overriding: child);
            }
            finally
            {
                visiting.Remove(childKey);
                resolutionPath.RemoveAt(resolutionPath.Count - 1);
            }
        }

        /// <summary>
        /// Locates a base language by its culture key and returns it fully resolved.
        /// <para>
        /// Lookup order (document-local definitions always take priority over the global cache):
        /// <list type="number">
        ///   <item><description><paramref name="docLanguages"/> — declared raw in this document; resolved recursively with full cycle detection.</description></item>
        ///   <item><description><paramref name="localCache"/> — already resolved in an earlier document of the current batch.</description></item>
        ///   <item><description><see cref="_cachedLanguageTypes"/> — resolved in a previously loaded document (cross-document inheritance).</description></item>
        /// </list>
        /// A locally declared language always shadows a same-name entry in the global cache, which
        /// prevents a cached definition from masking a cycle that exists in the current document.
        /// </para>
        /// </summary>
        private static LanguageDefinition FindAndResolveBase(
            string baseKey,
            IReadOnlyDictionary<string, LanguageDefinition> docLanguages,
            IReadOnlyDictionary<string, LanguageDefinition> localCache,
            HashSet<string> visiting,
            List<string> resolutionPath)
        {
            // 1. Declared raw in this document — resolve recursively.
            //    Document-local definitions take priority over the global cache so that a local
            //    definition of a same-named culture is used (and cycles are always detected even
            //    when an older version of the same culture exists in _cachedLanguageTypes).
            if (docLanguages.TryGetValue(baseKey, out LanguageDefinition? rawBase))
                return ResolveLanguage(rawBase, docLanguages, localCache, visiting, resolutionPath);

            // 2. Resolved in an earlier document of the current batch.
            if (localCache.TryGetValue(baseKey, out LanguageDefinition? cachedInBatch))
                return cachedInBatch;

            // 3. Resolved in a previously loaded document (cross-document inheritance).
            if (_cachedLanguageTypes.TryGetValue(baseKey, out LanguageDefinition? globalBase))
                return globalBase;

            throw new InvalidOperationException(
                $"Language configuration error: baseOn culture \"{baseKey}\" was not found. " +
                $"Ensure the base language is declared in the same document or loaded before this one.");
        }

        /// <summary>
        /// Projects an XML model onto an internal <see cref="LanguageDefinition"/>, translating the
        /// presence-sensitive <c>groupSize</c> attribute into an <see cref="Optional{T}"/> and the
        /// nested number scale into a <see cref="NumberScaleDefinition"/>.
        /// </summary>
        private static LanguageDefinition ToDefinition(LanguageXmlModel model) => new()
        {
            Cultures = model.Cultures is { Count: > 0 }
                ? model.Cultures
                : (IReadOnlyList<string>)[],
            BaseOn = model.BaseOn,
            GroupSize = model.GroupSizeSpecified ? Optional<int>.Of(model.GroupSize) : Optional<int>.Unspecified,
            Separator = model.Separator,
            GroupSeparator = model.GroupSeparator,
            Zero = model.Zero,
            Minus = model.Minus,
            DecimalSeparator = model.DecimalSeparator,
            FractionSeparator = model.FractionSeparator,
            MaxNumber = model.MaxNumber,
            Groups = model.Groups,
            Exceptions = model.Exceptions,
            NumberScale = ToNumberScaleDefinition(model.NumberScale),
            Replacements = model.Replacements,
            LanguageSpecificsTypeName = model.LanguageSpecificsTypeName,
            Fractions = model.Fractions,
            Ordinals = model.Ordinals,
            Variants = model.Variants,
            YearFormat = model.YearFormat,
            Triggers = model.Triggers,
            Multiplicatives = model.Multiplicatives,
            GroupConnector = model.GroupConnector,
            GroupConnectorThresholdString = model.GroupConnectorThresholdString,
            IntraGroupConnector = model.IntraGroupConnector,
            IntraGroupConnectorThresholdString = model.IntraGroupConnectorThresholdString,
            ScaleConnector = model.ScaleConnector,
            ScaleConnectorThresholdString = model.ScaleConnectorThresholdString,
            TimeUnits = model.TimeUnits,
            DateFormat = model.DateFormat,
        };

        /// <summary>
        /// Projects a number-scale XML model onto an internal <see cref="NumberScaleDefinition"/>,
        /// translating the presence-sensitive <c>firstLetterUpperCase</c> and <c>startIndex</c>
        /// attributes into <see cref="Optional{T}"/> values.
        /// </summary>
        private static NumberScaleDefinition? ToNumberScaleDefinition(NumberScaleXmlModel? model)
        {
            if (model == null) return null;
            return new NumberScaleDefinition
            {
                FirstLetterUpperCase = model.FirstLetterUpperCaseSpecified
                    ? Optional<bool>.Of(model.FirstLetterUpperCase)
                    : Optional<bool>.Unspecified,
                VoidGroup = model.VoidGroup,
                GroupSeparator = model.GroupSeparator,
                StartIndex = model.StartIndexSpecified
                    ? Optional<int>.Of(model.StartIndex)
                    : Optional<int>.Unspecified,
                StaticNames = model.StaticNames,
                Scale0Prefixes = model.Scale0Prefixes,
                UnitsPrefixes = model.UnitsPrefixes,
                TensPrefixes = model.TensPrefixes,
                HundredsPrefixes = model.HundredsPrefixes,
                Suffixes = model.Suffixes,
            };
        }

        /// <summary>
        /// Returns an <see cref="Optional{T}"/> that prefers an explicitly specified
        /// <paramref name="overriding"/> value and otherwise falls back to <paramref name="inherited"/>.
        /// An explicit <c>false</c>/<c>0</c> overrides an inherited value; only an unspecified
        /// override inherits.
        /// </summary>
        private static Optional<T> MergeOptional<T>(Optional<T> inherited, Optional<T> overriding) =>
            overriding.IsSpecified ? overriding : inherited;

        /// <summary>
        /// Returns a blank <see cref="LanguageDefinition"/> used as the accumulator seed when
        /// merging multiple inherited bases. Every field is unspecified/null so that the
        /// first real base's values are picked up unchanged.
        /// </summary>
        private static LanguageDefinition CreateEmptyLanguageDefinition() => new()
        {
            Cultures = [],
            BaseOn = null,
            GroupSize = Optional<int>.Unspecified,
        };

        /// <summary>
        /// Returns a new <see cref="LanguageDefinition"/> where <paramref name="overriding"/> values
        /// replace corresponding fields of <paramref name="inherited"/>.
        /// Only an absent value inherits from base; an explicitly declared value always overrides,
        /// including <see langword="false"/>, zero, an empty string, or an explicitly empty
        /// collection. For <see cref="OrdinalsType"/>, exceptions and word-rules are merged
        /// element-by-element so a child can extend rather than replace the base's ordinal
        /// configuration.
        /// </summary>
        private static LanguageDefinition MergeLanguageDefinition(LanguageDefinition inherited, LanguageDefinition overriding) =>
            new()
            {
                Cultures = overriding.Cultures.Count > 0 ? overriding.Cultures : inherited.Cultures,
                BaseOn = null,
                GroupSize = MergeOptional(inherited.GroupSize, overriding.GroupSize),
                Separator = overriding.Separator ?? inherited.Separator,
                GroupSeparator = overriding.GroupSeparator ?? inherited.GroupSeparator,
                Zero = overriding.Zero ?? inherited.Zero,
                Minus = overriding.Minus ?? inherited.Minus,
                DecimalSeparator = overriding.DecimalSeparator ?? inherited.DecimalSeparator,
                FractionSeparator = overriding.FractionSeparator ?? inherited.FractionSeparator,
                MaxNumber = overriding.MaxNumber ?? inherited.MaxNumber,
                Groups = overriding.Groups ?? inherited.Groups,
                Exceptions = overriding.Exceptions ?? inherited.Exceptions,
                NumberScale = MergeNumberScaleDefinition(inherited.NumberScale, overriding.NumberScale),
                Replacements = overriding.Replacements ?? inherited.Replacements,
                LanguageSpecificsTypeName = overriding.LanguageSpecificsTypeName != null
                    ? overriding.LanguageSpecificsTypeName : inherited.LanguageSpecificsTypeName,
                Fractions = overriding.Fractions ?? inherited.Fractions,
                Ordinals = MergeOrdinalsType(inherited.Ordinals, overriding.Ordinals),
                Variants = overriding.Variants ?? inherited.Variants,
                YearFormat = overriding.YearFormat ?? inherited.YearFormat,
                Triggers = overriding.Triggers ?? inherited.Triggers,
                Multiplicatives = overriding.Multiplicatives ?? inherited.Multiplicatives,
                GroupConnector = overriding.GroupConnector ?? inherited.GroupConnector,
                GroupConnectorThresholdString = overriding.GroupConnectorThresholdString ?? inherited.GroupConnectorThresholdString,
                IntraGroupConnector = overriding.IntraGroupConnector ?? inherited.IntraGroupConnector,
                IntraGroupConnectorThresholdString = overriding.IntraGroupConnectorThresholdString ?? inherited.IntraGroupConnectorThresholdString,
                ScaleConnector = overriding.ScaleConnector ?? inherited.ScaleConnector,
                ScaleConnectorThresholdString = overriding.ScaleConnectorThresholdString ?? inherited.ScaleConnectorThresholdString,
                TimeUnits = overriding.TimeUnits ?? inherited.TimeUnits,
                DateFormat = overriding.DateFormat ?? inherited.DateFormat,
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
        /// Merges two <see cref="NumberScaleDefinition"/> instances field by field so that a derived
        /// language can override individual sub-sections (e.g., <c>StaticNames</c>, <c>Suffixes</c>)
        /// while inheriting the rest (e.g., prefix tables) from the base language.
        /// Reference fields use <see langword="null"/> as the absent marker; the two value-type
        /// fields use <see cref="Optional{T}"/> so an explicit <c>false</c> for
        /// <c>FirstLetterUpperCase</c> and <c>0</c> for <c>StartIndex</c> override the inherited value.
        /// </summary>
        private static NumberScaleDefinition? MergeNumberScaleDefinition(NumberScaleDefinition? inherited, NumberScaleDefinition? overriding)
        {
            if (overriding == null) return inherited;
            if (inherited == null) return overriding;
            return new NumberScaleDefinition
            {
                FirstLetterUpperCase = MergeOptional(inherited.FirstLetterUpperCase, overriding.FirstLetterUpperCase),
                VoidGroup = overriding.VoidGroup ?? inherited.VoidGroup,
                GroupSeparator = overriding.GroupSeparator ?? inherited.GroupSeparator,
                StartIndex = MergeOptional(inherited.StartIndex, overriding.StartIndex),
                StaticNames = overriding.StaticNames ?? inherited.StaticNames,
                Scale0Prefixes = overriding.Scale0Prefixes ?? inherited.Scale0Prefixes,
                UnitsPrefixes = overriding.UnitsPrefixes ?? inherited.UnitsPrefixes,
                TensPrefixes = overriding.TensPrefixes ?? inherited.TensPrefixes,
                HundredsPrefixes = overriding.HundredsPrefixes ?? inherited.HundredsPrefixes,
                Suffixes = overriding.Suffixes ?? inherited.Suffixes,
            };
        }

        /// <summary>
        /// Builds the public <see cref="LanguageType"/> from a fully resolved
        /// <see cref="LanguageDefinition"/>. Absent value-type fields collapse to their historical
        /// defaults (<c>GroupSize</c> = 3, <c>StartIndex</c> = 0, <c>FirstLetterUpperCase</c> = false)
        /// so the public model stays free of nullable/technical members.
        /// </summary>
        private static LanguageType BuildResolvedLanguage(LanguageDefinition definition) => new()
        {
            Cultures = definition.Cultures.ToList(),
            BaseOn = null,
            GroupSize = definition.GroupSize.GetValueOrDefault(3),
            Separator = definition.Separator,
            GroupSeparator = definition.GroupSeparator,
            Zero = definition.Zero,
            Minus = definition.Minus,
            DecimalSeparator = definition.DecimalSeparator,
            FractionSeparator = definition.FractionSeparator,
            MaxNumber = definition.MaxNumber,
            Groups = definition.Groups,
            Exceptions = definition.Exceptions,
            NumberScale = BuildNumberScale(definition.NumberScale),
            Replacements = definition.Replacements,
            LanguageSpecificsTypeName = definition.LanguageSpecificsTypeName,
            Fractions = definition.Fractions,
            Ordinals = definition.Ordinals,
            Variants = definition.Variants,
            YearFormat = definition.YearFormat,
            Triggers = definition.Triggers,
            Multiplicatives = definition.Multiplicatives,
            GroupConnector = definition.GroupConnector,
            GroupConnectorThresholdString = definition.GroupConnectorThresholdString,
            IntraGroupConnector = definition.IntraGroupConnector,
            IntraGroupConnectorThresholdString = definition.IntraGroupConnectorThresholdString,
            ScaleConnector = definition.ScaleConnector,
            ScaleConnectorThresholdString = definition.ScaleConnectorThresholdString,
            TimeUnits = definition.TimeUnits,
            DateFormat = definition.DateFormat,
        };

        /// <summary>
        /// Builds the public <see cref="NumberScaleType"/> from a resolved
        /// <see cref="NumberScaleDefinition"/>, collapsing the two <see cref="Optional{T}"/> fields
        /// to their historical defaults when absent.
        /// </summary>
        private static NumberScaleType? BuildNumberScale(NumberScaleDefinition? definition)
        {
            if (definition == null) return null;
            return new NumberScaleType
            {
                FirstLetterUpperCase = definition.FirstLetterUpperCase.GetValueOrDefault(false),
                VoidGroup = definition.VoidGroup,
                GroupSeparator = definition.GroupSeparator,
                StartIndex = definition.StartIndex.GetValueOrDefault(0),
                StaticNames = definition.StaticNames,
                Scale0Prefixes = definition.Scale0Prefixes,
                UnitsPrefixes = definition.UnitsPrefixes,
                TensPrefixes = definition.TensPrefixes,
                HundredsPrefixes = definition.HundredsPrefixes,
                Suffixes = definition.Suffixes,
            };
        }

        /// <summary>Validates the fully inherited language model and reports all structural defects together.</summary>
        private static void ValidateResolvedLanguage(LanguageType language, string languageIdentifier)
        {
            var errors = new List<string>();
            void Require(bool condition, string path, string message)
            {
                if (!condition) errors.Add($"[{languageIdentifier}] {path}: {message}");
            }

            Require(language.Cultures?.Any(c => !string.IsNullOrWhiteSpace(c)) == true, "Cultures", "at least one non-empty culture is required.");
            Require(language.GroupSize > 0 && language.GroupSize < _decimalPowersOfTen.Length, "GroupSize", $"must be between 1 and {_decimalPowersOfTen.Length - 1}.");
            Require(!string.IsNullOrWhiteSpace(language.Zero), "Zero", "must be non-empty.");
            Require(!string.IsNullOrWhiteSpace(language.Minus) && language.Minus.Count(c => c == '*') == 1, "Minus", "must be non-empty and contain exactly one '*' body placeholder.");
            Require(language.Groups?.Groups != null && language.Groups.Groups.Count > 0, "Groups", "at least one group is required.");
            if (language.Groups?.Groups != null)
            {
                int expectedGroup = 1;
                foreach (var group in language.Groups.Groups.OrderBy(g => g.Level))
                {
                    int level = group.Level;
                    DigitListType digits = group;
                    Require(level == expectedGroup++, $"Groups[{level}]", "group levels must be contiguous starting at 1.");
                    Require(digits?.Digits != null, $"Groups[{level}].Digits", "digit list is required.");
                    if (digits?.Digits == null) continue;
                    var values = new HashSet<long>();
                    for (int i = 0; i < digits.Digits.Count; i++)
                    {
                        var digit = digits.Digits[i];
                        Require(digit != null, $"Groups[{level}].Digits[{i}]", "entry must not be null.");
                        if (digit == null) continue;
                        Require(values.Add(digit.Digit), $"Groups[{level}].Digits[{i}]", $"digit {digit.Digit} is duplicated.");
                        Require(digit.StringValue != null || digit.BuildString != null, $"Groups[{level}].Digits[{digit.Digit}]", "at least one of string or build must be non-null.");
                    }
                    for (int digit = 0; digit <= 9; digit++)
                        Require(values.Contains(digit), $"Groups[{level}].Digits[{digit}]", "required digit is missing.");
                }
            }

            if (language.TimeUnits?.Units != null)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var unit in language.TimeUnits.Units)
                {
                    Require(unit != null, "TimeUnits", "unit entry must not be null.");
                    if (unit == null) continue;
                    Require(names.Add(unit.Name), $"TimeUnits[{unit.Name}]", "canonical name must be unique.");
                    Require(!string.IsNullOrWhiteSpace(unit.Singular), $"TimeUnits[{unit.Name}].Singular", "must be non-empty.");
                    Require(!string.IsNullOrWhiteSpace(unit.Plural), $"TimeUnits[{unit.Name}].Plural", "must be non-empty.");
                    Require(unit.Count1Form == null || !string.IsNullOrWhiteSpace(unit.Count1Form), $"TimeUnits[{unit.Name}].Count1Form", "must not be blank.");
                }
                foreach (string required in new[] { "hour", "minute", "second" })
                    Require(names.Contains(required), $"TimeUnits[{required}]", "canonical unit is required.");
            }

            if (language.NumberScale == null)
            {
                bool parsed = BigInteger.TryParse(language.MaxNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maximum);
                Require(parsed, "MaxNumber", "is required when NumberScale is absent.");
                if (parsed && language.GroupSize > 0)
                    Require(maximum < BigInteger.Pow(10, language.GroupSize), "MaxNumber", "must be below the first value requiring a scale name.");
                language.NumberScale = new NumberScaleType
                {
                    StaticNames = new StaticNamesType
                    {
                        Scales = [new NumberType { Value = 0, StringValue = string.Empty }],
                    },
                };
            }
            else
            {
                Require(language.NumberScale.StaticNames?.Scales != null, "NumberScale.StaticNames", "static names are required (an empty list is allowed). ");
                if (language.NumberScale.StaticNames?.Scales != null)
                    foreach (var scaleName in language.NumberScale.StaticNames.Scales.Where(s => s.Value > 0))
                        Require(!string.IsNullOrWhiteSpace(scaleName.StringValue), $"NumberScale.StaticNames[{scaleName.Value}]", "scale names above index zero must be non-empty.");
            }

            if (errors.Count > 0)
                throw new InvalidOperationException("Resolved language configuration is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        /// <summary>
        /// Validates that no two variant dimensions share a canonical name or alias (case-insensitive).
        /// </summary>
        private static void ValidateDimensionNames(
            IReadOnlyList<NumberToStringConverter.VariantDimension> dims, string languageIdentifier)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in dims)
            {
                if (!seen.Add(d.Name))
                    throw new InvalidOperationException(
                        $"[{languageIdentifier}] Duplicate variant dimension name '{d.Name}'.");
                if (!string.IsNullOrEmpty(d.LocalName) && !seen.Add(d.LocalName))
                    throw new InvalidOperationException(
                        $"[{languageIdentifier}] Variant dimension alias '{d.LocalName}' for '{d.Name}' " +
                        $"collides with an existing name or alias.");
            }
        }

        /// <summary>
        /// Validates that static scale entries have unique, zero-based, contiguous integer indices.
        /// </summary>
        private static void ValidateStaticScaleIndices(
            IReadOnlyList<NumberType> scales, string languageIdentifier)
        {
            var sorted = scales.OrderBy(s => s.Value).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].Value != i)
                    throw new InvalidOperationException(
                        $"[{languageIdentifier}] Static scale indices must be unique and contiguous starting at 0. " +
                        $"Expected index {i} but found {sorted[i].Value}.");
            }
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

            ValidateStaticScaleIndices(confScale.StaticNames.Scales, languageIdentifier);
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

            BigInteger? configuredMaximum = string.IsNullOrWhiteSpace(language.MaxNumber)
                ? null
                : BigInteger.Parse(language.MaxNumber, CultureInfo.InvariantCulture);
            if (!scale.IsUnbounded)
            {
                if (!configuredMaximum.HasValue)
                    throw new InvalidOperationException($"[{languageIdentifier}] MaxNumber is required for a bounded NumberScale.");
                int maximumGroup = configuredMaximum.Value.IsZero
                    ? 0
                    : (configuredMaximum.Value.ToString(CultureInfo.InvariantCulture).Length - 1) / language.GroupSize;
                for (int groupIndex = 1; groupIndex <= maximumGroup; groupIndex++)
                    if (!scale.CanNameGroup(groupIndex))
                        throw new InvalidOperationException($"[{languageIdentifier}] NumberScale cannot name group {groupIndex} required by MaxNumber {configuredMaximum.Value}.");
            }

            IEnumerable<NumberToStringConverter.ReplacementRule> ParseReplacements(ReplacementsListType list)
            {
                if (list?.Replacements == null) return [];
                var rules = new List<NumberToStringConverter.ReplacementRule>();
                foreach (var r in list.Replacements)
                {
                    if (r.NewValue != null)
                        rules.Add(new NumberToStringConverter.ReplacementRule(r.OldValue, r.NewValue, r.Scope, r.OnScale, r.OnValue));
                    else if (r.FormVariants == null || r.FormVariants.Count == 0)
                        throw new InvalidOperationException(
                            $"[{languageIdentifier}] Replacement for '{r.OldValue}' has neither a newValue " +
                            $"nor child form-variant elements. Either add newValue or provide at least one <Variant>.");
                    // else: no direct newValue but has form variants — handled by ExpandFormVariants in ParseVariantRules
                }
                return rules;
            }

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
            ValidateDimensionNames(parsedDimensions, languageIdentifier);
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
                                            List<NumberToStringConverter.ReplacementRule> Replacements,
                                            int Priority)>(StringComparer.Ordinal);

                foreach (var repl in language.Replacements?.Replacements ?? [])
                {
                    if (repl.FormVariants?.Count > 0)
                    {
                        foreach (var (c, form, priority) in ExpandFormVariants(repl.FormVariants, parsedDimensions,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                        {
                            var key = RankedConstraintKey(c, priority);
                            if (!syntheticByKey.TryGetValue(key, out var entry))
                            {
                                entry = (c, [], priority);
                                syntheticByKey[key] = entry;
                            }
                            entry.Replacements.Add(new NumberToStringConverter.ReplacementRule(repl.OldValue, form, repl.Scope, repl.OnScale,
                                repl.OnValue));
                        }
                    }
                }

                foreach (var (constraints, replacements, priority) in syntheticByKey.Values)
                    result.Add(new NumberToStringConverter.VariantRule(constraints, replacements, priority));

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

                var replacements = new List<NumberToStringConverter.ReplacementRule>();
                foreach (var r in variant.Replacements ?? [])
                {
                    if (r.NewValue != null)
                        replacements.Add(new NumberToStringConverter.ReplacementRule(r.OldValue, r.NewValue, r.Scope, r.OnScale, r.OnValue));
                    else if (r.FormVariants == null || r.FormVariants.Count == 0)
                        throw new InvalidOperationException(
                            $"[{languageIdentifier}] Replacement for '{r.OldValue}' inside a Variant rule has neither " +
                            $"a newValue nor child form-variant elements.");
                    else
                        throw new InvalidOperationException(
                            $"[{languageIdentifier}] Replacement for '{r.OldValue}' inside a Variant rule has form-variant " +
                            $"children, which are not supported at this nesting level. Use a flat Variant element instead.");
                }

                foreach (var dimValue in dimValues)
                {
                    var constraints = new Dictionary<string, string>(inheritedConstraints, StringComparer.OrdinalIgnoreCase);
                    if (dimType != null && dimValue.Length > 0)
                        constraints[dimType] = dimValue;

                    result.Add(new NumberToStringConverter.VariantRule(constraints, replacements, variant.Priority));

                    foreach (var child in variant.NestedVariants ?? [])
                        CollectVariantRules(child, constraints, result);
                }
            }

            // Returns a stable string key for a constraint dictionary, used to merge form-variant
            // rules that share the same (dimension-type, dimension-value) combination.
            static string ConstraintKey(IReadOnlyDictionary<string, string> c) =>
                string.Join("|", c.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                                   .Select(kvp => $"{kvp.Key}={kvp.Value}"));

            // Keeps candidates with identical constraints but distinct explicit priorities separate.
            static string RankedConstraintKey(IReadOnlyDictionary<string, string> constraints, int priority) =>
                $"{ConstraintKey(constraints)}|priority={priority.ToString(CultureInfo.InvariantCulture)}";

            // Walks a FormVariantType tree and yields (constraints, form) pairs.
            // Intermediate nodes (variant attribute present, children present) add one constraint
            // and recurse; leaf nodes (forms attribute present) expand positional entries using the
            // matching Dimension declaration order.
            IEnumerable<(Dictionary<string, string> Constraints, string Form, int Priority)> ExpandFormVariants(
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
                        yield return (constraints, node.Value, node.Priority);
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
                        if (entries.Length != dimValues.Count)
                            throw new InvalidOperationException(
                                $"Dimension '{dimName}' declares {dimValues.Count} value(s) " +
                                $"({string.Join(", ", dimValues)}) but forms=\"{node.Forms}\" " +
                                $"supplies {entries.Length} entry/entries. " +
                                $"The number of comma-separated forms must match the number of declared dimension values.");
                        for (int i = 0; i < dimValues.Count; i++)
                        {
                            var form = entries[i].Trim();
                            if (string.IsNullOrEmpty(form))
                                throw new InvalidOperationException(
                                    $"Dimension '{dimName}' value '{dimValues[i]}' (index {i}) has an empty form " +
                                    $"in forms=\"{node.Forms}\". All entries must be non-empty; " +
                                    $"use a named Variant element for partial mappings.");
                            var leafConstraints = new Dictionary<string, string>(constraints, StringComparer.OrdinalIgnoreCase)
                                { [dimName] = dimValues[i] };
                            yield return (leafConstraints, form, node.Priority);
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
                                            Dictionary<string, string> WordRules,
                                            int Priority)>(StringComparer.Ordinal);

                (Dictionary<string, string> c, Dictionary<long, string> e, Dictionary<string, string> w, int p)
                    GetOrAddSynthetic(string key, Dictionary<string, string> constraints, int priority)
                {
                    if (!syntheticByKey.TryGetValue(key, out var entry))
                    {
                        entry = (constraints, new Dictionary<long, string>(), new Dictionary<string, string>(), priority);
                        syntheticByKey[key] = entry;
                    }
                    return entry;
                }

                // Default variant query: first declared value per dimension.
                // Used to select the no-variant fallback form when an ordinal element supplies
                // only FormVariants and omits the explicit string=/to= attribute.
                // Resolving by declared default rather than by expansion order makes the fallback
                // deterministic regardless of how <Variant> children are sequenced in the XML.
                var defaultVariantQuery = parsedDimensions
                    .Where(d => d.Values.Count > 0)
                    .ToDictionary(d => d.Name, d => d.Values[0], StringComparer.OrdinalIgnoreCase);

                bool MatchesDefaultQuery(IReadOnlyDictionary<string, string> constraints)
                {
                    if (constraints.Count != defaultVariantQuery.Count)
                        return false;
                    return defaultVariantQuery.All(kv =>
                        constraints.TryGetValue(kv.Key, out var actual) &&
                        string.Equals(actual, kv.Value, StringComparison.OrdinalIgnoreCase));
                }

                string SelectDefaultForm(IReadOnlyList<VariantTextCandidate> candidates, string context)
                {
                    VariantTextCandidate? selected = VariantRulePrecedence.SelectBestUnique(
                        candidates,
                        candidate => candidate.Constraints,
                        candidate => candidate.Priority,
                        defaultVariantQuery,
                        context);
                    return selected?.Text ?? throw new InvalidOperationException(
                        $"[{languageIdentifier}] {context} has no form matching the declared default variant values.");
                }

                var fallbackExceptions = new Dictionary<long, string>();
                var fallbackWordRules  = new Dictionary<string, string>();

                foreach (var exc in ordinals?.Exceptions ?? [])
                {
                    if (exc.FormVariants?.Count > 0)
                    {
                        bool needsDefault = exc.StringValue == null;
                        var defaultCandidates = new List<VariantTextCandidate>();

                        foreach (var (c, form, priority) in ExpandFormVariants(exc.FormVariants, parsedDimensions,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                        {
                            if (needsDefault && MatchesDefaultQuery(c))
                                defaultCandidates.Add(new VariantTextCandidate(c, form, priority));
                            var entry = GetOrAddSynthetic(RankedConstraintKey(c, priority), c, priority);
                            entry.e[exc.Value] = form;
                        }

                        if (needsDefault)
                        {
                            string defaultForm = SelectDefaultForm(
                                defaultCandidates, $"OrdinalException[{exc.Value}].DefaultForm");
                            fallbackExceptions.TryAdd(exc.Value, defaultForm);
                        }
                    }
                }

                foreach (var rule in ordinals?.Rules ?? [])
                {
                    if (rule.FormVariants?.Count > 0)
                    {
                        bool needsDefault = rule.To == null;
                        var defaultCandidates = new List<VariantTextCandidate>();

                        foreach (var (c, form, priority) in ExpandFormVariants(rule.FormVariants, parsedDimensions,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                        {
                            if (needsDefault && MatchesDefaultQuery(c))
                                defaultCandidates.Add(new VariantTextCandidate(c, form, priority));
                            var entry = GetOrAddSynthetic(RankedConstraintKey(c, priority), c, priority);
                            entry.w[rule.From] = form;
                        }

                        if (needsDefault)
                        {
                            string defaultForm = SelectDefaultForm(
                                defaultCandidates, $"Ordinal[{rule.From}].DefaultForm");
                            fallbackWordRules.TryAdd(rule.From, defaultForm);
                        }
                    }
                }

                // Merge synthetic exceptions/wordRules into any container rule sharing the same
                // constraint key. Without this, a container rule (suffix=sten, exceptions={}) and
                // a synthetic rule (exceptions={1:"ersten",...}, suffix=null) both have specificity
                // 3 and FindBestOrdinalVariant picks whichever appears first, losing the other's data.
                var containerKeyToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < result.Count; i++)
                    containerKeyToIndex[RankedConstraintKey(result[i].Constraints, result[i].Priority)] = i;

                foreach (var (constraints, exceptions, wordRules, priority) in syntheticByKey.Values)
                {
                    var key = RankedConstraintKey(constraints, priority);
                    if (containerKeyToIndex.TryGetValue(key, out var idx))
                    {
                        var existing = result[idx];
                        var mergedExc = new Dictionary<long, string>(existing.Exceptions);
                        foreach (var kv in exceptions) mergedExc[kv.Key] = kv.Value;
                        var mergedWr = new Dictionary<string, string>(existing.WordRules);
                        foreach (var kv in wordRules) mergedWr[kv.Key] = kv.Value;
                        result[idx] = new NumberToStringConverter.OrdinalVariantRule(
                            existing.Constraints, mergedExc, mergedWr,
                            existing.Suffix, existing.RemoveTrailing, existing.Priority);
                    }
                    else
                    {
                        result.Add(new NumberToStringConverter.OrdinalVariantRule(constraints, exceptions, wordRules, null, null, priority));
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
                        constraints, exceptions, wordRules, variant.Suffix, variant.RemoveTrailing, variant.Priority));

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
                        var forms = new List<NumberToStringConverter.TriggerReplacementForm>();

                        if (replace.FormVariants?.Count > 0)
                        {
                            foreach (var (constraints, form, priority) in ExpandFormVariants(
                                replace.FormVariants, parsedDimensions,
                                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))
                            {
                                forms.Add(new NumberToStringConverter.TriggerReplacementForm(constraints, form, priority));
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
                DateFirstCardinalDay = language.DateFormat?.FirstCardinalDay,
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
                    foreach (var form in replace.Forms)
                    {
                        foreach (var (key, value) in form.Constraints)
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
                try
                {
                    return registered() ?? throw new InvalidOperationException("The registered factory returned null.");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"LanguageSpecifics factory for configured type '{typeName}' failed.", ex);
                }
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
