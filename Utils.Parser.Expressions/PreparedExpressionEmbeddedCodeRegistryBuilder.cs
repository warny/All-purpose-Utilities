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

        foreach (var action in definition.Actions)
        {
            AddSkippedEntry(
                CreateSource(action.RawCode, EmbeddedCodeKind.GrammarAction, null, null, null),
                null,
                "Grammar-level actions are not prepared by the expression registry builder.",
                skippedEntries,
                allEntries);
        }

        foreach (var rule in definition.ParserRules)
        {
            ScanRule(
                definition,
                rule,
                preparer,
                options,
                registry,
                successfulSemanticPredicates,
                successfulParserActions,
                nonSuccessEntries,
                duplicateEntries,
                skippedEntries,
                allEntries);
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
    /// Scans one parser rule and records any supported or intentionally skipped embedded-code items.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Parser rule to scan.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulSemanticPredicates">Successful semantic predicate entries.</param>
    /// <param name="successfulParserActions">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="skippedEntries">Skipped entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void ScanRule(
        ParserDefinition definition,
        Rule rule,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulSemanticPredicates,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulParserActions,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        if (rule.InitAction is not null)
        {
            AddSkippedEntry(
                CreateSource(rule.InitAction.RawCode, EmbeddedCodeKind.RuleInitAction, rule.Name, null, null),
                rule.Name,
                "Rule initialization actions are not prepared by the expression registry builder.",
                skippedEntries,
                allEntries);
        }

        if (definition.LeftRecursiveRules.TryGetValue(rule.Name, out var leftRecursiveInfo))
        {
            ScanLeftRecursiveRule(
                definition,
                leftRecursiveInfo,
                preparer,
                options,
                registry,
                successfulSemanticPredicates,
                successfulParserActions,
                nonSuccessEntries,
                duplicateEntries,
                skippedEntries,
                allEntries);
        }
        else
        {
            ScanContent(
                definition,
                rule,
                rule.Content,
                null,
                null,
                preparer,
                options,
                registry,
                successfulSemanticPredicates,
                successfulParserActions,
                nonSuccessEntries,
                duplicateEntries,
                skippedEntries,
                allEntries);
        }

        if (rule.AfterAction is not null)
        {
            AddSkippedEntry(
                CreateSource(rule.AfterAction.RawCode, EmbeddedCodeKind.RuleAfterAction, rule.Name, null, null),
                rule.Name,
                "Rule finalization actions are not prepared by the expression registry builder.",
                skippedEntries,
                allEntries);
        }
    }

    /// <summary>
    /// Recursively scans rule content using the same local alternative and sequence indexes supplied by the parser runtime.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="content">Content node to scan.</param>
    /// <param name="alternativeIndex">Current local alternative index, or <c>null</c> when unavailable.</param>
    /// <param name="elementIndex">Current sequence element index, or <c>null</c> when unavailable.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulSemanticPredicates">Successful semantic predicate entries.</param>
    /// <param name="successfulParserActions">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="skippedEntries">Skipped entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void ScanContent(
        ParserDefinition definition,
        Rule rule,
        RuleContent content,
        int? alternativeIndex,
        int? elementIndex,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulSemanticPredicates,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulParserActions,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        switch (content)
        {
            case Alternation alternation:
                ScanAlternation(definition, rule, alternation, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
                break;
            case Alternative alternative:
                ScanContent(definition, rule, alternative.Content, alternativeIndex, elementIndex, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
                break;
            case Sequence sequence:
                ScanSequence(definition, rule, sequence, alternativeIndex, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
                break;
            case Quantifier quantifier:
                ScanQuantifier(definition, rule, quantifier, alternativeIndex, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
                break;
            case Negation negation:
                ScanNegation(definition, rule, negation, alternativeIndex, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
                break;
            case ValidatingPredicate predicate:
                PrepareSemanticPredicate(definition, rule, predicate, alternativeIndex, elementIndex, preparer, options, registry, successfulSemanticPredicates, nonSuccessEntries, duplicateEntries, allEntries);
                break;
            case EmbeddedAction action:
                PrepareOrSkipParserAction(definition, rule, action, alternativeIndex, elementIndex, preparer, options, registry, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
                break;
        }
    }

    /// <summary>
    /// Scans an alternation in runtime priority order so local indexes match scheduler contexts.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="alternation">Alternation to scan.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulSemanticPredicates">Successful semantic predicate entries.</param>
    /// <param name="successfulParserActions">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="skippedEntries">Skipped entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void ScanAlternation(
        ParserDefinition definition,
        Rule rule,
        Alternation alternation,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulSemanticPredicates,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulParserActions,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        var ordered = alternation.Alternatives.OrderBy(static alternative => alternative.Priority).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ScanContent(definition, rule, ordered[index].Content, index, null, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
        }
    }

    /// <summary>
    /// Scans a direct-left-recursive rule using the runtime seed and recursive-tail views.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="info">Left-recursive rule split metadata.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulSemanticPredicates">Successful semantic predicate entries.</param>
    /// <param name="successfulParserActions">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="skippedEntries">Skipped entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void ScanLeftRecursiveRule(
        ParserDefinition definition,
        LeftRecursiveRuleInfo info,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulSemanticPredicates,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulParserActions,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        var baseAlternatives = info.BaseAlternatives.OrderBy(static alternative => alternative.Priority).ToList();
        for (var index = 0; index < baseAlternatives.Count; index++)
        {
            ScanContent(definition, info.Rule, baseAlternatives[index].Content, index, null, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
        }

        var recursiveAlternatives = info.RecursiveAlternatives.OrderBy(static alternative => alternative.Priority).ToList();
        for (var index = 0; index < recursiveAlternatives.Count; index++)
        {
            var tailContent = RemoveLeadingSelfReference(info.Rule.Name, recursiveAlternatives[index].Content);
            if (tailContent is not null)
            {
                ScanLeftRecursiveTail(definition, info.Rule, tailContent, index, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
            }
        }
    }

    /// <summary>
    /// Scans recursive-tail content after applying the same leading self-reference removal used by the runtime.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning left-recursive rule.</param>
    /// <param name="tailContent">Effective tail content parsed by the runtime.</param>
    /// <param name="alternativeIndex">Runtime recursive alternative index.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulSemanticPredicates">Successful semantic predicate entries.</param>
    /// <param name="successfulParserActions">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="skippedEntries">Skipped entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void ScanLeftRecursiveTail(
        ParserDefinition definition,
        Rule rule,
        RuleContent tailContent,
        int alternativeIndex,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulSemanticPredicates,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulParserActions,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
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

                ScanContent(definition, rule, item, alternativeIndex, index, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
            }

            return;
        }

        ScanContent(definition, rule, tailContent, alternativeIndex, alternativeIndex, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
    }

    /// <summary>
    /// Scans sequence items with stable zero-based element indexes matching runtime contexts.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="sequence">Sequence to scan.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulSemanticPredicates">Successful semantic predicate entries.</param>
    /// <param name="successfulParserActions">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="skippedEntries">Skipped entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void ScanSequence(
        ParserDefinition definition,
        Rule rule,
        Sequence sequence,
        int? alternativeIndex,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulSemanticPredicates,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulParserActions,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        for (var index = 0; index < sequence.Items.Count; index++)
        {
            ScanContent(definition, rule, sequence.Items[index], alternativeIndex, index, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
        }
    }

    /// <summary>
    /// Scans quantified content with the runtime element-index strategy for quantifier inner parsing.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="quantifier">Quantifier model node.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulSemanticPredicates">Successful semantic predicate entries.</param>
    /// <param name="successfulParserActions">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="skippedEntries">Skipped entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void ScanQuantifier(
        ParserDefinition definition,
        Rule rule,
        Quantifier quantifier,
        int? alternativeIndex,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulSemanticPredicates,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulParserActions,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        ScanContent(definition, rule, quantifier.Inner, alternativeIndex, alternativeIndex, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
    }

    /// <summary>
    /// Scans negated content with the runtime element-index strategy for negation inner probing.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="negation">Negation model node.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulSemanticPredicates">Successful semantic predicate entries.</param>
    /// <param name="successfulParserActions">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="skippedEntries">Skipped entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void ScanNegation(
        ParserDefinition definition,
        Rule rule,
        Negation negation,
        int? alternativeIndex,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulSemanticPredicates,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulParserActions,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        ScanContent(definition, rule, negation.Inner, alternativeIndex, alternativeIndex, preparer, options, registry, successfulSemanticPredicates, successfulParserActions, nonSuccessEntries, duplicateEntries, skippedEntries, allEntries);
    }

    /// <summary>
    /// Prepares and registers one validating semantic predicate.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="predicate">Predicate model node.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="elementIndex">Current sequence element index.</param>
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
        ValidatingPredicate predicate,
        int? alternativeIndex,
        int? elementIndex,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        var source = CreateSource(predicate.Code, EmbeddedCodeKind.SemanticPredicate, rule.Name, alternativeIndex, elementIndex);
        var context = CreatePreparationContext(definition, rule, options);
        var result = preparer.PrepareSemanticPredicate(source, context);
        var key = result.Artifact is null ? null : PreparedExpressionEmbeddedCodeKey.FromSource(result.Artifact.Source, result.Artifact.PreparationContext.RuleName);
        var wasAdded = result.Artifact is not null && registry.TryAddSemanticPredicate(result.Artifact);
        var entry = CreateEntry(source, key, rule.Name, result.Status, result.DiagnosticDescriptor, result.Exception, result.DiagnosticArguments, wasAdded, result.Artifact is not null && !wasAdded);

        AddPreparationEntry(entry, result.Artifact is not null && wasAdded, successfulEntries, nonSuccessEntries, duplicateEntries, allEntries);
    }

    /// <summary>
    /// Prepares and registers one inline parser action, or records unsupported action positions as skipped.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Owning parser rule.</param>
    /// <param name="action">Action model node.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="elementIndex">Current sequence element index.</param>
    /// <param name="preparer">Preparer used for supported embedded-code items.</param>
    /// <param name="options">Builder options.</param>
    /// <param name="registry">Registry being populated.</param>
    /// <param name="successfulEntries">Successful parser action entries.</param>
    /// <param name="nonSuccessEntries">Non-success preparation entries.</param>
    /// <param name="duplicateEntries">Duplicate key entries.</param>
    /// <param name="skippedEntries">Skipped entries.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void PrepareOrSkipParserAction(
        ParserDefinition definition,
        Rule rule,
        EmbeddedAction action,
        int? alternativeIndex,
        int? elementIndex,
        IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction> preparer,
        PreparedExpressionEmbeddedCodeRegistryBuilderOptions options,
        PreparedExpressionEmbeddedCodeRegistry registry,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> successfulEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> nonSuccessEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> duplicateEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        var source = CreateSource(action.RawCode, EmbeddedCodeKind.ParserInlineAction, rule.Name, alternativeIndex, elementIndex);
        if (action.Context != ActionContext.Alternative || action.Position != ActionPosition.Inline)
        {
            AddSkippedEntry(
                source,
                rule.Name,
                "Only inline parser actions declared inside alternatives are prepared by the expression registry builder.",
                skippedEntries,
                allEntries);
            return;
        }

        var context = CreatePreparationContext(definition, rule, options);
        var result = preparer.PrepareParserAction(source, context);
        var key = result.Artifact is null ? null : PreparedExpressionEmbeddedCodeKey.FromSource(result.Artifact.Source, result.Artifact.PreparationContext.RuleName);
        var wasAdded = result.Artifact is not null && registry.TryAddParserAction(result.Artifact);
        var entry = CreateEntry(source, key, rule.Name, result.Status, result.DiagnosticDescriptor, result.Exception, result.DiagnosticArguments, wasAdded, result.Artifact is not null && !wasAdded);

        AddPreparationEntry(entry, result.Artifact is not null && wasAdded, successfulEntries, nonSuccessEntries, duplicateEntries, allEntries);
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
    /// <param name="source">Embedded-code source metadata.</param>
    /// <param name="ruleName">Owning rule name, or <c>null</c> for grammar-level items.</param>
    /// <param name="reason">Reason why the item was skipped.</param>
    /// <param name="skippedEntries">Skipped entries bucket.</param>
    /// <param name="allEntries">All entries in traversal order.</param>
    private static void AddSkippedEntry(
        EmbeddedCodeSource source,
        string? ruleName,
        string reason,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> skippedEntries,
        List<PreparedExpressionEmbeddedCodeRegistryBuildEntry> allEntries)
    {
        var entry = new PreparedExpressionEmbeddedCodeRegistryBuildEntry(
            source,
            null,
            ruleName,
            EmbeddedCodePreparationStatus.Unsupported,
            wasAddedToRegistry: false,
            isSkipped: true,
            skipReason: reason);

        skippedEntries.Add(entry);
        allEntries.Add(entry);
    }
}
