using Utils.Parser.Model;
using Utils.Parser.Diagnostics;

namespace Utils.Parser.Runtime;

/// <summary>
/// Key used for O(1) detection of left-recursive cycles in the parser engine.
/// A cycle is detected when the same rule is re-entered at the same token-list position.
/// </summary>
internal readonly record struct ParserFrameKey(string RuleName, int InputPosition);
internal readonly record struct ParseMemoKey(string RuleName, int InputPosition, int MinimumPrecedence);

internal sealed record ParseMemoEntry
{
    public required ParseNode? Node { get; init; }

    public required int EndPosition { get; init; }

    public required bool IsFailure { get; init; }
}

internal sealed record RuleContentCursor
{
    public required int Index { get; init; }

    public required string Kind { get; init; }
}

internal sealed record ParseBranch
{
    public required Rule Rule { get; init; }

    public required Alternative Alternative { get; init; }

    public required int InputPosition { get; init; }

    public required RuleContentCursor Cursor { get; init; }

    public required ParseNode PartialNode { get; init; }

    public required int EndPosition { get; init; }

    public bool IsComplete { get; init; }
}

internal sealed record BranchKey
{
    public required string RuleName { get; init; }

    public required int InputPosition { get; init; }

    public required string CursorKey { get; init; }

    public required string ParentContextKey { get; init; }
}

/// <summary>
/// Builds a parse tree from a flat token list using the rules in a
/// <see cref="ParserDefinition"/>.
/// <para>
/// The engine is a recursive-descent parser with full backtracking.
/// It tries each alternative in priority order and rolls back the token position on
/// failure. Left-recursive rules are handled by a cycle-detection stack that returns
/// <c>null</c> when the same rule is re-entered at the same token position.
/// </para>
/// <para>
/// Semantic predicates (<see cref="ValidatingPredicate"/>, <see cref="GatingPredicate"/>)
/// and embedded actions (<see cref="EmbeddedAction"/>) are silently accepted without
/// execution; they have no effect on the parse tree shape.
/// </para>
/// </summary>
public sealed class ParserEngine(ParserDefinition definition)
{
    private readonly HashSet<ParserFrameKey> _activeRuleFrames = new();
    private readonly Dictionary<ParseMemoKey, ParseMemoEntry> _memo = new();
    private readonly bool _caseInsensitive = IsCaseInsensitive(definition);

    /// <summary>
    /// Parses <paramref name="tokens"/> starting from <paramref name="startRule"/>
    /// (or the definition's root rule when <c>null</c>) and returns the root
    /// <see cref="ParseNode"/>.
    /// If parsing fails completely an <see cref="ErrorNode"/> is returned rather than
    /// throwing an exception.
    /// </summary>
    /// <param name="tokens">Flat list of tokens produced by <see cref="LexerEngine"/>.</param>
    /// <param name="startRule">Override for the start rule, or <c>null</c> to use the definition's root.</param>
    /// <returns>Root parse-tree node.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no start rule is available (neither <paramref name="startRule"/>
    /// nor <see cref="ParserDefinition.RootRule"/> is set).
    /// </exception>
    public ParseNode Parse(IEnumerable<Token> tokens, Rule? startRule = null, DiagnosticBag? diagnostics = null)
    {
        var tokenList = tokens.ToList();
        var root = startRule ?? definition.RootRule
            ?? throw new InvalidOperationException("No root rule defined");

        _activeRuleFrames.Clear();
        _memo.Clear();
        var context = new ParseContext(tokenList);
        var result = ParseRule(context, root, precedence: 0, diagnostics);

        if (result is null)
        {
            diagnostics?.Add(ParserDiagnostics.ParseFailure, "Failed to parse from root rule");
            return new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE",
                "Failed to parse from root rule", root);
        }

