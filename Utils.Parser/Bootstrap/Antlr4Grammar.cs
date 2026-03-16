using System.Text.RegularExpressions;
using Utils.Parser.Model;

namespace Utils.Parser.Bootstrap;

/// <summary>
/// Builds the meta-grammar: a hand-coded <see cref="Utils.Parser.Model.ParserDefinition"/>
/// capable of tokenizing and parsing ANTLR4 <c>.g4</c> files.
/// <para>
/// This is the only <see cref="Utils.Parser.Model.ParserDefinition"/> in the project that is
/// constructed programmatically rather than loaded from a <c>.g4</c> file.
/// It uses exactly the same model objects as any other grammar, demonstrating that the
/// framework is self-describing.
/// </para>
/// <para>
/// The resulting definition must be passed through
/// <c>RuleResolver.Resolve</c> before use.
/// </para>
/// </summary>
public static class Antlr4Grammar
{
    /// <summary>
    /// Creates and returns the ANTLR4 meta-grammar definition.
    /// The definition is <em>not</em> pre-resolved; callers must invoke
    /// <c>RuleResolver.Resolve</c> before passing it to
    /// <see cref="Utils.Parser.Runtime.LexerEngine"/> or
    /// <see cref="Utils.Parser.Runtime.ParserEngine"/>.
    /// </summary>
    /// <returns>An unresolved <see cref="Utils.Parser.Model.ParserDefinition"/> for ANTLR4 grammars.</returns>
    public static ParserDefinition Build()
    {
        int order = 0;

        // ═══════════════════════════════════════════════════════════════════
        // FRAGMENTS
        // ═══════════════════════════════════════════════════════════════════

        var hexDigit = new Rule("HexDigit", order++, true,
            Alts(Alt(0, CharSet("0123456789abcdefABCDEF"))));

        var unicodeEsc = new Rule("UnicodeESC", order++, true,
            Alts(Alt(0, Seq(
                Lit("u"),
                Opt(Seq(
                    Ref("HexDigit"),
                    Opt(Seq(
                        Ref("HexDigit"),
                        Opt(Seq(
                            Ref("HexDigit"),
                            Opt(Ref("HexDigit"))))))))))));

        var escSequence = new Rule("ESC_SEQUENCE", order++, true,
            Alts(Alt(0, Seq(
                Lit("\\"),
                Alts(
                    Alt(0, CharSet("btnfr\"'\\")),
                    Alt(1, Ref("UnicodeESC")),
                    Alt(2, Any()))))));

        var doubleQuoteLiteral = new Rule("DoubleQuoteLiteral", order++, true,
            Alts(Alt(0, Seq(
                Lit("\""),
                Star(Alts(
                    Alt(0, Ref("ESC_SEQUENCE")),
                    Alt(1, NegCharSet("\"\r\n\\"))), greedy: false),
                Lit("\"")))));

        var tripleQuoteLiteral = new Rule("TripleQuoteLiteral", order++, true,
            Alts(Alt(0, Seq(
                Lit("\"\"\""),
                Star(Alts(
                    Alt(0, Ref("ESC_SEQUENCE")),
                    Alt(1, Any())), greedy: false),
                Lit("\"\"\"")))));

        var backtickQuoteLiteral = new Rule("BacktickQuoteLiteral", order++, true,
            Alts(Alt(0, Seq(
                Lit("`"),
                Star(Alts(
                    Alt(0, Ref("ESC_SEQUENCE")),
                    Alt(1, NegCharSet("\"\r\n\\"))), greedy: false),
                Lit("`")))));

        var nameStartChar = new Rule("NameStartChar", order++, true,
            Alts(
                Alt(0, Range('A', 'Z')),
                Alt(1, Range('a', 'z')),
                Alt(2, Range('\u00C0', '\u00D6')),
                Alt(3, Range('\u00D8', '\u00F6')),
                Alt(4, Range('\u00F8', '\u02FF')),
                Alt(5, Range('\u0370', '\u037D')),
                Alt(6, Range('\u037F', '\u1FFF')),
                Alt(7, Range('\u200C', '\u200D')),
                Alt(8, Range('\u2070', '\u218F')),
                Alt(9, Range('\u2C00', '\u2FEF')),
                Alt(10, Range('\u3001', '\uD7FF')),
                Alt(11, Range('\uF900', '\uFDCF')),
                Alt(12, Range('\uFDF0', '\uFFFD'))));

        var nameChar = new Rule("NameChar", order++, true,
            Alts(
                Alt(0, Ref("NameStartChar")),
                Alt(1, Range('0', '9')),
                Alt(2, Lit("_")),
                Alt(3, Lit("\u00B7")),
                Alt(4, Range('\u0300', '\u036F')),
                Alt(5, Range('\u203F', '\u2040'))));

        var nestedAction = new Rule("NESTED_ACTION", order++, true,
            Alts(Alt(0, Seq(
                Lit("{"),
                Star(Alts(
                    Alt(0, Ref("NESTED_ACTION")),
                    Alt(1, Ref("STRING_LITERAL")),
                    Alt(2, Ref("DoubleQuoteLiteral")),
                    Alt(3, Ref("TripleQuoteLiteral")),
                    Alt(4, Ref("BacktickQuoteLiteral")),
                    Alt(5, Seq(Lit("/*"), Star(Any(), greedy: false), Lit("*/"))),
                    Alt(6, Seq(Lit("//"), Star(NegCharSet("\r\n")))),
                    Alt(7, Seq(Lit("\\"), Any())),
                    Alt(8, NegCharSet("\\\"'`{"))
                ), greedy: false),
                Lit("}")
            ))));

        var wsFragment = new Rule("WS_FRAGMENT", order++, true,
            Alts(Alt(0, CharSet(" \t\r\n\f"))));

        // ═══════════════════════════════════════════════════════════════════
        // DEFAULT_MODE LEXER RULES
        // ═══════════════════════════════════════════════════════════════════

        var docComment = new Rule("DOC_COMMENT", order++, false,
            Alts(Alt(0, Seq(
                Lit("/**"),
                Star(Any(), greedy: false),
                Alts(
                    Alt(0, Lit("*/")),
                    Alt(1, Ref("EOF")))))));

        var blockComment = new Rule("BLOCK_COMMENT", order++, false,
            Alts(Alt(0, Seq(
                Lit("/*"),
                Star(Any(), greedy: false),
                Alts(
                    Alt(0, Lit("*/")),
                    Alt(1, Ref("EOF")))))));

        var lineComment = new Rule("LINE_COMMENT", order++, false,
            Alts(Alt(0, Seq(
                Lit("//"),
                Star(NegCharSet("\r\n"))))));

        var intRule = new Rule("INT", order++, false,
            Alts(
                Alt(0, Lit("0")),
                Alt(1, Seq(Range('1', '9'), Star(Range('0', '9'))))));

        var stringLiteral = new Rule("STRING_LITERAL", order++, false,
            Alts(Alt(0, Seq(
                Lit("'"),
                Star(Alts(
                    Alt(0, Ref("ESC_SEQUENCE")),
                    Alt(1, NegCharSet("'\r\n\\")))),
                Lit("'")))));

        var unterminatedStringLiteral = new Rule("UNTERMINATED_STRING_LITERAL", order++, false,
            Alts(Alt(0, Seq(
                Lit("'"),
                Star(Alts(
                    Alt(0, Ref("ESC_SEQUENCE")),
                    Alt(1, NegCharSet("'\r\n\\"))))))));

        var beginArgument = new Rule("BEGIN_ARGUMENT", order++, false,
            Alts(Alt(0, Lit("["))));

        var action = new Rule("ACTION", order++, false,
            Alts(Alt(0, Ref("NESTED_ACTION"))));

        var options = new Rule("OPTIONS", order++, false,
            Alts(Alt(0, Seq(
                Lit("options"),
                Star(Ref("WS_FRAGMENT")),
                Lit("{")))));

        var tokens = new Rule("TOKENS", order++, false,
            Alts(Alt(0, Seq(
                Lit("tokens"),
                Star(Ref("WS_FRAGMENT")),
                Lit("{")))));

        var channels = new Rule("CHANNELS", order++, false,
            Alts(Alt(0, Seq(
                Lit("channels"),
                Star(Ref("WS_FRAGMENT")),
                Lit("{")))));

        var importKw = new Rule("IMPORT", order++, false,
            Alts(Alt(0, Lit("import"))));

        var fragment = new Rule("FRAGMENT", order++, false,
            Alts(Alt(0, Lit("fragment"))));

        var lexerKw = new Rule("LEXER", order++, false,
            Alts(Alt(0, Lit("lexer"))));

        var parserKw = new Rule("PARSER", order++, false,
            Alts(Alt(0, Lit("parser"))));

        var grammar = new Rule("GRAMMAR", order++, false,
            Alts(Alt(0, Lit("grammar"))));

        var protectedKw = new Rule("PROTECTED", order++, false,
            Alts(Alt(0, Lit("protected"))));

        var publicKw = new Rule("PUBLIC", order++, false,
            Alts(Alt(0, Lit("public"))));

        var privateKw = new Rule("PRIVATE", order++, false,
            Alts(Alt(0, Lit("private"))));

        var returnsKw = new Rule("RETURNS", order++, false,
            Alts(Alt(0, Lit("returns"))));

        var localsKw = new Rule("LOCALS", order++, false,
            Alts(Alt(0, Lit("locals"))));

        var throwsKw = new Rule("THROWS", order++, false,
            Alts(Alt(0, Lit("throws"))));

        var catchKw = new Rule("CATCH", order++, false,
            Alts(Alt(0, Lit("catch"))));

        var finallyKw = new Rule("FINALLY", order++, false,
            Alts(Alt(0, Lit("finally"))));

        var modeKw = new Rule("MODE", order++, false,
            Alts(Alt(0, Lit("mode"))));

        // Punctuation
        var colon = new Rule("COLON", order++, false,
            Alts(Alt(0, Lit(":"))));

        var colonColon = new Rule("COLONCOLON", order++, false,
            Alts(Alt(0, Lit("::"))));

        var comma = new Rule("COMMA", order++, false,
            Alts(Alt(0, Lit(","))));

        var semi = new Rule("SEMI", order++, false,
            Alts(Alt(0, Lit(";"))));

        var lparen = new Rule("LPAREN", order++, false,
            Alts(Alt(0, Lit("("))));

        var rparen = new Rule("RPAREN", order++, false,
            Alts(Alt(0, Lit(")"))));

        var rbrace = new Rule("RBRACE", order++, false,
            Alts(Alt(0, Lit("}"))));

        var rarrow = new Rule("RARROW", order++, false,
            Alts(Alt(0, Lit("->"))));

        var lt = new Rule("LT", order++, false,
            Alts(Alt(0, Lit("<"))));

        var gt = new Rule("GT", order++, false,
            Alts(Alt(0, Lit(">"))));

        var assign = new Rule("ASSIGN", order++, false,
            Alts(Alt(0, Lit("="))));

        var question = new Rule("QUESTION", order++, false,
            Alts(Alt(0, Lit("?"))));

        var star = new Rule("STAR", order++, false,
            Alts(Alt(0, Lit("*"))));

        var plusAssign = new Rule("PLUS_ASSIGN", order++, false,
            Alts(Alt(0, Lit("+="))));

        var plus = new Rule("PLUS", order++, false,
            Alts(Alt(0, Lit("+"))));

        var or = new Rule("OR", order++, false,
            Alts(Alt(0, Lit("|"))));

        var dollar = new Rule("DOLLAR", order++, false,
            Alts(Alt(0, Lit("$"))));

        var range = new Rule("RANGE", order++, false,
            Alts(Alt(0, Lit(".."))));

        var dot = new Rule("DOT", order++, false,
            Alts(Alt(0, Lit("."))));

        var at = new Rule("AT", order++, false,
            Alts(Alt(0, Lit("@"))));

        var pound = new Rule("POUND", order++, false,
            Alts(Alt(0, Lit("#"))));

        var not = new Rule("NOT", order++, false,
            Alts(Alt(0, Lit("~"))));

        // We split ID into RULE_REF (lowercase start) and TOKEN_REF (uppercase start).
        // RULE_REF and TOKEN_REF must have lower DeclarationOrder than ID so they win
        // tie-breaking in MatchLongest (first-wins-on-equal-length).
        var ruleRef = new Rule("RULE_REF", order++, false,
            Alts(Alt(0, Seq(
                Range('a', 'z'),
                Star(Ref("NameChar"))))));

        var tokenRef = new Rule("TOKEN_REF", order++, false,
            Alts(Alt(0, Seq(
                Range('A', 'Z'),
                Star(Ref("NameChar"))))));

        // ID is a fallback for any NameStartChar identifier not matched above.
        var id = new Rule("ID", order++, false,
            Alts(Alt(0, Seq(
                Ref("NameStartChar"),
                Star(Ref("NameChar"))))));

        // Whitespace
        var ws = new Rule("WS", order++, false,
            Alts(Alt(0, Plus(CharSet(" \t\r\n\f")))));

        // EOF pseudo-rule (matches nothing, used as a sentinel)
        var eof = new Rule("EOF", order++, false,
            Alts(Alt(0, Lit(""))));

        // ═══════════════════════════════════════════════════════════════════
        // ARGUMENT MODE LEXER RULES
        // ═══════════════════════════════════════════════════════════════════

        var nestedArgument = new Rule("NESTED_ARGUMENT", order++, false,
            Alts(Alt(0, Seq(
                Lit("["),
                new Model.LexerCommand(LexerCommandType.PushMode, "Argument")))));

        var argumentEscape = new Rule("ARGUMENT_ESCAPE", order++, false,
            Alts(Alt(0, Seq(Lit("\\"), Any()))));

        var argumentStringLiteral = new Rule("ARGUMENT_STRING_LITERAL", order++, false,
            Alts(Alt(0, Ref("DoubleQuoteLiteral"))));

        var argumentCharLiteral = new Rule("ARGUMENT_CHAR_LITERAL", order++, false,
            Alts(Alt(0, Ref("STRING_LITERAL"))));

        var endArgument = new Rule("END_ARGUMENT", order++, false,
            Alts(Alt(0, Seq(
                Lit("]"),
                new Model.LexerCommand(LexerCommandType.PopMode, null)))));

        var unterminatedArgument = new Rule("UNTERMINATED_ARGUMENT", order++, false,
            Alts(Alt(0, Seq(
                Ref("EOF"),
                new Model.LexerCommand(LexerCommandType.PopMode, null)))));

        var argumentContent = new Rule("ARGUMENT_CONTENT", order++, false,
            Alts(Alt(0, Any())));

        // ═══════════════════════════════════════════════════════════════════
        // LEXER_CHAR_SET MODE LEXER RULES
        // ═══════════════════════════════════════════════════════════════════

        var lexerCharSetBody = new Rule("LEXER_CHAR_SET_BODY", order++, false,
            Alts(Alt(0, Seq(
                Plus(Alts(
                    Alt(0, NegCharSet("]\\")),
                    Alt(1, Seq(Lit("\\"), Any())))),
                new Model.LexerCommand(LexerCommandType.More, null)))));

        var lexerCharSet = new Rule("LEXER_CHAR_SET", order++, false,
            Alts(Alt(0, Seq(
                Lit("]"),
                new Model.LexerCommand(LexerCommandType.PopMode, null)))));

        var unterminatedCharSet = new Rule("UNTERMINATED_CHAR_SET", order++, false,
            Alts(Alt(0, Seq(
                Ref("EOF"),
                new Model.LexerCommand(LexerCommandType.PopMode, null)))));

        // ═══════════════════════════════════════════════════════════════════
        // LEXER MODES
        // ═══════════════════════════════════════════════════════════════════

        // Default mode: order matters for maximal munch + priority
        // Multi-char tokens must come before single-char tokens
        var defaultModeRules = new List<Rule>
        {
            // Fragments (referenced by other rules, never emitted directly)
            hexDigit, unicodeEsc, escSequence,
            doubleQuoteLiteral, tripleQuoteLiteral, backtickQuoteLiteral,
            nameStartChar, nameChar, nestedAction, wsFragment,

            // Comments (must be before punctuation)
            docComment, blockComment, lineComment,

            // Integer
            intRule,

            // String literals
            stringLiteral, unterminatedStringLiteral,

            // Argument start
            beginArgument,

            // Action blocks
            action,

            // Keyword-brace combinations (must be before plain keywords)
            options, tokens, channels,

            // Keywords (must be before ID to take priority)
            importKw, fragment, lexerKw, parserKw, grammar,
            protectedKw, publicKw, privateKw,
            returnsKw, localsKw, throwsKw, catchKw, finallyKw, modeKw,

            // Multi-char punctuation (must be before single-char)
            colonColon, plusAssign, rarrow, range,

            // Single-char punctuation
            colon, comma, semi, lparen, rparen, rbrace,
            lt, gt, assign, question, star, plus, or, dollar,
            dot, at, pound, not,

            // Identifiers (after keywords so keywords take priority)
            ruleRef, tokenRef, id,

            // Whitespace
            ws,

            // EOF
            eof,
        };

        var defaultMode = new LexerMode("DEFAULT_MODE", defaultModeRules);

        var argumentMode = new LexerMode("Argument", new List<Rule>
        {
            nestedArgument,
            argumentEscape,
            argumentStringLiteral,
            argumentCharLiteral,
            endArgument,
            unterminatedArgument,
            argumentContent,
        });

        var lexerCharSetMode = new LexerMode("LexerCharSet", new List<Rule>
        {
            lexerCharSetBody,
            lexerCharSet,
            unterminatedCharSet,
        });

        // ═══════════════════════════════════════════════════════════════════
        // PARSER RULES
        // ═══════════════════════════════════════════════════════════════════

        int pOrder = 0;

        // identifier : RULE_REF | TOKEN_REF
        var pIdentifier = new Rule("identifier", pOrder++, false,
            Alts(
                Alt(0, Ref("RULE_REF")),
                Alt(1, Ref("TOKEN_REF"))));

        // qualifiedIdentifier : identifier (DOT identifier)*
        var pQualifiedIdentifier = new Rule("qualifiedIdentifier", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("identifier"),
                Star(Seq(Ref("DOT"), Ref("identifier")))))));

        // actionBlock : ACTION
        var pActionBlock = new Rule("actionBlock", pOrder++, false,
            Alts(Alt(0, Ref("ACTION"))));

        // argActionBlock : BEGIN_ARGUMENT ARGUMENT_CONTENT*? END_ARGUMENT
        var pArgActionBlock = new Rule("argActionBlock", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("BEGIN_ARGUMENT"),
                Star(Ref("ARGUMENT_CONTENT"), greedy: false),
                Ref("END_ARGUMENT")))));

        // elementOption : qualifiedIdentifier | identifier ASSIGN (qualifiedIdentifier | STRING_LITERAL | INT)
        var pElementOption = new Rule("elementOption", pOrder++, false,
            Alts(
                Alt(0, Seq(
                    Ref("identifier"),
                    Ref("ASSIGN"),
                    Alts(
                        Alt(0, Ref("qualifiedIdentifier")),
                        Alt(1, Ref("STRING_LITERAL")),
                        Alt(2, Ref("INT"))))),
                Alt(1, Ref("qualifiedIdentifier"))));

        // elementOptions : LT elementOption (COMMA elementOption)* GT
        var pElementOptions = new Rule("elementOptions", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("LT"),
                Ref("elementOption"),
                Star(Seq(Ref("COMMA"), Ref("elementOption"))),
                Ref("GT")))));

        // terminalDef : TOKEN_REF elementOptions? | STRING_LITERAL elementOptions?
        var pTerminalDef = new Rule("terminalDef", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("TOKEN_REF"), Opt(Ref("elementOptions")))),
                Alt(1, Seq(Ref("STRING_LITERAL"), Opt(Ref("elementOptions"))))));

        // characterRange : STRING_LITERAL RANGE STRING_LITERAL
        var pCharacterRange = new Rule("characterRange", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("STRING_LITERAL"),
                Ref("RANGE"),
                Ref("STRING_LITERAL")))));

        // setElement : TOKEN_REF elementOptions? | STRING_LITERAL elementOptions? | characterRange | LEXER_CHAR_SET
        var pSetElement = new Rule("setElement", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("TOKEN_REF"), Opt(Ref("elementOptions")))),
                Alt(1, Seq(Ref("STRING_LITERAL"), Opt(Ref("elementOptions")))),
                Alt(2, Ref("characterRange")),
                Alt(3, Ref("LEXER_CHAR_SET"))));

        // blockSet : LPAREN setElement (OR setElement)* RPAREN
        var pBlockSet = new Rule("blockSet", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("LPAREN"),
                Ref("setElement"),
                Star(Seq(Ref("OR"), Ref("setElement"))),
                Ref("RPAREN")))));

        // notSet : NOT setElement | NOT blockSet
        var pNotSet = new Rule("notSet", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("NOT"), Ref("setElement"))),
                Alt(1, Seq(Ref("NOT"), Ref("blockSet")))));

        // wildcard : DOT elementOptions?
        var pWildcard = new Rule("wildcard", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("DOT"),
                Opt(Ref("elementOptions"))))));

        // ruleref : RULE_REF argActionBlock? elementOptions?
        var pRuleref = new Rule("ruleref", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("RULE_REF"),
                Opt(Ref("argActionBlock")),
                Opt(Ref("elementOptions"))))));

        // atom : terminalDef | ruleref | notSet | wildcard
        var pAtom = new Rule("atom", pOrder++, false,
            Alts(
                Alt(0, Ref("terminalDef")),
                Alt(1, Ref("ruleref")),
                Alt(2, Ref("notSet")),
                Alt(3, Ref("wildcard"))));

        // ebnfSuffix : QUESTION QUESTION? | STAR QUESTION? | PLUS QUESTION?
        var pEbnfSuffix = new Rule("ebnfSuffix", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("QUESTION"), Opt(Ref("QUESTION")))),
                Alt(1, Seq(Ref("STAR"), Opt(Ref("QUESTION")))),
                Alt(2, Seq(Ref("PLUS"), Opt(Ref("QUESTION"))))));

        // optionsSpec : OPTIONS (option SEMI)* RBRACE
        var pOptionsSpec = new Rule("optionsSpec", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("OPTIONS"),
                Star(Seq(Ref("option"), Ref("SEMI"))),
                Ref("RBRACE")))));

        // option : identifier ASSIGN optionValue
        var pOption = new Rule("option", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("identifier"),
                Ref("ASSIGN"),
                Ref("optionValue")))));

        // optionValue : identifier (DOT identifier)* | STRING_LITERAL | actionBlock | INT
        var pOptionValue = new Rule("optionValue", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("identifier"), Star(Seq(Ref("DOT"), Ref("identifier"))))),
                Alt(1, Ref("STRING_LITERAL")),
                Alt(2, Ref("actionBlock")),
                Alt(3, Ref("INT"))));

        // altList : alternative (OR alternative)*
        var pAltList = new Rule("altList", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("alternative"),
                Star(Seq(Ref("OR"), Ref("alternative")))))));

        // block : LPAREN (optionsSpec? ruleAction* COLON)? altList RPAREN
        var pBlock = new Rule("block", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("LPAREN"),
                Opt(Seq(
                    Opt(Ref("optionsSpec")),
                    Star(Ref("ruleAction")),
                    Ref("COLON"))),
                Ref("altList"),
                Ref("RPAREN")))));

        // blockSuffix : ebnfSuffix
        var pBlockSuffix = new Rule("blockSuffix", pOrder++, false,
            Alts(Alt(0, Ref("ebnfSuffix"))));

        // ebnf : block blockSuffix?
        var pEbnf = new Rule("ebnf", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("block"),
                Opt(Ref("blockSuffix"))))));

        // labeledElement : identifier (ASSIGN | PLUS_ASSIGN) (atom | block)
        var pLabeledElement = new Rule("labeledElement", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("identifier"),
                Alts(
                    Alt(0, Ref("ASSIGN")),
                    Alt(1, Ref("PLUS_ASSIGN"))),
                Alts(
                    Alt(0, Ref("atom")),
                    Alt(1, Ref("block")))))));

        // predicateOption : elementOption | identifier ASSIGN (actionBlock | INT | STRING_LITERAL)
        var pPredicateOption = new Rule("predicateOption", pOrder++, false,
            Alts(
                Alt(0, Seq(
                    Ref("identifier"),
                    Ref("ASSIGN"),
                    Alts(
                        Alt(0, Ref("actionBlock")),
                        Alt(1, Ref("INT")),
                        Alt(2, Ref("STRING_LITERAL"))))),
                Alt(1, Ref("elementOption"))));

        // predicateOptions : LT predicateOption (COMMA predicateOption)* GT
        var pPredicateOptions = new Rule("predicateOptions", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("LT"),
                Ref("predicateOption"),
                Star(Seq(Ref("COMMA"), Ref("predicateOption"))),
                Ref("GT")))));

        // element : labeledElement (ebnfSuffix |) | atom (ebnfSuffix |) | ebnf | actionBlock QUESTION? predicateOptions?
        var pElement = new Rule("element", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("labeledElement"), Opt(Ref("ebnfSuffix")))),
                Alt(1, Seq(Ref("atom"), Opt(Ref("ebnfSuffix")))),
                Alt(2, Ref("ebnf")),
                Alt(3, Seq(
                    Ref("actionBlock"),
                    Opt(Ref("QUESTION")),
                    Opt(Ref("predicateOptions"))))));

        // alternative : elementOptions? element+ | (epsilon)
        var pAlternative = new Rule("alternative", pOrder++, false,
            Alts(
                Alt(0, Seq(Opt(Ref("elementOptions")), Plus(Ref("element")))),
                Alt(1, Seq()))); // epsilon

        // lexerAtom : characterRange | terminalDef | notSet | LEXER_CHAR_SET | wildcard
        var pLexerAtom = new Rule("lexerAtom", pOrder++, false,
            Alts(
                Alt(0, Ref("characterRange")),
                Alt(1, Ref("terminalDef")),
                Alt(2, Ref("notSet")),
                Alt(3, Ref("LEXER_CHAR_SET")),
                Alt(4, Ref("wildcard"))));

        // lexerCommandExpr : identifier | INT
        var pLexerCommandExpr = new Rule("lexerCommandExpr", pOrder++, false,
            Alts(
                Alt(0, Ref("identifier")),
                Alt(1, Ref("INT"))));

        // lexerCommandName : identifier | MODE
        var pLexerCommandName = new Rule("lexerCommandName", pOrder++, false,
            Alts(
                Alt(0, Ref("identifier")),
                Alt(1, Ref("MODE"))));

        // lexerCommand : lexerCommandName LPAREN lexerCommandExpr RPAREN | lexerCommandName
        var pLexerCommand = new Rule("lexerCommand", pOrder++, false,
            Alts(
                Alt(0, Seq(
                    Ref("lexerCommandName"),
                    Ref("LPAREN"),
                    Ref("lexerCommandExpr"),
                    Ref("RPAREN"))),
                Alt(1, Ref("lexerCommandName"))));

        // lexerCommands : RARROW lexerCommand (COMMA lexerCommand)*
        var pLexerCommands = new Rule("lexerCommands", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("RARROW"),
                Ref("lexerCommand"),
                Star(Seq(Ref("COMMA"), Ref("lexerCommand")))))));

        // lexerBlock : LPAREN lexerAltList RPAREN
        var pLexerBlock = new Rule("lexerBlock", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("LPAREN"),
                Ref("lexerAltList"),
                Ref("RPAREN")))));

        // lexerElement : lexerAtom ebnfSuffix? | lexerBlock ebnfSuffix? | actionBlock QUESTION?
        var pLexerElement = new Rule("lexerElement", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("lexerAtom"), Opt(Ref("ebnfSuffix")))),
                Alt(1, Seq(Ref("lexerBlock"), Opt(Ref("ebnfSuffix")))),
                Alt(2, Seq(Ref("actionBlock"), Opt(Ref("QUESTION"))))));

        // lexerElements : lexerElement+ | (epsilon)
        var pLexerElements = new Rule("lexerElements", pOrder++, false,
            Alts(
                Alt(0, Plus(Ref("lexerElement"))),
                Alt(1, Seq()))); // epsilon

        // lexerAlt : lexerElements lexerCommands? | (epsilon)
        var pLexerAlt = new Rule("lexerAlt", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("lexerElements"), Opt(Ref("lexerCommands")))),
                Alt(1, Seq()))); // epsilon

        // lexerAltList : lexerAlt (OR lexerAlt)*
        var pLexerAltList = new Rule("lexerAltList", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("lexerAlt"),
                Star(Seq(Ref("OR"), Ref("lexerAlt")))))));

        // lexerRuleBlock : lexerAltList
        var pLexerRuleBlock = new Rule("lexerRuleBlock", pOrder++, false,
            Alts(Alt(0, Ref("lexerAltList"))));

        // lexerRuleSpec : FRAGMENT? TOKEN_REF optionsSpec? COLON lexerRuleBlock SEMI
        var pLexerRuleSpec = new Rule("lexerRuleSpec", pOrder++, false,
            Alts(Alt(0, Seq(
                Opt(Ref("FRAGMENT")),
                Ref("TOKEN_REF"),
                Opt(Ref("optionsSpec")),
                Ref("COLON"),
                Ref("lexerRuleBlock"),
                Ref("SEMI")))));

        // labeledAlt : alternative (POUND identifier)?
        var pLabeledAlt = new Rule("labeledAlt", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("alternative"),
                Opt(Seq(Ref("POUND"), Ref("identifier")))))));

        // ruleAltList : labeledAlt (OR labeledAlt)*
        var pRuleAltList = new Rule("ruleAltList", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("labeledAlt"),
                Star(Seq(Ref("OR"), Ref("labeledAlt")))))));

        // ruleBlock : ruleAltList
        var pRuleBlock = new Rule("ruleBlock", pOrder++, false,
            Alts(Alt(0, Ref("ruleAltList"))));

        // ruleModifier : PUBLIC | PRIVATE | PROTECTED | FRAGMENT
        var pRuleModifier = new Rule("ruleModifier", pOrder++, false,
            Alts(
                Alt(0, Ref("PUBLIC")),
                Alt(1, Ref("PRIVATE")),
                Alt(2, Ref("PROTECTED")),
                Alt(3, Ref("FRAGMENT"))));

        // ruleModifiers : ruleModifier+
        var pRuleModifiers = new Rule("ruleModifiers", pOrder++, false,
            Alts(Alt(0, Plus(Ref("ruleModifier")))));

        // ruleAction : AT identifier actionBlock
        var pRuleAction = new Rule("ruleAction", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("AT"),
                Ref("identifier"),
                Ref("actionBlock")))));

        // localsSpec : LOCALS argActionBlock
        var pLocalsSpec = new Rule("localsSpec", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("LOCALS"),
                Ref("argActionBlock")))));

        // throwsSpec : THROWS qualifiedIdentifier (COMMA qualifiedIdentifier)*
        var pThrowsSpec = new Rule("throwsSpec", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("THROWS"),
                Ref("qualifiedIdentifier"),
                Star(Seq(Ref("COMMA"), Ref("qualifiedIdentifier")))))));

        // ruleReturns : RETURNS argActionBlock
        var pRuleReturns = new Rule("ruleReturns", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("RETURNS"),
                Ref("argActionBlock")))));

        // rulePrequel : optionsSpec | ruleAction
        var pRulePrequel = new Rule("rulePrequel", pOrder++, false,
            Alts(
                Alt(0, Ref("optionsSpec")),
                Alt(1, Ref("ruleAction"))));

        // finallyClause : FINALLY actionBlock
        var pFinallyClause = new Rule("finallyClause", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("FINALLY"),
                Ref("actionBlock")))));

        // exceptionHandler : CATCH argActionBlock actionBlock
        var pExceptionHandler = new Rule("exceptionHandler", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("CATCH"),
                Ref("argActionBlock"),
                Ref("actionBlock")))));

        // exceptionGroup : exceptionHandler* finallyClause?
        var pExceptionGroup = new Rule("exceptionGroup", pOrder++, false,
            Alts(Alt(0, Seq(
                Star(Ref("exceptionHandler")),
                Opt(Ref("finallyClause"))))));

        // parserRuleSpec : ruleModifiers? RULE_REF argActionBlock? ruleReturns? throwsSpec?
        //                  localsSpec? rulePrequel* COLON ruleBlock SEMI exceptionGroup
        var pParserRuleSpec = new Rule("parserRuleSpec", pOrder++, false,
            Alts(Alt(0, Seq(
                Opt(Ref("ruleModifiers")),
                Ref("RULE_REF"),
                Opt(Ref("argActionBlock")),
                Opt(Ref("ruleReturns")),
                Opt(Ref("throwsSpec")),
                Opt(Ref("localsSpec")),
                Star(Ref("rulePrequel")),
                Ref("COLON"),
                Ref("ruleBlock"),
                Ref("SEMI"),
                Ref("exceptionGroup")))));

        // ruleSpec : parserRuleSpec | lexerRuleSpec
        var pRuleSpec = new Rule("ruleSpec", pOrder++, false,
            Alts(
                Alt(0, Ref("parserRuleSpec")),
                Alt(1, Ref("lexerRuleSpec"))));

        // rules : ruleSpec*
        var pRules = new Rule("rules", pOrder++, false,
            Alts(Alt(0, Star(Ref("ruleSpec")))));

        // modeSpec : MODE identifier SEMI lexerRuleSpec*
        var pModeSpec = new Rule("modeSpec", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("MODE"),
                Ref("identifier"),
                Ref("SEMI"),
                Star(Ref("lexerRuleSpec"))))));

        // actionScopeName : identifier | LEXER | PARSER
        var pActionScopeName = new Rule("actionScopeName", pOrder++, false,
            Alts(
                Alt(0, Ref("identifier")),
                Alt(1, Ref("LEXER")),
                Alt(2, Ref("PARSER"))));

        // action_ : AT (actionScopeName COLONCOLON)? identifier actionBlock
        var pAction = new Rule("action_", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("AT"),
                Opt(Seq(Ref("actionScopeName"), Ref("COLONCOLON"))),
                Ref("identifier"),
                Ref("actionBlock")))));

        // idList : identifier (COMMA identifier)* COMMA?
        var pIdList = new Rule("idList", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("identifier"),
                Star(Seq(Ref("COMMA"), Ref("identifier"))),
                Opt(Ref("COMMA"))))));

        // channelsSpec : CHANNELS idList? RBRACE
        var pChannelsSpec = new Rule("channelsSpec", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("CHANNELS"),
                Opt(Ref("idList")),
                Ref("RBRACE")))));

        // tokensSpec : TOKENS idList? RBRACE
        var pTokensSpec = new Rule("tokensSpec", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("TOKENS"),
                Opt(Ref("idList")),
                Ref("RBRACE")))));

        // delegateGrammar : identifier ASSIGN identifier | identifier
        var pDelegateGrammar = new Rule("delegateGrammar", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("identifier"), Ref("ASSIGN"), Ref("identifier"))),
                Alt(1, Ref("identifier"))));

        // delegateGrammars : IMPORT delegateGrammar (COMMA delegateGrammar)* SEMI
        var pDelegateGrammars = new Rule("delegateGrammars", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("IMPORT"),
                Ref("delegateGrammar"),
                Star(Seq(Ref("COMMA"), Ref("delegateGrammar"))),
                Ref("SEMI")))));

        // prequelConstruct : optionsSpec | delegateGrammars | tokensSpec | channelsSpec | action_
        var pPrequelConstruct = new Rule("prequelConstruct", pOrder++, false,
            Alts(
                Alt(0, Ref("optionsSpec")),
                Alt(1, Ref("delegateGrammars")),
                Alt(2, Ref("tokensSpec")),
                Alt(3, Ref("channelsSpec")),
                Alt(4, Ref("action_"))));

        // grammarType : LEXER GRAMMAR | PARSER GRAMMAR | GRAMMAR
        var pGrammarType = new Rule("grammarType", pOrder++, false,
            Alts(
                Alt(0, Seq(Ref("LEXER"), Ref("GRAMMAR"))),
                Alt(1, Seq(Ref("PARSER"), Ref("GRAMMAR"))),
                Alt(2, Ref("GRAMMAR"))));

        // grammarDecl : grammarType identifier SEMI
        var pGrammarDecl = new Rule("grammarDecl", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("grammarType"),
                Ref("identifier"),
                Ref("SEMI")))));

        // grammarSpec : grammarDecl prequelConstruct* rules modeSpec* EOF
        var pGrammarSpec = new Rule("grammarSpec", pOrder++, false,
            Alts(Alt(0, Seq(
                Ref("grammarDecl"),
                Star(Ref("prequelConstruct")),
                Ref("rules"),
                Star(Ref("modeSpec")),
                Ref("EOF")))));

        // ═══════════════════════════════════════════════════════════════════
        // ASSEMBLE THE PARSER DEFINITION
        // ═══════════════════════════════════════════════════════════════════

        var parserRules = new List<Rule>
        {
            pGrammarSpec, pGrammarDecl, pGrammarType,
            pPrequelConstruct, pOptionsSpec, pOption, pOptionValue,
            pDelegateGrammars, pDelegateGrammar,
            pTokensSpec, pChannelsSpec, pIdList,
            pAction, pActionScopeName, pActionBlock, pArgActionBlock,
            pModeSpec, pRules, pRuleSpec,
            pParserRuleSpec, pExceptionGroup, pExceptionHandler, pFinallyClause,
            pRulePrequel, pRuleReturns, pThrowsSpec, pLocalsSpec,
            pRuleAction, pRuleModifiers, pRuleModifier,
            pRuleBlock, pRuleAltList, pLabeledAlt,
            pLexerRuleSpec, pLexerRuleBlock, pLexerAltList,
            pLexerAlt, pLexerElements, pLexerElement, pLexerBlock,
            pLexerCommands, pLexerCommand, pLexerCommandName, pLexerCommandExpr,
            pAltList, pAlternative, pElement,
            pPredicateOptions, pPredicateOption,
            pLabeledElement, pEbnf, pBlockSuffix, pEbnfSuffix,
            pLexerAtom, pAtom, pWildcard, pNotSet, pBlockSet, pSetElement,
            pBlock, pRuleref, pCharacterRange, pTerminalDef,
            pElementOptions, pElementOption,
            pIdentifier, pQualifiedIdentifier,
        };

        return new ParserDefinition(
            Name: "ANTLR4",
            Type: GrammarType.Combined,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [defaultMode, argumentMode, lexerCharSetMode],
            ParserRules: parserRules,
            RootRule: pGrammarSpec
        );
    }

    // ─── Builder helpers ───────────────────────────────────────────────────────

    /// <summary>Creates an <see cref="Alternation"/> from the supplied alternatives.</summary>
    private static Alternation Alts(params Alternative[] alts) => new(alts);

    /// <summary>Creates an <see cref="Alternative"/> with the given priority, content, associativity, and optional label.</summary>
    private static Alternative Alt(int priority, RuleContent content,
        Associativity assoc = Associativity.Left, string? label = null)
        => new(priority, assoc, content, label);

    /// <summary>Creates a <see cref="Sequence"/> from the supplied items.</summary>
    private static Sequence Seq(params RuleContent[] items) => new(items);

    /// <summary>Creates a <see cref="LiteralMatch"/> for the given string value.</summary>
    private static LiteralMatch Lit(string value) => new(value);

    /// <summary>Creates a <see cref="RangeMatch"/> spanning the inclusive character range [<paramref name="from"/>, <paramref name="to"/>].</summary>
    private static RangeMatch Range(char from, char to) => new(from, to);

    /// <summary>Creates a <see cref="RuleRef"/> to the named rule, optionally with a label.</summary>
    private static RuleRef Ref(string name, RuleLabel? label = null) => new(name, label);

    /// <summary>Creates an unbounded, greedy (or optionally non-greedy) zero-or-more quantifier (<c>*</c>).</summary>
    private static Quantifier Star(RuleContent inner, bool greedy = true) => new(inner, 0, null, greedy);
    /// <summary>Creates an unbounded, greedy (or optionally non-greedy) one-or-more quantifier (<c>+</c>).</summary>
    private static Quantifier Plus(RuleContent inner, bool greedy = true) => new(inner, 1, null, greedy);
    /// <summary>Creates a zero-or-one quantifier (<c>?</c>).</summary>
    private static Quantifier Opt(RuleContent inner, bool greedy = true) => new(inner, 0, 1, greedy);

    /// <summary>Creates a <see cref="Negation"/> that matches any single character/token not matched by <paramref name="inner"/>.</summary>
    private static Negation Not(RuleContent inner) => new(inner);

    /// <summary>Creates an <see cref="AnyChar"/> wildcard that matches any single character.</summary>
    private static AnyChar Any() => new();

    /// <summary>Creates a non-negated <see cref="CharSetMatch"/> from the characters in <paramref name="chars"/>.</summary>
    private static CharSetMatch CharSet(string chars)
    {
        var set = new HashSet<char>(chars);
        return new CharSetMatch(set, Negated: false);
    }

    /// <summary>Creates a negated <see cref="CharSetMatch"/> that matches any character <em>not</em> in <paramref name="chars"/>.</summary>
    private static CharSetMatch NegCharSet(string chars)
    {
        var set = new HashSet<char>(chars);
        return new CharSetMatch(set, Negated: true);
    }

    /// <summary>Creates an <see cref="EmbeddedAction"/> from raw code, extracting <c>$label</c> references automatically.</summary>
    private static EmbeddedAction Action(string code, ActionContext ctx, ActionPosition pos)
    {
        var labels = ExtractLabels(code);
        return new EmbeddedAction(code, ctx, pos, labels);
    }

    /// <summary>Scans <paramref name="code"/> for <c>$label</c> and <c>$label.property</c> patterns and returns the resulting <see cref="LabelRef"/> list.</summary>
    private static IReadOnlyList<LabelRef> ExtractLabels(string code)
    {
        var labels = new List<LabelRef>();
        var matches = Regex.Matches(code, @"\$(\w+)(?:\.(\w+))?");
        foreach (Match match in matches)
        {
            var ruleLabel = match.Groups[1].Value;
            var property = match.Groups[2].Success ? match.Groups[2].Value : null;

            // Si pas de propriété, c'est une référence directe
            if (property is null)
                labels.Add(new LabelRef(null, ruleLabel));
            else
                labels.Add(new LabelRef(ruleLabel, property));
        }
        return labels;
    }
}
