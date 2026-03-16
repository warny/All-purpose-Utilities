# omy.Utils.Parser

A **self-describing universal parser framework** for .NET.
Load any ANTLR4 `.g4` grammar file at runtime and immediately tokenize and parse
source text against it — no code generation required.

## Install

```bash
dotnet add package omy.Utils.Parser
```

## Key concepts

| Concept | Description |
|---|---|
| `ParserDefinition` | Immutable description of a grammar (rules, modes, options). |
| `LexerEngine` | Tokenizes a character stream using lexer rules (maximal-munch). |
| `ParserEngine` | Builds a parse tree from a token list using parser rules (backtracking recursive-descent). |
| `Antlr4GrammarConverter` | Converts a `.g4` source text into a `ParserDefinition`. |
| `RuleResolver` | Validates and enriches a `ParserDefinition` (rule-kind inference, reference validation). |

## Quick usage

### Parse an ANTLR4 grammar

```csharp
using Utils.Parser.Bootstrap;

// Parse any .g4 grammar into a ParserDefinition
var definition = Antlr4GrammarConverter.Parse("""
    grammar Exp;
    eval        : additionExp ;
    additionExp : multiplyExp (('+' | '-') multiplyExp)* ;
    multiplyExp : atomExp (('*' | '/') atomExp)* ;
    atomExp     : Number | '(' additionExp ')' ;
    Number      : ('0'..'9')+ ;
    WS          : (' ' | '\t' | '\r' | '\n')+ -> skip ;
    """);
```

### Tokenize source text

```csharp
using Utils.Parser.Runtime;

var lexer = new LexerEngine(definition);
var stream = new StringCharStream("1 + 2 * 3");
foreach (var token in lexer.Tokenize(stream))
    Console.WriteLine($"{token.RuleName}: {token.Text}");
```

### Parse source text

```csharp
var lexer  = new LexerEngine(definition);
var parser = new ParserEngine(definition);

var tokens = lexer.Tokenize(new StringCharStream("1 + 2 * 3")).ToList();
var tree   = parser.Parse(tokens);
```

## Build a grammar programmatically

The meta-grammar itself (which parses `.g4` files) is hand-coded using the same
model objects. See `Antlr4Grammar.Build()` in the source for a full example.

## Self-describing design

The ANTLR4 grammar reader is built with the same `ParserDefinition`, `Rule`, and
`RuleContent` objects that any other grammar uses. There is no special-cased code
for the meta-grammar — it is a first-class consumer of the framework.

## License

Apache 2.0 — see the repository root for details.
