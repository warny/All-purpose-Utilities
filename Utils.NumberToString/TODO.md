# Utils.NumberToString — Current backlog

Re-audited on 2026-10-08 after closing NTS-25A (Cyrillic Conway tables, RU/BG/UK short scale, SW/TR
junctions; NTS-25B left open for ID/MS and the TR/SW alphabets), and after closing NTS-24 (strict Conway-Guy-Wechsler scale names shared by every
SCALE-SHORT and SCALE-LONG language; opened NTS-25), on 2026-10-07 after closing NTS-23 (Ewe large scales:
Conway-Wechsler short scale inherited from SCALE-SHORT, unbounded domain; opened NTS-24), after closing NTS-22 (Ewe millions: static `miliɔn`,
domain 0–999 999 999; opened NTS-23), after closing NTS-20 (Ewe cardinals rebuilt to 999 999 and `-lia` ordinals
restored, with the generic `<Groups onScale>` and `multiplierPosition` primitives; opened NTS-22), after
closing NTS-19 (Wolof cardinals and ordinals to 999 999; opened NTS-21),
after closing NTS-16 (Wolof ordinals corrected, Ewe ordinals withdrawn; opened NTS-19 and NTS-20)
and after closing NTS-15 and NTS-18 (Italian ordinals complete: every
category is either supported on sourced evidence or a deliberate fail-closed limitation); on 2026-10-06 after closing NTS-17 and narrowing NTS-15 (which opened NTS-18), after
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
      JA, KO, ZH, EU.
    Validated so far: NL (Taaladvies), EU (EHU grammar), KO (National Institute of Korean
    Language), SW (two sources) and AR (university textbook) ClockTime; NO, BG, HR, RO and DA 100
    (`hundrede`) ordinals; SK compound ordinal spelling; HR cardinals; IT ordinals 1–1999 (except
    1110–1910) and round thousands (NTS-13: Treccani grammar and vocabolario, DICO, Crusca), 1010
    and 100001–100009 (NTS-15: Treccani "ordinale", "centomillesimo"), the round multiples of
    milione, miliardo and bilione (NTS-17: Treccani entries, CNR), and of biliardo and trilione
    (NTS-18: MIT/ADN table, Nuovo De Mauro, GDLI); WO cardinals 0–999 999 and ordinals (NTS-16, NTS-19: decree
    2005-992, Kosogorova 2023, Robert 2021, Gaye 1980; large ordinals apply the stated rule); EE
    cardinals 0–999 999 and ordinals (NTS-20: Dzablu-Kumah, *Basic Ewe for Foreign Students*; the Biblica
    Ewe Bible corpus; Ewe Basic Course 1968; large ordinals apply the stated rule). The remaining Italian values (1110–1910, other
    non-round thousands, non-round values from a million) are deliberate linguistic limitations,
    not open work: see `DONE-2026-10-07.md`.
    NTS-08 closes when every capability above is validated or explicitly deferred; a corrected
    string must be fixed in the `.feature` first, then in the configuration.
  - The remaining gaps below are explicit decisions, each pinned by a "does not support"
    scenario:
    - SW and ZU ordinals: an obligatory noun-class concord, no standalone form; needs a public
      `nounClass` dimension and a documented class inventory.
    - EE ClockTime: no sourced minute convention.
    - WO ClockTime: competing native and French-derived conventions, no single sourced system.
    - EE ordinal of zero: no attested form (`ZeroOrdinalUnsupportedLanguageSpecifics`).
    - WO ordinals of zero and of the round thousands (last element `junni`): no attested ordinal of
      zero, conflicting forms for `junni` (`junneel`/`junniéél`); deliberate limitations.

- **NTS-21 — Wolof million-scale cardinals.** Found by NTS-19, which kept `maxNumber="999999"`. The
  configuration's unused scale suffix `milyon` is unaudited: Boston University's 200 Word Project
  gives a million as `fukki téeméeri junni` (the regular 10 × 100 × 1000 with the connective) or
  `benn milyoŋ`, with the velar nasal. Decide the canonical form (decree 2005-992 alphabet for `ŋ`,
  a grammar or dictionary for the loan) before opening the domain above 999 999.
