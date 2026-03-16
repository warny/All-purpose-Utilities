using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Converts an <see cref="ICharStream"/> into a sequence of <see cref="Token"/> values
/// using the lexer rules in a <see cref="ParserDefinition"/>.
/// <para>
/// The engine uses <em>maximal-munch</em> (longest-match) tokenization: among all rules
/// that match at the current position it picks the one with the longest match, breaking
/// ties by <see cref="Rule.DeclarationOrder"/> (lower order wins).
/// </para>
/// <para>
/// Lexer modes are handled via an internal stack. Structured
/// <see cref="LexerCommand"/> directives (<c>pushMode</c>, <c>popMode</c>, <c>mode</c>,
/// <c>skip</c>, etc.) are executed after each successful match.
/// </para>
/// </summary>
public sealed class LexerEngine(ParserDefinition definition)
{
    private readonly Stack<LexerMode> _modeStack = new();

    /// <summary>
    /// Tokenizes the entire <paramref name="stream"/>, yielding one <see cref="Token"/>
    /// per recognized lexical unit.
    /// <para>
    /// Fragment rules and tokens marked with <c>-> skip</c> are consumed but not yielded.
    /// When no rule matches at the current position the engine enters panic mode:
    /// it consumes one character and emits an <c>ERROR</c> token.
    /// </para>
    /// </summary>
    /// <param name="stream">Character stream to tokenize.</param>
    /// <returns>Lazy sequence of tokens.</returns>
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
                // Panic mode: consume one character and emit an error token.
                var pos = stream.Position;
                var ch = stream.Peek();
                stream.Consume();
                yield return new Token(
                    new SourceSpan(pos, 1), "ERROR", mode.Name, ch.ToString());
                continue;
            }

            // Execute any LexerCommand directives embedded in the matched rule.
            bool skip = ExecuteLexerCommands(rule!, token);

            // Fragment rules and skipped tokens are consumed but not emitted.
            if (!rule!.IsFragment && !skip)
                yield return token;
        }
    }

    /// <summary>
    /// Scans all non-fragment rules in <paramref name="mode"/> and returns the token
    /// produced by the rule that matches the most characters at the current stream position.
    /// Ties are broken by <see cref="Rule.DeclarationOrder"/>: the rule with the lower
    /// order value wins. Returns <c>(null, null)</c> when no rule matches.
    /// </summary>
    /// <param name="stream">Character stream positioned at the start of the next token.</param>
    /// <param name="mode">Active lexer mode whose rules are tried.</param>
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

    /// <summary>
    /// Attempts to match <paramref name="rule"/> at the current stream position.
    /// Returns a <see cref="Token"/> on success, or <c>null</c> when the rule does not match.
    /// Zero-length matches are rejected.
    /// </summary>
    /// <param name="stream">Character stream (position is not permanently advanced on failure).</param>
    /// <param name="rule">Lexer rule to try.</param>
    /// <param name="modeName">Name of the active mode, recorded in the token.</param>
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

    /// <summary>
    /// Recursively attempts to match a <see cref="RuleContent"/> node against the
    /// current stream position. Advances the stream on success; leaves it unchanged
    /// on failure (callers are responsible for saving/restoring the position when needed).
    /// </summary>
    /// <param name="stream">Character stream.</param>
    /// <param name="content">Grammar element to match.</param>
    /// <returns><c>true</c> if the element matched and the stream was advanced accordingly.</returns>
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
                        continue; // Commands do not consume characters.
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
                return true; // These do not consume characters.

            default:
                return false;
        }
    }

    /// <summary>
    /// Tries to consume the exact character sequence <paramref name="value"/> from
    /// <paramref name="stream"/>. Advances the stream on success.
    /// </summary>
    /// <param name="stream">Character stream.</param>
    /// <param name="value">Literal string to match.</param>
    /// <returns><c>true</c> if the full literal was matched.</returns>
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

    /// <summary>
    /// Matches the inner element of a <see cref="Quantifier"/> as many times as
    /// allowed by its bounds. Protects against infinite loops caused by zero-length matches.
    /// </summary>
    /// <param name="stream">Character stream.</param>
    /// <param name="quant">Quantifier to evaluate.</param>
    /// <returns><c>true</c> if the minimum repetition count was satisfied.</returns>
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

            // Guard against zero-length matches to prevent infinite loops.
            if (stream.Position == savedPos)
                break;

            count++;

            if (!quant.Greedy && count >= quant.Min)
                break;
        }

        return count >= quant.Min;
    }

    /// <summary>
    /// Implements the <c>~</c> negation operator: succeeds when the inner element
    /// does <em>not</em> match, consuming exactly one character.
    /// </summary>
    /// <param name="stream">Character stream.</param>
    /// <param name="neg">Negation element to evaluate.</param>
    /// <returns><c>true</c> if the inner element did not match (and one character was consumed).</returns>
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
    /// Walks the content tree of a matched rule, executes any
    /// <see cref="LexerCommand"/> directives found, and returns whether the token
    /// should be skipped (suppressed from output).
    /// </summary>
    /// <param name="rule">The rule that produced <paramref name="token"/>.</param>
    /// <param name="token">The matched token (not mutated).</param>
    /// <returns><c>true</c> if the token should be discarded.</returns>
    private bool ExecuteLexerCommands(Rule rule, Token token)
    {
        bool skip = false;
        CollectAndExecuteCommands(rule.Content, ref skip);
        return skip;
    }

    /// <summary>
    /// Recursively traverses <paramref name="content"/> and executes any
    /// <see cref="LexerCommand"/> nodes encountered, updating <paramref name="skip"/>
    /// and the mode stack as needed.
    /// </summary>
    /// <param name="content">Grammar element to inspect.</param>
    /// <param name="skip">Set to <c>true</c> when a <c>skip</c> command is found.</param>
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
                        // Prepend matched text to the next token; handled downstream.
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
                        // Channel and Type are token metadata handled downstream.
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

    /// <summary>
    /// Returns the <c>DEFAULT_MODE</c> from the grammar definition, falling back
    /// to the first mode when <c>DEFAULT_MODE</c> is not found.
    /// </summary>
    private LexerMode GetDefaultMode() =>
        definition.Modes.FirstOrDefault(m => m.Name == "DEFAULT_MODE")
        ?? definition.Modes[0];

    /// <summary>
    /// Reads <paramref name="length"/> characters starting at <paramref name="startPos"/>
    /// from <paramref name="stream"/> and returns them as a string.
    /// For <see cref="StringCharStream"/> this is done without advancing the stream;
    /// for other implementations the current position is saved and restored.
    /// </summary>
    /// <param name="stream">Character stream to read from.</param>
    /// <param name="startPos">Absolute start position of the region to extract.</param>
    /// <param name="length">Number of characters to read.</param>
    /// <returns>The extracted text.</returns>
    private static string ExtractText(ICharStream stream, int startPos, int length)
    {
        // For StringCharStream we can access the underlying characters directly.
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

        // Generic fallback for other ICharStream implementations.
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
