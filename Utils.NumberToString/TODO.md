# Utils.NumberToString — Current backlog

Re-audited on 2026-08-21 after NTS-03 was merged in PR #549. Historical
details remain in the archived audit files; this file is the active source of
truth.

See `docs/releasing/TodoAudit-2026-08-16.md` for the repository-wide
classification.

## P3 — deferred design limitation, non-blocking for 2.0

### NTS-04 — Units/connectors/month forms are not fully variant-aware

The variant system targets numeric morphology. Surrounding phrase constituents
such as time-unit names, fraction/date-time connectors, culture-provided month
names, and caller-provided currency text are not independently variant-aware.

A final post-NTS-03 audit found no incorrect output for a currently supported
built-in language and public conversion API. Existing built-in requirements
are covered by singular/plural forms, `Count1Form`, ordinal variants, first-day
overrides, numeric variant rules, language finalization, or caller-provided
`CurrencyDefinition` values.

**Decision:** keep NTS-04 deferred and do not treat it as a 2.0 blocker. Extend
only the affected constituent when a red test for a supported built-in language
demonstrates that the existing mechanisms are insufficient. Do not apply
numeric `VariantRules` to an assembled phrase.

The complete evidence matrix and architectural guidance are recorded in
`DONE-2026-08-21(1).md`.
