# Expression simplifier roadmap

Audit date: 2026-09-12  
Baseline: `master` at `0897872cfb9cd0906fabaf28a260e7f28f1e995c` after PR #589.

This roadmap deliberately uses stable stage identifiers (`S1`, `S2`, ...) instead of pull-request numbers. Other work may be interleaved between these stages, so the GitHub PR number that implements a stage is not part of the roadmap contract.

## Architectural boundary

There are two distinct concerns and they should remain separate.

### Simplifier — current focus

The simplifier should produce a mathematically meaningful, deterministic, compact and robust symbolic expression tree. Its primary concerns are:

- correctness of symbolic rewrites;
- deterministic/canonical representation;
- strong structural comparison foundations;
- robust behavior across supported expression shapes and numeric types;
- predictable failure/unsupported-case behavior;
- reasonable construction cost, but only after correctness and robustness.

The simplifier is **not** responsible for choosing the fastest possible machine-level form for the compiled delegate. For example, a compact symbolic power such as `x^2` may be a perfectly reasonable simplifier output even if a future execution optimizer later lowers it back to `x*x`.

### Optimizer — future work

A separate execution optimizer should later transform an already-simplified expression into a form chosen primarily for `Expression.Compile()` runtime performance. Its first-order metric will be repeated execution time of the compiled delegate; construction and compile time come after that unless they enable the runtime improvement.

Possible optimizer responsibilities include:

- strength reduction such as `Pow(x, 2)` to `x * x` when measured faster;
- Horner-form polynomial evaluation;
- common-subexpression elimination where semantics permit it;
- reducing repeated function calls;
- reassociation or factorization chosen from runtime benchmark evidence;
- other lowering decisions driven by generated/executed code rather than symbolic compactness.

The intended pipeline is therefore:

```text
source expression
    -> simplifier
    -> stable symbolic/canonical form
    -> execution optimizer (future)
    -> Expression.Compile()
    -> hot repeated execution
```

Do not automatically run the simplifier again after the future execution optimizer unless that interaction has been explicitly designed: the simplifier could otherwise reconstruct a symbolic form that the optimizer intentionally lowered for execution speed.

## Simplifier stages

### S1 — Harden `ExpressionComparer`

Priority: highest.

`ExpressionComparer` is used by simplification rules to decide whether operands, factors, function arguments or sub-expressions are equivalent before applying algebraic rewrites. It must therefore be a trustworthy foundation rather than merely a convenient test comparer.

Known audit concerns to characterize and address:

- unary comparison currently compares only operands and ignores important metadata such as result `Type`, `Method`, `IsLifted` and `IsLiftedToNull`;
- binary comparison checks `NodeType` and operands but ignores metadata including result `Type`, operator `Method`, lifting state and lambda `Conversion` for coalescing nodes;
- `MemberExpression` comparison passes `yParams` for the left expression context instead of `xParams`;
- parameter matching uses parameter names, which is ambiguous when distinct `ParameterExpression` instances share a name;
- lambda alpha-equivalence and nested parameter scopes need an explicit, reference/scope-aware mapping rather than name lookup;
- `GetHashCode` is based on `Simplify(obj).ToString()` and may not satisfy the `Equals => same hash code` contract for expressions considered alpha-equivalent by `Equals`;
- nullable expression inputs and unsupported expression node kinds need explicit characterization;
- comparer recursion must remain safe when it is invoked from inside simplifier rules that themselves simplify expressions.

The work should begin with baseline characterization tests before changing behavior. Correctness takes precedence over allocation or construction speed in this stage.

#### S1 progress (2026-09-13)

Implemented by this PR (`Utils/Expressions/ExpressionComparer.cs`, `UtilsTest/Mathematics/Expressions/ExpressionComparerTests.cs`, `UtilsTest/Mathematics/Expressions/ExpressionSimplifierComparerRegressionTests.cs`):

