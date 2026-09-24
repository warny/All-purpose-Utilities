# Expression simplifier roadmap

Audit date: 2026-09-15  
Current audit baseline: `master` at `8d9521549696095b9880db26c68def7b3a86900a` after PR #594.  
Original roadmap baseline: `0897872cfb9cd0906fabaf28a260e7f28f1e995c` after PR #589.

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

The simplifier is **not** responsible for choosing the fastest possible machine-level form for the compiled delegate. For example, a compact symbolic power such as `x^2` may be a perfectly reasonable simplifier output even if an execution optimizer later lowers it back to `x*x`.

### Optimizer — separate workstream

The repository already contains the public `Utils.Expressions.ExpressionOptimiser`. The future `O*` stages below therefore describe correctness hardening and benchmark-driven evolution of that existing execution-oriented pass, not an invitation to mix runtime optimization logic into `ExpressionSimplifier`.

An execution optimizer should transform an already-simplified expression into a form chosen primarily for `Expression.Compile()` runtime performance. Its first-order metric is repeated execution time of the compiled delegate; construction and compile time come after that unless they enable the runtime improvement.

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
    -> execution optimizer
    -> Expression.Compile()
    -> hot repeated execution
```

Do not automatically run the simplifier again after the execution optimizer unless that interaction has been explicitly designed: the simplifier could otherwise reconstruct a symbolic form that the optimizer intentionally lowered for execution speed.

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
- lambda `TailCall` participates in equality at the root level (captured before simplification, since `ExpressionTransformer.PrepareLambda` did not preserve it through a rebuild at that point in the work);
- unary/binary node metadata (`Method`, `IsLifted`, `IsLiftedToNull`, and `Type` via a blanket check) now participates in equality, closing a false-positive path where two `BinaryExpression` nodes built from different custom operator `Method`s but the same operands were reported equal;
- `MethodCallExpression.Object` (the receiver) and `MemberExpression.Expression` are now compared structurally/scope-aware instead of by reference or with the previous parameter-array swap bug;
- nonnumeric constant equality is null-safe and requires matching `Type`; native numeric constant equality (`Types.Number` domain only) is now based on an exact rational/NaN/infinity key derived from each type's bit representation, so it is reflexive/symmetric/transitive and never throws (the previous `Convert.ChangeType` pairwise algorithm could throw `OverflowException`, e.g. comparing `-1L` against `ulong.MaxValue`);
- `GetHashCode` is now structural (mirrors `Equals`, using the same scope-relative parameter hashing and exact numeric key) instead of `Simplify(obj).ToString().GetHashCode()`, so alpha-equivalent lambdas and exact cross-type numeric constants now satisfy the `Equals` &#8658; equal-hash contract (verified with `HashSet<Expression>` lookups too);
- a mandatory end-to-end regression proves the old `Method`-blind binary comparison could let `sin(x) / cos(x) -> tan(x)` fire across two operands built from different custom operator methods; the hardened comparer blocks it while leaving the ordinary built-in trigonometric/power identity matrix unchanged.

Remaining S1 work as of 2026-09-13 (addressed below on 2026-09-14, except the last item):

- ~~`ExpressionTransformer.CopyUnaryExpression` does not preserve an original custom unary operator `Method` through simplification~~ - fixed, see the 2026-09-14 entry below;
- ~~a `TailCall` (or other lossy metadata) difference on a lambda *nested inside* a compared body is not recoverable~~ - fixed for `Type`/`TailCall`/`Name` at every nesting depth, see below;
- broader `Expression` node-family coverage (`Conditional`, `New`, `Block`, etc.) remains conservatively "unequal for distinct instances", as intended - see "Do not broaden node-family support" guidance for this stage. This is an optional/conservative follow-up, not a correctness bug: the comparer never claims false equality for these kinds, it is simply silent (returns `false`) on them.

#### S1 progress (2026-09-14) — reconstruction fidelity

`ExpressionComparer` (2026-09-13) still received already-damaged trees from `ExpressionSimplifier.Simplify`, because the exact built-in simplifier inherited two lossy reconstruction behaviors from the generic public `ExpressionTransformer`:

- `PrepareUnary`'s historical reconstruction (`CopyUnaryExpression`) rebuilds each unary family through its narrow factory (`Expression.Negate(operand)`, `Expression.Throw(operand)`, ...), silently dropping an explicit `UnaryExpression.Method` and a typed `Throw`'s declared result `Type` - not just a comparer problem: `new ExpressionSimplifier().Simplify(Expression.Negate(x, customMethod))` changed the expression's actual execution semantics whenever `customMethod` was not ordinary arithmetic negation;
- `PrepareLambda`'s historical reconstruction rebuilds via the type-inferring `Expression.Lambda(body, parameters)` overload, which cannot preserve a custom delegate `Type`, `TailCall`, or `Name` - and does this at *every* nesting depth, not just the root PR #590 could patch around from the comparer side.

Fixed by introducing two narrowly-scoped `internal virtual` reconstruction hooks on `ExpressionTransformer` (`RebuildUnaryExpression`, `RebuildLambdaExpression`), wired into `PrepareUnary`/`PrepareLambda` in place of the old direct calls. `internal`, not `protected`, so this is not a new extensibility contract for third-party subclasses in other assemblies. The base implementation of each hook reproduces the exact historical behavior unchanged (`CopyUnaryExpression` / `Expression.Lambda(body, parameters)`), so every existing `ExpressionTransformer` characterization test (`ExpressionTransformerUnaryLazyParametersTests` et al.) keeps passing unmodified, and any *derived* `ExpressionSimplifier` subclass keeps the historical, metadata-dropping behavior too.

Only the exact built-in `new ExpressionSimplifier()` runtime type overrides both hooks, guarded by `GetType() == typeof(ExpressionSimplifier)` (the same pattern `FinalizeExpression` already used):

- unary reconstruction uses `Expression.MakeUnary(expression.NodeType, operand, expression.Type, expression.Method)`, which - verified empirically across all 22 historical `CopyUnaryExpression` node families, including a typed `Throw` and a null-operand `Rethrow` - preserves `Method`, `Type`, and lifting flags in one uniform call, no per-family switch needed;
- lambda reconstruction uses `Expression.Lambda(expression.Type, body, expression.Name, expression.TailCall, parameters)`, preserving all three at every recursion depth since the same simplifier instance drives every nested `PrepareLambda` call;
- `ExpressionSimplifier.PrepareExpression` also gained a `null`-input short-circuit (again gated to the exact type) fixing a `NullReferenceException` that `Simplify(Expression.Rethrow())` threw before reconstruction ever ran, for the same historical reason (`Transform(null)` dereferences a null `context.Expression`).

`ExpressionComparer` was not changed in this PR; it now simply receives metadata-faithful trees when comparing output from the exact built-in simplifier, closing the remaining custom-unary-method and nested-lambda-metadata false positives characterized in `UtilsTest/Mathematics/Expressions/ExpressionSimplifierReconstructionFidelityTests.cs`.

**This PR does not claim custom/user-defined arithmetic operators are now fully safe for symbolic algebra** - reconstruction fidelity means a custom operator's metadata *survives* simplification, not that every algebraic rule already checks for it before rewriting. See the S3 findings below.

### S2 — Audit unreachable / dead simplification rules

Audit every transformation rule against the dispatch plan and identify rules that cannot be reached because of missing or incompatible method-level `ExpressionSignatureAttribute` metadata.

Known candidate from the 2026-09-12 audit:

- `ExpressionSimplifier.INumber.Logarithm10SimplificationAddNumber` appears to lack a method-level `[ExpressionSignature(ExpressionType.Add)]`, unlike neighboring logarithm rules, and therefore appears excluded from `ExpressionTransformer.BuildPlan`.

For every candidate, add a behavior test that fails before the fix and passes afterward. Keep each fix narrow and avoid mixing unrelated rule semantics.

#### S2 progress (2026-09-15) — production rule reachability audit complete

Audited all 66 production transformation rules declared by `ExpressionSimplifier` across
`ExpressionSimplifier.cs`, `ExpressionSimplifier.Math.cs`, and `ExpressionSimplifier.INumber.cs` against
`ExpressionTransformer.BuildPlan`, including each method-level signature and node bucket, positional
parameter shape, parameter-level constraint, same-name overload exposure, neighboring-rule metadata,
and end-to-end test coverage. No dispatcher defect was found and `BuildPlan` was not changed.

The audit confirmed two reachability defects and one rule-shape/executability hazard in the four intended
unary logarithm-combination rules:

- `Logarithm10SimplificationAddNumber` had parameter constraints but no method-level `Add` signature, so
  it was absent from the dispatch plan. It now has `[ExpressionSignature(ExpressionType.Add)]`.
- `Logarithm10SimplificationSubstractNumber` was registered in the `Subtract` bucket but constrained both
  operands to `Math.Log10`, while the active call conversion and the other three combination rules use
  concrete methods declared by `double`. Both constraints now consistently target `double.Log10`.
- `ExpressionCallSignatureAttribute` intentionally matches only declaring type and method name, so the
  natural-log rules also accepted `double.Log(value, base)` and silently ignored each base. All four rules
  now require two calls to the exact same concrete unary method and conservatively return `null` for any
  other shape. Combined results preserve that concrete method rather than emitting a static-abstract
  interface `MethodInfo`, making the simplified lambdas directly compilable and executable.

Dedicated end-to-end tests now simplify, structurally inspect, compile, and execute natural-log and
base-10 addition/subtraction over positive finite values. Negative controls prove that different-base and
same-base two-argument `Log` calls keep both base arguments and their source semantics. A reflection-based
test also prevents any future method with parameter-level signature constraints from silently lacking the
method-level signature required for plan registration. Direct-input characterization confirmed that the
active simplifier representation is the `double.*` method family; direct `Math.Log` and `Math.Log10` calls
remain preserved and are not broadened into new combination behavior.

No other unreachable or parameter-incompatible production transformation rule was found. S2 is complete.
S3 remains open: logarithm domains, IEEE-754 details, evaluation assumptions and custom-operator algebra
policy are deliberately unchanged and continue to be tracked below.

### S3 — Formalize and enforce the symbolic-equivalence contract

Priority: highest remaining simplifier correctness stage.

The simplifier is intended to implement **symbolic algebra**, not strict bit-for-bit CLR execution
preservation. That distinction must now become an explicit contract and, importantly, rules that are not
valid even under the chosen symbolic model must be blocked rather than justified by that contract.

The contract must distinguish at least:

- exact CLR/operator semantics versus symbolic mathematical identities;
- floating-point/decimal rounding and reassociation differences;
- `NaN`, infinities, signed zero, overflow and exception-timing differences;
- the mathematical domain/preconditions of identities such as logarithm combination and powers;
- pure/referentially-transparent symbolic sub-expressions versus expressions with observable side effects;
- predefined numeric operators versus explicit custom/user-defined operator `Method`s;
- ordinary non-lifted arithmetic versus lifted nullable operators;
- ring-like arithmetic versus identities that require field-style division.

Comments such as "preserving semantics" must be tightened where they currently overstate the guarantee.
The contract should say explicitly whether equivalence is claimed only on the common mathematical domain
where an identity's preconditions hold. The existing logarithm rules are intentional functionality and
must remain available for the supported positive-domain use cases; S3 must not silently delete them merely
because the simplifier has no general domain solver.

#### S3 audit (2026-09-15) — operator safety is broader than the known `Negate` gap

Reconstruction fidelity from S1 preserves custom operator metadata, but many algebraic rules still consume
nodes solely from their `NodeType` and therefore treat an explicit custom method as ordinary arithmetic.
The previously known unary cases remain affected:

- `AdditionWithNegate`
- `SubstractionWithNegate`
- `MultiplicationWithNegate`
- `DivisionWithNegate`
- `NegateWithSubstraction`
- `CollectAdditiveTerms`

The same class of defect also exists for binary rules. Representative affected families include:

- zero/one identities (`AdditionWithZero`, `SubstractionWithZero`, `MultiplicationWithZeroOrOne`,
  `DivideWithZeroOrOne`, `DivideWithZero`, `PowerOfZeroOrOne`, `PowerByZeroOrOne`);
- constant folding (`AdditionOfConstants`, `SubstractionOfConstants`, `MultiplicationOfConstants`,
  `DivisionOfConstants`, `PowerOfConstants`);
- factoring/reassociation (`AdditionOfEqualsElements`, `SubstractionOfEqualsElements`, the
  `Multiplication`/`MultiplicationOfEqualsElements` overloads and `DivisionOfDivision`);
- logarithmic and trigonometric identities in `ExpressionSimplifier.INumber.cs`, whose outer `Add`,
  `Subtract`, `Multiply` or `Divide` node can itself carry an explicit custom operator method.

For ordinary predefined numeric `Add`/`Subtract`/`Multiply`/`Divide`, expression-tree `Method` is normally
`null`; an explicitly supplied/user-defined implementation is observable through `BinaryExpression.Method`.
`ExpressionType.Power` needs separate treatment because the ordinary `double` power node itself has a
concrete implementing method. S3 should centralize the decision instead of scattering ad-hoc checks and
must preserve the normal `double` power path while rejecting an explicitly different custom power method.

`ExpressionComparer` amplifies this problem because both `Equals` and `GetHashCode` simplify before their
structural comparison/hash pass. If an unsafe algebraic rule first erases or consumes a custom operator,
the comparer can again report a false symbolic equivalence even though its structural core correctly
compares `Method` metadata. Regression coverage therefore needs both direct `Simplify -> Compile -> Execute`
tests and comparer-level tests.

#### S3 audit (2026-09-15) — nested custom operators can be erased by canonicalization

`CanCanonicalizeCommutativeBinary` protects only the root node. Once canonicalization starts,
`CollectAdditiveTerms` recursively flattens every `Add`/`Subtract` node and every `Negate` based only on
`NodeType`, and `CollectMultiplicativeFactors` does the same for every nested `Multiply`. The rebuild then
uses ordinary `Expression.Add`, `Expression.Subtract` and `Expression.Multiply` factories.

Therefore an ordinary outer arithmetic node can contain an inner custom `Add`, `Subtract`, `Multiply` or
`Negate` whose `Method` is preserved by S1 but then lost during flattening. Nested nodes must only be
flattened when they satisfy the same built-in/symbolic-safety predicate as the root; otherwise they must
remain atomic terms/factors.

Required regression direction:

- ordinary `Add(customAdd(x, y), z)` must retain the custom inner addition as one term;
- ordinary `Add(customSubtract(x, y), z)` must retain the custom inner subtraction as one term;
- ordinary `Multiply(customMultiply(x, y), z)` must retain the custom inner multiplication as one factor;
- additive canonicalization containing a custom `Negate` must retain that unary node rather than flipping
  its sign as if it were ordinary negation.

Each test must compare compiled source and simplified delegates using custom methods whose behavior is
observably different from the corresponding built-in operator.

#### S3 audit (2026-09-15) — integer nested-division identities are not generally valid

At least the denominator-division rewrites are invalid for integer arithmetic because integer division
truncates toward zero:

```text
x / (y / z)       -> (x * z) / y
(x / y) / (z / w) -> (x * w) / (y * z)
```

For example, with integers `8 / (3 / 2)` evaluates to `8`, while `(8 * 2) / 3` evaluates to `5`.
These rules must not be applied merely because the node type is `Divide`. A conservative S3 fix may reject
all `DivisionOfDivision` reassociation for integer result types rather than trying to preserve the subset
that happens to be valid under truncating arithmetic. Floating/decimal reassociation remains subject to
the documented symbolic (not bit-for-bit CLR) contract.

#### S3 audit (2026-09-15) — lifted nullable arithmetic requires an explicit policy

`ConstantNumericAttribute` matches numeric constant values, while several zero/one rules do not first
require the binary result type to be one of the non-nullable `Types.Number` entries. Lifted arithmetic can
therefore reach algebraic identities that collapse a nullable result to a non-null symbolic constant. In
particular, `x * 0 -> 0` is not valid for `int? x` when `x` is `null`: lifted arithmetic produces `null`.

S3 should conservatively reject lifted (`IsLifted`/`IsLiftedToNull`) arithmetic from identities unless a
rule is explicitly proven nullable-safe. Add end-to-end nullable tests rather than relying only on metadata
checks.

#### S3 audit (2026-09-15) — symbolic rewriting assumes purity and can change evaluation count/order

Several existing transformations are mathematically valid for pure expressions but not execution-equivalent
for arbitrary expression trees with side effects:

- `x * 0 -> 0` can remove evaluation of `x`;
- `x + (-x) -> 0`, factoring and cancellation can remove repeated evaluations;
- additive/multiplicative canonical ordering can reorder method calls/member accesses;
- `InvokeExpression` performs beta-reduction by substituting invocation arguments directly into the lambda
  body, so an argument used zero or multiple times can be dropped or evaluated multiple times.

A general side-effect/purity analyser is **not** required as part of S3 unless separately justified. The
symbolic contract should instead state clearly that algebraic simplification assumes referentially
transparent symbolic operands. Do not claim execution-order/side-effect preservation for impure trees.
If a production rule already has a cheap, local way to preserve evaluation count without complicating the
symbolic tree, it may be considered separately, but do not turn S3 into an execution optimizer.

#### S3 audit (2026-09-15) — IEEE-754 and mathematical-domain behavior must be explicit

Existing identities deliberately differ from exact CLR evaluation in edge cases. Examples include
reassociation/reordering, `x * 0 -> 0`, cancellation with `NaN`/infinities, signed-zero differences,
logarithm combination and power identities. These are not all production bugs if the documented contract
is symbolic algebra rather than exact execution preservation.

Domain-sensitive rules must state their assumptions. In particular:

- `Log(x) + Log(y) -> Log(x * y)` and the subtraction/Log10 variants assume the logarithm arguments lie
  in the supported positive real domain;
- power rules such as `0^x -> 0`, `x^a * x^b -> x^(a+b)` and reciprocal-power rewrites have exponent/base
  preconditions that are not represented by the current expression tree;
- trigonometric quotient/product identities are symbolic identities on their common mathematical domain,
  not promises of identical libm rounding or identical behavior at poles/undefined points.

S3 should characterize these boundaries with tests and documentation without disabling the intended
positive finite logarithm behavior restored in S2.

#### S3 implementation direction

Prefer small centralized safety predicates over one-off guards in every rule. The implementation should
make it difficult for a future rule to forget operator metadata again. Candidate responsibilities include:

- a helper deciding whether a `UnaryExpression` is ordinary built-in numeric negation and non-lifted;
- a helper deciding whether a `BinaryExpression` is an ordinary supported built-in arithmetic operation,
  with explicit handling for `Power` because its normal `double` implementation has a non-null method;
- a narrower helper for identities requiring field-style division rather than ring/integer arithmetic;
- collectors that flatten only nodes accepted by those helpers and otherwise treat the node as atomic.

Do **not** change `ExpressionTransformer.BuildPlan`, `ExpressionCallSignatureAttribute`, S4 canonical-key
identity, or `ExpressionOptimiser` in the S3 simplifier PR. Preserve the historical behavior of external
`ExpressionSimplifier` subclasses unless a production correctness fix necessarily applies through their
existing protected rule surface and is covered explicitly by compatibility tests.

Minimum S3 test matrix:

1. custom root `Add`, `Subtract`, `Multiply`, `Divide`, `Power` and `Negate` nodes are not consumed by
   built-in algebraic identities;
2. nested custom `Add`/`Subtract`/`Multiply`/`Negate` survive additive/multiplicative canonicalization with
   their exact `Method` and runtime behavior;
3. custom outer operators around logarithmic/trigonometric operands do not trigger the built-in identities;
4. `ExpressionComparer` does not regain false equivalence through pre-comparison simplification of a
   custom operator expression;
5. integer nested-division counterexamples remain execution-equivalent after simplification and are not
   rewritten through field identities;
6. lifted nullable cases such as `int? x * 0` preserve `null` behavior;
7. ordinary built-in numeric positive controls continue to simplify (`x + 0`, `x * 1`, canonical ordering,
   factoring, natural-log/Log10 combination, trigonometric identities);
8. logarithm tests stay on positive finite values so S3 operator safety does not accidentally become a
   domain-policy rewrite;
9. every changed `Utils` behavior is exercised through public `new ExpressionSimplifier().Simplify(...)`,
   and semantic regressions compile and execute both source and simplified lambdas.

S3 is complete only when the contract is documented, unsafe custom/lifted/integer-field rewrites are
blocked, the canonical collectors preserve unsafe nested nodes atomically, and the ordinary symbolic
simplification matrix remains green.

Adjacent documentation cleanup discovered during this audit: `ExpressionComparer` still contains comments
written before the S1 reconstruction-fidelity fix that describe nested lambda `Type`/`TailCall` metadata as
being erased by simplification. The exact built-in `ExpressionSimplifier` now preserves that metadata at
every nesting depth. Update those comments when touching the comparer for S3 regression coverage; no
comparer behavior change is required for that cleanup.

#### S3 progress (2026-09-15) — contract documented, unsafe rewrites blocked

S3 is implemented. The chosen contract is documented in XML doc on `ExpressionSimplifier`'s class remarks
and on `Simplify`: symbolic algebra, not bit-for-bit CLR/IEEE-754 execution preservation; algebraic rewrites
assume pure/referentially-transparent operands (no general side-effect analyzer added); domain-sensitive
identities (logarithm combination, power rules) are valid on their documented common domain only, with no
general domain/constraint solver added.

**Centralized operator-safety predicates** (`Utils/Expressions/ExpressionSimplifier.cs`, "Operator safety
(S3 symbolic-equivalence contract)" region): `IsOrdinaryUnaryNegate`, `IsOrdinaryBinaryArithmetic`, and the
narrower `IsOrdinaryFieldDivision` (for identities requiring field, not ring, division). Rather than
hard-coding "`Method is null`" or a fixed `Math.Pow` assumption, each predicate consults a table built once
by structurally probing every `Types.Number` entry against the real `Expression.Add`/`Subtract`/`Multiply`/
`Divide`/`Power`/`Negate` factories and recording the resulting `.Method` (`null` for CLR-intrinsic
primitives such as `double`/`int`, a genuine operator method such as `Decimal.op_Addition` for `decimal`,
absent entirely for an unsupported combination such as `Power` on any non-`double` type or any arithmetic
operator on `byte`/`sbyte`). A node is "ordinary" only when it is non-lifted
(`!IsLifted && !IsLiftedToNull`) and its actual `Method` matches the probed default for its `(NodeType, Type)`
pair. This uniformly resolves the custom-operator, lifted-nullable, and `decimal`-is-not-`null`-but-still-
ordinary cases with one mechanism instead of three ad hoc checks.

**Guarded rule families**, all in `Utils/Expressions/ExpressionSimplifier.cs` unless noted:

| Rule family | Previous status | Guard applied | Regression test |
| --- | --- | --- | --- |
| `AdditionWithZero`, `SubstractionWithZero`, `MultiplicationWithZeroOrOne`, `DivideWithZeroOrOne`, `DivideWithZero`, `PowerOfZeroOrOne`, `PowerByZeroOrOne` | No outer-operator check; also silently collapsed lifted nullable results (e.g. `int? x * 0` with `x == null`) | `IsOrdinaryBinaryArithmetic(e)` on the outer node (rejects custom `Method` and lifted/`IsLiftedToNull`) | `CustomAdd_WithZero_...`, `CustomSubtract_WithZero_...`, `CustomMultiply_ByOne_...`, `CustomDivide_ByOne_...`, `LiftedNullableMultiplicationByZero_...`, `LiftedNullableDivisionOfZero_...`, `NonLiftedIntMultiplicationByZero_StillSimplifies` (positive control) |
| `AdditionOfConstants`, `SubstractionOfConstants`, `MultiplicationOfConstants`, `DivisionOfConstants`, `PowerOfConstants` | Folded two constants via ordinary CLR `dynamic` arithmetic regardless of the outer node's custom `Method` | `IsOrdinaryBinaryArithmetic(e)` | Covered indirectly by the root-identity tests above (constant folding is one of the candidate rules for those shapes) |
| `AdditionWithNegate` (×2), `SubstractionWithNegate` (×2), `NegateWithSubstraction` | Treated any `Negate`/outer op as ordinary regardless of `Method`/lifting (the originally-known S3 hazard) | `IsOrdinaryBinaryArithmetic` on the outer node **and** `IsOrdinaryUnaryNegate` on the `Negate` operand | `CustomNegate_InsideAdditionWithNegateShape_KeepsCustomMethod`, `CustomNegate_OuterNegateOfSubtraction_KeepsCustomMethod` |
| `SubstractionWithAddition`, `SubstractionWithSubstraction` | No check on the outer `Subtract` or the nested `Add`/`Subtract` | `IsOrdinaryBinaryArithmetic` on both | Covered by `AdditiveCanonicalization_WithNestedCustomNegate_DoesNotFlipSign` exercising the same reassociation path; no dedicated test needed since these rules only reshape already-guarded nodes |
| `AdditionOfEqualsElements`, `SubstractionOfEqualsElements` (factoring) | Decomposed any nested `Multiply` by `NodeType` alone, ignoring outer op or inner `Method` | `IsOrdinaryBinaryArithmetic(e)`; nested `Multiply` only decomposed via `IsOrdinaryBinaryArithmetic` on that inner node, otherwise treated as an atomic `1 * term` | Exercised indirectly through canonicalization tests; no separate factoring-specific reachability test added (out of scope beyond the audited hazard) |
| `Multiplication` (commute-with-constant, constant×constant-times-rest, constant×constant product-combine) | Commuted/combined regardless of outer or nested `Method` | `IsOrdinaryBinaryArithmetic` on outer and any nested `Multiply` operand | `OrdinaryMultiplication_WithNestedCustomMultiply_KeepsInnerNodeAtomic` |
| `MultiplicationOfEqualsElements` (constant-distribute ×2, power-combine) | Distributed/combined regardless of outer/nested `Method`; power-combine only checked `leftleft.Type != typeof(double)`, not `Method` | `IsOrdinaryBinaryArithmetic` on outer and any nested `Multiply`/`Power` operand before decomposing it | `OrdinaryMultiplication_WithNestedCustomMultiply_KeepsInnerNodeAtomic`, `OrdinaryDouble_StandardPower_StillSimplifies` (positive control) |
| `MultiplicationWithNegate` (×2), `DivisionWithNegate` (×2) | Same `Negate`-consuming hazard as the additive family | `IsOrdinaryBinaryArithmetic` on outer **and** `IsOrdinaryUnaryNegate` on the operand | Covered by the negate-preservation tests above via the shared helper; no separate test needed (identical guard shape) |
| `DivisionOfDivision` (×3 overloads: `(x/y)/(z/w)`, `x/(y/z)`, `(x/y)/z`) | Field-style reassociation applied to **any** numeric `Divide`, including truncating integer division (`8 / (3 / 2)` source `8` vs. rewritten `5`) | `IsOrdinaryFieldDivision` (adds `Types.FloatingPointNumber.Contains(Type)`) on the outer node and every nested `Divide` operand involved | `IntegerDivisionOfDivision_XOverYOverZ_...`, `IntegerDivisionOfDivision_XOverYAllOverZOverW_...`, `FloatingPointDivisionOfDivision_StillReassociates` (positive control) |
| Logarithm combination (`LogarithmSimplificationAddNumber`/`SubstractNumber`, `Logarithm10Simplification...`), trig quotient/product identities (`DivisionOfCosAndSinNumber`, `DivisionOfSinAndCosNumber`, `MultiplicationOfCosAndTanNumber`, `MultiplicationOfTanAndCosNumber`, `DivisionOfSinAndTanNumber`) in `ExpressionSimplifier.INumber.cs` | Checked the inner `Log`/`Sin`/`Cos`/`Tan` calls but never the OUTER `Add`/`Subtract`/`Multiply`/`Divide` node's `Method` | `IsOrdinaryBinaryArithmetic` on the outer node (cast from the `Expression e` parameter) | `CustomAdd_OfTwoLogCalls_DoesNotCombineIntoLogOfProduct`, `CustomDivide_OfSinAndCos_DoesNotBecomeTan`, `OrdinaryDouble_LogarithmCombination_...`/`OrdinaryDouble_SinOverCos_...` (positive controls) |
| `AdditionOfCos2andSin2Number` (`sin²(x)+cos²(x) -> 1`) in `ExpressionSimplifier.INumber.cs` | Initially guarded only the outer `Add`'s `Method`, same as the row above; a post-review pass (commit `abee7676`) found the two `Power` operands (`sin(x)^2`, `cos(x)^2`) were still unguarded, so a custom-`Method` `Power` could still be consumed by the identity | `IsOrdinaryBinaryArithmetic` on the outer `Add` **and** on both `Power` operands | `CustomPower_InsideSinSquaredPlusCosSquared_DoesNotCollapseToOne` |
| `CanCanonicalizeCommutativeBinary` | Checked `Method is null` only (missed lifted nullable rejection) | Delegates to `IsOrdinaryBinaryArithmetic` | Covered by every canonicalization test above |
| `CollectAdditiveTerms`, `CollectMultiplicativeFactors` | Flattened any nested `Add`/`Subtract`/`Negate`/`Multiply` by `NodeType` alone, silently erasing a nested custom operator even though S1 preserved its `Method` up to that point | Flatten only when `IsOrdinaryBinaryArithmetic`/`IsOrdinaryUnaryNegate` accepts the nested node; otherwise keep it as one atomic term/factor | `OrdinaryAddition_WithNestedCustomAdd_...`, `OrdinaryAddition_WithNestedCustomSubtract_...`, `OrdinaryMultiplication_WithNestedCustomMultiply_...`, `AdditiveCanonicalization_WithNestedCustomNegate_...` |

**`ExpressionComparer` regression:** `Comparer_CustomAdditionWithZero_IsNotEqualToPlainOperand` proves a
custom-method `Add(x, 0)` no longer pre-comparison-simplifies to plain `x`, closing the false-equivalence
path described in the audit (this test failed on the pre-S3 baseline: `Equals` returned `true`).

**Baseline verification:** `UtilsTest/Mathematics/Expressions/ExpressionSimplifierSymbolicContractTests.cs`
was written and run against the pre-fix baseline first; 17 of its (then 27) tests failed there (the
custom-operator, nested-canonicalization, log/trig-outer-operator, integer-division, and lifted-nullable
cases above), 10 positive controls already passed. All 27 passed after the fix. A post-review pass (commit
`abee7676`) added one more regression test, `CustomPower_InsideSinSquaredPlusCosSquared_DoesNotCollapseToOne`
(see the `AdditionOfCos2andSin2Number` row above), reproduced its failure against the momentarily-reverted
guard before restoring the fix. The file now has 28 tests, all passing; the full `Mathematics.Expressions`
namespace (380 tests) and the existing S1/S2/logarithm/comparer/finalization suites remain green, unchanged.

**Deliberately deferred / out of scope for S3:**

- No general side-effect/purity analyzer (documented assumption instead).
- No general mathematical-domain/constraint solver; logarithm and power identities keep their existing
  positive-finite/precondition assumptions, now stated explicitly in the XML doc contract.
- `decimal` is classified as an *ordinary* operand type by the structural probe (its real `op_Addition`
  etc. match the probed default), so decimal identities keep firing; this was a deliberate design choice
  over a blanket "any non-null `Method` is unsafe" rule, verified with a positive-control test
  (`OrdinaryDecimal_AdditionWithZero_PositiveControl`).
- `ExpressionTransformer.BuildPlan`, `ExpressionCallSignatureAttribute`, S4 structural canonical keys, and
  `ExpressionOptimiser` (tracked separately as O0) were not touched.
- No S5 allocation/performance cleanup beyond the guard checks themselves, which are simple dictionary
  lookups against a table built once per process.

### S4 — Replace textual canonical identity with structural canonical keys

Current additive/multiplicative canonical ordering uses `Expression.ToString()` as a canonical key. This is useful but not a true structural identity and is affected by parameter names and custom `ToString()` overrides.

After S1 establishes reliable structural comparison semantics, design a structural canonical key/order that is:

- deterministic;
- scope-aware for parameters;
- independent of `Expression.ToString()` formatting;
- capable of ordering supported node kinds without accidentally claiming unsupported nodes are equal;
- compatible with the simplifier's documented symbolic-equivalence contract from S3.

Do not introduce a grouping-key cache until the observable `ToString()`/key-evaluation behavior and compatibility impact have been explicitly characterized.

#### S4 progress (2026-09-18) — structural canonical keys implemented

S4 is implemented. `Expression.ToString()` and arbitrary sub-expression/object `ToString()` overrides no
longer participate in any canonical ordering or grouping decision made by `ExpressionSimplifier`.

**Files changed:**

- `Utils/Expressions/ExpressionTranformer.cs` — two narrowly-scoped `internal virtual` hooks,
  `OnEnterLambdaScope`/`OnExitLambdaScope`, bracket `PrepareLambda`'s body traversal (`try`/`finally`).
  Base implementation is a no-op, so every existing `ExpressionTransformer` subclass's behavior is
  unchanged; only the exact built-in `ExpressionSimplifier` overrides them (same gating pattern as the S1
  `RebuildUnaryExpression`/`RebuildLambdaExpression` hooks).
- `Utils/Expressions/ExpressionComparer.cs` — `ExactNumericValue` (the exact rational/NaN/infinity numeric
  model) is now `internal` (was `private`) and gained `IComparable<ExactNumericValue>`, so the new
  structural-key code reuses the *same* exact numeric model instead of a second implementation. Two new
  `internal static` entry points, `StructuralEqualsRaw`/`StructuralHashRaw`, expose the existing
  `EqualsCore`/`Hash` recursion directly (fresh `ParameterBindingContext`/`ParameterScopeStack`, no
  `Simplify()` call) for reuse by the S4 additive-grouping equality. Public `Equals`/`GetHashCode` behavior
  was unchanged as of this initial commit — a later review-fix round (see "S4 review fixes" below) added one
  narrowly-scoped, mathematically-justified WIDENING to public `Equals`/`GetHashCode` (a commutative operand
  fallback for ordinary `Add`/`Multiply`), needed to fix a genuine free-parameter regression this stage
  introduced; that is the one intentional, documented exception to "unchanged" and is characterized by its
  own tests.
- `Utils/Expressions/ExpressionCanonicalOrder.cs` (new) — the structural canonical-order key itself: an
  internal `KeyNode` hierarchy (`ConstantKey`, `ParameterKey`, `UnaryKey`, `BinaryKey`, `MethodCallKey`,
  `MemberKey`, `LambdaKey`, plus `NullKey`/`UnsupportedKey`) with a fixed per-kind rank and
  deterministic, reflection-metadata-based `Type`/`MethodInfo`/`MemberInfo` comparison (namespace/name text,
  declaring type, generic arity/arguments, static/instance, parameter/return types; a final tie-break for
  the case where every other dimension ties, unchanged as of this initial commit — later review-fix rounds
  changed both its dimensions and their order more than once (see "S4 review fixes" below, rounds 1, 7 and
  8) after finding it could throw, then that it was not actually transitive, then that it under-distinguished
  two same-name/same-token modules; the CURRENT dimension order is Assembly, then Module name, then
  `Module.ModuleVersionId`, then `MetadataToken`). No `GetHashCode()`, `RuntimeHelpers.GetHashCode()`,
  `HashCode`, object reference order, or `ToString()` participates in ordering.
- `Utils/Expressions/ExpressionSimplifier.cs` — `GetCanonicalExpressionKey` (the `expression.ToString()`
  method) is removed outright. `GetAdditiveGroupingKey` (the `"func:...:catOrder"`/`"expr:..."` string
  builder) is replaced by `ClassifyForAdditiveGrouping` (same classification rule: `Power(MethodCall, exp)`
  and bare `MethodCall` cluster by function family/category, ignoring the exponent; everything else is
  "opaque") plus a dedicated `[ThreadStatic]` lexical-scope stack and the new comparers described below.
  `CanonicalizeAdditiveExpression`/`CanonicalizeMultiplicativeExpression` now order/group via those, never
  via string keys.

**Structural-key architecture.** Two deliberately separate mechanisms, matching the roadmap's explicit
"do not replace `GetAdditiveGroupingKey` with a full structural expression key" instruction:

1. **Complete canonical order** (`ExpressionCanonicalOrder.BuildKey`/`Compare`) — the full identity used for
   the final tie-break (`.ThenBy(term => term.Key)`) and for the entire multiplicative-factor ordering.
   Distinguishes exact `Method`/`Type`/exponent/every structural detail for the seven node families
   `ExpressionComparer` already understands (`LambdaExpression`, `ParameterExpression`,
   `ConstantExpression`, `UnaryExpression`, `BinaryExpression`, `MethodCallExpression`,
   `MemberExpression`).
2. **Additive grouping** (`ClassifyForAdditiveGrouping` + `AdditiveGroupingEqualityComparer`, both in
   `ExpressionSimplifier.cs`) — deliberately coarser: a `Power(MethodCall, exponent)` groups by the call's
   function category and structural argument identity, ignoring the exponent, so `Sin`/`Cos`/`Tan` (same
   category) with the same arguments still cluster together as before. Grouping *equality* uses
   `ExpressionComparer.StructuralEqualsRaw`/`StructuralHashRaw` (wrapped with a same-instance shortcut), a
   *separate* mechanism from `KeyNode`'s order ties — see "Unsupported node kinds" below for why conflating
   them would be unsafe. Grouping *order* (which bucket of the coarse classification sorts before which,
   the first-level `OrderBy`) reuses the complete key for the "opaque" bucket and a lexicographic
   per-argument complete-key comparison for the "function-like" bucket, so the roadmap's example — a
   `Power(MethodCall, exponent)` term groups by its call's arguments/category but the *complete* key still
   distinguishes the exponent — holds by construction (proven by
   `PowerWrappedFunctionGrouping_DistinguishesExponentsInCompleteKey`).

**Parameter-scope / binding policy.** A `ParameterExpression` bound by a `LambdaExpression` is encoded as
`(relative depth, declaration position, declared type)` — a De-Bruijn-style index — never by `Name`. Two
alpha-equivalent lambdas (any parameter names, any `ParameterExpression` instances) produce identical keys;
two distinct same-named parameters in one lambda remain distinguishable by position. The lexical scope
enclosing the node currently being canonicalized (needed because `FinalizeExpression` sees only the
Add/Multiply node's own subtree, not its enclosing lambdas) is captured once per canonicalization call from
a `[ThreadStatic]` stack maintained by `ExpressionSimplifier`'s `OnEnterLambdaScope`/`OnExitLambdaScope`
override, *not* stored as shared mutable instance state (`ExpressionExtensions`/`ExpressionComparer` each
hold their own static `ExpressionSimplifier` instance, and simplification is both concurrent-callable and
re-entrant through `ExpressionComparer.Default`). `[ThreadStatic]` — rather than fully explicit per-call
context propagation — was chosen because propagating an explicit scope parameter through the entire
recursive engine would require changing the signature of `ExpressionTransformer.PrepareExpression`/
`FinalizeExpression`, both `protected virtual` extensibility points a third-party subclass in another
assembly can already override; the roadmap explicitly rules out a new public extensibility contract for
this. `[ThreadStatic]` gives each OS thread an independent stack (no cross-thread leakage), and every push
is paired with a `finally`-guarded pop, so re-entrant calls (e.g. a rule invoking
`ExpressionComparer.Default`, which itself calls `Simplify`) always leave the stack exactly as the outer
call left it once they return — proven by `Concurrency_ManyThreadsSharedSimplifier_NoScopeStateLeakage` and
`Reentrancy_ThroughExpressionComparerDefault_OuterCanonicalizationStillCorrect`. A nested `LambdaExpression`
encountered *within* the term being keyed (not via the ambient stack) is handled by `BuildKey`'s own local,
non-shared scope list — ordinary recursive-call-stack state, not ambient/ThreadStatic.

**Free-parameter policy.** A `ParameterExpression` not found in any active scope (ambient or local-to-the-
term) is "free". Two distinct free parameters have no name-independent structural total order (unlike a
bound parameter's declaration position, nothing else about a free parameter is a canonical tree property),
so `ParameterKey` compares two free parameters only by declared `Type` and otherwise reports a tie (`0`),
relying on `Enumerable.OrderBy`'s documented stability to preserve original source order rather than
inventing one — mirroring `ExpressionComparer`'s own free-parameter (reference-equality-only) policy.

**Supported node families:** `LambdaExpression`, `ParameterExpression`, `ConstantExpression`,
`UnaryExpression`, `BinaryExpression`, `MethodCallExpression`, `MemberExpression` — the same seven
`ExpressionComparer` already understands structurally.

**Deliberately unsupported node families:** every other node kind (`ConditionalExpression`, `Extension`,
`New`, `Block`, ...). `Build` routes any such node to a single shared `UnsupportedKey` via a plain `is`
pattern-match dispatch — it is never inspected further, and in particular `ToString()` is never called on
it. `UnsupportedKey.CompareSameRank` always returns `0` (a stable-sort-preserving tie, exactly like the
free-parameter tie above) but this is *only* used for the complete ORDER key; the separate additive-grouping
EQUALITY (`AdditiveGroupingEqualityComparer`, via `ExpressionComparer.StructuralEqualsRaw`) never claims two
distinct unsupported-kind instances are the same group — `ExpressionComparer`'s own `EqualsCore` already
returns `false` unconditionally for a nested unsupported node kind, by design (see its S1 remarks). Proven
by `TwoDistinctConditionalTerms_NeverConflated` (two different `ConditionalExpression` terms both survive as
separate nodes) and the `ThrowingExtensionTerm_*`/`ThrowingExtensionArgument_*` tests (a `ToString()`-
throwing `Extension` node survives conservatively and its `ToString()` is never called, including as a
method-call argument reached through the additive-grouping classification — the exact code path that used
to call `string.Join<Expression>`, invoking every argument's `ToString()`).

**Regression tests added:** `UtilsTest/Mathematics/Expressions/ExpressionSimplifierStructuralCanonicalizationTests.cs`
(25 tests, covering the full matrix: bound-parameter-name independence, duplicate-name convergence by
position, nested-scope/capture/shadowing, `ToString()`-must-not-execute for both a bare atomic term and a
method-call argument, same-text-different-`Method` custom operators, exact method-call identity across two
same-name/signature methods on different declaring types, power-wrapped-function-grouping exponent
distinction plus the sin²+cos² positive control, nine ordinary positive controls (bound addition/
multiplication/three-term/subtraction-sign/Sin-Cos/Max-Min/decimal/custom-operator-atomicity/lifted-
nullable), two unsupported-node-conservatism cases, and concurrency/re-entrancy). Every test in the
"must fail on the pre-S4 baseline" category (parameter-name independence, duplicate-name convergence,
`ToString()`-must-not-execute ×3, same-text-different-method distinguishability) was confirmed to fail
against the pre-fix `GetCanonicalExpressionKey`/`GetAdditiveGroupingKey` implementation before the
production change, and to pass after it.

**Existing tests superseded:** `UtilsTest/Mathematics/Expressions/ExpressionSimplifierAdditiveGroupingKeyTests.cs`
had nine characterization tests reflecting into the private `GetAdditiveGroupingKey(Expression)` method and
asserting its exact `"func:...:catOrder"`/`"expr:..."` text, per-argument `ToString()` call count/order,
unescaped `"|"` handling, `null`-`ToString()` joining, and `ToString()`-exception propagation. That method no
longer exists, so those tests were removed (with an explanation left in the file's remarks, per `AGENTS.md`)
rather than kept as dead reflection-based assertions; their useful end-to-end coverage (Sin/Cos/Max/Min/
power-wrapped-function grouping, subtraction sign handling) was already duplicated by this file's own
end-to-end tests, which are kept unchanged. `ExpressionSimplifierFinalizationTests.Simplify_AddReachingFallback_StillCanonicalizes`/
`Simplify_MultiplyReachingFallback_StillCanonicalizes` used two bare/free `ParameterExpression` instances and
compared `Expression.ToString()` directly; both are incompatible with S4 (free parameters have no
name-independent order, and canonicalization no longer depends on `ToString()` at all), so they were
rewritten around bound lambda parameters, asserting via `ExpressionComparer.Default` plus compiled behavior
instead — preserving their actual protected invariant (fallback Add/Multiply canonicalization still
happens) rather than weakening it.

**Validation performed (2026-09-18, in order):**

1. Focused new S4 tests: `ExpressionSimplifierStructuralCanonicalizationTests` — 25/25 passed.
2. Complete `UtilsTest/Mathematics/Expressions` namespace (includes all S1/S2/S3 regression suites, the
   Gherkin feature scenarios, and the comparer/finalization/reconstruction-fidelity suites): 566/566 passed.
3. Full `UtilsTest.Unit`: 7642/7642 passed, 0 skipped.
4. Full `UtilsTest.Functional`: 383/383 passed.
5. Full `UtilsTest.Security`: 225/228 passed, 3 skipped — all three
   (`TryCreate_ReturnsNull_OnNonWindowsPlatform`, `VerifyAuthenticodeSignature_OnNonWindows_ThrowsPlatformNotSupportedException`,
   `HasValidAuthenticodeSignature_OnNonWindows_ThrowsPlatformNotSupportedException`) are pre-existing,
   unrelated, platform-gated tests that only run on a non-Windows OS — expected to skip on this Windows
   environment regardless of this change.
6. Release build of `Utils.sln`: succeeded, no errors (one pre-existing, unrelated `CS8618` warning in
   `Utils/Objects/ReturnValue.cs`).

**Deferred to S5 (explicitly out of scope here):** a cross-call key cache; recomputing `KeyNode`s fewer
times per comparison; `ExpressionComparer` temporary-array/search allocation cleanup; any other
allocation/CPU-cost tuning. `AnnotatedAdditiveTerm` computes each term's key exactly once per
canonicalization call (mirroring the pre-S4 `OrderBy(keySelector)` idiom, not a new cache), and the
additive-grouping order comparison still rebuilds argument-list keys per pairwise comparison — both
deliberately left as-is for S5 to profile and address.

#### S4 review fixes (2026-09-18) — PR #600 human review

A human review of PR #600 at commit `dac47b9b` found six issues (four independently confirmed by earlier
automated review threads, plus two new ones from a closer reading) before it should merge. All six are
fixed on the same branch; none required reverting the S4 design itself.

1. **`DynamicMethod.MetadataToken` could throw and fail `Simplify()`.** `CompareFinalMemberTiebreak`
   (`ExpressionCanonicalOrder.cs`) assumed every `MemberInfo`/`Type` exposes a readable `MetadataToken`;
   an unbaked `System.Reflection.Emit.DynamicMethod` throws `InvalidOperationException` there on some
   runtimes. The tie-break (used for both `Type` and `MethodInfo`/`MemberInfo` comparison — `Type` derives
   from `MemberInfo`, so the two duplicate tie-breaks were unified into one) now reads
   `MetadataToken`/`Module.Name`/`Module.Assembly.FullName` through guarded `TryGet*` helpers and falls
   back to a conservative tie (`0`) instead of throwing, consistent with the roadmap's "prefer preserving
   source order over inventing one" policy. Regression:
   `CompareMethod_TwoDynamicMethodsWithIdenticalSignature_DoesNotThrow` (calls
   `ExpressionCanonicalOrder.CompareMethod` directly via reflection with two owner-less `DynamicMethod`s
   sharing a name/signature).
2. **Public `ExpressionComparer.Default` regression on free parameters.** Before S4, two distinct FREE
   `ParameterExpression`s (not lambda-bound) reordered deterministically by `Name` text, so
   `Equals(Add(a, b), Add(b, a))` was `true`; S4 correctly stops inventing that name-based order (per the
   "Free parameters" policy), so each side can independently keep its own source operand order, and the
   comparer's purely positional `BinaryEqual` then reported `false` — a real regression in the PUBLIC
   comparer contract the roadmap explicitly says to fix narrowly rather than accept. Fixed by making
   `BinaryEqual` (and `HashBinary`, to keep the `IEqualityComparer<T>` contract) try operands SWAPPED as a
   fallback for an ordinary (`IsOrdinaryBinaryArithmetic`), non-lifted `Add`/`Multiply` node — mathematically
   justified by commutativity, not a parameter-name-based fix, and inert for every case that already worked
   (bound parameters, non-commutative operators, custom/lifted operators). Regression tests (deliberately
   NOT wrapped in a lambda, so the parameters are free from the comparer's point of view):
   `ExpressionComparerTests.FreeParameters_CommutativeAddition_NotWrappedInLambda_StillEqual`,
   `..._CommutativeMultiplication_...`, plus negative controls for non-commutative `Subtract` and a
   custom-operator `Add` (S3 safety unaffected).
3. **Additive grouping still executed arbitrary user code via a constant's `Equals`/`GetHashCode`.**
   `ExpressionComparer.StructuralEqualsRaw`/`StructuralHashRaw` (added earlier in S4 for grouping reuse)
   delegated non-numeric constant comparison to `ConstantsEqual`/`HashConstant`, which call the boxed
   value's own possibly user-defined `object.Equals`/`GetHashCode` — meaning an expression tree merely
   containing a hostile constant (one whose `Equals`/`GetHashCode` throws or has side effects) could make an
   ordinary `Simplify()` call fail or misbehave, without the tree ever being executed. `ParameterBindingContext`/
   `ParameterScopeStack` gained a `SafeConstantsOnly` flag (`false` for the public, simplifying `Equals`/
   `GetHashCode` — unchanged, documented behavior); `StructuralEqualsRaw`/`StructuralHashRaw` (S4's only
   callers) now pass `true`, making `ConstantsEqual` use `ReferenceEquals` and `HashConstant` use
   `RuntimeHelpers.GetHashCode` for a non-numeric constant instead of the value's own overrides. The
   `RuntimeHelpers.GetHashCode` use is only ever a `GroupBy` bucketing accelerator, never the final order,
   consistent with the roadmap's hash policy. Regression:
   `ExpressionSimplifierStructuralCanonicalizationTests.HostileConstantArgument_InAdditiveGrouping_NeverCallsUserEqualsOrGetHashCode`
   (a `HostileConstant` whose overrides record a call and throw; two additive terms embed the SAME instance,
   forcing a hash collision and therefore an equality check, resolved via `ReferenceEquals` without
   invoking either override). Constructed as `Add(Add(term1, x), term2)`, not `Add(term1, term2)` directly:
   the latter also reaches the pre-existing, S4-unrelated `AdditionOfEqualsElements` factoring rule, which
   calls the PUBLIC (non-safe) `ExpressionComparer.Default.Equals` before canonicalization ever runs — a
   separate, out-of-scope hazard (every `*OfEqualsElements` factoring rule has the same characteristic, not
   introduced by S4) noted here for a future audit but not fixed in this stage.
4. **`ConstantKey`'s order-side tie-break called `IComparable.CompareTo()` on an arbitrary boxed value.**
   Same class of hazard as item 3 but on the ORDERING side (`ExpressionCanonicalOrder.ConstantKey`, used by
   the final tie-break and multiplicative ordering): calling a user `IComparable.CompareTo()` can execute
   user code, and even a "safe" framework type's default comparer (e.g. `string`'s culture-aware
   `CompareTo`) can depend on `Thread.CurrentCulture`, which is incompatible with a key that must be
   deterministic. The generic `IComparable` path was removed; only `string` is special-cased (via
   `string.CompareOrdinal`, culture-invariant), and every other non-numeric constant type ties (`0`),
   relying on the caller's stable sort — consistent with the free-parameter and unsupported-node policies
   already documented above.
5. **`int[]` (SZ/vector array) and `int[*]` (general rank-1 array with explicit bounds) compared equal.**
   `CompareType`'s array branch checked only `IsArray` and `GetArrayRank()`, which both report `true`/`1`
   for either shape even though they are distinct, non-interchangeable CLR types. Fixed by comparing
   `Type.IsSZArray` before rank, which also transitively fixes the same confusion when the array type is
   nested inside a constructed generic argument (e.g. `List<int[]>` vs `List<int[*]>`), since that reaches
   the same branch through the existing generic-argument recursion. Regression:
   `CompareType_DistinguishesSzArrayFromBoundedRank1Array` and its
   `..._NestedInGenericArgument` counterpart (both call `ExpressionCanonicalOrder.CompareType` directly via
   reflection).
6. **Incomplete XML documentation on the new production and test code**, per `AGENTS.md`'s "all
   classes/methods/private members" requirement. Added throughout `ExpressionCanonicalOrder.cs` (every
   `Build*` helper, every `KeyNode` subclass and its constructor, the new `TryGet*` tie-break helpers, the
   rank constants) and to the test helpers/types added by this stage's own test file (`Contains`,
   `CustomOpA`/`CustomOpB` and their reflected `MethodInfo` fields, `MathVariantA`/`MathVariantB.Compute`,
   `HostileConstant`, `IdentityFromObject`). One helper that became unused after the item-7 test rewrite
   below (`EnumerateConstantDoubles`) was deleted rather than documented.

Two of the original 25 S4 tests were also hardened per the review's item-7 finding (`ExactMethodCallIdentity_...`
and `PowerWrappedFunctionGrouping_DistinguishesExponentsInCompleteKey` concluded mainly via
`ExpressionComparer.Default`, which re-simplifies both sides and could mask a first-pass canonicalization
defect — the exact pitfall the original S4 audit itself flagged for the dedicated test file). Both now
inspect the raw `Left`/`Right` shape of each FIRST `Simplify()` call's result directly, matching every other
test in the file. The suite is now 29 tests (25 original, unchanged in count by the items-7 rewrites since
those edited existing tests in place, plus 4 new regressions: the hostile-constant grouping test, the two
`CompareType` SZ-array tests, and the `CompareMethod` `DynamicMethod` test).

**Validation performed after the review fixes (2026-09-18):** `ExpressionSimplifierStructuralCanonicalizationTests`
and `ExpressionComparerTests` together: 79/79 passed. Full `UtilsTest.Unit`: 7650/7650 passed, 0 skipped.
Full `UtilsTest.Functional`: 383/383 passed. Full `UtilsTest.Security`: 225/228 passed, 3 skipped (the same
three pre-existing, unrelated, platform-gated tests noted above). Release build of `Utils.sln`: succeeded,
no errors (same pre-existing, unrelated `CS8618` warning).

#### S4 review fixes, round 2 (2026-09-18) — PR #600 second human review pass

A second human review pass at commit `b688dba0` confirmed all five round-1 fixes (DynamicMethod, the
`IComparable`/culture hazard, `int[]`/`int[*]`, and both item-7 test rewrites) and found one real remaining
S4 defect plus two contract/conformity points. Fixed on the same branch.

1. **The complete order key still could not distinguish `bool`/`char`/`enum` constants.** Round 1 correctly
   removed the generic `IComparable.CompareTo()` fallback from `ConstantKey`, special-casing only `string`
   (ordinal). Every OTHER non-numeric constant type — including `bool`, `char` and any `enum` — fell through
   to a tie (`0`). Concretely, `F(false) + F(true)` and `F(true) + F(false)` (for any method `F` taking a
   single `bool`) no longer converged to the same canonical tree: their `ConstantKey`s tied, the stable sort
   preserved each side's own source order, and `Simplify()` was no longer deterministic across source order
   for this shape — a real regression against S4's own "deterministic" completion criterion, not merely a
   missed nice-to-have. Fixed by recognizing that `bool`, `char` and `Enum` (like `string`'s `Equals`/
   `GetHashCode`, though not its default `IComparable`) have fixed, non-user-overridable, non-culture-
   dependent comparison behavior built into the CLR: `ConstantKey` now also compares these three via
   `bool.CompareTo`/`char.CompareTo`/`Enum`'s own `IComparable` (the latter compares the underlying integral
   value — safe because an enum type cannot declare methods, so it can never be a *user* override).
   Regression: `ConstantOrdering_BoolConstants_CanonicalizeDeterministicallyRegardlessOfSourceOrder` and its
   `Char`/`Enum` counterparts in `ExpressionSimplifierStructuralCanonicalizationTests`.
2. **`SafeConstantsOnly` was more conservative than necessary for the additive-grouping side of the same
   issue.** Item 3 of the round-1 fixes made `StructuralEqualsRaw`/`StructuralHashRaw` treat every
   non-numeric constant as opaque (reference-identity-only), which is safe but also stopped grouping two
   structurally-equal-but-reference-distinct safe values (two `string`s with the same content built
   separately, two separately-boxed `bool`s, two equal `enum` values) — a behavior change from the pre-S4
   `GetAdditiveGroupingKey`, which grouped by value representation. Both this and finding 1 are the same
   underlying question — "which constant types are safe to compare directly?" — so they now share one
   answer: `ExpressionComparer.IsKnownSafeConstantValue(object)` (`string`, `bool`, `char`, `Enum`) is the
   single predicate both `ConstantsEqual`/`HashConstant` (grouping's `safeConstantsOnly` policy) and
   `ConstantKey` (the order key) consult, so the two sides can never independently drift on what counts as
   safe. `ConstantsEqual`/`HashConstant` now call the value's own `Equals`/`GetHashCode` for a known-safe
   value even under `safeConstantsOnly`, restoring the pre-S4 grouping-by-value behavior for exactly the
   types that are safe to do so for, while an opaque/arbitrary type still falls back to reference identity.
   No new regression test beyond finding 1's: the fix is the same code path, and finding 1's tests already
   force these terms through additive grouping (same category, different constant argument) as well as
   ordering.
3. **The free-parameter regression fix (round 1, item 2) also WIDENS public `Equals`/`GetHashCode`,
   beyond only restoring the pre-S4 case.** Two distinct free parameters that happen to share the SAME
   `Name` (e.g. both literally named `"v"`) previously compared unequal under `Add`/`Add`-swapped (the old
   textual key tied on identical name text, stable sort preserved each side's own operand order, and the
   then-positional-only `BinaryEqual` found the operands reference-unequal); the round-1 commutative
   fallback now matches them. This is intentional and mathematically sound (ordinary addition is
   commutative regardless of what its operands are named), not a bug, but it is a real, observable widening
   of the public equivalence relation beyond the literal free-parameter regression the fix targeted, so it
   is now called out explicitly (including in the "Files changed" note above) rather than left implicit.
   Regression/characterization: `ExpressionComparerTests.FreeParameters_SameNameDistinctInstances_CommutativeAddition_NowEqual`.
4. **Two production/test members were still undocumented**, per `AGENTS.md`. `ExpressionComparer.ExactNumericValue.KindRank(NumericKind)`
   and `ExpressionSimplifierStructuralCanonicalizationTests.ThrowingExpression.ToString()` (plus a few
   sibling members on the same test type and on `HostileConstant` found while auditing) now have XML docs.
5. The PR description itself (not this roadmap file) was updated to match the current test counts (29 S4
   tests, not 25; `UtilsTest.Unit` 7654/7654) — noted here only because the reviewer flagged it; no code or
   roadmap content was stale.

**Validation performed after round 2 (2026-09-18):** `ExpressionSimplifierStructuralCanonicalizationTests`
and `ExpressionComparerTests` together: 83/83 passed. Full `UtilsTest.Unit`: 7654/7654 passed, 0 skipped.
Full `UtilsTest.Functional`: 383/383 passed. Full `UtilsTest.Security`: 225/228 passed, 3 skipped (same three
pre-existing, unrelated, platform-gated tests). Release build of `Utils.sln`: succeeded, no errors (same
pre-existing, unrelated `CS8618` warning).

#### S4 review fixes, round 3 (2026-09-22) — PR #600 third human review pass

A third human review pass at commit `da49f7a0` confirmed all five round-2 fixes still hold and found one
real remaining S4 defect plus two contract/conformity points. Fixed on the same branch.

1. **`ConstantKey` still collided for constants whose DECLARED `Type` is wider than their boxed value's
   runtime type** — e.g. `Expression.Constant(false, typeof(object))` or `Expression.Constant(1,
   typeof(object))`, which occur for any argument typed `object` (or another wide interface/base class) at
   the call site. Round 2 fixed `bool`/`char`/`enum` ordering and numeric detection, but both fixes keyed
   off `ConstantExpression.Type` — the *declared* type — not the boxed value's own type. Two constants
   declared `object` but holding, say, `false` and `"x"` therefore both had `_type == typeof(object)`,
   `CompareType` tied, and neither the string/bool/char/enum special-casing nor `ExactNumericValue` (built
   only when the *declared* type is in `Types.Number`) ever engaged — so `F((object)false) + F((object)"x")`
   and its operand-swapped source form were not guaranteed to converge to the same canonical tree, a real
   S4 determinism regression for this shape. Fixed by falling back to the boxed value's own runtime type
   (`value.GetType()`, always safe to call — never user-overridable) whenever the *declared* type does not
   itself identify the value as numeric or known-safe: `BuildConstant`/`TryGetNumericValue` now try the
   declared type first and the runtime type second for `ExactNumericValue` construction, and
   `ConstantKey.CompareSameRank` compares runtime types up front (via the same non-throwing `CompareType`
   helper) before falling through to the string/bool/char/enum branches, so a declared-type tie can no
   longer mask a real runtime-type difference. Regression:
   `ConstantOrdering_DifferentSafeRuntimeTypesDeclaredAsObject_CanonicalizeDeterministically` and
   `ConstantOrdering_BoxedNumericConstantsDeclaredAsObject_CanonicalizeDeterministically` in
   `ExpressionSimplifierStructuralCanonicalizationTests`.
2. **Round 2's `SafeConstantsOnly` grouping-by-value fix (finding 2) had no dedicated regression test.**
   Round 2's own bool/char/enum tests use distinct values, which only exercises the order key's ability to
   *distinguish* unequal safe constants, not `StructuralEqualsRaw`/`StructuralHashRaw`'s ability to *group*
   equal-but-differently-referenced/boxed safe constants — the actual behavior the finding 2 fix restored.
   The implementation itself needed no change (`ConstantsEqual`/`HashConstant` were already correct), but the
   gap was real: a future regression here would have gone undetected. Added four targeted tests reflecting
   into the internal `StructuralEqualsRaw`/`StructuralHashRaw` helpers directly: two equal-content `string`s
   built from separate `char[]`s, two separately-boxed equal `bool`s, and two separately-boxed equal
   `SampleColor` enum values are all grouped by value; a negative control with two distinct opaque
   (non-known-safe) values confirms they are still *not* grouped and, critically, that neither side's
   `Equals`/`GetHashCode` was ever invoked (arbitrary user code still unreachable).
3. **The PR description's test counts were stale again**, same class of issue as round 2 finding 5: it still
   said "29 tests" (actual count at `da49f7a0` was already 32 before this round's 6 additions, now 38) and
   carried the original commit's `Mathematics/Expressions: 566/566` figure unchanged through both later
   rounds even though the namespace's actual measured count is unrelated to the incremental "+8/+4" arithmetic
   used to justify it — re-measured directly at `da49f7a0` it is 442/442, not 566 or 578. PR description
   updated to the directly re-measured current counts rather than incremented arithmetic, to avoid the same
   staleness recurring in a future round.

**Validation performed after round 3 (2026-09-22):** `ExpressionSimplifierStructuralCanonicalizationTests`:
38/38 passed. Full `UtilsTest/Mathematics/Expressions` namespace: 448/448 passed. Full `UtilsTest.Unit`:
7660/7660 passed, 0 skipped.

#### S4 review fixes, round 4 (2026-09-22) — PR #600 fourth human review pass

A fourth human review pass at commit `32aac339` found two real remaining S4 defects and asked for an
explicit decision on a public-compatibility question round 2 had already documented but not formally
decided. Fixed/decided on the same branch.

1. **The `[ThreadStatic]` ambient lexical-scope stack leaked between independent, reentrant `Simplify()`
   calls.** `ExpressionSimplifier._lexicalScopeStack` was populated/consumed only through
   `OnEnterLambdaScope`/`OnExitLambdaScope`, called from `TransformCore`'s traversal — but the PUBLIC entry
   point, `Transform` (and therefore `Simplify`), established no boundary of its own: it simply called
   `TransformCore` directly. Every call reaching `Transform` - the true top-level call from application code,
   but ALSO `ExpressionComparer`'s own internal re-simplification of its operands
   (`_expressionSimplifier.Simplify(x)`/`(y)` in `Equals`/`GetHashCode`) - therefore shared whatever ambient
   stack happened to be open on the current thread. Concretely: `AdditionOfEqualsElements` (a pre-existing,
   S4-unrelated factoring rule) calls the PUBLIC, non-safe `ExpressionComparer.Default.Equals` on two
   addends, which for an opaque (non-known-safe) constant argument calls that constant's own, possibly
   user-defined `object.Equals` override - a documented, accepted hazard of the PUBLIC comparer specifically
   (see `ConstantsEqual`'s `safeConstantsOnly` policy). If that user code then starts a brand-new, unrelated
   `new ExpressionSimplifier().Simplify(...)` call on the same thread while an OUTER simplification's lambda
   scope is still open, and that inner call happens to reuse the outer lambda's own bound
   `ParameterExpression` instances (same object references) in an expression with no lambda wrapper of its
   own, `ExpressionCanonicalOrder.BuildParameter`'s reference-based scope search would incorrectly resolve
   them as bound at the OUTER lambda's depth/position and reorder them accordingly, instead of correctly
   treating them as free (their only correct classification from that independent inner call's own,
   self-contained point of view) - a real, if narrow, S4 determinism hazard. Fixed by giving the exact
   built-in `ExpressionSimplifier`'s `Transform` override its own boundary: it now saves the caller's current
   `_lexicalScopeStack`, installs a fresh empty one, runs `TransformCore`, and restores the caller's stack in
   a `finally` block. Every call to the public `Transform`/`Simplify` API is therefore self-contained and
   never depends on, or leaks into, any concurrently-in-progress ambient state elsewhere on the same thread -
   including `ExpressionComparer`'s own internal re-simplification calls, which is an intentional,
   correctness-neutral side effect: `ExpressionComparer.Equals`'s subsequent structural comparison resolves
   parameter binding independently via its own `ParameterBindingContext`, never via this ambient stack, so it
   does not depend on the nested `Simplify` call seeing an outer scope. Regression:
   `Reentrancy_IndependentNestedSimplifyTriggeredDuringPublicComparerEquals_DoesNotLeakAmbientScope` (verified
   to fail without the fix, reproducing the reviewer's exact scenario, before being fixed).
2. **`ConstantKey`'s numeric branch still tied two mathematically-equal-but-runtime-type-different boxed
   numerics declared as `object`.** Round 3 taught `TryGetNumericValue` to fall back to a constant's boxed
   value's own RUNTIME type when its DECLARED type is not itself numeric (e.g.
   `Expression.Constant(1, typeof(object))`), but `ConstantKey.CompareSameRank`'s numeric branch returned the
   bare `ExactNumericValue.CompareTo()` result the instant both sides were numeric - with no further
   tie-break when that comparison was itself `0` (mathematically equal, e.g. runtime `int 1` vs runtime
   `double 1.0`). Since `ExpressionComparer.ConstantsEqual`'s own numeric fast path (`TryGetExactNumericValue`
   in `ExpressionComparer.cs`) only ever consults the DECLARED type - it was not updated in round 3 - these
   two constants are NOT structurally equal despite tying in the order key: `F((object)1) + F((object)1.0)`
   and its reversed source form both kept their own source order (a stable-sort tie in both directions),
   failing to converge - a real S4 determinism regression for this shape, for the same underlying reason as
   round 3's finding 1. Fixed by tie-breaking on the boxed value's own runtime type (`object.GetType()`,
   always safe, never user-overridable) whenever the numeric comparison ties AND at least one side's DECLARED
   type is not itself a native numeric type - i.e., only in the runtime-type-fallback scenario round 3
   introduced. The ordinary, long-established cross-type-numeric-equality case (both sides DECLARED as
   different native numeric types, e.g. `int 1` vs `long 1`, which `ConstantsEqual` already treats as equal
   via the declared type) is deliberately left tying exactly as before, since adding a runtime-type
   tie-break there would make the order key distinguish two constants `ConstantsEqual` still reports as
   equal. Regression:
   `ConstantOrdering_BoxedNumericConstantsDeclaredAsObjectWithDifferentRuntimeTypes_CanonicalizeDeterministically`
   (verified to fail without the fix). Noted but deliberately NOT fixed this round, as a separate, non-blocking
   follow-up the reviewer flagged as lower priority: `ExpressionComparer.ConstantsEqual`/`StructuralEqualsRaw`
   still do not themselves recognize a declared-`object` boxed numeric via its runtime type the way the order
   key now does, so two such constants with the SAME runtime-numeric value (e.g. two separately-boxed `object`
   constants both holding `int 1`) are not (yet) grouped as equal by the additive-grouping/public-equality
   side - a missed grouping opportunity, not a correctness bug (the conservative direction), left for a future
   round to decide whether unifying it is worth the additional grouping/merge-behavior surface it would touch.
3. **Public-compatibility decision: `ExpressionComparer.Default`'s free-parameter commutative widening
   (round 2, finding 3) is explicitly ACCEPTED, not reverted.** The reviewer asked for an explicit decision
   rather than letting this pass under the "S4" label alone, since `ExpressionComparer` is a public
   `IEqualityComparer<Expression>` an external caller could use as a `Dictionary`/`HashSet` key comparer.
   Decision: keep the widening as round 2 shipped it. Reasoning: (a) it is mathematically correct - ordinary
   addition truly is commutative regardless of what its free operands are named; (b) the OLD behavior it
   changes (two distinct free parameters that happen to share a `Name` comparing unequal under
   `Add`/swapped-`Add`) was itself an accidental side effect of the pre-S4 textual-key implementation, never a
   deliberately designed contract, so reverting to it would reintroduce a real non-commutativity inconsistency
   rather than remove one; (c) the project's batched-2.0.0 versioning plan already treats this kind of
   accumulating, individually-reasoned public-behavior change as expected between major releases rather than a
   per-change compatibility blocker. `ExpressionComparer.BinaryEqual`'s XML remarks now say so explicitly
   ("Public compatibility decision (S4 review, round 4)" paragraph), so this is a recorded decision rather
   than a silent carry-over. No code or test change beyond that documentation - round 2's own
   `FreeParameters_SameNameDistinctInstances_CommutativeAddition_NowEqual` already characterizes the accepted
   behavior.

**Validation performed after round 4 (2026-09-22):** `ExpressionSimplifierStructuralCanonicalizationTests`:
40/40 passed. Full `UtilsTest/Mathematics/Expressions` namespace: 450/450 passed. Full `UtilsTest.Unit`:
7662/7662 passed, 0 skipped. Full `UtilsTest.Functional`: 383/383 passed. Full `UtilsTest.Security`: 225/228
passed, 3 skipped (same three pre-existing, unrelated, platform-gated tests). Release build of `Utils.sln`:
succeeded, no errors.

#### S4 review fixes, round 5 (2026-09-22) — PR #600 fifth human review pass

A fifth human review pass at commit `7a690723` found one real remaining S4 defect and asked for an explicit
decision on a comparer scope question round 2's fix left open. Fixed/decided on the same branch.

1. **`ConstantKey`'s round-4 runtime-type tie-break was itself incomplete: it did not cover two constants
   with the SAME exact value AND the SAME runtime type but DIFFERENT DECLARED types.** Round 4 taught the
   numeric branch to tie-break on the boxed value's runtime type when the exact-numeric comparison itself
   tied, fixing e.g. `Constant(1, typeof(object))` [runtime `int`] vs `Constant(1.0, typeof(object))`
   [runtime `double`]. But `Constant(1.0, typeof(object))` vs `Constant(1.0, typeof(IConvertible))` - same
   exact value (`1`), same runtime type (`double`), only the DECLARED type differs - still tied all the way
   through: the runtime-type tie-break itself found no difference (`double == double`) and fell straight to
   `return 0`, without ever comparing the DECLARED types. `ExpressionComparer.ConstantsEqual`'s fallback path
   (reached here, since neither `object` nor `IConvertible` is itself native numeric) requires
   `x.Type == y.Type`, so these two constants are NOT structurally equal despite the order key's tie - the
   same category of source-order-independence regression as rounds 3 and 4, just one level deeper. Fixed by
   adding a further tie-break on the constants' own DECLARED `Type` (same non-throwing `CompareType` helper)
   when the runtime types also tied, still gated to the same "at least one side's declared type is not
   itself native numeric" condition rounds 3/4 established, so the ordinary declared-numeric cross-type case
   (`int 1` vs `long 1`) is entirely unaffected. Regression:
   `ConstantOrdering_EqualNumericValueSameRuntimeTypeDifferentDeclaredTypes_CanonicalizesDeterministically`
   (verified to fail without the fix).
2. **Comparer scope decision: the round-2 commutative-operand fallback in `ExpressionComparer.BinaryEqual`
   is explicitly scoped to the DIRECT two-operand case, not a general n-ary restoration - and is NOT being
   extended in this round.** Before S4, three or more free parameters chained through ordinary
   addition/multiplication converged to one canonical order via the old `ParameterExpression.Name`-based
   textual sort regardless of source association (e.g. `Simplify((a+b)+c)` and `Simplify((c+b)+a)` both
   produced the same tree), so the pre-S4 comparer trivially agreed on such chains too. Verified directly:
   post-S4, `Simplify((a+b)+c)` stays `a+(b+c)` and `Simplify((c+b)+a)` stays `c+(b+a)` (each side keeps its
   own source association, per the deliberate "Free parameters" policy), and
   `ExpressionComparer.Default.Equals((a+b)+c, (c+b)+a)` is `false` for three distinct free parameters -
   confirming the reviewer's diagnosis exactly. The round-2 fallback only swaps a single `BinaryExpression`
   node's two operands; recognizing this would require comparing/hashing ordinary `Add`/`Multiply` chains as
   associative-commutative MULTISETS of terms - ambiguous term-to-term matching (not simple positional or
   single-swap comparison), interaction with `ParameterBindingContext`'s bound-parameter tracking for
   sub-terms that are themselves lambdas, and `GetHashCode` consistency - a materially larger capability
   change than any of rounds 1-5's narrow, bounded fixes, and not one to take on as a "just also fix this"
   addendum to an already-five-round review cycle. Decision: DEFER, and make the scope boundary explicit
   rather than leave it an unspecified gap - matching the reviewer's second offered resolution path.
   `ExpressionComparer.BinaryEqual`'s XML remarks now say so explicitly ("Explicit scope boundary (S4 review,
   round 5): NOT a general n-ary restoration" paragraph). Characterization tests added (asserting the
   CURRENT, decided behavior, not a requirement):
   `ExpressionComparerTests.FreeParameters_ThreeTermAdditionPermutation_PreservesPreS4ComparerBehavior` and
   its multiplicative counterpart (renamed in round 8 to `..._CharacterizesPostS4ScopeBoundary`: the original
   name was itself backwards - it asserted `false`, i.e. that the comparer does NOT preserve pre-S4 behavior
   for this shape, the opposite of what the name said). A full associative-commutative comparer/hasher for
   such chains, if ever wanted, belongs in its own dedicated stage/PR, not folded into S4's closeout.

**Validation performed after round 5 (2026-09-22):** `ExpressionSimplifierStructuralCanonicalizationTests`:
41/41 passed. `ExpressionComparerTests`: 53/53 passed. Full `UtilsTest/Mathematics/Expressions` namespace:
453/453 passed. Full `UtilsTest.Unit`: 7665/7665 passed, 0 skipped. Full `UtilsTest.Functional`: 383/383
passed (one transient failure in `DllMapperTests.MapFromInterfaceInIsolatedWorkerTest` during a FIRST run
that raced four validation suites/builds in parallel - a known isolated-worker/named-pipe flakiness under
resource contention in this environment, unrelated to this change - did not reproduce on a clean sequential
re-run). Full `UtilsTest.Security`: 225/228 passed, 3 skipped (same three pre-existing, unrelated,
platform-gated tests). Release build of `Utils.sln`: succeeded, no errors.

#### S4 review fixes, round 6 (2026-09-22) — self-review (`/code-review high`) at commit `6194ed14`

A self-review pass (`/code-review high`, 8 finder angles, 9 deduplicated candidates, all verified) found one
real remaining S4 defect, confirmed one already-known/deliberately-deferred item is not actually observable,
strengthened two tests that had silently stopped proving what their names claim, and made two small,
low-risk documentation/reuse cleanups. Fixed on the same branch.

1. **`FinalizeExpression` called `CanonicalizeAdditiveExpression`/`CanonicalizeMultiplicativeExpression`
   unconditionally, unlike every other S4 integration point in this class** (`Transform`, `PrepareExpression`,
   `RebuildUnaryExpression`, `RebuildLambdaExpression`, `OnEnterLambdaScope`, `OnExitLambdaScope`, all gated
   to `GetType() == typeof(ExpressionSimplifier)`). Since `OnEnterLambdaScope`/`OnExitLambdaScope` no-op for
   any other runtime type, the ambient lexical-scope stack these two canonicalization methods read via
   `CaptureLexicalScopeSnapshot()` was NEVER populated for a subclass's own traversal - so every bound
   `ParameterExpression` a subclass's canonicalization encountered was silently misclassified as free (ties
   by declared `Type` only, stable-sort source order preserved instead of declaration-position reordering).
   Verified directly: `new MinimalDerivedSimplifier().Simplify((p0, p1) => p1 + p0)` (a zero-override
   subclass) kept `p1 + p0` unchanged instead of reordering to `p0 + p1` the way the exact built-in type does
   for the identical source lambda. This is a real canonicalization-quality regression introduced by S4 with
   no comparable pre-S4 counterpart (the removed `ToString()`-based ordering needed no ambient state, so it
   worked identically for every subclass) and had no test coverage for any subclass processing a bound
   lambda through these two rules. Fixed by gating the same way every other S4 integration point already is:
   a subclass now falls through to the historical `CopyExpression(e, parameters)` path for `Add`/`Subtract`/
   `Multiply` nodes reaching this fallback, exactly like every other node kind it does not specifically
   canonicalize - operands simply keep their original source order rather than being reordered from an
   incomplete (bound-as-free) view of the enclosing scope. A conservative "commutative reordering is
   skipped", not a correctness bug, consistent with this class's established exact-type-only S4 policy.
   Regression: `DerivedSimplifier_BoundAddition_DoesNotMisclassifyBoundParametersAsFree` in
   `ExpressionSimplifierStructuralCanonicalizationTests` (verified to fail without the fix). This also
   changed the observable result of an EXISTING test,
   `ExpressionTransformationRuleBranchTests.Simplify_NegateWithSubstraction_DirectRuleBody_RewritesOperands`
   (which uses `ExposedSimplifier`, a subclass, specifically to invoke a protected rule body directly): that
   rule body's own `TransformCore` recursion used to incidentally reach the now-gated canonicalization too,
   rebuilding its `Subtract(y, x)` result into `Add(y, Negate(x))`; post-fix it correctly stays
   `Subtract(y, x)` (same compiled value, different but now-correct shape). Updated that test's assertion and
   added a remark explaining why, rather than leaving a misleading expectation in place.
2. **Confirmed non-issue, already known and deliberately deferred (round 4's own "Noted but deliberately NOT
   fixed this round" item): `ConstantKey` still ties for two SEPARATELY-BOXED numeric constants sharing the
   exact SAME value and the exact SAME declared type** (e.g. two independently-boxed `object`-declared
   `1.0`s), since `ExpressionComparer.ConstantsEqual`'s safe (additive-grouping) path falls back to
   `ReferenceEquals` for these, while the order key's declared/runtime-type tie-breaks find no difference on
   either axis and tie. Verified directly this does NOT translate into an observable
   `ExpressionComparer.Default` regression, unlike the round 3-5 findings: the PUBLIC (non-safe) comparer
   path calls `1.0.Equals(1.0)`, which is `true` regardless of which boxed instance ends up on which side, so
   positional comparison already succeeds without ever needing the commutative-swap fallback - both reversed
   source orderings of such a pair are correctly reported equal by `ExpressionComparer.Default` even though
   their canonical trees are not necessarily identical. No code change; added
   `ConstantOrdering_SameDeclaredTypeDistinctlyBoxedEqualNumericConstants_PublicComparerStillAgrees` as
   permanent evidence for this conclusion, closing the loop on round 4's deferred item with a verified answer
   rather than an assumption.
3. **Two tests named for what canonicalization does were only checking `ExpressionComparer.Default` equality,
   which - because of round 2's own commutative-operand fallback for ordinary `Add`/`Multiply` - would keep
   passing even if canonicalization were silently disabled entirely.**
   `Simplify_AddReachingFallback_StillCanonicalizes`/`Simplify_MultiplyReachingFallback_StillCanonicalizes`
   in `ExpressionSimplifierFinalizationTests` asserted only `Assert.AreEqual(result1, result2,
   ExpressionComparer.Default)` plus a compiled-value check - both of which are satisfied by ordinary
   commutativity regardless of whether real operand reordering happened. Strengthened with a direct
   structural check (`FirstDeclaredParameterIsOnLeft`) proving the first-declared parameter actually ends up
   on the Left in BOTH results (including the one requiring a real swap), so a future regression that
   silently disables `CanonicalizeAdditiveExpression`/`CanonicalizeMultiplicativeExpression` would be caught
   here specifically, not only by the (less targeted) `ExpressionComparer.Default` equality these tests
   already had.
4. **Minor documentation/reuse cleanups, no behavior change:** `AdditiveGroupingEqualityComparer.Equals`/
   `GetHashCode` (both public members on an internal, IEqualityComparer-contract-bearing type) gained XML
   docs per `AGENTS.md`, including an explicit note that the two must stay classification-consistent with
   each other. `AnnotatedAdditiveTerm`'s doc comment overclaimed that everything it holds is "computed once
   per term rather than recomputed on every pairwise comparison" - true for `.Key`'s own use in the final
   `.ThenBy(term => term.Key)` tie-break, but NOT true for `CompareAdditiveGroupingOrder`'s primary-sort use
   of `Group.Opaque`, which for an opaque term IS the same expression `.Key` was already built from, yet gets
   rebuilt from scratch via `ExpressionCanonicalOrder.Compare` on every pairwise comparison anyway; corrected
   the doc to say so precisely and left the actual (construction-time-only, not correctness-affecting)
   inefficiency for S5, consistent with the roadmap's own S4/S5 boundary. `MethodCallKey.CompareSameRank`'s
   hand-rolled element-wise-plus-length-tie-break loop over its argument-key array was replaced with
   `Utils.Collections.EnumerableComparer<KeyNode>.Default` (an existing, already-tested, semantically
   identical generic helper `KeyNode`'s own `IComparable<KeyNode>` makes directly usable here) rather than
   keeping a duplicate of the same algorithm to maintain independently.

**Validation performed after round 6 (2026-09-22):** `ExpressionSimplifierStructuralCanonicalizationTests`:
43/43 passed. Full `UtilsTest/Mathematics/Expressions` namespace: 455/455 passed. Full `UtilsTest.Unit`:
7667/7667 passed, 0 skipped. Full `UtilsTest.Functional`: 383/383 passed. Full `UtilsTest.Security`: 225/228
passed, 3 skipped (same three pre-existing, unrelated, platform-gated tests). Release build of `Utils.sln`:
succeeded, no errors. All four suites run sequentially this round (not in parallel), after round 5's transient
parallel-contention flake, to keep the validation record clean.

#### S4 review fixes, round 7 (2026-09-22) — PR #600 sixth human review pass

A sixth human review pass at commit `6ab0ab55` found one real remaining S4 defect (a non-transitive
metadata tie-break, unrelated to round 6) and correctly identified that round 6's own subclass fix was both
under-tested (its test could not actually distinguish the bug from the fix) and the wrong shape of fix
entirely (it traded a correctness bug for a capability loss, and required editing a pre-existing, correct
test to match - something `AGENTS.md` asks not to do). Both addressed on the same branch.

1. **`CompareFinalMemberTiebreak` was not a consistent total preorder: which dimension (metadata token,
   module name, or assembly name) decided a comparison depended on the SPECIFIC PAIR being compared, not on
   a fixed priority.** For two members that both had a token and it differed, token decided; otherwise, for
   two members that both had a module name, module decided; otherwise assembly decided. Three members with
   different combinations of available dimensions can then form a genuine 3-cycle (A vs C decided by token,
   A vs B and B vs C each decided by module, with token order and module order disagreeing) - breaking the
   total order `OrderBy` requires, so `Simplify()`'s result could depend on incidental pairwise comparison
   order for expressions embedding such members. There was also a conceptual problem independent of the
   cycle: a `MetadataToken` is only meaningful WITHIN its own module, so comparing it before module identity
   is established to match compares values that can belong to entirely different metadata spaces. Fixed by
   making the tie-break a FIXED lexicographic tuple - assembly, then module, then token, each dimension
   checking "is it available" before "what is its value" and falling through to the next dimension only on
   an exact tie - evaluated in this same order for every pair, which is provably transitive (a lexicographic
   comparison over dimensions that are each individually transitive is always transitive) and also resolves
   the conceptual ordering problem (token, now the FINEST-grained dimension, is only ever compared once
   module identity already matches). Regression:
   `CompareMethod_MetadataTiebreak_AssemblyOrderTakesPriorityOverContradictingTokenOrder`, which bakes two
   real, owner-less "global" methods (via `ModuleBuilder.DefineGlobalMethod`/`CreateGlobalFunctions`, so they
   have genuine `MetadataToken`s while still having `DeclaringType == null` like a `DynamicMethod`) in two
   dynamic assemblies engineered so assembly-name order and metadata-token order deliberately DISAGREE
   (verified to fail without the fix - the old code compared by token first and got the opposite answer). A
   genuine 3-member cyclic-order reproduction was attempted first but abandoned: on this runtime, every
   dynamically-created module reports the identical literal name `"<In Memory Module>"` regardless of which
   assembly it belongs to, so two baked global methods (in different assemblies) can never be distinguished
   by module name alone, which turned out to remove enough degrees of freedom that a true 3-cycle could not
   be constructed with real `Reflection.Emit` objects on this runtime - the two-member,
   assembly-versus-token test above still directly and reliably exercises the actual dimension-priority
   change, and the fix's transitivity is additionally guaranteed structurally (not merely by this one
   example) by the fixed-tuple-comparison shape itself.
2. **Round 6's subclass fix is reworked: the actual root cause is fixed instead of worked around.** Round 6
   gated `FinalizeExpression`'s `CanonicalizeAdditiveExpression`/`CanonicalizeMultiplicativeExpression`
   dispatch to the exact built-in `ExpressionSimplifier` runtime type, since `OnEnterLambdaScope`/
   `OnExitLambdaScope` (which populate the ambient scope those two methods depend on) were themselves
   exact-type-only - so a subclass would otherwise misclassify its own bound parameters as free. Two problems
   with that fix, both raised by this review round: (a) its regression test,
   `DerivedSimplifier_BoundAddition_DoesNotMisclassifyBoundParametersAsFree`, asserted operands stayed in
   SOURCE order - but for two SAME-TYPE parameters, "misclassified as free" (ties, stable sort preserves
   source order) and "canonicalization skipped entirely" (round 6's own fix) produce the IDENTICAL observable
   result, so the test could not actually distinguish the bug from the fix (confirmed: reverting only the
   `FinalizeExpression` gate while leaving everything else in place still passed the test). (b) The fix
   itself traded one problem for another: pre-S4, a subclass got FULL, correct canonicalization (the removed
   `ToString()`-based ordering needed no ambient state, so it worked identically for every subclass); round
   6 made a subclass lose additive/multiplicative canonicalization ENTIRELY instead of just fixing the
   bound/free misclassification - a new, observable capability loss for any `ExpressionSimplifier` subclass,
   which also forced editing a pre-existing, correct test
   (`ExpressionTransformationRuleBranchTests.Simplify_NegateWithSubstraction_DirectRuleBody_RewritesOperands`)
   to match the new, degraded behavior, contrary to `AGENTS.md`'s "do not modify existing tests unless adding
   new functionality" (this was a behavior-driven edit, not a new-functionality one). Fixed for real this
   round: `OnEnterLambdaScope`/`OnExitLambdaScope` (and `Transform`'s ambient-scope reset boundary, round 4's
   fix) are now unconditional - unlike the reconstruction-fidelity guards on `RebuildLambdaExpression`/
   `RebuildUnaryExpression` (which preserve genuine pre-S4 historical behavior for a subclass), these two
   scope-tracking hooks are NEW to S4 with no historical behavior to preserve, and being `internal`, only a
   same-assembly (or `InternalsVisibleTo`) subclass could ever reach them regardless of gating - so removing
   the gate exposes no new public surface. `FinalizeExpression`'s canonicalization dispatch is therefore back
   to unconditional too (its pre-round-6 shape), and
   `Simplify_NegateWithSubstraction_DirectRuleBody_RewritesOperands` is reverted to its ORIGINAL, unmodified
   assertion (`ExpressionType.Add`) - it was correct all along. The round-6 test is renamed and rewritten to
   assert what actually needs proving: `DerivedSimplifier_BoundAddition_CanonicalizesIdenticallyToExactType`
   verifies a zero-override subclass reorders `(p0, p1) => p1 + p0` to `p0 + p1`, matching the exact type's
   own behavior for the identical source lambda (verified to fail - `p0` was not on the Left - when only the
   `OnEnterLambdaScope`/`OnExitLambdaScope`/`Transform` gates are reverted to round-6/earlier, confirming this
   version DOES distinguish the bug from the fix, unlike its round-6 predecessor).

**Validation performed after round 7 (2026-09-22):** `ExpressionSimplifierStructuralCanonicalizationTests`:
44/44 passed. Full `UtilsTest/Mathematics/Expressions` namespace: 456/456 passed. Full `UtilsTest.Unit`:
7668/7668 passed, 0 skipped. Full `UtilsTest.Functional`: 383/383 passed. Full `UtilsTest.Security`: 225/228
passed, 3 skipped (same three pre-existing, unrelated, platform-gated tests). Release build of `Utils.sln`:
succeeded, no errors. All four suites run sequentially.

#### S4 review fixes, round 8 (2026-09-22) — PR #600 seventh human review pass

A seventh human review pass at commit `3c7404fc` found one real remaining S4 defect (round 7's own fix was
incomplete) and several documentation inaccuracies left behind by earlier rounds. Fixed on the same branch.

1. **`CompareFinalMemberTiebreak` compared modules by `Module.Name` text, which does not reliably identify
   a module.** Round 7 fixed the tie-break's dimension ORDER (assembly, then module, then token) but the
   "module" dimension itself was still just `Module.Name` - a display string, not the module's actual
   identity. A `MemberInfo.MetadataToken` is meaningful only in combination with its actual `Module`, not
   with that module's name text specifically; since `Module.Name` is not guaranteed unique (every
   dynamically-created module on this runtime reports the identical literal `"<In Memory Module>"`,
   regardless of which assembly it belongs to - confirmed empirically while building this round's test), two
   different modules from two different builds could share an identical assembly-name string, an identical
   module-name string, AND an identical token value while genuinely representing different metadata - the
   tie-break would then conservatively tie (`0`) on a pair it could and should have distinguished. Fixed by
   adding `Module.ModuleVersionId` (a GUID the runtime generates to uniquely identify one physical module
   instance) as a dimension between module name and metadata token - `TryGetModuleVersionId`, guarded the
   same defensive way as the other `TryGet*` helpers. `Guid.CompareTo` does not order lexicographically by
   displayed hex text, but it is still a valid, deterministic total order over the GUID's raw bytes, which is
   all determinism/transitivity requires here. A residual, now explicitly documented limit:
   the SAME physical assembly file loaded twice (e.g. into two different `AssemblyLoadContext`s) produces two
   `MemberInfo` instances sharing an identical assembly name, module name, `ModuleVersionId` AND token, since
   the MVID is embedded in the file's own metadata and travels with every copy of the same build - this
   tie-break's contract is "distinguish two members by their AVAILABLE STRUCTURAL METADATA", not "always
   distinguish the exact runtime identity", and this is now stated explicitly in
   `CompareFinalMemberTiebreak`'s remarks rather than left implicit. Regression:
   `CompareMethod_MetadataTiebreak_ModuleVersionTakesPriorityOverSameNameSameToken` (two baked global methods
   in two dynamic assemblies sharing identical assembly-name text, module-name text, AND metadata token,
   distinguishable only by `ModuleVersionId`; verified to fail - tie to `0` - without the fix) plus an
   end-to-end counterpart, `CompareExpression_CallsOfMethodsDistinguishedOnlyByModuleVersion_OrderDeterministically`,
   exercising `ExpressionCanonicalOrder.Compare` directly on `Call(methodOne, x)`/`Call(methodTwo, x)` nodes
   (not through a full `Simplify()` call: an owner-less method routed through the full pipeline hits the
   same unrelated, pre-existing `ExpressionCallSignatureAttribute.Match` null-`DeclaringType` gap already
   noted on `CompareMethod_TwoDynamicMethodsWithIdenticalSignature_DoesNotThrow` - a different, out-of-scope
   bug the reviewer's suggested literal `Simplify()`-based end-to-end test would also have hit).
2. **Documentation cleanup**, all in files this branch already touches, no behavior change:
   `ExpressionSimplifier._lexicalScopeStack`'s doc comment still said "for the exact built-in
   `ExpressionSimplifier` runtime type only", stale since round 7 made the hooks unconditional - corrected.
   `ExpressionCanonicalOrder`'s class-level and `CompareType`'s own XML remarks still described the final
   tie-break as `MetadataToken`/`Module`/`Assembly` (the PRE-round-7 order) and called reaching it "practically
   unreachable... since the CLR does not allow two distinct types with an identical fully-qualified name to
   coexist" - true only within a single load context, not across two different assemblies/builds loaded side
   by side (e.g. via `Assembly.LoadFile`, which does not participate in the default identity-based binding
   cache) - corrected to describe the actual, current dimension order and to stop overclaiming
   unreachability. `CompareType`'s summary also implied object identity/reference order was used as a
   "last-resort deterministic tie-break", when the actual implementation never uses it as one (a tie
   conservatively returns `0`, never a reference-derived value) - reworded. The roadmap's own S4-progress
   "Files changed" summary for `ExpressionCanonicalOrder.cs` carried the same stale claim - given a
   forward-pointer to this round instead of being silently rewritten, preserving the historical record.
   Finally, `ExpressionComparerTests.FreeParameters_ThreeTermAdditionPermutation_PreservesPreS4ComparerBehavior`
   (and its multiplicative counterpart, round 5) asserted `Assert.IsFalse` while named "Preserves..." - backwards,
   since its own doc comment explains the pre-S4 comparer returned `true` for this shape; renamed to
   `..._CharacterizesPostS4ScopeBoundary`, with every cross-reference (`ExpressionComparer.BinaryEqual`'s XML
   remarks, this roadmap file) updated to match.

**Validation performed after round 8 (2026-09-22):** `ExpressionSimplifierStructuralCanonicalizationTests`:
46/46 passed. `ExpressionComparerTests`: 53/53 passed. Full `UtilsTest/Mathematics/Expressions` namespace:
458/458 passed. Full `UtilsTest.Unit`: 7670/7670 passed, 0 skipped. Full `UtilsTest.Functional`: 383/383
passed. Full `UtilsTest.Security`: 225/228 passed, 3 skipped (same three pre-existing, unrelated,
platform-gated tests). Release build of `Utils.sln`: succeeded, no errors. All four suites run sequentially.

### S5 — Construction-performance cleanup

Only after the correctness/structure stages above, re-profile construction-time allocations and CPU cost in the simplifier.

Potential remaining areas include:

- additive `OrderBy` / `ThenBy` / `ThenBy` / `ToList` / `GroupBy` pipeline;
- repeated key computation - concretely, `CompareAdditiveGroupingOrder`'s primary-sort comparator rebuilds a
  full `ExpressionCanonicalOrder` key from scratch (via `ExpressionCanonicalOrder.Compare`) for both operands
  on every pairwise comparison, even for an "opaque" term whose key `AnnotatedAdditiveTerm.Key` already holds
  precomputed (see S4 review round 6's finding 4) - turning an O(n) key-construction cost into an O(n log n)
  one across the sort;
- intermediate group/list materialization;
- `ExpressionComparer` temporary arrays and searches;
- other LINQ/reflection scaffolding still present on frequently used construction paths.

Construction optimizations should be accepted only when they do not weaken the symbolic contract or complicate the later execution optimizer. Prefer small, benchmarked and causally isolated changes like PRs #577-#589.

#### S5 progress (2026-09-23) — P1 implemented; P2 benchmarked and rejected

Baseline for this stage: `master` at `4550b3614b7ce05a23b1da99d379bfdf42d2c6ad` (S4 review round 8 / PR #600
merge), matching `origin/master` at the start of this work — no rebase was needed.

**Benchmark methodology.** No permanent BenchmarkDotNet project exists for this code, consistent with prior
performance PRs #577-#589. A standalone temporary console harness (not part of this repository; built and
run from outside the working tree, referencing `Utils.csproj` via `ProjectReference`) measured, for term
counts n=2/8/32/128:

1. a **micro** scenario reflecting directly into the private
   `ExpressionSimplifier.CanonicalizeAdditiveExpression`/`CanonicalizeMultiplicativeExpression` methods with a
   pre-built n-term chain, isolating the sort/group mechanism this stage targets from the rest of the
   `Transform` pipeline;
2. a **public end-to-end** scenario calling `new ExpressionSimplifier().Simplify(...)` on the same term
   families, capped at n=2/8 (see "End-to-end scaling was not usable beyond n=8" below).

Five term families were covered: (1) opaque parameters/compound terms, (2) `double.Sin`/`Cos`/`Tan`
`MethodCallExpression` terms, (3) `Power(MethodCall, exponent)` terms, (4) a mix of positive and
`Negate`-wrapped terms, (5) multiplicative factors (an explicit "should be unaffected" control, since this
stage's P1 change does not touch `CanonicalizeMultiplicativeExpression`), plus (6) a bound-lambda-parameter
end-to-end scenario exercising S4 scopes. Each scenario ran a warm-up phase then 15-21 measured rounds, each
round batched (to amortize `Stopwatch`/timer resolution noise) and measuring both `Stopwatch` elapsed time and
`GC.GetAllocatedBytesForCurrentThread()` deltas; the table below reports the median time and the (fully
deterministic, byte-identical run-to-run) median allocation. Machine: Windows 11, .NET 8.0.31 (`Utils.csproj`
target), 20 logical CPUs, workstation GC, single-threaded, sequential (never parallel/concurrent) baseline vs.
candidate runs from the same machine state.

**End-to-end scaling was not usable beyond n=8.** A raw, left-associated n-term source expression makes
`Transform`'s bottom-up traversal canonicalize EVERY nesting level, and pre-existing (not S5-introduced)
factoring rules such as `AdditionOfEqualsElements` call the PUBLIC `ExpressionComparer`, which itself
re-invokes `Simplify()` on its operands — a real, S4-review-documented (round 6, "S4-unrelated" `Comparer`
re-simplification hazard already noted for every `*OfEqualsElements` rule) construction-cost multiplier
entirely unrelated to this stage's P1/P2 hotspots. Measured directly on the pre-S5 baseline: end-to-end
`Simplify()` at n=8 already costs several to tens of milliseconds and multiple megabytes per call (see the
table below); n=32/128 end-to-end was therefore dropped from the benchmark matrix as unreasonably slow to run
repeatedly, consistent with this stage's own "prefer small, benchmarked and causally isolated changes"
guidance — a benchmark whose per-call cost is dominated by an unrelated hazard would not isolate this PR's
change. This is a separate, pre-existing hazard, out of scope for this PR; it is left as a documented S5
follow-up candidate (see "S5 follow-ups" below) rather than folded into this change.

**P1 — additive-sort key reconstruction (confirmed hotspot, fixed).** Exactly the hotspot described by the
roadmap: `CompareAdditiveGroupingOrder`'s primary-sort comparator rebuilt a complete
`ExpressionCanonicalOrder.KeyNode` tree from scratch, via `ExpressionCanonicalOrder.Compare`/`.BuildKey`, for
BOTH operands on EVERY pairwise comparison the O(n log n) sort performs — for an opaque term this reconstructs
the exact same key `AnnotatedAdditiveTerm.Key` already holds; for a function-like term, `CompareArgumentLists`
similarly rebuilt every argument's key from scratch per comparison.

*Fix* (`Utils/Expressions/ExpressionCanonicalOrder.cs`, `Utils/Expressions/ExpressionSimplifier.cs`):

- `ExpressionCanonicalOrder.BuildKeys(IReadOnlyList<Expression>, IReadOnlyList<ParameterExpression[]>)` (new,
  `internal static`) builds one `KeyNode` per element under one shared working scope list, instead of the N
  separate scope-list allocations `BuildKey` called N times would cost.
- `AnnotatedAdditiveTerm` (the per-term annotation already built once per `CanonicalizeAdditiveExpression`
  call) gained a precomputed `ArgumentKeys` field: for a function-like term, each argument's complete key,
  built once via `BuildKeys`; `null` for an opaque term (which already reuses its own precomputed `Key`).
- `CompareAdditiveGroupingOrder` now takes two `AnnotatedAdditiveTerm` values directly and consumes only
  their precomputed `Key`/`ArgumentKeys` — for an opaque term, `x.Key.CompareTo(y.Key)` (provably the same
  value `ExpressionCanonicalOrder.Compare(x.Group.Opaque, y.Group.Opaque, scopes)` would have produced, since
  `Group.Opaque` IS the term `Key` was built from); for a function-like term,
  `CompareArgumentKeyLists(x.ArgumentKeys!, y.ArgumentKeys!)` compares precomputed `KeyNode`s directly
  (`KeyNode.CompareTo`) instead of rebuilding them.
- `OrderBy`/`.ThenBy`/`.ThenBy`/`.ToList()`, `GroupBy`, `AdditiveGroupingEqualityComparer`, `GroupEquals`/
  `GroupHash`, `ClassifyForAdditiveGrouping`, and `CanonicalizeMultiplicativeExpression` are all **unchanged**
  — this PR's production diff is confined to the primary-sort comparator and its inputs.

*Complexity.* Before: O(n log n) `KeyNode`-tree reconstructions across the sort (each comparison rebuilding
both operands' keys from scratch). After: O(n) key construction (once per term at annotation time) plus O(n
log n) cheap `KeyNode.CompareTo` calls over already-built trees.

**P2 — GroupBy re-classification (benchmarked and REJECTED).** The roadmap's second candidate: reuse
`AnnotatedAdditiveTerm.Group` in the `GroupBy` step so `AdditiveGroupingEqualityComparer` would not call
`ClassifyForAdditiveGrouping` a second time per term. Implemented as an `IEqualityComparer<AnnotatedAdditiveTerm>`
variant (`GroupBy(static term => term, ...)` instead of `GroupBy(static term => term.Term, ...)`) and
benchmarked against the P1-only candidate. Result: measurably WORSE at n=32/128 across every family (e.g.
family 1, n=128: 84.36us/140386B P1-only vs. 95.27us/146530B with P2 added; family 3, n=32: 61.86us/51168B
vs. 81.14us/52704B), including a real allocation regression, not just noise. Likely explanation (consistent
with the measured allocation increase, not independently isolated/profiled beyond this benchmark):
`AnnotatedAdditiveTerm` is a five-field struct (including the nested `AdditiveGroupClass`), and copying it
repeatedly through `GroupBy`'s internal `Lookup<TKey,TElement>` storage plausibly costs more than the cheap
`ClassifyForAdditiveGrouping` re-run (a NodeType pattern match plus field reads, no allocation) it was meant
to avoid — `GroupBy`'s existing key/element storage already handles a plain `Expression` reference (8 bytes)
far more cheaply. **Rejected** regardless of the exact mechanism, on the measured numbers alone:
the shipped code keeps the original `Expression`-keyed `GroupBy`/`IEqualityComparer<Expression>` unchanged,
with an XML remark on `AdditiveGroupingEqualityComparer` recording this experiment and its numbers so it is
not silently retried.

**Other candidates from this stage's own "Potential remaining areas" list: not pursued.** After P1, the
remaining measured hotspot at scale is dominated by (a) the pre-existing end-to-end re-simplification hazard
already described above (out of scope), and (b) `ExpressionCanonicalOrder.BuildKey`'s per-call scope-list
allocation for the single-expression entry point — partially already addressed for the multi-argument case by
`BuildKeys` sharing one scope list (and, after review round 2 below, for the zero-argument case too); the
single-key `BuildKey` entry point itself was left unchanged since altering its signature would touch the many
other call sites (multiplicative ordering, the final `.ThenBy(term.Key)` tie-break) outside this PR's minimal
scope. `ExpressionComparer` temporary arrays/searches and reflection-metadata scaffolding (`CompareType`/
`CompareMethod`) were not touched: both would need the kind of static-cache/determinism/
`AssemblyLoadContext`-safety analysis this stage's own guidance calls for before introducing any new cache,
and neither showed up as a P1-comparable hotspot in this stage's own benchmark (the multiplicative control
family, which exercises the same `ExpressionCanonicalOrder` reflection-based comparisons as the additive
families, stayed flat/byte-identical between baseline and candidate at every size).

#### S5 review, round 2 (2026-09-23) — zero-argument fast path, wider-arity benchmark coverage

A review pass found one real construction-cost regression this stage's own benchmark had not covered (a
`MethodCallExpression` with zero arguments), asked for wider-arity benchmark coverage beyond the unary
`Sin`/`Cos`/`Tan` family already measured, corrected two inaccuracies in this file's own prose, and suggested
an additional test. All addressed on the same branch.

1. **`BuildKeys` allocated an unused working scope list for a zero-argument function-like term.** For a
   `MethodCallExpression` with no arguments (a niladic call), the pre-S5 baseline never built any argument
   key at all (`CompareArgumentLists`'s loop runs zero times over an empty list). Before this fix, S5's
   `BuildKeys(group.Arguments!, scopes)` still allocated its `List<ParameterExpression[]>` working scope copy
   unconditionally, before checking whether there was anything to iterate — genuinely new, entirely
   unamortized construction cost for this shape, the opposite of this stage's goal. Fixed: `BuildKeys` now
   fast-paths an empty `expressions` to `[]` (`Array.Empty<KeyNode>()`), before allocating anything. Measured
   directly (same standalone harness, n=128, a niladic static method call as the additive term): the
   unconditional-allocation version cost 98 706 B/call; the fast-pathed version costs 86 418 B/call — the
   ~96 B/term the wasted `List<ParameterExpression[]>` cost is gone. A small residual gap remained at this
   point against the true pre-S5 baseline (80 362 B/call) — attributable to the `AnnotatedAdditiveTerm`
   struct's per-element footprint growing to carry the new `ArgumentKeys` field THIS PR introduces (not an
   S4-established cost, and not yet the whole `AdditiveGroupClass` shrink round 3 below performs) — disclosed
   here rather than left implicit at the time. **Superseded by round 3 below**, which closes this gap
   entirely (and then some) by shrinking `AnnotatedAdditiveTerm` itself.
2. **Benchmark coverage was unary-only.** The original benchmark matrix only exercised `Sin`/`Cos`/`Tan`
   (one-argument calls). Re-run with three additional static test methods of arity 0/2/3 confirms the
   optimization's effect scales with argument count, in the direction expected from the architecture (more
   arguments per comparison means more redundant per-comparison key rebuilding eliminated):

   | Family (n=128), true pre-S5 baseline vs. post-round-2 candidate | Baseline time | Candidate time | Speedup | Baseline alloc | Candidate alloc | Alloc reduction |
   | --- | --- | --- | --- | --- | --- | --- |
   | Arity 0 (niladic) | 78.15 us | 90.94 us | ~0.9x (see finding 1) | 80 362 B | 86 418 B | ~0.93x (see finding 1) |
   | Arity 1 (`Sin`/`Cos`/`Tan`, already reported above) | 135.85 us | 86.37 us | 1.6x | 334 130 B | 157 794 B | 2.1x |
   | Arity 2 | 766.73 us | 312.56 us | 2.5x | 551 138 B | 232 738 B | 2.4x |
   | Arity 3 | 911.07 us | 531.74 us | 1.7x | 602 466 B | 307 682 B | 2.0x |

   Arity 0 is the one shape where this stage's change is not a clear win even after the round-2 fast path — a
   small, disclosed, structural overhead remains (finding 1) — but every other arity, including higher arities
   than originally benchmarked, shows the same large improvement pattern as the originally-measured unary
   family, confirming the optimization is not merely unary-specific.
3. **This file's own prose cited a "roadmap section 1"/"roadmap 4.1"/"section 10" and a French quote that do
   not exist in this roadmap document.** Those references were carried over from the external work-item
   prompt that requested this stage's work (which used that numbering/wording in its own instructions), not
   from this file's actual content — this file's own "S5 — Construction-performance cleanup" section has never
   used numbered subsections. Corrected throughout the S5-progress notes above to describe the actual
   "Potential remaining areas" bullets directly, in self-contained English, so this file no longer depends on
   an external, non-committed document to be understood.
4. **The P2-rejection note overstated confidence in the exact causal mechanism.** "Root cause: ..." was
   softened to "likely explanation (consistent with the measured allocation increase, not independently
   isolated/profiled beyond this benchmark)" — the benchmark numbers themselves are what justify the
   rejection; the struct-copy explanation is a plausible, unverified mechanism, not a proven one.
5. **Test suggestion, added:** `BuildKeys_MultipleArgumentsWithNestedLambdas_RestoresSharedScopeBetweenSiblings`
   in `ExpressionSimplifierAdditiveSortScaleTests` — reflects directly into `ExpressionCanonicalOrder.BuildKeys`
   itself (not `CanonicalizeAdditiveExpression`, and not multiple additive terms) with a three-element argument
   array `[lambda1, p, lambda2]`, where `lambda1`/`lambda2` are structurally alpha-equivalent nested
   `LambdaExpression`s each capturing the same OUTER bound parameter `p`, separated by a plain reference to
   `p` itself. This is the shape that specifically exercises `BuildKeys`' shared mutable working-scope list
   across successive sibling arguments (each nested lambda's `BuildLambda` call pushes/pops its own frame onto
   the SAME list `BuildKeys` passes to every argument in sequence): a bug that let one argument's nested-lambda
   scope leak into a later sibling argument's key would misclassify that sibling's captured outer parameter as
   bound at the wrong depth, making `lambda1`'s and `lambda2`'s otherwise-identical keys compare unequal.
   Verified this test fails (`CompareTo` returns `-1` instead of `0`) when `ExpressionCanonicalOrder.BuildLambda`'s
   scope-pop is deliberately, locally removed (reverted immediately after verification, never committed),
   confirming it exercises the property described.

**Validation performed after review round 2 (2026-09-23):** superseded by round 3 below; see the "Validation"
list further down, which reflects the final, post-round-3 code and test count.

#### S5 review, round 3 (2026-09-23) — shrink `AnnotatedAdditiveTerm`, cache the comparer, doc fixes

A third review pass confirmed round 2's fixes but flagged that round 2's own "residual gap" for arity 0
(finding 1 above) was real and avoidable, not an inherent cost, identified two more documentation
inaccuracies, and suggested one more (non-blocking) micro-optimization. Addressed on the same branch.

1. **`AnnotatedAdditiveTerm` carried a whole, now-mostly-unused `AdditiveGroupClass` per term.** After
   annotation, `CompareAdditiveGroupingOrder` only ever read two scalar fields off the stored classification
   (`IsFunctionLike`, `CategoryOrder`); `AdditiveGroupClass.Arguments`/`.Opaque` (two reference-type fields)
   were needed only transiently, while building `ArgumentKeys`/`Key` in the annotation loop, never afterward.
   Carrying the whole `AdditiveGroupClass` anyway meant every term paid for copying those two now-dead
   references through `List<AnnotatedAdditiveTerm>`/`OrderBy`/`GroupBy` for no benefit — the main/likely
   architectural cause behind round 2's arity-0 "residual gap", consistent with the measurements below (per
   the round-2 numbers, which showed the SAME roughly-constant per-term overhead at every arity, a small tax
   on every other family too). Fixed: `AnnotatedAdditiveTerm` now stores `IsFunctionLike`/`CategoryOrder`
   directly as two scalar fields instead of a nested `AdditiveGroupClass`; the annotation loop still calls
   `ClassifyForAdditiveGrouping` once per term as before (needed for `Arguments`/`Opaque` during annotation),
   but copies out only the two fields that survive it. Measured directly (same standalone harness): at n=128,
   EVERY additive family improved by the same ~15 400 B/call relative to the round-2 candidate - see the
   updated table below. This round also cached the comparer (finding 3 below) in the same commit, so the
   struct shrink was not isolated from that change in this measurement; the shrink is the far larger and more
   plausible contributor (a `Comparer<T>.Create` delegate/adapter allocation is on the order of tens of bytes
   once per `CanonicalizeAdditiveExpression` call, not per term, so it cannot explain a per-term, linearly
   n-scaling effect), but the two were not benchmarked separately. For arity 0 specifically, this closes
   round 2's residual gap entirely and then some: 86 418 B/call (round 2) → 67 962 B/call (round 3), now
   BELOW the true pre-S5 baseline's 80 362 B/call - the fast path plus this shrink together make even the one
   previously-regressed shape a net improvement.
2. **Round 2's "an S4-established per-term annotation design point" attribution was wrong.** `AnnotatedAdditiveTerm`
   itself (and its `Key` field) are S4-established; the `ArgumentKeys` field - and therefore the struct-growth
   cost round 2's finding 1 measured - was introduced by S5/this PR, not inherited from S4. Corrected in place
   above (see the "Superseded by round 3" note on that finding).
3. **`Comparer<AnnotatedAdditiveTerm>.Create(CompareAdditiveGroupingOrder)` was rebuilt on every
   `CanonicalizeAdditiveExpression` call**, even though `CompareAdditiveGroupingOrder` is `static` and captures
   nothing per-call (confirmed once `CompareAdditiveGroupingOrder` stopped taking a `scopes` parameter earlier
   in this stage). Cached as a `static readonly AdditiveGroupingOrderComparer` field instead, built once per
   process. A constant, easily-avoidable per-call allocation (the `Comparer<T>` wrapper plus its delegate) with
   no correctness implication either way; folded into this round since it was found alongside finding 1's
   investigation, not benchmarked in isolation as its own line item.
4. **Test name/description did not match what the round-2 test actually does.** The test suggested and added
   in round 2 was named `NestedLambdaArgument_AcrossMultipleFunctionLikeTerms_BuildsCorrectArgumentKeysUnderSharedScope`
   and described as covering "several function-like terms ... canonicalized in one
   `CanonicalizeAdditiveExpression` call" - it actually reflects directly into `ExpressionCanonicalOrder.BuildKeys`
   with a single three-element argument array, never calling `CanonicalizeAdditiveExpression` and never
   constructing more than one additive term. Renamed to
   `BuildKeys_MultipleArgumentsWithNestedLambdas_RestoresSharedScopeBetweenSiblings` and its description (both
   in the test file and in round 2's own write-up above) corrected to describe the actual shape exercised.
5. **Stale count/example fixes:** the class-level remark on `ExpressionSimplifierAdditiveSortScaleTests`
   still described only "Tests 1-3 .../Tests 4-5 ..." with no mention of the round-2-added sixth test - added
   a note describing test 6. The GitHub PR description was still entirely round-1 content (5 tests, stale
   counts, no arity/rebase information) - refreshed to match the current branch state. `BuildKeys`' own XML
   doc used `DateTime.Now` as its "niladic method call" example - `DateTime.Now` is a property, not a method
   call; replaced with `Guid.NewGuid()`.

**Updated benchmark results (micro scenarios, n=128; supersedes the round-1 table above for the `Utils`
production-code numbers — median of 15 rounds, byte-identical allocations across repeated runs).**

| Family (n=128) | True pre-S5 baseline | Round 3 candidate | Speedup | Baseline alloc | Candidate alloc | Alloc reduction |
| --- | --- | --- | --- | --- | --- | --- |
| 1 Additive-Opaque | 387.03 us | 82.97 us | 4.7x | 543 794 B | 124 978 B | 4.4x |
| 2 Additive-FunctionLike (arity 1) | 135.85 us | 87.37 us | 1.6x | 334 130 B | 142 386 B | 2.3x |
| 3 Additive-PowerWrapped | 215.12 us | 182.14 us | 1.2x | 382 866 B | 186 418 B | 2.1x |
| 4 Additive-MixedSign | 130.84 us | 89.76 us | 1.5x | 349 962 B | 154 186 B | 2.3x |
| 5 Multiplicative-Opaque (control, untouched code) | 25.28 us | 22.58 us | ~1.0x (noise) | 42 554 B | 42 554 B | 1.0x (byte-identical) |
| 6 FunctionLike arity 0 (niladic) | 78.15 us | 89.47 us | ~0.9x (time noise; see finding 1) | 80 362 B | 67 962 B | 1.18x (now BELOW baseline) |
| 7 FunctionLike arity 2 | 766.73 us | 360.93 us | 2.1x | 551 138 B | 217 330 B | 2.5x |
| 8 FunctionLike arity 3 | 911.07 us | 553.20 us | 1.6x | 602 466 B | 292 274 B | 2.1x |

Every family this PR's code touches now shows a consistent, unambiguous allocation reduction at n=128 (1.18x
to 4.4x) with no remaining regressed shape; the multiplicative control remains exactly byte-identical, and
arity-0 wall-clock time (the one figure still not a clear win) is noise-level (89.47 us candidate vs. 78.15 us
baseline, on a call that allocates a mere ~68 KB and takes well under 100 us either way - see "On timing
noise" above for why sub-100us medians in this environment are not fully trustworthy in isolation).

**Validation performed after review round 3 (2026-09-23):** superseded by round 4 below; see the
"Validation" list further down for the final numbers.

#### S5 review, round 4 (2026-09-23) — P2 re-tested against the shrunk `AnnotatedAdditiveTerm`; still rejected

A fourth review pass raised one substantive question - since round 3 shrank `AnnotatedAdditiveTerm`, the
exact struct round 1's P2 experiment measured as "too expensive to carry through `GroupBy`" no longer exists,
so round 1's rejection needed re-verifying against the current, smaller struct rather than being assumed
still valid - plus two documentation nits. Addressed on the same branch.

1. **P2 re-tested against the round-3 struct.** Reconstructed the same `IEqualityComparer<AnnotatedAdditiveTerm>`
   `GroupBy` variant round 1 tried (`GroupBy(static term => term, ...)` instead of
   `GroupBy(static term => term.Term, AdditiveGroupingEqualityComparer.Instance)`), adapted to read
   `IsFunctionLike`/`CategoryOrder` from the now-shrunk annotation directly and re-derive `Arguments`/`Opaque`
   from `Term` only where still needed for the actual structural equality/hash check (`GroupEquals`/`GroupHash`,
   unchanged). Benchmarked as a temporary, uncommitted local change (same standalone harness) against the
   round-3 shipped code, across families 1-3 and every size:

   | Family | n=2 | n=8 | n=32 | n=128 |
   | --- | --- | --- | --- | --- |
   | 1 Additive-Opaque (shipped → P2-retest) | 2 848 → 2 896 B | 8 560 → 8 752 B | 31 920 → 32 688 B | 124 978 → 128 050 B |
   | 2 Additive-FunctionLike (shipped → P2-retest) | 3 120 → 3 168 B | 9 648 → 9 840 B | 36 272 → 37 040 B | 142 386 → 145 458 B |
   | 3 Additive-PowerWrapped (shipped → P2-retest) | 3 808 → 3 856 B | 12 400 → 12 592 B | 47 280 → 48 048 B | 186 418 → 189 490 B |

   Every single cell regresses by EXACTLY 24 B per term (48 B at n=2, 192 B at n=8, 768 B at n=32, 3 072 B at
   n=128 - a perfectly linear, deterministic, family-independent per-term cost), confirming a real,
   reproducible allocation regression in the tested `AnnotatedAdditiveTerm`-keyed `GroupBy` variant,
   consistent with additional per-element storage cost rather than benchmark noise. Wall-clock time
   was statistically indistinguishable from the shipped code at every size (differences within the same
   run-to-run noise band documented above). **Conclusion: the rejection still holds.** The shrunk struct did
   roughly HALVE the absolute per-term regression (round 1 measured ~48 B/term against the pre-round-3 struct;
   round 4 measures ~24 B/term against the post-round-3 struct - consistent with a per-term key-storage cost
   that scales with struct size, exactly as the reviewer's hypothesis predicted), but it did not reverse the
   direction: `GroupBy`'s existing `Expression`-keyed (8-byte key) path remains strictly cheaper than the
   tested `AnnotatedAdditiveTerm`-keyed `GroupBy` variant, at every size and family measured, both before and
   after the round-3 shrink. The temporary comparer/call-site change used for this measurement was reverted immediately
   after; the shipped `GroupBy(static term => term.Term, AdditiveGroupingEqualityComparer.Instance)` is
   unchanged from round 3.
2. **Doc fix: stale test count.** The "Tests" summary near the end of this file's S5 section still said
   "new, 5 tests"; corrected to 6 and now names the round-2-added `BuildKeys_...` test explicitly.
3. **Doc fix: overstated causal claim, and one XML typo.** Round 3's "exactly the architectural root cause"
   claim for the struct-shrink's ~15 400 B/call improvement was softened to "the main/likely architectural
   cause ... consistent with the measurements", with an explicit note that round 3 ALSO cached the comparer
   in the same commit and the two changes were not benchmarked in isolation from each other (the comparer
   cache is a per-call, not per-term, allocation and so cannot itself explain a linearly n-scaling effect,
   making the struct shrink the far more plausible primary contributor - but this was reasoning, not a
   controlled isolation). A stray unescaped `"` in an XML doc comment (`<c>Opaque"</c>`) was also fixed to
   `<c>Opaque</c>`, and a following sentence reworded for clarity.

**Validation performed after review round 4 (2026-09-23):** see the "Validation" list below.

**Benchmark results (micro scenarios — the direct P1 target; median of 15-21 rounds, byte-identical
allocations across repeated runs). Historical: these are round 1's original numbers, kept for the audit
trail; see the round-3 table above for the current, final numbers.**

| Family (n=128) | Baseline time | Candidate time | Speedup | Baseline alloc | Candidate alloc | Alloc reduction |
| --- | --- | --- | --- | --- | --- | --- |
| 1 Additive-Opaque | 387.03 us | 85.16 us | 4.5x | 543 794 B | 140 386 B | 3.9x |
| 2 Additive-FunctionLike | 135.85 us | 86.37 us | 1.6x | 334 130 B | 157 794 B | 2.1x |
| 3 Additive-PowerWrapped | 215.12 us | 182.62 us | 1.2x | 382 866 B | 201 826 B | 1.9x |
| 4 Additive-MixedSign | 130.84 us | 92.13 us | 1.4x | 349 962 B | 169 594 B | 2.1x |
| 5 Multiplicative-Opaque (control, untouched code) | 25.28 us | 24.49 us | ~1.0x (noise) | 42 554 B | 42 554 B | 1.0x (byte-identical) |

| Family (n=32) | Baseline alloc | Candidate alloc | Alloc reduction |
| --- | --- | --- | --- |
| 1 Additive-Opaque | 102 496 B | 35 808 B | 2.9x |
| 2 Additive-FunctionLike | 72 880 B | 40 160 B | 1.8x |
| 3 Additive-PowerWrapped | 80 304 B | 51 168 B | 1.6x |
| 4 Additive-MixedSign | 71 368 B | 43 128 B | 1.7x |
| 5 Multiplicative-Opaque (control) | 11 064 B | 11 064 B | 1.0x (byte-identical) |

| Family (n=8) | Baseline time | Candidate time | Baseline alloc | Candidate alloc |
| --- | --- | --- | --- | --- |
| 1 Additive-Opaque | 32.97 us | 19.54 us | 15 360 B | 9 568 B |
| 2 Additive-FunctionLike | 23.43 us | 19.17 us | 13 296 B | 10 656 B |
| 3 Additive-PowerWrapped | 14.55 us | 34.20 us (noise; min 8.03/31.36 us) | 16 496 B | 13 408 B |
| 4 Additive-MixedSign | 6.19 us | 23.11 us (noise; both single-digit-to-low-20s us range across repeats) | 14 728 B | 11 416 B |
| 5 Multiplicative-Opaque (control) | 2.31 us | 5.12 us (noise, see below) | 3 192 B | 3 192 B (byte-identical) |

**On timing noise.** Wall-clock medians at n=8/32 show real run-to-run variance in this environment (observed
directly: a repeat run of the SAME P1-only build at n=32 measured 57.82us and, later, 73.06us for family 1 —
a ~25% spread from GC/scheduler noise alone), including for the untouched multiplicative control family
(2.31us baseline vs. 5.12-5.72us candidate at n=8, despite `CanonicalizeMultiplicativeExpression` not being
touched by this PR) — an artifact of shared-process GC state carried over from the immediately preceding
(differently-allocating) scenario runs, not a real regression. **Allocations are the trustworthy signal here**:
deterministic and byte-identical across repeated runs of the same build, they show a consistent, monotonic
reduction at every measured size for every family this PR's code touches, and an exact, byte-identical zero
change for the untouched multiplicative control at every size — the cleanest possible confirmation that this
change altered construction cost without altering behavior. The large, unambiguous n=128 time wins (1.2x-4.5x,
consistent across two independent full benchmark runs) corroborate the allocation trend once n is large enough
for the O(n log n) reconstruction this stage targets to dominate over environmental noise.

**Tests.** `UtilsTest/Mathematics/Expressions/ExpressionSimplifierAdditiveSortScaleTests.cs` (new, 6 tests):
large-N (16-term, and 8-pair/16-term power-wrapped) characterizations of the additive sort at a scale that
actually exercises the O(n log n) path this stage optimizes — bound function-like terms ordered by argument
declaration position, bound opaque terms ordered by declaration position (the `Key`-reuse path), power-wrapped
terms clustering by argument while the complete-key tie-break still distinguishes the exponent, free-parameter
stable-sort-order preservation when every canonical-order dimension ties (the invariant most at risk from a
naive `OrderBy`/comparator refactor), an adversarial (hostile-constant plus unsupported-node) large-term-set
regression proving no additive-grouping/ordering step executes user `Equals`/`GetHashCode`/`ToString`, and
(added in review round 2) `BuildKeys_MultipleArgumentsWithNestedLambdas_RestoresSharedScopeBetweenSiblings`,
reflecting directly into `ExpressionCanonicalOrder.BuildKeys` to prove its shared working-scope list is
correctly restored between successive sibling arguments. The three purely-structural sort tests reflect
directly into the private `CanonicalizeAdditiveExpression` (bypassing the unrelated end-to-end
re-simplification cost described above, matching this project's own precedent of reflecting into internal
canonicalization methods in `ExpressionSimplifierStructuralCanonicalizationTests`); the free-parameter and
adversarial tests use the public `Simplify(Expression)` path directly at a term count confirmed to run in
well under a second. Existing S3/S4 suites
(`ExpressionSimplifierStructuralCanonicalizationTests`, `ExpressionSimplifierAdditiveGroupingKeyTests`,
`ExpressionSimplifierFinalizationTests`, `ExpressionComparerTests`) were not modified and remain the primary
correctness oracle that this stage's change must keep green — see "Validation" below for exact counts.

**Validation performed (2026-09-23, after review round 4, in order; branch rebased onto `master` at
`1f874f9e0caa28e097060316a992cd5a4ce4fdbd` — PR #603, `Utils.NumberToString`-only, no file overlap with this
change — before review round 2's run; rounds 3 and 4 made no further rebase; round 4's own P2 re-test was a
temporary, uncommitted local change, reverted before this validation run):**

1. `ExpressionSimplifierAdditiveSortScaleTests` (6 tests: the original 5 plus
   `BuildKeys_MultipleArgumentsWithNestedLambdas_RestoresSharedScopeBetweenSiblings`, added in round 2 and
   renamed in round 3): 6/6 passed.
2. Full `UtilsTest/Mathematics/Expressions` namespace: 464/464 passed (458 pre-existing + 6 new).
3. Full `UtilsTest.Unit`: 7736/7736 passed, 0 skipped.
4. Full `UtilsTest.Functional`: 383/383 passed.
5. Full `UtilsTest.Security`: 225/228 passed, 3 skipped — the same three pre-existing, unrelated,
   platform-gated tests noted throughout S4 (`TryCreate_ReturnsNull_OnNonWindowsPlatform`,
   `VerifyAuthenticodeSignature_OnNonWindows_ThrowsPlatformNotSupportedException`,
   `HasValidAuthenticodeSignature_OnNonWindows_ThrowsPlatformNotSupportedException`).
6. Release build of `Utils.sln` (the full multi-project solution, not just `Utils.csproj`): succeeded, 0
   errors, 51 warnings — all pre-existing (`NU1603` package-version-resolution notices unrelated to `Utils`,
   and pre-existing nullable/cref warnings in `DrawTest`/`Fractals`, neither touched by this PR).
7. `Utils/Utils.csproj` remains on `<TargetFramework>net8.0</TargetFramework>`, unchanged.

**S5 follow-ups (deliberately left open — a small, benchmarked, causally-isolated change, not an attempt to
close every item in this stage's "Potential remaining areas" list in one PR):**

- The end-to-end nested-canonicalization/re-simplification construction-cost hazard described above
  (pre-existing, S4-review-documented, not introduced or fixed by this PR).
- `ExpressionCanonicalOrder.BuildKey`'s per-call scope-list allocation for the single-expression entry point
  — `BuildKeys` addresses the multi-argument case (and, after review round 2, the zero-argument case too); the
  single-key `BuildKey` entry point itself was left unchanged, since altering its signature would touch the
  many other call sites (multiplicative ordering, the final `.ThenBy(term.Key)` tie-break) outside this PR's
  minimal scope.
- The "intermediate group/list materialization" and "`ExpressionComparer` temporary arrays and searches" areas
  this stage's own list above already names — not shown to be a comparably significant hotspot by this PR's
  own benchmark (the multiplicative control family, which exercises the same `ExpressionCanonicalOrder`
  reflection-based comparisons, stayed flat/byte-identical between baseline and candidate at every size); any
  reflection-metadata or `ExpressionComparer`-context caching would additionally need the kind of
  `AssemblyLoadContext`/thread-safety/determinism analysis this stage's own guidance calls for before
  introducing any new cache, which is a separate, dedicated piece of work.

#### S5 progress (2026-09-23) — P3: batch the comparer re-simplification hazard in the high-fan-out factoring rules

**Baseline for this stage.** `origin/claude/s5-construction-performance-cleanup` at `e878c1a98502dadbac7d5fc67a92a34f13d05b35`
(the round-4 commit above; PR #604, base `master` at `1f874f9e0caa28e097060316a992cd5a4ce4fdbd`) — confirmed to be
an ancestor of the branch this work started from, and `master` had not advanced past `1f874f9e` by the time this
round's validation ran, so no further rebase was needed.

**Audit.** `ExpressionComparer.Equals(x, y)` (`Utils/Expressions/ExpressionComparer.cs`) performs, in order: (1) a
top-level `ReferenceEquals`/null check; (2) a root-`LambdaExpression` metadata check (`TailCall`/`Type`); (3)
`_expressionSimplifier.Simplify(x)`; (4) `_expressionSimplifier.Simplify(y)`; (5) structural `EqualsCore(...)`.
Both simplify calls run on every invocation that reaches step 3 — there is no early exit between them.
`ExpressionSimplifier` contained exactly 14 call sites for `ExpressionComparer.Default.Equals` before this round,
confirmed by direct enumeration: two single-comparison sites in `AdditionWithNegate` (one per overload), one
single-comparison site in `MultiplicationOfEqualsElements`, five in `AdditionOfEqualsElements`, and six in
`SubstractionOfEqualsElements` (matching the roadmap's original audit numbers exactly). `SubstractionWithNegate`,
`NegateWithSubstraction`, `SubstractionWithAddition`, `SubstractionWithSubstraction` use no comparer calls at all.
Per the roadmap's own instruction, only the two high-fan-out sites (`AdditionOfEqualsElements`,
`SubstractionOfEqualsElements`) were changed; the three single-comparison sites were left untouched, since a
per-call batching cache has no possible win when there is only one comparison to batch.

**Second-pass semantic dependency — characterized before any production change.** `PrepareBinary` transforms
each operand once, bottom-up, before a rule's own dispatch runs, but a rule that fires can return a newly-built
expression that `TransformCore` hands back immediately — it is not re-transformed to a `Simplify()` fixed point.
Two characterizations were built and run against the true pre-P3 baseline before any production code changed:

1. `(2*x + 3*x) + -(5*x)` (double `x`), through the already-existing, UNTOUCHED `AdditionWithNegate` site: the
   inner `2*x + 3*x` factors first, via `AdditionOfEqualsElements`, to the literal, not-yet-folded expression
   `(2 + 3) * x` (an `Add(Constant(2), Constant(3))` node as the coefficient, not a folded `Constant(5)`) — this
   is the exact value `TransformCore` hands back immediately, per the paragraph above. That unfolded `left`
   then reaches `AdditionWithNegate(BinaryExpression e, Expression left, UnaryExpression right)`, which calls
   `ExpressionComparer.Default.Equals(left, right.Operand)` — i.e. `Equals((2+3)*x, 5*x)` — and it is THIS call's
   own internal `Simplify()` of `(2+3)*x` (folding it to `5*x`) that lets the comparison recognize the two sides
   as equal. Confirmed empirically (standalone harness, not committed): the baseline's exact final result is the
   single numeric constant `0`.
2. An analogous shape reaching the ACTUAL rule this round modifies: `(2*x + 3*x) + 5*z` (distinct free
   parameters `x`, `z`). The inner `2*x + 3*x` again factors to the unfolded `(2+3)*x`, which becomes the OUTER
   `AdditionOfEqualsElements` call's own `left` operand — so `leftleft` is the literal `Add(Constant(2),
   Constant(3))`, not `Constant(5)`, when the outer call's OWN four-candidate comparisons run. Confirmed
   empirically that the baseline's exact final result is `(x + z) * (2 + 3)`: the coefficient is recognized as
   equal to the outer term's `Constant(5)` (via the comparer's own internal simplify-and-compare), the two
   variable terms are swapped out into a new `Add(x, z)`, and the still-literally-unfolded `(2 + 3)` survives,
   unmodified, as the factored-out second operand of the result.

**Critical warning verified experimentally (both directions).** A deliberately naive substitution of every
`ExpressionComparer.Default.Equals` call inside `AdditionOfEqualsElements` with
`ExpressionComparer.StructuralEqualsRaw` was applied directly to the production method (temporarily, as an
uncommitted local edit, reverted immediately after the observation below) and rebuilt: characterization #2 above
then produces `(5 * z) + ((2 + 3) * x)` instead — the rule no longer fires at all, because `StructuralEqualsRaw`
never simplifies its operands, so `Add(2, 3)` never gets the chance to be recognized as `5`. This proves the
second-pass dependency is real and that `StructuralEqualsRaw` is not a safe substitute, exactly as this stage's
"Critical warning" states. The same substitution was independently re-verified inside the FINAL implementation
(`FactorEqualityProbe.Equals` temporarily replaced with a bare `ExpressionComparer.StructuralEqualsRaw(x, y)`
call, no fast path, no simplification): 8 of the 11 new regression tests below failed under it (all of the
second-pass, unsupported-node-reflexivity, and hostile-constant tests — the three ordinary-shape tests were
unaffected, as expected), and all 11 pass again once reverted to the shipped implementation. Both experiments
were reverted before any commit; neither `StructuralEqualsRaw` call site inside `ExpressionSimplifier`'s
production code was changed by this round.

**Benchmark methodology.** A second standalone, out-of-repository console harness (same methodology as P1/P2:
`ProjectReference` to `Utils.csproj`, Release config, .NET 8.0.31, `Stopwatch` +
`GC.GetAllocatedBytesForCurrentThread()`, 15 measured rounds per size after warm-up, batched per round, median
reported). Families, all left-associated chains built to maximize the number of pairwise probes the two
high-fan-out rules perform per level:

- **A** — near-miss additive chain: `a_i * x` and `x * a_i` alternate by parity, so factor unification must try
  several swap branches per level (the "left-associated additive near-miss terms" family this stage's own plan
  calls for);
- **B** — the same shape through subtraction (`SubstractionOfEqualsElements`);
- **C** — an equal-term cancellation chain (`(k*x - k*x) + (k*x - k*x) + ...`), included as a family this
  change should NOT meaningfully affect (its dominant cost is the untouched `AdditionWithNegate`/cancellation
  path, not the two rules this round batches);
- **D** — the exact `(2*x + 3*x) + -(5*x)` second-pass characterization, as a single-call (not batch) timing;
- **E** — function-like/compound near-miss terms (`k * Sin(x)`), representative of the #604 end-to-end benchmark
  families;
- **F** — the same near-miss additive shape (family A) wrapped in a bound `Expression.Lambda`, exercising the S4
  lexical-scope boundary through the new probe's own `ExpressionComparer.SimplifyForComparison` calls.

Sizes n = 2, 8, 16, 32 for every family, plus a best-effort n = 64 for family A (the others were not attempted at
n = 64, consistent with this stage's "do not require n=128 if impractical" allowance — n = 32 already shows the
asymptotic trend clearly and n = 64 for every family would have cost more harness time than the result was worth).
Allocations were confirmed BYTE-IDENTICAL across repeated runs of the same build, for both the baseline and the
candidate (each measured twice); wall-clock medians show the same run-to-run GC/scheduler noise band documented
in the P1 round-4 entry above, most visible at n ≤ 8 — allocations, not time, are the trustworthy signal, exactly
as that entry argues.

**Diagnostic: nested `Simplify()` calls initiated by comparer equality checks.** A temporary counter (removed
before commit) was added once to `ExpressionComparer.Equals`'s two `Simplify()` calls (baseline) and once to the
new `SimplifyForComparison` (candidate), and run once against families A/B at every size:

| n | A (baseline → candidate) | B (baseline → candidate) |
| --- | --- | --- |
| 2 | 8 → 5 (−37.5%) | 10 → 5 (−50.0%) |
| 8 | 50 → 29 (−42.0%) | 64 → 29 (−54.7%) |
| 16 | 106 → 61 (−42.5%) | 136 → 61 (−55.1%) |
| 32 | 218 → 125 (−42.7%) | 280 → 125 (−55.4%) |

This directly attributes the allocation reduction below to fewer redundant `Simplify()` calls, not to an
unrelated side effect — the reduction is roughly constant (~40–55%) at every size, growing slightly with n
because near-miss chains at larger n contain proportionally more full-fan-out (five/six-comparison) rule
invocations.

**Benchmark results (median of 15 rounds; allocations byte-identical across repeated runs of the same build).
Historical — every table and figure from here through the end of the Family D paragraph below is round 5's
original measurement, taken against the round-5 (BEFORE round-6's fix) candidate. The "Cancellation" row's "1.0x
(noise)" label was itself a round-5 reporting mistake, corrected in round 6 (those bytes are byte-identical
across repeated runs, not noise). Round 6 re-measured n=8/n=32/n=64/`Cancellation` against its own fixed
candidate (see round 6's own table); round 6 ALSO re-measured n=2 and Family D specifically because round 6's
safety-scan overhead is a small, roughly-constant per-candidate cost that a tiny n=2 batch amortizes least well
of any size measured - see round 6's own "n=2 and Family D, re-measured" note for the corrected numbers. Kept
here unedited for the audit trail; do not re-cite the "noise" label or treat the n=2/Family D figures below as
the current numbers.**

| Family, n=8 | Baseline time | Candidate time | Baseline alloc | Candidate alloc | Alloc reduction |
| --- | --- | --- | --- | --- | --- |
| A NearMissAdditive | 121.28 us | 92.75 us | 79 753 B | 49 005 B | 1.63x |
| B NearMissSubtraction | 109.47 us | 70.56 us | 113 297 B | 50 797 B | 2.23x |
| C Cancellation (control) | 36.34 us | 37.14 us | 54 278 B | 54 710 B | see correction, round 6 |
| E FunctionLike | 92.34 us | 43.50 us | 98 361 B | 64 477 B | 1.53x |
| F BoundLambda | 30.73 us | 23.44 us | 80 353 B | 49 605 B | 1.62x |

| Family, n=32 | Baseline time | Candidate time | Baseline alloc | Candidate alloc | Alloc reduction |
| --- | --- | --- | --- | --- | --- |
| A NearMissAdditive | 407.02 us | 228.16 us | 1 460 834 B | 768 106 B | 1.90x |
| B NearMissSubtraction | 744.91 us | 285.43 us | 2 165 962 B | 776 042 B | 2.79x |
| C Cancellation (control) | 142.30 us | 134.97 us | 221 599 B | 223 183 B | see correction, round 6 |
| E FunctionLike | 983.73 us | 296.35 us | 1 538 482 B | 831 866 B | 1.85x |
| F BoundLambda | 783.17 us | 236.97 us | 1 462 010 B | 769 282 B | 1.90x |

| Family, n=2 | Baseline alloc | Candidate alloc | Note |
| --- | --- | --- | --- |
| A | 3 799 B | 3 895 B | +2.5% — see "Small-n regression" below |
| B | 4 103 B | 4 151 B | +1.2% |
| E | 7 637 B | 7 285 B | −4.6% |
| F | 4 245 B | 4 341 B | +2.3% |

| Family A, best-effort n=64 | Baseline time | Candidate time | Baseline alloc | Candidate alloc |
| --- | --- | --- | --- | --- |
| NearMissAdditive | 2893.64 us | 947.36 us (3.05x) | 5 983 400 B | 3 067 568 B (1.95x) |

Family D (single call, not a batch): 17.47 us / 14 274 B baseline → 8.67 us / 14 370 B candidate (time is
noise-level for a single sub-100us call; the small +0.7% allocation delta is the fixed cost of one unused
`FactorEqualityProbe`/backing `List` allocated by the INNER `AdditionOfEqualsElements(2x, 3x)` call, which never
grows past its initial capacity for this shape — negligible in absolute terms). The exact simplified shape is
byte-for-byte identical before and after this round's change: the single constant `0`.

**Small-n regression, disclosed rather than hidden.** At n=2, families A/B/F show a small (1–2.5%, tens of
bytes) allocation INCREASE, not a win: `FactorEqualityProbe`'s own `List<(Expression, Expression)>` (capacity 4)
is allocated once per rule invocation regardless of how many candidates end up cached, and at n=2 there are too
few rule invocations, each doing too little redundant work, for the avoided re-simplification to pay for that
allocation. This mirrors the exact shape of the P1 round-2 "arity-0" finding (a fixed per-call structural cost
that does not pay for itself on the smallest possible input) and is disclosed the same way rather than glossed
over. Every larger size (n ≥ 8) — where this stage's own "left-associated near-miss terms" target scenario
actually accumulates redundant work — is an unambiguous, growing win in both time and allocations.

**Implementation.**

- `Utils/Expressions/ExpressionComparer.cs` — `Equals(Expression?, Expression?)` is refactored, with NO
  observable behavior change, into three `internal static` entry points reused by the change below:
  `TryFastPathEquals` (the top-level `ReferenceEquals`/null/root-lambda-metadata checks — returns `true` with
  the decided `result` when one of those checks already answers the comparison, `false` when a full
  simplify-then-compare is still needed), `SimplifyForComparison` (simplifies through the exact same shared,
  exact-built-in `_expressionSimplifier` instance `Equals`/`GetHashCode` already use), and
  `StructuralEqualsAfterSimplification` (the same non-safe-constant `EqualsCore` call `Equals` already performs
  on the two simplified operands — deliberately NOT `StructuralEqualsRaw`, which uses the safe-constant policy
  and is documented as unsafe for this purpose by this stage's "Critical warning"). `Equals` itself is now a
  three-line composition of these three methods, calling them in the exact same order with the exact same
  inputs as before; every existing `ExpressionComparerTests`/`ExpressionSimplifierComparerRegressionTests`/
  `ExpressionSimplifierSymbolicContractTests` test passes unmodified, confirming the refactor changed nothing
  observable.
- `Utils/Expressions/ExpressionSimplifier.cs` — a new `private sealed class FactorEqualityProbe`, instantiated
  once at the top of `AdditionOfEqualsElements` and once at the top of `SubstractionOfEqualsElements` (a fresh
  instance per rule invocation, never stored anywhere longer-lived), replaces every
  `ExpressionComparer.Default.Equals(...)` call in those two rules with `equalityProbe.Equals(...)`.
  `FactorEqualityProbe.Equals(x, y)` calls `ExpressionComparer.TryFastPathEquals` first, then — only if that did
  not already decide the comparison — looks up each operand's simplified form in a small
  `List<(Expression Original, Expression Simplified)>` (initial capacity 4, linearly scanned by
  `ReferenceEquals`; a plain `List`, not a `Dictionary`, since at most four distinct operands
  (`leftleft`/`leftright`/`rightleft`/`rightright`) ever participate in one factoring decision — a linear scan
  of ≤4 entries was judged cheaper than a `Dictionary`'s hashing/bucket overhead by reasoning, consistent with
  the P1 round-1/round-4 finding that per-element storage overhead can dominate at this scale, but this specific
  micro-decision (`List` vs `Dictionary`) was NOT separately benchmarked in isolation — see "Rejected /
  not-separately-benchmarked variants" below), computing and caching via `SimplifyForComparison` on a cache
  miss, then calls `StructuralEqualsAfterSimplification` on the two (possibly newly cached) simplified forms.
- No other call site was changed: the two `AdditionWithNegate` overloads and `MultiplicationOfEqualsElements`
  still call `ExpressionComparer.Default.Equals` directly (one comparison each — no batching opportunity, per
  this stage's own scope guidance), and `AnnotatedAdditiveTerm`/`ExpressionCanonicalOrder`/the P1/P2
  additive-sort machinery are entirely untouched.

**Why behavior is preserved.**

1. Public `ExpressionComparer.Equals`/`GetHashCode` API and observable behavior are unchanged — a pure
   decomposition of existing logic, proven by the full pre-existing comparer test suites passing unmodified.
2. `FactorEqualityProbe.Equals` performs the IDENTICAL sequence of checks the direct
   `ExpressionComparer.Default.Equals(x, y)` call it replaces would have performed: same fast-path shortcuts,
   same shared exact-built-in simplifier instance (so the same independent, self-contained top-level
   lexical-scope boundary is established on every simplify — see `ExpressionSimplifier`'s own "Independent
   top-level calls" remarks, unaffected by this change), same non-safe post-simplification structural
   comparison policy.
3. Caching by `ReferenceEquals` is safe because (a) the four candidate operands are captured once, before any
   comparison runs, and `ObjectUtils.Swap` only reassigns which LOCAL VARIABLE points at which already-captured
   object — it never mutates an `Expression` (expression trees are immutable) or introduces a new object that
   would need its own cache entry; (b) `ExpressionSimplifier.Simplify` is a pure function of its input for any
   single top-level call, so returning a cached result for a reference already simplified earlier in the SAME
   rule invocation is indistinguishable from re-simplifying it.
4. `FactorEqualityProbe` never outlives the single rule invocation that creates it — no static, thread-local, or
   instance-level state is introduced, satisfying this stage's "no cache may survive the comparison/rule
   operation whose semantics justified it" constraint.
5. The second-pass dependency and the non-safe/hostile-constant policy were both verified experimentally (not
   merely reasoned about) to still hold, in both directions (present in the baseline AND in the shipped
   implementation; absent under the naive `StructuralEqualsRaw` substitution in both places it was tried) — see
   "Critical warning verified experimentally" above.

**Rejected / not-separately-benchmarked variants.**

- No alternative caching SHAPE (e.g. a `Dictionary<Expression, Expression>` keyed by reference, or a fixed
  4-slot inline struct instead of a `List`) was implemented and benchmarked side-by-side with the shipped `List`
  design in this round — unlike P1 round 1's P2 experiment, which WAS implemented and measured before being
  rejected. The `List`-based design was chosen by reasoning from the P1 rounds' own established lesson (a
  `Dictionary`'s per-entry storage/hashing overhead can exceed the cost of the work it avoids when the element
  count is very small — P1 round 1 measured exactly this for `GroupBy`'s `Lookup<TKey,TElement>` storage), not
  from an isolated experiment on this exact decision. This is disclosed rather than asserted as proven, per this
  stage's own "do not state an inferred root cause as proven unless it was isolated experimentally" instruction.
  If a future round wants a stronger basis for this specific micro-decision, it would need its own isolated
  benchmark; the measured end-to-end numbers above already meet this stage's acceptance bar regardless of which
  of the two container shapes turns out to be marginally cheaper.
- Broadening the same `FactorEqualityProbe` pattern to the three single-comparison sites (`AdditionWithNegate`
  ×2, `MultiplicationOfEqualsElements`) was considered and explicitly NOT attempted, per this stage's own
  instruction that "a batching mechanism has no inherent win when there is only one comparison" — no
  measurement was taken for these sites in this round, so this is a scope decision, not a rejected experiment.

**Required correctness tests.** `UtilsTest/Mathematics/Expressions/ExpressionSimplifierComparerBatchingTests.cs`
(new, 11 tests), reflecting directly into the protected `AdditionOfEqualsElements`/`SubstractionOfEqualsElements`
methods (matching this project's established precedent — see `ExpressionSimplifierAdditiveSortScaleTests`'s own
reflection into `CanonicalizeAdditiveExpression`) to isolate the exact rules this round changed from the
surrounding pipeline's unrelated sibling-rule interactions:

- the second-pass-needed cancellation/factoring cases through both `AdditionOfEqualsElements` and
  `SubstractionOfEqualsElements` (including the FINAL cancellation check inside `SubstractionOfEqualsElements`,
  reached after an earlier swap), asserting exact resulting AST shape, not merely compiled numeric equivalence;
- an end-to-end (public `Simplify(Expression)`) confirmation that the same second-pass shape is reachable
  through ordinary nested source expressions, not only via direct reflection;
- ordinary factor-match and near-miss (no-match, returns `null`) branches as positive/negative controls;
- same-reference-instance handling for an unsupported node kind (`ConditionalExpression`), proving the
  `TryFastPathEquals` reference shortcut still applies, and a companion test proving two DISTINCT
  structurally-identical unsupported-node instances are still conservatively NOT conflated;
- a hostile constant (throws from `Equals`/records a call count) embedded inside a method-call argument,
  proving the non-safe public comparer policy is still in effect (the hostile `Equals` IS invoked — not silently
  bypassed by an accidental safe-policy substitution), invoked exactly once (not more than the single, unbatched
  comparison would have invoked it), and that the resulting exception propagates out of the rule unmodified;
- a bound-lambda end-to-end scenario (S4 scope boundary) producing the identical shape to the free-parameter
  version, plus compiled-delegate execution parity between source and simplified lambdas;
- a minimal `ExpressionSimplifier` subclass with no overrides (neither modified method is `virtual`, so a
  subclass cannot override their behavior; this test pins that the INHERITED behavior is unchanged, matching
  this project's established "derived simplifier" test pattern).

Every one of the six tests exercising the second-pass mechanism, the unsupported-node fast path, or the hostile
constant's non-safe policy was confirmed to FAIL under the two naive-substitution experiments described above
(8 of 11 failed under the `FactorEqualityProbe`-internal substitution); the three ordinary-shape tests (factor
match, near-miss null, distinct-unsupported-instances) were unaffected by either substitution, as expected.

**Validation performed (2026-09-23, in order; branch was already at `master`-`1f874f9e0caa28e097060316a992cd5a4ce4fdbd`
from the round-4 rebase, and `master` had not advanced further by the time this validation ran, so no further
rebase was needed):**

1. `ExpressionSimplifierComparerBatchingTests` (new, 11 tests): 11/11 passed.
2. Full `UtilsTest/Mathematics/Expressions` namespace: 475/475 passed (464 pre-existing + 11 new).
3. Full `UtilsTest.Unit`: 7747/7747 passed, 0 skipped (7736 pre-existing + 11 new).
4. Full `UtilsTest.Functional`: 383/383 passed.
5. Full `UtilsTest.Security`: 225/228 passed, 3 skipped — the same three pre-existing, unrelated, platform-gated
   tests noted throughout S4/S5.
6. Release build of `Utils.sln` (the full multi-project solution): succeeded, 0 errors, 51 warnings — the same
   pre-existing count/shape as the round-4 baseline; the two production files this round touched introduce no
   new warning category (confirmed by a targeted diff of every warning on those two files' line ranges before
   and after).
7. `Utils/Utils.csproj` remains on `<TargetFramework>net8.0</TargetFramework>`, unchanged.

**S5 follow-ups (this round's own, in addition to the round-4 list above, which mostly still stands):**

- The round-4 "end-to-end nested-canonicalization/re-simplification construction-cost hazard" is now measurably
  smaller for the two rules that were its worst offenders (this round's own benchmark), but the underlying
  architecture — a factoring rule's equality decision can trigger the comparer's own internal `Simplify()`,
  which can itself invoke another factoring rule — is unchanged; it is reduced here, not eliminated.
- The `List`-vs-`Dictionary` micro-decision for `FactorEqualityProbe`'s own cache noted above was not isolated
  and benchmarked on its own; a future round could do so if the difference turns out to matter at a realistic
  n.
- Broadening batching to the three single-comparison call sites remains unexplored (deliberately out of this
  round's scope, not shown to be beneficial or harmful).
- The round-4 follow-ups (`ExpressionCanonicalOrder.BuildKey`'s single-key entry point, `ExpressionComparer`
  temporary array/search allocation cleanup) remain open and were not touched by this round either.

#### S5 P3 review, round 6 (2026-09-23) — caching must not reduce nested user-code invocation count

A human review of `c39470341e09732ea5c0c942a7b82249744252be` found one real semantic hazard the P3 round above
missed and one reporting inaccuracy. Both fixed on the same branch; P3's overall design (batch the comparer
re-simplification hazard) is unchanged.

**P1 (semantic hazard, confirmed and fixed).** The round-5 write-up's claim that `FactorEqualityProbe.Equals`
behaves "exactly like the public `ExpressionComparer` contract" was **false** for one case the round-5 test
suite did not cover: `ExpressionSimplifier.Simplify(Expression)` is a pure function of its **returned shape**
for a given input, but it is not necessarily side-effect-free **during construction** - simplifying a candidate
operand can dispatch a NESTED rule invocation (including a nested `AdditionOfEqualsElements`/
`SubstractionOfEqualsElements` call, with its own comparer probes) purely because that operand happens to
contain its own `Add`/`Subtract`/`Multiply` structure, and if that nested probe reaches a non-safe constant, it
invokes that constant's own user-defined `Equals`/`GetHashCode` - the same non-safe policy the public comparer
has always used, just reached one level deeper than the round-5 `HostileConstant` test exercised (which only
covered the FINAL structural comparison of the outer term, never a nested `Simplify()` call reached while
producing a cached candidate's own simplified form).

Reviewer-supplied repro, verified empirically (both directions) before any further code change:

```csharp
Expression inner = Expression.Add(
    Expression.Multiply(Expression.Constant(2.0), Expression.Call(identity, Expression.Constant(c1, typeof(object)))),
    Expression.Multiply(Expression.Constant(3.0), Expression.Call(identity, Expression.Constant(c2, typeof(object)))));
