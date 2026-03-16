# Utils.Parser

A **self-describing universal parser framework** for .NET that turns any ANTLR4 `.g4`
grammar into a tokenizer and parse-tree builder — no code generation required.

## Install

```bash
dotnet add package omy.Utils.Parser
```

> For compile-time grammar compilation (zero runtime `.g4` parsing), see
> [`omy.Utils.Parser.Generators`](../Utils.Parser.Generators/README.md).

## Supported frameworks

- net9.0

## Key concepts

| Class / Type | Role |
|---|---|
| `ParserDefinition` | Immutable description of a grammar (rules, modes, imports, options). |
| `Rule` | A single lexer or parser rule with its content tree and declaration order. |
| `RuleContent` | Abstract base for grammar elements: `LiteralMatch`, `RangeMatch`, `CharSetMatch`, `Sequence`, `Alternation`, `Quantifier`, `RuleRef`, `Negation`, `LexerCommand`, … |
| `LexerEngine` | Tokenizes a character stream using lexer rules (maximal-munch, lexer modes, `skip` / `more` / `pushMode` / `popMode`). |
| `ParserEngine` | Builds a parse tree from a token list using parser rules (backtracking recursive-descent, left-recursion detection, precedence predicates). |
| `ParseTreeNavigator` | Fluent, index- and name-based navigation over a `ParseNode` tree. |
| `ParseTreeCompiler<TContext,TResult>` | Generic depth-first compiler: descent handlers enrich context top-down; ascent handlers fold results bottom-up. |
| `Antlr4GrammarConverter` | Compiles a `.g4` source string into a `ParserDefinition` at runtime. |
| `RuleResolver` | Validates and enriches a `ParserDefinition` (rule-kind inference, reference validation). |

---

## Quick start

### 1 — Parse a grammar from a `.g4` string

```csharp
using Utils.Parser.Bootstrap;

var definition = Antlr4GrammarConverter.Parse("""
    grammar Exp;
    eval        : additionExp EOF ;
    additionExp : multiplyExp (('+' | '-') multiplyExp)* ;
    multiplyExp : atomExp (('*' | '/') atomExp)* ;
    atomExp     : Number | '(' additionExp ')' ;
    Number      : [0-9]+ ('.' [0-9]+)? ;
    WS          : [ \t\r\n]+ -> skip ;
    """);
```

### 2 — Tokenize source text

```csharp
using Utils.Parser.Runtime;

var lexer  = new LexerEngine(definition);
var tokens = lexer.Tokenize(new StringCharStream("1 + 2 * 3")).ToList();

foreach (var token in tokens)
    Console.WriteLine($"{token.RuleName,-12} {token.Text}");
// Number       1
// +
// Number       2
// *
// Number       3
```

### 3 — Build a parse tree

```csharp
var parser = new ParserEngine(definition);
var tree   = parser.Parse(tokens);

if (tree is ErrorNode err)
    Console.Error.WriteLine(err.Message);
```

### 4 — Navigate the parse tree

`ParseTreeNavigator` wraps any `ParseNode` and provides fluent navigation:

```csharp
var nav = new ParseTreeNavigator(tree);

// Index-based
var firstChild = nav[0];

// Name-based (first direct child with that rule name)
var addition = nav.Child("additionExp");

// Recursive descent to first matching descendant
var number = nav.Descendant("Number");

// Access the underlying token of a leaf node
string text = nav[0].Token?.Text ?? "";

// Enumerate children
foreach (var child in nav.Children())
    Console.WriteLine(child.RuleName);
```

### 5 — Compile a parse tree with `ParseTreeCompiler<TContext, TResult>`

`ParseTreeCompiler<TContext, TResult>` traverses the tree in two ordered phases per node:

1. **Descent** (top-down) — enrich the context before children are visited (push a scope, resolve a type, …)
2. **Ascent** (bottom-up) — fold child results into a value for the current node

Handlers are registered either **by rule name** (exact match, O(1)) or **by predicate**
(`Func<ParseTreeNavigator, bool>`, checked in registration order). Rule-name handlers
are resolved first; predicate handlers are tried only when no name-based handler matches.
`Default*` fallbacks are consulted last.

