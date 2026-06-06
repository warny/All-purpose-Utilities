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

These pieces are now runtime rollback authority for managed parser execution state at parser backtracking attempt boundaries and for memoization-hit state restoration. `ParserEngine` validates the policy state-manager contract, reads `GetCurrentStateKey()` for completed-rule memoization, stores post-rule snapshots in reusable rule results, restores those snapshots on memoization hits, captures state before ordinary alternatives, left-recursive extensions, quantifier attempts, and negation probes, restores discarded attempts, and commits only retained parser-attempt state. Rule lifecycle hooks (`@init`/`@after`) are now supported; they fire through `IParserRuleLifecycleExecutor` in `ParserRuntimeFeaturePolicy.RuleLifecycleExecutor`. Generated policies always install a `GeneratedExecutionStateManager`, so predicates, inline actions, and lifecycle hooks all share the same state-aware memoization and rollback infrastructure. `GeneratedRuleLifecycleExecutor` is installed only when the grammar declares `@init` or `@after` hooks; otherwise `RuleLifecycleExecutor` remains the base no-op executor. Action buffering, replay, lexer embedded-code state, and rollback of external side effects remain unsupported.

## Problem being solved

Parser backtracking attempt boundaries now restore both the token cursor and mutable managed embedded-code state through the configured execution-state manager. The remaining gaps are outside managed parser execution state: action buffering/replay, lexer embedded-code state, external side effects, and final top-level parse rejection after a locally successful root rule.

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

This state-aware memoization now works together with parser attempt rollback: after a discarded ordinary alternative, left-recursive extension, quantifier attempt, or negation probe is restored, `GetCurrentStateKey()` reflects the restored state before later parser work queries completed-result cache entries. Successful and failed completed rule results also store the opaque execution-state snapshot captured when the result was recorded; on a memoization hit, `ParserEngine` restores that snapshot and the end position so cached rule reuse reflects the semantic state produced by the original invocation without replaying actions. It still does not enable action buffering, replay, lifecycle actions, lexer embedded-code state, or external side-effect rollback.

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

This is now active for parser backtracking attempt boundaries: ordinary parser alternatives, left-recursive extensions, quantifier attempts, and negation probes. `ParserEngine` validates that the property is non-null and uses it to capture/restore managed parser execution state at those boundaries. Parser lifecycle hooks can participate in that managed rollback through generated C# opt-in policies, and lifecycle contexts can carry passive parser rule invocation frames. Generated execution contexts expose explicit lifecycle helper methods that can read or write the active frame-local store. In the generated C# opt-in path only, the lifecycle executor allocates missing declared local names as `null` before `@init`, without overwriting pre-seeded values. Frames still do not type or bind locals, create array or value-type defaults, expose implicit local variables, or execute rule parameters, returns, throws/catch/finally metadata, or rule options. Automatic action buffering, replay, lexer embedded-code state, and external side-effect rollback are not active.

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

This step does not by itself provide complete ANTLR transactional semantics. Lifecycle hooks participate through the generated C# opt-in path, but action buffering, replay, lexer embedded-code state, top-level final parse rejection rollback, and external side-effect rollback remain separate unsupported concerns.

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
- complete ANTLR embedded-code transactional semantics are not active because action buffering, lexer embedded-code state, replay, and external side-effect rollback remain unsupported;
- lexer embedded code remains unsupported.
