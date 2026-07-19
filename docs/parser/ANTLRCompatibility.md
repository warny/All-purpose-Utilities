# ANTLR4 Compatibility Reference

This document lists ANTLR4 grammar features and their support status in Utils.Parser.

For each feature that behaves differently from standard ANTLR4, a **Usage** section explains how to achieve the equivalent result.

This document must be consulted before modifying any grammar-related component, and updated whenever a feature's support status changes.

> Note: this reference is complementary to `Antlr4CompatibilityMatrix.md` (high-level matrix).
> Use this file for implementation-oriented usage guidance when behavior differs from standard ANTLR4.

> Rule arguments and returns integration plan: see [`RuleArgumentsAndReturnsPlan.md`](./RuleArgumentsAndReturnsPlan.md) for the design split between conservative runtime behavior, explicit runtime policies, generated-C# opt-in helpers, metadata-only facts, rollback boundaries, and the generated-C# positional binding boundary. In short, rule parameters are parsed as metadata, rule-call raw arguments are preserved, manual/explicit runtime policy support exists for limited literals, generated C# emitted with `enableGeneratedRuleArgumentBinding` performs narrow exact-arity positional simple-literal binding on `ParseWithEmbeddedCode(...)` overloads without a `basePolicy`, the overload with `basePolicy` preserves the caller rule-call policy, return metadata and managed return snapshots exist, and ANTLR-style return access remains partial and opt-in.

---

## Generated-C# positional rule-call argument binding

Generated parsers emitted with generated rule-argument binding expose distinct surfaces for `callee[...]` rule-call arguments. The shipped generator exposes an explicit project-wide opt-in, disabled by default, through the MSBuild property `UtilsParserEnableGeneratedRuleArgumentBinding`. Consumers get metadata/helper behavior until they set that property to `true`.

| Surface | Behavior |
|---|---|
| `Parse(...)` | Metadata only; no generated embedded-code hooks and no argument binding are executed. |
| `ParseWithEmbeddedCode(...)` without `basePolicy` | When emitted with the generation flag enabled, installs the generated-C# rule-call wrapper, requires exact positional arity, converts supported simple literals through the typed positional literal policy, and submits one managed seed batch. |
| `ParseWithEmbeddedCode(input, executionContext, basePolicy)` | Preserves the caller's `RuleCallExecutionPolicy`; generated-C# automatic argument binding is deliberately disabled. |

When generated, the automatic generated-C# subset is intentionally narrower than ANTLR4: positional only, simple literals only, allowlisted declared target types only, exact arity only, no omitted/default argument consumption, no named or mixed arguments, no arbitrary expressions, no generated ANTLR rule signatures, and no `Parse(...)` behavior change. The generated wrapper delegates conversion to `TypedPositionalLiteralRuleCallExecutionPolicy` with throwing failures, so invalid bindings fail before child lifecycle hooks observe partial seeds. Managed seeds participate in the existing generated execution-context rollback and state-aware memoization boundaries.

## Compatibility level estimate

This project should not claim full ANTLR4 compatibility. A practical estimate is:

- common grammar syntax and deterministic runtime tokenization/parsing: about 60-65% of practical ANTLR4 usage;
- strict ANTLR4 compatibility including target-language actions, attributes, generated APIs, adaptive prediction, and full embedded-code semantics: about 40-45%;
- embedded-code compatibility specifically: about 35-40%.

These numbers are approximate maintenance guidance, not a formal conformance score.

The runtime supports common grammar forms, lexer modes, commands, project imports, token vocabularies, direct left recursion with limits, semantic predicates/actions through explicit opt-in paths, and bounded generated-C# lexer action/predicate support.

The project still intentionally does not support full ANTLR target-language semantics, full attribute syntax, full automatic typed parameters/returns/locals/labels beyond the documented generated-C# positional literal subset, lexer predicate attributes, runtime-inline lexer execution, adaptive LL prediction, general action buffering/replay, full external side-effect rollback, or full ANTLR-generated API parity.

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
- embedded code is preserved unchanged by default through `NoOpParserEmbeddedCodeTransformer`;
- any embedded-code transformation must go through `IParserEmbeddedCodeTransformer`;
- generated mode emits transformed target-language code;
- dynamic mode sends transformed code to the existing compiler/preparer mechanism before producing prepared predicates/actions;
- the runtime-inline prepared expression path is available as an explicit opt-in for callers that provide an `IExpressionCompiler`;
- the source-generator C# path is available as an explicit opt-in for generated grammars;
- ANTLR-style `$...` convenience forms such as `$x.value`, `$xs.value`, `$rule.value`, `$param`, and `$local` are not core parser syntax; the optional C# embedded-code transformer rewrites only documented forms such as `$param`, `$local`, declared bare `$returnName`, assignment-label child return reads (`$x.value`), and list-label return projections (`$xs.value`) in supported parser action locations, while other child/current-rule dotted conveniences remain unsupported and the default no-op transformer leaves all forms unchanged;
- runtime-inline lexer actions and predicates, non-inline parser actions, generalized action buffering/replay, complete ANTLR transactional semantics, and arbitrary external parser state mutation remain unsupported for execution; simple lexer inline actions and predicates are supported only by the generated-C# opt-in path, including tested fragments, rule references, simple quantifiers, duplicate hook positions, and false-predicate/action ordering;
- `ParserExecutionContextCopier<TContext>` exists as a runtime helper for execution-context snapshot/fork/commit work. Generated execution contexts expose internal `Fork()`, `CopyFrom(...)`, and `GetExecutionStateKey()` helpers. `ParserRuntimeFeaturePolicy` also exposes `IParserExecutionStateManager`; the default policy uses the no-op `NullParserExecutionStateManager`, and generated policies always install a `GeneratedExecutionStateManager` that captures/restores with `Fork()` / `CopyFrom(...)` and supplies semantic memoization keys with `GetExecutionStateKey()`. `ParserEngine` now captures and restores managed parser execution state around parser backtracking attempt boundaries: ordinary alternatives, left-recursive extensions, quantifier attempts, and negation probes. This does not provide complete ANTLR transactional semantics and does not enable action buffering or external side-effect rollback.

### Runtime policy API compatibility note

`ParserRuntimeFeaturePolicy.ExecutionStateManager` and `ParserRuntimeFeaturePolicy.RuleInvocationFrameManager` are required policy properties. Existing code that derives a policy from `ParserRuntimeFeaturePolicy.Default` remains compatible because the default policy already supplies `NullParserExecutionStateManager.Instance` and `NullParserRuleInvocationFrameManager.Instance`:

```csharp
var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = customEvaluator
};
```

External callers that construct a policy directly must now provide an execution-state manager and rule invocation-frame manager explicitly. To preserve the current conservative/no-op behavior, use `NullParserExecutionStateManager.Instance` and `NullParserRuleInvocationFrameManager.Instance`:

```csharp
var policy = new ParserRuntimeFeaturePolicy
{
    SemanticPredicateEvaluator = new DefaultSemanticPredicateEvaluator(),
    ParserActionExecutor = new DefaultParserActionExecutor(),
    ExecutionStateManager = NullParserExecutionStateManager.Instance,
    RuleInvocationFrameManager = NullParserRuleInvocationFrameManager.Instance
};
```

`ParserEngine` validates that both managers are non-null, uses `GetCurrentStateKey()` to isolate completed-rule memoization entries, stores post-rule execution-state snapshots in reusable completed results, restores those snapshots on memoization hits without replaying actions, and captures/restores the manager around parser backtracking attempt boundaries. Parser lifecycle hooks participate in that managed rollback through generated C# opt-in policies. Lifecycle contexts now also expose an optional passive `ParserRuleInvocationFrame`. Rule invocation frames can carry a passive `ParserRuleInvocationDescriptor` populated from metadata already present in the parser model, such as represented rule parameters, returns, locals, throws/catch/finally metadata, and rule options. Descriptors are preparatory observation containers only: rule parameters, locals, returns, throws/catch/finally metadata, and rule options remain metadata-only and are not executed, typed, bound, allocated, propagated, or applied. Locals and exception metadata preserve raw text when ingestion exposes it, but are not invented when the parser model does not expose them. Action buffering remains unsupported.

### Embedded-code transformer compatibility note

`IParserEmbeddedCodeTransformer` is the only supported hook for rewriting embedded parser code. It receives `ParserEmbeddedCodeTransformationContext` with passive metadata (`Code`, `Location`, `GrammarName`, `RuleName`, `Parameters`, `Locals`, `Returns`, and `Labels`) and returns `ParserEmbeddedCodeTransformationResult` with target-language code plus optional diagnostics. `ParserEmbeddedCodeDiagnostic`, `ParserEmbeddedCodeDiagnosticSeverity`, and `ParserEmbeddedCodeLocation` describe transformation feedback and source location.

