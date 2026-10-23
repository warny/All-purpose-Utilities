# Utils — `IntRange<T>` with an open lower bound (2026-10-23)

Found while implementing NTS-52 (`Utils.NumberToString`, PR #658), which works around it and does not change
`Utils.Range`. Not fixed: `Utils` is referenced by every project, so the fix needs its own change and a run of every
test project.

## Open items

- **INTRANGE-01 — set difference and complement lose an open lower bound.** With
  `a = NumberToStringConverter.ParseRangeExpression("..5")` (canonical `∞-5`, `a.Contains(-3)` is true) and
  `z = ParseRangeExpression("0..")` (`0-∞`):
  - `(a - z).ToString()` is empty, expected `∞--1`;
  - `(~z).ToString()` is empty, expected `∞--1`, so `(a & ~z)` is empty too.

  `Except` / `Complement` (`Except(FullRange, this)`) are suspected in `SimpleRange.Except` when a bound is `null`.
  The intersection is correct (`(a & ParseRangeExpression("..0")).ToString()` is `∞-0`).
- **INTRANGE-02 — a negative upper bound cannot be written.** `new IntRange<long>("∞--1")` parses as `[1]`: the `-`
  of the bound is read as the interval separator. The `..` syntax of `ParseRangeExpression` translates to the same
  form, so `"..-1"` has the same result.

Both need unit tests on `IntRange<long>` (open/closed bounds on each side, negative bounds) before any change. The
NTS-52 workaround (`CurrencyNounConnectorRule.CoversNegative`, intersection with `..0`) can stay after the fix.
