# Flatten rule of `ExpressionSimplifier` (PR B)

Status: first n-ary treatment of the simplifier, restricted to the *linear additive chains*. Built on the infrastructure of
[FlattenArchitecture.md](FlattenArchitecture.md) (PR A, `a9ec71ac`). No multiplicative chain, no division, no C3, no change to
`ExpressionDerivation<T>` or `ExpressionIntegration<T>`.

## 1. Purpose

Level by level, a sum of `n` terms costs `n - 1` node treatments, and each of them prepares its operands, dispatches the
additive rules, runs the factor comparisons of `AdditionOfEqualsElements` / `SubstractionOfEqualsElements` (each comparison
simplifies both operands again through the comparer) and canonicalizes the whole sub-chain. On a left-associated chain the
sub-chain grows by one term per level, and the comparer simplifies a canonical chain that is not a fixed point of the
subtraction rules (S5A-12): `x0 - x1 - ... - x15` with free parameters is refused by the default budget.

The flatten rule treats a whole chain at once: the terms are prepared once, and the canonicalization runs once.

## 2. B0 audit: the level by level pipeline

For a binary node `e`, `TransformCore(e)` does, in this order:

1. `PrepareExpression(left)`, then `PrepareExpression(right)`: for the simplifier, `FoldNaNOperand(TransformCore(child))`.
   The children are therefore complete results, canonicalized and `NaN`-folded, before the node sees them.
2. The node is rebuilt from the prepared children (`CopyBinaryExpression`).
3. The candidate rules of the node type run in plan order; the effect gate (`AllowsRules`) is asked once, when the first
   candidate matches. The first rule that returns a result ends the treatment: **no finalization, no canonicalization**.
4. Otherwise `FinalizeExpression`: for an ordinary `Add`/`Subtract` of a type whose operator cannot throw,
   `CanonicalizeAdditiveExpression(left, right)`, which collects the terms of both prepared operands (`CollectAdditiveTerms`,
   through ordinary `Add`, `Subtract` and `Negate`), pins the terms that are not inert, drops zero constants, aggregates the
   constants when the level holds at least two of them, sorts (stable), groups, cancels and rebuilds a right-associated chain.
5. The result goes back to the parent, whose `PrepareExpression` applies `FoldNaNOperand` to it.

Consequences that matter for an n-ary treatment:

- **The result of an inner node is what its parent's rules match.** A canonical sub-chain is an `Add` node (or `0 - t`, a
  `Subtract`); a single term, a constant or a product can also come up when terms cancel.
- **A rule result is not canonical.** `SubstractionWithAddition` returns `(a - b) - c` as built; the factoring rules return
  `(a + b) * k`; `AdditionWithZero(t, 0)` transforms `t` again.
- **The sort is stable and free parameters tie** (S4: two free parameters of one type have no order), so the order of tied
  terms is the order in which the level collected them, which depends on the grouping and on the rules that ran below.
- **Constants are aggregated per level**, only when a level holds two of them.

Hence the level by level result of a chain is *not* a function of its signed terms: `a + a` is `2 * a`, `a + (b + a)` is
`(a + a) + b`; `2*a + 2*b` is `(a + b) * 2`; `a - (b + c)` is `(a - b) - c`; `(c - b) - a` is `(0 - b) + ((0 - a) + c)` and
simplifying that again gives `(0 - a) + ((0 - b) + c)`; `((a + 5) - 5) - 3` is `(0 - 3) + a` while `(a + 5) - 8` is `-3 + a`.

### 2.1 Rule matrix

`L'`, `R'` are the prepared operands of the node. "Linear chain" is defined in §3.

| Rule (plan order within its node type) | Operands it needs | Compares? | May run user code? | What it does to evaluations | On a linear chain |
|---|---|---|---|---|---|
| `AdditionWithZero` (2 overloads) | one zero constant | no | no | drops the zero; `(t, 0)` re-transforms `t` | never matches: no zero constant term, no pair of constants, no sub-chain cancels to zero |
| `SubstractionWithZero(t, 0)` | zero constant on the right | no | no | drops the zero | never matches |
| `SubstractionWithZero(0, t)` | zero constant on the left | no | no | builds `-t` and transforms it (then `AdditionWithNegate` reorders the parent) | never matches: the `0 - t` of a canonical operand is not the left operand of a subtraction node of the source |
| `AdditionOfConstants`, `SubstractionOfConstants` | two constants | no | no | folds | never matches: no node has two constant operands, and a sub-chain with a parameter never collapses to a constant |
| `AdditionWithNegate` (2), `SubstractionWithNegate` (2) | a `Negate` operand | yes (`EqualsKeepingSimplifiedForms`), then `TransformCore` or the prepared-operand entry | yes, through the comparer | merges (`t + (-t)` → `0`), re-transforms | never matches: a negation is not crossed and is not a linear term; no rule builds one |
| `NegateWithSubstraction` | `Negate` root | no | no | re-transforms | not a chain root (`Negate` is not declared) |
| `SubstractionWithAddition`, `SubstractionWithSubstraction` | `R'` is `Add` / `Subtract` | no | no | regroups without canonicalizing; the second swaps two terms | never matches: no subtraction has a crossed right operand, and a prepared term is never additive |
| `LogarithmSimplification*`, `AdditionOfCos2andSin2Number` | calls, powers | yes | yes | merges calls | never matches: no call, no power |
| `AdditionOfEqualsElements`, `SubstractionOfEqualsElements` | any | up to 6 comparer equalities on `{1 or factor} × {1 or factor}` | yes, through the comparer | merges equal factors (`a*x + b*x`), cancels (`t - t`) | runs and **always declines** (§3.2); its comparisons are the cost the flatten rule removes |
| Finalization (`CanonicalizeAdditiveExpression`) | — | structural keys, group equality | group equality of custom constants | reorders inert terms, aggregates constants, cancels | the only transformation of every level; composes into one global canonicalization (§3.3) |

No historical rule is removed: the level by level path, the derived simplifiers and every chain that is not linear use them
unchanged.

## 3. Linear chains

A chain rooted at an ordinary `Add` or `Subtract` node is **linear** when (`ExpressionSimplifier.IsLinearChain`):

1. its type is `double`, `float`, `int`, `long`, `uint` or `ulong`, and every crossed node is an ordinary operator
   (`IsOrdinaryBinaryArithmetic`); the crossed nodes are the `Add` and `Subtract` nodes the default `CanFlattenThrough`
   accepts (no operator method, intrinsic non-nullable result type), so `decimal`, checked, lifted and user-defined
   operators are operands, and a `Negate` is an operand too (it is not declared);
