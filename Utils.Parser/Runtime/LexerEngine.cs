using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

public sealed class LexerEngine(ParserDefinition definition)
{
    private readonly Stack<LexerMode> _modeStack = new();

    public IEnumerable<Token> Tokenize(ICharStream stream)
    {
        _modeStack.Clear();
        _modeStack.Push(GetDefaultMode());

        while (!stream.IsEnd)
        {
            var mode = _modeStack.Peek();
            var (token, rule) = MatchLongest(stream, mode);

            if (token is null)
            {
                // Panic mode : consommer un caractère et émettre un token d'erreur
                var pos = stream.Position;
                var ch = stream.Peek();
                stream.Consume();
                yield return new Token(
                    new SourceSpan(pos, 1), "ERROR", mode.Name, ch.ToString());
                continue;
            }

            // Exécuter les LexerCommands de la règle
            bool skip = ExecuteLexerCommands(rule!, token);

            // Ne pas émettre les fragments ni les tokens skippés
            if (!rule!.IsFragment && !skip)
                yield return token;
        }
    }

    private (Token? token, Rule? rule) MatchLongest(ICharStream stream, LexerMode mode)
    {
        Token? best = null;
        Rule? bestRule = null;

        foreach (var rule in mode.Rules.OrderBy(r => r.DeclarationOrder))
        {
            if (rule.IsFragment)
                continue;

            var savedPos = stream.SavePosition();
            var matched = TryMatchRule(stream, rule, mode.Name);

            if (matched is not null &&
                (best is null || matched.Span.Length > best.Span.Length))
            {
                best = matched;
                bestRule = rule;
            }

            stream.RestorePosition(savedPos);
        }

        if (best is not null)
            stream.Consume(best.Span.Length);

        return (best, bestRule);
    }

    private Token? TryMatchRule(ICharStream stream, Rule rule, string modeName)
    {
        var startPos = stream.Position;
        if (TryMatchContent(stream, rule.Content))
        {
            var length = stream.Position - startPos;
            if (length == 0)
                return null;

            var text = ExtractText(stream, startPos, length);
            return new Token(new SourceSpan(startPos, length), rule.Name, modeName, text);
        }
        return null;
    }

    private bool TryMatchContent(ICharStream stream, RuleContent content)
    {
        switch (content)
        {
            case LiteralMatch lit:
                return TryMatchLiteral(stream, lit.Value);

            case RangeMatch range:
                if (stream.IsEnd) return false;
                var ch = stream.Peek();
                if (ch >= range.From && ch <= range.To)
                {
                    stream.Consume();
                    return true;
                }
                return false;

            case CharSetMatch charSet:
                if (stream.IsEnd) return false;
                var c = stream.Peek();
                var inSet = charSet.Chars.Contains(c);
                if (charSet.Negated ? !inSet : inSet)
                {
                    stream.Consume();
                    return true;
                }
                return false;

            case AnyChar:
                if (stream.IsEnd) return false;
                stream.Consume();
                return true;

            case RuleRef ruleRef:
                if (definition.AllRules.TryGetValue(ruleRef.RuleName, out var referencedRule))
                    return TryMatchContent(stream, referencedRule.Content);
                return false;

            case Sequence seq:
                var seqSave = stream.SavePosition();
                foreach (var item in seq.Items)
                {
                    if (item is Model.LexerCommand or EmbeddedAction)
                        continue; // Les commandes lexer ne consomment pas de caractères
                    if (!TryMatchContent(stream, item))
                    {
                        stream.RestorePosition(seqSave);
                        return false;
                    }
                }
                return true;

            case Alternation alternation:
                foreach (var alt in alternation.Alternatives)
                {
                    var altSave = stream.SavePosition();
                    if (TryMatchContent(stream, alt.Content))
                        return true;
                    stream.RestorePosition(altSave);
                }
                return false;

            case Alternative alt:
                return TryMatchContent(stream, alt.Content);

            case Quantifier quant:
                return TryMatchQuantifier(stream, quant);

            case Negation neg:
                return TryMatchNegation(stream, neg);

            case Model.LexerCommand:
            case EmbeddedAction:
                return true; // Ne consomment pas de caractères

            default:
                return false;
        }
    }

