using System.IO;
using System.Text;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Converts a <see cref="TextReader"/> into a sequence of <see cref="Token"/> values
/// using the lexer rules in a <see cref="ParserDefinition"/>.
/// </summary>
public sealed class LexerEngine(ParserDefinition definition)
{
    private const string DefaultChannel = "DEFAULT_CHANNEL";
    private const string HiddenChannel = "HIDDEN";

    private readonly Stack<LexerMode> _modeStack = [];
    private readonly bool _caseInsensitive = definition.EffectiveOptions.CaseInsensitive;
    private readonly IReadOnlyDictionary<string, KeywordLookup> _keywordTrieByMode =
        BuildKeywordTries(definition, definition.EffectiveOptions.CaseInsensitive);
    private readonly StringBuilder _moreTextBuilder = new();
    private readonly HashSet<(string RuleName, int Offset)> _activeRuleRefs = [];
    private int _moreStartPos = -1;

    /// <summary>
    /// Tokenizes the full input stream with a strict forward-only reader.
    /// </summary>
    /// <param name="reader">Input text reader.</param>
    /// <param name="options">Optional lexer options.</param>
    /// <param name="diagnostics">Optional diagnostics sink.</param>
    /// <param name="filePath">Optional source file path for diagnostics.</param>
    /// <returns>Token stream including non-default channels.</returns>
    public IEnumerable<Token> Tokenize(
        TextReader reader,
        LexerEngineOptions? options = null,
        DiagnosticBag? diagnostics = null,
        string? filePath = null)
    {
        options ??= new LexerEngineOptions();
        var input = new TextReaderBuffer(reader);
        var emittedTokens = new List<Token>();

        ValidateExtensions(options.Extensions, diagnostics, filePath);

        _modeStack.Clear();
        _modeStack.Push(GetDefaultMode());
        _moreTextBuilder.Clear();
        _moreStartPos = -1;

        while (!input.IsEnd)
        {
            var mode = _modeStack.Peek();

            foreach (Token extensionToken in RunTryReadTokensExtensions(options.Extensions, input, emittedTokens, mode.Name, diagnostics, filePath))
            {
                emittedTokens.Add(extensionToken);
                yield return extensionToken;
            }

            if (input.IsEnd)
            {
                break;
            }

            var (match, rule, commands) = MatchLongest(input, mode, filePath);
            if (match is null)
            {
                int bad = input.Peek(0);
                var text = bad < 0 ? string.Empty : ((char)bad).ToString();
                var span = new SourceSpan(input.Position, 1, input.Line, input.Column, filePath);
                diagnostics?.AddWithContext(ParserDiagnostics.ParseFailure, span.Position, span.Length, null, null, $"Unrecognized character '{text}'.");
                input.Consume();
                var errorToken = new Token(span, "ERROR", mode.Name, DefaultChannel, text);
                emittedTokens.Add(errorToken);
                yield return errorToken;
                continue;
            }

            string tokenText = BuildText(input, match.Length);
            input.Consume(match.Length);
            var token = new Token(match, rule!.Name, mode.Name, DefaultChannel, tokenText);

            bool skip = ExecuteLexerCommands(commands, ref token, diagnostics, filePath);
            if (rule.IsFragment || skip)
            {
                continue;
            }

            if (_moreTextBuilder.Length > 0)
            {
                var combinedSpan = new SourceSpan(_moreStartPos, token.Span.Position + token.Span.Length - _moreStartPos, token.Span.Line, token.Span.Column, filePath);
                token = token with { Span = combinedSpan, Text = _moreTextBuilder + token.Text };
                _moreTextBuilder.Clear();
                _moreStartPos = -1;
            }

            ValidateTokenEmission(token, diagnostics, filePath);

            emittedTokens.Add(token);
            yield return token;

            foreach (Token extensionToken in RunAfterTokenExtensions(options.Extensions, token, input, emittedTokens, mode.Name, diagnostics, filePath))
            {
                emittedTokens.Add(extensionToken);
                yield return extensionToken;
            }
        }

        foreach (Token endToken in RunEndOfInputExtensions(options.Extensions, input, emittedTokens, diagnostics, filePath))
        {
            emittedTokens.Add(endToken);
            yield return endToken;
        }

    }

    private void ValidateExtensions(IReadOnlyList<ILexerExtension> extensions, DiagnosticBag? diagnostics, string? filePath)
    {
        if (definition.DeclaredTokens.Count > 0 && definition.ExtensionBindings.Count == 0)
        {
            ThrowValidation(diagnostics, filePath, 1, 1, null, "UP2001", "tokens { ... } requires superClass / extension binding.");
        }

        if (definition.ExtensionBindings.Count > 0 && extensions.Count == 0)
        {
            ThrowValidation(diagnostics, filePath, 1, 1, null, "UP2002", "superClass extension binding exists but no ILexerExtension is configured.");
        }
    }

