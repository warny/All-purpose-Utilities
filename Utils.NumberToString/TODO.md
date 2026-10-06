# Utils.NumberToString — Current backlog

Re-audited on 2026-10-06 after closing NTS-14 (which opened NTS-16 and NTS-17) and NTS-13 (which
opened NTS-15); on 2026-10-03 after closing
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
    (`hundrede`) ordinals; SK compound ordinal spelling; HR cardinals; IT ordinals 1–1999 (except
    1010–1910) and round thousands (NTS-13: Treccani grammar and vocabolario, DICO, Crusca).
    NTS-08 closes when every capability above is validated or explicitly deferred; a corrected
    string must be fixed in the `.feature` first, then in the configuration.
  - The remaining gaps below are explicit decisions, each pinned by a "does not support"
    scenario:
    - SW and ZU ordinals: an obligatory noun-class concord, no standalone form; needs a public
      `nounClass` dimension and a documented class inventory.
    - EE ClockTime: no sourced minute convention.
    - WO ClockTime: competing native and French-derived conventions, no single sourced system.

- **NTS-15 — Italian ordinals of non-round thousands above 1999 and of 1010–1910.** NTS-13 makes
  1–1999 (except 1010–1910) and the round thousands up to 999000 productive;
  `ItalianOrdinalLanguageSpecifics` still rejects 1010, 1110 … 1910 and 2001, 21001, 100001 … with
  `NotSupportedException`. For the thousands ending in ten, *dieci* keeps its lexical *decimo* in
  compounds (`centodecimo`, Crusca) but only the analytic `millesimo decimo` is attested (Treccani),
  no synthetic form. The consulted sources do not establish a canonical
  synthetic form there: DICO lists only `milleunesimo, milleduesimo ecc.`, and Treccani
  ("centomillesimo") gives the analytic `centomillesimoprimo, centomillesimosecondo` for 100001+,
  the synthetic `centomiladuesimo` being a partitive. Needs a sourced decision between the
  synthetic (`duemilaunesimo`) and analytic (`duemillesimo primo`) forms, per range, before the
  guard can be lifted. Millions are tracked separately (NTS-17).

- **NTS-16 — Wolof and Ewe ordinal formation unsourced.** Found while deciding their ordinal of
  zero (NTS-14, now fail-closed). The consulted sources form Wolof ordinals with `-eel`/`-éél`
  (`ñaaréél`, Janga Wolof) whereas the configuration writes `-ël` (`ñaarël`), and Ewe ordinals with
  a `-lia` suffix (`evelia`, `etɔ̃lia`, Omniglot/Wiktionary) whereas the configuration prefixes
  `etsõ`. The configured zero cardinals (`sero`, `zero`) also differ from the attested `tus`/`dara`
  and `nadeke`. Needs a normative orthography source for each language before changing them.

- **NTS-17 — Italian ordinals of one million and above.** NTS-14 fixed the cardinals
  (`un milione`, `due milioni e centomila`), but no ordinal is validated there: Treccani attests
  `milionesimo`, while the mechanical form of `un milione` would be the fractional
  `un milionesimo`, and compound forms are unsourced. `ItalianOrdinalLanguageSpecifics` keeps
  rejecting them.

- **NTS-09 — `Trigger` elements are not inherited through `baseOn`.** `XmlSerializer` materializes
  an absent `<Trigger>` list as an empty list, so `MergeLanguageDefinition`'s
  `overriding.Triggers ?? inherited.Triggers` never falls back to the base. No built-in configuration
  uses triggers today; the README and XSD document the current behaviour. Fix by treating an empty
  list as absent (as `Cultures` already does) together with a regression test.

NTS-01 through NTS-05 and NTS-10 through NTS-14 are closed:

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
- NTS-14 (IT millions, AR thousands, HE compound ordinals, zero ordinals) with scale lexical forms
  and ordinal-only replacements: `DONE-2026-10-06(1).md`.

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