`NoOpParserEmbeddedCodeTransformer` is the default and returns code unchanged. It is the correct default when grammar actions are already valid target-language code. Runtime preparation and generated emission materialize grammar text as `RawEmbeddedCode`, invoke the transformer through `ParserEmbeddedCodeTransformationService.TransformOrThrow(...)`, validate diagnostics, and then hand `TransformedEmbeddedCode` to the compiler or C# emitter. `TransformedEmbeddedCode` has no public constructor, so consumer paths cannot intentionally accept raw text when transformed code is required. The transformer is not a compiler: dynamic execution still uses the existing compiler/preparer supplied by the caller, and generated mode still relies on Roslyn compiling the generated C# project. Transformer error diagnostics prevent the downstream compiler/preparer from being invoked for that code block.

Optional ANTLR-style convenience transformers must be target-language-specific. For example, a C# transformer may rewrite `$...` convenience syntax to generated helper calls, but that is compatibility behavior outside parser core and does not imply full ANTLR action compatibility.

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
-> ParseWithEmbeddedCode(...) with a fresh context, or CreateRuntimePolicy(context, ...) with an explicit context
-> ParserEngine
```

This path:

- does **not** use `IExpressionCompiler`;
- emits C# source hooks for supported parser embedded-code constructs plus simple lexer inline actions and simple lexer predicates;
- injects supported parser header blocks near the top of the generated C# file;
- lets Roslyn compile the generated hooks and injected C# with the consuming project;
- leaves invalid C# as normal C# compilation errors;
- keeps generated `Parse(...)` conservative;
- executes generated hooks only through `ParseWithEmbeddedCode(...)` or a policy returned by `CreateRuntimePolicy(executionContext, basePolicy)`;
- binds generated dispatchers to an explicit execution context rather than shared static state; `ParseWithEmbeddedCode(string)` creates a fresh context for that parse, while reusing a context or a policy bound to that context intentionally reuses its state;
- injects unscoped `@header` and `@parser::header` near the top of the generated C# file before generated type declarations;
- injects unscoped `@members` and `@parser::members` into that context, not into the static facade.

### Supported executable parser constructs

The runtime-inline path can execute parser semantic predicates and inline parser actions only through explicit opt-in policies. The source-generator C# path also supports generated C# parser lifecycle hooks plus parser header/member injection as a compatibility bridge, again without changing generated `Parse(...)`.

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
- unscoped `@header` and `@parser::header` blocks injected verbatim near the top of the generated C# file as a parser-header compatibility bridge;
- unscoped `@members` and `@parser::members` blocks injected verbatim into `{ClassName}ExecutionContext` as a C# compatibility bridge;
- unscoped `@footer` and `@parser::footer` blocks injected verbatim near the end of the generated C# file after generated type declarations as a trailing parser-footer compatibility bridge;
- `@lexer::header` blocks injected verbatim after parser headers and before generated type declarations as a limited lexer-header compatibility bridge;
- `@lexer::members` blocks injected verbatim into `{ClassName}ExecutionContext` after parser members as a limited lexer-members compatibility bridge;
- `@lexer::footer` blocks injected verbatim after parser footers near the end of the generated C# file as a limited lexer-footer compatibility bridge;
- rule `@init` lifecycle hooks executed at rule entry before any alternative is tried;
- rule `@after` lifecycle hooks executed after a successful rule result.

Parser and lexer grammar-level named-action injection is source-generator C# compatibility only. Lexer `@lexer::*` support mirrors the parser named-action injection points, uses dedicated lexer markers, and does not create a separate ANTLR lexer runtime or connect lexer inline actions/predicates. `$...` rewriting is skipped for grammar-level lexer header/member/footer blocks because they are not tied to a current parser rule.

Parser header injection is source-generator C# compatibility only: ordinary C# header content such as `using` directives is emitted verbatim before generated type declarations, invalid C# is reported by Roslyn, and this does not imply full ANTLR target-language compatibility. Parser footer injection is also source-generator C# compatibility only: footer code is emitted verbatim as trailing generated C# source near the end of the generated parser file, after generated type declarations and still inside the generated namespace when a file-scoped namespace is used. The parser footer injection point is not a second header region and does not claim `using` directives are valid there; invalid C# is reported by Roslyn. Generated predicate hooks expose `context`, `ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`, and `predicateCode`. Generated action hooks expose `context`, `ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`, and `actionCode`. Generated lifecycle hooks expose `context` (a `ParserRuleLifecycleContext`) and the rule name. Hooks are instance methods on the generated `{ClassName}ExecutionContext`, so injected parser members can hold isolated instance state. `ParseWithEmbeddedCode(string input)` creates a new context per call; `ParseWithEmbeddedCode(string input, {ClassName}ExecutionContext executionContext)` lets advanced callers provide and inspect a context explicitly; and `CreateRuntimePolicy({ClassName}ExecutionContext executionContext, ParserRuntimeFeaturePolicy? basePolicy = null)` binds a policy to the supplied context. Generated policies always install a `GeneratedExecutionStateManager`, enabling `ParserEngine` to roll back context mutations from parser backtracking attempt boundaries for all generated execution contexts. `GeneratedRuleLifecycleExecutor` is installed only when the grammar declares `@init` or `@after` hooks; otherwise `RuleLifecycleExecutor` remains the base no-op executor. Reusing a context or a policy bound to it intentionally reuses the same member state. No generated `CreateRuntimePolicy()` overload creates a hidden context. This context is a generated parser execution context, not a lexer mode or lexer-state context. Lexer members injected there are ordinary generated-C# compatibility members only and do not imply lexer-state-aware execution semantics. Generated `Parse(...)` remains conservative and does not execute hooks or create an execution context.

### Rule locals and exception metadata

Rule locals and exception metadata are preserved as passive raw metadata when ANTLR4 ingestion exposes them. `ParserRuleInvocationDescriptor` can surface this preserved metadata through `RawLocals`, `Locals`, and `Exceptions`, and local descriptors also preserve a conservative `RawType` prefix when the declaration has a clean `type name` shape. Generated C# execution contexts expose explicit lifecycle helpers (`GetRuleLocal`, `TryGetRuleLocal`, `SetRuleLocal`, and `GetRuleLocalDescriptors`) for reading/writing only the active `context.InvocationFrame` locals store and inspecting local descriptors in opt-in generated hooks. An optional generated C# embedded-code transformer may support read-only bare `$localName` for current-rule locals by emitting typed helper access from the declared raw type. This read does not convert values: the runtime value must already be compatible with the declared type, nullable/reference reads may receive `null`, and non-nullable value-type reads of `null` fail deterministically. Local writes still require explicit helpers such as `SetRuleLocal(context, "name", value)`. Local declarations are not initialized to typed defaults, no typed local fields/properties or implicit local variables are generated, `throws` clauses do not affect parser exception behavior, `catch` and `finally` actions are not executed, and no typed ANTLR-compatible rule invocation semantics or generated parser signature changes are implemented. Generated `Parse(...)` remains conservative.

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

### Predicate options

Predicate options (`{ condition }?<fail=...>`) are parsed by the meta-grammar and produce a `ValidatingPredicate` from the condition text. The options content (`<fail=...>`) is recognized but not stored or executed. `UP1030` (`PredicateOptionsIgnored`) is emitted when options are present. This is a compatibility-oriented diagnostic only; the predicate itself is still created and evaluated through the standard semantic-predicate path.

### Unsupported / represented-only constructs

The following constructs may be represented as metadata when visible to ingestion, but they are not executed by the current embedded-code paths:

- lexer actions;
- lexer predicates;
- grammar actions other than the source-generator C# parser-header, parser-members, parser-footer, lexer-header, lexer-members, and lexer-footer compatibility bridges;
- `@header` / `@parser::header` in the runtime-inline path (supported only as source-generator C# parser-header injection);
- `@members` / `@parser::members` in the runtime-inline path (supported only as source-generator C# execution-context injection);
- `@footer` / `@parser::footer` in the runtime-inline path (supported only as source-generator C# trailing parser-footer injection);
- parser actions that are not inline alternative elements;
- action buffering;
- complete ANTLR transactional semantics;
- controlled context mutation models beyond the configured execution-state manager;
- arbitrary parser state mutation outside the managed execution state.

Rule lifecycle hooks (`@init`/`@after`) are supported in the source-generator C# path; see the **Rule actions** section above. They are not part of the runtime-inline expression path.

Managed execution-state rollback covers parser backtracking attempt boundaries. It does not imply automatic rollback of a caller-supplied execution context after a top-level parse is rejected for trailing tokens or other final validation failures.

When visible through runtime discovery, unsupported constructs must remain classified with explicit `EmbeddedCodeUnsupportedReason` values rather than being treated as executable metadata. The existence of stored source text is not execution authority.

### Default behavior

Default parsing remains conservative:

- `ParserEngine` does not execute embedded code unless a caller supplies an explicit policy;
- `ParserRuntimeFeaturePolicy.Default` does not evaluate predicate source or execute action source;
- semantic predicates that are not evaluated are conservatively accepted, and `UP1006` is emitted when applicable;
- parser actions whose executor returns `NotExecuted` remain non-executed, with existing runtime diagnostics when applicable;
- generated `Parse(...)` uses the conservative default policy;
- generated `ParseWithEmbeddedCode(...)` is the opt-in generated C# embedded-code path.

### Runtime execution-context copy helper

`Utils.Parser.Runtime.ParserExecutionContextCopier<TContext>` is a public preparatory helper for future source-generator execution-context snapshot designs. Generated `{ClassName}ExecutionContext` classes expose internal `Fork()` and `CopyFrom({ClassName}ExecutionContext source)` helpers that delegate to this copier. `Fork()` calls `Copy(this, static () => new {ClassName}ExecutionContext())`, so `source.Clone()` is used first when the context implements `ICloneable`; the clone result must be non-null and assignable to `TContext`, and the factory is used only for non-cloneable sources. `CopyFrom(source)` validates `source` and calls `CopyTo(source, this)`. `CopyTo(source, target)` intentionally does not use `ICloneable` because it must copy into the supplied target instance.

When field copying is used, the helper copies context fields by reflection-backed inspection once per context type, emits a compiled field-copy delegate, and reuses the cached delegate for subsequent field-copy calls. Field-copy behavior is shallow structural copying. Arrays, `List<T>`, `Dictionary<TKey,TValue>`, and `HashSet<T>` fields are recreated through explicit copy expressions when non-null, while contained elements are not deep-cloned. Unknown `IEnumerable<T>` collection fields can be recreated when they expose a compatible public copy constructor, a public parameterless constructor plus `AddRange(IEnumerable<T>)`, or a public parameterless constructor plus `Add(T)`. Unknown collections without a safe reconstruction strategy, and other unrecognized reference fields, are assigned by reference. Static fields are skipped. Field-like event backing fields are skipped. Readonly instance fields are rejected with an explicit configuration exception so context authors must choose mutable state or wait for a later custom context strategy. The semantics of `ICloneable.Clone()` belong to the user context type.

These helpers and `IParserExecutionStateManager` are infrastructure, not direct ANTLR construct support. They do not execute lexer actions or predicates, do not buffer actions, and do not alter generated `Parse(...)` conservative embedded-code behavior. `ParserEngine` calls the manager around parser backtracking attempt boundaries for managed execution-state rollback. Rule lifecycle hooks (`@init`/`@after`) are now supported in the source-generator C# path. Generated policies always install a `GeneratedExecutionStateManager`, making rollback active for all generated execution contexts. `GeneratedRuleLifecycleExecutor` is installed only when the grammar declares `@init` or `@after` hooks; otherwise `RuleLifecycleExecutor` remains the base no-op executor.

### Diagnostics

Current diagnostics boundaries are intentionally split by path:

- invalid generated C# is reported by Roslyn as C# compilation diagnostics;
- unsupported embedded-code constructs are classified with explicit unsupported reasons when visible to runtime discovery;
- runtime non-evaluation/non-execution uses existing runtime diagnostics such as `UP1006` and `UP1005` when applicable;
- rich source-generator diagnostics for every unsupported embedded-code construct are not yet implemented unless already covered by existing generator diagnostics.

### Known limitations

Known limitations include:

- no complete automatic rollback or action buffering beyond managed execution-state capture/restore at parser backtracking attempt boundaries;
- execution-context rollback through the generated state manager is active for all parser backtracking attempt boundaries for all generated execution contexts (predicates, inline actions, and lifecycle hooks share the same state-aware infrastructure);
- inline actions in negation probes require caution and are not a general side-effect-safe model;
- no rollback of external side effects or top-level final parse rejection for caller-supplied execution contexts;
- no lexer embedded-code execution;
- generated C# supports limited parser `@header` / `@parser::header`, parser `@members` / `@parser::members`, parser `@footer` / `@parser::footer`, and lexer-scoped `@lexer::header` / `@lexer::members` / `@lexer::footer` injection only; grammar-level metadata remains non-executable outside those generated compatibility bridges;
- the source-generator C# path does not parse C# semantically; it applies light body normalization and leaves validation to Roslyn;
- the generator hook collector remains separate from `EmbeddedCodeRuntimeDiscovery`.

### Recommended next steps

Recommended next steps are:

1. design lexer predicate/action semantics explicitly before enabling lexer embedded code;
2. design action buffering/replay rules before supporting side-effect-sensitive external actions;
3. evaluate deeper alignment between the generator `G4Grammar` collector and `EmbeddedCodeRuntimeDiscovery`;
4. expand the ANTLR grammar corpus used for compatibility and regression checks.

---

### Semantic predicates `{ condition }?`

See [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md) for the two-path execution boundary (source generation C# vs runtime expression compilation) and project responsibility map.

The shared embedded-code diagnostics taxonomy is documented in `EmbeddedCodeExecutionModel.md` and `ParserDiagnostics`.


**Standard ANTLR4**: The predicate body is target-language code evaluated inline during parsing.

**Utils.Parser**: Predicates are parsed and stored. Evaluation is delegated to an `ISemanticPredicateEvaluator` registered in `ParserRuntimeFeaturePolicy`. The default policy returns `NotEvaluated` without detailed diagnostic metadata, which does **not** reject the branch — it acts as if the predicate passed and `ParserEngine` emits `UP1006`.
Optional expression-backed evaluators can be configured explicitly (for example through `omy.Utils.Parser.Expressions`) to enforce predicate outcomes. One adapter uses a caller-provided `IExpressionCompiler` and may compile opportunistically during evaluation with compilation caching; this is documented as an intermediate implementation detail. A separate prepared-artifact adapter consumes `PreparedExpressionSemanticPredicate` instances from an explicit registry without compiling during evaluation. `omy.Utils.Parser.Expressions` also exposes an opt-in policy builder that prepares predicates from a `ParserDefinition`, builds the registry, and returns a `ParserRuntimeFeaturePolicy` configured with prepared adapters. Neither expression path is selected by default, and `ParserEngine` does not prepare predicates automatically.

`Utils.Parser.Generators` now provides a separate C# source-generation path for parser semantic predicates. For generated grammars, simple C# predicate expressions such as `{ true }?`, `{ false }?`, and `{ inputPosition == 0 }?` are emitted as private generated C# hook methods, compiled by Roslyn with the consuming project, and executed only when the generated `ParseWithEmbeddedCode(...)` helper or an explicit-context `CreateRuntimePolicy(executionContext, basePolicy)` result is used. The existing generated `Parse(...)` helper keeps the default conservative policy. Dispatch is tested against the runtime indexes used for single-item alternatives, sequence elements, quantified content, negation predicate probes, same-source hooks in different alternatives, and direct-left-recursive tail views because generated helpers resolve the generated definition before parsing with the generated policy. Invalid C# in this path is a compile-time C# error, not a runtime expression-compilation diagnostic.

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
Custom predicate evaluators may satisfy or reject predicates. The optional prepared expression path can build a registry from parser-model `ValidatingPredicate` nodes, including predicates nested in runtime-executable structures and direct-left-recursive tails, and wire it through `ParserRuntimeFeaturePolicy` explicitly; it is not enabled by default and does not change the compatibility level. Generated grammars can instead use generated C# hooks through `ParseWithEmbeddedCode(...)` or a policy returned by `CreateRuntimePolicy(executionContext, basePolicy)`; this source-generation path supports predicate expressions and predicate blocks with `return`, and it is source generation, not `IExpressionCompiler` usage. Roslyn remains responsible for validating whether the generated hook body is valid C# and returns `bool`.
This behavior is runtime-policy-driven or generated-policy-driven, not compatibility metadata.

> **Important**: completed-rule memoization is keyed by `(rule, input position, precedence, execution-state key)`. If semantic state can make a rule parse differently, the configured `IParserExecutionStateManager` must return a different `ParserExecutionStateKey` for those states. The no-op manager returns `ParserExecutionStateKey.Stateless`, preserving the former effective key shape for stateless parsing. Completed memoized results carry a post-rule execution-state snapshot that is restored on cache hits without replaying actions. After parser attempt-boundary rollback, the restored manager state must produce the restored key so later cache lookups use the correct state-aware entry.

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

`Utils.Parser.Generators` now provides a separate C# source-generation path for inline parser actions. For generated grammars, simple, multi-statement, and multi-line C# statement bodies such as `{ OnAction(context); }` or blocks containing local variables are emitted as private generated C# hook methods, compiled by Roslyn with the consuming project, and dispatched through a generated `IParserActionExecutor` only when `ParseWithEmbeddedCode(...)` or an explicit-context `CreateRuntimePolicy(executionContext, basePolicy)` is used. User action code can call members injected into or supplied by another declaration of the generated execution-context partial class, and generated parser headers may provide ordinary C# `using` directives for that generated source file. The existing generated `Parse(...)` helper keeps the default conservative policy and does not execute these generated action hooks. Dispatch is tested against the runtime indexes used for single-item alternatives, sequence elements, quantified content, same-source hooks in different alternatives, and direct-left-recursive tail views because generated helpers resolve the generated definition before parsing with the generated policy. Parser actions inside negation probes are not documented as supported by this source-generator path. Invalid C# in this path is a compile-time C# error. `UP1028` remains reserved for explicit execution-disabled runtime policies, which the current expression-backed adapters do not expose.

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

> **Important**: ParserEngine now captures and restores managed parser execution state around parser backtracking attempt boundaries when a stateful `IParserExecutionStateManager` is configured. This does not provide complete ANTLR transactional semantics. Parser lifecycle hooks participate in that managed rollback through generated C# opt-in policies. Lifecycle contexts now also expose an optional passive `ParserRuleInvocationFrame`. Rule invocation frames can carry a passive `ParserRuleInvocationDescriptor` populated from metadata already present in the parser model, such as represented rule parameters, returns, locals, throws/catch/finally metadata, and rule options. Descriptors are preparatory observation containers only: rule parameters, returns, throws/catch/finally metadata, and rule options remain metadata-only and are not executed, typed, bound, propagated, or applied. Declared locals are also not typed or bound; only the generated C# opt-in lifecycle executor allocates their captured names as missing-only `null` entries before `@init`. Generated C# execution contexts provide explicit rule-local helper methods for lifecycle hook bodies (`GetRuleLocal`, `TryGetRuleLocal`, `SetRuleLocal`, and `GetRuleLocalDescriptors`), and those helpers operate only on `context.InvocationFrame`; they do not infer C# types, instantiate arrays or value-type defaults, generate typed local members, or make locals implicit variables in actions. Locals and exception metadata preserve raw text when ingestion exposes it, but are not invented when the parser model does not expose them. Action buffering remains unsupported. External side effects outside the managed execution state are not rolled back; keep executors side-effect-free or idempotent where possible.

---

### Rule actions `@init { }` and `@after { }`

See [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md) for the two-path execution boundary.


**Standard ANTLR4**: `@init` runs before the rule body; `@after` runs after a successful rule exit.

**Utils.Parser (default runtime)**: Both are parsed and stored in `Rule.InitAction` / `Rule.AfterAction` as raw text. They are not executed by default. To act on them without the source-generator path, inspect the `Rule` model and invoke custom logic at the appropriate parse-tree traversal step using `ParseTreeCompiler<TContext, TResult>`.

**Utils.Parser (source-generator C# path)**: `@init` and `@after` are now supported as generated C# lifecycle hook methods executed through `ParseWithEmbeddedCode(...)` or an explicit-context `CreateRuntimePolicy(executionContext, basePolicy)`. `@init` fires at rule entry before any alternative is tried. `@after` fires after a successful rule result, before the result is committed. Generated lifecycle hook bodies receive `ParserRuleLifecycleContext context` and can explicitly call generated rule-local frame helpers (`GetRuleLocal`, `TryGetRuleLocal`, `SetRuleLocal`, and `GetRuleLocalDescriptors`). Before dispatching generated `@init`, the opt-in lifecycle executor allocates missing declared local names in the active invocation-frame store with `null` values and preserves any pre-seeded values. The helpers remain the only action-body access bridge; generated `Parse(...)` neither allocates locals nor executes hooks, and the generator still does not add rule parameters, returns, exception execution, typed locals, implicit local variables, or parser method signature changes. Generated policies always install a `GeneratedExecutionStateManager` that allows `ParserEngine` to capture and restore execution-context state around parser backtracking attempt boundaries, so context mutations from predicates, inline actions, or lifecycle hooks are rolled back when those parser attempts are discarded. `GeneratedRuleLifecycleExecutor` is installed only when the grammar declares `@init` or `@after` hooks; otherwise `RuleLifecycleExecutor` remains the base no-op executor. Generated `Parse(...)` remains conservative and does not execute lifecycle hooks.

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
| Labels — `x=child` and `xs+=child` | Preserved as passive metadata end-to-end. `x=child` yields `LabelKind = Assignment`; `xs+=child` yields `LabelKind = List`. Labels are stored on `RuleRef.Label` / `RuleRef.LabelName` / `RuleRef.LabelKind` and exposed at runtime on `ParserRuleCallResult.LabelName` and `ParserRuleCallResult.LabelKind`. Both the ANTLR converter path and the source-generator G4 parser path preserve and emit label metadata. Labels compose with `callee[...]` raw argument metadata. Labels targeting literals and other non-rule-reference elements are recognized and ignored with explicit diagnostic `UP1022 LabelOnNonRuleReferenceIgnored`. Generated C# opt-in code can inspect label metadata explicitly via `GetLastRuleCallResult(context)?.LabelName` and `?.LabelKind`. Labels are metadata-only: no `$x`, `$x.value`, `$xs`, implicit variables, typed label fields/properties, automatic parse-node storage, automatic binding, or automatic argument evaluation are added. Conservative `Parse(...)` remains conservative. |
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
| Rule parameters `rule[int x]` | `Rule.Parameters` as raw text (each declaration split and name extracted lexically) | No ANTLR-compatible argument passing; generated C# lifecycle hooks and inline actions can inspect parameter descriptors, read explicitly-supplied frame values, and seed parameter values for the next named child rule via `SetNextRuleParameter` (lifecycle-hook form) or `SetNextRuleParameter`/`ClearNextRuleParameters` inline-action overloads (accepting `ParserActionExecutionContext`); seeds are rollback-safe; `callee[expr]` is not evaluated; bare `$param` reads are available only through an optional generated C# embedded-code transformer for current-rule parameters; argument passing is still explicit and call arguments are not evaluated automatically. |
| Rule returns `returns [int x]` | `Rule.ReturnType` as raw text and `UP1007 RuleReturnsIgnored` | Recognized and preserved for passive descriptors (each declaration split and name extracted lexically); reported with `UP1007 RuleReturnsIgnored`; generated C# lifecycle hooks may explicitly read/write untyped return entries on the active frame; the parent rule can observe the most recent completed child call result via `GetLastRuleCallResult`/`TryGetLastRuleCallReturn` helpers; returns are not typed, auto-allocated, exposed as implicit variables, propagated automatically, or accessible via `$rule.value`. Call results are rollback-safe. |
| `locals [...]` | `Rule.Locals` as raw text and `UP1008 RuleLocalsIgnored` | Recognized and preserved for passive descriptors; generated C# lifecycle hooks may explicitly read/write the active frame store through helper methods, but locals are still not automatically executed, typed, allocated, or exposed as implicit variables. |
| `throws ExceptionType` | `Rule.ExceptionMetadata.Throws` as raw text and `UP1023 RuleExceptionMetadataIgnored` | Recognized and preserved for passive descriptors; does not change parser exception behavior. |
| `catch [...] {...}` / `finally {...}` | `Rule.ExceptionMetadata.CatchClauses` / `Rule.ExceptionMetadata.FinallyAction` as raw text and `UP1023 RuleExceptionMetadataIgnored` | Recognized and preserved for passive descriptors; catch/finally blocks are not executed or bound to runtime exception handling. |
| Grammar-level actions `@header`, `@members`, `@footer`, etc. | `ParserDefinition.Actions` | Metadata only in the default runtime and runtime-inline path. In the source-generator C# path only, parser `@header` / `@parser::header` are injected near the top of the generated file, parser `@members` / `@parser::members` are injected into `{ClassName}ExecutionContext`, and parser `@footer` / `@parser::footer` are injected as trailing generated C# source near the end of the generated file. Lexer-scoped `@lexer::header`, `@lexer::members`, and `@lexer::footer` are injected through the same limited generated-C# compatibility model with dedicated lexer markers; this does not support lexer inline actions, lexer predicates, `$...` rewriting, or complete ANTLR lexer target-language compatibility. See [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md). |
| Rule-call argument clause `callee[...]` | `RuleRef.RawArguments` as raw text (outer brackets excluded); reported with `UP1037 RuleCallArgumentsPreservedAsMetadata`. At runtime, the raw text is also carried into `ParserRuleCallResult.RawArguments` on the parent frame's last completed child call result. | Recognized and preserved as raw metadata by default. The argument text is not evaluated or parsed as C# expressions. `PendingChildSeeds` and `InvocationFrame.Parameters` are not populated unless an explicitly installed policy requests managed seeds; the concrete positional literal policy described below is the only built-in binder. Generated `Parse(...)` is unchanged. Generated C# opt-in code can inspect the raw argument text explicitly via `GetLastRuleCallResult(context)?.RawArguments` or `TryGetLastRuleCallRawArguments(context, ruleName, out rawArgs)` in parent lifecycle/action hooks. A `SetNextRuleParameterFromRawArguments(context, ruleName, parameterName, rawArguments, map)` helper is available for explicit user-controlled mapping into a future child seed; requires a caller-supplied delegate; does not evaluate arguments automatically. `SplitRawArgumentsTopLevel(rawArguments)` and `TrySplitLastRuleCallRawArguments(context, ruleName, out args)` split raw text into top-level argument slices (respects nested parentheses/brackets/braces and quoted strings; no evaluation). `SetNextRuleParametersFromRawArguments(context, ruleName, rawArgs, mappings)` maps multiple positional slices to named seeds in one call using `ParserRawArgumentParameterMapping` entries; validates all indices before applying any seed; last mapping wins for duplicate names. `ParserRawNamedArgumentSplitter.SplitNamedTopLevel` and `TrySplitLastRuleCallNamedRawArguments` split raw text into named dictionaries (`value: 42` / `value = 42` forms); missing separator throws/returns false. `SetNextRuleParametersFromNamedRawArguments(context, ruleName, named, mappings)` maps named entries to seeds via `ParserRawNamedArgumentParameterMapping`; missing argument name returns false with no partial seed. Call-site metadata is rollback-safe and memoization-safe: always reflects the current call site rather than any stale snapshot. Use `SetNextRuleParameter(...)` for explicit parameter seeding. bare `$param` reads are available only through an optional generated C# embedded-code transformer for current-rule parameters; argument passing is still explicit and call arguments are not evaluated automatically. |
| Lexer rule options block `TOKEN options { ... }` | `Rule.Options` as `RuleOptions` key/value map | Recognized and reported with `UP1033 LexerRuleOptionsIgnored`; not applied to runtime lexer behavior. |
| Parser rule options block `rule options { ... }` | `Rule.Options` as `RuleOptions` key/value map | Recognized and reported with `UP1034 ParserRuleOptionsIgnored`; not applied to runtime parser behavior. |
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
| Target-language action execution engines | No target-language engine is active by default. Inline parser actions and rule lifecycle hooks (`@init`/`@after`) can execute only through an explicit runtime policy or generated C# opt-in path. Parser `@header` / `@parser::header` are injected only as generated C# source-file compatibility blocks, and parser `@footer` / `@parser::footer` are injected only as trailing generated C# source. Lexer actions, lexer predicates, `@lexer::header`, `@lexer::members`, `@lexer::footer`, and other grammar actions remain non-executable. |
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
| `UP1029` EmbeddedCodeConstructNotExecutedByGenerator | Not emitted by runtime. | Emitted as a source-generator warning for visible embedded-code constructs that are preserved or recognized but not executed by generated C# hooks or injected parser C# compatibility blocks, including lexer actions/predicates, unsupported grammar actions, `@lexer::header`, `@lexer::members`, `@lexer::footer`, parser named actions in lexer grammars, lexer-grammar unscoped `@header`, `@members`, and `@footer`, unknown parser named actions, and unknown named-action scopes. `@init`, `@after`, parser `@header` / `@parser::header`, parser `@members` / `@parser::members`, and parser `@footer` / `@parser::footer` are no longer in this list for generated parser or combined grammars. | Generator-only | This diagnostic uses deterministic most-specific wording and does not add execution or alter `ParserEngine`; parser semantic predicates, inline parser actions, rule lifecycle hooks (`@init`/`@after`), parser headers, parser members, and parser footers are promoted only in the generated C# opt-in path. |
| `UP1035` EmbeddedHeaderInjectedByGenerator | Not emitted by runtime. | Emitted as a source-generator warning when parser `@header` or `@parser::header` is injected near the top of the generated C# file. | Generator-only | This is generated C# parser-header compatibility only. Invalid C# is reported by Roslyn, and no full ANTLR target-language compatibility is implied. |
| `UP1036` EmbeddedFooterInjectedByGenerator | Not emitted by runtime. | Emitted as a source-generator warning when parser `@footer` or `@parser::footer` is injected near the end of the generated C# file after generated type declarations. | Generator-only | This is generated C# parser-footer compatibility only. Invalid C# is reported by Roslyn, no full ANTLR target-language compatibility is implied, and the footer is not a second header or documented `using` region. |
| `UP1037` RuleCallArgumentsPreservedAsMetadata | Emitted when `callee[...]` is parsed in a rule alternative (runtime converter). | Emitted when `callee[...]` is parsed in a rule alternative (generator G4 parser). | Yes | Metadata-only by default: raw text is preserved on `RuleRef.RawArguments`; only an explicitly installed positional or named literal policy can bind its limited literal subset into managed parser-rule parameter seeds. |

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

The expression-backed prepared registry consumes this shared discovery result before invoking its preparer. Unsupported constructs such as unsupported grammar actions, lexer actions/predicates, and non-inline parser actions remain non-executable, but they now carry explicit skip reasons. Rule lifecycle hooks (`@init`/`@after`) are now supported in the source-generator C# path and are no longer classified as unsupported. Parser `@header` / `@parser::header` are supported only as generated C# parser-header injection, parser `@members` / `@parser::members` are supported only as generated execution-context injection, and parser `@footer` / `@parser::footer` are supported only as trailing generated C# parser-footer injection. The C# source-generator path reports `UP1029 EmbeddedCodeConstructNotExecutedByGenerator` for embedded-code constructs that are visible in its `G4Grammar` model but not executed or injected, such as lexer actions, lexer predicates, unsupported grammar actions, `@lexer::header`, `@lexer::members`, and `@lexer::footer`; parser header injection reports `UP1035 EmbeddedHeaderInjectedByGenerator`, while parser footer injection reports `UP1036 EmbeddedFooterInjectedByGenerator`. These diagnostics are warnings only: they do not execute lexer code, do not modify `ParserEngine`, and do not change generated `Parse(...)` behavior. Invalid C# in a source-generator-supported hook or injected parser header/member/footer block remains a Roslyn compilation error rather than a custom parser diagnostic.


## Explicit rule-call policy compatibility boundary

`IParserRuleCallExecutionPolicy` provides explicit `BeforeRuleCall(...)` and `AfterRuleCall(...)` callbacks around parser rule references, including right-hand self-references in direct-left-recursive tails. The passive `ParserRuleCallExecutionContext` exposes the target rule name and descriptor, caller frame when stack tracking is enabled, current raw argument text, current label name/kind, syntactic positional/named raw splits when available, success/failure, and the annotated completed call result on successful tracked calls. `NullParserRuleCallExecutionPolicy.Instance` is the default, so this extension point changes no default parse behavior.

This is not ANTLR-compatible argument execution. `callee[...]` remains metadata-only by default: arguments are not evaluated, positionally or nominally bound, or converted into parameter seeds. `$param`, bare `$x`, `$rule.value`, typed parameter/return variables, generated parser method signatures, and label-backed parse-node storage remain unsupported; documented assignment-label `$x.value` return sugar is separate from argument binding. Generated C# callers may opt in by supplying a `ParserRuntimeFeaturePolicy` containing a custom rule-call policy to generated `CreateRuntimePolicy(executionContext, basePolicy)` or `ParseWithEmbeddedCode(input, executionContext, basePolicy)`; generated `Parse(...)` remains conservative.

Policy side effects are ordinary external side effects and are not automatically rolled back. Only state routed through an independently rollback-aware parser-state mechanism can participate in capture/restore. Successful call results are annotated after fresh or memoized child parsing and before `AfterRuleCall(...)`, so raw arguments and labels always describe the current call site rather than stale cached metadata.

### Opt-in positional literal rule-call binding

`PositionalLiteralRuleCallExecutionPolicy` is the first concrete rule-call execution policy. It is **not** installed by default: `ParserRuntimeFeaturePolicy.Default.RuleCallExecutionPolicy` remains `NullParserRuleCallExecutionPolicy.Instance`, so call arguments remain metadata-only unless a caller explicitly supplies the concrete policy. Generated `Parse(...)` remains conservative; generated hooks can use the policy only through a caller-supplied `basePolicy` passed to `ParseWithEmbeddedCode(...)`.

The policy requires exact positional arity and maps argument index to the corresponding declared parser-rule parameter descriptor name. It accepts only `null`, lowercase `true`/`false`, signed decimal `int` or `long` values without suffixes, finite invariant-culture decimal/exponent `double` values without suffixes, double-quoted strings, and single-quoted characters. The supported escape set is `\\`, `\"`, `\'`, `\n`, `\r`, `\t`, and `\0`. Declared C# parameter types are not enforced. Named or mixed binding, identifiers, member access, calls, arithmetic, casts, enums, interpolation, verbatim strings, Roslyn evaluation, and arbitrary expressions remain unsupported.

