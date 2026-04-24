using Utils.Parser.Model;
using Utils.Parser.Diagnostics;

namespace Utils.Parser.Resolution;

/// <summary>
/// Validates a <see cref="ParserDefinition"/> and enriches it with derived information.
/// <para>
/// Resolution is a two-step process:
/// <list type="number">
///   <item>Build the flat <see cref="ParserDefinition.AllRules"/> lookup from all modes
///         and parser-rule lists, assigning <see cref="RuleKind"/> values.</item>
///   <item>Validate all <see cref="RuleRef"/> targets, perform consistency checks
///         (mixed lexer/parser content), verify fragment constraints, and
///         validate labels.</item>
/// </list>
/// </para>
/// </summary>
public static class RuleResolver
{
    /// <summary>
    /// Resolves and validates <paramref name="definition"/>, returning an updated
    /// <see cref="ParserDefinition"/> with a fully populated
    /// <see cref="ParserDefinition.AllRules"/> dictionary.
    /// </summary>
    /// <param name="definition">The grammar definition to resolve.</param>
    /// <returns>The same definition with <see cref="ParserDefinition.AllRules"/> populated.</returns>
    /// <exception cref="GrammarValidationException">
    /// Thrown when duplicate rule names are detected, a rule references an unknown rule,
    /// rule kinds cannot be consistently resolved, or structural constraints are violated.
    /// </exception>
    public static ParserDefinition Resolve(ParserDefinition definition, DiagnosticBag? diagnostics = null)
    {
        // 1. Build AllRules, assigning known kinds at registration time.
        var allRules = new Dictionary<string, Rule>();

        foreach (var mode in definition.Modes)
        {
            foreach (var rule in mode.Rules)
            {
                if (!allRules.TryAdd(rule.Name, rule))
                {
                    diagnostics?.Add(ParserDiagnostics.InternalInconsistency, $"Duplicate rule name: {rule.Name}");
                    throw new GrammarValidationException($"Duplicate rule name: {rule.Name}");
                }
                rule.Kind = RuleKind.Lexer;
            }
        }

        foreach (var rule in definition.ParserRules)
        {
            if (!allRules.TryAdd(rule.Name, rule))
            {
                diagnostics?.Add(ParserDiagnostics.InternalInconsistency, $"Duplicate rule name: {rule.Name}");
                throw new GrammarValidationException($"Duplicate rule name: {rule.Name}");
            }
            rule.Kind = RuleKind.Parser;
        }

        // Update AllRules on the definition.
        definition = definition with { AllRules = allRules };

        // 2. Verify that all RuleRefs point to existing rules.
        foreach (var rule in allRules.Values)
        {
            ValidateRuleRefs(rule.Content, allRules, rule.Name, diagnostics);
        }

        // 3. Infer RuleKind for any rules still marked Unresolved.
        //    Iterative resolution handles circular dependencies.
        bool changed = true;
        int maxIterations = allRules.Count + 1;
        while (changed && maxIterations-- > 0)
        {
            changed = false;
            foreach (var rule in allRules.Values)
            {
                if (rule.Kind != RuleKind.Unresolved)
                    continue;

                var inferred = InferKind(rule.Content, allRules);
                if (inferred != RuleKind.Unresolved)
                {
                    rule.Kind = inferred;
                    changed = true;
                }
            }
        }

        // Apply the ANTLR4 naming convention to any remaining Unresolved rules:
        // upper-case start → lexer; lower-case start → parser.
        foreach (var rule in allRules.Values)
        {
            if (rule.Kind == RuleKind.Unresolved)
            {
                rule.Kind = char.IsUpper(rule.Name[0]) ? RuleKind.Lexer : RuleKind.Parser;
            }
        }

        // 4. Validate that lexer rules do not reference parser content.
        foreach (var rule in allRules.Values)
        {
            ValidateKindConsistency(rule, allRules);
        }

        // 5. Validate that fragment rules are lexer rules.
        foreach (var rule in allRules.Values)
        {
            if (rule.IsFragment && rule.Kind != RuleKind.Lexer)
            {
                throw new GrammarValidationException(
                    $"Fragment rule '{rule.Name}' must be a lexer rule");
            }
        }

        // 6. Validate labels: ensure every label's target rule exists.
        foreach (var rule in allRules.Values)
        {
            ValidateLabels(rule.Content, allRules, rule.Name, diagnostics);
        }

        // 7. Remove strict duplicate alternatives while preserving labels/actions semantics.
        definition = NormalizeAlternatives(definition, diagnostics);

        // 8. Analyze direct left recursion metadata and unsupported indirect cycles.
        var leftRecursiveRules = LeftRecursionAnalyzer.Analyze(definition, diagnostics);
        definition = definition with { LeftRecursiveRules = leftRecursiveRules };

        return definition;
    }

