using Utils.Parser.Model;
using Utils.Parser.Resolution;

namespace UtilsTest.Parser;

/// <summary>
/// Builds the Exp grammar from the ANTLR3-style example:
///
///   grammar Exp;
///   eval        : additionExp EOF ;
///   additionExp : multiplyExp ('+' multiplyExp | '-' multiplyExp)* ;
///   multiplyExp : atomExp ('*' atomExp | '/' atomExp)* ;
///   atomExp     : Number | '(' additionExp ')' ;
///   Number      : ('0'..'9')+ ('.' ('0'..'9')+)? ;
///   WS          : (' ' | '\t' | '\r'| '\n') -> skip ;
/// </summary>
internal static class ExpGrammar
{
    public static ParserDefinition Build()
    {
        int order = 0;

        // ═══════════════════════════════════════
        // LEXER RULES
        // ═══════════════════════════════════════

        // Number : ('0'..'9')+ ('.' ('0'..'9')+)?
        var number = new Rule("Number", order++, false,
            Alts(Alt(0, Seq(
                Plus(Range('0', '9')),
                Opt(Seq(
                    Lit("."),
                    Plus(Range('0', '9'))))))));

        // WS : (' ' | '\t' | '\r' | '\n')+ -> skip
        var ws = new Rule("WS", order++, false,
            Alts(Alt(0, Seq(
                Plus(CharSet(" \t\r\n")),
                new LexerCommand(LexerCommandType.Skip, null)))));

        // Implicit token rules for operators and parentheses
        var plus = new Rule("PLUS", order++, false,
            Alts(Alt(0, Lit("+"))));

        var minus = new Rule("MINUS", order++, false,
            Alts(Alt(0, Lit("-"))));

        var mult = new Rule("MULT", order++, false,
            Alts(Alt(0, Lit("*"))));

        var div = new Rule("DIV", order++, false,
            Alts(Alt(0, Lit("/"))));

        var lparen = new Rule("LPAREN", order++, false,
            Alts(Alt(0, Lit("("))));

        var rparen = new Rule("RPAREN", order++, false,
            Alts(Alt(0, Lit(")"))));

        var defaultMode = new LexerMode("DEFAULT_MODE", new List<Rule>
        {
            number, ws, plus, minus, mult, div, lparen, rparen
        });

        // ═══════════════════════════════════════
        // PARSER RULES
        // ═══════════════════════════════════════

        // atomExp : Number | '(' additionExp ')'
        var atomExp = new Rule("atomExp", order++, false,
            Alts(
                Alt(0, Ref("Number")),
                Alt(1, Seq(
                    Lit("("),
                    Ref("additionExp"),
                    Lit(")")))));

        // multiplyExp : atomExp ('*' atomExp | '/' atomExp)*
        var multiplyExp = new Rule("multiplyExp", order++, false,
            Alts(Alt(0, Seq(
                Ref("atomExp"),
                Star(Alts(
                    Alt(0, Seq(Lit("*"), Ref("atomExp"))),
                    Alt(1, Seq(Lit("/"), Ref("atomExp")))))))));

        // additionExp : multiplyExp ('+' multiplyExp | '-' multiplyExp)*
        var additionExp = new Rule("additionExp", order++, false,
            Alts(Alt(0, Seq(
                Ref("multiplyExp"),
                Star(Alts(
                    Alt(0, Seq(Lit("+"), Ref("multiplyExp"))),
                    Alt(1, Seq(Lit("-"), Ref("multiplyExp")))))))));

        // eval : additionExp
        var eval = new Rule("eval", order++, false,
            Alts(Alt(0, Ref("additionExp"))));

        var parserRules = new List<Rule> { eval, additionExp, multiplyExp, atomExp };

        var definition = new ParserDefinition(
            Name: "Exp",
            Type: GrammarType.Combined,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [defaultMode],
            ParserRules: parserRules,
            RootRule: eval
        );

        return RuleResolver.Resolve(definition);
    }

    // ─── Helpers ─────────────────────────────────────────────

    private static Alternation Alts(params Alternative[] alts) => new(alts);
    private static Alternative Alt(int priority, RuleContent content) =>
        new(priority, Associativity.Left, content);
    private static Sequence Seq(params RuleContent[] items) => new(items);
    private static LiteralMatch Lit(string value) => new(value);
    private static RangeMatch Range(char from, char to) => new(from, to);
    private static RuleRef Ref(string name) => new(name);
    private static Quantifier Star(RuleContent inner) => new(inner, 0, null, true);
    private static Quantifier Plus(RuleContent inner) => new(inner, 1, null, true);
    private static Quantifier Opt(RuleContent inner) => new(inner, 0, 1, true);

    private static CharSetMatch CharSet(string chars) =>
        new(new HashSet<char>(chars), false);
}