    private IEnumerable<Token> RunTryReadTokensExtensions(IReadOnlyList<ILexerExtension> extensions, TextReaderBuffer input, IReadOnlyList<Token> emittedTokens, string modeName, DiagnosticBag? diagnostics, string? filePath)
    {
        foreach (ILexerExtension extension in extensions)
        {
            int startPosition = input.Position;
            var context = new LexerExtensionContext(definition, input, emittedTokens, modeName);
            var tokens = extension.TryReadTokens(context);
            if (tokens is null)
            {
                ThrowValidation(diagnostics, filePath, input.Line, input.Column, null, "UP2102", $"Extension '{extension.GetType().Name}' returned null from TryReadTokens.");
            }

            int consumedFromExtension = 0;
            bool emitted = false;
            foreach (Token token in tokens)
            {
                emitted = true;
                ValidateTokenEmission(token, diagnostics, filePath);
                int end = token.Span.Position + token.Span.Length;
                consumedFromExtension = Math.Max(consumedFromExtension, end - startPosition);
                yield return token;
            }

            if (emitted)
            {
                if (consumedFromExtension <= 0)
                {
                    ThrowValidation(diagnostics, filePath, input.Line, input.Column, null, "UP2103", $"Extension '{extension.GetType().Name}' emitted token(s) without consuming input.");
                }

                input.Consume(consumedFromExtension);
            }
        }
    }

    private IEnumerable<Token> RunAfterTokenExtensions(IReadOnlyList<ILexerExtension> extensions, Token token, TextReaderBuffer input, IReadOnlyList<Token> emittedTokens, string modeName, DiagnosticBag? diagnostics, string? filePath)
    {
        foreach (ILexerExtension extension in extensions)
        {
            var context = new LexerExtensionContext(definition, input, emittedTokens, modeName);
            var extra = extension.OnAfterToken(token, context);
            if (extra is null)
            {
                ThrowValidation(diagnostics, filePath, token.Span.Line, token.Span.Column, token.Span, "UP2104", $"Extension '{extension.GetType().Name}' returned null from OnAfterToken.");
            }

            foreach (Token extraToken in extra)
            {
                ValidateTokenEmission(extraToken, diagnostics, filePath);
                yield return extraToken;
            }
        }
    }

    private IEnumerable<Token> RunEndOfInputExtensions(IReadOnlyList<ILexerExtension> extensions, TextReaderBuffer input, IReadOnlyList<Token> emittedTokens, DiagnosticBag? diagnostics, string? filePath)
    {
        foreach (ILexerExtension extension in extensions)
        {
            var context = new LexerExtensionContext(definition, input, emittedTokens, _modeStack.Peek().Name);
            var extra = extension.OnEndOfInput(context);
            if (extra is null)
            {
                ThrowValidation(diagnostics, filePath, input.Line, input.Column, null, "UP2105", $"Extension '{extension.GetType().Name}' returned null from OnEndOfInput.");
            }

            foreach (Token extraToken in extra)
            {
                ValidateTokenEmission(extraToken, diagnostics, filePath);
                yield return extraToken;
            }
        }
    }

    private void ValidateTokenEmission(Token token, DiagnosticBag? diagnostics, string? filePath)
    {
        if (token.Span.Position < 0 || token.Span.Length < 0 || token.Span.Line < 1 || token.Span.Column < 1)
        {
            ThrowValidation(diagnostics, filePath, Math.Max(1, token.Span.Line), Math.Max(1, token.Span.Column), token.Span, "UP2106", $"Invalid token span for '{token.RuleName}'.");
        }

        if (!definition.AllRules.ContainsKey(token.RuleName) && !definition.DeclaredTokens.Contains(token.RuleName) && token.RuleName != "ERROR")
        {
            ThrowValidation(diagnostics, filePath, token.Span.Line, token.Span.Column, token.Span, "UP2100", $"Unknown emitted token '{token.RuleName}'.");
        }

        if (!definition.DeclaredChannels.Contains(token.Channel) && token.Channel is not (DefaultChannel or HiddenChannel))
        {
            ThrowValidation(diagnostics, filePath, token.Span.Line, token.Span.Column, token.Span, "UP2101", $"Unknown channel '{token.Channel}'.");
        }
    }

