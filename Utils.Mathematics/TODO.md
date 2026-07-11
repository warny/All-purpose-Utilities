# Utils.Mathematics — Quality and numerical-correctness audit (2026-07-11)

Static review of `Utils.Mathematics`, covering statistics, numerical methods, polynomials, Fourier transforms, linear algebra, symbolic expressions, and geometric transformations. Findings are ordered by severity. No production code is changed by this commit.

## Critical and high-priority findings

### 1. `Statistics.Median<T>` returns the upper middle value for even-length sequences

The documentation promises the lower of the two middle values, but the implementation returns `sorted[sorted.Length / 2]`. For `[1, 3, 5, 7]`, this returns `5` instead of `3`.

The existing test named `Median_EvenLength_ReturnsLowerMiddle` expects `5`, so it locks in the incorrect behavior while claiming to test the opposite contract.

**Fix:** use `sorted[(sorted.Length - 1) / 2]`, or redefine the API explicitly as upper median. Correct the test name and expected value.

**Priority: P1 statistical correctness.**

### 2. `MatrixTransformations.Skew<T>` has incompatible dimension and index formulas

The inferred dimension formula does not match the number of angles consumed by the nested loops. In addition, the column-skipping expression uses `y >= x ? y : y + 1`, which fails to skip the diagonal for the first row and writes values into the wrong columns.

**Risk:** most input lengths are rejected incorrectly, while accepted lengths can produce an invalid homogeneous shear matrix.

**Fix:** redefine the exact mathematical contract first: number of free shear coefficients, homogeneous dimension, angle ordering, and diagonal/translation behavior. Then implement from that mapping and add hand-calculated tests.

**Priority: P1 functional correctness.**

### 3. Approximate polynomial equality violates the hash-code contract

`Polynomial<T>.Equals` considers coefficients equal when their absolute difference is at most `Epsilon`. `GetHashCode`, however, hashes the exact degree and exact first coefficient. Two polynomials can therefore compare equal while returning different hash codes.

**Risk:** `Dictionary`, `HashSet`, caches, and LINQ set operations behave incorrectly for equal polynomial instances.

**Fix:** either make object equality exact and expose a separate approximate comparer, or quantize every coefficient consistently for both equality and hashing. A separate tolerance-aware comparer is strongly preferred.

**Priority: P1 .NET contract correctness.**

### 4. One fixed absolute polynomial epsilon is invalid across generic floating-point types and scales

`Epsilon` is created from `1e-10` for every `T`. For low-precision types such as `Half`, it can collapse to zero or below useful resolution; for `decimal`, `double`, and very large/small coefficients, a fixed absolute tolerance has unrelated numerical meaning.

The same value controls constructor trimming, degree, equality, zero-term formatting, and Newton convergence defaults, coupling four different policies.

**Risk:** degree and equality change unexpectedly with scalar type and coefficient scale. Legitimate small coefficients can be deleted, while large-value comparisons become unrealistically strict.

**Fix:** use exact zero for canonical polynomial storage unless the caller explicitly requests normalization. Separate configurable absolute/relative comparison tolerance and root-finding tolerance.

**Priority: P1 generic numerical correctness.**

### 5. `Polynomial.FindRoot` accepts invalid iteration and tolerance parameters

Negative or zero `maxIterations` silently returns `null`. A negative or non-finite tolerance prevents meaningful convergence checks. The method also compares the derivative exactly with zero, which is numerically fragile near stationary points.

**Fix:** require `maxIterations > 0`, finite positive tolerance, and finite initial guess. Use a derivative threshold based on an explicit numerical policy and reject/propagate non-finite iterates deterministically.

**Priority: P1 root-finding correctness.**

### 6. Matrix solve/determinant singularity tests use exact floating-point zero

Gaussian elimination and determinant computation treat a pivot as singular only when it equals `T.Zero`. Nearly singular matrices therefore proceed through divisions by extremely small pivots, producing huge errors, infinities, or NaNs while still returning a result.

**Fix:** introduce scale-aware pivot tolerances, expose decomposition status/conditioning diagnostics, and keep an exact path only for types/uses where exact comparison is intended.

**Priority: P1 linear-algebra correctness.**

### 7. Matrix structural flags also rely on exact equality

`IsTriangular`, `IsDiagonal`, `IsIdentity`, and `IsNormalSpace` compare floating-point values exactly to zero or one. Results from normal arithmetic/decomposition that differ by rounding noise are classified as structurally false.

**Fix:** distinguish exact structural predicates from tolerance-aware predicates. Do not silently choose one global tolerance; expose explicit numerical options/comparers.

**Priority: P1 API semantics.**

### 8. Simpson integration can overflow while normalizing an odd step count

When `steps == int.MaxValue`, `steps++` overflows to `int.MinValue`. Validation occurs before that increment, so the now-invalid negative value continues into numeric conversion and loop logic.

The method also does not reject a null function or non-finite bounds/results despite documenting a finite integrand.

**Fix:** reject `int.MaxValue` when odd or normalize through checked arithmetic. Add null guards and define the policy for NaN/infinite bounds and function values.

**Priority: P1 numerical-method robustness.**

### 9. Lagrange interpolation validates duplicate abscissas only during partial evaluation

Duplicate `x` values are detected inside the nested basis loop after result construction has begun. Non-finite values, especially NaN, bypass `denom == T.Zero` and propagate invalid output rather than producing the documented distinct-point error.

