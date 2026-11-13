# S5 deep audit — `ExpressionSimplifier` (2026-11-04)

Status: **audit report, open findings**. Companion of the S5 stage of `TODO-2026-09-12-expression-simplifier-roadmap.md`
(section "S5 deep audit"). No production rule, canonicalization strategy, budget or ordering was changed by this audit; the
experiments that mutated the engine ran in a temporary worktree and are not part of the audit branch.

## 1. Reference

| Item | Value |
|---|---|
| Baseline | `master` `af2dc928254bdcf8faf30971aaf777196165935c` (S5 P17, PR #679), verified unchanged against `origin/master` before starting |
| Audit branch | `audit/s5-simplifier-deep-audit` |
| Machine | Windows 11 Pro 10.0.26300, Intel i7-12700KF (20 logical processors) |
| Runtime | .NET SDK 10.0.403; harness `net9.0`, `Utils` `net8.0`, Release, workstation non-concurrent GC, `InvariantGlobalization` |
| Harness | `tests/ExpressionSimplifierAudit` (not in `Utils.sln`), driven by `run-audit.ps1`; see its `README.md` |
| Characterization tests | `UtilsTest/Mathematics/Expressions/ExpressionSimplifierDeepAuditTests.cs` (category `S5-Audit`) |

Read before the audit: `AGENTS.md`, `Utils/AGENTS.md`, the S1–S5 sections of the simplifier roadmap (P1–P17), the ETC roadmap
(R0–R4, closure audit, tracing), `docs/releasing/MigrationTo2.0.md`, and the sources listed by the brief.

## 2. Method

Every number below comes from the harness, rerunnable with the commands of §16. The campaigns:

| Campaign | Cases | Notes |
|---|---:|---|
| `inventory` | 76 rules | reflection in `BuildPlan` order, IL scan of every rule body (transitively through the simplifier's helpers), portable-PDB locations |
| `terminate` | 66,270 | every instantiation of the 53 structures of the brief over `x, y, z, 0, 1, -1, 2, -2, 0.5, 1e300` (62,270) and 4,000 seeded second-level compositions; default limits; counting tracer on every case, trace collector and `ExpressionTraceCycleAnalyzer` on every anomaly |
| `converge` | 70,360 | the same corpus (another composition sample), the 26 complexity families at n = 2..8, 4,000 generated trees; up to 8 public passes compared with a strict structural signature (no simplification while comparing) |
| `canon` | 48 groups, 576 trees | every permutation and association of 8 term sets, sum/product/signed, free and lambda-bound |
| `comparer` | 28,000 seeds | 7 numeric types; about 1.46 million invariant checks; curated scope cases |
| `diff` | 8,000 seeds | `double`, `float`, `int`, `long`, `decimal`, `int?`, `uint`; 26 assignments per seed (moderate and special values); interpreter cross-check; automatic reduction |
| `effects` | 8,000 seeds | counting call, constant call, getter with an effect, throwing call; value, exception and ordered evaluation log compared; 5,262 trees actually rewritten |
| `s3` | 34 identities × applicable types | every assignment of the domain values |
| `measure` | 26 families × sizes 4..20 | one process per point, untraced timing, then instrumented counts |

The generators are weighted towards the shapes of the brief (`a*x ± b*x`, `c*(a-b)`, `(a/b)/(c/d)`, left- and right-nested
differences, `x/(1/x)`, `(1-y)/(1/y)`, `a*x + (1-a)*x`, powers, logarithms, `sin²+cos²`) and share subtrees (DAGs). Every anomaly
keeps its seed, its generator options, the source, the simplified form and, for the first occurrence of each signature, a
reduced counterexample re-verified by the reducer's own predicate.

Classification of differences (§8 of the brief): `rounding`, `signedZero`, `ieee` (NaN or infinity on one side with moderate
inputs) and `extreme` (an extreme input or constant) are the latitude of the S3 contract; `VALUE`, `EXC-ADDED`, `EXC-REMOVED`,
`EXC-CHANGED`, `SIMPLIFY-THROWS`, `COMPILE-FAIL` and `TYPE-CHANGED` are candidates, examined one by one; `LINQ` marks a difference
the expression interpreter does not reproduce (framework compiler).

## 3. Rule inventory (Phase A)

76 rules (`ExpressionSimplifier.cs` 38, `.INumber.cs` 12, `.Math.cs` 26), all reachable through `BuildPlan` (the S2 ledger
`ExpressionTransformationRuleCoverage.md` covers each one; this audit found no rule that became unreachable). Every rule runs
behind the engine gate `AllowsRules` (`ExpressionEffectAnalysis.HasInertOperands`, ETC R1b C-06) except `InvokeExpression`,
which has its own inlining analysis; no rule body calls `ExpressionEffectAnalysis` itself.

Columns: `TC` = `TransformCore` on a node the rule builds (re-entry), `TC-prepared` = `TransformCoreWithPreparedOperands` (P16);
comparer: `probe` = `FactorEqualityProbe` (P3/P9/P15 caches), `P16 forms` = `EqualsKeepingSimplifiedForms`, `Equals` =
`ExpressionComparer.EqualsWithinOperation`; guards: `ord` `IsOrdinaryBinaryArithmetic`, `neg` `IsOrdinaryUnaryNegate`, `field`
`IsOrdinaryFieldDivision`, `pair` `IsConstantPair`, `signed` `IsSignedIntegral`, `zero-div` `IsZeroDivisorOutsideFieldDomain`;
"Builds" = node factories called (transitively); "Applied" = applications over the termination campaign. Locations are the first
statement of the body.

| # | Rule | Node | Location | Re-entry | Comparer | Guards | Builds | Applied (terminate) | S2 ledger |
|---:|---|---|---|---|---|---|---|---:|---|
| 0 | `AdditionWithZero(BinaryExpression, Expression, ConstantExpression)` | Add | ExpressionSimplifier.cs:1029 | TC | — | pair, ord | — | 641 | COVERED |
| 1 | `AdditionWithZero(BinaryExpression, ConstantExpression, Expression)` | Add | ExpressionSimplifier.cs:1041 | — | — | pair, ord | — | 654 | COVERED |
| 2 | `SubstractionWithZero(BinaryExpression, Expression, ConstantExpression)` | Subtract | ExpressionSimplifier.cs:1053 | — | — | pair, ord | — | 2939 | COVERED |
| 3 | `SubstractionWithZero(BinaryExpression, ConstantExpression, Expression)` | Subtract | ExpressionSimplifier.cs:1070 | TC | — | pair, ord | Negate | 14076 | COVERED |
| 4 | `MultiplicationWithZeroOrOne(BinaryExpression, Expression, ConstantExpression)` | Multiply | ExpressionSimplifier.cs:1082 | — | — | pair, ord | Negate | 1745 | COVERED |
| 5 | `MultiplicationWithZeroOrOne(BinaryExpression, ConstantExpression, Expression)` | Multiply | ExpressionSimplifier.cs:1096 | — | — | pair, ord | Negate | 2434 | COVERED |
| 6 | `DivideWithZeroOrOne` | Divide | ExpressionSimplifier.cs:1127 | — | — | ord, signed | Negate | 2656 | COVERED |
| 7 | `DivideWithZero` | Divide | ExpressionSimplifier.cs:1155 | — | — | ord | — | 1537 | COVERED |
| 8 | `PowerOfZeroOrOne` | Power | ExpressionSimplifier.cs:1174 | — | — | ord | — | 1752 | COVERED |
| 9 | `PowerByZeroOrOne` | Power | ExpressionSimplifier.cs:1201 | TC | — | ord | Divide, Power | 3530 | COVERED |
| 10 | `AdditionOfConstants` | Add | ExpressionSimplifier.cs:1219 | — | — | ord | — | 7548 | COVERED |
| 11 | `AdditionWithNegate(BinaryExpression, Expression, UnaryExpression)` | Add | ExpressionSimplifier.cs:1229 | TC, TC-prepared | P16 forms | ord, neg | Subtract | 7496 | COVERED |
| 12 | `AdditionWithNegate(BinaryExpression, UnaryExpression, Expression)` | Add | ExpressionSimplifier.cs:1250 | TC, TC-prepared | P16 forms | ord, neg | Subtract | 3262 | COVERED |
| 13 | `SubstractionWithNegate(BinaryExpression, Expression, UnaryExpression)` | Subtract | ExpressionSimplifier.cs:1375 | TC | — | ord, neg | Add | 1302 | COVERED |
| 14 | `SubstractionWithNegate(BinaryExpression, UnaryExpression, Expression)` | Subtract | ExpressionSimplifier.cs:1386 | TC | — | ord, neg | Add, Negate | 1795 | COVERED |
| 15 | `NegateWithSubstraction` | Negate | ExpressionSimplifier.cs:1397 | TC | — | ord, neg | Add, Negate | 28 | PREPARED_AWAY |
| 16 | `SubstractionWithAddition` | Subtract | ExpressionSimplifier.cs:1408 | — | — | ord | Subtract | 8108 | COVERED |
| 17 | `SubstractionWithSubstraction` | Subtract | ExpressionSimplifier.cs:1422 | — | — | ord | Add, Subtract | 3969 | PREPARED_AWAY |
| 18 | `SubstractionOfConstants` | Subtract | ExpressionSimplifier.cs:1436 | — | — | ord | — | 37319 | COVERED |
| 19 | `AdditionOfEqualsElements` | Add | ExpressionSimplifier.cs:1817 | — | probe | pair, ord, neg | Add, Multiply, Negate, Subtract | 1016 | COVERED |
| 20 | `SubstractionOfEqualsElements` | Subtract | ExpressionSimplifier.cs:1901 | — | probe | pair, ord, neg | Add, Multiply, Negate, Subtract | 1532 | COVERED |
| 21 | `MultiplicationOfConstants` | Multiply | ExpressionSimplifier.cs:2120 | — | — | ord | — | 8852 | COVERED |
| 22 | `Multiplication(BinaryExpression, Expression, ConstantExpression)` | Multiply | ExpressionSimplifier.cs:2129 | — | — | ord | Multiply | 4114 | COVERED |
| 23 | `Multiplication(BinaryExpression, ConstantExpression, BinaryExpression)` | Multiply | ExpressionSimplifier.cs:2142 | — | — | ord | Multiply | 169 | COVERED |
| 24 | `Multiplication(BinaryExpression, BinaryExpression, BinaryExpression)` | Multiply | ExpressionSimplifier.cs:2162 | TC | — | ord | Multiply | 6 | COVERED |
| 25 | `DivisionOfConstants` | Divide | ExpressionSimplifier.cs:2185 | — | — | ord | — | 18357 | COVERED |
| 26 | `MultiplicationWithNegate(BinaryExpression, Expression, UnaryExpression)` | Multiply | ExpressionSimplifier.cs:2194 | TC | — | ord, neg | Multiply, Negate | 175 | COVERED |
| 27 | `MultiplicationWithNegate(BinaryExpression, UnaryExpression, Expression)` | Multiply | ExpressionSimplifier.cs:2207 | TC | — | ord, neg | Multiply, Negate | 73 | COVERED |
| 28 | `DivisionWithNegate(BinaryExpression, Expression, UnaryExpression)` | Divide | ExpressionSimplifier.cs:2224 | TC | — | ord, neg, signed | Divide, Negate | 419 | COVERED |
| 29 | `DivisionWithNegate(BinaryExpression, UnaryExpression, Expression)` | Divide | ExpressionSimplifier.cs:2237 | TC | — | ord, neg | Divide, Negate | 455 | COVERED |
| 30 | `MultiplicationOfEqualsElements(BinaryExpression, BinaryExpression, Expression)` | Multiply | ExpressionSimplifier.cs:2251 | TC | — | ord | Multiply | 139 | COVERED |
| 31 | `MultiplicationOfEqualsElements(BinaryExpression, Expression, BinaryExpression)` | Multiply | ExpressionSimplifier.cs:2286 | TC | — | ord | Multiply | 145 | COVERED |
| 32 | `MultiplicationOfEqualsElements(BinaryExpression, Expression, Expression)` | Multiply | ExpressionSimplifier.cs:2321 | TC | Equals | ord | Add, Power | 1056 | COVERED |
| 33 | `DivisionOfDivision(BinaryExpression, BinaryExpression, BinaryExpression)` | Divide | ExpressionSimplifier.cs:2394 | TC | — | ord, field, zero-div | Divide, Multiply | 1754 | COVERED |
| 34 | `DivisionOfDivision(BinaryExpression, Expression, BinaryExpression)` | Divide | ExpressionSimplifier.cs:2410 | TC | — | ord, field, zero-div | Divide, Multiply | 2926 | COVERED |
| 35 | `DivisionOfDivision(BinaryExpression, BinaryExpression, Expression)` | Divide | ExpressionSimplifier.cs:2428 | TC | — | ord, field, zero-div | Divide, Multiply | 2231 | COVERED |
| 36 | `PowerOfConstants` | Power | ExpressionSimplifier.cs:2488 | — | — | ord | — | 13288 | COVERED |
| 37 | `InvokeExpression` | Invoke | ExpressionSimplifier.cs:2512 | TC | — | — | — | 0 | COVERED |
| 38 | `PowerConvertionNumber1` | Call | ExpressionSimplifier.INumber.cs:22 | TC | — | — | Power | 0 | DUPLICATE_DISPATCH |
| 39 | `PowerConvertionNumber2` | Call | ExpressionSimplifier.INumber.cs:35 | TC | — | — | Power | 0 | COVERED |
| 40 | `LogarithmSimplificationAddNumber` | Add | ExpressionSimplifier.INumber.cs:53 | TC | — | log, ord | Call, Multiply | 185 | COVERED |
| 41 | `LogarithmSimplificationSubstractNumber` | Subtract | ExpressionSimplifier.INumber.cs:74 | TC | — | log, ord | Call, Divide | 171 | COVERED |
| 42 | `Logarithm10SimplificationAddNumber` | Add | ExpressionSimplifier.INumber.cs:95 | TC | — | log, ord | Call, Multiply | 0 | COVERED |
| 43 | `Logarithm10SimplificationSubstractNumber` | Subtract | ExpressionSimplifier.INumber.cs:116 | TC | — | log, ord | Call, Divide | 0 | COVERED |
| 44 | `AdditionOfCos2andSin2Number` | Add | ExpressionSimplifier.INumber.cs:182 | — | Equals | ord | — | 81 | COVERED |
| 45 | `DivisionOfCosAndSinNumber` | Divide | ExpressionSimplifier.INumber.cs:215 | — | Equals | ord | Call | 89 | COVERED |
| 46 | `DivisionOfSinAndCosNumber` | Divide | ExpressionSimplifier.INumber.cs:236 | — | Equals | ord | Call, Divide | 0 | COVERED |
| 47 | `MultiplicationOfCosAndTanNumber` | Multiply | ExpressionSimplifier.INumber.cs:257 | — | Equals | ord | Call | 0 | COVERED |
| 48 | `MultiplicationOfTanAndCosNumber` | Multiply | ExpressionSimplifier.INumber.cs:278 | — | Equals | ord | Call | 0 | COVERED |
| 49 | `DivisionOfSinAndTanNumber` | Divide | ExpressionSimplifier.INumber.cs:300 | — | Equals | ord | Call | 0 | COVERED |
| 50 | `PowerConversionMath` | Call | ExpressionSimplifier.Math.cs:32 | TC | — | — | Power | 0 | DUPLICATE_DISPATCH |
| 51 | `SignConversionMath` | Call | ExpressionSimplifier.Math.cs:182 | — | — | — | Call, Convert | 0 | COVERED |
| 52 | `MinConversionMath` | Call | ExpressionSimplifier.Math.cs:189 | — | — | — | Call, Convert | 0 | COVERED |
| 53 | `MaxConversionMath` | Call | ExpressionSimplifier.Math.cs:196 | — | — | — | Call, Convert | 0 | COVERED |
| 54 | `ClampConversionMath` | Call | ExpressionSimplifier.Math.cs:203 | — | — | — | Call, Convert | 0 | COVERED |
| 55 | `AbsConversionMath` | Call | ExpressionSimplifier.Math.cs:210 | — | — | — | Call, Convert | 0 | COVERED |
| 56 | `RoundConversionMath` | Call | ExpressionSimplifier.Math.cs:217 | — | — | — | Call, Convert | 0 | COVERED |
| 57 | `FloorConversionMath` | Call | ExpressionSimplifier.Math.cs:224 | — | — | — | Call, Convert | 0 | COVERED |
| 58 | `CeilingConversionMath` | Call | ExpressionSimplifier.Math.cs:231 | — | — | — | Call, Convert | 0 | COVERED |
| 59 | `SqrtConversionMath` | Call | ExpressionSimplifier.Math.cs:238 | — | — | — | Call, Convert | 0 | COVERED |
| 60 | `CbrtConversionMath` | Call | ExpressionSimplifier.Math.cs:245 | — | — | — | Call, Convert | 0 | COVERED |
| 61 | `LogConversionMath` | Call | ExpressionSimplifier.Math.cs:252 | — | — | — | Call, Convert | 1572 | COVERED |
| 62 | `Log2ConversionMath` | Call | ExpressionSimplifier.Math.cs:259 | — | — | — | Call, Convert | 0 | COVERED |
| 63 | `Log10ConversionMath` | Call | ExpressionSimplifier.Math.cs:266 | — | — | — | Call, Convert | 0 | COVERED |
| 64 | `CosConversionMath` | Call | ExpressionSimplifier.Math.cs:273 | — | — | — | Call, Convert | 251 | COVERED |
| 65 | `SinConversionMath` | Call | ExpressionSimplifier.Math.cs:280 | — | — | — | Call, Convert | 251 | COVERED |
| 66 | `TanConversionMath` | Call | ExpressionSimplifier.Math.cs:287 | — | — | — | Call, Convert | 1 | COVERED |
| 67 | `ACosConversionMath` | Call | ExpressionSimplifier.Math.cs:294 | — | — | — | Call, Convert | 0 | COVERED |
| 68 | `ASinConversionMath` | Call | ExpressionSimplifier.Math.cs:301 | — | — | — | Call, Convert | 0 | COVERED |
| 69 | `ATanConversionMath` | Call | ExpressionSimplifier.Math.cs:308 | — | — | — | Call, Convert | 0 | COVERED |
| 70 | `CoshConversionMath` | Call | ExpressionSimplifier.Math.cs:315 | — | — | — | Call, Convert | 0 | COVERED |
| 71 | `SinhConversionMath` | Call | ExpressionSimplifier.Math.cs:322 | — | — | — | Call, Convert | 0 | COVERED |
| 72 | `TanhConversionMath` | Call | ExpressionSimplifier.Math.cs:329 | — | — | — | Call, Convert | 0 | COVERED |
| 73 | `ACoshConversionMath` | Call | ExpressionSimplifier.Math.cs:336 | — | — | — | Call, Convert | 0 | COVERED |
| 74 | `ASinhConversionMath` | Call | ExpressionSimplifier.Math.cs:343 | — | — | — | Call, Convert | 0 | COVERED |
| 75 | `ATanhConversionMath` | Call | ExpressionSimplifier.Math.cs:350 | — | — | — | Call, Convert | 0 | COVERED |

### Dispatch order and priorities

- `BuildPlan` keeps the order of `Type.GetMethods`, which .NET does not specify; CoreCLR returns declaration order, and the plan
  depends on it (first non-null rule result wins, and a rule result is not dispatched again). No test pins that order
  (finding S5A-11).
- A rule result is final for its node. Three priority pairs observed to leave work for a later pass (S5A-05):
  `SubstractionWithAddition` (16) and `SubstractionWithSubstraction` (17) before `SubstractionOfEqualsElements` (20), so
  `(a + b) - (a + b)` is reassociated instead of cancelled; `Multiplication(e, t, c)` (22, commutation) before
  `MultiplicationWithNegate` (26–27) and the coefficient folding of `Multiplication(e, c, c*t)` (23).
- `PowerConvertionNumber1` (38) and `PowerConversionMath` (50) both match `double.Pow`; the first always returns a result, so the
  second is shadowed (the ledger's `DUPLICATE_DISPATCH`).
- The 30 rules never applied by the arithmetic campaigns are call shapes (`Invoke`, the `*ConversionMath` rules, `Log10` and the
  trigonometric product rules, `Math.Pow`): the ledger tests cover them; `RoundConversionMath` fails on three overloads (S5A-02).
- `NegateWithSubstraction` and `SubstractionWithSubstraction` (`PREPARED_AWAY` in the ledger: a source of that shape is prepared
  away first) do run, 3,969 and 2 times, on nodes rebuilt by other rules.

## 4. Interaction graph (Phase A)

Static may-reach graph (edge A → B when A re-enters `TransformCore` on a node of a type B is declared on): one strongly connected
component of **25 rules** (every re-entering rule: the negation, subtraction, multiplication, division, power and logarithm
rewrites and `SubstractionWithZero(0, t)`); with the comparer (every rule that compares operands reaches every rule) the whole
arithmetic plan is one component. The static graph is an over-approximation; it locates where convergence must be argued.

Dynamic graph (observed over the termination campaign, 311 `via` edges: B applied inside a treatment requested by A; 813
`consumed` edges: B applied to the parent of a node A rewrote): the `via` graph has one component of **20 rules**, including the
self-loop `SubstractionWithZero(0, t) → SubstractionWithZero(0, t)` (683 nested occurrences: `0 - t` re-entering on a `t` that
holds another `0 - c`); with the `consumed` edges, one component of 44 rules. The heaviest edges come from the factoring probes:
`SubstractionOfEqualsElements → SubstractionWithZero(0, t)` 7,069, `→ AdditionWithNegate` 6,819, `→ SubstractionOfConstants`
3,315; `AdditionOfEqualsElements → SubstractionWithZero(0, t)` 1,523. Every cycle of these graphs terminated on every input of
§5; they are the places where the convergence and complexity findings below live.

## 5. Termination (Phase B)

| Class | Cases |
|---|---:|
| Normal termination | 65,295 |
| Exception (all S5A-01, `NullReferenceException` in `AdditionOfCos2andSin2Number`) | 975 |
| Refusal by the budget | 0 |
| Strict cycle / expansion / suspect recurrence | 0 / 0 / 0 |
| Excessive but finite work (more than max(20,000, 500 × nodes) visits) | 0 |

The largest visit count of the corpus is 700 visits. The generated campaigns (`diff`, `effects`, `converge`: 20,000 more trees) have
no refusal either. AUD-03 is not reproduced (P17 holds on its 154 neighbours and on the whole corpus).

Refusals do occur, but only on the complexity families of §7, by the work budget (`Work`), and always on a finite computation:
with the limits lifted they all terminate (for example `negsum` n = 28 after 9.9 M visits). They are classified "growth" (class 6),
not loops. `MultiplicationOfEqualsElements(e, c*t, u)` still has no termination argument of its own (P17 remark): no input of the
campaigns makes it re-enter on a factor 1 or on its own output.

## 6. Convergence (Phase C)

| Origin | Cases | Fixed point after 1 pass | 2 passes | > 2 passes | Cycle between forms | Error | No fixed point in 8 passes |
|---|---:|---:|---:|---:|---:|---:|---:|
| Corpus, level 1 | 62,270 | 48,965 | 8,532 | 2,442 | 1,392 | 939 | 0 |
| Nested compositions | 4,000 | 2,705 | 777 | 228 | 158 | 132 | 0 |
| Generated trees | 4,000 | 3,062 | 649 | 172 | 112 | 5 | 0 |
| Families (n = 2..8) | 90 | 69 | 2 | 0 | 17 | 1 | 1 |
| **All** | **70,360** | **54,801 (77.9 %)** | **9,960 (14.2 %)** | **2,842 (4.0 %)** | **1,679 (2.4 %)** | **1,077** | **1** |

Cycle periods: 2 (1,593 cases), 3 (79), 4 (2), 5 (3), 7 (2). 1,584 cases have a later pass larger than the first. The errors are
S5A-01 (1,076; 98 of them at the second or third pass: the first pass reorders `x^0.5 + (-1)^x` into the crashing `(-1)^x + x^0.5`), plus
one refusal at the **second** pass of family `o` (n = 8): re-simplifying the canonical form costs more than the source.

What the first pass leaves (residue shapes of the first-pass output of the 14,481 cases that need two or more passes or cycle):

| Residue | Cases | Completed by (second-pass rules) |
|---|---:|---|
| `0 - c`, a negative constant group (P10/P17 representation) | 6,726 | `SubstractionWithZero(0, t)` 34,041, `SubstractionOfConstants` 12,005 |
| `t - (sum)`, an unfinalized reassociation | 5,012 | `AdditionWithNegate` 31,991, `SubstractionWithAddition` 2,800, `SubstractionWithNegate` 3,620 |
| unfolded constant pair `c1 - c2` | 1,803 | `SubstractionOfConstants` |
| `/ ±1` (`DivisionOfDivision` on a unit numerator, P17) | 836 | `DivideWithZeroOrOne` 836 |
| constant on the right of `*`, `c1 * (c2 * t)` | 392 | `Multiplication(e, t, c)` 232, `Multiplication(e, c, c*t)` 135 |
| `-(sum)`, `-(product)`, `-(-t)`, `-(c)` | 353 | `DivisionWithNegate`, `MultiplicationWithNegate`, `NegateWithSubstraction` |
| nested division | 46 | `DivisionOfDivision` |

The `*ConversionMath` rules also show up in the second pass (`SinConversionMath` 3,636, `LogConversionMath` 3,590, ...): they
rebuild every `double.Sin(...)`-style call with a fresh node, so a call is "rewritten" by every pass without any change; this
is noise for idempotence measured by node identity, not by structure (the strict signature compares structure).

**Cycles are not only the S4 tie of free parameters.** With every parameter bound by a lambda (positions fixed), `(x - y) - z`
still alternates between `x + ((0 - y) + (0 - z))` and `(x - y) - z`, and `((1 - x) - y) - z` has period 3 (S5A-06):
`SubstractionWithZero` turns `0 - t` into `-t`, `AdditionWithNegate` turns `a + -t` into a difference that
`SubstractionWithAddition`/`SubstractionWithSubstraction` reassociate without finalizing, and the next pass canonicalizes again —
the mechanism P10 documented ("bounded cycle"). The free-parameter tie adds wandering forms: family `g` n = 8 visits a new
permutation of its eight free parameters at every pass (no fixed point in 8 passes), while its bound version walks through
reassociations.

Assessment (brief §5): the extra passes are not needed to avoid an exponential cost — they come from rule results that are final
for their node (S5A-05) and from the orientation conflict between the canonical `0 - t` representation and the subtraction/negation
rules (S5A-06). P10 rejected a confluent left-associated form for its 3^n user-code cost, and a re-normalization of generated
nodes for re-running the factoring rules; the residue table shows that most remaining work is local (constant pairs, `0 - c`,
`/ 1`, `c1 * (c2 * t)`, priority pairs) and can be finalized without a new traversal (§15, P20).

## 7. Complexity (Phase D)

Families measured at n = 4, 6, ..., 20 (one process per point, limits lifted, a family stops after the first size above 10 s,
or when the 6 GB heap guard trips). Visits = `TransformCore` treatments (comparer simplifications included); the ratios are
observations, not asymptotic proofs. Complete per-size data (time, allocations, comparer simplifications and their maximum depth,
user calls, output size, ratios of each metric, outcome under the default limits) are in the collapsed table below.

| Family | Shape | visits n=4 | n=8 | n=12 | n=16 | n=20 | last ratio visits(n)/visits(n-2) | time at largest measured n | growth (observed) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| p | 100*x0 - x1*3 - 4*x2 - x3*5 - ... | 188 | 11,092 | 34,516 | 60,068 | 88,452 | 1.20 | 42.9 ms (n=20) | polynomial (≈n^2.5) |
| B | 2*x0 - x1*3 - 4*x2 - ... | 145 | 3,034 | 10,054 | 18,802 | 29,790 | 1.24 | 24.9 ms (n=20) | polynomial (≈n^2) |
| g | 3*x0 - 3*x1 - 3*x2 - ... | 66 | 3,048 | 33,140 | 1,093,232 | memory | 5.92 | 4,313.4 ms (n=18) | exponential (×6 per +2) |
| F | 100*x0 - 3*x1 - ... with a custom multiplication operator on every term | 15 | 31 | 47 | 63 | 79 | 1.11 | 0.1 ms (n=20) | linear |
| z | 100*carrier(opaque) - x1*3 - 4*x2 - ... | 429 | 56,865 | 243,163 | 1,638,981 | 22,388,319 | 3.85 | 15,949.4 ms (n=20) | exponential (×3.9 per +2) |
| e | 100*carrier(o1) - 3*carrier(o2) - 4*x2 - ... | 623 | 57,199 | 243,609 | 1,639,539 | 22,388,989 | 3.85 | 16,441.9 ms (n=20) | exponential (= z) |
| k | 100*carrier("s") - x1*3 - ... | 429 | 56,865 | 243,163 | 1,638,981 | 22,388,319 | 3.85 | 16,262.4 ms (n=20) | exponential (= z) |
| n | 100*Sin(x0) - x1*3 - ... | 315 | 42,336 | 189,214 | 1,372,812 | 19,145,130 | 3.87 | 13,565.2 ms (n=20) | exponential (≈ z) |
| o | 100*carrier(o0) - 3*carrier(o1) - 4*carrier(o2) - ... | 1,505 | 605,685 | memory | — | — | 15.80 | 5,575.2 ms (n=10) | exponential (×16 per +2, user calls) |
| r | 100*carrier(o0) + 3*carrier(o1) + ... | 435 | 28,735 | 1,166,995 | memory | — | 8.70 | 9,455.7 ms (n=14) | exponential (×8.7 per +2, user calls) |
| H | 2*x + 3*x + 4*x + ... | 32 | 68 | 104 | 140 | 176 | 1.11 | 0.1 ms (n=20) | linear |
| Lsub | x0 - x1 - x2 - ... | 84 | 5,209 | 58,849 | 1,960,419 | memory | 5.95 | 8,121.1 ms (n=18) | exponential (×6 per +2; polynomial when bound) |
| Rsub | `x0 - (x1 - (x2 - ...))` | 11 | 19 | 27 | 35 | 43 | 1.10 | 0.0 ms (n=20) | linear |
| Lmul | x0 * x1 * x2 * ... | 29 | 509 | 2,680 | 5,240 | 8,312 | 1.24 | 2.6 ms (n=20) | polynomial |
| Gm | `(2*x0) * (3*x1) * ...` | 70 | 1,042 | 8,422 | 25,426 | 53,910 | 1.42 | 15.2 ms (n=20) | polynomial (≈n^3) |
| div | `x0 / (x1 / (x2 / ...))` | 17 | 109 | 489 | 2,021 | 7,171 | 1.76 | 1.5 ms (n=20) | exponential, small base (×1.8–2 per +2) |
| pow | x^2 * x^3 * x^4 * ... | 33 | 73 | 113 | 153 | 193 | 1.12 | 0.0 ms (n=20) | linear |
| bal | balanced sum of differences | 45 | 307 | 1,667 | 4,833 | 5,601 | 1.07 | 2.2 ms (n=20) | polynomial |
| negmul | `(-x(n-1)) * (... * ((-x1) * x0))` | 98 | 3,620 | 57,953 | 258,763 | 710,597 | 1.59 | 193.7 ms (n=20) | polynomial ≈n^4 to n=20 (×2 per +4 up to n=28) |
| negdiv | `(-((-x0) / x1)) / x2 ...` | 72 | 2,130 | 41,510 | 221,064 | 643,882 | 1.63 | 162.5 ms (n=20) | polynomial ≈n^4 (≈ negmul) |
| subneg | `(-((-x0) - x1)) - x2 ...` | 119 | 4,680 | 55,281 | 122,743 | 384,509 | 1.45 | 249.7 ms (n=20) | super-polynomial (n=24: 2.3 M, n=28: 5.9 M visits) |
| negsum | -(-(-(x0 - x1) - x2) ... - x(n-1)) | 212 | 4,020 | 42,587 | 150,970 | 363,882 | 1.73 | 239.9 ms (n=20) | super-polynomial (n=24: 1.6 M, n=28: 9.9 M visits) |
| zerominus | `0 - ((0 - (x0 * x1)) * x2) ...` | 126 | 4,128 | 60,632 | 264,002 | 718,908 | 1.59 | 196.7 ms (n=20) | polynomial ≈n^4 |
| plus0 | ((x0 * x1) + 0) * x2 + 0 ... | 82 | 1,518 | 11,053 | 30,601 | 62,141 | 1.39 | 16.9 ms (n=20) | polynomial ≈n^3 |
| zeroplus | `0 + ((0 + (x0 * x1)) * x2) ...` | 35 | 523 | 2,702 | 5,270 | 8,350 | 1.24 | 2.5 ms (n=20) | polynomial ≈n^2 |
| pow1 | ((x0 * x1) ^ 1 * x2) ^ 1 ... | 82 | 1,518 | 11,053 | 30,601 | 62,141 | 1.39 | 17.3 ms (n=20) | = plus0 |

<details>
<summary>Per-size measurements</summary>

| Family | n | Time (ms) | Allocated / op | Visits | Comparer simplifications | Max comparer depth | User calls | Output nodes | T ratio | Visits ratio | Comparisons ratio | Alloc ratio | Default limits |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| p | 4 | 0.186 | 94,822 | 188 | 92 | 4 | 0 | 21 | — | — | — | — | accepted |
| p | 6 | 1.724 | 882,766 | 1,819 | 882 | 6 | 0 | 33 | 9.26 | 9.68 | 9.59 | 9.31 | accepted |
| p | 8 | 10.281 | 5,356,878 | 11,092 | 5,376 | 8 | 0 | 45 | 5.96 | 6.10 | 6.10 | 6.07 | accepted |
| p | 10 | 19.657 | 11,061,635 | 22,582 | 11,054 | 8 | 0 | 57 | 1.91 | 2.04 | 2.06 | 2.06 | accepted |
| p | 12 | 15.779 | 17,248,472 | 34,516 | 17,108 | 8 | 0 | 69 | 0.80 | 1.53 | 1.55 | 1.56 | accepted |
| p | 14 | 25.021 | 24,056,356 | 46,982 | 23,618 | 8 | 0 | 81 | 1.59 | 1.36 | 1.38 | 1.39 | accepted |
| p | 16 | 29.550 | 31,646,054 | 60,068 | 30,664 | 8 | 0 | 93 | 1.18 | 1.28 | 1.30 | 1.32 | accepted |
| p | 18 | 31.030 | 40,194,518 | 73,862 | 38,326 | 8 | 0 | 105 | 1.05 | 1.23 | 1.25 | 1.27 | accepted |
| p | 20 | 42.943 | 49,897,699 | 88,452 | 46,684 | 8 | 0 | 117 | 1.38 | 1.20 | 1.22 | 1.24 | accepted |
| B | 4 | 0.144 | 71,368 | 145 | 68 | 3 | 0 | 21 | — | — | — | — | accepted |
| B | 6 | 0.581 | 376,270 | 721 | 362 | 5 | 0 | 33 | 4.02 | 4.97 | 5.32 | 5.27 | accepted |
| B | 8 | 3.051 | 1,735,548 | 3,034 | 1,648 | 7 | 0 | 45 | 5.25 | 4.21 | 4.55 | 4.61 | accepted |
| B | 10 | 6.472 | 3,908,320 | 6,360 | 3,642 | 7 | 0 | 57 | 2.12 | 2.10 | 2.21 | 2.25 | accepted |
| B | 12 | 11.630 | 6,676,795 | 10,054 | 6,068 | 7 | 0 | 69 | 1.80 | 1.58 | 1.67 | 1.71 | accepted |
| B | 14 | 14.908 | 10,252,332 | 14,180 | 9,022 | 7 | 0 | 81 | 1.28 | 1.41 | 1.49 | 1.54 | accepted |
| B | 16 | 16.389 | 14,858,196 | 18,802 | 12,600 | 7 | 0 | 93 | 1.10 | 1.33 | 1.40 | 1.45 | accepted |
| B | 18 | 27.914 | 20,750,561 | 23,984 | 16,898 | 7 | 0 | 105 | 1.70 | 1.28 | 1.34 | 1.40 | accepted |
| B | 20 | 24.869 | 28,217,075 | 29,790 | 22,012 | 7 | 0 | 117 | 0.89 | 1.24 | 1.30 | 1.36 | accepted |
| g | 4 | 0.043 | 24,672 | 66 | 38 | 3 | 0 | 9 | — | — | — | — | accepted |
| g | 6 | 0.310 | 144,800 | 427 | 216 | 5 | 0 | 13 | 7.13 | 6.47 | 5.68 | 5.87 | accepted |
| g | 8 | 2.303 | 1,024,958 | 3,048 | 1,526 | 7 | 0 | 17 | 7.42 | 7.14 | 7.06 | 7.08 | accepted |
| g | 10 | 9.159 | 4,017,496 | 9,527 | 5,720 | 7 | 0 | 21 | 3.98 | 3.13 | 3.75 | 3.92 | accepted |
| g | 12 | 19.385 | 17,952,622 | 33,140 | 24,084 | 9 | 0 | 25 | 2.12 | 3.48 | 4.21 | 4.47 | accepted |
| g | 14 | 95.057 | 111,466,635 | 178,799 | 143,666 | 10 | 0 | 29 | 4.90 | 5.40 | 5.97 | 6.21 | accepted |
| g | 16 | 705.518 | 722,036,356 | 1,093,232 | 902,642 | 12 | 0 | 33 | 7.42 | 6.11 | 6.28 | 6.48 | ResourceLimit:Work |
| g | 18 | 4,313.427 | 4,446,031,696 | 6,472,541 | 5,386,056 | 14 | 0 | 37 | 6.11 | 5.92 | 5.97 | 6.16 | ResourceLimit:Work |
| g | 20 | memory | | | | | | | | | | | |
| F | 4 | 0.005 | 7,760 | 15 | 0 | 0 | 0 | 21 | — | — | — | — | accepted |
| F | 6 | 0.010 | 13,824 | 23 | 0 | 0 | 0 | 33 | 2.00 | 1.53 | — | 1.78 | accepted |
| F | 8 | 0.016 | 20,968 | 31 | 0 | 0 | 0 | 45 | 1.58 | 1.35 | — | 1.52 | accepted |
| F | 10 | 0.024 | 30,312 | 39 | 0 | 0 | 0 | 57 | 1.52 | 1.26 | — | 1.45 | accepted |
| F | 12 | 0.033 | 40,992 | 47 | 0 | 0 | 0 | 69 | 1.36 | 1.21 | — | 1.35 | accepted |
| F | 14 | 0.045 | 52,472 | 55 | 0 | 0 | 0 | 81 | 1.34 | 1.17 | — | 1.28 | accepted |
| F | 16 | 0.056 | 64,816 | 63 | 0 | 0 | 0 | 93 | 1.25 | 1.15 | — | 1.24 | accepted |
| F | 18 | 0.068 | 80,512 | 71 | 0 | 0 | 0 | 105 | 1.22 | 1.13 | — | 1.24 | accepted |
| F | 20 | 0.083 | 98,120 | 79 | 0 | 0 | 0 | 117 | 1.21 | 1.11 | — | 1.22 | accepted |
| z | 4 | 0.465 | 158,840 | 429 | 130 | 4 | 0 | 25 | — | — | — | — | accepted |
| z | 6 | 17.570 | 2,500,227 | 6,933 | 2,104 | 6 | 0 | 37 | 37.75 | 16.16 | 16.18 | 15.74 | accepted |
| z | 8 | 19.859 | 20,905,457 | 56,865 | 17,718 | 8 | 0 | 49 | 1.13 | 8.20 | 8.42 | 8.36 | accepted |
| z | 10 | 46.583 | 50,578,301 | 125,832 | 43,458 | 8 | 0 | 61 | 2.35 | 2.21 | 2.45 | 2.42 | accepted |
| z | 12 | 106.428 | 115,541,156 | 243,163 | 101,414 | 8 | 0 | 73 | 2.28 | 1.93 | 2.33 | 2.28 | accepted |
| z | 14 | 305.193 | 321,877,122 | 554,010 | 288,354 | 8 | 0 | 85 | 2.87 | 2.28 | 2.84 | 2.79 | accepted |
| z | 16 | 1,059.531 | 1,093,928,018 | 1,638,981 | 991,350 | 8 | 0 | 97 | 3.47 | 2.96 | 3.44 | 3.40 | ResourceLimit:Work |
| z | 18 | 4,040.894 | 4,129,082,616 | 5,820,508 | 3,758,690 | 8 | 0 | 109 | 3.81 | 3.55 | 3.79 | 3.77 | ResourceLimit:Work |
| z | 20 | 15,949.374 | 16,216,935,360 | 22,388,319 | 14,783,526 | 8 | 0 | 121 | 3.95 | 3.85 | 3.93 | 3.93 | ResourceLimit:Work |
| e | 4 | 1.640 | 203,608 | 623 | 153 | 4 | 3 | 29 | — | — | — | — | accepted |
| e | 6 | 3.175 | 2,556,579 | 7,199 | 2,127 | 6 | 3 | 41 | 1.94 | 11.56 | 13.90 | 12.56 | accepted |
| e | 8 | 20.413 | 20,973,073 | 57,199 | 17,741 | 8 | 3 | 53 | 6.43 | 7.95 | 8.34 | 8.20 | accepted |
| e | 10 | 46.138 | 50,656,221 | 126,222 | 43,481 | 8 | 3 | 65 | 2.26 | 2.21 | 2.45 | 2.42 | accepted |
| e | 12 | 107.229 | 115,629,469 | 243,609 | 101,437 | 8 | 3 | 77 | 2.32 | 1.93 | 2.33 | 2.28 | accepted |
| e | 14 | 313.580 | 321,972,394 | 554,512 | 288,377 | 8 | 3 | 89 | 2.92 | 2.28 | 2.84 | 2.78 | accepted |
| e | 16 | 1,094.496 | 1,094,034,978 | 1,639,539 | 991,373 | 8 | 3 | 101 | 3.49 | 2.96 | 3.44 | 3.40 | ResourceLimit:Work |
| e | 18 | 4,044.324 | 4,129,202,600 | 5,821,122 | 3,758,713 | 8 | 3 | 113 | 3.70 | 3.55 | 3.79 | 3.77 | ResourceLimit:Work |
| e | 20 | 16,441.915 | 16,217,064,312 | 22,388,989 | 14,783,549 | 8 | 3 | 125 | 4.07 | 3.85 | 3.93 | 3.93 | ResourceLimit:Work |
| k | 4 | 1.269 | 158,840 | 429 | 130 | 4 | 0 | 25 | — | — | — | — | accepted |
| k | 6 | 16.948 | 2,500,184 | 6,933 | 2,104 | 6 | 0 | 37 | 13.36 | 16.16 | 16.18 | 15.74 | accepted |
| k | 8 | 20.090 | 20,925,048 | 56,865 | 17,718 | 8 | 0 | 49 | 1.19 | 8.20 | 8.42 | 8.37 | accepted |
| k | 10 | 45.743 | 50,784,756 | 125,832 | 43,458 | 8 | 0 | 61 | 2.28 | 2.21 | 2.45 | 2.43 | accepted |
| k | 12 | 110.098 | 116,600,742 | 243,163 | 101,414 | 8 | 0 | 73 | 2.41 | 1.93 | 2.33 | 2.30 | accepted |
| k | 14 | 309.177 | 326,457,162 | 554,010 | 288,354 | 8 | 0 | 85 | 2.81 | 2.28 | 2.84 | 2.80 | accepted |
| k | 16 | 1,087.082 | 1,112,703,435 | 1,638,981 | 991,350 | 8 | 0 | 97 | 3.52 | 2.96 | 3.44 | 3.41 | ResourceLimit:Work |
| k | 18 | 4,098.208 | 4,204,752,496 | 5,820,508 | 3,758,690 | 8 | 0 | 109 | 3.77 | 3.55 | 3.79 | 3.78 | ResourceLimit:Work |
| k | 20 | 16,262.445 | 16,520,292,552 | 22,388,319 | 14,783,526 | 8 | 0 | 121 | 3.97 | 3.85 | 3.93 | 3.93 | ResourceLimit:Work |
| n | 4 | 0.381 | 148,256 | 315 | 130 | 4 | 0 | 22 | — | — | — | — | accepted |
| n | 6 | 14.219 | 2,343,414 | 5,139 | 2,104 | 6 | 0 | 34 | 37.35 | 16.31 | 16.18 | 15.81 | accepted |
| n | 8 | 18.506 | 19,604,448 | 42,336 | 17,718 | 8 | 0 | 46 | 1.30 | 8.24 | 8.42 | 8.37 | accepted |
| n | 10 | 36.418 | 47,523,760 | 95,049 | 43,458 | 8 | 0 | 58 | 1.97 | 2.25 | 2.45 | 2.42 | accepted |
| n | 12 | 85.039 | 108,908,816 | 189,214 | 101,414 | 8 | 0 | 70 | 2.34 | 1.99 | 2.33 | 2.29 | accepted |
| n | 14 | 249.881 | 304,364,856 | 449,247 | 288,354 | 8 | 0 | 82 | 2.94 | 2.37 | 2.84 | 2.79 | accepted |
| n | 16 | 871.504 | 1,036,323,032 | 1,372,812 | 991,350 | 8 | 0 | 94 | 3.49 | 3.06 | 3.44 | 3.40 | ResourceLimit:Work |
| n | 18 | 3,381.493 | 3,914,528,880 | 4,950,565 | 3,758,690 | 8 | 0 | 106 | 3.88 | 3.61 | 3.79 | 3.78 | ResourceLimit:Work |
| n | 20 | 13,565.237 | 15,377,996,864 | 19,145,130 | 14,783,526 | 8 | 0 | 118 | 4.01 | 3.87 | 3.93 | 3.93 | ResourceLimit:Work |
| o | 4 | 1.058 | 429,441 | 1,505 | 292 | 4 | 13 | 37 | — | — | — | — | accepted |
| o | 6 | 14.868 | 10,863,972 | 38,683 | 7,416 | 6 | 313 | 57 | 14.05 | 25.70 | 25.40 | 25.30 | accepted |
| o | 8 | 254.078 | 223,182,433 | 605,685 | 164,484 | 8 | 7,813 | 77 | 17.09 | 15.66 | 22.18 | 20.54 | accepted |
| o | 10 | 5,575.222 | 4,833,410,960 | 9,567,389 | 3,788,016 | 9 | 195,313 | 97 | 21.94 | 15.80 | 23.03 | 21.66 | ResourceLimit:Work |
| o | 12 | memory | | | | | | | | | | | |
| r | 4 | 1.273 | 148,073 | 435 | 100 | 3 | 9 | 31 | — | — | — | — | accepted |
| r | 6 | 12.326 | 1,378,713 | 3,999 | 928 | 5 | 81 | 47 | 9.68 | 9.19 | 9.28 | 9.31 | accepted |
| r | 8 | 16.290 | 12,024,229 | 28,735 | 8,380 | 7 | 729 | 63 | 1.32 | 7.19 | 9.03 | 8.72 | accepted |
| r | 10 | 115.265 | 101,987,028 | 154,585 | 75,448 | 9 | 6,561 | 79 | 7.08 | 5.38 | 9.00 | 8.48 | accepted |
| r | 12 | 1,019.861 | 904,321,718 | 1,166,995 | 679,060 | 11 | 59,049 | 95 | 8.85 | 7.55 | 9.00 | 8.87 | ResourceLimit:Work |
| r | 14 | 9,455.704 | 8,117,960,504 | 10,157,677 | 6,111,568 | 13 | 531,441 | 111 | 9.27 | 8.70 | 9.00 | 8.98 | ResourceLimit:Work |
| r | 16 | memory | | | | | | | | | | | |
| H | 4 | 0.024 | 11,472 | 32 | 17 | 1 | 0 | 3 | — | — | — | — | accepted |
| H | 6 | 0.036 | 17,328 | 50 | 27 | 1 | 0 | 3 | 1.54 | 1.56 | 1.59 | 1.51 | accepted |
| H | 8 | 0.047 | 23,184 | 68 | 37 | 1 | 0 | 3 | 1.31 | 1.36 | 1.37 | 1.34 | accepted |
| H | 10 | 0.053 | 29,040 | 86 | 47 | 1 | 0 | 3 | 1.12 | 1.26 | 1.27 | 1.25 | accepted |
| H | 12 | 0.075 | 34,896 | 104 | 57 | 1 | 0 | 3 | 1.41 | 1.21 | 1.21 | 1.20 | accepted |
| H | 14 | 0.075 | 40,752 | 122 | 67 | 1 | 0 | 3 | 1.00 | 1.17 | 1.18 | 1.17 | accepted |
| H | 16 | 0.055 | 46,608 | 140 | 77 | 1 | 0 | 3 | 0.73 | 1.15 | 1.15 | 1.14 | accepted |
| H | 18 | 0.110 | 52,464 | 158 | 87 | 1 | 0 | 3 | 2.01 | 1.13 | 1.13 | 1.13 | accepted |
| H | 20 | 0.118 | 58,320 | 176 | 97 | 1 | 0 | 3 | 1.07 | 1.11 | 1.11 | 1.11 | accepted |
| Lsub | 4 | 0.017 | 29,928 | 84 | 42 | 3 | 0 | 13 | — | — | — | — | accepted |
| Lsub | 6 | 0.249 | 247,488 | 785 | 368 | 5 | 0 | 21 | 14.65 | 9.35 | 8.76 | 8.27 | accepted |
| Lsub | 8 | 1.065 | 1,730,468 | 5,209 | 2,578 | 7 | 0 | 29 | 4.28 | 6.64 | 7.01 | 6.99 | accepted |
| Lsub | 10 | 6.351 | 6,766,211 | 15,833 | 9,594 | 7 | 0 | 37 | 5.96 | 3.04 | 3.72 | 3.91 | accepted |
| Lsub | 12 | 23.909 | 32,598,484 | 58,849 | 43,412 | 9 | 0 | 45 | 3.76 | 3.72 | 4.52 | 4.82 | accepted |
| Lsub | 14 | 198.666 | 205,755,839 | 326,023 | 263,266 | 10 | 0 | 53 | 8.31 | 5.54 | 6.06 | 6.31 | accepted |
| Lsub | 16 | 1,265.861 | 1,305,364,570 | 1,960,419 | 1,621,146 | 12 | 0 | 61 | 6.37 | 6.01 | 6.16 | 6.34 | ResourceLimit:Work |
| Lsub | 18 | 8,121.051 | 8,083,640,568 | 11,660,133 | 9,711,146 | 14 | 0 | 69 | 6.42 | 5.95 | 5.99 | 6.19 | ResourceLimit:Work |
| Lsub | 20 | memory | | | | | | | | | | | |
| Rsub | 4 | 0.002 | 3,712 | 11 | 4 | 1 | 0 | 9 | — | — | — | — | accepted |
| Rsub | 6 | 0.003 | 4,000 | 15 | 4 | 1 | 0 | 13 | 1.26 | 1.36 | 1.00 | 1.08 | accepted |
| Rsub | 8 | 0.004 | 4,288 | 19 | 4 | 1 | 0 | 17 | 1.31 | 1.27 | 1.00 | 1.07 | accepted |
| Rsub | 10 | 0.005 | 4,576 | 23 | 4 | 1 | 0 | 21 | 1.29 | 1.21 | 1.00 | 1.07 | accepted |
| Rsub | 12 | 0.006 | 4,864 | 27 | 4 | 1 | 0 | 25 | 1.14 | 1.17 | 1.00 | 1.06 | accepted |
| Rsub | 14 | 0.007 | 5,152 | 31 | 4 | 1 | 0 | 29 | 1.30 | 1.15 | 1.00 | 1.06 | accepted |
| Rsub | 16 | 0.008 | 5,440 | 35 | 4 | 1 | 0 | 33 | 1.14 | 1.13 | 1.00 | 1.06 | accepted |
| Rsub | 18 | 0.010 | 5,728 | 39 | 4 | 1 | 0 | 37 | 1.19 | 1.11 | 1.00 | 1.05 | accepted |
| Rsub | 20 | 0.013 | 6,016 | 43 | 4 | 1 | 0 | 41 | 1.35 | 1.10 | 1.00 | 1.05 | accepted |
| Lmul | 4 | 0.006 | 7,944 | 29 | 14 | 3 | 0 | 7 | — | — | — | — | accepted |
| Lmul | 6 | 0.025 | 34,792 | 125 | 62 | 5 | 0 | 11 | 4.38 | 4.31 | 4.43 | 4.38 | accepted |
| Lmul | 8 | 0.103 | 142,880 | 509 | 254 | 7 | 0 | 15 | 4.06 | 4.07 | 4.10 | 4.11 | accepted |
| Lmul | 10 | 0.327 | 446,096 | 1,552 | 782 | 8 | 0 | 19 | 3.18 | 3.05 | 3.08 | 3.12 | accepted |
| Lmul | 12 | 0.602 | 808,322 | 2,680 | 1,382 | 8 | 0 | 23 | 1.84 | 1.73 | 1.77 | 1.81 | accepted |
| Lmul | 14 | 0.958 | 1,245,394 | 3,904 | 2,070 | 8 | 0 | 27 | 1.59 | 1.46 | 1.50 | 1.54 | accepted |
| Lmul | 16 | 1.307 | 1,777,813 | 5,240 | 2,862 | 8 | 0 | 31 | 1.36 | 1.34 | 1.38 | 1.43 | accepted |
| Lmul | 18 | 1.971 | 2,428,744 | 6,704 | 3,774 | 8 | 0 | 35 | 1.51 | 1.28 | 1.32 | 1.37 | accepted |
| Lmul | 20 | 2.589 | 3,223,778 | 8,312 | 4,822 | 8 | 0 | 39 | 1.31 | 1.24 | 1.28 | 1.33 | accepted |
| Gm | 4 | 0.016 | 21,408 | 70 | 30 | 3 | 0 | 9 | — | — | — | — | accepted |
| Gm | 6 | 0.060 | 76,864 | 268 | 126 | 5 | 0 | 13 | 3.68 | 3.83 | 4.20 | 3.59 | accepted |
| Gm | 8 | 0.234 | 294,534 | 1,042 | 510 | 7 | 0 | 17 | 3.93 | 3.89 | 4.05 | 3.83 | accepted |
| Gm | 10 | 0.772 | 1,032,990 | 3,625 | 1,806 | 8 | 0 | 21 | 3.29 | 3.48 | 3.54 | 3.51 | accepted |
| Gm | 12 | 1.893 | 2,464,563 | 8,422 | 4,264 | 8 | 0 | 25 | 2.45 | 2.32 | 2.36 | 2.39 | accepted |
| Gm | 14 | 3.608 | 4,730,595 | 15,617 | 8,052 | 8 | 0 | 29 | 1.91 | 1.85 | 1.89 | 1.92 | accepted |
| Gm | 16 | 5.987 | 8,011,132 | 25,426 | 13,370 | 8 | 0 | 33 | 1.66 | 1.63 | 1.66 | 1.69 | accepted |
| Gm | 18 | 9.826 | 12,531,569 | 38,097 | 20,450 | 8 | 0 | 37 | 1.64 | 1.50 | 1.53 | 1.56 | accepted |
| Gm | 20 | 15.154 | 18,566,990 | 53,910 | 29,556 | 8 | 0 | 41 | 1.54 | 1.42 | 1.45 | 1.48 | accepted |
| div | 4 | 0.002 | 2,832 | 17 | 4 | 1 | 0 | 7 | — | — | — | — | accepted |
| div | 6 | 0.009 | 9,328 | 47 | 16 | 2 | 0 | 11 | 3.96 | 2.76 | 4.00 | 3.29 | accepted |
| div | 8 | 0.019 | 24,528 | 109 | 44 | 3 | 0 | 15 | 2.04 | 2.32 | 2.75 | 2.63 | accepted |
| div | 10 | 0.044 | 57,552 | 235 | 104 | 4 | 0 | 19 | 2.39 | 2.16 | 2.36 | 2.35 | accepted |
| div | 12 | 0.097 | 126,458 | 489 | 228 | 5 | 0 | 23 | 2.19 | 2.08 | 2.19 | 2.20 | accepted |
| div | 14 | 0.206 | 267,338 | 999 | 480 | 6 | 0 | 27 | 2.12 | 2.04 | 2.11 | 2.11 | accepted |
| div | 16 | 0.410 | 552,400 | 2,021 | 988 | 7 | 0 | 31 | 1.99 | 2.02 | 2.06 | 2.07 | accepted |
| div | 18 | 0.803 | 1,128,826 | 4,063 | 2,008 | 8 | 0 | 35 | 1.96 | 2.01 | 2.03 | 2.04 | accepted |
| div | 20 | 1.530 | 2,020,352 | 7,171 | 3,572 | 8 | 0 | 39 | 1.91 | 1.76 | 1.78 | 1.79 | accepted |
| pow | 4 | 0.005 | 6,312 | 33 | 0 | 0 | 0 | 3 | — | — | — | — | accepted |
| pow | 6 | 0.007 | 10,136 | 53 | 0 | 0 | 0 | 3 | 1.40 | 1.61 | — | 1.61 | accepted |
| pow | 8 | 0.011 | 13,960 | 73 | 0 | 0 | 0 | 3 | 1.45 | 1.38 | — | 1.38 | accepted |
| pow | 10 | 0.014 | 17,784 | 93 | 0 | 0 | 0 | 3 | 1.31 | 1.27 | — | 1.27 | accepted |
| pow | 12 | 0.017 | 21,608 | 113 | 0 | 0 | 0 | 3 | 1.20 | 1.22 | — | 1.22 | accepted |
| pow | 14 | 0.019 | 25,432 | 133 | 0 | 0 | 0 | 3 | 1.16 | 1.18 | — | 1.18 | accepted |
| pow | 16 | 0.021 | 29,256 | 153 | 0 | 0 | 0 | 3 | 1.10 | 1.15 | — | 1.15 | accepted |
| pow | 18 | 0.024 | 33,278 | 173 | 0 | 0 | 0 | 3 | 1.14 | 1.13 | — | 1.14 | accepted |
| pow | 20 | 0.028 | 36,904 | 193 | 0 | 0 | 0 | 3 | 1.14 | 1.12 | — | 1.11 | accepted |
| bal | 4 | 0.053 | 18,064 | 45 | 24 | 2 | 0 | 11 | — | — | — | — | accepted |
| bal | 6 | 0.042 | 72,952 | 176 | 98 | 4 | 0 | 17 | 0.79 | 3.91 | 4.08 | 4.04 | accepted |
| bal | 8 | 0.144 | 127,616 | 307 | 172 | 4 | 0 | 23 | 3.48 | 1.74 | 1.76 | 1.75 | accepted |
| bal | 10 | 0.592 | 444,678 | 987 | 588 | 6 | 0 | 29 | 4.10 | 3.21 | 3.42 | 3.48 | accepted |
| bal | 12 | 1.097 | 761,926 | 1,667 | 1,004 | 6 | 0 | 35 | 1.85 | 1.69 | 1.71 | 1.71 | accepted |
| bal | 14 | 1.220 | 1,638,372 | 3,250 | 2,094 | 8 | 0 | 41 | 1.11 | 1.95 | 2.09 | 2.15 | accepted |
| bal | 16 | 2.090 | 2,514,892 | 4,833 | 3,184 | 8 | 0 | 47 | 1.71 | 1.49 | 1.52 | 1.53 | accepted |
| bal | 18 | 2.920 | 2,937,139 | 5,217 | 3,616 | 8 | 0 | 53 | 1.40 | 1.08 | 1.14 | 1.17 | accepted |
| bal | 20 | 2.197 | 3,359,105 | 5,601 | 4,048 | 8 | 0 | 59 | 0.75 | 1.07 | 1.12 | 1.14 | accepted |
| negmul | 4 | 0.015 | 20,648 | 98 | 36 | 3 | 0 | 10 | — | — | — | — | accepted |
| negmul | 6 | 0.118 | 161,896 | 651 | 290 | 5 | 0 | 16 | 7.80 | 6.64 | 8.06 | 7.84 | accepted |
| negmul | 8 | 0.673 | 972,813 | 3,620 | 1,736 | 7 | 0 | 22 | 5.68 | 5.56 | 5.99 | 6.01 | accepted |
| negmul | 10 | 3.829 | 5,015,501 | 18,006 | 8,886 | 8 | 0 | 28 | 5.69 | 4.97 | 5.12 | 5.16 | accepted |
| negmul | 12 | 12.475 | 16,649,378 | 57,953 | 29,098 | 8 | 0 | 34 | 3.26 | 3.22 | 3.27 | 3.32 | accepted |
| negmul | 14 | 30.214 | 40,000,890 | 134,160 | 68,652 | 8 | 0 | 40 | 2.42 | 2.31 | 2.36 | 2.40 | accepted |
| negmul | 16 | 60.783 | 80,449,829 | 258,763 | 135,204 | 8 | 0 | 46 | 2.01 | 1.93 | 1.97 | 2.01 | accepted |
| negmul | 18 | 111.089 | 145,033,410 | 445,626 | 238,042 | 8 | 0 | 52 | 1.83 | 1.72 | 1.76 | 1.80 | accepted |
| negmul | 20 | 193.664 | 242,883,069 | 710,597 | 388,342 | 8 | 0 | 58 | 1.74 | 1.59 | 1.63 | 1.67 | accepted |
| negdiv | 4 | 0.010 | 9,768 | 72 | 14 | 2 | 0 | 10 | — | — | — | — | accepted |
| negdiv | 6 | 0.074 | 87,104 | 417 | 152 | 4 | 0 | 16 | 7.30 | 5.79 | 10.86 | 8.92 | accepted |
| negdiv | 8 | 0.412 | 536,394 | 2,130 | 954 | 6 | 0 | 22 | 5.53 | 5.11 | 6.28 | 6.16 | accepted |
| negdiv | 10 | 2.122 | 2,820,280 | 10,401 | 5,012 | 8 | 0 | 28 | 5.15 | 4.88 | 5.25 | 5.26 | accepted |
| negdiv | 12 | 8.654 | 11,680,210 | 41,510 | 20,568 | 8 | 0 | 34 | 4.08 | 3.99 | 4.10 | 4.14 | accepted |
| negdiv | 14 | 23.897 | 31,446,344 | 107,819 | 54,494 | 8 | 0 | 40 | 2.76 | 2.60 | 2.65 | 2.69 | accepted |
| negdiv | 16 | 51.485 | 67,106,557 | 221,064 | 114,062 | 8 | 0 | 46 | 2.15 | 2.05 | 2.09 | 2.13 | accepted |
| negdiv | 18 | 95.233 | 125,179,088 | 394,645 | 208,112 | 8 | 0 | 52 | 1.85 | 1.79 | 1.82 | 1.87 | accepted |
| negdiv | 20 | 162.468 | 214,141,672 | 643,882 | 347,308 | 8 | 0 | 58 | 1.71 | 1.63 | 1.67 | 1.71 | accepted |
| subneg | 4 | 0.041 | 37,960 | 119 | 54 | 3 | 0 | 13 | — | — | — | — | accepted |
| subneg | 6 | 0.358 | 502,480 | 1,379 | 718 | 5 | 0 | 15 | 8.85 | 11.59 | 13.30 | 13.24 | accepted |
| subneg | 8 | 1.064 | 2,088,700 | 4,680 | 2,770 | 7 | 0 | 22 | 2.97 | 3.39 | 3.86 | 4.16 | accepted |
| subneg | 10 | 3.450 | 7,237,859 | 14,431 | 9,190 | 8 | 0 | 31 | 3.24 | 3.08 | 3.32 | 3.47 | accepted |
| subneg | 12 | 17.377 | 31,427,678 | 55,281 | 38,546 | 8 | 0 | 33 | 5.04 | 3.83 | 4.19 | 4.34 | accepted |
| subneg | 14 | 26.233 | 50,575,147 | 82,830 | 60,210 | 8 | 0 | 40 | 1.51 | 1.50 | 1.56 | 1.61 | accepted |
| subneg | 16 | 42.596 | 81,997,705 | 122,743 | 93,474 | 8 | 0 | 49 | 1.62 | 1.48 | 1.55 | 1.62 | accepted |
| subneg | 18 | 146.424 | 247,554,585 | 264,512 | 245,286 | 8 | 0 | 51 | 3.44 | 2.16 | 2.62 | 3.02 | accepted |
| subneg | 20 | 249.737 | 408,102,371 | 384,509 | 381,134 | 8 | 0 | 58 | 1.71 | 1.45 | 1.55 | 1.65 | accepted |
| negsum | 4 | 0.094 | 74,824 | 212 | 108 | 3 | 0 | 9 | — | — | — | — | accepted |
| negsum | 6 | 0.477 | 358,150 | 913 | 496 | 5 | 0 | 19 | 5.07 | 4.31 | 4.59 | 4.79 | accepted |
| negsum | 8 | 0.903 | 1,787,748 | 4,020 | 2,388 | 6 | 0 | 24 | 1.89 | 4.40 | 4.81 | 4.99 | accepted |
| negsum | 10 | 6.862 | 13,916,158 | 26,582 | 17,480 | 8 | 0 | 27 | 7.60 | 6.61 | 7.32 | 7.78 | accepted |
| negsum | 12 | 12.123 | 23,874,724 | 42,587 | 29,324 | 8 | 0 | 37 | 1.77 | 1.60 | 1.68 | 1.72 | accepted |
| negsum | 14 | 25.684 | 48,610,656 | 78,914 | 57,524 | 8 | 0 | 42 | 2.12 | 1.85 | 1.96 | 2.04 | accepted |
| negsum | 16 | 61.297 | 113,197,800 | 150,970 | 122,830 | 8 | 0 | 45 | 2.39 | 1.91 | 2.14 | 2.33 | accepted |
| negsum | 18 | 108.605 | 185,756,315 | 209,828 | 187,548 | 8 | 0 | 55 | 1.77 | 1.39 | 1.53 | 1.64 | accepted |
| negsum | 20 | 239.904 | 385,516,219 | 363,882 | 359,136 | 8 | 0 | 60 | 2.21 | 1.73 | 1.91 | 2.08 | accepted |
| zerominus | 4 | 0.021 | 29,064 | 126 | 50 | 3 | 0 | 10 | — | — | — | — | accepted |
| zerominus | 6 | 0.157 | 197,800 | 775 | 352 | 5 | 0 | 16 | 7.52 | 6.15 | 7.04 | 6.81 | accepted |
| zerominus | 8 | 0.845 | 1,117,622 | 4,128 | 1,990 | 7 | 0 | 22 | 5.40 | 5.33 | 5.65 | 5.65 | accepted |
| zerominus | 10 | 4.281 | 5,465,324 | 19,557 | 9,668 | 8 | 0 | 28 | 5.07 | 4.74 | 4.86 | 4.89 | accepted |
| zerominus | 12 | 13.038 | 17,464,488 | 60,632 | 30,480 | 8 | 0 | 34 | 3.05 | 3.10 | 3.15 | 3.20 | accepted |
| zerominus | 14 | 31.886 | 41,258,780 | 138,063 | 70,722 | 8 | 0 | 40 | 2.45 | 2.28 | 2.32 | 2.36 | accepted |
| zerominus | 16 | 61.818 | 82,246,608 | 264,002 | 138,066 | 8 | 0 | 46 | 1.94 | 1.91 | 1.95 | 1.99 | accepted |
| zerominus | 18 | 119.039 | 147,487,194 | 452,329 | 241,816 | 8 | 0 | 52 | 1.93 | 1.71 | 1.75 | 1.79 | accepted |
| zerominus | 20 | 196.700 | 246,138,170 | 718,908 | 393,164 | 8 | 0 | 58 | 1.65 | 1.59 | 1.63 | 1.67 | accepted |
| plus0 | 4 | 0.015 | 20,736 | 82 | 36 | 3 | 0 | 7 | — | — | — | — | accepted |
| plus0 | 6 | 0.070 | 99,088 | 368 | 176 | 5 | 0 | 11 | 4.82 | 4.49 | 4.89 | 4.78 | accepted |
| plus0 | 8 | 0.289 | 420,894 | 1,518 | 748 | 7 | 0 | 15 | 4.14 | 4.12 | 4.25 | 4.25 | accepted |
| plus0 | 10 | 1.035 | 1,458,812 | 5,136 | 2,568 | 8 | 0 | 19 | 3.58 | 3.38 | 3.43 | 3.47 | accepted |
| plus0 | 12 | 2.425 | 3,248,649 | 11,053 | 5,622 | 8 | 0 | 23 | 2.34 | 2.15 | 2.19 | 2.23 | accepted |
| plus0 | 14 | 4.458 | 5,947,812 | 19,464 | 10,094 | 8 | 0 | 27 | 1.84 | 1.76 | 1.80 | 1.83 | accepted |
| plus0 | 16 | 7.273 | 9,756,982 | 30,601 | 16,200 | 8 | 0 | 31 | 1.63 | 1.57 | 1.60 | 1.64 | accepted |
| plus0 | 18 | 11.302 | 14,924,607 | 44,728 | 24,188 | 8 | 0 | 35 | 1.55 | 1.46 | 1.49 | 1.53 | accepted |
| plus0 | 20 | 16.889 | 21,751,288 | 62,141 | 34,338 | 8 | 0 | 39 | 1.49 | 1.39 | 1.42 | 1.46 | accepted |
| zeroplus | 4 | 0.006 | 9,390 | 35 | 14 | 3 | 0 | 7 | — | — | — | — | accepted |
| zeroplus | 6 | 0.026 | 36,872 | 135 | 62 | 5 | 0 | 11 | 4.21 | 3.86 | 4.43 | 3.93 | accepted |
| zeroplus | 8 | 0.107 | 145,792 | 523 | 254 | 7 | 0 | 15 | 4.06 | 3.87 | 4.10 | 3.95 | accepted |
| zeroplus | 10 | 0.317 | 449,840 | 1,570 | 782 | 8 | 0 | 19 | 2.95 | 3.00 | 3.08 | 3.09 | accepted |
| zeroplus | 12 | 0.584 | 813,086 | 2,702 | 1,382 | 8 | 0 | 23 | 1.84 | 1.72 | 1.77 | 1.81 | accepted |
| zeroplus | 14 | 0.911 | 1,250,990 | 3,930 | 2,070 | 8 | 0 | 27 | 1.56 | 1.45 | 1.50 | 1.54 | accepted |
| zeroplus | 16 | 1.296 | 1,784,428 | 5,270 | 2,862 | 8 | 0 | 31 | 1.42 | 1.34 | 1.38 | 1.43 | accepted |
| zeroplus | 18 | 1.855 | 2,436,379 | 6,738 | 3,774 | 8 | 0 | 35 | 1.43 | 1.28 | 1.32 | 1.37 | accepted |
| zeroplus | 20 | 2.471 | 3,232,235 | 8,350 | 4,822 | 8 | 0 | 39 | 1.33 | 1.24 | 1.28 | 1.33 | accepted |
| pow1 | 4 | 0.015 | 19,728 | 82 | 36 | 3 | 0 | 7 | — | — | — | — | accepted |
| pow1 | 6 | 0.071 | 97,408 | 368 | 176 | 5 | 0 | 11 | 4.85 | 4.49 | 4.89 | 4.94 | accepted |
| pow1 | 8 | 0.310 | 418,344 | 1,518 | 748 | 7 | 0 | 15 | 4.38 | 4.12 | 4.25 | 4.29 | accepted |
| pow1 | 10 | 1.115 | 1,455,413 | 5,136 | 2,568 | 8 | 0 | 19 | 3.59 | 3.38 | 3.43 | 3.48 | accepted |
| pow1 | 12 | 2.514 | 3,244,192 | 11,053 | 5,622 | 8 | 0 | 23 | 2.25 | 2.15 | 2.19 | 2.23 | accepted |
| pow1 | 14 | 4.715 | 5,942,120 | 19,464 | 10,094 | 8 | 0 | 27 | 1.88 | 1.76 | 1.80 | 1.83 | accepted |
| pow1 | 16 | 7.707 | 9,749,877 | 30,601 | 16,200 | 8 | 0 | 31 | 1.63 | 1.57 | 1.60 | 1.64 | accepted |
| pow1 | 18 | 11.668 | 14,915,693 | 44,728 | 24,188 | 8 | 0 | 35 | 1.51 | 1.46 | 1.49 | 1.53 | accepted |
| pow1 | 20 | 17.324 | 21,740,210 | 62,141 | 34,338 | 8 | 0 | 39 | 1.48 | 1.39 | 1.42 | 1.46 | accepted |

</details>

**What the default limits do.** The default work budget (1,000,000 visits) refuses `g` from n = 16 (`3*x0 - 3*x1 - ... - 3*x15`,
31 nodes), `z`/`e`/`k`/`n` from n = 16, `Lsub` from n = 16 (`x0 - x1 - ... - x15`, 31 nodes), `o` from n = 10, `r` from n = 12,
and the negation families around n = 22–24; these refusals are controlled (`ExpressionResourceLimitException`, kind `Work`), but
they reject ordinary, small expressions.

**Sources of the cost.**

- *`F` is fixed.* The custom-operator chain of P16 (848,334 visits at n = 10) costs 8n − 1 visits: since ETC R1b a custom operator
  is not inert, so the rules around its terms are gated and nothing is compared.
- *`Lsub` and the free-parameter tie (new, S5A-12).* `x0 - x1 - ... - x(n-1)` with free parameters costs 1,960,419 visits at
  n = 16; the same tree with its parameters bound by a lambda costs 14,986 (×131), and a chain with distinct coefficients
  (`2*x0 - 3*x1 - ...`) 18,818. Free parameters of one type tie in the S4 order, so the canonical form depends on the order of the
  terms it is built from, and the binding is the only difference between the free and bound runs. The causal experiment of §10
  (S5A-06) locates the cost in the `0 - t -> -t` re-entries of `SubstractionWithZero(0, t)`: without them the free chain costs
  9,771 visits at n = 14 instead of 326,023.
- *Common lever.* Disabling `SubstractionWithZero(0, t)` (diagnostic only, it changes outputs) also divides `g` n = 14 by 16
  and `p` n = 16 by 4.8: re-simplifying a canonical chain re-runs `0 - t -> -t -> AdditionWithNegate` at every level.
- *`g` and `z`/`e`/`k`/`n` (documented L-5, L-6).* Not caused by the tie (the bound versions cost the same: g14 178,799 against
  178,814, z14 554,010 against 554,025); the comparer performs 0.8 to 0.9 simplification per visit (`comparer` column), at depth
  2–3, as P15/P16 described. `z`, `e`, `k` and `n` have identical counts: the opaque constant costs exactly its known-safe control (the
  P15 invariant still holds), so their residual is the numeric shape of the first-sorted-last chain.
- *`o`/`r` (L-7):* user calls grow with the work; reuse is forbidden by the contract after a user call (P3 round 6, P15).
- *Negation re-entries (S5A-08) and redundant re-transformation (S5A-07):* the rules re-enter `TransformCore` on operands that are
  already prepared; `plus0`/`pow1` against `zeroplus` isolates the one-line cause (×4.1 at n = 12, ×7.4 at n = 20).
- *`div` (observation):* the right-nested division `x0 / (x1 / (x2 / ...))` doubles every two terms (7,171 visits at n = 20):
  `DivisionOfDivision(e, t, u/v)` re-transforms the product it builds; small today, exponential in shape.

**Repeated work that can be eliminated without changing the observable contract:** the re-transformation of prepared operands
(S5A-07, S5A-08: inert arithmetic only, the P16 guards), the re-preparation caused by tied free parameters (S5A-12: an order that
does not depend on the source order for free parameters, for example by first-occurrence position, would make the forms stable).
**Imposed by the contract:** the user calls of `o`/`r` (P15 options need a contract change); the comparer simplifications whose
operands may call user code.

## 8. Observable effects (Phase G)

| Result | Seeds |
|---|---:|
| Same value, exception and ordered evaluation log | 7,999 |
| Different evaluation log (dropped, repeated, reordered or moved effect) | **0** |
| Different exception | **0** |
| Value difference | 1 (`0^y -> 0` at `y = 0`: the documented domain of the identity) |

Value differences around constants of extreme magnitude (`(1e300 + z) - (1e300 + z)` reassociated) are reported as `extreme`, the
S3 reassociation latitude; a first run without that distinction had 7 of them, all of this kind. 5,262 of the 8,000 trees were
rewritten by the simplifier, including factoring, cancellation, zero products and canonical
reordering around effectful terms. The C-06 promise holds for calls, getters and throwing calls. User code run by the
simplification itself:

| Scenario | Observed | Contract |
|---|---|---|
| A throwing `Equals` of an opaque constant | the user exception propagates out of `Simplify` | as documented |
| An unstable `Equals` (alternating answers) | terminates | as documented (answers are the user's) |
| A logging `Equals`, simplified twice | identical call logs | deterministic |
| `2c - 2c` with one opaque value | cancels to `0`, two reference `Equals` calls | as documented |

Not covered by this campaign and kept as is: the ETC R4 opaque-node policy (no inspection added), dynamic operations (covered by
the ETC R4 suites).

## 9. S3 contract (Phase F)

### Safety matrix (what the code enforces)

| Expression type | Domain | Allowed transformation | Preconditions in the code |
|---|---|---|---|
| Integral arithmetic (`int`, `long`, `short`, unsigned) | modular (unchecked) | zero/one identities, folding, factoring, canonical reordering | `IsOrdinaryBinaryArithmetic`; folding throws on a constant zero divisor and `MinValue / -1` (S5A-04); no field division (`DivisionOfDivision` refused), `x / -1` refused for signed types (C-06), `0 / x` refused |
| `float` / `double` | IEEE-754, symbolic | everything, including field reassociation and `0 / x -> 0`, `x * 0 -> 0`, `x - x -> 0` | NaN, infinities and signed zeros not preserved by symbolic identities (documented); constant folding exact; zero-divisor constants refused for field division |
| `decimal` | CLR decimal | zero/one identities, folding, **field reassociation by a constant divisor** | arithmetic is not inert (may overflow), so factoring and reordering are gated (`2m*x - 2m*x` kept); `0 / x` refused for its exception (C-06) — but `DivisionOfDivision` still reassociates (S5A-03, fixed by P18b: no decimal field reassociation since) |
| Custom operator | user | none (node atomic) | `IsOrdinaryBinaryArithmetic`; not inert since R1b, so neighbouring rules are gated too |
| Lifted nullable | lifted | none | `IsLifted`/`IsLiftedToNull` refused (`int? x * 0` kept) |
| Math functions | function domains | logarithm combination, trigonometric quotients and products, `sin² + cos² -> 1` | positive real domain assumed for logarithms (documented); poles and rounding not preserved |
| Powers (`double` only) | real | `x^0`, `x^1`, `x^-n -> 1/x^n`, `x^a * x^b`, `0^x -> 0`, `1^x -> 1` | `0^x -> 0` valid for `x > 0` only (documented) |

### Mandatory identities (observed)

Differences over every assignment of the domain values (`double`: 16 values including ±0, NaN, ±∞, `Epsilon`, `1e-310`,
`MaxValue`, ±`1e300`).

| Identity | Result | Classification |
|---|---|---|
| `x * 0` | `0` (double, float, int, long, decimal); kept for `int?` | IEEE (NaN, -0) = S3 latitude; nullable kept (S3) |
| `0 / x` | `0` (double, float); kept for integral and decimal | IEEE at `x = 0` = S3 latitude; exception kept (C-06) |
| `x / x` | kept for every type | conservative (no rule) |
| `x - x` | `0`; kept for `int?` | IEEE for NaN/∞ = S3 |
| `x / -1` | `-x` (double, float, decimal); kept for signed integral types | exact; `MinValue` exception kept (C-06) |
| `x ^ 0`, `x ^ 1`, `1 ^ x` | `1`, `x`, `1` | exact on the finite domain |
| `0 ^ x` | `0` | **value difference at `x = 0` (1 against 0) and `x < 0` (∞ against 0)**: domain precondition `x > 0`, documented — class 4 |
| `x ^ -1`, `x ^ -2` | `1 / x`, `1 / x^2` | rounding (documented, P13) |
| `log(x) + log(y)`, `log(x) - log(y)`, `log10` | `log(x*y)`, `log(x/y)` | domain (non-positive inputs) and rounding = S3 |
| `sin(x)^2 + cos(x)^2` | `1` | rounding, amplified when the sum is raised to a large power (`(sin²y + cos²y)^((-2)^y)^z`: 9.1e15 against 1 at y = z = 7) = S3 |
| `sin(x) / cos(x)`, `cos(x) / sin(x)`, `cos(x) * tan(x)`, `sin(x) / tan(x)` | `tan`, `1 / tan`, `sin`, `cos` | rounding; `sin(x)/tan(x)` at `x = 0` (NaN against 1) = domain |
| `x^y + z^2`, `x^2 + z^y` | **throws** | S5A-01 |
| `double.Round(x, 2)` | **throws** | S5A-02 |
| `-(-x)` | kept, even after several passes | S5A-10 |
| `x / (1 / x)` | `x^2 / 1` (`(x * x) / 1f` for `float`) | P17 limitation (`/ 1`) |

Differential campaign (8,000 seeds), worst class per seed:

| Type | same | rounding | signedZero | ieee | extreme | VALUE | EXC-ADDED | EXC-REMOVED | EXC-CHANGED | SIMPLIFY-THROWS-LIKE-SOURCE | SIMPLIFY-THROWS | COMPILE-FAIL | TYPE-CHANGED | LINQ | refusal |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| decimal | 1086 | 0 | 0 | 0 | 0 | 0 | 1 | 2 | 3 | 51 | 0 | 0 | 0 | 0 | 0 |
| double | 723 | 71 | 19 | 0 | 328 | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| float | 636 | 74 | 26 | 5 | 402 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| int | 1008 | 0 | 0 | 0 | 9 | 0 | 0 | 0 | 0 | 126 | 0 | 0 | 0 | 0 | 0 |
| int? | 1143 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| long | 1030 | 0 | 0 | 0 | 8 | 0 | 0 | 0 | 0 | 105 | 0 | 0 | 0 | 0 | 0 |
| uint | 1000 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 142 | 0 | 0 | 0 | 0 | 0 |

Anomalies: 6 `decimal` exception differences (S5A-03), `0 ** y -> 0` at `y = 0` (domain, class 4) and `(sin²y + cos²y)^((-2)^y)^z` (rounding amplified by a large power, S3). `SIMPLIFY-THROWS-LIKE-SOURCE` (424 seeds): the simplifier throws on a constant zero divisor or an overflowing fold and the source throws on every assignment.

Every `SIMPLIFY-THROWS` case is a constant zero divisor (or a fold that overflows) in a tree whose evaluation throws on every
assignment too (often another exception first, such as an overflow at extreme inputs): the documented "throws as evaluating the
node would". The exception is genuinely moved only when the node is not always evaluated (a conditional branch: S5A-04, found by
the targeted probes, not by the generator, which builds no conditional). The 6 `decimal` exception differences are S5A-03.

## 10. Confirmed anomalies

Each anomaly has a characterization test in `ExpressionSimplifierDeepAuditTests` that passes on the baseline and pins the
defective behavior; the fixing stage changes that assertion in the same commit as the fix.

### S5A-01 — `x^y + z^2` throws `NullReferenceException` (High)

- **Reproducer:** `Simplify(Add(Power(x, y), Power(z, 2)))`, also `x^2 + z^y`, `sin(x)^y + cos(x)^2`, `x^y + x^z`; and
  `ExpressionComparer.Equals`/`GetHashCode` of such trees, which simplify first.
- **Proof:** `TargetInvocationException` wrapping `NullReferenceException`; 975 corpus cases, 98 more at the second pass of other
  inputs (the first pass builds the shape), and the comparer campaign (`((z^2) * (z*z)) + ((2^2)^y)`).
- **Location:** `ExpressionSimplifier.INumber.cs`, `AdditionOfCos2andSin2Number`: `var leftRight = left.Right as ConstantExpression;`
  then `leftRight.Value` (and `rightRight.Value`) without a null check. Any `double` sum of two ordinary powers reaches it, because
  the earlier rules decline and `double` implements `ITrigonometricFunctions<double>`.
- **Cause (causal experiment):** with the exponents checked (`leftRight is null || rightRight is null` declines), the termination corpus (62,270 cases without compositions) goes from 849 exceptions to 0, and nothing else changes.
- **Fix:** pattern-match the exponents (`left.Right is ConstantExpression { Value: 2.0 }`), no other change.

### S5A-02 — `double.Round(x, 2)` throws `InvalidOperationException` (High)

- **Reproducer:** `Simplify(Call(double.Round(double, int), x, 2))`; also `Round(double, MidpointRounding)` and
  `Round(double, int, MidpointRounding)`.
- **Location:** `ExpressionSimplifier.Math.cs`, `TransformCall` (used by `RoundConversionMath` and the other `*ConversionMath`
  rules) looks the target method up with an all-`double` signature of the call's arity (`ResolveTransformCallSignature`) and throws
  when there is none. The rule matches every `double.Round` overload (`ExpressionCallSignatureAttribute` matches type and name only).
- **Cause (causal experiment):** keeping the call when no all-`double` overload exists makes `double.Round(x, 2)` simplify to itself, with no value difference over the S3 matrix.
- **Fix:** rebuild with the call's own `MethodInfo` (or decline the overloads whose parameters are not all `double`). The other
  conversion rules have no overload with a non-`double` parameter.

### S5A-03 — decimal nested divisions change the exceptions (Critical for the C-06 contract, narrow trigger)

- **Reproducer:** `1m / (b / 3m)` → `3m / b`: at `b = 1e-28` the source divides by `b / 3m = 0` and throws
  `DivideByZeroException`, the result returns `3e28` (exception removed). `(a / 0.5m) / b` → `a / (0.5m * b)`: at `(1, 1e-28)` the
  source returns `2e28`, the result throws `DivideByZeroException` (exception added). `a / (b / 3m)` → `(3m * a) / b`: at
  `(3e28, 3)` the result throws `OverflowException` (exception added).
- **Proof:** 6 seeds of the differential campaign (`EXC-REMOVED` 2, `EXC-ADDED` 1, `EXC-CHANGED` 3), reduced to the forms above;
  the interpreter agrees with the compiler.
- **Location:** `IsOrdinaryFieldDivision` (`ExpressionSimplifier.cs`) accepts every type of `Types.FloatingPointNumber`, which lists
  `decimal`; `DivisionOfDivision` (three overloads) relies on it. The effect gate lets the rule run because a decimal division by a
  non-zero constant cannot throw, but the rewritten operation can.
- **Contract:** ETC R1b (C-06) states that "the identities whose own operator may throw keep that exception", and
  `DivideWithZero` declines `decimal` for exactly this reason; S3 had left decimal reassociation to the symbolic latitude before
  C-06 existed. The two policies contradict each other on this rule.
- **Cause (causal experiment):** restricting `IsOrdinaryFieldDivision` to `float`/`double` removes 29 of the 30 exception differences of a 6,000-seed `decimal` campaign (`EXC-ADDED` 2 → 0, `EXC-REMOVED` 9 → 1, `EXC-CHANGED` 18 → 0, the one `VALUE` difference too); the last one is S5A-13, a different cause.
- **Fix:** exclude `decimal` from field reassociation (`IsOrdinaryFieldDivision` restricted to `float`/`double`), or a new
  decision recorded in the migration guide if decimal reassociation is to stay.
- **Fixed (P18b, 2026-11-05):** `IsOrdinaryFieldDivision` accepts `double` and `float` only; the 6,000-seed `decimal` campaign
  goes from 30 exception differences and 1 value difference to none (together with S5A-13). Migration guide: "decimal division
  exceptions (S5 P18b)".

### S5A-04 — an exception of an unevaluated branch is raised while simplifying (Medium)

- **Reproducer:** `i > 0 ? 1 / 0 : 2`, `i > 0 ? i / 0 : 2`, `i > 0 ? int.MinValue / -1 : 2`, `m > 0 ? decimal.MaxValue * 2m : 0m`:
  `Simplify` throws (`DivideByZeroException`, `OverflowException`), while the source evaluates to `2` (or `0m`) for `i <= 0`.
  `ExpressionComparer.Equals` of such trees throws too.
- **Location:** `DivideWithZeroOrOne` (integral/decimal zero divisor: `throw new DivideByZeroException()`) and the typed folding
  (`FoldConstants`, P12) of `AdditionOfConstants`, `SubstractionOfConstants`, `MultiplicationOfConstants`, `DivisionOfConstants`.
- **Contract:** documented as "throws as evaluating the node would", which holds for a node that is always evaluated; inside a
  conditional, a short-circuit or an uninvoked lambda the exception moves from evaluation time to simplification time.
- **Fix (decision needed):** keep the node unfolded when the fold throws (the evaluation raises it when it runs), or keep throwing
  only outside lazily evaluated contexts, or document the current behavior. This changes a documented behavior, so it needs an
  explicit decision.

### S5A-05 — rule results left for the next pass, rule priorities (Medium)

- **Reproducer:** `(x + y) - (x + y)` → `((x + y) - x) - y` (second pass `0`); `(3 - z) - (3 - z)` →
  `((3 + (0 - z)) - 3) - (0 - z)`; `(-x) * 2` → `2 * (-x)` (then `-(2 * x)`); `(2 * x) * 3` → `3 * (2 * x)` (then `6 * x`).
  The comparer, which simplifies once, answers `(x + y) - (x + y) ≠ 0`.
- **Location:** plan order (§3): `SubstractionWithAddition` (16) and `SubstractionWithSubstraction` (17) return their reassociation
  without dispatching it and run before `SubstractionOfEqualsElements` (20); `Multiplication(e, t, c)` (22) returns `c * t`
  before `MultiplicationWithNegate` (26–27) can run, and its result is not folded with an inner coefficient.
- **Proof:** 11 violations of `Equals(a, b) == Equals(S(a), S(b))` in the comparer campaign are all this shape; the `t - (sum)`
  residue (5,012 cases) of §6.
- **Cause (causal experiment):** letting the two reassociations decline on structurally equal operands makes `(x + y) - (x + y)` cancel in one pass, but only 8 more corpus cases reach a fixed point after one pass: the pair is a precise defect, not the main source of non-idempotence (see S5A-06).
- **Fix (P20):** finalize these rule results locally (the way `BuildFactoredProduct` calls `MultiplicationWithZeroOrOne`), or let
  the reassociation decline when its operands are structurally equal; do not reorder the plan blindly.

### S5A-06 — repeated passes oscillate, bound parameters included (Medium, documented limitation with a consequence)

- **Reproducer:** `(x, y, z) => (x - y) - z` alternates with `(x, y, z) => x + ((0 - y) + (0 - z))`; `((1 - x) - y) - z` has
  period 3; family `g` (n = 8) has no fixed point in 8 passes. 1,679 corpus cases cycle (2.4 %).
- **Consequence:** `ExpressionComparer.Equals(e, Simplify(e))` is `false` for such `e`, because the comparer simplifies each side
  once and lands on the two different forms of the cycle; and re-simplifying a canonical form can cost far more than the source
  (family `o` n = 8: the second pass is refused by the default budget).
- **Location / cause:** P10 ("bounded cycle", four rules returning non-canonical shapes); this audit shows it is not caused by the
  S4 tie of free parameters.
- **Cause (causal experiments):** disabling `SubstractionWithZero(0, t)` (diagnostic only) removes **every** cycle of the corpus
  (1,468 → 0; more cases need three passes instead); disabling the two reassociations leaves 265. The `0 - t -> -t` re-entry is
  necessary to every observed cycle.
- **The same rewrite is the dominant cost of the worst families:** without it, `Lsub` n = 14 costs 9,771 visits instead of 326,023
  (×33), `g` n = 14 11,424 instead of 178,799 (×16), `p` n = 16 12,424 instead of 60,068 (×4.8), `zerominus` ×6.7, `negsum` ×2.5.
  Re-simplifying a canonical chain turns each `0 - t` into `-t` again, then `AdditionWithNegate` compares and re-prepares (the
  P16 mechanism, whose prepared-operand reuse only covers inert arithmetic and does not stop the rewrite itself).
- **Fix (P20):** make the canonical additive representation a fixed point of `SubstractionWithZero(0, t)`, `AdditionWithNegate` and
  the two reassociations (decline on a canonical chain, or finalize their result), measured against the P10/P16 families; it is
  also the most promising lever of P19 (§7).

### S5A-07 — `t + 0` and `t ^ 1` re-transform an operand that is already prepared (Medium)

- **Reproducer:** nested `((acc * x_i) + 0)`: 11,053 visits at n = 12 against 2,702 for `0 + (acc * x_i)` (×4.1), 171,693 against
  16,558 at n = 28 (×10.4); `(acc * x_i) ^ 1`: same as `+ 0`. Being a second pass on the operand, it also changes the result:
  `((x - y) - z) + 0` → `(x - y) - z` while `0 + ((x - y) - z)` → `x + ((0 - y) + (0 - z))`.
- **Location:** `AdditionWithZero(e, left, 0)` returns `TransformCore(left)`, its mirror `AdditionWithZero(e, 0, right)` returns
  `right`; `PowerByZeroOrOne` returns `TransformCore(left)` for the exponent 1. The operand is already prepared
  (`PrepareExpression` = `FoldNaNOperand(TransformCore(...))`).
- **Cause (causal experiment):** returning the prepared operand brings `plus0` and `pow1` (n = 12) from 11,053 to 2,702 visits, exactly the `zeroplus` control, and changes 3 of the 62,270 corpus convergence categories.
- **Fix (P19):** return the prepared operand, as the mirror overload does (no user-code change: the operand's own simplification
  already ran; the output changes where the operand was not a fixed point, which must be documented).

### S5A-08 — nested negations of differences grow super-polynomially (High)

- **Reproducer:** `negsum` = `-(...-(-(x0 - x1) - x2)... - x(n-1))`, `subneg` = `(-(...(-(x0) - x1)...)) - x(n-1)`, `negmul`,
  `negdiv`, `zerominus` (§7). The default budget refuses `negsum` at n = 24 (70 nodes: 1.57 M visits).
- **Location / cause:** every level re-enters the engine on operands that are already prepared (`NegateWithSubstraction`,
  `SubstractionWithNegate`, `MultiplicationWithNegate`, `DivisionWithNegate`, `SubstractionWithZero(0, t)` all call `TransformCore`
  on a node rebuilt from prepared operands), the re-preparation re-runs the factoring probes on canonical chains (the P16
  mechanism, whose prepared-operand reuse only covers `AdditionWithNegate`). Disabling `SubstractionWithZero(0, t)` (diagnostic only: it changes outputs) divides `negsum` n = 16 by 2.5, `zerominus` by 6.7 and leaves `negmul` unchanged, whose cost comes from `MultiplicationWithNegate`; disabling the two reassociations moves `negsum` by 18 % only.
- **Fix (P19):** extend the P16 prepared-operand path (`TransformCoreWithPreparedOperands`) to the negation rules where the
  operands are inert arithmetic, with the same guards (exact type, empty lexical scope, inert operands and forms).

### S5A-09 — equal terms grouped but not combined; power combination on direct operands only (Medium)

- **Reproducer:** `(x, y) => x + (x + y)` → `(x + x) + y` (combined by the second pass); `x * (x * y)` is kept, and the comparer
  does not identify it with `y * x^2`; with free parameters `{x, x, y}` gives 3 distinct sums and 4 distinct products over the 12
  orders (canonicalization campaign).
- **Location:** `CanonicalizeAdditiveExpression` groups structurally equal terms (`GroupBy`) and rebuilds each group as a sum without
  folding the occurrences; `MultiplicationOfEqualsElements(e, t, u)` compares its two direct operands only.
- **Fix (P20):** combine the occurrences of a group of inert terms in the canonicalization (`n * t`), the way `AggregateAdditiveConstants`
  sums constants; collect powers of equal bases in `CanonicalizeMultiplicativeExpression`.

### S5A-10 — no rule removes a double negation (Low)

`-(-x)` is kept by any number of passes; `x - (-y)` and `-(a - b)` have rules. A local rule `Negate(Negate(t)) -> t` (ordinary,
non-lifted negations) is exact for every type with an ordinary negation.

### S5A-11 — the dispatch order relies on the unspecified order of `Type.GetMethods` (Low)

`BuildPlan` reads the rules in reflection order and the plan semantics depend on it (S5A-05 shows priorities that matter). CoreCLR
returns declaration order today, and the partial files contribute their rules in the order the compiler receives them
(`ExpressionSimplifier.cs`, then `.INumber.cs`, then `.Math.cs` on this machine; MSBuild does not promise a glob order across file
systems). The order matters across files: `log(x) + log(x)` gives `2 * Log(x)` because `AdditionOfEqualsElements` (19) precedes
`LogarithmSimplificationAddNumber` (40); with the other order it would be `Log(x * x)`. Not verified on Linux (the local WSL has
SDK 7.0 only, which cannot build `net8.0`); the Ubuntu CI passing does not prove the orders agree, since no test covers such a pair.
A test pinning the plan order (or an explicit priority) removes the latent risk.

### S5A-12 — differences of free parameters of one type grow exponentially (High)

- **Reproducer:** `x0 - x1 - ... - x(n-1)` with free `double` parameters: 58,849 visits at n = 12, 1,960,419 at n = 16 (refused by
  the default budget, 31 nodes), 11.7 M at n = 18 (8 s), over the 6 GB heap guard at n = 20. The same tree with its parameters bound
  by a lambda: 7,808 and 14,986 visits; with distinct coefficients (family `B`): 18,818 at n = 16.
- **Location / cause:** free parameters of one type tie in the S4 canonical order (L-3), so the canonical chain depends on the
  order of the terms it is rebuilt from; the binding is the only difference between the exponential and the polynomial run. The
  causal experiment shows that the cost is carried by the `0 - t` re-entries of `SubstractionWithZero(0, t)` (×33 without them).
- **Fix (P19/P20):** order free parameters deterministically (first-occurrence position within the canonicalized node, or a stable
  identity), or make the canonical chain a fixed point (S5A-06); both need the P10 user-code gate.
- **Mitigated, not fixed (Flatten PR B, 2026-11-13):** the built-in simplifier canonicalizes a linear source chain at once
  (`docs/expressions/SimplifierFlatten.md`): `x0 - ... - x(n-1)` costs `n + 1` visits and is no longer refused, with the same
  result. The cause is open: the canonical chain is still not a fixed point, so simplifying the *result* again (its `0 - t`
  terms are not linear), a derived simplifier, and any chain that is not linear still pay the level by level growth. The same
  rule removes about a sixth of the visits of S5A-08 (117,637 to 99,916 at n = 20) without changing its growth.

### S5A-13 — a decimal division by a constant of magnitude below 1 is treated as inert (Critical for the C-06 contract, narrow trigger)

- **Reproducer:** `(a / 0.5m) - (a / 0.5m)` → `0`: at `a = decimal.MaxValue` the source throws `OverflowException`, the result
  returns `0`.
- **Location:** `ExpressionEffectAnalysis.IsSafeDivisor` (`decimal value => value != 0m`): a decimal division by any non-zero
  constant is classified as unable to throw, but dividing by a constant of magnitude below 1 multiplies and can overflow. The rule
  gate then lets `SubstractionOfEqualsElements` cancel the two evaluations (and lets `DivisionOfDivision` run, S5A-03).
- **Found by:** the S5A-03 causal experiment (the one exception difference left once decimal field division is disabled).
- **Fix:** a decimal constant divisor is safe only when its magnitude is at least 1 (ETC R1b effect table, with its own tests).
- **Fixed (P18b, 2026-11-05):** `IsSafeDivisor` accepts a decimal divisor of magnitude at least 1 only (division and remainder).
  The P18-A behavior analysis was never affected: a decimal division node carries `decimal.op_Division`, classified as partial.

### S5A-14 — a constant other than 1 added to a call nesting grows exponentially (High)

Found by the S5 P18 C2b performance audit (2026-11-06), on the PR #686 head `30046b86`, under the **default** policy; present
under every policy as long as the levels are not folded. Not caused by C1/C2 nor by C2b.

- **Reproducer (harness families `constabs` and `nanabs`):** `c + Abs(c + Abs(... Sin(x0)))` with a constant `c` other than 1
  (`2`, `NaN`, ...). Comparer simplifications `2^(n+2) - 4` (12, 60, 252, 1,020, 4,092 at n = 2, 4, 6, 8, 10), comparer depth
  `n` (no cap), every re-entry attributed to `AdditionOfEqualsElements`; transformation visits 137, 2,297, 36,857, 589,817 at
  n = 4, 8, 12, 16, structural visits 435, 8,519, 139,003, 2,227,887, 0.31 s at n = 16 for a 50-node tree. Control
  `1 + Abs(1 + Abs(...))`: no comparer simplification, 8 to 32 visits.
- **Cause:** `AdditionOfEqualsElements` only declines when an augmented operand is the constant **exactly 1**
  (`ExpressionSimplifier.cs`, "Check if the single factor is truly 1"); for any other constant it probes the factors with
  `FactorEqualityProbe`, which simplifies the other operand `Abs(...)` through the comparer. That simplification of the whole
  subtree runs the same probe on the level below, so each level doubles the work. The S5 P16 reuse of prepared forms does not
  apply: it is restricted to inert arithmetic, and `Abs` is a call.
- **Comparison with #686:** identical counters with the C2b branch under the default policy. Under a non-default policy C2b folds
  every `NaN` level before the rule runs: 199 structural visits and 56 µs at n = 16 (`nanabs`), which hides, without fixing, the
  `c` ≠ `NaN` case.
- **Fix (proposed P19d, independent, before C3):** the operands a rule receives are already prepared, so the probe should not
  simplify them again level by level: compare a constant with an already prepared operand structurally (a prepared operand
  equal to a constant is that constant, constant folding having run), or extend the P16 prepared-operand reuse to this probe.
  Declining outright is not equivalent: `c + t` factors into `(1 + 1) * c` when `t` simplifies to `c`. Tests: the `constabs`
  family bounded (linear visits), the factoring suites unchanged, the P9/P15 comparer-memo invariants.
- **Fixed (P19d, 2026-11-10), exact route (user decision):** both structural short-cuts above were rejected because they are
  not exact (a second pass can reveal the equality the prepared operand hides). Instead the P9/P15 comparison memo (1) activates
  on arithmetic of depth 2 at any comparison depth and on a method call from comparison depth 2 (the P9 signal D6/A2 required depth 6 and an
  arithmetic root, which `Abs(...)` never is), and (2) is retained across the comparison sessions of one `Transform` frame
  instead of being released with each outermost comparer simplification. Entries are unchanged (strict keys, user-code-free
  results only): when both versions complete, they return the same results; the reduced work can let an operation that the
  resource limits refused before complete now. `constabs`: 589,817 -> 468 visits at n = 16, 72 ms at n = 64 (quadratic).
  Pinned by `S5A14_ConstantPlusCallNesting_GrowsPolynomially`. Review follow-up: a public `ExpressionComparer.Equals` made from
  user code detaches the retained memo, and the observed tier refuses a call or member whose reflection object is not the
  runtime's (ArchitectureC-Audit.md §8.3).

### S5A-15 — the power-combination probe makes a Horner chain grow polynomially of high degree (High)

Found by the same audit, same base, default policy, also present under every policy (no `NaN` involved).

- **Reproducer (harness family `horner`):** `((x0 * x1 + 1) * x1 + 1) * x1 + 1 ...`. Comparer simplifications 6, 30, 396, 1,144,
  2,276 at n = 2, 4, 8, 12, 16 (comparer depth capped at 7), re-entries of `MultiplicationOfEqualsElements` 6, 30, 383, 909,
  1,451; transformation visits 1,176, 6,004, 30,188 and structural visits 7,388, 81,004, 1,138,284 at n = 8, 16, 32 (×14 per
  doubling, about n^3.8), 74 ms at n = 32 for a 129-node tree; extrapolated to seconds at n = 64.
- **Cause:** at every level the power rule `MultiplicationOfEqualsElements(e, left, right)` (`x^a * x^b -> x^(a+b)`) compares the
  bases `(1 + x1 * (...))` and `x1` with `ExpressionComparer.EqualsWithinOperation`, which simplifies both operands; the nested
  simplification of the left operand meets the same comparison on every lower level.
- **Comparison with #686:** identical counters with the C2b branch under the default, aggressive and permissive policies.
- **Fix (same P19d):** compare the bases structurally first when neither operand is a power and their node types differ before
  any simplification, or reuse the prepared operands (the P16 path) for the bases; measure with `horner` and the `pow` family.
- **Fixed (P19d, 2026-11-10), same exact route as S5A-14:** the memo was already active from comparer depth 6, but each
  outermost comparison of the main pass started an empty one, and the structural work (additive and multiplicative
  canonicalization of every re-simplified base) dominated. With the memo retained across the sessions of the operation:
  structural visits 1,138,284 -> 144,878 at n = 32 and 17,960,428 -> 1,146,846 at n = 64 (still about n^3: each canonicalization
  walks its whole operand), transformation visits 6,004 -> 564 at n = 16 and 8,388 at n = 64 (quadratic), 1,077 ms -> 65 ms at
  n = 64, which the default structural budget (10,000,000) no longer refuses. `pow` unchanged (153 visits, no comparer
  simplification). Pinned by `S5A15_HornerChain_GrowsPolynomially`.

## 11. Documented limitations, reproduced

| Id | Limitation | Source | Reproduced |
|---|---|---|---|
| L-1 | `0 - c` (and `0 - -1`) left for the next pass | P10, P17 | yes, 6,726 cases |
| L-2 | `DivisionOfDivision(e, t, 1/u)` leaves `/ 1` | P17 | yes, 836 cases; `x / (1/x)` → `x^2 / 1` |
| L-3 | free parameters of one type tie in the S4 order (`x + y` ≠ `y + x` for the comparer; wandering forms of `g`) | S4, P10 | yes; bound parameters are identified |
| L-4 | symbolic identities do not preserve NaN, infinities, signed zeros; `0^x -> 0` needs `x > 0`; logarithm domains | S3, P11–P13 | yes (S3 matrix) |
| L-5 | `g` (equal coefficients) exponential | P16 | yes, §7 |
| L-6 | first-sorted-last opaque, known-safe and call chains (`z`, `e`, `k`, `n`) exponential | P15, P16 | yes, §7 |
| L-7 | user-code chains (`o`, `r`): user calls grow and must be re-run by the contract | P15 | yes, §7 |
| L-8 | decimal factoring and reordering gated (decimal arithmetic may throw) | ETC R1b | yes (`2m*x - 2m*x` kept) |
| L-9 | lifted nullable and custom operators atomic | S3, R1b | yes |
| L-10 | the comparer identifies exact numeric constants across types (`1` and `1.0`) | S1 (documented model) | yes; no rule observed to misuse it |

Not reproduced any more: **`F` (custom multiplication operator, P16: 848,334 visits at n = 10)** is linear (79 visits at n = 20):
since ETC R1b a custom operator is user code, so its terms are not inert and the rules around them are gated.

## 12. Generated campaigns (Phase I)

- **Termination, convergence, differential, effects:** see §5, §6, §9, §8.
- **Canonicalization** (`canon`, 48 groups):

| Term set | Combination | Free: distinct forms / comparer-unequal pairs / unstable | Lambda-bound: distinct / unequal / unstable |
|---|---|---|---|
| `{x, y, z}` | sum | 6 / 48 / 0 | 1 / 0 / 0 |
| `{x, y, z}` | product | 6 / 48 / 0 | 1 / 0 / 0 |
| `{x, y, z}` | signed | 2 / 0 / 0 | 1 / 0 / 0 |
| `{2x, 3y, z}` | sum | 1 / 0 / 0 | 1 / 0 / 0 |
| `{2x, 3y, z}` | product | 6 / 48 / 0 | 1 / 0 / 0 |
| `{2x, 3y, z}` | signed | 1 / 0 / 0 | 1 / 0 / 0 |
| `{x, sin(y), 2}` | sum | 1 / 0 / 0 | 1 / 0 / 0 |
| `{x, sin(y), 2}` | product | 1 / 0 / 0 | 1 / 0 / 0 |
| `{x, sin(y), 2}` | signed | 1 / 0 / 0 | 1 / 0 / 0 |
| `{x, carrier(opaque), 3y}` | sum | 1 / 0 / 0 | 1 / 0 / 0 |
| `{x, carrier(opaque), 3y}` | product | 2 / 36 / 0 | 1 / 0 / 0 |
| `{x, carrier(opaque), 3y}` | signed | 1 / 0 / 0 | 1 / 0 / 0 |
| `{x*y, y*z, 2}` | sum | 4 / 0 / 12 | 3 / 0 / 12 |
| `{x*y, y*z, 2}` | product | 2 / 36 / 0 | 1 / 0 / 0 |
| `{x*y, y*z, 2}` | signed | 2 / 32 / 12 | 2 / 32 / 12 |
| `{x, custom(2,y), z}` | sum | 6 / 48 / 0 | 6 / 47 / 1 |
| `{x, custom(2,y), z}` | product | 6 / 48 / 0 | 6 / 47 / 1 |
| `{x, custom(2,y), z}` | signed | 6 / 48 / 0 | 6 / 47 / 1 |
| `{x, Next(), y}` | sum | 6 / 48 / 0 | 6 / 47 / 1 |
| `{x, Next(), y}` | product | 6 / 48 / 0 | 6 / 47 / 1 |
| `{x, Next(), y}` | signed | 6 / 48 / 0 | 6 / 47 / 1 |
| `{x, x, y}` | sum | 3 / 0 / 8 | 2 / 0 / 8 |
| `{x, x, y}` | product | 4 / 36 / 2 | 2 / 32 / 0 |
| `{x, x, y}` | signed | 1 / 0 / 0 | 1 / 0 / 0 |

Twelve trees per line (3! orders × left/right association). A unique canonical form is promised for inert terms bound by a lambda;
free parameters of one type tie (L-3); terms with a custom operator or an effect are pinned in source order on purpose (C-06), so
their six orders stay distinct. The remaining non-unique bound cases are the single-pass incompleteness of S5A-09 (`{x, x, y}`,
`{x*y, y*z, 2}`: the factoring `(x + z) * y` happens for some orders only, and its coefficient `z + x` is not reordered) — the
comparer then reports 32 unequal pairs between equivalent forms. No hash mismatch anywhere.

- **Comparer invariants** (`comparer`, 28,000 seeds over 7 types, 1.46 million checks): reflexivity on rebuilt copies
  (26,884 checks), symmetry (537,585), transitivity (589,056), hash consistency (299,079), alpha-equivalent lambdas (26,879):
  **0 violations**. Raw structural equality against the S4 canonical order (antisymmetry, raw equality implies a tie, transitivity
  of the strict order and of ties): 0 violations. Curated scope cases (nested alpha-equivalence, shadowing, captured variables
  with equal names, block variables): all as expected. `Equals(a, b) == Equals(S(a), S(b))`: 11 violations, all instances of S5A-05 (`(t + u) - (t + u)` not cancelled by one pass). Five
  comparisons threw: S5A-01 (1) and S5A-04 (4, constant zero divisors).

## 13. Benchmarks (Phase K)

The audit changes no production code, so there is no candidate to compare against master and no A/A pair to report; the
complexity table of §7 gives absolute untraced Release timings (medians of up to 15 runs after a warm-up of at least 1 s, one
process per point, nothing else running) and the instrumented counters (separate runs). Timings are only comparable within this
campaign; the counters are machine-independent. The fixing stages must use the A/A-controlled campaigns of P9–P17
(`tests/ExpressionTracingBench`, paired variants, rotating order) for their performance gates.

## 14. Priorities (Phase L)

| Id | Severity | Minimal expression | Cause | Status | Proposed fix |
|---|---|---|---|---|---|
| S5A-03 | Critical (C-06), narrow | `1m / (b / 3m)` at `b = 1e-28` | `IsOrdinaryFieldDivision` accepts `decimal` | **fixed (P18b)** | field reassociation for `float`/`double` only |
| S5A-13 | Critical (C-06), narrow | `(a / 0.5m) - (a / 0.5m)` at `a = decimal.MaxValue` | `IsSafeDivisor` accepts any non-zero decimal | **fixed (P18b)** | safe only when the magnitude is at least 1 |
| S5A-12 | High | `x0 - x1 - ... - x15` refused by default | free-parameter tie + `0 - t` re-entries | confirmed | stable free-parameter order or canonical fixed point |
| S5A-14 | High | `2 + Abs(2 + Abs(... Sin(x)))`, 0.31 s at 16 levels | `AdditionOfEqualsElements` probes constants other than 1 through the comparer | **fixed** (P19d, 2026-11-10) | P19d, before C3 |
| S5A-15 | High | Horner chain `((x*y + 1)*y + 1)...`, n^3.8 | power-combination probe simplifies its bases | **fixed** (P19d, 2026-11-10) | P19d, before C3 |
| S5A-01 | High | `x^y + z^2` | null dereference in `AdditionOfCos2andSin2Number` | confirmed | pattern-match the exponents |
| S5A-02 | High | `double.Round(x, 2)` | arity-based method lookup in `TransformCall` | confirmed | keep the call's own method |
| S5A-08 | High | `negsum` n = 24 refused by default | negation rules re-prepare prepared operands | confirmed | extend the P16 prepared-operand path |
| L-5, L-6 | High (documented) | `3*x0 - 3*x1 - ... - 3*x15` refused by default | factoring probes on non-canonical chains | reproduced | P19 |
| S5A-04 | Medium | `i > 0 ? 1/0 : 2` | eager folding and zero-divisor check | confirmed | decision needed |
| S5A-05 | Medium | `(x + y) - (x + y)` | priorities, unfinalized rule results | confirmed | local finalization |
| S5A-06 | Medium | `(x - y) - z` (bound) | P10 orientation conflict | confirmed (documented) | canonical form as fixed point |
| S5A-07 | Medium | `t + 0`, `t ^ 1` | `TransformCore` on a prepared operand | confirmed | return the operand |
| S5A-09 | Medium | `x + (x + y)` | groups not combined | confirmed | combine inert groups |
| S5A-10 | Low | `-(-x)` | missing rule | confirmed | add the rule |
| S5A-11 | Low | — | reflection order | latent | pin the plan order |

Not defects: the S3 latitude (§9), the documented domain preconditions, the observable-effect policy (0 violations), the
comparer invariants (0 violations), the exceptions thrown for constant zero divisors in always-evaluated nodes.

## 15. Correction plan (Phase M) and pull requests (Livrable D)

Proposed order, each PR independent unless stated:

1. **P18a — correctness crashes (S5A-01, S5A-02).** Objective: no exception on valid ordinary trees. Changes: pattern-matched
   exponents in `AdditionOfCos2andSin2Number`; `TransformCall` keeps the call's method (or declines) for non-`double` parameters.
   Semantic risk: none beyond removing the exception (the identities keep their domain). Tests: flip `S5A01`/`S5A02`, add the
   positive controls of every `double.Round` overload and of sums of powers with symbolic exponents (values compiled and compared).
   Measures: R4/tracing harness A/A, no expected change. No dependency.
2. **P18b — decimal exceptions (S5A-03, S5A-13).** Objective: C-06 consistency. Changes: `IsOrdinaryFieldDivision` for `float`/
   `double` only; `IsSafeDivisor` accepts a decimal constant of magnitude at least 1 only. Risk: decimal nested divisions are no longer reassociated (documented in the migration guide, like `0m / x`).
   Tests: flip `S5A03`, decimal positive controls, the differential campaign on `decimal` (0 exception differences expected).
3. **P19a — redundant re-transformation (S5A-07).** Change: `AdditionWithZero(t, 0)` and `PowerByZeroOrOne(t, 1)` return the
   prepared operand. Risk: outputs change where the operand is not a fixed point (documented with the measured list); no user-code
   change (the operand's simplification already ran once, and P15/P16 reuse rules are untouched). Tests: flip `S5A07`; the P3/P9/
   P15/P16 suites unchanged. Measures: families `plus0`, `pow1` and the P9–P16 families, paired with A/A.
4. **P20a — local finalization of rule results (S5A-05, S5A-10, L-1, L-2).** Changes: decline the reassociations on structurally
   equal operands (or finalize them), finalize the commutation and the `/ 1` of `DivisionOfDivision` the way `BuildFactoredProduct`
   finalizes its product, fold `0 - c`, add `-(-t) -> t`. Risk: changed outputs (shorter); user calls unchanged (no new
   comparison: structural raw equality only on inert operands). Tests: flip `S5A05`/`S5A10`, convergence campaign (fixed point after
   one pass expected to rise from 77.9 %). Measures: all S5 families.
5. **P19b — negation re-entries (S5A-08).** Extend `TransformCoreWithPreparedOperands` to the negation rules with the P16 guards.
   Depends on nothing, but measure after P19a. Tests: flip `S5A08` (work bounds), P16 characterization unchanged, opaque/user-code
   invariants unchanged (P15 tests).
6. **P20b — oscillation and the `0 - t` re-entry (S5A-06, S5A-09, S5A-12).** Make the canonical additive representation a fixed point of the subtraction/negation
   rules and combine inert groups; the largest semantic and performance risk (P10 rejected the left-associated alternative for its
   3^n user-code cost): needs the P10 user-code spy and the B/p/z families as gates. Depends on P20a.
7. **P19c — `g` and the first-sorted-last chains (L-5, L-6).** Study the factoring probes of non-canonical left-associated
   differences; separate the eliminable work from the user calls the contract imposes (L-7 needs a contract change, P15).
8. **P18c — exceptions of unevaluated branches (S5A-04).** Needs a contract decision first.
8b. **P19d — comparer probes on prepared operands (S5A-14, S5A-15), priority: before Architecture C stage C3. Done 2026-11-10 (operation-scoped comparison memo, see §10).** C3 adds
   reassociation and factoring under a policy, which would run the same probes on more shapes. Changes: compare constants and
   power bases with prepared operands structurally, or reuse the prepared forms (P16). Tests and measures: families `constabs`,
   `nanabs`, `horner`, `pow`, and the P9–P16 families with A/A. Afterwards, re-measure `nanpow`
   (`NaN + (NaN + (… x0) ^ 2) ^ 2`): its uncounted `IsInertArithmetic` walk is quadratic (`2n² + n` nodes) under a non-default
   policy; a per-operation memo was measured on C2b (2026-11-09) and withdrawn because these probes dominate (12.7 million
   structural visits at n = 64 under every policy) and the memo cost +5 % on small trees
   (`docs/expressions/SimplificationPolicies.md` §9.7).
9. **P22 — domains and determinism (S5A-11, L-8–L-10).** Pin the plan order; record the decimal and nullable policies and the
   cross-type comparer model in the S3 contract.

Justification of the order: P18a/P18b remove wrong results and crashes with one-line, low-risk changes; P19a is a one-line work
reduction that also removes a hidden extra pass; P20a then makes single-pass results stable on the frequent shapes, which reduces
the comparer's false negatives the factoring rules rely on; P19b/P20b/P19c are the larger structural changes and need the gates of
P9–P17.

## 16. Reproduction

**Environment:** Windows PowerShell 5.1

```powershell
.\tests\ExpressionSimplifierAudit\run-audit.ps1 -Campaigns terminate,inventory,converge,canon,comparer,diff,effects,s3 -Seeds 4000 -Nested 4000
.\tests\ExpressionSimplifierAudit\run-audit.ps1 -Campaigns diff,effects -Seeds 8000
.\tests\ExpressionSimplifierAudit\run-audit.ps1 -Campaigns complexity -Unlimited -StopAfterSeconds 10 -MeasureTimeoutSeconds 90
dotnet test .\UtilsTest\UtilsTest.Unit.csproj --configuration Release --filter "TestCategory=S5-Audit"
```

## 17. Validation

On the audit branch (no production code changed; `master` differs only by the 18 new `S5-Audit` cases):

| Suite | Result |
|---|---|
| `dotnet build Utils.sln -c Release` | 0 errors |
| `UtilsTest.Unit` (Release) | 14,484 passed, 1 not executed (pre-existing), 0 failed (includes the 18 `S5-Audit` cases) |
| `UtilsTest.Functional` (Release) | 383 passed |
| `UtilsTest.Security` (Release) | 315 passed, 3 not executed (pre-existing platform-gated tests) |
| ETC Debug job (`UtilsTest.Mathematics.Expressions.Coverage`, Debug) | 272 passed, 1 not executed (as in the ETC closure audit) |
| `UtilsTest.Mathematics.Expressions` (Debug, S5 and P9–P17 suites) | 1,147 passed, 1 not executed |

The `S5-Audit` category runs in about 1 s. No exclusion or inconclusive test was added.
