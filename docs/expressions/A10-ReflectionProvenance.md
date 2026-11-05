# S5 A-10 — Trust in reflection metadata (fix note)

Base: `master` `623c7802` (after PR #681). Scope: the expression engine must never give the privileges of an intrinsic or
trusted member to a reflection object that is not the runtime's own, and its decisions must never run the members of such an
object. This is not a general protection against user code: user methods obtained through ordinary reflection stay user code,
analyzed as before.

## 1. Root cause

`System.Linq.Expressions` factories accept any subclass of `MethodInfo`, `FieldInfo`, `PropertyInfo` and `ConstructorInfo`.
Every member of such a subclass is user code: `DeclaringType`, `Name`, `Attributes`, `GetParameters`, `ReturnType`,
`IsDefined`, `Equals`, `GetHashCode`. Several engine decisions read that metadata, or compared it with `==` (which calls
the left operand's `Equals`), to decide that a node was intrinsic or inert:

- the effect gate's method classification and its `ConcurrentDictionary<MethodInfo, …>` cache (hash and equality first,
  then `DeclaringType == typeof(Math)` and the name);
- the "ordinary arithmetic" checks (`IsOrdinaryBinaryArithmetic`, `IsOrdinaryUnaryNegate`), which compared the node's
  operator method with the expected one through the node method's own `Equals`;
- the call-rule signatures (`ExpressionCallSignatureAttribute.Match`), which matched rules on `DeclaringType` and `Name`, and
  whose rules replace the call by an intrinsic node or by the real function;
- the gate's operator and conversion classification (`DeclaringType == typeof(decimal)`, `Name == "op_Implicit"`) and its
  field classification (`IsInitOnly`, `IsLiteral`, read from the user's `Attributes`);
- the inlining analysis (`GetParameters` for by-reference arguments, `IsDefined(IsReadOnlyAttribute)` for receiver mutation).

## 2. Reproducers (before the fix, `master` `623c7802`)

| Tree | Result before | After |
|---|---|---|
| `FakeMathSin(x) * 0` | `0`: the user call is dropped | `0 * FakeMathSin(x)`: kept, evaluated once |
| `FakeDoubleSin(x)` | `double.Sin(x)`: replaced by the real function (call rule) | unchanged |
| `FakeMathPow(x, 2)` | `x ** 2`: replaced by an intrinsic power (call rule) | unchanged |
| `d + 0m` with a fake `decimal.op_Addition` | `d`: the user operator is dropped | unchanged |
| `d * 0m` with a fake `decimal.op_Multiply` | `0` | unchanged |
| `Convert(i, decimal, fake op_Implicit)` | gate: inert | gate: observable |
| field `cell.V` through a fake `FieldInfo` claiming `InitOnly` | gate: inert; `cell.V * 0 -> 0` | observable; kept |
| any analysis of a fake method | hostile `GetHashCode`, `Equals`, `DeclaringType`, `Name`, `GetParameters`, `ReturnType` called | none called (§6) |

"Fake" is a user `MethodInfo`/`FieldInfo` forwarding to the real member and answering `true` when compared with it. The
same tests run against `master` fail 7 of 8 (`UtilsTest.Security/Expressions/ExpressionReflectionProvenanceTests`).

## 3. Fix

One provenance check, `ExpressionReflectionProvenance` (internal, `Utils/Expressions/ExpressionReflectionProvenance.cs`):

- `IsRuntime(member)` compares `member.GetType()` (not virtual) by reference with the closed set of concrete classes the
  runtime uses for loaded code: methods (`RuntimeMethodInfo`: ordinary, generic definitions and instantiations, array
  methods, methods of created dynamic and collectible types), constructors, ordinary and literal fields, properties, and
  `DynamicMethod`. The classes are read at type initialization from members of `object`, `string` and `int`, never named.
- `IsSameMember(actual, expected)` compares a node's member with an expected runtime member without calling the equality of
  an object that is not a runtime one.

Applied at every decision point found by the audit (§4), always before any member of the object is read and before it
reaches a cache:

| Site | Change |
|---|---|
| `ExpressionEffectAnalysis.MethodEffects` and its cache | non-runtime method: `Observable`, never cached |
| `ExpressionEffectAnalysis.OperatorMethodEffects` | non-runtime operator method: `Observable` |
| `ExpressionEffectAnalysis.ConversionEffects` | non-runtime conversion method: `Observable` |
| `ExpressionEffectAnalysis.AnalyzeMember` | non-runtime field: `Observable` |
| `ExpressionSimplifier.IsOrdinaryBinaryArithmetic`, `IsOrdinaryUnaryNegate` (hence `IsOrdinaryFieldDivision`) | non-runtime method refused before `IsLifted`; comparison through `IsSameMember` |
| `ExpressionCallSignatureAttribute.Match` | non-runtime method never matches a call rule |
| `AdditionOfCos2andSin2Number`, `CanCombineUnaryLogCalls` | comparison through `IsSameMember` (defense in depth) |
| `InvocationInliningAnalysis` (`ScanCall`, `ScanMember`, `ScanNew`, `ScanIndex`) | non-runtime member: `Preserve` |
| `ExpressionMethodBehaviors`, `ExpressionBehaviorAnalysis` (P18-A) | same check (previously "declared in the core library", which also accepted emit builders) |
| `ExpressionTraceFormatter` | renders `<custom member>` instead of reading the name |

No public API change. `ExpressionCallSignatureAttribute.Match` (public) no longer matches a call whose method is not a runtime
reflection object; nothing else changes for runtime objects. Declarations (`[Deterministic]`) are still not consumed, and
the exception policy is unchanged.

## 4. Audited paths

| # | Path | Object, provenance | Check before | Custom object before | Impact before | Status |
|---|---|---|---|---|---|---|
| 1 | `ExpressionEffectAnalysis.MethodEffects` + cache | `MethodCallExpression.Method`, caller | none | `GetHashCode`, `Equals`, `DeclaringType`, `IsStatic`, `GetParameters`, `ReturnType`, `Name` run; Math/double impersonation trusted | drop, merge, reorder (`AllowsRules`, `SplitPinnedTerms`) | **fixed** |
| 2 | `TryAnalyzeShallow` | same | via 1 | same | same | fixed via 1 |
| 3 | `OperatorMethodEffects` | `BinaryExpression.Method` | none | `Equals` (via 7), `DeclaringType` | custom operator inert | **fixed** |
| 4 | `ConversionEffects` | `UnaryExpression.Method` | none | `DeclaringType`, `Name` | custom conversion inert | **fixed** |
| 5 | `UnaryOwnEffects` | `UnaryExpression.Method` | via 7 | `Equals` | custom negation inert | fixed via 7 |
| 6 | `AnalyzeMember` | `MemberExpression.Member` | none | `Attributes` (`IsInitOnly`, `IsLiteral`) | field claims read-only → inert | **fixed**; properties were already observable without read |
| 7 | `IsOrdinaryBinaryArithmetic`, `IsOrdinaryUnaryNegate`, `IsOrdinaryFieldDivision` | node method | none | `Equals` lies; `IsLifted` reads `GetParameters` | algebraic rules on a custom operator (`d + 0m -> d`) | **fixed** |
| 8 | `ExpressionCallSignatureAttribute.Match` | call method | none | `DeclaringType`, `Name` | call rules replace the call (`Pow` → `Power`, `double.Sin` → real) | **fixed** |
| 9 | `CanCombineUnaryLogCalls` | two matched calls | via 8 | `Equals` | log combination | fixed via 8, hardened |
| 10 | `AdditionOfCos2andSin2Number` | `Power` operand calls | gate (1) | `Equals` lies | `sin² + cos² -> 1` | fixed via 1, hardened |
| 11 | `ClassifyForAdditiveGrouping` (`Method.Name`) | unpinned terms | gate (1) | `Name` | ordering only | not vulnerable after 1 (hostile terms are pinned) |
| 12 | `InvocationInliningAnalysis` scans | call, member, constructor, indexer | none | `GetParameters`, `IsStatic`, `IsDefined` | wrong inlining (lost by-reference write, struct receiver mutation) | **fixed** (`Preserve`) |
| 13 | `InvocationInliningAnalysis.Analyze` body type check | `MethodCallExpression.Type` = `Method.ReturnType` | none | `ReturnType` | none (type equality) | residual L-1 |
| 14 | `ExpressionMethodBehaviors`, `ExpressionBehaviorAnalysis` | P18-A | assembly test | none read, but emit builders accepted | — | tightened |
| 15 | `ExpressionTraceFormatter` | call method, member | none | `DeclaringType`, `Name` | trace text only, inside a tracing scope | **fixed** |
| 16 | `ExpressionTraceCycleAnalyzer.MixIdentity` | members | reference identity | none | — | not vulnerable |
| 17 | `ExpressionComparer` equality and hash | `Method`, `Member`, `Constructor`, `Indexer`, `AddMethod` | none | `Equals`, `GetHashCode` | asymmetric equality, forced hash collisions | **separate finding A10-B** |
| 18 | `ExpressionCanonicalOrder` comparisons | same | none | `Name`, `DeclaringType`, `GetParameters`, `MetadataToken`, `Module` | ordering of keys | A10-B (simplifier keys are built for inert terms only) |
| 19 | `ComparisonMemo`, `ObservedComparisonMemo` | same | none | `GetHashCode`, `Equals` | memo key collisions | A10-B (memoized comparisons run on inert operands only) |
| 20 | Rebuilds (`CopyExpression`, substitution) | node members | none | framework factories read them | none (validation) | residual L-1 (framework reads, ETC R4 policy) |
| 21 | Rule discovery (`BuildRule`) | the transformer's own methods | runtime reflection | none | — | not vulnerable |
| 22 | `TransformCall` (`ExpressionSimplifier.Math.cs`) | method resolved by name on `FloatingPointType` | — | — | reached only through 8 | fixed via 8 |
| 23 | `Type` of nodes | `TypeDelegator` and user `Type` subclasses are accepted by the factories | none | `IsPrimitive`, `IsValueType`, `IsEnum`, equality | conversion classification | residual L-2 |

Why 17–19 are not bypasses of the fix: the simplifier compares, hashes, memoizes or keys only operands that passed the gate
(`AllowsRules`) or terms that were not pinned (`SplitPinnedTerms`); after the fix any node holding a non-runtime reflection
object is observable, so these consumers never decide a rewrite of such a node.

## 5. Distinct findings (not fixed here)

- **A10-B — structural consumers call the members of custom reflection objects.** `ExpressionComparer.Equals(fake, real)`
  returns `true` while `Equals(real, fake)` returns `false` (asymmetric), both hash alike, and the comparison calls the
  fake's `Attributes`, `GetHashCode`, `GetParameters`, `DeclaringType`, `Equals` and `ReturnType`. Same pattern in the canonical
  keys and the comparison memos. Proposed fix (separate PR): compare and hash non-runtime reflection objects by reference
  identity, never through their members.
- **A10-C — `ExpressionOptimiser` drops evaluations.** The separate `ExpressionOptimiser` rewrites `x * 0 -> 0`, `x + 0 -> x`
  and `x * 1 -> x` without any effect or operator check: `F() * 0` loses the call of `F`, and a custom operator is treated as
  arithmetic. Unrelated to reflection provenance; proposed as a separate PR.
- **P18-A list omission.** `Pow` is missing from the total floating functions of `ExpressionMethodBehaviors` (the gate trusts
  it). Conservative (`UnknownCode`); to add in a separate change.

## 6. Tests

- `UtilsTest.Security/Expressions/ExpressionReflectionProvenanceTests` (8 tests; 7 fail on `master`):
  S-01/S-08 impersonated `Math.Sin`, `double.Sin`, `Math.Pow` keep every evaluation (counted through the expression interpreter,
  which calls the fake's `Invoke`) for the exact and a derived simplifier; S-02/S-10 with every member armed to throw, the
  provenance check, the gate, the ordinary-arithmetic checks, the call signatures, the inlining analysis and the public
  analysis read nothing; S-06 caches, sequential and concurrent, with hash-sharing impersonations; S-03 operators; S-04
  conversions; S-05 fields and properties; provenance classes (ordinary, generic, array, dynamic, collectible, `DynamicMethod`,
  constructors, literal fields, properties accepted; emit builders, user subclasses, `null` refused); S-09 hostile nestings and
  DAGs refused by the resource limits without any member read.
- `UtilsTest/Mathematics/Expressions/Coverage/ExpressionReflectionProvenanceControlTests` (category `S5-A10`, S-07): real
  `Math.Sin`, `MathF.Sin`, `double.Sin`, `float.Sin`, `Math.Pow`, decimal operators, conversions and fields keep their
  classification and simplifications.

## 7. Residual limitations

- **L-1 — framework getters delegate to the object.** `MethodCallExpression.Type` returns `Method.ReturnType`;
  `BinaryExpression.IsLifted` and `UnaryExpression.IsLifted` read `GetParameters` for nullable operands; the framework
  factories that rebuild a parent read the members they validate. Every engine walk reads node types, so a custom method's
  `ReturnType` still runs during simplification (the tests count these reads instead of refusing them). These reads cannot
  grant any trust; making such nodes fully opaque would mean treating every node that holds a custom reflection object like an
  extension node in every walk.
- **L-2 — `Type` provenance.** The factories accept `TypeDelegator` and user `Type` subclasses as node types. The gate's
  conversion rules read `IsPrimitive`, `IsValueType` and `IsEnum` of node types. Out of the scope of this fix (method, member,
  operator and conversion metadata); to study separately.
- Emit builders (`MethodBuilder` before `CreateType`, generic instantiations over type builders) are refused conservatively.

## 8. Measurements

Windows 11, .NET 9, Release; `master` `623c7802` against the fix, 5 alternating processes per variant, 9 rounds each, medians;
A/A = master's odd runs against its even runs. Gate entries are called outside an operation, through compiled accessors.

| Case | master | fix | Δ | A/A |
|---|---:|---:|---:|---:|
| `MethodEffects(Math.Sin)`, warm cache | 6.5 ns | 6.7 ns | +3 % | 1 % |
| `MethodEffects(fake)` | 7.9 ns | 3.9 ns | −51 % | −5 % |
| `IsOrdinaryBinaryArithmetic`, `decimal +` | 16.3 ns | 19.3 ns | +18 % | 4 % |
| `IsOrdinaryBinaryArithmetic`, `double *` | 13.7 ns | 13.6 ns | −1 % | −4 % |
| gate, `Math.Sin(x)` | 23.7 ns | 23.9 ns | +1 % | 0 % |
| gate, `d + 1m` (operator method) | 44.5 ns | 49.5 ns | +11 % | −1 % |
| gate, `int -> decimal` (implicit conversion) | 21.6 ns | 22.8 ns | +6 % | 2 % |
| gate, `decimal -> int` (explicit conversion) | 43.9 ns | 45.0 ns | +3 % | −3 % |
| gate, mutable field | 65.2 ns | 68.3 ns | +5 % | −3 % |
| gate, literal field | 48.0 ns | 51.6 ns | +8 % | 3 % |
| gate, property | 56.0 ns | 55.8 ns | 0 % | −1 % |
| gate, mixed tree (8 nodes) | 429 ns | 430 ns | 0 % | 3 % |
| gate, 400 nested calls | 44.8 µs | 42.7 µs | −5 % | 0 % |
| gate, shared DAG (10 levels) | 108.3 µs | 106.7 µs | −1 % | 1 % |
| gate, hostile call | 56.9 ns | 45.3 ns | −20 % | −7 % |
| cold classification, 370 methods (per method) | 2.59 µs | 2.56 µs | −1 % | 2 % |

The check costs a few nanoseconds where it runs (an operator, conversion or field node: +1 to +5 ns); a warm method lookup is
unchanged, and the hostile case is cheaper because nothing is read. `Simplify` on the 11-case corpus: −2.0 % to +4.5 %, inside
the A/A spread of the same campaign (up to ±19 % on the smallest cases, ±1–4 % on the larger ones). Earlier iterations of the
fix measured +10–20 % on `decimal +` (the comparison called the runtime `Equals` instead of testing the reference first) and a
bimodal `int -> decimal` (24/48 ns, a code-layout effect); `IsSameMember` now tests the reference first and the conversion reads
the method once. The S5 campaigns (`terminate`, `converge`, `diff`, `effects`, `s3`, `canon`, 1,000 seeds) produce
byte-identical result files on `master` and on the fix.
