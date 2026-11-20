# `ExpressionOptimiser` — safety audit and contract (A10-C, O0–O5)

Branch `fix/expression-optimiser-safety`, base `master` `76bf0b55` (PRs #715, #716 and #717 merged). This document records the
audit of the public `Utils.Expressions.ExpressionOptimiser`, the defects found, the rules kept with their guards, and the
limits of the optimiser. It closes finding **A10-C** of [A10-ReflectionProvenance.md](A10-ReflectionProvenance.md) and the
correctness part of the execution-optimizer stage **O0** of `Utils/TODO-2026-09-12-expression-simplifier-roadmap.md`.

Vocabulary: **proven defect** (a reproducer fails on `76bf0b55`), **confirmed risk** (the mechanism exists, no reproducer),
**deliberate limitation**, **changed behavior** (to keep the semantics), **optimisation not made**.

## 1. Consumers and compatibility

`ExpressionOptimiser` has no consumer inside the repository: no project calls `Optimize` (verified with a search of every
project, generators and packaged-acceptance consumers included). Its consumers are external users of `omy.Utils`, documented by
`Utils/README.md`. The public surface keeps the parameterless constructor and `Optimize(Expression)`; the only additions are
`ExpressionOptimiser(ExpressionResourceLimits)` and `ResourceLimits`, the convention of `ExpressionSimplifier` and
`ExpressionComparer`. Behavioral changes visible to a consumer:

| Change | Before | After |
|---|---|---|
| Identities that were not exact | applied (`x * 0 -> 0` dropped `F()`, `x + 0.0 -> x` changed `-0.0`, …) | declined (§3) |
| Type of a rewritten node | could change (`Block(7, Empty())` became `int`) | always the source type |
| Extension and dynamic nodes | `Accept`/`VisitChildren`/`Reduce` called; dynamic reduced to a call site | passed through / kept, never called |
| Quoted expressions | rewritten inside | kept as data |
| Resource limits | none (stack overflow on deep input) | `ExpressionResourceLimits.Default`: depth 512, 50,000 logical nodes, 10,000,000 structural visits, stack guard |

A consumer optimising trusted trees deeper than 512 levels or larger than 50,000 logical nodes passes
`ExpressionResourceLimits.Unlimited` (the stack guard stays active).

## 2. O0 — architecture of the audited version (`76bf0b55`)

A private `ExpressionVisitor` (`StandardOptimiserVisitor`) with five overrides: `VisitUnary`, `VisitBinary`, `VisitConditional`,
`VisitBlock`, plus the framework defaults for every other family. Findings on the traversal itself:

- **Families.** Every framework family is visited by `ExpressionVisitor`, including quoted lambdas (rewritten as data) and
  `DynamicExpression`: a plain visitor reaches it through `VisitExtension`, whose default calls `VisitChildren`, hence
  `Reduce`, which replaces the operation by `CallSite<T>.Target(site, …)` (**proven defect D-12**).
- **Extension nodes.** `Visit` calls the node's overridable `Accept`, then `VisitChildren`, `CanReduce` and `Reduce`
  (**proven defect D-11**, ETC R4).
- **Resources.** No validation, no budget, no stack guard: a deep tree overflows the stack and kills the process
  (**confirmed risk R-1**: not reproducible in-process, the test host dies), and the framework's binding walk recurses through
  nested member bindings with a delegate per level.
- **DAGs.** A shared subtree is walked once per use (unfolded), and a rewritten one is rebuilt once per use.
- **Scopes.** Variables and labels are never renamed; the only scope hazard is the block unwrapping of D-9/D-10.
- **Exceptions.** The optimiser runs no user code except through D-11; `CreateZeroConstant` (`Convert.ChangeType`) throws
  `InvalidCastException` on an enumeration type (**proven defect D-6**).

## 3. Rule matrix

`kind` = arithmetic family of the node type: *integral* = `short`, `ushort`, `int`, `uint`, `long`, `ulong`; *floating* =
`float`, `double`; *lifted* = their `Nullable<T>` forms. Every rule now requires `Method == null` (no operator method) and
non-opaque operands (`ExpressionOpacity.IsOpaque` before any `Type` read), and returns a node of exactly the source type
(reference equality of `Type`). Constants are classified exactly in their own type by a type switch on the boxed value
(`Classify`), never through `double`.

| # | Rule | Before (`76bf0b55`) | After | Guards (all required) |
|---|---|---|---|---|
| 1 | `x + 0`, `0 + x` | any numeric type with or without method, zero `≈ 0.0` through `Convert.ToDouble` | integral and lifted integral: zero; floating (lifted or not): **`-0.0` only** | no method; constant of the node type; kept operand of the node type |
| 2 | `x - 0` | idem | integral: zero; floating: **`+0.0` only** | idem |
| 3 | `x * 1`, `1 * x` | idem, `1.0000000000000000000000000001m` counted as one | integral, lifted integral, floating | idem |
| 4 | `x / 1` | idem | integral, lifted integral, floating | idem |
| 5 | `x * 0`, `0 * x` | any numeric type, operand dropped unconditionally, `Convert.ChangeType` zero | **non-lifted integral only**, dropped operand `ExpressionEffectAnalysis.CanDiscard` | the zero constant itself is the result |
| 6 | `-(-x)` | any `Negate` over `Negate`, methods ignored | primitive unchecked negation, both levels without method | inner operand of the node type, not opaque |
| 7 | `Convert(x)` to `x.Type` | any `Convert` whose visited operand has the target type | without method only | operand of the node type, not opaque |
| 8 | `true && r`, `false \|\| r` → `r` | `bool` constant of any type | `bool` / `bool?` without method | constant of the node type, `r` of the node type |
| 9 | `false && r`, `true \|\| r` | returned `Constant(false)` / `Constant(true)` typed `bool` | returns the constant operand itself (node type); `r` not visited | constant of the node type |
| 10 | `l && true`, `l \|\| false` → `l` | `bool` constant of any type | `bool` / `bool?` without method | idem |
| 11 | `l && false`, `l \|\| true` | kept (already) | kept | — |
| 12 | `c ? a : b`, constant `c` | branch returned whatever its type, both branches visited | branch of exactly the conditional's type; discarded branch neither visited nor defining a label/loop | both branches not opaque |
| 13 | block: drop `Default(void)` | every one, the last included; rebuilt without explicit type | **not the last one**; rebuilt with `Update` (own type and variables) | — |
| 14 | block: single expression | unwrapped whatever its type | no variable, expression of the block's type, not a `LabelExpression` | not opaque |

Columns 1–10 of the mission (activation, transformation, hypotheses, evaluations, exceptions, types, custom operators, nullable,
floating values, equivalence conditions) per rule, for the version on the branch:

| # | Evaluations | Exceptions | Custom operators | Nullable | Floating special values | Why it is exact |
|---|---|---|---|---|---|---|
| 1–4 | constant dropped; `x` evaluated once, same position | none: unchecked primitive `+ - * /` by these constants never throws (`/ 1` included) | refused (`Method != null`) | lifted: `null op c = null = x` | `x + (-0.0)`, `x - 0.0`, `x * 1`, `x / 1` return `x` bit for bit (NaN payloads, infinities, subnormals) except that hardware may quiet a signaling NaN | IEEE-754 and two's complement identities |
| 5 | `x` dropped only if `CanDiscard` (no write, exception, allocation or unknown code; a read may be dropped) | `x` cannot throw; `x * 0` cannot overflow | refused | lifted refused (`null * 0 = null`) | floating refused (`NaN`, `±∞`, `-0.0`) | integral product with zero is zero |
| 6 | `x` once | primitive unchecked negation never throws; `NegateChecked` never matched | refused at both levels | lifted: `-(-null) = null` | sign bit flipped twice, NaN payload kept | involution |
| 7 | `x` once | an identity conversion without method is a no-op | refused | identity on `T?` too | no-op | — |
| 8–10 | the deciding constant is the left operand; the right one is never evaluated at run time, so dropping it changes nothing; neutral constants removed, the other operand evaluated exactly as before | none added or removed | refused | `bool?` lifted semantics kept | n/a | short-circuit truth tables |
| 12 | the test is a constant; the discarded branch never runs | none | n/a | n/a | n/a | a dropped branch with a label could break a jump into it (void conditional), so it is kept |
| 13–14 | `Default(void)` evaluates nothing | none | n/a | n/a | n/a | type and scope preserved |

Not rewritten (optimisation not made, by design): checked arithmetic, `decimal` arithmetic (its identities change the scale:
`1.5m + 0.00m = 1.50m`, `1.5m * 1.0m = 1.50m`; `x * 0m` also drops an overflow of `x`), custom operators and conversions,
`Modulo`, `Power`, comparisons, `Not`, quoted expressions (their value is the tree), extension nodes, conversion lambdas of
`Coalesce` and compound assignments (not visited, as before).

## 4. Proven defects (all reproduced on `76bf0b55`, fixed on the branch)

| Id | Defect | Minimal reproducer | Root cause | Test |
|---|---|---|---|---|
| D-1 | `x * 0` drops an observable operand (call, mutation, throw, division by zero, allocation) | `F(i) * 0` → `0`, `F` not called | no effect check | `O1_CallTimesZero_KeepsTheCall`, `O1_MutationTimesZero_*`, `O1_ThrowingOperandTimesZero_*`, `O1_AllocationTimesZero_*`, `O1_DistinctEffectsOnBothSides_*` |
| D-2 | `x * 0` wrong for lifted, floating and decimal types | `(int?)null * 0` → `0`; `NaN * 0.0` → `0`; `(m*m) * 0m` loses the overflow; `1.5m * 0.00m` scale | identity applied outside integral arithmetic | `O1_NullableTimesZero_KeepsNull`, `O1_FloatingTimesZero_*`, `O1_DecimalTimesZero_*` |
| D-3 | identities drop a custom operator method | `Add(i, 0, AddCounted)` → `i`; idem `*`, `/`, `-`, lifted | `BinaryExpression.Method` ignored | `O1_CustomOperatorTimesZero_*`, `O2_CustomOperatorIdentities_*`, `O2_LiftedCustomOperator_IsKept`, `ImpersonatingOperatorMethods_AreNeverReadNorRemoved` |
| D-4 | `x + 0.0`, `0.0 + x`, `x - (-0.0)` change `-0.0` into `+0.0` | `(-0.0) + 0.0` is `+0.0`, optimised `-0.0` | signed zero ignored | `O2_FloatingAdditiveZero_RespectsTheSignOfZero`, `Optimize_AppliesArithmeticIdentities` |
| D-5 | decimal identities through `Convert.ToDouble` | `1e28m * 1.0000000000000000000000000001m` → `1e28m`; `1.5m + 0.00m` loses the scale | inexact constant test, decimal scale | `O2_DecimalIdentities_KeepValueAndScale` |
| D-6 | `Optimize` throws on an enumeration custom operator by zero | `Multiply(level, Zero, MultiplyLevels)` → `InvalidCastException` | `Convert.ChangeType(0, enum)` | `O1_CustomEnumerationOperatorTimesZero_DoesNotThrow` |
| D-7 | `-(-x)` drops custom negation operators | `-(-money)` with `operator -` | `UnaryExpression.Method` ignored | `O3_CustomDoubleNegation_KeepsTheOperators`, impersonation test |
| D-8 | conversion through a method dropped when types are equal | `Convert(i, int, IdentityCounted)` → `i` | `Method` ignored | `O3_Conversions_KeepTheirMethod` |
| D-9 | `bool?` short circuits return a `bool` constant | `(bool?)false && nb` → `Constant(false)`: `Lambda` throws | constant built with `typeof(bool)` | `O4_NullableBooleanShortCircuits_KeepTheNullableType` |
| D-10 | type changes: constant conditional with explicit type, block ending with `Empty()`, explicitly typed blocks | `Block(Constant(7), Empty())` becomes `int` | type of the selected/last child used | `O4_ConstantConditional_KeepsTheType`, `O5_BlockEndingWithEmpty_StaysVoid`, `O5_ExplicitlyTypedBlocks_*`, `O5_BlockShapes_*` |
| D-10b | single-label block unwrapped into its parent scope | `Block(Block(Label(t)), Block(Label(t)))` → "Cannot redefine label" | unwrapping changes the label scope | `O5_ControlFlowInBlocks_IsPreserved` |
| D-11 | extension node called (`Accept`, `VisitChildren`, `Reduce`) | any `R4HostileExtension` | framework `Visit` | `HostileExtension_IsNeverCalled` |
| D-12 | dynamic operation reduced to a call-site invocation | `MakeDynamic(...)` → `InvocationExpression` | framework `VisitExtension` → `Reduce` | `DynamicOperation_IsKeptAndItsBinderNeverCalled`, `UnchangedDynamicOperation_IsReturnedAsIs` |
| D-13 | quoted lambdas rewritten | `Quote(i => i * 1)` value changed | framework visit of `Quote` | `QuotedExpression_IsNotRewritten` |
| D-14 | shared dependency: `ExpressionEffectAnalysis` read `Type` of an extension operand (conversion, `TypeAs`, field receiver) | `Convert(hostile, int)` analysed | R4 check missing before `Operand.Type` | `EffectAnalysis_NeverReadsTheTypeOfAnExtensionOperand` |

D-14 is in the shared effect analysis, also reached by the simplifier's gate. The fix returns `Observable` before the read; the
operand's own classification (`Observable`, as an unknown node) already made the union `Observable`, and every consumer only
uses `IsInert`/`CanDiscard`/`CanRepeat`/`CanReorder` or the `Observable` bit, so no classification result changes.

## 5. Confirmed risks without in-process reproducer, and deliberate limitations

- **R-1 stack overflow on `76bf0b55`** for deep inputs (unary, binary, nested bindings): kills the process, so no in-process
  test can fail on `master`; the branch tests (`HostileNesting_OfEveryFamily_IsRefusedOnASmallStack`) end every family with a
  controlled `Stack` refusal on a 256 KiB thread.
- **Signaling NaN.** `x * 1.0`, `x / 1.0`, `x + (-0.0)`, `x - 0.0` return `x` itself; the source operation may quiet a signaling
  NaN (quiet bit set). Quiet NaN payloads are kept (tested with `0x7FF8000000000123`). Signaling NaNs are outside the contract,
  as for C compilers without `-fsignaling-nans`.
- **Framework reads of hostile reflection objects on rebuild.** When a child of a call, member, binding or initializer changes,
  the framework factory (`Update`) revalidates the parent and reads its reflection object (as for every engine rebuild,
  documented in `ExpressionOpacity`). The optimiser itself never reads one: operator methods are only compared with `null`, and an
  unchanged node is never rebuilt (`ImpersonatingMember_InAnUnchangedInitialization_IsNeverRead`).
- **Implicitly typed conditional chains.** `ConditionalExpression.Type` delegates to `IfTrue.Type`; a chain nested through
  `IfTrue` costs O(depth) per read in the framework (its construction is already quadratic). The optimiser reads it once per
  conditional with a constant test, after the opacity check of both branches.
- **DAGs** are walked per logical node, like every engine walk: bounded by `MaxNodes` (validated without unfolding) and by
  the structural budget, which also counts the effect analysis (`SharedDag_IsBoundedByTheLimits`,
  `SharedProductsByZero_AreBoundedByTheBudget`). Memoising rewrites by reference would make DAGs linear but costs a table on
  every call; not done.
- **No fixed point.** One bottom-up pass: rewrites whose result enables a rule higher up are applied (`(x * 1 + 0) * (x - 0)`
  → `x * x`); a rule enabled only by a later sibling is not revisited.

### Guard mutation check

Each guard was disabled in turn and the optimiser tests rerun (Debug): the label, discardability, operator-method, signed-zero,
operand-opacity, lifted-product, quote, conditional-type, label-unwrap and dynamic-rebuild guards each make 1 to 5 tests fail.
The inner-negation method guard survived the first run (no test had a primitive outer negation over a custom inner one); the
case was added. The "keep the last block expression" guard is an equivalent mutant: dropping a final `Default(void)` and
rebuilding through `BlockExpression.Update`, which keeps the block's type, gives the same type and behavior; the guard is kept
for structural fidelity.

## 6. Architecture

**A — keep the `ExpressionVisitor`, add guards (retained).** `Visit` counts one structural visit (stack check included) and
returns an extension node before `Accept`; `VisitExtension` rebuilds a dynamic operation from its arguments; `VisitUnary` keeps
quotes; `VisitMemberInit`/`VisitListInit` walk bindings and initializers with the optimiser's own loop (one visit per binding
and initializer, original `AddMethod`), so the framework's delegate-per-level binding recursion never runs; `VisitCatchBlock` and
`VisitSwitchCase` count their container. `Optimize` runs inside `ExpressionResourceBudget.Run` (input validation, budgets,
`Stack` translation at the outermost boundary, `finally` restoration, nested calls join and only tighten). Rule admission is
centralised in `TryGetArithmeticKind`, `Classify`, `KeepIfNeutral` and the `Method is null` / `IsOpaque` / exact-type checks;
discardability reuses `ExpressionEffectAnalysis`. About 600 lines, no new public concept.

