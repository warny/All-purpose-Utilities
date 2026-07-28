grammar Packaged;
root : WORD;
WORD : [a-z]+;
WS : [ \t\r\n]+ -> skip;