2. no `Subtract` has a crossed right operand, and no crossed node has two constant operands;
3. every term is a **linear term**: a parameter; a finite non-zero constant (a boxed primitive of exactly the chain type); or
   an ordinary product of a parameter by another parameter, or by a finite constant other than `0`, `1` and `-1`;
4. no parameter occurs twice in the chain (as a term or as a factor), no constant coefficient value occurs twice, and no
   coefficient equals a constant term;
5. the chain holds at most two constant terms.

### 3.1 Preparing a linear term

A parameter and a constant are their own preparation. A product `c * x`, `x * c` or `x * y` is prepared by the
multiplicative rules into a product (`Multiplication(left, const)` commutes the constant first; the zero and one identities
do not match; `MultiplicationOfEqualsElements` compares two distinct operands and declines; the finalization orders the
factors). No user code runs, nothing throws, and the prepared term is never a sum, a difference, a negation or a constant.
The `NaN` fold cannot apply (no non-finite constant).

### 3.2 No rule applies at any level

By induction on the levels, with the matrix above. The only rule that runs is the factoring rule, and its comparisons fail:

- two operands of one node never hold the same parameter (condition 4), and the comparer's simplifier never introduces a
  parameter, so `L'` and `R'`, or a factor of one and the other operand, cannot simplify to equal trees;
- a constant coefficient is compared with another coefficient (distinct values), with a parameter, or with a constant term
  (distinct from every coefficient);
- the constant `1` the rule supplies for an operand that is not a product is compared with a factor, never `1`; an operand
  that is the constant `1` makes the rule decline before any comparison.

Nothing cancels (no repeated parameter), so a sub-chain with `k` non-constant terms keeps `k` groups, and every group holds
one term.

### 3.3 The per-level canonicalizations compose

Every level therefore ends with `CanonicalizeAdditiveExpression` on its two operands. Its result is rebuilt from one-term
groups in sorted order (`t` or `0 - t`), and the parent collects it back in that same order. A stable sort of the
concatenation of two stably sorted lists is the stable sort of the concatenation of their sources, so by induction the root
orders the terms as one stable sort of the source order does. The key comparer is the same at every level (one lexical
scope: a chain never crosses a binder).

Constants: with at most two constant terms, the level where the second one arrives is the only aggregation, with the two
constants in source order, exactly as one aggregation of the whole list does. With three or more it differs (found by the
differential campaign): a floating-point sum depends on the grouping, and when the first two cancel the third stays alone
and is not rewritten (`0 - 3` instead of `-3`).

So the level by level result is `CanonicalizeAdditiveTerms(prepared terms in source order)`, which is what the rule returns.
The helper is the body of `CanonicalizeAdditiveExpression`, extracted without change; both paths share it.

### 3.4 What is refused, and what is still accepted below it

Everything else is refused before anything is prepared: a repeated parameter (`a + a`, `a*b + a`, a shared sub-chain), equal
coefficients (`2*a + 2*b`), a coefficient equal to a constant term (`2 + 2*a`), a zero, `NaN` or infinite constant, the
coefficients `0`, `1`, `-1`, a subtracted group (`a - (b + c)`), a pair of constants (`a + (1 + 2)`), a third constant, a
negation, a call, a power, a division, a conversion, a nested product, any node with effects, `short`, `ushort` and
`decimal` chains.

A refused chain is transformed from its root exactly as before, and **the chains below it are offered in turn**: in
`(x0 - x1 - ... - x9) + Sin(y)` the root is refused and its left operand is a linear chain of its own. This also applies to
the additive nodes the rules rebuild and transform again (`a - (-b)` → `a + b`), and to the trees the comparer simplifies.

**Limit: the decision is all or nothing per chain root.** One term that is not linear, or one repetition, anywhere in the
chain sends the root to the level by level treatment, with its historical cost; only the linear sub-chains *below* the
refused node are treated at once. On a left-associated chain whose last term is the offending one, the whole prefix is a
linear sub-chain and most of the gain remains; when the offending term comes first, or when the repetition is far from
the start, the levels above the repetition pay the historical cost. Example: the S5 family `p`
(`100*x0 - x1*3 - 4*x2 - ...`, coefficient `i + 2`) repeats its first coefficient at term 98: up to 97 terms it is
accepted (linear cost), from 99 terms the levels above term 98 are treated as on `master`, whose cost on this family
grows too fast for 256 terms to finish in hours (measured: stopped after three hours on the branch; the same tree is not
expected to be faster on `master`). Splitting a refused chain into maximal linear runs is the natural next step and is not
part of this PR: it changes which terms are canonicalized together, so it needs its own proof of compatibility.

## 4. Mechanism

- `[Flatten(ExpressionType.Add, ExpressionType.Subtract)] private Expression? FlattenAdditiveChain(Expression source,
  FlattenedTerm[] terms)`: prepares each term once, in source order, with `PrepareExpression` (transformation, then `NaN`
  fold), collects it with `CollectAdditiveTerms`, and returns `CanonicalizeAdditiveTerms`. It never declines.
- `FlattenAdmission(root)` (new engine-internal hook, see [FlattenArchitecture.md](FlattenArchitecture.md) §5): the built-in
  simplifier admits the linear chains only. The test is one iterative walk of the crossed nodes that stops at the first
  refusal, reads framework operators and leaves by type tests only (an extension node is never read, an operator method
  is only tested for provenance), counts one structural visit per node, and is linear in the distinct nodes of a shared DAG.
- The effect gate is not asked for the built-in type: a linear chain only holds parameters, primitive constants and ordinary
  products under operators that cannot throw, a subset of what `ExpressionEffectAnalysis.HasInertOperands` proves inert (a
  test asserts it on every accepted chain of the differential corpus). The gate analyzes the operands recursively, which
  would bound the depth of an accepted chain by the execution stack.
- A chain that is not admitted reports `RuleRejected` with the new reason `ExpressionRuleRejection.ChainNotAdmitted`
  (detailed trace) and is not extracted.

### 4.1 Derived simplifiers

The rule is `private`. `BuildPlan` scans `GetMethods(Public | NonPublic | Instance)` of the concrete type, which does not
return the private methods of a base type: the plan of a derived simplifier holds no flatten rule, and its
`PrepareExpression`, `FinalizeExpression`, own rules and hiding rules see every node as before. A derived simplifier that
declares its own `[Flatten]` rule opts in for itself, and keeps the effect gate as its admission. No public extension point
was added. The comparisons a derived simplifier triggers still simplify with the built-in type (as before), which now
flattens the linear chains it meets; results are unchanged.

### 4.2 Policies