    private static bool TryMatchLiteral(ICharStream stream, string value)
    {
        if (value.Length == 0)
            return true;

        for (int i = 0; i < value.Length; i++)
        {
            if (stream.Peek(i) != value[i])
                return false;
        }

        stream.Consume(value.Length);
        return true;
    }

    private bool TryMatchQuantifier(ICharStream stream, Quantifier quant)
    {
        int count = 0;

        while (quant.Max is null || count < quant.Max.Value)
        {
            var savedPos = stream.SavePosition();
            if (!TryMatchContent(stream, quant.Inner))
            {
                stream.RestorePosition(savedPos);
                break;
            }

            // Protection contre les boucles infinies (match de longueur zéro)
            if (stream.Position == savedPos)
                break;

            count++;

            if (!quant.Greedy && count >= quant.Min)
                break;
        }

        return count >= quant.Min;
    }

    private bool TryMatchNegation(ICharStream stream, Negation neg)
    {
        if (stream.IsEnd) return false;

        var savedPos = stream.SavePosition();
        var matched = TryMatchContent(stream, neg.Inner);
        stream.RestorePosition(savedPos);

        if (!matched)
        {
            stream.Consume();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Retourne true si le token doit être ignoré (skip)
    /// </summary>
    private bool ExecuteLexerCommands(Rule rule, Token token)
    {
        bool skip = false;
        CollectAndExecuteCommands(rule.Content, ref skip);
        return skip;
    }

    private void CollectAndExecuteCommands(RuleContent content, ref bool skip)
    {
        switch (content)
        {
            case Model.LexerCommand cmd:
                switch (cmd.Type)
                {
                    case LexerCommandType.Skip:
                        skip = true;
                        break;
                    case LexerCommandType.More:
                        // More : le texte du prochain token sera préfixé par celui-ci
                        break;
                    case LexerCommandType.PushMode:
                        if (cmd.Argument is not null)
                        {
                            var mode = definition.Modes.FirstOrDefault(
                                m => m.Name == cmd.Argument);
                            if (mode is not null)
                                _modeStack.Push(mode);
                        }
                        break;
                    case LexerCommandType.PopMode:
                        if (_modeStack.Count > 1)
                            _modeStack.Pop();
                        break;
                    case LexerCommandType.Mode:
                        if (cmd.Argument is not null)
                        {
                            var targetMode = definition.Modes.FirstOrDefault(
                                m => m.Name == cmd.Argument);
                            if (targetMode is not null)
                            {
                                if (_modeStack.Count > 0)
                                    _modeStack.Pop();
                                _modeStack.Push(targetMode);
                            }
                        }
                        break;
                    case LexerCommandType.Channel:
                    case LexerCommandType.Type:
                        // Channel et Type : métadonnées sur le token, gérées en aval
                        break;
                }
                break;

            case Sequence seq:
                foreach (var item in seq.Items)
                    CollectAndExecuteCommands(item, ref skip);
                break;

            case Alternation alt:
                foreach (var a in alt.Alternatives)
                    CollectAndExecuteCommands(a.Content, ref skip);
                break;

            case Alternative a:
                CollectAndExecuteCommands(a.Content, ref skip);
                break;
        }
    }

    private LexerMode GetDefaultMode() =>
        definition.Modes.FirstOrDefault(m => m.Name == "DEFAULT_MODE")
        ?? definition.Modes[0];

    private static string ExtractText(ICharStream stream, int startPos, int length)
    {
        // Pour StringCharStream, on peut accéder directement au texte
        if (stream is StringCharStream scs)
        {
            var savedPos = stream.SavePosition();
            stream.RestorePosition(startPos);
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = stream.Peek(i);
            }
            stream.RestorePosition(savedPos);
            return new string(chars);
        }

        // Fallback générique
        var saved = stream.SavePosition();
        stream.RestorePosition(startPos);
        var buffer = new char[length];
        for (int i = 0; i < length; i++)
        {
            buffer[i] = stream.Peek(i);
        }
        stream.RestorePosition(saved);
        return new string(buffer);
    }

}