        // Reject parses that leave trailing tokens unconsumed.
        if (!context.IsEnd)
        {
            var trailing = context.Peek()!;
            diagnostics?.AddWithContext(
                ParserDiagnostics.TrailingTokensAfterParse,
                trailing.Span.Position,
                trailing.Span.Length,
                null,
                null,
                trailing.Text);
            return new ErrorNode(result.Span, result.ModeName,
                $"Unexpected token '{trailing.Text}' at position {trailing.Span.Position}", root);
        }

        diagnostics?.Add(ParserDiagnostics.DefaultBehaviorApplied, "Parser completed without recovery.");
        return result;
    }

    /// <summary>
    /// Attempts to match <paramref name="rule"/> at the current context position.
    /// Tries each alternative in ascending priority order and returns the first match,
    /// or <c>null</c> when no alternative matches.
    /// Returns <c>null</c> immediately when a left-recursive cycle is detected
    /// (same rule re-entered at the same token position).
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="rule">Parser rule to attempt.</param>
    /// <param name="precedence">Minimum precedence level accepted for this parse attempt.</param>
    private ParseNode? ParseRule(ParseContext context, Rule rule, int precedence, DiagnosticBag? diagnostics = null)
    {
        diagnostics?.AddWithContext(ParserDiagnostics.EnteringRule, null, null, rule.Name, null, rule.Name);
        var memoKey = new ParseMemoKey(rule.Name, context.Position, precedence);
        if (_memo.TryGetValue(memoKey, out var memoEntry))
        {
            diagnostics?.AddWithContext(ParserDiagnostics.ParseMemoHit, null, null, rule.Name, null, rule.Name);
            context.RestorePosition(memoEntry.EndPosition);
            return memoEntry.IsFailure ? null : memoEntry.Node;
        }

        diagnostics?.AddWithContext(ParserDiagnostics.ParseMemoMiss, null, null, rule.Name, null, rule.Name);
        var initialPosition = context.Position;
        var frameKey = new ParserFrameKey(rule.Name, context.Position);
        if (!_activeRuleFrames.Add(frameKey))
        {
            return null;
        }

        try
        {
            ParseNode? parsed;
            if (definition.LeftRecursiveRules.TryGetValue(rule.Name, out var leftRecursiveInfo))
            {
                diagnostics?.AddWithContext(
                    ParserDiagnostics.LeftRecursivePrecedencePartiallySupported,
                    null,
                    null,
                    rule.Name,
                    null,
                    rule.Name);
                parsed = ParseLeftRecursiveRule(context, leftRecursiveInfo, precedence, diagnostics);
            }
            else
            {
                parsed = TryParseAlternativesParallel(context, rule.Content.Alternatives, rule, precedence, diagnostics);
            }

            _memo[memoKey] = new ParseMemoEntry
            {
                Node = parsed,
                EndPosition = context.Position,
                IsFailure = parsed is null
            };

            return parsed;
        }
        finally
        {
            _activeRuleFrames.Remove(frameKey);
            if (context.Position < initialPosition)
            {
                context.RestorePosition(initialPosition);
            }
            diagnostics?.AddWithContext(ParserDiagnostics.LeavingRule, null, null, rule.Name, null, rule.Name);
        }
    }

    private ParseNode? ParseLeftRecursiveRule(
        ParseContext context,
        LeftRecursiveRuleInfo info,
        int minimumPrecedence,
        DiagnosticBag? diagnostics)
    {
        var seed = TryParseAlternativesParallel(
            context,
            info.BaseAlternatives,
            info.Rule,
            minimumPrecedence,
            diagnostics);
        if (seed is null)
        {
            return null;
        }

        var current = seed;
        while (true)
        {
            var extension = TryExtendLeft(context, info, current, minimumPrecedence, diagnostics);
            if (extension is null)
            {
                break;
            }

            current = extension;
        }

        return current;
    }

    private ParseNode? TryExtendLeft(
        ParseContext context,
        LeftRecursiveRuleInfo info,
        ParseNode current,
        int minimumPrecedence,
        DiagnosticBag? diagnostics)
    {
        var startPosition = context.Position;
        ParseBranch? bestBranch = null;
        var recursiveAlternatives = info.RecursiveAlternatives.OrderBy(a => a.Priority).ToList();

        for (int index = 0; index < recursiveAlternatives.Count; index++)
        {
            var alternative = recursiveAlternatives[index];
            var precedenceLevel = recursiveAlternatives.Count - index;
            if (precedenceLevel < minimumPrecedence || !CheckPrecedence(alternative, minimumPrecedence))
            {
                continue;
            }

            var saved = context.SavePosition();
            var candidate = TryParseRecursiveAlternative(
                context,
                info.Rule,
                alternative,
                current,
                precedenceLevel,
                diagnostics);
            if (candidate is null)
            {
                context.RestorePosition(saved);
                continue;
            }

            var branch = new ParseBranch
            {
                Rule = info.Rule,
                Alternative = alternative,
                InputPosition = startPosition,
                Cursor = new RuleContentCursor { Index = 0, Kind = "recursive-extension" },
                PartialNode = candidate,
                EndPosition = context.Position,
                IsComplete = true
            };

            if (bestBranch is null || IsBetterBranch(branch, bestBranch))
            {
                if (bestBranch is not null)
                {
                    diagnostics?.AddWithContext(ParserDiagnostics.ParseBranchPruned, null, null, info.Rule.Name, null, info.Rule.Name);
                }

                bestBranch = branch;
            }
            else
            {
                diagnostics?.AddWithContext(ParserDiagnostics.ParseBranchPruned, null, null, info.Rule.Name, null, info.Rule.Name);
            }

            context.RestorePosition(saved);
        }

        if (bestBranch is null)
        {
            context.RestorePosition(startPosition);
            return null;
        }

        context.RestorePosition(bestBranch.EndPosition);
        return bestBranch.PartialNode;
    }

    private ParseNode? TryParseRecursiveAlternative(
        ParseContext context,
        Rule ownerRule,
        Alternative alternative,
        ParseNode leftSeed,
        int precedenceLevel,
        DiagnosticBag? diagnostics)
    {
        var children = new List<ParseNode> { leftSeed };

        var tailContent = RemoveLeadingSelfReference(ownerRule.Name, alternative.Content);
        if (tailContent is null)
        {
            return null;
        }

        var tailNodes = TryParseLeftRecursiveTail(
            context,
            tailContent,
            ownerRule,
            alternative.Assoc,
            precedenceLevel,
            diagnostics);
        if (tailNodes is null)
        {
            return null;
        }

        children.AddRange(tailNodes);
        var endToken = context.Peek(-1);
        var end = endToken is null ? leftSeed.Span.Position + leftSeed.Span.Length : endToken.Span.Position + endToken.Span.Length;
        var span = new SourceSpan(leftSeed.Span.Position, Math.Max(0, end - leftSeed.Span.Position));
        return new ParserNode(span, leftSeed.ModeName, ownerRule, children);
    }

    /// <summary>
    /// Returns <c>true</c> when the alternative either has no precedence predicate
    /// or its predicate level satisfies <paramref name="currentPrecedence"/>.
    /// </summary>
    /// <param name="alt">Alternative to check.</param>
    /// <param name="currentPrecedence">Minimum acceptable precedence level.</param>
    private bool CheckPrecedence(Alternative alt, int currentPrecedence)
    {
        var predLevel = FindPrecedenceLevel(alt.Content);
        if (predLevel is null)
            return true; // No predicate — always accepted.

        return predLevel.Value >= currentPrecedence;
    }

    /// <summary>
    /// Searches <paramref name="content"/> for the first <see cref="PrecedencePredicate"/>
    /// and returns its level, or <c>null</c> when none is found.
    /// Only the first level of sequences is examined (matching ANTLR4 semantics).
    /// </summary>
    /// <param name="content">Grammar element to inspect.</param>
    private int? FindPrecedenceLevel(RuleContent content)
    {
        switch (content)
        {
            case PrecedencePredicate pp:
                return pp.Level;
            case Sequence seq:
                foreach (var item in seq.Items)
                {
                    var level = FindPrecedenceLevel(item);
                    if (level is not null) return level;
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Delegates to <see cref="TryParseContent"/> for the content of a single alternative.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="alt">Alternative to attempt.</param>
    /// <param name="rule">Parent rule (carried to child nodes).</param>
    private ParseNode? TryParseAlternative(ParseContext context, Alternative alt, Rule rule, DiagnosticBag? diagnostics = null)
    {
        return TryParseContent(context, alt.Content, rule, diagnostics);
    }

    /// <summary>
    /// Dispatches to the appropriate handler based on the concrete type of
    /// <paramref name="content"/>.
    /// Predicates and embedded actions are silently treated as empty successful matches.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="content">Grammar element to match.</param>
    /// <param name="rule">Rule in whose context the element is being matched.</param>
    private ParseNode? TryParseContent(ParseContext context, RuleContent content, Rule rule, DiagnosticBag? diagnostics = null)
    {
        switch (content)
        {
            case RuleRef ruleRef:
                return TryParseRuleRef(context, ruleRef, rule, diagnostics);

            case Sequence seq:
                return TryParseSequence(context, seq, rule, diagnostics);

            case Alternation alternation:
                return TryParseAlternation(context, alternation, rule, diagnostics);

            case Alternative alt:
                return TryParseAlternative(context, alt, rule, diagnostics);

            case Quantifier quant:
                return TryParseQuantifier(context, quant, rule, diagnostics);

            case LiteralMatch lit:
                return TryParseLiteral(context, lit, rule);

            case Negation neg:
                return TryParseNegation(context, neg, rule, diagnostics);

            case ValidatingPredicate:
            case GatingPredicate:
                // Semantic predicates: silently accepted without evaluation.
                diagnostics?.AddWithContext(ParserDiagnostics.SemanticPredicateNotEnforced, null, null, rule.Name, null);
                return CreateEmptyNode(context, rule);

            case PrecedencePredicate:
                // Already handled in CheckPrecedence.
                return CreateEmptyNode(context, rule);

            case EmbeddedAction:
                // Embedded actions: never executed, always succeed.
                diagnostics?.AddWithContext(ParserDiagnostics.InlineActionStoredNotExecuted, null, null, rule.Name, null);
                return CreateEmptyNode(context, rule);

            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves a rule reference, either consuming a matching token (for lexer rules)
    /// or recursing into the referenced parser rule.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="ruleRef">Reference to resolve.</param>
    /// <param name="parentRule">The rule in which this reference appears.</param>
    private ParseNode? TryParseRuleRef(ParseContext context, RuleRef ruleRef, Rule parentRule, DiagnosticBag? diagnostics = null)
    {
        if (!definition.AllRules.TryGetValue(ruleRef.RuleName, out var referencedRule))
            return null;

        if (referencedRule.Kind == RuleKind.Lexer)
        {
            // Direct token match: the next token must have been produced by this lexer rule.
            var token = context.Peek();
            if (token is null) return null;

            if (token.RuleName == ruleRef.RuleName)
            {
                context.Consume();
                diagnostics?.AddWithContext(
                    ParserDiagnostics.TokenMatched,
                    token.Span.Position,
                    token.Span.Length,
                    ruleRef.RuleName,
                    null,
                    ruleRef.RuleName,
                    token.Text);
                return new LexerNode(token.Span, token.ModeName, referencedRule, token);
            }
            return null;
        }

        // Parser rule: recurse.
        return ParseRule(context, referencedRule, precedence: 0, diagnostics);
    }

    /// <summary>
    /// Attempts to match every item in a sequence in order.
    /// Returns a <see cref="ParserNode"/> containing all non-empty child nodes on success,
    /// or <c>null</c> if any item fails to match (the cursor is not restored here;
    /// callers must save/restore the position).
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="seq">Sequence to match.</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private ParseNode? TryParseSequence(ParseContext context, Sequence seq, Rule rule, DiagnosticBag? diagnostics = null)
    {
        var children = new List<ParseNode>();
        var startPos = context.Position;
        var startToken = context.Peek();

        foreach (var item in seq.Items)
        {
            if (item is EmbeddedAction or Model.LexerCommand)
                continue;

            var node = TryParseContent(context, item, rule, diagnostics);
            if (node is null)
                return null;

            // Omit empty nodes (predicates, actions) from the child list.
            if (node.Span.Length > 0 || node is ParserNode { Children.Count: > 0 })
                children.Add(node);
        }

        var span = ComputeSpan(startToken, context, startPos);
        return new ParserNode(span, startToken?.ModeName ?? "DEFAULT_MODE", rule, children);
    }

    /// <summary>
    /// Tries each alternative of an <see cref="Alternation"/> in priority order,
    /// saving and restoring the cursor between attempts.
    /// Returns the first successful match, or <c>null</c> when all alternatives fail.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="alternation">Alternation to evaluate.</param>
    /// <param name="rule">Owning rule (carried to child matches).</param>
    private ParseNode? TryParseAlternation(ParseContext context, Alternation alternation, Rule rule, DiagnosticBag? diagnostics = null)
    {
        return TryParseAlternativesParallel(context, alternation.Alternatives, rule, precedence: 0, diagnostics);
    }

    private ParseNode? TryParseAlternativesParallel(
        ParseContext context,
        IEnumerable<Alternative> alternatives,
        Rule rule,
        int precedence,
        DiagnosticBag? diagnostics)
    {
        var alternativeList = alternatives.OrderBy(a => a.Priority).ToList();
        var startPosition = context.Position;
        var startToken = context.Peek();
        var survivingBranches = new List<ParseBranch>();

        foreach (var alt in alternativeList)
        {
            if (!CheckPrecedence(alt, precedence))
            {
                continue;
            }

            var savedPos = context.SavePosition();
            var result = TryParseContent(context, alt.Content, rule, diagnostics);
            if (result is null)
            {
                diagnostics?.AddWithContext(ParserDiagnostics.BacktrackingUsed, null, null, rule.Name, null, rule.Name);
                context.RestorePosition(savedPos);
                continue;
            }

            survivingBranches.Add(new ParseBranch
            {
                Rule = rule,
                Alternative = alt,
                InputPosition = startPosition,
                Cursor = new RuleContentCursor { Index = 0, Kind = "alternative-root" },
                PartialNode = result,
                EndPosition = context.Position,
                IsComplete = true
            });
            context.RestorePosition(savedPos);
        }

        if (survivingBranches.Count == 0)
        {
            return null;
        }

        var pruned = PruneEquivalentBranches(survivingBranches, diagnostics);
        var winner = pruned[0];
        for (int i = 1; i < pruned.Count; i++)
        {
            if (IsBetterBranch(pruned[i], winner))
            {
                winner = pruned[i];
            }
        }

        context.RestorePosition(winner.EndPosition);
        if (winner.PartialNode is ParserNode parserNode && ReferenceEquals(parserNode.Rule, rule))
        {
            return parserNode;
        }

        var span = ComputeSpan(startToken, context, startPosition);
        return new ParserNode(span, winner.PartialNode.ModeName, rule, [winner.PartialNode]);
    }

    private static bool IsBetterBranch(ParseBranch candidate, ParseBranch current)
    {
        if (candidate.EndPosition != current.EndPosition)
        {
            return candidate.EndPosition > current.EndPosition;
        }

        return candidate.Alternative.Priority < current.Alternative.Priority;
    }

    private List<ParseBranch> PruneEquivalentBranches(
        IReadOnlyList<ParseBranch> branches,
        DiagnosticBag? diagnostics)
    {
        var map = new Dictionary<BranchKey, ParseBranch>();
        foreach (var branch in branches)
        {
            var key = new BranchKey
            {
                RuleName = branch.Rule.Name,
                InputPosition = branch.InputPosition,
                CursorKey = $"{branch.Cursor.Kind}:{branch.Cursor.Index}:{branch.EndPosition}",
                ParentContextKey = branch.Alternative.Label ?? string.Empty
            };

            if (!map.TryGetValue(key, out var existing))
            {
                map[key] = branch;
                continue;
            }

            if (HasDistinctSemantics(existing.Alternative, branch.Alternative))
            {
                continue;
            }

            if (branch.Alternative.Priority < existing.Alternative.Priority)
            {
                map[key] = branch;
            }

            diagnostics?.AddWithContext(ParserDiagnostics.AmbiguousAlternativesPruned, null, null, branch.Rule.Name, null, branch.Rule.Name);
        }

        return map.Values.OrderBy(b => b.Alternative.Priority).ToList();
    }

    private static bool HasDistinctSemantics(Alternative left, Alternative right)
    {
        return !string.Equals(left.Label, right.Label, StringComparison.Ordinal)
            || left.Assoc != right.Assoc
            || ContainsPredicateOrAction(left.Content)
            || ContainsPredicateOrAction(right.Content);
    }

    private static bool ContainsPredicateOrAction(RuleContent content)
    {
        return content switch
        {
            ValidatingPredicate or GatingPredicate or EmbeddedAction => true,
            Sequence seq => seq.Items.Any(ContainsPredicateOrAction),
            Alternation alt => alt.Alternatives.Any(a => ContainsPredicateOrAction(a.Content)),
            Quantifier q => ContainsPredicateOrAction(q.Inner),
            Negation n => ContainsPredicateOrAction(n.Inner),
            Alternative a => ContainsPredicateOrAction(a.Content),
            _ => false
        };
    }

    /// <summary>
    /// Matches a quantified element as many times as allowed by its bounds.
    /// Returns a <see cref="ParserNode"/> when the minimum repeat count is satisfied,
    /// or <c>null</c> otherwise.
    /// Zero-length matches are detected and break the loop to prevent infinite recursion.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="quant">Quantifier to evaluate.</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private ParseNode? TryParseQuantifier(ParseContext context, Quantifier quant, Rule rule, DiagnosticBag? diagnostics = null)
    {
        var children = new List<ParseNode>();
        var startPos = context.Position;
        var startToken = context.Peek();

        int count = 0;
        while (quant.Max is null || count < quant.Max.Value)
        {
            var savedPos = context.SavePosition();
            var node = TryParseContent(context, quant.Inner, rule, diagnostics);
            if (node is null)
            {
                context.RestorePosition(savedPos);
                break;
            }

            // Guard against zero-length matches.
            if (context.Position == savedPos)
                break;

            children.Add(node);
            count++;

            if (!quant.Greedy && count >= quant.Min)
                break;
        }

        if (count < quant.Min)
            return null;

        var span = ComputeSpan(startToken, context, startPos);
        return new ParserNode(span, startToken?.ModeName ?? "DEFAULT_MODE", rule, children);
    }

    /// <summary>
    /// Matches the next token against a literal string.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="lit">Literal to match.</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private ParseNode? TryParseLiteral(ParseContext context, LiteralMatch lit, Rule rule)
    {
        var token = context.Peek();
        if (token is null) return null;

        if (string.Equals(
            token.Text,
            lit.Value,
            _caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            context.Consume();
            return new LexerNode(token.Span, token.ModeName, rule, token);
        }

        return null;
    }

    /// <summary>Returns <c>true</c> when the grammar declares <c>caseInsensitive = true</c>.</summary>
    private static bool IsCaseInsensitive(ParserDefinition definition) =>
        definition.EffectiveOptions.CaseInsensitive;

    /// <summary>
    /// Implements the <c>~</c> negation operator at the token level: consumes the
    /// next token when the inner element does <em>not</em> match.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="neg">Negation element to evaluate.</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private ParseNode? TryParseNegation(ParseContext context, Negation neg, Rule rule, DiagnosticBag? diagnostics = null)
    {
        var token = context.Peek();
        if (token is null) return null;

        var savedPos = context.SavePosition();
        var matched = TryParseContent(context, neg.Inner, rule, diagnostics);
        context.RestorePosition(savedPos);

        if (matched is null)
        {
            // Negation succeeds: consume one token.
            var consumed = context.Consume();
            return new LexerNode(consumed.Span, consumed.ModeName, rule, consumed);
        }

        return null;
    }

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

    private List<ParseNode>? TryParseLeftRecursiveTail(
        ParseContext context,
        RuleContent tailContent,
        Rule ownerRule,
        Associativity associativity,
        int precedenceLevel,
        DiagnosticBag? diagnostics)
    {
        if (tailContent is Sequence sequence)
        {
            var result = new List<ParseNode>();
            foreach (var item in sequence.Items)
            {
                ParseNode? node;
                if (item is RuleRef rr &&
                    string.Equals(rr.RuleName, ownerRule.Name, StringComparison.Ordinal) &&
                    definition.AllRules.TryGetValue(rr.RuleName, out var recursiveRule))
                {
                    var minimumRightPrecedence = GetRightPrecedenceThreshold(associativity, precedenceLevel);
                    node = ParseRule(context, recursiveRule, minimumRightPrecedence, diagnostics);
                }
                else
                {
                    node = TryParseContent(context, item, ownerRule, diagnostics);
                }

                if (node is null)
                {
                    return null;
                }

                if (node.Span.Length > 0 || node is ParserNode { Children.Count: > 0 })
                {
                    result.Add(node);
                }
            }

            return result;
        }

        var tailNode = TryParseContent(context, tailContent, ownerRule, diagnostics);
        return tailNode is null ? null : [tailNode];
    }

    private static int GetRightPrecedenceThreshold(Associativity associativity, int precedenceLevel)
    {
        if (associativity == Associativity.Right)
        {
            return precedenceLevel;
        }

        return precedenceLevel + 1;
    }

    /// <summary>
    /// Creates a zero-length, empty <see cref="ParserNode"/> at the current position.
    /// Used for predicates and embedded actions that succeed without consuming input.
    /// </summary>
    /// <param name="context">Token-stream cursor (position is not advanced).</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private static ParseNode CreateEmptyNode(ParseContext context, Rule rule)
    {
        var token = context.Peek();
        var pos = token?.Span.Position ?? 0;
        var modeName = token?.ModeName ?? "DEFAULT_MODE";
        return new ParserNode(new SourceSpan(pos, 0), modeName, rule, []);
    }

    /// <summary>
    /// Computes the <see cref="SourceSpan"/> that covers all tokens consumed since
    /// <paramref name="startPosition"/>. Returns a zero-length span when no tokens
    /// were consumed or <paramref name="startToken"/> is <c>null</c>.
    /// </summary>
    /// <param name="startToken">The first token in the range, or <c>null</c>.</param>
    /// <param name="context">Current cursor (used to locate the last consumed token).</param>
    /// <param name="startPosition">Token-list index at the start of the range.</param>
    private static SourceSpan ComputeSpan(Token? startToken, ParseContext context, int startPosition)
    {
        if (startToken is null)
            return new SourceSpan(0, 0);

        var endToken = context.Peek(-1);
        if (endToken is not null && context.Position > startPosition)
        {
            var end = endToken.Span.Position + endToken.Span.Length;
            return new SourceSpan(startToken.Span.Position, end - startToken.Span.Position);
        }

        return new SourceSpan(startToken.Span.Position, 0);
    }
}
