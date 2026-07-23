# Utils.NumberToString — Quality and correctness audit

Static review of `Utils.NumberToString`, including integer, decimal, floating-point, currency, fraction, ordinal, date/time conversion, XML configuration loading, variants, replacements, triggers and language-specific extensions.

## Previous implementation log

The earlier backlog items 1–46 have been implemented, documented, rejected intentionally, or recorded as architectural limitations. Completed work includes:

- decimal, currency, significant-digit, ordinal, fraction, multiplicative and temporal overloads;
- additional languages and language-specific ordinal plugins;
- compiled trigger regexes, variant validation and pre-sorted variant rules;
- thread-safe configuration/specifics dictionaries;
- recursive BCP-47 fallback and negative-year formatting;
- configurable group, intra-group and scale connectors;
- broader tests for languages, decimals, dates, fractions and variants;
- targeted linguistic fixes for Spanish, Italian, Finnish, Korean, Japanese, Romanian and other configurations.

Known linguistic limitations retained from the previous backlog:

- full Zulu ordinal support requires noun-class-aware language logic;
- Arabic ordinal gender polarity and dual forms require a dedicated plugin;
- Greek cardinal and ordinal gender defaults cannot currently differ within one shared variant dimension;
- counted-noun agreement for Slavic languages is outside the current number-only model.

The findings below are newly identified and remain open.

## Critical and high-priority findings

### 47. ✅ `decimal.MinValue` overflows in decimal and currency conversion

Both decimal conversion and currency conversion compute an absolute value with unary negation:

```csharp
if (number < 0) number = -number;
decimal absAmount = isNegative ? -amount : amount;
```

Negating `decimal.MinValue` throws `OverflowException`, so the public API cannot convert the full declared `decimal` domain.

**Fix:** avoid taking a positive decimal absolute value when the minimum value cannot be represented. Split the integral/fractional components while preserving the sign, or convert through an unsigned magnitude representation. Add explicit `decimal.MinValue`, `decimal.MaxValue` and values adjacent to both endpoints.

**Priority: P1 numeric correctness.**

### 48. ✅ Currency conversion is restricted to `long` without declaring that limit

`ConvertCurrency` casts the truncated unit amount to `long`. Valid decimal values outside `long` range therefore throw even though the API accepts `decimal` and the cardinal converter supports `BigInteger`.

**Fix:** use `BigInteger` for units and subunits throughout currency conversion. Validate the converter's configured `MaxNumber` before formatting and document any deliberate currency-specific range.

**Priority: P1 API correctness.**

### 49. ✅ `CurrencyDefinition.SubunitDigits` is unvalidated and uses floating-point arithmetic

The subunit factor is calculated through `Math.Pow(10, SubunitDigits)`, converted to `long`, and the decimal fraction is converted to `double` before rounding. Negative values can produce a zero factor and division by zero; large values overflow or produce invalid factors; conversion through `double` can round monetary values incorrectly.

**Fix:** validate `SubunitDigits` against an explicit supported range and calculate the factor exactly with decimal/`BigInteger` arithmetic. Round using `decimal.Round` with a documented midpoint policy. Never route currency through `double`.

**Priority: P1 financial correctness.**

### 50. ✅ Minimum signed values overflow in ordinal, year and duration conversion

- `ConvertOrdinal(long)` uses `Math.Abs(long)`, which throws for `long.MinValue`.
- `ConvertYear(int)` uses `Math.Abs(int)`, which throws for `int.MinValue`.
- `Convert(TimeSpan)` negates negative durations, which throws for `TimeSpan.MinValue`.

**Fix:** derive magnitudes through wider or unsigned representations (`BigInteger`, `ulong`, ticks handled without unary negation). Ensure all signed public input domains have deterministic behavior.

**Priority: P1 numeric correctness.**

### 51. ✅ The `BigInteger` ordinal API only supports the `long` range

`ConvertOrdinal(BigInteger)` immediately performs `checked((long)number)`. The overload therefore suggests arbitrary-precision support while rejecting values outside `Int64`, unlike cardinal conversion.

**Fix:** either implement the ordinal pipeline on `BigInteger`, or replace/deprecate the overload and document the actual range explicitly. Do not expose a wider type solely to narrow it at entry.

