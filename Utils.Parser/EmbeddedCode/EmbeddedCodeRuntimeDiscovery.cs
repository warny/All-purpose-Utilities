using Utils.Parser.Model;

namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Discovers embedded-code items from a resolved parser definition using indexes compatible with parser runtime contexts.
/// </summary>
public static class EmbeddedCodeRuntimeDiscovery
{
    /// <summary>
    /// Discovers embedded-code entries from the supplied parser definition.
    /// </summary>
    /// <param name="definition">Parser definition to inspect without modification.</param>
    /// <returns>Discovery entries in deterministic traversal order.</returns>
    public static EmbeddedCodeRuntimeDiscoveryResult Discover(ParserDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var entries = new List<EmbeddedCodeRuntimeEntry>();
        foreach (var action in definition.Actions)
        {
            entries.Add(CreateUnsupported(action.RawCode, EmbeddedCodeKind.GrammarAction, null, null, null, EmbeddedCodeUnsupportedReason.GrammarAction));
        }

        foreach (var rule in definition.ParserRules)
        {
            DiscoverParserRule(definition, rule, entries);
        }

        foreach (var mode in definition.Modes)
        {
            foreach (var rule in mode.Rules)
            {
                DiscoverLexerRule(rule, entries);
            }
        }

        return new EmbeddedCodeRuntimeDiscoveryResult(entries);
    }

    /// <summary>
    /// Discovers embedded-code entries in a parser rule, including lifecycle actions and left-recursive metadata.
    /// </summary>
    /// <param name="definition">Owning parser definition.</param>
    /// <param name="rule">Parser rule to inspect.</param>
    /// <param name="entries">Destination entry list.</param>
    private static void DiscoverParserRule(ParserDefinition definition, Rule rule, List<EmbeddedCodeRuntimeEntry> entries)
    {
        if (rule.InitAction is not null)
        {
            entries.Add(CreateUnsupported(rule.InitAction.RawCode, EmbeddedCodeKind.RuleInitAction, rule.Name, null, null, EmbeddedCodeUnsupportedReason.RuleInitAction));
        }

        if (definition.LeftRecursiveRules.TryGetValue(rule.Name, out var leftRecursiveInfo))
        {
            DiscoverLeftRecursiveRule(leftRecursiveInfo, entries);
        }
        else
        {
            DiscoverContent(rule.Name, rule.Content, null, null, entries);
        }

        if (rule.AfterAction is not null)
        {
            entries.Add(CreateUnsupported(rule.AfterAction.RawCode, EmbeddedCodeKind.RuleAfterAction, rule.Name, null, null, EmbeddedCodeUnsupportedReason.RuleAfterAction));
        }
    }

    /// <summary>
    /// Discovers embedded-code entries in a lexer rule as unsupported parser runtime hooks.
    /// </summary>
    /// <param name="rule">Lexer rule to inspect.</param>
    /// <param name="entries">Destination entry list.</param>
    private static void DiscoverLexerRule(Rule rule, List<EmbeddedCodeRuntimeEntry> entries)
    {
        DiscoverLexerContent(rule.Name, rule.Content, null, null, entries);
    }

    /// <summary>
    /// Recursively discovers embedded-code entries from parser content using runtime-compatible indexes.
    /// </summary>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="content">Parser content to inspect.</param>
    /// <param name="alternativeIndex">Current runtime alternative index.</param>
    /// <param name="elementIndex">Current runtime element index.</param>
    /// <param name="entries">Destination entry list.</param>
    private static void DiscoverContent(string ruleName, RuleContent content, int? alternativeIndex, int? elementIndex, List<EmbeddedCodeRuntimeEntry> entries)
    {
        switch (content)
        {
            case Alternation alternation:
                DiscoverAlternation(ruleName, alternation, entries);
                break;
            case Alternative alternative:
                DiscoverContent(ruleName, alternative.Content, alternativeIndex, elementIndex, entries);
                break;
            case Sequence sequence:
                DiscoverSequence(ruleName, sequence.Items, alternativeIndex, entries);
                break;
            case Quantifier quantifier:
                DiscoverContent(ruleName, quantifier.Inner, alternativeIndex, alternativeIndex, entries);
                break;
            case Negation negation:
                DiscoverContent(ruleName, negation.Inner, alternativeIndex, alternativeIndex, entries);
                break;
            case ValidatingPredicate predicate:
                entries.Add(CreateExecutable(predicate.Code, EmbeddedCodeKind.SemanticPredicate, ruleName, alternativeIndex, elementIndex));
                break;
            case GatingPredicate predicate:
                entries.Add(CreateUnsupported(predicate.Code, EmbeddedCodeKind.SemanticPredicate, ruleName, alternativeIndex, elementIndex, EmbeddedCodeUnsupportedReason.UnsupportedEmbeddedCodeKind));
                break;
            case EmbeddedAction action:
                DiscoverParserAction(ruleName, action, alternativeIndex, elementIndex, entries);
                break;
        }
    }