The complete call is validated and parsed before any pending child seed is written. The policy submits all validated values through one `TrySetParameterSeeds(...)` batch, and the frame-manager contract requires implementations to retain every value or none. Successful batches use one immutable pending-child seed-store replacement, overwrite existing seeds for the same target rule and parameter names, preserve unrelated seeds, and distinguish a present `null` value from an absent parameter. If the configured invocation-frame manager rejects the complete batch, ignore mode leaves the call unbound and throw mode reports a binding failure. Rollback restores pending seeds. Generated state-aware memoization includes the policy's values using deterministic hashing for `null`, `bool`, `int`, `long`, `double`, `string`, and `char`, so calls such as `child[1]` and `child[2]` cannot share a completed result created under a different bound value. Existing explicit seeds of additional scalar types are also deterministically hashed; arbitrary non-hashable objects remain accepted but force volatile state keys that conservatively bypass memoization while pending. This support applies only to parser rules; it does not add lexer arguments, `$param`, bare `$x`, `$rule.value`, return binding, or label-backed values. Documented assignment-label `$x.value` return sugar remains separate from argument binding.

### Opt-in named literal rule-call binding

`NamedLiteralRuleCallExecutionPolicy` is a separate, explicitly installed policy; it is not the default and is not automatically combined with `PositionalLiteralRuleCallExecutionPolicy`. It consumes only `ParserRuleCallExecutionContext.NamedRawArguments`, supporting `name: literal` and `name = literal` according to `ParserRawNamedArgumentSplitter`. Matching uses exact ordinal names, so argument order is irrelevant but casing must match. The named-argument set must exactly equal the declared parameter-name set: missing, extra, blank, or duplicate declared names fail the complete call. Optional/default parameters, partial binding, and mixed positional/named calls are unsupported. Duplicate raw argument names inherit the splitter's documented last-wins behavior before the policy receives the dictionary.

