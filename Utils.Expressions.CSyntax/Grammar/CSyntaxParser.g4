parser grammar CSyntaxParser;

options { tokenVocab=CSyntaxLexer; }

instruction
    : if_instruction
    | for_instruction
    | foreach_instruction
    | while_instruction
    | do_while_instruction
    | switch_instruction
    | using_instruction
    | try_catch_instruction
    | method_declaration
    | lambda_expression
    | block_instruction
    | empty_instruction
    | assignment_instruction
    | invocation_instruction
    | operation
    ;

empty_instruction
    : SEMICOLON
    ;

using_instruction
    : USING OPEN_PAREN resource_acquisition CLOSE_PAREN instruction
    | USING qualified_identifier SEMICOLON
    ;

resource_acquisition
    : variable_declaration_assignment
    | assignment_instruction
    | invocation_instruction
    | identifier_part
    ;

try_catch_instruction
    : TRY block_instruction catch_clause+ finally_clause?
    | TRY block_instruction finally_clause
    ;

catch_clause
    : CATCH OPEN_PAREN type_reference identifier? CLOSE_PAREN block_instruction
    | CATCH block_instruction
    ;

finally_clause
    : FINALLY block_instruction
    ;

method_declaration
    : method_modifiers? type_reference identifier OPEN_PAREN parameter_list? CLOSE_PAREN block_instruction
    ;

method_modifiers
    : method_modifier+
    ;

method_modifier
    : PUBLIC
    | PRIVATE
    | PROTECTED
    | INTERNAL
    | STATIC
    | VIRTUAL
    | OVERRIDE
    | ABSTRACT
    | ASYNC
    ;

parameter_list
    : parameter (COMMA parameter)*
    ;

parameter
    : type_reference identifier
    ;

lambda_expression
    : lambda_parameters OP_LAMBDA lambda_body
    ;

lambda_parameters
    : identifier
    | OPEN_PAREN parameter_list? CLOSE_PAREN
    ;

lambda_body
    : instruction
    | block_instruction
    ;

if_instruction
    : IF OPEN_PAREN instruction CLOSE_PAREN instruction (ELSE instruction)?
    ;

for_instruction
    : FOR OPEN_PAREN instruction? SEMICOLON instruction? SEMICOLON instruction? CLOSE_PAREN instruction
    ;

foreach_instruction
    : FOREACH OPEN_PAREN (type_reference)? identifier IN instruction CLOSE_PAREN instruction
    ;

while_instruction
    : WHILE OPEN_PAREN instruction CLOSE_PAREN instruction
    ;

do_while_instruction
    : DO instruction WHILE OPEN_PAREN instruction CLOSE_PAREN SEMICOLON?
    ;

switch_instruction
    : SWITCH OPEN_PAREN instruction CLOSE_PAREN OPEN_BRACE switch_section* CLOSE_BRACE
    ;

switch_section
    : switch_label+ instruction*
    ;

switch_label
    : CASE instruction COLON
    | DEFAULT COLON
    ;

block_instruction
    : OPEN_BRACE instruction* CLOSE_BRACE
    ;

assignment_instruction
    : assignable_target OP_ASSIGN instruction
    | variable_declaration_assignment
    ;

variable_declaration_assignment
    : type_reference identifier OP_ASSIGN instruction
    ;

type_reference
    : (
        predefined_type
        | VAR
        | DYNAMIC
        | generic_type_reference
        | qualified_identifier
      ) array_rank_specifier*
    ;

array_rank_specifier
    : OPEN_BRACKET CLOSE_BRACKET
    ;

generic_type_reference
    : qualified_identifier OP_LESS type_reference (COMMA type_reference)* OP_GREATER
    ;

qualified_identifier
    : IDENTIFIER (DOT IDENTIFIER)*
    ;

predefined_type
    : BOOL
    | BYTE
    | CHAR
    | DECIMAL
    | DOUBLE
    | FLOAT
    | INT
    | LONG
    | OBJECT
    | SBYTE
    | SHORT
    | STRING
    | UINT
    | ULONG
    | USHORT
    | VOID
    ;

assignable_target
    : identifier_part
    ;

identifier_part
    : identifier_atom identifier_suffix*
    ;

identifier_atom
    : IDENTIFIER
    | VAR
    | predefined_type
    ;

identifier_suffix
    : member_access_suffix
    | indexer_suffix
    | invocation_suffix
    ;

member_access_suffix
    : DOT IDENTIFIER
    ;

indexer_suffix
    : OPEN_BRACKET argument_list? CLOSE_BRACKET
    ;

invocation_suffix
    : OPEN_PAREN argument_list? CLOSE_PAREN
    ;

indexer_access
    : identifier_part indexer_suffix
    ;

invocation_instruction
    : identifier_part invocation_suffix
    ;

argument_list
    : instruction (COMMA instruction)*
    ;

operation
    : operation_or
    ;

operation_or
    : operation_and (OP_OR operation_and)*
    ;

operation_and
    : operation_equality (OP_AND operation_equality)*
    ;

operation_equality
    : operation_relational ((OP_EQUAL | OP_NOT_EQUAL) operation_relational)*
    ;

operation_relational
    : operation_shift ((OP_LESS | OP_LESS_EQUAL | OP_GREATER | OP_GREATER_EQUAL) operation_shift)*
    ;

operation_shift
    : operation_plus ((OP_SHIFT_LEFT | OP_SHIFT_RIGHT) operation_plus)*
    ;

operation_plus
    : operation_mul ((OP_PLUS | OP_MINUS) operation_mul)*
    ;

operation_mul
    : operation_pow ((OP_MULTIPLY | OP_DIVIDE | OP_MODULO) operation_pow)*
    ;

operation_pow
    : operation_unary (OP_POWER operation_unary)*
    ;

operation_unary
    : (OP_PLUS | OP_MINUS | OP_NOT | OP_BITWISE_NOT) operation_unary
    | operation_primary
    ;

operation_primary
    : literal
    | identifier_part
    | invocation_instruction
    | object_creation_expression
    | OPEN_PAREN instruction CLOSE_PAREN
    | interpolated_string
    ;

object_creation_expression
    : NEW (type_reference | DYNAMIC)? (OPEN_PAREN argument_list? CLOSE_PAREN)?
    ;

interpolated_string
    : INTERPOLATED_STRING_START interpolated_segment* INTERPOLATED_STRING_END
    ;

interpolated_segment
    : INTERPOLATED_TEXT
    | INTERPOLATED_ESCAPED_OPEN
    | INTERPOLATED_ESCAPED_CLOSE
    | INTERPOLATED_INTERPOLATION_START instruction INTERPOLATED_INTERPOLATION_END
    ;

identifier
    : identifier_part
    ;

literal
    : NUMBER
    | STRING_LITERAL
    | CHAR_LITERAL
    | TRUE
    | FALSE
    | NULL
    ;
