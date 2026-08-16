# Utils.NumberToString — Current 2.0 backlog

Re-audited on 2026-08-16 against `master` `6bcb7aed0a0afa45b07b82f442511becc5036da4`.

The former items 47–61 in this file are all resolved. Residual work from the historical pass-2 and pass-4 audits is consolidated here so there is one active source of truth. Full history remains available in Git and the archived audit files.

See `docs/releasing/TodoAudit-2026-08-16.md` for repository-wide classification and PR order.

## P1

### NTS-01 — Configuration XML is not validated against the published XSD

`BuildConfiguration` creates an `XmlSerializer` and deserializes a `StringReader` directly. The string `"Utils/NumberConvertionConfiguration.xsd"` is used as the serializer namespace; it does not create schema validation.

**Risk:** schema-invalid or misspelled XML can reach semantic validation/deserialization behavior instead of failing with a precise line/position diagnostic.

**Fix:** validate with a securely configured schema-validating `XmlReader` before deserialization, then keep the existing semantic validation phase for cross-field rules that XSD cannot express.

**Tests:** unknown/misspelled elements and attributes, invalid restricted values, missing required structure, line/position diagnostics, XXE/DTD-disabled behavior, and every built-in resource through the validation path.

### NTS-02 — One invalid built-in configuration can poison static initialization

The static constructor still calls `InitializeConfigurations(...)` for the entire built-in locale set. Any exception escaping that path becomes a `TypeInitializationException` and makes the type unusable for the process lifetime.

**Fix:** make built-in validation a release/CI gate and separate validation/build from publication. Prefer an explicit initialization result/aggregate diagnostic or a guaranteed-safe core registry rather than allowing one optional locale to poison every locale.

**Dependency:** implement NTS-01 first so initialization failures have precise diagnostics.

## P2 — prove before refactoring

### NTS-03 — Composite phrase finalization may still be applied inconsistently

Historical item 65 identified composite methods that assemble results from public `Convert(...)` calls and later apply adjustment/finalization at phrase level. Many adjacent paths were subsequently fixed, so the old prose must not be treated as proof that the defect still exists in every conversion kind.

**Required first step:** add a deliberately non-idempotent `INumberToStringLanguageSpecifics` finalizer and a matrix covering decimal, fraction, currency, duration/time and date composition. Count and observe finalization of subparts and final phrases.

**Fix only if reproduced:** split raw typed-fragment generation from one final phrase-render stage. Do not perform a broad linguistic-pipeline refactor without a failing behavioral test.

## P3 — design feature, not generic bug

### NTS-04 — Units/connectors/month forms are not fully variant-aware

Historical item 74 describes a real extensibility limitation for languages requiring case/gender agreement outside the numeral itself. It is not a generic correctness defect for currently supported outputs by itself.

**Decision:** only extend the configuration/phrase model when a concrete supported-language test demonstrates the requirement. Keep this item as an architectural limitation rather than a mandatory 2.0 blocker.

## Closed / superseded historical findings

- Items 47–61: resolved in current code/history (signed numeric boundaries, exact currency arithmetic, regex timeout, configuration validation, transactional registration, inheritance-cycle/presence handling, variant strictness/precedence, language-specifics registration, strict culture lookup, etc.).
- Pass-2 items 62–64 and 66–73: resolved or intentionally documented.
- Pass-4 item 92: superseded by the internal presence-aware `LanguageDefinition` / `Optional<T>` model. Explicit zero/false and absent values are no longer conflated for the presence-sensitive fields.
- Pass-4 item 95: fraction-key validation is implemented; generic "all digits must exist" validation remains intentionally deferred because partial digit fixtures/configurations are supported by the current model.
- Historical Zulu/Arabic/Greek/Slavic agreement notes are linguistic-model limitations, not regressions to fix speculatively.

## Recommended implementation order

1. NTS-01 — schema validation and built-in resource validation tests.
2. NTS-02 — initialization isolation/aggregate diagnostics.
3. NTS-03 — behavioral proof first; refactor only if the matrix reproduces inconsistent finalization.
4. NTS-04 — only with a concrete language requirement.