Every named value is parsed only by `ParserSimpleLiteralParser`, with the same limited literals and escapes as the positional policy. Declared C# parameter types remain metadata and are neither validated nor used for conversion. Identifiers, calls, member access, arithmetic, enums, casts, interpolation, verbatim strings, Roslyn, and arbitrary expressions remain unsupported. After complete validation, one atomic `TrySetParameterSeeds(...)` batch overwrites matching seeds and preserves unrelated pending seeds. The batch is rollback-aware, a present `null` remains distinguishable from absence, and generated memoization distinguishes supported bound values. `AfterRuleCall(...)` does not bind returns or labels. Generated `Parse(...)` remains conservative, and neither policy adds `$param`, bare `$x`, `$rule.value`, return/label binding, or lexer support. Documented assignment-label `$x.value` return sugar remains separate from named-argument binding.

## Explicit typed literal rule-call binding

Declared parser-rule parameter types and conservatively split top-level default text (`ParserRuleParameterDescriptor.RawDefaultValue`) remain passive metadata by default. The existing `PositionalLiteralRuleCallExecutionPolicy` and `NamedLiteralRuleCallExecutionPolicy` also remain deliberately **untyped**: for example, they continue to bind the string literal `"hello"` to a parameter declared as `int value`. Type validation is available only by explicitly installing `TypedPositionalLiteralRuleCallExecutionPolicy` or `TypedNamedLiteralRuleCallExecutionPolicy` through `ParserRuntimeFeaturePolicy.RuleCallExecutionPolicy`. The policies are not selected automatically, are not composed automatically, and generated `Parse(...)` remains conservative.

