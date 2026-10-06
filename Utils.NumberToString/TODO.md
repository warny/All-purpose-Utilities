# Utils.NumberToString — Current backlog

Re-audited on 2026-10-06 after closing NTS-13 (which opened NTS-15); on 2026-10-03 after closing
NTS-10, NTS-11 and NTS-12 (which opened NTS-13 and NTS-14);
previously re-audited on 2026-10-01 after the NTS-08 regional consolidation (#608), which also
opened NTS-09. Historical details remain in
the archived audit files; this file is the active source of truth.

See `docs/releasing/TodoAudit-2026-08-16.md` for the repository-wide
classification.

## Open items

- **NTS-08 — linguistic ordinal and ClockTime coverage audit (implementation done, validation open).**
  Every natural-language configuration now has tested ordinal and ClockTime behaviour or an explicit
  deferral (`docs/NTS-08-linguistic-sources.md`, `DONE-2026-10-01(1).md`). NTS-08 is not closed:
  - **Linguistic validation remaining, tracked per capability.** A scenario only proves that a
    configuration produces the string chosen by the PR; a capability counts as validated once its
    tested wording rests on sufficient consulted evidence (see `docs/NTS-08-linguistic-sources.md`):
    an authoritative normative or academic reference read during the audit, or at least two
    independent sources. A capability backed by a single weak or isolated source (a learner blog,
    a search summary, or a sole introductory course that could not be cross-checked) stays open.
    Still unvalidated:
    - *Ordinals added or changed by NTS-08*: SV; DA vigesimal tens (`halvtredsindstyvende` ...);
      HU; UK; FA; TR; AR 20-99, 100 and 1000; HE above ten (agreeing-cardinal policy) and the AR/HE
      compound cardinals 11-99 they rely on; VN `thứ tư`; CS compound spelling (search summary of
      Naše řeč only); CS and SK gender/case declension tables.
    - *ClockTime*: DA, NO, SV, BG, HR, HU, CS, SK, UK, PL, RU, ES, IT (five-minute step since NTS-10: the
      Crusca covers `meno venti`/`meno dieci`, the `e` + minutes reading follows learner usage), PT
      (the deliberate direct reading; the consulted source covers `para`/`menos` only), GL, RO, EL, FI, HE, FA, JA, VN; and,
      backed by a single isolated source only, HI and ZH (one learner site each), TR (search summary
      of TDK usage, page not read) and ZU (sole introductory course).
    - *Pre-existing ordinals not re-verified by the audit*: NL, PL, RU, ES, PT, GL, EL, FI, HI,
      JA, KO, ZH, EU, EE, WO.
    Validated so far: NL (Taaladvies), EU (EHU grammar), KO (National Institute of Korean
    Language), SW (two sources) and AR (university textbook) ClockTime; NO, BG, HR, RO and DA 100
    (`hundrede`) ordinals; SK compound ordinal spelling; HR cardinals; IT ordinals 1–1999 and round
    thousands (NTS-13: Treccani grammar and vocabolario, DICO, Crusca).
    NTS-08 closes when every capability above is validated or explicitly deferred; a corrected
    string must be fixed in the `.feature` first, then in the configuration.
  - The remaining gaps below are explicit decisions, each pinned by a "does not support"
    scenario:
    - SW and ZU ordinals: an obligatory noun-class concord, no standalone form; needs a public
      `nounClass` dimension and a documented class inventory.
    - EE ClockTime: no sourced minute convention.
    - WO ClockTime: competing native and French-derived conventions, no single sourced system.

- **NTS-15 — Italian ordinals of non-round thousands above 1999.** NTS-13 makes 1–1999 and the
  round thousands up to 999000 productive; `ItalianOrdinalLanguageSpecifics` still rejects 2001,
  21001, 100001 … with `NotSupportedException`. The consulted sources do not establish a canonical
  synthetic form there: DICO lists only `milleunesimo, milleduesimo ecc.`, and Treccani
  ("centomillesimo") gives the analytic `centomillesimoprimo, centomillesimosecondo` for 100001+,
  the synthetic `centomiladuesimo` being a partitive. Needs a sourced decision between the
  synthetic (`duemilaunesimo`) and analytic (`duemillesimo primo`) forms, per range, before the
  guard can be lifted. Millions stay blocked by NTS-14.

- **NTS-14 — findings outside the NTS-10/11/12 scope (not fixed).**
  - IT millions and above: `uno Millione`, `due Millioni` (should be `un milione`, `due milioni`),
    and the junction with a following thousands group is not handled.
  - AR thousands morphology: `اثنان ألف`, `ثلاثة ألف`, `واحد وعشرون ألف` instead of the dual and
    plural forms (`ألفان`, `ثلاثة آلاف`, `واحد وعشرون ألفًا`); only the `و` connector was in scope.
  - HE agreeing-cardinal ordinals ending in a teen inside a larger number keep the counting
    (feminine) teen without a gender: `111 → מאה ואחת עשרה`, `1011 → אלף ואחת עשרה` (11–19 alone
    are masculine exceptions).
  - Suffix-formed ordinals of zero in other languages, which NTS-12 deliberately accepts as explicit
    formations but which look unsourced: RU `нолый` (expected `нулевой`), DE `nullste` (Duden:
    `nullte`), CA `zeroè`, WO `seroël`, EE `etsõ zero`.

- **NTS-09 — `Trigger` elements are not inherited through `baseOn`.** `XmlSerializer` materializes
  an absent `<Trigger>` list as an empty list, so `MergeLanguageDefinition`'s
  `overriding.Triggers ?? inherited.Triggers` never falls back to the base. No built-in configuration
  uses triggers today; the README and XSD document the current behaviour. Fix by treating an empty
  list as absent (as `Cultures` already does) together with a regression test.

NTS-01 through NTS-05 and NTS-10 through NTS-13 are closed:

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
- NTS-10 (IT/HI/AR cardinals), NTS-11 (HE thousands), NTS-12 (zero ordinal) and the `<Fusion>`
  primitive: `DONE-2026-10-03.md`.
- NTS-13 (Italian compound ordinals) and the `<OrdinalStem>` primitive: `DONE-2026-10-06.md`.

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
