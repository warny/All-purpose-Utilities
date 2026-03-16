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
                tokens.Add(new G4Token(G4TokenKind.Eof, "", _line));
                break;
            }

            char c = _text[_pos];

            if (c == '\'')
            {
                tokens.Add(ReadStringLiteral());
            }
            else if (c == '[')
            {
                tokens.Add(ReadCharClass());
            }
            else if (c == '.' && Peek(1) == '.')
            {
                tokens.Add(new G4Token(G4TokenKind.DotDot, "..", _line));
                _pos += 2;
            }
            else if (c == '-' && Peek(1) == '>')
            {
                tokens.Add(new G4Token(G4TokenKind.Arrow, "->", _line));
                _pos += 2;
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
                    '@' => G4TokenKind.At,
                    '}' => G4TokenKind.RBrace,
                    _   => (G4TokenKind)(-1)
                };

                if ((int)kind >= 0)
                    tokens.Add(new G4Token(kind, c.ToString(), _line));
                // else: unknown character — silently skip
                _pos++;
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

            if (c == '\n') { _line++; _pos++; continue; }
            if (c == '\r') { _pos++; continue; }
            if (char.IsWhiteSpace(c)) { _pos++; continue; }

            // Line comment
            if (c == '/' && Peek(1) == '/')
            {
                while (_pos < _text.Length && _text[_pos] != '\n')
                    _pos++;
                continue;
            }

            // Block comment
            if (c == '/' && Peek(1) == '*')
            {
                _pos += 2;
                while (_pos < _text.Length - 1)
                {
                    if (_text[_pos] == '\n') _line++;
                    if (_text[_pos] == '*' && _text[_pos + 1] == '/')
                    {
                        _pos += 2;
                        break;
                    }
                    _pos++;
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
        _pos++; // skip opening '
        var sb = new StringBuilder();

        while (_pos < _text.Length && _text[_pos] != '\'')
        {
            if (_text[_pos] == '\\' && _pos + 1 < _text.Length)
            {
                _pos++;
                sb.Append(DecodeEscape(_text[_pos]));
                _pos++;
            }
            else
            {
                if (_text[_pos] == '\n') _line++;
                sb.Append(_text[_pos++]);
            }
        }

        if (_pos < _text.Length) _pos++; // skip closing '

        return new G4Token(G4TokenKind.StringLiteral, sb.ToString(), startLine);
    }

    /// <summary>
    /// Reads a character class <c>[...]</c>, preserving the raw inner content
    /// so the parser can interpret ranges and escaped chars.
    /// </summary>
    private G4Token ReadCharClass()
    {
        int startLine = _line;
        _pos++; // skip [
        var sb = new StringBuilder();

        while (_pos < _text.Length && _text[_pos] != ']')
        {
            if (_text[_pos] == '\\' && _pos + 1 < _text.Length)
            {
                sb.Append('\\');
                _pos++;
                sb.Append(_text[_pos++]);
            }
            else
            {
                if (_text[_pos] == '\n') _line++;
                sb.Append(_text[_pos++]);
            }
        }

        if (_pos < _text.Length) _pos++; // skip ]

        return new G4Token(G4TokenKind.CharClass, sb.ToString(), startLine);
    }

    /// <summary>Reads a balanced <c>{ ... }</c> block (embedded action or predicate).</summary>
    private G4Token ReadBraceBlock()
    {
        int startLine = _line;
        _pos++; // skip {
        var sb = new StringBuilder();
        int depth = 1;

        while (_pos < _text.Length && depth > 0)
        {
            char c = _text[_pos];
            if (c == '\n') _line++;
            if (c == '{') depth++;
            else if (c == '}') { depth--; if (depth == 0) { _pos++; break; } }
            sb.Append(c);
            _pos++;
        }

        return new G4Token(G4TokenKind.BraceBlock, sb.ToString(), startLine);
    }

    /// <summary>Reads an identifier (keyword or rule name).</summary>
    private G4Token ReadIdentifier()
    {
        int startLine = _line;
        int start = _pos;

        while (_pos < _text.Length && IsIdentContinue(_text[_pos]))
            _pos++;

        return new G4Token(G4TokenKind.Identifier, _text.Substring(start, _pos - start), startLine);
    }

    // ── Character helpers ────────────────────────────────────────────

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
