# Simplification policies — design note (S5 P18 Architecture B)

Status: public configuration (Architecture B), **consumed by the zero and one identities since Architecture C, stages C1 and
C2** (§8). The default policy gives exactly the historical results, user calls, exceptions and resource consumption; the
canonicalization, the other rules, the comparer and the inlining do not consult the policy yet.
Base of B: `master` `f4bb85ec` (after PR #682, A-10). Architecture C: audit and plan in
[ArchitectureC-Audit.md](ArchitectureC-Audit.md).

Related notes: [BehaviorAnalysis.md](BehaviorAnalysis.md) (Architecture A: the classification and the permissions it
prepared), [A10-ReflectionProvenance.md](A10-ReflectionProvenance.md) (trust in reflection metadata).

## 1. API

```csharp
namespace Utils.Expressions;

public enum ExpressionTrustPolicy { Verified = 0, TrustDeclarations = 1 }      // was internal (P18-A)

[Flags]
public enum ExpressionExceptionPreservation
{
    None = 0, Arithmetic = 1, Runtime = 2, UserCode = 4,
    All = Arithmetic | Runtime | UserCode,
}

public sealed record ExpressionSimplifierOptions
{
    public static ExpressionSimplifierOptions Default { get; }
    public ExpressionTrustPolicy TrustPolicy { get; init; }                        // Verified
    public bool AggressiveSimplification { get; init; }                            // false
    public ExpressionExceptionPreservation ExceptionPreservation { get; init; }    // All
}

// Utils.Mathematics.Expressions
public partial class ExpressionSimplifier
{
    public ExpressionSimplifierOptions Options { get; init; }                      // ExpressionSimplifierOptions.Default
}
```

Example:

```csharp
var simplifier = new ExpressionSimplifier(ExpressionResourceLimits.Default)
{
    Options = new ExpressionSimplifierOptions
    {
        TrustPolicy = ExpressionTrustPolicy.TrustDeclarations,
        ExceptionPreservation = ExpressionExceptionPreservation.Runtime | ExpressionExceptionPreservation.UserCode,
    },
};

// Today: same result as new ExpressionSimplifier().Simplify(e).
Expression simplified = simplifier.Simplify(e);
```

Choices:

- **An `init` property, not a constructor.** `new ExpressionSimplifier(null)` compiles today (to the limits overload); a
  one-argument options constructor would make it ambiguous, a source-breaking change. The property follows the existing
  `SimplifyQuotedExpressions` pattern, works with the limits constructor and on derived simplifiers, and needs no new
  constructor in subclasses. `null` is refused (`ArgumentNullException`).
- **The options hold the three policy axes only.** Resource limits stay a constructor argument and `SimplifyQuotedExpressions`
  stays a property: both already have their own contract and are not rewrite permissions.
- **Immutable, validated record.** The `init` accessors validate (`ArgumentOutOfRangeException` for an undefined trust policy
  or a flag outside `All`), so a `with` copy is validated too. Value equality makes policies comparable (a future memo key).
- **Frozen at construction.** The simplifier keeps the reference it was given in a read-only field; there is no static,
  ambient or thread-local policy. Nested, re-entrant and concurrent calls on an instance apply the same policy; two instances
  never share one implicitly.
- **`ExpressionTrustPolicy` made public** instead of a new enum: its two values and their meaning are those of P18-A and will
  not change in C.
- **Preservation flags rather than relaxation flags**, as requested: `All` (preserve everything) is the default. Its zero value
  `None` is the least conservative one; the options default to `All`, so only an explicit choice reaches `None`.

## 2. The three axes

### 2.1 Declaration trust (`TrustPolicy`)

| | `Verified` (default) | `TrustDeclarations` |
|---|---|---|
| `[Deterministic]` declaration | treated as unknown code (`Effective` widens the flags to `UnknownCode`) | trusted for what it promises: deterministic, no ambient state, no side effect |
| Exceptions of declared code | – | still `MayThrowUserCode` |
| Allocation, argument state | – | still `Allocates` / `ReadsMutableState` when the signature allows it |
| Undeclared, unverified code | unknown | unknown |
| Aggressive simplification | not implied | not implied |

The audit of `Effective` found one non-conservative corner: it tested `policy == Verified`, so an undefined enum value reaching
the internal permissions behaved as `TrustDeclarations`. It now tests `policy != TrustDeclarations` (any other value is
verified). No current caller passes an undefined value; the public options refuse one.

### 2.2 Aggressive simplification (`AggressiveSimplification`)

Distinguish three things:

1. **No proof** about an expression (`Unknown`, side effects, declarations not trusted): no policy makes it rewritable.
2. **A deliberately non-conservative transformation** (this axis): the rewrite is correct for the value, but changes an
   identity or timing guarantee the conservative mode keeps.
3. **An incorrect transformation** (introducing an exception, evaluating at simplification time, dropping a side effect,
   breaking the IEEE/domain contract): forbidden whatever the policy.

Relaxed by the axis (on analyzed code only):

- **Allocation identity and count**: an evaluation that allocates may be dropped, merged, repeated or moved (two boxes may
  become one, an unused array may disappear).
- **Nondeterministic values without side effects**: a clock read may be merged, repeated or moved (`Now - Now` may become `0`),
  provided it raises no preserved exception category.

Kept, whatever its value: side effects; unknown code (it never becomes "verified", and nothing runs user code to find out);
the preserved exception categories (only §2.3 decides them); declarations (only §2.1 decides them); the order of a
mutable-state read relative to the side effects around it; the absence of introduced exceptions; no exception raised during
simplification; the S3 symbolic contract (IEEE and domain-sensitive identities as today). Unknown expressions are unaffected.

### 2.3 Exception preservation (`ExceptionPreservation`)

A preserved category may not disappear nor be observed in another order. A category left out lifts one obstacle only: a
future rule may, under its explicit conditions, drop the evaluation or move it among other movable evaluations. The other
guarantees remain (side effects, allocation, determinism, mutable-state ordering, unknown code).

The five operations of the brief:

| Operation | Governed by | Notes |
|---|---|---|
| 1. Drop an exception (`t * 0 -> 0` with a throwing `t`) | the category (`CanDiscard`) | |
| 2. Change the order of two exceptions | the category (`CanReorder`) | each moved evaluation must be movable, and so must every evaluation it crosses (contextual) |
| 3. Repeat an expression that may throw | nothing for a deterministic one, locally | it throws on its first evaluation either way; a nondeterministic one (aggressive only) must raise no preserved category; the position of the first evaluation must not move (else 2 applies, §3.6) |
| 4. Merge several evaluations | as 3, locally | a merge that also moves the surviving occurrence, or that keeps a later occurrence instead of the first, is a reorder (2); §3.6 |
| 5. Move an exception from run time to simplification time | never | constant folding must not evaluate a throwing operation; not a policy dimension |
| (Introduce an exception) | never | |

The three categories suffice: operations 1 and 2 are the only ones that change whether or when a category is observed, 3 and 4
reduce to them, and 5 is never allowed. A finer "operation kind" dimension (for example, allowing reordering but not dropping)
has no demonstrated need; if C ever needs one, it would be a new, restrictive option whose default keeps today's meaning of a
cleared category, so this API would not change meaning.

`Unknown` never becomes admissible by clearing categories: it always carries `SideEffects` and `Unknown`, which no policy lifts.

## 3. Permissions

`ExpressionBehaviorPermissions` (internal) answers, for one evaluation's `ExpressionBehavior`, under a policy:
`CanDiscard`, `CanRepeat`, `CanReorder`. The P18-A overloads taking an `ExpressionTrustPolicy` remain and mean "the default
policy with this trust"; the new overloads take an `ExpressionSimplifierOptions`. One private core per permission.

### 3.1 Table — which flag forbids which permission

| Flag (after `Effective`) | Discard | Repeat / merge | Reorder |
|---|---|---|---|
| `SideEffects` | always | always | always |
| `Unknown` (with every flag) | always | always | always |
| `ReliesOnDeclaration` under `Verified` | always (widened to unknown) | always | always |
| `Allocates` | unless aggressive | unless aggressive | unless aggressive |
| `Nondeterministic` | never | unless aggressive | unless aggressive |
| `MayThrowArithmetic` | if `Arithmetic` preserved | only with `Nondeterministic` under aggressive, if preserved | if `Arithmetic` preserved |
| `MayThrowRuntime` | if `Runtime` preserved | idem, `Runtime` | if `Runtime` preserved |
| `MayThrowUserCode` | if `UserCode` preserved | idem, `UserCode` | if `UserCode` preserved |
| `ReadsMutableState` | never (local) | never (local; contextual condition below) | never (local; contextual condition below) |

Under `ExpressionSimplifierOptions.Default` the table gives exactly the P18-A permissions (tested on all 512 flag
combinations).

### 3.2 Priority rules

1. `Effective` first: an untrusted declaration becomes unknown code.
2. Imperative obstacles (`SideEffects`, `Unknown`) refuse every permission under every policy.
3. Then each remaining flag is an obstacle unless the policy lifts it (allocation and nondeterminism by the aggressive axis,
   each exception flag by its category).

Every rule has the form "a flag forbids", so the permissions are **monotone** (tested over 512 behaviors × 32 policies):
an extra flag never adds a permission; `Verified` is never more permissive than `TrustDeclarations`; preserving one more
category never adds one; disabling aggressive simplification never adds one.

### 3.3 Local, not contextual

Reading mutable state is allowed by every permission. A permission says the evaluation itself may be dropped, repeated or
moved; it does not say that its context allows it. Determinism is "same state, same outcome": `CanRepeat` on `a[0]` does not
make `a[0] + (a[0] = 1, 0) + a[0]` mergeable. Before merging, repeating or moving an evaluation that reads mutable state, a
rule must also prove that no evaluation with side effects, and no unknown code, runs between the occurrences or across the
move, as `SplitPinnedTerms` does today. A move is allowed only among evaluations that are themselves movable. No policy relaxes
this condition.

### 3.4 The `ToEffects` bridge

`ToEffects(behavior, ExpressionTrustPolicy)` is kept as it was: the gate's `ExpressionEffects` have no notion of nondeterminism
or exception categories, so the bridge cannot express the aggressive and exception axes and stays as strict as the default
there. A rule that honors those axes must ask the permissions with the options, not the bridged effects.

### 3.5 `CanRepeat` covers two different operations

`CanRepeat` is a local permission to **change the number of evaluations** (at least one remains). It covers two operations that
are not interchangeable, and it is not a sufficient proof for either of them:

- **Merging** several evaluations into one (`t + t -> 2 * t`): occurrences that could differ in the source now share one
  value. For a deterministic evaluation in an unchanged state they agree anyway; for a nondeterministic one (aggressive only), two
  clock reads become one snapshot.
- **Duplicating** one evaluation (inlining an argument used twice): one value of the source becomes several evaluations. For a
  nondeterministic one, a value the source used consistently (`c - c` with `c = Now`, always `0`) may now differ between its
  uses (`Now - Now`), a different and stronger change than a merge.

So C must check, for the exact transformation, the values (consistency of the uses of one evaluation, snapshot of several),
the position of the surviving or added evaluations (§3.6) and the context (§3.3), instead of reading `CanRepeat` as "this rewrite
is safe". A rule may impose a stricter condition than the permission (for example refuse to duplicate a nondeterministic
evaluation even under aggressive simplification); it may never impose a weaker one.

