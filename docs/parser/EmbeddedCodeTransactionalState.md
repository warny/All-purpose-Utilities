# Embedded Code Transactional State Plan

This document records the current audit and the required implementation sequence for transactional embedded-code state.

It is intentionally a design note, not a complete ANTLR feature announcement. `ParserEngine` now captures and restores managed parser execution state around parser backtracking attempt boundaries: ordinary alternatives, left-recursive extensions, quantifier attempts, and negation probes. Rule lifecycle hooks (`@init`/`@after`) are now supported in the source-generator C# opt-in path. Generated policies install the generated execution-state manager for generated parser execution contexts, so parser predicates, inline parser actions, and lifecycle hooks share the same state-aware memoization and rollback infrastructure. The runtime still does not provide replay, action buffering, or external side-effect rollback. Completed rule-result memoization is semantic-state-aware through an opaque execution-state key, completed results carry opaque post-rule execution-state snapshots for memoization hits, and rollback restores the key before later cache lookups.

## Current state

The parser runtime already performs token-cursor backtracking. `ParserEngine` saves and restores `ParseContext.Position` around failed alternatives, recursive-extension candidates, quantifier attempts, and negation probes. It now also captures and restores the configured managed parser execution state at those parser backtracking attempt boundaries.

Generated C# embedded-code execution now has the following preparatory pieces:

- generated `{ClassName}ExecutionContext` instances own generated parser predicate/action hooks and injected parser `@members` / `@parser::members` state;
- `ParseWithEmbeddedCode(string)` creates a fresh execution context per call;
- `ParseWithEmbeddedCode(string, {ClassName}ExecutionContext)` lets callers provide a context explicitly;
- `CreateRuntimePolicy({ClassName}ExecutionContext, ParserRuntimeFeaturePolicy?)` binds generated dispatchers to a supplied context;
- `ParserExecutionContextCopier<TContext>` can shallow-copy execution contexts;
- generated execution contexts expose internal `Fork()` and `CopyFrom(...)` helpers that delegate to that copier;
- `IParserExecutionStateManager` is exposed through `ParserRuntimeFeaturePolicy`;
- `ParserRuntimeFeaturePolicy.Default` uses `NullParserExecutionStateManager.Instance`;
- generated runtime policies install a generated state manager whose `Capture()` calls `Fork()` and whose `Restore(...)` calls `CopyFrom(...)`;
- `IParserExecutionStateManager.GetCurrentStateKey()` supplies the opaque semantic-state key used by completed-result memoization;
- generated state managers delegate `GetCurrentStateKey()` to the generated execution context, which hashes supported context fields through `ParserExecutionContextHasher<TContext>`.

These pieces are now runtime rollback authority for managed parser execution state at parser backtracking attempt boundaries and for memoization-hit state restoration. `ParserEngine` validates the policy state-manager contract, reads `GetCurrentStateKey()` for completed-rule memoization, stores post-rule snapshots in reusable rule results, restores those snapshots on memoization hits, captures state before ordinary alternatives, left-recursive extensions, quantifier attempts, and negation probes, restores discarded attempts, and commits only retained parser-attempt state. Rule lifecycle hooks (`@init`/`@after`) are now supported; they fire through `IParserRuleLifecycleExecutor` in `ParserRuntimeFeaturePolicy.RuleLifecycleExecutor`. Generated policies always install a `GeneratedExecutionStateManager`, so predicates, inline actions, and lifecycle hooks all share the same state-aware memoization and rollback infrastructure. `GeneratedRuleLifecycleExecutor` is installed only when the grammar declares `@init` or `@after` hooks; otherwise `RuleLifecycleExecutor` remains the base no-op executor. Action buffering, replay, general lexer embedded-code state, and rollback of external side effects remain unsupported. The limited generated-C# lexer `$type`/`$channel`/`$mode` write support is bounded action-result mutation for the accepted token and following lexer mode only, not parser-managed transactional lexer state.

## Problem being solved

Parser backtracking attempt boundaries now restore both the token cursor and mutable managed embedded-code state through the configured execution-state manager. The remaining gaps are outside managed parser execution state: action buffering/replay, general lexer embedded-code state, external side effects, and final top-level parse rejection after a locally successful root rule.

