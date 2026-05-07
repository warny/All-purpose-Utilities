# Utils.Parser

A parser framework for .NET that can load and execute a **subset of ANTLR4 `.g4` grammars**
at runtime (without mandatory code generation).

## Install

```bash
dotnet add package omy.Utils.Parser
```

> For compile-time grammar compilation (zero runtime `.g4` parsing), see
> [`omy.Utils.Parser.Generators`](../Utils.Parser.Generators/README.md).

## Supported frameworks

- net9.0

## `.g4` file support status

ANTLR4 `.g4` support is currently **partial**.

- ✅ Scenarios covered by the `UtilsTest.Parser` test suite are supported.
- ⚠️ Full ANTLR4 compatibility is not guaranteed for every advanced syntax/rule.
- ✅ If you need stricter build-time guarantees, also use `omy.Utils.Parser.Generators`.

### Known missing or limited features

The items below are not guaranteed yet and should be considered missing or limited support areas:

- Full ANTLR4 parity across all grammar constructs (combined/parser/lexer edge cases).
- Embedded target-language actions and action-dependent behavior parity with ANTLR4 toolchains.
- Complete semantic predicate and advanced precedence behavior parity in all grammar shapes.
- Full interoperability parity for complex multi-file grammar composition/import scenarios.
- Complete error-recovery parity with ANTLR4-generated parsers in highly ambiguous grammars.

If a grammar relies on one of these areas, validate it with targeted tests before production rollout.

### Current support matrix (high-level)

| Area | Status | Notes |
|---|---|---|
| Runtime `.g4` parsing (`Antlr4GrammarConverter`) | Partial | Works for covered scenarios; full ANTLR4 parity is not claimed. |
| Lexer commands `skip` / `more` / `type` / `channel` | Implemented | Core commands are supported by runtime lexer execution. |
| Lexer modes (`pushMode`, `popMode`, `mode`) | Implemented | Mode stack behavior is available in runtime lexer. |
| Declared `tokens { ... }` and `channels { ... }` metadata | Partial | Available in model/runtime, but must be validated against your grammar set. |
| Runtime lexer extensions (`ILexerExtension`) | Implemented | Hooks are available (`TryReadTokens`, `OnAfterToken`, `OnEndOfInput`). |
| Full ANTLR4 grammar compatibility | Not guaranteed | Advanced/edge constructs may differ from ANTLR4 behavior. |
| Scheduled alternative look-ahead | Implemented (internal) | Lightweight cache records alternative-start observations to avoid repeated obviously impossible starts during deterministic sequential scheduling. |

## Key concepts

| Class / Type | Role |
|---|---|
| `ParserDefinition` | Immutable description of a grammar (rules, modes, imports, options). |
| `Rule` | A single lexer or parser rule with its content tree and declaration order. |
| `RuleContent` | Abstract base for grammar elements: `LiteralMatch`, `RangeMatch`, `CharSetMatch`, `Sequence`, `Alternation`, `Quantifier`, `RuleRef`, `Negation`, `LexerCommand`, … |
| `LexerEngine` | Tokenizes a character stream using lexer rules (maximal-munch, lexer modes, `skip` / `more` / `pushMode` / `popMode`). |
| `ParserEngine` | Builds a parse tree from a token list using parser rules (backtracking recursive-descent, left-recursion detection, precedence predicates). |
| `ActiveParseState` *(internal)* | Infrastructure record representing an explicit active parser branch/state candidate. Exposes two distinct key projections: `ToStateKey()` (scheduler identity) and `ToBranchEquivalenceKey()` (semantic pruning equivalence). |
| `ActiveParseStateKey` *(internal)* | **Scheduler identity key.** Uniquely identifies a parse state for registry/scheduling purposes. Includes alternative index, priority, and continuation — fields that distinguish every scheduler path but must not be used for ambiguity pruning. |
| `ActiveParseBranchEquivalenceKey` *(internal)* | **Semantic pruning key.** Groups branches that reached the same parser-state shape (rule, origin, end position, cursor kind/index). Excludes alternative index, priority, and label — those are handled by `HasDistinctSemantics`. |
| `ParseTreeNavigator` | Fluent, index- and name-based navigation over a `ParseNode` tree. |
| `ParseTreeCompiler<TContext,TResult>` | Generic depth-first compiler: descent handlers enrich context top-down; ascent handlers fold results bottom-up. |
| `Antlr4GrammarConverter` | Compiles a `.g4` source string into a `ParserDefinition` at runtime. |
| `RuleResolver` | Validates and enriches a `ParserDefinition` (rule-kind inference, reference validation). |