The operands of a linear chain are inert: the gate admits every rule on every node of it under every
`ExpressionSimplifierOptions`, no audited rule runs outside the gate, and the `NaN` fold of a non-default policy keeps the
historical decision on inert arithmetic. The rule is therefore policy-independent; `AuditedRules`, `CanDiscard`,
`CanRepeat` and `CanReorder` are untouched.

### 4.3 The prepared-operand entry

`TransformCoreWithPreparedOperands` (two calls, both in `AdditionWithNegate`) still does not go through the flatten path:
it runs the rules and the finalization of one rebuilt `Subtract` node on operands that are already prepared. It is only
reached from a node with a `Negate` operand, which is never part of a linear chain, so no accepted chain depends on it, and
its results are those of `master`. It was left unchanged on purpose: routing it to the flatten path would hand prepared
operands to a rule that prepares its terms.

A rule that calls `TransformCore` on a rebuilt additive node (`SubstractionWithNegate`, `NegateWithSubstraction`,
`AdditionWithZero`, the difference of `AdditionWithNegate` when no form is reused) offers that node as a chain: its operands
are prepared results, so it is linear only if they are linear terms or linear sub-chains, and then §3 applies to it as to a
source chain (preparing a prepared linear term again is what the level by level path does too).

## 5. Budgets and depth

Not identical to `master`, by design: the rule removes work.

| | Level by level | Accepted linear chain of `n` terms |
|---|---|---|
| Transformation visits | `2n - 1` node treatments, plus every treatment of the comparer simplifications of the factoring probes | `1 + ` the preparation of each term (`n + 1` for parameters and constants) |
| Structural visits | keys, comparisons, hashes and effect analyses of every level, and of every comparer simplification | admission walk (`n - 1`), extraction (`2n - 1`), then the keys, sort and grouping of one canonicalization |
| Execution stack | one recursion per level, and the recursive effect analysis | constant |

- Every piece of work is still counted, in the session of the operation; a refusal is never absorbed; the instance is
  reusable after a refusal (security tests, just above each limit).
- `MaxDepth` and `MaxNodes` are validated before any rule, as before: a chain deeper than 512 nodes is refused by the
  default limits although the rule would treat it without recursion.
- **Refusals that became acceptances.** A chain whose level by level work exceeded a budget and is linear is now accepted
  (`x0 - ... - x15`, free parameters: 1,960,419 visits on `master`, 17 now). The reverse does not occur on an accepted
  chain. A chain that is not linear keeps its level by level counters, minus the work saved on the linear chains below it
  and in its comparer simplifications.
- The statement of `SimplificationPolicies.md` that the default policy keeps the historical *resource consumption* holds for
  the policy mechanism; it does not describe this rule, which lowers the consumption of the linear chains under every policy.

## 6. Compatibility

Under every policy, for the built-in type: same result trees (structure, types, constants, parameter instances) for the
first and the second pass, same user-code calls in the same order, same exceptions, as `master` `a9ec71ac`, except the
resource refusals that became acceptances (§5). Derived simplifiers: unchanged, counters included, except for the work of
the comparisons they trigger. Traces of the built-in type differ on the chains concerned (one treatment per chain, the
flatten rule as the applied rule, `ChainNotAdmitted` rejections at the detailed level).

## 7. Validation

Reference: `master` `a9ec71ac` in a separate worktree, same machine, runtime and configuration.

- **Differential corpus** (a dedicated harness, the same deterministic corpus simplified by both checkouts, compared line by
  line): every tree of two terms over a pool of twenty linear and non-linear terms for `double`, `float`, `int`, `long`,
  `uint` and `decimal`, every tree of three terms and every assignment of `+`/`-` for `double` and `int` (96,768 trees), and
  seeded random trees of two to twelve terms, left, right, random and mixed groupings, free, lambda-bound and wrapped under
  another node, with linear terms, repeated parameters, zero/one/`NaN`/infinite constants, negations, calls, powers,
  divisions, checked nodes, unknown calls and logged custom constants. Compared: the result of the first and of the second
  pass, the exception, the number and the ordered log of the user `Equals`/`GetHashCode` calls, the number of effectful
  calls. **460,008 cases under the default policy: 0 difference.** 109,008 cases under each of four other policies (trust,
  aggressive, no exception preserved, permissive) and with a derived simplifier: 0 difference.
  The first run of this corpus found the three-constant divergence of §3.3 (4 cases out of 154,008), which narrowed
  condition 5.
- **S5 campaigns** (`tests/ExpressionSimplifierAudit/run-audit.ps1`, 1,000 seeds, 1,000 nested compositions, default
  policy): `canon`, `comparer`, `diff`, `effects` and `s3` are byte-identical. `converge` (64,420 cases): the first pass,
  the last pass, the category and the sizes are identical; only the list of rule names applied by the second pass differs
  (`FlattenAdditiveChain` appears in 8,419 cases). `terminate` (63,270 cases): classes, exceptions, results and output sizes
  are identical; the counters differ: 739,669 treatments on `master`, 618,329 on the branch (−16.4 %), fewer in 12,719
  cases, more in 2 cases (at most +7; not investigated further), trace depth lower in 4,349 cases and never higher.
- **Suites**: `UtilsTest.Unit`, `UtilsTest.Security` and `UtilsTest.Functional` pass.
- **Mutations** (applied one at a time, then removed): a term prepared twice, the terms in reverse order, the signs ignored,
  the admission always granted, a call accepted as a term, repeated parameters, a third constant, a subtracted group, zero
  and non-finite constants, equal coefficients, a pair of constants, the gate of derived simplifiers removed: each makes
  at least one test of `ExpressionSimplifierFlattenTests` fail. One mutation survives because it is equivalent: preparing
  the terms with `TransformCore` instead of `PrepareExpression` (no linear term can hold a `NaN` operation). The mutations
  of the brief about `decimal` and about the prepared-operand entry have nothing to mutate: `decimal` chains are not
  crossed, and that entry is not on the flatten path.

Tests whose input was a linear chain used as a vehicle were given another vehicle, with the same assertions (see the PR
description): two tracing tests, one comparer diagnostic test, and the budget tests of `UtilsTest.Security` that relied on
the S5 families `g` and on a left sum of 100 parameters to exhaust a budget. `S5A12` and `S5A08` of the deep-audit tests now
pin the level by level growth on a derived simplifier, respectively a lower budget, and the new behavior of the built-in
type.

## 8. Measures

Release, .NET 9, x64, affinity `0x14`, high priority. A = `master` `a9ec71ac`, B = a copy of the A binaries (A/A
control), C = this branch; BenchmarkDotNet in-process, 8 warm-up and 20 measured iterations, one process per variant and
round, three rounds in rotated order (A B C, B C A, C A B). Figures: median of the three round means.

