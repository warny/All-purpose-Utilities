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

### S5 — Construction-performance cleanup

Only after the correctness/structure stages above, re-profile construction-time allocations and CPU cost in the simplifier.

Potential remaining areas include:

- additive `OrderBy` / `ThenBy` / `ThenBy` / `ToList` / `GroupBy` pipeline;
- repeated key computation;
- intermediate group/list materialization;
- `ExpressionComparer` temporary arrays and searches;
- other LINQ/reflection scaffolding still present on frequently used construction paths.

Construction optimizations should be accepted only when they do not weaken the symbolic contract or complicate the later execution optimizer. Prefer small, benchmarked and causally isolated changes like PRs #577-#589.

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
