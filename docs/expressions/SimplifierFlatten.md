# Flatten rule of `ExpressionSimplifier` (PR B)

Status: first n-ary treatment of the simplifier, restricted to the *linear additive chains*; §11 describes the shared metadata of mission C3a, §12 the term analysis and the spine chains of mission C3b, §13 the global factoring of mission C3c. Built on the infrastructure of
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
  `-1 + y`, the form the next call folded from `(0 - 1) + y`; `NegateSubtractedConstant`); an unsigned type keeps `0 - c`.

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
- A coefficient produced by the binary factoring on a non-factorable chain is still left for the next pass
  (`x*y + (1 - x)*y` gives `y*(x + (1 + (0 - x)))`, S5 P14).
- A shared sub-chain beyond the revisit allowance, a chain holding a non-inert call, a division, a logarithm or a constant pair
  keeps the level by level treatment (with phase A at each level).
- Derived simplifiers keep their historical results; the factorable family and phase A are reserved to the built-in type.
- Free parameters of one type still tie in the canonical order (S4): a sum of free parameters may keep an order that depends on
  the source; bound parameters order by their scope.
