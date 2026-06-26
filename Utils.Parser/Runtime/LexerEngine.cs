using System.IO;
using System.Text;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Source;

namespace Utils.Parser.Runtime;

/// <summary>
/// Converts a <see cref="TextReader"/> into a sequence of <see cref="Token"/> values
/// using the lexer rules in a <see cref="ParserDefinition"/>.
/// </summary>
/// <remarks>
/// This class is not thread-safe. Each concurrent tokenization must use a separate
/// <see cref="LexerEngine"/> instance.
/// </remarks>
public sealed class LexerEngine(ParserDefinition definition, ParserRuntimeFeaturePolicy? runtimeFeaturePolicy = null)
{
    private readonly ParserRuntimeFeaturePolicy _runtimeFeaturePolicy = runtimeFeaturePolicy ?? ParserRuntimeFeaturePolicy.Default;
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

            var (match, rule, commands, actions) = MatchLongest(input, mode, filePath);
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

            ExecuteLexerActions(actions);
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

    private (SourceSpan? Span, Rule? Rule, List<Model.LexerCommand> Commands, List<LexerActionOccurrence> Actions) MatchLongest(TextReaderBuffer input, LexerMode mode, string? filePath)
    {
        SourceSpan? best = null;
        Rule? bestRule = null;
        List<Model.LexerCommand> bestCommands = [];
        List<LexerActionOccurrence> bestActions = [];

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
            var actions = new List<LexerActionOccurrence>();
            if (TryMatchContent(input, rule.Content, 0, commands, actions, rule, out int length) && length > 0)
            {
                if (best is null || length > best.Length)
                {
                    best = new SourceSpan(input.Position, length, input.Line, input.Column, filePath);
                    bestRule = rule;
                    bestCommands = commands;
                    bestActions = actions;
                }
            }
        }