**Noise.** The A/A control is the limit of what can be read: B differs from A by −0.8 % to −37 % (median), and by up to
±45 % within one round (round 1, where A ran last, was about 30 % slower for every case). Time differences below that
band are not significant; allocations are deterministic and are.

| Case (`n` terms) | Linear? | A time | B vs A | C vs A | A alloc | C alloc |
|---|---|---|---|---|---|---|
| `sumL` 16 (left sum of parameters) | yes | 2.14 ms | −7.0 % | **−99.7 %** | 2,497 KB | 11 KB |
| `sumL` 64 | yes | 419 ms | +1.2 % | **−99.99 %** | 433,883 KB | 40 KB |
| `sumR` 64 (right sum) | yes | 26.0 ms | −4.7 % | **−99.9 %** | 26,431 KB | 38 KB |
| `sumB` 64 (balanced sum) | yes | 9.08 ms | −3.5 % | **−99.7 %** | 9,745 KB | 39 KB |
| `sumLbound` 16 (lambda-bound) | yes | 2.13 ms | 0.0 % | **−99.6 %** | 2,498 KB | 12 KB |
| `subL` 12 (left difference) | yes | 33.2 ms | +4.0 % | **−99.99 %** | 24,545 KB | 10 KB |
| `p` 12 (S5 family `p`) | yes | 3.97 ms | −6.7 % | **−99.4 %** | 2,592 KB | 33 KB |
| `lin` 32 (parameters × coefficients) | yes | 93.1 ms | −13.8 % | **−99.9 %** | 58,254 KB | 105 KB |
| `g` 12 (S5 family `g`) | yes | 13.3 ms | −37.3 % | **−99.3 %** | 9,485 KB | 94 KB |
| `repeatLast` 16 (repeated parameter in the last term) | below the root | 2.21 ms | −7.8 % | **−99.3 %** | 2,496 KB | 30 KB |
| `negsum` 12 (negated sums) | sub-chains | 4.36 ms | −8.5 % | −42.8 % | 4,036 KB | 2,914 KB |
| `poly` 6 (powers) | no | 0.367 ms | −0.8 % | −26.4 % (noise) | 254 KB | 257 KB (+1.2 %) |
| `mul` 16 (product chain) | not additive | 1.25 ms | −7.7 % | −28.0 % (noise) | 827 KB | 827 KB |
| `calls` 16 (unknown calls) | no | 4.49 ms | −6.0 % | +1.0 % | 3,155 KB | 3,155 KB |
| `small` 2 | no | 6.3 µs | −4.6 % | +5.4 % | 4.2 KB | 4.2 KB |
| `subR` 12 (right difference, subtracted groups) | no | 10.1 µs | −1.6 % | −7.6 % | 4.3 KB | 4.2 KB |
| `H` 12 (refused chain) | no | 44.5 µs | −8.9 % | +2.1 % | 33.6 KB | 37.9 KB (+12.8 %) |
| `badFirst` 32 (call in the deepest term) | no | 35.6 ms | −5.3 % | +11.6 % | 32,650 KB | 35,334 KB (+8.2 %) |

- **Accepted chains**: −99 % or better in time and allocations; the cost becomes linear in the number of terms (§5).
- **Refused chains**: same transformation visits, time within the A/A band, allocations +0 to +13 %. The extra allocations
  are the admission walk (its stack and operand sets) repeated for every sub-chain of a refused chain: when the term that
  causes the refusal is the deepest one (`badFirst`), each level walks down to it again, which is quadratic in the depth
  of the chain, like the per-level canonicalizations that the level by level path performs on the same chain anyway.

**Growth** (one process per measure, default limits; `visits` = transformation visits):

| Family | `n` | A | C |
|---|---|---|---|
| `subL` | 12 | 25.1 ms, 39,011 visits | 0.010 ms, 13 visits |
| `subL` | 14 | 189 ms, 263,313 visits | 0.012 ms |
| `p` | 20 | 20.7 ms, 13,240 visits | 0.16 ms, 81 visits |
| `p` | 64 | 3,090 ms, refused (`StructuralWork`) | 0.53 ms, 257 visits |
| `p` | 97 | 12,978 ms, refused (`StructuralWork`) | 1.16 ms, 390 visits |
| `p` | 99 | — | 295 ms, 53,466 visits (coefficient repeated at term 98: refused, level by level) |
| `g` | 16 | 491 ms, 648,607 visits | 0.32 ms, 225 visits |
| balanced sum | 16 | 0.73 ms, 783 visits | 0.013 ms, 17 visits |
| `negsum` | 16 | 22,830 visits, 29.6 MB | 19,612 visits, 18.7 MB |
| `H`, `n`, `z`, `F`, product chains, Horner | 8–16 | — | same visits as A |

**Depth.** On a 256 KiB thread with `MaxDepth`/`MaxNodes` disabled, C simplifies accepted chains of 100,000 terms of every
grouping in about 200 ms; A throws `InsufficientExecutionStackException` from 512 terms on a left sum and exceeds the time
limit on a left difference of 64 terms. A refused deep chain is still a controlled stack refusal (security test).

**Acceptance** (differential corpus, default policy, both passes and the comparer simplifications included): every chain
offered to the rule is accepted, since the admission runs before the offer. 257,220 chains were simplified at once;
55.2 % of the 118,008 cases contain at least one (`double` 48.3 %, `int` 67.3 %, `long` 86.1 %, `float` 80.9 %, `uint`
86.0 %, `decimal` 0 %, the type is not crossed).

**Residual risks.**
- Admission is all or nothing per root: a long chain with one non-linear term (the `p` family from term 98) keeps the level
  by level cost, even on its linear part. Its linear sub-chains below the offending term are still accepted.
- The admission walk is repeated for every sub-chain of a refused chain (allocations above). Addressed by mission C1 (§9).
- A future change of a rule or of the canonical order must keep the proof of §3 valid; the differential corpus and the
  exhaustive grouping test are the guard.

## 9. Refused chains: known refusals (mission C1)

Status: optimization of the admission only. **No admission decision changes**, so no result, user-code call or exception
changes; only the work spent on refused chains decreases.

### 9.1 C0 characterization

A refused root is transformed level by level (§3.4), and every crossed node below it is offered in turn to the admission as
the root of a chain of its own. Each offer walked its chain from its own root, so when the cause of the refusal lies deep
(a call at the bottom of a left chain, a repetition between the last term and a deep one), each level walked down to the
cause again: `n²/2` node visits, and the operand sets rebuilt as many times. The opt-in counters of the admission
(`ExpressionSimplifier.AdmissionStatistics`, a `FlattenAdmissionStatistics` installed by an audit) measured it on
`master` `e47fb957` (C0 commit: the same walk, counters only), one whole simplification per case, `n = 64`:

| Family | Admissions | Walks | Nodes examined | of which re-examined | Cause |
|---|---:|---:|---:|---:|---|
| `sumL`, `sumR`, `sumB`, `subL`, `lin` | 1 | 1 | 63–64 | 0 | (admitted) |
| `repeatLast`, `repeatLastR`, `coefRepeatL` | 3 | 3 | 187 | 62 | repeated parameter / coefficient next to the root |
| `H` | 63 | 63 | 125 | 62 | repeated parameter, two nodes deep |
| `badFirst` (`sin(x0) + x1 + ... `) | 41,727 | 41,727 | 679,056 | 637,329 | call term (the comparisons offer new chains) |
| `sinMidL` | 36,242 | 36,242 | 631,254 | 594,952 | call term |
| `sinLastR` (right, call deepest) | 2,016 | 2,016 | 43,680 | 41,664 | call term |
| `obsFirstL` (unknown call deepest, left) | 63 | 63 | 2,016 | 1,953 | call term |
| `obsMidL` | 33 | 33 | 559 | 496 | call term |
| `obsMidB` (balanced) | 11 | 11 | 109 | 46 | call term |
| `calls` | 41,727 | 41,727 | 41,727 | 0 | call term at each root (refused at the first node) |

Causes, as classified by the walk (`FlattenRefusalCause`):

| Cause | Kind | Inherited by |
|---|---|---|
| chain type, operator not ordinary, term not linear (call or other), pair of constants, subtracted group | local: one crossed node or one term | every chain that crosses that node |
| repeated parameter, repeated coefficient, coefficient equal to a constant, third constant | global: two or three terms | every chain that holds those terms |

Effect boundaries (observable calls) are calls refused as terms: the walk never analyzes effects.

### 9.2 Strategies studied

- **A. Propagating proven refusals** (retained). Every cause of a refusal is inherited by every chain that contains it: a
  local cause belongs to a crossed node that a larger chain crosses the same way (an intrinsic operator has the type of its
  operands); a global cause involves terms that a larger chain holds too, and the sets only grow. The converse is false:
  an admitted chain says nothing about a larger one (`a + b` and `c + a` are admitted, `(a + b) + (c + a)` is not), so
  nothing is inferred from an admission.
- **B. Bottom-up summaries.** Composing per-node summaries (types, parameters, coefficients, constants) would decide every
  sub-chain from one pass, but the offers come top-down (the root first), the summaries either copy their sets (quadratic
  memory) or share them (a mutable structure behind a recorded decision), and the admitted case would pay for summaries it
  never reads. Not retained.
- **C. An operation-local cache of decisions.** Caching every decision by node is not needed: an admitted root is extracted
  and its nodes are never offered again (except through a shared DAG), so only refusals are re-asked; A is that cache
  restricted to proven refusals, filled by the walk that proves them.

### 9.3 Mechanism

- The walk (`IsLinearChain`) is unchanged in its decisions and order. It now reports where the cause lies on the **spine**:
  the path it follows from the root while every node has a single crossed operand, up to the first node with two (whose
  subtree holds everything visited afterwards). A spine node at index `i` contains every operand met at an index at least
  `i`; the walk reports the index of the refusing node (local cause), of the first constant (third constant), or the
  refusing node and the repeated key (repetition).
- On a refusal, `RecordRefusedSpine` follows the spine again and records, in the table of the operation
  (`ExpressionResourceBudget.Session.FlattenRefusals`, an `ExpressionNodeSet`), the spine nodes from index 1 down to the
  smallest index involved: for a repetition, down to the first node that holds an earlier occurrence of the key (each node
  is recorded before its own terms are tested). The root itself is not recorded (it is being decided), nor anything below
  the first node with two crossed operands.
- An offered root found in the table is refused by one lookup, counted as one structural visit.
- The operand sets keep their first four entries inline (`SmallOperandSet`), so a chain refused within its first few terms
  allocates nothing (the `H` family).

**Invariants.**
- Only proven refusals are recorded: every recorded node contains the cause. Decisions are a function of the immutable
  node alone (framework operator nodes, runtime types by reference, boxed primitives, parameter references), so an entry is
  valid in any context of the operation: another instance, a nested operation (a comparison), a DAG occurrence.
- Lifetime: the outermost operation of the thread. The outermost `Exit` drops the table (in `finally`, so after an
  exception too); a nested operation shares it; another thread has its own session. No global state.
- The table is filled and read by the exact built-in type only (a derived simplifier keeps the effect gate).
- No user code: the recording pass reads crossed framework nodes and the linear terms the walk accepted, by type tests;
  keys are compared as the sets compare them (reference for a parameter, value for a boxed primitive). An entry is a node
  reference in an open-addressing table, so no `Equals`/`GetHashCode` of a node ever runs.
- Budgets: the walk counts one structural visit per node as before, the recording pass one per recorded node, each lookup
  one. A budget refusal in the middle of the recording pass leaves a correct table (every entry already proven).

### 9.4 Complexity

| | Before C1 | C1 |
|---|---|---|
| Refused left or right chain, cause at depth `d` | `Σ` walks of every level down to the cause: `O(d²)` visits and set insertions | one walk (`d`), one recording pass (`d`), one lookup per level |
| Repetition at depth `d` with earlier occurrence at `e` | `O(d · e)` | one walk, recording down to `e`, the levels below `e` walked once each as before |
| Refused balanced chain | `O(n log n)` | unchanged (nothing is recorded past the first branching node) |
| Admitted chain | one walk | one walk (no recording, no extra allocation) |

Measured (same counters, C1 `0c3988da`, `n = 64`): `badFirst` 679,056 → 41,727 nodes examined (walks 41,727 → 1,954,
39,773 lookups), no node examined twice; `sinMidL` 631,254 → 36,302; `sinLastR` 43,680 → 2,016; `obsFirstL` and
`obsLastR` 2,016 → 63 (one walk); `obsMidL` 559 → 63; `sinMidB` (balanced) 6,397 → 817; `H` unchanged (125: its cause
is two nodes deep), allocations 211,896 → 186,696 bytes (−11.9 %, the inline sets); admitted chains: same walks, same
allocations (`sumL 64`: 41,304 → 41,256 bytes). On the `p` family past its repeated coefficient (`n = 99`, 100), the
admission is a negligible part of the cost (14,454 → 14,357 nodes examined; the level by level treatment of the levels above
term 98 dominates, 0.3 s and 197 MB for `n = 99` either way): C1 does not change the residual limit of §8.

