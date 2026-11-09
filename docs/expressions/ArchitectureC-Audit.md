# Architecture C — audit before integrating the simplification policies (S5 P18)

Status: **phase 1 report** (audit, dependencies, plan). Base: `master` `d16ae332` (PR #683, Architecture B, merged).
Scope: what the rules of `ExpressionSimplifier` would have to declare and check to consume `ExpressionSimplifierOptions`, which
anomalies block that, and in which order to integrate it. Related notes: [BehaviorAnalysis.md](BehaviorAnalysis.md) (A),
[SimplificationPolicies.md](SimplificationPolicies.md) (B), [A10-ReflectionProvenance.md](A10-ReflectionProvenance.md), the S5
deep audit (`Utils/TODO-2026-11-04-s5-simplifier-deep-audit.md`).

## 1. Contract decision (2026-11-05)

Taken before the design, at the user's request:

1. **The default policy is strictly preserved.** Under `ExpressionSimplifierOptions.Default` the simplifier, and therefore
   `ExpressionComparer.Default` (whose internal simplifier uses the default), give exactly the results, user calls, exceptions and
   resource consumption of `master` before C. Verified by the S5 campaigns (§8).
2. **Category A improvements of the default go to a separate pull request** with its own compatibility entry: the
   rewrites that are correct under every policy (`F() + 0 -> F()`), and the evaluations the P18-A analysis proves safe where the
   gate refuses them (invoked lambdas, blocks with local effects).
3. **Intrinsic admissibility is separated from the historical compatibility of the default.** A rule is not admitted "only when the
   explicit policy grants a permission the default lacks": that would make the rules that are correct under every policy
   unreachable under every policy. Instead:

   - *intrinsic admissibility* of a rewrite = the permissions of the evaluations it actually drops, merges, duplicates or moves,
     under the instance's `Options`, plus the contextual conditions (B §3.3, §3.5, §3.6); a rewrite that changes no evaluation
     needs no permission;
   - *historical admission* = the ETC R1b gate (`AllowsRules`: operands inert by `ExpressionEffectAnalysis`), unchanged;
   - a node's rules run when the historical gate allows them (exactly as today); when it refuses, an **audited** rule may still run
     under its intrinsic admissibility, **except under the default policy**, which keeps the historical admission alone. That
     exception is a single switch (`ExpressionSimplifier.UsesHistoricalAdmission`): the category A pull request will remove it,
     without touching the rules. Its compatibility entry will be **C-08**: C-07 is already taken (ETC R2, replacing a runtime
     variable by anything but a parameter throws `NotSupportedException`).

   Consequence, documented as such: any explicit non-default policy leaves the historical-compatibility mode, so it also enables
   the category A rewrites of the audited rules, which require no permission and are correct under every policy. The three axes
   stay independent in what they *relax* (each lifts only its own flags of the permission table); leaving the compatibility
   mode is not a relaxation.

## 2. Where the policy can plug in

| Point | File | Today | Observation |
|---|---|---|---|
| `AllowsRules` | `ExpressionSimplifier.cs` (≈ l. 525) | `node is InvocationExpression \|\| HasInertOperands(node)` | asked once per node, before the first matching rule; a refusal skips **every** rule of the node (`ExpressionTranformer.TryTransform`, ≈ l. 1999) |
| rule plan | `ExpressionTranformer.BuildPlan` | rules = annotated methods of the runtime type, in reflection order | a derived simplifier adds its own rules to the same plan; they run behind the same gate |
| `SplitPinnedTerms` | `ExpressionSimplifier.cs` (≈ l. 558) | non-inert terms pinned in source order; reads of mutable state pinned when an observable term exists | the contextual model of B §3.3 |
| `CanonicalizesOperator` | idem (≈ l. 535) | reassociation only for an operator with no effect (excludes `decimal`) | |
| `FoldNaNOperand` / `IsEliminableFloatingOperand` | idem (≈ l. 949, 1027) | static, narrow whitelist, applied to every prepared node | static: no access to `Options` |
| `InvokeExpression` + `InvocationInliningAnalysis` | idem, `InvocationInliningAnalysis.cs` | its own analysis; not behind the gate | |
| comparer | `ExpressionComparer` + `SimplifyForComparisonInSession` | always the comparer's shared exact simplifier (default options, `JoinsRunningOperation`) | the P9/P15 memos store only that simplifier's results |

## 3. Rule matrix

76 rules (deep audit §3; locations of `master` `d16ae332`). Columns: what the rewrite does to the **evaluations of its non-constant
operands** (a constant, a default and a parameter read are not evaluations that can be observed); "Op." = whether the node's own
operator(s) can throw and whether the rewrite changes that; "Needs" = the permissions of B §3.6 the rewrite needs when an operand is
not inert; "Cat." = A (correct under every policy, no permission), B (needs permissions every policy may grant), C (needs a
relaxation only an explicit policy grants, e.g. an operator exception), — (no non-constant operand, or out of scope).

### 3.1 Zero and one identities (`ExpressionSimplifier.cs` l. 1055–1236) — integrated by C2

| Rule (overload) | Rewrite | Dropped | Merged / duplicated | Moved | Op. | Needs | Cat. |
|---|---|---|---|---|---|---|---|
| `AdditionWithZero(e, t, 0)` | `t + 0 -> T(t)` | – | – | – | `+ 0` never throws (ordinary, non-lifted; `decimal + 0m` cannot overflow) | none | A |
| `AdditionWithZero(e, 0, t)` | `0 + t -> t` | – | – | – | idem | none | A |
| `SubstractionWithZero(e, t, 0)` | `t - 0 -> t` | – | – | – | idem | none | A |
| `SubstractionWithZero(e, 0, t)` | `0 - t -> T(-t)` | – | – | – | `0 - t` and `-t` wrap alike (integers), are exact (`decimal`, IEEE) | none | A |
| `MultiplicationWithZeroOrOne` (both) | `t * 1 -> t`, `t * -1 -> -t` | – | – | – | `* ±1` never throws (`MinValue * -1` and `-MinValue` wrap alike) | none | A |
| idem | `t * 0 -> 0` | `t` | – | – | `* 0` never throws | `CanDiscard(t)` | B/C |
| `DivideWithZeroOrOne` | `t / 1 -> t`, `t / -1 -> -t` (not signed integral) | – | – | – | `/ ±1` never throws there | none | A |
| idem | integral/decimal `t / 0` **throws while simplifying** | (all) | – | – | moves an exception to simplification time | **never outside the gate** (B §2.3, op. 5; S5A-04) | – |
| `DivideWithZero` | `0 / t -> 0` (`float`/`double`) | `t` | – | – | IEEE division never throws | `CanDiscard(t)` | B/C |
| `PowerOfZeroOrOne` | `0 ^ t -> 0`, `1 ^ t -> 1` | `t` | – | – | `Math.Pow` never throws | `CanDiscard(t)` | B/C |
| `PowerByZeroOrOne` | `t ^ 0 -> 1` | `t` | – | – | idem | `CanDiscard(t)` | B/C |
| idem | `t ^ 1 -> T(t)`, `t ^ -1 -> T(1 / t)`, `t ^ -n -> 1 / T(t ^ n)` | – | – | – | IEEE, never throws | none | A |

`T(x)` is `TransformCore(x)`: a re-transformation of an already prepared operand (S5A-07), which evaluates nothing. "B/C": the
drop is allowed by the default permissions for an evaluation without effect, allocation or exception (B), and by an explicit policy
for an allocation, a nondeterministic read or a non-preserved exception category (C); `Unknown` and `SideEffects` are never dropped.

### 3.2 Negation and reassociation rewrites (l. 1245–1466) — C3

| Rule | Rewrite | Effect on evaluations | Op. | Needs | Cat. |
|---|---|---|---|---|---|
| `AdditionWithNegate` (both) | `t + -u -> t - u` | none | same exceptions (`decimal`: same overflow condition) | none | A |
| idem | `t + -t -> 0` (comparer says equal) | drops both | – | `CanDiscard` ×2, **and the comparison runs only after the permissions** (§5.1) | B/C |
| `SubstractionWithNegate`, `NegateWithSubstraction` | `t - -u -> t + u`, `-t - u -> -(t + u)`, `-(t - u) -> -t + u` | none (order kept) | identical exceptions and IEEE results | none | A |
| `SubstractionWithAddition` | `t - (u + v) -> (t - u) - v` | none (order kept) | `decimal`: different overflow conditions; IEEE: rounding (S3) | integral/IEEE: none; `decimal`: an operator exception may change → `Arithmetic` not preserved | A / C |
| `SubstractionWithSubstraction` | `t - (u - v) -> (t + v) - u` | `v` moved before `u` | idem | `CanReorder(u, v)` + context | B/C |
| `MultiplicationWithNegate`, `DivisionWithNegate` | `t * -u -> -(t * u)`, … | none | identical (division: signed-integral guard already present) | none | A |
| `Multiplication(e, t, c)` | `t * c -> c * t` | none (a constant crosses `t`) | – | none | A |
| `Multiplication(e, c, c2 * t)`, `(e, a*b, c*d)` | coefficient folding, reassociation | none; but folds `c * c2` (may throw while simplifying for integers/decimal) | reassociation changes operator exceptions (`decimal`) | `Arithmetic` not preserved for `decimal`; never a simplification-time fold of a throwing pair | C |

### 3.3 Factoring, merging and quotients — C3

| Rule | Rewrite | Effect | Needs | Cat. |
|---|---|---|---|---|
| `AdditionOfEqualsElements`, `SubstractionOfEqualsElements` | `a*x ± b*x -> (a ± b)*x`, `x - x -> 0`, … | merges occurrences of `x` (and may keep a later one), may drop | `CanRepeat` (+ `CanReorder` when the kept occurrence is not the first) and `CanDiscard` per affected evaluation, context of B §3.3; comparisons after the permissions | B/C |
| `MultiplicationOfEqualsElements` (3) | `t * t -> t ^ 2`, `t^a * t^b -> t^(a+b)`, distribution | merges | `CanRepeat`, first in place; distribution duplicates (`CanRepeat` on every new occurrence) | B/C |
| `DivisionOfDivision` (3) | `(x/y)/(z/w) -> (x*w)/(y*z)`, … | moves `w` before `y`, `z` | `CanReorder` + context; field division only (`float`/`double`) after P18b | B/C |
| `AdditionOfCos2andSin2Number` | `sin²t + cos²t -> 1` | drops both evaluations of `t` | `CanDiscard` ×2, comparison after the permissions | B/C |
| `DivisionOfCosAndSinNumber` and the four other quotient/product rules (`.INumber.cs`) | `sin t / cos t -> tan t`, … | merge two evaluations of `t` into the first | `CanRepeat(t)`, first in place, no evaluation between the two occurrences | B/C |
| `Logarithm*Simplification*Number` (4) | `log t + log u -> log(t * u)` | none (order kept) | domain (S3) | A |

### 3.4 Calls and conversions

| Rule | Rewrite | Effect | Cat. |
|---|---|---|---|
| `PowerConvertionNumber1/2`, `PowerConversionMath` | `Pow(t, u) -> t ^ u` | none | A |
| 25 `*ConversionMath` rules (`.Math.cs`) | `double.F(t) -> INumber<double>.F(t)` | none (both trusted) | A (S5A-02 first for `Round`) |
| `InvokeExpression` | inlining | own analysis (`InvocationInliningAnalysis`), outside the gate | C4 (see §6) |
| constant folds (`*OfConstants`, `PowerOfConstants`) | fold | no non-constant operand: the gate always allows them | – |

### 3.5 Outside the rules

| Mechanism | Effect | C stage |
|---|---|---|
| `FinalizeExpression` / `CanonicalizeAdditiveExpression` / `CanonicalizeMultiplicativeExpression` | regroup, reorder, aggregate constants; pins non-inert terms (`SplitPinnedTerms`) | C3: pin the terms that are not `CanReorder` under the policy, keep the §3.3 context |
| `CanonicalizesOperator` | `decimal` chains are not reassociated | C3: only when `Arithmetic` is not preserved, and never by a fold that throws while simplifying |
| `FoldNaNOperand` | `t + NaN -> NaN` drops `t` (whitelist) | C2b: `CanDiscard(t)` under the policy; the method is static today |
| derived-simplifier rules | run behind the gate | **never admitted outside the gate**: only the methods declared by `ExpressionSimplifier` itself and listed as audited are (identified by method handle, so a `new` method of a subclass is not) |

## 4. Pre-existing anomalies (verified on `d16ae332`)

| Id | Still present | Reproducer | Blocks C? | Action |
|---|---|---|---|---|
| **S5A-13** `IsSafeDivisor` accepts any non-zero `decimal` | yes (`ExpressionEffectAnalysis.cs` ≈ l. 451) | `(a / 0.5m) - (a / 0.5m)` → `0`; source throws `OverflowException` at `a = decimal.MaxValue` | **no** for the policy path: `ExpressionBehaviorRules.DivisionBehaviors` shares `IsSafeDivisor`, but only for intrinsic (method-less) divisions; a `decimal` division node carries `decimal.op_Division`, which the behavior analysis classifies as partial whatever the divisor (verified by test). It is a defect of the historical gate, which C keeps | **PR P18b (#684)**, independent |
| **S5A-03** `IsOrdinaryFieldDivision` accepts `decimal` | yes (≈ l. 3602) | `1m / (b / 3m) -> 3m / b` removes a `DivideByZeroException` at `b = 1e-28` | not for C2 (no division reassociation outside the gate); yes for C3 (`DivisionOfDivision` uses it as its own guard) | same PR P18b (#684); must be merged before C3 |
| **`Pow` omission** | yes: `Pow` is absent from the floating total names of `ExpressionMethodBehaviors` | `ExpressionBehaviorAnalysis.Analyze(x ^ 2.0)` is `UnknownCode`: an intrinsic `Power` node carries the method `Math.Pow`, classified as an unknown method | not a soundness issue (conservative), but every power makes its whole subtree `Unknown`, so C2 never drops an operand holding a power (`(x ^ 2) * 0`) until it is fixed | **PR #685**, independent; C2 is correct without it, only less effective |
| A10-B (comparer, canonical keys, memos read custom reflection objects) | yes | `ExpressionComparer.Equals(fake, real)` asymmetric | **no**, provided the invariant of §5.1 holds: outside the gate a rule checks the permissions (which classify any non-runtime reflection object as unknown code) **before** any comparison, key or memo of the operands. C2 compares nothing; C3 must keep the order and test it with armed fakes | separate PR, not a prerequisite |
| A10-C (`ExpressionOptimiser` drops `F() * 0`) | yes | `ExpressionOptimiser` | no (another class) | separate PR |
| L-1 (framework getters: `Type` of a call reads `ReturnType`) | yes | — | no: reads grant no trust | none |
| L-2 (`Type` provenance) | yes | `TypeDelegator` node types | no new exposure: C reads behaviors, not conversion types | none |
| S5A-04 (fold throws while simplifying an unevaluated branch) | yes | `i > 0 ? 1 / 0 : 2` | constrains C2: outside the gate `DivideWithZeroOrOne` must **decline** a zero divisor instead of throwing (otherwise `F() / 0` would lose `F()`'s effect and move the exception to simplification time) | rule-local guard in C2 |
| S5A-01, S5A-02 | yes | `x^y + z^2`, `double.Round(x, 2)` | no for C2 | P18a (independent) |

Opaque nodes, custom operators and conversions: `IsOrdinaryBinaryArithmetic`/`IsOrdinaryUnaryNegate` keep refusing them inside the
audited rules (the rules stay atomic on them), and the behavior analysis classifies them as unknown or declared code.

## 5. Design of the integration

### 5.1 Invariants

- **I-1 Permissions before structure.** Outside the gate, a rule decides from the permissions before it compares, hashes, keys or
  memoizes an operand. So a node holding a custom reflection object (unknown code) never reaches the A10-B consumers through C.
- **I-2 Every affected evaluation, every operation.** A rule asks the permission of each evaluation it actually drops, merges,
  duplicates or moves (B §3.6); a rewrite that changes none asks nothing.
- **I-3 No new exception, none at simplification time.** Outside the gate a rule never throws for a zero divisor or an overflowing
  fold; it declines.
- **I-4 Derived rules stay behind the gate.** Only the audited methods of `ExpressionSimplifier` run outside it.
- **I-5 Default unchanged.** Under `ExpressionSimplifierOptions.Default` no rule runs outside the gate.

### 5.2 Mechanism (C1)

- `ExpressionTransformer.TryTransform`: when `AllowsRules(node)` refuses, it asks once `AllowsAuditedRulesOutsideGate(node)` (base:
  `false`); if that also refuses, the loop stops exactly as today (same trace). Otherwise only the rules for which
  `IsAuditedRule(method)` holds are invoked, each with an ambient, thread-static "outside the gate" flag set for the duration of
  the call (saved and restored, so nested rules and comparer simplifications see their own admission); the other candidates are
  traced as rejected by the effect gate, without their signature being matched (a custom `Match` is user code, and a detailed
  trace must not run more of it than the untraced run).
- `ExpressionSimplifier`: `AllowsAuditedRulesOutsideGate` is `!UsesHistoricalAdmission` (`Options` equal to the default); the
  audited set is a static set of method handles of its own rules.
- Rule helpers: `CanDropOperand(t)` is `true` inside the gate (the gate proved `t` inert) and, outside it, `CanDiscard` of
  the behavior of `t` under `Options`, computed by the internal entry `ExpressionBehaviorAnalysis.AnalyzeWithinOperation`: it
  joins the running operation without opening a nested one nor validating `t` again; every analyzed node, rebuilt ones
  included, still consumes a structural visit and checks the stack.

### 5.3 Behavior analysis cost and caches

A drop analyzes the dropped operand once (`Analyze`: ≈ 40 ns + 12–25 ns per node, joining the running operation, counted in its
visit budget). Analyzing a subtree out of its lexical context is conservative (a parameter bound outside it is read as mutable
state, a write to it as a side effect), so no context-keyed cache is needed for correctness, and none is added in C2: a cache is
justified only by a measurement (pathological family `((F() * 0) * 0) … * 0` with an unknown `F`, quadratic in depth, bounded by
the visit budget, measured in §8). C3 (merges and moves, several analyses per chain) is where a per-operation, reference-keyed
memo inside the resource session becomes necessary; its key must then include the lexical scope (the canonicalization already
knows it) and must not outlive the session.

The comparer keeps the default simplifier (`ExpressionComparer.Default` unchanged): the P9/P15 memos only store its results, so
they need no policy key. A rule that compares under a relaxed policy uses the default comparer's equality, which is a sound
(conservative) notion of equality for every policy.

## 6. Plan

| Step | Content | Default behavior | Depends on |
|---|---|---|---|
| PR P18b (#684) | S5A-13 (`IsSafeDivisor`: a decimal divisor is safe only when its magnitude is at least 1), S5A-03 (field reassociation for `float`/`double` only) | changes (bug fix; migration note) | – |
| PR Pow (#685) | `Pow` total in the floating family of `ExpressionMethodBehaviors`, with its precondition row and the edge-value evidence | unchanged (the analysis is not consumed by the default) | – |
| PR C1+C2 | §5.2 mechanism; audited zero/one identities (§3.1) with their permissions; tests over the 32 policies; S5 campaigns identical on the default; measurements | unchanged | none (P18b and Pow independent) |
| C2b | `FoldNaNOperand` under the policy | unchanged | C1 |
| C3 | negation/reassociation family (§3.2), factoring and quotients (§3.3), canonicalization (`SplitPinnedTerms`, `CanonicalizesOperator`), per-operation behavior memo | unchanged | C1, A10-B tests |
| C4 | invocations (only if a rule needs it), comparer bound to a policy (only if needed), global validation | unchanged | C3 |
| PR C-08 | category A under the default (`UsesHistoricalAdmission` removed or narrowed), compatibility entry C-08 | changes | C2, C3 |

## 7. Validation criteria

- Default policy: the S5 campaigns (`terminate`, `converge`, `diff`, `effects`, `s3`, `canon`, 1,000 seeds, 1,000 nested) give
  byte-identical result files on `master` and on the branch (only `environment.txt` differs); the existing suites are unchanged.
- Explicit policies: for every audited rule, positive and negative cases over the 32 policies; generated trees run compiled give
  the values, exceptions, traced effects and call counts the policy still guarantees (an evaluation dropped only when its
  permission holds; none added, repeated or moved in C2); no user call during simplification beyond the default's.
- Security: custom reflection objects armed to throw are never read outside the gate; resource limits, stack guard and visit
  budget unchanged (256 KiB stack, deep trees, DAGs); derived simplifiers' own rules never run outside the gate.
- Performance: A/A-controlled campaigns (default unchanged within the A/A spread; relaxed policies measured, with the pathological
  drop family).

## 8. Results

### 8.1 Stages C1 and C2 (this pull request)

Implemented as planned in §5.2, with the audited set of §3.1; contract in [SimplificationPolicies.md](SimplificationPolicies.md) §8.

Findings made while implementing, recorded here because they correct the audit:

- **The default already reduces `F() + 0` to `F()`.** The additive canonicalization of `FinalizeExpression` pins the non-inert
  term and drops the zero constant, even though the gate refuses the rules. So `ExpressionComparer.Default` already answers
  `Equals(F() + 0, F())` = `true`; category A matters for the other identities (`F() * 1`, `F() / 1`, `0 - F()`, `F() ^ 1`, …).
- **A clock read may be dropped under every non-default policy**: B's table forbids repeating or moving a nondeterministic
  evaluation without aggressive simplification, never dropping it.
- **A throw is dropped only when its operand is analyzed**: `throw new E()` holds a constructor (unknown code) and is never
  dropped; `throw <constant>` is `MayThrowUserCode` only.
- **The impersonating reflection objects are read by the framework when a call node is rebuilt** (`ContainsGenericParameters`,
  `Attributes`, `ReturnType`, and `GetHashCode`/`Equals` of the framework's parameter cache), on the historical path too: L-1.
  The security test therefore checks that a relaxed policy reads no member the historical path does not read.

Validation:

| Suite | Result |
|---|---|
| `UtilsTest.Unit` (Release, with #684 and #685 merged) | 14,540 run, 14,539 passed, 0 failed, 1 ignored (pre-existing) |
| `UtilsTest.Security` (Release) | 342 run, 339 passed, 0 failed, 3 ignored (pre-existing, platform-gated) |
| `UtilsTest.Functional` (Release) | 383 passed |
| ETC Debug job (`UtilsTest.Mathematics.Expressions.Coverage`, Debug) | 328 run, 327 passed, 1 ignored (pre-existing) |
| S5 campaigns, default policy (`terminate`, `converge`, `diff`, `effects`, `s3`, `canon`, 1,000 seeds, 1,000 nested) | 19 result files byte-identical with `master` `d16ae332`; only `environment.txt` differs |

Performance ([SimplificationPolicies.md](SimplificationPolicies.md) §8.5, BenchmarkDotNet, `master`, #685, #686 and an A/A
copy, processes pinned to performance cores): default within −5.5 % to +1.8 % against an A/A of −4.8 % to +4.0 %, allocations
identical; #685 within −3.9 % to +1.9 %. The +1.6 % to +4.8 % of the first, unpinned campaigns came from the hybrid processor
(a process on an efficiency core runs about twice slower), not from the code. Relaxed policy: +0.25–0.55 µs and up to
400 bytes per node where an audited rule applies without shortening the tree; faster and smaller when it shortens it.

Review follow-up: the admission flag is per thread, not per instance. Tests run nested simplifications of another policy from
a rule, from an extension node's user code and from an impersonating method's members, the last ones also while an audited
rule runs with the flag set: each nested operation gets its own admission, and the flag is unchanged after it.

Second review follow-up (2026-11-09):

- **Detailed tracing matched skipped signatures.** Once a node is outside the gate, an unaudited rule was skipped before its
  `Match` untraced, but matched under detailed tracing to report its rejection: tracing ran user code the untraced run does not
  (a custom `ExpressionSignatureAttribute.Match` can count, mutate or throw). The rule is now refused before `Match` at every
  level of tracing, the rejection still reported (`DetailedTracing_MatchesNoSkippedSignature`, which fails on the previous code).
- **The dropping identities called the public analysis entry.** `Analyze(t, ResourceLimits)` opened a nested operation and
  validated `t` again for every attempted drop. The internal entry `AnalyzeWithinOperation` joins the running operation: the
  budgets are the same (the simplifier's limits are already those of the operation), the analysis visits stay counted (exact
  refusal one visit below), and a rebuilt or shared operand is still bounded by the structural budget and the stack guard
  (`InternalAnalysis_JoinsTheRunningOperation`). A rebuilt operand larger than the node limit is now refused by the structural
  budget rather than by the node limit.
- **Resource-limit tests moved to `UtilsTest.Security`**: `AuditedRuleAnalysis_UsesTheInstanceLimits` and the budget-refusal
  part of `OutsideGateFlag_IsScopedToTheAuditedRule` (now `ResourceRefusal_ClearsTheOutsideGateFlag`).

Both code changes run only outside the gate, so the default path is unchanged. Validation after this follow-up: `UtilsTest.Unit`
(Release) 14,539 run, 14,538 passed, 1 ignored; `UtilsTest.Security` (Release) 346 run, 343 passed, 3 ignored;
`UtilsTest.Functional` 383 passed; ETC Debug job 327 run, 326 passed, 1 ignored.

### 8.2 Remaining for the next stages

- C2b: `FoldNaNOperand` under the policy (static today).
- C3: PR #684 (P18b, `DivisionOfDivision` guards on `IsOrdinaryFieldDivision`) is merged (`50e595bd`); still needs invariant I-1 for every
  comparing rule, a per-operation behavior memo keyed with the lexical scope, and A10-B tests with armed fakes.
- S5A-07 (`t + 0` re-transforms its prepared operand) now also costs about 0.9 µs per `F() + 0` under a non-default policy.
- C-08: the category A improvements of the default (`UsesHistoricalAdmission`), with their own compatibility entry.
