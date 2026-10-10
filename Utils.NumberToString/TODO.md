# Utils.NumberToString — Current backlog

Re-audited on 2026-10-10 after closing NTS-27 (cardinal defects found by NTS-08: SV, PL, RU, VN, JA/KO/ZH, HI,
EU, ES; generic `groupPattern` and `innerString`/`innerBuildString` primitives; opened NTS-30), on 2026-10-09 after closing NTS-08 (final linguistic validation of the ordinals and ClockTime;
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

- **NTS-28 — compound ordinals the declarative pipeline does not build.** Sourced rules exist but need a plugin
  (the last-word transformation cannot produce them); the values fail closed today:
  - ES (DPD: *trigésimo primero*, *centésimo primero*, *ducentésimo*, *dosmilésimo*), PT (*vigésimo primeiro*,
    *ducentésimo*), GL (*vixésimo primeiro*), EL (*εικοστός πρώτος*, *εκατοστός πρώτος*, *δισχιλιοστός*), FI
    (*kahdeskymmenesensimmäinen*, and every inflected case: genitive *kolmannen*, partitive *kolmatta*).
  - PL/RU from 2000 (*dwutysięczny*, *dwa tysiące pierwszy*; *двухтысячный*, *две тысячи первый*). Since NTS-27
    the cardinals agree (*dwa tysiące*, *две тысячи*): no longer blocked by the cardinal.
  - Guards kept by NTS-08 because the cardinal was wrong, no longer blocked since NTS-27 (the cardinal is now
    correct; the ordinal still needs a source): VN from 1000 (*thứ một nghìn*?), HI from 100 000 (*एक लाखवाँ*?),
    JA/KO above 1000 (第一万, 제일만?), ZH 101–999 non-round and from 1000 (第一百零一?), EU above 1000
    (*mila eta batgarren*?), SV cardinals now one word (the plugin builds its own ordinals; unchanged).
  - UK round thousands with a multiplier above ten and round millions/milliards above one (Pravopys 2019 §38:
    *п'ятсоттридцятитисячний*, *чотирьохмільйонний*, *семимільярдний*).
  - SV above one million (no consulted source settles *två miljonte* vs *tvåmiljonte*; *miljardte* unattested
    in SAOB).
  - AR above 100 (*المئة والسابع والثلاثون*, after M. Bussey/EMSA), not enabled by NTS-08.
- **NTS-30 — cardinal defects found by NTS-27 outside its list.** Recorded, not fixed (NTS-27 kept to its ticket):
  - ES/EU long scale: a milliard followed by millions is not composed (`dos mil millones quinientos millones` for
    DPD *dos mil quinientos millones*, "3500 millones (tres mil quinientos millones)"; `mil millones un millón`
    for *mil un millones*; EU `bi mila milioi eta bostehun milioi`). The multiplier of millón/milioi spans six
    digits and needs a recursive multiplier, which neither the uniform groups nor `groupPattern` express.
  - UK, BG, CS, SK: the thousands and millions do not agree with their multiplier (`два тисяч`, `две хиляда`,
    `dva tisíc`, `dva milion`); the NTS-27 `ScaleForm` + selector mechanism applies (CLDR uk = ru rule; cs/sk
    have one/few/many with different ranges), with sources per language.
  - RU above the milliard: the generated Conway names keep the bare singular (`два триллион`); `ScaleForm`
    only covers named indices.
  - VN: a whole zero class after a higher class is skipped (`một triệu không trăm linh một`), whereas the
    SGK Toán 4 solution keys read it `không nghìn` (*năm mươi triệu không nghìn một trăm linh hai*); needs a
    zero-group rendering and a normative source.
  - All languages: zero digits after the decimal separator are dropped (`21.05` → `twenty-one point  five` in
    most configurations, `二十一点五` in JA/ZH); the decimal path reads the level-1 digit text, empty for 0.
  - ES currency: `uno euro` for DPD *un euro* (apocope before a noun); the caller-defined currency wording does
    not force the attributive form.
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


NTS-01 through NTS-05, NTS-08, NTS-09, NTS-10 through NTS-25 and NTS-27 are closed:

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
- NTS-27 (cardinal defects found by NTS-08: SV one word and *en*, PL/RU agreement of the thousands, VN classes
  read in full, JA/KO/ZH counting by 万/만 with 零/一十 and 제44항 spacing, HI lakh/crore and पाँच, EU rule 7,
  ES apocope; generic `groupPattern` and `innerString`/`innerBuildString`, `RussianPluralLexicalFormSelector` and
  `PolishPluralLexicalFormSelector`; NTS-30 opened): `DONE-2026-10-10.md`.

Full multi-form plural systems (Russian/Slavic count-dependent noun forms,
Arabic dual/paucal/plural categories) were long deliberately out of scope — the
`ILexicalFormSelector` architecture supports them without redesign. Arabic (NTS-14)
and Russian/Polish scale nouns (NTS-27) now use them; the other Slavic languages are
NTS-30. See the "Deliberately deferred" section of `DONE-2026-08-25(1).md` for the
original design decision.

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
