namespace Utils.Parser.Generators.Internal;

internal enum G4TokenKind
{
    Identifier,     // rule names, keywords (grammar, fragment, options, mode…)
    StringLiteral,  // 'text'
    CharClass,      // [a-zA-Z0-9]
    DotDot,         // ..
    Arrow,          // ->
    Semi,           // ;
    Colon,          // :
    Pipe,           // |
    LParen,         // (
    RParen,         // )
    Star,           // *
    Plus,           // +
    QMark,          // ?
    Tilde,          // ~
    Hash,           // #
    Dot,            // .
    Comma,          // ,
    At,             // @
    LBrace,         // {
    RBrace,         // }
    BraceBlock,     // { ... } balanced — for embedded actions / predicates
    Eof
}

internal readonly struct G4Token
{
    public G4TokenKind Kind  { get; }
    public string      Value { get; }
    public int         Line  { get; }

    public G4Token(G4TokenKind kind, string value, int line)
    {
        Kind  = kind;
        Value = value;
        Line  = line;
    }

    public override string ToString() => $"[{Kind} '{Value}' @{Line}]";
}
