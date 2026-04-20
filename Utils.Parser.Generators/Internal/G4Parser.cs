using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Parser.Diagnostics;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Recursive-descent parser for ANTLR4 .g4 grammar files.
/// Produces a <see cref="G4Grammar"/> AST without depending on Utils.Parser.
/// </summary>
internal sealed class G4Parser
{
    private readonly List<G4Token> _tokens;
    private readonly DiagnosticBag _diagnostics;
    private int _pos;

    public G4Parser(List<G4Token> tokens, DiagnosticBag? diagnostics = null)
    {
        _tokens = tokens;
        _diagnostics = diagnostics ?? new DiagnosticBag();
    }

    // ── Public entry point ───────────────────────────────────────────

    /// <summary>Parses the full token stream into a <see cref="G4Grammar"/>.</summary>
    public G4Grammar Parse()
    {
        var grammar = new G4Grammar();

        // ── grammarDecl ──────────────────────────────────────────────
        if (PeekValue("lexer"))
        {
            Consume(); Expect("grammar");
            grammar.Kind = G4GrammarKind.Lexer;
        }
        else if (PeekValue("parser"))
        {
            Consume(); Expect("grammar");
            grammar.Kind = G4GrammarKind.Parser;
        }
        else
        {
            Expect("grammar");
            grammar.Kind = G4GrammarKind.Combined;
        }

        grammar.Name = ExpectIdentifier();
        Expect(G4TokenKind.Semi);

        // ── prequelConstructs & rules ────────────────────────────────
        G4LexerMode? currentMode = null;

        while (!AtEof())
        {
            // options { ... }
            if (PeekValue("options")) { ParseOptionsBlock(grammar.Options); continue; }

            // tokens { ... } — skip
            if (PeekValue("tokens")) { _diagnostics.Add(ParserDiagnostics.TokensBlockIgnored); SkipBlock(); continue; }

            // @ action — skip
            if (Peek().Kind == G4TokenKind.At) { _diagnostics.Add(ParserDiagnostics.ActionIgnored, "@action"); SkipAction(); continue; }

            // import — skip line
            if (PeekValue("import")) { _diagnostics.Add(ParserDiagnostics.ImportParsedButNotResolved, "import"); while (!AtEof() && Peek().Kind != G4TokenKind.Semi) Consume(); TryConsume(G4TokenKind.Semi); continue; }

            // channels { ... } — skip
            if (PeekValue("channels")) { _diagnostics.Add(ParserDiagnostics.ChannelsBlockIgnored); SkipBlock(); continue; }

            // mode Name; — starts a new lexer mode
            if (PeekValue("mode"))
            {
                Consume();
                var modeName = ExpectIdentifier();
                TryConsume(G4TokenKind.Semi);
                currentMode = new G4LexerMode { Name = modeName };
                grammar.ExtraModes.Add(currentMode);
                continue;
            }

            // Rule declaration
            if (Peek().Kind == G4TokenKind.Identifier)
            {
                var rule = ParseRule();
                bool isLexer = char.IsUpper(rule.Name[0]) || rule.IsFragment;

                if (grammar.Kind == G4GrammarKind.Parser)
                    grammar.ParserRules.Add(rule);
                else if (isLexer)
                {
                    if (currentMode == null) grammar.LexerRules.Add(rule);
                    else                     currentMode.Rules.Add(rule);
                }
                else
                {
                    grammar.ParserRules.Add(rule);
                }
                continue;
            }

            Consume(); // skip unexpected token
        }

        return grammar;
    }

    // ── Rule ─────────────────────────────────────────────────────────

    private G4Rule ParseRule()
    {
        var rule = new G4Rule();

        if (PeekValue("fragment"))
        {
            rule.IsFragment = true;
            Consume();
        }

        rule.Name = ExpectIdentifier();

        // Skip optional rule options/returns/throws/locals before ':'
        while (!AtEof() && Peek().Kind != G4TokenKind.Colon)
            Consume();

        Expect(G4TokenKind.Colon);

        rule.Content = ParseAlternation();

        Expect(G4TokenKind.Semi);

        return rule;
    }

    // ── Alternation ──────────────────────────────────────────────────

    private G4Alternation ParseAlternation()
    {
        var alternation = new G4Alternation();
        int priority = 0;

        alternation.Alternatives.Add(ParseAlternative(priority++));

        while (Peek().Kind == G4TokenKind.Pipe)
        {
            Consume(); // |
            alternation.Alternatives.Add(ParseAlternative(priority++));
        }

        return alternation;
    }

    private G4Alternative ParseAlternative(int priority)
    {
        var alt = new G4Alternative { Priority = priority };

        while (!AtAltEnd())
        {
            var item = TryParseElement();
            if (item == null) break;
            alt.Items.Add(item);
        }

        // Optional alternative label #Name
        if (Peek().Kind == G4TokenKind.Hash)
        {
            Consume();
            if (Peek().Kind == G4TokenKind.Identifier)
                alt.Label = Consume().Value;
        }

        return alt;
    }