### 3.6 Combined transformations

A transformation that combines dropping, merging, duplicating or moving must satisfy **every permission corresponding to the
modifications it actually makes**, for each evaluation it affects, plus the contextual conditions of §3.3:

| The transformation… | Permissions required for the affected evaluation |
|---|---|
| drops it | `CanDiscard` |
| changes its number of evaluations, the first one staying in place | `CanRepeat` |
| changes its number of evaluations and moves the first (or keeps a later occurrence instead of the first) | `CanRepeat` and `CanReorder` |
| moves it | `CanReorder`, and every evaluation it crosses is movable |
| drops some occurrences and keeps others | `CanRepeat` (count) and `CanReorder` if the kept one is not the first |

Example: merging two deterministic evaluations that may throw is admissible locally (`CanRepeat`: the first one throws under
the same conditions); a merge that also places the surviving evaluation after another one changes the order of exceptions and
needs `CanReorder`, which a preserved category refuses. Likewise, an evaluation that reads mutable state may be merged only if
no side effect separates the occurrences, whatever its permissions. Choosing a single permission (`CanRepeat` alone) for a
combined rewrite is a defect of the rule, not a policy decision.

## 4. Guarantees of Architecture B (historical; see §8 for what C changed)

- The simplifier stored the options and never read them: no rule, gate (`AllowsRules`), `SplitPinnedTerms`,
  `CanonicalizesOperator`, `FinalizeExpression`, comparer, canonical key, memo or inlining decision changed.
