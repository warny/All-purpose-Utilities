using System.Collections.Generic;
using System.Text;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Hand-written tokenizer for ANTLR4 .g4 grammar files.
/// Produces a flat list of <see cref="G4Token"/> values.
/// </summary>
internal sealed class G4Tokenizer
{
    private readonly string _text;
    private int _pos;
    private int _line = 1;
    private int _column;

    public G4Tokenizer(string text) => _text = text;

    // ── Public entry point ───────────────────────────────────────────

    public List<G4Token> Tokenize()
    {
        var tokens = new List<G4Token>();

        while (true)
        {
            SkipWhitespaceAndComments();

            if (_pos >= _text.Length)
            {
                tokens.Add(new G4Token(G4TokenKind.Eof, "", _line, _column));
                break;
            }

            char c = _text[_pos];

            if (c == '\'')
            {
                tokens.Add(ReadStringLiteral());
            }
            else if (c == '[')
            {
                bool isRuleLocalsClause = tokens.Count > 0
                    && tokens[tokens.Count - 1].Kind == G4TokenKind.Identifier
                    && string.Equals(tokens[tokens.Count - 1].Value, "locals", System.StringComparison.Ordinal);
                tokens.Add(ReadBracketBlock(isRuleLocalsClause));
            }
            else if (c == '.' && Peek(1) == '.')
            {
                tokens.Add(new G4Token(G4TokenKind.DotDot, "..", _line, _column));
                Advance();
                Advance();
            }
            else if (c == '-' && Peek(1) == '>')
            {
                tokens.Add(new G4Token(G4TokenKind.Arrow, "->", _line, _column));
                Advance();
                Advance();
            }
            else if (c == '{')
            {
                tokens.Add(ReadBraceBlock());
            }
            else if (IsIdentStart(c))
            {
                tokens.Add(ReadIdentifier());
            }
            else
            {
                var kind = c switch
                {
                    ';' => G4TokenKind.Semi,
                    ':' => G4TokenKind.Colon,
                    '|' => G4TokenKind.Pipe,
                    '(' => G4TokenKind.LParen,
                    ')' => G4TokenKind.RParen,
                    '*' => G4TokenKind.Star,
                    '+' => G4TokenKind.Plus,
                    '?' => G4TokenKind.QMark,
                    '~' => G4TokenKind.Tilde,
                    '#' => G4TokenKind.Hash,
                    '.' => G4TokenKind.Dot,
                    ',' => G4TokenKind.Comma,
                    '=' => G4TokenKind.Equal,
                    '@' => G4TokenKind.At,
                    '}' => G4TokenKind.RBrace,
                    _   => (G4TokenKind)(-1)
                };

                if ((int)kind >= 0)
                    tokens.Add(new G4Token(kind, c.ToString(), _line, _column));
                // else: unknown character — silently skip
                Advance();
            }
        }

        return tokens;
    }

    // ── Skip helpers ─────────────────────────────────────────────────

    private void SkipWhitespaceAndComments()
    {
        while (_pos < _text.Length)
        {
            char c = _text[_pos];

            if (c == '\n') { Advance(); continue; }
            if (c == '\r') { Advance(); continue; }
            if (char.IsWhiteSpace(c)) { Advance(); continue; }

            // Line comment
            if (c == '/' && Peek(1) == '/')
            {
                while (_pos < _text.Length && _text[_pos] != '\n')
                    Advance();
                continue;
            }

            // Block comment
            if (c == '/' && Peek(1) == '*')
            {
                Advance();
                Advance();
                while (_pos < _text.Length - 1)
                {

                    if (_text[_pos] == '*' && _text[_pos + 1] == '/')
                    {
                        Advance();
                        Advance();
                        break;
                    }
                    Advance();
                }
                continue;
            }

            break;
        }
    }

    // ── Token readers ────────────────────────────────────────────────

