using Utils.Parser.Model;

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
    public static ParserDefinition Resolve(ParserDefinition definition)
    {
        // 1. Build AllRules, assigning known kinds at registration time.
        var allRules = new Dictionary<string, Rule>();

        foreach (var mode in definition.Modes)
        {
            foreach (var rule in mode.Rules)
            {
                if (!allRules.TryAdd(rule.Name, rule))
                    throw new GrammarValidationException($"Duplicate rule name: {rule.Name}");
                rule.Kind = RuleKind.Lexer;
            }
        }

        foreach (var rule in definition.ParserRules)
        {
            if (!allRules.TryAdd(rule.Name, rule))
                throw new GrammarValidationException($"Duplicate rule name: {rule.Name}");
            rule.Kind = RuleKind.Parser;
        }

        // Update AllRules on the definition.
        definition = definition with { AllRules = allRules };

        // 2. Verify that all RuleRefs point to existing rules.
        foreach (var rule in allRules.Values)
        {
            ValidateRuleRefs(rule.Content, allRules, rule.Name);
        }

        // 2b. Detect lexer recursion cycles (ANTLR-style lexer recursion is invalid).
        ValidateNoLexerCycles(allRules);

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
            ValidateLabels(rule.Content, allRules, rule.Name);
        }

        return definition;
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
    /// Validates that lexer rules do not form reference cycles.
    /// </summary>
    /// <param name="rules">All known rules keyed by name.</param>
    /// <exception cref="GrammarValidationException">
    /// Thrown when a lexer-reference cycle is detected.
    /// </exception>
    private static void ValidateNoLexerCycles(IDictionary<string, Rule> rules)
    {
        var colors = new Dictionary<string, int>(StringComparer.Ordinal);
        var path = new Stack<string>();

        foreach (Rule rule in rules.Values.Where(candidate => candidate.Kind == RuleKind.Lexer))
        {
            if (!colors.ContainsKey(rule.Name))
            {
                VisitLexerRule(rule.Name, rules, colors, path);
            }
        }
    }

    /// <summary>
    /// Performs a DFS visit for lexer-cycle detection using white/gray/black colors.
    /// </summary>
    /// <param name="ruleName">Lexer rule currently visited.</param>
    /// <param name="rules">All known rules keyed by name.</param>
    /// <param name="colors">DFS color map (0=white, 1=gray, 2=black).</param>
    /// <param name="path">Current DFS path.</param>
    private static void VisitLexerRule(
        string ruleName,
        IDictionary<string, Rule> rules,
        IDictionary<string, int> colors,
        Stack<string> path)
    {
        colors[ruleName] = 1;
        path.Push(ruleName);

        var refs = new HashSet<string>(StringComparer.Ordinal);
        CollectRuleRefs(rules[ruleName].Content, refs);

        foreach (string referencedRuleName in refs)
        {
            if (!rules.TryGetValue(referencedRuleName, out Rule? referencedRule) || referencedRule.Kind != RuleKind.Lexer)
            {
                continue;
            }

            if (!colors.TryGetValue(referencedRuleName, out int color))
            {
                VisitLexerRule(referencedRuleName, rules, colors, path);
                continue;
            }

            if (color == 1)
            {
                string[] cyclePath = path.Reverse().TakeWhile(name => name != referencedRuleName).Reverse().Concat(new[] { referencedRuleName }).ToArray();
                string cycle = string.Join(" -> ", cyclePath);
                throw new GrammarValidationException($"Lexer recursion cycle detected: {cycle}");
            }
        }

        path.Pop();
        colors[ruleName] = 2;
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
