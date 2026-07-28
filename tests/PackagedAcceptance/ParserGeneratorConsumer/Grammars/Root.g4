parser grammar Root;
options { tokenVocab=Tokens; }
import Middle;
start : local;
local : TOKEN;