**Fix:** validate all points first, including finiteness and uniqueness under a clearly defined exact/tolerance policy, then evaluate. For repeated evaluations, expose a precomputed interpolation object or barycentric form.

**Priority: P1 correctness and diagnostics.**

## Medium-priority findings

### 10. FFT null and zero-length contracts are implicit

`Transform` and `InverseTransform` dereference `array` without a null guard. Zero-length behavior depends entirely on the external `MathEx.IsPowerOfTwo(0)` implementation rather than an explicit FFT contract.

**Fix:** reject null explicitly and define whether length zero is invalid or a valid no-op. Add tests for lengths 0, 1, 2, and non-powers of two.

**Priority: P2 API quality.**

### 11. Recursive FFT allocates at every recursion level

`Separate` allocates a new half-size array for every recursive call. Across the transform this creates substantial allocation pressure and repeated copying despite the API being described as in-place.

**Fix:** use iterative bit-reversal plus butterfly stages, or rent/reuse a single workspace. Clarify that the current algorithm mutates in place but is not allocation-free.

**Priority: P2 performance.**

### 12. Correlation performs redundant passes and materialization

`Correlation` materializes both sequences, then separately computes covariance and two standard deviations, each re-enumerating arrays and independently calculating means.

**Fix:** compute covariance and both variances in one numerically stable online pass, while validating equal length and non-finite input consistently.

**Priority: P2 performance and consistency.**

### 13. Statistical algorithms have no explicit NaN/infinity policy

Mean, variance, covariance, correlation, and median accept non-finite values. Depending on position and sort behavior, results become NaN or otherwise implementation-dependent without a diagnostic.

**Fix:** document propagation semantics or reject non-finite samples through an option. Apply one consistent policy across all statistical operations.

**Priority: P2 statistical contract.**

### 14. Matrix formatting rounds values instead of formatting them

`Matrix.ToString(format, provider)` calls `T.Round` and then appends the value without forwarding the requested format string to the scalar formatter. The format argument currently changes only row separators (`S`, `C`, `SC`) and cannot control numeric formatting as expected from `IFormattable`.

**Fix:** separate layout format from scalar format, or define a documented composite format. Use `IFormattable.ToString`/`TryFormat` rather than modifying numeric values before display.

**Priority: P2 API correctness.**

### 15. Matrix equality unnecessarily depends on cached hash equality

`Matrix.Equals(Matrix<T>)` first requires equal hash codes before comparing elements. Correct hash implementations guarantee equal values have equal hashes, but using a cached hash as a semantic precondition makes future tolerance-aware equality or hashing changes hazardous and adds no correctness value.

**Fix:** compare dimensions/elements directly; use hashes only as collection infrastructure or an optional optimization proven consistent with the exact equality policy.

**Priority: P2 maintainability.**

## Duplications of intent to reduce

- Zero/singularity/tolerance decisions are independently embedded in polynomials, matrix decomposition, structural predicates, root finding, and interpolation. Introduce explicit numerical-policy/comparer objects rather than scattered exact checks and constants.
- Mean, variance, covariance, standard deviation, and correlation duplicate online-statistics state. Use one accumulator that can expose all derived statistics.
- Matrix determinant, solve, inverse, QR, and eigenvalue routines should share validated decomposition primitives and pivot policy instead of implementing related elimination decisions independently.
- Polynomial canonicalization, equality, formatting, and root convergence should not share one hard-coded epsilon.
- FFT validation and workspace management should be centralized for forward/inverse transforms.

## Required tests

- Even median with 2, 4, and 6 elements, verifying the documented lower/upper/average policy.
- Hand-calculated 2D and 3D skew matrices, invalid parameter counts, and unchanged homogeneous diagonal/translation positions.
- Equal polynomial pairs within tolerance must satisfy the hash contract, or exact equality must reject them.
- Polynomial behavior for `Half`, `float`, `double`, and `decimal`, including tiny and large coefficients.
- Invalid/non-finite Newton parameters and near-zero derivatives.
- Singular and nearly singular matrices under multiple scalar types and scales.
- Exact versus tolerance-aware identity/triangular predicates.
- Simpson steps at 1, odd values, `int.MaxValue`, reversed bounds, null function, and non-finite samples.
- Lagrange duplicate/NaN/infinite abscissas and prevalidated repeated evaluation.
- FFT null, empty, one element, round-trip, and allocation benchmarks.
- Statistics with NaN/infinity and a one-pass correlation reference.
- Matrix scalar formatting under custom cultures and numeric formats.

## Recommended order

| Priority | Action |
|---|---|
| P1 | Correct median and completely redesign/test `Skew` |
| P1 | Separate exact polynomial identity from approximate comparison |
| P1 | Introduce explicit tolerance/pivot policies for numerical algorithms |
| P1 | Validate Newton, Simpson, and interpolation inputs before execution |
| P2 | Consolidate online statistics and FFT workspace handling |
| P2 | Correct matrix formatting and simplify equality |

## Deployment warning

Until items 1–9 are addressed, avoid relying on even-length median results, `Skew`, approximate polynomial equality in hashed collections, near-singular matrix solutions, or generic numerical behavior across very different floating-point types. Several methods can return mathematically incorrect or non-finite results without a clear failure signal.