- `ExpressionComparer.Default` and the comparer's internal simplifier are unchanged (default options). **Still true after C.**
- No new analysis ran during simplification and no user code ran because of the options.
- Resource consumption was identical (same transformation and structural visits; same refusals; same stack guard). **Still
  true after C for the default policy.**

Tests (category `S5-P18B`, also run by the ETC Debug job; the corpus tests were adapted by C, see §8):

- `UtilsTest/Mathematics/Expressions/Coverage/ExpressionSimplifierOptionsTests` (Unit): defaults on every constructor; setting
  at construction; invalid values (direct and `with`); independence of the axes; the permission matrix (equivalence with P18-A
  under the default, imperative obstacles, monotonicity, the exact relaxation contract, category mapping, undefined trust
  policy); permissions of analyzed expressions (trusted function, partial function, integer division, array length, clock,
  boxing, declared, unknown, assignment); for all 32 policies × exact, exact with quotes, derived: the hand corpus (lying
  declaration, declared and unknown calls, traced order, shared DAG, integer division, checked overflow, throw, custom operators
  and conversions, invoked lambda, closure, quote, clock, algebra) and 150 generated trees of every family but extensions give
  the default tree (debug view and node sharing), the same user calls during simplification, and the same values, exceptions,
  traced effects and call counts when run; 60 trees with extension nodes give the same engine reads and keep the same
  instances; custom constants see the same `Equals`/`GetHashCode` calls in the same order; parallel instances with different
  policies and a shared instance do not contaminate each other; a re-entrant simplification with another policy gives the
  default result.