For example:

```antlr
start
    : { Count++; } A
    | B
    ;
```

For ordinary parser alternatives, if the first alternative executes the action and then fails, the token cursor and managed execution-context state are restored before the second alternative is tried. The same managed-state guarantee now applies to the parser attempt kinds listed below, while external side effects remain outside the rollback contract.

The same issue applies to:

- inline parser actions;
- semantic predicates with side effects;
- future `@init` actions;
- future `@after` actions;
- negation probes;
- failed quantifier iterations;
- left-recursive extension candidates that are tested and discarded.

## Runtime audit

### Alternatives

`TryParseScheduledAlternatives(...)` delegates each alternative attempt to `ScheduledAlternativeExecutor.Execute(...)`.

`ScheduledAlternativeExecutor.Execute(...)` now aligns ordinary parser alternative attempts with this transactional boundary:

1. capture semantic state before the attempt;
2. parse the alternative;
3. if the attempt fails, restore the pre-attempt state;
4. if the attempt succeeds, capture the post-attempt state, restore the pre-attempt state while scheduling continues, and carry the post-attempt state on the completed branch;
5. when the scheduler selects the winner, restore the winner's post-attempt state together with the winner's end token position.

### Left-recursive extensions

`TryExtendLeft(...)` tests recursive-extension candidates and keeps the best branch. It restores token position after each candidate attempt and later restores the selected branch position.

The selected branch now carries a semantic-state snapshot. Non-selected candidates do not leak managed embedded-code mutations.

### Quantifiers

`TryParseQuantifier(...)` saves the token position before each iteration. If the inner element fails or produces no progress, the token position is restored.

The semantic state follows the same rule:

- a successful retained iteration commits its state into the current context;
- a failed iteration restores the state from before that iteration;
- a non-progressive iteration restores the state from before that iteration.

### Negation

`TryParseNegation(...)` probes the inner element and always restores token position before deciding whether the negation succeeds.

The inner probe must never commit semantic state. Regardless of whether the negation succeeds or fails, embedded-code mutations made while probing the negated content must be discarded.

This is stricter than ordinary alternatives because the negated content is not part of the accepted parse tree.

### Rule entry and rule exit

`ParseRule(...)` is the natural future boundary for `@init` and `@after`, but it is not enough for branch-level transactional state on its own.

A rule invocation may occur inside an alternative, quantifier, recursive extension, or negation probe that is later discarded. Rule lifecycle actions must therefore be added only after branch-level state capture/restore exists.

## Memoization concern

`ParseRule(...)` reuses completed rule results through `ParserStateRegistry` using rule name, input position, precedence, and `IParserExecutionStateManager.GetCurrentStateKey()` as the key. The no-op manager returns `ParserExecutionStateKey.Stateless`, so stateless/default policies keep the same effective cache behavior as the former `(rule, input position, precedence)` key.

Stateful policies must obey the correction rule: if two semantic states can influence parsing differently, they must produce two different parser execution-state keys. This prevents a completed result observed under one semantic state from being reused under another state that could accept, reject, or consume differently. Equivalent states may conservatively produce different keys, but incompatible states must not share a key.

Generated execution contexts compute keys with `ParserExecutionContextHasher<TContext>`. The hasher ignores static fields and field-like event backing fields, includes auto-property backing fields as state, supports deterministic scalar values and common ordered/unordered collections, and requires explicit `IParserExecutionStateHashable` support for complex user objects. Unsupported complex objects fail explicitly rather than falling back to reference identity or `object.GetHashCode()`.

Dictionary and set hashing is deterministic and independent from native enumeration order. Dictionary entries are sorted by canonical structural representations of their keys and then values; set elements are sorted by the canonical structural representation of the whole element. Enumeration index is never used as a tie-breaker. If distinct dictionary keys or set elements produce identical canonical representations and cannot be ordered safely, hashing fails explicitly instead of falling back to insertion order, `object.GetHashCode()`, or reference identity. `OrderedDictionary` or any business collection whose order is meaningful should be modeled as an ordered sequence rather than as an unordered dictionary/set state.

