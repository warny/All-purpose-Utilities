# ANTLR4 Compatibility Reference

This document lists ANTLR4 grammar features and their support status in Utils.Parser.

For each feature that behaves differently from standard ANTLR4, a **Usage** section explains how to achieve the equivalent result.

This document must be consulted before modifying any grammar-related component, and updated whenever a feature's support status changes.

> Note: this reference is complementary to `Antlr4CompatibilityMatrix.md` (high-level matrix).
> Use this file for implementation-oriented usage guidance when behavior differs from standard ANTLR4.

---

## Supported — full runtime support

These features work as in standard ANTLR4.

| Feature | Notes |
|---|---|
| `grammar Name;` / `lexer grammar Name;` / `parser grammar Name;` | Combined, lexer-only, and parser-only grammars are all supported. |
| Literal matching `'text'` | Exact string match in lexer and parser rules. |
| Character ranges `'a'..'z'` | Range match in lexer rules. |
| Character classes `[a-zA-Z_0-9]` | Set match, including Unicode ranges. |
| Negated character classes `[^...]` | Inverted set match. |
| Wildcard `.` | Matches any single character (lexer) or any single token (parser). |
| Quantifiers `*`, `+`, `?`, `{n,m}` | All quantifier forms are supported. |
| Alternation `a \| b \| c` | Alternatives are tried in declaration order. |
| Grouping `(...)` | Inline groups are supported in lexer and parser rules. |
| Negation `~a` | Supported in lexer and parser rules. |
| `fragment` rules | Fragment rules are never emitted as tokens; they serve as reusable building blocks. |
| Lexer modes — `mode Name;`, `-> pushMode(...)`, `-> popMode`, `-> mode(...)` | Full mode-stack behaviour is implemented. |
| Lexer commands — `-> skip`, `-> more`, `-> channel(...)`, `-> type(...)` | All seven built-in lexer commands are supported. |
| Maximal munch | Longest-match rule wins; ties are broken by declaration order. |
| Panic mode | An unrecognised character emits an `ERROR` token and advances by one character. |
| `options { caseInsensitive = true; }` | Honoured by the lexer engine. |
| Diagnostic codes | Full set of `UP0xxx`–`UP9xxx` and `PARSER0xx` codes. |

---

## Supported — behaviour differs from standard ANTLR4

These features are supported but work differently. Read the **Usage** section before using them.

---

### `superClass` option

**Standard ANTLR4**: `options { superClass = MyBase; }` sets the generated parser or lexer class's base class.

**Utils.Parser**: `superClass` is parsed and stored as metadata in `EffectiveGrammarOptions.ParserSuperClass` / `LexerSuperClass` and in `GrammarExtensionBinding`. It has no effect on class inheritance.

At runtime, the lexer calls **all** registered `ILexerExtension` instances in sequence — there is no automatic dispatch by `superClass` name. The `superClass` value is readable by an extension via `context.Definition.ExtensionBindings`, which lets the extension decide whether to apply its logic to a given grammar. The runtime enforces one constraint: if `ExtensionBindings.Count > 0` (i.e. the grammar declared `superClass`) but no extensions are registered, a validation error is raised.

**Usage** — implement and register a lexer extension; inspect `ExtensionBindings` to filter by grammar if needed:

```csharp
// Grammar declares:  options { superClass = IndentTracker; }

public class IndentTrackerExtension : ILexerExtension
{
    public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context)
    {
        // Optionally guard by superClass name:
        bool applies = context.Definition.ExtensionBindings
            .Any(b => b.SuperClassName == "IndentTracker");
        if (!applies) return [];

        // Custom token injection logic here.
        return [];
    }

    public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];
    public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
}

var options = new LexerEngineOptions
{
    Extensions = [new IndentTrackerExtension()]
};
var lexer = new LexerEngine(definition, options);
```

The `GrammarExtensionBinding` record exposes `SuperClassName`, the owning grammar's lexer rule names, declared tokens, and declared channels.

