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
| `tests/ExpressionResourceProbe/` | Isolated-process resource probe (stack, CPU, memory) and campaign script; raw results in `results/2026-10-27/` (evidence for ETC-16 … ETC-19, §7, §12). Not in `Utils.sln`, never run by the test suites. |

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
| Conditional | C `Test`, C `IfTrue`, C `IfFalse` | M `Type` | — |
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
**Perf**: `R1b`/`R1c` = measured in the R1b/R1c campaign (§9), `R2` = measured in the R2 campaign (§9), `R3` = measured in the R3
campaign (§9); the established scalar
families are also covered by the S5 benchmark corpora.

| Family (samples) | Recog. | Traversal | Reconstruction | Substitution | Equality | Hashing | Scope | Simplifier safety | Tests | Perf |
|---|---|---|---|---|---|---|---|---|---|---|
| Arithmetic, bitwise, comparison, logical binary (22 kinds) | ✅ | ✅ | ✅ `MakeBinary` | ✅ | ✅ (Add/Multiply commutative fallback) | ✅ | — | ✅ S3 guards | B | S5 |
| Coalesce; binary metadata: custom `Method`, lifted, lifted-to-null, `Coalesce` + `Conversion` | ✅ | ✅ (`Conversion` lambda not traversed) | ✅ | ✅ R2 (`Conversion` substituted, capture-avoiding) | ✅ (`Conversion` compared, lambda frame) | ✅ | ✅ R2 `Conversion` binds its parameter (lambda frame) | ✅ S3 | B, R2 | S5 |
| ArrayIndex | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | ⚠️ may throw | B | — |
| Assign and 14 compound assignments (incl. custom `Method`) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | ✅ R1b: observable, never rewritten as an operand (C-06) | B, F, R1b | R1b |
| Pre/Post Increment/Decrement Assign (incl. custom `Method`) | ✅ | ✅ | ✅ R1b (C-01) | ✅ R1b | ✅ | ✅ | — | ✅ R1b: observable (C-06) | B, F, R1b | R1b |
| Negate, NegateChecked, UnaryPlus, Not, OnesComplement, Increment, Decrement, IsTrue, IsFalse | ✅ | ✅ | ✅ R1b `MakeUnary` for every transformer (C-01) | ✅ R1b | ✅ | ✅ | — | ✅ S3 (`IsOrdinaryUnaryNegate`); custom `Method` observable, checked may throw (C-06) | B, F, R1b | S5, R1b |
| Convert, ConvertChecked, TypeAs, Unbox | ✅ | ✅ | ✅ R1b (C-01) | ✅ R1b | ✅ | ✅ | — | ✅ R1b: casts/unboxing may throw, boxing allocates, custom conversions observable (C-06) | B, F, R1b | — |
| ArrayLength | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | ✅ | B | — |
| Throw (typed), Rethrow | ✅ | ✅ `Rethrow` is a leaf since R1a (ETC-13 fixed) | ✅ R1b typed `Throw` keeps its type (C-01); `Rethrow` kept as is | ✅ R1b typed throw kept; `Rethrow` kept as is | ✅ | ✅ | — | ✅ R1b: observable, never rewritten as an operand (C-06) | B, F, R1b | — |
| Quote | ✅ | ✅ | ✅ | ⛔ quoted lambda not entered (kept explicitly in R2, C-05 pending) | ✅ | ✅ | ⚠️ captured outer parameters | ❌ policy ETC-12 (quoted data rewritten) | B, F, R2 | — |
| Constant | ✅ | — | ✅ | — | ✅ exact numeric model | ✅ | — | ✅ | B | S5 |
| Parameter | ✅ | — | — | ✅ (free) | ✅ binding-aware (R2: lambda, block, catch frames) | ✅ De Bruijn (R2: depth, position, binder kind) | ✅ R2 lambda, block, catch | ✅ | B, R2 | S5 |
| Default | ✅ R1b explicit leaf | — | ✅ (returned as is) | — | ✅ R1c (type) | ✅ R1c | — | ✅ inert | B, R1b, R1c | R1c |
| Call (static, instance receiver) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | ✅ R1b: only an explicit allow-list of total `Math`/`MathF`/`double`/`float` functions is inert; validating functions may throw; every other method is observable (C-06) | B, R1b | S5, R1b |
| MemberAccess (instance, static) | ✅ R1b `PrepareMember` | ✅ R1b receiver traversed (ETC-10 fixed) | ✅ R1b `MakeMemberAccess`, arity contract | ✅ R1b receiver substituted | ✅ | ✅ | — | ✅ R1b: getter observable; field read reads mutable state, may throw on a non-constant reference receiver | B, F, R1b | R1b |
| Invoke (lambda, delegate) | ✅ | ✅ | ✅ | ✅ R2 lambda target entered (capture-avoiding) | ✅ R1c (target, arguments) | ✅ R1c | — | ⚠️ R1a guard (ETC-11 guarded), R1b: substitution-safe families widened, R2: nested lambdas, blocks, catch, switch scanned (closure timing refused); the invocation itself is observable (C-06) | B, F, R1b, R1c, R2 | R1c, R2 |
| Lambda (incl. delegate type, name, tail call) | ✅ | ✅ (body via `TransformCore`) | ✅ R1b for every transformer (ETC-05 fixed, C-01) | ✅ R2 free occurrences, shadowing, capturing parameter renamed (ETC-03) | ✅ alpha-equivalence; R2 label scope per body | ✅ | ✅ parameters | ✅ S4 lexical scope; construction allocates a delegate (C-06) | B, F, R1b, R2 | S5 |
| Conditional | ✅ | ✅ | ✅ | ✅ | ✅ R1c (ETC-06 fixed for this family) | ✅ R1c (ETC-07 fixed) | — | ⚠️ branches rewritten independently; inert when its children are | B, F, R1c | R1c |
| TypeIs, TypeEqual | ✅ R1b | ✅ R1b `PrepareTypeBinary` | ✅ explicit copy (R1a, C-02) | ✅ R1b | ✅ R1c | ✅ R1c | — | ✅ inert when its operand is | B, F, R1a, R1b, R1c | R1c |
| New (ctor arguments, `Members`, value-type default) | ✅ R1b | ✅ R1b `PrepareNew` (arguments in order) | ✅ explicit copy keeps `Members` (R1a, C-02) | ✅ R1b | ✅ R1c (constructor, arguments, members) | ✅ R1c | — | ✅ R1b: constructor observable and allocating, never merged (C-06) | B, F, R1a, R1b, R1c | R1c |
| NewArrayInit | ✅ R1b | ✅ R1b `PrepareNewArray` (ETC-08 completed) | ✅ explicit copy (R1a, C-02) | ✅ R1b | ✅ R1c | ✅ R1c | — | ✅ R1b: allocates, never merged (C-06) | B, F, R1a, R1b, R1c | R1c |
| NewArrayBounds | ✅ R1b | ✅ R1b `PrepareNewArray` | ✅ explicit copy (R1a, C-02) | ✅ R1b | ✅ R1c | ✅ R1c | — | ✅ R1b: allocates, bounds may throw (C-06) | B, F, R1a, R1b, R1c | R1c |
| MemberInit, MemberAssignment / MemberMemberBinding / MemberListBinding | ✅ R3 `PrepareMemberInit` | ✅ R3 (creation, then bindings in declaration order, nested bindings depth first, list-binding arguments; H-4 not extended) | ✅ R3 constructor, members, binding kinds and order kept; C-02 flattened layout | ✅ R3 (capture-avoiding, bindings declare nothing) | ✅ R3 (creation, binding kind, member, order) | ✅ R3 | — | ✅ observable (C-06): constructions, setters, getters and `Add` calls never merged, dropped or reordered | B, F, R3 | R3 |
| ListInit, ElementInit (incl. explicit `AddMethod`) | ✅ R3 `PrepareListInit` | ✅ R3 (creation, initializers and arguments in order) | ✅ R3 exact `AddMethod` (never rediscovered), order kept; C-02 | ✅ R3 | ✅ R3 (`AddMethod`, order, arguments) | ✅ R3 | — | ✅ observable (C-06): every `Add` call kept, in order | B, F, R3 | R3 |
| Index (array, indexer) | ✅ R3 `PrepareIndex` | ✅ R3 (receiver, then arguments) | ✅ R3 same indexer or array access, type kept, writable targets stay writable; C-02 | ✅ R3 | ✅ R3 (indexer incl. null, receiver incl. null, arguments in order) | ✅ R3 | — | ✅ observable (C-06): accessors and array accesses (may throw) kept and ordered | B, F, R3 | R3 |
| Block (variables, typed, shadowing, DebugInfo, goto/label, runtime variables) | ✅ R2 `PrepareBlock` | ✅ R2 (variables first, statements through `PrepareExpression`; H-4 not extended) | ✅ R2 variables (identity), explicit type; C-02 copy since R1a | ✅ R2 (variables hide, capturing variable renamed) | ✅ R2 alpha-equivalence (block frame) | ✅ R2 | ✅ R2 comparer, keys and S4 (`OnEnterBlockScope`) | ✅ observable as a whole (C-06): never merged, dropped or reordered | B, F, R1a, R2 | R2 |
| Try (catch, catch variable + filter, finally, fault) | ✅ R2 `PrepareTry` | ✅ R2 (body, handlers: variable, filter, body; finally; fault) | ✅ R2 test types, variables, filters, finally vs fault, type; C-02 layout | ✅ R2 (catch variable hides, renamed on capture) | ✅ R2 (catch frame over filter and body only) | ✅ R2 | ✅ R2 comparer, keys and S4 (`OnEnterCatchScope`) | ✅ observable (C-06): finally and fault kept | B, F, R2 | R2 |
| Switch (incl. `Comparison`) | ✅ R2 `PrepareSwitch` | ✅ R2 (value, test values in order, bodies, default) | ✅ R2 comparison (or none), case and test order, type; C-02 layout | ✅ R2 | ✅ R2 | ✅ R2 | — | ✅ observable (C-06): tests and comparisons kept in order | B, F, R2 | R2 |
| Loop (break, continue) | ✅ R2 `PrepareLoop` | ✅ R2 | ✅ same label targets (R1a copy, R2 rebuild) | ✅ R2 | ✅ R2 (label bijection) | ✅ R2 (first-occurrence label index) | ✅ R2 labels per lambda body | ✅ observable (C-06): iteration count kept | B, F, R1a, R2 | R2 |
| Goto (return, goto), Label | ✅ R2 `PrepareGoto`, `PrepareLabel` | ✅ R2 (value, default value) | ✅ R2 kind, same target, type; C-02 | ✅ R2 (targets never cloned) | ✅ R2 (label bijection, forward jumps) | ✅ R2 | ✅ R2 labels per lambda body | ✅ observable (C-06) | B, R2 | R2 |
| RuntimeVariables | ✅ R2 `PrepareRuntimeVariables` | ✅ R2 references stay parameters | ✅ R2 (same instances, C-02: parameters only) | ✅ R2 by another parameter; otherwise `NotSupportedException` (C-07) | ✅ R2 references through the binding model | ✅ R2 | ✅ R2 references, not declarations | ✅ observable | B, R2 | — |
| DebugInfo | ✅ R2 explicit leaf | — | ✅ (returned as is) | — | ✅ R2 non-semantic (always equal) | ✅ R2 | — | ✅ | B, R2 | — |
| Dynamic | ⛔ | ⛔ opaque | ✅ (as is) | ⛔ | ⛔ | ⛔ | — | ✅ | B, F | — |
| Extension (reducible, irreducible) | ⛔ | ⛔ opaque (`Reduce`/`VisitChildren` never called) | ✅ (as is) | ⛔ | ⛔ | ⛔ (reads the virtual `NodeType`/`Type` getters only) | — | ✅ | B, F | — |

No family is reported as fully supported without a baseline row; every ❌ cell has a named reproducer.

## 3. Findings register

Severity: Critical = silent wrong result from a public entry point; High = exception or unusable output on valid input from a
public entry point, or wrong result inside a documented scope; Medium = contract gap or policy issue; Low = quirk.