**Priority: P1 API contract.**

### 52. ✅ Large finite `double`/`float` values silently lose their fractional part

When a finite floating-point value cannot be parsed as `decimal`, conversion falls back to:

```csharp
Convert(new BigInteger(Math.Truncate(number)), variants)
```

This silently discards the complete fractional part. The same method can therefore preserve decimals for smaller values but change semantics solely because the magnitude crossed the decimal range.

**Fix:** define an explicit contract. Prefer throwing `ArgumentOutOfRangeException` when an exact supported decimal representation is unavailable, or provide a separate scientific/significant-digit conversion path. Silent truncation should not be the fallback.

**Priority: P1 numeric correctness.**

### 53. ✅ Configurable regular expressions have no execution timeout

Trigger patterns are compiled with `new Regex(pattern, RegexOptions.Compiled)` and later applied to generated text without a timeout. Programmatic or externally registered configurations can provide catastrophic-backtracking patterns.

**Risk:** CPU denial of service during converter construction or conversion.

**Fix:** compile with a finite, configurable timeout and convert `RegexMatchTimeoutException` into a configuration/runtime diagnostic that identifies the language and pattern. Consider `RegexOptions.NonBacktracking` for compatible patterns.

**Priority: P1 robustness/security.**

### 54. Core grouping configuration is not structurally validated

The constructor checks that `Groups` and `Scale` are non-null but does not ensure that:

- the group size is positive and within the supported integer arithmetic range;
- groups are non-empty and contiguous;
- every required digit exists;
- `Groups.Keys.Max()` is safe;
- `10^groupSize` fits the subsequent `long` remainder cast;
- each `DigitType.BuildString` has a coherent placeholder contract.

Malformed programmatic/XML configuration can therefore fail later with `DivideByZeroException`, `OverflowException`, `InvalidOperationException` or `KeyNotFoundException`, sometimes after partial registration.

**Fix:** add one comprehensive immutable configuration-validation phase before creating or registering a converter. Reject unsupported group sizes explicitly and report the exact language/group/digit path.

**Priority: P1 configuration safety.**

## Medium-priority findings

### 55. ✅ Fraction conversion accepts a zero or negative denominator without normalization

`ConvertFraction` delegates directly to `BuildFractionText`; denominator zero becomes spoken text such as “one / zero” rather than an error. Negative denominators can place the sign in the denominator text instead of normalizing it onto the numerator. Fractions are not reduced, making named-form selection and pluralization dependent on the caller's unreduced representation.

**Fix:** reject a zero denominator, normalize the denominator to positive, and decide/document whether fractions are reduced by `GCD` before formatting. Add tests for `1/0`, `1/-2`, `-1/-2`, and reducible fractions.

**Priority: P2 semantic correctness.**

### 56. Configuration registration is non-transactional and duplicate cultures are silently ignored

`RegisterConfigurations` reads and registers each document sequentially. If a later document fails, earlier cultures remain globally registered. `TryAdd` silently keeps the first culture, so typos, conflicting versions or intended overrides are not observable.

**Fix:** parse and validate the entire batch into temporary structures, detect all collisions, then commit atomically. Expose an explicit duplicate policy (`Reject`, `KeepExisting`, `Replace`) rather than always using first-wins behavior.

**Priority: P2 lifecycle/configuration.**

### 57. `baseOn` inheritance is global, order-dependent and has no cycle detection

Resolved language definitions are stored in a process-wide cache while documents are parsed. A child can therefore inherit from whichever definition of a culture was cached first in an earlier call. Recursive `baseOn` chains do not maintain a visited set, so cycles can recurse until stack overflow.

**Fix:** resolve inheritance inside an isolated configuration graph with explicit document/batch scope, detect cycles with a visiting set, and commit resolved definitions only after the graph validates. Cross-batch inheritance should require an explicit registry/version policy.

**Priority: P2 determinism and robustness.**

### 58. Runtime variants are permissive while configuration variants are strict

Configuration references are validated, but `BuildVariantQuery` silently accepts malformed strings, unknown dimension names and undeclared values. These entries either do nothing or prevent expected rules from matching, making caller mistakes difficult to diagnose.