---


## Current embedded-code status

Two executable embedded-code paths now exist for **parser semantic predicates** and **inline parser actions**, but neither path is activated automatically by `ParserEngine`. The runtime core remains language-neutral, and default policies remain conservative. `docs/parser/EmbeddedCodeExecutionModel.md` describes the architectural model; this section is the canonical ANTLR compatibility status.

Current high-level state:

- default runtime parsing preserves embedded-code metadata but does not execute target-language source;
- the runtime-inline prepared expression path is available as an explicit opt-in for callers that provide an `IExpressionCompiler`;
- the source-generator C# path is available as an explicit opt-in for generated grammars;
- lexer embedded code, grammar-level actions, rule lifecycle actions, non-inline parser actions, rollback/buffering, and arbitrary parser state mutation remain unsupported for execution.

### Execution paths

#### Runtime-inline prepared expression path

The prepared expression flow is explicit and model-driven:

```text
ParserDefinition
+ IExpressionCompiler
-> ExpressionEmbeddedCodePreparer
-> EmbeddedCodeRuntimeDiscovery
-> PreparedExpressionEmbeddedCodeRegistryBuilder
-> PreparedExpressionEmbeddedCodeRegistry
-> PreparedExpressionRuntimePolicyBuilder
-> ParserRuntimeFeaturePolicy
-> ParserEngine
```

This path:

- uses `IExpressionCompiler`;
- supports the expression languages supplied by the consumer;
- can use the existing expression compiler packages such as `Utils.Expressions.CSyntax` and `Utils.Expressions.VBSyntax`;
- prepares artifacts before parsing when callers use the prepared registry/policy builder;
- does **not** compile source during `Evaluate()` or `Execute()` in the prepared-registry path;
- is opt-in through `PreparedExpressionRuntimePolicyBuilder` or equivalent manual registry/adapters wiring;
- does not make `ParserEngine` prepare or execute embedded code by default.

#### Source-generator C# path

The generated C# flow is build-time source generation plus explicit runtime policy opt-in:

```text
.g4
-> Utils.Parser.Generators
-> generated C# hooks
-> generated evaluator/executor
-> CreateRuntimePolicy(...)
-> ParseWithEmbeddedCode(...)
-> ParserEngine
```

This path:

- does **not** use `IExpressionCompiler`;
- emits C# source hooks for supported parser embedded-code constructs;
- lets Roslyn compile the generated hooks with the consuming project;
- leaves invalid C# as normal C# compilation errors;
- keeps generated `Parse(...)` conservative;
- executes generated hooks only through `ParseWithEmbeddedCode(...)` or a policy returned by `CreateRuntimePolicy(...)`;
- binds generated dispatchers to a per-parse execution context rather than shared static state;
- injects unscoped `@members` and `@parser::members` into that context, not into the static facade.

### Supported executable parser constructs

The only currently executable embedded-code constructs are parser semantic predicates and inline parser actions, and only through the explicit opt-in paths above.

#### Runtime-inline prepared expression path

Supported executable parser constructs are:

- semantic predicates prepared from parser-model `ValidatingPredicate` nodes;
- inline parser actions prepared from parser-model `EmbeddedAction` nodes.

Support depends on the selected `IExpressionCompiler` and the expression language it implements. The shared preparation context exposes a minimal read-only symbol model: `ruleName`, `inputPosition`, `alternativeIndex`, and `elementIndex`. The prepared expression path does not expose a separate user `context` symbol; the compiler receives those symbols as reads derived from the runtime predicate/action context parameter.

#### Source-generator C# path

Supported executable parser constructs are:

- expression-bodied parser predicates, for example `{ inputPosition == 0 }?`;
- block-bodied parser predicates that include `return`, for example `{ return inputPosition == 0; }?`;
- multi-line predicate blocks with local variables and a `return` statement;
- inline parser actions containing a single statement;
- inline parser actions containing multiple statements;
- multi-line inline parser actions;
- local variables in generated predicate/action hooks;
- calls from inline parser actions to members injected into or supplied by another declaration of the generated execution-context partial class;
- unscoped `@members` and `@parser::members` blocks injected verbatim into `{ClassName}ExecutionContext` as a C# compatibility bridge.

