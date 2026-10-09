# Utils.NumberToString — Current backlog

Re-audited on 2026-10-09 after closing NTS-08 (final linguistic validation of the ordinals and ClockTime;
opened NTS-27, NTS-28 and NTS-29), after closing NTS-21 (Wolof large scales: attested `milyoŋ`/`milyaar`, long-scale
Conway-Wechsler extension in Wolof spelling, unbounded cardinals; `suffixSeparator` primitive) and NTS-09
(`Trigger` inheritance through `baseOn`), on 2026-10-08 after closing NTS-25 (NTS-25B: ID/MS, SW and TR Conway tables localized to their
alphabets; NTS-25A: Cyrillic Conway tables, RU/BG/UK short scale, SW/TR junctions), and after closing NTS-24 (strict Conway-Guy-Wechsler scale names shared by every
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

- **NTS-27 — cardinal defects found by the NTS-08 validation.** Each made an ordinal or clock wrong, so the
  affected ordinals now fail closed (`DONE-2026-10-09(1).md`); the cardinals themselves are unchanged:
  - SV: `ett miljon` (Språkrådet: *en miljon*), `tusen ett hundra`, `två tusen`, `tjugoett tusen` (one word below a
    million, *en* after the tens: Frågelådan 27138, 21216).
  - PL/RU: the thousands do not agree with the multiplier (`dwa tysiąc`, `два тысяча` for *dwa tysiące*, *две тысячи*).
  - VN: `nghìn` for *một nghìn*, `hai nghìn năm` for *hai nghìn không trăm linh năm*.
  - JA/KO/ZH: thousands split with spaces and no counting by 万/만 (`千 一`, `십 천`, `一 千`); ZH without 零 / 一十
    (`一百一`, `一百十`).
  - HI: `एक सौ हज़ार` for *एक लाख* (and the larger lakh/crore values); spelling `पांच` (anusvara) against the
    chandrabindu of Kamta Prasad Guru (`पाँच`).
  - EU: `mila bat` for *mila eta bat*, `bat milioi` for *milioi bat* (Euskaltzaindia rule 7).
  - ES: `veintiuno mil` for *veintiún mil*.
- **NTS-28 — compound ordinals the declarative pipeline does not build.** Sourced rules exist but need a plugin
  (the last-word transformation cannot produce them); the values fail closed today:
  - ES (DPD: *trigésimo primero*, *centésimo primero*, *ducentésimo*, *dosmilésimo*), PT (*vigésimo primeiro*,
    *ducentésimo*), GL (*vixésimo primeiro*), EL (*εικοστός πρώτος*, *εκατοστός πρώτος*, *δισχιλιοστός*), FI
    (*kahdeskymmenesensimmäinen*, and every inflected case: genitive *kolmannen*, partitive *kolmatta*).
  - PL/RU from 2000 (*dwutysięczny*, *dwa tysiące pierwszy*; *двухтысячный*, *две тысячи первый*), after NTS-27.
  - UK round thousands with a multiplier above ten and round millions/milliards above one (Pravopys 2019 §38:
    *п'ятсоттридцятитисячний*, *чотирьохмільйонний*, *семимільярдний*).
  - SV above one million (no consulted source settles *två miljonte* vs *tvåmiljonte*; *miljardte* unattested
    in SAOB).
  - AR above 100 (*المئة والسابع والثلاثون*, after M. Bussey/EMSA), not enabled by NTS-08.
- **NTS-29 — attested ordinals of zero not enabled.** DA *nulte* (Retskrivningsordbogen since 1986, Dansk
  Sprognævn SV00001178) and HU *nulladik* (MTA "Számok" tool) still fail closed; enabling them is additive.

- **NTS-26 — Wolof connective `-i` inside the multiplier of `téeméer` (`juróom ñent téeméeri milyoŋ`).**
  Found by NTS-21 (`DONE-2026-10-09.md`). The configuration follows the NTS-19 rule: the multiplier of
  `téeméer` takes `-i` on its last element (`juróom ñenti téeméer` 900, `juróom ñaari téeméer` 700), also when
  the hundreds are themselves a multiplier (`juróom ñenti téeméeri milyoŋ`). The Wolof Ajami Reader (Ngom et
  al., Boston University, 2025, p. 192) writes `juróomi milyaar ak juróom ñent téeméeri milyoŋ ak ñaar fukk ak
  juróom` and `juróom ñaar téeméeri milyoŋ`: no `-i` before `téeméer`, `-i` only before the scale noun.
  Kosogorova 2023 (`ñaar tééméér`) and Gaye 1980 (`-i` optional with `ñaar`) already record forms without the
  inner `-i`. Decide whether the inner `-i` is obligatory, optional (variant) or absent, possibly depending on
  the following scale noun, from more sources (Bible corpus counts, decree 2005-992, grammars). Any change
  alters cardinals below a million (every value with 200–999 in a hundreds position, e.g. 200, 900, 900 000),
  byte-identical since NTS-19; a variant dimension would keep the default output.


NTS-01 through NTS-05, NTS-08, NTS-09 and NTS-10 through NTS-25 are closed:

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
- NTS-25 (localized Conway names): NTS-25A (Cyrillic Conway tables, RU/BG/UK short scale with a static
  milliard, SW and TR Conway junctions, multi-letter linking markers) in `DONE-2026-10-08(1).md`; NTS-25B
  (ID/MS, SW and TR Conway tables localized to their alphabets, ID/MS junctions) in `DONE-2026-10-08(2).md`.
- NTS-21 (Wolof large scales: attested `milyoŋ`/`milyaar`, productive long-scale Conway-Wechsler names in
  Wolof spelling, `-i` on the multipliers, unbounded cardinals, ordinals kept to 999 999; the
  `suffixSeparator` primitive): `DONE-2026-10-09.md`.
- NTS-09 (`Trigger` elements inherited through `baseOn`; an empty list is read as absent), closed with NTS-21,
  which introduced the first production trigger: `DONE-2026-10-09.md`.
- NTS-08 (linguistic validation of every ordinal and ClockTime capability: validated, corrected or fail-closed;
  `OrdinalDomainGuardLanguageSpecifics`; NTS-27, NTS-28 and NTS-29 opened): `DONE-2026-10-09(1).md`, after the
  implementation passes of `DONE-2026-10-01(1).md`.

Full multi-form plural systems (Russian/Slavic count-dependent noun forms,
Arabic dual/paucal/plural categories) are deliberately out of scope — the
`ILexicalFormSelector` architecture supports them without redesign, but no
production language uses more than two forms yet. See the "Deliberately
deferred" section of `DONE-2026-08-25(1).md`. This is design headroom, not an
open backlog item.

New findings should be appended here as they are identified, and archived to
a dated `DONE-*.md` file once resolved, per the repository's `AGENTS.md`
TODO/DONE convention.


### NTS-08 final validation (2026-10-09, closed)

See `DONE-2026-10-09(1).md`.

### NTS-08 completion pass (2026-10-01)

See `DONE-2026-10-01(1).md`.

### NTS-08 completed slice (2026-09-23)

- Split Belgian and Swiss French into `baseOn="FR"` children; Belgian keeps `quatre-vingts` while Swiss keeps `huitante`.
- Split Valencian into a `baseOn="CA"` child with its own clock convention.
- Split Malay into a `baseOn="ID"` child, preserving the Malay `lapan` stem and a distinct `pukul` clock convention.
- Added productive Indonesian/Malay ordinal plugins handling suppletive `pertama` and `ke-` formation over language-specific cardinal stems.
- Added English and Catalan ClockTime rules. The remaining languages were completed on 2026-10-01 and validated on 2026-10-09 (see above).