Only those typed policies consume defaults. Positional binding permits omitted trailing parameters only, and every parameter after the last explicit argument must expose a valid default. Named binding may omit parameters in any order, but every omitted declared parameter must expose a valid default; names remain ordinal and extras remain invalid. Explicit arguments always override defaults, so an invalid default is evaluated only when the parameter is omitted. Required defaults pass through `ParserSimpleLiteralParser` and then `ParserLiteralTypeConverter`; explicit and default-derived values are fully resolved before one atomic managed seed batch.

This is not general C# optional-parameter execution. Defaults cannot reference other parameters and cannot use constants, enums, identifiers, member access, calls, arithmetic, casts, `default`, `default(T)`, `nameof`, interpolation, arrays, collections, or user-defined types. Returns, locals, labels, `$param` forms, and lexer defaults remain metadata-only or unsupported. Rollback uses the existing managed pending-seed mechanism, and memoization keys use final converted parameter values, so explicit and default forms producing the same runtime state may share a result while different values or runtime types remain distinct.

The descriptor preserves the supported declaration shapes `type name` and `type name = defaultText` as `RawType`, `Name`, optional `RawDefaultValue`, and the unchanged `RawDeclaration`. The separator is the first top-level assignment outside parentheses, brackets, braces, strings, and character literals. This is a narrow lexical extraction, not general C# parsing. Typed binding accepts only the exact aliases `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`, `char`, `string`, and `object`, or the exact canonical names `System.Boolean`, `System.Byte`, `System.SByte`, `System.Int16`, `System.UInt16`, `System.Int32`, `System.UInt32`, `System.Int64`, `System.UInt64`, `System.Single`, `System.Double`, `System.Decimal`, `System.Char`, `System.String`, and `System.Object`. Surrounding whitespace and one `?` suffix are accepted. `string?` and `object?` behave like their non-annotated reference forms; reference nullability annotations are not enforced. `System.Nullable<T>` is not supported.