- `UtilsTest.Security/Expressions/ExpressionSimplifierOptionsSecurityTests` (Security): immutability (init-only properties,
  read-only fields, no mutable static state, init-only simplifier property, `with` leaves the original intact); for six
  policies, exact and derived: identical transformation and structural visits and refusal exactly one visit below; input
  validation (depth, nodes) before recursion; controlled `Stack` refusal of a 100,000-call chain on a 256 KiB stack and a usable
  thread afterwards; joining a running operation without loosening it.

## 5. Limitations

- Only the zero and one identities consume the options (Architecture C, stages C1 and C2, §8); the canonicalization, the
  factoring and merging rules, the NaN fold and the inlining do not yet.
- `ExpressionComparer` always simplifies with the default policy (its internal instance is not configurable).
- The permissions inherit the P18-A classification and its known gaps: the `Pow` omission (conservative), and the A10-B, A10-C,
  L-1, L-2 findings (see the A-10 note).
- `TrustDeclarations` trusts the author: a lying declaration (`[Deterministic]` on a counter) would be merged under C. This is
  the documented purpose of the axis, and the reason it is off by default.

## 6. Preparation of Architecture C

Integration points (status after stages C1 and C2 in the last column; the detailed plan is in
[ArchitectureC-Audit.md](ArchitectureC-Audit.md) §6):

| Point | Before C | C would |
|---|---|---|
| `ExpressionSimplifier.AllowsRules` | operands inert by `ExpressionEffectAnalysis` | ask, per rule, the permissions of the operation the rule performs (drop, merge, move), with `Options` — **done for the zero and one identities (C1, C2)** |
| `SplitPinnedTerms` | non-inert terms pinned | pin terms that are not `CanReorder`, plus the contextual check of §3.3 |
| `CanonicalizesOperator` | no reassociation of `decimal` | allow it when `Arithmetic` is not preserved |
| `IsEliminableFloatingOperand` (NaN fold) | narrow whitelist | `CanDiscard` |
| Identities whose own operator may throw | keep the exception | consult the category of the operator's own exception |
| `InvocationInliningAnalysis` | evaluation count and order of arguments | `CanRepeat`/`CanReorder` of each argument |
| `FinalizeExpression` | none | none expected |
| `ExpressionComparer` | default simplifier | a comparer bound to a policy, or the default kept explicitly |