This state-aware memoization now works together with parser attempt rollback: after a discarded ordinary alternative, left-recursive extension, quantifier attempt, or negation probe is restored, `GetCurrentStateKey()` reflects the restored state before later parser work queries completed-result cache entries. Successful and failed completed rule results also store the opaque execution-state snapshot captured when the result was recorded; on a memoization hit, `ParserEngine` restores that snapshot and the end position so cached rule reuse reflects the semantic state produced by the original invocation without replaying actions. It still does not enable action buffering, replay, lifecycle actions, general lexer embedded-code state, or external side-effect rollback.

## Limited lexer action-result mutation boundary

The generated-C# opt-in `$type` / `$channel` / `$mode` write support introduces only bounded action-result mutation for the accepted token and following lexer mode. It does not introduce general lexer embedded-code transactional state. Generated hooks do not mutate `Token` directly; they set `LexerActionExecutionResult.TokenType`, `LexerActionExecutionResult.Channel`, or `LexerActionExecutionResult.Mode`, and `LexerEngine` applies those requested mutations once after accepted lexer actions run and before lexer commands.

This boundary does not add action replay, action buffering, external side-effect rollback, runtime-inline lexer execution, a separate lexer runtime, general lexer state rollback, or full ANTLR lexer compatibility. Rejected lexer paths still do not execute collected actions, so writes from rejected alternatives or predicate-rejected paths do not leak. Accepted writes remain subordinate to language-neutral lexer commands: `type(...)`, `channel(...)`, `mode(...)`, `pushMode(...)`, `popMode`, `skip`, and `more` keep their existing behavior. `$mode = ...` replaces the current mode like `mode(...)`, but does not push or pop modes. `pushMode(...)` and `popMode` keep their stack semantics.

## Top-level parse rejection boundary

Managed execution-state rollback covers parser backtracking attempt boundaries. It does not imply automatic rollback of a caller-supplied execution context after a top-level parse is rejected for trailing tokens or other final validation failures. For example, a generated C# opt-in `@after` hook on the root rule can mutate the supplied execution context after the root rule succeeds locally; if the final parse result is then rejected because unconsumed trailing tokens remain, that completed root-rule mutation is preserved.

## Required runtime abstraction

`ParserEngine` must not know about generated `{ClassName}ExecutionContext` types directly.

A runtime abstraction should be introduced through `ParserRuntimeFeaturePolicy`, for example:

```csharp
public interface IParserExecutionStateManager
{
    object Capture();
    void Restore(object snapshot);

    ParserExecutionStateKey GetCurrentStateKey();
}
```

The default policy uses `NullParserExecutionStateManager.Instance`, a singleton no-op implementation, so the default parser remains conservative and behavior-compatible.

A generated policy now provides an implementation backed by the generated execution context:

```text
Capture() -> executionContext.Fork()
Restore(snapshot) -> executionContext.CopyFrom((PExecutionContext)snapshot)
```

The addition of `ParserRuntimeFeaturePolicy.ExecutionStateManager` is an API compatibility concern for callers that instantiate policies directly. Code based on `ParserRuntimeFeaturePolicy.Default with { ... }` remains compatible because the default policy already carries the no-op manager:

```csharp
var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = customEvaluator
};
```

Direct policy construction must now provide the required execution-state manager and invocation-frame manager. Use `NullParserExecutionStateManager.Instance` and `NullParserRuleInvocationFrameManager.Instance` to keep the same conservative/no-op behavior:

```csharp
var policy = new ParserRuntimeFeaturePolicy
{
    SemanticPredicateEvaluator = new DefaultSemanticPredicateEvaluator(),
    ParserActionExecutor = new DefaultParserActionExecutor(),
    ExecutionStateManager = NullParserExecutionStateManager.Instance,
    RuleInvocationFrameManager = NullParserRuleInvocationFrameManager.Instance
};
```

This is now active for parser backtracking attempt boundaries: ordinary parser alternatives, left-recursive extensions, quantifier attempts, and negation probes. `ParserEngine` validates that the property is non-null and uses it to capture/restore managed parser execution state at those boundaries. Parser lifecycle hooks can participate in that managed rollback through generated C# opt-in policies, and lifecycle contexts can carry passive parser rule invocation frames. Generated execution contexts expose explicit lifecycle helper methods that can read or write the active frame-local store. In the generated C# opt-in path only, the lifecycle executor allocates missing declared local names as `null` before `@init`, without overwriting pre-seeded values. Frames still do not type or bind locals, create array or value-type defaults, expose implicit local variables, or execute rule parameters, returns, throws/catch/finally metadata, or rule options. Automatic action buffering, replay, general lexer embedded-code state, and external side-effect rollback are not active.

