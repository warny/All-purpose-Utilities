# Embedded Code Transactional State Plan

This document records the current audit and the required implementation sequence for transactional embedded-code state.

It is intentionally a design note, not a feature announcement. The runtime does not currently provide rollback, replay, action buffering, semantic-state-aware memoization, or rule lifecycle execution.

## Current state

The parser runtime already performs token-cursor backtracking. `ParserEngine` saves and restores `ParseContext.Position` around failed alternatives, recursive-extension candidates, quantifier attempts, and negation probes.

Generated C# embedded-code execution now has the following preparatory pieces:

- generated `{ClassName}ExecutionContext` instances own generated parser predicate/action hooks and injected parser `@members` / `@parser::members` state;
- `ParseWithEmbeddedCode(string)` creates a fresh execution context per call;
- `ParseWithEmbeddedCode(string, {ClassName}ExecutionContext)` lets callers provide a context explicitly;
- `CreateRuntimePolicy({ClassName}ExecutionContext, ParserRuntimeFeaturePolicy?)` binds generated dispatchers to a supplied context;
- `ParserExecutionContextCopier<TContext>` can shallow-copy execution contexts;
- generated execution contexts expose internal `Fork()` and `CopyFrom(...)` helpers that delegate to that copier.

These pieces are not yet runtime authority. `ParserEngine` does not call `Fork()`, `CopyFrom(...)`, or `ParserExecutionContextCopier<TContext>`.

## Problem being solved

Backtracking currently restores only the token cursor. It does not restore mutable embedded-code state.

For example:

```antlr
start
    : { Count++; } A
    | B
    ;
```

If the first alternative executes the action and then fails, the token cursor can be restored before the second alternative is tried. The `Count` mutation, however, remains unless execution-context state is made transactional.

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

`ScheduledAlternativeExecutor.Execute(...)` already saves and restores the token position around each alternative attempt. A transactional state model should align with this boundary:

1. capture semantic state before the attempt;
2. parse the alternative;
3. if the attempt fails, restore the pre-attempt state;
4. if the attempt succeeds, capture the post-attempt state, restore the pre-attempt state while scheduling continues, and carry the post-attempt state on the completed branch;
5. when the scheduler selects the winner, restore the winner's post-attempt state together with the winner's end token position.

### Left-recursive extensions

`TryExtendLeft(...)` tests recursive-extension candidates and keeps the best branch. It restores token position after each candidate attempt and later restores the selected branch position.

The selected branch must also carry a semantic-state snapshot. Non-selected candidates must not leak embedded-code mutations.

### Quantifiers

`TryParseQuantifier(...)` saves the token position before each iteration. If the inner element fails or produces no progress, the token position is restored.

The semantic state must follow the same rule:

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

`ParseRule(...)` currently reuses completed rule results through `ParserStateRegistry` using rule name, input position, and precedence as the key.

That key does not include semantic execution state. Once transactional embedded-code state becomes active, blindly reusing a memoized rule result can be incorrect if rule success or side effects depend on mutable context state.

Before enabling transactional embedded-code execution, one of the following must be done:

1. disable completed-result reuse when a non-null or non-default execution-state manager is active;
2. or extend the memoization model with explicit semantic-state identity.

The first option is the recommended first implementation because it is conservative and easier to audit.

## Required runtime abstraction

`ParserEngine` must not know about generated `{ClassName}ExecutionContext` types directly.

A runtime abstraction should be introduced through `ParserRuntimeFeaturePolicy`, for example:

```csharp
public interface IParserExecutionStateManager
{
    object Capture();
    void Restore(object snapshot);
}
```

The default policy should use a no-op implementation so the default parser remains conservative and behavior-compatible.

A generated policy can then provide an implementation backed by the generated execution context:

```text
Capture() -> executionContext.Fork()
Restore(snapshot) -> executionContext.CopyFrom((PExecutionContext)snapshot)
```

The exact type names may differ, but the ownership must remain the same:

- `ParserEngine` captures and restores opaque snapshots;
- generated code owns how the context is copied;
- `ParserEngine` does not depend on source-generator-specific types.

## Required implementation sequence

### Step 1 — Execution-state manager contract

Add a runtime contract for semantic-state capture/restore.

Expected scope:

- add `IParserExecutionStateManager` or equivalent;
- add a no-op default implementation;
- add it to `ParserRuntimeFeaturePolicy`;
- keep the default behavior unchanged;
- add unit tests for default policy behavior and null validation;
- do not yet change branch execution.

### Step 2 — Alternative transaction transport

Make scheduled alternative attempts carry semantic-state snapshots.

Expected scope:

- capture semantic state before each scheduled alternative attempt;
- restore pre-attempt state after each attempt while scheduling continues;
- carry post-attempt state on successful branch state;
- restore the selected branch semantic state when the selected branch is committed;
- disable or bypass completed-result reuse when transactional state is active unless a semantic-state-aware memoization design is added in the same PR;
- add tests proving that actions in failed alternatives do not leak into the winning alternative state.

### Step 3 — Left-recursive extension transactions

Apply the same model to left-recursive extension candidates.

Expected scope:

- isolate each candidate extension;
- discard mutations from rejected candidates;
- commit only the selected extension state;
- add tests with action mutations inside rejected and retained recursive extensions.

### Step 4 — Quantifier transactions

Apply capture/restore around quantified inner attempts.

Expected scope:

- restore semantic state when a quantified inner attempt fails;
- restore semantic state when a quantified inner attempt is rejected as non-progressive;
- retain state only for successful iterations that remain part of the parse;
- add tests for `*`, `+`, `?`, and bounded quantifier cases when applicable.

### Step 5 — Negation probe isolation

Ensure negation probes never commit semantic state.

Expected scope:

- capture state before probing the negated inner content;
- restore that state after the probe regardless of probe success/failure;
- add tests proving actions/predicates inside negated content do not mutate the retained context.

### Step 6 — Parser rule lifecycle hooks

Only after branch-level transactional state exists, add `@init` / `@after` execution.

Expected scope:

- introduce rule enter/exit lifecycle hooks;
- map `@init` to rule entry;
- map `@after` to rule exit according to an explicitly documented success/failure policy;
- execute lifecycle hooks only in the opt-in embedded-code paths;
- keep `Parse(...)` conservative;
- add tests for successful rules, failed rule attempts, alternatives, quantifiers, and nested rules.

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
- `@init` / `@after` before transactional state is in place;
- semantic-state-aware memoization without a dedicated design.

## Current safety summary

The safe current state is:

- generated contexts can be copied manually;
- runtime policies can execute generated parser predicates/actions in explicit opt-in paths;
- parser backtracking restores token position only;
- embedded-code state rollback is not active;
- rule lifecycle embedded code remains unsupported;
- lexer embedded code remains unsupported.
