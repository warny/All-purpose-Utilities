using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Internal record used to detect left-recursive cycles.
/// Stores the rule being attempted and the token-list position at which it was entered.
/// </summary>
internal record ParserFrame(Rule Rule, int InputPosition);
internal readonly record struct ParserFrameKey(string RuleName, int InputPosition);

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
    private readonly Stack<ParserFrame> _ruleStack = new();
    private readonly HashSet<ParserFrameKey> _activeRuleFrames = new();
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
    public ParseNode Parse(IEnumerable<Token> tokens, Rule? startRule = null)
    {
        var tokenList = tokens.ToList();
        var root = startRule ?? definition.RootRule
            ?? throw new InvalidOperationException("No root rule defined");

        var context = new ParseContext(tokenList);
        var result = ParseRule(context, root, precedence: 0);

        if (result is null)
            return new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE",
                "Failed to parse from root rule", root);

        // Reject parses that leave trailing tokens unconsumed.
        if (!context.IsEnd)
        {
            var trailing = context.Peek()!;
            return new ErrorNode(result.Span, result.ModeName,
                $"Unexpected token '{trailing.Text}' at position {trailing.Span.Position}", root);
        }

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
    private ParseNode? ParseRule(ParseContext context, Rule rule, int precedence)
    {
        // Detect left-recursive infinite cycles.
        var frameKey = new ParserFrameKey(rule.Name, context.Position);
        if (_activeRuleFrames.Contains(frameKey))
            return null;

        _activeRuleFrames.Add(frameKey);
        _ruleStack.Push(new ParserFrame(rule, context.Position));
        try
        {
            foreach (var alternative in rule.Content.Alternatives.OrderBy(a => a.Priority))
            {
                // Skip alternatives whose precedence predicate is below the current level.
                if (!CheckPrecedence(alternative, precedence))
                    continue;

                var savedPos = context.SavePosition();
                var result = TryParseAlternative(context, alternative, rule);
                if (result is not null)
                {
                    // Ensure the returned node is tagged with this rule.
                    if (result is ParserNode pn && object.ReferenceEquals(pn.Rule, rule))
                        return pn;
                    return new ParserNode(result.Span, result.ModeName, rule,
                        new List<ParseNode> { result });
                }
                context.RestorePosition(savedPos);
            }

            return null;
        }
        finally
        {
            _ruleStack.Pop();
            _activeRuleFrames.Remove(frameKey);
        }
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
    private ParseNode? TryParseAlternative(ParseContext context, Alternative alt, Rule rule)
    {
        return TryParseContent(context, alt.Content, rule);
    }

    /// <summary>
    /// Dispatches to the appropriate handler based on the concrete type of
    /// <paramref name="content"/>.
    /// Predicates and embedded actions are silently treated as empty successful matches.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="content">Grammar element to match.</param>
    /// <param name="rule">Rule in whose context the element is being matched.</param>
    private ParseNode? TryParseContent(ParseContext context, RuleContent content, Rule rule)
    {
        switch (content)
        {
            case RuleRef ruleRef:
                return TryParseRuleRef(context, ruleRef, rule);

            case Sequence seq:
                return TryParseSequence(context, seq, rule);

            case Alternation alternation:
                return TryParseAlternation(context, alternation, rule);

            case Alternative alt:
                return TryParseAlternative(context, alt, rule);

            case Quantifier quant:
                return TryParseQuantifier(context, quant, rule);

            case LiteralMatch lit:
                return TryParseLiteral(context, lit, rule);

            case Negation neg:
                return TryParseNegation(context, neg, rule);

            case ValidatingPredicate:
            case GatingPredicate:
                // Semantic predicates: silently accepted without evaluation.
                return CreateEmptyNode(context, rule);

            case PrecedencePredicate:
                // Already handled in CheckPrecedence.
                return CreateEmptyNode(context, rule);

            case EmbeddedAction:
                // Embedded actions: never executed, always succeed.
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
    private ParseNode? TryParseRuleRef(ParseContext context, RuleRef ruleRef, Rule parentRule)
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
                return new LexerNode(token.Span, token.ModeName, referencedRule, token);
            }
            return null;
        }

        // Parser rule: recurse.
        return ParseRule(context, referencedRule, precedence: 0);
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
    private ParseNode? TryParseSequence(ParseContext context, Sequence seq, Rule rule)
    {
        var children = new List<ParseNode>();
        var startPos = context.Position;
        var startToken = context.Peek();

        foreach (var item in seq.Items)
        {
            if (item is EmbeddedAction or Model.LexerCommand)
                continue;

            var node = TryParseContent(context, item, rule);
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
    private ParseNode? TryParseAlternation(ParseContext context, Alternation alternation, Rule rule)
    {
        foreach (var alt in alternation.Alternatives.OrderBy(a => a.Priority))
        {
            var savedPos = context.SavePosition();
            var result = TryParseContent(context, alt.Content, rule);
            if (result is not null)
                return result;
            context.RestorePosition(savedPos);
        }
        return null;
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
    private ParseNode? TryParseQuantifier(ParseContext context, Quantifier quant, Rule rule)
    {
        var children = new List<ParseNode>();
        var startPos = context.Position;
        var startToken = context.Peek();

        int count = 0;
        while (quant.Max is null || count < quant.Max.Value)
        {
            var savedPos = context.SavePosition();
            var node = TryParseContent(context, quant.Inner, rule);
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
        definition.Options?.Values.TryGetValue("caseInsensitive", out var value) == true
        && bool.TryParse(value, out var parsedValue)
        && parsedValue;

    /// <summary>
    /// Implements the <c>~</c> negation operator at the token level: consumes the
    /// next token when the inner element does <em>not</em> match.
    /// </summary>
    /// <param name="context">Mutable token-stream cursor.</param>
    /// <param name="neg">Negation element to evaluate.</param>
    /// <param name="rule">Owning rule for the returned node.</param>
    private ParseNode? TryParseNegation(ParseContext context, Negation neg, Rule rule)
    {
        var token = context.Peek();
        if (token is null) return null;

        var savedPos = context.SavePosition();
        var matched = TryParseContent(context, neg.Inner, rule);
        context.RestorePosition(savedPos);

        if (matched is null)
        {
            // Negation succeeds: consume one token.
            var consumed = context.Consume();
            return new LexerNode(consumed.Span, consumed.ModeName, rule, consumed);
        }

        return null;
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