### 9.5 Accounting changes

- Structural visits: a refused root counts its walk as before, plus one visit per node its recording pass records; a
  recorded node, offered later, counts one visit instead of its walk. Totals therefore decrease on deep refusals and are
  unchanged on admitted chains and on causes next to the root. Transformation visits are unchanged (the admission does not
  transform anything).
- Traces: unchanged (a recorded refusal reports the same `ChainNotAdmitted` rejection).

### 9.6 Validation

Reference: `master` `e47fb957` in a separate worktree, same machine and runtime.

- **Differential corpus** (the PR B harness, extended with a targeted corpus of mission C: a foreign term — `Math.Sin`, an
  observable call, an opaque carrier of a logged constant, a negation, a zero, a square, a group product — at the start, the
  middle or the end; deep repetitions of a parameter, a coefficient, a coefficient equal to a constant; two and three
  constants; shared sub-trees; left, right, balanced and random groupings with both operators, free and lambda-bound):
  487,008 cases under the default policy, 108,008 targeted cases, 138,008 cases under each of the trust, aggressive,
  no-exception-preserved and permissive policies and with a derived simplifier: **0 difference** (results of both passes,
  exceptions, ordered log of the user `Equals`/`GetHashCode` calls, effectful calls).
- **S5 campaigns** (`terminate`, `converge`, `canon`, `comparer`, `diff`, `effects`, `s3`; 1,000 seeds, 1,000 nested
  compositions): byte-identical to `master` (only `environment.txt` differs).
- **Tests.** `ExpressionSimplifierFlattenAdmissionTests` checks every admission decision against a recursive
  transcription of the definition on more than 500 trees (the targeted families of the brief in the three groupings,
  repetitions, shared DAGs, 400 seeded random chains), with the nodes offered top-down in one operation (the table filled
  as the fallback fills it), bottom-up, and one by one; every recorded node is checked to be a refused chain of the tree.
  Other tests pin the walk counts (a deep cause is walked once), the recording of repetitions, the lifetime of the table,
  the opt-in counters, and that a derived simplifier ignores the table. Security tests: the visits are counted and a budget
  refusal in the middle of the recording pass leaves a correct table; a 100,000-term refused chain is recorded on a
  256 KiB stack; hostile nodes (armed extension node, impersonating operator method, constant with counting equality
  members) are never read; tables are isolated between threads.
- **Suites**: Unit, Security, Functional and the ETC Debug job pass.
- **Mutations** (one at a time, then removed): a refusal recorded one node too deep, the table kept beyond the operation, a
  repeated parameter or coefficient accepted, the earlier occurrence of a repetition ignored, the lookup not counted, the
  inline set ignoring its overflow, the third constant reported at the refusing node, the table consulted by a derived
  simplifier, the root recorded before the walk decides: each makes at least one test fail. Removing the freezing of the
  spine index past a branching node, or the stop of the recording pass at a branching node, is not detected alone: each
  protection is sufficient for the other's case; removing both is detected by four tests.

### 9.7 Benchmarks

BenchmarkDotNet 0.14 (in-process emit toolchain, 8 warmup and 20 measured iterations of about 500 ms each, memory
diagnoser), .NET 9, i7-12700KF pinned to two performance cores (affinity `0x14`, high priority). Variants, each frozen
in its own folder and run in its own process: **A** `master` `e47fb957`, **B** a copy of the A binaries (A/A control),
**C** C1 at `ba1adeb6`. Three rounds, the order of the variants rotated between rounds; the figure is the median of the
three round means, the bracket gives the delta of each round against A. Allocations are deterministic.

| Case | A | B (A/A) | C (C1) | Allocations A → C |
|---|---|---|---|---|
| `obsFirstL 128` | 1,280 µs | +2.8 % | **−20.1 %** (−20/−23/−18) | 2,073,190 → 1,588,961 B |
| `obsFirstL 32` | 90.3 µs | +1.3 % | **−16.8 %** (−16/−19/−15) | 134,502 → 105,687 B |
| `obsLastR 64` | 349 µs | +2.7 % | **−22.7 %** (−22/−24/−23) | 530,268 → 407,327 B |
| `obsMidL 64` | 227 µs | +1.1 % | **−7.0 %** (−7/−8/−5) | 352,809 → 322,345 B |
| `sinLastR 32` | 3.26 ms | +0.4 % | −4.6 % (0/−6/−7) | 4,367,288 → 4,050,156 B |
| `sinMidL 32` | 24.6 ms | +0.3 % | −2.0 % (−2/+3/−4) | 32,632,279 → 30,434,806 B |
| `badFirst 32` | 27.6 ms | +0.7 % | −0.8 % (0/−4/+1) | 36,181,944 → 33,899,090 B |
| `H 64` | 182 µs | −0.6 % | −2.2 % (−4/−2/−2) | 211,948 → 186,737 B |
| `p 97` | 189 µs | −1.4 % | +1.4 % | 263,680 → 263,629 B |
| `calls 16`, `mul 16`, `poly 6` | 3.29 ms, 891 µs, 273 µs | +0.7 %, −1.5 %, +0.2 % | −0.2 %, 0.0 %, +2.6 % | unchanged |
| `sumL 16` | 5.01 µs | −1.6 % | **+13.4 %** (+10/+14/+13) | 11,244 → 11,192 B |
| `sumL 64` | 21.0 µs | −2.6 % | **+10.1 %** (+5/+14/+9) | 41,308 → 41,257 B |
| `sumB 64` | 21.1 µs | −1.2 % | **+12.0 %** (+8/+29/+12) | 39,619 → 39,567 B |
| `sumR 64` | 21.5 µs | −3.6 % | +7.1 % (+7/+11/+7) | 39,291 → 39,240 B |
| `subL 12` | 4.47 µs | −0.7 % | **+13.4 %** (+9/+47/+15) | 10,465 → 10,414 B |
| `repeatLast 16` | 16.3 µs | −3.4 % | +9.0 % (+14/+6/+8) | 31,171 → 31,027 B |
| `small 2` | 4.56 µs | +1.5 % | +5.4 % (+5/+8/+4) | unchanged |

The A/A control stays within ±3.6 %. Reading:

- **Deep refusals** (`obs*`, `n` from 32): a gain of 7 to 23 % in every round, well outside the control band, with the
  allocations; on `sin*` and `badFirst` the time is dominated by the level by level treatment (§9.8) and the gain is in
  the control band, while allocations drop by 6 to 8 %.