Conversion is deterministic and culture invariant:

- exact source/target matches are retained;
- `object` accepts every supported simple literal, including `null`;
- `null` is accepted for `string`, `object`, and nullable value types, and rejected for non-nullable value types;
- checked integral-to-integral conversion is accepted only in range;
- `int`/`long` to `float`/`double` is accepted only when conversion back preserves the integer exactly;
- exact `double` to `float` is accepted only when finite and exactly representable;
- floating-point-to-integral conversion is rejected;
- `int`/`long` to `decimal` is exact; `double` to `decimal` is rejected;
- `char` to `string` and a one-character `string` to `char` are accepted;
- strings are never parsed as numbers or Booleans, and Boolean/numeric interchange is rejected.

Unsupported declarations—including user-defined types, enums, arrays, generics, collections, tuples, delegates, records, classes, and interfaces—fail conservatively. No `Type.GetType`, assembly loading, namespace/import resolution, reflection construction, Roslyn conversion, or expression evaluation is performed. In `IgnoreCall` mode no typed seeds are written; in `Throw` mode `ParserRuleCallBindingException` identifies the rule, parameter, declared type, reason, and positional argument index when applicable.

Both typed policies validate the complete syntax, descriptor satisfiability, parameter names and types, every explicit literal, and every required default conversion before calling `TrySetParameterSeeds(...)` exactly once. Converted seeds therefore retain the existing all-or-none managed rollback behavior. Generated state-aware memoization hashes the converted runtime values, including their runtime types: for example, `byte(1)` differs from `int(1)`, and `float(1)` differs from `double(1)`. Different source text or source literal runtime types may intentionally share a memoized child result when conversion produces the same effective target value, such as `1` and `1.0` both becoming `double(1)`.

This feature does not add `$param`, bare `$x`, `$rule.value`, generated typed variables, generated parser signatures, general optional/default semantics outside the typed policies, mixed positional/named binding, return binding, label binding, lexer argument/action/predicate execution, or arbitrary C# expression semantics. Documented assignment-label `$x.value` return sugar remains separate from argument binding.

## Explicit labeled parser-rule call results

Successful parser-rule calls now capture an immutable ordinal return snapshot in `ParserRuleCallResult.Returns` after the child rule body and child `@after` lifecycle hook complete. A missing return key remains distinct from a present key whose value is `null`. Assignment references such as `x=child` retain the last successful completed result under `x`; list references such as `xs+=child` retain successful results in execution order. The store belongs to the parent invocation frame, participates in managed rollback/state hashing, and is restored across failed alternatives, optional attempts, repetition probes, and memoized execution-state restoration. Unsupported arbitrary return objects remain accepted but force conservative volatile state keys rather than relying on `object.GetHashCode()`.

Current call-site raw arguments and label metadata are re-applied after both fresh and memoized child calls before the result is bound and before `IParserRuleCallExecutionPolicy.AfterRuleCall(...)` runs. Consequently, return values may originate from a reused child computation while `LabelName` and `LabelKind` always describe the current reference. Repeated assignment labels are last-successful-result-wins; list labels append only successes. Grammar ingestion currently permits one lexical name to be used with both `=` and `+=`, so the runtime keeps assignment and list namespaces separate and each helper observes only its matching kind.

Generated C# opt-in contexts expose generic helpers `TryGetLabeledRuleCallResult`, `GetLabeledRuleCallResults`, `TryGetLabeledRuleCallReturn`, and `GetLabeledRuleCallReturns` for lifecycle and inline-action code. The list-return helper preserves call order, includes present-null values, and skips results where the requested return key is absent. These helpers perform ordinal lookup and no conversion or rule-name fallback. They do not add bare `$x`/`$xs`, typed label/return properties, automatic return-to-parameter/local binding, or lexer label/return support. Existing argument policies and conservative generated `Parse(...)` behavior are unchanged.

## Limited generated-C# current-rule return attributes

The generated-C# opt-in path supports deliberately narrow current-rule return conveniences in parser inline actions and parser `@after` blocks. Only a bare return name declared by the current parser rule is transformed. For example, `$value = 42;` rewrites to a typed generated helper call that stores `value` in the current invocation frame, and a later `$value` read in the same action rewrites to `GetRequiredRuleReturn<T>(context, "value")`. Reads before assignment preserve the required-return helper behavior and do not invent defaults.

This current-rule return feature is separate from child or labeled rule-call return access. Assignment-label child return reads such as `$c.value`/`$x.value` are supported only by the optional generated-C# transformer in inline parser actions and `@after`, where they rewrite to required labeled-return helpers. `$child.value`, `$rule.value`, `$ctx`, `$label.ctx`, typed parser contexts, public ANTLR-style rule signatures, token attributes, lexer attributes, and general ANTLR attribute expressions remain unsupported. List-label `$xs.value` reads are supported separately as generated-C# opt-in projection sugar in inline parser actions and `@after`. Explicit helper APIs such as `TryGetLabeledRuleCallReturn(...)` and `GetLabeledRuleCallReturns(...)` remain available for plain C# code. The rewrite does not add implicit variables, typed label properties, automatic propagation, or public generated parser method changes. `Parse(...)` remains conservative and does not execute embedded code.

### Embedded-code transformation architecture

Embedded parser code preservation is supported by default. `IParserEmbeddedCodeTransformer` is the explicit extension point for target-language rewrites; the no-op transformer is the default. ANTLR-style parser attribute rewriting is optional transformer behavior, not core grammar parsing or runtime behavior. Dynamic embedded-code compilation, where used, runs after optional transformation through the existing configured compiler path. Full ANTLR embedded-action compatibility is not claimed.