---


### Parser runtime architecture notes

`ParserEngine` keeps the current recursive/backtracking execution model, and introduces
an internal `ActiveParseState` normalization layer for branch candidates.

This layer is now orchestrated by an internal `AlternativeScheduler` that executes alternatives sequentially in deterministic order, applies `ActiveParseBranchEquivalenceKey`-based pruning, and selects the same winning branch as before.

- This is **infrastructural only**: no public API changes.
- Current diagnostics and parse-tree behavior remain unchanged.
- The abstraction is used to prepare future explicit scheduling features (shared
  alternative evaluation, continuation-driven orchestration, pruning strategies),
  without enabling parallel parsing at this stage.
- `ParserEngine` also keeps an internal look-ahead cache keyed by rule name, origin position, alternative index, minimum precedence, and alternation cursor context.
- The cache stores lightweight structured look-ahead probe observations only (immediate reject vs requires parse, plus first token snapshot), does not store parse trees, does not replace `ParserStateRegistry`, and does not change alternative selection semantics.
- The scheduled alternative look-ahead layer now performs conservative first-token probing for simple literal matches and lexer-rule references; unsupported or ambiguous constructs return `Unknown` and fall back to normal parsing.
- The scheduled alternative look-ahead layer can also classify structurally epsilon-capable constructs (for example optional `?`, zero-or-more `*`, or empty sequences) as `EpsilonPossible`. This remains informational only and never bypasses normal parsing execution.
- The scheduled alternative look-ahead layer can also record shallow expected-token observations for simple literals, lexer-rule references, and flat token alternations. These observations remain informational only and are not used for adaptive prediction or alternative ranking.
- The look-ahead layer can now detect shared shallow first-token candidates across alternatives from those expected-token observations. This is metadata only: it does not change scheduling, pruning, alternative selection, diagnostics, or parser output.
- The runtime now includes a structural continuation descriptor model that represents shallow continuation identities and shared-prefix continuation metadata. Continuations are not yet scheduled or executed.
- There is no continuation queue, no continuation execution/resumption, no shared-prefix parsing, no adaptive prediction, and no parallel parsing in this step.
- There is still no shared look-ahead graph, no adaptive prediction, no parallel parsing, no continuation queue, and nested alternations are not structurally explored yet.
- The current implementation only applies negative shortcut reuse to top-level rule alternative scheduling and left-recursive seed scheduling. Nested alternations are intentionally excluded in this step to preserve diagnostic stability and keep the optimization conservative.
- The scheduled alternative cursor context is part of the cache key so observations cannot be reused across different parser-shape positions, such as a rule-root choice and a nested alternation.
- Execution remains sequential: no parallel alternative parsing, no continuation queue, and no shared look-ahead graph in this step.

#### Two separate identity concepts on `ActiveParseState`

`ActiveParseState` deliberately exposes two distinct key projections:

| Method | Returns | Purpose | Fields included |
|---|---|---|---|
| `ToStateKey(precedence)` | `ActiveParseStateKey` | Scheduler identity — uniquely tags every parser path for registry and cycle detection | rule name, origin position, current position, alternative index, alternative priority, cursor index/kind, minimum precedence, continuation |
| `ToBranchEquivalenceKey()` | `ActiveParseBranchEquivalenceKey` | Semantic pruning — groups branches that reached the same parser-state shape | rule name, origin position, end/current position, cursor kind, cursor index |

