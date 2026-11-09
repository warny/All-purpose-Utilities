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
9. **Respect the effect permissions** (ETC R1b, C-06; S5 P18 Architectures B and C). A simplifier rule or canonicalization
   step that drops, repeats, merges or moves an evaluation is admitted in one of two ways:
   - **Historical admission** (the default `ExpressionSimplifierOptions`, `UsesHistoricalAdmission`): only on operands
     `ExpressionEffectAnalysis` proves inert. The shared rule gate `AllowsRules` covers the operands of a node; a rule that
     removes the exception of its own operator must check it itself.
   - **Explicit policy** (any other `ExpressionSimplifierOptions`): outside the gate, only the audited rules (`IsAuditedRule`)
     run, and each one checks, for the evaluations it changes, the permission `ExpressionBehaviorPermissions` grants for the
     behavior `ExpressionBehaviorAnalysis` computes (`CanDiscard` for a drop), plus the conditions specific to its
     transformation. An evaluation whose only obstacle is an exception of a category the policy does not preserve may then
     be dropped (`NaN + (double)(10 / i)` folds to `NaN` when arithmetic exceptions are not preserved), as the policy states.
   - **Under every policy**: no side effect and no unknown code is ever dropped, repeated or moved; no exception of a
     preserved category is lost; nothing is repeated, merged or moved without a contextual proof; a derived simplifier's own
     rules keep the historical admission.
   Structural equality alone never makes two evaluations interchangeable.
10. **Respect the lexical scope model** (ETC R2): a walk that enters a binder (lambda, conversion lambda, block, catch
    handler) opens its frame before the scope and closes it in `finally` (comparer frames, hash scopes, key scopes,
    `OnEnter*Scope`/`OnExit*Scope`, substitution scope); label targets are compared by bijection and keyed by
    first-occurrence index per lambda body, never by name, and are never recreated by a rebuild; a substitution replaces
    free occurrences only and renames a capturing declaration instead of inserting under it. `CatchBlock` and
    `SwitchCase` are not expression nodes: count their walk explicitly (`ConsumeStructuralVisit`).
11. **Count the initializer containers** (ETC R3): `MemberBinding` (`MemberAssignment`, `MemberMemberBinding`,
    `MemberListBinding`) and `ElementInit` are not expression nodes either, and member bindings nest without any expression
    node in between. Every walk counts one structural visit per binding and per initializer. An engine-internal read-only
    `ExpressionVisitor` must not let the framework recurse through bindings (it builds a delegate per level, outside the stack
    guard's margin): override `VisitMemberInit`, `VisitListInit`, `VisitMemberBinding` and `VisitElementInit` with
    `ReadOnlyBindingWalk`, otherwise a deep `MemberMemberBinding` chain overflows the stack. Never rediscover an `Add` method
    from argument types: rebuild with the original `ElementInit.AddMethod`.
12. **Never inspect an extension node** (ETC R4): recognize it with `ExpressionOpacity.IsOpaque` (type tests only) *before*
    reading `NodeType` or `Type` of a node that may be one, and never call its `CanReduce`, `Reduce`, `VisitChildren`,
    `Accept`, `Equals`, `GetHashCode` or `ToString`. An engine-internal `ExpressionVisitor` checks `IsOpaque` in its `Visit`
    override before `base.Visit` (which calls the overridable `Accept`). Compare and hash opaque nodes by identity only.
    Never invoke a `CallSiteBinder`: a dynamic operation's binder is metadata, compared by reference. `ExpressionTreeR4Fixtures`
    (`R4HostileExtension`, `R4CountingBinder`) detect any such call.
13. **Keep tracing observational** (`ExpressionTransformTracing`). A new transformation entry that runs the rules without
    going through `TransformCore` opens a treatment of the active trace, as `TransformCoreWithPreparedOperands` does. Never
    call a tracer directly: report through `ExpressionTraceSession`, which delivers events only on a sufficient stack and
    never from an exception filter or handler (`TreatmentFailed` only records). Tracing never adds a transformation, a
    comparison, a structural walk, a rendering or an extension-node read, and costs at most a field read when no scope is
    open (measure it with the R4 harness: master, A/A, tracing absent).