### Optional C# local write convenience syntax

ANTLR-style current-rule local writes are supported only by the optional C# `CSharpAntlrStyleParserEmbeddedCodeTransformer`; they are not parser-core syntax and are not applied by the default no-op transformer. In opt-in generated C# paths, simple local assignment, supported compound assignments, and statement-only increment/decrement are rewritten to typed local helper calls that use existing frame-local state. Compound assignment is getter/operator/setter based and does not reproduce C# compound-assignment narrowing conversions. Parameters, returns, assignment labels, list-label projections, token labels, lexer attributes, `ref`/`out`, semantic-predicate writes, nested assignment expressions, and increment/decrement expression values are intentionally unsupported and diagnosed by the transformer. Direct helper APIs remain the recommended style when no transformer is selected.

### Optional C# transformer current-rule return writes

The optional C# ANTLR-style transformer supports a narrow current-rule return write convenience syntax in rule `@after` code only. The default no-op transformer preserves bare `$returnName = ...` text unchanged. This is a logic-stage source rewrite only: broader execution semantics, parent visibility, rollback propagation, and inline action return-write semantics remain separate work.

Supported forms use the bare return attribute declared by the current rule, for example `$value = 42;`, compound assignments such as `$value += 1;`, and standalone increment/decrement statements. The transformer rewrites those forms to explicit typed helper calls such as `SetRequiredRuleReturn<T>(context, "value", ...)` and `GetRequiredRuleReturn<T>(context, "value")`. Direct helper APIs remain the preferred non-transformer style.

Parameters, child return access such as `$child.value`, lexer attributes, `ref`/`out`, semantic predicates, `@init`, and dotted current-rule return attributes such as `$rule.value` and `$rule.value = ...` remain unsupported. Assignment-label child return reads such as `$c.value` and list-label projections such as `$xs.value` are read-only and supported only in inline parser actions and `@after`; use bare `$returnName = ...` only for declared current-rule return attributes when opting into the C# transformer.

> Current-rule return writes: with the optional C# ANTLR-style transformer, bare declared returns such as `$value = ...`, `$value += ...`, and standalone increments are supported in `@after` and inline parser actions. The default no-op transformer preserves the syntax unchanged. Runtime writes use parser-managed invocation-frame return state, are rollback-safe, are captured into successful `ParserRuleCallResult` snapshots for explicit helper APIs, distinguish present-null from missing, and do not auto-initialize returns. Dotted writes (`$rule.value = ...`), predicates, `@init`, parameters, labels, list projections, tokens, lexer attributes, and `ref`/`out` writes remain unsupported.


Parser and lexer grammar-level named-action support is source-generator C# only. In parser or combined grammars, unscoped `@header` / `@members` / `@footer` are treated as parser compatibility blocks, and scoped `@parser::header` / `@parser::members` / `@parser::footer` are equivalent parser compatibility blocks. They emit parser header code, generated execution-context members, or deterministic trailing parser source in grammar source order, and they still produce compatibility warnings (`UP1035`, `UP1031`, or `UP1036`) because invalid C# remains a Roslyn responsibility. Scoped lexer named actions (`@lexer::header`, `@lexer::members`, `@lexer::footer`) mirror the same limited injection model in combined or lexer grammars only with dedicated lexer markers; parser-only grammars keep them unsupported because no lexer is generated; lexer members are emitted into the existing generated execution context and do not create a separate ANTLR lexer runtime type. Parser named actions in lexer grammars are invalid for this generator, and unscoped `@header`, `@members`, and `@footer` are not parser compatibility blocks in lexer grammars. Unsupported named actions, unknown lexer/parser action names such as `@lexer::custom` or `@parser::custom`, and unknown scopes such as `@tree::members` produce deterministic `UP1029` diagnostics and are not silently injected. The default/no-op transformer preserves named-action content unchanged; optional transformer behavior remains opt-in, and `$...` current-rule attribute rewriting is intentionally limited to parser actions/lifecycle code, not parser or lexer header/member/footer content. Parser and lexer members can be called from generated inline parser actions and supported `@init`/`@after` lifecycle hooks, simple lexer inline actions and simple lexer predicates are supported only in the explicit generated-C# opt-in path.


> Lexer inline actions and predicates: simple source-generator C# lexer inline actions and simple lexer predicates are supported only through the explicit opt-in generated path. `Parse(...)` remains conservative. Predicates run during lexer matching and reject only the current path; actions run after token acceptance and before accepted lexer commands are applied. Commands from rejected paths are not applied, and actions are not executed when an earlier predicate rejects that path. The tested boundary includes rule references, fragments, simple quantifiers, duplicate hook positions, `skip`, `channel(...)`, `type(...)`, `more`, and mode transitions with `mode(...)` and `pushMode(...)`/`popMode`. `AlternativeIndex` and `ElementIndex` identify source hook locations, not quantified runtime iterations. Lexer `$...` rewriting is limited to generated-C# opt-in inline action reads (`$text`, `$type`, `$channel`, `$mode`, `$line`, `$pos`) and simple `$type = ...`/`$channel = ...`/`$mode = ...` statement writes through `LexerActionExecutionResult`; lexer predicate attributes, runtime-inline lexer execution, a separate runtime lexer, complete ANTLR target-language lexer command/mode semantics beyond the explicit runtime commands and bounded generated-C# action-result writes documented here, generalized action buffering/replay, general lexer rollback, and external side-effect rollback remain unsupported.


### Limited generated-C# lexer `$...` attributes

Generated-C# opt-in lexer inline actions may use the optional C# ANTLR-style transformer for a deliberately small lexer attribute subset: `$text`, `$type`, `$channel`, `$mode`, `$line`, and `$pos` reads. `EmbeddedLexerAttributeRewriter` rewrites these forms to generated helpers on the execution context. `$text` becomes `GetRequiredLexerText(context)` over passive `LexerActionExecutionContext.Text` and exposes the accepted token/chunk text available to the accepted lexer action context; tests lock this as context-level accepted text for fragment and lexer-rule-reference actions, not a guaranteed fragment-local or subrule-local slice. Simple tokens, quantified tokens, and alternatives read the accepted selected text. Actions paired with `skip`, `type(...)`, or `channel(...)` read text before the command suppresses, retags, or hides the token. With `more`, the action reads the current accepted chunk before final accumulation, and the later token action reads its own chunk text.

`$type` becomes `GetRequiredLexerType(context)` and reads `LexerActionExecutionContext.TokenType`; `type(...)` is applied after the current action, so the action sees the passive context type. `$channel` becomes `GetRequiredLexerChannel(context)` and reads `LexerActionExecutionContext.Channel`; `channel(...)` is applied after the current action, so the action sees the passive context channel. `$mode` becomes `GetRequiredLexerMode(context)` and reads `LexerActionExecutionContext.Mode`, the mode that accepted the current token/chunk; `pushMode(...)`, `mode(...)`, and `popMode` affect subsequent tokenization after the current action. `$line` becomes `GetRequiredLexerLine(context)` and reads `LexerActionExecutionContext.Line`. `$pos` becomes `GetRequiredLexerPos(context)` and reads `LexerActionExecutionContext.Column`. Those line and position values come from `SourceSpan.Line` and `SourceSpan.Column` at the start of the accepted token or chunk; in this runtime, `$pos` is therefore a 1-based column value, not full ANTLR `charPositionInLine` compatibility. Fragment and lexer-rule-reference actions read the accepted outer token context metadata for these passive values in current tests.

The generated-C# opt-in write subset is deliberately bounded. Simple statement writes `$type = IDENTIFIER;`, `$type = "IDENTIFIER";`, `$channel = IDENTIFIER;`, `$channel = "IDENTIFIER";`, `$mode = IDENTIFIER;`, and `$mode = "IDENTIFIER";` are rewritten to `SetLexerType(result, "...")`, `SetLexerChannel(result, "...")`, or `SetLexerMode(result, "...")`. Generated hooks do not mutate `Token` directly; the helpers write only `LexerActionExecutionResult.TokenType`, `LexerActionExecutionResult.Channel`, or `LexerActionExecutionResult.Mode`. `LexerEngine` builds the accepted token, runs accepted lexer actions, applies the requested `LexerActionExecutionResult` token type, channel, and mode once, and then runs lexer commands. The ordering contract is therefore lexer action writes first and lexer commands second: `type(...)` overrides a previous `$type = ...`, `channel(...)` overrides a previous `$channel = ...`, `mode(...)` overrides a previous `$mode = ...`, `skip` remains authoritative over emission, and `more` keeps the existing chunking semantics. `$mode = ...` replaces the current mode like `mode(...)`; it does not push or pop modes. Reads such as `$type`, `$channel`, and `$mode` remain reads of the passive `LexerActionExecutionContext`; writes in the same body do not mutate the context values read later by lexer attributes.

Regression coverage stabilizes last-write-wins behavior, `$type` plus `$channel`/`$mode` in the same action, multiple actions in one accepted token, contradictory writes, fragments, lexer rule references, quantifiers, `more` intermediate and final chunks, rejected alternatives, command override, whitespace and newline variations, identifier and string values including escaped strings, deterministic diagnostics for unsupported complex forms, and direct observation of `Token.RuleName`, `Token.Channel`, and following lexer mode behavior.