The exact type names may differ, but the ownership must remain the same:

- `ParserEngine` captures and restores opaque snapshots;
- generated code owns how the context is copied;
- `ParserEngine` does not depend on source-generator-specific types.

## Required implementation sequence

### Step 1 — Execution-state manager contract

Status: implemented as contract-only infrastructure.

Implemented scope:

- `IParserExecutionStateManager` exists in `Utils.Parser.Runtime`;
- `NullParserExecutionStateManager` provides the singleton no-op default implementation;
- `ParserRuntimeFeaturePolicy.ExecutionStateManager` exposes the manager;
- `ParserRuntimeFeaturePolicy.Default` supplies the no-op manager;
- generated policies supply a generated manager backed by `Fork()` / `CopyFrom(...)`;
- `ParserEngine` validates the manager and invokes it for parser backtracking attempt boundaries;
- default parser behavior with `NullParserExecutionStateManager.Instance` remains no-op/stateless;
- generated `Parse(...)` remains conservative for embedded-code execution, while `ParseWithEmbeddedCode(...)` can now benefit from managed parser-attempt rollback when generated policies provide stateful managers.

### Step 2 — Alternative transaction transport

Status: complete.

Implemented scope:

- capture semantic state before each scheduled alternative attempt;
- restore pre-attempt state after each attempt while scheduling continues;
- carry post-attempt state on successful branch state;
- restore the selected branch semantic state when the selected branch is committed;
- keep completed-result reuse keyed by semantic execution state whenever transactional state is active;
- add tests proving that actions in failed alternatives do not leak into the winning alternative state.

This step does not by itself provide complete ANTLR transactional semantics. Lifecycle hooks participate through the generated C# opt-in path, but action buffering, replay, general lexer embedded-code state, top-level final parse rejection rollback, and external side-effect rollback remain separate unsupported concerns.

### Step 3 — Left-recursive extension transactions

Status: implemented for managed parser execution state.

The same model now applies to left-recursive extension candidates.

Expected scope:

- isolate each candidate extension;
- discard mutations from rejected candidates;
- commit only the selected extension state;
- add tests with action mutations inside rejected and retained recursive extensions.

### Step 4 — Quantifier transactions

Status: implemented for managed parser execution state.

Capture/restore now wraps quantified inner attempts.

Expected scope:

- restore semantic state when a quantified inner attempt fails;
- restore semantic state when a quantified inner attempt is rejected as non-progressive;
- retain state only for successful iterations that remain part of the parse;
- add tests for `*`, `+`, `?`, and bounded quantifier cases when applicable.

### Step 5 — Negation probe isolation

Status: implemented for managed parser execution state.

Negation probes no longer commit managed semantic state.

Expected scope:

- capture state before probing the negated inner content;
- restore that state after the probe regardless of probe success/failure;
- add tests proving actions/predicates inside negated content do not mutate the retained context.

### Step 6 — Parser rule lifecycle hooks

Status: **complete for the source-generator C# opt-in path**.

Implemented scope:

- `IParserRuleLifecycleExecutor` interface and `NullParserRuleLifecycleExecutor` singleton added;
- `ParserRuntimeFeaturePolicy.RuleLifecycleExecutor` property added;
- `ParserEngine` fires `@init` at rule entry before any alternative is tried, and `@after` after a successful rule result;
- lifecycle hook state mutations are automatically rolled back for failed alternatives, quantifier iterations, and negation probes via the generated `IParserExecutionStateManager`;
- generated `Parse(...)` remains conservative; lifecycle hooks execute only through `ParseWithEmbeddedCode(...)` or an explicit-context `CreateRuntimePolicy(executionContext, basePolicy)` result;
- lifecycle hook methods and `GeneratedRuleLifecycleExecutor` are generated only when the grammar declares `@init` or `@after` on at least one rule; `GeneratedExecutionStateManager` is always installed for all generated execution contexts;
- tests cover successful rules, failed rule attempts, alternatives, quantifiers, negation probes, memoization hits, and nested rules.

