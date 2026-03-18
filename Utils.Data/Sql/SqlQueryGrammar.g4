grammar SqlQueryGrammar;

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

WITH: [Ww] [Ii] [Tt] [Hh];
RECURSIVE: [Rr] [Ee] [Cc] [Uu] [Rr] [Ss] [Ii] [Vv] [Ee];
SELECT: [Ss] [Ee] [Ll] [Ee] [Cc] [Tt];
INSERT: [Ii] [Nn] [Ss] [Ee] [Rr] [Tt];
UPDATE: [Uu] [Pp] [Dd] [Aa] [Tt] [Ee];
DELETE: [Dd] [Ee] [Ll] [Ee] [Tt] [Ee];
INTO: [Ii] [Nn] [Tt] [Oo];
VALUES: [Vv] [Aa] [Ll] [Uu] [Ee] [Ss];
SET: [Ss] [Ee] [Tt];
FROM: [Ff] [Rr] [Oo] [Mm];
USING: [Uu] [Ss] [Ii] [Nn] [Gg];
WHERE: [Ww] [Hh] [Ee] [Rr] [Ee];
GROUP: [Gg] [Rr] [Oo] [Uu] [Pp];
BY: [Bb] [Yy];
HAVING: [Hh] [Aa] [Vv] [Ii] [Nn] [Gg];
ORDER: [Oo] [Rr] [Dd] [Ee] [Rr];
LIMIT: [Ll] [Ii] [Mm] [Ii] [Tt];
OFFSET: [Oo] [Ff] [Ff] [Ss] [Ee] [Tt];
OUTPUT: [Oo] [Uu] [Tt] [Pp] [Uu] [Tt];
RETURNING: [Rr] [Ee] [Tt] [Uu] [Rr] [Nn] [Ii] [Nn] [Gg];
UNION: [Uu] [Nn] [Ii] [Oo] [Nn];
ALL: [Aa] [Ll] [Ll];
INTERSECT: [Ii] [Nn] [Tt] [Ee] [Rr] [Ss] [Ee] [Cc] [Tt];
EXCEPT: [Ee] [Xx] [Cc] [Ee] [Pp] [Tt];
DISTINCT: [Dd] [Ii] [Ss] [Tt] [Ii] [Nn] [Cc] [Tt];
AS: [Aa] [Ss];

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