    /// <summary>Reads a single-quoted string literal, interpreting common escapes.</summary>
    private G4Token ReadStringLiteral()
    {
        int startLine = _line;
        int startColumn = _column;
        Advance(); // skip opening '
        var sb = new StringBuilder();

        while (_pos < _text.Length && _text[_pos] != '\'')
        {
            if (_text[_pos] == '\\' && _pos + 1 < _text.Length)
            {
                Advance();
                sb.Append(DecodeEscape(_text[_pos]));
                Advance();
            }
            else
            {

                sb.Append(_text[_pos]);
                Advance();
            }
        }

        if (_pos < _text.Length) Advance(); // skip closing '

        return new G4Token(G4TokenKind.StringLiteral, sb.ToString(), startLine, startColumn);
    }

    /// <summary>
    /// Reads a bracket-delimited block, preserving the raw inner content so the parser can interpret character
    /// ranges or rule-local declarations without losing nested array brackets.
    /// </summary>
    /// <param name="balanced">Whether nested brackets should be balanced before closing the block.</param>
    /// <returns>A token containing the raw bracket-block content without the outer brackets.</returns>
    private G4Token ReadBracketBlock(bool balanced)
    {
        int startLine = _line;
        int startColumn = _column;
        Advance(); // skip [
        int depth = 1;
        var sb = new StringBuilder();

        while (_pos < _text.Length && depth > 0)
        {
            char current = _text[_pos];
            if (current == '\\' && _pos + 1 < _text.Length)
            {
                sb.Append('\\');
                Advance();
                sb.Append(_text[_pos]);
                Advance();
                continue;
            }

            if (balanced && current == '[')
            {
                depth++;
                sb.Append(_text[_pos]);
                Advance();
                continue;
            }

            if (current == ']')
            {
                depth--;
                Advance();
                if (depth == 0)
                {
                    break;
                }

                sb.Append(current);
                continue;
            }

            sb.Append(_text[_pos]);
            Advance();
        }

        return new G4Token(G4TokenKind.CharClass, sb.ToString(), startLine, startColumn);
    }

    /// <summary>Reads a balanced <c>{ ... }</c> block (embedded action or predicate).</summary>
    private G4Token ReadBraceBlock()
    {
        int startLine = _line;
        int startColumn = _column;
        Advance(); // skip {
        var sb = new StringBuilder();
        int depth = 1;

        while (_pos < _text.Length && depth > 0)
        {
            char c = _text[_pos];
            if (c == '{') depth++;
            else if (c == '}') { depth--; if (depth == 0) { Advance(); break; } }
            sb.Append(c);
            Advance();
        }

        return new G4Token(G4TokenKind.BraceBlock, sb.ToString(), startLine, startColumn);
    }

    /// <summary>Reads an identifier (keyword or rule name).</summary>
    private G4Token ReadIdentifier()
    {
        int startLine = _line;
        int startColumn = _column;
        int start = _pos;

        while (_pos < _text.Length && IsIdentContinue(_text[_pos]))
            Advance();

        return new G4Token(G4TokenKind.Identifier, _text.Substring(start, _pos - start), startLine, startColumn);
    }

    // ── Character helpers ────────────────────────────────────────────

    private void Advance()
    {
        if (_pos >= _text.Length) return;
        if (_text[_pos] == '\n') { _line++; _column = 0; }
        else { _column++; }
        _pos++;
    }

    private char Peek(int offset) =>
        _pos + offset < _text.Length ? _text[_pos + offset] : '\0';

    private static bool IsIdentStart(char c)    => char.IsLetter(c) || c == '_';
    private static bool IsIdentContinue(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>Decodes a single escape character following a backslash.</summary>
    private static string DecodeEscape(char c) => c switch
    {
        'n'  => "\n",
        'r'  => "\r",
        't'  => "\t",
        'b'  => "\b",
        'f'  => "\f",
        '\'' => "'",
        '"'  => "\"",
        '\\' => "\\",
        _    => c.ToString()
    };
}