        return (best, bestRule, bestCommands, bestActions);
    }

    private readonly record struct LexerMatchResult(bool Matched, int Consumed);

    private delegate LexerMatchResult ContentMatcher(
        LexerEngine engine, TextReaderBuffer input, RuleContent content,
        int offset, List<Model.LexerCommand>? commands, List<LexerActionOccurrence>? actions, Rule? rule);

    /// <summary>Maps each concrete <see cref="RuleContent"/> type to its lexer match handler.</summary>
    private static readonly Dictionary<Type, ContentMatcher> ContentMatchers = new()
    {
        [typeof(LiteralMatch)] = static (e, input, c, offset, _, _, _) =>
            e.MatchLiteralContent(input, (LiteralMatch)c, offset),
        [typeof(RangeMatch)] = static (e, input, c, offset, _, _, _) =>
            e.MatchRangeContent(input, (RangeMatch)c, offset),
        [typeof(CharSetMatch)] = static (e, input, c, offset, _, _, _) =>
            e.MatchCharSetContent(input, (CharSetMatch)c, offset),
        [typeof(AnyChar)] = static (_, input, _, offset, _, _, _) =>
            MatchAnyCharContent(input, offset),
        [typeof(RuleRef)] = static (e, input, c, offset, commands, actions, _) =>
            e.MatchRuleRefContent(input, (RuleRef)c, offset, commands, actions),
        [typeof(Sequence)] = static (e, input, c, offset, commands, actions, rule) =>
            e.MatchSequenceContent(input, (Sequence)c, offset, commands, actions, rule),
        [typeof(Alternation)] = static (e, input, c, offset, commands, actions, rule) =>
            e.MatchAlternationContent(input, (Alternation)c, offset, commands, actions, rule),
        [typeof(Alternative)] = static (e, input, c, offset, commands, actions, rule) =>
            e.MatchAlternativeContent(input, (Alternative)c, offset, commands, actions, rule),
        [typeof(Quantifier)] = static (e, input, c, offset, commands, actions, rule) =>
            e.MatchQuantifierContent(input, (Quantifier)c, offset, commands, actions, rule),
        [typeof(Negation)] = static (e, input, c, offset, _, _, _) =>
            e.MatchNegationContent(input, (Negation)c, offset),
        [typeof(LexerCommand)] = static (_, _, c, _, commands, _, _) =>
            MatchLexerCommandContent((LexerCommand)c, commands),
        [typeof(EmbeddedAction)] = static (_, _, c, _, _, actions, rule) =>
            MatchEmbeddedActionContent((EmbeddedAction)c, actions, rule),
    };

    private bool TryMatchContent(TextReaderBuffer input, RuleContent content, int offset, List<Model.LexerCommand>? commands, List<LexerActionOccurrence>? actions, Rule? rule, out int consumed)
    {
        if (ContentMatchers.TryGetValue(content.GetType(), out var matcher))
        {
            var result = matcher(this, input, content, offset, commands, actions, rule);
            consumed = result.Consumed;
            return result.Matched;
        }

        consumed = 0;
        return false;
    }

    private bool TryMatchQuantifier(TextReaderBuffer input, Quantifier quant, int offset, List<Model.LexerCommand>? commands, List<LexerActionOccurrence>? actions, Rule? rule, out int consumed)
    {
        int count = 0;
        int total = 0;

        while (quant.Max is null || count < quant.Max.Value)
        {
            // Isolate each iteration so that commands from a failed attempt — or from
            // an attempt that matched zero characters — never reach the caller's list.
            var iterationCommands = commands is null ? null : new List<Model.LexerCommand>();
            var iterationActions = actions is null ? null : new List<LexerActionOccurrence>();
            if (!TryMatchContent(input, quant.Inner, offset + total, iterationCommands, iterationActions, rule, out int inner))
            {
                break;
            }

            if (inner == 0)
            {
                break;
            }

            commands?.AddRange(iterationCommands!);
            actions?.AddRange(iterationActions!);
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

    /// <summary>Matches a literal string at <paramref name="offset"/> using the current case-sensitivity setting.</summary>
    private LexerMatchResult MatchLiteralContent(TextReaderBuffer input, LiteralMatch lit, int offset)
    {
        int consumed = TryMatchLiteral(input, lit.Value, offset, _caseInsensitive) ? lit.Value.Length : 0;
        return new(consumed > 0 || lit.Value.Length == 0, consumed);
    }

    /// <summary>Matches a single character within an inclusive character range.</summary>
    private LexerMatchResult MatchRangeContent(TextReaderBuffer input, RangeMatch range, int offset)
    {
        int ch = input.Peek(offset);
        bool matched = ch >= 0 && IsCharInRange((char)ch, range.From, range.To, _caseInsensitive);
        return new(matched, matched ? 1 : 0);
    }

    /// <summary>Matches a single character that is (or is not, when negated) a member of an explicit set.</summary>
    private LexerMatchResult MatchCharSetContent(TextReaderBuffer input, CharSetMatch set, int offset)
    {
        int ch = input.Peek(offset);
        if (ch >= 0)
        {
            bool inSet = IsCharInSet(set.Chars, (char)ch, _caseInsensitive);
            if (set.Negated ? !inSet : inSet)
            {
                return new(true, 1);
            }
        }

        return new(false, 0);
    }

    /// <summary>Matches any single character (the <c>.</c> wildcard) at <paramref name="offset"/>.</summary>
    private static LexerMatchResult MatchAnyCharContent(TextReaderBuffer input, int offset)
    {
        int consumed = input.Peek(offset) >= 0 ? 1 : 0;
        return new(consumed == 1, consumed);
    }

    /// <summary>
    /// Resolves and matches a rule reference, guarding against direct left-recursion
    /// via the active-rule-refs set.
    /// </summary>
    private LexerMatchResult MatchRuleRefContent(TextReaderBuffer input, RuleRef ruleRef, int offset, List<Model.LexerCommand>? commands, List<LexerActionOccurrence>? actions)
    {
        if (!definition.AllRules.TryGetValue(ruleRef.RuleName, out var referenced))
        {
            return new(false, 0);
        }

        var key = (ruleRef.RuleName, offset);
        if (!_activeRuleRefs.Add(key))
        {
            return new(false, 0);
        }

        try
        {
            bool matched = TryMatchContent(input, referenced.Content, offset, commands, actions, referenced, out int consumed);
            return new(matched, consumed);
        }
        finally
        {
            _activeRuleRefs.Remove(key);
        }
    }

    /// <summary>
    /// Matches all items in a sequence in order.
    /// Commands are accumulated in a local buffer and flushed to <paramref name="commands"/>
    /// only when the entire sequence succeeds — ensuring that a partially-matched but
    /// ultimately failed sequence never leaks commands into the caller's list.
    /// </summary>
    private LexerMatchResult MatchSequenceContent(TextReaderBuffer input, Sequence seq, int offset, List<Model.LexerCommand>? commands, List<LexerActionOccurrence>? actions, Rule? rule)
    {
        // Accumulate into a local buffer so that commands from partially-matched
        // items are never visible to the caller when the sequence ultimately fails.
        // Only flush to `commands` when every item has succeeded.
        var local = commands is null ? null : new List<Model.LexerCommand>();
        var localActions = actions is null ? null : new List<LexerActionOccurrence>();
        int total = 0;
        foreach (RuleContent item in seq.Items)
        {
            if (!TryMatchContent(input, item, offset + total, local, localActions, rule, out int part))
            {
                return new(false, 0);
            }

            total += part;
        }

        commands?.AddRange(local!);
        actions?.AddRange(localActions!);
        return new(true, total);
    }

    /// <summary>
    /// Tries each alternative in order using an isolated branch buffer.
    /// The branch buffer is merged into <paramref name="commands"/> only when
    /// an alternative succeeds; failed alternatives are discarded without side-effects.
    /// </summary>
    private LexerMatchResult MatchAlternationContent(TextReaderBuffer input, Alternation alternation, int offset, List<Model.LexerCommand>? commands, List<LexerActionOccurrence>? actions, Rule? rule)
    {
        foreach (Alternative alt in alternation.Alternatives)
        {
            var branch = commands is null ? null : new List<Model.LexerCommand>();
            var branchActions = actions is null ? null : new List<LexerActionOccurrence>();
            if (TryMatchContent(input, alt.Content, offset, branch, branchActions, rule, out int altLen))
            {
                commands?.AddRange(branch!);
                actions?.AddRange(branchActions!);
                return new(true, altLen);
            }
        }

        return new(false, 0);
    }

    /// <summary>Delegates to the content of a single <see cref="Alternative"/>.</summary>
    private LexerMatchResult MatchAlternativeContent(TextReaderBuffer input, Alternative alternative, int offset, List<Model.LexerCommand>? commands, List<LexerActionOccurrence>? actions, Rule? rule)
    {
        bool matched = TryMatchContent(input, alternative.Content, offset, commands, actions, rule, out int consumed);
        return new(matched, consumed);
    }

    /// <summary>Delegates to <see cref="TryMatchQuantifier"/> and wraps the result.</summary>
    private LexerMatchResult MatchQuantifierContent(TextReaderBuffer input, Quantifier quant, int offset, List<Model.LexerCommand>? commands, List<LexerActionOccurrence>? actions, Rule? rule)
    {
        bool matched = TryMatchQuantifier(input, quant, offset, commands, actions, rule, out int consumed);
        return new(matched, consumed);
    }

    /// <summary>Implements the <c>~</c> negation operator: succeeds when the inner element does not match.</summary>
    private LexerMatchResult MatchNegationContent(TextReaderBuffer input, Negation neg, int offset)
    {
        if (!TryMatchContent(input, neg.Inner, offset, null, null, null, out _) && input.Peek(offset) >= 0)
        {
            return new(true, 1);
        }

        return new(false, 0);
    }

    /// <summary>Records a structured lexer command and always succeeds with zero consumed characters.</summary>
    private static LexerMatchResult MatchLexerCommandContent(LexerCommand cmd, List<Model.LexerCommand>? commands)
    {
        commands?.Add(cmd);
        return new(true, 0);
    }

    /// <summary>Records a lexer inline action for execution after the owning token rule is accepted.</summary>
    private static LexerMatchResult MatchEmbeddedActionContent(EmbeddedAction action, List<LexerActionOccurrence>? actions, Rule? rule)
    {
        if (rule is not null)
        {
            actions?.Add(new LexerActionOccurrence(rule, action.RawCode, -1, -1));
        }

        return new(true, 0);
    }

    /// <summary>Executes accepted lexer inline actions through the explicit runtime policy.</summary>
    private void ExecuteLexerActions(IReadOnlyList<LexerActionOccurrence> actions)
    {
        foreach (var action in actions)
        {
            _runtimeFeaturePolicy.LexerActionExecutor.Execute(new LexerActionExecutionContext(action.Rule, action.ActionCode, action.AlternativeIndex, action.ElementIndex));
        }
    }

    /// <summary>Describes a lexer action collected while matching an accepted token rule.</summary>
    private sealed record LexerActionOccurrence(Rule Rule, string ActionCode, int AlternativeIndex, int ElementIndex);

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
        int? spanStart = IsValidDiagnosticSpan(span) ? span.Position : null;
        int? spanLength = IsValidDiagnosticSpan(span) ? span.Length : null;

        diagnostics?.AddWithContext(
            ParserDiagnostics.ParseFailure,
            spanStart,
            spanLength,
            null,
            null,
            formatted);
        throw new LexerValidationException(formatted);
    }

    /// <summary>
    /// Determines whether a lexer source span can be safely converted to a diagnostic span.
    /// </summary>
    /// <param name="span">Optional lexer source span to validate.</param>
    /// <returns><see langword="true"/> when the span has non-negative offset and length values; otherwise, <see langword="false"/>.</returns>
    private static bool IsValidDiagnosticSpan(SourceSpan? span)
    {
        return span is not null && span.Position >= 0 && span.Length >= 0;
    }
}
