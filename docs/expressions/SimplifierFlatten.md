# Flatten rule of `ExpressionSimplifier` (PR B)

Status: first n-ary treatment of the simplifier, restricted to the *linear additive chains*; §11 describes the shared metadata of mission C3a, §12 the term analysis and the spine chains of mission C3b, §13 the global factoring of mission C3c, §14 the stabilization of mission C3d, §15 the trigonometric rule and the linear quotient chains, §16 the grouped quotient chains. Built on the infrastructure of
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
  FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher)`: prepares each term once, in source order, with
  `PrepareExpression` (transformation, then `NaN` fold), collects it with `CollectAdditiveTerms`, and returns
  `CanonicalizeAdditiveTerms`. It never declines. Since the dispatcher mission it receives the dispatcher of its group, the
  additive signature rules of the plan, which the anchored fallback uses (§10.3, [FlattenArchitecture.md](FlattenArchitecture.md) §21.6).
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
  not repeated. The figures above for admitted chains are therefore measures taken before that fix, not a bound
demonstrated for the current code.
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
case), the rule runs the levels one by one on the prepared operands, from the anchor's node up, then the `NaN` fold of
each child result, as its parent's `PrepareExpression` does: the historical treatment, without preparing anything twice.
Each level is the node rebuilt from its prepared operands, the additive signature rules through the group's
`FlattenRuleDispatcher`, then `FinalizeExpression` when none applies (until the dispatcher mission:
`TransformCoreWithPreparedOperands`, which runs the same candidates, admission and finalization; results and budgets are
unchanged, a trace shows a dispatch treatment per level, without its `Finalize` event). The audit counters can force this path
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
  above each limit (65 transformation visits for 64 terms); the walk by which the rule collects its segment is counted
  (`4n - 3` structural visits for `n` terms, refused just below and in the middle of that walk, state restored); a
  100,000-term anchored chain, left and right, is simplified
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

## 11. Enriched description of simplified terms (mission C3a)

Mission C3 (global simplification of flattened chains; not to be confused with stage C3 of the policy integration of
[ArchitectureC-Audit.md](ArchitectureC-Audit.md)) needs, for every term of a chain, what the simplifier already knows about it:
its structural hash, its canonical-order key and whether it may still change. Stage C3a builds that infrastructure **without
any new simplification**: every result is the one `master` gives.

### 11.1 C3a audit: what each stage knows and recomputes

| Stage | Available | Recomputed today | Can be shared | Cost before | Cost after C3a | Compatibility risk |
|---|---|---|---|---|---|---|
| `TransformCore` / `PrepareTransform` / `TransformPrepared` | the prepared node and its prepared children (`TransformContext`) | nothing | the result only (an `Expression`) | one visit per node | unchanged | none: hot path untouched |
| `PrepareExpression` (simplifier) | the transformed child, after the `NaN` fold | nothing | a state for the instance (`LocallySimplified`) | | unchanged | protected virtual: not modified |
| `TryTransform` / `TryInvokeTransformMethod` | candidates of the node type | effect gate per node (`HasInertOperands`, effect table for large subtrees) | effects: already shared by `Session.EffectCache` | | unchanged | none |
| `FinalizeExpression` → `CanonicalizeAdditiveExpression` | the two prepared operands | collects every term of both operands again (`CollectAdditiveTerms`) | the terms are the instances of the operands' own canonicalizations | O(terms) per level | unchanged | none |
| `CanonicalizeAdditiveTerms`: pinning | terms | `ExpressionEffectAnalysis.Analyze` per term (twice when something is pinned) | effect table (existing) | O(term size) per term per level | unchanged | none |
| `CanonicalizeAdditiveTerms`: keys | terms, scope snapshot | `BuildKeyFromSnapshot` per term, `BuildKeysFromSnapshot` per function argument, **at every level** | key per (node, scope frames) | O(Σ term size) per level, allocated | **one build per node and scopes** in the operation, for the levels that store (§11.2) | none: same key value |
| `CanonicalizeAdditiveTerms`: grouping | ordered terms | `StructuralHashRaw` per term (or per argument of a function-like term) by `GroupBy`, **at every level**; `StructuralEqualsRaw` on equal hashes | raw hash per node | O(Σ term size) per level | **one hash per node** in the operation, for the levels that store | none: same hash value |
| `CanonicalizeMultiplicativeExpression` | factors, scope snapshot | `BuildKeyFromSnapshot` per factor at every level | key per (node, scope frames) | as keys | one build per node and scopes | none |
| `FlattenAdditiveChain` | raw terms, prepared once each | nothing (one canonicalization per chain) | state of the prepared terms | O(n) | unchanged (+ state record) | none |
| `FactorEqualityProbe` (factoring rules) | prepared factors | simplification of both factors through the comparer, memoized per operation (P9, P19d) | already memoized | | unchanged | memo caches validated by S5: not touched |
| `ExpressionComparer.StructuralEqualsRaw` / `StructuralHashRaw` | | full walk each call | hash: root-level reuse only | O(size) | hash reused at the root of a term | none |

The quadratic part of a level by level chain is therefore the keys and hashes of the terms below each level: each level
collects, keys and hashes all of them again, although they are the very instances the level below already keyed and hashed
(a one-term group is rebuilt as the term itself, and `0 - t` is collected back to `t`). That is what C3a shares.

### 11.2 Representation

- `ExpressionMetadataTable` (internal, `Utils.Expressions`): an open-addressing table by node reference, modeled on the effect
  table, held by the resource session (`Session.SimplificationMetadata`) and dropped by the outermost exit. An entry holds
  the raw hash, the key with the scope snapshot it was built under, and a state with the instance that recorded it.
- `SimplifiedExpressionInfo` (internal record struct): `Expression`, `StructuralHash`, `Key`, `State`, assembled by
  `ExpressionSimplifier.DescribeTerm` for the next stages. Effects are not duplicated: the effect table of the session
  remains their only cache.
- Accessors used by the canonicalizations: `GetStructuralHash` (the hash of `GroupBy`), `GetCanonicalKey` and
  `GetCanonicalKeys` (keys of terms, function arguments and factors). Opt-in counters: `SimplificationMetadataStatistics`.
- **What is stored.** Every canonicalization *reads* the table. Only the canonicalizations whose values an enclosing one
  will read again *store* theirs: a level of the level by level treatment that already collected a canonicalized operand
  (more than two terms, or more than two factors). The single canonicalization of a flattened chain (linear or anchored), a
  two-term sum and a two-factor product store nothing: no enclosing canonicalization collects their terms again, and a
  first measure that stored everything cost `a + b` 856 bytes (+34 %) and `linear 32` +55 % allocations for no reuse. A
  state is recorded only in a table the operation already holds, for the same reason. Values that are not stored are
  computed exactly as before.

Strategies compared: changing the engine signatures (`PrepareExpression`, `TransformCore`) to return an enriched value would
break every derived transformer and add a parameter to the hot path (measured at about 10 ns per node in the dispatcher
mission); an enriched `TransformContext` only lives during one node and never reaches the parent's canonicalization; a
`ConditionalWeakTable` costs a dependent handle per node. A table of the operation, filled where the values are already
computed and read where they were recomputed, changes no signature and no hot path.

### 11.3 Hash and key: validity and scope

- **Hash.** `StructuralHashRaw` starts from an empty scope stack, identifies free parameters and unsupported nodes by
  identity and constants by value (safe constants only, no user code), so it is a function of the immutable node alone: a
  stored hash is valid for the whole operation, for every instance and policy. Equal structure implies equal hash; an equal
  hash is never taken as equality: the grouping still confirms it with `StructuralEqualsRaw` (pinned by a forged-collision
  test).
- **Key.** A key encodes a parameter bound by an enclosing lambda by its position, so it depends on the scope frames. A stored
  key is reused only under a snapshot made of the very same frame arrays, in the same order; otherwise it is rebuilt (and
  replaces the entry). The reused key is the instance built before; keys are immutable values, so the order is unchanged.
- **Budgets.** Computing a value consumes the structural visits of its walk; reusing it consumes none, because no walk
  happens. A value is stored only once fully computed, so a refusal in the middle of a walk leaves no entry. Transformation
  visits are unchanged.
- **Lifetime.** One public operation: a `Simplify` call and the internal work joining it (recursion, rules, comparer
  probes, the comparer's own simplification). Every public operation, **nested ones included**, starts with an empty table:
  the session detaches the enclosing table when an operation is entered and restores it on exit, as the comparison memo is
  detached (S5 P9). A nested public call (user code calling `Simplify` or `ExpressionComparer.Equals` while an operation
  runs) therefore never reads its caller's entries and its own budget stays exact, which
  `NestedPublicEquals_AppliesItsOwnStructuralBudget_WithoutTheRetainedMemo` pins (a first version shared the table with
  nested calls and failed that test). Nothing is global.

### 11.4 Canonicalization states

| State | Meaning | Proven for, in C3a |
|---|---|---|
| `Unknown` | nothing is known | every other node |
| `LocallySimplified` | the engine's treatment of the node by this instance ran to its end (rules and finalization); **not** a fixed point | the prepared terms of a flatten chain (`FlattenAdditiveChain`, anchored chains), recorded with the instance when the operation already holds a table (otherwise `Unknown`, a weaker and still correct answer) |
| `Canonical` | `S(e) = e` for this instance (structural equality) | parameters and constants, for the built-in type: it has no rule on them, its finalization keeps them and the `NaN` fold only rewrites binary nodes |

A state is the claim of the instance that recorded it: another instance, even of the same policy, and every derived
simplifier (which may declare rules on leaves) read `Unknown`. Nothing is ever declared `Canonical` because two successive
hashes are equal. Whether a canonical sum is a fixed point is not proven here: S5 P10 showed that the right-associated
canonical form is not a fixed point of the subtraction rules, so certifying sums is left to C3d, family by family.

### 11.5 Validation

Reference: `master` `adddc636`.

- **S5 campaigns** (`terminate`, `converge`, `diff`, `effects`, `s3`, `canon`, `comparer`, 1,000 seeds, 1,000
  nested): **byte-identical** to `master` (every result file; only `environment.txt` differs). The `terminate` and
  `converge` files hold every simplified form of the corpus, so no result, exception or rule interaction changed.
- **Tests** `ExpressionSimplifierMetadataTests` (14): reuse never changes a result (corpus of the C3 target families,
  sums, products, calls, shared sub-chains, lambdas binding the chain's parameters); a level by level chain keys and hashes
  each term about once instead of once per level; an admitted chain builds each key once and creates no table; the stored
  hash is the raw structural hash, deterministic, equal for equal structures; a forged hash collision never groups two terms;
  a key is reused only under the same scope frames and equals a fresh build; states are claims of the recording instance
  (another instance and a derived simplifier read `Unknown`); leaves are fixed points (`S(e) = e` checked on parameters,
  finite, `NaN`, infinite and signed-zero constants, decimals, strings, `null`); a node shared by several lexical contexts;
  instances and policies inside another instance's operation keep their results (a derived simplifier refused by the budget
  alone is refused there too); a nested public call starts with an empty table, costs exactly what it costs alone and leaves
  the caller's table untouched; a budget refusal leaves no table behind; the table grows only for a new node.
- **Suites** (final code): Unit 14,666/14,667 (1 skipped by design), Security 390/393 (3 skipped by design), Functional
  383/383, ETC Debug (`Coverage`, Debug) 350/351 (1 skipped).
- **Security pin found during C3a.** `NestedPublicEquals_AppliesItsOwnStructuralBudget_WithoutTheRetainedMemo` failed with a
  first version that shared the table with nested public calls (the nested comparison reused the caller's hashes and cost less
  than alone); the table is now detached per public operation (§11.3).

### 11.6 Measures

Targeted harness (Release, `Stopwatch` + `GC.GetAllocatedBytesForCurrentThread`, calibrated ~50 ms rounds, median of 9
rounds, four interleaved master/branch processes, mean of the two middle medians). Reference: `master` `adddc636`. The machine
was shared with a model server (CPU load 60–80 % for most rounds, 7 % at the end): **times are indicative** (the A/A spread of
this machine is up to ±45 %, §9.7); allocations are deterministic and exact. The BenchmarkDotNet campaign on an idle machine is
owed (C3d).

| Case | Time master | Time C3a | Δ | Alloc master | Alloc C3a | Δ |
|---|---|---|---|---|---|---|
| `tiny a+b` | 4.23 µs | 4.38 µs | +3.3 % | 2,528 B | 2,528 B | +0.0 % |
| `2x+3y` | 42.42 µs | 43.46 µs | +2.5 % | 7,602 B | 7,618 B | +0.2 % |
| `x+x+x` | 5.97 µs | 5.62 µs | -6.0 % | 1,176 B | 1,176 B | +0.0 % |
| `a*x+3*x+x` | 8.96 µs | 9.14 µs | +2.1 % | 7,234 B | 7,250 B | +0.2 % |
| `c3 target` | 592.75 µs | 548.39 µs | -7.5 % | 433,002 B | 412,928 B | -4.6 % |
| `linear 32` | 11.04 µs | 10.92 µs | -1.1 % | 21,160 B | 21,160 B | +0.0 % |
| `linear 200` | 69.28 µs | 68.69 µs | -0.9 % | 124,834 B | 124,834 B | +0.0 % |
| `repeated 16` | 34.29 µs | 37.69 µs | +9.9 % | 33,704 B | 36,040 B | +6.9 % |
| `repeated 48` | 102.73 µs | 107.14 µs | +4.3 % | 94,161 B | 104,273 B | +10.7 % |
| `sin 16x2` | 35,406.45 µs | 36,591.60 µs | +3.3 % | 15,068,664 B | 12,608,184 B | -16.3 % |
| `coef 24` | 293.35 µs | 320.25 µs | +9.2 % | 189,363 B | 194,784 B | +2.9 % |
| `product 24` | 6,783.89 µs | 6,865.73 µs | +1.2 % | 3,567,120 B | 2,990,728 B | -16.2 % |

Reuse counters (`SimplificationMetadataStatistics`, one simplification):

| Case | Keys built / reused | Hashes computed / reused |
|---|---|---|
| `a + b`, `2x + 3y`, `linear 32`, `linear 200` | n / 0 | n / 0 (no table created) |
| target `x² + 2x + ax + x + 15 + a + 3cos x + 5sin x` | 198 / 179 | 135 / 126 |
| `sin 16x2` (32 calls, two repeated runs) | 3,782 / 20,932 | 714 / 12,613 |
| `product 24` (level by level product) | 28 / 14,945 | 0 / 0 |
| derived simplifier, 24-term sum (all levels) | ≤ 28 / ≥ 275 (master: a build for every lookup) | ≤ 28 / ≥ 275 |

Reading:

- **Simple and admitted cases are unchanged**: same allocations to the byte, no table; the time differences are within
  the noise.
- **Level by level chains gain** where keys were rebuilt at every level: −16 % allocations on `product 24` and
  `sin 16x2`, −4.6 % on the target expression.
- **Residual cost**: a chain whose only level by level step is its root (`repeated 16/48`, `coef 24`: a repeated parameter
  refuses the root, the sub-chains are admitted) stores the root's values without any later reader: +2.9 to +10.7 %
  allocations (≈ 2–10 KB), time within the noise but up to +10 %. These are the repeated chains that C3b is meant to admit,
  which removes the cost; until then it is the price of the infrastructure.
- **Review fix.** The first version grew the table before looking the node up, so updating a stored node at the load
  threshold doubled it; it now grows only for a new node (`TableGrowsOnlyForNewNodes`). The `repeated 16` row was +14.7 %
  (38,648 B) before the fix; the other allocations did not change.
- Transformation visits are unchanged (the table never skips a rule or a node); structural visits decrease by the walks
  avoided.

### 11.7 Next stages

- **C3b** reads `SimplifiedExpressionInfo` of the prepared terms to decompose them (coefficient, factors) and widens the
  admission (`IsLinearChain`) to the families the new representation can treat.
- **C3c** groups terms by factor through the stored hashes, confirmed by `StructuralEqualsRaw`, and merges coefficients
  through `BuildFactoredProduct` / `AggregateFactoredCoefficient` in one pass.
- **C3d** certifies `Canonical` for the families whose idempotence is proven, and measures the remaining reuse (key of a
  product built from the keys of its factors; sorted runs of an operand's canonical form merged instead of sorted again).

## 12. Term analysis and spine chains (mission C3b)

Mission C3b prepares the global factoring of C3c: it gives each term of a chain a characterized representation (coefficient,
factors, family, metadata), and widens the admission to a family of repeated chains that the new treatment handles at a lower
cost **without losing any historical simplification**. It does not merge coefficients: `x + y + x` still gives
`(x + x) + y`, exactly as level by level, and `x + x + x` still goes through the factoring rules (`3 * x`).

### 12.1 Phase A: matrix of the refusals of `IsLinearChain`

| Cause (`FlattenRefusalCause`) | Minimal example | Historical treatment | Rules that may apply | Policy | Treatable at once? | Condition for validity | Cost of the refusal |
|---|---|---|---|---|---|---|---|
| `RepeatedParameter` | `x + y + x` | level by level; the levels above the repetition finalize, the factoring probes compare the sub-chain with the term and fail | `AdditionOfEqualsElements` / `SubstractionOfEqualsElements` (decline when the sub-chain holds two non-zero monomials) | none (inert terms) | **yes, as a spine chain** (§12.4) | comb, linear deepest pair, two certainly non-zero monomials below every level | rule dispatch and comparer probes per level, admission walk per sub-chain |
| `RepeatedParameter` (deepest pair) | `x + x + x`, `a*b + a` | the deepest pair factors (`2 * x`) and the next levels factor again | factoring rules **apply** | none | no: needs the merge of C3c | — | kept: historical |
| `RepeatedParameter` (cancellation) | `x + y - y + y` | `x + y - y` → `x`, then `x + y` | factoring may apply on a single-monomial sub-chain | none | no | — | kept |
| `RepeatedCoefficient` | `2*a + 3*b + 2*c` | as the first row | factoring rules decline (distinct parameters) | none | **yes, spine** | as the first row | as the first row |
| `RepeatedCoefficient` (deepest pair) | `2*a + 2*b` | `(a + b) * 2` (branch 1 of the factoring rule) | factoring rules **apply** | none | no | — | kept |
| `CoefficientEqualsConstant` | `x + 3*y + 3 + 3*z` | as the first row | decline | none | **yes, spine** | as the first row | as the first row |
| `TooManyConstants` | `a + 1 + 2 + 3` | constants summed per level (rounding, `0 - 3`) | constant folding per level | none | no (the grouping of the sums is observable) | — | kept |
| `SubtractedGroup` | `a - (b + c)` | `SubstractionWithAddition` regroups without canonicalizing | yes | none | no | — | kept |
| `ConstantPair` | `a + (1 + 2)` | folding, zero identities | yes | none | no | — | kept |
| `NonLinearTerm` (power, negation, division, conversion) | `x² + 2*x²`, `a + (-b)` | level by level | power, negation, factoring rules | gate-dependent for effects | no in C3b (the factoring of powers and calls is C3c) | — | kept |
| `CallTerm` inert | `sin(x) + y + 3*sin(x)` | level by level, factoring and trigonometric rules may apply | yes | gate | no in C3b | — | kept |
| `CallTerm` not inert | `F() + x1 + …` | anchored chain (C2) | none (gate refuses) | historical admission only | already treated (C2) | — | — |
| `Operator`, `ChainType` | checked, lifted, `decimal` | not crossed / level by level | yes | — | no | — | kept |

Refusals **needed for safety** are the ones where a rule applies or where the order of evaluation and grouping is observable
(deepest pair, cancellation down to one monomial, constants, subtracted groups, operators, effects). Refusals **due to the old
algorithm only** are the repetitions on which every rule provably declines: the spine family of §12.4. `IsLinearChain` is
unchanged; the spine test runs only after it refused for a repetition.

### 12.2 Phase B: representation of the terms

- `AnalyzedAdditiveTerm` (internal `readonly record struct`): the prepared term, `IsNegative`, `SourceIndex`, `Coefficient`
  (the single numeric constant factor of an ordinary product, the node itself, never converted; the term itself for a constant;
  `null` for an implicit one or when two constants remain unfolded), `FactorStart`/`FactorCount` (a slice of the shared factor
  arrays), `Family`, `IsDecomposable`, `IsFunctionLike`/`CategoryOrder` (the grouping classification), `StructuralHash`,
  `FactorHash` (an order-independent combination of the factor hashes) and `State`.
- `ExpressionSimplifier.AdditiveTermAnalysis` (internal sealed): the terms in one array, their factors and factor hashes in two
  shared arrays. A sort or a grouping permutes indices; nothing large is copied. Keys are built on demand only (`GetKey`),
  `Describe(i)` gives the `SimplifiedExpressionInfo` of a term.
- Factors are collected with `CollectMultiplicativeFactors` (the collection of the multiplicative canonicalization), through
  ordinary products only. A power and a call are kept whole as one factor (`x²` is the factor of `2 * x²`), so they can be
  matched as a unit by C3c; `a * x` has the factors `a` and `x` and **no** coefficient: no variable is chosen as the "main" one.
- An unknown term (negation, division, conversion, custom operator, extension node) is one opaque, indivisible factor,
  classified by type tests only (ETC R4: never read); a call whose method is not a runtime reflection object is function-like
  with the last category, its members never read (A-10).

| Term | Family | Coefficient | Factors |
|---|---|---|---|
| `x` | `Parameter` | — (one) | `x` |
| `3*x`, `x*3` | `Parameter` | `3` | `x` |
| `a*x` | `Product` | — | `a`, `x` |
| `(a+3)*x` | `Product` | — | `(a+3)`, `x` |
| `x²` | `Power` | — | `x²` |
| `2*x²` | `Power` | `2` | `x²` |
| `3*cos(x)` | `Function` | `3` | `cos(x)` |
| `sin(x)` | `Function` | — | `sin(x)` |
| `7` | `Constant` | `7` | — |
| `-x`, `x/2`, extension | `Opaque` | — | the term itself |

### 12.3 Phases C and D: metadata and classification

- **Present and valid**: the hash and the key stored by a canonicalization of the running operation are read back, with no walk
  (`GetStructuralHash`, `GetCanonicalKey`).
- **Absent**: computed once; stored only when the analysis is asked to record.
- **Key of another lexical context**: never reused; only the key is rebuilt, the hash (scope-independent) is still reused.
- **State**: reported as recorded by the analyzing instance; `LocallySimplified` is never used to skip anything; `Canonical`
  stays limited to the leaves of C3a.
- **Classification**: three separate notions are kept apart: the grouping class of the canonicalization (`IsFunctionLike`,
  `CategoryOrder`, an order only), the structural identity of a whole term (`StructuralHash` confirmed by `StructuralEqualsRaw`),
  and the factors a term may share (`FactorHash`, `HaveSameFactors`, `TryFindCommonFactor`). `GroupByFactors` buckets terms by
  factor hash and compares only within a bucket: `sin(x)` and `5*cos(x)` are both `Function` terms but share no factor.

### 12.4 Phase E: spine chains

A chain refused as linear for a repetition is a **spine chain** (`IsSpineChain`) when:

1. it is a **comb**: every crossed node has at most one crossed operand;
2. the local conditions of a linear chain hold, every term is a linear term other than the square `x * x`, and the chain holds
   at most two constant terms;
3. the two terms of the deepest node form a linear chain (§3.2: no rule applies to them);
4. above it, the sub-chain below every node holds **two monomials whose coefficient is certainly not zero**: all occurrences of
   the monomial (a parameter, an unordered pair of parameters, or the constant monomial when the sub-chain holds exactly one
   constant) have the same effective sign (sign of the term × sign of the coefficient), and, for an integral type, the sum of
   their magnitudes stays below `2^(bits - 1)` (wrapping arithmetic could reach zero).

**No rule applies above the deepest pair.** By induction, the prepared operand from below is the canonical sum of the sub-chain,
which has at least two groups (two certainly non-zero monomials cannot both cancel), hence an ordinary `Add` node; the other
operand is a prepared linear term (a parameter, a product, a non-zero constant). The zero, constant and negation rules need a
zero, two constants or a negation; the subtraction rules need a sum or a difference as their right operand (a subtracted group
is refused); the logarithm and trigonometric rules need calls; the factoring rules augment the sum to `1 * sum` and compare only
`1`, the sum, and the factors of the term (a parameter or a constant): a sum of two monomials with non-zero coefficients is
equal neither to a parameter nor to a constant, whatever the comparer simplifies it to.

**Treatment** (`FlattenSpineChain`). Each term is prepared once (`PrepareExpression`, source order); the deepest sub-chain that is
still linear is canonicalized once (it is a linear chain: §3); each level above it is its **finalization**, the canonicalization
of the terms collected from its two prepared operands, the child going through the `NaN` fold of its parent's preparation. No
composition of canonicalizations is assumed: every level is computed as level by level does it, so grouping, cancellation,
constant aggregation and the stable order of tied keys are those of `master` by construction. What is removed is the rule
dispatch of every level, the factor comparisons of the factoring rules (each of which simplified the sub-chain again through the
comparer) and the admission walks of the sub-chains. Keys and hashes of the intermediate levels are stored for the next level
(C3a), the root stores nothing.

**Handoff.** A spine chain only holds linear terms, so the rule cannot tell it from a linear chain by itself: the admission leaves
its root in the session of the running operation (`Session.AdmittedSpineRoot`), which the flatten rule invoked right after it
consumes (cleared on read, matched by reference). The root belongs to one public operation: `Enter` detaches it, so a nested
operation neither sees nor consumes its caller's root, and `Exit` (in a `finally`) restores it, which also releases it when a
budget refusal between the admission and the rule ends the operation (PR #701 review: a first version used a thread-static
slot, which kept the root alive after such a refusal). Outside an operation the admission does not admit spine chains.

**Known refusals.** A refused spine test reports the spine index of its cause; the chain is recorded only down to the shallower
of the linear and spine depths, so every recorded node is refused by every family.

**Policies, derived simplifiers.** The terms are inert, so the admission and the treatment are the same under every policy
(validated, §12.6). A derived simplifier keeps the effect gate (no spine walk).

| Family | Before C3b | After C3b |
|---|---|---|
| linear chains | admitted (one canonicalization) | unchanged |
| anchored chains | admitted (historical admission) | unchanged |
| comb with repeated parameters, coefficients or coefficient = constant, rules provably declining | refused, level by level | **admitted as spine chain** |
| `x + x + x`, `2*a + 2*b`, `a*b + a` (deepest pair) | refused | refused (rules apply; C3c) |
| `x + y - y + y` (one monomial left) | refused | refused |
| balanced trees with repetitions | refused | refused (`NotComb`) |
| squares `x*x`, powers, calls, negations, divisions | refused | refused |
| three constants, subtracted groups, constant pairs | refused | refused |

### 12.5 Phase F: contract with C3c

C3c obtains the analyzed terms of a chain (`AdditiveTermAnalysis.Analyze`), buckets them by factors (`GroupByFactors`, hash then
structural confirmation), finds common factors (`TryFindCommonFactor`: `a*x` and `3*x` share `x`), reads the coefficients
(`Coefficient`, the original constant nodes) and merges them through the existing `BuildFactoredProduct` /
`AggregateFactoredCoefficient`, without extracting anything again from the raw expressions. The tests show that `x, 2*x, a*x,
3*x`, `x², 2*x²` and `sin(x), 3*sin(x)` are brought together. The spine treatment is where such a merge will plug in: its levels
are the places where the historical treatment leaves `x + x` unmerged.

### 12.6 Validation

Reference: `master` `5ee1e0ea` in a separate worktree, same machine and runtime.

- **Differential corpus** (dedicated harness, the same seeded corpus simplified by both checkouts, both passes compared line
  by line; trees of 2 to 31 terms over 2 to 6 parameters, `double`, `float`, `int`, `long`, `uint`, `ulong`; left, right and
  random combs and random trees; parameters, coefficients on either side, products of parameters, squares, constants, `1 * x`,
  `sin`, negations, zeros; free, lambda-bound, under a product or a negation): **40,000 cases under the default policy, 0
  difference** (60,010 spine chains admitted); **15,000 cases under each of `TrustDeclarations`, aggressive, no exception
  preserved, the most permissive combination, and with a derived simplifier: 0 difference**.
- **S5 campaigns** (`terminate`, `converge`, `canon`, `comparer`, `diff`, `effects`, `s3`, 1,000 seeds, 1,000 nested):
  `canon`, `comparer`, `diff`, `effects` and `s3` are byte-identical; `converge` differs in `secondPassRules` only (69 cases),
  `terminate` in its counters only (`rules`, `visits`, `depth`, 985 cases): no result, class, exception or size changes.
  Transformation visits: 618,329 → 599,502 (−3.04 %), lower in 985 cases and higher in none; trace depth lower in 925 cases
  and higher in none.
- **Tests.** `ExpressionSimplifierTermAnalysisTests` (decomposition matrix, signs and positions, opaque and hostile terms,
  several constant factors, factor grouping and common factors, metadata present / absent / other scopes, keys on demand,
  states, order-independent factor hash). `ExpressionSimplifierFlattenSpineTests` (admitted families against the level by level
  reference under every policy, both passes, root admission checked through the admission itself; every flattened type;
  refused families; wrapping coefficients; 400 seeded random chains; visits below the level by level ones; budget refusal and
  reuse; one spine walk per refused root; derived simplifiers; lambdas, shared sub-chains, negated chains). The admission
  oracle of `ExpressionSimplifierFlattenAdmissionTests` transcribes the spine definition independently. Security:
  `SpineDeepChain_IsSimplified_OnASmallStack` (100,000 terms on 256 KiB, budgets refused just above each limit, reuse).
- **Tests whose premise changed** (their chain is now a spine chain; vehicle changed, assertion kept): `a + (b + a)` removed
  from the declined list of `ExpressionSimplifierFlattenTests`, `(x0 - ... - x9) + x0` replaced by `+ x0*x0` in
  `SubChains_OfARefusedChain_AreAdmittedOnTheirOwn`; `DeepRepetition_RecordsTheLevelsThatHoldBothOccurrences` checks the spine
  admission of its chains and keeps the C1 recording on a variant the spine refuses (a node with two crossed operands);
  `ComparisonDepth_IsNestedAndNeverLeaks` uses a derived simplifier as its level by level vehicle;
  `DeclinedDeepChain_IsAControlledStackRefusal` uses a square as its refused term; `SharedDag_IsRefusedByTheAdmission_WithoutExtraction`
  counts 51 structural visits instead of 50 (the spine test of the root, refused at once).
- **Suites** (final code): Unit 14,686/14,687 (1 skipped by design), Security 391/394 (3 skipped by design; one earlier full
  run failed `ImpersonatingObjects_AreNeverDroppedNorRead`, which holds no repeated chain, passes alone and in the next full
  run), Functional 383/383, ETC Debug (`Coverage`, Debug) 364/365 (1 skipped).

### 12.7 Measures

Visits are counted by bisection of the public limits (exact); keys and hashes by `SimplificationMetadataStatistics`.

| Case | Transformation visits | Structural visits | Keys built/reused | Hashes computed/reused |
|---|---|---|---|---|
| `a+b` | 3 → 3 | 10 → 10 | 2/0 → 2/0 | 2/0 → 2/0 |
| `x+x+x` (refused) | 11 → 11 | 7 → 11 | 0/0 → 0/0 | 0/0 → 0/0 |
| `repeated 16` | 39 → 19 | 345 → 209 | 48/1 → 16/17 | 48/1 → 16/17 |
| `repeated 48` | 103 → 51 | 1,525 → 911 | 144/1 → 48/49 | 144/1 → 48/49 |
| `repeated 512` | 1,031 → 515 | 25,445 → 15,777 | 1,536/1 → 512/513 | 1,536/1 → 512/513 |
| `coef 24` | 250 → 127 | 1,348 → 707 | 171/0 → 75/24 | 73/0 → 25/24 |
| `coef 128` | 1,290 → 647 | 9,080 → 5,127 | 899/0 → 387/128 | 385/0 → 129/128 |
| `tail 16` | 650 → 25 | 5,197 → 444 | 128/1,159 → 16/74 | 102/1,155 → 16/74 |
| `tail 48` | 167,574 → 73 | 5,111,805 → 4,542 | 10,700/1,029,691 → 48/654 | 7,090/1,029,471 → 48/654 |
| `twice 48` | 75,708 → 73 | 895,167 → 4,980 | 10,836/127,983 → 24/876 | 6,152/125,959 → 24/876 |
| `cancel 128` | 3,859 → 193 | 102,133 → 55,220 | 384/6,976 → 128/3,632 | 384/6,976 → 128/3,632 |
| `rightRep 128` | 263 → 131 | 4,711 → 2,807 | 384/1 → 128/129 | 384/1 → 128/129 |
| `xxx 16` (refused) | 76 → 76 | 59 → 102 | 0/0 | 0/0 |
| `linear 200` | 201 → 201 | 2,974 → 2,974 | 200/0 → 200/0 | 200/0 → 200/0 |
| `sinMid 16` (refused) | 1,435 → 1,435 | 10,259 → 10,259 | 210/3,974 → 210/3,974 | 33/3,546 → 33/3,546 |
| `tail 512`, `twice 128` | not measured on `master` (hours) | 769 / 193 | 981,738 / 49,280 | |

`repeated n` is a linear chain of `n` parameters plus the first one again; `coef n` the coefficients `i + 2` then the first
coefficient on a new parameter; `tail n` `n` parameters then the first `n / 4` again; `twice n` every parameter twice; `cancel n`
`n` parameters then the first `n / 4` subtracted; `rightRep n` a right comb with a repetition at the top; `xxx n` the same
parameter `n` times.

Allocations per simplification (deterministic, Release, `GC.GetAllocatedBytesForCurrentThread`):

| Case | `master` | C3b | Δ |
|---|---|---|---|
| `a+b` | 2,528 B | 2,528 B | 0 % |
| `x+x+x` (refused) | 1,176 B | 1,576 B | +34 % |
| `repeated 16` | 36,040 B | 28,200 B | −21.8 % |
| `repeated 48` | 104,273 B | 86,561 B | −17.0 % |
| `repeated 512` | 1,031,332 B | 888,556 B | −13.8 % |
| `coef 24` | 197,238 B | 110,479 B | −44.0 % |
| `coef 128` | 1,028,424 B | 561,763 B | −45.4 % |
| `tail 16` | 983,650 B | 53,720 B | −94.5 % |
| `tail 48` | 587,934,920 B | 321,516 B | −99.9 % |
| `twice 48` | 110,981,920 B | 368,845 B | −99.7 % |
| `cancel 128` | 3,780,848 B | 1,563,116 B | −58.7 % |
| `rightRep 128` | 246,819 B | 196,722 B | −20.3 % |
| `xxx 16` (refused) | 8,352 B | 8,992 B | +7.7 % |
| `linear 32`, `linear 200`, `sinMid 16` | | | 0 % |

The C3a baseline of the mission (`repeated 16` 36,040 B, `repeated 48` 104,273 B, `coef 24` 194,784 B on C3a) is below the
pre-C3a `master` now: 28,200 B, 86,561 B and 110,479 B.

Times (Release, `Stopwatch`, 300 ms warm-up per case, calibrated ~40 ms rounds, median of 9 rounds per process; four processes
interleaved `master`, C3b, `master`, C3b; mean of the two processes, each process in parentheses). A first campaign ran while a
language model server loaded the processors and was discarded (±40 % between processes of one binary); this one ran on an idle
machine (the server consumed under 0.2 s of CPU per process). BenchmarkDotNet results follow.

| Case | `master` µs | C3b µs | Δ |
|---|---|---|---|
| `repeated 16` | 18.48 (18.32/18.63) | 11.53 (11.05/12.01) | −37.6 % |
| `repeated 48` | 49.89 (48.45/51.33) | 33.77 (35.07/32.46) | −32.3 % |
| `repeated 512` | 567.97 (565.86/570.07) | 363.39 (361.26/365.52) | −36.0 % |
| `coef 8` | 62.05 (63.41/60.70) | 30.64 (31.36/29.93) | −50.6 % |
| `coef 24` | 163.05 (162.06/164.04) | 78.83 (78.38/79.28) | −51.7 % |
| `coef 128` | 862.57 (848.94/876.20) | 409.52 (409.51/409.53) | −52.5 % |
| `tail 16` | 757.52 | 23.98 | −96.8 % |
| `tail 48` | 604,515.75 | 157.02 | −99.97 % |
| `tail 512` | — | 19,323.45 | — |
| `twice 48` | 132,385.45 | 215.43 | −99.8 % |
| `cancel 128` | 2,701.59 | 986.39 | −63.5 % |
| `rightRep 128` | 129.07 | 77.16 | −40.2 % |
| `x+x+x` (refused) | 2.04 | 2.12 | +4.2 % |
| `xxx 8` (refused) | 6.62 (6.46/6.79) | 7.33 (7.25/7.41) | +10.6 % |
| `xxx 16` (refused) | 14.23 (14.02/14.44) | 15.46 (15.46/15.46) | +8.6 % |
| `linear 32` | 9.50 | 9.36 | −1.5 % |
| `linear 200` | 64.94 | 61.81 | −4.8 % |
| `sinMid 16` (refused) | 2,272.96 | 2,227.99 | −2.0 % |

`a + b` (first case of every process, 1.58 and 4.12 µs on the two `master` processes) is a warm-up artifact: its path and
allocations are those of `master`. The only measurable cost is the refused `xxx` family (+9 to +11 %, the one walk of the spine
test per operation); every admitted family is faster in time, allocations and visits.
**BenchmarkDotNet** (0.14.0, in-process, 8 warm-up and 20 measured iterations, `MemoryDiagnoser`; four processes `master`, C3b,
`master`, C3b, affinity `0x14`, high priority; the language model server consumed no CPU during any of them). Mean of the two
processes of each variant, each process in parentheses; allocations per operation as reported by the diagnoser.

| Case | `master` µs | C3b µs | Δ | Alloc `master` | Alloc C3b |
|---|---|---|---|---|---|
| `a+b` | 0.93 (0.93 / 0.93) | 0.93 (0.93 / 0.93) | +0.2 % | 2.47 KB | 2.47 KB |
| `repeated 16` | 18.00 (18.20 / 17.79) | 11.19 (11.38 / 11.01) | −37.8 % | 35.20 KB | 27.54 KB |
| `repeated 48` | 49.88 (50.29 / 49.46) | 31.74 (32.03 / 31.45) | −36.4 % | 101.83 KB | 84.53 KB |
| `repeated 512` | 554.20 (553.59 / 554.80) | 380.29 (388.28 / 372.29) | −31.4 % | 1,007.16 KB | 867.73 KB |
| `coef 8` | 57.48 (57.51 / 57.46) | 28.64 (28.92 / 28.35) | −50.2 % | 67.74 KB | 39.26 KB |
| `coef 24` | 161.85 (164.07 / 159.62) | 78.46 (78.44 / 78.49) | −51.5 % | 192.62 KB | 107.89 KB |
| `coef 128` | 831.00 (829.86 / 832.14) | 411.50 (408.95 / 414.06) | −50.5 % | 1,004.34 KB | 548.59 KB |
| `tail 16` | 719.81 (721.20 / 718.42) | 23.83 (23.80 / 23.86) | −96.7 % | 960.64 KB | 52.46 KB |
| `tail 48` | 551,911.89 (554,292.57 / 549,531.20) | 160.37 (161.54 / 159.19) | −99.97 % | 574,163.23 KB | 313.98 KB |
| `tail 512` | — | 18,650.70 (18,606.12 / 18,695.28) | — | — | 27,710.13 KB |
| `twice 16` | 762.26 (769.93 / 754.58) | 27.07 (27.11 / 27.03) | −96.4 % | 882.20 KB | 55.05 KB |
| `twice 48` | 126,800.68 (128,116.82 / 125,484.54) | 208.02 (210.04 / 206.00) | −99.8 % | 108,378.05 KB | 360.20 KB |
| `cancel 16` | 48.54 (48.77 / 48.30) | 20.72 (20.70 / 20.75) | −57.3 % | 84.64 KB | 47.68 KB |
| `cancel 128` | 2,632.46 (2,604.41 / 2,660.51) | 982.15 (998.01 / 966.29) | −62.7 % | 3,692.25 KB | 1,526.48 KB |
| `rightRep 16` | 17.27 (17.18 / 17.35) | 10.63 (10.84 / 10.42) | −38.4 % | 34.03 KB | 25.34 KB |
| `rightRep 128` | 124.49 (124.17 / 124.82) | 82.58 (81.09 / 84.08) | −33.7 % | 241.04 KB | 192.11 KB |
| `x+x+x` (refused) | 2.05 (2.06 / 2.04) | 2.22 (2.21 / 2.22) | +8.1 % | 1.15 KB | 1.54 KB |
| `xxx 8` (refused) | 7.01 (7.00 / 7.03) | 7.48 (7.45 / 7.50) | +6.6 % | 3.84 KB | 4.32 KB |
| `xxx 16` (refused) | 14.80 (14.65 / 14.96) | 15.74 (15.59 / 15.90) | +6.3 % | 8.16 KB | 8.78 KB |
| `linear 32` | 9.67 (9.64 / 9.70) | 9.70 (9.81 / 9.59) | +0.3 % | 20.66 KB | 20.66 KB |
| `linear 200` | 61.15 (60.13 / 62.17) | 62.36 (63.84 / 60.89) | +2.0 % | 121.91 KB | 121.91 KB |
| `sinMid 16` (refused) | 2,175.65 (2,219.41 / 2,131.90) | 2,244.01 (2,241.57 / 2,246.45) | +3.1 % | 2,630.71 KB | 2,630.71 KB |

Reading: every admitted family is faster (−31 to −99.97 %) and allocates less. `linear` and `sinMid` run the code of `master`
(same visits, same allocations): their differences (+0.3 to +3.1 %) are below the spread between the two `master` processes
(3.4 % and 4.1 %). The only measured cost is the refused `x + x + ... + x` family (+6 to +8 %, about 0.5–1 µs and +400–600 B):
the one walk of the spine test per operation.
### 12.8 Behaviors deliberately unchanged, residual limits

- No result changes: the spine treatment computes each level as level by level; repeated terms are still grouped as `x + x`
  and not merged (C3c). `x + x + x`, `2*a + 2*b`, cancellations down to one monomial, balanced trees, powers, calls, negations,
  three constants and subtracted groups keep the level by level treatment.
- The levels above the linear sub-chain are canonicalized one by one: a chain with many repeated levels stays quadratic in
  their number (`tail 512`: 981,738 structural visits). Merging them requires the global factoring of C3c, which will also
  change the results on purpose.
- A refused spine root costs one walk of its comb once per operation (`xxx 16`: +43 structural visits, +640 B), then a lookup
  per level (`FlattenSpineRefusals`).
- `AdditiveTermAnalysis` is not yet used by the production paths: it is the input of C3c. No `Canonical` state was added.

## 13. Global factoring of the additive chains (mission C3c)

Mission C3c uses the term analysis of C3b and the metadata of C3a to simplify a whole additive chain from its terms: the
terms holding the same factors are merged, and the terms of a chain are grouped by their common factors, instead of a
succession of binary factorings that only sees the pairs the grouping of the source happens to form. **Unlike C3a and C3b,
C3c produces new simplified forms** (`x^2 + 2*x + a*x + x + 15 + a + 3*cos(x) + 5*sin(x)` gives
`15 + (a + (3*cos(x) + (5*sin(x) + (x*(3 + a) + x^2))))` instead of keeping `x`, `2*x` and `a*x` apart), within the
symbolic-equivalence contract and the policies of the simplifier.

### 13.1 Audit: the additive and subtractive rules

| Rule | Operands | Attributes | Order | Applies | Declines | Effects, policy | Global treatment | Risk against the levels |
|---|---|---|---|---|---|---|---|---|
| `AdditionWithZero` (×2), `SubstractionWithZero` (×2) | a zero constant and a non-constant operand | `[ConstantNumeric(0)]` | first | drops the zero; `0 - t` gives `-t` (signed types) | constant pair | gate; zero always droppable | zeros dropped by the canonicalization; a sum of subtracted terms becomes a negation (§13.6) | `-0.0` of a constant pair: refused (§13.4) |
| `AdditionOfConstants`, `SubstractionOfConstants` | two numeric constants | `[ConstantNumeric]` | before the factoring | exact fold in the node type | — | inert | **refused**: a constant pair keeps the levels (exact `-0.0 + -0.0`) | none (refused) |
| `AdditionWithNegate` (×2), `SubstractionWithNegate` (×2), `NegateWithSubstraction` | a negation operand | `[ExpressionSignature(Negate)]` | before the factoring | rewrites to a difference / sum (cancels equal operands) | custom operators | gate | signs collected by `CollectAdditiveTerms`, cancellation by merging | none |
| `SubstractionWithAddition`, `SubstractionWithSubstraction` | a subtracted sum / difference | `[ExpressionSignature(Add/Subtract)]` | before the factoring | reassociates without canonicalizing | custom operators | gate | flattened with signs | none (the levels' shapes were not canonical) |
| `AdditionOfEqualsElements`, `SubstractionOfEqualsElements` | any two operands, products split at their top node | — | last additive rules | factors a common top-level operand, folds a constant coefficient | no common operand; `1` guard | gate | phase A (same factors) and phase B (common factor or coefficient, among all the terms) | a sub-sum equal to a factor of a term: such a term (a factor that is a sum) refuses the chain |
| `LogarithmSimplificationAdd/SubstractNumber`, `Logarithm10…` | two logarithm calls | call signatures | — | `log a + log b → log(a*b)` | other calls | gate | **refused**: logarithms are not factorable functions | none |
| `AdditionOfCos2andSin2Number` | two powers of calls | power signatures | — | `sin² + cos² → 1` | other shapes | gate | **refused**: a power of a call is not factorable, and a product holds one call at most (`sin*sin` would become `sin²`) | none |

The cases of the mission: `2*a + 2*b` (the binary rule factors the shared coefficient: phase B does, `2*(a + b)`), `a*b + a`
(`a*(1 + b)`), `a - (b + c)` (flattened: `(0 - b) + ((0 - c) + a)`), `x + x + x` (`3*x`), `x + y - y + y` (`x + y`),
`x^2 + 2*x^2` (`3*x^2`, the power is one factor), `sin(x) + cos(x)` (two distinct monomials: unchanged).

### 13.2 Phase A: identical monomials (`MergeIdenticalMonomials`)

In the additive canonicalization of the built-in type (`CanonicalizeAdditiveTerms`, so at every level and for every
flattened chain), after the pinned terms are split off, the zeros dropped and the constants aggregated:

1. the inert terms are analyzed once (`AdditiveTermAnalysis.Analyze`, hashes read from the metadata of C3a when a lower level
   stored them) and grouped by their factors (`GroupByFactors`: buckets by the order-independent factor hash, every member
   confirmed by a multiset comparison of the factors, `StructuralEqualsRaw`), so `2*a*b` and `3*b*a` meet and `x*x` never
   meets `x`;
2. only the terms whose factor part is a parameter, a product, an ordinary power or a call of a runtime method merge; an opaque
   term (a division, a conversion, a custom operator, an extension node) keeps the historical grouping;
3. the coefficients of a group (an implicit `1`) are summed in source order with `FoldConstants`, as successive binary
   factorings fold them (`SumCoefficients`): exact modular arithmetic for the integral types (`3*x - 4*x` is
   `4294967295*x` for `uint`, as before), IEEE-754 for the floating-point types (an overflow gives the infinity the binary
   factoring gives);
4. the group becomes one term: the factor part of its first member (the right operand of the canonical `c * rest`, never
   rebuilt) times the sum; `1` gives the factors alone, `-1` the factors subtracted, `0` drops the group (the terms are inert).

Coefficients are computed directly: `2*x + 3*x + 4*x` gives `9*x`, never `(2 + 3)*x + 4*x`. A derived simplifier keeps the
historical canonicalization (`MergesMonomials`, internal and virtual for the level by level references of the tests only).

### 13.3 Phase B: common factors (`FactorCommonFactors`)

Only on all the terms of a whole chain (a factorable chain, §13.4, and recursively the residues of one), so that the choice of
a factor never depends on how the source groups them:

- **Candidates**: every factor of a mergeable term (compared structurally, as a multiset member) and every numeric coefficient
  other than `0`, `1`, `-1` (compared by value; a constant term counts as its own coefficient, as the binary rule factors
  `2*a + 2`). Each round indexes the candidates of every term once in two dictionaries (structural hash confirmed by a
  structural comparison, one structural visit per indexed factor): never all terms against all terms.
- **Choice**: the candidate held by the most terms (it removes the most terms); a tie prefers a symbolic factor to a
  coefficient, then the first in the canonical order of the keys (`ExpressionCanonicalOrder`). The choice depends on the
  structure of the terms only, never on their source order nor on a "main" variable: `a*b + a*c + b*c` gives the same tree
  in the six orders.
- **Factoring**: the terms holding the candidate become one product, the candidate times the canonical sum of their residues
  (each term without one occurrence of the factor, or without its coefficient), the residues being factored in turn and
  their constants aggregated once: `2*x + a*x + 3*x` gives `x*(5 + a)` directly, `a*b*c + a*b*d` gives `a*(b*(c + d))`,
  `a*x + a*y + b*x + b*y` gives `(x + y)*(a + b)`. The product is canonical (`BuildCanonicalProduct`, the core of the
  multiplicative canonicalization), its numeric factors folded into one coefficient; terms that are all subtracted give a
  subtracted product of a positive sum. Every round removes at least one term, so the rounds end with no candidate shared by
  two terms.

### 13.4 Admission: the factorable family (`IsFactorableChain`)

After the linear test refused a chain, inside an operation, for the built-in type: every crossed node is an ordinary operator
of a flattened type and every term is factorable: a finite numeric constant, a parameter, an ordinary negation of a product,
or an ordinary product of parameters, finite constants, ordinary powers of a parameter by a finite constant and calls of an
inert runtime function of `Math` or `double` (`Sin`, `Cos`, `Tan`, their hyperbolic and inverse forms, `Exp`, `Sqrt`,
`Cbrt`, `Abs`) on parameters and constants, **one call per term at most**. Refused, and so kept level by level:

- a crossed node with two constant operands (the levels fold it exactly: `-0.0 + -0.0` is `-0.0`);
- a logarithm, `Pow`, a power of a call, two calls in one product (§13.1), a division, a conversion, a custom operator, a call
  that is not inert (an anchored chain, C2), any extension node or impersonating reflection object (type tests only, the
  provenance of a method checked before its name is read: ETC R4, S5 A-10);
- a factor that is a sum (`a + 3 + (a + 3)*x`: the levels factor the sub-sum `a + 3` itself, `(1 + x)*(3 + a)`);
- a shared sub-chain whose revisits exceed its distinct nodes (a DAG of `2^n` paths): refused after a number of visits linear in
  the distinct nodes. A few shared occurrences are accepted, so that a tree and its rebuilt copy are simplified alike (the
  comparer's reflexivity relies on it).

The family includes the linear terms, so it supersedes the spine chains of C3b, which are tested only when it is switched off
(`ExpressionSimplifier.FactorableChainsDisabled`, audits and tests; the C1/C2/C3b test classes run with it, against a level
by level reference that merges monomials too). A refused chain records its spine down to the shallower of the linear and
factorable depths (C1 known refusals). The admitted root travels to the rule in the session
(`ExpressionResourceBudget.Session.AdmittedFactorableRoot`), detached by a nested operation and released on exit, as the spine
root after the review of #701.

### 13.5 The signature rules of an admitted chain

Level by level, a crossed node sees two terms, a term and a canonical sub-sum, or two sub-sums. The zero, constant, negation and
reassociation rules are identities the canonicalization applies. The factoring rules split products at their top node and
compare the parts; a sub-sum is a single augmented factor, and since no factor of a factorable term is a sum, a sub-sum can
only match a whole sub-sum, and two terms match on a factor or a coefficient: phase A and phase B find these among all the
terms at once. The logarithm and trigonometric rules cannot apply (§13.4). So no applicable rule is lost: the global result
applies every identity the levels apply, among all the terms instead of the pairs the source's grouping forms. Nothing calls
the dispatcher level by level: the guarantee comes from the admission, not from a fallback.

### 13.6 Canonical shapes

- **Factored products**: the binary factoring of the built-in type builds the canonical product when the coefficient is not a
  constant (`a*x + b*x` gives `x*(a + b)`, the tree a second call gave from `(a + b)*x`), the shape phase B builds.
- **Subtracted sums**: a factorable chain whose terms are all subtracted gives the negation of their sum, as
  `SubstractionWithZero` (`0 - t → -t`) and `SubstractionWithNegate` (`(-a) - b → -(a + b)`) give it; a lone subtracted
  constant is folded (`0 - 1 → -1`, in every type); an unsigned type keeps `0 - t`.
- `0 - t` stays the representation of a subtracted group inside a sum (S5 P10), except for a constant of a type with an
  ordinary negation, which is folded into its negation in every canonicalization of the built-in type (`y - 1` gives
  `-1 + y`, the form the next call folded from `(0 - 1) + y`; `NegateSubtractedConstant`); an unsigned type kept `0 - c` until
  mission C3d, which folds it to its modular value too (§14.3, F6).

### 13.7 Idempotence

`Simplify(Simplify(e))` is structurally `Simplify(e)` on every example of the mission and on 300 random factorable chains
(`ExpressionSimplifierGlobalFactoringTests`); no state is promoted to `Canonical`. The transformations are oriented: phase B
only factors (the simplifier has no distribution of a product over a sum), and every round removes terms, so no
`a*x + b*x → (a + b)*x → a*x + b*x` cycle exists. A result holding a factor that is a sum is not factorable on the second pass;
it is simplified level by level, where no rule applies to it, except the documented limits of §13.12.

### 13.8 Complexity and budgets

- Phase A: one analysis per canonicalization (hashes reused across levels through C3a), bucketed grouping, linear expected.
- Phase B: per round, the factors are indexed once (one structural visit each, so the rounds are bounded by the structural
  budget); a round removes at least one term; the residues are canonicalized recursively over fewer factors.
- A factorable chain is prepared once per term and canonicalized once: repeated chains no longer finalize each level
  (`tail 512`, §13.10). The admission walk is iterative, as the result's final test: a 100,000-term chain is simplified on a
  256 KiB thread. Budgets are never bypassed: every walk consumes its structural visits.
- The caches P3/P9 and the probes of `FactorEqualityProbe` are untouched.

### 13.9 Validation

- **Unit tests** (`ExpressionSimplifierGlobalFactoringTests`, 20 tests): every example of the mission with its values and its
  second pass (§2.1 to §2.4 of the brief), several common factors and multiplicities (an integral `x*x*y + x*y*y`), indivisible
  powers, the mixed chain in three orders and associations, a deterministic choice over the six orders of `a*b + a*c + b*c`,
  numeric semantics (floating-point sums in source order, overflow to an infinity, `NaN`, `-0f + -0f`, integral and unsigned
  wrapping, `-1 + y`, unsigned `0 - 5`), decimal and checked chains untouched, observable calls never merged (two calls of an
  unknown method stay two evaluations, in order), the refused shapes (logarithms, `sin*sin`, powers of calls, sums as
  factors, divisions, constant pairs, infinite constants) and the rules that still apply on them (`sin² + cos² = 1`, the binary
  factoring of a sub-sum), every policy, derived simplifiers, 300 random factorable chains (values and second pass), shared
  sub-chains against their copies, linear work on repeated chains, the session root after an exception, a budget refusal and a
  nested operation, and a deep refusal walked no more than by the linear test.
- **Security tests**: the factorable admission on extension nodes, impersonating call methods and impersonating operators
  (never read, inside an operation), the shared DAG of `2^50` paths (refused in 150 structural visits), a 100,000-term chain on
  a 256 KiB thread, the C1 known refusals with the two walks.
- **Existing tests** whose premise changed were updated with their reason (merged monomials, canonical products, chains
  canonicalized at once, S5A-05/06/07/09 fixed at the first pass, S5A-08/12 no longer super-polynomial, budget vehicles made
  larger); the C1/C2/C3b family tests run with the factorable family switched off against a level by level reference that
  merges monomials too, so the families it supersedes stay validated.
- Suites: Unit 14,707 passed (1 skipped), Security 392 (3 skipped), Functional 383, ETC Debug (`Coverage`) 350 (1 skipped).

**S5 campaigns** (master `2c3f96b9` against the branch, default policy):

| Campaign | master | C3c | Classification |
|---|---|---|---|
| `comparer` | `Equals(a,b) == Equals(S(a),S(b))`: 11 violations; 5 exceptions | 0 violations; 4 exceptions | new canonicalization (the 11 pairs are now identified); the exceptions are integral zero divisors the simplifier proves, thrown like the source |
| `canon` | `{x, x, y}`: 3 forms; `{x*y, y*z, 2}`: 4 forms | 1 form; 2 (free) / 1 (lambda) | new canonicalization |
| `converge` (70,420 cases) | `fixed@1` 52,416; cycles 1,685; no fixed point 1 | `fixed@1` 63,399; cycles 100; no fixed point 0 | new factoring and canonicalization; main residue `0 - c` (6,726 cases) gone |
| `diff` | VALUE 1 (pre-existing); no exception added, removed or changed | same; moves between `same`, `rounding`, `signedZero` and `extreme` (float `extreme` 205 → 215), one more `SIMPLIFY-THROWS-LIKE-SOURCE` for `int` and for `uint` | symbolic-equivalence latitude (reassociation, factoring at extreme values) |
| `effects` | 1 VALUE, user-code scenarios as expected | identical | unchanged |
| `s3` | `x*y + x*z` → `(y + z) * x` | `x * (y + z)` | canonical product, same value classes |
| `terminate` | 975 pre-existing `AdditionOfCos2andSin2Number` exceptions (S5A-01); largest visit count 241 | identical classes; 218 | unchanged |

**Differential corpus** (the 40,000 random chains of C3b under the default policy; 15,000 for each explicit policy and for the
derived simplifier). Each branch result is compared with master and evaluated against its source on twelve samples: no value
differs beyond the floating-point rounding of a reassociation, and no exception differs. Default policy: 6,965 results
identical to master, 22,367 new forms of identical values, 10,662 within rounding, 6 cases master refused for its budget now
simplified. Not a fixed point after one pass: 27,931 cases on master, 6,190 on the branch. The explicit policies give
identical classes (their operands are inert). The derived simplifier keeps its results but in 4 cases, where its rules compare
operands through the comparer, which simplifies with the built-in type.

### 13.10 Deterministic counters

Transformation and structural visits (bisection of the public limits), master against C3c:

| Case | tv master | tv C3c | sv master | sv C3c |
|---|---:|---:|---:|---:|
| `x+x+x` | 11 | 4 | 11 | 23 |
| `repeated 512` | 515 | 514 | 15,777 | 17,174 |
| `coef 128` | 647 | 646 | 5,127 | 4,634 |
| `tail 48` | 73 | 61 | 4,542 | 1,222 |
| `tail 512` | 769 | 641 | 981,738 | 18,838 |
| `twice 128` | 193 | 129 | 49,280 | 2,359 |
| `cancel 128` | 193 | 161 | 55,220 | 2,164 |
| `rightRep 128` | 131 | 130 | 2,807 | 3,184 |
| `xxx 16` | 76 | 17 | 102 | 88 |
| `linear 200` | 201 | 201 | 2,974 | 2,974 |
| `sinMid 16` | 1,435 | 19 | 10,259 | 261 |
| `mixed 8` (the mission chain) | 389 | 31 | 2,086 | 244 |
| `mixed 32` | > 1.5 s | 121 | > 1.5 s | 1,245 |
| `mixed 128` | did not end | 481 | — | 5,356 |
| `partial 128` | 4,997 | 641 | 48,052 | 3,723 |
| `cross 32` | 20,323 | 161 | 498,878 | 1,761 |
| `cross 128` | > 1.5 s | 641 | > 1.5 s | 7,501 |

The quadratic growth of the repeated chains is gone (`tail 512`: −98 % structural visits). The repeated chains whose spine
treatment was already linear cost a few more structural visits (`repeated 512` +9 %, `rightRep 128` +13 %): the factorable walk
after the linear one and the analysis of the terms; the very short ones too (`x+x+x`: +12 structural visits, 7 transformation
visits fewer).


### 13.11 BenchmarkDotNet

BenchmarkDotNet 0.14.0, in process, 8 warmup and 20 measured iterations, machine idle (no `llama-server`), four processes:
two of the branch, two of master (master run again after its first two processes stopped on `mixed 32`, 28.8 s per
operation, which the in-process toolchain refuses). The two master processes differ by up to 2x on some cases (process-level
noise already seen in C1 to C3b); the branch is compared with the **faster** master process, the conservative choice.

| Case | master (µs) | C3c (µs) | Time | Allocated master | Allocated C3c |
|---|---:|---:|---:|---:|---:|
| `a+b` | 0.95 | 0.93 | −2.8 % | 2.47 KB | 2.47 KB |
| `x+x+x` | 3.88 | 2.15 | −44.5 % | 1.54 KB | 3.65 KB |
| `repeated 16` | 11.47 | 10.02 | −12.7 % | 27.54 KB | 24.57 KB |
| `repeated 48` | 32.46 | 27.97 | −13.8 % | 84.53 KB | 69.87 KB |
| `repeated 512` | 387.99 | 310.37 | −20.0 % | 867.73 KB | 735.55 KB |
| `coef 24` | 78.00 | 86.95 | **+11.5 %** | 107.89 KB | 120.02 KB |
| `coef 128` | 407.04 | 437.81 | **+7.6 %** | 548.59 KB | 595.89 KB |
| `tail 48` | 281.72 | 41.45 | −85.3 % | 313.98 KB | 96.74 KB |
| `tail 512` | 35,805.92 | 467.08 | −98.7 % | 27,710.26 KB | 987.33 KB |
| `twice 48` | 330.80 | 36.09 | −89.1 % | 360.20 KB | 73.06 KB |
| `cancel 128` | 997.60 | 64.50 | −93.5 % | 1,526.49 KB | 163.89 KB |
| `rightRep 128` | 79.00 | 66.63 | −15.7 % | 192.11 KB | 166.28 KB |
| `xxx 16` | 25.83 | 5.01 | −80.6 % | 8.78 KB | 8.36 KB |
| `linear 200` | 62.28 | 61.34 | −1.5 % | 121.91 KB | 121.91 KB |
| `sinMid 16` | 2,283.35 | 10.63 | −99.5 % | 2,630.71 KB | 24.70 KB |
| `mixed 8` (the mission chain) | 414.42 | 25.87 | −93.8 % | 404.70 KB | 29.39 KB |
| `mixed 32` | 28.8 s | 106.99 | −99.9996 % | — | 117.31 KB |
| `partial 32` | 245.73 | 72.48 | −70.5 % | 296.11 KB | 108.27 KB |
| `cross 32` | 38,945.33 | 79.12 | −99.8 % | 32,932.08 KB | 130.56 KB |

The regressions are measured and explained: `coef n` (distinct coefficients and one repeated coefficient, a spine chain in
C3b) now goes through the analysis of the terms and one factoring round (the repeated coefficient `2` is factored out,
`2*(x0 + xn)`, which the spine treatment kept apart), +8 to +12 % time and allocations; `x+x+x` allocates the analysis arrays
(+2.1 KB) while running 45 % faster. The linear chains are unchanged (factoring mode `None`).
### 13.12 Behaviors deliberately unchanged, residual limits

- Phase B only factors the factors present in the terms: it never recognizes a sum as a factor of another term, which is why a
  term holding a sum refuses the chain (`x + y + a*(x + y)` keeps the levels, whose binary rule gives `(1 + a)*(x + y)`); powers
  are never split (`x^2 + x*y` stays).
- A coefficient produced by the binary factoring on a non-factorable chain was still left for the next pass
  (`x*y + (1 - x)*y` gave `y*(x + (1 + (0 - x)))`, S5 P14); since mission C3d it is canonical and the product is `y` (§14.3).
- A shared sub-chain beyond the revisit allowance, a chain holding a non-inert call, a division, a logarithm or a constant pair
  keeps the level by level treatment (with phase A at each level).
- Derived simplifiers keep their historical results; the factorable family and phase A are reserved to the built-in type.
- Free parameters of one type still tie in the canonical order (S4): a sum of free parameters may keep an order that depends on
  the source; bound parameters order by their scope.

## 14. Stabilization, idempotence and performance (mission C3d)

Mission C3d starts from master `b7ab5917` (C3c merged) and develops no new factoring family. Its goal is that one public call
returns the tree a second call returns, wherever the existing rules allow it without changing any permitted behavior, and that
the allocations C3c added go down. The baseline is a separate worktree of `b7ab5917`; every figure below compares the branch
with it, never the branch with itself.

### 14.1 Audit tool: the `stability` campaign (C3d.1)

`tests/ExpressionSimplifierAudit` gains the command `stability` (`Campaigns.Stability.cs`). For every case:

1. `E`, `S(E)`, `S(S(E))`, … are computed up to a fixed point, a cycle, an error or eight passes; forms are compared by the
   strict structural signature of the harness (`StrictForm`), never by a hash.
2. When one pass does not stabilize, the smallest subtree of `S(E)` that the second pass rewrites is located by a parallel walk
   of both trees (the *residue*), and the second pass is traced: the first rule whose output differs structurally from its
   input is the *responsible rule*.
3. The *family* of the case is the responsible rule and the shapes of the residue before and after, leaves abstracted
   (parameters renamed by first occurrence, constants other than `0`, `1`, `-1`, `2` printed `c`).
4. The first cases of each family are reduced: a node is replaced by a child, a parameter or the constant `0`, `1` or `2` of its
   own type, while the case keeps its kind (cycle or not stable) and its responsible rule. Every candidate is rebuilt by the
   framework factories, so the reduction never changes a type or an operator.

`Campaigns.RunStability` takes any case source and simplifier, so the external harness ran it on the differential corpus of
C3b/C3c (40,000 cases, default policy; 15,000 per explicit policy and for a derived simplifier). On `b7ab5917` it reproduces
the documented baseline exactly: 63,399 cases stable after one pass and 100 cycles on `converge`, 6,190 non-idempotent cases on
the corpus.

### 14.2 Families and root causes (baseline)

`converge` (3,507 cases not stable after one pass, 100 cycles) and the corpus (6,190 cases, 598 cycles), by responsible rule:

| Family (residue → second pass) | converge | corpus | Cause | Category of the brief | Decision |
|---|---:|---:|---|---|---|
| `(c - a) - t` ↔ `c + ((0 - a) + (0 - t))` (period 2) | 989 (64 cycles) | 3,580 (565 cycles) | `SubstractionWithAddition`/`SubstractionWithSubstraction` return the nodes they build untransformed; the next call canonicalizes them, and a canonical chain refused by the admission is rewritten back by the sign rules | rebuilt subtraction, binary rules × Flatten | corrected (F1) |
| `(c1 - c2) - a` | 889 | 2,545 (unsigned) | same unfinished reassociation (an unfolded constant pair); for an unsigned type the canonical `(0 - c) + t` | unfinished coefficient, numeric type | corrected (F1, F6) |
| `0 - t`, `(0 - a) + (0 - b)` | ≈ 210 | — | an all-subtracted canonical sum written `0 - t`, which `SubstractionWithZero` rewrites | rebuilt negation | corrected (F2) |
| `t / 1`, `(-a) / b`, `t / -1` | 1,075 | — | `DivisionOfDivision` returns its division untransformed | specialized rule (not C3) | corrected (F3), local |
| `c * (c * a)`, `c * -a`, `1 * a`, `-1 * a` | ≈ 230 | — | the canonical product only orders its factors; the commutation and the constant combination return their product untransformed | multiplicative canonicalization | corrected (F4) |
| `y * (x + (1 + (0 - x)))`, `y * (1 + (y - z))`, `1 * -2` | ≈ 140 | 23 | the coefficient of the binary factoring is built raw (`BuildFactoredProduct`) | unfinished coefficient | corrected (F5) |
| `(0 - a) + ((0 - b) + R)` ↔ `(0 - b) + ((0 - a) + R)` | 16 | 304 (256 cycles) | two free parameters of one type tie in the canonical order (S4) and keep their collection order, which the level path changes | additive reorganization | **kept, decision required** (§14.7) |
| `a + a*b → a*(1 + b)` at the second call | ≈ 100 | ≈ 470 | the factorable admission depends on the source shape (a constant pair such as `2 + 0`, a sum factor); the second call admits the chain and factors it | admission × Flatten | deferred (§14.7) |
| `x / (NaN * y) → NaN`, `-(NaN / x)` | ≈ 120 | — | the policy-dependent `NaN` fold of the next preparation (S5 P18 C2b) | particular numeric type, policy | kept |
| `x * (y * y) → x * y²`, `2 * 2^x → 2^(1 + x)`, `(1/x) + (1/x)` | ≈ 25 | — | `MultiplicationOfEqualsElements`, `PowerByZeroOrOne` after a canonicalization | specialized rule (not C3) | deferred |
| `x^x + x^y` throws `NullReferenceException` | 978 errors | — | `AdditionOfCos2andSin2Number` dereferences a non-constant exponent (pre-existing, S5A-01) | not C3 | deferred, separate fix |

### 14.3 Corrections (C3d.2)

All the corrections apply to the built-in type only (`StabilizesRebuiltForms`, which follows `MergesMonomials`, so that the
level by level references of the tests match the built-in type); derived simplifiers keep the historical rules.

- **F1, finished reassociations.** `SubstractionWithAddition` and `SubstractionWithSubstraction` transform the two nodes they
  rebuild from their prepared operands (`FinishReassociation`, through `TransformCoreWithPreparedOperands`: rules and
  finalization, one transformation visit each, no second preparation). One finishing step, not a loop.
- **F2, negated sums.** A canonical sum whose inert terms are all subtracted, in a type with an ordinary negation, is the
  negation of their sum (`IsNegatedSum`): the form `SubstractionWithZero` and `SubstractionWithNegate` give.
- **F3, finished nested divisions.** `DivisionOfDivision` transforms the division it builds (`FinishDivision`).
- **F4, normalized products.** The canonical product moves the ordinary negations of its inert factors out and multiplies its
  inert constant factors once (`NormalizeProductFactors`; a resulting `1` is dropped, `-1` becomes the sign, `0` is kept); the
  commutation `t * c → c * t` and the two constant combinations transform the product they build when it is one the next
  call rewrote (`FinishProduct`).
- **F5, canonical coefficients.** The coefficient of a binary factoring is the canonical sum of the terms of both coefficients
  (`CanonicalizeFactoredCoefficient`), negated as a whole when all its terms are subtracted (the sign the levels and the global
  factoring give such a group); a constant coefficient of a constant factor is folded. Only a coefficient that holds a
  subtraction, a negation or two constants is canonicalized (`HasAdditiveNegation`, `CountAdditiveConstants`, both without
  allocation): a sum of added terms keeps the historical node, since canonicalizing a coefficient that grows by one term per
  level made the `dag` family quadratic in allocations (§14.8).
- **F6, unsigned subtracted constants.** The subtracted constant of an unsigned canonical sum is folded to its modular value
  (`p - 9ul` gives `18446744073709551607 + p`), as `SubstractionOfConstants` folded `(0 - 9) + p` at the next call. This
  reverses a C3c choice (which kept `0 - c` for readability, a form no call ever left alone).

Guards found by the test suites and kept:

- The finishing re-entries run only on inert, finite arithmetic (`FinishesRebuilt`, `IsFinishableOperand`): on an opaque
  constant, a call, a member access or a decimal, the re-entered rules compared operands and added user `Equals` calls and
  comparer simplifications the S5 P15/P16 tests pin; on a `NaN` or an infinity they anticipated the policy-dependent `NaN` fold.
  Those cases keep the nodes as built.
- Decimal sums and products are never reassociated (their operators throw on overflow).
- `FinishProduct` finishes only the shapes the next call rewrote; finishing a plain `c * t` re-ran the comparing rules.

### 14.4 Representation invariants

1. **Factors**: inert factors in canonical key order, pinned factors after them in source order; numeric constant factors of
   one type folded into one, first; ordinary negations moved out of the product (`-(c * a)`, the fixed point of
   `MultiplicationWithNegate`).
2. **Terms**: inert terms in canonical order (ties of free parameters keep their collection order, S4), pinned terms after
   them; right-associated.
3. **Numeric coefficient**: on the left of the product (`c * a`).
4. **Implicit coefficients**: `1` never written; `-1` written as a negation or a subtracted term.
5. **Zero coefficients**: the term is dropped; a zero constant factor is kept until `CanDropOperand` allows the rules to drop the
   other factors.
6. **Negations**: a subtracted term inside a sum is `0 - t`; a sum whose terms are all subtracted is `-(sum)`; a product is
   `-(product)`.
7. **Subtractions**: a subtracted constant is folded into a negative (signed types) or modular (unsigned types) constant.
8. **Factored products**: the canonical product of the factor and of the canonical coefficient.

No result is promoted to `CanonicalizationState.Canonical`: the fixed point is measured, not proven, and the residual families of
§14.7 show it does not hold everywhere.

### 14.5 Optimizations (C3d.3)

- **O1.** `FactorCommonFactors` builds its monomials from the `AdditiveTermAnalysis` that `MergeIdenticalMonomials` computed for
  the same terms (`FactoringMonomial.FromAnalysis`); only the merged terms are decomposed again.
- **O3.** `GroupByFactors` chains the representatives of a hash bucket through an index array instead of one list per bucket.
- Not done: a dedicated path for very small sums (`x+x+x` keeps its 3.64 KB, the analysis arrays): the measured gain did not
  justify a second merging path. The repeated per-round dictionaries of `FindBestCandidate` stay (one round in every measured
  family but `wide`, §14.8).

### 14.6 Validation

**Suites**: Unit 14,717 passed (1 skipped), Security 392 (3 skipped), Functional 383, ETC Debug (`Coverage`) green. New:
`ExpressionSimplifierStabilityTests` (10 tests: each corrected family reaches a structural fixed point in one call under every
policy, with its values; derived simplifier unchanged; decimal product not reassociated; unsigned fold; budget refusal).
Updated with their reason: the unsigned forms (F6), S5A-05 (the multiplicative cases now finish in one pass, and the comparer
identifies them with their result), AUD-03 and P14 (stable in one pass), `negatedChainCancels` (distinct parameters of the same
names: one canonical chain), the comparer-batching scenarios (the built-in type builds the canonical coefficient itself; the
probe path is proven with a derived simplifier), and `NestedProductsByZero_StayBounded` (the structural work is now linear in
the depth: 398 then 798 visits, instead of quadratic).

**S5 campaigns** (`b7ab5917` against the branch, default policy):

| Campaign | b7ab5917 | C3d | Classification |
|---|---|---|---|
| `converge` (70,420) | `fixed@1` 63,399; cycles 100 | `fixed@1` 66,552; cycles 54 | stabilization; the remaining cycles hold a call (`Log`) or a non-finite constant, or are free-parameter ties |
| `comparer` | `Equals(a,b) == Equals(S(a),S(b))`: 0 violations; 4 exceptions | 0 violations; 1 exception | 3 integral zero divisors the first pass now proves (§14.6 below); the remaining one is the `AdditionOfCos2andSin2Number` defect |
| `canon` | identical | identical | — |
| `diff` | no exception added, removed or changed | same; only the `double` and `float` rows move, by at most six cases, between latitude classes (`same`, `rounding`, `signedZero`, `extreme`); every exception column is unchanged | symbolic-equivalence latitude |
| `effects` | identical | identical | — |
| `s3` | `x / (1/x)` gives `(x ** 2) / 1` | `x ** 2` | stabilization, same value classes |
| `terminate` | 975 exceptions (S5A-01); largest visit count 218 | 974; 382 | the finishing re-entries; well within the budgets |

**Exception difference explained.** On `b7ab5917` the first pass left an integral zero divisor unproved
(`1 / (x - (x - (0 - 2)))`, that is `1 / -2`, `0` for `int`), and the comparer's own simplification of that result threw. The
first pass now finishes the reassociation and proves the divisor, so `Simplify` throws `DivideByZeroException` one call
earlier, as `DivideWithZeroOrOne` already does on a proven integral zero divisor and as the source does at every evaluation.

**Differential corpus.** Every branch result is compared with `b7ab5917` and evaluated against its source on twelve samples:

| Policy | same | new form, same values | rounding | value or exception difference | non-idempotent b7ab5917 → C3d |
|---|---:|---:|---:|---:|---:|
| default (40,000) | 34,353 | 4,189 | 1,458 | **0** | 6,190 → 782 |
| each explicit policy (15,000) | 12,865 | 1,590 | 545 | **0** | 2,347 → 322 |
| derived simplifier (15,000) | 15,000 | 0 | 0 | **0** | unchanged |

The explicit policies give identical classes (the operands are inert; the four policies were identical before the last change of F5, and `TrustDeclarations` was measured again after it); the derived simplifier keeps its results exactly.

### 14.7 Residual cycles and non-idempotence, and why

- **Free-parameter ties (304 corpus cases, 256 cycles; 16 converge cycles).** Two distinct free parameters of one type have no
  canonical order (S4, documented in `ExpressionCanonicalOrder`); the stable sort keeps their collection order, and a second
  call that takes the level path collects them in another order. Ordering them by name would remove the cycles but would invent
  an order the S4 design deliberately refuses; this is left to a decision.
- **Admission-dependent factoring (≈ 470 corpus cases).** A constant pair in the source (`(p0*p1) + (p0 + (2 + 0))`) or a term
  with a sum factor refuses the factorable family, so phase B does not run; the second call sees `2 + (p0 + p0*p1)`, admits it
  and factors `p0`. Admitting constant pairs would revisit the exactness decisions of C3c (signed zeros).
- **Finishing left out on purpose.** Calls (`Log`, `Sin`), opaque constants, decimals and non-finite constants keep their
  nodes as built (§14.3); the `NaN` family stays with the policy.
- **Specialized rules** (`MultiplicationOfEqualsElements`, the reciprocal powers): outside C3, deferred.
- **`AdditionOfCos2andSin2Number`** throws `NullReferenceException` on `x^x + x^y` (978 `converge` errors, pre-existing): a
  one-line guard, deferred to a separate fix.
- **Pre-existing explosion outside C3**: a subtraction chain of division terms (`1 - x1/x0 - x2/x0 - …`, never admitted)
  costs 7.7 ms at 16 terms and 1.4 s at 64 on `b7ab5917` as on the branch (§14.9).

The simplifier is therefore **not** globally idempotent or canonical.

### 14.8 Growth of the partial factorings (phase E)

Harness mode `growth` (one process per binary, CPU affinity fixed, machine idle): families of 8 to 512 terms; time (median of
seven rounds), allocations, structural visits (bisection of the public limits), the factoring counters and the size of the
result. Families: `shared` (`a_i * x`), `none` (`a_i * b_i`), `crossed` (`x*a_i + y*a_i`), `ties` (`a_i * a_(i+1)`, every
factor in two terms), `repeated` (`(a_i * a_i) * x`), `wide` (six factors per term, one shared), `collide` (free parameters of
one type hash alike), `dag` (one shared instance of `(u + v)` as a factor of every term), `coefshared` (constant coefficients).

| Family, 512 terms | b7ab5917 µs | C3d µs | b7ab5917 KB | C3d KB | structural visits (both) | rounds | factor comparisons | result nodes (both) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `shared` | 1,102 | 988 | 1,888 | 1,667 | 26,055 | 2 | 2,560 | 1,025 |
| `none` | 744 | 792 | 905 | 905 | 32,038 | 0 | 0 | 2,047 |
| `crossed` | 1,192 | 1,107 | 1,886 | 1,669 | 31,973 | 6 | 3,086 | 515 |
| `ties` | 20,439 | 20,028 | 60,933 | 60,748 | 1,280,854 | 513 | 394,496 | 1,535 |
| `repeated` | 4,778 | 4,699 | 3,697 | 3,475 | 65,826 | 2 | 2,048 | 2,049 |
| `wide` | 17,962 | 17,769 | 15,760 | 15,539 | 177,168 | 4 | 6,800 | 4,003 |
| `collide` | 21,682 | 19,561 | 60,472 | 60,288 | 1,283,552 | 507 | 391,403 | 1,541 |
| `dag` | 28,352 | 34,390 | 19,511 | 19,511 ¹ | 682,747 | 0 | 0 | 1,027 |
| `coefshared` | 1,363 | 1,426 | 1,744 | 1,698 | 14,276 | 1 | 257 | 1,027 |

¹ Before the restriction of F5 (§14.3) the branch took 73,199 µs, 111,805 KB and 2,664,361 structural visits on `dag 512`
(a canonicalization of the growing coefficient at every level); after it the structural visits are those of `b7ab5917` at every
size, and the remaining time difference (+7 % at 64 terms, +21 % at 512) is the allocation-free scan of the coefficient.

- Every family keeps the structural visits and the result of `b7ab5917` at every size (the corrections of C3d do not apply to
  these sums of products), and the allocations go down by up to 12 % (O1, O3).
- **Linear**: `shared`, `none`, `crossed`, `repeated`, `wide`, `coefshared`: a constant number of rounds (one per shared
  candidate), comparisons linear in the factors.
- **Quadratic, unchanged from C3c**: `ties` and `collide`, where every factor is shared by exactly two terms: each round factors
  one pair and indexes every factor again, so the rounds grow with `n` and the comparisons as `1.5 n²` (394,496 at 512). Every
  indexed factor consumes one structural visit, so the work stays under the budget (the default limits refuse such a chain
  instead of running away). An incremental index (updating the candidate counts of the terms a round removes instead of
  re-indexing) would make it linear; it is a change of the phase B algorithm, left to a later mission.
- **Quadratic, level by level (not admitted)**: `dag` (its factor is a sum, which the factorable admission refuses): the binary
  factoring rebuilds the product at every level, as on `b7ab5917`.

### 14.9 Measures

**BenchmarkDotNet** 0.14.0, in process, 8 warmup and 20 measured iterations, four processes run one after the other with the same
CPU affinity (performance cores) on an idle machine: `b7ab5917`, branch, `b7ab5917`, branch. The two processes of one binary
differ by less than 4 %; the table compares the faster process of each side (before the last restriction of F5, which only
concerns the binary factoring of non-admitted chains and changes none of these cases' visits).

| Case | b7ab5917 µs | C3d µs | Time | b7ab5917 KB | C3d KB | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| `a+b` | 0.91 | 0.92 | +0.8 % | 2.47 | 2.47 | 0.0 % |
| `x+x+x` | 2.12 | 2.12 | 0.0 % | 3.65 | 3.64 | −0.3 % |
| `repeated 16` | 10.00 | 9.57 | −4.3 % | 24.57 | 21.96 | −10.6 % |
| `repeated 48` | 27.59 | 26.12 | −5.3 % | 69.87 | 60.03 | −14.1 % |
| `repeated 512` | 306.27 | 292.27 | −4.6 % | 735.55 | 627.03 | −14.8 % |
| `coef 8` | 33.07 | 33.21 | +0.4 % | 46.85 | 44.66 | −4.7 % |
| `coef 24` | 87.21 | 86.31 | −1.0 % | 120.02 | 114.81 | −4.3 % |
| `coef 128` | 437.81 | 437.02 | −0.2 % | 595.89 | 569.71 | −4.4 % |
| `tail 48` | 40.15 | 39.76 | −1.0 % | 96.74 | 85.94 | −11.2 % |
| `tail 512` | 460.89 | 443.17 | −3.8 % | 987.33 | 869.66 | −11.9 % |
| `twice 48` | 36.05 | 34.52 | −4.2 % | 73.06 | 66.45 | −9.0 % |
| `cancel 128` | 64.99 | 60.97 | −6.2 % | 163.89 | 142.30 | −13.2 % |
| `linear 200` | 60.48 | 61.86 | +2.3 % | 121.91 | 121.91 | 0.0 % |
| `sinMid 16` | 10.50 | 10.11 | −3.7 % | 24.70 | 21.76 | −11.9 % |
| `mixed 8` | 25.62 | 25.50 | −0.5 % | 29.39 | 28.44 | −3.2 % |
| `mixed 32` | 106.49 | 105.28 | −1.1 % | 117.31 | 110.03 | −6.2 % |
| `partial 32` | 70.58 | 71.59 | +1.4 % | 108.27 | 103.07 | −4.8 % |
| `cross 32` | 76.14 | 74.68 | −1.9 % | 130.56 | 118.89 | −8.9 % |
| `cycReassoc` (`(1 - y) - x/z`) | 8.55 | 8.48 | −0.8 % | 12.13 | 11.96 | −1.4 % |
| `cycConstPair` (`2 - (x - 0.5)`) | 2.29 | 2.28 | −0.3 % | 3.82 | 3.82 | 0.0 % |
| `cycNestedDiv` (`x / (1/x)`) | 3.09 | 3.42 | **+10.6 %** | 2.81 | 2.86 | +1.8 % |
| `cycProduct` (`(x * -2) * -2`) | 1.00 | 1.66 | **+65.6 %** | 1.58 | 2.73 | +72.8 % |
| `cycCoefficient` (`x*y + (1 - x)*y`) | 10.88 | 10.61 | −2.5 % | 11.51 | 12.93 | +12.3 % |
| `subChain 16` (`1 - x1/x0 - … - x16/x0`) | 7,670 | 8,498 | **+10.8 %** | 7,214 | 7,423 | +2.9 % |
| `subChain 64` | 1,405,723 | 1,361,545 | −3.1 % | 1,148,802 | 1,024,074 | −10.9 % |

- The families C3c optimized keep their gains; their allocations go down by 4 to 15 % (O1, O3) and their time by up to 6 %.
- The three regressions are the price of the finishing step on tiny or level by level trees, and each is justified: `cycProduct`
  reaches `4 * x` in one call where `b7ab5917` needed two (`-2 * (-2 * x)` first, 2.00 µs for both calls), likewise
  `cycNestedDiv` (`x ** 2` instead of `(x ** 2) / 1`); `subChain 16` runs the finishing re-entries and their operand scans at
  every level of a chain that the admission refuses (+3 % transformation visits, +9 % structural visits).
- `x+x+x` keeps its allocations (§14.5). `subChain 64` shows a pre-existing super-linear cost of the non-admitted subtraction
  chains of divisions (7.7 ms at 16 terms, 1.4 s at 64, on both binaries): outside the C3 families, to be studied separately.

**Deterministic counters** (harness mode `counters`, transformation and structural visits by bisection of the public limits):
identical to `b7ab5917` on every scenario of the mission (`a+b` to `cross 32`, results identical as well); `cycNestedDiv` and
`cycProduct` take one more transformation visit (the finishing step) and reach their second-pass result; `cycCoefficient` takes
five structural visits fewer (57 → 52); `subChain 16` takes 6,934 transformation visits instead of 6,724 and 73,776 structural
visits instead of 67,715.

### 14.10 Possible next steps

- A decision on the free-parameter ties (§14.7): order them by name as a last resort, or keep the S4 policy.
- An incremental candidate index for phase B (§14.8, `ties`/`collide`).
- The admission of constant pairs and of sum factors by the factorable family, with their exactness proofs.
- The `AdditionOfCos2andSin2Number` guard and the cost of the non-admitted division chains (done in §15).

## 15. Trigonometric rule and quotient chains

Two pre-existing defects of `ExpressionSimplifier`, found by the S5 campaigns and deferred by C3d (§14.7, §14.10): the
`NullReferenceException` of `AdditionOfCos2andSin2Number` on powers with a symbolic exponent (S5A-01), and the cost of the
additive chains of quotients `1 - x1/x0 - x2/x0 - … - xn/x0` (`subChain`, §14.9). The baseline is a separate worktree of the
diagnostic commit `d9805c58` (master `ad0a65c4` plus opt-in counters and the reproduction tests); every figure compares the
branch with it.

### 15.1 Reproduction (diagnostic commit)

- `x^x + x^y`, `x^2 + x^y`, `x^y + x^2`, `x^(x+1) + y^2`, `x^sin(y) + x^2`, `sin(x)^y + cos(x)^2` threw
  `NullReferenceException` from `Simplify` and from the comparer (`ExpressionSimplifierTrigonometricRuleTests`,
  `ExpressionSimplifierDeepAuditTests.S5A01_…`).
- The quotient and product rules (`DivisionOfCosAndSinNumber`, `DivisionOfSinAndCosNumber`, `MultiplicationOfCosAndTanNumber`,
  `MultiplicationOfTanAndCosNumber`, `DivisionOfSinAndTanNumber`) called directly, as a derived simplifier may call them,
  rewrote foreign methods: `DegreeTrigonometry.Sin(x) / DegreeTrigonometry.Cos(x)` (a test type implementing
  `ITrigonometricFunctions<double>` explicitly, with public degree-based `Sin`/`Cos`) became `double.Tan(x)`, and the second
  argument of a two-argument `Sin(x, s) / Cos(x, t)` was dropped. Through the public entry points the effect gate refuses
  every rule on a call of a user method under every policy, so no public result was wrong: the defect was latent.
- `subChain`: opt-in counters in `SimplificationMetadataStatistics` (`ProbeComparisons`, `ProbeFastPathDecisions`,
  `ComparerSimplifications`, `ComparerMemoHits`, `AdditiveCanonicalizations`), and the S5 harness command
  `profile <family> <n>` with the families `subdiv` (left-associated), `subdivR` (right-associated), `subdivB` (a balanced
  group subtracted once), `subdivM` (alternating signs), `subdivD` (distinct denominators) and `subdivL` (`long`).

### 15.2 Root cause of the `NullReferenceException`

`AdditionOfCos2andSin2Number` read `left.Right as ConstantExpression` and then `.Value` without testing the cast: any exponent
that is not a constant (a parameter, a call, a computed expression) dereferenced `null`. The rule is a candidate for every sum
of two ordinary powers, so the defect hit every such sum, and the comparer, which simplifies first.

### 15.3 Correction of the trigonometric rules

- **Shape before value.** The identity checks every shape before reading any value: two ordinary powers of one type, both
  exponents the `double` constant `2` (`IsSquareExponent`), both bases calls of the sine and the cosine of that type with one
  argument each, then the structural identity of the arguments. Any other shape declines (`null`), never throws.
- **Exact function of the type.** `TryGetTrigonometricArgument` (and `TryMatchTrigonometricPair` for the quotients and
  products) accepts a call only when it is exactly the public static `T F(T)` function of the call's own result type, one
  argument, its provenance checked first (`ExpressionReflectionProvenance.IsRuntime`, `IsSameMember`, S5 A-10): an overload of
  another arity, a homonym with other values (a degree-based sine) or an impersonating reflection object never matches.
  `TrigonometricMethods` resolves `null` for a function the type does not declare with that signature and return type (an
  explicit interface implementation), and the rule then declines instead of building a call of `double`.
- Tests: `ExpressionSimplifierTrigonometricRuleTests` (non-constant exponents, other constant exponents, other arguments or
  functions, foreign methods called directly or through a derived simplifier, extension operands, effectful arguments, the
  identities that still apply) and `ExpressionTrigonometricRuleSecurityTests` (impersonating methods declined without being
  read; mixed calls keep the impersonated evaluation). The deep-audit test `S5A01_…_Declines` pins the fix.

### 15.4 Root cause of the cost of the quotient chains

A chain `1 - x1/x0 - … - xn/x0` was refused by the linear family (a division is not a linear term, §12) and simplified level
by level. At every level `SubstractionOfEqualsElements` compares the canonical prefix `P_k` with the new quotient and the
constant; the comparer simplifies `P_k` again, and that simplification rebuilds the prefix level by level through the `0 - t`
re-entries of the sign rules. The work is cubic in `n`, and 97 % of the transformation visits ran inside comparer
simplifications: for 64 quotients, 373,830 transformation visits, 25.8 million structural visits and 345,408 comparer
simplifications (2.4 s).

### 15.5 Correction: linear quotients

A linear chain (`IsLinearChain`, §12) of `double` or `float` may now hold **linear quotients** (`IsLinearQuotient`): an
ordinary division of a parameter by a parameter, of the chain's type. `LinearChainOperands` keeps its proof obligations:

- the numerators are parameters that the chain holds nowhere else (as a term, a factor or another numerator);
- a denominator may divide several quotients (`_denominators`) but is never a term, a factor or a numerator;
- every other condition of the linear family is unchanged (constants, coefficients, crossed nodes, subtracted groups).

Why the result is the level by level result. No rule prepares such a quotient into anything else (no constant, no negation,
no nested division, no call: a field division of two parameters never throws in IEEE-754). Two operands of a level always hold a
parameter the other does not hold (a term, a factor or a numerator), the simplifier never introduces a parameter and never
removes one from such a chain, so the factoring comparisons of `AdditionOfEqualsElements` and `SubstractionOfEqualsElements`
fail at every level and nothing cancels, exactly as for the parameters of a linear chain. The accepted chain is therefore
rebuilt canonically without any comparison: structurally the tree the levels give (pinned by
`ExpressionSimplifierFlattenTests.QuotientChains_AreAccepted_AndGiveTheLevelByLevelResult` against a derived simplifier).

Refused on purpose, and kept level by level (`OtherQuotientChains_AreDeclined_AndKeepTheLevelByLevelResult`): integral quotients
(truncating, throwing on a zero divisor), decimal chains, a quotient whose numerator or denominator is not a parameter, a
denominator that is also a term, a factor or a numerator, a repeated numerator. The spine family (§12.4) keeps its linear terms
only (`IsSpineTerm`): a quotient is never a spine term.

Tests: `ExpressionSimplifierDivisionChainTests` (values in every type and shape, one result under every policy, effectful
chains keep their evaluations, derived simplifiers keep their rules, budgets refuse cleanly at the limit, shared quotient
instances treated once, a 2,000-term chain on a small stack, concurrency and re-entrancy, linear work).

### 15.6 Validation

**Suites**: Unit 14,736 passed (1 skipped), Security 394 (3 skipped), Functional 383, ETC Debug (`Coverage`) 350 (1 skipped).

**S5 campaigns** (`d9805c58` against the branch, default policy, 1,000 seeds and 1,000 nested cases):

| Campaign | d9805c58 | Branch | Classification |
|---|---|---|---|
| `terminate` | 881 `NullReferenceException` (`AdditionOfCos2andSin2Number`); largest visit count 129 | 0 exceptions; 140 | S5A-01 fixed; the 881 cases now run their rules (well within the budgets) |
| `converge` (64,450) | `error@1` 879, `error@2` 92; `fixed@0` 1,640, `fixed@1` 61,672; cycles 15 | no error; `fixed@0` 2,129, `fixed@1` 62,154; cycles 15 | S5A-01 fixed; the cases not stable after one pass are the same 152 |
| `stability` | same categories as `converge` | same as `converge` | one case moves between the families `(finalization)` and `FlattenAdditiveChain` (a quotient chain now admitted) |
| `s3` | `x^y + z^2` throws | `(z ** 2) + (x ** y)` | S5A-01 fixed |
| `comparer`, `diff`, `effects`, `canon` | — | byte-identical | — |

**Differential corpus** (the corpus of C3b–C3d; every branch result compared with `d9805c58`, then evaluated against its source
on twelve samples when it differs). `C3E_QUOTIENTS=1` adds quotient terms (parameter by parameter, parameter by constant) and up
to sixteen parameters per case.

| Corpus | Cases | same | different | non-idempotent d9805c58 → branch |
|---|---:|---:|---:|---:|
| default | 40,000 | 40,000 | **0** | 782 → 782 |
| each explicit policy | 15,000 | 15,000 | **0** | 322 → 322 |
| derived simplifier | 15,000 | 15,000 | **0** | 10,501 → 10,501 |
| quotients, default | 40,000 | 40,000 | **0** | 4,873 → 4,864 |
| quotients, derived simplifier | 15,000 | 15,000 | **0** | 6,012 → 6,008 |

Every first-pass result is identical; the admitted quotient chains only make a few second passes reach the first-pass result.

### 15.7 Growth (`profile`)

S5 harness `profile <family> <n>`, one process per measure, median of the timed runs; a family stops after the first size whose
median exceeds 2 s. Transformation visits are deterministic.

| Family | n | d9805c58 ms | branch ms | d9805c58 transformation visits | branch transformation visits |
|---|---:|---:|---:|---:|---:|
| `subdiv` | 16 | 8.56 | 0.04 | 6,934 | 50 |
| `subdiv` | 64 | 2,388 | 0.16 | 373,830 | 194 |
| `subdiv` | 256 | — | 0.48 | — | 770 |
| `subdivD` | 64 | 2,368 | 0.35 | 373,830 | 194 |
| `subdivM` | 24 | 3,168 | 0.04 | 1,112,766 | 74 |
| `subdivM` | 256 | — | 0.51 | — | 770 |
| `subdivB` | 64 | 2,517 | 393 | 376,046 | 294,472 |
| `subdivB` | 128 | — | 4,182 | — | 2,227,336 |
| `subdivR` | 24 | 2,917 | 1,540 | 907,610 | 811,813 |
| `subdivL` | 128 | 2.61 | 2.39 | 513 | 513 |

- `subdiv`, `subdivD` (distinct denominators) and `subdivM` (alternating signs) are now linear: `3n + 2` transformation
  visits, no comparer simplification.
- `subdivL` (`long`) was already linear: the effect gate refuses the rules on integral divisions, which may throw.
- **Residual limit.** `subdivR` (right-associated, `1 - (x1/x0 - (x2/x0 - …))`) and `subdivB` (a balanced group subtracted
  once) stay super-linear: a subtracted group is not a term of the linear family, so their roots stay level by level and the
  comparer rebuilds the subtracted groups. Admitting them needs the factorable family of C3c with quotient atoms, a change of
  its proof obligations; it is out of the scope of this mission. Their cost goes down a little (−11 % and −22 % visits), as
  inner groups are now admitted as linear chains (one main chain flattened instead of none; at 16 terms 210 and 451 chains
  flattened inside the comparer, admission walks 1,404 → 452 and 4,811 → 2,644).
- Mixed-sign quotient chains keep the free-parameter ties of S4 (§14.7): their result is the level by level result, which is
  not always idempotent either.

### 15.8 Measures

**Reduced validation (user decision).** To save about one hour of machine time, the branch growth profile of `subdivR` stops at
24 terms and BenchmarkDotNet runs a targeted list only: `subChain 16/64` and the quotient variants `subChainM 24`,
`subChainD 64`, `subChainF 64`, plus the C3c/C3d references (`a+b`, `x+x+x`, `repeated 16/48/512`, `coef 24/128`,
`tail 48/512`, `twice 48`, `cancel 128`, `linear 200`, `sinMid 16`, `mixed 8/32`, `partial 32`, `cross 32`) and the harness
`growth` families (`dag`, `ties`, `collide` among them).

**BenchmarkDotNet** 0.14.0, in process, 8 warmup and 20 measured iterations, four processes run one after the other pinned to
the performance cores at high priority: `d9805c58`, branch, `d9805c58`, branch. The table compares the faster process of each
side; the two processes of one binary differ by less than 6 % (9 % for `tail 48` on `d9805c58`).

| Case | d9805c58 µs | branch µs | Time | d9805c58 KB | branch KB | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| `a+b` | 0.90 | 0.89 | −2.1 % | 2.47 | 2.47 | 0.0 % |
| `x+x+x` | 2.07 | 2.07 | +0.2 % | 3.64 | 3.64 | 0.0 % |
| `repeated 16` | 9.51 | 9.27 | −2.6 % | 21.96 | 21.96 | 0.0 % |
| `repeated 48` | 26.72 | 25.53 | −4.4 % | 60.03 | 60.03 | 0.0 % |
| `repeated 512` | 287.21 | 285.94 | −0.4 % | 627.03 | 627.03 | 0.0 % |
| `coef 24` | 84.03 | 84.31 | +0.3 % | 114.81 | 114.81 | 0.0 % |
| `coef 128` | 426.86 | 430.87 | +0.9 % | 569.73 | 569.71 | 0.0 % |
| `tail 48` | 37.93 | 38.58 | +1.7 % | 85.94 | 85.94 | 0.0 % |
| `tail 512` | 438.66 | 448.09 | +2.1 % | 869.66 | 869.66 | 0.0 % |
| `twice 48` | 34.61 | 34.91 | +0.9 % | 66.45 | 66.45 | 0.0 % |
| `cancel 128` | 59.18 | 59.91 | +1.2 % | 142.30 | 142.30 | 0.0 % |
| `linear 200` | 60.24 | 59.29 | −1.6 % | 121.91 | 121.91 | 0.0 % |
| `sinMid 16` | 9.75 | 9.79 | +0.4 % | 21.76 | 21.76 | 0.0 % |
| `mixed 8` | 24.99 | 24.79 | −0.8 % | 28.44 | 28.44 | 0.0 % |
| `mixed 32` | 101.12 | 102.71 | +1.6 % | 110.03 | 110.03 | 0.0 % |
| `partial 32` | 69.83 | 70.01 | +0.3 % | 103.07 | 103.07 | 0.0 % |
| `cross 32` | 73.12 | 73.61 | +0.7 % | 118.89 | 118.89 | 0.0 % |
| `subChain 16` | 8,026.95 | 17.07 | **−99.8 %** | 7,422.96 | 17.83 | −99.8 % |
| `subChain 64` | 1,332,638.65 | 64.47 | **÷ 20,670** | 1,024,072.95 | 65.56 | ÷ 15,620 |
| `subChainM 24` (alternating signs) | 1,882,977.92 | 25.75 | **÷ 73,120** | 1,310,054.66 | 23.20 | ÷ 56,470 |
| `subChainD 64` (distinct denominators) | 1,318,146.36 | 67.02 | **÷ 19,670** | 1,024,072.84 | 68.87 | ÷ 14,870 |
| `subChainF 64` (`float`) | 1,380,861.47 | 73.98 | **÷ 18,670** | 1,024,072.01 | 65.38 | ÷ 15,660 |

- The reference families of C3c/C3d allocate exactly what they did and their times stay within the noise of the two processes
  (−4.4 % to +2.1 %): the new test (`IsLinearQuotient`) runs only on the operands that are not linear terms, by type tests.
- The quotient chains go from seconds to tens of microseconds, linearly in their length.

**Harness `growth` families** (`shared`, `none`, `crossed`, `ties`, `repeated`, `wide`, `collide`, `dag`, `coefshared`, 8 to 512
terms, §14.8): transformation visits, structural visits and result sizes identical to `d9805c58` at every size; allocations
equal up to a few hundred bytes of measurement noise; times within −3 % to +4 % (`dag 512`: 33.6 ms and 34.8 ms).

### 15.9 Residual limits

- Right-associated and balanced subtracted groups of quotients (`subdivR`, `subdivB`) stay super-linear (§15.7).
- Quotients of other shapes (a constant or a product numerator, a sum denominator) and integral or decimal quotient chains keep
  the level by level path, with its cost.
- The free-parameter ties (S4) and the other residual families of §14.7 are unchanged.

## 16. Grouped quotient chains

The residual limit of §15.9: the right-associated and balanced subtracted groups of quotients, `subdivR`
(`1 - (x1/x0 - (x2/x0 - (… - xn/x0)))`) and `subdivB` (`1 - ((x1/x0 + x2/x0) + (x3/x0 + x4/x0))`), stayed super-linear.
The baseline is `master` `ed3ea136` (PR #705), built from a separate worktree; every figure compares the branch with it.

### 16.1 Reproduction and root cause

Diagnostic commit: the S5 harness `profile` reports the admission refusals per cause and the rule candidacies and declinations
per rule, split between the main transformation and the comparer simplifications (opt-in, behavior-neutral), and gains the
families `subdivRD` (distinct denominators), `subdivRF` (`float`), `subdivBM` (balanced, mixed signs) and `subdivBF`.

Baseline growth (`ed3ea136` with the diagnostic commit, one process per point, median of the timed runs; a family stops after
the first size above 3 s, and the right-associated families at 24 quotients, whose next size would take minutes per run):

| Family | n | transformation visits | structural visits | comparer simplifications | median ms |
|---|---:|---:|---:|---:|---:|
| `subdivR` | 4 | 127 | 574 | 44 | 0.14 |
| `subdivR` | 8 | 869 | 5,340 | 316 | 0.94 |
| `subdivR` | 12 | 4,313 | 31,210 | 1,438 | 4.8 |
| `subdivR` | 16 | 22,085 | 189,431 | 7,046 | 27.8 |
| `subdivR` | 24 | 811,813 | 10,188,073 | 247,450 | 1,509 |
| `subdivB` | 8 | 976 | 4,273 | 168 | 1.3 |
| `subdivB` | 16 | 6,040 | 34,384 | 720 | 9.4 |
| `subdivB` | 32 | 40,744 | 344,777 | 2,976 | 34.9 |
| `subdivB` | 64 | 294,472 | 3,616,658 | 12,096 | 392 |
| `subdivBM` | 8 | 512 | 2,870 | 168 | 0.65 |
| `subdivBM` | 16 | 8,320 | 70,732 | 2,640 | 10.2 |
| `subdivBM` | 24 | 293,292 | 3,813,720 | 89,414 | 570 |

`subdivRD` (distinct denominators) and `subdivRF` (`float`) have exactly the counters of `subdivR` at every size (1.6 s at 24).

At 8 quotients 748 of the 869 transformation visits of `subdivR` run inside comparer simplifications, and at 12 quotients every
division rule is a candidate 990 times there (each quotient is transformed again about 80 times). The mechanism:

1. The linear family refuses the root at once (a subtraction with a crossed right operand, `SubtractedGroup`, spine index 0),
   and so does the factorable family (a quotient is not a factorable term, §13.4): the root is simplified level by level.
   Each inner sub-chain without a subtracted group is admitted as a linear chain (§15.5); the admission walks themselves are
   cheap (one or two nodes each).
2. Every level `A - B` whose prepared `B` is a canonical sum is reassociated (`SubstractionWithAddition`,
   `SubstractionWithSubstraction`, finished by `FinishReassociation`), which transforms the rebuilt nodes again; the canonical
   form `(0 - q) + R` then goes through `SubstractionWithZero(0, t)` and `AdditionWithNegate`, whose comparison
   (`EqualsKeepingSimplifiedForms`) simplifies both operands through the comparer.
3. The canonical sums the comparer simplifies hold the symbolic zeros of `0 - q`: a zero is not a linear term, so they are
   refused as linear chains (`NonLinearTerm`, 378 refusals at 12 quotients) and simplified level by level again, with the same
   re-entries one comparison deeper. The memo of S5 P9 answers most repeated comparisons (1,294 hits for 1,438
   simplifications at 12), but each level still simplifies a canonical prefix of growing length.

The admission refusals, the walks of `IsLinearChain` and `IsFactorableChain` and `SubstractionOfEqualsElements` are not the
cost: the cost is the repeated comparison-driven simplification of already prepared, already canonical groups.

### 16.2 Reference characterization

Before any production change, `ExpressionSimplifierGroupedQuotientReferenceTests` pins, with literal expectations that pass on
`ed3ea136`, the result of one and two calls of the built-in simplifier on `subdivR`, `subdivB`, `subdivBM`, `subdivRD`,
`subdivRF` and the `long` variant under the six policies of the brief (identical under all of them), the values on finite,
zero, negative zero, infinite, NaN, tiny and huge operands and zero divisors (within the S3 reassociation latitude), the
exceptions of the integral variant (a zero divisor throws as the source does) and the order of observable evaluations around a
grouped chain (the simplification itself runs none). Two observations:

- The first-pass result of every grouped chain of these families is **exactly the canonicalization of its signed terms**: the
  result of the linear chain of the same signed terms in source order, term instance for term instance (`subdivR n` gives the
  result of the alternating chain `subdivM n`, `subdivB n` the one of `subdiv n`; `subdivR 4` gives
  `1 + ((0 - x1/x0) + ((0 - x3/x0) + (x2/x0 + x4/x0)))`). No factoring, cancellation, merging or constant folding happens.
- With mixed signs the second pass reorders the subtracted terms (`0 - x3/x0` before `0 - x1/x0`): the free-parameter ties of
  S4 (§14.7, §15.9), unchanged here.

The built-in simplifier itself is the reference of every differential test: `ExpressionSimplifier.GroupedQuotientChainsDisabled`
(internal, per thread, audits and tests only) switches the new family off and leaves every other path as it is, so the
simplifier treats such a chain level by level, as before. A derived simplifier is not the reference (its plan differs since
C3c, and it keeps the level by level treatment anyway).

### 16.3 Admission: `IsGroupedLinearQuotientChain`

`FlattenAdmission` tests the family only when the linear walk refused the chain **for a subtracted group** (its first refusal
cause is `SubtractedGroup`), after the factorable family refused it too, for the exact built-in type, inside an operation.
Every other linear refusal is a refusal of this family as well (its conditions are those of a linear chain, with at most one
constant and no anchor), so no other chain is walked once more. The conditions:

1. the chain is `double` or `float`;
2. every crossed node is an ordinary `Add` or `Subtract` of that type (`AsCrossedAdditiveNode`, `IsOrdinaryBinaryArithmetic`):
   no custom, lifted or checked operator (a checked node is not crossed, so it is a term, and no term is one);
3. no crossed node has two constant operands;
4. every term is a linear term (a parameter, a finite non-zero constant, a product of a parameter by a parameter or by a
   finite constant other than `0`, `1`, `-1`) or a linear quotient (`IsLinearQuotient`: an ordinary division of a parameter by a
   parameter of the chain's type, §15.5): no call, invocation, member access, conversion, negation, nested division, extension
   or dynamic node;
5. the checks of `LinearChainOperands` over the **whole tree**, every branch included: a numerator occurs once and is never a
   term, a factor or another numerator; a denominator may divide several quotients but is never a term, a factor or a
   numerator; the coefficients differ from each other and from the constant term;
6. at most **one** constant term (`LinearChainOperands.ForGroupedChain`, explicit local state) and at least one quotient
   (`HasQuotients`; a chain of linear terms only belongs to the factorable family).

The walk is the walk of `IsLinearChain` without the subtracted-group and anchor cases: iterative, one structural visit per
crossed node, type tests first (an extension node is never read; the operator of a node is classified only once both operands
are known to be framework nodes of an accepted shape), the pending stack allocated only for a branched tree. A shared sub-chain
holds a numerator twice (refused at its second occurrence) or constants only (refused at its first constant pair), so the walk
is linear in the distinct nodes even on a DAG whose paths multiply.

**Refusal propagation.** A refusal of the family is inherited by every chain that contains its cause, as for the linear family
(§9): a local cause by every chain that crosses its node, a repetition by every chain that holds both occurrences, a second
constant by every chain that holds both constants. `RecordRefusedSpine` (now given its target table and returning the depth it
proved) records the spine nodes that contain the cause in the family's own table, `FlattenGroupedRefusals`, which only ever
skips this test (a node below a refused grouped chain may still be a linear chain). The shared table of known refusals is
lowered to the depth this family proved, so that a sub-chain the family may admit is never refused by one lookup; the linear
refusal depth is not reused unproven. The type of the chain and the absence of quotients (`NoQuotient`) are inherited by every
sub-chain and do not restrict that depth. The old paths are unchanged for the chains no family admits: the linear walk, then
the factorable walk (or the spine test when the factorable family is switched off), with their own causes and depths.

### 16.4 Treatment: `FlattenGroupedQuotientChain`

The admitted root is recorded in the session (`AdmittedGroupedQuotientRoot`, detached by every nested operation and restored on
exit, like the spine and factorable roots); the flatten rule invoked right after it on that root consumes it.
`FlattenedChain.Extract` already gives the signed terms of the whole tree, the inverted sign applied through every subtracted
group: the rule prepares each term once, in source order (`PrepareExpression`, `RecordLocallySimplified`), collects them and
canonicalizes them once with `AdditiveFactoring.None`. Each quotient is an **indivisible additive atom**: `IsFactorableTerm`,
`AdditiveTermFamily` and the algorithms of C3c/C3d are unchanged, and no transformation `a/b + c/b → (a + c)/b`,
`a/b - a/b → 0` or `c * (a/b) → (c * a)/b` is introduced. `ExpressionTranformer.Flatten.cs` is not modified.

**Fallback.** The proof below needs every prepared term to be its raw term, a constant node holding the same boxed value, or the
same quotient or product (`IsPreparedGroupedTerm`). No rule is known to rewrite such a term; should one do so, the levels run
one by one on the prepared terms, children before their parent, through `TransformLevel` and the `NaN` fold of the parent's
preparation (`TransformGroupedLevelByLevel`, an explicit stack of frames, one structural visit per node): the historical
treatment, without preparing anything twice. `FlattenAdmissionStatistics.ForceGroupedFallback` forces it for audits; the seeded
corpus gives the historical structure through it too.

### 16.5 Why the result is the level by level result

Level by level, a crossed node sees two prepared operands, each a prepared term or the canonical form of a sub-chain.

- **Terms.** Preparing a term gives the term itself (§15.5): a parameter, the constant, a product, a field division of two
  parameters, which never throws in IEEE-754; no rule rewrites it into a sum, a negation, a zero or a call.
- **The rules that apply** are the reassociation rules on a subtracted canonical sum (`SubstractionWithAddition`,
  `SubstractionWithSubstraction` and their finishing), the sign rules on the symbolic `0 - t` of a canonical subtracted term
  (`SubstractionWithZero(0, t)`, `AdditionWithNegate`, `SubstractionWithNegate`) and the finalization, which canonicalizes the
  signed terms it collects. They only move signs and regroup terms: identities the global canonicalization applies.
- **Nothing else can apply.** Two operands of a level always hold a parameter the other does not hold (a term, a factor or a
  numerator; a shared denominator is never an operand by itself), the simplifier never introduces a parameter and never removes
  one from such a chain, so every factoring comparison of `AdditionOfEqualsElements`, `SubstractionOfEqualsElements` and of the
  additive canonicalization fails and nothing cancels or merges; a sub-sum only matches another sub-sum (§13.4), never one with
  disjoint parameters. With one constant term no constant pair is folded; the only zeros are the symbolic ones of `0 - t`, which
  the sign rules consume. No logarithm, power or call is present, so no other additive rule matches.
- **Order.** Free parameters tie in the canonical order (S4); every rewrite above keeps the relative order of the terms of one
  sign, those of the left operand before those of the right one, so the per-level stable sorts compose into the stable sort of
  the signed terms in source order, which is what the single canonicalization sorts.
- **Effects and policies.** Every operand is inert ordinary floating-point arithmetic over parameters and primitive constants:
  nothing throws, nothing calls user code, the effect gate admits every rule on it under every policy, the result does not
  depend on the policy, and no evaluation is dropped, repeated or moved.

This is the proof sketch; the equality is **pinned** on every tested shape against the historical treatment of the built-in
simplifier, by strict structural identity after one pass (node types, static types, operator methods, constant bits and
parameter instances), never through a numerical tolerance alone (§16.6).

### 16.6 Validation

**Suites** (Release): Unit 14,751 passed (1 skipped), Security 399 (3 skipped), Functional 383 (a first run reported one
failure that the rerun did not reproduce; 383 of 383 on the rerun), ETC Debug (`Coverage`) 350 (1 skipped).

**New tests.** `ExpressionSimplifierGroupedQuotientReferenceTests` (§16.2, passes on `ed3ea136`),
`ExpressionSimplifierGroupedQuotientChainTests` (Unit) and `ExpressionSimplifierGroupedQuotientSecurityTests` (Security):

- a seeded corpus of 3,000 grouped chains (2 to 14 terms, `double` and `float`, shared and own denominators, parameters,
  linear products, at most one constant, random groupings and signs, distinct, shared or missing parameter names) gives after
  one and two passes exactly the structure of the historical treatment, under the six policies on one case in ten; 938 of them
  are admitted grouped chains. The forced fallback gives it too (600 cases);
- right, balanced and mixed-sign families of 2 to 12 quotients, shared and distinct denominators, `double` and `float`, are
  admitted and give the historical structure under every policy, after one and two passes; 8 to 128 quotients give the result
  of the linear chain of the same signed terms (the historical treatment is the reference up to 16);
- 24 refused shapes keep the root refused and the historical result: repeated numerator, numerator reused as a denominator,
  denominator reused as a numerator, a term or a factor, two constants, a `NaN` constant, no quotient, constant numerator or
  denominator, sum numerator, product denominator, nested division, negated quotient, custom division, custom or checked crossed
  operator, effectful call or numerator, shared group, `long`, `int`, `decimal` and lifted `double?` quotients;
- derived simplifiers keep their rules (a division rule of their own sees every quotient as often as before), their
  finalization and their result, and the admission never walks the family for them;
- values bit for bit those of the historical result on special operands; observable evaluations as often and in the order of
  the source, none run by the simplification; comparer equality and hash consistent with the linear chain;
- concurrency (32 parallel calls on one instance), a nested public call from user code, an instance reused after a budget
  refusal, the per-thread switch;
- security: extension nodes (as a term, a numerator, a denominator) and impersonating division and subtraction methods never
  read by the admission; the work counted and refused just above each limit; 20,000 quotients simplified on a 256 KiB stack, the
  same chain ending with a call keeps the controlled `Stack` refusal; a DAG of `2^50` paths refused by the admission in fewer
  than four visits per distinct node; a refused 200-level chain walked once by the family (199 lookups), the table isolated per
  operation and thread.

**S5 campaigns** (`ed3ea136` against the branch, default policy, 1,000 seeds and 1,000 nested cases): `terminate`, `converge`,
`stability`, `comparer`, `diff`, `effects`, `s3` and `canon` produce byte-identical result, anomaly and summary files (25 files):
no case of these corpora changes.

**Differential corpora** (every branch result compared with `ed3ea136`, then evaluated against its source on twelve samples when
it differs):

| Corpus | Cases | same | different | `master` budget refusal, branch result |
|---|---:|---:|---:|---:|
| historical (C3b–C3e), default | 40,000 | 40,000 | 0 | 0 |
| historical, each explicit policy (5) | 15,000 | 15,000 | 0 | 0 |
| historical, derived simplifier | 15,000 | 15,000 | 0 | 0 |
| historical with quotients, default | 40,000 | 40,000 | 0 | 0 |
| historical with quotients, derived | 15,000 | 15,000 | 0 | 0 |
| grouped (2 to 40 terms), default | 3,000 | 2,995 | 0 | 5 |
| grouped, each explicit policy (5) | 1,000 | 997 | 0 | 3 |
| grouped, derived simplifier | 1,000 | 1,000 | 0 | 0 |

- **Expected improvements.** The only differences are grouped chains on which `master` exceeds the default budget
  (`ExpressionResourceLimitException`, 1,000,000 transformation visits) and the branch returns a result. On the five cases of the
  default policy, the historical treatment run **without limits** (1.7 to 9.9 s each) gives exactly the structure of the branch,
  and the values agree with the source within rounding.
- **Structural changes, exception changes, value changes, evaluation-count changes: none.** The non-idempotent counts are those
  of the baseline (782, 322, 10,501, 4,864, 6,008 on the historical corpora; free-parameter ties on the grouped ones).

**Stress families** (S5 harness `profile`, `ed3ea136` against the branch): `p` 16 and 64, `B`, `g`, `F`, `H`, `Lsub`, `Rsub`,
`Lmul`, `bal` 64, `z`, `e`, `k`, `n` 16, `o`, `r` 8, `Gm`, `div`, `pow`, `negmul`, `negdiv`, `subneg`, `plus0`, `zeroplus`,
`pow1` 32, `negsum`, `zerominus` 16, `horner` 12, `constabs`, `nanabs`, `nanpow` 10 and `subdiv`, `subdivM`, `subdivD`, `subdivL`
64: identical minimal transformation and structural budgets, comparer simplifications and results at every point.

### 16.7 Growth (`profile`)

Branch, same protocol (`profile`, one process per point; the times are medians on a loaded machine, the counters are
deterministic):

| Family | n | transformation visits | structural visits | comparer simplifications | median ms |
|---|---:|---:|---:|---:|---:|
| `subdivR` | 8 | 26 | 171 | 0 | 0.018 |
| `subdivR` | 24 | 74 | 895 | 0 | 0.043 |
| `subdivR` | 64 | 194 | 3,623 | 0 | 0.13 |
| `subdivR` | 128 | 386 | 7,377 | 0 | 0.19 |
| `subdivR` | 256 | 770 | 18,561 | 0 | 0.49 |
| `subdivR` | 512 | 1,538 | 40,751 | 0 | 2.8 |
| `subdivB` | 64 | 194 | 2,224 | 0 | 0.36 |
| `subdivB` | 512 | 1,538 | 29,977 | 0 | 5.7 |
| `subdivBM` | 24 | 74 | 980 | 0 | 0.041 |
| `subdivBM` | 512 | 1,538 | 39,915 | 0 | 2.8 |

- Every grouped family (`subdivR`, `subdivB`, `subdivBM`, `subdivRD`, `subdivRF`, `subdivBF`) takes `3n + 2` transformation
  visits at every size from 4 to 512 (one for the root, one per term, one per operand of each quotient) and no comparer
  simplification: the growth is linear. `subdivR 24`: 811,813 → 74 visits, 1.5 s → 43 µs; `subdivB 64`: 294,472 → 194 visits,
  392 ms → 0.36 ms; `subdivBM 24`: 293,292 → 74 visits.
- The structural visits are those of the linear chain of the same signed terms (`subdivR` and `subdivM`, `subdivB` and
  `subdiv`) plus the admission walk (3 to 11 visits more): a ratio of 2.0 to 2.5 per doubling, the `n log n` comparisons of the
  sort of the canonicalization (§15.7).
- The reference families have exactly their baseline counters at every size from 4 to 512: `subdiv`, `subdivM`, `subdivD`
  (`3n + 2` transformation visits; 29,966, 40,748 and 29,966 structural visits at 512) and `subdivL` (`4n + 1`, refused for the
  integral division; its structural visits stay quadratic, 131,333 at 512, as on the baseline).

### 16.8 Measures

**BenchmarkDotNet** 0.14.0, in process, 8 warmup and 20 measured iterations, five processes run one after the other pinned to the
performance cores (affinity `0x14`) at high priority, as in §15.8: base (`ed3ea136`), branch, base, branch, base. The table
compares the faster process of each side; the A/A column is the spread of the three base processes. `subChainBM 32` could not run
on the base (BenchmarkDotNet refuses an in-process case of several seconds per operation): `subChainBM 16` replaces it.

| Case | base µs | branch µs | Time | base A/A | base KB | branch KB | Allocated |
|---|---:|---:|---:|---:|---:|---:|---:|
| `a+b` | 0.90 | 0.90 | +0.7 % | 3.2 % | 2.47 | 2.47 | 0.0 % |
| `x+x+x` | 2.08 | 2.08 | +0.2 % | 2.5 % | 3.64 | 3.64 | 0.0 % |
| `repeated 16` | 9.31 | 9.52 | +2.3 % | 3.3 % | 21.96 | 21.96 | 0.0 % |
| `repeated 48` | 25.33 | 25.32 | −0.1 % | 0.8 % | 60.03 | 60.03 | 0.0 % |
| `repeated 512` | 282.61 | 283.94 | +0.5 % | 1.0 % | 627.03 | 627.03 | 0.0 % |
| `coef 24` | 83.40 | 84.60 | +1.4 % | 0.9 % | 114.81 | 114.81 | 0.0 % |
| `coef 128` | 422.91 | 429.00 | +1.4 % | 0.6 % | 569.71 | 569.71 | 0.0 % |
| `tail 48` | 37.95 | 37.79 | −0.4 % | 0.9 % | 85.94 | 85.94 | 0.0 % |
| `tail 512` | 429.06 | 437.11 | +1.9 % | 0.9 % | 869.66 | 869.66 | 0.0 % |
| `twice 48` | 33.35 | 33.83 | +1.4 % | 1.2 % | 66.45 | 66.45 | 0.0 % |
| `cancel 128` | 58.07 | 58.49 | +0.7 % | 1.3 % | 142.30 | 142.30 | 0.0 % |
| `linear 200` | 58.63 | 58.85 | +0.4 % | 1.8 % | 121.91 | 121.91 | 0.0 % |
| `sinMid 16` | 9.81 | 9.71 | −1.0 % | 0.2 % | 21.76 | 21.76 | 0.0 % |
| `mixed 8` | 24.75 | 24.94 | +0.8 % | 0.3 % | 28.44 | 28.44 | 0.0 % |
| `mixed 32` | 100.91 | 102.84 | +1.9 % | 1.8 % | 110.03 | 110.03 | 0.0 % |
| `partial 32` | 69.39 | 70.05 | +0.9 % | 0.1 % | 103.07 | 103.07 | 0.0 % |
| `cross 32` | 72.07 | 72.77 | +1.0 % | 1.0 % | 118.89 | 118.89 | 0.0 % |
| `subChain 16` | 16.39 | 16.90 | +3.1 % | 6.6 % | 17.83 | 17.83 | 0.0 % |
| `subChain 64` | 60.67 | 63.05 | +3.9 % | 8.2 % | 65.56 | 65.56 | 0.0 % |
| `subChainM 24` | 24.36 | 25.28 | +3.8 % | 6.3 % | 23.20 | 23.20 | 0.0 % |
| `subChainD 64` | 62.65 | 65.48 | +4.5 % | 7.5 % | 68.87 | 68.87 | 0.0 % |
| `subChainF 64` | 70.76 | 71.37 | +0.9 % | 4.2 % | 65.38 | 65.38 | 0.0 % |
| `subChainR 16` | 18,319.53 | 16.79 | **÷ 1,091** | 0.7 % | 14,077.00 | 16.10 | ÷ 874 |
| `subChainR 24` | 924,035.99 | 24.86 | **÷ 37,170** | 0.5 % | 587,847.88 | 22.47 | ÷ 26,160 |
| `subChainB 32` | 30,652.35 | 31.24 | **÷ 981** | 0.6 % | 18,606.01 | 32.68 | ÷ 569 |
| `subChainB 64` | 250,266.74 | 62.14 | **÷ 4,027** | 2.5 % | 134,269.07 | 62.95 | ÷ 2,133 |
| `subChainBM 16` | 6,817.78 | 17.39 | **÷ 392** | 1.3 % | 5,362.02 | 16.59 | ÷ 323 |

- The grouped chains go from milliseconds or seconds to tens of microseconds, linearly in their length, and cost what the linear
  chain of the same length costs (`subChainR 24` 24.9 µs, `subChainM 24` 25.3 µs; `subChainB 64` 62.1 µs, `subChain 64` 63.1 µs).
- The reference families allocate exactly what they did. Their times differ by −1.0 % to +4.5 %: within the A/A spread of the
  base for the quotient chains (4 to 8 %) and the small cases; `coef 24`, `coef 128`, `tail 512`, `mixed 32` and `twice 48`
  (+1.4 % to +1.9 %) are slightly above the spread of the three base processes, a level §15.8 also measured between two
  processes of one binary (up to 9 %). The new test runs only after a linear refusal for a subtracted group and a factorable
  refusal, which none of these cases reaches; the only change on their path is one more field in the saved operation scope.
- **Harness `growth` families** (`shared`, `none`, `crossed`, `ties`, `repeated`, `wide`, `collide`, `dag`, `coefshared`, 8 to
  512 terms): transformation visits, structural visits, factoring counters and result sizes identical at every size;
  allocations equal up to 160 bytes of measurement noise. Single-process times vary by −10 % to +14 %; a second A/A pair at 512
  terms (two pinned processes per binary) gives `ties` 18.4 / 19.3 ms (base) and 19.4 / 18.6 ms (branch), `dag` 33.5 / 34.5 and
  34.5 / 34.3 ms, `wide` 17.3 / 17.2 and 17.7 / 18.6 ms, `coefshared` 1.39 / 1.34 and 1.39 / 1.34 ms: noise of the same order on
  both sides.

### 16.9 Residual limits

- The family is restricted: at most one constant term (with two, the level by level constant folding depends on the
  grouping), no repeated numerator, no denominator used elsewhere, no quotient of another shape (a constant or product
  numerator, a sum or product denominator, a nested division), no call, no negated term, `double` and `float` only. These
  chains keep the historical cost.
- A grouped chain whose terms repeat (`x - (x/y - …)`) or share factors is neither linear, factorable nor grouped: admitting it
  needs quotient atoms in the proof obligations of the factorable family (C3c), a separate decision.
- The structural visits grow as `n log n`, like those of every linear chain: the sort of the canonicalization (§15.7).
- Mixed signs keep the free-parameter ties of S4: the result is the level by level result, which is not always idempotent.
- A root refused by one lookup of the family's own table lowers the shared known-refusal depth to `0` for that root (a
  performance-only choice: its sub-chains are walked again by the old families).

## 17. Double negations (S5A-10)

Branch `fix/expression-s5a10-double-negation`, base `master` `2e6c6550` (PR #718), measured in a detached worktree of the base.
The S5 campaigns ran with 4,000 seeds and 4,000 nested compositions, both sides built by the same harness.

### 17.1 Reproduction on the base

`-(-x)`, `-(-(-x))`, `-(-(x + y))` and `-(-(x - y))` (`-(-((0 - y) + x))`) were kept by any number of calls, while inside a sum the
signs were already absorbed (`x + -(-y)` gave `x + y`, `x - -(-y)` gave `(0 - y) + x`, `x + (0 - (0 - y))` gave `x + y`): the
additive canonicalization collects the terms through `CollectAdditiveTerms`, which flips the sign of a term at every ordinary
negation it crosses. The rules that negate a prepared operand also left double negations in their results: `(-x) * (-y)` gave
`-(-(x * y))`, `(-x) / (-y)` gave `-(-(x / y))`, `(0 - x) * -1` gave `-(-x)`, `(-y) * ((y * -1) + ((1 - y) * -1))` gave `-(-y)`.

### 17.2 The negation rules

| Rule (signature) | Admission | Builds | Result transformed? |
|---|---|---|---|
| `SubstractionWithZero(Subtract; 0, t)` | ordinary, no constant pair, type with a negation | `-t` | yes (`TransformCore`) |
| `MultiplicationWithZeroOrOne(Multiply; t, ±1)` and mirror | ordinary, no constant pair | `-t` for `-1` | **no** |
| `DivideWithZeroOrOne(Divide; t, c)` | ordinary, `c ≠ 0`, not signed integral for `-1` | `-t` for `-1` | **no** |
| `AdditionWithNegate(Add; a, -b)` and mirror | ordinary outer, `IsOrdinaryUnaryNegate` | `a - b`, or `0` | yes |
| `SubstractionWithNegate(Subtract; a, -b)` | idem | `a + b` | yes |
| `SubstractionWithNegate(Subtract; -a, b)` | idem | `-(a + b)` | yes |
| `NegateWithSubstraction(Negate; a - b)` | idem | `-a + b` | yes |
| `MultiplicationWithNegate(Multiply; a, -b)` and mirror | idem | `-(a * b)` | inner only, **not the negation** |
| `DivisionWithNegate(Divide; a, -b)` and mirror | idem, not signed integral | `-(a / b)` | inner only, **not the negation** |
| additive canonicalization (`FinalizeExpression` of `Add`/`Subtract`) | `CanCanonicalizeCommutativeBinary`, `CanonicalizesOperator`; not gated, pins non-inert terms | signed terms; `-(sum)` for an all-negative sum (`IsNegatedSum`) | it is the finalization |
| multiplicative canonicalization (`BuildCanonicalProduct`) | idem | `-(product)` when the negated factors are odd (`NormalizeProductFactors`) | idem |
| `CanonicalizeFactoredCoefficient` (factoring), `NegateSubtractedSum` (C3c) | ordinary, factorable chain | `-(sum)` | idem |

Dependency graph of the rewrites that produce or consume a negation (an arrow reads "may produce a node that"):

```text
0 - t ──SubstractionWithZero──► -t ──(operand of)──► AdditionWithNegate ──► a - b ──canonicalization──► (0 - b) + a
t * -1, t / -1 ──────────────► -t ─┐
(-a) * b, (-a) / b ───────────► -(a op b) ─┼──(operand of)──► MultiplicationWithNegate / DivisionWithNegate ──► -(…)
(-a) - b ─────────────────────► -(a + b) ──┘                                                          │
-(a - b) ──NegateWithSubstraction──► -a + b                                                             ▼
all-negative sum or product ──canonicalization──► -(…)                          -(-t) when the operand was already -t
```

No rule had the signature `Negate(Negate)`: `NegateWithSubstraction` needs a `Subtract` operand, and the operand of a negation
is prepared before the rules see it (`-(-(x - y))`: the inner `x - y` becomes `(0 - y) + x`, an `Add`).

### 17.3 Why a local rule is not enough (strategy A, measured)

The experiment of the 2026-11-04 audit was reproduced in a separate worktree: a rule
`NegateOfNegate(Negate; Negate) -> operand.Operand`, admitted by `IsOrdinaryUnaryNegate` on both nodes. On the 70,470 cases of the
current `converge` corpus it changes 232 first-pass forms (229 historically), `terminate` 11, `diff` 34, `effects` 16, `s3` 5,
raises the cases fixed after two calls from 272 to **423** (exactly the historical count) and gives **3** violations of the
comparer invariant `Equals(a, b) == Equals(S(a), S(b))` (none on the base).

Root cause of both: an unstable canonical form, not the comparer. The rule only sees the negations of the source and the ones
a rule transforms itself (`SubstractionWithZero`, `SubstractionWithNegate`). The rules marked **no** above return the negation
they build without transforming it, so its `-(-t)` waits for the next call: 148 cases keep the same first pass but move from
fixed@1 to fixed@2. The three violations are such pairs: `(-y) * ((y * -1) + ((1 - y) * -1))` (`int` and `long`, seed 1519) is
`-(-y)` after one call (`MultiplicationWithNegate` over `(-y) * (-1)`) and `y` after two, so `Equals(a, y)` compares `-(-y)`
with `y` (false) while `Equals(S(a), S(y))` compares `y` with `y` (true); the `double` case `-1 * (-1)` against a product of
powers of `x ** 0` is the same with a constant. The comparison memo, the caches and the scopes play no part: the comparer
simplifies once with the public simplifier, which is simply not idempotent there. Changing the comparer would have hidden the
symptom; making the first call finish the negations removes it.

### 17.4 Where the double negation is removed

| Placement | Assessment |
|---|---|
| 1. Before the flatten rule | A flatten chain does not cross negations (`[Flatten(Add, Subtract)]`): a negated operand is an operand, never a linear term (§3), so there is nothing to remove before it. |
| 2. During the extraction of the terms | Already done, and unchanged: `CollectAdditiveTerms` flips the sign at each ordinary negation, for every caller (level canonicalization, linear, anchored, spine, factorable and grouped chains). |
| 3. In the flattened representation | Same mechanism: a term is `(Expression, IsNegative)`; a double negation is two flips. Nothing to add. |
| 4. During the reconstruction | The canonicalization builds `-(…)` only from flattened terms, never over a negation, and the rules that build a negation are not part of the reconstruction: this is where the residues came from. |
| 5. An independent final rule | Strategy A: sees only some of the negations (17.3). |

The choice is the common model of placements 2 and 3 applied to a lone term: the sign model of the additive canonicalization,
used wherever the simplifier builds or finalizes an ordinary negation (strategy C, restricted).

### 17.5 Strategies compared

| | A — local rule | B — flatten only | C — common sign model (chosen) |
|---|---|---|---|
| Change | one rule in the plan | none needed: already absorbed in sums | finalization of `Negate` + one builder `BuildNegation` at the 11 construction sites |
| Plan of rules | +1 rule (pinned plan, trace indices change) | unchanged | unchanged (76 rules, same order) |
| `-(-x)` alone | fixed | not covered (a negation is not a chain) | fixed |
| Rule results `-(-t)` | left to the next call | not covered | removed by the same call |
| fixed@2 (corpus) | 272 → 423 | — | 272 → 273 (17.7) |
| Comparer violations | 3 | — | 0 |
| Values, exceptions, effects | unchanged | — | unchanged |
| Cost | one more rule candidate per `Negate` | — | two type tests per finalized or built negation |

### 17.6 Mechanism and proof obligations

- **Finalization.** `FinalizeExpression` of an ordinary `Negate` whose prepared operand is an ordinary `Negate` returns the
  operand of the latter. It runs where the additive canonicalization runs: after the rules, for every runtime type (a derived
  simplifier included, like the additive canonicalization), behind no effect gate.
- **Builder.** `BuildNegation(t)` returns `t.Operand` when `t` is an ordinary negation, `Expression.Negate(t)` otherwise. Every
  negation the simplifier builds goes through it (`x * -1`, `-1 * x`, `x / -1`, `(-a) * b`, `a * (-b)`, `(-a) / b`,
  `a / (-b)`, the negated sums of the canonicalization and of the factoring, the negated products): the simplifier never
  builds `-(-t)`. The negations built through `TransformCore` are finalized by the first point.
- **Admission.** `IsOrdinaryUnaryNegate` on both nodes: the predefined, non-lifted, unchecked negation of a type of
  `Types.Number` that has one, whose method is absent or is the runtime operator itself (provenance checked first, A-10).
  Excluded: `NegateChecked` (outer or inner: `checked(-(-int.MinValue))` throws), lifted `int?` negations, user operators
  (`-(-s)` runs the operator twice), impersonated reflection objects, a conversion between the two negations, unsigned
  types (no negation), extension nodes (never negations).
- **Exactness.** Two ordinary negations are the identity on every value: `float`/`double` flip the sign bit twice (signed
  zeros, infinities, subnormals and NaN payloads bit for bit), signed integers wrap twice (`-(-int.MinValue)` is
  `int.MinValue`, same for `short` and `long`), `decimal` negation flips the sign without rounding, scale change or overflow.
  Neither can throw; the type of `t` is the type of the node.
- **Effects and policies.** The two operators evaluate nothing but `t`: `t` is evaluated exactly once at the same place, every
  exception it raises is unchanged, so no evaluation is dropped, repeated, merged or moved, whatever `t` is (pure, reading
  or writing state, throwing, allocating, unknown, declared deterministic). The removal therefore needs none of the
  permissions of `ExpressionBehaviorPermissions` and is the same under every policy (default, `Verified`,
  `TrustDeclarations`, `AggressiveSimplification`, every `ExceptionPreservation`): `-(-f())` gives `f()` for an observable
  `f()`, as `a + -(-f())` already gave `a + f()` through the pinned terms of the canonicalization. No policy cache changes.
- **Resources.** Two type tests and a provenance check per finalized or built negation, no walk: no new structural visit, no
  new transformation visit, no allocation (one node less per removed pair).

### 17.7 Differential (base against branch)

Every first-pass difference was classified; none changes a value class (`diff`, `effects`: 0 of 94 changed records change
their class), an exception, an evaluation log or a type.

| Campaign | Records changed | Classification |
|---|---:|---|
| `terminate` (66,270) | 128 | 116 results that held `-(-t)`; 7 identical results with fewer visits (79 → 62, 35 → 24, …); 5 results (`-(1 + (0 - z))` → `-1 + z`, `-Infinity + (0 - x)` → `(-1 - Infinity) - x`) |
| `converge` (70,495) | 251 | 238 first passes that held `-(-t)`; 12 other first passes; 1 same first pass with a different fixed point |
| `diff` (4,000 seeds) | 57 | 54 held `-(-t)`; 3 sums ordered differently or `-0` → `0` (`(-A) - (-A)`), all `same` |
| `effects` (4,004) | 37 | all held `-(-t)`; evaluation logs and exceptions identical |
| `s3` | 5 | the identity `-(-x)` → `x` for `double`, `float`, `int`, `long`, `decimal`: 0 difference over every assignment |
| `canon` | 0 | |
| `comparer` | 0 violation (28,000 invariant checks) | only the number of hash checks changes (its sample depends on the forms) |
| `inventory` | 76 rules, same plan order | only the line numbers and the calls listed for the rules that use `BuildNegation` |

**Delimitation of the changed forms.** Every other changed result was checked to be the base result of the same source with its
double negations already removed (`S_branch(E) = S_master(E′)`): for example `-1 - (((-1 * x) / -1) - c)` gives the base result
of `-1 - (x - c)`, `((-2 + 2) - (-z)) - …` the base result of `z - …`. The rules then see `t` instead of the unfinished `-(-t)`,
so a different rule may apply (`SubstractionWithSubstraction` instead of `SubstractionWithNegate`); none of these rules changed.

**Convergence** (`converge`, 8 passes):

| Category | Base | Branch |
|---|---:|---:|
| fixed@0 | 2,940 | 2,920 |
| fixed@1 | 67,179 | 67,197 |
| fixed@2 | 272 | 273 |
| fixed@3 | 21 | 21 |
| fixed@4 | 8 | 9 |
| cycle, period 2 | 62 | 62 |
| cycle, period 3 | 13 | 13 |

The 20 cases leaving fixed@0 are sources holding a double negation (the new families of the harness included), now simplified
by the first call. Four cases need more passes and one fewer (net +2 cases needing several passes, 0 new cycle); the four are pre-existing residues that the double negations
used to hide, because `SubstractionWithNegate` and `DivisionWithNegate` transformed their operands a second time:

| Case | Base | Branch | Residue (identical on the base for `E′`) |
|---|---|---|---|
| gen#2430 | fixed@1 | fixed@2 | `z - (x - R)` with a call in `R`: `t - (sum)` reassociation not finished (S5A-05) |
| gen#3191 | fixed@1 | fixed@2 | the same with `Log`/`Sin` terms |
| gen#2330 | fixed@1 | fixed@2 | `4 / ((x * (y + -1)) + (x * (y + -1)))`: `t + t` over a product is not finished (`(-1 + y) * (x + x)`) |
| gen#459 | fixed@2 | fixed@4 | the base takes 4 passes for the source with `y / y` instead of `(-y) / (-y)` |
| gen#743 | fixed@2 | fixed@1 | without the negation pair another rule path reaches the canonical form in one call |

These residues are S5A-05/S5A-06 material and are left to their own change, as the mission requires.

### 17.8 Complexity and performance

Deterministic counters (`measure`, limits lifted, one process per point), 26 families at n = 4, 8, 12, 16 and 20 (130 points):
90 points have identical visits and comparer simplifications (every control family: `n`, `constabs`, `nanabs`, `horner`, `Lsub`, `g`, `p`, `B`, `H`, `bal`, `div`,
`plus0`, `pow1`, `zeroplus`, `subdivR`, `subdivBM`, and `neg`, `negcall`, whose towers cost one visit per node on both sides; only their output is smaller).
The other 40 points are the families built from negations, all cheaper:

| Family (n = 20) | Visits base → branch | Comparer simplifications base → branch |
|---|---:|---:|
| `negsum` (S5A-08) | 3,038 → 384 | 782 → 36 |
| `subneg` | 3,059 → 377 | 792 → 36 |
| `negmul` | 6,745 → 1,056 | 4,948 → 888 |
| `negdiv` | 8,869 → 2,773 | 6,472 → 2,444 |
| `zerominus` | 9,224 → 3,535 | 7,262 → 3,202 |
| `negprod` (new) | 7,345 → 1,085 | 5,292 → 908 |
| `negadd` (new) | 570 → 310 | 38 → 76 |
| `negdag` (new, 2^20 logical nodes) | 20,971,750 → 9,437,176 | 8,388,710 → 3,145,725 |

`negsum` at 60 and 120 terms: 57,268 → 2,964 and 408,863 → 11,334 visits, 7,182 → 116 and 28,782 → 236 comparer
simplifications: the work goes from about n^2.8 to n^1.9, the comparer simplifications from quadratic to linear (`2n - 4`).
`negadd` is the only counter that grows: the canonicalization of `x + y` (the double negation removed first) probes the comparer
twice per level instead of once, still linear (`4(n - 1)`), for 46 % fewer visits. `negdag` shares one double negation per level,
so its logical tree has 2^n nodes and both sides grow by the same factor 16 per four levels (every occurrence of a shared node is
transformed, as for every DAG, §5): no new exponential family, 2.2 times fewer visits. No structural walk was added: the removal
is two type tests and a provenance check per finalized or built negation.

Performance (`tests/ExpressionTracingBench/run-campaign.ps1`, one process per scenario and variant, rotating order, A/A copy of
the base binaries, 4 rounds; four scenarios added for the touched path: `neg1` a lone negation, `negsum16` a sum of sixteen
negations, `neg2sum16` a sum of sixteen double negations, `negmul8` a product of eight negations):

| Scenario | Base | A/A | Branch | Allocated base → branch |
|---|---:|---:|---:|---:|
| `ext-root` | 92.8 ns | −0.3 % | −14.5 % (7 rounds: +0.9 % / −12.5 %) | 0 B |
| `scalar` | 1,345 ns | +2.3 % | +2.1 % (7 rounds: −0.9 % / +0.9 %) | 2,256 B |
| `binary` | 5,681 ns | −0.1 % | −0.2 % | 6,513 B |
| `lambda` | 3,235 ns | −1.0 % | +4.4 % (7 rounds: −1.1 % / +0.2 %) | 6,385 B |
| `leftsum20` | 7,070 ns | −9.8 % | −7.7 % | 14,368 B |
| `p9family16` | 41,117 ns | −2.7 % | −1.7 % | 52,132 → 52,131 B |
| `dag10` | 993,995 ns | −3.8 % | +0.1 % | 1,288,325 → 1,288,326 B |
| `calls8` | 4,508 ns | −1.3 % | +0.5 % | 2,632 B |
| `callsum16` | 20,187 ns | +4.2 % | +1.6 % | 31,344 B |
| `cmp-pos` | 60,785 ns | −0.5 % | −1.6 % | 61,742 → 61,741 B |
| `hash` | 30,634 ns | −2.4 % | −0.3 % | 29,694 B |
| `neg1` | 151 ns | +0.9 % | −0.9 % (7 rounds: +1.6 % / +1.3 %) | 80 B |
| `negsum16` | 10,043 ns | +0.2 % | +1.3 % | 20,432 B |
| `neg2sum16` | 128,255 ns | +1.1 % | **−14.7 %** | 198,634 → 193,058 B |
| `negmul8` | 263,121 ns | +1.3 % | **−58.1 %** | 231,019 → 110,265 B |

Reading: the scenarios that hold negations are faster and allocate less where a double negation disappeared (`negmul8` builds
no `-(-t)` any more), the others stay inside the A/A band; the lone negation pays two type tests, below the noise. `ext-root` (an extension root, a path this change does not touch) measured faster twice; with 11 ns on 92 ns it is a code-layout effect, not attributed to the change. Allocations are identical except where a double negation disappears (`calls8`: one base round measured 2,776 B, the eleven other runs of every variant 2,632 B); the single bytes of `p9family16`, `dag10` and `cmp-pos` are rounding of the per-operation average.

### 17.9 Residual limits

- The negation of a constant is not folded (`-(c)`, 3 residues of the base, unchanged): `-(-2)` with an inner constant `-2` is
  one negation of a constant, not a double negation.
- `-(-2 * x)` (a negation of a product with a negative coefficient) is not a double negation and is kept, as on the base.
- The pre-existing residues of 17.7 (`t - (sum)`, `t + t` over a product) now show where a double negation hid them.
- Rebuilding a unary node still goes through the framework factory, which reads the `Type` of its operand and the metadata of
  its method (limitation L-1 of `A10-ReflectionProvenance.md`): unchanged, the classification itself compares reflection
  objects by provenance only.

## 18. Finalization of rebuilt forms (S5A-05, PR A)

Branch `fix/expression-s5a05-finalization`, base `master` `65495e50` (PR #719), measured in a detached worktree of the base,
both sides built by the same harness (S5 campaigns, 4,000 seeds and 4,000 nested compositions).

### 18.1 The residues of §17.7

| Case | Base | First rule left unfinished | Next call | Family |
|---|---|---|---|---|
| gen#2430 | fixed@2 | `SubstractionWithSubstraction`: `(z + R) - (x - (0 - y))` with `Tan(x)` in `R` | `FlattenAdditiveChain` | R |
| gen#3191 | fixed@2 | `SubstractionWithAddition`: `(x - (0 - Log(-20))) - Sin(-1)` | `SubstractionWithZero`, `SubstractionWithNegate` | R |
| gen#459 | fixed@4 | nested reassociations over `-Infinity` and `Sin(y)`, one level finished per call | canonicalizations | R |
| gen#2330 | fixed@2 | `AdditionOfEqualsElements`: `(-1 + y) * (x + x)` | coefficient `x + x` merged | Q |
| gen#743 | fixed@1 | — (fixed by #719, kept as a control) | — | — |

**Family R — unfinished reassociations.** `SubstractionWithAddition` (`t - (a + b)`) and `SubstractionWithSubstraction`
(`t - (a - b)`) finish the nodes they rebuild with the rules (`FinishReassociation`, mission C3d) only when every operand is inert
finite arithmetic (`FinishesRebuilt`): re-entering the rules over a call, an opaque constant or a decimal would add comparer
simplifications and user `Equals` calls the historical rules never made, and a non-finite constant belongs to the
policy-dependent `NaN` fold. Any other operand returned the rebuilt difference as built. Inside an additive parent this is
harmless, since the parent's canonicalization collects its terms, but at a root, or below a product or a quotient, the next call
had to canonicalize it. The signs and coefficients played no part: the terms were already signed, and only the finalization was
missing.

**Family Q — a product added twice with a symbolic coefficient.** `AdditionOfEqualsElements` factors one factor of
`x*(y - 1) + x*(y - 1)` and keeps the coefficient `x + x`. `CanonicalizeFactoredCoefficient` deliberately leaves a coefficient of
added terms alone, because canonicalizing it at every level of the `dag` family made the factoring quadratic in allocations
(§14.8), so `x + x` waited for the next call. The structure hid that the two operands are one product.

### 18.2 Corrections

- **R:** when `FinishesRebuilt` refuses, the rebuilt difference is finalized by the additive canonicalization alone
  (`CanonicalizeReassociation`): the terms of `inner - last` are collected and canonicalized as the finalization of an ordinary
  `Subtract` node would do (zeros dropped, constants aggregated, monomials merged, pinned terms in place). No rule is dispatched,
  no operand is compared through the public comparer, no user code runs, and nothing is folded to `NaN`. A difference with a `NaN`
  term is kept as built, so the policy-dependent `NaN` fold still sees its operand (the `S5-P18C` matrix pins this). Decimal
  differences (`CanonicalizesOperator`) and derived simplifiers (`StabilizesRebuiltForms`) keep the node as built.
- **Q:** when both factor pairs of `AdditionOfEqualsElements` are equal and the common coefficient is not a constant, the result
  is the canonical product of `2` and the factors of the operand (`BuildCanonicalProduct`), the tree the next call built. The
  decision reuses the two comparisons the first test of the rule already made, so it makes no extra comparison. A constant
  coefficient keeps the historical path, which folds it (`0.5*x + 0.5*x` gives `x`, `2*x + 2*x` gives `4 * x`).

Both are inside the effect gate (neither rule is audited for admission outside it), so their operands are inert. Q merges the
two evaluations of an inert product, which is the permission the rule already used to merge the common factor. The plan of
rules is unchanged (76 rules, same order).

### 18.3 Differential (base against branch)

| Campaign | Changed records | Classification |
|---|---:|---|
| `terminate` (66,270) | 36 | 30 results finalized at the same visit count; 6 identical results whose visit count changes (2 lower, 4 higher, by a few visits) |
| `converge` (70,495) | 118 | first passes reaching the fixed point the base reached after more calls (below) |
| `diff` (4,000) | 19 | no value-class regression; one `rounding` becomes `same` |
| `effects` (4,004) | 4 | evaluation logs and exceptions identical |
| `s3` | 0 | |
| `canon` | 0 | |
| `comparer` | 0 violation (28,000 checks) | |
| `inventory` | 76 rules, same order | line numbers and called helpers only |

**Convergence** (8 passes, free parameters / parameters bound by a lambda, `S5_AUDIT_BIND=1`):

| Category | Base free | Branch free | Base bound | Branch bound |
|---|---:|---:|---:|---:|
| fixed@1 | 67,197 | 67,307 | 67,188 | 67,302 |
| fixed@2 | 273 | 212 | 271 | 209 |
| fixed@3 | 21 | 5 | 21 | 4 |
| fixed@4 | 9 | 0 | 9 | 0 |
| cycle, period 2 | 62 | 44 | 20 | **0** |
| cycle, period 3 | 13 | 7 | 6 | **0** |

The cases needing several passes go from 303 to 217, and the cycles from 75 to 51. Every bound cycle is gone, so the 51 cycles
left are free-parameter ties of the S4 canonical order (limitation L-3). One case moves from fixed@2 to a period-2 cycle
(gen#867: the two forms differ only by the order of the free parameters `x` and `y`; bound, it has no cycle). No case moves
from fixed@1 to more passes. With bound parameters, the first rule of the second pass of the remaining cases is
`DivideWithZeroOrOne` (48, `/ ±1`, L-2), the finalization (46), `DivisionOfConstants` (40), `FlattenAdditiveChain` (22),
`SubstractionWithZero` (17) and `AdditionOfEqualsElements` (14): left to later changes.

### 18.4 Complexity and performance

`measure`, limits lifted, 26 families at n = 4 to 20 (the 130 points of §17.8): all 130 points have identical visits, comparer
simplifications and output sizes. The corrected paths are not exercised by those families: R needs a call or a non-finite
constant below a reassociation, and Q a product added twice.

Performance (`tests/ExpressionTracingBench/run-campaign.ps1`, one process per scenario and variant, rotating order, A/A copy of
the base binaries, 4 rounds, then 7 rounds for the four scenarios outside the A/A band; two scenarios added: `subcall`, a
reassociation over a `Tan` call, and `prod2x`, a product added twice):

| Scenario | Base | A/A | Branch | Allocated base → branch |
|---|---:|---:|---:|---:|
| `ext-root` | 79.4 ns | −0.6 % | +0.3 % | 0 B |
| `scalar` | 1,408 ns | +0.2 % | −4.8 % | 2,256 B |
| `binary` | 5,931 ns | −0.6 % | −1.9 % | 6,513 B |
| `lambda` | 3,305 ns | +2.8 % | +4.4 % (7 rounds: +1.4 % / −1.6 %) | 6,385 B |
| `leftsum20` | 6,783 ns | −1.7 % | +2.3 % (7 rounds: +3.4 % / −0.3 %) | 14,368 B |
| `p9family16` | 40,722 ns | +3.6 % | −1.1 % | 52,131 B |
| `dag10` | 983,567 ns | −0.2 % | +0.6 % | 1,288,326 → 1,288,328 B |
| `calls8` | 4,602 ns | +1.8 % | −1.0 % | 2,632 B |
| `callsum16` | 21,156 ns | −1.1 % | −0.7 % | 31,344 B |
| `cmp-pos` | 59,743 ns | +6.6 % | +6.5 % | 61,742 B |
| `hash` | 29,768 ns | +1.7 % | +1.4 % | 29,694 B |
| `neg2sum16` | 112,606 ns | −2.6 % | +2.5 % | 193,058 B |
| `negmul8` | 106,865 ns | +3.5 % | +4.8 % (7 rounds: −1.0 % / +2.6 %) | 110,265 B |
| `subcall` | 7,784 ns | +1.1 % | −0.1 % | 10,064 B |
| `prod2x` | 15,366 ns | −0.6 % | −1.5 % (7 rounds: −2.7 % / −3.1 %) | 21,344 → 21,568 B |

Reading: no scenario is outside the noise of the A/A copy except `negmul8` in the 7-round re-measure (+2.6 % against −1.0 %), a
product of negations that neither correction touches (its counters and allocations are identical): not attributed to the
change. `prod2x` allocates 224 bytes more (the factor list of the canonical product) and is not slower. `subcall` keeps its
allocations: its source chain is already canonicalized by the flatten rule, so the reassociation of family R is reached only
from prepared operands, as in the corpus cases.

### 18.5 Residual limits

- Free-parameter ties (L-3) still order `x` and `y` by collection order; one case of the corpus now cycles through them.
- `/ ±1` (L-2), constant pairs left by `DivisionOfConstants` and `SubstractionOfConstants`, and the negation of a constant
  (`-(c)`, PR B) remain residues of the second pass.
- The reassociation rules still finish with the rules only on inert finite arithmetic; with other operands they stop at the
  canonicalization, so a rule that would apply to the canonical sum waits for the next call.

## 19. Constant negations and signed coefficients (S5A-05, PR B, and the PR C audit)

Branch `fix/expression-s5a05-constant-negation`, stacked on PR A (`fix/expression-s5a05-finalization`, #720): its baseline is
PR A, measured with the same harness (S5 campaigns, 4,000 seeds and 4,000 nested compositions).

### 19.1 Constant negation matrix

`TryNegateConstant` folds the ordinary negation of a constant into the constant the node computes when it runs:

| Type | Admitted | Value |
|---|---|---|
| `int`, `long` | yes | unchecked negation: `-int.MinValue` is `int.MinValue` |
| `short` | yes | negated in `int` and truncated, as the compiled node does: `-short.MinValue` is `short.MinValue` |
| `double`, `float` | yes | IEEE-754 `negate`: the sign bit flips; signed zeros, infinities, subnormals and NaN payloads bit for bit |
| `decimal` | yes | `decimal.Negate`, the operator the node calls: sign flipped, scale kept (`-(0.00m)` keeps scale 2) |
| `int?` and other lifted negations | no | `IsOrdinaryUnaryNegate` refuses lifted nodes |
| `NegateChecked` | no | not an ordinary negation (`checked(-int.MinValue)` still throws at run time) |
| user or impersonated operator | no | not ordinary (A-10 provenance) |
| unsigned, enumerations, boxed values | no | no ordinary negation, or declared type is not the runtime type |

No value is converted. In particular, `Convert.ToDouble` is never used, so no user code runs and nothing can throw.

### 19.2 One sign model

`TryFoldNegation` is the single place where a negation disappears: `-(-t)` gives `t` (S5A-10), and `-c` gives the folded
constant. Both the finalization of an ordinary `Negate` node and `BuildNegation` use it. Every negation the rules and the
canonicalizations build (`x * -1`, `x / -1`, `(-a) * b`, negated sums and products, the factored coefficients) goes through
`BuildNegation`, so none of them keeps `-(c)`. The additive canonicalization's `NegateSubtractedConstant` folds `0 - c`, a
different operation (`0 - 0.0` is `+0`, `-0.0` is `-0`), and keeps its own fold. Operands that are not admitted are boundaries:
they are never traversed, and only type tests and runtime reflection objects are read.

### 19.3 Differential (PR A against PR B)

| Campaign | Changed records | Classification |
|---|---:|---|
| `terminate` (66,270) | 5 | same results with one node less (`-1` built as a constant instead of `-(1)`) |
| `converge` (70,495) | 180 | negations of constants folded; 21 sources holding `-(c)` leave fixed@0 for fixed@1 |
| `diff` (4,000) | 110 | one value class improves (`extreme` → `same`); none regresses |
| `effects` (4,004) | 80 | evaluation logs and exceptions identical |
| `s3`, `canon` | 0 | |
| `comparer` | 0 violation | `-(2)` and `-2` are now one form |
| complexity | 130/130 points identical | |

Convergence: fixed@1 67,307 → 67,326, fixed@2 212 → 213, fixed@3 5 → 5, fixed@5 0 → 1, cycles unchanged (44 / 7); bound:
fixed@2 209 → 210, fixed@5 0 → 1, no cycle. Three cases need more passes, each a pre-existing residue that the negations hid:

| Case | PR A | PR B | Residue |
|---|---|---|---|
| gen#983 | fixed@1 | fixed@5 | a reassociation with a `NaN` term, deliberately left to the policy-dependent `NaN` fold (§18.2) |
| gen#1594 | fixed@1 | fixed@2 | `x / (--1)` went through `DivisionWithNegate`, which transformed its dividend again; once `--1` is `1`, the factored coefficient of added terms (§14.8) is not canonicalized |
| gen#2973 | fixed@1 | fixed@2 | the PR A finalization merges monomials but does not factor common factors (the next call's factorable chain factors `y`) |

### 19.4 Absorbing a negation into a coefficient (measured, deferred)

`-(-2 * x)` is still kept. Two designs were measured against the base of PR A, `65495e50`:

| Variant | `converge` changes | fixed@2 | fixed@3 | fixed@5 | cycles |
|---|---:|---:|---:|---:|---:|
| base | — | 273 | 21 | 0 | 75 |
| B1 — constants only (this PR) | 181 | 273 | 22 | 1 | 75 |
| B — B1 + `-(c * t)` → `(-c) * t` for `c < 0` | 327 | 282 | 26 | 1 | 73 |
| B2 — B + sign normalization of the additive terms | 1,846 | 286 | 23 | 1 | 78 |

The products are exact in every variant: integers form a ring, decimals round the magnitude, and floating-point products give the
same bits except a NaN result, whose sign and payload IEEE-754 leaves unspecified. Convergence, however, regresses. A canonical
sum carries a subtracted term as `0 - (c * t)`, so once the next call can remove a negation from `-(c * t)`, a term with a
negative coefficient has two physical forms in the same sum. The first is `0 - (-2 * z)`: a fixed point before, and after B
rewritten by `SubstractionWithZero` into `2 * z` at the next call. Normalizing the sign of every term (B2) restores those cases
but changes 1,846 canonical forms and makes other residues appear. Both B and B2 are rejected. A sound version needs the sign of
a term carried only by its additive flag at every construction site (rules included), designed and measured as its own change.

### 19.5 Factorization families (the PR C audit)

The families of the mission, simplified once with PR B, each result a structural fixed point:

| Family | Example | Result |
|---|---|---|
| A — opposite coefficients | `-(2*x) + 2*x`, `-(a*x) + a*x` | `0` |
| B — common factors with signs | `-(a*x) + a*y`, `-(a*x) - a*y` | `a * ((0 - x) + y)`, `-(a * (x + y))` |
| C — repeated products | `(a*b) + (a*b)`, `-(a*b) + (a*b)` | `2 * (a * b)`, `0` |
| D — factorization in a chain | `a*x - (a*y + a*z)`, `-(a*(x - y)) + a*x` | `a * ((0 - y) + ((0 - z) + x))`, `y * a` |
| E — quotients | `-(a/b) + a/b` / `(a*x)/b - (a*y)/b` | `0` / not factored |

The signed families A–D are already recognized through negations: `CollectAdditiveTerms` collects a negation as a subtracted
term, and the term analysis (`AnalyzedAdditiveTerm`: sign, coefficient, factors) is the existing logical representation of
signs and coefficients. No second representation is needed. With an observable call (`F() * x - F() * y`, `F()*x + F()*x`), the
effect gate refuses the factoring and both evaluations stay, under every policy (`ExpressionSimplifierConstantNegationTests`).
Family E needs quotient atoms in the proof obligations of the factorable chains (C3c), already listed as deferred.

**Disposition of PR C.** No change measured here meets the acceptance criteria. Coefficient absorption regresses convergence
(19.4), and family E needs a new proof obligation. PR C is therefore delivered as this audit, with no production change. The
proposed next steps are:

1. Carry the sign of an additive term only in its flag, at every construction site, and measure it alone.
2. Add quotient atoms to the factorable family.
3. Canonicalize the factored coefficient of added terms when it repeats a term (gen#1594), within the allocation bound of
   §14.8.

### 19.6 Complexity and performance

Deterministic counters: the 130 `measure` points of §17.8 are identical to PR A. Performance
(`tests/ExpressionTracingBench/run-campaign.ps1`, baseline PR A, A/A copy, 4 rounds, then 7 rounds for the scenarios outside
the band; a game was running on the machine during both campaigns, so only differences well beyond the A/A spread are read; one
scenario added, `negconst8`, a sum of eight products by negated constants):

| Scenario | PR A | A/A | PR B | Allocated PR A → PR B |
|---|---:|---:|---:|---:|
| `scalar` | 1,625 ns | −0.5 % | +0.8 % (7 rounds: +0.7 % / +1.4 %) | 2,256 B |
| `binary` | 6,813 ns | +0.4 % | −0.0 % | 6,513 B |
| `lambda` | 3,993 ns | −1.1 % | +0.1 % | 6,385 B |
| `leftsum20` | 7,903 ns | +0.4 % | +0.2 % | 14,368 B |
| `dag10` | 1,182,550 ns | +4.5 % | +1.0 % | 1,288,328 B |
| `calls8` | 5,402 ns | +0.5 % | +1.5 % (7 rounds: +1.4 % / +0.8 %) | 2,632 B |
| `neg1` | 175 ns | +2.8 % | +4.6 % (7 rounds: −1.5 % / +3.9 %) | 80 B |
| `neg2sum16` | 133,441 ns | +1.7 % | +0.1 % | 193,058 B |
| `negmul8` | 132,749 ns | −2.2 % | −2.6 % | 110,265 B |
| `negconst8` | 293,220 ns | −6.0 % | **−46.8 %** | 253,314 → 162,253 B |
| `subcall` | 9,684 ns | −4.5 % | +32.7 % (7 rounds: +2.1 % / +1.1 %) | 10,064 B |
| `prod2x` | 17,956 ns | −0.7 % | −0.1 % | 21,568 B |

Reading: folded constants make the products of negated constants cheaper, since the rules no longer rewrite `x * -(c)` through
`MultiplicationWithNegate`. A lone negation pays the type tests of `TryFoldNegation`: about 7 ns on 176 ns in the 7-round
re-measure, the only difference beyond the A/A spread. The first `subcall` round was an outlier, which the re-measure does not
reproduce. `calls8` alternates between 2,632 B and 2,776 B in the baseline, the A/A copy and PR B alike (tiered compilation), so its allocations are not attributed to either side.

### 19.7 Residual limits

- `-(-2 * x)` and `-(2 * x)` keep their negation (19.4).
- A `NaN` term in a reassociation is left to the policy fold, so gen#983 needs five calls.
- Quotient factorization (family E) is not done.

## 20. Additive sign normalization (audit, no production change)

Branch `fix/expression-additive-sign-normalization`, base `master` `1c7cec21` (PRs #720 and #721). Every variant was measured
against that base with the same harness: S5 campaigns with 4,000 seeds and 4,000 nested compositions (70,515 convergence cases
with the four new families), and bound stability (`S5_AUDIT_BIND=1`).

### 20.1 Construction sites

| Site | Sign from | Represented by | Negative coefficient possible | Outer negation kept | Finished at once |
|---|---|---|---|---|---|
| `CollectAdditiveTerms` | subtraction, ordinary negation | sign flag | yes (a constant in the term) | no, absorbed into the flag | — (collection) |
| `CanonicalizeAdditiveTerms` (levels, linear, anchored, spine, factorable and grouped chains, `CanonicalizeReassociation`, factored coefficients) | collected flags | flag; `0 - t` or `a - t` on rebuild | yes, kept as an added term (`x + (-2 * y)`) | no | yes |
| `MergeIdenticalMonomials` / `BuildMergedMonomial` | signed coefficient sum | flag for a sum of `-1`, otherwise in the coefficient (`-2 * x`) | yes | no | yes |
| `IsNegatedSum` | all terms subtracted | outer negation of the positive sum (`-(a + b)`) | no | yes (built) | yes |
| `NegateSubtractedConstant` | a subtracted constant | the constant's own sign (`-1 + y`) | n/a | no | yes |
| `BuildCanonicalProduct` / `NormalizeProductFactors` | negated factors | outer negation (`-(2 * x)`); a folded constant keeps its sign (`-2 * x`) | yes | yes | yes |
| `BuildNegation` / `TryFoldNegation` | rule results | outer negation; `-(-t)` and `-(c)` folded | yes (`-(-2 * x)`) | yes | yes |
| `FinishReassociation`, `CanonicalizeReassociation` | rebuilt difference | canonicalization above | yes | no | yes (PR A) |
| `AggregateFactoredCoefficient` / `BuildAdditiveExpression` | coefficient terms | flag; `0 - t` | yes | no | constants only |

No site moves an observable evaluation: the canonicalization pins every term that is not inert, and the rules run inside the
effect gate. The ambiguity is in the reconstruction only: an added term with a negative coefficient (`x + (-2 * y)`) and a
subtracted term with a positive one (`x + (0 - (2 * y))`) are both canonical, and so is the double negative
`x + (0 - (-2 * y))`.

### 20.2 Analysis is already sign-consistent (experiment A)

The term analysis (`AnalyzedAdditiveTerm`: sign flag, coefficient, factors) and the merge of identical monomials combine signed
coefficients whatever the physical form: `(x - (-2*y)) - 2*y`, `(x + -(2*y)) + 2*y` and `(x + (-2*y)) + 2*y` all give `x`, and
`-(2*x) + (-2*x)` gives `-4 * x` (`ExpressionSimplifierAdditiveSignTests.LogicalAnalysis_MergesAcrossPhysicalForms`). A
separate logical normalization would change no output. The existing structures are sufficient, and none was added.

### 20.3 Normalizing the reconstruction (experiments B and C)

| Variant | First-pass `converge` changes | More passes / fewer | fixed@2 free / bound | cycles free / bound | comparer | value classes |
|---|---:|---:|---:|---:|---:|---|
| `master` | — | — | 213 / 210 | 51 / 0 | 0 | — |
| V1: every negative coefficient into the flag | 1,582 | 14 / 1 | 225 / 223 | 52 / 0 | 0 | 1 `same` → `extreme` |
| C1: V1 + `-(c * t)` → `(-c) * t` for `c < 0` | 1,698 | 17 / 1 | 227 / 225 | 52 / 0 | 0 | 1 `same` → `extreme` |
| C2: V1 + every direct constant absorbs the negation | 1,889 | 18 / 2 | 227 / 225 | 52 / 0 | 0 | none |
| V1b: subtracted negative coefficients only (`0 - (-2 * y)`) | 92 | 3 / 0 | 216 / 213 | 51 / 0 | 0 | none |
| V1b + C1 | 217 | 4 / 0 | 216 / 213 | 51 / 0 | 0 | none |
| V1d: V1, also before the merges | not run: same mechanism, no improvement on the reproducers | | | | | |

Every rewrite is exact. `c * t = -((-c) * t)` holds on integers (a ring modulo 2^n) and on decimals, and bit for bit on every
non-NaN floating-point product, a NaN product keeping the sign and payload IEEE-754 leaves unspecified. `a + p = a - (-p)` holds
for every type. Only inert terms are rewritten.

V1 unifies the sums (`x + (-2 * y)` and `x + -(2 * y)` give one form, and so do `x - (-2 * y)` and `x - -(2 * y)`), and C2 also
unifies the lone products (`-(2 * x)` and `-2 * x`). Every variant, however, needs more passes on some cases, and the cause is the
same in all of them. Removing a sign from a coefficient makes coefficients equal: `x - (y - (x - y))` gives `(0 - (2 * y)) + (2 *
x)` instead of `(-2 * y) + (2 * x)`, and `-1 + ((0 - (-2 * z)) + (2 * x))` becomes `-1 + ((2 * x) + (2 * z))`. The next call then
factors those equal coefficients through the factorable chain (`2 * ((0 - y) + x)`, `-1 + (2 * (x + z))`). The canonicalization
of a level never extracts common factors (mission C3c: the factors must not depend on how the source groups them), so the first
call cannot reach that form. The normalization does not create the instability; it exposes the gap between level and
whole-chain factoring wherever the double negative used to keep coefficients apart.

**Disposition.** Under the acceptance rule of the mission (no new unstable first result), no variant is kept, and the production
code is unchanged. `gen#1594` and `gen#2973` (§19.3) are not sign problems: every variant gives them exactly the base results.
Their causes are the factored coefficient of added terms and the common factors of whole chains
(`ExpressionSimplifierS5A05FinalizationTests.KnownResidues_TakeTwoCalls`).