    private static ParserDefinition NormalizeAlternatives(ParserDefinition definition, DiagnosticBag? diagnostics)
    {
        var updatedParserRules = new List<Rule>(definition.ParserRules.Count);
        bool changedAny = false;

        foreach (var rule in definition.ParserRules)
        {
            var grouped = rule.Content.Alternatives
                .GroupBy(BuildAlternativeFingerprint)
                .ToList();

            if (grouped.All(g => g.Count() == 1))
            {
                updatedParserRules.Add(rule);
                continue;
            }

            var normalized = new List<Alternative>();
            foreach (var group in grouped)
            {
                var kept = group.OrderBy(a => a.Priority).First();
                normalized.Add(kept);
                if (group.Count() > 1)
                {
                    diagnostics?.AddWithContext(
                        ParserDiagnostics.StaticDuplicateAlternativeRemoved,
                        null,
                        null,
                        rule.Name,
                        null,
                        rule.Name);
                }
            }

            var updatedRule = rule with
            {
                Content = new Alternation(normalized.OrderBy(a => a.Priority).ToList())
            };
            updatedRule.Kind = rule.Kind;
            updatedParserRules.Add(updatedRule);
            changedAny = true;
        }

        if (!changedAny)
        {
            return definition;
        }

        var updatedAllRules = new Dictionary<string, Rule>(definition.AllRules, StringComparer.Ordinal);
        foreach (var parserRule in updatedParserRules)
        {
            updatedAllRules[parserRule.Name] = parserRule;
        }

        return definition with
        {
            ParserRules = updatedParserRules,
            RootRule = definition.RootRule is null ? null : updatedAllRules[definition.RootRule.Name],
            AllRules = updatedAllRules
        };
    }

    private static string BuildAlternativeFingerprint(Alternative alternative)
    {
        return $"{alternative.Assoc}|{alternative.Label}|{BuildContentFingerprint(alternative.Content)}";
    }

    private static string BuildContentFingerprint(RuleContent content)
    {
        return content switch
        {
            RuleRef r => $"Ref({r.RuleName},{r.Label?.Label},{r.Label?.RuleName},{r.Label?.IsAdditive})",
            LiteralMatch l => $"Lit({l.Value})",
            RangeMatch r => $"Range({r.From},{r.To})",
            CharSetMatch c => $"Set({c.Negated}:{new string(c.Chars.OrderBy(ch => ch).ToArray())})",
            AnyChar => "AnyChar",
            Sequence s => $"Seq[{string.Join(",", s.Items.Select(BuildContentFingerprint))}]",
            Alternation a => $"Alt[{string.Join("|", a.Alternatives.Select(BuildAlternativeFingerprint))}]",
            Alternative a => BuildAlternativeFingerprint(a),
            Quantifier q => $"Quant({q.Min},{q.Max},{q.Greedy}:{BuildContentFingerprint(q.Inner)})",
            Negation n => $"Neg({BuildContentFingerprint(n.Inner)})",
            ValidatingPredicate v => $"ValPred({v.Code})",
            GatingPredicate g => $"GatePred({g.Code})",
            PrecedencePredicate p => $"PrecPred({p.Level})",
            EmbeddedAction action => $"Action({action.Context},{action.Position},{action.RawCode})",
            LexerCommand cmd => $"Cmd({cmd.Type},{cmd.Argument})",
            ModeSwitch mode => $"Mode({mode.ModeName},{mode.Push})",
            _ => content.GetType().Name
        };
    }

