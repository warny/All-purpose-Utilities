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

        var registry = new PreparedExpressionEmbeddedCodeRegistry();
        var successfulSemanticPredicates = new List<PreparedExpressionEmbeddedCodeRegistryBuildEntry>();
        var successfulParserActions = new List<PreparedExpressionEmbeddedCodeRegistryBuildEntry>();
        var nonSuccessEntries = new List<PreparedExpressionEmbeddedCodeRegistryBuildEntry>();
        var duplicateEntries = new List<PreparedExpressionEmbeddedCodeRegistryBuildEntry>();
        var skippedEntries = new List<PreparedExpressionEmbeddedCodeRegistryBuildEntry>();
        var allEntries = new List<PreparedExpressionEmbeddedCodeRegistryBuildEntry>();
        var discovery = EmbeddedCodeRuntimeDiscovery.Discover(definition);

        foreach (var entry in discovery.Entries)
        {
            if (!entry.IsRuntimeExecutable)
            {
                AddSkippedEntry(entry, skippedEntries, allEntries);
                continue;
            }

            var rule = GetParserRule(definition, entry.RuleName!);
            if (entry.Kind == EmbeddedCodeKind.SemanticPredicate)
            {
                PrepareSemanticPredicate(definition, rule, entry.Source, preparer, options, registry, successfulSemanticPredicates, nonSuccessEntries, duplicateEntries, allEntries);
                continue;
            }

            if (entry.Kind == EmbeddedCodeKind.ParserInlineAction)
            {
                PrepareParserAction(definition, rule, entry.Source, preparer, options, registry, successfulParserActions, nonSuccessEntries, duplicateEntries, allEntries);
                continue;
            }

            AddSkippedEntry(entry, skippedEntries, allEntries);
        }

        return new PreparedExpressionEmbeddedCodeRegistryBuildResult(
            registry,
            successfulSemanticPredicates,
            successfulParserActions,
            nonSuccessEntries,
            duplicateEntries,
            skippedEntries,
            allEntries);
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
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="source">Runtime-indexed source metadata.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulEntries">Successful semantic predicate entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void PrepareSemanticPredicate(
        ParserDefinition definition,
        Rule rule,
        EmbeddedCodeSource source,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        var context = CreatePreparationContext(definition, rule, options);
        var result = preparer.PrepareSemanticPredicate(source, context);
        var key = result.Artifact is null ? null : PreparedExpressionEmbeddedCodeKey.FromSource(result.Artifact.Source, result.Artifact.PreparationContext.RuleName);
        var wasAdded = result.Artifact is not null && registry.TryAddSemanticPredicate(result.Artifact);
        var buildEntry = CreateEntry(source, key, rule.Name, result.Status, result.DiagnosticDescriptor, result.Exception, result.DiagnosticArguments, wasAdded, result.Artifact is not null && !wasAdded);

        AddPreparationEntry(buildEntry, result.Artifact is not null && wasAdded, successfulEntries, nonSuccessEntries, duplicateEntries, allEntries);
    }

    /// <summary>
    /// Prepares and registers one inline parser action discovered by shared runtime metadata.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="source">Runtime-indexed source metadata.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulEntries">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void PrepareParserAction(
        ParserDefinition definition,
        Rule rule,
        EmbeddedCodeSource source,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        var context = CreatePreparationContext(definition, rule, options);
        var result = preparer.PrepareParserAction(source, context);
        var key = result.Artifact is null ? null : PreparedExpressionEmbeddedCodeKey.FromSource(result.Artifact.Source, result.Artifact.PreparationContext.RuleName);
        var wasAdded = result.Artifact is not null && registry.TryAddParserAction(result.Artifact);
        var buildEntry = CreateEntry(source, key, rule.Name, result.Status, result.DiagnosticDescriptor, result.Exception, result.DiagnosticArguments, wasAdded, result.Artifact is not null && !wasAdded);

        AddPreparationEntry(buildEntry, result.Artifact is not null && wasAdded, successfulEntries, nonSuccessEntries, duplicateEntries, allEntries);
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
    /// Adds a preparation entry to the appropriate result buckets.
    /// </summary>
    /// <param name="entry">Entry to add.</param>
    /// <param name="isRegisteredSuccess">Whether the entry is a registered success.</param>
    /// <param name="successfulEntries">Success bucket for this embedded-code kind.</param>
    /// <param name="nonSuccessEntries">Non-success preparation bucket.</param>
    /// <param name="duplicateEntries">Duplicate key bucket.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void AddPreparationEntry(
        PreparedExpressionEmbeddedCodeRegistryBuildEntry entry,
        bool isRegisteredSuccess,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        allEntries.Add(entry);

        if (entry.IsDuplicate)
        {
            duplicateEntries.Add(entry);
            return;
        }

        if (isRegisteredSuccess)
        {
            successfulEntries.Add(entry);
            return;
        }

        nonSuccessEntries.Add(entry);
    }

    /// <summary>
    /// Records an embedded-code item that is intentionally not prepared by this builder.
    /// </summary>
    /// <param name="runtimeEntry">Runtime discovery entry to record as skipped.</param>
    /// <param name="skippedEntries">Skipped entries bucket.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void AddSkippedEntry(
        EmbeddedCodeRuntimeEntry runtimeEntry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
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

        skippedEntries.Add(entry);
        allEntries.Add(entry);
    }

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
}