- **NTS-25 — Localized Conway names in derived scales (partially closed).** Found by NTS-24, pre-existing.
  Project rule: once a language has adopted the mi/bi/tri/… large-number family, the library extends it
  productively with Conway-Wechsler, using that language's own transliteration of the prefix tables (a
  generation convention of the library, not a claim about the languages' norms).
  - *NTS-25A closed* (`DONE-2026-10-08(1).md`): Cyrillic Conway tables (`SCALE-SHORT-CYRILLIC`, multi-letter
    linking marker `кс`), RU/BG/UK moved to the short scale with a static milliard, SW junction (`li` + `oni`),
    TR junction (`l` + `yon`) and TR case variants on every generated name.
  - **NTS-25 remaining (NTS-25B): audit ID/MS localized Conway prefix tables beyond Scale0Prefixes.** Their
    `Scale0Prefixes` are already localized (`kuadri`, `kuinti`, `seksti`, `okti`…) while the units/tens/hundreds
    tables are the shared Latin ones, and `groupSeparator=""` still loses the junction above the 999th
    -illion (ID n = 1000 `miniliun`). Same iteration, decided by the owner: adapt the TR and SW Latin tables
    to their alphabets (no q or x in Turkish or Swahili: TR `quattuordecilyon`, `sexcentilyon`, `decilyon`
    read with Turkish c; SW `quadrilioni`, `quintilioni`, `sextilioni` — the SW 1–9 forms would change too).
  - Accepted limitation (engine, owner decision): one `groupSeparator` serves both between Conway groups
    and before the suffix, so UK n = 1000 is `мільнільйон` and TR `milnilyon` (not `мілінільйон`/`milinilyon`).
- **NTS-09 — `Trigger` elements are not inherited through `baseOn`.** `XmlSerializer` materializes
  an absent `<Trigger>` list as an empty list, so `MergeLanguageDefinition`'s
  `overriding.Triggers ?? inherited.Triggers` never falls back to the base. No built-in configuration
  uses triggers today; the README and XSD document the current behaviour. Fix by treating an empty
  list as absent (as `Cultures` already does) together with a regression test.

NTS-01 through NTS-05 and NTS-10 through NTS-20, NTS-22, NTS-23 and NTS-24, are closed:

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
- NTS-15 (Italian non-round thousands, 1110–1910) and NTS-18 (Italian ordinals from a biliardo,
  non-round values from a million): round biliardo/trilione ordinals implemented, the unsourced
  families closed as deliberate fail-closed limitations: `DONE-2026-10-07.md`.
- NTS-16 (Wolof and Ewe ordinals): Wolof `-eel` on 1–999, Ewe ordinals withdrawn pending NTS-20:
  `DONE-2026-10-07(1).md`.
- NTS-19 (Wolof cardinals: orthography and thousands; ordinals from 1000, spelling `-éel`):
  `DONE-2026-10-07(2).md`.
- NTS-20 (Ewe cardinals rebuilt, `-lia` ordinals restored; generic `<Groups onScale>` and
  `multiplierPosition` primitives): `DONE-2026-10-07(3).md`.
- NTS-22 (Ewe millions: static `miliɔn`, domain 0–999 999 999; billions deferred to NTS-23):
  `DONE-2026-10-07(4).md`.
- NTS-23 (Ewe large scales: Conway-Wechsler short scale from SCALE-SHORT, `akpe`/`miliɔn`/`biliɔn`/
  `triliɔn` anchors, unbounded domain; `kpakple` not corroborated): `DONE-2026-10-07(5).md`.
- NTS-24 (strict Conway-Guy-Wechsler scale names for every SCALE-SHORT/SCALE-LONG language: `uni` → `un`,
  `vingti` → `viginti`, terminal `[a|-illi=>i]` endings, grouped names above the 999th restarting from
  `Scale0Prefixes`, single Conway linking consonant): `DONE-2026-10-08.md`.
- NTS-25A (part of NTS-25: Cyrillic Conway tables, RU/BG/UK short scale with a static milliard, SW and TR
  Conway junctions; NTS-25B stays open above): `DONE-2026-10-08(1).md`.

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