- **Admitted short chains** (`sumL`, `sumB`, `sumR`, `subL`, `repeatLast`): **a slowdown of 7 to 13 %** in every round, that
  is 0.5 to 2 µs per simplification, outside the control band. The admission of such a chain is one walk of a few dozen
  nodes, so the slowdown is a fixed cost of the walk per term (about 40 ns), not a growth. The likely cause, the inline
  sets comparing every parameter through the virtual `object.Equals` and probing the overflow set twice, was removed after
  this campaign (parameters compared by reference, one probe, same comparisons); a short re-measure could not confirm it,
  the machine having been loaded during it (sustained 30 to 40 % shifts on cases C1 does not touch), and the campaign was
  not repeated. The residual cost on admitted chains is therefore stated as measured above, as an upper bound.
- The control compares identical binaries, so it does not capture the effect of a different build (code layout); a few
  percent on microsecond cases (`small 2`, `poly 6`) are within what such an effect can produce.

### 9.8 Residual limits

- A cause next to the root records nothing (`H`, `repeatLast`): the levels below still walk their own chain, which stops at
  their own cause within two nodes, or admits them.
- Nothing is recorded past the first node with two crossed operands: a refused balanced chain keeps its `O(n log n)`
  walks (`obsMidB`).
- Chains built by the rules or by the comparisons are fresh nodes: their admissions are walked (`badFirst`: 1,954 walks
  for 41,727 admissions), each once.
- The level by level cost of a refused chain is unchanged: C1 removes the redundant admission work, not the historical
  treatment (`p` from 99 terms, `badFirst`).
- Admitted short chains measured 7 to 13 % slower in time (0.5 to 2 µs, same allocations) before the inline-set fix of
  §9.7, which was not re-measured under clean conditions.

## 10. Mixed chains: anchored chains (mission C2)

Status: first treatment of chains that are not linear. Restricted to one family whose level by level result is proven to be
one canonicalization; every other mixed chain keeps the level by level treatment (with the known refusals of §9).

### 10.1 C2 audit: which segments can be treated at once