`ActiveParseBranchEquivalenceKey` intentionally excludes alternative index, priority,
label, and continuation. Two branches that consumed the same input span and left the
cursor at the same shape are shape-equivalent regardless of which alternative produced
them. Whether those branches are also *semantically* equivalent (i.e., safe to prune) is
decided separately by `HasDistinctSemantics`, which checks label, associativity, and the
presence of predicates or actions. Pruning occurs only when both conditions hold: same
shape key **and** `HasDistinctSemantics` returns `false`.

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
using System.IO;

var lexer  = new LexerEngine(definition);
var tokens = lexer.Tokenize(new StringReader("1 + 2 * 3")).ToList();

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
| Explicit alternative scheduling | Alternatives are evaluated sequentially in deterministic order; equivalent branches may be pruned, and the best match (longest, then lowest priority) is selected. |
| Left-recursion handling | Direct left-recursive rules are detected at resolution time and parsed using a seed-and-extend loop. |
| Precedence predicates | `<assoc=right>` / priority-annotated alternatives are respected during left-recursive extension. |
| Trailing-token validation | `Parse()` returns an `ErrorNode` when unconsumed tokens remain after the root rule. |
| Registry-backed invocation reuse | Completed rule invocations are centralized in `ParserStateRegistry` and reused only for matching `(rule, position, precedence)` identities. |
| Continuation tracking (preparatory) | Shared rule invocations register continuation metadata in `ParserStateRegistry` to prepare future active-state scheduling work. |
| Non-progressive quantifier guard | Quantifier iterations (`*`, `+`) that match without consuming any token are stopped immediately (`PARSER002`). |
| Non-progressive left-recursion guard | Left-recursive extensions that produce no token progress are stopped and reported (`PARSER003`). |
| Parser state cycle detection | Repeated parser states (same rule, position, and alternative index) during alternative exploration are detected and skipped (`PARSER001`). |
| Ambiguous alternative pruning | Structurally equivalent branches are pruned; the lower-priority winner is kept (`UP1013`). |

### Registry-backed reuse semantics

`ParserStateRegistry` is currently authoritative for completed invocation reuse:

- Reuse key: `(ruleName, originPosition, minimumPrecedence)`.
- Reusable completion can be:
  - a successful parse result (`ParseNode`),
  - or a deterministic failure for the same invocation key.
- When both success and failure entries exist for a key, success is preferred.
- Failure reuse avoids re-evaluating the same failing rule invocation across backtracked alternatives.

Current scope is intentionally limited:

- ✅ implemented: completed invocation reuse + continuation metadata recording.
- ❌ not implemented yet: shared look-ahead, continuation-driven parsing, or concurrent alternative execution.

## Grammar validation

`RuleResolver` enforces grammar type constraints before resolution:

- **Lexer grammars** (`lexer grammar`) must not contain parser rules — violators raise `GrammarValidationException` with diagnostic `UP0008`.
- **Parser grammars** (`parser grammar`) must not declare their own lexer rules — violators raise `GrammarValidationException` with diagnostic `UP0007`. Merged definitions (after project-level imports are resolved) are exempt from this check since imported lexer rules are expected.

`Antlr4GrammarProjectCompiler` performs an additional pre-merge validation step (`ValidateEntryGrammarTypeConstraints`) that catches lexer rules declared directly inside the entry parser grammar before any merging takes place.

---

## Diagnostics

The runtime parser and the source generator now share the same diagnostic model
(`Utils.Parser.Diagnostics`), including descriptor codes and message templates.

### Code scheme