Generated predicate hooks expose `context`, `ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`, and `predicateCode`. Generated action hooks expose `context`, `ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`, and `actionCode`. Hooks are instance methods on the generated `{ClassName}ExecutionContext`, so injected parser members can hold per-parse instance state. `ParseWithEmbeddedCode(string input)` creates a new context per call; `ParseWithEmbeddedCode(string input, {ClassName}ExecutionContext executionContext)` lets advanced callers provide and inspect a context explicitly. Generated `Parse(...)` remains conservative and does not execute hooks or create an execution context.

### Runtime-compatible indexing

`EmbeddedCodeRuntimeDiscovery` provides shared runtime metadata for parser embedded-code dispatch. Runtime keys include:

- embedded-code kind;
- owning rule name;
- raw source text;
- alternative index;
- element index.

The shared indexing model covers the sensitive shapes that currently have regression coverage:

- single-item alternatives;
- sequences;
- quantifiers;
- negation probes;
- duplicate source text in different runtime positions;
- direct-left-recursive base alternatives;
- direct-left-recursive tails.

`PreparedExpressionEmbeddedCodeRegistryBuilder` consumes the shared discovery result. `GrammarEmitter` still uses a generator-side collector over the `G4Grammar` AST, but generated hook dispatch is kept aligned with the shared runtime discovery on the sensitive cases above by parity and regression tests.

### Unsupported / represented-only constructs

The following constructs may be represented as metadata when visible to ingestion, but they are not executed by the current embedded-code paths:

- lexer actions;
- lexer predicates;
- grammar actions;
- `@members`;
- `@init`;
- `@after`;
- parser actions that are not inline alternative elements;
- action rollback or buffering;
- controlled context mutation models;
- arbitrary parser state mutation.

When visible through runtime discovery, unsupported constructs must remain classified with explicit `EmbeddedCodeUnsupportedReason` values rather than being treated as executable metadata. The existence of stored source text is not execution authority.

### Default behavior

Default parsing remains conservative:

- `ParserEngine` does not execute embedded code unless a caller supplies an explicit policy;
- `ParserRuntimeFeaturePolicy.Default` does not evaluate predicate source or execute action source;
- semantic predicates that are not evaluated are conservatively accepted, and `UP1006` is emitted when applicable;
- parser actions whose executor returns `NotExecuted` remain non-executed, with existing runtime diagnostics when applicable;
- generated `Parse(...)` uses the conservative default policy;
- generated `ParseWithEmbeddedCode(...)` is the opt-in generated C# embedded-code path.

### Diagnostics

Current diagnostics boundaries are intentionally split by path:

- invalid generated C# is reported by Roslyn as C# compilation diagnostics;
- unsupported embedded-code constructs are classified with explicit unsupported reasons when visible to runtime discovery;
- runtime non-evaluation/non-execution uses existing runtime diagnostics such as `UP1006` and `UP1005` when applicable;
- rich source-generator diagnostics for every unsupported embedded-code construct are not yet implemented unless already covered by existing generator diagnostics.

### Known limitations

Known limitations include:

- no rollback or action buffering;
- actions in negation probes require caution and are not a general side-effect-safe model;
- no controlled context mutation model;
- no lexer embedded-code execution;
- no `@members` injection;
- no `@init` / `@after` execution;
- the source-generator C# path does not parse C# semantically; it applies light body normalization and leaves validation to Roslyn;
- the generator hook collector remains separate from `EmbeddedCodeRuntimeDiscovery`.

### Recommended next steps

Recommended next steps are:

1. design controlled `@members` support if generated grammars need it;
2. design lexer predicate/action semantics explicitly before enabling lexer embedded code;
3. design action buffering/rollback before supporting side-effect-sensitive actions;
4. evaluate deeper alignment between the generator `G4Grammar` collector and `EmbeddedCodeRuntimeDiscovery`;
5. expand the ANTLR grammar corpus used for compatibility and regression checks.

---

### Semantic predicates `{ condition }?`

See [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md) for the two-path execution boundary (source generation C# vs runtime expression compilation) and project responsibility map.

The shared embedded-code diagnostics taxonomy is documented in `EmbeddedCodeExecutionModel.md` and `ParserDiagnostics`.


**Standard ANTLR4**: The predicate body is target-language code evaluated inline during parsing.

**Utils.Parser**: Predicates are parsed and stored. Evaluation is delegated to an `ISemanticPredicateEvaluator` registered in `ParserRuntimeFeaturePolicy`. The default policy returns `NotEvaluated` without detailed diagnostic metadata, which does **not** reject the branch — it acts as if the predicate passed and `ParserEngine` emits `UP1006`.
Optional expression-backed evaluators can be configured explicitly (for example through `omy.Utils.Parser.Expressions`) to enforce predicate outcomes. One adapter uses a caller-provided `IExpressionCompiler` and may compile opportunistically during evaluation with compilation caching; this is documented as an intermediate implementation detail. A separate prepared-artifact adapter consumes `PreparedExpressionSemanticPredicate` instances from an explicit registry without compiling during evaluation. `omy.Utils.Parser.Expressions` also exposes an opt-in policy builder that prepares predicates from a `ParserDefinition`, builds the registry, and returns a `ParserRuntimeFeaturePolicy` configured with prepared adapters. Neither expression path is selected by default, and `ParserEngine` does not prepare predicates automatically.

`Utils.Parser.Generators` now provides a separate C# source-generation path for parser semantic predicates. For generated grammars, simple C# predicate expressions such as `{ true }?`, `{ false }?`, and `{ inputPosition == 0 }?` are emitted as private generated C# hook methods, compiled by Roslyn with the consuming project, and executed only when the generated `ParseWithEmbeddedCode(...)` helper or `CreateRuntimePolicy(...)` result is used. The existing generated `Parse(...)` helper keeps the default conservative policy. Dispatch is tested against the runtime indexes used for single-item alternatives, sequence elements, quantified content, negation predicate probes, same-source hooks in different alternatives, and direct-left-recursive tail views because generated helpers resolve the generated definition before parsing with the generated policy. Invalid C# in this path is a compile-time C# error, not a runtime expression-compilation diagnostic.

**Usage** — implement `ISemanticPredicateEvaluator` and pass it via the policy:

```csharp
public class MyPredicateEvaluator : ISemanticPredicateEvaluator
{
    public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
    {
        // context.PredicateCode  — raw predicate text from the .g4 file
        // context.Rule           — current Rule
        // context.InputPosition  — current token index
        // context.AlternativeIndex / ElementIndex — position within the rule

        if (context.PredicateCode == "IsKeyword()")
            return _keywords.Contains(CurrentToken) 
                ? SemanticPredicateEvaluationOutcome.Satisfied 
                : SemanticPredicateEvaluationOutcome.Rejected;

        return SemanticPredicateEvaluationOutcome.NotEvaluated();
    }
}

var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = new MyPredicateEvaluator()
};
var parser = new ParserEngine(definition, policy);
```

Recognized and represented as runtime predicate objects.
Default runtime does not evaluate predicate source code.
When predicates are not evaluated, runtime conservatively treats them as accepted and emits `UP1006` (`SemanticPredicateNotEnforced`).
Custom predicate evaluators may satisfy or reject predicates. The optional prepared expression path can build a registry from parser-model `ValidatingPredicate` nodes, including predicates nested in runtime-executable structures and direct-left-recursive tails, and wire it through `ParserRuntimeFeaturePolicy` explicitly; it is not enabled by default and does not change the compatibility level. Generated grammars can instead use generated C# hooks through `ParseWithEmbeddedCode(...)` or a policy returned by `CreateRuntimePolicy(...)`; this source-generation path supports predicate expressions and predicate blocks with `return`, and it is source generation, not `IExpressionCompiler` usage. Roslyn remains responsible for validating whether the generated hook body is valid C# and returns `bool`.
This behavior is runtime-policy-driven or generated-policy-driven, not compatibility metadata.

> **Important**: memoization is keyed by `(rule, input position, precedence)`. Evaluators must be deterministic for identical invocation contexts.

### Gated semantic predicates `{ condition }=>`

ANTLR gated predicates remain a compatibility question unless explicitly proven by converter tests.
If recognized by grammar ingestion, they follow the same runtime-policy path as semantic predicates (`ISemanticPredicateEvaluator`), including conservative acceptance with `UP1006` when not evaluated.

### Precedence predicates `{precpred(_ctx, N)}?`

Recognized and normalized into precedence behavior.
Not routed through semantic predicate evaluation.

---

### Inline actions `{ code }`

See [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md) for explicit compiler/executor separation and non-goals.


**Standard ANTLR4**: Action code is target-language code executed as a side effect during parsing.

**Utils.Parser**: Actions are parsed and stored. Execution is delegated to an `IParserActionExecutor` registered in `ParserRuntimeFeaturePolicy`. The default policy returns `NotExecuted`.
Optional runtime expression-backed parser action executors can be configured explicitly. Callers may use the prepared expression runtime policy builder to prepare inline parser actions from the parser model before parsing, build the registry, and return a policy configured with no-compile prepared adapters. Lower-level callers may still use the prepared expression registry builder directly, including for inline actions nested in runtime-executable structures and direct-left-recursive tails, or use the older expression-backed executor that may compile opportunistically during execution with compilation caching. Both expression paths are limited to the configured expression language and read-only contextual symbols, are not enabled by default, and do not increase default ANTLR action support.

`Utils.Parser.Generators` now provides a separate C# source-generation path for inline parser actions. For generated grammars, simple, multi-statement, and multi-line C# statement bodies such as `{ OnAction(context); }` or blocks containing local variables are emitted as private generated C# hook methods, compiled by Roslyn with the consuming project, and dispatched through a generated `IParserActionExecutor` only when `ParseWithEmbeddedCode(...)` or `CreateRuntimePolicy(...)` is used. User action code can call members supplied by another part of the generated partial class. The existing generated `Parse(...)` helper keeps the default conservative policy and does not execute these generated action hooks. Dispatch is tested against the runtime indexes used for single-item alternatives, sequence elements, quantified content, same-source hooks in different alternatives, and direct-left-recursive tail views because generated helpers resolve the generated definition before parsing with the generated policy. Parser actions inside negation probes are not documented as supported by this source-generator path. Invalid C# in this path is a compile-time C# error. `UP1028` remains reserved for explicit execution-disabled runtime policies, which the current expression-backed adapters do not expose.

**Usage** — implement `IParserActionExecutor`:

```csharp
public class MyActionExecutor : IParserActionExecutor
{
    public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
    {
        // context.ActionCode     — raw action text from the .g4 file
        // context.Rule           — current Rule
        // context.InputPosition  — current token index
        // context.AlternativeIndex / ElementIndex — position within the rule

        Console.WriteLine($"Action in rule {context.Rule.Name}: {context.ActionCode}");
        return ParserActionExecutionOutcome.Executed;
    }
}

var policy = ParserRuntimeFeaturePolicy.Default with
{
    ParserActionExecutor = new MyActionExecutor()
};
var parser = new ParserEngine(definition, policy);
```

> **Important**: actions may fire in branches that are later rejected by backtracking. There is no rollback mechanism. Keep executors side-effect-free or idempotent where possible.

---

### Rule actions `@init { }` and `@after { }`

See [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md) for future-safe boundaries between stored metadata and explicit execution paths.


**Standard ANTLR4**: `@init` runs before the rule body; `@after` runs after.

**Utils.Parser**: Both are parsed and stored in `Rule.InitAction` / `Rule.AfterAction` as raw text. They are not executed automatically. To act on them, inspect the `Rule` model and invoke custom logic at the appropriate parse-tree traversal step using `ParseTreeCompiler<TContext, TResult>`.

---

### Runtime observation

**Standard ANTLR4**: no built-in scheduling observation API.

**Utils.Parser**: a passive, non-authoritative observer can be attached via `ParserRuntimeFeaturePolicy.RuntimeObserver`. It receives `AlternativeRuntimeObservation` records describing scheduler events in deterministic order without affecting parse outcomes.

**Usage**:

```csharp
var recorder = new RuntimeObservationRecorder();
var policy = ParserRuntimeFeaturePolicy.Default with { RuntimeObserver = recorder };
var parser = new ParserEngine(definition, policy);
parser.Parse(tokens);

string text = RuntimeObservationTextWriter.Write(recorder.Observations);
string json  = RuntimeObservationJsonWriter.Write(recorder.Observations);
```

See `RuntimeObservationAndExportContract.md` for the full contract.

---

## Partially supported

| Feature | Limitation |
|---|---|
| Direct left recursion | Detected at resolution time and handled with a seed-and-extend loop, but not equivalent to all ANTLR4 left-recursive shapes. Emits `LeftRecursivePrecedencePartiallySupported` where applicable. |
| Precedence predicates `precpred(_ctx, N)` | Regex-based extraction. Falls back to precedence `0` if the level cannot be parsed. Only recognised in direct left-recursive rules. Not routed through semantic predicate evaluation and does not emit `UP1006`. |
| Right-associativity `<assoc=right>` | Parsed and applied during left-recursive extension. Only meaningful within direct left-recursive rules; subject to the same partial-parity limits as left recursion. |
| Labels — `e=expr` and `ids+=ID` | Applied when the labelled item is a `RuleRef`. Labels targeting literals and other non-reference items are recognized and ignored with explicit diagnostic `UP1022 LabelOnNonRuleReferenceIgnored`. |
| `import` | Fully resolved when grammars are compiled as a project set (`Antlr4GrammarProjectCompiler`). Single-file compilation emits `ImportParsedButNotResolved`. |
| `options { tokenVocab = MyLexer; }` | Dependency loading depends on available resolver inputs at compilation time. |
| Unknown grammar options (`visitor`, `listener`, `contextSuperClass`, …) | Parsed and preserved as raw option metadata, but rejected with `UP1021 UnsupportedAntlrOptionIgnored`. Recognised options that do not trigger this diagnostic are: `tokenVocab`, `superClass`, `caseInsensitive`, and `language`. |
| Lexer commands | Only the seven built-in commands are accepted. Any unknown command name is rejected with `UnsupportedLexerCommand`. |
| `tokens { }` block | Recognised, stored in `GrammarExtensionBinding.DeclaredTokens`, and reported explicitly with `UP1002 TokensBlockIgnored`. Not mapped to runtime token definitions. |
| `channels { }` block | Recognised, stored in `GrammarExtensionBinding.DeclaredChannels`, and reported explicitly with `UP1003 ChannelsBlockIgnored`. Not mapped to runtime channel semantics beyond `-> channel(...)` command support. |

---

## Parsed and stored — no runtime semantics

These constructs are recognised without error but produce no runtime effect.

| Construct | Stored where | Runtime behaviour |
|---|---|---|
| Rule parameters `rule[int x]` | `Rule.Parameters` as raw text | No argument passing, no typed binding, no invocation frame. |
| Rule returns `returns [int x]` | `Rule.ReturnType` as raw text | Recognized, ignored by runtime semantics, and reported with `UP1007 RuleReturnsIgnored`. |
| `locals [...]` | Parsed at rule level and surfaced with `UP1008 RuleLocalsIgnored` | Recognized, ignored, not executed, and no runtime invocation frame is created. |
| `throws ExceptionType` | Parsed at rule level and surfaced with `UP1023 RuleExceptionMetadataIgnored` | Recognized, ignored, not executed, and no runtime invocation frame is created. |
| `catch [...] {...}` / `finally {...}` | Parsed at rule level and surfaced with `UP1023 RuleExceptionMetadataIgnored` | Recognized, ignored, not executed, and no runtime invocation frame is created. |
| Grammar-level actions `@header`, `@members`, etc. | `ParserDefinition.Actions` | Metadata only; not executed. See [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md). |
| Other rule actions (not `@init`/`@after`) | Parsed, discarded | `ActionIgnored` diagnostic emitted. |

---

## Shared prequel metadata and diagnostics boundary

ANTLR prequel metadata is normalized through `Utils.Parser.Antlr4.Common`.

Runtime and generator parsing remain separate, but both paths map prequel metadata to `Antlr4PrequelModel` and can validate it through `Antlr4PrequelValidator`.

`Antlr4PrequelValidator` emits neutral diagnostic facts only. Runtime and generator remain responsible for mapping those facts to `ParserDiagnostics`.

Known intentional differences:

- runtime does not currently emit `UP1004` for grammar-level actions, although neutral validation facts expose them;
- generator import diagnostics may differ in granularity from neutral import facts.

---

## Runtime metadata boundary

Continuation metadata descriptors are internal runtime metadata.
They are prepared after grammar resolution.
They are not ANTLR grammar constructs.
They are preserved/normalized as descriptive metadata only.
They are never executed, replayed, or resumed.

---

## Not supported — intentional exclusions

These capabilities are outside the current runtime model by design. Attempting to use them produces explicit diagnostics or has no effect.

| Capability | Diagnostic / behaviour |
|---|---|
| Indirect left recursion | Error `UP0xxx` — explicitly unsupported. Rewrite as direct recursion or factor out the common prefix. |
| Adaptive LL / GLL prediction | Not implemented. The runtime uses sequential backtracking with memoization. |
| Error recovery (resync, token insertion/deletion) | No recovery strategy. `ParserEngine.Parse()` returns `ErrorNode` on failure. |
| Speculative parsing / continuation replay | Not implemented. Continuation metadata exists but is descriptive only. |
| Parse-forest generation | A single parse tree is produced. |
| Async or parallel parsing | Not implemented. |
| Target-language action execution engines | No target-language engine is active by default. Inline parser actions can execute only through an explicit runtime policy or generated C# opt-in path; lexer actions, grammar actions, `@members`, `@init`, and `@after` remain non-executable. |
| `superClass` class inheritance (generated code) | `superClass` is repurposed as an extension-binding key. See the **Usage** section above. |

---

## Runtime/Generator diagnostics parity inventory

| Diagnostic | Runtime | Generator | Equivalent | Notes |
|---|---|---|---|---|
| `UP1001` ImportParsedButNotResolved | Emitted when `import` is parsed but unresolved. | Emitted when `import` is parsed but unresolved. | Yes | Deterministic recovery: keep parsing and preserve import metadata. |
| `UP1002` TokensBlockIgnored | Emitted when `tokens { ... }` is parsed. | Emitted when `tokens { ... }` is parsed. | Yes | Deterministic recovery: keep parsing and preserve declared token names. |
| `UP1003` ChannelsBlockIgnored | Emitted when `channels { ... }` is parsed. | Emitted when `channels { ... }` is parsed. | Yes | Deterministic recovery: keep parsing and preserve declared channel names. |
| `UP1004` ActionIgnored | Emitted for ignored grammar/rule actions outside supported lifecycle slots. | Emitted for ignored grammar-level actions. | Partial | Runtime has broader rule-prequel coverage; this remains intentional and documented. |
| `UP1005` InlineActionStoredNotExecuted | Emitted for inline `{ ... }` action nodes when no action executor handles them. | Suppressed for parser inline actions because they are supported generated C# hooks in the source-generator path; lexer actions use `UP1029`. | Partial | Deterministic recovery remains metadata-preserving. The generator no longer reports supported parser inline actions as unsupported. |
| `UP1006` SemanticPredicateNotEnforced | Emitted for `{ ... }?` nodes in conservative runtime policy mode. | Suppressed for parser semantic predicates because they are supported generated C# hooks in the source-generator path; lexer predicates use `UP1029`. | Partial | Invalid C# in a supported generated predicate remains a Roslyn compilation error. |
| `UP1029` EmbeddedCodeConstructNotExecutedByGenerator | Not emitted by runtime. | Emitted as a source-generator warning for visible embedded-code constructs that are preserved or recognized but not executed by generated C# hooks, including lexer actions/predicates, grammar actions, `@members`, `@init`, and `@after`. | Generator-only | This diagnostic does not add execution and does not alter `ParserEngine`; only parser semantic predicates and inline parser actions are promoted to generated C# hooks. |

Intentional remaining difference: runtime diagnostics can include broader rule-context metadata for rule-prequel constructs (`returns`, `locals`, exception metadata) that are outside generator parser scope.
Additional intentional test-documented difference: malformed prequel inputs currently fail fast in runtime conversion (`GrammarParseException`) while generator parsing keeps best-effort recovery.

---

## Diagnostics quick reference

| Prefix | Severity | Meaning |
|---|---|---|
| `UP0xxx` | Error | Blocking — unresolved rules, grammar violations, import failures |
| `UP1xxx` | Warning | Compatibility behavior that is recognized and ignored / partially normalized (e.g. `UP1002` tokens block ignored, `UP1003` channels block ignored, `UP1007` rule returns ignored, `UP1020` unsupported lexer command ignored, `UP1021` option ignored, `UP1022` label ignored on non-rule reference, `UP1029` embedded code construct not executed by generator) |
| `UP5xxx` | Warning | Best-effort recovery warnings (trailing tokens, ambiguity) |
| `UP8xxx` | Info | Informational runtime events |
| `UP9xxx` | Debug | Detailed execution traces |
| `PARSER0xx` | Warning/Info | Runtime safety guards (cycle detection, non-progressive loop termination) |
| `APU0xxx` | Error/Warning | Source-generator diagnostics (Roslyn pipeline) |

Full descriptor table: `ParserDiagnostics.All`.

### Shared runtime indexing metadata

Parser embedded-code discovery now has a shared metadata model in `Utils.Parser.EmbeddedCode`. `EmbeddedCodeRuntimeDiscovery` walks a `ParserDefinition` and emits `EmbeddedCodeRuntimeEntry` values with the raw source, `EmbeddedCodeKind`, owning rule name, runtime-compatible alternative and element indexes, a runtime key for executable entries, and an explicit `EmbeddedCodeUnsupportedReason` for skipped entries. The metadata mirrors the existing parser runtime indexing rules for priority-ordered alternatives, single-item alternatives, sequences, quantifier inner parsing, negation probes, and direct-left-recursive base/tail alternatives. It is metadata only: it does not compile source, generate C#, execute actions, or change `ParserEngine` behavior.

The expression-backed prepared registry consumes this shared discovery result before invoking its preparer. Unsupported constructs such as grammar actions, `@init`, `@after`, lexer actions/predicates, and non-inline parser actions remain non-executable, but they now carry explicit skip reasons. The C# source-generator path additionally reports `UP1029 EmbeddedCodeConstructNotExecutedByGenerator` for unsupported embedded-code constructs that are visible in its `G4Grammar` model, such as lexer actions, lexer predicates, grammar actions, `@members`, `@init`, and `@after`. These diagnostics are warnings only: they do not execute the code, do not modify `ParserEngine`, and do not change generated `Parse(...)` behavior. Invalid C# in a source-generator-supported hook remains a Roslyn compilation error rather than a custom parser diagnostic.