Problems to solve before activation (status after C2 in bold):

1. **Per-rule effect declarations.** The gate is coarse (operands inert or not). Each rule must declare which evaluations it
   drops, merges or moves, so the right permission is asked; otherwise a relaxed policy over-applies. **Solved by audited rules
   that check their own permissions (§8); only the zero and one identities are audited.**
2. **Cost.** `ExpressionBehaviorAnalysis` costs about 40 ns plus 12–25 ns per node per call; asking it at every rule on every
   node would be quadratic. C needs a per-operation, reference-keyed behavior memo inside the resource session (and the visit
   budget must count it).
3. **Policy-keyed caches.** The comparison memos (S5 P9, P15), the factor cache (P3) and the comparer's simplification must be
   keyed by policy, or kept on the default; a nested simplification with another policy joins the running session today. The
   value equality of `ExpressionSimplifierOptions` makes it usable in a key, but it is not a sufficient key: every cache must also
   include whatever else influences its result — the lexical scope context (bound parameters, binder frames), the resource
   session it lives in (entries must not outlive or cross sessions), and the simplifier instance or runtime type when a derived
   simplifier or `SimplifyQuotedExpressions` changes the rules.
4. **Contextual ordering.** The local permissions must be combined with a proof that no side effect or unknown code runs between
   merged occurrences or across a move (§3.3); `SplitPinnedTerms` is the model.