| Id | Severity | Location | Minimal reproducer | Actual | Expected | Contracts | Round | Evidence | Status |
|---|---|---|---|---|---|---|---|---|---|
| ETC-01 | High (root cause of ETC-08/09/10) | `ExpressionTransformer.PrepareTransform`, `PrepareDefault` | `Simplify(MemberInit(...p*1...))` | 20 node types (Default, MemberAccess, TypeIs, TypeEqual, New, NewArrayInit, NewArrayBounds, MemberInit, ListInit, Index, Block, Try, Switch, Loop, Goto, Label, RuntimeVariables, DebugInfo, Dynamic, Extension) reach `PrepareDefault`: no child prepared, `FinalizeExpression(e, [])` | each family traversed, passed through explicitly, or rejected explicitly | G, S, D | R1–R4 | `ETC01_*`, baseline `Opaque`/`Throws`/`Corrupts` rows | Confirmed; **resolved by R1b for Default, MemberAccess, TypeIs, TypeEqual, New, NewArrayInit, NewArrayBounds** (traversed, C-03); **resolved by R2 for Block, Try, Switch, Loop, Goto, Label** (traversed) with explicit policies for RuntimeVariables and DebugInfo; 5 families remain for R3–R4 (MemberInit, ListInit, Index, Dynamic, Extension); **resolved by R3 for MemberInit, ListInit and Index** (traversed with their bindings and initializers): 2 families remain for R4 (Dynamic, Extension) |
| ETC-02 | High | `ExpressionTransformer.CopyExpression` | `CopyExpression(Block([v], …), children)` | Block loses variables and explicit type; Loop loses labels; New loses `Members`; 11 child-bearing node types (Lambda, MemberAccess, MemberInit, ListInit, Index, Try, Switch, Goto, Label, Dynamic, Extension) ignore the supplied children | rebuild preserves every M/D/L role of §1.2, or fails explicitly | G, D, every subclass using `CopyExpression` | R1 (scalar), R2 (Block/Loop), R3 | `ETC02_*` | Confirmed; explicit copies of Block, Loop, New, NewArray, TypeBinary fixed in R1a (C-02), MemberAccess in R1b (arity contract), Try, Switch, Goto, Label, RuntimeVariables and DebugInfo in R2 (positional layouts, named rebuilders); 6 ignoring families open (MemberInit, ListInit, Index, Dynamic, Extension, Lambda; R3–R4); **R3**: MemberInit, ListInit and Index follow the arity contract (flattened positional layouts, named rebuilders keeping members, binding kinds, `Add` methods and indexer, type change rejected); 3 ignoring families open (Dynamic, Extension, Lambda; R4) |
| ETC-03 | High | `ExpressionTransformer.ReplaceArgumentsCore` | `ReplaceArguments(Invoke(() => p), [p], [5])` | recurses into Unary, Binary, Invocation, MethodCall, Conditional only; rebuilds unary via `CopyExpression` (typed throw → void) | capture-avoiding substitution of free occurrences in every family (§5.3) | R, S (inlining) | R2 (+R1 for unary fidelity) | `ETC03_*`, baseline `Partial`/`Opaque` | Confirmed; **partly resolved by R1b**: faithful unary rebuild (typed throw kept) and substitution through MemberAccess, TypeBinary, New, NewArray; binders (nested lambda, Block, Catch, Switch, RuntimeVariables) stay for R2 — not globally resolved; **resolved by R2 for every family traversed up to R2**: free occurrences only, shadowing, capture avoidance by alpha-renaming (lambda, conversion lambda, block, catch), control flow with the same label targets, RuntimeVariables C-07; remaining: R3–R4 families (MemberInit, ListInit, Index, Dynamic, Extension) are not entered, Quote is not entered (C-05 pending); **extended by R3** to MemberInit, ListInit and Index (binding values, nested bindings, `Add` arguments, index receivers and arguments); remaining: Dynamic, Extension (R4) and Quote (C-05 pending) |
| ETC-04 | Medium | `CopyUnaryExpression`, `RebuildUnaryExpression` | `RecursiveTransformer.Transform(Negate(x, FortyTwo))` | G and D drop `Method` (value 42 → -3) and typed `Throw`'s `Type`; S preserves both | policy C-01 | G, D, R | R1 | `ETC04_*`, rows `Negate.CustomMethod`, `Convert.CustomMethod`, `PreIncrementAssign.CustomMethod`, `Throw` | **Fixed in R1b** (C-01: `MakeUnary` for every transformer, `CopyExpression` and substitution) |
| ETC-05 | Medium | `RebuildLambdaExpression` | `Transform(Lambda<CatalogDelegate>(…, "named", true, x))` | G and D rebuild `Func<double,double>`, no name, no tail call; S preserves | policy C-01 | G, D | R1 | `ETC05_*`, row `Lambda.Metadata` | **Fixed in R1b** (C-01: faithful lambda rebuild for every transformer) |
| ETC-06 | Medium (by design) | `ExpressionComparer.EqualsCore` | two identical `Condition(…)` | `Unequal` for every family outside Parameter/Constant/Lambda/Unary/Binary/Call/Member | structural equality with scope model, family by family | E | R1–R4 | `ETC06_*`, 32 `Unequal` rows | Confirmed; **resolved by R1c for Conditional, Invoke, TypeIs, TypeEqual, Default, New, NewArrayInit, NewArrayBounds** (C-04); remaining families R2–R4; **extended by R2 to Block, Try, Switch, Loop, Goto, Label, RuntimeVariables, DebugInfo** (binder frames, label bijection); remaining R3–R4; **extended by R3 to MemberInit, ListInit, Index** (bindings and initializers in declaration order, binding kinds, members, `Add` methods, indexers); remaining R4 (Dynamic, Extension) |
| ETC-07 | Low (by design) | `ExpressionComparer.Hash`, `HashUnsupported` | two different conditionals | identical hash (`NodeType`, `Type`) | hash extended together with equality | H | R1–R4 | `ETC07_*`, `Hashing_IsNeverInconsistentWithEquality` | Confirmed; **resolved by R1c for the same families** (hash mirrors equality); remaining families keep the coarse hash; **extended by R2** to the same families (binder kind in bound-parameter hashes, first-occurrence label indices); **extended by R3** to MemberInit, ListInit, Index (same order and metadata as equality) |
| ETC-08 | **Critical** | `PrepareDefault` + `ExpressionSimplifier.FinalizeExpression` → `CopyExpression(e, [])` | `Simplify(NewArrayInit(p*1, p*1))` | returns `new double[0]`; any `Block` becomes an empty `void` block (statements, variables, result lost) | traversal or pass-through, never a rebuild from an empty child list | S, D, G | R1a | `ETC08_*`, rows `NewArrayInit`, `Block*` | **Fixed in R1a** (pass-through) |
| ETC-09 | High | same path | `Simplify(TypeIs(…))`, `Simplify(New(ctor, p))`, `GetHashCode(TypeIs(…))` | `IndexOutOfRangeException` (TypeIs, TypeEqual, Loop), `ArgumentException` (New with arguments or members, NewArrayBounds), `ArgumentNullException` (value-type `New`); the public comparer throws too, so such trees cannot enter a `HashSet<Expression>` while `Equals(x, x)` is `true` | no exception on valid trees | S, D, G, E, H | R1a | `ETC09_*`, 7 `Throws` rows | **Fixed in R1a** (pass-through, hashable) |
| ETC-10 | Medium | `PrepareTransform` (MemberExpression → `PrepareDefault`) | `Simplify(TimeSpan.FromSeconds(p*1).TotalSeconds)` | same instance; receiver never simplified although the comparer and P15 treat Member as supported | receiver traversed and rebuilt with `Expression.MakeMemberAccess` | G, S, D, R | R1 | `ETC10_*`, row `MemberAccess` | **Fixed in R1b** (`PrepareMember`, faithful rebuild, substitution) |
| ETC-11 | High | `ExpressionSimplifier.InvokeExpression` + ETC-03 | `Simplify(x => ((q) => () => q)(x)())` | inlined body keeps `q` unbound; `Compile()` throws `InvalidOperationException`; same with a member-access body | capture-avoiding inlining or no inlining | S, D | R2 (guard in R1a) | `ETC11_*` | **Guarded in R1a** (no unbound parameter); capture-avoiding inlining open for R2; **R2**: substitution is capture-avoiding and inlining scans nested lambdas, blocks, catch handlers and switches; a variable argument read inside a nested closure, or after an effect, still keeps the invocation (conservative, documented) |
| ETC-12 | Medium (policy) | `PrepareUnary` for `Quote` | `Simplify(Quote(x => x * 1))` | quoted lambda body rewritten to `x` | policy C-05 | S, D, G | R4 | `ETC12_*`, rows `Quote*` | Confirmed |
| ETC-13 | Medium | `PrepareUnary` → `PrepareExpression(null)`; `ReplaceArgumentsCore` | `Transform(Rethrow(typeof(double)))` | G and D throw `NullReferenceException`; substitution returns a `void` throw | null operand passed through | G, D, R | R1a | `ETC13_*`, row `Throw.Rethrow` | **Fixed in R1a** |
| ETC-14 | Medium | `ExpressionCanonicalOrder.Build` (`UnsupportedKey`) | `Compare(Conditional, Default)` | 0 for every unsupported pair | each family gaining equality also gains a canonical key, before additive/multiplicative grouping can merge it | S4 | with each equality extension | `ETC14_*` | Confirmed; **resolved by R1c for the R1c families** (dedicated keys, antisymmetric, consistent with equality); remaining families keep `UnsupportedKey`; **extended by R2** (`StructureKey` with binder frames and normalized label indices); **extended by R3** (`StructureKey` with member metadata: binding members and kinds, indexers, `Add` methods; ranks 21–23, unsupported 24) |
| ETC-15 | High (within S3) | algebraic rules on Unary/Binary operands; `IsOrdinaryBinaryArithmetic` does not look at operands | `Simplify(++v + ++v)`, `((v = 5) * 0) + v`, `++v + v` | `2 * ++v` (3 → 2), `v` (5 → 0), `v + ++v` (2 → 1) | assignment, increment and throw nodes are opaque operands for symbolic rules (C-06) | S, D | R1 | `ETC15_*` | **Resolved by R1b for every algebraic rule and canonicalization of `ExpressionSimplifier`** (C-06, strict boundary chosen by the reviewer on 2026-10-29): the rules of a node run only when its operands are inert, and the additive/multiplicative canonicalizations pin the non-inert terms; limits in §9 R1b result. **R2**: the binding and control-flow families stay observable as a whole (never merged, dropped or reordered, tested with traces); **R3**: the initializer and index families stay observable as a whole although they are now compared structurally (tested with traces: equal indexer reads, out-of-range array accesses, equal initializations, index writes, canonical order of observable terms) |
| ETC-16 | **Critical (security)** | `Utils.Parser` `CompiledGrammar.Parse`, reached by `CSyntaxTokenParser.Parse`, `VBSyntaxTokenParser.Parse` and every `CompileExpression(string, …)` | `new CSyntaxExpressionCompiler().CompileExpression("((((((((((((((((((((x0))))))))))))))))))))")` | stack overflow terminates the process at 20 parenthesis pairs (CSyntax) / 18 (VBSyntax) on a default Windows thread, 2–3 on a 256 KiB thread, 16 on Linux 1 MiB, 150 on Linux 8 MB; about 50 KB of stack per nesting level; no depth limit, no guard | controlled exception for excessive nesting, configurable limit | public parsers of `Utils.Expressions.*`, `Utils.Parser.Expressions` (inherits), possibly every `Utils.Parser` grammar | dedicated `Utils.Parser` stage (S-06) | probe `E-*-parens`, `E-*-nested`; `results/2026-10-27/win-parser-nesting.csv`, `win-vb-nesting.csv`, `*-parser.csv` | Confirmed (out of ETC code scope, not fixed here) |
| ETC-17 | High (security, availability) | `ExpressionSimplifier` rule/comparer interplay (additive canonicalization, factoring probes, comparer re-simplification) | `Simplify(x0 + x1 + … + x99)` (100 distinct parameters) | 1.5 s, 397k visits, 684k comparer re-simplifications, 2.5 GB allocated; 250 terms > 60 s; balanced 1,000 terms 30 s / 31 GB; F-g 20 terms (79 nodes) 56 s / 27.6 GB | bounded work with a controlled refusal (work budget); algorithmic improvement is a separate S5 continuation | S, D, E, H (public comparer simplifies) | R1a-Security (budget); S5 follow-up (algorithm) | `results/2026-10-27/win-simplify.csv`, `win-comparer.csv`, `win-combinatorial.csv`, `win-work-counters.csv` | **Bounded in R1a-Security** (visit budgets; the algorithmic cost itself stays for S5) |
| ETC-18 | Medium (security) | `Utils.Parser` engine | CSyntax/VBSyntax parse of `x0 + … + x19999` (169 KB) | 6.2 s / 5.2 GB (CSyntax parse), 12.1 s / 4.7 GB (VBSyntax compile): about 30 KB allocated per input character, linear | allocation proportional to a documented, small constant; input-length guidance | parsers | `Utils.Parser` stage (S-06) | `win-parser.csv`, `win-vb-sum.csv` | Confirmed (out of ETC code scope) |
| ETC-19 | High (security) | every recursive path: `TransformCore`/`Prepare*`, `ReplaceArgumentsCore`, `ExpressionComparer.EqualsCore`/`Hash`, `ExpressionCanonicalOrder.Build` | left sum of 3,000 terms through `ReplaceArguments`; `Abs` chain of 2,000 through the canonical order | uncatchable stack overflow at about 2,000–5,000 levels on 1 MB stacks, from 250 on 256 KiB stacks; no `EnsureSufficientExecutionStack` anywhere | controlled `ExpressionResourceLimitException` (depth limit + stack guard) | G, S, D, R, E, H, canonical order | R1a-Security | §7.2, §7.5; `win-structural.csv`, `win-*-structural.csv`, `linux-*-structural.csv` | **Fixed in R1a-Security** for the ETC engine (depth limit, stack guards; §9 R1a-Security result); the parser remains (ETC-16) |

## 4. Compatibility register

Every entry needs an explicit decision before its round changes production behavior. "Recommendation" is the R0 proposal.

| Id | Behavior | Affected surface | Options | Recommendation | Decision |
|---|---|---|---|---|---|
| C-01 | Unary `Method`/`Type` and lambda metadata loss for generic transformers and derived simplifiers (ETC-04/05) | public `ExpressionTransformer` subclasses, derived `ExpressionSimplifier`s | (a) keep historical loss; (b) make the faithful rebuild the base default; (c) faithful for derived simplifiers only | (b): the loss changes execution semantics (custom operators, typed throws) and no test documents it as intended; record as a 2.0.0 behavior change | **(b) adopted in R1b** (2.0.0 behavior change): `MakeUnary` and faithful lambda rebuild for every transformer, `CopyExpression` and substitution; the exact-type gates of the simplifier overrides are removed (H-5) |
| C-02 | `CopyExpression` (protected static) with a child list that does not match the node | every subclass finalizing with `CopyExpression` | (a) keep silent corruption; (b) return `e` when the list is empty and the family has children; (c) throw `ArgumentException` on arity mismatch | (b) for an empty list (exact pass-through, fixes ETC-08/09 for every subclass), (c) otherwise | **Adopted in R1a** for New, NewArrayInit, NewArrayBounds, TypeIs, TypeEqual, Block, Loop and rethrow (§9 R1a); **extended in R3** to MemberInit, ListInit and Index: flattened positional layouts, members, binding kinds, `Add` methods and indexer taken from the node, a creation that is not a `NewExpression` of the same type, a value or argument that does not fit, or a type change rejected with `ArgumentException` |
| C-03 | Families newly traversed (`Prepare*` added) | subclass rules declared on those node types now receive positional children; `FinalizeExpression` receives non-empty `parameters`; `PrepareExpression` is called on more children (call count/order observable, H-1, H-2) | (a) traverse for every subclass; (b) traverse for the exact simplifier only | (a), announced in the changelog; keep the lambda body asymmetry (H-4) | **(a) adopted in R1b** for Member, TypeBinary, New, NewArray (Default is a leaf); lambda body asymmetry (H-4) kept and not extended; **(a) extended in R2** to Block, Try, Switch, Loop, Goto, Label (positional layouts documented on each `Prepare*`), RuntimeVariables (references stay parameters) and DebugInfo (leaf); block and catch variables follow the lambda-parameter declaration policy; block, catch and switch bodies go through `PrepareExpression` (H-4 still not extended); **(a) extended in R3** to MemberInit, ListInit and Index (flattened layouts documented on `PrepareMemberInit`, `PrepareListInit`, `PrepareIndex`; the creation follows the declaration policy: it must stay a `NewExpression`, `InvalidCastException` otherwise; H-4 still not extended) |
| C-04 | Public comparer: more `Equal` results (ETC-06) | external `Dictionary`/`HashSet<Expression>` users | per family, after reconstruction is fixed | adopt per family with alpha-equivalence; never before ETC-08 is fixed (otherwise corrupted trees compare equal: two different arrays both become `new double[0]`) | **Adopted in R1c** for Conditional, Invoke, TypeIs, TypeEqual, Default, New, NewArrayInit, NewArrayBounds (alpha-equivalence through the existing binding context); **extended in R2** to Block, Try, Switch, Loop, Goto, Label, RuntimeVariables and DebugInfo (binder frames for block and catch variables, label bijection per lambda body, debug information non-semantic); **extended in R3** to MemberInit, ListInit and Index (bindings and initializers in declaration order; structural equality still never makes two initializations interchangeable) |
| C-05 | Quoted expressions (ETC-12) | `Simplify` callers building query providers | (a) keep rewriting; (b) never enter `Quote`; (c) enter only for the exact simplifier with an option | (b): a quoted tree is data; rewriting it is not part of the symbolic contract | pending |
| C-06 | Side-effecting operands (ETC-15) | `Simplify` results for trees containing assignments | (a) keep S3 purity assumption; (b) treat assignment/increment/throw nodes (and any subtree containing them) as opaque for algebraic rules | (b): cheap local check like the custom-operator guards; generalized purity analysis stays out of scope | **Adopted in R1b with a strict boundary** chosen by the reviewer on 2026-10-29 ("option 4"): every subtree not proven inert (writes, throws, user methods outside an explicit allow-list, getters, custom operators, constructors, allocations, invocations, dynamic/extension nodes, checked arithmetic, integral/decimal division, decimal arithmetic, array access, casts) is opaque to the rewrites that drop, repeat, merge or move evaluations; structural equality, keys and memos are unchanged (§9 R1b result) |
| C-07 | Exceptions instead of corruption for families not yet supported | `Simplify`, comparer | (a) pass through unchanged; (b) throw `NotSupportedException` | (a) pass-through for the simplifier and comparer (matches the conservative policy), explicit `NotSupportedException` only for substitution into `RuntimeVariables` | **(a) adopted in R1a** for the simplifier and the comparer; the `RuntimeVariables` part belongs to R2; **adopted in R2**: substituting a runtime variable by a non-parameter throws `NotSupportedException`, a parameter replacement is applied; **R3**: a `MemberBinding` of a class other than the three framework classes (only constructible through the obsolete protected constructor, and refused by `Expression.MemberInit` itself) is rejected with `NotSupportedException` instead of being dropped |

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

### 7.1 Protocol and environments

Harness: `tests/ExpressionResourceProbe` (one scenario and one operation per process, see its README); campaigns driven by
`run-campaign.ps1` (ascending sizes 100, 250, 500, 1,000, 2,000, 3,000, 5,000, then 10,000+ only after success; a
scenario/operation pair stops at its first failure; 60 s timeout; 4 GB working-set limit on Windows). Raw data:
`tests/ExpressionResourceProbe/results/2026-10-27/`. Every point was measured **once**; timings are indicative (A/A noise on
this machine is about ±6 %), thresholds are the reliable part. The timings of `equals`, `rawequals` and `canonical` include
building the second, independent tree (for the parser families: compiling the source again), so they overstate the
comparison itself.

| Environment | Details |
|---|---|
| Windows | Windows 11 Pro 10.0.26300, x64, .NET 8.0.33 runtime (`net8.0` harness, Release); main thread 1 MB; thread-pool threads behave like the main thread (same guarded frame count); explicit 256 KiB threads measured too |
| Linux | WSL2 Ubuntu, kernel 6.18.54.1-microsoft-standard-WSL2, x64, self-contained linux-x64 publish of the same harness (.NET 8.0.33); `ulimit -s` 8,192 KiB: main and thread-pool threads get about 8 MB; explicit 1 MiB threads measured too. No memory limit applied on Linux. Bare-metal Linux not available: WSL2 results are not extrapolated to other distributions or container stack limits. |

`EnsureSufficientExecutionStack` calibration (`ess` mode, fixed ~230-byte frames): Windows main/pool 3,738 / 3,746 guarded
frames, 256 KiB thread 334; Linux main/pool 23,433 / 23,441, 1.5 MiB thread 4,080, 256 KiB thread 356. The guard always threw
`InsufficientExecutionStackException` before an overflow, and a second guarded descent on the same thread reached the same
depth (the stack fully unwound). Cost: 0.7 ns per call on Windows, 4–6 ns on Linux (measured under load).

### 7.2 First failures by tree shape (`size` = number of terms; tree depth in parentheses when it differs)

Windows, main thread (1 MB). "—" = no failure up to the largest size tried (20,000 unless stated); "n/r" = not run.

| Family | transform (generic `ExpressionTransformer`) | substitute (`ReplaceArguments`) | simplify | rawhash | rawequals | canonical |
|---|---|---|---|---|---|---|
| A left sum | 5,000 | 3,000 | timeout at 250 (1.5 s at 100) | 10,000 | 5,000 | 10,000 |
| B right sum | 5,000 | 3,000 | timeout at 250 | 10,000 | 5,000 | 10,000 |
| C balanced sum (depth 16 at 20,000) | — | — | timeout at 2,000 (30 s, 31 GB allocated at 1,000) | — | — | — |
| D powers `Σ c·x^k` | 5,000 | 3,000 | timeout at 250 (11.7 s at 100) | 10,000 | 5,000 | 10,000 |
| D products, unshared (25 M nodes at 5,000) | 5,000 | 3,000 | n/r | 5,000 | 5,000 | memory limit at 5,000 |
| D products, shared DAG (397 distinct / 10,099 logical nodes at 100) | 5,000 | 3,000 | timeout at 250 | 5,000 | 5,000 | 10,000 (5.3 s at 5,000) |
| D Horner (depth 2n) | 2,000 (3,999) | 2,000 (3,999) | timeout at 250 (17 s at 100) | 3,000 | 3,000 | 3,000 |
| D repeated `x + x + …` | 5,000 | 3,000 | 5,000 (overflow; 10 ms at 3,000) | 10,000 | 5,000 | 10,000 |
| E CSyntax flat sum | 5,000 | 3,000 | timeout at 250 | 10,000 | 5,000 | 10,000 |
| G Negate chain | 5,000 | 3,000 | 5,000 | 10,000 | 10,000 | 10,000 |
| G Conditional chain | 5,000 | 3,000 | 5,000 | — (unsupported node) | — | — |
| G `Math.Abs` chain | 3,000 | 3,000 | 3,000 | 5,000 | 5,000 | **2,000** |
| G Invoke of nested lambdas (depth 2n) | 2,000 (4,001) | — (lambdas not entered, ETC-03) | 2,000 | — (unsupported) | — | — |

All failures above are uncatchable stack overflows (exit `0xC00000FD`), except "timeout" and "memory limit". The public
`ExpressionComparer.GetHashCode`/`Equals` follow `simplify` (they simplify first): left sum 100 terms costs 1.5 s / 2.8 s and
2.5 / 5.1 GB allocated; balanced sum 1,000 terms costs 30 s / 55 s.

Other stacks (subset):