### Step 7 — Lexer embedded-code design

Lexer actions, lexer predicates, lexer members, and mode-sensitive lexer state require a separate design.

They must not be added as a side effect of parser transactional state work.

## Non-goals

This plan does not approve:

- a new parser engine;
- graph parsing;
- parallel parsing;
- continuation replay;
- shared-prefix execution;
- action replay;
- default execution of embedded code;
- lexer embedded-code execution;
- additional semantic-state-aware cache dimensions without a dedicated design.

## Current safety summary

The safe current state is:

- generated contexts can be copied through the execution-state manager;
- runtime policies can execute generated parser predicates/actions in explicit opt-in paths;
- all parser backtracking boundaries restore token position and managed parser execution state;
- left-recursive extensions, quantifier attempts, and negation probes also restore managed parser execution state for discarded attempts;
- completed-result memoization is isolated by execution-state keys and memoization hits restore stored post-rule execution-state snapshots without replaying actions;
- rule lifecycle hooks (`@init`/`@after`) are supported in the source-generator C# path, activated only through `ParseWithEmbeddedCode(...)` or an explicit `CreateRuntimePolicy(executionContext, basePolicy)` result; grammars without lifecycle hooks use the no-op executor;
- complete ANTLR embedded-code transactional semantics are not active because action buffering, general lexer embedded-code state, replay, and external side-effect rollback remain unsupported;
- general lexer embedded-code transactional state remains unsupported beyond bounded generated-C# `$type`/`$channel`/`$mode` action-result mutation for the accepted token and following lexer mode.

## Rule-call policy transaction boundary

The optional `IParserRuleCallExecutionPolicy` receives `BeforeRuleCall(...)` before a parser child rule is invoked and `AfterRuleCall(...)` after that invocation succeeds, fails, or exits exceptionally. Raw argument text and labels remain metadata, and the parser performs no automatic evaluation or binding. An explicitly installed policy can request current-target seeds through the narrow managed API; the default no-op policy does not. On success, the engine annotates the current `ParserRuleCallResult` with the current call site's raw arguments and label before the after callback, preserving the existing rollback- and memoization-safe metadata ordering.

Policy callbacks themselves are not transactional. Mutations of external objects, logging sinks, files, services, or other side effects performed by a policy are not automatically rolled back when a parser alternative fails. The positional literal policy intentionally routes seeds through the rollback-aware pending-child contract and does not expose broad mutable managers. Policies must not retain mutable caller frames across alternatives. The default `NullParserRuleCallExecutionPolicy` has no side effects and preserves current behavior.

## Positional literal seeds and memoization

The opt-in positional literal policy validates the entire call before writing and submits the complete binding set through one all-or-none frame-manager batch. The stack manager constructs one immutable merged pending-seed-store snapshot and publishes it in a single frame mutation. Custom frame managers are contractually required to retain every supplied value or none. Consequently, both validation and application are atomic, while parser attempt snapshots restore failed-alternative seeds and prevent partial or stale bindings from leaking. A successfully parsed `null` is stored as a present dictionary entry and remains distinguishable from a missing parameter.

Before a child memoization lookup, the generated execution-state manager synchronizes the active frame's pending seeds into the generated execution context. `ParserRuleParameterSeedStore` hashes rule names, parameter names, presence, runtime scalar type, and value deterministically for the policy's supported set: `null`, `bool`, `int`, `long`, `double`, `string`, and `char`. Additional scalar values historically accepted by explicit seeding, including decimal and enum values, also have deterministic representations. Arbitrary explicit objects remain accepted for compatibility, but when they do not implement `IParserExecutionStateHashable` they contribute a fresh volatile nonce on every state-key calculation. This conservatively prevents completed-result memoization while such a value is pending instead of using an unstable object hash code or aborting parsing. This ensures `child[1]` and `child[2]` receive distinct stable keys while arbitrary objects receive no unsupported memoization-safety claim. External side effects remain outside rollback guarantees.

## Named literal seed batches

