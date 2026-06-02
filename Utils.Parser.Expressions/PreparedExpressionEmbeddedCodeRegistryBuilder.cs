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

        foreach (var action in definition.Actions)
        {
            state.RecordSkippedEntry(
                CreateSource(action.RawCode, EmbeddedCodeKind.GrammarAction, null, null, null),
                null,
                "Grammar-level actions are not prepared by the expression registry builder.");
        }

        foreach (var rule in definition.ParserRules)
        {
            ScanRule(rule, state);
        }

        return state.ToResult();
    }

    /// <summary>
    /// Scans one parser rule and records any supported or intentionally skipped embedded-code items.
    /// </summary>
    /// <param name="rule">Parser rule to scan.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void ScanRule(Rule rule, BuildState state)
    {
        if (rule.InitAction is not null)
        {
            state.RecordSkippedEntry(
                CreateSource(rule.InitAction.RawCode, EmbeddedCodeKind.RuleInitAction, rule.Name, null, null),
                rule.Name,
                "Rule initialization actions are not prepared by the expression registry builder.");
        }

        if (state.Definition.LeftRecursiveRules.TryGetValue(rule.Name, out var leftRecursiveInfo))
        {
            ScanLeftRecursiveRule(leftRecursiveInfo, state);
        }
        else
        {
            ScanContent(rule, rule.Content, null, null, state);
        }

        if (rule.AfterAction is not null)
        {
            state.RecordSkippedEntry(
                CreateSource(rule.AfterAction.RawCode, EmbeddedCodeKind.RuleAfterAction, rule.Name, null, null),
                rule.Name,
                "Rule finalization actions are not prepared by the expression registry builder.");
        }
    }

    /// <summary>
    /// Recursively scans rule content using the same local alternative and sequence indexes supplied by the parser runtime.
    /// </summary>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="content">Content node to scan.</param>
    /// <param name="alternativeIndex">Current local alternative index, or <c>null</c> when unavailable.</param>
    /// <param name="elementIndex">Current sequence element index, or <c>null</c> when unavailable.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void ScanContent(Rule rule, RuleContent content, int? alternativeIndex, int? elementIndex, BuildState state)
    {
        switch (content)
        {
            case Alternation alternation:
                ScanAlternation(rule, alternation, state);
                break;
            case Alternative alternative:
                ScanContent(rule, alternative.Content, alternativeIndex, elementIndex, state);
                break;
            case Sequence sequence:
                ScanSequence(rule, sequence, alternativeIndex, state);
                break;
            case Quantifier quantifier:
                ScanQuantifier(rule, quantifier, alternativeIndex, state);
                break;
            case Negation negation:
                ScanNegation(rule, negation, alternativeIndex, state);
                break;
            case ValidatingPredicate predicate:
                PrepareSemanticPredicate(rule, predicate, alternativeIndex, elementIndex, state);
                break;
            case EmbeddedAction action:
                PrepareOrSkipParserAction(rule, action, alternativeIndex, elementIndex, state);
                break;
        }
    }

    /// <summary>
    /// Scans an alternation in runtime priority order so local indexes match scheduler contexts.
    /// </summary>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="alternation">Alternation to scan.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void ScanAlternation(Rule rule, Alternation alternation, BuildState state)
    {
        var ordered = alternation.Alternatives.OrderBy(static alternative => alternative.Priority).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ScanContent(rule, ordered[index].Content, index, null, state);
        }
    }

    /// <summary>
    /// Scans a direct-left-recursive rule using the runtime seed and recursive-tail views.
    /// </summary>
    /// <param name="info">Left-recursive rule split metadata.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void ScanLeftRecursiveRule(LeftRecursiveRuleInfo info, BuildState state)
    {
        var baseAlternatives = info.BaseAlternatives.OrderBy(static alternative => alternative.Priority).ToList();
        for (var index = 0; index < baseAlternatives.Count; index++)
        {
            ScanContent(info.Rule, baseAlternatives[index].Content, index, null, state);
        }

        var recursiveAlternatives = info.RecursiveAlternatives.OrderBy(static alternative => alternative.Priority).ToList();
        for (var index = 0; index < recursiveAlternatives.Count; index++)
        {
            var tailContent = RemoveLeadingSelfReference(info.Rule.Name, recursiveAlternatives[index].Content);
            if (tailContent is not null)
            {
                ScanLeftRecursiveTail(info.Rule, tailContent, index, state);
            }
        }
    }

    /// <summary>
    /// Scans recursive-tail content after applying the same leading self-reference removal used by the runtime.
    /// </summary>
    /// <param name="rule">Owning left-recursive rule.</param>
    /// <param name="tailContent">Effective tail content parsed by the runtime.</param>
    /// <param name="alternativeIndex">Runtime recursive alternative index.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void ScanLeftRecursiveTail(Rule rule, RuleContent tailContent, int alternativeIndex, BuildState state)
    {
        if (tailContent is Sequence sequence)
        {
            for (var index = 0; index < sequence.Items.Count; index++)
            {
                var item = sequence.Items[index];
                if (item is RuleRef ruleRef && string.Equals(ruleRef.RuleName, rule.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                ScanContent(rule, item, alternativeIndex, index, state);
            }

            return;
        }

        ScanContent(rule, tailContent, alternativeIndex, alternativeIndex, state);
    }

    /// <summary>
    /// Scans sequence items with stable zero-based element indexes matching runtime contexts.
    /// </summary>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="sequence">Sequence to scan.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void ScanSequence(Rule rule, Sequence sequence, int? alternativeIndex, BuildState state)
    {
        for (var index = 0; index < sequence.Items.Count; index++)
        {
            ScanContent(rule, sequence.Items[index], alternativeIndex, index, state);
        }
    }

    /// <summary>
    /// Scans quantified content with the runtime element-index strategy for quantifier inner parsing.
    /// </summary>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="quantifier">Quantifier model node.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void ScanQuantifier(Rule rule, Quantifier quantifier, int? alternativeIndex, BuildState state)
    {
        ScanContent(rule, quantifier.Inner, alternativeIndex, alternativeIndex, state);
    }

    /// <summary>
    /// Scans negated content with the runtime element-index strategy for negation inner probing.
    /// </summary>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="negation">Negation model node.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void ScanNegation(Rule rule, Negation negation, int? alternativeIndex, BuildState state)
    {
        ScanContent(rule, negation.Inner, alternativeIndex, alternativeIndex, state);
    }

    /// <summary>
    /// Prepares and registers one validating semantic predicate.
    /// </summary>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="predicate">Predicate model node.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="elementIndex">Current sequence element index.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void PrepareSemanticPredicate(Rule rule, ValidatingPredicate predicate, int? alternativeIndex, int? elementIndex, BuildState state)
    {
        var source = CreateSource(predicate.Code, EmbeddedCodeKind.SemanticPredicate, rule.Name, alternativeIndex, elementIndex);
        var context = CreatePreparationContext(state.Definition, rule, state.Options);
        var result = state.Preparer.PrepareSemanticPredicate(source, context);
        var key = result.Artifact is null ? null : PreparedExpressionEmbeddedCodeKey.FromSource(result.Artifact.Source, result.Artifact.PreparationContext.RuleName);
        var wasAdded = result.Artifact is not null && state.Registry.TryAddSemanticPredicate(result.Artifact);
        var entry = CreateEntry(source, key, rule.Name, result.Status, result.DiagnosticDescriptor, result.Exception, result.DiagnosticArguments, wasAdded, result.Artifact is not null && !wasAdded);

        state.RecordSemanticPredicateEntry(entry, wasAdded);
    }

    /// <summary>
    /// Prepares and registers one inline parser action, or records unsupported action positions as skipped.
    /// </summary>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="action">Action model node.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="elementIndex">Current sequence element index.</param>
    /// <param name="state">Mutable build state accumulating registry entries and artifacts.</param>
    private static void PrepareOrSkipParserAction(Rule rule, EmbeddedAction action, int? alternativeIndex, int? elementIndex, BuildState state)
    {
        var source = CreateSource(action.RawCode, EmbeddedCodeKind.ParserInlineAction, rule.Name, alternativeIndex, elementIndex);
        if (action.Context != ActionContext.Alternative || action.Position != ActionPosition.Inline)
        {
            state.RecordSkippedEntry(
                source,
                rule.Name,
                "Only inline parser actions declared inside alternatives are prepared by the expression registry builder.");
            return;
        }

        var context = CreatePreparationContext(state.Definition, rule, state.Options);
        var result = state.Preparer.PrepareParserAction(source, context);
        var key = result.Artifact is null ? null : PreparedExpressionEmbeddedCodeKey.FromSource(result.Artifact.Source, result.Artifact.PreparationContext.RuleName);
        var wasAdded = result.Artifact is not null && state.Registry.TryAddParserAction(result.Artifact);
        var entry = CreateEntry(source, key, rule.Name, result.Status, result.DiagnosticDescriptor, result.Exception, result.DiagnosticArguments, wasAdded, result.Artifact is not null && !wasAdded);

        state.RecordParserActionEntry(entry, wasAdded);
    }

    /// <summary>
    /// Removes the leading direct self-reference exactly as the parser runtime does for left-recursive tails.
    /// </summary>
    /// <param name="ruleName">Name of the left-recursive rule.</param>
    /// <param name="content">Recursive alternative content.</param>
    /// <returns>The effective tail content parsed by the runtime, or <c>null</c> when no leading self-reference exists.</returns>
    private static RuleContent? RemoveLeadingSelfReference(string ruleName, RuleContent content)
    {
        switch (content)
        {
            case RuleRef ruleRef when string.Equals(ruleRef.RuleName, ruleName, StringComparison.Ordinal):
                return new Sequence([]);
            case Sequence sequence when sequence.Items.Count > 0:
            {
                if (sequence.Items[0] is RuleRef leading &&
                    string.Equals(leading.RuleName, ruleName, StringComparison.Ordinal))
                {
                    return new Sequence(sequence.Items.Skip(1).ToList());
                }

                return null;
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Creates source metadata for a discovered embedded-code item.
    /// </summary>
    /// <param name="sourceText">Raw embedded-code source text.</param>
    /// <param name="kind">Embedded-code kind.</param>
    /// <param name="ruleName">Owning rule name.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="elementIndex">Current sequence element index.</param>
    /// <returns>Source metadata suitable for preparation and key creation.</returns>
    private static EmbeddedCodeSource CreateSource(string sourceText, EmbeddedCodeKind kind, string? ruleName, int? alternativeIndex, int? elementIndex) =>
        new(sourceText, kind, ruleName, alternativeIndex, elementIndex);

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
        Utils.Parser.Diagnostics.ParserDiagnosticDescriptor? diagnosticDescriptor,
        Exception? exception,
        IReadOnlyList<object?> diagnosticArguments,
        bool wasAdded,
        bool isDuplicate) =>
        new(source, key, ruleName, status, diagnosticDescriptor, exception, diagnosticArguments, wasAdded, isDuplicate);

    /// <summary>
    /// Holds the mutable traversal state shared across all scan and prepare methods during a single <see cref="Build"/> call.
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
        /// <param name="source">Embedded-code source metadata.</param>
        /// <param name="ruleName">Owning rule name, or <c>null</c> for grammar-level items.</param>
        /// <param name="reason">Reason why the item was skipped.</param>
        public void RecordSkippedEntry(EmbeddedCodeSource source, string? ruleName, string reason)
        {
            var entry = new PreparedExpressionEmbeddedCodeRegistryBuildEntry(
                source,
                null,
                ruleName,
                EmbeddedCodePreparationStatus.Unsupported,
                wasAddedToRegistry: false,
                isSkipped: true,
                skipReason: reason);

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
