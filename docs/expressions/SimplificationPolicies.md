# Simplification policies — design note (S5 P18 Architecture B)

Status: public configuration only. **No rule, gate, canonicalization or comparison consults it yet**: every policy gives the
results, user calls, exceptions and resource consumption of the default.
Base: `master` `f4bb85ec` (after PR #682, A-10). Scope: Architecture B only; Architecture C (consumption) is not started.

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
| 3. Repeat an expression that may throw | nothing for a deterministic one | it throws on its first evaluation either way; a nondeterministic one (aggressive only) must raise no preserved category |
| 4. Merge several evaluations | as 3 | a merge that also moves the surviving occurrence is a reorder (2) |
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

## 4. Guarantees of this version

- The simplifier stores the options and never reads them: no rule, gate (`AllowsRules`), `SplitPinnedTerms`,
  `CanonicalizesOperator`, `FinalizeExpression`, comparer, canonical key, memo or inlining decision changes.
- `ExpressionComparer.Default` and the comparer's internal simplifier are unchanged (default options).
- No new analysis runs during simplification and no user code runs because of the options.
- Resource consumption is identical (same transformation and structural visits; same refusals; same stack guard).

Tests (category `S5-P18B`, also run by the ETC Debug job):

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

- The options have no effect yet; their meaning is fixed, their consumption is Architecture C.
- `ExpressionComparer` always simplifies with the default policy (its internal instance is not configurable).
- The permissions inherit the P18-A classification and its known gaps: the `Pow` omission (conservative), and the A10-B, A10-C,
  L-1, L-2 findings (see the A-10 note).
- `TrustDeclarations` trusts the author: a lying declaration (`[Deterministic]` on a counter) would be merged under C. This is
  the documented purpose of the axis, and the reason it is off by default.

## 6. Preparation of Architecture C (not done)

Integration points, unchanged in this version:

| Point | Today | C would |
|---|---|---|
| `ExpressionSimplifier.AllowsRules` | operands inert by `ExpressionEffectAnalysis` | ask, per rule, the permissions of the operation the rule performs (drop, merge, move), with `Options` |
| `SplitPinnedTerms` | non-inert terms pinned | pin terms that are not `CanReorder`, plus the contextual check of §3.3 |
| `CanonicalizesOperator` | no reassociation of `decimal` | allow it when `Arithmetic` is not preserved |
| `IsEliminableFloatingOperand` (NaN fold) | narrow whitelist | `CanDiscard` |
| Identities whose own operator may throw | keep the exception | consult the category of the operator's own exception |
| `InvocationInliningAnalysis` | evaluation count and order of arguments | `CanRepeat`/`CanReorder` of each argument |
| `FinalizeExpression` | none | none expected |
| `ExpressionComparer` | default simplifier | a comparer bound to a policy, or the default kept explicitly |

Problems to solve before activation:

1. **Per-rule effect declarations.** The gate is coarse (operands inert or not). Each rule must declare which evaluations it
   drops, merges or moves, so the right permission is asked; otherwise a relaxed policy over-applies.
2. **Cost.** `ExpressionBehaviorAnalysis` costs about 40 ns plus 12–25 ns per node per call; asking it at every rule on every
   node would be quadratic. C needs a per-operation, reference-keyed behavior memo inside the resource session (and the visit
   budget must count it).
3. **Policy-keyed caches.** The comparison memos (S5 P9, P15), the factor cache (P3) and the comparer's simplification must be
   keyed by policy, or kept on the default; a nested simplification with another policy joins the running session today.
4. **Contextual ordering.** The local permissions must be combined with a proof that no side effect or unknown code runs between
   merged occurrences or across a move (§3.3); `SplitPinnedTerms` is the model.
5. **Correctness items first.** P18b (S5A-03, S5A-13: decimal division exceptions) and the `Pow` omission change what is
   classified as throwing; A10-B must be fixed before any policy widens comparisons of nodes holding reflection objects.
6. **Contract decision.** Enabling any axis changes results and user-call counts; C needs its own compatibility entry (as C-05
   and C-06 had).

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
stored, never read on the simplification path. Construction with an explicit `Options` measured 88.9 ns in this harness
(stable over the 10 processes, against 39.6 ns without), but an isolated micro-benchmark gives 41–47 ns against 39–40 ns
(`{ SimplifyQuotedExpressions = true }`: 40 ns), that is the cost of one more reference store; the harness value is not
explained and is not on the simplification path. The options overloads of the permissions cost 2–3 ns more than the trust
overloads (three property reads and the aggressive branch); they have no caller yet.