The explicitly installed `NamedLiteralRuleCallExecutionPolicy` uses the same atomic pending-child state as positional binding. It first validates the target descriptor, exact ordinal parameter-name coverage, and every `ParserSimpleLiteralParser` value, then submits one complete dictionary through `TrySetParameterSeeds(...)`. No partial seed is visible when validation or a custom manager rejects the batch. Matching seeds are overwritten, unrelated seeds remain, present `null` is distinct from absence, and failed alternatives restore prior pending state. Supported named values therefore participate in the same deterministic execution-state hashing, preventing stale memoized child results across call sites such as `child[value: 1]` and `child[value: 2]`.

This guarantee does not extend to policy external side effects, arbitrary expression evaluation, returns, labels, or lexer execution. The named policy remains opt-in and separate from the positional policy; default and generated `Parse(...)` behavior is metadata-only. Both colon and equals forms inherit the named splitter's nesting, quoted-separator, and duplicate-name last-wins behavior. Optional/default parameters, partial binding, declared-type validation, and mixed syntax remain unsupported.

## Converted typed seed state

The explicitly installed typed positional and named literal policies complete every declared-type validation and conversion for explicit arguments and required simple-literal defaults before submitting exactly one pending-child seed batch. Positional omission is trailing-only; named omission may occur in any order; explicit values override defaults and unused defaults are not evaluated. A failed conversion, unsupported declaration/default, missing required value, overflow, or manager rejection cannot expose a partial typed state. The resulting values use the same managed capture/restore path as untyped pending seeds, so failed alternatives cannot leak converted values.

`ParserRuleParameterSeedStore` hashes the converted effective runtime values, not their original source spelling. Supported converted scalars (`byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`, `bool`, `char`, `string`, and `null`) have deterministic representations. Runtime type metadata remains part of the hash, so `byte(1)`, `int(1)`, and `long(1)` are distinct, as are `float(1)` and `double(1)`. Conversely, explicit and default source forms may share a memoized result when both produce the same complete converted seed state, just as integer `1` and floating literal `1.0` may share after both convert to the same effective `double(1)` seed. Present nullable `null` defaults are hashed as present null seeds. No volatile hash is required for a value produced by the typed allowlist.

This guarantee applies to managed generated execution state. It does not extend rollback to external side effects, enable lexer execution, evaluate arbitrary expressions, bind returns or labels, or add `$param`-style access.

## Labeled completed-call state

Each invocation frame owns an immutable `ParserLabeledRuleCallResultStore` for successful labeled child references executed by that frame. Generated managed execution contexts mirror that store before capture and state-key calculation, then synchronize it back to the active frame during restore. Therefore failed alternatives, failed optionals, final failed repetition probes, and failed list appends restore the earlier assignment/list snapshot. Nested child stores remain on the child frame; only the child's final immutable call result is bound into its parent.

The state hash includes assignment label names/results and list label names, lengths, and item order. Call-result hashing includes rule identity, input/depth metadata, ordinal sorted return names and runtime-typed values, raw arguments, and label metadata. Present-null differs from an absent key. Deterministic scalar and `IParserExecutionStateHashable` values remain stable; arbitrary unsupported return objects contribute a volatile nonce so unsafe memoized reuse is bypassed. Memoized child snapshots are re-annotated and rebound using the current call-site label, preventing a failed `x=child` attempt from leaking `x` when a later `y=child` reference reuses the child computation.

## Parser-managed helper state and transactional state

Parser-managed frame state remains rollback-aware. Parameters, locals, returns, pending parameter seeds, and labeled child-call results are parser-managed values when accessed through generated helper APIs or runtime frame APIs. Generated helper calls such as `GetRequiredRuleParameter<int>(context, "count")`, `SetRuleLocal(context, "total", value)`, `SetRuleReturn(context, "result", value + 1)`, `GetRequiredLabeledRuleCallReturn(context, "x", "value")`, and `GetLabeledRuleCallReturns(context, "xs", "value")` participate in the managed state model provided by the generated execution context and invocation frames.

ANTLR-style convenience forms such as `$x.value`, `$xs.value`, `$rule.value`, `$param`, and `$local` are not parser transactional primitives. They are not core parser syntax and are not rewritten by default. With `NoOpParserEmbeddedCodeTransformer`, these forms remain unchanged target-language text. An optional C# ANTLR-style transformer may rewrite documented current-rule forms such as `$param`, `$local`, and declared bare `$returnName` to generated helper calls; child/labeled return conveniences remain unsupported, and the transformer remains a compatibility layer outside parser core behavior.

