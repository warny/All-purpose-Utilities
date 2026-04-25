lexer grammar VBSyntaxLexer;

options { caseInsensitive=true; }

// Keywords
ANDALSO : 'AndAlso' ;
AND : 'And' ;
AS : 'As' ;
BOOLEAN : 'Boolean' ;
BYTE : 'Byte' ;
CHAR : 'Char' ;
DECIMAL : 'Decimal' ;
DIM : 'Dim' ;
DO : 'Do' ;
DOUBLE : 'Double' ;
EACH : 'Each' ;
ELSEIF : 'ElseIf' ;
ELSE : 'Else' ;
END : 'End' ;
FALSE : 'False' ;
FOR : 'For' ;
FRIEND : 'Friend' ;
FUNCTION : 'Function' ;
IF : 'If' ;
IN : 'In' ;
INTEGER : 'Integer' ;
LONG : 'Long' ;
LOOP : 'Loop' ;
MOD : 'Mod' ;
NEW : 'New' ;
NEXT : 'Next' ;
NOTHING : 'Nothing' ;
NOT : 'Not' ;
OBJECT : 'Object' ;
OF : 'Of' ;
ORELSE : 'OrElse' ;
OR : 'Or' ;
OVERRIDABLE : 'Overridable' ;
OVERRIDES : 'Overrides' ;
PRIVATE : 'Private' ;
PROTECTED : 'Protected' ;
PUBLIC : 'Public' ;
RETURN : 'Return' ;
SBYTE : 'SByte' ;
SHARED : 'Shared' ;
SHORT : 'Short' ;
SINGLE : 'Single' ;
STEP : 'Step' ;
STRING : 'String' ;
SUB : 'Sub' ;
THEN : 'Then' ;
TO : 'To' ;
TRUE : 'True' ;
UINTEGER : 'UInteger' ;
ULONG : 'ULong' ;
USHORT : 'UShort' ;
WHILE : 'While' ;
XOR : 'Xor' ;

IDENTIFIER : [a-zA-Z_] [a-zA-Z0-9_]* ;
NUMBER : [0-9]+ ('.' [0-9]+)? ([eE] [+-]? [0-9]+)? [fFdDmMuUlL]* ;
STRING_LITERAL : '"' (~["\r\n] | '""')* '"' ;

LINE_COMMENT : '\'' ~[\r\n]* -> skip ;
BLOCK_COMMENT : '/*' .*? '*/' -> skip ;
WS : [ \t\r\n]+ -> skip ;

OP_POWER : '^' ;
OP_SHIFT_LEFT : '<<' ;
OP_SHIFT_RIGHT : '>>' ;
OP_LESS_EQUAL : '<=' ;
OP_GREATER_EQUAL : '>=' ;
OP_NOT_EQUAL : '<>' ;
OP_EQUAL : '=' ;
OP_PLUS : '+' ;
OP_MINUS : '-' ;
OP_MULTIPLY : '*' ;
OP_DIVIDE : '/' ;
OP_CONCAT : '&' ;
OP_LESS : '<' ;
OP_GREATER : '>' ;

DOT : '.' ;
OPEN_PAREN : '(' ;
CLOSE_PAREN : ')' ;
OPEN_BRACKET : '[' ;
CLOSE_BRACKET : ']' ;
COMMA : ',' ;
