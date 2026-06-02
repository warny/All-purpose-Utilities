using Utils.Parser.Diagnostics;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Model;

namespace Utils.Parser.Expressions;

/// <summary>
/// Builds a prepared expression embedded-code registry by scanning an existing parser model explicitly.
/// </summary>
public static class PreparedExpressionEmbeddedCodeRegistryBuilder
{
    /// <summary>
    /// Builds a registry from semantic predicates and inline parser actions found in a parser definition.
    /// </summary>
    /// <param name="definition">Parser definition to scan without modification.</param>
    /// <param name="preparer">Embedded-code preparer used to compile or otherwise prepare discovered items.</param>
    /// <param name="options">Optional builder configuration.</param>
    /// <returns>A build result containing the registry and audit entries for successes, failures, duplicates, and skipped items.</returns>
    public static PreparedExpressionEmbeddedCodeRegistryBuildResult Build(
        ParserDefinition definition,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(preparer);

        options ??= PreparedExpressionEmbeddedCodeRegistryBuilderOptions.Default;

        var state = new BuildState(definition, preparer, options);
        var discovery = EmbeddedCodeRuntimeDiscovery.Discover(definition);

        foreach (var entry in discovery.Entries)
        {
            if (!entry.IsRuntimeExecutable)
            {
                state.RecordSkippedEntry(entry);
                continue;
            }

            var rule = GetParserRule(definition, entry.RuleName!);
            if (entry.Kind == EmbeddedCodeKind.SemanticPredicate)
            {
                PrepareSemanticPredicate(rule, entry.Source, state);
                continue;
            }

            if (entry.Kind == EmbeddedCodeKind.ParserInlineAction)
            {
                PrepareParserAction(rule, entry.Source, state);
                continue;
            }

            state.RecordSkippedEntry(entry);
        }

        return state.ToResult();
    }

    /// <summary>
    /// Finds a parser rule by name in the definition currently being scanned.
    /// </summary>
    /// <param name="definition">Parser definition that owns the rule.</param>
    /// <param name="ruleName">Rule name to locate.</param>
    /// <returns>The matching parser rule.</returns>
    private static Rule GetParserRule(ParserDefinition definition, string ruleName) =>
        definition.ParserRules.First(rule => string.Equals(rule.Name, ruleName, StringComparison.Ordinal));

    /// <summary>
    /// Prepares and registers one validating semantic predicate discovered by shared runtime metadata.
    /// </summary>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="source">Runtime-indexed source metadata.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void PrepareSemanticPredicate(Rule rule, EmbeddedCodeSource source, BuildState state)
    {
        var context = CreatePreparationContext(state.Definition, rule, state.Options);
        var result = state.Preparer.PrepareSemanticPredicate(source, context);
        var key = result.Artifact is null ? null : PreparedExpressionEmbeddedCodeKey.FromSource(result.Artifact.Source, result.Artifact.PreparationContext.RuleName);
        var wasAdded = result.Artifact is not null && state.Registry.TryAddSemanticPredicate(result.Artifact);
        var entry = CreateEntry(source, key, rule.Name, result.Status, result.DiagnosticDescriptor, result.Exception, result.DiagnosticArguments, wasAdded, result.Artifact is not null && !wasAdded);

        state.RecordSemanticPredicateEntry(entry, wasAdded);
    }

    /// <summary>
    /// Prepares and registers one inline parser action discovered by shared runtime metadata.
    /// </summary>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="source">Runtime-indexed source metadata.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void PrepareParserAction(Rule rule, EmbeddedCodeSource source, BuildState state)
    {
        var context = CreatePreparationContext(state.Definition, rule, state.Options);
        var result = state.Preparer.PrepareParserAction(source, context);
        var key = result.Artifact is null ? null : PreparedExpressionEmbeddedCodeKey.FromSource(result.Artifact.Source, result.Artifact.PreparationContext.RuleName);
        var wasAdded = result.Artifact is not null && state.Registry.TryAddParserAction(result.Artifact);
        var entry = CreateEntry(source, key, rule.Name, result.Status, result.DiagnosticDescriptor, result.Exception, result.DiagnosticArguments, wasAdded, result.Artifact is not null && !wasAdded);

        state.RecordParserActionEntry(entry, wasAdded);
    }