## Transformer boundary and transactional state

Embedded-code transformation is a pre-emission or pre-compilation step. The default no-op transformer does not read or mutate parser transactional state. Optional transformers may rewrite target-language text, but generated helper APIs and dynamic compiler/preparer integration remain the execution boundary; transformation alone does not grant runtime authority, local writes, return writes, rollback control, or parse acceptance control.

Embedded code can still cause arbitrary target-language side effects, including external side effects. External side effects are not automatically rolled back by parser backtracking, memoization restore, or generated execution-state copying. Transformers do not change that rule. Users should prefer parser-managed APIs for rollback-aware parser state and isolate external side effects so they do not depend on speculative parser attempts.

> Current-rule return writes: with the optional C# ANTLR-style transformer, bare declared returns such as `$value = ...`, `$value += ...`, and standalone increments are supported in `@after` and inline parser actions. The default no-op transformer preserves the syntax unchanged. Runtime writes use parser-managed invocation-frame return state, are rollback-safe, are captured into successful `ParserRuleCallResult` snapshots for explicit helper APIs, distinguish present-null from missing, and do not auto-initialize returns. Dotted writes (`$rule.value = ...`), predicates, `@init`, parameters, labels, list projections, tokens, lexer attributes, and `ref`/`out` writes remain unsupported.

## Generated-C# lexer action context metadata

The generated-C# opt-in lexer action path can expose passive lexer action context values through generated helper reads. `$line` and `$pos` are optional C# transformer conveniences only for lexer inline actions: `$line` rewrites to `GetRequiredLexerLine(context)` and reads `LexerActionExecutionContext.Line`, while `$pos` rewrites to `GetRequiredLexerPos(context)` and reads `LexerActionExecutionContext.Column`. These values come from the accepted token/chunk `SourceSpan.Line` and `SourceSpan.Column`, so both identify the 1-based beginning of that accepted token/chunk. In this runtime, `$pos` follows `SourceSpan.Column` and must not be treated as full ANTLR `charPositionInLine` compatibility.

These reads do not add parser-managed transactional state or rollback authority. They are passive metadata passed to the accepted lexer action before lexer commands such as `skip`, `type(...)`, `channel(...)`, `mode(...)`, `pushMode(...)`, or `popMode` apply to later token emission or tokenization. With `more`, each accepted action reads the current accepted chunk coordinates, not a final accumulated token coordinate. Because the coordinates are read-only values copied from the accepted token/chunk span, no additional managed snapshot or restore behavior is introduced beyond the existing generated lexer action context.

The `$type` / `$channel` / `$mode` write support introduces only bounded action-result mutation for the accepted token and following lexer mode. Simple `$type = identifierOrString;`, `$channel = identifierOrString;`, and `$mode = identifierOrString;` statements in generated-C# opt-in lexer inline actions set `LexerActionExecutionResult.TokenType`, `LexerActionExecutionResult.Channel`, or `LexerActionExecutionResult.Mode`; generated hooks do not mutate `Token` directly. `LexerEngine` applies those accepted writes once before lexer commands, so commands remain authoritative. This is not general lexer transactional state: no action replay, buffering, external side-effect rollback, runtime-inline lexer execution, separate lexer runtime, or general lexer state rollback is added. Rejected lexer paths still do not execute collected actions, and lexer predicate `$...` usage remains unsupported.

## Lexer grammar-level named actions

`@lexer::header`, `@lexer::members`, and `@lexer::footer` are generated-C# source injection compatibility blocks for combined and lexer grammars only. They do not participate in parser-managed transactional execution beyond any ordinary fields they add to the existing generated execution context. `@lexer::members` is copied, hashed, and rolled back only to the same extent as other fields on that generated execution context; no separate lexer runtime state, lexer action buffering, lexer predicate execution, or complete ANTLR lexer transactional model is introduced. Parser-only grammars keep scoped `@lexer::*` actions unsupported because no lexer is generated.


### Generated-C# explicit simple positional rule-call binding