**B — rebuild on `ExpressionTransformer` (not retained).** Benefits: budgets, validation, rebuilds, scopes and opacity
conventions already tested. Costs measured or established during the audit: every node goes through `TransformCore`
(preparation, plan dispatch, `CopyExpression` finalisation), which costs about 1–4 µs per visit against 50–250 ns per node for
the visitor (§8); the transformer's rules are dispatched by signature, which would make the short-circuit, conditional and block
rules (that must not visit a discarded child) awkward; and the transformer copies node families the optimiser leaves untouched,
changing rebuild counts. A gives every invariant with the existing session, so B brings no correctness benefit.

## 7. Comparison with `ExpressionSimplifier`

The simplifier is bound by a *symbolic-equivalence* contract (floating NaN, infinities and signed zeros may differ, decimal
scale is not kept) plus ETC R1b/C-06 for evaluations; the optimiser by an *execution* contract (bit-exact values). Differences
below that follow from the contracts are not anomalies.

| Transformation | ExpressionOptimiser (branch) | ExpressionSimplifier (`76bf0b55`) | Guard equivalence |
|---|---|---|---|
| Drop of an evaluation (`x * 0`) | `CanDiscard`, non-lifted integral only | `AllowsRules` gate (`HasInertOperands`) / `CanDropOperand` per policy; any ordinary type | equivalent on evaluations; the simplifier also folds floating/decimal by its symbolic contract |
| `x + 0`, `x - 0`, `x * 1`, `x / 1` | no method, exact sign of zero, no decimal | `IsOrdinaryBinaryArithmetic` (runtime-provenance method check), any sign of zero, decimal included | equivalent for operators; IEEE/scale latitude by contract |
| Custom operators | refused (`Method != null`, never read) | refused (`IsOrdinaryBinaryArithmetic`, provenance first) | equivalent |
| Hostile reflection | never read | provenance check before any read (A-10) | equivalent |
| Lifted nullable arithmetic | identities kept (exact), `* 0` refused | refused (`!IsLifted`) | optimiser more permissive, exact |
| Exceptions of the operator | checked and decimal arithmetic never rewritten | own-operator exceptions kept (`DivideWithZeroOrOne`, S5A-04 deferred folds) | equivalent |
| Double negation | `-(-x)` removed for primitive unchecked negation | not simplified (S5A-10, open) | n/a: missing rule, not a missing guard |
| Conversions | identity conversion without method removed | no conversion rule | n/a |
| Booleans, conditionals, blocks | rules 8–14 | no rule (generic rebuild keeps types) | n/a |
| Resource budgets | structural budget + validation + stack guard | transformation + structural budgets + validation + stack guard | equivalent infrastructure |