| Prefix | Severity | Purpose |
|---|---|---|
| `UP0xxx` | Error | Blocking errors (unresolved rules, grammar type violations, import failures, …) |
| `UP1xxx` | Warning | Unsupported / ignored / partial behavior (embedded actions, left-recursion handling, registry reuse traces, …) |
| `UP5xxx` | Warning | Best-effort recovery warnings (trailing tokens, ambiguous constructs, …) |
| `UP8xxx` | Info | Informational runtime events |
| `UP9xxx` | Debug | Detailed execution traces (entering/leaving rules, backtracking, registry reuse hits, …) |
| `PARSER0xx` | Warning / Info | Runtime safety guard events (cycle detection, non-progressive loop termination) |

Severity is derived from the code prefix (not manually assigned per call site).

### Shared descriptor table

Use `ParserDiagnostics` to access named descriptors and global lookup:

```csharp
var descriptor = ParserDiagnostics.UnexpectedToken;
var allByCode = ParserDiagnostics.All;
```

### Collecting diagnostics at runtime

```csharp
using Utils.Parser.Diagnostics;

var diagnostics = new DiagnosticBag();
var definition = Antlr4GrammarConverter.Parse(grammarText, diagnostics);
```

`DiagnosticBag` supports direct `Add(...)`, descriptor-based `Add(...)`, range merge,
read-only enumeration, and severity filtering (`GetAtLeast(...)`).

### Generator compatibility

`Utils.Parser.Generators` uses the same `ParserDiagnostics` descriptors and `DiagnosticBag`
during `.g4` parsing, then maps emitted diagnostics to Roslyn diagnostics.

---

## Self-describing design

The ANTLR4 grammar reader is itself built with the same `ParserDefinition`, `Rule`, and
`RuleContent` objects that every other grammar uses. There is no special-cased code for the
meta-grammar — it is a first-class consumer of the framework.

---

## Current limitations

The parser framework targets broad ANTLR4 compatibility, but some constructs are currently
parsed in a simplified way, stored without full semantics, or ignored by one pipeline.
The points below reflect the current implementation behavior.

### ANTLR4 G4 feature support matrix

| ANTLR4 feature | Runtime converter + engine (`Antlr4GrammarConverter` / `LexerEngine` / `ParserEngine`) | Source generator parser (`G4Parser`) | Notes |
| --- | --- | --- | --- |
| Grammar options (`options { ... }`) | ✅ Supported (ingested) | ⚠️ Partially supported | Runtime keeps effective options. |
| Grammar imports (`import ...`) | ⚠️ Partially supported | ❌ Not supported | Runtime parses imports with resolver constraints. |
| `tokens { ... }` block | ⚠️ Parsed but not mapped | ❌ Not supported | Recognized but ignored in model conversion. |
| `channels { ... }` block | ⚠️ Parsed but not mapped | ❌ Not supported | Recognized but ignored in model conversion. |
| Top-level grammar actions (`@... { ... }`) | ⚠️ Parsed but not executed | ❌ Not supported | Stored metadata only. |
| Rule actions (`@init`, `@after`, inline `{...}`) | ⚠️ Partially supported | ⚠️ Parsed as raw blocks | Runtime stores actions and does not execute them. |
| Semantic predicates (`{...}?`) | ⚠️ Parsed but not enforced | ⚠️ Parsed as raw blocks | Runtime accepts as empty successful matches. |
| `returns [...]` | ⚠️ Partially supported | ❌ Not supported | Runtime stores raw return text. |
| `locals`, `throws`, `catch`, `finally` | ⚠️ Parsed but ignored | ❌ Not supported | No runtime semantics yet. |
| Rule labels | ⚠️ Partially supported | ⚠️ Partially supported | Runtime applies labels on `RuleRef` paths. |
| Lexer commands (`skip`, `more`, `channel`, `type`, `pushMode`, `popMode`, `mode`) | ✅ Supported | ⚠️ Parsed without full validation | Runtime validates command names and known behaviors. |
| Unknown lexer commands | ❌ Not supported (explicit error) | ⚠️ Kept as parsed tokens | Runtime rejects unknown commands. |
| Direct left recursion | ⚠️ Partially supported | N/A | Runtime supports guarded direct left recursion. |
| Indirect left recursion | ❌ Not supported | ❌ Not supported | Explicitly diagnosed as unsupported. |
| Precedence predicates (`precpred(_ctx, N)`) | ⚠️ Partially supported | ❌ Not supported | Runtime extraction is pattern-based. |
| Error recovery during parsing | ❌ Not supported | ⚠️ Best-effort parser behavior | Runtime returns `ErrorNode` on failure. |