Unsupported lexer attribute forms remain explicit: `$text = ...`, `$line = ...`, `$pos = ...`, compound/coalescing/increment writes such as `$mode += ...`, `$type += ...`, `$channel ??= ...`, `$mode++`, `$type++`, and `$channel++`, dotted or chained writes, expression writes, embedded assignment expressions, `ref`/`out`, unknown attributes, and lexer predicate `$...` usage are rejected deterministically by the transformer. `$index`, `$int`, `$token`, `$start`, `$stop`, `$ctx`, and `$input` are deferred. Default/no-op transformer behavior, conservative `Parse(...)`, runtime-inline lexer behavior, separate runtime lexer execution, general lexer action buffering/replay, general lexer rollback, and full ANTLR lexer compatibility remain unchanged and unsupported.


### Generated-C# explicit simple positional rule-call binding

Generated parsers can explicitly install a generated-C#-only rule-call policy for `ParseWithEmbeddedCode(...)` when generation enables simple positional rule-argument binding. When a parser rule call supplies raw positional arguments, the generated policy first requires the raw positional argument count to exactly match the declared target-rule parameter count, including zero-parameter target rules; an explicit empty argument list such as `child[]` is therefore valid only when the target declares zero parameters. This generated-C# automatic boundary is stricter than the reusable typed runtime policy: declared parameter defaults are not consumed to satisfy omitted generated-C# call-site arguments. After exact arity passes, the generated policy converts supported simple literals and submits one atomic managed seed batch to the existing invocation-frame parameter store. The conservative generated `Parse(...)` path remains unchanged and does not execute this binding path.

Supported automatic generated-C# argument forms are intentionally narrow: exact-arity simple positional literals that the typed literal binding policy can convert to the declared parameter type, including decimal integer literals for `int` parameters. Named arguments and arbitrary C# expressions remain unsupported and are rejected deterministically in the generated-C# explicit binding path before child lifecycle hooks can observe partially seeded state. Full ANTLR-compatible generated rule signatures such as `child(int value)` are still not emitted; generated hooks should continue to read parameters through frame helpers such as `GetRequiredRuleParameter<T>(context, "name")`, and the optional C# ANTLR-style transformer may rewrite `$name` to those helpers. Explicit runtime policies such as `TypedPositionalLiteralRuleCallExecutionPolicy` may still support simple typed defaults separately when callers install them directly.

The implementation uses existing parser-managed pending seeds, invocation frames, execution-state snapshots, rollback, and memoization boundaries. No target-language expression evaluator was added to `ParserEngine`.

### Generated-C# returns/labels boundary and named-action strategy

The rule-return and labeled rule-call boundary follows the existing parser named-action architecture rather than a parallel implementation path. Classification of grammar-level named actions is centralized in `EmbeddedMembersSupport`: `@members` and `@parser::members` are parser compatibility blocks injected into the generated execution context, `@header` and `@parser::header` are injected near the top of generated C# source, and `@footer` and `@parser::footer` are injected as trailing generated C# source. Unsupported parser-scoped actions such as `@parser::init` and parser named actions inside lexer grammars remain deterministic diagnostics and are not generated-source injection points.

Parser embedded code must continue to pass through `IParserEmbeddedCodeTransformer` via `TransformEmbeddedCode(...)`. The default path preserves target-language code, and generated-C# embedded-code paths remain opt-in. Metadata is not execution authority: rule-return declarations may be present in grammar metadata, and labeled rule-call storage may be present in parser-managed frame state, but metadata/storage alone does not imply automatic runtime support, ANTLR-compatible label access, public typed parser contexts, `$label.ctx`, `$ctx`, or public ANTLR-style rule methods. Conservative `Parse(...)` remains unchanged, and `ParserEngine` remains target-language-neutral.

Future simple generated-C# return assignment/access should reuse generated execution-context helpers and optional transformer rewriting. Future labeled rule-call return access should build on existing labeled result storage where available. Any `$...` syntax support must be implemented through the parser embedded-code transformer, not the runtime parser core. No full ANTLR parser context model is promised by the current generated-C# compatibility bridge.


### Explicit generated-C# labeled return helpers

Generated-C# compatibility supports labeled child return access through explicit helper calls and the narrow assignment-label sugar `$c.value` in inline parser actions and `@after`. Use `GetRequiredLabeledRuleCallReturn(context, "c", "value")`, `TryGetLabeledRuleCallReturn(context, "c", "value", out object? value)`, `TryGetLabeledRuleCallResult(context, "c", out ParserRuleCallResult? result)`, `GetLabeledRuleCallResults(context, "xs")`, and `GetLabeledRuleCallReturns(context, "xs", "value")` to inspect `c=child` and `xs+=child` results in generated parser actions or lifecycle hooks. Bare `$value` is limited to current-rule return access in supported generated-C# parser action locations; it does not imply parent access to child labels.

Assignment labels expose the successful labeled call result; list labels expose successful results in order. Required helper calls throw deterministic parser attribute access exceptions for missing labels or missing return names, while present-null values are returned as `null`. Failed alternatives are rolled back and do not leak label results; memoized child results preserve return values while rebinding to the successful current call-site label. `$child.value`, `$rule.value`, `$c.ctx`, `$ctx`, typed parser contexts, public ANTLR-style rule methods, and general ANTLR attribute compatibility remain unsupported; `$c.value`/`$x.value` are supported for assignment-label child return reads and `$xs.value` is supported for list-label child return projections in inline parser actions and `@after`. Conservative `Parse(...)` remains unchanged and `ParserEngine` remains target-language-neutral.


Generated-C# list-label return sugar is intentionally narrow: `$xs.value` is available only in inline parser actions and `@after` when `xs` is a visible parser-rule list label from `xs+=child` and every referenced child rule declares `value`. The transformer rewrites only the `$xs.value` root/projection to `GetLabeledRuleCallReturns(context, "xs", "value")`; any following C# member access, for example `.Count`, is ordinary C#. The projection is read-only, reads only successful child calls, preserves order and present-null values, and follows the same rollback semantics as the explicit helper. It is unavailable in `@init`, semantic predicates, parser/lexer members, headers, footers, and lexer actions. `$child.value`, `$rule.value`, `$xs.ctx`, `$ctx`, typed parser contexts, public ANTLR-style parser methods, label writes, token attributes, and lexer attributes remain unsupported. Conservative `Parse(...)` remains unchanged and `ParserEngine` remains target-language-neutral.
### Generated-C# parser return convenience boundary

Supported generated-C# opt-in convenience forms are deliberately narrow:

- bare `$value` reads/writes a declared return of the current rule;
- `$c.value` / `$x.value` read a declared child return through an assignment label such as `c=child`;
- `$xs.value` reads a list-label projection through `xs+=child` and returns the generated helper list.

The following remain unsupported and must produce deterministic transformer diagnostics rather than new runtime syntax: `$child.value`, `$rule.value`, `$ctx`, `$c.ctx`, `$xs.ctx`, bare `$c` / `$xs` label objects, writes to `$c.value` or `$xs.value`, label-return reads in `@init`, label-return reads in semantic predicates, token attributes such as `$t.text`, lexer attributes, typed parser contexts, public ANTLR-style parser rule methods, and general ANTLR attribute compatibility.

These forms are optional `IParserEmbeddedCodeTransformer` rewrites for generated C# only. The default/no-op transformer leaves `$...` text unchanged, conservative `Parse(...)` remains unchanged, and `ParserEngine` remains target-language-neutral. Parser-managed return and label state follows the existing rollback semantics; no rollback of external side effects is implied.

### Static diagnostics for generated-C# positional binding

When `UtilsParserEnableGeneratedRuleArgumentBinding=true`, the source generator validates certain generated-C# rule-call argument binding failures before emitting C#. Static binding diagnostics validate locally declared parser-rule targets and parser-rule targets reached through direct or transitive imports only when every imported grammar is available as a `.g4` `AdditionalFiles` item in the same project and the target rule is unique. The resolver uses declared grammar names rather than file names; local parser rules shadow imported rules; missing imports, duplicate declared grammar names, ambiguous imported rule declarations, aliased imports, lexer-only rules, and unresolved calls remain metadata-only and do not produce `APU0107`. This is a diagnostic-only extension: it does not merge grammar ASTs, make imports executable in a new way, alter runtime resolution, or change generated `Parse(...)`. The runtime remains authoritative for policies installed manually by callers and for all unsupported ANTLR4 delegate-grammar semantics.

The generator reports `APU0107` as an error for certain invalid local parser-rule call sites: arity mismatches, named or mixed arguments, unsupported simple-literal syntax, impossible conversions to allowlisted declared types, unsupported declared target types, and duplicated or unusable target parameter names. A file with these certain errors does not produce generated source, but other grammar files continue.

No C# expression evaluation is performed and the supported syntax is not expanded. For example:

```antlr
start : child[1 + 2] ;
child[int value] : A ;
```

With generated binding enabled, `child[1 + 2]` produces a generation diagnostic because the argument is not a simple literal. `Parse(...)` behavior is unchanged.
