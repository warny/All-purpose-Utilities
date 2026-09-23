# Utils.NumberToString — Current backlog

Re-audited on 2026-08-25 after NTS-05 was closed. Historical details remain in
the archived audit files; this file is the active source of truth.

See `docs/releasing/TodoAudit-2026-08-16.md` for the repository-wide
classification.

## Open items

- **NTS-08 — linguistic ordinal and ClockTime coverage audit.** The engine work needed for
  hour-conditioned clock rules is complete, but production language coverage must not be enabled
  without grammatical sources and compound/scale tests. Ordinals remain deferred for BG, CS, DA,
  FA, NO, RO, SK, SV, SW, TR, UK, and ZU; HR and HU require productive replacements for their
  partial configurations. Idiomatic ClockTime is now configured for DE (with de-CH inheritance), FR (with separate FR-be and FR-ch children), EN (with EN-GB inheritance), Catalan, Valencian, Indonesian, and Malay. Every other natural-language configuration remains explicitly unsupported until its regional convention, grammatical variants, compounds, and scale behavior
  are verified. SW/ZU additionally require a documented noun-class policy. AR and HE require a
  sourced decision for compounds above their existing explicitly configured ranges.

NTS-01 through NTS-05 are closed:

- NTS-01 — XSD validation: `DONE-2026-08-21.md`.
- NTS-02 — initialization isolation: `DONE-2026-08-21.md`.
- NTS-03 — single composite finalization: `DONE-2026-08-21(1).md`.
- NTS-04 — constituent-local `ForcedVariants`: `DONE-2026-08-24(1).md`
  (supersedes the deferral recorded in `DONE-2026-08-21(1).md`).
- NTS-05 — extensible lexical form selection + Spanish attributive apocope:
  `DONE-2026-08-25(1).md` (resolves the Spanish deferral recorded in
  `DONE-2026-08-24(1).md`), with pre-merge review fixes (selector-specific
  XML configuration, per-type reflection activation caching, and the
  `TimeUnitForms`/`TimeUnitFormSelectors` effective-state correction) in
  `DONE-2026-08-25(2).md`, and a second review round (selector activation
  caching reuses `Utils.Collections.CachedLoader` instead of a bespoke
  cache) in `DONE-2026-08-25(3).md`.

Full multi-form plural systems (Russian/Slavic count-dependent noun forms,
Arabic dual/paucal/plural categories) are deliberately out of scope — the
`ILexicalFormSelector` architecture supports them without redesign, but no
production language uses more than two forms yet. See the "Deliberately
deferred" section of `DONE-2026-08-25(1).md`. This is design headroom, not an
open backlog item.

New findings should be appended here as they are identified, and archived to
a dated `DONE-*.md` file once resolved, per the repository's `AGENTS.md`
TODO/DONE convention.


### NTS-08 completed slice (2026-09-23)

- Split Belgian and Swiss French into `baseOn="FR"` children; Belgian keeps `quatre-vingts` while Swiss keeps `huitante`.
- Split Valencian into a `baseOn="CA"` child with its own clock convention.
- Split Malay into a `baseOn="ID"` child, preserving the Malay `lapan` stem and a distinct `pukul` clock convention.
- Added productive Indonesian/Malay `ke-` ordinals with a small plugin only for suppletive `pertama`.
- Added English and Catalan ClockTime rules. Remaining languages listed above are still open and must not be marked supported without the documented grammatical audit.