    /// <summary>
    /// Recursively validates that every <see cref="RuleRef"/> in <paramref name="content"/>
    /// names a rule that exists in <paramref name="rules"/>.
    /// </summary>
    /// <param name="content">Grammar element to inspect.</param>
    /// <param name="rules">All known rules, keyed by name.</param>
    /// <param name="contextRuleName">Owning rule name, used in exception messages.</param>
    private static void ValidateRuleRefs(
        RuleContent content,
        IDictionary<string, Rule> rules,
        string contextRuleName,
        DiagnosticBag? diagnostics)
    {
        switch (content)
        {
            case RuleRef r:
                if (!rules.ContainsKey(r.RuleName))
                {
                    diagnostics?.AddWithContext(ParserDiagnostics.UnknownRuleReference, null, null, contextRuleName, null, contextRuleName, r.RuleName);
                    throw new GrammarValidationException(
                        $"Rule '{contextRuleName}' references unknown rule '{r.RuleName}'");
                }
                break;
            case Sequence s:
                foreach (var item in s.Items)
                    ValidateRuleRefs(item, rules, contextRuleName, diagnostics);
                break;
            case Alternation a:
                foreach (var alt in a.Alternatives)
                    ValidateRuleRefs(alt.Content, rules, contextRuleName, diagnostics);
                break;
            case Alternative alt:
                ValidateRuleRefs(alt.Content, rules, contextRuleName, diagnostics);
                break;
            case Quantifier q:
                ValidateRuleRefs(q.Inner, rules, contextRuleName, diagnostics);
                break;
            case Negation n:
                ValidateRuleRefs(n.Inner, rules, contextRuleName, diagnostics);
                break;
        }
    }

    /// <summary>
    /// Recursively validates that every labeled <see cref="RuleRef"/> has a label
    /// whose target rule name exists in <paramref name="rules"/>.
    /// </summary>
    /// <param name="content">Grammar element to inspect.</param>
    /// <param name="rules">All known rules, keyed by name.</param>
    /// <param name="contextRuleName">Owning rule name, used in exception messages.</param>
    private static void ValidateLabels(
        RuleContent content,
        IDictionary<string, Rule> rules,
        string contextRuleName,
        DiagnosticBag? diagnostics)
    {
        switch (content)
        {
            case RuleRef r when r.Label is not null:
                if (!rules.ContainsKey(r.Label.RuleName))
                {
                    diagnostics?.AddWithContext(ParserDiagnostics.UnknownRuleReference, null, null, contextRuleName, null, contextRuleName, r.Label.RuleName);
                    throw new GrammarValidationException(
                        $"Rule '{contextRuleName}' has label '{r.Label.Label}' " +
                        $"referencing unknown rule '{r.Label.RuleName}'");
                }
                break;
            case Sequence s:
                foreach (var item in s.Items)
                    ValidateLabels(item, rules, contextRuleName, diagnostics);
                break;
            case Alternation a:
                foreach (var alt in a.Alternatives)
                    ValidateLabels(alt.Content, rules, contextRuleName, diagnostics);
                break;
            case Alternative alt:
                ValidateLabels(alt.Content, rules, contextRuleName, diagnostics);
                break;
            case Quantifier q:
                ValidateLabels(q.Inner, rules, contextRuleName, diagnostics);
                break;
            case Negation n:
                ValidateLabels(n.Inner, rules, contextRuleName, diagnostics);
                break;
        }
    }

    /// <summary>
    /// Ensures that a lexer rule does not reference parser content.
    /// Parser rules in combined grammars may legitimately reference both lexer tokens
    /// and inline literals, so only lexer rules are checked.
    /// </summary>
    /// <param name="rule">Rule to validate.</param>
    /// <param name="rules">All known rules, used to resolve references.</param>
    private static void ValidateKindConsistency(Rule rule, IDictionary<string, Rule> rules)
    {
        // Only lexer rules are checked; parser rules may legitimately mix references.
        if (rule.Kind != RuleKind.Lexer)
            return;

        try
        {
            var inferred = InferKind(rule.Content, rules);
            if (inferred == RuleKind.Parser)
            {
                throw new GrammarValidationException(
                    $"Lexer rule '{rule.Name}' references parser content");
            }
        }
        catch (GrammarValidationException)
        {
            throw new GrammarValidationException(
                $"Lexer rule '{rule.Name}' mixes lexer and parser content");
        }
    }

