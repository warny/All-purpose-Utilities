# ETC — Expression tree coverage, reconstruction fidelity and structural equality

Stage identifiers (`R0`…`R4`, `ETC-xx`, `C-xx`, `H-xx`) are stable and are not pull-request numbers.

## R0 provenance

- Audit date: 2026-10-27.
- Baseline: `master` at `b5c05dc3fcf1bb4ab0dbdadece8dc0a5bec86276` (S5 P16 merged, PR #668), verified as the latest master
  commit before starting.
- Environment: Windows 11 Pro 10.0.26300, .NET SDK 10.0.403, `Utils` targets `net8.0`, tests run on `net9.0`
  (`ExpressionType` has 85 values on both).
- Scope: `ExpressionTransformer` (`Utils/Expressions/ExpressionTranformer.cs`), `ExpressionComparer`, `ExpressionSimplifier`
  (all partial files), `ExpressionCanonicalOrder`, substitution (`ReplaceArgumentsCore`) and the P3/P9/P15/P16 memo policies.
- R0 changes **no production code**. It adds the executable inventory, the baseline matrix, named reproducers and this
  roadmap. The relationship with `Utils/TODO-2026-09-12-expression-simplifier-roadmap.md` is: S1 left broader node coverage
  as a conservative follow-up; ETC is that follow-up. ETC does not add algebraic rules.

### R0 executable artifacts

All in `UtilsTest/Mathematics/Expressions/Coverage/`, category `ETC-R0` (`dotnet test UtilsTest/UtilsTest.Unit.csproj --filter TestCategory=ETC-R0`):

| File | Role |
|---|---|
| `ExpressionNodeCatalog.cs` | 117 samples: one primary sample per `ExpressionType` (85) plus 32 metadata/scope/initializer/special variants. Every sample places the marker leaf `p * 1.0` in every valid child position. |
| `ExpressionContractProbe.cs` | Runs six contracts on a sample and classifies the result (vocabulary below). Reusable by every round. |
| `ExpressionNodeInventory.cs` | Field map: every public class of the expression object model, every declared public property and its role; `ExpressionType` → public class map. |
| `ExpressionTreeInventoryTests.cs` | Programmatic completeness: every `ExpressionType` has one primary sample and a class; every public class/property of the running framework is classified; every `ExpressionVisitor.Visit*` target is inventoried; every inventoried class is exercised by the catalog. |
| `ExpressionTreeCoverageBaselineTests.cs` | The executable coverage matrix: 117 recorded rows × 6 contracts, plus a hash/equality consistency check. |
| `ExpressionTreeFindingsTests.cs` | Minimal named reproducers for ETC-01 … ETC-15. |

### Outcome vocabulary

Contracts (columns of the baseline): **G** generic recursive transformer (overrides `PrepareExpression => TransformCore`, finalizes
with `CopyExpression`, one rule rewriting the constant `1.0` into `2.0`), **S** exact `ExpressionSimplifier`, **D** derived
simplifier overriding nothing, **R** substitution of the free `p` by `5.0` through `ReplaceArguments`, **E** `ExpressionComparer.Equals`
on two independently built trees, **H** `GetHashCode` checked against **E**.

| Outcome | Meaning |
|---|---|
| `Rewrites` | every marker in every child position was reached and rewritten |
| `Partial` | some markers rewritten (a child position is not traversed) |
| `Opaque` | no marker rewritten: the node is passed through |
| `Same` / `Rebuilt` | no marker in the sample; same instance / equivalent new instance |
| `Drops` | (G, R) root semantic metadata lost (method, lifting, labels, variables, members…) with the same `Type` |
| `Corrupts` | `Type` changed, a child lost, or (S, D) the compiled result differs from the original |
| `Unbound` | (S, D) the result references a parameter that is not in scope and cannot compile |
| `Throws:X` | the contract threw `X` on a valid tree |
| `Equal` / `Unequal` | comparer verdict; `Unequal` on identical trees is the documented conservative answer |
| `Consistent` / `Mismatch` | hashing agrees / disagrees with equality |

## 1. Node taxonomy

`ExpressionNodeInventory` holds the authoritative data; `ExpressionTreeInventoryTests` fails if the runtime adds a node type,
class, property or visitor entry point that is not classified here.

### 1.1 Node types → public class (85 values)

| Public class | `ExpressionType` values |
|---|---|
| `BinaryExpression` (39) | Add, AddChecked, Subtract, SubtractChecked, Multiply, MultiplyChecked, Divide, Modulo, Power, And, Or, ExclusiveOr, LeftShift, RightShift, Equal, NotEqual, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, AndAlso, OrElse, Coalesce, ArrayIndex, Assign, AddAssign, AddAssignChecked, SubtractAssign, SubtractAssignChecked, MultiplyAssign, MultiplyAssignChecked, DivideAssign, ModuloAssign, PowerAssign, AndAssign, OrAssign, ExclusiveOrAssign, LeftShiftAssign, RightShiftAssign |
| `UnaryExpression` (20) | Negate, NegateChecked, UnaryPlus, Not, OnesComplement, Increment, Decrement, IsTrue, IsFalse, Convert, ConvertChecked, TypeAs, Unbox, ArrayLength, Quote, Throw, PreIncrementAssign, PreDecrementAssign, PostIncrementAssign, PostDecrementAssign |
| `TypeBinaryExpression` | TypeIs, TypeEqual |
| `NewArrayExpression` | NewArrayInit, NewArrayBounds |
| one class each | Constant (`ConstantExpression`), Parameter (`ParameterExpression`), Default, Call (`MethodCallExpression`), MemberAccess (`MemberExpression`), Invoke (`InvocationExpression`), Lambda (`Expression<TDelegate>`), Conditional, New, MemberInit, ListInit, Index, Block, Try, Switch, Loop, Goto, Label, RuntimeVariables, DebugInfo, Dynamic |
| any user subclass of `Expression` | Extension |

Structural objects that are not expression nodes: `CatchBlock`, `SwitchCase`, `LabelTarget`, `MemberBinding`
(`MemberAssignment`, `MemberMemberBinding`, `MemberListBinding`), `ElementInit`, `SymbolDocumentInfo`, and
`System.Runtime.CompilerServices.CallSiteBinder` (held by `DynamicExpression.Binder`).

### 1.2 Child positions, metadata and scopes

Roles: **C** child, **C?** nullable child, **C\*** ordered children, **S** structural objects owning children, **D** declaration
(introduces a binding), **V** variable reference that must stay a `ParameterExpression`, **L** label identity, **M** semantic
metadata, **G** debug metadata. Derived properties (`NodeType`, `CanReduce`, `ArgumentCount`, `IsLifted`, `Result`, `ReturnType`…)
are omitted; a `Type` is listed when the factory takes it as an independent input.

| Class | Children | Metadata | Scope |
|---|---|---|---|
| Binary | C `Left`, C `Right`, C? `Conversion` (a lambda) | M `Method`, M `IsLiftedToNull` (comparisons) | `Conversion` binds its own parameter |
| Unary | C? `Operand` (null only for `Rethrow`) | M `Method`, M `Type` (Convert, ConvertChecked, TypeAs, Unbox, typed Throw) | — |
| Constant | — | M `Value`, M `Type` | — |
| Parameter | — | M `Type`, M `IsByRef`, G `Name` | identity is the binding |
| Default | — | M `Type` | — |
| Conditional | C `Test`, C `IfTrue`, C `IfFalse` | M `Type` (void conditional) | — |
| TypeBinary | C `Expression` | M `TypeOperand`, M node type (TypeIs vs TypeEqual) | — |
| MethodCall | C? `Object`, C\* `Arguments` | M `Method` | — |
| Member | C? `Expression` (null for static) | M `Member` | — |
| Invocation | C `Expression`, C\* `Arguments` | — | — |
| Lambda | C `Body` | M `Type` (delegate), M `TailCall`, G `Name` | D `Parameters` |
| New | C\* `Arguments` | M `Constructor` (null for value-type default), M `Members` (anonymous types) | — |
| NewArray | C\* `Expressions` (elements or bounds) | M `Type` (element/rank), M node type | — |
| MemberInit | C `NewExpression`, S `Bindings` | binding order | — |
| ListInit | C `NewExpression`, S `Initializers` | M `ElementInit.AddMethod` | — |
| MemberAssignment / MemberMemberBinding / MemberListBinding | C `Expression` / S `Bindings` / S `Initializers` | M `Member` | — |
| ElementInit | C\* `Arguments` | M `AddMethod` | — |
| Index | C `Object`, C\* `Arguments` | M `Indexer` (null for arrays) | writable target position |
| Block | C\* `Expressions` | M `Type` | D `Variables` |
| Try | C `Body`, S `Handlers`, C? `Finally`, C? `Fault` | M `Type` | — |
| CatchBlock | C `Body`, C? `Filter` | M `Test` (exception type) | D `Variable` (nullable) |
| Switch | C `SwitchValue`, S `Cases`, C? `DefaultBody` | M `Comparison`, M `Type` | — |
| SwitchCase | C\* `TestValues`, C `Body` | — | — |
| Loop | C `Body` | — | L `BreakLabel`, L `ContinueLabel` |
| Goto | C? `Value` | M `Kind`, M `Type` | L `Target` |
| Label | C? `DefaultValue` | — | L `Target` |
| LabelTarget | — | M `Type`, G `Name` | identity is the label |
| RuntimeVariables | — | — | V `Variables` |
| DebugInfo | — | G `Document`, G lines and columns (`IsClear` derived) | — |
| Dynamic | C\* `Arguments` | M `Binder`, M `DelegateType`, M `Type` | — |
| Extension | user-defined (`VisitChildren`) | user-defined (`CanReduce`, `Reduce`) | user-defined |

## 2. Coverage matrix

Status: ✅ correct; ⚠️ correct but incomplete or lossy for some callers; ❌ defect (corrupts, throws or loses semantics);
⛔ conservative or unsupported by design; — not applicable. Recognition = an explicit `Prepare*` case in
`ExpressionTransformer.PrepareTransform`. Equality and Hashing describe the **public comparer** contract (it simplifies first).
**Tests**: `B` = baseline row in `ExpressionTreeCoverageBaselineTests`, `F` = named reproducer in `ExpressionTreeFindingsTests`.
**Perf**: no column is performance-validated for the new families yet; the established scalar families are covered by the S5
benchmark corpora only.

| Family (samples) | Recog. | Traversal | Reconstruction | Substitution | Equality | Hashing | Scope | Simplifier safety | Tests | Perf |
|---|---|---|---|---|---|---|---|---|---|---|
| Arithmetic, bitwise, comparison, logical binary (22 kinds) | ✅ | ✅ | ✅ `MakeBinary` | ✅ | ✅ (Add/Multiply commutative fallback) | ✅ | — | ✅ S3 guards | B | S5 |
| Coalesce; binary metadata: custom `Method`, lifted, lifted-to-null, `Coalesce` + `Conversion` | ✅ | ✅ (`Conversion` lambda not traversed) | ✅ | ✅ (`Conversion` not substituted) | ✅ (`Conversion` compared) | ✅ | ⚠️ `Conversion` binds a parameter | ✅ S3 | B | S5 |
| ArrayIndex | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | ⚠️ may throw | B | — |
| Assign and 14 compound assignments (incl. custom `Method`) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | ❌ ETC-15 (side effects merged, dropped, reordered) | B, F | — |
| Pre/Post Increment/Decrement Assign (incl. custom `Method`) | ✅ | ✅ | ⚠️ S ✅; G/D drop `Method` (ETC-04) | ⚠️ drops `Method` | ✅ | ✅ | — | ❌ ETC-15 | B, F | — |
| Negate, NegateChecked, UnaryPlus, Not, OnesComplement, Increment, Decrement, IsTrue, IsFalse | ✅ | ✅ | ⚠️ S ✅; G/D drop `Method` (ETC-04) | ⚠️ drops `Method` | ✅ | ✅ | — | ✅ S3 (`IsOrdinaryUnaryNegate`) | B, F | S5 |
| Convert, ConvertChecked, TypeAs, Unbox | ✅ | ✅ | ⚠️ G/D drop conversion `Method` (ETC-04) | ⚠️ drops `Method` | ✅ | ✅ | — | ✅ | B, F | — |
| ArrayLength | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | ✅ | B | — |
| Throw (typed), Rethrow | ✅ | ❌ G/D: NRE on `Rethrow` (ETC-13) | ⚠️ S ✅; G/D lose typed `Type` (ETC-04) | ❌ typed throw becomes `void` | ✅ | ✅ | — | ⚠️ exception node treated as a value | B, F | — |
| Quote | ✅ | ✅ | ✅ | ⛔ quoted lambda not entered | ✅ | ✅ | ⚠️ captured outer parameters | ❌ policy ETC-12 (quoted data rewritten) | B, F | — |
| Constant | ✅ | — | ✅ | — | ✅ exact numeric model | ✅ | — | ✅ | B | S5 |
| Parameter | ✅ | — | — | ✅ (free) | ✅ binding-aware | ✅ De Bruijn | ✅ lambda only | ✅ | B | S5 |
| Default | ⛔ `PrepareDefault` | — | ✅ (returned as is) | — | ⛔ Unequal | ⛔ coarse | — | ✅ | B | — |
| Call (static, instance receiver) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | ⚠️ S3 purity assumption | B | S5 |
| MemberAccess (instance, static) | ⛔ `PrepareDefault` | ❌ receiver never traversed (ETC-10) | ✅ (returned as is) | ❌ receiver not substituted (feeds ETC-11) | ✅ | ✅ | — | ✅ | B, F | — |
| Invoke (lambda, delegate) | ✅ | ✅ | ✅ | ❌ lambda target not entered (ETC-03) | ⛔ Invocation unsupported (a lambda target compares `Equal` only because it is inlined first) | ⛔ coarse | — | ❌ ETC-11 inlining leaves unbound parameters; S3 evaluation count | B, F | — |
| Lambda (incl. delegate type, name, tail call) | ✅ | ✅ (body via `TransformCore`) | ⚠️ S ✅; G/D lose metadata (ETC-05) | ❌ not entered (ETC-03) | ✅ alpha-equivalence | ✅ | ✅ parameters | ✅ S4 lexical scope | B, F | S5 |
| Conditional | ✅ | ✅ | ✅ | ✅ | ⛔ Unequal (ETC-06) | ⛔ coarse (ETC-07) | — | ⚠️ branches rewritten independently | B, F | — |
| TypeIs, TypeEqual | ⛔ | ❌ `IndexOutOfRangeException` (ETC-09) | ❌ | ⛔ not entered | ❌ throws | ❌ throws | — | ❌ | B, F | — |
| New (ctor arguments, `Members`, value-type default) | ⛔ | ❌ `ArgumentException` / `ArgumentNullException` (ETC-09) | ❌ loses `Members` (ETC-02) | ⛔ | ❌ throws | ❌ throws | — | ❌ | B, F | — |
| NewArrayInit | ⛔ | ❌ every element dropped (ETC-08) | ❌ | ⛔ | ⛔ Unequal | ⛔ | — | ❌ silent corruption | B, F | — |
| NewArrayBounds | ⛔ | ❌ `ArgumentException` (ETC-09) | ❌ | ⛔ | ❌ throws | ❌ throws | — | ❌ | B, F | — |
| MemberInit, MemberAssignment / MemberMemberBinding / MemberListBinding | ⛔ | ⛔ opaque | ✅ (as is) | ⛔ | ⛔ | ⛔ | — | ✅ (untouched) | B, F | — |
| ListInit, ElementInit (incl. explicit `AddMethod`) | ⛔ | ⛔ opaque | ✅ (as is) | ⛔ | ⛔ | ⛔ | — | ✅ | B, F | — |
| Index (array, indexer) | ⛔ | ⛔ opaque | ✅ (as is) | ⛔ | ⛔ | ⛔ | — | ✅ | B, F | — |
| Block (variables, typed, shadowing, DebugInfo, goto/label, runtime variables) | ⛔ | ❌ replaced by an empty `void` block (ETC-08) | ❌ loses variables/type (ETC-02) | ⛔ | ⛔ | ⛔ | ❌ block variables unknown to comparer/S4 | ❌ silent corruption | B, F | — |
| Try (catch, catch variable + filter, finally, fault) | ⛔ | ⛔ opaque | ✅ (as is) | ⛔ | ⛔ | ⛔ | ⚠️ catch variable unmodeled | ✅ (untouched) | B, F | — |
| Switch (incl. `Comparison`) | ⛔ | ⛔ opaque | ✅ (as is) | ⛔ | ⛔ | ⛔ | — | ✅ | B, F | — |
| Loop (break, continue) | ⛔ | ❌ `IndexOutOfRangeException` (ETC-09) | ❌ loses labels (ETC-02) | ⛔ | ❌ throws | ❌ throws | ❌ labels unmodeled | ❌ | B, F | — |
| Goto (return, goto), Label | ⛔ | ⛔ opaque | ✅ (as is) | ⛔ | ⛔ | ⛔ | ❌ labels unmodeled | ✅ | B | — |
| RuntimeVariables | ⛔ | — | ✅ (as is) | ⛔ (correct: cannot replace by a non-parameter) | ⛔ | ⛔ | ❌ references unmodeled | ✅ | B | — |
| DebugInfo | ⛔ | — | ✅ (as is) | — | ⛔ | ⛔ | — | ✅ | B | — |
| Dynamic | ⛔ | ⛔ opaque | ✅ (as is) | ⛔ | ⛔ | ⛔ | — | ✅ | B, F | — |
| Extension (reducible, irreducible) | ⛔ | ⛔ opaque (`Reduce`/`VisitChildren` never called) | ✅ (as is) | ⛔ | ⛔ | ⛔ (reads the virtual `NodeType`/`Type` getters only) | — | ✅ | B, F | — |

No family is reported as fully supported without a baseline row; every ❌ cell has a named reproducer.

## 3. Findings register

Severity: Critical = silent wrong result from a public entry point; High = exception or unusable output on valid input from a
public entry point, or wrong result inside a documented scope; Medium = contract gap or policy issue; Low = quirk.

| Id | Severity | Location | Minimal reproducer | Actual | Expected | Contracts | Round | Evidence | Status |
|---|---|---|---|---|---|---|---|---|---|
| ETC-01 | High (root cause of ETC-08/09/10) | `ExpressionTransformer.PrepareTransform`, `PrepareDefault` | `Simplify(MemberInit(...p*1...))` | 20 node types (Default, MemberAccess, TypeIs, TypeEqual, New, NewArrayInit, NewArrayBounds, MemberInit, ListInit, Index, Block, Try, Switch, Loop, Goto, Label, RuntimeVariables, DebugInfo, Dynamic, Extension) reach `PrepareDefault`: no child prepared, `FinalizeExpression(e, [])` | each family traversed, passed through explicitly, or rejected explicitly | G, S, D | R1–R4 | `ETC01_*`, baseline `Opaque`/`Throws`/`Corrupts` rows | Confirmed |
| ETC-02 | High | `ExpressionTransformer.CopyExpression` | `CopyExpression(Block([v], …), children)` | Block loses variables and explicit type; Loop loses labels; New loses `Members`; 11 child-bearing node types (Lambda, MemberAccess, MemberInit, ListInit, Index, Try, Switch, Goto, Label, Dynamic, Extension) ignore the supplied children | rebuild preserves every M/D/L role of §1.2, or fails explicitly | G, D, every subclass using `CopyExpression` | R1 (scalar), R2 (Block/Loop), R3 | `ETC02_*` | Confirmed |
| ETC-03 | High | `ExpressionTransformer.ReplaceArgumentsCore` | `ReplaceArguments(Invoke(() => p), [p], [5])` | recurses into Unary, Binary, Invocation, MethodCall, Conditional only; rebuilds unary via `CopyExpression` (typed throw → void) | capture-avoiding substitution of free occurrences in every family (§5.3) | R, S (inlining) | R2 (+R1 for unary fidelity) | `ETC03_*`, baseline `Partial`/`Opaque` | Confirmed |
| ETC-04 | Medium | `CopyUnaryExpression`, `RebuildUnaryExpression` | `RecursiveTransformer.Transform(Negate(x, FortyTwo))` | G and D drop `Method` (value 42 → -3) and typed `Throw`'s `Type`; S preserves both | policy C-01 | G, D, R | R1 | `ETC04_*`, rows `Negate.CustomMethod`, `Convert.CustomMethod`, `PreIncrementAssign.CustomMethod`, `Throw` | Confirmed |
| ETC-05 | Medium | `RebuildLambdaExpression` | `Transform(Lambda<CatalogDelegate>(…, "named", true, x))` | G and D rebuild `Func<double,double>`, no name, no tail call; S preserves | policy C-01 | G, D | R1 | `ETC05_*`, row `Lambda.Metadata` | Confirmed |
| ETC-06 | Medium (by design) | `ExpressionComparer.EqualsCore` | two identical `Condition(…)` | `Unequal` for every family outside Parameter/Constant/Lambda/Unary/Binary/Call/Member | structural equality with scope model, family by family | E | R1–R4 | `ETC06_*`, 32 `Unequal` rows | Confirmed (not a false positive) |
| ETC-07 | Low (by design) | `ExpressionComparer.Hash`, `HashUnsupported` | two different conditionals | identical hash (`NodeType`, `Type`) | hash extended together with equality | H | R1–R4 | `ETC07_*`, `Hashing_IsNeverInconsistentWithEquality` | Confirmed |
| ETC-08 | **Critical** | `PrepareDefault` + `ExpressionSimplifier.FinalizeExpression` → `CopyExpression(e, [])` | `Simplify(NewArrayInit(p*1, p*1))` | returns `new double[0]`; any `Block` becomes an empty `void` block (statements, variables, result lost) | traversal or pass-through, never a rebuild from an empty child list | S, D, G | R1a | `ETC08_*`, rows `NewArrayInit`, `Block*` | Confirmed |
| ETC-09 | High | same path | `Simplify(TypeIs(…))`, `Simplify(New(ctor, p))`, `GetHashCode(TypeIs(…))` | `IndexOutOfRangeException` (TypeIs, TypeEqual, Loop), `ArgumentException` (New with arguments or members, NewArrayBounds), `ArgumentNullException` (value-type `New`); the public comparer throws too, so such trees cannot enter a `HashSet<Expression>` while `Equals(x, x)` is `true` | no exception on valid trees | S, D, G, E, H | R1a | `ETC09_*`, 7 `Throws` rows | Confirmed |
| ETC-10 | Medium | `PrepareTransform` (MemberExpression → `PrepareDefault`) | `Simplify(TimeSpan.FromSeconds(p*1).TotalSeconds)` | same instance; receiver never simplified although the comparer and P15 treat Member as supported | receiver traversed and rebuilt with `Expression.MakeMemberAccess` | G, S, D, R | R1 | `ETC10_*`, row `MemberAccess` | Confirmed (not in the input list) |
| ETC-11 | High | `ExpressionSimplifier.InvokeExpression` + ETC-03 | `Simplify(x => ((q) => () => q)(x)())` | inlined body keeps `q` unbound; `Compile()` throws `InvalidOperationException`; same with a member-access body | capture-avoiding inlining or no inlining | S, D | R2 (guard in R1a) | `ETC11_*` | Confirmed |
| ETC-12 | Medium (policy) | `PrepareUnary` for `Quote` | `Simplify(Quote(x => x * 1))` | quoted lambda body rewritten to `x` | policy C-05 | S, D, G | R4 | `ETC12_*`, rows `Quote*` | Confirmed |
| ETC-13 | Medium | `PrepareUnary` → `PrepareExpression(null)`; `ReplaceArgumentsCore` | `Transform(Rethrow(typeof(double)))` | G and D throw `NullReferenceException`; substitution returns a `void` throw | null operand passed through | G, D, R | R1a | `ETC13_*`, row `Throw.Rethrow` | Confirmed |
| ETC-14 | Medium | `ExpressionCanonicalOrder.Build` (`UnsupportedKey`) | `Compare(Conditional, Default)` | 0 for every unsupported pair | each family gaining equality also gains a canonical key, before additive/multiplicative grouping can merge it | S4 | with each equality extension | `ETC14_*` | Confirmed |
| ETC-15 | High (within S3) | algebraic rules on Unary/Binary operands; `IsOrdinaryBinaryArithmetic` does not look at operands | `Simplify(++v + ++v)`, `((v = 5) * 0) + v`, `++v + v` | `2 * ++v` (3 → 2), `v` (5 → 0), `v + ++v` (2 → 1) | assignment, increment and throw nodes are opaque operands for symbolic rules (C-06) | S, D | R1 | `ETC15_*` | Confirmed (not in the input list) |

## 4. Compatibility register

Every entry needs an explicit decision before its round changes production behavior. "Recommendation" is the R0 proposal.

| Id | Behavior | Affected surface | Options | Recommendation | Decision |
|---|---|---|---|---|---|
| C-01 | Unary `Method`/`Type` and lambda metadata loss for generic transformers and derived simplifiers (ETC-04/05) | public `ExpressionTransformer` subclasses, derived `ExpressionSimplifier`s | (a) keep historical loss; (b) make the faithful rebuild the base default; (c) faithful for derived simplifiers only | (b): the loss changes execution semantics (custom operators, typed throws) and no test documents it as intended; record as a 2.0.0 behavior change | pending |
| C-02 | `CopyExpression` (protected static) with a child list that does not match the node | every subclass finalizing with `CopyExpression` | (a) keep silent corruption; (b) return `e` when the list is empty and the family has children; (c) throw `ArgumentException` on arity mismatch | (b) for an empty list (exact pass-through, fixes ETC-08/09 for every subclass), (c) otherwise | pending |
| C-03 | Families newly traversed (`Prepare*` added) | subclass rules declared on those node types now receive positional children; `FinalizeExpression` receives non-empty `parameters`; `PrepareExpression` is called on more children (call count/order observable, H-1, H-2) | (a) traverse for every subclass; (b) traverse for the exact simplifier only | (a), announced in the changelog; keep the lambda body asymmetry (H-4) | pending |
| C-04 | Public comparer: more `Equal` results (ETC-06) | external `Dictionary`/`HashSet<Expression>` users | per family, after reconstruction is fixed | adopt per family with alpha-equivalence; never before ETC-08 is fixed (otherwise corrupted trees compare equal: two different arrays both become `new double[0]`) | pending |
| C-05 | Quoted expressions (ETC-12) | `Simplify` callers building query providers | (a) keep rewriting; (b) never enter `Quote`; (c) enter only for the exact simplifier with an option | (b): a quoted tree is data; rewriting it is not part of the symbolic contract | pending |
| C-06 | Side-effecting operands (ETC-15) | `Simplify` results for trees containing assignments | (a) keep S3 purity assumption; (b) treat assignment/increment/throw nodes (and any subtree containing them) as opaque for algebraic rules | (b): cheap local check like the custom-operator guards; generalized purity analysis stays out of scope | pending |
| C-07 | Exceptions instead of corruption for families not yet supported | `Simplify`, comparer | (a) pass through unchanged; (b) throw `NotSupportedException` | (a) pass-through for the simplifier and comparer (matches the conservative policy), explicit `NotSupportedException` only for substitution into `RuntimeVariables` | pending |

### Subclass compatibility hazards

- **H-1** Rule shapes: a subclass rule `[ExpressionSignature(ExpressionType.New)] Expression R(NewExpression e, Expression a0)` is
  matched positionally only when `PrepareNew` exists; today it receives no children. Adding `Prepare*` changes which rules match.
  No built-in rule targets a newly traversed family (built-in rules: Add, Subtract, Multiply, Divide, Negate, Power, Invoke and
  calls; `ExpressionDerivation`/`ExpressionIntegration` add Parameter, Constant, Convert, ConvertChecked).
- **H-2** `PrepareExpression` overrides observe more calls, in child order. Order inside each new family must follow
  `ExpressionVisitor` order (documented per family in R1–R4) so it is stable.
- **H-3** `FinalizeExpression(e, parameters)` receives non-empty `parameters` for new families; `ExpressionDerivation` throws
  `NotSupportedException` for them (unchanged), `ExpressionIntegration` likewise.
- **H-4** `PrepareLambda` transforms a lambda body through `TransformCore` even when `PrepareExpression` is the identity
  (`ETC01_NonRecursiveTransformer_TransformsOnlyLambdaBodyRoots`); Block/Try bodies must not silently adopt that behavior.
- **H-5** The exact-type gates (`GetType() == typeof(ExpressionSimplifier)`) in `PrepareExpression`, `RebuildUnaryExpression`,
  `RebuildLambdaExpression` and `FinalizeExpression` are part of the subclass contract (S1, S5); C-01 removes two of them.
- **H-6** `CopyExpression` is the documented default finalizer of third-party subclasses; C-02 changes its result for
  previously corrupting inputs only.
- **H-7** New hooks stay `internal virtual` (S1 policy): no new protected extensibility point without a separate decision.

## 5. Binding and label-scope model (R2 specification)

### 5.1 Declarations

| Binder | Declares | Scope of the declaration |
|---|---|---|
| Lambda | `Parameters` | `Body` |
| Binary `Conversion` (a lambda) | its parameter | the conversion body |
| Block | `Variables` | all `Expressions` |
| CatchBlock | `Variable` (nullable) | `Filter` and `Body` |
| Loop | `BreakLabel`, `ContinueLabel` | the loop body |
| Label / Goto | `Target` | the enclosing lambda body (labels are not lexically nested; a target is identified by instance) |

`RuntimeVariables.Variables` are references, not declarations. Reusing one `ParameterExpression` instance in two binders is
legal and means two different variables (shadowing).

### 5.2 Equality and hashing

- Generalize `ParameterBindingContext` and `ParameterScopeStack` from "lambda parameter lists" to "binder frames" tagged with
  the binder kind (lambda, conversion, block, catch). Equality pushes positional pairs for each binder and resolves a reference
  innermost first; hashing uses `(relative depth, position, kind)`. A free variable keeps reference identity, as today.
- Two binders are comparable only if they have the same kind, the same declared count and the same declared types/`IsByRef`.
  Names never participate.
- Labels: maintain one bijection `LabelTarget(left) ↔ LabelTarget(right)` per lambda body, established at the first occurrence
  (definition or jump) in the deterministic traversal order, and checked at every later occurrence; `Type` must match, `Name`
  is ignored. Hash a label by its first-occurrence index in the same traversal.
- No `ReferenceEquals` shortcut below the root (S1 rule), including for shared subtrees and shared labels.
- Required positive tests: alpha-renamed block variables; alpha-renamed catch variables; renamed labels; nested capture of an
  outer block variable from a lambda. Required negative tests: same-name distinct variables; swapped declaration positions;
  reference bound in one tree and free in the other; label bijection violated (two jumps to one label vs to two labels);
  same instance declared by two nested blocks (shadowing) compared with two distinct instances.

### 5.3 Substitution

- Replace only **free** occurrences. A binder that redeclares a replaced parameter instance hides it inside its scope.
- Capture avoidance: if a replacement's free variables include a variable declared by a binder the substitution enters,
  alpha-rename that binder's declaration (fresh `ParameterExpression`, same type and name) and rename its references, or
  refuse; R2 chooses renaming for lambda, block and catch.
- `RuntimeVariables` referencing a replaced parameter: replacement by another `ParameterExpression` is allowed, otherwise
  `NotSupportedException` (C-07).
- Label targets are reused, never cloned; a rebuild never creates a target that is not reachable from its definition.
- Distinguish this API (free substitution) from alpha-renaming of bound declarations (a separate internal helper).

### 5.4 Simplifier scope state

`OnEnterLambdaScope`/`OnExitLambdaScope` feed S4 canonical ordering with lambda parameters only. Block and catch variables
become bound variables for S4 too (otherwise two alpha-equivalent blocks canonicalize differently); add `internal virtual`
`OnEnterBlockScope`/`OnExitBlockScope` with the same unconditional policy (S4 round 7). The P9/P16 key hashes parameters by
reference, which stays valid inside a block because the key is used within one tree; this must be re-proved when block
variables become eligible, so they stay ineligible in R2 (§6).

## 6. Simplifier safety contract per family

| Family | Rewrite children symbolically? | Side effects / exceptions / order | New scope | S4 needs scope? | P3/P9/P15 memo | P16 reuse | Subclass hooks observe rebuild? |
|---|---|---|---|---|---|---|---|
| Member receiver | yes (receiver is an ordinary operand) | receiver evaluated once, unchanged | no | no | P15 already accepts Member; P9/P16 stay ineligible | no | `PrepareExpression` called on receiver (H-2) |
| TypeBinary, Default, New, NewArray | children yes; the node itself is never an arithmetic operand | constructors and allocations are effects: never merge or drop two `New`/`NewArray` nodes, even if equal | no | no | ineligible | no | yes |
| Conditional | each branch independently; never hoist across the test | branch evaluation is conditional | no | no | ineligible | no | already |
| Assignments, increments, Throw | operands yes; the node is opaque to algebra (C-06) | effects and exceptions must be kept and ordered | no | no | ineligible (already, not ordinary arithmetic) | no | already |
| Block, Try, Switch, Loop, Goto, Label | statements independently; no algebra across statements or exception/loop boundaries | order, count and exception regions preserved | Block, Catch, labels | yes (block/catch variables) | ineligible | no | new hooks internal |
| MemberInit, ListInit, Index | expression children yes; binding order and `Add` order preserved | each binding and `Add` is an effect | no | no | ineligible | no | yes |
| Dynamic | arguments only if C-07 says so; binder never interpreted | binder call is an effect | no | no | ineligible | no | — |
| Extension | never entered, never reduced (no getter, no `Reduce`) | user code | user | — | ineligible | no | — |
| Quote | not entered (C-05) | — | quoted lambda | — | ineligible | no | — |

Rule: a family enters a memo eligibility policy only in a separate, benchmarked stage with its own proof.

## 7. Robustness and resource limits (measured on the baseline, isolated processes)

Release build, default 1 MB main-thread stack, each measurement in its own process (scratch harness, not in the repository):

| Tree (depth n) | `Simplify` | `StructuralHashRaw` |
|---|---|---|
| Negate chain | ok at 3,000; process killed by stack overflow at 5,000 | ok at 5,000; overflow at 20,000 |
| Conditional nesting | ok at 3,000; overflow at 5,000 | ok at 100,000 (the unsupported root stops recursion) |
| `Math.Abs` call chain | ok at 2,000; overflow at 3,000 | ok at 3,000; overflow at 5,000 |
| Invoke of nested lambdas | ok at 1,000; overflow at 2,000 | ok at 100,000 (Invoke unsupported) |

Consequences: extending structural hashing/equality to Conditional, Invoke, Block, etc. **removes the accidental recursion
stop** and turns those trees into stack-overflow candidates. Every round that adds structural recursion must add a
`RuntimeHelpers.EnsureSufficientExecutionStack` guard (catchable `InsufficientExecutionStackException`) in the new recursive
paths, and pathological depth tests must run in an isolated process (`UtilsTest.Functional`, which may spawn processes), never
in the shared test runner. The S5 P16 stack overflow (`a*x + (1 - a)*x`) stays a separate known limitation. Wide trees, DAGs
with heavy sharing and large initializer collections are R1–R3 test items (the catalog currently uses small shapes).

## 8. Performance plan

R0 has no production change, hence no benchmark. From R1 on, every round uses the S5 protocol (Release, `net8.0` harness,
separate worktrees for baseline and candidate, paired runs, A/A control, counters only in instrumented copies) and reports:
traversal cost and allocations on the S5 scalar families (must stay within +5 % or be justified and approved), comparer and hash
CPU, rule dispatch counts, P3/P9/P15 hit/store counts and P16 regression families (p, q, i, h, g, t, x, D, R, z, k, n, e, o, F),
plus new families (deep/wide trees per new node kind). The `PrepareTransform` dispatch change (R1) needs a dedicated
micro-benchmark because it sits on the hot path; prefer strongly typed `Prepare*` methods without visitor allocations.

## 9. Correction rounds

Ordering constraints: (1) fix corruption and exceptions (R1a) before extending equality (C-04), otherwise corrupted trees
compare equal; (2) every equality extension ships its hash, its canonical key (ETC-14) and its stack guard (§7) in the same PR;
(3) scope model (R2) before substitution or equality for any binder.

### R1a — Stop corruption and exceptions (small PR, first)

| Change | File / method | Tests |
|---|---|---|
| Empty child list ⇒ pass-through; arity mismatch ⇒ `ArgumentException` (C-02) | `ExpressionTransformer.CopyExpression` | flip rows `NewArrayInit`, `NewArrayBounds`, `New*`, `TypeIs`, `TypeEqual`, `Loop`, `Block*` (G, S, D, E, H) to `Opaque`/`Unequal`/`Consistent`; `ETC08_*`, `ETC09_*` replaced by "unchanged instance" and "no exception" assertions; derived transformer with custom `FinalizeExpression` unaffected |
| Null operand passes through | `ExpressionTransformer.PrepareUnary` (or base `PrepareExpression` null short-circuit for every runtime type), `ReplaceArgumentsCore` | `ETC13_*`, row `Throw.Rethrow` |
| Inlining guard: do not inline when the lambda body contains a family `ReplaceArgumentsCore` does not enter | `ExpressionSimplifier.InvokeExpression` | `ETC11_*` compile and evaluate to 3 |
| Docs | this roadmap, `ExpressionComparer` remarks | — |

### R1b — Scalar traversal and reconstruction

| Change | File / method | Tests |
|---|---|---|
| Add `PrepareMember`, `PrepareTypeBinary`, `PrepareNew`, `PrepareNewArray`; `Default` explicit leaf. Replace the `PrepareTransform` type switch (would exceed 10 cases, AGENTS.md) by a family dispatch table or a two-level family switch, benchmarked | `ExpressionTransformer.PrepareTransform` and new `Prepare*` | rows flip to `Rewrites` for G/S/D; preparation order test per family (H-2); subclass rule shape test (H-1) |
| Per-family rebuilders replacing the 80-case `CopyExpression` switch: `MakeMemberAccess`, `TypeIs`/`TypeEqual`, `New(ctor, args, members)` and `New(type)`, `NewArrayInit`/`NewArrayBounds` | `CopyExpression` → dictionary of rebuilders | `RootMetadata` unchanged for every R1 sample (G, R) |
| C-01 faithful unary/lambda rebuild as base default | `RebuildUnaryExpression`, `RebuildLambdaExpression`, `CopyUnaryExpression`, `CopyExpression` unary branches, `ReplaceArgumentsCore` | rows `Negate.CustomMethod`, `Convert.CustomMethod`, `Throw`, `Lambda.Metadata` → `Rewrites` in G/D/R; update `ExpressionTransformerUnaryLazyParametersTests` only where it pins the loss (recorded here) |
| C-06 side-effect opacity | `ExpressionSimplifier` rule guards (`IsOrdinaryBinaryArithmetic`/operand classification used by additive/multiplicative grouping, `AdditionOfEqualsElements`, factoring, zero rules) | `ETC15_*` flipped to evaluation-preserving results; S5 corpora unchanged |
| Substitution through Member, TypeBinary, New, NewArray, Unary (faithful) | `ReplaceArgumentsCore` | rows → `Rewrites` in R |

### R1c — Scalar equality and hashing

| Change | File / method | Tests |
|---|---|---|
| Equality + hash for Conditional, Invoke, TypeBinary, Default, New (ctor, members), NewArray | `ExpressionComparer.EqualsCore`, `Hash` (family dispatch, AGENTS.md switch rule) | positive/negative pairs per metadata field; `Dictionary`/`HashSet` membership with identical references and independent trees; reflexivity/symmetry/transitivity on catalog pairs |
| Canonical keys for the same families | `ExpressionCanonicalOrder.Build` + new key nodes | `ETC14_*` flipped; S4 idempotence tests |
| Stack guards on new recursion | comparer, hash, canonical key | isolated-process depth test (Functional) |
| `New`/`NewArray` are never merged as equal terms by algebra (allocation identity) | simplifier grouping | dedicated tests |

### R2 — Binding and control flow

| Change | File / method | Tests |
|---|---|---|
| `PrepareBlock`, `PrepareTry` (handlers, filters, finally, fault), `PrepareSwitch` (comparison kept), `PrepareLoop`, `PrepareGoto`, `PrepareLabel`; `RuntimeVariables`, `DebugInfo` explicit leaves | `ExpressionTransformer` | rows `Block*`, `Try*`, `Switch*`, `Loop`, `Goto*`, `Label` → `Rewrites`; labels identical instances after rebuild; compile and run original vs rebuilt |
| Scope model §5 | `ExpressionComparer` binding context and scope stack | §5.2 positive and negative alpha tests |
| Capture-avoiding substitution §5.3 | `ReplaceArgumentsCore` (+ internal alpha-rename helper) | `ETC03_*`, `ETC11_*` flipped; capture and shadowing tests |
| Block/catch scope hooks for S4 | `ExpressionTransformer`, `ExpressionSimplifier` | canonical order of block-local terms independent of variable instances |

### R3 — Initializers and indexing

| Change | File / method | Tests |
|---|---|---|
| `PrepareMemberInit`, `PrepareListInit`, `PrepareIndex` with binding visitors for MemberAssignment, MemberMemberBinding, MemberListBinding, ElementInit | `ExpressionTransformer` | rows `MemberInit*`, `ListInit*`, `Index*` → `Rewrites`; binding order, `AddMethod`, `Indexer`, constructor identity preserved (`RootMetadata`); nested paths; writable `Index`/`Member` targets of assignments stay writable |
| Equality and hash for the same, including binding order | `ExpressionComparer` | metadata-distinction negative tests; large initializer collections |
| Explicit failure for unsupported binding shapes | rebuilders | `ArgumentException` tests |

### R4 — Special and extensible nodes

| Family | Policy proposal | Tests |
|---|---|---|
| Dynamic | arguments traversed; binder compared by reference only; never invoked | binder distinction test; no call into the binder |
| Extension | conservative pass-through; never call `Reduce`, `CanReduce`, `VisitChildren` or custom getters; hash `NodeType`/`Type` only | throwing `Reduce`, throwing/stateful getters, call counts |
| Quote | not entered (C-05); equality compares the quoted lambda structurally (already) | `ETC12_*` flipped |
| DebugInfo | leaf, ignored by equality like `Name` (debug metadata), preserved by rebuild | rebuild keeps document and lines |
| Custom `Expression` subclasses with real node types | treated by public class, never by runtime type | catalog variants |

## 10. Round history

| Round | Date | Result |
|---|---|---|
| R0 | 2026-10-27 | Inventory, executable baseline (117 samples × 6 contracts) and reproducers committed; ETC-01 … ETC-07 confirmed; ETC-08 … ETC-15 added; no production change, no benchmark needed. Probes (scratch, not committed): per-family probe harness linking the catalog, depth limits in isolated processes (§7). |

## 11. Decisions requested from the reviewer

1. C-01 … C-07 (table §4).
2. Whether R1a may ship before the rest of R1 (recommended: yes, it only removes corruption and exceptions).
3. Whether the `PrepareTransform`/`CopyExpression`/`EqualsCore`/`Hash` switches are converted to dictionaries now (AGENTS.md rule
   over 10 cases) or in a dedicated, benchmarked refactoring PR before R1b.