    /// <summary>
    /// Discovers embedded-code entries from an alternation in runtime priority order.
    /// </summary>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="alternation">Alternation to inspect.</param>
    /// <param name="entries">Destination entry list.</param>
    private static void DiscoverAlternation(string ruleName, Alternation alternation, List<EmbeddedCodeRuntimeEntry> entries)
    {
        var ordered = alternation.Alternatives.OrderBy(static alternative => alternative.Priority).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            DiscoverContent(ruleName, ordered[index].Content, index, null, entries);
        }
    }

    /// <summary>
    /// Discovers embedded-code entries from sequence items with zero-based runtime element indexes.
    /// </summary>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="items">Sequence items to inspect.</param>
    /// <param name="alternativeIndex">Current runtime alternative index.</param>
    /// <param name="entries">Destination entry list.</param>
    private static void DiscoverSequence(string ruleName, IReadOnlyList<RuleContent> items, int? alternativeIndex, List<EmbeddedCodeRuntimeEntry> entries)
    {
        for (var index = 0; index < items.Count; index++)
        {
            DiscoverContent(ruleName, items[index], alternativeIndex, index, entries);
        }
    }

    /// <summary>
    /// Discovers entries from direct-left-recursive metadata after the resolver has split base and recursive alternatives.
    /// </summary>
    /// <param name="info">Left-recursive rule metadata.</param>
    /// <param name="entries">Destination entry list.</param>
    private static void DiscoverLeftRecursiveRule(LeftRecursiveRuleInfo info, List<EmbeddedCodeRuntimeEntry> entries)
    {
        var baseAlternatives = info.BaseAlternatives.OrderBy(static alternative => alternative.Priority).ToList();
        for (var index = 0; index < baseAlternatives.Count; index++)
        {
            DiscoverContent(info.Rule.Name, baseAlternatives[index].Content, index, null, entries);
        }

        var recursiveAlternatives = info.RecursiveAlternatives.OrderBy(static alternative => alternative.Priority).ToList();
        for (var index = 0; index < recursiveAlternatives.Count; index++)
        {
            var tailContent = RemoveLeadingSelfReference(info.Rule.Name, recursiveAlternatives[index].Content);
            if (tailContent is not null)
            {
                DiscoverLeftRecursiveTail(info.Rule.Name, tailContent, index, entries);
            }
        }
    }

    /// <summary>
    /// Discovers entries from direct-left-recursive tail content after leading self-reference removal.
    /// </summary>
    /// <param name="ruleName">Owning left-recursive rule name.</param>
    /// <param name="tailContent">Effective tail content used by runtime parsing.</param>
    /// <param name="alternativeIndex">Runtime recursive alternative index.</param>
    /// <param name="entries">Destination entry list.</param>
    private static void DiscoverLeftRecursiveTail(string ruleName, RuleContent tailContent, int alternativeIndex, List<EmbeddedCodeRuntimeEntry> entries)
    {
        if (tailContent is Sequence sequence)
        {
            for (var index = 0; index < sequence.Items.Count; index++)
            {
                if (sequence.Items[index] is RuleRef ruleRef && string.Equals(ruleRef.RuleName, ruleName, StringComparison.Ordinal))
                {
                    continue;
                }

                DiscoverContent(ruleName, sequence.Items[index], alternativeIndex, index, entries);
            }

            return;
        }

        DiscoverContent(ruleName, tailContent, alternativeIndex, alternativeIndex, entries);
    }

    /// <summary>
    /// Discovers and classifies a parser action according to parser runtime hook support.
    /// </summary>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="action">Embedded parser action.</param>
    /// <param name="alternativeIndex">Current runtime alternative index.</param>
    /// <param name="elementIndex">Current runtime element index.</param>
    /// <param name="entries">Destination entry list.</param>
    private static void DiscoverParserAction(string ruleName, EmbeddedAction action, int? alternativeIndex, int? elementIndex, List<EmbeddedCodeRuntimeEntry> entries)
    {
        if (action.Context == ActionContext.Alternative && action.Position == ActionPosition.Inline)
        {
            entries.Add(CreateExecutable(action.RawCode, EmbeddedCodeKind.ParserInlineAction, ruleName, alternativeIndex, elementIndex));
            return;
        }

        var reason = action.Context != ActionContext.Alternative
            ? EmbeddedCodeUnsupportedReason.UnsupportedActionContext
            : EmbeddedCodeUnsupportedReason.UnsupportedActionPosition;
        entries.Add(CreateUnsupported(action.RawCode, EmbeddedCodeKind.ParserInlineAction, ruleName, alternativeIndex, elementIndex, reason));
    }

    /// <summary>
    /// Recursively discovers lexer embedded code and classifies it as unsupported for parser runtime hooks.
    /// </summary>
    /// <param name="ruleName">Owning lexer rule name.</param>
    /// <param name="content">Lexer content to inspect.</param>
    /// <param name="alternativeIndex">Current local alternative index.</param>
    /// <param name="elementIndex">Current local element index.</param>
    /// <param name="entries">Destination entry list.</param>
    private static void DiscoverLexerContent(string ruleName, RuleContent content, int? alternativeIndex, int? elementIndex, List<EmbeddedCodeRuntimeEntry> entries)
    {
        switch (content)
        {
            case Alternation alternation:
                var ordered = alternation.Alternatives.OrderBy(static alternative => alternative.Priority).ToList();
                for (var index = 0; index < ordered.Count; index++)
                {
                    DiscoverLexerContent(ruleName, ordered[index].Content, index, null, entries);
                }
                break;
            case Alternative alternative:
                DiscoverLexerContent(ruleName, alternative.Content, alternativeIndex, elementIndex, entries);
                break;
            case Sequence sequence:
                for (var index = 0; index < sequence.Items.Count; index++)
                {
                    DiscoverLexerContent(ruleName, sequence.Items[index], alternativeIndex, index, entries);
                }
                break;
            case Quantifier quantifier:
                DiscoverLexerContent(ruleName, quantifier.Inner, alternativeIndex, alternativeIndex, entries);
                break;
            case Negation negation:
                DiscoverLexerContent(ruleName, negation.Inner, alternativeIndex, alternativeIndex, entries);
                break;
            case ValidatingPredicate predicate:
                entries.Add(CreateUnsupported(predicate.Code, EmbeddedCodeKind.LexerPredicate, ruleName, alternativeIndex, elementIndex, EmbeddedCodeUnsupportedReason.LexerPredicate));
                break;
            case GatingPredicate predicate:
                entries.Add(CreateUnsupported(predicate.Code, EmbeddedCodeKind.LexerPredicate, ruleName, alternativeIndex, elementIndex, EmbeddedCodeUnsupportedReason.LexerPredicate));
                break;
            case EmbeddedAction action:
                entries.Add(CreateUnsupported(action.RawCode, EmbeddedCodeKind.LexerAction, ruleName, alternativeIndex, elementIndex, EmbeddedCodeUnsupportedReason.LexerAction));
                break;
        }
    }

    /// <summary>
    /// Removes a leading direct self-reference from left-recursive alternatives.
    /// </summary>
    /// <param name="ruleName">Name of the left-recursive rule.</param>
    /// <param name="content">Recursive alternative content.</param>
    /// <returns>The effective tail content, or <c>null</c> when no leading self-reference exists.</returns>
    private static RuleContent? RemoveLeadingSelfReference(string ruleName, RuleContent content)
    {
        switch (content)
        {
            case RuleRef ruleRef when string.Equals(ruleRef.RuleName, ruleName, StringComparison.Ordinal):
                return new Sequence([]);
            case Sequence sequence when sequence.Items.Count > 0:
                return TryRemoveLeadingSelfReference(ruleName, sequence);
            default:
                return null;
        }
    }

    /// <summary>
    /// Removes a leading self-reference from a sequence when present.
    /// </summary>
    /// <param name="ruleName">Name of the left-recursive rule.</param>
    /// <param name="sequence">Sequence to inspect.</param>
    /// <returns>The sequence tail, or <c>null</c> when the sequence does not start with the rule.</returns>
    private static RuleContent? TryRemoveLeadingSelfReference(string ruleName, Sequence sequence)
    {
        if (sequence.Items[0] is RuleRef leading && string.Equals(leading.RuleName, ruleName, StringComparison.Ordinal))
        {
            return new Sequence(sequence.Items.Skip(1).ToList());
        }

        return null;
    }

    /// <summary>
    /// Creates an executable runtime entry.
    /// </summary>
    /// <param name="sourceText">Raw embedded-code source text.</param>
    /// <param name="kind">Embedded-code kind.</param>
    /// <param name="ruleName">Owning parser rule name.</param>
    /// <param name="alternativeIndex">Runtime alternative index.</param>
    /// <param name="elementIndex">Runtime element index.</param>
    /// <returns>An executable runtime discovery entry.</returns>
    private static EmbeddedCodeRuntimeEntry CreateExecutable(string sourceText, EmbeddedCodeKind kind, string ruleName, int? alternativeIndex, int? elementIndex) =>
        new(new EmbeddedCodeSource(sourceText, kind, ruleName, alternativeIndex, elementIndex), true);

    /// <summary>
    /// Creates an unsupported runtime entry.
    /// </summary>
    /// <param name="sourceText">Raw embedded-code source text.</param>
    /// <param name="kind">Embedded-code kind.</param>
    /// <param name="ruleName">Optional owning rule name.</param>
    /// <param name="alternativeIndex">Optional runtime alternative index.</param>
    /// <param name="elementIndex">Optional runtime element index.</param>
    /// <param name="reason">Unsupported reason.</param>
    /// <returns>An unsupported runtime discovery entry.</returns>
    private static EmbeddedCodeRuntimeEntry CreateUnsupported(
        string sourceText,
        EmbeddedCodeKind kind,
        string? ruleName,
        int? alternativeIndex,
        int? elementIndex,
        EmbeddedCodeUnsupportedReason reason) =>
        new(new EmbeddedCodeSource(sourceText, kind, ruleName, alternativeIndex, elementIndex), false, reason);
}