Generated parsers can explicitly install a generated-C#-only rule-call policy for `ParseWithEmbeddedCode(...)` when generation enables simple positional rule-argument binding. When a parser rule call supplies raw positional arguments, the generated policy first requires the raw positional argument count to exactly match the declared target-rule parameter count, including zero-parameter target rules; an explicit empty argument list such as `child[]` is therefore valid only when the target declares zero parameters. This generated-C# automatic boundary is stricter than the reusable typed runtime policy: declared parameter defaults are not consumed to satisfy omitted generated-C# call-site arguments. After exact arity passes, the generated policy converts supported simple literals and submits one atomic managed seed batch to the existing invocation-frame parameter store. The conservative generated `Parse(...)` path remains unchanged and does not execute this binding path.

Supported automatic generated-C# argument forms are intentionally narrow: exact-arity simple positional literals that the typed literal binding policy can convert to the declared parameter type, including decimal integer literals for `int` parameters. Named arguments and arbitrary C# expressions remain unsupported and are rejected deterministically in the generated-C# explicit binding path before child lifecycle hooks can observe partially seeded state. Full ANTLR-compatible generated rule signatures such as `child(int value)` are still not emitted; generated hooks should continue to read parameters through frame helpers such as `GetRequiredRuleParameter<T>(context, "name")`, and the optional C# ANTLR-style transformer may rewrite `$name` to those helpers. Explicit runtime policies such as `TypedPositionalLiteralRuleCallExecutionPolicy` may still support simple typed defaults separately when callers install them directly.

The implementation uses existing parser-managed pending seeds, invocation frames, execution-state snapshots, rollback, and memoization boundaries. No target-language expression evaluator was added to `ParserEngine`.

### Generated-C# returns/labels boundary and named-action strategy

The rule-return and labeled rule-call boundary follows the existing parser named-action architecture rather than a parallel implementation path. Classification of grammar-level named actions is centralized in `EmbeddedMembersSupport`: `@members` and `@parser::members` are parser compatibility blocks injected into the generated execution context, `@header` and `@parser::header` are injected near the top of generated C# source, and `@footer` and `@parser::footer` are injected as trailing generated C# source. Unsupported parser-scoped actions such as `@parser::init` and parser named actions inside lexer grammars remain deterministic diagnostics and are not generated-source injection points.

Parser embedded code must continue to pass through `IParserEmbeddedCodeTransformer` via `TransformEmbeddedCode(...)`. The default path preserves target-language code, and generated-C# embedded-code paths remain opt-in. Metadata is not execution authority: rule-return declarations may be present in grammar metadata, and labeled rule-call storage may be present in parser-managed frame state, but metadata/storage alone does not imply automatic runtime support, ANTLR-compatible label access, public typed parser contexts, `$label.ctx`, `$ctx`, or public ANTLR-style rule methods. Conservative `Parse(...)` remains unchanged, and `ParserEngine` remains target-language-neutral.

Future simple generated-C# return assignment/access should reuse generated execution-context helpers and optional transformer rewriting. Future labeled rule-call return access should build on existing labeled result storage where available. Any `$...` syntax support must be implemented through the parser embedded-code transformer, not the runtime parser core. No full ANTLR parser context model is promised by the current generated-C# compatibility bridge.


## Labeled child-return helper state

Labeled child-return helpers, and generated-C# `$c.value` sugar that rewrites to the required assignment-label helper, are transactional because they read the existing parser-managed invocation-frame state. `c=child` stores an immutable completed `ParserRuleCallResult` for the assignment label, while `xs+=child` appends immutable successful results in execution order. `GetRequiredLabeledRuleCallReturn` distinguishes a present return whose value is `null` from an absent return key, and `GetLabeledRuleCallReturns` projects only present return keys while preserving list-label call order.

Backtracking snapshots include the labeled result store, so failed alternatives do not leak assignment or list label returns into the selected alternative. Memoized child results restore their return snapshot, but the successful call site still annotates the result with the current label before binding. This preserves helper behavior without adding C#-specific semantics to `ParserEngine`; `$c.value`/`$x.value` are supported generated-C# assignment-label transformer sugar in inline parser actions and `@after`; `$xs.value`, `$child.value`, and `$rule.value` remain unsupported transformer syntax.
