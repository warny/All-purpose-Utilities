using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

internal record ParserFrame(Rule Rule, int InputPosition);

public sealed class ParserEngine(ParserDefinition definition)
{
    private readonly Stack<ParserFrame> _ruleStack = new();

    public ParseNode Parse(IEnumerable<Token> tokens, Rule? startRule = null)
    {
        var tokenList = tokens.ToList();
        var root = startRule ?? definition.RootRule
            ?? throw new InvalidOperationException("No root rule defined");

        var context = new ParseContext(tokenList);
        return ParseRule(context, root, precedence: 0)
            ?? new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE",
                "Failed to parse from root rule", root);
    }

    private ParseNode? ParseRule(ParseContext context, Rule rule, int precedence)
    {
        // Détection de récursion infinie
        if (_ruleStack.Any(f => f.Rule.Name == rule.Name && f.InputPosition == context.Position))
            return null;

        _ruleStack.Push(new ParserFrame(rule, context.Position));
        try
        {
            foreach (var alternative in rule.Content.Alternatives.OrderBy(a => a.Priority))
            {
                // Vérifier le niveau de précédence pour les alternatives récursives gauches
                if (!CheckPrecedence(alternative, precedence))
                    continue;

                var savedPos = context.SavePosition();
                var result = TryParseAlternative(context, alternative, rule);
                if (result is not null)
                    return result;
                context.RestorePosition(savedPos);
            }

            return null;
        }
        finally
        {
            _ruleStack.Pop();
        }
    }

    private bool CheckPrecedence(Alternative alt, int currentPrecedence)
    {
        // Chercher un PrecedencePredicate dans l'alternative
        var predLevel = FindPrecedenceLevel(alt.Content);
        if (predLevel is null)
            return true; // Pas de prédicat de précédence → toujours accepté

        return predLevel.Value >= currentPrecedence;
    }

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

    private ParseNode? TryParseAlternative(ParseContext context, Alternative alt, Rule rule)
    {
        return TryParseContent(context, alt.Content, rule);
    }

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
                // Prédicats sémantiques : ignorés silencieusement pour l'instant
                return CreateEmptyNode(context, rule);

            case PrecedencePredicate:
                // Déjà géré dans CheckPrecedence
                return CreateEmptyNode(context, rule);

            case EmbeddedAction:
                // Actions embarquées : jamais exécutées, considérées comme réussies
                return CreateEmptyNode(context, rule);

            default:
                return null;
        }
    }

    private ParseNode? TryParseRuleRef(ParseContext context, RuleRef ruleRef, Rule parentRule)
    {
        if (!definition.AllRules.TryGetValue(ruleRef.RuleName, out var referencedRule))
            return null;

        if (referencedRule.Kind == RuleKind.Lexer)
        {
            // Correspondance directe avec un token
            var token = context.Peek();
            if (token is null) return null;

            if (token.RuleName == ruleRef.RuleName)
            {
                context.Consume();
                return new LexerNode(token.Span, token.ModeName, referencedRule, token);
            }
            return null;
        }

        // Règle parser : récursion
        return ParseRule(context, referencedRule, precedence: 0);
    }

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

            // Ne pas ajouter les nœuds vides (prédicats, actions)
            if (node.Span.Length > 0 || node is ParserNode { Children.Count: > 0 })
                children.Add(node);
        }

        var span = ComputeSpan(startToken, context, startPos);
        return new ParserNode(span, startToken?.ModeName ?? "DEFAULT_MODE", rule, children);
    }

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

            // Protection contre les boucles infinies
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

    private ParseNode? TryParseLiteral(ParseContext context, LiteralMatch lit, Rule rule)
    {
        var token = context.Peek();
        if (token is null) return null;

        if (token.Text == lit.Value)
        {
            context.Consume();
            return new LexerNode(token.Span, token.ModeName, rule, token);
        }

        return null;
    }

    private ParseNode? TryParseNegation(ParseContext context, Negation neg, Rule rule)
    {
        var token = context.Peek();
        if (token is null) return null;

        var savedPos = context.SavePosition();
        var matched = TryParseContent(context, neg.Inner, rule);
        context.RestorePosition(savedPos);

        if (matched is null)
        {
            // La négation réussit : consommer un token
            var consumed = context.Consume();
            return new LexerNode(consumed.Span, consumed.ModeName, rule, consumed);
        }

        return null;
    }

    private static ParseNode CreateEmptyNode(ParseContext context, Rule rule)
    {
        var token = context.Peek();
        var pos = token?.Span.Position ?? 0;
        var modeName = token?.ModeName ?? "DEFAULT_MODE";
        return new ParserNode(new SourceSpan(pos, 0), modeName, rule, []);
    }

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
