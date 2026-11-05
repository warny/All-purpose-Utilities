# Expression behavior analysis — design note (S5 P18 Architecture A)

Status: implemented as a read-only foundation. Architecture B (public options) is described in
[SimplificationPolicies.md](SimplificationPolicies.md); since Architecture C (stages C1 and C2) the zero and one identities ask
`CanDiscard` of this analysis under a non-default policy (§8 of that note); the default policy never consults it.
Base: `master` `ad237762` (after PR #680). Scope: Architecture A only; Architectures B and C are not started.

Naming: this "P18 Architecture A" is the behavior-analysis foundation requested on 2026-11-04. It is distinct from
the correctness fixes the S5 deep audit called P18a/P18b/P18c (S5A-01 … S5A-13), which remain open.

## 1. Purpose

The simplifier needs to know, before it drops, repeats, merges or moves an evaluation, whether that evaluation is
reproducible, free of side effects, and which exceptions it may raise. Since ETC R1b the answer comes from
`ExpressionEffectAnalysis`, a strict internal gate. It cannot express determinism, the origin of an exception, the
parts it did not analyze, or a user's promise that a method is pure; and it does not look inside lambdas or blocks.

Architecture A adds a separate, public, read-only classification (`ExpressionBehaviorAnalysis`) and a declaration
attribute (`[Deterministic]`), with the permissions and the bridge a future policy would need. Nothing is wired into
`ExpressionSimplifier`, `ExpressionComparer` or their rule gates: every simplification result, every observable user call
and every comparer answer is unchanged.

## 2. Audit of the existing mechanisms

| Mechanism | Where | What it does |
|---|---|---|
| `ExpressionEffectAnalysis` | `Utils/Expressions/ExpressionEffectAnalysis.cs` | Flags `ReadsMutableState`, `MayThrow`, `Allocates`, `Observable`; permissions `CanDiscard`/`CanRepeat`/`CanReorder`/`IsInert`; bounded pass then per-operation table. |
| Rule gate `AllowsRules` | `ExpressionSimplifier.cs` | A node's rules run only when its operands are inert (or the node is an invocation, handled by the inlining analysis). |
| `CanonicalizesOperator` | `ExpressionSimplifier.cs` | Reassociation only for operators without effects (excludes `decimal`). |
| `SplitPinnedTerms` | `ExpressionSimplifier.cs` | Terms that are not inert keep their source order and occurrences. |
| `IsEliminableFloatingOperand` | `ExpressionSimplifier.cs` | Narrow whitelist for the NaN fold (S5 P11). |
| `InvocationInliningAnalysis` | `InvocationInliningAnalysis.cs` | Decides whether `Invoke(lambda, args)` can be inlined (evaluation count and order of arguments). |
| Opacity | `ExpressionOpacity.cs` | Extension nodes are recognized by class only and never read. |

Findings (none changes a result today; none is fixed here, as the scope forbids behavior changes):

- **A-1** `ArrayIndex` is `MayThrow` but not `ReadsMutableState`, although an element is mutable state. Harmless: a
  `MayThrow` term is already pinned.
- **A-2** Checked `double`/`float` arithmetic (`AddChecked`, `NegateChecked` …) is `MayThrow`; it never throws.
  Conservative.
- **A-3** Any operator method declared by `decimal` or `string` is inert (`OperatorMethodEffects`): trust by declaring
  type, not by method. `Expression.Add(a, b, string.Concat)` is inert although it allocates.
- **A-4** A quote is inert (data). Evaluating it builds a tree, and with hoisted variables it captures them like a
  closure.
- **A-5** A lambda is `Allocates` and its body is ignored (correct for its evaluation); a lambda invoked in place is
  `Observable`; blocks and local variables are always `Observable` (no scope model).
- **A-6** Conversions with a method are trusted only for `decimal`; `Half`, `Int128`, `UInt128` and `BigInteger`
  operators and conversions are observable. Conservative.
- **A-7** The total floating-point list requires every parameter to have the result type, which excludes `ScaleB`,
  `ILogB`, `RootN`, the `Is*` predicates and every integer overload of `Math` (`Max(int, int)` is observable).
  Conservative.
- **A-8** Clocks, random generators and property getters are all `Observable`: nondeterminism is not distinguished from
  side effects or from unknown code.
- **A-9** Custom operators and user methods are always `Observable`; there is no declaration route (`[Pure]` is
  deliberately not trusted, ETC R1b).
- **A-10 (confirmed, fixed: see [A10-ReflectionProvenance.md](A10-ReflectionProvenance.md))** The method cache and classifier read the members of the `MethodInfo`
  (`GetHashCode`, `DeclaringType`, `Attributes`, `GetParameters`, `ReturnType`, `Name`). `Expression.Call` accepts a user
  subclass of `MethodInfo`; one that forwards to `Math.Sin` and reports its metadata is classified inert by the gate, and
  `Simplify(FakeSin(x) * 0)` returns `0`: a user call is dropped on the strength of metadata the gate did not obtain from the
  runtime. It needs a hand-built call node with a custom reflection object. Not fixed in P18-A (no behavior change
  allowed); fixed by the A-10 pull request, which also found the same trust in the call-rule signatures, the
  ordinary-arithmetic checks, the field classification and the inlining analysis.
- **A-11** `IsSafeDivisor` accepted every nonzero `decimal` divisor (S5A-13). Fixed by P18b: a decimal divisor is safe only
  when its magnitude is at least 1. The behavior analysis was not affected (a decimal division carries `decimal.op_Division`).
- **A-12** Constants are inert whatever their value; the user code of a constant's `Equals`/`GetHashCode` belongs to the
  comparer, not to the evaluation (documented on `ExpressionSimplifier`).

## 3. Representation

`ExpressionBehaviors` (public `[Flags]` enum) — every flag but the last is a *may* fact: an absent flag is a guarantee.

| Flag | Meaning |
|---|---|
| `ReadsMutableState` | Reads a free or captured variable, a non-read-only field, an array element, or the state of an object passed to a declared method. |
| `Nondeterministic` | Two evaluations in the same state may differ (clocks, random generators, fresh identifiers, unknown code). |
| `SideEffects` | Changes state that outlives the evaluation (writes to free or captured variables, fields, elements, properties; state-advancing members; unknown code). |
| `Allocates` | Allocates an object whose identity is observable. |
| `MayThrowArithmetic` | Exceptions of built-in arithmetic and trusted numeric functions (overflow, division by zero, NaN sign, invalid numeric arguments). |
| `MayThrowRuntime` | Other runtime exceptions (null dereference, invalid cast or unboxing, absent nullable value, index or size out of range). |
| `MayThrowUserCode` | Exceptions of user code: a `throw` node, a declared method, a known framework member, unknown code. |
| `Unknown` | Part of the evaluation was not analyzed. Always comes with every other may-flag (`UnknownCode`). |
| `ReliesOnDeclaration` | Provenance: part of the answer relies on a `[Deterministic]` declaration. |

`ExpressionBehavior` (public `readonly record struct`) wraps the flags with the derived guarantees `IsFullyAnalyzed`,
`IsDeterministic`, `IsSideEffectFree`, `IsReproducible`, `MayThrow`, `MayThrowUserCode`, `ReliesOnDeclarations`.

Choices:

- **Unknown carries every flag.** A consumer that tests one flag never mistakes unanalyzed code for safe code; `Unknown`
  only explains why.
- **Determinism is "same state, same outcome".** Reading mutable state does not make an expression nondeterministic: a
  deterministic method called with a free parameter is a deterministic expression that reads mutable state.
  `IsReproducible` is the stronger property (no state read, no effect, no allocation).
- **Exceptions are split by origin**, so a future policy can, for example, accept merging an expression that may overflow
  while refusing one that runs user code.

## 4. `[Deterministic]`

`Utils.Expressions.DeterministicAttribute` (methods only, not inherited, not repeatable). Contract, within one process:
the outcome depends only on the arguments (receiver included) and on the state of the objects they reference — equal
results, or exceptions of the same type; no global or ambient state (static mutable fields, clocks, random generators,
environment, culture, I/O); no observable side effect. Exceptions and allocation are outside the promise.

The analysis honors it only where it is bound to the code that runs and speaks for all of it:

- on a static method, a non-virtual instance method, a sealed override, or a method of a sealed or value type — not on an
  overridable virtual or abstract method (an override could run instead);
- not on an open generic method, nor one with a by-reference or pointer parameter or return, nor one taking a delegate or
  an expression tree (code the declaration cannot speak for);
- read on the exact method (`IsDefined(…, inherit: false)`), recognized by its type identity only: another library's
  attribute of the same name, and `[Pure]`, are ignored.

A declared call is `ReliesOnDeclaration | MayThrowUserCode`, plus `ReadsMutableState` when its receiver or a parameter
can reference mutable state (any reference type but `string`, or a value type holding one in a field, recursively), plus
`Allocates` when it returns a reference type. Custom operators and conversions are declared on their operator method;
getters on their accessor.

The attribute is never treated as proof: a lying declaration cannot change any result today (no policy reads it), and the
verified policy prepared for later treats it as unknown code.

## 5. Trusted built-in numeric functions

`ExpressionMethodBehaviors` trusts a method only when **all** hold:

1. its declaring type is one of `Math`, `MathF`, `double`, `float`, `Half`, `decimal`, `sbyte`, `byte`, `short`, `ushort`,
   `int`, `uint`, `long`, `ulong`, `nint`, `nuint`, `Int128`, `UInt128`, `BigInteger` — the method itself, not an
   interface or an inherited member;
2. it is a public, static, non-generic runtime method;
3. every parameter and the result are numeric types of that list, `bool` or `MidpointRounding`, by value;
4. its name is listed for the numeric family of its signature (floating, decimal, integer, `BigInteger`; for `Math`/`MathF`
   the family of the first parameter).

Each listed name is total (`None`) or partial (`MayThrowArithmetic`) per family; `Round` is total only with one parameter;
`op_Checked*` and `op_Explicit` are always partial. Highlights:

- floating: the IEEE functions (trigonometric, hyperbolic, `*Pi`, exponential and logarithmic families, `Pow`, `Sqrt`, `Cbrt`,
  `Hypot`, `RootN`, `ScaleB`, `ILogB`, `FusedMultiplyAdd`, `Lerp`, rounding without digits, `Min`/`Max` variants, `Is*`
  predicates, `Half` operators) are total; `Sign` (NaN), `Clamp` (reversed bounds), `Round` with digits or mode are partial;
- decimal: rounding, `Abs`, `Negate`, comparisons, `Sign`, `CopySign`, implicit conversions are total; arithmetic, `Clamp`,
  rounding with digits or mode, explicit conversions are partial;
- integer: `Min`/`Max` variants, `Sign`, bit counts and rotations, unchecked and bitwise operators, implicit conversions are
  total; `Abs` and `CopySign` (minimum value), `Log2` (negative), `Clamp`, division and remainder, checked and explicit
  conversions are partial;
- `BigInteger`: addition, subtraction, multiplication, negation, comparisons, `GreatestCommonDivisor`, `Log`/`Log10` are
  total; division, remainder, `Pow`, `ModPow`, `Log2`, shifts, `Clamp`, explicit conversions are partial.

Not trusted: estimate functions (`ReciprocalEstimate`, `ReciprocalSqrtEstimate`, whose result may depend on the code
path), parsing and formatting (culture), tuple-returning `DivRem`, by-reference overloads (`BigMul(…, out)`), static
abstract interface members (`INumberBase<double>.Abs`), and every user type, including user implementations of
`INumber<T>`, `IAdditionOperators<…>` and the other static numeric interfaces, user methods named `Sin` or `Abs`, and
extension methods.

Verification: the unit test `TrustedFunctions_TotalOnesNeverThrowOnEdgeValues` invokes every total function of the running
runtime (more than 300 signatures) on the edge values of its signature (zeros, ±1, extremes, NaN, infinities, epsilon,
invalid `MidpointRounding`). The partial classification was established with the same probe (any throwing signature is
partial). This is evidence, not proof: a total classification is also backed by the documented IEEE semantics.

Reproducibility of trusted functions holds within a process; their last bits may differ across platforms or runtime
versions.

### Preconditions inventory

The edge-value test is evidence, not a proof of totality over every argument combination, so the list is kept
conservative. Every total function that takes a parameter other than its values (a count, a bound, a base, a mode)
is listed here with the reason it cannot throw; every partial function, with the precondition that makes it throw.

| Function (family) | Classification | Argument that could be a precondition | Why |
|---|---|---|---|
| `ScaleB(x, n)` (floating) | total | `n` any `int` | saturates to ±∞ or ±0 |
| `RootN(x, n)` (floating) | total | `n` any `int` | `n = 0` gives NaN |
| `ILogB(x)` (floating) | total | – | NaN, 0 and ∞ map to defined `int` values |
| `Log(x, base)` (floating, `BigInteger`) | total | `base` any value | invalid base gives NaN |
| `Pow(x, y)` (floating) | total | `y` any value, `x` negative with a fractional `y` | IEEE `pow`: NaN, ±∞ or ±0 (`Pow(-8, 1/3)` is NaN, `Pow(0, -1)` is +∞), never validates |
| `Lerp(a, b, t)`, `FusedMultiplyAdd` (floating) | total | `t` any value | no validation, IEEE result |
| `Max`/`Min` and their `Magnitude`/`Number` variants | total | – | NaN propagates, no validation |
| `Round(x)` (floating, decimal) | total | – | single-argument overload only |
| `RotateLeft`/`RotateRight(x, n)` (integer) | total | `n` any `int` | count masked |
| `<<`, `>>`, `>>>` (fixed-size integers) | total | count any `int` | count masked |
| `BigMul(a, b)` (integer) | total | – | result type wide enough |
| `op_Implicit` (all families) | total | – | widening only |
| `Clamp(x, min, max)` | partial | `min <= max` | `ArgumentException` |
| `Round(x, digits[, mode])`, `Round(x, mode)` | partial | `digits` in range, `mode` defined | `ArgumentOutOfRangeException`, `ArgumentException` |
| `Sign(x)` (floating) | partial | `x` not NaN | `ArithmeticException` |
| `Abs`, `CopySign` (signed integer) | partial | `x != MinValue` | `OverflowException` |
| `Log2(x)` (integer, `BigInteger`) | partial | `x >= 0` | `ArgumentOutOfRangeException` |
| division, remainder (integer, decimal, `BigInteger`) | partial | divisor `!= 0`; not `MinValue / -1` | `DivideByZeroException`, `OverflowException` |
| decimal arithmetic, `++`, `--` | partial | result in range | `OverflowException` |
| `Pow(x, n)`, `ModPow` (`BigInteger`) | partial | `n >= 0`; modulus `!= 0` | `ArgumentOutOfRangeException`, `DivideByZeroException` |
| shifts (`BigInteger`) | partial | count bounded by memory | `OverflowException`, `OutOfMemoryException` |
| `op_Explicit`, `op_Checked*` | partial | value in the target range | `OverflowException` |

Maintenance rule: a name is added as total only with its row in this table (when it takes such an argument) and its
coverage by the edge-value test; when in doubt it is listed as partial.

## 6. Known nondeterministic members

Recognized by declaring type and name: `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`,
`DateTimeOffset.UtcNow`, `Environment.TickCount`, `Environment.TickCount64`, `Stopwatch.GetTimestamp`, `Guid.NewGuid`
(`Nondeterministic | ReadsMutableState`, side-effect free), and every public instance method of `Random` (every may-flag but
`Unknown`: it advances its state, may validate arguments, and may be overridden). They are classified, not unknown.

## 7. Expression analysis

- **Call versus expression.** `AnalyzeMethod` classifies the method alone, its arguments already evaluated. `Analyze`
  classifies an expression: method ∪ receiver ∪ arguments, plus `MayThrowRuntime` for a reference receiver that is not a
  non-null constant.
- **Scopes.** Free parameters are mutable state. Block variables, the parameters of a lambda invoked in place, and the
  parameters of the lambda given to `AnalyzeInvocation` are local: reading them reads no outside state, writing them is no
  side effect — unless a by-reference parameter, or a local captured by an escaping closure or quoted tree (the write is
  then observed after the evaluation). A closure containing code the walk cannot see is assumed to capture every local.
  Shadowing is handled by declaration index.
- **Lambdas.** Evaluating a lambda allocates its delegate; its body does not run. A lambda invoked in place, and the
  conversion lambda of a coalescing or compound assignment, run their body, analyzed recursively with parameters bound,
  without compiling or executing anything. Any other delegate invocation is unknown code. Compiler-generated closures read
  their captures through fields of a display object: `ReadsMutableState`.
- **Operators.** Intrinsic operators (no method) follow `ExpressionBehaviorRules`: unchecked, bitwise, comparison and
  logical operators never throw; checked arithmetic overflows on integers only; integer division throws unless the divisor
  is a safe constant; IEEE division never throws. An operator method is classified like any method (trusted, declared or
  unknown).
- **Conversions.** Primitive and enumeration conversions never throw unchecked; checked ones may overflow an integral target;
  nullable wrapping is total, unwrapping may throw; boxing allocates; unboxing and non-widening reference casts may throw.
- **Members.** A field reads mutable state unless read-only or constant; a property runs its getter, classified as a
  method; an indexer likewise; an array element is mutable state and may be out of range.
- **Branches.** Conditionals and short-circuit operators contribute the union of what may run.
- **Unknown code.** Unannotated user methods, getters, setters, constructors, custom operators without declaration,
  delegate invocations, loops, jumps, labels, switches, try blocks, member and list initializers, runtime variables, debug
  information, dynamic operations and extension nodes.
- **Hostile inputs.** An extension node is recognized by class (`ExpressionOpacity.IsOpaque`) before any `Type` or
  `NodeType` read; no binder is invoked; a `MethodInfo`, `FieldInfo` or `PropertyInfo` that is not a runtime reflection
  object (finding A-10) is unknown code without any member read and is not cached. Since the A-10 fix the check is the
  engine-wide `ExpressionReflectionProvenance.IsRuntime` (a closed set of runtime classes), which also refuses emit builders.
- **Former omission (fixed).** `Pow` was missing from the total floating functions (the gate trusts it), so every intrinsic
  `Power` node, which carries `Math.Pow`, was unknown code. It is listed since the S5 P18 `Pow` classification change (with its
  precondition row above and the edge-value test); a power with a user method stays unknown, `BigInteger.Pow` stays partial.
- **Not modeled.** Asynchronous and resource exceptions, evaluation time, termination.

## 8. Resources and concurrency

Each public call is one expression operation (`ExpressionResourceBudget.Run`): the input is validated against the depth and
node limits before any recursion, every analyzed node counts one structural visit and checks the stack, and a call made
inside a running operation joins it and can only tighten its limits. A shared DAG is walked once per use, like the other
engine walks, so it cannot evade the node limit or the visit budget. Scope state lives in a per-call walker; the method
caches are concurrent dictionaries of runtime metadata. There is no thread-static state.

## 9. Preparation of future policies (not enabled)

Internal, unused by any rule:

- `ExpressionTrustPolicy` — `Verified` (a declaration counts as unknown code) or `TrustDeclarations` (declared code is
  trusted; unknown code stays unknown).
- `ExpressionBehaviorPermissions.CanDiscard` (no side effect, allocation or exception; a clock read may be dropped),
  `CanRepeat` (deterministic, no side effect or allocation; exceptions allowed), `CanReorder` (deterministic, no side
  effect, allocation or exception).
- **The permissions are local, not contextual.** Reading mutable state is allowed by every permission, so none of them
  alone justifies a rewrite across other evaluations. `CanRepeat` means that two evaluations in the same state agree; a
  caller that merges, repeats or moves an evaluation reading mutable state must also prove that no evaluation with side
  effects, and no unknown code, runs between the occurrences or across the move (as `SplitPinnedTerms` does today).
  Architecture C must not use `CanRepeat` in isolation.
- `ExpressionBehaviorPermissions.ToEffects` — the bridge to `ExpressionEffects`: exceptions become `MayThrow`, side effects,
  nondeterminism and unknown code become `Observable`. It is at least as strict as the gate on unknown code.

Intended extension, for a later stage: `ExpressionSimplifierOptions` set at construction (like
`SimplifyQuotedExpressions`), with **three independent axes**, each off by default:

1. trusting `[Deterministic]` declarations (`ExpressionTrustPolicy.TrustDeclarations`), so that annotated methods (for
   example those of `MathEx`) can be simplified without trusting any other user code;
2. `AggressiveSimplification`, which would rewrite unknown code; it does not imply the first axis, nor the converse;
3. an exception-preservation policy by kind (division by zero, overflow, invalid numeric argument, user exception).

The options would be consulted by `AllowsRules` and `SplitPinnedTerms` through `ToEffects` or the permissions, with
per-rule effect declarations replacing the coarse gate. Enabling any of them changes simplification results and the
number of user calls, so it needs its own contract decision. Finding A-10 must be fixed first.

**Update (Architecture B, see [SimplificationPolicies.md](SimplificationPolicies.md)).** A-10 is fixed. The options exist as
public API (`ExpressionSimplifierOptions`, `ExpressionTrustPolicy` made public, `ExpressionExceptionPreservation` by category
of `ExpressionBehaviors`: arithmetic, runtime, user code) and the permissions take them; nothing consumes them yet. Two
decisions differ from the sketch above: `AggressiveSimplification` does **not** rewrite unknown code (it lifts allocation
identity and side-effect-free nondeterminism on analyzed code only), and exception preservation is by origin category, not by
exception type. `ToEffects` keeps the trust axis only.

## 10. Guarantees

| Kind | Examples | Basis |
|---|---|---|
| Certain (verified by the analysis) | absence of a flag on intrinsic operators, trusted numeric functions, local writes, constants, read-only fields; known nondeterministic members | the expression model and the runtime's own types |
| Unknown | `Unknown`/`UnknownCode` on user methods without declaration, getters, constructors, delegates, unsupported families, extension nodes, user reflection objects | the analysis does not see the code |
| Declared | `ReliesOnDeclaration` on `[Deterministic]` methods, operators, conversions and getters | the author's promise, never verified |

## 11. Tests

- Unit (`UtilsTest/Mathematics/Expressions/Coverage`, category `S5-P18A`, also run by the ETC Debug job):
  `ExpressionBehaviorAnalysisTests` (trusted, partial and untrusted functions; declarations honored or not; members;
  nondeterministic members; unsupported families; hostile extension node; intrinsic exceptions; conditionals; scopes;
  captures; nested, invoked and passed lambdas; permissions; exact and derived simplifiers ignore declarations, even a lying
  one, with unchanged call counts; concurrent analyses; limits) and `ExpressionBehaviorAnalysisPropertyTests` (1,500 seeded
  trees of every family but extensions: every "no exception", "no side effect" and "deterministic" guarantee holds when the
  compiled tree runs; differential against the gate).
- Security (`UtilsTest.Security/Expressions/ExpressionBehaviorAnalysisResourceTests`): hostile nestings of six families on a
  256 KiB stack end with a `Stack` refusal and leave the thread usable; default limits refuse them before recursion; shared
  DAGs; exact visit budget; nested analyses join the running operation; a user `MethodInfo` is never read.

Differential result on the corpus (minimal disagreeing nodes): stricter than the gate only on created quotes (442 nodes,
A-4); more permissive only on blocks with local effects (473) and lambdas invoked in place (530), both covered by the
run-time soundness property.

## 12. Measurements

Windows 11, .NET 9, Release, single process per run, global warm-up before measurement, medians of 7–9 rounds.

Analysis cost (`ExpressionBehaviorAnalysis.Analyze`, default limits; "gate" is `ExpressionEffectAnalysis.Analyze` called
outside a session, for scale only: it opens no operation, validates nothing and stops at the first unknown node):

| Tree | Nodes | New analysis | Allocated | Gate |
|---|---:|---:|---:|---:|
| `x` | 1 | 39 ns | 56 B | 5 ns |
| `x*2 + Sin(y)` | 6 | 131 ns | 56 B | 45 ns |
| `d(x) + d(y)` (declared) | 5 | 147 ns | 56 B | 58 ns |
| `f(x)` (user) | 2 | 83 ns | 56 B | 33 ns |
| 30-term sum | 119 | 1.46 µs | 56 B | 0.94 µs |
| 400-term sum | 1,599 | 19.3 µs | 56 B | 12.7 µs |
| 20 nested identities | 85 | 1.01 µs | 56 B | 0.73 µs |
| 10 nested invoked lambdas | 61 | 2.17 µs | 392 B | 25 ns (stops: observable) |
| block with closure | 17 | 0.83 µs | 784 B | 32 ns (stops: observable) |
| `AnalyzeMethod` (cached) | – | 15 ns | 0 B | – |

About 40 ns of fixed cost per call (operation boundary, shape validation, walker), then roughly 12–25 ns per node.

Simplifier (`Simplify`, master `ad237762` against the branch, 10 alternating processes, 9 rounds each): no line of its path
changed (one helper of `ExpressionEffectAnalysis` became `internal`, which changes no code generation). Branch against master
medians: −3.6 % to +6.2 % over 11 cases (`g8` −1.0 %, 12-term sum 0.0 %, `(x+1)*(x+1) - x*x` −2.1 %, user calls −1.6 %),
inside the A/A spread of the same campaign (master against master: up to ±26 % on the smallest cases, ±2 % on the larger
ones). A first campaign was discarded: the machine slowed down by a factor of about 2 for both variants halfway through it.