| Path | Windows 256 KiB thread | Linux 1 MiB thread | Linux main (8 MB) |
|---|---|---|---|
| transform, left sum / Negate / `Abs` / Invoke | 1,000 / 1,000 / 500 / n/r | 3,000 / 3,000 / 2,000 / 1,000 | 20,000 / 30,000 / 20,000 / 10,000 |
| substitute, left sum / Negate / `Abs` / Horner | 500 / 500 / 500 / n/r | 2,000 / 2,000 / 2,000 / 1,000 | 20,000 / 20,000 / 20,000 / 10,000 |
| simplify, Negate / `Abs` / Invoke | 1,000 / 500 / n/r | 3,000 / 2,000 / 1,000 | 30,000 / 20,000 / 10,000 |
| rawhash, left sum / Negate / `Abs` | 1,000 / 2,000 / 1,000 | 5,000 / 10,000 / 5,000 | 50,000 / 50,000 / 30,000 |
| canonical, `Abs` chain | **250** | 2,000 | 10,000 |

Observations:

- The worst first failure on a 1 MB stack is around **2,000 nesting levels** (canonical order of nested calls; Invoke inside
  the transformer and simplifier; Horner substitution on Linux 1 MiB); on a 256 KiB stack it is **250**.
- Frames are larger on Linux than on Windows for the same stack size (Linux 1 MiB fails earlier than Windows 1 MB).
- Two families survive only because the node is not traversed (Conditional and Invoke in hashing; Invoke in substitution):
  R1/R2 will remove these accidental stops.
- A balanced tree has logarithmic depth and never overflows, but its simplification cost still explodes (C row).

### 7.3 Parsers (`Utils.Expressions.CSyntax`, `Utils.Expressions.VBSyntax`)

Both front ends use the shared `Utils.Parser` `CompiledGrammar` engine (`CSyntaxTokenParser.Parse`, `VBSyntaxTokenParser.Parse`,
reached by every `CompileExpression(string, …)` overload).

| Input | CSyntax first overflow | VBSyntax first overflow |
|---|---|---|
| `((…(x0)…))`, n pairs — Windows main / pool | 20 (ok at 19) | 18 (ok at 16) |
| `x0 + (x1 + (…))`, n levels — Windows main | 20 | 18 |
| `((…(x0)…))` — Windows 256 KiB thread | 3 | 2 |
| `((…(x0)…))` — Linux 1 MiB thread | 16 (ok at 10) | 16 (ok at 10) |
| `((…(x0)…))` — Linux main (8 MB) | 150 (ok at 100) | 150 (ok at 100) |

A 42-character CSyntax input (`((((((((((((((((((((x0))))))))))))))))))))`) or a 38-character VBSyntax input terminates the
process on a default Windows thread. Flat sums are linear and never overflow up to 20,000 terms, but allocate about 30 KB per
input character (CSyntax parse of a 169 KB text: 6.2 s, 5.2 GB allocated; VBSyntax compile: 12.1 s, 4.7 GB).
`Utils.Parser.Expressions` delegates to a caller-selected `IExpressionCompiler` and inherits the exposure. Other grammars
built on `Utils.Parser` were not measured.

### 7.4 Work explosion (family F and ordinary sums)

Instrumented build (counter only, `results/2026-10-27/experiment-not-merged-guards-and-counters.patch`), `TransformCore`
visits per public `Simplify` call, nested comparer simplifications included:

| Scenario | Nodes / depth | Visits | Comparer re-simplifications (max nesting) | Time | Allocated |
|---|---|---|---|---|---|
| A left sum, 25 / 50 / 100 terms | 49 / 99 / 199 nodes | 17,423 / 70,973 / 396,823 | 16,284 / 95,484 / 683,884 (8) | 44 ms / 0.29 s / 1.46 s | 18 MB / 188 MB / 2.5 GB |
| C balanced, 100 / 250 / 500 terms | depth 8–10 | 28,695 / 108,143 / 342,851 | 31,164 / 146,268 / 542,660 (8) | 0.12 / 0.58 / 2.3 s | 58 MB / 551 MB / 4.0 GB |
| D Horner, 25 / 50 terms | 97 / 197 nodes | 85,515 / 314,615 | 64,894 / 321,844 (7) | 0.28 / 1.2 s | 80 / 624 MB |
| F-p (S5 `p`), 8 / 16 / 32 terms | depth 9 / 17 / 33 | 10,728 / 57,888 / 188,432 | 5,376 / 30,664 / 115,928 (8) | 21 / 100 / 536 ms | 5 / 30 / 140 MB |
| F-g (equal coefficients), 10 / 14 / 18 / 20 terms | depth 11–21, ≤ 79 nodes | 8,597 / 154,906 / 5,574,955 / — | 5,710 / 143,652 / 5,386,038 (14) | 13 ms / 0.41 s / 5.8 s / **56 s** | 4 MB / 108 MB / 4.3 GB / **27.6 GB** |
| F-custom (custom `*`), 10 / 14 / 16 terms | depth ≤ 17 | 67,165 / 3,518,623 / — | 18,680 / 1,275,008 (12) | 0.11 / 2.4 / **27 s** | 31 MB / 2.0 GB / 16.6 GB |
| F-opaque (S5 `z`), 10 / 16 / 20 terms | depth ≤ 22 | 95,049 / 1,372,812 / — | 43,458 / 991,350 (8) | 0.17 / 1.6 / **26 s** | 44 MB / 974 MB / 14.5 GB |
| G chains (Negate 3,000; `Abs` 2,000) | depth 3,001 / 2,001 | = node count | 0 | ≤ 7 ms | < 1 MB |

A tree of depth 21 and 79 nodes (F-g, 20 terms) costs 56 s and 27.6 GB of allocations: **a depth or node limit cannot protect
against work explosion**. Visits track time closely (about 1–4 µs per visit, about 0.8–6.5 KB allocated per visit), so a single
per-call visit counter is a usable work budget.

Reference workload (`results/2026-10-27/unit-suite-visit-histogram.txt`): the 1,573 `UtilsTest.Mathematics`/`UtilsTest.Expressions`
unit tests make 23,609 outermost simplifier calls; 95.4 % use at most 10 visits, 99.4 % at most 10,000, 53 calls between 10^4
and 10^5, and 3 more (the largest, 353,625 visits, is an S5 opaque-chain regression test).

### 7.5 Stack-guard experiment (never merged)

Same patch, `ETC_GUARDS=1`: `EnsureSufficientExecutionStack` at the entry of `TransformCore`, `ReplaceArgumentsCore`,
`ExpressionComparer.EqualsCore`, `ExpressionComparer.Hash` and `ExpressionCanonicalOrder.Build`.

- Every previously crashing case tried (Negate 5,000 and 20,000, `Abs` 3,000 and 5,000, Invoke 2,000; simplify, rawhash,
  transform) ended with a controlled `InsufficientExecutionStackException`.
- After the exception, on the same thread: `_lexicalScopeStack` null, comparison-memo depth 0 / inactive / no memo, no
  observed memo, metrics scope depth 0 / no cache, comparer simplification depth 0, no diagnostic list; a following ordinary
  simplification returned the expected tree and compared equal to a fresh one. The existing `finally` blocks are sufficient
  for the exception paths exercised; this is not a proof for every path.
- Overhead (paired, 7–9 rounds): −0.1 % to +1.2 % on simplify/transform/hash workloads, within the ±6 % A/A noise.
- Limits: the guard keeps a fixed headroom; any non-guarded recursive segment deeper than that headroom (framework expression
  factories, user `Equals`/`GetHashCode`/extension code, the parser engine, P15 key hashing) can still overflow. It does not
  bound CPU or memory.

### 7.6 Denial-of-service assessment

| Vector | Input size | Effect | Protection needed |
|---|---|---|---|
| Parser nesting (ETC-16) | 38–42 characters | process termination | parser nesting limit + stack guard in `Utils.Parser` (separate stage) |
| Deep expression tree, any public API (ETC-19) | 2,000 levels (1 MB), 250 (256 KiB) | process termination | input depth limit + stack guards |
| Simplification work (ETC-17) | 100–1,000 terms, or 20 terms (F) | seconds to minutes of CPU, tens of GB allocated | work budget |
| Parser allocations (ETC-18) | 100 KB of text | several GB allocated | input length / work limit in the parser stage |

## 8. Performance plan

R0 has no production change, hence no benchmark. From R1 on, every round uses the S5 protocol (Release, `net8.0` harness,
separate worktrees for baseline and candidate, paired runs, A/A control, counters only in instrumented copies) and reports:
traversal cost and allocations on the S5 scalar families (must stay within +5 % or be justified and approved), comparer and hash
CPU, rule dispatch counts, P3/P9/P15 hit/store counts and P16 regression families (p, q, i, h, g, t, x, D, R, z, k, n, e, o, F),
plus new families (deep/wide trees per new node kind). The `PrepareTransform` dispatch change (R1) needs a dedicated
micro-benchmark because it sits on the hot path; prefer strongly typed `Prepare*` methods without visitor allocations.

## 9. Correction rounds

Order (§12.9): R1a, then **R1a-Security** (§12), then R1b, R1c, R2–R4. Ordering constraints: (1) fix corruption and
exceptions (R1a) before extending equality (C-04), otherwise corrupted trees compare equal; (2) stack guards and budgets
(R1a-Security) before any structural extension that deepens recursion; (3) every equality extension ships its hash, its
canonical key (ETC-14) and its guards in the same PR; (4) scope model (R2) before substitution or equality for any binder.

### R1a — Stop corruption and exceptions (small PR, first)

| Change | File / method | Tests |
|---|---|---|
| Empty child list ⇒ pass-through; arity mismatch ⇒ `ArgumentException` (C-02) | `ExpressionTransformer.CopyExpression` | flip rows `NewArrayInit`, `NewArrayBounds`, `New*`, `TypeIs`, `TypeEqual`, `Loop`, `Block*` (G, S, D, E, H) to `Opaque`/`Unequal`/`Consistent`; `ETC08_*`, `ETC09_*` replaced by "unchanged instance" and "no exception" assertions; derived transformer with custom `FinalizeExpression` unaffected |
| Null operand passes through | `ExpressionTransformer.PrepareUnary` (or base `PrepareExpression` null short-circuit for every runtime type), `ReplaceArgumentsCore` | `ETC13_*`, row `Throw.Rethrow` |
| Inlining guard: do not inline when the lambda body contains a family `ReplaceArgumentsCore` does not enter | `ExpressionSimplifier.InvokeExpression` | `ETC11_*` compile and evaluate to 3 |
| Docs | this roadmap, `ExpressionComparer` remarks | — |

#### R1a result (2026-10-28)