    /// <summary>
    /// Creates the preparation context supplied to the configured preparer.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="options">Builder options.</param>
    /// <returns>A runtime-inline expression preparation context.</returns>
    private static EmbeddedCodePreparationContext CreatePreparationContext(
        ParserDefinition definition,
        Rule rule,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options) =>
        new(
            options.GrammarName ?? definition.Name,
            EmbeddedCodeTarget.RuntimeInlineExpression,
            rule.Name,
            options.LanguageOrCompilerIdentity,
            supportedSymbols: options.SupportedSymbols);

    /// <summary>
    /// Creates a build entry for a preparation attempt.
    /// </summary>
    /// <param name="source">Embedded-code source metadata.</param>
    /// <param name="key">Registry key used for successful artifacts.</param>
    /// <param name="ruleName">Owning rule name.</param>
    /// <param name="status">Preparation status.</param>
    /// <param name="diagnosticDescriptor">Optional diagnostic metadata.</param>
    /// <param name="exception">Optional exception metadata.</param>
    /// <param name="diagnosticArguments">Optional diagnostic arguments.</param>
    /// <param name="wasAdded">Whether the artifact was added to the registry.</param>
    /// <param name="isDuplicate">Whether the key collided with an existing artifact.</param>
    /// <returns>A build entry describing the attempt.</returns>
    private static PreparedExpressionEmbeddedCodeRegistryBuildEntry CreateEntry(
        EmbeddedCodeSource source,
        PreparedExpressionEmbeddedCodeKey? key,
        string? ruleName,
        EmbeddedCodePreparationStatus status,
        ParserDiagnosticDescriptor? diagnosticDescriptor,
        Exception? exception,
        IReadOnlyList<object?> diagnosticArguments,
        bool wasAdded,
        bool isDuplicate) =>
        new(source, key, ruleName, status, diagnosticDescriptor, exception, diagnosticArguments, wasAdded, isDuplicate);

    /// <summary>
    /// Converts a common unsupported reason into the legacy human-readable skip reason text.
    /// </summary>
    /// <param name="reason">Common unsupported reason.</param>
    /// <returns>Human-readable skip reason.</returns>
    private static string GetSkipReason(EmbeddedCodeUnsupportedReason reason) => reason switch
    {
        EmbeddedCodeUnsupportedReason.GrammarAction => "Grammar-level actions are not prepared by the expression registry builder.",
        EmbeddedCodeUnsupportedReason.RuleInitAction => "Rule initialization actions are not prepared by the expression registry builder.",
        EmbeddedCodeUnsupportedReason.RuleAfterAction => "Rule finalization actions are not prepared by the expression registry builder.",
        EmbeddedCodeUnsupportedReason.UnsupportedActionContext or EmbeddedCodeUnsupportedReason.UnsupportedActionPosition or EmbeddedCodeUnsupportedReason.NonInlineParserAction => "Only inline parser actions declared inside alternatives are prepared by the expression registry builder.",
        EmbeddedCodeUnsupportedReason.LexerAction => "Lexer actions are not prepared by the expression registry builder.",
        EmbeddedCodeUnsupportedReason.LexerPredicate => "Lexer predicates are not prepared by the expression registry builder.",
        EmbeddedCodeUnsupportedReason.UnsupportedEmbeddedCodeKind => "This embedded-code kind is not prepared by the expression registry builder.",
        EmbeddedCodeUnsupportedReason.MissingRuntimeIndex => "The embedded code is missing runtime dispatch indexes required by the expression registry builder.",
        EmbeddedCodeUnsupportedReason.UnsupportedRuntimeShape => "The embedded code appears in a runtime shape that is not prepared by the expression registry builder.",
        _ => "The embedded code is not prepared by the expression registry builder."
    };

    /// <summary>
    /// Holds the mutable traversal state shared across all prepare methods during a single <see cref="Build"/> call.
    /// </summary>
    private sealed class BuildState
    {
        /// <summary>
        /// Initializes a new build state for the given parser definition, preparer, and options.
        /// </summary>
        /// <param name="definition">Parser definition being scanned.</param>
        /// <param name="preparer">Embedded-code preparer configured for this build.</param>
        /// <param name="options">Builder options configured for this build.</param>
        public BuildState(
            ParserDefinition definition,
            IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
            PreparedExpressionEmbeddedCodeRegistryBuilderOptions options)
        {
            Definition = definition;
            Preparer = preparer;
            Options = options;
        }