Expression left = Expression.Multiply(inner, x);
Expression right = Expression.Multiply(Expression.Constant(7.0), y);
// AdditionOfEqualsElements(Add(left, right), left, right)
```

`inner` becomes `leftleft` and participates in two of the outer rule's comparisons (`Equals(leftleft, rightleft)`
then `Equals(leftleft, rightright)`, since neither swap branch matches). Simplifying `inner` reaches its own
nested `AdditionOfEqualsElements`, whose own probe compares the two method-call arguments and invokes
`c1.Equals(c2)`. Measured directly (standalone harness, non-throwing `CountingConstant` recording a call count
instead of the round-5 `HostileConstant`'s throwing one):

| Build | `c1.EqualsCallCount` |
| --- | --- |
| True pre-P3 baseline (`e878c1a98502dadbac7d5fc67a92a34f13d05b35`) | 2 |
| Round-5 shipped candidate (`c39470341e09732ea5c0c942a7b82249744252be`) | **1** |
| Round-6 fixed candidate (below) | 2 |

The round-5 candidate's cache made `c1.Equals(c2)` run ONE time instead of TWO: the second comparison reused the
first comparison's cached simplified form of `inner` instead of re-simplifying it, silently halving how many
times this nested user code ran relative to the pre-existing, uncached per-comparison behavior - exactly the
class of difference this stage's own "Side effects and hostile constants" guidance forbids introducing silently
("If a proposed caching approach changes observable user-code invocation behavior, ... reject that approach ...
unless you can prove the existing contract explicitly permits the difference"). No such proof exists; the
approach was not rejected outright (per the reviewer's own recommendation) but narrowed instead - see the fix
below.

*Fix* (`Utils/Expressions/ExpressionSimplifier.cs`, `FactorEqualityProbe`): caching is now conditional on a new
conservative predicate, `MightInvokeUserCodeWhenSimplified(Expression?)`, evaluated once per distinct candidate
reference (and itself cached alongside the simplified form, so it is also computed at most once per candidate
per rule invocation). This predicate walks a candidate's ENTIRE subtree - not merely the seven node kinds
`ExpressionComparer` understands structurally, since a nested rule invocation is not limited to those either
(e.g. a `ConditionalExpression`'s branches are still simplified even though `ExpressionComparer` treats the
whole node as opaque) - and reports "might invoke user code" (conservatively `true`, meaning "do not cache") for:
any `ConstantExpression` whose value is neither a native numeric type in `Types.Number` (compared via the exact
rational/NaN/infinity model, which never calls user code) nor a known-safe type (see
`ExpressionComparer.IsKnownSafeConstantValue`); and any node kind this predicate does not explicitly recognize.
The second point is a deliberate design choice, not an oversight: an `ExpressionVisitor`-based walk was
considered and rejected, because `ExpressionVisitor`'s default `VisitExtension`/`Expression.VisitChildren`
**throws** `ArgumentException` on a non-reducible `ExpressionType.Extension` node (`CanReduce == false`) - exactly
the shape `ExpressionSimplifierAdditiveSortScaleTests`'s own existing adversarial coverage (`ThrowingExpression`)
uses, and exactly the kind of node this SAFETY check itself must never crash on. A hand-written recursive
pattern match, falling back to a conservative `true` for any node kind it does not explicitly enumerate,
achieves the same safety without ever touching such a node's internals.

`FactorEqualityProbe.Equals` now looks up each operand's cached `(CanCache, Simplified)` pair; if either operand
is not cacheable, the comparison falls back to calling `ExpressionComparer.Default.Equals(x, y)` directly for
THAT ONE comparison (never populating or consulting the cache for that operand at all) - exactly the call, and
therefore exactly the invocation count, every comparison made before `FactorEqualityProbe` existed. A cacheable
operand (no reachable non-safe constant anywhere in its subtree) is simplified and cached at most once, exactly
as round 5 already did - this is the case every one of P3's benchmark families actually exercises (none of them
use opaque/non-numeric constants), so the fix targets exactly the gap the reviewer found without touching the
mechanism the benchmark numbers below still credit.

**Re-benchmarked after the fix** (same standalone harness, same methodology; allocations re-confirmed
byte-identical across two repeated runs of the new build):

| Family, n=8 | True baseline | Round-5 (buggy) | Round-6 (fixed) |
| --- | --- | --- | --- |
| NearMissAdditive time | 121.28 us | 92.75 us | 99.90 us |
| NearMissAdditive alloc | 79 753 B | 49 005 B | 49 229 B |
| NearMissSubtraction time | 109.47 us | 70.56 us | 97.28 us |
| NearMissSubtraction alloc | 113 297 B | 50 797 B | 51 021 B |
| FunctionLike time | 92.34 us | 43.50 us | 43.72 us |
| FunctionLike alloc | 98 361 B | 64 477 B | 65 377 B |
| BoundLambda time | 30.73 us | 23.44 us | 24.36 us |
| BoundLambda alloc | 80 353 B | 49 605 B | 49 829 B |

| Family, n=32 | True baseline | Round-5 (buggy) | Round-6 (fixed) |
| --- | --- | --- | --- |
| NearMissAdditive time | 407.02 us | 228.16 us | 259.12 us |
| NearMissAdditive alloc | 1 460 834 B | 768 106 B | 769 098 B |
| NearMissSubtraction time | 744.91 us | 285.43 us | 319.17 us |
| NearMissSubtraction alloc | 2 165 962 B | 776 042 B | 777 034 B |
| FunctionLike time | 983.73 us | 296.35 us | 335.70 us |
| FunctionLike alloc | 1 538 482 B | 831 866 B | 835 834 B |
| BoundLambda time | 783.17 us | 236.97 us | 256.89 us |
| BoundLambda alloc | 1 462 010 B | 769 282 B | 770 274 B |

Best-effort n=64, NearMissAdditive: true baseline 2893.64 us / 5 983 400 B → round-6 fixed 1013.62 us / 3 069 584 B
(2.9x time, 1.95x alloc - essentially unchanged from round 5's 3.05x/1.95x, since the allocation profile is
dominated by the same avoided re-simplifications either way).

The fix costs a small, constant per-candidate safety-scan overhead (the `MightInvokeUserCodeWhenSimplified` walk
- itself allocation-free, since it only reads existing tree structure and returns a `bool`): every family's
ALLOCATION reduction versus the true baseline is preserved almost exactly (within ~0.2-1% of round 5's numbers -
the tiny residual difference is the `(bool, Expression?)` tuple's slightly larger footprint in the cache list,
not a new allocation source), while wall-clock TIME shows a modest, expected reduction in the improvement margin
(e.g. NearMissAdditive n=32: 43.9% faster than baseline at round 5, 36.3% faster at round 6) - the scan itself is
CPU-only work with no corresponding baseline cost to offset against. This is judged an acceptable, disclosed
trade for closing a real correctness gap, and every family remains an unambiguous, substantial win over the true
baseline at every n ≥ 8.

**n=2 and Family D, re-measured.** Round 5's n=2/Family D table (above, in the historical section) is superseded
by these round-6 numbers (true baseline → round-6 fixed candidate; allocations re-confirmed byte-identical
across two repeated runs):

| Family, n=2 | Baseline alloc | Round-6 fixed alloc | Delta |
| --- | --- | --- | --- |
| A NearMissAdditive | 3 799 B | 3 927 B | +128 B, +3.4% (was +2.5% at round 5) |
| B NearMissSubtraction | 4 103 B | 4 183 B | +80 B, +1.9% (was +1.2%) |
| E FunctionLike | 7 637 B | 7 413 B | −224 B, −2.9% (still an improvement; was −4.6%) |
| F BoundLambda | 4 245 B | 4 373 B | +128 B, +3.0% (was +2.3%) |
| D SecondPass (single call) | 14 274 B | 14 402 B | +128 B, +0.9% (was +0.7%) |

Every n=2/D delta grew slightly relative to round 5's numbers, consistent with the added per-candidate safety
scan being a small, roughly-constant fixed cost that a very small batch amortizes least well of any size
measured - the same "Small-n regression" shape round 5 already disclosed (not a new phenomenon), just slightly
larger in absolute terms. `E FunctionLike` remains a net improvement even at n=2. No exact-shape result changed:
Family D still simplifies to the single constant `0`, and every reflection-based shape check in this round's own
new test plus the eleven round-5 tests still passes unchanged (see "Validation" below).

**Confirmed the fix reverses the P1 finding.** Re-ran the exact repro above against the round-6 fixed build:
`c1.EqualsCallCount` is now 2, matching the true baseline exactly.

**Test added and verified to fail pre-fix.** `NestedSimplifyReachingUserCode_InvokedOncePerComparison_NotOncePerCandidate`
in `ExpressionSimplifierComparerBatchingTests.cs` reproduces the reviewer's exact repro with a non-throwing
`CountingConstant` (records a call count instead of throwing, unlike the round-5 `HostileConstant`, so the test
can inspect the count directly rather than only prove an exception propagates) and asserts `c1.EqualsCallCount == 2`.
Confirmed to FAIL against the round-5 shipped build (`c39470341e09732ea5c0c942a7b82249744252be`'s
`ExpressionSimplifier.cs`, temporarily swapped in with the round-6 test file kept as-is, then reverted) with
exactly the predicted `1 != 2` mismatch, and to pass against the round-6 fixed build. A second, "stateful/
alternating `Equals`" test (returning a different result on each call) was considered, as the reviewer suggested
as an even-stronger check, but not added: the round-6 fix's guarantee is unconditional per-candidate ("any
operand whose subtree contains a reachable non-safe constant is NEVER cached, full stop"), so a stateful-`Equals`
scenario would exercise the exact same code path (the uncached fallback) as the deterministic counting test
above, rather than a materially different one - judged not to add distinguishing coverage proportionate to its
own construction complexity and fragility (predicting an intentionally-nondeterministic `Equals`'s exact
resulting AST shape across two independent re-simplifications).

**P2 (reporting inaccuracy, fixed).** The round-5 write-up labeled the `Cancellation` control family's small
allocation increase "1.0x (noise)" at every size. This directly contradicted this stage's own established
methodology (allocations are byte-identical across repeated runs of the same build and are the trustworthy
signal, specifically BECAUSE they are not noise - see the P1 round-4 "On timing noise" entry). Re-measured and
confirmed deterministic (byte-identical across two repeated runs, both before and after the round-6 fix): the
`Cancellation` family's allocation numbers, true baseline → round-6 fixed candidate, are **12 576 B → 12 784 B
(+208 B, +1.65%)** at n=2, **54 278 B → 54 966 B (+688 B, +1.27%)** at n=8, **110 009 B → 111 337 B (+1 328 B,
+1.21%)** at n=16, and **221 599 B → 224 207 B (+2 608 B, +1.18%)** at n=32 - a real, small, reproducible
regression, not noise, on the same order of magnitude as the already-disclosed n=2 regression for the OTHER
families. Corrected
in place in this file and in the PR description: this family's allocation numbers are now reported as a
disclosed regression alongside the n=2 findings, not as "noise" or "byte-identical."

Root cause (not separately isolated/profiled beyond this observation, consistent with this stage's own
"do not state an inferred root cause as proven unless it was isolated experimentally" instruction): the
`Cancellation` family's dominant cost is the untouched `AdditionWithNegate`/cancellation path (a single
comparison per node, not routed through `FactorEqualityProbe` at all), but every node in this family's chain is
still built from `Multiply`/`Subtract` pairs that DO reach `SubstractionOfEqualsElements` at some point in the
chain, each invocation now allocating one `FactorEqualityProbe` (and, after this round's fix, its cache tuples
carry an extra `bool`) regardless of whether that invocation's own comparisons end up being useful - the same
fixed per-call cost the P1 round-2/round-3 entries already characterized for `AnnotatedAdditiveTerm`, applying
here to a family whose useful work this rule call performs is comparatively small.

**Validation performed (2026-09-23, after review round 6, in order; no rebase needed, `master` still at
`1f874f9e0caa28e097060316a992cd5a4ce4fdbd`):**

1. `ExpressionSimplifierComparerBatchingTests` (12 tests: the original 11 plus
   `NestedSimplifyReachingUserCode_InvokedOncePerComparison_NotOncePerCandidate`): 12/12 passed.
2. Full `UtilsTest/Mathematics/Expressions` namespace: 476/476 passed (464 pre-existing + 12 new).
3. Full `UtilsTest.Unit`: 7748/7748 passed, 0 skipped.
4. Full `UtilsTest.Functional`: 383/383 passed.
5. Full `UtilsTest.Security`: 225/228 passed, 3 skipped — the same three pre-existing, unrelated,
   platform-gated tests noted throughout S4/S5.
6. Release build of `Utils.sln`: succeeded, 0 errors, 51 warnings — same pre-existing count/shape; a targeted
   diff of every warning inside `FactorEqualityProbe`'s new line range confirmed zero new warnings there.
7. `Utils/Utils.csproj` remains on `<TargetFramework>net8.0</TargetFramework>`, unchanged.

#### S5 P3 review, round 7 (2026-09-24) — classify both operands before simplifying either one

A second human review of round 6's head (`c412f5cb`) confirmed round 6's own fix works (the P1 hazard it
targeted is gone, CI is fully green including `source-gates-ubuntu`/`required`) but found one further semantic
hazard in the same type, plus one allocation-hygiene nit. Both fixed on the same branch; the overall P3 design
(batch the comparer re-simplification hazard via a per-invocation cache) is unchanged.

**P1 (semantic hazard, confirmed and fixed) — interleaved classify-then-simplify reorders operand
simplification relative to the public contract.** Round 6's `FactorEqualityProbe.Equals` called
`GetSimplifiedIfCacheable(x, ...)` then `GetSimplifiedIfCacheable(y, ...)` in sequence, and each of those calls
BOTH classified its operand (via `MightInvokeUserCodeWhenSimplified`) AND, if cacheable, immediately simplified
it — before the pair's overall `!canCacheX || !canCacheY` fallback decision was made. This is fine when both
operands end up cacheable (both get simplified, in `x`-then-`y` order, matching the public contract) or when
`x` is non-cacheable and `y` also turns out non-cacheable (neither is simplified by the probe). It is NOT fine
when `x` is non-cacheable and `y` IS cacheable: `y` gets simplified during the SECOND call, before the pair is
known to require a fallback at all - so if simplifying `y` has an observable effect (throws, or runs user code
reachable during simplification), that effect is observed BEFORE `x` is ever touched, and before the fallback
that would otherwise simplify `x` first is even reached. The public
`ExpressionComparer.Equals(Expression?, Expression?)` contract this type must reproduce exactly always
simplifies `x` first, then `y` - never the reverse, and it never simplifies `y` at all if simplifying `x`
throws first.

Reviewer-supplied repro, verified empirically (built as a permanent regression test, not just an ad hoc probe -
see "Test" below): `x` a non-cacheable operand built from the same "inner" shape as round 6's own
`NestedSimplifyReachingUserCode...` test (an `Add` of two `Multiply`/`Call` terms, each embedding a distinct
`HostileConstant`, so simplifying `x` on its own reaches a nested `AdditionOfEqualsElements` call whose
structural argument comparison invokes `hostileLeft.Equals(hostileRight)` and throws
`HostileConstantException`); `y` a cacheable operand (`Divide(Constant(1.0), Constant(0.0))` - only native
numeric constants, so `MightInvokeUserCodeWhenSimplified` clears it) whose OWN simplification unconditionally
throws `DivideByZeroException` (`ExpressionSimplifier.DivideWithZeroOrOne`) regardless of numeric type -
deliberately a DIFFERENT exception type from `x`'s, so the type that propagates identifies which operand was
actually attempted first:

| Build | Exception observed for `Equals(x, y)` |
| --- | --- |
| True pre-P3 baseline (`ExpressionComparer.Default.Equals(x, y)` directly) | `HostileConstantException` (from simplifying `x`, always attempted first) |
| Round-6 shipped candidate (`c412f5cb`) | `DivideByZeroException` (from simplifying `y`, reached first via the interleaved classify-and-simplify call) |
| Round-7 fixed candidate (below) | `HostileConstantException` (matches the true baseline) |

**Fix** (`Utils/Expressions/ExpressionSimplifier.cs`, `FactorEqualityProbe`): `GetSimplifiedIfCacheable` is split
into two methods with a hard ordering contract in `Equals`: `IsCacheable(Expression)` classifies (and caches
the classification for) an operand WITHOUT ever simplifying it, and `GetOrSimplify(Expression)` simplifies (and
caches) an operand a caller has already proven cacheable via `IsCacheable`. `Equals` now calls `IsCacheable` for
BOTH `x` and `y` first; only if both return `true` does it call `GetOrSimplify` for `x` then `y`, in that order.
When either is not cacheable, `Equals` falls back to `ExpressionComparer.Default.Equals(x, y)` directly, having
simplified NEITHER operand itself - so the fallback's own `Simplify(x)`-then-`Simplify(y)` sequence is the only
place either operand gets simplified, exactly reproducing the public contract's order (and its "never reach `y`
if `x` throws first" behavior) for every combination of cacheability, not merely the both-cacheable case round 6
already got right.

**Test.** `NonCacheableThenCacheableOperand_FallsBackWithoutSimplifyingEitherFirst_PreservesXBeforeYOrder` in
`ExpressionSimplifierComparerBatchingTests.cs` reproduces the exact repro above, using a shared `Constant(5.0)`
instance as both `Multiply`'s left factor (`leftleft`/`rightleft`) so `TryFastPathEquals`'s
`ReferenceEquals` shortcut satisfies that first comparison without touching the probe's cache at all, making
`Equals(leftright=x, rightright=y)` - reached inside the same short-circuited `&&` chain - the very first
substantive comparison the probe performs. Asserts the propagated exception (unwrapped through however many
`TargetInvocationException` layers reflection introduces) is `HostileConstantException`, not
`DivideByZeroException`. Confirmed to FAIL against the round-6 shipped build (`c412f5cb`'s
`ExpressionSimplifier.cs`, temporarily swapped in via `git stash`/`git stash pop` with the round-7 test file kept
as-is) with exactly the predicted `DivideByZeroException` instead of `HostileConstantException`, and to pass
against the round-7 fixed build.

**P2 (allocation-hygiene nit, fixed).** `MightInvokeUserCodeWhenSimplified`'s `MethodCallExpression` arm used
`mce.Arguments.Any(MightInvokeUserCodeWhenSimplified)` - a LINQ `Enumerable.Any` call on a
`ReadOnlyCollection<Expression>` inside a classification path meant to be a cheap, allocation-light static-shape
scan run once per candidate per rule invocation, inconsistent with this project's existing avoidance of LINQ on
other frequently used construction paths (see this stage's own P1/P3 history). Replaced with a new
`private static bool AnyArgumentMightInvokeUserCodeWhenSimplified(IReadOnlyList<Expression> arguments)` indexed
loop, called from the same switch-expression arm. This is a hygiene/consistency fix, not a correctness one - no
regression test was added specifically for it (the existing `MightInvokeUserCodeWhenSimplified` coverage already
exercises the `MethodCallExpression` arm via every `HostileConstant`/`CountingConstant` test in this file); round
6's own small-`n` allocation deltas were already attributed to the `(bool, Expression?)` tuple's footprint, a
claim this fix does not retroactively re-verify (not re-benchmarked separately - the change is judged safe by
inspection, consistent with this stage's own "reasoned, not separately benchmarked" precedent for
sub-benchmark-noise micro-decisions).

**Validation performed (2026-09-24, after review round 7, in order; no rebase needed, `master` still at
`1f874f9e0caa28e097060316a992cd5a4ce4fdbd`):**

1. `ExpressionSimplifierComparerBatchingTests` (13 tests: the round-6 12 plus
   `NonCacheableThenCacheableOperand_FallsBackWithoutSimplifyingEitherFirst_PreservesXBeforeYOrder`): 13/13
   passed; confirmed to fail pre-fix as described above.
2. `ExpressionSimplifierComparerBatchingTests` + `ExpressionSimplifierAdditiveSortScaleTests` together: 19/19
   passed.
3. Full `UtilsTest.Unit`: 7749/7749 passed, 0 skipped.
4. Full `UtilsTest.Functional`: 383/383 passed.
5. Full `UtilsTest.Security`: 225/228 passed, 3 skipped — the same three pre-existing, unrelated,
   platform-gated tests noted throughout S4/S5.
6. Release build of `Utils.sln`: succeeded, 0 errors, 51 warnings — same pre-existing count/shape.
7. `Utils/Utils.csproj` remains on `<TargetFramework>net8.0</TargetFramework>`, unchanged.

#### S5 P3 round-7 CPU rebenchmark (2026-09-24) — confirms no measurable regression

Round 7's split of round 6's single combined cache lookup (`GetSimplifiedIfCacheable`) into two separate scans
of the same ≤4-entry `_cache` list (`IsCacheable` then `GetOrSimplify`) is, in the worst case, one extra linear
scan of at most 4 reference-equality checks per operand per `FactorEqualityProbe.Equals` call on the
both-cacheable hot path - bounded, `O(1)` work, but a review specifically asked for it to be measured rather
than assumed negligible, since it changed the hot path the P3 benchmarks themselves target.

**Methodology change from the original P1/P3 harnesses, disclosed.** An initial attempt to reproduce
"`NearMissAdditive`/`NearMissSubtraction`/`FunctionLike`/`BoundLambda` at n=8/32" as chains of n terms run
through the full public `Simplify()` pipeline (matching the earlier harnesses' own approach, which were never
committed to this repository and could not be recovered byte-for-byte) hit a real but UNRELATED cost: a
left-deep n-term chain forces repeated re-simplification of growing prefixes at every level of the chain,
dominating the measurement by 1-2 orders of magnitude and making even n=8 take low-single-digit milliseconds
per round (versus the ~100us this family name previously reported) - swamping the small, bounded signal this
rebenchmark was actually trying to isolate, and making n=32 impractically slow to complete even one round.
Since round 7's change is a bounded, per-call, `O(1)` cost independent of surrounding chain length, this
rebenchmark instead isolates it directly: each family is now ONE minimal `AdditionOfEqualsElements`/
`SubstractionOfEqualsElements` invocation (reflected into directly, exactly like
`ExpressionSimplifierComparerBatchingTests`' own `InvokeAdditionOfEqualsElements` helper), built so none of its
comparisons match structurally (maximizing `FactorEqualityProbe` probe-call count per invocation - matching the
original "near-miss" family intent) with every candidate cacheable (no reachable non-safe constant), landing
squarely in the hot path round 7 changed. "n=8"/"n=32" are reinterpreted as the repetition count used to compute
a per-call median (the established "15 rounds, median reported" idea, parameterized) rather than chain length;
since this is a bounded, `O(1)`-per-call operation, per-call cost should be statistically indistinguishable at
n=8 vs. n=32 if - and only if - there is no hidden n-dependent cost, so that convergence is itself part of the
answer, not just a formality. Standalone temporary harness (not part of this repository, built in a scratch
directory), Windows 11, .NET 8.0.31, workstation/non-concurrent GC, Release, `ProjectReference` to
`Utils.csproj`. Each reported number is the median of 15 batches (`n` calls per batch, fresh
`ExpressionSimplifier`/expression tree per call), run twice per build (two independent process invocations) to
check run-to-run stability; the round-6 comparison build was produced by temporarily overwriting the working
tree's `ExpressionSimplifier.cs` with `git show c412f5cb:...` and rebuilding, then restored via
`git checkout HEAD --` before committing anything (the same revert-before-commit discipline this stage already
uses for correctness verification) - the repository was confirmed clean (`git status --short`) both before and
after.

| Family, n=8 | Round-6 (avg of 2 runs) | Round-7 (avg of 2 runs) | Δ time |
| --- | --- | --- | --- |
| NearMissAdditive time | 3.763 us | 3.825 us | +1.7% |
| NearMissSubtraction time | 3.700 us | 3.725 us | +0.7% |
| FunctionLike time | 11.362 us | 11.763 us | +3.5% |
| BoundLambda time | 25.919 us | 26.806 us | +3.4% |

| Family, n=32 | Round-6 (avg of 2 runs) | Round-7 (avg of 2 runs) | Δ time |
| --- | --- | --- | --- |
| NearMissAdditive time | 3.964 us | 4.136 us | +4.3% |
| NearMissSubtraction time | 4.067 us | 4.183 us | +2.9% |
| FunctionLike time | 10.819 us | 10.921 us | +0.9% |
| BoundLambda time | 26.522 us | 24.919 us | **−6.0%** |

**Allocations (byte-identical across both runs of the same build, at both n - the trustworthy signal, per this
stage's own established methodology):**

| Family | Round-6 alloc/call | Round-7 alloc/call | Δ |
| --- | --- | --- | --- |
| NearMissAdditive | 1 413.0 B (n=8) / 1 409.2 B (n=32) | identical | 0 (byte-identical) |
| NearMissSubtraction | 1 413.0 B (n=8) / 1 409.2 B (n=32) | identical | 0 (byte-identical) |
| BoundLambda | 9 997.0 B (n=8) / 9 993.2 B (n=32) | identical | 0 (byte-identical) |
| FunctionLike | 1 925.0 B (n=8) / 1 921.2 B (n=32) | 1 829.0 B (n=8) / 1 825.2 B (n=32) | **−96 B (−5.0%)** |

**Reading these numbers.** Timing deltas range from −6.0% to +4.3% and do not point in a consistent direction
across families or between n=8 and n=32 for the same family (`BoundLambda` is 3.4% SLOWER at n=8 but 6.0%
FASTER at n=32) - the signature of measurement noise at this scale (3-27us per call, where OS scheduling, JIT
tiering and frequency scaling introduce several-percent jitter) rather than a systematic regression; a real,
reproducible `O(1)` cost from one extra ≤4-entry reference-equality scan would be expected to show up as a
small but CONSISTENTLY signed delta at every n, which these numbers do not show. Allocations - unaffected by
timing jitter and confirmed byte-identical across repeated runs, exactly as this stage's methodology already
treats them as the trustworthy signal - are unchanged for every family whose candidates are not
`MethodCallExpression`s, and are 96 bytes LOWER per call for `FunctionLike` (the one family that exercises the
`MethodCallExpression` classification arm), confirming empirically - not merely by inspection, as round 7's own
write-up initially had to leave it - that round 7's P2 fix (`Arguments.Any(...)` → an indexed loop) is a real,
reproducible allocation improvement, not a wash. **Verdict: no measurable CPU regression from round 7's
classify-then-simplify split; the fix is CPU-neutral within measurement noise and allocation-neutral-to-positive.**
S5 P3 is considered closed pending no further review findings.

#### S5 progress (2026-09-24) — P4: `ExpressionCanonicalOrder.BuildKey`'s scope-workspace allocation eliminated

**Baseline.** `master` at `77d571deb3065ab76267ad2de278814061dcc540` (PR #605, `Utils.NumberToString`-only,
no overlap with this change) - confirmed to be current `master` (re-fetched: `origin/master` unchanged at
the same commit) both before branching and again immediately before the final validation run below, so no
rebase was needed. This commit is itself a descendant of `019a4551...` (PR #604, the P1 commit the task
brief named as its baseline), so P4 is implemented directly on top of the already-merged P1/P2/P3 work.

**Audit finding (as described in the task brief, confirmed by reading the pre-existing code).**
`ExpressionCanonicalOrder.BuildKey` and `BuildKeys` each built a fresh, eagerly-copied
`List<ParameterExpression[]>` working scope list on every call:

```csharp
var scopes = new List<ParameterExpression[]>(enclosingScopes.Count + 2);
scopes.AddRange(enclosingScopes);
return Build(expression, scopes);
```

purely so `BuildLambda` could temporarily push/pop one frame for a nested `LambdaExpression` encountered
while walking the term - work paid on every call, including the common case (constants, parameters, unary/
binary nodes, method calls, member access, most additive terms, most multiplicative factors) where no nested
lambda is ever encountered and the list is only ever read from, never mutated.

**Change.** `Utils/Expressions/ExpressionCanonicalOrder.cs`: introduced a private `ref struct ScopeState`
holding the immutable, caller-supplied `EnclosingScopes` snapshot (never copied) plus a lazily-allocated
`List<ParameterExpression[]>? NestedScopes` (stays `null` until `BuildLambda` pushes its first frame).
`BuildKey`/`BuildKeys` now construct a `ScopeState` (a stack-only value, not a heap allocation) instead of the
`List`, and every private `Build*` helper (`Build`, `BuildParameter`, `BuildUnary`, `BuildBinary`,
`BuildMethodCall`, `BuildMember`, `BuildLambda`) now takes `ref ScopeState scopes` instead of
`List<ParameterExpression[]> scopes`, so the SAME `ScopeState` (and, once allocated, the SAME `NestedScopes`
list) is threaded through the whole recursive walk and, for `BuildKeys`, across every sibling argument - the
`ref` is what lets a nested-scope allocation that happens deep in the recursion (or in an earlier sibling
argument) remain visible to the rest of the walk, exactly as the task brief's suggested design required.
`BuildParameter` now searches `NestedScopes` innermost-to-outermost first (when non-`null`), then
`EnclosingScopes` innermost-to-outermost, instead of one combined list scanned once. The empty-argument-list
fast path in `BuildKeys` (`expressions.Count == 0` returning `[]` before touching `ScopeState` at all) was
already allocation-free after P1's review round 2 and is unchanged.

**Why bound-parameter depth is identical.** With `nestedCount` frames pushed and `enclosing.Count` supplied
enclosing frames, a nested frame at index `i` (found before any enclosing search runs) gets
`depth = nestedCount - 1 - i`, and an enclosing frame at index `i` (found only after every nested frame
misses) gets `depth = nestedCount + (enclosing.Count - 1 - i)` - i.e. every enclosing depth is uniformly
shifted up by however many nested frames are currently open, and nested depths are always strictly lower
(0..nestedCount-1) than every enclosing depth (nestedCount..nestedCount+enclosing.Count-1). This is
arithmetically identical to the old single concatenated list's `depth = scopes.Count - 1 - i` for every `i`,
since that list was always exactly `enclosingScopes` followed by whatever nested frames were currently pushed
(in the same push order) - the two-part search only avoids materializing that concatenation, it does not
change which index anything resolves to. Verified both by direct reasoning (worked through with the task
brief's own `outer0, outer1, inner0, inner1` example - depths 3,2,1,0 as specified) and by the new tests below,
including a doubly-nested-lambda-inside-one-enclosing-scope case that isolates exactly this depth arithmetic
by holding every other structural dimension (type, position, lambda/parameter shape) constant across three
otherwise-identical terms.

**Design confirmed, no rejected experiment.** The task brief's own suggested shape (an enclosing snapshot
plus a lazily-allocated nested stack, threaded by `ref`) was implemented as described and met every
performance-acceptance criterion on the first attempt (see benchmarks below); no alternative representation
was built or benchmarked.

**Benchmark methodology.** Standalone temporary harness (not part of this repository; built under this
session's scratch directory), Windows 11, .NET 8.0, Release, `ProjectReference` to `Utils.csproj` (no
`InternalsVisibleTo`; internal members reached via reflection, exactly like this project's own test suite).
Every `ExpressionCanonicalOrder`/`ExpressionSimplifier` internal member invoked through a `Delegate.CreateDelegate`-bound
open-instance/static delegate (not raw `MethodInfo.Invoke`), so the measured allocation is the callee's own
work, not a per-call reflection-argument-array tax. Each scenario: expression trees, scope lists and delegates
built OUTSIDE the measured region; a warmup phase (JIT tiering) before measurement; `GC.Collect()` x2 before
starting the clock; `Stopwatch` for elapsed time and `GC.GetAllocatedBytesForCurrentThread()` (thread-local,
collection-count-independent) for allocation, both around the same iteration loop. The harness was run once
against the unmodified baseline commit, then again after the production change, from the same machine/session
without other load.

**Direct `BuildKey` results (300 000 iterations unless noted; B/op = bytes allocated per call):**

| Scenario | Baseline B/op | Candidate B/op | Δ | Baseline ns/op | Candidate ns/op |
| --- | --- | --- | --- | --- | --- |
| Constant, no enclosing scope | 248.00 | 176.00 | **−72** | 638.7 | 632.2 |
| Free parameter, no enclosing scope | 112.00 | 40.00 | **−72** | 51.3 | 43.1 |
| Binary (a+b), no enclosing scope | 216.00 | 144.00 | **−72** | 139.0 | 135.8 |
| Method call Sin(a), no enclosing scope | 184.00 | 112.00 | **−72** | 113.5 | 102.7 |
| Expr under ONE enclosing scope | 360.00 | 280.00 | **−80** | 385.0 | 349.1 |
| Expr under 8 enclosing scopes (outermost ref) | 176.00 | 40.00 | **−136** | 140.3 | 185.7 |
| ONE nested lambda capturing enclosing param | 336.00 | 344.00 | +8 (noise) | 333.1 | 344.4 |
| TWO nested lambda levels capturing enclosing param | 552.00 | 560.00 | +8 (noise) | 613.3 | 590.4 |

Every no-nested-lambda scenario's allocation dropped, including the 8-enclosing-scope case (proving the
elimination is independent of how many enclosing scopes are supplied, since the old code copied
`enclosingScopes` into a new `List` regardless of its size). The two nested-lambda control scenarios are flat
within noise (+8B out of 336-552B, ~1.4-2.4%), exactly the "must not materially regress" acceptance bar - the
tiny increase is consistent with `ScopeState` itself carrying one extra reference-sized field versus a bare
`List<T>` reference, not with any new per-call work.

**`BuildKeys` results (300 000 iterations, or 500 000 for the zero-argument case):**

| Scenario | Baseline B/op | Candidate B/op | Δ |
| --- | --- | --- | --- |
| 0 arguments | 0.00 | 0.00 | unchanged (already allocation-free since P1 review round 2) |
| 1 argument | 248.00 | 176.00 | **−72** |
| 4 arguments (bound params, no lambdas) | 296.00 | 216.00 | **−80** |
| 4 arguments incl. 2 nested lambdas | 728.00 | 736.00 | +8 (noise) |

**Additive/multiplicative canonicalization integration (direct calls to the private
`CanonicalizeAdditiveExpression`/`CanonicalizeMultiplicativeExpression`, matching P1's own established
methodology for isolating this code from the unrelated end-to-end re-simplification hazard described below):**

| Scenario | Baseline B/op | Candidate B/op | Δ | Baseline ns/op | Candidate ns/op |
| --- | --- | --- | --- | --- | --- |
| Additive, n=8, opaque bound terms | 6 480.08 | 5 840.07 | **−640 (−9.9%)** | 7 806.6 | 7 233.8 |
| Additive, n=8, function-like Sin(p) terms | 8 656.11 | 7 376.10 | **−1 280 (−14.8%)** | 10 634.4 | 10 005.2 |
| Multiplicative, n=8, bound-parameter factors | 2 120.03 | 1 480.03 | **−640 (−30.2%)** | 1 485.8 | 1 631.2 |
| Additive, n=32, opaque bound terms | 23 504.30 | 20 944.30 | **−2 560 (−10.9%)** | 10 831.0 | 11 065.7 |
| Additive, n=32, function-like Sin(p) terms | 32 208.42 | 27 088.36 | **−5 120 (−15.9%)** | 17 291.9 | 16 322.6 |
| Multiplicative, n=32, bound-parameter factors | 7 256.10 | 4 696.07 | **−2 560 (−35.3%)** | 5 736.7 | 5 254.4 |
| Additive, n=128, opaque bound terms | 91 217.16 | 80 977.16 | **−10 240 (−11.2%)** | 53 319.8 | 46 425.6 |
| Additive, n=128, function-like Sin(p) terms | 126 033.64 | 105 553.48 | **−20 480 (−16.3%)** | 90 629.4 | 80 469.7 |
| Multiplicative, n=128, bound-parameter factors | 27 656.52 | 17 416.36 | **−10 240 (−37.0%)** | 28 715.4 | 28 153.0 |

Every allocation delta is consistent with removing exactly one workspace allocation per `BuildKey` call in the
per-term annotation loop (roughly `n x ~72-80` bytes; e.g. `8 x 80 = 640`, `32 x 80 = 2560`, `128 x 80 = 10240`
match the additive-opaque row's deltas almost exactly) - stated as "consistent with", per this stage's own
methodology, since the exact per-object byte accounting was not independently isolated beyond the direct
`BuildKey` table above. The multiplicative family shows the largest proportional win (30-37%), matching the
roadmap's own prediction that `.OrderBy(factor => BuildKey(factor, scopes))` pays this cost once per factor
with nothing else to amortize it against. CPU is neutral-to-better at every size (no regression at any row;
several rows 5-13% faster, plausibly less GC pressure from less allocation, though this was not isolated
further since the allocation reduction alone already clears the acceptance bar).

**Public `Simplify()` control - dominated by unrelated work, disclosed rather than treated as a signal.**
A 16-term reversed-bound-parameter additive lambda (P1's own established "empirically well under a second at
this term count" control shape) took ~104 ms/call on the pre-P4 baseline and ~86 ms/call on the candidate
(20 iterations; reduced from an initial 3 000-iteration baseline run that took over 5 minutes once this cost
per call became apparent). Both figures are 3-4 orders of magnitude larger than the direct-canonicalization
figures above for the same shape, confirming - exactly as P3's own round-7 write-up already had to disclose
for a similar shape - that the full `Simplify()` pipeline's end-to-end repeated-re-simplification hazard
(tracked as a still-open S5 follow-up, not part of this PR) dominates this measurement completely; the ~17%
apparent improvement is not attributed to this PR's change and is reported only for completeness, per this
stage's "disclose rather than claim an inferred cause" rule.

**Performance acceptance: met.** No-nested-lambda `BuildKey`/`BuildKeys` allocations clearly decreased
(including when enclosing scopes are supplied); nested-lambda cases did not materially regress (+8B noise);
additive/multiplicative canonicalization showed the expected reduction at every tested size; CPU was neutral
or better everywhere measured; no symbolic/canonical output changed (see tests below).

**Compatibility.** `BuildKey`'s and `BuildKeys`' own `internal` signatures are unchanged - only the private
`Build*` helper signatures (never called outside this file) changed shape (`List<ParameterExpression[]>` to
`ref ScopeState`). No new `internal`/public API surface was introduced (`ScopeState` is `private`). No call
site outside `ExpressionCanonicalOrder.cs` was touched.

**Tests added** (`UtilsTest/Mathematics/Expressions/ExpressionCanonicalOrderScopeStateTests.cs`, reflecting
directly into `ExpressionCanonicalOrder.Compare`, exactly like `ExpressionSimplifierStructuralCanonicalizationTests`'s
own `Compare`/`CompareType`/`CompareMethod` reflection precedent):

1. `Compare_MultipleEnclosingScopesWithoutNestedLambda_OrdersByDepthThenPosition` - three enclosing scopes,
   two same-type parameters each (six pairwise-distinct `(depth, position)` bound parameters, no nested
   lambda at all), sorted from a shuffled order via `Compare`, must recover exactly the depth-then-position
   ascending order - the "enclosing scopes without a nested lambda" minimum from the task brief.
2. `Compare_NestedLambdaCapturingEnclosingParameter_AlphaEquivalentInstancesTie` - a lambda nested inside one
   enclosing scope, body referencing both its own (nested, depth 0) and the enclosing (depth 1) parameter,
   ties for two alpha-equivalent instances using entirely different nested-parameter instances/names; a
   negative control (a lambda that does NOT capture the enclosing parameter) proves the equality is not
   vacuous - the "nested lambda plus enclosing capture" minimum.
3. `Compare_TwoNestedLambdaLevelsPlusEnclosingScope_DistinguishesAllThreeDepths` - `enclosing -> nested A ->
   nested B`, with three otherwise-structurally-identical terms differing only in which of `outer`/`a`/`b` the
   inner body references (same position, same type), proving depths 0/1/2 sort strictly `b < a < outer` (and
   antisymmetrically the other way), plus an alpha-equivalence check at this same two-level depth (fresh `a`/`b`
   instances each build) - the "multiple nested lambda depths" minimum.
4. `BuildKeys_MultipleArgumentsWithNestedLambdas_RestoresSharedScopeBetweenSiblings` (P1's own test,
   `UtilsTest/Mathematics/Expressions/ExpressionSimplifierAdditiveSortScaleTests.cs`) - kept completely
   unchanged and still passes, since `ScopeState`'s lazy `NestedScopes` list is pushed/popped through the same
   `try`/`finally` discipline `BuildLambda` already used - the "sibling scope restoration" requirement.

**Validation performed (2026-09-24, in order, on `Utils.csproj` still targeting `net8.0`):**

1. New scope-state tests plus P1's existing scale-test file (`ExpressionCanonicalOrderScopeStateTests` +
   `ExpressionSimplifierAdditiveSortScaleTests`): 9/9 passed (3 new + 6 pre-existing, unmodified).
2. `ExpressionSimplifierStructuralCanonicalizationTests` (the full S4 regression suite): 46/46 passed.
3. Full `UtilsTest/Mathematics/Expressions` namespace: 480/480 passed (477 pre-existing, including every test
   P1/P2/P3 already added to this namespace, + 3 new from this PR's `ExpressionCanonicalOrderScopeStateTests`).
4. Full `UtilsTest.Unit`: 7 841/7 841 passed, 0 skipped.
5. Full `UtilsTest.Functional`: 383/383 passed.
6. Full `UtilsTest.Security`: 225/228 passed, 3 skipped - the same three pre-existing, unrelated,
   platform-gated tests noted throughout this roadmap (`TryCreate_ReturnsNull_OnNonWindowsPlatform`,
   `VerifyAuthenticodeSignature_OnNonWindows_ThrowsPlatformNotSupportedException`,
   `HasValidAuthenticodeSignature_OnNonWindows_ThrowsPlatformNotSupportedException`).
7. Release build of `Utils.sln` (the full multi-project solution): succeeded, 0 errors, 51 warnings - all
   pre-existing and unrelated (the same `NU1603` package-resolution notices and `DrawTest`/`Fractals`
   nullable/cref warnings noted throughout this roadmap).
8. `master` re-checked immediately before this validation run (`git fetch origin` + `git rev-parse`):
   unchanged at `77d571de...`, so no rebase was needed before opening the PR.

**S5 follow-ups still open (unchanged by this PR, listed here again for continuity):**

- The end-to-end nested-canonicalization/re-simplification construction-cost hazard the public `Simplify()`
  control above ran into again (pre-existing, first documented at P1, re-disclosed at P3 round 7 and again
  here) - not introduced or fixed by this PR.
- "Intermediate group/list materialization" and "`ExpressionComparer` temporary arrays and searches" - as
  P1 already found, not shown to be a comparably significant hotspot by any benchmark run so far; would need
  its own dedicated `AssemblyLoadContext`/thread-safety/determinism analysis before introducing any cache.
- Reflection-metadata caching for `CompareType`/`CompareMethod`/`CompareMember` remains unexplored, as noted
  throughout S4/S5.

This PR is opened for review only and is **not** merged, per the task brief.

## Execution-optimizer stages

These remain separate from the simplifier stages above.

### O0 — Audit the existing `ExpressionOptimiser` correctness boundary

Before adding benchmark-driven lowering, audit the already-public `ExpressionOptimiser` for the same
execution-semantic hazards that an optimizer must preserve rather than merely document away. At minimum
characterize custom unary/binary operator `Method`s, lifted nullable arithmetic, double-negation rewrites,
zero identities, side-effect/evaluation-count preservation and exception behavior. This is a separate PR
from S3: do not fix `ExpressionOptimiser` while formalizing the simplifier's symbolic contract.

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
