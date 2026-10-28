# AGENTS Guidelines — `Utils` project

This file applies to the `Utils` project (everything under `Utils/`). Every rule of the repository-level
[`AGENTS.md`](../AGENTS.md) applies here unchanged, **except** the local exception below. Where the two files differ,
this file takes precedence for `Utils/`, and only on the point it explicitly states.

## Local exception — node-type dispatch `switch` statements

The repository rule says that large `switch` statements (more than **10 cases** or **30 lines**) must be replaced by a
`Dictionary<case, method>` or `Dictionary<case, class>`.

In `Utils`, that rule does **not** apply to a `switch` (statement or expression) whose only purpose is to dispatch on:

- an `ExpressionType` value;
- an expression node class (`BinaryExpression`, `MethodCallExpression`, `LambdaExpression`, …), including type patterns;
- a structural category of expression nodes (for example the node families of `ExpressionTransformer`,
  `ExpressionComparer`, `ExpressionCanonicalOrder` or the simplifier memo keys).

Such a dispatch `switch` may have more than ten cases, provided that it stays a dispatch table:

```csharp
ExpressionType.Add => PrepareBinary(expression),
ExpressionType.Call => PrepareMethodCall(expression),
ExpressionType.Lambda => PrepareLambda(expression),
```

### Criteria

- Every branch is a short expression, a direct return or a call to a specialized method.
- No significant logic is written inside the `switch`; complex branches are extracted into named methods.
- The whole `switch` stays easy to read and to maintain.
- When the order of the cases matters (type patterns over a class hierarchy, a default case), the dispatch order stays
  deterministic and is documented.
- The exception does **not** cover large `switch` statements unrelated to expression node types; those still follow the
  repository rule.

A dictionary of handlers remains an acceptable design when measurements or architectural reasons justify it (for
example extensibility, or a dispatch shown to be faster), but it is not mandatory for node-type dispatch.

## Expression engine — checklist for new recursive handlers

The expression engine (`ExpressionTransformer`, `ExpressionSimplifier`, `ExpressionComparer`,
`ExpressionCanonicalOrder`, substitution) runs under the shared resource session `ExpressionResourceBudget`
(ETC R1a-Security, see `TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md` §12). Every new `Prepare*`, rebuild,
equality, hash, canonical-key or other recursive handler must:

1. **Participate in the right budget.** Transformation work goes through `TransformCore`/`TransformPrepared` (one
   visit per node, counted there). A structural walk recurses through its family dispatcher (`EqualsCore`, `Hash`,
   `ExpressionCanonicalOrder.Build`, `KeyNode.CompareTo`, `ReplaceArgumentsCore`), which calls
   `ExpressionResourceBudget.ConsumeStructuralVisit()`; a new walker that does not go through one of them calls it at its
   own recursive entry.
2. **Keep the active session.** Never start a new operation from inside the engine: internal calls use the internal
   entry points (`EqualsWithinOperation`, `TransformDetached`, `RunInternal`), never the public ones, and an
   engine-internal helper instance sets `JoinsRunningOperation`.
3. **Check the stack before recursing.** Budgeted calls already do; a helper that recurses without counting work calls
   `ExpressionResourceBudget.EnsureStack()` first. A helper that may recurse deeply before returning to its dispatcher
   needs its own check.
4. **Restore local scope in `finally`** (scope stacks, memo depth, diagnostics) and never throw from a `finally`.
5. **Propagate resource exceptions.** Never catch `ExpressionResourceLimitException` or
   `InsufficientExecutionStackException`; a `catch (InvalidOperationException)` must not wrap engine recursion (the
   resource exception derives from it), and a catch that wraps rule exceptions must exclude both.
6. **Never turn a refusal into a result**: no `false` from equality and no synthetic hash after a resource failure.
7. **Test deep and shared inputs**: a deep nesting of the new family on a 256 KiB worker thread (controlled `Stack`
   refusal), a shared DAG, and budget refusals just above the limit.
8. **Benchmark the handler** against `master` (paired runs with an A/A control).
