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
