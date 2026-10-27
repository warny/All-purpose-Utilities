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
| ETC-16 | **Critical (security)** | `Utils.Parser` `CompiledGrammar.Parse`, reached by `CSyntaxTokenParser.Parse`, `VBSyntaxTokenParser.Parse` and every `CompileExpression(string, …)` | `new CSyntaxExpressionCompiler().CompileExpression("((((((((((((((((((((x0))))))))))))))))))))")` | stack overflow terminates the process at 20 parenthesis pairs (CSyntax) / 18 (VBSyntax) on a default Windows thread, 2–3 on a 256 KiB thread, 16 on Linux 1 MiB, 150 on Linux 8 MB; about 50 KB of stack per nesting level; no depth limit, no guard | controlled exception for excessive nesting, configurable limit | public parsers of `Utils.Expressions.*`, `Utils.Parser.Expressions` (inherits), possibly every `Utils.Parser` grammar | dedicated `Utils.Parser` stage (S-06) | probe `E-*-parens`, `E-*-nested`; `results/2026-10-27/win-parser-nesting.csv`, `win-vb-nesting.csv`, `*-parser.csv` | Confirmed (out of ETC code scope, not fixed here) |
| ETC-17 | High (security, availability) | `ExpressionSimplifier` rule/comparer interplay (additive canonicalization, factoring probes, comparer re-simplification) | `Simplify(x0 + x1 + … + x99)` (100 distinct parameters) | 1.5 s, 397k visits, 684k comparer re-simplifications, 2.5 GB allocated; 250 terms > 60 s; balanced 1,000 terms 30 s / 31 GB; F-g 20 terms (79 nodes) 56 s / 27.6 GB | bounded work with a controlled refusal (work budget); algorithmic improvement is a separate S5 continuation | S, D, E, H (public comparer simplifies) | R1a-Security (budget); S5 follow-up (algorithm) | `results/2026-10-27/win-simplify.csv`, `win-comparer.csv`, `win-combinatorial.csv`, `win-work-counters.csv` | Confirmed |
| ETC-18 | Medium (security) | `Utils.Parser` engine | CSyntax/VBSyntax parse of `x0 + … + x19999` (169 KB) | 6.2 s / 5.2 GB (CSyntax parse), 12.1 s / 4.7 GB (VBSyntax compile): about 30 KB allocated per input character, linear | allocation proportional to a documented, small constant; input-length guidance | parsers | `Utils.Parser` stage (S-06) | `win-parser.csv`, `win-vb-sum.csv` | Confirmed (out of ETC code scope) |
| ETC-19 | High (security) | every recursive path: `TransformCore`/`Prepare*`, `ReplaceArgumentsCore`, `ExpressionComparer.EqualsCore`/`Hash`, `ExpressionCanonicalOrder.Build` | left sum of 3,000 terms through `ReplaceArguments`; `Abs` chain of 2,000 through the canonical order | uncatchable stack overflow at about 2,000–5,000 levels on 1 MB stacks, from 250 on 256 KiB stacks; no `EnsureSufficientExecutionStack` anywhere | controlled `ExpressionResourceLimitException` (depth limit + stack guard) | G, S, D, R, E, H, canonical order | R1a-Security | §7.2, §7.5; `win-structural.csv`, `win-*-structural.csv`, `linux-*-structural.csv` | Confirmed |

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

### R1a-Security — Resource budgets and stack safety (second)

Specified in §12 (ETC Security). File-level scope: new `Utils/Expressions/ExpressionResourceLimits.cs`,
`ExpressionResourceLimitException.cs`, an iterative validator (from `tests/ExpressionResourceProbe/ExpressionShapeMeter.cs`),
budget scope in `ExpressionTransformer.TransformCore` / `ExpressionSimplifier.Transform` / `TransformWithinBoundary`,
`ExpressionComparer.Equals`/`GetHashCode` and the raw structural entry points, `ReplaceArgumentsCore`, stack guards at every
recursive method listed in §12.3. Tests: §12.7.

### R1b — Scalar traversal and reconstruction

| Change | File / method | Tests |
|---|---|---|
| Add `PrepareMember`, `PrepareTypeBinary`, `PrepareNew`, `PrepareNewArray`; `Default` explicit leaf. `PrepareTransform` stays a type-pattern dispatch `switch` (allowed by the local exception of `Utils/AGENTS.md`); dispatch-order change benchmarked on the hot path | `ExpressionTransformer.PrepareTransform` and new `Prepare*` | rows flip to `Rewrites` for G/S/D; preparation order test per family (H-2); subclass rule shape test (H-1) |
| Per-family rebuilders called from the `CopyExpression` dispatch `switch` (kept as a `switch`; each branch becomes a short call): `MakeMemberAccess`, `TypeIs`/`TypeEqual`, `New(ctor, args, members)` and `New(type)`, `NewArrayInit`/`NewArrayBounds` | `CopyExpression` + named `Rebuild*` methods | `RootMetadata` unchanged for every R1 sample (G, R) |
| C-01 faithful unary/lambda rebuild as base default | `RebuildUnaryExpression`, `RebuildLambdaExpression`, `CopyUnaryExpression`, `CopyExpression` unary branches, `ReplaceArgumentsCore` | rows `Negate.CustomMethod`, `Convert.CustomMethod`, `Throw`, `Lambda.Metadata` → `Rewrites` in G/D/R; update `ExpressionTransformerUnaryLazyParametersTests` only where it pins the loss (recorded here) |
| C-06 side-effect opacity | `ExpressionSimplifier` rule guards (`IsOrdinaryBinaryArithmetic`/operand classification used by additive/multiplicative grouping, `AdditionOfEqualsElements`, factoring, zero rules) | `ETC15_*` flipped to evaluation-preserving results; S5 corpora unchanged |
| Substitution through Member, TypeBinary, New, NewArray, Unary (faithful) | `ReplaceArgumentsCore` | rows → `Rewrites` in R |

### R1c — Scalar equality and hashing

| Change | File / method | Tests |
|---|---|---|
| Equality + hash for Conditional, Invoke, TypeBinary, Default, New (ctor, members), NewArray | `ExpressionComparer.EqualsCore`, `Hash` (dispatch `switch`es with one short call per family, `Utils/AGENTS.md` exception) | positive/negative pairs per metadata field; `Dictionary`/`HashSet` membership with identical references and independent trees; reflexivity/symmetry/transitivity on catalog pairs |
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
| R0 complement | 2026-10-27 | `Utils/AGENTS.md` local switch exception. Resource campaign (`tests/ExpressionResourceProbe`, Windows + WSL2 Linux, 6 thread/stack configurations, families A–G, CSyntax and VBSyntax): ETC-16 … ETC-19 added (§7). Experiments, never merged: `TransformCore` visit counter and unit-suite visit histogram (accepted as calibration data); stack guards at five recursive entry points (overhead within noise, controlled failure and state restoration observed: accepted as the R1a-Security design basis, §12). No production change. |

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

### 12.9 Required order of rounds

1. **R1a** — correction of existing corruptions and exceptions (ETC-08, ETC-09, ETC-11 guard, ETC-13).
2. **R1a-Security** — this stage: validator, budgets, stack guards, exception type, tests of 12.7.
3. **R1b** — scalar traversal and reconstruction.
4. **R1c** — equality, hashing and canonical keys of the scalar families (needs the stack guards of R1a-Security).
5. **R2–R4** — binding/control flow, initializers, special nodes; every new recursive method ships with its guard and is
   counted by the structural-visit budget.
