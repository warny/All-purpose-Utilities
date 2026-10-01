# Utils.NumberToString — Current backlog

Re-audited on 2026-10-01 after the NTS-08 regional consolidation (#608), which also opened
NTS-09. Historical details remain in
the archived audit files; this file is the active source of truth.

See `docs/releasing/TodoAudit-2026-08-16.md` for the repository-wide
classification.

## Open items

- **NTS-08 — linguistic ordinal and ClockTime coverage audit (implementation done, validation open).**
  Every natural-language configuration now has tested ordinal and ClockTime behaviour or an explicit
  deferral (`docs/NTS-08-linguistic-sources.md`, `DONE-2026-10-01(1).md`). NTS-08 is not closed:
  - **Linguistic validation remaining, tracked per capability.** A scenario only proves that a
    configuration produces the string chosen by the PR; a capability counts as validated once its
    tested wording rests on a consulted source (see `docs/NTS-08-linguistic-sources.md`). Still
    unvalidated:
    - *Ordinals added or changed by NTS-08*: SV; DA vigesimal tens (`halvtredsindstyvende` ...);
      HU; UK; FA; TR; AR 20-99, 100 and 1000; HE above ten (agreeing-cardinal policy) and the AR/HE
      compound cardinals 11-99 they rely on; VN `thứ tư`; CS and SK gender/case declension tables
      (only the compound spelling was consulted).
    - *ClockTime*: DA, NO, SV, BG, HR, HU, CS, SK, UK, PL, RU, ES, IT, PT (the deliberate direct
      reading; the consulted source covers `para`/`menos` only), GL, RO, EL, FI, HE, FA, JA, VN; ZU
      rests on a single source.
    - *Pre-existing ordinals not re-verified by the audit*: NL, PL, RU, ES, IT, PT, GL, EL, FI, HI,
      JA, KO, ZH, EU, EE, WO.
    Validated so far: NL, SW, EU, KO, ZH, HI and AR ClockTime; TR ClockTime (search summary of
    TDK usage); NO, BG, HR, RO and DA 100 (`hundrede`) ordinals; CS/SK compound ordinal spelling;
    HR cardinals. NTS-08 closes when every capability above is validated or explicitly deferred; a
    corrected string must be fixed in the `.feature` first, then in the configuration.
  - The remaining gaps below are explicit decisions, each pinned by a "does not support"
    scenario:
    - SW and ZU ordinals: an obligatory noun-class concord, no standalone form; needs a public
      `nounClass` dimension and a documented class inventory.
    - EE ClockTime: no sourced minute convention.
    - WO ClockTime: competing native and French-derived conventions, no single sourced system.

- **NTS-10 — cardinal defects found during NTS-08.** Not caused by NTS-08 and left unchanged so
  the clock/ordinal work stays reviewable:
  - IT compound cardinals are not written as one word (`venti cinque` instead of `venticinque`);
    the Italian clock therefore uses a quarter-hour step.
  - HI cardinals 21–99 are not lexicalized (`बीस एक` instead of `इक्कीस`).
  - AR cardinals from 1001 lack the `و` connector between groups.

- **NTS-12 — the declarative ordinal pipeline returns the cardinal for zero.** When no ordinal
  exception or word rule matches and no suffix is configured, `ConvertOrdinal(0)` returns the
  unchanged cardinal: PL `zero`, ES/GL `cero`, PT `zero`, EL `μηδέν`, FI `nolla`, HE `אפס`
  (pre-existing; AR and TR were fixed in the NTS-08 PR). A fail-closed engine rule for an
  unmatched declarative ordinal, with per-language zero forms where they exist, is needed.

- **NTS-11 — Hebrew cardinals from 1000.** `אחד אלף` instead of `אלף`, missing `אלפיים` and the
  construct forms (`שלושת אלפים`) and the `ו` connector between groups. The existing scenario
  pinning `1000 → אחד אלף` must be corrected together with the fix.

- **NTS-09 — `Trigger` elements are not inherited through `baseOn`.** `XmlSerializer` materializes
  an absent `<Trigger>` list as an empty list, so `MergeLanguageDefinition`'s
  `overriding.Triggers ?? inherited.Triggers` never falls back to the base. No built-in configuration
  uses triggers today; the README and XSD document the current behaviour. Fix by treating an empty
  list as absent (as `Cultures` already does) together with a regression test.

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


### NTS-08 completion pass (2026-10-01)

See `DONE-2026-10-01(1).md`.

### NTS-08 completed slice (2026-09-23)

- Split Belgian and Swiss French into `baseOn="FR"` children; Belgian keeps `quatre-vingts` while Swiss keeps `huitante`.
- Split Valencian into a `baseOn="CA"` child with its own clock convention.
- Split Malay into a `baseOn="ID"` child, preserving the Malay `lapan` stem and a distinct `pukul` clock convention.
- Added productive Indonesian/Malay ordinal plugins handling suppletive `pertama` and `ke-` formation over language-specific cardinal stems.
- Added English and Catalan ClockTime rules. Remaining languages listed above are still open and must not be marked supported without the documented grammatical audit.