- null/reflexivity contract: `Equals(null, null)` is `true`, exactly one `null` is `false`, and a top-level `ReferenceEquals` check (before simplification) makes every expression reflexive, including node kinds the comparer does not structurally understand;
- scope-aware, binding-position-based parameter equality for lambda alpha-equivalence, replacing `ParameterExpression.Name` lookup; nested captures remain visible while comparing an inner lambda's body; same-named-but-distinct parameters and swapped-position reuse of the same `ParameterExpression` instance are both handled correctly;
- lambda `TailCall` participates in equality at the root level (captured before simplification, since `ExpressionTransformer.PrepareLambda` does not preserve it through a rebuild);
- unary/binary node metadata (`Method`, `IsLifted`, `IsLiftedToNull`, and `Type` via a blanket check) now participates in equality, closing a false-positive path where two `BinaryExpression` nodes built from different custom operator `Method`s but the same operands were reported equal;
- `MethodCallExpression.Object` (the receiver) and `MemberExpression.Expression` are now compared structurally/scope-aware instead of by reference or with the previous parameter-array swap bug;
- nonnumeric constant equality is null-safe and requires matching `Type`; native numeric constant equality (`Types.Number` domain only) is now based on an exact rational/NaN/infinity key derived from each type's bit representation, so it is reflexive/symmetric/transitive and never throws (the previous `Convert.ChangeType` pairwise algorithm could throw `OverflowException`, e.g. comparing `-1L` against `ulong.MaxValue`);
- `GetHashCode` is now structural (mirrors `Equals`, using the same scope-relative parameter hashing and exact numeric key) instead of `Simplify(obj).ToString().GetHashCode()`, so alpha-equivalent lambdas and exact cross-type numeric constants now satisfy the `Equals` &#8658; equal-hash contract (verified with `HashSet<Expression>` lookups too);
- a mandatory end-to-end regression proves the old `Method`-blind binary comparison could let `sin(x) / cos(x) -> tan(x)` fire across two operands built from different custom operator methods; the hardened comparer blocks it while leaving the ordinary built-in trigonometric/power identity matrix unchanged.

Remaining S1 work (deliberately out of scope for this PR):

- `ExpressionTransformer.CopyUnaryExpression` does not preserve an original custom unary operator `Method` through simplification, so two unary nodes that differ only in that method can no longer be distinguished once both sides have been simplified; fixing this would require changing `ExpressionTransformer`, which this PR intentionally avoids;
- similarly, a `TailCall` (or other lossy metadata) difference on a lambda *nested inside* a compared body is not recoverable, because `PrepareLambda` rebuilds every lambda it visits, not only the root one; only the root-level flag is protected here;
- broader `Expression` node-family coverage (`Conditional`, `New`, `Block`, etc.) remains conservatively "unequal for distinct instances", as intended - see "Do not broaden node-family support" guidance for this stage.

### S2 — Audit unreachable / dead simplification rules

Audit every transformation rule against the dispatch plan and identify rules that cannot be reached because of missing or incompatible method-level `ExpressionSignatureAttribute` metadata.

Known candidate from the 2026-09-12 audit:

- `ExpressionSimplifier.INumber.Logarithm10SimplificationAddNumber` appears to lack a method-level `[ExpressionSignature(ExpressionType.Add)]`, unlike neighboring logarithm rules, and therefore appears excluded from `ExpressionTransformer.BuildPlan`.

For every candidate, add a behavior test that fails before the fix and passes afterward. Keep each fix narrow and avoid mixing unrelated rule semantics.

### S3 — Formalize the symbolic-equivalence contract

The current simplifier intentionally performs algebraic rewrites that are not guaranteed to preserve bit-for-bit CLR/IEEE-754 evaluation for every floating-point input, for example reassociation/reordering of addition and multiplication and identities such as `x * 0 -> 0`.

Document the actual contract explicitly. The intended direction is a **symbolic algebra simplifier**, not a strict IEEE-754 execution-preserving optimizer.

