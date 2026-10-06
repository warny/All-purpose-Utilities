# Utils.NumberToString — Current backlog

Re-audited on 2026-10-06 after closing NTS-17 and narrowing NTS-15 (which opened NTS-18), after
closing NTS-14 (which opened NTS-16 and NTS-17) and NTS-13 (which opened NTS-15); on 2026-10-03 after closing
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
    1110–1910) and round thousands (NTS-13: Treccani grammar and vocabolario, DICO, Crusca), 1010
    and 100001–100009 (NTS-15: Treccani "ordinale", "centomillesimo"), and the round multiples of
    milione, miliardo and bilione (NTS-17: Treccani entries, CNR).
    NTS-08 closes when every capability above is validated or explicitly deferred; a corrected
    string must be fixed in the `.feature` first, then in the configuration.
  - The remaining gaps below are explicit decisions, each pinned by a "does not support"
    scenario:
    - SW and ZU ordinals: an obligatory noun-class concord, no standalone form; needs a public
      `nounClass` dimension and a documented class inventory.
    - EE ClockTime: no sourced minute convention.
    - WO ClockTime: competing native and French-derived conventions, no single sourced system.

- **NTS-15 — Italian ordinals of non-round thousands above 1999 and of 1110–1910 (narrowed).**
  Since 2026-10-06 the attested analytic forms are produced by `<OrdinalComposition>`: 1010 →
  `millesimo decimo` (Treccani "ordinale", separate spelling preferred) and 100001–100009 →
  `centomillesimoprimo` … (Treccani "centomillesimo", soldered). `ItalianOrdinalLanguageSpecifics`
  still rejects:
  - 1110, 1210 … 1910: two analytic splits are conceivable (`millesimo centodecimo`,
    `millecentesimo decimo`) and neither was found;
  - every other non-round thousand above 1999 (2001, 2010, 21001, 100010, 100100, 999999 …): the
    synthetic form (`duemilaunesimo`) is unattested and would be partitive by Treccani's
    `centomiladuesimo` remark, and the spelling of the analytic form (`duemillesimo primo` or
    `duemillesimoprimo`) is not sourced. Treccani's "ecc." after `centomillesimoterzo` was not
    extended beyond the units.
  Needs a sourced spelling, per range, before more of the guard is lifted; the engine side is done
  (`<OrdinalComposition>` rules per range).

- **NTS-16 — Wolof and Ewe ordinal formation unsourced.** Found while deciding their ordinal of
  zero (NTS-14, now fail-closed). The consulted sources form Wolof ordinals with `-eel`/`-éél`
  (`ñaaréél`, Janga Wolof) whereas the configuration writes `-ël` (`ñaarël`), and Ewe ordinals with
  a `-lia` suffix (`evelia`, `etɔ̃lia`, Omniglot/Wiktionary) whereas the configuration prefixes
  `etsõ`. The configured zero cardinals (`sero`, `zero`) also differ from the attested `tus`/`dara`
  and `nadeke`. Needs a normative orthography source for each language before changing them.

- **NTS-18 — Italian compound ordinals from one million, and ordinals from a biliardo.** Opened
  by NTS-17, which closed the round multiples of milione, miliardo and bilione (`<OrdinalScale>`).
  `ItalianOrdinalLanguageSpecifics` still rejects:
  - the non-round values from a million (1000001, 1001000, 2000001, 1000000001 …): no consulted
    source gives a compound ordinal there (analytic `milionesimo primo`? soldered? synthetic forms
    risk the partitive reading, as `centomiladuesimo` does);
  - every value from a biliardo (10^15), round or not: Treccani has no entry for `biliardesimo` or
    `trilionesimo`, so the productivity of the scale ordinal is not established there.
  Once sourced, the engine needs only configuration (`<OrdinalComposition>` ranges, a wider
  `<OrdinalScale scales>`).

- **NTS-09 — `Trigger` elements are not inherited through `baseOn`.** `XmlSerializer` materializes
  an absent `<Trigger>` list as an empty list, so `MergeLanguageDefinition`'s
  `overriding.Triggers ?? inherited.Triggers` never falls back to the base. No built-in configuration
  uses triggers today; the README and XSD document the current behaviour. Fix by treating an empty
  list as absent (as `Cultures` already does) together with a regression test.

NTS-01 through NTS-05, NTS-10 through NTS-14 and NTS-17 are closed:

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
- NTS-17 (Italian round million-scale ordinals) with the `<OrdinalScale>` and `<OrdinalComposition>`
  primitives, and the attested part of NTS-15 (1010, 100001–100009): `DONE-2026-10-06(2).md`.

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