5. **Correctness items first.** P18b (S5A-03, S5A-13: decimal division exceptions) and the `Pow` omission change what is
   classified as throwing; A10-B must be fixed before any policy widens comparisons of nodes holding reflection objects.
   **P18b and `Pow` are separate pull requests (#684, #685), not prerequisites of C2 (the audit, §4, explains why); A10-B is
   avoided by invariant I-1 (permissions before any comparison) rather than fixed.**
6. **Contract decision.** Enabling any axis changes results and user-call counts; C needs its own compatibility entry (as C-05
   and C-06 had). **Decided on 2026-11-05: the default stays strictly historical; the conservative improvements of the default
   go to a separate pull request with the compatibility entry C-08 (§8).**

## 7. Measurements

Windows 11, .NET 9, Release; `master` `f4bb85ec` against the branch, plus a copy of the `master` binary as A/A control;
10 rounds of alternating processes (rotating order), each case a median of 9 batches after a 1.5 s global warm-up; table =
median over the 10 processes. Internal permissions are called through compiled delegates (same harness on both sides).

| Case | master | branch | Δ | A/A |
|---|---:|---:|---:|---:|
| `new ExpressionSimplifier()` | 39.9 ns | 39.6 ns | −0.8 % | −4.0 % |
| `new ExpressionSimplifier(limits)` | 38.7 ns | 40.2 ns | +3.9 % | +0.8 % |
| `new ExpressionSimplifier { Options = … }` | – | see below | | |
| `new ExpressionSimplifierOptions { … }` | – | 4.7 ns | | |
| read `simplifier.Options` | – | 4.7 ns (delegate overhead included) | | |
| `CanDiscard`/`CanRepeat`/`CanReorder` (trust overload) | 6.0 / 6.1 / 6.4 ns | 6.2 / 6.1 / 6.0 ns | +3 / 0 / −6 % | +3 / +3 / −3 % |
| same, options overload | – | 9.3 / 7.9 / 9.4 ns | +2–3 ns | |
| `Simplify` `x*2 + sin(y)` | 8.67 µs | 8.79 µs (options: 8.71) | +1.4 % (+0.5 %) | +1.6 % |
| `Simplify` 12-term sum | 3.66 ms | 3.69 ms (3.66) | +0.7 % (0.0 %) | +1.2 % |
| `Simplify` `(x+1)*(x+1) - x*x` | 38.1 µs | 39.1 µs (38.7) | +2.6 % (+1.6 %) | +2.7 % |
| `Simplify` `f(x)+f(x)-f(x)` (user calls) | 5.43 µs | 5.43 µs (5.36) | 0.0 % (−1.1 %) | +3.4 % |
| `Simplify` `2f(x) + 2f(y)*0` | 10.19 µs | 10.25 µs (10.11) | +0.6 % (−0.8 %) | +0.7 % |
| `Simplify` 200 nested negations | 1.28 ms | 1.27 ms (1.27) | −1.1 % (−1.2 %) | +0.5 % |
| `Simplify` 200 nested user calls | 196 µs | 194 µs (195) | −1.1 % (−0.5 %) | −1.1 % |
| `Simplify` shared DAG (10 levels) | 35.9 ms | 36.3 ms (36.1) | +1.2 % (+0.6 %) | −0.9 % |

Every `Simplify` difference, with or without explicit options, is inside the A/A spread of the same campaign: the options are
stored, never read on the simplification path.

**Open measurement: construction with explicit options.** Construction with an explicit `Options` measured 88.9 ns in this
harness (stable over the 10 processes, about +125 % against 39.6 ns without), while an isolated micro-benchmark gives 41–47 ns
against 39–40 ns (`{ SimplifyQuotedExpressions = true }`: 40 ns). The added code (one validated reference store over a
field initializer) does not explain the harness value; a benchmark, JIT or object-lifetime artifact is plausible but not
demonstrated. This note therefore does **not** conclude that the explicit-options construction has no extra cost: the harness
value stands as measured, unexplained. Construction is not on the simplification path, so it does not block B; it should be
re-measured (for example with BenchmarkDotNet and a disassembly of both paths) before C relies on per-call simplifier instances.

The options overloads of the permissions cost 2–3 ns more than the trust overloads (three property reads and the aggressive branch); they have no caller yet.

## 8. Architecture C, stages C1 and C2 — the zero and one identities consume the policy

Audit, decisions and plan: [ArchitectureC-Audit.md](ArchitectureC-Audit.md).

### 8.1 Admission

- **Historical admission (unchanged).** A node's rules run when its operands are inert (`AllowsRules`, ETC R1b, C-06).
- **Default policy: historical admission alone.** `ExpressionSimplifier.UsesHistoricalAdmission` is `true` when `Options` equals
  `ExpressionSimplifierOptions.Default` by value: results, user calls, exceptions and resource consumption are those of `master`
  before C (the S5 campaigns give byte-identical files), and so are the answers of `ExpressionComparer.Default`.
- **Any other policy: audited rules outside the gate.** When the gate refuses a node, the dispatch (`ExpressionTransformer.TryTransform`)
  asks `AllowsAuditedRulesOutsideGate` once; if it allows, only the rules `IsAuditedRule` lists run, each with the thread-static
  flag `RuleRunsOutsideGate` set for its call (and the rules it calls directly), cleared for every node transformed meanwhile
  (`TransformPrepared`), restored on failure. Every other candidate is traced as rejected by the effect gate.
- **Audited rules.** The ten overloads of `AdditionWithZero`, `SubstractionWithZero`, `MultiplicationWithZeroOrOne`,
  `DivideWithZeroOrOne`, `DivideWithZero`, `PowerOfZeroOrOne` and `PowerByZeroOrOne` declared by `ExpressionSimplifier`,
  identified by method handle: a derived simplifier's own rules, even one hiding an audited rule with `new`, keep the
  historical admission.

Leaving the historical mode is not a relaxation of an axis: any non-default policy, whatever its axes, also enables the rewrites
that change no evaluation (category A of the audit), because they are correct under every policy. Each axis still relaxes only
its own flags of the permission table (§3.1).

### 8.2 What each audited rule checks outside the gate

| Rewrite | Evaluations changed | Check | Operator |
|---|---|---|---|
| `t + 0`, `0 + t`, `t - 0`, `0 - t -> -t`, `t * ±1`, `±1 * t`, `t / 1`, `t / -1 -> -t` (not signed integral), `t ^ 1`, `t ^ -1 -> 1 / t`, `t ^ -n -> 1 / t^n` | none | none | none of these can throw (`decimal` included); `MinValue * -1` and `-MinValue` wrap alike |
| `t * 0`, `0 * t`, `0 / t` (IEEE), `t ^ 0`, `0 ^ t`, `1 ^ t` | `t` dropped | `CanDiscard(Analyze(t), Options)` before anything compares or keys `t` | `* 0`, IEEE division and `Math.Pow` never throw |
| integral or `decimal` `t / 0` | – | declines: no exception while simplifying, the dividend still runs first | – |

The dropped operand is analyzed out of its lexical context, which is conservative, by `ExpressionBehaviorAnalysis` under the
instance's own limits, joining the running operation (its visits are counted there).

### 8.3 Examples

With `F` an unannotated user method, `S` a `[Deterministic]` method, `i`, `j` integers:

| Source | Default | `TrustDeclarations` | `AggressiveSimplification` | `ExceptionPreservation = Runtime \| UserCode` | `TrustDeclarations`, preserve `Arithmetic \| Runtime` |
|---|---|---|---|---|---|
| `F(x) * 1` | kept | `F(x)` | `F(x)` | `F(x)` | `F(x)` |
| `F(x) * 0` | kept | kept | kept | kept | kept |
| `S(x) * 0` | kept | kept (user-code exceptions preserved) | kept | kept (declaration not trusted) | `0` |
| `(i / j) * 0` | kept | kept | kept | `0` | kept |
| `Stopwatch.GetTimestamp() * 0` | kept | `0` | `0` | `0` | `0` |
| `F(i) / 0` | kept (no exception while simplifying) | kept | kept | kept | kept |

A clock read is nondeterministic but has no side effect: B's table forbids repeating or moving it without aggressive
simplification, never dropping it. Note that the default canonicalization already reduces `F(x) + 0` to `F(x)`.

### 8.4 Guarantees kept under every policy

No side effect or unknown code is dropped; no exception is added, and none is moved to simplification time; an exception
disappears only with a dropped evaluation of a category the policy does not preserve; no evaluation is duplicated, merged or
moved by C2; no user code runs because of the policy (the dropped operand is analyzed, never evaluated; a custom reflection
object is unknown code, read no more than by the historical path); the resource limits, the visit budgets and the stack guard
apply to the analysis.

Tests (category `S5-P18C`, also run by the ETC Debug job):

- `UtilsTest/Mathematics/Expressions/Coverage/ExpressionSimplifierPolicyRulesTests` (Unit): historical admission exactly for the
  default-equal policies, the exact audited set (inherited, never derived or hiding); the six dropping identities × 12 operand
  families (unknown, declared, lying declaration, integer division, checked conversion, unboxing, two throws, clock, allocation,
  assignment, local block) × 32 policies, exact and derived, against the permission matrix and a family-by-family expectation;
  evaluation counts; exceptions removed only when their category is not preserved; the evaluation-preserving identities on
  `double`, `int`, `decimal` × 32 policies with values and call counts (`MinValue` and overflow cases included); the zero divisor;
  derived and hiding rules; the scope of the admission flag (nested transformation, resource refusal); the instance limits of the
  analysis; the README example.
- `ExpressionSimplifierOptionsTests` (Unit, adapted): default-equal policies give the historical tree, calls and outcomes on the
  hand corpus and 150 generated trees; every other policy keeps the guarantees above on them (no exception added, traced effects
  in order, values except where the default's value is not finite, lying calls only fewer and only under trust without
  user-code preservation); extension nodes keep their instances and reads; concurrency compares each policy with itself.
- `UtilsTest.Security/Expressions/ExpressionSimplifierPolicyRulesSecurityTests` (Security): impersonating reflection objects are
  never dropped and read no member the historical path does not read; the analysis is counted in the budget (refusal exactly one
  visit below); the pathological nesting `((F() * 0) * 0) … * 0` stays bounded (quadratic structural work, linear
  transformation work); a 100,000-level nesting ends with a `Stack` refusal on a 256 KiB stack.

### 8.5 Measurements

Windows 11, .NET 9, Release, unlimited resource limits; `master` `d16ae332` against the branch, plus a copy of the `master`
binary as A/A control; 10 rounds of alternating processes (rotating order), 1.5 s global warm-up, then for each tree the
policies are measured in 15 interleaved rounds of about 20 ms (median); table = median over the 10 processes. A first campaign
that measured the policies one after the other showed a position bias (the same `master` code measured 1.2 µs or 2.0 µs depending
on its rank) and was discarded. `F` is an unannotated user method.

| Case | Policy | master | branch | Δ | A/A |
|---|---|---:|---:|---:|---:|
| `x*2 + sin(y)` | default | 4.16 µs | 4.29 µs | +2.9 % | −0.8 % |
| 12-term sum | default | 2.25 ms | 2.35 ms | +4.1 % | +1.7 % |
| `(x+1)*(x+1) - x*x` | default | 42.3 µs | 42.9 µs | +1.6 % | +0.1 % |
| `f(x)+f(x)-f(x)` | default | 5.60 µs | 5.53 µs | −1.3 % | +4.0 % |
| `2f(x) + 2f(y)*0` | default | 9.58 µs | 9.82 µs | +2.5 % | +1.7 % |
| 200 nested negations | default | 1.37 ms | 1.41 ms | +2.7 % | +6.6 % |
| 200 nested user calls | default | 196 µs | 201 µs | +2.6 % | +11.5 % |
| shared DAG (10 levels) | default | 11.36 ms | 11.91 ms | +4.8 % | −2.3 % |
| `((F(x)*0)*0)…` 100 / 400 levels | default | 11.0 / 186.4 ms | 11.2 / 187.2 ms | +1.4 / +0.4 % | +1.1 / +0.7 % |
| `F(x) + 0` | non-default (3 policies) | 1.93–1.98 µs | 2.84–2.89 µs | +46 % | +3 to +7 % |
| `F(x) * 1` | non-default | 1.93–1.98 µs | 1.75–1.79 µs | −8 to −10 % | +1 to +7 % |
| `F(x) * 0` (drop refused) | non-default | 1.96–1.99 µs | 2.80–2.81 µs | +42 % | +3 to +4 % |
| `(i/j) * 0` (refused / dropped) | trust, aggressive / partial | 1.39–1.41 µs | 1.90–1.91 / 1.31 µs | +36 % / −7 % | +1 % |
| `2f(x) + 2f(y)*0` | non-default | 9.6–9.8 µs | 11.6 µs | +19 to +21 % | +0 to +2 % |
| `((F(x)*0)*0)…` 100 / 400 levels | non-default | 11.0 / 186–188 ms | 11.4 / 190–191 ms | +3 / +2 % | ±1.5 % |
| `new ExpressionSimplifier { Options = … }` | – | 104.1 ns | 103.2 ns | −0.8 % | −0.6 % |

**Default policy.** Every difference is inside the A/A spread of the campaign (−2.3 % to +11.5 %), but they are mostly positive
(+1.6 % to +4.8 %). The default path gained one thread-static read per transformed node (`TransformPrepared`) and one integer
test per candidate rule. An isolated run of the shared DAG, `master` against the branch against a variant of the branch without
the per-node read (8 alternating rounds), gives 5.88, 5.90 and 5.87 ms: the read is not the cause, and no other cause was found.
Like the construction measurement of B, this is recorded as an **open measurement**: it does not show that the default has
no extra cost, and it should be re-measured with BenchmarkDotNet before C3.

**Other policies.** A node refused by the gate where an audited rule applies costs about 0.5–0.9 µs more: the audited rules run
(with their reflection invocation), a refused drop runs the behavior analysis (validation and walk of the operand), and
`F(x) + 0` re-transforms its operand (`AdditionWithZero` returns `TransformCore(left)`, finding S5A-07) where the default reduces
it by the canonicalization alone. When the drop happens or the rewrite shortens the tree, the simplification is faster. Skipping
the non-audited candidates before matching their signature did not change these figures measurably. The pathological
nesting of products by zero stays within +2 to +4 % at 100 and 400 levels: its quadratic structural work (see the security
test) is small next to the transformation work.