- Baseline: `master` at `4ebb4b835b85c5e8d1afcdbd4fdbcd941fec5184` (ETC R0 merged, PR #669). Final SHA: the head of the R1a
  pull request (branch `etc/r1a-corruption-fixes`).
- Scope kept to R1a: no new traversal (`Prepare*`), no comparer or canonical-order change, no algebraic rule change, no
  resource budget. ETC-01, ETC-02 (beyond the explicit copies below), ETC-03, ETC-06, ETC-07, ETC-10, ETC-12, ETC-14 and
  ETC-15 stay open.

**Production methods changed**

| Method | Change |
|---|---|
| `ExpressionTransformer.TransformContext` | private sentinel `OpaqueChildren`, `IsOpaque`, `Opaque(e)`; `MaterializeExpressionParameters` never lets the sentinel escape (rules and `FinalizeExpression` keep receiving `Array.Empty`) |
| `ExpressionTransformer.PrepareDefault` | returns `TransformContext.Opaque(e)`: an untraversed node is marked explicitly instead of being indistinguishable from a childless supported node |
| `ExpressionTransformer.TransformPrepared` | unchanged dispatch: an untraversed node is still finalized by `FinalizeExpression(e, [])`, so every subclass observes the same calls |
| `ExpressionTransformer.PrepareUnary` | a rethrow (`Operand == null`) gets the lazy unary context without `PrepareExpression(null)` and without rebuild |
| `ExpressionTransformer.ReplaceArgumentsCore` | a rethrow is returned unchanged |
| `ExpressionTransformer.CopyExpression` (+ `KeepsUntraversedNode`, `CopyNewExpression`, `CopyNewArrayExpression`, `CopyTypeBinaryExpression`, `CopyBlockExpression`, `CopyLoopExpression`) | C-02 arity contract for New, NewArrayInit, NewArrayBounds, TypeIs, TypeEqual, Block and Loop; faithful explicit copies (`Members`, block variables and type, loop labels); rethrow kept |
| `ExpressionSimplifier.InvokeExpression` | inlines only when `InvocationInliningAnalysis` allows it; otherwise returns the prepared invocation without another `TransformCore` |
| `InvocationInliningAnalysis` (new, internal) | conservative ETC-11 guard, described below |

**C-02 contract (adopted).** For the seven families above, `CopyExpression(e, [])` returns `e` (either the child list of an
untraversed node or a node with nothing to replace: both answers are the same node), a list of the node's arity rebuilds it
with its metadata, any other length throws `ArgumentException`. Every other family keeps its historical behavior (no
universal "empty means opaque" rule). Compatibility: a third-party subclass finalizing with `CopyExpression` now receives the
original node instead of a corrupted node or an exception (H-6), and an explicit copy of a block, loop or construction keeps
variables, labels and `Members` (R0 pinned the losses in `ETC02_*`; those three tests now assert preservation).

**C-07 (adopted, option a).** Untraversed families pass through unchanged in the simplifier and the comparer; nothing throws
`NotSupportedException` in R1a.

**ETC-11 guard.** `InvocationInliningAnalysis` scans the lambda body in evaluation order over exactly the families
`ReplaceArgumentsCore` enters (parameter, unary, binary, call, invocation, conditional). Inlining is refused when: a parameter
is mentioned (referenced or redeclared, i.e. shadowed) inside any other node or inside a binary `Conversion`; an extension
node is present (never reduced); a parameter is an assignment target or a by-reference argument; an argument is anything but a
constant, a default or a parameter read; a parameter-read argument is used after a node that may write a variable or run user
code (call, invocation, custom operator, assignment, untraversed node); the body type differs from the invocation type; an
argument type differs from its parameter type, or a parameter is by-reference; substitution would rebuild a unary node whose
`Method` or typed-throw `Type` the historical factories drop (ETC-04). A body that reads no parameter is inlined without
substitution. Refused invocations are returned as prepared. Consequence: an invocation whose argument is a non-trivial
expression is no longer inlined (its evaluation count and order are preserved instead); capture-avoiding inlining is R2.

**ETC-13.** A typed or `void` rethrow is a leaf for every path: generic transformer, exact and derived simplifiers,
`ReplaceArguments`, `CopyExpression`. The historical `[null]` child list seen by rules and `FinalizeExpression` is kept
(`ExpressionTransformerUnaryLazyParametersTests.Rethrow_NullOperand_FinalizeReceivesArrayContainingNull` unchanged).

**Rejected alternatives**

- *Exact-simplifier shortcut returning an opaque node without calling `FinalizeExpression`* (an internal
  `FinalizeOpaqueExpression` hook overridden for the exact type). It removed one `NodeType` read per untraversed node, which
  changed the user-code getter reads pinned by `ExpressionSimplifierOpaqueConstantInvariantTests` (S5 P15: 158 → 126 reads,
  throwing read moved). The pass-through therefore goes through the unchanged `FinalizeExpression` → `CopyExpression` path.
- *Rethrow as an empty-array context.* It changed the `[null]` child list pinned for subclasses; the lazy unary context keeps it.
- *Inlining non-trivial arguments when used exactly once.* Proving evaluation order against every preceding effect is R2's
  substitution work; refused as unsafe for R1a.
- *Faithful `MakeUnary` rebuild in `ReplaceArgumentsCore`.* That is C-01 (R1b); R1a refuses the substitution instead.

**Matrix cells changed** (`ExpressionTreeCoverageBaselineTests`, columns G | S | D | R | E | H): `TypeIs`, `TypeEqual`, `New`,
`New.Members`, `NewArrayBounds`, `Loop`: `Throws:*` → `Opaque | Opaque | Opaque | Opaque | Unequal | Consistent`;
`NewArrayInit`, `Block`, `Block.TypedResult`, `Block.Shadowing`, `Block.DebugInfo`, `Block.GotoLabel`,
`Block.RuntimeVariables`: `Corrupts` → `Opaque`; `New.ValueTypeDefault`: `Throws:ArgumentNullException` → `Same` in every
column (`Unequal` in E); `Throw.Rethrow`: `Throws:NullReferenceException | Rebuilt | Throws:NullReferenceException | Corrupts`
→ `Same | Same | Same | Same`. These families are **passed through, not supported**: their children are still not traversed.
No other row changed (no change for supported families).

**Derived transformers.** Third-party `ExpressionTransformer` subclasses see the same `PrepareExpression` and
`FinalizeExpression` calls as before, except that `PrepareExpression` is no longer called with `null` for a rethrow. Their
rules on untraversed node types still run, and their `FinalizeExpression` result is still used
(`GenericTransformer_RulesAndFinalizeOverride_StillApplyToUntraversedNodes`). Derived simplifiers keep the C-01 losses
(ETC-04/05), now documented by `Invoke_BodyWithCustomUnaryOperator_IsKept`.

**Tests.** Updated reproducers: `ETC02_CopyExpression_Block_KeepsVariables`, `ETC02_CopyExpression_Loop_KeepsLabels`,
`ETC02_CopyExpression_NewWithMembers_KeepsMembers`, `ETC08_ExactSimplifier_NewArrayInit_KeepsEveryElement`,
`ETC08_ExactSimplifier_Block_KeepsStatementsVariablesAndResult`, `ETC09_ExactSimplifier_DoesNotThrowOnValidTrees`,
`ETC09_Comparer_HashesValidTree`, `ETC11_InvokeInlining_KeepsInvocationWhenParameterIsNotSubstitutable`,
`ETC13_Rethrow_NullOperand`; `ExpressionSimplifierFinalizationTests.Simplify_TypeBinaryWithoutRule_ReturnsNodeUnchanged`
(it pinned ETC-09). New `ExpressionTreeR1aTests` (category `ETC-R1a`, 33 tests). Results: `Utils.sln` Release build without
error; UtilsTest.Unit 14,231/14,231; UtilsTest.Functional 383/383; UtilsTest.Security 244 passed, 3 inconclusive
(platform-only tests, unchanged).

**Performance** (Release, `ExpressionSimplifier.Simplify` per operation, median of 6 alternated process triples base / R1a /
A/A copy of base, 1.5 s warm-up, 1 s measurement; scenarios rerun with 8 triples are marked *):

| Scenario | base ns | R1a ns | R1a vs base | A/A vs base | bytes base → R1a |
|---|---|---|---|---|---|
| scalar arithmetic | 7,762 | 7,674 | −1.1 % | +1.6 % | 14,403 → 14,403 |
| nested unary (20) * | 1,288 | 1,267 | −1.7 % | −0.7 % | 2,401 → 2,401 |
| binary sum of 10 parameters | 562,100 | 550,814 | −2.0 % | −0.6 % | 1,326,600 → 1,326,600 |
| method calls | 4,764 | 4,768 | +0.1 % | −0.7 % | 4,817 → 4,817 |
| conditional | 922 | 912 | −1.0 % | +0.4 % | 1,432 → 1,432 |
| lambda | 1,866 | 1,832 | −1.8 % | −0.3 % | 4,009 → 4,009 |
| supported Invoke inlining * | 7,123 | 7,238 | +1.6 % | +0.0 % | 11,659 → 11,707 |
| repeated additive terms (12) | 6,607 | 6,498 | −1.7 % | −0.6 % | 6,704 → 6,704 |
| P16 numeric chain (16) | 58,416 | 58,320 | −0.2 % | +1.1 % | 76,145 → 76,145 |
| P15 opaque grouping (8 + opaque) | 1,822,396 | 1,791,724 | −1.7 % | −1.6 % | 2,796,590 → 2,796,590 |
| 200-element untraversed array * | 2,286 | 2,134 | −6.7 % | −0.7 % | 4,337 → 4,241 |

No regression above 5 %. A first run showed nested unary at +4.3 % (all six triples above base); it came from an `IsOpaque`
test added to `TransformPrepared`, which was redundant (the opaque context already materializes `Array.Empty`) and was
removed. Supported Invoke inlining costs one analysis object (+48 B) and the body scan (+1.6 %). The untraversed array is
cheaper because `master` rebuilt it (into a corrupted empty array) while R1a keeps it. The base cannot run a `Block` scenario
at all (ETC-08 turns it into `void` and the enclosing `Add` throws), so the large untraversed tree uses an array only.

**Remaining limitations (R1b/R2).** Untraversed families are not simplified inside (ETC-01, ETC-10); explicit `CopyExpression`
still ignores children for 11 families (ETC-02); substitution does not enter binders (ETC-03), so inlining stays conservative
until R2; derived simplifiers still drop unary/lambda metadata (C-01); side-effecting operands (ETC-15, C-06) are untouched;
no stack guard or budget yet (R1a-Security).

### R1a-Security — Resource budgets and stack safety (second)

Specified in §12 (ETC Security). File-level scope: new `Utils/Expressions/ExpressionResourceLimits.cs`,
`ExpressionResourceLimitException.cs`, an iterative validator (from `tests/ExpressionResourceProbe/ExpressionShapeMeter.cs`),
budget scope in `ExpressionTransformer.TransformCore` / `ExpressionSimplifier.Transform` / `TransformWithinBoundary`,
`ExpressionComparer.Equals`/`GetHashCode` and the raw structural entry points, `ReplaceArgumentsCore`, stack guards at every
recursive method listed in §12.3. Tests: §12.7.

#### R1a-Security result (2026-10-28)

- Baseline: `master` at `8d705f2c1d37a81e8d3735437bdfa670d3697006` (R1a merged, PR #670). Final SHA: head of the
  R1a-Security pull request (branch `etc/r1a-security-resource-limits`).
- Decisions applied: S-01 (defaults, confirmed below), S-02, S-03, S-04, S-05. S-06 (parser) stays open: see "Unprotected
  boundaries".

**Architecture**

| Piece | Where | Role |
|---|---|---|
| `ExpressionResourceLimits` (public sealed record) | `Utils/Expressions/ExpressionResourceLimits.cs` | immutable, validated (`>= 1`) limits; `Default` (512 / 50,000 / 1,000,000 / 10,000,000), `Unlimited` (`int.MaxValue`/`long.MaxValue`: checks off, stack guard still on) |
| `ExpressionResourceLimitException : InvalidOperationException`, `ExpressionResourceLimitKind` | `ExpressionResourceLimitException.cs` | `Kind` (`Depth`, `Nodes`, `Work`, `StructuralWork`, `Stack`), `Limit`, `Observed` (exact, or a lower bound when the measurement stopped); `Stack` keeps the `InsufficientExecutionStackException` as inner exception. `StructuralWork` was added to the R0 proposal so the two budgets are distinguishable. |
| `ExpressionResourceBudget` (internal) | `ExpressionResourceBudget.cs` | one `[ThreadStatic]` session object per thread: operation nesting, owner, two visit counters with deadlines, active depth/node limits; `Run` (public operation), `RunInternal` (internal walk, default limits, no validation when no operation runs), `ConsumeTransformVisit`, `ConsumeStructuralVisit`, `EnsureStack`, `ToSortedList` |
| `ExpressionShapeValidator` (internal) | `ExpressionShapeValidator.cs` | non-recursive validation and exact measurement (below) |

**Operations and nesting.** A public operation is the outermost engine call on a thread: `ExpressionSimplifier.Transform`
(`Simplify`, `ExpressionExtensions.Simplify`), `ExpressionComparer.Equals` (after the reference fast path) and `GetHashCode`,
and `ExpressionTransformer.TransformCore`/`ReplaceArguments` when no operation runs or when they are entered on another
instance than the operation's owner (a nested public call of a third-party transformer). It validates its inputs, then
runs under the visit budgets. A nested public call (rule, user `Equals`/`GetHashCode`, extension getter) joins the running
operation: its inputs are validated against `min(active, own)` depth/node limits, each visit deadline becomes
`min(active deadline, visits so far + own budget)`, the counters are never reset, and the previous limits come back when it
returns (the visits it made stay counted). Engine-internal calls never start an operation: the simplifier's rules compare
through `ExpressionComparer.EqualsWithinOperation`, `GetHashCode` simplifies through `ExpressionSimplifier.TransformDetached`,
the comparer's own simplifier is marked `JoinsRunningOperation`, and the internal walks enter through `RunInternal`. So the
limits configured by the caller of the public operation govern all the work it causes, and an engine-internal helper never
applies its own defaults over them.

**Validation** (`ExpressionShapeValidator.Validate`). Iterative walk of the unfolded tree with a reused per-thread stack
(no allocation in the steady state, no user code: dispatch on the runtime class, never on the virtual `NodeType`), counting
every item reached and the depth of every path, stopping as soon as either limit is crossed: O(min(logical items, node
limit)) time and memory whatever the sharing, and a partial walk is never a success. It covers every child of the R0
inventory, including R1b–R4 families that the engine does not traverse yet (`New`, `NewArray*`, `Block` variables and
statements, `Try` handlers/filters/finally/fault, `Switch` cases, `MemberInit`/`ListInit` bindings and initializers,
`Index`, `Loop`, `Goto`, `Label`, `RuntimeVariables`, conversion lambdas, lambda parameters); containers (`SwitchCase`,
`CatchBlock`, `MemberBinding`, `ElementInit`) count as one item and one level. Extension and custom nodes are opaque
leaves (no `Reduce`, `CanReduce`, `VisitChildren` or getter call). The walk descends into the first child directly and
pushes only the others; calls and invocations read their arguments through `IArgumentProvider`. A refusal reports the
exact value through `Measure` (post-order over distinct items, saturating `long` instead of the prototype's `BigInteger`,
child edges bounded by the same cap, cycles of custom graphs treated as leaves) when that stays below `4 × MaxNodes`
distinct items, otherwise the lower bound of the decision walk. Measured cost: about 3 ns per node for call chains, 4 ns
for binary trees.

**Shared DAG validation (ETC R2 follow-up, 2026-10-29).** The walk above is O(min(logical items, node limit)): bounded by
the default limit (50,000), but exponential for a shared DAG once the node limit exceeds its occurrence count — in
particular under `ExpressionResourceLimits.Unlimited`, where `e = e + e` repeated 40 times (41 distinct nodes, 2^41 - 1
occurrences) never finished. `Validate` now keeps the historical walk alone for node limits up to 65,536 (every default
validation, same cost and refusals), returns at once when both limits are at their maximum (nothing can be refused; the
stack guards of the following walks still apply), and otherwise decides in bounded stages: an unfolded walk of 4,096
occurrences (small inputs), `Measure` capped at 256 distinct items (heavily shared DAGs, decided without unfolding), the
same unfolded walk resumed where it paused (it pauses between two items instead of throwing, so nothing is walked twice)
up to 4,194,304 occurrences (large trees at the cost of a plain walk, no memoization overhead), then the uncapped
`Measure` (distinct items or child edges above the node limit refuse at once). The capped `Measure` stops at its edge
budget without an exception and without enumerating the rest of a wide child list. Total cost O(4,194,304 + distinct
items + child edges), iterative, identity-memoized, no reference kept after the call. `Measure` now skips a second
schedule of an item being expanded, so each item's child edges are examined once. Refusals keep their kinds; the observed
value of a measured refusal is exact (saturated at `long.MaxValue`); when both limits are exceeded, a measured decision
reports the depth first. Tests: `ExpressionSharedDagValidationTests` (Security, 10) and
`ExpressionShapeValidationCorpusTests` (Unit: every R0–R2 catalog sample and a 16-level doubling DAG over it agree on both
paths).

Measurements (Release, same harness as §9 R2, 3 rounds, A/A control; "before" is the PR head `f176a3a6`). Shapes of the
doubling DAG `e[n+1] = e[n] + e[n]` (measured, never unfolded): 20 levels — 21 distinct nodes, depth 21, 2,097,151
occurrences; 40 levels — 41, 41, 2,199,023,255,551; 60 levels — 61, 61, 2,305,843,009,213,693,951; 70 levels — 71, 71,
saturated at `long.MaxValue`. `val-*` call `Validate` directly: default limits for the historical families and
`val-tree15` (32,767 nodes), `MaxNodes` 1,000,000 for `val-wide100k` (100,000 distinct constants) and `val-tree17`
(262,143 nodes), `MaxNodes` `long.MaxValue - 1` for `val-dagpartial` (60 levels, one shared level in three) and
`val-dagexp22` (22-level doubling DAG, 8,388,607 occurrences; larger ones cannot be measured "before").

| Scenario | before ns | A/A | after ns | after/before | bytes before→after |
|---|---|---|---|---|---|
| scalar | 601 | -1.2% | 593 | -1.3% | 832→832 |
| unary | 491 | -1.0% | 485 | -1.2% | 576→576 |
| binary | 14259 | +1.5% | 14287 | +0.2% | 18356→18356 |
| lambda | 3855 | -2.4% | 3737 | -3.1% | 6937→6937 |
| leftsum20 | 4905875 | -0.2% | 5040825 | +2.8% | 9667359→9667357 |
| p15opaque8 | 21170300 | -1.1% | 20804200 | -1.7% | 18165726→18165726 |
| p16chain12 | 13037650 | +0.2% | 12925450 | -0.9% | 17247996→17247996 |
| cmp-pos | 3074514 | +0.2% | 3045914 | -0.9% | 4123405→4123405 |
| hash | 3816483 | +0.4% | 3817683 | +0.0% | 5358408→5358408 |
| val-dagpartial | 33256100 | +4.1% | 30546 | -99.9% | 136→21728 |
| val-dagexp22 | 36312400 | +4.0% | 22160 | -99.9% | 136→7424 |
| val-scalar | 59 | +0.0% | 62 | +5.1% | 96→96 |
| val-unary | 62 | +1.6% | 61 | -1.6% | 96→96 |
| val-binary | 98 | +0.0% | 99 | +1.0% | 96→96 |
| val-lambda | 112 | +0.0% | 113 | +0.9% | 96→96 |
| val-leftsum20 | 243 | -4.1% | 241 | -0.8% | 96→96 |
| val-p15opaque8 | 377 | +0.5% | 376 | -0.3% | 96→96 |
| val-p16chain12 | 315 | +0.6% | 316 | +0.3% | 96→96 |
| val-tree15 | 148005 | +6.4% | 155444 | +5.0% | 96→96 |
| val-wide100k | 1518121 | +0.1% | 1594931 | +5.1% | 4193693→4201179 |
| val-tree17 | 1519793 | -3.9% | 1560493 | +2.7% | 99→33394 |

The engine scenarios (simplify, comparer, hash) and the direct default validations stay within the noise (-3.1 % to
+2.8 %, A/A up to ±6 % on sub-microsecond timings; `val-scalar` +5.1 % is 3 ns on 59 ns); the default path is the historical loop, unchanged. The engine and DAG rows come from the campaign run just before the historical loop was put back inline (a change that only removes work from the default path); the `val-*` rows of the default and large-tree paths were measured on the final code. Shared DAGs under
a high limit are decided about a thousand times faster (33–36 ms to 22–31 µs) and no longer grow with the occurrence
count; `Unlimited` performs no validation at all. Large trees without sharing under a high limit cost +3 to +5 % (the
4,096-occurrence pause and the capped probe, about 60–75 µs, then the resumed walk); the allocations of those scenarios
come from the probe (`val-tree17`: 33 KB) and, for `val-wide100k`, from the work stack growth that already existed.

**Counter and guard placement**

| Path | Budget | Stack check |
|---|---|---|
| `ExpressionTransformer.TransformPrepared` | 1 transformation visit per node, for `TransformCore` and the P16 `TransformCoreWithPreparedOperands` path alike (single accounting point) | — |
| `ExpressionTransformer.TransformCore`, `TransformCoreWithPreparedOperands` | — (session/owner check) | yes, every call |
| `ExpressionTransformer.ReplaceArgumentsCore` | 1 structural visit per node | yes (with the visit) |
| `ExpressionComparer.EqualsCore`, `Hash` (public, raw and memo-free variants) | 1 structural visit per compared pair / node | yes |
| `ExpressionCanonicalOrder.Build`, `KeyNode.CompareTo` (base class: every key kind) | 1 structural visit per node / compared pair | yes |
| `InvocationInliningAnalysis.Scan`, its `ReferenceFinder.Visit` | 1 structural visit per node | yes |
| P9 and P15 memo key `TryHash`/`StrictEquals` | own 1,024-node budget (unchanged) | yes |
| simplifier helpers `GetExpressionMetrics`, `IsEliminableFloatingOperand`, `IsInertArithmetic`, `MightInvokeUserCodeWhenSimplified`, `CountAdditiveConstants`, `CollectAdditiveTerms`, `CollectMultiplicativeFactors` | — (linear in their input) | yes |

The canonical-order entry points (`BuildKey*`, `BuildKeys*`, `Compare`) and the raw comparer entry points run through
`RunInternal`, so they are bounded even when called without a public boundary. A refusal raised by a key comparison inside
a framework sort (`OrderBy`, which wraps comparer exceptions in an `InvalidOperationException`) is restored by
`ToSortedList` (found by the isolated campaign: left sums of 100 terms surfaced as a `TargetInvocationException`).

**Stack failures through rule invocation.** Rule invocation wrapped every exception of a rule in a new
`TargetInvocationException` from inside a `catch` block. Measured in-process: a stack-guard exception raised under a
recursion that goes through rules (a test transformer traversing nested arrays from a rule; symbolic derivation is built
the same way) was re-wrapped at every level, the catch handlers running on top of the still-deep stack, until the process
overflowed. Resource failures (`ExpressionResourceLimitException`, `InsufficientExecutionStackException`) are no longer
caught by rule invocation (fast invokers use an exception filter; the reflection fallback uses
`BindingFlags.DoNotWrapExceptions` when reflection binds the arguments as they are, and keeps the historical call
otherwise); every other rule exception keeps its historical single wrapping. The outermost boundary still unwraps a
resource exception reached only through `TargetInvocationException` layers, and turns an
`InsufficientExecutionStackException` into a `Stack` refusal; every other exception, including a user exception wrapping
a resource exception, propagates unchanged.

**Equals/GetHashCode contract.** `Equals(x, x)` on the same instance, and the `null` cases, return without validation
(documented compatibility exception, O(1)); two distinct instances are both validated before simplification;
`GetHashCode` always validates. A refusal is an exception, never `false` or a fallback hash.

**Compatibility (2.0.0 batch, `CHANGELOG.md`).** Default limits are on (S-04): inputs deeper than 512 levels or larger than
50,000 logical nodes, and operations beyond the budgets, now throw. The full suites pass unchanged with the defaults
(Unit 14,231, Functional 383, Security 244 before this round), so no existing test exceeds them; the pathological R0
families do (left sum of 100 terms: 1.5 s and 2.5 GB on `master`, now a `StructuralWork` refusal after about 0.9 s).
Additive public API only: `ExpressionResourceLimits`, `ExpressionResourceLimitException`, `ExpressionResourceLimitKind`,
`new ExpressionSimplifier(ExpressionResourceLimits)`, `ExpressionComparer.Create`/`ResourceLimits`, protected
`ExpressionTransformer(ExpressionResourceLimits)`/`ResourceLimits`. A resource failure inside a rule is no longer wrapped
in a `TargetInvocationException`.

**Subclasses.** Third-party `ExpressionTransformer` subclasses inherit the protection through `TransformCore` and
`ReplaceArguments` (they are the only ways into the engine) with `ExpressionResourceLimits.Default` unless they call the
new protected constructor; `ExpressionDerivation`/`ExpressionIntegration` (Utils.Mathematics) use the defaults. A subclass
whose public `Transform` never calls `TransformCore` runs outside the limits: the library cannot intercept it.

**Tests** (`UtilsTest.Security/Expressions`, 29 new tests in `ExpressionResourceLimitsTests` and
`ExpressionResourceStackAndValidationTests`, helpers in `ExpressionResourceTestTrees`): configuration; depth and node limits
just below and above on every public entry point; logical vs distinct nodes (shared DAG); wide and balanced trees;
transformation and structural budgets just below and above on every walk; refusal inside a sort; comparer refusal vs
inequality vs unsupported family; reference fast path; nested stricter and looser calls; user-code re-entrancy (constant
`Equals` calling the simplifier, constant `GetHashCode` calling the comparer, a rule calling the comparer and re-entering
its own `Transform`); ambient state after failures at every phase (operation, P9/P15 sessions, lexical scopes, metrics,
comparer depth); thread isolation; stack refusals on 256 KiB threads for simplify, transform, substitution, comparer
equality and hash, raw equality and hash, canonical keys and key comparison; validator correctness (tree, diamond, repeated
children, many parents, 2^65 logical nodes, huge fan-out, every container, extension nodes never inspected); R1b/R1c
future-handler fixtures (below). Results on the final head: `Utils.sln` Release build without error or new warning; UtilsTest.Unit 14,231/14,231; UtilsTest.Functional 383/383; UtilsTest.Security 273 passed (244 before + 29 new) and 3 inconclusive (platform-only, unchanged); ETC R0/R1a categories included in Unit.

**Isolated campaign** (`tests/ExpressionResourceProbe`, new `--limits` option and exit code 13; raw data in
`tests/ExpressionResourceProbe/results/2026-10-28/`). One process per run; families A-left-sum, B-right-sum, D-horner,
D-poly-mul-shared, G-negate, G-call, G-invoke, G-conditional at sizes 1,000 / 5,000 / 20,000 (a pair stops at its first
refusal) on the eight operations (`simplify`, `transform`, `substitute`, `hash`, `equals`, `rawhash`, `rawequals`,
`canonical`), plus the work families (F-g, C-balanced-sum, A-left-sum at 20 / 100 / 1,000 terms):

| Configuration | Runs | ok | Controlled refusals | Stack overflow / crash / other |
|---|---|---|---|---|
| Windows main 1 MB, `--limits default` | 98 | 40 | Depth 40, Stack 18 (raw walks: no input validation) | 0 |
| Windows main 1 MB, `--limits budgets` | 122 | 65 | Stack 44, StructuralWork 12, Work 1 | 0 |
| Windows 256 KiB thread, `--limits budgets` | 78 | 21 | Stack 57 | 0 |
| Windows main, work families, `--limits default` | 18 | 9 | Work 3, StructuralWork 6 | 0 |
| Linux (WSL2 Ubuntu) main 8 MB, `--limits budgets` | 162 | 116 | Stack 29, StructuralWork 16, Work 1 | 0 |
| Linux 1 MiB thread, `--limits budgets` | 115 | 58 | Stack 47, StructuralWork 9, Work 1 | 0 |
| Linux 256 KiB thread, `--limits budgets` | 78 | 21 | Stack 57 | 0 |

Every run ended with a successful result or an `ExpressionResourceLimitException`, and a clean process exit: none of the
R0 stack overflows (left/right sums, Horner, Negate, `Abs` and Invoke chains, 256 KiB threads) remains on the guarded
paths. Work examples: S5 family g at 20 terms and a balanced sum of 1,000 terms (R0: 56 s / 27.6 GB and 30 s / 31 GB)
now stop with a Work or StructuralWork refusal; a left sum of 100 terms (R0: 1.5 s / 2.5 GB) stops with StructuralWork after
about 0.9 s. The first campaign run found the sort-wrapping defect described above (9 runs ended with a
`TargetInvocationException`); the table is the run after the fix.

**Performance** (Release, `net9.0`, one process per scenario and variant, 6 alternated triples base / R1a-Security / A/A copy
of base, 1.5 s warm-up, 0.6 s measurement; A/A within −3.0 % … +2.5 %):

| Scenario | base ns | R1a-Security ns | Δ | A/A | Δ ns |
|---|---|---|---|---|---|
| `Simplify(Constant)` (session cost) | 40 | 62 | +51.9 % | +1.2 % | +21 |
| scalar arithmetic | 8,254 | 8,635 | +4.6 % | −0.5 % | +380 |
| unary chain (20) | 1,081 | 1,286 | +19.0 % | +0.8 % | +205 |
| binary sum (10 parameters) | 561,112 | 584,466 | +4.2 % | −0.2 % | +23,353 |
| method calls | 4,162 | 4,433 | +6.5 % | +0.2 % | +270 |
| conditional | 914 | 1,114 | +21.9 % | +0.7 % | +200 |
| lambda | 1,826 | 2,042 | +11.9 % | +0.8 % | +217 |
| supported Invoke inlining | 7,398 | 7,810 | +5.6 % | +0.6 % | +412 |
| P16 numeric chain (16) | 57,629 | 58,364 | +1.3 % | −1.2 % | +735 |
| P15 opaque grouping | 1,841,252 | 1,864,227 | +1.2 % | +0.5 % | +22,976 |
| S5 family g, 12 terms | 11,908,616 | 12,219,556 | +2.6 % | −0.6 % | +310,940 |
| 200-element untraversed array | 2,253 | 7,604 | +237.5 % | +0.6 % | +5,351 |
| simplify, call chain 400 | 158,226 | 159,309 | +0.7 % | −0.3 % | +1,082 |
| identity transformer, call chain 400 | 30,930 | 33,605 | +8.6 % | −0.6 % | +2,674 |
| identity transformer, balanced 4,096 leaves | 274,795 | 327,776 | +19.3 % | +1.2 % | +52,982 |
| identity transformer, shared DAG (8,191 logical) | 279,333 | 330,896 | +18.5 % | +0.4 % | +51,563 |
| public `Equals`, call chains 400 | 344,926 | 355,123 | +3.0 % | −1.1 % | +10,197 |
| public `GetHashCode`, call chain 400 | 177,782 | 181,360 | +2.0 % | +0.1 % | +3,579 |
| raw structural equality, 400 | 7,784 | 9,688 | +24.5 % | +2.5 % | +1,904 |
| raw structural hash, 400 | 11,702 | 12,794 | +9.3 % | +0.4 % | +1,092 |
| canonical order, 400 | 20,914 | 22,340 | +6.8 % | −3.0 % | +1,427 |

Allocations are unchanged in every scenario. Decomposition (measured separately): (1) one-time validation, about 3 ns per
node for call chains and 4 ns for binary trees (`validate-*` scenarios: 79 ns for the 9-node scalar tree, 1.27 µs for 401
nodes, 32 µs for 8,191 nodes); (2) per transformation visit, about 1–2 ns (counter in the session object plus the owner
check of `TransformCore`); (3) stack checks, 1.0–1.35 ns per call (`ess` calibration), once per transformation step and
once per structural visit; (4) session, about 21 ns per public operation. The relative cost is above 5 % only where the
protected work is itself tiny: simplifications of a few nodes that finish in about 1 µs (+200 ns: validation of every
node plus the visit accounting), the identity transformer (whose per-node work is about 35 ns), the raw structural walks
(about 5 ns of guard and counter per 20 ns visit), and the untraversed array, whose 601 nodes `master` never visited but the
validator must (R1b will traverse them). Two optimizations were applied after the first measurement (one thread-static
session object instead of eleven thread-static fields; one stack check per transformation step instead of two; the
validator's first-child descent, dispatch order and `IArgumentProvider`), which brought the unary chain from +20.5 % to
+19.0 %, the identity transformer on a call chain from +25.9 % to +8.6 % and the call-chain validation from 3.7 µs to
1.27 µs. Sampling the stack check every N visits was rejected: it would save about 1 ns per visit, and §12.3 asks for a
check at every recursive entry. The remaining overhead is the cost of the security contract (every input is measured, every
step is counted and stack-checked) and is accepted as such.

**Remaining risks.** The visit budgets bound measured engine work, not time or memory: 1,000,000 transformation visits
can still mean several seconds and several gigabytes of cumulative allocation on the worst families (§7.4: 0.8–6.5 KB per
visit). The stack guard keeps a fixed headroom (about 64–128 KB): an unguarded recursive segment deeper than that (user
`Equals`/`GetHashCode` or extension code, framework expression compilation or `ToString`, the parser) can still overflow. A
non-resource exception thrown by a rule deep inside a recursion that goes through rules is still re-wrapped at every
level (historical behavior) and could exhaust the stack while unwinding.

**Unprotected boundaries.** `Utils.Parser` and the `CSyntax`/`VBSyntax` front ends (ETC-16, ETC-18: a 42-character input
still terminates the process before any expression exists; dedicated stage S-06, **critical**); `Expression.Compile`,
`Expression.ToString` and other framework walks; `ExpressionEx`, `ExpressionOptimiser`, `BlockExpressionBuilder` and the
expression compilers of `Utils.Expressions` (not part of the ETC engine); third-party `Transform` implementations that never
call `TransformCore`; user code run from constants, rules and extension nodes (bounded only in the engine work it causes).

**Instructions for R1b/R1c authors.** A new `Prepare*` method is reached through `PrepareTransform` and recurses through
`PrepareExpression` → `TransformCore`, so it is stack-checked and counted (in `TransformPrepared`) without touching the
session; a new equality, hash or canonical-key family is added as a case of `EqualsCore`, `Hash` or `Build` (or a new
`KeyNode` kind), which already count and check. A helper that recurses on its own calls `ConsumeStructuralVisit` (counting)
or `EnsureStack` (bounded helper). The fixtures `FutureTraversalHandler_IsBudgetedAndGuarded` and
`FutureStructuralWalkers_AreBudgetedAndGuarded` show both patterns on deep conditionals, invocations and nested arrays.
Follow the checklist of `Utils/AGENTS.md` ("Expression engine — checklist for new recursive handlers").

### R1b — Scalar traversal and reconstruction

| Change | File / method | Tests |
|---|---|---|
| Add `PrepareMember`, `PrepareTypeBinary`, `PrepareNew`, `PrepareNewArray`; `Default` explicit leaf. `PrepareTransform` stays a type-pattern dispatch `switch` (allowed by the local exception of `Utils/AGENTS.md`); dispatch-order change benchmarked on the hot path | `ExpressionTransformer.PrepareTransform` and new `Prepare*` | rows flip to `Rewrites` for G/S/D; preparation order test per family (H-2); subclass rule shape test (H-1) |
| Per-family rebuilders called from the `CopyExpression` dispatch `switch` (kept as a `switch`; each branch becomes a short call): `MakeMemberAccess`, `TypeIs`/`TypeEqual`, `New(ctor, args, members)` and `New(type)`, `NewArrayInit`/`NewArrayBounds` | `CopyExpression` + named `Rebuild*` methods | `RootMetadata` unchanged for every R1 sample (G, R) |
| C-01 faithful unary/lambda rebuild as base default | `RebuildUnaryExpression`, `RebuildLambdaExpression`, `CopyUnaryExpression`, `CopyExpression` unary branches, `ReplaceArgumentsCore` | rows `Negate.CustomMethod`, `Convert.CustomMethod`, `Throw`, `Lambda.Metadata` → `Rewrites` in G/D/R; update `ExpressionTransformerUnaryLazyParametersTests` only where it pins the loss (recorded here) |
| C-06 side-effect opacity | `ExpressionSimplifier` rule guards (`IsOrdinaryBinaryArithmetic`/operand classification used by additive/multiplicative grouping, `AdditionOfEqualsElements`, factoring, zero rules) | `ETC15_*` flipped to evaluation-preserving results; S5 corpora unchanged |
| Substitution through Member, TypeBinary, New, NewArray, Unary (faithful) | `ReplaceArgumentsCore` | rows → `Rewrites` in R |

#### R1b result (2026-10-29)

Branch `etc/r1b-r1c-scalar-traversal-equality` (base `b071bd0a`), one PR with R1c. Commits: `d841b7d4` (traversal and
rebuild), `70973f5e` + `df37dd46` (C-01; `70973f5e` holds the transformer part only, completed by `df37dd46` after a staging
script error — history not rewritten), `60aba1e8` (substitution), `96e1081d` (ETC-15), `4a362335` (tests, checkpoint),
`43407a4c` (ETC-15 follow-up on division identities). `d841b7d4` and `df37dd46` each leave one or two characterization tests
red (the substitution column of the coverage baseline, ETC-03) until `60aba1e8`; every later commit is green.

**Traversal and reconstruction (C-03).** `PrepareTransform` dispatches `MemberExpression`, `TypeBinaryExpression`,
`NewExpression`, `NewArrayExpression` and `DefaultExpression` to `PrepareMember` (receiver, if any), `PrepareTypeBinary`
(tested expression), `PrepareNew` (arguments in order), `PrepareNewArray` (elements or bounds in order) and
`PrepareDefaultValue` (explicit leaf, not opaque), in `ExpressionVisitor` order. Nodes are rebuilt by the R1a copy helpers
(member, constructor and members, element type and rank, type operand kept); the helpers return the node itself when every
child is the same instance, and `CopyExpression` applies the C-02 arity contract to `MemberAccess` (one child for an
instance member, none for a static one). No getter, constructor or delegate is run while transforming. The exact
simplifier's `FinalizeExpression` reuses the rebuilt node for the new families. The lambda body asymmetry (H-4) is kept.

**Faithful unary and lambda reconstruction (C-01).** `CopyUnaryExpression` uses `MakeUnary` (method and type kept, lifted
operators lifted as by the original factory), shared by `PrepareUnary`/`RebuildUnaryExpression`, `CopyExpression` and
`ReplaceArgumentsCore`; a rethrow is kept as is. `RebuildLambdaExpression` keeps the delegate type, the name and the
tail-call flag by default. The simplifier's exact-type unary override is removed, its lambda override only folds `NaN`
operands. `OnEnterLambdaScope`/`OnExitLambdaScope` are unchanged (paired in `finally`).

**Scalar substitution.** `ReplaceArgumentsCore` enters member receivers, type tests, creation arguments and array elements
with the same metadata-preserving helpers; unary nodes go through the faithful rebuild. Nested lambdas, blocks, catches,
switches and runtime variables stay untouched (R2, ETC-03 not globally resolved). `InvocationInliningAnalysis` mirrors the
new families (a property read or a constructor may run user code, so later parameter reads stay guarded) and no longer
refuses unary nodes with metadata.

**Side effects (ETC-15, C-06).** `ExpressionEffectAnalysis` classifies an evaluation as reading mutable state (parameters,
mutable fields), possibly throwing (checked arithmetic, integral and decimal division by a non-constant or 0/-1 divisor,
decimal arithmetic, array access, null dereference of a non-constant reference receiver, casts, unboxing, nullable access,
array bounds, `Math.Sign`/`Clamp`/`Round(digits)`/`Abs(signed integer)`), allocating (arrays, boxing, lambdas, objects) or
observable (assignments, increments, throw, every method outside the explicit allow-list of total `Math`/`MathF`/`double`/
`float` functions, property getters, constructors, custom operators and conversions, invocations, and every unknown or
extension node, whose properties are never read). `CanDiscard`, `CanRepeat` and `CanReorder` are distinct permissions; a rule
whose effect on its operands is not declared needs the three (`HasInertOperands`). Results are cached per operation in the
resource session (`Session.EffectCache`, dropped at the outermost exit) and each newly analyzed node counts one structural
visit. Integration, at the decision points only: (1) `ExpressionTransformer.AllowsRules` (new `internal virtual` hook, H-7)
is asked once per node before the first matching rule runs; the simplifier allows its rules only when the node's operands
are inert (invocations keep their own inlining analysis); (2) the additive and multiplicative canonicalizations pin the
non-inert terms (and, next to an observable term, every term reading mutable state): pinned terms keep their occurrences
and source order, after the canonicalized inert terms, which are still grouped, cancelled and sorted; (3) decimal chains are
not reassociated (`CanonicalizesOperator`); (4) the rules whose own operator may throw were audited: `0 / x -> 0` is
restricted to floating-point types, `x / -1 -> -x` and `x / (-y) -> -(x / y)` are declined for signed integral types
(`43407a4c`); the decimal identities preserve overflow, and the nested division rules are blocked by the operand gate.
Structural equality, hashing, canonical keys and the P3/P9/P15/P16 memos are untouched: two calls `f(x)` stay structurally
equal (and memoizable) without being interchangeable. `FoldNaNOperand` was already stricter (unchanged).

Exceptions of ordinary arithmetic on its own domain are now preserved too (division by zero, `MinValue / -1`, decimal
overflow), which narrows the S3 "symbolic latitude" for exceptions; IEEE-754 special values, signed zeros and floating
reassociation keep their documented S3 latitude.

**S5 scenarios reclassified (reviewer decision of 2026-10-29).** The S5 scenarios broken by the strict boundary (59 at the first strict run) were
kept and classified: (a) structural and memo contracts (opaque-constant invariants, comparison memo, observed memo, comparer
batching, structural canonicalization ordering): the opaque constant is now carried by the inert term
`Math.Truncate((constant as double?) ?? 1.0)` (`UtilsTest/.../SafeOpaqueTerm.cs`) instead of a test-local user method; every
user-call count and sequence is unchanged, and the form digests that include the carrier text were re-measured on `master`
`b071bd0a` with that carrier and are identical on the branch; the canonical-order tests on user methods use same-name
allow-listed functions (`Math.Sin`/`double.Sin`) and a pinned-order counterpart; (b) algebraic rewrites of unproven terms
(custom operators, user calls, decimal chains): same scenarios, now asserting the preserved evaluations and traces;
(c) user-code contracts on extension nodes: the getter-read counts now measured (6 reads, constant in the chain length) are
asserted exactly, with the former baseline counts as upper bounds. The former user-method carrier is asserted by
`ExpressionTreeR1bEffectTests.UserMethodCarrierChains_KeepEveryCallInOrder`.

**Tests.** `ExpressionTreeR1bTests` (17), `ExpressionTreeR1bEffectTests` (11, results and traces, exact and derived
simplifiers), `ExpressionScalarFamilyResourceTests` (Security); updated characterization: ETC-02/03/04/05/08/09/10/11/15,
R1a pass-through pins, coverage baseline (12 rows), derived-simplifier fidelity pins, the R1a-Security model handler.

**Remaining limits.** The rule gate is coarse (no rule declares which evaluations it drops), so a safe rewrite such as
`f() * 1` is skipped when `f()` is not proven inert (the additive canonicalization still drops zero constants next to pinned terms);
parameters are treated as mutable next to an observable term; per-rule effect declarations, a configurable purity policy
(`[Pure]` is not trusted, as decided) and capture-aware closures are left open. Quote is unchanged (C-05 pending, ETC-12).
### R1c — Scalar equality and hashing

| Change | File / method | Tests |
|---|---|---|
| Equality + hash for Conditional, Invoke, TypeBinary, Default, New (ctor, members), NewArray | `ExpressionComparer.EqualsCore`, `Hash` (dispatch `switch`es with one short call per family, `Utils/AGENTS.md` exception) | positive/negative pairs per metadata field; `Dictionary`/`HashSet` membership with identical references and independent trees; reflexivity/symmetry/transitivity on catalog pairs |
| Canonical keys for the same families | `ExpressionCanonicalOrder.Build` + new key nodes | `ETC14_*` flipped; S4 idempotence tests |
| Stack guards on new recursion | comparer, hash, canonical key | isolated-process depth test (Functional) |
| `New`/`NewArray` are never merged as equal terms by algebra (allocation identity) | simplifier grouping | dedicated tests |

#### R1b/R1c measurements (2026-10-29, Release, one process per scenario, 3 rounds, A/A control)

Harness: one process per (variant, scenario), warmup >= 10 ops and 1.5 s, 25 calibrated batches, variant order rotated per round; transform and structural visits read inside an outer operation. Final branch (`c01b6577`) against `master` `b071bd0a`:

| Scenario | master ns | A/A | final ns | final/master | bytes master→final | transform visits | structural visits master→final |
|---|---|---|---|---|---|---|---|
| scalar | 558 | -2.3% | 594 | +6.5% | 832→832 | 6→6 | 0→0 |
| unary | 413 | +1.0% | 468 | +13.3% | 576→576 | 5→5 | 0→0 |
| binary | 13746 | +0.6% | 14407 | +4.8% | 18580→18580 | 33→33 | 91→91 |
| lambda | 3476 | +2.2% | 3724 | +7.1% | 7049→7049 | 21→21 | 16→16 |
| invoke | 1099 | -1.0% | 1182 | +7.6% | 1448→1448 | 11→11 | 2→2 |
| new | 91 | +0.0% | 770 | +746.2% | 0→960 | 1→7 | 0→0 |
| newarray | 140 | -5.0% | 1901 | +1257.9% | 0→2424 | 1→15 | 0→4 |
| member | 1817 | +2.5% | 1789 | -1.5% | 2952→1744 | 9→12 | 18→12 |
| typebinary | 76 | +0.0% | 490 | +544.7% | 0→568 | 1→5 | 0→0 |
| conditional | 1058 | -0.5% | 1147 | +8.4% | 1440→1440 | 13→13 | 0→0 |
| leftsum20 | 4693460 | +0.7% | 5083700 | +8.3% | 9895385→9895386 | 11463→11463 | 46413→48925 |
| rightsum20 | 4610760 | +3.7% | 5149450 | +11.7% | 9895384→9895387 | 11463→11463 | 46413→48925 |
| balanced64 | 23032800 | -0.4% | 24592400 | +6.8% | 27017566→27017447 | 19215→19215 | 211087→221003 |
| p16chain12 | 12143500 | +2.9% | 12857900 | +5.9% | 17384676→17384735 | 34516→34516 | 59217→60080 |
| p15opaque8 | 19385200 | +1.1% | 21143000 | +9.1% | 18296992→18297012 | 48021→48021 | 120902→130180 |
| p9family16 | 6261525 | +0.3% | 6544675 | +4.5% | 8189236→8189236 | 9172→9172 | 47847→48935 |
| unsafecalls8 | 1064126 | +0.7% | 17431 | -98.4% | 1088030→13120 | 2082→39 | 6765→15 |
| dag10 | 1195429 | +0.6% | 1272700 | +6.5% | 1138197→1138198 | 7164→7164 | 2557→2557 |
| cmp-pos | 2969800 | +0.8% | 3141200 | +5.8% | 4156758→4156758 | 7972→7972 | 14465→14647 |
| cmp-neg | 3272243 | +0.1% | 3441850 | +5.2% | 4489602→4489594 | 8310→8310 | 16124→16382 |
| hash | 3700083 | +1.2% | 3962340 | +7.1% | 5399940→5399940 | 11092→11092 | 17650→17781 |

Attribution. Transform visits are identical on every pre-existing family. **Semantic changes:** New, NewArray and TypeBinary now do real work (traversal, C-03; their former cost was a pass-through), member access is cheaper (the receiver is simplified first), and `unsafecalls8` is 98 % cheaper because user-call terms are no longer factored or compared (C-06). **Engine cost:** the remaining +5 to +12 % on sums, P15/P16 and comparer scenarios, and +7 to +13 % (40–60 ns) on very small trees, is the C-06 rule gate (controlled experiments: about 16 points before optimization were the gate, 3 the pinning, R1c adds nothing measurable). Investigation history: a first implementation with an always-on per-operation table cost +20 to +37 %; instrumentation showed 0 table hits on a 20-term sum (the gate inspects operands freshly built by the previous rewrite) and ~200 KB of table growth per simplification. The final design (shallow path, bounded pass of 512 analyses without table, table fallback only for large trees and shared DAGs, per-method classification cache, identity map) brought allocations back to master and the large scenarios from +23 %/+13 % to +8 %/+6 %. The residual cost is accepted as the price of the safety gate (the gates were not weakened to gain speed); a per-rule effect declaration (R2+) would let most rules skip the analysis.

#### R1c result (2026-10-29)

Commits `629ed9fb` (equality and hash), `a2cf4db2` (canonical keys), `205f61c8` (property and resource tests).

**Equality (C-04).** `ExpressionComparer.EqualsCore` compares, after the existing node-type and result-type checks:
conditionals (test, then both branches in order, never swapped, never evaluated), invocations (invoked expression, then
arguments in order, never invoked), type tests (node type, `TypeOperand`, tested expression), defaults (type), creations
(constructor, `null` for a value-type default, arguments in order, `Members` absent on both sides or the same members in the
same order; the constructor is never run) and array creations (`NewArrayInit`/`NewArrayBounds`, array type, children in
order). Binding reuses `ParameterBindingContext` (free parameters by identity, bound parameters by position; names never
count); no new scope kind is introduced (Block/Catch stay for R2).

**Hashing.** `Hash` mirrors every field and child in the same order (commutative fallback unchanged for ordinary
`Add`/`Multiply` only), through `ParameterScopeStack`; equal trees hash equally (public and raw paths), no user code runs,
and a budget refusal is always an exception.

**Canonical keys (ETC-14).** New `ConditionalKey`, `InvocationKey`, `TypeBinaryKey`, `DefaultKey`, `NewKey`
(constructor ordered by declaring type, parameter count and types, then the metadata tie-break; members by `CompareMember`)
and `NewArrayKey`, ranked 7–12 between `LambdaKey` and `UnsupportedKey` (now 13). Keys never use a hash, `ToString` or an
evaluation; equal trees tie; antisymmetry and transitivity are checked on a bounded corpus. Families outside the scope keep
the shared tying `UnsupportedKey` (ETC-14 tests keep a Block/Loop tie).

**Boundary with algebra (§12).** Structural equality does not make expressions interchangeable: two `new Object()`, two
array creations or two invocations compare equal and hash equally, yet the R1b gate keeps every evaluation (tested with
construction and invocation counts). Newly equal pure conditionals, type tests and defaults can be grouped like any inert
term.

**Tests.** `ExpressionTreeR1cTests` (8: per-family equal pairs and single-field variants on public/raw comparers, hashes,
`HashSet`/`Dictionary`, keys; nesting; binding; no user code; properties on a bounded corpus; public versus raw paths;
the §12 boundary; convergent, idempotent canonicalization), comparer/hash/key paths and budget refusals in
`ExpressionScalarFamilyResourceTests`; updated: ETC-06/07/14, coverage baseline equality column (10 rows), comparer and
batching pins that used conditionals or creations as unsupported examples (a block is used now).
### R2 — Binding and control flow

| Change | File / method | Tests |
|---|---|---|
| `PrepareBlock`, `PrepareTry` (handlers, filters, finally, fault), `PrepareSwitch` (comparison kept), `PrepareLoop`, `PrepareGoto`, `PrepareLabel`; `RuntimeVariables`, `DebugInfo` explicit leaves | `ExpressionTransformer` | rows `Block*`, `Try*`, `Switch*`, `Loop`, `Goto*`, `Label` → `Rewrites`; labels identical instances after rebuild; compile and run original vs rebuilt |
| Scope model §5 | `ExpressionComparer` binding context and scope stack | §5.2 positive and negative alpha tests |
| Capture-avoiding substitution §5.3 | `ReplaceArgumentsCore` (+ internal alpha-rename helper) | `ETC03_*`, `ETC11_*` flipped; capture and shadowing tests |
| Block/catch scope hooks for S4 | `ExpressionTransformer`, `ExpressionSimplifier` | canonical order of block-local terms independent of variable instances |

#### R2 result (2026-10-29)

Branch `etc/r2-binding-scopes-control-flow` (base `e6ccfd5a`). Commits: `442f1d42` (checkpoint A, traversal and
reconstruction), `c809b3c9` (B, binder frames and label bijection), `3ced1a6b` (C, capture-avoiding substitution and
inlining), `ec07a341` (D, S4 scopes, effect and resource tests, allocation fix), then the documentation and measurements
commit. Every checkpoint ran the three suites green before the next one started.

**A — Traversal and reconstruction (C-03 extended).** `PrepareTransform` dispatches `BlockExpression`, `TryExpression`,
`SwitchExpression`, `LoopExpression`, `GotoExpression`, `LabelExpression`, `RuntimeVariablesExpression` and
`DebugInfoExpression` to `PrepareBlock`, `PrepareTry` (+ `PrepareCatchBlock`), `PrepareSwitch`, `PrepareLoop`,
`PrepareGoto`, `PrepareLabel`, `PrepareRuntimeVariables` and `PrepareDebugInfo` (leaf). Declarations (block and catch
variables) follow the lambda-parameter policy (`PrepareVariables`): prepared through `PrepareExpression` first, required to
stay `ParameterExpression`s (`InvalidCastException` otherwise, as for lambda parameters); runtime variables are references
with the same constraint. Block, catch, switch and loop bodies go through `PrepareExpression`, not `TransformCore`, so the
lambda-body descent (H-4) is not extended. Positional child layouts (rules, `FinalizeExpression`, `CopyExpression`): block
statements; try body, then per handler filter (if any) and body, then finally, fault; switch value, then per case test
values and body, then default body; loop body; jump value; label default value; runtime variables; none for debug
information. Named rebuilders (`RebuildBlockExpression`, `RebuildTryExpression`, `RebuildSwitchExpression`,
`CopyTryExpression`, `CopySwitchExpression`, `CopyGotoExpression`, `CopyLabelExpression`, `CopyRuntimeVariablesExpression`,
`CopyLeafExpression`) take the non-expression metadata (test types, catch variables, case structure, comparison, label
targets, jump kind, result type) from the original node, return it when no child changed, and follow the C-02 arity
contract. Label targets are never recreated. New `internal virtual` hooks `OnEnter/OnExitBlockScope`,
`OnEnter/OnExitCatchScope` (H-7), paired in `finally`. `CatchBlock` and `SwitchCase` walks count structural visits.

**B — Binding model and labels (C-04 extended).** `ParameterBindingContext` records binder frames (kind: lambda — which
covers the conversion lambda of a binary node —, block, catch; start position) over the existing positional pairs;
declarations must agree on type and `IsByRef`; bound references compare by position (innermost first, so a reused instance
shadows), free references by identity. Labels use a separate bijection (`LabelsCorrespond`): type checked, names ignored,
the first occurrence (jump or definition) records the pair, later occurrences must agree; each lambda body opens a fresh
label scope; the commutative fallback of `BinaryEqual` rolls back the correspondences of its failed positional attempt.
`Hash` mirrors it: bound parameters by (depth, position, binder kind), labels by first-occurrence index per lambda body,
commutative operands numbered from the same point. Canonical keys: `StructureKey` (rank, type, integer metadata, type
metadata, method, children) for the R2 families, block and catch frames pushed like lambda frames, label indices
normalized per lambda body and per batch element; `UnsupportedKey` moved to rank 21. Debug information is non-semantic
(always equal, constant hash, tying key) — the roadmap policy; no other metadata policy was mixed in.

**C — Capture-avoiding substitution (ETC-03).** `ReplaceArgumentsCore` delegates to `Substitute`, which carries a
`SubstitutionScope` created at the first binder: a declaration that is a replaced parameter (or already mapped by an outer
binder) hides it; a declaration mentioned by a replacement that occurs free in its scope is alpha-renamed to a fresh
parameter (same type, `IsByRef`, name) through the scope's binding entries — renaming is not a recursive call of the
public substitution. Free variables of replacements and free occurrences are found by a read-only, binder-aware
`ExpressionVisitor` that never reduces an extension node (an extension makes the result conservative). Lambdas keep their
delegate type, name and tail call; conversion lambdas are substituted and rebuilt with `MakeBinary`; control flow is
rebuilt with the same label targets; runtime variables accept another parameter only (`NotSupportedException`, C-07); a
quote is returned as is (C-05 untouched). `InvocationInliningAnalysis` scans nested lambdas, blocks, catch handlers and
switches with binder hiding: a variable argument read inside a nested closure is refused (the closure would read the
variable later), a write to a body local is not an effect on an argument variable, a switch comparison counts as user
code after each test; loops, jumps, labels, runtime variables and quotes stay barriers (a label defined in a body inlined
twice would be defined twice, targets are never cloned). Argument eligibility is unchanged.

**D — S4, effects, security.** The simplifier pushes block and catch frames on its ambient lexical stack (a frame for
every block and handler), so block-local terms are ordered by binding position, and keeps them out of the P15 shared-form
reuse (it requires an empty scope stack). P9/P16 eligibility is unchanged (block-local terms are not made eligible). The
R2 families are unknown to `ExpressionEffectAnalysis`, hence observable as a whole: they are never merged, dropped,
cancelled or reordered by the rules (tested with traces: equal blocks, `try/finally * 0`, `X - X` on switches and loops,
jumps, allocations and calls inside blocks).

**Pre-existing limit found, then fixed in the same PR.** With `ExpressionResourceLimits.Unlimited` (or any node limit above
a DAG's occurrence count), the shape validator unfolded a shared DAG logically (2^n occurrences) before any walk. Fixed by
the follow-up commit "validate shared expression DAGs in linear time" (see R1a-Security, "Shared DAG validation").

**Tests.** `ExpressionTreeR2Tests` (16, checkpoint A), `ExpressionTreeR2BindingTests` (24, B),
`ExpressionTreeR2SubstitutionTests` (21, C), `ExpressionTreeR2EffectTests` (9, D), `ExpressionControlFlowResourceTests`
(Security, 7). Updated characterization: R1a pass-through pins retargeted to the remaining untraversed families (Index,
ListInit) with the traversal contract asserted for the R2 families; ETC-01/02/03/06/07/08/09/14 findings tests; the
"unsupported family" examples of comparer, batching and Security tests (an R3 index access instead of a block);
`ReplaceArguments_NestedLambda_*` and `ETC03_*` flipped to the capture-avoiding contract; 22 coverage rows (`// ETC R2`: traversal, equality and substitution columns).

#### R2 measurements (2026-10-29, Release, one process per scenario, 3 rounds, A/A control)

Same harness as R1b/R1c (one process per variant and scenario, warmup >= 10 ops and 1.5 s, 25 calibrated batches, variant
order rotated per round), extended with R2 scenarios: blocks of 2 and 16 variables (`block2`, `block16`), 8 nested
try/catch with filters (`catches8`), a 64-case switch (`switch64`), 8 nested loops (`loops8`), 16 jump/label pairs
(`goto16`), a shared DAG of blocks (`blockdag6`), the inlining of a lambda whose body is a block (`invokeblock`), the public
comparer on alpha-equivalent and different blocks of 8 variables (`alpha-pos`, `alpha-neg`), their hash (`hash-block`) and
canonical key (`key-block`). R2 at `ec07a341` against `master` `e6ccfd5a` (which includes the R1b/R1c safety cost):

| Scenario | master ns | A/A | R2 ns | R2/master | bytes master→R2 | transform visits | structural visits master→R2 |
|---|---|---|---|---|---|---|---|
| scalar | 588 | -0.2% | 608 | +3.4% | 832→832 | 6→6 | 0→0 |
| unary | 478 | +0.0% | 481 | +0.6% | 576→576 | 5→5 | 0→0 |
| binary | 14628 | +0.2% | 14456 | -1.2% | 18580→18356 | 33→33 | 91→91 |
| lambda | 3681 | -0.1% | 3741 | +1.6% | 7049→6937 | 21→21 | 16→16 |
| invoke | 1171 | -0.5% | 1155 | -1.4% | 1448→1464 | 11→11 | 2→2 |
| new | 746 | +0.1% | 762 | +2.1% | 960→960 | 7→7 | 0→0 |
| newarray | 1923 | -0.5% | 1924 | +0.1% | 2424→2416 | 15→15 | 4→4 |
| member | 1769 | +0.6% | 1813 | +2.5% | 1744→1744 | 12→12 | 12→12 |
| typebinary | 476 | +0.6% | 485 | +1.9% | 568→568 | 5→5 | 0→0 |
| conditional | 1161 | -1.3% | 1167 | +0.5% | 1440→1440 | 13→13 | 0→0 |
| leftsum20 | 5047675 | -1.9% | 5024240 | -0.5% | 9895387→9667357 | 11463→11463 | 48925→48925 |
| rightsum20 | 4999450 | +0.4% | 4889100 | -2.2% | 9895387→9667360 | 11463→11463 | 48925→48925 |
| balanced64 | 24246000 | +0.2% | 24353500 | +0.4% | 27017566→26635743 | 19215→19215 | 221003→221003 |
| p16chain12 | 12832400 | -1.3% | 12953850 | +0.9% | 17384676→17247996 | 34516→34516 | 60080→60080 |
| p15opaque8 | 20882600 | -0.7% | 21001200 | +0.6% | 18297012→18165726 | 48021→48021 | 130180→130180 |
| p9family16 | 6536050 | -0.2% | 6556325 | +0.3% | 8189236→8087029 | 9172→9172 | 48935→48935 |
| unsafecalls8 | 16888 | +1.3% | 16993 | +0.6% | 13120→13120 | 39→39 | 15→15 |
| dag10 | 1261356 | +0.2% | 1270950 | +0.8% | 1138197→1117742 | 7164→7164 | 2557→2557 |
| cmp-pos | 3169500 | -2.0% | 3114100 | -1.7% | 4156758→4123422 | 7972→7972 | 14647→14647 |
| cmp-neg | 3419350 | -0.2% | 3354233 | -1.9% | 4489607→4452826 | 8310→8310 | 16382→16382 |
| hash | 3836900 | +0.6% | 3866333 | +0.8% | 5399940→5358408 | 11092→11092 | 17781→17781 |
| block2 | 200 | -1.0% | 1550 | +675.0% | 0→1912 | 1→17 | 0→0 |
| block16 | 788 | +0.9% | 7985 | +913.3% | 0→9778 | 1→101 | 0→0 |
| catches8 | 855 | +1.5% | 6954 | +713.3% | 0→7738 | 1→75 | 0→8 |
| switch64 | 3256 | -2.1% | 44476 | +1266.0% | 0→109672 | 1→264 | 0→64 |
| loops8 | 339 | -2.4% | 1432 | +322.4% | 0→1632 | 1→19 | 0→0 |
| goto16 | 2440 | -5.3% | 18704 | +666.6% | 0→19260 | 1→225 | 0→0 |
| blockdag6 | 10624 | -0.3% | 105843 | +896.3% | 528→119563 | 3→885 | 6→378 |
| invokeblock | 761 | +0.0% | 5253 | +590.3% | 648→8938 | 5→30 | 5→31 |
| alpha-pos | 948 | -0.1% | 9857 | +939.8% | 88→11674 | 2→106 | 1→34 |
| alpha-neg | 1027 | +0.0% | 9467 | +821.8% | 88→11834 | 2→112 | 1→1 |
| hash-block | 511 | -1.4% | 5644 | +1004.5% | 88→5873 | 1→53 | 1→34 |
| key-block | 47 | +2.1% | 3691 | +7753.2% | 64→3952 | 0→0 | 1→61 |

Attribution. **Existing families:** every pre-R2 scenario stays within -2.2 % to +3.4 % of master with an A/A control
within -2.0 % to +1.3 %, identical transform and structural visit counts, and allocations at or below master (the binder
frames, hash scopes, binding pairs and label maps are now created on first use; a first run without that change showed
+4 to +16 % bytes on sums and comparer scenarios). `invoke` allocates 16 bytes more (two fields of the inlining analysis).
No regression above 5 %, so no further investigation was needed. **R2 families (semantic change):** they used to be
returned as is (one transform visit, no allocation); they are now traversed, rebuilt, compared, hashed and keyed. Their
per-visit cost is in line with the scalar families (79 ns per transform visit for `block16`, 168 ns for `switch64`,
whose constants are rebuilt by the simplifier, about 60 ns per structural visit for `key-block`); `alpha-*` is dominated
by the public comparer's simplification of both blocks. `blockdag6` unfolds its shared blocks (885 transform visits for 6
levels): blocks are not memoized (P9/P16 ineligible by design, §6), and the cost stays bounded by the visit budgets.

### R3 — Initializers and indexing

| Change | File / method | Tests |
|---|---|---|
| `PrepareMemberInit`, `PrepareListInit`, `PrepareIndex` with binding visitors for MemberAssignment, MemberMemberBinding, MemberListBinding, ElementInit | `ExpressionTransformer` | rows `MemberInit*`, `ListInit*`, `Index*` → `Rewrites`; binding order, `AddMethod`, `Indexer`, constructor identity preserved (`RootMetadata`); nested paths; writable `Index`/`Member` targets of assignments stay writable |
| Equality and hash for the same, including binding order | `ExpressionComparer` | metadata-distinction negative tests; large initializer collections |
| Explicit failure for unsupported binding shapes | rebuilders | `ArgumentException` tests |

#### R3 result (2026-10-30)

Branch `etc/r3-initializers-indexing` (base `0717a8dc`). Commits: `ac369727` (checkpoint A, traversal and reconstruction),
`3b03388a` (B, substitution, inlining, equality, hashing and canonical keys), then checkpoint C (visitor guards, effect and
resource tests, documentation and measurements). Every checkpoint ran the three suites green before the next one started.

**A — Traversal and reconstruction (C-03 extended).** `PrepareTransform` dispatches `MemberInitExpression`,
`ListInitExpression` and `IndexExpression` to `PrepareMemberInit`, `PrepareListInit` and `PrepareIndex` (sealed framework
classes, so the position of the new cases in the dispatch does not matter; the historical families keep their order). The
creation is prepared first and must stay a `NewExpression` (the declaration policy of lambda parameters and block
variables: `InvalidCastException`), then the bindings in declaration order (`PrepareBinding`: a `MemberAssignment` value; the
nested bindings of a `MemberMemberBinding`, depth first; the arguments of each `ElementInit` of a `MemberListBinding`), or the
initializers and their arguments in order (`PrepareElementInits`); an index access prepares its receiver, then its
arguments. Every binding and every initializer counts one structural visit (stack check included), since they are not
expression nodes. Positional child layout (rules, `FinalizeExpression`, `CopyExpression`): `[creation, flattened binding or
initializer expressions…]` for MemberInit and ListInit (`MemberInitChildCount`, `ListInitChildCount`), `[receiver?,
arguments…]` for Index; members, binding kinds, `Add` methods and the indexer are metadata, never children. Named rebuilders
(`RebuildMemberInitExpression`, `RebuildListInitExpression`, `RebuildIndexExpression`, `RebuildMemberAssignment`,
`RebuildMemberMemberBinding`, `RebuildMemberListBinding`, `RebuildElementInit`, and the C-02 entries
`CopyMemberInitExpression`, `CopyListInitExpression`, `CopyIndexExpression`) keep the constructor, members, binding kinds,
order, the exact `AddMethod` (the `ElementInit` overload of `Expression.ListInit`, never the `Add`-discovering one) and the
indexer (`Expression.Property` with the same `PropertyInfo`, or `Expression.ArrayAccess` when there is none, so an index
access that was an assignment target stays writable); they reuse every unchanged node, binding and initializer. A creation
of another type, an index access that would change type, a value or argument that does not fit its member or `Add` method,
and a wrong child count are rejected with `ArgumentException`; an unknown `MemberBinding` class with `NotSupportedException`
(unreachable through the framework factories, which refuse it). The exact simplifier reuses the rebuilt R3 nodes in
`FinalizeExpression` like the R2 families. No new hook, public or protected; no constructor, setter, getter, `Add` method or
accessor is ever run by the engine (traced fixtures assert an empty trace after every transformation). The framework accepts
a `ListInit` without initializer; it has the creation as its only child.

**B — Substitution, inlining, equality, hashing, keys (C-04 extended).** `Substitute` enters the three families with the R2
scope model (they declare nothing: the enclosing `SubstitutionScope` is used as is) and rebuilds them through the A
rebuilders; bindings and initializers count one structural visit each. `InvocationInliningAnalysis` scans them in evaluation
order (creation, bindings and initializers in declaration order; receiver, then arguments): every setter, getter, `Add` call
and indexer accessor counts as user code (`effectSeen`), an array element access writes no variable; argument eligibility is
unchanged, so an argument with effects, or a variable argument read after any of these calls or after a write, keeps the
invocation. `ExpressionComparer.EqualsCore` compares the creation, then the bindings in order (`BindingsEqual`: kind, member,
contents) or the initializers in order (`ElementInitsEqual`: `AddMethod`, arguments), and for an index access the indexer
(including `null`), the receiver (including `null`) and the arguments; `Hash` mirrors it in the same order (so label indices
stay consistent). Canonical keys reuse `StructureKey`, extended with an optional member (bound member or indexer), with
ranks MemberInit 21, ListInit 22, Index 23 (`UnsupportedKey` moved to 24); bindings are keyed with their kind and member,
initializers with their `Add` method. Free parameters keep tying in the keys by design (no order invented between two free
parameters), as before.

**C — Effects, security, documentation.** The R3 families stay unknown to `ExpressionEffectAnalysis`, hence observable as a
whole: the rule gate (`AllowsRules`) and the canonicalization pinning of R1b keep them in place although they are now
structurally comparable (tested with traces: `g[k] + g[k]`, `2*g[k] + 3*g[k]`, out-of-range `a[i] - a[i]` and `a[i] * 0`,
equal initializations read, subtracted or compared by reference, index writes inside sums, observable terms in a sum and a
product). No algebraic rule, purity allow-list or memo eligibility changed (P3/P9/P15/P16 untouched). **Defect found and
fixed during C:** the two engine-internal `ExpressionVisitor`s (`FreeVariables` of the substitution's capture check, and the
`ReferenceFinder` of the inlining analysis) counted only `Visit(Expression)`; the framework visitor walks nested member
bindings without visiting any expression node in between, so a deep `MemberMemberBinding` chain inside a binder (capture
check) or inside a substitution barrier (inlining analysis) recursed without stack check and terminated the process with a
stack overflow (confirmed by removing either guard: the test host crashes). A first fix overriding `VisitMemberBinding` and
`VisitElementInit` to count a structural visit was not enough: the full Security suite still crashed intermittently inside
`ExpressionVisitor.VisitMemberMemberBinding`, which builds a delegate to a virtual method at every level (native runtime
code that can need more stack than the `EnsureSufficientExecutionStack` margin), while the test passed in isolation. Both
visitors now walk bindings and initializers through `ReadOnlyBindingWalk` (one structural visit per binding and per
initializer, expressions visited through the visitor's own `Visit`, nothing rebuilt); the Security suite then passed three
consecutive full runs. The walk is not iterative: nested member bindings still recurse, one plain managed call per level
without any delegate, and every level checks the stack through `ConsumeStructuralVisit` before descending. `CopyExpression`
likewise recurses through nested member bindings (`BindingsChildCount`, `CopyBindings`) behind `EnsureStack` without
consuming the structural budget, so a derived transformer calling it directly is covered by its own Security test
(`DirectFinalizerOnNestedMemberBindings_IsStackGuarded`: faithful copy at depth 200, controlled refusal at depth 100,000 on
a 256 KiB thread inside and outside an operation). `Utils/AGENTS.md` gained the corresponding checklist item. The measurements below were
taken before this last change, which only affects the walks of R3 nodes inside these two visitors.

**Tests.** `ExpressionTreeR3Tests` (19, checkpoint A), `ExpressionTreeR3SubstitutionTests` (5) and
`ExpressionTreeR3EqualityTests` (7, B), `ExpressionTreeR3EffectTests` (6, C), with the instrumented fixtures of
`ExpressionTreeR3Fixtures` (traced constructor, setters, getters, overloaded, two-parameter and throwing `Add`, two traced
indexers, a hiding indexer and a hiding `Add` for metadata-only differences); `ExpressionInitializerResourceTests` (Security,
10: hostile nesting of the five R3 shapes, nested member bindings included, on a 256 KiB thread for 11 walks; default-limit
refusal; budgeted structural walks; 5,000 bindings, 5,000 list-binding initializers, 300 sixteen-argument initializers,
5,000 and 100,000 elements; shared DAG in initializer arguments, validated without unfolding under unlimited limits;
re-entrant comparison; exceptions inside bindings; no user code; no retained reference). Updated characterization: the
R1a pass-through pins, ETC-01/02/06/07/14 findings tests and the "unsupported family" examples of the comparer, batching,
substitution and Security tests retargeted to the remaining R4 families (dynamic operations and extension nodes), each with
the new R3 contract asserted next to it; 7 coverage rows (`// ETC R3`: traversal, substitution and equality columns).

#### R3 measurements (2026-10-30, Release, one process per scenario, 3 rounds, A/A control)

Same harness as R2 (one process per variant and scenario, warmup >= 10 ops and 1.5 s, 25 calibrated batches, variant order
rotated per round), with three more columns measured once per process outside the timing: result nodes that are not nodes
of the input (rebuilt), input depth (ExpressionShapeValidator.Measure, bindings and initializers count as levels) and user
calls made by the operation (a counted constructor and counted setters). Historical scenarios: the R2 set (p3factor12,
twelve terms c_i * x factored, stands for the S5 P3 factoring rules). R3 scenarios: a one-binding initialization (mi1),
16 bindings (mi16), three nested member bindings (mi-nested), a 256-element list binding (mlb256), list initializers of
8, 64 and 1,024 elements (li*), an array access (idx1), a two-argument indexer (idx2), an array element as assignment
target (idx-assign), the public comparer on equal and different 16-binding initializations (mi-eq-pos, mi-eq-neg),
their hash (mi-hash) and canonical key (mi-key), the inlining of a lambda whose body is a list initialization over its
bound parameter (subst-bound), a shared DAG of six list initializations (li-dag6) and an initialization with counted
setters (mi-usercode). R3 at checkpoint C against master `0717a8dc`:

| Scenario | master ns | A/A | R3 ns | R3/master | bytes master→R3 | transform visits master→R3 | structural visits master→R3 | rebuilt nodes master→R3 | input depth | user calls master→R3 |
|---|---|---|---|---|---|---|---|---|---|---|
| scalar | 587 | +0.5% | 580 | -1.2% | 832→832 | 6→6 | 0→0 | 0→0 | 3 | 0→0 |
| unary | 474 | +1.9% | 480 | +1.3% | 576→576 | 5→5 | 0→0 | 2→2 | 4 | 0→0 |
| binary | 14383 | +1.5% | 14203 | -1.3% | 18356→18356 | 33→33 | 91→91 | 2→2 | 4 | 0→0 |
| lambda | 3666 | +2.6% | 3743 | +2.1% | 6937→6937 | 21→21 | 16→16 | 2→2 | 5 | 0→0 |
| invoke | 1137 | +1.8% | 1155 | +1.6% | 1464→1464 | 11→11 | 2→2 | 0→0 | 5 | 0→0 |
| new | 751 | -2.8% | 777 | +3.5% | 960→960 | 7→7 | 0→0 | 1→1 | 4 | 0→0 |
| newarray | 1835 | -0.5% | 1884 | +2.7% | 2416→2416 | 15→15 | 4→4 | 2→2 | 4 | 0→0 |
| member | 1718 | +1.0% | 1751 | +1.9% | 1744→1744 | 12→12 | 12→12 | 5→5 | 5 | 0→0 |
| conditional | 1150 | -0.9% | 1135 | -1.3% | 1440→1440 | 13→13 | 0→0 | 3→3 | 4 | 0→0 |
| leftsum20 | 5010840 | -2.0% | 4793220 | -4.3% | 9667357→9667359 | 11463→11463 | 48925→48925 | 19→19 | 20 | 0→0 |
| rightsum20 | 4988700 | -3.6% | 4821320 | -3.4% | 9667360→9667359 | 11463→11463 | 48925→48925 | 19→19 | 20 | 0→0 |
| balanced64 | 24771400 | +0.1% | 23810900 | -3.9% | 26635743→26635743 | 19215→19215 | 221003→221003 | 191→191 | 8 | 0→0 |
| p3factor12 | 28350 | +1.4% | 27733 | -2.2% | 34447→34447 | 104→104 | 81→81 | 2→2 | 13 | 0→0 |
| p16chain12 | 12928350 | -2.2% | 12645150 | -2.2% | 17247996→17247996 | 34516→34516 | 60080→60080 | 57→57 | 13 | 0→0 |
| p15opaque8 | 20544300 | +1.9% | 20704500 | +0.8% | 18165860→18165726 | 48021→48021 | 130180→130180 | 77→77 | 12 | 0→0 |
| p9family16 | 6543000 | -0.2% | 6391100 | -2.3% | 8087014→8087014 | 9172→9172 | 48935→48935 | 47→47 | 17 | 0→0 |
| dag10 | 1231806 | +0.6% | 1260025 | +2.3% | 1117735→1117735 | 7164→7164 | 2557→2557 | 2→2 | 12 | 0→0 |
| cmp-pos | 3089643 | +1.7% | 3123257 | +1.1% | 4123405→4123405 | 7972→7972 | 14647→14647 | 0→0 | 9 | 0→0 |
| cmp-neg | 3424733 | -1.1% | 3377157 | -1.4% | 4452846→4452834 | 8310→8310 | 16382→16382 | 0→0 | 9 | 0→0 |
| hash | 3747417 | +2.8% | 3928750 | +4.8% | 5358414→5358428 | 11092→11092 | 17781→17781 | 0→0 | 9 | 0→0 |
| block16 | 8016 | -0.8% | 8032 | +0.2% | 9778→9778 | 101→101 | 0→0 | 17→17 | 4 | 0→0 |
| catches8 | 6744 | +2.3% | 6977 | +3.5% | 7738→7738 | 75→75 | 8→8 | 24→24 | 11 | 0→0 |
| switch64 | 43085 | -1.5% | 42979 | -0.2% | 109673→109673 | 264→264 | 64→64 | 129→129 | 4 | 0→0 |
| mi1 | 133 | +0.8% | 620 | +366.2% | 0→648 | 1→5 | 0→1 | 0→1 | 4 | 0→0 |
| mi16 | 725 | +0.8% | 6038 | +732.8% | 0→7849 | 1→50 | 0→16 | 0→1 | 4 | 0→0 |
| mi-nested | 331 | -2.1% | 1724 | +420.8% | 0→2088 | 1→12 | 0→6 | 0→1 | 7 | 0→0 |
| mlb256 | 12707 | +0.0% | 114194 | +798.7% | 0→143720 | 1→770 | 0→257 | 0→1 | 5 | 0→0 |
| li8 | 502 | +1.8% | 3724 | +641.8% | 0→4649 | 1→26 | 0→8 | 0→1 | 4 | 0→0 |
| li64 | 3397 | +2.2% | 27387 | +706.2% | 0→36016 | 1→194 | 0→64 | 0→1 | 4 | 0→0 |
| li1024 | 50864 | -0.0% | 445142 | +775.2% | 0→573738 | 1→3074 | 0→1024 | 0→1 | 4 | 0→0 |
| idx1 | 99 | +0.0% | 614 | +520.2% | 0→720 | 1→6 | 0→0 | 0→3 | 4 | 0→0 |
| idx2 | 125 | -0.8% | 1076 | +760.8% | 0→1208 | 1→11 | 0→0 | 0→4 | 4 | 0→0 |
| idx-assign | 524 | +1.5% | 1012 | +93.1% | 488→1208 | 5→10 | 0→0 | 1→4 | 5 | 0→0 |
| mi-eq-pos | 1512 | +2.4% | 12498 | +726.6% | 80→15779 | 2→100 | 1→66 | 0→0 | 4 | 0→0 |
| mi-eq-neg | 1517 | +1.7% | 12309 | +711.4% | 80→15779 | 2→100 | 1→65 | 0→0 | 4 | 0→0 |
| mi-hash | 779 | -2.7% | 6511 | +735.8% | 72→7953 | 1→50 | 1→50 | 0→0 | 4 | 0→0 |
| mi-key | 46 | +0.0% | 4417 | +9502.2% | 64→7184 | 0→0 | 1→82 | 0→0 | 4 | 0→0 |
| subst-bound | 1362 | +0.1% | 29018 | +2030.5% | 752→46994 | 5→116 | 5→164 | 3→13 | 7 | 0→0 |
| li-dag6 | 11693 | -1.1% | 67048 | +473.4% | 0→67463 | 1→444 | 0→126 | 0→189 | 20 | 0→0 |
| mi-usercode | 177 | -0.6% | 1012 | +471.8% | 0→1128 | 1→9 | 0→2 | 0→1 | 4 | 0→0 |

Attribution. **Existing families:** every historical scenario stays within -4.3 % to +4.8 % of master with an A/A control
within -3.6 % to +2.8 %, identical transform and structural visit counts and identical rebuilt-node counts; allocations
are unchanged (a few bytes of noise on the comparer scenarios). hash (+4.8 % against an A/A of +2.8 %) is the only
scenario above the A/A range; it does not traverse any R3 node, its visit counts and allocations are unchanged, and the
Hash dispatch only gained three type patterns after every historical one, so it is attributed to noise; no regression
reaches 5 %, so no further investigation was needed. **R3 families (semantic change):** they used to be returned as is
(one transform visit, no allocation); they are now traversed, rebuilt, substituted, compared, hashed and keyed. Their cost
per transform visit is in line with the other families (about 125 ns for mi1, 145 ns for li1024, 150 ns for mlb256,
whose constants are rebuilt by the simplifier); mi-eq-* is dominated by the public comparer's simplification of both
trees; mi-key builds 82 keys where master built a single unsupported key; subst-bound now inlines the invocation (116
transform visits instead of 5, 13 rebuilt nodes); li-dag6 unfolds its shared initializations (444 transform visits for 6
levels): initializations are not memoized (P9/P16 ineligible by design, §6), and the cost stays bounded by the visit budgets.
No scenario runs user code (mi-usercode: 0 calls).

Post-review campaign (2026-10-30, same harness, 3 rounds, variant order rotated): the measured build (r3-pre, before
`ReadOnlyBindingWalk`) against the final code (r3-post) and master, on the 17 R3 scenarios and six historical controls
(scalar, lambda, invoke, hash, cmp-pos, p16chain12). r3-post against r3-pre stays within -4.4 % to +4.0 % (subst-bound,
the only scenario whose capture check walks R3 bindings, -4.4 %), with identical transform, structural, rebuilt-node and
user-call counts on every scenario; the historical controls stay within -2.0 % to +3.5 % of master (hash -0.7 %, so the
earlier +4.8 % was noise). The fix therefore changes neither the work nor the cost of the measured scenarios.
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
| R0 complement | 2026-10-27 | `Utils/AGENTS.md` local switch exception. Resource campaign (`tests/ExpressionResourceProbe`, Windows + WSL2 Linux, 6 thread/stack configurations, families A–G, CSyntax and VBSyntax): ETC-16 … ETC-19 added (§7). Experiments, never merged: `TransformCore` visit counter and unit-suite visit histogram (accepted as calibration data); stack guards at five recursive entry points (overhead within noise, controlled failure and state restoration observed: accepted as the R1a-Security design basis, §12). No production change. |
| R1a | 2026-10-28 | ETC-08, ETC-09 and ETC-13 fixed, ETC-11 guarded (no unbound parameter, argument evaluation preserved); C-02 and C-07 (a) adopted; 15 baseline rows moved from `Throws`/`Corrupts` to `Opaque`/`Same` (pass-through, not support); no supported-family change; no regression above 5 % (§9 R1a result). |
| R1a-Security | 2026-10-28 | Resource limits, per-thread budget session, iterative validator, stack guards on every engine walk, nested-call and re-entrancy policy, Stack translation; ETC-19 fixed and ETC-17 bounded for the ETC engine; rule invocation no longer re-wraps resource failures (stack overflow found and fixed); measured overhead documented (§9 R1a-Security result). |
| R1b | 2026-10-29 | Member, TypeBinary, New, NewArray traversed and Default an explicit leaf (C-03); faithful unary and lambda reconstruction for every transformer (C-01, ETC-04/05 fixed); scalar substitution; ETC-10 fixed; ETC-15 resolved for the simplifier with the strict C-06 boundary (effect analysis, rule gate, canonicalization pinning, division identities); S5 scenarios reclassified with an inert carrier; performance in §9 R1b/R1c measurements. |
| R1c | 2026-10-29 | Structural equality, hashing and canonical keys of Conditional, Invoke, TypeIs, TypeEqual, Default, New and NewArray (C-04, ETC-06/07/14 resolved for these families); structural equality kept apart from algebraic interchangeability; resource tests for every new walker. |
| R2 | 2026-10-29 | Block, Try, Switch, Loop, Goto, Label traversed and faithfully rebuilt, RuntimeVariables and DebugInfo explicit (C-03); binder frames and label bijection in equality, hashing and canonical keys (C-04); capture-avoiding substitution with alpha-renaming, RuntimeVariables C-07, Quote untouched (C-05 pending); binder-aware inlining analysis; S4 block/catch scopes; R2 families observable for the rules (ETC-15 kept); resource tests for every new walk; ETC-01/02/03 advanced, ETC-06/07/14 extended; performance in §9 R2 measurements. Follow-up in the same PR: shared DAGs validated in linear time (pre-existing exponential validation under high node limits, §9 R1a-Security "Shared DAG validation"). |
| R3 | 2026-10-30 | MemberInit (MemberAssignment, MemberMemberBinding, MemberListBinding), ListInit (ElementInit) and Index traversed and faithfully rebuilt (C-03, C-02: flattened positional layouts, exact `Add` methods, indexers, writable targets); capture-avoiding substitution and conservative inlining through them; structural equality, hashing and canonical keys (C-04); the families stay observable for the rules (ETC-15 kept); stack guards added to the two engine-internal visitors for nested member bindings (overflow found and fixed); ETC-01/02/03 advanced, ETC-06/07/14 extended; R4 (Dynamic, Extension) and C-05 open; performance in §9 R3 measurements. |

## 11. Decisions requested from the reviewer

1. C-01 … C-07 (table §4) and S-01 … S-06 (§12.8).
2. Whether R1a may ship before the rest of R1 (recommended: yes, it only removes corruption and exceptions).
3. ~~Whether the dispatch switches are converted to dictionaries~~ — resolved 2026-10-27: `Utils/AGENTS.md` allows node-type
   dispatch `switch`es with more than ten short cases. `PrepareTransform`, `CopyExpression`, `EqualsCore` and `Hash` stay
   `switch`es; a handler dictionary is adopted only if a measurement or an architectural reason justifies it.
4. Whether the parser findings (ETC-16, ETC-18) open a dedicated `Utils.Parser` security stage (recommended: yes, before any
   further parser feature work; they are outside the ETC code scope).

## 12. ETC Security — Resource budgets and stack safety

Goal: no expression, however pathological, may terminate the process or consume unbounded CPU or memory through the public
entry points; every excess ends in a controlled, documented exception and leaves no ambient state behind. This stage
**precedes** every structural extension that can deepen recursion (R1c and later), and follows R1a.

### 12.1 Three independent dimensions

| Dimension | What it bounds | Measured basis (§7) | Initial default | Notes |
|---|---|---|---|---|
| A. Structural depth | stack use of every recursive walk | worst first overflow ≈ 2,000 levels on 1 MB stacks (Windows main/pool, Linux 1 MiB), 250 on a 256 KiB thread, 10,000+ on Linux 8 MB | **512** | ≥ 3.9× margin on 1 MB stacks; **not** safe alone on small stacks (256 KiB fails at 250) and does not cover the extra stack of nested comparer sessions (nesting up to 14 observed): the stack guard (12.3) is the backstop, the depth limit is the early, predictable refusal |
| B. Node count | size of the input, structural-walk cost | transform/rawhash walks of 10,000–40,000 nodes take ≤ 6 ms; unshared polynomial of 25 M logical nodes hits 4 GB; shared DAG of 397 distinct nodes = 10,099 logical | **50,000 logical nodes**, plus the same bound on distinct nodes | counted as **logical occurrences** (the unfolded size), because today's comparer, hash, canonical key and transformer walks do not memoize shared subtrees; computed by the DAG-aware meter without unfolding |
| C. Cumulative work | CPU and allocations of simplification | 1–4 µs and 0.8–6.5 KB per `TransformCore` visit; ordinary unit-test calls ≤ 10,000 visits (99.4 %), largest 353,625; F-g 20 terms > 5.5 M visits | **1,000,000 visits** per outermost public call (≈ 1–4 s, < 7 GB cumulative allocation) | must be confirmed on the full test corpus and the S5 benchmark families before adoption; the tests that legitimately exceed it (S5 regression tests) pass an explicit larger budget |

A depth or node limit alone is insufficient: F-g at depth 21 with 79 nodes costs 56 s (§7.4).

### 12.2 Smallest useful work counters

1. **`TransformCore` visits**, counted per outermost public call and **including** nested comparer simplifications, P16 re-entries
   and rule re-entries: one counter covers rule entries, comparer re-simplifications and recursive preparation (they all go
   through `TransformCore`). Measured correlation with time and allocation is close (§7.4).
2. **Structural-walk node visits** (`EqualsCore`, `Hash`, `ExpressionCanonicalOrder.Build`, `ReplaceArgumentsCore`): these walks
   also run outside `TransformCore` (public `Equals` on already simplified trees, canonical keys during finalization). One
   shared counter, default bound 10× the visit budget.

Not counted separately: rule entries (a subset of visits), comparisons (covered by both counters), canonicalization passes
(covered by counter 2). The P9/P15 memo traversals are already bounded by their own node budget.

### 12.3 Stack guard

- `RuntimeHelpers.EnsureSufficientExecutionStack()` at the entry of every recursive method of the expression engine:
  `ExpressionTransformer.TransformCore`, `ReplaceArgumentsCore`, `ExpressionComparer.EqualsCore` and `Hash` (and the
  `ParameterBindingContext`-free raw variants), `ExpressionCanonicalOrder.Build`, the P15 observed-memo key hashing, and every
  new `Prepare*`/rebuild/equality method added by R1–R4. Once per call is cheap enough (0.7–6 ns; no measurable overhead,
  §7.5); no sampling.
- It is a backstop for small or unknown stacks, not a substitute for limit A: its headroom is fixed and segments without a
  guard (framework factories, user code) consume it.
- Thread kinds: verified on main, thread-pool and explicit-stack threads on Windows and Linux (§7.1).
- Re-entrancy: a guard in a nested public call (user code calling `Simplify` from an `Equals` override) sees the real
  remaining stack, which is what is wanted.

### 12.4 Non-recursive pre-validation

`tests/ExpressionResourceProbe/ExpressionShapeMeter.cs` is the prototype: explicit stack, post-order dynamic programming over
**distinct** nodes (reference identity), giving distinct count, logical count (saturating, never unfolding) and depth in
O(distinct nodes) time and memory, stopping at a distinct-node cap. It reads only framework public properties; an
`Extension` node (or any node of an unknown class) is an opaque leaf: `Reduce`, `CanReduce`, `VisitChildren` and custom
getters are never called. The only user code it can run is the virtual `NodeType` getter of a user subclass, which every
dispatcher already reads. Validation runs once per outermost public call, before any transformation.

### 12.5 Public contract

| Question | Proposal |
|---|---|
| 1. Where do limits apply? | at the outermost public call on a thread: `ExpressionSimplifier.Simplify`/`Transform`, `ExpressionExtensions.Simplify`, `ExpressionTransformer.TransformCore` when no budget scope is active (covers every subclass, since `Transform` implementations must call it), `ExpressionComparer.Equals`/`GetHashCode`, `ExpressionUtils.Equals`, `ReplaceArguments`, and the derivation/integration entry points of `Utils.Mathematics` (they call the simplifier) |
| 2. Common limits? | one shared options type for the three components, so a comparer and the simplifier it uses agree |
| 3. Per instance? | yes: immutable options passed to a constructor or factory; `Default` instances keep the documented defaults; no global mutable setting |
| 4. Simplify vs hash consistency | `Equals` and `GetHashCode` validate the same input with the same options before simplifying; the reference fast path of `Equals(x, x)` stays (O(1)), documented as the only call that succeeds on an over-limit tree |
| 5. Exception | new `ExpressionResourceLimitException : InvalidOperationException` with `Kind` (`Depth`, `Nodes`, `Work`, `Stack`), `Limit`, `Observed`; a stack-guard `InsufficientExecutionStackException` is wrapped (as inner exception) at the outermost public boundary only |
| 6. Excess vs unsupported node | unsupported nodes keep their own contract (pass-through or `NotSupportedException`, C-07); `ExpressionResourceLimitException` is reserved for budgets |
| 7. Re-entrancy | a nested public call made from user code during an outer call **joins** the outer work budget (otherwise user code can multiply the work without bound) but validates its own input; decision S-03 |
| 8. Isolation | budget scope is `[ThreadStatic]` and saved/restored around every outermost call, like the P9 session (`Transform` already detaches and restores it); no static mutable counters |
| 9. Masking user exceptions | checks run only at check points (method entry), never in `finally`/`catch` blocks, so an in-flight user exception is never replaced; scope restoration in `finally` does not throw |
| 10. Subclasses | enforcement lives in non-virtual `TransformCore` and the protected helpers, so third-party subclasses inherit it; they read the active options through a protected property; no new virtual hook |

Not changed in R0: no public signature.

### 12.6 Minimal API for the correction round

```csharp
public sealed record ExpressionResourceLimits
{
    public static ExpressionResourceLimits Default { get; } = new();
    public int MaxDepth { get; init; } = 512;
    public long MaxNodes { get; init; } = 50_000;            // logical occurrences; also applied to distinct nodes
    public long MaxTransformVisits { get; init; } = 1_000_000;
    public long MaxStructuralVisits { get; init; } = 10_000_000;
}

public enum ExpressionResourceLimitKind { Depth, Nodes, Work, Stack }

public sealed class ExpressionResourceLimitException : InvalidOperationException
{
    public ExpressionResourceLimitKind Kind { get; }
    public long Limit { get; }
    public long Observed { get; }
}

// ExpressionSimplifier(ExpressionResourceLimits limits); ExpressionComparer.Create(ExpressionResourceLimits limits);
// ExpressionTransformer: protected ExpressionResourceLimits ResourceLimits { get; } (constructor overload).
```

Explicitly unlimited configurations (`int.MaxValue`/`long.MaxValue`) remain possible for trusted inputs; the stack guard
stays active regardless.

### 12.7 Tests to prepare (UtilsTest.Security: resource-exhaustion invariants)

- In-process (safe sizes only): controlled refusal for excessive depth (validator), excessive logical nodes (including a
  shared DAG of few distinct nodes), exhausted work budget (F-g 20, balanced sum 1,000) with the exact exception kind and
  limits; acceptance just below each limit; no refusal of an acceptable arithmetic family (left/right/balanced sums, Horner,
  repeated term at the same size); a tree that passes validation but exceeds the budget during simplification (comparer
  re-simplification); ambient state (lexical scope, P9/P15 sessions, metrics, comparer depth) identical before and after a
  refused call; nested public call from user code consuming the outer budget; concurrent independent calls on separate
  threads with different options; subclass of `ExpressionTransformer` inheriting the limits; `Equals`/`GetHashCode`
  consistency on over-limit inputs.
- Isolated process (the probe or an equivalent child process started by the test): depths that overflow today (left sum
  10,000, Invoke 5,000, `Abs` chain 5,000; 256 KiB thread at 1,000) must end with a controlled exception and exit code,
  never with a stack overflow; the test asserts on the child's exit code and output.
- Parser stage (separate): `((…(x0)…))` with 20/18 pairs and beyond.

### 12.8 Decisions for this stage

| Id | Question | Recommendation |
|---|---|---|
| S-01 | Default values | depth 512, 50,000 logical nodes, 1,000,000 visits, 10,000,000 structural visits; confirm the visit budget on the full Unit/Functional/Security suites and S5 families first |
| S-02 | Exception type | new `ExpressionResourceLimitException : InvalidOperationException` (12.5) |
| S-03 | Nested public calls from user code | join the outer work budget |
| S-04 | Default limits on by default | yes for `Default` instances; it is a 2.0.0 behavior change (record in the batched release notes) |
| S-05 | Where the stack guard goes | every recursive method of the engine (12.3), unconditionally |
| S-06 | Parser protection | dedicated `Utils.Parser` stage: nesting limit and guard in the grammar engine, input-length guidance |

S-01 … S-05 are applied by R1a-Security (§9, "R1a-Security result"); the defaults were kept after the full Unit,
Functional and Security suites and the S5 benchmark families passed with them. S-06 remains open.

### 12.9 Required order of rounds

1. **R1a** — correction of existing corruptions and exceptions (ETC-08, ETC-09, ETC-11 guard, ETC-13).
2. **R1a-Security** — this stage: validator, budgets, stack guards, exception type, tests of 12.7.
3. **R1b** — scalar traversal and reconstruction.
4. **R1c** — equality, hashing and canonical keys of the scalar families (needs the stack guards of R1a-Security).
5. **R2–R4** — binding/control flow, initializers, special nodes; every new recursive method ships with its guard and is
   counted by the structural-visit budget.
