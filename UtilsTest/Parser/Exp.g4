// Exp.g4 -- simple arithmetic expression grammar used by the Utils.Parser test suite.
// Compiled at test time via Antlr4GrammarConverter.Parse() to validate that the
// Utils.Parser library can bootstrap itself from ANTLR4 source.
grammar Exp;

eval        : additionExp ;
additionExp : multiplyExp ('+' multiplyExp | '-' multiplyExp)* ;
multiplyExp : atomExp ('*' atomExp | '/' atomExp)* ;
atomExp     : Number | '(' additionExp ')' ;

Number : ('0'..'9')+ ('.' ('0'..'9')+)? ;
PLUS   : '+' ;
MINUS  : '-' ;
MULT   : '*' ;
DIV    : '/' ;
LPAREN : '(' ;
RPAREN : ')' ;
WS     : (' ' | '\t' | '\r' | '\n')+ -> skip ;