### Grammar ingestion (runtime converter: `Antlr4GrammarConverter`)

- `options { ... }`, `import ...`, and top-level `@... { ... }` actions are ingested.
- `tokens { ... }` and `channels { ... }` are recognized by the meta-grammar but not mapped
  into `ParserDefinition` during conversion.
- `returns [...]` is currently stored as one raw argument-block string (`RuleReturn(raw, raw)`),
  not as structured typed returns.
- `throws`, `locals`, `catch`, `finally`, rule arguments, and rule modifiers are parsed by the
  meta-grammar but not converted into dedicated runtime semantics.
- Only `@init` and `@after` rule actions are mapped to dedicated rule-level action slots.
- Labels on `labeledElement` are applied when the labeled item is a `RuleRef`; labels on
  non-reference items are currently ignored.

### Runtime semantics (`LexerEngine` / `ParserEngine`)

- Embedded actions and semantic predicates are parsed and stored, but the parser engine does
  not execute them; they are accepted as successful empty matches.
- Precedence is only enforced through recognized `precpred(_ctx, N)` patterns.
- `precpred` extraction is regex-based; if the level cannot be parsed, precedence falls back
  to `0`.
- Lexer commands are restricted to known commands (`skip`, `more`, `channel`, `type`,
  `pushMode`, `popMode`, `mode`); unknown command names raise a conversion error.
- Parsing is not error-recovering: parsing failures return `ErrorNode`, and trailing tokens are
  reported as an error instead of attempting recovery.
- Runtime safety guards stop non-progressive loops in quantifiers (`*`, `+`, `?`) and
  repeated parser-state cycles during branch exploration.
- Direct left-recursive extension is guarded by strict input-progress checks to avoid
  non-terminating expansion attempts.
- Backtracking is still part of the current architecture and has not been removed yet.
- Runtime safety guards (`PARSER001`–`PARSER003`) stop non-terminating patterns:
  `PARSER001` aborts repeated parser states; `PARSER002` stops zero-progress quantifier iterations;
  `PARSER003` stops zero-progress left-recursive extension loops.

### Source generator pipeline limitations (`G4Parser`)

- The generator parser is best-effort by design: `Expect(...)` methods do not fail hard and may
  silently continue on missing tokens.
- `tokens { ... }`, `channels { ... }`, `import ...`, and `@...` actions are skipped.
- Rule headers are simplified: text before `:` (such as `returns`, `throws`, `locals`, and other
  pre-`:` constructs) is skipped rather than modeled.
- Embedded actions/predicates are represented as raw block text (`G4EmbeddedAction`) without
  semantic execution in the generator parser itself.
- Lexer command arguments are currently parsed only from parenthesized identifiers.

### Runtime vs source generator differences

- Runtime conversion keeps a subset of prequel metadata (`options`, `imports`, grammar actions),
  while the source generator skips imports and grammar actions entirely.
- Runtime conversion throws explicit errors for unknown lexer commands; the source generator keeps
  lexer commands as parsed name/argument pairs without command-name validation at parse time.
- Both pipelines recognize large parts of ANTLR4 syntax, but they do not currently provide full
  ANTLR4 semantic equivalence for actions, predicates, exception handlers, and advanced rule
  header metadata.

---

## License

Apache 2.0 — see the repository root for details.