    // ── Element ───────────────────────────────────────────────────────

    private G4Content? TryParseElement()
    {
        // Embedded action or predicate: { ... } or { ... }?
        if (Peek().Kind == G4TokenKind.BraceBlock)
        {
            var code = Consume().Value;
            bool isPred = Peek().Kind == G4TokenKind.QMark;
            if (isPred) Consume();
            if (isPred) _diagnostics.Add(ParserDiagnostics.SemanticPredicateNotEnforced);
            else _diagnostics.Add(ParserDiagnostics.InlineActionStoredNotExecuted);
            return new G4EmbeddedAction { Code = code, IsPredicate = isPred };
        }

        // Lexer command: ->
        if (Peek().Kind == G4TokenKind.Arrow)
        {
            return ParseLexerCommand();
        }

        // Negation: ~
        G4Content atom;
        if (Peek().Kind == G4TokenKind.Tilde)
        {
            Consume();
            var inner = ParseAtom();
            if (inner == null) return null;
            atom = new G4Negation { Inner = inner };
        }
        else
        {
            atom = ParseAtom()!;
            if (atom == null) return null;
        }

        // Optional quantifier: * + ? *? +? ??
        return WrapWithQuantifier(atom);
    }

    private G4Content? ParseAtom()
    {
        var tok = Peek();

        // String literal — may be followed by '..' for a range
        if (tok.Kind == G4TokenKind.StringLiteral)
        {
            Consume();
            string value = tok.Value;

            if (Peek().Kind == G4TokenKind.DotDot)
            {
                Consume(); // ..
                var toTok = ExpectToken(G4TokenKind.StringLiteral);
                char from = value.Length > 0 ? value[0] : '\0';
                char to   = toTok.Length > 0 ? toTok[0] : '\0';
                return new G4RangeMatch { From = from, To = to };
            }

            return new G4LiteralMatch { Value = value };
        }

        // Character class [...]
        if (tok.Kind == G4TokenKind.CharClass)
        {
            Consume();
            return ParseCharClass(tok.Value);
        }

        // Dot → AnyChar
        if (tok.Kind == G4TokenKind.Dot)
        {
            Consume();
            return new G4AnyCharMatch();
        }

        // Group (...)
        if (tok.Kind == G4TokenKind.LParen)
        {
            Consume();
            var inner = ParseAlternation();
            TryConsume(G4TokenKind.RParen);

            // If a single alternative with a single item, unwrap to avoid redundant wrapping
            if (inner.Alternatives.Count == 1 && inner.Alternatives[0].Items.Count == 1)
                return inner.Alternatives[0].Items[0];

            return inner;
        }

        // Rule reference or keyword-as-reference
        if (tok.Kind == G4TokenKind.Identifier && !IsKeyword(tok.Value))
        {
            Consume();
            return new G4RuleRef { RuleName = tok.Value };
        }

        return null;
    }

    private G4Content WrapWithQuantifier(G4Content atom)
    {
        var next = Peek().Kind;

        if (next == G4TokenKind.Star || next == G4TokenKind.Plus || next == G4TokenKind.QMark)
        {
            Consume();
            bool greedy = true;
            if (Peek().Kind == G4TokenKind.QMark) { Consume(); greedy = false; }

            int min = next == G4TokenKind.Plus ? 1 : 0;
            int? max = next == G4TokenKind.QMark ? 1 : (int?)null;
            return new G4Quantifier { Inner = atom, Min = min, Max = max, Greedy = greedy };
        }

        return atom;
    }

    // ── Char class ────────────────────────────────────────────────────

    private G4CharClassMatch ParseCharClass(string raw)
    {
        var result = new G4CharClassMatch();
        int i = 0;

        if (i < raw.Length && raw[i] == '^')
        {
            result.Negated = true;
            i++;
        }

        while (i < raw.Length)
        {
            char lo = ReadCharClassChar(raw, ref i);
            if (i < raw.Length - 1 && raw[i] == '-' && raw[i + 1] != ']')
            {
                i++; // skip '-'
                char hi = ReadCharClassChar(raw, ref i);
                result.Entries.Add((lo, hi));
            }
            else
            {
                result.Entries.Add((lo, null));
            }
        }

        return result;
    }

    private char ReadCharClassChar(string raw, ref int i)
    {
        if (i >= raw.Length) return '\0';

        if (raw[i] == '\\' && i + 1 < raw.Length)
        {
            i++;
            char escaped = raw[i++];
            return escaped switch
            {
                'n'  => '\n',
                'r'  => '\r',
                't'  => '\t',
                'b'  => '\b',
                'f'  => '\f',
                '\'' => '\'',
                '"'  => '"',
                '\\' => '\\',
                '-'  => '-',
                ']'  => ']',
                _    => escaped
            };
        }

        return raw[i++];
    }

    // ── Lexer command ────────────────────────────────────────────────