    private (SourceSpan? Span, Rule? Rule, List<Model.LexerCommand> Commands) MatchLongest(TextReaderBuffer input, LexerMode mode, string? filePath)
    {
        SourceSpan? best = null;
        Rule? bestRule = null;
        List<Model.LexerCommand> bestCommands = [];

        if (_keywordTrieByMode.TryGetValue(mode.Name, out var trie) && !trie.IsEmpty)
        {
            var (span, rule) = trie.TryMatch(input, mode.Name, filePath);
            if (span is not null)
            {
                best = span;
                bestRule = rule;
            }
        }

        foreach (var rule in mode.Rules.OrderBy(static r => r.DeclarationOrder))
        {
            if (rule.IsFragment || IsPureLiteralRule(rule))
            {
                continue;
            }

            var commands = new List<Model.LexerCommand>();
            if (TryMatchContent(input, rule.Content, 0, commands, out int length) && length > 0)
            {
                if (best is null || length > best.Length)
                {
                    best = new SourceSpan(input.Position, length, input.Line, input.Column, filePath);
                    bestRule = rule;
                    bestCommands = commands;
                }
            }
        }

        return (best, bestRule, bestCommands);
    }

    private bool TryMatchContent(TextReaderBuffer input, RuleContent content, int offset, List<Model.LexerCommand>? commands, out int consumed)
    {
        switch (content)
        {
            case LiteralMatch lit:
                consumed = TryMatchLiteral(input, lit.Value, offset, _caseInsensitive) ? lit.Value.Length : 0;
                return consumed > 0 || lit.Value.Length == 0;

            case RangeMatch range:
                int rangeCh = input.Peek(offset);
                if (rangeCh >= 0 && IsCharInRange((char)rangeCh, range.From, range.To, _caseInsensitive))
                {
                    consumed = 1;
                    return true;
                }

                consumed = 0;
                return false;

            case CharSetMatch set:
                int setCh = input.Peek(offset);
                if (setCh >= 0)
                {
                    bool inSet = IsCharInSet(set.Chars, (char)setCh, _caseInsensitive);
                    if (set.Negated ? !inSet : inSet)
                    {
                        consumed = 1;
                        return true;
                    }
                }

                consumed = 0;
                return false;

            case AnyChar:
                consumed = input.Peek(offset) >= 0 ? 1 : 0;
                return consumed == 1;

            case RuleRef ruleRef:
                if (definition.AllRules.TryGetValue(ruleRef.RuleName, out var referenced))
                {
                    var key = (ruleRef.RuleName, offset);
                    if (!_activeRuleRefs.Add(key))
                    {
                        consumed = 0;
                        return false;
                    }

                    try
                    {
                        return TryMatchContent(input, referenced.Content, offset, commands, out consumed);
                    }
                    finally
                    {
                        _activeRuleRefs.Remove(key);
                    }
                }

                consumed = 0;
                return false;

            case Sequence seq:
                int total = 0;
                foreach (RuleContent item in seq.Items)
                {
                    if (!TryMatchContent(input, item, offset + total, commands, out int part))
                    {
                        consumed = 0;
                        return false;
                    }

                    total += part;
                }

                consumed = total;
                return true;

            case Alternation alternation:
                foreach (Alternative alt in alternation.Alternatives)
                {
                    var branch = commands is null ? null : new List<Model.LexerCommand>();
                    if (TryMatchContent(input, alt.Content, offset, branch, out int altLen))
                    {
                        commands?.AddRange(branch!);
                        consumed = altLen;
                        return true;
                    }
                }

                consumed = 0;
                return false;

            case Alternative alternative:
                return TryMatchContent(input, alternative.Content, offset, commands, out consumed);

            case Quantifier quant:
                return TryMatchQuantifier(input, quant, offset, commands, out consumed);

            case Negation neg:
                if (!TryMatchContent(input, neg.Inner, offset, null, out _) && input.Peek(offset) >= 0)
                {
                    consumed = 1;
                    return true;
                }

                consumed = 0;
                return false;

            case Model.LexerCommand cmd:
                commands?.Add(cmd);
                consumed = 0;
                return true;

            case EmbeddedAction:
                consumed = 0;
                return true;

            default:
                consumed = 0;
                return false;
        }
    }

    private bool TryMatchQuantifier(TextReaderBuffer input, Quantifier quant, int offset, List<Model.LexerCommand>? commands, out int consumed)
    {
        int count = 0;
        int total = 0;

        while (quant.Max is null || count < quant.Max.Value)
        {
            if (!TryMatchContent(input, quant.Inner, offset + total, commands, out int inner))
            {
                break;
            }

            if (inner == 0)
            {
                break;
            }

            total += inner;
            count++;
            if (!quant.Greedy && count >= quant.Min)
            {
                break;
            }
        }

        consumed = total;
        return count >= quant.Min;
    }