```csharp
var compiler = new ParseTreeCompiler<object?, double>()
    .OnAscend("Number",      (nav, _)       => double.Parse(nav.Token!.Text))
    .OnAscend("atomExp",     (nav, _, kids) => kids[0] ?? 0)
    .OnAscend("multiplyExp", (nav, _, kids) =>
    {
        double result = (double)kids[0]!;
        for (int i = 1; i + 1 < kids.Count; i += 2)
        {
            var op  = nav[i].Token!.Text;
            var rhs = (double)kids[i + 1]!;
            result  = op == "*" ? result * rhs : result / rhs;
        }
        return result;
    })
    .OnAscend("additionExp", (nav, _, kids) =>
    {
        double result = (double)kids[0]!;
        for (int i = 1; i + 1 < kids.Count; i += 2)
        {
            var op  = nav[i].Token!.Text;
            var rhs = (double)kids[i + 1]!;
            result  = op == "+" ? result + rhs : result - rhs;
        }
        return result;
    })
    .OnAscend("eval", (nav, _, kids) => kids[0]);

double value = compiler.Compile(tree, null) ?? 0;
Console.WriteLine(value); // 7
```

#### Predicate-based handlers

Use a predicate when the same logic applies to several rules that share a structural
property rather than a common name:

```csharp
// Collect the text of every leaf token, regardless of its rule name.
var tokens = new List<string>();

var compiler = new ParseTreeCompiler<int, string>()
    // Fires for any LexerNode (leaf).
    .OnAscend(nav => nav.IsLexer,  (nav, _) =>
    {
        tokens.Add(nav.Token!.Text);
        return nav.Token.Text;
    })
    // Fires for any ParserNode (inner node) — sum child texts.
    .OnAscend(nav => nav.IsParser, (nav, _, kids) =>
        string.Join(" ", kids.Where(k => k is not null)));

compiler.Compile(tree, 0);
```

The descent counterpart adjusts the context for all matching nodes:

```csharp
var compiler = new ParseTreeCompiler<int, int>()
    // Increment depth for every parser (inner) node.
    .OnDescend(nav => nav.IsParser, (nav, depth) => depth + 1)
    .OnAscend("Number", (nav, depth) =>
    {
        Console.WriteLine($"Number at depth {depth}");
        return depth;
    })
    .DefaultAscend((_, _, __) => 0);
```

#### Handler resolution order

| Priority | Registration method | Match condition |
|---|---|---|
| 1 (highest) | `OnDescend(string, …)` / `OnAscend(string, …)` | Rule name equals the registered name |
| 2 | `OnDescend(Func<…,bool>, …)` / `OnAscend(Func<…,bool>, …)` | First predicate in registration order that returns `true` |
| 3 (lowest) | `DefaultDescend` / `DefaultAscend` | Always (fallback) |

---

## Build a grammar programmatically

Every grammar can also be constructed in pure C# using the model objects:

```csharp
using Utils.Parser.Model;
using static Utils.Parser.Model.RuleContentFactory;

var number = new Rule("Number", 0, isFragment: false,
    Alts(Alt(0, Seq(Plus(Range('0','9')), Opt(Seq(Lit("."), Plus(Range('0','9'))))))));

var ws = new Rule("WS", 1, isFragment: false,
    Alts(Alt(0, Seq(Plus(CharSet(" \t\r\n")),
                    new LexerCommand(LexerCommandType.Skip, null)))));

var definition = new ParserDefinition(
    Name: "Calc",
    Type: GrammarType.Combined,
    Options:     [],
    Actions:     [],
    Imports:     [],
    Modes:       [new LexerMode("DEFAULT_MODE", [number, ws])],
    ParserRules: [],
    RootRule:    null);
```

The ANTLR4 meta-grammar itself (`Antlr4Grammar.cs`) is hand-coded this way and serves as
a comprehensive real-world example.

---

## Lexer features

| Feature | Description |
|---|---|
| Maximal munch | The longest-matching rule wins; ties broken by declaration order. |
| Lexer modes | `pushMode`, `popMode`, and `mode` commands switch between named mode stacks. |
| `skip` | Matched text is consumed but no token is emitted. |
| `more` | Matched text is accumulated and prepended to the next emitted token. |
| Panic mode | An unrecognized character emits an `ERROR` token and advances by one character. |
| Fragment rules | Rules marked `fragment` are never emitted; they exist only as reusable building blocks. |

## Parser features

| Feature | Description |
|---|---|
| Backtracking | Each alternative is tried in priority order; the cursor is restored on failure. |
| Left-recursion detection | Cycles (same rule at the same token position) are detected and skipped. |
| Precedence predicates | `<assoc=right>` / priority-annotated alternatives are respected. |
| Trailing-token validation | `Parse()` returns an `ErrorNode` when unconsumed tokens remain after the root rule. |

---

## Self-describing design

The ANTLR4 grammar reader is itself built with the same `ParserDefinition`, `Rule`, and
`RuleContent` objects that every other grammar uses. There is no special-cased code for the
meta-grammar — it is a first-class consumer of the framework.

---

## License

Apache 2.0 — see the repository root for details.