        /// <summary>Gets the parser definition being scanned.</summary>
        public ParserDefinition Definition { get; }

        /// <summary>Gets the embedded-code preparer configured for this build.</summary>
        public IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> Preparer { get; }

        /// <summary>Gets the builder options configured for this build.</summary>
        public PreparedExpressionEmbeddedCodeRegistryBuilderOptions Options { get; }

        /// <summary>Gets the registry being populated during this build.</summary>
        public PreparedExpressionEmbeddedCodeRegistry Registry { get; } = new();

        /// <summary>Gets the successful semantic predicate entries accumulated so far.</summary>
        public List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> SuccessfulSemanticPredicates { get; } = [];

        /// <summary>Gets the successful parser action entries accumulated so far.</summary>
        public List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> SuccessfulParserActions { get; } = [];

        /// <summary>Gets the non-success preparation entries accumulated so far.</summary>
        public List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> NonSuccessEntries { get; } = [];

        /// <summary>Gets the duplicate-key entries accumulated so far.</summary>
        public List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> DuplicateEntries { get; } = [];

        /// <summary>Gets the intentionally skipped entries accumulated so far.</summary>
        public List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> SkippedEntries { get; } = [];

        /// <summary>Gets all entries in traversal order accumulated so far.</summary>
        public List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> AllEntries { get; } = [];

        /// <summary>
        /// Packages accumulated state into the final build result.
        /// </summary>
        /// <returns>The completed build result.</returns>
        public PreparedExpressionEmbeddedCodeRegistryBuildResult ToResult() =>
            new(Registry, SuccessfulSemanticPredicates, SuccessfulParserActions, NonSuccessEntries, DuplicateEntries, SkippedEntries, AllEntries);

        /// <summary>
        /// Records a semantic predicate preparation entry in the appropriate buckets.
        /// </summary>
        /// <param name="entry">Entry to record.</param>
        /// <param name="wasAdded">Whether the artifact was successfully added to the registry.</param>
        public void RecordSemanticPredicateEntry(PreparedExpressionEmbeddedCodeRegistryBuildEntry entry, bool wasAdded) =>
            RecordPreparationEntry(entry, wasAdded, SuccessfulSemanticPredicates);

        /// <summary>
        /// Records a parser action preparation entry in the appropriate buckets.
        /// </summary>
        /// <param name="entry">Entry to record.</param>
        /// <param name="wasAdded">Whether the artifact was successfully added to the registry.</param>
        public void RecordParserActionEntry(PreparedExpressionEmbeddedCodeRegistryBuildEntry entry, bool wasAdded) =>
            RecordPreparationEntry(entry, wasAdded, SuccessfulParserActions);

        /// <summary>
        /// Records an embedded-code item that is intentionally not prepared by this builder.
        /// </summary>
        /// <param name="runtimeEntry">Runtime discovery entry to record as skipped.</param>
        public void RecordSkippedEntry(EmbeddedCodeRuntimeEntry runtimeEntry)
        {
            var entry = new PreparedExpressionEmbeddedCodeRegistryBuildEntry(
                runtimeEntry.Source,
                null,
                runtimeEntry.RuleName,
                EmbeddedCodePreparationStatus.Unsupported,
                wasAddedToRegistry: false,
                isSkipped: true,
                skipReason: GetSkipReason(runtimeEntry.UnsupportedReason),
                unsupportedReason: runtimeEntry.UnsupportedReason);

            SkippedEntries.Add(entry);
            AllEntries.Add(entry);
        }

        /// <summary>
        /// Routes a preparation entry to the correct success, non-success, or duplicate bucket.
        /// </summary>
        /// <param name="entry">Entry to route.</param>
        /// <param name="isRegisteredSuccess">Whether the artifact was successfully added to the registry.</param>
        /// <param name="successfulEntries">Success bucket for the artifact kind being recorded.</param>
        private void RecordPreparationEntry(
            PreparedExpressionEmbeddedCodeRegistryBuildEntry entry,
            bool isRegisteredSuccess,
            List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulEntries)
        {
            AllEntries.Add(entry);

            if (entry.IsDuplicate)
            {
                DuplicateEntries.Add(entry);
                return;
            }

            if (isRegisteredSuccess)
            {
                successfulEntries.Add(entry);
                return;
            }

            NonSuccessEntries.Add(entry);
        }
    }
}
