using System.Text;
using Utils.Parser.Diagnostics;
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
    private readonly bool _caseInsensitive = IsCaseInsensitive(definition);

    // ── "more" buffering state ────────────────────────────────────────────────
    // When a token fires "-> more", its text is accumulated here and prepended to
    // the next emitted token, with _moreStartPos recording the original start.
    private readonly StringBuilder _moreTextBuilder = new();
    private int    _moreStartPos = -1;

    // ── Lexer cycle detection ─────────────────────────────────────────────────
    // Tracks (ruleName, streamPosition) pairs that are currently being expanded.
    // A same pair appearing twice means the rule cannot consume any input at that
    // position, which would cause infinite recursion (e.g. A: A; or deep mutual
    // recursion between fragments). The set is maintained with push/pop semantics
    // inside TryMatchContent so it is always empty between top-level token matches.
    private readonly HashSet<(string RuleName, int Position)> _activeRuleRefs = new();

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
    public IEnumerable<Token> Tokenize(ICharStream stream, DiagnosticBag? diagnostics = null)
    {
        diagnostics ??= new DiagnosticBag();
        _modeStack.Clear();
        _modeStack.Push(GetDefaultMode());
        _moreTextBuilder.Clear();
        _moreStartPos = -1;
        _activeRuleRefs.Clear();

        while (!stream.IsEnd)
        {
            var mode = _modeStack.Peek();
            var (token, rule, commands) = MatchLongest(stream, mode);

            if (token is null)
            {
                // Panic mode: consume one character and emit an error token.
                var pos = stream.Position;
                var ch = stream.Peek();
                stream.Consume();
                diagnostics.AddWithContext(ParserDiagnostics.ParseFailure, pos, 1, null, null, $"Unrecognized character '{ch}'.");
                yield return new Token(
                    new SourceSpan(pos, 1), "ERROR", mode.Name, ch.ToString());
                continue;
            }

            // Execute commands collected only from the matched path.
            bool skip = ExecuteLexerCommands(commands, ref token, diagnostics);

            if (rule!.IsFragment || skip)
                continue;

            // Apply any accumulated "more" text, then emit.
            if (_moreTextBuilder.Length > 0)
            {
                var combinedSpan = new SourceSpan(
                    _moreStartPos,
                    token.Span.Position + token.Span.Length - _moreStartPos);
                token = new Token(combinedSpan, token.RuleName, token.ModeName,
                    _moreTextBuilder.ToString() + token.Text);
                _moreTextBuilder.Clear();
                _moreStartPos = -1;
            }

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
    private (Token? token, Rule? rule, List<Model.LexerCommand> commands) MatchLongest(
        ICharStream stream, LexerMode mode)
    {
        Token? best = null;
        Rule? bestRule = null;
        var bestCommands = new List<Model.LexerCommand>();

        foreach (var rule in mode.Rules.OrderBy(r => r.DeclarationOrder))
        {
            if (rule.IsFragment)
                continue;

            var savedPos = stream.SavePosition();
            var ruleCommands = new List<Model.LexerCommand>();
            var matched = TryMatchRule(stream, rule, mode.Name, ruleCommands);

            if (matched is not null &&
                (best is null || matched.Span.Length > best.Span.Length))
            {
                best = matched;
                bestRule = rule;
                bestCommands = ruleCommands;
            }

            stream.RestorePosition(savedPos);
        }

        if (best is not null)
            stream.Consume(best.Span.Length);

        return (best, bestRule, bestCommands);
    }

    /// <summary>
    /// Attempts to match <paramref name="rule"/> at the current stream position.
    /// Returns a <see cref="Token"/> on success, or <c>null</c> when the rule does not match.
    /// Zero-length matches are rejected.
    /// On success <paramref name="matchedCommands"/> is populated with the
    /// <see cref="LexerCommand"/> nodes encountered only along the matched path.
    /// </summary>
    /// <param name="stream">Character stream (position is not permanently advanced on failure).</param>
    /// <param name="rule">Lexer rule to try.</param>
    /// <param name="modeName">Name of the active mode, recorded in the token.</param>
    /// <param name="matchedCommands">Receives commands from the matched path only.</param>
    private Token? TryMatchRule(ICharStream stream, Rule rule, string modeName,
        List<Model.LexerCommand> matchedCommands)
    {
        var startPos = stream.Position;
        if (TryMatchContent(stream, rule.Content, matchedCommands))
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
    /// <param name="commands">
    /// When non-<c>null</c>, any <see cref="LexerCommand"/> nodes encountered
    /// <em>along the matched path only</em> are appended to this list.
    /// Commands in alternatives that did not match are not collected.
    /// </param>
    /// <returns><c>true</c> if the element matched and the stream was advanced accordingly.</returns>
    private bool TryMatchContent(ICharStream stream, RuleContent content,
        List<Model.LexerCommand>? commands = null)
    {
        switch (content)
        {
            case LiteralMatch lit:
                return TryMatchLiteral(stream, lit.Value, _caseInsensitive);

            case RangeMatch range:
                if (stream.IsEnd) return false;
                var ch = stream.Peek();
                if (IsCharInRange(ch, range.From, range.To, _caseInsensitive))
                {
                    stream.Consume();
                    return true;
                }
                return false;

            case CharSetMatch charSet:
                if (stream.IsEnd) return false;
                var c = stream.Peek();
                var inSet = IsCharInSet(charSet.Chars, c, _caseInsensitive);
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
                {
                    // Guard against infinite recursion: if this rule is already being
                    // expanded at the same stream position, it cannot make progress.
                    var cycleKey = (ruleRef.RuleName, stream.Position);
                    if (!_activeRuleRefs.Add(cycleKey))
                        return false;
                    try
                    {
                        return TryMatchContent(stream, referencedRule.Content, commands);
                    }
                    finally
                    {
                        _activeRuleRefs.Remove(cycleKey);
                    }
                }
                return false;

            case Sequence seq:
                var seqSave = stream.SavePosition();
                foreach (var item in seq.Items)
                {
                    if (!TryMatchContent(stream, item, commands))
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
                    // Use a branch-local list so commands from failing branches are discarded.
                    var branchCommands = commands is not null ? new List<Model.LexerCommand>() : null;
                    if (TryMatchContent(stream, alt.Content, branchCommands))
                    {
                        commands?.AddRange(branchCommands!);
                        return true;
                    }
                    stream.RestorePosition(altSave);
                }
                return false;

            case Alternative alt:
                return TryMatchContent(stream, alt.Content, commands);

            case Quantifier quant:
                return TryMatchQuantifier(stream, quant, commands);

            case Negation neg:
                return TryMatchNegation(stream, neg);

            case Model.LexerCommand cmd:
                // Collect only when we are on the matched path (commands != null).
                commands?.Add(cmd);
                return true;

            case EmbeddedAction:
                return true; // Does not consume characters.

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
    /// <param name="caseInsensitive"><c>true</c> to compare characters without regard to letter case.</param>
    /// <returns><c>true</c> if the full literal was matched.</returns>
    private static bool TryMatchLiteral(ICharStream stream, string value, bool caseInsensitive)
    {
        if (value.Length == 0)
            return true;

        for (int i = 0; i < value.Length; i++)
        {
            if (!CharsEqual(stream.Peek(i), value[i], caseInsensitive))
                return false;
        }

        stream.Consume(value.Length);
        return true;
    }

    /// <summary>Returns <c>true</c> when the grammar declares <c>caseInsensitive = true</c>.</summary>
    private static bool IsCaseInsensitive(ParserDefinition definition) =>
        definition.Options?.Values.TryGetValue("caseInsensitive", out var value) == true
        && bool.TryParse(value, out var parsedValue)
        && parsedValue;

    /// <summary>Compares two characters using ordinal or case-insensitive semantics.</summary>
    private static bool CharsEqual(char left, char right, bool caseInsensitive) =>
        caseInsensitive
            ? char.ToUpperInvariant(left) == char.ToUpperInvariant(right)
            : left == right;

    /// <summary>Checks whether <paramref name="value"/> belongs to the inclusive range.</summary>
    private static bool IsCharInRange(char value, char start, char end, bool caseInsensitive)
    {
        if (!caseInsensitive)
            return value >= start && value <= end;

        char normalizedValue = char.ToUpperInvariant(value);
        char normalizedStart = char.ToUpperInvariant(start);
        char normalizedEnd = char.ToUpperInvariant(end);
        return normalizedValue >= normalizedStart && normalizedValue <= normalizedEnd;
    }

    /// <summary>Checks membership in a character set, optionally ignoring case.</summary>
    private static bool IsCharInSet(IReadOnlySet<char> chars, char value, bool caseInsensitive)
    {
        if (chars.Contains(value))
            return true;

        if (!caseInsensitive)
            return false;

        return chars.Contains(char.ToUpperInvariant(value))
            || chars.Contains(char.ToLowerInvariant(value));
    }

    /// <summary>
    /// Matches the inner element of a <see cref="Quantifier"/> as many times as
    /// allowed by its bounds. Protects against infinite loops caused by zero-length matches.
    /// </summary>
    /// <param name="stream">Character stream.</param>
    /// <param name="quant">Quantifier to evaluate.</param>
    /// <param name="commands">Receives commands collected from each successful iteration.</param>
    /// <returns><c>true</c> if the minimum repetition count was satisfied.</returns>
    private bool TryMatchQuantifier(ICharStream stream, Quantifier quant,
        List<Model.LexerCommand>? commands = null)
    {
        int count = 0;

        while (quant.Max is null || count < quant.Max.Value)
        {
            var savedPos = stream.SavePosition();
            if (!TryMatchContent(stream, quant.Inner, commands))
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
    /// Executes the <see cref="LexerCommand"/> directives that were collected
    /// <em>only from the matched path</em> during <see cref="TryMatchContent"/>.
    /// Updates the mode stack and returns whether the token should be skipped.
    /// When a <c>more</c> command is found, the token text is accumulated in
    /// <see cref="_moreTextBuilder"/> and the method returns <c>true</c> (suppress emit).
    /// </summary>
    /// <param name="commands">Commands from the matched path (may be empty).</param>
    /// <param name="token">The matched token; may be passed for context but is not mutated here.</param>
    /// <returns><c>true</c> if the token should be suppressed from output.</returns>
    private bool ExecuteLexerCommands(List<Model.LexerCommand> commands, ref Token token, DiagnosticBag diagnostics)
    {
        bool skip = false;
        bool more = false;

        foreach (var cmd in commands)
        {
            switch (cmd.Type)
            {
                case LexerCommandType.Skip:
                    skip = true;
                    break;

                case LexerCommandType.More:
                    more = true;
                    break;

                case LexerCommandType.PushMode:
                    if (cmd.Argument is not null)
                    {
                        var mode = definition.Modes.FirstOrDefault(m => m.Name == cmd.Argument);
                        if (mode is not null)
                            _modeStack.Push(mode);
                        else
                            diagnostics.AddWithContext(ParserDiagnostics.UnknownLexerMode, token.Span.Position, token.Span.Length, null, null, cmd.Argument, "pushMode");
                    }
                    break;

                case LexerCommandType.PopMode:
                    if (_modeStack.Count > 1)
                        _modeStack.Pop();
                    break;

                case LexerCommandType.Mode:
                    if (cmd.Argument is not null)
                    {
                        var targetMode = definition.Modes.FirstOrDefault(m => m.Name == cmd.Argument);
                        if (targetMode is not null)
                        {
                            if (_modeStack.Count > 0)
                                _modeStack.Pop();
                            _modeStack.Push(targetMode);
                        }
                        else
                        {
                            diagnostics.AddWithContext(ParserDiagnostics.UnknownLexerMode, token.Span.Position, token.Span.Length, null, null, cmd.Argument, "mode");
                        }
                    }
                    break;

                case LexerCommandType.Channel:
                case LexerCommandType.Type:
                    // Token metadata; handled downstream.
                    break;
            }
        }

        if (more)
        {
            // Accumulate text for the next token.
            if (_moreStartPos < 0)
                _moreStartPos = token.Span.Position;
            _moreTextBuilder.Append(token.Text);
            return true; // suppress this token
        }

        return skip;
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