No simplifier defect was found by this comparison; nothing was changed in `ExpressionSimplifier`.

## 8. Performance

Harness `OptimiserBench` (console, Release, .NET 9, process at high priority pinned to one core, 2,000 calls per batch,
median of 25 batches, warm), built once against a detached worktree of `76bf0b55` and once against the branch, run alternately
master / branch / branch / master (A/A control: master −5 % to +3 %, branch −2 % to +12 %), plus the branch with
`ExpressionResourceLimits.Unlimited` (no shape validation) to isolate the validation cost. Windows 11, i7-12700KF.

| Workload | Input nodes / depth | master ns | branch ns | branch, no validation | branch / master | Allocated B (master → branch) |
|---|---:|---:|---:|---:|---:|---:|
| simple `x * 1 + 0` | 6 / 4 | 423 | 935 | 665 | 2.21× | 320 → 320 |
| arithmetic (identities) | 242 / 26 | 8,056 | 6,379 | 4,746 | 0.79× | 3,224 → 3,224 |
| calls (`Sin`, `Cos`) | 162 / 25 | 4,089 | 8,447 | 5,948 | 2.07× | 3,544 → 3,544 |
| effects (`F(i) * 0`, `(i + k) * 0`) | 222 / 25 | 4,726 | 7,373 | 10,112 | 1.56× | 3,200 → 1,280 |
| conversions | 103 / 43 | 3,394 | 5,877 | 4,949 | 1.73× | 2,240 → 2,240 |
| blocks (empties) | 258 / 9 | 13,246 | 15,320 | 10,197 | 1.16× | 5,960 → 5,440 |
| conditionals (constant tests) | 162 / 24 | 1,474 | 6,128 | 3,919 | 4.16× | 1,232 → 1,256 |
| deep negations (400) | 402 / 402 | 6,112 | 14,127 | 10,921 | 2.31× | 9,872 → 9,872 |
| deep calls (450) | 452 / 452 | 8,990 | 17,867 | 13,306 | 1.99× | 24 → 24 |
| shared DAG `e + e` (12 levels) | 8,192 / 14 | 40,109 | 159,876 | 117,049 | 3.99× | 24 → 24 |
| shared DAG `(e + e) * 1` (10 levels) | 4,094 / 22 | 33,521 | 99,113 | 62,733 | 2.96× | 24,848 → 24,848 |

Output shape (logical nodes / depth / distinct nodes created) is identical on master and branch for every workload except
`effects` (master 2 / 2 / 1, wrong: the 20 calls of `F` were dropped; branch 102 / 24 / 21). Structural visits on the branch,
measured as the smallest accepted `MaxStructuralVisits`: logical nodes + 1 (the lambda's parameter) on every workload, the
effect analysis of the small operands of `x * 0` being answered by its shallow path without counting.

Explanation: the added cost is per node, about 15–20 ns (`ConsumeStructuralVisit`: stack check and thread-static counter, then
the opacity type tests), plus the shape validation (about 30 % of the overhead, an unfolded walk of the input), plus a fixed
≈ 0.5 µs per call for the session. Constant conditionals also pay the label search of their discarded branch. `arithmetic`
is faster although its output is identical: the exact constant classification is a type switch on the boxed value,
where master called `Convert.ToDouble` up to four times per arithmetic node. Allocations never grow except
for the reused label finder (24 B per call with a constant conditional); unchanged blocks no longer allocate. The overhead is
the price of the controlled refusals and of R4, of the same order as the other engine walks; no optimisation of the rules was
attempted beyond removing two allocations.