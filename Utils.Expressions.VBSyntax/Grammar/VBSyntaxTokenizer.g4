grammar VBSyntaxTokenizer;

// Top-level rule — a VB-like instruction or expression.
instruction
    : if_instruction
    | for_instruction
    | for_each_instruction
    | while_instruction
    | do_loop_instruction
    | method_declaration
    | dim_instruction
    | return_instruction
    | assignment_instruction
    | expression
    ;

// Variable declaration: Dim x As Type [= expression]
dim_instruction
    : DIM identifier AS type_reference (OP_EQUAL expression)?
    ;

// Return value from a Function.
return_instruction
    : RETURN expression
    ;

// Simple assignment: target = expression
assignment_instruction
    : identifier_part OP_EQUAL expression
    ;

// If / ElseIf / Else / End If
if_instruction
    : IF expression THEN instruction+ (ELSEIF expression THEN instruction+)* (ELSE instruction+)? END IF
    ;

// For variable = start To end [Step step] ... Next [variable]
for_instruction
    : FOR identifier OP_EQUAL expression TO expression (STEP expression)? instruction+ NEXT identifier?
    ;

// For Each variable [As type] In collection ... Next [variable]
for_each_instruction
    : FOR EACH identifier (AS type_reference)? IN expression instruction+ NEXT identifier?
    ;

// While condition ... End While
while_instruction
    : WHILE expression instruction+ END WHILE
    ;

// Do [While condition] ... Loop [While condition]
do_loop_instruction
    : DO (WHILE expression)? instruction+ LOOP (WHILE expression)?
    ;

// Function/Sub declaration with optional access modifiers.
method_declaration
    : method_modifier* FUNCTION identifier OPEN_PAREN parameter_list? CLOSE_PAREN AS type_reference instruction+ END FUNCTION
    | method_modifier* SUB identifier OPEN_PAREN parameter_list? CLOSE_PAREN instruction+ END SUB
    ;

method_modifier
    : PUBLIC
    | PRIVATE
    | PROTECTED
    | FRIEND
    | SHARED
    | OVERRIDABLE
    | OVERRIDES
    ;

parameter_list
    : parameter (COMMA parameter)*
    ;

parameter
    : identifier AS type_reference
    ;

type_reference
    : predefined_type array_rank_specifier*
    | generic_type_reference array_rank_specifier*
    | qualified_identifier array_rank_specifier*
    ;

array_rank_specifier
    : OPEN_PAREN CLOSE_PAREN
    ;

generic_type_reference
    : qualified_identifier OPEN_PAREN OF type_reference (COMMA type_reference)* CLOSE_PAREN
    ;

qualified_identifier
    : IDENTIFIER (DOT IDENTIFIER)*
    ;

predefined_type
    : INTEGER
    | LONG
    | SHORT
    | BYTE
    | SBYTE
    | SINGLE
    | DOUBLE
    | DECIMAL
    | BOOLEAN
    | STRING
    | CHAR
    | OBJECT
    | USHORT
    | UINTEGER
    | ULONG
    ;

// Expression — does not include assignment, so = means equality here.
expression
    : operation_or
    | lambda_expression
    ;

// Inline Function lambda: Function(x As Double) x * 2
lambda_expression
    : FUNCTION OPEN_PAREN parameter_list? CLOSE_PAREN expression
    ;

operation_or
    : operation_and ((ORELSE | OR | XOR) operation_and)*
    ;

operation_and
    : operation_not ((ANDALSO | AND) operation_not)*
    ;

operation_not
    : NOT operation_not
    | operation_equality
    ;

operation_equality
    : operation_relational ((OP_EQUAL | OP_NOT_EQUAL) operation_relational)*
    ;

operation_relational
    : operation_concat ((OP_LESS | OP_LESS_EQUAL | OP_GREATER | OP_GREATER_EQUAL) operation_concat)*
    ;

// String concatenation with & has lower precedence than arithmetic.
operation_concat
    : operation_shift (OP_CONCAT operation_shift)*
    ;

operation_shift
    : operation_plus ((OP_SHIFT_LEFT | OP_SHIFT_RIGHT) operation_plus)*
    ;

operation_plus
    : operation_mul ((OP_PLUS | OP_MINUS) operation_mul)*
    ;

operation_mul
    : operation_negate ((OP_MULTIPLY | OP_DIVIDE | MOD) operation_negate)*
    ;

operation_negate
    : OP_MINUS operation_negate
    | OP_PLUS operation_negate
    | operation_power
    ;

operation_power
    : operation_primary (OP_POWER operation_primary)*
    ;

operation_primary
    : literal
    | identifier_part
    | object_creation_expression
    | OPEN_PAREN expression CLOSE_PAREN
    ;

identifier_part
    : identifier_atom identifier_suffix*
    ;

identifier_atom
    : IDENTIFIER
    | predefined_type
    ;

identifier_suffix
    : member_access_suffix
    | invocation_or_indexer_suffix
    ;

member_access_suffix
    : DOT IDENTIFIER
    ;

// In VB, both function calls and array/collection indexers use parentheses.
invocation_or_indexer_suffix
    : OPEN_PAREN argument_list? CLOSE_PAREN
    ;

identifier
    : IDENTIFIER
    ;

argument_list
    : expression (COMMA expression)*
    ;

// construction_type omits array_rank_specifier so the constructor's ()
// is not greedily consumed as an array rank specifier.
construction_type
    : predefined_type
    | generic_type_reference
    | qualified_identifier
    ;

object_creation_expression
    : NEW construction_type OPEN_PAREN argument_list? CLOSE_PAREN
    | NEW OPEN_PAREN argument_list? CLOSE_PAREN
    ;

literal
    : NUMBER
    | STRING_LITERAL
    | TRUE
    | FALSE
    | NOTHING
    ;

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
