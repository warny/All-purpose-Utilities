grammar SqlQueryGrammar;

options { caseInsensitive = true; }

sqlQuery
    : statement statementTerminator*
    ;

statement
    : withClause? coreStatement
    ;

coreStatement
    : selectStatement
    | insertStatement
    | updateStatement
    | deleteStatement
    ;

withClause
    : WITH RECURSIVE? cteDefinition (COMMA cteDefinition)*
    ;

cteDefinition
    : identifier columnList? AS LPAREN statement statementTerminator* RPAREN
    ;

columnList
    : LPAREN identifier (COMMA identifier)* RPAREN
    ;

selectStatement
    : SELECT DISTINCT? selectElements fromClause? whereClause? groupByClause? havingClause? orderByClause? limitClause? offsetClause? returningClause? setOperatorClause?
    ;

insertStatement
    : INSERT INTO targetSegment outputClause? valuesClause returningClause?
    | INSERT INTO targetSegment outputClause? selectStatement returningClause?
    ;

updateStatement
    : UPDATE targetSegment setClause outputClause? fromClause? whereClause? returningClause?
    ;

deleteStatement
    : DELETE deleteTarget? FROM fromElements outputClause? usingClause? whereClause? returningClause?
    ;

deleteTarget
    : targetSegment
    ;

fromClause
    : FROM fromElements
    ;

usingClause
    : USING fromElements
    ;

whereClause
    : WHERE predicateElements
    ;

groupByClause
    : GROUP BY clauseElements
    ;

havingClause
    : HAVING predicateElements
    ;

orderByClause
    : ORDER BY clauseElements
    ;

limitClause
    : LIMIT clauseElements
    ;

offsetClause
    : OFFSET clauseElements
    ;

outputClause
    : OUTPUT clauseElements
    ;

returningClause
    : RETURNING clauseElements
    ;

valuesClause
    : VALUES valuesElements
    ;

setClause
    : SET setElements
    ;

setOperatorClause
    : setOperator selectStatement
    ;

setOperator
    : UNION ALL?
    | INTERSECT
    | EXCEPT
    ;

selectElements
    : selectElement+
    ;

fromElements
    : fromElement+
    ;

predicateElements
    : predicateElement+
    ;

valuesElements
    : valuesElement+
    ;

setElements
    : setElement+
    ;

clauseElements
    : clauseElement+
    ;

targetSegment
    : targetElement+
    ;

selectElement
    : nestedSelectGroup
    | parenthesizedClause
    | baseToken
    ;

fromElement
    : nestedSelectGroup
    | parenthesizedClause
    | baseToken
    ;

predicateElement
    : nestedSelectGroup
    | parenthesizedClause
    | baseToken
    ;

valuesElement
    : nestedSelectGroup
    | parenthesizedClause
    | baseToken
    ;

setElement
    : nestedSelectGroup
    | parenthesizedClause
    | baseToken
    ;

clauseElement
    : nestedSelectGroup
    | parenthesizedClause
    | baseToken
    ;

targetElement
    : parenthesizedClause
    | baseToken
    ;

nestedSelectGroup
    : LPAREN statement RPAREN
    ;

parenthesizedClause
    : LPAREN clauseAtom* RPAREN
    ;

clauseAtom
    : nestedSelectGroup
    | parenthesizedClause
    | baseToken
    ;

baseToken
    : identifier
    | parameter
    | numericLiteral
    | stringLiteral
    | punctuation
    ;

identifier
    : IDENTIFIER
    | QUOTED_IDENTIFIER
    | BRACKET_IDENTIFIER
    ;

parameter
    : PARAMETER
    ;

numericLiteral
    : NUMBER
    ;

stringLiteral
    : STRING
    ;

punctuation
    : DOT
    | COMMA
    | STAR
    | EQUAL
    | PLUS
    | MINUS
    | SLASH
    | PERCENT
    | LT
    | GT
    | LTE
    | GTE
    | NEQ
    | CONCAT
    | SEMI
    ;

statementTerminator
    : SEMI
    ;

WITH: 'WITH';
RECURSIVE: 'RECURSIVE';
SELECT: 'SELECT';
INSERT: 'INSERT';
UPDATE: 'UPDATE';
DELETE: 'DELETE';
INTO: 'INTO';
VALUES: 'VALUES';
SET: 'SET';
FROM: 'FROM';
USING: 'USING';
WHERE: 'WHERE';
GROUP: 'GROUP';
BY: 'BY';
HAVING: 'HAVING';
ORDER: 'ORDER';
LIMIT: 'LIMIT';
OFFSET: 'OFFSET';
OUTPUT: 'OUTPUT';
RETURNING: 'RETURNING';
UNION: 'UNION';
ALL: 'ALL';
INTERSECT: 'INTERSECT';
EXCEPT: 'EXCEPT';
DISTINCT: 'DISTINCT';
AS: 'AS';

LPAREN: '(';
RPAREN: ')';
COMMA: ',';
DOT: '.';
STAR: '*';
EQUAL: '=';
PLUS: '+';
MINUS: '-';
SLASH: '/';
PERCENT: '%';
LTE: '<=';
GTE: '>=';
NEQ: '<>' | '!=';
LT: '<';
GT: '>';
CONCAT: '||';
SEMI: ';';

PARAMETER: [:@$?] [A-Za-z_] [A-Za-z0-9_]*;
NUMBER: [0-9]+ ('.' [0-9]+)?;
STRING: '\'' ('\'\'' | ~['\r\n])* '\'';
QUOTED_IDENTIFIER: '"' ('""' | ~["\r\n])* '"';
BRACKET_IDENTIFIER: '[' (~[\]\r\n] | ']]')* ']';
IDENTIFIER: [A-Za-z_#] [A-Za-z0-9_$#]*;
LINE_COMMENT: '--' ~[\r\n]* -> skip;
BLOCK_COMMENT: '/*' .*? '*/' -> skip;
WS: [ \t\r\n]+ -> skip;
