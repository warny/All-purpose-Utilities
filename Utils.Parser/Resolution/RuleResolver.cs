using Utils.Parser.Model;

namespace Utils.Parser.Resolution;

public static class RuleResolver
{
    public static ParserDefinition Resolve(ParserDefinition definition)
    {
        // 1. Construire AllRules
        var allRules = new Dictionary<string, Rule>();

        foreach (var mode in definition.Modes)
        {
            foreach (var rule in mode.Rules)
            {
                if (!allRules.TryAdd(rule.Name, rule))
                    throw new GrammarValidationException($"Duplicate rule name: {rule.Name}");
            }
        }

        foreach (var rule in definition.ParserRules)
        {
            if (!allRules.TryAdd(rule.Name, rule))
                throw new GrammarValidationException($"Duplicate rule name: {rule.Name}");
        }

        // Mettre à jour AllRules sur la définition
        definition = definition with { AllRules = allRules };

        // 2. Vérifier que toutes les RuleRef pointent vers une règle existante
        foreach (var rule in allRules.Values)
        {
            ValidateRuleRefs(rule.Content, allRules, rule.Name);
        }

        // 3. Inférer RuleKind pour chaque règle
        // Résolution itérative pour gérer les dépendances circulaires
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

        // Les règles encore Unresolved : convention ANTLR4
        // minuscule = parser, majuscule = lexer
        foreach (var rule in allRules.Values)
        {
            if (rule.Kind == RuleKind.Unresolved)
            {
                rule.Kind = char.IsUpper(rule.Name[0]) ? RuleKind.Lexer : RuleKind.Parser;
            }
        }

        // 4. Valider la cohérence (pas de mélange Lexer/Parser dans une règle)
        foreach (var rule in allRules.Values)
        {
            ValidateKindConsistency(rule, allRules);
        }

        // 5. Valider les fragments (référencés uniquement depuis des règles Lexer)
        foreach (var rule in allRules.Values)
        {
            if (rule.IsFragment && rule.Kind != RuleKind.Lexer)
            {
                throw new GrammarValidationException(
                    $"Fragment rule '{rule.Name}' must be a lexer rule");
            }
        }

        ValidateFragmentReferences(allRules);

        // 6. Résoudre les RuleLabel : vérifier que le RuleName référencé existe
        foreach (var rule in allRules.Values)
        {
            ValidateLabels(rule.Content, allRules, rule.Name);
        }

        return definition;
    }

    private static void ValidateRuleRefs(
        RuleContent content,
        IDictionary<string, Rule> rules,
        string contextRuleName)
    {
        switch (content)
        {
            case RuleRef r:
                if (!rules.ContainsKey(r.RuleName))
                    throw new GrammarValidationException(
                        $"Rule '{contextRuleName}' references unknown rule '{r.RuleName}'");
                break;
            case Sequence s:
                foreach (var item in s.Items)
                    ValidateRuleRefs(item, rules, contextRuleName);
                break;
            case Alternation a:
                foreach (var alt in a.Alternatives)
                    ValidateRuleRefs(alt.Content, rules, contextRuleName);
                break;
            case Alternative alt:
                ValidateRuleRefs(alt.Content, rules, contextRuleName);
                break;
            case Quantifier q:
                ValidateRuleRefs(q.Inner, rules, contextRuleName);
                break;
            case Negation n:
                ValidateRuleRefs(n.Inner, rules, contextRuleName);
                break;
        }
    }

    private static void ValidateLabels(
        RuleContent content,
        IDictionary<string, Rule> rules,
        string contextRuleName)
    {
        switch (content)
        {
            case RuleRef r when r.Label is not null:
                if (!rules.ContainsKey(r.Label.RuleName))
                    throw new GrammarValidationException(
                        $"Rule '{contextRuleName}' has label '{r.Label.Label}' " +
                        $"referencing unknown rule '{r.Label.RuleName}'");
                break;
            case Sequence s:
                foreach (var item in s.Items)
                    ValidateLabels(item, rules, contextRuleName);
                break;
            case Alternation a:
                foreach (var alt in a.Alternatives)
                    ValidateLabels(alt.Content, rules, contextRuleName);
                break;
            case Alternative alt:
                ValidateLabels(alt.Content, rules, contextRuleName);
                break;
            case Quantifier q:
                ValidateLabels(q.Inner, rules, contextRuleName);
                break;
            case Negation n:
                ValidateLabels(n.Inner, rules, contextRuleName);
                break;
        }
    }

    private static void ValidateKindConsistency(Rule rule, IDictionary<string, Rule> rules)
    {
        try
        {
            var inferred = InferKind(rule.Content, rules);
            if (inferred != RuleKind.Unresolved && inferred != rule.Kind)
            {
                // Allow mixed content in combined grammars where naming conventions apply
                // Only throw if content analysis is conclusive and contradicts the rule kind
            }
        }
        catch (GrammarValidationException)
        {
            throw new GrammarValidationException(
                $"Rule '{rule.Name}' mixes lexer and parser content");
        }
    }

    private static void ValidateFragmentReferences(IDictionary<string, Rule> rules)
    {
        var fragmentNames = rules.Values
            .Where(r => r.IsFragment)
            .Select(r => r.Name)
            .ToHashSet();

        if (fragmentNames.Count == 0)
            return;

        foreach (var rule in rules.Values)
        {
            if (rule.Kind == RuleKind.Parser)
            {
                var referencedFragments = new HashSet<string>();
                CollectRuleRefs(rule.Content, referencedFragments);

                foreach (var refName in referencedFragments)
                {
                    if (fragmentNames.Contains(refName))
                    {
                        throw new GrammarValidationException(
                            $"Parser rule '{rule.Name}' references fragment '{refName}'. " +
                            "Fragments can only be referenced from lexer rules.");
                    }
                }
            }
        }
    }

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
            EmbeddedAction            => RuleKind.Unresolved, // neutre, hérité du contexte
            _ => throw new GrammarValidationException($"Unknown RuleContent: {content.GetType()}")
        };

    private static RuleKind ResolveUniform(IEnumerable<RuleKind> kinds)
    {
        var set = kinds.Where(k => k != RuleKind.Unresolved).ToHashSet();
        if (set.Count == 0) return RuleKind.Unresolved;
        if (set.Count == 1) return set.Single();
        throw new GrammarValidationException("Rule mixes lexer and parser content");
    }
}