    private bool ExecuteLexerCommands(List<Model.LexerCommand> commands, ref Token token, DiagnosticBag? diagnostics, string? filePath)
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
                        if (mode is null)
                        {
                            diagnostics?.AddWithContext(ParserDiagnostics.UnknownLexerMode, token.Span.Position, token.Span.Length, null, null, cmd.Argument, "pushMode");
                        }
                        else
                        {
                            _modeStack.Push(mode);
                        }
                    }

                    break;
                case LexerCommandType.PopMode:
                    if (_modeStack.Count > 1)
                    {
                        _modeStack.Pop();
                    }

                    break;
                case LexerCommandType.Mode:
                    if (cmd.Argument is not null)
                    {
                        var mode = definition.Modes.FirstOrDefault(m => m.Name == cmd.Argument);
                        if (mode is null)
                        {
                            diagnostics?.AddWithContext(ParserDiagnostics.UnknownLexerMode, token.Span.Position, token.Span.Length, null, null, cmd.Argument, "mode");
                        }
                        else
                        {
                            _modeStack.Pop();
                            _modeStack.Push(mode);
                        }
                    }

                    break;
                case LexerCommandType.Channel:
                    if (cmd.Argument is not null)
                    {
                        token = token with { Channel = cmd.Argument };
                    }

                    break;
                case LexerCommandType.Type:
                    if (cmd.Argument is not null)
                    {
                        token = token with { RuleName = cmd.Argument };
                    }

                    break;
            }
        }

        if (more)
        {
            if (_moreStartPos < 0)
            {
                _moreStartPos = token.Span.Position;
            }

            _moreTextBuilder.Append(token.Text);
            return true;
        }

        return skip;
    }

    private LexerMode GetDefaultMode() =>
        definition.Modes.FirstOrDefault(static mode => mode.Name == "DEFAULT_MODE") ?? definition.Modes[0];

    private static bool IsPureLiteralRule(Rule rule) => rule.Content.Alternatives.All(static alternative => alternative.Content is LiteralMatch);

    private static bool TryMatchLiteral(TextReaderBuffer stream, string value, int offset, bool caseInsensitive)
    {
        for (int i = 0; i < value.Length; i++)
        {
            int source = stream.Peek(offset + i);
            if (source < 0 || !CharsEqual((char)source, value[i], caseInsensitive))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CharsEqual(char left, char right, bool caseInsensitive) =>
        caseInsensitive ? char.ToUpperInvariant(left) == char.ToUpperInvariant(right) : left == right;

    private static bool IsCharInRange(char value, char start, char end, bool caseInsensitive)
    {
        if (!caseInsensitive)
        {
            return value >= start && value <= end;
        }

        char normalizedValue = char.ToUpperInvariant(value);
        char normalizedStart = char.ToUpperInvariant(start);
        char normalizedEnd = char.ToUpperInvariant(end);
        return normalizedValue >= normalizedStart && normalizedValue <= normalizedEnd;
    }

    private static bool IsCharInSet(IReadOnlySet<char> chars, char value, bool caseInsensitive)
    {
        if (chars.Contains(value))
        {
            return true;
        }

        if (!caseInsensitive)
        {
            return false;
        }

        return chars.Contains(char.ToUpperInvariant(value)) || chars.Contains(char.ToLowerInvariant(value));
    }

    private static IReadOnlyDictionary<string, KeywordLookup> BuildKeywordTries(ParserDefinition parserDefinition, bool caseInsensitive)
    {
        var result = new Dictionary<string, KeywordLookup>(StringComparer.Ordinal);
        foreach (var mode in parserDefinition.Modes)
        {
            var trie = new KeywordLookup(caseInsensitive);
            foreach (var rule in mode.Rules)
            {
                if (rule.IsFragment || !IsPureLiteralRule(rule))
                {
                    continue;
                }

                foreach (var alt in rule.Content.Alternatives)
                {
                    trie.Add(((LiteralMatch)alt.Content).Value, rule);
                }
            }

            result[mode.Name] = trie;
        }

        return result;
    }

    private static string FormatError(string? filePath, int line, int column, string code, string message)
    {
        var path = string.IsNullOrWhiteSpace(filePath) ? "<input>" : filePath;
        return $"{path}({line},{column}): error {code}: {message}";
    }

    private static string BuildText(TextReaderBuffer input, int length)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        var chars = new char[length];
        for (int index = 0; index < length; index++)
        {
            int value = input.Peek(index);
            chars[index] = value < 0 ? '\0' : (char)value;
        }

        return new string(chars);
    }

    private static void ThrowValidation(
        DiagnosticBag? diagnostics,
        string? filePath,
        int line,
        int column,
        SourceSpan? span,
        string code,
        string message)
    {
        string formatted = FormatError(filePath, line, column, code, message);
        diagnostics?.AddWithContext(
            ParserDiagnostics.ParseFailure,
            span?.Position,
            span?.Length,
            null,
            null,
            formatted);
        throw new LexerValidationException(formatted);
    }
}