A refused chain is processed level by level: each level prepares its two operands (the lower level's result and a term),
then runs its rules and its finalization on the prepared node. A treatment of several levels at once must reproduce what
those rules would have done with the operands they would have seen. The categories of segments:

1. **A sub-tree that is itself an admitted chain** (a linear sub-chain below a refused node): already treated at once by
   PR B when the fallback offers it (§3.4). Nothing new to gain.
2. **A path of nodes of the spine** (the levels from the root down to the node that holds a non-linear term): the
   candidates of this mission. Their result depends on the rules that run at each level.
3. **A run of terms that is not a sub-tree** (`x2 + x3` inside `((x1 + x2) + x3) + F()`): never a unit of the level by level
   treatment; regrouping it would reassociate the source. Not treated.
4. **A segment separated by an operation with effects**: the effect gate refuses every rule of a node whose prepared
   operands are not inert; this is what makes category 2 tractable (below).
5. **A segment where a factorization may apply** (`2*x + 2*y`, `x - x`, `a + (-a)`): the factoring and negation rules
   change the structure the parent sees; such a segment keeps the level by level treatment.
6. **A segment whose canonicalization depends on its neighbours** (pinned terms reading mutable state, constants summed
   per level, ties of the stable sort): composable only under the conditions of §10.2.

**Approach A, maximal admitted sub-trees**: already obtained by PR B (category 1); nothing new. **Approach C, incremental
summaries** of the canonical result along the path: it would have to reproduce the rules of every level, including the
comparisons of the factoring rules, whose user code (the simplifications of the comparer, custom `Equals`) runs once per
level and per comparison; a summary that skips them changes the user code, one that runs them is the level by level
treatment. Not retained. **Approach B, segments of a path** (retained) for the segments on which no rule can run at any
level: those where the effect gate refuses every rule.

### 10.2 Anchored chains

A chain rooted at an ordinary `Add` or `Subtract` node is **anchored** when it satisfies every condition of a linear chain
(§3) except that exactly one term, the **anchor**, is not linear, and:

1. the anchor is a static call of a runtime method (`ExpressionReflectionProvenance.IsRuntime`) whose execution is not
   inert for the effect analysis (`ExpressionEffectAnalysis.MethodEffects`: an unknown method, which is observable, or a
   function of the allow-list that may throw, such as `double.Clamp`); `Math.Sin` and the other total functions are inert
   and are not anchors (their rules may apply level by level);
2. the anchor is held by a node of the spine: every node above it has a single crossed operand (`F() + x1 + ... + xN` in
   a left grouping, `x0 + (... + (xN + F()))` in a right one, any comb of the two); the other operand of the anchor's node
   may be a crossed linear chain (`x0 + ... + x50 + F() + x51 + ... + xN`), prepared as a unit;
3. the instance uses the historical admission (`ExpressionSimplifierOptions.Default`): under an explicit policy the audited
   rules may run outside the gate, so the chain keeps the level by level treatment there.

The repetition conditions of §3 apply to the other terms (the anchor is left out of the sets). The test is the walk of §9,
which accepts the anchor when it meets it on the spine; its refusals keep the inheritance of §9.2 (a second anchor refuses
the chains that hold both, an anchor below a branching node every chain above it), so the known refusals stay correct.

### 10.3 Why one canonicalization is the level by level result

Let the **segment** be the nodes from the root down to the anchor's node, and its **operands** the terms of those nodes and
the other operand of the anchor's node, in source order.

1. **No rule runs at any level.** Every node of the segment contains the anchor. Its prepared operands hold the prepared
   anchor (the anchor's node directly, every node above it through the canonical result of the level below, which keeps
   every pinned term). When the prepared anchor is not inert, `AllowsRules` (`HasInertOperands`) refuses the node, and
   the historical admission admits no audited rule outside the gate (`AllowsAuditedRulesOutsideGate` is false): every level
   is its finalization, `CanonicalizeAdditiveExpression` of its two prepared operands (the nodes are ordinary, so
   `CanCanonicalizeCommutativeBinary` and `CanonicalizesOperator` hold).
2. **The canonicalizations compose.** In `CanonicalizeAdditiveTerms`, the anchor is pinned (not inert); when it is observable,
   every term reading mutable state is pinned too (parameters, products), the constants being the only kept terms; when it
   only may throw, the kept terms are the linear ones. Pinned terms keep their collection order and are appended after the
   kept groups, so each level collects the pinned terms of the level below in source order, followed by its own term: the
   pinned list of the root is the source order. The kept terms are distinct linear terms (no repetition, coefficient
   distinct from the constants), so, as on a linear chain (§3.3), every group holds one term, nothing cancels, the stable
   sorts compose, and at most two constants are aggregated once, in source order. A `0 - t` group is collected back as a
   zero (dropped) and `t` with its sign.
3. **The fold of `NaN` changes nothing**: no level has a `NaN` constant operand (the constants are finite).

Hence the result is `CanonicalizeAdditiveTerms` of the collected prepared operands of the segment, in source order, which
`FlattenAnchoredChain` returns.

**Preparation, order, user code.** The operands are prepared once each with `PrepareExpression`, in source order; the
anchor is prepared as a call, its arguments with it, exactly as its node prepares it. The levels only interleave their
canonicalizations, which run no user code here: the anchor is pinned, so it is never keyed, grouped or compared; the
effect analysis reads it by type tests and runtime reflection; the rebuilt nodes read its type, the return type of a
runtime method. Preparing a linear term runs no user code either (§3.1). So the observable order of user code and of
exceptions is the level by level one.

**Fallback.** The proof needs the prepared anchor to be a call of a runtime method that is not inert. The admission only
knows the raw call; if a rule rewrote it (none of the current rules does for an anchor: the probe of the audit found no
case), the rule runs the levels one by one on the prepared operands, from the anchor's node up
(`TransformCoreWithPreparedOperands`, then the `NaN` fold of each child result, as its parent's `PrepareExpression` does):
the historical treatment, without preparing anything twice. The audit counters can force this path
(`FlattenAdmissionStatistics.ForceAnchoredFallback`); the tests compare it with the level by level treatment on every
anchored case.

### 10.4 What is not treated

- A chain whose non-linear term is inert (`sin(x0) + x1 + ...`, the `badFirst` family): its factoring rules run at every
  level; skipping them requires proving that all their comparisons fail and run no user code. It keeps the level by level
  treatment (and the known refusals of §9).
- Two anchors, an anchor below a node with two crossed operands, a subtracted group, a negated call, a member access or a
  property getter (`MutableGetter()` when it is a property), a call of an instance method, a custom reflection object,
  any repetition among the other terms, a third constant.
- Every chain under an explicit policy (the anchored chains are refused there, and their levels are recorded in a table of
  their own, `FlattenAnchoredRefusals`, consulted by the instances of such a policy only).
- Derived simplifiers (the rule is private).

### 10.5 Validation

Reference: `master` `e47fb957`, same harnesses as §9.6.

- **Differential corpus**: the corpora of §9.6 (487,008 default cases, 108,008 targeted cases holding observable calls,
  carriers and throwing allow-list functions at the start, the middle and the end, 138,008 cases under each policy and
  with a derived simplifier): **0 difference** (results, exceptions, ordered user-code log, effectful calls).
- **S5 campaigns** (`terminate`, `converge`, `canon`, `comparer`, `diff`, `effects`, `s3`, 1,000 seeds): byte-identical
  to `master`.
- **Tests.** `ExpressionSimplifierFlattenAnchoredTests` compares every anchored case with the level by level treatment
  (`LevelByLevelSimplifier`): the anchor at every position, the three groupings and seeded random combs, the forced
  fallback, every policy; other chains holding calls keep their treatment; every user call runs once, in source order;
  an anchored chain is walked once. The admission oracle of §9.6 now transcribes the anchored definition too, with an
  instance under a policy interleaved in each operation. Security tests: visits are counted and the rule is refused just
  above each limit (65 transformation visits for 64 terms); a 100,000-term anchored chain, left and right, is simplified
  on a small stack (the level by level treatment is refused by the stack guard there); hostile objects are never read by
  the anchor test.
- **Two PR B tests changed premise.** `a + opaque()` and `a + unknown()` were examples of refused chains; they are
  anchored chains now. They moved to `CallChains_AreAnchored_AndKeepTheLevelByLevelResult`, which checks that their result
  is still the level by level one, and `ObservableEvaluations_AreNeitherDroppedNorMoved` allows the treatment of one call
  under the default policy only, still checking that no evaluation is dropped or moved.
- **Suites**: Unit 14,606/14,607, ETC Debug 343/344, Security 378/381 (the non-executed ones are skipped by design),
  Functional 380/383: two architecture-scan tests fail the same way on a `master` worktree (environment), one named-pipe
  test is a known flake and passes when rerun.
- **Mutations** N1–N13, one at a time: ten are detected. Three survive and are equivalent: preparing the operands in
  another order (only the anchor runs user code, so the order is not observable), dropping the `NaN` fold of the fallback
  (no operand can be `NaN`), and dropping the check that the prepared anchor is still not inert (no rule of the simplifier
  turns an anchor into an inert node; the forced fallback covers that path).

### 10.6 Benchmarks

Same campaign as §9.7, with a fourth variant **D**, C2 at `df0e274b` (before the inline-set fix of §9.7, like C).

| Case | A | C (C1) | D (C2) | D vs C | Allocations A → D |
|---|---|---|---|---|---|
| `obsFirstL 128` | 1,280 µs | −20.1 % | **−98.0 %** (−98/−98/−98) | −97.5 % | 2,073,190 → 48,374 B |
| `obsLastR 64` | 349 µs | −22.7 % | **−95.9 %** (−96/−96/−96) | −94.7 % | 530,268 → 20,122 B |
| `obsFirstL 32` | 90.3 µs | −16.8 % | **−91.1 %** (−91/−91/−91) | −89.3 % | 134,502 → 12,257 B |
| `obsMidL 64` | 227 µs | −7.0 % | **−89.5 %** (−90/−89/−89) | −88.7 % | 352,809 → 44,155 B |
| `obsMidB 64` (balanced, not anchored) | 39.4 µs | +8.2 % | +11.7 % | +3.2 % | 68,116 → 67,482 B |
| other cases | | | | −2.7 to +4.7 % | as C |

The deterministic growth harness gives the same picture: `obsFirstL 128` 4,790 µs and 1.59 MB with C1, 31.9 µs and 48 KB
with C2; `obsMidL` 3,635 → 46 µs; `obsLastR` 8,654 → 31.5 µs. The other cases stay at the C1 level (D vs C within the
±3.6 % control band but for a few microsecond cases, up to +4.7 %, within what a different build can produce, §9.7).

### 10.7 Residual limits

- One anchor only, on the spine, under the default policy; inert non-linear terms (`Math.Sin`) keep the level by level
  treatment, so `badFirst` and `sinMidL` are unchanged by C2.
- A balanced chain with an anchor below its first branching node (`obsMidB`) is not treated.
- **Pre-existing, out of scope:** an armed extension node as an argument of a call throws when the call is prepared
  (`Expression.Call` validates its arguments, which reads them). This happens level by level on `master` too, so C2 neither
  causes nor fixes it; it is a gap of the ETC R4 protection, reported separately.