### 20.4 Proposed order

1. Make the level canonicalization reach the whole-chain factoring where its terms are the whole chain (a root, or a node whose
   parent is not additive), or record the factoring decision in the metadata so that the next call agrees with the first. This
   is a C3c change, and it removes the mechanism of every regression in 20.3.
2. Then apply V1b + C1 (the smallest variant: 217 changed first passes, no new cycle, no value change). It removes the double
   negatives `0 - (-c * t)` and `-(-c * t)`, then the remaining forms: V1 for the sums, C2 for the lone products.
3. Measure each step alone against the previous one, with the reproducers of `ExpressionSimplifierAdditiveSignTests`.

### 20.5 Measures

- Complexity (`measure`, 150 points: the 130 of §17.8 and the four new families `altsign`, `negcoef`, `negprodsum`,
  `sharedneg` at n = 4 to 20). On `master` the new families are linear: 97, 101, 111 and 101 visits at n = 20. V1b + C1 has
  identical visits and comparer simplifications on all 150 points; only the output of `altsign` shrinks (97 → 77 nodes at
  n = 20).
- Performance: not measured. No production change is delivered, and the machine was loaded (a game used about 1.4 cores), which
  the protocol excludes.

### 20.6 Reproducers

`ExpressionSimplifierAdditiveSignTests` (`S5-Audit`) pins the forms of `master`:

- the matrix of the mission;
- associations and repeated terms;
- quotients, powers, extreme, infinite and NaN coefficients;
- bound parameters and shared products;
- an observable call with a negative coefficient, evaluated once under three policies;
- the competing forms the comparer does not identify.

A future normalization must update these tests together with the change of 20.4.

## 21. Level and whole-chain factoring agree (mission C3c, consistency)

Branch `fix/expression-c3c-factoring-consistency`, base `master` `523fc708` (PR #722). The base was measured in a detached
worktree of `523fc708`; every figure below compares the branch with it, never the branch with itself. Same harness as §20: S5
campaigns with 4,000 seeds and 4,000 nested compositions (70,515 convergence cases), bound stability (`S5_AUDIT_BIND=1`).

### 21.1 Diagnosis: three mechanisms, not one admission condition

The stability campaign of the base leaves 213 cases at `fixed@2`, 5 at `fixed@3`, 1 at `fixed@5` and 51 cycles. Tracing every case
whose second pass factors (`FlattenAdditiveChain`, `AdditionOfEqualsElements`, `SubstractionWithZero` as responsible rule) gives
three distinct mechanisms, plus a fourth one outside the factorable family.

| | Mechanism | First divergence | Reproducers |
|---|---|---|---|
| M1 | The raw admission refuses the chain (a constant pair, the negation of a constant, a product by `1`, a negated sum, a factor that is a sum the levels fold), so it is simplified level by level, where every canonicalization merges monomials (`AdditiveFactoring.Monomials`) but never extracts common factors. Its result is a factorable chain, which the next call admits and factors. | `FlattenAdmission` decides on the raw root, before any preparation; nothing decides again on the result. | gen#2973, gen#2594, gen#2339, gen#2991, gen#3634, gen#3817, gen#3999, gen#611, nested#546 |
| M2 | A binary factoring (`AdditionOfEqualsElements`) builds the coefficient of two added single terms with the ordinary factory (`CanonicalizeFactoredCoefficient` keeps a sum of added terms, §14.8); the next call orders it. | `CanonicalizeFactoredCoefficient` | gen#1594, gen#777, gen#1535, gen#1778, gen#2167, gen#2171, gen#3018, nested#282, … (16 cases) |
| M3 | The global factoring compares numeric coefficients with their sign: `2*x - 2*y` (a subtracted `2`) gives `2 * ((0 - y) + x)`, `2*x + (-2)*y` (an added `-2`) gives `(-2 * y) + (2 * x)`. Phase A puts the sign of a merged group in its coefficient (`x - (y - (x - y))` merges into `-2 * y`), so the merged form is not factored, while any sign normalization of the reconstruction (§20.3: V1, V1b) makes the two coefficients equal and lets the next call factor them. | `FindBestCandidate` keyed coefficients by their signed value. | Case A of the mission, the regressions of every §20.3 variant |
| M4 | A refused chain that stays refused after the levels (a quotient, a call that is not inert) is canonicalized as a right-associated sum; the next call offers each suffix of that sum as a chain of its own, and a factorable suffix is factored. | The canonical reconstruction of a refused chain (pre-existing: `((2*y) - (x/w)) + (2*z)` takes two calls on the base) | nested#1179 (exposed by M3, below) |

Case A (`x - (y - (x - y))`) is a fixed point on the base (`(-2 * y) + (2 * x)`, one call): its instability appears only once a
sign normalization moves the sign of `-2` into the flag, which is exactly M3. Case B (`gen#1594`) is M2, a separate correction of
the binary factoring, not of the C3c admission. Case C (`gen#2973`) is M1: `(-1E+300) + (-y)` is the negation of a constant,
which `IsFactorableTerm` refuses; once prepared it is the constant `-1E+300`, and the result of the levels,
`-1E+300 + ((0 - y) + (z + ((0 - (x * y)) + (y ** 2))))`, is a factorable chain. Its trace also shows the inner chain
`-1E+300 + ((0 - y) + (z + (0 - (x * y))))` factored by a probe of `AdditionOfEqualsElements` and discarded: the factoring was
reached, on a node whose result an enclosing canonicalization collects again.

### 21.2 Questions of the admission (phase B of the mission)

1. **The admission is evaluated before the preparation** of the operands, on the raw root, once per chain root.
2. **A refused chain can become admissible without any change of its operators**: its refusing terms are folded by the levels
   (constant pairs, negated constants, products by `1`), and no part of the engine tests the result again.
3. **Recorded refusals stay valid**: the tables of known refusals (C1) hold raw nodes of the operation, which are never rebuilt;
   a result is a new tree and is never looked up.
4. **A sub-chain is not distinguished from a whole chain** by the admission: every crossed node is offered as a root of its own,
   and an admitted sub-chain of a refused chain is factored on its own (M4, and the probe of case C).
5. **A parent does not reuse the analysis of its children**: each canonicalization analyzes its terms again (with the hashes of
   C3a).
6. **The session cannot carry a new admission** without keeping a decision that a rebuild invalidates: the admitted roots are
   consumed by the rule invoked right after the admission (§13.4).
7. **Factoring once per chain** needs the boundary of the chain: the node no node of the same chain is transforming.

### 21.3 Strategies compared

| Strategy | Principle | Measured or analyzed | Decision |
|---|---|---|---|
| A — deferred admission after preparation | Test `IsFactorableChain` again on the prepared operands of every crossed node | One more walk per level (quadratic on a refused comb), and an inner level that factors makes the result depend on the grouping again (§13.3) | rejected |
| B — whole-chain canonicalization at the boundary | At the boundary of a refused chain only, test its *result* as the next call would (linear, then factorable) and canonicalize it with `AdditiveFactoring.CommonFactors` | Two walks and one canonicalization per refused chain, linear (21.6); the result is the next call's result (21.5) | **kept (M1)** |
| C — factorability metadata in the session | Record that a prepared sub-chain is factorable, so that the parent decides without walking | The decision belongs to the whole chain, not to a sub-chain (Q4): the metadata would need the boundary anyway, and would outlive the rebuilds that invalidate it (Q6) | rejected |
| D — specialized finishing of some rebuilds | Finish the coefficient of a binary factoring, or the factored forms of the reassociation rules | Reuses `CanonicalizeAdditiveTerms` (no second factoring algorithm), at a constant cost when the coefficient has two single terms | **kept, restricted (M2)** |
| D' — inner sub-chains without common factors | An admitted sub-chain of a refused chain merges monomials only, the boundary factors | Removes M4, but changes the historical result of every refused family whose sub-chains are factorable today (`(a*x + a*y) + x/b` keeps `a*(x + y)` on the base) | rejected (refused families keep the historical behavior) |

**The boundary.** The engine knows it without any parent pointer: `ExpressionTransformer.TransformWithFlatten` sets a thread-static
marker (`WithinRefusedChain`) while a refused root is transformed historically, so its crossed operands and the nodes its rules
build (a reassociation, a probe) are not boundaries; the marker is cleared while a node of no flatten group (a call, a product, a
lambda, a term of an admitted chain) is transformed, so a chain below it is the boundary of its own chain; and the simplifier
clears it at the entry of every operation (`TransformWithinBoundary`, the public calls and the comparer's simplifications), so a
nested operation never inherits it. It is restored in `finally` on every path, so a budget refusal never leaves it set. The hot
path of a node of no group pays one thread-static read.

**M3.** `FindBestCandidate` indexes a coefficient by its magnitude (`CoefficientMagnitude`: the exact negation, IEEE-754 for the
floating-point types, never for an unsigned type nor a signed minimum, which is its own negation). When every holder has the same
value the factored coefficient is that value (the historical result: `-2*x + -2*y` keeps `-2 * (x + y)`); when holders have
opposite values the magnitude is factored and the holders of the negative value keep their residue with the opposite sign
(`(-c)*t` is `-(c*t)` exactly). A tie between coefficients is broken on the key of the magnitude, so it never depends on which
holder came first. The linear family compares its coefficients and constants by magnitude too (`LinearChainOperands`): a chain
holding `2*x` and `-2*y` is no longer linear (no rule could apply at a level, but the global factoring now does), so it reaches the
factorable family. No sign normalization of the reconstruction is changed (§20.3 stays the decision of a later change).

**M2.** `CanonicalizeFactoredCoefficient` also canonicalizes a coefficient whose two operands are single terms (neither an
ordinary `Add` nor `Subtract`): its canonicalization costs two keys, whatever the length of the chain. A coefficient that is
already a sum keeps the historical node, so the `dag` growth family of §14.8 stays linear (21.6).

### 21.4 Proof obligations of the boundary finishing

- **Same identities as the next call.** The result is tested by `IsLinearChain` then `IsFactorableChain`, the tests the next
  call's admission applies to it, and canonicalized by `CanonicalizeAdditiveTerms(…, CommonFactors)` then `NegateSubtractedSum`,
  the treatment of `FlattenFactorableChain`; a negated sum (`-(sum)`, C3d) is finished through its operand and negated again
  (`BuildNegation`), as the next call finishes the operand of a `Negate` root. The terms are not prepared again: they are the
  finished terms of the levels (the next call prepares them, which rewrote none of the factorable terms of the campaign: every
  M1 case reaches in one call the exact tree its second call reached on the base).
- **No admission is widened.** Every proof of §13.4 and §13.5 holds on the tested result: every term inert and factorable, every
  crossed node an ordinary operator, no constant pair crossed. The constant pairs, the negated constants and the products by
  `1` the raw admission refused are folded by the levels, exactly, before the test; the decisions about signed zeros
  (`-0.0 + -0.0`), `NaN` and the infinities are those of the levels (a non-finite constant refuses the result).
- **Effects.** A result holding a call that is not inert, a pinned term or any node outside the family is returned as is: no
  user code runs, no evaluation moves (type tests and runtime reflection objects only, ETC R4 and S5 A-10).
- **Once per chain.** The boundary is finished once, after its whole historical treatment; its crossed operands and the nodes its
  rules build never are, so a sub-sum is never factored before the enclosing canonicalization collects its terms.
- **Budgets.** Both walks count their structural visits and stop at the first refusal; the canonicalization counts as every
  canonicalization. Every walk is iterative.

### 21.5 Validation

**Stability** (8 passes, free parameters / parameters bound by a lambda):

| Category | Base | M1 | M1 + M3 | M1 + M2 + M3 (branch) | Base bound | Branch bound |
|---|---:|---:|---:|---:|---:|---:|
| fixed@1 | 67,346 | 67,355 | 67,354 | 67,370 | 67,341 | 67,364 |
| fixed@2 | 213 | 204 | 205 | **189** | 210 | **187** |
| fixed@3 | 5 | 5 | 5 | 5 | 4 | 4 |
| fixed@5 | 1 | 1 | 1 | 1 | 1 | 1 |
| cycles, period 2 / 3 | 44 / 7 | 44 / 7 | 44 / 7 | 44 / 7 | 0 | **0** |

25 cases reach their fixed point in one call (every M1 and M2 reproducer, gen#1594 and gen#2973 included); one case needs two
calls instead of one: nested#1179, `(0 - (x / (y / 0))) + ((-2 * y) + (2 * z))`, an M4 case (a refused chain with a quotient,
whose suffix `(-2 * y) + (2 * z)` is factorable once coefficients compare by magnitude; on the base the same suffix with `2 * y`
needs two calls as well). No case moves to a cycle; the 51 remaining cycles are the free-parameter ties of S4 (L-3), with no bound
cycle on either side.

**Campaigns** (base against branch):

| Campaign | Changed records | Classification |
|---|---:|---|
| `converge` (70,515) | 40 first passes | 11 new forms that were already fixed points (M3: `a - (b - (c - d))` with `c = a`, `d = b` gives `2 * ((0 - y) + x)`, `-1 + ((0 - (-2 * z)) + (2 * x))` gives `-1 + (2 * (z + x))`); 25 first passes reaching the base's second pass (M1, M2); 3 coefficients ordered by M2 in cases that stay `fixed@2` for a sum factor (outside C3c); nested#1179 |
| `terminate` (66,270) | 17 | identical visit counts and classes; results only (M3 forms) |
| `diff` (4,000 seeds, 7 types) | 21 | one value class moves: `float` `(y - (y - (0.5f - y))) - ((y / (1f / y)) - (2f - x))`, two samples `same` → `extreme` at `y = 3E+38` (the factoring of `-y - y*y`, the S3 latitude the base's second call already took); no `VALUE`, no exception added, removed or changed |
| `effects` (4,004) | 0 | evaluation logs and exceptions identical |
| `canon`, `s3` | 0 | |
| `comparer` | — | 0 violation; 164,888 hash-consistent and 329,298 transitive checks instead of 164,702 and 328,740 (more pairs identified) |
| `inventory` | — | 76 rules, same order |

**Suites**: Unit, Security, Functional and the Debug expression jobs (21.8). Updated with their reason:
`ExpressionSimplifierS5A05FinalizationTests.KnownResidues_TakeTwoCalls` becomes `FormerResidues_TakeOneCall` (gen#1594 and
gen#2973 now reach the base's second result in one call); `ExpressionSimplifierNumericChainGrowthTests` `negatedInLambda` (M3:
`-7 * x3` and `-(7 * x5)` share `7`) and `negatedChainCancels` (M1: a negated sum, factored at the boundary as the next call
factored it), both now asserted to be fixed points. New: `ExpressionSimplifierFactoringConsistencyTests` (Unit) and
`ExpressionSimplifierFactoringConsistencyResourceTests` (Security). The evaluation-preserving tests are Unit tests: the
Functional suite holds the tests that need sockets, processes or environment paths, which these do not.

### 21.6 Complexity

**The 150 points** (`measure`, limits lifted, the 130 points of §17.8 and the four sign families of §20.5 at n = 4 to 20): all 150
points have identical transformation visits, comparer simplifications, user calls and output sizes on the base and the branch.

**Targeted families** (`profile`, limits lifted, one process per point; new audit families of `Corpus.cs`):

| Family (n = 512) | What it exercises | Transformation visits (both) | Structural visits base → branch | Allocated base → branch | Output nodes base → branch | Boundary tests / factorings |
|---|---|---:|---:|---:|---:|---|
| `refusedfac` `(2 - 1) + x0*x1 + …` | M1, factorable once folded | 394,501 | 11,807,484 → 11,824,804 (+0.15 %) | 464 → 469 MB | 2,045 → 1,025 | 1 / 1 |
| `refusednone` `(2 - 1) + x1*y1 + …` | M1 test, no common factor | 395,011 | 13,655,225 → 13,655,736 | 338.1 → 338.1 MB | 2,045 → 2,045 | 1 / 0 |
| `tailquot` `x0*x1 + … + x1/x0` | refused by a quotient | 3,077 | 40,275 → 40,277 | 1.92 → 1.94 MB | 1,027 → 1,027 | 1 / 0 |
| `dagfactor` `s*x1 + s*x2 + …`, `s` shared | M2 on the dag family of §14.8 | 8,700 | 682,747 → 682,757 | 19.51 → 19.51 MB | 1,027 → 1,027 | 0 |
| `mixsign` `2*x1 - 2*x2 + (-2)*x3 …` | M3 | 2,561 | 27,741 → 27,718 | 2.46 → 2.43 MB | 1,539 → 1,537 | 0 |

At every size from 8 to 512 the transformation visits and the comparer simplifications are identical on both sides; the
structural visits of the finishing grow linearly (`refusedfac`: +166 at 8 terms, +17,320 at 512, the walks and the analysis of
the result), and M2 adds a constant (`dagfactor`: +10 visits and +3 KB at every size, the canonicalization of the first two-term
coefficient only, never of the growing one). The cost of `refusedfac` and `refusednone` is the quadratic level by level
treatment of a refused comb, identical on the base (its probes of `AdditionOfEqualsElements`, 263,672 comparer simplifications
at 512): the finishing does not re-simplify anything, it canonicalizes the result once.

The finishing of a 100,000-term result is iterative except where the global factoring itself recurses: the canonical key and the
effect analysis of a factored product `x0 * (sum)` walk its sum recursively, so a factored sum of thousands of terms needs the
stack the levels of the same chain already needed. `BoundaryFinishing_NeverRefusesWhatTheLevelsAccept_OnASmallStack` (Security)
checks, on a 256 KiB thread and without limits, that at every size from 64 to 4,096 terms the finished simplification succeeds
wherever the levels alone succeed.