The contract should distinguish at least:

- exact CLR/operator semantics;
- algebraic identities assumed by the symbolic engine;
- floating-point rounding/reassociation differences;
- `NaN`, infinities and signed zero;
- domain-sensitive identities such as logarithm combination and powers;
- custom/user-defined operators, which must not be treated as ordinary commutative arithmetic unless explicitly proven safe.

Comments such as "preserving semantics" should be tightened where they currently overstate the floating-point guarantee.

### S4 — Replace textual canonical identity with structural canonical keys

Current additive/multiplicative canonical ordering uses `Expression.ToString()` as a canonical key. This is useful but not a true structural identity and is affected by parameter names and custom `ToString()` overrides.

After S1 establishes reliable structural comparison semantics, design a structural canonical key/order that is:

- deterministic;
- scope-aware for parameters;
- independent of `Expression.ToString()` formatting;
- capable of ordering supported node kinds without accidentally claiming unsupported nodes are equal;
- compatible with the simplifier's documented symbolic-equivalence contract from S3.

Do not introduce a grouping-key cache until the observable `ToString()`/key-evaluation behavior and compatibility impact have been explicitly characterized.

### S5 — Construction-performance cleanup

Only after the correctness/structure stages above, re-profile construction-time allocations and CPU cost in the simplifier.

Potential remaining areas include:

- additive `OrderBy` / `ThenBy` / `ThenBy` / `ToList` / `GroupBy` pipeline;
- repeated key computation;
- intermediate group/list materialization;
- `ExpressionComparer` temporary arrays and searches;
- other LINQ/reflection scaffolding still present on frequently used construction paths.

Construction optimizations should be accepted only when they do not weaken the symbolic contract or complicate the later execution optimizer. Prefer small, benchmarked and causally isolated changes like PRs #577-#589.

## Future execution-optimizer stages

These are intentionally deferred until the simplifier is structurally solid.

### O1 — Establish compiled-runtime benchmark suite

Build a benchmark suite whose primary metric is repeated invocation cost of delegates produced by `Expression.Compile()`.

Separate and report:

- simplification time;
- optimizer construction time;
- `Expression.Compile()` time;
- warm compiled delegate execution time;
- execution allocations, if any.

The acceptance criterion for optimizer rewrites must primarily be compiled-runtime improvement, not a prettier or smaller expression tree.

### O2 — Strength reduction and power lowering

Benchmark and, where beneficial, lower small integer powers such as `x^2`, `x^3`, etc. to multiplication chains. Compare alternative association shapes rather than assuming one is faster.

Preserve overflow/domain/operator semantics appropriate to each supported numeric type.

### O3 — Polynomial execution forms

Detect suitable polynomial expressions and benchmark Horner evaluation versus canonical symbolic power/sum forms. Adopt Horner or another scheme only where the compiled delegate is measurably faster.

### O4 — Common-subexpression and repeated-call reduction

Investigate repeated expensive function calls or repeated subtrees. Introduce temporary variables/blocks only where evaluation-count semantics permit it and compiled runtime improves enough to justify the more complex tree.

### O5 — Broader runtime-oriented lowering

Consider additional benchmark-driven rewrites such as factorization, reassociation, branch simplification and call specialization. Every rewrite must be justified by compiled execution measurements and preserve the optimizer's explicit semantic contract.

## Working rules

- Stage identifiers in this file are stable and are not PR numbers.
- A stage may require more than one PR.
- Unrelated PRs may be interleaved freely.
- Simplifier correctness and symbolic robustness come before its construction performance.
- Future optimizer decisions are benchmarked against **compiled delegate execution first**.
- Keep simplifier and optimizer responsibilities distinct.
- Every production change receives focused tests.
- If `Utils` changes, run Unit, Functional and Security test projects plus a Release build.
- Keep `Utils/Utils.csproj` on `net8.0` until its dependent projects are upgraded together.
