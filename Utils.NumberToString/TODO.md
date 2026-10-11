# Utils.NumberToString — Current backlog

Re-audited on 2026-10-11 after closing the cardinal part of NTS-31 (Dutch long scale on `SCALE-LONG`: `miljoen`,
`miljard`, `biljoen` … with `-joen`/`-jard` and the `l` junction, Conway tables inherited, unbounded cardinals; the
ordinals from a million remain open under NTS-31), after closing NTS-29 (attested ordinals of zero: DA `nulte`, HU `nulladik`) and NTS-32
(Polish ordinal of 400: `czterechsetny`), after closing NTS-28 (compound and large-scale ordinals in ES, PT, GL, EL, FI, PL, RU, UK; guards
widened for JA, KO, ZH, VN, HI, EU; SV, AR deliberately fail-closed; NL blocked by the new NTS-31; NTS-32 opened),
on 2026-10-10 after closing NTS-27 (cardinal defects found by NTS-08: SV, PL, RU, VN, JA/KO/ZH, HI,
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

- **NTS-31 (remaining) — Dutch ordinals from a million.** The cardinal part of NTS-31 is closed
  (`DONE-2026-10-11(2).md`: NL on `SCALE-LONG`, unbounded long-scale cardinals). The ordinal guard
  `DutchOrdinalLanguageSpecifics` still stops at 999 999, explicitly and independently of the cardinal domain. To
  do: source the ordinals of the scale nouns (`miljoenste` vs `een miljoenste`, `tweemiljoenste` vs `twee
  miljoenste`, `miljardste`; whether the ordinal of a composite value keeps the cardinal groups, e.g. `een miljoen
  eerste`), add the `-ste` stems for `joen`/`jard`, then widen the guard.
- **NTS-30 — cardinal defects found by NTS-27 and NTS-28 outside their lists.** Recorded, not fixed:
  - FI (found by NTS-28): the thousands do not take the partitive after a multiplier (`kaksi tuhat` for
    *kaksituhatta*) and the compounds are spaced (`kaksikymmentä yksi`, Kielitoimiston ohjepankki writes numbers
    as one word); the Finnish ordinals from 2000 wait for this cardinal and a source for the round thousands.
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
- **NTS-26 — Wolof connective `-i` inside nested numeral multipliers.** Re-audited 2026-10-11:
  the evidence supports **real variation**, not a demonstrated defect in the current default. The current
  configuration writes the multiplier of `téeméer` with `-i` (`juróom ñenti téeméer` 900) and keeps that
  connective when the hundreds phrase is itself the multiplier of a higher scale
  (`juróom ñenti téeméeri milyoŋ`). This analysis is supported by Guérin (CNRS/LLACAN), who describes the
  multiples of hundreds and thousands as genitive constructions and gives nested forms such as
  `ñaar-i téeméer-i dërëm`; pedagogical sources also attest forms such as `juróom ñaari téeméer`,
  `ñenti téeméer` and `ñaari junni`. Conversely, the Wolof Ajami Reader (Ngom et al., Boston University,
  2025, p. 192) writes `juróom ñent téeméeri milyoŋ` and `juróom ñaar téeméeri milyoŋ`, i.e. without the
  inner `-i` before `téeméer`; Kosogorova 2023 and Gaye 1980 also record forms without that connective
  (Gaye treats `-i` as optional with `ñaar`). The decree 2005-992 establishes the general attached
  `-u/-i` genitive linker but does not settle these nested numeral chains. **Do not change the default output
  from NTS-19/NTS-21 on the present evidence.** Next step, if pursued: targeted corpus work on
  `... téeméeri milyoŋ/milyaar` and comparable nested hundreds, to determine whether the two patterns are
  dialectal/register variants, context-dependent, or freely optional. Only if that evidence justifies it,
  consider a variant dimension; avoid a silent default change because it would affect many byte-stable Wolof
  cardinals below and above a million.


NTS-01 through NTS-05, NTS-08, NTS-09, NTS-10 through NTS-25, NTS-27, NTS-28, NTS-29, NTS-32 and the cardinal part of NTS-31 are closed:

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
- NTS-28 (compound and large-scale ordinals: ES, PT, GL, EL, FI composed by `<OrdinalComposition>`/ordinal
  replacements, PL, RU, UK round values in their plugins, JA/KO/ZH/VN/HI/EU guards widened to the cardinal domain,
  SV/AR deliberately fail-closed, NL blocked by NTS-31; guard extension point `TryBuildOrdinal`; NTS-31, NTS-32
  opened): `DONE-2026-10-11.md`.
- NTS-29 (attested ordinals of zero: DA *nulte* through a Danish-only hook of the Scandinavian base, HU
  *nulladik*; NO, SV and PL still fail closed) and NTS-32 (Polish ordinal of 400 *czterechsetny* in its 30
  forms, after WSJP PAN and SJP PWN): `DONE-2026-10-11(1).md`.
- NTS-31, cardinal part (Dutch long scale: `baseOn="SCALE-LONG"`, `maxNumber` removed, suffixes `joen`/`jard` with
  `suffixSeparator="l"`, Conway tables inherited with the Latin `quadri`/`quinti`/`sexti`/`octi`, `een` kept from
  `miljoen` upward, singular scale nouns, spaced groups without `en`; ordinals still guarded at 999 999):
  `DONE-2026-10-11(2).md`.

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