**Fix:** provide a strict default that validates every `dimension=value` pair against `_dimensionIndex` and the dimension's declared values. If backward compatibility requires permissive behavior, expose it as an explicit option and offer a typed variant-query API.

**Priority: P2 API ergonomics.**

### 59. Trigger and ordinal tie-breaking depends on declaration order

For equally specific matching trigger forms or ordinal variants, the first declared item wins. Overlapping constraints of equal specificity are not rejected, so configuration reordering can silently change output.

**Fix:** detect ambiguous equal-specificity matches during validation unless an explicit priority is supplied. Add deterministic precedence metadata if intentional overlap is needed.

**Priority: P2 determinism.**

### 60. `RegisterLanguageSpecifics` stores shared mutable instances globally

A single registered `INumberToStringLanguageSpecifics` instance may be reused by many immutable converters and concurrent calls. The API does not require implementations to be stateless/thread-safe and allows silent replacement under the same key.

**Fix:** register factories or immutable descriptors instead of instances, validate non-empty names, define duplicate behavior, and document/thread-test the required lifecycle.

**Priority: P2 concurrency/design.**

### 61. Unknown cultures silently fall back to English

After recursively stripping BCP-47 subtags, `GetConverter` returns the English converter for any unresolved culture. A spelling error can therefore produce valid but wrong-language business text without any diagnostic.

**Fix:** make fallback policy explicit. Prefer a throwing `GetRequiredConverter`, a `TryGetConverter`, and an opt-in fallback converter/culture. Preserve the current behavior only as a clearly named compatibility path.

**Priority: P2 API correctness.**

## Duplications of intent to reduce

- Absolute-value/sign handling is independently implemented for decimal, currency, ordinal, year, duration and rational conversion. Centralize signed-magnitude extraction per numeric family.
- Decimal and currency rounding use different arithmetic and midpoint paths. Centralize exact decimal decomposition and rounding.
- Variant matching logic is repeated across variant rules, ordinal variants and trigger forms. Build one validated rule-selection component with explicit ambiguity handling.
- Configuration validation is split between XML deserialization, `ReadConverter`, constructor checks and variant-reference validation. Produce one complete validated configuration model before constructing a converter.
- Global registration, inheritance resolution and cache mutation are intertwined. Separate parsing, graph resolution, validation and atomic registry commit.

## Required tests

- Full signed endpoints: `decimal.MinValue`, `long.MinValue`, `int.MinValue`, `TimeSpan.MinValue`.
- Currency values beyond `long`, invalid `SubunitDigits`, exact carry behavior, and midpoint rounding without `double`.
- Finite `double`/`float` values just inside and outside decimal range, asserting no silent fractional truncation.
- Regex catastrophic-backtracking patterns with enforced timeout.
- Empty/sparse/non-contiguous groups, zero or oversized group size, missing digits and invalid scale definitions.
- Zero/negative/reducible denominators.
- Atomic multi-document registration rollback and every duplicate-culture policy.
- Direct and indirect `baseOn` cycles, same culture defined in multiple batches, and parallel registration.
- Malformed, unknown and duplicate runtime variants plus ambiguous equal-specificity rules.
- Unknown-culture behavior under strict and explicit fallback policies.

## Recommended order

| Priority | Action |
|---|---|
| P1 | Correct signed-minimum handling across all numeric APIs |
| P1 | Rework currency around exact decimal/`BigInteger` arithmetic and validate `SubunitDigits` |
| P1 | Remove silent floating-point fractional truncation |
| P1 | Add regex timeouts and comprehensive configuration validation |
| P1 | Align or narrow the `BigInteger` ordinal contract |
| P2 | Normalize and validate fractions |
| P2 | Make registration transactional and inheritance cycle-safe |
| P2 | Make runtime variants and tie-breaking deterministic |
| P2 | Define language-specific instance lifecycle and culture fallback policy |

## Deployment warning

Until items 47–54 are fixed, avoid using this package for unbounded decimals, financial values with custom subunit precision, signed minimum values, untrusted regex/configuration input, or floating-point values outside decimal range. Current behavior can throw unexpectedly, silently truncate fractional data, apply imprecise monetary rounding, or consume unbounded CPU on a pathological regex.