    private G4LexerCommand ParseLexerCommand()
    {
        Consume(); // ->
        var name = ExpectIdentifier();
        string? arg = null;

        if (TryConsume(G4TokenKind.LParen))
        {
            if (Peek().Kind == G4TokenKind.Identifier)
                arg = Consume().Value;
            TryConsume(G4TokenKind.RParen);
            if (arg is null)
                _diagnostics.Add(ParserDiagnostics.FallbackStrategyUsed, $"Lexer command '{name}' without identifier argument.");
        }

        return new G4LexerCommand { Name = name, Arg = arg };
    }

    // ── Skip helpers ─────────────────────────────────────────────────

    /// <summary>Skips <c>keyword { ... }</c>.</summary>
    private void SkipBlock()
    {
        Consume(); // keyword
        if (Peek().Kind == G4TokenKind.BraceBlock)
            Consume();
    }

    /// <summary>Parses <c>options { key=value; }</c> into the provided dictionary.</summary>
    private void ParseOptionsBlock(IDictionary<string, string> options)
    {
        Consume(); // options

        if (Peek().Kind != G4TokenKind.BraceBlock)
        {
            SkipBlock();
            return;
        }

        var rawBlock = Consume().Value;
        foreach (var optionDeclaration in rawBlock.Split(';'))
        {
            var trimmedDeclaration = optionDeclaration.Trim();
            if (trimmedDeclaration.Length == 0)
                continue;

            int separatorIndex = trimmedDeclaration.IndexOf('=');
            if (separatorIndex < 0)
                continue;

            var key = trimmedDeclaration.Substring(0, separatorIndex).Trim();
            var value = trimmedDeclaration.Substring(separatorIndex + 1).Trim();

            if (key.Length == 0)
                continue;

            if (value.Length >= 2 && value.First() == '\'' && value.Last() == '\'')
                value = value.Substring(1, value.Length - 2);

            options[key] = value;
        }
    }

    /// <summary>Skips <c>@ name { ... }</c>.</summary>
    private void SkipAction()
    {
        Consume(); // @
        while (!AtEof() && Peek().Kind != G4TokenKind.BraceBlock && Peek().Kind != G4TokenKind.LBrace)
            Consume();
        if (!AtEof())
            Consume(); // the block
    }

    // ── Token helpers ────────────────────────────────────────────────

    private G4Token Peek() => _pos < _tokens.Count ? _tokens[_pos] : new G4Token(G4TokenKind.Eof, "", 0);

    private G4Token Consume()
    {
        var t = Peek();
        if (_pos < _tokens.Count) _pos++;
        return t;
    }

    private void Expect(G4TokenKind kind)
    {
        if (Peek().Kind == kind)
        {
            Consume();
            return;
        }

        _diagnostics.Add(ParserDiagnostics.ExpectedTokenMissing, kind.ToString());
        _diagnostics.Add(ParserDiagnostics.BestEffortRecoveryUsed, kind.ToString());
    }

    private void Expect(string keyword)
    {
        if (Peek().Kind == G4TokenKind.Identifier && Peek().Value == keyword)
            Consume();
        else
        {
            _diagnostics.Add(ParserDiagnostics.ExpectedTokenMissing, keyword);
            _diagnostics.Add(ParserDiagnostics.BestEffortRecoveryUsed, keyword);
        }
    }

    private string ExpectIdentifier()
    {
        if (Peek().Kind == G4TokenKind.Identifier)
            return Consume().Value;
        _diagnostics.Add(ParserDiagnostics.ExpectedTokenMissing, "Identifier");
        _diagnostics.Add(ParserDiagnostics.BestEffortRecoveryUsed, "Identifier");
        return "";
    }

    private string ExpectToken(G4TokenKind kind)
    {
        if (Peek().Kind == kind) return Consume().Value;
        _diagnostics.Add(ParserDiagnostics.ExpectedTokenMissing, kind.ToString());
        _diagnostics.Add(ParserDiagnostics.BestEffortRecoveryUsed, kind.ToString());
        return "";
    }

    private bool TryConsume(G4TokenKind kind)
    {
        if (Peek().Kind == kind) { Consume(); return true; }
        return false;
    }

    private bool PeekValue(string value) =>
        Peek().Kind == G4TokenKind.Identifier && Peek().Value == value;

    private bool AtEof() => Peek().Kind == G4TokenKind.Eof;

    /// <summary>
    /// Returns true when the current position is at the end of a grammar alternative:
    /// pipe, semicolon, right-paren, EOF.
    /// </summary>
    private bool AtAltEnd()
    {
        var k = Peek().Kind;
        return k == G4TokenKind.Pipe
            || k == G4TokenKind.Semi
            || k == G4TokenKind.RParen
            || k == G4TokenKind.Eof;
    }

    private static bool IsKeyword(string value) =>
        value == "grammar"  || value == "lexer"    || value == "parser"   ||
        value == "fragment" || value == "options"  || value == "tokens"   ||
        value == "channels" || value == "import"   || value == "mode"     ||
        value == "returns"  || value == "throws"   || value == "locals"   ||
        value == "catch"    || value == "finally";
}