    /// <summary>
    /// Collects all <see cref="RuleRef"/> names referenced (directly or indirectly)
    /// within <paramref name="content"/> into <paramref name="refs"/>.
    /// </summary>
    /// <param name="content">Grammar element to traverse.</param>
    /// <param name="refs">Set that receives the referenced rule names.</param>
    private static void CollectRuleRefs(RuleContent content, ISet<string> refs)
    {
        switch (content)
        {
            case RuleRef r:
                refs.Add(r.RuleName);
                break;
            case Sequence s:
                foreach (var item in s.Items)
                    CollectRuleRefs(item, refs);
                break;
            case Alternation a:
                foreach (var alt in a.Alternatives)
                    CollectRuleRefs(alt.Content, refs);
                break;
            case Alternative alt:
                CollectRuleRefs(alt.Content, refs);
                break;
            case Quantifier q:
                CollectRuleRefs(q.Inner, refs);
                break;
            case Negation n:
                CollectRuleRefs(n.Inner, refs);
                break;
        }
    }

    /// <summary>
    /// Infers the <see cref="RuleKind"/> of a grammar element based on the kinds of
    /// its constituent parts.
    /// Returns <see cref="RuleKind.Unresolved"/> when not enough information is
    /// available yet (e.g. a referenced rule is still unresolved).
    /// </summary>
    /// <param name="content">Grammar element to analyse.</param>
    /// <param name="rules">All known rules, used to look up reference kinds.</param>
    /// <returns>The inferred kind, or <see cref="RuleKind.Unresolved"/>.</returns>
    /// <exception cref="GrammarValidationException">
    /// Thrown when a rule element mixes lexer and parser content.
    /// </exception>
    internal static RuleKind InferKind(RuleContent content, IDictionary<string, Rule> rules)
        => content switch
        {
            TokenizerContent          => RuleKind.Lexer,
            ModeSwitch                => RuleKind.Lexer,
            LexerCommand              => RuleKind.Lexer,
            RuleRef r                 => rules.TryGetValue(r.RuleName, out var rule)
                                         ? rule.Kind : RuleKind.Unresolved,
            Sequence s                => ResolveUniform(s.Items.Select(i => InferKind(i, rules))),
            Alternation a             => ResolveUniform(a.Alternatives.Select(alt => InferKind(alt.Content, rules))),
            Quantifier q              => InferKind(q.Inner, rules),
            Negation n                => InferKind(n.Inner, rules),
            Alternative alt           => InferKind(alt.Content, rules),
            ValidatingPredicate       => RuleKind.Parser,
            PrecedencePredicate       => RuleKind.Parser,
            GatingPredicate           => RuleKind.Parser,
            EmbeddedAction            => RuleKind.Unresolved, // Neutral; inherits from context.
            _ => throw new GrammarValidationException($"Unknown RuleContent: {content.GetType()}")
        };

    /// <summary>
    /// Returns the single non-<see cref="RuleKind.Unresolved"/> kind present in
    /// <paramref name="kinds"/>, or <see cref="RuleKind.Unresolved"/> when all are unresolved.
    /// Throws when both <see cref="RuleKind.Lexer"/> and <see cref="RuleKind.Parser"/> appear.
    /// </summary>
    /// <param name="kinds">Collection of inferred kinds to reconcile.</param>
    private static RuleKind ResolveUniform(IEnumerable<RuleKind> kinds)
    {
        var set = kinds.Where(k => k != RuleKind.Unresolved).ToHashSet();
        if (set.Count == 0) return RuleKind.Unresolved;
        if (set.Count == 1) return set.Single();
        throw new GrammarValidationException("Rule mixes lexer and parser content");
    }
}
